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


IDENTITY_VERSION = 'source-binding-v2'
TARGET_KEYS = ('path', 'rule', 'line', 'occurrence', 'fingerprint', 'context_sha256',
               'immutable_source_revision', 'identity_version')


def digest(value):
    return hashlib.sha256(value).hexdigest()


def object_hash(value):
    return digest(json.dumps(value, sort_keys=True, separators=(',', ':'), ensure_ascii=True).encode())


def canonical_bytes(raw):
    # Only newline code units change. Invalid bytes survive intact, even when the
    # presentation decoder later replaces them. Archives are always byte-exact.
    if raw.startswith(b'PK'):
        return raw
    if raw.startswith((b'\xff\xfe', b'\xfe\xff')):
        endian = 'little' if raw.startswith(b'\xff\xfe') else 'big'
        cr, lf = (n.to_bytes(2, endian) for n in (13, 10))
        units = [raw[i:i + 2] for i in range(2, len(raw), 2)]
        out = []; i = 0
        while i < len(units):
            unit = units[i]
            if unit == cr:
                out.append(lf)
                if i + 1 < len(units) and units[i + 1] == lf:
                    i += 1
            else:
                out.append(unit)
            i += 1
        return raw[:2] + b''.join(out)
    return raw.replace(b'\r\n', b'\n').replace(b'\r', b'\n')


def source_revision(raw):
    content = canonical_bytes(raw)
    # Git SHA-256 blob preimage, independent of checkout commit and object format.
    return 'git-blob-sha256:' + digest(b'blob ' + str(len(content)).encode() + b'\0' + content)


def presentation(raw):
    return canonical_bytes(raw).decode('utf-16' if raw.startswith((b'\xff\xfe', b'\xfe\xff')) else 'utf-8', errors='replace')


def findings(text, locator, *, raw=None, source=None):
    raw = text.encode('utf-8') if raw is None else raw
    text = text.replace('\r\n', '\n').replace('\r', '\n')
    context = digest(canonical_bytes(raw))
    for rule, pattern in RULES.items():
        for match in pattern.finditer(text):
            line = text.count('\n', 0, match.start()) + 1
            # The fingerprint covers the exact match and cannot act as a path-wide exemption.
            yield {'path': locator, 'rule': rule, 'line': line, 'occurrence': match.start(),
                   'fingerprint': hashlib.sha256(match.group().encode()).hexdigest(),
                   'context_sha256': context, 'immutable_source_revision': source or source_revision(raw),
                   'identity_version': IDENTITY_VERSION}


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
    source = source_revision(raw)
    result = list(findings(raw.decode('utf-8', errors='replace'), label, raw=raw, source=source))
    if raw.startswith((b'\xff\xfe', b'\xfe\xff')):
        result.extend(findings(presentation(raw), label + '#utf16', raw=raw, source=source))
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
            names = [m.filename for m in members]
            if len(names) != len(set(names)):
                raise ValueError('ambiguous-archive-member')
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
                result.extend(findings(decoded, location, raw=body, source=source))
                if body.startswith((b'\xff\xfe', b'\xfe\xff')):
                    result.extend(findings(presentation(body), location + '#utf16', raw=body, source=source))
                if member.filename.endswith('.xml'):
                    # Join Word runs to detect split literals, without entity/network expansion.
                    xml_decoded = presentation(body)
                    if '<!DOCTYPE' in xml_decoded or '<!ENTITY' in xml_decoded:
                        raise ValueError('xml')
                    element = ElementTree.fromstring(body)
                    result.extend(findings(''.join(element.itertext()), location + '#text', raw=body, source=source))
        expand(raw, label, 0)
    return result


def approved_exceptions(document, trusted_review_sha256=()):
    """Consume coordinator-pinned receipts; strings in an exception file confer no authority.

    Trust pins MUST come from protected coordinator/CI configuration, never from this
    document. No receipt/pin is generated by the scanner or proposal authoring driver.
    """
    if document == []:
        return []
    if not isinstance(document, dict) or set(document) != {'schema_version', 'proposals', 'receipts', 'exceptions'} or document['schema_version'] != IDENTITY_VERSION:
        raise ValueError('exceptions')
    proposals = {}; receipts = {}
    for proposal in document['proposals']:
        if set(proposal) != {'target', 'classification', 'rationale', 'evidence_sha256', 'author_reference'}:
            raise ValueError('proposal')
        target = proposal['target']
        if set(target) != set(TARGET_KEYS) or target['identity_version'] != IDENTITY_VERSION or target['rule'] not in RULES:
            raise ValueError('target')
        if not isinstance(target['path'], str) or not target['path'] or type(target['line']) is not int or target['line'] < 1 or type(target['occurrence']) is not int or target['occurrence'] < 0:
            raise ValueError('target')
        for key in ('context_sha256', 'fingerprint'):
            if not isinstance(target[key], str) or not re.fullmatch('[0-9a-f]{64}', target[key]):
                raise ValueError('target')
        if not re.fullmatch(r'git-blob-sha256:[0-9a-f]{64}', target['immutable_source_revision']):
            raise ValueError('source')
        if proposal['classification'] not in ('synthetic-fixture', 'formal-reference', 'public-example') or not isinstance(proposal['rationale'], str) or len(proposal['rationale']) < 40 or not proposal['author_reference'] or not re.fullmatch('[0-9a-f]{64}', proposal['evidence_sha256']):
            raise ValueError('proposal')
        proposals[object_hash(proposal)] = proposal
    for receipt in document['receipts']:
        receipts[object_hash(receipt)] = receipt
    accepted = []
    for selection in document['exceptions']:
        if set(selection) != {'proposal_sha256', 'review_sha256'} or selection['review_sha256'] not in trusted_review_sha256:
            raise ValueError('untrusted-review')
        proposal = proposals[selection['proposal_sha256']]
        receipt = receipts[selection['review_sha256']]
        if set(receipt) != {'schema_version', 'status', 'proposal_sha256', 'evidence_sha256', 'author_reference', 'reviewer_reference', 'target_sha256'}:
            raise ValueError('review')
        if (receipt['schema_version'] != IDENTITY_VERSION or receipt['status'] != 'independently-reviewed'
                or receipt['proposal_sha256'] != selection['proposal_sha256']
                or receipt['evidence_sha256'] != proposal['evidence_sha256']
                or receipt['author_reference'] != proposal['author_reference']
                or not receipt['reviewer_reference'] or receipt['reviewer_reference'] == receipt['author_reference']
                or receipt['target_sha256'] != object_hash(proposal['target'])):
            raise ValueError('review-binding')
        accepted.append(proposal['target'])
    return accepted


def scan(root, mode, bundle=None, exceptions=None, trusted_review_sha256=()):
    root = Path(root).resolve()
    if os.name == 'nt' and not str(root).startswith('\\\\?\\'):
        root = Path('\\\\?\\' + str(root))
    exemptions = [] if exceptions is None else json.loads(read_file(Path(exceptions), root))
    exemptions = approved_exceptions(exemptions, trusted_review_sha256)
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
    return covered, list({object_hash(f): f for f in remaining}.values())


def main(argv=None):
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[1])
    parser.add_argument('--mode', required=True, choices=['repository', 'client'])
    parser.add_argument('--bundle', default='frontend/dist')
    parser.add_argument('--exceptions', type=Path)
    parser.add_argument('--review-sha256', action='append', default=[], help='Exact receipt hash from protected coordinator configuration')
    args = parser.parse_args(argv)
    try:
        count, found = scan(args.root, args.mode, args.bundle, args.exceptions, args.review_sha256)
        for item in found:
            print(json.dumps({k: item[k] for k in ('path', 'rule', 'line')}, ensure_ascii=True))
        print(json.dumps({'mode': args.mode, 'covered_files': count, 'findings': len(found)}))
        return 1 if found else 0
    except Exception:
        print(json.dumps({'rule': 'required-input-or-coverage-failure'}))
        return 2


if __name__ == '__main__':
    sys.exit(main())
