"""DEV-067 / T-S10-004: pilot workload performance baseline (measured, never an SLA claim).

Runs only the opt-in `Category=Performance` API test (excluded from every api_test_gate.py
scope), which builds a FICTIONAL synthetic pilot fixture on an owned Testcontainers
PostgreSQL and writes raw per-request samples. This script computes p50/p95 from those
raw samples and writes a JSON envelope to .runtime/perf/. Queries slower than 500 ms are
reported with their endpoint key and parameterized SQL text (no parameter values).

Source: 18.01 Backlog DEV-067 "Today/readiness/audit queries bounded; agreed p95 envelope
recorded; slow query traces actionable." The *agreed* envelope is a product owner/QA
decision; this script records the measured value and leaves the agreement NOT_AGREED.

Docker is required by the measurement. When it is unavailable (or --skip-measurement is
given) the envelope is written with status NOT_RUN; a NOT_RUN result is never a pass.
Exit codes: 0 MEASURED, 2 FAILED, 3 NOT_RUN.

Usage: py scripts/perf_baseline.py [--iterations 30] [--work-orders 1000] [--vessels 10]
Requires a prior Release build (`dotnet build Poseidon.slnx -c Release`).
"""
import argparse
from datetime import datetime, timezone
import hashlib
import json
import math
import os
from pathlib import Path
import subprocess
import uuid

ROOT = Path(__file__).resolve().parents[1]
SCHEMA = 'poseidon.perf-baseline.v1'
RAW_SCHEMA = 'poseidon.perf-raw.v1'
THRESHOLD_MS = 500.0
SQL_LIMIT = 4000
MEANING = ('Measured on the stated host/transport with a synthetic FICTIONAL fixture. Not an SLA, not a '
           'production capacity claim and not an agreed p95 envelope; the agreement is a product owner/QA decision.')
PROJECT = 'tests/Poseidon.Api.Tests/Poseidon.Api.Tests.csproj'


def percentile(values, fraction):
    """Nearest-rank percentile (no interpolation): the smallest sample with rank >= ceil(p*n)."""
    if not values or not 0 < fraction <= 1:
        raise ValueError('percentile-input')
    ordered = sorted(values)
    rank = max(1, math.ceil(fraction * len(ordered)))
    return ordered[rank - 1]


def summarize(endpoint):
    samples, statuses = endpoint['samplesMs'], endpoint['statuses']
    commands = endpoint.get('dbCommands', [])
    if (not isinstance(samples, list) or not isinstance(statuses, list) or len(samples) != len(statuses) or not samples
            or any(isinstance(v, bool) or not isinstance(v, (int, float)) or v < 0 or not math.isfinite(v) for v in samples)
            or any(isinstance(s, bool) or not isinstance(s, int) for s in statuses)):
        raise ValueError('raw-endpoint-samples')
    ok = [v for v, s in zip(samples, statuses) if 200 <= s < 300]
    result = {'key': str(endpoint['key']), 'surface': str(endpoint.get('surface', '')),
              'requestsPerSample': int(endpoint.get('requestsPerSample', 1)),
              'samples': len(samples), 'successSamples': len(ok),
              'statusCounts': {str(s): statuses.count(s) for s in sorted(set(statuses))}}
    if len(ok) != len(samples):
        # Error-path timings are not a baseline for the success path; report, never average them in.
        result['state'] = 'NOT_MEASURED_SUCCESS_PATH'
        return result
    result.update({'state': 'MEASURED', 'p50Ms': percentile(ok, 0.50), 'p95Ms': percentile(ok, 0.95),
                   'minMs': min(ok), 'maxMs': max(ok), 'meanMs': round(sum(ok) / len(ok), 3)})
    if commands and len(commands) == len(samples) and all(isinstance(c, int) and c >= 0 for c in commands):
        result['dbCommandsPerSample'] = {'p50': percentile(commands, 0.50), 'max': max(commands)}
    return result


def slow_queries(raw):
    out = []
    for item in raw.get('slowQueries', []):
        duration = item.get('durationMs')
        if isinstance(duration, bool) or not isinstance(duration, (int, float)) or duration <= THRESHOLD_MS:
            continue
        sql = str(item.get('sql', ''))[:SQL_LIMIT]
        out.append({'endpoint': str(item.get('endpoint', 'unknown')), 'durationMs': duration, 'sql': sql,
                    'sqlSha256': hashlib.sha256(sql.encode('utf-8')).hexdigest()})
    return sorted(out, key=lambda q: -q['durationMs'])


def build_envelope(raw, context):
    if not isinstance(raw, dict) or raw.get('schemaVersion') != RAW_SCHEMA or not isinstance(raw.get('endpoints'), list) or not raw['endpoints']:
        raise ValueError('raw-schema')
    endpoints = [summarize(e) for e in raw['endpoints']]
    slow = slow_queries(raw)
    return {**context, 'status': 'MEASURED', 'fixture': raw.get('fixture'), 'transport': raw.get('transport'),
            'iterations': raw.get('iterations'), 'warmup': raw.get('warmup'), 'endpoints': endpoints,
            'slowQueryThresholdMs': THRESHOLD_MS, 'slowQueryCount': len(slow), 'slowQueries': slow,
            'notMeasuredSuccessPath': [e['key'] for e in endpoints if e['state'] != 'MEASURED']}


def base_context(root, args):
    commit = 'UNKNOWN'
    try:
        done = subprocess.run(['git', '-C', str(root), 'rev-parse', 'HEAD'], capture_output=True, timeout=30)
        if done.returncode == 0:
            commit = done.stdout.decode().strip()
    except (OSError, subprocess.SubprocessError):
        pass
    return {'schemaVersion': SCHEMA, 'requirement': 'DEV-067', 'testTrace': 'T-S10-004', 'commit': commit,
            'generatedAt': datetime.now(timezone.utc).isoformat(), 'slaClaim': False, 'meaning': MEANING,
            'agreedEnvelope': {'state': 'NOT_AGREED', 'owner': 'Product owner / QA', 'note': 'Record the agreed p95 envelope as a human decision.'},
            'requested': {'iterations': args.iterations, 'warmup': args.warmup, 'workOrders': args.work_orders, 'vessels': args.vessels}}


def docker_available():
    """Read-only probe; never starts or restarts Docker."""
    try:
        return subprocess.run(['docker', 'info', '--format', '{{.ServerVersion}}'], capture_output=True, timeout=30).returncode == 0
    except (OSError, subprocess.SubprocessError):
        return False


def dotnet(root):
    local = root / '.tools/dotnet' / ('dotnet.exe' if os.name == 'nt' else 'dotnet')
    return str(local) if local.is_file() else 'dotnet'


def measure(root, args, raw_path, runner=subprocess.run):
    env = {**os.environ, 'POSEIDON_PERF_OUTPUT': str(raw_path), 'POSEIDON_PERF_ITERATIONS': str(args.iterations),
           'POSEIDON_PERF_WARMUP': str(args.warmup), 'POSEIDON_PERF_WORK_ORDERS': str(args.work_orders),
           'POSEIDON_PERF_VESSELS': str(args.vessels)}
    command = [dotnet(root), 'test', '--project', PROJECT, '--configuration', 'Release', '--no-build',
               '--filter-trait', 'Category=Performance']
    # Raw runner output stays in memory: failed HTTP calls may include request data.
    done = runner(command, cwd=root, env=env, capture_output=True, timeout=args.timeout)
    return done.returncode, hashlib.sha256(done.stdout or b'').hexdigest()


def run(args, root=ROOT, probe=docker_available, runner=subprocess.run):
    context = base_context(root, args)
    if args.skip_measurement:
        return 3, {**context, 'status': 'NOT_RUN', 'reason': 'operator_skipped_measurement'}
    if not probe():
        return 3, {**context, 'status': 'NOT_RUN', 'reason': 'docker_unavailable'}
    directory = root / '.runtime/perf'
    directory.mkdir(parents=True, exist_ok=True)
    raw_path = directory / f'raw-{uuid.uuid4().hex}.json'
    try:
        code, output_hash = measure(root, args, raw_path, runner)
        context['runner'] = {'exit_code': code, 'output_sha256': output_hash, 'raw_output_persisted': False}
        if code != 0 or not raw_path.is_file():
            return 2, {**context, 'status': 'FAILED', 'reason': 'performance_test_failed_or_no_raw_samples'}
        return 0, build_envelope(json.loads(raw_path.read_text(encoding='utf-8')), context)
    except subprocess.TimeoutExpired:
        return 2, {**context, 'status': 'FAILED', 'reason': 'performance_test_deadline'}
    except (ValueError, KeyError, TypeError):
        return 2, {**context, 'status': 'FAILED', 'reason': 'raw_samples_invalid'}
    finally:
        raw_path.unlink(missing_ok=True)


def write(root, envelope):
    directory = root / '.runtime/perf'
    directory.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now(timezone.utc).strftime('%Y%m%dT%H%M%SZ')
    text = json.dumps(envelope, indent=2, ensure_ascii=False) + '\n'
    path = directory / f'perf-baseline-{stamp}.json'
    path.write_text(text, encoding='utf-8')
    (directory / 'latest.json').write_text(text, encoding='utf-8')
    return path


def parser():
    p = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    p.add_argument('--iterations', type=int, default=30, choices=range(5, 501), metavar='5..500')
    p.add_argument('--warmup', type=int, default=3, choices=range(0, 51), metavar='0..50')
    p.add_argument('--work-orders', type=int, default=1000, choices=range(1, 5001), metavar='1..5000')
    p.add_argument('--vessels', type=int, default=10, choices=range(1, 51), metavar='1..50')
    p.add_argument('--timeout', type=int, default=3000, choices=range(60, 3601), metavar='60..3600')
    p.add_argument('--skip-measurement', action='store_true', help='record NOT_RUN without probing Docker')
    return p


def main(argv=None):
    args = parser().parse_args(argv)
    code, envelope = run(args)
    path = write(ROOT, envelope)
    summary = {'status': envelope['status'], 'reason': envelope.get('reason'), 'envelope': str(path.relative_to(ROOT)),
               'endpoints': [{k: e.get(k) for k in ('key', 'state', 'p50Ms', 'p95Ms')} for e in envelope.get('endpoints', [])],
               'slowQueryCount': envelope.get('slowQueryCount')}
    print(json.dumps(summary, ensure_ascii=False))
    return code


if __name__ == '__main__':
    raise SystemExit(main())
