using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

/// <summary>
/// Coordinates construction material supply, physical WIP custody and terminal
/// release through the existing input owner and material outbox. Order records
/// and revisions remain in the shared replaceable aggregate store.
/// </summary>
internal sealed class WorkOrderMaterialSupplyRuntime
{
    private readonly IWorldItemStackRuntime itemStackRuntime;
    private readonly IWorkforceReplanService workforceReplanService;
    private readonly IPhysicalItemSourcePublicationService materialSources;
    private readonly IWorkConstructionInputOwnerRuntime constructionInputs;
    private readonly WorkOrderAggregateStateStore stateStore;

    internal WorkOrderMaterialSupplyRuntime(
        IWorldItemStackRuntime itemStackRuntime,
        IWorkforceReplanService workforceReplanService,
        IPhysicalItemSourcePublicationService materialSources,
        IWorkConstructionInputOwnerRuntime constructionInputs,
        WorkOrderAggregateStateStore stateStore)
    {
        this.itemStackRuntime = itemStackRuntime
            ?? throw new ArgumentNullException(nameof(itemStackRuntime));
        this.workforceReplanService = workforceReplanService;
        this.materialSources = materialSources
            ?? throw new ArgumentNullException(nameof(materialSources));
        this.constructionInputs = constructionInputs;
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
    }

    internal bool TryPrepareConstructionMaterials(
        WorkOrderRecord order,
        BuildingSO building,
        ConstructionSite site,
        bool allowInstallationKit,
        out string failureReason)
    {
        failureReason = string.Empty;
        string installationKitItemId =
            FacilityInstallationKitItemIds.ForBuilding(building);
        if (allowInstallationKit
            && HasAvailableInstallationKit(installationKitItemId))
        {
            order.requiredItemMaterials[installationKitItemId] = 1;
            order.deliveredItemMaterials[installationKitItemId] = 0;
        }
        else
        {
            foreach (ItemAmountDefinition material
                     in building.GetConstructionMaterials())
            {
                order.requiredItemMaterials.Add(
                    material.ItemId,
                    material.Amount);
                order.deliveredItemMaterials.Add(material.ItemId, 0);
            }
        }

        if (order.requiredItemMaterials.Count == 0)
        {
            order.status = WorkOrderStatus.Ready;
        }
        else
        {
            BuildingInstanceId siteId = site.RequirePersistentInstanceId();
            order.constructionSitePersistentId = siteId.Value;
            if (constructionInputs == null
                || !constructionInputs.TryOpen(
                    ToConstructionInputDescriptor(order),
                    out WorkConstructionInputOwnerProjection projection,
                    out failureReason))
            {
                failureReason = constructionInputs == null
                    ? "work-construction-input-owner-runtime-missing"
                    : failureReason;
                return false;
            }
            order.materialBufferCapacityGrams = projection.CapacityGrams;
            order.materialMassAuthorityRevision =
                projection.MassAuthorityRevision;
            order.materialCapacityFingerprint =
                projection.CapacityFingerprint;
            RequestMissingMaterials(order);
        }
        return true;
    }

    internal bool TryReleaseForCancellation(
        WorkOrderRecord order,
        bool refundDeliveredMaterials)
    {
        if (!refundDeliveredMaterials
            && (order.requiredItemMaterials.Count > 0
                || CountPendingDestinationItems(order) > 0))
        {
            return false;
        }
        if (order.materialTransfer.HasCustody)
        {
            if (order.materialTransfer.Phase is
                    WorkOrderMaterialTransferPhase.InputCommitted
                    or WorkOrderMaterialTransferPhase.CustodyPublished
                && !WorkOrderMaterialOutbox.TryCommitOrResume(
                    order,
                    itemStackRuntime,
                    out _))
            {
                return false;
            }
            if (!WorkOrderMaterialOutbox.TryPublishRestitution(
                    order,
                    materialSources,
                    out _))
            {
                order.status = WorkOrderStatus.WaitingForOutputSpace;
                stateStore.InvalidateCandidates();
                return false;
            }
        }
        if (order.workTypeId == BuiltInWorkTypeIds.Construct
            && order.requiredItemMaterials.Count > 0)
        {
            WorkConstructionInputOwnerDescriptor descriptor =
                ToConstructionInputDescriptor(order);
            if (constructionInputs == null
                || !constructionInputs.TryPrepareTerminalRelease(
                    descriptor,
                    "work-construction-cancelled",
                    out _)
                || !constructionInputs.TryRevoke(descriptor, out _))
            {
                return false;
            }
        }
        else
        {
            itemStackRuntime.ReleaseStacksByDestination(
                order.materialDestinationId,
                order.position);
        }
        return true;
    }

    internal bool TryPrepareCompletion(
        WorkOrderRecord order,
        out WorkConstructionInputOwnerDescriptor descriptor,
        out string message)
    {
        descriptor = null;
        message = string.Empty;
        if (!WorkOrderMaterialOutbox.HasAcknowledgedCustody(order))
        {
            order.status = WorkOrderStatus.WaitingForMaterials;
            message = "construction material custody is not acknowledged";
            return false;
        }
        if (order.requiredItemMaterials.Count > 0)
        {
            descriptor = ToConstructionInputDescriptor(order);
            if (constructionInputs == null
                || !constructionInputs.TryPrepareTerminalRelease(
                    descriptor,
                    "work-construction-completed",
                    out message))
            {
                message = constructionInputs == null
                    ? "construction input-owner runtime missing"
                    : message;
                order.status = WorkOrderStatus.Blocked;
                return false;
            }
        }
        return true;
    }

    internal bool TryRetireAfterCompletion(
        WorkConstructionInputOwnerDescriptor descriptor,
        out string message) => constructionInputs.TryRevoke(descriptor, out message);

    internal bool TryPublishRestitution(WorkOrderRecord order, out string failureReason) =>
        WorkOrderMaterialOutbox.TryPublishRestitution(order, materialSources, out failureReason);

    internal void ReplaceForRestore()
    {
        string ownerFailure = string.Empty;
        if (constructionInputs == null
            || !constructionInputs.TryReplaceForRestore(
                stateStore.Writable.OrdersById.Values
                    .Where(value => value.workTypeId == BuiltInWorkTypeIds.Construct
                        && value.requiredItemMaterials.Count > 0
                        && value.terminalPhase == WorkOrderTerminalPhase.None)
                    .OrderBy(value => value.workOrderId, StringComparer.Ordinal)
                    .Select(ToConstructionInputDescriptor)
                    .ToArray(),
                out ownerFailure))
        {
            throw new InvalidOperationException(
                "Construction input-owner restore join failed: "
                + (constructionInputs == null
                    ? "owner-runtime-missing"
                    : ownerFailure));
        }
    }

    internal bool EnsureMaterialsReady(WorkOrderRecord order, out string failureReason)
    {
        failureReason = string.Empty;
        if (order.requiredItemMaterials.Count == 0)
        {
            order.status = WorkOrderStatus.Ready;
            return true;
        }

        if (!WorkOrderMaterialOutbox.TryCommitOrResume(
                order,
                itemStackRuntime,
                out failureReason))
        {
            order.status = WorkOrderStatus.WaitingForMaterials;
            return false;
        }

        order.status = WorkOrderStatus.Ready;
        stateStore.InvalidateCandidates();
        return true;
    }

    internal void RequestMissingMaterials(WorkOrderRecord order)
    {
        if (order == null
            || order.requiredItemMaterials.Count == 0)
        {
            return;
        }

        bool requestedAny = false;
        foreach (KeyValuePair<string, int> pair in order.requiredItemMaterials)
        {
            int delivered = order.deliveredItemMaterials.TryGetValue(
                pair.Key,
                out int currentDelivered)
                ? currentDelivered
                : 0;
            int pending = CountPendingDestinationItem(order, pair.Key);
            int remaining = Mathf.Max(
                0,
                pair.Value - delivered - pending);
            if (remaining <= 0)
            {
                continue;
            }

            int requested = 0;
            constructionInputs?.TryRequestItem(
                ToConstructionInputDescriptor(order),
                pair.Key,
                remaining,
                out requested,
                out _);
            requestedAny |= requested > 0;
        }

        if (requestedAny)
        {
            foreach (WorldItemStackSnapshot stack in itemStackRuntime.GetAllStacks())
            {
                if (stack != null
                    && string.Equals(
                        stack.DestinationId,
                        order.materialDestinationId,
                        StringComparison.Ordinal))
                {
                    itemStackRuntime.PrioritizeHaul(stack.StackId);
                }
            }

            workforceReplanService?.RequestOneHaulerToReplan(
                forceInterrupt: false);
        }
    }

    private int CountPendingDestinationItem(
        WorkOrderRecord order,
        string itemId)
    {
        int worldQuantity = itemStackRuntime.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.DestinationId,
                    order.materialDestinationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.ItemId,
                    itemId,
                    StringComparison.Ordinal)
                && (stack.State == WorldItemStackState.Loose
                    || stack.State == WorldItemStackState.FacilityBuffer
                    || (stack.State == WorldItemStackState.Stored
                        && !string.IsNullOrWhiteSpace(
                            stack.SourceStorageDestinationId))))
            .Sum(stack => stack.Quantity);
        int carriedQuantity = itemStackRuntime
            .GetCommittedHaulDeliveryQuantity(
                order.materialDestinationId,
                itemId);
        return worldQuantity + carriedQuantity;
    }

    private int CountPendingDestinationItems(WorkOrderRecord order)
    {
        if (order == null)
        {
            return 0;
        }
        long total = order.requiredItemMaterials.Keys.Sum(itemId =>
            (long)CountPendingDestinationItem(order, itemId));
        return total >= int.MaxValue ? int.MaxValue : (int)total;
    }

    private bool HasAvailableInstallationKit(string itemId)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        return itemStackRuntime.GetAllStacks().Any(stack =>
            stack != null
            && stack.Quantity > 0
            && !stack.Forbidden
            && stack.AvailableQuantity > 0
            && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
            && ((stack.State == WorldItemStackState.Loose
                    && string.IsNullOrWhiteSpace(stack.DestinationId))
                || stack.State == WorldItemStackState.Stored));
    }

    internal bool HasRequiredMaterialReserve(
        QualityTargetPipelineSaveData pipeline,
        BuildingSO building)
    {
        Dictionary<string, int> reserve = (pipeline?.minimumMaterialReserve
                ?? new List<ItemAmountDefinition>())
            .Where(value => value != null && value.Amount > 0)
            .ToDictionary(
                value => value.ItemId,
                value => value.Amount,
                StringComparer.Ordinal);
        if (reserve.Count == 0)
        {
            return true;
        }
        Dictionary<string, int> required = building.GetConstructionMaterials()
            .Where(value => value != null && value.Amount > 0)
            .GroupBy(value => value.ItemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(value => value.Amount),
                StringComparer.Ordinal);
        Dictionary<string, int> available = itemStackRuntime.GetAllStacks()
            .Where(stack => stack != null
                && !stack.Forbidden
                && stack.State is WorldItemStackState.Loose
                    or WorldItemStackState.Stored)
            .GroupBy(stack => stack.ItemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(stack => stack.Quantity),
                StringComparer.Ordinal);
        return reserve.All(pair =>
            available.TryGetValue(pair.Key, out int count)
            && count - (required.TryGetValue(pair.Key, out int cost) ? cost : 0)
                >= pair.Value);
    }

    private static WorkConstructionInputOwnerDescriptor
        ToConstructionInputDescriptor(WorkOrderRecord order)
    {
        return new WorkConstructionInputOwnerDescriptor(
            order.workOrderId,
            order.materialDestinationId,
            order.constructionSitePersistentId,
            order.position,
            order.requiredItemMaterials,
            order.materialBufferCapacityGrams,
            order.materialMassAuthorityRevision,
            order.materialCapacityFingerprint);
    }
}
