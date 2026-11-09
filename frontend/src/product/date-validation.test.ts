import { describe, expect, it } from 'vitest'
import { calendar, timestamp } from './date-validation'

describe('untrusted calendar wire values', () => {
  it.each([null, undefined, 20261109, {}, [], true])('rejects non-string input without throwing: %j', value => {
    expect(calendar(value)).toBe(false)
  })

  it('accepts a Gregorian leap date and rejects rollover, zero-year and appended input', () => {
    expect(calendar('2000-02-29')).toBe(true)
    for (const value of ['1900-02-29', '2026-02-30', '0000-01-01', '2026-11-09\n', '2026-11-09T00:00:00Z']) {
      expect(calendar(value)).toBe(false)
    }
  })

  it('keeps timestamp offset and UTC-range boundaries explicit', () => {
    expect(timestamp('2026-11-09T12:00:00+14:00')).toBe(true)
    expect(timestamp('2026-11-09T12:00:00+14:01')).toBe(false)
    expect(timestamp('0001-01-01T00:00:00+00:01')).toBe(false)
    expect(timestamp('2026-11-09T12:00:00')).toBe(false)
  })
})
