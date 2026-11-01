import type { components } from '../generated/api'
export type Profile = components['schemas']['RegulatoryDraftDto']
export type ProfileContent = components['schemas']['RegulatoryDraftContent']
export type ProfileCommand = components['schemas']['SaveRegulatoryDraft']
export type ProfileHistory = components['schemas']['PageDtoOfRegulatoryRevisionDto']
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
  ['tradingArea', 'text', 'Sefer bölgesi', 'Trading area'], ['cargoContext', 'text', 'Yük bağlamı', 'Cargo context'],
  ['declaration', 'text', 'Beyan / kaynak açıklaması', 'Declaration / source description'],
  ['sourceReference', 'text', 'Kaynak referansı', 'Source reference'],
  ['sourceObservedAt', 'text', 'Kaynak gözlem zamanı (saat dilimi ile)', 'Source observed at (with timezone)'],
] as const
