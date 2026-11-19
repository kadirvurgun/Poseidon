using Poseidon.Persistence;

namespace Poseidon.Api;

// DEV-034 sync reconciliation accessors. Same ordinary write/scope/policy gates as the
// tug offline replay; no new authority and no receipt disclosure beyond replay recovery.
public static partial class TugEndpoints
{
    internal static bool SyncRights(HttpContext h, TrustedActor a, Guid vessel) => PlanWriteScoped(h, a, vessel);
    internal static bool SyncEnrollmentCurrent(HttpContext h, TugOfflineEnrollment e) => TugOfflineCurrent(h, e);
}
