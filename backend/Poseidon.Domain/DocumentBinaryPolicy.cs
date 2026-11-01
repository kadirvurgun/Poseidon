using System.Text;

namespace Poseidon.Domain;

// Current 26.02 paragraph201 permits PDF/PNG/JPEG. No type is mandatory and operators can
// select a narrower policy. Limited signatures are type checks, never malware/safety approval.
public static class DocumentBinaryPolicy
{
    public static bool Supported(string mime) => mime is "image/png" or "image/jpeg" or "application/pdf";
    public static bool ValidName(string? name, string? mime) => name is { Length: > 4 and <= 120 } &&
        name.IsNormalized(NormalizationForm.FormC) && name.All(c => char.IsLetterOrDigit(c) || c is ' ' or '.' or '-' or '_') && name.Count(c => c == '.') == 1 &&
        (mime switch { "image/png" => name.EndsWith(".png", StringComparison.OrdinalIgnoreCase),
            "image/jpeg" => name.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".jpeg", StringComparison.OrdinalIgnoreCase),
            "application/pdf" => name.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase), _ => false });
    public static async Task ValidateAsync(Stream stream, string mime, long maxPixels, CancellationToken ct)
    {
        if (mime == "image/png") { await SecureFilePolicy.ValidatePngAsync(stream, maxPixels, ct); return; }
        if (!Supported(mime) || stream.Length < 8) throw new InvalidDataException("file_type_invalid");
        var start = new byte[8]; await stream.ReadExactlyAsync(start, ct);
        if (mime == "application/pdf") {
            if (!Encoding.ASCII.GetString(start).StartsWith("%PDF-1.", StringComparison.Ordinal) && !Encoding.ASCII.GetString(start).StartsWith("%PDF-2.", StringComparison.Ordinal)) throw new InvalidDataException("file_type_invalid");
            stream.Seek(-Math.Min(1024, stream.Length), SeekOrigin.End);
            var tail = new byte[(int)(stream.Length - stream.Position)]; await stream.ReadExactlyAsync(tail, ct);
            if (!Encoding.ASCII.GetString(tail).TrimEnd().EndsWith("%%EOF", StringComparison.Ordinal)) throw new InvalidDataException("file_type_invalid");
        } else {
            if (start[0] != 0xff || start[1] != 0xd8 || start[2] != 0xff) throw new InvalidDataException("file_type_invalid");
            stream.Seek(-2, SeekOrigin.End); var tail = new byte[2]; await stream.ReadExactlyAsync(tail, ct);
            if (tail[0] != 0xff || tail[1] != 0xd9) throw new InvalidDataException("file_type_invalid");
        }
    }
}
