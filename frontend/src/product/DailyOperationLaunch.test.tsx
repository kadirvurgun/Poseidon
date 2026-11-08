import { cleanup, render, screen } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { afterEach, expect, it, vi } from 'vitest'
import { DailyOperationLaunch } from './DailyOperationLaunch'
import { decodeDailyCapabilities } from './daily-operations-api'
import { dailyCapsFixture, dailyRecordFixture, dailyVessel } from './daily-test-fixtures'

const destinations = [{ key: 'fuel-bunker-log', href: `/app/fuel-bunker-log/${dailyVessel}` }, { key: 'port-activity-sof', href: `/app/port-activity-sof/${dailyVessel}` }]
afterEach(() => { cleanup(); vi.restoreAllMocks() })

it('launches only a saved revision pin without moving private field values or command state into the URL', async () => {
  const record = dailyRecordFixture('Noon'), navigate = vi.fn()
  record.fields.fuel_summary = { state: 'Known', value: 'FICTIONAL private free text; never a mass estimate' }
  render(<DailyOperationLaunch record={record} destinations={destinations} disabled={false} language="en" onNavigate={navigate} />)
  await userEvent.click(screen.getByRole('button', { name: 'Open fuel record from saved source' }))
  const url = new URL(navigate.mock.calls[0][0], 'https://localhost')
  expect(url.pathname).toBe(`/app/fuel-bunker-log/${dailyVessel}`)
  expect(Object.fromEntries(url.searchParams)).toEqual({ originKind: 'Noon', originId: record.id, originRevision: String(record.revision) })
  expect(url.href).not.toContain('FICTIONAL')
  expect(url.href).not.toContain(record.hash)
})

it('blocks source launch while another form owns pending changes', async () => {
  const navigate = vi.fn()
  render(<DailyOperationLaunch record={dailyRecordFixture('Noon')} destinations={destinations} disabled language="en" onNavigate={navigate} />)
  const button = screen.getByRole('button', { name: 'Open fuel record from saved source' })
  expect(button).toBeDisabled(); await userEvent.click(button); expect(navigate).not.toHaveBeenCalled()
})

it('keeps Engine origins out of port events and rejects forged scoped capability links', () => {
  render(<DailyOperationLaunch record={dailyRecordFixture('Engine')} destinations={destinations} disabled={false} language="en" onNavigate={vi.fn()} />)
  expect(screen.queryByRole('button', { name: 'Open port event from saved source' })).not.toBeInTheDocument()
  const caps = { ...dailyCapsFixture('Noon'), destinations }
  expect(decodeDailyCapabilities(caps, dailyVessel, 'Noon').destinations).toHaveLength(2)
  expect(() => decodeDailyCapabilities({ ...caps, destinations: [{ ...destinations[0], href: destinations[0].href + '?actorId=forged' }] }, dailyVessel, 'Noon')).toThrow()
  expect(() => decodeDailyCapabilities({ ...dailyCapsFixture('Engine'), destinations: [destinations[1]] }, dailyVessel, 'Engine')).toThrow()
})

it('launches downtime only from the saved Engine context and rejects Noon or Deck capability injection', async () => {
  const destination = { key: 'operational-downtime', href: `/app/operational-downtime/${dailyVessel}` }, navigate = vi.fn()
  const engine = dailyRecordFixture('Engine')
  render(<DailyOperationLaunch record={engine} destinations={[destination]} disabled={false} language="en" onNavigate={navigate} />)
  await userEvent.click(screen.getByRole('button', { name: 'Open downtime record from saved source' }))
  expect(navigate).toHaveBeenCalledWith(`${destination.href}?originKind=Engine&originId=${engine.id}&originRevision=${engine.revision}`)
  expect(decodeDailyCapabilities({ ...dailyCapsFixture('Engine'), destinations: [destination] }, dailyVessel, 'Engine').destinations).toEqual([destination])
  for (const kind of ['Noon', 'Deck'] as const)
    expect(() => decodeDailyCapabilities({ ...dailyCapsFixture(kind), destinations: [destination] }, dailyVessel, kind)).toThrow()
})
