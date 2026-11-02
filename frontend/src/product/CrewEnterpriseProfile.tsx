import { Badge } from '../components/ui/badge'
import type { CrewRow } from './crew-api'

export function CrewEnterpriseProfileFields({ row, language }: { row: CrewRow; language: 'tr' | 'en' }) {
  const en = language === 'en'
  const unknown = en ? 'Unknown' : 'Bilinmiyor'
  const withheld = en ? 'Withheld' : 'Gizli'
  const field = (label: string, value: string | null, hidden = false) => <dl key={label}><dt>{label}</dt><dd>{value ?? (hidden ? withheld : unknown)}</dd></dl>
  return <>
    <div className="crew-enterprise-profile-fields" aria-label={en ? 'Assignment particulars' : 'Atama bilgileri'}>
      {field(en ? 'Employee number' : 'Personel numarası', row.employeeNo, true)}
      {field(en ? 'Position / rank / capacity' : 'Pozisyon / rütbe / kapasite', `${row.rank ?? unknown} / ${row.capacity ?? unknown}`)}
      {field(en ? 'Department' : 'Bölüm', row.department)}
      {field(en ? 'Period' : 'Dönem', `${row.from ?? unknown} — ${row.to ?? unknown}`)}
      {field(en ? 'Contract end date' : 'Sözleşme bitiş tarihi', row.contractEndDate)}
      {field(en ? 'Watch group' : 'Vardiya grubu', row.watchGroup)}
    </div>
    <details className="crew-enterprise-disclosure"><summary>{en ? 'Qualification and protected information' : 'Yeterlilik ve korumalı bilgiler'} <Badge>{en ? 'Separate evaluations' : 'Ayrı değerlendirmeler'}</Badge></summary>
      <div><p>{en ? 'An assignment declaration does not determine qualification. Contact and health information have separate access policies.' : 'Atama beyanı yeterlilik sonucu oluşturmaz. İletişim ve sağlık bilgilerinin erişim politikaları ayrıdır.'}</p>
        <div className="crew-enterprise-profile-fields">
          {field(en ? 'Eligibility' : 'Uygunluk', null)}{field(en ? 'Fitness' : 'Sağlık uygunluğu', null)}
          {field(en ? 'Certificates' : 'Belgeler', null)}{field(en ? 'Competence' : 'Yetkinlik', null)}
          {field(en ? 'Contact' : 'İletişim', null, true)}{field(en ? 'Health' : 'Sağlık', null, true)}
        </div>
      </div>
    </details>
  </>
}
