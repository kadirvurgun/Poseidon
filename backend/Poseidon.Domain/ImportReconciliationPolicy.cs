using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Poseidon.Domain;

// S11 WP-A DEV-070 / T-S11-002 (18.01 Backlog row 71: "Source count/accepted/rejected/duplicate/missing critical list;
// owner sign-off before pilot start"). 18.03 paragraph 10 ("Import success != business validation; reconciliation ve
// owner sign-off gerekir"), paragraphs 49-66 (minimum data domains), 82 (unknown regulatory-critical data stays
// UNKNOWN/REVIEW_REQUIRED), 84-102 (count/identity/certificate/PMS sample, trace, exceptions with disposition and the
// customer data owner's signature). The report is an immutable, hash-pinned snapshot of explainable counts; it is
// never a business validation by itself. The sign-off is a separate human record by an appointed data owner who is
// neither the report author nor an uploader of a pinned batch. Nothing here signs, accepts or approves anything.
public sealed record ReconciliationBatchFacts(Guid BatchId, string Category, long Revision, string State, string SourceHash,
    string TemplateFingerprint, Guid? PreviewId, int SourceRows, int AcceptedRows, int RejectedRows, int DuplicateRows, Guid UploadedBy);
public sealed record ReconciliationCounts(int Source, int Accepted, int Rejected, int Duplicate, int NotCommitted, int MissingCritical);
public sealed record ReconciliationTarget(Guid BatchId, int RowNumber, string TargetKind, Guid TargetId);
public sealed record MissingCriticalItem(Guid BatchId, int RowNumber, string TargetKind, Guid TargetId, string Field, string Reason);
public sealed record ReconciliationException(string Kind, string Reference, string Disposition);

public static class ImportReconciliationPolicy
{
    public const string PolicyVersion = "DEV-070-reconciliation-v1";
    public const string CriticalFieldVersion = "DEV-070-critical-fields-v1";
    public const string SourceLocator = "18.01/Backlog/row:71;18.03/paragraph:10,49-66,82,84-102";
    public const string AwaitingSignOff = "AWAITING_SIGN_OFF", SignedOff = "SIGNED_OFF", Superseded = "SUPERSEDED";
    public static readonly string[] States = [AwaitingSignOff, SignedOff, Superseded];
    public const string Accepted = "ACCEPTED", AcceptedWithExceptions = "ACCEPTED_WITH_EXCEPTIONS", Rejected = "REJECTED";
    public static readonly string[] Decisions = [Accepted, AcceptedWithExceptions, Rejected];
    public static readonly string[] ExceptionKinds = ["MISSING_CRITICAL", "REJECTED_ROWS", "DUPLICATE_ROWS", "NOT_COMMITTED", "SAMPLE_UNVERIFIED", "SAMPLE_MISMATCH", "OTHER"];
    public const string Missing = "MISSING", Unknown = "UNKNOWN";
    public const string DataOwnerDuty = "DataOwner";
    public const int MaxBatches = 50, MaxSample = 100, MaxExceptions = 100, MaxMissingListed = 500;
    public static readonly string[] Categories = ["vessel", "crew", "certificate", "equipment"];
    public static readonly string[] TargetKinds = ["Vessel", "CrewAssignment", "Credential", "Asset"];

    // Per-category critical field set, derived from the 18.03 minimum dataset (p57 vessel identity; p60 crew
    // role/rank + assignment; p63 certificate type/issuer/validity/evidence; p66 equipment hierarchy/criticality),
    // restricted to fields the current templates actually carry. This is a reconciliation listing rule, not an approved
    // regulatory catalog: the customer data owner confirms or records exceptions at sign-off.
    public static FieldDefinition[] CriticalFields(string category) => (category switch {
        "vessel" => new[] { "code", "name" },
        "crew" => new[] { "position_ref", "from" },
        "certificate" => new[] { "certificate_type", "issuer", "issue_date", "expiry_date", "document_ref" },
        "equipment" => new[] { "code", "name", "level", "criticality" },
        _ => Array.Empty<string>()
    }).Select(k => new FieldDefinition(k, DataKind.Text, true)).ToArray();

    // An explicit "UNKNOWN" (any case, trimmed) is Unknown, never Known; blank is Missing. Both are listed.
    public static DraftValue Value(string? text) => text != null && string.Equals(text.Trim(), Unknown, StringComparison.OrdinalIgnoreCase)
        ? DraftValue.Unknown(DataKind.Text) : DraftValue.Text(text);

    public static IReadOnlyList<RequiredDataBlock> Assess(string category, IReadOnlyDictionary<string, string?> values) {
        var fields = CriticalFields(category);
        if (fields.Length == 0) throw new ArgumentException("Unknown reconciliation category.", nameof(category));
        var policy = new RequiredDataPolicy(fields);
        return policy.Assess(fields.Select(f => new KeyValuePair<string, DraftValue>(f.Key, Value(values.TryGetValue(f.Key, out var v) ? v : null)))).BlockingFields;
    }

    public static MissingCriticalItem[] Missings(ReconciliationTarget target, string category, IReadOnlyDictionary<string, string?> values) =>
        Assess(category, values).Select(b => new MissingCriticalItem(target.BatchId, target.RowNumber, target.TargetKind, target.TargetId, b.FieldKey,
            b.Reason == RequiredDataReason.UnknownRequiredValue ? Unknown : Missing)).ToArray();

    // Every source row is explained exactly once: accepted (committed), rejected (latest preview) or not committed.
    public static ReconciliationCounts Count(IReadOnlyCollection<ReconciliationBatchFacts> batches, int missingCritical) {
        int source = 0, accepted = 0, rejected = 0, duplicate = 0;
        foreach (var b in batches) {
            if (b.SourceRows < 0 || b.AcceptedRows < 0 || b.RejectedRows < 0 || b.DuplicateRows < 0 || b.AcceptedRows + b.RejectedRows > b.SourceRows || b.DuplicateRows > b.RejectedRows)
                throw new ArgumentException("Inconsistent batch facts.", nameof(batches));
            source += b.SourceRows; accepted += b.AcceptedRows; rejected += b.RejectedRows; duplicate += b.DuplicateRows;
        }
        return new(source, accepted, rejected, duplicate, source - accepted - rejected, missingCritical);
    }

    // Deterministic sample: the N targets with the lowest SHA-256(seed || target) order. The seed is recorded on the
    // report, so any reader can recompute the same sample; nothing is random at sign-off time.
    public static ReconciliationTarget[] Sample(string seed, IEnumerable<ReconciliationTarget> targets, int size) {
        if (string.IsNullOrEmpty(seed) || size < 1 || size > MaxSample) throw new ArgumentOutOfRangeException(nameof(size));
        return targets.DistinctBy(t => (t.TargetKind, t.TargetId))
            .Select(t => (t, key: Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(seed + "|" + t.TargetKind + "|" + t.TargetId.ToString("D"))))))
            .OrderBy(x => x.key, StringComparer.Ordinal).Take(size).Select(x => x.t).ToArray();
    }

    public static string Seed(Guid reportId) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("DEV-070-sample|" + reportId.ToString("D")))).ToLowerInvariant()[..32];

    public static string Hash(Guid vessel, Guid report, IEnumerable<ReconciliationBatchFacts> batches, ReconciliationCounts counts,
        IEnumerable<MissingCriticalItem> missing, IEnumerable<ReconciliationTarget> sample, string seed) =>
        Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(new {
            policy = PolicyVersion, fields = CriticalFieldVersion, vessel, report, seed,
            batches = batches.OrderBy(b => b.BatchId).ToArray(), counts, missing = missing.ToArray(), sample = sample.ToArray() }))).ToLowerInvariant();

    // Open items that an ACCEPTED decision cannot leave unaddressed; ACCEPTED_WITH_EXCEPTIONS must disposition them.
    public static string[] OpenItems(ReconciliationCounts counts, int sampleSize, int sampleVerified) {
        var open = new List<string>();
        if (counts.MissingCritical > 0) open.Add("MISSING_CRITICAL");
        if (counts.Rejected > 0) open.Add("REJECTED_ROWS");
        if (counts.Duplicate > 0) open.Add("DUPLICATE_ROWS");
        if (counts.NotCommitted > 0) open.Add("NOT_COMMITTED");
        if (sampleVerified < sampleSize) open.Add("SAMPLE_UNVERIFIED");
        return open.ToArray();
    }

    private static bool Text(string? value, int min, int max) => value != null && value.Trim() == value && value.Length >= min && value.Length <= max && !value.Any(char.IsControl);

    // Returns null when the human sign-off record may be written; otherwise a fixed refusal code.
    public static string? SignOffFailure(string state, string decision, string reason, IReadOnlyList<ReconciliationException>? exceptions,
        IReadOnlyCollection<Guid> sampleVerified, IReadOnlyCollection<Guid> sample, ReconciliationCounts counts, Guid signer, Guid author, IEnumerable<Guid> uploaders) {
        if (state != AwaitingSignOff) return state == Superseded ? "reconciliation_superseded" : "invalid_state";
        if (!Decisions.Contains(decision, StringComparer.Ordinal)) return "invalid_decision";
        if (!Text(reason, 10, 2000)) return "invalid_reason";
        exceptions ??= [];
        if (exceptions.Count > MaxExceptions || exceptions.Any(e => e == null || !ExceptionKinds.Contains(e.Kind, StringComparer.Ordinal) ||
            !Text(e.Reference, 1, 200) || !Text(e.Disposition, 3, 1000))) return "invalid_exception";
        if (sampleVerified.Count != sampleVerified.Distinct().Count() || sampleVerified.Any(id => !sample.Contains(id))) return "invalid_sample";
        if (signer == Guid.Empty || signer == author || uploaders.Contains(signer)) return "separation_of_duties";
        var open = OpenItems(counts, sample.Count, sampleVerified.Count);
        if (decision == Accepted && (open.Length > 0 || exceptions.Count > 0)) return "reconciliation_open_items";
        if (decision == AcceptedWithExceptions && (exceptions.Count == 0 || open.Any(o => exceptions.All(e => e.Kind != o)))) return "reconciliation_exceptions_required";
        return null;
    }

    // A sign-off is only ever effective for the exact current report; superseding invalidates it without editing it.
    public static string SignOffValidity(string reportState) => reportState == SignedOff ? "EFFECTIVE" : reportState == Superseded ? "INVALIDATED_SUPERSEDED" : "NONE";
}
