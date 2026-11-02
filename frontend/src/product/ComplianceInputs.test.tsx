import { render, screen, fireEvent } from '@testing-library/react'
import { describe, expect, it } from 'vitest'
import { ComplianceInputEditor } from './ComplianceInputEditor'
import { blank, validContent, type Content } from './control-evidence-api'
import { validCompliancePolicies, validOwningReferences } from './compliance-input-contract'

describe('source-defined compliance capture', () => {
  it('preserves Unknown and explicit false without approval or declaration coercion', () => {
    let value: Content = { ...blank(), classPack: { packReference: null, edition: null, surveyReference: null, iacsM79: null } }
    const { rerender } = render(<ComplianceInputEditor value={value} onChange={v => { value = v }} offline={false} language="en" />)
    expect(screen.getByLabelText('M79 statement in source')).toHaveValue('')
    fireEvent.change(screen.getByLabelText('M79 statement in source'), { target: { value: 'false' } })
    expect(value.classPack?.iacsM79).toBe(false)
    rerender(<ComplianceInputEditor value={value} onChange={v => { value = v }} offline={true} language="en" />)
    expect(screen.getByLabelText('M79 statement in source')).toBeDisabled()
    expect(validContent(value)).toBe(true)
    expect(validContent({ ...value, msmd: { documentReference: null, issuerReference: null, roles: null } })).toBe(false)
    expect(validContent({ ...value, classPack: { ...value.classPack, iacsM79: 'false' } })).toBe(false)
    expect(validContent({ ...value, verified: true })).toBe(false)
  })
  it('requires exact upstream evaluation identity and source policy tuples', () => {
    const base = { operationId: null, cargoPlanId: null, winchFitmentId: null }
    expect(validOwningReferences(base)).toBe(true)
    expect(validOwningReferences({ ...base, ism: true })).toBe(false)
    expect(validOwningReferences({ ...base, roleAssessmentIds: ['not-an-id'] })).toBe(false)
    expect(validCompliancePolicies({ eligibility: { positionId: null, sourceLocator: null, criteria: [] } })).toBe(true)
    expect(validCompliancePolicies({ ismDependency: { ruleId: 'invalid', ruleRevision: 1, sourceLocator: 'source' } })).toBe(false)
    expect(validCompliancePolicies({ classPack: { packReference: 'pack', edition: '1', sourceLocator: 'source', approved: true } })).toBe(false)
  })
})
