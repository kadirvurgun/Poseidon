import { localizedText } from "../i18n/messages";
import type { QueueDisplayState } from './sync-operations';

// DEV-033/DEV-034 shared queue notice: waiting behind an earlier change, or rejected
// because the server holds different content for the key. Never a saved state.
export function SyncQueueNotice({ state, en }: { state: QueueDisplayState | undefined; en: boolean }) {
    if (state === 'blocked')
        return <p role="status">{localizedText(en, "pairs.m_e9537921034aa833")}</p>;
    if (state === 'rejected')
        return <p role="alert">{localizedText(en, "pairs.m_4aff1a1251fdec46")}</p>;
    return null;
}
