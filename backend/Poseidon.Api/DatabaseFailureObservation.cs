using Poseidon.Persistence;

namespace Poseidon.Api;

internal static class DatabaseFailureObservation
{
    internal static void Record(HttpContext http, Exception error)
    {
        var provider = PostgresFailure.Provider(error);
        // Schema identifiers only. Never include provider messages, details, SQL or values.
        var constraint = provider?.ConstraintName;
        if (constraint is not { Length: > 0 and <= 128 } || constraint.Any(c => !char.IsAsciiLetterOrDigit(c) && c != '_')) constraint = "unavailable";
        http.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Poseidon.DatabaseBoundary")
            .LogWarning("Database command rejected. SQL state {SqlState}; constraint {Constraint}; correlation {CorrelationId}",
                provider?.SqlState ?? "unavailable", constraint, http.TraceIdentifier);
    }
}
