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
