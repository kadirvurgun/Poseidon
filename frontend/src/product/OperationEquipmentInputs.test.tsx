import { useState } from 'react'
import { render, screen, waitFor } from '@testing-library/react'
import userEvent from '@testing-library/user-event'
import { beforeEach, expect, it, vi } from 'vitest'
import { OperationEquipmentEditor, OwningEvaluationSelector } from './OperationEquipmentEditor'
import { blank, validContent, type Content } from './control-evidence-api'
import { validBindings, validOwning } from './operation-equipment-contract'
import * as graph from './control-evidence-api'
import * as technical from './technical-api'
import { tugApi } from './tug-api'

vi.mock('./technical-api', () => ({ snapshot: vi.fn() }))
vi.mock('./tug-api', () => ({ tugApi: { board: vi.fn() } }))
vi.mock('./control-evidence-api', async importOriginal => ({ ...await importOriginal<typeof import('./control-evidence-api')>(), page: vi.fn() }))
const vessel = '10000000-0000-4000-8000-000000000001'
beforeEach(() => {
  vi.mocked(technical.snapshot).mockResolvedValue({ items: [], complete: true, canWrite: false, canReadHistory: false })
  vi.mocked(graph.page).mockResolvedValue({ items: [], offset: 0, hasMore: false, canWrite: true })
  vi.mocked(tugApi.board).mockResolvedValue({ items: [], hasMore: false })
})
function Harness() {
  const [value, setValue] = useState<Content>(blank())
  return <><OperationEquipmentEditor vessel={vessel} language="en" offline={false} isTug={false} value={value} onChange={setValue} /><output data-testid="wire">{JSON.stringify(value)}</output></>
}
it('cargo capture preserves Unknown and explicit false separately without verifying it', async () => {
  const user = userEvent.setup(); render(<Harness />)
  await user.selectOptions(screen.getByLabelText('Document purpose'), 'cargoPlan')
  expect(screen.getByLabelText('Packaged dangerous goods declaration')).toHaveValue('')
  expect(JSON.parse(screen.getByTestId('wire').textContent!).cargoPlan.packagedDangerousGoods).toBeNull()
  await user.type(screen.getByLabelText('Cargo plan reference'), 'FICTIONAL PLAN')
  await user.selectOptions(screen.getByLabelText('Packaged dangerous goods declaration'), 'false')
  expect(JSON.parse(screen.getByTestId('wire').textContent!).cargoPlan).toEqual({ planReference: 'FICTIONAL PLAN', packagedDangerousGoods: false })
  await user.selectOptions(screen.getByLabelText('Packaged dangerous goods declaration'), '')
  expect(JSON.parse(screen.getByTestId('wire').textContent!).cargoPlan.packagedDangerousGoods).toBeNull()
  expect(screen.queryByText('Ready', { exact: true })).not.toBeInTheDocument()
})
it('current reference denial clears evaluation references and keeps metadata unavailable', async () => {
  vi.mocked(graph.page).mockRejectedValue(new Error('denied'))
  const changed = vi.fn(); render(<OwningEvaluationSelector vessel={vessel} language="en" offline={false} onChange={changed} />)
  await screen.findByRole('alert'); await waitFor(() => expect(changed).toHaveBeenCalledWith(null))
  expect(screen.getByLabelText('Cargo plan')).toBeDisabled()
})
it('closed wire contract rejects truth coercion, caller approvals, incomplete tuple and mixed documents', () => {
  expect(validOwning('cargoPlan', { planReference: null, packagedDangerousGoods: null })).toBe(true)
  expect(validOwning('cargoPlan', { planReference: 'FICTIONAL', packagedDangerousGoods: false })).toBe(true)
  expect(validOwning('cargoPlan', { planReference: 'FICTIONAL', packagedDangerousGoods: 'false' })).toBe(false)
  expect(validOwning('cargoPlan', { planReference: 'FICTIONAL', packagedDangerousGoods: true, verified: true })).toBe(false)
  expect(validOwning('winchFitment', { assetId: vessel, assetRevision: 1, assetVersion: null, fitted: true, registerReference: 'FICTIONAL' })).toBe(false)
  expect(validContent({ ...blank(), cargoPlan: { planReference: null, packagedDangerousGoods: null }, winchFitment: { assetId: null, assetRevision: null, assetVersion: null, fitted: null, registerReference: null } })).toBe(false)
  expect(validBindings([{ field: 'operation.jobType', ownerValue: 'Harbour Towage', ruleValue: 'HARBOUR_ASSIST', sourceLocator: 'FICTIONAL/source' }])).toBe(true)
  expect(validBindings([{ field: 'operation.jobType', ownerValue: 'Harbour Towage', ruleValue: 'HARBOUR_ASSIST', sourceLocator: '' }])).toBe(false)
})
