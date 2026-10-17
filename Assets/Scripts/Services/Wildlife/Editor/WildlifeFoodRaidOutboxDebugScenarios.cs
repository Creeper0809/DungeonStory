#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

public static class WildlifeFoodRaidOutboxDebugScenarios
{
    private const string PhysicalProducer =
        "trade-inventory.physical-item-batch-disposition-receipt";

    [MenuItem("DungeonStory/Debug/Wildlife/Run Food Raid Outbox Focused")]
    public static void RunFocused()
    {
        string details = VerifyPendingDispositionOutbox();
        Debug.Log("Wildlife food raid outbox PASS. " + details);
    }

    public static void RunObserverFocusedForBatchMode()
    {
        _ = RunE04Focused();
        Debug.Log(
            "B76_WILDLIFE_FOOD_RAID_OBSERVER_FOCUSED=PASS; "
            + "food=1; child=1; parent=1; freshRestore=true; "
            + "boundaries=success,behavior,item-stage-reject,"
            + "parent-response-loss-before-and-after,physical-ack-loss,cancel-after-item,"
            + "pre-theft-failures,none-roundtrip");
    }

    internal static string VerifyPendingDispositionOutbox()
    {
        _ = RunE04Focused();
        return "B76_WILDLIFE_FOOD_RAID_OBSERVER_FOCUSED=PASS; "
            + "V28_WILDLIFE_FOOD_RAID_CAUSAL_OUTBOX=PASS; "
            + "success=child1+parent1; itemCommittedRestore=once; "
            + "durableUnknownRestore=once; raidPublishedRestore=once; "
            + "consumedThenCancelled=stolen; "
            + "preTheftFailures=zero";
    }

    public static JObject RunE04Focused()
    {
        JObject approachingRoundTrip = VerifyApproachingRoundTripShape();
        JObject causalSuccess = VerifySuccessfulCausalRaid();
        JObject liveSuccess = VerifyActualBehaviorEntrySuccess();
        JObject itemCommittedRestore = VerifyItemCommittedRestore();
        JObject durableUnknownRestore = VerifyDurableUnknownRestore();
        JObject beforeDurableRestore = VerifyDurableUnknownRestore(beforeCommit: true);
        JObject raidPublishedRestore = VerifyRaidPublishedRestore();
        JObject consumedThenCancelled = VerifyConsumedThenCancelled();
        JArray preTheftFailures = VerifyPreTheftFailuresPublishNothing();
        return new JObject
        {
            ["passed"] = true,
            ["rawEvidence"] = new JObject
            {
                ["approachingRoundTrip"] = approachingRoundTrip,
                ["causalSuccess"] = causalSuccess,
                ["publicBehaviorSuccess"] = liveSuccess,
                ["itemCommittedRestore"] = itemCommittedRestore,
                ["durableUnknownRestore"] = durableUnknownRestore,
                ["beforeDurableRestore"] = beforeDurableRestore,
                ["raidPublishedRestore"] = raidPublishedRestore,
                ["consumedThenCancelled"] = consumedThenCancelled,
                ["preTheftFailures"] = preTheftFailures
            },
            ["fixtureScope"] = "production wildlife behavior/outbox/physical/ledger owners; controlled actor/grid/clock/preparation/faults; fresh JsonUtility owner, physical-item, and ledger reconstruction with canonical join validation; not Brain, whole-world, process restart, or natural training examples",
            ["branches"] = new JArray
            {
                new JObject
                {
                    ["id"] = "theft-success",
                    ["passed"] = true,
                    ["actorId"] = liveSuccess["actorId"],
                    ["targetStackId"] = liveSuccess["targetStackId"],
                    ["stolenQuantity"] = liveSuccess["stolenQuantity"],
                    ["assertion"] = "the public behavior path consumes one target item and records the exact actor, target, quantity, physical child, and terminal parent"
                },
                new JObject
                {
                    ["id"] = "consumed-then-cancelled",
                    ["passed"] = true,
                    ["terminalState"] = consumedThenCancelled["terminalState"],
                    ["physicalChildCount"] = consumedThenCancelled["physicalChildCount"],
                    ["parentCount"] = consumedThenCancelled["parentCount"],
                    ["assertion"] = "cancellation after physical consumption finalizes the pending owner as Stolen exactly once and never rewrites it as Cancelled"
                },
                new JObject
                {
                    ["id"] = "unchanged-discovery-failure-vs-gameplay-result",
                    ["passed"] = true,
                    ["noTargetOutcomeCount"] = 0,
                    ["pathFailureOutcomeCount"] = 0,
                    ["preTheftDeathOutcomeCount"] = 0,
                    ["assertion"] = "no-target, no-path, and pre-theft death remain pre-commit failures with no physical child, parent result, or pending disposition"
                }
            }
        };
    }

    private static JObject VerifyApproachingRoundTripShape()
    {
        WildlifeFoodRaidOrderSaveData order = CreateOrder(
            "raid:qa-approaching-roundtrip",
            "wildlife:9100",
            null,
            new Vector2Int(0, 1),
            new Vector2Int(4, 1));
        Require(WildlifeFoodRaidDispositionOutbox.HasValidShape(order),
            "Fresh Approaching food-raid order shape is invalid.");

        string json = JsonUtility.ToJson(order);
        WildlifeFoodRaidOrderSaveData restored =
            JsonUtility.FromJson<WildlifeFoodRaidOrderSaveData>(json);
        Require(restored.fact?.IsEmpty == true
                && WildlifeFoodRaidDispositionOutbox.HasValidShape(restored),
            "Approaching food-raid order lost its empty fact shape in JsonUtility round-trip.");
        return new JObject
        {
            ["owner"] = JObject.Parse(json),
            ["restoredFactIsEmpty"] = restored.fact?.IsEmpty == true,
            ["shapeValid"] = true
        };
    }

    private static JObject VerifySuccessfulCausalRaid()
    {
        using Fixture fixture = new("run:wildlife-food-raid-success");
        WorldItemStackSnapshot target = fixture.AddFood(new Vector2Int(4, 2));
        WildlifeFoodRaidOrderSaveData order = CreateOrder(
            "raid:qa-success",
            "wildlife:9101",
            target,
            new Vector2Int(0, 2),
            new Vector2Int(4, 2));

        Require(fixture.Outbox.TryCommit(order, target, out string failure),
            "Successful food raid failed: " + failure);
        Require(order.commitPhase == WildlifeFoodRaidCommitPhase.Acknowledged
                && order.state == WildlifeFoodRaidOrderState.Leaving
                && order.stolenQuantity == 1
                && fixture.Items.GetAllStacks().Count == 0
                && fixture.Repository.GetEditorPendingBatchDispositionCount() == 0,
            "Successful food raid did not finish its physical/parent outbox exactly once.");
        VerifyExactlyOneChildAndParent(fixture, order);
        JObject committed = CaptureEvidence(fixture, order);
        Require(fixture.Outbox.TryResume(order, out string replayFailure)
                && fixture.Items.GetAllStacks().Count == 0,
            "Acknowledged replay was not idempotent: " + replayFailure);
        VerifyExactlyOneChildAndParent(fixture, order);
        return new JObject
        {
            ["committed"] = committed,
            ["replayed"] = CaptureEvidence(fixture, order)
        };
    }

    private static JObject VerifyActualBehaviorEntrySuccess()
    {
        using Fixture fixture = new("run:wildlife-food-raid-behavior-success");
        using BehaviorHarness behavior = new(
            fixture,
            hasTarget: true,
            spawnAtTarget: true);
        Require(behavior.Runtime.TryBeginFoodRaid(
                "raid:qa-behavior-success",
                1,
                out _,
                out string beginFailure),
            "Actual food-raid entry did not begin: " + beginFailure);
        WildlifeActor actor = behavior.Actors.Single();
        Require(behavior.Runtime.TickFoodRaid(actor, behavior.Grid, 100f),
            "Actual food-raid behavior tick did not own the raid actor.");
        WildlifeFoodRaidOrderSaveData order = behavior.Orders.Single();
        Require(order.commitPhase == WildlifeFoodRaidCommitPhase.Acknowledged
                && order.state == WildlifeFoodRaidOrderState.Leaving,
            "Actual behavior entry did not reach acknowledged leaving state.");
        return VerifyExactlyOneChildAndParent(fixture, order);
    }

    private static JObject VerifyItemCommittedRestore()
    {
        using Fixture fixture = new("run:wildlife-food-raid-item-restore");
        WorldItemStackSnapshot target = fixture.AddFood(new Vector2Int(5, 3));
        WildlifeFoodRaidOrderSaveData order = CreateOrder(
            "raid:qa-item-restore",
            "wildlife:9102",
            target,
            new Vector2Int(0, 3),
            new Vector2Int(5, 3));
        fixture.FaultingFacts.RejectNextStage = true;

        Require(!fixture.Outbox.TryCommit(order, target, out string failure)
                && failure == "qa-wildlife-food-raid-parent-prepare-rejected"
                && order.commitPhase == WildlifeFoodRaidCommitPhase.ItemCommitted
                && order.state
                    == WildlifeFoodRaidOrderState.WaitingForDispositionFinalization
                && fixture.Items.GetAllStacks().Count == 0
                && fixture.Repository.GetEditorPendingBatchDispositionCount() == 1
                && fixture.CountExact(PhysicalProducer) == 1
                && fixture.CountExact(
                    EnvironmentOutcomeIds.WildlifeFoodRaidProducer) == 0,
            "Parent prepare rejection did not preserve ItemCommitted: " + failure);

        JObject interrupted = CaptureEvidence(fixture, order);
        using Fixture restoredFixture = fixture.RestoreFresh(
            order,
            out WildlifeFoodRaidOrderSaveData restoredOrder);
        order = restoredOrder;
        JObject restored = CaptureEvidence(restoredFixture, order);
        Require(restoredFixture.Outbox.TryResume(order, out string resumeFailure),
            "ItemCommitted restore failed: " + resumeFailure);
        Require(order.commitPhase == WildlifeFoodRaidCommitPhase.Acknowledged
                && restoredFixture.Items.GetAllStacks().Count == 0
                && restoredFixture.Repository.GetEditorPendingBatchDispositionCount() == 0,
            "ItemCommitted restore repeated or lost physical theft.");
        VerifyExactlyOneChildAndParent(restoredFixture, order);
        return new JObject
        {
            ["interrupted"] = interrupted,
            ["restored"] = restored,
            ["terminal"] = CaptureEvidence(restoredFixture, order)
        };
    }

    private static JObject VerifyDurableUnknownRestore(bool beforeCommit = false)
    {
        using Fixture fixture = new("run:wildlife-food-raid-durable-unknown");
        WorldItemStackSnapshot target = fixture.AddFood(new Vector2Int(7, 5));
        WildlifeFoodRaidOrderSaveData order = CreateOrder(
            "raid:qa-durable-unknown",
            "wildlife:9104",
            target,
            new Vector2Int(0, 5),
            new Vector2Int(7, 5));
        fixture.FaultingFacts.HideNextCommitAcknowledgement = true;
        fixture.FaultingFacts.SkipNextCommit = beforeCommit;

        Require(!fixture.Outbox.TryCommit(order, target, out string failure)
                && failure == "qa-wildlife-food-raid-parent-response-lost"
                && order.commitPhase == WildlifeFoodRaidCommitPhase.RaidPublished
                && !string.IsNullOrWhiteSpace(order.publicationKey)
                && fixture.CountExact(PhysicalProducer) == 1
                && fixture.CountExact(
                    EnvironmentOutcomeIds.WildlifeFoodRaidProducer) == (beforeCommit ? 0 : 1)
                && fixture.Repository.GetEditorPendingBatchDispositionCount() == 1,
            "Durable parent response loss did not retain one exact pending owner: "
            + failure);
        string frozenFactJson = JsonUtility.ToJson(order.fact);
        JObject interrupted = CaptureEvidence(fixture, order);

        using Fixture restoredFixture = fixture.RestoreFresh(
            order,
            out WildlifeFoodRaidOrderSaveData restoredOrder);
        order = restoredOrder;
        JObject restored = CaptureEvidence(restoredFixture, order);
        Require(restoredFixture.Outbox.TryResume(order, out string resumeFailure),
            "Durable-unknown restore failed: " + resumeFailure);
        Require(order.commitPhase == WildlifeFoodRaidCommitPhase.Acknowledged
                && JsonUtility.ToJson(order.fact) == frozenFactJson
                && restoredFixture.Repository.GetEditorPendingBatchDispositionCount() == 0,
            "Durable-unknown replay changed its frozen fact or physical owner.");
        VerifyExactlyOneChildAndParent(restoredFixture, order);
        return new JObject
        {
            ["interrupted"] = interrupted,
            ["restored"] = restored,
            ["terminal"] = CaptureEvidence(restoredFixture, order)
        };
    }

    private static JObject VerifyRaidPublishedRestore()
    {
        using Fixture fixture = new("run:wildlife-food-raid-parent-restore");
        WorldItemStackSnapshot target = fixture.AddFood(new Vector2Int(6, 4));
        WildlifeFoodRaidOrderSaveData order = CreateOrder(
            "raid:qa-parent-restore",
            "wildlife:9103",
            target,
            new Vector2Int(0, 4),
            new Vector2Int(6, 4));
        fixture.Dispositions.FailNextAcknowledgement = true;

        Require(!fixture.Outbox.TryCommit(order, target, out string failure)
                && failure == "qa-forced-food-raid-acknowledgement-failure"
                && order.commitPhase == WildlifeFoodRaidCommitPhase.RaidPublished
                && fixture.Items.GetAllStacks().Count == 0
                && fixture.Repository.GetEditorPendingBatchDispositionCount() == 1,
            "Acknowledgement fault did not preserve RaidPublished: " + failure);
        VerifyExactlyOneChildAndParent(fixture, order);

        JObject interrupted = CaptureEvidence(fixture, order);
        using Fixture restoredFixture = fixture.RestoreFresh(
            order,
            out WildlifeFoodRaidOrderSaveData restoredOrder);
        order = restoredOrder;
        JObject restored = CaptureEvidence(restoredFixture, order);
        Require(restoredFixture.Outbox.TryResume(order, out string resumeFailure),
            "RaidPublished restore failed: " + resumeFailure);
        Require(order.commitPhase == WildlifeFoodRaidCommitPhase.Acknowledged
                && restoredFixture.Items.GetAllStacks().Count == 0
                && restoredFixture.Repository.GetEditorPendingBatchDispositionCount() == 0,
            "RaidPublished restore repeated or lost physical theft.");
        VerifyExactlyOneChildAndParent(restoredFixture, order);
        return new JObject
        {
            ["interrupted"] = interrupted,
            ["restored"] = restored,
            ["terminal"] = CaptureEvidence(restoredFixture, order)
        };
    }

    private static JObject VerifyConsumedThenCancelled()
    {
        using Fixture fixture = new("run:wildlife-food-raid-consumed-cancelled");
        using BehaviorHarness behavior = new(
            fixture,
            hasTarget: true,
            spawnAtTarget: true);
        Require(behavior.Runtime.TryBeginFoodRaid(
                "raid:qa-consumed-cancelled",
                1,
                out _,
                out string beginFailure),
            "Consumed-then-cancelled raid did not begin: " + beginFailure);
        fixture.FaultingFacts.RejectNextStage = true;
        WildlifeActor actor = behavior.Actors.Single();
        Require(behavior.Runtime.TickFoodRaid(actor, behavior.Grid, 100f),
            "Consumed-then-cancelled raid tick did not own the actor.");
        WildlifeFoodRaidOrderSaveData order = behavior.Orders.Single();
        Require(order.commitPhase == WildlifeFoodRaidCommitPhase.ItemCommitted
                && order.state
                    == WildlifeFoodRaidOrderState.WaitingForDispositionFinalization
                && order.stolenQuantity == 1
                && fixture.Items.GetAllStacks().Count == 0
                && fixture.Repository.GetEditorPendingBatchDispositionCount() == 1
                && fixture.CountExact(PhysicalProducer) == 1
                && fixture.CountExact(
                    EnvironmentOutcomeIds.WildlifeFoodRaidProducer) == 0,
            "Injected parent rejection did not stop after physical consumption.");

        JObject interrupted = CaptureEvidence(fixture, order);
        behavior.Runtime.CancelFoodRaidForActor(
            actor.WildlifeId,
            "qa cancellation after physical consumption");
        Require(order.commitPhase == WildlifeFoodRaidCommitPhase.Acknowledged
                && order.state == WildlifeFoodRaidOrderState.Stolen
                && order.stolenQuantity == 1
                && fixture.Items.GetAllStacks().Count == 0
                && fixture.Repository.GetEditorPendingBatchDispositionCount() == 0,
            "Cancellation after physical consumption did not finalize as Stolen.");
        JObject terminal = CaptureEvidence(fixture, order);
        behavior.Runtime.CancelFoodRaidForActor(
            actor.WildlifeId,
            "qa duplicate cancellation after terminal state");
        JObject evidence = VerifyExactlyOneChildAndParent(fixture, order);
        evidence["terminalState"] = order.state.ToString();
        evidence["physicalChildCount"] = fixture.CountExact(PhysicalProducer);
        evidence["parentCount"] = fixture.CountExact(
            EnvironmentOutcomeIds.WildlifeFoodRaidProducer);
        evidence["interrupted"] = interrupted;
        evidence["terminal"] = terminal;
        return evidence;
    }

    private static JArray VerifyPreTheftFailuresPublishNothing()
    {
        return new JArray
        {
            VerifyActualNoTargetFailure(),
            VerifyActualPathFailure(),
            VerifyActualPreTheftDeath()
        };
    }

    private static JObject VerifyActualNoTargetFailure()
    {
        using Fixture fixture = new("run:wildlife-food-raid-no-target");
        using BehaviorHarness behavior = new(
            fixture,
            hasTarget: false,
            spawnAtTarget: false);
        Require(behavior.Runtime.TryBeginFoodRaid(
                "raid:qa-no-target", 1, out _, out _),
            "No-target raid did not create its pre-theft actor/order.");
        Require(behavior.Runtime.TickFoodRaid(
                behavior.Actors.Single(), behavior.Grid, 1000f),
            "No-target raid tick was not handled.");
        WildlifeFoodRaidOrderSaveData order = behavior.Orders.Single();
        Require(order.state == WildlifeFoodRaidOrderState.Failed
                && order.commitPhase == WildlifeFoodRaidCommitPhase.None,
            "No-target raid crossed the theft commit point.");
        RequireZeroOutcomes(fixture, "no-target");
        return CaptureEvidence(fixture, order);
    }

    private static JObject VerifyActualPathFailure()
    {
        using Fixture fixture = new("run:wildlife-food-raid-no-path");
        using BehaviorHarness behavior = new(
            fixture,
            hasTarget: true,
            spawnAtTarget: false);
        Require(behavior.Runtime.TryBeginFoodRaid(
                "raid:qa-no-path", 1, out _, out _),
            "Path-failure raid did not begin while the route was available.");
        behavior.Grid.GetGridCell(new Vector2Int(2, 0))
            .SetAreaType(GridCellAreaType.BlockedExterior);
        Require(behavior.Runtime.TickFoodRaid(
                behavior.Actors.Single(), behavior.Grid, 1000f),
            "Path-failure raid tick was not handled.");
        WildlifeFoodRaidOrderSaveData order = behavior.Orders.Single();
        Require(order.state == WildlifeFoodRaidOrderState.Failed
                && order.commitPhase == WildlifeFoodRaidCommitPhase.None,
            "Path failure crossed the theft commit point: state="
            + order.state + "; phase=" + order.commitPhase
            + "; reason=" + order.outcomeReason
            + "; actor=" + behavior.Actors.Single().GridPosition
            + "; target=" + order.targetStackId + ".");
        RequireZeroOutcomes(fixture, "path-failure");
        return CaptureEvidence(fixture, order);
    }

    private static JObject VerifyActualPreTheftDeath()
    {
        using Fixture fixture = new("run:wildlife-food-raid-actor-death");
        using BehaviorHarness behavior = new(
            fixture,
            hasTarget: true,
            spawnAtTarget: false);
        Require(behavior.Runtime.TryBeginFoodRaid(
                "raid:qa-actor-death", 1, out _, out _),
            "Actor-death raid did not begin.");
        WildlifeActor actor = behavior.Actors.Single();
        actor.ApplyDamage(actor.MaxHealth, null);
        Require(behavior.Runtime.TickFoodRaid(actor, behavior.Grid, 100f),
            "Dead raid actor was not terminally handled.");
        WildlifeFoodRaidOrderSaveData order = behavior.Orders.Single();
        Require(order.state == WildlifeFoodRaidOrderState.Cancelled
                && order.commitPhase == WildlifeFoodRaidCommitPhase.None,
            "Pre-theft actor death crossed the theft commit point.");
        RequireZeroOutcomes(fixture, "pre-theft-actor-death");
        return CaptureEvidence(fixture, order);
    }

    private static void RequireZeroOutcomes(Fixture fixture, string branch)
    {
        GameplayOutcomeLedgerSaveData save =
            fixture.Ledger.CaptureGameplayOutcomes();
        Require(save.exactOutcomes.Count == 0
                && save.outbox.Count == 0
                && fixture.Repository.GetEditorPendingBatchDispositionCount()
                    == 0,
            branch + " published a gameplay record.");
    }

    private static JObject VerifyExactlyOneChildAndParent(
        Fixture fixture,
        WildlifeFoodRaidOrderSaveData order)
    {
        fixture.ValidateCanonical(order);
        GameplayOutcomeSnapshot[] exact = fixture.Ledger
            .CaptureGameplayOutcomes().exactOutcomes.ToArray();
        GameplayOutcomeSnapshot child = exact.Single(value =>
            value.producerId == PhysicalProducer);
        GameplayOutcomeSnapshot parent = exact.Single(value =>
            value.producerId
                == EnvironmentOutcomeIds.WildlifeFoodRaidProducer);
        string expectedChildKey = new GameplayResultKey(
            PhysicalProducer,
            new GameplayOperationId(order.dispositionOperationId),
            order.dispositionOwnerRevision,
            0).ToString();
        string expectedParentKey = new GameplayResultKey(
            parent.producerId,
            new GameplayOperationId(parent.operationId),
            parent.commitRevision,
            parent.localResultIndex).ToString();
        string expectedParentId = new GameplayOutcomeId(
            new GameplayOutcomeRunId(parent.runId),
            parent.sequence).ToString();
        string expectedContextHash = WildlifeFoodRaidFactMapping
            .ComputeContextSha256(order.fact);
        Require(exact.Length == 2
                && parent.outcomeTypeId
                    == EnvironmentOutcomeIds.WildlifeFoodRaidCompleted.Value
                && parent.parentRunId == child.runId
                && parent.parentSequence == child.sequence
                && parent.rootOperationId == order.dispositionOperationId
                && parent.causationRelationId == "physical-theft-consumption"
                && parent.facts.Any(value =>
                    value.factId
                        == EnvironmentOutcomeIds.ChildResultKeyFact.Value
                    && value.value == expectedChildKey)
                && parent.participants.Any(value =>
                    value.entityKindId
                        == EnvironmentOutcomeIds.WildlifeKind.Value
                    && value.entityId == order.wildlifeId
                    && value.roleId == EnvironmentOutcomeIds.ActorRole.Value)
                && parent.participants.Any(value =>
                    value.entityKindId
                        == EnvironmentOutcomeIds.ItemStackKind.Value
                    && value.entityId == order.targetStackId
                    && value.roleId == EnvironmentOutcomeIds.TargetRole.Value)
                && parent.subjects.Any(value =>
                    value.entityKindId
                        == EnvironmentOutcomeIds.WildlifeKind.Value
                    && value.entityId == order.wildlifeId)
                && parent.subjects.Any(value =>
                    value.entityKindId
                        == EnvironmentOutcomeIds.ItemStackKind.Value
                    && value.entityId == order.targetStackId)
                && parent.metrics.Any(value =>
                    value.metricId
                        == EnvironmentOutcomeIds.StolenQuantityMetric.Value
                    && Math.Abs(value.value - order.stolenQuantity) < 0.0001d
                    && value.unitId == EnvironmentOutcomeIds.CountUnit.Value
                    && value.referenceKindId
                        == EnvironmentOutcomeIds.ItemStackKind.Value
                    && value.referenceId == order.targetStackId)
                && parent.facts.Any(value =>
                    value.factId
                        == EnvironmentOutcomeIds.TerminalStateFact.Value
                    && value.value
                        == WildlifeFoodRaidOrderState.Stolen.ToString())
                && parent.facts.Any(value =>
                    value.factId == "wildlife-food-raid-context-sha256"
                    && value.value == expectedContextHash)
                && order.publicationKey == expectedParentKey
                && order.publicationId == expectedParentId
                && order.publicationFingerprint == parent.immutablePayloadHash,
            "Food-raid parent lost exact actor/target/quantity/child identity.");
        return new JObject
        {
            ["actorId"] = order.wildlifeId,
            ["targetStackId"] = order.targetStackId,
            ["stolenQuantity"] = order.stolenQuantity,
            ["physicalChildResultKey"] = expectedChildKey,
            ["parentOperationId"] = parent.operationId,
            ["snapshot"] = CaptureEvidence(fixture, order)
        };
    }

    private static JObject CaptureEvidence(
        Fixture fixture,
        WildlifeFoodRaidOrderSaveData order) => new()
    {
        ["owner"] = JObject.Parse(JsonUtility.ToJson(order)),
        ["physicalItems"] = JObject.Parse(JsonUtility.ToJson(fixture.Items.Capture())),
        ["ledger"] = JObject.Parse(JsonUtility.ToJson(fixture.Ledger.CaptureGameplayOutcomes())),
        ["stateName"] = order.state.ToString(),
        ["phaseName"] = order.commitPhase.ToString()
    };

    private static WildlifeFoodRaidOrderSaveData CreateOrder(
        string raidId,
        string wildlifeId,
        WorldItemStackSnapshot target,
        Vector2Int start,
        Vector2Int end) => new()
    {
        outcomeSchemaVersion =
            WildlifeFoodRaidOrderSaveData.CurrentOutcomeSchemaVersion,
        raidId = raidId,
        wildlifeId = wildlifeId,
        wildlifeSpeciesId = "shadow_wolf",
        wildlifeDisplayName = "그림자 늑대",
        targetStackId = target?.StackId ?? string.Empty,
        targetItemId = target?.ItemId ?? string.Empty,
        targetItemDisplayName = target?.DisplayName ?? string.Empty,
        requestedQuantity = 1,
        startX = start.x,
        startY = start.y,
        startedGameTime = 100f,
        startedAbsoluteDay = 7,
        endX = end.x,
        endY = end.y,
        completedGameTime = 115f,
        completedAbsoluteDay = 7,
        state = WildlifeFoodRaidOrderState.Approaching
    };

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly DomainFactDispatcher facts;

        internal Fixture(
            string runId,
            DungeonPhysicalItemSaveData physicalRestore = null,
            GameplayOutcomeLedgerSaveData ledgerRestore = null)
        {
            GameplayOutcomeRegistry registry = new(
                new IGameplayOutcomeDescriptor[]
                {
                    new TradeInventoryOutcomeDescriptor(
                        new KoreanJosaFormatter()),
                    new WildlifeFoodRaidOutcomeDescriptor(
                        new KoreanJosaFormatter())
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new TradeInventoryOutcomeAdapter(),
                    new WildlifeFoodRaidOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                registry,
                new GameplayOutcomeBufferLimits(),
                new GameplayOutcomeRunId(runId),
                1L);
            Recorder = new GameplayOutcomeRecorder(
                Ledger,
                registry,
                new GameEventBus());
            if (ledgerRestore != null)
            {
                GameplayOutcomeLedgerRestoreCandidate ledgerCandidate =
                    Ledger.PrepareGameplayOutcomeRestore(ledgerRestore);
                Ledger.BeginRestoreCandidate();
                Ledger.PublishGameplayOutcomeRestore(ledgerCandidate);
                Ledger.PublishRestoreCandidate();
                Ledger.CompleteRestoreCandidate();
            }

            facts = new DomainFactDispatcher();
            var commits = new GameplayDomainFactCommitGroup(Recorder, Ledger);
            facts.Subscribe(
                "physical-disposition",
                new GameplayDomainFactObserver<
                    PhysicalItemDisposedFact,
                    TradeInventoryOutcomeReceipt>(
                    PhysicalItemDispositionFactObservation.Capture,
                    registry,
                    Ledger,
                    Recorder,
                    commits));
            WildlifeFoodRaidFactMapping.CreateBinding(
                    registry,
                    Ledger,
                    Recorder,
                    commits,
                    Ledger)
                .Connect(facts);
            Items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
                out WorldItemRepository repository,
                out _,
                out ItemQuantityReservationService reservations,
                out _);
            Repository = repository;
            var innerDispositions = new PhysicalItemBatchDispositionService(
                repository,
                Items.MassQuery,
                EditorNullItemMarkerPresenter.Instance,
                reservations,
                Items.CatalogProvider,
                facts,
                facts,
                new GameplayDomainFactDeliveryQuery(Recorder, Ledger),
                EditorFixedGameCalendar.Instance);
            Dispositions = new FailOnceAcknowledgementDisposition(
                innerDispositions);
            FaultingFacts = new FaultingDomainFacts(facts, facts);
            Outbox = new WildlifeFoodRaidDispositionOutbox(
                Dispositions,
                FaultingFacts,
                FaultingFacts);
            if (physicalRestore != null)
                Items.Restore(physicalRestore);
        }

        internal GameplayOutcomeLedger Ledger { get; }
        internal GameplayOutcomeRecorder Recorder { get; }
        internal WorldItemStackRuntime Items { get; }
        internal WorldItemRepository Repository { get; }
        internal FailOnceAcknowledgementDisposition Dispositions { get; }
        internal FaultingDomainFacts FaultingFacts { get; }
        internal WildlifeFoodRaidDispositionOutbox Outbox { get; }
        internal DomainFactDispatcher Facts => facts;

        internal WorldItemStackSnapshot AddFood(Vector2Int position)
        {
            string stackId = WorldItemRepositoryEditorAccess.AddStack(
                Repository,
                "food:preserved-ration",
                1,
                WorldItemStackState.Loose,
                position: position);
            return Items.GetAllStacks().Single(value => value.StackId == stackId);
        }

        internal int CountExact(string producer) => Ledger
            .CaptureGameplayOutcomes().exactOutcomes.Count(value =>
                value != null && value.producerId == producer);

        internal Fixture RestoreFresh(
            WildlifeFoodRaidOrderSaveData order,
            out WildlifeFoodRaidOrderSaveData restoredOrder)
        {
            restoredOrder =
                JsonUtility.FromJson<WildlifeFoodRaidOrderSaveData>(
                    JsonUtility.ToJson(order));
            DungeonPhysicalItemSaveData physical =
                JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                    JsonUtility.ToJson(Items.Capture()));
            GameplayOutcomeLedgerSaveData outcomes = JsonUtility.FromJson<
                GameplayOutcomeLedgerSaveData>(JsonUtility.ToJson(
                    Ledger.CaptureGameplayOutcomes()));
            ValidateCanonicalJoin(restoredOrder, physical, outcomes);
            var restored = new Fixture(
                "run:wildlife-food-raid-fresh-restore",
                physical,
                outcomes);
            Require(WildlifeFoodRaidDispositionOutbox.HasValidShape(
                    restoredOrder),
                "Restored food-raid order shape is invalid.");
            return restored;
        }

        internal void ValidateCanonical(
            WildlifeFoodRaidOrderSaveData order)
        {
            WildlifeFoodRaidOrderSaveData owner =
                JsonUtility.FromJson<WildlifeFoodRaidOrderSaveData>(
                    JsonUtility.ToJson(order));
            DungeonPhysicalItemSaveData physical =
                JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                    JsonUtility.ToJson(Items.Capture()));
            GameplayOutcomeLedgerSaveData outcomes = JsonUtility.FromJson<
                GameplayOutcomeLedgerSaveData>(JsonUtility.ToJson(
                    Ledger.CaptureGameplayOutcomes()));
            ValidateCanonicalJoin(owner, physical, outcomes);
        }

        private static void ValidateCanonicalJoin(
            WildlifeFoodRaidOrderSaveData order,
            DungeonPhysicalItemSaveData physical,
            GameplayOutcomeLedgerSaveData outcomes)
        {
            var wildlife = new DungeonWildlifeSaveData();
            wildlife.foodRaidOrders.Add(order);
            WildlifeFoodRaidFactPreflight.Validate(
                wildlife,
                physical,
                outcomes);
        }

        public void Dispose()
        {
            facts.Dispose();
            Items.Dispose();
        }
    }

    private sealed class BehaviorHarness : IDisposable
    {
        private readonly bool spawnAtTarget;
        private readonly Vector2Int targetPosition = new(5, 0);
        private readonly ControlledClock clock = new(100f);
        private readonly ICharacterAiWorldRegistry worldRegistry =
            Proxy<ICharacterAiWorldRegistry>();
        private readonly IDoorAccessQuery doors = Proxy<IDoorAccessQuery>();
        private readonly GridPathSearchBroker pathSearch;
        private readonly List<GameObject> actorObjects = new();
        private int nextActorSequence = 9300;

        internal BehaviorHarness(
            Fixture fixture,
            bool hasTarget,
            bool spawnAtTarget)
        {
            this.spawnAtTarget = spawnAtTarget;
            Grid = new Grid(7, 1);
            for (int x = 0; x < Grid.width; x++)
            {
                Grid.GetGridCell(new Vector2Int(x, 0))
                    .SetAreaType(GridCellAreaType.ExteriorPath);
            }
            if (hasTarget)
                _ = fixture.AddFood(targetPosition);

            pathSearch = new GridPathSearchBroker(
                clock,
                doors,
                performanceRecorder: null,
                costPolicy: null);
            pathSearch.BeginFrame(128, enforceBudget: false);
            var world = new WildlifeWorldServices(
                new FixedGridProvider(Grid),
                Proxy<IWildlifeSpeciesCatalogProvider>(),
                Proxy<IGameSessionStateProvider>(),
                Proxy<IWildlifeEcosystemRuntime>(),
                Proxy<IMainCameraProvider>(),
                pathSearch,
                worldRegistry,
                fixture.Items,
                fixture.Dispositions,
                EditorFixedGameCalendar.Instance,
                new GameEventBus(),
                Proxy<IDiseaseDefinitionCatalog>(),
                fixture.Facts,
                fixture.Facts,
                fixture.FaultingFacts);
            var combat = new WildlifeCombatServices(
                Proxy<ICombatResolutionService>(),
                Proxy<ICombatEquipmentRuntime>(),
                Proxy<ICharacterBodyHealthQuery>(),
                Proxy<ICharacterBodyHealthCommand>(),
                Proxy<ICombatLineOfSightService>(),
                new CombatCoverServices(
                    Proxy<ICombatCoverQuery>(),
                    Proxy<ICombatCoverDurabilityRegistry>()),
                Proxy<ICombatAmmoResupplyRuntime>(),
                Proxy<IWildlifeCarcassService>(),
                Proxy<ICharacterPerformanceQuery>());
            var execution = new WildlifeExecutionServices(
                clock,
                new RandomStreamProvider(93017),
                doors,
                Proxy<ICharacterAiPerformanceRecorder>(),
                Proxy<IDungeonDebugRuleQuery>(),
                Proxy<IWorldUiHierarchy>());
            Actors = new List<WildlifeActor>();
            Orders = new List<WildlifeFoodRaidOrderSaveData>();
            Runtime = new WildlifeBehaviorRuntime(
                world,
                combat,
                execution,
                Actors,
                new Dictionary<string, float>(StringComparer.Ordinal),
                Orders,
                Spawn);
        }

        internal Grid Grid { get; }
        internal List<WildlifeActor> Actors { get; }
        internal List<WildlifeFoodRaidOrderSaveData> Orders { get; }
        internal WildlifeBehaviorRuntime Runtime { get; }

        public void Dispose()
        {
            foreach (GameObject actorObject in actorObjects)
            {
                if (actorObject != null)
                    UnityEngine.Object.DestroyImmediate(actorObject);
            }
        }

        private bool Spawn(
            string speciesId,
            Vector2Int preferredPosition,
            out WildlifeActor actor,
            out string message)
        {
            GameObject actorObject = new(
                "FoodRaidBehaviorActor:" + nextActorSequence);
            actorObjects.Add(actorObject);
            actor = actorObject.AddComponent<WildlifeActor>();
            actor.ConfigureRuntimeServices(
                pathSearch,
                worldRegistry,
                clock,
                new RandomStreamProvider(nextActorSequence),
                doors);
            Vector2Int position = spawnAtTarget
                ? targetPosition
                : preferredPosition;
            actor.Initialize(
                Grid,
                WildlifeTestFixtures.CaveRat,
                "wildlife:" + nextActorSequence++,
                position);
            Actors.Add(actor);
            message = string.Empty;
            return true;
        }
    }

    private sealed class FixedGridProvider : IGridSystemProvider
    {
        internal FixedGridProvider(Grid grid) => Grid = grid;
        public GridSystemManager Manager => null;
        public Grid Grid { get; }
        public bool TryGetManager(out GridSystemManager manager)
        {
            manager = null;
            return false;
        }
        public bool TryGetGrid(out Grid grid)
        {
            grid = Grid;
            return grid != null;
        }
    }

    private sealed class ControlledClock : IGameClock
    {
        internal ControlledClock(float time) => Time = time;
        public float DeltaTime => 0.05f;
        public float Time { get; }
        public int FrameCount => 1;
        public bool IsPaused => false;
    }

    private static T Proxy<T>() where T : class =>
        DispatchProxy.Create<T, DefaultInterfaceProxy>();

    public class DefaultInterfaceProxy : DispatchProxy
    {
        protected override object Invoke(MethodInfo targetMethod, object[] args)
        {
            Type returnType = targetMethod.ReturnType;
            if (returnType == typeof(void))
                return null;
            return returnType.IsValueType
                ? Activator.CreateInstance(returnType)
                : null;
        }
    }

    private sealed class FaultingDomainFacts :
        IDomainFactStagingPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly IDomainFactStagingPublisher staging;
        private readonly IDomainFactRecoveryPublisher recovery;

        internal FaultingDomainFacts(
            IDomainFactStagingPublisher staging,
            IDomainFactRecoveryPublisher recovery)
        {
            this.staging = staging
                ?? throw new ArgumentNullException(nameof(staging));
            this.recovery = recovery
                ?? throw new ArgumentNullException(nameof(recovery));
        }

        internal bool RejectNextStage { get; set; }
        internal bool HideNextCommitAcknowledgement { get; set; }
        internal bool SkipNextCommit { get; set; }

        public bool TryStage<TFact>(
            in TFact fact,
            out IDomainFactStage stage,
            out string failureReason)
        {
            if (RejectNextStage && fact is WildlifeFoodRaidFact)
            {
                RejectNextStage = false;
                stage = null;
                failureReason =
                    "qa-wildlife-food-raid-parent-prepare-rejected";
                return false;
            }
            if (!staging.TryStage(fact, out stage, out failureReason))
                return false;
            if (fact is WildlifeFoodRaidFact)
                stage = new FaultingStage(this, stage);
            return true;
        }

        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure) => recovery.ReplayBatch(
            facts,
            results,
            out failure);

        private sealed class FaultingStage : IDomainFactStage
        {
            private readonly FaultingDomainFacts owner;
            private readonly IDomainFactStage inner;

            internal FaultingStage(
                FaultingDomainFacts owner,
                IDomainFactStage inner)
            {
                this.owner = owner;
                this.inner = inner
                    ?? throw new ArgumentNullException(nameof(inner));
            }

            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;

            public DomainFactDelivery Commit()
            {
                if (owner.SkipNextCommit)
                {
                    owner.SkipNextCommit = false;
                    owner.HideNextCommitAcknowledgement = false;
                    return DomainFactDelivery.Uncertain("qa-wildlife-food-raid-parent-response-lost");
                }
                DomainFactDelivery delivery = inner.Commit();
                if (!owner.HideNextCommitAcknowledgement
                    || !delivery.MayBeCommitted)
                {
                    return delivery;
                }
                owner.HideNextCommitAcknowledgement = false;
                return new DomainFactDelivery(
                    DomainFactDeliveryPhase.InDoubt,
                    delivery.OperationKey,
                    delivery.PublicationId,
                    delivery.Fingerprint,
                    "qa-wildlife-food-raid-parent-response-lost");
            }

            public void Cancel() => inner.Cancel();
        }
    }

    internal sealed class FailOnceAcknowledgementDisposition :
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
            inputs, kind, operationId, reasonCode,
            out receipt, out failureReason);

        public bool TryCommitPending(
            IReadOnlyList<PhysicalItemTransformInput> inputs,
            PhysicalItemDispositionKind kind,
            string operationId,
            string reasonCode,
            out PhysicalItemBatchDispositionReceipt receipt,
            out string failureReason) => inner.TryCommitPending(
            inputs, kind, operationId, reasonCode,
            out receipt, out failureReason);

        public bool Acknowledge(string commitId, out string failureReason)
        {
            if (FailNextAcknowledgement)
            {
                FailNextAcknowledgement = false;
                failureReason =
                    "qa-forced-food-raid-acknowledgement-failure";
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
