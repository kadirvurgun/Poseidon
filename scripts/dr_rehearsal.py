"""DEV-071 / T-S11-003 restore rehearsal with measured RPO/RTO (23.04 paragraph:12-14, 40-79).

Starts a throw-away PostgreSQL container (loopback port, random password held only in memory),
restores a dr_backup.py directory into it with dr_restore.restore, runs the 23.04 restore
checklist that can be checked from data alone and measures:

  RPO = simulated incident instant (rehearsal start) - newest audit record in the RESTORED database
  RTO = restore start -> restored database open + schema/migration version matches the manifest

The 23.04 tier values (T0 RPO <= 15 min, RTO <= 4 h, paragraph:47) are proposed design targets
that need Production Council approval (paragraph:48, 57); the report compares against them only
as information and never marks DR accepted. Tenant isolation, offline replay and PMS/operations
business samples need the application host (paragraph:70-79) and are reported NOT_RUN here.
The rollback procedure signature is a human record: the report carries an empty
OWNER_SIGNATURE_PENDING field and this script can never fill it.

Requires Docker. Do not run it against a shared Docker engine another session is using.
"""
from __future__ import annotations

import argparse
from datetime import datetime
import json
from pathlib import Path
import random
import secrets
import subprocess
import tempfile
import time

from dr_backup import COUNT_TABLES, ROOT, DrError, iso, query, run, sha256_file, utc_now
from dr_restore import load_manifest, restore

IMAGE = 'postgres:18.6'  # Same pinned engine as the PostgreSQL integration tests.
DATABASE = 'poseidon_rehearsal'
REPORT_SCHEMA = 'poseidon.dr-rehearsal.v1'
PROPOSED_TARGETS = {'rpoSeconds': 900, 'rtoSeconds': 14400, 'tier': 'T0', 'source': '23.04/paragraph:45-48',
                    'approval': 'PRODUCTION_COUNCIL_APPROVAL_REQUIRED'}


def parse_instant(value: str | None) -> datetime | None:
    if not value:
        return None
    return datetime.fromisoformat(value.replace('Z', '+00:00'))


class Container:
    """Owned loopback PostgreSQL container; removed in every outcome."""

    def __init__(self, runner=subprocess.run, sleep=time.sleep, attempts=60):
        self.runner, self.sleep, self.attempts = runner, sleep, attempts
        self.pg_secret = secrets.token_urlsafe(24)
        self.id = None

    def start(self) -> dict:
        env = {'POSTGRES_PASSWORD': self.pg_secret}
        output = run(['docker', 'run', '--detach', '--rm', '--publish', '127.0.0.1::5432', '--env', 'POSTGRES_PASSWORD',
                      '--env', f'POSTGRES_DB={DATABASE}', IMAGE], {**_base(), **env}, self.runner, timeout=600)
        self.id = output.strip().splitlines()[-1] if output.strip() else None
        if not self.id or not all(c in '0123456789abcdef' for c in self.id):
            raise DrError('container_start_failed')
        port = run(['docker', 'port', self.id, '5432/tcp'], _base(), self.runner).strip().splitlines()[0].rsplit(':', 1)[-1]
        if not port.isdigit():
            raise DrError('container_port_unavailable')
        env = {**_base(), 'PGHOST': '127.0.0.1', 'PGPORT': port, 'PGUSER': 'postgres', 'PGPASSWORD': self.pg_secret, 'PGDATABASE': DATABASE}
        for _ in range(self.attempts):
            try:
                run(['pg_isready', '--quiet'], env, self.runner, timeout=10)
                return env
            except DrError:
                self.sleep(1)
        raise DrError('container_not_ready')

    def stop(self):
        if self.id:
            try:
                self.runner(['docker', 'rm', '--force', self.id], env=_base(), capture_output=True, text=True, timeout=120, check=False)
            except (OSError, subprocess.TimeoutExpired):
                pass


def _base() -> dict:
    import os
    return {k: v for k, v in os.environ.items() if k.upper() in ('PATH', 'SYSTEMROOT', 'TEMP', 'TMP', 'HOME', 'USERPROFILE', 'DOCKER_HOST', 'DOCKER_CONTEXT')}


def check(name: str, ok: bool | None, detail: str, locator: str) -> dict:
    return {'check': name, 'result': 'NOT_RUN' if ok is None else 'PASS' if ok else 'FAIL', 'detail': detail, 'source': locator}


def rehearse(backup_dir: Path, out_dir: Path, sample: int = 20, seed: int = 0, runner=subprocess.run, clock=utc_now, container=None) -> dict:
    manifest = load_manifest(backup_dir)
    incident = clock()
    container = container or Container(runner)
    checks = []
    try:
        env = container.start()
        with tempfile.TemporaryDirectory(prefix='poseidon-dr-objects-') as objects:
            restore_started = clock()
            restored = restore(backup_dir, DATABASE, Path(objects) / 'restored', env=env, runner=runner)
            opens = query('SELECT 1', env, runner) == '1'
            facts = {'migration': query('SELECT max("MigrationId") FROM "__EFMigrationsHistory"', env, runner) or None}
            healthy = opens and facts['migration'] == manifest['database'].get('migration')
            health_at = clock()
            checks.append(check('DATABASE_OPENS', opens, 'SELECT 1 on the restored database', '23.04/paragraph:68'))
            checks.append(check('MIGRATION_VERSION', healthy, 'restored __EFMigrationsHistory head equals the manifest', '23.04/paragraph:69'))
            expected = manifest['database'].get('tableCounts', {})
            mismatched = [t for t in COUNT_TABLES if t in expected and query(f'SELECT count(*) FROM {t}', env, runner) != str(expected[t])]
            checks.append(check('ROW_COUNTS', not mismatched, f'{len(expected) - len(mismatched)}/{len(expected)} owning tables match', '23.04/paragraph:76-77'))
            last_audit = query('SELECT to_char(max("OccurredAt") AT TIME ZONE \'UTC\', \'YYYY-MM-DD"T"HH24:MI:SS.US"Z"\') FROM audit."AuditEvents"', env, runner) or None
            checks.append(check('AUDIT_QUERYABLE', last_audit is not None and last_audit == manifest['database'].get('lastAuditAt'),
                                'critical history queryable; newest audit equals the backup', '23.04/paragraph:74-75'))
            items = (manifest.get('objects') or {}).get('items') or []
            picked = random.Random(seed).sample(items, min(sample, len(items))) if items else []
            evidence_ok = None
            if items:
                evidence_ok = True
                for item in picked:
                    _, tenant, ident = item['key'].split('/')
                    path = Path(objects) / 'restored' / item['key']
                    stored = query(f'SELECT upper("Checksum") FROM files."StoredFiles" WHERE "TenantId" = \'{_guid(tenant)}\' AND "Id" = \'{_guid(ident)}\'', env, runner)
                    if not path.is_file() or sha256_file(path) != item['sha256'] or (stored and stored != item['sha256'].upper()):
                        evidence_ok = False
            checks.append(check('EVIDENCE_SAMPLE', evidence_ok, f'{len(picked)} sampled objects: checksum and StoredFiles provenance', '23.04/paragraph:72-73'))
            checks.append(check('TENANT_ISOLATION', None, 'needs the application host with two tenant actors; DEFERRED', '23.04/paragraph:70-71'))
            checks.append(check('OFFLINE_SYNC', None, 'queued/replayed command behaviour needs the application host; DEFERRED', '23.04/paragraph:78-79'))
    finally:
        container.stop()
    rpo = (incident - parse_instant(last_audit)).total_seconds() if last_audit else None
    rto = (health_at - restore_started).total_seconds() if healthy else None
    failed = any(c['result'] == 'FAIL' for c in checks)
    report = {
        'schemaVersion': REPORT_SCHEMA, 'backup': backup_dir.name, 'incidentAt': iso(incident), 'restoreStartedAt': iso(restore_started),
        'healthAt': iso(health_at) if healthy else None, 'image': IMAGE,
        'measured': {'rpoSeconds': rpo, 'rtoSeconds': rto, 'backupLagSeconds': _lag(manifest)},
        'proposedTargets': PROPOSED_TARGETS,
        'withinProposedTargets': None if rpo is None or rto is None else rpo <= PROPOSED_TARGETS['rpoSeconds'] and rto <= PROPOSED_TARGETS['rtoSeconds'],
        'checks': checks, 'objectsRestored': restored['objectsRestored'],
        'status': 'FAILED' if failed else 'MEASURED_PARTIAL_UNSIGNED',
        # Human record only (18.01 DEV-071 "rollback procedure signed"); never filled by automation.
        'rollbackProcedureSignOff': {'state': 'OWNER_SIGNATURE_PENDING', 'signedBy': None, 'signedAt': None, 'runbook': 'docs/operations/dr-runbook.md'},
        'acceptance': 'NOT_ACCEPTED',
    }
    out_dir.mkdir(parents=True, exist_ok=True)
    path = out_dir / f'rehearsal-{incident.strftime("%Y%m%dT%H%M%SZ")}.json'
    path.write_text(json.dumps(report, indent=2) + '\n', encoding='utf-8')
    return {'status': report['status'], 'report': path.name, 'rpoSeconds': rpo, 'rtoSeconds': rto}


def _guid(value: str) -> str:
    if len(value) != 32 or any(c not in '0123456789abcdef' for c in value):
        raise DrError('object_key_unexpected')
    return f'{value[:8]}-{value[8:12]}-{value[12:16]}-{value[16:20]}-{value[20:]}'


def _lag(manifest: dict) -> float | None:
    completed, last = parse_instant(manifest.get('dumpCompletedAt')), parse_instant(manifest['database'].get('lastAuditAt'))
    return (completed - last).total_seconds() if completed and last else None


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description='POSEIDON pilot restore rehearsal (Docker, measured RPO/RTO, unsigned).')
    parser.add_argument('backup', type=Path)
    parser.add_argument('--out-dir', type=Path, default=ROOT / '.runtime' / 'dr')
    parser.add_argument('--sample', type=int, default=20)
    parser.add_argument('--seed', type=int, default=0)
    args = parser.parse_args(argv)
    try:
        print(json.dumps(rehearse(args.backup, args.out_dir, args.sample, args.seed)))
        return 0
    except DrError as error:
        print(json.dumps({'status': 'FAILED', 'cause': str(error)}))
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
