import { sourceText, localizedLabel } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { Button } from '../components/ui/button';
import { EnterpriseActions, EnterpriseField, EnterpriseFormGrid } from './EnterprisePatterns';
import { CertificateMetadataForm } from './CertificateMetadataForm';
import { CertificateDocumentPicker } from './CertificateDocumentPicker';
import { CertificateRenewalForm } from './CertificateRenewalForm';
import { CertificateSummary } from './CertificateSummary';
import { CertificateRenewalWorkflowPanel } from './CertificateRenewalWorkflowPanel';
import type { RenewalDto } from './certificates-api';
import type { CertificateWorkflowEditor, CertificateWorkspaceSource } from './certificate-workspace-model';
import type { CertificateRenewalContent, VesselCertificateContent } from './certificate-content';
import type { ApiError } from './api';
import { certificateLocationPath } from './certificate-location';
export function CertificateEditorView({ editor, source, vessel, language, editable, busy, frozen, failure, conflict, denied, saved, onCertificate, onRenewal, onSave, onCancel, onNavigate, onWorkflowDirty, onTransitioned }: {
    onWorkflowDirty?: (value: boolean) => void;
    onTransitioned?: (receipt: RenewalDto) => void;
    editor: CertificateWorkflowEditor;
    source: CertificateWorkspaceSource;
    vessel: string;
    language: AppLocale;
    editable: boolean;
    busy: boolean;
    frozen: boolean;
    failure: ApiError | null;
    conflict: boolean;
    denied: boolean;
    saved: boolean;
    onCertificate: (content: VesselCertificateContent) => void;
    onRenewal: (content: CertificateRenewalContent) => void;
    onSave: () => void;
    onCancel: () => void;
    onNavigate: (href: string) => void;
}) {
    const en = language === 'en';
    const changedCertificate = editor.kind === 'certificate' && editor.record && source.certificate && editor.record.version !== source.certificate.version;
    const permitted = editor.kind === 'certificate' ? !!source.caps?.canWriteVessel : !!source.renewalCaps?.canWrite;
    const actions = <EnterpriseActions aria-label={localizedText(en, "pairs.m_bbb1a7707e5cc0fb")}>
    <Button variant="outline" disabled={busy || frozen} onClick={onCancel}>{localizedText(en, "pairs.m_71be3ecd3d59de10")}</Button>
    {permitted && <Button type="submit" disabled={busy || denied || conflict || !frozen && !editable}>{frozen ? (localizedText(en, "pairs.m_5df89b5440ef63d0")) : busy ? (localizedText(en, "pairs.m_5b598bccc6931d54")) : (localizedText(en, "pairs.m_1057f80f39b6b640"))}</Button>}
  </EnterpriseActions>;
    return <section aria-label={localizedText(en, "pairs.m_0ad79cad6feb3a23")}>
    {saved && !editor.dirty && <p role="status">{localizedText(en, "pairs.m_b1d8a4435990c462")}</p>}
    {failure && <p role="alert">{frozen ? (localizedText(en, "pairs.m_3e3733db55d79db6")) : conflict ? (localizedText(en, "pairs.m_505d0414b148a03b")) : (localizedText(en, "pairs.m_f1ba6c8ff9168f37"))}</p>}
    {conflict && !failure && <p role="alert">{localizedText(en, "pairs.m_36006b5c85e6ed83")}</p>}
    {!editable && !frozen && <p>{localizedText(en, "pairs.m_ef3c6006e9676baa")}</p>}
    {editor.kind === 'certificate' ? <>
      <h2>{localizedText(en, "pairs.m_ea03b2507d92ca14")}</h2>
      {editor.record && <CertificateSummary row={changedCertificate ? { ...editor.record, currentRevision: false, canEdit: false } : editor.record} language={language}/>}
      <form noValidate aria-label={localizedText(en, "pairs.m_6d941c403a741aff")} aria-busy={busy} onSubmit={event => { event.preventDefault(); onSave(); }}>
        <EnterpriseFormGrid disabled={!editable}><legend>{localizedText(en, "pairs.m_1f53a4cdfbbf5985")}</legend>
          {([
                ['documentName', sourceText("en", "pairs.m_fd67fb611a66fe30"), sourceText("tr", "pairs.m_fd67fb611a66fe30")], ['regulatoryBasis', sourceText("en", "pairs.m_9bfd71e78b494846"), sourceText("tr", "pairs.m_9bfd71e78b494846")],
                ['declaredFlag', sourceText("en", "pairs.m_59684b2de6555add"), sourceText("tr", "pairs.m_59684b2de6555add")], ['surveyWindowStart', sourceText("en", "pairs.m_628e6b3597efbb09"), sourceText("tr", "pairs.m_628e6b3597efbb09")],
                ['surveyWindowEnd', sourceText("en", "pairs.m_1b853a028343f20e"), sourceText("tr", "pairs.m_1b853a028343f20e")],
            ] as const).map(([key, label, tr]) => <EnterpriseField key={key} htmlFor={'certificate-' + key} label={localizedLabel({ en: label, tr: tr }, en)}>
            <input id={'certificate-' + key} type={key === 'surveyWindowStart' ? 'date' : 'text'} maxLength={4096} value={editor.content[key] ?? ''} onChange={event => onCertificate({ ...editor.content, [key]: event.target.value || null })}/>
          </EnterpriseField>)}
          <EnterpriseField htmlFor="certificate-availability" label={localizedText(en, "pairs.m_f5f657ff5920e9f4")}>
            <select id="certificate-availability" value={editor.content.availability ?? ''} onChange={event => {
                const value = event.target.value;
                if (value === '' || value === 'Available' || value === 'Missing' || value === 'Review')
                    onCertificate({ ...editor.content, availability: value === '' ? null : value });
            }}>
              <option value="">{localizedText(en, "pairs.m_a5a7675bd7000dbd")}</option>
              {source.caps?.availabilityOptions.map(value => <option key={value} value={value}>{value === 'Available' ? (localizedText(en, "pairs.m_234d28f884ac32b6")) : value === 'Missing' ? (localizedText(en, "pairs.m_64db0f6687a1b350")) : (localizedText(en, "pairs.m_5c7e52f1cd883154"))}</option>)}
            </select>
          </EnterpriseField>
        </EnterpriseFormGrid>
        <CertificateMetadataForm value={editor.content.metadata} language={language} disabled={!editable} errors={failure?.errors} onChange={metadata => onCertificate({ ...editor.content, metadata })} documentPicker={<CertificateDocumentPicker vessel={vessel} owner={editor.owner} value={editor.content.document} language={language} disabled={!editable} onChange={document => onCertificate({ ...editor.content, document, metadata: { ...editor.content.metadata, documentReference: document?.id ?? null } })}/>}/>
        {actions}
      </form>
      {editor.record && <EnterpriseActions align="start" aria-label={localizedText(en, "pairs.m_5925dac72aaac1f9")}>
        <Button variant="outline" disabled={busy || frozen || !source.renewalCaps?.canWrite || !editor.record.currentRevision || editor.record.state === 'SUPERSEDED'} onClick={() => onNavigate(certificateLocationPath({ page: 'renewal', vessel, id: null, certificateId: editor.id, offset: 0 }))}>{localizedText(en, "pairs.m_b562720e1bac5a5e")}</Button>
      </EnterpriseActions>}
    </> : <>
      <h2>{localizedText(en, "pairs.m_60ddeb550474f1f0")}</h2>
      <CertificateSummary row={editor.certificate} language={language}/>
      {editor.record && (editor.record.certificateRevision !== editor.certificate.revision || editor.record.certificateVersion !== editor.certificate.version) && <p role="alert">{localizedText(en, "pairs.m_fb88f406a071208b")}</p>}
      <form noValidate aria-label={localizedText(en, "pairs.m_ac7c39e0cce4206a")} aria-busy={busy} onSubmit={event => { event.preventDefault(); onSave(); }}>
        <CertificateRenewalForm value={editor.content} vessel={vessel} owner={editor.owner} certificateId={editor.record?.certificateId ?? editor.certificate.id} ownerOptions={source.renewalCaps?.ownerOptions ?? []} disabled={!editable} language={language} errors={failure?.errors} onChange={onRenewal}/>
        {actions}
      </form>
      <p>{localizedText(en, "pairs.m_7e4e96172f321199")}</p>
      {editor.record ? <CertificateRenewalWorkflowPanel key={editor.owner + '/' + editor.record.id + '/' + editor.record.version} vessel={vessel} record={editor.record} enabled={!busy && !frozen && !editor.dirty && !denied && !conflict} language={language} onDirty={onWorkflowDirty ?? (() => undefined)} onApplied={receipt => onTransitioned?.(receipt)}/>
        : <Button disabled>{localizedText(en, "pairs.m_8cb7f72fb9fa00d9")}</Button>}
    </>}
  </section>;
}
