using Microsoft.EntityFrameworkCore;
using Xunit;
namespace Poseidon.Persistence.Tests;

public sealed class ErpBoundaryArchitectureTests
{
    [Fact]
    public void Actual_relational_model_has_no_PO_AP_AR_payroll_or_finance_ledger_aggregate_tables() {
        var options = new DbContextOptionsBuilder<PoseidonDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=synthetic_contract_model;Username=synthetic").Options;
        using var db = new PoseidonDbContext(options, Guid.NewGuid(), ActorContext.User(Guid.NewGuid(), "synthetic-contract-model"));
        var forbidden = new[] { "PURCHASEORDER", "REQUESTFORQUOTATION", "ACCOUNTSPAYABLE", "ACCOUNTPAYABLE", "ACCOUNTSRECEIVABLE",
            "ACCOUNTRECEIVABLE", "PAYROLL", "GENERALLEDGER", "FINANCELEDGER", "FINANCIALLEDGER", "CHARTERPARTY" };
        var tables = db.Model.GetEntityTypes().Select(t => t.GetTableName()).Where(t => t != null).Select(t => t!.Replace("_", "", StringComparison.Ordinal).ToUpperInvariant()).Distinct().ToArray();
        Assert.NotEmpty(tables);
        Assert.DoesNotContain(tables, table => forbidden.Any(prefix => table.StartsWith(prefix, StringComparison.Ordinal)));
        Assert.Empty(db.ChangeTracker.Entries());
        // Model metadata only; never connects, creates or modifies a database.
        // This catches canonical aliases; independent semantic schema review remains required.
    }
}
