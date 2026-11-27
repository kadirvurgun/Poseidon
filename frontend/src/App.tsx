import { LanguageSelector } from "./components/LanguageSelector";
import { localizedCopy, localizedText } from "./i18n/messages";
import { useAppLanguage } from "./i18n";
import { presentationLabel } from './i18n/labels';
import { Button } from './components/ui/button';
import { ImportWorkspace } from './product/ImportWorkspace';
import { DailyReportWorkspace } from './product/DailyReportWorkspace';
import { TugWorkspace } from './product/TugWorkspace';
import { TugReadinessWorkspace } from './product/TugReadinessWorkspace';
import { TowageWorkspace } from './product/TowageWorkspace';
import { FieldOpsWorkspace } from './product/FieldOpsWorkspace';
import { TugOperationsWorkspace } from './product/TugOperationsWorkspace';
import { HighRiskWorkWorkspace } from './product/HighRiskWorkWorkspace';
import { SafetyEventsWorkspace } from './product/SafetyEventsWorkspace';
import { DataTrustCenterWorkspace } from './product/DataTrustCenterWorkspace';
import { IndustryAssuranceWorkspace } from './product/IndustryAssuranceWorkspace';
import { TechnicalWorkspace } from './product/TechnicalWorkspace';
import { CrewWorkspace } from './product/CrewWorkspace';
import { CrewComplianceWorkspace } from './product/CrewComplianceWorkspace';
import { useEffect, useState } from 'react';
import { getSystemStatus } from './api';
import { AgentsDashboard } from './agents/AgentsDashboard';
import { ProductApp } from './product/ProductApp';
import { SignInGate } from './product/SignInGate';
import { SystemHealthWorkspace } from './product/SystemHealthWorkspace';
import { NotificationWorkspace } from './product/NotificationWorkspace';
import { ConflictCenterWorkspace } from './product/ConflictCenterWorkspace';
import { ShipOperationsWorkspace } from './product/ShipOperationsWorkspace';
import { CertificateWorkspace } from './product/CertificateWorkspace';
import { DefectWorkspace } from './product/DefectWorkspace';
import { WorkOrderWorkspace } from './product/WorkOrderWorkspace';
import { DowntimeWorkspace } from './product/DowntimeWorkspace';
import { RiskAssessmentWorkspace } from './product/RiskAssessmentWorkspace';
import { PmsCommandCenterWorkspace } from './product/PmsCommandCenterWorkspace';
import { DepartureReadinessWorkspace } from './product/DepartureReadinessWorkspace';
import { RegulatoryReviewWorkspace } from './product/RegulatoryReviewWorkspace';
import { ServiceReportWorkspace } from './product/ServiceReportWorkspace';
import { VoiceEventDraftWorkspace } from './product/VoiceEventDraftWorkspace';
import { DocumentCompareWorkspace } from './product/DocumentCompareWorkspace';
import { UatWorkspace } from './product/UatWorkspace';
import { PilotGateWorkspace } from './product/PilotGateWorkspace';
import { PilotProgramWorkspace } from './product/PilotProgramWorkspace';
import { UserAdminWorkspace } from './product/UserAdminWorkspace';
import { AcceptInvitationPage } from './product/AcceptInvitationPage';
import { RiskTemplateLibraryWorkspace } from './product/RiskTemplateLibraryWorkspace';
import { CriticalSparesWorkspace } from './product/CriticalSparesWorkspace';
import { AuditReadyWorkspace } from './product/AuditReadyWorkspace';
import { SurveyWorkspace } from './product/SurveyWorkspace';
import { ShipDemoJourney } from './product/ShipDemoJourney';
import { DataReconciliationWorkspace } from './product/DataReconciliationWorkspace';
import { AppearanceControls } from './theme/AppearanceControls';
import { HelpCenterWorkspace } from './help/HelpCenterWorkspace';
type Connection = 'loading' | 'available' | 'error';
const content = localizedCopy("App_tsx_content");
export function App() {
    // UAT-DEPLOY-01: a signed-out visitor gets one full-page sign-in screen on every /app page.
    const path = window.location.pathname;
    if (path === '/app' || path.startsWith('/app/'))
        return <SignInGate><AppRoutes /></SignInGate>;
    return <AppRoutes />;
}
function AppRoutes() {
    // S10 WP-C DEV-120 ship buyer demo journey (read-only guide over existing screens).
    if (/^\/app\/ship-demo(?:\/|$)/.test(window.location.pathname))
        return <ShipDemoJourney />;
    if (/^\/app\/(fuel-bunker-log|port-activity-sof)(?:\/|$)/.test(window.location.pathname))
        return <ShipOperationsWorkspace />;
    if (/^\/app\/(certificate-command-center|renewal-workflow)(?:\/|$)/.test(window.location.pathname))
        return <CertificateWorkspace />;
    if (/^\/app\/(pre-survey-checklist|survey-planner)(?:\/|$)/.test(window.location.pathname))
        return <SurveyWorkspace />;
    if (/^\/app\/(noon-report|deck-daily-log|engine-daily-log)(?:\/|$)/.test(window.location.pathname))
        return <DailyReportWorkspace />;
    if (/^\/app\/audit-ready(?:\/|$)/.test(window.location.pathname))
        return <AuditReadyWorkspace />;
    // HELP-CENTER-01: page guide index; lists only guides of the caller's own granted pages.
    if (/^\/app\/help\/?$/.test(window.location.pathname))
        return <HelpCenterWorkspace />;
    if (window.location.pathname === '/app/system-health')
        return <SystemHealthWorkspace />;
    if (window.location.pathname === '/app/notification-escalation')
        return <NotificationWorkspace />;
    if (window.location.pathname === '/app/offline-conflict-center')
        return <ConflictCenterWorkspace />;
    // S11 WP-A DEV-070 pilot data reconciliation report and data owner sign-off.
    if (/^\/app\/data-reconciliation\/[0-9a-fA-F-]{36}\/?$/.test(window.location.pathname))
        return <DataReconciliationWorkspace />;
    const importer = /^\/app\/excel-migration\/([0-9a-fA-F-]{36})\/?$/.exec(window.location.pathname);
    if (importer)
        return <ImportWorkspace key={importer[1]} vessel={importer[1]}/>;
    if (/^\/app\/tug-(assignment|readiness|equipment-readiness|crew-readiness)(?:\/|$)/.test(window.location.pathname))
        return <TugReadinessWorkspace />;
    // S8 WP-D DEV-087/088 towage plan and risk & briefing; DEV-107 fleet high-risk work.
    // S9 WP-D DEV-089/090/111 towage execution console, tug daily log and drydock planning.
    if (/^\/app\/(towage-execution|tug-daily-log|drydock-planning)(?:\/|$)/.test(window.location.pathname))
        return <FieldOpsWorkspace />;
    // S10 WP-B DEV-091/092/119 tug operation history, offline tug operations and tug demo journey.
    if (/^\/app\/(tug-operation-history|tug-operations-offline|tug-demo-journey)(?:\/|$)/.test(window.location.pathname))
        return <TugOperationsWorkspace />;
    if (/^\/app\/towage-(plan|risk-briefing)(?:\/|$)/.test(window.location.pathname))
        return <TowageWorkspace />;
    if (/^\/app\/high-risk-work(?:\/|$)/.test(window.location.pathname))
        return <HighRiskWorkWorkspace />;
    // S9 WP-B DEV-060/061 safety events, investigation and action tracking.
    if (/^\/app\/safety-events(?:\/|$)/.test(window.location.pathname))
        return <SafetyEventsWorkspace />;
    // S10 WP-A DEV-065 data trust center and DEV-113 industry assurance (SIRE 2.0 synthetic schema).
    if (/^\/app\/data-trust-center(?:\/|$)/.test(window.location.pathname))
        return <DataTrustCenterWorkspace />;
    if (/^\/app\/industry-assurance(?:\/|$)/.test(window.location.pathname))
        return <IndustryAssuranceWorkspace />;
    if (/^\/app\/tug-(command|job-board|job-intake|job-detail)(?:\/|$)/.test(window.location.pathname))
        return <TugWorkspace />;
    if (/^\/app\/(equipment-hierarchy|maintenance-plan)\//.test(window.location.pathname))
        return <TechnicalWorkspace />;
    if (/^\/app\/defects(?:\/|$)/.test(window.location.pathname))
        return <DefectWorkspace />;
    if (/^\/app\/departure-signoff(?:\/|$)/.test(window.location.pathname))
        return <DepartureReadinessWorkspace />;
    if (/^\/app\/regulatory-review(?:\/|$)/.test(window.location.pathname))
        return <RegulatoryReviewWorkspace />;
    if (/^\/app\/pms-command-center(?:\/|$)/.test(window.location.pathname))
        return <PmsCommandCenterWorkspace />;
    if (/^\/app\/service-engineer-report(?:\/|$)/.test(window.location.pathname))
        return <ServiceReportWorkspace />;
    if (/^\/app\/voice-event-draft(?:\/|$)/.test(window.location.pathname))
        return <VoiceEventDraftWorkspace />;
    if (/^\/app\/role-uat(?:\/|$)/.test(window.location.pathname))
        return <UatWorkspace />;
    if (/^\/app\/pilot-gate(?:\/|$)/.test(window.location.pathname))
        return <PilotGateWorkspace />;
    if (/^\/app\/pilot-program(?:\/|$)/.test(window.location.pathname))
        return <PilotProgramWorkspace />;
    // USER-ADMIN-02: invitation acceptance; reached only through an invitation link, never a navigation destination.
    if (/^\/app\/accept-invitation\/?$/.test(window.location.pathname))
        return <AcceptInvitationPage />;
    if (/^\/app\/user-admin(?:\/|$)/.test(window.location.pathname))
        return <UserAdminWorkspace />;
    if (/^\/app\/document-compare(?:\/|$)/.test(window.location.pathname))
        return <DocumentCompareWorkspace />;
    if (/^\/app\/work-order-center(?:\/|$)/.test(window.location.pathname))
        return <WorkOrderWorkspace />;
    if (/^\/app\/operational-downtime(?:\/|$)/.test(window.location.pathname))
        return <DowntimeWorkspace />;
    if (/^\/app\/risk-assessment(?:\/|$)/.test(window.location.pathname))
        return <RiskAssessmentWorkspace />;
    if (/^\/app\/risk-template-library(?:\/|$)/.test(window.location.pathname))
        return <RiskTemplateLibraryWorkspace />;
    if (/^\/app\/critical-spares(?:\/|$)/.test(window.location.pathname))
        return <CriticalSparesWorkspace />;
    const crew = /^\/app\/crew-roster\/([0-9a-fA-F-]{36})(?:\/crew-profile\/([0-9a-fA-F-]{36}))?\/?$/.exec(window.location.pathname);
    if (crew)
        return <CrewWorkspace key={`${crew[1]}:${crew[2] ?? ''}`} vessel={crew[1]} assignment={crew[2]}/>;
    // S7 WP-A: crew certificates (DEV-048), training-position matrix (DEV-049), Safe Manning (DEV-050).
    const compliance = /^\/app\/(crew-certificates|training-position-matrix|safe-manning)\/([0-9a-fA-F-]{36})\/?$/.exec(window.location.pathname);
    if (compliance)
        return <CrewComplianceWorkspace key={`${compliance[1]}:${compliance[2]}`} view={compliance[1] === 'crew-certificates' ? 'certificates' : compliance[1] === 'training-position-matrix' ? 'matrix' : 'safe-manning'} vessel={compliance[2]}/>;
    if (window.location.pathname === '/app' || window.location.pathname.startsWith('/app/'))
        return <ProductApp />;
    return window.location.pathname.replace(/\/$/, '') === '/agents' ? <AgentsDashboard /> : <ExistingApp />;
}
export function ExistingApp() {
    const [language, setLanguage] = useAppLanguage();
    const [connection, setConnection] = useState<Connection>('loading');
    const [attempt, setAttempt] = useState(0);
    const t = content[language];
    useEffect(() => {
        document.title = `POSEIDON · ${content[language].page}`;
    }, [language]);
    useEffect(() => {
        const controller = new AbortController();
        const timeout = setTimeout(() => controller.abort(), 10000);
        let current = true;
        getSystemStatus(controller.signal)
            .then(() => { if (current)
            setConnection('available'); })
            .catch(() => { if (current)
            setConnection('error'); })
            .finally(() => clearTimeout(timeout));
        return () => { current = false; clearTimeout(timeout); controller.abort(); };
    }, [attempt]);
    const retry = () => { setConnection('loading'); setAttempt(value => value + 1); };
    return (<div className="app-shell">
      <a className="skip-link" href="#main">{t.skip}</a>
      <aside className="sidebar" aria-label="POSEIDON">
        <a className="brand" href="#main" aria-label="POSEIDON"><span className="brand-mark" aria-hidden="true">P<span /></span><span>POSEIDON<small>{presentationLabel(language, 'maritimePlatform')}</small></span></a>
        <div className="sidebar-rule"/>
        <div className="sidebar-section">{presentationLabel(language, 'workspaceCaption')}</div>
        <a className="nav-item" href="#main" aria-current="page"><span aria-hidden="true">◈</span>{t.page}</a>
        <a className="nav-item" href="/app">{localizedText(language, "pairs.m_dcf657183d7745b8")}</a>
        <a className="nav-item" href="/agents">{localizedText(language, "pairs.m_7c98a73bc97d1a74")}</a>
        <div className="sidebar-bottom"><span className="compass" aria-hidden="true">✧</span><p>{t.phase}</p><small><bdi dir="ltr">v0.1</bdi> · {presentationLabel(language, 'developmentCaption')}</small></div>
      </aside>
      <div className="workspace">
        <header className="topbar"><span className="breadcrumb">POSEIDON <span aria-hidden="true">/</span> {t.page}</span><div className="topbar-actions"><AppearanceControls language={language} /><span className="environment"><span />{t.environment}</span><LanguageSelector locale={language} onChange={setLanguage} /></div></header>
        <main id="main" tabIndex={-1}>
          <div className="page-heading"><p className="eyebrow">{t.eyebrow}</p><h1>{t.heading}</h1><p className="intro">{t.intro}</p></div>
          <section className={`connection-card connection-${connection}`} aria-labelledby="connection-title" data-testid="connection-card" data-state={connection}>
            <div className="connection-top"><span className={`connection-icon ${connection === 'loading' ? 'spinning' : ''}`} aria-hidden="true">{connection === 'loading' ? '◌' : connection === 'available' ? '↗' : '!'}</span><span className="card-tag">API / HTTP</span></div>
            <div role="status" aria-live="polite" aria-atomic="true"><h2 id="connection-title">{t[connection]}</h2><p className="connection-detail">{connection === 'loading' ? t.loadingDetail : connection === 'available' ? t.availableDetail : t.errorDetail}</p></div>
            <div className="endpoint"><span>{t.endpoint}</span><code>/api/v1/system/status</code></div>
            <Button className="primary-button" type="button" onClick={retry} disabled={connection === 'loading'} data-testid="connection-retry">{connection === 'loading' ? t.loading : connection === 'error' ? t.retry : t.check}<span aria-hidden="true">↻</span></Button>
          </section>
          <div className="context-grid"><section className="scope-note" aria-labelledby="scope-title"><span className="info-mark" aria-hidden="true">i</span><div><h2 id="scope-title">{t.technical}</h2><p>{t.scope}</p></div></section><section className="contract" aria-labelledby="contract-title"><h2 id="contract-title">{t.protocol}</h2><p>{t.expected}</p><code>poseidon <span>/</span> available</code></section></div>
          <div className="phase-note"><span aria-hidden="true">↳</span><p><strong>{t.phase}.</strong> {t.phaseDetail}</p></div>
        </main>
        <footer><span>{t.footer}</span><span className="footer-marker" aria-hidden="true"><bdi dir="ltr">01</bdi> / {presentationLabel(language, 'foundationCaption')}</span></footer>
      </div>
    </div>);
}
