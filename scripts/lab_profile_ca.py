"""Import only the owned lab CA into a fresh disposable Firefox NSS database.

Uses public NSS APIs from Playwright's pinned Firefox distribution, not an
unverified certutil download or Windows certificate stores. Called privately by
strict-browser.ts. All diagnostics are fixed codes; no exception text escapes.
On Linux (the help-screenshot workflow) the same NSS API is loaded from the same
pinned Playwright Firefox directory (libnss3.so and its bundled dependencies).
"""
import contextlib
import ctypes as c
import os
from pathlib import Path
import sys


class Trust(c.Structure):
    _fields_ = [('sslFlags', c.c_uint), ('emailFlags', c.c_uint), ('objectSigningFlags', c.c_uint)]


# Firefox's own dependency order. Every library is required in the pinned bundle: a missing one would
# otherwise let the dynamic loader satisfy it from a system NSS/NSPR install, so the import fails closed.
POSIX_DEPENDENCIES = ('libnspr4.so', 'libplc4.so', 'libplds4.so', 'libmozsqlite3.so', 'libnssutil3.so')
# Libraries exporting the NSS API (Linux Firefox does not fold NSS: CERT_DecodeCertFromPackage is in smime).
POSIX_NSS = ('libnss3.so', 'libsmime3.so')
# Loaded by NSS_InitReadWrite itself from libnss3's directory; required so no system softoken is used.
POSIX_SOFTOKEN = ('libsoftokn3.so', 'libfreeblpriv3.so')
WINDOWS_NSS = ('nss3.dll',)


def require_bundle(bundle, names):
    """Fail closed unless every library is a regular file located directly in the pinned bundle."""
    root = Path(bundle).resolve(strict=True)
    paths = []
    for name in names:
        path = root / name
        try:
            located = path.resolve(strict=True)
        except OSError:
            raise RuntimeError('nss_bundle_incomplete') from None
        if located.parent != root or not located.is_file():
            raise RuntimeError('nss_bundle_incomplete')
        paths.append(located)
    return paths


def load_nss(bundle, platform=None):
    """Libraries exporting the NSS API: folded nss3.dll on Windows; libnss3.so + libsmime3.so on Linux."""
    if (platform or os.name) == 'nt':
        return tuple(c.CDLL(str(path)) for path in require_bundle(bundle, WINDOWS_NSS))
    # Check the complete set before loading anything: a partial bundle never reaches the loader.
    paths = require_bundle(bundle, POSIX_DEPENDENCIES + POSIX_NSS + POSIX_SOFTOKEN)
    dependencies, nss = paths[:len(POSIX_DEPENDENCIES)], paths[len(POSIX_DEPENDENCIES):][:len(POSIX_NSS)]
    # Preload the bundle's own NSPR/NSS dependencies globally so libnss3.so resolves them from this
    # directory only (never a system NSS or certutil).
    for path in dependencies:
        c.CDLL(str(path), mode=c.RTLD_GLOBAL)
    return tuple(c.CDLL(str(path), mode=c.RTLD_GLOBAL) for path in nss)


def install(profile, executable, owner):
    private = Path(__file__).resolve().parents[1] / '.runtime/oidc-lab'
    private = private.resolve(strict=True)
    profile = Path(profile).resolve(strict=True)
    if (not profile.is_relative_to(private) or profile.parent == private or
            (profile.parent / '.owner').read_text() != owner or any(profile.iterdir())):
        raise RuntimeError('profile_ownership')
    bundle = Path(executable).resolve(strict=True).parent
    # Only the distribution selected by the locked Playwright installation.
    if bundle.name != 'firefox' or bundle.parent.name != 'firefox-1555' or os.name not in ('nt', 'posix'):
        raise RuntimeError('unsupported_bundle')
    pem = (private / 'ca.pem').read_bytes()
    if pem.count(b'-----BEGIN CERTIFICATE-----') != 1:
        raise RuntimeError('single_owned_ca_required')
    with os.add_dll_directory(str(bundle)) if os.name == 'nt' else contextlib.nullcontext():
        libraries = load_nss(bundle)
        def api(name, result, *args):
            f = next((getattr(lib, name) for lib in libraries if hasattr(lib, name)), None)
            if f is None:
                raise RuntimeError('nss_symbol')
            f.restype, f.argtypes = result, list(args)
            return f
        init = api('NSS_InitReadWrite', c.c_int, c.c_char_p)
        shutdown = api('NSS_Shutdown', c.c_int)
        decode = api('CERT_DecodeCertFromPackage', c.c_void_p, c.c_char_p, c.c_int)
        destroy = api('CERT_DestroyCertificate', None, c.c_void_p)
        database = api('CERT_GetDefaultCertDB', c.c_void_p)
        slot_get = api('PK11_GetInternalKeySlot', c.c_void_p)
        slot_free = api('PK11_FreeSlot', None, c.c_void_p)
        cert_import = api('PK11_ImportCert', c.c_int, c.c_void_p, c.c_void_p, c.c_ulong, c.c_char_p, c.c_int)
        needs_pin = api('PK11_NeedUserInit', c.c_int, c.c_void_p)
        init_pin = api('PK11_InitPin', c.c_int, c.c_void_p, c.c_char_p, c.c_char_p)
        change = api('CERT_ChangeCertTrust', c.c_int, c.c_void_p, c.c_void_p, c.POINTER(Trust))
        if init(('sql:' + str(profile)).encode()) != 0:
            raise RuntimeError('nss_init')
        cert = slot = None
        try:
            cert = decode(pem, len(pem))
            slot = slot_get()
            if slot and needs_pin(slot) and init_pin(slot, None, b'') != 0:
                raise RuntimeError('nss_pin')
            if not cert or not slot or cert_import(slot, cert, 0, b'POSEIDON owned disposable lab CA', 0) != 0:
                raise RuntimeError('nss_import')
            # CERTDB_VALID_CA | CERTDB_TRUSTED_CA: server authentication only.
            if change(database(), cert, c.byref(Trust(0x18, 0, 0))) != 0:
                raise RuntimeError('nss_trust')
        finally:
            if cert:
                destroy(cert)
            if slot:
                slot_free(slot)
            if shutdown() != 0:
                raise RuntimeError('nss_shutdown')


def main(argv):
    """Exit 0 on success; otherwise exit 1 with only a fixed diagnostic code (never exception text)."""
    try:
        install(*argv)
    except Exception as error:
        code = error.args[0] if isinstance(error, RuntimeError) and len(error.args) == 1 else None
        fixed = isinstance(code, str) and code.isascii() and code.replace('_', '').isalpha() and code.islower()
        print(f'lab_profile_ca: {code if fixed else "failed"}', file=sys.stderr)
        return 1
    return 0


if __name__ == '__main__':
    sys.exit(main(sys.argv[1:]))
