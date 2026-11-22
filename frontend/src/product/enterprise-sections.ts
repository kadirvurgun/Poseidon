// Sidebar sections of the enterprise navigation (presentation grouping only; it grants nothing).
// Pure data so that the offline user manual generator (USER-GUIDE-01 UG-4) groups page guides
// exactly as the sidebar does. `label` is the i18n message key of the section title.
export type EnterpriseSection = { key: string; label: string; keys: readonly string[] }

export const enterpriseSections: readonly EnterpriseSection[] = Object.freeze([
  { key: 'overview', label: 'pairs.m_5f50c36af429c54d', keys: ['today', 'fleet'] },
  { key: 'operations', label: 'pairs.m_d045227afb1d4aaa', keys: ['noon-report', 'deck-daily-log', 'engine-daily-log', 'fuel-bunker-log', 'port-activity-sof', 'operational-downtime', 'departure-signoff'] },
  { key: 'technical', label: 'pairs.m_470cefc094dedfe3', keys: ['equipment', 'maintenance', 'pms-command-center', 'defects', 'service-engineer-report', 'work-order-center', 'critical-spares', 'drydock-planning'] },
  { key: 'crew', label: 'pairs.m_a6f7eeadcb7b0ca5', keys: ['crew', 'crew-certificates', 'crew-competence', 'safe-manning'] },
  { key: 'tug', label: 'pairs.m_8d10fb1a220cd756', keys: ['tug-command', 'tug-job-board', 'tug-job-intake', 'tug-assignment', 'tug-readiness', 'tug-equipment-readiness', 'tug-crew-readiness', 'towage-plan', 'towage-risk-briefing', 'towage-execution', 'tug-daily-log', 'tug-operation-history', 'tug-operations-offline', 'tug-demo-journey'] },
  { key: 'assurance', label: 'pairs.m_dda1fc9a101a6728', keys: ['risk-assessment', 'voice-event-draft', 'document-compare', 'role-uat', 'pilot-gate', 'pilot-program', 'risk-template-library', 'high-risk-work', 'safety-events', 'industry-assurance', 'data-trust-center', 'certificate-command-center', 'renewal-workflow', 'pre-survey-checklist', 'survey-planner', 'regulatory-review', 'sources', 'requirements', 'audit', 'audit-ready', 'import', 'data-reconciliation'] },
  { key: 'workspace', label: 'pairs.m_c3e5a55de61effb3', keys: ['companies', 'user-admin', 'system-health', 'notification-escalation', 'offline-conflict-center'] },
].map(section => Object.freeze({ ...section, keys: Object.freeze(section.keys) })))
