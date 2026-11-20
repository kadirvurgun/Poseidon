using System.Text.Json;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record WorkOrderOfflineScope(Guid TenantId,Guid ActorId,Guid VesselId);
public sealed record WorkOrderOfflineField(string Key,string ClassificationReference,string Purpose,string PrivacyClass);

// DEV-057 server operator configuration only; a reference is configuration, not a legal approval.
// No approved WO offline classification/retention decision exists in the sources, so the policy accepts
// only the most restrictive existing class (RestrictedOperationalDraft) for every evidence part and
// every part must be classified. Missing/incomplete configuration never permits local persistence.
// Gate WO-OFFLINE-CLASSIFICATION remains open until an authorized privacy/retention decision exists.
public sealed class WorkOrderOfflinePolicy
{
    public const string RequiredPrivacyClass="RestrictedOperationalDraft";
    public const string Operation="work-order.execution-evidence.v1";
    public static readonly string[] EvidenceParts=["readings","spares","files","workDone","findings"];
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
    public WorkOrderOfflineField[] Fields {get;set;}=[];
    public WorkOrderOfflineScope[] Scopes {get;set;}=[];
    private static bool Text(string? value,int maximum=256)=>value is {Length:>0} && value.Length<=maximum && !string.IsNullOrWhiteSpace(value) && !value.Any(char.IsControl);
    internal bool Configured=>Text(Version,80) && SourceVersion==WorkOrderExecutionPolicy.SourceVersion &&
        new[]{StoragePolicyReference,KeyPolicyReference,RetentionPolicyReference}.All(v=>Text(v)) &&
        LeaseSeconds is >=60 and <=86400 && RetentionSeconds>=LeaseSeconds && RetentionSeconds<=2592000 &&
        NotBefore.Offset==TimeSpan.Zero && ExpiresAt.Offset==TimeSpan.Zero && ExpiresAt>NotBefore &&
        MaxEnrollments is >=1 and <=32 && MaxRecords is >=2 and <=128 && MaxEncryptedBytes is >=131072 and <=4194304 &&
        MaxDeviceRecords>=MaxRecords && MaxDeviceRecords<=512 && MaxDeviceEncryptedBytes>=MaxEncryptedBytes && MaxDeviceEncryptedBytes<=16777216 &&
        Fields is {Length:5} && Fields.All(f=>f!=null && EvidenceParts.Contains(f.Key) && Text(f.ClassificationReference) && Text(f.Purpose) && f.PrivacyClass==RequiredPrivacyClass) &&
        Fields.Select(f=>f.Key).Distinct(StringComparer.Ordinal).Count()==Fields.Length &&
        Scopes is {Length:>0 and <=256} && Scopes.All(s=>s!=null && s.TenantId!=Guid.Empty && s.ActorId!=Guid.Empty && s.VesselId!=Guid.Empty) && Scopes.Distinct().Count()==Scopes.Length;
    internal string Fingerprint()=>RegisterStorage.Hash(JsonSerializer.Serialize(this));
    internal bool Allows(Guid tenant,Guid actor,Guid vessel,DateTimeOffset now)=>Configured && now>=NotBefore && now<ExpiresAt && Scopes.Contains(new(tenant,actor,vessel));
    internal WorkOrderOfflinePolicy Snapshot()=>new(){Version=Version,SourceVersion=SourceVersion,StoragePolicyReference=StoragePolicyReference,KeyPolicyReference=KeyPolicyReference,
        RetentionPolicyReference=RetentionPolicyReference,RetentionSeconds=RetentionSeconds,LeaseSeconds=LeaseSeconds,NotBefore=NotBefore,ExpiresAt=ExpiresAt,MaxEnrollments=MaxEnrollments,
        MaxRecords=MaxRecords,MaxEncryptedBytes=MaxEncryptedBytes,MaxDeviceRecords=MaxDeviceRecords,MaxDeviceEncryptedBytes=MaxDeviceEncryptedBytes,
        Fields=Fields.Select(f=>f with{}).OrderBy(f=>f.Key,StringComparer.Ordinal).ToArray(),
        Scopes=Scopes.OrderBy(s=>s.TenantId).ThenBy(s=>s.ActorId).ThenBy(s=>s.VesselId).ToArray()};
}
public sealed class WorkOrderOfflineSettings
{
    public WorkOrderOfflinePolicy? Policy {get;set;}
    internal WorkOrderOfflinePolicy? For(Guid tenant,Guid actor,Guid vessel,DateTimeOffset now)=>Policy?.Allows(tenant,actor,vessel,now)==true?Policy.Snapshot():null;
}
