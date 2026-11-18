using System.Text.Json.Serialization;

namespace Poseidon.Domain;

// Optional engineering lineage for DEV-100/101, outside their canonical field
// dictionaries. A pin records an actual saved declaration, never its approval.
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record ShipOperationOriginPin(string Kind, Guid Id, long Revision, uint Version, string Hash, string SourceVersion);
