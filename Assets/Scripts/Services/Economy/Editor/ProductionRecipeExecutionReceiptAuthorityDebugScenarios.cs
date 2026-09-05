#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class ProductionRecipeExecutionReceiptAuthorityDebugScenarios
{
    [MenuItem(
        "DungeonStory/Tests/Production/Recipe Execution Receipt Authority")]
    public static void Run()
    {
        ProductionRecipeExecutionReceiptAuthority authority = new();
        ProductionBillId billId = new(
            "production-bill:natural-recipe-receipt-test");
        BuildingInstanceId facilityId = new(
            "building:natural-recipe-receipt-test");
        ProductionRecipeExecutionCorrelation correlation = new(
            billId,
            1,
            "recipe:natural-receipt-test",
            facilityId);
        const string ActionId = "natural-action:recipe-receipt-test";

        Require(!authority.RequiresExactCapture(billId, 1),
            "an uncorrelated cycle requested exact diagnostics capture");
        Require(authority.TryRegisterExecution(
                ActionId,
                correlation,
                out string registerFailure),
            registerFailure);
        Require(authority.RequiresExactCapture(billId, 1),
            "a registered cycle did not request exact diagnostics capture");

        ProductionPreparedOutputBatchSaveData completed = CreateCompleted(
            billId,
            facilityId,
            correlation.RecipeId);
        Require(authority.TryPublishCompleted(
                billId,
                1,
                correlation.RecipeId,
                facilityId,
                "production-input-commit:natural-receipt-test",
                3,
                3_000L,
                completed,
                out string publishFailure),
            publishFailure);
        Require(authority.TryCaptureExecutionReceipt(
                ActionId,
                out ProductionRecipeExecutionReceipt receipt),
            "completed receipt was not queryable");
        Require(receipt.ActualBatchMassGrams == 4_000L,
            "completed mass drifted");
        Require(receipt.Outputs.Count == 1
                && receipt.PhysicalSlices.Count == 2,
            "one output line split across two physical stacks was not retained");

        Require(authority.TryPublishCompleted(
                billId,
                1,
                correlation.RecipeId,
                facilityId,
                "production-input-commit:natural-receipt-test",
                3,
                3_000L,
                completed,
                out string replayFailure),
            replayFailure);
        Require(authority.TryAcknowledgeExecutionReceipt(
                ActionId,
                receipt.RuntimeReceiptDigest,
                out string acknowledgeFailure),
            acknowledgeFailure);
        Require(!authority.TryCaptureExecutionReceipt(ActionId, out _),
            "acknowledged receipt remained queryable");
        Require(!authority.RequiresExactCapture(billId, 1),
            "acknowledged cycle retained exact diagnostics capture");

        Require(authority.TryRegisterExecution(
                ActionId,
                correlation,
                out string reregisterFailure),
            reregisterFailure);
        Require(authority.TryPublishCompleted(
                billId,
                1,
                correlation.RecipeId,
                facilityId,
                "production-input-commit:natural-receipt-test",
                3,
                3_000L,
                completed,
                out string republishFailure),
            republishFailure);
        Require(authority.TryCancelExecution(
                ActionId,
                correlation,
                out string completedCancelFailure),
            completedCancelFailure);
        Require(!authority.TryCaptureExecutionReceipt(ActionId, out _),
            "cancelled completed receipt remained queryable");

        Require(authority.TryPublishCompleted(
                new ProductionBillId("production-bill:uncorrelated-test"),
                1,
                correlation.RecipeId,
                facilityId,
                string.Empty,
                0,
                0L,
                completed,
                out string uncorrelatedFailure),
            uncorrelatedFailure);
        VerifyExactCapabilityReceipt();
        VerifyPhysicalCommitCoverageRejectsMismatch();
        Debug.Log(
            "[ProductionRecipeExecutionReceiptAuthorityDebugScenarios] PASS");
    }

    private static void VerifyPhysicalCommitCoverageRejectsMismatch()
    {
        const string Fingerprint =
            "cccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccccc";
        ProductionRecipeExecutionCorrelation correlation = new(
            new ProductionBillId("production-bill:commit-coverage-test"),
            1,
            "recipe:commit-coverage-test",
            new BuildingInstanceId("building:commit-coverage-test"));
        bool rejected = false;
        try
        {
            _ = new ProductionRecipeExecutionReceipt(
                "natural-action:commit-coverage-test",
                correlation,
                string.Empty,
                0,
                0L,
                ProductionRecipeExecutionPublicationKind.ExactCapabilityUnits,
                "production-output-batch:commit-coverage-test",
                new[] { "production-output-commit:expected" },
                Fingerprint,
                Fingerprint,
                new[]
                {
                    new ProductionRecipeExecutionOutputLineReceipt(
                        "output:commit-coverage-test",
                        "item:commit-coverage-test",
                        1,
                        1_000L,
                        Fingerprint,
                        new[] { "production-output-commit:expected" })
                },
                new[]
                {
                    new ProductionRecipeExecutionPhysicalSliceReceipt(
                        "output:commit-coverage-test",
                        "item:commit-coverage-test",
                        "world-stack:commit-coverage-test",
                        1,
                        1_000L,
                        "production-output-commit:wrong")
                });
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected,
            "a physical slice with the wrong commit ID passed exact coverage");
    }

    private static void VerifyExactCapabilityReceipt()
    {
        const string Fingerprint =
            "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
        const string ActionId = "natural-action:recipe-exact-receipt-test";
        const string RecipeId = "recipe:natural-exact-receipt-test";
        const string OutputLineId = "output:natural-exact-receipt-test";
        const string ItemId = "item:natural-exact-receipt-test";
        ProductionBillId billId = new(
            "production-bill:natural-exact-receipt-test");
        BuildingInstanceId facilityId = new(
            "building:natural-exact-receipt-test");
        ProductionRecipeExecutionCorrelation correlation = new(
            billId,
            1,
            RecipeId,
            facilityId);
        ProductionRecipeExecutionReceiptAuthority authority = new();
        Require(authority.TryRegisterExecution(
                ActionId,
                correlation,
                out string registerFailure),
            registerFailure);
        Require(!authority.TryAcknowledgeExecutionReceipt(
                ActionId,
                Fingerprint,
                out _),
            "an exact receipt was acknowledged before cycle finalization");

        ProductionResolvedOutputSaveData output = new()
        {
            outputLineId = OutputLineId,
            itemId = ItemId,
            outputCapabilityFingerprint = Fingerprint,
            amount = 2
        };
        CaptureExactUnit(
            authority,
            billId,
            facilityId,
            RecipeId,
            output,
            "production-output-commit:natural-exact-receipt-test:01",
            "world-stack:natural-exact-receipt-test:01",
            1_250L,
            Fingerprint);
        CaptureExactUnit(
            authority,
            billId,
            facilityId,
            RecipeId,
            output,
            "production-output-commit:natural-exact-receipt-test:02",
            "world-stack:natural-exact-receipt-test:02",
            1_750L,
            Fingerprint);
        output.pendingCommitId = string.Empty;
        output.pendingCommitApplied = false;
        output.pendingOutputPublication =
            ProductionExactOutputPublicationSaveData.Empty();
        output.committedAmount = 2;
        output.committedMassGrams = 3_000L;

        Require(authority.TryFinalizeExactCompleted(
                billId,
                1,
                RecipeId,
                facilityId,
                "production-input-commit:natural-exact-receipt-test",
                2,
                2_000L,
                new[] { output },
                out string finalizeFailure),
            finalizeFailure);
        Require(authority.TryCaptureExecutionReceipt(
                ActionId,
                out ProductionRecipeExecutionReceipt receipt),
            "finalized exact receipt was not queryable");
        Require(receipt.PublicationKind ==
                    ProductionRecipeExecutionPublicationKind.ExactCapabilityUnits
                && receipt.ActualBatchMassGrams == 3_000L
                && receipt.RouteBatchCommitIds.Count == 2
                && receipt.PhysicalSlices.Count == 2,
            "exact receipt did not retain its actual commit and physical slices");
        Require(!authority.TryFinalizeExactCompleted(
                billId,
                1,
                RecipeId,
                facilityId,
                "production-input-commit:natural-exact-receipt-test",
                2,
                2_000L,
                new[] { output },
                out _),
            "an exact cycle replay finalized twice");
        Require(authority.TryAcknowledgeExecutionReceipt(
                ActionId,
                receipt.RuntimeReceiptDigest,
                out string acknowledgeFailure),
            acknowledgeFailure);
    }

    private static void CaptureExactUnit(
        ProductionRecipeExecutionReceiptAuthority authority,
        ProductionBillId billId,
        BuildingInstanceId facilityId,
        string recipeId,
        ProductionResolvedOutputSaveData output,
        string commitId,
        string stackId,
        long massGrams,
        string fingerprint)
    {
        output.pendingCommitId = commitId;
        output.pendingCommitApplied = true;
        ProductionCommittedOutputSnapshot snapshot = new(
            commitId,
            facilityId.Value,
            "capability:natural-exact-receipt-test",
            1,
            "codec:natural-exact-receipt-test",
            1,
            fingerprint,
            massGrams,
            fingerprint,
            massGrams,
            massGrams,
            fingerprint,
            fingerprint,
            ProductionOutputDestinationId.FromFacility(facilityId).Value,
            0,
            0,
            "production",
            "production-operation:natural-exact-receipt-test",
            facilityId.Value,
            1L,
            false,
            new[]
            {
                new ProductionCommittedOutputStackSnapshot(
                    output.outputLineId,
                    stackId,
                    output.itemId,
                    1,
                    massGrams,
                    fingerprint,
                    string.Empty)
            });
        Require(authority.TryCaptureExactCommittedUnit(
                billId,
                1,
                recipeId,
                facilityId,
                output,
                snapshot,
                out string captureFailure),
            captureFailure);
    }

    private static ProductionPreparedOutputBatchSaveData CreateCompleted(
        ProductionBillId billId,
        BuildingInstanceId facilityId,
        string recipeId)
    {
        const string Fingerprint =
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        const string BatchCommitId =
            "production-output-batch:natural-recipe-receipt-test";
        const string OutputLineId = "output:natural-receipt-test";
        const string ItemId = "item:natural-receipt-test";
        const string LineCommitId =
            "production-output-line-commit:natural-recipe-receipt-test";
        return new ProductionPreparedOutputBatchSaveData
        {
            phase = ProductionPreparedOutputPhase.Completed,
            billId = billId.Value,
            cycleSequence = 1,
            recipeId = recipeId,
            destinationId = ProductionOutputDestinationId
                .FromFacility(facilityId).Value,
            outcomeFingerprint = Fingerprint,
            admissionFingerprint = Fingerprint,
            batchCommitId = BatchCommitId,
            totalPhysicalMassGrams = 4_000L,
            lines = new List<ProductionPreparedOutputLineSaveData>
            {
                new()
                {
                    outputLineId = OutputLineId,
                    role = ProductionOutputRole.Main,
                    itemId = ItemId,
                    outputCapabilityFingerprint = Fingerprint,
                    lineCommitId = LineCommitId,
                    quantity = 4,
                    rollSucceeded = true,
                    exactMassGrams = 4_000L
                }
            },
            physicalCandidates = new List<
                ProductionPreparedOutputPhysicalCandidateSaveData>
            {
                new()
                {
                    stackId = "world-stack:natural-receipt-test:01",
                    batchCommitId = BatchCommitId,
                    outputLineId = OutputLineId,
                    itemId = ItemId,
                    lineCommitId = LineCommitId,
                    quantity = 2,
                    massGrams = 2_000L,
                    destinationId = ProductionOutputDestinationId
                        .FromFacility(facilityId).Value
                },
                new()
                {
                    stackId = "world-stack:natural-receipt-test:02",
                    batchCommitId = BatchCommitId,
                    outputLineId = OutputLineId,
                    itemId = ItemId,
                    lineCommitId = LineCommitId,
                    quantity = 2,
                    massGrams = 2_000L,
                    destinationId = ProductionOutputDestinationId
                        .FromFacility(facilityId).Value
                }
            }
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
