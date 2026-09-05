#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class CharacterMedicalSupplyDestinationDrainRuntimeDebugScenarios
{
    private const string DigestA =
        "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string DigestB =
        "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [MenuItem(
        "DungeonStory/Debug/V27/Run Character Medical Supply Destination Drain Runtime Contracts")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log(
            "[V27][PASS] Character Medical destination drain phases, sequence lifetimes, Sink ordering, re-Downed reopening, drain-state preservation, and detached cross joins are exact.");
    }

    public static void RunAll()
    {
        VerifyExactPhaseProgression();
        VerifyActiveOneClosedManyReassignmentSequence();
        VerifyPendingSinkRecoveryPrecedesDrain();
        VerifyTerminalTargetReopensWhenPatientIsDownedAgain();
        VerifyReservationReleaseAndDownedNotificationPreserveDraining();
        VerifyDetachedCrossJoinRejectsTamperMissingDuplicateAndLowerOnly();
    }

    private static void VerifyExactPhaseProgression()
    {
        DrainFixture fixture = new();
        CharacterMedicalOrder order = fixture.CreateCurrentOrder(1);
        bool observedPrepared = false;
        bool observedCommitted = false;
        bool observedAcknowledged = false;
        fixture.Children.BeforeAdvance = () =>
        {
            CharacterMedicalSupplyDestinationDrainJoinData upper =
                RequireSingleActive(order);
            observedPrepared = upper.phase ==
                CharacterMedicalSupplyDestinationDrainPhase.Prepared;
        };
        fixture.Children.BeforeAcknowledge = () =>
        {
            CharacterMedicalSupplyDestinationDrainJoinData upper =
                RequireSingleActive(order);
            observedCommitted = upper.phase ==
                    CharacterMedicalSupplyDestinationDrainPhase
                        .EffectCommittedAwaitingOwnerAck
                && upper.commitId.Length > 0
                && upper.receiptFingerprint == DigestB;
        };
        fixture.Destinations.BeforeRevoke = () =>
        {
            CharacterMedicalSupplyDestinationDrainJoinData upper =
                RequireSingleActive(order);
            observedAcknowledged = upper.phase ==
                CharacterMedicalSupplyDestinationDrainPhase
                    .OwnerAcknowledgedAwaitingClosure;
        };

        CharacterMedicalSupplyDestinationDrainAdvanceResult result = fixture
            .Runtime.TryBeginOrResume(
                order,
                CharacterMedicalOrderState.AwaitingBed,
                CharacterMedicalStatusCode.AwaitingBed,
                Array.Empty<string>());
        CharacterMedicalSupplyDestinationDrainJoinData closed = order
            .treatmentDestinationDrainJoins.Single();

        Require(result.IsClosed
            && observedPrepared
            && observedCommitted
            && observedAcknowledged
            && closed.phase == CharacterMedicalSupplyDestinationDrainPhase
                .ClosedAwaitingCheckpointGc
            && order.state == CharacterMedicalOrderState.AwaitingBed
            && order.statusCode == CharacterMedicalStatusCode.AwaitingBed
            && order.treatmentDestinationSequence == 0
            && order.treatmentMaterialDestinationId.Length == 0
            && fixture.Destinations.RevokedSequences.SequenceEqual(new[] { 1 }),
            "Prepared -> committed -> acknowledged -> Closed phase progression was not exact.");
    }

    private static void VerifyActiveOneClosedManyReassignmentSequence()
    {
        DrainFixture fixture = new();
        CharacterMedicalOrder order = fixture.CreateCurrentOrder(1);
        Require(fixture.Runtime.TryBeginOrResume(
                    order,
                    CharacterMedicalOrderState.AwaitingBed,
                    CharacterMedicalStatusCode.AwaitingBed,
                    Array.Empty<string>()).IsClosed,
            "First destination lifetime did not close.");

        fixture.AssignNextLifetime(order, sequence: 2);
        fixture.Children.DeferAdvance = true;
        CharacterMedicalSupplyDestinationDrainAdvanceResult deferred = fixture
            .Runtime.TryBeginOrResume(
                order,
                CharacterMedicalOrderState.AwaitingBed,
                CharacterMedicalStatusCode.AwaitingBed,
                Array.Empty<string>());
        Require(deferred.Status ==
                CharacterMedicalSupplyDestinationDrainAdvanceStatus.Deferred
            && order.treatmentDestinationDrainJoins.Count == 2
            && order.treatmentDestinationDrainJoins.Count(value => value.phase !=
                CharacterMedicalSupplyDestinationDrainPhase
                    .ClosedAwaitingCheckpointGc) == 1
            && order.treatmentDestinationDrainJoins.Count(value => value.phase ==
                CharacterMedicalSupplyDestinationDrainPhase
                    .ClosedAwaitingCheckpointGc) == 1
            && order.state ==
                CharacterMedicalOrderState.MaterialDestinationDraining,
            "Reassignment did not retain closed-N with exactly one active lifetime.");

        fixture.Children.DeferAdvance = false;
        Require(fixture.Runtime.TryResume(order).IsClosed
            && order.treatmentDestinationDrainJoins.Select(value =>
                    value.destinationSequence).SequenceEqual(new[] { 1, 2 })
            && order.treatmentDestinationDrainJoins.All(value => value.phase ==
                CharacterMedicalSupplyDestinationDrainPhase
                    .ClosedAwaitingCheckpointGc)
            && fixture.Destinations.RevokedSequences.SequenceEqual(new[] { 1, 2 })
            && order.nextTreatmentMaterialDestinationSequence == 3,
            "Closed-N destination sequences were reused, reordered, or left active.");
    }

    private static void VerifyPendingSinkRecoveryPrecedesDrain()
    {
        List<string> events = new();
        const string operationId =
            "character-medical-supply:runtime-sink-ordering:00000001";
        const string reasonCode =
            CharacterMedicalSupplyCoordinator.DispositionReasonCode;
        PhysicalItemBatchDispositionReceipt receipt = new(
            PhysicalItemDispositionKind.Sink,
            operationId,
            reasonCode,
            DigestA,
            new[] { "stack:medical-sink-fixture" },
            1,
            140L);
        RecordingPhysicalSink sink = new(receipt, events);
        RecordingOwnerDrain ownerDrain = new(events, closeImmediately: true);
        CharacterMedicalOrder order = new()
        {
            orderId = "medical:runtime-sink-ordering",
            patientId = "character:runtime-sink-ordering",
            state = CharacterMedicalOrderState.AwaitingRescue,
            statusCode = CharacterMedicalStatusCode.AwaitingRescue,
            treatmentSupplyCommitPhase =
                (int)CharacterMedicalSupplyCommitPhase.SupplyPublished,
            treatmentSupplyOperationSequence = 1,
            treatmentSupplyOperationId = operationId,
            treatmentSupplyReasonCode = reasonCode,
            treatmentPhysicalItemId = "medicine:runtime-sink-ordering",
            treatmentPhysicalQuantity = 1,
            treatmentSourceStackIds = new List<string>
            {
                "stack:medical-sink-fixture"
            },
            treatmentInputMassGrams = 140L,
            treatmentPhysicalCommitId = receipt.CommitId
        };

        using MedicalRuntimeFixture fixture = new(
            order,
            downed: false,
            sink,
            ownerDrain);
        fixture.Runtime.NotifyCharacterRecovered(fixture.Actor);

        Require(events.SequenceEqual(new[] { "sink-recovery", "drain-begin" })
            && (CharacterMedicalSupplyCommitPhase)
                order.treatmentSupplyCommitPhase ==
                CharacterMedicalSupplyCommitPhase.None
            && sink.AcknowledgeCount == 1
            && ownerDrain.BeginCount == 1,
            "Pending treatment Sink was not recovered before destination drain began.");
    }

    private static void VerifyTerminalTargetReopensWhenPatientIsDownedAgain()
    {
        DrainFixture fixture = new();
        CharacterMedicalOrder order = fixture.CreateCurrentOrder(1);
        fixture.Children.DeferAdvance = true;
        CharacterMedicalSupplyDestinationDrainAdvanceResult terminal = fixture
            .Runtime.TryBeginOrResume(
                order,
                CharacterMedicalOrderState.Completed,
                CharacterMedicalStatusCode.TreatmentCompleted,
                Array.Empty<string>());
        Require(terminal.Status ==
                CharacterMedicalSupplyDestinationDrainAdvanceStatus.Deferred
            && RequireSingleActive(order).targetState ==
                CharacterMedicalOrderState.Completed,
            "Terminal target fixture did not remain pending.");

        fixture.Children.DeferAdvance = false;
        CharacterMedicalSupplyDestinationDrainAdvanceResult reopened = fixture
            .Runtime.TryBeginOrResume(
                order,
                CharacterMedicalOrderState.AwaitingStabilization,
                CharacterMedicalStatusCode.AwaitingStabilization,
                Array.Empty<string>());

        Require(reopened.IsClosed
            && order.state == CharacterMedicalOrderState.AwaitingStabilization
            && order.statusCode ==
                CharacterMedicalStatusCode.AwaitingStabilization
            && order.treatmentDestinationDrainJoins.Single().targetState ==
                CharacterMedicalOrderState.AwaitingStabilization,
            "A re-Downed patient did not reopen a pending terminal drain target.");
    }

    private static void
        VerifyReservationReleaseAndDownedNotificationPreserveDraining()
    {
        CharacterMedicalOrder order = new()
        {
            orderId = "medical:runtime-draining-preservation",
            patientId = "character:runtime-draining-preservation",
            rescuerId = "character:rescuer:runtime-draining-preservation",
            state = CharacterMedicalOrderState.MaterialDestinationDraining,
            statusCode = CharacterMedicalStatusCode.MaterialDestinationDraining,
            treatmentSupplyOperationSequence = 1
        };
        RecordingOwnerDrain ownerDrain = new(
            new List<string>(),
            closeImmediately: false);
        using MedicalRuntimeFixture fixture = new(
            order,
            downed: true,
            new RecordingPhysicalSink(default, new List<string>()),
            ownerDrain);

        Require(fixture.Runtime.TryReleaseReservation(
                    order.orderId,
                    rescuer: null,
                    CharacterMedicalStatusCode.ReservationReleased,
                    out DomainFailure releaseFailure)
            && !releaseFailure.IsFailure
            && order.state ==
                CharacterMedicalOrderState.MaterialDestinationDraining
            && order.statusCode ==
                CharacterMedicalStatusCode.MaterialDestinationDraining,
            "TryReleaseReservation overwrote MaterialDestinationDraining.");

        fixture.Runtime.NotifyCharacterDowned(fixture.Actor);
        Require(order.state ==
                CharacterMedicalOrderState.MaterialDestinationDraining
            && order.statusCode ==
                CharacterMedicalStatusCode.MaterialDestinationDraining
            && ownerDrain.BeginCount == 1
            && ownerDrain.LastTargetState is
                CharacterMedicalOrderState.AwaitingStabilization
                or CharacterMedicalOrderState.AwaitingRescue,
            "NotifyCharacterDowned overwrote MaterialDestinationDraining.");
    }

    private static void
        VerifyDetachedCrossJoinRejectsTamperMissingDuplicateAndLowerOnly()
    {
        DrainFixture fixture = new();
        CharacterMedicalOrder order = fixture.CreateCurrentOrder(1);
        Require(fixture.Runtime.TryBeginOrResume(
                    order,
                    CharacterMedicalOrderState.AwaitingBed,
                    CharacterMedicalStatusCode.AwaitingBed,
                    Array.Empty<string>()).IsClosed,
            "Cross-join fixture did not close.");
        FacilityBufferDestinationCustodyDrainSnapshot child = fixture.Children
            .Snapshots.Single();
        CharacterMedicalSupplyDestinationDrainCrossAggregateJoin.Validate(
            new[] { order },
            new[] { child });

        FacilityBufferDestinationCustodyDrainSnapshot tampered = CopyChild(
            child,
            ownerFacilityId: child.OwnerFacilityId + ":tampered");
        RequireThrows(
            () => CharacterMedicalSupplyDestinationDrainCrossAggregateJoin
                .Validate(new[] { order }, new[] { tampered }),
            "cross-join-invalid");
        RequireThrows(
            () => CharacterMedicalSupplyDestinationDrainCrossAggregateJoin
                .Validate(
                    new[] { order },
                    Array.Empty<
                        FacilityBufferDestinationCustodyDrainSnapshot>()),
            "cross-join-invalid");
        RequireThrows(
            () => CharacterMedicalSupplyDestinationDrainCrossAggregateJoin
                .Validate(new[] { order }, new[] { child, child }),
            "lower-owner-invalid");

        CharacterMedicalOrder lowerOnlyOwner = new()
        {
            orderId = order.orderId,
            patientId = order.patientId,
            state = CharacterMedicalOrderState.AwaitingBed,
            statusCode = CharacterMedicalStatusCode.AwaitingBed,
            nextTreatmentMaterialDestinationSequence = 2,
            treatmentSupplyOperationSequence = 1,
            treatmentDestinationDrainJoins = new List<
                CharacterMedicalSupplyDestinationDrainJoinData>()
        };
        RequireThrows(
            () => CharacterMedicalSupplyDestinationDrainCrossAggregateJoin
                .Validate(new[] { lowerOnlyOwner }, new[] { child }),
            "lower-without-upper");
    }

    private static CharacterMedicalSupplyDestinationDrainJoinData
        RequireSingleActive(CharacterMedicalOrder order) => order
        .treatmentDestinationDrainJoins.Single(value => value.phase !=
            CharacterMedicalSupplyDestinationDrainPhase
                .ClosedAwaitingCheckpointGc);

    private static FacilityBufferDestinationCustodyDrainSnapshot CopyChild(
        FacilityBufferDestinationCustodyDrainSnapshot source,
        string ownerFacilityId) => new(
        source.ParentOperationId,
        source.StepOperationId,
        source.OwnerStableId,
        source.OwnerSubjectId,
        ownerFacilityId,
        source.SourceDestinationId,
        source.SourceAuthorityFingerprint,
        source.RequestFingerprint,
        source.OwnerGridX,
        source.OwnerGridY,
        source.Phase,
        source.SourceActorCount,
        source.CompletedActorCount,
        source.SourceOperationCount,
        source.ReleasedOperationCount,
        source.InputQuantity,
        source.InputMassGrams,
        source.ReleasedQuantity,
        source.ReleasedMassGrams,
        source.CommitId,
        source.ReceiptFingerprint);

    private sealed class DrainFixture
    {
        internal DrainFixture()
        {
            Claims = new RecordingClaimQuery();
            Destinations = new RecordingDestinationRuntime(Claims);
            Children = new RecordingCustodyDrainService();
            Runtime = new CharacterMedicalSupplyDestinationDrainRuntime(
                Children,
                Claims,
                Destinations);
        }

        internal RecordingClaimQuery Claims { get; }
        internal RecordingDestinationRuntime Destinations { get; }
        internal RecordingCustodyDrainService Children { get; }
        internal CharacterMedicalSupplyDestinationDrainRuntime Runtime { get; }

        internal CharacterMedicalOrder CreateCurrentOrder(int sequence)
        {
            CharacterMedicalOrder order = new()
            {
                orderId = "medical:destination-drain-runtime",
                patientId = "character:destination-drain-runtime",
                treatmentFacilityId = FacilityId(sequence),
                state = CharacterMedicalOrderState.AwaitingBed,
                statusCode = CharacterMedicalStatusCode.AwaitingBed,
                treatmentMaterialDestinationId =
                    CharacterMedicalSupplyDestinationAuthority
                        .FormatDestinationId(
                            "medical:destination-drain-runtime",
                            sequence),
                nextTreatmentMaterialDestinationSequence = sequence + 1,
                treatmentDestinationSequence = sequence,
                treatmentBufferCapacityGrams = 900L,
                treatmentMassAuthorityRevision = 1L,
                treatmentCapacityFingerprint = DigestA,
                treatmentSupplyOperationSequence = 1,
                treatmentDestinationDrainJoins = new List<
                    CharacterMedicalSupplyDestinationDrainJoinData>()
            };
            Claims.Set(CreateClaim(order));
            return order;
        }

        internal void AssignNextLifetime(
            CharacterMedicalOrder order,
            int sequence)
        {
            order.treatmentFacilityId = FacilityId(sequence);
            order.treatmentDestinationSequence = sequence;
            order.nextTreatmentMaterialDestinationSequence = sequence + 1;
            order.treatmentMaterialDestinationId =
                CharacterMedicalSupplyDestinationAuthority.FormatDestinationId(
                    order.orderId,
                    sequence);
            order.treatmentBufferCapacityGrams = 900L;
            order.treatmentMassAuthorityRevision = 1L;
            order.treatmentCapacityFingerprint = DigestA;
            order.state = CharacterMedicalOrderState.AwaitingBed;
            order.SetStatus(CharacterMedicalStatusCode.AwaitingBed);
            Claims.Set(CreateClaim(order));
        }

        private static string FacilityId(int sequence) =>
            $"facility:medical-drain-runtime:{sequence}";

        private static FacilityBufferDestinationClaim CreateClaim(
            CharacterMedicalOrder order) => new(
            order.treatmentMaterialDestinationId,
            new Vector2Int(order.treatmentDestinationSequence + 2, 7),
            CharacterMedicalSupplyDestinationAuthority.OwnerDomain,
            CharacterMedicalSupplyDestinationAuthority.FormatOwnerOperationId(
                order.orderId,
                order.treatmentDestinationSequence),
            order.treatmentFacilityId,
            FacilityBufferDestinationAnchorKind.LiveFacility,
            FacilityBufferDestinationAdmissionPolicy.ExactGramRequired);
    }

    private sealed class RecordingClaimQuery :
        IFacilityBufferDestinationClaimQuery
    {
        private readonly List<FacilityBufferDestinationClaim> claims = new();

        public long Revision { get; private set; }

        internal void Set(FacilityBufferDestinationClaim claim)
        {
            claims.Clear();
            claims.Add(claim);
            Revision++;
        }

        internal void Remove(string destinationId)
        {
            claims.RemoveAll(value => string.Equals(
                value.DestinationId,
                destinationId,
                StringComparison.Ordinal));
            Revision++;
        }

        public bool TryGetClaim(
            string destinationId,
            Vector2Int dropPosition,
            out FacilityBufferDestinationClaim claim)
        {
            claim = claims.SingleOrDefault(value =>
                value.DropPosition == dropPosition
                && string.Equals(value.DestinationId, destinationId,
                    StringComparison.Ordinal));
            return claim != null;
        }

        public IReadOnlyList<FacilityBufferDestinationClaim> CaptureClaims() =>
            claims.ToArray();
    }

    private sealed class RecordingDestinationRuntime :
        ICharacterMedicalSupplyDestinationRuntime
    {
        private readonly RecordingClaimQuery claims;

        internal RecordingDestinationRuntime(RecordingClaimQuery claims) =>
            this.claims = claims;

        internal List<int> RevokedSequences { get; } = new();
        internal Action BeforeRevoke { get; set; }

        public bool TryEnsure(
            CharacterMedicalOrder order,
            BuildableObject facility,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryReplace(
            IReadOnlyList<CharacterMedicalOrder> orders,
            IReadOnlyDictionary<string, Vector2Int> facilityPositions,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryRevoke(
            CharacterMedicalOrder order,
            out string failureReason)
        {
            BeforeRevoke?.Invoke();
            failureReason = string.Empty;
            RevokedSequences.Add(order.treatmentDestinationSequence);
            claims.Remove(order.treatmentMaterialDestinationId);
            return true;
        }

        public bool TryValidate(
            CharacterMedicalOrder order,
            out string failureReason)
        {
            failureReason = string.Empty;
            return order != null
                && order.treatmentDestinationSequence > 0
                && order.treatmentBufferCapacityGrams > 0L
                && order.treatmentMassAuthorityRevision > 0L
                && order.treatmentCapacityFingerprint == DigestA;
        }
    }

    private sealed class RecordingCustodyDrainService :
        IFacilityBufferDestinationCustodyDrainService
    {
        private readonly Dictionary<string,
            FacilityBufferDestinationCustodyDrainSnapshot> snapshots =
            new(StringComparer.Ordinal);

        public bool RequiresImmediateRecoveryBeforeGameplayTick => true;
        internal bool DeferAdvance { get; set; }
        internal Action BeforeAdvance { get; set; }
        internal Action BeforeAcknowledge { get; set; }
        internal IReadOnlyList<FacilityBufferDestinationCustodyDrainSnapshot>
            Snapshots => snapshots.Values
                .OrderBy(value => value.StepOperationId, StringComparer.Ordinal)
                .ToArray();

        public FacilityBufferDestinationCustodyDrainResult TryPrepare(
            FacilityBufferDestinationCustodyDrainDescriptor descriptor)
        {
            if (snapshots.TryGetValue(
                    descriptor.StepOperationId,
                    out FacilityBufferDestinationCustodyDrainSnapshot replay))
            {
                return Applied(replay, replay: true);
            }
            FacilityBufferDestinationCustodyDrainSnapshot prepared = new(
                descriptor.ParentOperationId,
                descriptor.StepOperationId,
                descriptor.OwnerStableId,
                descriptor.OwnerSubjectId,
                descriptor.OwnerFacilityId,
                descriptor.SourceDestinationId,
                descriptor.SourceAuthorityFingerprint,
                DigestA,
                descriptor.OwnerPosition.x,
                descriptor.OwnerPosition.y,
                FacilityBufferDestinationCustodyDrainPhase.Prepared,
                0,
                0,
                0,
                0,
                1,
                500L,
                0,
                0L,
                string.Empty,
                string.Empty);
            snapshots.Add(descriptor.StepOperationId, prepared);
            return Applied(prepared, replay: false);
        }

        public FacilityBufferDestinationCustodyDrainResult TryAdvance(
            string stepOperationId,
            string requestFingerprint)
        {
            BeforeAdvance?.Invoke();
            FacilityBufferDestinationCustodyDrainSnapshot source =
                snapshots[stepOperationId];
            if (DeferAdvance)
            {
                return new FacilityBufferDestinationCustodyDrainResult(
                    FacilityBufferDestinationCustodyDrainStatus.Deferred,
                    source,
                    "fixture-deferred");
            }
            FacilityBufferDestinationCustodyDrainSnapshot committed = new(
                source.ParentOperationId,
                source.StepOperationId,
                source.OwnerStableId,
                source.OwnerSubjectId,
                source.OwnerFacilityId,
                source.SourceDestinationId,
                source.SourceAuthorityFingerprint,
                source.RequestFingerprint,
                source.OwnerGridX,
                source.OwnerGridY,
                FacilityBufferDestinationCustodyDrainPhase
                    .EffectCommittedAwaitingOwnerAck,
                source.SourceActorCount,
                source.SourceActorCount,
                source.SourceOperationCount,
                source.SourceOperationCount,
                source.InputQuantity,
                source.InputMassGrams,
                source.InputQuantity,
                source.InputMassGrams,
                "commit:medical-drain-runtime:" + stepOperationId,
                DigestB);
            snapshots[stepOperationId] = committed;
            return Applied(committed, replay: false);
        }

        public FacilityBufferDestinationCustodyDrainResult TryAcknowledge(
            string stepOperationId,
            string receiptFingerprint)
        {
            BeforeAcknowledge?.Invoke();
            FacilityBufferDestinationCustodyDrainSnapshot source =
                snapshots[stepOperationId];
            FacilityBufferDestinationCustodyDrainSnapshot acknowledged = new(
                source.ParentOperationId,
                source.StepOperationId,
                source.OwnerStableId,
                source.OwnerSubjectId,
                source.OwnerFacilityId,
                source.SourceDestinationId,
                source.SourceAuthorityFingerprint,
                source.RequestFingerprint,
                source.OwnerGridX,
                source.OwnerGridY,
                FacilityBufferDestinationCustodyDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc,
                source.SourceActorCount,
                source.CompletedActorCount,
                source.SourceOperationCount,
                source.ReleasedOperationCount,
                source.InputQuantity,
                source.InputMassGrams,
                source.ReleasedQuantity,
                source.ReleasedMassGrams,
                source.CommitId,
                source.ReceiptFingerprint);
            snapshots[stepOperationId] = acknowledged;
            return Applied(acknowledged, replay: false);
        }

        public bool TryCapture(
            string stepOperationId,
            out FacilityBufferDestinationCustodyDrainSnapshot snapshot) =>
            snapshots.TryGetValue(stepOperationId, out snapshot);

        private static FacilityBufferDestinationCustodyDrainResult Applied(
            FacilityBufferDestinationCustodyDrainSnapshot snapshot,
            bool replay) => new(
            replay
                ? FacilityBufferDestinationCustodyDrainStatus.Replay
                : FacilityBufferDestinationCustodyDrainStatus.Applied,
            snapshot,
            string.Empty);
    }

    private sealed class MedicalRuntimeFixture : IDisposable
    {
        private readonly GameObject actorObject;

        internal MedicalRuntimeFixture(
            CharacterMedicalOrder order,
            bool downed,
            IPhysicalFacilityItemSinkGateway sink,
            RecordingOwnerDrain ownerDrain)
        {
            actorObject = new GameObject("CharacterMedicalDrainRuntimeFixture");
            Actor = actorObject.AddComponent<CharacterActor>();
            Actor.EnsureRuntimeState();
            Actor.Identity.SetPersistentId(order.patientId);
            Actor.SetLifecycleState(downed
                ? CharacterLifecycleState.Downed
                : CharacterLifecycleState.Active);

            RuntimeWorldRegistry world = new(Actor);
            DungeonRuntimeAggregateRootStore roots = new();
            CharacterMedicalAggregateState state = new();
            state.Orders.Add(order);
            state.OrderSequence = 1;
            roots.Replace(state);
            Runtime = new CharacterMedicalRuntime(
                new FixedBodyHealthQuery(downed),
                CreateDefaultProxy<ICharacterBodyHealthCommand>(),
                new CharacterMedicalWorldServices(
                    new UnavailableGridProvider(),
                    world,
                    CreateDefaultProxy<IWorldItemStackRuntime>()),
                new GameEventBus(),
                new FixedCarePriorityQuery(),
                EmptyResourceEconomyContentCatalog.Instance,
                new ResourceItemDefinitionCatalog(
                    Array.Empty<ItemDefinitionSO>()),
                roots,
                CreateDefaultProxy<ICharacterPerformanceQuery>(),
                sink,
                new RecordingTareDisposition(),
                new AlwaysValidDestinationRuntime(),
                ownerDrain,
                new EmptyDrainRestoreCandidateQuery());
        }

        internal CharacterActor Actor { get; }
        internal CharacterMedicalRuntime Runtime { get; }

        public void Dispose() => UnityEngine.Object.DestroyImmediate(actorObject);
    }

    private sealed class RecordingOwnerDrain :
        ICharacterMedicalSupplyDestinationDrainRuntime
    {
        private readonly List<string> events;
        private readonly bool closeImmediately;

        internal RecordingOwnerDrain(
            List<string> events,
            bool closeImmediately)
        {
            this.events = events;
            this.closeImmediately = closeImmediately;
        }

        internal int BeginCount { get; private set; }
        internal CharacterMedicalOrderState LastTargetState { get; private set; }

        public CharacterMedicalSupplyDestinationDrainAdvanceResult
            TryBeginOrResume(
                CharacterMedicalOrder order,
                CharacterMedicalOrderState targetState,
                CharacterMedicalStatusCode targetStatusCode,
                IReadOnlyList<string> targetStatusParameters)
        {
            events.Add("drain-begin");
            BeginCount++;
            LastTargetState = targetState;
            if (!closeImmediately)
            {
                return new CharacterMedicalSupplyDestinationDrainAdvanceResult(
                    CharacterMedicalSupplyDestinationDrainAdvanceStatus.Deferred,
                    string.Empty,
                    "fixture-deferred");
            }
            order.state = targetState;
            order.SetStatus(
                targetStatusCode,
                (targetStatusParameters ?? Array.Empty<string>()).ToArray());
            return new CharacterMedicalSupplyDestinationDrainAdvanceResult(
                CharacterMedicalSupplyDestinationDrainAdvanceStatus.Closed,
                order.treatmentFacilityId,
                string.Empty);
        }

        public CharacterMedicalSupplyDestinationDrainAdvanceResult TryResume(
            CharacterMedicalOrder order) =>
            new(
                CharacterMedicalSupplyDestinationDrainAdvanceStatus.Deferred,
                string.Empty,
                "fixture-deferred");
    }

    private sealed class RecordingPhysicalSink :
        IPhysicalFacilityItemSinkGateway
    {
        private readonly PhysicalItemBatchDispositionReceipt receipt;
        private readonly List<string> events;

        internal RecordingPhysicalSink(
            PhysicalItemBatchDispositionReceipt receipt,
            List<string> events)
        {
            this.receipt = receipt;
            this.events = events;
        }

        internal int AcknowledgeCount { get; private set; }

        public bool TryCommitSinkPending(
            string destinationId,
            string itemId,
            int quantity,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt committed,
            out string failureReason)
        {
            committed = default;
            failureReason = "fixture-does-not-commit";
            return false;
        }

        public bool TryGetPending(
            string operationId,
            out PhysicalItemBatchDispositionReceipt pending)
        {
            pending = receipt;
            return receipt.IsCommitted
                && string.Equals(receipt.OperationId, operationId,
                    StringComparison.Ordinal);
        }

        public bool Acknowledge(string commitId, out string failureReason)
        {
            failureReason = string.Empty;
            if (!receipt.IsCommitted
                || !string.Equals(receipt.CommitId, commitId,
                    StringComparison.Ordinal))
            {
                return false;
            }
            events.Add("sink-recovery");
            AcknowledgeCount++;
            return true;
        }
    }

    private sealed class RecordingTareDisposition :
        IPackagedLotTareDispositionService
    {
        public bool EnsureTerminalSinkOutputs(
            IReadOnlyDictionary<string, int> consumedItems,
            Vector2Int outputPosition,
            string parentCommitId,
            out PackagedLotTareOutputReceipt receipt,
            out string failureReason)
        {
            receipt = default;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class AlwaysValidDestinationRuntime :
        ICharacterMedicalSupplyDestinationRuntime
    {
        public bool TryEnsure(
            CharacterMedicalOrder order,
            BuildableObject facility,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryReplace(
            IReadOnlyList<CharacterMedicalOrder> orders,
            IReadOnlyDictionary<string, Vector2Int> facilityPositions,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryRevoke(
            CharacterMedicalOrder order,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryValidate(
            CharacterMedicalOrder order,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class EmptyDrainRestoreCandidateQuery :
        IFacilityBufferDestinationCustodyDrainRestoreCandidateQuery
    {
        public bool IsCandidateAvailable => true;
        public IReadOnlyList<FacilityBufferDestinationCustodyDrainSnapshot>
            Drains => Array.Empty<
                FacilityBufferDestinationCustodyDrainSnapshot>();

        public bool TryGetDrain(
            string stepOperationId,
            out FacilityBufferDestinationCustodyDrainSnapshot snapshot)
        {
            snapshot = null;
            return false;
        }
    }

    private sealed class FixedBodyHealthQuery : ICharacterBodyHealthQuery
    {
        private readonly bool downed;

        internal FixedBodyHealthQuery(bool downed) => this.downed = downed;

        public CharacterVitalsSnapshot GetVitals(CharacterActor actor) =>
            new(100f, 100f, 0f);
        public CharacterVitalsSnapshot GetVitals(string characterId) =>
            new(100f, 100f, 0f);
        public CharacterBodyHealthSnapshot GetSnapshot(CharacterActor actor) =>
            Snapshot();
        public CharacterBodyHealthSnapshot GetSnapshot(string characterId) =>
            Snapshot();
        public float GetTotalBleeding(CharacterActor target) => 0f;
        public float GetMissingPartHealth(CharacterActor target) => 0f;

        private CharacterBodyHealthSnapshot Snapshot() => new(
            Array.Empty<CharacterBodyPartHealthState>(),
            0f,
            0f,
            1f,
            1f,
            downed ? 0.05f : 1f,
            downed);
    }

    private sealed class FixedCarePriorityQuery : ICharacterCarePriorityQuery
    {
        public bool IsCareSubject(string persistentCharacterId) => true;
        public int GetCarePriority(string persistentCharacterId) => 1;
    }

    private sealed class UnavailableGridProvider : IGridSystemProvider
    {
        public GridSystemManager Manager => null;
        public Grid Grid => null;
        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }
        public bool TryGetGrid(out Grid grid)
        {
            grid = null;
            return false;
        }
    }

    private sealed class RuntimeWorldRegistry : ICharacterAiWorldRegistry
    {
        private readonly List<CharacterActor> characters;

        internal RuntimeWorldRegistry(CharacterActor actor) =>
            characters = new List<CharacterActor> { actor };

        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters => characters;
        public int LifetimeCharacterVersion => 1;
        public IReadOnlyList<CharacterActor> AllCharacters => characters;
        public int WildlifeVersion => 0;
        public IReadOnlyList<WildlifeActor> Wildlife =>
            Array.Empty<WildlifeActor>();
        public int BuildingVersion => 0;
        public IReadOnlyList<BuildableObject> Buildings =>
            Array.Empty<BuildableObject>();
        public int WarehouseVersion => 0;
        public IReadOnlyList<IWarehouseFacility> Warehouses =>
            Array.Empty<IWarehouseFacility>();
        public int RetailVersion => 0;
        public IReadOnlyList<IRetailFacility> RetailFacilities =>
            Array.Empty<IRetailFacility>();
        public int Version => 1;
        public void RegisterCharacter(CharacterActor actor) =>
            characters.Add(actor);
        public void UnregisterCharacter(CharacterActor actor) =>
            characters.Remove(actor);
        public void RegisterCharacterLifetime(CharacterActor actor) { }
        public void UnregisterCharacterLifetime(CharacterActor actor) { }
        public void RegisterWildlife(WildlifeActor actor) { }
        public void UnregisterWildlife(WildlifeActor actor) { }
        public void RegisterBuilding(BuildableObject building) { }
        public void UnregisterBuilding(BuildableObject building) { }
        public int ReleaseTransientBuildingOwnership(
            IBuildingVisitorPort visitor,
            string reason) => 0;
        public int GetTransientBuildingOwnershipCount(CharacterId characterId) =>
            0;
        public void RegisterWarehouse(IWarehouseFacility warehouse) { }
        public void UnregisterWarehouse(IWarehouseFacility warehouse) { }
        public void SetGrid(Grid grid) { }
        public bool TryGetGrid(out Grid grid)
        {
            grid = null;
            return false;
        }
        public bool TryGetSessionState(out GameSessionState data)
        {
            data = null;
            return false;
        }
        public void Clear() => characters.Clear();
    }

    public class DefaultDispatchProxy : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            ParameterInfo[] parameters = targetMethod.GetParameters();
            for (int index = 0; index < parameters.Length; index++)
            {
                if (parameters[index].IsOut)
                {
                    Type valueType = parameters[index].ParameterType
                        .GetElementType();
                    args[index] = valueType != null && valueType.IsValueType
                        ? Activator.CreateInstance(valueType)
                        : null;
                }
            }
            Type returnType = targetMethod.ReturnType;
            return returnType == typeof(void)
                ? null
                : returnType.IsValueType
                    ? Activator.CreateInstance(returnType)
                    : null;
        }
    }

    private static T CreateDefaultProxy<T>() where T : class =>
        DispatchProxy.Create<T, DefaultDispatchProxy>();

    private static void RequireThrows(Action action, string expected)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            Require(exception.Message.Contains(expected,
                    StringComparison.Ordinal),
                "Unexpected fail-loud reason: " + exception.Message);
            return;
        }
        throw new InvalidOperationException(
            "Expected fail-loud cross-join rejection: " + expected);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
