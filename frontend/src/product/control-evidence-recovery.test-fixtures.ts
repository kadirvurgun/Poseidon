// Deterministic UI/wire fixtures only, never production or actual host evidence.
import { blank, type Command, type Kind, type Row } from './control-evidence-api'
import { reviewAnchor, reviewSession } from './human-review.test-fixtures'
export const controlVessel = reviewAnchor.vesselId
export const controlId = reviewAnchor.evidenceId
export const controlSession = reviewSession
export function controlRow(kind: Kind = 'Control', revision = 1): Row {
  return { id: controlId, vesselId: controlVessel, kind, revision, version: revision+4, content: blank(), hash: 'ABCDEF'[revision-1].repeat(64),
    lifecycle: 'DRAFT', completion: 'UNKNOWN', verification: 'UNVERIFIED', effectiveness: 'UNKNOWN', timeState: 'UNKNOWN',
    evidenceExists: false, file: null, requirements: [], links: [], savedAt: '2026-11-13T00:00:00Z', asOf: '2026-11-13T00:00:00Z',
    blockers: ['SOURCE_ADMISSION_UNAVAILABLE', 'VERIFICATION_NOT_PERFORMED', 'TITLE_MISSING', 'OWNER_MISSING', 'REQUIREMENT_LINK_MISSING',
      'FREQUENCY_UNSPECIFIED', 'EVIDENCE_POLICY_MISSING', 'VERIFICATION_POLICY_MISSING', 'CRITICALITY_UNKNOWN'] }
}
export const controlCommand = (): Command => ({ expectedVersion: 0, content: blank(), requirements: [], links: [], file: null, reasonCode: 'INITIAL_AUTHORING' })
export function echoControl(command: Command, kind: Kind = 'Control', revision = 1): Row {
  return { ...controlRow(kind, revision), content: command.content, requirements: command.requirements, links: command.links,
    file: command.file, evidenceExists: command.file !== null }
}
