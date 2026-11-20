using System.Globalization;
using System.Text;

namespace Poseidon.Api;

// DEV-022 / QA follow-up: global search matches an authorized destination by its route key or by
// the TR/EN display labels the UI shows for it (search result label and sidebar menu label).
// Source of truth is the frontend catalog (frontend/src/i18n/messages/{tr,en}.json resolved through
// WorkspaceSearch.tsx, workspace-lens-api.ts and EnterpriseChrome.tsx); WorkspaceDestinationLabelSyncTests
// fails when this map drifts. Labels are matched only for destinations already in the actor's scope.
internal static class WorkspaceDestinationLabels
{
    internal static readonly IReadOnlyDictionary<string, string[]> Labels = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        ["today"] = ["Bugün", "Today"],
        ["companies"] = ["Şirketler", "Companies"],
        ["fleet"] = ["Filo", "Fleet"],
        ["audit"] = ["İşlem geçmişi", "Action history"],
        ["audit-ready"] = ["Denetime hazır iz", "Audit-ready trace"],
        ["system-health"] = ["Sistem sağlığı", "System health"],
        ["notification-escalation"] = ["Bildirimler", "Notifications"],
        ["offline-conflict-center"] = ["Önemli çakışma merkezi", "Material conflict center"],
        ["defects"] = ["Arızalar ve önlemler", "Defects and safeguards"],
        ["work-order-center"] = ["İş emirleri", "Work orders"],
        ["critical-spares"] = ["Kritik yedekler ve talepler", "Critical spares and requisitions"],
        ["pms-command-center"] = ["PMS komuta merkezi", "PMS command center"],
        ["departure-signoff"] = ["Kalkış hazırlığı ve Kaptan kararı", "Departure readiness and Master sign-off"],
        ["service-engineer-report"] = ["Servis ve mühendis raporları", "Service and engineer reports"],
        ["voice-event-draft"] = ["Sesli olay taslağı", "Voice event draft"],
        ["document-compare"] = ["Doküman revizyon karşılaştırma", "Document revision compare"],
        ["operational-downtime"] = ["Operasyonel duruşlar", "Operational downtime"],
        ["risk-assessment"] = ["Risk değerlendirmesi", "Risk assessment"],
        ["risk-template-library"] = ["Risk şablon kütüphanesi", "Risk template library"],
        ["high-risk-work"] = ["Yüksek riskli işler", "High-risk work"],
        ["safety-events"] = ["Olaylar ve ramak kala", "Safety events & near misses"],
        ["certificate-command-center"] = ["Sertifika merkezi", "Certificate center"],
        ["renewal-workflow"] = ["Sertifika yenileme", "Certificate renewal"],
        ["pre-survey-checklist"] = ["Sörvey öncesi kontrol listesi", "Pre-survey checklist"],
        ["survey-planner"] = ["Sörvey planlayıcı", "Survey planner"],
        ["tug-command"] = ["Römorkör operasyonları", "Tug operations", "Römorkör merkezi", "Tug command"],
        ["tug-job-board"] = ["Römorkör iş panosu", "Tug job board", "İş panosu", "Job board"],
        ["tug-job-intake"] = ["Römorkör iş kaydı", "Tug job intake", "İş talebi", "Job intake"],
        ["tug-assignment"] = ["Römorkör atama", "Tug assignment"],
        ["tug-readiness"] = ["Römorkör hazırlık kapısı", "Tug readiness gate"],
        ["tug-equipment-readiness"] = ["Çekme ekipmanı hazırlığı", "Tow equipment readiness"],
        ["tug-crew-readiness"] = ["Römorkör mürettebat yetkinliği", "Tug crew competence"],
        ["towage-plan"] = ["Römorkaj operasyon planı", "Towage plan"],
        ["towage-risk-briefing"] = ["Römorkaj risk ve brifing", "Towage risk and briefing"],
        ["fuel-bunker-log"] = ["Yakıt, ikmal ve ROB", "Fuel, bunker and ROB"],
        ["port-activity-sof"] = ["Liman faaliyeti ve SOF", "Port activity and SOF"],
        ["noon-report"] = ["Öğle raporu", "Noon report"],
        ["deck-daily-log"] = ["Güverte günlük kaydı", "Deck daily log"],
        ["engine-daily-log"] = ["Makine günlük kaydı", "Engine daily log"],
        ["sources"] = ["Kaynak taslakları", "Source drafts", "Kaynaklar", "Sources"],
        ["requirements"] = ["Gereksinim taslakları", "Requirement drafts", "Gereksinimler", "Requirements"],
    };

    private static readonly IReadOnlyDictionary<string, string[]> Folded =
        Labels.ToDictionary(p => p.Key, p => p.Value.Select(Fold).ToArray(), StringComparer.Ordinal);

    // Culture-independent Turkish-aware folding (65.03 paragraph 226): İ/I/ı/i -> i, lower-case,
    // NFD and combining marks removed. Same rule as the sidebar filter (foldMenuText).
    internal static string Fold(string value)
    {
        var builder = new StringBuilder(value.Length);
        foreach (var c in value)
            builder.Append(c is 'İ' or 'I' or 'ı' ? 'i' : char.ToLowerInvariant(c));
        var decomposed = builder.ToString().Normalize(NormalizationForm.FormD);
        builder.Clear();
        foreach (var c in decomposed)
            if (CharUnicodeInfo.GetUnicodeCategory(c) is not (UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark))
                builder.Append(c);
        return builder.ToString().Normalize(NormalizationForm.FormC).Trim();
    }

    // `needle` is already folded. Keys are ASCII route keys; folding them is equivalent to an
    // ordinal case-insensitive compare plus Turkish dotted/dotless I tolerance.
    internal static bool Matches(string key, string needle) =>
        needle.Length > 0 && (Fold(key).Contains(needle, StringComparison.Ordinal) ||
            Folded.TryGetValue(key, out var labels) && labels.Any(label => label.Contains(needle, StringComparison.Ordinal)));
}
