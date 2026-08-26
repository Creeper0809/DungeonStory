#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class ProductionRuinedBatchExecutionContractDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Run Production Ruined Batch Contracts")]
    public static void RunAll()
    {
        VerifySilageMassDisposition();
        VerifyWastewaterAndRemainderDisposition();
        VerifyInvalidDispositionFailsLoud();
        Debug.Log("V27_PRODUCTION_RUINED_BATCH_CONTRACT=PASS");
    }

    private static void VerifySilageMassDisposition()
    {
        ProductionRuinedBatchDispositionPlan plan =
            ProductionRuinedBatchDispositionPlan.Create(
                wipInputMassGrams: 590L,
                processCleanWaterMassGrams: 100L,
                processWastewaterMassGrams: 0L,
                spoilageItemId: "waste:plant-rot",
                spoilageUnitMassGrams: 600L);
        Require(plan.AvailableMassGrams == 690L,
            "silage available mass was not 690g");
        Require(plan.RecoverableWasteQuantity == 1
            && plan.RecoverableWasteMassGrams == 600L,
            "silage ruin did not recover exactly one 600g plant-rot item");
        Require(plan.DeclaredLossMassGrams == 90L,
            "silage ruin did not declare the exact 90g process loss");

        ProductionRuinedBatchExecutionResult completed =
            ProductionRuinedBatchExecutionResult.Completed(plan);
        Require(completed.IsValid
            && completed.BatchDispositionCompleted
            && completed.Phase == ProductionPreparedOutputPhase.Completed
            && !completed.Failure.IsFailure,
            "completed ruined-batch result was not canonical");
    }

    private static void VerifyWastewaterAndRemainderDisposition()
    {
        ProductionRuinedBatchDispositionPlan plan =
            ProductionRuinedBatchDispositionPlan.Create(
                wipInputMassGrams: 590L,
                processCleanWaterMassGrams: 100L,
                processWastewaterMassGrams: 50L,
                spoilageItemId: "waste:plant-rot",
                spoilageUnitMassGrams: 600L);
        Require(plan.RecoverableWasteMassGrams == 600L
            && plan.ProcessWastewaterMassGrams == 50L
            && plan.DeclaredLossMassGrams == 40L
            && plan.AvailableMassGrams == 690L,
            "wastewater and declared-loss accounting did not conserve mass");

        ProductionRuinedBatchExecutionResult blocked =
            ProductionRuinedBatchExecutionResult.Blocked(
                ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace,
                new DomainFailure(
                    FailureCode.ProductionOutputSpaceUnavailable,
                    "production-output:test"));
        Require(blocked.IsValid
            && !blocked.BatchDispositionCompleted
            && blocked.Phase ==
                ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace
            && blocked.Failure.Code ==
                FailureCode.ProductionOutputSpaceUnavailable,
            "blocked ruined-batch result lost its durable waiting phase");
    }

    private static void VerifyInvalidDispositionFailsLoud()
    {
        RequireThrows(() => ProductionRuinedBatchDispositionPlan.Create(
                590L,
                100L,
                0L,
                " waste:plant-rot",
                600L),
            "noncanonical spoilage ID");
        RequireThrows(() => ProductionRuinedBatchDispositionPlan.Create(
                590L,
                100L,
                0L,
                "waste:plant-rot",
                700L),
            "zero recoverable-waste quantity");
        RequireThrows(() => ProductionRuinedBatchDispositionPlan.Create(
                long.MaxValue,
                1L,
                0L,
                "waste:plant-rot",
                600L),
            "available-mass overflow");
        RequireThrows(() => ProductionRuinedBatchExecutionResult.Blocked(
                ProductionPreparedOutputPhase.Completed,
                new DomainFailure(FailureCode.ProductionOutputUnavailable)),
            "blocked Completed phase");
    }

    private static void RequireThrows(Action action, string label)
    {
        try
        {
            action();
        }
        catch (Exception)
        {
            return;
        }
        throw new InvalidOperationException(
            $"Expected ruined-batch contract failure: {label}");
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
