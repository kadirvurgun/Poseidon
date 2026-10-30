namespace Poseidon.Api;

/// <summary>Technical API connectivity; never a vessel readiness or authorization decision.</summary>
public sealed record SystemStatus(string Service, string Status);
