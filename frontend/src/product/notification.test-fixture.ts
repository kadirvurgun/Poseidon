import type { Inbox, NoticePreference } from './notification-inbox-api'
export const noticeVessel = '10000000-0000-4000-8000-000000000001'
export const noticeId = '10000000-0000-4000-8000-000000000002'
export const noticeSource = '10000000-0000-4000-8000-000000000003'
export const notificationInbox = (): Inbox => ({ offset: 0, hasMore: false, policyState: 'ConfiguredUnverified', delivery: 'InAppOnly', canManage: false, canAcknowledge: true, canWritePreference: true, items: [{ id: noticeId, vesselId: noticeVessel, sourceId: noticeSource, sourceRevision: 3, ruleKey: 'SYNTHETIC-MAINTENANCE', policyVersion: 'synthetic-v1', sourceState: 'Due', severity: 'Critical', mandatory: true, recipientRoleId: noticeSource, escalationRoleId: null, dueDate: '2026-11-05', createdAt: '2026-11-04T12:00:00Z', escalateAt: null, state: 'Open', revision: 1, version: 7, provenance: 'ConfiguredUnverified' }] })
export const notificationPreference = (): NoticePreference => ({ mutedAdvisory: false, revision: 0, version: 0, mandatoryRouteMutable: false })
