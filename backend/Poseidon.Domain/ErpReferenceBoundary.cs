using System.Collections.Frozen;
using System.Globalization;
using System.Text;
using System.Text.Json;

namespace Poseidon.Domain;

// A data-only external reference contract, not a purchasing/finance aggregate.
// Callers must authorize the current integration principal and owning object
// BEFORE decoding/reconciliation. These expected IDs are never body authority.
public sealed record ErpReferenceContext(Guid TenantId, Guid VesselId, Guid ReferenceId,
    string Kind, string SourceSystem, string SourceVersion, DateTimeOffset ReceivedAt, TimeSpan MaxAge);
public enum ErpReferenceFreshness { Current, Stale, Future }
public enum ErpReferenceReconciliation { Duplicate, NewerReference, OlderReference, Conflict, DifferentBinding }
public sealed record ErpReferenceEnvelope(Guid MessageId, Guid ReferenceId, Guid TenantId, Guid VesselId,
    string Kind, string SourceSystem, string SourceVersion, long SourceRevision, string ExternalReference,
    string? ExternalStatus, string? ExternalPurchaseOrderReference, string? ExternalVendorReference,
    string? ExternalCostContextReference, DateTimeOffset ObservedAt);
public sealed record ErpReferenceProjection(ErpReferenceEnvelope Reference, ErpReferenceFreshness Freshness);

public static class ErpReferenceBoundary
{
    public const int ContractVersion = 1;
    public const int MaxUtf8Bytes = 16384;
    public const long MaxSourceRevision = 9_007_199_254_740_991;
    public const string SourceVersion = "poseidon-v46.3-20261030";
    public static readonly IReadOnlySet<string> AllowedKinds = new[] {
        "TechnicalRequisition", "CriticalSpareReference"
    }.ToFrozenSet(StringComparer.Ordinal);
    private static readonly HashSet<string> Fields = new(StringComparer.Ordinal) {
        "contractVersion", "messageId", "referenceId", "tenantId", "vesselId", "kind", "sourceSystem", "sourceVersion",
        "sourceRevision", "externalReference", "externalStatus", "externalPurchaseOrderReference", "externalVendorReference",
        "externalCostContextReference", "observedAt"
    };
    private static bool Token(string? value, int limit) => value is { Length: > 0 } && value.Length <= limit &&
        value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '.' or ':' or '/');
    private static bool Id(JsonElement e, string key, out Guid id) {
        id = Guid.Empty; return e.TryGetProperty(key, out var x) && x.ValueKind == JsonValueKind.String &&
            Guid.TryParseExact(x.GetString(), "D", out id) && id != Guid.Empty;
    }
    private static bool Text(JsonElement e, string key, int length, bool required, out string? value) {
        value = null;
        if (!e.TryGetProperty(key, out var x) || x.ValueKind == JsonValueKind.Null) return !required;
        if (x.ValueKind != JsonValueKind.String || !Token(x.GetString(), length)) return false;
        value = x.GetString(); return true;
    }
    private static bool PositiveInteger(JsonElement e, string key, out long value) {
        value = 0;
        if (!e.TryGetProperty(key, out var x) || x.ValueKind != JsonValueKind.Number) return false;
        var raw = x.GetRawText();
        if (raw.StartsWith('-')) return false;
        var exponentAt = raw.IndexOfAny(['e', 'E']); var exponent = 0;
        if (exponentAt >= 0 && !int.TryParse(raw[(exponentAt + 1)..], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out exponent)) return false;
        var mantissa = exponentAt < 0 ? raw : raw[..exponentAt]; var dot = mantissa.IndexOf('.');
        var digits = mantissa.Replace(".", "", StringComparison.Ordinal).TrimStart('0');
        if (digits.Length == 0) return false;
        var scale = (long)exponent - (dot < 0 ? 0 : mantissa.Length - dot - 1);
        if (scale < 0) {
            var remove = -scale;
            if (remove >= digits.Length) return false;
            var cut = digits.Length - (int)remove;
            if (digits.AsSpan(cut).IndexOfAnyExcept('0') >= 0) return false;
            digits = digits[..cut]; scale = 0;
        }
        // Exact decimal lexeme arithmetic: decimal parsing could round a tiny
        // fractional revision to an integer. No floating-point or rounding.
        if (digits.Length + scale > 16 || !long.TryParse(digits, NumberStyles.None, CultureInfo.InvariantCulture, out value)) return false;
        for (var i = 0L; i < scale; i++) value *= 10;
        return value > 0 && value <= MaxSourceRevision;
    }
    public static bool TryRead(string? json, ErpReferenceContext context, out ErpReferenceProjection? projection) {
        projection = null;
        if (context is null || json == null || json.Length > MaxUtf8Bytes || Encoding.UTF8.GetByteCount(json) > MaxUtf8Bytes ||
            context.TenantId == Guid.Empty || context.VesselId == Guid.Empty || context.ReferenceId == Guid.Empty ||
            !AllowedKinds.Contains(context.Kind) || !Token(context.SourceSystem, 64) || !Token(context.SourceVersion, 80) ||
            context.ReceivedAt.Offset != TimeSpan.Zero || context.MaxAge <= TimeSpan.Zero || context.MaxAge > TimeSpan.FromDays(365)) return false;
        try {
            using var doc = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 3, AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
            var e = doc.RootElement;
            if (e.ValueKind != JsonValueKind.Object) return false;
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var field in e.EnumerateObject()) if (!Fields.Contains(field.Name) || !names.Add(field.Name)) return false;
            if (!PositiveInteger(e, "contractVersion", out var v) || v != ContractVersion ||
                !Id(e, "messageId", out var message) || !Id(e, "referenceId", out var reference) || !Id(e, "tenantId", out var tenant) || !Id(e, "vesselId", out var vessel) ||
                reference != context.ReferenceId || tenant != context.TenantId || vessel != context.VesselId ||
                !Text(e, "kind", 32, true, out var kind) || kind != context.Kind ||
                !Text(e, "sourceSystem", 64, true, out var system) || system != context.SourceSystem ||
                !Text(e, "sourceVersion", 80, true, out var sourceVersion) || sourceVersion != context.SourceVersion ||
                !PositiveInteger(e, "sourceRevision", out var rev) || rev < 1 || rev > MaxSourceRevision ||
                !Text(e, "externalReference", 160, true, out var external) ||
                !Text(e, "externalStatus", 64, false, out var status) ||
                !Text(e, "externalPurchaseOrderReference", 160, false, out var po) ||
                !Text(e, "externalVendorReference", 160, false, out var vendor) ||
                !Text(e, "externalCostContextReference", 160, false, out var cost) ||
                !e.TryGetProperty("observedAt", out var observedValue) || observedValue.ValueKind != JsonValueKind.String ||
                !DateTimeOffset.TryParseExact(observedValue.GetString(), ["yyyy-MM-dd'T'HH:mm:ss'Z'", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF'Z'", "yyyy-MM-dd'T'HH:mm:sszzz", "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"],
                    CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var observed)) return false;
            var freshness = observed > context.ReceivedAt ? ErpReferenceFreshness.Future :
                context.ReceivedAt - observed > context.MaxAge ? ErpReferenceFreshness.Stale : ErpReferenceFreshness.Current;
            projection = new(new(message, reference, tenant, vessel, kind!, system!, sourceVersion!, rev, external!, status, po, vendor, cost, observed), freshness);
            return true;
        } catch (JsonException) { return false; }
        catch (InvalidOperationException) { return false; }
    }
    // No last-write-wins for equal-revision payload differences. A receipt/DB
    // consumer must enforce this result transactionally if/when implemented.
    public static ErpReferenceReconciliation Reconcile(ErpReferenceEnvelope current, ErpReferenceEnvelope incoming) {
        if (current.ReferenceId != incoming.ReferenceId || current.TenantId != incoming.TenantId || current.VesselId != incoming.VesselId ||
            current.Kind != incoming.Kind || current.SourceSystem != incoming.SourceSystem || current.SourceVersion != incoming.SourceVersion)
            return ErpReferenceReconciliation.DifferentBinding;
        if (current.MessageId == incoming.MessageId) return current == incoming ? ErpReferenceReconciliation.Duplicate : ErpReferenceReconciliation.Conflict;
        if (incoming.SourceRevision < current.SourceRevision) return ErpReferenceReconciliation.OlderReference;
        if (incoming.SourceRevision == current.SourceRevision) return (current with { MessageId = incoming.MessageId }) == incoming ?
            ErpReferenceReconciliation.Duplicate : ErpReferenceReconciliation.Conflict;
        return ErpReferenceReconciliation.NewerReference;
    }
}
