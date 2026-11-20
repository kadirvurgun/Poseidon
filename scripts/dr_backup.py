"""DEV-071 / T-S11-003 pilot backup (23.04 Backup, Restore & DR Runbook paragraph:18-39).

Takes a PostgreSQL custom-format dump plus an object/evidence snapshot with a key/size/sha256
manifest. Connection settings come ONLY from the standard libpq environment (PGHOST, PGPORT,
PGUSER, PGPASSWORD or PGPASSFILE, PGDATABASE, PGSSLMODE); they are never accepted as arguments,
written to the manifest or printed. Object bytes are read from an operator-provided snapshot
directory (a provider sync/mirror of the bucket, 23.04 paragraph:26); this script never holds
provider credentials.

The manifest records what was copied and its hashes. It is not a restore proof (23.04
paragraph:12): only dr_rehearsal.py measures restore, RPO and RTO, and only a human signs the
rollback procedure.
"""
from __future__ import annotations

import argparse
from datetime import datetime, timezone
import hashlib
import json
import os
from pathlib import Path
import re
import shutil
import subprocess

ROOT = Path(__file__).resolve().parents[1]
SCHEMA = 'poseidon.dr-backup.v1'
LIBPQ_ENV = ('PGHOST', 'PGPORT', 'PGUSER', 'PGPASSWORD', 'PGPASSFILE', 'PGDATABASE', 'PGSSLMODE', 'PGSSLROOTCERT', 'PGSERVICE', 'PGSERVICEFILE')
# Owning-source tables counted exactly at backup and compared after restore (23.04 paragraph:68-79).
COUNT_TABLES = ('core."Tenants"', 'core."Vessels"', 'audit."AuditEvents"', 'files."StoredFiles"', 'crew."Assignments"',
                'technical."WorkOrders"', 'tug."Jobs"', 'hse."SafetyEvents"', 'intelligence."PilotMetricBatches"')
OBJECT_KEY = re.compile(r'poseidon/[0-9a-f]{32}/[0-9a-f]{32}\Z')
MAX_OBJECTS = 1_000_000


class DrError(RuntimeError):
    """Fixed cause code only: never a path, connection value, command line or exception text."""


def utc_now() -> datetime:
    return datetime.now(timezone.utc)


def iso(value: datetime) -> str:
    return value.astimezone(timezone.utc).strftime('%Y-%m-%dT%H:%M:%S.%fZ')


def libpq_environment(source=None) -> dict:
    """Child environment with only PATH/system basics plus libpq variables."""
    source = os.environ if source is None else source
    keep = {k: v for k, v in source.items() if k.upper() in ('PATH', 'SYSTEMROOT', 'TEMP', 'TMP', 'HOME', 'USERPROFILE', 'LANG', 'LC_ALL')}
    keep.update({k: v for k, v in source.items() if k in LIBPQ_ENV})
    if 'PGDATABASE' not in keep:
        raise DrError('database_not_configured')
    return keep


def run(command, env, runner=subprocess.run, timeout=3600) -> str:
    # Output is captured and never echoed; a failure is reported by its fixed stage code.
    try:
        result = runner(command, env=env, capture_output=True, text=True, timeout=timeout, check=False)
    except (OSError, subprocess.TimeoutExpired):
        raise DrError(f'{Path(command[0]).name}_unavailable') from None
    if result.returncode != 0:
        raise DrError(f'{Path(command[0]).name}_failed')
    return result.stdout


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open('rb') as handle:
        for block in iter(lambda: handle.read(1024 * 1024), b''):
            digest.update(block)
    return digest.hexdigest()


def query(sql: str, env, runner=subprocess.run) -> str:
    return run(['psql', '--no-psqlrc', '--tuples-only', '--no-align', '--set', 'ON_ERROR_STOP=1', '--command', sql], env, runner).strip()


def database_facts(env, runner=subprocess.run) -> dict:
    migration = query('SELECT max("MigrationId") FROM "__EFMigrationsHistory"', env, runner) or None
    last_audit = query('SELECT to_char(max("OccurredAt") AT TIME ZONE \'UTC\', \'YYYY-MM-DD"T"HH24:MI:SS.US"Z"\') FROM audit."AuditEvents"', env, runner) or None
    counts = {}
    for table in COUNT_TABLES:
        value = query(f'SELECT count(*) FROM {table}', env, runner)
        if not value.isdigit():
            raise DrError('count_unreadable')
        counts[table] = int(value)
    return {'migration': migration, 'lastAuditAt': last_audit, 'tableCounts': counts}


def object_manifest(root: Path, destination: Path | None = None) -> list:
    """Walk the object snapshot; copy each object (when destination is given) and hash it."""
    if not root.is_dir():
        raise DrError('object_root_unavailable')
    items = []
    for path in sorted(p for p in root.rglob('*') if p.is_file()):
        if path.is_symlink():
            raise DrError('object_root_link_refused')
        key = path.relative_to(root).as_posix()
        if not OBJECT_KEY.match(key):
            raise DrError('object_key_unexpected')
        if len(items) >= MAX_OBJECTS:
            raise DrError('object_limit_exceeded')
        digest = sha256_file(path)
        if destination is not None:
            target = destination / key
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(path, target)
            if sha256_file(target) != digest:
                raise DrError('object_copy_mismatch')
        items.append({'key': key, 'size': path.stat().st_size, 'sha256': digest})
    return items


def backup(out_dir: Path, object_root: Path | None, env=None, runner=subprocess.run, clock=utc_now) -> dict:
    env = libpq_environment(env)
    started = clock()
    stamp = started.strftime('%Y%m%dT%H%M%SZ')
    target = out_dir / f'backup-{stamp}'
    try:
        target.mkdir(parents=True, exist_ok=False)
    except FileExistsError:
        raise DrError('backup_directory_exists') from None
    facts_before = database_facts(env, runner)
    dump = target / 'database.dump'
    # Custom format: compressed, selective/parallel restore; ownership/ACLs are re-applied by the target operator.
    run(['pg_dump', '--format=custom', '--no-owner', '--no-privileges', '--file', str(dump)], env, runner)
    completed = clock()
    if not dump.is_file() or dump.stat().st_size == 0:
        raise DrError('dump_missing')
    objects = object_manifest(object_root, target / 'objects') if object_root is not None else None
    manifest = {
        'schemaVersion': SCHEMA,
        'createdAt': iso(started), 'dumpCompletedAt': iso(completed),
        'database': {'file': dump.name, 'format': 'custom', 'bytes': dump.stat().st_size, 'sha256': sha256_file(dump), **facts_before},
        'objects': None if objects is None else {'directory': 'objects', 'count': len(objects), 'bytes': sum(o['size'] for o in objects), 'items': objects},
        'objectState': 'NOT_CAPTURED' if objects is None else 'CAPTURED',
        # Secrets are provider-managed (23.04 paragraph:31-32) and never exported by this script.
        'secretsExported': False,
        'restoreProof': 'NOT_PROVEN_BY_BACKUP',
    }
    (target / 'manifest.json').write_text(json.dumps(manifest, indent=2) + '\n', encoding='utf-8')
    return {'status': 'BACKUP_WRITTEN', 'directory': target.name, 'databaseBytes': manifest['database']['bytes'],
            'objectCount': None if objects is None else len(objects), 'migration': facts_before['migration']}


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description='POSEIDON pilot backup (DB custom dump + object manifest).')
    parser.add_argument('--out-dir', type=Path, default=ROOT / '.runtime' / 'dr')
    parser.add_argument('--object-root', type=Path, help='Operator-provided object snapshot directory (poseidon/<tenant>/<id> layout).')
    args = parser.parse_args(argv)
    try:
        print(json.dumps(backup(args.out_dir, args.object_root)))
        return 0
    except DrError as error:
        print(json.dumps({'status': 'FAILED', 'cause': str(error)}))
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
