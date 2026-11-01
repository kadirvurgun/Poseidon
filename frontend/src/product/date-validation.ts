// Shared .NET DateOnly / DateTimeOffset component and UTC range boundary.
export function calendar(v: string): boolean {
  if (v.length !== 10 || !/^[0-9]{4}-[0-9]{2}-[0-9]{2}$/.test(v)) return false
  const [year, month, day] = v.split('-').map(Number)
  const leap = year % 4 === 0 && (year % 100 !== 0 || year % 400 === 0)
  return year >= 1 && month >= 1 && month <= 12 && day >= 1 && day <= [31, leap ? 29 : 28, 31, 30, 31, 30, 31, 31, 30, 31, 30, 31][month - 1]
}
export function timestamp(v: unknown): v is string {
  if (typeof v !== 'string') return false
  const match = /^([0-9]{4}-[0-9]{2}-[0-9]{2})T([0-9]{2}):([0-9]{2}):([0-9]{2})(?:\.[0-9]{1,7})?(Z|([+-])([0-9]{2}):([0-9]{2}))$/.exec(v)
  if (!match || match[0] !== v || !calendar(match[1]) || +match[2] > 23 || +match[3] > 59 || +match[4] > 59) return false
  if (match[5] !== 'Z' && (+match[7] > 14 || +match[8] > 59 || (+match[7] === 14 && +match[8] !== 0))) return false
  // Calendar validity was checked first; parsing here checks UTC range at extreme offsets.
  const instant = Date.parse(v)
  return Number.isFinite(instant) && instant >= -62135596800000 && instant <= 253402300799999
}
