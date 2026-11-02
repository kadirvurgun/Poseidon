import { useId } from 'react'
import './import-mapping-editor.css'

// The owning workspace adapts generated API DTOs to this presentation model.
// Descriptors originate from the authorized server, never from uploaded headers.
export type ImportMappingField = Readonly<{ key: string; label: string; required: boolean; writable: boolean }>
export type ImportMappingBinding = Readonly<{ targetField: string; sourceColumn: number }>
export type ImportMappingTemplate = Readonly<{
  version: string
  fingerprint: string
  fields: readonly ImportMappingField[]
  allowSourceColumnReuse: boolean
}>
type Props = {
  language: 'tr' | 'en'
  currentContextKey: string
  loadedContextKey: string
  state: 'loading' | 'ready' | 'denied' | 'unavailable'
  template: ImportMappingTemplate | null
  headers: readonly string[]
  bindings: readonly ImportMappingBinding[]
  busy?: boolean
  failure?: 'draft' | 'preview' | null
  onChange: (bindings: readonly ImportMappingBinding[]) => void
  onSaveDraft: () => void
  onPreview: () => void
  onRetry?: () => void
}

// A controlled editor: it neither caches private mappings nor commits domain rows.
// Context keys must cover actor/session, tenant, vessel, upload and schema revision.
export function ImportMappingEditor(props: Props) {
  const en = props.language === 'en'
  if (props.state === 'loading') return <p role="status">{en ? 'Loading import template…' : 'İçe aktarma şablonu yükleniyor…'}</p>
  if (props.state === 'denied') return <p role="alert">{en ? 'Import access is not permitted.' : 'İçe aktarma yetkiniz yok.'}</p>
  if (props.state !== 'ready') return <div role="alert">
    <p>{en ? 'Import template could not be loaded.' : 'İçe aktarma şablonu yüklenemedi.'}</p>
    {props.onRetry && <button type="button" onClick={props.onRetry}>{en ? 'Retry' : 'Tekrar dene'}</button>}
  </div>
  if (!props.currentContextKey || props.loadedContextKey !== props.currentContextKey) return <p role="status">
    {en ? 'Reload the import for the current session and selection.' : 'Geçerli oturum ve seçim için içe aktarma verisini yeniden yükleyin.'}
  </p>
  const template = props.template
  if (!template || !template.version || !template.fingerprint || !Array.isArray(template.fields) ||
    template.fields.length === 0 || template.fields.some(field => !field || !field.key ||
      !field.label || /\p{Cc}/u.test(field.key) || field.required && !field.writable) ||
    new Set(template.fields.map(field => field.key)).size !== template.fields.length) return <p role="alert">
    {en ? 'A valid import template is required.' : 'Geçerli bir içe aktarma şablonu gerekiyor.'}
  </p>
  return <MappingFields key={props.currentContextKey} {...props} template={template} />
}

function MappingFields({ language, template, headers, bindings, busy, failure, onChange, onPreview, onSaveDraft }: Props & { template: ImportMappingTemplate }) {
  const prefix = useId(), en = language === 'en'
  const writable = new Set(template.fields.filter(field => field.writable).map(field => field.key))
  const mappedSources = new Map<number, number>()
  for (const binding of bindings) if (binding && Number.isInteger(binding.sourceColumn)) {
    mappedSources.set(binding.sourceColumn, (mappedSources.get(binding.sourceColumn) ?? 0) + 1)
  }
  const unknownMapping = bindings.some(binding => !binding || !writable.has(binding.targetField))
  const problems = template.fields.map(field => {
    const choices = bindings.filter(binding => binding?.targetField === field.key)
    if (choices.length > 1) return en ? 'More than one source is assigned to this field.' : 'Bu alana birden fazla kaynak atanmış.'
    if (choices.length === 0) return field.required ? (en ? 'Choose a source column.' : 'Bir kaynak kolonu seçin.') : null
    const column = choices[0].sourceColumn
    if (!field.writable || !Number.isInteger(column) || column < 0 || column >= headers.length)
      return en ? 'Choose an available source column.' : 'Mevcut bir kaynak kolonu seçin.'
    if (!template.allowSourceColumnReuse && (mappedSources.get(column) ?? 0) > 1)
      return en ? 'This source column is already assigned to another field.' : 'Bu kaynak kolonu başka bir alana da atanmış.'
    return null
  })
  const invalid = unknownMapping || problems.some(Boolean)
  function change(targetField: string, value: string) {
    if (busy) return
    const rest = bindings.filter(binding => binding?.targetField !== targetField)
    onChange(value === '' ? rest : [...rest, { targetField, sourceColumn: Number(value) }])
  }
  return <section className="import-mapping" aria-label={en ? 'Import column mapping' : 'İçe aktarma kolon eşleme'} aria-busy={busy || undefined}>
    <h3>{en ? 'Match source columns' : 'Kaynak kolonlarını eşleyin'}</h3>
    <p>{en ? 'Template version' : 'Şablon sürümü'}: <bdi>{template.version}</bdi></p>
    <p id={`${prefix}-help`}>{en
      ? 'Column numbers distinguish blank or repeated headers. Preview validates the rows; it does not import records. Incomplete mappings can be saved as drafts.'
      : 'Kolon numaraları boş veya tekrarlanan başlıkları ayırt eder. Önizleme satırları doğrular; kayıtları içe aktarmaz. Eksik eşlemeler taslak olarak kaydedilebilir.'}</p>
    {headers.length === 0 && <p role="status">{en ? 'No source columns are available.' : 'Kaynak kolonu bulunamadı.'}</p>}
    {unknownMapping && <p role="alert">{en ? 'The saved mapping contains fields outside this template. Reload the mapping.' : 'Kayıtlı eşleme bu şablonun dışında alanlar içeriyor. Eşlemeyi yeniden yükleyin.'}</p>}
    <fieldset disabled={busy} aria-describedby={`${prefix}-help`}>
      <legend>{en ? 'Template fields' : 'Şablon alanları'}</legend>
      {template.fields.map((field, index) => {
        const id = `${prefix}-field-${index}`, issueId = `${id}-issue`
        const choices = bindings.filter(binding => binding?.targetField === field.key)
        const value = choices.length === 1 && Number.isInteger(choices[0].sourceColumn) &&
          choices[0].sourceColumn >= 0 && choices[0].sourceColumn < headers.length ? String(choices[0].sourceColumn) : ''
        return <div className="import-mapping__field" key={field.key}>
          {field.writable ? <>
            <label htmlFor={id}>{field.label}{field.required ? (en ? ' (required)' : ' (zorunlu)') : ''}</label>
            <select id={id} value={value} onChange={event => change(field.key, event.target.value)}
              aria-required={field.required || undefined} aria-invalid={problems[index] ? true : undefined}
              aria-describedby={problems[index] ? issueId : undefined}>
              <option value="">{en ? 'Not mapped' : 'Eşlenmedi'}</option>
              {headers.map((header, column) => <option key={column} value={column}>
                {column + 1} — {header.trim() || (en ? 'Unnamed column' : 'Başlıksız kolon')}
              </option>)}
            </select>
          </> : <p>{field.label} — {en ? 'Managed by the system' : 'Sistem tarafından yönetilir'}</p>}
          {problems[index] && <p className="import-mapping__issue" id={issueId}>{problems[index]}</p>}
        </div>
      })}
    </fieldset>
    {failure && <p role="alert">{failure === 'draft'
      ? (en ? 'The draft could not be saved. Your current mapping is retained.' : 'Taslak kaydedilemedi. Geçerli eşlemeniz korunuyor.')
      : (en ? 'Preview could not be prepared. Your current mapping is retained.' : 'Önizleme hazırlanamadı. Geçerli eşlemeniz korunuyor.')}</p>}
    <div className="import-mapping__actions">
      <button type="button" disabled={busy} onClick={onSaveDraft}>{en ? 'Save mapping draft' : 'Eşleme taslağını kaydet'}</button>
      <button type="button" disabled={busy || invalid || headers.length === 0} onClick={onPreview}>{en ? 'Validate and preview' : 'Doğrula ve önizle'}</button>
    </div>
  </section>
}
