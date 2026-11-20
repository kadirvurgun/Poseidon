"""DEV-071 / T-S11-003 pilot restore (23.04 paragraph:58-64).

Restores a dr_backup.py directory into an ISOLATED validation database and object directory.
The target comes only from the libpq environment and must be confirmed by name
(--confirm-target equals PGDATABASE); a database whose name does not contain "restore" or
"rehearsal" is refused unless --allow-named-target is given, so a production database is not
overwritten by a mistyped environment (23.04 paragraph:62: restore into isolated validation first).
The dump and every object are verified against the manifest hashes BEFORE anything is written.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import shutil
import subprocess

from dr_backup import SCHEMA, DrError, libpq_environment, run, sha256_file

ISOLATED_MARKERS = ('restore', 'rehearsal')


def load_manifest(directory: Path) -> dict:
    path = directory / 'manifest.json'
    try:
        manifest = json.loads(path.read_text(encoding='utf-8'))
    except (OSError, ValueError):
        raise DrError('manifest_unreadable') from None
    if manifest.get('schemaVersion') != SCHEMA or manifest.get('database', {}).get('format') != 'custom':
        raise DrError('manifest_schema_mismatch')
    return manifest


def verify(directory: Path, manifest: dict) -> None:
    dump = directory / manifest['database']['file']
    if not dump.is_file() or sha256_file(dump) != manifest['database']['sha256']:
        raise DrError('dump_hash_mismatch')
    objects = manifest.get('objects')
    if objects:
        root = directory / objects['directory']
        for item in objects['items']:
            path = root / item['key']
            if not path.is_file() or path.stat().st_size != item['size'] or sha256_file(path) != item['sha256']:
                raise DrError('object_hash_mismatch')


def guard_target(env: dict, confirm: str, allow_named: bool) -> None:
    database = env.get('PGDATABASE', '')
    if confirm != database:
        raise DrError('target_not_confirmed')
    if not allow_named and not any(marker in database.lower() for marker in ISOLATED_MARKERS):
        raise DrError('target_not_isolated')


def restore(directory: Path, confirm: str, object_target: Path | None = None, allow_named: bool = False, env=None, runner=subprocess.run) -> dict:
    env = libpq_environment(env)
    guard_target(env, confirm, allow_named)
    manifest = load_manifest(directory)
    verify(directory, manifest)
    run(['pg_restore', '--clean', '--if-exists', '--no-owner', '--no-privileges', '--exit-on-error', '--single-transaction',
         '--dbname', env['PGDATABASE'], str(directory / manifest['database']['file'])], env, runner)
    restored = 0
    objects = manifest.get('objects')
    if objects and object_target is not None:
        if object_target.exists() and any(object_target.iterdir()):
            raise DrError('object_target_not_empty')
        for item in objects['items']:
            target = object_target / item['key']
            target.parent.mkdir(parents=True, exist_ok=True)
            shutil.copyfile(directory / objects['directory'] / item['key'], target)
            if sha256_file(target) != item['sha256']:
                raise DrError('object_restore_mismatch')
            restored += 1
    return {'status': 'RESTORED_UNVALIDATED', 'migration': manifest['database'].get('migration'), 'objectsRestored': restored}


def main(argv=None) -> int:
    parser = argparse.ArgumentParser(description='Restore a POSEIDON pilot backup into an isolated validation target.')
    parser.add_argument('backup', type=Path)
    parser.add_argument('--confirm-target', required=True, help='Must equal PGDATABASE of the target.')
    parser.add_argument('--object-target', type=Path)
    parser.add_argument('--allow-named-target', action='store_true')
    args = parser.parse_args(argv)
    try:
        print(json.dumps(restore(args.backup, args.confirm_target, args.object_target, args.allow_named_target)))
        return 0
    except DrError as error:
        print(json.dumps({'status': 'FAILED', 'cause': str(error)}))
        return 1


if __name__ == '__main__':
    raise SystemExit(main())
