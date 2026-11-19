import { render, screen, within } from '@testing-library/react'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { PlannerView, checklistHref, readSurveyLocation } from './SurveyWorkspace'
import { surveyCopy } from './PreSurveyChecklistPanel'
import { ApiError } from './api'
import { surveyApi, type Planner } from './survey-api'
import { surveyCertificateId, surveyChecklistId, surveyVessel } from './survey-test-fixtures'

const row = (id: string, window: Planner['items'][number]['window'], fit: Planner['items'][number]['fit'], checklist: Planner['items'][number]['checklist'] = null): Planner['items'][number] => ({
  vesselId: surveyVessel, vesselCode: 'FICT-1', vesselName: 'Fictional One', vesselKind: 'Ship', certificateId: id, certificateRevision: 1, certificateVersion: 3, certificateType: 'SYNTHETIC-CERT',
  certificateNumber: null, issuer: 'Fictional issuer', declaredFlag: 'FICTIONAL', regulatoryBasis: null, expiryDate: '2027-06-01', window, fit, checklist, coordination: 'EXTERNAL_NOT_SIMULATED',
  href: `/app/certificate-command-center/${surveyVessel}?id=${id}` })
const planner = (): Planner => ({
  from: '2026-10-21', to: '2027-11-20', referenceDate: '2026-11-20', truncated: true, vesselsTruncated: false,
  items: [
    row(surveyCertificateId, { state: 'OPEN', start: '2026-11-01', end: '2027-02-01', daysToOpen: 0, daysToClose: 73, daysOverdue: null, qualifiers: ['DECLARED_WINDOW_NOT_SOURCE_POLICY'] },
      { state: 'CONDITIONAL', reasons: ['VOYAGE_UNKNOWN', 'AUTHORITY_COORDINATION_EXTERNAL'], basis: [] },
      { id: surveyChecklistId, revision: 3, readiness: 'PREPARED_PENDING_HUMAN_CONFIRMATION', criticalUnresolved: 0, requiredUnresolved: 0, plannedStart: '2026-12-01', plannedEnd: '2026-12-02', portCallReference: 'CURRENT', href: `/app/pre-survey-checklist/${surveyVessel}?id=${surveyChecklistId}` }),
    row('99999999-9999-4999-8999-999999999999', { state: 'UNKNOWN', start: null, end: null, daysToOpen: null, daysToClose: null, daysOverdue: null, qualifiers: ['WINDOW_NOT_DECLARED'] },
      { state: 'UNKNOWN', reasons: ['WINDOW_UNKNOWN', 'NO_PREPARATION_PLAN'], basis: [] }),
  ],
  counts: { windowUnknown: 1, windowUpcoming: 0, windowOpen: 1, windowOverdue: 0, fitConditional: 1, fitNotFit: 0, fitUnknown: 1, checklistBlocked: 0, checklistMissing: 1 },
  qualification: 'ADVISORY_PLANNING_NOT_AUTHORITY_SCHEDULE', fitStates: ['CONDITIONAL', 'NOT_FIT', 'UNKNOWN'], sourceVersion: 'synthetic',
})

describe('DEV-104/DEV-112 survey routes', () => {
  it('reads canonical planner and checklist locations and rejects malformed ones', () => {
    expect(readSurveyLocation('/app/survey-planner', '')).toMatchObject({ page: 'planner', invalid: false })
    expect(readSurveyLocation('/app/survey-planner', '?x=1').invalid).toBe(true)
    expect(readSurveyLocation(`/app/pre-survey-checklist/${surveyVessel}`, `?id=${surveyChecklistId}`)).toMatchObject({ page: 'checklist', vessel: surveyVessel, id: surveyChecklistId, invalid: false })
    expect(readSurveyLocation('/app/pre-survey-checklist', `?id=${surveyChecklistId}`).invalid).toBe(true)
    expect(readSurveyLocation(`/app/pre-survey-checklist/${surveyVessel}`, '?id=not-a-guid').invalid).toBe(true)
    expect(checklistHref(surveyVessel, null, surveyCertificateId)).toBe(`/app/pre-survey-checklist/${surveyVessel}?certificateId=${surveyCertificateId}`)
  })
})

describe('DEV-112 planner view', () => {
  afterEach(() => vi.restoreAllMocks())
  it('groups declared windows by month, keeps undeclared windows UNKNOWN and never shows FIT or a ready state', async () => {
    vi.spyOn(surveyApi, 'planner').mockResolvedValue(planner())
    render(<PlannerView online t={surveyCopy.en} onOpen={() => undefined}/>)
    const open = await screen.findByRole('region', { name: '2026-11' })
    expect(within(open).getByText('Window open')).toBeVisible()
    expect(within(open).getByText('Conditional')).toBeVisible()
    expect(within(open).getByText('Prepared, pending human confirmation')).toBeVisible()
    expect(within(open).getByText('VOYAGE_UNKNOWN')).toBeVisible()
    const unknown = screen.getByRole('region', { name: 'Window not declared' })
    expect(within(unknown).getByText('Window unknown')).toBeVisible()
    expect(within(unknown).getByRole('button', { name: 'Start checklist' })).toBeVisible()
    expect(screen.getByText(/Only part of the fleet is shown/)).toBeVisible()
    expect(screen.getByText(/coordination is external/)).toBeVisible()
    expect(screen.queryByText(/^Fit$/)).not.toBeInTheDocument()
  })
  it('reports denial without rendering stale rows', async () => {
    vi.spyOn(surveyApi, 'planner').mockRejectedValue(new ApiError(403, 'permission_denied'))
    render(<PlannerView online t={surveyCopy.en} onOpen={() => undefined}/>)
    expect(await screen.findByRole('alert')).toHaveTextContent('You do not have access to this view.')
    expect(screen.queryByText('Fictional One')).not.toBeInTheDocument()
  })
})
