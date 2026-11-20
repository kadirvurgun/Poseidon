using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Poseidon.Persistence.Seeding;
using Xunit;

namespace Poseidon.Persistence.Tests;

// S10 WP-A DEV-064/113: audit pack manifests/items, mapping versions, item revisions, field evidence and receipts are
// insert-only in the audited SaveChanges path; a staged import is decided once; a finding is never closed or verified
// and gains its action link once. The model carries the CHECKs that keep the label, OPEN/UNVERIFIED and SoD in the DB.
[Trait("Category", "Unit")]
[Trait("Requirement", "DEV-064")]
[Trait("Requirement", "DEV-113")]
public sealed class AssuranceStorageGuardTests
{
    private static readonly Guid Tenant = Guid.Parse("00000000-0000-4000-8000-00000000a064");
    private static PoseidonDbContext Context() => new(
        SeedImporter.Options("Host=127.0.0.1;Port=1;Database=unreachable_guard_test;Username=unused"), Tenant, new ActorContext("user", "User", "request"));

    [Fact]
    public void Packs_mappings_item_revisions_evidence_and_receipts_cannot_be_rewritten_or_deleted()
    {
        using var db = Context();
        void Rejected(object entity, Action<object> change)
        {
            db.Attach(entity); change(entity);
            Assert.Throws<InvalidOperationException>(() => db.SaveChanges());
            db.ChangeTracker.Clear();
        }
        Rejected(new AuditPackManifest { TenantId = Tenant, Id = Guid.NewGuid(), ItemCount = 3 }, e => ((AuditPackManifest)e).ItemCount = 2);
        Rejected(new AuditPackItem { TenantId = Tenant, PackId = Guid.NewGuid(), Ordinal = 1, Kind = "Evidence", State = "Superseded" }, e => db.Remove(e));
        Rejected(new AuditPackItem { TenantId = Tenant, PackId = Guid.NewGuid(), Ordinal = 2, Kind = "Evidence", State = "Superseded" }, e => ((AuditPackItem)e).State = "Current");
        Rejected(new AssuranceImportMapping { TenantId = Tenant, Id = Guid.NewGuid(), Key = "k", Version = 1 }, e => ((AssuranceImportMapping)e).BindingsJson = "[]x");
        Rejected(new AssuranceInspectionItem { TenantId = Tenant, InspectionId = Guid.NewGuid(), QuestionRef = "2.1.1", Revision = 1 }, e => ((AssuranceInspectionItem)e).ContentJson = "{\"tampered\":true}");
        Rejected(new AssuranceEvidence { TenantId = Tenant, Id = Guid.NewGuid() }, e => db.Remove(e));
        Rejected(new AssuranceReceipt { TenantId = Tenant, ActorId = Guid.NewGuid(), Key = Guid.NewGuid().ToString(), Action = "AuditPackCreate", Revision = 1 }, e => ((AssuranceReceipt)e).Revision = 2);
        Assert.Empty(db.ChangeTracker.Entries<AuditEvent>());
    }

    [Fact]
    public void A_decided_import_is_final_and_a_finding_is_never_closed_and_links_its_action_once()
    {
        using var db = Context();
        var decided = new AssuranceImport { TenantId = Tenant, Id = Guid.NewGuid(), State = "ACCEPTED", DecidedBy = Guid.NewGuid() };
        db.Attach(decided); decided.State = "REJECTED";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges()); db.ChangeTracker.Clear();
        var pending = new AssuranceImport { TenantId = Tenant, Id = Guid.NewGuid(), State = "PENDING_REVIEW" };
        db.Attach(pending); pending.ItemsJson = "[{\"tampered\":true}]";
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges()); db.ChangeTracker.Clear();
        var finding = new AssuranceFinding { TenantId = Tenant, Id = Guid.NewGuid(), Revision = 1 };
        db.Attach(finding); finding.State = "CLOSED"; finding.Revision = 2;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges()); db.ChangeTracker.Clear();
        var verified = new AssuranceFinding { TenantId = Tenant, Id = Guid.NewGuid(), Revision = 1 };
        db.Attach(verified); verified.Verification = "VERIFIED"; verified.Revision = 2;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges()); db.ChangeTracker.Clear();
        var linked = new AssuranceFinding { TenantId = Tenant, Id = Guid.NewGuid(), Revision = 2, ActionId = Guid.NewGuid(), ActionLinkState = "LINKED" };
        db.Attach(linked); linked.ActionId = Guid.NewGuid(); linked.Revision = 3;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges()); db.ChangeTracker.Clear();
        var inspection = new AssuranceInspection { TenantId = Tenant, Id = Guid.NewGuid(), Revision = 1, ReferenceNo = "FICTIONAL-1" };
        db.Attach(inspection); inspection.ReferenceNo = "FICTIONAL-2"; inspection.Revision = 2;
        Assert.Throws<InvalidOperationException>(() => db.SaveChanges()); db.ChangeTracker.Clear();
    }

    [Fact]
    public void Model_carries_the_label_open_unverified_and_separation_of_duties_checks()
    {
        using var db = Context();
        var model = db.GetService<Microsoft.EntityFrameworkCore.Metadata.IDesignTimeModel>().Model;
        string Check(Type type, string name) => model.FindEntityType(type)!.GetCheckConstraints().Single(c => c.Name == name).Sql;
        Assert.Contains("SYSTEM_COMPILATION_NOT_OFFICIAL_RECORD", Check(typeof(AuditPackManifest), "CK_AuditPackManifest_Label"));
        Assert.Contains("\"State\"='OPEN' AND \"Verification\"='UNVERIFIED'", Check(typeof(AssuranceFinding), "CK_AssuranceFinding_Open"));
        Assert.Contains("\"Verification\"='UNVERIFIED'", Check(typeof(AssuranceEvidence), "CK_AssuranceEvidence_Unverified"));
        Assert.Contains("\"DecidedBy\"<>\"ImportedBy\"", Check(typeof(AssuranceImport), "CK_AssuranceImport_Decision"));
        Assert.Contains("(\"Classification\"<>'NOT_TANKER')", Check(typeof(AssuranceInspection), "CK_AssuranceInspection_State"));
        Assert.Equal("\"State\"='PENDING_REVIEW'", model.FindEntityType(typeof(AssuranceImport))!.GetIndexes().Single(i => i.GetDatabaseName() == "IX_AssuranceImports_OnePending").GetFilter());
    }
}
