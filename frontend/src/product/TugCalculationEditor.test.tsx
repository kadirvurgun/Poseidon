import { render, screen, fireEvent } from '@testing-library/react'
import { useState } from 'react'
import { expect, it } from 'vitest'
import { TugCalculationEditor } from './TugCalculationEditor'
import { blankCalculation, type CalculationPolicy } from './tug-capability-contract'

function Harness() {
  const [policy, setPolicy] = useState<CalculationPolicy | null>(null)
  return <><TugCalculationEditor language="en" value={policy} onChange={setPolicy} /><output aria-label="payload">{JSON.stringify(policy)}</output></>
}
it('requires an explicit formula, aggregation, rounding and values while retaining an incomplete source draft', () => {
  render(<Harness />)
  expect(screen.queryByLabelText('Source formula')).not.toBeInTheDocument()
  fireEvent.change(screen.getByLabelText('Calculation source scope'), { target: { value: 'tug' } })
  expect(JSON.parse(screen.getByLabelText('payload').textContent!)).toEqual(blankCalculation())
  fireEvent.change(screen.getByLabelText('Source formula'), { target: { value: 'MeasuredTimesVerifiedCondition' } })
  fireEvent.change(screen.getByLabelText('Qualified decimal scale (0–6)'), { target: { value: '0' } })
  const draft = JSON.parse(screen.getByLabelText('payload').textContent!)
  expect(draft.scale).toBe(0); expect(draft.formula).toBe('MeasuredTimesVerifiedCondition')
  expect(draft.maxLossPercent).toBeNull(); expect(draft.aggregation).toBeNull(); expect(draft.rounding).toBeNull()
  fireEvent.change(screen.getByLabelText('Calculation source scope'), { target: { value: '' } })
  expect(screen.getByLabelText('payload')).toHaveTextContent('null')
})
it('provides actual Turkish labels for the same source workflow', () => {
  render(<TugCalculationEditor language="tr" value={blankCalculation()} onChange={() => {}} />)
  expect(screen.getByLabelText('Kaynak formülü')).toHaveValue('')
  expect(screen.getByLabelText('Kaynak azami test yaşı (gün)')).toHaveValue(null)
})
