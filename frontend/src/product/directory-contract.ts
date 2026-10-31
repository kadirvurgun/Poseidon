import type { components } from '../generated/api'

export type Session = components['schemas']['SessionDto']
export type Company = components['schemas']['CompanyDto']
export type Vessel = Omit<components['schemas']['VesselDto'], 'kind'> & { kind: 'Ship' | 'Tug' }
export type Page<T> = Omit<components['schemas']['PageDtoOfCompanyDto'], 'items'> & { items: T[] }
export type DirectoryRecord = Company | Vessel
export type CompanyCommand = components['schemas']['CompanyCreateCommand'] | components['schemas']['CompanyUpdateCommand']
export type VesselCommand = components['schemas']['VesselCreateCommand'] | components['schemas']['VesselUpdateCommand']
export type VesselCreateCommand = components['schemas']['VesselCreateCommand']
