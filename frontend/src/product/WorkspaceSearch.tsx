import { useEffect, useRef, useState } from 'react'
import { ApiError } from './api'
import { searchWorkspace, searchMaxOffset } from './workspace-search-api'
import type { SearchPage } from './workspace-search-api'
type Props = { language: 'tr' | 'en'; onNavigate: (href: string) => void; onClose: () => void }
export function WorkspaceSearch({ language, onNavigate, onClose }: Props) {
  const en = language === 'en'
  const destinationLabels: Record<string, string> = { today: en ? 'Today' : 'Bugün', fleet: en ? 'Fleet' : 'Filo', companies: en ? 'Companies' : 'Şirketler', sources: en ? 'Source drafts' : 'Kaynak taslakları', requirements: en ? 'Requirement drafts' : 'Gereksinim taslakları', audit: en ? 'Action history' : 'İşlem geçmişi', 'tug-command': en ? 'Tug operations' : 'Römorkör operasyonları', 'tug-job-board': en ? 'Tug job board' : 'Römorkör iş panosu', 'tug-job-intake': en ? 'Tug job intake' : 'Römorkör iş kaydı' }
  const dialog = useRef<HTMLDialogElement>(null)
  const [query, setQuery] = useState('')
  const [search, setSearch] = useState('')
  const [offset, setOffset] = useState(0)
  const [attempt, setAttempt] = useState(0)
  const [state, setState] = useState<SearchPage | null>(null)
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
    searchWorkspace(search, offset, controller.signal)
      .then(value => {
        if (active) { setFailure(null); setState(value) }
      }).catch(error => { if (active) { setState(null); setFailure(error instanceof ApiError ? error.status : 0) } })
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
      <ul>{state.items.map(item => <li key={item.key}><a href={item.href} onClick={e => { e.preventDefault(); onNavigate(item.href) }}>{item.kind === 'Destination' ? destinationLabels[item.key] ?? item.label : item.label} {item.kind === 'Ship' ? (en ? '· Ship' : '· Gemi') : item.kind === 'Tug' ? (en ? '· Tug' : '· Römorkör') : ''}</a></li>)}</ul>
      <div className="form-actions"><button disabled={!offset} onClick={() => reload(Math.max(0, offset - 25))}>{en ? 'Previous' : 'Önceki'}</button><button disabled={!state.hasMore || offset >= searchMaxOffset} onClick={() => reload(offset + 25)}>{en ? 'Next' : 'Sonraki'}</button></div>
    </div>}
  </dialog>
}
