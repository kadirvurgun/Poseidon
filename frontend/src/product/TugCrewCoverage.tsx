import { localizedCopy } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import type { TugCrewCoverage as Coverage } from './tug-crew-contract';
const copy = localizedCopy("TugCrewCoverage_tsx_copy");
export function TugCrewCoverage({ items, language }: {
    items: Coverage[];
    language: AppLocale;
}) {
    const t = copy[language];
    return <section aria-label={t.title}><h4>{t.title}</h4><p>{t.boundary}</p>
    {items.map(c => <article key={c.tugId} aria-label={`${t.title} ${c.tugId}`}>
      <p>{c.tugId} · {c.state === 'CurrentMinimumRoleCoverage' ? t.current : c.state === 'Blocked' ? t.blocked : c.state === 'Unavailable' ? t.protected : t.review}</p>
      <p>{c.reason === 'CrossRoleAllocationReviewRequired' ? t.allocation : t[c.reason as keyof typeof t] ?? t.review}</p>
      <p>{t.window}: {c.windowStart ?? t.unknown} / {c.windowEnd ?? t.unknown}</p><p>{t.assessed}: {c.assessedAt}</p>
      {c.msmdId && <><p>{t.document}: {c.msmdId} / {c.msmdRevision}</p><p>{t.valid}: {c.validUntil}</p></>}
      {c.roles.length > 0 && <div className="product-table"><table><thead><tr><th scope="col">{t.role}</th><th scope="col">{t.required}</th><th scope="col">{t.declared}</th><th scope="col">{t.qualified}</th><th scope="col">{t.unresolved}</th></tr></thead>
        <tbody>{c.roles.map(r => <tr key={r.positionId}><th scope="row">{r.positionId}</th><td>{r.required}</td><td>{r.declared}</td><td>{r.qualified}</td><td>{r.unknown}</td></tr>)}</tbody></table></div>}
    </article>)}
  </section>;
}
