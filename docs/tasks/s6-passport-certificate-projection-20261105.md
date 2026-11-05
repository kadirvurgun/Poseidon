# S5 passport / S6 owning certificate projection

State: SOURCE_AUTHORED / CORE_SOURCE_INTEGRATED / UNVERIFIED. No canonical
S5 or S6 acceptance, runtime evidence or story points are awarded by this source.

Owner: root, branch `foundation/s6-certificate-foundation-20261105`. Dependency:
the S6 owning vessel certificate model, `CertificateReader.Visible` / `Dto`,
audited writer, EF migration and current read permission from DEV-043. This source
is integrated with backend core `c64c9cb4871ab030a099fb47d402b8e884c97a5a` via normal
merge `e3ccca4d33f45220bcb71e517f4b3136394eff85`. This is source integration, with
all model/contract/build/runtime checks still deferred to the later bundle gate.

## Source and acceptance criteria

Active baseline `poseidon-v46.3-20261030`; 18.01 backlog SHA-256
`09890162c4718867cf10397a08b2ff3d13dc44b1384e06b0be0c437d15df4674`:

- DEV-042 / T-S5-004 / Backlog row 43: current applicable requirements,
  certificates, evidence completeness, blockers and source freshness drilldown.
- DEV-043 / T-S6-001 / Backlog row 44: scoped certificate center with temporal
  states, source/issuer and distinct verification.
- 14.02 Data Dictionary SHA-256
  `3b30f8e779890b3d4291fa41d4d55502dbd2c876894bc5e7f6dee7fe2f3e4018`,
  rows 33–40 and 1708–1755 for center/renewal declarations; existing common
  certificate declaration metadata rows 1634–1657. FR-/TC-certificate-command-center-*
  and FR-/TC-vessel-compliance-passport-* apply to the shared owning source.
- Existing [S5 integration source contract](./s5-integration-experience-01.md)
  and [S5 phase report](../phases/S5-COMPLIANCE-FOUNDATION-20261104.md) retain
  their source, source-version, currentness and privacy limitations.

Authored criteria:

1. Consume actual owning current vessel revisions, never a parallel inventory or
   fabricated crew record. Return a stable 25-row page and independent HasMore.
2. Match original vessel/resource/revision/actual concurrency version/hash and
   current head again in the same fresh SQL statement as profile, evaluation and
   evidence visibility before and after the parent security-audit await.
3. Without certificate.read return UNAVAILABLE and no certificate rows. With it,
   return AUTHORIZED_PAGE_ONLY even for an empty page. Neither state establishes
   inventory completeness, an authoritative certificate catalog or legal validity.
4. Expose declared type, number, issuer, dates, temporal state, exact document
   revision/checksum and owning drilldown. Qualification stays REVIEW_REQUIRED.
   Overall passport freshness stays Unverified, and critical-count uncertainty
   remains null. Crew PII is outside this passport projection.
5. Strict client decoding pins all resource links, versions, dates, source/hash,
   file references, coverage and paging. Render calendar observation separately
   from verification, with responsive cards and shared actions. Passport next-page
   navigation considers certificate HasMore independently from graph paging.
6. An actual assigned Human's recorded assertion is displayed as its exact state
   and immutable event id/revision/version/time, with RECORDED_UNASSESSED and
   current/historical context separate from certificate REVIEW_REQUIRED. Scalar
   output omits actor/role/authority identifiers and rationale; complete owning
   DTOs remain server-side for ReturnedVisible and synchronous CurrentPolicies
   after the combined fresh SQL, before and after audit. Original row39 and
   renewal1754/1755 transition approval fields remain unresolved; no legal
   verification, completion or automatic supersession is inferred.

## Authored checks and limits

Real API/PostgreSQL sources cover owning current revisions and immutable old
hashes, read-side audit stability, independent bounded paging, and deterministic
permission/head changes during the parent audit await. Head changes use the actual
owning API; owned test-only PostgreSQL advisory locks synchronize the interleaving.
Component/contract sources cover current declaration display and drilldown,
certificate-only pagination, unavailable versus authorized-empty pages and forged
qualification, links, hashes, file pins, reference dates and coverage rejection.

All test/build/typecheck/lint/generated-contract/model/migration/browser/CI
execution remains DEFERRED until the actual larger PR gate. No runtime provider
or fixture was activated. This is a bounded declaration projection; it does not
close the S5 complete-critical-universe, manual RuleOverride, authoritative issuer
catalog, real human qualification or production retention/privacy-policy gates.
