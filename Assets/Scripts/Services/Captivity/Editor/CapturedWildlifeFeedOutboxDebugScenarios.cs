#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class CapturedWildlifeFeedOutboxDebugScenarios
{
    private const string WildlifeId = "wildlife:qa-captive-feed";
    private const string ItemId = "food:preserved-ration";

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

        return "two sequence-unique feeds, exact Sink mass/quantity, "
            + "acknowledgement retry, JSON provenance, clone isolation, "
            + "tamper rejection and once-resolved disease outcome";
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
