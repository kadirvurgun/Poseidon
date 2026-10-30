import { beforeEach, describe, expect, it, vi } from "vitest";
import { fireEvent, render, screen, waitFor } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { App } from "./App";
import { BffApi, beginBffLogin } from "./api";
const session = { subject: "issuer-subject", tenantId: "tenant", companyId: "company", vesselIds: [], synthetic: false };
const reply = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status });
let signedIn: boolean, expired: boolean, logoutFailed: boolean;
let fetcher: ReturnType<typeof vi.fn<typeof fetch>>;
beforeEach(() => {
  vi.unstubAllEnvs(); vi.stubEnv("VITE_ENABLE_BFF", "true");
  history.replaceState(null, "", "/"); signedIn = true; expired = false; logoutFailed = false;
  Element.prototype.scrollIntoView = vi.fn();
  vi.spyOn(window, "confirm").mockReturnValue(true);
  fetcher = vi.fn(async (input, init) => {
    const url = String(input);
    if (url === "/bff/antiforgery") return reply({ requestToken: "csrf-only-not-an-auth-token" });
    if (url === "/bff/logout") { if (logoutFailed) throw new Error("offline"); signedIn = false; return new Response(null, { status: 204 }); }
    if (!signedIn || expired) return reply({}, 401);
    if (url === "/bff/session") return reply(session);
    if (init?.method === "POST") return reply({ draftId: "id", version: 1, persistenceState: "DRAFT", submissionAllowed: false, data: JSON.parse(String(init.body)).data });
    return reply({ items: [] });
  });
  vi.stubGlobal("fetch", fetcher);
});
async function editor() {
  const user = userEvent.setup(); render(<App />); await screen.findByText("Henüz taslak yok");
  await user.click(screen.getByRole("button", { name: "Yeni taslak" }));
  await user.type(screen.getByLabelText("Gemi adı"), "Unsaved BFF"); return user;
}
describe("BFF browser mode", () => {
  it("restores session without exposing bearer or synthetic controls", async () => {
    vi.stubEnv("VITE_ENABLE_DEV_SESSION", "true"); render(<App />); await screen.findByText("Henüz taslak yok");
    expect(screen.queryByLabelText("Erişim belirteci")).not.toBeInTheDocument();
    expect(screen.queryByText("Sentetik oturum aç")).not.toBeInTheDocument();
    expect(fetcher.mock.calls.every(([url]) => String(url).startsWith("/bff/"))).toBe(true);
  });
  it("does not automatically redirect anonymous users and localizes explicit login", async () => {
    signedIn = false; const submit = vi.spyOn(HTMLFormElement.prototype, "submit").mockImplementation(() => {});
    const user = userEvent.setup(); render(<App />);
    await screen.findByRole("button", { name: "Giriş yap" }); expect(submit).not.toHaveBeenCalled();
    await user.click(screen.getByRole("button", { name: "Switch to English" }));
    await user.click(screen.getByRole("button", { name: "Sign in" })); expect(submit).toHaveBeenCalledTimes(1);
  });
  it("retains dirty edits on navigation cancel and clears only after confirmed logout", async () => {
    const user = await editor(); vi.mocked(window.confirm).mockReturnValue(false);
    await user.click(screen.getByRole("button", { name: "Sertifika taslakları" }));
    await user.click(screen.getByRole("button", { name: "Çıkış" }));
    expect(screen.getByLabelText("Gemi adı")).toHaveValue("Unsaved BFF");
    expect(fetcher.mock.calls.some(([url]) => url === "/bff/logout")).toBe(false);
    vi.mocked(window.confirm).mockReturnValue(true); await user.click(screen.getByRole("button", { name: "Çıkış" }));
    await screen.findByRole("button", { name: "Giriş yap" });
    expect(screen.queryByDisplayValue("Unsaved BFF")).not.toBeInTheDocument();
    const logout = fetcher.mock.calls.find(([url]) => url === "/bff/logout")!;
    expect(logout[1]?.method).toBe("POST"); expect(logout[1]?.headers).toMatchObject({ "X-CSRF-TOKEN": "csrf-only-not-an-auth-token" });
  });
  it("binds deferred logout to displayed A and preserves replacement B on mismatch", async () => {
    const user = await editor();
    let release!: (response: Response) => void;
    const base = fetcher.getMockImplementation()!;
    fetcher.mockImplementation(async (input, init) => {
      if (input === "/bff/antiforgery") return new Promise<Response>(resolve => { release = resolve; });
      if (input === "/bff/logout") {
        expect(init?.headers).toMatchObject({ "X-Poseidon-Scope": "issuer-subject:tenant:company", "X-CSRF-TOKEN": "csrf-for-B" });
        return reply({ code: "logout_scope_changed" }, 409);
      }
      return base(input, init);
    });
    await user.click(screen.getByRole("button", { name: "Çıkış" }));
    await waitFor(() => expect(release).toBeTypeOf("function"));
    // Another tab has replaced the cookie with B while this request waited for CSRF.
    release(reply({ requestToken: "csrf-for-B" }));
    await screen.findByText("Oturum sona erdi.");
    expect(signedIn).toBe(true);
    expect(screen.getByLabelText("Gemi adı")).toHaveValue("Unsaved BFF");
    expect(screen.getByRole("button", { name: "Taslağı kaydet" })).toBeDisabled();
    expect(fetcher.mock.calls.filter(([url]) => url === "/bff/logout")).toHaveLength(1);
    expect(fetcher.mock.calls.filter(([,init]) => init?.method === "POST")).toHaveLength(1);
  });
  it("does not clear edits or claim logout success on a network failure", async () => {
    const user = await editor(); logoutFailed = true;
    await user.click(screen.getByRole("button", { name: "Çıkış" }));
    await screen.findByRole("alert"); expect(screen.getByLabelText("Gemi adı")).toHaveValue("Unsaved BFF");
    expect(screen.getByRole("button", { name: "Çıkış" })).toBeInTheDocument();
  });
  it("freezes expired edits, localizes expiry, rejects canceled relogin and never retries writes", async () => {
    const user = await editor(); expired = true;
    await user.click(screen.getByRole("button", { name: "Taslağı kaydet" }));
    await screen.findByText("Oturum sona erdi.");
    expect(screen.getByLabelText("Gemi adı")).toHaveValue("Unsaved BFF");
    expect(screen.getByRole("button", { name: "Aynı isteği tekrar dene" })).toBeDisabled();
    await user.click(screen.getByRole("button", { name: "Switch to English" }));
    expect(screen.getByText("Session ended.")).toBeInTheDocument();
    vi.mocked(window.confirm).mockReturnValue(false);
    const submit = vi.spyOn(HTMLFormElement.prototype, "submit").mockImplementation(() => {});
    await user.click(screen.getByRole("button", { name: "Sign in again" })); expect(submit).not.toHaveBeenCalled();
    expect(fetcher.mock.calls.filter(([url, init]) => String(url).includes("/vessels") && init?.method === "POST")).toHaveLength(1);
  });
  it("checks session when returning to the page without automatic login", async () => {
    await editor(); expired = true; fireEvent.focus(window);
    await screen.findByText("Oturum sona erdi.");
    expect(fetcher.mock.calls.some(([url]) => String(url).startsWith("/bff/login"))).toBe(false);
  });
  it("retains edits if login preparation fails", async () => {
    const user = await editor(); expired = true; fireEvent.focus(window); await screen.findByText("Oturum sona erdi.");
    fetcher.mockResolvedValue(reply({}, 503)); await user.click(screen.getByRole("button", { name: "Yeniden giriş yap" }));
    await waitFor(() => expect(screen.getByRole("button", { name: "Yeniden giriş yap" })).not.toBeDisabled());
    expect(screen.getByLabelText("Gemi adı")).toHaveValue("Unsaved BFF");
  });
});
describe("BFF transport", () => {
  it("uses same-origin cookie routes, fresh csrf and no auth token", async () => {
    const client = new BffApi(); await client.session(); await client.save("vessels", { name: "BFF" }, "key");
    const [url, init] = fetcher.mock.calls[2]; expect(url).toBe("/bff/api/v1/vessels");
    expect(init?.credentials).toBe("same-origin"); expect(init?.redirect).toBe("error");
    expect(init?.headers).not.toHaveProperty("Authorization");
    expect(init?.headers).toMatchObject({ "Idempotency-Key": "key", "X-CSRF-TOKEN": "csrf-only-not-an-auth-token" });
  });
  it("invalidates clients on 401 and after logout, without retrying", async () => {
    const client = new BffApi(); await client.session(); expired = true;
    await expect(client.list("vessels")).rejects.toMatchObject({ status: 401 });
    const count = fetcher.mock.calls.length; await expect(client.save("vessels", {}, "key")).rejects.toMatchObject({ status: 401 });
    expect(fetcher).toHaveBeenCalledTimes(count);
    expired = false; const next = new BffApi(); await next.session(); await next.logout();
    const after = fetcher.mock.calls.length; await expect(next.save("vessels", {}, "key")).rejects.toMatchObject({ status: 401 }); expect(fetcher).toHaveBeenCalledTimes(after);
  });
  it("rejects a different account or tenant without resubmitting edits", async () => {
    const client = new BffApi(); await client.session();
    fetcher.mockResolvedValue(reply({ ...session, companyId: "other-company" }));
    await expect(client.session()).rejects.toMatchObject({ status: 401, code: "SESSION_CHANGED" });
    const count = fetcher.mock.calls.length;
    await expect(client.save("vessels", {}, "key")).rejects.toMatchObject({ status: 401 }); expect(fetcher).toHaveBeenCalledTimes(count);
  });
  it("uses browser origin and CSRF for UAT login, with no redirect following", async () => {
    vi.stubEnv("VITE_ENABLE_UAT", "true");
    fetcher.mockImplementation(async (input, init) => String(input).includes("antiforgery")
      ? reply({ requestToken: "csrf" }) : reply({}, 400));
    const submit = vi.spyOn(HTMLFormElement.prototype, "submit");
    await expect(beginBffLogin("en")).rejects.toMatchObject({ code: "LOGIN_UNCONFIRMED" });
    expect(submit).not.toHaveBeenCalled();
    const call = fetcher.mock.calls.find(([url]) => url === "/bff/login")!;
    expect(call[1]).toMatchObject({ method: "POST", mode: "same-origin", credentials: "same-origin", redirect: "error",
      headers: { "X-CSRF-TOKEN": "csrf", "X-Poseidon-Uat-Origin": window.location.origin } });
  });
  it("uses a fixed local form return URL and sends only antiforgery data", async () => {
    history.replaceState(null, "", "/?returnUrl=https://evil.invalid");
    let action = "", fields: string[] = [];
    vi.spyOn(HTMLFormElement.prototype, "submit").mockImplementation(function (this: HTMLFormElement) { action = this.action; fields = [...new FormData(this).keys()]; });
    await beginBffLogin("en");
    expect(new URL(action).pathname).toBe("/bff/login"); expect(new URL(action).searchParams.get("returnUrl")).toBe("/?lang=en");
    expect(fields).toEqual(["__RequestVerificationToken"]);
  });
});
