import { useEffect, useRef, useState } from "react";
import {
  Anchor,
  ArrowLeft,
  Check,
  ChevronRight,
  FileText,
  Globe2,
  LogOut,
  Plus,
  RefreshCw,
  Search,
  ShieldAlert,
  Ship,
  Waves,
} from "lucide-react";
import {
  Api,
  BffApi,
  beginBffLogin,
  ApiError,
  newRequestKey,
  type BrowseOptions,
  type Draft,
  type DraftData,
  type Kind,
  type Session,
} from "./api";
import { Inventory } from "./Inventory";
import { CertificateRegister } from "./CertificateRegister";
import { fields, payload, validate } from "./forms";
import { Button } from "./components/ui/button";
type Lang = "tr" | "en";
export function App() {
  const [lang, setLang] = useState<Lang>(new URLSearchParams(window.location.search).get("lang") === "en" ? "en" : "tr");
  const t = (tr: string, en: string) => (lang === "tr" ? tr : en);
  const [inventoryView,setInventoryView]=useState(false);
  const [certificateView,setCertificateView]=useState(false);
  const [inventoryDirty,setInventoryDirty]=useState(false);
  const [inventoryBusy,setInventoryBusy]=useState(false);
  const [inventoryUncertain,setInventoryUncertain]=useState(false);
  const [api, setApi] = useState<Api | null>(null),
    [session, setSession] = useState<Session | null>(null);
  const [token, setToken] = useState(""),
    [fixture, setFixture] = useState("tenant-a");
  const [kind, setKind] = useState<Kind>("vessels"),
    [items, setItems] = useState<Draft[]>([]),
    [vessels, setVessels] = useState<Pick<Draft,"draftId"|"data">[]>([]);
  const [editor, setEditor] = useState(false),
    [record, setRecord] = useState<Draft | undefined>(),
    [data, setData] = useState<DraftData>({}),
    [baseline, setBaseline] = useState("{}");
  const [parentBusy, setBusy] = useState(false),
    [loading, setLoading] = useState(false),
    [error, setError] = useState<ApiError | null>(null),
    [serverFieldErrors, setServerFieldErrors] = useState<Record<string, string[]>>({}),
    [notice, setNotice] = useState(""),
    [query, setQuery] = useState("");
  const [browse, setBrowse] = useState<BrowseOptions>({});
  const [flagFilter, setFlagFilter] = useState("");
  const [typeFilter, setTypeFilter] = useState("");
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [pageCursor, setPageCursor] = useState<string | undefined>();
  const [vesselQuery, setVesselQuery] = useState("");
  const [vesselCursor, setVesselCursor] = useState<string | null>(null);
  const [vesselSearch, setVesselSearch] = useState("");
  const [vesselLoading, setVesselLoading] = useState(false);
  const vesselSequence = useRef(0);
  const listAbort = useRef<AbortController | null>(null);
  const vesselAbort = useRef<AbortController | null>(null);
  const [pending, setPending] = useState<{
    key: string;
    data: DraftData;
    uncertain?: boolean;
  } | null>(null);
  // Keep the submitted client values, so changing language re-renders the same
  // validation result without changing edits or replacing server diagnostics.
  const [validationData, setValidationData] = useState<DraftData | null>(null);
  const fieldErrors = validationData
    ? validate(kind, validationData, lang)
    : serverFieldErrors;
  const busy = parentBusy || inventoryBusy;
  const sequence = useRef(0);
  const lock = useRef(false);
  const leaving = useRef(false);
  const dirty = JSON.stringify(data) !== baseline;
  const bffEnabled = import.meta.env.VITE_ENABLE_BFF === "true";
  const [expired, setExpired] = useState(false);
  const [checking, setChecking] = useState(bffEnabled);
  const devEnabled = !bffEnabled && import.meta.env.VITE_ENABLE_DEV_SESSION === "true";
  useEffect(() => {
    document.documentElement.lang = lang;
  }, [lang]);
  useEffect(() => {
    const prevent = (event: BeforeUnloadEvent) => {
      if (!leaving.current && (dirty || pending || inventoryDirty)) {
        event.preventDefault();
        event.returnValue = "";
      }
    };
    window.addEventListener("beforeunload", prevent);
    return () => window.removeEventListener("beforeunload", prevent);
  }, [dirty, pending, inventoryDirty]);
  const normalize = (e: unknown) =>
    e instanceof ApiError
      ? e
      : new ApiError(
          0,
          "CLIENT_ERROR",
          t("İşlem tamamlanamadı.", "The operation could not be completed."),
        );
  const canLeave = () =>
    (!dirty && !pending && !inventoryDirty) ||
    window.confirm(inventoryUncertain || pending?.uncertain
      ? t("İşlemin sonucu bilinmiyor; kayıt sunucuda oluşmuş olabilir. Ayrılırsanız aynı anahtarla tekrar deneme bilgisi kaybolur. Devam edilsin mi?", "The outcome is unknown; the record may already exist on the server. Leaving discards the original retry key. Continue?")
      : t("Kaydedilmemiş değişiklikler bırakılacak. Devam edilsin mi?", "Unsaved changes will be discarded. Continue?"));
  function handleFailure(e: unknown) {
    const failure = normalize(e);
    setError(failure);
    if (bffEnabled && (failure.status === 401 || failure.code === "logout_scope_changed")) {
      sequence.current++;
      api?.invalidate();
      setExpired(true);
      setItems([]); setVessels([]); setNextCursor(null); setVesselCursor(null);
      vesselSequence.current++; listAbort.current?.abort(); vesselAbort.current?.abort(); setVesselLoading(false);
      setPending(operation => operation ? { ...operation, uncertain: true } : null);
      setLoading(false);
      setNotice("");
    }
  }
  useEffect(() => {
    if (!bffEnabled) return;
    let canceled = false;
    const client = new BffApi();
    void client.session().then(active => {
      if (!canceled) { setApi(client); setSession(active); }
    }).catch(e => {
      if (!canceled && !(e instanceof ApiError && e.status === 401)) setError(normalize(e));
    }).finally(() => { if (!canceled) setChecking(false); });
    return () => { canceled = true; client.invalidate(); };
  }, [bffEnabled]);
  useEffect(() => {
    if (!bffEnabled || !api || expired || busy) return;
    let active = true;
    const check = () => {
      if (document.visibilityState === "hidden") return;
      void api.session().catch(e => { if (active) handleFailure(e); });
    };
    window.addEventListener("focus", check);
    document.addEventListener("visibilitychange", check);
    return () => { active = false; window.removeEventListener("focus", check); document.removeEventListener("visibilitychange", check); };
  }, [api, bffEnabled, expired, busy]);
  async function browserLogin() {
    if (busy || checking || !canLeave()) return;
    setBusy(true); setError(null);
    try { await beginBffLogin(lang, () => {
      // The confirmed full-page navigation drops edits. Keep memory intact until it occurs.
      leaving.current = true;
    }); } catch (e) { leaving.current = false; handleFailure(e); setBusy(false); }
  }
  async function connect(dev = false) {
    if (lock.current) return;
    lock.current = true;
    setBusy(true);
    setError(null);
    try {
      let client = new Api(token.trim());
      let active: Session;
      if (dev) {
        const result = await client.devSession(fixture);
        client = new Api(result.accessToken);
        active = result.session;
      } else active = await client.session();
      setApi(client);
      setSession(active);
      setToken("");
      setEditor(false);
      setData({});
      setBaseline("{}");
      setPending(null);
    } catch (e) {
      setError(normalize(e));
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }
  function applyBrowse(options: BrowseOptions) {
    sequence.current++; listAbort.current?.abort(); setItems([]); setNextCursor(null); setPageCursor(undefined);
    setBrowse(options);
  }
  async function load(client: Api, section: Kind, cursor?: string) {
    const id = ++sequence.current;
    listAbort.current?.abort(); const controller = new AbortController(); listAbort.current = controller;
    vesselSequence.current++; vesselAbort.current?.abort(); setVesselLoading(false);
    setItems([]); setVessels([]); setNextCursor(null); setVesselCursor(null);
    setPageCursor(cursor); setLoading(true); setError(null);
    try {
      const result = await client.list(section, section === "vessels" ? { ...browse, cursor } : {}, controller.signal);
      const vesselResult = section === "vessels" ? result : await client.inventoryOptions({}, controller.signal);
      if (id !== sequence.current || controller.signal.aborted) return;
      setItems(result.items); setNextCursor(result.nextCursor ?? null);
      setVessels(vesselResult.items); setVesselCursor(vesselResult.nextCursor ?? null);
      setVesselSearch(""); setVesselQuery("");
    } catch (e) {
      if (id === sequence.current && !controller.signal.aborted) handleFailure(e);
    } finally { if (id === sequence.current) setLoading(false); }
  }
  async function loadVessels(append = false) {
    if (!api || expired || busy) return;
    const id = ++vesselSequence.current;
    vesselAbort.current?.abort(); const controller = new AbortController(); vesselAbort.current = controller;
    const q = append ? vesselSearch : vesselQuery.trim();
    const cursor = append ? vesselCursor ?? undefined : undefined;
    if (!append) { setVessels([]); setVesselCursor(null); }
    setVesselLoading(true); setError(null);
    try {
      const result = await api.inventoryOptions({ q, cursor }, controller.signal);
      if (id !== vesselSequence.current || controller.signal.aborted) return;
      setVessels(previous => append ? [...previous, ...result.items.filter(v => !previous.some(old => old.draftId === v.draftId))] : result.items);
      setVesselSearch(q); setVesselCursor(result.nextCursor ?? null);
    } catch (e) { if (id === vesselSequence.current && !controller.signal.aborted) { setVessels([]); setVesselCursor(null); handleFailure(e); } }
    finally { if (id === vesselSequence.current) setVesselLoading(false); }
  }
  useEffect(() => {
    if (api && !expired) void load(api, kind);
    return () => { sequence.current++; vesselSequence.current++; listAbort.current?.abort(); vesselAbort.current?.abort(); };
  }, [api, kind, browse, expired]);
  function navigate(next: Kind) {
    if (expired || busy || (!inventoryView && !certificateView && !editor && next === kind) || !canLeave()) return;
    setInventoryView(false);setCertificateView(false);
    if (next !== kind) sequence.current++;
    vesselSequence.current++; vesselAbort.current?.abort();
    if (next === kind && api) void load(api, kind);
    if (next !== kind) {
      setItems([]);
      setVessels([]);
    }
    setKind(next);
    setEditor(false);
    setData({});
    setBaseline("{}");
    setPending(null);
    setNotice("");
    setError(null);
    if (next !== kind) { setQuery(""); setFlagFilter(""); setTypeFilter(""); setBrowse({}); }
  }
  async function open(item: Draft) {
    if (!api || expired || busy || !canLeave()) return;
    const id = ++sequence.current;
    setBusy(true);
    setError(null);
    setNotice("");
    try {
      const latest = await api.get(kind, item.draftId);
      if (id !== sequence.current) return;
      setRecord(latest);
      setData(latest.data);
      setBaseline(JSON.stringify(latest.data));
      setEditor(true);
      setServerFieldErrors({});
      setValidationData(null);
      setPending(null);
    } catch (e) {
      if (id === sequence.current) handleFailure(e);
    } finally {
      setBusy(false);
    }
  }
  function create() {
    if (expired || busy || !canLeave()) return;
    const initial: DraftData =
      kind === "vessels" ? { company_id: session!.companyId } : {};
    setRecord(undefined);
    setData(initial);
    setBaseline(JSON.stringify(initial));
    setEditor(true);
    setServerFieldErrors({});
    setValidationData(null);
    setPending(null);
    setError(null);
    setNotice("");
  }
  async function save() {
    if (!api || expired || lock.current) return;
    // An unresolved operation is immutable. New client rules must not silently
    // replace its body/key or prevent an explicit same-operation reconciliation.
    const errors = pending ? {} : validate(kind, data, lang);
    setValidationData(pending ? null : data);
    setServerFieldErrors({});
    if (Object.keys(errors).length) {
      document.getElementById(Object.keys(errors)[0])?.focus();
      return;
    }
    const operation = pending || { key: newRequestKey(), data: payload(data), uncertain: false };
    setPending(operation);
    lock.current = true;
    setBusy(true);
    setError(null);
    setNotice("");
    try {
      const saved = await api.save(kind, operation.data, operation.key, record);
      setRecord(saved);
      setData(saved.data);
      setBaseline(JSON.stringify(saved.data));
      setPending(null);
      setItems((previous) => [
        saved,
        ...previous.filter((i) => i.draftId !== saved.draftId),
      ]);
      if (kind === "vessels")
        setVessels((previous) => [
          saved,
          ...previous.filter((i) => i.draftId !== saved.draftId),
        ]);
      setNotice(t("Taslak sunucuya kaydedildi.", "Draft saved to the server."));
    } catch (e) {
      const failure = normalize(e);
      handleFailure(failure);
      setValidationData(null);
      setServerFieldErrors(failure.errors);
      // A rejection now cannot establish whether an earlier lost response committed.
      if (operation.uncertain || failure.status === 0 || failure.status === 401 || failure.status >= 500)
        setPending({ ...operation, uncertain: true });
      else setPending(null);
    } finally {
      lock.current = false;
      setBusy(false);
    }
  }
  const conflict = error?.code !== "logout_scope_changed" && (error?.status === 412 || error?.status === 409);
  const authFailed = error?.status === 401 || error?.status === 403;
  async function disconnect() {
    if (busy || !canLeave()) return;
    if (bffEnabled) {
      setBusy(true);
      try {
        if (!(api instanceof BffApi)) throw new ApiError(401, "SESSION_ENDED", "Session ended.");
        await api.logout();
      }
      catch (e) {
        if (!(e instanceof ApiError && e.status === 401)) { handleFailure(e); setBusy(false); return; }
      }
      setBusy(false);
    }
    api?.invalidate(); listAbort.current?.abort(); vesselAbort.current?.abort(); vesselSequence.current++;
    setNextCursor(null); setVesselCursor(null); setBrowse({});
    setExpired(false);
    setRecord(undefined);
    setServerFieldErrors({}); setValidationData(null);
    sequence.current++;
    setInventoryView(false);setCertificateView(false);setInventoryDirty(false);
    setSession(null);
    setApi(null);
    setItems([]);
    setVessels([]);
    setEditor(false);
    setData({});
    setBaseline("{}");
    setPending(null);
    setError(null);
    setNotice("");
  }
  const errorView = error && (
    <div className="error-panel" role="alert">
      <ShieldAlert size={19} />
      <div>
        <strong>
          {error.code === "logout_scope_changed"
            ? t("Oturum değişti; çıkış yapılmadı.", "Session changed; no sign-out was performed.")
            : authFailed
            ? t(
                "Oturum veya erişim doğrulanamadı.",
                "Session or access could not be verified.",
              )
            : conflict
              ? t(
                  "Kayıt değişti. Değişikliklerinizi kontrol edin.",
                  "This record has changed. Review your changes.",
                )
              : t("İşlem tamamlanamadı.", "The operation did not complete.")}
        </strong>
        <p>{bffEnabled && authFailed ? t("Oturum sona ermiş veya erişiminiz kaldırılmış olabilir. Değişiklikler otomatik gönderilmez.", "Your session may have ended or access was removed. Changes are never sent automatically.") : bffEnabled ? t("İşlem doğrulanamadı. Bağlantıyı ve oturumunuzu kontrol edin; tekrar denemek sizin seçiminizdir.", "The operation could not be confirmed. Check your connection and session; retry only when you choose to.") : error.message}</p>
        <small>
          {error.code} {error.status ? `· HTTP ${error.status}` : ""}
        </small>
        {pending && (
          <p>
            {t(
              "Önceki istek kaydedilmiş olabilir; sonraki ret yanıtı bunu geri almaz. Aynı istek kimliği korunur. Yeni kayıt açmadan önce mevcut kayıtları ayrı sekmede kontrol edin.",
              "The earlier request may have committed; a later rejection does not undo it. The original request key is retained. Check existing records in another tab before creating a new record.",
            )}
          </p>
        )}
        {conflict && record && (
          <Button
            variant="outline"
            onClick={() => void open(record)}
            disabled={busy}
          >
            {t("Sunucudaki sürümü yeniden aç", "Reopen server version")}
          </Button>
        )}
      </div>
    </div>
  );
  return (
    <div className="app-shell">
      <a className="skip-link" href="#main">
        {t("İçeriğe geç", "Skip to content")}
      </a>
      <aside className="sidebar">
        <div className="brand">
          <span className="brand-mark">
            <Anchor size={25} />
          </span>
          <div>
            <strong>POSEIDON</strong>
            <small>{t("Denizcilik çalışma alanı", "Maritime workspace")}</small>
          </div>
        </div>
        <div className="nav-label">{t("ÇALIŞMA ALANI", "WORKSPACE")}</div>
        <nav aria-label={t("Ana menü", "Main navigation")}>
          <button className={certificateView?"active":""} disabled={!session||busy||expired} onClick={()=>{if(!canLeave())return;setEditor(false);setData({});setBaseline("{}");setPending(null);setInventoryView(false);setCertificateView(true);}}><FileText size={19}/>{t("Belge kayıtları","Certificate records")}</button>
          <button className={inventoryView?"active":""} disabled={!session||busy||expired} onClick={()=>{if(!canLeave())return;setEditor(false);setData({});setBaseline("{}");setPending(null);setCertificateView(false);setInventoryView(true);}}><Ship size={19}/>{t("İdari envanter","Administrative inventory")}</button>
          <button
            className={!inventoryView && !certificateView && kind === "vessels" ? "active" : ""}
            onClick={() => navigate("vessels")}
            disabled={!session || busy || expired}
          >
            <Ship size={19} />
            {t("Gemi taslakları", "Vessel drafts")}
            <ChevronRight size={15} />
          </button>
          <button
            className={!inventoryView && !certificateView && kind === "certificates" ? "active" : ""}
            onClick={() => navigate("certificates")}
            disabled={!session || busy || expired}
          >
            <FileText size={19} />
            {t("Sertifika taslakları", "Certificate drafts")}
            <ChevronRight size={15} />
          </button>
        </nav>
        <div className="sidebar-note">
          <ShieldAlert size={20} />
          <strong>
            {t("İdari kayıt alanı", "Administrative records")}
          </strong>
          <p>
            {t(
              "Kayıtlar başvuru, hukuki geçerlilik veya operasyonel onay oluşturmaz.",
              "Records do not constitute submission, legal validity or operational approval.",
            )}
          </p>
        </div>
        <div className="sidebar-footer">
          <Waves size={23} />
          <span>
            POSEIDON <small>PRODUCT SLICE · 01</small>
          </span>
        </div>
      </aside>
      <div className="workspace">
        <header className="topbar">
          <span className="context">
            {t("Kayıt yönetimi", "Record management")}{" "}
            <ChevronRight size={13} /> <b>{t("Taslak", "Draft")}</b>
          </span>
          <div className="top-actions">
            <Button
              variant="ghost"
              size="sm"
              onClick={() => setLang(lang === "tr" ? "en" : "tr")}
              aria-label={t("Switch to English", "Türkçeye geç")}
            >
              <Globe2 size={16} />
              {lang === "tr" ? "EN" : "TR"}
            </Button>
            {session && (
              <Button
                variant="outline"
                size="sm"
                onClick={disconnect}
                disabled={busy}
              >
                <LogOut size={15} />
                {t("Çıkış", "Sign out")}
              </Button>
            )}
          </div>
        </header>
        <main id="main" tabIndex={-1}>
          {import.meta.env.VITE_ENABLE_UAT === "true" && <p role="note">{t("Sentetik UAT — yalnız örnek veriler; gerçek hesap veya operasyon onayı değildir.", "Synthetic UAT — sample data only; not a real account or operational approval.")}</p>}
          {expired && session && <section className="error-panel" role="alert"><div>
            <strong>{t("Oturum sona erdi.", "Session ended.")}</strong>
            <p>{t("Kaydedilmemiş değişiklikler bu sayfada korunur; yeniden giriş veya çıkış bunları siler. Otomatik tekrar gönderim yapılmaz.", "Unsaved changes remain on this page; signing in again or signing out discards them. Nothing is retried automatically.")}</p>
            <Button onClick={() => void browserLogin()} disabled={busy}>{t("Yeniden giriş yap", "Sign in again")}</Button>
          </div></section>}
          {!session ? (
            <section className="auth-layout">
              <div className="auth-intro">
                <div className="eyebrow">POSEIDON / 01</div>
                <h1>
                  {t(
                    "Filonuzun kayıtları.\nKontrollü bir başlangıç.",
                    "Your fleet records.\nA controlled beginning.",
                  )}
                </h1>
                <p>
                  {t(
                    "Gemi ve sertifika bilgilerini yetkili kapsamınız içinde hazırlayın. İdari envanter kaydı operasyonel onay değildir.",
                    "Prepare vessel and certificate information within your authorized scope. Administrative inventory recording is not operational approval.",
                  )}
                </p>
                <div className="auth-feature">
                  <Ship size={20} />
                  {t("Gemi profilleri", "Vessel profiles")}
                  <span>01</span>
                </div>
                <div className="auth-feature">
                  <FileText size={20} />
                  {t("Sertifika metadata kayıtları", "Certificate metadata")}
                  <span>02</span>
                </div>
              </div>
              <div className="card auth-card">
                <span className="eyebrow">
                  {t("GÜVENLİ ERİŞİM", "SCOPED ACCESS")}
                </span>
                <h2>{t("Oturum doğrulama", "Verify a session")}</h2>
                {bffEnabled ? <>
                  <p>{import.meta.env.VITE_ENABLE_UAT === "true" ? t("Sentetik UAT oturumu — gerçek hesap veya müşteri verisi kullanmayın. Oturum 20 dakika sonra sona erer.", "Synthetic UAT session — do not use real accounts or customer data. Session expires after 20 minutes.") : t("Kurum hesabınızla güvenli oturum açın.", "Sign in securely with your organization account.")}</p>
                  <Button onClick={() => void browserLogin()} disabled={busy || checking}>
                    {checking ? t("Oturum denetleniyor…", "Checking session…") : busy ? t("Giriş başlatılıyor…", "Starting sign-in…") : t("Giriş yap", "Sign in")}
                  </Button>
                  {errorView}
                </> : <>
                <p>
                  {t(
                    "Üretim OIDC giriş akışı yapılandırılmalıdır. Bu entegrasyon görünümü, mevcut bir erişim belirtecini sunucuda doğrular.",
                    "Production OIDC sign-in requires configuration. This integration view verifies an existing access token with the server.",
                  )}
                </p>
                <form
                  onSubmit={(e) => {
                    e.preventDefault();
                    void connect();
                  }}
                >
                  <label htmlFor="token">
                    {t("Erişim belirteci", "Access token")}
                  </label>
                  <input
                    id="token"
                    type="password"
                    autoComplete="off"
                    value={token}
                    onChange={(e) => setToken(e.target.value)}
                    required
                    disabled={busy}
                  />
                  <Button type="submit" disabled={busy || !token.trim()}>
                    {busy
                      ? t("Doğrulanıyor…", "Verifying…")
                      : t("Oturumu doğrula", "Verify session")}
                  </Button>
                </form>
                {devEnabled && (
                  <div className="dev-login">
                    <strong>
                      {t(
                        "SENTETİK YEREL GELİŞTİRME",
                        "SYNTHETIC LOCAL DEVELOPMENT",
                      )}
                    </strong>
                    <p>
                      {t(
                        "Gerçek kimlik veya üretim erişimi değildir. Sunucuda ayrıca etkinleştirilmelidir.",
                        "Not a real identity or production access. Must also be explicitly enabled on the server.",
                      )}
                    </p>
                    <label htmlFor="fixture">
                      {t("Test kapsamı", "Test scope")}
                    </label>
                    <select
                      id="fixture"
                      value={fixture}
                      onChange={(e) => setFixture(e.target.value)}
                      disabled={busy}
                    >
                      <option value="tenant-a">Tenant A</option>
                      <option value="inventory-manager">Inventory manager (synthetic)</option>
                      <option value="certificate-manager">Certificate manager (synthetic)</option>
                      {import.meta.env.VITE_ENABLE_EVIDENCE_INTAKE === "true" && <option value="evidence-manager">{lang === "tr" ? "Kanıt alım yöneticisi (sentetik)" : "Evidence intake manager (synthetic)"}</option>}
                      <option value="tenant-b">Tenant B</option>
                    </select>
                    <Button
                      variant="outline"
                      onClick={() => void connect(true)}
                      disabled={busy}
                    >
                      {t("Sentetik oturum aç", "Open synthetic session")}
                    </Button>
                  </div>
                )}
                {errorView}
                <small className="muted">
                  {t(
                    "Belirteç yalnızca bellekte tutulur; sayfa yenilenince oturum kapanır.",
                    "Token is held in memory only; refreshing ends this session.",
                  )}
                </small>
                </>}
              </div>
            </section>
          ) : certificateView && api ? (
            <CertificateRegister api={api} lang={lang} expired={expired} parentBusy={parentBusy} onFailure={handleFailure} onDirty={setInventoryDirty} onBusy={setInventoryBusy} onUncertain={setInventoryUncertain}/>
          ) : inventoryView && api ? (
            <Inventory api={api} lang={lang} expired={expired} parentBusy={parentBusy} onFailure={handleFailure} onDirty={setInventoryDirty} onBusy={setInventoryBusy} onUncertain={setInventoryUncertain}/>
          ) : (
            <>
              <div
                className={`session-strip ${session.synthetic ? "synthetic" : ""}`}
              >
                <ShieldAlert size={17} />
                <span>
                  {session.synthetic
                    ? t(
                        "Sentetik yerel oturum · Üretim kimliği değildir",
                        "Synthetic local session · Not a production identity",
                      )
                    : t(
                        "Sunucuda doğrulanmış oturum",
                        "Server-verified session",
                      )}
                </span>
                <span className="session-subject">{session.subject}</span>
              </div>
              <div className="page-heading">
                <div>
                  <div className="eyebrow">
                    {t(
                      "KAYITLAR / TASLAK ÇALIŞMA",
                      "RECORDS / DRAFT WORKSPACE",
                    )}
                  </div>
                  <h1>
                    {kind === "vessels"
                      ? t("Gemi taslakları", "Vessel drafts")
                      : t("Sertifika taslakları", "Certificate drafts")}
                  </h1>
                  <p>
                    {t(
                      "Kaydedin, yeniden açın, bilgileri tamamlayın. Başvuru ve onay bu kapsamın dışındadır.",
                      "Save, reopen and complete information. Submission and approval are outside this scope.",
                    )}
                  </p>
                </div>
                {!editor && (
                  <Button
                    onClick={create}
                    disabled={busy || loading || authFailed || expired}
                  >
                    <Plus size={18} />
                    {t("Yeni taslak", "New draft")}
                  </Button>
                )}
              </div>
              <div className="scope-bar">
                <span>
                  {t("Şirket kapsamı", "Company scope")}
                  <code>{session.companyId}</code>
                </span>
                <span className="draft-badge">
                  {t("Yalnızca TASLAK", "DRAFT only")}
                </span>
              </div>
              {errorView}
              {notice && (
                <div role="status" className="success">
                  <Check size={17} />
                  {notice}
                </div>
              )}
              {editor ? (
                <section className="card editor">
                  <div className="editor-heading">
                    <Button
                      variant="ghost"
                      onClick={() => navigate(kind)}
                      disabled={busy || expired}
                    >
                      <ArrowLeft size={17} />
                      {t("Listeye dön", "Back to list")}
                    </Button>
                    <span className="draft-badge">
                      {t("TASLAK", "DRAFT")}
                      {record ? ` · v${record.version}` : ""}
                    </span>
                  </div>
                  <h2>
                    {record
                      ? t("Taslağı düzenle", "Edit draft")
                      : t("Yeni taslak", "New draft")}
                  </h2>
                  <p className="muted">
                    {t(
                      "Eksik bilgiler taslak olarak kaydedilebilir. Girilen değerler geçerli biçimde olmalıdır.",
                      "Missing metadata can be saved as a draft. Supplied values must have a valid format.",
                    )}
                  </p>
                  {record && <p className="record-id">ID: {record.draftId}</p>}
                  <form
                    noValidate
                    onSubmit={(e) => {
                      e.preventDefault();
                      void save();
                    }}
                  >
                    <div className="form-grid">
                      {fields[kind].map((field) => (
                        <div
                          className={`field ${field.key === "notes" ? "wide" : ""}`}
                          key={field.key}
                        >
                          <label htmlFor={field.key}>
                            {field[lang]}
                            {field.key === "vessel_id" && (
                              <span className="required"> *</span>
                            )}
                          </label>
                          {field.key === "vessel_id" ? (
                            <>
                            {!record && <div className="vessel-picker-tools">
                              <label htmlFor="vessel-search">{t("Seçilecek gemiyi ara", "Search vessel options")}</label>
                              <input id="vessel-search" value={vesselQuery} maxLength={100} disabled={busy || expired || !!pending} onChange={e => setVesselQuery(e.target.value)} />
                              <Button type="button" variant="outline" onClick={() => void loadVessels()} disabled={busy || expired || !!pending}>{t("Gemileri ara", "Search vessels")}</Button>
                              {vesselCursor && <Button type="button" variant="outline" onClick={() => void loadVessels(true)} disabled={vesselLoading || busy || expired || !!pending}>{t("Daha fazla gemi", "More vessels")}</Button>}
                              {vesselLoading && <span role="status">{t("Gemiler yükleniyor…", "Loading vessels…")}</span>}
                            </div>}
                            <select
                              id={field.key}
                              value={data[field.key] || ""}
                              disabled={busy || !!pending || !!record || expired}
                              onChange={(e) =>
                                setData({
                                  ...data,
                                  [field.key]: e.target.value,
                                })
                              }
                              aria-required="true"
                              aria-invalid={!!fieldErrors[field.key]}
                              aria-describedby={
                                fieldErrors[field.key]
                                  ? `${field.key}-error`
                                  : undefined
                              }
                            >
                              <option value="">
                                {t(
                                  "Gemi seçin — varsayılan yok",
                                  "Select a vessel — no default",
                                )}
                              </option>
                              {data.vessel_id && !vessels.some(v => v.draftId === data.vessel_id) && <option value={data.vessel_id}>{t("Seçili gemi", "Selected vessel")} · {data.vessel_id}</option>}
                              {vessels.map((v) => (
                                <option key={v.draftId} value={v.draftId}>
                                  {v.data.name ||
                                    t("Adsız gemi", "Unnamed vessel")}{" "}
                                  · {v.draftId.slice(0, 8)}
                                </option>
                              ))}
                            </select>
                            </>
                          ) : field.key === "notes" ? (
                            <textarea
                              id={field.key}
                              value={data[field.key] || ""}
                              disabled={busy || !!pending || expired}
                              rows={4}
                              onChange={(e) =>
                                setData({
                                  ...data,
                                  [field.key]: e.target.value,
                                })
                              }
                              aria-invalid={!!fieldErrors[field.key]}
                              aria-describedby={
                                fieldErrors[field.key]
                                  ? `${field.key}-error`
                                  : undefined
                              }
                            />
                          ) : (
                            <input
                              id={field.key}
                              type={field.type || "text"}
                              value={data[field.key] || ""}
                              disabled={busy || !!pending || expired}
                              onChange={(e) =>
                                setData({
                                  ...data,
                                  [field.key]: e.target.value,
                                })
                              }
                              aria-invalid={!!fieldErrors[field.key]}
                              aria-describedby={
                                fieldErrors[field.key]
                                  ? `${field.key}-error`
                                  : undefined
                              }
                              inputMode={
                                field.key === "imo_no" ? "numeric" : undefined
                              }
                            />
                          )}
                          {fieldErrors[field.key] && (
                            <span
                              className="field-error"
                              id={`${field.key}-error`}
                            >
                              {fieldErrors[field.key].join(" ")}
                            </span>
                          )}
                        </div>
                      ))}
                    </div>
                    <div className="form-footer">
                      <p>
                        {t(
                          "Taslak kaydı ≠ geçerlilik veya uygunluk",
                          "Saving a draft ≠ validity or compliance",
                        )}
                        <small>
                          {t(
                            "Başvuruya izin verilmez.",
                            "Submission is not allowed.",
                          )}
                        </small>
                      </p>
                      <Button
                        type="submit"
                        disabled={busy || conflict || authFailed || expired}
                      >
                        {busy
                          ? t("Kaydediliyor…", "Saving…")
                          : pending
                            ? t("Aynı isteği tekrar dene", "Retry same request")
                            : t("Taslağı kaydet", "Save draft")}
                      </Button>
                    </div>
                  </form>
                </section>
              ) : (
                <section className="card list-card">
                  <form className="list-toolbar" onSubmit={e => { e.preventDefault(); applyBrowse({ q: query.trim(), flag: flagFilter.trim(), vessel_type: typeFilter.trim() }); }}>
                    <label className="search">
                      <Search size={18} />
                      <span className="sr-only">
                        {t("Taslaklarda ara", "Search drafts")}
                      </span>
                      <input
                        placeholder={t(
                          "Ad veya numara ile ara…",
                          "Search by name or number…",
                        )}
                        value={query}
                        maxLength={100}
                        disabled={expired}
                        onChange={(e) => setQuery(e.target.value)}
                      />
                    </label>
                    {kind === "vessels" && <>
                      <label>{t("Bayrak filtresi", "Flag filter")}<input value={flagFilter} maxLength={200} disabled={expired} onChange={e => setFlagFilter(e.target.value)} /></label>
                      <label>{t("Gemi tipi filtresi", "Vessel type filter")}<input value={typeFilter} maxLength={200} disabled={expired} onChange={e => setTypeFilter(e.target.value)} /></label>
                      <Button type="submit" disabled={busy || expired}>{t("Ara / filtrele", "Search / filter")}</Button>
                      <Button type="button" variant="outline" disabled={busy || expired} onClick={() => { setQuery(""); setFlagFilter(""); setTypeFilter(""); applyBrowse({}); }}>{t("Filtreleri temizle", "Clear filters")}</Button>
                    </>}
                    <Button
                      type="button"
                      variant="ghost"
                      onClick={() => api && void load(api, kind)}
                      disabled={loading || busy || expired}
                    >
                      <RefreshCw size={16} />
                      {t("Yenile", "Refresh")}
                    </Button>
                  </form>
                  {loading ? (
                    <div role="status" className="empty">
                      {t("Sunucudan yükleniyor…", "Loading from server…")}
                    </div>
                  ) : error ? (
                    <div className="empty">
                      {t(
                        "Kayıtlar yüklenemedi. Listeyi yenileyin veya oturumunuzu doğrulayın.",
                        "Records could not be loaded. Refresh the list or verify your session.",
                      )}
                    </div>
                  ) : items.length === 0 ? (
                    <div className="empty">
                      <div className="empty-icon">
                        {kind === "vessels" ? (
                          <Ship size={30} />
                        ) : (
                          <FileText size={30} />
                        )}
                      </div>
                      <h2>{t("Henüz taslak yok", "No drafts yet")}</h2>
                      <p>
                        {t(
                          "Bu kapsamda yeni bir taslak oluşturarak başlayın.",
                          "Start by creating a draft in this scope.",
                        )}
                      </p>
                    </div>
                  ) : (
                    <div className="table-wrap">
                      <table>
                        <thead>
                          <tr>
                            <th>
                              {kind === "vessels"
                                ? t("Gemi / numara", "Vessel / number")
                                : t(
                                    "Sertifika / numara",
                                    "Certificate / number",
                                  )}
                            </th>
                            <th>{t("Durum", "State")}</th>
                            <th>{t("Sürüm", "Version")}</th>
                            <th>
                              <span className="sr-only">
                                {t("İşlem", "Action")}
                              </span>
                            </th>
                          </tr>
                        </thead>
                        <tbody>
                          {items
                            .filter((i) => kind === "vessels" ||
                              Object.values(i.data)
                                .join(" ")
                                .toLocaleLowerCase(lang)
                                .includes(query.toLocaleLowerCase(lang)),
                            )
                            .map((item) => (
                              <tr key={item.draftId}>
                                <td>
                                  <strong>
                                    {item.data.name ||
                                      item.data.type ||
                                      t("Adsız taslak", "Untitled draft")}
                                  </strong>
                                  <small>
                                    {item.data.imo_no ||
                                      item.data.certificate_no ||
                                      item.draftId}
                                  </small>
                                </td>
                                <td>
                                  <span className="draft-badge">
                                    {t("TASLAK", "DRAFT")}
                                  </span>
                                </td>
                                <td className="muted">v{item.version}</td>
                                <td>
                                  <Button
                                    variant="ghost"
                                    size="sm"
                                    onFocus={(event) =>
                                      event.currentTarget.scrollIntoView({
                                        block: "nearest",
                                        inline: "nearest",
                                      })
                                    }
                                    onClick={() => void open(item)}
                                    disabled={busy || expired}
                                  >
                                    {t("Aç", "Open")}
                                    <ChevronRight size={16} />
                                  </Button>
                                </td>
                              </tr>
                            ))}
                        </tbody>
                      </table>
                      {items.filter((i) => kind === "vessels" ||
                        Object.values(i.data)
                          .join(" ")
                          .toLocaleLowerCase(lang)
                          .includes(query.toLocaleLowerCase(lang)),
                      ).length === 0 && (
                        <div className="empty">
                          {t(
                            "Aramanızla eşleşen taslak yok.",
                            "No matching drafts.",
                          )}
                        </div>
                      )}
                    </div>
                  )}
                  <div className="list-footer">
                    {kind === "vessels" && <div className="page-actions">
                      {pageCursor && <Button type="button" variant="outline" onClick={() => api && void load(api, kind)} disabled={loading || expired}>{t("İlk sayfa", "First page")}</Button>}
                      {nextCursor && <Button type="button" variant="outline" onClick={() => api && void load(api, kind, nextCursor)} disabled={loading || expired}>{t("Sonraki sayfa", "Next page")}</Button>}
                    </div>}
                    {items.length}{" "}
                    {t(
                      "bu sayfada yüklenen kayıt · yalnızca yetkili kapsam",
                      "records loaded on this page · authorized scope only",
                    )}
                  </div>
                </section>
              )}
              <footer className="main-footer">
                <span>POSEIDON</span>
                {t(
                  "Taslak kayıt alanı · Başvuru, onay veya operasyon izni içermez.",
                  "Draft workspace · No submission, approval or operational authorization.",
                )}
              </footer>
            </>
          )}
        </main>
      </div>
    </div>
  );
}
