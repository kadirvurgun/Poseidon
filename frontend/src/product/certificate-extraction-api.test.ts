import { describe, expect, it } from 'vitest'
import { decodeExtraction, freezeExtraction } from './certificate-extraction-api'
import { extractionActor, extractionFixture, extractionId, extractionVessel } from './certificate-extraction-test-fixtures'
describe('DEV045 closed extraction contract sources, not executed', () => {
  it('binds all candidate fields to exact certificate, version, source and advisory semantics', () => {
    const source = extractionFixture(); expect(decodeExtraction(source, extractionVessel, extractionId, 17)).toEqual(source)
    for (const changed of [{ ...source, vesselId: extractionActor }, { ...source, version: 18 }, { ...source, state: 'VERIFIED' }, { ...source, sourceVersion: 'other' }, { ...source, verified: true }, { ...source, candidates: source.candidates.slice(1) }, { ...source, candidates: [source.candidates[0], ...source.candidates.slice(0, 6)] }, { ...source, candidates: source.candidates.map((f, i) => i === 0 ? { ...f, state: 'VERIFIED' } : f) }, { ...source, candidates: source.candidates.map((f, i) => i === 0 ? { ...f, lowConfidence: false } : f) }]) expect(() => decodeExtraction(changed, extractionVessel, extractionId, 17)).toThrow()
  })
  it('requires fact-by-fact and vessel confirmation, forbids identity writes, and freezes exact body/key', () => {
    const source = extractionFixture(), owner = `${extractionVessel}/${extractionActor}`
    const command = freezeExtraction(extractionVessel, extractionId, owner, source, [{ key: 'certificate_no', value: 'MASKED-CORRECTED', confirmed: true }], true)
    expect(Object.isFrozen(command)).toBe(true); expect(JSON.parse(command.body)).toEqual({ expectedVersion: 17, reviewContext: source.reviewContext, facts: [{ key: 'certificate_no', value: 'MASKED-CORRECTED', confirmed: true }], vesselContextConfirmed: true })
    expect(() => freezeExtraction(extractionVessel, extractionId, owner, source, [], true)).toThrow()
    expect(() => freezeExtraction(extractionVessel, extractionId, owner, source, [{ key: 'issuer', value: 'MASKED', confirmed: true }], false)).toThrow()
    expect(() => freezeExtraction(extractionVessel, extractionId, owner, { ...source, canConfirm: false }, [{ key: 'issuer', value: 'MASKED', confirmed: true }], true)).toThrow()
  })
})
describe('DEV045 source region and ambiguity contract', () => {
  it('accepts bounded regions on a stated page and rejects ambiguous candidates that are not low-confidence review', () => {
    const source = extractionFixture(); const at = (patch: Record<string, unknown>) => ({ ...source, candidates: source.candidates.map((f, i) => i === 1 ? { ...f, ...patch } : f) })
    const ambiguous = at({ value: 'SYN-1', confidence: 0.95, page: 1, lowConfidence: true, ambiguous: true, region: { x: 0.1, y: 0.2, width: 0.3, height: 0.02 }, reasons: ['LOW_CONFIDENCE_REVIEW', 'AMBIGUOUS_CANDIDATE_REVIEW', 'PROVIDER_CANDIDATE_NOT_VERIFICATION'] })
    expect(decodeExtraction(ambiguous, extractionVessel, extractionId, 17).candidates[1].ambiguous).toBe(true)
    for (const changed of [
      at({ value: 'SYN-1', confidence: 0.95, page: 1, lowConfidence: false, ambiguous: true, reasons: ['AMBIGUOUS_CANDIDATE_REVIEW', 'PROVIDER_CANDIDATE_NOT_VERIFICATION'] }),
      at({ value: 'SYN-1', confidence: 0.4, page: 1, lowConfidence: true, ambiguous: true, reasons: ['PROVIDER_CANDIDATE_NOT_VERIFICATION'] }),
      at({ region: { x: 0.9, y: 0.2, width: 0.3, height: 0.02 } }),
      at({ region: { x: 0.1, y: 0.2, width: 0.3, height: 0.02 }, page: null }),
      at({ region: { x: 0.1, y: 0.2, width: 0.3 } }),
      at({ ambiguous: 'no' }),
    ]) expect(() => decodeExtraction(changed, extractionVessel, extractionId, 17)).toThrow()
  })
})
