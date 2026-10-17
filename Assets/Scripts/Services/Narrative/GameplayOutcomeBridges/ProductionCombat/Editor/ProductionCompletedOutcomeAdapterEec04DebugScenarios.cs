#if UNITY_EDITOR
using System;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class ProductionCompletedOutcomeAdapterEec04DebugScenarios
{
    [MenuItem("DungeonStory/Debug/Run EEC04 Production Outcome Projection")]
    public static void Run()
    {
        GameplayOutcomeRegistry registry = new(
            new IGameplayOutcomeDescriptor[]
            {
                new ProductionCompletedOutcomeDescriptor()
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new ProductionCompletedOutcomeAdapter()
            });
        GameplayOutcomeLedger ledger = new(
            registry,
            new GameplayOutcomeBufferLimits(smallPageCount: 3),
            new GameplayOutcomeRunId("run:eec04-production-outcomes"),
            1L);
        GameplayOutcomeRecorder recorder = new(
            ledger,
            registry,
            new GameEventBus());

        AssertProjection(
            ledger,
            recorder,
            Receipt("success", "output:product", "item:product", 3, 1800L, 0L),
            GameplayOutcomeStatus.Succeeded,
            3,
            1800L,
            0L);
        AssertProjection(
            ledger,
            recorder,
            Receipt("partial", "output:product", "item:product", 2, 1200L, 300L),
            GameplayOutcomeStatus.PartiallySucceeded,
            2,
            1200L,
            300L);
        AssertProjection(
            ledger,
            recorder,
            Receipt(
                "ruined",
                ProductionRuinedOutputProtocol.RecoverableWasteOutputLineId,
                "item:spoilage",
                4,
                2400L,
                300L),
            GameplayOutcomeStatus.Failed,
            4,
            2400L,
            300L);

        Debug.Log("PASS ProductionCompletedOutcomeAdapter EEC04 projection");
    }

    private static void AssertProjection(
        GameplayOutcomeLedger ledger,
        GameplayOutcomeRecorder recorder,
        ProductionCompletedOutcomeReceipt receipt,
        GameplayOutcomeStatus expectedStatus,
        int expectedQuantity,
        long expectedMassGrams,
        long expectedLossMassGrams)
    {
        OutcomePrepareResult prepare = recorder.TryPrepare(
            receipt,
            out PreparedOutcomeToken prepared);
        Require(prepare.Success, "Production outcome prepare failed: " + prepare.DetailCode);

        OutcomeCommitResult commit = recorder.CommitPrepared(
            prepared,
            receipt.CycleSequence,
            out CommittedOutcomeToken committed);
        Require(commit.Success, "Production outcome commit failed: " + commit.DetailCode);
        OutcomeDeliveryResult delivery = recorder.TryDeliver(committed);
        Require(delivery.Published, "Production outcome delivery failed: " + delivery.DetailCode);
        Require(recorder.Acknowledge(committed).Success, "Production outcome acknowledgement failed.");
        Require(ledger.TryGetExact(delivery.OutcomeId, out GameplayOutcomeSnapshot snapshot),
            "Published production outcome was not queryable.");
        Require(snapshot.status == expectedStatus,
            $"Expected {expectedStatus}, received {snapshot.status}.");

        string recipeId = receipt.RecipeId;
        AssertMetric(
            snapshot,
            ProductionCombatOutcomeIds.ProducedQuantityMetric.Value,
            ProductionCombatOutcomeIds.CountUnit.Value,
            ProductionCombatOutcomeIds.RecipeKind.Value,
            recipeId,
            expectedQuantity);
        AssertMetric(
            snapshot,
            ProductionCombatOutcomeIds.ProducedMassMetric.Value,
            ProductionCombatOutcomeIds.GramUnit.Value,
            ProductionCombatOutcomeIds.RecipeKind.Value,
            recipeId,
            expectedMassGrams);
        AssertMetric(
            snapshot,
            ProductionCombatOutcomeIds.DeclaredLossMetric.Value,
            ProductionCombatOutcomeIds.GramUnit.Value,
            ProductionCombatOutcomeIds.RecipeKind.Value,
            recipeId,
            expectedLossMassGrams);
        AssertMetric(
            snapshot,
            ProductionCombatOutcomeIds.ProducedQuantityMetric.Value,
            ProductionCombatOutcomeIds.CountUnit.Value,
            ProductionCombatOutcomeIds.ItemKind.Value,
            receipt.OutputLines.Single().ItemId,
            expectedQuantity);
        AssertMetric(
            snapshot,
            ProductionCombatOutcomeIds.ProducedMassMetric.Value,
            ProductionCombatOutcomeIds.GramUnit.Value,
            ProductionCombatOutcomeIds.ItemKind.Value,
            receipt.OutputLines.Single().ItemId,
            expectedMassGrams);
    }

    private static void AssertMetric(
        GameplayOutcomeSnapshot snapshot,
        string metricId,
        string unitId,
        string referenceKindId,
        string referenceId,
        double expectedValue)
    {
        GameplayOutcomeMetricSnapshot metric = snapshot.metrics.Single(value =>
            string.Equals(value.metricId, metricId, StringComparison.Ordinal)
            && string.Equals(value.unitId, unitId, StringComparison.Ordinal)
            && string.Equals(value.referenceKindId, referenceKindId, StringComparison.Ordinal)
            && string.Equals(value.referenceId, referenceId, StringComparison.Ordinal));
        Require(Math.Abs(metric.value - expectedValue) < 0.0001d,
            $"Metric {metricId} for {referenceId} was {metric.value}, expected {expectedValue}.");
    }

    private static ProductionCompletedOutcomeReceipt Receipt(
        string scenarioId,
        string outputLineId,
        string itemId,
        int quantity,
        long massGrams,
        long declaredLossMassGrams)
    {
        ProductionBillId billId = new("production-bill:eec04:" + scenarioId);
        BuildingInstanceId facilityId = new("building:eec04-facility");
        const string recipeId = "recipe:eec04";
        string commitId = "commit:eec04:" + scenarioId;
        const string digest = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        ProductionOutcomeLineSnapshot line = new(
            outputLineId,
            commitId,
            itemId,
            quantity,
            massGrams,
            digest);
        ProductionOutcomePhysicalStackSnapshot stack = new(
            "stack:eec04:" + scenarioId,
            outputLineId,
            itemId,
            quantity,
            massGrams,
            string.Empty);

        return new ProductionCompletedOutcomeReceipt(
            billId,
            1,
            recipeId,
            facilityId,
            ProductionCombatOutcomeNames.Snapshot("생산 시설", facilityId.Value),
            string.Empty,
            default,
            "batch:eec04:" + scenarioId,
            digest,
            digest,
            "destination:eec04:" + scenarioId,
            string.Empty,
            0,
            0L,
            0L,
            0L,
            declaredLossMassGrams,
            0L,
            new[] { line },
            new[] { stack },
            1);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
