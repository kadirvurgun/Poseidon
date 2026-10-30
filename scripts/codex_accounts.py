"""Bounded Codex workers with separate OAuth homes, worktrees and quota handoff.

No tokens are read/copied by this script. Account metadata comes from the documented
app-server interface. A produced patch always awaits independent QA and CI.
"""

import argparse
from concurrent.futures import ThreadPoolExecutor
from contextlib import contextmanager
import json
import os
from pathlib import Path
import queue
import re
import shutil
import signal
import subprocess
import threading
import time
import uuid

ROOT = Path(__file__).resolve().parents[1]
QUOTA_CODES = {'usage_limit_reached', 'rate_limit_exceeded', 'UsageLimitExceeded'}


class BusyError(RuntimeError):
    pass


class MetadataError(RuntimeError):
    """Safe protocol diagnostics with no raw account response."""


class WorkerStillRunningError(Exception):
    """Termination is uncertain: preserve locks and require reconciliation."""


@contextmanager
def exclusive(path):
    path.parent.mkdir(parents=True, exist_ok=True)
    release = True
    try:
        descriptor = os.open(path, os.O_CREAT | os.O_EXCL | os.O_WRONLY, 0o600)
    except FileExistsError:
        raise BusyError('Worker/job already locked; reconcile an interrupted process before retrying.') from None
    try:
        with os.fdopen(descriptor, 'w') as stream:
            json.dump({'pid': os.getpid(), 'started_at': time.time()}, stream)
        yield
    except WorkerStillRunningError:
        release = False
        raise
    finally:
        if release:
            path.unlink()


def write_json(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_suffix('.tmp')
    temporary.write_text(json.dumps(value, indent=2, ensure_ascii=False) + '\n', encoding='utf-8', newline='\n')
    os.replace(temporary, path)


def quota_error(event):
    if event.get('type') not in {'error', 'turn.failed'}:
        return False

    def classified(value):
        if isinstance(value, dict):
            if value.get('code') in QUOTA_CODES or value.get('codex_error_info') == 'UsageLimitExceeded':
                return True
            message = value.get('message', '')
            if isinstance(message, str) and message.startswith("You've hit your usage limit"):
                return True
            return any(classified(item) for item in value.values())
        if isinstance(value, list):
            return any(classified(item) for item in value)
        return False

    return classified(event)


def evaluate_run(returncode, output):
    events = []
    for line in output.splitlines():
        try:
            event = json.loads(line)
            if isinstance(event, dict):
                events.append(event)
        except json.JSONDecodeError:
            continue
    failed = [event for event in events if event.get('type') in {'error', 'turn.failed'}]
    completed = [event for event in events if event.get('type') == 'turn.completed']
    last_failure = max((i for i, event in enumerate(events) if event.get('type') in {'error', 'turn.failed'}), default=-1)
    last_success = max((i for i, event in enumerate(events) if event.get('type') == 'turn.completed'), default=-1)
    if returncode == 0 and completed and last_success > last_failure:
        # Persist usage numbers only, never raw tool output, prompts or auth data.
        usage = completed[-1].get('usage', {})
        return 'produced', {key: value for key, value in usage.items() if isinstance(value, (int, float))}
    if failed and last_failure > last_success and quota_error(failed[-1]):
        return 'quota', None
    return 'failed', None


def summarize_account(profile, account, limits):
    account = account or {}
    plan = account.get('planType')
    buckets = limits.get('rateLimitsByLimitId') or {}
    bucket = buckets.get('codex') or limits.get('rateLimits') or {}
    windows = [bucket[key] for key in ('primary', 'secondary') if isinstance(bucket.get(key), dict)]
    exhausted = any(window.get('usedPercent', 0) >= 100 for window in windows)
    authenticated = account.get('type') == 'chatgpt'
    matches = plan in profile.get('accepted_plans', [profile['expected_plan']])
    return {
        'profile': profile['id'], 'authenticated': authenticated, 'plan': plan,
        'plan_matches': matches, 'exhausted': exhausted,
        'limits_known': bool(windows),
        'ready': authenticated and matches and bool(windows) and not exhausted,
        'windows': [{key: window.get(key) for key in ('usedPercent', 'windowDurationMins', 'resetsAt')} for window in windows],
    }


class Workers:
    def __init__(self, root=ROOT, home_root=None):
        self.root = Path(root).resolve()
        self.config = json.loads((self.root / 'automation/codex-accounts.json').read_text(encoding='utf-8'))
        self.profiles = {item['id']: item for item in self.config['profiles']}
        if set(self.profiles) != {'esra-pro', 'kadir-plus'} or len(self.config['profiles']) != 2:
            raise ValueError('Exactly the two declared account profiles are required.')
        if not 1 <= self.config['max_accounts_per_attempt'] <= 2 or not 1 <= self.config['timeout_seconds'] <= 7200:
            raise ValueError('Worker limits must be bounded.')
        self.home_root = Path(home_root or Path.home() / '.poseidon-codex').resolve()
        self.runtime = self.root / '.runtime' / 'account-workers'

    def home(self, profile):
        if profile not in self.profiles:
            raise ValueError('Unknown account profile.')
        return self.home_root / profile

    def initialize(self):
        for profile in self.profiles:
            home = self.home(profile)
            home.mkdir(parents=True, exist_ok=True)
            config = home / 'config.toml'
            if not config.exists():
                config.write_text('cli_auth_credentials_store = "keyring"\n', encoding='utf-8', newline='\n')

    def environment(self, profile):
        environment = os.environ.copy()
        environment['CODEX_HOME'] = str(self.home(profile))
        # These workers must use their explicit ChatGPT login, not an ambient API key.
        for key in ('OPENAI_API_KEY', 'CODEX_API_KEY', 'CODEX_ACCESS_TOKEN'):
            environment.pop(key, None)
        sdk = self.root / '.tools/dotnet'
        if sdk.is_dir():
            environment['DOTNET_ROOT'] = str(sdk)
            environment['PATH'] = str(sdk) + os.pathsep + environment.get('PATH', '')
        return environment

    def command(self):
        native = self.root / '.tools/codex/node_modules/@openai/codex-win32-x64/vendor/x86_64-pc-windows-msvc/bin/codex.exe'
        if os.name == 'nt' and native.is_file():
            return [str(native), '--no-daemon', '-c', 'cli_auth_credentials_store="keyring"']
        node = shutil.which('node')
        cli = self.root / '.tools/codex/node_modules/@openai/codex/bin/codex.js'
        if not node or not cli.is_file():
            raise RuntimeError('Install Node and the project-local Codex CLI first.')
        return [node, str(cli), '--no-daemon', '-c', 'cli_auth_credentials_store="keyring"']

    def login(self, profile):
        self.initialize()
        with exclusive(self.home(profile) / 'worker.lock'):
            kwargs = {'creationflags': subprocess.CREATE_NEW_PROCESS_GROUP} if os.name == 'nt' else {'start_new_session': True}
            process = subprocess.Popen(self.command() + ['login', '--device-auth'], env=self.environment(profile), **kwargs)
            try:
                return process.wait()
            except BaseException:
                self.stop(process)
                raise

    def account(self, profile):
        self.initialize()
        kwargs = {'creationflags': subprocess.CREATE_NEW_PROCESS_GROUP} if os.name == 'nt' else {'start_new_session': True}
        process = subprocess.Popen(self.command() + ['app-server'], env=self.environment(profile),
                                   stdin=subprocess.PIPE, stdout=subprocess.PIPE, stderr=subprocess.DEVNULL,
                                   text=True, encoding='utf-8', **kwargs)
        messages = queue.Queue()

        def receive():
            for line in process.stdout:
                try:
                    messages.put(json.loads(line))
                except json.JSONDecodeError:
                    continue
            messages.put(None)

        threading.Thread(target=receive, daemon=True).start()

        def request(identifier, method, params=None):
            process.stdin.write(json.dumps({'id': identifier, 'method': method, 'params': params or {}}) + '\n')
            process.stdin.flush()
            deadline = time.monotonic() + 30
            while True:
                remaining = deadline - time.monotonic()
                if remaining <= 0:
                    raise RuntimeError('Account metadata request timed out; no worker dispatched.')
                try:
                    message = messages.get(timeout=remaining)
                except queue.Empty:
                    raise RuntimeError('Account metadata request timed out; no worker dispatched.') from None
                if message is None:
                    raise RuntimeError('Account metadata service exited; no worker dispatched.')
                if message.get('id') == identifier:
                    if 'error' in message:
                        error = message['error']
                        text = str(error.get('message', '')).lower()
                        terms = ('keyring', 'refresh', 'expired', 'unauthorized', 'timeout', 'permission', 'credential', 'storage', 'token', 'account', 'json', 'locked', 'initialize', 'unsupported', 'file', 'auth', 'path', 'database', 'connection', 'socket', 'network', 'client', 'server', 'proxy', 'tls', 'certificate', 'version', 'cache', 'deserialize')
                        category = ','.join(name for name in terms if name in text) or 'unclassified'
                        # Include only a numeric protocol code and a fixed category, never a raw auth error.
                        code = error.get('code')
                        code = code if isinstance(code, int) else 'unknown'
                        raise MetadataError(f'Account metadata unavailable (code={code}, category={category}); no worker dispatched.')
                    return message['result']

        try:
            request(1, 'initialize', {'clientInfo': {'name': 'poseidon-account-workers', 'version': '1.0.0'}})
            process.stdin.write('{"method":"initialized"}\n')
            process.stdin.flush()
            account = request(2, 'account/read', {'refreshToken': False}).get('account')
            limits = request(3, 'account/rateLimits/read') if account else {}
            return summarize_account(self.profiles[profile], account, limits)
        finally:
            self.finish_metadata(process)

    @staticmethod
    def finish_metadata(process):
        try:
            process.stdin.close()
            process.wait(timeout=5)
        except BaseException as error:
            Workers.stop(process)
            if not isinstance(error, subprocess.TimeoutExpired):
                raise
        finally:
            if process.poll() is not None:
                process.stdout.close()

    @staticmethod
    def stop(process):
        # Only stop this worker's own process tree; an unknown/still-running worker is never handed off.
        try:
            if os.name == 'nt':
                stopped = subprocess.run(['taskkill', '/PID', str(process.pid), '/T', '/F'], capture_output=True, check=False, timeout=10)
                if stopped.returncode != 0:
                    raise WorkerStillRunningError('Worker termination could not be verified. Keep locks and reconcile the process tree.')
            else:
                try:
                    os.killpg(process.pid, signal.SIGKILL)
                except ProcessLookupError:
                    pass
            process.wait(timeout=10)
        except WorkerStillRunningError:
            raise
        except BaseException:
            raise WorkerStillRunningError('Worker termination failed. Keep locks and reconcile the process tree.') from None

    def execute(self, profile, worktree, prompt):
        kwargs = {'creationflags': subprocess.CREATE_NEW_PROCESS_GROUP} if os.name == 'nt' else {'start_new_session': True}
        process = subprocess.Popen(self.command() + ['-C', str(worktree), '-a', 'never', 'exec', '-s', 'danger-full-access', '--json', '-'],
                                   env=self.environment(profile), stdin=subprocess.PIPE, stdout=subprocess.PIPE,
                                   stderr=subprocess.DEVNULL, text=True, encoding='utf-8', **kwargs)
        try:
            output, _ = process.communicate(prompt, timeout=self.config['timeout_seconds'])
        except subprocess.TimeoutExpired:
            self.stop(process)
            return 'timeout', None
        except BaseException:
            self.stop(process)
            raise
        return evaluate_run(process.returncode, output)

    def run(self, task_file, preferred=None, *, single_account=False, eligibility=None):
        preferred = preferred or next(iter(self.profiles))
        if preferred not in self.profiles:
            raise ValueError('Unknown preferred account profile.')
        task = Path(task_file).read_text(encoding='utf-8').strip()
        if not task:
            raise ValueError('Task file is empty.')
        job_id = uuid.uuid4().hex
        directory = self.runtime / 'jobs' / job_id
        directory.mkdir(parents=True)
        (directory / 'task.md').write_text(task, encoding='utf-8', newline='\n')
        worktree = self.runtime / 'worktrees' / job_id
        worktree.parent.mkdir(parents=True, exist_ok=True)
        branch = 'agents/' + job_id
        subprocess.run(['git', 'worktree', 'add', '-b', branch, str(worktree), 'HEAD'], cwd=self.root, check=True, capture_output=True)
        state = {'job_id': job_id, 'branch': branch, 'worktree': str(worktree), 'status': 'queued', 'attempts': []}
        write_json(directory / 'state.json', state)
        return self.dispatch(directory, state, preferred, single_account=single_account, eligibility=eligibility)

    def resume(self, job_id, preferred=None):
        if not re.fullmatch('[0-9a-f]{32}', job_id):
            raise ValueError('Invalid job ID.')
        directory = self.runtime / 'jobs' / job_id
        return self.dispatch(directory, None, preferred or next(iter(self.profiles)))

    def dispatch(self, directory, state, preferred, *, single_account=False, eligibility=None):
        order = [preferred] + ([] if single_account else [profile for profile in self.profiles if profile != preferred])
        task = (directory / 'task.md').read_text(encoding='utf-8')
        with exclusive(directory / 'job.lock'):
            if state is None:
                state = json.loads((directory / 'state.json').read_text(encoding='utf-8'))
                if state['status'] not in {'waiting_for_quota', 'waiting_for_account'}:
                    raise ValueError('Only quota/account-waiting jobs may automatically resume. Reconcile other failures first.')
            attempt_start = len(state['attempts'])
            for profile in order[:self.config['max_accounts_per_attempt']]:
                try:
                    with exclusive(self.home(profile) / 'worker.lock'):
                        status = self.account(profile)
                        if not status['ready'] or (eligibility is not None and not eligibility(status)):
                            state['attempts'].append({'profile': profile, 'result': 'quota' if status['exhausted'] else 'account-unavailable'})
                            continue
                        state['status'] = 'running'
                        write_json(directory / 'state.json', state)
                        prompt = ('Work on this POSEIDON task in this isolated worktree. Read AGENTS.md and the applicable role skill. '
                                  'A prior worker may have stopped at its quota: inspect git status/diff and reconcile existing changes before continuing. '
                                  'Preserve partial work. Produce code and tests for independent QA; leave publishing/merge to the coordinator. '
                                  'Do not mark the task accepted. Never expose credentials or personal data in output.\n\n' + task)
                        result, usage = self.execute(profile, Path(state['worktree']), prompt)
                        state['attempts'].append({'profile': profile, 'result': result, 'usage': usage})
                        state['status'] = 'needs_review' if result == 'produced' else 'waiting_for_quota' if result == 'quota' else 'blocked'
                        write_json(directory / 'state.json', state)
                        if result != 'quota':
                            return state
                except BusyError:
                    state['attempts'].append({'profile': profile, 'result': 'busy'})
                except WorkerStillRunningError:
                    state['status'] = 'reconcile_required'
                    write_json(directory / 'state.json', state)
                    raise
                except (RuntimeError, OSError, subprocess.SubprocessError) as error:
                    attempt = {'profile': profile, 'result': 'worker-error'}
                    if isinstance(error, MetadataError):
                        attempt['diagnostic'] = str(error)
                    state['attempts'].append(attempt)
                    state['status'] = 'blocked'
                    write_json(directory / 'state.json', state)
                    return state
                except BaseException:
                    state['status'] = 'blocked'
                    write_json(directory / 'state.json', state)
                    raise
            state['status'] = 'waiting_for_quota' if any(item['result'] == 'quota' for item in state['attempts'][attempt_start:]) else 'waiting_for_account'
            write_json(directory / 'state.json', state)
            return state


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    commands = parser.add_subparsers(dest='action', required=True)
    commands.add_parser('init')
    for action in ('login', 'status'):
        command = commands.add_parser(action)
        command.add_argument('profile', choices=['esra-pro', 'kadir-plus'])
    run = commands.add_parser('run')
    run.add_argument('task_file', type=Path)
    run.add_argument('--profile', choices=['esra-pro', 'kadir-plus'], default='esra-pro')
    resume = commands.add_parser('resume')
    resume.add_argument('job_id')
    resume.add_argument('--profile', choices=['esra-pro', 'kadir-plus'], default='esra-pro')
    parallel = commands.add_parser('parallel')
    parallel.add_argument('pro_task', type=Path)
    parallel.add_argument('plus_task', type=Path)
    arguments = parser.parse_args()
    workers = Workers()
    workers.initialize()
    if arguments.action == 'init':
        print(json.dumps({'profiles': list(workers.profiles), 'authenticated': 'not-checked'}))
    elif arguments.action == 'login':
        raise SystemExit(workers.login(arguments.profile))
    elif arguments.action == 'status':
        print(json.dumps(workers.account(arguments.profile)))
    elif arguments.action == 'run':
        print(json.dumps(workers.run(arguments.task_file, arguments.profile)))
    elif arguments.action == 'resume':
        print(json.dumps(workers.resume(arguments.job_id, arguments.profile)))
    else:
        with ThreadPoolExecutor(max_workers=2) as executor:
            jobs = [executor.submit(workers.run, arguments.pro_task, 'esra-pro'),
                    executor.submit(workers.run, arguments.plus_task, 'kadir-plus')]
            print(json.dumps([job.result() for job in jobs]))


if __name__ == '__main__':
    main()
