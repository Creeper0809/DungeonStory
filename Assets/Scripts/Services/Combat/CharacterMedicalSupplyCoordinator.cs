using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal interface ICharacterMedicalSupplyStockPort
{
    IReadOnlyList<WorldItemStackSnapshot> GetAllStacks();
    bool TryRequestItemDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason);
}

internal sealed class CharacterMedicalSupplyStockPort :
    ICharacterMedicalSupplyStockPort
{
    private readonly IWorldItemStackRuntime items;
    internal CharacterMedicalSupplyStockPort(IWorldItemStackRuntime items) =>
        this.items = items ?? throw new ArgumentNullException(nameof(items));
    public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
        items.GetAllStacks();
    public bool TryRequestItemDelivery(
        string itemId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out string failureReason) => items.TryRequestItemDelivery(
        itemId,
        amount,
        destinationPosition,
        destinationId,
        out requested,
        out failureReason);
}

internal sealed class CharacterMedicalSupplyCoordinator
{
    internal const string DispositionReasonCode =
        "character-medical-treatment-supply";
    internal const string ExtractedBloodItemId =
        CaptivityItemDefinitions.ExtractedBloodItemId;

    private readonly ICharacterMedicalSupplyStockPort stock;
    private readonly IResourceEconomyContentCatalog resourceCatalog;
    private readonly IPhysicalFacilityItemSinkGateway physicalSinks;
    private readonly IPackagedLotTareDispositionService packagedTare;

    public CharacterMedicalSupplyCoordinator(
        ICharacterMedicalSupplyStockPort stock,
        IResourceEconomyContentCatalog resourceCatalog,
        IPhysicalFacilityItemSinkGateway physicalSinks,
        IPackagedLotTareDispositionService packagedTare)
    {
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.resourceCatalog = resourceCatalog
            ?? throw new ArgumentNullException(nameof(resourceCatalog));
        this.physicalSinks = physicalSinks
            ?? throw new ArgumentNullException(nameof(physicalSinks));
        this.packagedTare = packagedTare
            ?? throw new ArgumentNullException(nameof(packagedTare));
    }

    public bool EnsureTreatmentSupplyReady(
        CharacterMedicalOrder order,
        BuildableObject facility)
    {
        if (order == null || facility == null)
        {
            return false;
        }

        EnsureDestination(order);
        if (!TryRecoverPendingSupply(order, out _))
        {
            order.SetStatus(CharacterMedicalStatusCode.SupplyUnavailable);
            return false;
        }
        if (order.treatmentSupply == CharacterMedicalSupplyKind.None
            && !TryRequestMedicine(order, facility)
            && !TryRequestExtractedBlood(order, facility.centerPos))
        {
            order.SetStatus(CharacterMedicalStatusCode.SupplyUnavailable);
            return false;
        }

        if (order.treatmentSupplyConsumed)
        {
            return true;
        }

        bool consumed = TryConsumeAssignedSupply(order, facility);
        if (!consumed)
        {
            order.SetStatus(
                order.treatmentSupply == CharacterMedicalSupplyKind.Medicine
                    ? CharacterMedicalStatusCode.AwaitingMedicineDelivery
                    : CharacterMedicalStatusCode.AwaitingExtractedBloodDelivery);
            return false;
        }

        ApplyState(
            order,
            CharacterMedicalSupplyPolicy.MarkConsumed(CreateCurrentState(order)));
        return true;
    }

    private static void EnsureDestination(CharacterMedicalOrder order)
    {
        if (!string.IsNullOrWhiteSpace(order.treatmentMaterialDestinationId))
        {
            return;
        }

        order.treatmentMaterialDestinationId =
            WorldItemStackRuntime.FacilityInputDestinationPrefix
            + $"medical:{order.orderId}";
    }

    private bool TryConsumeAssignedSupply(
        CharacterMedicalOrder order,
        BuildableObject facility)
    {
        string itemId = order.treatmentSupply == CharacterMedicalSupplyKind.Medicine
            ? order.treatmentItemId
            : ExtractedBloodItemId;
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return false;
        }

        RecordSupplyIntent(order, itemId, facility.centerPos);
        if (!physicalSinks.TryCommitSinkPending(
                order.treatmentMaterialDestinationId,
                itemId,
                1,
                order.treatmentSupplyOperationId,
                order.treatmentSupplyReasonCode,
                out _,
                out _))
        {
            if (!physicalSinks.TryGetPending(
                    order.treatmentSupplyOperationId,
                    out _))
            {
                ClearPhysicalCommit(order, advanceSequence: false);
            }
            return false;
        }
        return TryRecoverPendingSupply(order, out _)
            && order.treatmentSupplyConsumed;
    }

    private bool TryRequestMedicine(
        CharacterMedicalOrder order,
        BuildableObject facility)
    {
        if (order.treatmentSupplyDeliveryRequested)
        {
            return order.treatmentSupply == CharacterMedicalSupplyKind.Medicine;
        }

        CharacterMedicalMedicineCandidate[] candidates = resourceCatalog.Items
            .Where(item => item != null
                && item.Kind == ResourceItemKind.Medicine
                && item.SupportsInjuryTreatment)
            .Select(item => new CharacterMedicalMedicineCandidate(
                item.ItemId,
                item.UnitPrice,
                item.TreatmentPotency,
                item.InfectionReduction,
                item.PainReduction))
            .ToArray();
        IReadOnlyList<CharacterMedicalMedicineCandidate> ranked =
            CharacterMedicalSupplyPolicy.RankMedicines(
                candidates,
                order.requiredTreatmentWork);
        foreach (CharacterMedicalMedicineCandidate medicine in ranked)
        {
            if (HasAvailableExactSupply(
                    order.treatmentMaterialDestinationId,
                    medicine.ItemId))
            {
                ApplyState(
                    order,
                    CharacterMedicalSupplyPolicy.CreateMedicine(
                        medicine,
                        consumed: false));
                order.SetStatus(
                    CharacterMedicalStatusCode.MedicineReady,
                    medicine.ItemId);
                return true;
            }

            if (!stock.TryRequestItemDelivery(
                    medicine.ItemId,
                    1,
                    facility.centerPos,
                    order.treatmentMaterialDestinationId,
                    out int requested,
                    out _)
                || requested < 1)
            {
                continue;
            }

            ApplyState(
                order,
                CharacterMedicalSupplyPolicy.CreateMedicine(
                    medicine,
                    consumed: false));
            order.SetStatus(
                CharacterMedicalStatusCode.AwaitingMedicineDelivery,
                medicine.ItemId);
            return true;
        }

        return false;
    }

    internal bool TryRequestExtractedBlood(
        CharacterMedicalOrder order,
        Vector2Int destinationPosition)
    {
        if (order == null)
        {
            return false;
        }
        if (order.treatmentSupplyDeliveryRequested)
        {
            return order.treatmentSupply
                == CharacterMedicalSupplyKind.ExtractedBlood;
        }
        if (!stock.TryRequestItemDelivery(
                ExtractedBloodItemId,
                1,
                destinationPosition,
                order.treatmentMaterialDestinationId,
                out int requested,
                out _)
            || requested < 1)
        {
            return false;
        }

        ApplyState(order, CharacterMedicalSupplyPolicy.CreateExtractedBlood());
        return true;
    }

    internal bool TryRecoverPendingSupply(
        CharacterMedicalOrder order,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (order == null)
        {
            failureReason = "medical order is missing";
            return false;
        }
        CharacterMedicalSupplyCommitPhase phase =
            (CharacterMedicalSupplyCommitPhase)order.treatmentSupplyCommitPhase;
        if (phase == CharacterMedicalSupplyCommitPhase.None)
        {
            return true;
        }

        bool hasReceipt = physicalSinks.TryGetPending(
            order.treatmentSupplyOperationId,
            out PhysicalItemBatchDispositionReceipt receipt);
        if (hasReceipt && !ReceiptMatches(order, receipt))
        {
            failureReason = "medical supply physical receipt mismatch";
            return false;
        }

        if (phase == CharacterMedicalSupplyCommitPhase.IntentRecorded)
        {
            if (!hasReceipt)
            {
                ClearPhysicalCommit(order, advanceSequence: false);
                return true;
            }
            if (!packagedTare.EnsureTerminalSinkOutputs(
                    new Dictionary<string, int>(StringComparer.Ordinal)
                    {
                        [order.treatmentPhysicalItemId] =
                            order.treatmentPhysicalQuantity
                    },
                    new Vector2Int(
                        order.treatmentOutputX,
                        order.treatmentOutputY),
                    receipt.CommitId,
                    out _,
                    out failureReason))
            {
                return false;
            }

            order.treatmentSupplyCommitPhase =
                (int)CharacterMedicalSupplyCommitPhase.SupplyPublished;
            order.treatmentSourceStackIds = receipt.SourceStackIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            order.treatmentInputMassGrams = receipt.InputMassGrams;
            order.treatmentPhysicalCommitId = receipt.CommitId;
            ApplyState(
                order,
                CharacterMedicalSupplyPolicy.MarkConsumed(
                    CreateCurrentState(order)));
        }

        if (hasReceipt
            && !physicalSinks.Acknowledge(
                receipt.CommitId,
                out failureReason))
        {
            return false;
        }

        ClearPhysicalCommit(order, advanceSequence: true);
        return true;
    }

    private bool HasAvailableExactSupply(string destinationId, string itemId) =>
        stock.GetAllStacks().Any(stack => stack != null
            && stack.State == WorldItemStackState.FacilityBuffer
            && stack.ReservedQuantity == 0
            && string.IsNullOrEmpty(stack.ReservedByPersistentId)
            && stack.AvailableQuantity > 0
            && string.Equals(
                stack.DestinationId,
                destinationId,
                StringComparison.Ordinal)
            && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal));

    private static void RecordSupplyIntent(
        CharacterMedicalOrder order,
        string itemId,
        Vector2Int outputPosition)
    {
        if ((CharacterMedicalSupplyCommitPhase)order.treatmentSupplyCommitPhase
            != CharacterMedicalSupplyCommitPhase.None)
        {
            throw new InvalidOperationException(
                $"Medical order '{order.orderId}' already owns a pending supply operation.");
        }
        order.treatmentSupplyOperationId =
            $"character-medical-supply:{order.orderId}:"
            + $"{order.treatmentSupplyOperationSequence:D8}";
        order.treatmentSupplyReasonCode = DispositionReasonCode;
        order.treatmentPhysicalItemId = itemId ?? string.Empty;
        order.treatmentPhysicalQuantity = 1;
        order.treatmentOutputX = outputPosition.x;
        order.treatmentOutputY = outputPosition.y;
        order.treatmentSupplyCommitPhase =
            (int)CharacterMedicalSupplyCommitPhase.IntentRecorded;
    }

    private static bool ReceiptMatches(
        CharacterMedicalOrder order,
        PhysicalItemBatchDispositionReceipt receipt) =>
        receipt.IsCommitted
        && receipt.Kind == PhysicalItemDispositionKind.Sink
        && string.Equals(
            receipt.OperationId,
            order.treatmentSupplyOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            order.treatmentSupplyReasonCode,
            StringComparison.Ordinal)
        && receipt.Quantity == order.treatmentPhysicalQuantity;

    private static void ClearPhysicalCommit(
        CharacterMedicalOrder order,
        bool advanceSequence)
    {
        if (advanceSequence)
        {
            order.treatmentSupplyOperationSequence = checked(
                order.treatmentSupplyOperationSequence + 1);
        }
        order.treatmentSupplyCommitPhase =
            (int)CharacterMedicalSupplyCommitPhase.None;
        order.treatmentSupplyOperationId = string.Empty;
        order.treatmentSupplyReasonCode = string.Empty;
        order.treatmentPhysicalItemId = string.Empty;
        order.treatmentPhysicalQuantity = 0;
        order.treatmentOutputX = 0;
        order.treatmentOutputY = 0;
        order.treatmentSourceStackIds.Clear();
        order.treatmentInputMassGrams = 0L;
        order.treatmentPhysicalCommitId = string.Empty;
    }

    private static CharacterMedicalSupplyState CreateCurrentState(
        CharacterMedicalOrder order) =>
        new CharacterMedicalSupplyState(
            order.treatmentSupply,
            order.treatmentSupplyConsumed,
            order.treatmentSupplyDeliveryRequested,
            order.treatmentItemId,
            order.treatmentPotency,
            order.treatmentInfectionReduction,
            order.treatmentPainReduction);

    private static void ApplyState(
        CharacterMedicalOrder order,
        CharacterMedicalSupplyState state)
    {
        order.treatmentSupply = state.Kind;
        order.treatmentSupplyConsumed = state.Consumed;
        order.treatmentSupplyDeliveryRequested = state.DeliveryRequested;
        order.treatmentItemId = state.ItemId;
        order.treatmentPotency = state.Potency;
        order.treatmentInfectionReduction = state.InfectionReduction;
        order.treatmentPainReduction = state.PainReduction;
    }
}
