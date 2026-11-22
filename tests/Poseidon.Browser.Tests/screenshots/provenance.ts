// USER-GUIDE-01 UG-2 (UG-DOC-03): provenance record saved next to every committed screenshot and the
// DOM-text privacy scan (no OCR: the visible text of the captured page is scanned instead).
// Pure; used by the capture runner before writing and by provenance.test.ts on committed files.

export const provenanceSchema = 'poseidon.help.screenshot-provenance.v1' as const
export const generator = 'tests/Poseidon.Browser.Tests/screenshots/capture.ts' as const
/** Only the repository synthetic seed plus opt-in FICTIONAL demo fixtures may feed a screenshot. */
export const allowedDatasets = Object.freeze(['backend/seed/synthetic-demo.json', 'DemoFixtureCommand'])
/** Tenant visible to the lab-manager account. The second synthetic tenant must never appear. */
export const visibleTenant = 'Synthetic Fleet A'
export const foreignTenantMarkers = Object.freeze(['Synthetic Fleet B', 'Synthetic Operator B', 'Synthetic Ship B'])

export type Provenance = {
  schemaVersion: typeof provenanceSchema
  generator: typeof generator
  commit: string
  key: string
  locale: string
  step: number
  viewport: string
  datasets: string[]
  tenant: string
  fixedClock: string
  png: { sha256: string; width: number; height: number }
  domText: string
}

const patterns: ReadonlyArray<readonly [string, RegExp]> = [
  ['email', /[A-Za-z0-9._%+-]+@[A-Za-z0-9-]+(?:\.[A-Za-z0-9-]+)*\.[A-Za-z]{2,}/],
  ['imo', /\bIMO[\s:#-]*\d{7}\b/i],
  ['password', /\b(?:password|parola|şifre)\s*[:=]\s*\S/i],
  ['bearer', /\bbearer\s+[A-Za-z0-9._~+/-]{16,}/i],
  ['jwt', /\beyJ[A-Za-z0-9_-]{8,}\.[A-Za-z0-9_-]{8,}\./],
  ['phone', /\+\d{1,3}[\s-]?\(?\d{2,4}\)?[\s-]?\d{3}[\s-]?\d{2,4}/],
]

/** Returns the finding kinds (never the matched text, so a report cannot leak what it found). */
export function scanDomText(text: string, secrets: readonly string[] = []): string[] {
  const findings = patterns.filter(([, pattern]) => pattern.test(text)).map(([kind]) => kind)
  if (foreignTenantMarkers.some(marker => text.includes(marker))) findings.push('foreign_tenant')
  if (secrets.some(secret => secret.length >= 8 && text.includes(secret))) findings.push('credential')
  return findings
}

export function validateProvenance(value: unknown, expected: { key: string; locale: string; step: number; viewport: string; sha256: string }): string[] {
  const problems: string[] = []
  if (!value || typeof value !== 'object') return ['not_object']
  const record = value as Partial<Provenance>
  if (record.schemaVersion !== provenanceSchema) problems.push('schema')
  if (record.generator !== generator) problems.push('generator')
  if (typeof record.commit !== 'string' || !/^[0-9a-f]{40}$/.test(record.commit)) problems.push('commit')
  for (const field of ['key', 'locale', 'step', 'viewport'] as const) if (record[field] !== expected[field]) problems.push(field)
  if (!Array.isArray(record.datasets) || !record.datasets.length || record.datasets.some(item => !allowedDatasets.includes(item))) problems.push('datasets')
  if (record.tenant !== visibleTenant) problems.push('tenant')
  if (record.png?.sha256 !== expected.sha256) problems.push('png_sha256')
  if (typeof record.domText !== 'string' || !record.domText) problems.push('dom_text')
  else problems.push(...scanDomText(record.domText).map(kind => 'dom:' + kind))
  return problems
}
