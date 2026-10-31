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
}

public sealed record ActorContext(string Id, string Type, string CorrelationId)
{
    public static ActorContext SeedImport() => new("system:seed-import", "System", Guid.NewGuid().ToString());
}

// Online directory retry metadata only; no request/response bodies or credential material.
public sealed class CommandReceipt : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid ActorId { get; set; }
    public required string Key { get; set; }
    public required string Action { get; set; }
    public required string Fingerprint { get; set; }
    public Guid ResourceId { get; set; }
    public DateTimeOffset CommittedAt { get; set; }
}
