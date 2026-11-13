"""Owned source-fixture object storage: create privately once, validate reuse read-only.

The helper accepts only the trusted lab module, not request paths or identities.
Existing objects and ACLs are never rewritten or repaired. All errors are closed.
"""
import hashlib
import os
from pathlib import Path
import re
import stat
import tempfile

from lab_profile_acl import FULL_CONTROL, WindowsAcl


def _windows():
    return os.name == 'nt'


def _metadata(path):
    return path.lstat()


def _identity(info):
    return info.st_dev, info.st_ino


def _ordinary(path, info):
    return (stat.S_ISDIR(info.st_mode) and not path.is_symlink() and
            not path.is_junction() and not getattr(info, 'st_file_attributes', 0) & 0x400)


def _boundary(path, repository, *, exists):
    if (not path.is_absolute() or '..' in path.parts or path.resolve() != path or
            path == repository or path.is_relative_to(repository)):
        raise RuntimeError('source_bundle_object_boundary')
    info = None
    for current in (path, *path.parents):
        try:
            value = _metadata(current)
        except FileNotFoundError:
            if current == path and not exists:
                continue
            raise RuntimeError('source_bundle_object_boundary') from None
        marker = current / '.git'
        if (not _ordinary(current, value) or marker.exists() or
                marker.is_symlink() or marker.is_junction()):
            raise RuntimeError('source_bundle_object_boundary')
        if current == path:
            info = value
    return info


def validate_windows(snapshot, current_user):
    """Exact ACL produced by the unchanged owner-only private-directory creator."""
    if (type(snapshot) is not dict or set(snapshot) != {'owner', 'protected', 'rules'} or
            not isinstance(current_user, str) or not re.fullmatch(r'S-1-(?:[0-9]+-)*[0-9]+', current_user) or
            snapshot['owner'] != current_user or snapshot['protected'] is not True or
            type(snapshot['rules']) is not list or len(snapshot['rules']) != 1):
        raise RuntimeError('source_bundle_object_acl')
    rule = snapshot['rules'][0]
    if (type(rule) is not dict or set(rule) != {'sid', 'type', 'flags', 'mask'} or
            rule['sid'] != current_user or
            any(type(rule[key]) is not int or rule[key] != expected
                for key, expected in [('type', 0), ('flags', 3), ('mask', FULL_CONTROL)])):
        raise RuntimeError('source_bundle_object_acl')


def validate_posix(info, current_user):
    if info.st_uid != current_user or stat.S_IMODE(info.st_mode) != 0o700:
        raise RuntimeError('source_bundle_object_private')


def _validate_existing(path, repository, expected):
    before = _boundary(path, repository, exists=True)
    if _identity(before) != expected:
        raise RuntimeError('source_bundle_object_identity')
    if _windows():
        # Only token/descriptor READ methods. Never WindowsAcl.protect or Set-Acl.
        native = WindowsAcl()
        validate_windows(native.snapshot(path), native.current_user)
        after = _boundary(path, repository, exists=True)
        validate_windows(native.snapshot(path), native.current_user)
    else:
        current_user = os.geteuid()
        validate_posix(before, current_user)
        after = _boundary(path, repository, exists=True)
        validate_posix(after, current_user)
    final = _boundary(path, repository, exists=True)
    if _identity(after) != expected or _identity(final) != expected:
        raise RuntimeError('source_bundle_object_identity')


def ensure(lab):
    """Resolve the established per-worktree root; no arbitrary caller target exists."""
    try:
        repository = Path(lab.ROOT)
        if (not repository.is_absolute() or '..' in repository.parts or
                repository.resolve(strict=True) != repository or not repository.is_dir()):
            raise RuntimeError('source_bundle_object_boundary')
        project = 'poseidon-lab-' + hashlib.sha256(str(repository).encode()).hexdigest()[:12]
        if lab.PROJECT != project:
            raise RuntimeError('source_bundle_object_boundary')
        path = Path(tempfile.gettempdir()) / (project + '-source-bundle-objects')
        before = _boundary(path, repository, exists=False)
        if before is None:
            try:
                # Exclusive creation. A racing existing root is not adopted/restricted.
                path.mkdir(mode=0o700)
            except FileExistsError:
                raise RuntimeError('source_bundle_object_creation_raced') from None
            created = _boundary(path, repository, exists=True)
            expected = _identity(created)
            # This is the only mutating branch, before any runtime/object bytes.
            lab.private_directory(path)
            _validate_existing(path, repository, expected)
            if any(path.iterdir()):
                raise RuntimeError('source_bundle_object_creation_raced')
        else:
            # An API may already hold object handles. Readback never rewrites ACLs.
            _validate_existing(path, repository, _identity(before))
        return path
    except (OSError, ValueError, TypeError, AttributeError):
        raise RuntimeError('source_bundle_object_boundary') from None

