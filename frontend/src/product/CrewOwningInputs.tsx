import { useEffect, useState } from 'react'
import { ApiError } from './api'
import { owningInputs, type CrewOwningInput } from './crew-api'

export function CrewOwningInputs({ vessel, assignment, language }: { vessel: string; assignment: string; language: 'tr' | 'en' }) {
  const en = language === 'en', [inputs, setInputs] = useState<CrewOwningInput[] | null>(null), [failed, setFailed] = useState(false), [denied, setDenied] = useState(false), [generation, reload] = useState(0)
  useEffect(() => {
    const c = new AbortController(); setInputs(null); setFailed(false); setDenied(false)
    owningInputs(vessel, assignment, c.signal).then(v => { if (!c.signal.aborted) setInputs(v) }).catch(e => {
      if (!c.signal.aborted) { setFailed(true); setDenied(e instanceof ApiError && [401, 403, 404].includes(e.status)) }
    })
    return () => c.abort()
  }, [vessel, assignment, generation])
  const label: Record<string, string> = en ? { certificate_state: 'Certificate input', contact_status: 'Contact input', competence_state: 'Competence', work_rest_state: 'Work/rest', eligibility_state: 'Eligibility', fitness_state: 'Fitness' }
    : { certificate_state: 'Belge girdisi', contact_status: 'İletişim girdisi', competence_state: 'Yetkinlik', work_rest_state: 'Çalışma/dinlenme', eligibility_state: 'Uygunluk', fitness_state: 'Sağlık uygunluğu' }
  const states = en ? { Withheld: 'Access withheld', Missing: 'No visible declaration', DeclaredUnverified: 'Declared, unverified', Unavailable: 'Protected input unavailable', EvaluationUnavailable: 'Qualified evaluation unavailable; remains Unknown' }
    : { Withheld: 'Erişim gizli', Missing: 'Görünür beyan yok', DeclaredUnverified: 'Beyan edilmiş, doğrulanmamış', Unavailable: 'Korumalı girdi kullanılamıyor', EvaluationUnavailable: 'Yetkili değerlendirme kullanılamıyor; Bilinmiyor olarak kalır' }
  return <section aria-label={en ? 'Crew input status' : 'Mürettebat girdi durumu'}><h2>{en ? 'Crew input status' : 'Mürettebat girdi durumu'}</h2>
    <p>{en ? 'A recorded declaration does not establish authenticity, competence, fitness, eligibility or a safety decision.' : 'Kayıtlı beyan gerçeklik, yetkinlik, sağlık uygunluğu, göreve uygunluk veya emniyet kararı oluşturmaz.'}</p>
    {!inputs && !failed && <p role="status">{en ? 'Loading input status…' : 'Girdi durumu yükleniyor…'}</p>}
    {failed && <p role="alert">{denied ? (en ? 'Crew input access denied.' : 'Mürettebat girdi erişimi reddedildi.') : (en ? 'Crew input status unavailable.' : 'Mürettebat girdi durumu kullanılamıyor.')}</p>}
    <button onClick={() => reload(x => x + 1)}>{en ? 'Refresh input status' : 'Girdi durumunu yenile'}</button>
    {inputs && <dl>{inputs.map(v => <div key={v.field}><dt>{label[v.field]}</dt><dd>{states[v.state]}</dd></div>)}</dl>}
  </section>
}
