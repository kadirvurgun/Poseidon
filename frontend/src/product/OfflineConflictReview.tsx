import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useId, useState } from 'react';
import { Button } from '../components/ui/button';
import { EnterpriseActions, EnterpriseField } from './EnterprisePatterns';
import './offline-conflict-review.css';
export type OfflineConflictValue = Readonly<{
    kind: 'unknown' | 'missing' | 'withheld';
}> | Readonly<{
    kind: 'known';
    text: string;
}>;
export type OfflineConflictChoice = 'keepServer' | 'useCaptured';
export type OfflineConflictField = Readonly<{
    key: string;
    label: string;
    captured: OfflineConflictValue;
    before?: OfflineConflictValue;
    server: OfflineConflictValue;
    choices: readonly OfflineConflictChoice[];
}>;
export type OfflineConflict = Readonly<{
    id: string;
    revision: string;
    contextKey: string;
    fields: readonly OfflineConflictField[];
}>;
// Send choices for the exact server conflict revision, not copied client values,
// an actor identity, a safety signatory or a claimed current aggregate version.
export type OfflineConflictResolution = Readonly<{
    conflictId: string;
    expectedRevision: string;
    reason: string;
    decisions: readonly Readonly<{
        field: string;
        choice: OfflineConflictChoice;
    }>[];
}>;
type Props = {
    language: AppLocale;
    contextKey: string;
    state: 'loading' | 'ready' | 'denied' | 'unavailable' | 'stale';
    conflict: OfflineConflict | null;
    canResolve: boolean;
    online: boolean;
    operation: 'idle' | 'submitting' | 'outcomeUnknown' | 'failed';
    reasonLimit: number;
    onResolve: (resolution: OfflineConflictResolution) => void;
    onCheckOutcome: () => void;
    onRetry?: () => void;
};
function validValue(value: OfflineConflictValue) {
    return value && (['missing', 'unknown', 'withheld'].includes(value.kind) || value.kind === 'known' && typeof value.text === 'string');
}
export function OfflineConflictReview(props: Props) {
    const en = props.language === 'en', conflict = props.conflict;
    if (props.state === 'loading')
        return <p role="status">{localizedText(en, "pairs.m_69bb14ee0aa62a89")}</p>;
    if (props.state === 'denied')
        return <p role="alert">{localizedText(en, "pairs.m_45d77cbecf2fb8f8")}</p>;
    if (props.state === 'stale' || !props.contextKey || conflict && conflict.contextKey !== props.contextKey)
        return <p role="status">
    {localizedText(en, "pairs.m_16c100983efd9f5a")}
  </p>;
    if (props.state !== 'ready' || !conflict || !conflict.id || !conflict.revision || !Array.isArray(conflict.fields) ||
        conflict.fields.length === 0 || new Set(conflict.fields.map(field => field?.key)).size !== conflict.fields.length ||
        conflict.fields.some(field => !field || !field.key || !field.label || !validValue(field.captured) || !validValue(field.server) || field.before !== undefined && !validValue(field.before) ||
            !Array.isArray(field.choices) || field.choices.some((choice: unknown) => !['keepServer', 'useCaptured'].includes(String(choice)))))
        return <div role="alert">
    <p>{localizedText(en, "pairs.m_0db37f4ec5ac2221")}</p>
    {props.onRetry && <Button variant="outline" onClick={props.onRetry}>{localizedText(en, "pairs.m_044bf27da41fda96")}</Button>}
  </div>;
    return <Review key={`${props.contextKey}/${conflict.id}/${conflict.revision}`} {...props} conflict={conflict}/>;
}
function Review({ language, conflict, canResolve, online, operation, reasonLimit, onResolve, onCheckOutcome }: Props & {
    conflict: OfflineConflict;
}) {
    const en = language === 'en', prefix = useId();
    const [reason, setReason] = useState('');
    const [choices, setChoices] = useState<Readonly<Record<string, OfflineConflictChoice>>>({});
    const configuredLimit = Number.isSafeInteger(reasonLimit) && reasonLimit > 0 && reasonLimit <= 2147483647;
    const locked = !canResolve || !online || !configuredLimit || operation === 'submitting' || operation === 'outcomeUnknown';
    const available = (field: OfflineConflictField, choice: OfflineConflictChoice) => field.captured.kind !== 'withheld' && field.server.kind !== 'withheld' && field.choices.includes(choice);
    const selected = (field: OfflineConflictField) => Object.hasOwn(choices, field.key) && available(field, choices[field.key]);
    const complete = conflict.fields.every(selected) && reason.trim().length > 0 && reason.length <= reasonLimit;
    const value = (item: OfflineConflictValue): string => {
        switch (item.kind) {
            case 'unknown': return localizedText(en, "pairs.m_a5a7675bd7000dbd");
            case 'missing': return localizedText(en, "pairs.m_d94e6bc1eb6a42fe");
            case 'withheld': return localizedText(en, "pairs.m_c854afb41d45423e");
            case 'known': return item.text === '' ? (localizedText(en, "pairs.m_5aa4eba40098e2bf")) : item.text;
        }
    };
    const choiceLabel = (choice: OfflineConflictChoice) => choice === 'keepServer'
        ? (localizedText(en, "pairs.m_22bccc78f7fd1331")) : (localizedText(en, "pairs.m_1f944ab38c31685f"));
    function submit() {
        if (locked || !complete)
            return;
        onResolve({ conflictId: conflict.id, expectedRevision: conflict.revision, reason,
            decisions: conflict.fields.map(field => ({ field: field.key, choice: choices[field.key] })) });
    }
    return <section className="offline-conflict" aria-label={localizedText(en, "pairs.m_c9fdcb02c179cf5b")} aria-busy={operation === 'submitting' || undefined}>
    <h3>{localizedText(en, "pairs.m_ceaf111d43319930")}</h3>
    <p>{localizedText(en, "pairs.m_54fa60c1cbb10938")}</p>
    {!canResolve && <p role="status">{localizedText(en, "pairs.m_f15379fa295f1fbe")}</p>}
    {!online && <p role="status">{localizedText(en, "pairs.m_dfdb786ea73fa344")}</p>}
    {!configuredLimit && <p role="alert">{localizedText(en, "pairs.m_d6d7d1705e000234")}</p>}
    {operation === 'outcomeUnknown' && <div role="alert">
      <p>{localizedText(en, "pairs.m_4038319912e8219d")}</p>
      <Button variant="outline" disabled={!online} onClick={onCheckOutcome}>{localizedText(en, "pairs.m_68e8ce0091671671")}</Button>
    </div>}
    {operation === 'failed' && <p role="alert">{localizedText(en, "pairs.m_b8b9c8f098d5ac9c")}</p>}
    <div className="offline-conflict__fields">
      {conflict.fields.map((field, index) => <fieldset key={field.key} disabled={locked}>
        <legend>{field.label}</legend>
        <dl>
          {field.before && <div><dt>{localizedText(en, "pairs.m_5eb8aabea964097a")}</dt><dd><bdi>{value(field.before)}</bdi></dd></div>}
          <div><dt>{localizedText(en, "pairs.m_b9441f91545d6179")}</dt><dd><bdi>{value(field.captured)}</bdi></dd></div>
          <div><dt>{localizedText(en, "pairs.m_20d3c06469870bdc")}</dt><dd><bdi>{value(field.server)}</bdi></dd></div>
          <div><dt>{localizedText(en, "pairs.m_6433e567701f733e")}</dt><dd><bdi>{selected(field)
                ? value(choices[field.key] === 'keepServer' ? field.server : field.captured)
                : (localizedText(en, "pairs.m_256b7daad72ae578"))}</bdi></dd></div>
        </dl>
        {(['keepServer', 'useCaptured'] as const).map(choice => <label key={choice} className="offline-conflict__choice">
          <input type="radio" name={`${prefix}-field-${index}`} value={choice} disabled={!available(field, choice)} checked={Object.hasOwn(choices, field.key) && choices[field.key] === choice} onChange={() => setChoices(prior => ({ ...prior, [field.key]: choice }))}/>
          {choiceLabel(choice)}
        </label>)}
        {field.choices.length === 0 || field.captured.kind === 'withheld' || field.server.kind === 'withheld'
                ? <p>{localizedText(en, "pairs.m_8ef9ae5ca5c67c8b")}</p> : null}
      </fieldset>)}
    </div>
    <EnterpriseField htmlFor={`${prefix}-reason`} label={localizedText(en, "pairs.m_80a51f709123310f")}>
    <textarea id={`${prefix}-reason`} value={reason} disabled={locked} aria-required="true" maxLength={configuredLimit ? reasonLimit : undefined} onChange={event => setReason(event.target.value)}/>
    </EnterpriseField><EnterpriseActions aria-label={localizedText(en, "pairs.m_a52c0af3edc8fbce")}><Button disabled={locked || !complete} onClick={submit}>{localizedText(en, "pairs.m_01578de8e65a9238")}</Button></EnterpriseActions>
  </section>;
}
