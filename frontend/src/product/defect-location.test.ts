import { describe, expect, it } from 'vitest'
import { defectLocationPath, readDefectLocation } from './defect-location'
import { defectAssetPinFixture, defectId, defectVessel } from './defect-test-fixtures'
describe('T-S8-004 nonprivate route context — source only', () => {
  it('supports bare selection and exact paired asset context without granting authority', () => {
    expect(readDefectLocation('/app/defects')).toEqual({ vessel: null, id: null, offset: 0 })
    const asset = defectAssetPinFixture(), href = '/app/defects/' + defectVessel + '?assetId=' + asset.id + '&assetRevision=2'
    expect(defectLocationPath(readDefectLocation(href))).toBe(href)
  })
  it('rejects missing or duplicate selectors, saved-id overrides and form values in the URL', () => {
    const asset = defectAssetPinFixture(), base = '/app/defects/' + defectVessel
    for (const query of ['?assetId=' + asset.id, '?assetId=' + asset.id + '&assetRevision=0', '?assetId=' + asset.id + '&assetRevision=2&assetRevision=3', '?id=' + defectId + '&assetId=' + asset.id + '&assetRevision=2', '?restriction=private', '?assetId=' + asset.id + '&assetRevision=2&hash=guessed']) expect(readDefectLocation(base + query).invalid).toBe(true)
  })
})
