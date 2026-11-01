"""DEV-017 bounded tracked-repository / actual production-client artifact scan.

This pattern scanner is a gate, not proof that all possible secrets are absent.
Never print matching data, exception details, environment or raw lines.
"""
import argparse
import hashlib
import io
import os
import json
import re
import subprocess
import sys
import zipfile
from pathlib import Path
from xml.etree import ElementTree

MAX_FILE = 128 * 1024 * 1024
MAX_MEMBER = 32 * 1024 * 1024
MAX_EXPANDED = 128 * 1024 * 1024
MAX_DEPTH = 6
RULES = {
    'private-key': re.compile(r'-----BEGIN (?:RSA |EC |OPENSSH |DSA )?PRIVATE KEY-----'),
    'github-token': re.compile(r'\b(?:gh[pousr]_[A-Za-z0-9]{30,255}|github_pat_[A-Za-z0-9_]{40,255})\b'),
    'aws-key': re.compile(r'\b(?:AKIA|ASIA)[A-Z0-9]{16}\b'),
    'jwt': re.compile(r'\beyJ[A-Za-z0-9_-]{12,}\.[A-Za-z0-9_-]{12,}\.[A-Za-z0-9_-]{16,}\b'),
    'db-password': re.compile(r'(?i)\b(?:password|pwd)\s*=\s*[^\s;\x00"\'<>]{8,}'),
    'credential-literal': re.compile(r'''(?ix)["']?(?:client[_-]?secret|api[_-]?key|access[_-]?token|password)["']?\s*[:=]\s*["'][^"'\r\n]{8,}["']'''),
}


def findings(text, locator):
    for rule, pattern in RULES.items():
        for match in pattern.finditer(text):
            line = text.count('\n', 0, match.start()) + 1
            # The fingerprint covers the exact match and cannot act as a path-wide exemption.
            yield {'path': locator, 'rule': rule, 'line': line,
                   'fingerprint': hashlib.sha256(match.group().encode()).hexdigest()}


def safe_path(value):
    # A hostile filename can itself contain credential data. Use a safe identifier then.
    if len(value) > 240 or any(ord(c) < 32 for c in value) or any(p.search(value) for p in RULES.values()):
        return 'path-sha256-' + hashlib.sha256(value.encode()).hexdigest()[:20]
    return value


def read_file(path, root):
    if os.name == 'nt':
        # Normalize the long-path prefix without following links before the guards below.
        path, root = Path(os.path.abspath(path)), Path(os.path.abspath(root))
        if not str(path).startswith('\\\\?\\'):
            path = Path('\\\\?\\' + str(path))
        if not str(root).startswith('\\\\?\\'):
            root = Path('\\\\?\\' + str(root))
    if path.is_symlink() or path.is_junction() or not path.resolve().is_relative_to(root.resolve()):
        raise ValueError('input')
    for parent in path.parents:
        if parent == root.parent:
            break
        if parent.is_symlink() or parent.is_junction():
            raise ValueError('input')
    if not path.is_file() or path.stat().st_size > MAX_FILE:
        raise ValueError('input')
    return path.read_bytes()


def scan_file(path, root):
    label = safe_path(path.relative_to(root).as_posix())
    raw = read_file(path, root)
    result = list(findings(raw.decode('utf-8', errors='replace'), label))
    if raw.startswith((b'\xff\xfe', b'\xfe\xff')):
        result.extend(findings(raw.decode('utf-16'), label + '#utf16'))
    # Controlled originals are scanned without editing their bytes/hashes. No blanket docs/fixture exclusion.
    if path.suffix.lower() in ('.docx', '.xlsx', '.pptx', '.zip'):
        budget = {'bytes': 0, 'members': 0}
        def expand(content, prefix, depth):
            if depth > MAX_DEPTH:
                raise ValueError('archive-depth')
            with zipfile.ZipFile(io.BytesIO(content)) as archive:
                scan_archive(archive, prefix, depth)
        def scan_archive(archive, prefix, depth):
            members = archive.infolist()
            budget['members'] += len(members)
            budget['bytes'] += sum(m.file_size for m in members)
            if budget['members'] > 10000 or budget['bytes'] > MAX_EXPANDED:
                raise ValueError('archive')
            for member in members:
                if member.is_dir():
                    continue
                if member.flag_bits & 1 or member.file_size > MAX_MEMBER:
                    raise ValueError('archive')
                with archive.open(member) as stream:
                    body = stream.read(MAX_MEMBER + 1)
                if len(body) > MAX_MEMBER or len(body) != member.file_size:
                    raise ValueError('archive')
                location = prefix + '!' + safe_path(member.filename)
                if body.startswith(b'PK\x03\x04'):
                    expand(body, location, depth + 1)
                decoded = body.decode('utf-8', errors='replace')
                result.extend(findings(decoded, location))
                if member.filename.endswith('.xml'):
                    # Join Word runs to detect split literals, without entity/network expansion.
                    if '<!DOCTYPE' in decoded or '<!ENTITY' in decoded:
                        raise ValueError('xml')
                    element = ElementTree.fromstring(body)
                    result.extend(findings(''.join(element.itertext()), location + '#text'))
        expand(raw, label, 0)
    return result


def scan(root, mode, bundle=None, exceptions=None):
    root = Path(root).resolve()
    if os.name == 'nt' and not str(root).startswith('\\\\?\\'):
        root = Path('\\\\?\\' + str(root))
    exemptions = [] if exceptions is None else json.loads(read_file(Path(exceptions), root))
    if not isinstance(exemptions, list):
        raise ValueError('exceptions')
    for item in exemptions:
        if set(item) != {'path', 'rule', 'line', 'fingerprint', 'rationale'} or not isinstance(item['rationale'], str) or len(item['rationale']) < 20 or not re.fullmatch('[0-9a-f]{64}', item['fingerprint']):
            raise ValueError('exceptions')
    if mode == 'repository':
        listed = subprocess.run(['git', '-C', str(root), 'ls-files', '-z'], check=True, capture_output=True).stdout
        paths = [root / p.decode('utf-8') for p in listed.split(b'\0') if p]
        if not paths:
            raise ValueError('coverage')
    else:
        folder = (root / bundle).resolve() if bundle else root / 'frontend/dist'
        if not folder.is_relative_to(root) or not folder.is_dir() or folder.is_symlink():
            raise ValueError('coverage')
        paths = sorted(folder.rglob('*'))
        if any(p.is_symlink() for p in paths):
            raise ValueError('input')
        paths = [p for p in paths if p.is_file()]
        if not (folder / 'index.html').is_file() or not any(p.suffix == '.js' and p.stat().st_size > 0 for p in paths):
            raise ValueError('coverage')
    all_findings = []; covered = 0; covered_bytes = 0
    for path in paths:
        all_findings.extend(scan_file(path, root)); covered += 1; covered_bytes += path.stat().st_size
    if covered == 0 or covered_bytes == 0:
        raise ValueError('coverage')
    remaining = [f for f in all_findings if not any(all(f[k] == x[k] for k in f) for x in exemptions)]
    return covered, list({(f['path'], f['rule'], f['line'], f['fingerprint']): f for f in remaining}.values())


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--mode', required=True, choices=['repository', 'client'])
    parser.add_argument('--bundle', default='frontend/dist')
    parser.add_argument('--exceptions', type=Path)
    args = parser.parse_args(argv)
    try:
        count, found = scan(args.root, args.mode, args.bundle, args.exceptions)
        for item in found:
            print(json.dumps({k: item[k] for k in ('path', 'rule', 'line')}, ensure_ascii=True))
        print(json.dumps({'mode': args.mode, 'covered_files': count, 'findings': len(found)}))
        return 1 if found else 0
    except Exception:
        print(json.dumps({'rule': 'required-input-or-coverage-failure'}))
        return 2


if __name__ == '__main__':
    sys.exit(main())
