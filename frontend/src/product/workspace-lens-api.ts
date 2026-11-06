import { ApiError, request } from './api'
import { isGuid } from './identifiers'

export type LensItem = { key: string; href: string; group: 'daily' | 'review' }
export type Lens = { reference: string; tr: string; en: string; items: LensItem[] }
export type Lenses = { tenantId: string; actorId: string; vesselId: string | null; mode: 'Fleet' | 'Ship' | 'Tug'; state: 'Configured' | 'Missing' | 'Unavailable'; version: string | null; lenses: Lens[]; canSearch: boolean }
export const lensLabels: Record<string, readonly [string, string]> = {
  defects: ['Arızalar ve önlemler', 'Defects and safeguards'],
  'fuel-bunker-log': ['Yakıt, ikmal ve ROB', 'Fuel, bunker and ROB'], 'port-activity-sof': ['Liman faaliyeti ve SOF', 'Port activity and SOF'],
  'certificate-command-center': ['Sertifika merkezi', 'Certificate center'], 'renewal-workflow': ['Sertifika yenileme', 'Certificate renewal'],
  'noon-report': ['Öğle raporu', 'Noon report'], 'deck-daily-log': ['Güverte günlük kaydı', 'Deck daily log'], 'engine-daily-log': ['Makine günlük kaydı', 'Engine daily log'],
  today: ['Bugün', 'Today'], fleet: ['Filo', 'Fleet'], companies: ['Şirketler', 'Companies'], crew: ['Mürettebat', 'Crew'],
  equipment: ['Ekipman', 'Equipment'], maintenance: ['Bakım planları', 'Maintenance plans'], import: ['Kontrollü içe aktarma', 'Controlled import'],
  audit: ['İşlem geçmişi', 'Action history'], sources: ['Kaynak taslakları', 'Source drafts'], requirements: ['Gereksinim taslakları', 'Requirement drafts'],
  'tug-command': ['Römorkör bağlamı', 'Tug context'], 'tug-job-board': ['Römorkör iş panosu', 'Tug job board'], 'tug-job-intake': ['Römorkör iş taslağı', 'Tug job draft'],
}
const root: Record<string, string> = { crew: 'crew-roster', equipment: 'equipment-hierarchy', maintenance: 'maintenance-plan', import: 'excel-migration' }
export function lensHref(key: string, vessel: string | null) {
  if (!Object.hasOwn(lensLabels, key)) return null
  if (['defects', 'fuel-bunker-log', 'port-activity-sof', 'noon-report', 'deck-daily-log', 'engine-daily-log', 'certificate-command-center', 'renewal-workflow'].includes(key)) return vessel ? `/app/${key}/${vessel}` : `/app/${key}`
  if (Object.hasOwn(root, key)) return vessel ? `/app/${root[key]}/${vessel}` : null
  return `/app/${key}${vessel && key.startsWith('tug-') ? `?vessel=${vessel}` : vessel && key === 'today' ? `/${vessel}` : ''}`
}
const object = (v: unknown): v is Record<string, unknown> => !!v && typeof v === 'object' && !Array.isArray(v)
const text = (v: unknown, max: number): v is string => typeof v === 'string' && v.length > 0 && v.length <= max && v.trim() === v && !/\p{Cc}/u.test(v)
function invalid(): never { throw new ApiError(502, 'invalid_navigation') }
export function decodeLenses(value: unknown, tenant: string, actor: string, vessel: string | null): Lenses {
  if (!isGuid(tenant) || !isGuid(actor) || vessel !== null && !isGuid(vessel) || !object(value) ||
    value.tenantId !== tenant || value.actorId !== actor || value.vesselId !== vessel ||
    typeof value.mode !== 'string' || !['Fleet', 'Ship', 'Tug'].includes(value.mode) || (vessel === null) !== (value.mode === 'Fleet') ||
    typeof value.state !== 'string' || !['Configured', 'Missing', 'Unavailable'].includes(value.state) || typeof value.canSearch !== 'boolean' ||
    !Array.isArray(value.lenses) || value.lenses.length > 16 ||
    (value.state === 'Configured' ? !text(value.version, 64) : value.version !== null || value.lenses.length !== 0)) invalid()
  const lenses: Lens[] = []
  for (const row of value.lenses) {
    if (!object(row) || !isGuid(row.reference) || lenses.some(x => x.reference === String(row.reference).toLowerCase()) ||
      !text(row.tr, 200) || !text(row.en, 200) || !Array.isArray(row.items) || row.items.length > 20) invalid()
    const items: LensItem[] = []
    for (const item of row.items) {
      if (!object(item) || typeof item.key !== 'string' || !Object.hasOwn(lensLabels, item.key) || typeof item.href !== 'string' || items.some(x => x.key === item.key) ||
        item.href !== lensHref(item.key, vessel) || (item.group !== 'daily' && item.group !== 'review') ||
        item.group !== (['audit', 'sources', 'requirements', 'import', 'certificate-command-center', 'renewal-workflow'].includes(item.key) ? 'review' : 'daily') ||
        (value.mode === 'Ship' && item.key.startsWith('tug-')) || (value.mode === 'Tug' && ['fuel-bunker-log', 'port-activity-sof'].includes(item.key))) invalid()
      items.push({ key: item.key, href: item.href as string, group: item.group })
    }
    lenses.push({ reference: row.reference.toLowerCase(), tr: row.tr, en: row.en, items })
  }
  return { tenantId: tenant, actorId: actor, vesselId: vessel, mode: value.mode as Lenses['mode'], state: value.state as Lenses['state'], version: value.version as string | null, canSearch: value.canSearch, lenses }
}
export async function loadLenses(tenant: string, actor: string, vessel: string | null, signal: AbortSignal) {
  return decodeLenses(await request<unknown>('workspace/lenses' + (vessel ? '/' + encodeURIComponent(vessel) : ''), signal), tenant, actor, vessel)
}
