using Poseidon.Persistence;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

public sealed class SeedValidationTests
{
    internal static SeedData Demo() => SeedData.Load(Path.Combine(AppContext.BaseDirectory, "synthetic-demo.json"));

    [Fact]
    public void Demo_includes_two_isolated_tenants_and_ship_tug_profiles()
    {
        var data = Demo();
        Assert.Equal(2, data.Tenants.Length);
        Assert.Equal(3, data.Vessels.Length);
        Assert.Single(data.TugProfiles);
    }

    [Fact]
    public void Cross_tenant_company_reference_is_rejected()
    {
        var data = Demo();
        data.Vessels[0].CompanyId = data.Companies[1].Id;
        Assert.Throws<InvalidDataException>(data.Validate);
    }

    [Fact]
    public void Duplicate_identity_is_rejected()
    {
        var data = Demo();
        data = data with { Tenants = [.. data.Tenants, data.Tenants[0]] };
        Assert.Throws<InvalidDataException>(data.Validate);
    }

    [Fact]
    public void Missing_tug_profile_is_rejected()
    {
        var data = Demo() with { TugProfiles = [] };
        Assert.Throws<InvalidDataException>(data.Validate);
    }

    [Fact]
    public void Ship_cannot_receive_tug_profile()
    {
        var data = Demo();
        data.TugProfiles[0].VesselId = data.Vessels[0].Id;
        Assert.Throws<InvalidDataException>(data.Validate);
    }

    [Fact]
    public void Unknown_manifest_fields_are_rejected()
    {
        var path = Path.GetTempFileName();
        try
        {
            File.WriteAllText(path, File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "synthetic-demo.json")).Replace("\"schemaVersion\": 1", "\"unrecognized\": true, \"schemaVersion\": 1"));
            Assert.Throws<System.Text.Json.JsonException>(() => SeedData.Load(path));
        }
        finally { File.Delete(path); }
    }
}
