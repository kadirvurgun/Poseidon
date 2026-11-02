import { useEffect, useState } from 'react'
import type { Content, Row } from './control-evidence-api'
import * as graph from './control-evidence-api'
import { tugApi, type TugBoard } from './tug-api'
import * as technical from './technical-api'
import type { OwningContent } from './operation-equipment-contract'

export function OperationEquipmentEditor({ vessel, language, offline, isTug, value, onChange }: { vessel: string; language: 'en' | 'tr'; offline: boolean; isTug: boolean; value: Content; onChange: (v: Content) => void }) {
  const t = (en: string, tr: string) => language === 'en' ? en : tr
  const [jobs, setJobs] = useState<TugBoard | null>(null), [offset, setOffset] = useState(0), [assets, setAssets] = useState<technical.Asset[]>([])
  const [evidence, setEvidence] = useState<graph.Page | null>(null), [evidenceOffset, setEvidenceOffset] = useState(0), [error, setError] = useState(false)
  const key = (['operation', 'serviceAuthorization', 'cargoPlan', 'winchFitment'] as const).find(k => value[k] != null)
  useEffect(() => {
    setJobs(null); setAssets([]); setEvidence(null); setError(false)
    if (offline) return
    const c = new AbortController(); let active = true
    void Promise.allSettled([isTug ? tugApi.board(vessel, '', '', offset, c.signal) : Promise.resolve(null), technical.snapshot(vessel, c.signal), graph.page(vessel, 'Evidence', evidenceOffset, c.signal)]).then(([j, a, e]) => {
      if (!active) return
      if (j.status === 'fulfilled') setJobs(j.value)
      if (a.status === 'fulfilled') setAssets(a.value.items)
      if (e.status === 'fulfilled') setEvidence(e.value)
      setError([j, a, e].some(r => r.status === 'rejected'))
    })
    return () => { active = false; c.abort() }
  }, [vessel, offline, isTug, offset, evidenceOffset])
  const update = (v: OwningContent) => onChange({ ...value, ...v })
  const choose = (k: string) => onChange(Object.assign({}, value, { tugCapability: null, classPack: null, msmd: null, crewRoleAssessment: null, operation: null, serviceAuthorization: null, cargoPlan: null, winchFitment: null },
    (k === 'operation' ? { operation: { jobId: null, jobRevision: null, jobVersion: null, planRevision: null, planVersion: null, profile: null, serviceAuthorizationId: null, serviceAuthorizationRevision: null } } :
      k === 'serviceAuthorization' ? { serviceAuthorization: { jobId: null, jobRevision: null, jobVersion: null, planRevision: null, planVersion: null, serviceContext: null, issuerReference: null } } :
      k === 'cargoPlan' ? { cargoPlan: { planReference: null, packagedDangerousGoods: null } } : k === 'winchFitment' ? { winchFitment: { assetId: null, assetRevision: null, assetVersion: null, fitted: null, registerReference: null } } : {})))
  const bool = (label: string, v: boolean | null, change: (v: boolean | null) => void) => <label>{label}<select value={v == null ? '' : String(v)} onChange={e => change(e.target.value === '' ? null : e.target.value === 'true')}><option value="">{t('Unknown', 'Bilinmiyor')}</option><option value="true">{t('Yes', 'Evet')}</option><option value="false">{t('No', 'Hayır')}</option></select></label>
  const jobRef = value.operation ?? value.serviceAuthorization
  return <fieldset disabled={offline}><legend>{t('Owning operation, cargo or equipment document', 'Operasyon, yük veya donanım kaynak kaydı')}</legend>
    <p>{t('Capture remains unverified. Attach the owning document and use assigned independent review. Intake and plan geometry do not authorize service.', 'Kayıt doğrulanmamış kalır. Kaynak belgesini ekleyip atanmış bağımsız incelemeyi kullanın. Talep ve plan geometrisi hizmet yetkisi vermez.')}</p>
    {error && <p role="alert">{t('Some current references are unavailable. Reload before changing their bindings.', 'Bazı güncel referanslar kullanılamıyor. Bağlantılarını değiştirmeden yeniden yükleyin.')}</p>}
    <label>{t('Document purpose', 'Belge amacı')}<select value={key ?? ''} onChange={e => choose(e.target.value)}><option value="">{t('Ordinary evidence', 'Genel kanıt')}</option>{isTug && <><option value="operation">{t('Operation reference', 'Operasyon referansı')}</option><option value="serviceAuthorization">{t('Shore service authorization record', 'Kara hizmet yetki kaydı')}</option></>}<option value="cargoPlan">{t('Cargo plan', 'Yük planı')}</option><option value="winchFitment">{t('Towing winch fitment record', 'Çekme vinci donatım kaydı')}</option></select></label>
    {jobRef && <><label>{t('Current job and plan', 'Güncel iş ve plan')}<select value={jobRef.jobId ?? ''} onChange={e => {
      const row = jobs?.items.find(j => j.request.id === e.target.value), plan = row?.draftPlan
      const ref = { jobId: row?.request.id ?? null, jobRevision: row?.request.revision ?? null, jobVersion: row?.request.version ?? null, planRevision: plan?.revision ?? null, planVersion: plan?.version ?? null }
      if (value.operation) update({ operation: { ...value.operation, ...ref, serviceAuthorizationId: null, serviceAuthorizationRevision: null } })
      else if (value.serviceAuthorization) update({ serviceAuthorization: { ...value.serviceAuthorization, ...ref } })
    }}><option value="">{t('Select', 'Seçiniz')}</option>{jobRef.jobId && !jobs?.items.some(j => j.request.id === jobRef.jobId) && <option value={jobRef.jobId}>{t('Stored reference; reload current job', 'Kayıtlı referans; güncel işi yükleyin')}</option>}{jobs?.items.map(j => <option key={j.request.id} value={j.request.id}>{j.request.jobNumber} · {j.request.inputs.jobType} · {j.request.inputs.operationArea}</option>)}</select></label><button type="button" disabled={offset === 0} onClick={() => setOffset(v => v - 25)}>{t('Previous jobs', 'Önceki işler')}</button><button type="button" disabled={!jobs?.hasMore} onClick={() => setOffset(v => v + 25)}>{t('More jobs', 'Diğer işler')}</button></>}
    {value.operation && <><label>{t('Operation profile literal from document', 'Belgedeki operasyon profili')}<input value={value.operation.profile ?? ''} maxLength={2000} onChange={e => update({ operation: { ...value.operation!, profile: e.target.value || null } })} /></label><label>{t('Service authorization evidence', 'Hizmet yetki kanıtı')}<select value={value.operation.serviceAuthorizationId ?? ''} onChange={e => { const r = evidence?.items.find(r => r.id === e.target.value); update({ operation: { ...value.operation!, serviceAuthorizationId: r?.id ?? null, serviceAuthorizationRevision: r?.revision ?? null } }) }}><option value="">{t('Unknown', 'Bilinmiyor')}</option>{evidence?.items.filter(r => r.content.serviceAuthorization).map(r => <option key={r.id} value={r.id}>{r.content.title} · {r.verification}</option>)}</select></label></>}
    {value.serviceAuthorization && <>{(['serviceContext', 'issuerReference'] as const).map(k => <label key={k}>{k === 'serviceContext' ? t('Service context literal from authorization', 'Yetki belgesindeki hizmet bağlamı') : t('Issuing authority document reference', 'Düzenleyen makam belge referansı')}<input maxLength={2000} value={value.serviceAuthorization?.[k] ?? ''} onChange={e => update({ serviceAuthorization: { ...value.serviceAuthorization!, [k]: e.target.value || null } })} /></label>)}</>}
    {value.cargoPlan && <><label>{t('Cargo plan reference', 'Yük planı referansı')}<input maxLength={2000} value={value.cargoPlan.planReference ?? ''} onChange={e => update({ cargoPlan: { ...value.cargoPlan!, planReference: e.target.value || null } })} /></label>{bool(t('Packaged dangerous goods declaration', 'Ambalajlı tehlikeli yük beyanı'), value.cargoPlan.packagedDangerousGoods, v => update({ cargoPlan: { ...value.cargoPlan!, packagedDangerousGoods: v } }))}</>}
    {value.winchFitment && <><label>{t('Equipment register entry', 'Donanım kayıt girdisi')}<select value={value.winchFitment.assetId ?? ''} onChange={e => { const a = assets.find(a => a.id === e.target.value); update({ winchFitment: { ...value.winchFitment!, assetId: a?.id ?? null, assetRevision: a?.revision ?? null, assetVersion: a?.version ?? null } }) }}><option value="">{t('Select', 'Seçiniz')}</option>{assets.map(a => <option key={a.id} value={a.id}>{a.code} · {a.name}</option>)}</select></label><label>{t('Fitment document reference', 'Donatım belge referansı')}<input maxLength={2000} value={value.winchFitment.registerReference ?? ''} onChange={e => update({ winchFitment: { ...value.winchFitment!, registerReference: e.target.value || null } })} /></label>{bool(t('Towing winch fitted declaration', 'Çekme vinci donatılmış beyanı'), value.winchFitment.fitted, v => update({ winchFitment: { ...value.winchFitment!, fitted: v } }))}</>}
    {key === 'operation' && <><button type="button" disabled={evidenceOffset === 0} onClick={() => setEvidenceOffset(v => v - 25)}>{t('Previous evidence', 'Önceki kanıtlar')}</button><button type="button" disabled={!evidence?.hasMore} onClick={() => setEvidenceOffset(v => v + 25)}>{t('More evidence', 'Diğer kanıtlar')}</button></>}
  </fieldset>
}

export function OwningEvaluationSelector({ vessel, language, offline, disabled = false, onChange }: { vessel: string; language: 'tr' | 'en'; offline: boolean; disabled?: boolean; onChange: (refs: { operationId: string | null; cargoPlanId: string | null; winchFitmentId: string | null } | null) => void }) {
  const [rows, setRows] = useState<Row[]>([]), [offset, setOffset] = useState(0), [more, setMore] = useState(false), [error, setError] = useState(false)
  const [refs, setRefs] = useState({ operationId: null as string | null, cargoPlanId: null as string | null, winchFitmentId: null as string | null })
  const t = (en: string, tr: string) => language === 'en' ? en : tr
  useEffect(() => { setRefs({ operationId: null, cargoPlanId: null, winchFitmentId: null }); onChange(null) }, [vessel, offline])
  useEffect(() => {
    setRows([]); if (!vessel || offline) { onChange(null); return }
    const c = new AbortController(); let active = true
    graph.page(vessel, 'Evidence', offset, c.signal).then(p => { if (active) { setRows(p.items); setMore(p.hasMore); setError(false) } }).catch(() => { if (active) { setError(true); setRefs({ operationId: null, cargoPlanId: null, winchFitmentId: null }); onChange(null) } })
    return () => { active = false; c.abort() }
  }, [vessel, offline, offset])
  return <fieldset disabled={offline || error || disabled}><legend>{t('Owning input references', 'Kaynak girdi referansları')}</legend>{error && <p role="alert">{t('Current evidence access unavailable', 'Güncel kanıt erişimi kullanılamıyor')}</p>}{(['operationId', 'cargoPlanId', 'winchFitmentId'] as const).map((k, i) => <label key={k}>{[t('Operation', 'Operasyon'), t('Cargo plan', 'Yük planı'), t('Winch fitment', 'Vinç donatımı')][i]}<select value={refs[k] ?? ''} onChange={e => { const next = { ...refs, [k]: e.target.value || null }; setRefs(next); onChange(Object.values(next).some(Boolean) ? next : null) }}><option value="">{t('Unknown / no reference', 'Bilinmiyor / referans yok')}</option>{refs[k] && !rows.some(r => r.id === refs[k]) && <option value={refs[k]!}>{t('Selected reference', 'Seçili referans')}</option>}{rows.filter(r => [r.content.operation, r.content.cargoPlan, r.content.winchFitment][i]).map(r => <option key={r.id} value={r.id}>{r.content.title} · {r.verification}</option>)}</select></label>)}<button type="button" disabled={offset === 0} onClick={() => setOffset(v => v - 25)}>{t('Previous references', 'Önceki referanslar')}</button><button type="button" disabled={!more} onClick={() => setOffset(v => v + 25)}>{t('More references', 'Diğer referanslar')}</button></fieldset>
}
