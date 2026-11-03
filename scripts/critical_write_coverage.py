"""Measure current critical command coverage from actual, value-free audit observations.

This does not accept declared route witnesses or replay reports from another run.
The API gate owns the fresh directory, target, contract digest and native outcome.
Parser unit fixtures are never production evidence.
"""
from dataclasses import dataclass
import argparse
import hashlib
from itertools import islice
import json
import os
from pathlib import Path
import re
import stat
import subprocess
import uuid

SCHEMA = 'poseidon.critical-write-observation.v1'
CONTRACT_SCHEMA = 'poseidon.s1-current-security-scope.v1'
REASONS = frozenset({'PREPARATION_COMMAND', 'FILE_TRANSPORT', 'OFFLINE_LIFECYCLE',
                     'REGULATORY_WORKFLOW', 'HUMAN_REVIEW_WORKFLOW', 'SOURCE_QUALIFICATION'})
ENV_PREFIX = 'POSEIDON_CRITICAL_WRITE_COVERAGE_'
MAX_REPORT_BYTES = 262144
MAX_REPORTS = 4096
MAX_TOTAL_BYTES = 32 * 1024 * 1024
OPERATION = re.compile(r'[A-Z][A-Za-z0-9]{0,95}\Z', re.ASCII)
ENTITY = re.compile(r'[A-Z][A-Za-z0-9]{0,79}\Z', re.ASCII)
HEX32 = re.compile(r'[0-9a-f]{32}\Z')
HEX40 = re.compile(r'[0-9a-f]{40}\Z')
HEX64 = re.compile(r'[0-9a-f]{64}\Z')
VIOLATIONS = frozenset({
    'AUDIT_ACTOR_INVALID', 'AUDIT_ROLE_INVALID', 'AUDIT_TENANT_INVALID',
    'AUDIT_CORRELATION_INVALID', 'AUDIT_TIMESTAMP_INVALID', 'AUDIT_RESOURCE_INVALID',
    'AUDIT_ENTITY_INVALID', 'AUDIT_ACTION_INVALID', 'AUDIT_SUMMARY_INVALID',
    'AUDIT_COMMAND_INVALID', 'AUDIT_REASON_INVALID', 'AUDIT_SCOPE_INVALID',
    'AUDIT_BEFORE_AFTER_UNPROVEN', 'AUDIT_QUERY_UNAVAILABLE', 'AUDIT_ROW_LIMIT_EXCEEDED',
    'HTTP_COMMAND_UNOBSERVED',
})


class CoverageInputError(ValueError):
    """Fixed cause only: never include paths, source content or exception text."""


@dataclass(frozen=True)
class CoverageRun:
    directory: Path
    contract: Path
    contract_sha256: str
    run_id: str
    commit: str
    tree: str
    scope: str
    root: Path

    def environment(self, parent):
        result = clean_environment(parent)
        for key, value in {
            'DIRECTORY': str(self.directory), 'CONTRACT': str(self.contract),
            'CONTRACT_SHA256': self.contract_sha256, 'RUN_ID': self.run_id,
            'TARGET_COMMIT': self.commit, 'TARGET_TREE': self.tree, 'SCOPE': self.scope,
        }.items():
            result[ENV_PREFIX + key] = value
        return result


def clean_environment(parent):
    return {key: value for key, value in parent.items() if not key.startswith(ENV_PREFIX)}


def _no_duplicate_keys(pairs):
    result = {}
    for key, value in pairs:
        if key in result:
            raise CoverageInputError('DUPLICATE_JSON_KEY')
        result[key] = value
    return result


def _linked(info):
    return stat.S_ISLNK(info.st_mode) or bool(getattr(info, 'st_file_attributes', 0) & 0x400)


def _directory(path):
    if not path.is_absolute():
        raise CoverageInputError('UNSAFE_CAPTURE_DIRECTORY')
    for item in (path, *path.parents):
        info = item.lstat()
        if _linked(info) or not stat.S_ISDIR(info.st_mode):
            raise CoverageInputError('UNSAFE_CAPTURE_DIRECTORY')


def _read_bytes(path, limit=MAX_REPORT_BYTES):
    _directory(path.parent)
    before = path.lstat()
    if _linked(before) or not stat.S_ISREG(before.st_mode) or before.st_nlink != 1 or not 0 < before.st_size <= limit:
        raise CoverageInputError('UNSAFE_OR_OVERSIZED_INPUT')
    fd = os.open(path, os.O_RDONLY | getattr(os, 'O_NOFOLLOW', 0) | getattr(os, 'O_BINARY', 0))
    with os.fdopen(fd, 'rb') as stream:
        actual = os.fstat(stream.fileno())
        if (actual.st_dev, actual.st_ino) != (before.st_dev, before.st_ino) or actual.st_nlink != 1:
            raise CoverageInputError('INPUT_CHANGED_DURING_READ')
        raw = stream.read(limit + 1)
    after = path.lstat()
    if _linked(after) or (after.st_dev, after.st_ino, after.st_size) != (actual.st_dev, actual.st_ino, len(raw)):
        raise CoverageInputError('INPUT_CHANGED_DURING_READ')
    if not 0 < len(raw) <= limit:
        raise CoverageInputError('UNSAFE_OR_OVERSIZED_INPUT')
    return raw


def _read(path, limit=MAX_REPORT_BYTES):
    raw = _read_bytes(path, limit)
    try:
        return raw, json.loads(raw.decode('utf-8'), object_pairs_hook=_no_duplicate_keys)
    except (UnicodeError, json.JSONDecodeError, RecursionError):
        raise CoverageInputError('MALFORMED_JSON') from None


def source_hints(root, methods):
    """Only method identifiers found in actual local source; never echo arbitrary input."""
    remaining = {name for name in methods if isinstance(name, str) and re.fullmatch(r'[A-Za-z_][A-Za-z0-9_]{0,199}', name)}
    found, total = [], 0
    directory = Path(root) / 'tests/Poseidon.Api.Tests'
    try:
        paths = sorted(islice(directory.glob('*.cs'), 1025))
        if len(paths) > 1024: return []
        for path in paths:
            if not remaining: break
            if not re.fullmatch(r'[A-Za-z][A-Za-z0-9_]*\.cs', path.name): continue
            raw = _read_bytes(path, 1024 * 1024); total += len(raw)
            if total > 16 * 1024 * 1024: return []
            lines = raw.decode('utf-8-sig').splitlines()
            for number, line in enumerate(lines, 1):
                for name in sorted(remaining):
                    if re.search(r'\bpublic\s+(?:async\s+)?(?:Task(?:<[^>]+>)?|ValueTask|void)\s+' + re.escape(name) + r'\s*\(', line):
                        found.append({'file': path.name, 'line': number, 'method': name}); remaining.remove(name)
    except (OSError, CoverageInputError, UnicodeError):
        return []
    return found


def denominator(contract):
    if not isinstance(contract, dict) or contract.get('schemaVersion') != CONTRACT_SCHEMA:
        raise CoverageInputError('WRONG_CONTRACT_SCHEMA')
    reasons = contract.get('reasonCodes')
    if not isinstance(reasons, list) or len(reasons) != len(REASONS) or any(not isinstance(r, str) for r in reasons) or set(reasons) != REASONS:
        raise CoverageInputError('INVALID_REASON_CATALOG')
    operations = contract.get('operations')
    if not isinstance(operations, list) or not 1 <= len(operations) <= 1024:
        raise CoverageInputError('INVALID_OPERATION_CATALOG')
    expected, templates, ids, routes, variant_ids = {}, 0, set(), set(), set()
    for operation in operations:
        if not isinstance(operation, dict):
            raise CoverageInputError('INVALID_OPERATION_CATALOG')
        name, method, route = (operation.get(k) for k in ('operationId', 'method', 'routePattern'))
        if not isinstance(name, str) or not OPERATION.fullmatch(name) or name in ids or method not in ('POST', 'PUT', 'PATCH', 'DELETE') or not isinstance(route, str) or not route.startswith('/api/v1/') or len(route) > 300 or (method, route) in routes:
            raise CoverageInputError('INVALID_OPERATION_CATALOG')
        ids.add(name); routes.add((method, route))
        variants = operation.get('variants')
        if not isinstance(variants, list) or len(variants) > 32:
            raise CoverageInputError('INVALID_OPERATION_VARIANTS')
        if operation.get('kind') != 'CriticalWrite':
            if operation.get('kind') not in ('SecurityObservation', 'AlwaysDenied') or variants or operation.get('reasonCode') is not None:
                raise CoverageInputError('UNKNOWN_OPERATION_KIND')
            continue
        templates += 1
        reason = operation.get('reasonCode')
        if reason not in REASONS:
            raise CoverageInputError('INVALID_OPERATION_REASON')
        concrete = []
        route_values = set()
        for variant in variants:
            if not isinstance(variant, dict) or set(variant) != {'routeValue', 'operationId'}:
                raise CoverageInputError('INVALID_OPERATION_VARIANTS')
            value, variant_id = variant['routeValue'], variant['operationId']
            if not isinstance(value, str) or not re.fullmatch(r'[a-z]{1,32}', value) or value in route_values or not isinstance(variant_id, str) or not OPERATION.fullmatch(variant_id):
                raise CoverageInputError('INVALID_OPERATION_VARIANTS')
            route_values.add(value); concrete.append(variant_id); variant_ids.add(variant_id)
        for concrete_id in concrete or [name]:
            if concrete_id in expected:
                raise CoverageInputError('DUPLICATE_CRITICAL_OPERATION')
            expected[concrete_id] = {'operation_id': concrete_id, 'reason_code': reason,
                                     'family': operation.get('family'), 'scope': operation.get('scope')}
    if variant_ids & ids or not expected or any(type(contract.get(key)) is not int for key in ('mutationRouteTemplates', 'criticalRouteTemplates', 'criticalOperations')) or contract.get('mutationRouteTemplates') != len(operations) or contract.get('criticalRouteTemplates') != templates or contract.get('criticalOperations') != len(expected):
        raise CoverageInputError('CONTRACT_DENOMINATOR_MISMATCH')
    return expected, templates


def prepare(root, scope):
    if scope not in ('integration', 'all'):
        raise CoverageInputError('WRONG_NATIVE_SCOPE')
    root = Path(root).absolute()
    contract = root / 'contracts/security/s1-current-scope.v1.json'
    try:
        raw, definition = _read(contract)
    except OSError:
        raise CoverageInputError('CURRENT_SCOPE_CONTRACT_UNAVAILABLE') from None
    denominator(definition)
    def git(*args):
        result = subprocess.run(['git', *args], cwd=root, capture_output=True, timeout=20)
        if result.returncode:
            raise CoverageInputError('TARGET_BINDING_UNAVAILABLE')
        return result.stdout.decode('ascii').strip()
    commit, tree = git('rev-parse', 'HEAD'), git('rev-parse', 'HEAD^{tree}')
    if not HEX40.fullmatch(commit) or not HEX40.fullmatch(tree) or git('status', '--porcelain=v1', '--untracked-files=normal'):
        raise CoverageInputError('TARGET_NOT_CLEAN_AND_PINNED')
    parent = root / '.runtime/api-gate'
    parent.mkdir(parents=True, exist_ok=True)
    _directory(parent)
    run_id = uuid.uuid4().hex
    directory = parent / ('critical-write-' + run_id)
    directory.mkdir(mode=0o700, exist_ok=False)
    _directory(directory)
    return CoverageRun(directory, contract, hashlib.sha256(raw).hexdigest(), run_id, commit, tree, scope, root)


def verify_target(run):
    def git(*args):
        result = subprocess.run(['git', *args], cwd=run.root, capture_output=True, timeout=20)
        if result.returncode:
            raise CoverageInputError('TARGET_BINDING_UNAVAILABLE')
        return result.stdout.decode('ascii').strip()
    if git('rev-parse', 'HEAD') != run.commit or git('rev-parse', 'HEAD^{tree}') != run.tree or git('status', '--porcelain=v1', '--untracked-files=normal'):
        raise CoverageInputError('TARGET_CHANGED_DURING_NATIVE_SCOPE')


def evaluate(run, native_scope_passed):
    report = {'schema_version': 'poseidon.critical-write-coverage.v1',
              'status': 'FAILED_OR_UNPROVEN', 'native_scope_passed': native_scope_passed is True,
              'run_id': run.run_id, 'contract_sha256': run.contract_sha256,
              'target': {'commit': run.commit, 'tree': run.tree}, 'scope': run.scope,
              'declared_routes_are_execution_evidence': False, 'raw_audit_values_exported': False,
              'count_semantics': 'committed_audit_rows_joined_to_actual_http_requests_not_accepted_business_flows',
              'errors': []}
    try:
        if run.scope not in ('integration', 'all') or not HEX32.fullmatch(run.run_id) or not HEX64.fullmatch(run.contract_sha256) or not HEX40.fullmatch(run.commit) or not HEX40.fullmatch(run.tree):
            raise CoverageInputError('INVALID_TRUSTED_RUN_BINDING')
        verify_target(run)
        raw, definition = _read(run.contract)
        raw_contract_text = raw.decode('utf-8')
        if hashlib.sha256(raw).hexdigest() != run.contract_sha256:
            raise CoverageInputError('CONTRACT_DIGEST_MISMATCH')
        expected, templates = denominator(definition)
        report.update(critical_route_templates=templates, required_operations=len(expected))
        _directory(run.directory)
        paths = sorted(islice(run.directory.iterdir(), MAX_REPORTS + 1))
        if not 1 <= len(paths) <= MAX_REPORTS:
            raise CoverageInputError('MISSING_OR_EXCESSIVE_CAPTURE_REPORTS')
        seen, observed, total_bytes, unavailable, invalid, rows, valid_rows, excluded = set(), {}, 0, 0, 0, 0, 0, 0
        all_violations = set()
        capture_hashes, affected_methods = [], set()
        for path in paths:
            raw, data = _read(path)
            total_bytes += len(raw)
            if total_bytes > MAX_TOTAL_BYTES:
                raise CoverageInputError('CAPTURE_BYTES_EXCEEDED')
            if not isinstance(data, dict) or set(data) != {'schemaVersion', 'runId', 'contractSha256', 'target', 'scope', 'instanceId', 'fixture', 'testMethod', 'status', 'rowCount', 'excludedDirectFixtureRows', 'observations', 'violations', 'unavailableCause'}:
                raise CoverageInputError('MALFORMED_CAPTURE_REPORT')
            if data['testMethod'] is not None and (not isinstance(data['testMethod'], str) or not re.fullmatch(r'[A-Za-z_][A-Za-z0-9_]{0,199}', data['testMethod'])):
                raise CoverageInputError('MALFORMED_STATIC_TEST_CONTEXT')
            instance = data['instanceId']
            if not isinstance(instance, str) or not HEX32.fullmatch(instance) or path.name != instance + '.json' or instance in seen:
                raise CoverageInputError('DUPLICATE_OR_INVALID_INSTANCE')
            seen.add(instance)
            capture_hashes.append({'file': path.name, 'sha256': hashlib.sha256(raw).hexdigest()})
            if data['schemaVersion'] != SCHEMA or data['runId'] != run.run_id or data['contractSha256'] != run.contract_sha256 or data['target'] != {'commit': run.commit, 'tree': run.tree} or data['scope'] != run.scope or data['fixture'] != 'DirectoryWorkspaceTests':
                raise CoverageInputError('STALE_OR_WRONG_SCOPE_CAPTURE')
            violations = data['violations']
            if not isinstance(violations, list) or len(violations) > len(VIOLATIONS) or len(set(violations)) != len(violations) or any(v not in VIOLATIONS for v in violations):
                raise CoverageInputError('MALFORMED_CAPTURE_VIOLATIONS')
            all_violations.update(violations)
            if violations or data['status'] == 'UNAVAILABLE': affected_methods.add(data['testMethod'])
            if data['status'] == 'UNAVAILABLE':
                if data['rowCount'] is not None or data['excludedDirectFixtureRows'] is not None or data['observations'] != [] or data['unavailableCause'] != 'OWNED_AUDIT_QUERY_UNAVAILABLE' or violations != ['AUDIT_QUERY_UNAVAILABLE']:
                    raise CoverageInputError('MALFORMED_UNAVAILABLE_CAPTURE')
                unavailable += 1
                continue
            if data['status'] != 'OBSERVED' or type(data['rowCount']) is not int or not 0 <= data['rowCount'] <= 100000 or type(data['excludedDirectFixtureRows']) is not int or not 0 <= data['excludedDirectFixtureRows'] <= data['rowCount'] or data['unavailableCause'] is not None or not isinstance(data['observations'], list) or len(data['observations']) > 1024:
                raise CoverageInputError('MALFORMED_CAPTURE_REPORT')
            count, report_valid, report_ids = 0, 0, set()
            for item in data['observations']:
                if not isinstance(item, dict) or set(item) != {'operationId', 'observedRows', 'validRows', 'successfulHttpRows', 'nonSuccessHttpRows', 'entityTypes'}:
                    raise CoverageInputError('MALFORMED_OPERATION_OBSERVATION')
                operation, number, valid, entities = (item[k] for k in ('operationId', 'observedRows', 'validRows', 'entityTypes'))
                success, non_success = item['successfulHttpRows'], item['nonSuccessHttpRows']
                if operation not in expected or operation in report_ids or type(number) is not int or type(valid) is not int or not 1 <= number <= 100000 or not 0 <= valid <= number or type(success) is not int or type(non_success) is not int or success < 0 or non_success < 0 or success + non_success != number or not isinstance(entities, list) or not 0 <= len(entities) <= 256 or not entities and 'AUDIT_ENTITY_INVALID' not in violations or len(set(entities)) != len(entities) or any(not isinstance(e, str) or not ENTITY.fullmatch(e) for e in entities):
                    raise CoverageInputError('MALFORMED_OPERATION_OBSERVATION')
                report_ids.add(operation); count += number; report_valid += valid
                current = observed.setdefault(operation, {'operation_id': operation, 'observed_rows': 0, 'valid_rows': 0, 'successful_http_rows': 0, 'non_success_http_rows': 0, 'entity_types': set()})
                current['observed_rows'] += number; current['valid_rows'] += valid; current['entity_types'].update(entities)
                current['successful_http_rows'] += success; current['non_success_http_rows'] += non_success
            # Unknown command rows are counted in rowCount but omitted from operation buckets.
            # Their mandatory AUDIT_COMMAND_INVALID violation prevents them from becoming evidence.
            if count + data['excludedDirectFixtureRows'] > data['rowCount'] or count + data['excludedDirectFixtureRows'] < data['rowCount'] and 'AUDIT_COMMAND_INVALID' not in violations:
                raise CoverageInputError('CAPTURE_ROW_ACCOUNTING_MISMATCH')
            if report_valid != count and not violations:
                raise CoverageInputError('INVALID_ROWS_WITHOUT_VIOLATION')
            rows += data['rowCount']; valid_rows += report_valid; excluded += data['excludedDirectFixtureRows']; invalid += data['rowCount'] - data['excludedDirectFixtureRows'] - report_valid
        positive = {name for name, item in observed.items() if item['valid_rows'] > 0}
        missing = sorted(set(expected) - positive)
        verify_target(run)
        report.update(capture_instances=len(seen), unavailable_instances=unavailable,
                      observed_rows=rows, excluded_direct_fixture_rows=excluded, valid_rows=valid_rows, invalid_rows=invalid,
                      observed_operations=len(positive), missing_operation_ids=missing,
                      capture_report_hashes=capture_hashes,
                      audit_observation_diagnostics=source_hints(run.root, affected_methods),
                      missing_operation_diagnostics=[{
                          'operation_id': name,
                          'contract_file': 'contracts/security/s1-current-scope.v1.json',
                          'contract_line': next((i for i, line in enumerate(raw_contract_text.splitlines(), 1)
                              if re.search(r'"operationId"\s*:\s*"' + re.escape(name) + '"', line)), None),
                          'test_fixture_file': 'DirectoryWorkspaceTests.cs',
                          'test_evidence': 'actual_positive_http_audit_not_observed',
                      } for name in missing],
                      violations=sorted(all_violations),
                      observations=[dict(item, entity_types=sorted(item['entity_types'])) for _, item in sorted(observed.items())])
        if native_scope_passed is not True:
            report['errors'].append('NATIVE_SCOPE_NOT_PROVEN')
        if unavailable:
            report['errors'].append('AUDIT_OBSERVATION_UNAVAILABLE')
        if all_violations or invalid:
            report['errors'].append('AUDIT_CONTEXT_OR_BEFORE_AFTER_INVALID')
        if missing:
            report['errors'].append('CRITICAL_OPERATIONS_UNOBSERVED')
        if not report['errors']:
            report['status'] = 'PASS_ACTUAL_CURRENT_CRITICAL_SCOPE'
            report['coverage_percent'] = 100
        else:
            # A complete observed set cannot become a 100% claim when a native scope,
            # context, run binding or unavailable observation remains unproven.
            report['coverage_percent'] = None
    except CoverageInputError as error:
        report['errors'].append(str(error))
    except (OSError, ValueError, TypeError, KeyError):
        report['errors'].append('CAPTURE_EVALUATION_UNAVAILABLE')
    return report


def native_scope_complete(evidence, run):
    """Replay only a closed native scope bound by the actual API gate to this run."""
    if not isinstance(evidence, dict) or evidence.get('scope') != run.scope or evidence.get('raw_output_persisted') is not False or evidence.get('status') not in (
            'PASS_EXACT_DISCOVERED_SCOPE', 'CRITICAL_WRITE_COVERAGE_FAILED_OR_UNPROVEN'):
        return False
    discovery = evidence.get('discovery')
    if not isinstance(discovery, dict) or type(discovery.get('exit_code')) is not int or discovery['exit_code'] != 0 or type(discovery.get('cases')) is not int or discovery['cases'] <= 0 or type(evidence.get('exit_code')) is not int or evidence['exit_code'] != 0:
        return False
    counts = evidence.get('count_observations')
    if not isinstance(counts, dict) or set(counts) != {'total', 'failed', 'succeeded', 'skipped'}:
        return False
    if any(not isinstance(values, list) or len(values) != 1 or type(values[0]) is not int for values in counts.values()):
        return False
    if counts != {'total': [discovery['cases']], 'failed': [0], 'succeeded': [discovery['cases']], 'skipped': [0]}:
        return False
    actual = evidence.get('critical_write_coverage')
    return isinstance(actual, dict) and actual.get('run_id') == run.run_id and actual.get('scope') == run.scope and actual.get('contract_sha256') == run.contract_sha256 and actual.get('target') == {'commit': run.commit, 'tree': run.tree}


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--directory', type=Path, required=True)
    parser.add_argument('--contract', type=Path, required=True)
    parser.add_argument('--native-evidence', type=Path, required=True)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    args = parser.parse_args(argv)
    try:
        _, native = _read(args.native_evidence.absolute(), 2 * 1024 * 1024)
        actual = native['critical_write_coverage']
        run = CoverageRun(args.directory.absolute(), args.contract.absolute(), actual['contract_sha256'],
            actual['run_id'], actual['target']['commit'], actual['target']['tree'], actual['scope'], args.root.absolute())
        report = evaluate(run, native_scope_complete(native, run))
    except (OSError, ValueError, TypeError, KeyError):
        report = {'status': 'FAILED_OR_UNPROVEN', 'errors': ['NATIVE_REPLAY_INPUT_UNAVAILABLE'], 'raw_audit_values_exported': False}
    print(json.dumps(report))
    return 0 if report['status'] == 'PASS_ACTUAL_CURRENT_CRITICAL_SCOPE' else 2


if __name__ == '__main__':
    raise SystemExit(main())
