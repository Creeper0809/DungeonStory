using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class SurgeryMaterialTerminalCustodyDebugScenarios
{
    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Surgery Terminal Custody")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("[SurgeryMaterialTerminalCustodyDebugScenarios] PASS");
    }

    public static void RunAll()
    {
        VerifyOwnerNeutralFacadeProjectionAndFailClosedDefaults();
        VerifyEmptyDestinationDeferredRetryAndClosureReadiness();
        VerifyUpperChildRestoreJoinAndTamperRejection();
        VerifyBidirectionalOrphanRejection();
        VerifyRawSaveCrossAggregatePreflight();
    }

    private static void VerifyOwnerNeutralFacadeProjectionAndFailClosedDefaults()
    {
        ProductionInputDestinationCustodyDrainSaveData raw = CreateRawChild(
            ProductionInputDestinationCustodyDrainPhase.ReleasingActors,
            inputQuantity: 3,
            inputMassGrams: 750L);
        Require(
            ProductionInputDestinationCustodyDrainContract.IsValidSave(raw),
            "The owner-neutral facade fixture is not a valid Items child row.");
        FakeProductionInputDrainService inner = new(raw);
        FacilityBufferDestinationCustodyDrainService facade = new(inner, inner);

        Require(
            facade.RequiresImmediateRecoveryBeforeGameplayTick,
            "Owner-neutral facade lost the immediate-recovery contract.");
        Require(
            facade.TryCapture(
                raw.stepOperationId,
                out FacilityBufferDestinationCustodyDrainSnapshot projected)
            && projected != null
            && projected.Phase ==
                FacilityBufferDestinationCustodyDrainPhase.ReleasingActors
            && projected.ParentOperationId == raw.parentOperationId
            && projected.StepOperationId == raw.stepOperationId
            && projected.OwnerStableId == raw.ownerStableId
            && projected.OwnerSubjectId == raw.billId
            && projected.OwnerFacilityId == raw.facilityId
            && projected.SourceDestinationId == raw.sourceDestinationId
            && projected.SourceAuthorityFingerprint ==
                raw.sourceClaimFingerprint
            && projected.RequestFingerprint == raw.requestFingerprint
            && projected.OwnerGridX == raw.ownerGridX
            && projected.OwnerGridY == raw.ownerGridY
            && projected.InputQuantity == raw.inputQuantity
            && projected.InputMassGrams == raw.inputMassGrams,
            "Owner-neutral facade did not project the Items-owned row exactly.");

        FacilityBufferDestinationCustodyDrainResult missingDescriptor =
            facade.TryPrepare(null);
        Require(
            missingDescriptor.Status ==
                FacilityBufferDestinationCustodyDrainStatus.Conflict
            && missingDescriptor.Snapshot == null,
            "A missing facade descriptor did not fail closed.");

        inner.NextResultStatus =
            (ProductionInputDestinationCustodyDrainStatus)999;
        FacilityBufferDestinationCustodyDrainResult unknownStatus =
            facade.TryAdvance(raw.stepOperationId, raw.requestFingerprint);
        Require(
            unknownStatus.Status ==
                FacilityBufferDestinationCustodyDrainStatus.Conflict
            && !unknownStatus.Succeeded,
            "An unknown inner result status did not map to Conflict.");

        ProductionInputDestinationCustodyDrainPhase savedPhase = raw.phase;
        raw.phase = (ProductionInputDestinationCustodyDrainPhase)999;
        ExpectThrows(
            () => facade.TryCapture(raw.stepOperationId, out _),
            "An unknown persisted child phase was projected instead of rejected.");
        raw.phase = savedPhase;
    }

    private static void VerifyEmptyDestinationDeferredRetryAndClosureReadiness()
    {
        SurgeryOrder order = CreateOrder(withTerminalJoin: false);
        Vector2Int ownerPosition = new(7, 11);
        FacilityBufferDestinationClaimRegistry claims = new();
        Require(
            claims.TryClaim(
                new FacilityBufferDestinationClaim(
                    order.materialDestinationId,
                    ownerPosition,
                    SurgeryMaterialDestinationAuthority.OwnerDomain,
                    order.orderId,
                    order.facilityId,
                    FacilityBufferDestinationAnchorKind.LiveFacility,
                    FacilityBufferDestinationAdmissionPolicy.ExactGramRequired),
                out FacilityBufferDestinationClaimFailureCode claimFailure,
                out string claimDetail),
            "Could not publish the terminal fixture claim: "
            + claimFailure + ":" + claimDetail);

        FakeFacilityBufferDrainService drains = new()
        {
            DeferredAdvanceCount = 1
        };
        FakeSurgeryMaterialDestinationRuntime materialDestinations = new();
        SurgeryMaterialTerminalRuntime runtime = new(
            drains,
            claims,
            materialDestinations);

        SurgeryMaterialTerminalAdvanceResult first = runtime.TryBeginOrResume(
            order,
            SurgeryOrderState.Cancelled);
        Require(
            first.Status == SurgeryMaterialTerminalAdvanceStatus.Deferred
            && order.state == SurgeryOrderState.TerminalDraining
            && order.materialTerminalDrainPhase ==
                SurgeryMaterialTerminalDrainPhase.Prepared
            && order.materialTerminalInputQuantity == 0
            && order.materialTerminalInputMassGrams == 0L
            && !materialDestinations.Revoked,
            "An empty destination did not preserve its prepared join while deferred.");

        SurgeryMaterialTerminalAdvanceResult retried = runtime.TryBeginOrResume(
            order,
            SurgeryOrderState.Cancelled);
        Require(
            retried.IsReadyForOwnerClosure
            && order.materialTerminalDrainPhase ==
                SurgeryMaterialTerminalDrainPhase
                    .OwnerAcknowledgedAwaitingClosure
            && materialDestinations.Revoked
            && drains.PrepareCount == 1
            && drains.AcknowledgeCount == 1,
            "Deferred empty-destination recovery did not converge exactly once.");

        Require(
            order.materialTerminalTargetState == SurgeryOrderState.Cancelled
            && order.state == SurgeryOrderState.TerminalDraining,
            "The ready terminal join did not retain the owner closure target and boundary.");
    }

    private static void VerifyUpperChildRestoreJoinAndTamperRejection()
    {
        SurgeryOrder prepared = CreateOrder(withTerminalJoin: true);
        FacilityBufferDestinationCustodyDrainSnapshot preparedChild =
            CreateSnapshot(prepared,
                FacilityBufferDestinationCustodyDrainPhase.Prepared);
        ValidateJoin(prepared, preparedChild);

        SurgeryOrder committed = SurgeryStateCloner.CloneOrder(prepared);
        committed.materialTerminalDrainPhase =
            SurgeryMaterialTerminalDrainPhase.EffectCommittedAwaitingAck;
        committed.materialTerminalCommitId = "commit:surgery-terminal";
        committed.materialTerminalReceiptFingerprint = Digest("receipt");
        FacilityBufferDestinationCustodyDrainSnapshot committedChild =
            CreateSnapshot(
                committed,
                FacilityBufferDestinationCustodyDrainPhase
                    .EffectCommittedAwaitingOwnerAck);
        ValidateJoin(committed, committedChild);

        SurgeryOrder acknowledged = SurgeryStateCloner.CloneOrder(committed);
        acknowledged.materialTerminalDrainPhase =
            SurgeryMaterialTerminalDrainPhase
                .OwnerAcknowledgedAwaitingClosure;
        FacilityBufferDestinationCustodyDrainSnapshot acknowledgedChild =
            CreateSnapshot(
                acknowledged,
                FacilityBufferDestinationCustodyDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc);
        ValidateJoin(acknowledged, acknowledgedChild);

        SurgeryOrder closed = SurgeryStateCloner.CloneOrder(acknowledged);
        closed.materialTerminalDrainPhase =
            SurgeryMaterialTerminalDrainPhase.ClosedAwaitingCheckpointGc;
        closed.state = closed.materialTerminalTargetState;
        ValidateJoin(closed, acknowledgedChild);

        foreach (SnapshotMutation mutation in Enum.GetValues(
                     typeof(SnapshotMutation)))
        {
            if (mutation == SnapshotMutation.None)
                continue;
            FacilityBufferDestinationCustodyDrainSnapshot tampered =
                CreateSnapshot(
                    committed,
                    FacilityBufferDestinationCustodyDrainPhase
                        .EffectCommittedAwaitingOwnerAck,
                    mutation);
            ExpectThrows(
                () => ValidateJoin(committed, tampered),
                "Restore accepted a tampered terminal child: " + mutation);
        }
    }

    private static void VerifyBidirectionalOrphanRejection()
    {
        SurgeryOrder owner = CreateOrder(withTerminalJoin: true);
        FacilityBufferDestinationCustodyDrainSnapshot child = CreateSnapshot(
            owner,
            FacilityBufferDestinationCustodyDrainPhase.Prepared);

        SurgeryAggregateState missingChild = new();
        missingChild.Orders.Add(SurgeryStateCloner.CloneOrder(owner));
        ExpectThrows(
            () => SurgeryRestoreCoordinator.ValidateMaterialTerminalCustodyJoin(
                missingChild,
                new FakeRestoreQuery(Array.Empty<
                    FacilityBufferDestinationCustodyDrainSnapshot>())),
            "A Surgery upper terminal join without its Items child was accepted.");

        ExpectThrows(
            () => SurgeryRestoreCoordinator.ValidateMaterialTerminalCustodyJoin(
                new SurgeryAggregateState(),
                new FakeRestoreQuery(new[] { child })),
            "An Items terminal child without its Surgery upper owner was accepted.");

        ExpectThrows(
            () => SurgeryRestoreCoordinator.ValidateMaterialTerminalCustodyJoin(
                missingChild,
                new FakeRestoreQuery(new[] { child }, available: false)),
            "Restore accepted an unavailable detached custody candidate.");
    }

    private static void VerifyRawSaveCrossAggregatePreflight()
    {
        SurgeryOrder owner = CreateOrder(withTerminalJoin: true);
        ProductionInputDestinationCustodyDrainSaveData child =
            CreateRawTerminalChild(owner);
        owner.materialTerminalRequestFingerprint = child.requestFingerprint;
        DungeonSurgerySaveData surgery = new()
        {
            orders = new List<SurgeryOrder>
            {
                SurgeryStateCloner.CloneOrder(owner)
            }
        };
        DungeonPhysicalItemSaveData physical = new()
        {
            pendingProductionInputDestinationDrains = new List<
                ProductionInputDestinationCustodyDrainSaveData>
            {
                child.Clone()
            }
        };

        DungeonSaveSectionEnvelope surgeryEnvelope = new()
        {
            sectionId = SurgerySaveSection.Id,
            sectionVersion = DungeonSurgerySaveData.CurrentVersion,
            payloadJson = JsonUtility.ToJson(surgery)
        };
        DungeonSaveSectionEnvelope physicalEnvelope = new()
        {
            sectionId = PhysicalItemsSaveSection.Id,
            sectionVersion = DungeonPhysicalItemSaveData.CurrentVersion,
            payloadJson = JsonUtility.ToJson(physical)
        };
        SurgeryMaterialTerminalCrossAggregateSaveValidation validator = new();
        DungeonGameRestoreReport wholeReport = new();
        validator.Validate(new DungeonGameSaveData
        {
            sections = new List<DungeonSaveSectionEnvelope>
            {
                surgeryEnvelope,
                physicalEnvelope
            }
        }, wholeReport);
        Require(wholeReport.Success,
            "Whole-save Surgery terminal preflight rejected an exact join.");

        DungeonGameRestoreReport registryReport = new();
        validator.Validate(new Dictionary<string, DungeonSaveSectionEnvelope>(
        StringComparer.Ordinal)
        {
            [SurgerySaveSection.Id] = surgeryEnvelope,
            [PhysicalItemsSaveSection.Id] = physicalEnvelope
        }, registryReport);
        Require(registryReport.Success,
            "Registry Surgery terminal preflight rejected an exact join.");

        physical.pendingProductionInputDestinationDrains[0]
            .ownerGridX++;
        DungeonGameRestoreReport tamperedReport = new();
        validator.Validate(new DungeonGameSaveData
        {
            sections = new List<DungeonSaveSectionEnvelope>
            {
                surgeryEnvelope,
                new DungeonSaveSectionEnvelope
                {
                    sectionId = PhysicalItemsSaveSection.Id,
                    sectionVersion = DungeonPhysicalItemSaveData.CurrentVersion,
                    payloadJson = JsonUtility.ToJson(physical)
                }
            }
        }, tamperedReport);
        Require(!tamperedReport.Success,
            "Whole-save Surgery terminal preflight accepted a tampered raw child.");
    }

    private static void ValidateJoin(
        SurgeryOrder order,
        FacilityBufferDestinationCustodyDrainSnapshot child)
    {
        SurgeryAggregateState state = new();
        state.Orders.Add(SurgeryStateCloner.CloneOrder(order));
        SurgeryRestoreCoordinator.ValidateMaterialTerminalCustodyJoin(
            state,
            new FakeRestoreQuery(new[] { child }));
    }

    private static SurgeryOrder CreateOrder(bool withTerminalJoin)
    {
        SurgeryOrder order = new()
        {
            orderId = "surgery:terminal-custody-fixture",
            procedureId = "procedure:fixture",
            facilityId = "facility:surgery-fixture",
            materialDestinationId =
                "surgery-materials:surgery:terminal-custody-fixture",
            materialBufferCapacityGrams = 1_000L,
            materialMassAuthorityRevision = 1L,
            state = SurgeryOrderState.Recovering,
            subject = new SurgicalSubjectRef
            {
                kind = SurgicalSubjectKind.Character,
                subjectId = "character:patient"
            }
        };
        order.materialCapacityFingerprint = Digest("capacity");
        if (!withTerminalJoin)
            return order;

        order.state = SurgeryOrderState.TerminalDraining;
        order.materialTerminalDrainPhase =
            SurgeryMaterialTerminalDrainPhase.Prepared;
        order.materialTerminalTargetState = SurgeryOrderState.Cancelled;
        order.materialTerminalParentOperationId =
            SurgeryMaterialTerminalIdentity.FormatParentOperationId(
                order.orderId);
        order.materialTerminalStepOperationId =
            SurgeryMaterialTerminalIdentity.FormatStepOperationId(order.orderId);
        order.materialTerminalRequestFingerprint = Digest("request");
        order.materialTerminalInputQuantity = 2;
        order.materialTerminalInputMassGrams = 500L;
        order.materialTerminalOwnerX = 7;
        order.materialTerminalOwnerY = 11;
        return order;
    }

    private static FacilityBufferDestinationCustodyDrainSnapshot CreateSnapshot(
        SurgeryOrder order,
        FacilityBufferDestinationCustodyDrainPhase phase,
        SnapshotMutation mutation = SnapshotMutation.None)
    {
        bool committed = phase is
            FacilityBufferDestinationCustodyDrainPhase
                .EffectCommittedAwaitingOwnerAck
            or FacilityBufferDestinationCustodyDrainPhase
                .OwnerAcknowledgedAwaitingCheckpointGc;
        string parent = order.materialTerminalParentOperationId;
        string step = order.materialTerminalStepOperationId;
        string owner = SurgeryMaterialTerminalIdentity.FormatOwnerStableId(
            order.orderId);
        string subject = order.orderId;
        string facility = order.facilityId;
        string destination = order.materialDestinationId;
        string authority = order.materialCapacityFingerprint;
        string request = order.materialTerminalRequestFingerprint;
        int ownerX = order.materialTerminalOwnerX;
        int ownerY = order.materialTerminalOwnerY;
        int quantity = order.materialTerminalInputQuantity;
        long mass = order.materialTerminalInputMassGrams;
        string commit = committed ? order.materialTerminalCommitId : string.Empty;
        string receipt = committed
            ? order.materialTerminalReceiptFingerprint
            : string.Empty;

        switch (mutation)
        {
            case SnapshotMutation.Parent:
                parent += ":tampered";
                break;
            case SnapshotMutation.Step:
                step += ":tampered";
                break;
            case SnapshotMutation.Owner:
                owner += ":tampered";
                break;
            case SnapshotMutation.OwnerSubject:
                subject += ":tampered";
                break;
            case SnapshotMutation.Facility:
                facility += ":tampered";
                break;
            case SnapshotMutation.Destination:
                destination += ":tampered";
                break;
            case SnapshotMutation.SourceAuthority:
                authority = Digest("tampered-authority");
                break;
            case SnapshotMutation.Position:
                ownerX++;
                break;
            case SnapshotMutation.Request:
                request = Digest("tampered-request");
                break;
            case SnapshotMutation.Commit:
                commit += ":tampered";
                break;
            case SnapshotMutation.Receipt:
                receipt = Digest("tampered-receipt");
                break;
            case SnapshotMutation.InputQuantity:
                quantity++;
                break;
            case SnapshotMutation.InputMass:
                mass++;
                break;
        }

        return new FacilityBufferDestinationCustodyDrainSnapshot(
            parent,
            step,
            owner,
            subject,
            facility,
            destination,
            authority,
            request,
            ownerX,
            ownerY,
            phase,
            0,
            0,
            0,
            0,
            quantity,
            mass,
            committed ? quantity : 0,
            committed ? mass : 0L,
            commit,
            receipt);
    }

    private static ProductionInputDestinationCustodyDrainSaveData CreateRawChild(
        ProductionInputDestinationCustodyDrainPhase phase,
        int inputQuantity,
        long inputMassGrams)
    {
        ProductionInputDestinationDrainStackSaveData stack = new()
        {
            stackId = "stack:owner-neutral",
            itemId = "item:owner-neutral",
            componentFingerprint = Digest("components"),
            quantity = inputQuantity,
            massGrams = inputMassGrams,
            state = WorldItemStackState.FacilityBuffer,
            positionX = 3,
            positionY = 5,
            destinationPositionX = 3,
            destinationPositionY = 5
        };
        ProductionInputDestinationCustodyDrainSaveData value = new()
        {
            parentOperationId = "owner-neutral:parent",
            stepOperationId = "owner-neutral:parent:custody",
            ownerStableId = "owner-neutral:owner",
            billId = "owner-neutral:subject",
            facilityId = "facility:owner-neutral",
            sourceDestinationId = "destination:owner-neutral",
            ownerGridX = 3,
            ownerGridY = 5,
            sourceClaimFingerprint = Digest("authority"),
            sourceOwnershipFingerprint = Digest("ownership"),
            phase = phase,
            sourceStacks = new List<
                ProductionInputDestinationDrainStackSaveData> { stack },
            inputQuantity = inputQuantity,
            inputMassGrams = inputMassGrams
        };
        value.requestFingerprint =
            ProductionInputDestinationCustodyDrainFingerprint.CreateRequest(
                value.parentOperationId,
                value.stepOperationId,
                value.ownerStableId,
                value.billId,
                value.facilityId,
                value.sourceDestinationId,
                value.ownerGridX,
                value.ownerGridY,
                value.sourceClaimFingerprint,
                value.sourceOwnershipFingerprint,
                value.sourceStacks,
                value.sourceOperations,
                value.sourceActors,
                value.inputQuantity,
                value.inputMassGrams);
        return value;
    }

    private static ProductionInputDestinationCustodyDrainSaveData
        CreateRawTerminalChild(SurgeryOrder order)
    {
        ProductionInputDestinationCustodyDrainSaveData value = CreateRawChild(
            ProductionInputDestinationCustodyDrainPhase.ReleasingActors,
            order.materialTerminalInputQuantity,
            order.materialTerminalInputMassGrams);
        value.parentOperationId = order.materialTerminalParentOperationId;
        value.stepOperationId = order.materialTerminalStepOperationId;
        value.ownerStableId = SurgeryMaterialTerminalIdentity
            .FormatOwnerStableId(order.orderId);
        value.billId = order.orderId;
        value.facilityId = order.facilityId;
        value.sourceDestinationId = order.materialDestinationId;
        value.ownerGridX = order.materialTerminalOwnerX;
        value.ownerGridY = order.materialTerminalOwnerY;
        value.sourceClaimFingerprint = order.materialCapacityFingerprint;
        value.sourceStacks[0].positionX = value.ownerGridX;
        value.sourceStacks[0].positionY = value.ownerGridY;
        value.sourceStacks[0].destinationPositionX = value.ownerGridX;
        value.sourceStacks[0].destinationPositionY = value.ownerGridY;
        value.requestFingerprint =
            ProductionInputDestinationCustodyDrainFingerprint.CreateRequest(
                value.parentOperationId,
                value.stepOperationId,
                value.ownerStableId,
                value.billId,
                value.facilityId,
                value.sourceDestinationId,
                value.ownerGridX,
                value.ownerGridY,
                value.sourceClaimFingerprint,
                value.sourceOwnershipFingerprint,
                value.sourceStacks,
                value.sourceOperations,
                value.sourceActors,
                value.inputQuantity,
                value.inputMassGrams);
        return value;
    }

    private static string Digest(string value) =>
        ProductionInputDestinationCustodyDrainFingerprint.Hash(value);

    private static void ExpectThrows(Action action, string message)
    {
        bool threw = false;
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            threw = true;
        }
        Require(threw, message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private enum SnapshotMutation
    {
        None = 0,
        Parent = 1,
        Step = 2,
        Owner = 3,
        OwnerSubject = 4,
        Facility = 5,
        Destination = 6,
        SourceAuthority = 7,
        Position = 8,
        Request = 9,
        Commit = 10,
        Receipt = 11,
        InputQuantity = 12,
        InputMass = 13
    }

    private sealed class FakeRestoreQuery :
        IFacilityBufferDestinationCustodyDrainRestoreCandidateQuery
    {
        private readonly Dictionary<string,
            FacilityBufferDestinationCustodyDrainSnapshot> byStep;

        internal FakeRestoreQuery(
            IEnumerable<FacilityBufferDestinationCustodyDrainSnapshot> values,
            bool available = true)
        {
            IsCandidateAvailable = available;
            Drains = Array.AsReadOnly((values ?? Array.Empty<
                    FacilityBufferDestinationCustodyDrainSnapshot>())
                .ToArray());
            byStep = Drains.Where(value => value != null)
                .ToDictionary(value => value.StepOperationId,
                    StringComparer.Ordinal);
        }

        public bool IsCandidateAvailable { get; }
        public IReadOnlyList<FacilityBufferDestinationCustodyDrainSnapshot>
            Drains { get; }

        public bool TryGetDrain(
            string stepOperationId,
            out FacilityBufferDestinationCustodyDrainSnapshot snapshot) =>
            byStep.TryGetValue(stepOperationId ?? string.Empty, out snapshot);
    }

    private sealed class FakeProductionInputDrainService :
        IProductionInputDestinationCustodyDrainService,
        IProductionInputDestinationCustodyDrainRestoreCandidateQuery
    {
        internal FakeProductionInputDrainService(
            ProductionInputDestinationCustodyDrainSaveData current)
        {
            Current = current;
        }

        internal ProductionInputDestinationCustodyDrainSaveData Current { get; }
        internal ProductionInputDestinationCustodyDrainStatus NextResultStatus
        { get; set; } = ProductionInputDestinationCustodyDrainStatus.Applied;

        public bool RequiresImmediateRecoveryBeforeGameplayTick => true;
        public bool IsCandidateAvailable => true;
        public IReadOnlyList<ProductionInputDestinationCustodyDrainSaveData>
            Drains => Current == null
            ? Array.Empty<ProductionInputDestinationCustodyDrainSaveData>()
            : new[] { Current };

        public bool TryGetDrain(
            string stepOperationId,
            out ProductionInputDestinationCustodyDrainSaveData drain) =>
            TryCapture(stepOperationId, out drain);

        public bool TryCaptureSource(
            string sourceDestinationId,
            out ProductionInputDestinationCustodySourceSnapshot snapshot,
            out string failureReason)
        {
            snapshot = null;
            failureReason = "fixture-capture-source-not-configured";
            return false;
        }

        public bool TryBuildRequest(
            string parentOperationId,
            string stepOperationId,
            string ownerStableId,
            string billId,
            string facilityId,
            Vector2Int ownerPosition,
            string sourceClaimFingerprint,
            ProductionInputDestinationCustodySourceSnapshot snapshot,
            out ProductionInputDestinationCustodyDrainRequest request,
            out string failureReason)
        {
            request = null;
            failureReason = "fixture-build-request-not-configured";
            return false;
        }

        public bool TryCaptureRequest(
            string parentOperationId,
            string stepOperationId,
            string ownerStableId,
            string billId,
            string facilityId,
            string sourceDestinationId,
            Vector2Int ownerPosition,
            string sourceClaimFingerprint,
            out ProductionInputDestinationCustodyDrainRequest request,
            out string failureReason)
        {
            request = null;
            failureReason = "fixture-capture-request-not-configured";
            return false;
        }

        public ProductionInputDestinationCustodyDrainResult TryPrepare(
            ProductionInputDestinationCustodyDrainRequest request) => Result();

        public ProductionInputDestinationCustodyDrainResult TryCommit(
            string stepOperationId,
            string requestFingerprint) => Result();

        public ProductionInputDestinationCustodyDrainResult TryAcknowledge(
            string stepOperationId,
            string receiptFingerprint) => Result();

        public ProductionInputDestinationCustodyDrainResult TryGarbageCollect(
            string stepOperationId,
            string receiptFingerprint) => Result();

        public bool TryCapture(
            string stepOperationId,
            out ProductionInputDestinationCustodyDrainSaveData record)
        {
            record = Current;
            return Current != null && string.Equals(
                Current.stepOperationId,
                stepOperationId,
                StringComparison.Ordinal);
        }

        private ProductionInputDestinationCustodyDrainResult Result() => new(
            NextResultStatus,
            Current?.commitId,
            Current?.receiptFingerprint,
            NextResultStatus == ProductionInputDestinationCustodyDrainStatus
                .Applied
                ? string.Empty
                : "fixture-result");
    }

    private sealed class FakeFacilityBufferDrainService :
        IFacilityBufferDestinationCustodyDrainService
    {
        private FacilityBufferDestinationCustodyDrainSnapshot current;

        internal int DeferredAdvanceCount { get; set; }
        internal int PrepareCount { get; private set; }
        internal int AcknowledgeCount { get; private set; }
        public bool RequiresImmediateRecoveryBeforeGameplayTick => true;

        public FacilityBufferDestinationCustodyDrainResult TryPrepare(
            FacilityBufferDestinationCustodyDrainDescriptor descriptor)
        {
            PrepareCount++;
            current = new FacilityBufferDestinationCustodyDrainSnapshot(
                descriptor.ParentOperationId,
                descriptor.StepOperationId,
                descriptor.OwnerStableId,
                descriptor.OwnerSubjectId,
                descriptor.OwnerFacilityId,
                descriptor.SourceDestinationId,
                descriptor.SourceAuthorityFingerprint,
                Digest("empty-request"),
                descriptor.OwnerPosition.x,
                descriptor.OwnerPosition.y,
                FacilityBufferDestinationCustodyDrainPhase.Prepared,
                0,
                0,
                0,
                0,
                0,
                0L,
                0,
                0L,
                string.Empty,
                string.Empty);
            return Applied();
        }

        public FacilityBufferDestinationCustodyDrainResult TryAdvance(
            string stepOperationId,
            string requestFingerprint)
        {
            if (!ExactRequest(stepOperationId, requestFingerprint))
                return Conflict("fixture-request-conflict");
            if (DeferredAdvanceCount-- > 0)
            {
                return new FacilityBufferDestinationCustodyDrainResult(
                    FacilityBufferDestinationCustodyDrainStatus.Deferred,
                    current,
                    "fixture-deferred");
            }

            string commit = "commit:empty-surgery-terminal";
            string receipt = Digest("empty-receipt");
            current = Copy(
                FacilityBufferDestinationCustodyDrainPhase
                    .EffectCommittedAwaitingOwnerAck,
                commit,
                receipt);
            return Applied();
        }

        public FacilityBufferDestinationCustodyDrainResult TryAcknowledge(
            string stepOperationId,
            string receiptFingerprint)
        {
            if (!string.Equals(
                    current?.StepOperationId,
                    stepOperationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    current?.ReceiptFingerprint,
                    receiptFingerprint,
                    StringComparison.Ordinal))
            {
                return Conflict("fixture-receipt-conflict");
            }
            AcknowledgeCount++;
            current = Copy(
                FacilityBufferDestinationCustodyDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc,
                current.CommitId,
                current.ReceiptFingerprint);
            return Applied();
        }

        public bool TryCapture(
            string stepOperationId,
            out FacilityBufferDestinationCustodyDrainSnapshot snapshot)
        {
            snapshot = current;
            return current != null && string.Equals(
                current.StepOperationId,
                stepOperationId,
                StringComparison.Ordinal);
        }

        private bool ExactRequest(string stepOperationId, string request) =>
            current != null
            && string.Equals(current.StepOperationId, stepOperationId,
                StringComparison.Ordinal)
            && string.Equals(current.RequestFingerprint, request,
                StringComparison.Ordinal);

        private FacilityBufferDestinationCustodyDrainSnapshot Copy(
            FacilityBufferDestinationCustodyDrainPhase phase,
            string commit,
            string receipt) => new(
            current.ParentOperationId,
            current.StepOperationId,
            current.OwnerStableId,
            current.OwnerSubjectId,
            current.OwnerFacilityId,
            current.SourceDestinationId,
            current.SourceAuthorityFingerprint,
            current.RequestFingerprint,
            current.OwnerGridX,
            current.OwnerGridY,
            phase,
            current.SourceActorCount,
            current.CompletedActorCount,
            current.SourceOperationCount,
            current.ReleasedOperationCount,
            current.InputQuantity,
            current.InputMassGrams,
            current.InputQuantity,
            current.InputMassGrams,
            commit,
            receipt);

        private FacilityBufferDestinationCustodyDrainResult Applied() => new(
            FacilityBufferDestinationCustodyDrainStatus.Applied,
            current,
            string.Empty);

        private FacilityBufferDestinationCustodyDrainResult Conflict(
            string reason) => new(
            FacilityBufferDestinationCustodyDrainStatus.Conflict,
            current,
            reason);
    }

    private sealed class FakeSurgeryMaterialDestinationRuntime :
        ISurgeryMaterialDestinationRuntime
    {
        internal bool Revoked { get; private set; }

        public bool TryClaim(
            SurgeryOrder order,
            BuildableObject facility,
            out string failureReason)
        {
            failureReason = "fixture-not-supported";
            return false;
        }

        public bool TryReplace(
            IReadOnlyList<SurgeryOrder> orders,
            IReadOnlyDictionary<string, Vector2Int> facilityPositions,
            out string failureReason)
        {
            failureReason = "fixture-not-supported";
            return false;
        }

        public bool TryRevoke(
            SurgeryOrder order,
            out string failureReason)
        {
            Revoked = true;
            failureReason = string.Empty;
            return true;
        }

        public bool TryValidate(
            SurgeryOrder order,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
    }
}
