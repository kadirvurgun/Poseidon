using System.Text.Json;
using System.Text.Json.Serialization;

namespace Poseidon.Persistence.Seeding;

public sealed record SeedData(
    int SchemaVersion, string Dataset, Tenant[] Tenants, Company[] Companies,
    Vessel[] Vessels, VesselProfile[] VesselProfiles, TugProfile[] TugProfiles)
{
    public static SeedData Load(string path)
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            RespectRequiredConstructorParameters = true
        };
        options.Converters.Add(new JsonStringEnumConverter(allowIntegerValues: false));
        var data = JsonSerializer.Deserialize<SeedData>(File.ReadAllText(path), options)
            ?? throw new InvalidDataException("Empty seed manifest.");
        data.Validate();
        return data;
    }

    public void Validate()
    {
        if (SchemaVersion != 1 || Dataset != "synthetic-demo") Fail("Unsupported seed schema or dataset.");
        if (Tenants is null || Companies is null || Vessels is null || VesselProfiles is null || TugProfiles is null)
            Fail("All seed collections are required.");
        if (Tenants.Any(x => x is null) || Companies.Any(x => x is null) || Vessels.Any(x => x is null)
            || VesselProfiles.Any(x => x is null) || TugProfiles.Any(x => x is null))
            Fail("Seed collections cannot contain null records.");
        Unique(Tenants!.Select(x => x.Id));
        Unique(Tenants.Select(x => x.Code));
        Unique(Companies!.Select(x => (x.TenantId, x.Id)));
        Unique(Companies.Select(x => (x.TenantId, x.Code)));
        Unique(Vessels!.Select(x => (x.TenantId, x.Id)));
        Unique(Vessels.Select(x => (x.TenantId, x.Code)));
        Unique(VesselProfiles!.Select(x => (x.TenantId, x.VesselId)));
        Unique(TugProfiles!.Select(x => (x.TenantId, x.VesselId)));
        foreach (var tenant in Tenants)
        {
            if (tenant.Id == Guid.Empty) Fail("Tenant IDs must be non-empty.");
            Text(tenant.Code, 64); Text(tenant.Name, 200);
        }
        foreach (var company in Companies)
        {
            if (company.Id == Guid.Empty || !Tenants.Any(t => t.Id == company.TenantId)) Fail("Invalid company tenant reference.");
            Text(company.Code, 64); Text(company.Name, 200);
        }
        foreach (var vessel in Vessels)
        {
            if (vessel.Id == Guid.Empty || !Enum.IsDefined(vessel.Kind) || !Companies.Any(c => c.TenantId == vessel.TenantId && c.Id == vessel.CompanyId))
                Fail("Invalid vessel company reference or kind.");
            Text(vessel.Code, 64); Text(vessel.Name, 200);
            if (!VesselProfiles.Any(p => p.TenantId == vessel.TenantId && p.VesselId == vessel.Id)) Fail("Every vessel requires a profile.");
            var tug = TugProfiles.Any(p => p.TenantId == vessel.TenantId && p.VesselId == vessel.Id);
            if ((vessel.Kind == VesselKind.Tug) != tug) Fail("Only tug vessels require a tug profile.");
        }
        foreach (var profile in VesselProfiles)
            if (profile.GrossTonnage < 0 || !Vessels.Any(v => v.TenantId == profile.TenantId && v.Id == profile.VesselId)) Fail("Invalid vessel profile.");
        foreach (var profile in TugProfiles)
            if (profile.BollardPullTonnes <= 0 || !Vessels.Any(v => v.TenantId == profile.TenantId && v.Id == profile.VesselId && v.Kind == VesselKind.Tug)) Fail("Invalid tug profile.");
    }

    private static void Unique<T>(IEnumerable<T> values)
    {
        var list = values.ToList();
        if (list.Distinct().Count() != list.Count) Fail("Duplicate seed identity or code.");
    }

    private static void Text(string value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length > maxLength) Fail("Invalid seed text length.");
    }

    [System.Diagnostics.CodeAnalysis.DoesNotReturn]
    private static void Fail(string message) => throw new InvalidDataException(message);
}
