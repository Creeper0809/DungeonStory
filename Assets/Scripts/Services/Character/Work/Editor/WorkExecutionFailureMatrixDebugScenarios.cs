#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class WorkExecutionFailureMatrixDebugScenarios
{
    public const string ReportPath =
        "docs/implementation-reports/work-execution-failure-matrix-latest.txt";

    public static string LastReport { get; private set; } = string.Empty;

    [MenuItem("Tools/Dungeon Story/Validation/AI/Work Execution Failure Matrix (31)")]
    public static void RunAll()
    {
        MatrixEvaluation result = Evaluate();
        WriteReport(result.Report);
        LastReport = result.Report;
        if (!result.Passed)
            throw new InvalidOperationException(result.Summary);

        Debug.Log(result.Summary);
    }

    /// <summary>
    /// Unity MCP entrypoint. It always writes the complete matrix before a red
    /// row throws, so an incomplete work type cannot disappear behind a single
    /// aggregate assertion.
    /// </summary>
    public static string RunFromUnityMcp()
    {
        RunAll();
        return LastReport;
    }

    public static string CaptureReportWithoutThrowing()
    {
        MatrixEvaluation result = Evaluate();
        WriteReport(result.Report);
        LastReport = result.Report;
        return result.Report;
    }

    private static MatrixEvaluation Evaluate()
    {
        List<string> structuralFailures = new List<string>();
        IReadOnlyList<WorkTypeId> builtIns = BuiltInWorkTypeIds.All;
        IReadOnlyList<WorkTypeDefinition> definitions = WorkTypeCatalog.All;
        IReadOnlyList<WorkExecutionFailureProfile> profiles =
            BuiltInWorkExecutionFailureProfiles.All;

        ValidateExactCatalog(builtIns, definitions, profiles, structuralFailures);
        bool commonContractReady = ValidateCommonExecutorContract(structuralFailures);
        ValidateRestockLeaseFocusedContract(structuralFailures);

        Dictionary<WorkTypeId, WorkExecutionFailureProfile> byId = profiles
            .GroupBy(profile => profile.WorkTypeId)
            .Where(group => group.Count() == 1)
            .ToDictionary(group => group.Key, group => group.Single());
        StringBuilder rows = new StringBuilder(8192);
        int green = 0;
        int unimplemented = 0;

        rows.AppendLine("status | workType | route | target | facility | recipe | item | checkpoint | executor | evidence");
        rows.AppendLine("--- | --- | --- | --- | --- | --- | --- | --- | --- | ---");
        foreach (WorkTypeId id in builtIns)
        {
            if (!byId.TryGetValue(id, out WorkExecutionFailureProfile profile))
            {
                unimplemented++;
                rows.Append("UNIMPLEMENTED | ").Append(id.Value)
                    .AppendLine(" | MISSING_PROFILE | Missing | Missing | Missing | Missing | Missing | - | authored profile absent");
                continue;
            }

            List<string> rowFailures = ValidateProfile(profile, commonContractReady);
            bool implemented = rowFailures.Count == 0;
            if (implemented) green++; else unimplemented++;
            rows.Append(implemented ? "GREEN" : "UNIMPLEMENTED")
                .Append(" | ").Append(id.Value)
                .Append(" | ").Append(profile.Route)
                .Append(" | ").Append(profile.TargetInvalidation)
                .Append(" | ").Append(ReservationCoverage(profile, WorkReservationFailureKinds.Facility))
                .Append(" | ").Append(ReservationCoverage(profile, WorkReservationFailureKinds.Recipe))
                .Append(" | ").Append(ReservationCoverage(profile, WorkReservationFailureKinds.Item))
                .Append(" | ").Append(profile.SafeCheckpointCancellation)
                .Append(" | ").Append(profile.ExecutorType.Name)
                .Append(" | ").Append(profile.Evidence);
            if (!implemented)
                rows.Append("; missing=").Append(string.Join(",", rowFailures));
            rows.AppendLine();
        }

        bool passed = structuralFailures.Count == 0
            && green == 31
            && unimplemented == 0;
        StringBuilder report = new StringBuilder(12288);
        report.AppendLine("WORK_EXECUTION_FAILURE_MATRIX_V1");
        report.Append("result=").AppendLine(passed ? "PASS" : "FAIL");
        report.Append("catalog=").Append(builtIns.Count)
            .Append("; profiles=").Append(profiles.Count)
            .Append("; green=").Append(green)
            .Append("; unimplemented=").Append(unimplemented)
            .Append("; commonContract=").AppendLine(commonContractReady ? "GREEN" : "BROKEN");
        report.AppendLine("axes=target invalidation + facility/recipe/item reservation failure + safe-checkpoint cancellation");
        report.AppendLine("restockFocusedContract=two-owner competition + cancellation return + shortage rejection + exact-once lease commit");
        report.AppendLine("note=NotApplicable and PolicyExempt are explicit authored outcomes, never implicit success.");
        if (structuralFailures.Count > 0)
        {
            report.AppendLine("STRUCTURAL_FAILURES");
            foreach (string failure in structuralFailures)
                report.Append("- ").AppendLine(failure);
        }
        report.AppendLine();
        report.Append(rows);

        string summary =
            $"WORK_EXECUTION_FAILURE_MATRIX={(passed ? "PASS" : "FAIL")}; "
            + $"catalog={builtIns.Count}; profiles={profiles.Count}; green={green}; "
            + $"unimplemented={unimplemented}; report={ReportPath}";
        return new MatrixEvaluation(passed, summary, report.ToString());
    }

    private static void ValidateExactCatalog(
        IReadOnlyList<WorkTypeId> builtIns,
        IReadOnlyList<WorkTypeDefinition> definitions,
        IReadOnlyList<WorkExecutionFailureProfile> profiles,
        List<string> failures)
    {
        if (builtIns.Count != 31)
            failures.Add($"BuiltInWorkTypeIds.All count is {builtIns.Count}, expected 31.");
        if (builtIns.Distinct().Count() != builtIns.Count)
            failures.Add("BuiltInWorkTypeIds.All contains duplicate IDs.");
        if (definitions.Count != 31)
            failures.Add($"WorkTypeCatalog count is {definitions.Count}, expected 31.");
        if (definitions.Select(value => value.WorkTypeId).Distinct().Count() != definitions.Count)
            failures.Add("WorkTypeCatalog contains duplicate IDs.");
        if (profiles.Count != 31)
            failures.Add($"Failure profile count is {profiles.Count}, expected 31.");
        if (profiles.Select(value => value.WorkTypeId).Distinct().Count() != profiles.Count)
            failures.Add("Failure profiles contain duplicate IDs.");

        HashSet<WorkTypeId> expected = new HashSet<WorkTypeId>(builtIns);
        HashSet<WorkTypeId> catalog = new HashSet<WorkTypeId>(
            definitions.Select(value => value.WorkTypeId));
        HashSet<WorkTypeId> authored = new HashSet<WorkTypeId>(
            profiles.Select(value => value.WorkTypeId));
        if (!expected.SetEquals(catalog))
            failures.Add("WorkTypeCatalog IDs do not exactly match BuiltInWorkTypeIds.All.");
        if (!expected.SetEquals(authored))
            failures.Add("Failure profile IDs do not exactly match BuiltInWorkTypeIds.All.");
    }

    private static bool ValidateCommonExecutorContract(List<string> failures)
    {
        const BindingFlags instancePrivate = BindingFlags.Instance | BindingFlags.NonPublic;
        Type executor = typeof(WorkTaskExecutor);
        bool targetGuard = executor.GetMethod(
                "CanContinueTimedWork",
                instancePrivate,
                null,
                new[] { typeof(int), typeof(CharacterActor), typeof(BuildableObject) },
                null) != null;
        bool checkpoint = executor.GetMethod(
                "TrySuspendAtSafeCheckpoint",
                instancePrivate,
                null,
                new[] { typeof(CharacterActor), typeof(BuildableObject), typeof(WorkTypeId) },
                null) != null;
        bool abort = executor.GetMethod("AbortWorkRun", instancePrivate) != null;
        bool end = executor.GetMethod("EndAiAction", BindingFlags.Static | BindingFlags.NonPublic) != null;
        bool contextCheckpoint = typeof(WorkExecutionContext).GetMethod(
                nameof(WorkExecutionContext.TrySuspendAtCheckpoint),
                BindingFlags.Instance | BindingFlags.Public) != null;

        if (!targetGuard) failures.Add("Common executor target-identity guard is absent.");
        if (!checkpoint || !contextCheckpoint)
            failures.Add("Common executor checkpoint bridge is absent.");
        if (!abort || !end)
            failures.Add("Common executor terminal cleanup path is absent.");
        return targetGuard && checkpoint && contextCheckpoint && abort && end;
    }

    private static void ValidateRestockLeaseFocusedContract(List<string> failures)
    {
        try
        {
            WorldItemRepository repository = new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            string stackId = repository.AddEditorTestStack(
                "item:restock-focused",
                3,
                WorldItemStackState.Stored,
                "warehouse:restock-focused");
            ItemQuantityReservationService reservations =
                new ItemQuantityReservationService(
                    repository,
                    EditorNullItemMarkerPresenter.Instance,
                    new UnityGameClock());
            ItemQuantityReservationRequest two = new ItemQuantityReservationRequest(
                new ItemStackId(stackId),
                2,
                ItemReservationSignature.Create(
                    "item:restock-focused",
                    Array.Empty<ItemInstanceComponentSaveData>()));
            ItemQuantityReservationRequest one = new ItemQuantityReservationRequest(
                new ItemStackId(stackId),
                1,
                ItemReservationSignature.Create(
                    "item:restock-focused",
                    Array.Empty<ItemInstanceComponentSaveData>()));

            Require(reservations.TryReserve(
                    "restock:actor-a:shop:1",
                    "actor-a",
                    ItemReservationPurpose.FacilityBuffer,
                    "restock:shop:1",
                    two,
                    out ItemQuantityLease first,
                    out _),
                "first competing restock lease failed");
            Require(reservations.TryReserve(
                    "restock:actor-b:shop:1",
                    "actor-b",
                    ItemReservationPurpose.FacilityBuffer,
                    "restock:shop:1",
                    one,
                    out ItemQuantityLease second,
                    out _),
                "second competing restock lease failed");
            Require(!reservations.TryReserve(
                    "restock:actor-c:shop:1",
                    "actor-c",
                    ItemReservationPurpose.FacilityBuffer,
                    "restock:shop:1",
                    one,
                    out _,
                    out DomainFailure shortage)
                && shortage.Code == FailureCode.ItemReservationQuantityUnavailable,
                "restock quantity shortage was not rejected");
            Require(reservations.Release(
                    second.leaseId,
                    ItemReservationReleaseReason.Cancelled)
                && reservations.GetAvailableQuantity(new ItemStackId(stackId)) == 1,
                "restock cancellation did not return exactly one unit");
            Require(reservations.TryReserve(
                    "restock:actor-d:shop:1",
                    "actor-d",
                    ItemReservationPurpose.FacilityBuffer,
                    "restock:shop:1",
                    one,
                    out ItemQuantityLease replacement,
                    out _),
                "released restock quantity could not be reacquired");

            IItemQuantityLeaseMutation mutation = reservations;
            Require(mutation.TryConsumeSlices(
                    first.leaseId,
                    2,
                    out _,
                    out _),
                "restock lease commit failed");
            Require(!mutation.TryConsumeSlices(
                    first.leaseId,
                    1,
                    out _,
                    out _),
                "restock lease committed twice");
            Require(reservations.Release(
                    replacement.leaseId,
                    ItemReservationReleaseReason.Cancelled)
                && reservations.GetReservedQuantity(new ItemStackId(stackId)) == 0,
                "restock focused contract leaked a reservation");
        }
        catch (Exception exception)
        {
            failures.Add("Restock focused quantity-Lease contract failed: "
                + exception.Message);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static List<string> ValidateProfile(
        WorkExecutionFailureProfile profile,
        bool commonContractReady)
    {
        List<string> failures = new List<string>(4);
        if (!profile.IsImplemented)
        {
            if (profile.TargetInvalidation == WorkFailureAxisCoverage.Missing)
                failures.Add("target-invalidation");
            if (profile.ReservationFailure == WorkFailureAxisCoverage.Missing)
                failures.Add("reservation-failure");
            if (profile.SafeCheckpointCancellation == WorkFailureAxisCoverage.Missing)
                failures.Add("safe-checkpoint");
        }

        if (UsesCommon(profile) && !commonContractReady)
            failures.Add("common-contract");
        if (profile.TargetInvalidation is WorkFailureAxisCoverage.NotApplicable
            or WorkFailureAxisCoverage.PolicyExempt)
            failures.Add("target-axis-invalid-exemption");

        bool hasReservation = profile.ReservationKinds != WorkReservationFailureKinds.None;
        if (hasReservation
            && profile.ReservationFailure == WorkFailureAxisCoverage.NotApplicable)
            failures.Add("reservation-applicability-mismatch");
        if (!hasReservation
            && profile.ReservationFailure != WorkFailureAxisCoverage.NotApplicable)
            failures.Add("reservation-none-mismatch");

        if (!WorkTypeCatalog.TryGet(profile.WorkTypeId, out WorkTypeDefinition definition))
        {
            failures.Add("catalog-definition");
            return failures;
        }
        bool interruptible = (definition.EmergencyFlags & EmergencyWorkFlags.ReserveEligible) != 0;
        if (interruptible
            && profile.SafeCheckpointCancellation is WorkFailureAxisCoverage.PolicyExempt
                or WorkFailureAxisCoverage.NotApplicable)
            failures.Add("checkpoint-required-by-policy");
        if (!interruptible
            && profile.SafeCheckpointCancellation != WorkFailureAxisCoverage.PolicyExempt)
            failures.Add("checkpoint-must-be-policy-exempt");

        if (profile.Route == WorkExecutorRoute.RegisteredHandler
            && !typeof(IWorkExecutionHandler).IsAssignableFrom(profile.ExecutorType))
            failures.Add("handler-contract");
        if (profile.ExecutorType.IsAbstract)
            failures.Add("abstract-executor");
        return failures.Distinct(StringComparer.Ordinal).ToList();
    }

    private static bool UsesCommon(WorkExecutionFailureProfile profile) =>
        profile.TargetInvalidation == WorkFailureAxisCoverage.CommonExecutor
        || profile.ReservationFailure == WorkFailureAxisCoverage.CommonExecutor
        || profile.SafeCheckpointCancellation == WorkFailureAxisCoverage.CommonExecutor;

    private static WorkFailureAxisCoverage ReservationCoverage(
        WorkExecutionFailureProfile profile,
        WorkReservationFailureKinds kind) =>
        (profile.ReservationKinds & kind) != 0
            ? profile.ReservationFailure
            : WorkFailureAxisCoverage.NotApplicable;

    private static void WriteReport(string report)
    {
        string directory = Path.GetDirectoryName(ReportPath);
        if (!string.IsNullOrWhiteSpace(directory))
            Directory.CreateDirectory(directory);
        File.WriteAllText(ReportPath, report, new UTF8Encoding(false));
        AssetDatabase.Refresh();
    }

    private readonly struct MatrixEvaluation
    {
        public MatrixEvaluation(bool passed, string summary, string report)
        {
            Passed = passed;
            Summary = summary;
            Report = report;
        }

        public bool Passed { get; }
        public string Summary { get; }
        public string Report { get; }
    }
}
#endif
