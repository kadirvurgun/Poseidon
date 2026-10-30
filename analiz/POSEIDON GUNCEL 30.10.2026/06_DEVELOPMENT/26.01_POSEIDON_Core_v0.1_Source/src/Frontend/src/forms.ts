import {imoMessage} from "./imo";
import type { DraftData, Kind } from "./api";
export type Field = {
  key: string;
  tr: string;
  en: string;
  type?: string;
  max?: number;
};
export const fields: Record<Kind, Field[]> = {
  vessels: [
    { key: "name", tr: "Gemi adı", en: "Vessel name", max: 200 },
    { key: "imo_no", tr: "IMO numarası", en: "IMO number", max: 7 },
    { key: "official_no", tr: "Resmî numara", en: "Official number", max: 200 },
    { key: "flag", tr: "Bayrak", en: "Flag", max: 200 },
    { key: "vessel_type", tr: "Gemi tipi", en: "Vessel type", max: 200 },
    { key: "notes", tr: "Notlar", en: "Notes", max: 4000 },
  ],
  certificates: [
    { key: "vessel_id", tr: "Gemi", en: "Vessel" },
    {
      key: "type",
      tr: "Sertifika tipi",
      en: "Certificate type",
      max: 200,
    },
    {
      key: "certificate_no",
      tr: "Sertifika numarası",
      en: "Certificate number",
      max: 200,
    },
    { key: "issuer", tr: "Düzenleyen kurum", en: "Issuer", max: 200 },
    {
      key: "issue",
      tr: "Düzenlenme tarihi",
      en: "Issue date",
      type: "date",
    },
    {
      key: "expiry",
      tr: "Son geçerlilik tarihi",
      en: "Expiry date",
      type: "date",
    },
    { key: "notes", tr: "Notlar", en: "Notes", max: 4000 },
  ],
};
export function validate(kind: Kind, data: DraftData, lang: "tr" | "en") {
  const errors: Record<string, string[]> = {};
  const err = (key: string, tr: string, en: string) => {
    errors[key] = [lang === "tr" ? tr : en];
  };
  for (const field of fields[kind])
    if (field.max && (data[field.key]?.length || 0) > field.max)
      err(
        field.key,
        `En fazla ${field.max} karakter.`,
        `Use at most ${field.max} characters.`,
      );
  if (kind === "vessels") {
    const message = imoMessage(data.imo_no, lang);
    if (message) errors.imo_no = [message];
  }
  if (kind === "certificates") {
    if (!data.vessel_id)
      err(
        "vessel_id",
        "Kapsam için bir gemi seçin.",
        "Choose a vessel to establish scope.",
      );
    else if (
      !/^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(
        data.vessel_id,
      ) ||
      /^0{8}-0{4}-0{4}-0{4}-0{12}$/.test(data.vessel_id)
    )
      err(
        "vessel_id",
        "Geçerli bir gemi kimliği seçin.",
        "Choose a valid vessel identifier.",
      );
    for (const key of ["issue", "expiry"])
      if (
        data[key] &&
        (!/^\d{4}-\d{2}-\d{2}$/.test(data[key]) ||
          data[key].startsWith("0000-") ||
          Number.isNaN(Date.parse(data[key])) ||
          new Date(data[key]).toISOString().slice(0, 10) !== data[key])
      )
        err(key, "Geçerli bir tarih girin.", "Enter a valid date.");
    if (
      !errors.issue &&
      !errors.expiry &&
      data.issue &&
      data.expiry &&
      data.expiry < data.issue
    )
      err(
        "expiry",
        "Bitiş tarihi düzenlenme tarihinden önce olamaz.",
        "Expiry cannot precede issue date.",
      );
  }
  return errors;
}
export function payload(data: DraftData): DraftData {
  return Object.fromEntries(
    Object.entries(data)
      .filter(([, value]) => value.trim() !== "")
      .map(([key, value]) => [key, value.trim()]),
  );
}
