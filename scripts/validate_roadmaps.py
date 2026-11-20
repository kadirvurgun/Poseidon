"""DEV-117 / DEV-118 roadmap guard (22.01 paragraph:41, 78, 122-124; 18.01 rows 118-119).

Checks docs/compliance/*-roadmap.{json,md}: closed schema, a named owner role on every gap,
existing evidence paths, an explicit no-claim disclaimer and NO certification/approval claim
language. Approval fields may only be filled by a human edit; a roadmap marked APPROVED without
an approver and date is refused. No model or network calls.
"""
from __future__ import annotations

import json
from pathlib import Path
import re
import sys

ROOT = Path(__file__).resolve().parents[1]
DIRECTORY = ROOT / 'docs' / 'compliance'
SCHEMA = 'poseidon.compliance-roadmap.v1'
DISCLAIMER = 'Sertifika/onay iddiası değildir'
EXPECTED = {'DEV-117': 'pms-type-approval-roadmap', 'DEV-118': 'iso27001-27701-readiness-roadmap'}
STATES = {'DRAFT', 'IN_REVIEW', 'APPROVED'}
GAP_STATES = {'OPEN', 'IN_PROGRESS', 'EVIDENCE_AUTHORED_UNVERIFIED', 'CLOSED_HUMAN_VERIFIED'}
DATE = re.compile(r'\d{4}-\d{2}-\d{2}\Z')
# Claim language that asserts an award or conformity. Negated disclaimers are written without these words.
FORBIDDEN = [re.compile(p, re.IGNORECASE) for p in (
    r'\bcertified\b', r'\bapproved by (?:the )?class\b', r'\btype[- ]approved\b', r'\bclass[- ]approved\b',
    r'\bfully compliant\b', r'\bcompliant with iso\b', r'\biso(?:/iec)? ?2770?1 compliant\b',
    r'sertifikal[ıi]y[ıi]z', r'sertifikal[ıi]d[ıi]r', r'\bsertifikal[ıi]\b', r'onayl[ıi]d[ıi]r', r'uyumludur')]


def claims(text: str) -> list[str]:
    return sorted({m.group(0).lower() for pattern in FORBIDDEN for m in pattern.finditer(text)})


def validate_document(data: dict, markdown: str, root: Path = ROOT) -> list[str]:
    errors = []
    if data.get('schemaVersion') != SCHEMA: errors.append('schema_version')
    if data.get('id') not in EXPECTED: errors.append('unknown_id')
    if data.get('state') not in STATES: errors.append('state')
    if DISCLAIMER not in str(data.get('disclaimer', '')): errors.append('json_disclaimer_missing')
    if DISCLAIMER not in markdown: errors.append('markdown_disclaimer_missing')
    approval = data.get('approval') or {}
    if not approval.get('approverRole'): errors.append('approver_role_missing')
    approved = approval.get('approvedBy') is not None or approval.get('approvedAt') is not None
    if data.get('state') == 'APPROVED' and not (approval.get('approvedBy') and approval.get('approvedAt')): errors.append('approval_unrecorded')
    if approved and data.get('state') != 'APPROVED': errors.append('approval_state_mismatch')
    if approval.get('approvedAt') is not None and not DATE.match(str(approval['approvedAt'])[:10]): errors.append('approval_date')
    gaps = data.get('gaps')
    if not isinstance(gaps, list) or not gaps:
        errors.append('gaps_missing'); gaps = []
    ids = [g.get('id') for g in gaps if isinstance(g, dict)]
    if len(ids) != len(set(ids)) or len(ids) != len(gaps): errors.append('gap_ids')
    for gap in gaps:
        if not isinstance(gap, dict): continue
        gid = gap.get('id', '?')
        if not str(gap.get('ownerRole') or '').strip(): errors.append(f'{gid}:owner_missing')
        if not gap.get('area') or not gap.get('source'): errors.append(f'{gid}:area_or_source_missing')
        if gap.get('status') not in GAP_STATES: errors.append(f'{gid}:status')
        if gap.get('targetDate') is not None and not DATE.match(str(gap['targetDate'])): errors.append(f'{gid}:target_date')
        if not isinstance(gap.get('evidence'), list): errors.append(f'{gid}:evidence'); continue
        for link in gap['evidence']:
            if not isinstance(link, str) or (not link.startswith('https://') and not (root / link).exists()):
                errors.append(f'{gid}:evidence_unresolved')
        if gap.get('status') == 'CLOSED_HUMAN_VERIFIED' and not gap.get('verifiedBy'): errors.append(f'{gid}:closure_unrecorded')
        if gap.get('status') == 'EVIDENCE_AUTHORED_UNVERIFIED' and not gap['evidence']: errors.append(f'{gid}:evidence_missing')
    found = claims(json.dumps(data, ensure_ascii=False) + '\n' + markdown)
    errors.extend(f'claim_language:{c}' for c in found)
    return errors


def validate(directory: Path = DIRECTORY, root: Path = ROOT) -> dict:
    results = {}
    for ident, stem in EXPECTED.items():
        json_path, md_path = directory / f'{stem}.json', directory / f'{stem}.md'
        try:
            data = json.loads(json_path.read_text(encoding='utf-8'))
            markdown = md_path.read_text(encoding='utf-8')
        except (OSError, ValueError):
            results[ident] = ['unreadable']; continue
        errors = validate_document(data, markdown, root)
        if data.get('id') != ident: errors.append('id_file_mismatch')
        results[ident] = errors
    return {'valid': all(not e for e in results.values()), 'errors': results}


def main() -> int:
    result = validate()
    print(json.dumps(result, ensure_ascii=False, indent=2))
    return 0 if result['valid'] else 1


if __name__ == '__main__':
    sys.exit(main())
