import { afterEach, expect, it, vi } from 'vitest'
import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { CrewOwningInputs } from './CrewOwningInputs'
const vessel = '11111111-1111-4111-8111-111111111111', assignment = '22222222-2222-4222-8222-222222222222'
const fields = ['certificate_state', 'contact_status', 'competence_state', 'work_rest_state', 'eligibility_state', 'fitness_state']
const response = (v: unknown, status = 200) => new Response(JSON.stringify(v), { status, headers: { 'Content-Type': 'application/json' } })
afterEach(() => { cleanup(); vi.unstubAllGlobals() })
it('distinguishes an actual declaration observation from unavailable evaluation and withdraws it on current denial', async () => {
  const fetcher = vi.fn().mockResolvedValue(response({ assignmentId: assignment, authority: 'InputObservationOnly', inputs: fields.map((field, i) => ({
    field, state: i === 0 ? 'DeclaredUnverified' : i === 1 ? 'Withheld' : 'EvaluationUnavailable', ownerTask: 'DEV-048', reason: 'synthetic-test',
  })) })); vi.stubGlobal('fetch', fetcher)
  render(<CrewOwningInputs vessel={vessel} assignment={assignment} language="en" />)
  expect(await screen.findByText('Declared, unverified')).toBeVisible()
  expect(screen.getAllByText('Qualified evaluation unavailable; remains Unknown')).toHaveLength(4)
  fetcher.mockResolvedValue(response({}, 403)); await userEvent.click(screen.getByRole('button', { name: 'Refresh input status' }))
  expect(await screen.findByText('Crew input access denied.')).toBeVisible()
  expect(screen.queryByText('Declared, unverified')).not.toBeInTheDocument()
})
it('rejects a wrong-person/context observation without rendering a positive state', async () => {
  vi.stubGlobal('fetch', vi.fn().mockResolvedValue(response({ assignmentId: vessel, authority: 'InputObservationOnly', inputs: [] })))
  render(<CrewOwningInputs vessel={vessel} assignment={assignment} language="tr" />)
  expect(await screen.findByText('Mürettebat girdi durumu kullanılamıyor.')).toBeVisible()
  expect(screen.queryByRole('definition')).not.toBeInTheDocument()
})
