import { useState } from 'react'
import { render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { describe, expect, it } from 'vitest'
import { CertificateMetadataForm } from './CertificateMetadataForm'
import { emptyCertificateMetadata } from './certificate-metadata'
function Harness({ scope = 'Vessel', disabled = false, language = 'en' }: { scope?: 'Vessel' | 'Crew'; disabled?: boolean; language?: 'en' | 'tr' }) {
  const [value, setValue] = useState(emptyCertificateMetadata)
  return <><CertificateMetadataForm value={value} scope={scope} disabled={disabled} language={language} onChange={setValue} /><output aria-label="Declared metadata fixture">{JSON.stringify(value)}</output></>
}
describe('T-S6-001 declared certificate fields (authored; not executed)', () => {
  it('retains exact numeric planning text and leaves undeclared dates/document/crew-only members null', async () => {
    const user = userEvent.setup(); render(<Harness />)
    await user.type(screen.getByLabelText('Declared renewal lead days'), '9007199254740993.123456789012345678901')
    await user.type(screen.getByLabelText('Declared issuer'), 'Synthetic issuer declaration')
    const value = JSON.parse(screen.getByLabelText('Declared metadata fixture').textContent ?? '{}')
    expect(value.renewalLeadDaysNumeric).toBe('9007199254740993.123456789012345678901')
    expect(value.declaration).toMatchObject({ issuer: 'Synthetic issuer declaration', issueDate: null, expiryDate: null })
    expect(value.documentReference).toBeNull(); expect(value.stcwReference).toBeNull(); expect(value.endorsementRequired).toBeNull(); expect(value.vesselTypeScope).toBeNull()
    expect(screen.queryByLabelText('STCW reference declaration')).not.toBeInTheDocument()
    expect(screen.queryByLabelText('Declared vessel type scope')).not.toBeInTheDocument()
    expect(screen.getByText(/do not establish authenticity/)).toBeVisible()
  })
  it('supports explicit unknown endorsement rather than converting absence into no', async () => {
    const user = userEvent.setup(); render(<Harness scope="Crew" />)
    await user.click(screen.getByText('Additional crew declarations', { selector: 'summary' }))
    expect(screen.getByLabelText('Endorsement required declaration')).toHaveValue('')
    await user.selectOptions(screen.getByLabelText('Endorsement required declaration'), 'false')
    expect(JSON.parse(screen.getByLabelText('Declared metadata fixture').textContent ?? '{}').endorsementRequired).toBe(false)
    await user.selectOptions(screen.getByLabelText('Endorsement required declaration'), '')
    expect(JSON.parse(screen.getByLabelText('Declared metadata fixture').textContent ?? '{}').endorsementRequired).toBeNull()
  })
  it('keeps labels associated and fields disabled without write authority in both languages', () => {
    const view = render(<Harness disabled />)
    expect(screen.getByLabelText('Certificate number')).toBeDisabled()
    view.rerender(<Harness disabled language="tr" />)
    expect(screen.getByLabelText('Sertifika numarası')).toBeDisabled()
    expect(screen.getByLabelText('Beyan edilen düzenleyen')).toBeDisabled()
  })
})
