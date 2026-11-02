"""Run the real API test project; retain only closed, value-free test evidence.

Owned provider environment comes from s3_gate/clamd_gate. Never substitute a
provider or suppress a test when it is missing. Raw runner/SDK output stays in
memory because failed HTTP calls can include authentication and personal data.
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
import re
import subprocess
import time

from oidc_lab import ROOT, root_tool
from codex_accounts import Workers


def capture(args, root, timeout):
    # Keep the live parent until its owned process tree is stopped. run(timeout)
    # can kill only dotnet and leave its test executable alive on Windows.
    groups = {'creationflags': subprocess.CREATE_NEW_PROCESS_GROUP} if os.name == 'nt' else {'start_new_session': True}
    process = subprocess.Popen(args, cwd=root, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, **groups)
    try:
        output, _ = process.communicate(timeout=timeout)
        return subprocess.CompletedProcess(args, process.returncode, output)
    except BaseException:
        if process.poll() is None:
            Workers.stop(process)
        raise
    finally:
        if process.stdout:
            process.stdout.close()


def normalized(output):
    return re.sub(r'\x1b\[[0-?]*[ -/]*[@-~]', '', output.decode('utf-8', errors='replace'))


def observations(output):
    text = normalized(output)
    return {key: [int(value) for value in re.findall(r'(?im)^\s*' + key + r'\s*:\s*(\d{1,9})\s*$', text)]
            for key in ('total', 'failed', 'succeeded', 'skipped')}


def deadline_diagnostics(output, discovered):
    """Identifiers are diagnostic occurrences, never completed/running test claims."""
    raw = output if isinstance(output, bytes) else b''
    known = set(discovered)
    seen = []
    for name in re.findall(r'\bPoseidon\.Api\.Tests\.[A-Za-z0-9_.]+', normalized(raw)):
        if name in known:
            if name in seen:
                seen.remove(name)
            seen.append(name)
    return {'output_sha256': hashlib.sha256(raw).hexdigest(),
            'static_identifier_count': len(seen), 'last_static_identifiers': seen[-8:],
            'identifier_semantics': 'diagnostic_occurrence_only',
            'partial_count_observations': observations(raw)}


def failure_locations(output, root):
    """Only existing source filenames/line numbers; no exception or body text."""
    known = {p.name for p in (root / 'tests/Poseidon.Api.Tests').glob('*.cs')}
    locations = set()
    for name, line in re.findall(r'([A-Za-z][A-Za-z0-9_]*\.cs):(?:line\s+)?([0-9]{1,7})', normalized(output)):
        if name in known:
            locations.add((name, int(line)))
    return [{'file': name, 'line': line} for name, line in sorted(locations)][:100]


def command(scope):
    args = [root_tool('dotnet'), 'test', '--project', 'tests/Poseidon.Api.Tests/Poseidon.Api.Tests.csproj',
            '--configuration', 'Release', '--no-build']
    if scope != 'all':
        args.extend(['--filter-trait' if scope == 'integration' else '--filter-not-trait', 'Category=Integration'])
    return args


def run(scope, root=ROOT, timeout=1800):
    if scope not in ('all', 'unit', 'integration') or type(timeout) is not int or not 1 <= timeout <= 1800:
        raise ValueError('invalid-gate-input')
    args = command(scope)
    evidence = {'scope': scope, 'status': 'FAILED_OR_UNPROVEN', 'raw_output_persisted': False,
                'started_at': datetime.now(timezone.utc).isoformat()}
    started = time.monotonic()
    stage, discovered = 'discovery', []
    try:
        discovery = capture([*args, '--list-tests'], root, min(timeout, 120))
        discovered = re.findall(r'(?m)^\s*(Poseidon\.Api\.Tests\.[A-Za-z0-9_.]+)', normalized(discovery.stdout))
        evidence['discovery'] = {'exit_code': discovery.returncode, 'cases': len(discovered),
                                 'output_sha256': hashlib.sha256(discovery.stdout).hexdigest()}
        if discovery.returncode or not discovered:
            evidence['status'] = 'DISCOVERY_FAILED_OR_EMPTY'
            return 2, evidence
        stage = 'execution'
        execution = capture([*args, '--output', 'Detailed'], root, timeout)
        counts = observations(execution.stdout)
        evidence.update(exit_code=execution.returncode, count_observations=counts,
                        output_sha256=hashlib.sha256(execution.stdout).hexdigest())
        if execution.returncode:
            evidence['failure_source_locations'] = failure_locations(execution.stdout, root)
        # Only static identifiers already found in discovery may be emitted.
        # Parameters, request bodies, exception text and raw source paths never are.
        static_names = {name.split('.')[-1] for name in discovered}
        failed_lines = [line for line in normalized(execution.stdout).splitlines()
                        if re.match(r'\s*(?:failed\b|\[FAIL\])', line, re.I)]
        evidence['failed_static_test_names'] = sorted(name for name in static_names
            if any(re.search(r'\b' + re.escape(name) + r'\b', line) for line in failed_lines))
        complete = (all(len(values) == 1 for values in counts.values()) and counts['failed'] == [0]
                    and counts['skipped'] == [0] and counts['total'] == counts['succeeded'] == [len(discovered)])
        if execution.returncode == 0 and complete:
            evidence['status'] = 'PASS_EXACT_DISCOVERED_SCOPE'
            return 0, evidence
        return execution.returncode or 2, evidence
    except subprocess.TimeoutExpired as expired:
        evidence['status'] = 'INTERRUPTED_DEADLINE'
        evidence['interrupted_stage'] = stage
        evidence['deadline_diagnostics'] = deadline_diagnostics(expired.output, discovered)
        return 2, evidence
    except Exception:
        evidence['status'] = 'GATE_EXECUTION_FAILED'
        return 2, evidence
    finally:
        evidence['elapsed_seconds'] = round(time.monotonic() - started, 3)
        evidence['finished_at'] = datetime.now(timezone.utc).isoformat()


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--scope', choices=('all', 'unit', 'integration'), required=True)
    args = parser.parse_args()
    code, evidence = run(args.scope)
    directory = ROOT / '.runtime/api-gate'
    directory.mkdir(parents=True, exist_ok=True)
    (directory / (args.scope + '.json')).write_text(json.dumps(evidence, indent=2) + '\n', encoding='utf-8')
    print(json.dumps(evidence))
    return code


if __name__ == '__main__':
    try:
        raise SystemExit(main())
    except Exception:
        print(json.dumps({'status': 'GATE_SETUP_OR_EVIDENCE_FAILURE', 'raw_output_persisted': False}))
        raise SystemExit(2) from None
