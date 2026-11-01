import { useEffect, useState } from 'react'
import { ApiError } from './api'
import { auditDetail, auditPage, auditTypes, changes } from './audit-api'
import type { AuditPage, AuditRecord } from './audit-api'

type Load<T> = { state: 'loading' } | { state: 'ok'; data: T } | { state: 'error'; status: number }
export function AuditWorkspace({ language, selected, query = '', allowed, onNavigate }: {
  language: 'tr' | 'en'; selected: string | null; query?: string; allowed: boolean; onNavigate: (href: string) => void
}) {
  const en = language === 'en'
  const [page, setPage] = useState<Load<AuditPage>>({ state: 'loading' })
  const [detail, setDetail] = useState<Load<AuditRecord>>({ state: 'loading' })
  const [attempt, setAttempt] = useState(0)
  useEffect(() => {
    if (!allowed) return
    const c = new AbortController(); let active = true
    const timer = setTimeout(() => c.abort(), 10000)
    const load = async () => {
      setPage({ state: 'loading' }); setDetail({ state: 'loading' })
      try {
        const data = await auditPage(query, c.signal)
        if (!active) return
        if (selected) {
          const record = await auditDetail(selected, c.signal)
          if (!active) return
          setDetail({ state: 'ok', data: record })
        }
        setPage({ state: 'ok', data })
      } catch (error) {
        if (active) { const status = error instanceof ApiError ? error.status : 0; setPage({ state: 'error', status }); setDetail({ state: 'error', status }) }
      }
    }
    void load()
    return () => { active = false; clearTimeout(timer); c.abort() }
  }, [allowed, query, selected, attempt])
  const href = (id: string | null, value = query) => `/app/audit${id ? `/${id}` : ''}${value ? `?${value}` : ''}`
  if (!allowed) return <p role="alert">{en ? 'Audit permission is required.' : 'İşlem geçmişi yetkisi gerekiyor.'}</p>
  return <section aria-label={en ? 'Action history' : 'İşlem geçmişi'}>
    <p>{en ? 'Business changes in your authorized scope. Security events are restricted separately. Missing historical metadata is unavailable.' : 'Yetki alanınızdaki iş değişiklikleri. Güvenlik olayları ayrı kısıtlanır. Eksik tarihsel üst veri mevcut değildir.'}</p>
    <form key={query} className="audit-filters" onSubmit={e => {
      e.preventDefault(); const data = new FormData(e.currentTarget); const next = new URLSearchParams()
      for (const key of ['vesselId', 'resourceId', 'entityType', 'action']) { const value = String(data.get(key) ?? '').trim(); if (value) next.set(key, value) }
      onNavigate(href(null, next.toString()))
    }}>
      <label>{en ? 'Vessel ID' : 'Araç kimliği'}<input name="vesselId" maxLength={36} defaultValue={new URLSearchParams(query).get('vesselId') ?? ''} /></label>
      <label>{en ? 'Object ID' : 'Nesne kimliği'}<input name="resourceId" maxLength={36} defaultValue={new URLSearchParams(query).get('resourceId') ?? ''} /></label>
      <label>{en ? 'Object type' : 'Nesne türü'}<select name="entityType" defaultValue={new URLSearchParams(query).get('entityType') ?? ''}><option value="">{en ? 'All' : 'Tümü'}</option>{auditTypes.map(x => <option key={x}>{x}</option>)}</select></label>
      <label>{en ? 'Action' : 'İşlem'}<select name="action" defaultValue={new URLSearchParams(query).get('action') ?? ''}><option value="">{en ? 'All' : 'Tümü'}</option><option value="Added">{en ? 'Created' : 'Oluşturuldu'}</option><option value="Modified">{en ? 'Updated' : 'Güncellendi'}</option><option value="Deleted">{en ? 'Deleted' : 'Silindi'}</option></select></label>
      <button>{en ? 'Filter' : 'Filtrele'}</button><button type="button" onClick={() => onNavigate('/app/audit')}>{en ? 'Clear filters' : 'Filtreleri temizle'}</button>
    </form>
    {page.state === 'loading' && <p role="status">{en ? 'Loading history…' : 'Geçmiş yükleniyor…'}</p>}
    {page.state === 'error' && <div role="alert"><p>{page.status === 403 ? (en ? 'Audit permission is required.' : 'İşlem geçmişi yetkisi gerekiyor.') : page.status === 401 ? (en ? 'Sign in to continue.' : 'Devam etmek için oturum açın.') : page.status === 404 ? (en ? 'Historical evidence is unavailable.' : 'Tarihsel kanıt mevcut değil.') : (en ? 'History is unavailable. Check filters and retry.' : 'Geçmişe ulaşılamıyor. Filtreleri kontrol edip tekrar deneyin.')}</p><button onClick={() => setAttempt(x => x + 1)}>{en ? 'Retry' : 'Tekrar dene'}</button></div>}
    {page.state === 'ok' && <><p>{en ? 'Authorized records' : 'Yetkili kayıtlar'}: {page.data.total}</p>
      {!page.data.items.length ? <p>{en ? 'No authorized records match these filters.' : 'Bu filtrelere uyan yetkili kayıt yok.'}</p> : <div className="product-table"><table><thead><tr><th>{en ? 'Time' : 'Zaman'}</th><th>{en ? 'Object' : 'Nesne'}</th><th>{en ? 'Action' : 'İşlem'}</th></tr></thead><tbody>{page.data.items.map(row => <tr key={row.id}><td><time dateTime={row.occurredAt}>{row.occurredAt}</time></td><td><button onClick={() => onNavigate(href(row.id))}>{row.entityType} · {row.resourceId ?? (en ? 'Historical identifier unavailable' : 'Tarihsel kimlik mevcut değil')}</button></td><td>{row.action}</td></tr>)}</tbody></table></div>}
      <div className="product-pagination"><button disabled={!new URLSearchParams(query).has('cursor')} onClick={() => { const q = new URLSearchParams(query); q.delete('cursor'); onNavigate(href(null, q.toString())) }}>{en ? 'First page' : 'İlk sayfa'}</button><button disabled={!page.data.nextCursor} onClick={() => { const q = new URLSearchParams(query); q.set('cursor', page.data.nextCursor!); onNavigate(href(null, q.toString())) }}>{en ? 'Next' : 'Sonraki'}</button></div></>}
    {selected && detail.state === 'ok' && page.state === 'ok' && <section className="product-detail" aria-label={en ? 'Record details' : 'Kayıt ayrıntıları'}><h2>{en ? 'Record details' : 'Kayıt ayrıntıları'}</h2><button onClick={() => onNavigate(href(null))}>{en ? 'Close' : 'Kapat'}</button><dl>
      <dt>{en ? 'Actor reference' : 'Aktör referansı'}</dt><dd>{detail.data.actorReference ?? (en ? 'Unavailable' : 'Mevcut değil')}</dd>
      <dt>{en ? 'Assigned role references' : 'Atanmış rol referansları'}</dt><dd>{detail.data.roleContext ?? (en ? 'Role assignment unavailable' : 'Rol ataması mevcut değil')}</dd>
      <dt>{en ? 'Trusted capabilities' : 'Güvenilir yetkiler'}</dt><dd>{detail.data.capabilityContext ?? (en ? 'Historical context unavailable' : 'Tarihsel bağlam mevcut değil')}</dd>
      <dt>{en ? 'Reason' : 'Gerekçe'}</dt><dd>{detail.data.reasonStatus === 'NotRequiredForCommand' ? (en ? 'This draft/directory command does not require a decision reason.' : 'Bu taslak/dizin komutu karar gerekçesi gerektirmez.') : (en ? 'Historical reason unavailable' : 'Tarihsel gerekçe mevcut değil')}</dd>
      <dt>{en ? 'Metadata' : 'Üst veri'}</dt><dd>{detail.data.metadataStatus === 'Recorded' ? (en ? 'Recorded' : 'Kaydedildi') : (en ? 'Historical metadata incomplete' : 'Tarihsel üst veri eksik')}</dd>
      <dt>{en ? 'Resource version' : 'Kaynak sürümü'}</dt><dd>{detail.data.beforeVersion ?? '—'} → {detail.data.afterVersion ?? (en ? 'Unavailable' : 'Mevcut değil')}</dd>
      <dt>{en ? 'Correlation' : 'Korelasyon'}</dt><dd>{detail.data.correlationId ?? '—'}</dd>
    </dl>{detail.data.changeSummary === null ? <p>{en ? 'Historical values are unavailable.' : 'Tarihsel değerler mevcut değil.'}</p> : <ul>{changes(detail.data.changeSummary).map(c => <li key={c.field}>{c.field}: {c.evidence === 'Allowlisted' ? `${c.before ?? '—'} → ${c.after ?? '—'}` : c.evidence === 'RevisionReference' ? `${detail.data.beforeRevision ?? '—'} → ${detail.data.afterRevision ?? '—'}` : (en ? 'Values withheld' : 'Değerler gizlendi')}</li>)}</ul>}
      {detail.data.revisionHref ? <a href={detail.data.revisionHref}>{en ? 'Open exact immutable revision' : 'Değişmez revizyonu aç'}</a> : <p>{en ? 'Immutable revision evidence is unavailable for this record.' : 'Bu kayıt için değişmez revizyon kanıtı mevcut değil.'}</p>}
    </section>}
  </section>
}
