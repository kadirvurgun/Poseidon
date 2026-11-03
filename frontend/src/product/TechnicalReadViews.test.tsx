import { render, screen, within } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { expect, it, vi } from 'vitest'
import { EquipmentSummary, MaintenancePlanSummary, TechnicalOverview, TechnicalPlanList } from './TechnicalReadViews'
import type { Asset, Plan, References, Snapshot } from './technical-api'

const asset: Asset = { id: '11111111-1111-1111-1111-111111111111', vesselId: '22222222-2222-2222-2222-222222222222', parentId: null,
  code: 'DEMO-PUMP', name: 'FICTIONAL Pompa', level: 'Equipment', systemFamily: null, equipmentType: null, criticality: null, maker: null, model: null, serialNumber: null, location: null,
  commissionDate: null, responsibleRoleId: null, notes: '<img src="fictional" />', status: 'Draft', provenance: 'SyntheticUnverified', revision: 3, version: 1 }
const plan: Plan = { id: '33333333-3333-3333-3333-333333333333', assetId: asset.id, jobId: '44444444-4444-4444-4444-444444444444', ownerRoleId: null,
  inputs: { triggerType: 'Meter', currentMeter: '0.100000000000000002', intervalValue: '0.01', provenance: 'SyntheticUnverified' },
  due: { state: 'ReviewRequired', reason: 'EquipmentRevisionChanged', nextDueDate: null, nextDueMeter: null, algorithm: 'planning-current-v1' },
  revision: 2, version: 1, assetRevision: 1, status: 'Draft', provenance: 'SyntheticUnverified', referenceAt: '2026-11-02T12:00:00Z', savedAt: '2026-11-01T12:00:00Z' }
const references: References = { jobs: [{ id: plan.jobId!, code: 'DEMO-LUBE', title: 'FICTIONAL Lubrication', provenance: 'SyntheticUnverified' }], owners: [], policy: 'DraftDeclarationsOnly;PostponementPolicyUnresolved' }
const snapshot: Snapshot = { items: [asset, { ...asset, id: '55555555-5555-5555-5555-555555555555', level: 'System' }], complete: true, canWrite: false, canReadHistory: false }

it('summarizes only returned hierarchy levels and does not infer maintenance coverage from missing plan access', () => {
  render(<TechnicalOverview assets={snapshot} plans={null} en />)
  const summary = screen.getByRole('region', { name: 'Current technical scope overview' })
  expect(within(summary).getByText('Hierarchy records').parentElement).toHaveTextContent('2')
  expect(within(summary).getByText('Components').parentElement).toHaveTextContent('0')
  expect(within(summary).queryByText('Maintenance plans')).not.toBeInTheDocument()
  expect(summary).toHaveTextContent('Returned authorized records')
})

it('withdraws old counts when a new current maintenance response arrives without promoting NotDue to fitness', () => {
  const mounted = render(<TechnicalOverview assets={snapshot} plans={{ items: [plan], canWrite: false, canReadHistory: false }} en />)
  const summary = screen.getByRole('region', { name: 'Current technical scope overview' })
  expect(within(summary).getByText('Input review').parentElement).toHaveTextContent('1')
  mounted.rerender(<TechnicalOverview assets={snapshot} plans={{ items: [{ ...plan, due: { ...plan.due, state: 'NotDue' } }], canWrite: false, canReadHistory: false }} en />)
  expect(within(summary).getByText('Input review').parentElement).toHaveTextContent('0')
  expect(within(summary).getByText('Not due').parentElement).toHaveTextContent('1')
  expect(summary).toHaveTextContent('Operational fitness is separate')
})

it('matches actual equipment and job codes, delegates selection and distinguishes no matches from no plans', async () => {
  const onSelect = vi.fn(), user = userEvent.setup()
  const mounted = render(<TechnicalPlanList plans={[plan]} assets={[asset]} references={references} filter="pump" selectedId={plan.id} en onSelect={onSelect} />)
  const item = screen.getByRole('button', { name: 'FICTIONAL Lubrication · Review required' })
  expect(item).toHaveAttribute('aria-current', 'true')
  await user.click(item); expect(onSelect).toHaveBeenCalledWith(plan.id)
  mounted.rerender(<TechnicalPlanList plans={[plan]} assets={[asset]} references={references} filter="missing" selectedId={null} en onSelect={onSelect} />)
  expect(screen.getByText('No matching plans.')).toBeVisible()
  expect(screen.queryByText('No plans.')).not.toBeInTheDocument()
  mounted.rerender(<TechnicalPlanList plans={[plan]} assets={[asset]} references={references} filter="DEMO-LUBE" selectedId={null} en onSelect={onSelect} />)
  expect(screen.getByRole('button', { name: 'FICTIONAL Lubrication · Review required' })).toBeVisible()
})

it('groups actual equipment declarations and renders operator notes as text with unknown responsibility intact', () => {
  const { container } = render(<EquipmentSummary asset={asset} en />)
  expect(screen.getByRole('heading', { name: 'FICTIONAL Pompa' })).toBeVisible()
  expect(screen.getByText('Responsible role reference').nextElementSibling).toHaveTextContent('Unknown')
  expect(screen.getByText('<img src="fictional" />')).toBeVisible()
  expect(container.querySelector('img')).toBeNull()
})

it('retains current source/revision reasons, unknown boundaries and exact recorded meter inputs', async () => {
  render(<MaintenancePlanSummary plan={plan} asset={asset} references={references} en />)
  expect(screen.getByText(/Equipment changed after this plan revision/)).toBeVisible()
  expect(screen.getByText('Next due date / meter').nextElementSibling).toHaveTextContent('Unknown / Unknown')
  expect(screen.getByText(/Current planning calculation/)).toHaveTextContent('does not verify completed maintenance or operational fitness')
  await userEvent.click(screen.getByText('All recorded plan inputs'))
  expect(screen.getByText('0.100000000000000002')).toBeVisible()
  expect(screen.queryByText('Ready', { exact: true })).not.toBeInTheDocument()
})
