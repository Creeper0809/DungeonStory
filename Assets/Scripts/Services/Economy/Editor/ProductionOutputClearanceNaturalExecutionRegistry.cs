#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public sealed class ProductionOutputClearanceNaturalExecutionRequest
{
    internal ProductionOutputClearanceNaturalExecutionRequest(
        ProductionOutputClearanceNaturalPortfolioShardSnapshot shard,
        ProductionOutputClearanceMeasurementFixture fixture)
    {
        Shard = shard ?? throw new ArgumentNullException(nameof(shard));
        Fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));
        if (!ReferenceEquals(fixture.Plan, shard.Descriptor.Plan)
            || !shard.Fixtures.Contains(fixture))
        {
            throw new InvalidOperationException(
                "Natural execution request fixture is not owned by its shard.");
        }

        ActionId = "qa:natural-output-clearance:"
            + Fixture.Plan.DefinitionId + ":"
            + Fixture.Plan.WorkstationTag + ":"
            + Fixture.DeterministicSeed;
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-natural-execution-request@1");
        digest.Append(Shard.SourceDigest);
        digest.Append(Fixture.SourceDigest);
        digest.Append(ActionId);
        SourceDigest = digest.ComputeSha256();
    }

    public ProductionOutputClearanceNaturalPortfolioShardSnapshot Shard { get; }
    public ProductionOutputClearanceMeasurementFixture Fixture { get; }
    public ProductionOutputClearanceExecutableDescriptor Descriptor =>
        Shard.Descriptor;
    public string ActionId { get; }
    public string SourceDigest { get; }
}

public sealed class ProductionOutputClearanceNaturalExecutionResult
{
    public bool IsTerminal { get; private set; }
    public bool Succeeded { get; private set; }
    public string FailureReason { get; private set; } = string.Empty;
    public ProductionOutputClearanceNaturalShardEvidence Evidence
        { get; private set; }

    public void Complete(
        ProductionOutputClearanceNaturalShardEvidence evidence)
    {
        EnsureMutable();
        Evidence = evidence ?? throw new ArgumentNullException(nameof(evidence));
        IsTerminal = true;
        Succeeded = true;
    }

    public void Fail(string failureReason)
    {
        EnsureMutable();
        if (string.IsNullOrWhiteSpace(failureReason)
            || !string.Equals(
                failureReason,
                failureReason.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical natural-execution failure reason is required.",
                nameof(failureReason));
        }
        FailureReason = failureReason;
        Evidence = null;
        IsTerminal = true;
        Succeeded = false;
    }

    private void EnsureMutable()
    {
        if (IsTerminal)
        {
            throw new InvalidOperationException(
                "Natural execution result is already terminal.");
        }
    }
}

public interface IProductionOutputClearanceNaturalMeasurementExecutor
{
    string ExecutorId { get; }
    int ContractVersion { get; }
    string PayloadKind { get; }

    IEnumerator Execute(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionOutputClearanceNaturalExecutionResult result);
}

public sealed class ProductionOutputClearanceNaturalMeasurementExecutorRegistry
{
    private readonly IReadOnlyDictionary<string,
        IProductionOutputClearanceNaturalMeasurementExecutor> executors;

    public ProductionOutputClearanceNaturalMeasurementExecutorRegistry(
        IEnumerable<IProductionOutputClearanceNaturalMeasurementExecutor>
            executors)
    {
        IProductionOutputClearanceNaturalMeasurementExecutor[] ordered =
            (executors ?? throw new ArgumentNullException(nameof(executors)))
            .OrderBy(value => value?.PayloadKind, StringComparer.Ordinal)
            .ThenBy(value => value?.ExecutorId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0
            || ordered.Any(value => value == null
                || !Canonical(value.PayloadKind)
                || !Canonical(value.ExecutorId)
                || value.ContractVersion <= 0)
            || ordered.Select(value => value.PayloadKind)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            throw new InvalidOperationException(
                "Natural measurement executors require one canonical owner per "
                + "payload kind.");
        }

        this.executors = ordered.ToDictionary(
            value => value.PayloadKind,
            value => value,
            StringComparer.Ordinal);
        PayloadKinds = Array.AsReadOnly(ordered
            .Select(value => value.PayloadKind)
            .ToArray());
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-natural-executor-registry@1");
        digest.Append(ordered.Length);
        foreach (IProductionOutputClearanceNaturalMeasurementExecutor executor in
                 ordered)
        {
            digest.Append(executor.PayloadKind);
            digest.Append(executor.ExecutorId);
            digest.Append(executor.ContractVersion);
        }
        RegistryFingerprint = digest.ComputeSha256();
    }

    public IReadOnlyList<string> PayloadKinds { get; }
    public string RegistryFingerprint { get; }

    public IEnumerator Execute(
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionOutputClearanceNaturalExecutionResult result)
    {
        if (request == null)
            throw new ArgumentNullException(nameof(request));
        if (result == null)
            throw new ArgumentNullException(nameof(result));
        if (result.IsTerminal)
        {
            throw new InvalidOperationException(
                "A terminal result cannot be reused for natural execution.");
        }
        string payloadKind = request.Descriptor.Payload.PayloadKind;
        if (!executors.TryGetValue(
                payloadKind,
                out IProductionOutputClearanceNaturalMeasurementExecutor executor))
        {
            throw new InvalidOperationException(
                "NATURAL_PORTFOLIO_EXECUTOR_UNREGISTERED: " + payloadKind);
        }
        IEnumerator execution = executor.Execute(request, result);
        if (execution == null)
        {
            throw new InvalidOperationException(
                "Natural measurement executor returned a null coroutine: "
                + executor.ExecutorId);
        }
        return ExecuteChecked(executor, request, result, execution);
    }

    public void RequireExactCoverage(
        ProductionOutputClearanceCurrentPortfolioSnapshot portfolio)
    {
        if (portfolio == null)
            throw new ArgumentNullException(nameof(portfolio));
        string[] expected = portfolio.PayloadCounts.Keys
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] actual = PayloadKinds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Natural executor payload coverage differs from the current "
                + "portfolio. expected=" + string.Join("|", expected)
                + ";actual=" + string.Join("|", actual));
        }
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal)
        && !value.Any(char.IsWhiteSpace);

    private static IEnumerator ExecuteChecked(
        IProductionOutputClearanceNaturalMeasurementExecutor executor,
        ProductionOutputClearanceNaturalExecutionRequest request,
        ProductionOutputClearanceNaturalExecutionResult result,
        IEnumerator execution)
    {
        try
        {
            while (execution.MoveNext())
                yield return execution.Current;
        }
        finally
        {
            (execution as IDisposable)?.Dispose();
        }

        if (!result.IsTerminal)
        {
            throw new InvalidOperationException(
                "Natural measurement executor returned without a terminal result: "
                + executor.ExecutorId);
        }
        if (result.Succeeded
            && (result.Evidence == null
                || !ReferenceEquals(result.Evidence.Request, request)))
        {
            throw new InvalidOperationException(
                "Natural measurement executor completed with evidence for a "
                + "different request: " + executor.ExecutorId);
        }
        if (!result.Succeeded && result.Evidence != null)
        {
            throw new InvalidOperationException(
                "Failed natural measurement execution retained success evidence: "
                + executor.ExecutorId);
        }
    }
}
#endif
