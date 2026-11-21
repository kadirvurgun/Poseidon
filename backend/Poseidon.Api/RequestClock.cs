namespace Poseidon.Api;

// The only request-path clock: the DI-registered TimeProvider (system clock registered once in Startup).
// WallClockGuardTests forbids direct wall-clock reads in backend product code.
internal static class RequestClock
{
    internal static DateTimeOffset UtcNow(this HttpContext h) => h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
}
