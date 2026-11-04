import { useCallback, useEffect, useRef, useState } from 'react'
import { Button } from '../components/ui/button'
import { Badge } from '../components/ui/badge'
import * as api from './api'
import { EnterpriseChrome } from './EnterpriseChrome'
import { EnterpriseActions, EnterprisePageHeader, EnterprisePagination, EnterpriseToolbar } from './EnterprisePatterns'
import { DraftConfirmation } from './DraftConfirmation'
import { isGuid } from './identifiers'
import { useWorkspaceRevalidation } from './use-workspace-revalidation'
import * as notifications from './notification-inbox-api'
import './offline-enterprise.css'
import './product.css'

export function NotificationWorkspace() {
  const [language, setLanguage] = useState<'en' | 'tr'>('tr'), en = language === 'en', epoch = useWorkspaceRevalidation()
  const [selected, setSelected] = useState(() => {
    const query = new URLSearchParams(location.search), ids = query.getAll('vessel')
    return ids.length > 1 || [...query.keys()].some(k => k !== 'vessel') || ids.length === 1 && !isGuid(ids[0]) ? null : ids[0] ?? ''
  })
  const [identity, setIdentity] = useState<{ epoch: number; user?: api.Session; failed?: boolean } | null>(null)
  const [retainedUser, setRetainedUser] = useState<api.Session | null>(null)
  const [vessels, setVessels] = useState<{ epoch: number; page?: api.Page<api.Vessel>; failed?: boolean } | null>(null)
  const dirty = useRef(false)
  const setDirty = useCallback((value: boolean) => { dirty.current = value }, [])
  const [saving, setSaving] = useState(false), [pending, setPending] = useState<(() => void) | null>(null)
  const invalid = selected === null
  const online = navigator.onLine, visible = document.visibilityState !== 'hidden', lastUser = retainedUser
  const currentUser = identity?.epoch === epoch ? identity.user : null
  const authorized = selected !== null && online && visible && !!currentUser?.authenticated && currentUser.canRead && isGuid(currentUser.actorId) && isGuid(currentUser.tenantId) && currentUser.actorId === lastUser?.actorId && currentUser.tenantId === lastUser?.tenantId
  const guard = (action: () => void) => { if (saving) return; if (dirty.current) setPending(() => action); else action() }
  useEffect(() => {
    const warn = (event: BeforeUnloadEvent) => { if (dirty.current || saving) event.preventDefault() }
    window.addEventListener('beforeunload', warn); return () => window.removeEventListener('beforeunload', warn)
  }, [saving])
  useEffect(() => {
    if (invalid || !online || !visible) return
    const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 10000)
    api.session(c.signal).then(async user => {
      if (!active) return
      setIdentity({ epoch, user })
      if (user.authenticated && user.canRead) {
        setRetainedUser(user)
        try { const page = await api.vessels(0, '', c.signal); if (active) setVessels({ epoch, page }) }
        catch { if (active) setVessels({ epoch, failed: true }) }
      }
    }).catch(() => { if (active) setIdentity({ epoch, failed: true }) }).finally(() => clearTimeout(timer))
    return () => { active = false; clearTimeout(timer); c.abort() }
  }, [epoch, online, visible, invalid])
  const title = en ? 'Notifications' : 'Bildirimler'
  useEffect(() => { document.title = `POSEIDON · ${title}`; document.documentElement.lang = language }, [title, language])
  return <div className="product-shell enterprise-shell"><EnterpriseChrome session={authorized ? lastUser : null} vessel={selected || null} language={language} onLanguageChange={setLanguage} onNavigate={(href, committed) => guard(() => { committed?.(); location.assign(href) })} title={title} mainId="notification-main" />
    <main id="notification-main" tabIndex={-1}><EnterprisePageHeader title={title} description={en ? 'In-app configured source exceptions. Acknowledgement does not verify evidence or approve operations.' : 'Uygulama içi yapılandırılmış kaynak istisnaları. Alındı bildirimi kanıtı doğrulamaz veya operasyonu onaylamaz.'} />
      {selected === null ? <p role="alert">{en ? 'Notification context URL is invalid.' : 'Bildirim bağlamı adresi geçersiz.'}</p> : <>
        <EnterpriseToolbar><label>{en ? 'Notification vessel' : 'Bildirim aracı'}<select value={selected} disabled={saving || !authorized} onChange={event => { const value = event.target.value; guard(() => { setDirty(false); setSelected(value); history.replaceState(null, '', `/app/notification-escalation${value ? '?vessel=' + value : ''}`) }) }}><option value="">{en ? 'Select vessel' : 'Araç seçin'}</option>{selected && !vessels?.page?.items.some(v => v.id === selected) && <option value={selected}>{selected}</option>}{vessels?.epoch === epoch && vessels.page?.items.filter(v => isGuid(v.id)).map(v => <option key={v.id} value={v.id}>{v.code} · {v.name}</option>)}</select></label></EnterpriseToolbar>
        {vessels?.epoch === epoch && vessels.failed && <p role="alert">{en ? 'Vessel selection unavailable.' : 'Araç seçimi kullanılamıyor.'}</p>}
        {!online && <p role="status">{en ? 'Offline. No acknowledgement or preference command can be sent.' : 'Çevrimdışı. Alındı veya tercih komutu gönderilemez.'}</p>}
        {identity?.epoch === epoch && identity.failed && <p role="alert">{en ? 'Current session unavailable.' : 'Güncel oturum kullanılamıyor.'}</p>}
        {lastUser?.authenticated && lastUser.canRead && selected ? <NotificationInbox key={`${lastUser.tenantId}:${lastUser.actorId}:${selected}`} vessel={selected} session={lastUser} authorized={authorized} epoch={epoch} language={language} onDirty={setDirty} onSaving={setSaving} onNavigate={href => guard(() => location.assign(href))} /> : <p role="status">{en ? 'Choose an authorized vessel context.' : 'Yetkili araç bağlamı seçin.'}</p>}
      </>}
      {pending && <DraftConfirmation language={language} onStay={() => setPending(null)} onDiscard={() => { const action = pending; setDirty(false); setPending(null); action() }} />}
    </main></div>
}

export function NotificationInbox({ vessel, session, authorized, epoch, language, onDirty, onSaving, onNavigate }: { vessel: string; session: api.Session; authorized: boolean; epoch: number; language: 'en' | 'tr'; onDirty: (v: boolean) => void; onSaving: (v: boolean) => void; onNavigate: (href: string) => void }) {
  const en = language === 'en', [offset, setOffset] = useState(0), [reload, setReload] = useState(0), [change, setChange] = useState<boolean | null>(null)
  const [result, setResult] = useState<{ scope: string; inbox?: notifications.Inbox; preference?: notifications.NoticePreference; error?: number } | null>(null)
  const [attempt, setAttempt] = useState<notifications.NoticeCommand | null>(null), [busy, setBusy] = useState(false), [commandError, setCommandError] = useState<number | null>(null)
  const scope = `${epoch}:${offset}:${reload}`, current = result?.scope === scope ? result : null
  const commandController = useRef<AbortController | null>(null), alive = useRef(true), busyRef = useRef(false)
  useEffect(() => { alive.current = true; return () => { alive.current = false; commandController.current?.abort(); onDirty(false); onSaving(false) } }, [onDirty, onSaving])
  useEffect(() => { onDirty(change !== null || !!attempt); onSaving(busy || !!attempt) }, [change, attempt, busy, onDirty, onSaving])
  useEffect(() => { return () => commandController.current?.abort() }, [authorized, epoch])
  useEffect(() => {
    if (!authorized) return
    const c = new AbortController(); let active = true; const timer = setTimeout(() => c.abort(), 10000)
    notifications.loadInbox(vessel, offset, c.signal).then(async inbox => {
      const preference = inbox.canWritePreference ? await notifications.loadPreference(vessel, c.signal) : undefined
      if (active) setResult({ scope, inbox, preference })
    }).catch(error => { if (active) setResult({ scope, error: error instanceof api.ApiError ? error.status : 0 }) }).finally(() => clearTimeout(timer))
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [authorized, vessel, offset, reload, epoch, scope])
  async function send(command: notifications.NoticeCommand) {
    if (!authorized || busyRef.current || !session.csrfToken || !current?.inbox || (command.kind === 'preference' ? !current.inbox.canWritePreference : !current.inbox.canAcknowledge)) return
    busyRef.current = true; setAttempt(command); setBusy(true); setCommandError(null)
    const c = new AbortController(); commandController.current = c; const timer = setTimeout(() => c.abort(), 15000)
    try { await notifications.sendNoticeCommand(vessel, command, session.csrfToken, c.signal); if (alive.current) { setAttempt(null); setChange(null); setReload(n => n + 1) } }
    catch (error) { if (alive.current) { const code = error instanceof api.ApiError ? error.status : 0; setCommandError(code); if ([400, 401, 403, 404, 409].includes(code)) setAttempt(null); if ([401, 403, 404].includes(code)) setResult({ scope, error: code }) } }
    finally { clearTimeout(timer); busyRef.current = false; if (alive.current) setBusy(false) }
  }
  const locked = !authorized || busy || !!attempt || commandError === 409 || !session.csrfToken
  const inbox = current?.inbox, preference = current?.preference
  const severity = { Info: en ? 'Info' : 'Bilgilendirme', Warning: en ? 'Warning' : 'Uyarı', Critical: en ? 'Critical' : 'Kritik' }
  const state = { Open: en ? 'Open' : 'Açık', Acknowledged: en ? 'Acknowledged' : 'Alındı bildirildi', Escalated: en ? 'Escalated' : 'Üst role iletildi', SourceChanged: en ? 'Source changed' : 'Kaynak değişti' }
  if (!authorized) return <p role="status">{en ? 'Current authorization is required. Local notification edits or an uncertain command are retained in this tab.' : 'Güncel yetkilendirme gerekli. Yerel bildirim düzenlemeleri veya belirsiz komut bu sekmede korunuyor.'}</p>
  return <section aria-label={en ? 'Notification inbox' : 'Bildirim kutusu'}>
    {attempt && !busy && <p role="status">{en ? 'The original notification command is retained in this tab. Context changes remain locked until its outcome is checked.' : 'Özgün bildirim komutu bu sekmede korunuyor. Sonucu denetlenene kadar bağlam değişikliği kilitli.'}</p>}
    {commandError !== null && <p role="alert">{commandError === 409 ? (en ? 'Notification version changed. Reload before a new command.' : 'Bildirim sürümü değişti. Yeni komuttan önce yeniden yükleyin.') : attempt ? (en ? 'Outcome unknown. Retry the exact retained command; no automatic retry is performed.' : 'Sonuç bilinmiyor. Korunan aynı komutu yineleyin; otomatik tekrar yapılmaz.') : (en ? 'Command was not accepted.' : 'Komut kabul edilmedi.')}</p>}
    <EnterpriseActions align="start" aria-label={en ? 'Inbox recovery actions' : 'Bildirim kutusu kurtarma işlemleri'}><Button variant="outline" disabled={busy} onClick={() => { setCommandError(null); setReload(n => n + 1) }}>{en ? 'Reload inbox' : 'Bildirim kutusunu yenile'}</Button>{attempt && <Button disabled={busy || (attempt.kind === 'preference' ? !current?.inbox?.canWritePreference : !current?.inbox?.canAcknowledge)} onClick={() => void send(attempt)}>{en ? 'Retry exact notification command' : 'Aynı bildirim komutunu yinele'}</Button>}</EnterpriseActions>
    {!current ? <p role="status">{en ? 'Loading notification sources…' : 'Bildirim kaynakları yükleniyor…'}</p> : current.error !== undefined ? <p role="alert">{en ? 'Notification sources unavailable or access denied. No empty or healthy state is inferred.' : 'Bildirim kaynakları kullanılamıyor veya erişim reddedildi. Boş veya sağlıklı durum varsayılmadı.'}</p> : inbox && <>
      <p role="status">{inbox.policyState === 'Unavailable' ? (en ? 'Notification policy unavailable. An empty page does not mean all clear.' : 'Bildirim politikası kullanılamıyor. Boş sayfa her şeyin uygun olduğunu belirtmez.') : (en ? 'Configured · unverified source policy · in-app delivery only' : 'Yapılandırılmış · doğrulanmamış kaynak politikası · yalnız uygulama içi iletim')}</p>
      <p>{en ? 'Mandatory and critical routes cannot be muted by advisory preferences.' : 'Zorunlu ve kritik yollar bilgilendirme tercihleriyle susturulamaz.'}</p>
      {inbox.canWritePreference && preference && <div className="notification-preference"><label><input type="checkbox" checked={change ?? preference.mutedAdvisory} disabled={locked} onChange={event => setChange(event.target.checked)} />{en ? 'Mute nonmandatory advisory notifications' : 'Zorunlu olmayan bilgilendirme bildirimlerini sustur'}</label><EnterpriseActions aria-label={en ? 'Notification preference actions' : 'Bildirim tercihi işlemleri'}><Button variant="outline" disabled={locked || change === null} onClick={() => setChange(null)}>{en ? 'Discard preference edit' : 'Tercih düzenlemesini bırak'}</Button><Button disabled={locked || change === null} onClick={() => void send({ kind: 'preference', id: null, key: crypto.randomUUID(), body: JSON.stringify({ expectedVersion: preference.version, mutedAdvisory: change }) })}>{en ? 'Save advisory preference' : 'Bilgilendirme tercihini kaydet'}</Button></EnterpriseActions></div>}
      {!inbox.items.length && <p>{en ? 'No returned notifications on this page.' : 'Bu sayfada dönen bildirim yok.'}</p>}
      <ul className="notification-list">{inbox.items.map(row => <li key={row.id}><header><h2>{row.ruleKey}</h2><Badge tone={row.severity === 'Critical' ? 'danger' : row.severity === 'Warning' ? 'warning' : 'info'}>{severity[row.severity]}</Badge><Badge>{state[row.state]}</Badge></header><dl><div><dt>{en ? 'Source / revision' : 'Kaynak / sürüm'}</dt><dd>{row.sourceId} / {row.sourceRevision}</dd></div><div><dt>{en ? 'Due date' : 'Son tarih'}</dt><dd>{row.dueDate ?? (en ? 'Unknown' : 'Bilinmiyor')}</dd></div><div><dt>{en ? 'Created at' : 'Oluşturma zamanı'}</dt><dd><time dateTime={row.createdAt}>{row.createdAt}</time></dd></div></dl>{row.mandatory && <p>{en ? 'Mandatory route · cannot be muted' : 'Zorunlu yol · susturulamaz'}</p>}<EnterpriseActions align="start" aria-label={en ? 'Notification actions' : 'Bildirim işlemleri'}><Button variant="outline" asChild><a href={`/app/maintenance-plan/${vessel}?id=${row.sourceId}`} onClick={event => { event.preventDefault(); onNavigate(`/app/maintenance-plan/${vessel}?id=${row.sourceId}`) }}>{en ? 'Open source plan' : 'Kaynak planı aç'}</a></Button>{inbox.canAcknowledge && <Button disabled={locked || change !== null || !['Open', 'Escalated'].includes(row.state)} onClick={() => void send({ kind: 'acknowledge', id: row.id, key: crypto.randomUUID(), body: JSON.stringify({ expectedVersion: row.version }) })}>{en ? 'Acknowledge' : 'Alındı bildir'}</Button>}</EnterpriseActions></li>)}</ul>
      <EnterprisePagination label={en ? 'Notification pages' : 'Bildirim sayfaları'} previous={en ? 'Previous notifications' : 'Önceki bildirimler'} next={en ? 'More notifications' : 'Diğer bildirimler'} previousDisabled={locked || change !== null || !offset} nextDisabled={locked || change !== null || !inbox.hasMore || offset >= 100000} onPrevious={() => setOffset(n => Math.max(0, n - 25))} onNext={() => setOffset(n => n + 25)} />
    </>}
  </section>
}
