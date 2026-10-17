using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

/// <summary>
/// Owns treatment patient selection, supply, service and physical-effect-ACK
/// progression. Saved treatment state stays in the current survival aggregate;
/// the work map is transient and resets at the facade's restore boundary.
/// </summary>
internal sealed class SurvivalTreatmentRuntime
{
    private const float TreatmentMedicineHeal = 16f;
    private const int TreatmentWorkOperationCapacity = 512;
    private readonly Dictionary<string, ConsumableOperationId>
        treatmentOperationsByWork = new(StringComparer.Ordinal);
    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly SurvivalFoodStockRuntime stockRuntime;
    private readonly ISurvivalServiceSessionCapability serviceSessionRuntime;
    private readonly ICharacterConsumablesApplication consumables;
    private readonly IGameEventBus gameEventBus;
    private readonly DungeonRuntimeAggregateRootStore aggregateRootStore;

    internal SurvivalTreatmentRuntime(
        ICharacterAiWorldRegistry worldRegistry,
        SurvivalFoodStockRuntime stockRuntime,
        ISurvivalServiceSessionCapability serviceSessionRuntime,
        ICharacterConsumablesApplication consumables,
        IGameEventBus gameEventBus,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.worldRegistry = worldRegistry;
        this.stockRuntime = stockRuntime;
        this.serviceSessionRuntime = serviceSessionRuntime;
        this.consumables = consumables;
        this.gameEventBus = gameEventBus;
        this.aggregateRootStore = aggregateRootStore;
    }

    private SurvivalFoodAggregateState aggregateState =>
        aggregateRootStore.GetOrCreate(() => new SurvivalFoodAggregateState());
    private DungeonSurvivalSaveData state => aggregateState.Data;

    internal void ClearWorkOperationsAfterRestore() => treatmentOperationsByWork.Clear();
    internal float GetTreatmentToxicityUrgency(BuildableObject building) =>
        FindToxicityTreatmentPatient(building)?.Status.Toxicity ?? 0f;

    private IEnumerable<CharacterActor> GetSurvivalConsumers() =>
        SurvivalConsumerSelection.GetSurvivalConsumers(worldRegistry);

    private void EnsureStateLists()
    {
        aggregateState.Data ??= new DungeonSurvivalSaveData();
        SurvivalFoodStatePersistence.EnsureLists(state);
    }

    private sealed class ToxicityTreatmentPatient
    {
        internal ToxicityTreatmentPatient(
            CharacterActor actor,
            CharacterToxicityStatus status)
        {
            Actor = actor;
            Status = status;
        }

        internal CharacterActor Actor { get; }
        internal CharacterToxicityStatus Status { get; }
    }

    internal bool TryApplyTreat(
        IBuildingVisitorPort actor,
        BuildableObject building,
        ConsumableOperationId requestedOperationId,
        out ConsumableOperationId resolvedOperationId,
        out int amount,
        out DomainFailure failure)
    {
        EnsureStateLists();
        resolvedOperationId = default;
        amount = 0;
        BuildingMedicalAbility medical =
            building?.BuildingData?.GetAbility<BuildingMedicalAbility>();
        if (medical == null)
        {
            failure = new DomainFailure(
                FailureCode.SurvivalTreatmentUnsupported,
                building?.PersistentInstanceId.Value ?? string.Empty);
            return false;
        }

        BuildingInstanceId facilityId = building.RequirePersistentInstanceId();
        if (requestedOperationId.IsValid
            && state.completedTreatmentOperationIds.Contains(
                requestedOperationId.Value,
                StringComparer.Ordinal))
        {
            resolvedOperationId = requestedOperationId;
            amount = 1;
            failure = DomainFailure.None;
            return true;
        }

        SurvivalTreatmentPlanSaveData activeTreatmentPlan =
            FindActiveTreatmentPlan(requestedOperationId, facilityId);
        if (activeTreatmentPlan != null)
        {
            resolvedOperationId = new ConsumableOperationId(
                activeTreatmentPlan.operationId);
            return TryFinalizeTreatmentPlan(
                actor,
                building,
                medical,
                activeTreatmentPlan,
                out amount,
                out failure);
        }

        if (!requestedOperationId.IsValid
            && TryFindPendingDetoxTreatment(
                facilityId,
                out ConsumableOperationId pendingDetoxOperationId))
        {
            requestedOperationId = pendingDetoxOperationId;
        }

        CharacterId requestedPatientId = default;
        BuildingInstanceId requestedFacilityId = default;
        bool requestedCompleted = false;
        bool requestedReceipt = consumables != null
            && consumables.TryGetDetoxTreatmentOwner(
                requestedOperationId,
                out requestedPatientId,
                out requestedFacilityId,
                out requestedCompleted);
        if (requestedReceipt && !requestedFacilityId.Equals(facilityId))
        {
            failure = new DomainFailure(
                FailureCode.SurvivalTreatmentTargetMissing,
                requestedOperationId.Value);
            return false;
        }
        CharacterActor requestedPatient = requestedReceipt
            ? GetSurvivalConsumers().FirstOrDefault(candidate =>
                candidate != null
                && CharacterPersistentIdentity.Require(candidate)
                    .Equals(requestedPatientId))
            : null;
        SurvivalHealthSaveData patientEntry = requestedReceipt
            ? null
            : FindTreatmentEntry(building);
        ToxicityTreatmentPatient toxicityPatient = requestedReceipt
            ? requestedPatient == null
                ? null
                : new ToxicityTreatmentPatient(
                    requestedPatient,
                    consumables.GetToxicityStatus(requestedPatientId))
            : patientEntry == null
                ? FindToxicityTreatmentPatient(building)
                : null;
        if (patientEntry == null && toxicityPatient == null)
        {
            failure = new DomainFailure(
                FailureCode.SurvivalTreatmentTargetMissing,
                building.PersistentInstanceId.Value);
            return false;
        }

        bool detoxTreatment = toxicityPatient != null;
        CharacterActor patient = detoxTreatment
            ? toxicityPatient.Actor
            : SurvivalFoodStatePersistence.FindActor(
                GetSurvivalConsumers(), patientEntry.persistentId);
        if (requestedCompleted)
        {
            resolvedOperationId = requestedOperationId;
            amount = 1;
            failure = DomainFailure.None;
            return true;
        }

        SurvivalTreatmentMaterialSelection material = default;
        if (medical.requiresMedicine
            && !requestedReceipt
            && !stockRuntime.TryEnsureTreatmentMaterial(
                building,
                detoxTreatment,
                actor?.BuildingCharacterId ?? default,
                out material,
                out string supplyFailure))
        {
            failure = new DomainFailure(
                FailureCode.SurvivalTreatmentMaterialMissing,
                supplyFailure);
            return false;
        }

        ServiceSessionSnapshot serviceSession = !requestedCompleted
            ? FindActiveTreatmentServiceSession(building, patient)
            : null;
        BuildingServiceHubAbility serviceHub =
            building.GetServiceHubAbility();
        if (serviceHub != null
            && !requestedCompleted
            && serviceSession == null
            && !serviceSessionRuntime.TryBeginSession(
                new ServiceSessionRequest
                {
                    Hub = building,
                    Actor = patient,
                    ProcessId = serviceHub.supportedProcessIds?
                        .FirstOrDefault() ?? string.Empty,
                    IsInternalActor = Shop.IsInternalStaffUse(patient?.BuildingVisitor),
                    AdvertisedDemand = !Shop.IsInternalStaffUse(patient?.BuildingVisitor)
                },
                out serviceSession,
                out DomainFailure serviceFailure))
        {
            failure = serviceFailure;
            return false;
        }
        if (detoxTreatment)
        {
            CharacterId patientId = CharacterPersistentIdentity.Require(patient);
            CharacterDetoxTreatmentResult detoxResult = default;
            ConsumableOperationId operationId = requestedReceipt
                ? requestedOperationId
                : serviceSession == null
                    ? default
                    : CreateDetoxOperationId(serviceSession.SessionId);
            if (!requestedReceipt
                && consumables.TryGetPendingDetoxTreatment(
                    patientId,
                    out BuildingInstanceId pendingFacilityId,
                    out ConsumableOperationId pendingOperationId)
                && pendingFacilityId.Equals(facilityId))
            {
                operationId = pendingOperationId;
            }
            resolvedOperationId = operationId;
            if ((!requestedCompleted && serviceSession == null)
                || consumables == null
                || !consumables.TryApplyDetoxTreatment(
                    operationId,
                    patientId,
                    facilityId,
                    out detoxResult))
            {
                bool deliveryPending = detoxResult.FailureCode ==
                    CharacterConsumablesFailureCode.DeliveryPending;
                bool committedPending = deliveryPending && consumables != null
                    && consumables.TryGetPendingDetoxTreatment(patientId,
                        out BuildingInstanceId ownerFacility, out ConsumableOperationId ownerOperation)
                    && ownerFacility.Equals(facilityId) && ownerOperation.Equals(operationId);
                if (serviceSession != null && !committedPending
                    && !serviceSessionRuntime.CancelSession(serviceSession.SessionId, detoxResult.Detail))
                {
                    failure = new DomainFailure(FailureCode.ServiceSessionMissing,
                        "detox-unowned-session-cancel-failed:" + serviceSession.SessionId);
                    return false;
                }
                if (!committedPending) resolvedOperationId = default;
                failure = new DomainFailure(deliveryPending
                    ? FailureCode.SurvivalTreatmentMaterialMissing
                    : detoxResult.FailureCode is
                        CharacterConsumablesFailureCode.ItemDefinitionMissing
                        or CharacterConsumablesFailureCode.ItemStackMissing
                        or CharacterConsumablesFailureCode.PhysicalConsumptionFailed
                        ? FailureCode.SurvivalTreatmentMaterialMissing
                        : FailureCode.SurvivalTreatmentTargetMissing,
                    detoxResult.Detail);
                return false;
            }
            if (requestedCompleted)
            {
                amount = 1;
                failure = DomainFailure.None;
                return true;
            }
            if (!TryCompleteDetoxServiceSession(serviceSession)
                || !consumables.TryAcknowledgeDetoxTreatment(operationId))
            {
                failure = new DomainFailure(
                    FailureCode.ServiceSessionMissing,
                    operationId.Value);
                return false;
            }
            PublishWorkResult(new SurvivalFacilityWorkResultEvent(
                SurvivalFacilityWorkResultKind.DetoxTreatment, actor, building, 1,
                patient: patient, patientId: patientId.Value));
            amount = 1;
            failure = DomainFailure.None;
            return true;
        }

        if (medical.requiresMedicine)
        {
            ConsumableOperationId operationId =
                CreateTreatmentOperationId(serviceSession?.SessionId);
            if (!operationId.IsValid || !material.IsReady)
            {
                failure = new DomainFailure(
                    FailureCode.SurvivalTreatmentMaterialMissing,
                    material.DestinationId);
                return false;
            }
            resolvedOperationId = operationId;
            SurvivalTreatmentPlanSaveData plan = new()
            {
                operationId = operationId.Value,
                patientId = patientEntry.persistentId,
                facilityInstanceId = facilityId.Value,
                serviceSessionId = serviceSession?.SessionId ?? string.Empty,
                itemDefinitionId = material.ItemId,
                sourceStackId = material.StackId.Value,
                destinationId = material.DestinationId,
                usedBloodSubstitute = material.UsedBloodSubstitute,
                phase = SurvivalTreatmentPlanPhase.IntentRecorded,
                physicalCommitOperationId =
                    SurvivalTreatmentIdentity.CreateTreatmentPhysicalOperationId(operationId.Value),
                physicalCommitReasonCode = SurvivalTreatmentIdentity.TreatmentPhysicalSinkReason,
                physicalCommitPositionX = building.centerPos.x,
                physicalCommitPositionY = building.centerPos.y
            };
            state.activeTreatmentPlans.Add(plan);
            return TryFinalizeTreatmentPlan(
                actor,
                building,
                medical,
                plan,
                out amount,
                out failure);
        }

        ApplyTreatmentEffects(
            actor,
            building,
            medical,
            patientEntry,
            patient,
            usedBloodSubstitute: false);
        amount = 1;
        CompleteTreatmentServiceSession(serviceSession);
        failure = DomainFailure.None;
        return true;
    }

    private bool TryFinalizeTreatmentPlan(
        IBuildingVisitorPort actor,
        BuildableObject building,
        BuildingMedicalAbility medical,
        SurvivalTreatmentPlanSaveData plan,
        out int amount,
        out DomainFailure failure)
    {
        amount = 0;
        ConsumableOperationId operationId = new(plan?.operationId);
        BuildingInstanceId facilityId = building == null
            ? default
            : building.RequirePersistentInstanceId();
        if (plan == null
            || !operationId.IsValid
            || !facilityId.IsValid
            || !string.Equals(
                plan.facilityInstanceId,
                facilityId.Value,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.SurvivalTreatmentTargetMissing,
                plan?.operationId ?? string.Empty);
            return false;
        }

        CharacterActor patient = GetSurvivalConsumers().FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(
                candidate.Identity?.PersistentId,
                plan.patientId,
                StringComparison.Ordinal));
        bool effectsPending = plan.phase <
            SurvivalTreatmentPlanPhase.EffectsPublished;
        if (patient == null
            || effectsPending && patient.IsDead
            || plan.phase == SurvivalTreatmentPlanPhase.IntentRecorded
                && building.isDestroy)
        {
            failure = new DomainFailure(
                FailureCode.SurvivalTreatmentTargetMissing,
                plan.patientId);
            return false;
        }

        if (plan.phase == SurvivalTreatmentPlanPhase.IntentRecorded)
        {
            if (!stockRuntime.TryCommitTreatmentMaterialPending(
                    plan.destinationId,
                    plan.itemDefinitionId,
                    plan.physicalCommitOperationId,
                    plan.physicalCommitReasonCode,
                    out PhysicalItemBatchDispositionReceipt receipt,
                    out string physicalFailure))
            {
                failure = new DomainFailure(
                    FailureCode.SurvivalTreatmentMaterialMissing,
                    physicalFailure);
                return false;
            }
            plan.physicalCommitId = receipt.CommitId;
            plan.physicalCommitSourceStackIds = receipt.SourceStackIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            plan.physicalCommitQuantity = receipt.Quantity;
            plan.physicalCommitInputMassGrams = receipt.InputMassGrams;
            plan.phase = SurvivalTreatmentPlanPhase.ItemCommitted;
        }

        if (!stockRuntime.TryGetPendingTreatmentMaterial(
                plan.physicalCommitOperationId,
                out PhysicalItemBatchDispositionReceipt pendingReceipt)
            || !TreatmentReceiptMatches(plan, pendingReceipt))
        {
            failure = new DomainFailure(
                FailureCode.SurvivalTreatmentMaterialMissing,
                "treatment-physical-receipt-mismatch:"
                + plan.physicalCommitOperationId);
            return false;
        }

        if (plan.phase == SurvivalTreatmentPlanPhase.ItemCommitted)
        {
            SurvivalHealthSaveData patientEntry = state.health.FirstOrDefault(entry =>
                SurvivalHealthStateRules.IsActiveIssue(entry)
                && string.Equals(
                    entry.persistentId,
                    plan.patientId,
                    StringComparison.Ordinal));
            if (patientEntry == null)
            {
                failure = new DomainFailure(
                    FailureCode.SurvivalTreatmentTargetMissing,
                    plan.patientId);
                return false;
            }
            ApplyTreatmentEffects(
                actor,
                building,
                medical,
                patientEntry,
                patient,
                plan.usedBloodSubstitute);
            plan.phase = SurvivalTreatmentPlanPhase.EffectsPublished;
        }

        if (plan.phase == SurvivalTreatmentPlanPhase.EffectsPublished)
        {
            if (plan.serviceSessionId.Length > 0
                && !serviceSessionRuntime.TryCompleteSession(
                    plan.serviceSessionId,
                    out _,
                    out DomainFailure completionFailure))
            {
                failure = completionFailure.IsFailure
                    ? completionFailure
                    : new DomainFailure(
                        FailureCode.ServiceSessionMissing,
                        plan.serviceSessionId);
                return false;
            }
            plan.phase = SurvivalTreatmentPlanPhase.ServiceCompleted;
        }

        if (!stockRuntime.TryPublishTreatmentTareAndAcknowledge(
                plan.itemDefinitionId,
                new Vector2Int(
                    plan.physicalCommitPositionX,
                    plan.physicalCommitPositionY),
                plan.physicalCommitId,
                out string acknowledgeFailure))
        {
            failure = new DomainFailure(
                FailureCode.SurvivalTreatmentMaterialMissing,
                acknowledgeFailure);
            return false;
        }

        RememberCompletedTreatmentOperation(plan.operationId);
        state.activeTreatmentPlans.Remove(plan);
        amount = 1;
        failure = DomainFailure.None;
        return true;
    }

    private void ApplyTreatmentEffects(
        IBuildingVisitorPort actor,
        BuildableObject building,
        BuildingMedicalAbility medical,
        SurvivalHealthSaveData patientEntry,
        CharacterActor patient,
        bool usedBloodSubstitute)
    {
        float treatmentEfficiency = usedBloodSubstitute ? 0.55f : 1f;
        patientEntry.severity = Mathf.Clamp01(
            patientEntry.severity
            - (Mathf.Max(0f, medical.severityReduction) * treatmentEfficiency));
        patientEntry.remainingSeconds = Mathf.Max(
            0f,
            patientEntry.remainingSeconds - (180f * treatmentEfficiency));
        if (patientEntry.severity <= 0.05f || patientEntry.remainingSeconds <= 0f)
        {
            state.health.Remove(patientEntry);
        }
        else
        {
            patientEntry.state = SurvivalHealthState.Recovering;
        }

        patient.Heal(TreatmentMedicineHeal * treatmentEfficiency);
        if (usedBloodSubstitute)
        {
            SurvivalHealthStateRules.RegisterOrRefresh(
                state,
                patient,
                SurvivalHealthState.Exposed,
                0.25f,
                240f,
                "불안정한 혈액 대체 치료");
            patient.ApplyMoodFactor(
                "survival:blood-treatment",
                "정체불명의 혈액으로 치료받음",
                -4f,
                240f,
                1);
        }
        patient.ApplyMoodFactor(
            "survival:treated",
            "제때 치료받음",
            3f,
            180f,
            1);
        PublishWorkResult(new SurvivalFacilityWorkResultEvent(
            SurvivalFacilityWorkResultKind.Treatment, actor, building, 0,
            patient: patient, patientId: patientEntry.persistentId));
    }

    private SurvivalTreatmentPlanSaveData FindActiveTreatmentPlan(
        ConsumableOperationId requestedOperationId,
        BuildingInstanceId facilityId)
    {
        if (requestedOperationId.IsValid)
        {
            SurvivalTreatmentPlanSaveData exact = state.activeTreatmentPlans
                .FirstOrDefault(plan => plan != null && string.Equals(
                    plan.operationId,
                    requestedOperationId.Value,
                    StringComparison.Ordinal));
            if (exact != null)
            {
                return exact;
            }
        }
        return state.activeTreatmentPlans
            .Where(plan => plan != null && string.Equals(
                plan.facilityInstanceId,
                facilityId.Value,
                StringComparison.Ordinal))
            .OrderBy(plan => plan.operationId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private SurvivalTreatmentPlanSaveData FindTreatmentPlanByOperation(
        ConsumableOperationId operationId) => !operationId.IsValid
        ? null
        : state.activeTreatmentPlans.FirstOrDefault(plan => plan != null
            && string.Equals(
                plan.operationId,
                operationId.Value,
                StringComparison.Ordinal));

    private void RememberCompletedTreatmentOperation(string operationId)
    {
        if (!state.completedTreatmentOperationIds.Contains(
                operationId,
                StringComparer.Ordinal))
        {
            state.completedTreatmentOperationIds.Add(operationId);
            state.completedTreatmentOperationIds.Sort(StringComparer.Ordinal);
        }
        while (state.completedTreatmentOperationIds.Count > 512)
        {
            state.completedTreatmentOperationIds.RemoveAt(0);
        }
    }

    private static bool TreatmentReceiptMatches(
        SurvivalTreatmentPlanSaveData plan,
        PhysicalItemBatchDispositionReceipt receipt) =>
        plan != null
        && receipt.IsCommitted
        && receipt.Kind == PhysicalItemDispositionKind.Sink
        && string.Equals(
            receipt.OperationId,
            plan.physicalCommitOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            plan.physicalCommitReasonCode,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.CommitId,
            plan.physicalCommitId,
            StringComparison.Ordinal)
        && receipt.Quantity == plan.physicalCommitQuantity
        && receipt.InputMassGrams == plan.physicalCommitInputMassGrams
        && receipt.SourceStackIds.SequenceEqual(
            plan.physicalCommitSourceStackIds,
            StringComparer.Ordinal);

    private void CompleteTreatmentServiceSession(ServiceSessionSnapshot serviceSession)
    {
        if (serviceSession != null
            && !serviceSessionRuntime.TryCompleteSession(
                serviceSession.SessionId,
                out _,
                out DomainFailure completionFailure))
        {
            serviceSessionRuntime.CancelSession(
                serviceSession.SessionId,
                completionFailure.Code.ToString());
        }
    }

    internal bool TryGetTreatmentSupply(
        CharacterId patientId,
        out SurvivalTreatmentSupplySnapshot snapshot)
    {
        snapshot = default;
        if (!patientId.IsValid)
        {
            return false;
        }

        SurvivalTreatmentPlanSaveData activePlan = state.activeTreatmentPlans?
            .Where(plan => plan != null
                && string.Equals(
                    plan.patientId,
                    patientId.Value,
                    StringComparison.Ordinal))
            .OrderBy(plan => plan.operationId, StringComparer.Ordinal)
            .FirstOrDefault();
        if (activePlan != null)
        {
            BuildingInstanceId facilityId = new(activePlan.facilityInstanceId);
            ConsumableItemDefinitionId itemId = new(
                activePlan.itemDefinitionId);
            BuildableObject facility = FindLiveMedicalFacility(facilityId);
            if (facility == null
                || !itemId.IsValid
                || string.IsNullOrWhiteSpace(activePlan.destinationId))
            {
                return false;
            }
            if (activePlan.phase >= SurvivalTreatmentPlanPhase.ItemCommitted)
            {
                snapshot = new SurvivalTreatmentSupplySnapshot(
                    patientId,
                    facilityId,
                    itemId,
                    activePlan.destinationId,
                    SurvivalTreatmentKind.Standard,
                    SurvivalTreatmentSupplyState.Processing);
                return true;
            }
            if (!stockRuntime.TryInspectPinnedTreatmentMaterial(
                    facility,
                    itemId.Value,
                    activePlan.destinationId,
                    out SurvivalTreatmentMaterialSelection pinned))
            {
                return false;
            }
            snapshot = CreateTreatmentSupplySnapshot(
                patientId,
                facilityId,
                itemId,
                pinned.DestinationId,
                SurvivalTreatmentKind.Standard,
                pinned.Status);
            return true;
        }

        if (consumables != null
            && consumables.TryGetPendingDetoxTreatment(
                patientId,
                out BuildingInstanceId detoxFacilityId,
                out _,
                out ConsumableItemDefinitionId detoxItemId))
        {
            BuildableObject detoxFacility =
                FindLiveMedicalFacility(detoxFacilityId);
            if (detoxFacility == null)
            {
                return false;
            }
            string detoxDestinationId =
                CharacterConsumablesInputDestinationIdentity.Build(
                    CharacterConsumablesInputKind.MedicalTreatment,
                    detoxFacilityId,
                    detoxItemId);
            snapshot = new SurvivalTreatmentSupplySnapshot(
                patientId,
                detoxFacilityId,
                detoxItemId,
                detoxDestinationId,
                SurvivalTreatmentKind.Detox,
                SurvivalTreatmentSupplyState.Processing);
            return true;
        }

        CharacterActor patient = (worldRegistry.AllCharacters
                ?? Array.Empty<CharacterActor>())
            .Where(candidate => candidate != null && !candidate.IsDead)
            .FirstOrDefault(candidate => string.Equals(
                candidate.Identity?.PersistentId,
                patientId.Value,
                StringComparison.Ordinal));
        if (patient == null)
        {
            return false;
        }

        SurvivalTreatmentSupplySnapshot fallback = default;
        bool hasFallback = false;
        foreach (BuildableObject facility in (worldRegistry.Buildings
                     ?? Array.Empty<BuildableObject>())
                 .Where(candidate => candidate != null
                    && !candidate.isDestroy
                    && candidate.BuildingData?
                        .GetAbility<BuildingMedicalAbility>()?
                        .requiresMedicine == true)
                 .OrderBy(
                     candidate => candidate.RequirePersistentInstanceId().Value,
                     StringComparer.Ordinal))
        {
            SurvivalTreatmentKind kind;
            SurvivalHealthSaveData standardPatient =
                FindTreatmentEntry(facility);
            if (standardPatient != null)
            {
                if (!string.Equals(
                        standardPatient.persistentId,
                        patientId.Value,
                        StringComparison.Ordinal))
                {
                    continue;
                }
                kind = SurvivalTreatmentKind.Standard;
            }
            else
            {
                CharacterActor detoxPatient =
                    FindTreatmentSupplyDetoxPatient(facility);
                if (detoxPatient == null
                    || !CharacterPersistentIdentity.Require(detoxPatient)
                        .Equals(patientId))
                {
                    continue;
                }
                kind = SurvivalTreatmentKind.Detox;
            }

            if (!stockRuntime.TryInspectTreatmentMaterial(
                    facility,
                    detoxOnly: kind == SurvivalTreatmentKind.Detox,
                    out SurvivalTreatmentMaterialSelection material))
            {
                continue;
            }
            SurvivalTreatmentSupplySnapshot candidate =
                CreateTreatmentSupplySnapshot(
                patientId,
                facility.RequirePersistentInstanceId(),
                new ConsumableItemDefinitionId(material.ItemId),
                material.DestinationId,
                kind,
                material.Status);
            if (IsExistingTreatmentSupplyRoute(material.Status))
            {
                snapshot = candidate;
                return true;
            }
            if (!hasFallback)
            {
                fallback = candidate;
                hasFallback = true;
            }
        }
        if (hasFallback)
        {
            snapshot = fallback;
            return true;
        }
        return false;
    }

    private static bool IsExistingTreatmentSupplyRoute(
        SurvivalTreatmentSupplyStatus status) =>
        status is SurvivalTreatmentSupplyStatus.Ready
            or SurvivalTreatmentSupplyStatus.DeliveryRequested
            or SurvivalTreatmentSupplyStatus.DeliveryInTransit
            or SurvivalTreatmentSupplyStatus.DeliveryNoPath;

    private CharacterActor FindTreatmentSupplyDetoxPatient(
        BuildableObject facility)
    {
        if (consumables == null || facility == null)
        {
            return null;
        }
        BuildingInstanceId facilityId = facility.RequirePersistentInstanceId();
        return GetSurvivalConsumers()
            .Where(actor => actor != null && !actor.IsDead)
            .Select(actor => new
            {
                Actor = actor,
                CharacterId = CharacterPersistentIdentity.Require(actor),
                Status = consumables.GetToxicityStatus(
                    CharacterPersistentIdentity.Require(actor))
            })
            .Where(candidate => candidate.Status.Toxicity > 0f
                && (!candidate.Status.TreatmentPending
                    || consumables.TryGetPendingDetoxTreatment(
                        candidate.CharacterId,
                        out BuildingInstanceId pendingFacilityId,
                        out _)
                    && pendingFacilityId.Equals(facilityId)))
            .OrderByDescending(candidate =>
                candidate.Status.TreatmentPending)
            .ThenByDescending(candidate => candidate.Status.Toxicity)
            .ThenBy(
                candidate => candidate.CharacterId.Value,
                StringComparer.Ordinal)
            .Select(candidate => candidate.Actor)
            .FirstOrDefault();
    }

    private BuildableObject FindLiveMedicalFacility(BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
        {
            return null;
        }
        return (worldRegistry.Buildings ?? Array.Empty<BuildableObject>())
            .FirstOrDefault(candidate => candidate != null
                && !candidate.isDestroy
                && string.Equals(
                    candidate.PersistentInstanceId.Value,
                    facilityId.Value,
                    StringComparison.Ordinal)
                && candidate.BuildingData?
                    .GetAbility<BuildingMedicalAbility>() != null);
    }

    private static SurvivalTreatmentSupplySnapshot CreateTreatmentSupplySnapshot(
        CharacterId patientId,
        BuildingInstanceId facilityId,
        ConsumableItemDefinitionId itemId,
        string destinationId,
        SurvivalTreatmentKind kind,
        SurvivalTreatmentSupplyStatus status) => new(
        patientId,
        facilityId,
        itemId,
        destinationId,
        kind,
        status switch
        {
            SurvivalTreatmentSupplyStatus.Ready =>
                SurvivalTreatmentSupplyState.Ready,
            SurvivalTreatmentSupplyStatus.DeliveryRequested =>
                SurvivalTreatmentSupplyState.Requested,
            SurvivalTreatmentSupplyStatus.DeliveryInTransit =>
                SurvivalTreatmentSupplyState.InTransit,
            SurvivalTreatmentSupplyStatus.CapacityUnavailable =>
                SurvivalTreatmentSupplyState.CapacityUnavailable,
            SurvivalTreatmentSupplyStatus.DeliveryNoPath =>
                SurvivalTreatmentSupplyState.NoPath,
            SurvivalTreatmentSupplyStatus.Processing =>
                SurvivalTreatmentSupplyState.Processing,
            SurvivalTreatmentSupplyStatus.AwaitingRequest =>
                SurvivalTreatmentSupplyState.AwaitingRequest,
            _ => SurvivalTreatmentSupplyState.StockMissing
        });

    internal bool TryEnsureTreatmentSupply(
        IBuildingVisitorPort actor,
        BuildableObject building,
        int workRunId,
        out bool completionOnly,
        out DomainFailure failure)
    {
        EnsureStateLists();
        completionOnly = false;
        if (actor == null
            || !actor.BuildingCharacterId.IsValid
            || building == null
            || workRunId <= 0)
        {
            failure = new DomainFailure(
                FailureCode.SurvivalTreatmentTargetMissing,
                "treatment-work-owner-invalid");
            return false;
        }
        BuildingMedicalAbility medical =
            building.BuildingData?.GetAbility<BuildingMedicalAbility>();
        if (medical == null)
        {
            failure = new DomainFailure(
                FailureCode.SurvivalTreatmentUnsupported,
                building.RequirePersistentInstanceId().Value);
            return false;
        }
        BuildingInstanceId facilityId = building.RequirePersistentInstanceId();
        string workKey = CreateTreatmentWorkKey(
            actor.BuildingCharacterId,
            facilityId,
            workRunId);
        if (treatmentOperationsByWork.TryGetValue(
                workKey,
                out ConsumableOperationId requestedOperationId))
        {
            if (state.completedTreatmentOperationIds.Contains(
                    requestedOperationId.Value,
                    StringComparer.Ordinal))
            {
                completionOnly = true;
                failure = DomainFailure.None;
                return true;
            }
            SurvivalTreatmentPlanSaveData requestedPlan =
                FindTreatmentPlanByOperation(requestedOperationId);
            if (requestedPlan != null)
            {
                if (!string.Equals(
                        requestedPlan.facilityInstanceId,
                        facilityId.Value,
                        StringComparison.Ordinal))
                {
                    failure = new DomainFailure(
                        FailureCode.SurvivalTreatmentTargetMissing,
                        requestedOperationId.Value);
                    return false;
                }
                completionOnly = true;
                failure = DomainFailure.None;
                return true;
            }
            if (consumables != null
                && consumables.TryGetDetoxTreatmentOwner(
                    requestedOperationId,
                    out _,
                    out BuildingInstanceId requestedFacilityId,
                    out _))
            {
                if (!requestedFacilityId.Equals(facilityId))
                {
                    failure = new DomainFailure(
                        FailureCode.SurvivalTreatmentTargetMissing,
                        requestedOperationId.Value);
                    return false;
                }
                completionOnly = true;
                failure = DomainFailure.None;
                return true;
            }
        }
        if (FindActiveTreatmentPlan(default, facilityId) != null)
        {
            completionOnly = true;
            failure = DomainFailure.None;
            return true;
        }
        if (TryFindPendingDetoxTreatment(facilityId, out _))
        {
            completionOnly = true;
            failure = DomainFailure.None;
            return true;
        }

        SurvivalHealthSaveData patientEntry = FindTreatmentEntry(building);
        ToxicityTreatmentPatient toxicityPatient = patientEntry == null
            ? FindToxicityTreatmentPatient(building)
            : null;
        if (patientEntry == null && toxicityPatient == null)
        {
            failure = new DomainFailure(
                FailureCode.SurvivalTreatmentTargetMissing,
                facilityId.Value);
            return false;
        }
        if (toxicityPatient != null
            && consumables != null
            && consumables.TryGetPendingDetoxTreatment(
                CharacterPersistentIdentity.Require(toxicityPatient.Actor),
                out BuildingInstanceId pendingFacilityId,
                out _)
            && pendingFacilityId.Equals(facilityId))
        {
            completionOnly = true;
            failure = DomainFailure.None;
            return true;
        }
        if (!medical.requiresMedicine)
        {
            failure = DomainFailure.None;
            return true;
        }
        bool ready = stockRuntime.TryEnsureTreatmentMaterial(
            building,
            toxicityPatient != null,
            actor.BuildingCharacterId,
            out _,
            out string supplyFailure);
        failure = ready
            ? DomainFailure.None
            : new DomainFailure(
                FailureCode.SurvivalTreatmentMaterialMissing,
                supplyFailure);
        return ready;
    }

    internal bool TryApplyTreatmentWork(
        IBuildingVisitorPort actor,
        BuildableObject building,
        int workRunId,
        out int amount,
        out DomainFailure failure)
    {
        amount = 0;
        if (actor == null
            || !actor.BuildingCharacterId.IsValid
            || building == null
            || workRunId <= 0)
        {
            failure = new DomainFailure(
                FailureCode.SurvivalTreatmentTargetMissing,
                "treatment-work-owner-invalid");
            return false;
        }
        string workKey = CreateTreatmentWorkKey(
            actor.BuildingCharacterId,
            building.RequirePersistentInstanceId(),
            workRunId);
        treatmentOperationsByWork.TryGetValue(
            workKey,
            out ConsumableOperationId operationId);
        bool applied = TryApplyTreat(
            actor,
            building,
            operationId,
            out ConsumableOperationId resolvedOperationId,
            out amount,
            out failure);
        if (resolvedOperationId.IsValid)
        {
            RememberTreatmentWorkOperation(workKey, resolvedOperationId);
        }
        return applied;
    }

    internal bool TryReconcileLostTreatmentOwners(out string failureReason)
    {
        EnsureStateLists();
        foreach (SurvivalTreatmentPlanSaveData plan in state.activeTreatmentPlans
                     .Where(value => value != null)
                     .OrderBy(value => value.operationId, StringComparer.Ordinal)
                     .ToArray())
        {
            CharacterActor patient = worldRegistry.AllCharacters.FirstOrDefault(
                candidate => candidate != null
                    && string.Equals(
                        candidate.Identity?.PersistentId,
                        plan.patientId,
                        StringComparison.Ordinal));
            BuildableObject facility = worldRegistry.Buildings.FirstOrDefault(
                candidate => candidate != null
                    && !candidate.isDestroy
                    && string.Equals(
                        candidate.PersistentInstanceId.Value,
                        plan.facilityInstanceId,
                        StringComparison.Ordinal)
                    && candidate.BuildingData?
                        .GetAbility<BuildingMedicalAbility>() != null);
            if (patient != null && !patient.IsDead && facility != null)
            {
                continue;
            }

            if (plan.phase == SurvivalTreatmentPlanPhase.IntentRecorded)
            {
                CancelTreatmentSessionIfNeeded(plan);
                state.activeTreatmentPlans.Remove(plan);
                RemoveTreatmentWorkOperations(plan.operationId);
                continue;
            }
            if (!stockRuntime.TryGetPendingTreatmentMaterial(
                    plan.physicalCommitOperationId,
                    out PhysicalItemBatchDispositionReceipt receipt)
                || !TreatmentReceiptMatches(plan, receipt))
            {
                failureReason = "treatment-terminal-receipt-mismatch:"
                    + plan.physicalCommitOperationId;
                return false;
            }

            CancelTreatmentSessionIfNeeded(plan);
            if (!stockRuntime.TryPublishTreatmentTareAndAcknowledge(
                    plan.itemDefinitionId,
                    new Vector2Int(
                        plan.physicalCommitPositionX,
                        plan.physicalCommitPositionY),
                    plan.physicalCommitId,
                    out failureReason))
            {
                return false;
            }
            RememberCompletedTreatmentOperation(plan.operationId);
            state.activeTreatmentPlans.Remove(plan);
            RemoveTreatmentWorkOperations(plan.operationId);
        }
        failureReason = string.Empty;
        return true;
    }

    private void CancelTreatmentSessionIfNeeded(
        SurvivalTreatmentPlanSaveData plan)
    {
        if (plan != null
            && plan.phase < SurvivalTreatmentPlanPhase.ServiceCompleted
            && !string.IsNullOrEmpty(plan.serviceSessionId))
        {
            serviceSessionRuntime.CancelSession(
                plan.serviceSessionId,
                "survival-treatment-owner-lost");
        }
    }

    private bool TryFindPendingDetoxTreatment(
        BuildingInstanceId facilityId,
        out ConsumableOperationId operationId)
    {
        operationId = default;
        if (consumables == null || !facilityId.IsValid)
        {
            return false;
        }
        foreach (CharacterActor candidate in worldRegistry.AllCharacters
                     .Where(value => value != null
                         && !string.IsNullOrWhiteSpace(
                             value.Identity?.PersistentId))
                     .OrderBy(
                         value => value.Identity.PersistentId,
                         StringComparer.Ordinal))
        {
            if (consumables.TryGetPendingDetoxTreatment(
                    CharacterPersistentIdentity.Require(candidate),
                    out BuildingInstanceId pendingFacilityId,
                    out ConsumableOperationId pendingOperationId)
                && pendingFacilityId.Equals(facilityId))
            {
                operationId = pendingOperationId;
                return true;
            }
        }
        return false;
    }

    private void RememberTreatmentWorkOperation(
        string workKey,
        ConsumableOperationId operationId)
    {
        if (string.IsNullOrEmpty(workKey) || !operationId.IsValid)
        {
            return;
        }
        if (!treatmentOperationsByWork.ContainsKey(workKey)
            && treatmentOperationsByWork.Count >= TreatmentWorkOperationCapacity)
        {
            string retired = treatmentOperationsByWork.Keys
                .OrderBy(value => value, StringComparer.Ordinal)
                .First();
            treatmentOperationsByWork.Remove(retired);
        }
        treatmentOperationsByWork[workKey] = operationId;
    }

    private void RemoveTreatmentWorkOperations(string operationId)
    {
        foreach (string key in treatmentOperationsByWork
                     .Where(pair => string.Equals(
                         pair.Value.Value,
                         operationId,
                         StringComparison.Ordinal))
                     .Select(pair => pair.Key)
                     .ToArray())
        {
            treatmentOperationsByWork.Remove(key);
        }
    }

    private bool TryCompleteDetoxServiceSession(
        ServiceSessionSnapshot serviceSession) =>
        serviceSession != null
        && serviceSessionRuntime.TryCompleteSession(
            serviceSession.SessionId,
            out _,
            out _);

    private static ConsumableOperationId CreateDetoxOperationId(
        string serviceSessionId) => new(
        SurvivalTreatmentIdentity.DetoxOperationPrefix + serviceSessionId);

    private static ConsumableOperationId CreateTreatmentOperationId(
        string serviceSessionId) => string.IsNullOrWhiteSpace(serviceSessionId)
        ? default
        : new ConsumableOperationId(
            SurvivalTreatmentIdentity.TreatmentOperationPrefix + serviceSessionId);

    private static string CreateTreatmentWorkKey(
        CharacterId actorId,
        BuildingInstanceId facilityId,
        int workRunId) =>
        "treatment-work:"
        + Uri.EscapeDataString(actorId.Value) + ":"
        + Uri.EscapeDataString(facilityId.Value) + ":"
        + workRunId.ToString(
            System.Globalization.CultureInfo.InvariantCulture);

    private ServiceSessionSnapshot FindActiveTreatmentServiceSession(
        BuildableObject building,
        CharacterActor patient)
    {
        if (serviceSessionRuntime is not IServiceSessionRuntime sessions
            || building == null
            || patient == null)
        {
            return null;
        }
        string hubId = building.RequirePersistentInstanceId().Value;
        string actorId = patient.BuildingCharacterId.Value;
        return sessions.ActiveSessions
            .Where(session => session != null
                && session.IsActive
                && string.Equals(session.HubId, hubId, StringComparison.Ordinal)
                && string.Equals(session.ActorId, actorId, StringComparison.Ordinal))
            .OrderBy(session => session.SessionId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private bool HasTreatableHealth()
    {
        return SurvivalHealthStateRules.HasTreatable(state);
    }

    internal bool HasAvailableTreatment(BuildableObject building)
    {
        BuildingMedicalAbility medical =
            building?.BuildingData?.GetAbility<BuildingMedicalAbility>();
        if (medical == null)
            return false;
        BuildingInstanceId facilityId = building.RequirePersistentInstanceId();
        bool standardTreatmentAvailable = HasTreatableHealth()
            && (!medical.requiresMedicine
                || state.activeTreatmentPlans.Any(plan => plan != null
                    && string.Equals(
                        plan.facilityInstanceId,
                        facilityId.Value,
                        StringComparison.Ordinal))
                || stockRuntime.HasPotentialTreatmentMaterial(
                    detoxOnly: false));
        return standardTreatmentAvailable
            || building.GetServiceHubAbility() != null
            && FindToxicityTreatmentPatient(building) != null;
    }

    private ToxicityTreatmentPatient FindToxicityTreatmentPatient(
        BuildableObject building)
    {
        if (consumables == null || building == null)
            return null;
        BuildingInstanceId facilityId = building.RequirePersistentInstanceId();
        ToxicityTreatmentPatient[] candidates = GetSurvivalConsumers()
            .Where(actor => actor != null && !actor.IsDead)
            .Select(actor => new ToxicityTreatmentPatient(
                actor,
                consumables.GetToxicityStatus(
                    CharacterPersistentIdentity.Require(actor))))
            .Where(candidate => candidate.Status.TreatmentPending
                || candidate.Status.Toxicity > 0f
                && candidate.Status.TreatmentAvailable)
            .OrderByDescending(candidate =>
            {
                CharacterId characterId =
                    CharacterPersistentIdentity.Require(candidate.Actor);
                return consumables.TryGetPendingDetoxTreatment(
                        characterId,
                        out BuildingInstanceId pendingFacility,
                        out _)
                    && pendingFacility.Equals(facilityId);
            })
            .ThenByDescending(candidate => candidate.Status.Toxicity)
            .ThenBy(
                candidate => CharacterPersistentIdentity.Require(candidate.Actor).Value,
                StringComparer.Ordinal)
            .ToArray();
        return candidates.FirstOrDefault(candidate =>
        {
            CharacterId characterId =
                CharacterPersistentIdentity.Require(candidate.Actor);
            return !candidate.Status.TreatmentPending
                || consumables.TryGetPendingDetoxTreatment(
                    characterId,
                    out BuildingInstanceId pendingFacility,
                    out _)
                && pendingFacility.Equals(facilityId);
        });
    }

    private SurvivalHealthSaveData FindTreatmentEntry(
        BuildableObject building)
    {
        bool useFirstActive = building.GetServiceHubAbility() != null
            && serviceSessionRuntime.GetHubSnapshot(building).Mode
                == ServiceOperationMode.Direct;
        return SurvivalHealthStateRules.FindTreatmentEntry(state, useFirstActive);
    }

    private void PublishWorkResult(SurvivalFacilityWorkResultEvent fact)
    {
        try { gameEventBus.Publish(fact); }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        {
            Debug.LogWarning("survival-work-observation:" + exception.GetType().Name + ":" + exception.Message);
        }
    }
}
