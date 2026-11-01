using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace Poseidon.Domain;

// 14.02 Data Dictionary 2316 rows1634–1657. Lookup references are declarations
// awaiting controlled resolution, never approved catalogs or evidence verification.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CertificateMetadata(CredentialDeclaration Declaration,
    string? IssuingCountryNumeric = null, bool? EndorsementRequired = null,
    string? EndorsementNumber = null, DateOnly? EndorsementExpiry = null,
    string? StcwReference = null, string? TankerRelevance = null, string? VesselTypeScope = null,
    Guid? DocumentReference = null, string? RenewalLeadDaysNumeric = null,
    string? SourceSystem = null, string? Notes = null)
{
    public override string ToString() => nameof(CertificateMetadata);
}

public static partial class CertificateMetadataPolicy
{
    // Technical capture bounds, not approved source precision/scale or legal limits.
    public const int MaxText = 4096;
    public const int MaxNumericCharacters = 1024;
    [GeneratedRegex(@"\A(0|[1-9][0-9]*)(\.[0-9]+)?\z", RegexOptions.CultureInvariant)]
    private static partial Regex NonnegativeNumeric();
    public static bool ExactNumeric(string? value) => value is null ||
        value.Length <= MaxNumericCharacters && NonnegativeNumeric().IsMatch(value);

    public static bool Valid(CertificateMetadata? input) => input?.Declaration is { } d &&
        new[] { d.CertificateTypeReference, d.CertificateNumber, d.Issuer, d.Limitations,
            input.EndorsementNumber, input.StcwReference, input.TankerRelevance,
            input.VesselTypeScope, input.SourceSystem, input.Notes }
            .All(x => x == null || x.Length <= MaxText && !x.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t')) &&
        ExactNumeric(input.IssuingCountryNumeric) && ExactNumeric(input.RenewalLeadDaysNumeric) &&
        input.DocumentReference != Guid.Empty;
    // Missing, future, reversed and exact expiry dates remain calendar observations.
}
