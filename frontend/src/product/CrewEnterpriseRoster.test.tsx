import { afterEach, describe, expect, it, vi } from 'vitest'
import { cleanup, render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { CrewEnterpriseRoster } from './CrewEnterpriseRoster'
import { CrewEnterpriseProfileFields } from './CrewEnterpriseProfile'
import type { CrewPage, CrewRow } from './crew-api'

const vessel = '11111111-1111-1111-1111-111111111111'
const row: CrewRow = {
  id: '22222222-2222-2222-2222-222222222222', vesselId: vessel, personId: '33333333-3333-3333-3333-333333333333', version: 1, revision: 1,
  positionId: null, rank: null, capacity: null, department: null, from: null, to: null, contractEndDate: null, watchGroup: null,
  assignmentStatus: 'Draft', provenance: 'SyntheticUnverified', fullName: null, employeeNo: null, privacyScope: 'Withheld', sourceObservedAt: null,
  eligibilityState: 'Unknown', workRestState: 'Unknown', certificateState: 'Unknown', competenceState: 'Unknown', contactStatus: 'Withheld',
  nationality: null, nationalityState: 'Withheld', presenceState: 'Draft', presenceReason: 'incomplete_declaration', positionState: 'Missing',
}
const page = (items: CrewRow[], offset = 0, hasMore = false): CrewPage => ({ items, offset, hasMore, canWrite: false, canWriteIdentity: false })
const rosterHref = `/app/crew-roster/${vessel}`
const renderRoster = (value: CrewPage, locked = false) => {
  const onNavigate = vi.fn(), onPage = vi.fn()
  const result = render(<CrewEnterpriseRoster page={value} language="en" locked={locked} rosterHref={rosterHref} onNavigate={onNavigate} onPage={onPage} />)
  return { ...result, onNavigate, onPage }
}
afterEach(cleanup)

describe('ENTERPRISE-CREW-WORKSPACE-01 authored UI behavior; execution deferred', () => {
  it('counts only returned assignment rows and never treats a declaration as qualified manning', () => {
    renderRoster(page([
      row,
      { ...row, id: '44444444-4444-4444-4444-444444444444', presenceState: 'OnboardDeclaration', presenceReason: 'unverified_onboard_declaration' },
      { ...row, id: '55555555-5555-5555-5555-555555555555', presenceState: 'Stale', presenceReason: 'assignment_policy_changed' },
    ], 25, true))
    const section = screen.getByRole('region', { name: 'Authorized roster' })
    expect(within(section).getByText('Listed assignments').parentElement).toHaveTextContent('3')
    expect(within(section).getByText('Unverified declarations').parentElement).toHaveTextContent('1')
    expect(within(section).getByText('Context to review').parentElement).toHaveTextContent('1')
    expect(within(section).getByText('Incomplete drafts').parentElement).toHaveTextContent('1')
    expect(screen.getByText('Page 2')).toBeVisible()
    expect(screen.getByText('Onboard · unverified declaration')).toBeVisible()
    expect(screen.getByText(/Declarations do not establish qualification or minimum manning/)).toBeVisible()
    expect(screen.queryByText(/ready|qualified eligible/i)).not.toBeInTheDocument()
  })

  it('refines this page by state and department while preserving full-page summary counts', async () => {
    const user = userEvent.setup()
    renderRoster(page([
      { ...row, fullName: 'FICTIONAL A', privacyScope: 'IdentityPermitted', department: 'SYNTHETIC DECK', positionState: 'DeclaredUnverified' },
      { ...row, id: '44444444-4444-4444-4444-444444444444', fullName: 'FICTIONAL B', privacyScope: 'IdentityPermitted', presenceState: 'OnboardDeclaration', presenceReason: 'unverified_onboard_declaration', department: 'SYNTHETIC ENGINE', positionState: 'DeclaredUnverified' },
    ]))
    await user.selectOptions(screen.getByRole('combobox', { name: 'Assignment state' }), 'OnboardDeclaration')
    expect(screen.getByRole('link', { name: 'FICTIONAL B' })).toBeVisible()
    expect(screen.queryByRole('link', { name: 'FICTIONAL A' })).not.toBeInTheDocument()
    expect(screen.getByText('1 of 2 assignments on this page')).toBeVisible()
    expect(screen.getByText('Listed assignments').parentElement).toHaveTextContent('2')
    await user.selectOptions(screen.getByRole('combobox', { name: 'Department' }), 'SYNTHETIC DECK')
    expect(screen.getByText('No matches on this page')).toBeVisible()
    expect(screen.getByText(/These filters do not search the whole vessel/)).toBeVisible()
    await user.click(screen.getByRole('button', { name: 'Clear filters' }))
    expect(screen.getByRole('link', { name: 'FICTIONAL A' })).toBeVisible()
    expect(screen.getByRole('link', { name: 'FICTIONAL B' })).toBeVisible()
  })

  it('preserves withheld identity and routes profile clicks through the existing navigation guard', async () => {
    const user = userEvent.setup()
    const { onNavigate } = renderRoster(page([row]))
    const link = screen.getByRole('link', { name: 'Withheld' })
    expect(link).toHaveAttribute('href', `${rosterHref}/crew-profile/${row.id}`)
    await user.click(link)
    expect(onNavigate).toHaveBeenCalledWith(`${rosterHref}/crew-profile/${row.id}`)
    expect(screen.queryByText(row.personId)).not.toBeInTheDocument()
    expect(screen.queryByText(/crew total|fleet total/i)).not.toBeInTheDocument()
  })

  it('passes server offsets to paging and locks filters and paging during a dirty editor', async () => {
    const user = userEvent.setup()
    const { onPage, rerender } = renderRoster(page([row], 25, true))
    await user.click(screen.getByRole('button', { name: 'Next' }))
    expect(onPage).toHaveBeenCalledWith(50)
    await user.click(screen.getByRole('button', { name: 'Previous' }))
    expect(onPage).toHaveBeenCalledWith(0)
    rerender(<CrewEnterpriseRoster page={page([row], 25, true)} language="en" locked rosterHref={rosterHref} onNavigate={vi.fn()} onPage={onPage} />)
    expect(screen.getByRole('button', { name: 'Next' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Previous' })).toBeDisabled()
    expect(screen.getByRole('combobox', { name: 'Assignment state' })).toBeDisabled()
    expect(screen.getByRole('combobox', { name: 'Department' })).toBeDisabled()
  })

  it('keeps unknown qualification and protected health/contact separate from assignment particulars', async () => {
    const user = userEvent.setup()
    render(<CrewEnterpriseProfileFields row={{ ...row, watchGroup: 'SYNTHETIC WATCH' }} language="en" />)
    expect(screen.getByText('SYNTHETIC WATCH')).toBeVisible()
    const disclosure = screen.getByText('Qualification and protected information').closest('details')!
    expect(disclosure).not.toHaveAttribute('open')
    await user.click(screen.getByText('Qualification and protected information'))
    expect(disclosure).toHaveAttribute('open')
    const contact = within(disclosure).getByText('Contact').closest('dl')!
    const health = within(disclosure).getByText('Health').closest('dl')!
    expect(contact).toHaveTextContent('Withheld')
    expect(health).toHaveTextContent('Withheld')
    expect(within(disclosure).getByText('Eligibility').closest('dl')).toHaveTextContent('Unknown')
    expect(screen.queryByText(/eligible|fit for duty|approved/i)).not.toBeInTheDocument()
  })

  it('provides Turkish labels and an actual empty server page without invented people', () => {
    render(<CrewEnterpriseRoster page={page([])} language="tr" locked={false} rosterHref={rosterHref} onNavigate={vi.fn()} onPage={vi.fn()} />)
    expect(screen.getByText('Bu sayfada atama yok.')).toBeVisible()
    expect(screen.getByText('Bu sayfadaki 0 atamadan 0 kayıt')).toBeVisible()
    expect(screen.getByRole('button', { name: 'Önceki' })).toBeDisabled()
    expect(screen.getByRole('button', { name: 'Sonraki' })).toBeDisabled()
    expect(screen.queryByRole('table')).not.toBeInTheDocument()
  })
})
