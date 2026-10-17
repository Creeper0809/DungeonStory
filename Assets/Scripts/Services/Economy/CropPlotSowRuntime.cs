using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;
using static CropPlotSaveContract;

internal sealed class CropPlotSowRuntime
{
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;
    private readonly ICropCycleInputRequirementQuery inputRequirements;
    private readonly IProductionItemGateway items;
    private readonly IPhysicalSeedLotGateway seedLots;
    private readonly ICropEcologyService ecology;
    private readonly ICropPlotInputOwnerRuntime inputOwners;
    private readonly IWorkforceReplanService workforce;
    private readonly ISurvivalEnvironmentQuery environmentQuery;
    private readonly IMilestoneGameplayModifierQuery milestoneModifiers;
    private readonly CropPlotMutationRuntime mutation;
    private readonly CropPlotPhysicalInputRuntime physicalInputs;
    private readonly CropPlotInputRequirementsQuery inputRequirementsQuery;

    internal CropPlotSowRuntime(CropPlotWorldDependencies world,
        CropPlotSimulationDependencies simulation,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        CropPlotMutationRuntime mutation,
        CropPlotPhysicalInputRuntime physicalInputs,
        CropPlotInputRequirementsQuery inputRequirementsQuery,
        IMilestoneGameplayModifierQuery milestoneModifiers)
    {
        this.aggregateRootStore = aggregateRootStore;
        inputRequirements = world.InputRequirements;
        items = world.Items;
        seedLots = world.SeedLots;
        ecology = world.Ecology;
        inputOwners = world.InputOwners;
        workforce = world.Workforce;
        environmentQuery = simulation.EnvironmentQuery;
        this.milestoneModifiers = milestoneModifiers;
        this.mutation = mutation;
        this.physicalInputs = physicalInputs;
        this.inputRequirementsQuery = inputRequirementsQuery;
    }

    private bool snapshotsDirty
    {
        set => aggregateRootStore.GetOrCreate(() => new CropPlotAggregateState()).SnapshotsDirty = value;
    }

    internal void EnsureSowingMaterials(
        CropPlotState state,
        CropDefinitionSO crop,
        bool requestMaterials)
    {
        if (!CropPlotInputRequirementsQuery.HasFrozenSowInputs(state))
            FreezeSowInputs(state, crop);
        Dictionary<string, int> requirements = inputRequirementsQuery.BuildMaterialRequirements(state, crop);
        if (requirements.Count == 0)
        {
            state.MaterialsConsumed = true;
            state.Phase = CropPlotPhase.ReadyToSow;
            mutation.MarkChanged();
            return;
        }
        CropPlotInputOwnerDescriptor inputOwner =
            physicalInputs.CreateSowInputOwnerDescriptor(state, requirements);
        if (!inputOwners.TryEnsure(inputOwner, out string ownerFailure))
        {
            mutation.SetBlocked(state, "crop-sow-input-owner-unavailable:" + ownerFailure);
            return;
        }

        if (!state.MaterialsConsumed
            && (state.PendingSow.phase == CropPhysicalCommitPhase.InputCommitted
                || physicalInputs.HasDelivered(requirements, state.MaterialDestinationId)))
        {
            string operationId = CropPhysicalTransactionOutbox.FormatSowOperationId(
                state.PlotId.Value,
                state.NextSowOperationSequence);
            bool starting = state.PendingSow.phase == CropPhysicalCommitPhase.None;
            if (CropPhysicalTransactionOutbox.TryCommitOrResume(
                    state.PendingSow,
                    operationId,
                    CropPhysicalTransactionOutbox.SowReasonCode,
                    state.NextSowOperationSequence,
                    state.MaterialDestinationId,
                    requirements,
                    crop.SeedItemId,
                    crop.CropId,
                    seedLots,
                    out SeedLotState seedLot,
                    out string failureReason))
            {
                string ecologyBefore = CropPhysicalTransactionOutbox.CreateEcologyFingerprint(ecology.Plots, state.PlotId.Value);
                if (starting)
                {
                    state.PendingSow.ecologyBeforeFingerprint = ecologyBefore;
                }
                else if (!string.Equals(
                             state.PendingSow.ecologyBeforeFingerprint,
                             ecologyBefore,
                             StringComparison.Ordinal))
                {
                    mutation.SetBlocked(state, "crop-sow-ecology-before-conflict");
                    return;
                }
                ecology.Sow(state.PlotId.Value, crop.FamilyGroup, seedLot);
                if (state.Ability.CompostPerCycle > 0)
                    ecology.ApplyCompost(state.PlotId.Value);
                state.PendingSow.ecologyAfterFingerprint =
                    CropPhysicalTransactionOutbox.CreateEcologyFingerprint(ecology.Plots, state.PlotId.Value);
                state.PendingSow.phase = CropPhysicalCommitPhase.OutcomePublished;
                state.MaterialsConsumed = true;
                state.Phase = CropPlotPhase.ReadyToSow;
                state.BlockedReason = string.Empty;
                mutation.MarkChanged();
                FinalizePublishedSow(state);
                return;
            }

            mutation.SetBlocked(state, failureReason);
            return;
        }

        if (!requestMaterials)
        {
            state.Phase = CropPlotPhase.WaitingForMaterials;
            snapshotsDirty = true;
            return;
        }

        bool requestedAny = false;
        int requiredSeed = requirements.TryGetValue(
            crop.SeedItemId,
            out int authoredSeedRequirement)
                ? authoredSeedRequirement
                : 0;
        int deliveredSeed = requiredSeed > 0
            ? items.CountDelivered(crop.SeedItemId, state.MaterialDestinationId)
            : 0;
        int pendingSeed = requiredSeed > 0
            ? items.CountPending(crop.SeedItemId, state.MaterialDestinationId)
            : 0;
        if (requiredSeed > deliveredSeed && pendingSeed > deliveredSeed)
        {
            if (!seedLots.TryReleaseUnreachableSeedDelivery(
                    crop.SeedItemId,
                    crop.CropId,
                    state.Building.centerPos,
                    state.MaterialDestinationId,
                    out bool releasedUnreachableDelivery,
                    out DomainFailure recoveryFailure))
            {
                mutation.SetBlocked(
                    state,
                    "crop-seed-delivery-recovery-failed:"
                        + recoveryFailure.Code);
                return;
            }
            if (releasedUnreachableDelivery)
            {
                // Destination release is intentionally whole-owner atomic. Any
                // companion water/compost input is now counted again by the same
                // loop instead of being silently stranded under an old route.
                requestedAny = true;
            }
        }
        foreach (KeyValuePair<string, int> requirement in requirements)
        {
            int pending = items.CountPending(
                requirement.Key,
                state.MaterialDestinationId);
            int missing = Mathf.Max(0, requirement.Value - pending);
            if (missing <= 0)
            {
                continue;
            }

            int requested;
            if (string.Equals(requirement.Key, crop.SeedItemId, StringComparison.Ordinal))
            {
                seedLots.RequestBestSeedLot(
                    crop.SeedItemId,
                    crop.CropId,
                    state.Building.centerPos,
                    state.MaterialDestinationId,
                    out requested,
                    out _);
            }
            else
            {
                items.RequestDelivery(
                    requirement.Key,
                    missing,
                    state.Building.centerPos,
                    state.MaterialDestinationId,
                    out requested,
                    out _);
            }
            requestedAny |= requested > 0;
        }

        state.Phase = CropPlotPhase.WaitingForMaterials;
        state.BlockedReason = physicalInputs.BuildMaterialWaitReason(
            requirements,
            state.MaterialDestinationId);
        snapshotsDirty = true;
        if (requestedAny)
        {
            items.PrioritizeDestination(state.MaterialDestinationId);
            workforce.RequestOneHaulerToReplan(forceInterrupt: false);
            mutation.MarkChanged(replan: false);
        }
    }

    internal void FinalizePublishedSow(CropPlotState state)
    {
        if (state?.PendingSow == null
            || state.PendingSow.phase != CropPhysicalCommitPhase.OutcomePublished)
            return;
        string current = CropPhysicalTransactionOutbox.CreateEcologyFingerprint(ecology.Plots, state.PlotId.Value);
        if (!string.Equals(
                current,
                state.PendingSow.ecologyAfterFingerprint,
                StringComparison.Ordinal))
        {
            mutation.SetBlocked(state, "crop-sow-ecology-after-conflict");
            return;
        }
        if (!physicalInputs.TryRetireDestination(
                state,
                state.MaterialDestinationId,
                CropPlotInputOwnerAuthority.SowCompletedReleaseReasonCode,
                out string retireFailure))
        {
            state.BlockedReason = retireFailure;
            snapshotsDirty = true;
            return;
        }
        if (!CropPhysicalTransactionOutbox.TryAcknowledgeOutcome(
                state.PendingSow,
                seedLots,
                out string failureReason))
        {
            state.BlockedReason = failureReason;
            snapshotsDirty = true;
            return;
        }
        if (state.Ability == null)
            throw new InvalidOperationException(
                "Crop sow execution receipt requires its authored plot ability.");
        bool explicitCorrelation = !string.IsNullOrEmpty(
            state.PendingCycleCorrelationId);
        string correlationId = !explicitCorrelation
            ? "crop-cycle:" + state.PendingSow.operationId
            : state.PendingCycleCorrelationId;
        if (state.CycleExecutionReceipt != null
            && !state.CycleExecutionReceipt.IsEmpty)
        {
            CropPlanExecutionReceiptAuthority.Validate(
                state.CycleExecutionReceipt,
                requireCompleted: false);
            if (state.CycleExecutionReceipt.status
                != CropCycleExecutionReceiptStatus.Active)
            {
                if (state.CycleExecutionReceipt.explicitCorrelation)
                    throw new InvalidOperationException(
                        "An unacknowledged explicit crop receipt blocks the next cycle.");
                state.CycleExecutionReceipt =
                    new CropCycleExecutionReceiptSaveData();
            }
        }
        if (state.CycleExecutionReceipt == null
            || state.CycleExecutionReceipt.IsEmpty)
        {
            int committedWater = (state.PendingSow.inputs
                    ?? new List<CropPhysicalInputSaveData>())
                .Where(input => input != null && string.Equals(
                    input.itemId,
                    CropCycleInputRequirementAuthority.CleanWaterItemId,
                    StringComparison.Ordinal))
                .Sum(input => input.quantity);
            if (committedWater is < 0 or > (int)CropWaterRules.InitialWaterQuantity)
                throw new InvalidOperationException(
                    "Crop sow committed an invalid initial clean-water quantity.");
            state.CycleExecutionReceipt =
                CropPlanExecutionReceiptAuthority.Begin(
                    correlationId,
                    explicitCorrelation,
                    state.PlotId.Value,
                    state.Ability.Indoor,
                    state.PendingSow,
                    committedWater);
            state.CurrentWater = committedWater;
            state.WaterCapacity = CropWaterRules.WaterCapacity;
        }
        else
        {
            CropPlanExecutionReceiptAuthority.Validate(
                state.CycleExecutionReceipt,
                requireCompleted: false);
            if (!string.Equals(
                    state.CycleExecutionReceipt.sowOperationId,
                    state.PendingSow.operationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    state.CycleExecutionReceipt.correlationId,
                    correlationId,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "Crop sow execution receipt conflicts with the active cycle.");
            }
        }
        state.PendingCycleCorrelationId = string.Empty;
        CropPhysicalTransactionOutbox.Clear(state.PendingSow);
        ClearFrozenSowInputs(state);
        state.NextSowOperationSequence = checked(
            state.NextSowOperationSequence + 1);
        state.MaterialDestinationId = BuildSowDestinationId(
            state.PlotId,
            state.NextSowOperationSequence);
        state.BlockedReason = string.Empty;
        mutation.MarkChanged();
    }

    private void FreezeSowInputs(CropPlotState state, CropDefinitionSO crop)
    {
        CropCycleInputRequirementSnapshot snapshot = inputRequirements.Capture(
            crop,
            state.Ability,
            state.MaterialDestinationId,
            environmentQuery.GetEnvironmentSnapshot().Weather,
            milestoneModifiers.WaterAndFertilizerConsumptionMultiplier,
            (itemId, excludedDestinationId) => items.CountAvailableStock(
                itemId,
                excludedDestinationId));
        state.FrozenSowInputOperationSequence = state.NextSowOperationSequence;
        state.FrozenSowInputSourceDigest = snapshot.SourceDigest;
        state.FrozenSowInputWeather = snapshot.Weather;
        state.FrozenSowInputConsumptionMultiplier =
            snapshot.MilestoneConsumptionMultiplier;
        state.FrozenSowInputSelectedFuelItemId = snapshot.SelectedFuelItemId;
        state.FrozenSowInputs = new Dictionary<string, int>(
            snapshot.Requirements,
            StringComparer.Ordinal);
        state.FrozenSowInputVectorDigest = CaptureFrozenSowInputVectorDigest(
            state.PlotId,
            crop.CropId,
            state.FrozenSowInputOperationSequence,
            state.FrozenSowInputs);
    }

    internal static void ClearFrozenSowInputs(CropPlotState state)
    {
        state.FrozenSowInputOperationSequence = -1;
        state.FrozenSowInputSourceDigest = string.Empty;
        state.FrozenSowInputVectorDigest = string.Empty;
        state.FrozenSowInputWeather = default;
        state.FrozenSowInputConsumptionMultiplier = 0f;
        state.FrozenSowInputSelectedFuelItemId = string.Empty;
        state.FrozenSowInputs.Clear();
    }
}
