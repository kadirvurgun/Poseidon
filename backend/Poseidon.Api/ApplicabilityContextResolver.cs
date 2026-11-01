using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

// The request supplies an object identity, never profile facts or provenance.
internal static class ApplicabilityContextResolver
{
    internal static async Task<(long? Revision, SortedDictionary<string, ContextFact> Facts)> Resolve(PoseidonDbContext db, Vessel vessel, DateTimeOffset asOf, CancellationToken ct)
    {
        var facts = new SortedDictionary<string, ContextFact>(StringComparer.Ordinal);
        foreach (var field in ApplicabilityEngine.Fields)
            facts[field.Key] = new(field.Value, "Unknown", JsonSerializer.SerializeToElement<object?>(null), "AdapterUnavailable", ApplicabilityEngine.AdapterVersion);
        facts["context.mode"] = new(RuleFieldType.Text, "Known", JsonSerializer.SerializeToElement(vessel.Kind == VesselKind.Tug ? "TUG" : "SHIP"), "ServerVessel", vessel.Version.ToString(System.Globalization.CultureInfo.InvariantCulture));
        facts["evaluationDate"] = new(RuleFieldType.Date, "Known", JsonSerializer.SerializeToElement(asOf.UtcDateTime.ToString("yyyy-MM-dd")), "ServerAsOf", ApplicabilityEngine.AdapterVersion);
        var profile = await db.RegulatoryDraftRevisions.AsNoTracking().Where(x => x.VesselId == vessel.Id && x.SavedAt <= asOf).OrderByDescending(x => x.Revision).FirstOrDefaultAsync(ct);
        if (profile == null) return (null, facts);
        var content = RegulatoryDraftStorage.Deserialize(profile.ContentJson);
        void Set(string name, string state, string? value) => facts[name] = new(ApplicabilityEngine.Fields[name], state == "Known" ? "Unverified" : state,
            JsonSerializer.SerializeToElement(value), profile.Provenance, profile.Revision.ToString(System.Globalization.CultureInfo.InvariantCulture), profile.SavedAt);
        Set("vessel.flag", content.Flag.State, content.Flag.Value); Set("vessel.type", content.ShipType.State, content.ShipType.Value);
        Set("vessel.gt", content.Gt.State, content.Gt.Value); Set("vessel.buildDate", content.BuildDate.State, content.BuildDate.Value);
        Set("vessel.classSociety", content.ClassSociety.State, content.ClassSociety.Value); Set("vessel.tradingArea", content.TradingArea.State, content.TradingArea.Value);
        Set("cargo.type", content.CargoContext.State, content.CargoContext.Value);
        // Class-pack, manning, eligibility, equipment and operational authority are
        // separate owning adapters; a declaration/name never manufactures them.
        return (profile.Revision, facts);
    }
}
