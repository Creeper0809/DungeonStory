using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal sealed class ConveyorPayloadAdmissionPolicy
{
    private readonly IIndustrialInfrastructureTopologyRuntime topologyRuntime;
    private readonly IDungeonItemCatalogProvider catalog;
    private readonly ICombatEquipmentRuntime equipment;
    private readonly ISurvivalFoodQuery food;
    private readonly Func<IndustrialNodeDescriptor, ConveyorNodeRuntimeState>
        getNodeState;

    public ConveyorPayloadAdmissionPolicy(
        IIndustrialInfrastructureTopologyRuntime topologyRuntime,
        IDungeonItemCatalogProvider catalog,
        ICombatEquipmentRuntime equipment,
        ISurvivalFoodQuery food,
        Func<IndustrialNodeDescriptor, ConveyorNodeRuntimeState> getNodeState)
    {
        this.topologyRuntime = topologyRuntime
            ?? throw new ArgumentNullException(nameof(topologyRuntime));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
        this.food = food ?? throw new ArgumentNullException(nameof(food));
        this.getNodeState = getNodeState
            ?? throw new ArgumentNullException(nameof(getNodeState));
    }

    public bool CanEnter(string nodeId, ItemTransitStackSnapshot stack)
    {
        IndustrialTopologySnapshot topology = topologyRuntime.Current;
        if (!topology.Nodes.TryGetValue(nodeId, out IndustrialNodeDescriptor node))
        {
            return false;
        }

        ConveyorNodeRuntimeState runtimeFilter = getNodeState(node);
        if (!runtimeFilter.Enabled)
        {
            return false;
        }

        BuildingConveyorSegmentAbility staticFilter = node.Conveyor;
        if (stack.Forbidden
            && !(runtimeFilter.AllowForbidden
                || staticFilter?.allowForbidden == true))
        {
            return false;
        }

        if (!catalog.TryGetDefinition(
                stack.ItemId,
                out DungeonItemDefinition definition))
        {
            return runtimeFilter.ItemIds.Count == 0
                && runtimeFilter.StockCategories.Count == 0
                && (staticFilter?.allowedItemIds == null
                    || staticFilter.allowedItemIds.Length == 0)
                && (staticFilter?.allowedStockCategories == null
                    || staticFilter.allowedStockCategories.Length == 0);
        }

        bool runtimeAllows = runtimeFilter.ItemIds.Count == 0
            && runtimeFilter.StockCategories.Count == 0
            || runtimeFilter.ItemIds.Contains(stack.ItemId)
            || runtimeFilter.StockCategories.Contains(definition.StockCategory);
        bool staticAllows = staticFilter == null
            || (staticFilter.allowedItemIds == null
                || staticFilter.allowedItemIds.Length == 0)
            && (staticFilter.allowedStockCategories == null
                || staticFilter.allowedStockCategories.Length == 0)
            || staticFilter.allowedItemIds?.Contains(
                stack.ItemId,
                StringComparer.Ordinal) == true
            || staticFilter.allowedStockCategories?.Contains(
                definition.StockCategory) == true;
        return runtimeAllows
            && staticAllows
            && MatchesRuntimeMetadata(runtimeFilter, stack)
            && MatchesStaticMetadata(staticFilter, stack);
    }

    private bool MatchesRuntimeMetadata(
        ConveyorNodeRuntimeState filter,
        ItemTransitStackSnapshot stack)
    {
        return TryMatchEquipment(
                stack,
                filter.MaterialIds,
                filter.FilterQuality,
                filter.MinimumQuality,
                filter.MaximumQuality)
            && MatchesFreshness(
                stack,
                filter.FilterFreshness,
                filter.MinimumFreshness01,
                filter.MaximumFreshness01,
                filter.AllowContaminated);
    }

    private bool MatchesStaticMetadata(
        BuildingConveyorSegmentAbility filter,
        ItemTransitStackSnapshot stack)
    {
        return filter == null
            || TryMatchEquipment(
                    stack,
                    filter.allowedMaterialIds,
                    filter.filterQuality,
                    filter.minimumQuality,
                    filter.maximumQuality)
                && MatchesFreshness(
                    stack,
                    filter.filterFreshness,
                    filter.minimumFreshness01,
                    filter.maximumFreshness01,
                    filter.allowContaminated);
    }

    private bool TryMatchEquipment(
        ItemTransitStackSnapshot stack,
        ICollection<string> materialIds,
        bool filterQuality,
        CombatEquipmentQuality minimumQuality,
        CombatEquipmentQuality maximumQuality)
    {
        bool hasMaterialFilter = materialIds != null && materialIds.Count > 0;
        if (!hasMaterialFilter && !filterQuality)
        {
            return true;
        }

        if (!equipment.TryGetInstanceBySourceStack(
                stack.StackId.Value,
                out CombatEquipmentInstance instance))
        {
            return false;
        }

        if (hasMaterialFilter && !materialIds.Contains(instance.materialId))
        {
            return false;
        }

        return !filterQuality
            || (int)instance.quality >= (int)minimumQuality
            && (int)instance.quality <= (int)maximumQuality;
    }

    private bool MatchesFreshness(
        ItemTransitStackSnapshot stack,
        bool filterFreshness,
        float minimumFreshness01,
        float maximumFreshness01,
        bool allowContaminated)
    {
        if (!filterFreshness && allowContaminated)
        {
            return true;
        }

        bool contaminated = stack.Contamination > 0.001f;
        if (food.TryGetItemStatus(
                stack.StackId.Value,
                stack.ItemId,
                out SurvivalItemStatus status))
        {
            contaminated |= status.Contaminated;
            if (filterFreshness
                && (status.Freshness01
                        + 0.0001f < Mathf.Clamp01(minimumFreshness01)
                    || status.Freshness01
                        - 0.0001f > Mathf.Clamp01(maximumFreshness01)))
            {
                return false;
            }
        }
        else if (filterFreshness)
        {
            return false;
        }

        return allowContaminated || !contaminated;
    }
}
