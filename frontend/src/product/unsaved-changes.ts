import { useLayoutEffect, useRef } from 'react'

// DEV-021 / 65.03 §2 (paragraph 64) and UX-038 (paragraph 619): the shell must know
// whether the current surface holds an unsaved typed form BEFORE it starts a context
// switch request. Each guarded workspace reports its own state; the shell never reads
// form values, never persists drafts and never discards on a workspace's behalf. The
// workspace's own guard (Stay / Discard) remains the only place a draft is released.
export type UnsavedState = 'clean' | 'dirty' | 'saving'

const sources = new Set<{ read: () => UnsavedState }>()

// 'saving' wins over 'dirty': an in-flight write has an unknown outcome and must not be
// abandoned by a context change (65.03 paragraph 65).
export function currentUnsavedState(): UnsavedState {
  let result: UnsavedState = 'clean'
  for (const source of sources) {
    let value: UnsavedState
    try { value = source.read() } catch { value = 'dirty' }
    if (value === 'saving') return 'saving'
    if (value === 'dirty') result = 'dirty'
  }
  return result
}

export function registerUnsavedSource(read: () => UnsavedState): () => void {
  const entry = { read }
  sources.add(entry)
  return () => { sources.delete(entry) }
}

export function useUnsavedChangesSource(read: () => UnsavedState) {
  const latest = useRef(read)
  useLayoutEffect(() => { latest.current = read })
  useLayoutEffect(() => registerUnsavedSource(() => latest.current()), [])
}

export const unsavedState = (dirty: boolean, saving = false): UnsavedState => saving ? 'saving' : dirty ? 'dirty' : 'clean'
