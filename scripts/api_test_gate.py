"""Run the real API test project; retain only closed, value-free test evidence.

Owned provider environment comes from s3_gate/clamd_gate. Never substitute a
provider or suppress a test when it is missing. Raw runner/SDK output stays in
memory because failed HTTP calls can include authentication and personal data.
"""
import argparse
from collections import Counter
from concurrent.futures import ThreadPoolExecutor
from datetime import datetime, timezone
import hashlib
import json
import os
import re
import subprocess
import time

import critical_write_coverage

from oidc_lab import ROOT, root_tool
from codex_accounts import Workers


def capture(args, root, timeout, env=None):
    # Keep the live parent until its owned process tree is stopped. run(timeout)
    # can kill only dotnet and leave its test executable alive on Windows.
    groups = {'creationflags': subprocess.CREATE_NEW_PROCESS_GROUP} if os.name == 'nt' else {'start_new_session': True}
    process = subprocess.Popen(args, cwd=root, stdout=subprocess.PIPE, stderr=subprocess.STDOUT, env=env, **groups)
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


def discovered_methods(output):
    # Parameter values are intentionally discarded. A theory method retains its
    # complete discovery multiplicity and is never split between native runners.
    return re.findall(r'(?m)^\s*(Poseidon\.Api\.Tests\.[A-Za-z0-9_.]+)', normalized(output))


def partition_methods(discovered, shards):
    counts = Counter(discovered)
    if not counts or any(not re.fullmatch(r'Poseidon\.Api\.Tests\.[A-Za-z0-9_.]+', name) for name in counts):
        raise ValueError('invalid-discovered-method')
    groups = [Counter() for _ in range(min(shards, len(counts)))]
    for name, count in sorted(counts.items(), key=lambda item: (-item[1], item[0])):
        target = min(range(len(groups)), key=lambda i: (groups[i].total(), i))
        groups[target][name] = count
    return groups


def method_command(args, methods):
    result = [*args, '--filter-method', *sorted(methods)]
    # Windows CreateProcess has a 32767 UTF-16 code-unit limit including NUL.
    # Apply the same conservative bound on every platform, before any launch.
    if len(subprocess.list2cmdline([*result, '--output', 'Detailed']).encode('utf-16-le')) // 2 >= 30000:
        raise ValueError('native-command-too-long')
    return result


def valid_partition(full, expected, actual):
    if not expected or len(expected) != len(actual) or any(not group for group in expected):
        return False
    seen, combined = set(), Counter()
    for want, got in zip(expected, actual):
        if want != got or seen.intersection(got):
            return False
        seen.update(got); combined.update(got)
    return combined == Counter(full)


def execution_evidence(execution, discovered, root):
    counts = observations(execution.stdout)
    report = {'exit_code': execution.returncode, 'count_observations': counts,
              'output_sha256': hashlib.sha256(execution.stdout).hexdigest()}
    if execution.returncode:
        report['failure_source_locations'] = failure_locations(execution.stdout, root)
    static_names = {name.split('.')[-1] for name in discovered}
    failed_lines = [line for line in normalized(execution.stdout).splitlines()
                    if re.match(r'\s*(?:failed\b|\[FAIL\])', line, re.I)]
    report['failed_static_test_names'] = sorted(name for name in static_names
        if any(re.search(r'\b' + re.escape(name) + r'\b', line) for line in failed_lines))
    complete = (all(len(values) == 1 for values in counts.values()) and counts['failed'] == [0]
                and counts['skipped'] == [0] and counts['total'] == counts['succeeded'] == [len(discovered)])
    return report, execution.returncode == 0 and complete


def run(scope, root=ROOT, timeout=1800, shards=1):
    if (scope not in ('all', 'unit', 'integration') or type(timeout) is not int or not 1 <= timeout <= 1800
            or type(shards) is not int or not 1 <= shards <= 4 or (shards != 1 and scope != 'integration')):
        raise ValueError('invalid-gate-input')
    args = command(scope)
    evidence = {'scope': scope, 'status': 'FAILED_OR_UNPROVEN', 'raw_output_persisted': False,
                'started_at': datetime.now(timezone.utc).isoformat()}
    started = time.monotonic()
    deadline = started + timeout
    stage, discovered = 'discovery', []
    def remaining(limit=None):
        left = deadline - time.monotonic()
        if left <= 0:
            raise subprocess.TimeoutExpired('owned-api-native-gate', timeout)
        return min(left, limit) if limit is not None else left
    def parallel(commands, environment, discovery=False):
        def invoke(cmd):
            try:
                return capture(cmd, root, remaining(120 if discovery else None), env=environment)
            except Exception as failure:
                # Collect every launched shard even after another shard fails.
                return failure
        with ThreadPoolExecutor(max_workers=len(commands)) as pool:
            return list(pool.map(invoke, commands))
    try:
        child_environment = critical_write_coverage.clean_environment(os.environ)
        discovery = capture([*args, '--list-tests'], root, remaining(120), env=child_environment)
        discovered = discovered_methods(discovery.stdout)
        evidence['discovery'] = {'exit_code': discovery.returncode, 'cases': len(discovered),
                                 'output_sha256': hashlib.sha256(discovery.stdout).hexdigest()}
        if discovery.returncode or not discovered:
            evidence['status'] = 'DISCOVERY_FAILED_OR_EMPTY'
            return 2, evidence
        shard_commands = None
        groups = None
        if shards > 1:
            stage = 'shard-discovery'
            groups = partition_methods(discovered, shards)
            shard_commands = [method_command(args, group) for group in groups]
            results = parallel([[*cmd, '--list-tests'] for cmd in shard_commands], child_environment, True)
            evidence['shard_discovery'] = []
            actual = []
            for result in results:
                if isinstance(result, subprocess.TimeoutExpired):
                    raise result
                if isinstance(result, Exception):
                    raise RuntimeError('shard-discovery-execution-failed')
                names = discovered_methods(result.stdout)
                actual.append(Counter(names))
                evidence['shard_discovery'].append({'exit_code': result.returncode, 'cases': len(names),
                    'output_sha256': hashlib.sha256(result.stdout).hexdigest()})
            if any(r.returncode for r in results) or not valid_partition(discovered, groups, actual):
                evidence['status'] = 'SHARD_DISCOVERY_PARTITION_FAILED'
                return 2, evidence
            evidence['partition'] = {'status': 'EXACT_DISJOINT_COMPLETE_MULTISET', 'shards': len(groups),
                'cases': [group.total() for group in groups], 'methods': [len(group) for group in groups]}
        coverage_run = None
        if scope in ('integration', 'all'):
            stage = 'critical-write-coverage-setup'
            coverage_run = critical_write_coverage.prepare(root, scope)
            child_environment = coverage_run.environment(child_environment)
        stage = 'execution'
        if shard_commands is None:
            execution = capture([*args, '--output', 'Detailed'], root, remaining(), env=child_environment)
            report, complete = execution_evidence(execution, discovered, root)
            evidence.update(report)
            native_code = execution.returncode or 2
        else:
            results = parallel([[*cmd, '--output', 'Detailed'] for cmd in shard_commands], child_environment)
            evidence['shards'] = []
            complete, native_code = True, 2
            totals = {key: 0 for key in ('total', 'failed', 'succeeded', 'skipped')}
            interrupted = execution_error = False
            for group, result in zip(groups, results):
                names = list(group.elements())
                if isinstance(result, subprocess.TimeoutExpired):
                    report = {'status': 'INTERRUPTED_DEADLINE',
                              'deadline_diagnostics': deadline_diagnostics(result.output, names)}
                    interrupted = True; passed = False
                elif isinstance(result, Exception):
                    report = {'status': 'GATE_EXECUTION_FAILED'}
                    execution_error = True; passed = False
                else:
                    report, passed = execution_evidence(result, names, root)
                    report['status'] = 'PASS_EXACT_SHARD' if passed else 'FAILED_OR_UNPROVEN'
                    for key, values in report['count_observations'].items():
                        if len(values) == 1: totals[key] += values[0]
                complete = complete and passed
                evidence['shards'].append(report)
            # Native evidence replay requires a closed aggregate exit status as
            # well as counts. Never infer native success from counts alone.
            evidence['exit_code'] = 0 if complete else 2
            native_code = evidence['exit_code'] or 2
            evidence['failed_static_test_names'] = sorted({name for report in evidence['shards']
                for name in report.get('failed_static_test_names', [])})
            evidence['failure_source_locations'] = [location for report in evidence['shards']
                for location in report.get('failure_source_locations', [])]
            # Aggregates are only final counts when EVERY shard has strict final summaries.
            if all('count_observations' in report and all(len(values) == 1 for values in report['count_observations'].values())
                   for report in evidence['shards']):
                evidence['count_observations'] = {key: [value] for key, value in totals.items()}
            if interrupted or execution_error:
                evidence['status'] = 'INTERRUPTED_DEADLINE' if interrupted else 'GATE_EXECUTION_FAILED'
                evidence['interrupted_stage'] = stage
                return 2, evidence
        if complete:
            remaining()
            if coverage_run is not None:
                stage = 'critical-write-coverage-evaluation'
                evidence['critical_write_coverage'] = critical_write_coverage.evaluate(coverage_run, native_scope_passed=True)
                remaining()
                if evidence['critical_write_coverage']['status'] != 'PASS_ACTUAL_CURRENT_CRITICAL_SCOPE':
                    evidence['status'] = 'CRITICAL_WRITE_COVERAGE_FAILED_OR_UNPROVEN'
                    return 2, evidence
            evidence['status'] = 'PASS_EXACT_DISCOVERED_SCOPE'
            return 0, evidence
        return native_code, evidence
    except subprocess.TimeoutExpired as expired:
        evidence['status'] = 'INTERRUPTED_DEADLINE'
        evidence['interrupted_stage'] = stage
        evidence['deadline_diagnostics'] = deadline_diagnostics(expired.output, discovered)
        return 2, evidence
    except critical_write_coverage.CoverageInputError as failure:
        evidence['status'] = 'CRITICAL_WRITE_COVERAGE_SETUP_FAILED'
        evidence['critical_write_coverage'] = {'status': 'FAILED_OR_UNPROVEN', 'errors': [str(failure)]}
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
    parser.add_argument('--shards', type=int, choices=range(1, 5), default=None)
    args = parser.parse_args()
    shards = args.shards if args.shards is not None else int(os.environ.get('POSEIDON_API_GATE_SHARDS', '1')) if args.scope == 'integration' else 1
    code, evidence = run(args.scope, shards=shards)
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
