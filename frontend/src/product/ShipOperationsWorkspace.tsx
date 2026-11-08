import { useCallback, useEffect, useLayoutEffect, useRef, useState } from 'react'
import { Button } from '../components/ui/button'
import { EnterpriseActions, EnterprisePageHeader, EnterprisePagination } from './EnterprisePatterns'
import { EnterpriseChrome } from './EnterpriseChrome'
import { DraftConfirmation } from './DraftConfirmation'
import { ApiError, session, vessel as readVessel, vessels, type Session, type Vessel } from './api'
import { isGuid } from './identifiers'
import { emptyShipOpsFields, mutableShipOpsFields, shipOpsTitle, type ShipOpsFields } from './ship-operation-fields'
import { freezeShipOpsCommand, shipOperationsApi, shipOpsFieldErrors, type ShipOpsAttempt, type ShipOpsCapabilities, type ShipOpsHistory, type ShipOpsPage, type ShipOpsRecord, type ShipOperationOriginPin } from './ship-operations-api'
import { readShipOpsOrigin, type ShipOpsOriginContext } from './ship-operation-origin'
import { readShipOpsLocation, shipOpsLocationPath, useShipOpsLocation, type ShipOpsLocation } from './ship-operation-location'
import { ShipOperationsFields } from './ShipOperationsFields'
import { ShipOperationOriginView } from './ShipOperationOriginView'
import { ShipOpsRecordHistory, ShipOpsRecordList, ShipOpsRecordSummary } from './ShipOperationsRecordView'
import { useWorkspaceRevalidation } from './use-workspace-revalidation'
type Editor = { owner: string; id: string; fields: ShipOpsFields; record: ShipOpsRecord | null; origin: ShipOperationOriginPin | null; dirty: boolean }
type Source = { scope: string; user?: Session; vessel?: Vessel; vessels?: Vessel[]; hasMore?: boolean; caps?: ShipOpsCapabilities; page?: ShipOpsPage; record?: ShipOpsRecord; history?: ShipOpsHistory; origin?: ShipOpsOriginContext; failed?: boolean }
const identity = (user?: Session) => user?.authenticated && user.canRead && isGuid(user.actorId) && isGuid(user.tenantId) ? user.tenantId + '/' + user.actorId : null
const mutable = (record: ShipOpsRecord): ShipOpsFields => Object.fromEntries(mutableShipOpsFields(record.kind).map(field => [field.key, structuredClone(record.fields[field.key])]))
export function ShipOperationsWorkspace() {
  const [language, setLanguage] = useState<'en' | 'tr'>('tr'), [pending, setPending] = useState<(() => void) | null>(null), [instance, setInstance] = useState(0)
  const dirty = useRef(false), saving = useRef(false)
  const onDirty = useCallback((value: boolean) => { dirty.current = value }, []), onSaving = useCallback((value: boolean) => { saving.current = value }, [])
  const clear = () => { dirty.current = false; setInstance(n => n + 1) }
  const guard = (action: () => void) => { if (saving.current) return; if (dirty.current) setPending(() => action); else action() }
  const { route, move } = useShipOpsLocation(guard, clear)
  const navigate = (href: string, committed?: () => void) => {
    if (href !== '/app' && !href.startsWith('/app/') || /[\\\u0000-\u001f]/u.test(href)) return
    const next = readShipOpsLocation(href)
    guard(() => { committed?.(); clear(); if (next.invalid) window.location.assign(href); else move(next) })
  }
  const canonical = (id: string) => { dirty.current = false; saving.current = false; move({ kind: route.kind, vessel: route.vessel, id, offset: 0 }, true) }
  useEffect(() => { const unload = (event: BeforeUnloadEvent) => { if (dirty.current || saving.current) event.preventDefault() }; window.addEventListener('beforeunload', unload); return () => window.removeEventListener('beforeunload', unload) }, [])
  return <><ShipOpsSurface key={route.kind + '/' + route.vessel + '/' + instance} route={route} language={language} onLanguage={setLanguage} onNavigate={navigate} onCanonical={canonical} onDirty={onDirty} onSaving={onSaving} />{pending && <DraftConfirmation language={language} onStay={() => setPending(null)} onDiscard={() => { const action = pending; setPending(null); clear(); action() }} />}</>
}
function ShipOpsSurface({ route, language, onLanguage, onNavigate, onCanonical, onDirty, onSaving }: { route: ShipOpsLocation; language: 'en' | 'tr'; onLanguage: (value: 'en' | 'tr') => void; onNavigate: (href: string, committed?: () => void) => void; onCanonical: (id: string) => void; onDirty: (value: boolean) => void; onSaving: (value: boolean) => void }) {
  const en = language === 'en', epoch = useWorkspaceRevalidation(), [attempt, retry] = useState(0), [historyOffset, setHistoryOffset] = useState(0)
  const [draftId] = useState(() => crypto.randomUUID())
  const [source, setSource] = useState<Source | null>(null), [editor, setEditor] = useState<Editor | null>(null), [frozen, setFrozen] = useState<ShipOpsAttempt | null>(null)
  const [busy, setBusy] = useState(false), [failure, setFailure] = useState<ApiError | null>(null), [fieldErrors, setFieldErrors] = useState<Record<string, string[]>>({}), [receipt, setReceipt] = useState<{ owner: string; value: ShipOpsRecord } | null>(null)
  const mounted = useRef(false), running = useRef(false), writer = useRef<AbortController | null>(null), committed = useRef({ owner: null as string | null, scope: '' })
  const online = navigator.onLine, visible = document.visibilityState !== 'hidden', scope = shipOpsLocationPath(route) + '/' + epoch + '/' + attempt + '/' + historyOffset
  const current = source?.scope === scope && online && visible ? source : null, user = current?.user, owner = identity(user)
  const title = shipOpsTitle(route.kind, language), sameOwner = !editor || editor.owner === owner, sameAttempt = !frozen || frozen.owner === owner
  const sourceChanged = !!editor?.record && !!current?.record && (editor.record.version !== current.record.version || editor.record.revision !== current.record.revision)
  const conflict = !frozen && (failure?.status === 409 || sourceChanged), denied = !!failure && [401, 403, 404].includes(failure.status)
  const originBlocked = !!editor && !editor.record && !!route.origin && (!current?.origin || !current.origin.current || current.origin.pin.version !== editor.origin?.version || current.origin.pin.hash !== editor.origin?.hash)
  const headEditable = !editor?.record || editor.record.currentRevision && editor.record.canEdit
  const editable = !!owner && sameOwner && sameAttempt && !!current?.caps?.canWrite && headEditable && !busy && !frozen && !conflict && !denied
  const base = shipOpsLocationPath({ kind: route.kind, vessel: route.vessel, id: null, offset: 0 })
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
      if (!route.vessel) { const page = await vessels(route.offset, 'Ship', c.signal); await ensureOwner(); if (active) setSource({ scope, user, vessels: page.items, hasMore: page.hasMore }); return }
      const [caps, vessel] = await Promise.all([shipOperationsApi.capabilities(route.kind, route.vessel, c.signal), readVessel(route.vessel, c.signal)])
      if (!caps.canRead || vessel.id !== route.vessel || vessel.kind !== 'Ship') throw new ApiError(403)
      if (route.id) {
        const [record, history] = await Promise.all([shipOperationsApi.read(route.kind, route.vessel, route.id, c.signal), shipOperationsApi.history(route.kind, route.vessel, route.id, historyOffset, c.signal)])
        const origin = record.origin ? await readShipOpsOrigin(route.vessel, route.kind, { kind: record.origin.kind, id: record.origin.id, revision: record.origin.revision }, c.signal, record.origin) : undefined
        await ensureOwner()
        if (active) { setSource({ scope, user, vessel, caps, record, history, origin }); setEditor(previous => !previous ? { owner: original, id: record.id, fields: mutable(record), record, origin: record.origin, dirty: false } : previous.owner === original && previous.id === record.id && !previous.dirty && previous.record && !previous.record.currentRevision ? { ...previous, record, fields: mutable(record), origin: record.origin } : previous) }
      } else if (route.origin) {
        const origin = await readShipOpsOrigin(route.vessel, route.kind, route.origin, c.signal)
        await ensureOwner()
        if (active) { setSource({ scope, user, vessel, caps, origin }); if (caps.canWrite) setEditor(previous => previous ?? { owner: original, id: draftId, fields: emptyShipOpsFields(route.kind), record: null, origin: origin.pin, dirty: false }) }
      } else {
        const page = await shipOperationsApi.list(route.kind, route.vessel, route.offset, c.signal)
        await ensureOwner(); if (active) setSource({ scope, user, vessel, caps, page })
      }
      if (active) setFailure(previous => previous && [401, 403, 404].includes(previous.status) ? null : previous)
    }
    load().catch(() => { if (active) setSource({ scope, failed: true }) }).finally(() => clearTimeout(timer))
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [route, scope, route.vessel, historyOffset, online, visible, draftId])
  const change = (fields: ShipOpsFields) => { if (!editable || !editor) return; setEditor({ ...editor, fields, dirty: true }); onDirty(true); setFieldErrors({}); setFailure(null); setReceipt(null) }
  async function save() {
    if (!editor || !route.vessel || !owner || !sameOwner || !sameAttempt || !current?.caps?.canWrite || running.current || busy || denied || conflict || !frozen && (!editable || originBlocked)) return
    let command: ShipOpsAttempt
    try {
      if (frozen) command = frozen
      else { const errors = shipOpsFieldErrors(editor.fields, route.kind); if (Object.keys(errors).length) { setFieldErrors(errors); setFailure(new ApiError(400, 'invalid_draft_input', errors)); return } command = freezeShipOpsCommand(route.kind, route.vessel, editor.id, owner, editor.fields, editor.record, editor.origin) }
    } catch (error) { setFailure(error instanceof ApiError ? error : new ApiError(400)); return }
    const wasUnknown = !!frozen, originalScope = scope
    running.current = true; setBusy(true); onSaving(true); setFrozen(command); setFailure(null); setReceipt(null)
    const c = new AbortController(); writer.current = c; const timer = setTimeout(() => c.abort(), 20000)
    let sent = false, receiptReceived = false, confirmed = false, uncertain = wasUnknown
    try {
      const fresh = await session(c.signal)
      if (identity(fresh) !== command.owner) throw new ApiError(403, 'identity_changed')
      sent = true
      const outcome = await shipOperationsApi.save(command, fresh.csrfToken, c.signal); receiptReceived = true
      if (identity(await session(c.signal)) !== command.owner) throw new ApiError(403, 'identity_changed')
      if (!mounted.current || committed.current.scope !== originalScope || committed.current.owner !== command.owner) { uncertain = true; return }
      confirmed = true; uncertain = false; setFrozen(null); onDirty(false); setReceipt({ owner: command.owner, value: outcome })
      setEditor({ ...editor, record: outcome, fields: mutable(outcome), origin: outcome.origin, dirty: false })
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
  const readCurrent = () => onNavigate(shipOpsLocationPath({ kind: route.kind, vessel: route.vessel, id: editor?.record?.id ?? route.id, offset: 0 }))
  return <div className="product-shell enterprise-shell ship-operations-workspace">
    <EnterpriseChrome session={user ?? null} vessel={route.vessel} language={language} onLanguageChange={onLanguage} onNavigate={onNavigate} title={title} mainId="ship-operations-main" />
    <main id="ship-operations-main" tabIndex={-1}>
      <EnterprisePageHeader eyebrow={en ? 'SHIP OPERATIONS' : 'GEMİ OPERASYONLARI'} title={title} description={current?.vessel ? current.vessel.code + ' · ' + current.vessel.name : (en ? 'Scoped draft declarations and source history' : 'Kapsamlı taslak beyanlar ve kaynak geçmişi')} actionsLabel={en ? 'Page actions' : 'Sayfa işlemleri'} actions={editor ? <Button variant="outline" disabled={busy || !!frozen} onClick={() => onNavigate(base)}>{en ? 'Back to records' : 'Kayıtlara dön'}</Button> : route.vessel && current?.caps?.canWrite && owner ? <Button onClick={() => { setEditor({ owner, id: draftId, fields: emptyShipOpsFields(route.kind), record: null, origin: null, dirty: false }) }}>{en ? 'New draft record' : 'Yeni taslak kaydı'}</Button> : undefined} />
      {route.invalid ? <p role="alert">{en ? 'Invalid operational context. Source values or hidden grants cannot be supplied in the URL.' : 'Operasyon bağlamı geçersiz. URL ile kaynak değeri veya gizli yetki verilemez.'}</p>
        : !online || !visible ? <p role="status">{en ? 'Current operational sources unavailable offline or hidden. Local inputs remain only in this open page; no offline authority or plaintext storage is inferred.' : 'Çevrimdışı veya gizliyken güncel operasyon kaynakları kullanılamıyor. Yerel girdiler yalnız bu açık sayfada kalır; çevrimdışı yetki veya düz metin depolama varsayılmaz.'}</p>
        : !current ? <p role="status">{en ? 'Loading authorized operational sources…' : 'Yetkili operasyon kaynakları yükleniyor…'}</p>
        : current.failed ? <div role="alert"><p>{en ? 'Operational source or access unavailable. No healthy, complete or empty inventory is inferred.' : 'Operasyon kaynağı veya erişimi kullanılamıyor. Sağlıklı, tam ya da boş envanter varsayılmaz.'}</p><Button variant="outline" onClick={() => retry(n => n + 1)}>{en ? 'Retry source read' : 'Kaynak okumayı tekrar dene'}</Button></div>
        : !route.vessel ? <section aria-label={en ? 'Select operational vessel' : 'Operasyon aracını seç'}><h2>{en ? 'Select a ship' : 'Gemi seçin'}</h2><ul className="ship-operations-vessels">{current.vessels?.map(vessel => <li key={vessel.id}><Button variant="outline" onClick={() => onNavigate(shipOpsLocationPath({ ...route, vessel: vessel.id, offset: 0 }))}>{vessel.code} · {vessel.name}</Button></li>)}</ul>{!current.vessels?.length && <p>{en ? 'No authorized ships returned.' : 'Yetkili gemi dönmedi.'}</p>}<EnterprisePagination label={en ? 'Operational vessel pages' : 'Operasyon araç sayfaları'} previous={en ? 'Previous ships' : 'Önceki gemiler'} next={en ? 'More ships' : 'Diğer gemiler'} previousDisabled={!route.offset} nextDisabled={!current.hasMore} onPrevious={() => onNavigate(shipOpsLocationPath({ ...route, offset: Math.max(0, route.offset - 25) }))} onNext={() => onNavigate(shipOpsLocationPath({ ...route, offset: route.offset + 25 }))} /></section>
        : !sameOwner || !sameAttempt ? <p role="alert">{en ? 'Retained fields and commands belong to another verified identity. Only the original identity can resume; no private inputs transfer.' : 'Korunan alanlar ve komutlar başka bir doğrulanmış kimliğe ait. Yalnız özgün kimlik sürdürebilir; özel girdiler aktarılmaz.'}</p>
        : editor && current.caps ? <section aria-label={en ? 'Operational draft editor' : 'Operasyon taslak düzenleyici'}>
          {receipt && receipt.owner === owner && <p role="status">{en ? 'Command outcome recorded for revision ' : 'Komut sonucu şu sürüm için kaydedildi: '}{receipt.value.revision}{en ? '. Current source and editing authority are checked separately.' : '. Güncel kaynak ve düzenleme yetkisi ayrıca kontrol edilir.'}</p>}
          {editor.record && <ShipOpsRecordSummary value={sourceChanged ? { ...editor.record, currentRevision: false, canEdit: false } : editor.record} language={language} contextOnly />}
          {(editor.origin || route.origin) && <ShipOperationOriginView value={current.origin} unavailable={!current.origin} language={language} onNavigate={onNavigate} />}
          {originBlocked && !frozen && <p role="alert">{en ? 'A new command cannot bind this historical or unavailable daily revision. Open the source or explicitly start an independent draft; no automatic rebase is performed.' : 'Yeni komut geçmiş veya kullanılamayan günlük sürüme bağlanamaz. Kaynağı açın veya açıkça bağımsız taslak başlatın; otomatik temel değişimi yapılmaz.'}</p>}
          {originBlocked && !frozen && <Button variant="outline" onClick={() => onNavigate(base)}>{en ? 'Start an independent draft' : 'Bağımsız taslak başlat'}</Button>}
          {(failure || conflict) && <p role="alert">{frozen ? (en ? 'Outcome unknown. Keep the original identity, key, body and base version; explicitly retry after authorization.' : 'Sonuç bilinmiyor. Özgün kimlik, anahtar, içerik ve temel sürümü koruyun; yetkilendirme sonrası açıkça tekrar deneyin.') : conflict ? (en ? 'The saved source changed. Local inputs and their original version remain retained; explicitly reload or discard before another save.' : 'Kayıtlı kaynak değişti. Yerel girdiler ve özgün sürüm korunur; başka kayıt öncesi açıkça yenileyin veya bırakın.') : (en ? 'Check the declared values, explicit time offsets and current source permission.' : 'Beyan değerlerini, açık saat farklarını ve güncel kaynak yetkisini kontrol edin.')}</p>}
          {!editable && !frozen && <p>{en ? 'Read-only source snapshot or current write permission unavailable.' : 'Salt okunur kaynak anlık kaydı veya güncel yazma yetkisi kullanılamıyor.'}</p>}
          <form noValidate aria-label={route.kind === 'Fuel' ? (en ? 'Fuel draft form' : 'Yakıt taslak formu') : (en ? 'Port activity draft form' : 'Liman faaliyeti taslak formu')} aria-busy={busy} onSubmit={event => { event.preventDefault(); void save() }}>
            <ShipOperationsFields kind={route.kind} fields={editor.fields} schema={current.caps.schema} language={language} disabled={!editable} errors={{ ...failure?.errors, ...fieldErrors }} onChange={change} />
            <EnterpriseActions layout="form" aria-label={en ? 'Operational form actions' : 'Operasyon form işlemleri'}><Button variant="outline" disabled={busy || !!frozen} onClick={readCurrent}>{en ? 'Cancel editing' : 'Düzenlemeyi iptal et'}</Button>{current.caps.canWrite && <Button type="submit" disabled={busy || denied || conflict || !frozen && (!editable || originBlocked)}>{frozen ? (en ? 'Retry exact operational command' : 'Aynı operasyon komutunu tekrar dene') : busy ? (en ? 'Saving…' : 'Kaydediliyor…') : (en ? 'Save unverified draft' : 'Doğrulanmamış taslağı kaydet')}</Button>}</EnterpriseActions>
          </form>
          {editor.record && <EnterpriseActions align="start" aria-label={en ? 'Source read actions' : 'Kaynak okuma işlemleri'}><Button variant="outline" disabled={busy || !!frozen} onClick={readCurrent}>{en ? 'Read current saved record' : 'Güncel kayıtlı kaydı oku'}</Button></EnterpriseActions>}
          {current.history && <ShipOpsRecordHistory value={current.history} language={language} offset={historyOffset} disabled={busy || !!frozen || editor.dirty} onOffset={setHistoryOffset} />}
        </section>
        : current.page ? <ShipOpsRecordList kind={route.kind} page={current.page} language={language} offset={route.offset} onOffset={offset => onNavigate(shipOpsLocationPath({ ...route, offset }))} onOpen={id => onNavigate(shipOpsLocationPath({ kind: route.kind, vessel: route.vessel, id, offset: 0 }))} />
        : <p role="alert">{en ? 'This context cannot open an authorized operational draft.' : 'Bu bağlam yetkili operasyon taslağı açamıyor.'}</p>}
      <p className="ship-operations-hint">{en ? 'Human review, operational/legal approval, finalization and offline capture remain unavailable.' : 'İnsan incelemesi, operasyon/hukuk onayı, sonlandırma ve çevrimdışı kayıt kullanılamıyor.'}</p>
    </main>
  </div>
}
