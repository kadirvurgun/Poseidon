"""DEV-071 / T-S11-003: DR backup/restore/rehearsal logic with a fake process runner.

No Docker, PostgreSQL or network: every external command is answered by FakeRunner. This
proves manifest/hash/guard/measurement logic only; a real restore rehearsal remains DEFERRED.
"""
from datetime import datetime, timedelta, timezone
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

import dr_backup
from dr_backup import COUNT_TABLES, DrError, backup, libpq_environment
from dr_restore import restore
from dr_rehearsal import Container, rehearse

TENANT, FILE = '1' * 32, '2' * 32
SECRET = 'synthetic-password-never-logged'
LAST_AUDIT = '2026-11-20T09:59:00.000000Z'


class FakeRunner:
    def __init__(self, counts=None, migration='20261124062000_S11WpbPilotMetrics', last_audit=LAST_AUDIT, checksum=None, fail=None):
        self.calls, self.counts, self.migration, self.last_audit, self.checksum, self.fail = [], counts or {t: 3 for t in COUNT_TABLES}, migration, last_audit, checksum, fail

    def __call__(self, command, env=None, capture_output=True, text=True, timeout=None, check=False):
        self.calls.append((list(command), dict(env or {})))
        tool, out = Path(command[0]).name, ''
        if self.fail == tool:
            return subprocess.CompletedProcess(command, 1, '', 'synthetic failure ' + SECRET)
        if tool == 'pg_dump':
            Path(command[command.index('--file') + 1]).write_bytes(b'PGDMP synthetic dump')
        elif tool == 'psql':
            sql = command[command.index('--command') + 1]
            if sql.startswith('SELECT count(*) FROM '): out = str(self.counts[sql.removeprefix('SELECT count(*) FROM ')])
            elif 'MigrationId' in sql: out = self.migration
            elif 'OccurredAt' in sql: out = self.last_audit
            elif sql == 'SELECT 1': out = '1'
            elif 'StoredFiles' in sql: out = self.checksum or ''
            else: raise AssertionError('unexpected query')
        elif tool == 'docker' and command[1] == 'run': out = 'abc123def456\n'
        elif tool == 'docker' and command[1] == 'port': out = '127.0.0.1:55432\n'
        return subprocess.CompletedProcess(command, 0, out + '\n', '')


class Clock:
    def __init__(self, start): self.now = start
    def __call__(self):
        value = self.now; self.now += timedelta(seconds=30); return value


class DrScriptTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(); self.addCleanup(self.temp.cleanup)
        self.root = Path(self.temp.name)
        self.objects = self.root / 'objects-snapshot'
        (self.objects / 'poseidon' / TENANT).mkdir(parents=True)
        self.payload = b'FICTIONAL evidence bytes'
        (self.objects / 'poseidon' / TENANT / FILE).write_bytes(self.payload)
        self.env = {'PATH': '/usr/bin', 'PGHOST': 'db.invalid', 'PGUSER': 'poseidon', 'PGPASSWORD': SECRET, 'PGDATABASE': 'poseidon', 'UNRELATED_TOKEN': 'x'}

    def make_backup(self, runner=None):
        runner = runner or FakeRunner()
        result = backup(self.root / 'dr', self.objects, env=self.env, runner=runner, clock=Clock(datetime(2026, 11, 20, 10, 0, tzinfo=timezone.utc)))
        return self.root / 'dr' / result['directory'], result, runner

    def test_backup_writes_hashed_manifest_without_secrets_or_ambient_variables(self):
        directory, result, runner = self.make_backup()
        manifest = json.loads((directory / 'manifest.json').read_text(encoding='utf-8'))
        self.assertEqual(('BACKUP_WRITTEN', 1), (result['status'], result['objectCount']))
        self.assertEqual(hashlib.sha256(b'PGDMP synthetic dump').hexdigest(), manifest['database']['sha256'])
        self.assertEqual({'key': f'poseidon/{TENANT}/{FILE}', 'size': len(self.payload), 'sha256': hashlib.sha256(self.payload).hexdigest()}, manifest['objects']['items'][0])
        self.assertEqual((LAST_AUDIT, False, 'NOT_PROVEN_BY_BACKUP'), (manifest['database']['lastAuditAt'], manifest['secretsExported'], manifest['restoreProof']))
        self.assertEqual(set(COUNT_TABLES), set(manifest['database']['tableCounts']))
        text = (directory / 'manifest.json').read_text(encoding='utf-8') + json.dumps(result)
        self.assertNotIn(SECRET, text); self.assertNotIn('db.invalid', text)
        for command, env in runner.calls:
            self.assertNotIn(SECRET, ' '.join(command))  # never on a command line
            self.assertNotIn('UNRELATED_TOKEN', env)
        dump = next(c for c, _ in runner.calls if Path(c[0]).name == 'pg_dump')
        self.assertIn('--format=custom', dump)

    def test_failures_report_fixed_codes_only(self):
        with self.assertRaises(DrError) as caught:
            self.make_backup(FakeRunner(fail='pg_dump'))
        self.assertEqual('pg_dump_failed', str(caught.exception)); self.assertNotIn(SECRET, str(caught.exception))
        with self.assertRaises(DrError) as missing:
            libpq_environment({'PATH': '/usr/bin'})
        self.assertEqual('database_not_configured', str(missing.exception))
        (self.objects / 'poseidon' / TENANT / 'unexpected.txt').write_bytes(b'x')
        with self.assertRaises(DrError) as key:
            backup(self.root / 'dr-second', self.objects, env=self.env, runner=FakeRunner())
        self.assertEqual('object_key_unexpected', str(key.exception))

    def test_restore_refuses_unconfirmed_or_non_isolated_targets_and_tampered_backups(self):
        directory, _, _ = self.make_backup()
        target = {**self.env, 'PGDATABASE': 'poseidon_restore_check'}
        for confirm, env, code in (('other', target, 'target_not_confirmed'), ('poseidon', self.env, 'target_not_isolated')):
            with self.assertRaises(DrError) as caught:
                restore(directory, confirm, env=env, runner=FakeRunner())
            self.assertEqual(code, str(caught.exception))
        result = restore(directory, 'poseidon_restore_check', self.root / 'restored', env=target, runner=FakeRunner())
        self.assertEqual(('RESTORED_UNVALIDATED', 1), (result['status'], result['objectsRestored']))
        self.assertEqual(self.payload, (self.root / 'restored' / 'poseidon' / TENANT / FILE).read_bytes())
        (directory / 'objects' / 'poseidon' / TENANT / FILE).write_bytes(b'tampered')
        runner = FakeRunner()
        with self.assertRaises(DrError) as tampered:
            restore(directory, 'poseidon_restore_check', env=target, runner=runner)
        self.assertEqual('object_hash_mismatch', str(tampered.exception))
        self.assertFalse(any(Path(c[0]).name == 'pg_restore' for c, _ in runner.calls))  # verified before any write

    def test_rehearsal_measures_rpo_rto_and_stays_unsigned(self):
        directory, _, _ = self.make_backup()
        runner = FakeRunner(checksum=hashlib.sha256(self.payload).hexdigest().upper())
        container = Container(runner, sleep=lambda _: None)
        clock = Clock(datetime(2026, 11, 20, 10, 10, tzinfo=timezone.utc))
        result = rehearse(directory, self.root / 'reports', runner=runner, clock=clock, container=container)
        report = json.loads((self.root / 'reports' / result['report']).read_text(encoding='utf-8'))
        self.assertEqual('MEASURED_PARTIAL_UNSIGNED', report['status'])
        self.assertEqual(660.0, report['measured']['rpoSeconds'])  # 10:10:00 incident - 09:59:00 newest restored audit
        self.assertEqual(30.0, report['measured']['rtoSeconds'])
        results = {c['check']: c['result'] for c in report['checks']}
        self.assertEqual({'DATABASE_OPENS': 'PASS', 'MIGRATION_VERSION': 'PASS', 'ROW_COUNTS': 'PASS', 'AUDIT_QUERYABLE': 'PASS', 'EVIDENCE_SAMPLE': 'PASS',
                          'TENANT_ISOLATION': 'NOT_RUN', 'OFFLINE_SYNC': 'NOT_RUN'}, results)
        self.assertEqual({'state': 'OWNER_SIGNATURE_PENDING', 'signedBy': None, 'signedAt': None, 'runbook': 'docs/operations/dr-runbook.md'}, report['rollbackProcedureSignOff'])
        self.assertEqual(('NOT_ACCEPTED', 'PRODUCTION_COUNCIL_APPROVAL_REQUIRED'), (report['acceptance'], report['proposedTargets']['approval']))
        self.assertNotIn(container.pg_secret, json.dumps(report))
        run = next(c for c, _ in runner.calls if c[:2] == ['docker', 'run'])
        self.assertNotIn(container.pg_secret, ' '.join(run))  # passed through the environment only
        self.assertTrue(any(c[:3] == ['docker', 'rm', '--force'] for c, _ in runner.calls))

    def test_rehearsal_fails_on_count_or_migration_drift_and_always_removes_the_container(self):
        directory, _, _ = self.make_backup()
        drift = FakeRunner(counts={**{t: 3 for t in COUNT_TABLES}, 'audit."AuditEvents"': 2}, migration='20261122050000_S9IntegrationVoiceSafetyEventLink')
        result = rehearse(directory, self.root / 'reports', runner=drift, clock=Clock(datetime(2026, 11, 20, 10, 10, tzinfo=timezone.utc)),
                          container=Container(drift, sleep=lambda _: None))
        report = json.loads((self.root / 'reports' / result['report']).read_text(encoding='utf-8'))
        self.assertEqual(('FAILED', None), (report['status'], report['measured']['rtoSeconds']))
        self.assertEqual('FAIL', next(c['result'] for c in report['checks'] if c['check'] == 'ROW_COUNTS'))
        broken = FakeRunner(fail='pg_restore')
        with self.assertRaises(DrError):
            rehearse(directory, self.root / 'reports', runner=broken, container=Container(broken, sleep=lambda _: None))
        self.assertTrue(any(c[:3] == ['docker', 'rm', '--force'] for c, _ in broken.calls))


if __name__ == '__main__':
    unittest.main()
