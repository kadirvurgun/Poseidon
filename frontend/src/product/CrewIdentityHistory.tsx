import { useEffect, useState } from 'react'
import { request } from './api'
import { crewPath } from './crew-api'

// Safe metadata only. No historic identity values or ciphertext are fetched or cached.
export function CrewIdentityHistory({ vessel, assignment, language }: { vessel: string; assignment: string; language: 'tr' | 'en' }) {
  const [open, setOpen] = useState(false)
  const [items, setItems] = useState<{ revision: number; operation: string; savedAt: string }[] | null>(null)
  const [denied, setDenied] = useState(false)
  useEffect(() => {
    setItems(null); setDenied(false)
    if (!open) return
    const c = new AbortController(); let active = true
    request<{ items: { revision: number; operation: string; savedAt: string }[] }>(`${crewPath(vessel)}/${encodeURIComponent(assignment)}/person/history`, c.signal)
      .then(value => { if (active) setItems(value.items) }).catch(() => { if (active) { setItems(null); setDenied(true) } })
    return () => { active = false; c.abort() }
  }, [open, vessel, assignment])
  return <section><button type="button" aria-expanded={open} onClick={() => setOpen(v => !v)}>{language === 'tr' ? 'Kimlik işlem geçmişi' : 'Identity action history'}</button>
    {open && <><p>{language === 'tr' ? 'Geçmiş özel alan değerleri kullanılamıyor.' : 'Historical private values are unavailable.'}</p>
      {denied && <p role="alert">{language === 'tr' ? 'Kimlik geçmişi kullanılamıyor.' : 'Identity history unavailable.'}</p>}
      {items && <ol>{items.map(item => <li key={item.revision}>{item.revision} · {item.operation} · {item.savedAt}</li>)}</ol>}</>}
  </section>
}
