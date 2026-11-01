using System.Text.Json;
using Poseidon.Domain;

namespace Poseidon.Domain.Tests;

// Reusable bounded QA loader. No runtime seed/import, source qualification or
// verified evidence is created. Full original-byte source checks: golden_dataset.py.
public sealed record GoldenConsumerCase(string Id, string Profile, string Area, string Engine,
    Dictionary<string, string?> Inputs, string Expected, string ExpectedReadiness);
public sealed record GoldenPilotDataset(string SchemaVersion, DateTimeOffset ReferenceTime,
    bool SyntheticOnly, JsonElement[] Profiles, JsonElement[] SourceCases, GoldenConsumerCase[] ConsumerCases)
{
    public static readonly string[] Engines = ["Applicability", "RequiredData", "CredentialDates", "Freshness", "AssetHierarchy", "Maintenance", "OfflineConflict"];
    public static GoldenPilotDataset Load(Stream stream)
    {
        using var memory = new MemoryStream();
        var buffer = new byte[4096]; int count;
        while ((count = stream.Read(buffer)) > 0) {
            if (memory.Length + count > 512 * 1024) throw new InvalidDataException("dataset_budget");
            memory.Write(buffer, 0, count);
        }
        var data = JsonSerializer.Deserialize<GoldenPilotDataset>(memory.ToArray(), new JsonSerializerOptions {
            PropertyNameCaseInsensitive = true, MaxDepth = 24
        }) ?? throw new InvalidDataException("dataset_shape");
        if (data.SchemaVersion != "poseidon.golden-pilot.v1" || !data.SyntheticOnly ||
            data.ReferenceTime != new DateTimeOffset(2026, 10, 4, 0, 0, 0, TimeSpan.Zero) ||
            data.Profiles is not { Length: 7 } || data.SourceCases is not { Length: 10 } ||
            data.ConsumerCases is not { Length: > 0 and <= 128 }) throw new InvalidDataException("dataset_shape");
        var profiles = data.Profiles.Select(p => p.GetProperty("id").GetString()).ToHashSet(StringComparer.Ordinal);
        if (profiles.Count != data.Profiles.Length || data.ConsumerCases.Select(c => c.Id).Distinct(StringComparer.Ordinal).Count() != data.ConsumerCases.Length ||
            data.ConsumerCases.Any(c => !profiles.Contains(c.Profile) || !Engines.Contains(c.Engine) ||
                c.ExpectedReadiness != "NO_POSITIVE_READINESS_INFERRED" || c.Inputs == null || string.IsNullOrEmpty(c.Expected)))
            throw new InvalidDataException("dataset_case");
        return data;
    }

    // These invoke the actual typed domain policies. Outputs are observations,
    // never a successful operational readiness or production legal activation.
    public string Evaluate(GoldenConsumerCase c)
    {
        string? V(string key) => c.Inputs.GetValueOrDefault(key);
        DateOnly? Date(string key) => V(key) is { } v ? DateOnly.ParseExact(v, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) : null;
        var date = DateOnly.FromDateTime(ReferenceTime.UtcDateTime);
        var tenant = Guid.Parse("11111111-1111-1111-1111-111111111111");
        var vessel = Guid.Parse("22222222-2222-2222-2222-222222222222");
        var asset = Guid.Parse("33333333-3333-3333-3333-333333333333");
        switch (c.Engine) {
            case "Applicability":
                return ApplicabilityObservationPolicy.Assess(Enum.Parse<PredicateKnowledge>(V("knowledge")!), Enum.Parse<SourceCurrency>(V("currency")!)).ToString();
            case "RequiredData":
                var value = V("state") == "Unknown" ? DraftValue.Unknown(DataKind.Text) : DraftValue.Missing(DataKind.Text);
                return new RequiredDataPolicy([new("evidence", DataKind.Text, true)]).Assess([new("evidence", value)]).BlockingFields.Single().Reason.ToString();
            case "CredentialDates":
                var facts = CredentialDeclarationDates.Evaluate(new(IssueDate: Date("issue"), ExpiryDate: Date("expiry")), date);
                return $"{facts.Expiry}/{facts.Order}";
            case "Freshness":
                return SourceFreshnessPolicy.Assess(V("observed") is { } stamp ? DateTimeOffset.Parse(stamp, System.Globalization.CultureInfo.InvariantCulture) : null,
                    ReferenceTime, V("maxAge") is { } seconds ? TimeSpan.FromSeconds(int.Parse(seconds, System.Globalization.CultureInfo.InvariantCulture)) : null).ToString();
            case "AssetHierarchy":
                var parent = V("parent") == "self" ? asset : Guid.Parse("44444444-4444-4444-4444-444444444444");
                return EquipmentHierarchyPolicy.Validate(tenant, vessel, [new(tenant, vessel, asset, parent)]).ToString();
            case "Maintenance":
                var due = MaintenanceCalculation.Evaluate(new(TriggerType: V("trigger")), ReferenceTime);
                return $"{due.State}/{due.Reason}";
            case "OfflineConflict":
                var binding = new OfflineReplayBinding(tenant, asset, vessel, asset, asset);
                var command = new OfflineCapturedCommand(asset, binding, "synthetic-material", V("base"), "synthetic-v1", ["field"], ReferenceTime);
                var policy = new OfflineOperationPolicy("synthetic-material", new HashSet<string> { "field" }, true, false, false);
                var server = new OfflineReplayServerContext(binding, true, true, true, "synthetic-v1", ReferenceTime.AddDays(-1), ReferenceTime.AddDays(1), ReferenceTime, policy, V("current"));
                return OfflineReplayPolicy.Evaluate(command, server, new string('a', 64)).ToString();
            default: throw new InvalidDataException("unsupported_consumer");
        }
    }
}
