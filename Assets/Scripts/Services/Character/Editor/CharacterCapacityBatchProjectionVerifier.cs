#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Newtonsoft.Json.Linq;
using DiagnosticsProcess = System.Diagnostics.Process;
using DiagnosticsStopwatch = System.Diagnostics.Stopwatch;

public static class CharacterCapacityBatchProjectionVerifier
{
    public static JObject Verify(
        CharacterActor actor,
        IAnatomyHealthRuntime anatomy,
        IAnatomyProfileCatalog profiles,
        CharacterDerivedStatsSnapshotProjector effects,
        ICharacterPerformanceQuery performance)
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        if (anatomy == null) throw new ArgumentNullException(nameof(anatomy));
        if (profiles == null) throw new ArgumentNullException(nameof(profiles));
        if (effects == null) throw new ArgumentNullException(nameof(effects));
        if (performance == null) throw new ArgumentNullException(nameof(performance));

        using DiagnosticsProcess process = DiagnosticsProcess.GetCurrentProcess();
        Measurement reference = Measure(
            process,
            () => BuildSingleTargetReference(actor, anatomy, profiles, effects));
        Measurement production = Measure(
            process,
            () => performance.GetFunctionalCapacities(actor));

        JArray capacities = new JArray();
        int checkedTraceCount = 0;
        foreach (CharacterFunctionalCapacityId capacityId in Enum
                     .GetValues(typeof(CharacterFunctionalCapacityId))
                     .Cast<CharacterFunctionalCapacityId>())
        {
            CharacterFunctionalCapacityValue expected =
                reference.Snapshot.Get(capacityId);
            CharacterFunctionalCapacityValue actual =
                production.Snapshot.Get(capacityId);
            checkedTraceCount += CompareCapacity(expected, actual);
            capacities.Add(new JObject
            {
                ["capacityId"] = expected.StableId,
                ["isApplicable"] = expected.IsApplicable,
                ["value"] = expected.Value,
                ["nonApplicableReason"] = expected.NonApplicableReason,
                ["traceCount"] = expected.Contributions.Count
            });
        }

        return new JObject
        {
            ["passed"] = true,
            ["actorId"] = actor.Identity?.PersistentId ?? string.Empty,
            ["caps"] = capacities,
            ["checks"] = new JObject
            {
                ["capacityCount"] = capacities.Count,
                ["traceCount"] = checkedTraceCount,
                ["valuesExact"] = true,
                ["applicabilityExact"] = true,
                ["nonApplicableReasonsOrdinalExact"] = true,
                ["traceFieldsAndOrderExact"] = true,
                ["performanceThresholdApplied"] = false
            },
            ["elapsed"] = new JObject
            {
                ["reference"] = reference.ToJson(),
                ["production"] = production.ToJson(),
                ["cpuScope"] = "process-total-delta",
                ["timingsArePassCriteria"] = false
            },
            ["fullAuditClosure"] = false,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false
        };
    }

    private static CharacterFunctionalCapacitySnapshot BuildSingleTargetReference(
        CharacterActor actor,
        IAnatomyHealthRuntime anatomy,
        IAnatomyProfileCatalog profiles,
        CharacterDerivedStatsSnapshotProjector effects)
    {
        AnatomyHealthSnapshot anatomySnapshot = anatomy.GetAnatomySnapshot(actor);
        if (!profiles.TryGet(
                anatomySnapshot.ProfileId,
                out AnatomyProfileDefinition profile))
        {
            throw new InvalidOperationException(
                $"Anatomy profile '{anatomySnapshot.ProfileId}' is not available for character "
                + $"'{actor.Identity?.PersistentId}'.");
        }

        Dictionary<string, AnatomyNodeHealthState> nodeStates = anatomySnapshot.Nodes
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.nodeId))
            .ToDictionary(value => value.nodeId, StringComparer.Ordinal);
        List<CharacterFunctionalCapacityValue> values = new();
        foreach (CharacterFunctionalCapacityId capacityId in Enum
                     .GetValues(typeof(CharacterFunctionalCapacityId))
                     .Cast<CharacterFunctionalCapacityId>())
        {
            AnatomyFunction function = ToAnatomyFunction(capacityId);
            AnatomyNodeDefinition[] producers = profile.Nodes
                .Where(value => (value.ExpandedFunctions & function) != 0)
                .ToArray();
            if (producers.Length == 0)
            {
                if (capacityId == CharacterFunctionalCapacityId.ArcaneConduction)
                {
                    throw new InvalidOperationException(
                        $"Anatomy profile '{profile.ProfileId}' has no arcane-conduction producer. "
                        + "Arcane conduction must be numeric for every species.");
                }
                if (!profile.TryGetNotApplicableReason(capacityId, out string reason))
                {
                    throw new InvalidOperationException(
                        $"Anatomy profile '{profile.ProfileId}' has neither a producer nor "
                        + $"an explicit N/A reason for {CharacterFunctionalCapacityIds.GetStableId(capacityId)}.");
                }
                values.Add(new CharacterFunctionalCapacityValue(
                    capacityId,
                    false,
                    0f,
                    reason,
                    Array.Empty<CharacterPerformanceContributionTrace>()));
                continue;
            }

            float weightedTotal = 0f;
            float totalWeight = 0f;
            List<CharacterPerformanceContributionTrace> trace = new();
            foreach (AnatomyNodeDefinition producer in producers)
            {
                float weight = Math.Max(0.01f, producer.CapacityWeight);
                float efficiency = nodeStates.TryGetValue(
                        producer.NodeId,
                        out AnatomyNodeHealthState node)
                    ? node.FunctionalEfficiency
                    : 0f;
                weightedTotal += efficiency * weight;
                totalWeight += weight;
                trace.Add(new CharacterPerformanceContributionTrace
                {
                    SourceKind = "anatomy-node",
                    SourceId = producer.NodeId,
                    TargetId = CharacterFunctionalCapacityIds.GetStableId(capacityId),
                    AuthoredValue = weight,
                    AppliedValue = efficiency * weight,
                    Detail = $"{producer.DisplayName}: efficiency {efficiency:0.###} × weight {weight:0.###}"
                });
            }
            float value = totalWeight > 0f ? weightedTotal / totalWeight : 0f;
            if (capacityId == CharacterFunctionalCapacityId.PowerCirculation)
            {
                value = anatomySnapshot.PowerCirculation;
                trace.Add(new CharacterPerformanceContributionTrace
                {
                    SourceKind = "health-state",
                    SourceId = "blood-loss",
                    TargetId = CharacterFunctionalCapacityIds.PowerCirculation,
                    AuthoredValue = 1f,
                    AppliedValue = value,
                    Detail = "Power circulation includes current blood-loss pressure."
                });
            }
            string targetId = CharacterFunctionalCapacityIds.GetStableId(capacityId);
            GameplayEffectProjectionResult projection = effects.ProjectValue(
                actor,
                targetId,
                value);
            value = projection.Value;
            if (float.IsNaN(value) || float.IsInfinity(value) || value < 0f)
            {
                throw new InvalidOperationException(
                    $"Capacity projection '{targetId}' produced invalid value {value} "
                    + $"for character '{actor.Identity?.PersistentId}'.");
            }
            trace.AddRange(projection.Contributions.Select(contribution =>
                new CharacterPerformanceContributionTrace
                {
                    SourceKind = contribution.Source.Kind.ToString(),
                    SourceId = contribution.Source.SourceId,
                    TargetId = targetId,
                    AuthoredValue = contribution.AuthoredValue,
                    AppliedValue = contribution.AppliedValue,
                    Detail = contribution.Suppressed
                        ? $"suppressed: {contribution.SuppressionReason}"
                        : contribution.EffectId
                }));
            values.Add(new CharacterFunctionalCapacityValue(
                capacityId,
                true,
                value,
                string.Empty,
                trace));
        }
        return new CharacterFunctionalCapacitySnapshot(values);
    }

    private static int CompareCapacity(
        CharacterFunctionalCapacityValue expected,
        CharacterFunctionalCapacityValue actual)
    {
        string capacityId = expected.StableId;
        RequireEqual(
            expected.CapacityId == actual.CapacityId,
            capacityId,
            "capacity-id",
            expected.CapacityId.ToString(),
            actual.CapacityId.ToString());
        RequireEqual(
            expected.IsApplicable == actual.IsApplicable,
            capacityId,
            "is-applicable",
            expected.IsApplicable.ToString(),
            actual.IsApplicable.ToString());
        RequireEqual(
            expected.Value.Equals(actual.Value),
            capacityId,
            "value",
            FormatFloat(expected.Value),
            FormatFloat(actual.Value));
        RequireEqual(
            string.Equals(
                expected.NonApplicableReason,
                actual.NonApplicableReason,
                StringComparison.Ordinal),
            capacityId,
            "non-applicable-reason",
            expected.NonApplicableReason,
            actual.NonApplicableReason);
        RequireEqual(
            expected.Contributions.Count == actual.Contributions.Count,
            capacityId,
            "trace-count",
            expected.Contributions.Count.ToString(CultureInfo.InvariantCulture),
            actual.Contributions.Count.ToString(CultureInfo.InvariantCulture));

        for (int index = 0; index < expected.Contributions.Count; index++)
        {
            CompareTrace(
                capacityId,
                index,
                expected.Contributions[index],
                actual.Contributions[index]);
        }
        return expected.Contributions.Count;
    }

    private static void CompareTrace(
        string capacityId,
        int index,
        CharacterPerformanceContributionTrace expected,
        CharacterPerformanceContributionTrace actual)
    {
        if (actual == null)
        {
            throw new InvalidOperationException(
                $"Capacity batch projection mismatch: capacity={capacityId}; "
                + $"traceIndex={index}; field=trace; expected={DescribeTrace(expected)}; actual=<null>.");
        }
        if (expected == null)
        {
            throw new InvalidOperationException(
                $"Capacity batch projection reference is invalid: capacity={capacityId}; "
                + $"traceIndex={index}; expected=<null>; actual={DescribeTrace(actual)}.");
        }

        RequireTraceString(capacityId, index, "source-kind", expected.SourceKind, actual.SourceKind);
        RequireTraceString(capacityId, index, "source-id", expected.SourceId, actual.SourceId);
        RequireTraceString(capacityId, index, "target-id", expected.TargetId, actual.TargetId);
        RequireTraceFloat(
            capacityId,
            index,
            "authored-value",
            expected.AuthoredValue,
            actual.AuthoredValue);
        RequireTraceFloat(
            capacityId,
            index,
            "applied-value",
            expected.AppliedValue,
            actual.AppliedValue);
        RequireTraceString(capacityId, index, "detail", expected.Detail, actual.Detail);
    }

    private static void RequireTraceString(
        string capacityId,
        int index,
        string field,
        string expected,
        string actual)
    {
        if (!string.Equals(expected, actual, StringComparison.Ordinal))
        {
            throw TraceMismatch(capacityId, index, field, expected, actual);
        }
    }

    private static void RequireTraceFloat(
        string capacityId,
        int index,
        string field,
        float expected,
        float actual)
    {
        if (!expected.Equals(actual))
        {
            throw TraceMismatch(
                capacityId,
                index,
                field,
                FormatFloat(expected),
                FormatFloat(actual));
        }
    }

    private static InvalidOperationException TraceMismatch(
        string capacityId,
        int index,
        string field,
        string expected,
        string actual) => new InvalidOperationException(
        $"Capacity batch projection mismatch: capacity={capacityId}; "
        + $"traceIndex={index}; field={field}; expected={expected ?? "<null>"}; "
        + $"actual={actual ?? "<null>"}.");

    private static void RequireEqual(
        bool condition,
        string capacityId,
        string field,
        string expected,
        string actual)
    {
        if (!condition)
        {
            throw new InvalidOperationException(
                $"Capacity batch projection mismatch: capacity={capacityId}; "
                + $"field={field}; expected={expected ?? "<null>"}; "
                + $"actual={actual ?? "<null>"}.");
        }
    }

    private static string DescribeTrace(
        CharacterPerformanceContributionTrace trace) => trace == null
        ? "<null>"
        : $"{trace.SourceKind}|{trace.SourceId}|{trace.TargetId}|"
          + $"{FormatFloat(trace.AuthoredValue)}|{FormatFloat(trace.AppliedValue)}|"
          + trace.Detail;

    private static string FormatFloat(float value) =>
        value.ToString("R", CultureInfo.InvariantCulture)
        + $"/0x{BitConverter.SingleToInt32Bits(value):X8}";

    private static Measurement Measure(
        DiagnosticsProcess process,
        Func<CharacterFunctionalCapacitySnapshot> capture)
    {
        TimeSpan cpuAtStart = process.TotalProcessorTime;
        long allocatedAtStart = GC.GetAllocatedBytesForCurrentThread();
        long wallAtStart = DiagnosticsStopwatch.GetTimestamp();
        CharacterFunctionalCapacitySnapshot snapshot = capture();
        long wallAtEnd = DiagnosticsStopwatch.GetTimestamp();
        long allocatedAtEnd = GC.GetAllocatedBytesForCurrentThread();
        TimeSpan cpuAtEnd = process.TotalProcessorTime;
        return new Measurement(
            snapshot,
            (wallAtEnd - wallAtStart)
            * 1000.0
            / DiagnosticsStopwatch.Frequency,
            (cpuAtEnd - cpuAtStart).TotalMilliseconds,
            Math.Max(0L, allocatedAtEnd - allocatedAtStart));
    }

    private static AnatomyFunction ToAnatomyFunction(
        CharacterFunctionalCapacityId capacityId) => capacityId switch
    {
        CharacterFunctionalCapacityId.MentalMaintenance => AnatomyFunction.MentalMaintenance,
        CharacterFunctionalCapacityId.VisualDiscernment => AnatomyFunction.VisualDiscernment,
        CharacterFunctionalCapacityId.AuditorySensing => AnatomyFunction.AuditorySensing,
        CharacterFunctionalCapacityId.RespiratoryExchange => AnatomyFunction.RespiratoryExchange,
        CharacterFunctionalCapacityId.PowerCirculation => AnatomyFunction.PowerCirculation,
        CharacterFunctionalCapacityId.IntakeProcessing => AnatomyFunction.IntakeProcessing,
        CharacterFunctionalCapacityId.PurificationProcessing => AnatomyFunction.PurificationProcessing,
        CharacterFunctionalCapacityId.VitalityResponse => AnatomyFunction.VitalityResponse,
        CharacterFunctionalCapacityId.PhysicalPower => AnatomyFunction.PhysicalPower,
        CharacterFunctionalCapacityId.PrecisionManipulation => AnatomyFunction.PrecisionManipulation,
        CharacterFunctionalCapacityId.PhysicalMobility => AnatomyFunction.PhysicalMobility,
        CharacterFunctionalCapacityId.Communication => AnatomyFunction.Communication,
        CharacterFunctionalCapacityId.ArcaneConduction => AnatomyFunction.ArcaneConduction,
        CharacterFunctionalCapacityId.ImmuneDefense => AnatomyFunction.ImmuneDefense,
        _ => throw new ArgumentOutOfRangeException(nameof(capacityId), capacityId, null)
    };

    private readonly struct Measurement
    {
        public Measurement(
            CharacterFunctionalCapacitySnapshot snapshot,
            double wallMilliseconds,
            double processCpuMilliseconds,
            long allocatedBytes)
        {
            Snapshot = snapshot;
            WallMilliseconds = wallMilliseconds;
            ProcessCpuMilliseconds = processCpuMilliseconds;
            AllocatedBytes = allocatedBytes;
        }

        public CharacterFunctionalCapacitySnapshot Snapshot { get; }
        public double WallMilliseconds { get; }
        public double ProcessCpuMilliseconds { get; }
        public long AllocatedBytes { get; }

        public JObject ToJson() => new JObject
        {
            ["wallMilliseconds"] = WallMilliseconds,
            ["processCpuMilliseconds"] = ProcessCpuMilliseconds,
            ["allocatedBytes"] = AllocatedBytes
        };
    }
}
#endif
