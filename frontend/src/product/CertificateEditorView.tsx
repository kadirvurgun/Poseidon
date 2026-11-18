import { Button } from '../components/ui/button'
import { EnterpriseActions, EnterpriseField, EnterpriseFormGrid } from './EnterprisePatterns'
import { CertificateMetadataForm } from './CertificateMetadataForm'
import { CertificateDocumentPicker } from './CertificateDocumentPicker'
import { CertificateRenewalForm } from './CertificateRenewalForm'
import { CertificateSummary } from './CertificateSummary'
import type { CertificateWorkflowEditor, CertificateWorkspaceSource } from './certificate-workspace-model'
import type { CertificateRenewalContent, VesselCertificateContent } from './certificate-content'
import type { ApiError } from './api'
import { certificateLocationPath } from './certificate-location'

export function CertificateEditorView({ editor, source, vessel, language, editable, busy, frozen, failure, conflict, denied, saved, onCertificate, onRenewal, onSave, onCancel, onNavigate }: {
  editor: CertificateWorkflowEditor; source: CertificateWorkspaceSource; vessel: string; language: 'en' | 'tr'; editable: boolean; busy: boolean; frozen: boolean; failure: ApiError | null; conflict: boolean; denied: boolean; saved: boolean
  onCertificate: (content: VesselCertificateContent) => void; onRenewal: (content: CertificateRenewalContent) => void; onSave: () => void; onCancel: () => void; onNavigate: (href: string) => void
}) {
  const en = language === 'en'
  const changedCertificate = editor.kind === 'certificate' && editor.record && source.certificate && editor.record.version !== source.certificate.version
  const permitted = editor.kind === 'certificate' ? !!source.caps?.canWriteVessel : !!source.renewalCaps?.canWrite
  const actions = <EnterpriseActions aria-label={en ? 'Certificate form actions' : 'Sertifika form işlemleri'}>
    <Button variant="outline" disabled={busy || frozen} onClick={onCancel}>{en ? 'Cancel editing' : 'Düzenlemeyi iptal et'}</Button>
    {permitted && <Button type="submit" disabled={busy || denied || conflict || !frozen && !editable}>{frozen ? (en ? 'Retry exact certificate command' : 'Aynı sertifika komutunu tekrar dene') : busy ? (en ? 'Saving…' : 'Kaydediliyor…') : (en ? 'Save unverified draft' : 'Doğrulanmamış taslağı kaydet')}</Button>}
  </EnterpriseActions>
  return <section aria-label={en ? 'Certificate workflow editor' : 'Sertifika iş akışı düzenleyici'}>
    {saved && !editor.dirty && <p role="status">{en ? 'Draft outcome recorded. Current source access is checked separately; verification remains open.' : 'Taslak sonucu kaydedildi. Güncel kaynak erişimi ayrıca kontrol edilir; doğrulama açıktır.'}</p>}
    {failure && <p role="alert">{frozen ? (en ? 'Outcome unknown. Keep the exact original key, body and identity; explicitly retry after authorization.' : 'Sonuç bilinmiyor. Özgün anahtar, içerik ve kimliği koruyun; yetkilendirme sonrası açıkça tekrar deneyin.') : conflict ? (en ? 'Saved source changed. Local inputs and original base remain retained; explicitly reload or resolve.' : 'Kayıtlı kaynak değişti. Yerel girdiler ve özgün temel korunur; açıkça yenileyin veya çözün.') : (en ? 'Check current permission, declared values and exact source pins.' : 'Güncel yetkiyi, beyan değerlerini ve kesin kaynak sabitlemelerini kontrol edin.')}</p>}
    {conflict && !failure && <p role="alert">{en ? 'Current source changed. Local fields and their original base remain retained; explicitly discard/reload before another save.' : 'Güncel kaynak değişti. Yerel alanlar ve özgün temel korunur; başka kayıt öncesi açıkça bırakıp yeniden okuyun.'}</p>}
    {!editable && !frozen && <p>{en ? 'Read-only source snapshot or current write authority unavailable.' : 'Salt okunur kaynak anlık kaydı veya güncel yazma yetkisi kullanılamıyor.'}</p>}
    {editor.kind === 'certificate' ? <>
      <h2>{en ? 'Vessel certificate declaration' : 'Araç sertifika beyanı'}</h2>
      {editor.record && <CertificateSummary row={changedCertificate ? { ...editor.record, currentRevision: false, canEdit: false } : editor.record} language={language} />}
      <form noValidate aria-label={en ? 'Certificate declaration form' : 'Sertifika beyan formu'} aria-busy={busy} onSubmit={event => { event.preventDefault(); onSave() }}>
        <EnterpriseFormGrid disabled={!editable}><legend>{en ? 'Declared document context' : 'Beyan edilen belge bağlamı'}</legend>
          {([
            ['documentName', 'Document name', 'Belge adı'], ['regulatoryBasis', 'Declared regulatory basis', 'Beyan edilen mevzuat dayanağı'],
            ['declaredFlag', 'Declared flag', 'Beyan edilen bayrak'], ['surveyWindowStart', 'Declared survey window start', 'Beyan edilen sörvey başlangıcı'],
            ['surveyWindowEnd', 'Declared survey window end (source text)', 'Beyan edilen sörvey bitişi (kaynak metni)'],
          ] as const).map(([key, label, tr]) => <EnterpriseField key={key} htmlFor={'certificate-' + key} label={en ? label : tr}>
            <input id={'certificate-' + key} type={key === 'surveyWindowStart' ? 'date' : 'text'} maxLength={4096} value={editor.content[key] ?? ''} onChange={event => onCertificate({ ...editor.content, [key]: event.target.value || null })} />
          </EnterpriseField>)}
          <EnterpriseField htmlFor="certificate-availability" label={en ? 'Declared availability' : 'Beyan edilen kullanılabilirlik'}>
            <select id="certificate-availability" value={editor.content.availability ?? ''} onChange={event => {
              const value = event.target.value
              if (value === '' || value === 'Available' || value === 'Missing' || value === 'Review') onCertificate({ ...editor.content, availability: value === '' ? null : value })
            }}>
              <option value="">{en ? 'Unknown' : 'Bilinmiyor'}</option>
              {source.caps?.availabilityOptions.map(value => <option key={value} value={value}>{value === 'Available' ? (en ? 'Declared available' : 'Beyan: kullanılabilir') : value === 'Missing' ? (en ? 'Declared missing' : 'Beyan: eksik') : (en ? 'Declared review needed' : 'Beyan: inceleme gerekli')}</option>)}
            </select>
          </EnterpriseField>
        </EnterpriseFormGrid>
        <CertificateMetadataForm value={editor.content.metadata} language={language} disabled={!editable} errors={failure?.errors} onChange={metadata => onCertificate({ ...editor.content, metadata })} documentPicker={
          <CertificateDocumentPicker vessel={vessel} owner={editor.owner} value={editor.content.document} language={language} disabled={!editable} onChange={document => onCertificate({ ...editor.content, document, metadata: { ...editor.content.metadata, documentReference: document?.id ?? null } })} />
        } />
        {actions}
      </form>
      {editor.record && <EnterpriseActions align="start" aria-label={en ? 'Certificate related work' : 'Sertifikaya bağlı işler'}>
        <Button variant="outline" disabled={busy || frozen || !source.renewalCaps?.canWrite || !editor.record.currentRevision || editor.record.state === 'SUPERSEDED'} onClick={() => onNavigate(certificateLocationPath({ page: 'renewal', vessel, id: null, certificateId: editor.id, offset: 0 }))}>{en ? 'Prepare renewal' : 'Yenileme hazırla'}</Button>
      </EnterpriseActions>}
    </> : <>
      <h2>{en ? 'Renewal preparation draft' : 'Yenileme hazırlık taslağı'}</h2>
      <CertificateSummary row={editor.certificate} language={language} />
      {editor.record && (editor.record.certificateRevision !== editor.certificate.revision || editor.record.certificateVersion !== editor.certificate.version) && <p role="alert">{en ? 'This renewal targets a different certificate revision. Its original pins remain unchanged; no automatic rebase is made.' : 'Bu yenileme farklı sertifika sürümünü hedefliyor. Özgün sabitlemeleri korunur; otomatik temel değişimi yapılmaz.'}</p>}
      <form noValidate aria-label={en ? 'Certificate renewal form' : 'Sertifika yenileme formu'} aria-busy={busy} onSubmit={event => { event.preventDefault(); onSave() }}>
        <CertificateRenewalForm value={editor.content} vessel={vessel} owner={editor.owner} certificateId={editor.record?.certificateId ?? editor.certificate.id} ownerOptions={source.renewalCaps?.ownerOptions ?? []} disabled={!editable} language={language} errors={failure?.errors} onChange={onRenewal} />
        {actions}
      </form>
      <p>{en ? 'Draft replacement links do not supersede the original. Approval, verification and source-authorized completion remain separate.' : 'Taslak değiştirme bağlantıları özgün kaydın yerine geçmez. Onay, doğrulama ve kaynakça yetkilendirilmiş tamamlama ayrıdır.'}</p>
      <Button disabled>{en ? 'Controlled renewal completion unavailable' : 'Kontrollü yenileme tamamlama kullanılamıyor'}</Button>
    </>}
  </section>
}
