#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEngine;

public static class CapturedWildlifeFeedOutboxDebugScenarios
{
    private const string WildlifeId = "wildlife:qa-captive-feed";
    private const string ItemId = "food:preserved-ration";

    public static void RunForArchitectureBatch()
    {
        Require(Application.isBatchMode && !Application.isPlaying,
            "Captured feed owner verification requires edit batch execution.");
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        Require(!string.IsNullOrWhiteSpace(output) && Path.IsPathRooted(output),
            "Captured feed owner requires the isolated launcher's evidence directory.");
        string previous = Environment.GetEnvironmentVariable(DungeonPersistencePaths.VerificationRootVariable);
        var errors = new List<string>();
        void Observe(string condition, string stack, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert) errors.Add(condition);
        }
        Environment.SetEnvironmentVariable(DungeonPersistencePaths.VerificationRootVariable,
            Path.Combine(output, "persistent-data"));
        try
        {
            Require(DungeonPersistencePaths.IsIsolatedVerification,
                "Captured feed owner persistence boundary was not established.");
            Application.logMessageReceived += Observe;
            string result = Run();
            Require(errors.Count == 0, "Captured feed owner emitted engine errors: " + string.Join(" | ", errors));
            Debug.Log("CAPTURED_WILDLIFE_FEED_OWNER_BATCH=PASS; " + result);
        }
        finally
        {
            Application.logMessageReceived -= Observe;
            Environment.SetEnvironmentVariable(DungeonPersistencePaths.VerificationRootVariable, previous);
        }
    }

    public static string Run()
    {
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        WorldItemRepository repository = new(
            new GuidPersistentIdGenerator(),
            new DungeonRuntimeAggregateRootStore());
        PhysicalItemBatchDispositionService inner = new(
            repository,
            new PhysicalItemMassQuery(catalog),
            EditorNullItemMarkerPresenter.Instance);
        FailOnceAcknowledgementDisposition disposition = new(inner);
        string stackId = WorldItemRepositoryEditorAccess.AddStack(
            repository,
            ItemId,
            2,
            WorldItemStackState.FacilityBuffer,
            position: new Vector2Int(6, 4),
            destinationId: "building:qa-beast-pen");
        FakeFeedTarget target = new(
            WildlifeId,
            hunger: 0.9f,
            health: 10);
        CapturedWildlifeState state = new()
        {
            wildlifeId = WildlifeId,
            speciesId = "wildlife:wolf",
            penId = "building:qa-beast-pen",
            transportState = CapturedWildlifeTransportState.Penned,
            foodDeliveryPending = true,
            nextFeedOperationSequence = 1
        };

        string firstOperation =
            CapturedWildlifeFeedOutbox.FormatOperationId(WildlifeId, 1);
        Require(inner.TryCommitPending(
                new[] { new PhysicalItemTransformInput(stackId, 1) },
                PhysicalItemDispositionKind.Sink,
                firstOperation,
                CapturedWildlifeFeedOutbox.ReasonCode,
                out PhysicalItemBatchDispositionReceipt firstReceipt,
                out string firstCommitFailure),
            "Could not stage first captive feed: " + firstCommitFailure);
        CapturedWildlifeFeedOutbox.RecordPending(
            state,
            sequence: 1,
            firstReceipt,
            ItemId,
            nutrition: 0.4f,
            diseaseChance: 0f,
            diseaseTriggered: false,
            target);
        disposition.FailNextAcknowledgement = true;
        Require(!CapturedWildlifeFeedOutbox.TryFinalizePending(
                state,
                target,
                disposition,
                out bool firstApplied,
                out _)
            && firstApplied
            && state.pendingFeedPhase
                == CapturedWildlifeFeedCommitPhase.CarePublished
            && Mathf.Approximately(target.Hunger, 0.5f)
            && target.CurrentHealth == 10
            && target.ApplyCount == 1
            && repository.GetEditorPendingBatchDispositionCount() == 1,
            "Acknowledgement failure did not retain one published feed receipt.");

        CapturedWildlifeState roundTrip =
            JsonUtility.FromJson<CapturedWildlifeState>(
                JsonUtility.ToJson(state));
        Require(roundTrip != null
            && CapturedWildlifeFeedOutbox.IsStructurallyValid(roundTrip)
            && roundTrip.pendingFeedSourceStackIds.SequenceEqual(
                new[] { stackId },
                StringComparer.Ordinal),
            "Captured feed V3 provenance did not survive JSON round-trip.");
        CapturedWildlifeState clone = roundTrip.Clone();
        clone.pendingFeedSourceStackIds.Add("stack:alias-probe");
        Require(roundTrip.pendingFeedSourceStackIds.Count == 1,
            "Captured feed clone aliased pending source-stack provenance.");

        CapturedWildlifeState tampered =
            JsonUtility.FromJson<CapturedWildlifeState>(
                JsonUtility.ToJson(roundTrip));
        tampered.pendingFeedCommitId += ":tampered";
        Require(!CapturedWildlifeFeedOutbox.TryFinalizePending(
                tampered,
                target,
                disposition,
                out _,
                out _)
            && target.ApplyCount == 1
            && repository.GetEditorPendingBatchDispositionCount() == 1,
            "Tampered feed provenance mutated domain or physical authority.");

        roundTrip.lastCareStatus = "급식 영수증 대기 표시";
        CapturedWildlifeState acknowledgedCrash = roundTrip.Clone();
        Require(CapturedWildlifeFeedOutbox.TryFinalizePending(
                roundTrip,
                target,
                disposition,
                out bool replayApplied,
                out string firstFinalizeFailure)
            && !replayApplied
            && !CapturedWildlifeFeedOutbox.HasPending(roundTrip)
            && target.ApplyCount == 1
            && repository.GetEditorPendingBatchDispositionCount() == 0
            && repository.GetEditorTestQuantity(stackId) == 1,
            "First feed did not acknowledge exactly once: "
                + firstFinalizeFailure);
        Require(CapturedWildlifeFeedOutbox.TryFinalizePending(
                acknowledgedCrash,
                target,
                disposition,
                out bool acknowledgedReplayApplied,
                out string acknowledgedReplayFailure)
            && !acknowledgedReplayApplied
            && !CapturedWildlifeFeedOutbox.HasPending(acknowledgedCrash)
            && target.ApplyCount == 1,
            "Already-acknowledged feed recovery reapplied care: "
                + acknowledgedReplayFailure);

        roundTrip.nextFeedOperationSequence = 2;
        string secondOperation =
            CapturedWildlifeFeedOutbox.FormatOperationId(WildlifeId, 2);
        Require(!string.Equals(
                firstOperation,
                secondOperation,
                StringComparison.Ordinal),
            "Two captive feeds reused one operation ID.");
        Require(inner.TryCommitPending(
                new[] { new PhysicalItemTransformInput(stackId, 1) },
                PhysicalItemDispositionKind.Sink,
                secondOperation,
                CapturedWildlifeFeedOutbox.ReasonCode,
                out PhysicalItemBatchDispositionReceipt secondReceipt,
                out string secondCommitFailure),
            "Could not stage disease feed: " + secondCommitFailure);
        CapturedWildlifeFeedOutbox.RecordPending(
            roundTrip,
            sequence: 2,
            secondReceipt,
            ItemId,
            nutrition: 0.3f,
            diseaseChance: 0.5f,
            diseaseTriggered: true,
            target);
        Require(CapturedWildlifeFeedOutbox.TryFinalizePending(
                roundTrip,
                target,
                disposition,
                out bool secondApplied,
                out string secondFinalizeFailure)
            && secondApplied
            && Mathf.Approximately(target.Hunger, 0.2f)
            && target.CurrentHealth == 9
            && target.ApplyCount == 2
            && Mathf.Approximately(roundTrip.feedSicknessSeverity, 25f)
            && Mathf.Approximately(roundTrip.lastFeedDiseaseChance, 0.5f)
            && repository.GetEditorPendingBatchDispositionCount() == 0
            && repository.GetEditorTestQuantity(stackId) == 0,
            "Disease feed was not consumed and published exactly once: "
                + secondFinalizeFailure);

        VerifyFeedingOwnerPendingRetry();

        return "two sequence-unique feeds, exact Sink mass/quantity, "
            + "acknowledgement retry, JSON provenance, clone isolation, "
            + "tamper rejection, display-independent ACK and owner care-cycle retry";
    }

    private static void VerifyFeedingOwnerPendingRetry()
    {
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        DungeonRuntimeAggregateRootStore root = new();
        WorldItemRepository repository = new(new GuidPersistentIdGenerator(), root);
        PhysicalItemBatchDispositionService physical = new(repository,
            new PhysicalItemMassQuery(catalog), EditorNullItemMarkerPresenter.Instance);
        FailOnceAcknowledgementDisposition dispositions = new(physical);
        CapturedWildlifeStateSession states = new(root);
        ICharacterAiWorldRegistry world = CharacterAiEditorTestDependencies.WorldRegistry;
        UnityGameClock clock = new();
        GameObject actorObject = new("Captured feeding owner retry");
        WildlifeActor actor = null;
        try
        {
            actor = actorObject.AddComponent<WildlifeActor>();
            actor.ConfigureRuntimeServices(new GridPathSearchBroker(clock,
                    doorAccessQuery: null, performanceRecorder: null, costPolicy: null),
                world, clock, new RandomStreamProvider(612361),
                FeedingPorts.Create<IDoorAccessQuery>());
            actor.Initialize(new Grid(2, 1), WildlifeTestFixtures.CaveRat,
                "wildlife:qa-feeding-owner", Vector2Int.zero,
                new WildlifeSaveData { health = 10, hunger = 0.9f, thirst = 0f,
                    state = WildlifeState.Captured });
            CapturedWildlifeState state = new()
            {
                wildlifeId = actor.WildlifeId, speciesId = actor.SpeciesId,
                penId = "pen:qa-feeding-owner", penPosition = Vector2Int.zero,
                transportState = CapturedWildlifeTransportState.Penned,
                nextFeedOperationSequence = 1, foodDeliveryPending = true
            };
            states.Set(state);
            WildlifeCaptureCareContext care = new(
                FeedingPorts.Create<IWorldItemStackRuntime>(),
                FeedingPorts.Create<IWorldFilthQuery>(),
                FeedingPorts.Create<IResourceEconomyContentCatalog>(),
                FeedingPorts.Create<IWildlifeSpeciesCatalogProvider>(),
                FeedingPorts.Create<IWasteFeedCommand>(),
                FeedingPorts.Create<IWasteFeedCandidateQuery>(), dispositions,
                FeedingPorts.Create<IWildlifeHaulItemRuntime>());
            CapturedWildlifeFeedingRuntime feeding = new(states, world, care,
                new RandomStreamProvider(612362).Get("captivity.wildlife-care"));
            string source = WorldItemRepositoryEditorAccess.AddStack(repository,
                ItemId, 1, WorldItemStackState.FacilityBuffer,
                position: Vector2Int.zero, destinationId: state.penId);
            Require(physical.TryCommitPending(new[] { new PhysicalItemTransformInput(source, 1) },
                PhysicalItemDispositionKind.Sink,
                CapturedWildlifeFeedOutbox.FormatOperationId(state.wildlifeId, 1),
                CapturedWildlifeFeedOutbox.ReasonCode, out var receipt, out string failure),
                "Feeding owner source Sink failed: " + failure);
            CapturedWildlifeFeedOutbox.RecordPending(state, 1, receipt, ItemId,
                0.4f, 0.5f, diseaseTriggered: true, actor);
            bool captureRejected = false;
            dispositions.BeforeAcknowledgement = () =>
            {
                try { feeding.ValidateBeforeCapture(); }
                catch (InvalidOperationException) { captureRejected = true; }
            };
            dispositions.FailNextAcknowledgement = true;
            feeding.SatisfyNeeds(state, actor, 0f, 0f, Vector2Int.zero, state.penId,
                atCarePosition: false, out bool fed, out _);
            Require(!fed && captureRejected
                && state.pendingFeedPhase == CapturedWildlifeFeedCommitPhase.CarePublished
                && Mathf.Approximately(state.feedSicknessSeverity, 25f)
                && actor.CurrentHealth == 9 && Mathf.Approximately(actor.Hunger, 0.5f)
                && repository.GetEditorPendingBatchDispositionCount() == 1,
                "Pending away-from-pen feed did not preserve its exact published outcome.");
            feeding.ValidateBeforeCapture();
            state.lastCareStatus = "일반 돌봄 표시 문구";
            CapturedWildlifeState restored = JsonUtility.FromJson<CapturedWildlifeState>(
                JsonUtility.ToJson(state));
            states.Set(restored);
            dispositions.FailNextAcknowledgement = true;
            bool reconciliationRejected = false;
            try { feeding.ReconcilePendingFeedsOrThrow(); }
            catch (InvalidOperationException exception)
                when (exception.Message.Contains("qa-injected-feed-acknowledgement-failure",
                    StringComparison.Ordinal)) { reconciliationRejected = true; }
            Require(reconciliationRejected
                && restored.pendingFeedPhase == CapturedWildlifeFeedCommitPhase.CarePublished
                && Mathf.Approximately(restored.feedSicknessSeverity, 25f),
                "Restore reconciliation lost pending receipt or changed published sickness.");
            feeding.ValidateBeforeCapture();
            feeding.SatisfyNeeds(restored, actor, 0f, 0f, Vector2Int.zero, restored.penId,
                atCarePosition: false, out fed, out _);
            Require(fed && !CapturedWildlifeFeedOutbox.HasPending(restored)
                && CapturedWildlifeFeedOutbox.HasPending(state)
                && actor.CurrentHealth == 9 && Mathf.Approximately(actor.Hunger, 0.5f)
                && Mathf.Approximately(restored.feedSicknessSeverity, 23.5f)
                && repository.GetEditorPendingBatchDispositionCount() == 0
                && repository.GetEditorTestQuantity(source) == 0,
                "Owner retry duplicated care, changed retired state, or lost exact ACK.");
            feeding.SatisfyNeeds(restored, actor, 0f, 0f, Vector2Int.zero, restored.penId,
                atCarePosition: false, out _, out _);
            Require(Mathf.Approximately(restored.feedSicknessSeverity, 22f)
                && actor.CurrentHealth == 9,
                "Normal sickness decay changed after pending feed completed.");
        }
        finally
        {
            if (actor != null) world.UnregisterWildlife(actor);
            UnityEngine.Object.DestroyImmediate(actorObject);
        }
    }

    public class FeedingPorts : DispatchProxy
    {
        internal static T Create<T>() where T : class => DispatchProxy.Create<T, FeedingPorts>();
        protected override object Invoke(MethodInfo method, object[] args)
        {
            if (method.Name == nameof(IWorldItemStackRuntime.GetAllStacks))
                return Array.Empty<WorldItemStackSnapshot>();
            throw new InvalidOperationException("Unexpected pending-feed fixture call: " + method.Name);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FakeFeedTarget : ICapturedWildlifeFeedOutcomeTarget
    {
        internal FakeFeedTarget(
            string wildlifeId,
            float hunger,
            int health)
        {
            WildlifeId = wildlifeId;
            Hunger = hunger;
            CurrentHealth = health;
        }

        public string WildlifeId { get; }
        public float Hunger { get; private set; }
        public int CurrentHealth { get; private set; }
        public string LastCaptiveFeedCommitId { get; private set; } =
            string.Empty;
        internal int ApplyCount { get; private set; }

        public bool TryApplyCaptiveFeedOutcome(
            string commitId,
            float hungerTarget,
            int healthTarget,
            out bool applied)
        {
            applied = false;
            if (string.IsNullOrWhiteSpace(commitId)
                || hungerTarget is < 0f or > 1f
                || healthTarget < 0)
            {
                return false;
            }
            if (string.Equals(
                    LastCaptiveFeedCommitId,
                    commitId,
                    StringComparison.Ordinal))
            {
                return true;
            }

            Hunger = hungerTarget;
            CurrentHealth = Math.Min(CurrentHealth, healthTarget);
            LastCaptiveFeedCommitId = commitId;
            ApplyCount++;
            applied = true;
            return true;
        }
    }

    private sealed class FailOnceAcknowledgementDisposition :
        IPhysicalItemBatchDispositionService
    {
        private readonly IPhysicalItemBatchDispositionService inner;

        internal FailOnceAcknowledgementDisposition(
            IPhysicalItemBatchDispositionService inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        internal bool FailNextAcknowledgement { get; set; }
        internal Action BeforeAcknowledgement;

        public bool TryCommit(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => inner.TryCommit(
                inputs,
                kind,
                operationId,
                reasonCode,
                out receipt,
                out failureReason);

        public bool TryCommitPending(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => inner.TryCommitPending(
                inputs,
                kind,
                operationId,
                reasonCode,
                out receipt,
                out failureReason);

        public bool Acknowledge(
            string commitId,
            out string failureReason)
        {
            BeforeAcknowledgement?.Invoke();
            if (FailNextAcknowledgement)
            {
                FailNextAcknowledgement = false;
                failureReason = "qa-injected-feed-acknowledgement-failure";
                return false;
            }
            return inner.Acknowledge(commitId, out failureReason);
        }

        public bool TryGetPending(
            string operationId,
            out PhysicalItemBatchDispositionReceipt receipt) =>
            inner.TryGetPending(operationId, out receipt);
    }
}
#endif
