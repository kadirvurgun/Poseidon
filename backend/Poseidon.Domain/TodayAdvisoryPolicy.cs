namespace Poseidon.Domain;

// TW-01: observations about a draft, never applicability, mandatory policy or safety clearance.
public sealed record DraftObservation(int Known, int Missing, int Unknown, string Reason);
public static class TodayAdvisoryPolicy
{
    public static DraftObservation Observe(RegulatoryDraftContent? content)
    {
        if (content == null) return new(0, 0, 0, "DraftAbsent");
        string[] states = [content.Imo.State, content.Flag.State, content.ShipType.State,
            content.ClassSociety.State, content.ClassNumber.State, content.Gt.State, content.Nt.State,
            content.Dwt.State, content.Loa.State, content.Breadth.State, content.Draft.State,
            content.BuildDate.State, content.TradingArea.State, content.CargoContext.State,
            content.Declaration.State, content.SourceReference.State, content.SourceObservedAt.State];
        return new(states.Count(x => x == "Known"), states.Count(x => x == "Missing"),
            states.Count(x => x == "Unknown"), "DraftUnverified");
    }
}
