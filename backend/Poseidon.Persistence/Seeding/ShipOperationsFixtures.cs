using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Poseidon.Domain;

namespace Poseidon.Persistence.Seeding;

// Optional incomplete fictional module drafts. Nothing is activated at startup.
// No BDN, position, supplier, evidence, verification or user receipt is seeded.
public static class ShipOperationsFixtures
{
    public static Guid Id(Guid vessel,string kind)=>new(SHA256.HashData(Encoding.UTF8.GetBytes("poseidon.synthetic.ship-operations.v1:"+vessel.ToString("D")+":"+kind)).AsSpan(0,16));
    // Two fictional consumption drafts per Ship exercise the DEV-100 ledger
    // review aid: the second declared ROB-before intentionally does not
    // continue the first ROB-after, so the ledger shows REVIEW_REQUIRED. No BDN,
    // supplier, tank, evidence or verification is declared; nothing is corrected.
    public const string LedgerSlotA="Fuel-ledger-a",LedgerSlotB="Fuel-ledger-b";
    private static Dictionary<string,ShipOpsFact> Consumption(string date,string before,string consumed,string after)=>new() {
        ["record_date"]=new("Known",date),["record_type"]=new("Known","Consumption"),["fuel_type"]=new("Known","VLSFO"),
        ["rob_before_mt"]=new("Known",before),["quantity_received_mt"]=new("Known","0"),["quantity_consumed_mt"]=new("Known",consumed),["rob_after_mt"]=new("Known",after)};
    private static IEnumerable<(string Kind,string Slot,Dictionary<string,ShipOpsFact> Fields)> Drafts() {
        foreach(var kind in ShipOperationsPolicy.Kinds)yield return (kind,kind,new());
        yield return ("Fuel",LedgerSlotA,Consumption("2026-01-01T12:00:00Z","120.0","4.5","115.5"));
        yield return ("Fuel",LedgerSlotB,Consumption("2026-01-02T12:00:00Z","115.0","4.0","111.0"));
    }
    public static async Task AddMissingAsync(PoseidonDbContext db,IEnumerable<Vessel> vessels,CancellationToken ct=default) {
        if(!db.IsAdministrativeImport || db.Database.CurrentTransaction==null)throw new UnauthorizedAccessException("Synthetic ship operations require an administrative audited transaction.");
        foreach(var vessel in vessels.Where(v=>v.Kind==VesselKind.Ship))foreach(var (kind,slot,declared) in Drafts()) {
            var id=Id(vessel.Id,slot);if(await db.ShipOperations.AnyAsync(r=>r.TenantId==vessel.TenantId && r.Id==id,ct))continue;
            if(!await db.Vessels.AnyAsync(v=>v.TenantId==vessel.TenantId && v.Id==vessel.Id && v.Kind==VesselKind.Ship,ct))throw new InvalidOperationException("Synthetic ship scope is unavailable.");
            if(!ShipOperationsPolicy.TryNormalize(kind,declared,out var fields,out _))throw new InvalidOperationException("Synthetic draft schema unavailable.");
            var json=JsonSerializer.Serialize(fields,new JsonSerializerOptions(JsonSerializerDefaults.Web));var now=DateTimeOffset.UtcNow;
            var row=new ShipOperation{TenantId=vessel.TenantId,VesselId=vessel.Id,Id=id,Kind=kind,FieldsJson=json,SourceVersion=ShipOperationsPolicy.SourceVersion,
                Hash=RegisterStorage.Hash("SyntheticUnverified:"+vessel.TenantId+":"+vessel.Id+":"+slot+":"+json),Provenance="SyntheticUnverified",SavedAt=now,
                OccurredAt=fields.TryGetValue("record_date",out var date) && ShipOperationsPolicy.TryInstant(date.Value,out var at)?at:null};
            db.ShipOperations.Add(row);await db.SaveChangesAsync(ct);
            db.ShipOperationRevisions.Add(new(){TenantId=row.TenantId,VesselId=row.VesselId,RecordId=id,Kind=kind,SourceVersion=row.SourceVersion,Revision=1,RecordVersion=row.Version,
                FieldsJson=json,Hash=row.Hash,Provenance=row.Provenance,SavedAt=now,OccurredAt=row.OccurredAt});await db.SaveChangesAsync(ct);
        }
        // The caller commits; existing operator state is never updated or deleted.
    }
}
