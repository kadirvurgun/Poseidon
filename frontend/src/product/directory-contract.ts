import type { components } from '../generated/api'

// USER-ADMIN-02: invitationSession marks an invitation-only sign-in (no membership); absent is treated as false.
export type Session = Omit<components['schemas']['SessionDto'], 'invitationSession'> & { invitationSession?: boolean }
export type Company = components['schemas']['CompanyDto']
export type Vessel = Omit<components['schemas']['VesselDto'], 'kind'> & { kind: 'Ship' | 'Tug' }
export type Page<T> = Omit<components['schemas']['PageDtoOfCompanyDto'], 'items'> & { items: T[] }
export type DirectoryRecord = Company | Vessel
export type CompanyCommand = components['schemas']['CompanyCreateCommand'] | components['schemas']['CompanyUpdateCommand']
export type VesselCommand = components['schemas']['VesselCreateCommand'] | components['schemas']['VesselUpdateCommand']
export type VesselCreateCommand = components['schemas']['VesselCreateCommand']
