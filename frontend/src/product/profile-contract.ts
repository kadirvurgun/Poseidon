import type { components } from '../generated/api'
// Additive authored contract; regenerate the actual schema at the combined gate.
export type ProfileContent = components['schemas']['RegulatoryDraftContent'] & {
  mainPropulsionKw: NumberValue; contractForConstructionDate: TextValue; constructionRecordReference: TextValue;
  constructionRecord: { id: string; revision: number; checksum: string } | null;
}
export type Profile = Omit<components['schemas']['RegulatoryDraftDto'], 'content'> & { content: ProfileContent }
export type ProfileCommand = Omit<components['schemas']['SaveRegulatoryDraft'], 'content'> & { content: ProfileContent }
type HistoryRow = components['schemas']['PageDtoOfRegulatoryRevisionDto']['items'][number]
export type ProfileHistory = Omit<components['schemas']['PageDtoOfRegulatoryRevisionDto'], 'items'> & { items: (Omit<HistoryRow, 'content'> & { content: ProfileContent })[] }
export type TextValue = components['schemas']['TextInput']
export type NumberValue = components['schemas']['NumberInput']

// Presentation metadata only. Server owns field validation, scope and draft lifecycle.
export const profileFields = [
  ['imo', 'text', 'IMO', 'IMO'], ['flag', 'text', 'Bayrak', 'Flag'],
  ['shipType', 'text', 'Gemi tipi', 'Ship type'], ['classSociety', 'text', 'Klas kuruluşu', 'Class society'],
  ['classNumber', 'text', 'Klas numarası', 'Class number'], ['gt', 'number', 'GT', 'GT'],
  ['nt', 'number', 'NT', 'NT'], ['dwt', 'number', 'DWT', 'DWT'], ['loa', 'number', 'Tam boy (m)', 'LOA (m)'],
  ['breadth', 'number', 'Genişlik (m)', 'Breadth (m)'], ['draft', 'number', 'Yaz su çekimi (m)', 'Summer draft (m)'],
  ['buildDate', 'text', 'İnşa tarihi (YYYY-AA-GG)', 'Build date (YYYY-MM-DD)'],
  ['mainPropulsionKw', 'number', 'Ana sevk gücü (kW)', 'Main propulsion power (kW)'],
  ['contractForConstructionDate', 'text', 'İnşa sözleşme tarihi (YYYY-AA-GG)', 'Construction contract date (YYYY-MM-DD)'],
  ['constructionRecordReference', 'text', 'Klas / inşa sözleşmesi kayıt referansı', 'Class / construction contract record reference'],
  ['tradingArea', 'text', 'Sefer bölgesi', 'Trading area'], ['cargoContext', 'text', 'Yük bağlamı', 'Cargo context'],
  ['declaration', 'text', 'Beyan / kaynak açıklaması', 'Declaration / source description'],
  ['sourceReference', 'text', 'Kaynak referansı', 'Source reference'],
  ['sourceObservedAt', 'text', 'Kaynak gözlem zamanı (saat dilimi ile)', 'Source observed at (with timezone)'],
] as const
