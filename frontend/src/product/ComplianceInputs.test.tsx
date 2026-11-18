import { act, render, screen, fireEvent } from '@testing-library/react'
import { useState } from 'react'
import { describe, expect, it } from 'vitest'
import { ComplianceInputEditor } from './ComplianceInputEditor'
import { blank, validContent, type Content } from './control-evidence-api'
import { validCompliancePolicies, validOwningReferences } from './compliance-input-contract'
import { changeAppLanguage, type Locale } from '../i18n'

describe('source-defined compliance capture', () => {
  it('translates the actual class declaration inputs while preserving draft bytes, false and explicit unknown', async () => {
    function Draft({ language, offline = false }: { language: Locale; offline?: boolean }) {
      const [value, setValue] = useState<Content>(() => ({ ...blank(), classPack: {
        packReference: 'SYNTHETIC pack', edition: null, surveyReference: null, iacsM79: false,
      } }))
      return <><ComplianceInputEditor value={value} onChange={setValue} offline={offline} language={language} />
        <output aria-label="Retained compliance bytes">{JSON.stringify(value)}</output></>
    }
    const { rerender } = render(<Draft language="en" />)
    const reference = screen.getByLabelText('Class rule pack reference')
    const declaration = screen.getByLabelText('M79 statement in source')
    fireEvent.change(reference, { target: { value: 'SYNTHETIC retained declaration' } })
    const original = screen.getByLabelText('Retained compliance bytes').textContent
    await act(async () => { await changeAppLanguage('fr', false) })
    rerender(<Draft language="fr" />)
    expect(screen.getByLabelText('Référence du recueil de règles de classe')).toBe(reference)
    expect(reference).toHaveValue('SYNTHETIC retained declaration')
    expect(screen.getByLabelText('Déclaration M79 dans la source')).toBe(declaration)
    expect(declaration).toHaveValue('false')
    expect(screen.getByLabelText('Retained compliance bytes').textContent).toBe(original)
    fireEvent.change(declaration, { target: { value: '' } })
    expect(JSON.parse(screen.getByLabelText('Retained compliance bytes').textContent!).classPack.iacsM79).toBeNull()
    rerender(<Draft language="fr" offline />)
    expect(reference).toBeDisabled()
    expect(declaration).toBeDisabled()
  })

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
