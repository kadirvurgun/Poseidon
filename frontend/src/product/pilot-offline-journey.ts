// S10 WP-C DEV-066 (18.01 sheet:Backlog/row:67) full pilot offline journey: one reconnect driver over the three
// pilot flows (certificate renewal draft, safety event capture DEV-060, work order evidence DEV-057). Each flow
// keeps its own encrypted durable queue and signed idempotent replay; this driver only sequences replays on
// reconnect and projects an honest ledger. It never deletes, never resolves a conflict and never reports a
// queued capture as saved. Data-loss invariant: every capture retained before a sync is still retained after it
// (lost === 0); a dropped connection leaves the capture pending, a material conflict waits for a human decision.
// Dependency-free so the browser harness can load the exact source.
export type PilotFlow = 'certificateRenewal' | 'safetyEvent' | 'workOrderEvidence'
export type PilotOutcome = { result: string; parentCommandId?: string | null }
export type PilotRow = { commandId: string; body: string; outcome?: PilotOutcome }
export type PilotFlowAdapter = { flow: PilotFlow; captures(): Promise<PilotRow[]>; replay(row: PilotRow, signal: AbortSignal): Promise<PilotOutcome> }
export type PilotReplayFailure = 'offline' | 'conflict' | 'reauthorize' | 'rejected' | 'retry'
export type PilotLedger = { flow: PilotFlow; retained: number; committed: number; conflicts: number; pending: number; attempted: number; failures: Partial<Record<PilotReplayFailure, number>>; lost: number }

export function classifyPilotReplayFailure(error: unknown): PilotReplayFailure {
  if (error instanceof TypeError) return 'offline'
  const status = error && typeof error === 'object' && 'status' in error ? Number((error as { status: unknown }).status) : NaN
  const code = error && typeof error === 'object' && 'code' in error ? String((error as { code: unknown }).code) : error instanceof Error ? error.message : ''
  if (code === 'reauthorization_required_data_retained' || code === 'key_lost_data_retained' || status === 401 || code === 'offline_recovery_required') return 'reauthorize'
  if (!Number.isFinite(status) || status === 0) return 'offline'
  if (status === 409 && (code === 'offline_conflict_changed' || code === 'offline_resolution_exists')) return 'conflict'
  if (status === 409 && code === 'offline_retry' || status >= 500) return 'retry'
  return 'rejected'
}
const parent = (row: PilotRow) => { try { const v = JSON.parse(row.body) as { parentCommandId?: unknown }; return typeof v.parentCommandId === 'string' ? v.parentCommandId : null } catch { return null } }
// Terminal = Committed/ServerRetained receipt. A MaterialConflict stays open until a child resolution is terminal.
export function projectPilotRows(flow: PilotFlow, rows: readonly PilotRow[]): Omit<PilotLedger, 'attempted' | 'failures' | 'lost'> {
  const resolved = new Set(rows.filter(r => r.outcome && r.outcome.result !== 'MaterialConflict').map(parent).filter((p): p is string => p !== null))
  return { flow, retained: rows.length,
    committed: rows.filter(r => r.outcome && r.outcome.result !== 'MaterialConflict').length,
    conflicts: rows.filter(r => r.outcome?.result === 'MaterialConflict' && !resolved.has(r.commandId)).length,
    pending: rows.filter(r => !r.outcome).length }
}

// Replays sequentially (flow order, then capture order) only while the device reports a connection.
export async function syncPilotJourney(adapters: readonly PilotFlowAdapter[], signal: AbortSignal, online: () => boolean): Promise<PilotLedger[]> {
  const ledgers: PilotLedger[] = []
  for (const adapter of adapters) {
    const before = await adapter.captures(), ids = new Set(before.map(r => r.commandId)), failures: Partial<Record<PilotReplayFailure, number>> = {}
    let attempted = 0
    for (const row of before.filter(r => !r.outcome)) {
      if (signal.aborted || !online()) { failures.offline = (failures.offline ?? 0) + 1; continue }
      attempted++
      try { await adapter.replay(row, signal) }
      catch (error) {
        const kind = classifyPilotReplayFailure(error); failures[kind] = (failures[kind] ?? 0) + 1
        if (kind === 'reauthorize') break
      }
    }
    const after = await adapter.captures()
    const lost = [...ids].filter(id => !after.some(r => r.commandId === id)).length
    ledgers.push({ ...projectPilotRows(adapter.flow, after), attempted, failures, lost })
  }
  return ledgers
}
