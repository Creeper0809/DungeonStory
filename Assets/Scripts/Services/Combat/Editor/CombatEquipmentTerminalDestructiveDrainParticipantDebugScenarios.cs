using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class CombatEquipmentTerminalDestructiveDrainParticipantDebugScenarios
{
    [MenuItem("DungeonStory/V27/Physical Mass/Verify Combat Terminal Drain Participant")]
    public static void Run()
    {
        Verify(CombatEquipmentTerminalSourceKind.CraftOrder, "craft-fixture");
        Verify(CombatEquipmentTerminalSourceKind.RepairOrder, "repair-fixture");
        Debug.Log("V27_COMBAT_EQUIPMENT_TERMINAL_DRAIN_PARTICIPANT=PASS");
    }

    private static void Verify(CombatEquipmentTerminalSourceKind kind, string sourceId)
    {
        const string facilityText = "combat-terminal-facility";
        BuildingInstanceId facilityId = (BuildingInstanceId)facilityText;
        CombatEquipmentTerminalFrozenSubject source = CreateSource(
            kind, sourceId, facilityText);
        string contribution = ProductionFacilityDestructiveDrainCanonical
            .ComputeFingerprint("combat-terminal-contribution:" + kind);
        FakeProducer producer = new();
        CombatEquipmentTerminalDestructiveDrainParticipant participant = new(
            new FakeLifecycle(facilityId, contribution),
            new FakeSources(source), producer, producer,
            new NoChildDrain(), new FakeFacility(facilityId));
        ProductionFacilityDestructiveDrainOperationId operation =
            ProductionFacilityDestructiveDrainOperationId.FromFacility(facilityId);
        ProductionFacilityDestructiveDrainParticipantPlan plan = participant.Prepare(
            new ProductionFacilityDestructiveDrainPrepareContext(
                operation, ProductionFacilityDestructiveDrainCause.ExplicitDemolition,
                facilityId, ProductionOutputDestinationId.FromFacility(facilityId),
                contribution));
        Require(plan.Owners.Count == 1, "owner-count");
        ProductionFacilityDestructiveDrainOwnerPlan ownerPlan = plan.Owners[0];
        Require(string.Equals(ownerPlan.OwnerStableId, source.OwnerStableId,
            StringComparison.Ordinal), "owner-id");
        string step = ProductionFacilityDestructiveDrainCanonical.BuildStepOperationId(
            operation, CombatEquipmentTerminalDrainCanonical.ParticipantId,
            source.OwnerStableId);
        ProductionFacilityDestructiveDrainOwnerSaveData owner = new()
        {
            ownerStableId = ownerPlan.OwnerStableId,
            disposition = ownerPlan.Disposition,
            targetDestinationId = ownerPlan.TargetDestinationId,
            stepOperationId = step,
            phase = ProductionFacilityDestructiveDrainStepPhase.Planned,
            requestFingerprint = ownerPlan.RequestFingerprint
        };
        ProductionFacilityDestructiveDrainStepContext context = new(
            operation, facilityId, participant.ParticipantId, owner, contribution);
        Require(participant.TryPrepareDurable(context, out string failure),
            "durable:" + failure);
        Require(producer.TryCapture(step, out CombatEquipmentTerminalDrainSaveData stored),
            "producer-missing");
        Require(string.Equals(stored.source.sourceFingerprint,
            source.SourceFingerprint, StringComparison.Ordinal), "source-fingerprint");
        Require(stored.source.sourceKind == kind, "owner-kind");

        ProductionFacilityDestructiveDrainRecoveryResult ahead = participant.Recover(context);
        Require(ahead.Action == ProductionFacilityDestructiveDrainRecoveryAction.ResumeCommit
            && ahead.Step.Status == ProductionFacilityDestructiveDrainStepStatus.Deferred,
            "producer-ahead-recovery");
        ProductionFacilityDestructiveDrainStepResult committed =
            participant.TryCommit(context);
        Require(committed.Status == ProductionFacilityDestructiveDrainStepStatus.Applied,
            "commit-map");
        owner.phase = ProductionFacilityDestructiveDrainStepPhase
            .EffectCommittedAwaitingOwnerAck;
        owner.commitId = committed.CommitId;
        owner.receiptFingerprint = committed.ReceiptFingerprint;
        context = new ProductionFacilityDestructiveDrainStepContext(
            operation, facilityId, participant.ParticipantId, owner, contribution);
        Require(participant.Recover(context).Action ==
            ProductionFacilityDestructiveDrainRecoveryAction.ResumeAcknowledge,
            "commit-recovery-map");
        Require(participant.TryAcknowledge(context).Status ==
            ProductionFacilityDestructiveDrainStepStatus.Applied, "ack-map");
        owner.phase = ProductionFacilityDestructiveDrainStepPhase.OwnerAcknowledged;
        context = new ProductionFacilityDestructiveDrainStepContext(
            operation, facilityId, participant.ParticipantId, owner, contribution);
        Require(participant.Recover(context).Action ==
            ProductionFacilityDestructiveDrainRecoveryAction.AlreadyAcknowledged,
            "ack-recovery-map");

        ProductionFacilityDestructiveDrainOwnerSaveData drift = owner.Clone();
        drift.phase = ProductionFacilityDestructiveDrainStepPhase.Planned;
        drift.commitId = string.Empty;
        drift.receiptFingerprint = string.Empty;
        drift.requestFingerprint = ProductionFacilityDestructiveDrainCanonical
            .ComputeFingerprint("drift:" + kind);
        ProductionFacilityDestructiveDrainStepContext driftContext = new(
            operation, facilityId, participant.ParticipantId, drift, contribution);
        Require(participant.Recover(driftContext).Action ==
            ProductionFacilityDestructiveDrainRecoveryAction.Conflict,
            "request-drift-rejected");
    }

    private static CombatEquipmentTerminalFrozenSubject CreateSource(
        CombatEquipmentTerminalSourceKind kind,
        string sourceId,
        string facilityId)
    {
        CombatEquipmentTerminalMassAccounting mass = new(0, 0L, 1, 1000L, 0L, 1000L);
        CombatEquipmentTerminalFrozenSubject result;
        bool valid;
        if (kind == CombatEquipmentTerminalSourceKind.CraftOrder)
        {
            valid = CombatEquipmentTerminalFrozenSubject.TryCreateCraftOrder(
                new CombatEquipmentCraftOrderSaveData
                {
                    orderId = sourceId,
                    facilityPersistentId = facilityId,
                    materialDestinationId = "craft-input:" + sourceId
                }, mass, out result, out _);
        }
        else
        {
            valid = CombatEquipmentTerminalFrozenSubject.TryCreateRepairOrder(
                new CombatEquipmentRepairOrder
                {
                    orderId = sourceId,
                    equipmentInstanceId = "equipment:" + sourceId,
                    facilityBuildingId = facilityId
                }, mass, out result, out _);
        }
        if (!valid || result == null)
            throw new InvalidOperationException("source-fixture-invalid");
        return result;
    }

    private static void Require(bool condition, string code)
    {
        if (!condition) throw new InvalidOperationException(code);
    }

    private sealed class FakeSources : ICombatEquipmentTerminalFacilitySourceQuery
    {
        private readonly CombatEquipmentTerminalPreparedSource source;
        public FakeSources(CombatEquipmentTerminalFrozenSubject source)
        {
            ProductionInputDestinationCustodySourceSnapshot custody =
                string.IsNullOrEmpty(source.InputDestinationId)
                    ? null
                    : new ProductionInputDestinationCustodySourceSnapshot(
                        source.InputDestinationId,
                        1L,
                        new string('7', 64),
                        Array.Empty<
                            ProductionInputDestinationDrainStackSaveData>(),
                        Array.Empty<
                            ProductionInputDestinationDrainOperationSaveData>(),
                        Array.Empty<
                            ProductionInputDestinationDrainActorSaveData>(),
                        0,
                        0L);
            if (!CombatEquipmentTerminalPreparedSource.TryCreate(
                    source, custody, out this.source, out string failure))
            {
                throw new InvalidOperationException(failure);
            }
        }
        public IReadOnlyList<CombatEquipmentTerminalPreparedSource>
            CaptureFacilitySources(BuildingInstanceId facilityId) =>
            new[] { source };
    }

    private sealed class FakeLifecycle : IProductionOutputDestinationLifecycleQuery
    {
        private readonly BuildingInstanceId facility;
        private readonly string fingerprint;
        public FakeLifecycle(BuildingInstanceId facility, string fingerprint)
        { this.facility = facility; this.fingerprint = fingerprint; }
        public ProductionOutputDestinationLifecycleSnapshot Capture(BuildingInstanceId value)
        {
            Require(value.Equals(facility), "facility-drift");
            ProductionOutputDestinationLifecycleContribution contribution = new(
                CombatEquipmentTerminalDrainCanonical.ParticipantId, true, 0L, 1, 0L,
                Array.Empty<ProductionOutputLifecycleBlock>(), fingerprint, fingerprint);
            return new ProductionOutputDestinationLifecycleSnapshot(
                facility, ProductionOutputDestinationId.FromFacility(facility),
                new[] { contribution }, fingerprint, fingerprint);
        }
    }

    private sealed class FakeFacility : ICombatEquipmentTerminalFacilityQuery
    {
        private readonly ProductionFacilityHandle facility;
        public FakeFacility(BuildingInstanceId id) => facility = new(
            new object(), id, new Vector2Int(4, 7), false, string.Empty, false,
            default, "fixture-facility", "combat-crafting", 2);
        public ProductionFacilityHandle Capture(BuildingInstanceId facilityId) => facility;
    }

    private sealed class FakeProducer :
        ICombatEquipmentTerminalDrainQuery,
        ICombatEquipmentTerminalDrainCommand
    {
        private CombatEquipmentTerminalDrainSaveData state;
        public CombatEquipmentTerminalDrainResult TryPrepare(
            CombatEquipmentTerminalDrainRequest request)
        {
            if (state != null)
                return Result(CombatEquipmentTerminalDrainStatus.Replay);
            state = new CombatEquipmentTerminalDrainSaveData
            {
                parentOperationId = request.ParentOperationId,
                stepOperationId = request.StepOperationId,
                source = Freeze(request.Source),
                inputDestinationDrainStepOperationId = request.InputDestinationDrainStepOperationId,
                inputDestinationDrainRequestFingerprint = request.InputDestinationDrainRequestFingerprint,
                requestFingerprint = request.RequestFingerprint,
                phase = CombatEquipmentTerminalDrainPhase.PreparedAwaitingInputDestinationReceipt
            };
            return Result(CombatEquipmentTerminalDrainStatus.Applied);
        }
        public CombatEquipmentTerminalDrainResult TryProgress(string stepOperationId)
        {
            state.commitId = CombatEquipmentTerminalDrainCanonical.CreateCommitId(
                state.stepOperationId, state.requestFingerprint);
            state.terminalEffectFingerprint = ProductionFacilityDestructiveDrainCanonical
                .ComputeFingerprint("terminal:" + state.requestFingerprint);
            state.receiptFingerprint = CombatEquipmentTerminalDrainCanonical
                .CreateReceiptFingerprint(state.requestFingerprint,
                    state.terminalEffectFingerprint, state.commitId);
            state.phase = CombatEquipmentTerminalDrainPhase
                .TerminalEffectsCommittedAwaitingOwnerAcknowledgement;
            return Result(CombatEquipmentTerminalDrainStatus.Applied);
        }
        public CombatEquipmentTerminalDrainResult TryAcknowledge(
            string stepOperationId, string receiptFingerprint)
        {
            if (!string.Equals(state.receiptFingerprint, receiptFingerprint,
                    StringComparison.Ordinal))
                return new CombatEquipmentTerminalDrainResult(
                    CombatEquipmentTerminalDrainStatus.Conflict, default,
                    string.Empty, string.Empty, "receipt-drift");
            state.phase = CombatEquipmentTerminalDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc;
            return Result(CombatEquipmentTerminalDrainStatus.Applied);
        }
        public CombatEquipmentTerminalDrainResult TryGarbageCollect(string step, string receipt) =>
            Result(CombatEquipmentTerminalDrainStatus.Replay);
        public CombatEquipmentTerminalDrainResult TryRecover(string step) => TryProgress(step);
        public bool TryCapture(string step, out CombatEquipmentTerminalDrainSaveData record)
        { record = state?.Clone(); return state != null && string.Equals(state.stepOperationId, step, StringComparison.Ordinal); }
        public IReadOnlyList<CombatEquipmentTerminalDrainSaveData> CaptureCurrentFormat() =>
            state == null ? Array.Empty<CombatEquipmentTerminalDrainSaveData>() : new[] { state.Clone() };
        public bool TryCaptureLiveSource(string owner, out CombatEquipmentTerminalFrozenSubject source,
            out string fingerprint, out string failure)
        { source = null; fingerprint = string.Empty; failure = "fixture-unused"; return false; }
        public bool TryCaptureLiveSourceForPreparation(string owner,
            out CombatEquipmentTerminalPreparedSource prepared,
            out string failure)
        { prepared = null; failure = "fixture-unused"; return false; }
        public bool TryRestoreCurrentFormat(IEnumerable<CombatEquipmentTerminalDrainSaveData> records,
            IEnumerable<ProductionInputDestinationCustodyDrainSaveData> children, out string failure)
        { failure = "fixture-unused"; return false; }
        private CombatEquipmentTerminalDrainResult Result(CombatEquipmentTerminalDrainStatus status) =>
            new(status, state.phase, state.commitId, state.receiptFingerprint, string.Empty);

        private static CombatEquipmentTerminalFrozenSourceSaveData Freeze(
            CombatEquipmentTerminalFrozenSubject source) => new()
        {
            sourceKind = source.SourceKind,
            ownerStableId = source.OwnerStableId,
            sourceId = source.SourceId,
            facilityId = source.FacilityId,
            inputDestinationId = source.InputDestinationId,
            sourcePayload = source.SourcePayload,
            sourceFingerprint = source.SourceFingerprint,
            pendingInputQuantity = source.PendingInputQuantity,
            pendingInputMassGrams = source.PendingInputMassGrams,
            wipInputQuantity = source.WipInputQuantity,
            wipInputMassGrams = source.WipInputMassGrams,
            committedOutputMassGrams = source.CommittedOutputMassGrams,
            declaredLossMassGrams = source.DeclaredLossMassGrams
        };
    }

    private sealed class NoChildDrain : IProductionInputDestinationCustodyDrainService
    {
        public bool RequiresImmediateRecoveryBeforeGameplayTick => true;
        public bool TryCaptureSource(string sourceDestinationId,
            out ProductionInputDestinationCustodySourceSnapshot snapshot,
            out string failureReason)
        { snapshot = null; failureReason = "fixture-no-child"; return false; }
        public bool TryBuildRequest(string parentOperationId,
            string stepOperationId, string ownerStableId, string billId,
            string facilityId, Vector2Int ownerPosition,
            string sourceClaimFingerprint,
            ProductionInputDestinationCustodySourceSnapshot snapshot,
            out ProductionInputDestinationCustodyDrainRequest request,
            out string failureReason)
        { request = null; failureReason = "fixture-no-child"; return false; }
        public bool TryCaptureRequest(string parentOperationId, string stepOperationId,
            string ownerStableId, string billId, string facilityId,
            string sourceDestinationId, Vector2Int ownerPosition,
            string sourceClaimFingerprint,
            out ProductionInputDestinationCustodyDrainRequest request,
            out string failureReason)
        { request = null; failureReason = "fixture-no-child"; return false; }
        public ProductionInputDestinationCustodyDrainResult TryPrepare(
            ProductionInputDestinationCustodyDrainRequest request) => throw new NotSupportedException();
        public ProductionInputDestinationCustodyDrainResult TryCommit(string step, string fingerprint) => throw new NotSupportedException();
        public ProductionInputDestinationCustodyDrainResult TryAcknowledge(string step, string fingerprint) => throw new NotSupportedException();
        public ProductionInputDestinationCustodyDrainResult TryGarbageCollect(string step, string fingerprint) => throw new NotSupportedException();
        public bool TryCapture(string step, out ProductionInputDestinationCustodyDrainSaveData record)
        { record = null; return false; }
        public IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData> CaptureCurrentFormat() =>
            Array.Empty<ProductionInputDestinationCustodyDrainSaveData>();
        public bool TryRestoreCurrentFormat(IEnumerable<ProductionInputDestinationCustodyDrainSaveData> records,
            out string failureReason)
        { failureReason = "fixture-unused"; return false; }
    }
}
