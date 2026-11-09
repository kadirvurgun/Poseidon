import { localizedLabel } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
import type { ReactNode } from 'react';
import { Box, CalendarClock, CircleHelp, ClipboardList, Layers3, ListTree, Settings2, TriangleAlert, Wrench } from 'lucide-react';
import { Badge } from '../components/ui/badge';
import { Button } from '../components/ui/button';
import { Card, CardContent, CardHeader, CardTitle } from '../components/ui/card';
import { ScopeMetric } from './DirectoryOverview';
import { dueReason, dueState } from './maintenance-copy';
import { assetFields, planFields } from './technical-fields';
import type { Asset, Plan, Plans, References, Snapshot } from './technical-api';
import './technical-enterprise.css';
function Field({ label, children }: {
    label: string;
    children: ReactNode;
}) { return <div><dt>{label}</dt><dd>{children}</dd></div>; }
const provenance = (code: string, en: boolean) => code === 'SyntheticUnverified' ? (localizedText(en, "pairs.m_722c88f77fd25c99"))
    : code === 'UserDeclaredUnverified' ? (localizedText(en, "pairs.m_f37dd0ff02aa8041")) : code;
export function TechnicalOverview({ assets, plans, en }: {
    assets: Snapshot;
    plans: Plans | null;
    en: boolean;
}) {
    const returned = localizedText(en, "pairs.m_2dd62898c35b6ce0");
    return <section className="enterprise-metrics technical-overview" aria-label={localizedText(en, "pairs.m_bd4df0e3cbbcced7")}>
    {plans ? <>
      <ScopeMetric title={localizedText(en, "pairs.m_21f68c2e5c640595")} value={plans.items.length} note={returned} icon={ClipboardList}/>
      <ScopeMetric title={localizedText(en, "pairs.m_2fd0f40ff7d43adb")} value={plans.items.filter(plan => plan.due.state === 'Due').length} note={localizedText(en, "pairs.m_7feaa7d1b373141d")} icon={TriangleAlert}/>
      <ScopeMetric title={localizedText(en, "pairs.m_4f151bc7ae7f57ba")} value={plans.items.filter(plan => !['Due', 'NotDue'].includes(plan.due.state)).length} note={localizedText(en, "pairs.m_092bd942f1089905")} icon={CircleHelp}/>
      <ScopeMetric title={localizedText(en, "pairs.m_4296477d02b911a7")} value={plans.items.filter(plan => plan.due.state === 'NotDue').length} note={localizedText(en, "pairs.m_011388aafcc95ccf")} icon={CalendarClock}/>
    </> : <>
      <ScopeMetric title={localizedText(en, "pairs.m_4ff62010d8e17128")} value={assets.items.length} note={returned} icon={ListTree}/>
      <ScopeMetric title={localizedText(en, "pairs.m_2da53d6b005a64d3")} value={assets.items.filter(asset => asset.level === 'System').length} note={returned} icon={Layers3}/>
      <ScopeMetric title={localizedText(en, "pairs.m_b562d7b94d9cec93")} value={assets.items.filter(asset => asset.level === 'Equipment').length} note={returned} icon={Settings2}/>
      <ScopeMetric title={localizedText(en, "pairs.m_19c4a58f0efb68ad")} value={assets.items.filter(asset => asset.level === 'Component').length} note={returned} icon={Box}/>
    </>}
  </section>;
}
export function TechnicalPlanList({ plans, assets, references, filter, selectedId, en, onSelect }: {
    plans: Plan[];
    assets: Asset[];
    references: References | null;
    filter: string;
    selectedId: string | null;
    en: boolean;
    onSelect: (id: string) => void;
}) {
    const locale = en ? 'en' : 'tr', query = filter.toLocaleLowerCase(locale);
    const rows = plans.map(plan => ({ plan, asset: assets.find(asset => asset.id === plan.assetId), job: references?.jobs.find(job => job.id === plan.jobId) }))
        .filter(({ plan, asset, job }) => `${job?.title ?? ''} ${job?.code ?? ''} ${asset?.name ?? ''} ${asset?.code ?? ''} ${plan.id}`.toLocaleLowerCase(locale).includes(query));
    return <div className="technical-plan-list">
    <p className="technical-record-count">{rows.length} / {plans.length} {localizedText(en, "pairs.m_2f136b303fd1f968")}</p>
    {rows.length === 0 && <p className="technical-list-empty">{plans.length === 0 ? (localizedText(en, "pairs.m_dc6a831d7fb1c5ff")) : (localizedText(en, "pairs.m_da024c082d17a0b7"))}</p>}
    <ul>{rows.map(({ plan, asset, job }) => <li key={plan.id}><Button variant="ghost" className="technical-plan-item" aria-current={selectedId === plan.id ? 'true' : undefined} aria-label={`${job?.title ?? (localizedText(en, "pairs.m_b12309949fe76479"))} · ${dueState(plan.due.state, en)}`} onClick={() => onSelect(plan.id)}>
      <span className="technical-plan-item-title"><Wrench aria-hidden="true"/><strong>{job?.title ?? (localizedText(en, "pairs.m_b12309949fe76479"))}</strong><Badge tone={plan.due.state === 'Due' ? 'danger' : plan.due.state === 'NotDue' ? 'neutral' : 'warning'}>{dueState(plan.due.state, en)}</Badge></span>
      <span>{asset?.name || asset?.code || (localizedText(en, "pairs.m_1b5b48e0ca029fba"))}{job?.code ? ` · ${job.code}` : ''}</span>
      <span>{localizedText(en, "pairs.m_79a3416028186e6c")}: {plan.due.nextDueDate ?? '—'} / {plan.due.nextDueMeter ?? '—'} · r{plan.revision}</span>
    </Button></li>)}</ul>
  </div>;
}
export function TechnicalEmptyDetail({ plan, en }: {
    plan: boolean;
    en: boolean;
}) {
    return <div className="technical-empty-detail"><ListTree aria-hidden="true"/><h2>{plan ? (localizedText(en, "pairs.m_8a9ce3308cdb33ed")) : (localizedText(en, "pairs.m_915b973ccffea420"))}</h2><p>{localizedText(en, "pairs.m_0115d3e4d24feee7")}</p></div>;
}
export function EquipmentSummary({ asset, en }: {
    asset: Asset;
    en: boolean;
}) {
    const unknown = localizedText(en, "pairs.m_a5a7675bd7000dbd");
    const groups = [{ title: localizedText(en, "pairs.m_522036cca84ed10a"), keys: ['code', 'name', 'systemFamily', 'equipmentType', 'criticality'] },
        { title: localizedText(en, "pairs.m_11363e2c839c0d1e"), keys: ['maker', 'model', 'serialNumber', 'location', 'commissionDate'] }];
    return <div className="technical-read-detail" aria-label={localizedText(en, "pairs.m_233d9695574bc7ad")}>
    <div className="technical-detail-heading"><div><Badge>{asset.level === 'System' ? (localizedText(en, "pairs.m_0e986037a7ebffd5")) : asset.level === 'Component' ? (localizedText(en, "pairs.m_1015c6735bd0c910")) : (localizedText(en, "pairs.m_77e9d3d4c2e4b44f"))}</Badge><h2>{asset.name || asset.code || unknown}</h2><p>{asset.code ?? unknown} · {provenance(asset.provenance, en)}</p></div><Badge>r{asset.revision}</Badge></div>
    <div className="technical-detail-cards">{groups.map(group => <Card key={group.title}><CardHeader><CardTitle>{group.title}</CardTitle></CardHeader><CardContent><dl>{assetFields.filter(([key]) => group.keys.includes(key)).map(([key, english, turkish]) => <Field key={key} label={localizedLabel({ en: english, tr: turkish }, en)}>{asset[key] ?? unknown}</Field>)}</dl></CardContent></Card>)}</div>
    <Card className="technical-notes"><CardHeader><CardTitle>{localizedText(en, "pairs.m_9672c29ab351df60")}</CardTitle></CardHeader><CardContent><dl><Field label={localizedText(en, "pairs.m_4932250e7b636bb1")}>{asset.responsibleRoleId ?? unknown}</Field><Field label={localizedText(en, "pairs.m_0fb9c90ba8619153")}>{asset.notes ?? unknown}</Field></dl></CardContent></Card>
    <details className="technical-lineage"><summary>{localizedText(en, "pairs.m_b1ea11227320f32e")}</summary><dl><Field label={localizedText(en, "pairs.m_8dd47e82f3b62583")}>{asset.id}</Field><Field label={localizedText(en, "pairs.m_706de0dcd6d6c4ae")}>{asset.parentId ?? (localizedText(en, "pairs.m_bb68aba6e277efbc"))}</Field><Field label={localizedText(en, "pairs.m_9063738166a73fc0")}>{asset.revision}</Field></dl></details>
  </div>;
}
export function MaintenancePlanSummary({ plan, asset, references, en }: {
    plan: Plan;
    asset: Asset | undefined;
    references: References | null;
    en: boolean;
}) {
    const unknown = localizedText(en, "pairs.m_a5a7675bd7000dbd"), job = references?.jobs.find(job => job.id === plan.jobId);
    return <div className="technical-read-detail" aria-label={localizedText(en, "pairs.m_65d9eade09b7d268")}>
    <div className="technical-detail-heading"><div><Badge>{localizedText(en, "pairs.m_ad56d6d2a6bc2942")} · r{plan.revision}</Badge><h2>{job?.title ?? (localizedText(en, "pairs.m_b12309949fe76479"))}</h2><p>{asset?.name || asset?.code || unknown}</p></div><Badge tone={plan.due.state === 'Due' ? 'danger' : plan.due.state === 'NotDue' ? 'neutral' : 'warning'}>{dueState(plan.due.state, en)}</Badge></div>
    <div className="technical-calculation"><CalendarClock aria-hidden="true"/><div><strong>{localizedText(en, "pairs.m_c91d9e6aed0131ce")}</strong><p>{dueReason(plan.due.reason, en)}</p><p>{localizedText(en, "pairs.m_d2a2f9493c990f43")}</p></div></div>
    <div className="technical-detail-cards"><Card><CardHeader><CardTitle>{localizedText(en, "pairs.m_b4261ffcaea607fb")}</CardTitle></CardHeader><CardContent><dl>
      <Field label={localizedText(en, "pairs.m_647534047152a1ce")}>{plan.referenceAt}</Field><Field label={localizedText(en, "pairs.m_9b7abff02e4d5f73")}>{plan.inputs.timeZone ?? unknown}</Field>
      <Field label={localizedText(en, "pairs.m_79a3416028186e6c")}>{plan.due.nextDueDate ?? unknown} / {plan.due.nextDueMeter ?? unknown}</Field>
      <Field label={localizedText(en, "pairs.m_a5cc823a0879f240")}>{plan.inputs.provenance ?? unknown} / {plan.inputs.observationRevision ?? unknown}</Field><Field label={localizedText(en, "pairs.m_b38bd5b40d25214e")}>{plan.inputs.observedAt ?? unknown}</Field>
    </dl>{plan.observationState && plan.observationState !== 'NotRequested' && <p className="technical-note">{localizedText(en, "pairs.m_b17b12b140003ab3")}: {dueReason(plan.observationState, en)} · {plan.observationAssessedAt}</p>}</CardContent></Card>
    <Card><CardHeader><CardTitle>{localizedText(en, "pairs.m_73a694a0f8096511")}</CardTitle></CardHeader><CardContent><dl>
      <Field label={localizedText(en, "pairs.m_519c4012eb685675")}>{job?.title ?? unknown}</Field><Field label={localizedText(en, "pairs.m_aad8c1474d4ec372")}>{plan.ownerRoleId ?? unknown}</Field><Field label={localizedText(en, "pairs.m_c1f94e966d28caa0")}>{plan.assetRevision}</Field><Field label={localizedText(en, "pairs.m_4153a47895768832")}>{provenance(plan.provenance, en)}</Field>
    </dl></CardContent></Card></div>
    <details className="technical-lineage"><summary>{localizedText(en, "pairs.m_dd420ce904eab5ff")}</summary><dl>{planFields.map(([key, english, turkish]) => <Field key={key} label={localizedLabel({ en: english, tr: turkish }, en)}>{plan.inputs[key] ?? unknown}</Field>)}</dl></details>
    <p className="technical-note">{localizedText(en, "pairs.m_7d38376c12dac579")}</p>
  </div>;
}
