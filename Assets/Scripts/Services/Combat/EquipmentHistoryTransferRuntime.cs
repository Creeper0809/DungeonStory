using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Owns lineage-transfer work orders and one-time regional seal claims.
/// Equipment payloads remain authoritative in the physical item repository.
/// </summary>
public sealed class EquipmentHistoryTransferRuntime
{
    private readonly IItemInstanceRepository itemInstances;
    private readonly ICombatEquipmentCatalog equipmentCatalog;
    private readonly BlueprintResearchRuntime research;
    private readonly IEquipmentPhysicalItemGateway physicalItems;
    private readonly CombatEquipmentPhysicalStateWriter physicalState;
    private readonly CombatEquipmentLoadoutStore loadouts;
    private readonly CombatEquipmentRuntimeStateStore stateStore;

    private List<EquipmentHistoryTransferOrder> orders =>
        stateStore.Current.HistoryTransferOrders;
    private HashSet<string> claimedRegionIds =>
        stateStore.Current.ClaimedLineageSealRegionIds;

    private IDictionary<string, CombatEquipmentInstance> EquipmentInstances =>
        itemInstances.EquipmentInstances;

    public EquipmentHistoryTransferRuntime(
        IItemInstanceRepository itemInstances,
        ICombatEquipmentCatalog equipmentCatalog,
        ProgressionSceneRuntimeReferences progressionRuntimes,
        IEquipmentPhysicalItemGateway physicalItems,
        CombatEquipmentPhysicalStateWriter physicalState,
        CombatEquipmentLoadoutStore loadouts,
        CombatEquipmentRuntimeStateStore stateStore)
    {
        this.itemInstances = itemInstances
            ?? throw new ArgumentNullException(nameof(itemInstances));
        this.equipmentCatalog = equipmentCatalog
            ?? throw new ArgumentNullException(nameof(equipmentCatalog));
        research = (progressionRuntimes
                ?? throw new ArgumentNullException(nameof(progressionRuntimes)))
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(EquipmentHistoryTransferRuntime)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        this.physicalItems = physicalItems
            ?? throw new ArgumentNullException(nameof(physicalItems));
        this.physicalState = physicalState
            ?? throw new ArgumentNullException(nameof(physicalState));
        this.loadouts = loadouts
            ?? throw new ArgumentNullException(nameof(loadouts));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public IReadOnlyList<EquipmentHistoryTransferOrder> Snapshots =>
        orders.Select(order => order.Clone()).ToArray();

    public IReadOnlyList<EquipmentHistoryTransferOrder> CaptureOrders() =>
        orders
            .Where(order => order != null && !order.completed)
            .Select(order => order.Clone())
            .ToArray();

    public IReadOnlyCollection<string> CaptureClaimedRegionIds() =>
        claimedRegionIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();

    public bool TryQueue(
        string sourceEquipmentInstanceId,
        string targetEquipmentInstanceId,
        string lineageSealStackId,
        BuildableObject facility,
        out EquipmentHistoryTransferOrder order,
        out DomainFailure failure)
    {
        order = null;
        if (!TryRequireLineageFacility(
                facility,
                out string facilityId,
                out string destinationId,
                out failure))
        {
            return false;
        }
        if (!HasCompletedResearch("research:equipment:lineage-binding"))
        {
            failure = new DomainFailure(
                FailureCode.RequiredResearchUnavailable,
                "research:equipment:lineage-binding",
                "facility:equipment:lineage-archive");
            return false;
        }
        if (!EquipmentInstances.TryGetValue(
                sourceEquipmentInstanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance source)
            || !EquipmentInstances.TryGetValue(
                targetEquipmentInstanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance target)
            || ReferenceEquals(source, target)
            || !equipmentCatalog.TryGet(source.definitionId,
                out CombatEquipmentDefinitionSO sourceDefinition)
            || !equipmentCatalog.TryGet(target.definitionId,
                out CombatEquipmentDefinitionSO targetDefinition))
        {
            failure = new DomainFailure(FailureCode.EquipmentInstanceMissing);
            return false;
        }
        if (sourceDefinition.LineageKind != targetDefinition.LineageKind)
        {
            failure = new DomainFailure(FailureCode.EquipmentLineageMismatch);
            return false;
        }
        if (source.moduleSlots?.Any(slot => slot != null
                && !string.IsNullOrWhiteSpace(slot.moduleInstanceId)) == true)
        {
            failure = new DomainFailure(FailureCode.HistorySourceHasModules);
            return false;
        }
        if (orders.Any(candidate => candidate != null
            && !candidate.completed
            && (candidate.sourceEquipmentInstanceId == source.instanceId
                || candidate.targetEquipmentInstanceId == source.instanceId
                || candidate.sourceEquipmentInstanceId == target.instanceId
                || candidate.targetEquipmentInstanceId == target.instanceId)))
        {
            failure = new DomainFailure(FailureCode.HistoryTransferAlreadyActive);
            return false;
        }

        IReadOnlyList<WorldItemStackSnapshot> physicalStacks =
            physicalItems.GetAllStacks();
        if (!HasLinkedPhysicalStack(source, physicalStacks, destinationId)
            || !HasLinkedPhysicalStack(target, physicalStacks, destinationId))
        {
            failure = new DomainFailure(
                FailureCode.HistoryTransferEquipmentMissing);
            return false;
        }

        WorldItemStackSnapshot seal = physicalStacks
            .FirstOrDefault(stack => string.Equals(
                    stack.StackId,
                    lineageSealStackId?.Trim() ?? string.Empty,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.ItemId,
                    EquipmentProgressionItemIds.LineageSeal,
                    StringComparison.Ordinal)
                && stack.Quantity > 0
                && !stack.Forbidden
                && stack.AvailableQuantity > 0
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal));
        if (seal == null || string.IsNullOrWhiteSpace(seal.StackId))
        {
            failure = new DomainFailure(FailureCode.LineageSealMissing);
            return false;
        }
        if (orders.Any(candidate => candidate != null
            && !candidate.completed
            && string.Equals(
                candidate.lineageSealStackId,
                seal.StackId,
                StringComparison.Ordinal)))
        {
            failure = new DomainFailure(FailureCode.HistoryTransferAlreadyActive);
            return false;
        }

        order = new EquipmentHistoryTransferOrder
        {
            orderId = $"lineage-transfer:{Guid.NewGuid():N}",
            sourceEquipmentInstanceId = source.instanceId,
            targetEquipmentInstanceId = target.instanceId,
            lineageSealStackId = seal.StackId,
            facilityPersistentId = facilityId,
            destinationId = destinationId,
            requiredWork = 120f
        };
        orders.Add(order);
        failure = DomainFailure.None;
        return true;
    }

    public bool ApplyWork(
        string orderId,
        float work,
        BuildableObject facility,
        out bool completed,
        out DomainFailure failure)
    {
        completed = false;
        EquipmentHistoryTransferOrder order = orders.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(candidate.orderId, orderId, StringComparison.Ordinal));
        if (order == null || order.completed || work <= 0f)
        {
            failure = new DomainFailure(FailureCode.HistoryTransferOrderMissing);
            return false;
        }
        if (!TryRequireLineageFacility(
                facility,
                out string facilityId,
                out string destinationId,
                out failure)
            || !string.Equals(
                order.facilityPersistentId,
                facilityId,
                StringComparison.Ordinal)
            || !string.Equals(
                order.destinationId,
                destinationId,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.EquipmentProgressionFacilityUnavailable);
            return false;
        }
        if (!EquipmentInstances.TryGetValue(
                order.sourceEquipmentInstanceId,
                out CombatEquipmentInstance source)
            || !EquipmentInstances.TryGetValue(
                order.targetEquipmentInstanceId,
                out CombatEquipmentInstance target))
        {
            failure = new DomainFailure(FailureCode.HistoryTransferEquipmentMissing);
            return false;
        }
        IReadOnlyList<WorldItemStackSnapshot> physicalStacks =
            physicalItems.GetAllStacks();
        if (!HasLinkedPhysicalStack(source, physicalStacks, destinationId)
            || !HasLinkedPhysicalStack(target, physicalStacks, destinationId))
        {
            failure = new DomainFailure(FailureCode.HistoryTransferEquipmentMissing);
            return false;
        }
        if (source.moduleSlots?.Any(slot => slot != null
                && !string.IsNullOrWhiteSpace(slot.moduleInstanceId)) == true)
        {
            failure = new DomainFailure(FailureCode.HistorySourceHasModules);
            return false;
        }
        WorldItemStackSnapshot seal = physicalStacks.FirstOrDefault(stack =>
            stack != null
            && stack.Quantity > 0
            && !stack.Forbidden
            && stack.AvailableQuantity > 0
            && stack.State == WorldItemStackState.FacilityBuffer
            && string.Equals(
                stack.StackId,
                order.lineageSealStackId,
                StringComparison.Ordinal)
            && string.Equals(
                stack.ItemId,
                EquipmentProgressionItemIds.LineageSeal,
                StringComparison.Ordinal)
            && string.Equals(
                stack.DestinationId,
                destinationId,
                StringComparison.Ordinal));
        if (seal == null)
        {
            failure = new DomainFailure(FailureCode.HistoryTransferSealMissing);
            return false;
        }

        order.completedWork = Mathf.Min(
            order.requiredWork,
            order.completedWork + work);
        if (order.completedWork + 0.001f < order.requiredWork)
        {
            failure = DomainFailure.None;
            return true;
        }

        string sourceStackId = source.sourceStackId;
        EquipmentEvolutionState inheritedHistory = source.evolution?.Clone()
            ?? new EquipmentEvolutionState();
        if (string.IsNullOrWhiteSpace(sourceStackId)
            || !physicalItems.DeleteStack(sourceStackId))
        {
            failure = new DomainFailure(
                FailureCode.HistoryTransferEquipmentMissing);
            return false;
        }
        if (!physicalItems.TryConsumeStackQuantity(
                order.lineageSealStackId,
                1,
                out _))
        {
            failure = new DomainFailure(FailureCode.HistoryTransferSealMissing);
            return false;
        }

        target.evolution = inheritedHistory;
        physicalState.Persist(target);
        loadouts.RemoveEquipment(source.instanceId);
        EquipmentInstances.Remove(source.instanceId);
        order.completed = true;
        completed = true;
        failure = DomainFailure.None;
        return true;
    }

    public bool TryClaimRegionSeal(string regionId)
    {
        string normalized = regionId?.Trim() ?? string.Empty;
        return normalized.Length > 0 && claimedRegionIds.Add(normalized);
    }

    internal void PopulateRestoreState(
        CombatEquipmentRuntimeState target,
        IEnumerable<EquipmentHistoryTransferOrder> savedOrders,
        IEnumerable<string> savedClaimedRegionIds)
    {
        CombatEquipmentRuntimeState requiredTarget = target
            ?? throw new ArgumentNullException(nameof(target));
        foreach (EquipmentHistoryTransferOrder source in
                 savedOrders ?? Array.Empty<EquipmentHistoryTransferOrder>())
        {
            if (source == null || source.completed
                || string.IsNullOrWhiteSpace(source.orderId)
                || !EquipmentInstances.ContainsKey(source.sourceEquipmentInstanceId)
                || !EquipmentInstances.ContainsKey(source.targetEquipmentInstanceId)
                || requiredTarget.HistoryTransferOrders.Any(
                    order => order.orderId == source.orderId))
            {
                continue;
            }
            requiredTarget.HistoryTransferOrders.Add(source.Clone());
        }
        foreach (string regionId in savedClaimedRegionIds ?? Array.Empty<string>())
        {
            if (!string.IsNullOrWhiteSpace(regionId))
            {
                requiredTarget.ClaimedLineageSealRegionIds.Add(regionId.Trim());
            }
        }
    }

    private bool HasCompletedResearch(string researchId)
    {
        return string.IsNullOrWhiteSpace(researchId)
            || research.State.Projects.IsCompleted(new ResearchProjectId(researchId));
    }

    private static bool HasLinkedPhysicalStack(
        CombatEquipmentInstance instance,
        IEnumerable<WorldItemStackSnapshot> stacks,
        string destinationId)
    {
        return instance != null
            && !string.IsNullOrWhiteSpace(instance.sourceStackId)
            && (stacks ?? Array.Empty<WorldItemStackSnapshot>()).Any(stack =>
                stack != null
                && stack.Quantity > 0
                && !stack.Forbidden
                && stack.AvailableQuantity > 0
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    stack.StackId,
                    instance.sourceStackId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.ItemInstanceId,
                    instance.instanceId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.ItemId,
                    PhysicalItemIds.ForEquipment(instance.definitionId),
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal));
    }

    private static bool TryRequireLineageFacility(
        BuildableObject facility,
        out string facilityId,
        out string destinationId,
        out DomainFailure failure)
    {
        facilityId = facility?.PersistentInstanceId.Value ?? string.Empty;
        destinationId = EquipmentProgressionFacilityContract
            .GetLocalBufferDestinationId(facility);
        if (!EquipmentProgressionFacilityContract.Matches(
                facility,
                EquipmentProgressionWorkstationTags.LineageArchive))
        {
            failure = new DomainFailure(
                FailureCode.EquipmentProgressionFacilityUnavailable);
            return false;
        }
        failure = DomainFailure.None;
        return true;
    }
}
