using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;
using VContainer.Unity;
using static CropPlotSaveContract;

using static CropPlotOperationIdentity;

internal sealed class CropPlotTreatmentRuntime
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
    private readonly IPhysicalFacilityItemSinkGateway treatmentItems;
    private readonly IPackagedLotTareDispositionService packagedTare;
    private readonly ICropEcologyService ecology;

    internal CropPlotTreatmentRuntime(CropPlotWorldDependencies world,
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
        treatmentItems = world.TreatmentItems;
        packagedTare = world.PackagedTare;
        ecology = world.Ecology;
    }

    private int CurrentAbsoluteDay => calendar.Day;
    private bool snapshotsDirty
    {
        set => aggregateRootStore.GetOrCreate(() => new CropPlotAggregateState()).SnapshotsDirty = value;
    }

    internal void TickTreatmentDelivery(
        CropPlotState state,
        bool requestMaterials)
    {
        CropTreatmentOrderSaveData treatment = state?.Treatment;
        if (treatment == null
            || treatment.phase is CropTreatmentOrderPhase.None
                or CropTreatmentOrderPhase.InputCommitted
                or CropTreatmentOrderPhase.OutcomePublished
                or CropTreatmentOrderPhase.LedgerCommitted
                or CropTreatmentOrderPhase.PlotDestroyedLossPending)
            return;

        CropPlotInputOwnerDescriptor inputOwner =
            CropPlotPhysicalInputRuntime.CreateTreatmentInputOwnerDescriptor(state, treatment);
        if (!inputOwners.TryEnsure(inputOwner, out string ownerFailure))
        {
            treatment.failureReason =
                "crop-treatment-input-owner-unavailable:" + ownerFailure;
            snapshotsDirty = true;
            return;
        }

        int delivered = items.CountDelivered(
            treatment.itemId,
            treatment.destinationId);
        if (delivered >= treatment.quantity)
        {
            if (treatment.phase == CropTreatmentOrderPhase.WaitingForDelivery)
            {
                treatment.phase = CropTreatmentOrderPhase.ReadyForWork;
                treatment.failureReason = string.Empty;
                mutation.MarkChanged();
            }
            return;
        }

        treatment.phase = CropTreatmentOrderPhase.WaitingForDelivery;
        treatment.failureReason = $"처리제 운반 대기 {delivered}/{treatment.quantity}";
        snapshotsDirty = true;
        if (!requestMaterials)
            return;

        int pending = items.CountPending(
            treatment.itemId,
            treatment.destinationId);
        int missing = Mathf.Max(0, treatment.quantity - pending);
        if (missing <= 0)
            return;
        if (items.RequestDelivery(
                treatment.itemId,
                missing,
                state.Building.centerPos,
                treatment.destinationId,
                out int requested,
                out string failureReason)
            && requested > 0)
        {
            items.PrioritizeDestination(treatment.destinationId);
            workforce.RequestOneHaulerToReplan(forceInterrupt: false);
            treatment.failureReason = string.Empty;
            mutation.MarkChanged(replan: false);
            return;
        }
        treatment.failureReason = string.IsNullOrWhiteSpace(failureReason)
            ? "사용 가능한 처리제가 없습니다."
            : failureReason;
    }

    internal bool TryFinalizeTreatment(CropPlotState state)
    {
        CropTreatmentOrderSaveData treatment = state?.Treatment;
        if (treatment == null || treatment.phase == CropTreatmentOrderPhase.None)
            return false;
        if (treatment.outcomeSchemaVersion != CropTreatmentOrderSaveData.CurrentOutcomeSchemaVersion)
        {
            treatment.failureReason = "crop-treatment-outcome-schema-unsupported";
            snapshotsDirty = true;
            return false;
        }
        if (treatment.phase is not (CropTreatmentOrderPhase.Working
            or CropTreatmentOrderPhase.InputCommitted or CropTreatmentOrderPhase.OutcomePublished
            or CropTreatmentOrderPhase.LedgerCommitted))
            return false;
        if (string.IsNullOrWhiteSpace(treatment.responsibleWorkerId)
            || string.IsNullOrWhiteSpace(treatment.responsibleWorkerDisplayName)
            || treatment.workContributions == null || treatment.workContributions.Count == 0)
        {
            treatment.failureReason = "crop-treatment-worker-required";
            snapshotsDirty = true;
            return false;
        }

        DomainFactLease reservation = default;
        try
        {
            if (treatment.phase is CropTreatmentOrderPhase.OutcomePublished or CropTreatmentOrderPhase.LedgerCommitted)
            {
                if (treatment.phase == CropTreatmentOrderPhase.OutcomePublished
                    && treatment.ecologyAfterFingerprint != CropPhysicalTransactionOutbox.CreateEcologyFingerprint(ecology.Plots, state.PlotId.Value))
                {
                    treatment.failureReason = "crop-treatment-ecology-after-conflict";
                    return false;
                }
                return TryFinishCommittedTreatment(state, treatment,
                    factPublication.DeliverCropCommandFact(default, treatment.publicationKey,
                        new CropTreatmentFact(state.PlotId, treatment)));
            }
            if (treatment.completedWork + 0.001f < treatment.requiredWork)
                return false;

            CropTreatmentOrderSaveData proposal = treatment.DeepClone();
            if (treatment.phase == CropTreatmentOrderPhase.Working)
            {
                CropEcologyPlotSaveData before = RequireTreatmentEcology(state);
                string beforeFingerprint = CropPhysicalTransactionOutbox.CreateEcologyFingerprint(ecology.Plots, state.PlotId.Value);
                if (string.IsNullOrEmpty(proposal.ecologyBeforeFingerprint))
                {
                    proposal.ecologyBeforeFingerprint = beforeFingerprint;
                    proposal.pestPressureBefore = before.pestPressure;
                    proposal.diseasePressureBefore = before.diseasePressure;
                    proposal.cooldownBeforeDay = GetNextTreatmentAllowedDay(state, proposal.treatmentKind);
                    proposal.completedAbsoluteDay = CurrentAbsoluteDay;
                    proposal.plotDisplayName = state.PlotDisplayName;
                    proposal.itemDisplayName = treatmentDefinitions.ResolveTreatmentDisplayName(proposal.itemId);
                    proposal.locationX = state.LastKnownPosition.x;
                    proposal.locationY = state.LastKnownPosition.y;
                }
                else if (proposal.ecologyBeforeFingerprint != beforeFingerprint)
                {
                    treatment.failureReason = "crop-treatment-ecology-before-conflict";
                    return false;
                }
            }

            // The observer reserves only a proposal's bounded capacity. No
            // applied receipt exists before physical input and ecology effects.
            if (!factPublication.RequireDomainFacts().TryReserve(new CropTreatmentFact(state.PlotId, proposal),
                    out reservation, out string reserveFailure))
            {
                treatment.failureReason = reserveFailure;
                return false;
            }
            if (!string.IsNullOrEmpty(treatment.publicationKey)
                && treatment.publicationKey != reservation.PublicationKey)
                throw new InvalidOperationException("Crop treatment publication identity drifted.");
            proposal.publicationKey = reservation.PublicationKey;
            state.Treatment = treatment = proposal;

            // Both a first consumption and a saved InputCommitted continuation
            // must match the actual physical authority's pending receipt.
            if (!CropTreatmentPhysicalOutbox.TryCommitOrResume(treatment, treatmentItems, out string commitFailure))
            {
                treatment.failureReason = commitFailure;
                factPublication.RequireDomainFacts().Cancel(reservation);
                reservation = default;
                return false;
            }
            if (treatment.ecologyBeforeFingerprint != CropPhysicalTransactionOutbox.CreateEcologyFingerprint(ecology.Plots, state.PlotId.Value))
            {
                treatment.failureReason = "crop-treatment-ecology-before-conflict";
                factPublication.RequireDomainFacts().Cancel(reservation);
                reservation = default;
                return false;
            }
            Vector2Int position = state.Building != null ? state.Building.centerPos : state.LastKnownPosition;
            if (!CropTreatmentPhysicalOutbox.EnsureTareOutputs(treatment, position, packagedTare, out string tareFailure))
            {
                treatment.failureReason = tareFailure;
                factPublication.RequireDomainFacts().Cancel(reservation);
                reservation = default;
                return false;
            }

            switch (treatment.treatmentKind)
            {
                case CropTreatmentKind.PestLure:
                case CropTreatmentKind.BotanicalPesticide:
                    ecology.ApplyPestControl(state.PlotId.Value, treatment.effectAmount);
                    break;
                case CropTreatmentKind.Fungicide:
                    ecology.ApplyFungicide(state.PlotId.Value, treatment.effectAmount);
                    break;
                default:
                    throw new InvalidOperationException("crop-treatment-kind-invalid");
            }
            CropEcologyPlotSaveData after = RequireTreatmentEcology(state);
            treatment.pestPressureAfter = after.pestPressure;
            treatment.diseasePressureAfter = after.diseasePressure;
            treatment.ecologyAfterFingerprint = CropPhysicalTransactionOutbox.CreateEcologyFingerprint(ecology.Plots, state.PlotId.Value);
            treatment.cooldownAfterDay = checked(treatment.completedAbsoluteDay + treatment.cooldownDays);
            SetNextTreatmentAllowedDay(state, treatment.treatmentKind, treatment.cooldownAfterDay);
            treatment.phase = CropTreatmentOrderPhase.OutcomePublished;
            treatment.failureReason = string.Empty;
            mutation.MarkChanged();

            DomainFactDelivery delivered = factPublication.DeliverCropCommandFact(reservation, treatment.publicationKey,
                new CropTreatmentFact(state.PlotId, treatment));
            reservation = default;
            return TryFinishCommittedTreatment(state, treatment, delivered);
        }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            if (reservation.IsValid) factPublication.RequireDomainFacts().Cancel(reservation);
            treatment.failureReason = "crop-treatment-finalize-threw:"
                + exception.GetType().Name + ":" + exception.Message;
            return false;
        }
        finally { snapshotsDirty = true; }
    }

    private CropEcologyPlotSaveData RequireTreatmentEcology(CropPlotState state)
    {
        CropEcologyPlotSaveData plot = ecology.Plots.SingleOrDefault(value =>
            value != null
            && state != null
            && string.Equals(
                value.plotId,
                state.PlotId.Value,
                StringComparison.Ordinal));
        return plot ?? throw new InvalidOperationException(
            "Crop treatment requires an owned ecology plot.");
    }

    private bool TryFinishCommittedTreatment(
        CropPlotState state, CropTreatmentOrderSaveData treatment, DomainFactDelivery delivered)
    {
        if (!CropPlotFactPublication.ConfirmCropCommandPublication(delivered, treatment.publicationKey,
                treatment.gameplayOutcomeId, treatment.gameplayOutcomeDigest, out string failure))
        {
            treatment.failureReason = failure;
            snapshotsDirty = true;
            return false;
        }
        treatment.phase = CropTreatmentOrderPhase.LedgerCommitted;
        treatment.gameplayOutcomeId = delivered.PublicationId;
        treatment.gameplayOutcomeDigest = delivered.Fingerprint;
        if (delivered.Phase != DomainFactDeliveryPhase.Delivered)
        {
            treatment.failureReason = delivered.Detail;
            snapshotsDirty = true;
            return false;
        }
        if (!physicalInputs.TryRetireDestination(state, treatment.destinationId,
                CropPlotInputOwnerAuthority.TreatmentCompletedReleaseReasonCode, out string retireFailure))
        {
            treatment.failureReason = retireFailure;
            snapshotsDirty = true;
            return false;
        }
        if (!CropTreatmentPhysicalOutbox.TryAcknowledgeOutcome(treatment, treatmentItems, out string acknowledgeFailure))
        {
            treatment.failureReason = acknowledgeFailure;
            snapshotsDirty = true;
            return false;
        }
        state.NextTreatmentOperationSequence = checked(treatment.operationSequence + 1);
        CropTreatmentPhysicalOutbox.Clear(treatment);
        mutation.MarkChanged();
        return true;
    }

    internal string ResolveTreatmentUnavailableReason(CropPlotState state)
    {
        CropTreatmentOrderSaveData treatment = state?.Treatment;
        if (treatment == null
            || treatment.phase == CropTreatmentOrderPhase.None)
            return "예약된 작물 처리가 없습니다.";
        if (!string.IsNullOrWhiteSpace(treatment.failureReason))
            return treatment.failureReason;
        CropEcologyPlotSaveData ecologyPlot = ecology.Plots.SingleOrDefault(value =>
            value != null
            && string.Equals(value.plotId, state.PlotId.Value, StringComparison.Ordinal));
        float pressure = treatment.treatmentKind == CropTreatmentKind.Fungicide
            ? ecologyPlot?.diseasePressure ?? 0f
            : ecologyPlot?.pestPressure ?? 0f;
        if (pressure <= 0f
            && treatment.phase is CropTreatmentOrderPhase.WaitingForDelivery
                or CropTreatmentOrderPhase.ReadyForWork
                or CropTreatmentOrderPhase.Working)
            return "처리 필요성이 사라졌습니다. 예약을 취소하십시오.";
        return treatment.phase switch
        {
            CropTreatmentOrderPhase.WaitingForDelivery => "처리제 운반 대기",
            CropTreatmentOrderPhase.InputCommitted => "처리 결과 확정 대기",
            CropTreatmentOrderPhase.OutcomePublished => "물리 소비 승인 대기",
            CropTreatmentOrderPhase.LedgerCommitted => "원장 승인 대기",
            CropTreatmentOrderPhase.PlotDestroyedLossPending =>
                "파괴된 경작지 처리 손실 승인 대기",
            _ => string.Empty
        };
    }

    internal bool IsTreatmentStillRequired(CropPlotState state)
    {
        CropTreatmentOrderSaveData treatment = state?.Treatment;
        CropEcologyPlotSaveData ecologyPlot = ecology.Plots.SingleOrDefault(value =>
            value != null
            && state != null
            && string.Equals(value.plotId, state.PlotId.Value, StringComparison.Ordinal));
        if (treatment == null || ecologyPlot == null || ecologyPlot.cropDead)
            return false;
        return treatment.treatmentKind == CropTreatmentKind.Fungicide
            ? ecologyPlot.diseasePressure > 0f
            : ecologyPlot.pestPressure > 0f;
    }

    internal static int GetNextTreatmentAllowedDay(
        CropPlotState state,
        CropTreatmentKind kind) => kind switch
        {
            CropTreatmentKind.PestLure => state.PestLureNextAllowedDay,
            CropTreatmentKind.BotanicalPesticide =>
                state.BotanicalPesticideNextAllowedDay,
            CropTreatmentKind.Fungicide => state.FungicideNextAllowedDay,
            _ => int.MaxValue
        };

    private static void SetNextTreatmentAllowedDay(
        CropPlotState state,
        CropTreatmentKind kind,
        int absoluteDay)
    {
        int day = Math.Max(0, absoluteDay);
        switch (kind)
        {
            case CropTreatmentKind.PestLure:
                state.PestLureNextAllowedDay = day;
                break;
            case CropTreatmentKind.BotanicalPesticide:
                state.BotanicalPesticideNextAllowedDay = day;
                break;
            case CropTreatmentKind.Fungicide:
                state.FungicideNextAllowedDay = day;
                break;
            default:
                throw new InvalidOperationException(
                    "Unknown crop treatment kind cannot own a cooldown.");
        }
    }

    internal static void RecordTreatmentContribution(
        CropTreatmentOrderSaveData treatment,
        CharacterActor worker,
        float acceptedWork)
    {
        if (acceptedWork <= 0f || treatment == null)
            return;
        string characterId = worker?.Identity?.PersistentId?.Trim()
            ?? string.Empty;
        string displayName = worker?.Identity?.DisplayName?.Trim()
            ?? string.Empty;
        if (characterId.Length == 0 || displayName.Length == 0)
            return;
        treatment.workContributions ??= new List<CraftContributionSaveData>();
        CraftContributionSaveData contribution =
            treatment.workContributions.FirstOrDefault(value => value != null
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
            treatment.workContributions.Add(contribution);
        }
        contribution.contributedWork += acceptedWork;
        treatment.workContributions.Sort((left, right) =>
            string.CompareOrdinal(left?.characterId, right?.characterId));
        treatment.responsibleWorkerId = characterId;
        treatment.responsibleWorkerDisplayName = displayName;
    }
}
