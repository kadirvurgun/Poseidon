using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
namespace Poseidon;

// Only construct from a protocol handler's successfully validated principal.
public sealed record ExternalIdentity(string Issuer, string Subject);
public sealed record VesselGrant(Guid TenantId, Guid CompanyId, Guid VesselId);
public sealed record IdentityGrant(ExternalIdentity Identity, Guid TenantId, Guid CompanyId,
    IReadOnlyList<VesselGrant> Vessels, bool CanWrite, bool CanManageInventory = false, bool CanManageCertificates = false, bool CanCaptureEvidence = false);

// Implementations query authoritative server-owned membership data, never browser scope.
// Local accounts and AD federation may both be supplied behind the same OIDC boundary.
public interface IIdentityGrantResolver
{
    Task<IdentityGrant?> ResolveAsync(ExternalIdentity identity, CancellationToken ct);
}

public static class IdentityBoundary
{
    public static void RequireHttps(string? value)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) || uri.Scheme != "https" ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidOperationException("An absolute HTTPS issuer/authority without credentials, query or fragment is required.");
    }

    public static ExternalIdentity Read(ClaimsPrincipal principal, string issuer)
    {
        if (principal.Identity?.IsAuthenticated != true || principal.FindAll("iss").Count() != 1 ||
            principal.FindFirstValue("iss") != issuer || principal.FindAll("sub").Count() != 1 ||
            string.IsNullOrWhiteSpace(principal.FindFirstValue("sub")))
            throw new InvalidOperationException("Validated issuer and unique subject required.");
        return new(issuer, principal.FindFirstValue("sub")!);
    }

    public static string ActorKey(ExternalIdentity identity) => "oidc:" + Convert.ToHexString(
        SHA256.HashData(Encoding.UTF8.GetBytes(System.Text.Json.JsonSerializer.Serialize(identity))));

    public static ClaimsPrincipal Map(ExternalIdentity identity, IdentityGrant? grant)
    {
        if (grant is null || grant.Identity != identity || grant.TenantId == Guid.Empty || grant.CompanyId == Guid.Empty ||
            grant.Vessels is null || grant.Vessels.Any(v => v.TenantId != grant.TenantId || v.CompanyId != grant.CompanyId || v.VesselId == Guid.Empty) ||
            grant.Vessels.Select(v => v.VesselId).Distinct().Count() != grant.Vessels.Count)
            throw new InvalidOperationException("Missing or inconsistent server-owned identity grant.");
        var claims = new List<Claim> { new("iss", identity.Issuer), new("sub", identity.Subject),
            new("tenant_id", grant.TenantId.ToString()), new("company_id", grant.CompanyId.ToString()) };
        claims.AddRange(grant.Vessels.Select(v => new Claim("vessel_id", v.VesselId.ToString())));
        if (grant.CanCaptureEvidence) claims.Add(new("permission", "evidence:capture"));
        if (grant.CanWrite) claims.Add(new("permission", "draft:write"));
        if (grant.CanManageInventory) claims.Add(new("permission", "inventory:manage"));
        if (grant.CanManageCertificates) claims.Add(new("permission", "certificate:manage"));
        return new(new ClaimsIdentity(claims, BffAuthentication.CookieScheme));
    }

    // Public concurrency precondition, NOT a credential or authorization grant.
    public static string ScopeStamp(Session session) => $"{session.Subject}:{session.TenantId:D}:{session.CompanyId:D}";

    // Deliberately narrower than arbitrary local URLs; no encoded separators or controls.
    public static bool IsLocalReturnUrl(string? value) => value is { Length: > 0 } && value[0] == '/' &&
        (value.Length == 1 || value[1] != '/') && !value.Any(c => c == '\\' || c == '%' || char.IsControl(c)) &&
        !value.StartsWith("/bff", StringComparison.OrdinalIgnoreCase);
}
