import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { EnterpriseChrome } from './EnterpriseChrome'
import { Button } from '../components/ui/button'
import { EnterprisePageHeader, EnterprisePagination } from './EnterprisePatterns'
import { DraftConfirmation } from './DraftConfirmation'
import { ApiError, session, vessels, type Session } from './api'
import { isGuid } from './identifiers'
import { certificatesApi, type CertificateCommand } from './certificates-api'
import { emptyCertificateRenewal, emptyVesselCertificate, type CertificateRenewalContent, type VesselCertificateContent } from './certificate-content'
import { certificateLocationPath, readCertificateLocation, useCertificateLocation, type CertificateLocation } from './certificate-location'
import { CertificateCenterView } from './CertificateCenterView'
import { CertificateEditorView } from './CertificateEditorView'
import { CertificateWorkflowHistory } from './CertificateWorkflowHistory'
import type { CertificateWorkflowEditor, CertificateWorkspaceSource } from './certificate-workspace-model'
import { useWorkspaceRevalidation } from './use-workspace-revalidation'
import './certificates-enterprise.css'

const ownerOf = (user?: Session | null) => user?.authenticated && user.canRead && isGuid(user.tenantId) && isGuid(user.actorId) ? user.tenantId + '/' + user.actorId : null
export function CertificateWorkspace() {
  const [language, setLanguage] = useState<'en' | 'tr'>('tr'), [pending, setPending] = useState<(() => void) | null>(null), [instance, setInstance] = useState(0)
  const dirty = useRef(false), saving = useRef(false)
  const onDirty = useCallback((value: boolean) => { dirty.current = value }, []), onSaving = useCallback((value: boolean) => { saving.current = value }, [])
  const clear = () => { dirty.current = false; setInstance(n => n + 1) }
  const guard = (action: () => void) => { if (saving.current) return; if (dirty.current) setPending(() => action); else action() }
  const { route, move } = useCertificateLocation(guard, clear)
  const navigate = (href: string, committed?: () => void) => {
    if (href !== '/app' && !href.startsWith('/app/') || /[\\\u0000-\u001f]/u.test(href)) return
    const next = readCertificateLocation(href)
    guard(() => { committed?.(); clear(); if (next.invalid) window.location.assign(href); else move(next) })
  }
  useEffect(() => {
    const unload = (event: BeforeUnloadEvent) => { if (dirty.current || saving.current) event.preventDefault() }
    window.addEventListener('beforeunload', unload)
    return () => window.removeEventListener('beforeunload', unload)
  }, [])
  return <><CertificateSurface key={certificateLocationPath(route) + '/' + instance} route={route} language={language} onLanguage={setLanguage} onNavigate={navigate} onDirty={onDirty} onSaving={onSaving} />{pending && <DraftConfirmation language={language} onStay={() => setPending(null)} onDiscard={() => { const action = pending; setPending(null); clear(); action() }} />}</>
}

function CertificateSurface({ route, language, onLanguage, onNavigate, onDirty, onSaving }: { route: CertificateLocation; language: 'en' | 'tr'; onLanguage: (language: 'en' | 'tr') => void; onNavigate: (href: string, committed?: () => void) => void; onDirty: (value: boolean) => void; onSaving: (value: boolean) => void }) {
  const en = language === 'en', epoch = useWorkspaceRevalidation(), [attempt, retry] = useState(0), [historyOffset, setHistoryOffset] = useState(0)
  const [source, setSource] = useState<CertificateWorkspaceSource | null>(null), [editor, setEditor] = useState<CertificateWorkflowEditor | null>(null)
  const [busy, setBusy] = useState(false), [failure, setFailure] = useState<ApiError | null>(null), [saved, setSaved] = useState(false)
  const [frozen, setFrozen] = useState<{ owner: string; command: CertificateCommand; uncertain: boolean } | null>(null)
  const running = useRef(false), mounted = useRef(false), writer = useRef<AbortController | null>(null), liveOwner = useRef<string | null>(null)
  const online = navigator.onLine, visible = document.visibilityState !== 'hidden', scope = epoch + '/' + attempt + '/' + historyOffset
  const current = source?.scope === scope && online && visible ? source : null, user = current?.user, owner = ownerOf(user)
  const ownerChanged = !!editor && editor.owner !== owner || !!frozen && frozen.owner !== owner
  const sourceChanged = editor?.kind === 'certificate' && editor.record && current?.certificate
    ? editor.record.version !== current.certificate.version || editor.record.revision !== current.certificate.revision
    : editor?.kind === 'renewal' && current?.certificate
      ? (editor.record?.certificateVersion ?? editor.certificate.version) !== current.certificate.version || (editor.record?.certificateRevision ?? editor.certificate.revision) !== current.certificate.revision || !!editor.record && !!current.renewal && editor.record.version !== current.renewal.version
      : false
  const conflict = (failure?.status === 409 || sourceChanged) && !frozen?.uncertain, denied = !!failure && [401, 403, 404].includes(failure.status)
  const title = route.page === 'center' ? (en ? 'Certificate command center' : 'Sertifika merkezi') : (en ? 'Certificate renewal' : 'Sertifika yenileme')
  const base = certificateLocationPath({ ...route, id: null, certificateId: undefined, offset: 0 })
  useLayoutEffect(() => { liveOwner.current = owner }, [owner])
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; writer.current?.abort() } }, [])
  useEffect(() => { document.documentElement.lang = language; document.title = 'POSEIDON · ' + title }, [language, title])
  useEffect(() => {
    if (!online || !visible || route.invalid) return
    const c = new AbortController(); let active = true
    const timer = setTimeout(() => c.abort(), 10000)
    async function load() {
      const user = await session(c.signal), identity = ownerOf(user)
      if (!identity) throw new ApiError(403)
      if (!route.vessel) {
        const page = await vessels(route.offset, '', c.signal)
        if (active) setSource({ scope, user, vessels: page.items, hasMore: page.hasMore })
        return
      }
      if (route.page === 'center') {
        const caps = await certificatesApi.capabilities(route.vessel, c.signal)
        if (route.id) {
          const [certificate, certificateHistory, renewalCaps] = await Promise.all([
            certificatesApi.read(route.vessel, route.id, c.signal), certificatesApi.history(route.vessel, route.id, historyOffset, c.signal),
            certificatesApi.renewalCapabilities(route.vessel, c.signal).catch(() => null),
          ])
          if (certificate.scope !== 'Vessel') throw new ApiError(403)
          if (active) { setSource({ scope, user, caps, certificate, certificateHistory, renewalCaps }); setEditor(previous => previous ?? { kind: 'certificate', owner: identity, id: certificate.id, record: certificate, content: structuredClone(certificate.content), dirty: false }) }
        } else {
          const selectedScope = route.scope ?? caps.scopes[0]
          if (!selectedScope || !caps.scopes.includes(selectedScope)) throw new ApiError(403)
          const center = await certificatesApi.center(route.vessel, selectedScope, route.offset, route.temporal ?? null, caps.crewPurpose, c.signal)
          if (active) setSource({ scope, user, caps, center })
        }
      } else {
        const renewalCaps = await certificatesApi.renewalCapabilities(route.vessel, c.signal)
        if (route.id) {
          const [renewal, renewalHistory] = await Promise.all([certificatesApi.renewal(route.vessel, route.id, c.signal), certificatesApi.renewalHistory(route.vessel, route.id, historyOffset, c.signal)])
          const certificate = await certificatesApi.read(route.vessel, renewal.certificateId, c.signal)
          if (active) { setSource({ scope, user, renewalCaps, renewal, renewalHistory, certificate }); setEditor(previous => previous ?? { kind: 'renewal', owner: identity, id: renewal.id, record: renewal, certificate, content: structuredClone(renewal.content), dirty: false }) }
        } else if (route.certificateId) {
          const certificate = await certificatesApi.read(route.vessel, route.certificateId, c.signal)
          if (active) { setSource({ scope, user, renewalCaps, certificate }); if (renewalCaps.canWrite && certificate.currentRevision && certificate.state !== 'SUPERSEDED') setEditor(previous => previous ?? { kind: 'renewal', owner: identity, id: crypto.randomUUID(), certificate, content: emptyCertificateRenewal(), dirty: false }) }
        } else {
          const renewalPage = await certificatesApi.renewals(route.vessel, route.offset, c.signal)
          if (active) setSource({ scope, user, renewalCaps, renewalPage })
        }
      }
      if (active) setFailure(previous => previous && [401, 403, 404].includes(previous.status) ? null : previous)
    }
    load().catch(() => { if (active) setSource({ scope, failed: true }) }).finally(() => clearTimeout(timer))
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [route, scope, historyOffset, online, visible])

  const permitted = editor?.kind === 'certificate' ? !!current?.caps?.canWriteVessel : !!current?.renewalCaps?.canWrite
  const headEditable = !!editor && (!editor.record || editor.record.canEdit && editor.record.currentRevision && editor.record.state === 'DRAFT')
  const editable = !!owner && !ownerChanged && permitted && headEditable && !busy && !frozen && !conflict && !denied
  const changeCertificate = (content: VesselCertificateContent) => { if (editor?.kind !== 'certificate' || !editable) return; setEditor({ ...editor, content, dirty: true }); onDirty(true); setSaved(false); setFailure(null) }
  const changeRenewal = (content: CertificateRenewalContent) => { if (editor?.kind !== 'renewal' || !editable) return; setEditor({ ...editor, content, dirty: true }); onDirty(true); setSaved(false); setFailure(null) }
  async function save() {
    if (!editor || !route.vessel || !owner || editor.owner !== owner || frozen && frozen.owner !== owner || running.current || denied || conflict || !permitted || !frozen && !editable) return
    const expectedVersion = editor.record?.version ?? 0, content = structuredClone(editor.content)
    const body = editor.kind === 'certificate'
      ? { id: editor.id, expectedVersion, content, reasonCode: editor.record ? 'AUTHORING_CORRECTION' : 'INITIAL_AUTHORING' }
      : { id: editor.id, expectedVersion, certificateId: editor.record?.certificateId ?? editor.certificate.id, certificateRevision: editor.record?.certificateRevision ?? editor.certificate.revision, certificateVersion: editor.record?.certificateVersion ?? editor.certificate.version, content, reasonCode: editor.record ? 'AUTHORING_CORRECTION' : 'INITIAL_AUTHORING' }
    const command = frozen?.command ?? { key: crypto.randomUUID(), id: editor.id, vessel: route.vessel, expectedVersion, body: JSON.stringify(body) }, wasUnknown = frozen?.uncertain ?? false
    running.current = true; setBusy(true); onSaving(true); setFrozen({ owner, command, uncertain: wasUnknown }); setFailure(null); setSaved(false)
    const c = new AbortController(); writer.current = c; const timer = setTimeout(() => c.abort(), 15000)
    let uncertain = false, confirmed = false
    try {
      const fresh = await session(c.signal)
      if (ownerOf(fresh) !== owner) { setSource({ scope, failed: true }); throw new ApiError(403, 'identity_changed') }
      if (editor.kind === 'certificate') {
        const receipt = await certificatesApi.save(command, fresh.csrfToken, c.signal)
        if (!mounted.current || liveOwner.current !== owner) { uncertain = true; return }
        confirmed = true; setFrozen(null); onDirty(false); setSaved(true)
        setEditor({ ...editor, record: receipt, content: receipt.content, dirty: false })
        const actual = await certificatesApi.read(route.vessel, editor.id, c.signal)
        if (mounted.current && liveOwner.current === owner) setEditor({ ...editor, record: actual, content: actual.content, dirty: false })
      } else {
        const receipt = await certificatesApi.saveRenewal(command, fresh.csrfToken, c.signal)
        if (!mounted.current || liveOwner.current !== owner) { uncertain = true; return }
        confirmed = true; setFrozen(null); onDirty(false); setSaved(true)
        setEditor({ ...editor, record: receipt, content: receipt.content, dirty: false })
        const actual = await certificatesApi.renewal(route.vessel, editor.id, c.signal)
        if (mounted.current && liveOwner.current === owner) setEditor({ ...editor, record: actual, content: actual.content, dirty: false })
      }
      if (mounted.current && liveOwner.current === owner) retry(n => n + 1)
    } catch (error) {
      if (!mounted.current) return
      const problem = error instanceof ApiError ? error : new ApiError(0)
      uncertain = !confirmed && (wasUnknown || problem.status === 0 || problem.status >= 500)
      setFrozen(uncertain ? { owner, command, uncertain: true } : null); setFailure(problem)
      if ([401, 403, 404].includes(problem.status)) { setSource({ scope, failed: true }); retry(n => n + 1) }
    } finally {
      clearTimeout(timer); running.current = false; writer.current = null
      if (mounted.current) { setBusy(false); if (uncertain) { setFrozen({ owner, command, uncertain: true }); setFailure(new ApiError(0, 'outcome_unknown')) } onSaving(uncertain) }
    }
  }
  const readCurrent = () => onNavigate(certificateLocationPath({ page: route.page, vessel: route.vessel, id: editor?.record?.id ?? route.id, offset: 0 }))
  return <div className="product-shell enterprise-shell certificate-workspace">
    <EnterpriseChrome session={user ?? null} vessel={route.vessel} language={language} onLanguageChange={onLanguage} onNavigate={onNavigate} title={title} mainId="certificate-main" />
    <main id="certificate-main" tabIndex={-1}>
      <EnterprisePageHeader eyebrow={en ? 'CERTIFICATES AND ASSURANCE' : 'SERTİFİKALAR VE GÜVENCE'} title={title} description={en ? 'Review declared calendar dates, source lineage and unresolved authority separately.' : 'Beyan edilen takvim tarihlerini, kaynak bağını ve açık yetki durumunu ayrı inceleyin.'} actionsLabel={en ? 'Page actions' : 'Sayfa işlemleri'} actions={editor
        ? <Button variant="outline" disabled={busy || !!frozen} onClick={() => onNavigate(base)}>{en ? 'Back to records' : 'Kayıtlara dön'}</Button>
        : current?.caps?.canWriteVessel && owner ? <Button onClick={() => { if (owner) setEditor({ kind: 'certificate', owner, id: crypto.randomUUID(), content: emptyVesselCertificate(), dirty: false }) }}>{en ? 'New certificate draft' : 'Yeni sertifika taslağı'}</Button> : undefined} />
      {route.invalid ? <p role="alert">{en ? 'Invalid certificate context.' : 'Sertifika bağlamı geçersiz.'}</p>
        : !online || !visible ? <p role="status">{en ? 'Current certificate sources unavailable offline or hidden. Local edits remain only in this open page; no offline grant is inferred.' : 'Çevrimdışı veya gizliyken güncel sertifika kaynakları kullanılamıyor. Yerel düzenlemeler yalnız açık sayfada korunur; çevrimdışı yetki türetilmez.'}</p>
        : !current ? <p role="status">{en ? 'Loading authorized certificate sources…' : 'Yetkili sertifika kaynakları yükleniyor…'}</p>
        : current.failed ? <div role="alert"><p>{en ? 'Certificate source or access unavailable. No valid, empty or verified inventory is inferred.' : 'Sertifika kaynağı veya erişimi kullanılamıyor. Geçerli, boş veya doğrulanmış envanter varsayılmaz.'}</p><Button variant="outline" onClick={() => retry(n => n + 1)}>{en ? 'Retry source read' : 'Kaynak okumayı tekrar dene'}</Button></div>
        : !route.vessel ? <section aria-label={en ? 'Select certificate vessel' : 'Sertifika aracını seç'}><h2>{en ? 'Select a vessel' : 'Araç seçin'}</h2>
          {current.vessels?.length ? <ul>{current.vessels.map(vessel => <li key={vessel.id}><Button variant="outline" onClick={() => onNavigate(certificateLocationPath({ ...route, vessel: vessel.id, offset: 0 }))}>{vessel.code} · {vessel.name}</Button></li>)}</ul> : <p>{en ? 'No authorized vessels returned.' : 'Yetkili araç dönmedi.'}</p>}
          <EnterprisePagination label={en ? 'Certificate vessel pages' : 'Sertifika araç sayfaları'} previous={en ? 'Previous vessels' : 'Önceki araçlar'} next={en ? 'More vessels' : 'Diğer araçlar'} previousDisabled={!route.offset} nextDisabled={!current.hasMore} onPrevious={() => onNavigate(certificateLocationPath({ ...route, offset: Math.max(0, route.offset - 25) }))} onNext={() => onNavigate(certificateLocationPath({ ...route, offset: route.offset + 25 }))} />
        </section>
        : ownerChanged ? <p role="alert">{en ? 'Retained inputs belong to another verified identity. No values or uncertain commands transfer; only the original identity can resume.' : 'Korunan girdiler başka bir doğrulanmış kimliğe ait. Değerler veya belirsiz komut aktarılmaz; yalnız özgün kimlik sürdürebilir.'}</p>
        : editor ? <>
          <CertificateEditorView editor={editor} source={current} vessel={route.vessel} language={language} editable={editable} busy={busy} frozen={!!frozen} failure={failure} conflict={conflict} denied={denied} saved={saved} onCertificate={changeCertificate} onRenewal={changeRenewal} onSave={() => void save()} onCancel={readCurrent} onNavigate={onNavigate} />
          {route.id && <CertificateWorkflowHistory source={current} language={language} offset={historyOffset} disabled={busy || !!frozen || editor.dirty} onOffset={setHistoryOffset} />}
        </>
        : route.page === 'center' && current.center && current.caps ? <CertificateCenterView page={current.center} caps={current.caps} route={route} language={language} onNavigate={onNavigate} />
        : route.page === 'renewal' && current.renewalPage ? <section aria-label={en ? 'Renewal records' : 'Yenileme kayıtları'}>
          <h2>{en ? 'Recorded renewal drafts' : 'Kayıtlı yenileme taslakları'}</h2>
          <Button variant="outline" onClick={() => onNavigate(certificateLocationPath({ page: 'center', vessel: route.vessel, id: null, offset: 0 }))}>{en ? 'Choose certificate for renewal' : 'Yenileme için sertifika seç'}</Button>
          {!current.renewalPage.items.length && <p>{en ? 'No authorized renewal rows on this page.' : 'Bu sayfada yetkili yenileme satırı yok.'}</p>}
          <ul className="certificate-record-list">{current.renewalPage.items.map(row => <li key={row.id}><Button variant="outline" onClick={() => onNavigate(certificateLocationPath({ page: 'renewal', vessel: route.vessel, id: row.id, offset: 0 }))}>{en ? 'Renewal record' : 'Yenileme kaydı'} · r{row.revision} · {row.savedAt}</Button><p>{en ? 'Authority review remains required.' : 'Yetki incelemesi gerekliliği sürüyor.'}</p></li>)}</ul>
          <EnterprisePagination label={en ? 'Renewal record pages' : 'Yenileme kayıt sayfaları'} previous={en ? 'Previous renewals' : 'Önceki yenilemeler'} next={en ? 'More renewals' : 'Diğer yenilemeler'} previousDisabled={!route.offset} nextDisabled={!current.renewalPage.hasMore} onPrevious={() => onNavigate(certificateLocationPath({ ...route, offset: Math.max(0, route.offset - 25) }))} onNext={() => onNavigate(certificateLocationPath({ ...route, offset: route.offset + 25 }))} />
        </section> : <p role="alert">{en ? 'This context cannot open a mutable draft with current permissions.' : 'Bu bağlam güncel yetkilerle düzenlenebilir taslak açamıyor.'}</p>}
      {!current && online && !route.invalid && <Button variant="outline" onClick={() => retry(n => n + 1)}>{en ? 'Retry source read' : 'Kaynak okumayı tekrar dene'}</Button>}
      <p className="certificate-field-hint">{en ? 'Unsupported offline capture, approval and issuer verification remain unavailable.' : 'Desteklenmeyen çevrimdışı kayıt, onay ve düzenleyen doğrulaması kullanılamaz durumda kalır.'}</p>
    </main>
  </div>
}
