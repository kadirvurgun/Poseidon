import { describe, expect, it } from 'vitest'
import { locales } from '../i18n'

// DEV-024 re-QA note 3: every reason code the server can place in a critical-state explanation
// (CriticalStatePolicy, VesselProfileFieldPolicy, ComplianceOnboardingPolicy, the applicability
// review and CurrentRegulatoryContext.Stale) has a human label in every locale, so the panel
// never falls back to showing a raw code as the only explanation.
const serverReasonCodes = [
  // Onboarding (ComplianceOnboardingPolicy)
  'PROFILE_MISSING', 'REQUIRED_DATA_MISSING', 'REQUIRED_DATA_UNKNOWN', 'VERIFICATION_NOT_ESTABLISHED',
  // DEV-020 freshness summary and per-field reasons (VesselProfileFieldPolicy)
  'NO_PROFILE_FIELDS', 'FIELDS_UNAVAILABLE', 'FIELDS_STALE', 'FIELDS_UNKNOWN', 'ALL_FIELDS_WITHIN_POLICY',
  'VALUE_NOT_RECORDED', 'VALUE_UNKNOWN', 'FIELD_VERSION_UNAVAILABLE', 'SOURCE_NOT_DECLARED', 'SOURCE_DECLARED_FOR_PRIOR_VALUE',
  'SOURCE_UNKNOWN', 'SOURCE_REFERENCE_NOT_RECORDED', 'OBSERVED_AT_UNKNOWN', 'OBSERVED_AT_NOT_RECORDED', 'OBSERVED_AT_AFTER_CHECK',
  'FRESHNESS_POLICY_UNAVAILABLE', 'FRESHNESS_POLICY_UNCONFIGURED', 'WITHIN_POLICY_MAX_AGE', 'POLICY_MAX_AGE_EXCEEDED',
  // Applicability review and stale lineage
  'QUALIFIED_EVALUATION_UNAVAILABLE', 'EVALUATION_UNAVAILABLE', 'QUALIFIED_CATALOG_UNAVAILABLE', 'APPLICABILITY_REVIEW_REQUIRED',
  'CIRCULAR_INPUT_LINEAGE', 'QUALIFICATION_LINEAGE_UNAVAILABLE', 'ATTESTATION_AUTHORITY_CHANGED', 'ATTESTATION_EXPIRED',
  'CONSTRUCTION_RECORD_CHANGED', 'INPUT_STALE', 'OWNING_INPUT_CHANGED', 'PROFILE_CHANGED', 'QUALIFICATION_CHANGED', 'RULE_CHANGED',
  'SOURCE_NOT_CURRENT', 'SOURCE_REVIEW_REQUIRED', 'VESSEL_CHANGED',
]

type Catalog = { copies: { CriticalStatePanel_tsx_copy: { reasons: Record<string, string> } } }
const catalogs = import.meta.glob<{ default: Catalog }>('../i18n/messages/*.json', { eager: true })

describe('critical-state reason labels', () => {
  it.each(locales)('labels every server reason code in %s', locale => {
    const reasons = catalogs[`../i18n/messages/${locale}.json`]!.default.copies.CriticalStatePanel_tsx_copy.reasons
    const unlabeled = serverReasonCodes.filter(code => !reasons[code]?.trim() || reasons[code] === code)
    expect(unlabeled).toEqual([])
  })

  it('never labels a reason as a positive ready, verified or compliant outcome', () => {
    const en = catalogs['../i18n/messages/en.json']!.default.copies.CriticalStatePanel_tsx_copy.reasons
    for (const code of serverReasonCodes) expect(en[code]).not.toMatch(/\b(ready|compliant)\b|^verified/i)
  })
})
