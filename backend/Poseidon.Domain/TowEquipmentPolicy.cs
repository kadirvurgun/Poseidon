using System.Globalization;
using System.Text.Json.Serialization;

namespace Poseidon.Domain;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record TowEquipmentInputs(string Kind, string? Label, string? SwlTonnes, DateOnly? LastTestDate, DateOnly? NextTestDue,
    string Condition, bool? Critical, Guid? AssetId, Guid? CertificateId, string? Notes);
// Evidence the server resolves for one item at assessment time. null = not resolvable (UNKNOWN).
public sealed record TowEquipmentEvidence(string? Verification, string? PmsDue, bool CertificateLinked, DateOnly? CertificateExpiry);

// DEV-085 / T-TUG-006 (18.01 Backlog row 86: "SWL/test/condition/PMS/certificate evidence; NOT_READY on
// critical failure; evidence/verification trace"). 60.02 Rev2 paragraph 520 (role WINCH|LINE|HOOK|FENDER|FIFI|
// COMMS; a role not required by rule comes from source NOT_REQUIRED; a missing role is UNKNOWN, not N/A),
// paragraph 190 (open critical defect / overdue source rule blocks; a local checklist never releases PMS),
// paragraph 196 (towline/winch/ER/FiFi observations are typed source evidence). The Chief Engineer
// verification is a separate human record bound to one item revision; nothing here verifies equipment.
public static class TowEquipmentPolicy
{
    public const string SourceLocator = "18.01/sheet:Backlog/row:86;60.02/paragraph:190,196,520";
    public const string Winch = "Winch", TowHook = "TowHook", Towline = "Towline", EmergencyRelease = "EmergencyRelease", FiFi = "FiFi";
    public static readonly string[] Kinds = [Winch, TowHook, Towline, EmergencyRelease, FiFi];
    // Load-bearing kinds need a declared SWL and a linked test certificate.
    public static readonly string[] LoadBearing = [Winch, TowHook, Towline, EmergencyRelease];
    public const string Serviceable = "Serviceable", Degraded = "Degraded", Failed = "Failed", ConditionUnknown = "Unknown";
    public static readonly string[] Conditions = [Serviceable, Degraded, Failed, ConditionUnknown];
    public const string Verified = "VERIFIED", NotVerified = "NOT_VERIFIED";
    public static readonly string[] Verifications = [Verified, NotVerified];
    public const int TextMax = 200, NotesMax = 2000;

    public static string[] Invalid(TowEquipmentInputs x)
    {
        var errors = new List<string>();
        if (!Kinds.Contains(x.Kind, StringComparer.Ordinal)) errors.Add("kind");
        if (!Conditions.Contains(x.Condition, StringComparer.Ordinal)) errors.Add("condition");
        if (x.Label != null && (x.Label.Length is 0 or > TextMax || x.Label.Trim() != x.Label || x.Label.Any(char.IsControl))) errors.Add("label");
        if (x.Notes != null && (x.Notes.Length is 0 or > NotesMax || x.Notes.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))) errors.Add("notes");
        if (x.SwlTonnes != null && !TrySwl(x.SwlTonnes, out _)) errors.Add("swl_tonnes");
        if (x.LastTestDate != null && x.NextTestDue != null && x.NextTestDue < x.LastTestDate) errors.Add("next_test_due");
        if (x.AssetId == Guid.Empty) errors.Add("asset_id");
        if (x.CertificateId == Guid.Empty) errors.Add("certificate_id");
        return errors.ToArray();
    }

    public static bool TrySwl(string? text, out decimal? value)
    {
        value = null;
        if (text == null) return true;
        if (text.Length > 12 || !System.Text.RegularExpressions.Regex.IsMatch(text, @"\A[0-9]{1,6}(?:\.[0-9]{1,2})?\z") ||
            !decimal.TryParse(text, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var parsed) || parsed <= 0) return false;
        value = parsed; return true;
    }

    // One item -> one tow_equipment check (worst finding wins). Unknown criticality is treated as critical
    // for a failure (safe side) but never turns anything positive.
    public static TugReadinessCheck Assess(Guid id, long revision, TowEquipmentInputs x, TowEquipmentEvidence e, DateOnly asOf)
    {
        var critical = x.Critical != false;
        TugReadinessCheck C(string result, string reason) => new("tow_equipment", result, x.Kind + ":" + reason, critical, id, revision);
        const string F = TugReadinessEngine.Fail, U = TugReadinessEngine.Unknown, K = TugReadinessEngine.Condition, P = TugReadinessEngine.Pass;
        var findings = new List<TugReadinessCheck>();
        if (x.Condition == Failed) findings.Add(critical ? C(F, "CriticalEquipmentFailed") : C(K, "NonCriticalEquipmentFailed"));
        else if (x.Condition == Degraded) findings.Add(critical ? C(U, "CriticalEquipmentDegradedReviewRequired") : C(K, "NonCriticalEquipmentDegraded"));
        else if (x.Condition == ConditionUnknown) findings.Add(C(U, "ConditionUnknown"));
        if (x.Critical == null) findings.Add(C(U, "CriticalityNotAssessed"));
        if (LoadBearing.Contains(x.Kind) && x.SwlTonnes == null) findings.Add(C(U, "SwlMissing"));
        if (x.LastTestDate == null || x.NextTestDue == null) findings.Add(C(U, "TestDateMissing"));
        else if (x.NextTestDue < asOf) findings.Add(critical ? C(F, "TestOverdue") : C(K, "NonCriticalTestOverdue"));
        if (x.AssetId == null) findings.Add(C(U, "PmsLinkMissing"));
        else if (e.PmsDue == null) findings.Add(C(U, "PmsStateUnavailable"));
        else if (e.PmsDue == "Due") findings.Add(critical ? C(F, "LinkedPmsDue") : C(K, "NonCriticalLinkedPmsDue"));
        if (LoadBearing.Contains(x.Kind)) {
            if (x.CertificateId == null) findings.Add(C(U, "CertificateLinkMissing"));
            else if (!e.CertificateLinked) findings.Add(C(U, "CertificateUnavailable"));
            else if (e.CertificateExpiry == null) findings.Add(C(U, "CertificateExpiryUnknown"));
            else if (e.CertificateExpiry < asOf) findings.Add(C(F, "CertificateExpired"));
        }
        // Positive readiness needs the Chief Engineer's verification of exactly this revision.
        if (e.Verification == null) findings.Add(C(U, "ChiefEngineerVerificationMissing"));
        else if (e.Verification == NotVerified) findings.Add(C(U, "ChiefEngineerVerificationRefused"));
        var worst = findings.FirstOrDefault(f => f.Result == F) ?? findings.FirstOrDefault(f => f.Result == U) ?? findings.FirstOrDefault(f => f.Result == K);
        return worst ?? C(P, "DeclaredServiceableAndVerified");
    }

    // A required kind with no record is UNKNOWN (60.02 paragraph 520); nothing is marked N/A by the server.
    public static TugReadinessCheck[] Missing(IEnumerable<string> recordedKinds) =>
        Kinds.Except(recordedKinds, StringComparer.Ordinal).Select(k => new TugReadinessCheck("tow_equipment", TugReadinessEngine.Unknown, k + ":EquipmentRoleMissing")).ToArray();
}
