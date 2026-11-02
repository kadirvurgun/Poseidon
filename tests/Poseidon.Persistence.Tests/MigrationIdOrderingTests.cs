using System.Globalization;
using System.Reflection;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Poseidon.DbMigrator;
using Xunit;

namespace Poseidon.Persistence.Tests;

// DEV-005 / T-S0-005: append new schema changes after retained source migrations.
[Trait("Category", "Unit")]
public sealed class MigrationIdOrderingTests
{
    [Fact]
    public void Future_history_is_appended_and_names_round_trip()
    {
        var generator = new AppendOnlyMigrationIds(new History("29990101000000_Previous"));
        var id = generator.GenerateId("Capacity_With_Underscore");
        Assert.Equal("29990101000001_Capacity_With_Underscore", id);
        Assert.True(generator.IsValidId(id));
        Assert.Equal("Capacity_With_Underscore", generator.GetName(id));
    }

    [Fact]
    public void Concurrent_requests_never_reuse_an_id_or_precede_history()
    {
        var generator = new AppendOnlyMigrationIds(new History("29990101000000_Previous"));
        var ids = new string[32];
        Parallel.For(0, ids.Length, i => ids[i] = generator.GenerateId("Change"));
        Assert.Equal(ids.Length, ids.Distinct(StringComparer.Ordinal).Count());
        Assert.Equal(Enumerable.Range(1, 32).Select(i => 29990101000000L + i),
            ids.Select(id => long.Parse(id[..14], CultureInfo.InvariantCulture)).Order());
    }

    [Theory]
    [InlineData("")]
    [InlineData("123_bad")]
    [InlineData("2999010100000x_Name")]
    [InlineData("29990101000000_")]
    public void Malformed_ids_are_rejected(string id)
    {
        var generator = new AppendOnlyMigrationIds(new History(id));
        Assert.False(generator.IsValidId(id));
        Assert.Throws<ArgumentException>(() => generator.GetName(id));
    }

    [Fact]
    public void Fixed_width_capacity_cannot_silently_overflow()
    {
        var generator = new AppendOnlyMigrationIds(new History("99999999999999_Previous"));
        Assert.Throws<InvalidOperationException>(() => generator.GenerateId("Change"));
    }

    private sealed class History(params string[] ids) : IMigrationsAssembly
    {
        public IReadOnlyDictionary<string, TypeInfo> Migrations { get; } = ids.ToDictionary(
            id => id, _ => typeof(EmptyMigration).GetTypeInfo(), StringComparer.Ordinal);
        public ModelSnapshot? ModelSnapshot => null;
        public string? FindMigrationId(string nameOrId) => throw new NotSupportedException();
        public Assembly Assembly => typeof(History).Assembly;
        public Migration CreateMigration(TypeInfo migrationClass, string activeProvider) => throw new NotSupportedException();
    }

    private sealed class EmptyMigration : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder) { }
        protected override void Down(MigrationBuilder migrationBuilder) { }
    }
}
