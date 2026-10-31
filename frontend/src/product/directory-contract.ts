import type { components } from '../generated/api'

// Temporary DIR-WORKSPACE-01 additions, isolated until parent exports OpenAPI and regenerates aliases.
// The committed generated contract currently predates these fields and command schemas.
export type Session = components['schemas']['SessionDto'] & { canReadCompanies: boolean; canCreate: boolean }
export type Company = components['schemas']['CompanyDto'] & { version: number; canEdit: boolean }
export type Vessel = Omit<components['schemas']['VesselDto'], 'kind'> & { kind: 'Ship' | 'Tug'; version: number; canEdit: boolean }
export type Page<T> = Omit<components['schemas']['PageDtoOfCompanyDto'], 'items'> & { items: T[] }
export type DirectoryRecord = Company | Vessel
export type CompanyCommand = { code: string; name: string; expectedVersion?: number }
export type VesselCreateCommand = { code: string; name: string; kind: 'Ship' | 'Tug'; companyId: string }
