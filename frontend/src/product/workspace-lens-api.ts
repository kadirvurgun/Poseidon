import { sourceText } from "../i18n/messages";
import { ApiError, request } from './api';
import { isGuid } from './identifiers';
export type LensItem = {
    key: string;
    href: string;
    group: 'daily' | 'review';
};
export type Lens = {
    reference: string;
    tr: string;
    en: string;
    items: LensItem[];
};
export type Lenses = {
    tenantId: string;
    actorId: string;
    vesselId: string | null;
    mode: 'Fleet' | 'Ship' | 'Tug';
    state: 'Configured' | 'Missing' | 'Unavailable';
    version: string | null;
    lenses: Lens[];
    canSearch: boolean;
};
export const lensLabels: Record<string, readonly [
    string,
    string
]> = {
    'operational-downtime': [sourceText("tr", "pairs.m_0208af14c2c3282e"), sourceText("en", "pairs.m_0208af14c2c3282e")],
    'risk-assessment': [sourceText("tr", "pairs.m_2ba46e791f65fb5f"), sourceText("en", "pairs.m_2ba46e791f65fb5f")],
    'risk-template-library': [sourceText("tr", "pairs.m_c086860d120fa851"), sourceText("en", "pairs.m_c086860d120fa851")],
    'departure-signoff': [sourceText("tr", "pairs.m_9d2a4c6e8f0b1d35"), sourceText("en", "pairs.m_9d2a4c6e8f0b1d35")],
    'regulatory-review': [sourceText("tr", "pairs.m_3c8e1f5a7b2d9e40"), sourceText("en", "pairs.m_3c8e1f5a7b2d9e40")],
    'pms-command-center': [sourceText("tr", "pairs.m_8c3e1f0a5d2b7c41"), sourceText("en", "pairs.m_8c3e1f0a5d2b7c41")],
    'service-engineer-report': [sourceText("tr", "pairs.m_8c3e1f0a5d2b7c42"), sourceText("en", "pairs.m_8c3e1f0a5d2b7c42")],
    'safety-events': [sourceText("tr", "pairs.m_8a32195b2423b0ae"), sourceText("en", "pairs.m_8a32195b2423b0ae")],
    'data-trust-center': [sourceText("tr", "pairs.m_a51d0c7e3f9b2481"), sourceText("en", "pairs.m_a51d0c7e3f9b2481")],
    'industry-assurance': [sourceText("tr", "pairs.m_b62e1f8d4a0c3592"), sourceText("en", "pairs.m_b62e1f8d4a0c3592")],
    'voice-event-draft': [sourceText("tr", "pairs.m_9a7c51e0d3b2f461"), sourceText("en", "pairs.m_9a7c51e0d3b2f461")],
    'document-compare': [sourceText("tr", "pairs.m_9a7c51e0d3b2f462"), sourceText("en", "pairs.m_9a7c51e0d3b2f462")],
    'role-uat': [sourceText("tr", "pairs.m_5b1e7c9d3a2f4e60"), sourceText("en", "pairs.m_5b1e7c9d3a2f4e60")],
    'pilot-gate': [sourceText("tr", "pairs.m_5b1e7c9d3a2f4e61"), sourceText("en", "pairs.m_5b1e7c9d3a2f4e61")],
    'pilot-program': [sourceText("tr", "pairs.m_5b1e7c9d3a2f4e62"), sourceText("en", "pairs.m_5b1e7c9d3a2f4e62")],
    'high-risk-work': [sourceText("tr", "pairs.m_04076d4cdaa5ec7d"), sourceText("en", "pairs.m_04076d4cdaa5ec7d")],
    'work-order-center': [sourceText("tr", "pairs.m_8b20599d8e3aae73"), sourceText("en", "pairs.m_8b20599d8e3aae73")],
    'critical-spares': [sourceText("tr", "pairs.m_5e7a1c0d2b4f9b10"), sourceText("en", "pairs.m_5e7a1c0d2b4f9b10")],
    defects: [sourceText("tr", "pairs.m_e3d6ff22743649dc"), sourceText("en", "pairs.m_e3d6ff22743649dc")],
    'fuel-bunker-log': [sourceText("tr", "pairs.m_4409cb4e233ec268"), sourceText("en", "pairs.m_4409cb4e233ec268")], 'port-activity-sof': [sourceText("tr", "pairs.m_13875df296e09add"), sourceText("en", "pairs.m_13875df296e09add")],
    'certificate-command-center': [sourceText("tr", "pairs.m_49dd4df69d1c0e57"), sourceText("en", "pairs.m_49dd4df69d1c0e57")], 'renewal-workflow': [sourceText("tr", "pairs.m_0e4b1a01b223bd75"), sourceText("en", "pairs.m_0e4b1a01b223bd75")],
    'pre-survey-checklist': [sourceText("tr", "pairs.m_5e7a1c0d2b4f9a01"), sourceText("en", "pairs.m_5e7a1c0d2b4f9a01")], 'survey-planner': [sourceText("tr", "pairs.m_5e7a1c0d2b4f9a02"), sourceText("en", "pairs.m_5e7a1c0d2b4f9a02")],
    'noon-report': [sourceText("tr", "pairs.m_ff485806c57476ca"), sourceText("en", "pairs.m_ff485806c57476ca")], 'deck-daily-log': [sourceText("tr", "pairs.m_75d4c27ced71efda"), sourceText("en", "pairs.m_75d4c27ced71efda")], 'engine-daily-log': [sourceText("tr", "pairs.m_21eb6ede9f2b37f9"), sourceText("en", "pairs.m_21eb6ede9f2b37f9")],
    today: [sourceText("tr", "pairs.m_14d5741b2e6e2585"), sourceText("en", "pairs.m_14d5741b2e6e2585")], fleet: [sourceText("tr", "pairs.m_624c9bbf6ccfa3dd"), sourceText("en", "pairs.m_624c9bbf6ccfa3dd")], companies: [sourceText("tr", "pairs.m_3ff4996c5348b429"), sourceText("en", "pairs.m_3ff4996c5348b429")], crew: [sourceText("tr", "pairs.m_9c7ff80bf1b60400"), sourceText("en", "pairs.m_9c7ff80bf1b60400")],
    equipment: [sourceText("tr", "pairs.m_77e9d3d4c2e4b44f"), sourceText("en", "pairs.m_77e9d3d4c2e4b44f")], maintenance: [sourceText("tr", "pairs.m_21f68c2e5c640595"), sourceText("en", "pairs.m_21f68c2e5c640595")], import: [sourceText("tr", "pairs.m_f1779f35acec39d7"), sourceText("en", "pairs.m_f1779f35acec39d7")],
    'data-reconciliation': [sourceText("tr", "pairs.m_e5831d38d264db25"), sourceText("en", "pairs.m_e5831d38d264db25")],
    audit: [sourceText("tr", "pairs.m_9f8662ba28b5b4fc"), sourceText("en", "pairs.m_9f8662ba28b5b4fc")],
    'audit-ready': [sourceText("tr", "pairs.m_372c48ba7e84c124"), sourceText("en", "pairs.m_372c48ba7e84c124")], sources: [sourceText("tr", "pairs.m_d52797a6e362de20"), sourceText("en", "pairs.m_d52797a6e362de20")], requirements: [sourceText("tr", "pairs.m_d6ad4bb19c60b2b2"), sourceText("en", "pairs.m_d6ad4bb19c60b2b2")],
    'crew-certificates': [sourceText("tr", "pairs.m_57a1c0ce00000001"), sourceText("en", "pairs.m_57a1c0ce00000001")], 'crew-competence': [sourceText("tr", "pairs.m_57a1c0ce00000002"), sourceText("en", "pairs.m_57a1c0ce00000002")], 'safe-manning': [sourceText("tr", "pairs.m_57a1c0ce00000003"), sourceText("en", "pairs.m_57a1c0ce00000003")],
    'tug-command': [sourceText("tr", "pairs.m_21739423fc15322f"), sourceText("en", "pairs.m_21739423fc15322f")], 'tug-job-board': [sourceText("tr", "pairs.m_326009e7c4fb72c6"), sourceText("en", "pairs.m_326009e7c4fb72c6")], 'tug-job-intake': [sourceText("tr", "pairs.m_967bfbc16e73959c"), sourceText("en", "pairs.m_967bfbc16e73959c")],
    'tug-assignment': [sourceText("tr", "pairs.m_e933a2eb7a19d5c1"), sourceText("en", "pairs.m_e933a2eb7a19d5c1")],
    'tug-readiness': [sourceText("tr", "pairs.m_ea1a685b360fc1b6"), sourceText("en", "pairs.m_ea1a685b360fc1b6")],
    'tug-equipment-readiness': [sourceText("tr", "pairs.m_5151864934be8fa1"), sourceText("en", "pairs.m_5151864934be8fa1")],
    'tug-crew-readiness': [sourceText("tr", "pairs.m_13bb4091a158976a"), sourceText("en", "pairs.m_13bb4091a158976a")],
    'towage-plan': [sourceText("tr", "pairs.m_378130a9c426194c"), sourceText("en", "pairs.m_378130a9c426194c")],
    'towage-execution': [sourceText("tr", "pairs.m_a7ff6a594ad31c81"), sourceText("en", "pairs.m_a7ff6a594ad31c81")],
    'tug-daily-log': [sourceText("tr", "pairs.m_073dee70df0d133e"), sourceText("en", "pairs.m_073dee70df0d133e")],
    'tug-operation-history': [sourceText("tr", "pairs.m_8c8392446918f0da"), sourceText("en", "pairs.m_8c8392446918f0da")],
    'tug-operations-offline': [sourceText("tr", "pairs.m_6045fea6f2dedb7c"), sourceText("en", "pairs.m_6045fea6f2dedb7c")],
    'tug-demo-journey': [sourceText("tr", "pairs.m_c358e78b32f41ae3"), sourceText("en", "pairs.m_c358e78b32f41ae3")],
    'drydock-planning': [sourceText("tr", "pairs.m_7a31428311abb402"), sourceText("en", "pairs.m_7a31428311abb402")],
    'towage-risk-briefing': [sourceText("tr", "pairs.m_b75bd8b78f08cd52"), sourceText("en", "pairs.m_b75bd8b78f08cd52")],
};
const root: Record<string, string> = { crew: 'crew-roster', 'crew-certificates': 'crew-certificates', 'crew-competence': 'training-position-matrix', 'safe-manning': 'safe-manning', equipment: 'equipment-hierarchy', maintenance: 'maintenance-plan', import: 'excel-migration', 'data-reconciliation': 'data-reconciliation' };
export function lensHref(key: string, vessel: string | null) {
    if (!Object.hasOwn(lensLabels, key))
        return null;
    if (['audit-ready', 'operational-downtime', 'risk-assessment', 'pms-command-center', 'departure-signoff', 'regulatory-review', 'service-engineer-report', 'voice-event-draft', 'document-compare', 'work-order-center', 'defects', 'fuel-bunker-log', 'port-activity-sof', 'noon-report', 'deck-daily-log', 'engine-daily-log', 'certificate-command-center', 'renewal-workflow', 'pre-survey-checklist'].includes(key))
        return vessel ? `/app/${key}/${vessel}` : `/app/${key}`;
    if (Object.hasOwn(root, key))
        return vessel ? `/app/${root[key]}/${vessel}` : null;
    return `/app/${key}${vessel && (key.startsWith('tug-') || key.startsWith('towage-') || key === 'drydock-planning') ? `?vessel=${vessel}` : vessel && key === 'today' ? `/${vessel}` : ''}`;
}
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v);
const text = (v: unknown, max: number): v is string => typeof v === 'string' && v.length > 0 && v.length <= max && v.trim() === v && !/\p{Cc}/u.test(v);
function invalid(): never { throw new ApiError(502, 'invalid_navigation'); }
const lensesKeys = ['tenantId', 'actorId', 'vesselId', 'mode', 'state', 'version', 'lenses', 'canSearch'];
const closed = (v: Record<string, unknown>, keys: string[]) => Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k));
export function decodeLenses(value: unknown, tenant: string, actor: string, vessel: string | null): Lenses {
    // DEV-022: closed shapes at every level; a hidden count or denied-item list is rejected, never ignored.
    if (!isGuid(tenant) || !isGuid(actor) || vessel !== null && !isGuid(vessel) || !object(value) || !closed(value, lensesKeys) ||
        value.tenantId !== tenant || value.actorId !== actor || value.vesselId !== vessel ||
        typeof value.mode !== 'string' || !['Fleet', 'Ship', 'Tug'].includes(value.mode) || (vessel === null) !== (value.mode === 'Fleet') ||
        typeof value.state !== 'string' || !['Configured', 'Missing', 'Unavailable'].includes(value.state) || typeof value.canSearch !== 'boolean' ||
        !Array.isArray(value.lenses) || value.lenses.length > 16 ||
        (value.state === 'Configured' ? !text(value.version, 64) : value.version !== null || value.lenses.length !== 0))
        invalid();
    const lenses: Lens[] = [];
    for (const row of value.lenses) {
        if (!object(row) || !closed(row, ['reference', 'tr', 'en', 'items']) || !isGuid(row.reference) || lenses.some(x => x.reference === String(row.reference).toLowerCase()) ||
            !text(row.tr, 200) || !text(row.en, 200) || !Array.isArray(row.items) || row.items.length > 20)
            invalid();
        const items: LensItem[] = [];
        for (const item of row.items) {
            if (!object(item) || !closed(item, ['key', 'href', 'group']) || typeof item.key !== 'string' || !Object.hasOwn(lensLabels, item.key) || typeof item.href !== 'string' || items.some(x => x.key === item.key) ||
                item.href !== lensHref(item.key, vessel) || (item.group !== 'daily' && item.group !== 'review') ||
                item.group !== (['audit', 'audit-ready', 'sources', 'requirements', 'import', 'data-reconciliation', 'certificate-command-center', 'renewal-workflow', 'risk-template-library', 'high-risk-work', 'crew-certificates', 'pre-survey-checklist', 'crew-competence', 'survey-planner', 'safe-manning'].includes(item.key) ? 'review' : 'daily') ||
                (value.mode === 'Ship' && (item.key.startsWith('tug-') || item.key.startsWith('towage-'))) || (value.mode === 'Tug' && ['fuel-bunker-log', 'port-activity-sof', 'operational-downtime'].includes(item.key)))
                invalid();
            items.push({ key: item.key, href: item.href as string, group: item.group });
        }
        lenses.push({ reference: row.reference.toLowerCase(), tr: row.tr, en: row.en, items });
    }
    return { tenantId: tenant, actorId: actor, vesselId: vessel, mode: value.mode as Lenses['mode'], state: value.state as Lenses['state'], version: value.version as string | null, canSearch: value.canSearch, lenses };
}
export async function loadLenses(tenant: string, actor: string, vessel: string | null, signal: AbortSignal) {
    return decodeLenses(await request<unknown>('workspace/lenses' + (vessel ? '/' + encodeURIComponent(vessel) : ''), signal), tenant, actor, vessel);
}
