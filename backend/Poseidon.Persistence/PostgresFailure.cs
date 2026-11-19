using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Poseidon.Persistence;

// Npgsql's nonretrying execution strategy can wrap a transient DbUpdateException.
// Inspect only provider error types/codes; arbitrary InvalidOperationExceptions stay errors.
public static class PostgresFailure
{
    public static PostgresException? Provider(Exception error) => error is PostgresException postgres ? postgres
        : error.InnerException is { } inner ? Provider(inner) : null;
    public static bool IsSerialization(Exception error) => Provider(error)?.SqlState is "40001" or "40P01";
    // Concurrency-shaped conflicts: serialization/deadlock or a racing unique key.
    public static bool IsConflict(Exception error) => IsSerialization(error) || Provider(error)?.SqlState is "23505";
    // Database state-rule rejections raised by guard triggers/check constraints (55000 object not in
    // prerequisite state, 23514 check violation). Never classified as transient/unavailable.
    public static bool IsStateRule(Exception error) => Provider(error)?.SqlState is "55000" or "23514";
    public static bool IsDatabase(Exception error) => error is DbUpdateException or NpgsqlException || IsSerialization(error);
}
