using System.Globalization;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record ImportTemplateDto(string Category, string Version, string Fingerprint, ImportTemplateField[] Fields,
    bool AllowSourceColumnReuse, string Format, int MaxBytes, int MaxRows, int MaxColumns, int MaxCellCharacters,
    string NumericPolicy, string CommitPolicy);
public sealed record ImportRowIssueDto(string Code, int? FieldOrdinal);
public sealed record ImportValidationRowDto(int RowNumber, string Outcome, ImportRowIssueDto[] Issues);

internal static class ImportTemplates
{
    internal const string Version = "DEV-069-declaration-v3";
    internal static readonly string[] Categories = ["vessel", "crew", "certificate", "equipment"];
    internal static ImportTemplateDefinition? Definition(string category) {
        string[] fields = category switch {
            "vessel" => ["record_key", "base_version", "code", "name"],
            "crew" => new string[] { "record_key", "base_version", "full_name", "employee_no", "person_id", "person_base_version", "identity_policy_version", "identity_purpose", "position_policy_version", "position_ref", "from", "to", "contract_end_date", "watch_group", "profile_base_version", "profile_policy_version", "assignment_status", "assignment_policy_version" }
                .Concat(Enum.GetValues<CrewPersonalField>().Select(CrewPersonalData.Key)).ToArray(),
            "certificate" => ["record_key", "base_version", "certificate_type", "certificate_no", "issuer", "issuing_country", "issue_date", "expiry_date",
                "endorsement_required", "endorsement_no", "endorsement_expiry", "stcw_reference", "tanker_relevance", "vessel_type_scope",
                "document_ref", "renewal_lead_days", "restriction", "source_system", "notes"],
            "equipment" => ["record_key", "base_version", "parent_ref", "code", "name", "level", "system_family", "equipment_type", "criticality",
                "maker", "model", "serial_number", "location", "commission_date", "notes"],
            _ => []
        };
        return fields.Length == 0 ? null : new((category == "crew" ? "DEV-069-declaration-v4" : Version) + "/" + category,
            fields.Select(f => new ImportTemplateField(f, f == "record_key" || category == "vessel" && f is "code" or "name" or "base_version" ||
                category == "equipment" && f == "level", true))
                .Concat(category == "certificate" ? new[] { "person_id", "verification_status", "verified_by", "verified_at", "alert_state", "sync_state", "status" }
                    .Select(f => new ImportTemplateField(f, false, false)) : Enumerable.Empty<ImportTemplateField>()).ToArray(), false);
    }
    internal static ImportTemplateDto Describe(string category) {
        var definition = Definition(category)!;
        var identity = definition.Fields.Where(f => f.UserWritable).Select((f, i) => new ImportColumnBinding(i, f.Key)).ToArray();
        var fingerprint = ImportColumnMappingPolicy.Validate(definition, identity.Length, identity).Mapping!.TemplateFingerprint;
        return new(category, definition.Version, fingerprint, definition.Fields.ToArray(), false, "xlsx-single-sheet",
            ImportWorkbook.MaxBytes, ImportWorkbook.MaxRows, ImportWorkbook.MaxColumns, ImportWorkbook.MaxCellCharacters,
            "Exact lexical nonnegative numeric; source precision/scale TBD; no ISO catalog implied",
            "Explicit atomic trial: no rows committed if any row fails; no duplicate merge; production migration gate remains separate");
    }

    // Closed positional adapter; no reflection, arbitrary properties or executable mappings.
    internal sealed class Row(ValidatedImportMapping mapping, string[] cells)
    {
        public string? Get(string key) {
            var column = mapping.Columns.SingleOrDefault(c => c.TargetFieldKey == key);
            if (column.TargetFieldKey == null) return null;
            var value = cells[column.SourceColumn]; return value == "" ? null : value;
        }
        public bool Has(string key) => mapping.Columns.Any(c => c.TargetFieldKey == key);
        private string? Text(string key, string? previous) => Has(key) ? Get(key) : previous;
        private DateOnly? Day(string key, DateOnly? previous) => Has(key) ? Date(key) : previous;
        private Guid? Ref(string key, Guid? previous) => Has(key) ? Reference(key) : previous;
        public Guid Id(string key, bool required = false) {
            var s = Get(key); if (s == null && !required) return Guid.Empty;
            if (s == null || s.Length != 36 || !Guid.TryParseExact(s, "D", out var id) || id == Guid.Empty) throw new FormatException();
            return id;
        }
        public Guid? Reference(string key) { var id = Id(key); return id == Guid.Empty ? null : id; }
        public uint? BaseVersion(string key = "base_version") {
            var s = Get(key); if (s == null) return null;
            if (!uint.TryParse(s, NumberStyles.None, CultureInfo.InvariantCulture, out var v) || v == 0) throw new FormatException(); return v;
        }
        public DateOnly? Date(string key) {
            var s = Get(key); if (s == null) return null;
            if (!DateOnly.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var d)) throw new FormatException(); return d;
        }
        public bool? Boolean(string key) => Get(key) switch { null => null, "true" => true, "false" => false, _ => throw new FormatException() };
        public CrewProfileCommand? PersonalProfile() {
            var fields = Enum.GetValues<CrewPersonalField>().Where(f => Has(CrewPersonalData.Key(f))).ToArray();
            if (fields.Length == 0) return null;
            var data = new CrewPersonalData();
            foreach (var field in fields) {
                var value = Get(CrewPersonalData.Key(field));
                if (!CrewPersonalData.Valid(field, value)) throw new FormatException();
                data = data.With(field, value);
            }
            return new(BaseVersion("profile_base_version"), Get("profile_policy_version") ?? "", "", fields, data);
        }
        public CredentialSaveCommand Certificate(Credential? old = null) => new(Id("record_key", true), BaseVersion(), new(
            new(Text("certificate_type", old?.CertificateTypeReference), Text("certificate_no", old?.CertificateNumber), Text("issuer", old?.Issuer),
                Day("issue_date", old?.IssueDate), Day("expiry_date", old?.ExpiryDate), Text("restriction", old?.Limitations)),
            Text("issuing_country", old?.IssuingCountryNumeric), Has("endorsement_required") ? Boolean("endorsement_required") : old?.EndorsementRequired,
            Text("endorsement_no", old?.EndorsementNumber), Day("endorsement_expiry", old?.EndorsementExpiry), Text("stcw_reference", old?.StcwReference),
            Text("tanker_relevance", old?.TankerRelevance), Text("vessel_type_scope", old?.VesselTypeScope), Ref("document_ref", old?.DocumentReference),
            Text("renewal_lead_days", old?.RenewalLeadDaysNumeric), Text("source_system", old?.SourceSystem), Text("notes", old?.Notes)));
        public bool HasIdentity => Has("full_name") || Has("employee_no");
        public CrewPersonCommand Identity() => new(BaseVersion("person_base_version") ?? 0, Get("full_name"), Get("employee_no"),
            Get("identity_policy_version"), Get("identity_purpose"), Has("full_name"), Has("employee_no"));
        public CrewSaveCommand Crew(CrewAssignment? old) => new(Id("record_key", true), Reference("person_id") ?? old?.PersonId, BaseVersion(), old == null ? Get("full_name") : null, old == null ? Get("employee_no") : null,
            Ref("position_ref", old?.PositionId), Day("from", old?.From), Day("to", old?.To), Day("contract_end_date", old?.ContractEndDate), Text("watch_group", old?.WatchGroup), old?.SourceObservedAt, Get("assignment_status"), Get("assignment_policy_version"), Get("identity_policy_version"), Get("identity_purpose"), Get("position_policy_version"));
        public EquipmentCommand Equipment(Asset? old = null) => new(Id("record_key", true), BaseVersion(), Ref("parent_ref", old?.ParentId),
            Text("code", old?.Code), Text("name", old?.Name), Text("level", old?.Level) ?? "", Text("system_family", old?.SystemFamily),
            Text("equipment_type", old?.EquipmentType), Text("criticality", old?.Criticality), Text("maker", old?.Maker), Text("model", old?.Model),
            Text("serial_number", old?.SerialNumber), Text("location", old?.Location), Day("commission_date", old?.CommissionDate),
            old?.ResponsibleRoleId, Text("notes", old?.Notes));
        public VesselUpdateCommand Vessel() => new(Get("code"), Get("name"), BaseVersion());
    }
}
