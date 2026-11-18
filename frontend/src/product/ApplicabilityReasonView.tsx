import type { ReactNode } from 'react'
import { Badge } from '../components/ui/badge'
import type { EvaluationResult, Reason } from './applicability-api'

function reasonTree(reason: Reason, en: boolean): ReactNode {
  return <li key={reason.path}><strong>{reason.field ?? reason.operator ?? reason.path}</strong> · {reason.truth === 0 ? (en ? 'Unknown' : 'Bilinmiyor') : reason.truth === 1 ? (en ? 'False' : 'Yanlış') : (en ? 'True' : 'Doğru')} · {reason.reason}{reason.reviewRequired && <Badge tone="warning">{en ? 'Review required' : 'İnceleme gerekli'}</Badge>}{reason.children.length > 0 && <ul>{reason.children.map(child => reasonTree(child, en))}</ul>}</li>
}
export function ApplicabilityReasonView({ results, language }: { results: EvaluationResult[]; language: 'en' | 'tr' }) {
  const en = language === 'en', unknown = en ? 'Unavailable' : 'Kullanılamıyor'
  return <div className="applicability-reason-list">{results.map(row => <details key={`${row.ruleId}/${row.revision}`}><summary>{en ? 'Rule' : 'Kural'} {row.ruleId} / {row.revision} · {row.prediction.result}</summary><dl><div><dt>{en ? 'Requirement / revision' : 'Gereksinim / sürüm'}</dt><dd>{row.requirementId ?? unknown} / {row.requirementRevision ?? unknown}</dd></div><div><dt>{en ? 'Source / revision' : 'Kaynak / sürüm'}</dt><dd>{row.sourceId ?? unknown} / {row.sourceRevision ?? unknown}</dd></div><div><dt>{en ? 'Recorded lifecycle' : 'Kayıtlı yaşam döngüsü'}</dt><dd>{row.prediction.lifecycle}</dd></div><div><dt>{en ? 'Current result at evaluation' : 'Değerlendirmedeki güncel sonuç'}</dt><dd>{row.prediction.currentResult}</dd></div></dl><p>{row.prediction.authoritative ? (en ? 'Recorded qualified-input evaluation. Currentness and human approval remain separate.' : 'Kayıtlı nitelendirilmiş girdi değerlendirmesi. Güncellik ve insan onayı ayrıdır.') : (en ? 'Preview only. This is not a current obligation.' : 'Yalnız önizleme. Bu güncel bir yükümlülük değildir.')}</p><ul aria-label={en ? 'Applicability reasons' : 'Uygulanabilirlik gerekçeleri'}>{reasonTree(row.prediction.reason, en)}</ul><ul>{row.prediction.blockers.map((code, index) => <li key={`${code}/${index}`}>{code}</li>)}</ul><ul>{row.prediction.predictedActivations.map(a => <li key={`${a.type}:${a.id}`}>{a.type}: {a.id} · {en ? 'Projected need only' : 'Yalnız öngörülen ihtiyaç'}</li>)}</ul><p>{en ? 'Exact rule integrity hash' : 'Kesin kural bütünlük özeti'}: <code>{row.hash}</code></p></details>)}</div>
}
// Inputs are already admitted by the strict owning snapshot decoder. Keep this
// presentation read-only; showing a captured fact does not re-qualify it.
export function CapturedEvaluationInputs({ inputs, inputHash, ruleSetHash, language }: { inputs: Record<string, unknown> | null; inputHash: string | null; ruleSetHash: string | null; language: 'en' | 'tr' }) {
  const en = language === 'en', unavailable = en ? 'Unavailable' : 'Kullanılamıyor'
  const facts = inputs?.facts
  const groups: { title: string; facts: Record<string, unknown> }[] = []
  if (facts && typeof facts === 'object' && !Array.isArray(facts)) groups.push({ title: en ? 'Captured context facts' : 'Kaydedilmiş bağlam olguları', facts: facts as Record<string, unknown> })
  const qualifications = inputs?.qualifications
  if (Array.isArray(qualifications)) for (const qualification of qualifications) {
    if (qualification && typeof qualification === 'object' && !Array.isArray(qualification)) {
      const q = qualification as Record<string, unknown>
      if (q.facts && typeof q.facts === 'object' && !Array.isArray(q.facts)) groups.push({ title: `${en ? 'Qualified rule inputs' : 'Nitelendirilmiş kural girdileri'}: ${String(q.ruleId)} / ${String(q.revision)}`, facts: q.facts as Record<string, unknown> })
    }
  }
  return <section aria-label={en ? 'Exact evaluated inputs' : 'Kesin değerlendirilmiş girdiler'}><h4>{en ? 'Exact evaluated inputs' : 'Kesin değerlendirilmiş girdiler'}</h4><p>{en ? 'Immutable recorded inputs. Saved declarations and historical evaluations do not establish current approval.' : 'Değişmez kayıtlı girdiler. Kayıtlı beyanlar ve tarihsel değerlendirmeler güncel onay oluşturmaz.'}</p><dl className="onboarding-review-values"><div><dt>{en ? 'Input integrity hash' : 'Girdi bütünlük özeti'}</dt><dd><code>{inputHash ?? unavailable}</code></dd></div><div><dt>{en ? 'Rule-set integrity hash' : 'Kural kümesi bütünlük özeti'}</dt><dd><code>{ruleSetHash ?? unavailable}</code></dd></div><div><dt>{en ? 'Evaluation as of' : 'Değerlendirme zamanı'}</dt><dd>{typeof inputs?.asOf === 'string' ? inputs.asOf : unavailable}</dd></div></dl>{!groups.length && <p>{unavailable}</p>}{groups.map(group => <details key={group.title}><summary>{group.title}</summary><dl className="onboarding-review-values">{Object.entries(group.facts).map(([key, fact]) => {
    if (!fact || typeof fact !== 'object' || Array.isArray(fact)) return null
    const value = fact as Record<string, unknown>
    return <div key={key}><dt>{key}</dt><dd>{String(value.knowledge)} · {value.value === null ? unavailable : String(value.value)}<p>{en ? 'Source observation / freshness limit' : 'Kaynak gözlemi / güncellik sonu'}: {typeof value.observedAt === 'string' ? value.observedAt : unavailable} / {typeof value.freshUntil === 'string' ? value.freshUntil : unavailable}</p><p>{en ? 'Provenance / revision' : 'Köken / sürüm'}: {String(value.provenance)} / {String(value.revision)}</p></dd></div>
  })}</dl></details>)}</section>
}
