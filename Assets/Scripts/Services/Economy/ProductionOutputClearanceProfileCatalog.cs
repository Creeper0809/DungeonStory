using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// One clean, deterministic haul-clearance observation attributed to the
/// authored facility profile that produced it. The observation is an audit
/// input only; gameplay consumes the immutable aggregated profile catalog.
/// </summary>
public readonly struct ProductionOutputClearanceProfileObservation
{
    public ProductionOutputClearanceProfileObservation(
        string definitionId,
        string workstationTag,
        int deterministicSeed,
        string batchCommitId,
        long clearanceMicroHours,
        string runSourceDigest,
        long actualBatchMassGrams = 1L)
    {
        RequireCanonical(definitionId, nameof(definitionId));
        RequireCanonical(workstationTag, nameof(workstationTag));
        RequireCanonical(batchCommitId, nameof(batchCommitId));
        if (clearanceMicroHours <= 0L)
            throw new ArgumentOutOfRangeException(nameof(clearanceMicroHours));
        if (actualBatchMassGrams <= 0L)
            throw new ArgumentOutOfRangeException(nameof(actualBatchMassGrams));
        if (!IsLowercaseSha256(runSourceDigest))
        {
            throw new ArgumentException(
                "Clearance observation source digest must be lowercase SHA-256.",
                nameof(runSourceDigest));
        }

        DefinitionId = definitionId;
        WorkstationTag = workstationTag;
        DeterministicSeed = deterministicSeed;
        BatchCommitId = batchCommitId;
        ClearanceMicroHours = clearanceMicroHours;
        ActualBatchMassGrams = actualBatchMassGrams;
        RunSourceDigest = runSourceDigest;
    }

    public string DefinitionId { get; }
    public string WorkstationTag { get; }
    public int DeterministicSeed { get; }
    public string BatchCommitId { get; }
    public long ClearanceMicroHours { get; }
    public long ActualBatchMassGrams { get; }
    public string RunSourceDigest { get; }

    public static void RequireCanonical(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.IndexOf(';') >= 0
            || value.IndexOf('\r') >= 0
            || value.IndexOf('\n') >= 0)
        {
            throw new ArgumentException(
                "Production output-clearance identity must be canonical.",
                parameterName);
        }
    }

    public static bool IsLowercaseSha256(string value)
    {
        if (value == null || value.Length != 64)
            return false;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (!(character is >= '0' and <= '9'
                || character is >= 'a' and <= 'f'))
            {
                return false;
            }
        }
        return true;
    }
}

/// <summary>
/// Authored-reachable peak production envelope. It is calculated from the
/// maximum feasible support/work-speed assignment, not the currently attached
/// runtime supports, so attach/detach never silently shrinks buffer authority.
/// </summary>
public readonly struct ProductionOutputThroughputEnvelopeSnapshot
{
    public ProductionOutputThroughputEnvelopeSnapshot(
        string definitionId,
        string workstationTag,
        long peakOutputMassGramsPerHour,
        string sourceDigest)
    {
        ProductionOutputClearanceProfileObservation.RequireCanonical(
            definitionId,
            nameof(definitionId));
        ProductionOutputClearanceProfileObservation.RequireCanonical(
            workstationTag,
            nameof(workstationTag));
        if (peakOutputMassGramsPerHour <= 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(peakOutputMassGramsPerHour));
        }
        if (!ProductionOutputClearanceProfileObservation
            .IsLowercaseSha256(sourceDigest))
        {
            throw new ArgumentException(
                "Throughput envelope source digest must be lowercase SHA-256.",
                nameof(sourceDigest));
        }

        DefinitionId = definitionId;
        WorkstationTag = workstationTag;
        PeakOutputMassGramsPerHour = peakOutputMassGramsPerHour;
        SourceDigest = sourceDigest;
    }

    public string DefinitionId { get; }
    public string WorkstationTag { get; }
    public long PeakOutputMassGramsPerHour { get; }
    public string SourceDigest { get; }
    public string AuthorityDigest => SourceDigest;
}

/// <summary>
/// Frozen reviewable row used by runtime clearance lookup. All provenance is
/// retained in the row digest; runtime does not read raw telemetry or save DTOs.
/// </summary>
public sealed class ProductionOutputClearanceProfileRecord
{
    public const string Schema = "production-output-clearance-profile-record@1";

    internal ProductionOutputClearanceProfileRecord(
        string definitionId,
        string workstationTag,
        long p95HaulClearanceMilliHours,
        long peakOutputMassGramsPerHour,
        int sampleCount,
        int distinctSeedCount,
        string measurementSourceDigest,
        string throughputSourceDigest)
    {
        ProductionOutputClearanceProfileObservation.RequireCanonical(
            definitionId,
            nameof(definitionId));
        ProductionOutputClearanceProfileObservation.RequireCanonical(
            workstationTag,
            nameof(workstationTag));
        if (p95HaulClearanceMilliHours <= 0L
            || peakOutputMassGramsPerHour <= 0L
            || sampleCount <= 0
            || distinctSeedCount <= 0
            || distinctSeedCount > sampleCount
            || !ProductionOutputClearanceProfileObservation
                .IsLowercaseSha256(measurementSourceDigest)
            || !ProductionOutputClearanceProfileObservation
                .IsLowercaseSha256(throughputSourceDigest))
        {
            throw new InvalidOperationException(
                "Production output-clearance profile row is incomplete.");
        }

        DefinitionId = definitionId;
        WorkstationTag = workstationTag;
        P95HaulClearanceMilliHours = p95HaulClearanceMilliHours;
        PeakOutputMassGramsPerHour = peakOutputMassGramsPerHour;
        SampleCount = sampleCount;
        DistinctSeedCount = distinctSeedCount;
        MeasurementSourceDigest = measurementSourceDigest;
        ThroughputSourceDigest = throughputSourceDigest;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(DefinitionId);
        digest.Append(WorkstationTag);
        digest.Append(P95HaulClearanceMilliHours);
        digest.Append(PeakOutputMassGramsPerHour);
        digest.Append(SampleCount);
        digest.Append(DistinctSeedCount);
        digest.Append(MeasurementSourceDigest);
        digest.Append(ThroughputSourceDigest);
        SourceDigest = digest.ComputeSha256();
        Snapshot = new ProductionOutputClearanceProfileSnapshot(
            P95HaulClearanceMilliHours,
            PeakOutputMassGramsPerHour,
            SourceDigest);
    }

    public string DefinitionId { get; }
    public string WorkstationTag { get; }
    public long P95HaulClearanceMilliHours { get; }
    public long PeakOutputMassGramsPerHour { get; }
    public int SampleCount { get; }
    public int DistinctSeedCount { get; }
    public string MeasurementSourceDigest { get; }
    public string ThroughputSourceDigest { get; }
    public string SourceDigest { get; }
    public ProductionOutputClearanceProfileSnapshot Snapshot { get; }
}

/// <summary>
/// Deterministic nearest-rank p95 aggregation. It accepts only already-clean
/// observations and requires a throughput envelope for every profile key.
/// </summary>
public static class ProductionOutputClearanceProfileAggregator
{
    public const string MeasurementSchema =
        "production-output-clearance-profile-measurement@2";
    public const long MicroHoursPerMilliHour = 1_000L;

    public static IReadOnlyList<ProductionOutputClearanceProfileRecord>
        BuildFrozen(
            IReadOnlyList<ProductionOutputClearanceProfileObservation>
                observations,
            IReadOnlyList<ProductionOutputThroughputEnvelopeSnapshot> throughput,
            IReadOnlyList<int> expectedDeterministicSeeds,
            int expectedProfileCount)
    {
        if (observations == null)
            throw new ArgumentNullException(nameof(observations));
        if (throughput == null)
            throw new ArgumentNullException(nameof(throughput));
        if (expectedDeterministicSeeds == null)
            throw new ArgumentNullException(nameof(expectedDeterministicSeeds));
        if (expectedProfileCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(expectedProfileCount));

        int[] expectedSeeds = expectedDeterministicSeeds
            .OrderBy(value => value)
            .ToArray();
        if (expectedSeeds.Length == 0
            || expectedSeeds.Distinct().Count() != expectedSeeds.Length)
        {
            throw new InvalidOperationException(
                "Frozen clearance profiles require a nonempty unique seed cohort.");
        }
        int expectedObservationCount = checked(
            expectedProfileCount * expectedSeeds.Length);
        if (observations.Count != expectedObservationCount
            || throughput.Count != expectedProfileCount)
        {
            throw new InvalidOperationException(
                "Frozen clearance profile cardinality is incomplete.");
        }

        IGrouping<ProfileKey, ProductionOutputClearanceProfileObservation>[]
            groups = observations
                .GroupBy(value => new ProfileKey(
                    value.DefinitionId,
                    value.WorkstationTag))
                .OrderBy(group => group.Key.DefinitionId,
                    StringComparer.Ordinal)
                .ThenBy(group => group.Key.WorkstationTag,
                    StringComparer.Ordinal)
                .ToArray();
        if (groups.Length != expectedProfileCount)
        {
            throw new InvalidOperationException(
                "Frozen clearance observation profile-key set is incomplete.");
        }
        foreach (IGrouping<ProfileKey, ProductionOutputClearanceProfileObservation>
                 group in groups)
        {
            ProductionOutputClearanceProfileObservation[] samples =
                group.ToArray();
            int[] actualSeeds = samples
                .Select(value => value.DeterministicSeed)
                .OrderBy(value => value)
                .ToArray();
            if (samples.Length != expectedSeeds.Length
                || !actualSeeds.SequenceEqual(expectedSeeds)
                || samples.GroupBy(value => value.DeterministicSeed)
                    .Any(seedGroup => seedGroup.Count() != 1))
            {
                throw new InvalidOperationException(
                    "Frozen clearance profile does not contain exactly one observation per required seed: "
                    + group.Key);
            }
        }

        IReadOnlyList<ProductionOutputClearanceProfileRecord> records = Build(
            observations,
            throughput,
            minimumDistinctSeeds: expectedSeeds.Length);
        if (records.Count != expectedProfileCount)
        {
            throw new InvalidOperationException(
                "Frozen clearance profile aggregation emitted an incomplete row set.");
        }
        return records;
    }

    public static IReadOnlyList<ProductionOutputClearanceProfileRecord> Build(
        IReadOnlyList<ProductionOutputClearanceProfileObservation> observations,
        IReadOnlyList<ProductionOutputThroughputEnvelopeSnapshot> throughput,
        int minimumDistinctSeeds = 32)
    {
        if (observations == null)
            throw new ArgumentNullException(nameof(observations));
        if (throughput == null)
            throw new ArgumentNullException(nameof(throughput));
        if (minimumDistinctSeeds <= 0)
            throw new ArgumentOutOfRangeException(nameof(minimumDistinctSeeds));
        if (observations.Count == 0)
        {
            throw new InvalidOperationException(
                "Clearance profile aggregation requires clean observations.");
        }

        ProductionOutputClearanceProfileObservation[] orderedObservations =
            observations
                .OrderBy(value => value.DefinitionId, StringComparer.Ordinal)
                .ThenBy(value => value.WorkstationTag, StringComparer.Ordinal)
                .ThenBy(value => value.DeterministicSeed)
                .ThenBy(value => value.BatchCommitId, StringComparer.Ordinal)
                .ToArray();
        for (int index = 1; index < orderedObservations.Length; index++)
        {
            ProductionOutputClearanceProfileObservation previous =
                orderedObservations[index - 1];
            ProductionOutputClearanceProfileObservation current =
                orderedObservations[index];
            if (SameObservationIdentity(previous, current))
            {
                throw new InvalidOperationException(
                    "Clearance profile aggregation contains a duplicate observation: "
                    + current.BatchCommitId);
            }
        }

        ProductionOutputThroughputEnvelopeSnapshot[] orderedThroughput =
            throughput
                .OrderBy(value => value.DefinitionId, StringComparer.Ordinal)
                .ThenBy(value => value.WorkstationTag, StringComparer.Ordinal)
                .ToArray();
        Dictionary<ProfileKey, ProductionOutputThroughputEnvelopeSnapshot>
            throughputByKey = new();
        foreach (ProductionOutputThroughputEnvelopeSnapshot envelope
                 in orderedThroughput)
        {
            ProfileKey key = new(envelope.DefinitionId, envelope.WorkstationTag);
            if (!throughputByKey.TryAdd(key, envelope))
            {
                throw new InvalidOperationException(
                    "Clearance profile aggregation contains a duplicate throughput envelope: "
                    + key);
            }
        }

        List<ProductionOutputClearanceProfileRecord> records = new();
        foreach (IGrouping<ProfileKey, ProductionOutputClearanceProfileObservation>
                 group in orderedObservations.GroupBy(
                     value => new ProfileKey(
                         value.DefinitionId,
                         value.WorkstationTag)))
        {
            if (!throughputByKey.TryGetValue(group.Key, out var envelope))
            {
                throw new InvalidOperationException(
                    "Clearance profile aggregation is missing a throughput envelope: "
                    + group.Key);
            }

            ProductionOutputClearanceProfileObservation[] samples = group
                .OrderBy(value => value.ClearanceMicroHours)
                .ThenBy(value => value.DeterministicSeed)
                .ThenBy(value => value.BatchCommitId, StringComparer.Ordinal)
                .ToArray();
            int distinctSeedCount = samples
                .Select(value => value.DeterministicSeed)
                .Distinct()
                .Count();
            if (distinctSeedCount < minimumDistinctSeeds)
            {
                throw new InvalidOperationException(
                    "Clearance profile aggregation has insufficient deterministic seeds: "
                    + group.Key);
            }

            int p95Index = checked(
                (int)DivideCeiling(checked(samples.LongLength * 95L), 100L)
                - 1);
            long p95MicroHours = samples[p95Index].ClearanceMicroHours;
            long p95MilliHours = DivideCeiling(
                p95MicroHours,
                MicroHoursPerMilliHour);

            CanonicalSemanticDigestBuilder measurementDigest = new();
            measurementDigest.Append(MeasurementSchema);
            measurementDigest.Append(group.Key.DefinitionId);
            measurementDigest.Append(group.Key.WorkstationTag);
            measurementDigest.Append(samples.Length);
            measurementDigest.Append(distinctSeedCount);
            foreach (ProductionOutputClearanceProfileObservation sample
                     in samples
                         .OrderBy(value => value.DeterministicSeed)
                         .ThenBy(value => value.BatchCommitId,
                             StringComparer.Ordinal))
            {
                measurementDigest.Append(sample.DeterministicSeed);
                measurementDigest.Append(sample.BatchCommitId);
                measurementDigest.Append(sample.ClearanceMicroHours);
                measurementDigest.Append(sample.ActualBatchMassGrams);
                measurementDigest.Append(sample.RunSourceDigest);
            }
            measurementDigest.Append(p95MicroHours);
            measurementDigest.Append(p95MilliHours);

            records.Add(new ProductionOutputClearanceProfileRecord(
                group.Key.DefinitionId,
                group.Key.WorkstationTag,
                p95MilliHours,
                envelope.PeakOutputMassGramsPerHour,
                samples.Length,
                distinctSeedCount,
                measurementDigest.ComputeSha256(),
                envelope.SourceDigest));
            throughputByKey.Remove(group.Key);
        }

        if (throughputByKey.Count != 0)
        {
            throw new InvalidOperationException(
                "Clearance profile aggregation contains an orphan throughput envelope: "
                + throughputByKey.Keys
                    .OrderBy(value => value.DefinitionId, StringComparer.Ordinal)
                    .ThenBy(value => value.WorkstationTag, StringComparer.Ordinal)
                    .First());
        }

        return Array.AsReadOnly(records
            .OrderBy(value => value.DefinitionId, StringComparer.Ordinal)
            .ThenBy(value => value.WorkstationTag, StringComparer.Ordinal)
            .ToArray());
    }

    private static bool SameObservationIdentity(
        ProductionOutputClearanceProfileObservation left,
        ProductionOutputClearanceProfileObservation right) =>
        string.Equals(left.DefinitionId, right.DefinitionId,
            StringComparison.Ordinal)
        && string.Equals(left.WorkstationTag, right.WorkstationTag,
            StringComparison.Ordinal)
        && left.DeterministicSeed == right.DeterministicSeed
        && string.Equals(left.BatchCommitId, right.BatchCommitId,
            StringComparison.Ordinal);

    private static long DivideCeiling(long numerator, long denominator)
    {
        if (numerator <= 0L)
            throw new ArgumentOutOfRangeException(nameof(numerator));
        if (denominator <= 0L)
            throw new ArgumentOutOfRangeException(nameof(denominator));
        return checked(1L + ((numerator - 1L) / denominator));
    }

    private readonly struct ProfileKey : IEquatable<ProfileKey>
    {
        internal ProfileKey(string definitionId, string workstationTag)
        {
            DefinitionId = definitionId;
            WorkstationTag = workstationTag;
        }

        internal string DefinitionId { get; }
        internal string WorkstationTag { get; }

        public bool Equals(ProfileKey other) =>
            string.Equals(DefinitionId, other.DefinitionId,
                StringComparison.Ordinal)
            && string.Equals(WorkstationTag, other.WorkstationTag,
                StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is ProfileKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(
            DefinitionId == null
                ? 0
                : StringComparer.Ordinal.GetHashCode(DefinitionId),
            WorkstationTag == null
                ? 0
                : StringComparer.Ordinal.GetHashCode(WorkstationTag));

        public override string ToString() =>
            (DefinitionId ?? string.Empty) + "/" +
            (WorkstationTag ?? string.Empty);
    }
}

/// <summary>
/// Immutable production lookup. Missing or duplicate content fails loudly;
/// there is no default profile and no lookup by mutable facility instance.
/// </summary>
public sealed class ProductionOutputClearanceProfileCatalog :
    IProductionOutputClearanceProfileSource
{
    public const string Schema = "production-output-clearance-profile-catalog@1";

    private readonly Dictionary<ProfileKey, ProductionOutputClearanceProfileRecord>
        records;

    public ProductionOutputClearanceProfileCatalog(
        IReadOnlyList<ProductionOutputClearanceProfileRecord> source)
    {
        if (source == null)
            throw new ArgumentNullException(nameof(source));
        records = new Dictionary<ProfileKey, ProductionOutputClearanceProfileRecord>();
        ProductionOutputClearanceProfileRecord[] ordered = source
                     .OrderBy(value => value?.DefinitionId ?? string.Empty,
                         StringComparer.Ordinal)
                     .ThenBy(value => value?.WorkstationTag ?? string.Empty,
                         StringComparer.Ordinal)
                     .ToArray();
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(ordered.Length);
        foreach (ProductionOutputClearanceProfileRecord record in ordered)
        {
            if (record == null)
            {
                throw new InvalidOperationException(
                    "Clearance profile catalog contains a null row.");
            }
            ProfileKey key = new(record.DefinitionId, record.WorkstationTag);
            if (!records.TryAdd(key, record))
            {
                throw new InvalidOperationException(
                    "Clearance profile catalog contains a duplicate key: " + key);
            }
            digest.Append(record.DefinitionId);
            digest.Append(record.WorkstationTag);
            digest.Append(record.SourceDigest);
        }
        if (records.Count == 0)
        {
            throw new InvalidOperationException(
                "Clearance profile catalog must not be empty.");
        }
        SourceDigest = digest.ComputeSha256();
        Records = Array.AsReadOnly(ordered);
    }

    public string SourceDigest { get; }
    public string AuthorityDigest => SourceDigest;
    public IReadOnlyList<ProductionOutputClearanceProfileRecord> Records { get; }

    public ProductionOutputClearanceProfileSnapshot Capture(
        ProductionFacilityCapacitySubject facility)
    {
        ProfileKey key = new(facility.DefinitionId, facility.WorkstationTag);
        if (!records.TryGetValue(key, out var record))
        {
            throw new InvalidOperationException(
                "Production output-clearance profile is missing: " + key);
        }
        return record.Snapshot;
    }

    private readonly struct ProfileKey : IEquatable<ProfileKey>
    {
        internal ProfileKey(string definitionId, string workstationTag)
        {
            DefinitionId = definitionId;
            WorkstationTag = workstationTag;
        }

        internal string DefinitionId { get; }
        internal string WorkstationTag { get; }

        public bool Equals(ProfileKey other) =>
            string.Equals(DefinitionId, other.DefinitionId,
                StringComparison.Ordinal)
            && string.Equals(WorkstationTag, other.WorkstationTag,
                StringComparison.Ordinal);

        public override bool Equals(object obj) =>
            obj is ProfileKey other && Equals(other);

        public override int GetHashCode() => HashCode.Combine(
            DefinitionId == null
                ? 0
                : StringComparer.Ordinal.GetHashCode(DefinitionId),
            WorkstationTag == null
                ? 0
                : StringComparer.Ordinal.GetHashCode(WorkstationTag));

        public override string ToString() =>
            (DefinitionId ?? string.Empty) + "/" +
            (WorkstationTag ?? string.Empty);
    }
}
