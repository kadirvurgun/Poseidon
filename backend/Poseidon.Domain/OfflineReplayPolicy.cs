namespace Poseidon.Domain;

// Projections of authenticated server state, captured advisory metadata and a
// scoped stored receipt. None of these types grants authority by being constructed.
public sealed record OfflineReplayBinding(Guid TenantId, Guid ActorId, Guid VesselId, Guid DeviceId, Guid AggregateId)
{
    public override string ToString() => nameof(OfflineReplayBinding);
}
public sealed record OfflineCapturedCommand(Guid CommandId, OfflineReplayBinding Binding, string Operation,
    string? BaseVersion, string PolicyVersion, IReadOnlyList<string> Fields, DateTimeOffset CapturedAt)
{
    public override string ToString() => nameof(OfflineCapturedCommand);
}
public sealed record OfflineOperationPolicy(string Operation, IReadOnlySet<string> Fields,
    bool Material, bool AllowAdditiveMergeOnStaleBase, bool ProducesOperationalAuthority)
{
    public override string ToString() => nameof(OfflineOperationPolicy);
}
public sealed record OfflineReplayServerContext(OfflineReplayBinding Binding,
    bool CurrentReadAllowed, bool CurrentWriteAllowed, bool DeviceActive,
    string PolicyVersion, DateTimeOffset PolicyNotBefore, DateTimeOffset PolicyExpiresAt,
    DateTimeOffset ReceivedAt, OfflineOperationPolicy? OperationPolicy, string? CurrentAggregateVersion)
{
    public override string ToString() => nameof(OfflineReplayServerContext);
}
public sealed record OfflineReplayReceipt(Guid CommandId, OfflineReplayBinding Binding,
    string Operation, string? BaseVersion, string PayloadSha256)
{
    public override string ToString() => nameof(OfflineReplayReceipt);
}

public enum OfflineReplayDecision
{
    Denied,
    InvalidCommand,
    InvalidServerDigest,
    CommandReuseConflict,
    ReturnRecordedOutcome,
    PolicyUnavailable,
    PolicyNotYetActive,
    PolicyExpired,
    PolicyChanged,
    UnsupportedCommand,
    UnsupportedFields,
    AuthorityOperationForbidden,
    MissingBaseVersion,
    VersionConflict,
    MaterialConflict,
    EligibleForTransactionalRevalidation
}

// 15.01 paragraphs228–245: this is a preflight decision, not a receipt store,
// concurrency lock, domain validation, actual execution or operational approval.
public static class OfflineReplayPolicy
{
    public static OfflineReplayDecision Evaluate(OfflineCapturedCommand command,
        OfflineReplayServerContext server, string serverComputedPayloadSha256, OfflineReplayReceipt? receipt = null)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(server);
        // Authenticate/authorize and constrain DB receipt queries BEFORE calling.
        // Recheck binding before observing whether a matching receipt exists.
        if (!server.CurrentReadAllowed || !server.DeviceActive || !ValidBinding(server.Binding) || command.Binding != server.Binding)
            return OfflineReplayDecision.Denied;
        if (command.CommandId == Guid.Empty || string.IsNullOrWhiteSpace(command.Operation) ||
            string.IsNullOrWhiteSpace(command.PolicyVersion) || command.Fields is null ||
            command.Fields.Any(string.IsNullOrWhiteSpace) || command.Fields.Distinct(StringComparer.Ordinal).Count() != command.Fields.Count)
            return OfflineReplayDecision.InvalidCommand;
        if (!Digest(serverComputedPayloadSha256)) return OfflineReplayDecision.InvalidServerDigest;
        if (receipt is not null)
        {
            if (receipt.CommandId != command.CommandId || receipt.Binding != command.Binding ||
                !string.Equals(receipt.Operation, command.Operation, StringComparison.Ordinal) ||
                !string.Equals(receipt.BaseVersion, command.BaseVersion, StringComparison.Ordinal) ||
                !Digest(receipt.PayloadSha256) || !string.Equals(receipt.PayloadSha256, serverComputedPayloadSha256, StringComparison.OrdinalIgnoreCase))
                return OfflineReplayDecision.CommandReuseConflict;
            // This recovers a HISTORIC outcome under current read access. It does
            // not rerun a write, restore revoked write permission or return READY.
            return OfflineReplayDecision.ReturnRecordedOutcome;
        }
        if (!server.CurrentWriteAllowed) return OfflineReplayDecision.Denied;
        var policy = server.OperationPolicy;
        if (string.IsNullOrWhiteSpace(server.PolicyVersion) || policy is null || policy.Fields is null ||
            server.PolicyExpiresAt <= server.PolicyNotBefore || policy.Material && policy.AllowAdditiveMergeOnStaleBase)
            return OfflineReplayDecision.PolicyUnavailable;
        if (server.ReceivedAt < server.PolicyNotBefore) return OfflineReplayDecision.PolicyNotYetActive;
        if (server.ReceivedAt >= server.PolicyExpiresAt) return OfflineReplayDecision.PolicyExpired;
        if (!string.Equals(command.PolicyVersion, server.PolicyVersion, StringComparison.Ordinal))
            return OfflineReplayDecision.PolicyChanged;
        if (!string.Equals(command.Operation, policy.Operation, StringComparison.Ordinal))
            return OfflineReplayDecision.UnsupportedCommand;
        if (policy.ProducesOperationalAuthority) return OfflineReplayDecision.AuthorityOperationForbidden;
        // Enforce ordinal matching even when a supplied set uses a permissive comparer.
        var allowedFields = new HashSet<string>(policy.Fields, StringComparer.Ordinal);
        if (command.Fields.Any(field => !allowedFields.Contains(field))) return OfflineReplayDecision.UnsupportedFields;
        if (policy.Material && string.IsNullOrWhiteSpace(command.BaseVersion)) return OfflineReplayDecision.MissingBaseVersion;
        if (command.BaseVersion is not null && !string.Equals(command.BaseVersion, server.CurrentAggregateVersion, StringComparison.Ordinal))
        {
            if (policy.Material) return OfflineReplayDecision.MaterialConflict;
            if (!policy.AllowAdditiveMergeOnStaleBase) return OfflineReplayDecision.VersionConflict;
        }
        // CapturedAt is untrusted provenance, never the policy clock. The handler
        // must now revalidate current domain state inside the real transaction and
        // atomically save mutation+receipt through the attributed audited path.
        return OfflineReplayDecision.EligibleForTransactionalRevalidation;
    }

    private static bool ValidBinding(OfflineReplayBinding? binding) => binding is not null &&
        binding.TenantId != Guid.Empty && binding.ActorId != Guid.Empty && binding.VesselId != Guid.Empty &&
        binding.DeviceId != Guid.Empty && binding.AggregateId != Guid.Empty;
    private static bool Digest(string? digest) => digest is { Length: 64 } && digest.All(c =>
        c is >= '0' and <= '9' or >= 'a' and <= 'f' or >= 'A' and <= 'F');
}
