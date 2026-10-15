#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

public static class PlumbingWorkExecutionHandlerDebugScenarios
{
    private const int InitialQueryVersion = 40;

    private static readonly InfrastructureCommandResult SuccessResult =
        InfrastructureCommandResult.Success();

    private static readonly DomainFailure InjectedFailure =
        new DomainFailure(
            FailureCode.ExternalPaymentRejected,
            "plumbing-command-test");

    private static readonly InfrastructureCommandResult FailedResult =
        InfrastructureCommandResult.Failed(
            FailureCode.ExternalPaymentRejected,
            "plumbing-command-test");

    public static void RunOrThrow()
    {
        if (!RunAll(true))
            throw new InvalidOperationException(
                "B0 W07 plumbing work execution scenarios failed.");
    }

    internal static bool RunAll(bool log = false)
    {
        RunScenario(
            "full-success",
            20f,
            10f,
            SuccessResult,
            SuccessResult,
            new[] { "blockage", "leak" },
            true,
            true,
            2);
        RunScenario(
            "first-command-failure",
            20f,
            10f,
            FailedResult,
            SuccessResult,
            new[] { "blockage" },
            false,
            false,
            0);
        RunScenario(
            "second-command-failure-after-first-success",
            20f,
            10f,
            SuccessResult,
            FailedResult,
            new[] { "blockage", "leak" },
            false,
            true,
            1);
        RunScenario(
            "blockage-only-success",
            20f,
            0f,
            SuccessResult,
            SuccessResult,
            new[] { "blockage" },
            true,
            true,
            1);
        RunScenario(
            "leak-only-success",
            0f,
            20f,
            SuccessResult,
            SuccessResult,
            new[] { "leak" },
            true,
            true,
            1);
        RunScenario(
            "blockage-only-failure",
            20f,
            0f,
            FailedResult,
            SuccessResult,
            new[] { "blockage" },
            false,
            false,
            0);
        RunScenario(
            "leak-only-failure",
            0f,
            20f,
            SuccessResult,
            FailedResult,
            new[] { "leak" },
            false,
            false,
            0);

        if (log)
        {
            Debug.Log(
                "[PlumbingWorkExecutionHandler] All seven command-result scenarios passed.");
        }

        return true;
    }

    private static void RunScenario(
        string scenario,
        float blockage,
        float leak,
        InfrastructureCommandResult blockageResult,
        InfrastructureCommandResult leakResult,
        IReadOnlyList<string> expectedCalls,
        bool expectedSuccess,
        bool expectedCompletionEffectsAlreadyApplied,
        int expectedAppliedCommandCount)
    {
        Query query = new Query(blockage, leak, InitialQueryVersion);
        Commands commands = new Commands(query, blockageResult, leakResult);
        GameObject workObject = new GameObject(
            "PlumbingWorkExecutionHandler scenario work");
        workObject.SetActive(false);

        try
        {
            AbilityWork work = workObject.AddComponent<AbilityWork>();
            WorkExecutionContext context = new WorkExecutionContext(
                0,
                work,
                null,
                null,
                BuiltInWorkTypeIds.Plumbing,
                CompleteWorkAmount,
                () => true);
            WorkExecutionResult result = new WorkExecutionResult();
            PlumbingWorkExecutionHandler handler =
                new PlumbingWorkExecutionHandler(query, commands);

            Drain(handler.Execute(context, result));

            Require(
                result.CompletedSuccessfully == expectedSuccess,
                scenario + ": completion status did not match command results.");
            Require(
                result.CompletionEffectsAlreadyApplied
                    == expectedCompletionEffectsAlreadyApplied,
                scenario + ": applied-effects flag did not match actual commands.");
            Require(
                commands.AppliedCommandCount == expectedAppliedCommandCount,
                scenario + ": applied-command count was incorrect.");
            Require(
                commands.Calls.Count == expectedCalls.Count,
                scenario + ": command count was incorrect.");
            for (int index = 0; index < expectedCalls.Count; index++)
            {
                Require(
                    string.Equals(
                        commands.Calls[index],
                        expectedCalls[index],
                        StringComparison.Ordinal),
                    scenario + ": command order was incorrect.");
            }

            if (expectedSuccess)
            {
                Require(
                    result.Failure.Equals(DomainFailure.None),
                    scenario + ": successful execution unexpectedly reported failure.");
                return;
            }

            Require(
                result.Failure.Equals(InjectedFailure),
                scenario + ": command failure was not propagated.");
            Require(
                result.FailureAxis == CharacterOperationBlockAxis.Water,
                scenario + ": failure axis was not Water.");
            AssertFailureSourceUsesQueryVersion(scenario, result, query);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(workObject);
        }
    }

    private static void AssertFailureSourceUsesQueryVersion(
        string scenario,
        WorkExecutionResult result,
        Query query)
    {
        const BindingFlags NonPublicInstance =
            BindingFlags.Instance | BindingFlags.NonPublic;
        PropertyInfo revisionProperty = typeof(WorkExecutionResult).GetProperty(
            "FailureSourceRevision",
            NonPublicInstance);
        PropertyInfo readerProperty = typeof(WorkExecutionResult).GetProperty(
            "FailureSourceRevisionReader",
            NonPublicInstance);
        Require(
            revisionProperty != null && readerProperty != null,
            scenario + ": failure-source contract could not be inspected.");

        int capturedRevision = (int)revisionProperty.GetValue(result, null);
        Func<int> revisionReader = (Func<int>)readerProperty.GetValue(result, null);
        Require(
            revisionReader != null && capturedRevision == query.Version,
            scenario + ": failure did not capture the current fluid query version.");

        query.AdvanceVersion();
        Require(
            revisionReader() == query.Version,
            scenario + ": failure source did not retain the fluid query version reader.");
    }

    private static IEnumerator CompleteWorkAmount(
        float requiredWork,
        string label,
        float extraMultiplier)
    {
        yield break;
    }

    private static void Drain(IEnumerator routine)
    {
        try
        {
            while (routine.MoveNext())
            {
                if (routine.Current is IEnumerator nested)
                {
                    Drain(nested);
                }
            }
        }
        finally
        {
            (routine as IDisposable)?.Dispose();
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class Query : IFluidInfrastructureQuery
    {
        private readonly float blockage;
        private readonly float leak;

        internal Query(float blockage, float leak, int version)
        {
            this.blockage = blockage;
            this.leak = leak;
            Version = version;
        }

        public int Version { get; private set; }
        public IReadOnlyList<FluidNetworkSnapshot> Networks =>
            Array.Empty<FluidNetworkSnapshot>();
        public IReadOnlyList<WaterTransferFacilitySnapshot> WaterTransfers =>
            Array.Empty<WaterTransferFacilitySnapshot>();

        public bool TryGetNetwork(
            BuildableObject building,
            out FluidNetworkSnapshot snapshot)
        {
            snapshot = default;
            return false;
        }

        public bool TryGetMaintenance(
            BuildableObject building,
            out float currentBlockage,
            out float currentLeak)
        {
            currentBlockage = blockage;
            currentLeak = leak;
            return true;
        }

        public bool TryGetWaterCondition(
            BuildableObject building,
            out BuildingWaterConditionSnapshot snapshot)
        {
            snapshot = default;
            return false;
        }

        internal void AdvanceVersion()
        {
            Version++;
        }
    }

    private sealed class Commands : IFluidInfrastructureCommand
    {
        private readonly Query query;
        private readonly InfrastructureCommandResult blockageResult;
        private readonly InfrastructureCommandResult leakResult;

        internal Commands(
            Query query,
            InfrastructureCommandResult blockageResult,
            InfrastructureCommandResult leakResult)
        {
            this.query = query;
            this.blockageResult = blockageResult;
            this.leakResult = leakResult;
        }

        internal List<string> Calls { get; } = new List<string>();
        internal int AppliedCommandCount { get; private set; }

        public InfrastructureCommandResult SetWaterTransferMode(
            BuildableObject building,
            WaterContainerTransferMode mode)
        {
            throw new InvalidOperationException(
                "Water transfer mode is outside this scenario.");
        }

        public InfrastructureCommandResult ClearBlockage(
            BuildableObject building)
        {
            Calls.Add("blockage");
            if (blockageResult.Succeeded)
            {
                AppliedCommandCount++;
                query.AdvanceVersion();
            }

            return blockageResult;
        }

        public InfrastructureCommandResult RepairLeak(BuildableObject building)
        {
            Calls.Add("leak");
            if (leakResult.Succeeded)
            {
                AppliedCommandCount++;
                query.AdvanceVersion();
            }

            return leakResult;
        }
    }
}
#endif
