import { isGuid } from './identifiers'
import { timestamp } from './date-validation'

export type CapabilityStatement = { kind: 'BollardPullTest' | 'Condition'; assetId: string | null; assetRevision: number | null; assetVersion: number | null
  profileVersion: number | null; certifiedTonnes: string | null; measuredTonnes: string | null; testAt: string | null; reviewAt: string | null
  conditionFactor: string | null; sourceLocator: string | null; conditionEvidenceId: string | null; conditionEvidenceRevision: number | null; conditionEvidenceVersion: number | null }
export type CalculationPolicy = { formula: 'MeasuredTimesVerifiedCondition' | null; aggregation: 'SumForPlanning' | null; sourceLocator: string | null
  jobTypes: string[] | null; areas: string[] | null; scale: number | null; rounding: 'Floor' | null; maxTestAgeDays: number | null; maxLossPercent: string | null }
type Obj = Record<string, unknown>
const exact = (v: unknown, keys: string[]): v is Obj => !!v && typeof v === 'object' && !Array.isArray(v) && Object.keys(v).length === keys.length && keys.every(k => Object.hasOwn(v, k))
const integer = (v: unknown, max = Number.MAX_SAFE_INTEGER) => typeof v === 'number' && Number.isSafeInteger(v) && v > 0 && v <= max
const text = (v: unknown) => typeof v === 'string' && v.length > 0 && v.length <= 2000 && v === v.trim() && !/\p{Cc}/u.test(v)
export const bpNumber = (v: unknown) => typeof v === 'string' && /^(?:0|[1-9][0-9]{0,5})(?:\.[0-9]{1,6})?$(?![\s\S])/.test(v)
export const blankCapability = (kind: CapabilityStatement['kind']): CapabilityStatement => ({ kind, assetId: null, assetRevision: null, assetVersion: null, profileVersion: null,
  certifiedTonnes: null, measuredTonnes: null, testAt: null, reviewAt: null, conditionFactor: null, sourceLocator: null, conditionEvidenceId: null, conditionEvidenceRevision: null, conditionEvidenceVersion: null })
export const blankCalculation = (): CalculationPolicy => ({ formula: null, aggregation: null, sourceLocator: null, jobTypes: null, areas: null, scale: null, rounding: null, maxTestAgeDays: null, maxLossPercent: null })
export function validCapability(v: unknown): v is CapabilityStatement {
  if (!exact(v, Object.keys(blankCapability('Condition'))) || !['Condition', 'BollardPullTest'].includes(v.kind as string)) return false
  if ([v.assetId, v.conditionEvidenceId].some(x => x !== null && !isGuid(x)) || [v.assetRevision, v.conditionEvidenceRevision].some(x => x !== null && !integer(x)) ||
    [v.assetVersion, v.profileVersion, v.conditionEvidenceVersion].some(x => x !== null && !integer(x, 4294967295)) ||
    [v.certifiedTonnes, v.measuredTonnes, v.conditionFactor].some(x => x !== null && !bpNumber(x)) ||
    [v.testAt, v.reviewAt].some(x => x !== null && !timestamp(x)) || v.sourceLocator !== null && !text(v.sourceLocator)) return false
  if ((v.assetId === null) !== (v.assetRevision === null) || (v.conditionEvidenceId === null) !== (v.conditionEvidenceRevision === null) || (v.conditionEvidenceId === null) !== (v.conditionEvidenceVersion === null)) return false
  if (v.kind === 'Condition' && [v.certifiedTonnes, v.measuredTonnes, v.testAt, v.reviewAt, v.conditionEvidenceId].some(x => x !== null) || v.kind === 'BollardPullTest' && v.conditionFactor !== null) return false
  // Decimal values remain lexical. Values >1 have a nonzero integer part other than 1.000000.
  if (v.conditionFactor !== null && !/^(?:0(?:\.[0-9]{1,6})?|1(?:\.0{1,6})?)$/.test(v.conditionFactor as string)) return false
  return !(v.testAt && v.reviewAt && Date.parse(v.reviewAt as string) <= Date.parse(v.testAt as string))
}
export function validCalculation(v: unknown): v is CalculationPolicy {
  if (!exact(v, Object.keys(blankCalculation())) || v.formula !== null && v.formula !== 'MeasuredTimesVerifiedCondition' || v.aggregation !== null && v.aggregation !== 'SumForPlanning' || v.rounding !== null && v.rounding !== 'Floor' ||
    v.sourceLocator !== null && !text(v.sourceLocator) || v.scale !== null && !(typeof v.scale === 'number' && Number.isInteger(v.scale) && v.scale >= 0 && v.scale <= 6) ||
    v.maxTestAgeDays !== null && !integer(v.maxTestAgeDays, 36525) || v.maxLossPercent !== null && (!bpNumber(v.maxLossPercent) || Number(v.maxLossPercent) > 100)) return false
  return [v.jobTypes, v.areas].every(x => x === null || Array.isArray(x) && x.length <= (x === v.jobTypes ? 32 : 128) && x.every(text) && new Set(x).size === x.length)
}
export const completeCalculation = (v: CalculationPolicy) => Object.values(v).every(x => x !== null) && !!v.jobTypes?.length && !!v.areas?.length
