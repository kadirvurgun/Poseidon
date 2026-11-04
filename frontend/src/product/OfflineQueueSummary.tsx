import { Badge } from '../components/ui/badge'
import { isGuid } from './identifiers'
import { timestamp } from './date-validation'
import './offline-enterprise.css'

export type QueueCapture = { commandId: string; revision: number; body: string; outcome?: { result: 'Committed' | 'MaterialConflict'; serverVersion: number; revision: number; parentCommandId: string | null } }

// Display projection only. Never rewrites the frozen envelope or supplies values
// to replay/resolution. Domain fields and device/key identifiers stay undisclosed.
export function captureDisplayMetadata(row: QueueCapture): { baseVersion: number; capturedAt: string; parentCommandId: string | null } | null {
  try {
    if (!isGuid(row.commandId) || row.body.length > 16384) return null
    const value: unknown = JSON.parse(row.body)
    if (!value || typeof value !== 'object' || Array.isArray(value)) return null
    const metadata = value as Record<string, unknown>
    if (metadata.commandId !== row.commandId || typeof metadata.baseVersion !== 'number' || !Number.isSafeInteger(metadata.baseVersion) || metadata.baseVersion < 1 || metadata.baseVersion > 4294967295 ||
      !timestamp(metadata.capturedAt) || typeof metadata.capturedAt !== 'string' || metadata.parentCommandId === row.commandId || metadata.parentCommandId !== undefined && !isGuid(metadata.parentCommandId)) return null
    return { baseVersion: metadata.baseVersion, capturedAt: metadata.capturedAt, parentCommandId: typeof metadata.parentCommandId === 'string' ? metadata.parentCommandId : null }
  } catch { return null }
}

export function OfflineQueueSummary({ captures, language }: { captures: readonly QueueCapture[]; language: 'tr' | 'en' }) {
  const en = language === 'en'
  const pending = captures.filter(row => !row.outcome).length
  const conflicts = captures.filter(row => row.outcome?.result === 'MaterialConflict').length
  const committed = captures.filter(row => row.outcome?.result === 'Committed').length
  return <section className="offline-queue-summary" aria-label={en ? 'Unlocked local queue overview' : 'Açık yerel kuyruk özeti'}>
    <p>{en ? 'This authorized local context only. Unsent or uncertain captures require an explicit server check; a receipt is not operational approval.' : 'Yalnız bu yetkili yerel bağlam. Gönderilmemiş veya belirsiz kayıtlar açık sunucu sorgusu gerektirir; alındı operasyonel onay değildir.'}</p>
    <dl><div><dt>{en ? 'Stored captures' : 'Saklanan kayıtlar'}</dt><dd>{captures.length}</dd></div><div><dt>{en ? 'Unsent / outcome unknown' : 'Gönderilmemiş / sonuç belirsiz'}</dt><dd>{pending}</dd></div><div><dt>{en ? 'Material conflicts' : 'Maddi çakışmalar'}</dt><dd>{conflicts}</dd></div><div><dt>{en ? 'Server receipts' : 'Sunucu alındıları'}</dt><dd>{committed}</dd></div></dl>
  </section>
}

export function OfflineCaptureMetadata({ capture, language }: { capture: QueueCapture; language: 'tr' | 'en' }) {
  const en = language === 'en', metadata = captureDisplayMetadata(capture), unknown = en ? 'Unknown' : 'Bilinmiyor'
  return <div className="offline-capture-metadata">
    <Badge tone="neutral">{capture.outcome?.result === 'Committed' ? (en ? 'Server receipt retained' : 'Sunucu alındısı korundu') : capture.outcome?.result === 'MaterialConflict' ? (en ? 'Human review required' : 'İnsan incelemesi gerekli') : (en ? 'Pending server confirmation' : 'Sunucu teyidi bekliyor')}</Badge>
    <dl><div><dt>{en ? 'Client command UUID' : 'İstemci komut UUID'}</dt><dd><code>{capture.commandId}</code></dd></div><div><dt>{en ? 'Base version' : 'Temel sürüm'}</dt><dd>{metadata?.baseVersion ?? unknown}</dd></div><div><dt>{en ? 'Captured at' : 'Yakalama zamanı'}</dt><dd>{metadata ? <time dateTime={metadata.capturedAt}>{metadata.capturedAt}</time> : unknown}</dd></div><div><dt>{en ? 'Local revision' : 'Yerel sürüm'}</dt><dd>{capture.revision}</dd></div></dl>
    {!metadata && <p>{en ? 'Capture metadata unavailable. Keep the original encrypted record and check its exact outcome.' : 'Yakalama metaverisi kullanılamıyor. Özgün şifreli kaydı koruyun ve kesin sonucunu sorgulayın.'}</p>}
    {metadata?.parentCommandId && <p>{en ? 'Explicit resolution of command' : 'Komutun açık çözümü'}: <code>{metadata.parentCommandId}</code></p>}
  </div>
}
