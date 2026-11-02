import { CompliancePolicyEditor } from './CompliancePolicyEditor'
import { complianceFields } from './compliance-input-contract'
import { owningFields } from './operation-equipment-contract'
import { useEffect, useState } from 'react'
import { TugCalculationEditor } from './TugCalculationEditor'
import * as api from './qualification-api'
import { ApiError } from './api'
import { loadProfile } from './profile-api'
import type { Profile } from './profile-contract'

type Props = { rule: string; ruleRevision: number; vessel: string; language: 'tr' | 'en'; csrf: string; offline: boolean; onDirty: (v: boolean) => void; onSaving: (v: boolean) => void }
type Attempt = { body: string; key: string; action: 'events' | 'fact-attestations' }
export function RegulatoryQualification({ rule, ruleRevision, vessel, language, csrf, offline, onDirty, onSaving }: Props) {
  const t = (en: string, tr: string) => language === 'en' ? en : tr
  const [data, setData] = useState<api.Page | null>(null), [content, setContent] = useState(api.blank), [profile, setProfile] = useState<Profile | null>(null)
  const [historyRows, setHistoryRows] = useState<api.Qualification[]>([]), [historyMore, setHistoryMore] = useState(false)
  const [dirty, setDirty] = useState(false), [busy, setBusy] = useState(false), [error, setError] = useState<number | null>(null), [reload, setReload] = useState(0)
  const [attempt, setAttempt] = useState<Attempt | null>(null), [conflict, setConflict] = useState(false), [state, setState] = useState('ADOPTED'), [until, setUntil] = useState(''), [notice, setNotice] = useState('')
  useEffect(() => { onDirty(dirty || !!attempt || conflict) }, [dirty, attempt, conflict, onDirty])
  useEffect(() => { onSaving(busy) }, [busy, onSaving])
  useEffect(() => {
    if (offline) return
    let active = true; const c = new AbortController(); const timer = setTimeout(() => c.abort(), 15000)
    api.load(rule, vessel, c.signal).then(p => { if (active) { setData(p); setHistoryRows(p.items); setHistoryMore(p.hasMore); setContent(p.items[0]?.content ?? api.blank()); setError(null) } }).catch(e => { if (active) { setData(null); setError(e instanceof ApiError ? e.status : 0) } })
    if (vessel) loadProfile(vessel, c.signal).then(p => { if (active) setProfile(p) }).catch(() => { if (active) setProfile(null) }); else setProfile(null)
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [rule, ruleRevision, vessel, reload, offline])
  const current = data?.items[0]
  const locked = offline || busy || conflict || !!attempt || !data || [401, 403, 404].includes(error ?? -1)
  const edit = (patch: Partial<api.Content>) => { setContent(c => ({ ...c, ...patch })); setDirty(true) }
  async function send(a: Attempt) {
    if (offline || busy || conflict) return
    setAttempt(a); setBusy(true); setError(null)
    const c = new AbortController(); const timer = setTimeout(() => c.abort(), 15000)
    try { await api.send(rule, a, csrf, c.signal); setAttempt(null); setDirty(false); setReload(x => x + 1); setNotice(t('Human record saved. No operational release follows.', 'İnsan kaydı saklandı. Operasyonel serbest bırakma oluşturulmaz.')) }
    catch (e) { const status = e instanceof ApiError ? e.status : 0; setError(status); if ([400, 401, 403, 404, 409].includes(status)) setAttempt(null); if (status === 409) setConflict(true) }
    finally { setBusy(false); clearTimeout(timer) }
  }
  function transition(next: string) {
    const action = next === 'DRAFT' ? 'draft' : next === 'IN_REVIEW' ? 'review' : 'qualify'
    const authorityId = data?.authorities.find(a => a.action === action)?.authorityId
    if (!authorityId) return
    void send({ action: 'events', key: crypto.randomUUID(), body: JSON.stringify({ expectedVersion: current?.version ?? 0, ruleRevision, authorityId, state: next, content: next === 'DRAFT' ? content : null }) })
  }
  async function source(file: File | undefined) {
    if (!file) return
    if (file.size < 1 || file.size > 2097152) { setError(400); return }
    const bytes = new Uint8Array(await file.arrayBuffer()); let binary = ''; bytes.forEach(b => { binary += String.fromCharCode(b) })
    edit({ sourceBytesBase64: btoa(binary) })
  }
  const control = content.control
  const controlEdit = (patch: Partial<api.ControlPolicy>) => edit({ control: { ownerRole: content.ownerRole, frequency: null, evidencePolicy: null, verificationPolicy: null, maxAgeHours: null, requireFile: null, requireAcknowledgment: null, acknowledgmentRole: null, requiredVerificationMechanism: null, verificationMechanismSourceLocator: null, sourceLocator: null, ...control, ...patch } })
  return <section className="regulatory-qualification" aria-label={t('Source qualification and policy review', 'Kaynak nitelendirme ve politika incelemesi')}>
    <h3>{t('Source qualification and policy review', 'Kaynak nitelendirme ve politika incelemesi')}</h3>
    <p>{t('Human qualification records do not prove legal truth or verify an external signature. Required source mechanisms apply to their defined scope.', 'İnsan nitelendirme kayıtları hukuki doğruluğu kanıtlamaz veya dış imzayı doğrulamaz. Gerekli kaynak mekanizmaları tanımlı kapsamlarında uygulanır.')}</p>
    {offline && <p role="status">{t('Offline. Commands are unavailable.', 'Çevrimdışı. Komutlar kullanılamıyor.')}</p>}
    {!data && error === null && <p role="status">{t('Loading qualification…', 'Nitelendirme yükleniyor…')}</p>}
    {error !== null && <p role="alert">{error === 403 ? t('Assigned authority required.', 'Atanmış yetki gerekli.') : error === 409 ? t('Context or source gate needs review. Reload before a new command.', 'Bağlam veya kaynak kapısı incelenmeli. Yeni komuttan önce yenileyin.') : error === 0 ? t('Outcome ambiguous. Retry the exact retained command.', 'Sonuç belirsiz. Saklanan aynı komutu tekrar deneyin.') : `${t('Request unavailable', 'İstek kullanılamıyor')} (${error})`}</p>}
    {notice && <p role="status">{notice}</p>}
    {data?.items.length === 0 && <p>{t('No qualification. Incomplete drafts may be saved.', 'Nitelendirme yok. Eksik taslaklar kaydedilebilir.')}</p>}
    {current && current.ruleRevision !== ruleRevision && <p role="alert">{t('Rule revision changed. Save a new draft and repeat qualification.', 'Kural sürümü değişti. Yeni taslak kaydedip nitelendirmeyi tekrarlayın.')}</p>}
    {current && <p>{current.state} · {current.revision} · {current.savedAt} · {current.policyVersion}<br /><span className="register-hash">{current.sourceHash || '—'} · {current.hash}</span><br />{current.blockers.join(' · ')}<br />{current.contract?.sourceLocator} · {current.contract?.requiredMechanism}</p>}
    {current?.sourceHash && data && data.authorities.length > 0 && <a href={`/api/v1/regulatory-source-governance/${rule}/revisions/${current.revision}/source`} download>{t('Inspect exact immutable source bytes', 'Tam değişmez kaynak baytlarını incele')}</a>}
    <fieldset disabled={locked || !data?.actions.includes('draft')}><legend>{t('Exact source and reviewed policy', 'Tam kaynak ve incelenmiş politika')}</legend>
      <label>{t('Exact source bytes (maximum 2 MiB)', 'Tam kaynak baytları (en çok 2 MiB)')}<input type="file" onChange={e => void source(e.target.files?.[0])} /></label>
      {(['publisher', 'edition', 'validationReview', 'trigger', 'impactReview', 'supersedingRuleId'] as const).map((key, i) => <label key={key}>{[t('Publisher', 'Yayıncı'), t('Edition', 'Baskı'), t('Validation review', 'Geçerleme incelemesi'), t('Acceptance trigger', 'Kabul tetikleyicisi'), t('Impact review', 'Etki incelemesi'), t('Superseding rule ID', 'Yerine geçen kural kimliği')][i]}<input value={content[key] ?? ''} maxLength={2000} onChange={e => edit({ [key]: e.target.value || null })} /></label>)}
      <label>{t('Superseding rule revision', 'Yerine geçen kural sürümü')}<input type="number" min={1} value={content.supersedingRuleRevision ?? ''} onChange={e => edit({ supersedingRuleRevision: e.target.value ? Number(e.target.value) : null })} /></label>
      <label>{t('Appointed source owner and role', 'Atanmış kaynak sahibi ve rolü')}<select value={content.ownerId ? `${content.ownerId}/${content.ownerRole}` : ''} onChange={e => { const [ownerId, ownerRole] = e.target.value.split('/'); edit({ ownerId: ownerId || null, ownerRole: ownerRole || null }) }}><option value="">{t('Missing', 'Eksik')}</option>{data?.owners.map(o => <option key={`${o.actorId}/${o.roleId}`} value={`${o.actorId}/${o.roleId}`}>{o.actorId} / {o.roleId}</option>)}</select></label>
      {[...api.profileInputs, ...owningFields, ...complianceFields].map(field => { const p = content.inputs?.find(x => x.field === field); return <label key={field}>{field}<select value={p?.basis ?? ''} onChange={e => edit({ inputs: [...(content.inputs ?? []).filter(x => x.field !== field), ...(e.target.value ? [{ field, basis: e.target.value as api.InputPolicy['basis'], maxAgeHours: p?.maxAgeHours ?? null }] : [])] })}><option value="">{t('Unqualified / Unknown', 'Nitelendirilmemiş / Bilinmiyor')}</option>{!api.verifiedProfileInputs.includes(field) && !owningFields.includes(field) && !complianceFields.includes(field) && <option value="Declared">{t('Known declaration', 'Bilinen beyan')}</option>}<option value="HumanAttested">{t('Human attested profile', 'İnsan tarafından doğrulanmış profil')}</option><option value="ProviderVerified">{t('External provider required', 'Dış sağlayıcı gerekli')}</option></select></label> })}
      {(['ownerRole', 'frequency', 'evidencePolicy', 'verificationPolicy', 'sourceLocator'] as const).map((key, i) => <label key={key}>{[t('Control owner role ID', 'Kontrol sahibi rol kimliği'), t('Frequency', 'Sıklık'), t('Evidence policy', 'Kanıt politikası'), t('Verification policy', 'Doğrulama politikası'), t('Policy source locator', 'Politika kaynak konumu')][i]}<input value={control?.[key] ?? ''} onChange={e => controlEdit({ [key]: e.target.value || null })} /></label>)}
      <label>{t('Evidence freshness in hours', 'Kanıt güncelliği (saat)')}<input type="number" min={1} max={87600} value={control?.maxAgeHours ?? ''} onChange={e => controlEdit({ maxAgeHours: e.target.value ? Number(e.target.value) : null })} /></label>
      <label>{t('Required evidence verification mechanism', 'Gerekli kanıt doğrulama mekanizması')}<input value={control?.requiredVerificationMechanism ?? ''} onChange={e => controlEdit({ requiredVerificationMechanism: e.target.value || null })} /></label>
      <label>{t('Verification mechanism source locator', 'Doğrulama mekanizması kaynak konumu')}<input value={control?.verificationMechanismSourceLocator ?? ''} onChange={e => controlEdit({ verificationMechanismSourceLocator: e.target.value || null })} /></label>
      <label>{t('Required acknowledgment role ID', 'Gerekli teyit rol kimliği')}<input value={control?.acknowledgmentRole ?? ''} onChange={e => controlEdit({ acknowledgmentRole: e.target.value || null })} /></label>
      {(['requireFile', 'requireAcknowledgment'] as const).map(key => <label key={key}>{key === 'requireFile' ? t('Source requires an immutable file', 'Kaynak değişmez dosya gerektiriyor') : t('Source requires exact revision acknowledgment', 'Kaynak tam sürüm teyidi gerektiriyor')}<select value={control?.[key] == null ? '' : String(control[key])} onChange={e => controlEdit({ [key]: e.target.value === '' ? null : e.target.value === 'true' })}><option value="">{t('Unknown', 'Bilinmiyor')}</option><option value="true">{t('Yes', 'Evet')}</option><option value="false">{t('No', 'Hayır')}</option></select></label>)}
      <fieldset><legend>{t('Source literal bindings', 'Kaynak değer eşlemeleri')}</legend>
        <p>{t('Exact owning value, exact rule value and source locator. No aliases are implied.', 'Tam kaynak değeri, tam kural değeri ve kaynak konumu. Örtük eşleme yoktur.')}</p>
        {(content.literalBindings ?? []).map((b, i) => <div key={i}>{(['field', 'ownerValue', 'ruleValue', 'sourceLocator'] as const).map(k => <label key={k}>{k}<input maxLength={2000} value={b[k] ?? ''} onChange={e => edit({ literalBindings: content.literalBindings!.map((x, n) => n === i ? { ...x, [k]: e.target.value || null } : x) })} /></label>)}<button type="button" onClick={() => edit({ literalBindings: content.literalBindings!.filter((_, n) => n !== i) })}>{t('Remove binding', 'Eşlemeyi kaldır')}</button></div>)}
        <button type="button" onClick={() => edit({ literalBindings: [...(content.literalBindings ?? []), { field: 'operation.jobType', ownerValue: null, ruleValue: null, sourceLocator: null }] })}>{t('Add source binding', 'Kaynak eşlemesi ekle')}</button>
      </fieldset>
      <fieldset><legend>{t('Owning document source policies', 'Kaynak belge politikaları')}</legend>
        {(content.owningDocuments ?? []).map((p, i) => <div key={p.documentKind}><p>{p.documentKind}</p>{(['authorRole', 'verifierRole', 'sourceLocator'] as const).map(k => <label key={k}>{k}<input value={p[k] ?? ''} onChange={e => edit({ owningDocuments: content.owningDocuments!.map((x, n) => n === i ? { ...x, [k]: e.target.value || null } : x) })} /></label>)}<button type="button" onClick={() => edit({ owningDocuments: content.owningDocuments!.filter((_, n) => n !== i) })}>{t('Remove policy', 'Politikayı kaldır')}</button></div>)}
        {['Operation', 'ServiceAuthorization', 'CargoPlan', 'WinchFitment', 'ClassPack', 'Msmd', 'CrewRoleAssessment'].filter(k => !content.owningDocuments?.some(p => p.documentKind === k)).map(k => <button type="button" key={k} onClick={() => edit({ owningDocuments: [...(content.owningDocuments ?? []), { documentKind: k, authorRole: null, verifierRole: null, sourceLocator: null }] })}>{t('Add document policy', 'Belge politikası ekle')} · {k}</button>)}
      </fieldset>
      <CompliancePolicyEditor value={content} onChange={edit} language={language} />
      <TugCalculationEditor language={language} value={content.tugCalculation ?? null} onChange={v => edit({ tugCalculation: v })} />
      <button type="button" onClick={() => transition('DRAFT')}>{t('Save qualification draft', 'Nitelendirme taslağını kaydet')}</button>
    </fieldset>
    <fieldset disabled={locked || dirty}><legend>{t('Human review and explicit lifecycle', 'İnsan incelemesi ve açık yaşam döngüsü')}</legend>
      <button disabled={!data?.actions.includes('review') || current?.state !== 'DRAFT'} onClick={() => transition('IN_REVIEW')}>{t('Submit for qualification review', 'Nitelendirme incelemesine gönder')}</button>
      <label>{t('Record lifecycle state', 'Yaşam döngüsü durumunu kaydet')}<select value={state} onChange={e => setState(e.target.value)}>{['ADOPTED', 'AWAITING', 'FUTURE_EFFECTIVE', 'IN_FORCE', 'SUPERSEDED'].map(s => <option key={s}>{s}</option>)}</select></label>
      <button disabled={!data?.actions.includes('qualify')} onClick={() => transition(state)}>{t('Record assigned human qualification', 'Atanmış insan nitelendirmesini kaydet')}</button>
      {data?.actions.includes('attest') && <><p>{t('Attests existing known profile values; an edit requires a new attestation.', 'Mevcut bilinen profil değerlerini doğrular; düzenleme yeni teyit gerektirir.')} {profile?.revision}</p><label>{t('Attestation valid until (UTC offset)', 'Teyit geçerlilik sonu (UTC farkı)')}<input value={until} onChange={e => setUntil(e.target.value)} /></label><button disabled={!profile || !until} onClick={() => { const authorityId = data.authorities.find(a => a.action === 'attest')?.authorityId; if (authorityId && profile) void send({ action: 'fact-attestations', key: crypto.randomUUID(), body: JSON.stringify({ authorityId, vesselId: vessel, profileRevision: profile.revision, profileVersion: profile.version, qualificationRevision: current?.revision, qualificationVersion: current?.version, fields: content.inputs?.filter(p => p.basis === 'HumanAttested' && api.profileInputs.includes(p.field)).map(p => p.field) ?? [], validUntil: until }) }) }}>{t('Attest current profile revision', 'Güncel profil sürümünü teyit et')}</button></>}
    </fieldset>
    {attempt && <button disabled={offline || busy || conflict} onClick={() => void send(attempt)}>{t('Retry exact qualification command', 'Aynı nitelendirme komutunu tekrar dene')}</button>}
    <button disabled={busy} onClick={() => { setAttempt(null); setDirty(false); setConflict(false); setReload(x => x + 1) }}>{t('Discard local changes and reload', 'Yerel değişiklikleri bırak ve yenile')}</button>
    <details><summary>{t('Immutable qualification history', 'Değişmez nitelendirme geçmişi')}</summary>{historyRows.map(row => <p key={row.revision}>{row.revision} · {row.state} · {row.savedAt} · {row.hash} · {row.authorityHash}</p>)}<button disabled={locked || !historyMore || historyRows.length === 0} onClick={() => { const c = new AbortController(); api.load(rule, vessel, c.signal, historyRows[historyRows.length - 1].revision).then(p => { setHistoryRows(rows => [...rows, ...p.items]); setHistoryMore(p.hasMore) }).catch(() => setError(403)) }}>{t('More source history', 'Diğer kaynak geçmişi')}</button></details>
  </section>
}
