namespace Poseidon.Domain;

public sealed record EquipmentHierarchyNode(Guid TenantId, Guid VesselId, Guid Id, Guid? ParentId);

public enum EquipmentHierarchyValidation
{
    Valid,
    InvalidScope,
    InvalidNodeIdentity,
    DuplicateNode,
    MissingParent,
    Cycle,
    NodeNotFound
}

// DEV-052 / T-S8-001; 14.01 paragraph105: Asset.parent_asset_id within its vessel.
// The caller supplies the complete current vessel snapshot after authorization. Apply a
// mutation under a tenant/vessel transaction lock (or equivalent serialization): two
// independently valid snapshots do not establish that their concurrent combination is valid.
// This policy validates structure; it does not grant access or declare equipment fit for use.
public static class EquipmentHierarchyPolicy
{
    public static EquipmentHierarchyValidation Validate(
        Guid tenantId, Guid vesselId, IReadOnlyCollection<EquipmentHierarchyNode> nodes)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        if (tenantId == Guid.Empty || vesselId == Guid.Empty)
            return EquipmentHierarchyValidation.InvalidScope;

        var parents = new Dictionary<Guid, Guid?>(nodes.Count);
        foreach (var node in nodes)
        {
            if (node is null || node.Id == Guid.Empty || node.ParentId == Guid.Empty)
                return EquipmentHierarchyValidation.InvalidNodeIdentity;
            if (node.TenantId != tenantId || node.VesselId != vesselId)
                return EquipmentHierarchyValidation.InvalidScope;
            if (!parents.TryAdd(node.Id, node.ParentId))
                return EquipmentHierarchyValidation.DuplicateNode;
        }

        foreach (var parent in parents.Values)
            if (parent is Guid parentId && !parents.ContainsKey(parentId))
                return EquipmentHierarchyValidation.MissingParent;

        // Iterative traversal handles deep imported forests without recursion or an
        // invented business depth limit. Every edge is traversed at most once.
        var visited = new Dictionary<Guid, byte>(parents.Count);
        var chain = new List<Guid>();
        foreach (var start in parents.Keys)
        {
            if (visited.GetValueOrDefault(start) == 2)
                continue;
            chain.Clear();
            var current = start;
            while (true)
            {
                var state = visited.GetValueOrDefault(current);
                if (state == 2)
                    break;
                if (state == 1)
                    return EquipmentHierarchyValidation.Cycle;
                visited[current] = 1;
                chain.Add(current);
                if (parents[current] is not Guid parent)
                    break;
                current = parent;
            }
            foreach (var id in chain)
                visited[id] = 2;
        }
        return EquipmentHierarchyValidation.Valid;
    }

    public static EquipmentHierarchyValidation ValidateReparent(
        Guid tenantId, Guid vesselId, IReadOnlyCollection<EquipmentHierarchyNode> nodes,
        Guid nodeId, Guid? newParentId)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        if (nodeId == Guid.Empty || newParentId == Guid.Empty)
            return EquipmentHierarchyValidation.InvalidNodeIdentity;

        // Immutable candidate: rejecting a descendant/cross-scope parent cannot mutate
        // the caller's in-memory aggregate before its transaction decides the outcome.
        var candidate = new List<EquipmentHierarchyNode>(nodes.Count);
        var found = false;
        foreach (var node in nodes)
        {
            if (node is null)
                return EquipmentHierarchyValidation.InvalidNodeIdentity;
            if (node.Id == nodeId)
            {
                found = true;
                candidate.Add(node with { ParentId = newParentId });
            }
            else
                candidate.Add(node);
        }
        var result = Validate(tenantId, vesselId, candidate);
        return result != EquipmentHierarchyValidation.Valid ? result
            : found ? EquipmentHierarchyValidation.Valid : EquipmentHierarchyValidation.NodeNotFound;
    }
}
