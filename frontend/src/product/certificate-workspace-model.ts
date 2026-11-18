import type { CertificateDto, RenewalDto } from './certificates-api'
import type { CertificateCapabilities, CertificateHistory, CertificatePage, RenewalCapabilities, RenewalPage } from './certificates-api'
import type { Session, Vessel } from './api'
import type { CertificateRenewalContent, VesselCertificateContent } from './certificate-content'

export type CertificateEditor = { kind: 'certificate'; owner: string; id: string; record?: CertificateDto; content: VesselCertificateContent; dirty: boolean }
export type RenewalEditor = { kind: 'renewal'; owner: string; id: string; record?: RenewalDto; certificate: CertificateDto; content: CertificateRenewalContent; dirty: boolean }
export type CertificateWorkflowEditor = CertificateEditor | RenewalEditor
export type CertificateWorkspaceSource = { scope: string; user?: Session; caps?: CertificateCapabilities; renewalCaps?: RenewalCapabilities | null; certificate?: CertificateDto; certificateHistory?: CertificateHistory; center?: CertificatePage; renewal?: RenewalDto; renewalPage?: RenewalPage; renewalHistory?: RenewalPage; vessels?: Vessel[]; hasMore?: boolean; failed?: boolean }
