import { useEffect, useState } from 'react'
import { profileName, profiles, quotaFresh } from './contract'
import { useOpsStatus } from './useOpsStatus'
import './agents.css'

export function AgentsDashboard() {
  const [language, setLanguage] = useState<'tr' | 'en'>('tr')
  const [now, setNow] = useState(Date.now)
  const { data, error, loading, receivedAt, retry } = useOpsStatus()
  const t = (tr: string, en: string) => language === 'tr' ? tr : en
  const locale = language === 'tr' ? 'tr-TR' : 'en-GB'
  const zone = Intl.DateTimeFormat().resolvedOptions().timeZone
  const date = (value: string | number | null | undefined) => value == null ? '—' : new Date(typeof value === 'number' ? value * 1000 : value).toLocaleString(locale, { dateStyle: 'short', timeStyle: 'medium' })
  const countdown = (epoch: number) => {
    const seconds = Math.max(0, Math.ceil(epoch - now / 1000))
    if (!seconds) return t('Süre doldu · yeni ölçüm bekleniyor', 'Due · awaiting a new measurement')
    return `${Math.floor(seconds / 86400)}${t('g', 'd')} ${String(Math.floor(seconds / 3600) % 24).padStart(2, '0')}:${String(Math.floor(seconds / 60) % 60).padStart(2, '0')}:${String(seconds % 60).padStart(2, '0')}`
  }
  const status = (value: string) => ({ known: t('Bilinen', 'Known'), busy: t('Meşgul', 'Busy'), unavailable: t('Çevrimdışı', 'Offline'), reconcile_required: t('Uzlaştırma gerekli', 'Reconciliation required'), armed: t('Kurulu', 'Armed'), waiting: t('Bekliyor', 'Waiting'), eligible: t('Uygun · servis bildirimi', 'Eligible · service reported'), unknown: t('Bilinmiyor', 'Unknown') })[value] ?? value
  const fresh = !!data && quotaFresh(data, now)
  const feedFresh = !!data && !error && receivedAt !== null && now - receivedAt < 20_000 && now - Date.parse(data.generatedAt) < 20_000 && now - Date.parse(data.generatedAt) >= -5_000
  useEffect(() => {
    const tick = setInterval(() => setNow(Date.now()), 1_000)
    return () => clearInterval(tick)
  }, [])
  useEffect(() => {
    document.documentElement.lang = language
    document.title = language === 'tr' ? 'POSEIDON · Geliştirme operasyonları' : 'POSEIDON · Development operations'
  }, [language])

  return <div className="ops-shell">
    <a className="skip-link" href="#ops-main">{t('Ana içeriğe geç', 'Skip to main content')}</a>
    <nav className="ops-nav" aria-label={t('Gezinme', 'Navigation')}>
      <a className="ops-brand" href="/">P<span>POSEIDON<small>LOCAL OPERATIONS</small></span></a>
      <div><a href="/">{t('Sistem bağlantısı', 'System connection')}</a><a href="/agents" aria-current="page">{t('Geliştirme operasyonları', 'Development operations')}</a></div>
      <span className="ops-local">{t('YEREL ÇALIŞMA ALANI', 'LOCAL WORKSPACE')}</span>
    </nav>
    <main id="ops-main" className="ops-main" tabIndex={-1}>
      <header className="ops-heading"><div><p className="ops-kicker">OPS / 01 · {t('GELİŞTİRME KONTROLÜ', 'DEVELOPMENT CONTROL')}</p><h1>{t('İki hesap. Tek görünüm.', 'Two accounts. One view.')}</h1><p>{t('Geliştirme oturumları, kota pencereleri ve kabul edilen ilerleme.', 'Development sessions, quota windows and accepted progress.')}</p></div>
        <div className="ops-tools"><div role="group" aria-label={t('Dil', 'Language')}>{(['tr', 'en'] as const).map(l => <button key={l} aria-pressed={language === l} onClick={() => setLanguage(l)}>{l.toUpperCase()}</button>)}</div><button onClick={retry} disabled={loading}>{loading ? t('Kontrol ediliyor…', 'Checking…') : t('Yenile', 'Refresh')}</button></div>
      </header>
      <div className="ops-feed"><span role="status">{error ? t('Servise ulaşılamadı veya veri geçersiz. Yeniden deneyin.', 'Service unavailable or invalid data. Retry.') : !data ? t('Veri bekleniyor…', 'Waiting for data…') : feedFresh ? t('Canlı servis bağlantısı · 5 sn sorgulama', 'Live service connection · 5s polling') : t('Veri güncel değil · uygunluk bilinmiyor', 'Stale data · eligibility unknown')}</span><span>{t('Son başarılı sorgu', 'Last successful poll')}: {receivedAt === null ? '—' : `${Math.max(0, Math.floor((now - receivedAt) / 1000))} ${t('sn önce', 's ago')}`}</span></div>
      {!data && <section className="ops-panel ops-empty"><h2>{t('Henüz operasyon verisi yok', 'No operations data yet')}</h2><p>{t('Hesap ve geçmiş bilgileri /ops/status yanıtından gösterilir.', 'Account and history information will appear from /ops/status.')}</p>{error && <button onClick={retry}>{t('Tekrar dene', 'Retry')}</button>}</section>}
      {data && <>
        <div className="ops-meta"><span>{t('Kota ölçümü', 'Quota measured')}: {date(data.quota.timestamp)} · {fresh ? t('Güncel', 'Fresh') : t('Bilinmiyor / eski ölçüm', 'Unknown / stale measurement')}</span><span>{t('Sonraki kontrol hedefi', 'Next check target')}: {date(data.quota.next_check)}</span></div>
        <div className="ops-profiles">{profiles.map(id => {
          const p = data.quota.profiles[id]
          const known = fresh && feedFresh && p.state === 'known'
          const ready = known && p.ready && !!p.windows?.length && p.windows.every(w => w.usedPercent < 85)
          const current = p.windows?.length ? p.windows : undefined
          const windows = current ?? (Array.isArray(p.lastKnown) ? p.lastKnown : p.lastKnown?.windows) ?? []
          const historical = windows.length > 0 && (!current || !fresh || !feedFresh || p.state !== 'known')
          const durations = [...new Set([300, 10080, ...windows.map(w => w.windowDurationMins)])]
          const jobs = data.coordinator.jobs.filter(j => j.profile === id)
          const activities = data.activities.filter(a => a.profile === id && !a.finishedAt && a.status === 'running')
          const working = feedFresh && activities.length > 0
          return <section className="ops-panel ops-profile" key={id} aria-labelledby={`${id}-title`}>
            <div className="ops-card-top"><span className="ops-avatar">{id === 'esra-pro' ? 'EP' : 'K+'}</span><div><h2 id={`${id}-title`}>{profileName(id)}</h2><p>{p.plan ?? '—'}</p></div><span className={`ops-badge ${working || ready ? 'ops-ready' : 'ops-caution'}`}>{working ? t('Çalışıyor', 'Working') : ready ? t('Yeni göreve uygun', 'Ready for new task') : t('Uygun değil / bilinmiyor', 'Ineligible / unknown')}</span></div>
            <p className="ops-flags">{working && p.state === 'busy' ? t('Görev sürüyor · kota ölçümü görev bitince yenilenecek', 'Task running · quota checked after the task') : `${status(p.state)}${!known ? ` · ${t('Kota uygunluğu bilinmiyor', 'Quota eligibility unknown')}` : ''}`}{historical && <strong className="ops-badge">{t('Son bilinen ölçüm', 'Last known measurement')}</strong>}{data.quota.selected === id && <span> · {t('Servisin seçimi', 'Service selection')}</span>}</p>
            <div className="ops-windows">{durations.map(duration => {
              const w = windows.find(item => item.windowDurationMins === duration)
              return <div className="ops-window" key={duration}><div className="ops-row"><h3>{duration === 300 ? t('5 saat', '5 hours') : duration === 10080 ? t('Haftalık', 'Weekly') : `${duration} ${t('dk', 'min')}`}</h3><strong>{w ? `${Number((100 - w.usedPercent).toFixed(2))}% ${t('kalan', 'remaining')}` : '—'}</strong></div>
                {w ? <><div className={`ops-meter ${!known || historical ? 'ops-muted' : ''}`} aria-label={t('Kullanılan yüzde', 'Used percent')} role="meter" aria-valuenow={w.usedPercent} aria-valuemin={0} aria-valuemax={100}><span style={{ width: `${w.usedPercent}%` }} /></div><p>{w.usedPercent}% {t('kullanıldı', 'used')}{historical ? ` · ${t('Son bilinen', 'Last known')}` : ''}</p><p className="ops-countdown">{t('Sıfırlanma', 'Reset')}: {countdown(w.resetsAt)}</p><small>{date(w.resetsAt)}</small></> : <p>{t('Pencere bildirilmedi', 'Window not reported')}</p>}
              </div>
            })}</div>
            <div className="ops-assignee"><h3>{t('Atanan hesap / görev / faz', 'Assignee / task / phase')}</h3>{activities.length ? activities.map(a => <p key={a.id}>{profileName(a.profile)} · <strong>{a.taskId}</strong> · {a.role} · {a.status}{a.model && ` · ${a.model} / ${a.effort ?? '—'}`}</p>) : <p>{t('Etkin atama bildirilmedi', 'No active assignment reported')}</p>}{jobs.map(j => <div key={j.id}><p><strong>{j.id}</strong> · {j.phase ?? '—'} · {j.status}</p>{j.reason && <p>{j.reason}</p>}{j.commit && <code>{j.commit}</code>}</div>)}</div>
          </section>
        })}</div>
        <div className="ops-summary">
          <section className="ops-panel"><p className="ops-kicker">{t('KOORDİNATÖR', 'COORDINATOR')}</p><h2>{t('CLI oturum bütçesi', 'CLI session budget')}</h2><p className="ops-big">{data.coordinator.modelCalls}<span> / {data.coordinator.maxCalls}</span></p><p>{data.coordinator.enabled ? t('Etkin', 'Enabled') : t('Devre dışı', 'Disabled')} · {data.coordinator.status}</p><p>{t('İş üst sınırı', 'Job limit')}: {data.coordinator.maxJobs} · {t('Bitiş', 'Deadline')}: {date(data.coordinator.deadline)}</p><small>{t('Çağrı / oturum sayısı; ham token kotası değildir.', 'Call / session count; not a raw token quota.')}</small></section>
          <section className="ops-panel"><p className="ops-kicker">{t('KABUL EDİLEN İLERLEME', 'ACCEPTED PROGRESS')}</p><h2>{data.project.stage || '—'}</h2><p className="ops-big">{data.project.completionPercent}<span>%</span></p><progress aria-label={t('Kabul edilen proje ilerlemesi', 'Accepted project progress')} value={data.project.completionPercent} max={100} /><p>{data.project.acceptedStoryPoints} / {data.project.totalStoryPoints} {t('kabul edilen hikâye puanı', 'accepted story points')}</p><p>{data.project.acceptedTasks} / {data.project.totalTasks} {t('kabul edilen görev', 'accepted tasks')}</p></section>
        </div>
        {data.modelPolicy && <section className="ops-panel"><h2>{t('Göreve göre model seçimi', 'Model selection by task')}</h2><p>{t('Otomatik pahalı modele geçiş', 'Automatic model escalation')}: {data.modelPolicy.automaticEscalation ? t('Açık', 'Enabled') : t('Kapalı', 'Disabled')}</p>{Object.entries(data.modelPolicy.roles).map(([role, choice]) => <p key={role}>{role}: <strong>{choice.model}</strong> · {choice.effort}</p>)}</section>}
        <div className="ops-details"><section className="ops-panel"><h2>{t('Alarm ve devam zamanı', 'Alarms and resume time')}</h2>{!data.alarms.length && <p className="ops-empty">{t('Alarm kaydı yok', 'No alarm records')}</p>}{data.alarms.map(a => <article className="ops-record" key={a.id}><div className="ops-row"><strong>{a.profile ? profileName(a.profile) : a.id}</strong><span className="ops-badge ops-caution">{!feedFresh || !fresh ? status('unknown') : status(a.state)}</span></div><p>{a.windowDurationMins === 300 ? t('5 saat', '5 hours') : a.windowDurationMins === 10080 ? t('Haftalık', 'Weekly') : a.windowDurationMins == null ? '—' : `${a.windowDurationMins} min`}{a.usedPercent != null && ` · ${a.usedPercent}%`}</p><p>{a.reason ?? t('Neden bildirilmedi', 'No reason reported')}</p><p>{t('Devam hedefi', 'Resume target')}: {date(a.resumeAt)}</p>{a.resumeAt != null && <p className="ops-countdown">{countdown(a.resumeAt)}</p>}{a.resetAt != null && <p>{t('Sıfırlanma', 'Reset')}: {date(a.resetAt)}</p>}</article>)}</section>
          <section className="ops-panel"><h2>{t('Kayıtlı zaman çizelgesi ve devirler', 'Recorded timeline and handovers')}</h2><p>{t('Yalnızca servisin bildirdiği olaylar. Yerel saat', 'Only service reported events. Local time')}: {zone}</p>{!data.events.length && !data.activities.length && <p className="ops-empty">{t('Geçmiş kaydı yok', 'No history records')}</p>}
            <ol className="ops-timeline">{[...data.events].sort((a, b) => Date.parse(b.at) - Date.parse(a.at)).map(e => <li key={e.id}><time dateTime={e.at}>{date(e.at)}</time><h3>{e.type} · {e.profile ? profileName(e.profile) : '—'} {e.taskId}</h3>{(e.from || e.to) && <p>{e.from ? profileName(e.from) : '—'} → {e.to ? profileName(e.to) : '—'}</p>}<p>{e.message ?? t('Neden / açıklama bildirilmedi', 'No reason / description reported')}</p>{e.usedPercent != null && <p>{e.usedPercent}% · {e.windowDurationMins ?? '—'} min</p>}{e.resetAt != null && <p>{t('Sıfırlanma', 'Reset')}: {date(e.resetAt)}</p>}</li>)}</ol>
            {data.activities.map(a => <article className="ops-record" key={a.id}><h3>{profileName(a.profile)} · {a.taskId}</h3><p>{a.role} · {a.status}</p><p>{t('Başlangıç', 'Started')}: {date(a.startedAt)} · {t('Bitiş', 'Finished')}: {date(a.finishedAt)}</p></article>)}
          </section></div>
      </>}
      <footer className="ops-footer"><span>OPS-DASH-UI-01 · {t('Yerel geliştirme operasyonları', 'Local development operations')}</span><span>{t('Yüzdeler plan pencereleridir; faturalı token tahmini değildir. Deniz operasyonu hazırlık durumu değildir.', 'Percentages describe plan windows, not billed token estimates. This is not maritime readiness.')}</span><span>{t('Tüm tarihler yerel saatle', 'All dates in local time')}: {zone}</span></footer>
    </main>
  </div>
}
