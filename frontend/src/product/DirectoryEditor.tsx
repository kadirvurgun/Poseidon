import { useEffect, useRef, useState } from 'react'
import type { FormEvent } from 'react'
import * as api from './api'
import { editorCopy } from './editor-copy'
import type { Language } from './editor-copy'

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
    try {
      const record = await api.saveDirectory(target.resource, initial?.id, body, csrf, command.current.key, controller.signal)
      onDirty(false); onSaved(record)
    } catch (error) {
      const failure = error instanceof api.ApiError ? error : new api.ApiError(0)
      setFailure(failure); setErrors(failure.errors)
      // A failed response or transport can follow a committed transaction. Retry keeps key and payload.
      setUncertain(failure.status === 0 || failure.status >= 500)
    } finally { clearTimeout(timeout); busy.current = false; setSaving(false); onSaving(false) }
  }
  const errorText = failure?.code === 'version_conflict' ? t.conflict : failure?.code === 'duplicate_code' ? t.duplicate :
    failure?.status === 401 || failure?.status === 403 ? t.forbidden : uncertain ? t.uncertain : t.failed
  const fieldError = (field: string) => errors[field]?.length ? <span id={`dir-${field}-error`} className="field-error">{errors[field][0] === 'required' ? t.required : t.invalid}</span> : null
  return <section className="directory-editor" aria-labelledby="editor-heading">
    <h2 id="editor-heading" ref={heading} tabIndex={-1}>{initial ? t.edit : t.create}</h2>
    <p>{t.requiredNote}</p>
    <form noValidate onSubmit={e => void submit(e)} aria-busy={saving}>
      <fieldset disabled={saving || uncertain}>
        <label htmlFor="dir-code">{t.code} *</label><input id="dir-code" value={code} maxLength={64} required aria-invalid={!!errors.code?.length} aria-describedby="dir-code-error" onChange={e => change('code', e.target.value)} />{fieldError('code')}
        <label htmlFor="dir-name">{t.name} *</label><input id="dir-name" value={name} maxLength={200} required aria-invalid={!!errors.name?.length} aria-describedby="dir-name-error" onChange={e => change('name', e.target.value)} />{fieldError('name')}
        {vesselCreate && <>
          <label htmlFor="dir-kind">{t.kind} *</label><select id="dir-kind" required value={kind} aria-invalid={!!errors.kind?.length} aria-describedby="dir-kind-error" onChange={e => change('kind', e.target.value)}><option value="">{t.choose}</option><option value="Ship">{t.ship}</option><option value="Tug">{t.tug}</option></select>{fieldError('kind')}
          <label htmlFor="dir-companyId">{t.company} *</label><select id="dir-companyId" required value={companyId} aria-invalid={!!errors.companyId?.length} aria-describedby="dir-companyId-error" onChange={e => change('companyId', e.target.value)}><option value="">{t.choose}</option>{companyId && !choices?.items.some(c => c.id === companyId) && <option value={companyId}>{companyId}</option>}{choices?.items.map(c => <option key={c.id} value={c.id}>{c.code} · {c.name}</option>)}</select>{fieldError('companyId')}
          {!choices && !choiceError && <p role="status">{t.loading}</p>}
          {choiceError && <p role="alert">{t.failed} <button type="button" onClick={() => { setChoiceError(false); setChoiceAttempt(v => v + 1) }}>{t.reloadCompanies}</button></p>}
          {choices && <div className="form-actions"><button type="button" disabled={choiceOffset === 0} onClick={() => { setChoices(null); setChoiceOffset(v => v - 25) }}>{t.previous}</button><button type="button" disabled={!choices.hasMore} onClick={() => { setChoices(null); setChoiceOffset(v => v + 25) }}>{t.next}</button></div>}
        </>}
      </fieldset>
      {initial && 'kind' in initial && <p>{t.immutable} {initial.kind === 'Ship' ? t.ship : t.tug}</p>}
      {failure && <div role="alert"><p>{errorText}</p>{failure.correlationId && <p>{t.reference}: <code>{failure.correlationId}</code></p>}{failure.code === 'version_conflict' && <button type="button" onClick={onReload}>{t.reload}</button>}</div>}
      <div className="form-actions"><button className="primary-button" type="submit" disabled={saving || failure?.code === 'version_conflict'}>{saving ? t.saving : uncertain ? t.retry : t.save}</button><button type="button" disabled={saving} onClick={onCancel}>{t.cancel}</button></div>
    </form>
  </section>
}
