namespace Poseidon.Domain;

// Software revision governance for DEV-047 controlled references. These states do
// not assert a production-approved codebook, credential qualification or appointment.
public static class CrewCatalogGovernance
{
    public static string? Reject(string? prior, string next, bool complete, bool independent, bool hasActive) => next switch {
        "Draft" => null,
        "Reviewed" when prior != "Draft" => "invalid_transition",
        "Reviewed" when !independent => "independent_catalog_authority_required",
        "Reviewed" when !complete => "catalog_incomplete",
        "Reviewed" => null,
        "Adopted" when prior != "Reviewed" => "invalid_transition",
        "Adopted" when !independent => "independent_catalog_authority_required",
        "Adopted" when !complete => "catalog_incomplete",
        "Adopted" => null,
        "Withdrawn" when hasActive => null,
        _ => "invalid_transition"
    };
}
