using System.Globalization;

using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// DEV-097/098 owning lookup readers. A daily reference field is Known only as
// an exact pin (source:id@revision) to an existing tenant/vessel-scoped owning
// revision that the current actor may read. A reader never copies the owning
// content, never upgrades it to verified/qualified, and never manufactures a
// lookup that has no owning module: without a registered reader the field
// remains lookup_source_unavailable (Missing/Unknown stay editable). Other
// packages add a reader by registering another IDailyReferenceReader; the
// domain policy is not edited (e.g. Engine work_orders/defect readers).
public sealed record DailyReferenceContext(PoseidonDbContext Db, TrustedActor Actor, Guid VesselId, string Kind, DateTimeOffset Now);
public sealed record DailyReferenceCheck(string State, long? CurrentRevision, string Qualification);
public sealed record DailyReferenceDto(string Field, string State, string? Source, Guid? Id, long? Revision, long? CurrentRevision, string Qualification);
public sealed record DailyReferencesDto(Guid RecordId, Guid VesselId, string Kind, long Revision, uint Version, DailyReferenceDto[] Items);
public sealed record DailyReferenceOptionDto(string Token, string Source, Guid Id, long Revision, string Label, string Qualification);
public sealed record DailyReferenceOptionsDto(Guid VesselId, string Kind, string Field, bool Available, DailyReferenceOptionDto[] Items, bool HasMore);

public interface IDailyReferenceReader
{
    // Pin source prefix owned by this reader (lowercase, e.g. "crew-assignment").
    string Source { get; }
    bool Supports(string kind, string field);
    // The actor may read the owning source at all (checked before any lookup).
    bool Available(TrustedActor actor);
    // State: Current | Superseded | ReviewRequired | NotFound.
    Task<DailyReferenceCheck> Resolve(DailyReferenceContext context, DailyReferencePin pin, CancellationToken ct);
    Task<DailyReferenceOptionDto[]> Options(DailyReferenceContext context, string field, int take, CancellationToken ct);
}

public sealed class DailyReferenceRegistry(IEnumerable<IDailyReferenceReader> readers)
{
    private readonly IDailyReferenceReader[] all = readers.ToArray();
    // Exactly one owning reader per field; an ambiguous registration fails closed.
    public IDailyReferenceReader? For(string kind, string field) {
        var matches = all.Where(r => r.Supports(kind, field)).Take(2).ToArray();
        return matches.Length == 1 && DailyOperationsPolicy.Schema(kind).Any(f => f.Key == field && f.Type == "Reference" && !f.ReadOnly) ? matches[0] : null;
    }
    public bool Resolvable(string kind, string field) => For(kind, field) != null;
    public DailyFieldDefinition[] Schema(string kind) => DailyOperationsPolicy.Schema(kind)
        .Select(f => f.ReferenceUnavailable && Resolvable(kind, f.Key) ? f with { ReferenceUnavailable = false } : f).ToArray();

    public async Task<DailyReferenceDto> Describe(DailyReferenceContext context, string field, DailyValue? value, CancellationToken ct) {
        if (value == null || value.State != "Known") return new(field, value?.State ?? "Missing", null, null, null, null, "NO_OWNING_REFERENCE_DECLARED");
        var reader = For(context.Kind, field);
        if (reader == null) return new(field, "SourceUnavailable", null, null, null, null, "OWNING_LOOKUP_UNAVAILABLE");
        if (!DailyReferencePin.TryParse(value.Value, out var pin) || pin.Source != reader.Source) return new(field, "Invalid", null, null, null, null, "PIN_NOT_RESOLVABLE");
        if (!reader.Available(context.Actor)) return new(field, "NotVisible", pin.Source, null, null, null, "OWNING_SOURCE_ACCESS_REQUIRED");
        var check = await reader.Resolve(context, pin, ct);
        return new(field, check.State, pin.Source, pin.Id, pin.Revision, check.CurrentRevision, check.Qualification);
    }

    // Only references whose Known value changed in this command are resolved;
    // an unchanged earlier pin stays as the immutable fact it was when saved
    // and its current state is shown by the read projection (Superseded etc.).
    public async Task<bool> VerifyChanged(DailyReferenceContext context, IReadOnlyDictionary<string, DailyValue> fields,
        IReadOnlyDictionary<string, DailyValue> previous, CancellationToken ct) {
        foreach (var field in DailyOperationsPolicy.Schema(context.Kind).Where(f => f.Type == "Reference" && !f.ReadOnly)) {
            if (!fields.TryGetValue(field.Key, out var value) || value.State != "Known") continue;
            if (previous.TryGetValue(field.Key, out var before) && before == value) continue;
            var described = await Describe(context, field.Key, value, ct);
            if (described.State != "Current") return false;
        }
        return true;
    }
}

public static class DailyReferenceRegistration
{
    public static IServiceCollection AddDailyReferenceReaders(this IServiceCollection services) {
        services.AddScoped<IDailyReferenceReader, DailyCrewAssignmentReferenceReader>();
        services.AddScoped<IDailyReferenceReader, DailyPortCallReferenceReader>();
        services.AddScoped<IDailyReferenceReader, DailyEvidenceReferenceReader>();
        services.AddScoped<DailyReferenceRegistry>();
        return services;
    }
}

// Deck officer_of_watch -> crew roster assignment (DEV-047 owning module).
// Current only while the assignment head is exactly the pinned revision AND
// the configured assignment/position policy says it is effective now. The
// option label carries the operator-managed position, never a person name.
internal sealed class DailyCrewAssignmentReferenceReader : IDailyReferenceReader
{
    public string Source => "crew-assignment";
    public bool Supports(string kind, string field) => kind == "Deck" && field == "officer_of_watch";
    public bool Available(TrustedActor actor) => actor.Permissions.Contains("crew.read");
    public async Task<DailyReferenceCheck> Resolve(DailyReferenceContext c, DailyReferencePin pin, CancellationToken ct) {
        var head = await c.Db.CrewAssignments.AsNoTracking().SingleOrDefaultAsync(a => a.VesselId == c.VesselId && a.Id == pin.Id, ct);
        if (head == null) return new("NotFound", null, "OWNING_RECORD_NOT_FOUND");
        if (head.Revision != pin.Revision) {
            var existed = await c.Db.CrewAssignmentRevisions.AsNoTracking().AnyAsync(r => r.VesselId == c.VesselId && r.AssignmentId == pin.Id && r.Revision == pin.Revision, ct);
            return existed && pin.Revision < head.Revision ? new("Superseded", head.Revision, "OWNING_REVISION_SUPERSEDED") : new("NotFound", null, "OWNING_REVISION_NOT_FOUND");
        }
        var position = head.PositionId == null ? null : await c.Db.CrewPositions.AsNoTracking().SingleOrDefaultAsync(p => p.VesselId == c.VesselId && p.Id == head.PositionId, ct);
        var current = position != null && c.Db.QualifiedInputs != null && await c.Db.QualifiedInputs.AssignmentPolicyCurrent(c.Db, head, position, c.Now, ct);
        return current ? new("Current", head.Revision, "DECLARED_ASSIGNMENT_NO_QUALIFICATION_DECISION")
            : new("ReviewRequired", head.Revision, "ASSIGNMENT_NOT_EFFECTIVE_UNDER_CURRENT_POLICY");
    }
    public async Task<DailyReferenceOptionDto[]> Options(DailyReferenceContext c, string field, int take, CancellationToken ct) {
        var rows = await (from a in c.Db.CrewAssignments.AsNoTracking().Where(a => a.VesselId == c.VesselId && a.PositionId != null && a.Status != "Draft")
                          join p in c.Db.CrewPositions.AsNoTracking() on new { a.TenantId, Id = a.PositionId, a.VesselId } equals new { p.TenantId, Id = (Guid?)p.Id, p.VesselId }
                          orderby p.Code, a.From, a.Id
                          select new { Assignment = a, Position = p }).Take(200).ToArrayAsync(ct);
        var result = new List<DailyReferenceOptionDto>();
        foreach (var row in rows) {
            if (result.Count >= take) break;
            if (c.Db.QualifiedInputs == null || !await c.Db.QualifiedInputs.AssignmentPolicyCurrent(c.Db, row.Assignment, row.Position, c.Now, ct)) continue;
            var pin = new DailyReferencePin(Source, row.Assignment.Id, row.Assignment.Revision);
            var label = string.Join(" · ", new[] { row.Position.Code, row.Position.Rank, row.Assignment.From?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) }.Where(s => !string.IsNullOrWhiteSpace(s)));
            result.Add(new(pin.Token, Source, pin.Id, pin.Revision, label, "DECLARED_ASSIGNMENT_NO_QUALIFICATION_DECISION"));
        }
        return result.ToArray();
    }
}

// Noon from_port/to_port -> Ship Operations port-activity/SOF record (DEV-101
// owning ledger). The pinned record is itself a draft declaration whose own
// port identity reference stays unresolved; the pin links the port call, it
// does not resolve a port master or UN/LOCODE (no such owning module exists).
internal sealed class DailyPortCallReferenceReader : IDailyReferenceReader
{
    public string Source => "port-call";
    public bool Supports(string kind, string field) => kind == "Noon" && field is "from_port" or "to_port";
    public bool Available(TrustedActor actor) => ShipOperationsEndpoints.CanRead(actor, "Port");
    public async Task<DailyReferenceCheck> Resolve(DailyReferenceContext c, DailyReferencePin pin, CancellationToken ct) {
        var revision = await c.Db.ShipOperationRevisions.AsNoTracking().AnyAsync(r => r.VesselId == c.VesselId && r.Kind == "Port" && r.RecordId == pin.Id &&
            r.Revision == pin.Revision && r.SourceVersion == ShipOperationsPolicy.SourceVersion, ct);
        var head = await c.Db.ShipOperations.AsNoTracking().Where(r => r.VesselId == c.VesselId && r.Kind == "Port" && r.Id == pin.Id).Select(r => (long?)r.Revision).SingleOrDefaultAsync(ct);
        if (!revision || head == null) return new("NotFound", null, "OWNING_RECORD_NOT_FOUND");
        return head == pin.Revision ? new("Current", head, "PORT_CALL_DRAFT_UNVERIFIED_PORT_IDENTITY_UNRESOLVED")
            : new("Superseded", head, "OWNING_REVISION_SUPERSEDED");
    }
    public async Task<DailyReferenceOptionDto[]> Options(DailyReferenceContext c, string field, int take, CancellationToken ct) {
        var rows = await c.Db.ShipOperations.AsNoTracking().Where(r => r.VesselId == c.VesselId && r.Kind == "Port" && r.SourceVersion == ShipOperationsPolicy.SourceVersion)
            .OrderByDescending(r => r.OccurredAt).ThenBy(r => r.Id).Take(take).ToArrayAsync(ct);
        return rows.Select(r => {
            string label;
            try {
                var fields = ShipOperationsPolicy.ReadFields("Port", c.VesselId, r.FieldsJson);
                label = string.Join(" · ", new[] { "activity_type", "event_start", "terminal" }
                    .Select(k => fields.GetValueOrDefault(k) is { State: "Known", Value: { } v } ? v : null).Where(v => v != null));
            } catch (InvalidDataException) { label = ""; }
            var pin = new DailyReferencePin(Source, r.Id, r.Revision);
            return new DailyReferenceOptionDto(pin.Token, Source, r.Id, r.Revision, label.Length == 0 ? "r" + r.Revision.ToString(CultureInfo.InvariantCulture) : label,
                "PORT_CALL_DRAFT_UNVERIFIED_PORT_IDENTITY_UNRESOLVED");
        }).ToArray();
    }
}

// Noon/Deck evidence_ref -> Control & Evidence library Evidence revision.
// Visibility reuses the evidence owning predicate. A link is not verification.
internal sealed class DailyEvidenceReferenceReader : IDailyReferenceReader
{
    public string Source => "evidence";
    public bool Supports(string kind, string field) => kind is "Noon" or "Deck" && field == "evidence_ref";
    public bool Available(TrustedActor actor) => ControlEvidenceEndpoints.Read(actor);
    public async Task<DailyReferenceCheck> Resolve(DailyReferenceContext c, DailyReferencePin pin, CancellationToken ct) {
        var visible = await OperationEquipmentCommands.Visible(c.Db, c.Actor, c.Db.ControlEvidenceRevisions.AsNoTracking())
            .AnyAsync(r => r.VesselId == c.VesselId && r.Kind == "Evidence" && r.Id == pin.Id && r.Revision == pin.Revision, ct);
        var head = await c.Db.ControlEvidenceHeads.AsNoTracking().Where(h => h.VesselId == c.VesselId && h.Kind == "Evidence" && h.Id == pin.Id)
            .Select(h => (long?)h.Revision).SingleOrDefaultAsync(ct);
        if (!visible || head == null) return new("NotFound", null, "OWNING_RECORD_NOT_FOUND");
        return head == pin.Revision ? new("Current", head, "EVIDENCE_LINK_IS_NOT_VERIFICATION") : new("Superseded", head, "OWNING_REVISION_SUPERSEDED");
    }
    public async Task<DailyReferenceOptionDto[]> Options(DailyReferenceContext c, string field, int take, CancellationToken ct) {
        var rows = await OperationEquipmentCommands.Visible(c.Db, c.Actor, c.Db.ControlEvidenceRevisions.AsNoTracking())
            .Where(r => r.VesselId == c.VesselId && r.Kind == "Evidence" &&
                c.Db.ControlEvidenceHeads.Any(h => h.VesselId == r.VesselId && h.Kind == r.Kind && h.Id == r.Id && h.Revision == r.Revision))
            .OrderByDescending(r => r.SavedAt).ThenBy(r => r.Id).Select(r => new { r.Id, r.Revision, r.Lifecycle, r.SavedAt }).Take(take).ToArrayAsync(ct);
        return rows.Select(r => {
            var pin = new DailyReferencePin(Source, r.Id, r.Revision);
            return new DailyReferenceOptionDto(pin.Token, Source, r.Id, r.Revision,
                r.Lifecycle + " · r" + r.Revision.ToString(CultureInfo.InvariantCulture) + " · " + r.SavedAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                "EVIDENCE_LINK_IS_NOT_VERIFICATION");
        }).ToArray();
    }
}
