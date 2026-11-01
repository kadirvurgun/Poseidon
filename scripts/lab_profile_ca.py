"""Import only the owned lab CA into a fresh disposable Firefox NSS database.

Uses public NSS APIs from Playwright's pinned Firefox distribution, not an
unverified certutil download or Windows certificate stores. Called privately by
strict-browser.ts. All diagnostics are fixed codes; no exception text escapes.
"""
import ctypes as c
import os
from pathlib import Path
import sys


class Trust(c.Structure):
    _fields_ = [('sslFlags', c.c_uint), ('emailFlags', c.c_uint), ('objectSigningFlags', c.c_uint)]


def install(profile, executable, owner):
    private = Path(__file__).resolve().parents[1] / '.runtime/oidc-lab'
    private = private.resolve(strict=True)
    profile = Path(profile).resolve(strict=True)
    if (not profile.is_relative_to(private) or profile.parent == private or
            (profile.parent / '.owner').read_text() != owner or any(profile.iterdir())):
        raise RuntimeError('profile_ownership')
    bundle = Path(executable).resolve(strict=True).parent
    # Only the distribution selected by the locked Playwright installation.
    if bundle.name != 'firefox' or bundle.parent.name != 'firefox-1555' or os.name != 'nt':
        raise RuntimeError('unsupported_bundle')
    pem = (private / 'ca.pem').read_bytes()
    if pem.count(b'-----BEGIN CERTIFICATE-----') != 1:
        raise RuntimeError('single_owned_ca_required')
    with os.add_dll_directory(str(bundle)):
        nss = c.CDLL(str(bundle / 'nss3.dll'))
        def api(name, result, *args):
            f = getattr(nss, name)
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


if __name__ == '__main__':
    try:
        install(*sys.argv[1:])
    except Exception:
        sys.exit(1)
