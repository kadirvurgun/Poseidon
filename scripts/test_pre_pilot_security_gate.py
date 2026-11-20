import json
import subprocess
import tempfile
import unittest
from pathlib import Path

import pre_pilot_security_gate as gate

PROFILES = [
    {'profileId': 'NATIVE-PUBLIC-1', 'kind': 'PUBLIC_READ', 'authentication': 'ANONYMOUS', 'csrf': 'NOT_APPLICABLE'},
    {'profileId': 'NATIVE-SESSION-1', 'kind': 'SESSION_READ', 'authentication': 'ANONYMOUS', 'csrf': 'NOT_APPLICABLE'},
    {'profileId': 'NATIVE-READ-1', 'kind': 'READ', 'authentication': 'REQUIRED', 'csrf': 'NOT_APPLICABLE'},
    {'profileId': 'NATIVE-COMMAND-1', 'kind': 'COMMAND', 'authentication': 'REQUIRED', 'csrf': 'REQUIRED'},
]


def endpoint(method, path, profile, anonymous=False, policies=('DirectoryRead',), operation='Op', deviations=()):
    return {'method': method, 'normalizedPath': path, 'profileId': profile, 'anonymous': anonymous,
            'authorizationPolicies': list(policies), 'securityOperationId': operation if method != 'GET' else None,
            'owner': 'X', 'deviations': list(deviations)}


GOOD = [endpoint('GET', '/health/live', 'NATIVE-PUBLIC-1', True, ()),
        endpoint('GET', '/api/v1/vessels/{id:guid}/critical-states', 'NATIVE-READ-1'),
        endpoint('GET', '/api/v1/work-orders/{id:guid}/history', 'NATIVE-READ-1'),
        endpoint('POST', '/api/v1/vessels', 'NATIVE-COMMAND-1')]

TESTS = {
    'tests/Poseidon.Api.Tests/CriticalStateTests.cs':
        'public sealed partial class DirectoryWorkspaceTests {\n'
        ' private static string P(Guid id) => $"/api/v1/vessels/{id}/critical-states";\n'
        ' public async Task Denies() { Assert.Equal(HttpStatusCode.NotFound, x); } }\n',
    'tests/Poseidon.Api.Tests/WorkOrderTests.cs':
        'public sealed partial class DirectoryWorkspaceTests {\n'
        ' string W(Guid? id, string s) => "/api/v1/work-orders" + "/" + id + s;\n'
        ' public async Task History() { await c.GetAsync(W(id, "/history")); Assert.Equal(HttpStatusCode.Forbidden, r); } }\n',
}


def mapped_tests():
    names = {t for c in gate.CONTROLS for t in c['tests']}
    files = {}
    for name in sorted(names):
        stem, method = name.split('.')
        files.setdefault(f'tests/Poseidon.Api.Tests/{stem}.cs', []).append(method)
    out = {}
    for path, methods in files.items():
        trait = '' if 'PermissionAllowlist' in path else '[Trait("Category", "Integration")]\n'
        body = '\n'.join(f'  public async Task {m}() {{ }}' for m in methods)
        out[path] = f'{trait}public sealed class {Path(path).stem} {{\n{body}\n}}\n'
    return out


class Fixture:
    def __init__(self, endpoints=GOOD, tests=None, controls=True):
        self.dir = tempfile.TemporaryDirectory()
        self.root = Path(self.dir.name)
        registry = {'profiles': PROFILES, 'endpoints': endpoints}
        (self.root / 'contracts/security').mkdir(parents=True)
        (self.root / gate.REGISTRY).write_text(json.dumps(registry), encoding='utf-8')
        (self.root / gate.S1_SCOPE).write_text(json.dumps({'mutationRouteTemplates': sum(e['method'] != 'GET' for e in endpoints)}), encoding='utf-8')
        files = {**(mapped_tests() if controls else {}), **(TESTS if tests is None else tests)}
        for name, text in files.items():
            path = self.root / name
            path.parent.mkdir(parents=True, exist_ok=True)
            path.write_text(text, encoding='utf-8')

    def evidence(self, unit='PASS_EXACT_DISCOVERED_SCOPE', integration='PASS_EXACT_DISCOVERED_SCOPE'):
        directory = self.root / '.runtime/api-gate'
        directory.mkdir(parents=True, exist_ok=True)
        for scope, status in (('unit', unit), ('integration', integration)):
            (directory / f'{scope}.json').write_text(json.dumps({'scope': scope, 'status': status, 'exit_code': 0 if status.startswith('PASS') else 2}), encoding='utf-8')
        return directory

    def close(self):
        self.dir.cleanup()


def runner(scan_codes=None, head='abc123', scan_output=None):
    scan_codes = scan_codes or {'repository': 0, 'client': 0}
    calls = []
    def run(command, **_kwargs):
        calls.append(command)
        if command[0] == 'git':
            return subprocess.CompletedProcess(command, 0, (head + '\n').encode())
        if command[1].endswith('reviewed_secret_scan.py'):
            mode = command[command.index('--mode') + 1]
            code = scan_codes[mode]
            output = scan_output if code == 1 and scan_output else json.dumps({'mode': mode, 'covered_files': 3, 'findings': 0}).encode()
            return subprocess.CompletedProcess(command, code, output)
        return subprocess.CompletedProcess(command, 0, b'{}')
    run.calls = calls
    return run


class PrePilotSecurityGateTests(unittest.TestCase):
    def fixture(self, **kwargs):
        fixture = Fixture(**kwargs)
        self.addCleanup(fixture.close)
        (fixture.root / 'frontend/dist').mkdir(parents=True)
        (fixture.root / 'frontend/dist/index.html').write_text('<html></html>', encoding='utf-8')
        return fixture

    def test_all_required_checks_run_and_bound_passing_evidence_is_pass(self):
        f = self.fixture()
        code, report = gate.evaluate(f.root, runner(), api_evidence_dir=f.evidence(), api_evidence_commit='abc123')
        self.assertEqual((0, 'PASS'), (code, report['status']), report['findings'])
        self.assertEqual({'critical': 0, 'high': 0, 'medium': 0}, report['counts'])
        self.assertTrue(all(c['state'] == 'PROVEN' for c in report['controls']))
        rows = {r['path']: r for r in report['routeMatrix']}
        self.assertEqual('ROUTE_TESTS_WITH_DENIAL_ASSERTIONS', rows['/api/v1/vessels/{id:guid}/critical-states']['coverage'])
        self.assertEqual('ROUTE_FRAGMENTS_WITH_DENIAL_ASSERTIONS', rows['/api/v1/work-orders/{id:guid}/history']['coverage'])
        self.assertEqual('MUTATION_SWEEP', rows['/api/v1/vessels']['coverage'])
        self.assertEqual({'anonymous': '401', 'unauthorizedRole': '403', 'foreignTenant': '404_OR_403_WITHOUT_DISCLOSURE'},
                         rows['/api/v1/vessels']['expectation'])
        self.assertEqual('ALLOWED_PUBLIC', rows['/health/live']['expectation']['anonymous'])

    def test_secret_finding_is_critical_and_exits_one_without_printing_values(self):
        f = self.fixture()
        output = b'{"path": "a.txt", "rule": "private-key", "line": 3}\n{"mode": "repository", "covered_files": 3, "findings": 1}\n'
        code, report = gate.evaluate(f.root, runner({'repository': 1, 'client': 0}, scan_output=output),
                                     api_evidence_dir=f.evidence(), api_evidence_commit='abc123')
        self.assertEqual((1, 'FAIL'), (code, report['status']))
        self.assertEqual(1, report['counts']['critical'])
        self.assertEqual('a.txt:3', report['findings'][0]['subject'])

    def test_unavailable_scan_trust_is_not_run_and_never_a_pass(self):
        f = self.fixture()
        code, report = gate.evaluate(f.root, runner({'repository': 2, 'client': 0}), api_evidence_dir=f.evidence(), api_evidence_commit='abc123')
        self.assertEqual((2, 'INCOMPLETE'), (code, report['status']))
        self.assertIn('secret-scan-repository', report['incompleteRequiredChecks'])

    def test_missing_frontend_output_is_not_run(self):
        f = self.fixture()
        (f.root / 'frontend/dist/index.html').unlink()
        code, report = gate.evaluate(f.root, runner(), api_evidence_dir=f.evidence(), api_evidence_commit='abc123')
        self.assertEqual(2, code)
        client = next(c for c in report['checks'] if c['check'] == 'secret-scan-client')
        self.assertEqual(('NOT_RUN', 'frontend_production_output_absent'), (client['state'], client['reason']))

    def test_unexpected_anonymous_route_and_anonymous_mutation_are_critical(self):
        f = self.fixture(endpoints=[*GOOD, endpoint('GET', '/api/v1/crew', 'NATIVE-PUBLIC-1', True, ()),
                                    endpoint('POST', '/api/v1/crew', 'NATIVE-COMMAND-1', True, ())])
        code, report = gate.evaluate(f.root, runner(), api_evidence_dir=f.evidence(), api_evidence_commit='abc123')
        rules = {x['rule'] for x in report['findings'] if x['severity'] == 'critical'}
        self.assertEqual(1, code)
        self.assertTrue({'UNEXPECTED_ANONYMOUS_ROUTE', 'ANONYMOUS_MUTATION'} <= rules)

    def test_mutation_without_csrf_or_s1_operation_and_unknown_profile_are_high(self):
        f = self.fixture(endpoints=[*GOOD, endpoint('PUT', '/api/v1/a', 'NATIVE-READ-1'),
                                    endpoint('POST', '/api/v1/b', 'NATIVE-COMMAND-1', operation=None),
                                    endpoint('GET', '/api/v1/c', 'UNASSIGNED')])
        code, report = gate.evaluate(f.root, runner(), api_evidence_dir=f.evidence(), api_evidence_commit='abc123')
        rules = {x['rule'] for x in report['findings'] if x['severity'] == 'high'}
        self.assertEqual(1, code)
        self.assertTrue({'MUTATION_WITHOUT_CSRF_PROFILE', 'MUTATION_WITHOUT_S1_OPERATION', 'UNKNOWN_OR_UNASSIGNED_PROFILE'} <= rules)

    def test_s1_scope_count_mismatch_is_high(self):
        f = self.fixture()
        (f.root / gate.S1_SCOPE).write_text(json.dumps({'mutationRouteTemplates': 99}), encoding='utf-8')
        code, report = gate.evaluate(f.root, runner(), api_evidence_dir=f.evidence(), api_evidence_commit='abc123')
        self.assertEqual(1, code)
        self.assertIn('S1_SCOPE_COUNT_MISMATCH', {x['rule'] for x in report['findings']})

    def test_missing_mapped_control_test_is_high(self):
        f = self.fixture(controls=False)
        code, report = gate.evaluate(f.root, runner(), api_evidence_dir=f.evidence(), api_evidence_commit='abc123')
        self.assertEqual(1, code)
        self.assertTrue(all(c['state'] == 'MISSING_TEST' for c in report['controls']))

    def test_uncovered_read_route_is_medium_and_does_not_block(self):
        f = self.fixture(endpoints=[*GOOD, endpoint('GET', '/api/v1/untested-surface', 'NATIVE-READ-1')])
        code, report = gate.evaluate(f.root, runner(), api_evidence_dir=f.evidence(), api_evidence_commit='abc123')
        self.assertEqual((0, 1), (code, report['counts']['medium']))
        self.assertEqual('NO_STATIC_TEST_REFERENCE', report['routeMatrix'][-1]['coverage'])

    def test_evidence_absent_stale_or_failing_never_proves_controls(self):
        f = self.fixture()
        code, report = gate.evaluate(f.root, runner())
        self.assertEqual((2, 'NOT_PROVIDED'), (code, report['checks'][3]['evidence']))
        code, report = gate.evaluate(f.root, runner(), api_evidence_dir=f.evidence(), api_evidence_commit='old')
        self.assertEqual((2, 'UNBOUND_OR_STALE_COMMIT'), (code, report['checks'][3]['evidence']))
        code, report = gate.evaluate(f.root, runner(), api_evidence_dir=f.evidence(integration='FAILED_OR_UNPROVEN'), api_evidence_commit='abc123')
        self.assertEqual(1, code)
        self.assertIn('API_GATE_EVIDENCE_NOT_PASSING', {x['rule'] for x in report['findings']})

    def test_performance_trait_tests_never_count_as_security_evidence(self):
        perf = {'tests/Poseidon.Api.Tests/Performance/P.cs':
                '[Trait("Category", "Performance")] class P { void M() { var u = "/api/v1/untested-surface"; Assert.Equal(HttpStatusCode.Forbidden, x); } }'}
        f = self.fixture(endpoints=[*GOOD, endpoint('GET', '/api/v1/untested-surface', 'NATIVE-READ-1')], tests={**TESTS, **perf})
        _, report = gate.evaluate(f.root, runner(), api_evidence_dir=f.evidence(), api_evidence_commit='abc123')
        self.assertEqual('NO_STATIC_TEST_REFERENCE', report['routeMatrix'][-1]['coverage'])

    def test_optional_clamd_and_release_gate_are_not_run_by_default(self):
        f = self.fixture()
        run = runner()
        code, report = gate.evaluate(f.root, run, api_evidence_dir=f.evidence(), api_evidence_commit='abc123')
        optional = {c['check']: c['state'] for c in report['checks'] if c.get('required') is False}
        self.assertEqual({'clamd-readiness': 'NOT_RUN', 'release-gate': 'NOT_RUN'}, optional)
        self.assertFalse(any('clamd_gate.py' in ' '.join(map(str, c)) for c in run.calls))
        self.assertEqual(0, code)

    def test_route_regex_matches_absolute_relative_and_interpolated_literals(self):
        pattern = gate.route_regex('/api/v1/vessels/{id:guid}/critical-states')
        self.assertTrue(pattern.search('$"/api/v1/vessels/{SeedVessel.Id}/critical-states{query}"'))
        self.assertFalse(pattern.search('"/api/v1/vessels/{id}/critical-states-extra"'))
        self.assertTrue(gate.route_regex('/api/v1/vessels').search('Send(client, "vessels", body)'))
        self.assertFalse(gate.route_regex('/api/v1/vessels').search('"/api/v1/vessels/{id}/x"'))

    def test_current_repository_registry_has_no_static_critical_or_high(self):
        registry = gate.read_json(gate.ROOT / gate.REGISTRY)
        rows, findings = gate.route_matrix(registry, gate.load_tests(gate.ROOT))
        self.assertEqual(len(registry['endpoints']), len(rows))
        self.assertEqual([], [f for f in findings if f['severity'] in ('critical', 'high')])
        controls, missing = gate.control_evidence(gate.load_tests(gate.ROOT), {'state': 'NOT_PROVIDED', 'scopes': {}})
        self.assertEqual([], missing)


if __name__ == '__main__':
    unittest.main()
