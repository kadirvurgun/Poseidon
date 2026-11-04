import { afterEach, expect, it, vi } from 'vitest'
import { decodeInbox, decodePreference, sendNoticeCommand } from './notification-inbox-api'
import { notificationInbox, notificationPreference, noticeVessel, noticeId } from './notification.test-fixture'

afterEach(() => vi.unstubAllGlobals())

it('DEV-036 keeps mandatory routes and source policy distinct from external delivery or operational approval', () => {
  expect(decodeInbox(notificationInbox(), noticeVessel, 0)).toEqual(notificationInbox())
  expect(decodePreference(notificationPreference()).mandatoryRouteMutable).toBe(false)
})
it.each([
  { ...notificationInbox(), delivery: 'EmailSent' }, { ...notificationInbox(), canAcknowledge: 'true' },
  { ...notificationInbox(), items: [{ ...notificationInbox().items[0], vesselId: notificationInbox().items[0].id }] },
  { ...notificationInbox(), items: [{ ...notificationInbox().items[0], mandatory: false }] },
  { ...notificationInbox(), items: [{ ...notificationInbox().items[0], state: 'Approved' }] },
  { ...notificationInbox(), items: [notificationInbox().items[0], notificationInbox().items[0]] },
])('rejects foreign context, invented approval/delivery, forged capability and duplicate notice %#', value => {
  expect(() => decodeInbox(value, noticeVessel, 0)).toThrow()
})
it('rejects a preference contract that would let advisory settings mute the mandatory route', () => {
  expect(() => decodePreference({ ...notificationPreference(), mandatoryRouteMutable: true })).toThrow()
})
it('sends the retained bytes and command UUID with CSRF on each explicitly requested acknowledgement retry', async () => {
  const fetch = vi.fn(async () => new Response(JSON.stringify({ id: noticeId, revision: 2, version: 8, state: 'Acknowledged' })))
  vi.stubGlobal('fetch', fetch)
  const command = { kind: 'acknowledge' as const, id: noticeId, key: '10000000-0000-4000-8000-000000000009', body: JSON.stringify({ expectedVersion: 7 }) }
  await sendNoticeCommand(noticeVessel, command, 'synthetic-csrf', new AbortController().signal)
  await sendNoticeCommand(noticeVessel, command, 'synthetic-csrf', new AbortController().signal)
  expect(fetch).toHaveBeenCalledTimes(2)
  expect(fetch).toHaveBeenLastCalledWith(`/api/v1/notification-escalation/${noticeId}/acknowledge?vesselId=${noticeVessel}`, expect.objectContaining({ method: 'POST', body: command.body, credentials: 'same-origin', headers: expect.objectContaining({ 'X-CSRF-TOKEN': 'synthetic-csrf', 'Idempotency-Key': command.key }) }))
  expect(JSON.parse(command.body)).not.toHaveProperty('actorId')
})
