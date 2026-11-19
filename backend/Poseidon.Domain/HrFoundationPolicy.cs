using System.Text.RegularExpressions;

namespace Poseidon.Domain;

// PLAN-1511-5201 / 52.01 Rev6 foundation. Closed vocabularies and pure rules only.
// No wage, payroll, bank, bonus or monetary personnel field exists in this model (52.01 paragraph:7, paragraph:460).
// Every period is half-open [from, to); an open end (null) means "until further notice" (52.01 paragraph:49).
public readonly record struct HrPeriod(DateTimeOffset From, DateTimeOffset? To)
{
    public bool IsValid => To is null || To.Value > From;
    public bool Overlaps(HrPeriod other) => (other.To is null || From < other.To.Value) && (To is null || other.From < To.Value);
    // True when [other] lies completely inside this period.
    public bool Covers(HrPeriod other) => From <= other.From && (To is null || other.To is not null && other.To.Value <= To.Value);
    public bool Contains(DateTimeOffset instant) => From <= instant && (To is null || instant < To.Value);
}

public static partial class HrSourcePolicy
{
    // Field/source revision provenance. "Synthetic" is fixture-only and never "verified".
    public static readonly string[] SourceKinds = ["UserDeclared", "RegistryExtract", "Document", "Import", "Synthetic"];
    public static bool ValidSourceKind(string? kind, bool allowSynthetic = false) =>
        kind is not null && SourceKinds.Contains(kind, StringComparer.Ordinal) && (allowSynthetic || kind != "Synthetic");
    public static bool ValidSourceRef(string? value) => value is null || value.Length is > 0 and <= 200 && !value.Any(char.IsControl);
    public const int ReasonMaxLength = 500;
    public static bool ValidReason(string? value, bool required) => value is null ? !required : value.Trim().Length is > 0 and <= ReasonMaxLength && !value.Any(char.IsControl);
    // 52.01 paragraph:50: a correction of an already recorded value produces a new revision with its reason. A first
    // declaration of a previously unknown value is not a correction. Reasons are free business text: no personal data.
    public static bool IsCorrection(string? before, string? after) => before is not null && !string.Equals(before, after, StringComparison.Ordinal);
    public static bool ValidText(string? value, int max) => value is not null && value.Trim().Length is > 0 && value.Trim().Length <= max && !value.Any(char.IsControl);
}

// 52.01 paragraph:50: every state decision on an HR record keeps who/when/why as an append-only state-change row.
public static class HrStateChangePolicy
{
    public static readonly string[] SubjectKinds = ["LegalEntityIdentifier", "LegalEntityRole", "Employment", "PlannedAssignment"];
    public static readonly string[] States = ["Current", "Retired", "Recorded", "Ended", "Cancelled", "Draft", "Active", "Planned", "Approved"];
}

public static partial class HrLegalEntityPolicy
{
    public static readonly string[] Statuses = ["Draft", "Active", "Inactive"];
    public static readonly string[] Locales = ["tr-TR", "en-GB"];
    public static readonly string[] CoreFields = ["LegalName", "CountryCode", "HrTimeZone", "BaseLocale", "EffectiveFrom", "EffectiveTo"];

    [GeneratedRegex("^[A-Z]{2}$", RegexOptions.CultureInvariant)] private static partial Regex Country();
    [GeneratedRegex("^(UTC|[A-Za-z][A-Za-z_]*(/[A-Za-z0-9_+\\-]+){1,2})$", RegexOptions.CultureInvariant)] private static partial Regex Zone();

    // 52.01 paragraph:95 IANA zone; Windows-only identifiers are rejected, no default country/calendar is chosen (paragraph:52).
    public static bool ValidTimeZone(string? id) => id is { Length: > 0 and <= 64 } && Zone().IsMatch(id) &&
        TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zone) && (zone.HasIanaId || id == "UTC");

    public static Dictionary<string, string[]> Validate(string? legalName, string? countryCode, string? hrTimeZone, string? baseLocale,
        DateTimeOffset? effectiveFrom, DateTimeOffset? effectiveTo)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        // Unknown (null) remains unknown; a draft may be incomplete (POL-DEFAULT-01).
        if (legalName is not null && !HrSourcePolicy.ValidText(legalName, 300)) errors["legalName"] = ["text_1_300_required"];
        if (countryCode is not null && !Country().IsMatch(countryCode)) errors["countryCode"] = ["iso_3166_alpha2_required"];
        if (hrTimeZone is not null && !ValidTimeZone(hrTimeZone)) errors["hrTimeZone"] = ["iana_zone_required"];
        if (baseLocale is not null && !Locales.Contains(baseLocale, StringComparer.Ordinal)) errors["baseLocale"] = ["tr-TR_or_en-GB_required"];
        if (effectiveTo is not null && effectiveFrom is null) errors["effectiveFrom"] = ["required_with_effective_to"];
        if (effectiveFrom is not null && effectiveTo is not null && effectiveTo <= effectiveFrom) errors["effectiveTo"] = ["must_follow_effective_from"];
        return errors;
    }

    // Submission/activation blocks on every missing required value instead of inventing one (52.01 paragraph:52, HR_REFERENCE_INCOMPLETE).
    public static string[] MissingForActivation(string? legalName, string? countryCode, string? hrTimeZone, string? baseLocale, DateTimeOffset? effectiveFrom) =>
        new (string Field, bool Missing)[] {
            ("legalName", string.IsNullOrWhiteSpace(legalName)), ("countryCode", countryCode is null),
            ("hrTimeZone", hrTimeZone is null), ("baseLocale", baseLocale is null), ("effectiveFrom", effectiveFrom is null)
        }.Where(x => x.Missing).Select(x => x.Field).ToArray();

    // 52.01 paragraph:51: a changed legal-entity period must still contain every active employment it carries.
    public static bool CoversAll(HrPeriod? entity, IEnumerable<HrPeriod> activeEmployments) =>
        activeEmployments.All(e => entity is { } period && period.Covers(e));

    // Deactivation never deletes employments or assignments (52.01 paragraph:119); reactivation is an explicit command.
    public static bool CanTransition(string from, string to) => (from, to) is ("Draft", "Active") or ("Active", "Inactive") or ("Inactive", "Active");
}

public static partial class HrIdentifierPolicy
{
    public static readonly string[] Types = ["TradeRegistry", "TaxId", "Mersis", "ImoCompanyNumber", "Lei", "Other"];
    [GeneratedRegex("^[A-Z0-9][A-Z0-9._:\\-]{0,99}$", RegexOptions.CultureInvariant)] private static partial Regex Issuer();
    [GeneratedRegex("^[A-Z0-9./\\-]{1,128}$", RegexOptions.CultureInvariant)] private static partial Regex Value();
    [GeneratedRegex("^[0-9]{7}$", RegexOptions.CultureInvariant)] private static partial Regex Imo();
    [GeneratedRegex("^[A-Z0-9]{20}$", RegexOptions.CultureInvariant)] private static partial Regex Lei();

    public static string Normalize(string value) => new string(value.Where(c => !char.IsWhiteSpace(c)).ToArray()).ToUpperInvariant();
    public static string NormalizeIssuer(string issuer) => issuer.Trim().ToUpperInvariant();

    public static Dictionary<string, string[]> Validate(string? type, string? issuer, string? value)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (type is null || !Types.Contains(type, StringComparer.Ordinal)) errors["type"] = ["closed_identifier_type_required"];
        if (issuer is null || !Issuer().IsMatch(NormalizeIssuer(issuer))) errors["issuer"] = ["issuer_code_required"];
        if (value is null || value.Any(char.IsControl) || !Value().IsMatch(Normalize(value))) errors["value"] = ["restricted_value_1_128_required"];
        else if (type == "ImoCompanyNumber" && !Imo().IsMatch(Normalize(value))) errors["value"] = ["imo_company_number_7_digits"];
        else if (type == "Lei" && !Lei().IsMatch(Normalize(value))) errors["value"] = ["lei_20_characters"];
        return errors;
    }

    // Responses carry only this mask by default (52.01 paragraph:93); the full value is returned solely to holders of
    // the specific hr.legal_entity.identifier.read permission.
    public static string Mask(string normalized) => normalized.Length <= 4 ? new string('*', normalized.Length) : new string('*', normalized.Length - 4) + normalized[^4..];
}

public static class HrRolePolicy
{
    // Typed legal-entity relations. Vessel relations are consumed by 59.01 (DEV-020); here they are declared, unverified facts.
    public static readonly string[] Roles = ["RegisteredOwner", "Operator", "TechnicalManager", "CrewManager", "CommercialManager", "DocCompany", "Employer"];
    // Recorded relations count (current or ended). Cancelled marks a mistaken record: it is retained but never counts.
    public static readonly string[] States = ["Recorded", "Cancelled"];
    // Cancelling a recorded relation is an independent act with a reason, like the other HR state decisions.
    public static string? CancelRejection(string state, string creator, string canceller) =>
        state != "Recorded" ? "invalid_transition" : creator == canceller ? "separation_of_duties" : null;
    public static bool RequiresVessel(string role) => role != "Employer";
    // 59.01 paragraph:273-279/285-286: co-ownership is possible; at most one primary operator and one holder per management role/vessel/period.
    public static bool ExclusivePerVessel(string role) => role is "Operator" or "TechnicalManager" or "CrewManager" or "CommercialManager" or "DocCompany";

    public sealed record Existing(Guid Id, Guid LegalEntityId, string Role, Guid? VesselId, HrPeriod Period);

    public static Dictionary<string, string[]> Validate(string? role, Guid? vesselId, DateTimeOffset? from, DateTimeOffset? to)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (role is null || !Roles.Contains(role, StringComparer.Ordinal)) errors["role"] = ["closed_role_required"];
        else if (RequiresVessel(role) && (vesselId is null || vesselId == Guid.Empty)) errors["vesselId"] = ["required_for_vessel_role"];
        else if (!RequiresVessel(role) && vesselId is not null) errors["vesselId"] = ["not_allowed_for_employer_role"];
        if (from is null) errors["effectiveFrom"] = ["required"];
        else if (to is not null && to <= from) errors["effectiveTo"] = ["must_follow_effective_from"];
        return errors;
    }

    public static Existing? Conflict(IEnumerable<Existing> current, Guid legalEntityId, string role, Guid? vesselId, HrPeriod period) =>
        current.FirstOrDefault(x => x.Role == role && x.Period.Overlaps(period) && (
            role == "Employer" ? x.LegalEntityId == legalEntityId :
            ExclusivePerVessel(role) ? x.VesselId == vesselId : x.VesselId == vesselId && x.LegalEntityId == legalEntityId));
}

public static class HrPersonPolicy
{
    // 52.01 paragraph:245-262. Unknown is retained; it is never silently converted to Shore/Seafarer.
    public static readonly string[] Types = ["Unknown", "Seafarer", "Shore", "Contractor"];
    public static readonly string[] States = ["Draft", "Verified"];
    public static bool ValidType(string? type) => type is not null && Types.Contains(type, StringComparer.Ordinal);
    // Verification is an independent act: the actor who last declared a core field cannot verify it.
    public static string? VerifyRejection(string state, string personType, string? lastCoreFieldActor, string verifier) =>
        state != "Draft" ? "invalid_transition" : personType == "Unknown" ? "hr_reference_incomplete" :
        lastCoreFieldActor is null || lastCoreFieldActor == verifier ? "separation_of_duties" : null;
}

public static class HrEmploymentPolicy
{
    public static readonly string[] Types = ["Employee", "Contractor", "Agency"];
    public static readonly string[] States = ["Draft", "Active", "Ended", "Cancelled"];

    public static Dictionary<string, string[]> Validate(string? employeeNo, string? type, DateTimeOffset? start, DateTimeOffset? end)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (!HrSourcePolicy.ValidText(employeeNo, 64)) errors["employeeNo"] = ["text_1_64_required"];
        if (type is null || !Types.Contains(type, StringComparer.Ordinal)) errors["employmentType"] = ["closed_type_required"];
        if (start is null) errors["startAt"] = ["required"];
        else if (end is not null && end <= start) errors["endAt"] = ["must_follow_start"];
        return errors;
    }

    public static bool CanTransition(string from, string to) => (from, to) is ("Draft", "Active") or ("Draft", "Cancelled") or ("Active", "Ended");

    // 52.01 paragraph:277/327: verified person, active legal employer and independent approval.
    public static string? ActivationRejection(string personState, string legalEntityStatus, HrPeriod? legalEntityPeriod,
        IEnumerable<HrPeriod> employerRoles, HrPeriod employment, string creator, string approver)
    {
        if (creator == approver) return "separation_of_duties";
        if (personState != "Verified") return "hr_person_not_verified";
        if (legalEntityStatus != "Active" || legalEntityPeriod is not { } entity || !entity.Covers(employment)) return "hr_legal_entity_inactive";
        if (!employerRoles.Any(r => r.Covers(employment))) return "hr_employer_role_missing";
        return null;
    }
}

public static class HrAssignmentPolicy
{
    public static readonly string[] Kinds = ["Vessel", "Shore"];
    public static readonly string[] States = ["Planned", "Approved", "Cancelled"];

    public static Dictionary<string, string[]> Validate(string? kind, Guid? vesselId, Guid? positionId, DateTimeOffset? from, DateTimeOffset? to, string? zone)
    {
        var errors = new Dictionary<string, string[]>(StringComparer.Ordinal);
        if (kind is null || !Kinds.Contains(kind, StringComparer.Ordinal)) errors["kind"] = ["closed_kind_required"];
        else if (kind == "Vessel" && (vesselId is null || vesselId == Guid.Empty)) errors["vesselId"] = ["required_for_vessel_assignment"];
        else if (kind == "Shore" && (vesselId is not null || positionId is not null)) errors["vesselId"] = ["not_allowed_for_shore_assignment"];
        if (positionId == Guid.Empty) errors["positionId"] = ["invalid"];
        // A planned period is always bounded (52.01 paragraph:417-418): to > from and the source zone is retained.
        if (from is null) errors["plannedFrom"] = ["required"];
        if (to is null) errors["plannedTo"] = ["required"];
        else if (from is not null && to <= from) errors["plannedTo"] = ["must_follow_planned_from"];
        if (zone is null || !HrLegalEntityPolicy.ValidTimeZone(zone)) errors["plannedZoneId"] = ["iana_zone_required"];
        return errors;
    }

    public static bool CanTransition(string from, string to) => (from, to) is ("Planned", "Approved") or ("Planned", "Cancelled") or ("Approved", "Cancelled");

    // 52.01 paragraph:404-407: plan inside employment, operator path for vessel duty, the planner cannot approve,
    // approved occupancy for the same person never overlaps. Plan alternatives (Planned) may overlap.
    public static string? ApprovalRejection(string employmentState, HrPeriod employment, HrPeriod plan, bool vesselDuty,
        IEnumerable<HrPeriod> operatorRoles, IEnumerable<HrPeriod> approvedForPerson, string planner, string approver)
    {
        if (planner == approver) return "separation_of_duties";
        if (employmentState != "Active") return "hr_employment_not_active";
        if (!employment.Covers(plan)) return "hr_period_outside_employment";
        if (vesselDuty && !operatorRoles.Any(r => r.Covers(plan))) return "hr_operator_path_missing";
        if (approvedForPerson.Any(p => p.Overlaps(plan))) return "hr_assignment_overlap";
        return null;
    }
}

// Actual (fiilî) facts are separate from the plan. Planned dates never fill actual values (52.01 paragraph:405, 561).
public static class HrActualTimeline
{
    public static readonly string[] Kinds = ["CheckIn", "CheckOut"];
    public static readonly string[] Origins = ["Self", "SupervisorReported"];
    public static readonly TimeSpan FutureTolerance = TimeSpan.FromMinutes(2);

    public sealed record Fact(Guid Id, string Kind, DateTimeOffset OccurredAt, string Quality, string Disposition);
    public sealed record Session(DateTimeOffset ActualFrom, DateTimeOffset? ActualTo);
    public sealed record Projection(string Status, DateTimeOffset? ActualFrom, DateTimeOffset? ActualTo, Session[] Sessions, int PendingReconciliation);

    // Returns (quality, disposition). Every fact is retained; doubtful facts are PendingReconciliation, never dropped.
    public static (string Quality, string Disposition) Classify(IEnumerable<Fact> existing, string kind, DateTimeOffset occurredAt, bool planApproved, DateTimeOffset now)
    {
        if (occurredAt > now + FutureTolerance) return ("Suspect", "PendingReconciliation");
        if (!planApproved) return ("Valid", "PendingReconciliation");
        var accepted = existing.Where(x => x.Disposition == "Accepted").OrderBy(x => x.OccurredAt).ToArray();
        if (accepted.Length > 0 && occurredAt <= accepted[^1].OccurredAt) return ("Valid", "PendingReconciliation");
        var open = accepted.Length > 0 && accepted[^1].Kind == "CheckIn";
        return kind switch
        {
            "CheckIn" when !open => ("Valid", "Accepted"),
            "CheckOut" when open => ("Valid", "Accepted"),
            _ => ("Valid", "PendingReconciliation")
        };
    }

    public static Projection Derive(IEnumerable<Fact> facts)
    {
        var all = facts.ToArray();
        var sessions = new List<Session>();
        DateTimeOffset? start = null;
        foreach (var fact in all.Where(x => x.Disposition == "Accepted").OrderBy(x => x.OccurredAt))
        {
            if (fact.Kind == "CheckIn") start = fact.OccurredAt;
            else if (start is { } from) { sessions.Add(new(from, fact.OccurredAt)); start = null; }
        }
        if (start is { } openFrom) sessions.Add(new(openFrom, null));
        var pending = all.Count(x => x.Disposition == "PendingReconciliation");
        var status = sessions.Count == 0 ? "NotStarted" : sessions[^1].ActualTo is null ? "InProgress" : "Completed";
        return new(status, sessions.FirstOrDefault()?.ActualFrom, sessions.Count > 0 ? sessions[^1].ActualTo : null, [.. sessions], pending);
    }
}
