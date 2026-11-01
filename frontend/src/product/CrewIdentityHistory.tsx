import { useEffect, useState } from 'react'
import { request } from './api'
import { crewPath } from './crew-api'

// Safe metadata only. No historic identity values or ciphertext are fetched or cached.
export function CrewIdentityHistory({ vessel, assignment, language }: { vessel: string; assignment: string; language: 'tr' | 'en' }) {
  const [open, setOpen] = useState(false)
  const [items, setItems] = useState<{ revision: number; operation: string; savedAt: string }[] | null>(null)
  const [denied, setDenied] = useState(false)
  const [available, setAvailable] = useState(navigator.onLine && document.visibilityState !== 'hidden')
  const [generation, setGeneration] = useState(0)
  useEffect(() => {
    const withdraw = () => { setItems(null); setDenied(false); setAvailable(false) }
    const refresh = () => {
      setItems(null); setDenied(false)
      setAvailable(navigator.onLine && document.visibilityState !== 'hidden'); setGeneration(g => g + 1)
    }
    const visibility = () => { if (document.visibilityState === 'hidden') withdraw(); else refresh() }
    window.addEventListener('offline', withdraw); window.addEventListener('online', refresh); document.addEventListener('visibilitychange', visibility)
    return () => { window.removeEventListener('offline', withdraw); window.removeEventListener('online', refresh); document.removeEventListener('visibilitychange', visibility) }
  }, [])
  useEffect(() => {
    setItems(null); setDenied(false)
    if (!open || !available) return
    const c = new AbortController(); let active = true
    request<{ items: { revision: number; operation: string; savedAt: string }[] }>(`${crewPath(vessel)}/${encodeURIComponent(assignment)}/person/history`, c.signal)
      .then(value => { if (active) setItems(value.items) }).catch(() => { if (active) { setItems(null); setDenied(true) } })
    return () => { active = false; c.abort() }
  }, [open, vessel, assignment, available, generation])
  return <section><button type="button" aria-expanded={open} onClick={() => setOpen(v => !v)}>{language === 'tr' ? 'Kimlik işlem geçmişi' : 'Identity action history'}</button>
    {open && available && <><p>{language === 'tr' ? 'Geçmiş özel alan değerleri kullanılamıyor.' : 'Historical private values are unavailable.'}</p>
      {denied && <p role="alert">{language === 'tr' ? 'Kimlik geçmişi kullanılamıyor.' : 'Identity history unavailable.'}</p>}
      {items && <ol>{items.map(item => <li key={item.revision}>{item.revision} · {item.operation} · {item.savedAt}</li>)}</ol>}</>}
  </section>
}
