"""Restrict a fresh owned Firefox profile to its user and privileged Windows OS actors.

The credential root is never changed. Native QuotaManager initialization needs
the normal SYSTEM/Administrators profile access that its owner-only parent lacks.
No sandbox, storage or certificate checks are disabled. CLI errors are value-free.
"""
import ctypes as c
from ctypes import wintypes as w
import os
from pathlib import Path
import sys
import uuid

PRIVATE = Path(__file__).resolve().parents[1] / '.runtime/oidc-lab'
SYSTEM = 'S-1-5-18'
ADMINISTRATORS = 'S-1-5-32-544'
FULL_CONTROL = 0x1F01FF


def owned_profile(profile, owner, private):
    """Fail closed before native writes, including aliases and nonempty profiles."""
    try:
        parsed = uuid.UUID(owner)
        if str(parsed) != owner or parsed.int == 0:
            raise ValueError()
    except (ValueError, TypeError, AttributeError):
        raise RuntimeError('profile_ownership') from None
    supplied = Path(profile).absolute()
    root = Path(private).resolve(strict=True)
    if (supplied.is_symlink() or supplied.parent.is_symlink() or
            any(getattr(path.lstat(), 'st_file_attributes', 0) & 0x400
                for path in (supplied, supplied.parent))):
        raise RuntimeError('profile_ownership')
    target = supplied.resolve(strict=True)
    directory = target.parent
    if (target != supplied or target.name != 'profile' or directory.parent != root or
            not directory.name.startswith('strict-profile-') or
            len(directory.name) <= len('strict-profile-') or
            not target.is_dir() or any(target.iterdir())):
        raise RuntimeError('profile_ownership')
    marker = directory / '.owner'
    if (marker.is_symlink() or not marker.is_file() or
            marker.lstat().st_nlink != 1 or
            getattr(marker.lstat(), 'st_file_attributes', 0) & 0x400 or
            marker.read_text(encoding='utf-8') != owner or
            {p.name for p in directory.iterdir()} != {'.owner', 'profile'}):
        raise RuntimeError('profile_ownership')
    return directory, target


def validate_acl(snapshot, current_user):
    allowed = {current_user, SYSTEM, ADMINISTRATORS}
    rules = snapshot['rules']
    if (not current_user.startswith('S-1-') or len(allowed) != 3 or
            snapshot['owner'] != current_user or not snapshot['protected'] or
            len(rules) != 3 or {r['sid'] for r in rules} != allowed or
            any(r['type'] != 0 or r['flags'] != 3 or
                r['mask'] != FULL_CONTROL for r in rules)):
        raise RuntimeError('profile_acl_readback')


class WindowsAcl:
    def __init__(self):
        if os.name != 'nt':
            raise RuntimeError('unsupported_platform')
        self.adv = c.WinDLL('advapi32', use_last_error=True)
        self.kernel = c.WinDLL('kernel32', use_last_error=True)
        def api(dll, name, result, *args):
            method = getattr(dll, name)
            method.restype, method.argtypes = result, list(args)
            return method
        self.open_token = api(self.adv, 'OpenProcessToken', w.BOOL, w.HANDLE, w.DWORD, c.POINTER(w.HANDLE))
        self.token_info = api(self.adv, 'GetTokenInformation', w.BOOL, w.HANDLE, c.c_int, w.LPVOID, w.DWORD, c.POINTER(w.DWORD))
        self.sid_text = api(self.adv, 'ConvertSidToStringSidW', w.BOOL, w.LPVOID, c.POINTER(w.LPWSTR))
        self.convert = api(self.adv, 'ConvertStringSecurityDescriptorToSecurityDescriptorW', w.BOOL, w.LPCWSTR, w.DWORD, c.POINTER(w.LPVOID), w.LPVOID)
        self.get_owner = api(self.adv, 'GetSecurityDescriptorOwner', w.BOOL, w.LPVOID, c.POINTER(w.LPVOID), c.POINTER(w.BOOL))
        self.get_dacl = api(self.adv, 'GetSecurityDescriptorDacl', w.BOOL, w.LPVOID, c.POINTER(w.BOOL), c.POINTER(w.LPVOID), c.POINTER(w.BOOL))
        self.set_named = api(self.adv, 'SetNamedSecurityInfoW', w.DWORD, w.LPWSTR, c.c_int, w.DWORD, w.LPVOID, w.LPVOID, w.LPVOID, w.LPVOID)
        self.get_named = api(self.adv, 'GetNamedSecurityInfoW', w.DWORD, w.LPCWSTR, c.c_int, w.DWORD, c.POINTER(w.LPVOID), w.LPVOID, c.POINTER(w.LPVOID), w.LPVOID, c.POINTER(w.LPVOID))
        self.control = api(self.adv, 'GetSecurityDescriptorControl', w.BOOL, w.LPVOID, c.POINTER(w.WORD), c.POINTER(w.DWORD))
        self.acl_info = api(self.adv, 'GetAclInformation', w.BOOL, w.LPVOID, w.LPVOID, w.DWORD, c.c_int)
        self.get_ace = api(self.adv, 'GetAce', w.BOOL, w.LPVOID, w.DWORD, c.POINTER(w.LPVOID))
        self.free = api(self.kernel, 'LocalFree', w.LPVOID, w.LPVOID)
        self.close = api(self.kernel, 'CloseHandle', w.BOOL, w.HANDLE)
        self.process = api(self.kernel, 'GetCurrentProcess', w.HANDLE)
        token = w.HANDLE()
        if not self.open_token(self.process(), 8, c.byref(token)):
            raise RuntimeError('profile_token')
        try:
            size = w.DWORD()
            self.token_info(token, 1, None, 0, c.byref(size))
            if not size.value:
                raise RuntimeError('profile_token')
            buf = c.create_string_buffer(size.value)
            if not self.token_info(token, 1, buf, size, c.byref(size)):
                raise RuntimeError('profile_token')
            self.current_user = self.text(c.cast(buf, c.POINTER(w.LPVOID))[0])
        finally:
            self.close(token)

    def text(self, sid):
        value = w.LPWSTR()
        if not self.sid_text(sid, c.byref(value)):
            raise RuntimeError('profile_sid')
        try:
            return value.value
        finally:
            self.free(c.cast(value, w.LPVOID))

    def protect(self, path):
        user = self.current_user
        sddl = f'O:{user}D:P(A;OICI;FA;;;{user})(A;OICI;FA;;;SY)(A;OICI;FA;;;BA)'
        sd, owner, dacl = w.LPVOID(), w.LPVOID(), w.LPVOID()
        defaulted, present = w.BOOL(), w.BOOL()
        if not self.convert(sddl, 1, c.byref(sd), None):
            raise RuntimeError('profile_acl_prepare')
        try:
            if (not self.get_owner(sd, c.byref(owner), c.byref(defaulted)) or
                    not self.get_dacl(sd, c.byref(present), c.byref(dacl), c.byref(defaulted)) or
                    not present.value or not dacl.value or
                    self.set_named(str(path), 1, 0x80000005, owner, None, dacl, None)):
                raise RuntimeError('profile_acl_prepare')
        finally:
            self.free(sd)

    def snapshot(self, path):
        sd, owner, dacl = w.LPVOID(), w.LPVOID(), w.LPVOID()
        if self.get_named(str(path), 1, 5, c.byref(owner), None, c.byref(dacl), None, c.byref(sd)):
            raise RuntimeError('profile_acl_readback')
        try:
            control, revision = w.WORD(), w.DWORD()
            size = (w.DWORD * 3)()
            if (not dacl.value or not self.control(sd, c.byref(control), c.byref(revision)) or
                    not self.acl_info(dacl, size, c.sizeof(size), 2)):
                raise RuntimeError('profile_acl_readback')
            rules = []
            for index in range(size[0]):
                ace = w.LPVOID()
                if not self.get_ace(dacl, index, c.byref(ace)):
                    raise RuntimeError('profile_acl_readback')
                header = (c.c_ubyte * 4).from_address(ace.value)
                if header[0] != 0 or int.from_bytes(bytes(header[2:4]), 'little') < 12:
                    raise RuntimeError('profile_acl_readback')
                rules.append({'type': header[0], 'flags': header[1],
                              'mask': w.DWORD.from_address(ace.value + 4).value,
                              'sid': self.text(ace.value + 8)})
            return {'owner': self.text(owner), 'protected': bool(control.value & 0x1000), 'rules': rules}
        finally:
            self.free(sd)


def prepare(profile, owner):
    directory, target = owned_profile(profile, owner, PRIVATE)
    backend = WindowsAcl()
    for path in (directory, target):
        # Recheck the marker and empty boundary immediately before each ACL write.
        owned_profile(profile, owner, PRIVATE)
        backend.protect(path)
        validate_acl(backend.snapshot(path), backend.current_user)


if __name__ == '__main__':
    try:
        prepare(*sys.argv[1:])
    except Exception:
        sys.exit(1)
