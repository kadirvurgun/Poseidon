using System.Net.Http.Headers;
using System.Text.Json;
using Poseidon.Domain;
using Poseidon.Persistence;

namespace Poseidon.Api;

public sealed record CertificateExtractionScope(Guid TenantId,Guid ActorId,Guid VesselId,string Scope);
public sealed class CertificateExtractionSettings
{
    public bool Enabled {get;set;}
    public string? Endpoint {get;set;}
    public string? AllowedHost {get;set;}
    public string? SecretReference {get;set;}
    public string? PolicyVersion {get;set;}
    public string? ProcessingReference {get;set;}
    public string? StorageReference {get;set;}
    public string? Region {get;set;}
    public string? PrivacyReference {get;set;}
    public string? PersonalProcessingReference {get;set;}
    public string? RetentionReference {get;set;}
    public string? ConfidenceReference {get;set;}
    public string? SourceVersion {get;set;}
    public string? ModelVersion {get;set;}
    public string? PromptVersion {get;set;}
    public decimal? LowConfidenceThreshold {get;set;}
    public int TimeoutSeconds {get;set;}
    public int ReviewSeconds {get;set;}
    public int MaxBytes {get;set;}
    public int MaxResponseBytes {get;set;}
    public CertificateExtractionScope[] Scopes {get;set;}=[];
    private static bool Text(string? value,int max=256)=>CertificateExtractionPolicy.Text(value,max);
    private static bool Version(string? value,int max)=>Text(value,max) && value!.All(c=>char.IsAsciiLetterOrDigit(c)||c is '-' or '.' or '_' or ':' or '/' or '@');
    internal bool Configured(IHostEnvironment environment)=>Enabled && Version(PolicyVersion,80) && new[]{ProcessingReference,StorageReference,Region,PrivacyReference,RetentionReference,ConfidenceReference,ModelVersion,PromptVersion}.All(v=>Text(v)) &&
        SourceVersion==CertificateExtractionPolicy.SourceVersion && Version(ModelVersion,128) && Version(PromptVersion,128) && LowConfidenceThreshold is >=0 and <=1 && TimeoutSeconds is >=1 and <=90 && ReviewSeconds is >=30 and <=600 &&
        MaxBytes is >=1 and <=5242880 && MaxResponseBytes is >=1024 and <=65536 &&
        SecretReference is {Length:>0 and <=80} && char.IsAsciiLetterUpper(SecretReference[0]) && SecretReference.All(c=>char.IsAsciiLetterUpper(c)||char.IsAsciiDigit(c)||c=='_') &&
        Uri.TryCreate(Endpoint,UriKind.Absolute,out var uri) && (uri.Scheme=="https" || environment.IsEnvironment("Test") && uri.Scheme=="http" && uri.IsLoopback) &&
        uri.UserInfo.Length==0 && uri.Query.Length==0 && uri.Fragment.Length==0 && Text(AllowedHost) && string.Equals(uri.DnsSafeHost,AllowedHost,StringComparison.OrdinalIgnoreCase) &&
        Scopes is {Length:>0 and <=256} && Scopes.All(s=>s!=null && s.TenantId!=Guid.Empty && s.ActorId!=Guid.Empty && s.VesselId!=Guid.Empty && s.Scope is "Vessel" or "Crew") && Scopes.Distinct().Count()==Scopes.Length;
    internal bool Allows(IHostEnvironment environment,Guid tenant,Guid actor,Guid vessel,string scope)=>Configured(environment) && Scopes.Contains(new(tenant,actor,vessel,scope)) &&
        (scope!="Crew" || Text(PersonalProcessingReference));
    internal string Fingerprint()=>RegisterStorage.Hash(JsonSerializer.Serialize(this));
    internal CertificateExtractionSettings Snapshot()=>new(){Enabled=Enabled,Endpoint=Endpoint,AllowedHost=AllowedHost,SecretReference=SecretReference,PolicyVersion=PolicyVersion,ProcessingReference=ProcessingReference,
        StorageReference=StorageReference,Region=Region,PrivacyReference=PrivacyReference,PersonalProcessingReference=PersonalProcessingReference,RetentionReference=RetentionReference,
        ConfidenceReference=ConfidenceReference,SourceVersion=SourceVersion,ModelVersion=ModelVersion,PromptVersion=PromptVersion,LowConfidenceThreshold=LowConfidenceThreshold,
        TimeoutSeconds=TimeoutSeconds,ReviewSeconds=ReviewSeconds,MaxBytes=MaxBytes,MaxResponseBytes=MaxResponseBytes,Scopes=Scopes.Select(s=>s with{}).ToArray()};
}
public sealed class CertificateExtractionUnavailableException : Exception
{
    public CertificateExtractionUnavailableException():base("certificate_extraction_unavailable"){}
}
// Explicit provider protocol, not an OCR-success stub. No provider is selected or
// activated by default, and only the caller's pinned current file bytes are sent.
public sealed class CertificateExtractionProvider(IHttpClientFactory clients,IHostEnvironment environment)
{
    private static readonly JsonSerializerOptions Json=new(JsonSerializerDefaults.Web){PropertyNameCaseInsensitive=false,MaxDepth=5,UnmappedMemberHandling=System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow};
    internal async Task<CertificateRawExtraction> Extract(CertificateExtractionSettings policy,byte[] bytes,string mime,string checksum,CancellationToken ct)
    {
        if(!policy.Configured(environment) || bytes.Length==0 || bytes.Length>policy.MaxBytes || mime is not("application/pdf" or "image/png" or "image/jpeg"))throw new CertificateExtractionUnavailableException();
        string? secret;
        try{secret=OperatorSecretReferences.Resolve(policy.SecretReference,Environment.GetEnvironmentVariable);}catch(InvalidOperationException){throw new CertificateExtractionUnavailableException();}
        if(secret==null || secret.Length>8192 || secret.Any(c=>!char.IsAsciiLetterOrDigit(c) && c is not ('-' or '.' or '_' or '~' or '+' or '/' or '=')))throw new CertificateExtractionUnavailableException();
        using var deadline=CancellationTokenSource.CreateLinkedTokenSource(ct);deadline.CancelAfter(TimeSpan.FromSeconds(policy.TimeoutSeconds));
        using var message=new HttpRequestMessage(HttpMethod.Post,policy.Endpoint);
        message.Headers.Authorization=new AuthenticationHeaderValue("Bearer",secret);message.Headers.Accept.Add(new("application/json"));
        using var content=new MultipartFormDataContent();
        var metadata=JsonSerializer.Serialize(new{protocol=CertificateExtractionPolicy.Protocol,modelVersion=policy.ModelVersion,promptVersion=policy.PromptVersion,inputSha256=checksum,fields=CertificateExtractionPolicy.Fields},Json);
        content.Add(new StringContent(metadata,System.Text.Encoding.UTF8,"application/json"),"contract");
        var document=new ByteArrayContent(bytes);document.Headers.ContentType=new(mime);content.Add(document,"document","source.document");message.Content=content;
        try{
            using var client=clients.CreateClient("CertificateExtraction");
            using var response=await client.SendAsync(message,HttpCompletionOption.ResponseHeadersRead,deadline.Token);
            if(!response.IsSuccessStatusCode || response.Content.Headers.ContentType?.MediaType!="application/json" || response.Content.Headers.ContentEncoding.Count>0 ||
                response.Content.Headers.ContentLength>policy.MaxResponseBytes)throw new CertificateExtractionUnavailableException();
            await using var stream=await response.Content.ReadAsStreamAsync(deadline.Token);using var bounded=new MemoryStream();
            await SecureFilePolicy.CopyAsync(stream,bounded,policy.MaxResponseBytes,deadline.Token);
            using var parsed=JsonDocument.Parse(bounded.ToArray(),new(){MaxDepth=5});ApplicabilityEngine.RejectDuplicateKeys(parsed.RootElement);
            var raw=parsed.RootElement.Deserialize<CertificateRawExtraction>(Json);
            if(!CertificateExtractionPolicy.ValidRaw(raw,checksum,policy.ModelVersion!,policy.PromptVersion!))throw new CertificateExtractionUnavailableException();
            return raw!;
        }catch(OperationCanceledException)when(!ct.IsCancellationRequested){throw new CertificateExtractionUnavailableException();}
        catch(Exception ex)when(ex is HttpRequestException or JsonException or InvalidDataException or IOException){throw new CertificateExtractionUnavailableException();}
    }
}

internal static class CertificateExtractionRegistration
{
    internal static void AddCertificateExtraction(this IServiceCollection services,IConfiguration configuration)
    {
        services.AddSingleton(configuration.GetSection("CertificateExtraction").Get<CertificateExtractionSettings>(o=>o.ErrorOnUnknownConfiguration=true)??new());
        services.AddSingleton<CertificateExtractionProvider>();
        services.AddHttpClient("CertificateExtraction",client=>client.Timeout=Timeout.InfiniteTimeSpan)
            .ConfigurePrimaryHttpMessageHandler(sp=> {
                var caPath=configuration["DevelopmentLab:CertificateExtractionCaPath"];
                if(caPath!=null) {
                    var policy=sp.GetRequiredService<CertificateExtractionSettings>();
                    return DevelopmentCertificateExtractionTransport.Create(sp.GetRequiredService<IHostEnvironment>(),policy.Endpoint,policy.AllowedHost,caPath);
                }
                return new SocketsHttpHandler{AllowAutoRedirect=false,UseCookies=false,UseProxy=false,AutomaticDecompression=System.Net.DecompressionMethods.None,ConnectTimeout=TimeSpan.FromSeconds(10)};
            })
            .RedactLoggedHeaders(_=>true);
    }
}
