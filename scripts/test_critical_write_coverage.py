"""Synthetic parser fixtures only; these are never actual audit coverage evidence."""
import copy
import hashlib
import json
import os
from pathlib import Path
import tempfile
import subprocess
import unittest
from unittest.mock import patch
import uuid

import critical_write_coverage as coverage


class CoverageFixture:
    def __init__(self):
        self.temporary = tempfile.TemporaryDirectory(prefix='poseidon-coverage-parser-test-')
        self.root = Path(self.temporary.name)
        self.contract = self.root / 'scope.json'
        self.directory = self.root / 'observations'; self.directory.mkdir()
        self.definition = {
            'schemaVersion': coverage.CONTRACT_SCHEMA, 'reasonCodes': sorted(coverage.REASONS),
            'mutationRouteTemplates': 3, 'criticalRouteTemplates': 2, 'criticalOperations': 3,
            'operations': [
                {'operationId': 'SyntheticCreate', 'method': 'POST', 'routePattern': '/api/v1/synthetic',
                 'kind': 'CriticalWrite', 'reasonCode': 'PREPARATION_COMMAND', 'family': 'Synthetic', 'scope': 'Tenant', 'variants': []},
                {'operationId': 'SyntheticAction', 'method': 'POST', 'routePattern': '/api/v1/synthetic/{action}',
                 'kind': 'CriticalWrite', 'reasonCode': 'OFFLINE_LIFECYCLE', 'family': 'Synthetic', 'scope': 'Vessel',
                 'variants': [{'routeValue': 'renew', 'operationId': 'SyntheticRenew'}, {'routeValue': 'revoke', 'operationId': 'SyntheticRevoke'}]},
                {'operationId': 'SyntheticObserve', 'method': 'POST', 'routePattern': '/api/v1/synthetic/observe',
                 'kind': 'SecurityObservation', 'reasonCode': None, 'family': 'Synthetic', 'scope': 'Tenant', 'variants': []},
            ],
        }
        self.write_contract()
        self.run = coverage.CoverageRun(self.directory, self.contract,
            hashlib.sha256(self.contract.read_bytes()).hexdigest(), uuid.uuid4().hex,
            'a' * 40, 'b' * 40, 'integration', self.root)

    def close(self):
        self.temporary.cleanup()

    def write_contract(self):
        self.contract.write_bytes((json.dumps(self.definition, indent=2) + '\n').encode())

    def report(self, operations=None):
        operations = ['SyntheticCreate', 'SyntheticRenew', 'SyntheticRevoke'] if operations is None else operations
        return {
            'schemaVersion': coverage.SCHEMA, 'runId': self.run.run_id, 'contractSha256': self.run.contract_sha256,
            'target': {'commit': self.run.commit, 'tree': self.run.tree}, 'scope': self.run.scope,
            'instanceId': uuid.uuid4().hex, 'fixture': 'DirectoryWorkspaceTests', 'testMethod': 'SyntheticCoverageCase', 'status': 'OBSERVED',
            'rowCount': len(operations), 'excludedDirectFixtureRows': 0,
            'observations': [{'operationId': name, 'observedRows': 1, 'validRows': 1,
                'successfulHttpRows': 1, 'nonSuccessHttpRows': 0, 'validSuccessfulHttpRows': 1,
                'entityTypes': ['SyntheticEntity']} for name in operations],
            'violations': [], 'unavailableCause': None,
        }

    def write(self, value):
        path = self.directory / (value['instanceId'] + '.json')
        path.write_bytes((json.dumps(value, indent=2) + '\n').encode())
        return path


class CriticalWriteCoverageTests(unittest.TestCase):
    def setUp(self):
        self.fixture = CoverageFixture(); self.addCleanup(self.fixture.close)
        # Bound only to these explicit synthetic parser fixtures. The real gate
        # always runs verify_target; its child/output behavior is tested separately.
        target = patch.object(coverage, 'verify_target'); target.start(); self.addCleanup(target.stop)

    def evaluate(self, value=None, native=True):
        self.fixture.write(self.fixture.report() if value is None else value)
        return coverage.evaluate(self.fixture.run, native)

    def assert_failure(self, report, code=None):
        self.assertNotEqual('PASS_ACTUAL_CURRENT_CRITICAL_SCOPE', report['status'])
        self.assertNotEqual(100, report.get('coverage_percent'))
        if code: self.assertIn(code, report['errors'])

    def test_only_complete_expanded_actual_observations_with_native_success_can_pass(self):
        result = self.evaluate()
        self.assertEqual('PASS_ACTUAL_CURRENT_CRITICAL_SCOPE', result['status'])
        self.assertEqual((2, 3, 3), (result['critical_route_templates'], result['required_operations'], result['observed_operations']))
        self.assertEqual(100, result['coverage_percent']); self.assertEqual([], result['errors'])
        self.assertFalse(result['declared_routes_are_execution_evidence'])

    def test_declared_catalog_without_capture_cannot_pass(self):
        self.assert_failure(coverage.evaluate(self.fixture.run, True), 'MISSING_OR_EXCESSIVE_CAPTURE_REPORTS')

    def test_zero_actual_rows_do_not_inherit_definition_coverage(self):
        result = self.evaluate(self.fixture.report([]))
        self.assert_failure(result, 'CRITICAL_OPERATIONS_UNOBSERVED')
        self.assertEqual(['SyntheticCreate', 'SyntheticRenew', 'SyntheticRevoke'], result['missing_operation_ids'])

    def test_generic_template_cannot_replace_missing_action_variant(self):
        result = self.evaluate(self.fixture.report(['SyntheticCreate', 'SyntheticRenew']))
        self.assert_failure(result, 'CRITICAL_OPERATIONS_UNOBSERVED')
        self.assertEqual(['SyntheticRevoke'], result['missing_operation_ids'])

    def test_complete_positive_rows_cannot_hide_native_failure(self):
        self.assert_failure(self.evaluate(native=False), 'NATIVE_SCOPE_NOT_PROVEN')

    def test_direct_database_fixture_contexts_cannot_close_http_gaps(self):
        report = self.fixture.report([]); report.update(rowCount=50, excludedDirectFixtureRows=50)
        result = self.evaluate(report)
        self.assert_failure(result, 'CRITICAL_OPERATIONS_UNOBSERVED')
        self.assertEqual(50, result['excluded_direct_fixture_rows']); self.assertEqual(0, result['observed_operations'])

    def test_valid_partial_fence_rows_cannot_replace_a_positive_successful_http_witness(self):
        report = self.fixture.report()
        report['observations'][0].update(successfulHttpRows=0, nonSuccessHttpRows=1, validSuccessfulHttpRows=0)
        result = self.evaluate(report)
        self.assert_failure(result, 'CRITICAL_OPERATIONS_UNOBSERVED')
        self.assertEqual(['SyntheticCreate'], result['missing_operation_ids'])
        first = next(x for x in result['observations'] if x['operation_id'] == 'SyntheticCreate')
        self.assertEqual(1, first['non_success_http_rows']); self.assertEqual(0, first['successful_http_rows'])
        self.assertEqual(1, first['valid_rows']); self.assertEqual(0, first['valid_successful_http_rows'])

    def test_valid_non_success_and_invalid_success_cannot_be_combined_into_a_valid_positive_witness(self):
        report = self.fixture.report(); report['rowCount'] += 1
        report['observations'][0].update(observedRows=2, validRows=1, successfulHttpRows=1,
                                         nonSuccessHttpRows=1, validSuccessfulHttpRows=0)
        report['violations'] = ['AUDIT_ROLE_INVALID']
        result = self.evaluate(report)
        self.assert_failure(result, 'CRITICAL_OPERATIONS_UNOBSERVED')
        self.assertIn('AUDIT_CONTEXT_OR_BEFORE_AFTER_INVALID', result['errors'])
        self.assertEqual(['SyntheticCreate'], result['missing_operation_ids'])

    def test_only_the_actual_valid_success_intersection_contributes_positive_operation_coverage(self):
        report = self.fixture.report(); report['rowCount'] += 1
        report['observations'][0].update(observedRows=2, validRows=2, successfulHttpRows=1,
                                         nonSuccessHttpRows=1, validSuccessfulHttpRows=1)
        result = self.evaluate(report)
        self.assertEqual('PASS_ACTUAL_CURRENT_CRITICAL_SCOPE', result['status'])
        self.assertEqual(3, result['observed_operations'])

    def test_separate_test_instances_cannot_supply_independent_valid_and_success_maxima(self):
        valid_failure = self.fixture.report(['SyntheticCreate'])
        valid_failure['observations'][0].update(successfulHttpRows=0, nonSuccessHttpRows=1, validSuccessfulHttpRows=0)
        self.fixture.write(valid_failure)
        invalid_success = self.fixture.report()
        invalid_success['observations'][0].update(validRows=0, validSuccessfulHttpRows=0)
        invalid_success['violations'] = ['AUDIT_ROLE_INVALID']
        result = self.evaluate(invalid_success)
        self.assert_failure(result, 'CRITICAL_OPERATIONS_UNOBSERVED')
        self.assertEqual(['SyntheticCreate'], result['missing_operation_ids'])

    def test_success_intersection_must_satisfy_actual_set_cardinality_bounds(self):
        mutations = [lambda item: item.update(validSuccessfulHttpRows=True),
                     lambda item: item.update(validSuccessfulHttpRows=-1),
                     lambda item: item.update(validSuccessfulHttpRows=2),
                     lambda item: item.update(validSuccessfulHttpRows=0),
                     lambda item: item.pop('validSuccessfulHttpRows')]
        for mutate in mutations:
            report = self.fixture.report(); mutate(report['observations'][0]); path = self.fixture.write(report)
            self.assert_failure(coverage.evaluate(self.fixture.run, True), 'MALFORMED_OPERATION_OBSERVATION'); path.unlink()

    def test_unavailable_query_has_null_counts_and_prevents_coverage_acceptance(self):
        report = self.fixture.report([])
        report.update(status='UNAVAILABLE', rowCount=None, excludedDirectFixtureRows=None,
                      violations=['AUDIT_QUERY_UNAVAILABLE'], unavailableCause='OWNED_AUDIT_QUERY_UNAVAILABLE')
        self.assert_failure(self.evaluate(report), 'AUDIT_OBSERVATION_UNAVAILABLE')

    def test_unavailable_must_not_fabricate_zero_rows(self):
        report = self.fixture.report([])
        report.update(status='UNAVAILABLE', violations=['AUDIT_QUERY_UNAVAILABLE'], unavailableCause='OWNED_AUDIT_QUERY_UNAVAILABLE')
        self.assert_failure(self.evaluate(report), 'MALFORMED_UNAVAILABLE_CAPTURE')

    def test_one_unavailable_case_cannot_be_hidden_by_other_complete_positive_observations(self):
        self.fixture.write(self.fixture.report())
        unavailable = self.fixture.report([])
        unavailable.update(status='UNAVAILABLE', rowCount=None, excludedDirectFixtureRows=None,
                           violations=['AUDIT_QUERY_UNAVAILABLE'], unavailableCause='OWNED_AUDIT_QUERY_UNAVAILABLE')
        self.assert_failure(self.evaluate(unavailable), 'AUDIT_OBSERVATION_UNAVAILABLE')

    def test_duplicate_operation_rows_are_not_multiple_witnesses(self):
        report = self.fixture.report(); report['observations'].append(copy.deepcopy(report['observations'][0])); report['rowCount'] += 1
        self.assert_failure(self.evaluate(report), 'MALFORMED_OPERATION_OBSERVATION')

    def test_wrong_instance_filename_and_duplicate_instance_content_are_rejected(self):
        report = self.fixture.report(); self.fixture.write(report)
        (self.fixture.directory / (uuid.uuid4().hex + '.json')).write_text(json.dumps(report))
        self.assert_failure(coverage.evaluate(self.fixture.run, True), 'DUPLICATE_OR_INVALID_INSTANCE')

    def test_duplicate_json_keys_fail_closed(self):
        report = self.fixture.report(); path = self.fixture.write(report)
        raw = path.read_text().replace('"rowCount": 3,', '"rowCount": 3, "rowCount": 3,')
        path.write_text(raw)
        self.assert_failure(coverage.evaluate(self.fixture.run, True), 'DUPLICATE_JSON_KEY')

    def test_stale_run_contract_target_and_wrong_scope_are_never_reused(self):
        for key, value in [('runId', '0' * 32), ('contractSha256', '0' * 64), ('scope', 'unit'), ('target', {'commit': 'c' * 40, 'tree': 'd' * 40})]:
            with self.subTest(key=key):
                report = self.fixture.report(); report[key] = value
                path = self.fixture.write(report)
                self.assert_failure(coverage.evaluate(self.fixture.run, True), 'STALE_OR_WRONG_SCOPE_CAPTURE')
                path.unlink()

    def test_contract_bytes_changed_after_binding_are_rejected(self):
        self.fixture.contract.write_bytes(self.fixture.contract.read_bytes() + b' ')
        self.assert_failure(self.evaluate(), 'CONTRACT_DIGEST_MISMATCH')

    def test_invalid_context_cannot_hide_behind_other_positive_rows_for_same_operation(self):
        report = self.fixture.report(); report['rowCount'] += 1
        report['observations'][0]['observedRows'] += 1; report['observations'][0]['successfulHttpRows'] += 1
        report['violations'] = ['AUDIT_ROLE_INVALID']
        self.assert_failure(self.evaluate(report), 'AUDIT_CONTEXT_OR_BEFORE_AFTER_INVALID')

    def test_unknown_operation_or_unbounded_diagnostic_never_exports_the_input_value(self):
        report = self.fixture.report(); report['observations'][0]['operationId'] = 'private-raw-body-or-secret'
        result = self.evaluate(report)
        self.assert_failure(result); self.assertNotIn('private-raw-body-or-secret', json.dumps(result))

    def test_unexpected_raw_fields_and_oversized_reports_are_never_accepted_or_echoed(self):
        report = self.fixture.report(); report['rawActor'] = 'private-user-context'
        result = self.evaluate(report)
        self.assert_failure(result, 'MALFORMED_CAPTURE_REPORT')
        self.assertNotIn('private-user-context', json.dumps(result))
        for path in self.fixture.directory.iterdir(): path.unlink()
        (self.fixture.directory / (uuid.uuid4().hex + '.json')).write_bytes(b'x' * (coverage.MAX_REPORT_BYTES + 1))
        self.assert_failure(coverage.evaluate(self.fixture.run, True), 'UNSAFE_OR_OVERSIZED_INPUT')

    def test_malformed_negative_boolean_counts_and_inconsistent_status_rows_are_rejected(self):
        mutations = [lambda r: r.update(rowCount=True), lambda r: r.update(excludedDirectFixtureRows=-1),
                     lambda r: r['observations'][0].update(observedRows=True),
                     lambda r: r['observations'][0].update(validRows=2),
                     lambda r: r['observations'][0].update(nonSuccessHttpRows=1)]
        for mutate in mutations:
            report = self.fixture.report(); mutate(report); path = self.fixture.write(report)
            self.assert_failure(coverage.evaluate(self.fixture.run, True)); path.unlink()

    def test_hardlinked_or_symlinked_report_is_not_regular_owned_evidence(self):
        report = self.fixture.report(); path = self.fixture.write(report)
        alias = self.fixture.root / 'linked-report'
        os.link(path, alias)
        self.assert_failure(coverage.evaluate(self.fixture.run, True), 'UNSAFE_OR_OVERSIZED_INPUT')

    def test_partial_temporary_write_does_not_count_as_a_capture(self):
        (self.fixture.directory / 'partial.tmp').write_text('{}')
        self.assert_failure(coverage.evaluate(self.fixture.run, True), 'MALFORMED_CAPTURE_REPORT')

    def test_catalog_denominator_and_duplicate_variant_ids_are_validated(self):
        for change in ('count', 'duplicate', 'noncritical'):
            document = copy.deepcopy(self.fixture.definition)
            if change == 'count': document['criticalOperations'] = 2
            elif change == 'duplicate': document['operations'][1]['variants'][1]['operationId'] = 'SyntheticRenew'
            else: document['operations'][2]['kind'] = 'Unknown'
            with self.assertRaises(coverage.CoverageInputError): coverage.denominator(document)

    def test_parent_coverage_environment_is_removed_before_a_new_native_run(self):
        result = coverage.clean_environment({'PATH': 'test-only', coverage.ENV_PREFIX + 'DIRECTORY': 'stale-private-path'})
        self.assertEqual({'PATH': 'test-only'}, result)
        child = self.fixture.run.environment(result)
        self.assertEqual(self.fixture.run.run_id, child[coverage.ENV_PREFIX + 'RUN_ID'])
        self.assertEqual(str(self.fixture.directory), child[coverage.ENV_PREFIX + 'DIRECTORY'])

    def test_source_hints_emit_only_static_methods_actually_present_in_source(self):
        directory = self.fixture.root / 'tests/Poseidon.Api.Tests'; directory.mkdir(parents=True)
        (directory / 'SyntheticCoverageTests.cs').write_text(
            'private raw diagnostic value\npublic async Task Actual_fixture()\n', encoding='utf-8')
        hints = coverage.source_hints(self.fixture.root, {'Actual_fixture', 'UnknownPrivateValue', 'private-raw-parameter', None})
        self.assertEqual([{'file': 'SyntheticCoverageTests.cs', 'line': 2, 'method': 'Actual_fixture'}], hints)
        self.assertNotIn('raw diagnostic', json.dumps(hints)); self.assertNotIn('UnknownPrivateValue', json.dumps(hints))

    def test_replay_requires_closed_exact_native_counts_and_same_coverage_binding(self):
        native = {
            'scope': 'integration', 'status': 'PASS_EXACT_DISCOVERED_SCOPE', 'raw_output_persisted': False,
            'discovery': {'exit_code': 0, 'cases': 2}, 'exit_code': 0,
            'count_observations': {'total': [2], 'failed': [0], 'succeeded': [2], 'skipped': [0]},
            'critical_write_coverage': {'run_id': self.fixture.run.run_id, 'scope': 'integration',
                'contract_sha256': self.fixture.run.contract_sha256,
                'target': {'commit': self.fixture.run.commit, 'tree': self.fixture.run.tree}},
        }
        self.assertTrue(coverage.native_scope_complete(native, self.fixture.run))
        for change in ('skipped', 'duplicate', 'boolean-exit', 'wrong-scope', 'wrong-target', 'unproven'):
            changed = copy.deepcopy(native)
            if change == 'skipped': changed['count_observations']['skipped'] = [1]
            elif change == 'duplicate': changed['count_observations']['total'] = [2, 2]
            elif change == 'boolean-exit': changed['exit_code'] = False
            elif change == 'wrong-scope': changed['scope'] = 'unit'
            elif change == 'wrong-target': changed['critical_write_coverage']['target']['commit'] = 'c' * 40
            else: changed['status'] = 'INTERRUPTED_DEADLINE'
            self.assertFalse(coverage.native_scope_complete(changed, self.fixture.run), change)


class CriticalWriteTargetBindingTests(unittest.TestCase):
    def test_actual_verifier_requires_same_clean_head_and_tree(self):
        fixture = CoverageFixture(); self.addCleanup(fixture.close)
        for outputs in ((b'a' * 40, b'b' * 40, b' M changed'),
                        (b'c' * 40,), (b'a' * 40, b'c' * 40)):
            results = [subprocess.CompletedProcess([], 0, item) for item in outputs]
            with patch.object(coverage.subprocess, 'run', side_effect=results), self.assertRaises(coverage.CoverageInputError):
                coverage.verify_target(fixture.run)
        with patch.object(coverage.subprocess, 'run', side_effect=[subprocess.CompletedProcess([], 0, item)
                for item in (b'a' * 40, b'b' * 40, b'')]):
            coverage.verify_target(fixture.run)

    def test_git_failure_does_not_export_diagnostics(self):
        fixture = CoverageFixture(); self.addCleanup(fixture.close)
        with patch.object(coverage.subprocess, 'run', return_value=subprocess.CompletedProcess([], 1, b'private-source', b'private-diagnostic')):
            with self.assertRaisesRegex(coverage.CoverageInputError, '^TARGET_BINDING_UNAVAILABLE$'):
                coverage.verify_target(fixture.run)
