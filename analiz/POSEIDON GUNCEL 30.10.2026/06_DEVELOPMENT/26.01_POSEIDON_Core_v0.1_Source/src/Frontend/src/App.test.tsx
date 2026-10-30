import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { App } from "./App";
import * as forms from "./forms";
import type { Draft } from "./api";
const session = {
  subject: "test-user",
  tenantId: "tenant",
  companyId: "company",
  vesselIds: [],
  synthetic: true,
};
const draft: Draft = {
  draftId: "00000000-0000-0000-0000-000000000001",
  version: 1,
  persistenceState: "DRAFT",
  submissionAllowed: false,
  data: { name: "Atlas", company_id: "company" },
};
const reply = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status });
let records: Draft[];
let fetcher: ReturnType<
  typeof vi.fn<(input: string, init?: RequestInit) => Promise<Response>>
>;
beforeEach(() => {
  vi.unstubAllEnvs();
  // jsdom has no layout; Chromium checks actual focus visibility.
  Element.prototype.scrollIntoView = vi.fn();
  records = [];
  fetcher = vi.fn(async (input: string, init?: RequestInit) => {
    if (input.endsWith("/session")) return reply(session);
    if(input.includes("/inventory/vessels"))return reply({items:records.map(v=>({id:v.draftId,version:v.version,adminState:"DRAFT",data:v.data,completeness:{missingRequiredFields:[],invalidFields:[]},createdAt:null,modifiedAt:null})),hasMore:false,nextCursor:null});
    if (init?.method === "POST") {
      const body = JSON.parse(init.body as string);
      const saved = {
        ...draft,
        version: input.endsWith(draft.draftId) ? 2 : 1,
        data: body.data,
      };
      records = [saved];
      return reply(saved);
    }
    if (input.endsWith(draft.draftId)) return reply(records[0] || draft);
    return reply({ items: records });
  });
  vi.stubGlobal("fetch", fetcher);
  vi.spyOn(window, "confirm").mockReturnValue(true);
});
async function login() {
  const user = userEvent.setup();
  render(<App />);
  await user.type(screen.getByLabelText("Erişim belirteci"), "token");
  await user.click(screen.getByRole("button", { name: "Oturumu doğrula" }));
  await screen.findByText("Henüz taslak yok");
  return user;
}
describe("workspace interactions (jsdom, not browser certification)", () => {
  it("hides synthetic sign-in by default", () => {
    render(<App />);
    expect(
      screen.queryByRole("button", { name: "Sentetik oturum aç" }),
    ).not.toBeInTheDocument();
  });
  it("labels opt-in synthetic login and requests only the selected fixture", async () => {
    vi.stubEnv("VITE_ENABLE_DEV_SESSION", "true");
    const base = fetcher.getMockImplementation()!;
    fetcher.mockImplementation(async (input, init) =>
      input.endsWith("/dev/session")
        ? reply({ accessToken: "dev-token", session })
        : base(input, init),
    );
    const user = userEvent.setup();
    render(<App />);
    await user.selectOptions(screen.getByLabelText("Test kapsamı"), "tenant-b");
    await user.click(
      screen.getByRole("button", { name: "Sentetik oturum aç" }),
    );
    await screen.findByText("Sentetik yerel oturum · Üretim kimliği değildir");
    const request = fetcher.mock.calls.find(([url]) =>
      url.endsWith("/dev/session"),
    )!;
    expect(JSON.parse(request[1]!.body as string)).toEqual({
      fixture: "tenant-b",
    });
    expect(JSON.stringify(request[1])).not.toContain("companyId");
  });

  it("does not orphan an in-flight load when clicking the current section", async () => {
    let release!: (response: Response) => void;
    fetcher.mockImplementation(async (input) =>
      input.endsWith("/session")
        ? reply(session)
        : new Promise<Response>((resolve) => {
            release = resolve;
          }),
    );
    const user = userEvent.setup();
    render(<App />);
    await user.type(screen.getByLabelText("Erişim belirteci"), "token");
    await user.click(screen.getByRole("button", { name: "Oturumu doğrula" }));
    await screen.findByText("Sunucudan yükleniyor…");
    await user.click(screen.getByRole("button", { name: "Gemi taslakları" }));
    release(reply({ items: [draft] }));
    expect(await screen.findByText("Atlas")).toBeInTheDocument();
    expect(screen.queryByText("Sunucudan yükleniyor…")).not.toBeInTheDocument();
  });

  it("clears prior rows when a different resource fails to load", async () => {
    records = [draft];
    const user = userEvent.setup();
    render(<App />);
    await user.type(screen.getByLabelText("Erişim belirteci"), "token");
    await user.click(screen.getByRole("button", { name: "Oturumu doğrula" }));
    await screen.findByText("Atlas");
    const base = fetcher.getMockImplementation()!;
    fetcher.mockImplementation(async (input, init) =>
      input.endsWith("/certificates")
        ? reply({ code: "unavailable", message: "Unavailable" }, 503)
        : base(input, init),
    );
    await user.click(
      screen.getByRole("button", { name: "Sertifika taslakları" }),
    );
    await screen.findByRole("alert");
    expect(screen.queryByText("Atlas")).not.toBeInTheDocument();
  });
  it("clears previously loaded rows after revoked read access", async () => {
    records = [draft];
    const user = userEvent.setup();
    render(<App />);
    await user.type(screen.getByLabelText("Erişim belirteci"), "token");
    await user.click(screen.getByRole("button", { name: "Oturumu doğrula" }));
    await screen.findByText("Atlas");
    fetcher.mockImplementation(async () =>
      reply({ code: "forbidden", message: "Revoked" }, 403),
    );
    await user.click(screen.getByRole("button", { name: "Yenile" }));
    await screen.findByRole("alert");
    expect(screen.queryByText("Atlas")).not.toBeInTheDocument();
  });

  it("switches language and document language accessibly", async () => {
    const user = userEvent.setup();
    render(<App />);
    await user.click(screen.getByRole("button", { name: "Switch to English" }));
    expect(document.documentElement.lang).toBe("en");
    expect(
      screen.getByRole("heading", { name: "Verify a session" }),
    ).toBeInTheDocument();
    expect(screen.getByLabelText("Access token")).toHaveAttribute(
      "type",
      "password",
    );
  });
  it("shows auth errors without opening a fake workspace", async () => {
    fetcher.mockImplementation(async () =>
      reply({ code: "unauthorized", message: "Denied" }, 401),
    );
    const user = userEvent.setup();
    render(<App />);
    await user.type(screen.getByLabelText("Erişim belirteci"), "bad");
    await user.click(screen.getByRole("button", { name: "Oturumu doğrula" }));
    expect(await screen.findByRole("alert")).toHaveTextContent("Denied");
    expect(screen.queryByText("Henüz taslak yok")).not.toBeInTheDocument();
  });
  it("saves, returns to list and reopens actual server data", async () => {
    const user = await login();
    await user.click(screen.getByRole("button", { name: "Yeni taslak" }));
    await user.type(screen.getByLabelText("Gemi adı"), "Atlas");
    await user.click(screen.getByRole("button", { name: "Taslağı kaydet" }));
    expect(await screen.findByRole("status")).toHaveTextContent(
      "sunucuya kaydedildi",
    );
    await user.click(screen.getByRole("button", { name: "Listeye dön" }));
    expect(screen.getByText("Atlas")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Aç" }));
    expect(await screen.findByLabelText("Gemi adı")).toHaveValue("Atlas");
    expect(fetcher).toHaveBeenCalledWith(
      `/api/v1/vessels/${draft.draftId}`,
      expect.anything(),
    );
  });
  it("keeps malformed supplied values in the form and associates errors", async () => {
    const user = await login();
    await user.click(screen.getByRole("button", { name: "Yeni taslak" }));
    await user.type(screen.getByLabelText("IMO numarası"), "12bad");
    await user.click(screen.getByRole("button", { name: "Taslağı kaydet" }));
    expect(screen.getByLabelText("IMO numarası")).toHaveAttribute(
      "aria-invalid",
      "true",
    );
    expect(screen.getByText("IMO tam 7 rakam olmalıdır.")).toBeInTheDocument();
    expect(
      fetcher.mock.calls.filter(([, r]) => r?.method === "POST"),
    ).toHaveLength(0);
  });
  it("relocalizes client errors both ways and preserves unsaved data", async () => {
    const user = await login();
    await user.click(screen.getByRole("button", { name: "Yeni taslak" }));
    await user.type(screen.getByLabelText("Gemi adı"), "Unsaved");
    await user.type(screen.getByLabelText("IMO numarası"), "123");
    await user.click(screen.getByRole("button", { name: "Taslağı kaydet" }));
    await user.click(screen.getByRole("button", { name: "Switch to English" }));
    expect(screen.getByText("IMO must contain exactly 7 digits.")).toBeInTheDocument();
    expect(screen.queryByText("IMO tam 7 rakam olmalıdır.")).not.toBeInTheDocument();
    expect(screen.getByLabelText("Vessel name")).toHaveValue("Unsaved");
    expect(screen.getByLabelText("IMO number")).toHaveValue("123");
    expect(screen.getByLabelText("IMO number")).toHaveAttribute("aria-describedby", "imo_no-error");
    await user.click(screen.getByRole("button", { name: "Türkçeye geç" }));
    expect(screen.getByText("IMO tam 7 rakam olmalıdır.")).toBeInTheDocument();
    await user.click(screen.getByRole("button", { name: "Sertifika taslakları" }));
    await waitFor(() => expect(screen.queryByText("Sunucudan yükleniyor…")).not.toBeInTheDocument());
    await user.click(screen.getByRole("button", { name: "Yeni taslak" }));
    expect(screen.queryByText("Kapsam için bir gemi seçin.")).not.toBeInTheDocument();
    await user.type(screen.getByLabelText("Düzenlenme tarihi"), "2026-10-27");
    await user.type(screen.getByLabelText("Son geçerlilik tarihi"), "2026-10-01");
    await user.click(screen.getByRole("button", { name: "Taslağı kaydet" }));
    await user.click(screen.getByRole("button", { name: "Switch to English" }));
    expect(screen.getByText("Choose a vessel to establish scope.")).toBeInTheDocument();
    expect(screen.getByText("Expiry cannot precede issue date.")).toBeInTheDocument();
    expect(screen.getByLabelText("Issue date")).toHaveValue("2026-10-27");
    expect(fetcher.mock.calls.filter(([, r]) => r?.method === "POST")).toHaveLength(0);
  });
  it("preserves server field diagnostics when language changes", async () => {
    const user = await login();
    await user.click(screen.getByRole("button", { name: "Yeni taslak" }));
    await user.type(screen.getByLabelText("Gemi adı"), "Keep me");
    fetcher.mockResolvedValue(reply({ code: "validation", message: "Server rejected", errors: { name: ["Server field detail"] } }, 400));
    await user.click(screen.getByRole("button", { name: "Taslağı kaydet" }));
    await screen.findByText("Server field detail");
    await user.click(screen.getByRole("button", { name: "Switch to English" }));
    expect(screen.getByText("Server field detail")).toBeInTheDocument();
    expect(screen.getByLabelText("Vessel name")).toHaveValue("Keep me");
  });
  it("does not discard dirty edits when canceling navigation", async () => {
    const user = await login();
    await user.click(screen.getByRole("button", { name: "Yeni taslak" }));
    await user.type(screen.getByLabelText("Gemi adı"), "Unsaved");
    vi.mocked(window.confirm).mockReturnValue(false);
    await user.click(
      screen.getByRole("button", { name: "Sertifika taslakları" }),
    );
    expect(screen.getByLabelText("Gemi adı")).toHaveValue("Unsaved");
  });
  it("retries uncertain saves with identical key and body, locking edits", async () => {
    const user = await login();
    await user.click(screen.getByRole("button", { name: "Yeni taslak" }));
    await user.type(screen.getByLabelText("Gemi adı"), "Atlas");
    const base = fetcher.getMockImplementation()!;
    let failed = false;
    fetcher.mockImplementation(async (input, init) => {
      if (init?.method === "POST" && !failed) {
        failed = true;
        throw new TypeError("offline");
      }
      return base(input, init);
    });
    await user.click(screen.getByRole("button", { name: "Taslağı kaydet" }));
    await screen.findByRole("alert");
    expect(screen.getByLabelText("Gemi adı")).toBeDisabled();
    await user.click(
      screen.getByRole("button", { name: "Aynı isteği tekrar dene" }),
    );
    await screen.findByRole("status");
    const saves = fetcher.mock.calls.filter(([, r]) => r?.method === "POST");
    expect(saves).toHaveLength(2);
    expect(saves[0][1]).toEqual(saves[1][1]);
  });
  it("retains an old-bundle uncertain create through 422 and never silently creates a second object", async () => {
    const user = await login();
    await user.click(screen.getByRole("button", { name: "Yeni taslak" }));
    await user.type(screen.getByLabelText("Gemi adı"), "Old bundle unknown create");
    await user.type(screen.getByLabelText("IMO numarası"), "1234568");
    const base = fetcher.getMockImplementation()!;
    const committed = new Map<string, Draft>();
    let calls = 0, rejectReplay = true;
    fetcher.mockImplementation(async (input, init) => {
      if (init?.method !== "POST") return base(input, init);
      calls++;
      const key = (init.headers as Record<string,string>)["Idempotency-Key"];
      if (!committed.has(key)) {
        const saved = {...draft, data: JSON.parse(String(init.body)).data};
        committed.set(key,saved);records=[saved];
        throw new TypeError("Response lost after old server committed");
      }
      if (rejectReplay) return reply({code:"invalid_imo_checksum",message:"New server rejects old payload"},422);
      return reply(committed.get(key));
    });
    // Simulate the already-open pre-checksum bundle for its initial operation only.
    const oldValidation = vi.spyOn(forms,"validate").mockReturnValue({});
    await user.click(screen.getByRole("button", { name: "Taslağı kaydet" }));
    await screen.findByRole("alert");oldValidation.mockRestore();
    await user.click(screen.getByRole("button", { name: "Aynı isteği tekrar dene" }));
    await screen.findByText(/invalid_imo_checksum/);
    expect(screen.getByLabelText("IMO numarası")).toBeDisabled();
    expect(screen.getByLabelText("IMO numarası")).toHaveValue("1234568");
    expect(committed.size).toBe(1);
    vi.mocked(window.confirm).mockReturnValue(false);
    await user.click(screen.getByRole("button", { name: "Sertifika taslakları" }));
    expect(window.confirm).toHaveBeenLastCalledWith(expect.stringContaining("aynı anahtarla tekrar deneme bilgisi kaybolur"));
    expect(screen.getByLabelText("Gemi adı")).toHaveValue("Old bundle unknown create");
    expect(calls).toBe(2);
    rejectReplay=false;
    await user.click(screen.getByRole("button", { name: "Aynı isteği tekrar dene" }));
    await screen.findByRole("status");
    const posts=fetcher.mock.calls.filter(([,init])=>init?.method==="POST");
    expect(posts).toHaveLength(3);
    expect(posts[0][1]).toEqual(posts[1][1]);expect(posts[1][1]).toEqual(posts[2][1]);
    expect(committed.size).toBe(1);
  });
  it("requires explicit uncertainty abandonment after a lost response and later rejection", async () => {
    const user=await login();await user.click(screen.getByRole("button",{name:"Yeni taslak"}));
    await user.type(screen.getByLabelText("Gemi adı"),"Retain me");
    const base=fetcher.getMockImplementation()!;let writes=0;
    fetcher.mockImplementation(async(input,init)=>{
      if(init?.method!=="POST")return base(input,init);
      if(++writes===1)throw new TypeError("lost");
      return reply({code:"validation_changed",message:"Rejected retry"},422);
    });
    await user.click(screen.getByRole("button",{name:"Taslağı kaydet"}));await screen.findByRole("alert");
    await user.click(screen.getByRole("button",{name:"Aynı isteği tekrar dene"}));await screen.findByText(/validation_changed/);
    await user.click(screen.getByRole("button",{name:"Listeye dön"}));
    expect(window.confirm).toHaveBeenLastCalledWith(expect.stringContaining("kayıt sunucuda oluşmuş olabilir"));
    expect(writes).toBe(2);await screen.findByRole("button",{name:"Yeni taslak"});
  });
  it("blocks a stale overwrite and explicitly reloads current server version", async () => {
    records = [draft];
    const user = userEvent.setup();
    render(<App />);
    await user.type(screen.getByLabelText("Erişim belirteci"), "token");
    await user.click(screen.getByRole("button", { name: "Oturumu doğrula" }));
    await user.click(await screen.findByRole("button", { name: "Aç" }));
    await user.type(await screen.findByLabelText("Gemi adı"), " local");
    const base = fetcher.getMockImplementation()!;
    fetcher.mockImplementation(async (input, init) =>
      init?.method === "POST"
        ? reply({ code: "version_conflict", message: "Newer version" }, 412)
        : base(input, init),
    );
    await user.click(screen.getByRole("button", { name: "Taslağı kaydet" }));
    await screen.findByRole("alert");
    expect(
      screen.getByRole("button", { name: "Taslağı kaydet" }),
    ).toBeDisabled();
    records = [{ ...draft, version: 2, data: { name: "Server edit" } }];
    await user.click(
      screen.getByRole("button", { name: "Sunucudaki sürümü yeniden aç" }),
    );
    await waitFor(() =>
      expect(screen.getByLabelText("Gemi adı")).toHaveValue("Server edit"),
    );
    expect(screen.getByText("TASLAK · v2")).toBeInTheDocument();
  });
  it("certificate requires explicit vessel choice and saves metadata only", async () => {
    const user = await login();
    records = [draft];
    await user.click(
      screen.getByRole("button", { name: "Sertifika taslakları" }),
    );
    await waitFor(() =>
      expect(
        screen.queryByText("Sunucudan yükleniyor…"),
      ).not.toBeInTheDocument(),
    );
    await user.click(screen.getByRole("button", { name: "Yeni taslak" }));
    expect(screen.getByLabelText("Gemi *")).toHaveValue("");
    await user.click(screen.getByRole("button", { name: "Taslağı kaydet" }));
    expect(screen.getByText("Kapsam için bir gemi seçin.")).toBeInTheDocument();
    await user.selectOptions(screen.getByLabelText("Gemi *"), draft.draftId);
    await user.type(screen.getByLabelText("Sertifika tipi"), "Safety");
    await user.type(screen.getByLabelText("Düzenlenme tarihi"), "2026-10-01");
    await user.type(
      screen.getByLabelText("Son geçerlilik tarihi"),
      "2027-10-01",
    );
    await user.click(screen.getByRole("button", { name: "Taslağı kaydet" }));
    await screen.findByRole("status");
    const request = fetcher.mock.calls.find(
      ([url, r]) => url === "/api/v1/certificates" && r?.method === "POST",
    )!;
    expect(JSON.parse(request[1]!.body as string)).toEqual({
      saveMode: "DRAFT",
      data: {
        vessel_id: draft.draftId,
        type: "Safety",
        issue: "2026-10-01",
        expiry: "2027-10-01",
      },
    });
  });
});
