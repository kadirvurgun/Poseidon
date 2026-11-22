using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

// USER-ADMIN-03 / UA3-AC-01, UA3-AC-04: the outbox row follows its state machine and never keeps the protected payload
// in a terminal state; the tenant invitation lifetime stays within 54.01 account.invite_hours. Guards run before any
// database round trip.
[Trait("Category", "Unit")]
[Trait("Requirement", "USER-ADMIN-03")]
public sealed class InvitationDeliveryStorageGuardTests
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-4000-8000-0000000ad003");
    private static PoseidonDbContext Context() => new(
        SeedImporter.Options("Host=127.0.0.1;Port=1;Database=unreachable_guard_test;Username=unused"), Tenant, new ActorContext("system:invitation-delivery", "System", "test"));
    private static InvitationDelivery Queued() => new() { TenantId = Tenant, Id = Guid.NewGuid(), InvitationId = Guid.NewGuid(), MembershipId = Guid.NewGuid(),
        State = "QUEUED", Attempts = 0, CreatedAt = DateTimeOffset.UnixEpoch, NextAttemptAt = DateTimeOffset.UnixEpoch, ProtectedPayload = "CfDJ8-synthetic-ciphertext",
        CreatedBy = Guid.NewGuid().ToString("D") };

    [Theory]
    [InlineData("SENT")]
    [InlineData("FAILED")]
    [InlineData("CANCELLED")]
    [InlineData("DELIVERED")]
    public void A_delivery_starts_queued_or_not_configured(string state)
    {
        using var db = Context();
        var row = Queued(); row.State = state;
        if (state != "DELIVERED") { row.ProtectedPayload = null; row.CompletedAt = DateTimeOffset.UnixEpoch; }
        db.Add(row);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void A_queued_delivery_needs_its_payload_and_a_terminal_one_never_keeps_it()
    {
        using var db = Context();
        var empty = Queued(); empty.ProtectedPayload = null;
        db.Add(empty);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var row = Queued();
        db.Attach(row);
        row.State = "SENT"; row.CompletedAt = DateTimeOffset.UnixEpoch; row.Attempts = 1; // payload left in place
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var notConfigured = Queued(); notConfigured.State = "NOT_CONFIGURED"; notConfigured.CompletedAt = DateTimeOffset.UnixEpoch; // payload kept
        db.Add(notConfigured);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void A_terminal_delivery_never_changes_and_history_is_never_removed()
    {
        using var db = Context();
        var sent = Queued(); sent.State = "SENT"; sent.ProtectedPayload = null; sent.CompletedAt = DateTimeOffset.UnixEpoch; sent.Attempts = 1;
        db.Attach(sent);
        sent.State = "QUEUED"; sent.ProtectedPayload = "CfDJ8-replayed"; sent.CompletedAt = null;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var removed = Queued();
        db.Attach(removed); db.Remove(removed);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void A_queued_delivery_keeps_its_payload_invitation_and_attempt_history()
    {
        using var db = Context();
        var swapped = Queued(); db.Attach(swapped); swapped.ProtectedPayload = "CfDJ8-other";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var moved = Queued(); db.Attach(moved); moved.InvitationId = Guid.NewGuid();
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var rewound = Queued(); rewound.Attempts = 2; db.Attach(rewound); rewound.Attempts = 1;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var tooMany = Queued(); tooMany.Attempts = 5; db.Attach(tooMany); tooMany.Attempts = 6;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
        db.ChangeTracker.Clear();
        var freeText = Queued(); db.Attach(freeText); freeText.LastErrorCode = "550 mailbox invitee@example.invalid unavailable";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Theory]
    [InlineData(0)]
    [InlineData(169)]
    [InlineData(-24)]
    public void The_tenant_lifetime_stays_within_1_to_168_hours(int hours)
    {
        using var db = Context();
        db.Add(new TenantInvitationSettings { TenantId = Tenant, LifetimeHours = hours, UpdatedAt = DateTimeOffset.UnixEpoch, UpdatedBy = Guid.NewGuid().ToString("D") });
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void The_tenant_lifetime_is_changed_never_removed()
    {
        using var db = Context();
        var row = new TenantInvitationSettings { TenantId = Tenant, LifetimeHours = 48, UpdatedAt = DateTimeOffset.UnixEpoch, UpdatedBy = Guid.NewGuid().ToString("D") };
        db.Attach(row); db.Remove(row);
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
    }

    [Fact]
    public void A_delivery_in_another_tenant_is_refused()
    {
        using var db = Context();
        var foreign = Queued(); foreign.TenantId = Guid.Parse("00000000-0000-4000-8000-0000000ad004");
        db.Add(foreign);
        Assert.ThrowsAny<Exception>(() => db.SaveChanges());
    }
}
