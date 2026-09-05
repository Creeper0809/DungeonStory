#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

public sealed class ProductionOutputClearanceNaturalRunIdentity
{
    public const string Schema =
        "production-output-clearance-natural-shard-run@3";
    public const string BootstrapProfileMode = "bootstrap";
    public const string StrictProfileMode = "strict";
    public const string OfficialGameplaySceneSha256 =
        "6c35a17693d3cedca2c85b89b22a8bff9b5bae6de88c01b255481c058d2aee40";

    internal ProductionOutputClearanceNaturalRunIdentity(
        string currentSourceDigest,
        string gameplaySceneDigest,
        string currentPortfolioDigest,
        string descriptorCoverageDigest,
        string measurementPortfolioDigest,
        int currentShardCount,
        string currentShardKeySetDigest,
        string shardId,
        string shardDigest,
        string handlerRegistryFingerprint,
        string executorRegistryFingerprint,
        string clearanceProfileMode,
        string clearanceProfileAuthorityDigest)
    {
        CurrentSourceDigest = Digest(currentSourceDigest, nameof(currentSourceDigest));
        GameplaySceneDigest = Digest(gameplaySceneDigest, nameof(gameplaySceneDigest));
        CurrentPortfolioDigest = Digest(
            currentPortfolioDigest,
            nameof(currentPortfolioDigest));
        DescriptorCoverageDigest = Digest(
            descriptorCoverageDigest,
            nameof(descriptorCoverageDigest));
        MeasurementPortfolioDigest = Digest(
            measurementPortfolioDigest,
            nameof(measurementPortfolioDigest));
        CurrentShardCount = currentShardCount > 0
            ? currentShardCount
            : throw new ArgumentOutOfRangeException(nameof(currentShardCount));
        CurrentShardKeySetDigest = Digest(
            currentShardKeySetDigest,
            nameof(currentShardKeySetDigest));
        ShardId = Token(shardId, nameof(shardId));
        ShardDigest = Digest(shardDigest, nameof(shardDigest));
        HandlerRegistryFingerprint = Digest(
            handlerRegistryFingerprint,
            nameof(handlerRegistryFingerprint));
        ExecutorRegistryFingerprint = Digest(
            executorRegistryFingerprint,
            nameof(executorRegistryFingerprint));
        ClearanceProfileMode = RequireProfileMode(clearanceProfileMode);
        ClearanceProfileAuthorityDigest = Digest(
            clearanceProfileAuthorityDigest,
            nameof(clearanceProfileAuthorityDigest));
        if (!string.Equals(
                GameplaySceneDigest,
                OfficialGameplaySceneSha256,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "NATURAL_PORTFOLIO_OFFICIAL_SCENE_DIGEST_MISMATCH");
        }

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(CurrentSourceDigest);
        digest.Append(GameplaySceneDigest);
        digest.Append(CurrentPortfolioDigest);
        digest.Append(DescriptorCoverageDigest);
        digest.Append(MeasurementPortfolioDigest);
        digest.Append(CurrentShardCount);
        digest.Append(CurrentShardKeySetDigest);
        digest.Append(ShardId);
        digest.Append(ShardDigest);
        digest.Append(HandlerRegistryFingerprint);
        digest.Append(ExecutorRegistryFingerprint);
        digest.Append(ClearanceProfileMode);
        digest.Append(ClearanceProfileAuthorityDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public string CurrentSourceDigest { get; }
    public string GameplaySceneDigest { get; }
    public string CurrentPortfolioDigest { get; }
    public string DescriptorCoverageDigest { get; }
    public string MeasurementPortfolioDigest { get; }
    public int CurrentShardCount { get; }
    public string CurrentShardKeySetDigest { get; }
    public string ShardId { get; }
    public string ShardDigest { get; }
    public string HandlerRegistryFingerprint { get; }
    public string ExecutorRegistryFingerprint { get; }
    public string ClearanceProfileMode { get; }
    public string ClearanceProfileAuthorityDigest { get; }
    public string SourceDigest { get; }

    public static ProductionOutputClearanceNaturalRunIdentity CaptureCurrent(
        ProductionOutputClearanceCurrentPortfolioSnapshot portfolio,
        ProductionOutputClearanceNaturalPortfolioShardSnapshot shard,
        ProductionOutputClearanceNaturalMeasurementHandlerRegistry handlers,
        ProductionOutputClearanceNaturalMeasurementExecutorRegistry executors,
        IProductionOutputClearanceProfileSource clearanceProfiles)
    {
        if (portfolio == null)
            throw new ArgumentNullException(nameof(portfolio));
        if (shard == null || !portfolio.Shards.Contains(shard))
            throw new InvalidOperationException(
                "Natural shard is not owned by the current portfolio.");
        if (handlers == null)
            throw new ArgumentNullException(nameof(handlers));
        if (executors == null)
            throw new ArgumentNullException(nameof(executors));
        if (clearanceProfiles == null)
            throw new ArgumentNullException(nameof(clearanceProfiles));
        handlers.RequireExactCoverage(portfolio.PayloadCounts.Keys);
        executors.RequireExactCoverage(portfolio);
        return new ProductionOutputClearanceNaturalRunIdentity(
            V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest(),
            V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest(),
            portfolio.SourceDigest,
            portfolio.ExecutableCoverage.SourceDigest,
            portfolio.Portfolio.SourceDigest,
            portfolio.Shards.Count,
            ProductionOutputClearanceNaturalPortfolioCoordinator
                .ComputeCurrentShardKeySetDigest(portfolio.Shards),
            shard.ShardId,
            shard.SourceDigest,
            handlers.RegistryFingerprint,
            executors.RegistryFingerprint,
            clearanceProfiles is ProductionOutputClearanceNaturalBootstrapProfileSource
                ? BootstrapProfileMode
                : StrictProfileMode,
            clearanceProfiles.AuthorityDigest);
    }

    public bool SameAs(ProductionOutputClearanceNaturalRunIdentity other) =>
        other != null
        && string.Equals(SourceDigest, other.SourceDigest, StringComparison.Ordinal);

    public void RequireCurrentFiles()
    {
        if (!string.Equals(CurrentSourceDigest,
                V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest(),
                StringComparison.Ordinal)
            || !string.Equals(GameplaySceneDigest,
                V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest(),
                StringComparison.Ordinal))
        {
            throw new ProductionOutputClearanceNaturalShardResumeException(
                "source-or-scene-changed-during-run");
        }
    }

    private static string Digest(string value, string name)
    {
        if (!ProductionOutputClearanceProfileObservation.IsLowercaseSha256(value))
            throw new ArgumentException("A lowercase SHA-256 digest is required.", name);
        return value;
    }

    internal static string Token(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal)
            || value.IndexOfAny(new[] { '|', '\r', '\n', '=' }) >= 0)
        {
            throw new ArgumentException(
                "A canonical shard-store token is required.", name);
        }
        return value;
    }

    private static string RequireProfileMode(string value)
    {
        if (!string.Equals(value, BootstrapProfileMode, StringComparison.Ordinal)
            && !string.Equals(value, StrictProfileMode, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical clearance-profile mode is required.",
                nameof(value));
        }
        return value;
    }
}

public sealed class ProductionOutputClearanceNaturalShardEvidence
{
    public ProductionOutputClearanceNaturalShardEvidence(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionOutputClearanceExecutionReceiptSnapshot receipt,
        ProductionOutputClearanceNaturalObservationRecord observation)
    {
        Request = request ?? throw new ArgumentNullException(nameof(request));
        Receipt = receipt ?? throw new ArgumentNullException(nameof(receipt));
        Observation = observation
            ?? throw new ArgumentNullException(nameof(observation));
        if (!ReferenceEquals(Request.Descriptor, Receipt.Descriptor)
            || !ReferenceEquals(Request.Fixture, Observation.Fixture)
            || !string.Equals(Request.ActionId, Receipt.ActionId,
                StringComparison.Ordinal)
            || !string.Equals(Receipt.RuntimeFacilityId,
                Observation.RuntimeFacilityId, StringComparison.Ordinal)
            || !string.Equals(Receipt.BatchCommitId,
                Observation.BatchCommitId, StringComparison.Ordinal)
            || !string.Equals(Receipt.ResolvedOutputVectorDigest,
                Observation.ResolvedOutputVectorDigest, StringComparison.Ordinal)
            || Receipt.ActualBatchMassGrams != Observation.ActualBatchMassGrams
            || Observation.TelemetryCompletedCount
                != Receipt.RouteBatchCommitIds.Count
            || !Observation.IsExact)
        {
            throw new InvalidOperationException(
                "Natural shard evidence does not form one exact execution join.");
        }

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-natural-shard-evidence@1");
        digest.Append(Request.SourceDigest);
        digest.Append(Receipt.SourceDigest);
        digest.Append(Observation.RunSourceDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public ProductionOutputClearanceNaturalExecutionRequest Request { get; }
    public ProductionOutputClearanceExecutionReceiptSnapshot Receipt { get; }
    public ProductionOutputClearanceNaturalObservationRecord Observation { get; }
    public string SourceDigest { get; }
}

public sealed class ProductionOutputClearanceNaturalShardProgressSnapshot
{
    internal ProductionOutputClearanceNaturalShardProgressSnapshot(
        ProductionOutputClearanceNaturalRunIdentity identity,
        ProductionOutputClearanceNaturalPortfolioShardSnapshot shard,
        IReadOnlyList<ProductionOutputClearanceNaturalShardEvidence> evidence)
    {
        Identity = identity ?? throw new ArgumentNullException(nameof(identity));
        Shard = shard ?? throw new ArgumentNullException(nameof(shard));
        if (!string.Equals(identity.ShardId, shard.ShardId, StringComparison.Ordinal)
            || !string.Equals(identity.ShardDigest, shard.SourceDigest,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Natural shard progress identity does not own the shard.");
        }
        ProductionOutputClearanceNaturalShardEvidence[] ordered = (evidence
                ?? throw new ArgumentNullException(nameof(evidence)))
            .OrderBy(value => value?.Request.Fixture.SeedIndex)
            .ThenBy(value => value?.Request.Fixture.DeterministicSeed)
            .ToArray();
        string[] allowed = shard.Fixtures.Select(value => value.SourceDigest)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        string[] actual = ordered.Select(value => value?.Request.Fixture.SourceDigest)
            .OrderBy(value => value, StringComparer.Ordinal).ToArray();
        if (ordered.Any(value => value == null
                || !ReferenceEquals(value.Request.Shard, shard))
            || ordered.Select(value => value.Request.Fixture.SourceDigest)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length
            || ordered.Select(value => value.Request.ActionId + "\u001f"
                    + value.Receipt.BatchCommitId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length
            || actual.Except(allowed, StringComparer.Ordinal).Any())
        {
            throw new InvalidOperationException(
                "Natural shard progress is duplicated or outside the frozen shard.");
        }
        Evidence = Array.AsReadOnly(ordered);
        IsComplete = ordered.Length == shard.Fixtures.Count;
        NextFixture = IsComplete
            ? null
            : shard.Fixtures.First(value => !ordered.Any(existing =>
                ReferenceEquals(existing.Request.Fixture, value)));

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-natural-shard-progress@1");
        digest.Append(identity.SourceDigest);
        digest.Append(shard.SourceDigest);
        digest.Append(ordered.Length);
        foreach (ProductionOutputClearanceNaturalShardEvidence value in ordered)
            digest.Append(value.SourceDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public ProductionOutputClearanceNaturalRunIdentity Identity { get; }
    public ProductionOutputClearanceNaturalPortfolioShardSnapshot Shard { get; }
    public IReadOnlyList<ProductionOutputClearanceNaturalShardEvidence> Evidence
        { get; }
    public bool IsComplete { get; }
    public ProductionOutputClearanceMeasurementFixture NextFixture { get; }
    public string SourceDigest { get; }

    public ProductionOutputClearanceNaturalShardProgressSnapshot Append(
        ProductionOutputClearanceNaturalShardEvidence value)
    {
        if (value == null)
            throw new ArgumentNullException(nameof(value));
        return new ProductionOutputClearanceNaturalShardProgressSnapshot(
            Identity,
            Shard,
            Evidence.Concat(new[] { value }).ToArray());
    }
}

public sealed class ProductionOutputClearanceNaturalShardResumeException :
    InvalidOperationException
{
    public ProductionOutputClearanceNaturalShardResumeException(string detail)
        : base("NATURAL_PORTFOLIO_RESUME_STALE: " + detail)
    {
    }
}

public static class ProductionOutputClearanceNaturalShardEvidenceStore
{
    private const string StoreSchema =
        "production-output-clearance-natural-shard-store@4";
    private static readonly UTF8Encoding Utf8NoBom = new(false, true);

    public static string RelativePath(
        ProductionOutputClearanceNaturalRunIdentity identity,
        ProductionOutputClearanceNaturalPortfolioShardSnapshot shard)
    {
        if (identity == null)
            throw new ArgumentNullException(nameof(identity));
        if (shard == null)
            throw new ArgumentNullException(nameof(shard));
        return "Temp/v27-output-clearance-natural-shards/"
            + identity.CurrentSourceDigest + "/"
            + identity.SourceDigest.Substring(0, 32) + "/"
            + shard.SourceDigest.Substring(0, 16) + ".state";
    }

    public static ProductionOutputClearanceNaturalShardProgressSnapshot
        LoadOrCreate(
            ProductionOutputClearanceNaturalRunIdentity identity,
            ProductionOutputClearanceNaturalPortfolioShardSnapshot shard)
    {
        if (identity == null)
            throw new ArgumentNullException(nameof(identity));
        if (shard == null)
            throw new ArgumentNullException(nameof(shard));
        string path = Absolute(RelativePath(identity, shard));
        if (!File.Exists(path))
        {
            return new ProductionOutputClearanceNaturalShardProgressSnapshot(
                identity,
                shard,
                Array.Empty<ProductionOutputClearanceNaturalShardEvidence>());
        }
        return Parse(identity, shard, File.ReadAllLines(path, Utf8NoBom));
    }

    public static bool Write(
        ProductionOutputClearanceNaturalShardProgressSnapshot snapshot)
    {
        if (snapshot == null)
            throw new ArgumentNullException(nameof(snapshot));
        return V27BalanceArtifactWriter.WriteIfDifferent(
            RelativePath(snapshot.Identity, snapshot.Shard),
            stream => Write(stream, snapshot));
    }

    private static void Write(
        Stream stream,
        ProductionOutputClearanceNaturalShardProgressSnapshot snapshot)
    {
        using StreamWriter writer = new(stream, Utf8NoBom, 16384, leaveOpen: true)
        {
            NewLine = "\n"
        };
        WriteHeader(writer, "schema", StoreSchema);
        WriteHeader(writer, "identity", snapshot.Identity.SourceDigest);
        WriteHeader(writer, "currentSource", snapshot.Identity.CurrentSourceDigest);
        WriteHeader(writer, "scene", snapshot.Identity.GameplaySceneDigest);
        WriteHeader(writer, "portfolio", snapshot.Identity.CurrentPortfolioDigest);
        WriteHeader(writer, "descriptors", snapshot.Identity.DescriptorCoverageDigest);
        WriteHeader(writer, "measurements", snapshot.Identity.MeasurementPortfolioDigest);
        WriteHeader(writer, "shardCount", snapshot.Identity.CurrentShardCount.ToString(
            CultureInfo.InvariantCulture));
        WriteHeader(writer, "shardKeySet", snapshot.Identity.CurrentShardKeySetDigest);
        WriteHeader(writer, "shardId", snapshot.Identity.ShardId);
        WriteHeader(writer, "shard", snapshot.Identity.ShardDigest);
        WriteHeader(writer, "handlers", snapshot.Identity.HandlerRegistryFingerprint);
        WriteHeader(writer, "executors", snapshot.Identity.ExecutorRegistryFingerprint);
        WriteHeader(writer, "clearanceProfileMode",
            snapshot.Identity.ClearanceProfileMode);
        WriteHeader(writer, "clearanceProfileAuthority",
            snapshot.Identity.ClearanceProfileAuthorityDigest);
        foreach (ProductionOutputClearanceNaturalShardEvidence value in
                 snapshot.Evidence)
        {
            ProductionOutputClearanceNaturalObservationRecord o = value.Observation;
            ProductionOutputClearanceExecutionReceiptSnapshot r = value.Receipt;
            WriteFields(writer,
                "R", o.Fixture.ObservationId, o.Fixture.SourceDigest,
                o.Fixture.SeedIndex, o.Fixture.DeterministicSeed,
                value.Request.ActionId, r.RuntimeFacilityId, r.OperationId,
                r.BatchCommitId, r.ResolvedOutputVectorDigest,
                r.ActualBatchMassGrams, o.TopologySourceDigest,
                o.TopologyStable, o.FacilityAttributionExact, o.OwnerRosterKey,
                o.ActionEpochDelta, o.ActionStartDelta, o.HaulStartDelta,
                o.ClearanceMicroHours, o.TelemetryCompletedCount,
                o.TelemetryActiveCount, o.OrphanPickupCount,
                o.ConflictingPublicationCount, o.OverPickupCount,
                o.CapacityExceededCount, o.RestoreInterruptionCount,
                o.TelemetryClean, o.SchedulerProvenanceExact, o.DeliveryExact,
                o.RandomStateDigest, o.RandomDrawDelta,
                r.RuntimeReceiptDigest, r.HandlerId, r.HandlerVersion,
                r.OutcomeFingerprint, r.PlannedOutputFingerprint,
                r.SourceDigest, value.SourceDigest);
            foreach (ProductionOutputClearanceExecutionOutputSliceSnapshot slice
                     in r.Outputs)
            {
                WriteFields(writer,
                    "S", o.Fixture.ObservationId, slice.OutputLineId,
                    slice.ItemId, slice.ItemInstanceId, slice.StackId,
                    slice.Quantity, slice.MassGrams,
                    slice.CapabilityFingerprint, slice.SourceDigest);
            }
            string[] routeBatchCommitIds = r.RouteBatchCommitIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            for (int index = 0; index < routeBatchCommitIds.Length; index++)
            {
                WriteFields(writer,
                    "B", o.Fixture.ObservationId, index,
                    routeBatchCommitIds[index]);
            }
        }
        writer.Flush();
    }

    private static ProductionOutputClearanceNaturalShardProgressSnapshot Parse(
        ProductionOutputClearanceNaturalRunIdentity expected,
        ProductionOutputClearanceNaturalPortfolioShardSnapshot shard,
        IReadOnlyList<string> lines)
    {
        Dictionary<string, string> header = new(StringComparer.Ordinal);
        List<string[]> records = new();
        List<string[]> slices = new();
        List<string[]> routeBatches = new();
        foreach (string line in lines)
        {
            if (line.StartsWith("R|", StringComparison.Ordinal))
                records.Add(line.Split('|'));
            else if (line.StartsWith("S|", StringComparison.Ordinal))
                slices.Add(line.Split('|'));
            else if (line.StartsWith("B|", StringComparison.Ordinal))
                routeBatches.Add(line.Split('|'));
            else
            {
                int separator = line.IndexOf('=');
                if (separator <= 0
                    || !header.TryAdd(line.Substring(0, separator),
                        line.Substring(separator + 1)))
                    throw new InvalidDataException("Natural shard header is invalid.");
            }
        }
        RequireHeader(header, "schema", StoreSchema);
        RequireHeader(header, "identity", expected.SourceDigest);
        RequireHeader(header, "currentSource", expected.CurrentSourceDigest);
        RequireHeader(header, "scene", expected.GameplaySceneDigest);
        RequireHeader(header, "portfolio", expected.CurrentPortfolioDigest);
        RequireHeader(header, "descriptors", expected.DescriptorCoverageDigest);
        RequireHeader(header, "measurements", expected.MeasurementPortfolioDigest);
        RequireHeader(header, "shardCount", expected.CurrentShardCount.ToString(
            CultureInfo.InvariantCulture));
        RequireHeader(header, "shardKeySet", expected.CurrentShardKeySetDigest);
        RequireHeader(header, "shardId", expected.ShardId);
        RequireHeader(header, "shard", expected.ShardDigest);
        RequireHeader(header, "handlers", expected.HandlerRegistryFingerprint);
        RequireHeader(header, "executors", expected.ExecutorRegistryFingerprint);
        RequireHeader(header, "clearanceProfileMode",
            expected.ClearanceProfileMode);
        RequireHeader(header, "clearanceProfileAuthority",
            expected.ClearanceProfileAuthorityDigest);
        if (header.Count != 15)
            throw new ProductionOutputClearanceNaturalShardResumeException(
                "unexpected-header-count");

        Dictionary<string, ProductionOutputClearanceMeasurementFixture> fixtures =
            shard.Fixtures.ToDictionary(value => value.ObservationId,
                StringComparer.Ordinal);
        Dictionary<string, List<string[]>> slicesByObservation = slices
            .GroupBy(value => Field(value, 1), StringComparer.Ordinal)
            .ToDictionary(value => value.Key, value => value.ToList(),
                StringComparer.Ordinal);
        Dictionary<string, List<string[]>> routeBatchesByObservation =
            routeBatches
                .GroupBy(value => Field(value, 1), StringComparer.Ordinal)
                .ToDictionary(value => value.Key, value => value.ToList(),
                    StringComparer.Ordinal);
        List<ProductionOutputClearanceNaturalShardEvidence> evidence = new();
        foreach (string[] fields in records)
        {
            if (fields.Length != 38)
                throw new InvalidDataException("Natural shard record width is invalid.");
            string observationId = Field(fields, 1);
            if (!fixtures.TryGetValue(observationId,
                    out ProductionOutputClearanceMeasurementFixture fixture)
                || !string.Equals(Field(fields, 2), fixture.SourceDigest,
                    StringComparison.Ordinal)
                || Integer(fields, 3) != fixture.SeedIndex
                || Integer(fields, 4) != fixture.DeterministicSeed)
            {
                throw new ProductionOutputClearanceNaturalShardResumeException(
                    "fixture-identity-mismatch");
            }
            ProductionOutputClearanceNaturalExecutionRequest request =
                new(shard, fixture);
            if (!string.Equals(Field(fields, 5), request.ActionId,
                    StringComparison.Ordinal))
                throw new ProductionOutputClearanceNaturalShardResumeException(
                    "action-identity-mismatch");
            if (!slicesByObservation.TryGetValue(observationId,
                    out List<string[]> outputRows))
                throw new InvalidDataException("Natural shard output slices are missing.");
            ProductionOutputClearanceExecutionOutputSliceSnapshot[] outputs =
                outputRows.Select(ParseSlice).ToArray();
            if (!routeBatchesByObservation.TryGetValue(observationId,
                    out List<string[]> routeRows))
            {
                throw new InvalidDataException(
                    "Natural shard route batches are missing.");
            }
            string[] routeBatchCommitIds = routeRows
                .OrderBy(value => Integer(value, 2))
                .Select((value, index) =>
                {
                    if (value.Length != 4 || Integer(value, 2) != index)
                    {
                        throw new InvalidDataException(
                            "Natural shard route batch ordinals are invalid.");
                    }
                    return Field(value, 3);
                })
                .ToArray();
            if (routeBatchCommitIds.Length == 0
                || routeBatchCommitIds.Distinct(StringComparer.Ordinal).Count()
                    != routeBatchCommitIds.Length)
            {
                throw new InvalidDataException(
                    "Natural shard route batches are empty or duplicated.");
            }
            ProductionOutputClearanceExecutionReceiptSnapshot receipt = new(
                shard.Descriptor,
                request.ActionId,
                Field(fields, 6),
                Field(fields, 7),
                Field(fields, 8),
                Field(fields, 34),
                Field(fields, 35),
                Field(fields, 9),
                Long(fields, 10),
                outputs,
                Field(fields, 31),
                Field(fields, 32),
                Integer(fields, 33),
                routeBatchCommitIds);
            if (!string.Equals(receipt.SourceDigest, Field(fields, 36),
                    StringComparison.Ordinal))
                throw new ProductionOutputClearanceNaturalShardResumeException(
                    "receipt-digest-mismatch");
            ProductionOutputClearanceNaturalObservationRecord observation = new(
                fixture, Field(fields, 6), Field(fields, 9), Long(fields, 10),
                Field(fields, 8), Field(fields, 11), Boolean(fields, 12),
                Boolean(fields, 13), Field(fields, 14), Long(fields, 15),
                Long(fields, 16), Long(fields, 17), Long(fields, 18),
                Integer(fields, 19), Integer(fields, 20), Integer(fields, 21),
                Integer(fields, 22), Integer(fields, 23), Integer(fields, 24),
                Integer(fields, 25), Boolean(fields, 26), Boolean(fields, 27),
                Boolean(fields, 28), Field(fields, 29), Long(fields, 30));
            ProductionOutputClearanceNaturalShardEvidence value =
                new(request, receipt, observation);
            if (!string.Equals(value.SourceDigest, Field(fields, 37),
                    StringComparison.Ordinal))
                throw new ProductionOutputClearanceNaturalShardResumeException(
                    "evidence-digest-mismatch");
            evidence.Add(value);
        }
        if (slicesByObservation.Keys.Except(records.Select(value => Field(value, 1)),
                StringComparer.Ordinal).Any())
            throw new InvalidDataException("Natural shard contains orphan output slices.");
        if (routeBatchesByObservation.Keys.Except(
                records.Select(value => Field(value, 1)),
                StringComparer.Ordinal).Any())
        {
            throw new InvalidDataException(
                "Natural shard contains orphan route batches.");
        }
        return new ProductionOutputClearanceNaturalShardProgressSnapshot(
            expected, shard, evidence);
    }

    private static ProductionOutputClearanceExecutionOutputSliceSnapshot
        ParseSlice(string[] fields)
    {
        if (fields.Length != 10)
            throw new InvalidDataException("Natural shard slice width is invalid.");
        ProductionOutputClearanceExecutionOutputSliceSnapshot value = new(
            Field(fields, 2), Field(fields, 3), Field(fields, 4),
            Field(fields, 5), Integer(fields, 6), Long(fields, 7),
            Field(fields, 8));
        if (!string.Equals(value.SourceDigest, Field(fields, 9),
                StringComparison.Ordinal))
            throw new ProductionOutputClearanceNaturalShardResumeException(
                "output-slice-digest-mismatch");
        return value;
    }

    private static void RequireHeader(
        IReadOnlyDictionary<string, string> header,
        string key,
        string expected)
    {
        if (!header.TryGetValue(key, out string actual)
            || !string.Equals(actual, expected, StringComparison.Ordinal))
            throw new ProductionOutputClearanceNaturalShardResumeException(key);
    }

    private static void WriteHeader(StreamWriter writer, string key, string value)
    {
        writer.Write(ProductionOutputClearanceNaturalRunIdentity.Token(key, key));
        writer.Write('=');
        writer.Write(ProductionOutputClearanceNaturalRunIdentity.Token(value, key));
        writer.Write('\n');
    }

    private static void WriteFields(StreamWriter writer, params object[] values)
    {
        for (int index = 0; index < values.Length; index++)
        {
            if (index > 0)
                writer.Write('|');
            string value = Convert.ToString(values[index], CultureInfo.InvariantCulture)
                ?? string.Empty;
            if (values[index] is bool)
                value = value.ToLowerInvariant();
            writer.Write(ProductionOutputClearanceNaturalRunIdentity.Token(
                value.Length == 0 ? "~" : value,
                "field"));
        }
        writer.Write('\n');
    }

    private static string Field(IReadOnlyList<string> fields, int index) =>
        fields[index] == "~" ? string.Empty : fields[index];
    private static int Integer(IReadOnlyList<string> fields, int index) =>
        int.Parse(Field(fields, index), NumberStyles.Integer,
            CultureInfo.InvariantCulture);
    private static long Long(IReadOnlyList<string> fields, int index) =>
        long.Parse(Field(fields, index), NumberStyles.Integer,
            CultureInfo.InvariantCulture);
    private static bool Boolean(IReadOnlyList<string> fields, int index) =>
        bool.Parse(Field(fields, index));

    private static string Absolute(string projectRelativePath)
    {
        string root = Directory.GetParent(UnityEngine.Application.dataPath)?.FullName
            ?? throw new InvalidOperationException("Project root is unavailable.");
        return Path.Combine(root,
            projectRelativePath.Replace('/', Path.DirectorySeparatorChar));
    }
}
#endif
