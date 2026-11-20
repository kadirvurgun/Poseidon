import { useAppLanguage, type Locale } from '../i18n'
import { useEffect, useState } from 'react'
import { Badge } from '../components/ui/badge'
import { EnterpriseChrome } from './EnterpriseChrome'
import { EnterprisePageHeader, EnterpriseToolbar } from './EnterprisePatterns'
import * as api from './api'
import { isGuid } from './identifiers'
import { decodeTodayVessels } from './today-api'
import { departureHref } from './DepartureReadinessWorkspace'
import { dailyLocationPath } from './daily-location'
import { shipOpsLocationPath } from './ship-operation-location'
import { downtimeLocationPath } from './downtime-location'
import { shipOperationsApi, type ShipOpsRecord } from './ship-operations-api'
import { s10Copy } from './CertificateRenewalOfflinePanel'
import './product.css'

// S10 WP-C DEV-120 (18.01 sheet:Backlog/row:121) ship buyer demo: Departure -> Noon/Daily -> Fuel/BDN -> SOF ->
// Downtime -> Readiness. A guided, read-only walk over the EXISTING screens; it never offers a second entry form.
// Single-entry reuse is shown from server ShipOperationOriginPin lineage (a Fuel/SOF record that pins its Noon or
// Engine origin revision). Demo records are synthetic drafts: nothing here is verified, signed or a readiness GO,
// and readiness remains the owning DEV-058 engine output plus the Master's human decision.
export const shipDemoPath = (vessel: string | null) => `/app/ship-demo${vessel ? '/' + vessel : ''}`
export function readShipDemoRoute(pathname: string, search: string): { vessel: string | null; invalid: boolean } {
  const match = /^\/app\/ship-demo(?:\/([0-9a-fA-F-]{36}))?\/?$/.exec(pathname)
  const vessel = match?.[1]?.toLowerCase() ?? null
  return { vessel: vessel && isGuid(vessel) ? vessel : null, invalid: !match || !!match[1] && !isGuid(vessel) || search !== '' }
}
type StepKey = 'departure' | 'daily' | 'fuel' | 'sof' | 'downtime' | 'readiness'
export const shipDemoSteps: readonly StepKey[] = ['departure', 'daily', 'fuel', 'sof', 'downtime', 'readiness']
export function shipDemoHref(step: StepKey, vessel: string) {
  switch (step) {
    case 'departure': case 'readiness': return departureHref(vessel)
    case 'daily': return dailyLocationPath({ kind: 'Noon', vessel, id: null, offset: 0 })
    case 'fuel': return shipOpsLocationPath({ kind: 'Fuel', vessel, id: null, offset: 0 })
    case 'sof': return shipOpsLocationPath({ kind: 'Port', vessel, id: null, offset: 0 })
    case 'downtime': return downtimeLocationPath({ vessel, id: null, offset: 0 })
  }
}
// Reuse is proven only by a server-returned origin pin; a record without one is "entered here", never inferred.
export function originReuse(records: readonly ShipOpsRecord[]) {
  return { pinned: records.filter(r => r.origin !== null).length, direct: records.filter(r => r.origin === null).length }
}
type Reuse = { state: 'loading' } | { state: 'error' } | { state: 'forbidden' } | { state: 'ready'; fuel: ReturnType<typeof originReuse>; port: ReturnType<typeof originReuse> }

function ReuseSummary({ vessel, language }: { vessel: string; language: Locale }) {
  const t = s10Copy[language].shipDemo, [reuse, setReuse] = useState<Reuse>({ state: 'loading' })
  useEffect(() => {
    const c = new AbortController(); let active = true
    Promise.all([shipOperationsApi.list('Fuel', vessel, 0, c.signal), shipOperationsApi.list('Port', vessel, 0, c.signal)])
      .then(([fuel, port]) => { if (active) setReuse({ state: 'ready', fuel: originReuse(fuel.items), port: originReuse(port.items) }) })
      .catch(error => { if (active) setReuse({ state: error instanceof api.ApiError && (error.status === 403 || error.status === 404) ? 'forbidden' : 'error' }) })
    return () => { active = false; c.abort() }
  }, [vessel])
  return <section aria-label={t.reuseHeading} className="ship-demo__reuse">
    <h2>{t.reuseHeading}</h2><p>{t.reuseHelp}</p>
    {reuse.state === 'loading' ? <p role="status">{t.loading}</p> : reuse.state === 'forbidden' ? <p role="alert">{t.forbidden}</p>
      : reuse.state === 'error' ? <p role="alert">{t.error}</p>
      : <ul>
        <li data-testid="ship-demo-reuse-fuel">{t.fuelReuse.replace('{pinned}', String(reuse.fuel.pinned)).replace('{direct}', String(reuse.fuel.direct))}</li>
        <li data-testid="ship-demo-reuse-port">{t.portReuse.replace('{pinned}', String(reuse.port.pinned)).replace('{direct}', String(reuse.port.direct))}</li>
      </ul>}
  </section>
}

export function ShipDemoJourney() {
  const [language, setLanguage] = useAppLanguage(), t = s10Copy[language].shipDemo
  const [route] = useState(() => readShipDemoRoute(window.location.pathname, window.location.search)), [vessel, setVessel] = useState(route.vessel ?? '')
  const [user, setUser] = useState<api.Session | null | 'error'>(null), [vessels, setVessels] = useState<api.Page<api.Vessel> | null | 'error'>(null)
  const [online, setOnline] = useState(() => navigator.onLine)
  useEffect(() => {
    const on = () => setOnline(true), off = () => setOnline(false)
    window.addEventListener('online', on); window.addEventListener('offline', off)
    return () => { window.removeEventListener('online', on); window.removeEventListener('offline', off) }
  }, [])
  useEffect(() => { document.title = `POSEIDON · ${t.title}` }, [t.title])
  useEffect(() => {
    if (route.invalid || !online) return
    const c = new AbortController(); let active = true
    api.session(c.signal).then(value => { if (active) setUser(value) }).catch(() => { if (active) setUser('error') })
    return () => { active = false; c.abort() }
  }, [route.invalid, online])
  const session = user && user !== 'error' ? user : null
  useEffect(() => {
    if (!session?.authenticated || !session.canRead || !online) return
    const c = new AbortController(); let active = true
    api.vessels(0, '', c.signal).then(rows => decodeTodayVessels(rows, 0)).then(rows => { if (active) setVessels(rows) }).catch(() => { if (active) setVessels('error') })
    return () => { active = false; c.abort() }
  }, [session, online])
  return <div className="product-shell enterprise-shell">
    <EnterpriseChrome session={session} vessel={vessel || null} language={language} onLanguageChange={setLanguage} onNavigate={(href, committed) => { committed?.(); window.location.assign(href) }} title={t.title} mainId="ship-demo-main"/>
    <main id="ship-demo-main" tabIndex={-1}>
      <EnterprisePageHeader title={t.title} eyebrow={t.eyebrow} description={t.description}/>
      <p><Badge tone="warning">{t.syntheticNotice}</Badge></p>
      {route.invalid ? <p role="alert">{t.invalidRoute}</p>
        : !online ? <p role="status">{t.offline}</p>
        : user === 'error' ? <p role="alert">{t.error}</p>
        : !user ? <p role="status">{t.loading}</p>
        : !user.authenticated || !user.canRead ? <p role="alert">{t.forbidden}</p>
        : <>
          <EnterpriseToolbar><label>{t.vessel}<select value={vessel} onChange={event => { const id = event.target.value; setVessel(id); history.replaceState(null, '', shipDemoPath(id || null)) }}>
            <option value="">{t.chooseVessel}</option>
            {vessel && vessels && vessels !== 'error' && !vessels.items.some(row => row.id === vessel) && <option value={vessel}>{vessel}</option>}
            {vessels && vessels !== 'error' && vessels.items.filter(row => isGuid(row.id)).map(row => <option key={row.id} value={row.id}>{row.code} · {row.name}</option>)}
          </select></label></EnterpriseToolbar>
          {vessels === 'error' && <p role="alert">{t.error}</p>}
          {vessels && vessels !== 'error' && vessels.items.length === 0 && <p role="status">{t.empty}</p>}
          {!vessel ? <p role="status">{t.selectVessel}</p> : <>
            <ol className="ship-demo__steps" aria-label={t.stepsLabel}>
              {shipDemoSteps.map((step, index) => <li key={step} data-step={step}>
                <h2>{index + 1}. {t.steps[step].title}</h2>
                <p>{t.steps[step].what}</p>
                <details><summary>{t.why}</summary><p>{t.steps[step].why}</p></details>
                <a href={shipDemoHref(step, vessel)}>{t.open}</a>
              </li>)}
            </ol>
            <ReuseSummary key={vessel} vessel={vessel} language={language}/>
          </>}
        </>}
    </main>
  </div>
}
