import { useEffect, useId, useLayoutEffect, useRef, useState } from 'react'
import { Button } from '../components/ui/button'
import { EnterpriseActions, EnterpriseField, EnterpriseFormGrid, EnterprisePagination } from './EnterprisePatterns'
import { ApiError, session, type Session } from './api'
import { isGuid } from './identifiers'
import { certificatesApi, type CertificateDto } from './certificates-api'
import { certificateAssertionApi, freezeCertificateAssertion, type CertificateAssertionAttempt } from './certificate-assertion-api'
import type { CertificateAssertionCapabilities, CertificateAssertionPage, CertificateAssertionState, CertificateVerification } from './certificate-assertion-contract'
import { CertificateAssertionSummary } from './CertificateAssertionSummary'
import { assertionStateLabel } from './certificate-copy'
import { useWorkspaceRevalidation } from './use-workspace-revalidation'
type Props = { vessel: string; record: CertificateDto; enabled: boolean; language: 'en' | 'tr'; onDirty: (value: boolean) => void; onSaving: (value: boolean) => void; onRecorded: (value: CertificateVerification) => void }
type Draft = { owner: string; version: number; revision: number; authorityId: string; state: CertificateAssertionState | ''; rationale: string }
const identity = (user: Session | undefined) => user?.authenticated && user.canRead && isGuid(user.actorId) && isGuid(user.tenantId) ? user.tenantId + '/' + user.actorId : null
export function CertificateAssertionPanel({ vessel, record, enabled, language, onDirty, onSaving, onRecorded }: Props) {
  const en = language === 'en', id = useId(), epoch = useWorkspaceRevalidation(), resource = vessel + '/' + record.id + '/' + record.version
  const available = enabled && navigator.onLine && document.visibilityState !== 'hidden'
  const [activation, setActivation] = useState({ resource, available, generation: 0 })
  if (activation.resource !== resource || activation.available !== available) setActivation({ resource, available, generation: activation.generation + 1 })
  const [offset, setOffset] = useState(0), [attempt, retry] = useState(0), scope = resource + '/' + activation.generation + '/' + epoch + '/' + attempt + '/' + offset + '/' + available
  const [source, setSource] = useState<{ scope: string; user?: Session; record?: CertificateDto; caps?: CertificateAssertionCapabilities; page?: CertificateAssertionPage; failed?: boolean } | null>(null)
  const [draft, setDraft] = useState<Draft | null>(null), [pending, setPending] = useState<CertificateAssertionAttempt | null>(null), [busy, setBusy] = useState(false), [validation, setValidation] = useState(false), [failure, setFailure] = useState<ApiError | null>(null), [saved, setSaved] = useState<{ owner: string; value: CertificateVerification } | null>(null)
  const mounted = useRef(false), running = useRef(false), writer = useRef<AbortController | null>(null), committed = useRef({ owner: null as string | null, scope: '' })
  const current = available && source?.scope === scope ? source : null, owner = identity(current?.user), caps = current?.caps
  const sameOwner = !draft || draft.owner === owner, samePending = !pending || pending.owner === owner
  const samePin = !!current?.record && current.record.version === record.version && current.record.revision === record.revision && (!draft || draft.version === record.version && draft.revision === record.revision)
  const retained = !!draft || !!pending
  const allowed = caps?.authorities.find(authority => authority.id === draft?.authorityId)
  const mayRecord = !!owner && !!caps?.canRecord && sameOwner && samePin && samePending && !busy && !pending && failure?.status !== 409
  useLayoutEffect(() => { committed.current = { owner, scope }; if (!available) writer.current?.abort() }, [owner, scope, available])
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; writer.current?.abort() } }, [])
  useEffect(() => { onDirty(retained) }, [retained, onDirty])
  useEffect(() => { onSaving(busy || !!pending) }, [busy, pending, onSaving])
  useEffect(() => {
    if (!available) return
    const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 10000)
    async function load() {
      const user = await session(c.signal), originalOwner = identity(user)
      if (!originalOwner) throw new ApiError(403)
      const actual = await certificatesApi.read(vessel, record.id, c.signal)
      const [caps, page] = await Promise.all([certificateAssertionApi.capabilities(vessel, record.id, c.signal), certificateAssertionApi.history(vessel, record.id, offset, c.signal)])
      if (identity(await session(c.signal)) !== originalOwner || actual.scope !== 'Vessel' || caps.certificateRevision !== actual.revision || caps.certificateVersion !== actual.version) throw new ApiError(403, 'identity_changed')
      if (active) setSource({ scope, user, record: actual, caps, page })
    }
    load().catch(() => { if (active) setSource({ scope, failed: true }) }).finally(() => clearTimeout(timer))
    return () => { active = false; clearTimeout(timer); c.abort() }
  }, [available, scope, vessel, record.id, offset])
  const edit = (patch: Partial<Draft>) => {
    if (!mayRecord || !owner) return
    setDraft(previous => ({ owner, version: record.version, revision: record.revision, authorityId: '', state: '', rationale: '', ...previous, ...patch }))
    onDirty(true); setValidation(false); setSaved(null)
  }
  async function save() {
    if (!current?.user || !owner || !caps || !sameOwner || !samePending || running.current || busy || !available) return
    let command: CertificateAssertionAttempt
    try {
      if (pending) command = pending
      else {
        if (!draft || !draft.state || !mayRecord) throw new ApiError(400)
        command = freezeCertificateAssertion(record, caps, owner, draft.authorityId, draft.state, draft.rationale)
      }
    } catch { setValidation(true); return }
    const wasUnknown = !!pending, originalScope = scope
    setPending(command); setBusy(true); onDirty(true); onSaving(true); setValidation(false); setFailure(null); running.current = true
    const c = new AbortController(); writer.current = c; const timer = setTimeout(() => c.abort(), 20000); let sent = false, receiptReceived = false
    try {
      const fresh = await session(c.signal)
      if (identity(fresh) !== command.owner) throw new ApiError(403, 'identity_changed')
      const actual = await certificatesApi.read(vessel, record.id, c.signal), authority = await certificateAssertionApi.capabilities(vessel, record.id, c.signal)
      if (identity(await session(c.signal)) !== command.owner || !authority.canRecord || !authority.authorities.some(option => option.id === command.authorityId && option.roleId === command.roleId && option.states.includes(command.state))) throw new ApiError(403, 'authority_unavailable')
      // An old unknown command may recover its immutable receipt after a newer
      // revision exists. Do not replace its body/base with the current version.
      if (!wasUnknown && (actual.version !== command.certificateVersion || actual.revision !== command.certificateRevision || authority.certificateVersion !== command.certificateVersion || authority.certificateRevision !== command.certificateRevision)) throw new ApiError(409, 'source_revision_changed')
      sent = true
      const receipt = await certificateAssertionApi.record(command, fresh.csrfToken, c.signal)
      receiptReceived = true
      const after = await session(c.signal)
      if (!mounted.current || committed.current.scope !== originalScope || committed.current.owner !== command.owner || identity(after) !== command.owner) { if (mounted.current) setFailure(new ApiError(0, 'outcome_unknown')); return }
      setSaved({ owner: command.owner, value: receipt }); setPending(null); setDraft(null); onRecorded(receipt); retry(n => n + 1)
    } catch (error) {
      if (!mounted.current) return
      const problem = error instanceof ApiError ? error : new ApiError(0); setFailure(problem)
      if (!wasUnknown && (!sent || !receiptReceived && problem.status > 0 && problem.status < 500)) setPending(null)
      if ([401, 403, 404].includes(problem.status)) setSource({ scope, failed: true })
    } finally { clearTimeout(timer); writer.current = null; running.current = false; if (mounted.current) setBusy(false) }
  }
  const pendingAuthority = !pending || !!caps?.canRecord && caps.authorities.some(authority => authority.id === pending.authorityId && authority.roleId === pending.roleId)
  const show = available && !!owner && sameOwner && samePending && pendingAuthority
  return <section className="certificate-assertion-panel certificate-metadata-form" aria-label={en ? 'Certificate human assertion records' : 'Sertifika insan beyan kayıtları'} aria-busy={busy}>
    <h2>{en ? 'Human assertion record' : 'İnsan inceleme kaydı'}</h2>
    <p>{en ? 'Record an assigned Auditor’s claim against the exact saved certificate and file. The result remains recorded and unassessed; legal qualification and renewal completion stay unavailable.' : 'Atanmış denetçinin beyanını kesin kayıtlı sertifika ve dosyaya bağlayın. Sonuç kayıtlı ve değerlendirilmemiş kalır; hukuki nitelendirme ve yenileme tamamlaması kullanılamaz.'}</p>
    {current?.failed && <p role="alert">{en ? 'Human assertion source or access unavailable. No empty or approved history is inferred.' : 'İnsan beyan kaynağı veya erişimi kullanılamıyor. Boş veya onaylı geçmiş varsayılmaz.'}</p>}
    {!show ? <><p role="status">{en ? 'Assertion access is locked. Save or discard other work, then reauthorize the original identity and source online. Retained inputs and unknown commands do not transfer.' : 'Beyan erişimi kilitli. Diğer işi kaydedin veya bırakın, sonra özgün kimlik ve kaynağı çevrimiçi tekrar yetkilendirin. Korunan girdiler ve belirsiz komut aktarılmaz.'}</p><Button variant="outline" disabled={!available || busy} onClick={() => retry(n => n + 1)}>{en ? 'Reauthorize assertion source' : 'Beyan kaynağını tekrar yetkilendir'}</Button></> : <>
      {saved && saved.owner === owner && <CertificateAssertionSummary value={saved.value} language={language} />}
      {failure && <p role="alert">{pending ? (en ? 'Assertion outcome unknown. Preserve the original command and identity; retry explicitly after current authorization.' : 'Beyan sonucu bilinmiyor. Özgün komut ve kimliği koruyun; güncel yetkilendirme sonrası açıkça tekrar deneyin.') : failure.status === 409 ? (en ? 'The saved source or assertion version changed. Local reason remains retained; reload or discard explicitly.' : 'Kayıtlı kaynak veya beyan sürümü değişti. Yerel gerekçe korunur; açıkça yeniden okuyun veya bırakın.') : (en ? 'Assertion source or current authority unavailable. No assertion was inferred.' : 'Beyan kaynağı veya güncel yetki kullanılamıyor. Beyan varsayılmadı.')}</p>}
      {!samePin && <p role="alert">{en ? 'The saved certificate changed. Original local reason and version remain retained; this form cannot attest the new revision.' : 'Kayıtlı sertifika değişti. Özgün yerel gerekçe ve sürüm korunur; form yeni sürüm için beyan veremez.'}</p>}
      {(failure?.status === 409 || !samePin) && <Button variant="outline" disabled={busy || !!pending} onClick={() => { setFailure(null); retry(n => n + 1) }}>{en ? 'Reload assertion source' : 'Beyan kaynağını yeniden oku'}</Button>}
      {!caps?.canRecord && <p>{en ? 'No current assigned human authority with the required source file is available. An administrator role alone does not permit recording.' : 'Gerekli kaynak dosyasıyla güncel atanmış insan yetkisi yok. Yönetici rolü tek başına kayıt izni vermez.'}</p>}
      <form noValidate aria-label={en ? 'Record certificate assertion' : 'Sertifika beyanını kaydet'} onSubmit={event => { event.preventDefault(); void save() }}>
        <EnterpriseFormGrid disabled={!mayRecord}><legend>{en ? 'Saved source assertion' : 'Kayıtlı kaynak beyanı'}</legend>
          <EnterpriseField htmlFor={id + '-authority'} label={en ? 'Assigned Auditor authority' : 'Atanmış denetçi yetkisi'}><select id={id + '-authority'} aria-required="true" aria-invalid={validation && !draft?.authorityId} aria-describedby={validation ? id + '-errors' : undefined} value={draft?.authorityId ?? ''} onChange={event => edit({ authorityId: event.target.value, state: '' })}><option value="">{en ? 'Select actual assignment' : 'Gerçek atamayı seç'}</option>{caps?.authorities.map((authority, index) => <option key={authority.id} value={authority.id}>{en ? 'Assigned Auditor' : 'Atanmış denetçi'} {index + 1} · {authority.roleId.slice(-8)}</option>)}</select></EnterpriseField>
          <EnterpriseField htmlFor={id + '-state'} label={en ? 'Human assertion state' : 'İnsan beyan durumu'}><select id={id + '-state'} aria-required="true" aria-invalid={validation && !draft?.state} aria-describedby={validation ? id + '-errors' : undefined} value={draft?.state ?? ''} onChange={event => { const state = allowed?.states.find(state => state === event.target.value); edit({ state: state ?? '' }) }}><option value="">{en ? 'Select recorded claim' : 'Kayıtlı beyanı seç'}</option>{allowed?.states.map(state => <option key={state} value={state}>{assertionStateLabel(state, en)}</option>)}</select></EnterpriseField>
          <EnterpriseField htmlFor={id + '-reason'} label={en ? 'Reason and source review' : 'Gerekçe ve kaynak incelemesi'}><textarea id={id + '-reason'} value={draft?.rationale ?? ''} maxLength={2000} rows={4} aria-required="true" aria-invalid={validation} aria-describedby={validation ? id + '-errors' : undefined} onChange={event => edit({ rationale: event.target.value })} /></EnterpriseField>
        </EnterpriseFormGrid>
        {validation && <p role="alert" id={id + '-errors'}>{en ? 'Choose an assigned authority and claim, and enter the source review reason (maximum 2,000 characters).' : 'Atanmış yetki ve beyanı seçin, kaynak inceleme gerekçesini yazın (en fazla 2.000 karakter).'}</p>}
        <EnterpriseActions aria-label={en ? 'Assertion form actions' : 'Beyan form işlemleri'}><Button variant="outline" disabled={busy || !!pending || !draft} onClick={() => { setDraft(null); setValidation(false); setFailure(null); onDirty(false); retry(n => n + 1) }}>{en ? 'Discard assertion inputs' : 'Beyan girdilerini bırak'}</Button><Button type="submit" disabled={busy || !owner || !caps?.canRecord || !samePending || !pending && !mayRecord}>{pending ? (en ? 'Retry exact assertion command' : 'Aynı beyan komutunu tekrar dene') : (en ? 'Record unassessed assertion' : 'Değerlendirilmemiş beyanı kaydet')}</Button></EnterpriseActions>
      </form>
      <section aria-label={en ? 'Immutable human assertion history' : 'Değişmez insan beyan geçmişi'}><h3>{en ? 'Recorded assertion history' : 'Kayıtlı beyan geçmişi'}</h3>{!current?.page?.items.length && <p>{en ? 'No authorized assertion rows returned on this page.' : 'Bu sayfada yetkili beyan satırı dönmedi.'}</p>}{current?.page?.items.map(value => <details key={value.id}><summary>{en ? 'Assertion revision' : 'Beyan sürümü'} {value.revision} · {value.savedAt}</summary><CertificateAssertionSummary value={value} language={language} /></details>)}<EnterprisePagination label={en ? 'Human assertion history pages' : 'İnsan beyan geçmiş sayfaları'} previous={en ? 'Previous assertions' : 'Önceki beyanlar'} next={en ? 'More assertions' : 'Diğer beyanlar'} previousDisabled={!offset || retained || busy} nextDisabled={!current?.page?.hasMore || retained || busy} onPrevious={() => setOffset(n => Math.max(0, n - 25))} onNext={() => setOffset(n => n + 25)} /></section>
    </>}
  </section>
}
