import { localizedCopy, localizedLabel } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { Badge } from '../components/ui/badge';
import type { TugContext, TugRoleLabel } from './tug-api';
import { tugLabel } from './tug-labels';
import './daily-operations.css';
const copy = localizedCopy('TugProfileContext_tsx_copy');
// DEV-080 / T-TUG-001: role lens and Ship Mode visibility come from the server context.
// UNKNOWN is shown only when the server says the role assignment is missing.
function RoleLens({ context, language }: { context: TugContext; language: AppLocale }) {
    const t = copy[language], lens = context.roleLens;
    const names = (rows: TugRoleLabel[]) => rows.map(row => localizedLabel(row, language)).join(', ');
    const roles = lens.roleState === 'Configured' ? names(lens.roles) : lens.roleState === 'Missing' ? t.roleUnknown : t.roleUnavailable;
    const lenses = lens.lensState !== 'Configured' ? (lens.lensState === 'Missing' ? t.roleUnknown : t.lensUnavailable) : lens.lenses.length === 0 ? t.noTugLens : names(lens.lenses);
    return <><div><dt>{t.roleLens}</dt><dd data-state={lens.roleState}>{roles}{lens.roleCatalogVersion && <small> · {lens.roleCatalogVersion}</small>}</dd></div>
      <div><dt>{t.tugLens}</dt><dd data-state={lens.lensState}>{lenses}{lens.lensVersion && <small> · {lens.lensVersion}</small>}</dd></div></>;
}
function ShipModeScreens({ context, language }: { context: TugContext; language: AppLocale }) {
    const t = copy[language], names: Record<string, string> = { 'noon-report': t.noon, 'deck-daily-log': t.deck, 'engine-daily-log': t.engine };
    return <section aria-label={t.shipScreens}><h3>{t.shipScreens}</h3>{!context.crossMode ? <p role="status">{t.shipHidden}</p> : context.shipModeDestinations.length === 0
        ? <p role="status">{t.shipNoneGranted}</p>
        : <ul>{context.shipModeDestinations.map(row => <li key={row.key}><a href={row.href}>{names[row.key]}</a></li>)}</ul>}</section>;
}
export function TugProfileContext({ context, language, offline }: {
    context: TugContext;
    language: AppLocale;
    offline: boolean;
}) {
    const en = language === 'en', unavailable = localizedText(en, "pairs.m_49138f2831bf4262"), unknown = localizedText(en, "pairs.m_a5a7675bd7000dbd"), binding = context.tugTypeContext;
    return <section className="tug-profile-context" aria-label={localizedText(en, "pairs.m_1f3c3b5955d7e1dc")}><h2>{localizedText(en, "pairs.m_21739423fc15322f")}</h2>{offline ? <p role="status">{localizedText(en, "pairs.m_531f421361669045")}</p> : <><Badge tone="warning">{localizedText(en, "pairs.m_2ed4ebb1d349566c")}</Badge><dl className="daily-source-grid"><RoleLens context={context} language={language}/><div><dt>{localizedText(en, "pairs.m_ede2d83d91053b61")}</dt><dd><small>{context.roleReferences ?? unknown}</small></dd></div><div><dt>{localizedText(en, "pairs.m_6d92165794b9a4e6")}</dt><dd>{context.crossMode ? (localizedText(en, "pairs.m_869122757b0994bc")) : (localizedText(en, "pairs.m_e8a7c0910b9d50d2"))}</dd></div><div><dt>{localizedText(en, "pairs.m_17e67e909863fa7c")}</dt><dd>{localizedLabel(context.tugTypeOptions?.find(x => x.code === context.tugType), language) ?? unavailable}</dd></div><div><dt>{localizedText(en, "pairs.m_f1f14ae935ad93c7")}</dt><dd>{binding ? `${binding.sourceReference} / ${binding.sourceRevision}` : unavailable}</dd></div><div><dt>{localizedText(en, "pairs.m_0f2c95e04d0115e6")}</dt><dd>{binding ? <time dateTime={binding.observedAt}>{binding.observedAt}</time> : unknown} / {context.tugTypePolicyVersion ?? unavailable}</dd></div><div><dt>{localizedText(en, "pairs.m_36b751df119da32d")}</dt><dd>{context.bollardPull.value ?? unknown} · {localizedText(en, "pairs.m_75adc2c40661559b")}</dd></div><div><dt>{localizedText(en, "pairs.m_b35128a3a4fe12aa")}</dt><dd>{context.bollardPull.source} / {context.bollardPull.version ?? unknown} / {tugLabel(context.bollardPull.provenance, language)}</dd></div><div><dt>{localizedText(en, "pairs.m_8c92c75d71382c5a")}</dt><dd>{tugLabel(context.bollardPull.freshness, language)} / {context.bollardPull.observedAt ? <time dateTime={context.bollardPull.observedAt}>{context.bollardPull.observedAt}</time> : unknown}</dd></div></dl><h3>{localizedText(en, "pairs.m_94b4b57928b66f65")}</h3><p>{tugLabel(context.equipmentCoverage, language)}</p>{context.equipment === null ? <p>{localizedText(en, "pairs.m_fca228ecf0fd544a")}</p> : context.equipment.length === 0 ? <p>{localizedText(en, "pairs.m_fa0c3df982a4b247")}</p> : <ul>{context.equipment.map(asset => <li key={asset.id}><strong>{asset.equipmentType ?? unknown}</strong> · {asset.level} · r{asset.revision} · {tugLabel(asset.provenance, language)}<small>{asset.id}</small></li>)}</ul>}<ShipModeScreens context={context} language={language}/><p>{tugLabel(context.reason, language)}</p></>}</section>;
}
