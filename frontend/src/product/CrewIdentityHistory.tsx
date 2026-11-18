import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useEffect, useState } from 'react';
import { request } from './api';
import { crewPath } from './crew-api';
// Safe metadata only. No historic identity values or ciphertext are fetched or cached.
export function CrewIdentityHistory({ vessel, assignment, language }: {
    vessel: string;
    assignment: string;
    language: AppLocale;
}) {
    const [open, setOpen] = useState(false);
    const [items, setItems] = useState<{
        revision: number;
        operation: string;
        savedAt: string;
    }[] | null>(null);
    const [denied, setDenied] = useState(false);
    const [available, setAvailable] = useState(navigator.onLine && document.visibilityState !== 'hidden');
    const [generation, setGeneration] = useState(0);
    useEffect(() => {
        const withdraw = () => { setItems(null); setDenied(false); setAvailable(false); };
        const refresh = () => {
            setItems(null);
            setDenied(false);
            setAvailable(navigator.onLine && document.visibilityState !== 'hidden');
            setGeneration(g => g + 1);
        };
        const visibility = () => { if (document.visibilityState === 'hidden')
            withdraw();
        else
            refresh(); };
        window.addEventListener('offline', withdraw);
        window.addEventListener('online', refresh);
        document.addEventListener('visibilitychange', visibility);
        return () => { window.removeEventListener('offline', withdraw); window.removeEventListener('online', refresh); document.removeEventListener('visibilitychange', visibility); };
    }, []);
    const resetScope1 = [open, vessel, assignment, available, generation] as const;
    const [previousScope1, setPreviousScope1] = useState(resetScope1);
    // Withdraw stale state in the same render that changes the request scope.
    if (!Object.is(previousScope1[0], open) || !Object.is(previousScope1[1], vessel) || !Object.is(previousScope1[2], assignment) || !Object.is(previousScope1[3], available) || !Object.is(previousScope1[4], generation)) {
        setPreviousScope1(resetScope1);
        setItems(null);
        setDenied(false);
    }
    useEffect(() => {
        if (!open || !available)
            return;
        const c = new AbortController();
        let active = true;
        request<{
            items: {
                revision: number;
                operation: string;
                savedAt: string;
            }[];
        }>(`${crewPath(vessel)}/${encodeURIComponent(assignment)}/person/history`, c.signal)
            .then(value => { if (active)
            setItems(value.items); }).catch(() => { if (active) {
            setItems(null);
            setDenied(true);
        } });
        return () => { active = false; c.abort(); };
    }, [open, vessel, assignment, available, generation]);
    return <section><button type="button" aria-expanded={open} onClick={() => setOpen(v => !v)}>{localizedText(language, "pairs.m_eef335e2a0bc14b4")}</button>
    {open && available && <><p>{localizedText(language, "pairs.m_70a01d06afc42682")}</p>
      {denied && <p role="alert">{localizedText(language, "pairs.m_6833150f1d363663")}</p>}
      {items && <ol>{items.map(item => <li key={item.revision}>{item.revision} · {item.operation} · {item.savedAt}</li>)}</ol>}</>}
  </section>;
}
