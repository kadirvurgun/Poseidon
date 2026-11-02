using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;
namespace Poseidon.Persistence;

// Normalized person-owned aggregate. Separate encrypted typed columns, no arbitrary field bag or searchable PII.
public sealed class CrewPersonalProfile : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PersonId { get; set; }
    public long Revision { get; set; } = 1;
    public uint Version { get; set; }
    // Explicit selection; never infer a photo from the latest uploaded version.
    public Guid? PhotoFileId { get; set; }
    public Guid? PhotoVesselId { get; set; }
    public string? PhotoPolicyVersion { get; set; }
    public string? PhotoPurpose { get; set; }
    public string? Nationality { get; set; } // authenticated encrypted lexical value
    public string? DateOfBirth { get; set; } // authenticated encrypted lexical value
    public string? PassportNo { get; set; } // authenticated encrypted lexical value
    public string? PassportExpiry { get; set; } // authenticated encrypted lexical value
    public string? SeamanBookNo { get; set; } // authenticated encrypted lexical value
    public string? SeamanBookExpiry { get; set; } // authenticated encrypted lexical value
    public string? CompanyStartDate { get; set; } // authenticated encrypted lexical value
    public string? SeaServiceMonths { get; set; } // authenticated encrypted lexical value
    public string? TankerExperienceMonths { get; set; } // authenticated encrypted lexical value
    public string? VesselTypeExperience { get; set; } // authenticated encrypted lexical value
    public string? CompanyExperienceMonths { get; set; } // authenticated encrypted lexical value
    public string? EmergencyContact { get; set; } // authenticated encrypted lexical value
    public string? PreferredLanguage { get; set; } // authenticated encrypted lexical value
    public string? ContactPhone { get; set; } // authenticated encrypted lexical value
    public string? Email { get; set; } // authenticated encrypted lexical value
    public string? MedicalExpiry { get; set; } // restricted declaration only; never a fitness/eligibility decision
    public string? Ciphertext(CrewPersonalField field) => field switch {
        CrewPersonalField.Nationality => Nationality,
        CrewPersonalField.DateOfBirth => DateOfBirth,
        CrewPersonalField.PassportNo => PassportNo,
        CrewPersonalField.PassportExpiry => PassportExpiry,
        CrewPersonalField.SeamanBookNo => SeamanBookNo,
        CrewPersonalField.SeamanBookExpiry => SeamanBookExpiry,
        CrewPersonalField.CompanyStartDate => CompanyStartDate,
        CrewPersonalField.SeaServiceMonths => SeaServiceMonths,
        CrewPersonalField.TankerExperienceMonths => TankerExperienceMonths,
        CrewPersonalField.VesselTypeExperience => VesselTypeExperience,
        CrewPersonalField.CompanyExperienceMonths => CompanyExperienceMonths,
        CrewPersonalField.EmergencyContact => EmergencyContact,
        CrewPersonalField.PreferredLanguage => PreferredLanguage,
        CrewPersonalField.ContactPhone => ContactPhone,
        CrewPersonalField.Email => Email,
        CrewPersonalField.MedicalExpiry => MedicalExpiry,
        _ => throw new ArgumentOutOfRangeException(nameof(field))
    };
    public void Capture(CrewPersonalField field, string? ciphertext) {
        switch (field) {
            case CrewPersonalField.Nationality: Nationality = ciphertext; break;
            case CrewPersonalField.DateOfBirth: DateOfBirth = ciphertext; break;
            case CrewPersonalField.PassportNo: PassportNo = ciphertext; break;
            case CrewPersonalField.PassportExpiry: PassportExpiry = ciphertext; break;
            case CrewPersonalField.SeamanBookNo: SeamanBookNo = ciphertext; break;
            case CrewPersonalField.SeamanBookExpiry: SeamanBookExpiry = ciphertext; break;
            case CrewPersonalField.CompanyStartDate: CompanyStartDate = ciphertext; break;
            case CrewPersonalField.SeaServiceMonths: SeaServiceMonths = ciphertext; break;
            case CrewPersonalField.TankerExperienceMonths: TankerExperienceMonths = ciphertext; break;
            case CrewPersonalField.VesselTypeExperience: VesselTypeExperience = ciphertext; break;
            case CrewPersonalField.CompanyExperienceMonths: CompanyExperienceMonths = ciphertext; break;
            case CrewPersonalField.EmergencyContact: EmergencyContact = ciphertext; break;
            case CrewPersonalField.PreferredLanguage: PreferredLanguage = ciphertext; break;
            case CrewPersonalField.ContactPhone: ContactPhone = ciphertext; break;
            case CrewPersonalField.Email: Email = ciphertext; break;
            case CrewPersonalField.MedicalExpiry: MedicalExpiry = ciphertext; break;
            default: throw new ArgumentOutOfRangeException(nameof(field));
        }
    }
}
// Safe immutable lineage deliberately excludes private plaintext, ciphertext and value hashes.
public sealed class CrewPersonalProfileRevision : ITenantEntity
{
    public Guid TenantId { get; set; }
    public Guid PersonId { get; set; }
    public long Revision { get; set; }
    public required string PolicyVersion { get; set; }
    public required string Provenance { get; set; }
    public DateTimeOffset SavedAt { get; set; }
    public bool IsPhotoSelection { get; set; }
    public Guid? PhotoFileId { get; set; }
    public Guid? PhotoVesselId { get; set; }
    public string? PhotoPurpose { get; set; }
}
public static class CrewPersonalProfileStorage
{
    public static void Configure(ModelBuilder model) {
        model.Entity<CrewPersonalProfile>(b => {
            b.ToTable("PersonalProfiles", "crew"); b.HasKey(x => new { x.TenantId, x.PersonId });
            b.HasOne<Person>().WithMany().HasForeignKey(x => new { x.TenantId, x.PersonId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.Version).IsRowVersion();
            b.Property(x => x.PhotoPolicyVersion).HasMaxLength(80); b.Property(x => x.PhotoPurpose).HasMaxLength(80);
            b.HasOne<FileStage>().WithMany().HasForeignKey(x => new { x.TenantId, x.PhotoFileId, x.PhotoVesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).HasConstraintName("FK_ProfilePhotoFile").OnDelete(DeleteBehavior.Restrict);
        });
        model.Entity<CrewPersonalProfileRevision>(b => {
            b.ToTable("PersonalProfileRevisions", "crew"); b.HasKey(x => new { x.TenantId, x.PersonId, x.Revision });
            b.HasOne<CrewPersonalProfile>().WithMany().HasForeignKey(x => new { x.TenantId, x.PersonId }).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.PolicyVersion).HasMaxLength(80); b.Property(x => x.Provenance).HasMaxLength(40);
            b.Property(x => x.PhotoPurpose).HasMaxLength(80);
            b.HasOne<FileStage>().WithMany().HasForeignKey(x => new { x.TenantId, x.PhotoFileId, x.PhotoVesselId })
                .HasPrincipalKey(x => new { x.TenantId, x.Id, x.VesselId }).HasConstraintName("FK_ProfileRevisionPhotoFile").OnDelete(DeleteBehavior.Restrict);
        });
    }
}
