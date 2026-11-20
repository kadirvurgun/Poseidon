"""DEV-068 / T-S10-005: pre-pilot security gate (critical/high = 0 for pilot scope).

Source: 18.01 Backlog DEV-068 "RBAC/tenant/file upload/privilege escalation/secret scans pass;
unresolved critical/high=0 for pilot scope." This gate aggregates existing evidence; it does not
grant pilot GO. Pilot opening stays a human product owner / security owner decision.

Checks
  secret-scan-repository  scripts/reviewed_secret_scan.py --mode repository (required)
  secret-scan-client      scripts/reviewed_secret_scan.py --mode client on frontend/dist (required)
  route-matrix            static RBAC/tenant consistency of contracts/endpoint-profiles.v1.json:
                          every route gets its unauthorized-role / foreign-tenant expectation and
                          the existing API tests that reference it (required, static)
  control-evidence        privilege escalation / upload / RBAC / tenant controls mapped to exact
                          API test methods; proven only by a PASS api_test_gate evidence bound
                          to the current commit (required)
  clamd-readiness         scripts/clamd_gate.py start/stop, only with --with-clamd (optional; Docker)
  release-gate            scripts/s0_release_gate.py, only with --release-candidate (optional)

Severity: secret findings and anonymous/unprotected business routes are CRITICAL; a mutating
route without CSRF/S1 classification, an unknown profile or a missing mapped test is HIGH;
an API route no test references is MEDIUM (coverage gap, listed). No secret exception is
created here: a needed exception follows docs/security/source-review-v2.md (human review).

Exit codes: 1 when critical+high > 0; 2 when 0 critical/high but a required check is NOT_RUN or
not proven (INCOMPLETE, never a pass); 0 only when every required check ran and passed.
"""
import argparse
from collections import Counter
from datetime import datetime, timezone
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT = Path(__file__).resolve().parents[1]
SCHEMA = 'poseidon.pre-pilot-security-gate.v1'
REGISTRY = 'contracts/endpoint-profiles.v1.json'
S1_SCOPE = 'contracts/security/s1-current-scope.v1.json'
API_TESTS = 'tests/Poseidon.Api.Tests'
MUTATING = {'POST', 'PUT', 'PATCH', 'DELETE'}
# Anonymous by design (58.03 paragraph:109 session bootstrap, liveness/status). Anything else
# anonymous is a CRITICAL finding.
PUBLIC_ROUTES = {('GET', '/api/v1/auth/login'), ('GET', '/api/v1/auth/session'),
                 ('GET', '/api/v1/system/status'), ('GET', '/health/live')}
PUBLIC_PROFILES = {'NATIVE-PUBLIC-1', 'NATIVE-SESSION-1'}
MEANING = ('Aggregated pre-pilot security evidence. PASS means 0 unresolved critical/high for the checked '
           'scope with every required check run; it is not a pilot GO, penetration test or human sign-off.')

# Mutation sweeps in the actual host that cover every S1 mutation route (anonymous -> 401,
# forged IdP claims -> 403, valid read scope without a server role -> 403).
MUTATION_SWEEPS = [
    'S1ScopeCoverageTests.All_current_mutation_routes_deny_anonymous_or_forged_IdP_privileges_without_business_effects',
    'S1ScopeCoverageTests.Every_current_critical_write_requires_a_real_server_role_even_when_read_scope_is_valid',
    'S1ScopeCoverageTests.Current_mutation_routes_are_exhaustively_classified_and_authorized_in_the_actual_host',
    'EndpointProfileRegistryTests.Registered_command_and_read_profiles_are_enforced_by_the_actual_host',
]

# Control -> exact existing test methods (Class.Method). Missing method => HIGH.
CONTROLS = [
    {'id': 'SEC-RBAC-01', 'area': 'rbac', 'control': 'Every mutation route denies anonymous / forged privileges and requires a real server role',
     'tests': MUTATION_SWEEPS[:3]},
    {'id': 'SEC-RBAC-02', 'area': 'rbac', 'control': 'Every API permission checked by the host survives the trusted allowlist (no client-invented permission)',
     'tests': ['PermissionAllowlistGuardTests.Every_permission_checked_by_the_api_survives_the_trusted_allowlist']},
    {'id': 'SEC-RBAC-03', 'area': 'rbac', 'control': 'Human writes require a trusted role and record complete context',
     'tests': ['TrustedWriteRoleTests.Every_current_human_write_requires_trusted_role_and_records_complete_available_context']},
    {'id': 'SEC-TENANT-01', 'area': 'tenant', 'control': 'Foreign tenant / vessel identifiers are not disclosed (404) and spoofed tenant queries are rejected',
     'tests': ['DirectoryWorkspaceTests.Today_rejects_spoofed_queries_and_never_leaks_out_of_scope_why',
               'MembershipLifecycleTests.Membership_administration_is_tenant_scoped_and_permission_gated',
               'DocumentDraftTests.Document_count_detail_revision_and_replay_deny_wrong_vessel_tenant_and_revoked_roles',
               'SecureFilesTests.Stage_xmin_fences_reconciliation_and_composite_owner_FK_denies_cross_tenant_relations']},
    {'id': 'SEC-PRIV-01', 'area': 'privilege_escalation', 'control': 'Grants/imports never widen an operator grant; delegation is bounded and expires',
     'tests': ['MembershipLifecycleTests.Import_is_deterministic_and_never_widens_the_operator_grant',
               'MembershipLifecycleTests.Delegation_is_bounded_attributed_and_expires_by_server_time',
               'MembershipLifecycleTests.Security_suspension_can_only_be_resumed_by_the_security_authority_and_closes_recovery']},
    {'id': 'SEC-PRIV-02', 'area': 'privilege_escalation', 'control': 'Non-human actors and client-supplied actor kind/role/signature cannot obtain human authority',
     'tests': ['CrewCatalogTests.Catalog_grants_never_let_nonhumans_publish',
               'HumanDecisionRecordingTests.Client_cannot_supply_actor_kind_role_signature_policy_or_decision_time',
               'CertificateAssertionTests.Assertion_requires_exact_assigned_Human_and_never_uses_admin_or_client_role_fallback',
               'CrewRosterTests.Crew_directory_write_and_spoofed_roles_do_not_grant_crew_access_or_counts']},
    {'id': 'SEC-UPLOAD-01', 'area': 'upload', 'control': 'Real clamd rejects the standard EICAR test signature',
     'tests': ['ClamdIntegrationTests.Operator_supplied_real_clamd_accepts_passive_bytes_and_rejects_standard_test_signature']},
    {'id': 'SEC-UPLOAD-02', 'area': 'upload', 'control': 'MIME/magic, checksum, size overflow, truncation, malware and scanner-unavailable bytes are rejected',
     'tests': ['SecureFilesRejectionTests.Rejected_actual_bytes_preserve_exact_pending_intent_and_all_previously_accepted_content',
               'SecureFilesRejectionTests.Metadata_or_current_operator_policy_rejection_does_not_claim_or_freeze_pending_intent',
               'SecureFilesTests.Checksum_mismatch_and_disguised_magic_do_not_reach_scanner_or_finalize',
               'SecureFilesTests.Chunked_actual_overflow_and_matching_hash_disguised_content_are_quarantined_before_scanner',
               'SecureFilesTests.Malformed_metadata_and_forged_protected_owner_or_clean_claims_are_rejected',
               'SecureFilesTests.Files_are_default_deny_and_unconfigured_service_cannot_accept_metadata_or_bytes',
               'ImportFoundationTests.Import_malformed_or_oversized_bytes_are_bounded_and_do_not_echo_private_inputs']},
]

DENIAL = re.compile(r'HttpStatusCode\.(?:Forbidden|NotFound|Unauthorized)|\b(?:403|404|401)\b')
METHOD = re.compile(r'\b(?:Task|void)\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(')
CLASS = re.compile(r'\bclass\s+([A-Za-z_][A-Za-z0-9_]*)')
INTEGRATION = re.compile(r'\[\s*Trait\s*\(\s*"Category"\s*,\s*"Integration"\s*\)')
PERFORMANCE = re.compile(r'Trait\s*\(\s*"Category"\s*,\s*"Performance"\s*\)')


def finding(severity, rule, subject, detail):
    return {'severity': severity, 'rule': rule, 'subject': subject, 'detail': detail}


def read_json(path):
    return json.loads(Path(path).read_text(encoding='utf-8'))


# ---------------------------------------------------------------- route matrix (static)

def route_regex(path):
    """Match a route template inside C# test string literals (absolute or /api/v1-relative)."""
    rel = path[len('/api/v1/'):] if path.startswith('/api/v1/') else path.lstrip('/')
    parts = []
    for segment in rel.split('/'):
        if segment.startswith('{') and segment.endswith('}'):
            parts.append(r'[^/"?\s]+')
        else:
            parts.append(re.escape(segment))
    body = '/'.join(parts)
    prefix = r'(?:/api/v1/|")' if path.startswith('/api/v1/') else r'(?:/|")'
    return re.compile(prefix + body + r'(?=["?{]|$)', re.IGNORECASE | re.MULTILINE)


def route_fragments(path):
    """Static prefix before the first parameter plus later static segments, for paths that
    tests build from helpers (prefix literal + interpolated ids + suffix literal)."""
    if '{' not in path:
        return None
    head, _, tail = path.partition('{')
    prefix = head.rstrip('/')
    rel = prefix[len('/api/v1/'):] if prefix.startswith('/api/v1/') else prefix.lstrip('/')
    if not rel:
        return None
    suffix = [seg for seg in tail.split('/')[1:] if seg and not seg.startswith('{')]
    patterns = [re.compile(r'(?:/api/v1/|")' + re.escape(rel) + r'(?=["/?{]|$)', re.IGNORECASE | re.MULTILINE)]
    patterns += [re.compile(r'["/]' + re.escape(seg) + r'(?=["/?{]|$)', re.IGNORECASE | re.MULTILINE) for seg in suffix]
    return patterns


def load_tests(root):
    files = {}
    for path in sorted((root / API_TESTS).rglob('*.cs')):
        if {'bin', 'obj'} & set(path.parts):
            continue
        files[path.relative_to(root).as_posix()] = path.read_text(encoding='utf-8', errors='replace')
    return files


def route_matrix(registry, tests):
    profiles = {p['profileId']: p for p in registry['profiles']}
    rows, findings = [], []
    denial_files = {name for name, text in tests.items() if DENIAL.search(text) and not PERFORMANCE.search(text)}
    for entry in registry['endpoints']:
        method, path = entry['method'], entry['normalizedPath']
        subject = f'{method} {path}'
        profile = profiles.get(entry['profileId'])
        anonymous = bool(entry.get('anonymous'))
        public = (method, path) in PUBLIC_ROUTES
        if profile is None:
            findings.append(finding('high', 'UNKNOWN_OR_UNASSIGNED_PROFILE', subject, entry['profileId']))
        if anonymous and not public:
            findings.append(finding('critical', 'UNEXPECTED_ANONYMOUS_ROUTE', subject, 'anonymous route outside the reviewed public set'))
        if public and (not anonymous or entry['profileId'] not in PUBLIC_PROFILES):
            findings.append(finding('high', 'PUBLIC_ROUTE_PROFILE_MISMATCH', subject, entry['profileId']))
        if not anonymous and not entry.get('authorizationPolicies'):
            findings.append(finding('critical', 'AUTHENTICATED_ROUTE_WITHOUT_POLICY', subject, 'no authorization policy'))
        if method in MUTATING:
            if anonymous:
                findings.append(finding('critical', 'ANONYMOUS_MUTATION', subject, 'mutation without authentication'))
            if profile is not None and profile.get('csrf') != 'REQUIRED':
                findings.append(finding('high', 'MUTATION_WITHOUT_CSRF_PROFILE', subject, entry['profileId']))
            if 'CSRF_NOT_ENFORCED_BEFORE_EFFECT' in entry.get('deviations', []):
                findings.append(finding('high', 'CSRF_DEVIATION', subject, 'CSRF_NOT_ENFORCED_BEFORE_EFFECT'))
            if not entry.get('securityOperationId'):
                findings.append(finding('high', 'MUTATION_WITHOUT_S1_OPERATION', subject, 'no S1 security operation'))
        elif profile is not None and profile.get('kind') == 'COMMAND':
            findings.append(finding('high', 'READ_METHOD_WITH_COMMAND_PROFILE', subject, entry['profileId']))
        pattern = route_regex(path)
        referenced = sorted(name for name, text in tests.items() if not PERFORMANCE.search(text) and pattern.search(text))
        fragments = route_fragments(path)
        assembled = [] if referenced or fragments is None else sorted(
            name for name, text in tests.items() if not PERFORMANCE.search(text) and all(p.search(text) for p in fragments))
        with_denial = [name for name in referenced + assembled if name in denial_files]
        if public:
            expectation = {'anonymous': 'ALLOWED_PUBLIC', 'unauthorizedRole': 'NOT_APPLICABLE', 'foreignTenant': 'NOT_APPLICABLE'}
        else:
            expectation = {'anonymous': '401', 'unauthorizedRole': '403',
                           'foreignTenant': '404_OR_403_WITHOUT_DISCLOSURE'}
        if public:
            coverage = 'PUBLIC'
        elif method in MUTATING:
            coverage = 'MUTATION_SWEEP' + ('+ROUTE_TESTS' if with_denial else '')
        elif with_denial:
            coverage = 'ROUTE_TESTS_WITH_DENIAL_ASSERTIONS' if referenced else 'ROUTE_FRAGMENTS_WITH_DENIAL_ASSERTIONS'
        elif referenced or assembled:
            coverage = 'ROUTE_REFERENCED_NO_DENIAL_ASSERTION'
        else:
            coverage = 'NO_STATIC_TEST_REFERENCE'
        if coverage in ('ROUTE_REFERENCED_NO_DENIAL_ASSERTION', 'NO_STATIC_TEST_REFERENCE'):
            findings.append(finding('medium', 'READ_ROUTE_DENIAL_NOT_STATICALLY_COVERED', subject, coverage))
        rows.append({'method': method, 'path': path, 'profileId': entry['profileId'], 'owner': entry.get('owner'),
                     'expectation': expectation, 'coverage': coverage,
                     'testFiles': [Path(n).name for n in (with_denial or referenced or assembled)][:8]})
    return rows, findings


def s1_consistency(registry, scope):
    """Mutation routes in the endpoint registry must equal the S1 scope's declared count."""
    mutating = {(e['method'], e['normalizedPath']) for e in registry['endpoints'] if e['method'] in MUTATING}
    declared = scope.get('mutationRouteTemplates')
    if isinstance(declared, int) and declared != len(mutating):
        return [finding('high', 'S1_SCOPE_COUNT_MISMATCH', S1_SCOPE, f'registry mutations={len(mutating)} s1 mutationRouteTemplates={declared}')]
    return []


# ------------------------------------------------------------- control evidence mapping

def test_index(tests):
    """Class.Method -> (file, scope). Class-level Integration trait or the integration partial class."""
    index = {}
    for name, text in tests.items():
        if PERFORMANCE.search(text) and 'Performance' in Path(name).parts:
            continue
        classes = CLASS.findall(text)
        integration = bool(INTEGRATION.search(text)) or 'DirectoryWorkspaceTests' in classes
        # Most API test files are partials of DirectoryWorkspaceTests: the file stem names the test.
        for owner in [Path(name).stem, *classes]:
            for method in METHOD.findall(text):
                index.setdefault(f'{owner}.{method}', (name, 'integration' if integration else 'unit'))
    return index


def api_evidence(directory, expected_commit, head):
    """Accept api_test_gate evidence only when it PASSED and is bound to the checked commit."""
    result = {'state': 'NOT_PROVIDED', 'scopes': {}}
    if directory is None:
        return result
    if not expected_commit or expected_commit != head:
        result['state'] = 'UNBOUND_OR_STALE_COMMIT'
        return result
    for scope in ('unit', 'integration'):
        path = Path(directory) / f'{scope}.json'
        if not path.is_file():
            result['scopes'][scope] = 'MISSING'
            continue
        raw = path.read_bytes()
        try:
            data = json.loads(raw)
        except ValueError:
            result['scopes'][scope] = 'INVALID'
            continue
        result['scopes'][scope] = ('PASS' if data.get('status') == 'PASS_EXACT_DISCOVERED_SCOPE' and data.get('scope') == scope
                                   and data.get('exit_code') == 0 else 'FAILED_OR_UNPROVEN')
        result.setdefault('sha256', {})[scope] = hashlib.sha256(raw).hexdigest()
    result['state'] = 'BOUND'
    return result


def control_evidence(tests, evidence):
    index = test_index(tests)
    rows, findings = [], []
    for control in CONTROLS:
        mapped = []
        for test in control['tests']:
            located = index.get(test)
            if located is None:
                findings.append(finding('high', 'MAPPED_CONTROL_TEST_MISSING', control['id'], test))
                mapped.append({'test': test, 'state': 'MISSING'})
                continue
            file, scope = located
            proof = evidence['scopes'].get(scope) if evidence['state'] == 'BOUND' else None
            mapped.append({'test': test, 'file': Path(file).name, 'scope': scope,
                           'state': 'PASSED_IN_BOUND_GATE' if proof == 'PASS' else 'NOT_PROVEN_THIS_RUN'})
        states = {m['state'] for m in mapped}
        state = 'MISSING_TEST' if 'MISSING' in states else 'PROVEN' if states == {'PASSED_IN_BOUND_GATE'} else 'NOT_PROVEN'
        rows.append({**{k: control[k] for k in ('id', 'area', 'control')}, 'state': state, 'tests': mapped})
    for scope, state in evidence['scopes'].items():
        if state == 'FAILED_OR_UNPROVEN':
            findings.append(finding('high', 'API_GATE_EVIDENCE_NOT_PASSING', scope, 'bound api_test_gate evidence is not PASS'))
    return rows, findings


# ------------------------------------------------------------------- external scripts

def last_json(output):
    for line in reversed(output.decode('utf-8', errors='replace').splitlines()):
        try:
            value = json.loads(line)
        except ValueError:
            continue
        if isinstance(value, dict):
            return value
    return {}


def secret_scan(root, mode, runner, bundle='frontend/dist'):
    name = f'secret-scan-{mode}'
    if mode == 'client' and not (root / bundle / 'index.html').is_file():
        return {'check': name, 'state': 'NOT_RUN', 'reason': 'frontend_production_output_absent'}, []
    command = [sys.executable, str(root / 'scripts/reviewed_secret_scan.py'), '--root', str(root), '--mode', mode]
    if mode == 'client':
        command += ['--bundle', bundle]
    done = runner(command, cwd=root, capture_output=True, timeout=1800)
    summary = last_json(done.stdout or b'')
    if done.returncode == 0:
        return {'check': name, 'state': 'PASS', 'coveredFiles': summary.get('covered_files')}, []
    if done.returncode == 1:
        items = []
        for line in (done.stdout or b'').decode('utf-8', errors='replace').splitlines():
            try:
                item = json.loads(line)
            except ValueError:
                continue
            if isinstance(item, dict) and {'path', 'rule', 'line'} <= set(item):
                items.append(finding('critical', 'SECRET_SCAN_FINDING', f"{item['path']}:{item['line']}", item['rule']))
        if not items:
            items.append(finding('critical', 'SECRET_SCAN_FINDING', mode, 'scan reported findings'))
        return {'check': name, 'state': 'FINDINGS', 'findings': len(items)}, items
    # Exit 2: trust/document binding or coverage input missing. Never invent an exception or pin.
    return {'check': name, 'state': 'NOT_RUN', 'reason': summary.get('rule', 'scan_input_or_trust_unavailable')}, []


def clamd_readiness(root, runner, enabled):
    if not enabled:
        return {'check': 'clamd-readiness', 'state': 'NOT_RUN', 'reason': 'not_requested_docker_policy', 'required': False}
    state = root / '.runtime/security-gate/clamd-state.json'
    started = runner([sys.executable, str(root / 'scripts/clamd_gate.py'), 'start', '--state', str(state)],
                     cwd=root, capture_output=True, timeout=600)
    ready = started.returncode == 0
    if state.exists():
        runner([sys.executable, str(root / 'scripts/clamd_gate.py'), 'stop', '--state', str(state)],
               cwd=root, capture_output=True, timeout=120)
    return {'check': 'clamd-readiness', 'state': 'PASS' if ready else 'FAILED', 'required': False}


def release_gate(root, runner, candidate):
    if candidate is None:
        return {'check': 'release-gate', 'state': 'NOT_RUN', 'reason': 'no_release_candidate', 'required': False}
    done = runner([sys.executable, str(root / 'scripts/s0_release_gate.py'), str(candidate)], cwd=root,
                  capture_output=True, timeout=600)
    return {'check': 'release-gate', 'state': 'ACCEPTED' if done.returncode == 0 else 'NOT_ACCEPTED', 'required': False}


# ------------------------------------------------------------------------------ gate

def head_commit(root, runner):
    try:
        done = runner(['git', '-C', str(root), 'rev-parse', 'HEAD'], capture_output=True, timeout=30)
        return done.stdout.decode().strip() if done.returncode == 0 else None
    except (OSError, subprocess.SubprocessError):
        return None


def evaluate(root=ROOT, runner=subprocess.run, api_evidence_dir=None, api_evidence_commit=None,
             with_clamd=False, release_candidate=None, skip_scans=False):
    root = Path(root)
    head = head_commit(root, runner)
    registry = read_json(root / REGISTRY)
    tests = load_tests(root)
    findings, checks = [], []
    if skip_scans:
        checks += [{'check': 'secret-scan-repository', 'state': 'NOT_RUN', 'reason': 'operator_skipped'},
                   {'check': 'secret-scan-client', 'state': 'NOT_RUN', 'reason': 'operator_skipped'}]
    else:
        for mode in ('repository', 'client'):
            check, items = secret_scan(root, mode, runner)
            checks.append(check); findings += items
    rows, items = route_matrix(registry, tests)
    findings += items
    scope_path = root / S1_SCOPE
    if scope_path.is_file():
        findings += s1_consistency(registry, read_json(scope_path))
    blocking_static = [f for f in items if f['severity'] in ('critical', 'high')]
    checks.append({'check': 'route-matrix', 'state': 'FINDINGS' if blocking_static else 'PASS', 'routes': len(rows),
                   'coverage': dict(Counter(r['coverage'] for r in rows))})
    evidence = api_evidence(api_evidence_dir, api_evidence_commit, head)
    controls, items = control_evidence(tests, evidence)
    findings += items
    proven = all(c['state'] == 'PROVEN' for c in controls)
    checks.append({'check': 'control-evidence', 'state': 'PASS' if proven else 'NOT_PROVEN' if not items else 'FINDINGS',
                   'evidence': evidence['state'], 'controls': len(controls)})
    checks.append(clamd_readiness(root, runner, with_clamd))
    checks.append(release_gate(root, runner, release_candidate))
    counts = Counter(f['severity'] for f in findings)
    blocking = counts['critical'] + counts['high']
    incomplete = [c['check'] for c in checks if c.get('required', True) and c['state'] not in ('PASS',)]
    status = 'FAIL' if blocking else 'INCOMPLETE' if incomplete else 'PASS'
    report = {'schemaVersion': SCHEMA, 'requirement': 'DEV-068', 'testTrace': 'T-S10-005', 'commit': head,
              'generatedAt': datetime.now(timezone.utc).isoformat(), 'meaning': MEANING, 'status': status,
              'counts': {k: counts[k] for k in ('critical', 'high', 'medium')}, 'incompleteRequiredChecks': incomplete,
              'checks': checks, 'controls': controls, 'mutationSweeps': MUTATION_SWEEPS,
              'findings': sorted(findings, key=lambda f: (['critical', 'high', 'medium'].index(f['severity']), f['rule'], f['subject'])),
              'routeMatrix': rows,
              'humanGates': ['Pilot GO decision (product owner)', 'Security owner review of medium coverage gaps',
                             'Any secret exception via docs/security/source-review-v2.md']}
    return (1 if blocking else 2 if incomplete else 0), report


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument('--api-evidence-dir', type=Path, help='directory with api_test_gate unit.json/integration.json')
    parser.add_argument('--api-evidence-commit', help='commit SHA the evidence was produced for (must equal HEAD)')
    parser.add_argument('--with-clamd', action='store_true', help='start/stop the owned loopback clamd (uses Docker)')
    parser.add_argument('--release-candidate', type=Path, help='S0 release gate candidate JSON')
    parser.add_argument('--skip-scans', action='store_true', help='record secret scans as NOT_RUN')
    parser.add_argument('--output', type=Path, default=ROOT / '.runtime/security-gate/pre-pilot.json')
    args = parser.parse_args(argv)
    try:
        code, report = evaluate(ROOT, api_evidence_dir=args.api_evidence_dir, api_evidence_commit=args.api_evidence_commit,
                                with_clamd=args.with_clamd, release_candidate=args.release_candidate, skip_scans=args.skip_scans)
    except (OSError, ValueError, KeyError, TypeError, subprocess.SubprocessError):
        print(json.dumps({'schemaVersion': SCHEMA, 'status': 'GATE_INPUT_FAILURE'}))
        return 2
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(report, indent=2, ensure_ascii=False) + '\n', encoding='utf-8')
    print(json.dumps({'status': report['status'], 'counts': report['counts'], 'incomplete': report['incompleteRequiredChecks'],
                      'blocking': [f"{f['severity']}:{f['rule']}:{f['subject']}" for f in report['findings'] if f['severity'] != 'medium'][:50],
                      'report': str(args.output)}, ensure_ascii=False))
    return code


if __name__ == '__main__':
    raise SystemExit(main())
