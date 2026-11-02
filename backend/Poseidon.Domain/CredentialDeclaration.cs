using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// 14.01 paragraph102: Credential declared metadata (person ownership is resolved
// from the trusted authorized route/import context, not from this input).
// Partial drafts are valid captures under POL-DEFAULT-01. This input cannot set
// verification, signatories, evidence authenticity, tenant or operational status.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CredentialDeclaration(
    string? CertificateTypeReference = null,
    string? CertificateNumber = null,
    string? Issuer = null,
    DateOnly? IssueDate = null,
    DateOnly? ExpiryDate = null,
    string? Limitations = null)
{
    // A diagnostic formatter is not a serialization or authorization boundary;
    // intentionally serialize the declaration only through authorized endpoints.
    public override string ToString() => nameof(CredentialDeclaration);
}

public enum DeclaredDateRelation { NotDeclared, BeforeReferenceDate, OnReferenceDate, AfterReferenceDate }
public enum DeclaredCredentialDateOrder { Incomplete, ExpiryBeforeIssue, SameDate, IssueBeforeExpiry }

// Calendar facts only. No IsValid/Eligible/Verified/Ready flag or source/issuer
// validation is inferred. An expiry date's legal cutover is a separate policy.
public sealed record DeclaredCredentialDateFacts(
    DeclaredDateRelation Issue,
    DeclaredDateRelation Expiry,
    DeclaredCredentialDateOrder Order)
{
    public override string ToString() => nameof(DeclaredCredentialDateFacts);
}

public static class CredentialDeclarationDates
{
    // The actual service supplies the explicitly selected server-side calendar
    // date/timezone context. Never substitute the browser/system local clock here.
    public static DeclaredCredentialDateFacts Evaluate(CredentialDeclaration declaration, DateOnly referenceDate)
    {
        ArgumentNullException.ThrowIfNull(declaration);
        var order = declaration.IssueDate is not { } issue || declaration.ExpiryDate is not { } expiry
            ? DeclaredCredentialDateOrder.Incomplete
            : expiry < issue ? DeclaredCredentialDateOrder.ExpiryBeforeIssue
            : expiry == issue ? DeclaredCredentialDateOrder.SameDate
            : DeclaredCredentialDateOrder.IssueBeforeExpiry;
        return new(Relation(declaration.IssueDate, referenceDate), Relation(declaration.ExpiryDate, referenceDate), order);
    }

    private static DeclaredDateRelation Relation(DateOnly? date, DateOnly reference) => date is not { } value
        ? DeclaredDateRelation.NotDeclared
        : value < reference ? DeclaredDateRelation.BeforeReferenceDate
        : value == reference ? DeclaredDateRelation.OnReferenceDate
        : DeclaredDateRelation.AfterReferenceDate;
}
