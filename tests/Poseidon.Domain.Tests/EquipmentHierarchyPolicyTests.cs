using Poseidon.Domain;
using Xunit;

namespace Poseidon.Domain.Tests;

// T-S8-001: source-defined invalid cycles, vessel scope and parent-child structure.
// Authored for the deferred larger foundation gate; no executed-result claim.
public sealed class EquipmentHierarchyPolicyTests
{
    private static readonly Guid Tenant = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid Vessel = Guid.Parse("20000000-0000-0000-0000-000000000001");
    private static readonly Guid A = Guid.Parse("30000000-0000-0000-0000-000000000001");
    private static readonly Guid B = Guid.Parse("30000000-0000-0000-0000-000000000002");
    private static readonly Guid C = Guid.Parse("30000000-0000-0000-0000-000000000003");
    private static EquipmentHierarchyNode Node(Guid id, Guid? parent = null) => new(Tenant, Vessel, id, parent);
    private static EquipmentHierarchyValidation Check(params EquipmentHierarchyNode[] nodes) =>
        EquipmentHierarchyPolicy.Validate(Tenant, Vessel, nodes);

    [Fact]
    public void Empty_and_multiple_root_forests_do_not_invent_required_asset_data()
    {
        Assert.Equal(EquipmentHierarchyValidation.Valid, Check());
        Assert.Equal(EquipmentHierarchyValidation.Valid, Check(Node(A), Node(B), Node(C, A)));
    }

    [Fact]
    public void Parent_input_order_is_irrelevant_and_shared_ancestors_are_valid()
    {
        Assert.Equal(EquipmentHierarchyValidation.Valid, Check(Node(C, A), Node(B, A), Node(A)));
        Assert.Equal(EquipmentHierarchyValidation.Valid, Check(Node(A), Node(B, A), Node(C, B)));
    }

    [Fact]
    public void Self_parent_and_longer_cycle_are_rejected()
    {
        Assert.Equal(EquipmentHierarchyValidation.Cycle, Check(Node(A, A)));
        Assert.Equal(EquipmentHierarchyValidation.Cycle, Check(Node(A, B), Node(B, C), Node(C, A)));
    }

    [Fact]
    public void A_cycle_in_a_disconnected_component_cannot_hide_behind_a_valid_tree()
    {
        Assert.Equal(EquipmentHierarchyValidation.Cycle, Check(Node(A), Node(B, C), Node(C, B)));
    }

    [Fact]
    public void Missing_parent_is_an_invalid_reference_not_an_implicit_root()
    {
        Assert.Equal(EquipmentHierarchyValidation.MissingParent, Check(Node(A, B)));
    }

    [Fact]
    public void Duplicate_identifiers_cannot_overwrite_a_parent_and_hide_a_cycle()
    {
        Assert.Equal(EquipmentHierarchyValidation.DuplicateNode, Check(Node(A, B), Node(B, A), Node(A)));
    }

    [Fact]
    public void Foreign_vessel_or_tenant_parent_cannot_join_the_snapshot()
    {
        Assert.Equal(EquipmentHierarchyValidation.InvalidScope,
            Check(Node(A, B), Node(B) with { VesselId = Guid.NewGuid() }));
        Assert.Equal(EquipmentHierarchyValidation.InvalidScope,
            Check(Node(A, B), Node(B) with { TenantId = Guid.NewGuid() }));
    }

    [Fact]
    public void Missing_scope_and_empty_identifiers_fail_closed()
    {
        Assert.Equal(EquipmentHierarchyValidation.InvalidScope,
            EquipmentHierarchyPolicy.Validate(Guid.Empty, Vessel, []));
        Assert.Equal(EquipmentHierarchyValidation.InvalidScope,
            EquipmentHierarchyPolicy.Validate(Tenant, Guid.Empty, []));
        Assert.Equal(EquipmentHierarchyValidation.InvalidNodeIdentity, Check(Node(Guid.Empty)));
        Assert.Equal(EquipmentHierarchyValidation.InvalidNodeIdentity, Check(Node(A, Guid.Empty)));
        Assert.Equal(EquipmentHierarchyValidation.InvalidNodeIdentity, Check(new EquipmentHierarchyNode[] { null! }));
    }

    [Fact]
    public void Reparenting_to_a_descendant_is_rejected_without_modifying_original_nodes()
    {
        EquipmentHierarchyNode[] nodes = [Node(A), Node(B, A), Node(C, B)];
        Assert.Equal(EquipmentHierarchyValidation.Cycle,
            EquipmentHierarchyPolicy.ValidateReparent(Tenant, Vessel, nodes, A, C));
        Assert.Null(nodes[0].ParentId);
        Assert.Equal(A, nodes[1].ParentId);
        Assert.Equal(B, nodes[2].ParentId);
    }

    [Fact]
    public void Moving_a_subtree_to_another_root_or_detaching_it_is_valid()
    {
        EquipmentHierarchyNode[] nodes = [Node(A), Node(B), Node(C, A)];
        Assert.Equal(EquipmentHierarchyValidation.Valid,
            EquipmentHierarchyPolicy.ValidateReparent(Tenant, Vessel, nodes, C, B));
        Assert.Equal(EquipmentHierarchyValidation.Valid,
            EquipmentHierarchyPolicy.ValidateReparent(Tenant, Vessel, nodes, C, null));
        Assert.Equal(A, nodes[2].ParentId);
    }

    [Fact]
    public void Unknown_node_or_parent_is_not_silently_inserted_by_reparenting()
    {
        Assert.Equal(EquipmentHierarchyValidation.NodeNotFound,
            EquipmentHierarchyPolicy.ValidateReparent(Tenant, Vessel, [Node(A)], B, null));
        Assert.Equal(EquipmentHierarchyValidation.MissingParent,
            EquipmentHierarchyPolicy.ValidateReparent(Tenant, Vessel, [Node(A)], A, B));
    }

    [Fact]
    public void Individually_valid_reparents_need_a_fresh_transactional_snapshot()
    {
        EquipmentHierarchyNode[] original = [Node(A), Node(B)];
        Assert.Equal(EquipmentHierarchyValidation.Valid,
            EquipmentHierarchyPolicy.ValidateReparent(Tenant, Vessel, original, A, B));
        Assert.Equal(EquipmentHierarchyValidation.Valid,
            EquipmentHierarchyPolicy.ValidateReparent(Tenant, Vessel, original, B, A));
        EquipmentHierarchyNode[] afterFirstCommit = [Node(A, B), Node(B)];
        Assert.Equal(EquipmentHierarchyValidation.Cycle,
            EquipmentHierarchyPolicy.ValidateReparent(Tenant, Vessel, afterFirstCommit, B, A));
    }

    [Fact]
    public void Deep_imported_hierarchy_uses_no_recursion_or_invented_depth_limit()
    {
        var nodes = new List<EquipmentHierarchyNode>(20000);
        Guid? parent = null;
        for (var i = 0; i < 20000; i++)
        {
            var id = Guid.NewGuid();
            nodes.Add(Node(id, parent));
            parent = id;
        }
        nodes.Reverse();
        Assert.Equal(EquipmentHierarchyValidation.Valid,
            EquipmentHierarchyPolicy.Validate(Tenant, Vessel, nodes));
        Assert.Equal(EquipmentHierarchyValidation.Cycle,
            EquipmentHierarchyPolicy.ValidateReparent(Tenant, Vessel, nodes, nodes[^1].Id, nodes[0].Id));
    }
}
