import { Badge } from '../components/ui/badge'
import { Button } from '../components/ui/button'
import { EnterprisePagination, EnterpriseToolbar } from './EnterprisePatterns'
import { CertificateSummary } from './CertificateSummary'
import { temporalStates, type CertificateCapabilities, type CertificatePage, type CertificateScope } from './certificates-api'
import { certificateTemporal } from './certificate-copy'
import { certificateLocationPath, type CertificateLocation } from './certificate-location'

export function CertificateCenterView({ page, caps, route, language, onNavigate }: { page: CertificatePage; caps: CertificateCapabilities; route: CertificateLocation; language: 'en' | 'tr'; onNavigate: (href: string) => void }) {
  const en = language === 'en'
  return <section aria-label={en ? 'Authorized certificate page' : 'Yetkili sertifika sayfası'}>
    <form aria-label={en ? 'Certificate scope filters' : 'Sertifika kapsam filtreleri'} onSubmit={event => event.preventDefault()}>
      <EnterpriseToolbar>
        <label>{en ? 'Certificate scope' : 'Sertifika kapsamı'}<select value={page.scope} onChange={event => onNavigate(certificateLocationPath({ ...route, scope: event.target.value as CertificateScope, offset: 0 }))}>
          {caps.scopes.map(scope => <option key={scope} value={scope}>{scope === 'Vessel' ? (en ? 'Vessel certificates' : 'Araç sertifikaları') : scope === 'Crew' ? (en ? 'Protected crew declarations' : 'Korunan mürettebat beyanları') : (en ? 'Authorized vessel and crew sources' : 'Yetkili araç ve mürettebat kaynakları')}</option>)}
        </select></label>
        <label>{en ? 'Declared calendar filter' : 'Beyan takvim filtresi'}<select value={route.temporal ?? ''} onChange={event => onNavigate(certificateLocationPath({ ...route, temporal: event.target.value === '' ? undefined : event.target.value as CertificateLocation['temporal'], offset: 0 }))}>
          <option value="">{en ? 'All declared calendar states' : 'Tüm beyan takvim durumları'}</option>
          {temporalStates.map(state => <option key={state} value={state}>{certificateTemporal(state, en)}</option>)}
        </select></label>
      </EnterpriseToolbar>
    </form>
    <h2>{en ? 'Returned authorized records' : 'Dönen yetkili kayıtlar'}</h2>
    <p>{en ? 'This page only. Calendar observations are separate from qualification; these are not fleet-wide readiness totals.' : 'Yalnız bu sayfa. Takvim gözlemleri nitelendirmeden ayrıdır; bunlar filo genelinde hazırlık toplamları değildir.'}</p>
    {page.crewCoverage === 'UNAVAILABLE_POLICY_OR_AUTHORITY' && <p role="alert">{en ? 'Crew policy or purpose access unavailable. Missing protected rows are not a complete or empty crew inventory.' : 'Mürettebat politikası veya amaç erişimi kullanılamıyor. Eksik korunan satırlar tam ya da boş mürettebat envanteri değildir.'}</p>}
    <dl className="certificate-calendar-counts">{([
      ['valid', 'Beyond due window', 'Hazırlık aralığı dışında'], ['due90', 'Declared expiry <90 days', 'Beyan bitişi <90 gün'],
      ['due60', 'Declared expiry <60 days', 'Beyan bitişi <60 gün'], ['due30', 'Declared expiry <30 days', 'Beyan bitişi <30 gün'],
      ['expired', 'Declared expiry passed', 'Beyan bitişi geçmiş'], ['surveyWindow', 'Declared survey window open', 'Beyan sörvey aralığı açık'],
      ['reviewRequired', 'Calendar review required', 'Takvim incelemesi gerekli'],
    ] as const).map(([key, label, tr]) => <div key={key}><dt>{en ? label : tr}</dt><dd>{page.counts[key]}</dd></div>)}</dl>
    <p>{en ? 'Critical gap coverage unavailable. Authority review remains required.' : 'Kritik eksik kapsamı kullanılamıyor. Yetki incelemesi gerekliliği sürüyor.'}</p>
    {!page.items.length && <p>{en ? 'No authorized certificate rows on this page. No complete or compliant inventory is inferred.' : 'Bu sayfada yetkili sertifika satırı yok. Tam veya uyumlu envanter varsayılmaz.'}</p>}
    <ul className="certificate-record-list">{page.items.map(row => <li key={row.scope + '/' + row.id}>
      <div className="certificate-record-heading"><Button variant="outline" onClick={() => onNavigate(row.href)}>{row.content.documentName ?? row.content.metadata.declaration.certificateNumber ?? (en ? 'Unverified certificate declaration' : 'Doğrulanmamış sertifika beyanı')}</Button>
        <Badge tone="neutral">{row.scope === 'Vessel' ? (en ? 'Vessel' : 'Araç') : (en ? 'Protected crew declaration' : 'Korunan mürettebat beyanı')}</Badge></div>
      <CertificateSummary row={row} language={language} />
    </li>)}</ul>
    <EnterprisePagination label={en ? 'Certificate record pages' : 'Sertifika kayıt sayfaları'} previous={en ? 'Previous certificates' : 'Önceki sertifikalar'} next={en ? 'More certificates' : 'Diğer sertifikalar'} previousDisabled={!route.offset} nextDisabled={!page.hasMore || route.offset >= 100000} onPrevious={() => onNavigate(certificateLocationPath({ ...route, offset: Math.max(0, route.offset - 25) }))} onNext={() => onNavigate(certificateLocationPath({ ...route, offset: route.offset + 25 }))} />
  </section>
}
