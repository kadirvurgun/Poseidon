export type Kind = "vessels" | "certificates";
export type DraftData = Record<string, string>;
export interface Session {
  subject: string;
  tenantId: string;
  companyId: string;
  vesselIds: string[];
  synthetic: boolean;
}
export interface Draft {
  draftId: string;
  version: number;
  persistenceState: "DRAFT";
  submissionAllowed: false;
  data: DraftData;
}
export class ApiError extends Error {
  constructor(
    public status: number,
    public code: string,
    message: string,
    public errors: Record<string, string[]> = {},
  ) {
    super(message);
  }
}
function checkedDraft(value: Draft): Draft {
  if (
    !value ||
    typeof value.draftId !== "string" ||
    !Number.isSafeInteger(value.version) ||
    value.version < 1 ||
    value.persistenceState !== "DRAFT" ||
    value.submissionAllowed !== false ||
    !value.data ||
    typeof value.data !== "object" ||
    Array.isArray(value.data) ||
    Object.values(value.data).some((v) => typeof v !== "string")
  )
    throw new ApiError(
      0,
      "INVALID_RESPONSE",
      "Server response did not confirm a draft.",
    );
  return value;
}
export interface BrowseOptions { q?: string; flag?: string; vessel_type?: string; pageSize?: number; cursor?: string }
export interface DraftPage { items: Draft[]; nextCursor?: string | null; hasMore?: boolean }
export class Api {
  private invalidated = false;
  private activeMutations = 0;
  private logoutPending = false;
  private browserScope: string | null = null;
  invalidate() { this.invalidated = true; }
  constructor(
    public token: string,
    protected transport: typeof fetch = fetch,
    private mode: "bearer" | "bff" = "bearer",
  ) {}
  // One client owns the session: reserve mutations/logout before the first await.
  // Already-started writes finish normally; logout is rejected rather than pretending to cancel them.
  protected async request<T>(path: string, init: RequestInit = {}): Promise<T> {
    const mutation = init.method === "POST" && path !== "/logout";
    if (mutation && this.logoutPending) throw new ApiError(409, "SESSION_ENDING", "Sign-out is in progress; no new write was sent.");
    if (mutation) this.activeMutations++;
    try { return await this.send<T>(path, init); }
    finally { if (mutation) this.activeMutations--; }
  }
  protected async endSession(): Promise<void> {
    if (this.logoutPending || this.activeMutations > 0) throw new ApiError(409, "OPERATION_IN_PROGRESS", "Wait for the current operation before signing out. Its outcome has not been canceled.");
    this.logoutPending = true;
    try { await this.request("/logout", { method: "POST" }); this.invalidate(); }
    finally { this.logoutPending = false; }
  }
  private async send<T>(path: string, init: RequestInit = {}): Promise<T> {
    if (this.invalidated) throw new ApiError(401, "SESSION_ENDED", "Session ended.");
    const browser = this.mode === "bff";
    const scopedRequest = browser && path !== "/session";
    if (scopedRequest && !this.browserScope) throw new ApiError(401, "SESSION_REQUIRED", "Verify session first.");
    const csrf = browser && init.method === "POST" ? await this.antiforgery() : null;
    // A request waiting for CSRF must not resume after logout/expiry.
    if (this.invalidated) throw new ApiError(401, "SESSION_ENDED", "Session ended.");
    let response: Response;
    try {
      // Native browser fetch must not receive the Api instance as its receiver.
      const transport = this.transport;
      const url = browser ? (path === "/session" || path === "/logout" ? `/bff${path}` : path.startsWith("/inventory/") ? `/bff${path}` : `/bff/api/v1${path}`) : `/api/v1${path}`;
      response = await transport(url, {
        ...init,
        credentials: browser ? "same-origin" : "omit",
        redirect: "error",
        headers: {
          "Content-Type": "application/json",
          ...(csrf ? { "X-CSRF-TOKEN": csrf } : {}),
          ...(scopedRequest ? { "X-Poseidon-Scope": this.browserScope! } : {}),
          ...(!browser && this.token ? { Authorization: `Bearer ${this.token}` } : {}),
          ...init.headers,
          ...(browser && csrf && import.meta.env.VITE_ENABLE_UAT === "true"
            ? { "X-Poseidon-Uat-Origin": window.location.origin } : {}),
        },
      });
    } catch {
      throw new ApiError(
        0,
        "NETWORK_ERROR",
        "The server could not be reached. The save outcome may be unknown.",
      );
    }
    if (this.invalidated) throw new ApiError(401, "SESSION_ENDED", "Session ended.");
    const body = await response.json().catch(() => ({}));
    if (this.invalidated) throw new ApiError(401, "SESSION_ENDED", "Session ended.");
    if (browser && (response.status === 401 || body.code === "logout_scope_changed")) this.invalidate();
    if (!response.ok)
      throw new ApiError(
        response.status,
        body.code || "HTTP_ERROR",
        body.message || `HTTP ${response.status}`,
        body.errors,
      );
    return body as T;
  }
  async antiforgery(): Promise<string> {
    const transport = this.transport;
    const response = await transport("/bff/antiforgery", { credentials: "same-origin", redirect: "error", cache: "no-store" });
    if (!response.ok) throw new ApiError(response.status, "CSRF_UNAVAILABLE", "Request verification unavailable.");
    if (this.invalidated) throw new ApiError(401, "SESSION_ENDED", "Session ended.");
    const body = await response.json();
    if (this.invalidated) throw new ApiError(401, "SESSION_ENDED", "Session ended.");
    if (typeof body.requestToken !== "string" || !body.requestToken) throw new ApiError(0, "INVALID_RESPONSE", "Request verification unavailable.");
    return body.requestToken;
  }
  async session() {
    const active = await this.request<Session>("/session");
    if (this.mode === "bff") {
      if (!active || !active.subject || !active.tenantId || !active.companyId) throw new ApiError(0, "INVALID_SESSION", "Invalid session response.");
      const stamp = `${active.subject}:${active.tenantId}:${active.companyId}`;
      if (this.browserScope && this.browserScope !== stamp) {
        this.invalidate(); throw new ApiError(401, "SESSION_CHANGED", "Session changed.");
      }
      this.browserScope = stamp;
    }
    return active;
  }
  devSession(fixture: string) {
    return this.request<{ accessToken: string; session: Session }>(
      "/dev/session",
      { method: "POST", body: JSON.stringify({ fixture }) },
    );
  }
  evidenceCapabilities(targetType: EvidenceTarget, targetId: string) {
    return this.request<{enabled:boolean;canCapture:boolean;maxBytes?:number}>(`/evidence-intake/capabilities?${new URLSearchParams({targetType,targetId})}`);
  }
  async evidenceList(targetType:EvidenceTarget,targetId:string,cursor?:string) {
    const query=new URLSearchParams({targetType,targetId,...(cursor?{cursor}:{})});
    const page=await this.request<EvidencePage>(`/evidence-intake?${query}`);
    if(!Array.isArray(page.items)||typeof page.hasMore!=="boolean"||page.hasMore!==!!page.nextCursor||(page.nextCursor&&(typeof page.nextCursor!=="string"||page.nextCursor.length>512))) throw new ApiError(0,"INVALID_RESPONSE","Evidence page not confirmed.");
    page.items.forEach(item=>{checkedEvidence(item);if(item.targetType!==targetType||item.targetId!==targetId)throw new ApiError(0,"INVALID_RESPONSE","Evidence target mismatch.");});return page;
  }
  evidenceHistory(id:string,cursor?:string) { return this.request<InventoryHistoryPage>(`/evidence-intake/${encodeURIComponent(id)}/history${cursor?`?cursor=${encodeURIComponent(cursor)}`:""}`); }
  async evidenceStatus(id:string) { return checkedEvidence(await this.request<EvidenceRecord>(`/evidence-intake/${encodeURIComponent(id)}`)); }
  // Hold the shared logout barrier across ALL awaits, including CSRF and status recovery.
  async evidenceIntake(operation: EvidenceOperation, upload: boolean, onReserved: (record: EvidenceRecord)=>void = ()=>{}) {
    if(this.invalidated) throw new ApiError(401,"SESSION_ENDED","Session ended.");
    if(this.logoutPending) throw new ApiError(409,"SESSION_ENDING","Sign-out is in progress.");
    this.activeMutations++;
    try {
      let record:EvidenceRecord;
      if(operation.record) record=await this.evidenceStatus(operation.record.id);
      else {
        const metadata={targetType:operation.targetType,targetId:operation.targetId,filename:operation.file.name,declaredMime:operation.file.type,expectedBytes:operation.file.size,...operation.metadata};
        record=checkedEvidence(await this.request<EvidenceRecord>(operation.previous?`/evidence-intake/${encodeURIComponent(operation.previous.id)}/supersede`:"/evidence-intake",{method:"POST",headers:{"Idempotency-Key":operation.reserveKey,...(operation.previous?{"If-Match":`"${operation.previous.version}"`}:{})},body:JSON.stringify(operation.previous?{metadata,reason:operation.reason}:metadata)}));
      }
      if((operation.previous&&record.previousId!==operation.previous.id)||record.targetType!==operation.targetType || record.targetId!==operation.targetId || record.metadata.filename!==operation.file.name || record.metadata.declaredMime!==operation.file.type || record.metadata.expectedBytes!==operation.file.size) throw new ApiError(0,"INVALID_RESPONSE","Evidence reservation does not match the retained file and target.");
      operation.record=record; onReserved(record);
      // Recovery is status-only. Sending bytes requires a separate explicit user action.
      if(upload && record.state==="RESERVED") {
        record=checkedEvidence(await this.request<EvidenceRecord>(`/evidence-intake/${encodeURIComponent(record.id)}/content`,{method:"POST",headers:{"Content-Type":operation.file.type,"Idempotency-Key":operation.uploadKey,"If-Match":`"${record.version}"`},body:operation.file}));
        operation.record=record;onReserved(record);
        record=await this.evidenceStatus(record.id);
      }
      return record;
    } finally { this.activeMutations--; }
  }
  certificateCapabilities() { return this.request<{canWriteDraft:boolean;canManage:boolean}>("/certificate-register/capabilities"); }
  async certificateList(options: CertificateBrowseOptions = {}, signal?: AbortSignal) {
    const query = new URLSearchParams();
    for (const [key,value] of Object.entries(options)) if (value !== undefined && value !== "") query.set(key,String(value));
    const result = await this.request<CertificatePage>(`/certificate-register${query.size ? `?${query}` : ""}`, {signal});
    if (!Array.isArray(result.items) || typeof result.hasMore !== "boolean" || result.hasMore !== !!result.nextCursor || (result.nextCursor && (typeof result.nextCursor !== "string" || result.nextCursor.length > 512)) || !calendarDate(result.asOfDate) || result.timeBasis !== "UTC_CALENDAR_DATE") throw new ApiError(0,"INVALID_RESPONSE","Certificate page not confirmed.");
    const items=result.items.map(checkedCertificate);
    if(items.some(item=>item.dateTracking.asOfDate!==result.asOfDate)) throw new ApiError(0,"INVALID_RESPONSE","Certificate page date context is inconsistent.");
    return {...result,items};
  }
  async certificateGet(id: string) { return checkedCertificate(await this.request<CertificateRecord>(`/certificate-register/${encodeURIComponent(id)}`)); }
  async certificateWrite(action: "create"|"metadata"|"record"|"reopen", body: object, key: string, record?: CertificateRecord) {
    const suffix = action === "create" ? "" : `/${encodeURIComponent(record!.id)}/${action === "metadata" ? "metadata" : `commands/${action}`}`;
    return checkedCertificate(await this.request<CertificateRecord>(`/certificate-register${suffix}`, {method:"POST",headers:{"Idempotency-Key":key,...(record ? {"If-Match":`"${record.version}"`}:{})},body:JSON.stringify(body)}));
  }
  certificateHistory(id:string,cursor?:string) { return this.request<InventoryHistoryPage>(`/certificate-register/${encodeURIComponent(id)}/history${cursor?`?cursor=${encodeURIComponent(cursor)}`:""}`); }
  inventoryCapabilities() { return this.request<{canWriteDraft:boolean;canManage:boolean}>("/inventory/vessels/capabilities"); }
  async inventoryList(options: InventoryBrowseOptions = {}, signal?: AbortSignal) {
    const query = new URLSearchParams();
    for (const [key,value] of Object.entries(options)) if (value !== undefined && value !== "") query.set(key,String(value));
    const result = await this.request<InventoryPage>(`/inventory/vessels${query.size ? `?${query}` : ""}`, {signal});
    if (!Array.isArray(result.items) || typeof result.hasMore !== "boolean" || result.hasMore !== !!result.nextCursor || (result.nextCursor && (typeof result.nextCursor !== "string" || result.nextCursor.length > 512))) throw new ApiError(0,"INVALID_RESPONSE","Inventory page not confirmed.");
    return {...result,items:result.items.map(checkedInventory)};
  }
  async inventoryGet(id: string) { return checkedInventory(await this.request<InventoryVessel>(`/inventory/vessels/${encodeURIComponent(id)}`)); }
  async inventoryWrite(action: "create"|"metadata"|"record"|"reopen", body: object, key: string, record?: InventoryVessel) {
    const suffix = action === "create" ? "" : `/${encodeURIComponent(record!.id)}/${action === "metadata" ? "metadata" : `commands/${action}`}`;
    return checkedInventory(await this.request<InventoryVessel>(`/inventory/vessels${suffix}`, {method:"POST",headers:{"Idempotency-Key":key,...(record ? {"If-Match":`"${record.version}"`} : {})},body:JSON.stringify(body)}));
  }
  inventoryHistory(id:string,cursor?:string) { return this.request<InventoryHistoryPage>(`/inventory/vessels/${encodeURIComponent(id)}/history${cursor?`?cursor=${encodeURIComponent(cursor)}`:""}`); }
  async inventoryOptions(options: BrowseOptions={},signal?:AbortSignal) {
    const page=await this.inventoryList(options,signal);
    return {...page,items:page.items.map(v=>({draftId:v.id,data:Object.fromEntries(Object.entries(v.data).map(([k,value])=>[k,String(value)]))}))};
  }
  async list(kind: Kind, options: BrowseOptions = {}, signal?: AbortSignal): Promise<DraftPage> {
    const query = new URLSearchParams();
    for (const [key,value] of Object.entries(options)) if (value !== undefined && value !== "") query.set(key,String(value));
    const suffix = query.size ? `?${query}` : "";
    const result = await this.request<DraftPage>(`/${kind}${suffix}`, { signal });
    if (!Array.isArray(result.items))
      throw new ApiError(
        0,
        "INVALID_RESPONSE",
        "Server did not return a draft list.",
      );
    if (result.nextCursor !== undefined && result.nextCursor !== null && (typeof result.nextCursor !== "string" || !result.nextCursor || result.nextCursor.length > 512))
      throw new ApiError(0, "INVALID_RESPONSE", "Invalid page cursor.");
    if (result.hasMore !== undefined && (typeof result.hasMore !== "boolean" || result.hasMore !== !!result.nextCursor))
      throw new ApiError(0, "INVALID_RESPONSE", "Invalid page continuation.");
    return { ...result, items: result.items.map(checkedDraft) };
  }
  async get(kind: Kind, id: string) {
    return checkedDraft(
      await this.request<Draft>(`/${kind}/${encodeURIComponent(id)}`),
    );
  }
  async save(kind: Kind, data: DraftData, key: string, record?: Draft) {
    return checkedDraft(
      await this.request<Draft>(
        `/${kind}${record ? `/${encodeURIComponent(record.draftId)}` : ""}`,
        {
          method: "POST",
          headers: {
            "Idempotency-Key": key,
            ...(record ? { "If-Match": `"${record.version}"` } : {}),
          },
          body: JSON.stringify({ saveMode: "DRAFT", data }),
        },
      ),
    );
  }
}
export const newRequestKey = () => crypto.randomUUID();

// Cookie-only browser transport: no access/ID/refresh token is returned to JavaScript.
export class BffApi extends Api {
  constructor(transport: typeof fetch = fetch) { super("", transport, "bff"); }
  async logout() { await this.endSession(); }
}

export async function beginBffLogin(lang: "tr" | "en", beforeNavigate: () => void = () => {}) {
  const csrf = await new BffApi().antiforgery();
  if (import.meta.env.VITE_ENABLE_UAT === "true") {
    // Browser-derived origin, never a query parameter or forwarded header.
    // Custom header + same-origin mode + CSRF; redirects fail closed.
    const response = await fetch("/bff/login", {
      method: "POST", mode: "same-origin", credentials: "same-origin", redirect: "error",
      headers: { "X-CSRF-TOKEN": csrf, "X-Poseidon-Uat-Origin": window.location.origin },
    });
    if (response.status !== 204) throw new ApiError(response.status, "LOGIN_UNCONFIRMED", "Sign-in was not confirmed.");
    beforeNavigate();
    window.location.assign("/?lang=" + lang);
    return;
  }
  // Top-level form navigation lets the handler challenge the IdP; never fetch its redirect.
  // Fixed local return URL, independent of caller URL/query parameters.
  const form = document.createElement("form");
  form.method = "POST"; form.action = `/bff/login?returnUrl=${encodeURIComponent("/?lang=" + lang)}`;
  const field = document.createElement("input");
  field.type = "hidden"; field.name = "__RequestVerificationToken"; field.value = csrf;
  form.append(field); document.body.append(form);
  beforeNavigate();
  try { form.submit(); } finally { form.remove(); }
}

export type InventoryData = Record<string,string|number>;
export interface InventoryVessel {id:string;version:number;adminState:"DRAFT"|"RECORDED";data:InventoryData;completeness:{missingRequiredFields:string[];invalidFields:string[]};createdAt:string|null;modifiedAt:string|null}
export interface InventoryBrowseOptions extends BrowseOptions {adminState?:string;asset_mode?:string}
export interface InventoryPage {items:InventoryVessel[];nextCursor:string|null;hasMore:boolean}
export interface InventoryHistoryItem {eventId:string;occurredAt:string;version:number;action:string;changedFields:string[];actorRelation:"SELF"|"OTHER"}
export interface InventoryHistoryPage {items:InventoryHistoryItem[];nextCursor:string|null;hasMore:boolean}
function checkedInventory(value:InventoryVessel):InventoryVessel {
 if(!value || typeof value.id!=="string" || !Number.isSafeInteger(value.version) || value.version<1 || !["DRAFT","RECORDED"].includes(value.adminState) || !value.data || typeof value.data!=="object" || Array.isArray(value.data) || Object.values(value.data).some(v=>typeof v!=="string" && (typeof v!=="number" || !Number.isFinite(v))) || !value.completeness || !Array.isArray(value.completeness.missingRequiredFields) || !Array.isArray(value.completeness.invalidFields))throw new ApiError(0,"INVALID_RESPONSE","Inventory write/read not confirmed.");
 return value;
}

export const dateBuckets = ["NO_EXPIRY_DATE","PAST_DATE","TODAY","WITHIN_30_DAYS","DAYS_31_TO_60","DAYS_61_TO_90","AFTER_90_DAYS"] as const;
export type DateBucket = typeof dateBuckets[number];
export interface CertificateDateTracking {asOfDate:string;timeBasis:"UTC_CALENDAR_DATE";daysToRecordedExpiry:number|null;bucket:DateBucket;issueDateInFuture:boolean}
export interface CertificateRecord {id:string;version:number;adminState:"DRAFT"|"RECORDED";data:DraftData;completeness:{missingRequiredFields:string[];invalidFields:string[]};dateTracking:CertificateDateTracking;verificationAssessment:"NOT_PERFORMED";operationalAssessment:"NOT_PERFORMED";parentVessel:{id:string;name:string|null;imoNo:string|null;flag:string|null;adminState:"DRAFT"|"RECORDED"}}
export interface CertificateBrowseOptions {q?:string;vessel_id?:string;adminState?:string;dateBucket?:string;pageSize?:number;cursor?:string}
export interface CertificatePage {items:CertificateRecord[];nextCursor:string|null;hasMore:boolean;asOfDate:string;timeBasis:"UTC_CALENDAR_DATE"}
export function calendarDate(value:unknown): value is string {
 if(typeof value!=="string" || !/^\d{4}-\d{2}-\d{2}$/.test(value) || value.startsWith("0000-")) return false;
 const [year,month,day]=value.split("-").map(Number);
 const leap=year%4===0&&(year%100!==0||year%400===0);
 const days=[31,leap?29:28,31,30,31,30,31,31,30,31,30,31];
 return month>=1&&month<=12&&day>=1&&day<=days[month-1];
}
function checkedCertificate(value:CertificateRecord):CertificateRecord {
 if(!value || typeof value.id!=="string" || !Number.isSafeInteger(value.version) || value.version<1 || !["DRAFT","RECORDED"].includes(value.adminState) || !value.data || typeof value.data!=="object" || Array.isArray(value.data) || Object.values(value.data).some(v=>typeof v!=="string") || !value.completeness || !Array.isArray(value.completeness.missingRequiredFields) || !Array.isArray(value.completeness.invalidFields) || value.verificationAssessment!=="NOT_PERFORMED" || value.operationalAssessment!=="NOT_PERFORMED") throw new ApiError(0,"INVALID_RESPONSE","Certificate metadata not confirmed.");
 const dates=value.dateTracking,parent=value.parentVessel;
 if(!dates || !calendarDate(dates.asOfDate) || dates.timeBasis!=="UTC_CALENDAR_DATE" || !dateBuckets.includes(dates.bucket) || typeof dates.issueDateInFuture!=="boolean" || (dates.daysToRecordedExpiry!==null&&!Number.isInteger(dates.daysToRecordedExpiry)) || !parent || typeof parent.id!=="string" || parent.id!==value.data.vessel_id || !["DRAFT","RECORDED"].includes(parent.adminState))throw new ApiError(0,"INVALID_RESPONSE","Certificate date/scope context not confirmed.");
 return value;
}

export type EvidenceTarget="Vessel"|"Certificate";
export interface EvidenceRecord {id:string;version:number;targetType:EvidenceTarget;targetId:string;metadata:{filename:string;declaredMime:string;expectedBytes:number;title?:string;source?:string;description?:string;capturedAt?:string};state:string;scanState:string;integrityStatus:string;sha256:string|null;actualBytes:number|null;canDownload:false;blockReason:string;evidenceType:"ATTACHMENT";verificationAssessment:string;operationalAssessment:string;previousId?:string|null;supersededBy?:string|null}
export interface EvidenceOperation {targetType:EvidenceTarget;targetId:string;file:File;reserveKey:string;uploadKey:string;record?:EvidenceRecord;previous?:EvidenceRecord;reason?:string;metadata?:{title?:string;source?:string;description?:string}}
function checkedEvidence(value:EvidenceRecord):EvidenceRecord {
 if(!value || typeof value.id!=="string" || !Number.isSafeInteger(value.version) || value.version<1 || value.canDownload!==false || value.evidenceType!=="ATTACHMENT" || !["RESERVED","RECEIVING","STORED_QUARANTINED","REJECTED","EXPIRED"].includes(value.state) || value.scanState!=="UNAVAILABLE" || !value.metadata || typeof value.metadata.filename!=="string" || value.verificationAssessment!=="NOT_PERFORMED" || value.operationalAssessment!=="NOT_PERFORMED") throw new ApiError(0,"INVALID_RESPONSE","Evidence intake status not confirmed.");
 return value;
}

export interface EvidencePage {items:EvidenceRecord[];nextCursor:string|null;hasMore:boolean}
