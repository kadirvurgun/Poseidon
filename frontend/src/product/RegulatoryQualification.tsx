import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { CompliancePolicyEditor } from './CompliancePolicyEditor';
import { complianceFields } from './compliance-input-contract';
import { owningFields } from './operation-equipment-contract';
import { Button } from '../components/ui/button';
import { EnterpriseActions } from './EnterprisePatterns';
import { useQualificationRecovery } from './use-qualification-recovery';
import { loginHref } from './workspace-location';
import { TugCalculationEditor } from './TugCalculationEditor';
import * as api from './qualification-api';
type Props = {
    rule: string;
    ruleRevision: number;
    vessel: string;
    language: AppLocale;
    csrf: string;
    offline: boolean;
    onDirty: (v: boolean) => void;
    onSaving: (v: boolean) => void;
};
export function RegulatoryQualification({ rule, ruleRevision, vessel, language, offline, onDirty, onSaving }: Props) {
    const { data, content, profile, historyRows, historyMore, dirty, busy, error, attempt, notice, locked, current, canShow, available, canRetry, withheld, sourceMismatch, expired, resupply, state, until, chooseState, chooseUntil, edit, send, capture, transition, source, discard, recheck, more } =
        useQualificationRecovery({ rule, ruleRevision, vessel, offline, onDirty, onSaving });
    const control = content.control;
    const controlEdit = (patch: Partial<api.ControlPolicy>) => edit({ control: { ownerRole: content.ownerRole, frequency: null, evidencePolicy: null, verificationPolicy: null, maxAgeHours: null, requireFile: null, requireAcknowledgment: null, acknowledgmentRole: null, requiredVerificationMechanism: null, verificationMechanismSourceLocator: null, sourceLocator: null, ...control, ...patch } });
    return <section className="regulatory-qualification" aria-label={localizedText(language, "pairs.m_f61046966aa5331c")}>
    <h3>{localizedText(language, "pairs.m_f61046966aa5331c")}</h3>
    <p>{localizedText(language, "pairs.m_33934b7c51852222")}</p>
    {offline && <p role="status">{localizedText(language, "pairs.m_6ce71a12ae4fcd9b")}</p>}
    {!canShow && error === null && <p role="status">{localizedText(language, "pairs.m_f9810a73f6e5c421")}</p>}
    {error !== null && <p role="alert">{error === 403 ? localizedText(language, "pairs.m_c96b3e85ec9464f6") : error === 409 ? localizedText(language, "pairs.m_75ed663c7b057ed4") : error === 0 ? localizedText(language, attempt?.unknown ? "pairs.m_0236b48b05ed5a34" : "pairs.m_eb3627298fe00267") : `${localizedText(language, "pairs.m_eb3627298fe00267")} (${error})`}</p>}
    {error === 401 && <a href={loginHref()} target="_blank" rel="noopener noreferrer">{localizedText(language, 'pairs.m_e3f876bc9c438b22')}</a>}
    {notice && <p role="status">{localizedText(language, 'pairs.m_d11768bdd33352e6')}</p>}
    {attempt?.unknown && <p role="status">{localizedText(language, 'pairs.m_62930366e47b200e')}</p>}
    {withheld && <label>{localizedText(language, 'pairs.m_401a5953741e7377')}<input type="file" disabled={busy || !available} onChange={e => void resupply(e.target.files?.[0])}/></label>}
    {sourceMismatch && <p role="alert">{localizedText(language, 'pairs.m_2adab3886ca18b5e')}</p>}
    {expired && <p role="status">{localizedText(language, 'pairs.m_6eb8ee576dacf887')}</p>}
    {canShow && <>
    {data?.items.length === 0 && <p>{localizedText(language, "pairs.m_6865d2343faf7ae9")}</p>}
    {current && current.ruleRevision !== ruleRevision && <p role="alert">{localizedText(language, "pairs.m_f2f67f1fe95584ab")}</p>}
    {current && <p>{current.state} · {current.revision} · {current.savedAt} · {current.policyVersion}<br /><span className="register-hash">{current.sourceHash || '—'} · {current.hash}</span><br />{current.blockers.join(' · ')}<br />{current.contract?.sourceLocator} · {current.contract?.requiredMechanism}</p>}
    {current?.sourceHash && data && data.authorities.length > 0 && <a href={`/api/v1/regulatory-source-governance/${rule}/revisions/${current.revision}/source`} download>{localizedText(language, "pairs.m_9c6448c23e8fa864")}</a>}
    <fieldset disabled={locked || !data?.actions.includes('draft')}><legend>{localizedText(language, "pairs.m_06a550d753abddc9")}</legend>
      <label>{localizedText(language, "pairs.m_27729a6ee466ec3a")}<input type="file" onChange={e => void source(e.target.files?.[0])}/></label>
      {(['publisher', 'edition', 'validationReview', 'trigger', 'impactReview', 'supersedingRuleId'] as const).map((key, i) => <label key={key}>{[localizedText(language, "pairs.m_06b67bfc97fe4813"), localizedText(language, "pairs.m_d1571b2eba004dc5"), localizedText(language, "pairs.m_fbca548161014817"), localizedText(language, "pairs.m_5019958a8f964a3f"), localizedText(language, "pairs.m_58209c1d442742ce"), localizedText(language, "pairs.m_e9007e24057cba44")][i]}<input value={content[key] ?? ''} maxLength={2000} onChange={e => edit({ [key]: e.target.value || null })}/></label>)}
      <label>{localizedText(language, "pairs.m_c442b2ff1948ef0d")}<input type="number" min={1} value={content.supersedingRuleRevision ?? ''} onChange={e => edit({ supersedingRuleRevision: e.target.value ? Number(e.target.value) : null })}/></label>
      <label>{localizedText(language, "pairs.m_6d62416398b312ba")}<select value={content.ownerId ? `${content.ownerId}/${content.ownerRole}` : ''} onChange={e => { const [ownerId, ownerRole] = e.target.value.split('/'); edit({ ownerId: ownerId || null, ownerRole: ownerRole || null }); }}><option value="">{localizedText(language, "pairs.m_a5bd35d90de7c160")}</option>{data?.owners.map(o => <option key={`${o.actorId}/${o.roleId}`} value={`${o.actorId}/${o.roleId}`}>{o.actorId} / {o.roleId}</option>)}</select></label>
      {[...api.profileInputs, ...owningFields, ...complianceFields].map(field => { const p = content.inputs?.find(x => x.field === field); return <label key={field}>{field}<select value={p?.basis ?? ''} onChange={e => edit({ inputs: [...(content.inputs ?? []).filter(x => x.field !== field), ...(e.target.value ? [{ field, basis: e.target.value as api.InputPolicy['basis'], maxAgeHours: p?.maxAgeHours ?? null }] : [])] })}><option value="">{localizedText(language, "pairs.m_9e419748db57c1ad")}</option>{!api.verifiedProfileInputs.includes(field) && !owningFields.includes(field) && !complianceFields.includes(field) && <option value="Declared">{localizedText(language, "pairs.m_ba5367b528c1524f")}</option>}<option value="HumanAttested">{localizedText(language, "pairs.m_e9da713c34c8cf5d")}</option><option value="ProviderVerified">{localizedText(language, "pairs.m_d7814db101b52168")}</option></select></label>; })}
      {(['ownerRole', 'frequency', 'evidencePolicy', 'verificationPolicy', 'sourceLocator'] as const).map((key, i) => <label key={key}>{[localizedText(language, "pairs.m_4984037cda93f1cd"), localizedText(language, "pairs.m_ae76bf6fcc92bf2e"), localizedText(language, "pairs.m_ad21ac911da803c5"), localizedText(language, "pairs.m_93be913cd6bcee53"), localizedText(language, "pairs.m_0e382d63eb2b2ab5")][i]}<input value={control?.[key] ?? ''} onChange={e => controlEdit({ [key]: e.target.value || null })}/></label>)}
      <label>{localizedText(language, "pairs.m_d8e8d603ccbc7748")}<input type="number" min={1} max={87600} value={control?.maxAgeHours ?? ''} onChange={e => controlEdit({ maxAgeHours: e.target.value ? Number(e.target.value) : null })}/></label>
      <label>{localizedText(language, "pairs.m_7ce7fba36e16e5b7")}<input value={control?.requiredVerificationMechanism ?? ''} onChange={e => controlEdit({ requiredVerificationMechanism: e.target.value || null })}/></label>
      <label>{localizedText(language, "pairs.m_17c794c76b5a43a0")}<input value={control?.verificationMechanismSourceLocator ?? ''} onChange={e => controlEdit({ verificationMechanismSourceLocator: e.target.value || null })}/></label>
      <label>{localizedText(language, "pairs.m_31e70d1a2c30eb4b")}<input value={control?.acknowledgmentRole ?? ''} onChange={e => controlEdit({ acknowledgmentRole: e.target.value || null })}/></label>
      {(['requireFile', 'requireAcknowledgment'] as const).map(key => <label key={key}>{key === 'requireFile' ? localizedText(language, "pairs.m_e6baa72a02df96b8") : localizedText(language, "pairs.m_0beaf60eb03c5cd3")}<select value={control?.[key] == null ? '' : String(control[key])} onChange={e => controlEdit({ [key]: e.target.value === '' ? null : e.target.value === 'true' })}><option value="">{localizedText(language, "pairs.m_a5a7675bd7000dbd")}</option><option value="true">{localizedText(language, "pairs.m_b0df697bd7b8694e")}</option><option value="false">{localizedText(language, "pairs.m_63083b11c5b758e1")}</option></select></label>)}
      <fieldset><legend>{localizedText(language, "pairs.m_b1981206557d44af")}</legend>
        <p>{localizedText(language, "pairs.m_4c4117869974cedb")}</p>
        {(content.literalBindings ?? []).map((b, i) => <div key={i}>{(['field', 'ownerValue', 'ruleValue', 'sourceLocator'] as const).map(k => <label key={k}>{k}<input maxLength={2000} value={b[k] ?? ''} onChange={e => edit({ literalBindings: content.literalBindings!.map((x, n) => n === i ? { ...x, [k]: e.target.value || null } : x) })}/></label>)}<button type="button" onClick={() => edit({ literalBindings: content.literalBindings!.filter((_, n) => n !== i) })}>{localizedText(language, "pairs.m_41b61dbd095a1e82")}</button></div>)}
        <button type="button" onClick={() => edit({ literalBindings: [...(content.literalBindings ?? []), { field: 'operation.jobType', ownerValue: null, ruleValue: null, sourceLocator: null }] })}>{localizedText(language, "pairs.m_6ac6a54c69213c81")}</button>
      </fieldset>
      <fieldset><legend>{localizedText(language, "pairs.m_69038f6a8425061a")}</legend>
        {(content.owningDocuments ?? []).map((p, i) => <div key={p.documentKind}><p>{p.documentKind}</p>{(['authorRole', 'verifierRole', 'sourceLocator'] as const).map(k => <label key={k}>{k}<input value={p[k] ?? ''} onChange={e => edit({ owningDocuments: content.owningDocuments!.map((x, n) => n === i ? { ...x, [k]: e.target.value || null } : x) })}/></label>)}<button type="button" onClick={() => edit({ owningDocuments: content.owningDocuments!.filter((_, n) => n !== i) })}>{localizedText(language, "pairs.m_4ba3770b95248c58")}</button></div>)}
        {['Operation', 'ServiceAuthorization', 'CargoPlan', 'WinchFitment', 'ClassPack', 'Msmd', 'CrewRoleAssessment'].filter(k => !content.owningDocuments?.some(p => p.documentKind === k)).map(k => <button type="button" key={k} onClick={() => edit({ owningDocuments: [...(content.owningDocuments ?? []), { documentKind: k, authorRole: null, verifierRole: null, sourceLocator: null }] })}>{localizedText(language, "pairs.m_f59a2764fe66562d")} · {k}</button>)}
      </fieldset>
      <CompliancePolicyEditor value={content} onChange={edit} language={language}/>
      <TugCalculationEditor language={language} value={content.tugCalculation ?? null} onChange={v => edit({ tugCalculation: v })}/>
      <button type="button" onClick={() => transition('DRAFT')}>{localizedText(language, "pairs.m_1da533f3cdfb3b33")}</button>
    </fieldset>
    <fieldset disabled={locked || dirty}><legend>{localizedText(language, "pairs.m_cfad7cfe48a0f655")}</legend>
      <button disabled={!data?.actions.includes('review') || current?.state !== 'DRAFT'} onClick={() => transition('IN_REVIEW')}>{localizedText(language, "pairs.m_1e37597983b930c9")}</button>
      <label>{localizedText(language, "pairs.m_a49a2ecee46f4622")}<select value={state} onChange={e => chooseState(e.target.value)}>{['ADOPTED', 'AWAITING', 'FUTURE_EFFECTIVE', 'IN_FORCE', 'SUPERSEDED'].map(s => <option key={s}>{s}</option>)}</select></label>
      <button disabled={!data?.actions.includes('qualify')} onClick={() => transition(state)}>{localizedText(language, "pairs.m_038db5c83a33d044")}</button>
      {data?.actions.includes('attest') && <><p>{localizedText(language, "pairs.m_8a73ccd70e32c31a")} {profile?.revision}</p><label>{localizedText(language, "pairs.m_a8bb7cd619125d09")}<input value={until} onChange={e => chooseUntil(e.target.value)}/></label><button disabled={!profile || !until} onClick={() => {
                const authorityId = data.authorities.find(a => a.action === 'attest')?.authorityId;
                if (authorityId && profile)
                    capture('fact-attestations', { authorityId, vesselId: vessel, profileRevision: profile.revision, profileVersion: profile.version, qualificationRevision: current?.revision, qualificationVersion: current?.version, fields: content.inputs?.filter(p => p.basis === 'HumanAttested' && api.profileInputs.includes(p.field)).map(p => p.field) ?? [], validUntil: until });
            }}>{localizedText(language, "pairs.m_55c9915df68385a0")}</button></>}
    </fieldset>
    <details><summary>{localizedText(language, "pairs.m_279e367745c63e95")}</summary>{historyRows.map(row => <p key={row.revision}>{row.revision} · {row.state} · {row.savedAt} · {row.hash} · {row.authorityHash}</p>)}<Button type="button" variant="outline" disabled={locked || !historyMore || historyRows.length === 0} onClick={() => void more()}>{localizedText(language, "pairs.m_ff1beddacf15fc11")}</Button></details>
    </>}
    <EnterpriseActions>
      <Button type="button" variant="outline" disabled={busy || !available} onClick={recheck}>{localizedText(language, 'pairs.m_542619365a11df9d')}</Button>
      {attempt && <Button type="button" disabled={!canRetry} onClick={() => void send(attempt)}>{localizedText(language, "pairs.m_2915875883556658")}</Button>}
      <Button type="button" variant="outline" disabled={busy || !!attempt} onClick={discard}>{localizedText(language, "pairs.m_7a7b7dd849b7bc4b")}</Button>
    </EnterpriseActions>
  </section>;
}
