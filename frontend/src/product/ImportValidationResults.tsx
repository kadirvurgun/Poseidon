import { sourceText, localizedTuple } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
import { type Locale as AppLocale } from "../i18n";
import { useId } from 'react';
import './import-validation-results.css';
// Authorized server projection only: no uploaded cell values, private keys or raw
// server diagnostics. The workspace maps generated DTOs and uses fresh scoped APIs.
export type ImportRowIssue = Readonly<{
    code: string;
    fieldOrdinal: number | null;
}>;
export type ImportValidationRow = Readonly<{
    rowNumber: number;
    outcome: 'valid' | 'rejected';
    issues: readonly ImportRowIssue[];
}>;
export type ImportValidationPage = Readonly<{
    previewId: string;
    revision: string;
    templateVersion: string;
    contextKey: string;
    totalRows: number;
    validRows: number;
    rejectedRows: number;
    offset: number;
    limit: number;
    hasMore: boolean;
    rows: readonly ImportValidationRow[];
}>;
type Props = {
    language: AppLocale;
    contextKey: string;
    state: 'loading' | 'ready' | 'denied' | 'unavailable' | 'stale';
    page: ImportValidationPage | null;
    fieldLabels: readonly string[];
    busy?: boolean;
    canDownloadRejectReport: boolean;
    onPage: (offset: number) => void;
    onDownloadRejectReport: () => void;
    onRetry?: () => void;
};
const issues: Readonly<Record<string, readonly [
    string,
    string
]>> = Object.freeze({
    required: [sourceText("tr", "pairs.m_7a9b253e6cacbc58"), sourceText("en", "pairs.m_7a9b253e6cacbc58")],
    invalid_format: [sourceText("tr", "pairs.m_72145c82931d28c4"), sourceText("en", "pairs.m_72145c82931d28c4")],
    duplicate: [sourceText("tr", "pairs.m_318aa8f8ade1ee50"), sourceText("en", "pairs.m_318aa8f8ade1ee50")],
    conflict: [sourceText("tr", "pairs.m_525ef7ba2aa08d03"), sourceText("en", "pairs.m_525ef7ba2aa08d03")],
    unresolved_reference: [sourceText("tr", "pairs.m_5098351d5050a09f"), sourceText("en", "pairs.m_5098351d5050a09f")],
    unsupported_value: [sourceText("tr", "pairs.m_f3506800d5ee4bf9"), sourceText("en", "pairs.m_f3506800d5ee4bf9")],
    domain_validation: [sourceText("tr", "pairs.m_5da0eb068ff45d92"), sourceText("en", "pairs.m_5da0eb068ff45d92")],
});
const natural = (value: number) => Number.isSafeInteger(value) && value >= 0;
function validPage(page: ImportValidationPage, fieldLabels: readonly string[]) {
    if (!page.previewId || !page.revision || !page.templateVersion || !Array.isArray(page.rows) ||
        ![page.totalRows, page.validRows, page.rejectedRows, page.offset, page.limit].every(natural) ||
        page.limit === 0 || page.offset > page.totalRows || page.validRows + page.rejectedRows !== page.totalRows ||
        page.rows.length > page.limit || page.rows.length > page.totalRows - page.offset ||
        page.totalRows > 0 && page.rows.length === 0 ||
        page.hasMore !== (page.offset + page.rows.length < page.totalRows))
        return false;
    const seen = new Set<number>();
    let prior = 0, valid = 0, rejected = 0;
    for (const row of page.rows) {
        if (!row || !natural(row.rowNumber) || row.rowNumber === 0 || seen.has(row.rowNumber) || row.rowNumber <= prior ||
            !Array.isArray(row.issues) || !['valid', 'rejected'].includes(row.outcome))
            return false;
        if (row.outcome === 'valid') {
            if (row.issues.length > 0)
                return false;
            valid++;
        }
        else {
            if (row.issues.length === 0)
                return false;
            rejected++;
        }
        for (const issue of row.issues)
            if (!issue || typeof issue.code !== 'string' ||
                issue.fieldOrdinal !== null && (!natural(issue.fieldOrdinal) || issue.fieldOrdinal >= fieldLabels.length))
                return false;
        seen.add(row.rowNumber);
        prior = row.rowNumber;
    }
    return valid <= page.validRows && rejected <= page.rejectedRows;
}
export function ImportValidationResults(props: Props) {
    const en = props.language === 'en';
    if (props.state === 'loading')
        return <p role="status">{localizedText(en, "pairs.m_bb01e9507b20dc0c")}</p>;
    if (props.state === 'denied')
        return <p role="alert">{localizedText(en, "pairs.m_4c2871a2872549a5")}</p>;
    if (props.state === 'stale' || !props.contextKey || props.page && props.page.contextKey !== props.contextKey)
        return <p role="status">
    {localizedText(en, "pairs.m_ff86721d81bf5c66")}
  </p>;
    if (props.state !== 'ready' || !props.page || !validPage(props.page, props.fieldLabels))
        return <div role="alert">
    <p>{localizedText(en, "pairs.m_1d0602e4441c40d3")}</p>
    {props.onRetry && <button type="button" onClick={props.onRetry}>{localizedText(en, "pairs.m_044bf27da41fda96")}</button>}
  </div>;
    return <Results key={`${props.contextKey}/${props.page.previewId}/${props.page.revision}`} {...props} page={props.page}/>;
}
function Results({ language, page, fieldLabels, busy, canDownloadRejectReport, onDownloadRejectReport, onPage }: Props & {
    page: ImportValidationPage;
}) {
    const prefix = useId(), en = language === 'en';
    const format = (value: number) => value.toLocaleString(en ? 'en-GB' : 'tr-TR');
    return <section className="import-results" aria-label={localizedText(en, "pairs.m_ff295213612daaca")} aria-busy={busy || undefined}>
    <h3>{localizedText(en, "pairs.m_cfa1aa6d2a83459f")}</h3>
    <p id={`${prefix}-notice`}>{localizedText(en, "pairs.m_9a8d49b29d4cc3b4")}</p>
    <p>{localizedText(en, "pairs.m_f4ab4b3c859b1b5d")}: <bdi>{page.templateVersion}</bdi></p>
    <dl className="import-results__counts">
      <div><dt>{localizedText(en, "pairs.m_274815d2f018c787")}</dt><dd>{format(page.totalRows)}</dd></div>
      <div><dt>{localizedText(en, "pairs.m_b17c2cbb0590215a")}</dt><dd>{format(page.validRows)}</dd></div>
      <div><dt>{localizedText(en, "pairs.m_a7b0cc08dd730597")}</dt><dd>{format(page.rejectedRows)}</dd></div>
    </dl>
    {page.totalRows === 0 ? <p role="status">{localizedText(en, "pairs.m_af06cfad2f54df8c")}</p> : <>
      <div className="import-results__scroll" role="region" aria-label={localizedText(en, "pairs.m_c7bbb94340fe6ec2")} tabIndex={0}>
        <table aria-describedby={`${prefix}-notice`}>
          <caption>{localizedText(en, "pairs.m_3a72819265c3457c")}</caption>
          <thead><tr><th scope="col">{localizedText(en, "pairs.m_c872871c60cb759e")}</th><th scope="col">{localizedText(en, "pairs.m_7e3fe0cfaf147171")}</th><th scope="col">{localizedText(en, "pairs.m_d159417b7b49aa08")}</th></tr></thead>
          <tbody>{page.rows.map(row => <tr key={row.rowNumber}>
            <th scope="row">{format(row.rowNumber)}</th>
            <td>{row.outcome === 'valid' ? (localizedText(en, "pairs.m_9a33c62faffd5873")) : (localizedText(en, "pairs.m_116c426da11868d8"))}</td>
            <td>{row.issues.length === 0 ? '—' : <ul>{row.issues.map((issue, ordinal) => <li key={ordinal}>
              {issue.fieldOrdinal !== null && <><bdi>{fieldLabels[issue.fieldOrdinal]}</bdi>: </>}
              {Object.hasOwn(issues, issue.code) ? localizedTuple(issues[issue.code], en, 1) : (localizedText(en, "pairs.m_7b405f80b6ce50ce"))}
            </li>)}</ul>}</td>
          </tr>)}</tbody>
        </table>
      </div>
      <p aria-live="polite">{localizedText(en, "pairs.m_2029bec39134a8f7")} {format(page.offset + 1)}–{format(page.offset + page.rows.length)} / {format(page.totalRows)}</p>
      <nav className="import-results__actions" aria-label={localizedText(en, "pairs.m_68af821650a4ee28")}>
        <button type="button" disabled={busy || page.offset === 0} onClick={() => onPage(Math.max(0, page.offset - page.limit))}>{localizedText(en, "pairs.m_c38de53e47945ea0")}</button>
        <button type="button" disabled={busy || !page.hasMore} onClick={() => onPage(page.offset + page.rows.length)}>{localizedText(en, "pairs.m_480a882a980c64b5")}</button>
      </nav>
    </>}
    {canDownloadRejectReport && page.rejectedRows > 0 && <button type="button" disabled={busy} onClick={onDownloadRejectReport}>
      {localizedText(en, "pairs.m_7e6a0f45ef019f79")}
    </button>}
  </section>;
}
