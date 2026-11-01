using System.Text.Json.Serialization;
namespace Poseidon.Domain;

// 14.02 rows1591–1608. All are declarations; health/qualification and person files have separate contracts.
public enum CrewPersonalField { Nationality, DateOfBirth, PassportNo, PassportExpiry, SeamanBookNo, SeamanBookExpiry, CompanyStartDate, SeaServiceMonths, TankerExperienceMonths, VesselTypeExperience, CompanyExperienceMonths, EmergencyContact, PreferredLanguage, ContactPhone, Email }
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CrewPersonalData(
    string? Nationality = null,
    DateOnly? DateOfBirth = null,
    string? PassportNo = null,
    DateOnly? PassportExpiry = null,
    string? SeamanBookNo = null,
    DateOnly? SeamanBookExpiry = null,
    DateOnly? CompanyStartDate = null,
    string? SeaServiceMonths = null,
    string? TankerExperienceMonths = null,
    string? VesselTypeExperience = null,
    string? CompanyExperienceMonths = null,
    string? EmergencyContact = null,
    string? PreferredLanguage = null,
    string? ContactPhone = null,
    string? Email = null)
{
    public override string ToString() => nameof(CrewPersonalData);
    public string? Lexical(CrewPersonalField field) => field switch {
        CrewPersonalField.Nationality => Nationality,
        CrewPersonalField.DateOfBirth => DateOfBirth?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        CrewPersonalField.PassportNo => PassportNo,
        CrewPersonalField.PassportExpiry => PassportExpiry?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        CrewPersonalField.SeamanBookNo => SeamanBookNo,
        CrewPersonalField.SeamanBookExpiry => SeamanBookExpiry?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        CrewPersonalField.CompanyStartDate => CompanyStartDate?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
        CrewPersonalField.SeaServiceMonths => SeaServiceMonths,
        CrewPersonalField.TankerExperienceMonths => TankerExperienceMonths,
        CrewPersonalField.VesselTypeExperience => VesselTypeExperience,
        CrewPersonalField.CompanyExperienceMonths => CompanyExperienceMonths,
        CrewPersonalField.EmergencyContact => EmergencyContact,
        CrewPersonalField.PreferredLanguage => PreferredLanguage,
        CrewPersonalField.ContactPhone => ContactPhone,
        CrewPersonalField.Email => Email,
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };
    public CrewPersonalData With(CrewPersonalField field, string? value) => field switch {
        CrewPersonalField.Nationality => this with { Nationality = value },
        CrewPersonalField.DateOfBirth => this with { DateOfBirth = value == null ? null : DateOnly.ParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) },
        CrewPersonalField.PassportNo => this with { PassportNo = value },
        CrewPersonalField.PassportExpiry => this with { PassportExpiry = value == null ? null : DateOnly.ParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) },
        CrewPersonalField.SeamanBookNo => this with { SeamanBookNo = value },
        CrewPersonalField.SeamanBookExpiry => this with { SeamanBookExpiry = value == null ? null : DateOnly.ParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) },
        CrewPersonalField.CompanyStartDate => this with { CompanyStartDate = value == null ? null : DateOnly.ParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture) },
        CrewPersonalField.SeaServiceMonths => this with { SeaServiceMonths = value },
        CrewPersonalField.TankerExperienceMonths => this with { TankerExperienceMonths = value },
        CrewPersonalField.VesselTypeExperience => this with { VesselTypeExperience = value },
        CrewPersonalField.CompanyExperienceMonths => this with { CompanyExperienceMonths = value },
        CrewPersonalField.EmergencyContact => this with { EmergencyContact = value },
        CrewPersonalField.PreferredLanguage => this with { PreferredLanguage = value },
        CrewPersonalField.ContactPhone => this with { ContactPhone = value },
        CrewPersonalField.Email => this with { Email = value },
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };
    public static string Key(CrewPersonalField field) => field switch {
        CrewPersonalField.Nationality => "nationality",
        CrewPersonalField.DateOfBirth => "date_of_birth",
        CrewPersonalField.PassportNo => "passport_no",
        CrewPersonalField.PassportExpiry => "passport_expiry",
        CrewPersonalField.SeamanBookNo => "seaman_book_no",
        CrewPersonalField.SeamanBookExpiry => "seaman_book_expiry",
        CrewPersonalField.CompanyStartDate => "company_start_date",
        CrewPersonalField.SeaServiceMonths => "sea_service_months",
        CrewPersonalField.TankerExperienceMonths => "tanker_experience_months",
        CrewPersonalField.VesselTypeExperience => "vessel_type_experience",
        CrewPersonalField.CompanyExperienceMonths => "company_experience_months",
        CrewPersonalField.EmergencyContact => "emergency_contact",
        CrewPersonalField.PreferredLanguage => "preferred_language",
        CrewPersonalField.ContactPhone => "contact_phone",
        CrewPersonalField.Email => "email",
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };
    public static bool Valid(CrewPersonalField field, string? value) => value == null ||
        value.Length <= 4096 && !value.Any(char.IsControl) && (field switch {
        CrewPersonalField.SeaServiceMonths or CrewPersonalField.TankerExperienceMonths or CrewPersonalField.CompanyExperienceMonths => CertificateMetadataPolicy.ExactNumeric(value),
        CrewPersonalField.DateOfBirth or CrewPersonalField.PassportExpiry or CrewPersonalField.SeamanBookExpiry or CrewPersonalField.CompanyStartDate => DateOnly.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out _),
        _ => true
    });
}
