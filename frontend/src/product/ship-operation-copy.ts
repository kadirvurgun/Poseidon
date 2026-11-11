import { codeOptionLabel, type PresentationLabel } from '../i18n/labels';
import { sourceText, localizedTuple } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
const options: Readonly<Record<string, PresentationLabel>> = {
    Consumption: 'consumption', 'Delivery/BDN': 'deliveryBdn', Transfer: 'transfer', 'Tank Sounding': 'tankSounding',
    Methanol: 'methanol', Other: 'other', NONE: 'differenceNone', ATTENTION: 'differenceAttention', DISPUTED: 'differenceDisputed',
    Arrival: 'arrival', NOR: 'norRecord', 'Pilot Onboard': 'pilotOnboard', 'Tug Made Fast': 'tugMadeFast',
    Berthing: 'berthing', Loading: 'loading', Discharging: 'discharging', Shifting: 'shifting', 'Tug Let Go': 'tugLetGo', 'Pilot Off': 'pilotOff', Departure: 'departure',
};
export const shipOpsOptionLabel = (value: string, en: boolean) => codeOptionLabel(value, en, options);
export const shipOpsFactLabel = (state: 'Known' | 'Missing' | 'Unknown', en: boolean) => state === 'Known' ? (localizedText(en, "pairs.m_c2e9e0b3ab9f0e8c")) : state === 'Missing' ? (localizedText(en, "pairs.m_a5bd35d90de7c160")) : (localizedText(en, "pairs.m_a5a7675bd7000dbd"));
export function shipOpsBlockerLabel(code: string, en: boolean) {
    const labels: Record<string, [
        string,
        string
    ]> = {
        REQUIRED_DATA_MISSING: [sourceText("en", "pairs.m_50c07e2db327e761"), sourceText("tr", "pairs.m_50c07e2db327e761")],
        REQUIRED_DATA_UNKNOWN: [sourceText("en", "pairs.m_903a570085da30f9"), sourceText("tr", "pairs.m_903a570085da30f9")],
        SOURCE_CONDITIONAL_UNRESOLVED: [sourceText("en", "pairs.m_6f91f1809784ca0f"), sourceText("tr", "pairs.m_6f91f1809784ca0f")],
        REFERENCE_UNAVAILABLE: [sourceText("en", "pairs.m_2e9195f254010743"), sourceText("tr", "pairs.m_2e9195f254010743")],
        HUMAN_REVIEW_REQUIRED: [sourceText("en", "pairs.m_db5329ad6a19795d"), sourceText("tr", "pairs.m_db5329ad6a19795d")],
        VERIFICATION_NOT_ESTABLISHED: [sourceText("en", "pairs.m_1fe9829b56f1be11"), sourceText("tr", "pairs.m_1fe9829b56f1be11")],
    };
    return localizedTuple(labels[code], en, 0) ?? (localizedText(en, "pairs.m_c27c5c9abfafcba5"));
}
