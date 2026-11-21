namespace Poseidon.Api;

// One server clock for request-path code: the DI TimeProvider (System in production, replaceable in
// tests). Static wall-clock reads in Poseidon.Api are rejected by ApiClockGuardTests.
internal static class RequestClock
{
    internal static DateTimeOffset UtcNow(this HttpContext h) => h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow();
}
