import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { Button } from '../components/ui/button'
import { EnterpriseActions, EnterprisePageHeader, EnterprisePagination } from './EnterprisePatterns'
import { EnterpriseChrome } from './EnterpriseChrome'
import { DraftConfirmation } from './DraftConfirmation'
import { ApiError, session, vessel as readVessel, vessels, type Session, type Vessel } from './api'
import { isGuid } from './identifiers'
import { emptyWorkOrderFields, type WorkOrderFields } from './work-order-fields'
import { workOrderApi, workOrderInputErrors, freezeWorkOrderCommand, type WorkOrderAttempt, type WorkOrderPlanContext, type WorkOrderPlanPin, type WorkOrderCapabilities, type WorkOrderHistory, type WorkOrderPage, type WorkOrderRecord } from './work-order-api'
import { readWorkOrderLocation, workOrderLocationPath, useWorkOrderLocation, type WorkOrderLocation } from './work-order-location'
import { WorkOrderFieldsForm } from './WorkOrderFieldsForm'
import { WorkOrderPlanAside, WorkOrderRecordHistory, WorkOrderRecordList, WorkOrderSourceSummary } from './WorkOrderRecordView'
import { useWorkspaceRevalidation } from './use-workspace-revalidation'
type Editor = { owner: string; id: string; fields: WorkOrderFields; record: WorkOrderRecord | null; plan: WorkOrderPlanPin | null; dirty: boolean }
type Source = { scope: string; user?: Session; vessel?: Vessel; vessels?: Vessel[]; hasMore?: boolean; caps?: WorkOrderCapabilities; page?: WorkOrderPage; record?: WorkOrderRecord; history?: WorkOrderHistory; planContext?: WorkOrderPlanContext; planIssue?: 'stale' | 'unavailable'; failed?: boolean }
const identity = (user?: Session) => user?.authenticated && user.canRead && isGuid(user.actorId) && isGuid(user.tenantId) ? user.tenantId + '/' + user.actorId : null
const mutable = (record: WorkOrderRecord): WorkOrderFields => structuredClone(record.fields)
export function WorkOrderWorkspace() {
  const [language, setLanguage] = useState<'en' | 'tr'>('tr'), [pending, setPending] = useState<(() => void) | null>(null), [instance, setInstance] = useState(0)
  const dirty = useRef(false), saving = useRef(false)
  const onDirty = useCallback((value: boolean) => { dirty.current = value }, []), onSaving = useCallback((value: boolean) => { saving.current = value }, [])
  const clear = () => { dirty.current = false; setInstance(n => n + 1) }
  const guard = (action: () => void) => { if (saving.current) return; if (dirty.current) setPending(() => action); else action() }
  const { route, move } = useWorkOrderLocation(guard, clear)
  const navigate = (href: string, committed?: () => void) => {
    if (href !== '/app' && !href.startsWith('/app/') || /[\\\u0000-\u001f]/u.test(href)) return
    const next = readWorkOrderLocation(href)
    guard(() => { committed?.(); clear(); if (next.invalid) window.location.assign(href); else move(next) })
  }
  const canonical = (id: string) => { dirty.current = false; saving.current = false; move({ vessel: route.vessel, id, offset: 0 }, true) }
  useEffect(() => { const unload = (event: BeforeUnloadEvent) => { if (dirty.current || saving.current) event.preventDefault() }; window.addEventListener('beforeunload', unload); return () => window.removeEventListener('beforeunload', unload) }, [])
  return <><WorkOrderSurface key={route.vessel + '/' + instance} route={route} language={language} onLanguage={setLanguage} onNavigate={navigate} onCanonical={canonical} onDirty={onDirty} onSaving={onSaving} />{pending && <DraftConfirmation language={language} onStay={() => setPending(null)} onDiscard={() => { const action = pending; setPending(null); clear(); action() }} />}</>
}
function WorkOrderSurface({ route, language, onLanguage, onNavigate, onCanonical, onDirty, onSaving }: { route: WorkOrderLocation; language: 'en' | 'tr'; onLanguage: (value: 'en' | 'tr') => void; onNavigate: (href: string, committed?: () => void) => void; onCanonical: (id: string) => void; onDirty: (value: boolean) => void; onSaving: (value: boolean) => void }) {
  const en = language === 'en', epoch = useWorkspaceRevalidation(), [attempt, retry] = useState(0), [historyOffset, setHistoryOffset] = useState(0)
  const [draftId] = useState(() => crypto.randomUUID())
  const [source, setSource] = useState<Source | null>(null), [editor, setEditor] = useState<Editor | null>(null), [frozen, setFrozen] = useState<WorkOrderAttempt | null>(null)
  const [busy, setBusy] = useState(false), [failure, setFailure] = useState<ApiError | null>(null), [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({}), [receipt, setReceipt] = useState<{ owner: string; value: WorkOrderRecord } | null>(null)
  const mounted = useRef(false), running = useRef(false), writer = useRef<AbortController | null>(null), committed = useRef({ owner: null as string | null, scope: '' })
  const online = navigator.onLine, visible = document.visibilityState !== 'hidden', scope = workOrderLocationPath(route) + '/' + epoch + '/' + attempt + '/' + historyOffset
  const current = source?.scope === scope && online && visible ? source : null, user = current?.user, owner = identity(user)
  const title = (en ? 'Work order center' : 'İş emri merkezi'), sameOwner = !editor || editor.owner === owner, sameAttempt = !frozen || frozen.owner === owner
  const sourceChanged = !!editor?.record && !!current?.record && (editor.record.version !== current.record.version || editor.record.revision !== current.record.revision)
  const conflict = !frozen && (failure?.status === 409 || sourceChanged), denied = !!failure && [401, 403, 404].includes(failure.status)
  const planBlocked = !!editor && !editor.record && !!route.plan && (!editor.plan || !current?.planContext || current.planContext.pin.version !== editor.plan.version || current.planContext.pin.hash !== editor.plan.hash)
  const headEditable = !editor?.record || editor.record.currentRevision && editor.record.canEdit
  const editable = !!owner && sameOwner && sameAttempt && !!current?.caps?.canWrite && headEditable && !busy && !frozen && !conflict && !denied
  const base = workOrderLocationPath({ vessel: route.vessel, id: null, offset: 0 })
  useLayoutEffect(() => { committed.current = { owner, scope }; if (!online || !visible) writer.current?.abort() }, [owner, scope, online, visible])
  useEffect(() => { mounted.current = true; return () => { mounted.current = false; writer.current?.abort() } }, [])
  useEffect(() => { document.documentElement.lang = language; document.title = 'POSEIDON · ' + title }, [language, title])
  useEffect(() => {
    if (!online || !visible || route.invalid) return
    const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 10000)
    async function load() {
      const user = await session(c.signal), original = identity(user)
      if (!original) throw new ApiError(403)
      const ensureOwner = async () => { if (identity(await session(c.signal)) !== original) throw new ApiError(403, 'identity_changed') }
      if (!route.vessel) { const page = await vessels(route.offset, '', c.signal); await ensureOwner(); if (active) setSource({ scope, user, vessels: page.items, hasMore: page.hasMore }); return }
      const [caps, vessel] = await Promise.all([workOrderApi.capabilities(route.vessel, c.signal), readVessel(route.vessel, c.signal)])
      if (!caps.canRead || vessel.id !== route.vessel) throw new ApiError(403)
      async function context(pin: { id: string; revision: number }, expected?: WorkOrderPlanPin): Promise<{ planContext?: WorkOrderPlanContext; planIssue?: 'stale' | 'unavailable' }> {
        try { return { planContext: await workOrderApi.planContext(vessel.id, pin.id, pin.revision, c.signal, expected) } }
        catch (error) { if (error instanceof ApiError && [401, 403].includes(error.status)) throw error; return { planIssue: error instanceof ApiError && error.status === 409 ? 'stale' : 'unavailable' } }
      }
      if (route.id) {
        const [record, history] = await Promise.all([workOrderApi.read(route.vessel, route.id, c.signal), workOrderApi.history(route.vessel, route.id, historyOffset, c.signal)])
        const plan = record.plan ? await context(record.plan, record.plan) : {}
        await ensureOwner()
        if (active) { setSource({ scope, user, vessel, caps, record, history, ...plan }); setEditor(previous => !previous ? { owner: original, id: record.id, fields: mutable(record), record, plan: record.plan, dirty: false } : previous.owner === original && previous.id === record.id && !previous.dirty && previous.record && !previous.record.currentRevision ? { ...previous, record, fields: mutable(record), plan: record.plan } : previous) }
      } else if (route.plan) {
        const plan = await context(route.plan)
        await ensureOwner()
        if (active) { setSource({ scope, user, vessel, caps, ...plan }); if (caps.canWrite) setEditor(previous => previous ?? { owner: original, id: draftId, fields: emptyWorkOrderFields(), record: null, plan: plan.planContext?.pin ?? null, dirty: false }) }
      } else {
        const page = await workOrderApi.list(route.vessel, route.offset, c.signal)
        await ensureOwner(); if (active) setSource({ scope, user, vessel, caps, page })
      }
      if (active) setFailure(previous => previous && [401, 403, 404].includes(previous.status) ? null : previous)
    }
    load().catch(() => { if (active) setSource({ scope, failed: true }) }).finally(() => clearTimeout(timer))
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [route, scope, route.vessel, historyOffset, online, visible, draftId])
  const change = (fields: WorkOrderFields) => { if (!editable || !editor) return; setEditor({ ...editor, fields, dirty: true }); onDirty(true); setFieldErrors({}); setFailure(null); setReceipt(null) }
  async function save() {
    if (!editor || !route.vessel || !owner || !sameOwner || !sameAttempt || !current?.caps?.canWrite || running.current || busy || denied || conflict || !frozen && (!editable || planBlocked)) return
    let command: WorkOrderAttempt
    try {
      if (frozen) command = frozen
      else { const errors = workOrderInputErrors(editor.fields); if (Object.keys(errors).length) { setFieldErrors(errors); setFailure(new ApiError(400, 'invalid_draft_input', errors)); return } command = freezeWorkOrderCommand(route.vessel, editor.id, owner, editor.fields, editor.record, editor.plan) }
    } catch (error) { setFailure(error instanceof ApiError ? error : new ApiError(400)); return }
    const wasUnknown = !!frozen, originalScope = scope
    running.current = true; setBusy(true); onSaving(true); setFrozen(command); setFailure(null); setReceipt(null)
    const c = new AbortController(); writer.current = c; const timer = setTimeout(() => c.abort(), 20000)
    let sent = false, receiptReceived = false, confirmed = false, uncertain = wasUnknown
    try {
      const fresh = await session(c.signal)
      if (identity(fresh) !== command.owner) throw new ApiError(403, 'identity_changed')
      sent = true
      const outcome = await workOrderApi.save(command, fresh.csrfToken, c.signal); receiptReceived = true
      if (identity(await session(c.signal)) !== command.owner) throw new ApiError(403, 'identity_changed')
      if (!mounted.current || committed.current.scope !== originalScope || committed.current.owner !== command.owner) { uncertain = true; return }
      confirmed = true; uncertain = false; setFrozen(null); onDirty(false); setReceipt({ owner: command.owner, value: outcome })
      setEditor({ ...editor, record: outcome, fields: mutable(outcome), plan: outcome.plan, dirty: false })
      // Persist the confirmed resource identity before optional readback. A
      // failed current GET must never reopen as a different new draft UUID.
      // The canonical source loader alone establishes fresh current/edit rights.
      onCanonical(outcome.id)
      retry(n => n + 1)
    } catch (error) {
      if (!mounted.current) return
      const problem = error instanceof ApiError ? error : new ApiError(0)
      uncertain = !confirmed && (wasUnknown || receiptReceived || sent && (problem.status === 0 || problem.status >= 500))
      setFrozen(uncertain ? command : null); setFailure(problem)
      if ([401, 403, 404].includes(problem.status)) { setSource({ scope, failed: true }); retry(n => n + 1) }
    } finally { clearTimeout(timer); writer.current = null; running.current = false; if (mounted.current) { setBusy(false); if (uncertain) { setFrozen(command); setFailure(new ApiError(0, 'outcome_unknown')) } onSaving(uncertain) } }
  }
  const readCurrent = () => onNavigate(workOrderLocationPath({ vessel: route.vessel, id: editor?.record?.id ?? route.id, offset: 0 }))
  const savedRecord = editor?.record
  const snapshot = savedRecord && (current?.record?.version === savedRecord.version ? current.record : current?.history?.items.find(record => record.version === savedRecord.version) ?? savedRecord)
  const planContext = current?.planContext && (!editor?.plan || current.planContext.pin.id === editor.plan.id && current.planContext.pin.revision === editor.plan.revision && current.planContext.pin.version === editor.plan.version && current.planContext.pin.hash === editor.plan.hash) ? current.planContext : undefined
  return <div className="product-shell enterprise-shell work-order-workspace">
    <EnterpriseChrome session={user ?? null} vessel={route.vessel} language={language} onLanguageChange={onLanguage} onNavigate={onNavigate} title={title} mainId="work-order-main" />
    <main id="work-order-main" tabIndex={-1}>
      <EnterprisePageHeader eyebrow={en ? 'MAINTENANCE EXECUTION' : 'BAKIM UYGULAMA'} title={title} description={current?.vessel ? current.vessel.code + ' · ' + current.vessel.name : (en ? 'Draft work orders and declared execution records' : 'Taslak iş emirleri ve uygulama beyanları')} actionsLabel={en ? 'Page actions' : 'Sayfa işlemleri'} actions={editor ? <Button variant="outline" disabled={busy || !!frozen} onClick={() => onNavigate(base)}>{en ? 'Back to work orders' : 'İş emirlerine dön'}</Button> : route.vessel && current?.caps?.canWrite && owner ? <Button onClick={() => setEditor({ owner, id: draftId, fields: emptyWorkOrderFields(), record: null, plan: null, dirty: false })}>{en ? 'New work order draft' : 'Yeni iş emri taslağı'}</Button> : undefined} />
      {route.invalid ? <p role="alert">{en ? 'Invalid work order context.' : 'İş emri bağlamı geçersiz.'}</p>
        : !online || !visible ? <p role="status">{en ? 'Current work order sources are unavailable offline or hidden. Your entries remain in this open page; no offline storage or authority is available.' : 'Çevrimdışı veya gizliyken güncel iş emri kaynakları kullanılamıyor. Girdiler bu açık sayfada kalır; çevrimdışı depolama veya yetki kullanılamıyor.'}</p>
        : !current ? <p role="status">{en ? 'Loading authorized work order sources…' : 'Yetkili iş emri kaynakları yükleniyor…'}</p>
        : current.failed ? <div role="alert"><p>{en ? 'Work order records or current access are unavailable. No empty inventory or safe-service state is inferred.' : 'İş emri kayıtları veya güncel erişim kullanılamıyor. Boş envanter veya güvenli hizmet durumu varsayılmaz.'}</p><Button variant="outline" onClick={() => retry(n => n + 1)}>{en ? 'Retry source read' : 'Kaynak okumayı tekrar dene'}</Button></div>
        : !route.vessel ? <section aria-label={en ? 'Select work order vessel context' : 'İş emri araç bağlamını seç'}><h2>{en ? 'Select a vessel or tug' : 'Gemi veya römorkör seçin'}</h2>{!current.vessels?.length && <p>{en ? 'No authorized vessels returned.' : 'Yetkili araç dönmedi.'}</p>}<ul className="work-order-records">{current.vessels?.map(vessel => <li key={vessel.id}><Button variant="outline" onClick={() => onNavigate(workOrderLocationPath({ vessel: vessel.id, id: null, offset: 0 }))}>{vessel.code} · {vessel.name}</Button></li>)}</ul><EnterprisePagination label={en ? 'Work order vessel pages' : 'İş emri araç sayfaları'} previous={en ? 'Previous vessels' : 'Önceki araçlar'} next={en ? 'More vessels' : 'Diğer araçlar'} previousDisabled={!route.offset} nextDisabled={!current.hasMore} onPrevious={() => onNavigate(workOrderLocationPath({ ...route, offset: Math.max(0, route.offset - 25) }))} onNext={() => onNavigate(workOrderLocationPath({ ...route, offset: route.offset + 25 }))} /></section>
        : !sameOwner || !sameAttempt ? <p role="alert">{en ? 'This retained draft belongs to another signed-in context. Only the original account can continue; private entries and saved commands do not transfer.' : 'Korunan taslak başka bir oturum bağlamına ait. Yalnız özgün hesap sürdürebilir; özel girdiler ve kayıtlı komut aktarılmaz.'}</p>
        : editor && current.caps ? <section aria-label={en ? 'Work order draft editor' : 'İş emri taslak düzenleyici'}>
          {receipt && receipt.owner === owner && <p role="status">{en ? 'Draft outcome recorded for revision ' : 'Taslak sonucu şu sürüm için kaydedildi: '}{receipt.value.revision}{en ? '. Current access is checked separately.' : '. Güncel erişim ayrıca kontrol edilir.'}</p>}
          {snapshot && <WorkOrderSourceSummary value={sourceChanged ? { ...snapshot, currentRevision: false, canEdit: false } : snapshot} language={language} />}
          {(editor.plan || route.plan) && <WorkOrderPlanAside value={planContext} retained={editor.plan} issue={current.planIssue ?? (editor.plan && current.planContext && !planContext ? 'stale' : undefined)} language={language} onNavigate={onNavigate} />}
          {planBlocked && !frozen && <div role="alert"><p>{en ? 'This maintenance plan context cannot be captured for a new draft. Read the current source or explicitly start without a linked plan.' : 'Bu bakım planı bağlamı yeni taslağa alınamıyor. Güncel kaynağı okuyun veya açıkça bakım planı bağı olmadan başlayın.'}</p>{current.planContext && <Button variant="outline" onClick={() => { if (editable && current.planContext) { setEditor({ ...editor, plan: current.planContext.pin, dirty: true }); onDirty(true) } }}>{en ? 'Use displayed current maintenance plan context' : 'Gösterilen güncel bakım planı bağını kullan'}</Button>}<Button variant="outline" onClick={() => onNavigate(base)}>{en ? 'Browse work orders without maintenance plan context' : 'Bakım planı bağı olmadan iş emirlerine dön'}</Button></div>}
          {(failure || conflict) && <p role="alert">{frozen ? (en ? 'The save outcome is unknown. Your original command is protected. Reauthorize the original account, then explicitly retry.' : 'Kayıt sonucu bilinmiyor. Özgün komut korunuyor. Özgün hesabı tekrar yetkilendirip açıkça yeniden deneyin.') : conflict ? (en ? 'The saved draft changed. Your entries and original base were kept; explicitly reload or discard before another save.' : 'Kayıtlı taslak değişti. Girdiler ve özgün temel korundu; başka kayıt öncesi açıkça yeniden okuyun veya bırakın.') : (en ? 'Check the declared values, calendar dates and current access.' : 'Taslak değerlerini, tarihleri ve güncel erişimi kontrol edin.')}</p>}
          {!editable && !frozen && <p>{en ? 'Read-only source snapshot or current write permission unavailable.' : 'Salt okunur kaynak anlık kaydı veya güncel yazma yetkisi kullanılamıyor.'}</p>}
          <form noValidate aria-label={en ? 'Work order draft form' : 'İş emri taslak formu'} aria-busy={busy} onSubmit={event => { event.preventDefault(); void save() }}>
            <EnterpriseActions layout="form" aria-label={en ? 'Work order form actions' : 'İş emri form işlemleri'}><Button variant="outline" disabled={busy || !!frozen} onClick={readCurrent}>{en ? 'Cancel editing' : 'Düzenlemeyi iptal et'}</Button>{current.caps.canWrite && <Button type="submit" disabled={busy || denied || conflict || !frozen && (!editable || planBlocked)}>{frozen ? (en ? 'Retry exact work order command' : 'Aynı iş emri komutunu tekrar dene') : busy ? (en ? 'Saving…' : 'Kaydediliyor…') : (en ? 'Save unassessed draft' : 'Değerlendirilmemiş taslağı kaydet')}</Button>}</EnterpriseActions>
            <WorkOrderFieldsForm fields={editor.fields} schema={current.caps.schema} language={language} disabled={!editable} errors={{ ...failure?.errors, ...fieldErrors }} onChange={change} />
          </form>
          {editor.record && <EnterpriseActions align="start" aria-label={en ? 'Work order source actions' : 'İş emri kaynak işlemleri'}><Button variant="outline" disabled={busy || !!frozen} onClick={readCurrent}>{en ? 'Read current draft' : 'Güncel taslağı oku'}</Button></EnterpriseActions>}
          {current.history && <WorkOrderRecordHistory value={current.history} offset={historyOffset} disabled={busy || !!frozen || editor.dirty} language={language} onOffset={setHistoryOffset} />}
        </section>
        : current.page ? <WorkOrderRecordList page={current.page} offset={route.offset} language={language} onOffset={offset => onNavigate(workOrderLocationPath({ ...route, offset }))} onOpen={id => onNavigate(workOrderLocationPath({ vessel: route.vessel, id, offset: 0 }))} />
        : <p role="alert">{en ? 'This context cannot open an authorized work order draft.' : 'Bu bağlam yetkili iş emri taslağı açamıyor.'}</p>}
    </main>
  </div>
}
