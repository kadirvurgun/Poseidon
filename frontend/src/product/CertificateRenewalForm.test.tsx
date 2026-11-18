import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it, vi } from 'vitest'
import { CertificateRenewalForm } from './CertificateRenewalForm'
import { emptyCertificateRenewal, type CertificateRenewalContent } from './certificate-content'
import { certificateActor, certificateId, certificateVessel } from './certificate-test-fixtures'
vi.mock('./CertificateDocumentPicker', () => ({ CertificateDocumentPicker: () => null }))
vi.mock('./CertificateReplacementPicker', () => ({ CertificateReplacementPicker: () => null }))
function Harness({ initial = emptyCertificateRenewal() }: { initial?: CertificateRenewalContent }) {
  const [value, setValue] = useState(initial)
  return <><CertificateRenewalForm value={value} vessel={certificateVessel} owner={certificateVessel + '/' + certificateActor} certificateId={certificateId} ownerOptions={[]} disabled={false} language="en" onChange={setValue} /><output aria-label="Declared renewal values">{JSON.stringify(value)}</output></>
}
describe('T-S6-002 renewal planning declarations', () => {
  it('keeps unknown distinct from declared no and preserves exact planning numbers', async () => {
    render(<Harness />); const user = userEvent.setup()
    const survey = screen.getByLabelText('Survey required declaration')
    expect(survey).toHaveValue('')
    await user.selectOptions(survey, 'false')
    await user.type(screen.getByLabelText('Declared lead days'), '9007199254740993.123456789')
    expect(JSON.parse(screen.getByLabelText('Declared renewal values').textContent!)).toMatchObject({ requiredSurvey: false, requiredDocuments: null, requiredActions: null, leadDaysNumeric: '9007199254740993.123456789', newIssueDate: null, newExpiryDate: null })
    await user.selectOptions(survey, '')
    expect(JSON.parse(screen.getByLabelText('Declared renewal values').textContent!).requiredSurvey).toBeNull()
  })
  it('allows explicitly clearing a retained owner when current owner options are unavailable', async () => {
    render(<Harness initial={{ ...emptyCertificateRenewal(), ownerRoleId: certificateActor }} />)
    const user = userEvent.setup(), owner = screen.getByLabelText('Assigned renewal owner role')
    expect(owner).toHaveValue(certificateActor)
    expect(screen.queryByRole('option', { name: /^Authorized owner role/ })).not.toBeInTheDocument()
    await user.selectOptions(owner, '')
    expect(JSON.parse(screen.getByLabelText('Declared renewal values').textContent!).ownerRoleId).toBeNull()
    expect(screen.getByText(/No explicitly assigned owner options available/)).toBeVisible()
  })
})
