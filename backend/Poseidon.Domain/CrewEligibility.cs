namespace Poseidon.Domain;

// S7 WP-A / DEV-049 + DEV-050 (18.01 Backlog rows 50-51; 53.01 Rev6; 58.04 wave rule
// "UNKNOWN olumluya çevrilmez"). Pure advisory evaluation. Nothing here is a GO,
// an assignment decision, a Safe Manning decision, a sign-off or a verification:
// those are separate attributed human records. UNKNOWN never becomes positive.
public static class CrewEligibilityStates
{
    public const string Eligible = "ELIGIBLE";
    public const string Attention = "ATTENTION";
    public const string NotEligible = "NOT_ELIGIBLE";
    public const string ReviewRequired = "REVIEW_REQUIRED";
    public static readonly string[] All = [Eligible, Attention, NotEligible, ReviewRequired];
}

public static class CrewRequirementTypes
{
    public const string Certificate = "CERTIFICATE", Endorsement = "ENDORSEMENT", Medical = "MEDICAL",
        Training = "TRAINING", Competence = "COMPETENCE", Familiarization = "FAMILIARIZATION";
    public static readonly string[] All = [Certificate, Endorsement, Medical, Training, Competence, Familiarization];
    public static bool CredentialBacked(string type) => type is Certificate or Endorsement or Medical;
}

public static class CrewCompetenceKinds
{
    // Training completion is a fact about attendance; it is never competence (53.01).
    public const string TrainingCompletion = "TRAINING_COMPLETION", Familiarization = "FAMILIARIZATION", CompetenceAssessment = "COMPETENCE_ASSESSMENT";
    public static readonly string[] All = [TrainingCompletion, Familiarization, CompetenceAssessment];
    public static string For(string requirementType) => requirementType switch {
        CrewRequirementTypes.Training => TrainingCompletion,
        CrewRequirementTypes.Familiarization => Familiarization,
        CrewRequirementTypes.Competence => CompetenceAssessment,
        _ => ""
    };
}

public static class CrewWorkRestStates
{
    public const string Unknown = "UNKNOWN", Compliant = "COMPLIANT", NonCompliant = "NON_COMPLIANT";
}

public static class CrewGapStates
{
    public const string Satisfied = "SATISFIED", Gap = "GAP", ReviewRequired = "REVIEW_REQUIRED", Attention = "ATTENTION", NotApplicable = "NOT_APPLICABLE";
}

public sealed record CrewEligibilityWindow(DateOnly From, DateOnly To)
{
    public bool Valid => To >= From && To.DayNumber - From.DayNumber <= 366;
}
public sealed record CrewRequirementFact(Guid Id, string Type, string Reference, bool Mandatory, int? ValidityMonths,
    string? VesselTypeScope, string? CargoScope, Guid? CompetenceLinkId, Guid? FamiliarizationLinkId);
// AssertionState is the latest independent human verification assertion, if any.
// AssertionCurrent: it pins the current credential revision and its appointment is still valid.
public sealed record CrewCredentialFact(Guid Id, string? TypeReference, DateOnly? ExpiryDate, bool? EndorsementRequired,
    DateOnly? EndorsementExpiry, string? AssertionState, bool AssertionCurrent);
// SignOffOutcome is the latest attributed human confirmation (assessor/officer), if any.
public sealed record CrewCompetenceFact(Guid Id, Guid RequirementId, string Kind, DateOnly? CompletedOn, DateOnly? ValidUntil,
    string? SignOffOutcome, bool SignOffCurrent);
public sealed record CrewWorkRestFact(string State, string Reason);
public sealed record CrewEligibilityInputs(Guid AssignmentId, Guid? PositionId, string PresenceState, CrewEligibilityWindow Window,
    DateOnly? AssignmentFrom, DateOnly? AssignmentTo, CrewRequirementFact[] Requirements, CrewCredentialFact[] Credentials,
    CrewCompetenceFact[] Competence, CrewWorkRestFact WorkRest, string? VesselTypeContext = null, string? CargoContext = null);

public sealed record CrewEligibilityReason(string Code, string Dimension, string Severity, Guid? RequirementId = null, Guid? SourceId = null);
public sealed record CrewRequirementGap(Guid RequirementId, string Type, bool Mandatory, string State, string Reason, Guid? SatisfiedBy);
public sealed record CrewEligibilityResult(Guid AssignmentId, Guid? PositionId, string State, CrewEligibilityReason[] Reasons,
    CrewRequirementGap[] Gaps, string[] Qualifications, string Authority = CrewEligibilityPolicy.Authority);

public static class CrewEligibilityPolicy
{
    public const string Authority = "ADVISORY_ONLY_NOT_A_DECISION";
    public static readonly string[] Qualifications = ["ADVISORY_ONLY", "SAFE_MANNING_DECISION_IS_A_HUMAN_RECORD", "UNKNOWN_NEVER_POSITIVE",
        "TRAINING_COMPLETION_IS_NOT_COMPETENCE", "DECLARED_DATA_REQUIRES_HUMAN_VERIFICATION"];
    private static readonly string[] Severity = ["INFO", CrewEligibilityStates.Attention, CrewEligibilityStates.ReviewRequired, CrewEligibilityStates.NotEligible];
    public const string Verified = "Verified";
    public const string Confirmed = "CONFIRMED";

    // A severity order used only for aggregation: NOT_ELIGIBLE (known negative) dominates,
    // then REVIEW_REQUIRED (unknown/unverified), then ATTENTION. ELIGIBLE needs no finding at all.
    public static string Aggregate(IEnumerable<string> severities)
    {
        var rank = severities.Select(s => Array.IndexOf(Severity, s)).DefaultIfEmpty(0).Max();
        return rank switch { 3 => CrewEligibilityStates.NotEligible, 2 => CrewEligibilityStates.ReviewRequired, 1 => CrewEligibilityStates.Attention, _ => CrewEligibilityStates.Eligible };
    }

    public static CrewEligibilityResult Evaluate(CrewEligibilityInputs input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var reasons = new List<CrewEligibilityReason>();
        void Add(string code, string dimension, string severity, Guid? requirement = null, Guid? source = null) => reasons.Add(new(code, dimension, severity, requirement, source));
        if (!input.Window.Valid) Add("EVALUATION_WINDOW_INVALID", "ASSIGNMENT", CrewEligibilityStates.ReviewRequired);
        if (input.PositionId == null) Add("POSITION_MISSING", "ASSIGNMENT", CrewEligibilityStates.ReviewRequired);
        switch (input.PresenceState) {
            case "OnboardDeclaration" or "PlannedDeclaration": break;
            case "OutsidePeriod": Add("ASSIGNMENT_OUTSIDE_PERIOD", "ASSIGNMENT", CrewEligibilityStates.Attention); break;
            default: Add("ASSIGNMENT_PRESENCE_" + Code(input.PresenceState), "ASSIGNMENT", CrewEligibilityStates.ReviewRequired); break;
        }
        if (input.AssignmentFrom == null) Add("ASSIGNMENT_PERIOD_UNKNOWN", "ASSIGNMENT", CrewEligibilityStates.ReviewRequired);
        else if (input.AssignmentFrom > input.Window.From || input.AssignmentTo != null && input.AssignmentTo <= input.Window.To)
            Add("ASSIGNMENT_PERIOD_PARTIAL_WINDOW", "ASSIGNMENT", CrewEligibilityStates.Attention);

        switch (input.WorkRest.State) {
            case CrewWorkRestStates.Compliant: break;
            case CrewWorkRestStates.NonCompliant: Add("WORK_REST_NON_COMPLIANT", "WORK_REST", CrewEligibilityStates.NotEligible); break;
            default: Add(string.IsNullOrEmpty(input.WorkRest.Reason) ? "WORK_REST_SOURCE_UNAVAILABLE" : Code(input.WorkRest.Reason), "WORK_REST", CrewEligibilityStates.ReviewRequired); break;
        }

        var gaps = Gaps(input);
        if (input.Requirements.Length == 0) Add("REQUIREMENT_MATRIX_EMPTY_FOR_POSITION", "COMPETENCE", CrewEligibilityStates.ReviewRequired);
        foreach (var gap in gaps) {
            var dimension = CrewRequirementTypes.CredentialBacked(gap.Type) ? "CREDENTIAL" : gap.Type;
            var severity = gap.State switch {
                CrewGapStates.Gap => gap.Mandatory ? CrewEligibilityStates.NotEligible : CrewEligibilityStates.Attention,
                CrewGapStates.ReviewRequired => gap.Mandatory ? CrewEligibilityStates.ReviewRequired : CrewEligibilityStates.Attention,
                CrewGapStates.Attention => CrewEligibilityStates.Attention,
                _ => null
            };
            if (severity != null) Add(gap.Reason, dimension, severity, gap.RequirementId, gap.SatisfiedBy);
        }
        var state = Aggregate(reasons.Select(r => r.Severity));
        return new(input.AssignmentId, input.PositionId, state, reasons.ToArray(), gaps, Qualifications);
    }

    public static CrewRequirementGap[] Gaps(CrewEligibilityInputs input)
    {
        var ids = input.Requirements.Select(r => r.Id).ToHashSet();
        var result = new List<CrewRequirementGap>();
        foreach (var requirement in input.Requirements.OrderBy(r => r.Type, StringComparer.Ordinal).ThenBy(r => r.Reference, StringComparer.Ordinal).ThenBy(r => r.Id)) {
            CrewRequirementGap Gap(string state, string reason, Guid? by = null) => new(requirement.Id, requirement.Type, requirement.Mandatory, state, reason, by);
            if (!CrewRequirementTypes.All.Contains(requirement.Type)) { result.Add(Gap(CrewGapStates.ReviewRequired, "REQUIREMENT_TYPE_UNRECOGNIZED")); continue; }
            var scope = Scope(requirement.VesselTypeScope, input.VesselTypeContext, "VESSEL_TYPE") ?? Scope(requirement.CargoScope, input.CargoContext, "CARGO");
            if (scope == "NOT_APPLICABLE") { result.Add(Gap(CrewGapStates.NotApplicable, "SCOPE_NOT_APPLICABLE")); continue; }
            if (scope != null) { result.Add(Gap(CrewGapStates.ReviewRequired, scope)); continue; }
            if (requirement.CompetenceLinkId is { } competence && !ids.Contains(competence) ||
                requirement.FamiliarizationLinkId is { } familiarization && !ids.Contains(familiarization)) {
                result.Add(Gap(CrewGapStates.ReviewRequired, "LINKED_REQUIREMENT_UNAVAILABLE")); continue;
            }
            var outcomes = CrewRequirementTypes.CredentialBacked(requirement.Type)
                ? input.Credentials.Where(c => c.TypeReference != null && string.Equals(c.TypeReference.Trim(), requirement.Reference.Trim(), StringComparison.Ordinal))
                    .Select(c => (Outcome: Credential(requirement, c, input.Window), Id: c.Id)).ToArray()
                : input.Competence.Where(c => c.RequirementId == requirement.Id && c.Kind == CrewCompetenceKinds.For(requirement.Type))
                    .Select(c => (Outcome: Competence(requirement, c, input.Window), Id: c.Id)).ToArray();
            if (outcomes.Length == 0) {
                var trainingOnly = requirement.Type == CrewRequirementTypes.Competence &&
                    input.Competence.Any(c => c.RequirementId == requirement.Id && c.Kind == CrewCompetenceKinds.TrainingCompletion);
                result.Add(Gap(CrewGapStates.Gap, CrewRequirementTypes.CredentialBacked(requirement.Type) ? "CREDENTIAL_MISSING"
                    : trainingOnly ? "TRAINING_COMPLETION_IS_NOT_COMPETENCE" : requirement.Type + "_RECORD_MISSING"));
                continue;
            }
            // The best candidate decides; a satisfied one never hides behind an unknown one.
            var best = outcomes.OrderBy(o => Rank(o.Outcome.State)).ThenBy(o => o.Id).First();
            result.Add(Gap(best.Outcome.State, best.Outcome.Reason, best.Id));
        }
        return result.ToArray();
    }

    // DEV-048 register status for one credential on one reference date. Expired or
    // unverified credentials are never shown as positive; only a current human
    // "Verified" assertion on a non-expired credential is VERIFIED_CURRENT.
    public static string CredentialStatus(CrewCredentialFact c, DateOnly reference) =>
        c.ExpiryDate is not { } expiry ? "EXPIRY_UNKNOWN"
        : expiry <= reference ? "EXPIRED"
        : c.AssertionState == null ? "UNVERIFIED"
        : c.AssertionState != Verified ? "VERIFICATION_NEGATIVE"
        : !c.AssertionCurrent ? "VERIFICATION_NOT_CURRENT"
        : "VERIFIED_CURRENT";

    private static int Rank(string state) => state switch {
        CrewGapStates.Satisfied => 0, CrewGapStates.Attention => 1, CrewGapStates.ReviewRequired => 2, _ => 3
    };

    private static string? Scope(string? required, string? context, string label)
    {
        if (string.IsNullOrWhiteSpace(required)) return null;
        if (string.IsNullOrWhiteSpace(context)) return label + "_SCOPE_CONTEXT_UNKNOWN";
        return string.Equals(required.Trim(), context.Trim(), StringComparison.OrdinalIgnoreCase) ? null : "NOT_APPLICABLE";
    }

    private static (string State, string Reason) Credential(CrewRequirementFact requirement, CrewCredentialFact c, CrewEligibilityWindow window)
    {
        if (c.ExpiryDate is not { } expiry) return (CrewGapStates.ReviewRequired, "CREDENTIAL_EXPIRY_UNKNOWN");
        if (expiry < window.From) return (CrewGapStates.Gap, "CREDENTIAL_EXPIRED");
        if (expiry <= window.To) return (CrewGapStates.Gap, "CREDENTIAL_EXPIRES_WITHIN_WINDOW");
        var endorsement = requirement.Type == CrewRequirementTypes.Endorsement ? true : c.EndorsementRequired;
        if (endorsement == null) return (CrewGapStates.ReviewRequired, "ENDORSEMENT_REQUIREMENT_UNKNOWN");
        if (endorsement == true) {
            if (c.EndorsementExpiry is not { } endorsed) return (CrewGapStates.ReviewRequired, "ENDORSEMENT_EXPIRY_UNKNOWN");
            if (endorsed <= window.To) return (CrewGapStates.Gap, "ENDORSEMENT_EXPIRED_OR_EXPIRES_WITHIN_WINDOW");
        }
        return c.AssertionState switch {
            null => (CrewGapStates.ReviewRequired, "CREDENTIAL_UNVERIFIED"),
            Verified when c.AssertionCurrent => (CrewGapStates.Satisfied, "CREDENTIAL_VERIFIED_BY_HUMAN_ASSERTION"),
            Verified => (CrewGapStates.ReviewRequired, "CREDENTIAL_VERIFICATION_NOT_CURRENT"),
            _ => (CrewGapStates.Gap, "CREDENTIAL_VERIFICATION_NEGATIVE")
        };
    }

    private static (string State, string Reason) Competence(CrewRequirementFact requirement, CrewCompetenceFact c, CrewEligibilityWindow window)
    {
        if (c.CompletedOn is not { } completed) return (CrewGapStates.ReviewRequired, "COMPLETION_DATE_UNKNOWN");
        if (completed > window.From) return (CrewGapStates.Gap, "NOT_COMPLETED_BEFORE_WINDOW");
        DateOnly? until = c.ValidUntil;
        if (requirement.ValidityMonths is { } months) {
            var byMatrix = completed.AddMonths(months);
            until = until == null || byMatrix < until ? byMatrix : until;
        }
        if (until is { } end && end <= window.To) return (CrewGapStates.Gap, "VALIDITY_EXPIRED_OR_EXPIRES_WITHIN_WINDOW");
        return c.SignOffOutcome switch {
            null => (CrewGapStates.ReviewRequired, "HUMAN_SIGN_OFF_MISSING"),
            Confirmed when c.SignOffCurrent => (CrewGapStates.Satisfied, "HUMAN_SIGN_OFF_RECORDED"),
            Confirmed => (CrewGapStates.ReviewRequired, "HUMAN_SIGN_OFF_NOT_CURRENT"),
            _ => (CrewGapStates.Gap, "HUMAN_SIGN_OFF_NEGATIVE")
        };
    }

    private static string Code(string value)
    {
        var chars = value.Select(ch => char.IsAsciiLetterOrDigit(ch) ? char.ToUpperInvariant(ch) : '_').ToArray();
        var code = new string(chars);
        return code.Length is > 0 and <= 64 ? code : "UNRECOGNIZED";
    }
}

public sealed record SafeManningEvaluation(Guid VesselId, string State, CrewEligibilityReason[] Reasons, string MsmdState,
    Guid? MsmdId, long? MsmdRevision, ManningRoleCoverage[] Roles, CrewEligibilityResult[] Members, CrewEligibilityWindow Window,
    DateTimeOffset EvaluatedAt, string Hash, string[] Qualifications, string Authority = CrewEligibilityPolicy.Authority);

// DEV-050 vessel level: MSMD role counts over per-assignment advisory states. Only an
// ELIGIBLE or ATTENTION member counts as qualified; REVIEW_REQUIRED stays unknown.
public static class SafeManningPolicy
{
    public const string MsmdCurrent = "CURRENT";
    public const string MemberNotEligible = "MEMBER_NOT_ELIGIBLE";
    public static readonly string[] Qualifications = [.. CrewEligibilityPolicy.Qualifications, "MSMD_MINIMUM_ROLE_COUNT_ONLY", "MASTER_OR_DPA_DECISION_REQUIRED"];

    public static (string State, CrewEligibilityReason[] Reasons, ManningRoleCoverage[] Roles) Evaluate(string msmdState, MsmdRoleRequirement[]? roles, CrewEligibilityResult[] members)
    {
        var reasons = new List<CrewEligibilityReason>();
        ManningRoleCoverage[] coverage = [];
        if (msmdState != MsmdCurrent || roles is not { Length: > 0 }) {
            reasons.Add(new(msmdState == MsmdCurrent ? "MSMD_ROLE_PROFILE_EMPTY" : "MSMD_" + msmdState, "MSMD", CrewEligibilityStates.ReviewRequired));
        } else {
            var candidates = members.Where(m => m.PositionId != null).Select(m => new ManningCandidate(m.PositionId!.Value, m.AssignmentId,
                m.State is CrewEligibilityStates.Eligible or CrewEligibilityStates.Attention ? true : m.State == CrewEligibilityStates.NotEligible ? false : null)).ToArray();
            var outcome = CrewJobCoveragePolicy.Observe(roles, candidates);
            coverage = outcome.Roles;
            reasons.Add(new(outcome.State switch {
                "CurrentMinimumRoleCoverage" => "MSMD_MINIMUM_ROLE_COUNT_MET",
                "Blocked" => "MSMD_ROLE_COUNT_NOT_MET",
                _ => "MSMD_ROLE_QUALIFICATION_INCOMPLETE"
            }, "MSMD", outcome.State switch {
                "CurrentMinimumRoleCoverage" => "INFO",
                "Blocked" => CrewEligibilityStates.NotEligible,
                _ => CrewEligibilityStates.ReviewRequired
            }));
            var counted = roles.Select(r => r.PositionId).ToHashSet();
            if (members.Any(m => m.PositionId is { } p && counted.Contains(p) && m.State == CrewEligibilityStates.Attention))
                reasons.Add(new("MEMBER_ATTENTION", "CREW", CrewEligibilityStates.Attention));
        }
        if (members.Length == 0) reasons.Add(new("NO_CREW_ASSIGNMENT_IN_WINDOW", "CREW", CrewEligibilityStates.ReviewRequired));
        // A known-negative crew member is a vessel-level negative even when the MSMD source is
        // unavailable or the position is not MSMD-counted; it can never hide behind REVIEW_REQUIRED.
        if (members.Any(m => m.State == CrewEligibilityStates.NotEligible)) reasons.Add(new(MemberNotEligible, "CREW", CrewEligibilityStates.NotEligible));
        // Member review/attention states also bound the vessel state (their reasons stay on the member).
        var severities = reasons.Select(r => r.Severity).Concat(members.Select(m => m.State == CrewEligibilityStates.Eligible ? "INFO" : m.State));
        return (CrewEligibilityPolicy.Aggregate(severities), reasons.ToArray(), coverage);
    }
}

// DEV-050 Safe Manning acceptance rule (SOLAS V/14 and IMO A.1047(27): the flag
// Administration issues the MSMD and the ship must be manned accordingly; STCW Art. VIII:
// only the Administration may issue a dispensation, in exceptional necessity, to a person
// certificated for the post immediately below, never for master/chief engineer except force
// majeure). The system only records and links the human-supplied facts; it never verifies a
// dispensation, and continuing crewing difficulty is never a ground it can accept.
public sealed record ManningReviewAcknowledgement(string ReasonCode, string Statement);
public sealed record ManningDispensation(string EvidenceKind, Guid EvidenceId, long EvidenceRevision, Guid AssignmentId, Guid PositionId, string Post,
    DateOnly ValidFrom, DateOnly ValidTo, bool HoldsCertificateForPostImmediatelyBelow, bool ForceMajeureDeclared);

public static class SafeManningDecisionPolicy
{
    public const string Accepted = "MANNING_ACCEPTED", NotAccepted = "MANNING_NOT_ACCEPTED", Hold = "MANNING_HOLD";
    public const string DispensationRequired = "manning_dispensation_required", AcknowledgementRequired = "manning_review_acknowledgement_required";
    public const string DispensationNotApplicable = "manning_dispensation_not_applicable";
    // STCW Art. VIII dispensations cover certificate gaps only (certificate of competency or its
    // endorsement). Work/rest, competence, familiarization, sign-off, medical or assignment
    // negatives can never be cleared by a dispensation.
    public static readonly string[] DispensableRequirementTypes = [CrewRequirementTypes.Certificate, CrewRequirementTypes.Endorsement];
    public static bool Dispensable(CrewEligibilityResult member) => member.Reasons.Where(r => r.Severity == CrewEligibilityStates.NotEligible).All(r =>
        r.Dimension == "CREDENTIAL" && (r.RequirementId is not { } requirement ||
            member.Gaps.Any(g => g.RequirementId == requirement && DispensableRequirementTypes.Contains(g.Type, StringComparer.Ordinal))));
    public static readonly string[] EvidenceKinds = ["EVIDENCE", "CONTROLLED_DOCUMENT"];
    // Closed post codes; MASTER and CHIEF_ENGINEER need an explicit force-majeure declaration.
    public static readonly string[] Posts = ["MASTER", "CHIEF_ENGINEER", "OFFICER", "ENGINEER_OFFICER", "RATING", "OTHER"];
    public static readonly string[] CommandPosts = ["MASTER", "CHIEF_ENGINEER"];
    public const int MaxStatement = 500;

    // Distinct REVIEW_REQUIRED reason codes of the pinned evaluation (vessel and member level).
    public static string[] ReviewReasonCodes(SafeManningEvaluation evaluation) =>
        evaluation.Reasons.Concat(evaluation.Members.SelectMany(m => m.Reasons)).Where(r => r.Severity == CrewEligibilityStates.ReviewRequired)
            .Select(r => r.Code).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();
    // Assignments with a known negative that only an Administration dispensation can cover.
    public static CrewEligibilityResult[] NotEligibleMembers(SafeManningEvaluation evaluation) =>
        evaluation.Members.Where(m => m.State == CrewEligibilityStates.NotEligible).ToArray();

    public static bool CommandPost(string post, string? positionText) => CommandPosts.Contains(post, StringComparer.Ordinal) ||
        positionText != null && (positionText.Contains("MASTER", StringComparison.OrdinalIgnoreCase) ||
            positionText.Contains("CHIEF ENGINEER", StringComparison.OrdinalIgnoreCase) || positionText.Contains("CHIEF_ENGINEER", StringComparison.OrdinalIgnoreCase));

    public static bool ValidStatement(string? value) => value is { Length: > 0 and <= MaxStatement } && !string.IsNullOrWhiteSpace(value) &&
        !value.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t'));

    // Returns null when the decision may be recorded, otherwise the refusal code. positionText
    // maps a position id to its declared rank/code text (used to catch master/chief posts).
    public static string? Refusal(SafeManningEvaluation evaluation, string decision, ManningReviewAcknowledgement[] acknowledgements,
        ManningDispensation[] dispensations, Func<Guid, string?> positionText)
    {
        if (decision is NotAccepted or Hold) return acknowledgements.Length == 0 && dispensations.Length == 0 ? null : "validation_failed";
        if (decision != Accepted) return "validation_failed";
        if (evaluation.State is CrewEligibilityStates.Eligible or CrewEligibilityStates.Attention)
            return acknowledgements.Length == 0 && dispensations.Length == 0 ? null : "validation_failed";
        // Every review reason needs its own non-empty "verified onboard" statement; no blanket,
        // duplicate or unknown acknowledgement is accepted.
        var review = ReviewReasonCodes(evaluation);
        if (acknowledgements.Any(a => !ValidStatement(a.Statement)) || acknowledgements.Select(a => a.ReasonCode).Distinct(StringComparer.Ordinal).Count() != acknowledgements.Length ||
            acknowledgements.Any(a => !review.Contains(a.ReasonCode, StringComparer.Ordinal)) || review.Any(code => !acknowledgements.Any(a => a.ReasonCode == code)))
            return AcknowledgementRequired;
        if (evaluation.State == CrewEligibilityStates.ReviewRequired) return dispensations.Length == 0 ? null : "validation_failed";
        if (evaluation.State != CrewEligibilityStates.NotEligible) return "validation_failed";
        var negative = NotEligibleMembers(evaluation);
        var covered = new HashSet<Guid>();
        foreach (var d in dispensations) {
            var member = negative.SingleOrDefault(m => m.AssignmentId == d.AssignmentId);
            if (member != null && !Dispensable(member)) return DispensationNotApplicable;
            if (member == null || member.PositionId != d.PositionId || !covered.Add(d.AssignmentId) || !EvidenceKinds.Contains(d.EvidenceKind) ||
                d.EvidenceId == Guid.Empty || d.EvidenceRevision < 1 || !Posts.Contains(d.Post, StringComparer.Ordinal) ||
                d.ValidFrom > evaluation.Window.From || d.ValidTo < evaluation.Window.To || !d.HoldsCertificateForPostImmediatelyBelow ||
                CommandPost(d.Post, positionText(d.PositionId)) && !d.ForceMajeureDeclared) return DispensationRequired;
        }
        if (negative.Any(m => !covered.Contains(m.AssignmentId))) return DispensationRequired;
        // Vessel-level negatives (MSMD role count) are covered only if the dispensed members
        // complete the count; a missing person can never be dispensed into existence.
        foreach (var reason in evaluation.Reasons.Where(r => r.Severity == CrewEligibilityStates.NotEligible)) {
            if (reason.Code == SafeManningPolicy.MemberNotEligible) continue; // covered per member above
            if (reason.Code != "MSMD_ROLE_COUNT_NOT_MET" || evaluation.Roles.Length == 0) return DispensationRequired;
            var roles = evaluation.Roles.Select(r => new MsmdRoleRequirement(r.PositionId, r.Required, "PINNED_EVALUATION")).ToArray();
            var candidates = evaluation.Members.Where(m => m.PositionId != null).Select(m => new ManningCandidate(m.PositionId!.Value, m.AssignmentId,
                covered.Contains(m.AssignmentId) || m.State is CrewEligibilityStates.Eligible or CrewEligibilityStates.Attention ? true
                    : m.State == CrewEligibilityStates.NotEligible ? false : null)).ToArray();
            if (CrewJobCoveragePolicy.Observe(roles, candidates).State == "Blocked") return DispensationRequired;
        }
        return null;
    }
}
