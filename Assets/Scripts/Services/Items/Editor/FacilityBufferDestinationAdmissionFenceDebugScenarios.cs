#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class FacilityBufferDestinationAdmissionFenceDebugScenarios
{
    private const string DestinationId =
        "facility-input:exact:qa:owner-neutral-fence";
    private const string OwnerDomain = "qa.owner-neutral-fence";
    private const string OwnerOperationId =
        "qa:owner-neutral-fence:operation";
    private const string OwnerFacilityId =
        "building:qa-owner-neutral-fence";
    private static readonly Vector2Int DropPosition = new(13, 7);

    [MenuItem(
        "DungeonStory/Debug/V27/Run Owner-Neutral FacilityBuffer Admission Fence Contracts")]
    public static void RunAll()
    {
        VerifyCanonicalSourceCompositionIsDeterministic();
        VerifyProductionCompatibilitySourceAndConstructor();
        VerifyUnifiedProductionMutationSourceBlocksTransientAndDurable();
        VerifyOwnerNeutralReservedTargetWithoutFacilityIsUnfenced();
        VerifySecondSyntheticSourceFencesWithoutAdmissionChanges();
        VerifyDuplicateSourceIsRejected();
        VerifyMultipleSourcesForOneSubjectFailLoudly();
        VerifyMassAdmissionFenceBlocksWithoutMutation();

        Debug.Log(
            "[V27][PASS] Owner-neutral FacilityBuffer admission fence source "
            + "composition, production compatibility, extension fencing, "
            + "ambiguity rejection, and mutation-free admission blocking are exact.");
    }

    private static void VerifyCanonicalSourceCompositionIsDeterministic()
    {
        SyntheticFenceSource alpha = new(
            "qa.admission-fence.alpha",
            revision: 11L,
            operationId: "qa:fence:alpha",
            subject => string.Equals(
                subject.OwnerDomain,
                "qa.alpha",
                StringComparison.Ordinal));
        SyntheticFenceSource zeta = new(
            "qa.admission-fence.zeta",
            revision: 29L,
            operationId: "qa:fence:zeta",
            subject => string.Equals(
                subject.DestinationId,
                DestinationId,
                StringComparison.Ordinal));

        FacilityBufferDestinationAdmissionFenceQuery forward = new(
            new IFacilityBufferDestinationAdmissionFenceSource[]
            {
                alpha,
                zeta
            });
        FacilityBufferDestinationAdmissionFenceQuery reversed = new(
            new IFacilityBufferDestinationAdmissionFenceSource[]
            {
                zeta,
                alpha
            });
        FacilityBufferDestinationAdmissionFenceSubject subject =
            CreateSubject();

        Require(
            forward.Revision == reversed.Revision,
            "Fence composition revision depended on registration order.");
        Require(
            forward.TryCaptureOpenFence(subject, out var first)
            && reversed.TryCaptureOpenFence(subject, out var second)
            && string.Equals(
                first.SourceId,
                zeta.SourceId,
                StringComparison.Ordinal)
            && string.Equals(
                first.OperationId,
                "qa:fence:zeta",
                StringComparison.Ordinal)
            && first.Revision == zeta.Revision
            && string.Equals(
                first.SourceId,
                second.SourceId,
                StringComparison.Ordinal)
            && string.Equals(
                first.OperationId,
                second.OperationId,
                StringComparison.Ordinal)
            && first.Revision == second.Revision,
            "Canonical source composition did not return the same snapshot.");

        FacilityBufferDestinationAdmissionFenceQuery closed = new(
            new[]
            {
                new SyntheticFenceSource(
                    "qa.admission-fence.closed",
                    revision: 1L,
                    operationId: "qa:fence:closed",
                    _ => false)
            });
        Require(
            !closed.TryCaptureOpenFence(subject, out _),
            "A closed canonical source unexpectedly fenced the subject.");
        Require(
            Throws<ArgumentException>(() => forward.TryCaptureOpenFence(
                new FacilityBufferDestinationAdmissionFenceSubject(
                    " " + DestinationId,
                    OwnerDomain,
                    OwnerOperationId,
                    OwnerFacilityId),
                out _)),
            "A non-canonical admission subject was accepted.");
        Require(
            Throws<InvalidOperationException>(() =>
                _ = new FacilityBufferDestinationAdmissionFenceQuery(
                    new[]
                    {
                        new SyntheticFenceSource(
                            " qa.admission-fence.invalid",
                            revision: 1L,
                            operationId: "qa:fence:invalid",
                            _ => false)
                    })),
            "A non-canonical source ID was accepted.");
    }

    private static void VerifyProductionCompatibilitySourceAndConstructor()
    {
        FakeProductionOpenOperationQuery open = new(
            (BuildingInstanceId)OwnerFacilityId,
            revision: 41,
            snapshotRevision: 17L)
        {
            IsFenceOpen = true
        };
        ProductionFacilityDestructiveDrainAdmissionFenceSource source = new(
            open);
        FacilityBufferDestinationAdmissionFenceQuery composition = new(
            new IFacilityBufferDestinationAdmissionFenceSource[] { source });

        Require(
            string.Equals(
                source.SourceId,
                ProductionFacilityDestructiveDrainAdmissionFenceSource
                    .StableSourceId,
                StringComparison.Ordinal)
            && source.Revision == open.Revision
            && composition.TryCaptureOpenFence(
                CreateSubject(),
                out FacilityBufferDestinationAdmissionFenceSnapshot snapshot)
            && string.Equals(
                snapshot.SourceId,
                source.SourceId,
                StringComparison.Ordinal)
            && string.Equals(
                snapshot.OperationId,
                ProductionFacilityDestructiveDrainOperationId.FromFacility(
                    (BuildingInstanceId)OwnerFacilityId).Value,
                StringComparison.Ordinal)
            && snapshot.Revision == 17L,
            "Production destructive-drain compatibility source lost its durable snapshot.");

        AdmissionFixture fixture = new(open);
        FacilityBufferMassAdmissionRequest request = fixture.CreateExactRequest(
            "qa:production-compatible:transfer");
        fixture.Occupancy.ResetCounters();
        Require(
            !fixture.Admission.TryReserveExactLot(
                request,
                out _,
                out FacilityBufferMassAdmissionFailureCode failure,
                out string reason)
            && failure
                == FacilityBufferMassAdmissionFailureCode
                    .OwnerDestructiveDrainOpen
            && reason.Contains(
                ProductionFacilityDestructiveDrainOperationId.FromFacility(
                    (BuildingInstanceId)OwnerFacilityId).Value,
                StringComparison.Ordinal)
            && fixture.Occupancy.CaptureCalls == 0
            && fixture.Occupancy.ExactLotCaptureCalls == 0,
            "The compatibility constructor did not fence before physical admission work.");
    }

    private static void VerifyUnifiedProductionMutationSourceBlocksTransientAndDurable()
    {
        DungeonRuntimeAggregateRootStore roots = new();
        ProductionFacilityDestructiveDrainJournal journal = new(roots);
        ProductionFacilityDestructiveDrainOpenOperationQuery durable =
            new(roots);
        ProductionFacilityMutationEpochRuntime transient = new();
        ProductionFacilityMutationAuthorityGate gate = new(
            transient,
            durable);
        ProductionFacilityMutationAdmissionFenceSource source = new(gate);
        FacilityBufferDestinationAdmissionFenceQuery composition = new(
            new IFacilityBufferDestinationAdmissionFenceSource[] { source });
        AdmissionFixture fixture = new(composition);
        BuildingInstanceId facilityId =
            (BuildingInstanceId)OwnerFacilityId;
        const string transientOperation = "qa:facility-mutation:transient";

        Require(
            gate.TryBegin(
                facilityId,
                transientOperation,
                out long epoch,
                out string beginFailure)
            && string.IsNullOrEmpty(beginFailure),
            "Unified admission fixture could not open a transient mutation: "
            + beginFailure);
        VerifyUnifiedFenceRejectsBothWithoutMutation(
            fixture,
            transientOperation);

        string fingerprint = new('b', 64);
        string durableMutation = ProductionFacilityDestructiveDrainCanonical
            .BuildInitiatingMutationOperationId(
                ProductionFacilityDestructiveDrainCause.ExplicitDemolition,
                facilityId);
        Require(
            journal.TryRequest(
                ProductionFacilityDestructiveDrainCause.ExplicitDemolition,
                facilityId,
                durableMutation,
                fingerprint,
                Array.Empty<
                    ProductionFacilityDestructiveDrainParticipantSaveData>(),
                out ProductionFacilityDestructiveDrainEntrySaveData durableEntry,
                out string durableFailure)
            && string.IsNullOrEmpty(durableFailure),
            "Unified admission fixture could not open a durable mutation: "
            + durableFailure);
        VerifyUnifiedFenceRejectsBothWithoutMutation(
            fixture,
            ProductionFacilityDestructiveDrainOperationId.FromFacility(
                facilityId).Value);

        Require(
            gate.TryEnd(
                facilityId,
                transientOperation,
                epoch,
                out string endFailure)
            && string.IsNullOrEmpty(endFailure),
            "Unified admission fixture could not close transient overlap: "
            + endFailure);
        durableEntry = AdvanceToCheckpointGc(
            journal,
            ProductionFacilityDestructiveDrainOperationId.FromFacility(
                facilityId),
            durableEntry,
            fingerprint);
        Require(
            journal.TryRemoveCheckpointed(
                ProductionFacilityDestructiveDrainOperationId.FromFacility(
                    facilityId),
                durableEntry.revision,
                out string removeFailure)
            && string.IsNullOrEmpty(removeFailure),
            "Unified admission fixture could not close durable mutation: "
            + removeFailure);

        FacilityBufferMassAdmissionRequest exact = fixture.CreateExactRequest(
            "qa:facility-mutation:after-close");
        Require(
            fixture.Admission.TryReserveExactLot(
                exact,
                out FacilityBufferMassAdmissionToken token,
                out _,
                out string reserveFailure)
            && fixture.Admission.TryRelease(
                token,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _),
            "Unified admission did not reopen after all mutation fences closed: "
            + reserveFailure);
    }

    private static void VerifyUnifiedFenceRejectsBothWithoutMutation(
        AdmissionFixture fixture,
        string expectedOperationId)
    {
        FacilityBufferMassAdmissionRequest exact = fixture.CreateExactRequest(
            "qa:facility-mutation:blocked-exact:" + expectedOperationId);
        FacilityBufferPlannedOutputRequest planned =
            fixture.CreatePlannedRequest(
                "qa:facility-mutation:blocked-planned:" + expectedOperationId);
        long revisionBefore = fixture.Admission.Revision;
        fixture.Occupancy.ResetCounters();
        fixture.Mass.ResetCounters();

        Require(
            !fixture.Admission.TryReserveExactLot(
                exact,
                out _,
                out FacilityBufferMassAdmissionFailureCode exactFailure,
                out string exactReason)
            && exactFailure ==
                FacilityBufferMassAdmissionFailureCode.OwnerMutationFenceOpen
            && exactReason.Contains(expectedOperationId, StringComparison.Ordinal)
            && !fixture.Admission.TryReservePlannedOutput(
                planned,
                out _,
                out FacilityBufferMassAdmissionFailureCode plannedFailure,
                out string plannedReason)
            && plannedFailure ==
                FacilityBufferMassAdmissionFailureCode.OwnerMutationFenceOpen
            && plannedReason.Contains(expectedOperationId, StringComparison.Ordinal)
            && fixture.Admission.Revision == revisionBefore
            && fixture.Occupancy.CaptureCalls == 0
            && fixture.Occupancy.ExactLotCaptureCalls == 0
            && fixture.Mass.QueryCalls == 0,
            "Unified mutation fence did not reject exact/planned admission without mutation.");
    }

    private static void VerifyOwnerNeutralReservedTargetWithoutFacilityIsUnfenced()
    {
        FakeProductionOpenOperationQuery open = new(
            (BuildingInstanceId)OwnerFacilityId,
            revision: 43,
            snapshotRevision: 19L)
        {
            IsFenceOpen = true
        };
        FacilityBufferDestinationAdmissionFenceQuery composition = new(
            new IFacilityBufferDestinationAdmissionFenceSource[]
            {
                new ProductionFacilityDestructiveDrainAdmissionFenceSource(open)
            });
        FacilityBufferDestinationAdmissionFenceSubject subject = new(
            "expedition:qa-owner-neutral-package",
            "offense.expedition-supply",
            "qa:expedition-package:1",
            null);

        Require(
            subject.IsCanonical
            && !composition.TryCaptureOpenFence(subject, out _),
            "A facility-neutral ReservedTarget was rejected or incorrectly fenced by the production-facility source.");
    }

    private static void VerifySecondSyntheticSourceFencesWithoutAdmissionChanges()
    {
        FakeProductionOpenOperationQuery production = new(
            (BuildingInstanceId)OwnerFacilityId,
            revision: 3,
            snapshotRevision: 2L)
        {
            IsFenceOpen = false
        };
        SyntheticFenceSource research = new(
            "research.arcane-index.destination-drain",
            revision: 7L,
            operationId: "research:arcane-index:drain:1",
            subject => string.Equals(
                subject.OwnerDomain,
                OwnerDomain,
                StringComparison.Ordinal));
        FacilityBufferDestinationAdmissionFenceQuery composition = new(
            new IFacilityBufferDestinationAdmissionFenceSource[]
            {
                new ProductionFacilityDestructiveDrainAdmissionFenceSource(
                    production),
                research
            });

        Require(
            composition.TryCaptureOpenFence(
                CreateSubject(),
                out FacilityBufferDestinationAdmissionFenceSnapshot snapshot)
            && string.Equals(
                snapshot.SourceId,
                research.SourceId,
                StringComparison.Ordinal)
            && string.Equals(
                snapshot.OperationId,
                "research:arcane-index:drain:1",
                StringComparison.Ordinal)
            && snapshot.Revision == research.Revision,
            "A second owner-neutral source did not fence through composition.");
    }

    private static void VerifyDuplicateSourceIsRejected()
    {
        Require(
            Throws<InvalidOperationException>(() =>
                _ = new FacilityBufferDestinationAdmissionFenceQuery(
                    new IFacilityBufferDestinationAdmissionFenceSource[]
                    {
                        new SyntheticFenceSource(
                            "qa.admission-fence.duplicate",
                            revision: 1L,
                            operationId: "qa:fence:duplicate:a",
                            _ => false),
                        new SyntheticFenceSource(
                            "qa.admission-fence.duplicate",
                            revision: 2L,
                            operationId: "qa:fence:duplicate:b",
                            _ => false)
                    })),
            "Duplicate admission fence source IDs were accepted.");
    }

    private static void VerifyMultipleSourcesForOneSubjectFailLoudly()
    {
        FacilityBufferDestinationAdmissionFenceQuery composition = new(
            new IFacilityBufferDestinationAdmissionFenceSource[]
            {
                new SyntheticFenceSource(
                    "qa.admission-fence.first-owner",
                    revision: 1L,
                    operationId: "qa:fence:first-owner",
                    _ => true),
                new SyntheticFenceSource(
                    "qa.admission-fence.second-owner",
                    revision: 1L,
                    operationId: "qa:fence:second-owner",
                    _ => true)
            });

        Require(
            Throws<InvalidOperationException>(() =>
                composition.TryCaptureOpenFence(CreateSubject(), out _)),
            "Multiple sources fencing the same subject did not fail loudly.");
    }

    private static void VerifyMassAdmissionFenceBlocksWithoutMutation()
    {
        SyntheticFenceSource source = new(
            "qa.admission-fence.synthetic-owner",
            revision: 53L,
            operationId: "qa:synthetic-owner:drain",
            subject => string.Equals(
                    subject.DestinationId,
                    DestinationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    subject.OwnerDomain,
                    OwnerDomain,
                    StringComparison.Ordinal)
                && string.Equals(
                    subject.OwnerOperationId,
                    OwnerOperationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    subject.OwnerFacilityId,
                    OwnerFacilityId,
                    StringComparison.Ordinal));
        FacilityBufferDestinationAdmissionFenceQuery composition = new(
            new[] { source });
        AdmissionFixture fixture = new(composition);
        FacilityBufferMassAdmissionRequest exact = fixture.CreateExactRequest(
            "qa:synthetic-fence:exact-transfer");
        FacilityBufferPlannedOutputRequest planned =
            fixture.CreatePlannedRequest(
                "qa:synthetic-fence:planned-publication");

        long revisionBefore = fixture.Admission.Revision;
        int profileCountBefore = fixture.Admission.CaptureProfiles().Count;
        Require(
            fixture.Admission.TryGetCapacity(
                DestinationId,
                DropPosition,
                out FacilityBufferMassCapacitySnapshot before)
            && before.ReservedMassGrams == 0L,
            "Admission fence fixture began with reserved grams.");
        fixture.Occupancy.ResetCounters();
        fixture.Mass.ResetCounters();

        Require(
            !fixture.Admission.TryReserveExactLot(
                exact,
                out FacilityBufferMassAdmissionToken blockedExact,
                out FacilityBufferMassAdmissionFailureCode exactFailure,
                out string exactReason)
            && exactFailure
                == FacilityBufferMassAdmissionFailureCode
                    .OwnerDestructiveDrainOpen
            && string.IsNullOrEmpty(blockedExact.TokenId)
            && exactReason.Contains(
                "qa:synthetic-owner:drain",
                StringComparison.Ordinal),
            "Synthetic source did not block exact-lot admission.");
        Require(
            !fixture.Admission.TryReservePlannedOutput(
                planned,
                out FacilityBufferPlannedOutputToken blockedPlanned,
                out FacilityBufferMassAdmissionFailureCode plannedFailure,
                out string plannedReason)
            && plannedFailure
                == FacilityBufferMassAdmissionFailureCode
                    .OwnerDestructiveDrainOpen
            && string.IsNullOrEmpty(blockedPlanned.TokenId)
            && plannedReason.Contains(
                "qa:synthetic-owner:drain",
                StringComparison.Ordinal),
            "Synthetic source did not block planned-output admission.");
        Require(
            fixture.Admission.Revision == revisionBefore
            && fixture.Admission.CaptureProfiles().Count == profileCountBefore
            && fixture.Admission.TryGetCapacity(
                DestinationId,
                DropPosition,
                out FacilityBufferMassCapacitySnapshot after)
            && after.ReservedMassGrams == 0L
            && fixture.Occupancy.CaptureCalls == 0
            && fixture.Occupancy.ExactLotCaptureCalls == 0
            && fixture.Mass.QueryCalls == 0,
            "A fenced admission mutated capacity, tokens, or physical/mass projections.");

        source.IsOpen = false;
        Require(
            fixture.Admission.TryReserveExactLot(
                exact,
                out FacilityBufferMassAdmissionToken exactToken,
                out _,
                out string exactUnfencedFailure)
            && string.Equals(
                exactToken.TokenId,
                "facility-buffer-admission:000000000001",
                StringComparison.Ordinal)
            && fixture.Admission.TryRelease(
                exactToken,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _),
            "Blocked exact admission consumed operation/token authority: "
            + exactUnfencedFailure);
        Require(
            fixture.Admission.TryReservePlannedOutput(
                planned,
                out FacilityBufferPlannedOutputToken plannedToken,
                out _,
                out string plannedUnfencedFailure)
            && string.Equals(
                plannedToken.TokenId,
                "facility-buffer-planned-output-admission:000000000002",
                StringComparison.Ordinal)
            && fixture.Admission.TryReleasePlannedOutput(
                plannedToken,
                FacilityBufferMassAdmissionReleaseReason.TransactionRollback,
                out _,
                out _),
            "Blocked planned admission consumed operation/token authority: "
            + plannedUnfencedFailure);
    }

    private static FacilityBufferDestinationAdmissionFenceSubject
        CreateSubject() => new(
        DestinationId,
        OwnerDomain,
        OwnerOperationId,
        OwnerFacilityId);

    private static ProductionFacilityDestructiveDrainEntrySaveData
        AdvanceToCheckpointGc(
            ProductionFacilityDestructiveDrainJournal journal,
            ProductionFacilityDestructiveDrainOperationId operationId,
            ProductionFacilityDestructiveDrainEntrySaveData current,
            string lifecycleFingerprint)
    {
        ProductionFacilityDestructiveDrainPhase[] phases =
        {
            ProductionFacilityDestructiveDrainPhase.DrainingParticipants,
            ProductionFacilityDestructiveDrainPhase.AwaitingEmptyVerification,
            ProductionFacilityDestructiveDrainPhase.AwaitingAuthorityRevoke,
            ProductionFacilityDestructiveDrainPhase.AwaitingWorldRemoval,
            ProductionFacilityDestructiveDrainPhase
                .WorldRemovedAwaitingCheckpointGc
        };
        foreach (ProductionFacilityDestructiveDrainPhase phase in phases)
        {
            Require(
                journal.TryAdvance(
                    operationId,
                    current.revision,
                    phase,
                    lifecycleFingerprint,
                    Array.Empty<
                        ProductionFacilityDestructiveDrainParticipantSaveData>(),
                    out ProductionFacilityDestructiveDrainEntrySaveData next,
                    out string failureReason),
                "Could not advance unified mutation fixture to " + phase
                + ": " + failureReason);
            current = next;
        }
        return current;
    }

    private static bool Throws<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
            return false;
        }
        catch (TException)
        {
            return true;
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class AdmissionFixture
    {
        internal AdmissionFixture(
            IFacilityBufferDestinationAdmissionFenceQuery fences)
        {
            Claims = new FacilityBufferDestinationClaimRegistry();
            Occupancy = new RecordingOccupancyQuery();
            Mass = new RecordingMassQuery();
            Admission = new FacilityBufferMassAdmissionService(
                Claims,
                Occupancy,
                Mass,
                fences);
            PublishAuthority();
        }

        internal AdmissionFixture(
            IProductionFacilityDestructiveDrainOpenOperationQuery production)
        {
            Claims = new FacilityBufferDestinationClaimRegistry();
            Occupancy = new RecordingOccupancyQuery();
            Mass = new RecordingMassQuery();
            Admission = new FacilityBufferMassAdmissionService(
                Claims,
                Occupancy,
                Mass,
                production);
            PublishAuthority();
        }

        internal FacilityBufferDestinationClaimRegistry Claims { get; }
        internal RecordingOccupancyQuery Occupancy { get; }
        internal RecordingMassQuery Mass { get; }
        internal FacilityBufferMassAdmissionService Admission { get; }

        internal FacilityBufferMassAdmissionRequest CreateExactRequest(
            string operationId) => new(
            operationId,
            DestinationId,
            DropPosition,
            OwnerDomain,
            OwnerOperationId,
            OwnerFacilityId,
            expectedCapacityRevision: 7L,
            new[]
            {
                new FacilityBufferMassLotSlice(
                    "stack:qa-owner-neutral-fence",
                    quantity: 2,
                    expectedReservationRevision: 1L)
            });

        internal FacilityBufferPlannedOutputRequest CreatePlannedRequest(
            string operationId) => new(
            operationId,
            "qa:owner-neutral-fence:batch",
            "qa:owner-neutral-fence:outcome",
            DestinationId,
            DropPosition,
            OwnerDomain,
            OwnerOperationId,
            OwnerFacilityId,
            expectedCapacityRevision: 7L,
            new[]
            {
                new FacilityBufferPlannedOutputSlice(
                    "output:qa-owner-neutral-fence",
                    PhysicalItemMassSubject.ForDefinition(
                        (ItemDefinitionId)"qa:item:owner-neutral-fence"),
                    quantity: 2)
            });

        private void PublishAuthority()
        {
            FacilityBufferDestinationClaim claim = new(
                DestinationId,
                DropPosition,
                OwnerDomain,
                OwnerOperationId,
                OwnerFacilityId,
                FacilityBufferDestinationAnchorKind.LiveFacility,
                FacilityBufferDestinationAdmissionPolicy.ExactGramRequired);
            FacilityBufferCapacityProfile profile = new(
                DestinationId,
                DropPosition,
                OwnerDomain,
                OwnerOperationId,
                OwnerFacilityId,
                new PhysicalMassGrams(10_000L),
                capacityRevision: 7L);
            Require(
                Claims.TryClaim(claim, out _, out string claimFailure),
                "Admission fence fixture claim failed: " + claimFailure);
            Require(
                Admission.TryReplaceOwnedProfiles(
                    OwnerDomain,
                    new[] { profile },
                    out _,
                    out string profileFailure),
                "Admission fence fixture profile failed: " + profileFailure);
        }
    }

    private sealed class SyntheticFenceSource :
        IFacilityBufferDestinationAdmissionFenceSource
    {
        private readonly string operationId;
        private readonly Func<FacilityBufferDestinationAdmissionFenceSubject,
            bool> matches;

        internal SyntheticFenceSource(
            string sourceId,
            long revision,
            string operationId,
            Func<FacilityBufferDestinationAdmissionFenceSubject, bool> matches)
        {
            SourceId = sourceId;
            Revision = revision;
            this.operationId = operationId;
            this.matches = matches;
        }

        public string SourceId { get; }
        public long Revision { get; }
        internal bool IsOpen { get; set; } = true;

        public bool TryCaptureOpenFence(
            FacilityBufferDestinationAdmissionFenceSubject subject,
            out FacilityBufferDestinationAdmissionFenceSnapshot snapshot)
        {
            snapshot = default;
            if (!IsOpen || matches?.Invoke(subject) != true)
                return false;
            snapshot = new FacilityBufferDestinationAdmissionFenceSnapshot(
                SourceId,
                operationId,
                Revision);
            return true;
        }
    }

    private sealed class FakeProductionOpenOperationQuery :
        IProductionFacilityDestructiveDrainOpenOperationQuery
    {
        private readonly BuildingInstanceId facilityId;
        private readonly long snapshotRevision;

        internal FakeProductionOpenOperationQuery(
            BuildingInstanceId facilityId,
            int revision,
            long snapshotRevision)
        {
            this.facilityId = facilityId;
            Revision = revision;
            this.snapshotRevision = snapshotRevision;
        }

        public int Revision { get; }
        internal bool IsFenceOpen { get; set; }

        public bool IsOpen(BuildingInstanceId candidate) =>
            IsFenceOpen && candidate.Equals(facilityId);

        public bool TryCapture(
            BuildingInstanceId candidate,
            out ProductionFacilityDestructiveDrainOpenOperationSnapshot snapshot)
        {
            snapshot = default;
            if (!IsOpen(candidate))
                return false;
            snapshot = new ProductionFacilityDestructiveDrainOpenOperationSnapshot(
                ProductionFacilityDestructiveDrainOperationId.FromFacility(
                    facilityId),
                facilityId,
                ProductionFacilityDestructiveDrainPhase.Prepared,
                snapshotRevision);
            return true;
        }
    }

    private sealed class RecordingOccupancyQuery :
        IFacilityBufferPhysicalOccupancyQuery
    {
        internal int CaptureCalls { get; private set; }
        internal int ExactLotCaptureCalls { get; private set; }

        public FacilityBufferPhysicalOccupancySnapshot Capture(
            string destinationId)
        {
            CaptureCalls++;
            return new FacilityBufferPhysicalOccupancySnapshot(0L, 0L);
        }

        public bool TryCaptureExactLot(
            IReadOnlyList<FacilityBufferMassLotSlice> slices,
            out FacilityBufferExactLotSnapshot lot,
            out string failureReason)
        {
            ExactLotCaptureCalls++;
            failureReason = string.Empty;
            int quantity = 0;
            foreach (FacilityBufferMassLotSlice slice in
                     slices ?? Array.Empty<FacilityBufferMassLotSlice>())
            {
                quantity = checked(quantity + slice.Quantity);
            }
            lot = new FacilityBufferExactLotSnapshot(
                "qa:owner-neutral-fence:lot:" + quantity,
                new PhysicalMassGrams(checked(quantity * 1_000L)));
            return quantity > 0;
        }

        internal void ResetCounters()
        {
            CaptureCalls = 0;
            ExactLotCaptureCalls = 0;
        }
    }

    private sealed class RecordingMassQuery : IPhysicalItemMassQuery
    {
        public long AuthorityRevision => 5L;
        internal int QueryCalls { get; private set; }

        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId)
        {
            QueryCalls++;
            return new PhysicalMassGrams(1_000L);
        }

        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject)
        {
            QueryCalls++;
            return subject.HasPreparedUnitMass
                ? subject.PreparedUnitMass
                : new PhysicalMassGrams(1_000L);
        }

        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject)
        {
            QueryCalls++;
            return subject.HasPreparedUnitMass
                ? subject.PreparedUnitMass
                : new PhysicalMassGrams(1_000L);
        }

        public PhysicalMassGrams GetStackTotalMass(
            PhysicalItemLotSnapshot lot)
        {
            QueryCalls++;
            return new PhysicalMassGrams(checked(lot.Quantity * 1_000L));
        }

        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity)
        {
            QueryCalls++;
            return new PhysicalMassGrams(checked(quantity * 1_000L));
        }

        internal void ResetCounters() => QueryCalls = 0;
    }
}
#endif
