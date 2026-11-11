import { LanguageSelector } from "../components/LanguageSelector";
import { localizedText } from "../i18n/messages";
import { useAppLanguage } from "../i18n";
import { presentationLabel } from '../i18n/labels';
import { Button } from '../components/ui/button';
import { useEffect, useState } from 'react';
import { profileName, profiles, quotaFresh } from './contract';
import { useOpsStatus } from './useOpsStatus';
import './agents.css';
export function AgentsDashboard() {
    const [language, setLanguage] = useAppLanguage();
    const [now, setNow] = useState(Date.now);
    const { data, error, loading, receivedAt, retry } = useOpsStatus();
    const locale = language;
    const zone = Intl.DateTimeFormat().resolvedOptions().timeZone;
    const date = (value: string | number | null | undefined) => value == null ? '—' : new Date(typeof value === 'number' ? value * 1000 : value).toLocaleString(locale, { dateStyle: 'short', timeStyle: 'medium' });
    const countdown = (epoch: number) => {
        const seconds = Math.max(0, Math.ceil(epoch - now / 1000));
        if (!seconds)
            return localizedText(language, "pairs.m_1663b63ed86d5307");
        return `${Math.floor(seconds / 86400)}${localizedText(language, "pairs.m_6b94e2d658601572")} ${String(Math.floor(seconds / 3600) % 24).padStart(2, '0')}:${String(Math.floor(seconds / 60) % 60).padStart(2, '0')}:${String(seconds % 60).padStart(2, '0')}`;
    };
    const status = (value: string) => ({ known: localizedText(language, "pairs.m_235d006bb61aac5a"), busy: localizedText(language, "pairs.m_f48dadd623857e2a"), unavailable: localizedText(language, "pairs.m_62c43b36825d5e83"), reconcile_required: localizedText(language, "pairs.m_ed81026957ac3b45"), armed: localizedText(language, "pairs.m_120daaf26bdd2fd5"), waiting: localizedText(language, "pairs.m_eb7cac8cdee6ea0a"), eligible: localizedText(language, "pairs.m_23f2d65cbdd68c6a"), unknown: localizedText(language, "pairs.m_a5a7675bd7000dbd") })[value] ?? value;
    const fresh = !!data && quotaFresh(data, now);
    const feedFresh = !!data && !error && receivedAt !== null && now - receivedAt < 20000 && now - Date.parse(data.generatedAt) < 20000 && now - Date.parse(data.generatedAt) >= -5000;
    useEffect(() => {
        const tick = setInterval(() => setNow(Date.now()), 1000);
        return () => clearInterval(tick);
    }, []);
    useEffect(() => {
        document.title = localizedText(language, "pairs.m_65cd629e1c21ba82");
    }, [language]);
    return <div className="ops-shell">
    <a className="skip-link" href="#ops-main">{localizedText(language, "pairs.m_c5380d7bc08b74cb")}</a>
    <nav className="ops-nav" aria-label={localizedText(language, "pairs.m_8bd42749f119c07d")}>
      <a className="ops-brand" href="/">P<span>POSEIDON<small>{presentationLabel(language, 'localOperations')}</small></span></a>
      <div><a href="/">{localizedText(language, "pairs.m_d8afd34c0d19a4c7")}</a><a href="/agents" aria-current="page">{localizedText(language, "pairs.m_7c98a73bc97d1a74")}</a></div>
      <span className="ops-local">{localizedText(language, "pairs.m_a17676cba090ee7a")}</span>
    </nav>
    <main id="ops-main" className="ops-main" tabIndex={-1}>
      <header className="ops-heading"><div><p className="ops-kicker">OPS / 01 · {localizedText(language, "pairs.m_c2e1aaea2163a587")}</p><h1>{localizedText(language, "pairs.m_acbe201bbe455558")}</h1><p>{localizedText(language, "pairs.m_6099246924366da3")}</p></div>
        <div className="ops-tools"><LanguageSelector locale={language} onChange={setLanguage} /><Button type="button" variant="outline" onClick={retry} disabled={loading}>{loading ? localizedText(language, "pairs.m_68f365bcf102d90e") : localizedText(language, "pairs.m_ae83b5ec8597dbb9")}</Button></div>
      </header>
      <div className="ops-feed"><span role="status">{error ? localizedText(language, "pairs.m_23493d5886cfc26a") : !data ? localizedText(language, "pairs.m_6503d65c1320e738") : feedFresh ? localizedText(language, "pairs.m_42022f6857625409") : localizedText(language, "pairs.m_e9fd0bfca623163f")}</span><span>{localizedText(language, "pairs.m_b346131002c53903")}: {receivedAt === null ? '—' : `${Math.max(0, Math.floor((now - receivedAt) / 1000))} ${localizedText(language, "pairs.m_df664a0f8ff6f30e")}`}</span></div>
      {!data && <section className="ops-panel ops-empty"><h2>{localizedText(language, "pairs.m_6086c6255c22550b")}</h2><p>{localizedText(language, "pairs.m_80ba2ae70893bbba")}</p>{error && <Button type="button" variant="outline" onClick={retry}>{localizedText(language, "pairs.m_044bf27da41fda96")}</Button>}</section>}
      {data && <>
        <div className="ops-meta"><span>{localizedText(language, "pairs.m_769e6e3ac33e2196")}: {date(data.quota.timestamp)} · {fresh ? localizedText(language, "pairs.m_e175dc632067f200") : localizedText(language, "pairs.m_a06d77cf96dd1698")}</span><span>{localizedText(language, "pairs.m_85a6edb6e81a13ca")}: {date(data.quota.next_check)}</span></div>
        <div className="ops-profiles">{profiles.map(id => {
                const p = data.quota.profiles[id];
                const known = fresh && feedFresh && p.state === 'known';
                const ready = known && p.ready && !!p.windows?.length && p.windows.every(w => w.usedPercent < 85);
                const current = p.windows?.length ? p.windows : undefined;
                const windows = current ?? (Array.isArray(p.lastKnown) ? p.lastKnown : p.lastKnown?.windows) ?? [];
                const historical = windows.length > 0 && (!current || !fresh || !feedFresh || p.state !== 'known');
                const durations = [...new Set([300, 10080, ...windows.map(w => w.windowDurationMins)])];
                const jobs = data.coordinator.jobs.filter(j => j.profile === id);
                const activities = data.activities.filter(a => a.profile === id && !a.finishedAt && a.status === 'running');
                const working = feedFresh && activities.length > 0;
                return <section className="ops-panel ops-profile" key={id} aria-labelledby={`${id}-title`}>
            <div className="ops-card-top"><span className="ops-avatar">{id === 'esra-pro' ? 'EP' : 'K+'}</span><div><h2 id={`${id}-title`}>{profileName(id)}</h2><p>{p.plan ?? '—'}</p></div><span className={`ops-badge ${working || ready ? 'ops-ready' : 'ops-caution'}`}>{working ? localizedText(language, "pairs.m_e7173ceb44d9cb0d") : ready ? localizedText(language, "pairs.m_38f117fe854600ff") : localizedText(language, "pairs.m_755e42f489cfe27d")}</span></div>
            <p className="ops-flags">{working && p.state === 'busy' ? localizedText(language, "pairs.m_a31e4faccccdd91a") : `${status(p.state)}${!known ? ` · ${localizedText(language, "pairs.m_7157d5173526ab9c")}` : ''}`}{historical && <strong className="ops-badge">{localizedText(language, "pairs.m_4fdb90776ce0c838")}</strong>}{data.quota.selected === id && <span> · {localizedText(language, "pairs.m_37852e6a723a4e71")}</span>}</p>
            <small>{localizedText(language, "pairs.m_95c5f86f46ff50fb")}: {date(p.observedAt ?? (!Array.isArray(p.lastKnown) ? p.lastKnown?.observedAt : null))}</small>
            <div className="ops-windows">{durations.map(duration => {
                        const w = windows.filter(item => item.windowDurationMins === duration).sort((a, b) => b.usedPercent - a.usedPercent)[0];
                        return <div className="ops-window" key={duration}><div className="ops-row"><h3>{duration === 300 ? localizedText(language, "pairs.m_f137395234b207a2") : duration === 10080 ? localizedText(language, "pairs.m_b4c8047d62caea8a") : `${duration} ${localizedText(language, "pairs.m_a90e17666f30d0fa")}`}</h3><strong>{w ? `${Number((100 - w.usedPercent).toFixed(2))}% ${localizedText(language, "pairs.m_56186eac1197591c")}` : '—'}</strong></div>
                {w ? <><div className={`ops-meter ${!known || historical ? 'ops-muted' : ''}`} aria-label={localizedText(language, "pairs.m_7229b78c8e43c77e")} role="meter" aria-valuenow={w.usedPercent} aria-valuemin={0} aria-valuemax={100}><span style={{ width: `${w.usedPercent}%` }}/></div><p>{w.usedPercent}% {localizedText(language, "pairs.m_55a3f7d7bda93575")}{historical ? ` · ${localizedText(language, "pairs.m_837c6b5f08aa8793")}` : ''}</p>{w.resetsAt == null ? <p>{localizedText(language, "pairs.m_a9dbaa0c1156ad2b")}</p> : <><p className="ops-countdown">{localizedText(language, "pairs.m_a56ab8b0073cfef4")}: {countdown(w.resetsAt)}</p><small>{date(w.resetsAt)}</small></>}</> : <p>{localizedText(language, "pairs.m_902e846da379f3cb")}</p>}
              </div>;
                    })}</div>
            <div className="ops-assignee"><h3>{localizedText(language, "pairs.m_d08507fb2e6454bc")}</h3>{activities.length ? activities.map(a => <p key={a.id}>{profileName(a.profile)} · <strong>{a.taskId}</strong> · {a.role} · {a.status}{a.model && ` · ${a.model} / ${a.effort ?? '—'}`}</p>) : <p>{localizedText(language, "pairs.m_5740205931968a2e")}</p>}{jobs.map(j => <div key={j.id}><p><strong>{j.id}</strong> · {j.phase ?? '—'} · {j.status}</p>{j.reason && <p>{j.reason}</p>}{j.commit && <code>{j.commit}</code>}</div>)}</div>
          </section>;
            })}</div>
        <div className="ops-summary">
          <section className="ops-panel"><p className="ops-kicker">{localizedText(language, "pairs.m_86eff47a62317084")}</p><h2>{localizedText(language, "pairs.m_f919b1583c440ca8")}</h2><p className="ops-big">{data.coordinator.modelCalls}<span> / {data.coordinator.maxCalls}</span></p><p>{data.coordinator.enabled ? localizedText(language, "pairs.m_964e5f18d7347306") : localizedText(language, "pairs.m_42aeb0f45f977b73")} · {data.coordinator.status}</p><p>{localizedText(language, "pairs.m_680f06424b6db191")}: {data.coordinator.maxJobs} · {localizedText(language, "pairs.m_733947d770c88830")}: {date(data.coordinator.deadline)}</p><small>{localizedText(language, "pairs.m_8f5c4c1e7bd60828")}</small></section>
          <section className="ops-panel"><p className="ops-kicker">{localizedText(language, "pairs.m_9b2adccabf2639d8")}</p><h2>{data.project.stage || '—'}</h2><p className="ops-big">{data.project.completionPercent}<span>%</span></p><progress aria-label={localizedText(language, "pairs.m_a581dd376b5cd7ae")} value={data.project.completionPercent} max={100}/><p>{data.project.acceptedStoryPoints} / {data.project.totalStoryPoints} {localizedText(language, "pairs.m_0d76880f9f2c0fb0")}</p><p>{data.project.acceptedTasks} / {data.project.totalTasks} {localizedText(language, "pairs.m_b849f915469b33bd")}</p></section>
        </div>
        {data.modelPolicy && <section className="ops-panel"><h2>{localizedText(language, "pairs.m_5f89730ffb04fa99")}</h2><p>{localizedText(language, "pairs.m_c528e7e4d3451732")}: {data.modelPolicy.automaticEscalation ? localizedText(language, "pairs.m_16314941279af292") : localizedText(language, "pairs.m_e5d604ae0982ac3c")}</p>{Object.entries(data.modelPolicy.roles).map(([role, choice]) => <p key={role}>{role}: <strong>{choice.model}</strong> · {choice.effort}</p>)}</section>}
        <div className="ops-details"><section className="ops-panel"><h2>{localizedText(language, "pairs.m_85dab443f07f78b0")}</h2>{!data.alarms.length && <p className="ops-empty">{localizedText(language, "pairs.m_0d28cd5137cf11f0")}</p>}{data.alarms.map(a => <article className="ops-record" key={a.id}><div className="ops-row"><strong>{a.profile ? profileName(a.profile) : a.id}</strong><span className="ops-badge ops-caution">{!feedFresh || !fresh ? status('unknown') : status(a.state)}</span></div><p>{a.windowDurationMins === 300 ? localizedText(language, "pairs.m_f137395234b207a2") : a.windowDurationMins === 10080 ? localizedText(language, "pairs.m_b4c8047d62caea8a") : a.windowDurationMins == null ? '—' : `${a.windowDurationMins} min`}{a.usedPercent != null && ` · ${a.usedPercent}%`}</p><p>{a.reason ?? localizedText(language, "pairs.m_7d2f22e8949a79b3")}</p><p>{localizedText(language, "pairs.m_822184313325ad67")}: {date(a.resumeAt)}</p>{a.resumeAt != null && <p className="ops-countdown">{countdown(a.resumeAt)}</p>}{a.resetAt != null && <p>{localizedText(language, "pairs.m_a56ab8b0073cfef4")}: {date(a.resetAt)}</p>}</article>)}</section>
          <section className="ops-panel"><h2>{localizedText(language, "pairs.m_fd431ef5e07b3b24")}</h2><p>{localizedText(language, "pairs.m_521bc63c0bbf76aa")}: {zone}</p>{!data.events.length && !data.activities.length && <p className="ops-empty">{localizedText(language, "pairs.m_b69be1fdf9347ec0")}</p>}
            <ol className="ops-timeline">{[...data.events].sort((a, b) => Date.parse(b.at) - Date.parse(a.at)).map(e => <li key={e.id}><time dateTime={e.at}>{date(e.at)}</time><h3>{e.type} · {e.profile ? profileName(e.profile) : '—'} {e.taskId}</h3>{(e.from || e.to) && <p>{e.from ? profileName(e.from) : '—'} → {e.to ? profileName(e.to) : '—'}</p>}<p>{e.message ?? localizedText(language, "pairs.m_3e1962aa334ee91c")}</p>{e.usedPercent != null && <p>{e.usedPercent}% · {e.windowDurationMins ?? '—'} min</p>}{e.resetAt != null && <p>{localizedText(language, "pairs.m_a56ab8b0073cfef4")}: {date(e.resetAt)}</p>}</li>)}</ol>
            {data.activities.map(a => <article className="ops-record" key={a.id}><h3>{profileName(a.profile)} · {a.taskId}</h3><p>{a.role} · {a.status}</p><p>{localizedText(language, "pairs.m_ce0bd11539d1a82e")}: {date(a.startedAt)} · {localizedText(language, "pairs.m_12522612164ee408")}: {date(a.finishedAt)}</p></article>)}
          </section></div>
      </>}
      <footer className="ops-footer"><span>OPS-DASH-UI-01 · {localizedText(language, "pairs.m_4344e4dc87a00a39")}</span><span>{localizedText(language, "pairs.m_98cc16eae5b6b4fe")}</span><span>{localizedText(language, "pairs.m_81e225695619981f")}: {zone}</span></footer>
    </main>
  </div>;
}
