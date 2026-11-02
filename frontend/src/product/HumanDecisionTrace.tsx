import { useEffect, useState } from 'react'
import * as service from './human-review-api'
import { ApiError } from './api'
import { timestamp } from './date-validation'

type Props = { anchor: service.Anchor; language: 'en' | 'tr'; csrf: string; offline: boolean; onDirty: (v: boolean) => void; onSaving: (v: boolean) => void }
export function HumanDecisionTrace({ anchor, language, csrf, offline, onDirty, onSaving }: Props) {
  const t = (en: string, tr: string) => language === 'en' ? en : tr
  const [data, setData] = useState<service.Trace | null>(null), [error, setError] = useState<number | null>(null)
  const [offset, setOffset] = useState(0), [asOf, setAsOf] = useState(''), [history, setHistory] = useState(''), [reload, setReload] = useState(0)
  const [rationale, setRationale] = useState(''), [authority, setAuthority] = useState(''), [selected, setSelected] = useState<service.Review | null>(null)
  const [decision, setDecision] = useState(false), [busy, setBusy] = useState(false), [conflict, setConflict] = useState(false)
  const [humanDecision, setHumanDecision] = useState(''), [decisionContext, setDecisionContext] = useState(''), [triggerSource, setTriggerSource] = useState('')
  const [recordingMode, setRecordingMode] = useState(false)
  const [attempt, setAttempt] = useState<Parameters<typeof service.save>[1] | null>(null), [notice, setNotice] = useState('')
  const dirty = !!rationale || !!humanDecision || !!decisionContext || !!triggerSource || !!attempt
  useEffect(() => { onDirty(dirty) }, [dirty, onDirty])
  useEffect(() => { onSaving(busy) }, [busy, onSaving])
  // Parent keys this component by the immutable anchor and guards context changes.
  const key = service.tracePath(anchor)
  useEffect(() => {
    setData(null); if (offline) return
    const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 15000)
    service.trace(anchor, offset, history, c.signal).then(v => { if (active) { setData(v); setError(null) } }).catch(e => { if (active) setError(e instanceof ApiError ? e.status : 0) })
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [key, offset, history, offline, reload]) // immutable anchor values form key
  const locked = offline || busy || conflict || !!history || !data || error !== null
  const grant = data?.authorities.find(g => g.id === authority)
  const capability = recordingMode ? 'human.decide' : decision ? 'human.review.submit' : selected ? (selected.kind === 'Decision' ? 'human.decide' : 'human.verify') : 'human.review.submit'
  const eligible = data?.authorities.filter(g => g.capability === capability && (!selected || g.operationId === selected.operationId)) ?? []
  function choose(row: service.Review | null, requestDecision = false, record = false) { if (dirty || busy) return; setSelected(row); setDecision(requestDecision); setRecordingMode(record); setAuthority(''); setConflict(false) }
  function clearRecording() { setHumanDecision(''); setDecisionContext(''); setTriggerSource(''); setRecordingMode(false) }
  async function command(state: string, reasonCode: string) {
    if (locked || !grant || !eligible.some(g => g.id === grant.id) || !rationale.trim() || rationale !== rationale.trim() || rationale.length > 2000) return
    if (recordingMode && (!grant.recording || grant.recording.requiredGate !== null || [humanDecision, decisionContext, triggerSource].some(v => !v.trim() || v !== v.trim() || v.length > 2000))) return
    const kind = decision || selected?.kind === 'Decision' ? 'Decision' : 'Verification'
    const frozen = attempt ?? { kind, id: decision ? null : selected?.id ?? null, key: crypto.randomUUID(), body: JSON.stringify({
      expectedVersion: decision ? 0 : selected?.version ?? 0, authorityId: grant.id, operationId: grant.operationId,
      evidenceId: anchor.evidenceId, evidenceRevision: anchor.evidenceRevision, evidenceVersion: data!.evidenceVersion,
      controlId: anchor.controlId, controlRevision: anchor.controlRevision, controlVersion: data!.controlVersion,
      evaluationId: selected?.evaluationId ?? null, verificationId: decision ? selected?.id : selected?.verificationId ?? null,
      verificationRevision: decision ? selected?.revision : selected?.verificationRevision ?? null, state, reasonCode, rationale,
      expectedProfileRevision: recordingMode ? data!.profileRevision : null,
      recording: recordingMode ? { decisionType: grant.recording!.decisionType, subjectType: grant.recording!.subjectType, subjectId: grant.recording!.subjectId, humanDecision, decisionContext, triggerSource } : null,
    }) } satisfies Parameters<typeof service.save>[1]
    setAttempt(frozen); setBusy(true); setError(null)
    const c = new AbortController(); const timer = setTimeout(() => c.abort(), 15000)
    try {
      await service.save(anchor, frozen, csrf, c.signal); setAttempt(null); setRationale(''); clearRecording(); setSelected(null); setDecision(false); setAuthority(''); setReload(v => v + 1)
      setNotice(t('Human review recorded. Operational approval remains blocked.', 'İnsan incelemesi kaydedildi. Operasyonel onay engelli.'))
    } catch (e) {
      const status = e instanceof ApiError ? e.status : 0; setError(status); if (status === 409) setConflict(true)
      setNotice(t('Outcome unresolved. Retain this exact attempt for retry; cancellation does not undo a committed command.', 'Sonuç belirsiz. Tekrar için aynı denemeyi koruyun; iptal kaydedilmiş komutu geri almaz.'))
    } finally { setBusy(false); clearTimeout(timer) }
  }
  async function retry() {
    if (!attempt || offline || busy || conflict || error === 401 || error === 403 || error === 404) return
    setBusy(true); const c = new AbortController(); const timer = setTimeout(() => c.abort(), 15000)
    try { await service.save(anchor, attempt, csrf, c.signal); setAttempt(null); setRationale(''); clearRecording(); setSelected(null); setAuthority(''); setDecision(false); setError(null); setReload(v => v + 1) }
    catch (e) { const status = e instanceof ApiError ? e.status : 0; setError(status); if (status === 409) setConflict(true) }
    finally { setBusy(false); clearTimeout(timer) }
  }
  const label: Record<string, string> = {
    RECORDED: t('Human decision recorded', 'İnsan kararı kaydedildi'),
    PENDING: t('Pending', 'Bekliyor'), VERIFIED: t('Human verified', 'İnsan doğrulaması'), REJECTED: t('Rejected', 'Reddedildi'), EXPIRED: t('Expired', 'Süresi doldu'), SUPERSEDED: t('Superseded', 'Yeni sürüm var'),
    EXPIRED_REVIEW_REQUIRED: t('Expired — review required', 'Süresi doldu — inceleme gerekli'), SUPERSEDED_REVIEW_REQUIRED: t('Inputs changed — review required', 'Girdiler değişti — inceleme gerekli'),
  }
  const why: Record<string, string> = {
    SOURCE_ADMISSION_UNAVAILABLE: t('Source admission unavailable', 'Kaynak kabulü kullanılamıyor'), AUTHORITATIVE_EVALUATION_UNAVAILABLE: t('Authoritative evaluation unavailable', 'Yetkili değerlendirme kullanılamıyor'),
    EXTERNAL_SIGNATURE_UNAVAILABLE: t('External signature mechanism unavailable', 'Harici imza mekanizması kullanılamıyor'), REQUIREMENT_LINK_MISSING: t('Requirement link missing', 'Gereksinim bağlantısı eksik'),
    REVISION_UNAVAILABLE: t('Revision unavailable', 'Sürüm kullanılamıyor'), COMPLETION_AND_EFFECTIVENESS_UNKNOWN: t('Completion and effectiveness unknown', 'Tamamlanma ve etkinlik bilinmiyor'), EXISTENCE_IS_NOT_VERIFICATION: t('Evidence presence is separate from verification', 'Kanıt varlığı doğrulamadan ayrıdır'), HISTORICAL_INPUTS_ONLY: t('Retained historical inputs only', 'Yalnız saklanan tarihsel girdiler'),
  }
  const segmentKind: Record<string, string> = { Source: t('Source', 'Kaynak'), Requirement: t('Requirement', 'Gereksinim'), Rule: t('Rule', 'Kural'), Applicability: t('Applicability', 'Uygulanabilirlik'), Control: t('Control', 'Kontrol'), Evidence: t('Evidence', 'Kanıt'), Evaluation: t('Evaluation', 'Değerlendirme') }
  const segmentState: Record<string, string> = { DRAFT: t('Draft', 'Taslak'), UNKNOWN: t('Unknown', 'Bilinmiyor'), REVIEW_REQUIRED: t('Review required', 'İnceleme gerekli'), RETAINED: t('Retained history', 'Saklanan geçmiş') }
  return <section className="human-review" aria-label={t('Human review and decision trace', 'İnsan incelemesi ve karar izi')}>
    <h3>{t('Human review and decision trace', 'İnsan incelemesi ve karar izi')}</h3>
    <p>{t('Review required. Verification cannot activate a permit or replace a human safety decision.', 'İnceleme gerekli. Doğrulama izin etkinleştiremez veya insan emniyet kararının yerini alamaz.')}</p>
    {offline && <p role="status">{t('Offline. Human review requires server connectivity.', 'Çevrimdışı. İnsan incelemesi sunucu bağlantısı gerektirir.')}</p>}
    {error !== null && <p role="alert">{error === 403 ? t('Permission unavailable or revoked.', 'Yetki yok veya kaldırıldı.') : error === 404 ? t('Context unavailable.', 'Bağlam kullanılamıyor.') : conflict ? t('Conflict. Reopen current context before a new command.', 'Çakışma. Yeni komuttan önce güncel bağlamı açın.') : t('Trace or command unavailable; result may be ambiguous.', 'İz veya komut kullanılamıyor; sonuç belirsiz olabilir.')}</p>}
    {!data && !offline && error === null && <p role="status">{t('Loading trace…', 'İz yükleniyor…')}</p>}
    <p role="status">{notice}</p>
    <label>{t('Trace as of with UTC offset', 'UTC farkıyla iz zamanı')}<input value={asOf} disabled={dirty || busy} onChange={e => setAsOf(e.target.value)} /></label>
    <button disabled={offline || dirty || busy || !!asOf && !timestamp(asOf)} onClick={() => { setHistory(asOf); setOffset(0); setSelected(null); setAuthority('') }}>{t('Read trace history', 'İz geçmişini oku')}</button>
    {data && <>
      <p>{t('Trace as of', 'İz zamanı')}: {data.asOf} · {data.historical ? t('Historical view', 'Tarihsel görünüm') : t('Current view', 'Güncel görünüm')}</p>
      <p>{t('Declared provenance', 'Beyan edilen köken')}: {data.evidenceContext.provenance ?? t('Unknown', 'Bilinmiyor')}</p>
      <p>{t('Observed / declared validity / declared freshness', 'Gözlem / beyan edilen geçerlilik / beyan edilen güncellik')}: {data.evidenceContext.observedAt ?? t('Unknown', 'Bilinmiyor')} / {data.evidenceContext.validUntil ?? t('Unknown', 'Bilinmiyor')} / {data.evidenceContext.freshUntil ?? t('Unknown', 'Bilinmiyor')}</p>
      <ul>{data.blockers.map(b => <li key={b}>{why[b]}</li>)}</ul>
      <ol>{data.segments.map((s, i) => <li key={i}><strong>{segmentKind[s.kind]}</strong> · {s.availability === 'Unavailable' ? t('Unavailable', 'Kullanılamıyor') : `${s.id}/${s.revision ?? '—'}`}<p>{segmentState[s.state]} · {why[s.why]} · {s.savedAt ?? t('Unknown', 'Bilinmiyor')}</p><code>{s.hash}</code></li>)}</ol>
      {data.items.length === 0 && <p>{t('No human review recorded in this context.', 'Bu bağlamda insan incelemesi kaydedilmedi.')}</p>}
      {data.items.map(row => <article key={`${row.id}/${row.revision}`}><h4>{row.kind === 'Verification' ? t('Verification', 'Doğrulama') : t('Decision', 'Karar')} · {row.revision} · {label[row.state]}</h4>
        <p>{label[row.currentState]} · {row.savedAt}</p><p>{t('Actor / role / authority / operation', 'Aktör / rol / yetki / operasyon')}: {row.actorId} / {row.roleId} / {row.authorityId} / {row.operationId}</p>
        <p>{row.rationale}</p><p>{t('Authority snapshot hash', 'Yetki anlık görüntü özeti')}: <code>{row.authorityHash}</code></p><code>{row.contextHash}</code><p>{t('Valid until', 'Geçerlilik sonu')}: {row.validUntil ?? t('Unknown', 'Bilinmiyor')}</p>
        {row.recording && <><p>{row.recording.content.humanDecision}</p><p>{row.recording.content.decisionContext} · {row.recording.content.triggerSource}</p><p>{row.recording.content.decisionType} · {row.recording.content.subjectType}/{row.recording.content.subjectId}</p><p>{t('Qualification pending. No legal signature, source adoption or permit activation.', 'Nitelendirme bekliyor. Hukuki imza, kaynak kabulü veya izin etkinleştirmesi yok.')}</p></>}
        <a href={`/api/v1/${service.tracePath(anchor, 0, row.savedAt)}`} target="_blank" rel="noreferrer">{t('Exact historical trace', 'Kesin tarihsel iz')}</a>
        <button disabled={locked || dirty || row.state === 'RECORDED'} onClick={() => choose(row)}>{t('Review this stream', 'Bu akışı incele')}</button>
        {row.kind === 'Verification' && <button disabled={locked || dirty} onClick={() => choose(row, true)}>{t('Request context-bound decision', 'Bağlama bağlı karar iste')}</button>}
        {row.kind === 'Verification' && <button disabled={locked || dirty} onClick={() => choose(row, true, true)}>{t('Record authorized human decision', 'Yetkili insan kararını kaydet')}</button>}
        {row.kind === 'Decision' && row.state === 'PENDING' && <button disabled={locked || dirty} onClick={() => choose(row, false, true)}>{t('Record authorized human decision', 'Yetkili insan kararını kaydet')}</button>}
      </article>)}
      <button disabled={dirty || busy || offset === 0} onClick={() => setOffset(v => v - 25)}>{t('Previous review events', 'Önceki inceleme olayları')}</button><button disabled={dirty || busy || !data.hasMore} onClick={() => setOffset(v => v + 25)}>{t('More review events', 'Diğer inceleme olayları')}</button>
      {!history && <fieldset disabled={locked || !!attempt}><legend>{t('Human review command', 'İnsan inceleme komutu')}</legend>
        <button disabled={dirty} onClick={() => choose(null)}>{t('New verification request', 'Yeni doğrulama isteği')}</button>
        <label>{t('Provisioned authority', 'Sunucuda atanmış yetki')}<select value={authority} onChange={e => setAuthority(e.target.value)}><option value="">{t('Select exact appointment', 'Kesin atamayı seçin')}</option>{eligible.map(g => <option key={g.id} value={g.id}>{g.roleId} / {g.operationId} / {g.capability}{g.recording ? ` / ${g.recording.decisionType} / ${g.recording.subjectType}` : ''}</option>)}</select></label>
        {eligible.length === 0 && <p>{t('No authorized human appointment is available.', 'Yetkili insan ataması bulunmuyor.')}</p>}
        <label>{t('Bound review reason', 'Bağlı inceleme gerekçesi')}<textarea maxLength={2000} value={rationale} onChange={e => setRationale(e.target.value)} /></label>
        {recordingMode ? <>
          <p>{grant?.recording ? `${grant.recording.decisionType} / ${grant.recording.subjectType} / ${grant.recording.subjectId}` : t('Unresolved decision type. An exact server policy is required.', 'Karar türü çözümlenmedi. Kesin sunucu politikası gerekli.')}</p>
          {grant?.recording?.requiredGate && <p role="alert">{t('This type requires an unavailable gate', 'Bu tür kullanılamayan bir kapı gerektiriyor')}: {grant.recording.requiredGate} · {grant.recording.gateSourceLocator}</p>}
          <label>{t('Human decision text', 'İnsan kararı metni')}<textarea maxLength={2000} value={humanDecision} onChange={e => setHumanDecision(e.target.value)} /></label>
          <label>{t('Decision context', 'Karar bağlamı')}<textarea maxLength={2000} value={decisionContext} onChange={e => setDecisionContext(e.target.value)} /></label>
          <label>{t('Trigger source', 'Tetikleyici kaynak')}<input maxLength={2000} value={triggerSource} onChange={e => setTriggerSource(e.target.value)} /></label>
          <p>{t('Records your decision text and immutable context. Qualification, source admission and operational release remain separate.', 'Karar metninizi ve değişmez bağlamı kaydeder. Nitelendirme, kaynak kabulü ve operasyonel serbest bırakma ayrıdır.')}</p>
          <button disabled={!grant?.recording || grant.recording.requiredGate !== null || !rationale.trim() || !humanDecision.trim() || !decisionContext.trim() || !triggerSource.trim()} onClick={() => void command('RECORDED', 'HUMAN_DECIDED')}>{t('Save human decision record', 'İnsan karar kaydını sakla')}</button>
        </> : (!selected || decision) ? <button disabled={!grant || !rationale.trim()} onClick={() => void command('PENDING', 'REQUEST_REVIEW')}>{t('Record pending request', 'Bekleyen isteği kaydet')}</button> : <>
          {selected.kind === 'Verification' && <><button disabled={!grant || !rationale.trim()} onClick={() => void command('VERIFIED', 'HUMAN_VERIFIED')}>{t('Record human verification', 'İnsan doğrulamasını kaydet')}</button><button disabled={!grant || !rationale.trim()} onClick={() => void command('EXPIRED', 'VALIDITY_ELAPSED')}>{t('Record expiry', 'Süre sonunu kaydet')}</button><button disabled={!grant || !rationale.trim()} onClick={() => void command('SUPERSEDED', 'INPUT_SUPERSEDED')}>{t('Record input supersession', 'Girdi sürüm değişimini kaydet')}</button></>}
          <button disabled={!grant || !rationale.trim()} onClick={() => void command('REJECTED', 'HUMAN_REJECTED')}>{t('Reject with reason', 'Gerekçeyle reddet')}</button>
        </>}
      </fieldset>}
    </>}
    {attempt && <button disabled={offline || busy || conflict || error === 401 || error === 403 || error === 404} onClick={() => void retry()}>{t('Retry exact human command', 'Aynı insan komutunu tekrar dene')}</button>}
    <button disabled={busy || offline} onClick={() => { if (!dirty) { setConflict(false); setError(null); setReload(v => v + 1) } }}>{t('Reload current trace', 'Güncel izi yenile')}</button>
    {dirty && <button disabled={busy} onClick={() => { setAttempt(null); setRationale(''); clearRecording(); setSelected(null); setDecision(false); setAuthority(''); setConflict(false); setError(null); setReload(v => v + 1); setNotice(t('Local attempt discarded; a recorded server event remains immutable.', 'Yerel deneme bırakıldı; sunucuda kaydedilmiş olay değişmez.')) }}>{t('Discard local review draft', 'Yerel inceleme taslağını bırak')}</button>}
  </section>
}
