using System.Globalization;

namespace Poseidon.Domain;

// DEV-098 officer_of_watch: the pinned crew assignment must have covered the
// deck log's own log_date, not the day the log is saved. Crew assignment
// periods are date-granular (From inclusive, To exclusive; DEV-047), so the
// watch period cannot be checked against them and is not inferred.
public static class DailyOfficerAssignment
{
    public static bool TryLogDate(IReadOnlyDictionary<string, DailyValue>? fields, out DateOnly date)
    {
        date = default;
        return fields != null && fields.TryGetValue("log_date", out var value) && value is { State: "Known", Value: { } text } &&
            DateOnly.TryParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);
    }

    // A missing start never counts as covering; an open end covers any later date.
    public static bool Covers(DateOnly? from, DateOnly? to, DateOnly logDate) =>
        from != null && from <= logDate && (to == null || logDate < to);
}
