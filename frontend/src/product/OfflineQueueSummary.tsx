import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { Badge } from '../components/ui/badge';
import { isGuid } from './identifiers';
import { timestamp } from './date-validation';
import './offline-enterprise.css';
export type QueueCapture = {
    commandId: string;
    revision: number;
    body: string;
    outcome?: {
        result: 'Committed' | 'MaterialConflict';
        serverVersion: number;
        revision: number;
        parentCommandId: string | null;
    };
};
// Display projection only. Never rewrites the frozen envelope or supplies values
// to replay/resolution. Domain fields and device/key identifiers stay undisclosed.
export function captureDisplayMetadata(row: QueueCapture): {
    baseVersion: number;
    capturedAt: string;
    parentCommandId: string | null;
} | null {
    try {
        if (!isGuid(row.commandId) || row.body.length > 16384)
            return null;
        const value: unknown = JSON.parse(row.body);
        if (!value || typeof value !== 'object' || Array.isArray(value))
            return null;
        const metadata = value as Record<string, unknown>;
        if (metadata.commandId !== row.commandId || typeof metadata.baseVersion !== 'number' || !Number.isSafeInteger(metadata.baseVersion) || metadata.baseVersion < 1 || metadata.baseVersion > 4294967295 ||
            !timestamp(metadata.capturedAt) || typeof metadata.capturedAt !== 'string' || metadata.parentCommandId === row.commandId || metadata.parentCommandId !== undefined && !isGuid(metadata.parentCommandId))
            return null;
        return { baseVersion: metadata.baseVersion, capturedAt: metadata.capturedAt, parentCommandId: typeof metadata.parentCommandId === 'string' ? metadata.parentCommandId : null };
    }
    catch {
        return null;
    }
}
export function OfflineQueueSummary({ captures, language }: {
    captures: readonly QueueCapture[];
    language: AppLocale;
}) {
    const en = language === 'en';
    const pending = captures.filter(row => !row.outcome).length;
    const conflicts = captures.filter(row => row.outcome?.result === 'MaterialConflict').length;
    const committed = captures.filter(row => row.outcome?.result === 'Committed').length;
    return <section className="offline-queue-summary" aria-label={localizedText(en, "pairs.m_827dd2ecdf1b62b3")}>
    <p>{localizedText(en, "pairs.m_9adb1052564fc370")}</p>
    <dl><div><dt>{localizedText(en, "pairs.m_5b15a308b84c4ad7")}</dt><dd>{captures.length}</dd></div><div><dt>{localizedText(en, "pairs.m_e2f96a34d108961d")}</dt><dd>{pending}</dd></div><div><dt>{localizedText(en, "pairs.m_b73b66dff22639d0")}</dt><dd>{conflicts}</dd></div><div><dt>{localizedText(en, "pairs.m_c76cbc1d54915e94")}</dt><dd>{committed}</dd></div></dl>
  </section>;
}
export function OfflineCaptureMetadata({ capture, language }: {
    capture: QueueCapture;
    language: AppLocale;
}) {
    const en = language === 'en', metadata = captureDisplayMetadata(capture), unknown = localizedText(en, "pairs.m_a5a7675bd7000dbd");
    return <div className="offline-capture-metadata">
    <Badge tone="neutral">{capture.outcome?.result === 'Committed' ? (localizedText(en, "pairs.m_65a39fd53af59b43")) : capture.outcome?.result === 'MaterialConflict' ? (localizedText(en, "pairs.m_33ca4856970bc649")) : (localizedText(en, "pairs.m_9600ab584f836c64"))}</Badge>
    <dl><div><dt>{localizedText(en, "pairs.m_c3012a7e100a2f1d")}</dt><dd><code>{capture.commandId}</code></dd></div><div><dt>{localizedText(en, "pairs.m_84e37cfff02104c4")}</dt><dd>{metadata?.baseVersion ?? unknown}</dd></div><div><dt>{localizedText(en, "pairs.m_fc7ceb2b0114d99a")}</dt><dd>{metadata ? <time dateTime={metadata.capturedAt}>{metadata.capturedAt}</time> : unknown}</dd></div><div><dt>{localizedText(en, "pairs.m_8a40befa11737b95")}</dt><dd>{capture.revision}</dd></div></dl>
    {!metadata && <p>{localizedText(en, "pairs.m_36fdfd869990ff0b")}</p>}
    {metadata?.parentCommandId && <p>{localizedText(en, "pairs.m_b05ca818504ff98f")}: <code>{metadata.parentCommandId}</code></p>}
  </div>;
}
