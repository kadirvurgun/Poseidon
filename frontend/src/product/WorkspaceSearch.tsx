import { useEffect, useRef, useState } from 'react'
import { ApiError, request } from './api'
import type { components } from '../generated/api'
import { readLocation } from './workspace-location'
type Item = components['schemas']['SearchItemDto']
type Props = { language: 'tr' | 'en'; onNavigate: (href: string) => void; onClose: () => void }
export function WorkspaceSearch({ language, onNavigate, onClose }: Props) {
  const en = language === 'en'
  const dialog = useRef<HTMLDialogElement>(null)
  const [query, setQuery] = useState('')
  const [search, setSearch] = useState('')
  const [offset, setOffset] = useState(0)
  const [attempt, setAttempt] = useState(0)
  const [state, setState] = useState<{ items: Item[]; hasMore: boolean } | null>(null)
  const [failure, setFailure] = useState<number | null>(null)
  useEffect(() => {
    const previous = document.activeElement as HTMLElement | null
    const node = dialog.current
    node?.showModal()
    return () => { node?.close(); if (previous?.isConnected) previous.focus() }
  }, [])
  useEffect(() => {
    const controller = new AbortController(); let active = true
    const timeout = setTimeout(() => controller.abort(), 10000)
    request<{ items: Item[]; hasMore: boolean }>(`workspace/search?q=${encodeURIComponent(search)}&offset=${offset}&limit=25`, controller.signal)
      .then(value => {
        if (!value || !Array.isArray(value.items) || value.items.length > 25 || typeof value.hasMore !== 'boolean' || value.items.some(item =>
          !item || typeof item.key !== 'string' || typeof item.label !== 'string' || typeof item.href !== 'string' || readLocation(item.href).invalid ||
          !['Destination', 'Ship', 'Tug'].includes(item.kind) ||
          (item.kind === 'Destination' ? !['/app/today', '/app/companies', '/app/fleet'].includes(item.href) : !/^\/app\/fleet\/[a-fA-F0-9-]{36}$/.test(item.href)))) throw new ApiError(502, 'invalid_contract')
        if (active) setState(value)
      }).catch(error => { if (active) setFailure(error instanceof ApiError ? error.status : 0) })
    return () => { active = false; clearTimeout(timeout); controller.abort() }
  }, [search, offset, attempt])
  const reload = (next: number) => { setState(null); setFailure(null); setOffset(next); setAttempt(v => v + 1) }
  return <dialog className="workspace-search" ref={dialog} aria-labelledby="workspace-search-title" onCancel={e => { e.preventDefault(); onClose() }}>
    <h2 id="workspace-search-title">{en ? 'Search workspace' : 'Çalışma alanında ara'}</h2>
    <button onClick={onClose}>{en ? 'Close' : 'Kapat'}</button>
    <form onSubmit={e => { e.preventDefault(); setSearch(query.trim()); reload(0) }}>
      <label htmlFor="workspace-query">{en ? 'Destinations and vessels' : 'Sayfalar ve araçlar'}</label>
      <input autoFocus id="workspace-query" type="search" maxLength={100} value={query} onChange={e => setQuery(e.target.value)} />
      <button>{en ? 'Search' : 'Ara'}</button>
    </form>
    {failure !== null ? <div role="alert"><p>{failure === 401 ? (en ? 'Sign in to continue.' : 'Devam etmek için oturum açın.') : failure === 403 ? (en ? 'Search is not permitted.' : 'Arama yetkiniz yok.') : (en ? 'Search unavailable. Results are unverified.' : 'Aramaya ulaşılamıyor. Sonuçlar doğrulanmadı.')}</p><button onClick={() => reload(offset)}>{en ? 'Retry' : 'Tekrar dene'}</button></div> : !state ? <p role="status">{en ? 'Loading…' : 'Yükleniyor…'}</p> : <div aria-live="polite">
      {!state.items.length && <p>{en ? 'No records found.' : 'Kayıt bulunamadı.'}</p>}
      <ul>{state.items.map(item => <li key={item.key}><a href={item.href} onClick={e => { e.preventDefault(); onNavigate(item.href) }}>{item.kind === 'Destination' ? (item.key === 'fleet' ? (en ? 'Fleet' : 'Filo') : (en ? 'Companies' : 'Şirketler')) : item.label} {item.kind === 'Ship' ? (en ? '· Ship' : '· Gemi') : item.kind === 'Tug' ? (en ? '· Tug' : '· Römorkör') : ''}</a></li>)}</ul>
      <div className="form-actions"><button disabled={!offset} onClick={() => reload(Math.max(0, offset - 25))}>{en ? 'Previous' : 'Önceki'}</button><button disabled={!state.hasMore} onClick={() => reload(offset + 25)}>{en ? 'Next' : 'Sonraki'}</button></div>
    </div>}
  </dialog>
}
