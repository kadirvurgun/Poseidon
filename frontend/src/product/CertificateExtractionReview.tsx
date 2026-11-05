import { useEffect, useId, useLayoutEffect, useRef, useState } from 'react'
import { Button } from '../components/ui/button'
import { Badge } from '../components/ui/badge'
import { EnterpriseActions } from './EnterprisePatterns'
import { ApiError, session as readSession, type Session } from './api'
import { isGuid } from './identifiers'
import { certificatesApi, type CertificateDto } from './certificates-api'
import { certificateExtractionApi, freezeExtraction, type CertificateExtraction, type ConfirmableField, type ExtractionAttempt, type ExtractionField } from './certificate-extraction-api'
import { useWorkspaceRevalidation } from './use-workspace-revalidation'
import './certificate-extraction.css'

export type CertificateExtractionReviewProps = { vessel: string; certificateId: string; expectedVersion: number; enabled: boolean; language: 'en' | 'tr'; onDirty: (value: boolean) => void; onSaving: (value: boolean) => void; onApplied: (saved: CertificateDto) => void }
const boundary = (s: Session | undefined) => s?.authenticated && s.canRead && isGuid(s.actorId) && isGuid(s.tenantId) ? `${s.tenantId}/${s.actorId}` : null
const labels: Record<ExtractionField, [string, string]> = { certificate_type: ['Certificate type', 'Sertifika türü'], certificate_no: ['Certificate number', 'Sertifika numarası'], issuer: ['Issuer', 'Düzenleyen'], issue_date: ['Issue date', 'Düzenlenme tarihi'], expiry_date: ['Expiry date', 'Son geçerlilik tarihi'], vessel_reference: ['Vessel reference', 'Gemi referansı'], person_reference: ['Person reference', 'Kişi referansı'] }
type Preview = { owner: string; resource: string; source: CertificateExtraction }
export function CertificateExtractionReview({ vessel, certificateId, expectedVersion, enabled, language, onDirty, onSaving, onApplied }: CertificateExtractionReviewProps) {
  const en = language === 'en', id = useId(), epoch = useWorkspaceRevalidation(), resource = `${vessel}/${certificateId}/${expectedVersion}`
  const mayRead = enabled && navigator.onLine && document.visibilityState !== 'hidden'
  const [activation, setActivation] = useState({ resource, mayRead, generation: 0 })
  if (activation.resource !== resource || activation.mayRead !== mayRead) setActivation({ resource, mayRead, generation: activation.generation + 1 })
  const [authorizationAttempt, reauthorize] = useState(0), scope = `${resource}/${activation.generation}/${epoch}/${mayRead}/${authorizationAttempt}`
  const [auth, setAuth] = useState<{ scope: string; user?: Session }>({ scope: '' }), [preview, setPreview] = useState<Preview | null>(null)
  const [values, setValues] = useState<Partial<Record<ConfirmableField, string>>>({}), [selected, setSelected] = useState<ConfirmableField[]>([]), [contextConfirmed, setContextConfirmed] = useState(false)
  const [pending, setPending] = useState<ExtractionAttempt | null>(null), [busy, setBusy] = useState(false), [failure, setFailure] = useState<ApiError | null>(null), [validation, setValidation] = useState(false)
  const mounted = useRef(false), running = useRef(false), writer = useRef<AbortController | null>(null), committed = useRef({ scope: '', owner: null as string | null })
  const user = mayRead && auth.scope === scope ? auth.user : undefined, owner = boundary(user)
  const current = owner && preview?.owner === owner && preview.resource === resource ? preview.source : null
  const retained = !!preview || !!pending, samePending = !!pending && pending.owner === owner && pending.vessel === vessel && pending.id === certificateId
  useLayoutEffect(() => { committed.current = { scope, owner }; if (!mayRead) writer.current?.abort() }, [scope, owner, mayRead])
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; writer.current?.abort() } }, [])
  useEffect(() => {
    if (!mayRead) return
    const c = new AbortController(); let active = true
    async function load() {
      const user = await readSession(c.signal)
      if (!boundary(user)) { if (active) setAuth({ scope }); return }
      // Session identity alone does not prove current source/file visibility.
      // Use the owning current read before restoring any retained private facts.
      const record = await certificatesApi.read(vessel, certificateId, c.signal)
      const after = await readSession(c.signal)
      if (active) setAuth(record.currentRevision && boundary(user) === boundary(after) ? { scope, user: after } : { scope })
    }
    load().catch(() => { if (active) setAuth({ scope }) })
    return () => { active = false; c.abort() }
  }, [scope, mayRead, vessel, certificateId])
  useEffect(() => { onDirty(retained) }, [retained, onDirty])
  useEffect(() => { onSaving(busy || !!pending) }, [busy, pending, onSaving])
  const canExtract = !!owner && !!user && mayRead && !busy && !pending && !preview
  async function extract() {
    if (!canExtract || running.current || !owner || !user) return
    running.current = true; setBusy(true); setFailure(null)
    const c = new AbortController(); writer.current = c; const original = { owner, scope }, timer = setTimeout(() => c.abort(), 95000)
    try {
      const fresh = await readSession(c.signal)
      if (boundary(fresh) !== original.owner) throw new ApiError(403, 'identity_boundary_changed')
      const source = await certificateExtractionApi.extract(vessel, certificateId, expectedVersion, fresh.csrfToken, c.signal)
      const after = await readSession(c.signal)
      if (!mounted.current || committed.current.scope !== original.scope || committed.current.owner !== original.owner || boundary(after) !== original.owner) return
      setPreview({ owner: original.owner, resource, source }); setSelected([]); setContextConfirmed(false)
      setValues(Object.fromEntries(source.candidates.filter(f => f.key !== 'person_reference' && f.key !== 'vessel_reference').map(f => [f.key, f.value ?? ''])))
    } catch (error) { if (mounted.current) { const problem = error instanceof ApiError ? error : new ApiError(0); setFailure(problem); if ([401, 403, 404].includes(problem.status)) setAuth({ scope }) } }
    finally { clearTimeout(timer); writer.current = null; running.current = false; if (mounted.current) setBusy(false) }
  }
  async function confirm() {
    if (running.current || busy || !mayRead || !owner || !user || !current || pending && !samePending) return
    let command: ExtractionAttempt
    try { command = pending ?? freezeExtraction(vessel, certificateId, owner, current, selected.map(key => ({ key, value: values[key] ?? '', confirmed: true })), contextConfirmed) }
    catch { setValidation(true); return }
    // Freeze the exact body/key BEFORE transport. An uncertain command cannot
    // be discarded, edited, replayed as another actor or automatically retried.
    setPending(command); setBusy(true); setFailure(null); setValidation(false); running.current = true
    const c = new AbortController(); writer.current = c; const originalScope = scope, timer = setTimeout(() => c.abort(), 20000)
    let sent = false
    try {
      const fresh = await readSession(c.signal)
      if (boundary(fresh) !== command.owner) throw new ApiError(403, 'identity_boundary_changed')
      sent = true
      const saved = await certificateExtractionApi.confirm(command, fresh.csrfToken, c.signal)
      const after = await readSession(c.signal)
      if (!mounted.current || committed.current.scope !== originalScope || committed.current.owner !== command.owner || boundary(after) !== command.owner) { if (mounted.current) setFailure(new ApiError(0, 'identity_boundary_changed')); return }
      setPending(null); setPreview(null); setValues({}); setSelected([]); setContextConfirmed(false); onApplied(saved)
    } catch (error) {
      if (!mounted.current) return
      const problem = error instanceof ApiError ? error : new ApiError(0); setFailure(problem)
      if ([401, 403, 404].includes(problem.status)) setAuth({ scope })
      // A previously unknown command stays frozen even if later authorization
      // fails. A first definitive 4xx permits explicit preview discard/review.
      if (!pending && (!sent || problem.status > 0 && problem.status < 500)) setPending(null)
    } finally { clearTimeout(timer); writer.current = null; running.current = false; if (mounted.current) setBusy(false) }
  }
  const show = mayRead && !!owner && (!retained || !!current) && (!pending || samePending)
  return <section className="certificate-extraction" aria-label={en ? 'Certificate document assistance' : 'Sertifika belge asistanı'} aria-busy={busy}>
    <div className="enterprise-section-header"><div><h3>{en ? 'Review document facts' : 'Belge bilgilerini incele'}</h3><p>{en ? 'Extract suggestions from the currently linked document. Each fact requires your confirmation; confidence never verifies a certificate or authorizes an operation.' : 'Güncel bağlı belgeden öneriler çıkarın. Her bilgi onayınızı gerektirir; güven skoru sertifika doğrulaması veya operasyon yetkisi oluşturmaz.'}</p></div><Badge variant="outline">{en ? 'Human confirmation' : 'İnsan onayı'}</Badge></div>
    {!show ? <><p role="status">{en ? 'Private document suggestions are locked until the original identity and context are reauthorized online. Any uncertain save remains protected.' : 'Özel belge önerileri, özgün kimlik ve bağlam çevrimiçi tekrar yetkilendirilene kadar kilitlidir. Belirsiz kayıt korunur.'}</p><Button type="button" variant="outline" disabled={!mayRead || busy} onClick={() => reauthorize(n => n + 1)}>{en ? 'Reauthorize original context' : 'Özgün bağlamı tekrar yetkilendir'}</Button></> : <>
      {failure && <p role="alert">{pending ? (en ? 'Save outcome unknown. Retry only the same protected command after reauthorization.' : 'Kayıt sonucu bilinmiyor. Yetkilendirme sonrası yalnız aynı korunan komutu tekrar deneyin.') : failure.status === 409 ? (en ? 'The saved version or review context changed. Read the current certificate before extracting again.' : 'Kayıtlı sürüm veya inceleme bağlamı değişti. Yeniden çıkarmadan önce güncel sertifikayı okuyun.') : (en ? 'Extraction or confirmation is unavailable. Processing policy, current source, permissions and provider response must all be available.' : 'Çıkarım veya onay kullanılamıyor. İşleme politikası, güncel kaynak, yetkiler ve sağlayıcı yanıtı kullanılabilir olmalıdır.')}</p>}
      {!current ? <Button variant="outline" disabled={!canExtract} onClick={() => void extract()}>{busy ? (en ? 'Reading linked document…' : 'Bağlı belge okunuyor…') : (en ? 'Extract from linked document' : 'Bağlı belgeden bilgileri çıkar')}</Button> : <>
        <p>{en ? 'All suggestions require review. Missing values remain unknown. Only selected corrected facts will update the draft.' : 'Tüm öneriler inceleme gerektirir. Eksik değerler bilinmiyor kalır. Yalnız seçilen ve düzeltilen bilgiler taslağı günceller.'}</p>
        <form noValidate onSubmit={event => { event.preventDefault(); void confirm() }} aria-label={en ? 'Confirm extracted certificate facts' : 'Çıkarılan sertifika bilgilerini onayla'}>
          <div className="certificate-extraction-facts">{current.candidates.map(field => {
            const writable = field.key !== 'person_reference' && field.key !== 'vessel_reference', key = field.key as ConfirmableField, label = labels[field.key][en ? 0 : 1]
            return <div className={`certificate-extraction-fact${field.lowConfidence ? ' certificate-extraction-review' : ''}`} key={field.key}>
              <div><strong>{label}</strong><span>{field.confidence === null ? (en ? 'Confidence unavailable' : 'Güven skoru yok') : `${Math.round(field.confidence * 100)}%`}{field.page !== null && ` · ${en ? 'Page' : 'Sayfa'} ${field.page}`}</span><Badge variant="outline">{field.lowConfidence ? (en ? 'Low confidence · review' : 'Düşük güven · incele') : (en ? 'Review required' : 'İnceleme gerekli')}</Badge></div>
              {writable ? <><label htmlFor={`${id}-${field.key}`}>{en ? 'Reviewed value' : 'İncelenen değer'} · {label}</label><input id={`${id}-${field.key}`} value={values[key] ?? ''} maxLength={512} disabled={busy || !!pending || !current.canConfirm} onChange={event => { setValues(previous => ({ ...previous, [key]: event.target.value })); setSelected(previous => previous.filter(f => f !== key)) }} /><label><input type="checkbox" checked={selected.includes(key)} disabled={busy || !!pending || !current.canConfirm} onChange={event => setSelected(previous => event.target.checked ? [...previous, key] : previous.filter(f => f !== key))} />{en ? 'I checked this fact against the source' : 'Bu bilgiyi kaynakla karşılaştırdım'} · {label}</label></> : <p>{field.value ?? (en ? 'Not extracted' : 'Çıkarılmadı')} · {en ? 'Context only; cannot change ownership' : 'Yalnız bağlam; sahipliği değiştirmez'}</p>}
              <details><summary>{en ? 'Review reasons' : 'İnceleme nedenleri'}</summary><ul>{field.reasons.map(reason => <li key={reason}><code>{reason}</code></li>)}</ul></details>
            </div>
          })}</div>
          <label><input type="checkbox" checked={contextConfirmed} disabled={busy || !!pending || !current.canConfirm} onChange={event => setContextConfirmed(event.target.checked)} />{en ? 'I checked that this document belongs to this vessel and certificate' : 'Belgenin bu gemi ve sertifikaya ait olduğunu kontrol ettim'}</label>
          {validation && <p role="alert">{en ? 'Confirm vessel context and select at least one nonempty reviewed fact. Dates must use YYYY-MM-DD.' : 'Gemi bağlamını onaylayın ve en az bir dolu, incelenmiş bilgi seçin. Tarihler YYYY-AA-GG biçiminde olmalıdır.'}</p>}
          {!current.canConfirm && <p>{en ? 'This source is review-only; no owning confirmation workflow is available.' : 'Bu kaynak yalnız inceleme içindir; sahip kayıt için onay akışı kullanılamıyor.'}</p>}
          <EnterpriseActions><Button type="button" variant="outline" disabled={busy || !!pending} onClick={() => { setPreview(null); setValues({}); setSelected([]); setContextConfirmed(false); setFailure(null); setValidation(false) }}>{en ? 'Discard suggestions' : 'Önerileri bırak'}</Button><Button type="submit" disabled={busy || !current.canConfirm || !!pending && !samePending}>{pending ? (en ? 'Retry exact confirmation' : 'Aynı onayı tekrar dene') : (en ? 'Confirm selected facts and save draft' : 'Seçilen bilgileri onayla ve taslağı kaydet')}</Button></EnterpriseActions>
        </form>
        <details><summary>{en ? 'Extraction provenance' : 'Çıkarım kaynağı'}</summary><dl><dt>{en ? 'Model / prompt' : 'Model / istem'}</dt><dd>{current.modelVersion} / {current.promptVersion}</dd><dt>{en ? 'Policy / expires' : 'Politika / son süre'}</dt><dd>{current.policyVersion} / <time dateTime={current.expiresAt}>{current.expiresAt}</time></dd><dt>{en ? 'Controlled source' : 'Kontrollü kaynak'}</dt><dd><code>{current.sourceVersion}</code></dd></dl></details>
      </>}
    </>}
  </section>
}
