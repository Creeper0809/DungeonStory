using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class
    ProductionPhysicalCustodyDestructiveDrainParticipantDebugScenarios
{
    private static readonly BuildingInstanceId FacilityId =
        (BuildingInstanceId)"building:qa-physical-participant";
    private static readonly ProductionOutputDestinationId DestinationId =
        ProductionOutputDestinationId.FromFacility(FacilityId);
    private static readonly ProductionFacilityDestructiveDrainOperationId
        OperationId = ProductionFacilityDestructiveDrainOperationId
            .FromFacility(FacilityId);
    private static readonly string ContributionFingerprint = Digest('a');
    private static readonly string ResultFingerprint = Digest('b');
    private static readonly string ReceiptFingerprint = Digest('c');
    private const string CommitId = "commit:qa-physical-participant";

    [MenuItem(
        "DungeonStory/Debug/Economy/Run Physical Custody Destructive Drain Participant Contracts")]
    public static void RunAll()
    {
        VerifyDeterministicPrepareAndOwnerContract();
        VerifyEmptyContributionProducesNoOwner();
        VerifyDurablePrepareRejectsFieldAndGramDrift();
        VerifyCommitAndAcknowledgeStatusMapping();
        VerifyTerminalFieldGuardFailsClosed();
        VerifyRecoveryRequiresExactProducerReceipt();
        Debug.Log(
            "Physical-custody destructive-drain participant contracts passed.");
    }

    private static void VerifyEmptyContributionProducesNoOwner()
    {
        Fixture fixture = Fixture.Create(hasAuthority: false);
        ProductionFacilityDestructiveDrainParticipantPlan first =
            fixture.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainParticipantPlan second =
            fixture.Participant.Prepare(CreatePrepareContext());
        Require(
            first.Owners.Count == 0
            && second.Owners.Count == 0
            && string.Equals(
                first.PlanFingerprint,
                second.PlanFingerprint,
                StringComparison.Ordinal)
            && fixture.Port.CaptureCalls == 0,
            "An empty physical contribution created a synthetic owner or touched Items.");
    }

    private static void VerifyDeterministicPrepareAndOwnerContract()
    {
        Fixture fixture = Fixture.Create();
        ProductionFacilityDestructiveDrainParticipantPlan first =
            fixture.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainParticipantPlan second =
            fixture.Participant.Prepare(CreatePrepareContext());

        Require(
            string.Equals(
                first.ParticipantId,
                ProductionFacilityDestructiveDrainParticipantIds
                    .PhysicalCustodyCarryRecovery,
                StringComparison.Ordinal)
            && first.ContractVersion == 1
            && first.DependsOn(second)
            && fixture.Participant.DependsOnParticipantIds.SequenceEqual(
                new[]
                {
                    ProductionFacilityDestructiveDrainParticipantIds
                        .CapacityRoutingOutbox
                },
                StringComparer.Ordinal),
            "Physical participant header or dependency contract drifted.");
        Require(
            first.Owners.Count == 1
            && second.Owners.Count == 1
            && string.Equals(
                first.PlanFingerprint,
                second.PlanFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                first.DurableContributionFingerprint,
                ContributionFingerprint,
                StringComparison.Ordinal),
            "Prepare did not produce one deterministic physical owner.");

        ProductionFacilityDestructiveDrainOwnerPlan owner = first.Owners[0];
        string expectedOwner =
            ProductionFacilityDestructiveDrainOwnerStableIds
                .PhysicalDestination(DestinationId.Value);
        Require(
            string.Equals(owner.OwnerStableId, expectedOwner,
                StringComparison.Ordinal)
            && owner.Disposition ==
                ProductionFacilityDestructiveDrainDisposition.Terminalize
            && string.IsNullOrEmpty(owner.TargetDestinationId)
            && IsDigest(owner.RequestFingerprint)
            && fixture.Port.CaptureCalls == 2,
            "Prepare did not preserve the exact destination request identity.");
    }

    private static void VerifyDurablePrepareRejectsFieldAndGramDrift()
    {
        Fixture fixture = Fixture.Create();
        ProductionFacilityDestructiveDrainParticipantPlan plan =
            fixture.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainStepContext context =
            CreateStepContext(plan.Owners[0], plan.DurableContributionFingerprint);

        Require(
            fixture.Participant.TryPrepareDurable(
                context,
                out string firstFailure)
            && string.IsNullOrEmpty(firstFailure)
            && fixture.Port.PrepareCalls == 1,
            "Exact durable physical request did not prepare.");

        fixture.Port.StackId = "stack:qa-physical-b";
        Require(
            !fixture.Participant.TryPrepareDurable(
                context,
                out string fieldFailure)
            && string.Equals(
                fieldFailure,
                "production-physical-custody-durable-prepare-plan-drift",
                StringComparison.Ordinal)
            && fixture.Port.PrepareCalls == 1,
            "One-field physical source drift reached the lower producer.");

        fixture.Port.StackId = "stack:qa-physical-a";
        fixture.Port.InputMassGrams = 1001L;
        Require(
            !fixture.Participant.TryPrepareDurable(
                context,
                out string gramFailure)
            && string.Equals(
                gramFailure,
                "production-physical-custody-durable-prepare-plan-drift",
                StringComparison.Ordinal)
            && fixture.Port.PrepareCalls == 1,
            "One-gram physical source drift reached the lower producer.");
    }

    private static void VerifyCommitAndAcknowledgeStatusMapping()
    {
        Fixture fixture = Fixture.Create();
        ProductionFacilityDestructiveDrainParticipantPlan plan =
            fixture.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainStepContext planned =
            CreateStepContext(plan.Owners[0], plan.DurableContributionFingerprint);
        Require(
            fixture.Participant.TryPrepareDurable(planned, out _),
            "Status fixture could not durably prepare.");

        fixture.Port.NextCommit = Result(
            ProductionPhysicalCustodyDrainStatus.Applied);
        ProductionFacilityDestructiveDrainStepResult progress =
            fixture.Participant.TryCommit(planned);
        Require(
            progress.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Deferred
            && string.IsNullOrEmpty(progress.CommitId)
            && string.IsNullOrEmpty(progress.ReceiptFingerprint),
            "Non-terminal lower Applied leaked as an upper commit.");

        fixture.Port.NextCommit = Result(
            ProductionPhysicalCustodyDrainStatus.Deferred);
        Require(
            fixture.Participant.TryCommit(planned).Status ==
                ProductionFacilityDestructiveDrainStepStatus.Deferred,
            "Lower Deferred did not remain deferred.");
        fixture.Port.NextCommit = Result(
            ProductionPhysicalCustodyDrainStatus.Conflict);
        Require(
            fixture.Participant.TryCommit(planned).Status ==
                ProductionFacilityDestructiveDrainStepStatus.Conflict,
            "Lower Conflict did not fail closed.");

        fixture.Port.NextCommit = Result(
            ProductionPhysicalCustodyDrainStatus.Applied,
            CommitId,
            ReceiptFingerprint);
        ProductionFacilityDestructiveDrainStepResult applied =
            fixture.Participant.TryCommit(planned);
        Require(
            applied.Status == ProductionFacilityDestructiveDrainStepStatus.Applied
            && string.Equals(applied.CommitId, CommitId,
                StringComparison.Ordinal)
            && string.Equals(applied.ReceiptFingerprint, ReceiptFingerprint,
                StringComparison.Ordinal),
            "Terminal lower Applied did not preserve exact terminal fields.");

        ProductionFacilityDestructiveDrainStepContext awaitingAck =
            CreateStepContext(
                plan.Owners[0],
                plan.DurableContributionFingerprint,
                ProductionFacilityDestructiveDrainStepPhase
                    .EffectCommittedAwaitingOwnerAck,
                CommitId,
                ReceiptFingerprint);
        fixture.Port.NextAcknowledge = Result(
            ProductionPhysicalCustodyDrainStatus.Replay,
            CommitId,
            ReceiptFingerprint);
        ProductionFacilityDestructiveDrainStepResult acknowledged =
            fixture.Participant.TryAcknowledge(awaitingAck);
        Require(
            acknowledged.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Replay
            && fixture.Port.State.phase ==
                ProductionPhysicalCustodyDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc,
            "Exact terminal acknowledgement did not replay safely.");
    }

    private static void VerifyTerminalFieldGuardFailsClosed()
    {
        Fixture fixture = Fixture.Create();
        ProductionFacilityDestructiveDrainParticipantPlan plan =
            fixture.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainStepContext planned =
            CreateStepContext(plan.Owners[0], plan.DurableContributionFingerprint);
        Require(fixture.Participant.TryPrepareDurable(planned, out _),
            "Terminal-field fixture could not prepare.");

        fixture.Port.NextCommit = Result(
            ProductionPhysicalCustodyDrainStatus.Applied,
            CommitId,
            string.Empty);
        Require(
            fixture.Participant.TryCommit(planned).Status ==
                ProductionFacilityDestructiveDrainStepStatus.Conflict,
            "A half-populated terminal result escaped the participant guard.");

        ProductionFacilityDestructiveDrainOwnerPlan invalidOwner = new(
            plan.Owners[0].OwnerStableId,
            ProductionFacilityDestructiveDrainDisposition.Transfer,
            "warehouse:qa-invalid-target",
            plan.Owners[0].RequestFingerprint);
        ProductionFacilityDestructiveDrainStepContext invalidContext =
            CreateStepContext(invalidOwner, plan.DurableContributionFingerprint);
        Require(
            fixture.Participant.TryCommit(invalidContext).Status ==
                ProductionFacilityDestructiveDrainStepStatus.Conflict,
            "A non-terminalize physical journal owner reached Items mutation.");
    }

    private static void VerifyRecoveryRequiresExactProducerReceipt()
    {
        Fixture fixture = Fixture.Create();
        ProductionFacilityDestructiveDrainParticipantPlan plan =
            fixture.Participant.Prepare(CreatePrepareContext());
        ProductionFacilityDestructiveDrainStepContext planned =
            CreateStepContext(plan.Owners[0], plan.DurableContributionFingerprint);
        Require(fixture.Participant.TryPrepareDurable(planned, out _),
            "Recovery fixture could not prepare.");

        ProductionFacilityDestructiveDrainRecoveryResult progress =
            fixture.Participant.Recover(planned);
        Require(
            progress.Action ==
                ProductionFacilityDestructiveDrainRecoveryAction.ResumeCommit
            && progress.Step.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Deferred,
            "Prepared producer did not recover as ResumeCommit/Deferred.");

        fixture.Port.MakeTerminal(
            ProductionPhysicalCustodyDrainPhase
                .EffectCommittedAwaitingOwnerAck,
            CommitId,
            ReceiptFingerprint);
        ProductionFacilityDestructiveDrainStepContext exact =
            CreateStepContext(
                plan.Owners[0],
                plan.DurableContributionFingerprint,
                ProductionFacilityDestructiveDrainStepPhase
                    .EffectCommittedAwaitingOwnerAck,
                CommitId,
                ReceiptFingerprint);
        ProductionFacilityDestructiveDrainRecoveryResult recovered =
            fixture.Participant.Recover(exact);
        Require(
            recovered.Action ==
                ProductionFacilityDestructiveDrainRecoveryAction
                    .ResumeAcknowledge
            && recovered.Step.Status ==
                ProductionFacilityDestructiveDrainStepStatus.Replay,
            "Exact producer/journal terminal receipt did not resume acknowledgement.");

        ProductionFacilityDestructiveDrainStepContext mismatched =
            CreateStepContext(
                plan.Owners[0],
                plan.DurableContributionFingerprint,
                ProductionFacilityDestructiveDrainStepPhase
                    .EffectCommittedAwaitingOwnerAck,
                CommitId,
                Digest('d'));
        Require(
            fixture.Participant.Recover(mismatched).Action ==
                ProductionFacilityDestructiveDrainRecoveryAction.Conflict,
            "A journal/producer receipt mismatch did not fail recovery closed.");

        fixture.Port.State.inputMassGrams++;
        Require(
            fixture.Participant.Recover(exact).Action ==
                ProductionFacilityDestructiveDrainRecoveryAction.Conflict,
            "A one-gram producer request mutation survived recovery validation.");
    }

    private static ProductionFacilityDestructiveDrainPrepareContext
        CreatePrepareContext() => new(
        OperationId,
        ProductionFacilityDestructiveDrainCause.ExplicitDemolition,
        FacilityId,
        DestinationId,
        Digest('f'));

    private static ProductionFacilityDestructiveDrainStepContext
        CreateStepContext(
            ProductionFacilityDestructiveDrainOwnerPlan owner,
            string contributionFingerprint,
            ProductionFacilityDestructiveDrainStepPhase phase =
                ProductionFacilityDestructiveDrainStepPhase.Planned,
            string commitId = "",
            string receiptFingerprint = "") => new(
        OperationId,
        FacilityId,
        ProductionFacilityDestructiveDrainParticipantIds
            .PhysicalCustodyCarryRecovery,
        new ProductionFacilityDestructiveDrainOwnerSaveData
        {
            ownerStableId = owner.OwnerStableId,
            disposition = owner.Disposition,
            targetDestinationId = owner.TargetDestinationId,
            stepOperationId = ProductionFacilityDestructiveDrainCanonical
                .BuildStepOperationId(
                    OperationId,
                    ProductionFacilityDestructiveDrainParticipantIds
                        .PhysicalCustodyCarryRecovery,
                    owner.OwnerStableId),
            phase = phase,
            requestFingerprint = owner.RequestFingerprint,
            commitId = commitId,
            receiptFingerprint = receiptFingerprint
        },
        contributionFingerprint);

    private static ProductionPhysicalCustodyDrainResult Result(
        ProductionPhysicalCustodyDrainStatus status,
        string commitId = "",
        string receiptFingerprint = "") => new(
        status,
        commitId,
        receiptFingerprint,
        status is ProductionPhysicalCustodyDrainStatus.Deferred
            or ProductionPhysicalCustodyDrainStatus.Conflict
            ? "qa-physical-participant-result"
            : string.Empty);

    private static string Digest(char value) => new(value, 64);

    private static bool IsDigest(string value) =>
        ProductionFacilityDestructiveDrainCanonical.IsFingerprint(value);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        private Fixture(
            ProductionPhysicalCustodyDestructiveDrainParticipant participant,
            FakePhysicalPort port)
        {
            Participant = participant;
            Port = port;
        }

        internal ProductionPhysicalCustodyDestructiveDrainParticipant
            Participant { get; }
        internal FakePhysicalPort Port { get; }

        internal static Fixture Create(bool hasAuthority = true)
        {
            ProductionFacilityHandle handle = new(
                new object(),
                FacilityId,
                new Vector2Int(3, 4),
                false,
                string.Empty,
                false,
                Vector2Int.zero,
                "building-definition:qa-physical-participant",
                "workstation:qa-physical-participant",
                2);
            IProductionAssemblyBridge bridge = new FakeBridge(handle);
            FakePhysicalPort port = new();
            return new Fixture(
                new ProductionPhysicalCustodyDestructiveDrainParticipant(
                    new FakeLifecycleQuery(hasAuthority),
                    port,
                    bridge),
                port);
        }
    }

    private sealed class FakeLifecycleQuery :
        IProductionOutputDestinationLifecycleQuery
    {
        private readonly bool hasAuthority;

        internal FakeLifecycleQuery(bool hasAuthority) =>
            this.hasAuthority = hasAuthority;

        public ProductionOutputDestinationLifecycleSnapshot Capture(
            BuildingInstanceId facilityId)
        {
            ProductionOutputDestinationLifecycleContribution contribution = new(
                ProductionFacilityDestructiveDrainParticipantIds
                    .PhysicalCustodyCarryRecovery,
                hasAuthority,
                1L,
                hasAuthority ? 1 : 0,
                hasAuthority ? 1000L : 0L,
                Array.Empty<ProductionOutputLifecycleBlock>(),
                ContributionFingerprint,
                ContributionFingerprint);
            return new ProductionOutputDestinationLifecycleSnapshot(
                facilityId,
                ProductionOutputDestinationId.FromFacility(facilityId),
                new[] { contribution },
                Digest('e'),
                Digest('e'));
        }
    }

    private sealed class FakeBridge : IProductionAssemblyBridge
    {
        private readonly ProductionFacilityHandle handle;

        internal FakeBridge(ProductionFacilityHandle handle) =>
            this.handle = handle;

        public IReadOnlyList<ProductionFacilityHandle> Facilities =>
            new[] { handle };

        public ProductionFacilityHandle CaptureFacility(object runtimeObject)
        {
            if (ReferenceEquals(runtimeObject, handle.RuntimeObject))
                return handle;
            throw new InvalidOperationException("Unknown fake facility object.");
        }

        public ProductionWorkerHandle CaptureWorker(object runtimeObject) =>
            throw Unsupported();
        public int CountDelivered(string itemId, string destinationId) =>
            throw Unsupported();
        public int CountPending(string itemId, string destinationId) =>
            throw Unsupported();
        public int CountAvailableStock(
            string itemId,
            string excludedDestinationId) => throw Unsupported();
        public int CountBufferedOutput(string itemId) => throw Unsupported();
        public int CountBufferedOutput(
            string itemId,
            string destinationId) => throw Unsupported();
        public bool RequestDelivery(
            string itemId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int requested,
            out string failureReason) => throw Unsupported();
        public bool ConsumeDeliveredToWip(
            string destinationId,
            IReadOnlyDictionary<string, int> costs,
            string operationId,
            out ProductionWipInputReceipt receipt,
            out string failureReason) => throw Unsupported();
        public bool AcknowledgeWipInput(
            string commitId,
            out string failureReason) => throw Unsupported();
        public bool CommitStockSensorInstallPending(
            string destinationId,
            string itemId,
            string operationId,
            string reasonCode,
            out ProductionStockSensorPhysicalReceipt receipt,
            out string failureReason) => throw Unsupported();
        public bool TryGetPendingStockSensorInstall(
            string operationId,
            out ProductionStockSensorPhysicalReceipt receipt) =>
            throw Unsupported();
        public bool AcknowledgeStockSensorInstall(
            string commitId,
            out string failureReason) => throw Unsupported();
        public bool SpawnOutput(
            string itemId,
            int amount,
            Vector2Int position) => throw Unsupported();
        public bool SpawnBufferedOutput(
            string itemId,
            int amount,
            Vector2Int position,
            string destinationId) => throw Unsupported();
        public bool TryCommitBufferedOutput(
            string commitId,
            string itemId,
            int amount,
            Vector2Int position,
            string destinationId,
            out DomainFailure failure) => throw Unsupported();
        public bool AcknowledgeBufferedOutput(
            string commitId,
            out DomainFailure failure) => throw Unsupported();
        public bool TryRouteBufferedOutput(
            string sourceDestinationId,
            string itemId,
            int amount,
            Vector2Int destinationPosition,
            string destinationId,
            out int routed,
            out DomainFailure failure) => throw Unsupported();
        public void PrioritizeDestination(string destinationId) =>
            throw Unsupported();
        public int ReleaseDestination(
            string destinationId,
            Vector2Int releasePosition) => throw Unsupported();
        public bool TryReleaseDestinationAtomically(
            string destinationId,
            Vector2Int releasePosition,
            out int released,
            out string failureReason) => throw Unsupported();
        public int RemoveDestination(string destinationId) =>
            throw Unsupported();
        public string GetOldestAvailableStackId(
            string itemId,
            string excludedDestinationId) => throw Unsupported();
        public ProductionBillRecord FindRunnableBill(
            IReadOnlyList<ProductionBillRecord> bills,
            ProductionFacilityHandle facility,
            WorkTypeId workTypeId,
            bool requireDeliveredInputs,
            out DomainFailure failure) => throw Unsupported();
        public bool HasDeliveredInputs(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            ProductionFacilityHandle facility,
            out DomainFailure failure) => throw Unsupported();
        public void RequestMissingInputs(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            ProductionFacilityHandle facility) => throw Unsupported();
        public long ResolveInputBufferMassCapacity(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            ProductionFacilityHandle facility) => throw Unsupported();
        public void RecalculatePrefetch(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            ProductionWorkerHandle worker) => throw Unsupported();
        public bool ShouldRunAnotherCycle(
            ProductionBillRecord record,
            ProductionRecipeSO recipe) => throw Unsupported();
        public bool IsResearchUnlocked(
            ProductionRecipeSO recipe,
            out DomainFailure failure) => throw Unsupported();
        public Dictionary<string, int> ToCycleInputMap(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            ProductionFacilityHandle facility) => throw Unsupported();
        public bool ValidateCycleRequirements(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            ProductionFacilityHandle facility,
            IReadOnlyList<ProductionBillRecord> allBills,
            out string failureReason) => throw Unsupported();
        public bool ValidateProcessingUtilities(
            string occupiedSupportNodeId,
            ProductionRecipeSO recipe,
            ProductionFacilityHandle facility,
            out string failureReason) => throw Unsupported();
        public bool TryConsumeCycleUtilities(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            ProductionFacilityHandle facility,
            out ProductionProcessFluidReceipt receipt,
            out string failureReason) => throw Unsupported();
        public bool AcknowledgeCycleUtilities(
            ProductionProcessFluidReceipt receipt,
            out string failureReason) => throw Unsupported();
        public bool TryResolveBatchSupport(
            ProductionBillRecord record,
            ProductionRecipeSO recipe,
            ProductionFacilityHandle facility,
            IReadOnlyList<ProductionBillRecord> allBills,
            out string supportNodeId,
            out string failureReason) => throw Unsupported();
        public float ResolveTemperatureSpeed(
            ProductionRecipeSO recipe,
            ProductionFacilityHandle facility,
            out bool dangerous) => throw Unsupported();
        public ProductionFacilityHandle ResolveOccupiedBatchSupport(
            string occupiedSupportNodeId,
            ProductionFacilityHandle facility) => throw Unsupported();
        public int ResolveOutputCapacity(
            ProductionFacilityHandle facility,
            string itemId,
            int outputPerBatch,
            int stackLimit) => throw Unsupported();
        public float ResolveSupportModifier(
            ProductionFacilityHandle facility,
            ProductionRecipeSO recipe,
            ProductionSupportModifierKind kind,
            float defaultValue,
            bool multiply) => throw Unsupported();
        public bool TryHandleOutput(
            ProductionRecipeSO recipe,
            ProductionFacilityHandle facility,
            ProductionWorkerHandle worker,
            string itemId,
            int amount,
            float qualityModifier,
            float workerQuality,
            string commitId,
            out bool handled,
            out DomainFailure failure) => throw Unsupported();
        public bool AcknowledgeHandledOutput(
            string itemId,
            string commitId,
            out DomainFailure failure) => throw Unsupported();
        public bool TryGetCommittedOutputMassGrams(
            string itemId,
            string commitId,
            out long massGrams,
            out DomainFailure failure) => throw Unsupported();
        public bool MatchesWorkstation(
            ProductionFacilityHandle facility,
            ProductionRecipeSO recipe) => throw Unsupported();
        public bool HasRequiredSupports(
            ProductionFacilityHandle facility,
            IReadOnlyList<string> requiredFeatureTags,
            out string failureReason) => throw Unsupported();
        public bool HasCompatibleWarehouse(
            string itemId,
            StockCategory category) => throw Unsupported();
        public void RequestWorkReplan(WorkTypeId workTypeId) =>
            throw Unsupported();
        public void RequestOneHaulerToReplan(bool forceInterrupt) =>
            throw Unsupported();

        private static NotSupportedException Unsupported() => new(
            "The physical participant fixture used an unrelated production bridge member.");
    }

    private sealed class FakePhysicalPort : IProductionPhysicalCustodyDrainPort
    {
        internal string StackId { get; set; } = "stack:qa-physical-a";
        internal long InputMassGrams { get; set; } = 1000L;
        internal int CaptureCalls { get; private set; }
        internal int PrepareCalls { get; private set; }
        internal ProductionPhysicalCustodyDrainSaveData State { get; set; }
        internal ProductionPhysicalCustodyDrainResult NextCommit { get; set; }
            = Result(ProductionPhysicalCustodyDrainStatus.Applied);
        internal ProductionPhysicalCustodyDrainResult NextAcknowledge
            { get; set; } = Result(
                ProductionPhysicalCustodyDrainStatus.Replay,
                CommitId,
                ReceiptFingerprint);

        public bool TryCaptureRequest(
            string stepOperationId,
            string ownerStableId,
            string sourceDestinationId,
            int ownerGridX,
            int ownerGridY,
            string expectedSourceOwnershipFingerprint,
            out ProductionPhysicalCustodyDrainRequest request,
            out string failureReason)
        {
            CaptureCalls++;
            failureReason = string.Empty;
            string[] stacks = { StackId };
            string[] actors = { "character:qa-physical" };
            string[] intents = { "haul:qa-physical" };
            string fingerprint =
                ProductionPhysicalCustodyDrainFingerprint.CreateRequest(
                    stepOperationId,
                    ownerStableId,
                    sourceDestinationId,
                    ownerGridX,
                    ownerGridY,
                    expectedSourceOwnershipFingerprint,
                    stacks,
                    actors,
                    intents,
                    2,
                    InputMassGrams);
            request = new ProductionPhysicalCustodyDrainRequest(
                stepOperationId,
                ownerStableId,
                sourceDestinationId,
                ownerGridX,
                ownerGridY,
                fingerprint,
                expectedSourceOwnershipFingerprint,
                stacks,
                actors,
                intents,
                2,
                InputMassGrams);
            return true;
        }

        public ProductionPhysicalCustodyDrainResult TryPrepare(
            ProductionPhysicalCustodyDrainRequest request)
        {
            PrepareCalls++;
            bool replay = State != null;
            if (!replay)
                State = FromRequest(request);
            return Result(replay
                ? ProductionPhysicalCustodyDrainStatus.Replay
                : ProductionPhysicalCustodyDrainStatus.Applied);
        }

        public ProductionPhysicalCustodyDrainResult TryCommit(
            string stepOperationId,
            string requestFingerprint)
        {
            if (!string.Equals(State?.stepOperationId, stepOperationId,
                    StringComparison.Ordinal)
                || !string.Equals(State?.requestFingerprint,
                    requestFingerprint, StringComparison.Ordinal))
            {
                return Result(ProductionPhysicalCustodyDrainStatus.Conflict);
            }
            if (IsDigest(NextCommit.ReceiptFingerprint)
                && !string.IsNullOrEmpty(NextCommit.CommitId))
            {
                MakeTerminal(
                    ProductionPhysicalCustodyDrainPhase
                        .EffectCommittedAwaitingOwnerAck,
                    NextCommit.CommitId,
                    NextCommit.ReceiptFingerprint);
            }
            return NextCommit;
        }

        public ProductionPhysicalCustodyDrainResult TryAcknowledge(
            string stepOperationId,
            string receiptFingerprint)
        {
            if (State == null
                || !string.Equals(State.stepOperationId, stepOperationId,
                    StringComparison.Ordinal)
                || !string.Equals(State.receiptFingerprint,
                    receiptFingerprint, StringComparison.Ordinal))
            {
                return Result(ProductionPhysicalCustodyDrainStatus.Conflict);
            }
            if (NextAcknowledge.Status is
                    ProductionPhysicalCustodyDrainStatus.Applied
                or ProductionPhysicalCustodyDrainStatus.Replay
                && IsDigest(NextAcknowledge.ReceiptFingerprint))
            {
                State.phase = ProductionPhysicalCustodyDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc;
            }
            return NextAcknowledge;
        }

        public ProductionPhysicalCustodyDrainResult TryGarbageCollect(
            string stepOperationId,
            string receiptFingerprint) => throw new NotSupportedException();

        public bool TryCapture(
            string stepOperationId,
            out ProductionPhysicalCustodyDrainSaveData record)
        {
            record = State?.Clone();
            return record != null
                && string.Equals(record.stepOperationId, stepOperationId,
                    StringComparison.Ordinal);
        }

        internal void MakeTerminal(
            ProductionPhysicalCustodyDrainPhase phase,
            string commitId,
            string receiptFingerprint)
        {
            State.phase = phase;
            State.releasedStackIds = new List<string>(State.sourceStackIds);
            State.completedActorIds = new List<string>(State.sourceActorIds);
            State.releasedHaulIntentOperationIds = new List<string>(
                State.sourceHaulIntentOperationIds);
            State.releasedQuantity = State.inputQuantity;
            State.releasedMassGrams = State.inputMassGrams;
            State.resultFingerprint = ResultFingerprint;
            State.commitId = commitId;
            State.receiptFingerprint = receiptFingerprint;
        }

        private static ProductionPhysicalCustodyDrainSaveData FromRequest(
            ProductionPhysicalCustodyDrainRequest request) => new()
        {
            stepOperationId = request.StepOperationId,
            ownerStableId = request.OwnerStableId,
            sourceDestinationId = request.SourceDestinationId,
            ownerGridX = request.OwnerGridX,
            ownerGridY = request.OwnerGridY,
            requestFingerprint = request.RequestFingerprint,
            sourceOwnershipFingerprint = request.SourceOwnershipFingerprint,
            phase = ProductionPhysicalCustodyDrainPhase.Prepared,
            sourceStackIds = request.SourceStackIds.ToList(),
            sourceActorIds = request.SourceActorIds.ToList(),
            sourceHaulIntentOperationIds =
                request.SourceHaulIntentOperationIds.ToList(),
            inputQuantity = request.InputQuantity,
            inputMassGrams = request.InputMassGrams
        };
    }

    private static bool DependsOn(
        this ProductionFacilityDestructiveDrainParticipantPlan left,
        ProductionFacilityDestructiveDrainParticipantPlan right) =>
        string.Equals(left.ParticipantId, right.ParticipantId,
            StringComparison.Ordinal)
        && left.ContractVersion == right.ContractVersion;
}
