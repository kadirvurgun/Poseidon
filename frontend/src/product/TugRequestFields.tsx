import { sourceText, localizedTuple } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { EnterpriseField, EnterpriseFormGrid } from './EnterprisePatterns';
import { tugLabel } from './tug-labels';
import type { TugContext, TugInputs } from './tug-api';
type Props = {
    inputs: TugInputs;
    context: TugContext;
    language: AppLocale;
    disabled: boolean;
    errors: Record<string, string[]>;
    onChange: (key: keyof TugInputs, value: string | number | null) => void;
};
const labels = {
    customerRef: [sourceText("tr", "pairs.m_aef3f92b82b79438"), sourceText("en", "pairs.m_aef3f92b82b79438")], assistedVessel: [sourceText("tr", "pairs.m_135f6f7818069194"), sourceText("en", "pairs.m_135f6f7818069194")], assistedVesselImo: [sourceText("tr", "pairs.m_e8bbcf27d2ac3770"), sourceText("en", "pairs.m_e8bbcf27d2ac3770")], operationArea: [sourceText("tr", "pairs.m_6c454ca04f132394"), sourceText("en", "pairs.m_6c454ca04f132394")], pickupPoint: [sourceText("tr", "pairs.m_5ef40c52d6c3bc7a"), sourceText("en", "pairs.m_5ef40c52d6c3bc7a")], destinationPoint: [sourceText("tr", "pairs.m_a5633b4fdbdc5ef0"), sourceText("en", "pairs.m_a5633b4fdbdc5ef0")], specialRequirements: [sourceText("tr", "pairs.m_ace74de96d335c55"), sourceText("en", "pairs.m_ace74de96d335c55")], notes: [sourceText("tr", "pairs.m_0fb9c90ba8619153"), sourceText("en", "pairs.m_0fb9c90ba8619153")],
    requestSource: [sourceText("tr", "pairs.m_b556ace00c560a04"), sourceText("en", "pairs.m_b556ace00c560a04")], jobType: [sourceText("tr", "pairs.m_4936290c1e33377c"), sourceText("en", "pairs.m_4936290c1e33377c")], priority: [sourceText("tr", "pairs.m_9077f204711545d1"), sourceText("en", "pairs.m_9077f204711545d1")], requestOwner: [sourceText("tr", "pairs.m_a93447b5aa0d6111"), sourceText("en", "pairs.m_a93447b5aa0d6111")], requestedStart: [sourceText("tr", "pairs.m_26b149ecb9e6e550"), sourceText("en", "pairs.m_26b149ecb9e6e550")], requestedEnd: [sourceText("tr", "pairs.m_f10ec0cbf0cd2313"), sourceText("en", "pairs.m_f10ec0cbf0cd2313")], requiredBollardPull: [sourceText("tr", "pairs.m_0887f8913a168f53"), sourceText("en", "pairs.m_0887f8913a168f53")], requiredTugCount: [sourceText("tr", "pairs.m_f65b450b39dd7b33"), sourceText("en", "pairs.m_f65b450b39dd7b33")],
} satisfies Record<keyof TugInputs, [
    string,
    string
]>;
const wireKey = (key: string) => key.replace(/[A-Z]/g, x => `_${x.toLowerCase()}`);
export function TugRequestFields({ inputs, context, language, disabled, errors, onChange }: Props) {
    const en = language === 'en';
    const field = (key: keyof TugInputs, options?: string[]) => {
        const id = `tug-request-${key}`, error = errors[key] ?? errors[wireKey(key)] ?? errors[`inputs.${key}`];
        const describedBy = error?.length ? `${id}-error` : undefined;
        const common = { id, 'aria-invalid': error?.length ? true as const : undefined, 'aria-describedby': describedBy };
        return <EnterpriseField key={key} htmlFor={id} label={localizedTuple(labels[key], en, 1)} className={['notes', 'specialRequirements'].includes(key) ? 'tug-field-wide' : undefined}>
      {options ? <select {...common} value={inputs[key] ?? ''} onChange={event => onChange(key, event.target.value || null)}><option value="">{localizedText(en, "pairs.m_a5a7675bd7000dbd")}</option>{options.map(value => <option key={value} value={value}>{tugLabel(value, language)}</option>)}</select>
                : ['notes', 'specialRequirements'].includes(key) ? <textarea {...common} rows={3} maxLength={2000} value={String(inputs[key] ?? '')} onChange={event => onChange(key, event.target.value || null)}/>
                    : key === 'requestedStart' || key === 'requestedEnd' ? <input {...common} type="datetime-local" step="1" value={inputs[key]?.replace(/Z$/, '').slice(0, 19) ?? ''} onChange={event => onChange(key, event.target.value ? `${event.target.value.length === 16 ? event.target.value + ':00' : event.target.value}Z` : null)}/>
                        : key === 'requiredTugCount' ? <input {...common} type="number" min="1" max="1000" step="1" value={inputs[key] ?? ''} onChange={event => onChange(key, event.target.value ? Number(event.target.value) : null)}/>
                            : <input {...common} inputMode={key === 'requiredBollardPull' ? 'decimal' : undefined} maxLength={key === 'requiredBollardPull' ? 11 : 200} value={String(inputs[key] ?? '')} onChange={event => onChange(key, event.target.value || null)}/>}
      {error?.length ? <p id={`${id}-error`} role="alert">{localizedText(en, "pairs.m_f6815c0301b68ca7")}</p> : null}
    </EnterpriseField>;
    };
    return <>
    <p className="tug-form-guidance">{localizedText(en, "pairs.m_69c3513841c0e08b")}</p>
    <EnterpriseFormGrid disabled={disabled}><legend>{localizedText(en, "pairs.m_8ce5bfa631e795c0")}</legend>{field('requestSource', context.requestSources)}{field('customerRef')}{field('assistedVessel')}{field('assistedVesselImo')}{field('jobType', context.jobTypes)}{field('priority', context.priorities)}{field('requestOwner', context.owners)}</EnterpriseFormGrid>
    <EnterpriseFormGrid disabled={disabled}><legend>{localizedText(en, "pairs.m_5d8280b3de1f7158")}</legend>{field('requestedStart')}{field('requestedEnd')}{field('operationArea')}{field('pickupPoint')}{field('destinationPoint')}</EnterpriseFormGrid>
    <EnterpriseFormGrid disabled={disabled}><legend>{localizedText(en, "pairs.m_6412a72ecbdf54dd")}</legend>{field('requiredBollardPull')}{field('requiredTugCount')}{field('specialRequirements')}{field('notes')}</EnterpriseFormGrid>
  </>;
}
