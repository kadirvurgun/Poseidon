import * as qualification from './qualification-api'
import { useEffect, useState } from 'react'
import * as service from './control-evidence-api'
import * as directory from './api'
import * as registers from './register-api'
import * as rules from './applicability-api'
import { filePage } from './file-api'
import type { FileRecord } from './file-api'
import { HumanDecisionTrace } from './HumanDecisionTrace'
import { VesselFiles } from './VesselFiles'
import { DraftConfirmation } from './DraftConfirmation'
import { timestamp } from './date-validation'

type Props = { language: 'tr' | 'en'; csrf: string; offline: boolean; onDirty: (v: boolean) => void; onSaving: (v: boolean) => void }
export function ControlEvidence(props: Props) {
  const t = (en: string, tr: string) => props.language === 'en' ? en : tr
  const [vessels, setVessels] = useState<directory.Vessel[]>([]), [vesselOffset, setVesselOffset] = useState(0), [more, setMore] = useState(false)
  const [vessel, setVessel] = useState(''), [kind, setKind] = useState<service.Kind>('Control'), [dirty, setDirty] = useState(false), [saving, setSaving] = useState(false)
  const [pending, setPending] = useState<{ vessel: string; kind: service.Kind } | null>(null), [error, setError] = useState(false)
  useEffect(() => {
    if (props.offline) return
    const c = new AbortController(); let active = true
    directory.vessels(vesselOffset, '', c.signal).then(p => { if (active) { setVessels(p.items); setMore(p.hasMore); setError(false) } }).catch(() => { if (active) setError(true) })
    return () => { active = false; c.abort() }
  }, [vesselOffset, props.offline])
  const change = (v: string, k: service.Kind) => { if (dirty) setPending({ vessel: v, kind: k }); else { setVessel(v); setKind(k) } }
  return <section className="control-evidence" aria-label={t('Controls and evidence', 'Kontroller ve kanıtlar')}>
    <h2>{t('Controls and evidence', 'Kontroller ve kanıtlar')}</h2>
    <p>{t('Draft declarations. File presence, completion, verification and effectiveness are separate.', 'Taslak beyanlar. Dosyanın varlığı, tamamlanma, doğrulama ve etkinlik ayrıdır.')}</p>
    {error && <p role="alert">{t('Vessel selection unavailable.', 'Araç seçimi kullanılamıyor.')}</p>}
    <label>{t('Vessel context', 'Araç bağlamı')}<select value={vessel} disabled={saving} onChange={e => change(e.target.value, kind)}><option value="">{t('Select vessel', 'Araç seçiniz')}</option>{vessel && !vessels.some(v => v.id === vessel) && <option value={vessel}>{vessel}</option>}{vessels.map(v => <option key={v.id} value={v.id}>{v.name}</option>)}</select></label>
    <button disabled={saving || vesselOffset === 0} onClick={() => setVesselOffset(v => v - 25)}>{t('Previous vessels', 'Önceki araçlar')}</button><button disabled={saving || !more} onClick={() => setVesselOffset(v => v + 25)}>{t('More vessels', 'Diğer araçlar')}</button>
    <label>{t('Register', 'Kayıt türü')}<select disabled={saving} value={kind} onChange={e => change(vessel, e.target.value as service.Kind)}><option value="Control">{t('Operational controls', 'Operasyonel kontroller')}</option><option value="Evidence">{t('Evidence', 'Kanıtlar')}</option></select></label>
    {pending && <DraftConfirmation language={props.language} onStay={() => setPending(null)} onDiscard={() => { setVessel(pending.vessel); setKind(pending.kind); setPending(null); setDirty(false); props.onDirty(false) }} />}
    {vessel && <Editor key={`${vessel}/${kind}`} {...props} vessel={vessel} kind={kind} onDirty={v => { setDirty(v); props.onDirty(v) }} onSaving={v => { setSaving(v); props.onSaving(v) }} />}
  </section>
}
function Editor({ vessel, kind, language, csrf, offline, onDirty, onSaving }: Props & { vessel: string; kind: service.Kind }) {
  const t = (en: string, tr: string) => language === 'en' ? en : tr
  const [rows, setRows] = useState<service.Page | null>(null), [offset, setOffset] = useState(0), [reload, setReload] = useState(0), [error, setError] = useState<number | null>(null)
  const [selected, setSelected] = useState<service.Row | null>(null), [content, setContent] = useState(service.blank), [dirty, setDirty] = useState(false)
  const [requirements, setRequirements] = useState<service.RequirementPin[]>([]), [links, setLinks] = useState<service.GraphPin[]>([]), [file, setFile] = useState<service.FilePin | null>(null)
  const [owners, setOwners] = useState<{ actorId: string; roleId: string }[]>([]), [reqs, setReqs] = useState<registers.RegisterPage<registers.Draft> | null>(null), [reqOffset, setReqOffset] = useState(0)
  const [controls, setControls] = useState<service.Page | null>(null), [controlOffset, setControlOffset] = useState(0), [ruleRows, setRuleRows] = useState<rules.Page<rules.RuleDraft> | null>(null), [ruleOffset, setRuleOffset] = useState(0)
  const [files, setFiles] = useState<FileRecord[]>([]), [fileOffset, setFileOffset] = useState(0), [fileMore, setFileMore] = useState(false)
  const [history, setHistory] = useState<service.Page | null>(null), [historyOffset, setHistoryOffset] = useState(0), [asOf, setAsOf] = useState(''), [historyAsOf, setHistoryAsOf] = useState('')
  const [orphan, setOrphan] = useState<Awaited<ReturnType<typeof service.orphans>> | null>(null), [orphanOffset, setOrphanOffset] = useState(0)
  const [busy, setBusy] = useState(false), [frozen, setFrozen] = useState<{ body: string; key: string; action?: 'submit' | 'complete' } | null>(null), [conflict, setConflict] = useState(false), [notice, setNotice] = useState('')
  const [reviewDrafts, setReviewDrafts] = useState<Record<string, boolean>>({}), [reviewSaves, setReviewSaves] = useState<Record<string, boolean>>({})
  const reviewDirty = Object.values(reviewDrafts).some(Boolean), reviewBusy = Object.values(reviewSaves).some(Boolean)
  const [uploadDirty, setUploadDirty] = useState(false), [uploadBusy, setUploadBusy] = useState(false), [discard, setDiscard] = useState(false)
  const [source, setSource] = useState<registers.RegisterPage<registers.Revision> | null>(null), [pickerError, setPickerError] = useState(false)
  const [sourceId, setSourceId] = useState(''), [sourceOffset, setSourceOffset] = useState(0)
  useEffect(() => { onDirty(dirty || !!frozen || uploadDirty || reviewDirty) }, [dirty, frozen, uploadDirty, reviewDirty, onDirty])
  useEffect(() => { onSaving(busy || uploadBusy || reviewBusy) }, [busy, uploadBusy, reviewBusy, onSaving])
  useEffect(() => {
    if (offline) return
    let active = true; const c = new AbortController(); const timer = setTimeout(() => c.abort(), 15000)
    service.page(vessel, kind, offset, c.signal).then(p => { if (active) { setRows(p); setError(null) } }).catch(e => { if (active) { setRows(null); setError(e instanceof directory.ApiError ? e.status : 0) } })
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [vessel, kind, offset, reload, offline])
  useEffect(() => {
    if (offline || !rows) return
    let active = true; const c = new AbortController()
    Promise.all([service.owners(vessel, c.signal), registers.listDrafts('requirements', '', reqOffset, c.signal), service.page(vessel, 'Control', controlOffset, c.signal), rules.rules(ruleOffset, c.signal), filePage(vessel, fileOffset, c.signal), service.orphans(vessel, orphanOffset, c.signal)]).then(([o, r, controls, ruleRows, files, orphan]) => {
      if (!active) return; setOwners(o); setReqs(r); setControls(controls); setRuleRows(ruleRows); setFiles(files.items); setFileMore(files.hasMore); setOrphan(orphan); setPickerError(false)
    }).catch(() => { if (active) { setPickerError(true); setOwners([]); setReqs(null); setControls(null); setRuleRows(null); setFiles([]); setOrphan(null) } })
    return () => { active = false; c.abort() }
  }, [vessel, rows, reqOffset, controlOffset, ruleOffset, fileOffset, orphanOffset, reload, offline])
  useEffect(() => {
    setHistory(null); if (!selected || offline) return
    let active = true; const c = new AbortController()
    service.page(vessel, kind, historyOffset, c.signal, selected.id, historyAsOf || undefined).then(p => { if (active) setHistory(p) }).catch(e => { if (active) setError(e instanceof directory.ApiError ? e.status : 0) })
    return () => { active = false; c.abort() }
  }, [vessel, kind, selected, historyOffset, historyAsOf, offline])
  useEffect(() => {
    setSource(null); if (!sourceId || offline) return
    const c = new AbortController(); let active = true
    registers.historyDraft('sources', sourceId, sourceOffset, c.signal).then(p => { if (active) setSource(p) }).catch(() => { if (active) setPickerError(true) })
    return () => { active = false; c.abort() }
  }, [sourceId, sourceOffset, offline])
  const locked = busy || uploadBusy || reviewDirty || reviewBusy || !!frozen || conflict || error === 401 || error === 403 || error === 404 || !rows?.canWrite
  const mutate = (key: keyof service.Content, value: string | boolean | null) => { setContent(c => ({ ...c, [key]: value })); setDirty(true) }
  function choose(row: service.Row | null) {
    if (dirty || frozen || uploadDirty || busy || uploadBusy || reviewDirty || reviewBusy) return
    setSelected(row); setContent(row?.content ?? service.blank()); setFile(row?.file ?? null); setRequirements(row?.requirements ?? []); setLinks(row?.links ?? []); setHistoryOffset(0); setSource(null); setSourceId(''); setConflict(false)
  }
  async function save(action?: 'submit' | 'complete') {
    if (offline || busy || conflict || !rows?.canWrite) return
    if (!service.validContent(content)) { setError(400); return }
    const command: service.Command = { expectedVersion: selected?.version ?? 0, content, requirements, links, file, reasonCode: action === 'submit' ? 'SUBMIT' : action === 'complete' ? 'COMPLETE' : selected ? 'AUTHORING_CORRECTION' : 'INITIAL_AUTHORING' }
    const attempt = frozen ?? { body: JSON.stringify(command), key: crypto.randomUUID(), action }; setFrozen(attempt); setBusy(true); setError(null)
    const c = new AbortController(); const timer = setTimeout(() => c.abort(), 15000)
    try {
      const saved = await service.save(vessel, kind, selected?.id ?? null, attempt, csrf, c.signal)
      setSelected(saved); setContent(saved.content); setFile(saved.file); setRequirements(saved.requirements); setLinks(saved.links); setFrozen(null); setDirty(false); setNotice(t('Draft saved; verification and effectiveness remain unmet.', 'Taslak kaydedildi; doğrulama ve etkinlik koşulları karşılanmadı.')); setReload(x => x + 1)
    } catch (e) {
      const status = e instanceof directory.ApiError ? e.status : 0; setError(status)
      if (status === 409) setConflict(true)
      setNotice(t('Outcome unresolved. Keep the same attempt for retry; cancellation cannot undo a committed command.', 'Sonuç çözümlenmedi. Tekrar için aynı denemeyi koruyun; iptal sunucuda kaydedilmiş komutu geri alamaz.'))
    } finally { setBusy(false); clearTimeout(timer) }
  }
  const pages = (value: number, hasMore: boolean, update: (v: number) => void, label: string) => <span aria-label={label}><button type="button" disabled={value === 0 || busy} onClick={() => update(value - 25)}>{t('Previous', 'Önceki')} · {label}</button><button type="button" disabled={!hasMore || busy} onClick={() => update(value + 25)}>{t('More', 'Diğer')} · {label}</button></span>
  const blockerLabels: Record<string, string> = {
    SOURCE_REVIEW_REQUIRED: t('Source review required', 'Kaynak incelemesi gerekli'), QUALIFIED_CONTROL_POLICY_MISMATCH: t('Control differs from qualified policy', 'Kontrol nitelendirilmiş politikadan farklı'), QUALIFIED_FRESHNESS_REQUIRED: t('Evidence freshness policy not satisfied', 'Kanıt güncellik politikası karşılanmadı'), SOURCE_ACKNOWLEDGMENT_REQUIRED: t('Exact source acknowledgment required', 'Tam kaynak teyidi gerekli'), AUTHORITATIVE_EVALUATION_UNAVAILABLE: t('Current evaluation unavailable', 'Güncel değerlendirme kullanılamıyor'), APPLICABILITY_REVIEW_REQUIRED: t('Applicability review required', 'Uygulanabilirlik incelemesi gerekli'), PROFILE_CHANGED: t('Vessel profile changed', 'Araç profili değişti'), SOURCE_NOT_CURRENT: t('Source is not current', 'Kaynak güncel değil'),
    TITLE_MISSING: t('Title missing', 'Başlık eksik'), OWNER_MISSING: t('Owner missing', 'Sahip eksik'), REQUIREMENT_LINK_MISSING: t('Requirement link missing', 'Gereksinim bağlantısı eksik'), APPLICABILITY_LINK_MISSING: t('Applicability revision missing', 'Uygulanabilirlik sürümü eksik'),
    FREQUENCY_UNSPECIFIED: t('Frequency unspecified', 'Sıklık belirtilmedi'), EVIDENCE_POLICY_MISSING: t('Evidence policy missing', 'Kanıt politikası eksik'), VERIFICATION_POLICY_MISSING: t('Verification policy missing', 'Doğrulama politikası eksik'), CRITICALITY_UNKNOWN: t('Criticality unknown', 'Kritiklik bilinmiyor'),
    CLEAN_FILE_MISSING: t('Clean file missing', 'Temiz dosya eksik'), PROVENANCE_MISSING: t('Provenance missing', 'Köken eksik'), OBSERVATION_TIME_MISSING: t('Observation time missing', 'Gözlem zamanı eksik'), SOURCE_ADMISSION_UNAVAILABLE: t('Source admission unavailable', 'Kaynak kabulü kullanılamıyor'), VERIFICATION_NOT_PERFORMED: t('Verification not performed', 'Doğrulama yapılmadı'),
  }
  const blockerLabel = (code: string) => blockerLabels[code] ?? code
  const timeLabel = (state: service.Row['timeState']) => ({ UNKNOWN: t('Freshness unknown', 'Güncellik bilinmiyor'), SUPERSEDED: t('Superseded; history retained', 'Yeni sürüm var; geçmiş korunuyor'), DECLARED_EXPIRED: t('Expired by declared date', 'Beyan edilen tarihe göre süresi dolmuş'), DECLARED_STALE: t('Stale by declared date', 'Beyan edilen tarihe göre güncel değil'), DECLARED_NOT_YET_VALID: t('Not yet valid by declared date', 'Beyan edilen tarihe göre henüz geçerli değil') })[state]
  return <div>
    {offline && <p role="status">{t('Offline. Draft retained in memory; server commands require connectivity.', 'Çevrimdışı. Taslak bellekte korunuyor; sunucu komutları bağlantı gerektirir.')}</p>}
    {error !== null && <p role="alert">{error === 409 ? t('Version conflict. Draft retained; inspect history before discarding.', 'Sürüm çatışması. Taslak korunuyor; bırakmadan önce geçmişi inceleyin.') : [401, 403, 404].includes(error) ? t('Current context or permission unavailable.', 'Geçerli bağlam veya yetki kullanılamıyor.') : t('Service unavailable or request rejected. Draft retained.', 'Servis kullanılamıyor veya istek reddedildi. Taslak korunuyor.')}</p>}
    {notice && <p role="status">{notice}</p>}
    <button disabled={busy || offline} onClick={() => setReload(x => x + 1)}>{t('Refresh register', 'Kaydı yenile')}</button>
    {!rows && error === null && <p role="status">{t('Loading register…', 'Kayıt yükleniyor…')}</p>}
    {rows && <><p>{rows.canWrite ? t('Draft authoring available', 'Taslak yazımı kullanılabilir') : t('Read only', 'Salt okunur')}</p>{rows.items.length === 0 && <p>{t('No scoped records.', 'Bu kapsamda kayıt yok.')}</p>}<ul>{rows.items.map(r => <li key={r.id}><button disabled={dirty || !!frozen || uploadDirty || busy || reviewDirty || reviewBusy} onClick={() => choose(r)}>{r.content.title ?? r.id} · {r.revision}</button> · {timeLabel(r.timeState)}</li>)}</ul>{pages(offset, rows.hasMore, setOffset, t('Register pages', 'Kayıt sayfaları'))}</>}
    <button disabled={dirty || !!frozen || uploadDirty || busy || reviewDirty || reviewBusy} onClick={() => choose(null)}>{t('New draft', 'Yeni taslak')}</button>
    {kind === 'Evidence' && selected && <button disabled={locked || dirty || uploadDirty} onClick={() => { const old = selected; setSelected(null); setContent(service.blank()); setFile(null); setRequirements(old.requirements); setLinks([{ kind: 'Evidence', id: old.id, revision: old.revision, expectedVersion: old.version }]); setDirty(true) }}>{t('New evidence superseding this revision', 'Bu sürümün yerine geçecek yeni kanıt')}</button>}
    <form onSubmit={e => { e.preventDefault(); void save() }}>
      <fieldset disabled={locked}><legend>{t('Draft authoring', 'Taslak yazımı')}</legend>
        <label>{t('Title', 'Başlık')}<input value={content.title ?? ''} maxLength={2000} onChange={e => mutate('title', e.target.value || null)} /></label>
        <label>{t('Actual owner and role', 'Gerçek sahip ve rol')}<select value={content.ownerId ? `${content.ownerId}/${content.ownerRole}` : ''} onChange={e => { const [ownerId, ownerRole] = e.target.value.split('/'); setContent(c => ({ ...c, ownerId: ownerId || null, ownerRole: ownerRole || null })); setDirty(true) }}><option value="">{t('Unassigned', 'Atanmamış')}</option>{content.ownerId && !owners.some(o => o.actorId === content.ownerId && o.roleId === content.ownerRole) && <option value={`${content.ownerId}/${content.ownerRole}`}>{t('Previous owner unavailable', 'Önceki sahip kullanılamıyor')}</option>}{owners.map(o => <option key={`${o.actorId}/${o.roleId}`} value={`${o.actorId}/${o.roleId}`}>{o.actorId} / {o.roleId}</option>)}</select></label>
        {kind === 'Control' ? <>
          <label>{t('Criticality', 'Kritiklik')}<select value={content.critical === null ? 'UNKNOWN' : String(content.critical)} onChange={e => mutate('critical', e.target.value === 'UNKNOWN' ? null : e.target.value === 'true')}><option value="UNKNOWN">{t('Unknown', 'Bilinmiyor')}</option><option value="true">{t('Critical declaration', 'Kritik beyanı')}</option><option value="false">{t('Noncritical declaration', 'Kritik değil beyanı')}</option></select></label>
          {(['frequency', 'evidencePolicy', 'verificationPolicy'] as const).map((k, i) => <label key={k}>{[t('Frequency declaration', 'Sıklık beyanı'), t('Required evidence policy declaration', 'Gerekli kanıt politikası beyanı'), t('Verification policy declaration', 'Doğrulama politikası beyanı')][i]}<textarea value={content[k] ?? ''} maxLength={2000} onChange={e => mutate(k, e.target.value || null)} /></label>)}
        </> : <>
          <label>{t('Provenance declaration', 'Köken beyanı')}<textarea value={content.provenance ?? ''} maxLength={2000} onChange={e => mutate('provenance', e.target.value || null)} /></label>
          {(['observedAt', 'validFrom', 'validUntil', 'freshUntil'] as const).map((k, i) => <label key={k}>{[t('Observed time with UTC offset', 'Gözlem zamanı, UTC farkı ile'), t('Declared valid from', 'Beyan edilen geçerlilik başlangıcı'), t('Declared expiry', 'Beyan edilen geçerlilik sonu'), t('Declared freshness limit', 'Beyan edilen güncellik sonu')][i]}<input value={content[k] ?? ''} onChange={e => mutate(k, e.target.value || null)} placeholder="2026-11-02T09:00:00+03:00" /></label>)}
          <label>{t('Clean vessel file version', 'Temiz araç dosyası sürümü')}<select disabled={!!selected?.file} value={file?.id ?? ''} onChange={e => { const f = files.find(f => f.id === e.target.value); setFile(f && f.revision && f.checksum ? { id: f.id, revision: f.revision, checksum: f.checksum } : null); setDirty(true) }}><option value="">{t('No file: incomplete draft', 'Dosya yok: eksik taslak')}</option>{file && !files.some(f => f.id === file.id) && <option value={file.id}>{file.id} / {file.revision}</option>}{files.filter(f => f.state === 'CleanTransport').map(f => <option key={f.id} value={f.id}>{f.name} / {f.revision}</option>)}</select></label>
          {pages(fileOffset, fileMore, setFileOffset, t('File pages', 'Dosya sayfaları'))}
          <p>{t('A different file requires a new evidence record; previous bytes and references are retained.', 'Farklı dosya yeni bir kanıt kaydı gerektirir; önceki baytlar ve referanslar korunur.')}</p>
          <fieldset><legend>{t('Exact control revision links', 'Kesin kontrol sürümü bağlantıları')}</legend>{controls?.items.map(r => <label key={r.id}><input type="checkbox" checked={links.some(p => p.kind === 'Control' && p.id === r.id && p.revision === r.revision)} onChange={e => { setLinks(p => e.target.checked ? [...p, { kind: 'Control', id: r.id, revision: r.revision, expectedVersion: r.version }] : p.filter(p => !(p.kind === 'Control' && p.id === r.id && p.revision === r.revision))); setDirty(true) }} />{r.content.title ?? r.id} / {r.revision}</label>)}{controls && pages(controlOffset, controls.hasMore, setControlOffset, t('Control pages', 'Kontrol sayfaları'))}</fieldset>
        </>}
        <fieldset><legend>{t('Exact requirement and source revisions', 'Kesin gereksinim ve kaynak sürümleri')}</legend>{reqs?.items.map(d => { const r = d.current; if (!r.sourceId || !r.sourceRevision) return null; return <label key={r.id}><input type="checkbox" checked={requirements.some(p => p.requirementId === r.id && p.requirementRevision === r.revision)} onChange={e => { setRequirements(p => e.target.checked ? [...p, { requirementId: r.id!, requirementRevision: r.revision, sourceId: r.sourceId!, sourceRevision: r.sourceRevision!, ruleId: null, ruleRevision: null }] : p.filter(p => !(p.requirementId === r.id && p.requirementRevision === r.revision))); setDirty(true) }} />{r.content.title.value ?? r.id} / {r.revision}</label> })}{reqs && pages(reqOffset, reqs.hasMore, setReqOffset, t('Requirement pages', 'Gereksinim sayfaları'))}</fieldset>
        {requirements.map(p => <label key={`${p.requirementId}/${p.requirementRevision}`}>{t('Applicability rule revision', 'Uygulanabilirlik kuralı sürümü')} · {p.requirementId}<select value={p.ruleId ? `${p.ruleId}/${p.ruleRevision}` : ''} onChange={e => { const r = ruleRows?.items.find(r => `${r.current.id}/${r.current.revision}` === e.target.value); setRequirements(ps => ps.map(x => x === p ? { ...x, ruleId: r?.current.id ?? null, ruleRevision: r?.current.revision ?? null } : x)); setDirty(true) }}><option value="">{t('Unavailable / not selected', 'Kullanılamıyor / seçilmedi')}</option>{p.ruleId && !ruleRows?.items.some(r => r.current.id === p.ruleId && r.current.revision === p.ruleRevision) && <option value={`${p.ruleId}/${p.ruleRevision}`}>{p.ruleId}/{p.ruleRevision}</option>}{ruleRows?.items.filter(r => r.current.requirementId === p.requirementId && r.current.requirementRevision === p.requirementRevision && r.current.sourceId === p.sourceId && r.current.sourceRevision === p.sourceRevision).map(r => <option key={r.current.id} value={`${r.current.id}/${r.current.revision}`}>{r.current.id} / {r.current.revision}</option>)}</select></label>)}
        {ruleRows && pages(ruleOffset, ruleRows.hasMore, setRuleOffset, t('Rule pages', 'Kural sayfaları'))}
      </fieldset>
      {pickerError && <p role="alert">{t('Scoped references unavailable. No empty result or authorization is inferred.', 'Kapsamlı referanslar kullanılamıyor. Boş sonuç veya yetki varsayılmadı.')}</p>}
      <button disabled={offline || busy || conflict || !rows?.canWrite || [401, 403, 404].includes(error ?? -1)}>{frozen ? t('Retry same command', 'Aynı komutu tekrar dene') : t('Save incomplete draft', 'Eksik taslağı kaydet')}</button>
      <button type="button" disabled={locked || offline || !selected} onClick={() => void save('submit')}>{t('Submit under qualified policy', 'Nitelendirilmiş politikayla gönder')}</button>
      <button type="button" disabled={locked || offline || selected?.lifecycle !== 'SUBMITTED' || dirty} onClick={() => void save('complete')}>{t('Record completion', 'Tamamlanmayı kaydet')}</button>
      {kind === 'Evidence' && requirements.filter(p => p.ruleId !== null).map(p => <button key={p.ruleId} type="button" disabled={locked || offline} onClick={() => { const c = new AbortController(); qualification.load(p.ruleId!, vessel, c.signal).then(q => { const row = q.items[0]; if (row?.state === 'IN_FORCE' && row.sourceHash) { setContent(previous => ({ ...previous, acknowledgments: [...(previous.acknowledgments ?? []).filter(a => a.ruleId !== p.ruleId), { ruleId: p.ruleId!, qualificationHash: row.hash, sourceHash: row.sourceHash }] })); setDirty(true); setNotice(`${t('Acknowledgment bound to source hash', 'Teyit kaynak özetine bağlandı')}: ${row.sourceHash}`) } else setError(409) }).catch(() => setError(403)) }}>{t('Acknowledge exact source revision', 'Tam kaynak sürümünü teyit et')} · {p.ruleId}</button>)}
      {(dirty || frozen || conflict) && <button type="button" disabled={busy || uploadBusy} onClick={() => setDiscard(true)}>{t('Discard local changes', 'Yerel değişiklikleri bırak')}</button>}
    </form>
    {discard && <DraftConfirmation language={language} onStay={() => setDiscard(false)} onDiscard={() => { setDirty(false); setFrozen(null); setConflict(false); setDiscard(false); setSelected(null); setContent(service.blank()); setRequirements([]); setLinks([]); setFile(null); setReload(x => x + 1) }} />}
    <ul aria-label={t('Retained links', 'Korunan bağlantılar')}>{requirements.map(p => <li key={`${p.requirementId}/${p.requirementRevision}`}>{p.requirementId}/{p.requirementRevision} → {p.sourceId}/{p.sourceRevision}<button disabled={offline} onClick={() => { setSourceId(p.sourceId); setSourceOffset(0) }}>{t('Source history', 'Kaynak geçmişi')}</button><button disabled={locked} onClick={() => { setRequirements(ps => ps.filter(x => x !== p)); setDirty(true) }}>{t('Unlink in next revision', 'Sonraki sürümde bağlantıyı kaldır')}</button></li>)}{links.map(p => <li key={`${p.kind}/${p.id}/${p.revision}`}>{p.kind} {p.id}/{p.revision}<button disabled={locked} onClick={() => { setLinks(ps => ps.filter(x => x !== p)); setDirty(true) }}>{t('Unlink in next revision', 'Sonraki sürümde bağlantıyı kaldır')}</button></li>)}</ul>
    {source && <><ul aria-label={t('Linked source history', 'Bağlı kaynak geçmişi')}>{source.items.map(r => <li key={r.revision}>{r.id}/{r.revision} · {r.content.title.value} · {r.content.locator.value} · {r.hash} · {r.lifecycle}</li>)}</ul>{pages(sourceOffset, source.hasMore, setSourceOffset, t('Source history pages', 'Kaynak geçmişi sayfaları'))}</>}
    {selected && <section aria-label={t('Immutable history', 'Değişmez geçmiş')}><h3>{t('Immutable history', 'Değişmez geçmiş')}</h3><label>{t('Replay as of with UTC offset', 'Tekrar zamanı, UTC farkı ile')}<input value={asOf} onChange={e => setAsOf(e.target.value)} /></label><button disabled={offline || !!asOf && !timestamp(asOf)} onClick={() => { setHistoryAsOf(asOf); setHistoryOffset(0) }}>{t('Read historical projection', 'Tarihsel görünümü oku')}</button>{history?.items.map(r => <article key={r.revision}><h4>{r.revision} · {r.content.title}</h4><p>{r.savedAt} · {timeLabel(r.timeState)} · {r.hash}</p><p>{t('Completion unknown · Unverified · Effectiveness unknown', 'Tamamlanma bilinmiyor · Doğrulanmamış · Etkinlik bilinmiyor')}</p><p>{r.file ? `${r.file.id}/${r.file.revision} · ${r.file.checksum}` : t('No clean evidence file', 'Temiz kanıt dosyası yok')}</p><ul>{r.requirements.map(p => <li key={`${p.requirementId}/${p.requirementRevision}`}>{p.requirementId}/{p.requirementRevision} → {p.sourceId}/{p.sourceRevision} · {p.ruleId ?? '—'}/{p.ruleRevision ?? '—'}</li>)}{r.links.map(p => <li key={`${p.kind}/${p.id}/${p.revision}`}>{p.kind} {p.id}/{p.revision}</li>)}</ul><p>{t('Submission blocked: source admission and authorized verification remain unmet.', 'Gönderim engelli: kaynak kabulü ve yetkili doğrulama karşılanmadı.')}</p><details><summary>{t('Unmet policy details', 'Karşılanmayan politika ayrıntıları')}</summary><ul>{r.blockers.map(b => <li key={b}>{blockerLabel(b)}</li>)}</ul></details></article>)}{history && pages(historyOffset, history.hasMore, setHistoryOffset, t('History pages', 'Geçmiş sayfaları'))}</section>}
    {orphan && <p role="status">{orphan.available ? t('Current qualified catalog scope', 'Güncel nitelendirilmiş katalog kapsamı') : t('Current obligation input incomplete', 'Güncel yükümlülük girdisi eksik')} · {orphan.reason} · {orphan.uncontrolledObligations.map(o => `${o.requirementId}/${o.requirementRevision}`).join(' · ')}</p>}
    {selected?.kind === 'Evidence' && !dirty && !frozen && !uploadDirty && selected.links.filter(p => p.kind === 'Control').map(p => <HumanDecisionTrace key={`${selected.id}/${selected.revision}/${p.id}/${p.revision}`} anchor={{ vesselId: vessel, evidenceId: selected.id, evidenceRevision: selected.revision, controlId: p.id, controlRevision: p.revision }} language={language} csrf={csrf} offline={offline} onDirty={v => setReviewDrafts(current => current[p.id] === v ? current : { ...current, [p.id]: v })} onSaving={v => setReviewSaves(current => current[p.id] === v ? current : { ...current, [p.id]: v })} />)}
    <section aria-label={t('Critical orphan report', 'Kritik yetim raporu')}><h3>{t('Critical orphan report', 'Kritik yetim raporu')}</h3><p>{t('Authoritative applicability report unavailable. The following are critical declarations without requirement links only.', 'Yetkili uygulanabilirlik raporu kullanılamıyor. Aşağıdakiler yalnızca gereksinim bağlantısı olmayan kritik beyanlardır.')}</p>{orphan ? <><ul>{orphan.draftCriticalOrphans.map(id => <li key={id}>{id}</li>)}</ul>{pages(orphanOffset, orphan.hasMore, setOrphanOffset, t('Report pages', 'Rapor sayfaları'))}</> : <p>{t('Draft report unavailable.', 'Taslak raporu kullanılamıyor.')}</p>}</section>
    {kind === 'Evidence' && <VesselFiles offline={offline} vessel={vessel} language={language} csrf={csrf} onDirty={setUploadDirty} onSaving={setUploadBusy} onCommitted={() => setReload(x => x + 1)} />}
  </div>
}
