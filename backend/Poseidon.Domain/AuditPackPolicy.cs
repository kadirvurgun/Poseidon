using System.Security.Cryptography;
using System.Text;

namespace Poseidon.Domain;

// S10 WP-A DEV-064 / T-S10-001 (18.01 Backlog row:65; audit-ready 13.02 paragraph:393, 15.01 paragraph:748; audit pack =
// immutable manifest whose binaries are re-checked at the source on every request, 62.04 paragraph:53 and paragraph:478).
// A pack is a SYSTEM COMPILATION, NOT AN OFFICIAL RECORD: it pins exact revisions + hashes of requirement, control,
// evidence (current and superseded), verification, decision and history rows that the creating reader could see at
// compile time. Reading a pack re-authorizes every item for the current reader; an item the reader may not see is
// only counted as Excluded (no id, no content). A new compilation is a new manifest; nothing is updated or deleted.
public sealed record AuditPackEntry(string Kind, Guid ResourceId, long Revision, string State, string? Hash, Guid EvidenceId, Guid ControlId,
    DateTimeOffset? SavedAt, Guid? FileId = null, long? FileRevision = null);

public static class AuditPackPolicy
{
    public const string Label = "SYSTEM_COMPILATION_NOT_OFFICIAL_RECORD";
    public const string SourceVersion = "18.01/Backlog/row:65;13.02/paragraph:393;15.01/paragraph:748;62.04/paragraph:53;62.04/paragraph:478";
    public static readonly string[] Kinds = ["Source", "Requirement", "Control", "Evidence", "Verification", "Decision", "History"];
    // Current = the chained revision; Superseded = an older evidence revision kept for trace (never deleted);
    // Unavailable = the pinned revision could not be read at compile time; Recorded = a human verification/decision row.
    public static readonly string[] States = ["Current", "Superseded", "Unavailable", "Recorded"];
    // Integrity at read time: the source row still carries the pinned hash (Match), carries a different one (Mismatch)
    // or can no longer be read / its file is no longer clean (SourceUnavailable).
    public static readonly string[] Integrity = ["Match", "Mismatch", "SourceUnavailable"];
    public const int MaxItems = 2048, MaxSupersededPerEvidence = 32, MaxHistoryPerControl = 32, MaxEventsPerChain = 64;

    public static string ManifestHash(Guid vessel, DateTimeOffset asOf, IEnumerable<AuditPackEntry> items) =>
        Hash("AUDIT_PACK_MANIFEST_V1\n" + vessel.ToString("D") + "\n" + asOf.UtcDateTime.ToString("O", System.Globalization.CultureInfo.InvariantCulture) + "\n" + Label + "\n" +
            string.Join("\n", Ordered(items).Select(i => string.Join("|", i.Kind, i.ResourceId.ToString("D"), i.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture),
                i.State, i.Hash ?? "-", i.EvidenceId.ToString("D"), i.ControlId.ToString("D"), i.FileId?.ToString("D") ?? "-",
                i.FileRevision?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? "-"))));

    public static IEnumerable<AuditPackEntry> Ordered(IEnumerable<AuditPackEntry> items) =>
        items.OrderBy(i => i.EvidenceId).ThenBy(i => i.ControlId).ThenBy(i => Array.IndexOf(Kinds, i.Kind)).ThenBy(i => i.ResourceId).ThenBy(i => i.Revision).ThenBy(i => i.State, StringComparer.Ordinal);

    public static string IntegrityOf(string? pinned, string? current, bool sourceReadable) =>
        !sourceReadable || current == null ? "SourceUnavailable" : string.Equals(pinned, current, StringComparison.OrdinalIgnoreCase) ? "Match" : "Mismatch";

    public static bool Valid(AuditPackEntry e) => e != null && Kinds.Contains(e.Kind, StringComparer.Ordinal) && States.Contains(e.State, StringComparer.Ordinal) &&
        e.ResourceId != Guid.Empty && e.Revision >= 0 && e.EvidenceId != Guid.Empty && e.ControlId != Guid.Empty &&
        (e.Hash == null || e.Hash.Length <= 64) && (e.State == "Unavailable") == (e.Hash == null) && (e.FileId == null) == (e.FileRevision == null) &&
        (e.State != "Recorded" || e.Kind is "Verification" or "Decision") && (e.State != "Superseded" || e.Kind == "Evidence");

    public static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
}
