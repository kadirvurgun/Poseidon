using System.Security.Cryptography;
using System.Text;

namespace Poseidon.Persistence.Seeding;

// PLAN-1511-5201 synthetic HR fixture. Idempotent by deterministic ids; it adds only missing rows and never
// rewrites operator edits. No real person/company data, no verified person, no active employment, no approved
// assignment and no actual (fiilî) fact is seeded. Roles stay Unverified; legal entities stay incomplete Drafts.
internal static class HrFixtures
{
    internal static readonly DateTimeOffset SyntheticStart = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
    internal static Guid Id(Guid scope, string kind) =>
        new(SHA256.HashData(scope.ToByteArray().Concat(Encoding.UTF8.GetBytes("SYNTHETIC-HR-5201-" + kind)).ToArray()).AsSpan(0, 16));

    internal static async Task AddMissingAsync(PoseidonDbContext db, IEnumerable<Company> companies, IEnumerable<Vessel> vessels, CancellationToken ct)
    {
        var actor = db.AuditActorId;
        foreach (var company in companies)
        {
            var identifier = Id(company.Id, "identifier");
            if (await db.LegalEntityIdentifiers.FindAsync([company.TenantId, identifier], ct) == null)
                db.LegalEntityIdentifiers.Add(new() { TenantId = company.TenantId, Id = identifier, LegalEntityId = company.Id, Type = "Other",
                    Issuer = "SYNTHETIC-FIXTURE", Value = "SYN-" + company.Id.ToString("N")[..12].ToUpperInvariant(), SourceKind = "Synthetic",
                    SourceRef = "synthetic-demo/hr-5201-v1", CreatedBy = actor, CreatedAt = SyntheticStart });
            var employer = Id(company.Id, "employer-role");
            if (await db.LegalEntityRoles.FindAsync([company.TenantId, employer], ct) == null)
                db.LegalEntityRoles.Add(new() { TenantId = company.TenantId, Id = employer, LegalEntityId = company.Id, Role = "Employer",
                    EffectiveFrom = SyntheticStart, SourceKind = "Synthetic", SourceRef = "synthetic-demo/hr-5201-v1", CreatedBy = actor, CreatedAt = SyntheticStart });
        }
        foreach (var vessel in vessels)
        {
            // CrewFixtures seeds one synthetic Person per vessel with Person.Id == Vessel.Id (the single person master).
            var employment = Id(vessel.Id, "employment");
            if (await db.Employments.FindAsync([vessel.TenantId, employment], ct) == null)
                db.Employments.Add(new() { TenantId = vessel.TenantId, Id = employment, PersonId = vessel.Id, LegalEntityId = vessel.CompanyId,
                    EmployeeNo = "SYN-" + vessel.Code, EmploymentType = "Employee", StartAt = SyntheticStart, State = "Draft",
                    CreatedBy = actor, CreatedAt = SyntheticStart });
            var assignment = Id(vessel.Id, "assignment");
            if (await db.HrAssignments.FindAsync([vessel.TenantId, assignment], ct) == null)
                db.HrAssignments.Add(new() { TenantId = vessel.TenantId, Id = assignment, EmploymentId = employment, PersonId = vessel.Id,
                    LegalEntityId = vessel.CompanyId, Kind = "Vessel", VesselId = vessel.Id, PlannedFrom = SyntheticStart.AddMonths(1),
                    PlannedTo = SyntheticStart.AddMonths(4), PlannedZoneId = "UTC", State = "Planned", PlannedBy = actor, PlannedAt = SyntheticStart });
        }
    }
}
