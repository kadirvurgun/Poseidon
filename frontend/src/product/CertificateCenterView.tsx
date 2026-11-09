import { sourceText, localizedLabel } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { EnterprisePagination, EnterpriseToolbar } from './EnterprisePatterns';
import { CertificateSummary } from './CertificateSummary';
import { temporalStates, type CertificateCapabilities, type CertificatePage, type CertificateScope } from './certificates-api';
import { certificateTemporal } from './certificate-copy';
import { certificateLocationPath, type CertificateLocation } from './certificate-location';
export function CertificateCenterView({ page, caps, route, language, onNavigate }: {
    page: CertificatePage;
    caps: CertificateCapabilities;
    route: CertificateLocation;
    language: AppLocale;
    onNavigate: (href: string) => void;
}) {
    const en = language === 'en';
    return <section aria-label={localizedText(en, "pairs.m_2d72a8a7f46ccfae")}>
    <form aria-label={localizedText(en, "pairs.m_638f3dc0ddd842ea")} onSubmit={event => event.preventDefault()}>
      <EnterpriseToolbar>
        <label>{localizedText(en, "pairs.m_7ce3d2dee6f18608")}<select value={page.scope} onChange={event => onNavigate(certificateLocationPath({ ...route, scope: event.target.value as CertificateScope, offset: 0 }))}>
          {caps.scopes.map(scope => <option key={scope} value={scope}>{scope === 'Vessel' ? (localizedText(en, "pairs.m_72b2d1a720a09207")) : scope === 'Crew' ? (localizedText(en, "pairs.m_8a2309fc2ff8d602")) : (localizedText(en, "pairs.m_ed2dd54be72d9571"))}</option>)}
        </select></label>
        <label>{localizedText(en, "pairs.m_f870d78bf20c67ee")}<select value={route.temporal ?? ''} onChange={event => onNavigate(certificateLocationPath({ ...route, temporal: event.target.value === '' ? undefined : event.target.value as CertificateLocation['temporal'], offset: 0 }))}>
          <option value="">{localizedText(en, "pairs.m_2295d00bc9fa1449")}</option>
          {temporalStates.map(state => <option key={state} value={state}>{certificateTemporal(state, en)}</option>)}
        </select></label>
      </EnterpriseToolbar>
    </form>
    <h2>{localizedText(en, "pairs.m_2dd62898c35b6ce0")}</h2>
    <p>{localizedText(en, "pairs.m_07b0fdf3c163b443")}</p>
    {page.crewCoverage === 'UNAVAILABLE_POLICY_OR_AUTHORITY' && <p role="alert">{localizedText(en, "pairs.m_01fe990a8e66d5bc")}</p>}
    <dl className="certificate-calendar-counts">{([
            ['valid', sourceText("en", "pairs.m_1b5e76c4b3850ebb"), sourceText("tr", "pairs.m_1b5e76c4b3850ebb")], ['due90', sourceText("en", "pairs.m_bf04b097b785dfd8"), sourceText("tr", "pairs.m_bf04b097b785dfd8")],
            ['due60', sourceText("en", "pairs.m_dfdff3e89145504f"), sourceText("tr", "pairs.m_dfdff3e89145504f")], ['due30', sourceText("en", "pairs.m_61daa0728e0e61bb"), sourceText("tr", "pairs.m_61daa0728e0e61bb")],
            ['expired', sourceText("en", "pairs.m_9019a7f3d4c7d12f"), sourceText("tr", "pairs.m_9019a7f3d4c7d12f")], ['surveyWindow', sourceText("en", "pairs.m_b10209e90cd25130"), sourceText("tr", "pairs.m_b10209e90cd25130")],
            ['reviewRequired', sourceText("en", "pairs.m_4bd59dd603c00386"), sourceText("tr", "pairs.m_4bd59dd603c00386")],
        ] as const).map(([key, label, tr]) => <div key={key}><dt>{localizedLabel({ en: label, tr: tr }, en)}</dt><dd>{page.counts[key]}</dd></div>)}</dl>
    <p>{localizedText(en, "pairs.m_98b47872df841534")}</p>
    {!page.items.length && <p>{localizedText(en, "pairs.m_b30989df215b19d3")}</p>}
    <ul className="certificate-record-list">{page.items.map(row => <li key={row.scope + '/' + row.id}>
      <div className="certificate-record-heading"><Button variant="outline" onClick={() => onNavigate(row.href)}>{row.content.documentName ?? row.content.metadata.declaration.certificateNumber ?? (localizedText(en, "pairs.m_508a3af9057ca93b"))}</Button>
        <Badge tone="neutral">{row.scope === 'Vessel' ? (localizedText(en, "pairs.m_e50c79dfa3d76f1e")) : (localizedText(en, "pairs.m_b52bb1d7f1ee92e9"))}</Badge></div>
      <CertificateSummary row={row} language={language}/>
    </li>)}</ul>
    <EnterprisePagination label={localizedText(en, "pairs.m_d948323cc7886ce8")} previous={localizedText(en, "pairs.m_d8668d7a51561389")} next={localizedText(en, "pairs.m_c6184d532434e325")} previousDisabled={!route.offset} nextDisabled={!page.hasMore || route.offset >= 100000} onPrevious={() => onNavigate(certificateLocationPath({ ...route, offset: Math.max(0, route.offset - 25) }))} onNext={() => onNavigate(certificateLocationPath({ ...route, offset: route.offset + 25 }))}/>
  </section>;
}
