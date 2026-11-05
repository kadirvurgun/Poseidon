import { useEffect, useRef } from 'react'
import { editorCopy } from './editor-copy'
import type { Language } from './editor-copy'
import { Button } from '../components/ui/button'
import { EnterpriseActions } from './EnterprisePatterns'

export function DraftConfirmation({ language, onStay, onDiscard, description }: { language: Language; onStay: () => void; onDiscard: () => void; description?: string }) {
  const dialog = useRef<HTMLDialogElement>(null)
  const t = editorCopy[language]
  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null
    const node = dialog.current
    node?.showModal()
    return () => { node?.close(); previous?.focus() }
  }, [])
  return <dialog ref={dialog} aria-labelledby="draft-title" aria-describedby={description ? 'draft-description' : undefined} onCancel={e => { e.preventDefault(); onStay() }}>
    <h2 id="draft-title">{t.discard}</h2>
    {description && <p id="draft-description">{description}</p>}
    <EnterpriseActions aria-label={language === 'en' ? 'Unsaved draft actions' : 'Kaydedilmemiş taslak işlemleri'}><Button variant="outline" autoFocus onClick={onStay}>{t.stay}</Button><Button variant="destructive" onClick={onDiscard}>{t.leave}</Button></EnterpriseActions>
  </dialog>
}
