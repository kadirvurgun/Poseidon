"""Publish-once private snapshots for the explicit disposable signed test host.

Windows private owner-only ACLs can reject replace-existing while allowing a
new-name rename. Configuration generations never overwrite live files or relax
ACLs. Missing, orphaned or modified generations fail closed.
"""
import argparse
import hashlib
import json
import os
from pathlib import Path
import re
import tempfile

import oidc_lab as lab

SCHEMA='poseidon.bundle.private-config-generation.v1'
MAX_BYTES=2097152


def _inside(path, *, exists=False):
    path=Path(path)
    root=lab.PRIVATE.resolve()
    if not path.is_absolute():path=path.absolute()
    if path.is_symlink() or path.resolve()==root or not path.resolve().is_relative_to(root):
        raise RuntimeError('bundle_private_boundary')
    current=path.parent
    while current.resolve()!=root:
        if current.is_symlink():raise RuntimeError('bundle_private_boundary')
        current=current.parent
    if exists and (not path.is_file() or path.stat().st_size>MAX_BYTES):
        raise RuntimeError('bundle_private_input_invalid')
    return path


def _bytes(value):
    return json.dumps(value,ensure_ascii=False,sort_keys=True,separators=(',',':')).encode('utf-8')


def digest(raw):return hashlib.sha256(raw).hexdigest()


def publish_temp(source,target):
    source=_inside(source,exists=True);target=_inside(target)
    if source.parent!=target.parent or source==target or target.exists():
        raise RuntimeError('bundle_private_publish_conflict')
    if os.name=='nt':
        os.rename(source,target)  # Windows rename without REPLACE_EXISTING; rejects a racing target.
    else:
        os.link(source,target)  # Atomic no-clobber publication on POSIX too.
        source.unlink()


def publish_bytes(path,raw):
    path=_inside(path)
    if not isinstance(raw,bytes) or len(raw)>MAX_BYTES or path.exists():
        raise RuntimeError('bundle_private_publish_conflict')
    descriptor,temporary=tempfile.mkstemp(prefix='.bundle-publish-',dir=path.parent)
    try:
        with os.fdopen(descriptor,'wb') as stream:
            stream.write(raw);stream.flush();os.fsync(stream.fileno())
        if os.name!='nt':os.chmod(temporary,0o600)
        publish_temp(Path(temporary),path)
    finally:
        Path(temporary).unlink(missing_ok=True)
    return path


def publish_json(path,value):return publish_bytes(path,_bytes(value))


def _closed(pairs):
    value={}
    for key,item in pairs:
        if key in value:raise RuntimeError('bundle_private_duplicate_json')
        value[key]=item
    return value


def read_json(path):
    path=_inside(path,exists=True)
    return json.loads(path.read_bytes(),object_pairs_hook=_closed)


def identity(config):
    federation=config['Authentication']['Oidc']
    # Mutable permissions/appointments remain explicit test configuration; the
    # original provider identities, database and TLS endpoints cannot rotate.
    members=[{k:m[k] for k in ('Issuer','Subject','ActorId','TenantId')} for m in federation['Memberships']]
    value={'ConnectionStrings':config['ConnectionStrings'],'Kestrel':config['Kestrel'],
        'Oidc':{k:federation[k] for k in ('Authority','Issuer','ClientId','ClientSecret')},
        'Memberships':sorted(members,key=lambda m:(m['Issuer'],m['Subject'])),
        'DevelopmentLab':{k:config['DevelopmentLab'][k] for k in ('OidcCaPath','WebRoot')}}
    return digest(_bytes(value))


def generations():
    manifests={};payloads={}
    for pattern,collection in (('source-bundle-host-*.json',manifests),('source-bundle-config-*.json',payloads)):
        for file in lab.PRIVATE.glob(pattern):
            match=re.fullmatch(r'source-bundle-(?:host|config)-(\d{6})\.json',file.name)
            if not match or int(match[1])<1:raise RuntimeError('bundle_private_generation_name_invalid')
            collection[int(match[1])]=file
    expected=set(range(1,len(manifests)+1))
    if set(manifests)!=expected or set(payloads)!=expected:
        raise RuntimeError('bundle_private_generation_missing_or_orphaned')
    previous=None;immutable=None;latest=None
    for number in sorted(expected):
        manifest_path=manifests[number];manifest=read_json(manifest_path)
        keys={'schemaVersion','generation','configFile','configSha256','previousManifestSha256','immutableIdentitySha256'}
        if (not isinstance(manifest,dict) or set(manifest)!=keys or manifest['schemaVersion']!=SCHEMA or
                type(manifest['generation']) is not int or manifest['generation']!=number or
                manifest['configFile']!=payloads[number].name or manifest['previousManifestSha256']!=previous):
            raise RuntimeError('bundle_private_generation_chain_invalid')
        config=read_json(payloads[number]);observed=identity(config)
        if manifest['configSha256']!=digest(payloads[number].read_bytes()) or manifest['immutableIdentitySha256']!=observed or immutable is not None and immutable!=observed:
            raise RuntimeError('bundle_private_generation_tampered')
        immutable=observed;previous=digest(manifest_path.read_bytes());latest=(config,payloads[number],number,previous)
    return latest


def selected():
    latest=generations()
    if latest is None:raise RuntimeError('bundle_private_generation_required')
    return latest[0],latest[1]


def publish_config(config):
    latest=generations();number=1 if latest is None else latest[2]+1
    immutable=identity(config)
    if latest is not None and identity(latest[0])!=immutable:
        raise RuntimeError('bundle_private_identity_changed')
    payload=lab.PRIVATE/f'source-bundle-config-{number:06d}.json'
    manifest=lab.PRIVATE/f'source-bundle-host-{number:06d}.json'
    raw=_bytes(config)
    publish_bytes(payload,raw)
    publish_json(manifest,{'schemaVersion':SCHEMA,'generation':number,'configFile':payload.name,
        'configSha256':digest(raw),'previousManifestSha256':None if latest is None else latest[3],
        'immutableIdentitySha256':immutable})
    return payload


def main(argv=None):
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('action',choices=['publish-temp']);parser.add_argument('--source',type=Path,required=True);parser.add_argument('--target',type=Path,required=True)
    args=parser.parse_args(argv)
    try:publish_temp(args.source,args.target);return 0
    except Exception:return 1


if __name__=='__main__':raise SystemExit(main())
