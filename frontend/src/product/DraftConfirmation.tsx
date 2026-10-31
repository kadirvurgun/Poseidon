import { useEffect, useRef } from 'react'
import { editorCopy } from './editor-copy'
import type { Language } from './editor-copy'

export function DraftConfirmation({ language, onStay, onDiscard }: { language: Language; onStay: () => void; onDiscard: () => void }) {
  const dialog = useRef<HTMLDialogElement>(null)
  const t = editorCopy[language]
  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null
    const node = dialog.current
    node?.showModal()
    return () => { node?.close(); previous?.focus() }
  }, [])
  return <dialog ref={dialog} aria-labelledby="draft-title" onCancel={e => { e.preventDefault(); onStay() }}>
    <h2 id="draft-title">{t.discard}</h2>
    <div className="form-actions"><button autoFocus onClick={onStay}>{t.stay}</button><button onClick={onDiscard}>{t.leave}</button></div>
  </dialog>
}
