using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// DEV-103 / T-PMS2-001 (18.01 Backlog row 104; 59.04 Rev2 service-engineer-report paragraphs 2440-2474).
[Trait("Requirement", "DEV-103")]
public sealed class ServiceReportPolicyTests
{
    private static readonly DateOnly Today = new(2026, 11, 20);
    private static ServiceReportContent Complete() => new("FICTIONAL Marine Services", "FICTIONAL Engineer", "2026-11-19", "FICTIONAL overhaul scope",
        "FICTIONAL work performed", [new("FICTIONAL seal kit", "PN-1", "2", "pcs")], [new("Vibration", "2.1", "mm/s", "WITHIN_LIMITS")],
        "FICTIONAL wear found", "FICTIONAL recommendation", "2027-05-19");

    [Fact]
    public void Incomplete_draft_is_storable_but_submission_lists_every_missing_field()
    {
        var draft = new ServiceReportContent(null, null, null, null, null, [], [new("Pressure", null, "bar", null)], null, null, null);
        Assert.True(ServiceReportPolicy.Valid(draft));
        Assert.Equal(["service_provider", "engineer_name", "service_date", "reason_scope", "work_performed", "equipment_id", "measurement_value"],
            ServiceReportPolicy.Missing(draft, false, Today));
        Assert.Empty(ServiceReportPolicy.Missing(Complete(), true, Today));
    }

    [Fact]
    public void Future_service_date_blocks_submission()
    {
        Assert.Contains("service_date_in_future", ServiceReportPolicy.Missing(Complete() with { ServiceDate = "2026-11-21" }, true, Today));
    }

    [Fact]
    public void Invalid_shapes_are_rejected()
    {
        Assert.False(ServiceReportPolicy.Valid(Complete() with { ServiceDate = "19.11.2026" }));
        Assert.False(ServiceReportPolicy.Valid(Complete() with { ServiceProvider = "  " }));
        Assert.False(ServiceReportPolicy.Valid(Complete() with { EngineerName = "bad\u0001name" }));
        Assert.False(ServiceReportPolicy.Valid(Complete() with { Parts = [new("seal", null, "-1", null)] }));
        Assert.False(ServiceReportPolicy.Valid(Complete() with { Parts = [new("seal", null, "1e3", null)] }));
        Assert.False(ServiceReportPolicy.Valid(Complete() with { Measurements = [new("Vibration", "2", null, "PASS")] }));
        Assert.False(ServiceReportPolicy.Valid(Complete() with { Parts = Enumerable.Repeat(new ServicePart("x", null, null, null), 101).ToArray() }));
        Assert.True(ServiceReportPolicy.Valid(Complete()));
    }

    [Fact]
    public void Only_draft_or_returned_reports_are_editable()
    {
        Assert.True(ServiceReportPolicy.CanEdit("DRAFT"));
        Assert.True(ServiceReportPolicy.CanEdit("RETURNED"));
        Assert.False(ServiceReportPolicy.CanEdit("SUBMITTED"));
        Assert.False(ServiceReportPolicy.CanEdit("VERIFIED"));
    }
}
