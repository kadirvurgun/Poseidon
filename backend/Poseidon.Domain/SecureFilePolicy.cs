using System.Buffers.Binary;
using System.Security.Cryptography;

namespace Poseidon.Domain;

// SECURE-FILES-01: deliberately narrow passive PNG subset; no archive extraction or document rendering.
public static class SecureFilePolicy
{
    public static bool ValidName(string? name) => name is { Length: > 4 and <= 120 } &&
        name.EndsWith(".png", StringComparison.OrdinalIgnoreCase) &&
        name.All(c => char.IsAsciiLetterOrDigit(c) || c is '.' or '-' or '_') && !name.Contains("..");
    public static bool Hash(string? value) => value is { Length: 64 } && value.All(char.IsAsciiHexDigit);

    public static async Task<(long Length, string Checksum)> CopyAsync(Stream input, Stream output, long limit, CancellationToken ct)
    {
        if (limit <= 0) throw new InvalidDataException("file_settings_unavailable");
        using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
        var buffer = new byte[65536]; long total = 0; int count;
        while ((count = await input.ReadAsync(buffer, ct)) != 0)
        {
            if (count > limit - total) throw new InvalidDataException("file_size_invalid");
            total += count; hash.AppendData(buffer, 0, count);
            await output.WriteAsync(buffer.AsMemory(0, count), ct);
        }
        if (total == 0) throw new InvalidDataException("file_size_invalid");
        return (total, Convert.ToHexString(hash.GetHashAndReset()));
    }

    public static async Task ValidatePngAsync(Stream stream, long maxPixels, CancellationToken ct)
    {
        var signature = new byte[8]; await stream.ReadExactlyAsync(signature, ct);
        if (!signature.SequenceEqual(new byte[] {137,80,78,71,13,10,26,10})) throw new InvalidDataException("file_type_invalid");
        var header = new byte[8]; var scratch = new byte[65536]; var crcBytes = new byte[4];
        bool ihdr = false, data = false, end = false;
        while (!end)
        {
            await stream.ReadExactlyAsync(header, ct);
            var length = BinaryPrimitives.ReadUInt32BigEndian(header);
            var type = System.Text.Encoding.ASCII.GetString(header, 4, 4);
            if (length > stream.Length - stream.Position - 4 ||
                (type != "IHDR" && type != "IDAT" && type != "IEND") || !ihdr && type != "IHDR" ||
                type == "IHDR" && (ihdr || length != 13) || type == "IEND" && (!data || length != 0) || type == "IDAT" && length == 0)
                throw new InvalidDataException("file_type_invalid");
            uint crc = uint.MaxValue;
            void Crc(ReadOnlySpan<byte> bytes) { foreach (var b in bytes) { crc ^= b; for (int i = 0; i < 8; i++) crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xedb88320u : 0); } }
            Crc(header.AsSpan(4));
            long remaining = length;
            while (remaining > 0)
            {
                var take = (int)Math.Min(scratch.Length, remaining); await stream.ReadExactlyAsync(scratch.AsMemory(0, take), ct);
                Crc(scratch.AsSpan(0, take)); remaining -= take;
                if (type == "IHDR")
                {
                    var width = BinaryPrimitives.ReadUInt32BigEndian(scratch);
                    var height = BinaryPrimitives.ReadUInt32BigEndian(scratch.AsSpan(4));
                    if (width == 0 || height == 0 || (ulong)width * height > (ulong)Math.Max(0, maxPixels) ||
                        scratch[8] != 8 || scratch[9] is not (2 or 6) || scratch[10] != 0 || scratch[11] != 0 || scratch[12] != 0)
                        throw new InvalidDataException("file_type_invalid");
                }
            }
            await stream.ReadExactlyAsync(crcBytes, ct);
            if (BinaryPrimitives.ReadUInt32BigEndian(crcBytes) != ~crc) throw new InvalidDataException("file_type_invalid");
            ihdr = true; data |= type == "IDAT"; end = type == "IEND";
        }
        if (stream.Position != stream.Length) throw new InvalidDataException("file_type_invalid");
    }
}
