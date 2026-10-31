"""OPS-DASH-RUNTIME-01: local read-only operations API and opt-in persistent worker."""
import argparse
from datetime import datetime, timezone
from http.server import BaseHTTPRequestHandler, ThreadingHTTPServer
import json
import math
from pathlib import Path
import re
import subprocess
import sys
import threading
import time

from codex_accounts import ROOT, BusyError, WorkerStillRunningError, exclusive, write_json
from codex_watch import Watch, PROFILES
from nightly_coordinator import Coordinator
from model_policy import ModelPolicy, ModelPolicyError

STATUSES = {'unknown', 'known', 'busy', 'unavailable', 'reconcile_required', 'monitoring', 'idle',
    'running', 'finished', 'queued', 'developing', 'verifying', 'awaiting_qa', 'awaiting_ci',
    'merged', 'blocked', 'waiting_for_account', 'waiting_for_quota', 'reconciliation_required', 'produced', 'failed', 'timeout', 'disabled'}
PHASES = {'development', 'independent_qa', 'developer', 'qa', 'backend', 'ui', 'unknown', 'routine', 'test-writing', 'security_qa'}
REASONS = {'qa_used_tools', 'qa_unknown_startup_error', 'qa_unknown_or_error_event', 'qa_invalid_event_stream',
    'independent_qa_rejected_or_invalid', 'qa_waiting_for_account', 'window_call_budget_wait',
    'window_job_budget_wait', 'reconciliation_required_before_renewal', 'deadline_reached', 'model_call_cap',
    'reconciliation_needed_worker_termination', 'reconciliation_needed_operation_failed',
    'reconciliation_needed_interrupted_phase', 'reconciliation_needed_unknown_stage',
    'quota_changed_before_dispatch', 'development_failed_preserve_worktree',
    'reviewed_head_or_worktree_changed', 'ci_failed', 'ci_failed_or_skipped', 'operation_blocked'}
REASONS |= {'invalid_or_missing_model_policy', 'model_not_advertised_for_profile'}


def reason(value):
    return choice(value, REASONS, 'operation_blocked')


def choice(value, options, default='unknown'):
    return value if isinstance(value, str) and value in options else default


def mapping(value):
    return value if isinstance(value, dict) else {}


def sequence(value):
    return value if isinstance(value, list) else []


def iso(at):
    return datetime.fromtimestamp(at, timezone.utc).isoformat()


def number(value):
    return type(value) in (int, float) and math.isfinite(value) and value >= 0


def label(value):
    return value if isinstance(value, str) and re.fullmatch(r'[A-Za-z0-9_-]{1,100}', value) else None


def linked(path):
    return any(p.is_symlink() or (hasattr(p, 'is_junction') and p.is_junction()) for p in (path, *path.parents))


def load(path, default=None):
    try:
        if linked(path) or path.stat().st_size > 2_000_000:
            return default or {}
        return mapping(json.loads(path.read_text(encoding='utf-8')))
    except (OSError, ValueError):
        return default or {}


def windows(values):
    result = []
    for value in values[:20] if isinstance(values, list) else []:
        if isinstance(value, dict):
            item = {k: value[k] for k in ('usedPercent', 'windowDurationMins', 'resetsAt') if number(value.get(k))}
            if item not in result:
                result.append(item)
    return result


def model_fields(value):
    result = {}
    if value.get('model') in {'gpt-6-luna', 'gpt-6.1-sol'}:
        result['model'] = value['model']
    if value.get('effort') in {'low', 'medium'}:
        result['effort'] = value['effort']
    return result


def date(value):
    try:
        return iso(datetime.fromisoformat(value).timestamp())
    except (TypeError, ValueError, OverflowError):
        return None


def numeric(values):
    return {k: v for k, v in values.items() if k in {
        'input_tokens', 'output_tokens', 'cached_input_tokens', 'reasoning_output_tokens',
        'cache_write_input_tokens', 'total_tokens'} and number(v)} if isinstance(values, dict) else {}


class Store:
    def __init__(self, root=ROOT, wall=time.time):
        self.root, self.wall = Path(root), wall
        self.runtime = self.root / '.runtime/ops'
        self.mutex = threading.RLock()

    def status(self):
        now = self.wall()
        raw = load(self.root / '.runtime/account-workers/quota.json')
        stamp = date(raw.get('timestamp'))
        fresh = stamp is not None and 0 <= now - datetime.fromisoformat(stamp).timestamp() <= 4500
        profiles, alarms = {}, []
        for profile in PROFILES:
            item = mapping(mapping(raw.get('profiles')).get(profile))
            state = choice(item.get('state'), {'known', 'busy', 'unavailable', 'reconcile_required', 'disabled'}, 'unavailable')
            if not fresh:
                state = 'unavailable'
            ws = windows(item.get('windows'))
            ready = state == 'known' and item.get('ready') is True and bool(ws) and all(
                number(w.get('usedPercent')) and w['usedPercent'] < 85 for w in ws)
            safe = {'state': state, 'ready': ready}
            if choice(item.get('plan'), {'pro', 'prolite', 'promax', 'plus'}) != 'unknown':
                safe['plan'] = item['plan']
            observed = date(item.get('observedAt')) or stamp
            if state == 'known':
                safe.update(windows=ws, observedAt=observed)
            else:
                last = item if ws else mapping(item.get('lastKnown'))
                if windows(last.get('windows')):
                    safe['lastKnown'] = {'windows': windows(last['windows']),
                        'observedAt': date(last.get('observedAt'))}
            profiles[profile] = safe
            for index, w in enumerate(ws if state == 'known' else safe.get('lastKnown', {}).get('windows', [])):
                blocking = w.get('usedPercent', 100) >= 85
                reset = w.get('resetsAt')
                known_reset = number(reset) and reset + 60 > now
                alarm = {'id': profile + '-' + str(index), 'profile': profile,
                         'state': 'unknown' if state != 'known' else 'armed' if blocking and known_reset else 'waiting' if blocking or not ready else 'eligible',
                         'reason': 'metadata_unknown' if state != 'known' else 'quota_window_blocking' if blocking else 'account_not_eligible' if not ready else 'below_reserve'}
                alarm.update({k: w[k] for k in ('usedPercent', 'windowDurationMins') if k in w})
                if number(reset):
                    alarm.update(resetAt=reset, resumeAt=reset + 60)
                alarms.append(alarm)
            if not ws and not safe.get('lastKnown'):
                alarms.append({'id': profile, 'profile': profile, 'state': 'unknown', 'reason': 'metadata_unknown'})
        state = load(self.root / '.runtime/nightly/state.json')
        jobs = []
        retained_jobs = list(mapping(state.get('jobs')).items())
        # API view only: never delete canonical dependency/reconciliation state.
        retained_jobs.sort(key=lambda item: isinstance(item[1], dict) and item[1].get('status') != 'merged', reverse=True)
        for identifier, j in retained_jobs[:100]:
            if not label(identifier) or not isinstance(j, dict):
                continue
            job = {'id': identifier, 'status': choice(j.get('status'), STATUSES)}
            if choice(j.get('phase'), PHASES) != 'unknown':
                job['phase'] = j['phase']
            if j.get('reason'):
                job['reason'] = reason(j['reason'])
            if j.get('profile') in PROFILES:
                job['profile'] = j['profile']
            if isinstance(j.get('commit'), str) and re.fullmatch('[a-f0-9]{40}', j['commit']):
                job['commit'] = j['commit']
            if isinstance(j.get('pr_url'), str) and re.fullmatch(r'https://github.com/kadirvurgun/Poseidon/pull/[0-9]+', j['pr_url']):
                job['prUrl'] = j['pr_url']
            job['calls'] = [{'profile': c['profile'], 'phase': choice(c.get('phase'), PHASES), 'usage': numeric(c.get('usage')), **model_fields(c)}
                            for c in sequence(j.get('calls'))[-20:] if isinstance(c, dict) and c.get('profile') in PROFILES]
            if job['calls']:
                job.setdefault('profile', job['calls'][-1]['profile'])
                job.setdefault('phase', job['calls'][-1]['phase'])
            jobs.append(job)
        activities = []
        directory = self.runtime / 'activities'
        if not linked(directory) and directory.is_dir():
            for path in sorted(directory.glob('*.json'))[-200:]:
                a = load(path)
                if not label(a.get('id')) or a.get('profile') not in PROFILES:
                    continue
                safe = {k: a[k] for k in ('id', 'profile', 'taskId') if label(a.get(k))}
                safe['role'] = choice(a.get('role'), PHASES)
                safe['status'] = choice(a.get('status'), STATUSES)
                safe['startedAt'] = date(a.get('startedAt'))
                if date(a.get('finishedAt')):
                    safe['finishedAt'] = date(a['finishedAt'])
                safe['usage'] = numeric(a.get('usage'))
                safe.update(model_fields(a))
                activities.append(safe)
        # A model receipt is saved before dispatch. Show that live lease immediately,
        # while the coordinator thread is still inside the CLI invocation.
        for identifier, job in retained_jobs[:100]:
            if not label(identifier) or not isinstance(job, dict):
                continue
            calls = sequence(job.get('calls'))
            lease = mapping(job.get('lease'))
            if (job.get('inflight') not in {'model', 'qa_model'} or not calls
                    or not number(lease.get('until')) or lease['until'] <= now):
                continue
            call = mapping(calls[-1])
            if call.get('profile') not in PROFILES or not number(call.get('started_at')) or number(call.get('finished_at')):
                continue
            active_id = identifier + '-' + str(len(calls) - 1)
            activities = [a for a in activities if a['id'] != active_id]
            activities.append({'id': active_id, 'taskId': identifier, 'profile': call['profile'],
                'role': choice(call.get('phase'), PHASES), 'status': 'running',
                'startedAt': iso(call['started_at']), 'usage': numeric(call.get('usage')), **model_fields(call)})
        project = load(self.root / 'docs/project-status.json')
        progress = {target: project.get(source) if number(project.get(source)) else None for target, source in {
            'completionPercent': 'completion_percent', 'acceptedStoryPoints': 'accepted_story_points',
            'totalStoryPoints': 'total_story_points', 'acceptedTasks': 'accepted_tasks', 'totalTasks': 'total_tasks'}.items()}
        progress['stage'] = 'S0' if str(project.get('current_stage', '')).startswith('S0') else 'unknown'
        service = load(self.runtime / 'service.json')
        next_check = date(raw.get('next_check'))
        if number(service.get('nextWake')):
            wake = iso(service['nextWake'])
            if next_check is None or datetime.fromisoformat(wake) < datetime.fromisoformat(next_check):
                next_check = wake
        for alarm in alarms:
            alarm['wakeState'] = choice(service.get('wakeState'), {'requested', 'unavailable', 'not_requested'}, 'not_requested')
        if label(state.get('reason')):
            wait = {'id': 'coordinator', 'state': 'waiting', 'reason': reason(state['reason'])}
            if state['reason'] in {'window_call_budget_wait', 'window_job_budget_wait'} and number(state.get('deadline')):
                wait['resumeAt'] = state['deadline']
            alarms.append(wait)
        service_status = service.get('status')
        routing = load(self.root / 'automation/codex-accounts.json').get('routing_policy')
        selected = next((p for p in PROFILES if profiles[p]['ready']), None)
        if routing == 'esra_until_quota':
            primary = profiles['esra-pro']
            quota_blocked = (primary['state'] == 'known'
                and any(w.get('usedPercent', 0) >= 85 for w in primary.get('windows', [])))
            selected = ('esra-pro' if primary['ready'] else 'kadir-plus'
                        if quota_blocked and profiles['kadir-plus']['ready'] else None)
        coordinator = {'enabled': service.get('enabled') is True,
            'status': choice(service_status, STATUSES | REASONS),
            'modelCalls': state.get('model_calls', 0) if number(state.get('model_calls', 0)) else 0,
            'maxCalls': state.get('max_calls', 8) if number(state.get('max_calls', 8)) else 8,
            'maxJobs': state.get('max_jobs', 4) if number(state.get('max_jobs', 4)) else 4,
            'deadline': state.get('deadline') if number(state.get('deadline')) else None, 'jobs': jobs}
        policy = {}
        try:
            configured = ModelPolicy(self.root).value
            policy['modelPolicy'] = {'roles': configured['roles'], 'automaticEscalation': False}
        except ModelPolicyError:
            pass
        return {'schemaVersion': 1, 'generatedAt': iso(now), **policy,
            'quota': {'timestamp': stamp, 'next_check': next_check, 'selected': selected, 'profiles': profiles},
            'coordinator': coordinator, 'activities': activities, 'events': self.events(), 'alarms': alarms, 'project': progress}

    def events(self):
        path = self.runtime / 'events.jsonl'
        try:
            if linked(path) or path.stat().st_size > 2_000_000:
                return []
            values = [json.loads(line) for line in path.read_text(encoding='utf-8').splitlines()[-500:]]
            return [self.clean_event(v) for v in values if isinstance(v, dict) and self.clean_event(v)]
        except (OSError, ValueError):
            return []

    @staticmethod
    def clean_event(value):
        if choice(value.get('type'), {'snapshot', 'threshold_cross', 'assignee', 'job_phase', 'renewal', 'reason'}) == 'unknown' or not date(value.get('at')):
            return {}
        result = {'type': value['type'], 'at': date(value['at'])}
        for k in ('id', 'taskId'):
            if label(value.get(k)):
                result[k] = value[k]
        for k in ('from', 'to'):
            if choice(value.get(k), STATUSES | PHASES) != 'unknown':
                result[k] = value[k]
        if value.get('message'):
            result['message'] = reason(value['message'])
        if value.get('profile') in PROFILES:
            result['profile'] = value['profile']
        for k in ('usedPercent', 'windowDurationMins', 'resetAt'):
            if number(value.get(k)):
                result[k] = value[k]
        return result

    def record(self, kind, **fields):
        with self.mutex:
            events = self.events()
            event = self.clean_event({'id': str(time.time_ns()), 'at': iso(self.wall()), 'type': kind, **fields})
            if event:
                events.append(event)
            self.runtime.mkdir(parents=True, exist_ok=True)
            path = self.runtime / 'events.jsonl'
            temporary = path.with_suffix('.tmp')
            temporary.write_text(''.join(json.dumps(v) + '\n' for v in events[-500:]), encoding='utf-8')
            temporary.replace(path)


def server(store, port=8765):
    class Handler(BaseHTTPRequestHandler):
        def log_message(self, *args):
            pass

        def do_GET(self):
            if self.headers.get_all('Host') != ['127.0.0.1:' + str(self.server.server_port)]:
                self.send_error(403)
                return
            if self.path != '/ops/status':
                self.send_error(404)
                return
            body = json.dumps(store.status(), allow_nan=False).encode()
            self.send_response(200)
            self.send_header('Content-Type', 'application/json; charset=utf-8')
            self.send_header('Cache-Control', 'no-store')
            self.send_header('X-Content-Type-Options', 'nosniff')
            self.send_header('Content-Length', str(len(body)))
            self.end_headers()
            self.wfile.write(body)
    return ThreadingHTTPServer(('127.0.0.1', port), Handler)


class Driver:
    def __init__(self, store, watch=None, coordinator=None, manage_wake=False):
        self.store = store
        self.watch = watch or Watch(wall=store.wall)
        self.coordinator = coordinator
        self.poll_at = 0
        self.previous = {(e.get('profile'), e.get('windowDurationMins')): e['usedPercent']
                         for e in store.events() if e.get('type') == 'snapshot' and number(e.get('usedPercent'))}
        self.manage_wake = manage_wake
        self.wake_state = 'not_requested'
        self.wake_at = -1 if manage_wake else None
        self.activity_signature = None

    def tick(self):
        now = self.store.wall()
        activities = self.store.status()['activities']
        signature = tuple((a['id'], a['status'], a.get('finishedAt')) for a in activities)
        if self.activity_signature is not None and signature != self.activity_signature:
            self.poll_at = 0  # Completion/assignment changes warrant fresh metadata, not a model probe.
        self.activity_signature = signature
        if now >= self.poll_at:
            self.watch.once()  # Metadata only; locks each profile separately.
            current = self.store.status()
            for profile, p in current['quota']['profiles'].items():
                for w in p.get('windows', []):
                    self.store.record('snapshot', profile=profile, usedPercent=w.get('usedPercent'),
                        windowDurationMins=w.get('windowDurationMins'), resetAt=w.get('resetsAt'))
                    key = (profile, w.get('windowDurationMins'))
                    old = self.previous.get(key)
                    if old is not None and (old >= 85) != (w.get('usedPercent', 100) >= 85):
                        self.store.record('threshold_cross', profile=profile, usedPercent=w.get('usedPercent'), windowDurationMins=w.get('windowDurationMins'))
                    self.previous[key] = w.get('usedPercent')
            # Observe real resets even when another (weekly) window still blocks.
            # Waking is a metadata recheck, never permission to start a model.
            resets = [a['resumeAt'] for a in current['alarms'] if number(a.get('resumeAt')) and a['resumeAt'] > now]
            self.poll_at = min([now + 3600, *resets])
            wake_at = min(resets) if resets else None
            if self.manage_wake and wake_at != self.wake_at:
                self.wake_at = wake_at
                try:
                    result = subprocess.run(['powershell.exe', '-NoProfile', '-NonInteractive', '-File',
                        str(self.store.root / 'scripts/install-ops-runtime.ps1'), '-WakeOnly',
                        '-PythonPath', sys.executable, '-WakeAt', str(int(wake_at or 0))],
                        capture_output=True, timeout=30, check=False)
                    self.wake_state = ('requested' if wake_at else 'not_requested') if result.returncode == 0 else 'unavailable'
                except (OSError, subprocess.TimeoutExpired):
                    self.wake_state = 'unavailable'
        current = self.store.status()
        enabled = self.coordinator is not None
        status = 'monitoring' if not enabled else 'waiting_for_quota'
        if enabled and current['quota']['selected']:
            before = load(self.store.root / '.runtime/nightly/state.json')
            write_json(self.store.runtime / 'service.json', {'enabled': True, 'status': 'running',
                'nextWake': self.poll_at, 'wakeState': self.wake_state, 'wakeAt': self.wake_at})
            after = self.coordinator.once()
            if after.get('model_calls') != before.get('model_calls'):
                self.poll_at = 0
            status = after.get('reason', 'idle' if not after.get('jobs') else 'running')
            if any(j.get('status') == 'blocked' or j.get('inflight') for j in after.get('jobs', {}).values()):
                status = 'reconcile_required'
            for key, job in after.get('jobs', {}).items():
                old = before.get('jobs', {}).get(key, {})
                if old.get('status') != job.get('status'):
                    self.store.record('job_phase', taskId=key, to=job.get('status'))
                if old.get('reason') != job.get('reason') and job.get('reason'):
                    self.store.record('reason', taskId=key, message=job['reason'])
                calls = job.get('calls', [])
                if len(calls) > len(old.get('calls', [])):
                    self.store.record('assignee', taskId=key, profile=calls[-1]['profile'], to=calls[-1]['phase'])
                for index, call in enumerate(calls):
                    if not label(key) or call.get('profile') not in PROFILES:
                        continue
                    identifier = key + '-' + str(index)
                    activity = {'id': identifier, 'taskId': key, 'profile': call['profile'],
                        'role': label(call.get('phase')) or 'unknown',
                        'status': 'finished' if number(call.get('finished_at')) else 'unknown',
                        'startedAt': iso(call['started_at']) if number(call.get('started_at')) else None,
                        'usage': numeric(call.get('usage')), **model_fields(call)}
                    if number(call.get('finished_at')):
                        activity['finishedAt'] = iso(call['finished_at'])
                    write_json(self.store.runtime / 'activities' / (identifier + '.json'), activity)
            if after.get('started') != before.get('started') and before:
                self.store.record('renewal')
            self.prune_activities()
        write_json(self.store.runtime / 'service.json', {'enabled': enabled, 'status': status,
            'nextWake': self.poll_at, 'wakeState': self.wake_state, 'wakeAt': self.wake_at})

    def prune_activities(self):
        directory = self.store.runtime / 'activities'
        if linked(directory) or not directory.is_dir():
            return
        finished = []
        for path in directory.glob('*.json'):
            if linked(path):
                continue
            value = load(path)
            if date(value.get('finishedAt')):
                finished.append((value['finishedAt'], path))
        for _, path in sorted(finished, key=lambda pair: pair[0], reverse=True)[200:]:
            path.unlink()  # Only bounded derived completed view; canonical state is retained.

    def run(self, stop):
        with exclusive(self.store.runtime / 'runtime.lock'):
            while not stop.is_set():
                try:
                    self.tick()
                except BusyError:
                    pass
                except WorkerStillRunningError:
                    write_json(self.store.runtime / 'service.json', {'enabled': False, 'status': 'reconcile_required'})
                    stop.wait()  # HTTP remains live; never retry uncertain work.
                except Exception:
                    write_json(self.store.runtime / 'service.json', {'enabled': False, 'status': 'reconcile_required'})
                    stop.wait()
                stop.wait(15)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--serve', action='store_true')
    parser.add_argument('--develop', action='store_true')
    parser.add_argument('--publish', action='store_true')
    parser.add_argument('--wake-check', action='store_true')
    parser.add_argument('--manage-wake', action='store_true', help='Operator opt-in Task Scheduler wake fallback')
    args = parser.parse_args()
    if args.publish and not args.develop:
        parser.error('--publish requires --develop')
    store = Store()
    if args.wake_check:
        try:
            with exclusive(store.runtime / 'runtime.lock'):
                Driver(store).tick()
        except BusyError:
            pass
        return
    if not args.serve:
        parser.error('--serve required')
    coordinator = Coordinator(enabled=True, publish=args.publish, max_jobs=4, max_calls=8, continuous=True) if args.develop else None
    stop = threading.Event()
    worker = threading.Thread(target=Driver(store, coordinator=coordinator, manage_wake=args.manage_wake).run, args=(stop,), daemon=True)
    http = server(store)
    worker.start()
    try:
        http.serve_forever()
    finally:
        stop.set()
        http.server_close()


if __name__ == '__main__':
    main()
