import { useEffect, useRef, useState } from 'react'
import { crewFileVersions, type CrewFileKind, type CrewFileManifest } from './crew-files-api'
import type { PersonFileOwner } from './file-api'
import { VesselFiles } from './VesselFiles'

type Props = { vessel: string; assignment: string; person: string; language: 'tr' | 'en'; csrf: string; onDirty: (value: boolean) => void }
// Parent mounts this private child for one current authenticated session/tenant/vessel/person only.
export function CrewPersonalFiles(props: Props) {
  const en = props.language === 'en'
  const scope = `${props.vessel}:${props.assignment}:${props.person}:${props.csrf}`
  const [loaded, setLoaded] = useState<{ scope: string; data: CrewFileManifest } | null>(null)
  const [owner, setOwner] = useState<{ scope: string; data: PersonFileOwner } | null>(null)
  const [kind, setKind] = useState<CrewFileKind>('Photo'); const [expiry, setExpiry] = useState('')
  const [error, setError] = useState(false); const [dirty, setDirty] = useState(false); const [busy, setBusy] = useState(false)
  const [generation, reload] = useState(0)
  const source = loaded?.scope === scope ? loaded.data : null
  const selected = owner?.scope === scope ? owner.data : undefined
  const changeDirty = (value: boolean) => { setDirty(value); props.onDirty(value) }
  const callbacks = useRef(props); callbacks.current = props
  useEffect(() => () => callbacks.current.onDirty(false), [])
  useEffect(() => {
    const c = new AbortController(); let active = true
    setLoaded(null); setError(false)
    crewFileVersions(props.vessel, props.assignment, props.person, c.signal).then(data => {
      if (active) { setLoaded({ scope, data }); setKind(old => data.kinds.includes(old) ? old : data.kinds[0]) }
    }).catch(() => {
      if (active) { setLoaded(null); setOwner(null); setExpiry(''); setDirty(false); callbacks.current.onDirty(false); setError(true) }
    })
    return () => { active = false; c.abort() }
  }, [scope, generation, props.vessel, props.assignment, props.person])
  const label = (value: CrewFileKind) => ({ Photo: en ? 'Photo' : 'Fotoğraf', Passport: en ? 'Passport scan' : 'Pasaport taraması',
    SeamanBook: en ? 'Seaman book scan' : 'Gemi adamı cüzdanı taraması', CertificateDocument: en ? 'Certificate document' : 'Sertifika belgesi' })[value]
  return <section aria-label={en ? 'Private person files' : 'Özel personel dosyaları'}>
    <h2>{en ? 'Private person files' : 'Özel personel dosyaları'}</h2>
    <p>{en ? 'Versioned scanned declarations. Scan and expiry metadata do not establish authenticity, eligibility or legal approval.' : 'Sürümlü taranmış beyanlar. Tarama ve bitiş tarihi bilgisi özgünlük, uygunluk veya hukuki onay sağlamaz.'}</p>
    {error && <p role="alert">{en ? 'Private file access or policy unavailable.' : 'Özel dosya erişimi veya politikası kullanılamıyor.'}</p>}
    <button disabled={dirty || busy} onClick={() => { setOwner(null); reload(n => n + 1) }}>{en ? 'Refresh private files' : 'Özel dosyaları yenile'}</button>
    {source && <>
      <p>{source.policyVersion} · {source.purpose}</p>
      {source.canWrite && <form onSubmit={e => { e.preventDefault(); const revision = source.next[kind]; if (revision) setOwner({ scope, data: { type: 'Person', assignment: props.assignment, kind, revision, expiresOn: kind === 'Photo' ? null : expiry || null } }) }}>
        <fieldset disabled={dirty || busy}><label>{en ? 'Private file kind' : 'Özel dosya türü'}<select value={kind} onChange={e => { setKind(e.target.value as CrewFileKind); setExpiry(''); setOwner(null) }}>{source.kinds.map(k => <option key={k} value={k}>{label(k)}</option>)}</select></label>
          {kind !== 'Photo' && <label>{en ? 'Declared expiry date (unknown if blank)' : 'Beyan edilen bitiş tarihi (boşsa bilinmiyor)'}<input type="date" value={expiry} onChange={e => { setExpiry(e.target.value); setOwner(null) }} /></label>}
          <button>{en ? 'Start new private version' : 'Yeni özel sürüm başlat'}</button></fieldset>
      </form>}
      <ol>{source.items.map(row => <li key={row.id}>
        <button disabled={dirty || busy} onClick={() => setOwner({ scope, data: { type: 'Person', assignment: props.assignment, kind: row.kind, revision: row.revision, expiresOn: row.expiresOn } })}>{label(row.kind)} · {row.revision}</button>
        {' · '}{row.expiresOn ?? (en ? 'Expiry unknown' : 'Bitiş tarihi bilinmiyor')}{' · '}{row.state === 'CleanTransport' ? (en ? 'Scanned, unverified' : 'Taranmış, doğrulanmamış') : (en ? 'Pending or quarantined' : 'Bekliyor veya karantinada')}
      </li>)}</ol>
      {source.hasMore && <p role="status">{en ? 'Only the latest 100 declarations are shown.' : 'Son 100 beyan gösteriliyor.'}</p>}
      {selected && <VesselFiles key={`${scope}:${selected.kind}:${selected.revision}`} owner={selected} vessel={props.vessel} csrf={props.csrf} language={props.language}
        onDirty={changeDirty} onSaving={setBusy} onCommitted={() => reload(n => n + 1)} />}
    </>}
  </section>
}
