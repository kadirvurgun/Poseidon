import { afterEach, describe, expect, it, vi } from 'vitest'
import { ApiError, vessel, vessels } from './api'

const response = (body: unknown) => ({ ok: true, status: 200, json: async () => body }) as Response
const ship = { id: 'vessel-1', companyId: 'company-1', code: 'S1', name: 'Synthetic Ship', kind: 'Ship', version: 1, canEdit: false }
afterEach(() => vi.unstubAllGlobals())

describe('product API vessel contract decoding', () => {
  it('decodes valid detail and paged responses, including generated string pagination fields', async () => {
    const fetchMock = vi.fn().mockResolvedValueOnce(response(ship)).mockResolvedValueOnce(response({ items: [ship], offset: '0', limit: '25', hasMore: false }))
    vi.stubGlobal('fetch', fetchMock)
    const signal = new AbortController().signal

    expect(await vessel('vessel-1', signal)).toEqual(ship)
    expect(await vessels(0, '', signal)).toEqual({ items: [ship], offset: '0', limit: '25', hasMore: false })
  })

  it.each([
    ['detail', () => vessel('vessel-1', new AbortController().signal), () => response({ ...ship, kind: 'Unknown' })],
    ['list', () => vessels(0, '', new AbortController().signal), () => response({ items: [{ ...ship, kind: 'Unknown' }], offset: 0, limit: 25, hasMore: false })],
  ])('rejects an unrecognized vessel kind in %s responses with a safe API error', async (_path, request, body) => {
    vi.stubGlobal('fetch', vi.fn().mockResolvedValue(body()))
    try {
      await request()
      expect.fail('Expected malformed vessel data to be rejected')
    } catch (error) {
      expect(error).toBeInstanceOf(ApiError)
      expect(error).toMatchObject({ status: 502 })
    }
  })
})
