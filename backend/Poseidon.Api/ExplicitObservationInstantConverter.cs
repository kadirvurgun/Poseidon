using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
namespace Poseidon.Api;
public sealed partial class ExplicitObservationInstantConverter : JsonConverter<DateTimeOffset?>
{
    [GeneratedRegex(@"\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\.[0-9]{1,7})?(?:Z|[+-][0-9]{2}:[0-9]{2})\z", RegexOptions.CultureInvariant)]
    private static partial Regex Instant();
    public override DateTimeOffset? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) {
        if (reader.TokenType == JsonTokenType.Null) return null;
        if (reader.TokenType != JsonTokenType.String || reader.GetString() is not { } text || !Instant().IsMatch(text) || !reader.TryGetDateTimeOffset(out var value))
            throw new JsonException("invalid_observation_instant");
        return value;
    }
    public override void Write(Utf8JsonWriter writer, DateTimeOffset? value, JsonSerializerOptions options) {
        if (value == null) writer.WriteNullValue(); else writer.WriteStringValue(value.Value);
    }
}
