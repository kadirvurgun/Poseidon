// Ship-number arithmetic only; never company IMO or registry verification.
export function imoIssue(value: string): "format" | "checksum" | null {
  if (value.length !== 7 || !/^[0-9]{7}$/.test(value)) return "format";
  let sum = 0;
  for (let i = 0; i < 6; i++) sum += Number(value[i]) * (7 - i);
  return sum % 10 === Number(value[6]) ? null : "checksum";
}
export function imoMessage(value: string | undefined, lang: "tr" | "en"): string | null {
  if (!value) return null;
  const issue = imoIssue(value);
  if (issue === "format") return lang === "tr" ? "IMO tam 7 rakam olmalıdır." : "IMO must contain exactly 7 digits.";
  if (issue === "checksum") return lang === "tr"
    ? "IMO kontrol basamağı uyuşmuyor. Kaynağından kontrol edin veya isteğe bağlı alanı boş bırakın."
    : "IMO check digit does not match. Check the source or leave this optional field blank.";
  return null;
}
