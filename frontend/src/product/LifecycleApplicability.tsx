import { ComplianceEvaluationSelector } from './ComplianceInputEditor'
import type { OwningReferences } from './compliance-input-contract'
import { RegulatoryQualification } from './RegulatoryQualification'
import { OwningEvaluationSelector } from './OperationEquipmentEditor'
import * as qualification from './qualification-api'
import { useLayoutEffect, useEffect, useRef, useState } from 'react'
import type { ReactNode } from 'react'
import * as service from './applicability-api'
import * as directory from './api'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'
import type { Capabilities, Evaluation, FrozenCommand, Page, Reason, RuleDraft, HistoryItem } from './applicability-api'

type Props = { language: 'tr' | 'en'; csrf: string; offline: boolean; onDirty: (v: boolean) => void; onSaving: (v: boolean) => void }
export function LifecycleApplicability({ language, csrf, offline, onDirty, onSaving }: Props) {
  const [owningInputs, setOwningInputs] = useState<OwningReferences | null>(null)
  const [qualificationDirty, setQualificationDirty] = useState(false), [qualificationSaving, setQualificationSaving] = useState(false)
  const [currentState, setCurrentState] = useState<{ id: string; current: boolean; causes: string[] } | null>(null)
  const t = (en: string, tr: string) => language === 'en' ? en : tr
  const [caps, setCaps] = useState<Capabilities | null>(null), [rows, setRows] = useState<Page<RuleDraft> | null>(null)
  const [vessels, setVessels] = useState<directory.Vessel[]>([]), [vesselPage, setVesselPage] = useState(0), [moreVessels, setMoreVessels] = useState(false)
  const [offset, setOffset] = useState(0), [attempt, setAttempt] = useState(0), [error, setError] = useState<number | null>(null)
  const [selected, setSelected] = useState<RuleDraft | null>(null), [ruleJson, setRuleJson] = useState('')
  const [source, setSource] = useState(''), [sourceRevision, setSourceRevision] = useState(''), [requirement, setRequirement] = useState(''), [requirementRevision, setRequirementRevision] = useState('')
  const [dirty, setDirty] = useState(false), [saving, setSaving] = useState(false), [frozen, setFrozen] = useState<FrozenCommand | null>(null), [conflict, setConflict] = useState(false)
  const [requested, setRequested] = useState('ADOPTED'), [ruleHistory, setRuleHistory] = useState<Page<RuleDraft> | null>(null), [ruleHistoryOffset, setRuleHistoryOffset] = useState(0)
  const [vessel, setVessel] = useState(''), [checked, setChecked] = useState<string[]>([]), [evaluation, setEvaluation] = useState<Evaluation | null>(null)
  const resetScope1 = [vessel, offline] as const
  const [previousScope1, setPreviousScope1] = useState(resetScope1)
  // Withdraw stale state in the same render that changes the request scope.
  if (!Object.is(previousScope1[0], vessel) || !Object.is(previousScope1[1], offline)) {
    setPreviousScope1(resetScope1)
    setOwningInputs(null)
  }
  useEffect(() => {  }, [vessel, offline])
  const [asOf, setAsOf] = useState('')
  const [history, setHistory] = useState<Page<HistoryItem> | null>(null), [historyOffset, setHistoryOffset] = useState(0), [notice, setNotice] = useState('')
  const liveVessel = useRef(vessel); useLayoutEffect(() => { liveVessel.current = vessel }, [vessel])
  useEffect(() => { onDirty(dirty || !!frozen || conflict || qualificationDirty) }, [dirty, frozen, conflict, qualificationDirty, onDirty])
  useEffect(() => { onSaving(saving || qualificationSaving) }, [saving, qualificationSaving, onSaving])
  useEffect(() => {
    if (offline) return
    const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 15000)
    service.capabilities(c.signal).then(async p => {
      if (!active) return; setCaps(p)
      if (!p.canRead) return
      const page = await service.rules(offset, c.signal); if (active) setRows(page)
      const v = await directory.vessels(vesselPage, '', c.signal); if (active) { setVessels(v.items); setMoreVessels(v.hasMore) }
    }).catch(e => { if (active) setError(e instanceof directory.ApiError ? e.status : 0) })
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [attempt, offset, vesselPage, offline])
  const resetScope2 = [vessel, historyOffset, attempt, offline, caps?.canRead] as const
  const [previousScope2, setPreviousScope2] = useState(resetScope2)
  // Withdraw stale state in the same render that changes the request scope.
  if (!Object.is(previousScope2[0], vessel) || !Object.is(previousScope2[1], historyOffset) || !Object.is(previousScope2[2], attempt) || !Object.is(previousScope2[3], offline) || !Object.is(previousScope2[4], caps?.canRead)) {
    setPreviousScope2(resetScope2)
    setHistory(null)
  }
  useEffect(() => {

    if (!vessel || offline || !caps?.canRead) return
    const c = new AbortController(); let active = true
    service.history(vessel, historyOffset, c.signal).then(p => { if (active) setHistory(p) }).catch(e => { if (active) setError(e instanceof directory.ApiError ? e.status : 0) })
    return () => { active = false; c.abort() }
  }, [vessel, historyOffset, attempt, offline, caps?.canRead])
  const resetScope3 = [selected, ruleHistoryOffset, offline] as const
  const [previousScope3, setPreviousScope3] = useState(resetScope3)
  // Withdraw stale state in the same render that changes the request scope.
  if (!Object.is(previousScope3[0], selected) || !Object.is(previousScope3[1], ruleHistoryOffset) || !Object.is(previousScope3[2], offline)) {
    setPreviousScope3(resetScope3)
    setRuleHistory(null)
  }
  useEffect(() => {

    if (!selected || offline) return
    const c = new AbortController(); let active = true
    service.versions(selected.current.id, ruleHistoryOffset, c.signal).then(p => { if (active) setRuleHistory(p) }).catch(e => { if (active) setError(e instanceof directory.ApiError ? e.status : 0) })
    return () => { active = false; c.abort() }
  }, [selected, ruleHistoryOffset, offline])
  const locked = qualificationDirty || qualificationSaving || saving || !!frozen || conflict || error === 401 || error === 403
  function choose(row: RuleDraft | null) {
    if (dirty || frozen) return
    setSelected(row); setRuleJson(row?.current.ruleJson ?? ''); setRuleHistoryOffset(0)
    setSource(row?.current.sourceId ?? ''); setSourceRevision(row?.current.sourceRevision?.toString() ?? '')
    setRequirement(row?.current.requirementId ?? ''); setRequirementRevision(row?.current.requirementRevision?.toString() ?? ''); setConflict(false)
  }
  async function send(command: FrozenCommand) {
    if (offline || saving || conflict) return
    setFrozen(command); setSaving(true); setError(null); setNotice('')
    const c = new AbortController(); const timer = setTimeout(() => c.abort(), 15000)
    try {
      const result = await service.send(command, csrf, c.signal)
      if ('current' in result) { setSelected(result); setRuleJson(result.current.ruleJson ?? ''); setDirty(false) }
      else { setEvaluation(result); setCurrentState(null); setHistoryOffset(0) }
      setFrozen(null); setAttempt(v => v + 1)
      setNotice(command.kind === 'review' ? t('Review request recorded: BLOCKED. Source admission and a qualified independent reviewer are pending.', 'İnceleme talebi kaydedildi: ENGELLİ. Kaynak kabulü ve yetkili bağımsız inceleyici bekleniyor.') : command.kind === 'evaluate' ? t('Preview computed without saving. Current obligations remain unavailable.', 'Önizleme kaydedilmeden hesaplandı. Güncel yükümlülükler kullanılamıyor.') : t('Saved as unverified preparation. Current obligations remain unavailable.', 'Doğrulanmamış hazırlık olarak kaydedildi. Güncel yükümlülükler kullanılamıyor.'))
    } catch (e) {
      const status = e instanceof directory.ApiError ? e.status : 0; setError(status)
      if ([400, 401, 403, 404, 409].includes(status)) setFrozen(null)
      if (status === 409) setConflict(true)
    } finally { clearTimeout(timer); setSaving(false) }
  }
  const freeze = (kind: FrozenCommand['kind'], path: string, body: unknown, method: 'POST' | 'PUT' = 'POST') => send({ kind, path, body: JSON.stringify(body), method, key: crypto.randomUUID() })
  function save() {
    if (!caps?.canWrite || locked || offline) return
    const validLink = (id: string, revision: string) => !id && !revision || isGuid(id) && /^[1-9][0-9]*$/.test(revision) && Number.isSafeInteger(Number(revision))
    if (!validLink(source, sourceRevision) || !validLink(requirement, requirementRevision)) { setError(400); return }
    try { if (ruleJson.trim()) { const rule: unknown = JSON.parse(ruleJson); if (!rule || typeof rule !== 'object' || Array.isArray(rule)) throw new Error() } }
    catch { setError(400); return }
    const metadata = {
      expectedVersion: selected?.version ?? 0, contract: service.ruleContract, sourceId: source || null, sourceRevision: source ? Number(sourceRevision) : null,
      requirementId: requirement || null, requirementRevision: requirement ? Number(requirementRevision) : null, reasonCode: selected ? 'AUTHORING_CORRECTION' : 'INITIAL_AUTHORING',
    }
    // Parse above checks JSON shape only. Preserve the original numeric lexemes;
    // never serialize the JS-parsed rule back through IEEE754.
    void send({ kind: 'author', path: selected ? `rules/${selected.current.id}` : 'rules', method: selected ? 'PUT' : 'POST', key: crypto.randomUUID(),
      body: `${JSON.stringify(metadata).slice(0, -1)},"rule":${ruleJson.trim() ? ruleJson : 'null'}}` })
  }
  const paging = (page: { hasMore: boolean } | null, value: number, change: (n: number) => void) => <div className="form-actions"><button type="button" disabled={saving || value === 0} onClick={() => change(Math.max(0, value - 25))}>{t('Previous', 'Önceki')}</button><button type="button" disabled={saving || !page?.hasMore} onClick={() => change(value + 25)}>{t('Next', 'Sonraki')}</button></div>
  function tree(reason: Reason): ReactNode {
    return <li key={reason.path}>{reason.field ?? reason.operator ?? reason.path} · {reason.truth === 0 ? t('Unknown', 'Bilinmiyor') : reason.truth === 1 ? t('False', 'Yanlış') : t('True', 'Doğru')} · {reason.reason}{reason.reviewRequired && ` · ${t('Review required', 'İnceleme gerekli')}`}{reason.children.length > 0 && <ul>{reason.children.map(tree)}</ul>}</li>
  }
  return <section className="lifecycle-applicability" aria-label={t('Lifecycle and applicability preparation', 'Yaşam döngüsü ve uygulanabilirlik hazırlığı')}>
    <h2>{t('Lifecycle and applicability', 'Yaşam döngüsü ve uygulanabilirlik')}</h2>
    <p>{t('PREVIEW / DRAFT. Predictions do not establish compliance, readiness, permits or legal approval. Current obligations require admitted sources and independent human authority.', 'ÖNİZLEME / TASLAK. Tahminler uyum, hazırlık, izin veya hukuki onay oluşturmaz. Güncel yükümlülükler kabul edilmiş kaynaklar ve bağımsız insan yetkisi gerektirir.')}</p>
    {offline && <p role="status">{t('Offline. Retained edits are available; commands need a connection.', 'Çevrimdışı. Düzenlemeler korunuyor; komutlar bağlantı gerektirir.')}</p>}
    {error !== null && <p role="alert">{error === 409 ? t('Conflict. Retained edits require reload after explicit discard.', 'Çatışma. Korunan düzenlemelerden açıkça vazgeçtikten sonra yeniden yükleyin.') : error === 403 || error === 401 ? t('Permission or session unavailable. Retained edits are locked.', 'Yetki veya oturum kullanılamıyor. Korunan düzenlemeler kilitlendi.') : error === 400 ? t('Check the rule, revision, source links and controlled policy.', 'Kuralı, sürümü, kaynak bağlantılarını ve kontrollü politikayı kontrol edin.') : t('Service unavailable. The command may have committed; retry the retained command.', 'Hizmet kullanılamıyor. Komut kaydedilmiş olabilir; korunan komutu tekrar deneyin.')}</p>}
    {notice && <p role="status">{notice}</p>}
    {!caps ? <p role="status">{t('Loading permissions…', 'Yetkiler yükleniyor…')}</p> : !caps.canRead ? <p role="status">{t('No applicability read permission.', 'Uygulanabilirlik okuma yetkisi yok.')}</p> : <>
      <h3>{t('Versioned rule drafts', 'Sürümlü kural taslakları')}</h3>
      {!rows ? <p role="status">{t('Loading rules…', 'Kurallar yükleniyor…')}</p> : rows.items.length === 0 ? <p>{t('No rule drafts.', 'Kural taslağı yok.')}</p> : <ul>{rows.items.map(row => <li key={row.current.id}><button type="button" disabled={locked || dirty} onClick={() => choose(row)}>{row.current.id} · {row.current.revision} · DRAFT</button></li>)}</ul>}
      {paging(rows, offset, setOffset)}
      {caps.canWrite && <button type="button" disabled={locked || dirty} onClick={() => choose(null)}>{t('New incomplete draft', 'Yeni eksik taslak')}</button>}
      <form onSubmit={e => { e.preventDefault(); save() }}><fieldset disabled={locked || !caps.canWrite}>
        <legend>{t('Immutable authoring revision', 'Değişmez yazım sürümü')} {selected?.current.revision ?? 0}</legend>
        <p>{t('An empty document can be saved as an incomplete draft. Evaluation requires a valid versioned rule. Declared lifecycle is a proposal; stored lifecycle stays DRAFT.', 'Boş belge eksik taslak olarak kaydedilebilir. Değerlendirme geçerli sürümlü kural gerektirir. Beyan edilen yaşam döngüsü öneridir; kayıtlı durum DRAFT kalır.')}</p>
        <label>{t('Rule document', 'Kural belgesi')}<textarea rows={12} maxLength={32768} value={ruleJson} onChange={e => { setRuleJson(e.target.value); setDirty(true) }} /></label>
        <label>{t('Source record ID', 'Kaynak kayıt kimliği')}<input value={source} onChange={e => { setSource(e.target.value); setDirty(true) }} maxLength={36} /></label>
        <label>{t('Exact source revision', 'Kesin kaynak sürümü')}<input inputMode="numeric" value={sourceRevision} onChange={e => { setSourceRevision(e.target.value); setDirty(true) }} /></label>
        <label>{t('Requirement record ID', 'Gereksinim kayıt kimliği')}<input value={requirement} onChange={e => { setRequirement(e.target.value); setDirty(true) }} maxLength={36} /></label>
        <label>{t('Exact requirement revision', 'Kesin gereksinim sürümü')}<input inputMode="numeric" value={requirementRevision} onChange={e => { setRequirementRevision(e.target.value); setDirty(true) }} /></label>
        <button type="submit" disabled={offline}>{t('Save draft revision', 'Taslak sürümünü kaydet')}</button>
      </fieldset></form>
      {selected && <>
        <p>{t('Stored lifecycle', 'Kayıtlı yaşam döngüsü')}: DRAFT · {selected.current.reviewStatus}</p>
        <fieldset disabled={locked || dirty || offline || !caps.canReview}><legend>{t('Request human review', 'İnsan incelemesi talep et')}</legend>
          <label>{t('Requested lifecycle', 'Talep edilen yaşam döngüsü')}<select value={requested} onChange={e => setRequested(e.target.value)}>{service.lifecycleStates.map(s => <option key={s}>{s}</option>)}</select></label>
          <button type="button" onClick={() => void freeze('review', `rules/${selected.current.id}/review`, { expectedVersion: selected.version, requestedLifecycle: requested, reasonCode: 'REQUEST_REVIEW' })}>{t('Record review request', 'İnceleme talebini kaydet')}</button>
        </fieldset>
        <h3>{t('Immutable rule history', 'Değişmez kural geçmişi')}</h3>
        {ruleHistory?.items.map(row => <details key={row.current.revision}><summary>{row.current.revision} · {row.current.lifecycle} · {row.current.requestedLifecycle} · {row.current.reviewStatus}</summary><p>{row.current.savedAt}</p><pre className="register-hash">{row.current.hash}</pre><pre>{row.current.ruleJson ?? '—'}</pre></details>)}
        {paging(ruleHistory, ruleHistoryOffset, setRuleHistoryOffset)}
      </>}
      <fieldset disabled={locked || dirty || offline || !caps.canEvaluate}><legend>{t('Evaluate preview', 'Önizlemeyi değerlendir')}</legend>
        <label>{t('Vessel context', 'Araç bağlamı')}<select value={vessel} onChange={e => { setVessel(e.target.value); setEvaluation(null); setHistoryOffset(0) }}><option value="">{t('Select', 'Seçin')}</option>{vessel && !vessels.some(v => v.id === vessel) && <option value={vessel}>{vessel}</option>}{vessels.map(v => <option key={v.id} value={v.id}>{v.name} · {v.kind}</option>)}</select></label>
        {paging({ hasMore: moreVessels }, vesselPage, setVesselPage)}
        {rows?.items.map(row => <label key={row.current.id}><input type="checkbox" disabled={!row.current.rule} checked={checked.includes(row.current.id)} onChange={e => setChecked(v => e.target.checked ? [...v, row.current.id] : v.filter(id => id !== row.current.id))} />{row.current.id}</label>)}
        <label>{t('As-of time (with UTC offset; blank uses server time)', 'Değerlendirme zamanı (UTC farkı ile; boşsa sunucu zamanı)')}<input value={asOf} maxLength={40} onChange={e => setAsOf(e.target.value)} /></label>
        <button type="button" disabled={!vessel || checked.length === 0} onClick={() => {
          if (asOf && !timestamp(asOf)) { setError(400); return }
          void freeze('evaluate', 'applicability/simulate', { vesselId: vessel, ruleIds: checked, asOf: asOf || null })
        }}>{t('Run preview without saving', 'Kaydetmeden önizle')}</button>
      </fieldset>
      {selected && <RegulatoryQualification key={selected.current.id} rule={selected.current.id} ruleRevision={selected.current.revision} vessel={vessel} language={language} csrf={csrf} offline={offline} onDirty={setQualificationDirty} onSaving={setQualificationSaving} />}
      <ComplianceEvaluationSelector value={owningInputs} onChange={setOwningInputs} offline={offline || locked || dirty} language={language} />
      <OwningEvaluationSelector key={vessel} vessel={vessel} language={language} offline={offline} disabled={locked} onChange={v => setOwningInputs(previous => v ? { ...previous, ...v } : previous ? { ...previous, operationId: null, cargoPlanId: null, winchFitmentId: null } : null)} />
      <button disabled={locked || dirty || offline || !vessel || checked.length === 0 || !caps?.canEvaluate} onClick={() => void freeze('evaluate', 'applicability/evaluate', { vesselId: vessel, ruleIds: checked, asOf: null, owningInputs })}>{t('Evaluate qualified current sources', 'Nitelendirilmiş güncel kaynakları değerlendir')}</button>
      {evaluation?.authoritative && <><button disabled={offline || saving} onClick={() => { const c = new AbortController(); qualification.current(evaluation.id, c.signal).then(v => setCurrentState({ ...v, id: evaluation.id })).catch(() => setCurrentState({ id: evaluation.id, current: false, causes: ['EVALUATION_UNAVAILABLE'] })) }}>{t('Check current validity', 'Güncel geçerliliği denetle')}</button><p role="status">{currentState?.id === evaluation.id ? currentState.current ? t('Inputs remain current. This is not safety release.', 'Girdiler güncel. Bu bir emniyet serbest bırakması değildir.') : `${t('Review required', 'İnceleme gerekli')}: ${currentState.causes.join(' · ')}` : t('Current validity has not been checked.', 'Güncel geçerlilik denetlenmedi.')}</p></>}
      {evaluation && <section aria-label={t('Preview result', 'Önizleme sonucu')}><h3>{evaluation.authoritative ? t('Recorded evaluation — see current validity separately', 'Kayıtlı değerlendirme — güncel geçerliliği ayrıca inceleyin') : t('PREVIEW — review required for current obligations', 'ÖNİZLEME — güncel yükümlülükler için inceleme gerekli')}</h3><p>{evaluation.asOf} · {evaluation.snapshotHash}</p>{evaluation.conflicts.map(c => <p role="alert" key={`${c.code}:${c.target}`}>{t('Conflict requires source owner and reviewer. Both candidates retained.', 'Çatışma kaynak sahibi ve inceleyici gerektirir. İki aday da korundu.')} {c.target} · {c.ruleReferences.join(' · ')}</p>)}{evaluation.results.map(r => <details key={r.ruleId} open><summary>{r.ruleId} · {r.revision} · {r.prediction.result} · {evaluation.authoritative ? t('Qualified input evaluation', 'Nitelendirilmiş girdi değerlendirmesi') : t('Prediction only', 'Yalnız tahmin')}</summary><p>{r.sourceId ?? t('Missing source', 'Kaynak eksik')} / {r.sourceRevision ?? '—'}</p><ul>{tree(r.prediction.reason)}</ul><p>{r.prediction.blockers.join(' · ')}</p><ul>{r.prediction.predictedActivations.map(a => <li key={`${a.type}:${a.id}`}>{a.type}: {a.id} · {t('Projected need only', 'Yalnız öngörülen ihtiyaç')}</li>)}</ul></details>)}</section>}
      {vessel && <section aria-label={t('Evaluation history', 'Değerlendirme geçmişi')}><h3>{t('Evaluation history', 'Değerlendirme geçmişi')}</h3>{!history ? <p role="status">{t('Loading history…', 'Geçmiş yükleniyor…')}</p> : history.items.length === 0 ? <p>{t('No evaluations.', 'Değerlendirme yok.')}</p> : history.items.map(row => <button type="button" disabled={saving || offline} key={row.id} onClick={() => {
        const c = new AbortController(); const timer = setTimeout(() => c.abort(), 15000)
        service.evaluation(row.id, c.signal).then(v => { if (v.vesselId === liveVessel.current) setEvaluation(v) }).catch(e => { if (row.vesselId === liveVessel.current) setError(e instanceof directory.ApiError ? e.status : 0) }).finally(() => clearTimeout(timer))
      }}>{row.asOf} · {t('Read retained snapshot', 'Korunan anlık kaydı oku')}</button>)}{paging(history, historyOffset, setHistoryOffset)}</section>}
    </>}
    {frozen && <button type="button" disabled={offline || saving || conflict} onClick={() => void send(frozen)}>{t('Retry exact retained command', 'Korunan aynı komutu tekrar dene')}</button>}
    {(dirty || frozen || conflict) && <><p role="status">{t('Edits retained in this tab. Discarding an ambiguous command does not undo a possible commit; reload history before further editing.', 'Düzenlemeler bu sekmede korunuyor. Belirsiz komuttan vazgeçmek olası kaydı geri almaz; düzenlemeye devam etmeden geçmişi yeniden yükleyin.')}</p><button type="button" disabled={saving} onClick={() => { setDirty(false); setFrozen(null); setConflict(false); setSelected(null); setRuleJson(''); setSource(''); setSourceRevision(''); setRequirement(''); setRequirementRevision(''); setAttempt(v => v + 1); setError(null) }}>{t('Discard edits and reload', 'Düzenlemelerden vazgeç ve yeniden yükle')}</button></>}
    {!frozen && <button type="button" disabled={offline || saving} onClick={() => { setError(null); setAttempt(v => v + 1) }}>{t('Refresh permissions and lists', 'Yetkileri ve listeleri yenile')}</button>}
  </section>
}
