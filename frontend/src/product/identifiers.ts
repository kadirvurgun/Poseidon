// Canonical nonempty .NET GUID boundary. Length prevents JS $ matching before a final newline.
export function isGuid(value: unknown): value is string {
  return typeof value === 'string' && value.length === 36 &&
    /^[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}$/i.test(value) &&
    value !== '00000000-0000-0000-0000-000000000000'
}
