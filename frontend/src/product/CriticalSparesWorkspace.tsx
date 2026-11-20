import { localizedCopy } from '../i18n/messages'
import { useAppLanguage } from '../i18n'
import { useEffect, useId, useRef, useState } from 'react'
import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { EnterpriseChrome } from './EnterpriseChrome'
import { EnterpriseActions, EnterprisePageHeader, EnterprisePagination, EnterpriseToolbar } from './EnterprisePatterns'
import * as api from './api'
import { isGuid } from './identifiers'
import { decodeTodayVessels } from './today-api'
import { criticalities, criticalSpareApi, freezeDecision, freezeProposal, freezeSpare, quantity, rejectReasons,
  type CriticalSpare, type CriticalSparePage, type RejectReason, type Requisition, type RequisitionPage, type SpareDefinition, type SupplyCommand } from './critical-spares-api'
import './product.css'

// DEV-110 / T-SUP2-001 critical-spares + technical-requisition screens. The server projects stock,
// decides low stock and computes the proposal; approval is a different human and only creates an
// ERP export reference. No vendor, price, purchase order or receipt exists in this boundary.
export const sparesCopy = localizedCopy('CriticalSpares_tsx_copy')
export type SparesCopy = (typeof sparesCopy)['en']
export type SparesRoute = { vessel: string | null; tab: 'spares' | 'requisitions'; invalid: boolean }
export function readSparesLocation(pathname: string, search: string): SparesRoute {
  const match = /^\/app\/critical-spares(?:\/([0-9a-f-]{36}))?\/?$/.exec(pathname), params = new URLSearchParams(search), tab = params.get('tab')
  if (!match) return { vessel: null, tab: 'spares', invalid: true }
  const vessel = match[1] ?? null
  return { vessel, tab: tab === 'requisitions' ? 'requisitions' : 'spares', invalid: !!vessel && !isGuid(vessel) || tab !== null && tab !== 'requisitions' || [...params.keys()].some(k => k !== 'tab') }
}
const href = (vessel: string | null, tab: SparesRoute['tab']) => `/app/critical-spares${vessel ? '/' + vessel : ''}${tab === 'requisitions' ? '?tab=requisitions' : ''}`
const stockTone = (state: string) => state === 'LOW_STOCK' ? 'danger' : 'warning'
const identity = (user?: api.Session | null) => user?.authenticated && isGuid(user.tenantId) && isGuid(user.actorId) ? user.tenantId + '/' + user.actorId : null
function useOnline() {
  const [online, setOnline] = useState(() => navigator.onLine)
  useEffect(() => {
    const update = () => setOnline(navigator.onLine)
    window.addEventListener('online', update); window.addEventListener('offline', update)
    return () => { window.removeEventListener('online', update); window.removeEventListener('offline', update) }
  }, [])
  return online
}
// One frozen attempt per command; an uncertain outcome keeps its key/body for a same-key retry.
function useCommand(t: SparesCopy, onDone: () => void) {
  const [pending, setPending] = useState<SupplyCommand | null>(null), [busy, setBusy] = useState(false), [failure, setFailure] = useState<api.ApiError | null>(null)
  const owner = useRef<string | null>(null)
  const run = async (make: () => SupplyCommand) => {
    if (busy) return
    const c = new AbortController(); const timer = setTimeout(() => c.abort(), 20000); setBusy(true); setFailure(null); let attempt = pending, sent = false
    try {
      const user = await api.session(c.signal); const who = identity(user)
      if (!who || owner.current && owner.current !== who) throw new api.ApiError(403, 'identity_changed')
      owner.current = who; attempt ??= make(); setPending(attempt); sent = true
      await criticalSpareApi.send(attempt, user.csrfToken, c.signal)
      setPending(null); onDone()
    } catch (error) {
      const problem = error instanceof api.ApiError ? error : new api.ApiError(0); setFailure(problem)
      if (!sent || problem.status > 0 && problem.status < 500) setPending(null)
    } finally { clearTimeout(timer); setBusy(false) }
  }
  const message = failure ? (failure.status === 409 ? t.conflict : [401, 403].includes(failure.status) ? t.denied : failure.status === 422 ? t.rejected : failure.status >= 500 || failure.status === 0 ? t.uncertain : t.invalid) : null
  return { run, pending, busy, failure, message, clear: () => { setFailure(null); setPending(null) } }
}

export function CriticalSparesWorkspace() {
  const [language, setLanguage] = useAppLanguage(), t = sparesCopy[language], online = useOnline()
  const [route, setRoute] = useState<SparesRoute>(() => readSparesLocation(window.location.pathname, window.location.search))
  const [user, setUser] = useState<api.Session | null | undefined>(undefined), [attempt, retry] = useState(0)
  useEffect(() => { document.title = `POSEIDON · ${t.title}` }, [t.title])
  useEffect(() => {
    if (!online || route.invalid) return
    const c = new AbortController(); let active = true
    api.session(c.signal).then(value => { if (active) setUser(value) }).catch(() => { if (active) setUser(null) })
    return () => { active = false; c.abort() }
  }, [online, route.invalid, attempt])
  useEffect(() => { const pop = () => setRoute(readSparesLocation(window.location.pathname, window.location.search)); window.addEventListener('popstate', pop); return () => window.removeEventListener('popstate', pop) }, [])
  const go = (target: string) => { history.pushState(null, '', target); const [p, q = ''] = target.split('?'); setRoute(readSparesLocation(p, q ? '?' + q : '')) }
  return <div className="product-shell enterprise-shell"><EnterpriseChrome session={user ?? null} vessel={route.vessel} language={language} onLanguageChange={setLanguage}
      onNavigate={(target, committed) => { committed?.(); window.location.assign(target) }} title={t.title} mainId="critical-spares-main"/>
    <main id="critical-spares-main" tabIndex={-1} data-testid="critical-spares">
      <EnterprisePageHeader title={t.title} description={t.description} actions={<Button variant="outline" disabled={!online} onClick={() => retry(n => n + 1)}>{t.retry}</Button>}/>
      {!online && <p role="status" data-state="offline">{t.offline}</p>}
      {route.invalid ? <p role="alert">{t.notFound}</p> : user === undefined ? (online ? <p role="status">{t.loading}</p> : null) : user === null ? <p role="alert">{t.loadError}</p>
        : !user.authenticated || !user.canRead ? <p role="alert">{t.denied}</p> : <>
          <VesselPicker vessel={route.vessel} online={online} t={t} onSelect={vessel => go(href(vessel, route.tab))}/>
          {route.vessel ? <>
            <EnterpriseActions align="start" aria-label={t.tabs}>
              <Button variant={route.tab === 'spares' ? 'default' : 'outline'} aria-pressed={route.tab === 'spares'} onClick={() => go(href(route.vessel, 'spares'))}>{t.sparesTab}</Button>
              <Button variant={route.tab === 'requisitions' ? 'default' : 'outline'} aria-pressed={route.tab === 'requisitions'} onClick={() => go(href(route.vessel, 'requisitions'))}>{t.requisitionsTab}</Button>
            </EnterpriseActions>
            <p className="work-order-hint">{t.erpBoundary}</p>
            {route.tab === 'spares' ? <SparesView key={`${user.actorId}:${route.vessel}:${attempt}`} vessel={route.vessel} online={online} t={t}/>
              : <RequisitionsView key={`${user.actorId}:${route.vessel}:${attempt}`} vessel={route.vessel} online={online} t={t}/>}
          </> : <p role="status">{t.chooseVessel}</p>}
        </>}
    </main></div>
}

function VesselPicker({ vessel, online, t, onSelect }: { vessel: string | null; online: boolean; t: SparesCopy; onSelect: (id: string) => void }) {
  const [rows, setRows] = useState<api.Vessel[] | null>(null), id = useId()
  useEffect(() => {
    if (!online) return
    const c = new AbortController(); let active = true
    api.vessels(0, '', c.signal).then(v => decodeTodayVessels(v, 0)).then(v => { if (active) setRows(v.items) }).catch(() => { if (active) setRows([]) })
    return () => { active = false; c.abort() }
  }, [online])
  return <EnterpriseToolbar><label htmlFor={id}>{t.vessel}</label><select id={id} value={vessel ?? ''} disabled={!online} onChange={e => { if (e.target.value) onSelect(e.target.value) }}>
    <option value="">{t.chooseVessel}</option>
    {vessel && !rows?.some(r => r.id.toLowerCase() === vessel) && <option value={vessel}>{vessel}</option>}
    {rows?.map(r => <option key={r.id} value={r.id.toLowerCase()}>{r.code} · {r.name}</option>)}
  </select></EnterpriseToolbar>
}

const emptyDefinition = (): SpareDefinition => ({ partNumber: '', name: '', unit: '', criticality: 'UNKNOWN', assetId: null, minQuantity: null, maxQuantity: null, location: null })
export function SparesView({ vessel, online, t }: { vessel: string; online: boolean; t: SparesCopy }) {
  const [offset, setOffset] = useState(0), [reload, setReload] = useState(0), [page, setPage] = useState<{ data?: CriticalSparePage; error?: number } | null>(null)
  const [editing, setEditing] = useState<CriticalSpare | 'new' | null>(null)
  const command = useCommand(t, () => { setEditing(null); setReload(n => n + 1) })
  useEffect(() => {
    if (!online) return
    const c = new AbortController(); let active = true
    criticalSpareApi.list(vessel, offset, c.signal).then(data => { if (active) setPage({ data }) }).catch(e => { if (active) setPage({ error: e instanceof api.ApiError ? e.status : 0 }) })
    return () => { active = false; c.abort() }
  }, [vessel, offset, reload, online])
  if (!page) return <p role="status" aria-live="polite">{t.loading}</p>
  if (!page.data) return <div role="alert"><p>{page.error === 403 ? t.denied : page.error === 404 ? t.notFound : t.loadError}</p><Button variant="outline" disabled={!online} onClick={() => setReload(n => n + 1)}>{t.retry}</Button></div>
  const data = page.data
  return <section aria-label={t.sparesTab} className="critical-spares">
    <p><Badge tone={data.lowStock ? 'danger' : 'neutral'} data-alert="low-stock">{t.lowStock}: {data.lowStock}</Badge> <Badge tone="warning">{t.reviewRequired}: {data.reviewRequired}</Badge></p>
    <p className="work-order-hint">{t.qualification}</p>
    {command.message && <p role="alert">{command.message}{command.failure?.code ? <> <code>{command.failure.code}</code></> : null}</p>}
    {data.canWrite && !editing && <Button disabled={!online} onClick={() => setEditing('new')}>{t.newSpare}</Button>}
    {editing && <SpareForm record={editing === 'new' ? null : editing} t={t} busy={command.busy || !online} retrying={!!command.pending} onCancel={() => { command.clear(); setEditing(null) }}
      onSave={(definition, count) => void command.run(() => freezeSpare(vessel, editing === 'new' ? null : editing, definition, count))}/>}
    {data.items.length === 0 ? <p role="status">{t.empty}</p> : <div className="conflict-center__table" tabIndex={0}><table>
      <caption className="sr-only">{t.sparesTab}</caption>
      <thead><tr><th scope="col">{t.part}</th><th scope="col">{t.criticality}</th><th scope="col">{t.equipmentCriticality}</th><th scope="col">{t.stock}</th><th scope="col">{t.projected}</th><th scope="col">{t.minMax}</th><th scope="col">{t.demand}</th><th scope="col">{t.action}</th></tr></thead>
      <tbody>{data.items.map(s => <tr key={s.id} data-stock={s.stock.state}>
        <th scope="row">{s.definition.partNumber} · {s.definition.name}</th>
        <td>{t[`crit${s.definition.criticality}`]}</td>
        <td>{s.equipmentCriticality === 'UNKNOWN' ? t.unknown : s.equipmentCriticality}</td>
        <td><Badge tone={stockTone(s.stock.state)} data-state={s.stock.state}>{t[`stock${s.stock.state}`]}</Badge></td>
        <td>{s.stock.projected ?? '—'} {s.definition.unit}</td>
        <td>{s.definition.minQuantity ?? t.unconfigured} / {s.definition.maxQuantity ?? t.unconfigured}</td>
        <td>{s.openWorkOrderDemand}</td>
        <td><EnterpriseActions align="start" aria-label={t.action}>
          {s.canEdit && <Button size="sm" variant="outline" disabled={!online || !!editing} onClick={() => setEditing(s)}>{t.edit}</Button>}
          {data.canPropose && s.stock.state === 'LOW_STOCK' && !s.openRequisitionId && <Button size="sm" disabled={!online || command.busy} onClick={() => void command.run(() => freezeProposal(vessel, s))}>{t.propose}</Button>}
          {s.openRequisitionId && <span>{t.openRequisition}</span>}
        </EnterpriseActions></td>
      </tr>)}</tbody>
    </table></div>}
    <EnterprisePagination label={t.pages} previous={t.previous} next={t.next} previousDisabled={!offset} nextDisabled={!data.hasMore} onPrevious={() => setOffset(n => Math.max(0, n - 25))} onNext={() => setOffset(n => n + 25)}/>
  </section>
}

export function SpareForm({ record, t, busy, retrying, onSave, onCancel }: { record: CriticalSpare | null; t: SparesCopy; busy: boolean; retrying: boolean
  onSave: (definition: SpareDefinition, count: { quantity: string; countedAt: string } | null) => void; onCancel: () => void }) {
  const [definition, setDefinition] = useState<SpareDefinition>(() => record ? { ...record.definition } : emptyDefinition())
  const [count, setCount] = useState(record?.count?.quantity ?? ''), [recount, setRecount] = useState(!record?.count)
  const set = (patch: Partial<SpareDefinition>) => setDefinition(d => ({ ...d, ...patch }))
  const number = (v: string) => v === '' || quantity(v)
  const valid = definition.partNumber.trim() && definition.name.trim() && definition.unit.trim() && number(definition.minQuantity ?? '') && number(definition.maxQuantity ?? '') && (!recount || number(count)) &&
    !(definition.minQuantity && definition.maxQuantity && Number(definition.maxQuantity) < Number(definition.minQuantity))
  const submit = () => onSave(definition, recount ? (count ? { quantity: count, countedAt: new Date().toISOString() } : null) : record?.count ?? null)
  return <form className="critical-spares__form" aria-label={record ? t.edit : t.newSpare} onSubmit={e => { e.preventDefault(); if (valid) submit() }}>
    <label>{t.partNumber}<input value={definition.partNumber} maxLength={64} required onChange={e => set({ partNumber: e.target.value })}/></label>
    <label>{t.name}<input value={definition.name} maxLength={200} required onChange={e => set({ name: e.target.value })}/></label>
    <label>{t.unit}<input value={definition.unit} maxLength={32} required onChange={e => set({ unit: e.target.value })}/></label>
    <label>{t.criticality}<select value={definition.criticality} onChange={e => set({ criticality: e.target.value as SpareDefinition['criticality'] })}>
      {criticalities.map(c => <option key={c} value={c}>{t[`crit${c}`]}</option>)}</select></label>
    <label>{t.minimum}<input value={definition.minQuantity ?? ''} inputMode="decimal" aria-invalid={!number(definition.minQuantity ?? '')} onChange={e => set({ minQuantity: e.target.value || null })}/></label>
    <label>{t.maximum}<input value={definition.maxQuantity ?? ''} inputMode="decimal" aria-invalid={!number(definition.maxQuantity ?? '')} onChange={e => set({ maxQuantity: e.target.value || null })}/></label>
    <p className="work-order-hint">{t.thresholdHint}</p>
    {record && <label><input type="checkbox" checked={recount} onChange={e => setRecount(e.target.checked)}/>{t.recordCount}</label>}
    {recount && <label>{t.countedQuantity}<input value={count} inputMode="decimal" aria-invalid={!number(count)} onChange={e => setCount(e.target.value)}/></label>}
    <p className="work-order-hint">{t.countHint}</p>
    <EnterpriseActions aria-label={t.save}>
      <Button type="button" variant="outline" disabled={busy && !retrying} onClick={onCancel}>{t.cancel}</Button>
      <Button type="submit" disabled={busy || !valid}>{retrying ? t.retrySame : t.save}</Button>
    </EnterpriseActions>
  </form>
}

export function RequisitionsView({ vessel, online, t }: { vessel: string; online: boolean; t: SparesCopy }) {
  const [offset, setOffset] = useState(0), [reload, setReload] = useState(0), [page, setPage] = useState<{ data?: RequisitionPage; error?: number } | null>(null)
  const [inputs, setInputs] = useState<Record<string, { quantity: string; reason: RejectReason | '' }>>({})
  const command = useCommand(t, () => setReload(n => n + 1))
  useEffect(() => {
    if (!online) return
    const c = new AbortController(); let active = true
    criticalSpareApi.requisitions(vessel, offset, c.signal).then(data => { if (active) setPage({ data }) }).catch(e => { if (active) setPage({ error: e instanceof api.ApiError ? e.status : 0 }) })
    return () => { active = false; c.abort() }
  }, [vessel, offset, reload, online])
  if (!page) return <p role="status" aria-live="polite">{t.loading}</p>
  if (!page.data) return <div role="alert"><p>{page.error === 403 ? t.denied : t.loadError}</p><Button variant="outline" disabled={!online} onClick={() => setReload(n => n + 1)}>{t.retry}</Button></div>
  const input = (r: Requisition) => inputs[r.id] ?? { quantity: r.proposedQuantity ?? '', reason: '' }
  return <section aria-label={t.requisitionsTab} className="technical-requisitions">
    {command.message && <p role="alert">{command.message}{command.failure?.code ? <> <code>{command.failure.code}</code></> : null}</p>}
    {page.data.items.length === 0 ? <p role="status">{t.noRequisitions}</p> : <ul className="technical-requisitions__list">{page.data.items.map(r => <li key={r.id} data-state={r.state}>
      <h3>{r.partNumber} · {r.name}</h3>
      <p><Badge tone={r.state === 'REJECTED' ? 'neutral' : r.state === 'PROPOSED' ? 'warning' : 'info'}>{t[`req${r.state}`]}</Badge> {t.proposedQuantity}: {r.proposedQuantity ?? t.quantityReview} {r.unit}
        {r.approvedQuantity && <> · {t.approvedQuantity}: {r.approvedQuantity} {r.unit}</>}</p>
      <p className="work-order-hint">{t.basis}: {t[`crit${r.spareCriticality}`]} · {t.equipmentCriticality}: {r.equipmentCriticality === 'UNKNOWN' ? t.unknown : r.equipmentCriticality} · {t.demand}: {r.openWorkOrderDemand}</p>
      {r.proposedByCurrentActor && <p className="work-order-hint">{t.youProposed}</p>}
      {r.reasonCode && <p>{t.rejectReason}: {t[`reason${r.reasonCode}`]}</p>}
      {r.erpReference && <details><summary>{t.erpReference}</summary><p><code>{r.erpReference.kind}</code> · <code>{r.erpReference.referenceId}</code> · {r.erpReference.quantity} {r.erpReference.unit}</p><p>{t.erpBoundary}</p></details>}
      {r.canDecide && !r.proposedByCurrentActor && <EnterpriseActions align="start" aria-label={t.decide}>
        <label>{t.approvedQuantity}<input value={input(r).quantity} inputMode="decimal" aria-invalid={!!input(r).quantity && !quantity(input(r).quantity)}
          onChange={e => setInputs(x => ({ ...x, [r.id]: { ...input(r), quantity: e.target.value } }))}/></label>
        <Button disabled={!online || command.busy || !quantity(input(r).quantity)} onClick={() => void command.run(() => freezeDecision(vessel, r, 'APPROVE', input(r).quantity === r.proposedQuantity ? null : input(r).quantity, null))}>{t.approve}</Button>
        <label>{t.rejectReason}<select value={input(r).reason} onChange={e => setInputs(x => ({ ...x, [r.id]: { ...input(r), reason: e.target.value as RejectReason | '' } }))}><option value="">—</option>
          {rejectReasons.map(reason => <option key={reason} value={reason}>{t[`reason${reason}`]}</option>)}</select></label>
        <Button variant="outline" disabled={!online || command.busy || !input(r).reason} onClick={() => void command.run(() => freezeDecision(vessel, r, 'REJECT', null, input(r).reason as RejectReason))}>{t.reject}</Button>
      </EnterpriseActions>}
    </li>)}</ul>}
    <EnterprisePagination label={t.pages} previous={t.previous} next={t.next} previousDisabled={!offset} nextDisabled={!page.data.hasMore} onPrevious={() => setOffset(n => Math.max(0, n - 25))} onNext={() => setOffset(n => n + 25)}/>
  </section>
}
