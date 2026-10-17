using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;
using static CropPlotSaveContract;

using static CropPlotOperationIdentity;

internal sealed class CropPlotWaterRuntime
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly ICropPlotInputOwnerRuntime inputOwners;
    private readonly IWorkforceReplanService workforce;
    private readonly IGameCalendar calendar;
    private readonly CropPlotMutationRuntime mutation;
    private readonly CropPlotPhysicalInputRuntime physicalInputs;
    private readonly CropPlotFactPublication factPublication;
    private readonly CropTreatmentDefinitionQuery treatmentDefinitions;
    private readonly IProductionItemGateway items;

    internal CropPlotWaterRuntime(CropPlotWorldDependencies world,
        CropPlotSimulationDependencies simulation,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        CropPlotMutationRuntime mutation,
        CropPlotPhysicalInputRuntime physicalInputs,
        CropPlotFactPublication factPublication,
        CropTreatmentDefinitionQuery treatmentDefinitions)
    {
        this.aggregateRootStore = aggregateRootStore;
        inputOwners = world.InputOwners;
        workforce = world.Workforce;
        calendar = simulation.Calendar;
        this.mutation = mutation;
        this.physicalInputs = physicalInputs;
        this.factPublication = factPublication;
        this.treatmentDefinitions = treatmentDefinitions;
        items = world.Items;
    }

    private int CurrentAbsoluteDay => calendar.Day;
    private bool snapshotsDirty
    {
        set => aggregateRootStore.GetOrCreate(() => new CropPlotAggregateState()).SnapshotsDirty = value;
    }

    internal static void RecordCycleWaterSupply(
        CropPlotState state,
        float waterBefore,
        float waterAfter,
        string childResultKey)
    {
        if (state?.CycleExecutionReceipt == null
            || state.CycleExecutionReceipt.IsEmpty)
            return;
        if (state.CycleExecutionReceipt.status
            != CropCycleExecutionReceiptStatus.Active)
            throw new InvalidOperationException(
                "Crop water supply has no active cycle receipt.");
        state.CycleExecutionReceipt = CropPlanExecutionReceiptAuthority
            .RecordWaterSupply(
                state.CycleExecutionReceipt,
                waterBefore,
                waterAfter,
                childResultKey);
    }

    internal void BeginWaterRefill(CropPlotState state)
    {
        if (state == null
            || state.WaterRefill.phase != CropWaterRefillPhase.None)
            return;
        int sequence = state.NextWaterRefillOperationSequence;
        string operationId = FormatWaterRefillOperationId(
            state.PlotId,
            sequence);
        string destinationId =
            CropPlotInputOwnerAuthority.BuildWaterRefillDestinationId(
                state.PlotId.Value,
                sequence);
        float requiredWork = CropWaterRules.RequireRefillWork(state.Building);
        CropWaterRefillSaveData refill = new()
        {
            phase = CropWaterRefillPhase.WaitingForDelivery,
            outcomeSchemaVersion =
                CropWaterRefillSaveData.CurrentOutcomeSchemaVersion,
            operationSequence = sequence,
            operationId = operationId,
            reasonCode = "crop-water-refill-input",
            destinationId = destinationId,
            itemId = CropCycleInputRequirementAuthority.CleanWaterItemId,
            quantity = CropWaterRules.RefillQuantity,
            requiredWork = requiredWork,
            requestFingerprint = CaptureWaterRefillRequestFingerprint(
                state.PlotId,
                sequence,
                destinationId,
                requiredWork)
        };
        CropPlotInputOwnerDescriptor descriptor =
            CropPlotPhysicalInputRuntime.CreateWaterRefillInputOwnerDescriptor(state, refill);
        if (!inputOwners.TryEnsure(descriptor, out string ownerFailure))
        {
            state.BlockedReason =
                "crop-water-refill-input-owner-unavailable:" + ownerFailure;
            snapshotsDirty = true;
            return;
        }
        state.WaterRefill = refill;
        mutation.MarkChanged();
    }

    internal void TickWaterRefillDelivery(
        CropPlotState state,
        bool requestMaterials)
    {
        CropWaterRefillSaveData refill = state?.WaterRefill;
        if (refill == null
            || refill.phase is CropWaterRefillPhase.None
                or CropWaterRefillPhase.InputCommitted
                or CropWaterRefillPhase.OutcomePublished
                or CropWaterRefillPhase.LedgerCommitted)
            return;

        CropPlotInputOwnerDescriptor descriptor =
            CropPlotPhysicalInputRuntime.CreateWaterRefillInputOwnerDescriptor(state, refill);
        if (!inputOwners.TryEnsure(descriptor, out string ownerFailure))
        {
            refill.failureReason =
                "crop-water-refill-input-owner-unavailable:" + ownerFailure;
            snapshotsDirty = true;
            return;
        }

        int delivered = items.CountDelivered(
            refill.itemId,
            refill.destinationId);
        if (delivered >= refill.quantity)
        {
            if (refill.phase == CropWaterRefillPhase.WaitingForDelivery)
            {
                refill.phase = CropWaterRefillPhase.ReadyForWork;
                refill.failureReason = string.Empty;
                mutation.MarkChanged();
            }
            return;
        }

        refill.phase = CropWaterRefillPhase.WaitingForDelivery;
        refill.failureReason = $"급수용 물 운반 대기 {delivered}/{refill.quantity}";
        snapshotsDirty = true;
        if (!requestMaterials)
            return;

        int pending = items.CountPending(refill.itemId, refill.destinationId);
        int missing = Mathf.Max(0, refill.quantity - pending);
        if (missing <= 0)
            return;
        if (items.RequestDelivery(
                refill.itemId,
                missing,
                state.Building.centerPos,
                refill.destinationId,
                out int requested,
                out string failureReason)
            && requested > 0)
        {
            items.PrioritizeDestination(refill.destinationId);
            workforce.RequestOneHaulerToReplan(forceInterrupt: false);
            refill.failureReason = string.Empty;
            mutation.MarkChanged(replan: false);
            return;
        }
        refill.failureReason = string.IsNullOrWhiteSpace(failureReason)
            ? "사용 가능한 깨끗한 물이 없습니다."
            : failureReason;
    }

    internal bool TryFinalizeWaterRefill(CropPlotState state)
    {
        CropWaterRefillSaveData refill = state?.WaterRefill;
        if (refill == null || refill.phase == CropWaterRefillPhase.None)
            return false;
        if (refill.outcomeSchemaVersion != CropWaterRefillSaveData.CurrentOutcomeSchemaVersion)
        {
            refill.failureReason = "crop-water-refill-outcome-schema-unsupported";
            snapshotsDirty = true;
            return false;
        }
        if (refill.phase is not (CropWaterRefillPhase.Working
                or CropWaterRefillPhase.InputCommitted
                or CropWaterRefillPhase.OutcomePublished
                or CropWaterRefillPhase.LedgerCommitted))
            return false;
        if (string.IsNullOrWhiteSpace(refill.responsibleWorkerId)
            || string.IsNullOrWhiteSpace(refill.responsibleWorkerDisplayName)
            || refill.workContributions == null || refill.workContributions.Count == 0)
        {
            refill.failureReason = "crop-water-refill-worker-required";
            snapshotsDirty = true;
            return false;
        }

        DomainFactLease reservation = default;
        try
        {
            if (refill.phase is CropWaterRefillPhase.OutcomePublished
                or CropWaterRefillPhase.LedgerCommitted)
            {
                if (Math.Abs(state.CurrentWater - refill.waterAfter) > 0.0001f)
                {
                    refill.failureReason = "crop-water-refill-water-after-conflict";
                    snapshotsDirty = true;
                    return false;
                }
                return TryFinishCommittedWaterRefill(state, refill,
                    factPublication.DeliverCropCommandFact(default, refill.publicationKey,
                        new CropWaterRefillFact(state.PlotId, refill)));
            }

            if (refill.completedWork + 0.001f < refill.requiredWork)
                return false;
            if (state.CurrentWater + refill.quantity > state.WaterCapacity + 0.001f)
            {
                refill.failureReason = "crop-water-refill-capacity-exceeded";
                snapshotsDirty = true;
                return false;
            }

            // Reserve observation capacity for a proposal, before consuming any
            // input. Only the subsequent applied fact can create a completion.
            CropWaterRefillSaveData proposal = refill.DeepClone();
            if (refill.phase == CropWaterRefillPhase.Working)
            {
                proposal.plotDisplayName = state.PlotDisplayName;
                proposal.itemDisplayName = treatmentDefinitions.ResolveTreatmentDisplayName(refill.itemId);
                proposal.locationX = state.LastKnownPosition.x;
                proposal.locationY = state.LastKnownPosition.y;
                proposal.waterBefore = state.CurrentWater;
                proposal.completedAbsoluteDay = CurrentAbsoluteDay;
            }
            if (!factPublication.RequireDomainFacts().TryReserve(
                    new CropWaterRefillFact(state.PlotId, proposal),
                    out reservation, out string reserveFailure))
            {
                refill.failureReason = reserveFailure;
                snapshotsDirty = true;
                return false;
            }
            if (!string.IsNullOrEmpty(refill.publicationKey)
                && refill.publicationKey != reservation.PublicationKey)
                throw new InvalidOperationException("Crop water refill publication identity drifted.");
            proposal.publicationKey = reservation.PublicationKey;
            state.WaterRefill = refill = proposal;

            if (refill.phase == CropWaterRefillPhase.Working)
            {
                Dictionary<string, int> costs = new(StringComparer.Ordinal)
                {
                    [refill.itemId] = refill.quantity
                };
                if (!items.ConsumeDeliveredToWip(
                        refill.destinationId, costs, refill.operationId,
                        out ProductionWipInputReceipt receipt, out string commitFailure))
                {
                    refill.failureReason = commitFailure;
                    factPublication.RequireDomainFacts().Cancel(reservation);
                    reservation = default;
                    snapshotsDirty = true;
                    return false;
                }
                string[] sourceStackIds = (receipt.SourceStackIds ?? Array.Empty<string>())
                    .OrderBy(value => value, StringComparer.Ordinal).ToArray();
                // Keep the committed physical receipt even if validation fails:
                // retry must never consume the material a second time.
                refill.commitId = receipt.CommitId;
                refill.inputQuantity = receipt.Quantity;
                refill.inputMassGrams = receipt.InputMassGrams;
                refill.physicalRequestFingerprint = receipt.PhysicalRequestFingerprint;
                refill.physicalOutcomeOwnerRevision = receipt.PhysicalOutcomeOwnerRevision;
                refill.sourceStackIds = sourceStackIds.ToList();
                refill.phase = CropWaterRefillPhase.InputCommitted;
                mutation.MarkChanged(replan: false);
                long expectedMass = items.GetDefinitionQuantityMassGrams(refill.itemId, refill.quantity);
                if (!receipt.IsCommitted || receipt.PhysicalOutcomeOwnerRevision <= 0L
                    || !IsCanonicalToken(receipt.PhysicalRequestFingerprint)
                    || sourceStackIds.Length == 0 || sourceStackIds.Any(value => !IsCanonicalToken(value))
                    || sourceStackIds.Distinct(StringComparer.Ordinal).Count() != sourceStackIds.Length
                    || receipt.Quantity != refill.quantity || receipt.InputMassGrams != expectedMass)
                    throw new InvalidOperationException(
                        "Crop water refill WIP receipt contradicts its exact physical input.");
            }

            if (Math.Abs(state.CurrentWater - refill.waterBefore) > 0.0001f)
            {
                refill.failureReason = "crop-water-refill-water-before-conflict";
                factPublication.RequireDomainFacts().Cancel(reservation);
                reservation = default;
                snapshotsDirty = true;
                return false;
            }
            if (refill.inputQuantity != refill.quantity
                || refill.inputMassGrams != items.GetDefinitionQuantityMassGrams(refill.itemId, refill.quantity))
                throw new InvalidOperationException("Crop water refill committed input mass contradicts its intent.");
            state.CurrentWater = Mathf.Min(state.WaterCapacity, state.CurrentWater + refill.quantity);
            refill.waterAfter = state.CurrentWater;
            refill.phase = CropWaterRefillPhase.OutcomePublished;
            refill.failureReason = string.Empty;
            mutation.MarkChanged();

            DomainFactDelivery delivered = factPublication.DeliverCropCommandFact(
                reservation, refill.publicationKey, new CropWaterRefillFact(state.PlotId, refill));
            reservation = default;
            return TryFinishCommittedWaterRefill(state, refill, delivered);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            if (reservation.IsValid) factPublication.RequireDomainFacts().Cancel(reservation);
            refill.failureReason = "crop-water-refill-finalize-threw:"
                + exception.GetType().Name + ":" + exception.Message;
            snapshotsDirty = true;
            return false;
        }
    }

    private bool TryFinishCommittedWaterRefill(
        CropPlotState state, CropWaterRefillSaveData refill, DomainFactDelivery delivered)
    {
        if (!CropPlotFactPublication.ConfirmCropCommandPublication(delivered, refill.publicationKey,
                refill.gameplayOutcomeId, refill.gameplayOutcomeDigest, out string failure))
        {
            refill.failureReason = failure;
            snapshotsDirty = true;
            return false;
        }
        refill.phase = CropWaterRefillPhase.LedgerCommitted;
        refill.gameplayOutcomeId = delivered.PublicationId;
        refill.gameplayOutcomeDigest = delivered.Fingerprint;
        if (delivered.Phase != DomainFactDeliveryPhase.Delivered)
        {
            refill.failureReason = delivered.Detail;
            snapshotsDirty = true;
            return false;
        }
        if (!physicalInputs.TryRetireDestination(state, refill.destinationId,
                CropPlotInputOwnerAuthority.WaterRefillCompletedReleaseReasonCode,
                out string retireFailure))
        {
            refill.failureReason = retireFailure;
            return false;
        }
        if (!items.AcknowledgeWipInput(refill.commitId, out string acknowledgeFailure))
        {
            refill.failureReason = acknowledgeFailure;
            return false;
        }
        RecordCycleWaterSupply(state, refill.waterBefore, refill.waterAfter, refill.publicationKey);
        state.NextWaterRefillOperationSequence = checked(refill.operationSequence + 1);
        state.WaterRefill = new CropWaterRefillSaveData();
        mutation.MarkChanged();
        return true;
    }

    internal bool TryRetireInactiveWaterRefill(CropPlotState state)
    {
        CropWaterRefillSaveData refill = state?.WaterRefill;
        if (refill == null || refill.phase == CropWaterRefillPhase.None)
            return true;
        if (refill.phase is CropWaterRefillPhase.InputCommitted
                or CropWaterRefillPhase.OutcomePublished
                or CropWaterRefillPhase.LedgerCommitted)
            return TryFinalizeWaterRefill(state);
        if (!physicalInputs.TryRetireDestination(
                state,
                refill.destinationId,
                CropPlotInputOwnerAuthority
                    .WaterRefillNoLongerRequiredReleaseReasonCode,
                out string retireFailure))
        {
            refill.failureReason = retireFailure;
            snapshotsDirty = true;
            return false;
        }
        state.NextWaterRefillOperationSequence = checked(
            state.NextWaterRefillOperationSequence + 1);
        state.WaterRefill = new CropWaterRefillSaveData();
        mutation.MarkChanged();
        return true;
    }

    internal static string ResolveWaterRefillUnavailableReason(
        CropPlotState state)
    {
        CropWaterRefillSaveData refill = state?.WaterRefill;
        if (refill == null || refill.phase == CropWaterRefillPhase.None)
            return "예약된 급수 작업이 없습니다.";
        if (!string.IsNullOrWhiteSpace(refill.failureReason))
            return refill.failureReason;
        if (state.Phase != CropPlotPhase.Growing)
            return "성장이 끝난 경작지는 보충 급수가 필요하지 않습니다.";
        return refill.phase switch
        {
            CropWaterRefillPhase.WaitingForDelivery => "급수용 물 운반 대기",
            CropWaterRefillPhase.InputCommitted => "물리 물 투입 확정 대기",
            CropWaterRefillPhase.OutcomePublished => "물 투입 승인 대기",
            CropWaterRefillPhase.LedgerCommitted => "물 투입 원장 승인 대기",
            _ => string.Empty
        };
    }

    internal static bool ShouldBeginManualWaterRefill(
        CropIrrigationStatus status) =>
        status is CropIrrigationStatus.NoOperationalIrrigator
            or CropIrrigationStatus.OutOfRange
            or CropIrrigationStatus.RouteBlocked
            or CropIrrigationStatus.NetworkUnavailable
            or CropIrrigationStatus.WaterUnavailable;

    internal static void RecordWaterRefillContribution(
        CropWaterRefillSaveData refill,
        CharacterActor worker,
        float acceptedWork)
    {
        if (acceptedWork <= 0f || refill == null)
            return;
        string characterId = worker?.Identity?.PersistentId?.Trim()
            ?? string.Empty;
        string displayName = worker?.Identity?.DisplayName?.Trim()
            ?? string.Empty;
        if (characterId.Length == 0 || displayName.Length == 0)
            return;
        refill.workContributions ??= new List<CraftContributionSaveData>();
        CraftContributionSaveData contribution = refill.workContributions
            .FirstOrDefault(value => value != null
                && string.Equals(
                    value.characterId,
                    characterId,
                    StringComparison.Ordinal));
        if (contribution == null)
        {
            contribution = new CraftContributionSaveData
            {
                characterId = characterId
            };
            refill.workContributions.Add(contribution);
        }
        contribution.contributedWork += acceptedWork;
        refill.workContributions.Sort((left, right) =>
            string.CompareOrdinal(left?.characterId, right?.characterId));
        refill.responsibleWorkerId = characterId;
        refill.responsibleWorkerDisplayName = displayName;
    }
}
