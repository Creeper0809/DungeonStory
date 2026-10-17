using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class CharacterConsumablesPhysicalInventory
{
    private readonly IWorldItemStackRuntime items;
    private readonly CharacterConsumablesWorldReferences references;
    private readonly IItemQuantityReservationService quantityReservations;
    private readonly IReservedItemTransferService reservedTransfers;
    private readonly IReservedPhysicalItemBatchDispositionService reservedBatchDispositions;
    private readonly IPhysicalItemBatchDispositionService batchDispositions;
    private readonly IPackagedLotTareDispositionService tareDispositions;

    internal CharacterConsumablesPhysicalInventory(IWorldItemStackRuntime items,
        CharacterConsumablesWorldReferences references, IItemQuantityReservationService quantityReservations,
        IReservedItemTransferService reservedTransfers,
        IReservedPhysicalItemBatchDispositionService reservedBatchDispositions,
        IPhysicalItemBatchDispositionService batchDispositions,
        IPackagedLotTareDispositionService tareDispositions)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.references = references ?? throw new ArgumentNullException(nameof(references));
        this.quantityReservations = quantityReservations;
        this.reservedTransfers = reservedTransfers;
        this.reservedBatchDispositions = reservedBatchDispositions;
        this.batchDispositions = batchDispositions;
        this.tareDispositions = tareDispositions;
    }

    internal bool TryConsume(ItemStackId stackId, int quantity) =>
        stackId.IsValid
        && quantity > 0
        && items.TryCommitPhysicalDisposition(
            stackId.Value,
            quantity,
            PhysicalItemDispositionKind.Sink,
            $"character-consumable:{stackId.Value}:{quantity}",
            "character-consumable-consumed",
            out _,
            out _);

    internal bool TryConsumeForCharacter(
        CharacterId characterId,
        ItemStackId stackId,
        int quantity)
    {
        if (!stackId.IsValid || quantity <= 0)
            return false;
        WorldItemStackSnapshot stack = items.GetAllStacks().FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(candidate.StackId, stackId.Value, StringComparison.Ordinal));
        if (stack == null)
            return false;
        if (stack.State != WorldItemStackState.Carried)
            return TryConsume(stackId, quantity);

        CharacterActor actor = references.FindActor(characterId);
        CharacterCarryInventory carry = actor != null
            ? CharacterCarryInventory.Ensure(actor)
            : null;
        if (carry == null
            || carry.Items.Where(item => item != null
                    && string.Equals(
                        item.carriedStackId,
                        stackId.Value,
                        StringComparison.Ordinal)
                    && string.Equals(item.itemId, stack.ItemId, StringComparison.Ordinal))
                .Sum(item => item.quantity) < quantity)
        {
            return false;
        }

        CharacterCarryInventorySaveData carryBefore = carry.Capture();
        if (!carry.TryConsumeCarriedStack(
                stackId.Value,
                stack.ItemId,
                quantity))
            return false;
        string operationId =
            $"character-consumable:{characterId.Value}:{stackId.Value}:{quantity}";
        bool physicalCommitted = batchDispositions
                is ICarriedPhysicalItemBatchDispositionService carried
            && carried.TryCommitCarriedSinkPending(
                stackId.Value,
                quantity,
                operationId,
                "character-carried-consumable-consumed",
                out _,
                out _);
        if (!physicalCommitted)
        {
            carry.Restore(carryBefore);
            return false;
        }
        return true;
    }

    internal bool TryReserveMealQuantity(
        ConsumableOperationId operationId,
        CharacterId characterId,
        BuildingInstanceId facilityId,
        ItemStackId stackId,
        out string leaseId)
    {
        leaseId = string.Empty;
        if (quantityReservations == null)
            return true;
        WorldItemStackSnapshot stack = items.GetAllStacks().FirstOrDefault(value =>
            value != null && string.Equals(
                value.StackId,
                stackId.Value,
                StringComparison.Ordinal));
        if (stack == null)
            return false;
        if (!quantityReservations.TryReserve(
                operationId.Value,
                characterId.Value,
                ItemReservationPurpose.Meal,
                $"meal:{facilityId.Value}:{stack.ItemId}",
                new ItemQuantityReservationRequest(
                    stackId,
                    1,
                    stack.ReservationSignature),
                out ItemQuantityLease lease,
                out _))
        {
            return false;
        }
        leaseId = lease.leaseId;
        return true;
    }

    internal bool RevalidateMealQuantity(string leaseId, ItemStackId stackId)
    {
        if (quantityReservations == null)
            return string.IsNullOrWhiteSpace(leaseId);
        return quantityReservations.Revalidate(
                leaseId,
                out ItemQuantityLease lease,
                out _)
            && lease.slices.Any(slice => slice != null
                && slice.quantity >= 1);
    }

    internal bool TryResolveMealQuantityStack(
        string leaseId,
        out ItemStackId stackId)
    {
        stackId = default;
        if (quantityReservations == null
            || !quantityReservations.Revalidate(
                leaseId,
                out ItemQuantityLease lease,
                out _))
        {
            return false;
        }
        ItemLeaseSlice slice = lease.slices
            .Where(value => value != null && value.quantity > 0)
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (slice == null)
            return false;
        stackId = new ItemStackId(slice.stackId);
        return stackId.IsValid;
    }

    internal bool TryRebindMealQuantityLease(
        ConsumableOperationId operationId,
        out string leaseId,
        out ItemStackId stackId)
    {
        leaseId = string.Empty;
        stackId = default;
        if (quantityReservations == null
            || !operationId.IsValid
            || !quantityReservations.TryGetLeasesByOwner(
                operationId.Value,
                out IReadOnlyList<ItemQuantityLease> leases))
        {
            return false;
        }
        ItemQuantityLease lease = leases
            .Where(value => value != null
                && value.purpose == ItemReservationPurpose.Meal
                && value.remainingQuantity >= 1)
            .OrderBy(value => value.leaseId, StringComparer.Ordinal)
            .FirstOrDefault();
        ItemLeaseSlice slice = lease?.slices
            .Where(value => value != null && value.quantity > 0)
            .OrderBy(value => value.stackId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (lease == null || slice == null)
            return false;
        leaseId = lease.leaseId;
        stackId = new ItemStackId(slice.stackId);
        return stackId.IsValid;
    }

    internal bool TryConsumeReservedMealQuantity(
        string leaseId,
        ItemStackId stackId,
        int quantity)
    {
        if (quantityReservations == null || reservedTransfers == null)
            return string.IsNullOrWhiteSpace(leaseId) && TryConsume(stackId, quantity);
        return reservedTransfers.TryConsumeReservedQuantity(
            leaseId,
            quantity,
            out _);
    }

    internal bool TryCommitReservedMealQuantityPending(
        ConsumableOperationId operationId,
        string leaseId,
        int quantity,
        out CharacterMealPhysicalCommitSnapshot commit,
        out string failureReason)
    {
        commit = default;
        if (reservedBatchDispositions == null)
        {
            failureReason = "meal-reserved-pending-service-missing";
            return false;
        }
        if (!reservedBatchDispositions.TryCommitReservedSinkPending(
                leaseId,
                quantity,
                operationId.Value,
                CharacterConsumablesRuntime.MealPhysicalSinkReason,
                out PhysicalItemBatchDispositionReceipt receipt,
                out failureReason))
        {
            return false;
        }
        commit = ToMealCommit(receipt);
        return true;
    }

    internal bool TryGetPendingMealConsumption(
        ConsumableOperationId operationId,
        out CharacterMealPhysicalCommitSnapshot commit)
    {
        commit = default;
        if (batchDispositions == null
            || !batchDispositions.TryGetPending(
                operationId.Value,
                out PhysicalItemBatchDispositionReceipt receipt)
            || receipt.Kind != PhysicalItemDispositionKind.Sink
            || !string.Equals(
                receipt.ReasonCode,
                CharacterConsumablesRuntime.MealPhysicalSinkReason,
                StringComparison.Ordinal))
        {
            return false;
        }
        commit = ToMealCommit(receipt);
        return true;
    }

    internal bool TryAcknowledgeMealConsumption(
        string commitId,
        out string failureReason)
    {
        if (batchDispositions == null)
        {
            failureReason = "meal-pending-ack-service-missing";
            return false;
        }
        return batchDispositions.Acknowledge(commitId, out failureReason);
    }

    internal bool TryAcknowledgeMealConsumption(
        CharacterId characterId,
        ConsumableItemDefinitionId itemId,
        int quantity,
        string commitId,
        out string failureReason) =>
        TryPublishTareAndAcknowledge(
            characterId,
            itemId,
            quantity,
            commitId,
            "meal",
            out failureReason);

    internal bool TryAcknowledgeMealConsumption(
        CharacterId characterId,
        ConsumableItemDefinitionId itemId,
        int quantity,
        string commitId,
        Vector2Int consumptionPosition,
        out string failureReason) =>
        TryPublishTareAndAcknowledge(characterId, itemId, quantity, commitId,
            "meal", out failureReason, consumptionPosition);

    internal bool TryCommitSubstanceConsumptionPending(
        ConsumableOperationId operationId,
        CharacterId characterId,
        ItemStackId stackId,
        out CharacterSubstancePhysicalCommitSnapshot commit,
        out string failureReason)
    {
        commit = default;
        if (batchDispositions == null)
        {
            failureReason = "substance-pending-service-missing";
            return false;
        }
        if (TryGetPendingSubstanceConsumption(operationId, out commit))
        {
            failureReason = string.Empty;
            return true;
        }

        WorldItemStackSnapshot stack = items.GetAllStacks().FirstOrDefault(value =>
            value != null
            && string.Equals(value.StackId, stackId.Value, StringComparison.Ordinal));
        if (stack == null)
        {
            failureReason = "substance-source-stack-missing";
            return false;
        }

        CharacterCarryInventory carry = null;
        CharacterCarryInventorySaveData carryBefore = null;
        if (stack.State == WorldItemStackState.Carried)
        {
            CharacterActor actor = references.FindActor(characterId);
            carry = actor != null ? CharacterCarryInventory.Ensure(actor) : null;
            if (carry == null
                || carry.Items.Where(item => item != null
                        && string.Equals(
                            item.carriedStackId,
                            stackId.Value,
                            StringComparison.Ordinal)
                        && string.Equals(
                            item.itemId,
                            stack.ItemId,
                            StringComparison.Ordinal))
                    .Sum(item => item.quantity) < 1)
            {
                failureReason = "substance-carried-source-missing";
                return false;
            }
            carryBefore = carry.Capture();
            if (!carry.TryConsumeCarriedStack(stackId.Value, stack.ItemId, 1))
            {
                failureReason = "substance-carried-debit-failed";
                return false;
            }
        }

        try
        {
            failureReason = string.Empty;
            PhysicalItemBatchDispositionReceipt receipt = default;
            bool committed;
            if (stack.State == WorldItemStackState.Carried)
            {
                committed = batchDispositions is ICarriedPhysicalItemBatchDispositionService
                    carriedDispositions
                    && carriedDispositions.TryCommitCarriedSinkPending(
                        stackId.Value,
                        1,
                        operationId.Value,
                        CharacterConsumablesRuntime.SubstancePhysicalSinkReason,
                        out receipt,
                        out failureReason);
            }
            else
            {
                committed = batchDispositions.TryCommitPending(
                    new[] { new PhysicalItemTransformInput(stackId.Value, 1) },
                    PhysicalItemDispositionKind.Sink,
                    operationId.Value,
                    CharacterConsumablesRuntime.SubstancePhysicalSinkReason,
                    out receipt,
                    out failureReason);
            }
            if (!committed)
            {
                if (string.IsNullOrEmpty(failureReason)
                    && stack.State == WorldItemStackState.Carried)
                {
                    failureReason = "substance-carried-pending-service-missing";
                }
                carry?.Restore(carryBefore);
                return false;
            }
            commit = ToSubstanceCommit(receipt);
            return true;
        }
        catch
        {
            carry?.Restore(carryBefore);
            throw;
        }
    }

    internal bool TryGetPendingSubstanceConsumption(
        ConsumableOperationId operationId,
        out CharacterSubstancePhysicalCommitSnapshot commit)
    {
        commit = default;
        if (batchDispositions == null
            || !batchDispositions.TryGetPending(
                operationId.Value,
                out PhysicalItemBatchDispositionReceipt receipt)
            || receipt.Kind != PhysicalItemDispositionKind.Sink
            || !string.Equals(
                receipt.ReasonCode,
                CharacterConsumablesRuntime.SubstancePhysicalSinkReason,
                StringComparison.Ordinal))
        {
            return false;
        }
        commit = ToSubstanceCommit(receipt);
        return true;
    }

    internal bool TryAcknowledgeSubstanceConsumption(
        string commitId,
        out string failureReason)
    {
        if (batchDispositions == null)
        {
            failureReason = "substance-pending-ack-service-missing";
            return false;
        }
        return batchDispositions.Acknowledge(commitId, out failureReason);
    }

    internal bool TryAcknowledgeSubstanceConsumption(
        CharacterId characterId,
        ConsumableItemDefinitionId itemId,
        int quantity,
        string commitId,
        out string failureReason) =>
        TryPublishTareAndAcknowledge(
            characterId,
            itemId,
            quantity,
            commitId,
            "substance",
            out failureReason);

    internal bool TryCommitDetoxConsumptionPending(
        ConsumableOperationId operationId,
        ItemStackId stackId,
        out CharacterDetoxPhysicalCommitSnapshot commit,
        out string failureReason)
    {
        commit = default;
        if (batchDispositions == null)
        {
            failureReason = "detox-pending-service-missing";
            return false;
        }
        if (TryGetPendingDetoxConsumption(operationId, out commit))
        {
            failureReason = string.Empty;
            return true;
        }
        WorldItemStackSnapshot stack = items.GetAllStacks().FirstOrDefault(value =>
            value != null
            && string.Equals(value.StackId, stackId.Value, StringComparison.Ordinal));
        if (stack == null
            || stack.State != WorldItemStackState.FacilityBuffer
            || stack.AvailableQuantity < 1
            || stack.ReservedQuantity != 0
            || !string.IsNullOrEmpty(stack.ReservedByPersistentId)
            || stack.Forbidden
            || !CharacterConsumablesInputDestinationIdentity
                .IsDestinationForKind(
                    stack.DestinationId,
                    CharacterConsumablesInputKind.MedicalTreatment))
        {
            failureReason = "detox-facility-buffer-source-missing";
            return false;
        }
        if (!batchDispositions.TryCommitPending(
                new[] { new PhysicalItemTransformInput(stackId.Value, 1) },
                PhysicalItemDispositionKind.Sink,
                operationId.Value,
                CharacterConsumablesRuntime.DetoxPhysicalSinkReason,
                out PhysicalItemBatchDispositionReceipt receipt,
                out failureReason))
        {
            return false;
        }
        commit = ToDetoxCommit(receipt);
        return true;
    }

    internal bool TryGetPendingDetoxConsumption(
        ConsumableOperationId operationId,
        out CharacterDetoxPhysicalCommitSnapshot commit)
    {
        commit = default;
        if (batchDispositions == null
            || !batchDispositions.TryGetPending(
                operationId.Value,
                out PhysicalItemBatchDispositionReceipt receipt)
            || receipt.Kind != PhysicalItemDispositionKind.Sink
            || !string.Equals(
                receipt.ReasonCode,
                CharacterConsumablesRuntime.DetoxPhysicalSinkReason,
                StringComparison.Ordinal))
        {
            return false;
        }
        commit = ToDetoxCommit(receipt);
        return true;
    }

    internal bool TryAcknowledgeDetoxConsumption(
        CharacterId characterId,
        ConsumableItemDefinitionId itemId,
        int quantity,
        string commitId,
        out string failureReason) =>
        TryPublishTareAndAcknowledge(
            characterId,
            itemId,
            quantity,
            commitId,
            "detox",
            out failureReason);

    private bool TryPublishTareAndAcknowledge(
        CharacterId characterId,
        ConsumableItemDefinitionId itemId,
        int quantity,
        string commitId,
        string consumerKind,
        out string failureReason,
        Vector2Int? consumptionPosition = null)
    {
        if (batchDispositions == null)
        {
            failureReason = consumerKind + "-pending-ack-service-missing";
            return false;
        }
        bool isPackagedLot = items.MassQuery is IPackagedLotDefinitionQuery packagedLots
            && packagedLots.TryGetPackagedLot(
                (ItemDefinitionId)itemId.Value,
                out _);
        if (isPackagedLot && tareDispositions == null)
        {
            failureReason = consumerKind + "-packaged-tare-service-missing";
            return false;
        }
        if (tareDispositions != null)
        {
            CharacterActor actor = references.FindActor(characterId);
            if ((!consumptionPosition.HasValue && actor == null) || quantity <= 0 || !itemId.IsValid)
            {
                failureReason = consumerKind + "-tare-output-context-invalid";
                return false;
            }
            if (!tareDispositions.EnsureTerminalSinkOutputs(
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [itemId.Value] = quantity
                    },
                    consumptionPosition ?? actor.GetNowXY(),
                    commitId,
                    out _,
                    out failureReason))
            {
                return false;
            }
        }
        return batchDispositions.Acknowledge(commitId, out failureReason);
    }

    private static CharacterMealPhysicalCommitSnapshot ToMealCommit(
        PhysicalItemBatchDispositionReceipt receipt) => new(
        receipt.OperationId,
        receipt.ReasonCode,
        receipt.CommitId,
        receipt.SourceStackIds,
        receipt.Quantity,
        receipt.InputMassGrams);

    private static CharacterSubstancePhysicalCommitSnapshot ToSubstanceCommit(
        PhysicalItemBatchDispositionReceipt receipt) => new(
        receipt.OperationId,
        receipt.ReasonCode,
        receipt.CommitId,
        receipt.SourceStackIds,
        receipt.Quantity,
        receipt.InputMassGrams);

    private static CharacterDetoxPhysicalCommitSnapshot ToDetoxCommit(
        PhysicalItemBatchDispositionReceipt receipt) => new(
        receipt.OperationId,
        receipt.ReasonCode,
        receipt.CommitId,
        receipt.SourceStackIds,
        receipt.Quantity,
        receipt.InputMassGrams);

    internal void ReleaseMealQuantity(ConsumableOperationId operationId)
    {
        if (quantityReservations != null && operationId.IsValid)
        {
            // Restore regenerates lease IDs before the meal tick can rebind one.
            // Cancellation must also work in that interval, using the durable owner.
            quantityReservations.ReleaseByOwner(
                operationId.Value,
                ItemReservationReleaseReason.Cancelled);
        }
    }
}
