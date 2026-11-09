import { sourceText, localizedTuple } from "../i18n/messages";
import { localizedText } from "../i18n/messages";
const reasons: Record<string, [
    string,
    string
]> = {
    EquipmentRevisionChanged: [sourceText("en", "pairs.m_6b9a8e6e7e9dbecb"), sourceText("tr", "pairs.m_6b9a8e6e7e9dbecb")],
    MaintenanceJobUnavailable: [sourceText("en", "pairs.m_5ebcef767c33418a"), sourceText("tr", "pairs.m_5ebcef767c33418a")],
    MaintenanceOwnerUnavailable: [sourceText("en", "pairs.m_221fc8d62349cb00"), sourceText("tr", "pairs.m_221fc8d62349cb00")],
    ObservationAssessmentUnavailable: [sourceText("en", "pairs.m_260125a865560131"), sourceText("tr", "pairs.m_260125a865560131")],
    ConfiguredDeclaredObservation: [sourceText("en", "pairs.m_9e02bcae4ad01d44"), sourceText("tr", "pairs.m_9e02bcae4ad01d44")],
    ObservationMissing: [sourceText("en", "pairs.m_a9da6c9d7483d126"), sourceText("tr", "pairs.m_a9da6c9d7483d126")],
    ObservationRevoked: [sourceText("en", "pairs.m_02216d27b8d2833a"), sourceText("tr", "pairs.m_02216d27b8d2833a")],
    ObservationPolicyUnavailable: [sourceText("en", "pairs.m_b37b42f8a48305ef"), sourceText("tr", "pairs.m_b37b42f8a48305ef")],
    ObservationPolicyChanged: [sourceText("en", "pairs.m_8abc1bae0426026d"), sourceText("tr", "pairs.m_8abc1bae0426026d")],
    ObservationUnitOrKindMismatch: [sourceText("en", "pairs.m_ce44df62a1a585fd"), sourceText("tr", "pairs.m_ce44df62a1a585fd")],
    ObservationFuture: [sourceText("en", "pairs.m_f500510ad7ca5c2e"), sourceText("tr", "pairs.m_f500510ad7ca5c2e")],
    ObservationStale: [sourceText("en", "pairs.m_a9e3427c3e277cfb"), sourceText("tr", "pairs.m_a9e3427c3e277cfb")],
    ObservationPrecisionUnsupported: [sourceText("en", "pairs.m_bb9211adccb7ebf0"), sourceText("tr", "pairs.m_bb9211adccb7ebf0")],
    ObservationConditionUnknown: [sourceText("en", "pairs.m_95e868832206a40d"), sourceText("tr", "pairs.m_95e868832206a40d")],
    ObservationScopeMismatch: [sourceText("en", "pairs.m_08db9e29023605fd"), sourceText("tr", "pairs.m_08db9e29023605fd")],
    MissingProvenance: [sourceText("en", "pairs.m_99d34a716270a9af"), sourceText("tr", "pairs.m_99d34a716270a9af")],
    MissingTimeZone: [sourceText("en", "pairs.m_0f8992ad858645e2"), sourceText("tr", "pairs.m_0f8992ad858645e2")],
    MissingCalendarInputs: [sourceText("en", "pairs.m_280bef79a8b8ff4d"), sourceText("tr", "pairs.m_280bef79a8b8ff4d")],
    UnsupportedCalendarInterval: [sourceText("en", "pairs.m_973190daad6767ed"), sourceText("tr", "pairs.m_973190daad6767ed")],
    FutureBaseline: [sourceText("en", "pairs.m_9745008dc19f0e82"), sourceText("tr", "pairs.m_9745008dc19f0e82")],
    CalendarBoundary: [sourceText("en", "pairs.m_2ef1832e50b29c3b"), sourceText("tr", "pairs.m_2ef1832e50b29c3b")],
    MissingMeterAdapterOrReading: [sourceText("en", "pairs.m_24f69688f5e25a1d"), sourceText("tr", "pairs.m_24f69688f5e25a1d")],
    FutureObservation: [sourceText("en", "pairs.m_9478ca5842569acd"), sourceText("tr", "pairs.m_9478ca5842569acd")],
    ReadingFreshnessPolicyUnresolved: [sourceText("en", "pairs.m_f6ae3b6ee0d91c55"), sourceText("tr", "pairs.m_f6ae3b6ee0d91c55")],
    MissingOrResetMeterBaseline: [sourceText("en", "pairs.m_144ef1c79e4e1968"), sourceText("tr", "pairs.m_144ef1c79e4e1968")],
    DeclaredMeterBoundary: [sourceText("en", "pairs.m_b90f46969a9e0271"), sourceText("tr", "pairs.m_b90f46969a9e0271")],
    MeterRangeExceeded: [sourceText("en", "pairs.m_3d0cde24d8e53e76"), sourceText("tr", "pairs.m_3d0cde24d8e53e76")],
    MeterPrecisionExceeded: [sourceText("en", "pairs.m_74122b012de518c6"), sourceText("tr", "pairs.m_74122b012de518c6")],
    MissingConditionAdapterOrObservation: [sourceText("en", "pairs.m_681af67a3f4b20f9"), sourceText("tr", "pairs.m_681af67a3f4b20f9")],
    ConditionFreshnessPolicyUnresolved: [sourceText("en", "pairs.m_54e4dbfd1a6bd622"), sourceText("tr", "pairs.m_54e4dbfd1a6bd622")],
    DeclaredConditionObservation: [sourceText("en", "pairs.m_b466d5918f65ef0c"), sourceText("tr", "pairs.m_b466d5918f65ef0c")],
    MissingOrUnsupportedTrigger: [sourceText("en", "pairs.m_e7d95af1fca7ea21"), sourceText("tr", "pairs.m_e7d95af1fca7ea21")],
    IncompletePlanContext: [sourceText("en", "pairs.m_f0a168b27301d2c2"), sourceText("tr", "pairs.m_f0a168b27301d2c2")],
};
export const dueReason = (code: string, en: boolean) => localizedTuple(reasons[code], en, 0) ?? (localizedText(en, "pairs.m_5a882288d4543644"));
export const dueState = (state: string, en: boolean) => state === 'Due' ? (localizedText(en, "pairs.m_48783fc51c2104ed"))
    : state === 'NotDue' ? (localizedText(en, "pairs.m_c2460821157f62f7"))
        : (localizedText(en, "pairs.m_6edb7d9a4aca3fd7"));
