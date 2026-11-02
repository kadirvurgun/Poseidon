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
    public static bool IsDatabase(Exception error) => error is DbUpdateException or NpgsqlException || IsSerialization(error);
}
