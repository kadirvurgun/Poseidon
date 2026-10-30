import { describe, it, expect } from "vitest";
import { fields, payload, validate } from "./forms";
describe("draft form rules", () => {
  it("uses canonical controlled certificate wire names", () => {
    const keys = fields.certificates.map((field) => field.key);
    expect(keys).toEqual([
      "vessel_id",
      "type",
      "certificate_no",
      "issuer",
      "issue",
      "expiry",
      "notes",
    ]);
  });
  it("allows missing vessel metadata without inventing defaults", () =>
    expect(validate("vessels", {}, "en")).toEqual({}));
  it("omits whitespace, retaining intentional content", () =>
    expect(payload({ name: "  Test ", notes: " ", imo_no: "" })).toEqual({
      name: "Test",
    }));
  it("rejects malformed supplied IMO and accepts 7 digits", () => {
    expect(validate("vessels", { imo_no: "123" }, "en").imo_no).toBeDefined();
    expect(validate("vessels", { imo_no: "1234567" }, "en")).toEqual({});
  });
  it("requires certificate scope but not completion metadata", () => {
    expect(validate("certificates", {}, "en").vessel_id).toBeDefined();
    expect(
      validate(
        "certificates",
        { vessel_id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa" },
        "en",
      ),
    ).toEqual({});
  });
  it("rejects impossible dates and reversed range", () => {
    expect(
      validate(
        "certificates",
        {
          vessel_id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
          issue: "2025-02-30",
        },
        "en",
      ).issue,
    ).toBeDefined();
    expect(
      validate(
        "certificates",
        {
          vessel_id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
          issue: "2026-10-20",
          expiry: "2026-10-19",
        },
        "en",
      ).expiry,
    ).toBeDefined();
  });
  it("accepts leap day and localizes field errors", () => {
    expect(
      validate(
        "certificates",
        {
          vessel_id: "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
          issue: "2024-02-29",
        },
        "tr",
      ),
    ).toEqual({});
    expect(validate("vessels", { imo_no: "a" }, "tr").imo_no[0]).toContain(
      "rakam",
    );
  });
  it("validates the server field length boundary", () => {
    expect(validate("vessels", { name: "a".repeat(200) }, "en")).toEqual({});
    expect(
      validate("vessels", { name: "a".repeat(201) }, "en").name,
    ).toBeDefined();
  });
});
