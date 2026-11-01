using System.Text.Json;
using Poseidon.Domain;

namespace Poseidon.Domain.Tests;

public sealed class CredentialDeclarationTests
{
    private static readonly DateOnly Reference = new(2030, 2, 28);
    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    [Fact]
    public void Empty_capture_remains_an_incomplete_unverified_draft_without_invented_dates()
    {
        var draft = JsonSerializer.Deserialize<CredentialDeclaration>("{}", Web)!;
        var facts = CredentialDeclarationDates.Evaluate(draft, Reference);
        Assert.Null(draft.CertificateNumber);
        Assert.Equal(DeclaredDateRelation.NotDeclared, facts.Issue);
        Assert.Equal(DeclaredDateRelation.NotDeclared, facts.Expiry);
        Assert.Equal(DeclaredCredentialDateOrder.Incomplete, facts.Order);
    }

    [Theory]
    [InlineData("verifiedBy")]
    [InlineData("verifiedAt")]
    [InlineData("verificationState")]
    [InlineData("verificationStatus")]
    [InlineData("approved")]
    [InlineData("tenantId")]
    [InlineData("personId")]
    public void Declaration_cannot_smuggle_verification_or_ownership_fields(string field)
    {
        var json = JsonSerializer.Serialize(new Dictionary<string, object?> {
            ["issuer"] = "Synthetic issuer", [field] = "forged"
        }, Web);
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<CredentialDeclaration>(json, Web));
    }

    [Fact]
    public void Source_text_is_preserved_without_silent_trimming_casefolding_or_issuer_authentication()
    {
        var draft = JsonSerializer.Deserialize<CredentialDeclaration>(
            """{"certificateTypeReference":" operator-ref ","certificateNumber":" 000-a/İ ","issuer":" Synthetic İssuer ","limitations":" draft restriction "}""", Web)!;
        Assert.Equal(" operator-ref ", draft.CertificateTypeReference);
        Assert.Equal(" 000-a/İ ", draft.CertificateNumber);
        Assert.Equal(" Synthetic İssuer ", draft.Issuer);
        Assert.Equal(" draft restriction ", draft.Limitations);
    }

    [Fact]
    public void Past_expiry_is_a_date_fact_even_when_issue_date_is_unknown()
    {
        var facts = CredentialDeclarationDates.Evaluate(new(ExpiryDate: new(2030, 2, 27)), Reference);
        Assert.Equal(DeclaredDateRelation.NotDeclared, facts.Issue);
        Assert.Equal(DeclaredDateRelation.BeforeReferenceDate, facts.Expiry);
        Assert.Equal(DeclaredCredentialDateOrder.Incomplete, facts.Order);
    }

    [Fact]
    public void Omitted_expiry_is_not_silently_converted_to_unlimited_validity()
    {
        var facts = CredentialDeclarationDates.Evaluate(new(IssueDate: new(2030, 2, 27)), Reference);
        Assert.Equal(DeclaredDateRelation.BeforeReferenceDate, facts.Issue);
        Assert.Equal(DeclaredDateRelation.NotDeclared, facts.Expiry);
        Assert.Equal(DeclaredCredentialDateOrder.Incomplete, facts.Order);
    }

    [Fact]
    public void Exact_expiry_day_is_kept_distinct_from_before_and_after_without_a_legal_cutover_guess()
    {
        var facts = CredentialDeclarationDates.Evaluate(new(IssueDate: new(2029, 1, 1), ExpiryDate: Reference), Reference);
        Assert.Equal(DeclaredDateRelation.OnReferenceDate, facts.Expiry);
        Assert.Equal(DeclaredCredentialDateOrder.IssueBeforeExpiry, facts.Order);
    }

    [Fact]
    public void Future_issue_is_visible_as_a_declared_fact_and_does_not_become_current_issuance()
    {
        var draft = new CredentialDeclaration(IssueDate: new(2031, 1, 1), ExpiryDate: new(2032, 1, 1));
        var facts = CredentialDeclarationDates.Evaluate(draft, Reference);
        Assert.Equal(DeclaredDateRelation.AfterReferenceDate, facts.Issue);
        Assert.Equal(DeclaredDateRelation.AfterReferenceDate, facts.Expiry);
        Assert.Equal(DeclaredCredentialDateOrder.IssueBeforeExpiry, facts.Order);
        Assert.Equal(new DateOnly(2031, 1, 1), draft.IssueDate);
    }

    [Fact]
    public void Reversed_dates_are_not_silently_reordered_or_replaced()
    {
        var draft = new CredentialDeclaration(IssueDate: new(2032, 1, 1), ExpiryDate: new(2028, 1, 1));
        var facts = CredentialDeclarationDates.Evaluate(draft, Reference);
        Assert.Equal(DeclaredCredentialDateOrder.ExpiryBeforeIssue, facts.Order);
        Assert.Equal(new DateOnly(2032, 1, 1), draft.IssueDate);
        Assert.Equal(new DateOnly(2028, 1, 1), draft.ExpiryDate);
    }

    [Fact]
    public void Same_day_issue_expiry_is_preserved_without_an_invented_minimum_duration()
    {
        var facts = CredentialDeclarationDates.Evaluate(new(IssueDate: Reference, ExpiryDate: Reference), Reference);
        Assert.Equal(DeclaredCredentialDateOrder.SameDate, facts.Order);
        Assert.Equal(DeclaredDateRelation.OnReferenceDate, facts.Issue);
        Assert.Equal(DeclaredDateRelation.OnReferenceDate, facts.Expiry);
    }

    [Fact]
    public void Explicit_reference_date_controls_boundary_facts_not_machine_time()
    {
        var draft = new CredentialDeclaration(IssueDate: new(2020, 1, 1), ExpiryDate: new(2030, 2, 28));
        Assert.Equal(DeclaredDateRelation.AfterReferenceDate, CredentialDeclarationDates.Evaluate(draft, new(2030, 2, 27)).Expiry);
        Assert.Equal(DeclaredDateRelation.OnReferenceDate, CredentialDeclarationDates.Evaluate(draft, Reference).Expiry);
        Assert.Equal(DeclaredDateRelation.BeforeReferenceDate, CredentialDeclarationDates.Evaluate(draft, new(2030, 3, 1)).Expiry);
    }

    [Fact]
    public void Calendar_extremes_do_not_require_day_arithmetic_or_overflow()
    {
        var draft = new CredentialDeclaration(IssueDate: DateOnly.MinValue, ExpiryDate: DateOnly.MaxValue);
        var first = CredentialDeclarationDates.Evaluate(draft, DateOnly.MinValue);
        var last = CredentialDeclarationDates.Evaluate(draft, DateOnly.MaxValue);
        Assert.Equal(DeclaredDateRelation.OnReferenceDate, first.Issue);
        Assert.Equal(DeclaredDateRelation.AfterReferenceDate, first.Expiry);
        Assert.Equal(DeclaredDateRelation.BeforeReferenceDate, last.Issue);
        Assert.Equal(DeclaredDateRelation.OnReferenceDate, last.Expiry);
    }

    [Fact]
    public void Normal_diagnostics_do_not_dump_personal_document_numbers_or_restrictions()
    {
        var draft = new CredentialDeclaration("Synthetic type", "Synthetic private number", "Synthetic issuer",
            new(2020, 1, 1), new(2030, 1, 1), "Synthetic private limitation");
        Assert.Equal(nameof(CredentialDeclaration), draft.ToString());
        Assert.Equal(nameof(DeclaredCredentialDateFacts), CredentialDeclarationDates.Evaluate(draft, Reference).ToString());
    }

    [Fact]
    public void Calendar_facts_are_independent_of_mutating_a_new_draft_copy()
    {
        var original = new CredentialDeclaration(IssueDate: new(2020, 1, 1), ExpiryDate: Reference);
        var changed = original with { ExpiryDate = new(2029, 1, 1) };
        Assert.Equal(DeclaredDateRelation.OnReferenceDate, CredentialDeclarationDates.Evaluate(original, Reference).Expiry);
        Assert.Equal(DeclaredDateRelation.BeforeReferenceDate, CredentialDeclarationDates.Evaluate(changed, Reference).Expiry);
    }

    [Fact]
    public void Missing_declaration_is_a_programming_error_not_an_invented_empty_certificate()
    {
        Assert.Throws<ArgumentNullException>(() => CredentialDeclarationDates.Evaluate(null!, Reference));
    }
}
