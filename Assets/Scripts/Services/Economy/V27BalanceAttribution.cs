using System;
using System.Collections.Generic;
using System.Linq;

namespace DungeonStory.Balance
{
    public enum BalanceAnomalySeverity
    {
        None = 0,
        Warning = 1,
        Critical = 2
    }

    public enum BalanceAnomalyDisposition
    {
        None = 0,
        RootCritical = 1,
        LocalCritical = 2,
        CollapsedInheritedOnly = 3,
        CollapsedRoundingOnly = 4,
        CollapsedMultiRoot = 5,
        Approved = 6
    }

    [BalanceImmutableRecord]
    public sealed class BalanceAttributionResult
    {
        internal BalanceAttributionResult(
            long totalDelta,
            long inheritedDelta,
            long rawLocalDelta,
            int roundingEnvelope,
            BalanceAnomalyDisposition disposition,
            IReadOnlyList<string> rootCauseIds)
        {
            TotalDelta = totalDelta;
            InheritedDelta = inheritedDelta;
            RawLocalDelta = rawLocalDelta;
            RoundingEnvelope = roundingEnvelope;
            Disposition = disposition;
            RootCauseIds = rootCauseIds ?? Array.Empty<string>();
        }

        public long TotalDelta { get; }
        public long InheritedDelta { get; }
        public long RawLocalDelta { get; }
        public int RoundingEnvelope { get; }
        public BalanceAnomalyDisposition Disposition { get; }
        public IReadOnlyList<string> RootCauseIds { get; }
    }

    [BalanceCaptureFactory]
    public static class BalanceAttribution
    {
        public static BalanceAttributionResult Attribute(
            long beforeMilliEwu,
            long upstreamOnlyAfterMilliEwu,
            long fullAfterMilliEwu,
            bool localFingerprintIdentical,
            bool changeOriginatesOnlyUpstream,
            int localQuantizationBoundaryCount,
            IEnumerable<string> rootCauseIds,
            bool isCritical)
        {
            if (beforeMilliEwu < 0L || upstreamOnlyAfterMilliEwu < 0L || fullAfterMilliEwu < 0L)
                throw new ArgumentOutOfRangeException(nameof(beforeMilliEwu));
            if (localQuantizationBoundaryCount < 0)
                throw new ArgumentOutOfRangeException(nameof(localQuantizationBoundaryCount));

            long inherited = checked(upstreamOnlyAfterMilliEwu - beforeMilliEwu);
            long local = checked(fullAfterMilliEwu - upstreamOnlyAfterMilliEwu);
            long total = checked(fullAfterMilliEwu - beforeMilliEwu);
            int envelope = Math.Min(2, localQuantizationBoundaryCount);
            string[] roots = (rootCauseIds ?? Array.Empty<string>())
                .Select(value => BalanceCanonicalText.StableId(value, "rootCauseId"))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();

            BalanceAnomalyDisposition disposition;
            if (!isCritical)
            {
                disposition = BalanceAnomalyDisposition.None;
            }
            else if (localFingerprintIdentical
                     && changeOriginatesOnlyUpstream
                     && local == 0L
                     && roots.Length == 1)
            {
                disposition = BalanceAnomalyDisposition.CollapsedInheritedOnly;
            }
            else if (localFingerprintIdentical
                     && changeOriginatesOnlyUpstream
                     && AbsWithoutOverflow(local) <= (ulong)envelope
                     && roots.Length == 1)
            {
                disposition = BalanceAnomalyDisposition.CollapsedRoundingOnly;
            }
            else if (localFingerprintIdentical
                     && changeOriginatesOnlyUpstream
                     && AbsWithoutOverflow(local) <= (ulong)envelope
                     && roots.Length > 1)
            {
                disposition = BalanceAnomalyDisposition.CollapsedMultiRoot;
            }
            else
            {
                disposition = roots.Length == 0
                    ? BalanceAnomalyDisposition.RootCritical
                    : BalanceAnomalyDisposition.LocalCritical;
            }

            return new BalanceAttributionResult(
                total,
                inherited,
                local,
                envelope,
                disposition,
                Array.AsReadOnly(roots));
        }

        private static ulong AbsWithoutOverflow(long value) =>
            value >= 0L ? (ulong)value : (ulong)(-(value + 1L)) + 1UL;
    }

    [BalanceImmutableRecord]
    public sealed class BalanceReviewApproval
    {
        private BalanceReviewApproval(
            string rootStableId,
            string metric,
            string exactAfterValue,
            string dependencyFingerprint,
            string sourceDigest,
            string reasonCode,
            string balanceBaselineRecordId)
        {
            RootStableId = rootStableId;
            Metric = metric;
            ExactAfterValue = exactAfterValue;
            DependencyFingerprint = dependencyFingerprint;
            SourceDigest = sourceDigest;
            ReasonCode = reasonCode;
            BalanceBaselineRecordId = balanceBaselineRecordId;
        }

        public string RootStableId { get; }
        public string Metric { get; }
        public string ExactAfterValue { get; }
        public string DependencyFingerprint { get; }
        public string SourceDigest { get; }
        public string ReasonCode { get; }
        public string BalanceBaselineRecordId { get; }

        [BalanceCaptureFactory]
        public static BalanceReviewApproval Capture(
            string rootStableId,
            string metric,
            string exactAfterValue,
            string dependencyFingerprint,
            string sourceDigest,
            string reasonCode,
            string balanceBaselineRecordId)
        {
            return new BalanceReviewApproval(
                BalanceCanonicalText.StableId(rootStableId, nameof(rootStableId)),
                BalanceCanonicalText.StableId(metric, nameof(metric)),
                BalanceCanonicalText.Display(exactAfterValue),
                RequireDigest(dependencyFingerprint, nameof(dependencyFingerprint)),
                RequireDigest(sourceDigest, nameof(sourceDigest)),
                BalanceCanonicalText.StableId(reasonCode, nameof(reasonCode)),
                BalanceCanonicalText.StableId(
                    balanceBaselineRecordId,
                    nameof(balanceBaselineRecordId)));
        }

        public bool Matches(
            string rootStableId,
            string metric,
            string exactAfterValue,
            string dependencyFingerprint,
            string sourceDigest) =>
            string.Equals(RootStableId, rootStableId, StringComparison.Ordinal)
            && string.Equals(Metric, metric, StringComparison.Ordinal)
            && string.Equals(ExactAfterValue, exactAfterValue, StringComparison.Ordinal)
            && string.Equals(DependencyFingerprint, dependencyFingerprint, StringComparison.Ordinal)
            && string.Equals(SourceDigest, sourceDigest, StringComparison.Ordinal);

        private static string RequireDigest(string value, string name)
        {
            string canonical = BalanceCanonicalText.Display(value);
            if (canonical.Length != 64 || canonical.Any(character =>
                    !(character >= '0' && character <= '9')
                    && !(character >= 'a' && character <= 'f')))
            {
                throw new InvalidOperationException($"{name} must be lowercase SHA-256.");
            }
            return canonical;
        }
    }

    [BalanceImmutableRecord]
    public sealed class BalanceAnomalyNode
    {
        internal BalanceAnomalyNode(
            string stableId,
            string metric,
            BalanceAnomalySeverity severity,
            BalanceAnomalyDisposition disposition,
            string reasonCode,
            IReadOnlyList<string> rootCauseIds)
        {
            StableId = stableId;
            Metric = metric;
            Severity = severity;
            Disposition = disposition;
            ReasonCode = reasonCode;
            RootCauseIds = rootCauseIds ?? Array.Empty<string>();
        }

        public string StableId { get; }
        public string Metric { get; }
        public BalanceAnomalySeverity Severity { get; }
        public BalanceAnomalyDisposition Disposition { get; }
        public string ReasonCode { get; }
        public IReadOnlyList<string> RootCauseIds { get; }
        public bool EmitsCiAnnotation => Severity == BalanceAnomalySeverity.Critical
            && (Disposition == BalanceAnomalyDisposition.RootCritical
                || Disposition == BalanceAnomalyDisposition.LocalCritical);

        [BalanceCaptureFactory]
        public static BalanceAnomalyNode Capture(
            string stableId,
            string metric,
            BalanceAnomalySeverity severity,
            BalanceAnomalyDisposition disposition,
            string reasonCode,
            IEnumerable<string> rootCauseIds)
        {
            string[] roots = (rootCauseIds ?? Array.Empty<string>())
                .Select(value => BalanceCanonicalText.StableId(value, "anomaly root"))
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            return new BalanceAnomalyNode(
                BalanceCanonicalText.StableId(stableId, nameof(stableId)),
                BalanceCanonicalText.StableId(metric, nameof(metric)),
                severity,
                disposition,
                BalanceCanonicalText.StableId(reasonCode, nameof(reasonCode)),
                Array.AsReadOnly(roots));
        }
    }

    public static class BalanceAnomalyDetector
    {
        public static BalanceAnomalySeverity ClassifyPercentDelta(decimal absolutePercentDelta)
        {
            if (absolutePercentDelta < 0m)
                throw new ArgumentOutOfRangeException(nameof(absolutePercentDelta));
            if (absolutePercentDelta > 300m)
                return BalanceAnomalySeverity.Critical;
            if (absolutePercentDelta >= 100m)
                return BalanceAnomalySeverity.Warning;
            return BalanceAnomalySeverity.None;
        }

        public static BalanceAnomalySeverity ClassifyPrimitiveDelta(
            decimal absolutePercentDelta,
            int downstreamConsumers)
        {
            if (absolutePercentDelta < 0m)
                throw new ArgumentOutOfRangeException(nameof(absolutePercentDelta));
            if (downstreamConsumers < 0)
                throw new ArgumentOutOfRangeException(nameof(downstreamConsumers));
            if (absolutePercentDelta > 50m && downstreamConsumers > 10)
                return BalanceAnomalySeverity.Critical;
            if (absolutePercentDelta >= 25m)
                return BalanceAnomalySeverity.Warning;
            return BalanceAnomalySeverity.None;
        }

        public static BalanceAnomalySeverity ClassifyLaborDensity(decimal ratio)
        {
            if (ratio < 0m)
                throw new ArgumentOutOfRangeException(nameof(ratio));
            if (ratio < 0.67m || ratio > 1.50m)
                return BalanceAnomalySeverity.Critical;
            if (ratio < 0.80m || ratio > 1.25m)
                return BalanceAnomalySeverity.Warning;
            return BalanceAnomalySeverity.None;
        }
    }

    [BalanceImmutableRecord]
    public sealed class BalanceTransform
    {
        private BalanceTransform(
            string transformId,
            IReadOnlyList<string> inputItemIds,
            IReadOnlyList<string> outputItemIds,
            long inputDebitMilliEwu,
            long outputCreditMilliEwu)
        {
            TransformId = transformId;
            InputItemIds = inputItemIds;
            OutputItemIds = outputItemIds;
            InputDebitMilliEwu = inputDebitMilliEwu;
            OutputCreditMilliEwu = outputCreditMilliEwu;
        }

        public string TransformId { get; }
        public IReadOnlyList<string> InputItemIds { get; }
        public IReadOnlyList<string> OutputItemIds { get; }
        public long InputDebitMilliEwu { get; }
        public long OutputCreditMilliEwu { get; }
        public long MarginMilliEwu => checked(OutputCreditMilliEwu - InputDebitMilliEwu);

        [BalanceCaptureFactory]
        public static BalanceTransform Capture(
            string transformId,
            IEnumerable<string> inputItemIds,
            IEnumerable<string> outputItemIds,
            long inputDebitMilliEwu,
            long outputCreditMilliEwu)
        {
            if (inputDebitMilliEwu < 0L || outputCreditMilliEwu < 0L)
                throw new ArgumentOutOfRangeException(nameof(inputDebitMilliEwu));
            string[] inputs = CaptureIds(inputItemIds, "transform input");
            string[] outputs = CaptureIds(outputItemIds, "transform output");
            if (inputs.Length == 0 || outputs.Length == 0)
                throw new InvalidOperationException("A transform requires inputs and outputs.");
            return new BalanceTransform(
                BalanceCanonicalText.StableId(transformId, nameof(transformId)),
                Array.AsReadOnly(inputs),
                Array.AsReadOnly(outputs),
                inputDebitMilliEwu,
                outputCreditMilliEwu);
        }

        private static string[] CaptureIds(IEnumerable<string> values, string name) =>
            (values ?? Array.Empty<string>())
            .Select(value => BalanceCanonicalText.StableId(value, name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    [BalanceImmutableRecord]
    public sealed class BalanceSccAuditResult
    {
        internal BalanceSccAuditResult(
            IReadOnlyList<IReadOnlyList<string>> components,
            IReadOnlyList<string> violatingTransformIds,
            long minimumMarginMilliEwu)
        {
            Components = components;
            ViolatingTransformIds = violatingTransformIds;
            MinimumMarginMilliEwu = minimumMarginMilliEwu;
        }

        public IReadOnlyList<IReadOnlyList<string>> Components { get; }
        public IReadOnlyList<string> ViolatingTransformIds { get; }
        public long MinimumMarginMilliEwu { get; }
        public bool Passed => ViolatingTransformIds.Count == 0;
    }

    [BalanceCaptureFactory]
    public static class BalanceSccAuditor
    {
        public static BalanceSccAuditResult Audit(IEnumerable<BalanceTransform> source)
        {
            BalanceTransform[] transforms = (source ?? throw new ArgumentNullException(nameof(source)))
                .Where(value => value != null)
                .OrderBy(value => value.TransformId, StringComparer.Ordinal)
                .ToArray();
            Dictionary<string, HashSet<string>> graph = new Dictionary<string, HashSet<string>>(
                StringComparer.Ordinal);
            foreach (BalanceTransform transform in transforms)
            {
                foreach (string input in transform.InputItemIds)
                {
                    if (!graph.TryGetValue(input, out HashSet<string> edges))
                    {
                        edges = new HashSet<string>(StringComparer.Ordinal);
                        graph.Add(input, edges);
                    }
                    foreach (string output in transform.OutputItemIds)
                        edges.Add(output);
                }
                foreach (string output in transform.OutputItemIds)
                    if (!graph.ContainsKey(output))
                        graph.Add(output, new HashSet<string>(StringComparer.Ordinal));
            }

            TarjanState state = new TarjanState(graph);
            foreach (string node in graph.Keys.OrderBy(value => value, StringComparer.Ordinal))
                if (!state.Indices.ContainsKey(node))
                    StrongConnect(node, state);

            string[] violations = transforms
                .Where(value => value.MarginMilliEwu >= 0L)
                .Select(value => value.TransformId)
                .ToArray();
            long minimumMargin = transforms.Length == 0
                ? 0L
                : transforms.Min(value => value.MarginMilliEwu);
            IReadOnlyList<string>[] components = state.Components
                .OrderBy(component => component[0], StringComparer.Ordinal)
                .Cast<IReadOnlyList<string>>()
                .ToArray();
            return new BalanceSccAuditResult(
                Array.AsReadOnly(components),
                Array.AsReadOnly(violations),
                minimumMargin);
        }

        private static void StrongConnect(string node, TarjanState state)
        {
            state.Indices[node] = state.NextIndex;
            state.LowLinks[node] = state.NextIndex;
            state.NextIndex++;
            state.Stack.Push(node);
            state.OnStack.Add(node);

            foreach (string target in state.Graph[node].OrderBy(value => value, StringComparer.Ordinal))
            {
                if (!state.Indices.ContainsKey(target))
                {
                    StrongConnect(target, state);
                    state.LowLinks[node] = Math.Min(state.LowLinks[node], state.LowLinks[target]);
                }
                else if (state.OnStack.Contains(target))
                {
                    state.LowLinks[node] = Math.Min(state.LowLinks[node], state.Indices[target]);
                }
            }

            if (state.LowLinks[node] != state.Indices[node])
                return;
            List<string> component = new List<string>();
            string current;
            do
            {
                current = state.Stack.Pop();
                state.OnStack.Remove(current);
                component.Add(current);
            }
            while (!string.Equals(current, node, StringComparison.Ordinal));
            component.Sort(StringComparer.Ordinal);
            state.Components.Add(Array.AsReadOnly(component.ToArray()));
        }

        private sealed class TarjanState
        {
            public TarjanState(Dictionary<string, HashSet<string>> graph)
            {
                Graph = graph;
            }

            public Dictionary<string, HashSet<string>> Graph { get; }
            public Dictionary<string, int> Indices { get; } =
                new Dictionary<string, int>(StringComparer.Ordinal);
            public Dictionary<string, int> LowLinks { get; } =
                new Dictionary<string, int>(StringComparer.Ordinal);
            public Stack<string> Stack { get; } = new Stack<string>();
            public HashSet<string> OnStack { get; } = new HashSet<string>(StringComparer.Ordinal);
            public List<IReadOnlyList<string>> Components { get; } =
                new List<IReadOnlyList<string>>();
            public int NextIndex { get; set; }
        }
    }

    public sealed class BalanceDependencyGraph
    {
        private readonly Dictionary<string, string[]> dependencies;
        private readonly Dictionary<string, string[]> consumers;

        public BalanceDependencyGraph(FrozenBalanceLedger ledger)
        {
            if (ledger == null)
                throw new ArgumentNullException(nameof(ledger));
            Dictionary<string, HashSet<string>> dependencySets =
                new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            Dictionary<string, HashSet<string>> consumerSets =
                new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
            foreach (CanonicalBalanceMetricRecord record in ledger.Records)
            {
                if (!dependencySets.TryGetValue(record.StableId, out HashSet<string> own))
                {
                    own = new HashSet<string>(StringComparer.Ordinal);
                    dependencySets.Add(record.StableId, own);
                }
                foreach (string dependency in record.DependencyIds)
                {
                    own.Add(dependency);
                    if (!consumerSets.TryGetValue(dependency, out HashSet<string> downstream))
                    {
                        downstream = new HashSet<string>(StringComparer.Ordinal);
                        consumerSets.Add(dependency, downstream);
                    }
                    downstream.Add(record.StableId);
                }
            }
            dependencies = FreezeMap(dependencySets);
            consumers = FreezeMap(consumerSets);
        }

        public IReadOnlyList<string> GetDependencies(string stableId) =>
            dependencies.TryGetValue(stableId ?? string.Empty, out string[] values)
                ? Array.AsReadOnly(values)
                : Array.Empty<string>();

        public IReadOnlyList<string> GetConsumers(string stableId) =>
            consumers.TryGetValue(stableId ?? string.Empty, out string[] values)
                ? Array.AsReadOnly(values)
                : Array.Empty<string>();

        private static Dictionary<string, string[]> FreezeMap(
            Dictionary<string, HashSet<string>> source)
        {
            Dictionary<string, string[]> result = new Dictionary<string, string[]>(
                source.Count,
                StringComparer.Ordinal);
            foreach (KeyValuePair<string, HashSet<string>> pair in source)
            {
                string[] values = pair.Value.OrderBy(value => value, StringComparer.Ordinal).ToArray();
                result.Add(pair.Key, values);
            }
            return result;
        }
    }
}
