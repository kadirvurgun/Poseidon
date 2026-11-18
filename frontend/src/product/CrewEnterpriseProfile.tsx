import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { Badge } from '../components/ui/badge';
import type { CrewRow } from './crew-api';
export function CrewEnterpriseProfileFields({ row, language }: {
    row: CrewRow;
    language: AppLocale;
}) {
    const en = language === 'en';
    const unknown = localizedText(en, "pairs.m_a5a7675bd7000dbd");
    const withheld = localizedText(en, "pairs.m_1b161b9f4d3717c7");
    const field = (label: string, value: string | null, hidden = false) => <dl key={label}><dt>{label}</dt><dd>{value ?? (hidden ? withheld : unknown)}</dd></dl>;
    return <>
    <div className="crew-enterprise-profile-fields" aria-label={localizedText(en, "pairs.m_bb1cb852006da8b2")}>
      {field(localizedText(en, "pairs.m_e9b5c1a710bc9750"), row.employeeNo, true)}
      {field(localizedText(en, "pairs.m_9594e672a0050f4b"), `${row.rank ?? unknown} / ${row.capacity ?? unknown}`)}
      {field(localizedText(en, "pairs.m_3b1cbbb26d2a528c"), row.department)}
      {field(localizedText(en, "pairs.m_20c8fd7c94a7ce75"), `${row.from ?? unknown} — ${row.to ?? unknown}`)}
      {field(localizedText(en, "pairs.m_cc5be2f8aed1d1c7"), row.contractEndDate)}
      {field(localizedText(en, "pairs.m_5054e17e5335521e"), row.watchGroup)}
    </div>
    <details className="crew-enterprise-disclosure"><summary>{localizedText(en, "pairs.m_f34f2f54f8396f7f")} <Badge>{localizedText(en, "pairs.m_3d0e011ff9f5e792")}</Badge></summary>
      <div><p>{localizedText(en, "pairs.m_8b861ee4ac415a23")}</p>
        <div className="crew-enterprise-profile-fields">
          {field(localizedText(en, "pairs.m_5ca62ea982acf657"), null)}{field(localizedText(en, "pairs.m_62f5cb74d1e29eee"), null)}
          {field(localizedText(en, "pairs.m_d1e2eb240ae39a85"), null)}{field(localizedText(en, "pairs.m_3c4e349aaa66b39d"), null)}
          {field(localizedText(en, "pairs.m_b9bf67ffa3cc16d2"), null, true)}{field(localizedText(en, "pairs.m_02d0007ab2c9949b"), null, true)}
        </div>
      </div>
    </details>
  </>;
}
