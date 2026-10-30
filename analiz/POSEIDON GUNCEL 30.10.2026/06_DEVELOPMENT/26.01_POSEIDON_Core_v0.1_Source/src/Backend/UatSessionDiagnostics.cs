using System.Globalization;
using Microsoft.AspNetCore.Authentication;

namespace Poseidon;

// Fixed vocabulary only. No identifiers, handles, cookies, claims, headers or exceptions.
public enum UatSessionEvent {
    PROCESS_STARTED, TICKET_CREATED, TICKET_EXPIRED, TICKET_MISSING, TICKET_REMOVED,
    LOGIN_CREATED, LOGIN_EXISTING_SESSION, LOGIN_CSRF_REJECTED,
    AUTH_401_COOKIE_PRESENT, AUTH_401_COOKIE_ABSENT, PRINCIPAL_REJECTED, LOGOUT_COMPLETED
}
internal static class UatSessionDiagnostics
{
    public static void Write(HttpContext context, UatSessionEvent code, AuthenticationProperties? properties = null)
    {
        if (!context.RequestServices.GetRequiredService<DevelopmentUat>().Enabled) return;
        Write(context.RequestServices.GetRequiredService<ILoggerFactory>().CreateLogger("Poseidon.UatSession"), code, properties);
    }
    public static void Write(ILogger logger, UatSessionEvent code, AuthenticationProperties? properties = null)
    {
        var now = DateTimeOffset.UtcNow;
        // Server ticket times only. -1 age indicates absent; remaining may be negative.
        var age = properties?.IssuedUtc is { } issued ? (int)Math.Clamp(Math.Floor((now-issued).TotalSeconds), 0, 86400) : -1;
        var remaining = properties?.ExpiresUtc is { } expires ? (int)Math.Clamp(Math.Floor((expires-now).TotalSeconds), -86400, 86400) : -86401;
        logger.LogInformation(new EventId(4812, "DevelopmentUatSession"),
            "POSEIDON_UAT_SESSION {Utc} {Code} AGE_SECONDS={Age} REMAINING_SECONDS={Remaining}",
            now.ToString("yyyy-MM-ddTHH:mm:ss.fffZ", CultureInfo.InvariantCulture), code, age, remaining);
    }
}
