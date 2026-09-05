#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using VContainer;

public sealed class ProductionOutputClearanceNaturalPortfolioRunResult
{
    internal ProductionOutputClearanceNaturalPortfolioRunResult(
        ProductionOutputClearanceCurrentPortfolioSnapshot current,
        ProductionOutputClearanceNaturalObservationPortfolioSnapshot accepted,
        ProductionOutputClearanceCanonicalNaturalArtifactPortfolioSnapshot canonical,
        int resumedObservationCount,
        int executedObservationCount,
        string reportSha256,
        string observationsSha256,
        string outputSlicesSha256)
    {
        Current = current ?? throw new ArgumentNullException(nameof(current));
        Accepted = accepted ?? throw new ArgumentNullException(nameof(accepted));
        Canonical = canonical ?? throw new ArgumentNullException(nameof(canonical));
        if (resumedObservationCount < 0
            || executedObservationCount < 0
            || resumedObservationCount + executedObservationCount
                != accepted.Records.Count)
            throw new ArgumentOutOfRangeException(nameof(executedObservationCount));
        ResumedObservationCount = resumedObservationCount;
        ExecutedObservationCount = executedObservationCount;
        ReportSha256 = Digest(reportSha256, nameof(reportSha256));
        ObservationsSha256 = Digest(
            observationsSha256,
            nameof(observationsSha256));
        OutputSlicesSha256 = Digest(
            outputSlicesSha256,
            nameof(outputSlicesSha256));
    }

    public ProductionOutputClearanceCurrentPortfolioSnapshot Current { get; }
    public ProductionOutputClearanceNaturalObservationPortfolioSnapshot Accepted
        { get; }
    public ProductionOutputClearanceCanonicalNaturalArtifactPortfolioSnapshot
        Canonical { get; }
    public int ResumedObservationCount { get; }
    public int ExecutedObservationCount { get; }
    public string ReportSha256 { get; }
    public string ObservationsSha256 { get; }
    public string OutputSlicesSha256 { get; }

    private static string Digest(string value, string name)
    {
        if (!ProductionOutputClearanceProfileObservation.IsLowercaseSha256(value))
            throw new ArgumentException("A lowercase SHA-256 digest is required.", name);
        return value;
    }
}

public static class ProductionOutputClearanceNaturalPortfolioCoordinator
{
    public const int MinimumV27BaselinePlanCount = 92;
    private const int MaximumFixtureYieldCount = 60_000;
    public const string ReportPath =
        "Artifacts/QA/v27-production-output-clearance-natural-portfolio.txt";
    public const string ObservationsCsvPath =
        "Artifacts/QA/v27-production-output-clearance-natural-observations.csv";
    public const string OutputSlicesCsvPath =
        "Artifacts/QA/v27-production-output-clearance-natural-output-slices.csv";
    public const string FocusShardEnvironmentVariable =
        "V27_NATURAL_FOCUS_SHARD";
    public const string PartitionIndexEnvironmentVariable =
        "V27_NATURAL_PARTITION_INDEX";
    public const string PartitionCountEnvironmentVariable =
        "V27_NATURAL_PARTITION_COUNT";
    public const string ExpectedSourceDigestEnvironmentVariable =
        "V27_EXPECTED_CURRENT_SOURCE_DIGEST";
    public const string ExpectedShardCountEnvironmentVariable =
        "V27_NATURAL_EXPECTED_SHARD_COUNT";
    public const string ExpectedShardKeySetDigestEnvironmentVariable =
        "V27_NATURAL_EXPECTED_SHARD_KEYSET_DIGEST";
    private static readonly UTF8Encoding Utf8NoBom = new(false, true);

    public static IEnumerator Execute(
        IObjectResolver container,
        ProductionOutputClearanceNaturalMeasurementHandlerRegistry handlers,
        ProductionOutputClearanceNaturalMeasurementExecutorRegistry executors,
        Func<ProductionOutputClearanceNaturalConsoleSnapshot> captureConsole,
        Action<ProductionOutputClearanceNaturalPortfolioRunResult> completed)
    {
        if (container == null)
            throw new ArgumentNullException(nameof(container));
        if (handlers == null)
            throw new ArgumentNullException(nameof(handlers));
        if (executors == null)
            throw new ArgumentNullException(nameof(executors));
        if (captureConsole == null)
            throw new ArgumentNullException(nameof(captureConsole));
        if (completed == null)
            throw new ArgumentNullException(nameof(completed));

        string executionSourceDigest = V27CurrentSourceEvidenceDigest
            .ComputeAllScriptsDigest();
        RequireExpectedSourceDigest(executionSourceDigest);
        string executionSceneDigest = V27CurrentSourceEvidenceDigest
            .ComputeGameplaySceneDigest();
        IProductionOutputClearanceProfileSource clearanceProfiles =
            container.Resolve<IProductionOutputClearanceProfileSource>();
        string clearanceProfileMode =
            clearanceProfiles is ProductionOutputClearanceNaturalBootstrapProfileSource
                ? ProductionOutputClearanceNaturalRunIdentity.BootstrapProfileMode
                : ProductionOutputClearanceNaturalRunIdentity.StrictProfileMode;
        string clearanceProfileAuthorityDigest =
            clearanceProfiles.AuthorityDigest;
        if (!ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                clearanceProfileAuthorityDigest))
        {
            throw new InvalidOperationException(
                "NATURAL_PORTFOLIO_PROFILE_AUTHORITY_NON_CANONICAL");
        }
        ProductionOutputClearanceCurrentPortfolioSnapshot current =
            ProductionOutputClearanceCurrentPortfolioCapture.Capture(container);
        if (string.Equals(
                clearanceProfileMode,
                ProductionOutputClearanceNaturalRunIdentity.StrictProfileMode,
                StringComparison.Ordinal))
        {
            _ = ProductionOutputClearanceStrictCurrentVerifier.VerifyAndWrite(
                current,
                clearanceProfiles);
        }
        int currentPlanCount = current.Shards.Count;
        int currentPayloadPlanCount = current.PayloadCounts.Values.Sum();
        if (currentPlanCount < MinimumV27BaselinePlanCount
            || currentPayloadPlanCount != currentPlanCount)
        {
            throw new InvalidOperationException(
                "NATURAL_PORTFOLIO_V27_PLAN_DENOMINATOR_MISMATCH: minimum="
                + MinimumV27BaselinePlanCount.ToString(
                    CultureInfo.InvariantCulture)
                + ";shards=" + currentPlanCount.ToString(
                    CultureInfo.InvariantCulture)
                + ";payloads=" + currentPayloadPlanCount.ToString(
                    CultureInfo.InvariantCulture));
        }
        handlers.RequireExactCoverage(current.PayloadCounts.Keys);
        executors.RequireExactCoverage(current);
        List<ProductionOutputClearanceNaturalShardEvidence> allEvidence =
            new(current.Portfolio.Fixtures.Count);
        List<ProductionOutputClearanceNaturalRunIdentity> runIdentities =
            new(current.Shards.Count);
        int resumed = 0;
        int executed = 0;

        IReadOnlyList<ProductionOutputClearanceNaturalPortfolioShardSnapshot>
            executionShards = SelectExecutionShards(
                current.Shards,
                out bool partitionOnly);
        foreach (ProductionOutputClearanceNaturalPortfolioShardSnapshot shard in
                 executionShards)
        {
            ProductionOutputClearanceNaturalRunIdentity identity =
                ProductionOutputClearanceNaturalRunIdentity.CaptureCurrent(
                    current,
                    shard,
                    handlers,
                    executors,
                    clearanceProfiles);
            if (!string.Equals(identity.CurrentSourceDigest,
                    executionSourceDigest, StringComparison.Ordinal)
                || !string.Equals(identity.GameplaySceneDigest,
                    executionSceneDigest, StringComparison.Ordinal))
            {
                throw new ProductionOutputClearanceNaturalShardResumeException(
                    "source-or-scene-drifted-before-shard");
            }
            runIdentities.Add(identity);
            ProductionOutputClearanceNaturalShardProgressSnapshot progress =
                ProductionOutputClearanceNaturalShardEvidenceStore.LoadOrCreate(
                    identity,
                    shard);
            resumed = checked(resumed + progress.Evidence.Count);
            while (!progress.IsComplete)
            {
                ProductionOutputClearanceNaturalExecutionRequest request =
                    new(shard, progress.NextFixture);
                ProductionOutputClearanceNaturalExecutionResult result = new();
                IEnumerator execution = executors.Execute(request, result);
                int yieldCount = 0;
                try
                {
                    while (execution.MoveNext())
                    {
                        yieldCount = checked(yieldCount + 1);
                        if (yieldCount > MaximumFixtureYieldCount)
                        {
                            throw new TimeoutException(
                                "NATURAL_PORTFOLIO_FIXTURE_TIMEOUT: action="
                                + request.ActionId + ";yields=" + yieldCount);
                        }
                        yield return execution.Current;
                    }
                }
                finally
                {
                    (execution as IDisposable)?.Dispose();
                }
                if (!result.Succeeded || result.Evidence == null)
                {
                    throw new InvalidOperationException(
                        "NATURAL_PORTFOLIO_EXECUTION_FAILED: action="
                        + request.ActionId + ";reason=" + result.FailureReason);
                }
                progress = progress.Append(result.Evidence);
                ProductionOutputClearanceNaturalShardEvidenceStore.Write(progress);
                executed = checked(executed + 1);
                yield return null;
            }
            identity.RequireCurrentFiles();
            if (ProductionOutputClearanceNaturalShardEvidenceStore.Write(progress))
            {
                throw new InvalidOperationException(
                    "NATURAL_PORTFOLIO_SHARD_SECOND_WRITE_CHANGED: "
                    + shard.ShardId);
            }
            allEvidence.AddRange(progress.Evidence);
            yield return null;
        }

        if (partitionOnly)
        {
            int expectedPartitionEvidence = executionShards.Sum(
                value => value.Fixtures.Count);
            bool partitionExact = runIdentities.Count == executionShards.Count
                && runIdentities.All(value => value != null
                    && string.Equals(value.CurrentSourceDigest,
                        executionSourceDigest, StringComparison.Ordinal)
                    && string.Equals(value.GameplaySceneDigest,
                        executionSceneDigest, StringComparison.Ordinal)
                    && string.Equals(value.CurrentPortfolioDigest,
                        current.SourceDigest, StringComparison.Ordinal)
                    && string.Equals(value.DescriptorCoverageDigest,
                        current.ExecutableCoverage.SourceDigest,
                        StringComparison.Ordinal)
                    && string.Equals(value.MeasurementPortfolioDigest,
                        current.Portfolio.SourceDigest,
                        StringComparison.Ordinal)
                    && value.CurrentShardCount == current.Shards.Count
                    && string.Equals(value.CurrentShardKeySetDigest,
                        ComputeCurrentShardKeySetDigest(current.Shards),
                        StringComparison.Ordinal)
                    && string.Equals(value.HandlerRegistryFingerprint,
                        handlers.RegistryFingerprint, StringComparison.Ordinal)
                    && string.Equals(value.ExecutorRegistryFingerprint,
                        executors.RegistryFingerprint, StringComparison.Ordinal)
                    && string.Equals(value.ClearanceProfileMode,
                        clearanceProfileMode, StringComparison.Ordinal)
                    && string.Equals(value.ClearanceProfileAuthorityDigest,
                        clearanceProfileAuthorityDigest, StringComparison.Ordinal))
                && allEvidence.Count == expectedPartitionEvidence
                && allEvidence.Select(value => value.Request.ActionId)
                    .Distinct(StringComparer.Ordinal).Count()
                    == expectedPartitionEvidence
                && allEvidence.Select(value => value.Request.ActionId + "\u001f"
                        + value.Receipt.BatchCommitId)
                    .Distinct(StringComparer.Ordinal).Count()
                    == expectedPartitionEvidence;
            if (!partitionExact)
            {
                throw new InvalidOperationException(
                    "NATURAL_PORTFOLIO_PARTITION_EVIDENCE_NOT_EXACT");
            }
            foreach (ProductionOutputClearanceNaturalRunIdentity identity in
                     runIdentities)
            {
                identity.RequireCurrentFiles();
            }

            // The external partition controller validates and atomically
            // publishes every completed shard before terminating this isolated
            // batch Worker. Staying in PlayMode avoids publishing a misleading
            // whole-portfolio PASS from a deliberate subset run.
            while (partitionOnly)
                yield return null;
        }

        ProductionOutputClearanceCurrentPortfolioSnapshot finalCurrent =
            ProductionOutputClearanceCurrentPortfolioCapture.Capture(container);
        string finalSourceDigest = V27CurrentSourceEvidenceDigest
            .ComputeAllScriptsDigest();
        string finalSceneDigest = V27CurrentSourceEvidenceDigest
            .ComputeGameplaySceneDigest();
        bool identitySetExact = runIdentities.Count == currentPlanCount
            && runIdentities.All(value => value != null
                && string.Equals(value.CurrentSourceDigest,
                    executionSourceDigest, StringComparison.Ordinal)
                && string.Equals(value.GameplaySceneDigest,
                    executionSceneDigest, StringComparison.Ordinal)
                && string.Equals(value.CurrentPortfolioDigest,
                    current.SourceDigest, StringComparison.Ordinal)
                && string.Equals(value.DescriptorCoverageDigest,
                    current.ExecutableCoverage.SourceDigest,
                    StringComparison.Ordinal)
                && string.Equals(value.MeasurementPortfolioDigest,
                    current.Portfolio.SourceDigest, StringComparison.Ordinal)
                && value.CurrentShardCount == current.Shards.Count
                && string.Equals(value.CurrentShardKeySetDigest,
                    ComputeCurrentShardKeySetDigest(current.Shards),
                    StringComparison.Ordinal)
                && string.Equals(value.HandlerRegistryFingerprint,
                    handlers.RegistryFingerprint, StringComparison.Ordinal)
                && string.Equals(value.ExecutorRegistryFingerprint,
                    executors.RegistryFingerprint, StringComparison.Ordinal)
                && string.Equals(value.ClearanceProfileMode,
                    clearanceProfileMode, StringComparison.Ordinal)
                && string.Equals(value.ClearanceProfileAuthorityDigest,
                    clearanceProfileAuthorityDigest, StringComparison.Ordinal))
            && string.Equals(finalSourceDigest,
                executionSourceDigest, StringComparison.Ordinal)
            && string.Equals(finalSceneDigest,
                executionSceneDigest, StringComparison.Ordinal)
            && string.Equals(finalCurrent.SourceDigest,
                current.SourceDigest, StringComparison.Ordinal)
            && string.Equals(finalCurrent.ExecutableCoverage.SourceDigest,
                current.ExecutableCoverage.SourceDigest, StringComparison.Ordinal)
            && string.Equals(finalCurrent.Portfolio.SourceDigest,
                current.Portfolio.SourceDigest, StringComparison.Ordinal)
            && finalCurrent.Shards.Count == currentPlanCount;
        if (!identitySetExact)
        {
            throw new ProductionOutputClearanceNaturalShardResumeException(
                "execution-identity-or-current-portfolio-drifted");
        }

        if (allEvidence.Count != current.Portfolio.Fixtures.Count
            || allEvidence.Select(value => value.Request.ActionId)
                .Distinct(StringComparer.Ordinal).Count() != allEvidence.Count
            || allEvidence.Select(value => value.Request.ActionId + "\u001f"
                    + value.Receipt.BatchCommitId)
                .Distinct(StringComparer.Ordinal).Count() != allEvidence.Count)
        {
            throw new InvalidOperationException(
                "NATURAL_PORTFOLIO_FINAL_EVIDENCE_NOT_BIJECTIVE");
        }
        ProductionOutputClearanceNaturalObservationPortfolioSnapshot accepted =
            ProductionOutputClearanceNaturalObservationPortfolioSnapshot.Build(
                current.Portfolio,
                allEvidence.Select(value => value.Observation).ToArray());
        ProductionOutputClearanceCanonicalNaturalArtifactPortfolioSnapshot canonical =
            ProductionOutputClearanceCanonicalNaturalArtifactProjection.Build(
                current.Portfolio,
                accepted,
                allEvidence);

        ProductionOutputClearanceNaturalConsoleSnapshot console =
            captureConsole();
        if (console == null || console.WarningCount != 0
            || console.ErrorCount != 0)
        {
            throw new InvalidOperationException(
                "NATURAL_PORTFOLIO_CONSOLE_NOT_CLEAN: warnings="
                + (console?.WarningCount ?? -1) + ";errors="
                + (console?.ErrorCount ?? -1));
        }
        WriteArtifacts(
            current,
            canonical,
            resumed,
            executed,
            console,
            handlers.RegistryFingerprint,
            executors.RegistryFingerprint,
            clearanceProfileMode,
            clearanceProfileAuthorityDigest,
            executionSourceDigest,
            executionSceneDigest,
            terminalPassed: false);
        NaturalArtifactStamp preliminary = CaptureArtifactStamp();
        ProductionOutputClearanceNaturalConsoleSnapshot secondConsole =
            captureConsole();
        if (secondConsole == null
            || secondConsole.WarningCount != console.WarningCount
            || secondConsole.ErrorCount != console.ErrorCount)
        {
            throw new InvalidOperationException(
                "NATURAL_PORTFOLIO_CONSOLE_CHANGED_DURING_SECOND_BUILD");
        }
        WriteArtifacts(
            current,
            canonical,
            resumed,
            executed,
            secondConsole,
            handlers.RegistryFingerprint,
            executors.RegistryFingerprint,
            clearanceProfileMode,
            clearanceProfileAuthorityDigest,
            executionSourceDigest,
            executionSceneDigest,
            terminalPassed: false);
        if (!preliminary.EqualsExact(CaptureArtifactStamp()))
        {
            throw new InvalidOperationException(
                "NATURAL_PORTFOLIO_PRELIMINARY_BUILD_NOT_BYTE_LENGTH_MTIME_IDENTICAL");
        }

        // Publish PASS only after the preliminary content has proved a true
        // no-write second build. Then prove the terminal artifact itself is
        // also byte/length/mtime stable so a failed no-op can never leave a
        // standalone PASS report behind.
        WriteArtifacts(
            current,
            canonical,
            resumed,
            executed,
            secondConsole,
            handlers.RegistryFingerprint,
            executors.RegistryFingerprint,
            clearanceProfileMode,
            clearanceProfileAuthorityDigest,
            executionSourceDigest,
            executionSceneDigest,
            terminalPassed: true);
        NaturalArtifactStamp terminal = CaptureArtifactStamp();
        WriteArtifacts(
            current,
            canonical,
            resumed,
            executed,
            secondConsole,
            handlers.RegistryFingerprint,
            executors.RegistryFingerprint,
            clearanceProfileMode,
            clearanceProfileAuthorityDigest,
            executionSourceDigest,
            executionSceneDigest,
            terminalPassed: true);
        if (!terminal.EqualsExact(CaptureArtifactStamp()))
        {
            File.Delete(ReportPath);
            throw new InvalidOperationException(
                "NATURAL_PORTFOLIO_TERMINAL_BUILD_NOT_BYTE_LENGTH_MTIME_IDENTICAL");
        }

        completed(new ProductionOutputClearanceNaturalPortfolioRunResult(
            current,
            accepted,
            canonical,
            resumed,
            executed,
            terminal.ReportSha256,
            terminal.ObservationsSha256,
            terminal.SlicesSha256));
    }

    private static IReadOnlyList<
        ProductionOutputClearanceNaturalPortfolioShardSnapshot>
        SelectExecutionShards(
            IReadOnlyList<ProductionOutputClearanceNaturalPortfolioShardSnapshot>
                shards,
            out bool partitionOnly)
    {
        if (shards == null)
            throw new ArgumentNullException(nameof(shards));
        string partitionIndexToken = Environment.GetEnvironmentVariable(
            PartitionIndexEnvironmentVariable);
        string partitionCountToken = Environment.GetEnvironmentVariable(
            PartitionCountEnvironmentVariable);
        string focus = Environment.GetEnvironmentVariable(
            FocusShardEnvironmentVariable);
        bool hasPartitionIndex = !string.IsNullOrEmpty(partitionIndexToken);
        bool hasPartitionCount = !string.IsNullOrEmpty(partitionCountToken);
        partitionOnly = hasPartitionIndex || hasPartitionCount;
        if (partitionOnly)
        {
            if (!hasPartitionIndex || !hasPartitionCount)
            {
                throw new InvalidOperationException(
                    "NATURAL_PORTFOLIO_PARTITION_COORDINATES_INCOMPLETE");
            }
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(
                    ExpectedSourceDigestEnvironmentVariable)))
            {
                throw new InvalidOperationException(
                    "NATURAL_PORTFOLIO_PARTITION_EXPECTED_SOURCE_REQUIRED");
            }
            if (!string.IsNullOrEmpty(focus))
            {
                throw new InvalidOperationException(
                    "NATURAL_PORTFOLIO_PARTITION_AND_FOCUS_CONFLICT");
            }
            if (!TryParseCanonicalNonNegativeInteger(
                    partitionIndexToken, out int partitionIndex)
                || !TryParseCanonicalPositiveInteger(
                    partitionCountToken, out int partitionCount)
                || partitionIndex >= partitionCount)
            {
                throw new InvalidOperationException(
                    "NATURAL_PORTFOLIO_PARTITION_COORDINATES_INVALID");
            }
            ProductionOutputClearanceNaturalPortfolioShardSnapshot[] ordered =
                shards.OrderBy(value => value.ShardId, StringComparer.Ordinal)
                    .ToArray();
            string expectedCountToken = Environment.GetEnvironmentVariable(
                ExpectedShardCountEnvironmentVariable);
            string expectedKeySetDigest = Environment.GetEnvironmentVariable(
                ExpectedShardKeySetDigestEnvironmentVariable);
            if (!TryParseCanonicalPositiveInteger(
                    expectedCountToken, out int expectedCount)
                || expectedCount != ordered.Length
                || !ProductionOutputClearanceProfileObservation.IsLowercaseSha256(
                    expectedKeySetDigest)
                || !string.Equals(expectedKeySetDigest,
                    ComputeCurrentShardKeySetDigest(ordered),
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    "NATURAL_PORTFOLIO_PARTITION_CURRENT_KEYSET_MISMATCH");
            }
            ProductionOutputClearanceNaturalPortfolioShardSnapshot[] selected =
                ordered.Where((_, index) => index % partitionCount == partitionIndex)
                    .ToArray();
            if (selected.Length == 0)
            {
                throw new InvalidOperationException(
                    "NATURAL_PORTFOLIO_PARTITION_EMPTY");
            }
            return selected;
        }
        if (string.IsNullOrEmpty(focus))
            return shards;
        if (!string.Equals(focus, focus.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "NATURAL_PORTFOLIO_FOCUS_SHARD_NON_CANONICAL: " + focus);
        }
        ProductionOutputClearanceNaturalPortfolioShardSnapshot[] matches = shards
            .Where(value => string.Equals(
                value.ShardId,
                focus,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            throw new InvalidOperationException(
                "NATURAL_PORTFOLIO_FOCUS_SHARD_NOT_EXACT: focus=" + focus
                + ";matches=" + matches.Length.ToString(
                    CultureInfo.InvariantCulture));
        }
        return matches.Concat(shards.Where(value => !ReferenceEquals(
                value,
                matches[0])))
            .ToArray();
    }

    internal static string ComputeCurrentShardKeySetDigest(
        IReadOnlyList<ProductionOutputClearanceNaturalPortfolioShardSnapshot> shards)
    {
        if (shards == null)
            throw new ArgumentNullException(nameof(shards));
        string[] ids = shards.Select(value => value?.ShardId
                ?? throw new InvalidOperationException(
                    "Natural shard key-set contains a null shard."))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (ids.Length == 0
            || ids.Distinct(StringComparer.Ordinal).Count() != ids.Length)
        {
            throw new InvalidOperationException(
                "Natural shard key-set is empty or duplicated.");
        }

        byte[] canonical = Utf8NoBom.GetBytes(string.Join("\n", ids) + "\n");
        using SHA256 sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(canonical))
            .Replace("-", string.Empty)
            .ToLowerInvariant();
    }

    private static void RequireExpectedSourceDigest(string actual)
    {
        string expected = Environment.GetEnvironmentVariable(
            ExpectedSourceDigestEnvironmentVariable);
        if (string.IsNullOrEmpty(expected))
            return;
        if (!ProductionOutputClearanceProfileObservation.IsLowercaseSha256(expected)
            || !string.Equals(actual, expected, StringComparison.Ordinal))
        {
            throw new ProductionOutputClearanceNaturalShardResumeException(
                "expected-current-source-digest-mismatch:expected="
                + expected + ":actual=" + actual);
        }
    }

    private static bool TryParseCanonicalNonNegativeInteger(
        string value,
        out int parsed) =>
        int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture,
            out parsed)
        && parsed >= 0
        && string.Equals(parsed.ToString(CultureInfo.InvariantCulture), value,
            StringComparison.Ordinal);

    private static bool TryParseCanonicalPositiveInteger(
        string value,
        out int parsed) =>
        TryParseCanonicalNonNegativeInteger(value, out parsed) && parsed > 0;

    private static NaturalArtifactStamp CaptureArtifactStamp() => new(
        V27BalanceArtifactWriter.ComputeSha256(ReportPath),
        V27BalanceArtifactWriter.ComputeSha256(ObservationsCsvPath),
        V27BalanceArtifactWriter.ComputeSha256(OutputSlicesCsvPath),
        new FileInfo(ReportPath).Length,
        new FileInfo(ObservationsCsvPath).Length,
        new FileInfo(OutputSlicesCsvPath).Length,
        File.GetLastWriteTimeUtc(ReportPath).Ticks,
        File.GetLastWriteTimeUtc(ObservationsCsvPath).Ticks,
        File.GetLastWriteTimeUtc(OutputSlicesCsvPath).Ticks);

    private readonly struct NaturalArtifactStamp
    {
        internal NaturalArtifactStamp(
            string reportSha256,
            string observationsSha256,
            string slicesSha256,
            long reportLength,
            long observationsLength,
            long slicesLength,
            long reportWriteTicks,
            long observationsWriteTicks,
            long slicesWriteTicks)
        {
            ReportSha256 = reportSha256;
            ObservationsSha256 = observationsSha256;
            SlicesSha256 = slicesSha256;
            ReportLength = reportLength;
            ObservationsLength = observationsLength;
            SlicesLength = slicesLength;
            ReportWriteTicks = reportWriteTicks;
            ObservationsWriteTicks = observationsWriteTicks;
            SlicesWriteTicks = slicesWriteTicks;
        }

        internal string ReportSha256 { get; }
        internal string ObservationsSha256 { get; }
        internal string SlicesSha256 { get; }
        private long ReportLength { get; }
        private long ObservationsLength { get; }
        private long SlicesLength { get; }
        private long ReportWriteTicks { get; }
        private long ObservationsWriteTicks { get; }
        private long SlicesWriteTicks { get; }

        internal bool EqualsExact(NaturalArtifactStamp other) =>
            string.Equals(ReportSha256, other.ReportSha256,
                StringComparison.Ordinal)
            && string.Equals(ObservationsSha256, other.ObservationsSha256,
                StringComparison.Ordinal)
            && string.Equals(SlicesSha256, other.SlicesSha256,
                StringComparison.Ordinal)
            && ReportLength == other.ReportLength
            && ObservationsLength == other.ObservationsLength
            && SlicesLength == other.SlicesLength
            && ReportWriteTicks == other.ReportWriteTicks
            && ObservationsWriteTicks == other.ObservationsWriteTicks
            && SlicesWriteTicks == other.SlicesWriteTicks;
    }

    private static void WriteArtifacts(
        ProductionOutputClearanceCurrentPortfolioSnapshot current,
        ProductionOutputClearanceCanonicalNaturalArtifactPortfolioSnapshot canonical,
        int resumed,
        int executed,
        ProductionOutputClearanceNaturalConsoleSnapshot console,
        string handlerRegistryFingerprint,
        string executorRegistryFingerprint,
        string clearanceProfileMode,
        string clearanceProfileAuthorityDigest,
        string expectedSourceDigest,
        string expectedSceneDigest,
        bool terminalPassed)
    {
        string source = V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest();
        string scene = V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest();
        if (!string.Equals(source, expectedSourceDigest, StringComparison.Ordinal)
            || !string.Equals(scene, expectedSceneDigest, StringComparison.Ordinal)
            || !string.Equals(scene,
                ProductionOutputClearanceNaturalRunIdentity
                    .OfficialGameplaySceneSha256,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "NATURAL_PORTFOLIO_OFFICIAL_SCENE_DIGEST_MISMATCH");

        V27BalanceArtifactWriter.WriteIfDifferent(ObservationsCsvPath, stream =>
            WriteObservationCsv(stream, current, canonical, source, scene));
        V27BalanceArtifactWriter.WriteIfDifferent(OutputSlicesCsvPath, stream =>
            WriteOutputSlicesCsv(stream, canonical, source, scene));
        string observationsSha = V27BalanceArtifactWriter.ComputeSha256(
            ObservationsCsvPath);
        string slicesSha = V27BalanceArtifactWriter.ComputeSha256(
            OutputSlicesCsvPath);
        string report =
            "schema=v27-production-output-clearance-natural-portfolio@2\n"
            + "result=" + (terminalPassed ? "PASS" : "IN_PROGRESS") + "\n"
            + "currentSourceDigest=" + source + "\n"
            + "gameplaySceneSha256=" + scene + "\n"
            + "currentPortfolioDigest=" + current.SourceDigest + "\n"
            + "measurementPortfolioDigest=" + current.Portfolio.SourceDigest + "\n"
            + "descriptorCoverageDigest="
                + current.ExecutableCoverage.SourceDigest + "\n"
            + "minimumV27Plans=" + MinimumV27BaselinePlanCount.ToString(
                CultureInfo.InvariantCulture) + "\n"
            + "measurementPlanCsvSha256="
                + V27BalanceArtifactWriter.ComputeSha256(
                    ProductionWorkRateCompositionPlayModeVerifier
                        .ClearanceMeasurementPlanCsvPath) + "\n"
            + "acceptedPortfolioDigest=" + canonical.SourceDigest + "\n"
            + "canonicalArtifactSchema="
                + ProductionOutputClearanceCanonicalNaturalArtifactPortfolioSnapshot
                    .Schema + "\n"
            + "plans=" + current.Shards.Count.ToString(CultureInfo.InvariantCulture)
            + "\nseeds=" + current.Portfolio.Seeds.Count.ToString(
                CultureInfo.InvariantCulture)
            + "\nobservations=" + canonical.Records.Count.ToString(
                CultureInfo.InvariantCulture)
            + "\noutputSlices=" + canonical.OutputSlices.Count
                .ToString(CultureInfo.InvariantCulture)
            + "\nresumed=" + resumed.ToString(CultureInfo.InvariantCulture)
            + "\nexecuted=" + executed.ToString(CultureInfo.InvariantCulture)
            + "\nhandlerRegistryFingerprint=" + handlerRegistryFingerprint
            + "\nexecutorRegistryFingerprint=" + executorRegistryFingerprint
            + "\nclearanceProfileMode=" + clearanceProfileMode
            + "\nclearanceProfileAuthorityDigest="
                + clearanceProfileAuthorityDigest
            + "\nobservationsCsvSha256=" + observationsSha
            + "\noutputSlicesCsvSha256=" + slicesSha
            + "\nconsoleWarnings=" + console.WarningCount.ToString(
                CultureInfo.InvariantCulture)
            + "\nconsoleErrors=" + console.ErrorCount.ToString(
                CultureInfo.InvariantCulture)
            + "\nsecondBuildByteDiff=0"
            + "\nsecondBuildLengthDiff=0"
            + "\nsecondBuildMtimeDiff=0\n";
        byte[] reportBytes = Utf8NoBom.GetBytes(report);
        V27BalanceArtifactWriter.WriteIfDifferent(ReportPath,
            stream => stream.Write(reportBytes, 0, reportBytes.Length));
    }

    private static void WriteObservationCsv(
        Stream stream,
        ProductionOutputClearanceCurrentPortfolioSnapshot current,
        ProductionOutputClearanceCanonicalNaturalArtifactPortfolioSnapshot canonical,
        string source,
        string scene)
    {
        using StreamWriter writer = new(stream, Utf8NoBom, 16384, leaveOpen: true);
        WriteCsvRow(writer,
            "schema", "definitionId", "workstationTag", "seedIndex",
            "deterministicSeed", "observationId", "facilitySemanticId",
            "operationSemanticId", "batchSemanticId", "actualBatchMassGrams",
            "clearanceMicroHours", "canonicalResolvedOutputVectorDigest",
            "canonicalReceiptDigest", "canonicalRunDigest",
            "currentSourceDigest", "gameplaySceneSha256",
            "measurementPortfolioDigest", "acceptedPortfolioDigest");
        foreach (ProductionOutputClearanceCanonicalNaturalObservationRecord value in
                 canonical.Records)
        {
            WriteCsvRow(writer,
                ProductionOutputClearanceCanonicalNaturalObservationRecord.Schema,
                value.Fixture.Plan.DefinitionId,
                value.Fixture.Plan.WorkstationTag,
                value.Fixture.SeedIndex,
                value.Fixture.DeterministicSeed,
                value.ObservationId,
                value.FacilitySemanticId,
                value.OperationSemanticId,
                value.BatchSemanticId,
                value.ActualBatchMassGrams,
                value.ClearanceMicroHours,
                value.CanonicalResolvedOutputVectorDigest,
                value.CanonicalReceiptDigest,
                value.CanonicalRunDigest,
                source,
                scene,
                current.Portfolio.SourceDigest,
                canonical.SourceDigest);
        }
        writer.Flush();
    }

    private static void WriteOutputSlicesCsv(
        Stream stream,
        ProductionOutputClearanceCanonicalNaturalArtifactPortfolioSnapshot canonical,
        string source,
        string scene)
    {
        using StreamWriter writer = new(stream, Utf8NoBom, 16384, leaveOpen: true);
        WriteCsvRow(writer,
            "schema", "observationId", "batchSemanticId", "outputLineId",
            "itemId", "itemInstanceSemanticId", "stackSemanticId",
            "sliceOrdinal", "quantity", "massGrams", "capabilityFingerprint",
            "semanticDigest",
            "currentSourceDigest", "gameplaySceneSha256");
        foreach (ProductionOutputClearanceCanonicalNaturalOutputSliceRecord slice in
                 canonical.OutputSlices)
        {
            WriteCsvRow(writer,
                ProductionOutputClearanceCanonicalNaturalOutputSliceRecord.Schema,
                slice.ObservationId,
                slice.BatchSemanticId,
                slice.OutputLineId,
                slice.ItemId,
                slice.ItemInstanceSemanticId,
                slice.StackSemanticId,
                slice.SliceOrdinal,
                slice.Quantity,
                slice.MassGrams,
                slice.CapabilityFingerprint,
                slice.SemanticDigest,
                source,
                scene);
        }
        writer.Flush();
    }

    private static void WriteCsvRow(StreamWriter writer, params object[] fields)
    {
        for (int index = 0; index < fields.Length; index++)
        {
            if (index > 0)
                writer.Write(',');
            string value = Convert.ToString(fields[index],
                CultureInfo.InvariantCulture) ?? string.Empty;
            V27BalanceCsvSerializer.WriteEscapedField(writer, value.AsSpan());
        }
        writer.Write('\r');
        writer.Write('\n');
    }
}

public sealed class ProductionOutputClearanceNaturalConsoleSnapshot
{
    public ProductionOutputClearanceNaturalConsoleSnapshot(
        int warningCount,
        int errorCount)
    {
        if (warningCount < 0 || errorCount < 0)
            throw new ArgumentOutOfRangeException(nameof(warningCount));
        WarningCount = warningCount;
        ErrorCount = errorCount;
    }

    public int WarningCount { get; }
    public int ErrorCount { get; }
}
#endif
