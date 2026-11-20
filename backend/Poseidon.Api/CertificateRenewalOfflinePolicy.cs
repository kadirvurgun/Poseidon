using System.Text.Json;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record CertificateRenewalOfflineScope(Guid TenantId,Guid ActorId,Guid VesselId);
public sealed record CertificateRenewalOfflineField(string Key,string ClassificationReference,string Purpose,string PrivacyClass);

// S10 WP-C DEV-066 server operator configuration only; a reference is configuration, not a legal approval.
// No approved classification/retention decision for offline certificate renewal drafts exists in the sources,
// so every captured part must carry the most restrictive existing class (RestrictedOperationalDraft) and a
// missing/incomplete policy never permits local persistence (UNAVAILABLE_POLICY). Only the DRAFT/PREPARING
// metadata authoring command is offline; replacement certificate/document links, every workflow transition,
// verification and completion stay online human commands.
// Gate CERT-RENEWAL-OFFLINE-CLASSIFICATION remains open until an authorized privacy/retention decision exists.
public sealed class CertificateRenewalOfflinePolicy
{
    public const string RequiredPrivacyClass="RestrictedOperationalDraft";
    public const string Operation="certificate-renewal.draft.v1";
    public static readonly string[] Parts=["renewalType","schedule","owner","requirements","application"];
    public string Version {get;set;}="";
    public string SourceVersion {get;set;}="";
    public string StoragePolicyReference {get;set;}="";
    public string KeyPolicyReference {get;set;}="";
    public string RetentionPolicyReference {get;set;}="";
    public int RetentionSeconds {get;set;}
    public int LeaseSeconds {get;set;}
    public DateTimeOffset NotBefore {get;set;}
    public DateTimeOffset ExpiresAt {get;set;}
    public int MaxEnrollments {get;set;}
    public int MaxRecords {get;set;}
    public int MaxEncryptedBytes {get;set;}
    public int MaxDeviceRecords {get;set;}
    public int MaxDeviceEncryptedBytes {get;set;}
    public CertificateRenewalOfflineField[] Fields {get;set;}=[];
    public CertificateRenewalOfflineScope[] Scopes {get;set;}=[];
    private static bool Text(string? value,int maximum=256)=>value is {Length:>0} && value.Length<=maximum && !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
    internal bool Configured=>Text(Version,80) && SourceVersion==VesselCertificatePolicy.SourceVersion &&
        new[]{StoragePolicyReference,KeyPolicyReference,RetentionPolicyReference}.All(v=>Text(v)) &&
        LeaseSeconds is >=60 and <=86400 && RetentionSeconds>=LeaseSeconds && RetentionSeconds<=2592000 &&
        NotBefore.Offset==TimeSpan.Zero && ExpiresAt.Offset==TimeSpan.Zero && ExpiresAt>NotBefore &&
        MaxEnrollments is >=1 and <=32 && MaxRecords is >=2 and <=128 && MaxEncryptedBytes is >=131072 and <=4194304 &&
        MaxDeviceRecords>=MaxRecords && MaxDeviceRecords<=512 && MaxDeviceEncryptedBytes>=MaxEncryptedBytes && MaxDeviceEncryptedBytes<=16777216 &&
        Fields is {Length:5} && Fields.All(f=>f!=null && Parts.Contains(f.Key) && Text(f.ClassificationReference) && Text(f.Purpose) && f.PrivacyClass==RequiredPrivacyClass) &&
        Fields.Select(f=>f.Key).Distinct(StringComparer.Ordinal).Count()==Fields.Length &&
        Scopes is {Length:>0 and <=256} && Scopes.All(s=>s!=null && s.TenantId!=Guid.Empty && s.ActorId!=Guid.Empty && s.VesselId!=Guid.Empty) && Scopes.Distinct().Count()==Scopes.Length;
    internal string Fingerprint()=>RegisterStorage.Hash(JsonSerializer.Serialize(Snapshot()));
    internal bool Allows(Guid tenant,Guid actor,Guid vessel,DateTimeOffset now)=>Configured && now>=NotBefore && now<ExpiresAt && Scopes.Contains(new(tenant,actor,vessel));
    internal CertificateRenewalOfflinePolicy Snapshot()=>new(){Version=Version,SourceVersion=SourceVersion,StoragePolicyReference=StoragePolicyReference,KeyPolicyReference=KeyPolicyReference,
        RetentionPolicyReference=RetentionPolicyReference,RetentionSeconds=RetentionSeconds,LeaseSeconds=LeaseSeconds,NotBefore=NotBefore,ExpiresAt=ExpiresAt,MaxEnrollments=MaxEnrollments,
        MaxRecords=MaxRecords,MaxEncryptedBytes=MaxEncryptedBytes,MaxDeviceRecords=MaxDeviceRecords,MaxDeviceEncryptedBytes=MaxDeviceEncryptedBytes,
        Fields=Fields.Select(f=>f with{}).OrderBy(f=>f.Key,StringComparer.Ordinal).ToArray(),
        Scopes=Scopes.OrderBy(s=>s.TenantId).ThenBy(s=>s.ActorId).ThenBy(s=>s.VesselId).ToArray()};
    // Replacement certificate and new-document links are verified online against current visibility and
    // file custody; an offline draft may change only declared metadata of the bound renewal case.
    internal static bool OfflineEditable(CertificateRenewalContent captured,CertificateRenewalContent server)=>CertificateRenewalPolicy.Valid(captured) &&
        captured.NewCertificateId==server.NewCertificateId && captured.NewCertificateRevision==server.NewCertificateRevision &&
        captured.NewCertificateVersion==server.NewCertificateVersion && Equals(captured.NewDocument,server.NewDocument);
}
public sealed class CertificateRenewalOfflineSettings
{
    public CertificateRenewalOfflinePolicy? Policy {get;set;}
    internal CertificateRenewalOfflinePolicy? For(Guid tenant,Guid actor,Guid vessel,DateTimeOffset now)=>Policy?.Allows(tenant,actor,vessel,now)==true?Policy.Snapshot():null;
}
