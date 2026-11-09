import { sourceText, localizedTuple } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
const options: Record<string, string> = {
    Consumption: 'Tüketim', 'Delivery/BDN': 'İkmal / BDN', Transfer: 'Transfer', ROB: 'ROB', 'Tank Sounding': 'Tank ölçümü',
    VLSFO: 'VLSFO', HFO: 'HFO', MGO: 'MGO', LNG: 'LNG', Methanol: 'Metanol', Other: 'Diğer',
    NONE: 'Fark beyan edilmedi', ATTENTION: 'Dikkat gerektiren beyan', DISPUTED: 'İhtilaf beyanı',
    Arrival: 'Varış', NOR: 'NOR kaydı', 'Pilot Onboard': 'Kılavuzun gemiye çıkışı', 'Tug Made Fast': 'Römorkör bağlantısı',
    Berthing: 'Yanaşma', Loading: 'Yükleme', Discharging: 'Tahliye', Shifting: 'Yer değiştirme', 'Tug Let Go': 'Römorkör bağlantısının bırakılması', 'Pilot Off': 'Kılavuzun ayrılması', Departure: 'Ayrılış',
};
export const shipOpsOptionLabel = (value: string, en: boolean) => en ? value : options[value] ?? value;
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
