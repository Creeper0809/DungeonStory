#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class WildlifeFoodRaidOutboxDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Wildlife/Run Food Raid Outbox Focused")]
    public static void RunFocused()
    {
        string details = VerifyPendingDispositionOutbox();
        Debug.Log("Wildlife food raid outbox PASS. " + details);
    }

    internal static string VerifyPendingDispositionOutbox()
    {
        WorldItemStackRuntime items = null;
        try
        {
            items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                out WorldItemRepository repository,
                out _,
                out _,
                out _);
            PhysicalItemBatchDispositionService inner = new(
                repository,
                new PhysicalItemMassQuery(items.CatalogProvider),
                EditorNullItemMarkerPresenter.Instance);
            FailOnceAcknowledgementDisposition disposition = new(inner)
            {
                FailNextAcknowledgement = true
            };
            WildlifeFoodRaidDispositionOutbox outbox = new(disposition);
            string createdStackId = WorldItemRepositoryEditorAccess.AddStack(
                repository,
                "food:preserved-ration",
                1,
                WorldItemStackState.Loose,
                position: new Vector2Int(2, 1));
            Require(!string.IsNullOrWhiteSpace(createdStackId),
                "food-raid source stack was not created");
            WorldItemStackSnapshot target = items.GetAllStacks()
                .Single(value => value.StackId == createdStackId);
            WildlifeFoodRaidOrderSaveData order = CreateOrder(
                "raid:qa-outbox",
                "wild:9101",
                createdStackId);

            Require(!outbox.TryCommit(order, target, out string forcedFailure)
                    && forcedFailure == "qa-forced-food-raid-acknowledgement-failure",
                $"forced acknowledgement did not leave a pending order: {forcedFailure}");
            Require(order.state
                    == WildlifeFoodRaidOrderState.WaitingForDispositionFinalization
                    && order.commitPhase == WildlifeFoodRaidCommitPhase.RaidPublished
                    && order.stolenQuantity == 1
                    && order.dispositionQuantity == 1
                    && order.dispositionInputMassGrams > 0L
                    && !items.GetAllStacks().Any(value => value.StackId == createdStackId)
                    && disposition.TryGetPending(order.dispositionOperationId, out _),
                "food-raid pending receipt/domain phase was not exact");
            string operationId = order.dispositionOperationId;
            Require(outbox.TryResume(order, out string retryFailure),
                $"food-raid pending retry failed: {retryFailure}");
            Require(order.state == WildlifeFoodRaidOrderState.Leaving
                    && order.commitPhase == WildlifeFoodRaidCommitPhase.None
                    && order.stolenQuantity == 1
                    && !disposition.TryGetPending(operationId, out _)
                    && items.GetAllStacks().Count == 0,
                "food-raid retry consumed twice or retained pending authority");

            string secondStackId = WorldItemRepositoryEditorAccess.AddStack(
                repository,
                "food:preserved-ration",
                1,
                WorldItemStackState.Loose,
                position: new Vector2Int(3, 1));
            WorldItemStackSnapshot secondTarget = items.GetAllStacks()
                .Single(value => value.StackId == secondStackId);
            WildlifeFoodRaidOrderSaveData second = CreateOrder(
                "raid:qa-outbox",
                "wild:9102",
                secondStackId);
            disposition.FailNextAcknowledgement = true;
            Require(!outbox.TryCommit(second, secondTarget, out _),
                "second food-raid fixture unexpectedly acknowledged");
            string secondOperation = second.dispositionOperationId;
            string secondCommit = second.dispositionCommitId;
            WildlifeFoodRaidOrderSaveData tampered = Clone(second);
            tampered.dispositionCommitId += ":tampered";
            Require(!outbox.TryValidatePending(tampered, out _, out string tamperFailure)
                    && tamperFailure == "wildlife-food-raid-pending-receipt-mismatch"
                    && disposition.TryGetPending(secondOperation, out _)
                    && second.dispositionCommitId == secondCommit
                    && second.commitPhase == WildlifeFoodRaidCommitPhase.RaidPublished,
                "food-raid tamper changed or lost live receipt authority");
            Require(outbox.TryResume(second, out string secondRetryFailure),
                $"second food-raid retry failed: {secondRetryFailure}");
            Require(items.GetAllStacks().Count == 0
                    && second.stolenQuantity == 1
                    && second.commitPhase == WildlifeFoodRaidCommitPhase.None
                    && !disposition.TryGetPending(secondOperation, out _),
                "second food-raid did not finalize exactly once");

            Require(operationId != secondOperation,
                "two wildlife actors in one raid shared an operation ID");
            return $"V27_WILDLIFE_FOOD_RAID_PENDING_OUTBOX=PASS; "
                + $"operations={operationId},{secondOperation}; quantity=1+1; tamper=reject";
        }
        finally
        {
            items?.Dispose();
        }
    }

    private static WildlifeFoodRaidOrderSaveData CreateOrder(
        string raidId,
        string wildlifeId,
        string stackId) => new()
    {
        raidId = raidId,
        wildlifeId = wildlifeId,
        targetStackId = stackId,
        state = WildlifeFoodRaidOrderState.Approaching
    };

    private static WildlifeFoodRaidOrderSaveData Clone(
        WildlifeFoodRaidOrderSaveData source) =>
        JsonUtility.FromJson<WildlifeFoodRaidOrderSaveData>(
            JsonUtility.ToJson(source));

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class FailOnceAcknowledgementDisposition :
        IPhysicalItemBatchDispositionService
    {
        private readonly IPhysicalItemBatchDispositionService inner;

        public FailOnceAcknowledgementDisposition(
            IPhysicalItemBatchDispositionService inner) =>
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));

        public bool FailNextAcknowledgement { get; set; }

        public bool TryCommit(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => inner.TryCommit(
                inputs, kind, operationId, reasonCode, out receipt, out failureReason);

        public bool TryCommitPending(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => inner.TryCommitPending(
                inputs, kind, operationId, reasonCode, out receipt, out failureReason);

        public bool Acknowledge(string commitId, out string failureReason)
        {
            if (FailNextAcknowledgement)
            {
                FailNextAcknowledgement = false;
                failureReason = "qa-forced-food-raid-acknowledgement-failure";
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
