using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Poseidon.Domain;

namespace Poseidon.Persistence;

// A single controlled exception to immutable source storage: exact bytes are sealed, not edited.
internal static class LegacyImportStorage
{
    internal const string Check = """
        ("LegacyProtectedAt" IS NULL AND "LegacyProtectionPolicyVersion" IS NULL AND "LegacyProtectionIdentityVersion" IS NULL AND "LegacyProtectionPurpose" IS NULL)
        OR ("Category" = 'crew' AND "LegacyProtectedAt" IS NOT NULL
        AND "LegacyProtectionPolicyVersion" IS NOT NULL AND "LegacyProtectionIdentityVersion" IS NOT NULL AND "LegacyProtectionPurpose" IS NOT NULL
        AND length("LegacyProtectionPolicyVersion") BETWEEN 1 AND 80 AND length("LegacyProtectionIdentityVersion") BETWEEN 1 AND 80
        AND length("LegacyProtectionPurpose") BETWEEN 1 AND 80
        AND "TableJson" LIKE 'protected-legacy-v1:%' AND "BindingsJson" = '[]' AND "SourceHash" = ''
        AND "SourceName" = 'name-withheld.xlsx' AND "SourceLocator" = 'protected-history')
        """;
    private static readonly string[] StorageFields = ["TableJson", "BindingsJson", "SourceHash", "SourceName", "SourceLocator",
        "LegacyProtectedAt", "LegacyProtectionPolicyVersion", "LegacyProtectionIdentityVersion", "LegacyProtectionPurpose"];
    internal static bool IsConversion(EntityEntry entry) => entry.Entity is ImportBatch b && entry.State == EntityState.Modified &&
        entry.Property(nameof(ImportBatch.LegacyProtectedAt)).OriginalValue == null && b.LegacyProtectedAt != null &&
        LegacyCrewImport.Convertible(b.Category, (string)entry.Property(nameof(ImportBatch.TableJson)).OriginalValue!, false) &&
        b.TableJson.StartsWith(LegacyCrewImport.Prefix, StringComparison.Ordinal) && b.BindingsJson == "[]" && b.SourceHash == "" &&
        b.SourceName == "name-withheld.xlsx" && b.SourceLocator == "protected-history" &&
        b.LegacyProtectionPolicyVersion?.Length is > 0 and <= 80 && b.LegacyProtectionIdentityVersion?.Length is > 0 and <= 80 &&
        b.LegacyProtectionPurpose?.Length is > 0 and <= 80 && entry.Properties.Where(p => p.IsModified).All(p => StorageFields.Contains(p.Metadata.Name));
    internal static bool Protected(EntityEntry entry) => entry.Property(nameof(ImportBatch.LegacyProtectedAt)).OriginalValue != null;
}
