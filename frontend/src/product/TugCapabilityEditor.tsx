import { useEffect, useState } from 'react'
import { ApiError } from './api'
import { tugApi } from './tug-api'
import * as equipment from './technical-api'
import * as evidence from './control-evidence-api'
import { blankCapability, type CapabilityStatement } from './tug-capability-contract'

export function TugCapabilityEditor({ vessel, language, offline, value, onChange }: { vessel: string; language: 'tr' | 'en'; offline: boolean; value: CapabilityStatement | null; onChange: (v: CapabilityStatement | null) => void }) {
  const t = (en: string, tr: string) => language === 'en' ? en : tr
  const [assets, setAssets] = useState<equipment.Asset[]>([]), [conditions, setConditions] = useState<evidence.Row[]>([]), [offset, setOffset] = useState(0), [more, setMore] = useState(false)
  const [profileVersion, setProfileVersion] = useState<number | null>(null), [state, setState] = useState('idle')
  const hasValue = value !== null
  const resetScope1 = [vessel, hasValue, offline, offset] as const
  const [previousScope1, setPreviousScope1] = useState(resetScope1)
  // Withdraw stale state in the same render that changes the request scope.
  if (!Object.is(previousScope1[0], vessel) || !Object.is(previousScope1[1], hasValue) || !Object.is(previousScope1[2], offline) || !Object.is(previousScope1[3], offset)) {
    setPreviousScope1(resetScope1)
    setState(hasValue && !offline ? 'loading' : 'idle'); setAssets([]); setConditions([]); setProfileVersion(null)
  }
  useEffect(() => {
    if (!hasValue || offline) return
    const c = new AbortController();
    Promise.all([equipment.snapshot(vessel, c.signal), tugApi.context(vessel, c.signal), evidence.page(vessel, 'Evidence', offset, c.signal)])
      .then(([a, p, e]) => { if (c.signal.aborted) return; setAssets(a.items); setProfileVersion(p.bollardPull.version); setConditions(e.items.filter(r => r.content.tugCapability?.kind === 'Condition')); setMore(e.hasMore); setState('loaded') })
      .catch(e => { if (!c.signal.aborted) { setAssets([]); setConditions([]); setProfileVersion(null); setState(e instanceof ApiError && [401, 403, 404].includes(e.status) ? 'denied' : 'unavailable') } })
    return () => c.abort()
  }, [vessel, hasValue, offline, offset])
  const patch = (p: Partial<CapabilityStatement>) => { if (value) onChange({ ...value, ...p }) }
  return <fieldset aria-label={t('Owned tug capability evidence', 'Römorköre ait kapasite kanıtı')}>
    <legend>{t('Owned tug capability evidence', 'Römorköre ait kapasite kanıtı')}</legend>
    <label>{t('Capability record type', 'Kapasite kayıt türü')}<select disabled={offline} value={value?.kind ?? ''} onChange={e => onChange(e.target.value ? blankCapability(e.target.value as CapabilityStatement['kind']) : null)}><option value="">{t('No capability statement', 'Kapasite beyanı yok')}</option><option value="BollardPullTest">{t('Bollard pull certificate / measured test', 'Bollard pull sertifikası / ölçülen test')}</option><option value="Condition">{t('Equipment condition evidence', 'Ekipman kondisyon kanıtı')}</option></select></label>
    {value && <>
      <p>{t('Attach the owning certificate or condition document and exact source/control revisions below. Independent human verification is a separate command.', 'Aşağıdan ait olduğu sertifika veya kondisyon belgesini ve kesin kaynak/kontrol sürümlerini bağlayın. Bağımsız insan doğrulaması ayrı komuttur.')}</p>
      {state === 'loading' && <p role="status">{t('Loading current owning records…', 'Güncel sahip kayıtlar yükleniyor…')}</p>}
      {['denied', 'unavailable'].includes(state) && <p role="alert">{t('Current capability context unavailable.', 'Güncel kapasite bağlamı kullanılamıyor.')}</p>}
      {offline && <p role="status">{t('Capability evidence requires online current-source validation.', 'Kapasite kanıtı çevrimiçi güncel kaynak doğrulaması gerektirir.')}</p>}
      <fieldset disabled={offline || state !== 'loaded'}>
        <label>{t('Owning equipment revision', 'Ait olduğu ekipman sürümü')}<select value={value.assetId ?? ''} onChange={e => { const a = assets.find(a => a.id === e.target.value); patch({ assetId: a?.id ?? null, assetRevision: a?.revision ?? null, assetVersion: a?.version ?? null, profileVersion }) }}><option value="">{t('Unknown / incomplete', 'Bilinmiyor / eksik')}</option>{value.assetId && !assets.some(a => a.id === value.assetId) && <option value={value.assetId}>{t('Captured reference unavailable', 'Kaydedilen referans kullanılamıyor')}</option>}{assets.map(a => <option key={a.id} value={a.id}>{a.code ?? a.id} / r{a.revision}</option>)}</select></label>
        <button type="button" disabled={!value.assetId || !assets.some(a => a.id === value.assetId)} onClick={() => { const a = assets.find(a => a.id === value.assetId)!; patch({ assetRevision: a.revision, assetVersion: a.version, profileVersion }) }}>{t('Pin current equipment and profile versions', 'Güncel ekipman ve profil sürümlerini sabitle')}</button>
        <p>{t('Captured profile version', 'Kaydedilen profil sürümü')}: {value.profileVersion ?? t('Unknown', 'Bilinmiyor')} · {t('Equipment version', 'Ekipman sürümü')}: {value.assetVersion ?? '—'}</p>
        <label>{t('Exact measurement / condition source locator', 'Kesin ölçüm / kondisyon kaynak konumu')}<input maxLength={2000} value={value.sourceLocator ?? ''} onChange={e => patch({ sourceLocator: e.target.value || null })} /></label>
        {value.kind === 'Condition' ? <label>{t('Source supported condition factor (0–1)', 'Kaynak destekli kondisyon katsayısı (0–1)')}<input inputMode="decimal" value={value.conditionFactor ?? ''} onChange={e => patch({ conditionFactor: e.target.value || null })} /></label> : <>
          {(['certifiedTonnes', 'measuredTonnes', 'testAt', 'reviewAt'] as const).map((key, i) => <label key={key}>{[t('Certified BP (tonnes)', 'Sertifikalı BP (ton)'), t('Measured BP (tonnes)', 'Ölçülen BP (ton)'), t('Test date and UTC offset', 'Test tarihi ve UTC farkı'), t('Certificate review / expiry and UTC offset', 'Sertifika inceleme / sona erme ve UTC farkı')][i]}<input value={value[key] ?? ''} onChange={e => patch({ [key]: e.target.value || null })} /></label>)}
          <label>{t('Owning condition evidence revision', 'Ait olduğu kondisyon kanıt sürümü')}<select value={value.conditionEvidenceId ?? ''} onChange={e => { const r = conditions.find(r => r.id === e.target.value); patch({ conditionEvidenceId: r?.id ?? null, conditionEvidenceRevision: r?.revision ?? null, conditionEvidenceVersion: r?.version ?? null }) }}><option value="">{t('Unknown / incomplete', 'Bilinmiyor / eksik')}</option>{value.conditionEvidenceId && !conditions.some(r => r.id === value.conditionEvidenceId) && <option value={value.conditionEvidenceId}>{value.conditionEvidenceId}</option>}{conditions.map(r => <option key={r.id} value={r.id}>{r.content.title ?? r.id} / r{r.revision} · {r.verification}</option>)}</select></label>
          {conditions.length === 0 && <p>{t('No condition evidence on this visible page.', 'Görünür sayfada kondisyon kanıtı yok.')}</p>}
          <button type="button" disabled={offset === 0} onClick={() => setOffset(x => x - 25)}>{t('Previous condition records', 'Önceki kondisyon kayıtları')}</button><button type="button" disabled={!more} onClick={() => setOffset(x => x + 25)}>{t('More condition records', 'Diğer kondisyon kayıtları')}</button>
        </>}
      </fieldset>
    </>}
  </fieldset>
}
