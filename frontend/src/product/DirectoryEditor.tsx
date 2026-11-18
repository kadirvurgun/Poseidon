import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import * as api from './api'
import { editorCopy } from './editor-copy'
import type { Language } from './editor-copy'
import { Button } from '../components/ui/button'
import { EnterpriseActions, EnterpriseField, EnterpriseFormGrid } from './EnterprisePatterns'

export type EditorTarget = { instanceKey?: string; resource: 'companies' | 'vessels'; record?: api.DirectoryRecord }
type Props = { target: EditorTarget; language: Language; csrf: string; onDirty: (dirty: boolean) => void; onSaving: (saving: boolean) => void; onCancel: () => void; onReload: () => void; onSaved: (record: api.DirectoryRecord) => void }

export function DirectoryEditor({ target, language, csrf, onDirty, onSaving, onCancel, onReload, onSaved }: Props) {
  const initial = target.record
  const [code, setCode] = useState(initial?.code ?? '')
  const [name, setName] = useState(initial?.name ?? '')
  const [kind, setKind] = useState('')
  const [companyId, setCompanyId] = useState('')
  const [choices, setChoices] = useState<api.Page<api.Company> | null>(null)
  const [choiceOffset, setChoiceOffset] = useState(0)
  const [choiceAttempt, setChoiceAttempt] = useState(0)
  const [choiceError, setChoiceError] = useState(false)
  const [errors, setErrors] = useState<Record<string, string[]>>({})
  const [failure, setFailure] = useState<api.ApiError | null>(null)
  const [saving, setSaving] = useState(false)
  const [uncertain, setUncertain] = useState(false)
  const busy = useRef(false)
  const command = useRef({ key: '', payload: '' })
  const abort = useRef<AbortController | null>(null)
  const heading = useRef<HTMLHeadingElement>(null)
  const t = editorCopy[language]
  const vesselCreate = target.resource === 'vessels' && !initial
  useEffect(() => { const previous = document.activeElement as HTMLElement | null; heading.current?.focus(); return () => { abort.current?.abort(); if (previous?.isConnected) previous.focus() } }, [])
  useEffect(() => {
    if (!vesselCreate) return
    const controller = new AbortController()
    let active = true
    api.companies(choiceOffset, controller.signal).then(data => { if (active) setChoices(data) }).catch(() => { if (active) setChoiceError(true) })
    return () => { active = false; controller.abort() }
  }, [vesselCreate, choiceOffset, choiceAttempt])

  function change(field: string, value: string) {
    if (saving || uncertain) return
    if (field === 'code') setCode(value)
    if (field === 'name') setName(value)
    if (field === 'kind') setKind(value)
    if (field === 'companyId') setCompanyId(value)
    setErrors(current => ({ ...current, [field]: [] }))
    setFailure(null)
    onDirty(true)
  }
  async function submit(event: FormEvent) {
    event.preventDefault()
    if (busy.current) return
    const fields: Record<string, string[]> = {}
    for (const [field, value, max] of [['code', code, 64], ['name', name, 200]] as const) {
      if (!value.trim()) fields[field] = ['required']
      else if (value.length > max || Array.from(value).some(c => c.charCodeAt(0) < 32 || (c.charCodeAt(0) >= 127 && c.charCodeAt(0) <= 159))) fields[field] = ['invalid']
    }
    if (vesselCreate && !kind) fields.kind = ['required']
    if (vesselCreate && !companyId) fields.companyId = ['required']
    setErrors(fields)
    if (Object.keys(fields).length) { document.getElementById(`dir-${Object.keys(fields)[0]}`)?.focus(); return }
    const body = vesselCreate ? { code: code.trim(), name: name.trim(), kind: kind as 'Ship' | 'Tug', companyId } :
      { code: code.trim(), name: name.trim(), ...(initial ? { expectedVersion: initial.version } : {}) }
    const payload = JSON.stringify(body)
    if (!command.current.key || command.current.payload !== payload) command.current = { key: crypto.randomUUID(), payload }
    busy.current = true; onDirty(true); setSaving(true); onSaving(true); setFailure(null)
    const controller = new AbortController(); abort.current = controller
    const timeout = setTimeout(() => controller.abort(), 15000)
    let navigationFrozen = false
    try {
      const record = await api.saveDirectory(target.resource, initial?.id, body, csrf, command.current.key, controller.signal)
      onDirty(false); onSaved(record)
    } catch (error) {
      const failure = error instanceof api.ApiError ? error : new api.ApiError(0)
      setFailure(failure); setErrors(failure.errors)
      // A failed response or transport can follow a committed transaction. Retry keeps key and payload.
      setUncertain(failure.status === 0 || failure.status >= 500)
      navigationFrozen = failure.status === 0 || failure.status >= 500
    } finally { clearTimeout(timeout); busy.current = false; setSaving(false); onSaving(navigationFrozen) }
  }
  const errorText = failure?.code === 'version_conflict' ? t.conflict : failure?.code === 'duplicate_code' ? t.duplicate :
    failure?.status === 401 || failure?.status === 403 ? t.forbidden : uncertain ? t.uncertain : t.failed
  const fieldError = (field: string) => errors[field]?.length ? <span id={`dir-${field}-error`} className="field-error">{errors[field][0] === 'required' ? t.required : t.invalid}</span> : null
  return <section className="directory-editor" aria-labelledby="editor-heading">
    <h2 id="editor-heading" ref={heading} tabIndex={-1}>{initial ? t.edit : t.create}</h2>
    <p>{t.requiredNote}</p>
    <form noValidate aria-label={language === 'en' ? 'Directory record form' : 'Kayıt formu'} onSubmit={e => void submit(e)} aria-busy={saving}>
      <EnterpriseFormGrid disabled={saving || uncertain}>
        <EnterpriseField label={`${t.code} *`} htmlFor="dir-code"><input id="dir-code" value={code} maxLength={64} required aria-invalid={!!errors.code?.length} aria-describedby={errors.code?.length ? 'dir-code-error' : undefined} onChange={e => change('code', e.target.value)} />{fieldError('code')}</EnterpriseField>
        <EnterpriseField label={`${t.name} *`} htmlFor="dir-name"><input id="dir-name" value={name} maxLength={200} required aria-invalid={!!errors.name?.length} aria-describedby={errors.name?.length ? 'dir-name-error' : undefined} onChange={e => change('name', e.target.value)} />{fieldError('name')}</EnterpriseField>
        {vesselCreate && <>
          <EnterpriseField label={`${t.kind} *`} htmlFor="dir-kind"><select id="dir-kind" required value={kind} aria-invalid={!!errors.kind?.length} aria-describedby={errors.kind?.length ? 'dir-kind-error' : undefined} onChange={e => change('kind', e.target.value)}><option value="">{t.choose}</option><option value="Ship">{t.ship}</option><option value="Tug">{t.tug}</option></select>{fieldError('kind')}</EnterpriseField>
          <EnterpriseField label={`${t.company} *`} htmlFor="dir-companyId"><select id="dir-companyId" required value={companyId} aria-invalid={!!errors.companyId?.length} aria-describedby={errors.companyId?.length ? 'dir-companyId-error' : undefined} onChange={e => change('companyId', e.target.value)}><option value="">{t.choose}</option>{companyId && !choices?.items.some(c => c.id === companyId) && <option value={companyId}>{companyId}</option>}{choices?.items.map(c => <option key={c.id} value={c.id}>{c.code} · {c.name}</option>)}</select>{fieldError('companyId')}</EnterpriseField>
          {!choices && !choiceError && <p className="enterprise-form-full" role="status">{t.loading}</p>}
          {choiceError && <div className="enterprise-form-full" role="alert"><p>{t.failed}</p><Button variant="outline" onClick={() => { setChoiceError(false); setChoiceAttempt(v => v + 1) }}>{t.reloadCompanies}</Button></div>}
          {choices && <EnterpriseActions className="enterprise-form-full" aria-label={language === 'en' ? 'Company choices pagination' : 'Şirket seçenekleri sayfalama'}><Button variant="outline" disabled={choiceOffset === 0} onClick={() => { setChoices(null); setChoiceOffset(v => v - 25) }}>{t.previous}</Button><Button variant="outline" disabled={!choices.hasMore} onClick={() => { setChoices(null); setChoiceOffset(v => v + 25) }}>{t.next}</Button></EnterpriseActions>}
        </>}
      </EnterpriseFormGrid>
      {initial && 'kind' in initial && <p>{t.immutable} {initial.kind === 'Ship' ? t.ship : t.tug}</p>}
      {failure && <div role="alert"><p>{errorText}</p>{failure.correlationId && <p>{t.reference}: <code>{failure.correlationId}</code></p>}{failure.code === 'version_conflict' && <Button variant="outline" onClick={onReload}>{t.reload}</Button>}</div>}
      <EnterpriseActions layout="form" aria-label={language === 'en' ? 'Form actions' : 'Form işlemleri'}><Button variant="outline" disabled={saving} onClick={onCancel}>{t.cancel}</Button><Button type="submit" disabled={saving || failure?.code === 'version_conflict'}>{saving ? t.saving : uncertain ? t.retry : t.save}</Button></EnterpriseActions>
    </form>
  </section>
}
