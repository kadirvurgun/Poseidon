# S0-THREATS-01

Source: 18.01 Backlog row7 DEV-006 / T-S0-006; docs/security-baseline.md

Prepare docs/security/threat-model.md from docs/security-baseline.md and the merged s0 ADRs. DEV-006 requires tenant isolation, authn/authz, file upload, protected reporting, secrets, audit, offline devices and AI prompt/data threats. Cover each with concrete abuse scenarios, current controls versus gaps, verification and responsible role; map P0 mitigations to actual DEV IDs obtained by bounded scripts/query_sources.py. No invented implementation, retention durations, production hosting or compliance approval. Call out EF write audit versus HTTP read/security auditing and bootstrap/admin context boundaries. Include log redaction, source documents treated as data, tenant-bound retrieval and no AI authority over safety. This is draft engineering evidence until authorized security review. No source-pack edits, SP acceptance or runtime changes.

Acceptance:
- Each required threat area has assets, trust boundary, abuse case, mitigation, verification and owner
- P0 mitigation table maps real backlog IDs and explicitly records unimplemented controls
- No GDPR/KVKK compliance or expert approval claimed
