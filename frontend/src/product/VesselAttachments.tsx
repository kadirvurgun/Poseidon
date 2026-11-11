import { useState } from 'react'
import { VesselFiles } from './VesselFiles'
import { VesselDocuments } from './VesselDocuments'
import { Button } from '../components/ui/button'
import { EnterpriseActions } from './EnterprisePatterns'
import { localizedText } from '../i18n/messages'
import { presentationLabel } from '../i18n/labels'

export function VesselAttachments(props: Parameters<typeof VesselFiles>[0]) {
  const [mode, setMode] = useState<'files' | 'documents'>('files')
  const [dirty, setDirty] = useState(false); const [busy, setBusy] = useState(false)
  const shared = { ...props, onDirty: (v: boolean) => { setDirty(v); props.onDirty(v) }, onSaving: (v: boolean) => { setBusy(v); props.onSaving(v) } }
  return <section>
    <nav aria-label={presentationLabel(props.language, 'attachmentContext')}>
      <EnterpriseActions align="start">
        <Button type="button" variant={mode === 'files' ? 'secondary' : 'outline'} aria-pressed={mode === 'files'} disabled={dirty || busy} onClick={() => setMode('files')}>{presentationLabel(props.language, 'vesselAttachments')}</Button>
        <Button type="button" variant={mode === 'documents' ? 'secondary' : 'outline'} aria-pressed={mode === 'documents'} disabled={dirty || busy} onClick={() => setMode('documents')}>{localizedText(props.language, 'pairs.m_e9c1e56ebf003d9d')}</Button>
      </EnterpriseActions>
    </nav>
    {mode === 'files' ? <VesselFiles {...shared} /> : <VesselDocuments {...shared} />}
  </section>
}
