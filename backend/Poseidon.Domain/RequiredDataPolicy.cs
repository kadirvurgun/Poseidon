namespace Poseidon.Domain;

public enum DataState { Missing, Unknown, Known }
public enum DataKind { Text, Boolean, Number }

// Private construction prevents inconsistent state/payload combinations.
public sealed record DraftValue
{
    private DraftValue(DataState state, DataKind kind, object? value)
        => (State, Kind, Value) = (state, kind, value);
    public DataState State { get; }
    public DataKind Kind { get; }
    public object? Value { get; }
    public static DraftValue Missing(DataKind kind) => Empty(DataState.Missing, kind);
    public static DraftValue Unknown(DataKind kind) => Empty(DataState.Unknown, kind);
    private static DraftValue Empty(DataState state, DataKind kind)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        return new(state, kind, null);
    }
    public static DraftValue Text(string? value) => string.IsNullOrWhiteSpace(value)
        ? Missing(DataKind.Text) : new(DataState.Known, DataKind.Text, value);
    public static DraftValue Boolean(bool? value) => value.HasValue
        ? new(DataState.Known, DataKind.Boolean, value.Value) : Missing(DataKind.Boolean);
    public static DraftValue Number(decimal? value) => value.HasValue
        ? new(DataState.Known, DataKind.Number, value.Value) : Missing(DataKind.Number);
}

// Construct only in trusted domain code; never bind definitions from request data.
public sealed record FieldDefinition
{
    public FieldDefinition(string key, DataKind kind, bool required)
    {
        ValidateKey(key);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        (Key, Kind, Required) = (key, kind, required);
    }
    internal static void ValidateKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key != key.Trim())
            throw new ArgumentException("Field keys must be nonblank with no surrounding whitespace.", nameof(key));
    }
    public string Key { get; }
    public DataKind Kind { get; }
    public bool Required { get; }
}

public enum RequiredDataReason { MissingRequiredValue, UnknownRequiredValue }
public sealed record RequiredDataBlock(string FieldKey, RequiredDataReason Reason);
public sealed class RequiredDataAssessment
{
    internal RequiredDataAssessment(IEnumerable<RequiredDataBlock> blocks)
        => BlockingFields = Array.AsReadOnly(blocks.ToArray());
    public IReadOnlyList<RequiredDataBlock> BlockingFields { get; }
    // This gate alone confers no business permission or operational readiness.
    public bool RequiredDataGateSatisfied => BlockingFields.Count == 0;
}

public sealed class RequiredDataPolicy
{
    private readonly FieldDefinition[] definitions;
    public RequiredDataPolicy(IEnumerable<FieldDefinition> trustedDefinitions)
    {
        ArgumentNullException.ThrowIfNull(trustedDefinitions);
        definitions = trustedDefinitions.ToArray();
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var definition in definitions)
        {
            ArgumentNullException.ThrowIfNull(definition);
            if (!keys.Add(definition.Key)) throw new ArgumentException("Duplicate field definition.", nameof(trustedDefinitions));
        }
        Array.Sort(definitions, (a, b) => StringComparer.Ordinal.Compare(a.Key, b.Key));
    }

    // Missing fields remain valid draft inputs; assessment never changes them.
    public RequiredDataAssessment Assess(IEnumerable<KeyValuePair<string, DraftValue>> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        var snapshot = new Dictionary<string, DraftValue>(StringComparer.Ordinal);
        foreach (var (key, value) in values)
        {
            FieldDefinition.ValidateKey(key);
            ArgumentNullException.ThrowIfNull(value);
            var definition = Array.Find(definitions, d => d.Key == key);
            if (definition is null || definition.Kind != value.Kind)
                throw new ArgumentException("Undefined field or mismatched value kind.", nameof(values));
            if (!snapshot.TryAdd(key, value)) throw new ArgumentException("Duplicate field value.", nameof(values));
        }
        var blocks = new List<RequiredDataBlock>();
        foreach (var definition in definitions.Where(d => d.Required))
        {
            var state = snapshot.TryGetValue(definition.Key, out var value) ? value.State : DataState.Missing;
            if (state != DataState.Known)
                blocks.Add(new(definition.Key, state == DataState.Unknown
                    ? RequiredDataReason.UnknownRequiredValue : RequiredDataReason.MissingRequiredValue));
        }
        return new(blocks);
    }
}
