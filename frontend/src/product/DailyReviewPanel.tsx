import { useEffect, useId, useLayoutEffect, useRef, useState } from 'react'
import { Button } from '../components/ui/button'
import { Badge } from '../components/ui/badge'
import { EnterpriseActions, EnterprisePagination } from './EnterprisePatterns'
import { ApiError, session as readSession, type Session } from './api'
import { isGuid } from './identifiers'
import type { DailyRecord } from './daily-operations-api'
import { dailyReviewApi, freezeDailyReview, reviewRationale, type DailyReview, type DailyReviewAttempt, type DailyReviewCapabilities, type DailyReviewChoice, type DailyReviewPage } from './daily-review-api'
import { useWorkspaceRevalidation } from './use-workspace-revalidation'
import { dailyReviewDuty, reviewBlocker, dailyReviewChoiceLabel as reviewChoice } from './daily-review-copy'
import './daily-operations.css'
export type DailyReviewPanelProps = { owner: string; session: Session | null; record: DailyRecord; language: 'en' | 'tr'; enabled: boolean; onDirty: (value: boolean) => void; onSaving: (value: boolean) => void; onCommitted: () => void; onReloadRecord: () => void }
type Source = { scope: string; caps?: DailyReviewCapabilities; history?: DailyReviewPage; user?: Session; failed?: boolean; differentIdentity?: boolean }
const boundary = (user: Session | null | undefined) => user?.authenticated && user.canRead && isGuid(user.tenantId) && isGuid(user.actorId) ? `${user.tenantId}/${user.actorId}` : null
export function DailyReviewPanel({ owner, session, record, language, enabled, onDirty, onSaving, onCommitted, onReloadRecord }: DailyReviewPanelProps) {
  const en = language === 'en', epoch = useWorkspaceRevalidation(), id = useId(), [offset, setOffset] = useState(0), [attempt, retry] = useState(0), [source, setSource] = useState<Source | null>(null)
  const [authorityId, setAuthority] = useState(''), [decision, setDecision] = useState<DailyReviewChoice | ''>(''), [rationale, setRationale] = useState(''), [validation, setValidation] = useState(false), [failure, setFailure] = useState<ApiError | null>(null), [notice, setNotice] = useState(false)
  const [frozen, setFrozen] = useState<{ command: DailyReviewAttempt; uncertain: boolean } | null>(null), [busy, setBusy] = useState(false)
  const mounted = useRef(false), running = useRef(false), writer = useRef<AbortController | null>(null), liveIdentity = useRef<string | null>(null)
  const online = navigator.onLine, visible = document.visibilityState !== 'hidden', identity = boundary(session)
  const [identityEpoch, setIdentityEpoch] = useState({ identity, epoch: 0 })
  if (identityEpoch.identity !== identity) setIdentityEpoch({ identity, epoch: identityEpoch.epoch + 1 })
  const scope = `${record.id}/${record.revision}/${record.version}/${identityEpoch.epoch}/${epoch}/${attempt}/${offset}`
  const mayRead = online && visible && identity === owner
  const current = mayRead && source?.scope === scope ? source : null, caps = current?.caps
  useLayoutEffect(() => { liveIdentity.current = current?.user ? boundary(current.user) : null }, [current])
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; writer.current?.abort() } }, [])
  useEffect(() => {
    if (!mayRead) return
    const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 10000)
    async function load() {
      const user = await readSession(c.signal)
      if (boundary(user) !== owner) { if (active) setSource({ scope, differentIdentity: true }); return }
      const [caps, history] = await Promise.all([dailyReviewApi.capabilities(record.vesselId, record.kind, record.id, c.signal), dailyReviewApi.history(record.vesselId, record.kind, record.id, offset, c.signal)])
      if (active) { setSource({ scope, user, caps, history }); if (caps.canRecord) setFailure(previous => previous && [401, 403, 404].includes(previous.status) ? null : previous) }
    }
    load().catch(() => { if (active) setSource({ scope, failed: true }) }).finally(() => clearTimeout(timer))
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [record.id, record.vesselId, record.kind, owner, scope, offset, mayRead])
  const sameRecord = !!caps && caps.recordRevision === record.revision && caps.recordVersion === record.version
  const chosen = caps?.authorities.find(a => a.id === (frozen?.command.authority.id ?? authorityId))
  const originalAuthority = !frozen || !!chosen && chosen.roleId === frozen.command.authority.roleId && chosen.duty === frozen.command.authority.duty
  const canSend = enabled && mayRead && !!current?.user && !!caps?.canRecord && !!chosen && originalAuthority && (frozen ? frozen.command.owner === owner : sameRecord) && !busy && (failure?.status !== 409 || !!frozen)
  const editing = enabled && mayRead && !!current?.user && !!caps?.canRecord && sameRecord && !busy && !frozen && failure?.status !== 409
  const change = (nextAuthority = authorityId, nextDecision: DailyReviewChoice | '' = decision, nextRationale = rationale) => { onDirty(nextAuthority !== '' || nextDecision !== '' || nextRationale !== ''); setNotice(false); setFailure(null) }
  const dirty = authorityId !== '' || decision !== '' || rationale !== ''
  async function save() {
    if (running.current || !canSend || !caps || !current?.user) return
    let command: DailyReviewAttempt
    try {
      if (frozen) command = frozen.command
      else {
        const actorId = current.user.actorId, tenantId = current.user.tenantId
        if (!isGuid(actorId) || !isGuid(tenantId) || decision !== 'REVIEWED' && decision !== 'RETURNED') { setValidation(true); return }
        command = freezeDailyReview({ record, caps, actorId, tenantId, authorityId, decision, rationale })
      }
    }
    catch { setValidation(true); return }
    const wasUnknown = frozen?.uncertain ?? false
    running.current = true; setBusy(true); onSaving(true); setFrozen({ command, uncertain: wasUnknown }); setFailure(null); setValidation(false); setNotice(false)
    const c = new AbortController(); writer.current = c; const timer = setTimeout(() => c.abort(), 15000); let uncertain = false
    try {
      const user = await readSession(c.signal)
      if (boundary(user) !== command.owner) { setSource({ scope, differentIdentity: true }); throw new ApiError(403, 'identity_boundary_changed') }
      await dailyReviewApi.record(command, user.csrfToken, c.signal)
      if (!mounted.current) return
      if (liveIdentity.current !== command.owner) { uncertain = true; setFrozen({ command, uncertain: true }); setFailure(new ApiError(0, 'identity_boundary_changed')); return }
      setFrozen(null); setAuthority(''); setDecision(''); setRationale(''); onDirty(false); setNotice(true); setOffset(0); retry(n => n + 1); onCommitted()
    } catch (error) {
      if (!mounted.current) return
      const problem = error instanceof ApiError ? error : new ApiError(0)
      uncertain = wasUnknown || problem.status === 0 || problem.status >= 500
      setFrozen(uncertain ? { command, uncertain: true } : null); setFailure(problem)
      if ([401, 403, 404].includes(problem.status)) retry(n => n + 1)
    } finally { clearTimeout(timer); writer.current = null; running.current = false; if (mounted.current) { setBusy(false); onSaving(uncertain) } }
  }
  const hidden = !mayRead || current?.differentIdentity
  return <section className="daily-review-panel" aria-label={en ? 'Human review recording' : 'İnsan inceleme kaydı'}><h3>{en ? 'Human review recording' : 'İnceleme kaydı'}</h3><p>{en ? 'Record a human review of an exact saved revision. This does not establish verification, submission, Master safety approval or Chief Engineer sign-off.' : 'Kesin kayıtlı sürüm için insan incelemesini kaydedin. Bu kayıt doğrulama, gönderim, kaptan emniyet onayı veya başmühendis imzası oluşturmaz.'}</p>
    {hidden ? <p role="status">{en ? 'Review sources locked while offline, hidden or outside the original verified identity. Retained inputs and unknown commands are not transferred.' : 'Çevrimdışı, gizli veya özgün doğrulanmış kimlik dışında inceleme kaynakları kilitli. Korunan girdiler ve belirsiz komutlar aktarılmaz.'}</p> : !current ? <p role="status">{en ? 'Loading review sources…' : 'İnceleme kaynakları yükleniyor…'}</p> : current.failed || !caps ? <p role="alert">{en ? 'Review source or permission unavailable. No empty history or approval is inferred.' : 'İnceleme kaynağı veya yetkisi kullanılamıyor. Boş geçmiş veya onay varsayılmaz.'}</p> : <>
      {notice && <p role="status">{en ? 'Human review recorded · unassessed. Current operational approval remains separate.' : 'İnsan incelemesi kaydedildi · değerlendirilmemiş. Güncel operasyon onayı ayrıdır.'}</p>}
      <p>{en ? 'Displayed saved revision' : 'Görüntülenen kayıtlı sürüm'}: <strong>{record.revision}</strong> · <time dateTime={record.savedAt}>{record.savedAt}</time></p><details><summary>{en ? 'Exact saved record reference' : 'Kesin kayıtlı sürüm referansı'}</summary><code>{record.hash}</code></details>
      {!caps.canRecord && <p>{en ? 'No current source-authorized human appointment is available for this record.' : 'Bu kayıt için güncel, kaynakça yetkilendirilmiş insan görevlendirmesi kullanılamıyor.'}</p>}
      {!sameRecord && <div role="alert"><p>{en ? 'The saved record revision changed. The retained rationale targets its original revision; read the current draft before a new review.' : 'Kayıtlı sürüm değişti. Korunan gerekçe özgün sürümünü hedefler; yeni incelemeden önce güncel taslağı okuyun.'}</p><Button variant="outline" disabled={busy || !!frozen} onClick={onReloadRecord}>{en ? 'Read current saved draft' : 'Güncel kayıtlı taslağı oku'}</Button></div>}
      {!enabled && <p>{en ? 'Resolve or explicitly discard the other pending form before recording a review.' : 'İnceleme kaydetmeden önce diğer bekleyen formu çözün veya açıkça bırakın.'}</p>}
      {failure && <p role="alert">{frozen ? (en ? 'Review outcome unknown. The original key, body and identity remain frozen; only explicitly retry that same command after authorization.' : 'İnceleme sonucu bilinmiyor. Özgün anahtar, içerik ve kimlik kilitli korunur; yetkilendirme sonrası yalnız aynı komutu açıkça tekrar deneyin.') : failure.status === 409 ? (en ? 'Review context changed. Your rationale is retained; explicitly reload or discard before another attempt.' : 'İnceleme bağlamı değişti. Gerekçeniz korunur; başka denemeden önce açıkça yenileyin veya bırakın.') : (en ? 'Review recording was not confirmed. Check current authority, selection and rationale.' : 'İnceleme kaydı doğrulanmadı. Güncel yetkiyi, seçimi ve gerekçeyi kontrol edin.')}</p>}
      <details><summary>{en ? 'Review policy and source references' : 'İnceleme politikası ve kaynak referansları'}</summary><p>{en ? 'Saved revision / review concurrency version' : 'Kayıtlı sürüm / inceleme eşzamanlılık sürümü'}: {caps.recordRevision} / {caps.reviewVersion}</p><code>{caps.sourceVersion}</code><ul>{caps.blockers.map((code, i) => <li key={`${code}/${i}`}>{reviewBlocker(code, en)} <code>{code}</code></li>)}</ul>{caps.authorities.map(a => <p key={a.id}>{dailyReviewDuty(a.duty, en)} · {en ? 'Appointment / role references' : 'Görevlendirme / rol referansları'}: {a.id} / {a.roleId}</p>)}</details>
      {(caps.canRecord || frozen || dirty) && <form noValidate aria-label={en ? 'Review recording form' : 'İnceleme kayıt formu'} aria-busy={busy} onSubmit={event => { event.preventDefault(); void save() }}><div className="enterprise-form-grid"><div className="enterprise-form-field"><label htmlFor={`${id}-authority`}>{en ? 'Authorized human appointment' : 'Yetkili insan görevlendirmesi'}</label><select id={`${id}-authority`} disabled={!editing} aria-invalid={validation && !authorityId} value={authorityId} onChange={event => { setAuthority(event.target.value); setDecision(''); change(event.target.value, '', rationale) }}><option value="">{en ? 'Choose an appointment' : 'Görevlendirme seçin'}</option>{caps.authorities.map(a => <option key={a.id} value={a.id}>{dailyReviewDuty(a.duty, en)} · {a.id.slice(-8)}</option>)}</select></div><div className="enterprise-form-field"><label htmlFor={`${id}-decision`}>{en ? 'Review entry' : 'İnceleme kaydı türü'}</label><select id={`${id}-decision`} disabled={!editing || !chosen} aria-invalid={validation && !decision} value={decision} onChange={event => { setDecision(event.target.value as DailyReviewChoice | ''); change(authorityId, event.target.value as DailyReviewChoice | '', rationale) }}><option value="">{en ? 'Choose a record type' : 'Kayıt türü seçin'}</option>{chosen?.decisions.map(value => <option key={value} value={value}>{reviewChoice(value, en)}</option>)}</select></div></div><label htmlFor={`${id}-rationale`}>{en ? 'Review rationale' : 'İnceleme gerekçesi'}</label><textarea id={`${id}-rationale`} value={rationale} disabled={!editing} maxLength={2000} rows={4} aria-invalid={validation && !reviewRationale(rationale)} aria-describedby={validation ? `${id}-errors` : undefined} onChange={event => { setRationale(event.target.value); change(authorityId, decision, event.target.value) }} />{validation && <p id={`${id}-errors`} role="alert">{en ? 'Choose an authorized appointment and record type, and enter a rationale.' : 'Yetkili görevlendirme ve kayıt türü seçin, gerekçe girin.'}</p>}<EnterpriseActions aria-label={en ? 'Review recording actions' : 'İnceleme kayıt işlemleri'}><Button variant="outline" disabled={busy || !!frozen} onClick={onReloadRecord}>{en ? 'Cancel review entry' : 'İnceleme girişini iptal et'}</Button><Button type="submit" disabled={!canSend}>{frozen ? (en ? 'Retry exact review command' : 'Aynı inceleme komutunu tekrar dene') : busy ? (en ? 'Recording…' : 'Kaydediliyor…') : (en ? 'Record review entry' : 'İnceleme kaydını oluştur')}</Button></EnterpriseActions></form>}
      <section aria-label={en ? 'Immutable review history' : 'Değişmez inceleme geçmişi'}><h4>{en ? 'Recorded review history' : 'İnceleme kayıt geçmişi'}</h4>{current.history?.items.length ? current.history.items.map(entry => <ReviewEntry key={entry.id} entry={entry} currentRevision={caps.recordRevision} en={en} />) : <p>{en ? 'No authorized review entries returned on this page. This does not prove review completion.' : 'Bu sayfada yetkili inceleme kaydı dönmedi. Bu, incelemenin tamamlandığını göstermez.'}</p>}<EnterprisePagination label={en ? 'Review history pages' : 'İnceleme geçmiş sayfaları'} previous={en ? 'Previous review entries' : 'Önceki inceleme kayıtları'} next={en ? 'More review entries' : 'Diğer inceleme kayıtları'} previousDisabled={!offset || busy || dirty || !!frozen} nextDisabled={!current.history?.hasMore || offset >= 100000 || busy || dirty || !!frozen} onPrevious={() => setOffset(n => Math.max(0, n - 25))} onNext={() => setOffset(n => n + 25)} /></section>
    </>}
    <EnterpriseActions align="start" aria-label={en ? 'Review source actions' : 'İnceleme kaynak işlemleri'}><Button variant="outline" disabled={!online || !visible || busy} onClick={() => retry(n => n + 1)}>{en ? 'Refresh review sources' : 'İnceleme kaynaklarını yenile'}</Button></EnterpriseActions>
  </section>
}
function ReviewEntry({ entry, currentRevision, en }: { entry: DailyReview; currentRevision: number; en: boolean }) { return <details className="daily-review-entry"><summary>{reviewChoice(entry.decision, en)} · {en ? 'Revision' : 'Sürüm'} {entry.recordRevision} · {entry.savedAt}</summary><Badge tone="warning">{en ? 'Recorded · unassessed' : 'Kaydedilmiş · değerlendirilmemiş'}</Badge><p>{entry.recordRevision === currentRevision ? (en ? 'Recorded against the current saved revision. This is not operational approval.' : 'Güncel kayıtlı sürüm için kaydedilmiş. Bu operasyon onayı değildir.') : (en ? 'Historical revision. This entry does not review the current saved draft.' : 'Tarihsel sürüm. Bu kayıt güncel kayıtlı taslağın incelemesi değildir.')}</p><p>{dailyReviewDuty(entry.duty, en)}</p><p className="daily-review-rationale">{entry.rationale}</p><time dateTime={entry.savedAt}>{entry.savedAt}</time><details><summary>{en ? 'Immutable review references' : 'Değişmez inceleme referansları'}</summary><dl className="daily-source-grid"><div><dt>{en ? 'Actor / role / appointment' : 'Aktör / rol / görevlendirme'}</dt><dd>{entry.actorId} / {entry.roleId} / {entry.authorityId}</dd></div><div><dt>{en ? 'Sequence / event / version' : 'Sıra / olay / sürüm'}</dt><dd>{entry.sequence} / {entry.id} / {entry.version}</dd></div><div><dt>{en ? 'Recorded authority integrity reference' : 'Kayıtlı yetki bütünlük referansı'}</dt><dd><code>{entry.authorityHash}</code></dd></div><div><dt>{en ? 'Reviewed record integrity reference' : 'İncelenen kayıt bütünlük referansı'}</dt><dd><code>{entry.recordHash}</code></dd></div><div><dt>{en ? 'Controlled source / reason' : 'Kontrollü kaynak / gerekçe'}</dt><dd><code>{entry.sourceVersion}</code> / <code>{entry.reasonCode}</code></dd></div></dl></details></details> }
