using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Api;
using Poseidon.Domain;
using Xunit;

namespace Poseidon.Api.Tests;

// DEV045/T-S6-003 (Integration, real PostgreSQL): with the in-process
// LocalTextLayer provider, an accepted PNG certificate photo is an explicit,
// audited OCR capability gap. No HTTP peer is contacted, nothing is mutated and
// no candidate, value or document content is returned.
public sealed partial class DirectoryWorkspaceTests
{
    private CertificateExtractionSettings LocalExtractionPolicy() {
        var p=CertificateLocalExtractionProviderTests.LocalPolicy();
        p.Scopes=[new(SeedVessel.TenantId,actorId,SeedVessel.Id,"Vessel")];return p;
    }
    [Fact]
    public async Task Local_text_layer_reports_image_certificate_as_audited_ocr_capability_gap_without_network_or_mutation() {
        var root=ExtractionRoot();using var scanner=new ScanPeer("stream: OK\0");var p=LocalExtractionPolicy();using var peer=new ExtractionPeer{Policy=p};
        try {
            await using var host=ExtractionHost(root,scanner.Port,p,peer);using var client=Client(host);ExtractionPermissions(host.Services);await Token(client);
            var row=await ExtractionSource(client);await using var db=Db();
            using var response=await Send(client,ExtractionPath(row),new CertificateExtractCommand(row.Version));
            Assert.Equal(HttpStatusCode.ServiceUnavailable,response.StatusCode);
            var body=await response.Content.ReadAsStringAsync(Ct);Assert.Equal("ocr_capability_unavailable",JsonDocument.Parse(body).RootElement.GetProperty("code").GetString());
            Assert.DoesNotContain("MASKED",body);Assert.Equal(0,peer.Requests);
            Assert.Equal(1,await db.VesselCertificateRevisions.CountAsync(r=>r.CertificateId==row.Id,Ct));
        }finally{RemoveExtractionFiles(root);}
    }
}
