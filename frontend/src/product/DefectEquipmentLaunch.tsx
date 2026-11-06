import { useEffect, useState } from 'react'
import { Button } from '../components/ui/button'
import { EnterpriseActions } from './EnterprisePatterns'
import { defectApi } from './defect-api'
import type { Session } from './directory-contract'
import { isGuid } from './identifiers'

// A saved equipment pointer is launch context, never an inferred criticality,
// copied engineering assessment or an authorization to return to service.
export function DefectEquipmentLaunch({ session, vessel, assetId, revision, language, disabled, onNavigate }: {
  session: Session; vessel: string; assetId: string; revision: number; language: 'en' | 'tr';
  disabled: boolean; onNavigate: (href: string) => void
}) {
  const en = language === 'en'; const source = `${vessel}:${assetId}:${revision}`
  const [grant, setGrant] = useState<{ owner: Session; source: string; canWrite: boolean } | null>(null)
  useEffect(() => {
    if (!session.authenticated || !session.canRead) return
    const controller = new AbortController(); let active = true
    const timer = setTimeout(() => controller.abort(), 10000)
    defectApi.capabilities(vessel, controller.signal).then(caps => {
      if (active && !controller.signal.aborted) setGrant({ owner: session, source, canWrite: caps.canWrite })
    }).catch(() => { if (active) setGrant({ owner: session, source, canWrite: false }) })
    return () => { active = false; controller.abort(); clearTimeout(timer) }
  }, [session, vessel, source])
  if (!isGuid(vessel) || !isGuid(assetId) || !Number.isSafeInteger(revision) || revision < 1 || grant?.owner !== session || grant.source !== source || !grant.canWrite) return null
  return <section aria-label={en ? 'Defect linked to saved equipment' : 'Kayıtlı ekipmana bağlı arıza'}>
    <h3>{en ? 'Report a defect' : 'Arıza kaydı aç'}</h3>
    <p>{en ? 'Start a separate declaration linked to this saved equipment revision.' : 'Bu kayıtlı ekipman sürümüne bağlı ayrı bir beyan açın.'}</p>
    <EnterpriseActions align="start"><Button variant="outline" disabled={disabled} onClick={() => {
      if (!disabled) onNavigate(`/app/defects/${vessel}?${new URLSearchParams({ assetId, assetRevision: String(revision) })}`)
    }}>{en ? 'Open linked defect draft' : 'Bağlı arıza taslağı aç'}</Button></EnterpriseActions>
  </section>
}
