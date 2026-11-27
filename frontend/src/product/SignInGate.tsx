import { useEffect, useState, type ReactNode } from 'react';
import { Anchor, BookOpen, FileCheck2, LogIn, ShieldCheck, Ship, Waves } from 'lucide-react';
import { LanguageSelector } from '../components/LanguageSelector';
import { Button } from '../components/ui/button';
import { useAppLanguage } from '../i18n';
import { localizedCopy } from '../i18n/messages';
import { AppearanceControls } from '../theme/AppearanceControls';
import { session as readSession } from './api';
import './sign-in.css';

const copy = localizedCopy('SignIn_tsx_copy');
// Pages that must stay reachable without a session (invitation acceptance, help).
const PUBLIC_PATHS = /^\/app\/(accept-invitation|help)(?:\/|$)/;

export function signInHref(path = window.location.pathname) {
    const target = path === '/app' || path.startsWith('/app/') ? path : '/app';
    return `/api/v1/auth/login?returnUrl=${encodeURIComponent(target)}`;
}

type GateState = 'checking' | 'signed-out' | 'open';

// A signed-out visitor sees one full-page sign-in screen instead of an empty workspace shell.
// Presentation only: every API still enforces authentication. Unknown/offline/error states
// open the page so offline work and the pages' own error handling keep working.
export function SignInGate({ children }: { children: ReactNode }) {
    const [state, setState] = useState<GateState>(() => PUBLIC_PATHS.test(window.location.pathname) || !navigator.onLine ? 'open' : 'checking');
    useEffect(() => {
        if (state !== 'checking')
            return;
        let live = true;
        const controller = new AbortController();
        const timer = setTimeout(() => controller.abort(), 8000);
        readSession(controller.signal)
            // Only an explicit signed-out answer shows the sign-in screen; anything unexpected opens the page.
            .then(s => { if (live) setState(s?.authenticated === false && s.setupRequired !== true && s.invitationSession !== true ? 'signed-out' : 'open'); })
            .catch(() => { if (live) setState('open'); })
            .finally(() => clearTimeout(timer));
        return () => { live = false; clearTimeout(timer); controller.abort(); };
    }, [state]);
    if (state === 'open')
        return <>{children}</>;
    return <SignInScreen checking={state === 'checking'}/>;
}

export function SignInScreen({ checking = false }: { checking?: boolean }) {
    const [language, setLanguage] = useAppLanguage();
    const t = copy[language];
    const auth = new URLSearchParams(window.location.search).get('auth');
    useEffect(() => { document.title = `POSEIDON · ${t.signin}`; }, [t]);
    const points: [typeof Ship, string][] = [[Ship, t.pointOps], [FileCheck2, t.pointCompliance], [ShieldCheck, t.pointAccess]];
    return <div className="signin-page" data-state={checking ? 'checking' : 'signed-out'}>
      <section className="signin-hero">
        <div className="signin-brand">
          <span className="signin-mark" aria-hidden="true"><Waves/></span>
          <span><strong>POSEIDON</strong><small>{t.eyebrow}</small></span>
        </div>
        <div className="signin-hero-body">
          <p className="signin-headline">{t.headline}</p>
          <ul>{points.map(([Icon, label]) => <li key={label}><span aria-hidden="true"><Icon/></span>{label}</li>)}</ul>
        </div>
        <svg className="signin-sea" viewBox="0 0 1200 240" preserveAspectRatio="none" aria-hidden="true">
          <path d="M0 120 C 200 60 400 180 600 120 S 1000 60 1200 120 V240 H0 Z"/>
          <path d="M0 160 C 220 110 420 210 640 160 S 1020 110 1200 160 V240 H0 Z"/>
          <path d="M0 200 C 240 160 460 240 680 200 S 1040 170 1200 200 V240 H0 Z"/>
        </svg>
      </section>
      <main className="signin-panel" id="main" tabIndex={-1}>
        <div className="signin-tools"><AppearanceControls language={language}/><LanguageSelector locale={language} onChange={setLanguage}/></div>
        <div className="signin-card">
          <span className="signin-card-mark" aria-hidden="true"><Anchor/></span>
          <h1>{t.welcome}</h1>
          <p className="signin-lead">{t.lead}</p>
          {auth && <p role="alert" className="signin-alert" data-auth={auth === 'request_limit' ? 'request_limit' : 'failed'}>{auth === 'request_limit' ? t.limited : t.failed}</p>}
          {checking
            ? <p role="status" className="signin-checking"><span className="signin-spinner" aria-hidden="true"/>{t.checking}</p>
            : <Button asChild size="lg" className="signin-button"><a href={signInHref()}><LogIn aria-hidden="true"/>{t.signin}</a></Button>}
          <p className="signin-secure"><ShieldCheck aria-hidden="true"/><span>{t.secure}</span></p>
        </div>
        <footer className="signin-footer"><span>{t.footer}</span><a href="/app/help"><BookOpen aria-hidden="true"/>{t.help}</a></footer>
      </main>
    </div>;
}
