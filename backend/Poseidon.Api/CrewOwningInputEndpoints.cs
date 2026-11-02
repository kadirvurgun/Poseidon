using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record CrewOwningInput(string Field, string State, string OwnerTask, string Reason);
public static class CrewOwningInputEndpoints
{
    public static void MapCrewOwningInputs(this IEndpointRouteBuilder app) => app.MapGet(
        "/api/v1/vessels/{vessel:guid}/crew/{assignment:guid}/owning-inputs", Read).RequireAuthorization("DirectoryRead").WithName("CrewOwningInputs");

    private static async Task<IResult> Read(Guid vessel, Guid assignment, HttpContext h, PoseidonDbContext db)
    {
        h.Response.Headers.CacheControl = "no-store";
        var actor = h.RequestServices.GetRequiredService<ActorResolver>().Resolve(h.User)!;
        var audit = h.RequestServices.GetRequiredService<SecurityAudit>();
        // No input queries or counts until ordinary Crew/vessel/object authorization succeeds.
        if (!actor.CanRead || !actor.Permissions.Contains("crew.read") ||
            !await actor.Scope(db.Vessels).AnyAsync(x => x.Id == vessel, h.RequestAborted)) {
            await audit.WriteAsync(h, "CrewRead", "permission_denied"); return Results.NotFound();
        }
        var owner = await db.CrewAssignments.AsNoTracking().SingleOrDefaultAsync(x => x.Id == assignment && x.VesselId == vessel, h.RequestAborted);
        if (owner == null) { await audit.WriteAsync(h, "CrewRead", "resource_not_found"); return Results.NotFound(); }
        var certificate = new CrewOwningInput("certificate_state", "Withheld", "DEV-048", "private_input_access_required");
        if (CredentialEndpoints.Permitted(actor, vessel, false)) {
            // Bounded existence observation only. No generic evidence/trace/count projection of person files.
            var records = await db.Credentials.AsNoTracking().Where(x => x.VesselId == vessel && x.AssignmentId == assignment && x.PersonId == owner.PersonId).Take(101).ToArrayAsync(h.RequestAborted);
            var blocked = false;
            foreach (var record in records) {
                if (record.DocumentReference is { } file && !await CrewFileEndpoints.AuthorizedDocument(vessel, assignment, file, h, db)) { blocked = true; break; }
            }
            certificate = blocked ? certificate : new("certificate_state", records.Length == 0 ? "Missing" : "DeclaredUnverified", "DEV-048",
                records.Length == 0 ? "no_credential_declaration" : "declaration_is_not_authenticity_or_qualification");
        }
        var contact = new CrewOwningInput("contact_status", "Withheld", "DEV-047", "private_input_access_required");
        var policy = h.RequestServices.GetRequiredService<CrewProfilePolicy>();
        var fields = new[] { CrewPersonalField.ContactPhone, CrewPersonalField.Email }.Where(f =>
            CrewProfileEndpoints.Permitted(actor, vessel, false) && policy.Rule(f, policy.ImportPurpose, actor.Permissions, false) != null).ToArray();
        if (fields.Length > 0) {
            var profile = await db.CrewPersonalProfiles.AsNoTracking().SingleOrDefaultAsync(x => x.PersonId == owner.PersonId, h.RequestAborted);
            var protection = h.RequestServices.GetRequiredService<CrewProfileProtection>();
            try {
                if (!protection.Available) throw new CryptographicException("protected_storage_unavailable");
                var known = fields.Any(f => protection.Decrypt(db.TenantId, owner.PersonId, f, profile?.Ciphertext(f)) != null);
                contact = new("contact_status", known ? "DeclaredUnverified" : "Missing", "DEV-047", known ? "contact_declaration_only" : "no_visible_contact_declaration");
            } catch (CryptographicException) { contact = new("contact_status", "Unavailable", "DEV-047", "protected_storage_unavailable"); }
        }
        var inputs = new[] {
            certificate, contact,
            new CrewOwningInput("competence_state", "EvaluationUnavailable", "DEV-049", "owning_evaluator_not_implemented"),
            new CrewOwningInput("work_rest_state", "EvaluationUnavailable", "DEV-050", "owning_evaluator_not_implemented"),
            await CrewEligibilityObservation.Read(db, actor, h.RequestServices.GetRequiredService<FederationSettings>(), owner, h.RequestServices.GetRequiredService<TimeProvider>().GetUtcNow(), h.RequestAborted),
            new CrewOwningInput("fitness_state", "EvaluationUnavailable", "DEV-048", "qualified_clinical_workflow_not_implemented")
        };
        await audit.WriteAsync(h, "CrewProfile", "Success", resourceId: owner.PersonId);
        return Results.Ok(new { assignmentId = owner.Id, inputs, authority = "InputObservationOnly" });
    }
}
