using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Authorization;
namespace Poseidon;

public static class DraftEndpoints
{
    public static void MapDraftEndpoints(this WebApplication app)
    {
        Map(app.MapGroup("/api/v1"), "draft-write");
        if (!app.Services.GetRequiredService<BffSettings>().Enabled) return;
        var browser = app.MapGroup("/bff/api/v1")
            .RequireAuthorization(new AuthorizeAttribute { AuthenticationSchemes = BffAuthentication.CookieScheme });
        // Only these explicit cookie routes accept browser sessions. All mutations are CSRF checked.
        browser.AddEndpointFilter(async (context, next) => {
            if (!HttpMethods.IsGet(context.HttpContext.Request.Method)) {
                try { await context.HttpContext.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(context.HttpContext); }
                catch (AntiforgeryValidationException) { return Results.Json(new { code = "csrf_invalid", message = "Request verification failed." }, statusCode: 400); }
            }
            var expected = IdentityBoundary.ScopeStamp(Contracts.Scope(context.HttpContext.User));
            if (context.HttpContext.Request.Headers["X-Poseidon-Scope"].ToString() != expected)
                return Results.Json(new { code = "session_scope_changed", message = "Reload the authenticated session before accessing drafts." }, statusCode: 401);
            return await next(context);
        });
        Map(browser, "bff-draft-write");
    }

    private static void Map(RouteGroupBuilder group, string writePolicy)
    {
        foreach (var resource in new[] { "vessels", "certificates" }) {
            group.MapGet($"/{resource}", async (HttpContext c, DraftStore store, CancellationToken ct) => {
                var scope = Contracts.Scope(c.User);
                return resource == "vessels"
                    ? Results.Ok(await store.BrowseVessels(scope, VesselBrowse.Parse(c.Request.Query, scope), ct))
                    : Results.Ok(new { items = await store.List(scope, resource, ct) });
            });
            group.MapGet($"/{resource}/{{id:guid}}", async (Guid id, HttpContext c, DraftStore store, CancellationToken ct) => {
                var draft = await store.Get(Contracts.Scope(c.User), resource, id, ct);
                if (draft is null) throw new ApiFault(404, "not_found", "Draft not found.");
                c.Response.Headers.ETag = $"\"{draft.Version}\""; return Results.Ok(draft);
            });
            group.MapPost($"/{resource}", async (JsonElement body, HttpContext c, DraftStore store, CancellationToken ct) => await Save(null, body, c, store, resource, ct)).RequireAuthorization(writePolicy);
            group.MapPost($"/{resource}/{{id:guid}}", async (Guid id, JsonElement body, HttpContext c, DraftStore store, CancellationToken ct) => await Save(id, body, c, store, resource, ct)).RequireAuthorization(writePolicy);
            group.MapPost($"/{resource}/{{id:guid}}/submit", () => Results.Json(new { code = "submission_disabled", message = "Domain approval rules are unresolved. Metadata drafts cannot be submitted." }, statusCode: 403)).RequireAuthorization(writePolicy);
        }
    }
    private static async Task<IResult> Save(Guid? id, JsonElement body, HttpContext c, DraftStore store, string kind, CancellationToken ct)
    {
        var data = Contracts.Validate(body, kind); var key = c.Request.Headers["Idempotency-Key"].ToString();
        if (!Guid.TryParseExact(key, "D", out _)) throw new ApiFault(400, "idempotency_required", "Idempotency-Key UUID required.");
        long? expected = null;
        if (id.HasValue) {
            var match = c.Request.Headers.IfMatch.ToString();
            if (match.Length < 3 || match[0] != '"' || match[^1] != '"' || !long.TryParse(match[1..^1], out var version) || version < 1)
                throw new ApiFault(428, "version_required", "If-Match with quoted positive version is required.");
            expected = version;
        }
        var result = await store.Save(Contracts.Scope(c.User), kind, id, expected, data, key, c.TraceIdentifier, ct);
        c.Response.Headers.ETag = $"\"{result.Version}\""; return Results.Ok(result);
    }
}
