namespace Poseidon.Persistence;

public interface ITenantEntity { Guid TenantId { get; } }

public sealed class Tenant
{
    public Guid Id { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public uint Version { get; set; }
}

public sealed class Company : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public uint Version { get; set; }
}

public enum VesselKind { Ship, Tug }

public sealed class Vessel : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public required string Code { get; set; }
    public required string Name { get; set; }
    public VesselKind Kind { get; set; }
    public uint Version { get; set; }
}

public sealed class VesselProfile : ITenantEntity
{
    public Guid VesselId { get; set; }
    public Guid TenantId { get; set; }
    public decimal GrossTonnage { get; set; }
    public uint Version { get; set; }
}

public sealed class TugProfile : ITenantEntity
{
    public Guid VesselId { get; set; }
    public Guid TenantId { get; set; }
    public decimal BollardPullTonnes { get; set; }
    public uint Version { get; set; }
}

// Metadata only. Never store passwords, tokens, connection strings or raw record snapshots.
public sealed class AuditEvent : ITenantEntity
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public required string ActorId { get; set; }
    public required string ActorType { get; set; }
    public required string Action { get; set; }
    public required string EntityType { get; set; }
    public required string EntityId { get; set; }
    public required string CorrelationId { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    // Nullable means unavailable historical metadata, never guessed during migration.
    public Guid? VesselId { get; set; }
    public Guid? ResourceId { get; set; }
    public string? RoleContext { get; set; }
    public string? CapabilityContext { get; set; }
    public string? ChangeSummary { get; set; }
    public uint? BeforeVersion { get; set; }
    public uint? AfterVersion { get; set; }
    public long? BeforeRevision { get; set; }
    public long? AfterRevision { get; set; }
    public string? ResourceKind { get; set; }
    public string? ReasonStatus { get; set; }
    public string? Source { get; set; }
}

public sealed record ActorContext(string Id, string Type, string CorrelationId, string? CapabilityContext = null, string? Source = null, string? RoleContext = null)
{
    public static ActorContext SeedImport() => new("system:seed-import", "System", Guid.NewGuid().ToString(), Source: "SeedImport");
}

// Retry metadata only; register receipts pin the committed immutable revision and xmin.
public sealed class CommandReceipt : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public required string Key { get; set; }
    public required string Action { get; set; }
    public required string Fingerprint { get; set; }
    public Guid ResourceId { get; set; }
    public DateTimeOffset CommittedAt { get; set; }
    public long? RegisterRevision { get; set; }
    public uint? RegisterVersion { get; set; }
}
