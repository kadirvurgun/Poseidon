import { WorkspaceRoleNavigation } from './WorkspaceRoleNavigation'
import { CrewIdentityHistory } from './CrewIdentityHistory'
import { CrewAssignmentHistory } from './CrewAssignmentHistory'
import { CrewOwningInputs } from './CrewOwningInputs'
import { OfflineApplicationPanel } from './OfflineApplicationPanel'
import { CrewPersonalFiles } from './CrewPersonalFiles'
import { CrewPersonalProfile } from './CrewPersonalProfile'
import { WorkspaceContextBar } from './WorkspaceContextBar'
import { CertificatePanel } from './CertificatePanel'
import { useEffect, useRef, useState } from 'react'
import { ApiError, session } from './api'
import type { Session } from './directory-contract'
import * as crew from './crew-api'

const words = {
  en: { title: 'Crew roster', profile: 'Crew profile', loading: 'Loading…', empty: 'No assignments in this vessel.', denied: 'Crew access denied.', error: 'Crew service unavailable.',
    save: 'Save draft', retry: 'Retry', create: 'Add person and assignment', edit: 'Edit assignment', editPerson: 'Edit person', cancel: 'Cancel', name: 'Full name', employee: 'Employee number',
    position: 'Position / rank / capacity', from: 'Sign-on date', to: 'Sign-off date (exclusive)', contract: 'Contract end date', watch: 'Watch group',
    unknown: 'Unknown', withheld: 'Withheld', dirty: 'Unsaved changes', uncertain: 'Result uncertain. Retry the same attempt before editing.',
    conflict: 'Conflict. Reload the current assignment and resolve the changes.', invalid: 'Check the period and field values.',
    offline: 'Ordinary forms require a connection. Use Durable offline drafts for explicitly configured capture.',
    prev: 'Previous', next: 'Next', roster: 'Back to roster', fleet: 'Back to vessel', unverified: 'Draft · unverified', expired: 'Session expired. Sign in again.',
    privacy: 'Identity, contact and health access are separate. Unavailable information remains Unknown.', saved: 'Draft saved.', reload: 'Discard and reload',
    eligibility: 'Eligibility', fitness: 'Fitness', certificate: 'Certificates', competence: 'Competence', contact: 'Contact', health: 'Health',
    language: 'Language', department: 'Department', rank: 'Rank', capacity: 'Capacity', period: 'Period', person: 'Person',
  },
  tr: { title: 'Mürettebat listesi', profile: 'Personel profili', loading: 'Yükleniyor…', empty: 'Bu araçta atama yok.', denied: 'Mürettebat erişimi reddedildi.', error: 'Mürettebat servisine erişilemiyor.',
    save: 'Taslağı kaydet', retry: 'Tekrar dene', create: 'Personel ve atama ekle', edit: 'Atamayı düzenle', editPerson: 'Personeli düzenle', cancel: 'Vazgeç', name: 'Ad soyad', employee: 'Personel numarası',
    position: 'Pozisyon / rütbe / kapasite', from: 'Katılış tarihi', to: 'Ayrılış tarihi (hariç)', contract: 'Sözleşme bitiş tarihi', watch: 'Vardiya grubu',
    unknown: 'Bilinmiyor', withheld: 'Gizli', dirty: 'Kaydedilmemiş değişiklikler', uncertain: 'Sonuç belirsiz. Düzenlemeden önce aynı denemeyi tekrarlayın.',
    conflict: 'Çakışma. Güncel atamayı yükleyip değişiklikleri uzlaştırın.', invalid: 'Dönemi ve alan değerlerini kontrol edin.',
    offline: 'Normal formlar bağlantı gerektirir. Açıkça yapılandırılmış kayıt için Kalıcı çevrimdışı taslaklar bölümünü kullanın.',
    prev: 'Önceki', next: 'Sonraki', roster: 'Listeye dön', fleet: 'Araca dön', unverified: 'Taslak · doğrulanmamış', expired: 'Oturum süresi doldu. Yeniden oturum açın.',
    privacy: 'Kimlik, iletişim ve sağlık erişimleri ayrıdır. Eksik bilgi Bilinmiyor olarak kalır.', saved: 'Taslak kaydedildi.', reload: 'Vazgeç ve yeniden yükle',
    eligibility: 'Uygunluk', fitness: 'Sağlık uygunluğu', certificate: 'Belgeler', competence: 'Yetkinlik', contact: 'İletişim', health: 'Sağlık',
    language: 'Dil', department: 'Bölüm', rank: 'Rütbe', capacity: 'Kapasite', period: 'Dönem', person: 'Personel',
  },
}
type Editor = { command: crew.CrewCommand; key: string }
export function CrewWorkspace({ vessel, assignment }: { vessel: string; assignment?: string }) {
  const [language, setLanguage] = useState<'tr' | 'en'>('tr'); const t = words[language]
  const [user, setUser] = useState<Session | null>(null)
  const [page, setPage] = useState<crew.CrewPage | null>(null)
  const [detail, setDetail] = useState<crew.CrewProfile | null>(null)
  const [current, setCurrent] = useState<{ state: string; vesselId?: string; assignmentId?: string; policyVersion?: string; position?: { rank: string | null; department: string | null } | null } | null>(null)
  const [positions, setPositions] = useState<crew.CrewPosition[]>([])
  const [offset, setOffset] = useState(0); const [generation, setGeneration] = useState(0)
  const [loading, setLoading] = useState(true); const [error, setError] = useState(0)
  const [certificateDirty, setCertificateDirty] = useState(false)
  const [profileDirty, setProfileDirty] = useState(false)
  const [fileDirty, setFileDirty] = useState(false)
  const [offlineDirty, setOfflineDirty] = useState(false)
  const childDirty = certificateDirty || profileDirty || fileDirty || offlineDirty
  const [personMode, setPersonMode] = useState(false)
  const [conversion, setConversion] = useState(false)
  const [positionVersion, setPositionVersion] = useState<string | null>(null)
  const [editor, setEditor] = useState<Editor | null>(null); const [saving, setSaving] = useState(false)
  const [result, setResult] = useState<'idle' | 'saved' | 'uncertain' | 'conflict' | 'invalid' | 'denied' | 'expired'>('idle')
  const [online, setOnline] = useState(navigator.onLine)
  const [destination, setDestination] = useState<string | null>(null)
  const inFlight = useRef(false)
  const knownIdentity = useRef<Session | null>(null)
  const [paused, setPaused] = useState(false)
  const dialog = useRef<HTMLDialogElement>(null)
  const rosterHref = `/app/crew-roster/${encodeURIComponent(vessel)}`
  const clear = () => { knownIdentity.current = null; setPage(null); setDetail(null); setPositions([]); setCurrent(null); setEditor(null); setUser(null) }
  useEffect(() => {
    const c = new AbortController(); let active = true
    const timer = setTimeout(() => c.abort(), 10000)
    setLoading(true); setError(0); setPage(null); setDetail(null); setPositions([]); setCurrent(null)
    session(c.signal).then(async identity => {
      if (!identity.authenticated) throw new ApiError(401)
      const value = await crew.roster(vessel, offset, c.signal)
      const record = assignment ? await crew.profile(vessel, assignment, c.signal) : null
      const currentAssignment = assignment ? await crew.currentAssignment(vessel, assignment, c.signal) : null
      const refs = value.canWrite ? await crew.positionPolicy(vessel, c.signal) : { items: [], version: null, state: 'Unavailable' }
      if (active) {
        const old = knownIdentity.current
        if (old && (old.actorId !== identity.actorId || old.tenantId !== identity.tenantId) || !value.canWrite || !value.canWriteIdentity) {
          setEditor(previous => previous && (old?.actorId !== identity.actorId || old?.tenantId !== identity.tenantId || !value.canWrite || previous.command.fullName !== null || previous.command.employeeNo !== null) ? null : previous)
        }
        knownIdentity.current = identity; setUser(identity); setPage(value); setDetail(record); setCurrent(currentAssignment); setPositions(refs.items); setPositionVersion(refs.version); setPaused(false)
      }
    }).catch(e => { if (active) { clear(); setPaused(false); setError(e instanceof ApiError ? e.status : 503) } })
      .finally(() => { if (active) setLoading(false); clearTimeout(timer) })
    return () => { active = false; c.abort(); clearTimeout(timer) }
  }, [vessel, assignment, offset, generation])
  useEffect(() => {
    const update = () => setOnline(navigator.onLine)
    const leave = (e: BeforeUnloadEvent) => { if (editor || childDirty || inFlight.current) e.preventDefault() }
    // Nothing is stored in browser storage. Hide all personal projections when the session leaves view.
    const hide = () => { if (document.visibilityState === 'hidden') { setPaused(true); setPage(null); setDetail(null); setPositions([]); setCurrent(null) }
      else { setGeneration(v => v + 1) } }
    const restore = (e: PageTransitionEvent) => { if (e.persisted) { setPaused(true); setPage(null); setDetail(null); setGeneration(v => v + 1) } }
    window.addEventListener('pageshow', restore)
    window.addEventListener('online', update); window.addEventListener('offline', update); window.addEventListener('beforeunload', leave)
    document.addEventListener('visibilitychange', hide)
    return () => { window.removeEventListener('pageshow', restore); window.removeEventListener('online', update); window.removeEventListener('offline', update); window.removeEventListener('beforeunload', leave); document.removeEventListener('visibilitychange', hide) }
  }, [editor, childDirty])
  useEffect(() => { document.documentElement.lang = language; document.title = `POSEIDON · ${assignment ? t.profile : t.title}` }, [language, assignment, t])
  useEffect(() => { if (destination && dialog.current && !dialog.current.open) dialog.current.showModal() }, [destination])
  const start = (row?: crew.CrewRow, personal = false, convert = false) => { setPersonMode(personal); setConversion(convert); setResult('idle'); setEditor({ key: crypto.randomUUID(), command: {
    id: row?.id ?? crypto.randomUUID(), personId: row?.personId ?? null, baseVersion: personal ? detail?.personVersion ?? null : row?.version ?? null,
    fullName: personal ? row?.fullName ?? null : null, employeeNo: personal ? row?.employeeNo ?? null : null, positionId: row?.positionId ?? null, from: row?.from ?? null, to: row?.to ?? null,
    contractEndDate: row?.contractEndDate ?? null, watchGroup: row?.watchGroup ?? null, sourceObservedAt: row?.sourceObservedAt ?? null, declaredStatus: null, assignmentPolicyVersion: page?.declarationPolicy?.version ?? null,
    identityPolicyVersion: (detail?.identityPolicy ?? page?.identityPolicy)?.version ?? null, identityPurpose: (detail?.identityPolicy ?? page?.identityPolicy)?.purpose ?? null, positionPolicyVersion: positionVersion,
  } }) }
  const change = (field: keyof crew.CrewCommand, value: string) => setEditor(old => old ? { ...old, command: { ...old.command, [field]: value || null } } : null)
  const save = async () => {
    if (!editor || !user || inFlight.current || !online) return
    inFlight.current = true; setSaving(true)
    const c = new AbortController(); const timer = setTimeout(() => c.abort(), 15000)
    try {
      await (conversion ? crew.convertPerson : personMode ? crew.savePerson : crew.saveCrew)(vessel, editor.command, user.csrfToken, editor.key, c.signal)
      setEditor(null); setResult('saved'); setGeneration(v => v + 1)
    } catch (e) {
      if (e instanceof ApiError && (e.status === 401 || e.status === 403)) { clear(); setResult(e.status === 401 ? 'expired' : 'denied') }
      else setResult(e instanceof ApiError && e.status === 409 ? 'conflict' : e instanceof ApiError && e.status === 400 ? 'invalid' : 'uncertain')
    } finally { clearTimeout(timer); inFlight.current = false; setSaving(false) }
  }
  const row = detail?.assignment
  const nullable = (value: string | null | undefined) => value ?? t.unknown
  const presence = (value: crew.CrewRow) => ({
    Draft: language === 'tr' ? 'Eksik taslak' : 'Incomplete draft',
    Unknown: language === 'tr' ? 'Atama bağlamı bilinmiyor' : 'Assignment context unknown',
    Stale: language === 'tr' ? 'Beyan politikası değişti: inceleme gerekli' : 'Declaration policy changed: review required',
    OutsidePeriod: language === 'tr' ? 'Güncel dönem dışında' : 'Outside current period',
    PlannedDeclaration: language === 'tr' ? 'Planlanan · doğrulanmamış beyan' : 'Planned · unverified declaration',
    OnboardDeclaration: language === 'tr' ? 'Gemide · doğrulanmamış beyan' : 'Onboard · unverified declaration',
  })[value.presenceState]
  const navigate = (href: string) => { if (saving || result === 'uncertain') return; if (editor || childDirty) setDestination(href); else window.location.assign(href) }
  return <div className="product-shell crew-shell"><a className="skip-link" href="#crew-main">{assignment ? t.profile : t.title}</a>
    <header className="product-header"><a href="/app" onClick={e => { e.preventDefault(); navigate('/app') }}>POSEIDON</a><div role="group" aria-label={t.language}><button aria-pressed={language === 'tr'} onClick={() => setLanguage('tr')}>TR</button><button aria-pressed={language === 'en'} onClick={() => setLanguage('en')}>EN</button></div><WorkspaceContextBar session={user} vessel={vessel} language={language} /></header>
    <WorkspaceRoleNavigation session={user} vessel={vessel} language={language} onNavigate={navigate} />
    <main id="crew-main" tabIndex={-1}>
      <nav><a href={`/app/fleet/${vessel}`} onClick={e => { e.preventDefault(); navigate(`/app/fleet/${vessel}`) }}>{t.fleet}</a>{assignment && <a href={rosterHref} onClick={e => { e.preventDefault(); navigate(rosterHref) }}>{t.roster}</a>}</nav>
      <h1>{assignment ? t.profile : t.title}</h1><p>{t.privacy}</p><p>{t.unverified}</p>
      {assignment && <OfflineApplicationPanel key={`offline:${vessel}:${assignment}`} vessel={vessel} assignment={assignment} language={language} onDirty={setOfflineDirty} />}
      {!online && <p role="alert">{t.offline}</p>}
      {loading && <p role="status">{t.loading}</p>}
      {error > 0 && <div role="alert"><p>{error === 401 ? t.expired : error === 403 || error === 404 ? t.denied : t.error}</p><button onClick={() => setGeneration(v => v + 1)}>{t.retry}</button>{error === 401 && <a href={`/api/v1/auth/login?returnUrl=${encodeURIComponent(window.location.pathname)}`}>{t.expired}</a>}</div>}
      {result !== 'idle' && <p role={result === 'saved' ? 'status' : 'alert'}>{t[result]}</p>}
      {page && online && !paused && !assignment && <><button disabled={!page.canWrite || !page.canWriteIdentity || !page.identityPolicy || !online || !!editor || childDirty} onClick={() => start()}>{t.create}</button>
        {page.items.length === 0 ? <p>{t.empty}</p> : <div className="product-table"><table><thead><tr><th>{t.person}</th><th>{t.rank}</th><th>{t.capacity}</th><th>{t.department}</th><th>{language === 'tr' ? 'Uyruk' : 'Nationality'}</th><th>{t.period}</th><th>{language === 'tr' ? 'Atama durumu' : 'Assignment state'}</th></tr></thead><tbody>{page.items.map(item => <tr key={item.id}><td><a href={`${rosterHref}/crew-profile/${item.id}`} onClick={e => { e.preventDefault(); navigate(`${rosterHref}/crew-profile/${item.id}`) }}>{item.fullName ?? t.withheld}</a></td><td>{nullable(item.rank)}</td><td>{nullable(item.capacity)}</td><td>{nullable(item.department)}</td><td>{item.nationality ?? (item.nationalityState === 'Withheld' ? t.withheld : t.unknown)}</td><td>{nullable(item.from)} — {nullable(item.to)}</td><td>{presence(item)}</td></tr>)}</tbody></table></div>}
        <div className="product-pagination"><button disabled={offset === 0 || !!editor || childDirty} onClick={() => setOffset(v => v - 25)}>{t.prev}</button><button disabled={!page.hasMore || !!editor || childDirty} onClick={() => setOffset(v => v + 25)}>{t.next}</button></div></>}
      {current && !paused && <section aria-label={language === 'tr' ? 'Güncel atama' : 'Current assignment'}><h2>{language === 'tr' ? 'Güncel atama' : 'Current assignment'}</h2><p>{current.state === 'ConfiguredEffectiveDeclaration' ? (language === 'tr' ? 'Yapılandırılmış dönem ve durumla eşleşen doğrulanmamış beyan' : 'Unverified declaration matching configured period and state') : current.state === 'PolicyUnavailable' ? (language === 'tr' ? 'Güncellik politikası yapılandırılmamış' : 'Effective assignment policy unavailable') : current.state === 'PolicyStale' ? (language === 'tr' ? 'Beyan politikası değişti: inceleme gerekli' : 'Declaration policy changed: review required') : current.state === 'Ambiguous' ? (language === 'tr' ? 'Birden fazla aday: inceleme gerekli' : 'Multiple candidates: review required') : (language === 'tr' ? 'Güncel atama bulunamadı' : 'No current assignment')}</p>{current.assignmentId && <><p>{current.position?.rank ?? t.unknown} · {current.position?.department ?? t.unknown}</p><a href={`/app/crew-roster/${current.vesselId}/crew-profile/${current.assignmentId}`} onClick={e => { e.preventDefault(); navigate(`/app/crew-roster/${current.vesselId}/crew-profile/${current.assignmentId}`) }}>{language === 'tr' ? 'Güncel atamayı aç' : 'Open current assignment'}</a></>}</section>}
      {page && online && !paused && row && <section><h2>{row.fullName ?? t.withheld}</h2>{detail?.statusSpecification && <p role="status">{language === 'tr' ? 'Profil durumu: kaynak tanımlı türetim şartnamesi gerekli.' : 'Profile status: source-defined derivation specification required.'}</p>}{row.privacyScope === 'LegacyUnavailable' && <p role="status">{language === 'tr' ? 'Eski kimlik verisi: kontrollü dönüşüm gerekli.' : 'Legacy identity unavailable: controlled conversion required.'}</p>}{row.privacyScope === 'Unavailable' && <p role="alert">{language === 'tr' ? 'Korumalı kimlik kullanılamıyor.' : 'Protected identity unavailable.'}</p>}<p role="status">{presence(row)}</p><p>{row.positionState === 'Stale' ? (language === 'tr' ? 'Pozisyon kataloğu değişti: güncel seçimi inceleyin.' : 'Position catalog changed: review the current selection.') : null}</p><dl><dt>{t.employee}</dt><dd>{row.employeeNo ?? t.withheld}</dd><dt>{t.position}</dt><dd>{nullable(row.rank)} / {nullable(row.capacity)}</dd><dt>{t.department}</dt><dd>{nullable(row.department)}</dd><dt>{t.period}</dt><dd>{nullable(row.from)} — {nullable(row.to)}</dd><dt>{t.eligibility}</dt><dd>{t.unknown}</dd><dt>{t.fitness}</dt><dd>{t.unknown}</dd><dt>{t.certificate}</dt><dd>{t.unknown}</dd><dt>{t.competence}</dt><dd>{t.unknown}</dd><dt>{t.contact}</dt><dd>{t.withheld}</dd><dt>{t.health}</dt><dd>{t.withheld}</dd></dl><button disabled={!page.canWrite || !!editor || childDirty || !online} onClick={() => start(row)}>{t.edit}</button>{page.canWrite && page.canWriteIdentity && detail?.personVersion && <><button disabled={!!editor || childDirty || !online || row.privacyScope !== 'IdentityPermitted'} onClick={() => start(row, true)}>{t.editPerson}</button>{row.privacyScope === 'LegacyUnavailable' && detail?.canConvertIdentity && <button disabled={!!editor || childDirty || !online} onClick={() => start(row, true, true)}>{language === 'tr' ? 'Eski kimliği korumalı depoya dönüştür' : 'Convert legacy identity to protected storage'}</button>}</>}</section>}
      {editor && !paused && <form onSubmit={e => { e.preventDefault(); void save() }}><h2>{t.dirty}</h2><fieldset disabled={saving || result === 'uncertain' || result === 'conflict'}>
        {!conversion && (personMode || !editor.command.personId) && page?.canWriteIdentity && <><label>{t.name}<input maxLength={200} value={editor.command.fullName ?? ''} onChange={e => change('fullName', e.target.value)} /></label><label>{t.employee}<input disabled={personMode && detail?.identityPolicy?.employeeNumberMode === 'ImmutableAfterCapture' && row?.employeeNo != null} maxLength={64} value={editor.command.employeeNo ?? ''} onChange={e => change('employeeNo', e.target.value)} /></label></>}
        {conversion && <p>{language === 'tr' ? 'Mevcut eski kimlik değerlerini koruyarak korumalı depoya dönüştür.' : 'Preserve existing legacy identity values and convert to protected storage.'}</p>}
        {!personMode && <>{!positionVersion && <p role="status">{language === 'tr' ? 'Kontrollü pozisyon listesi kullanılamıyor. Eksik taslakta pozisyon bilinmiyor kalabilir.' : 'Controlled position lookup unavailable. An incomplete draft may keep position unknown.'}</p>}<label>{t.position}<select value={editor.command.positionId ?? ''} onChange={e => change('positionId', e.target.value)}><option value="">{t.unknown}</option>{positions.map(p => <option value={p.id} key={p.id}>{p.code} · {nullable(p.rank)} · {nullable(p.capacity)} · {nullable(p.department)}</option>)}</select></label>
        <label>{t.from}<input type="date" value={editor.command.from ?? ''} onChange={e => change('from', e.target.value)} /></label><label>{t.to}<input type="date" value={editor.command.to ?? ''} onChange={e => change('to', e.target.value)} /></label>
        <label>{t.contract}<input type="date" value={editor.command.contractEndDate ?? ''} onChange={e => change('contractEndDate', e.target.value)} /></label>{page?.declarationPolicy && <label>{language === 'tr' ? 'Beyan edilen atama durumu' : 'Declared assignment state'}<select value={editor.command.declaredStatus ?? ''} onChange={e => change('declaredStatus', e.target.value)}><option value="">{language === 'tr' ? 'Mevcut durumu koru' : 'Preserve current state'}</option>{page.declarationPolicy.statuses.map(status => <option key={status}>{status}</option>)}</select></label>}
        <label>{language === 'tr' ? 'Kaynak gözlem zamanı (UTC/offset)' : 'Source observation time (UTC/offset)'}<input placeholder="2026-11-02T10:00:00+03:00" value={editor.command.sourceObservedAt ?? ''} onChange={e => change('sourceObservedAt', e.target.value)} /></label><label>{t.watch}<input maxLength={64} value={editor.command.watchGroup ?? ''} onChange={e => change('watchGroup', e.target.value)} /></label></>}
      </fieldset><button type="submit" disabled={saving || !online || result === 'conflict'}>{result === 'uncertain' ? t.retry : t.save}</button>
        <button type="button" disabled={saving || result === 'uncertain'} onClick={() => { setEditor(null); setResult('idle'); setGeneration(v => v + 1) }}>{result === 'conflict' ? t.reload : t.cancel}</button></form>}
      {destination && <dialog ref={dialog} aria-label={t.dirty} onCancel={() => setDestination(null)}><p>{t.dirty}</p><button autoFocus disabled={saving || result === 'uncertain'} onClick={() => window.location.assign(destination)}>{t.reload}</button><button onClick={() => setDestination(null)}>{t.cancel}</button></dialog>}
      {assignment && detail && user && !editor && !paused && online && error === 0 && <CrewIdentityHistory key={`identity:${user.actorId}:${user.tenantId}:${user.csrfToken}:${vessel}:${assignment}`} vessel={vessel} assignment={assignment} language={language} />}
      {assignment && detail && user && !editor && !paused && online && error === 0 && <CrewAssignmentHistory key={`assignment-history:${user.actorId}:${user.tenantId}:${user.csrfToken}:${vessel}:${assignment}`} vessel={vessel} assignment={assignment} language={language} />}
      {assignment && detail && user && !editor && !paused && online && error === 0 && <CrewOwningInputs key={`owning-inputs:${user.actorId}:${user.tenantId}:${user.csrfToken}:${vessel}:${assignment}`} vessel={vessel} assignment={assignment} language={language} />}
      {assignment && detail && user && !editor && !paused && error === 0 && <CrewPersonalProfile key={`profile:${user.actorId}:${user.tenantId}:${user.csrfToken}:${vessel}:${assignment}`} vessel={vessel} assignment={assignment} language={language} csrf={user.csrfToken} onDirty={setProfileDirty} />}
      {assignment && row && detail && user && !editor && !paused && error === 0 && <CrewPersonalFiles key={`files:${user.actorId}:${user.tenantId}:${user.csrfToken}:${vessel}:${assignment}`} vessel={vessel} assignment={assignment} person={row.personId} language={language} csrf={user.csrfToken} onDirty={setFileDirty} />}
      {assignment && detail && user && !editor && !paused && error === 0 && <CertificatePanel key={`${user.actorId}:${user.tenantId}:${user.csrfToken}:${vessel}:${assignment}`} vessel={vessel} assignment={assignment} person={row?.personId} language={language} csrf={user.csrfToken} onDirty={setCertificateDirty} />}
    </main></div>
}
