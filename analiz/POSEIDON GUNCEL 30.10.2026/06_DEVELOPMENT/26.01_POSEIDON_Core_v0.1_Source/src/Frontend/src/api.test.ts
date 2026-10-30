import { describe, it, expect, vi } from "vitest";
import { Api, ApiError, type Draft } from "./api";
const draft: Draft = {
  draftId: "id",
  version: 2,
  persistenceState: "DRAFT",
  submissionAllowed: false,
  data: { name: "Test" },
};
const reply = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status });
describe("API transport", () => {
  it("calls browser transport without an Api receiver", async () => {
    const transport = vi.fn(function (this: unknown) {
      if (this !== undefined) throw new TypeError("Illegal invocation");
      return Promise.resolve(reply({ items: [] }));
    });
    await expect(new Api("", transport).list("vessels")).resolves.toEqual({
      items: [],
    });
    expect(transport).toHaveBeenCalledTimes(1);
  });
  it("refuses to report success when server fails to confirm DRAFT", async () => {
    const api = new Api(
      "x",
      vi.fn().mockResolvedValue(
        reply({
          ...draft,
          persistenceState: "APPROVED",
          submissionAllowed: true,
        }),
      ),
    );
    await expect(api.save("vessels", {}, "key")).rejects.toMatchObject({
      code: "INVALID_RESPONSE",
      status: 0,
    });
  });
  it("rejects malformed successful list payloads", async () => {
    const api = new Api("x", vi.fn().mockResolvedValue(reply({})));
    await expect(api.list("vessels")).rejects.toMatchObject({
      code: "INVALID_RESPONSE",
    });
  });

  it("sends exact draft envelope, bearer, idempotency and quoted version", async () => {
    const fetcher = vi.fn().mockResolvedValue(reply(draft));
    const api = new Api("secret", fetcher);
    expect(
      await api.save("vessels", { name: "Test" }, "request-key", draft),
    ).toEqual(draft);
    const [path, request] = fetcher.mock.calls[0];
    expect(path).toBe("/api/v1/vessels/id");
    expect(request.method).toBe("POST");
    expect(request.headers).toMatchObject({
      Authorization: "Bearer secret",
      "Idempotency-Key": "request-key",
      "If-Match": '"2"',
    });
    expect(JSON.parse(request.body)).toEqual({
      saveMode: "DRAFT",
      data: { name: "Test" },
    });
  });
  it.each([401, 403, 409, 412, 422, 503])(
    "surfaces HTTP %s without fake success",
    async (status) => {
      const api = new Api(
        "x",
        vi
          .fn()
          .mockResolvedValue(
            reply({ code: "test", message: "Rejected" }, status),
          ),
      );
      await expect(api.list("vessels")).rejects.toMatchObject({
        status,
        code: "test",
        message: "Rejected",
      });
    },
  );
  it("keeps network uncertainty distinct from validation errors", async () => {
    const api = new Api(
      "x",
      vi.fn().mockRejectedValue(new TypeError("offline")),
    );
    await expect(api.list("vessels")).rejects.toBeInstanceOf(ApiError);
    await expect(api.list("vessels")).rejects.toMatchObject({
      status: 0,
      code: "NETWORK_ERROR",
    });
  });
  it("does not automatically retry a mutation", async () => {
    const fetcher = vi.fn().mockRejectedValue(new TypeError("offline"));
    await new Api("x", fetcher).save("vessels", {}, "key").catch(() => {});
    expect(fetcher).toHaveBeenCalledTimes(1);
  });
});

 it("rejects a JSON body resolved after the authenticated client was invalidated",async()=>{
  let release!:(v:unknown)=>void;
  const response={ok:true,status:200,json:()=>new Promise(resolve=>{release=resolve;})} as Response;
  const transport=vi.fn().mockResolvedValue(response);const api=new Api("test",transport);
  const pending=api.list("vessels");await Promise.resolve();await Promise.resolve();api.invalidate();release({items:[]});
  await expect(pending).rejects.toMatchObject({status:401,code:"SESSION_ENDED"});
 });
