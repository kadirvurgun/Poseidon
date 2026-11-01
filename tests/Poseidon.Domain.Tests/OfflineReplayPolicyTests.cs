using Poseidon.Domain;

namespace Poseidon.Domain.Tests;

public sealed class OfflineReplayPolicyTests
{
    private static readonly DateTimeOffset Received = new(2030, 1, 1, 12, 0, 0, TimeSpan.Zero);
    private static readonly OfflineReplayBinding Binding = new(
        Guid.Parse("aaaa0000-0000-0000-0000-000000000001"), Guid.Parse("aaaa0000-0000-0000-0000-000000000002"),
        Guid.Parse("aaaa0000-0000-0000-0000-000000000003"), Guid.Parse("aaaa0000-0000-0000-0000-000000000004"),
        Guid.Parse("aaaa0000-0000-0000-0000-000000000005"));
    private static readonly string Digest = new('a', 64);
    private static OfflineCapturedCommand Command() => new(
        Guid.Parse("aaaa0000-0000-0000-0000-000000000006"), Binding, "synthetic.draft.edit", "version-1",
        "synthetic-policy-1", ["watchGroup"], Received.AddHours(-1));
    private static OfflineReplayServerContext Server() => new(Binding, true, true, true, "synthetic-policy-1",
        Received.AddDays(-1), Received.AddDays(1), Received,
        new("synthetic.draft.edit", new HashSet<string>(["watchGroup"], StringComparer.Ordinal), true, false, false), "version-1");
    private static OfflineReplayReceipt Receipt() => new(Command().CommandId, Binding, Command().Operation, "version-1", Digest);

    [Fact]
    public void Permitted_draft_is_only_eligible_for_transactional_revalidation_not_already_executed()
    {
        Assert.Equal(OfflineReplayDecision.EligibleForTransactionalRevalidation, OfflineReplayPolicy.Evaluate(Command(), Server(), Digest));
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("actor")]
    [InlineData("vessel")]
    [InlineData("device")]
    [InlineData("aggregate")]
    public void Cross_binding_command_is_denied_even_if_a_receipt_is_supplied(string part)
    {
        var other = Guid.Parse("bbbb0000-0000-0000-0000-000000000001");
        var changed = part switch {
            "tenant" => Binding with { TenantId = other }, "actor" => Binding with { ActorId = other },
            "vessel" => Binding with { VesselId = other }, "device" => Binding with { DeviceId = other },
            _ => Binding with { AggregateId = other }
        };
        Assert.Equal(OfflineReplayDecision.Denied, OfflineReplayPolicy.Evaluate(Command() with { Binding = changed }, Server(), Digest, Receipt()));
    }

    [Fact]
    public void Current_read_revocation_prevents_receipt_recovery_and_mutation()
    {
        Assert.Equal(OfflineReplayDecision.Denied, OfflineReplayPolicy.Evaluate(Command(), Server() with { CurrentReadAllowed = false }, Digest, Receipt()));
    }

    [Fact]
    public void Revoked_device_cannot_replay_or_read_a_matching_receipt()
    {
        Assert.Equal(OfflineReplayDecision.Denied, OfflineReplayPolicy.Evaluate(Command(), Server() with { DeviceActive = false }, Digest, Receipt()));
    }

    [Fact]
    public void Revoked_write_prevents_new_execution()
    {
        Assert.Equal(OfflineReplayDecision.Denied, OfflineReplayPolicy.Evaluate(Command(), Server() with { CurrentWriteAllowed = false }, Digest));
    }

    [Fact]
    public void Current_read_can_recover_historic_outcome_after_write_revocation_without_executing_again()
    {
        var server = Server() with { CurrentWriteAllowed = false, CurrentAggregateVersion = "version-99", PolicyExpiresAt = Received.AddHours(-1) };
        Assert.Equal(OfflineReplayDecision.ReturnRecordedOutcome, OfflineReplayPolicy.Evaluate(Command(), server, Digest, Receipt()));
    }

    [Fact]
    public void Same_command_uuid_with_another_server_computed_payload_is_a_reuse_conflict()
    {
        Assert.Equal(OfflineReplayDecision.CommandReuseConflict, OfflineReplayPolicy.Evaluate(Command(), Server(), new('b', 64), Receipt()));
    }

    [Fact]
    public void Reusing_uuid_with_another_base_version_does_not_replay_an_unrelated_result()
    {
        Assert.Equal(OfflineReplayDecision.CommandReuseConflict, OfflineReplayPolicy.Evaluate(Command() with { BaseVersion = "version-2" }, Server(), Digest, Receipt()));
    }

    [Fact]
    public void Receipt_for_another_operation_cannot_be_reused()
    {
        Assert.Equal(OfflineReplayDecision.CommandReuseConflict, OfflineReplayPolicy.Evaluate(Command(), Server(), Digest, Receipt() with { Operation = "synthetic.other" }));
    }

    [Theory]
    [InlineData("")]
    [InlineData("client-supplied-not-a-server-digest")]
    [InlineData("gggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggggg")]
    public void Invalid_server_digest_cannot_authorize_execution(string digest)
    {
        Assert.Equal(OfflineReplayDecision.InvalidServerDigest, OfflineReplayPolicy.Evaluate(Command(), Server(), digest));
    }

    [Fact]
    public void Digest_hexadecimal_casing_does_not_change_the_recorded_bytes()
    {
        Assert.Equal(OfflineReplayDecision.ReturnRecordedOutcome, OfflineReplayPolicy.Evaluate(Command(), Server(), Digest.ToUpperInvariant(), Receipt()));
    }

    [Fact]
    public void Unconfigured_policy_never_defaults_to_allowed()
    {
        Assert.Equal(OfflineReplayDecision.PolicyUnavailable, OfflineReplayPolicy.Evaluate(Command(), Server() with { OperationPolicy = null }, Digest));
    }

    [Fact]
    public void Changed_policy_requires_explicit_renewal_instead_of_silent_reinterpretation()
    {
        Assert.Equal(OfflineReplayDecision.PolicyChanged, OfflineReplayPolicy.Evaluate(Command(), Server() with { PolicyVersion = "synthetic-policy-2" }, Digest));
    }

    [Fact]
    public void Exact_expiry_is_expired_even_when_capture_timestamp_claims_to_be_earlier()
    {
        var server = Server() with { PolicyExpiresAt = Received };
        Assert.Equal(OfflineReplayDecision.PolicyExpired, OfflineReplayPolicy.Evaluate(Command() with { CapturedAt = Received.AddYears(-5) }, server, Digest));
    }

    [Fact]
    public void Future_policy_is_not_activated_by_a_forged_future_capture_timestamp()
    {
        var server = Server() with { PolicyNotBefore = Received.AddHours(1) };
        Assert.Equal(OfflineReplayDecision.PolicyNotYetActive, OfflineReplayPolicy.Evaluate(Command() with { CapturedAt = Received.AddYears(5) }, server, Digest));
    }

    [Fact]
    public void Equivalent_server_instants_with_different_offsets_have_the_same_policy_boundary()
    {
        var server = Server() with { ReceivedAt = Received.ToOffset(TimeSpan.FromHours(3)), PolicyExpiresAt = Received };
        Assert.Equal(OfflineReplayDecision.PolicyExpired, OfflineReplayPolicy.Evaluate(Command(), server, Digest));
    }

    [Fact]
    public void Unknown_operation_cannot_borrow_another_commands_field_policy()
    {
        Assert.Equal(OfflineReplayDecision.UnsupportedCommand, OfflineReplayPolicy.Evaluate(Command() with { Operation = "synthetic.other" }, Server(), Digest));
    }

    [Fact]
    public void Authority_producing_operation_is_forbidden_even_under_an_explicit_policy()
    {
        var server = Server(); server = server with { OperationPolicy = server.OperationPolicy! with { ProducesOperationalAuthority = true } };
        Assert.Equal(OfflineReplayDecision.AuthorityOperationForbidden, OfflineReplayPolicy.Evaluate(Command(), server, Digest));
    }

    [Fact]
    public void Unknown_sensitive_fields_are_denied_without_a_raw_value_diagnostic()
    {
        Assert.Equal(OfflineReplayDecision.UnsupportedFields, OfflineReplayPolicy.Evaluate(Command() with { Fields = ["private-health-field"] }, Server(), Digest));
    }

    [Fact]
    public void Permissive_set_comparer_cannot_make_field_authorization_case_insensitive()
    {
        var server = Server(); server = server with { OperationPolicy = server.OperationPolicy! with { Fields = new HashSet<string>(["watchGroup"], StringComparer.OrdinalIgnoreCase) } };
        Assert.Equal(OfflineReplayDecision.UnsupportedFields, OfflineReplayPolicy.Evaluate(Command() with { Fields = ["WATCHGROUP"] }, server, Digest));
    }

    [Fact]
    public void Ambiguous_duplicate_fields_are_rejected()
    {
        Assert.Equal(OfflineReplayDecision.InvalidCommand, OfflineReplayPolicy.Evaluate(Command() with { Fields = ["watchGroup", "watchGroup"] }, Server(), Digest));
    }

    [Fact]
    public void Material_command_requires_a_base_version()
    {
        Assert.Equal(OfflineReplayDecision.MissingBaseVersion, OfflineReplayPolicy.Evaluate(Command() with { BaseVersion = null }, Server(), Digest));
    }

    [Fact]
    public void Stale_material_version_requires_conflict_not_last_write_wins()
    {
        Assert.Equal(OfflineReplayDecision.MaterialConflict, OfflineReplayPolicy.Evaluate(Command(), Server() with { CurrentAggregateVersion = "version-2" }, Digest));
    }

    [Fact]
    public void Calling_a_command_non_material_does_not_automatically_enable_stale_merge()
    {
        var server = Server(); server = server with { CurrentAggregateVersion = "version-2", OperationPolicy = server.OperationPolicy! with { Material = false } };
        Assert.Equal(OfflineReplayDecision.VersionConflict, OfflineReplayPolicy.Evaluate(Command(), server, Digest));
    }

    [Fact]
    public void Explicit_non_material_additive_merge_still_requires_actual_domain_transactional_revalidation()
    {
        var server = Server(); server = server with { CurrentAggregateVersion = "version-2", OperationPolicy = server.OperationPolicy! with { Material = false, AllowAdditiveMergeOnStaleBase = true } };
        Assert.Equal(OfflineReplayDecision.EligibleForTransactionalRevalidation, OfflineReplayPolicy.Evaluate(Command(), server, Digest));
    }

    [Fact]
    public void Invalid_material_auto_merge_configuration_fails_closed()
    {
        var server = Server(); server = server with { OperationPolicy = server.OperationPolicy! with { AllowAdditiveMergeOnStaleBase = true } };
        Assert.Equal(OfflineReplayDecision.PolicyUnavailable, OfflineReplayPolicy.Evaluate(Command(), server, Digest));
    }

    [Fact]
    public void Diagnostic_formatters_do_not_dump_actor_ids_private_fields_or_digest_values()
    {
        Assert.Equal(nameof(OfflineCapturedCommand), Command().ToString());
        Assert.Equal(nameof(OfflineReplayServerContext), Server().ToString());
        Assert.Equal(nameof(OfflineReplayReceipt), Receipt().ToString());
        Assert.Equal(nameof(OfflineReplayBinding), Binding.ToString());
    }
}
