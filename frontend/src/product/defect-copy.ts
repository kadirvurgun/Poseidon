import { localizedTuple, sourceText } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
import type { DefectTemporalState } from './defect-api';
export const defectTemporalLabel = (state: DefectTemporalState, en: boolean) => localizedTuple(({
    MEASURE_MISSING: [sourceText("en", "pairs.m_f7c194a2c0f6875f"), sourceText("tr", "pairs.m_f7c194a2c0f6875f")], MEASURE_UNKNOWN: [sourceText("en", "pairs.m_4f91331e8acb1fcf"), sourceText("tr", "pairs.m_4f91331e8acb1fcf")],
    DEADLINE_MISSING: [sourceText("en", "pairs.m_938be29689896ca3"), sourceText("tr", "pairs.m_938be29689896ca3")], DEADLINE_UNKNOWN: [sourceText("en", "pairs.m_3e54344fbbbeb3ae"), sourceText("tr", "pairs.m_3e54344fbbbeb3ae")],
    EXPIRED_DECLARED: [sourceText("en", "pairs.m_da44fd2a0dfe4a06"), sourceText("tr", "pairs.m_da44fd2a0dfe4a06")], NOT_DUE_DECLARED: [sourceText("en", "pairs.m_3cdc4a80a1f0b4b5"), sourceText("tr", "pairs.m_3cdc4a80a1f0b4b5")],
})[state], en, 0);
export function defectBlockerLabel(code: string, en: boolean) {
    const labels: Record<string, [
        string,
        string
    ]> = { UNVERIFIED_DECLARATION: [sourceText("en", "pairs.m_35ec82d4a02a33ea"), sourceText("tr", "pairs.m_35ec82d4a02a33ea")], CRITICALITY_NOT_ASSESSED: [sourceText("en", "pairs.m_160430aa5ff5ac88"), sourceText("tr", "pairs.m_160430aa5ff5ac88")], SAFEGUARD_NOT_VERIFIED: [sourceText("en", "pairs.m_5b5ed17d1d06a722"), sourceText("tr", "pairs.m_5b5ed17d1d06a722")], RESTRICTION_NOT_AUTHORIZED: [sourceText("en", "pairs.m_afabef78d04f51a9"), sourceText("tr", "pairs.m_afabef78d04f51a9")], CLOSURE_POLICY_UNAVAILABLE: [sourceText("en", "pairs.m_6ff1f731948dd708"), sourceText("tr", "pairs.m_6ff1f731948dd708")], EFFECTIVENESS_NOT_ASSESSED: [sourceText("en", "pairs.m_b067915af85f8d90"), sourceText("tr", "pairs.m_b067915af85f8d90")] };
    return localizedTuple(labels[code], en, 0) ?? (localizedText(en, "pairs.m_c27c5c9abfafcba5"));
}
