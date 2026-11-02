namespace Poseidon.Domain;

// CREW-ROSTER-01: incomplete periods remain drafts. End is exclusive; no readiness inference.
public static class CrewPeriod
{
    public static bool Valid(DateOnly? from, DateOnly? to) => to == null || from != null && to > from;
    public static bool Overlaps(DateOnly? from, DateOnly? to, DateOnly? otherFrom, DateOnly? otherTo) =>
        from != null && otherFrom != null && (otherTo == null || from < otherTo) && (to == null || otherFrom < to);
}
