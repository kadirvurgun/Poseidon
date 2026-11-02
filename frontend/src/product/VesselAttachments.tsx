import { useState } from 'react'
import { VesselFiles } from './VesselFiles'
import { VesselDocuments } from './VesselDocuments'

export function VesselAttachments(props: Parameters<typeof VesselFiles>[0]) {
  const [mode, setMode] = useState<'files' | 'documents'>('files')
  const [dirty, setDirty] = useState(false); const [busy, setBusy] = useState(false)
  const shared = { ...props, onDirty: (v: boolean) => { setDirty(v); props.onDirty(v) }, onSaving: (v: boolean) => { setBusy(v); props.onSaving(v) } }
  return <section>
    <nav aria-label={props.language === 'en' ? 'Attachment context' : 'Ek bağlamı'}>
      <button aria-pressed={mode === 'files'} disabled={dirty || busy} onClick={() => setMode('files')}>{props.language === 'en' ? 'Vessel attachments' : 'Araç ekleri'}</button>
      <button aria-pressed={mode === 'documents'} disabled={dirty || busy} onClick={() => setMode('documents')}>{props.language === 'en' ? 'Document drafts' : 'Belge taslakları'}</button>
    </nav>
    {mode === 'files' ? <VesselFiles {...shared} /> : <VesselDocuments {...shared} />}
  </section>
}
