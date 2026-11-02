import { useId } from 'react'
import './import-validation-results.css'

// Authorized server projection only: no uploaded cell values, private keys or raw
// server diagnostics. The workspace maps generated DTOs and uses fresh scoped APIs.
export type ImportRowIssue = Readonly<{ code: string; fieldOrdinal: number | null }>
export type ImportValidationRow = Readonly<{ rowNumber: number; outcome: 'valid' | 'rejected'; issues: readonly ImportRowIssue[] }>
export type ImportValidationPage = Readonly<{
  previewId: string
  revision: string
  templateVersion: string
  contextKey: string
  totalRows: number
  validRows: number
  rejectedRows: number
  offset: number
  limit: number
  hasMore: boolean
  rows: readonly ImportValidationRow[]
}>
type Props = {
  language: 'tr' | 'en'
  contextKey: string
  state: 'loading' | 'ready' | 'denied' | 'unavailable' | 'stale'
  page: ImportValidationPage | null
  fieldLabels: readonly string[]
  busy?: boolean
  canDownloadRejectReport: boolean
  onPage: (offset: number) => void
  onDownloadRejectReport: () => void
  onRetry?: () => void
}

const issues: Readonly<Record<string, readonly [string, string]>> = Object.freeze({
  required: ['Zorunlu değer eksik.', 'A required value is missing.'],
  invalid_format: ['Değer biçimi geçersiz.', 'The value format is invalid.'],
  duplicate: ['Tekrarlanan kayıt anahtarı.', 'The record key is duplicated.'],
  conflict: ['Geçerli kayıtla çakışıyor.', 'Conflicts with the current record.'],
  unresolved_reference: ['İzin verilen referans bulunamadı.', 'An allowed reference could not be resolved.'],
  unsupported_value: ['Değer bu şablon için desteklenmiyor.', 'The value is not supported by this template.'],
  domain_validation: ['Kayıt iş kuralını karşılamıyor.', 'The record does not satisfy a domain rule.'],
})
const natural = (value: number) => Number.isSafeInteger(value) && value >= 0

function validPage(page: ImportValidationPage, fieldLabels: readonly string[]) {
  if (!page.previewId || !page.revision || !page.templateVersion || !Array.isArray(page.rows) ||
    ![page.totalRows, page.validRows, page.rejectedRows, page.offset, page.limit].every(natural) ||
    page.limit === 0 || page.offset > page.totalRows || page.validRows + page.rejectedRows !== page.totalRows ||
    page.rows.length > page.limit || page.rows.length > page.totalRows - page.offset ||
    page.totalRows > 0 && page.rows.length === 0 ||
    page.hasMore !== (page.offset + page.rows.length < page.totalRows)) return false
  const seen = new Set<number>()
  let prior = 0, valid = 0, rejected = 0
  for (const row of page.rows) {
    if (!row || !natural(row.rowNumber) || row.rowNumber === 0 || seen.has(row.rowNumber) || row.rowNumber <= prior ||
      !Array.isArray(row.issues) || !['valid', 'rejected'].includes(row.outcome)) return false
    if (row.outcome === 'valid') { if (row.issues.length > 0) return false; valid++ }
    else { if (row.issues.length === 0) return false; rejected++ }
    for (const issue of row.issues) if (!issue || typeof issue.code !== 'string' ||
      issue.fieldOrdinal !== null && (!natural(issue.fieldOrdinal) || issue.fieldOrdinal >= fieldLabels.length)) return false
    seen.add(row.rowNumber); prior = row.rowNumber
  }
  return valid <= page.validRows && rejected <= page.rejectedRows
}

export function ImportValidationResults(props: Props) {
  const en = props.language === 'en'
  if (props.state === 'loading') return <p role="status">{en ? 'Validating import rows…' : 'İçe aktarma satırları doğrulanıyor…'}</p>
  if (props.state === 'denied') return <p role="alert">{en ? 'Import results are not permitted.' : 'İçe aktarma sonuçlarına erişim yetkiniz yok.'}</p>
  if (props.state === 'stale' || !props.contextKey || props.page && props.page.contextKey !== props.contextKey) return <p role="status">
    {en ? 'Validate again for the current session, source and template.' : 'Geçerli oturum, kaynak ve şablon için yeniden doğrulayın.'}
  </p>
  if (props.state !== 'ready' || !props.page || !validPage(props.page, props.fieldLabels)) return <div role="alert">
    <p>{en ? 'Import results could not be loaded.' : 'İçe aktarma sonuçları yüklenemedi.'}</p>
    {props.onRetry && <button type="button" onClick={props.onRetry}>{en ? 'Retry' : 'Tekrar dene'}</button>}
  </div>
  return <Results key={`${props.contextKey}/${props.page.previewId}/${props.page.revision}`} {...props} page={props.page} />
}

function Results({ language, page, fieldLabels, busy, canDownloadRejectReport, onDownloadRejectReport, onPage }: Props & { page: ImportValidationPage }) {
  const prefix = useId(), en = language === 'en'
  const format = (value: number) => value.toLocaleString(en ? 'en-GB' : 'tr-TR')
  return <section className="import-results" aria-label={en ? 'Import validation results' : 'İçe aktarma doğrulama sonuçları'} aria-busy={busy || undefined}>
    <h3>{en ? 'Validation preview' : 'Doğrulama önizlemesi'}</h3>
    <p id={`${prefix}-notice`}>{en
      ? 'This preview has not imported records. Validation does not verify declarations or grant operational approval. Import must recheck current permissions and records.'
      : 'Bu önizleme kayıtları içe aktarmadı. Doğrulama beyanları doğrulanmış kanıt yapmaz veya operasyonel onay vermez. İçe aktarma geçerli yetkileri ve kayıtları yeniden kontrol etmelidir.'}</p>
    <p>{en ? 'Template version' : 'Şablon sürümü'}: <bdi>{page.templateVersion}</bdi></p>
    <dl className="import-results__counts">
      <div><dt>{en ? 'Total rows' : 'Toplam satır'}</dt><dd>{format(page.totalRows)}</dd></div>
      <div><dt>{en ? 'Valid rows' : 'Geçerli satır'}</dt><dd>{format(page.validRows)}</dd></div>
      <div><dt>{en ? 'Rejected rows' : 'Reddedilen satır'}</dt><dd>{format(page.rejectedRows)}</dd></div>
    </dl>
    {page.totalRows === 0 ? <p role="status">{en ? 'There are no source rows to import.' : 'İçe aktarılacak kaynak satırı bulunamadı.'}</p> : <>
      <div className="import-results__scroll" role="region" aria-label={en ? 'Row validation table' : 'Satır doğrulama tablosu'} tabIndex={0}>
        <table aria-describedby={`${prefix}-notice`}>
          <caption>{en ? 'Source row outcomes' : 'Kaynak satırı sonuçları'}</caption>
          <thead><tr><th scope="col">{en ? 'Source row' : 'Kaynak satırı'}</th><th scope="col">{en ? 'Result' : 'Sonuç'}</th><th scope="col">{en ? 'Validation details' : 'Doğrulama ayrıntıları'}</th></tr></thead>
          <tbody>{page.rows.map(row => <tr key={row.rowNumber}>
            <th scope="row">{format(row.rowNumber)}</th>
            <td>{row.outcome === 'valid' ? (en ? 'Valid for preview' : 'Önizleme için geçerli') : (en ? 'Rejected' : 'Reddedildi')}</td>
            <td>{row.issues.length === 0 ? '—' : <ul>{row.issues.map((issue, ordinal) => <li key={ordinal}>
              {issue.fieldOrdinal !== null && <><bdi>{fieldLabels[issue.fieldOrdinal]}</bdi>: </>}
              {Object.hasOwn(issues, issue.code) ? issues[issue.code][en ? 1 : 0]
                : (en ? 'The row could not be validated. Review the authorized reject report.' : 'Satır doğrulanamadı. Yetkili ret raporunu inceleyin.')}
            </li>)}</ul>}</td>
          </tr>)}</tbody>
        </table>
      </div>
      <p aria-live="polite">{en ? 'Showing' : 'Gösterilen'} {format(page.offset + 1)}–{format(page.offset + page.rows.length)} / {format(page.totalRows)}</p>
      <nav className="import-results__actions" aria-label={en ? 'Result pages' : 'Sonuç sayfaları'}>
        <button type="button" disabled={busy || page.offset === 0} onClick={() => onPage(Math.max(0, page.offset - page.limit))}>{en ? 'Previous page' : 'Önceki sayfa'}</button>
        <button type="button" disabled={busy || !page.hasMore} onClick={() => onPage(page.offset + page.rows.length)}>{en ? 'Next page' : 'Sonraki sayfa'}</button>
      </nav>
    </>}
    {canDownloadRejectReport && page.rejectedRows > 0 && <button type="button" disabled={busy} onClick={onDownloadRejectReport}>
      {en ? 'Download reject report' : 'Ret raporunu indir'}
    </button>}
  </section>
}
