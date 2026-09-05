#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using VContainer;

public sealed class ProductionOutputClearanceCurrentPortfolioSnapshot
{
    private readonly IReadOnlyDictionary<string,
        ProductionOutputClearanceExecutableDescriptor> descriptorsByPlanKey;

    internal ProductionOutputClearanceCurrentPortfolioSnapshot(
        ProductionAuthoredThroughputFacilityScopeSnapshot authoredScope,
        ProductionOutputClearanceMeasurementScopeSnapshot measurementScope,
        ProductionOutputClearanceExecutableDescriptorCoverage executableCoverage,
        ProductionOutputClearanceMeasurementPortfolioSnapshot portfolio)
    {
        AuthoredScope = authoredScope
            ?? throw new ArgumentNullException(nameof(authoredScope));
        MeasurementScope = measurementScope
            ?? throw new ArgumentNullException(nameof(measurementScope));
        ExecutableCoverage = executableCoverage
            ?? throw new ArgumentNullException(nameof(executableCoverage));
        Portfolio = portfolio
            ?? throw new ArgumentNullException(nameof(portfolio));

        if (AuthoredScope.AutomaticProducerCount <= 0
            || AuthoredScope.Coverage.Gaps.Count != 0
            || MeasurementScope.Gaps.Count != 0
            || ExecutableCoverage.Gaps.Count != 0
            || MeasurementScope.Plans.Count
                != AuthoredScope.AutomaticProducerCount
            || ExecutableCoverage.Descriptors.Count
                != MeasurementScope.Plans.Count
            || Portfolio.Scope != MeasurementScope
            || Portfolio.Seeds.Count
                != ProductionOutputClearanceMeasurementPortfolioAuthority
                    .RequiredSeedCount
            || Portfolio.Fixtures.Count != checked(
                MeasurementScope.Plans.Count * Portfolio.Seeds.Count))
        {
            throw new InvalidOperationException(
                "Current natural-clearance portfolio authorities are incomplete or "
                + "do not share one exact denominator.");
        }

        string[] authoredKeys = AuthoredScope.Coverage.CompleteEnvelopes
            .Select(value => Key(value.DefinitionId, value.WorkstationTag))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] measurementKeys = MeasurementScope.Plans
            .Select(value => Key(value.DefinitionId, value.WorkstationTag))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] descriptorKeys = ExecutableCoverage.Descriptors
            .Select(value => Key(
                value.Plan.DefinitionId,
                value.Plan.WorkstationTag))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (authoredKeys.Distinct(StringComparer.Ordinal).Count()
                != authoredKeys.Length
            || measurementKeys.Distinct(StringComparer.Ordinal).Count()
                != measurementKeys.Length
            || descriptorKeys.Distinct(StringComparer.Ordinal).Count()
                != descriptorKeys.Length
            || !authoredKeys.SequenceEqual(
                measurementKeys,
                StringComparer.Ordinal)
            || !measurementKeys.SequenceEqual(
                descriptorKeys,
                StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Authored, measurement, and executable portfolio plan keys are not "
                + "an exact bijection.");
        }

        descriptorsByPlanKey = ExecutableCoverage.Descriptors.ToDictionary(
            value => Key(value.Plan.DefinitionId, value.Plan.WorkstationTag),
            value => value,
            StringComparer.Ordinal);
        if (Portfolio.Fixtures.Any(value => value == null
                || !descriptorsByPlanKey.TryGetValue(
                    Key(value.Plan.DefinitionId, value.Plan.WorkstationTag),
                    out ProductionOutputClearanceExecutableDescriptor descriptor)
                || !ReferenceEquals(descriptor.Plan, value.Plan)))
        {
            throw new InvalidOperationException(
                "Frozen portfolio fixtures are not joined to their exact executable "
                + "descriptor plan instances.");
        }

        PayloadCounts = ExecutableCoverage.Descriptors
            .GroupBy(value => value.Payload.PayloadKind, StringComparer.Ordinal)
            .OrderBy(value => value.Key, StringComparer.Ordinal)
            .ToDictionary(
                value => value.Key,
                value => value.Count(),
                StringComparer.Ordinal);

        ProductionOutputClearanceNaturalPortfolioShardSnapshot[] shards =
            MeasurementScope.Plans
                .OrderBy(value => value.DefinitionId, StringComparer.Ordinal)
                .ThenBy(value => value.WorkstationTag, StringComparer.Ordinal)
                .Select(plan =>
                {
                    string planKey = Key(plan.DefinitionId, plan.WorkstationTag);
                    ProductionOutputClearanceMeasurementFixture[] fixtures =
                        Portfolio.Fixtures
                            .Where(value => ReferenceEquals(value.Plan, plan))
                            .OrderBy(value => value.SeedIndex)
                            .ThenBy(value => value.DeterministicSeed)
                            .ToArray();
                    return new
                        ProductionOutputClearanceNaturalPortfolioShardSnapshot(
                            descriptorsByPlanKey[planKey],
                            fixtures,
                            Portfolio.Seeds);
                })
                .ToArray();
        if (shards.Length != MeasurementScope.Plans.Count
            || shards.Select(value => value.ShardId)
                .Distinct(StringComparer.Ordinal).Count() != shards.Length
            || shards.Sum(value => value.Fixtures.Count)
                != Portfolio.Fixtures.Count)
        {
            throw new InvalidOperationException(
                "Current natural-clearance shards are incomplete or duplicated.");
        }
        Shards = Array.AsReadOnly(shards);

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-current-portfolio@1");
        digest.Append(AuthoredScope.SourceDigest);
        digest.Append(MeasurementScope.SourceDigest);
        digest.Append(ExecutableCoverage.SourceDigest);
        digest.Append(Portfolio.SourceDigest);
        digest.Append(PayloadCounts.Count);
        foreach (KeyValuePair<string, int> payload in PayloadCounts
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            digest.Append(payload.Key);
            digest.Append(payload.Value);
        }
        digest.Append(Shards.Count);
        foreach (ProductionOutputClearanceNaturalPortfolioShardSnapshot shard in
                 Shards)
            digest.Append(shard.SourceDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public ProductionAuthoredThroughputFacilityScopeSnapshot AuthoredScope
        { get; }
    public ProductionOutputClearanceMeasurementScopeSnapshot MeasurementScope
        { get; }
    public ProductionOutputClearanceExecutableDescriptorCoverage
        ExecutableCoverage { get; }
    public ProductionOutputClearanceMeasurementPortfolioSnapshot Portfolio
        { get; }
    public IReadOnlyDictionary<string, int> PayloadCounts { get; }
    public IReadOnlyList<
        ProductionOutputClearanceNaturalPortfolioShardSnapshot> Shards { get; }
    public string SourceDigest { get; }

    public ProductionOutputClearanceExecutableDescriptor GetDescriptor(
        ProductionOutputClearanceMeasurementFixture fixture)
    {
        if (fixture == null)
            throw new ArgumentNullException(nameof(fixture));
        string key = Key(
            fixture.Plan.DefinitionId,
            fixture.Plan.WorkstationTag);
        if (!descriptorsByPlanKey.TryGetValue(
                key,
                out ProductionOutputClearanceExecutableDescriptor descriptor)
            || !ReferenceEquals(descriptor.Plan, fixture.Plan))
        {
            throw new InvalidOperationException(
                "Fixture is not owned by this current portfolio snapshot: "
                + key.Replace('\n', '/'));
        }
        return descriptor;
    }

    private static string Key(string definitionId, string workstationTag) =>
        definitionId + "\n" + workstationTag;
}

public sealed class ProductionOutputClearanceNaturalPortfolioShardSnapshot
{
    internal ProductionOutputClearanceNaturalPortfolioShardSnapshot(
        ProductionOutputClearanceExecutableDescriptor descriptor,
        IReadOnlyList<ProductionOutputClearanceMeasurementFixture> fixtures,
        IReadOnlyList<int> portfolioSeeds)
    {
        Descriptor = descriptor
            ?? throw new ArgumentNullException(nameof(descriptor));
        ProductionOutputClearanceMeasurementFixture[] ordered = (fixtures
                ?? throw new ArgumentNullException(nameof(fixtures)))
            .OrderBy(value => value?.SeedIndex)
            .ThenBy(value => value?.DeterministicSeed)
            .ToArray();
        int[] seeds = (portfolioSeeds
                ?? throw new ArgumentNullException(nameof(portfolioSeeds)))
            .ToArray();
        if (ordered.Length != seeds.Length
            || ordered.Length
                != ProductionOutputClearanceMeasurementPortfolioAuthority
                    .RequiredSeedCount
            || ordered.Any(value => value == null
                || !ReferenceEquals(value.Plan, Descriptor.Plan))
            || ordered.Select(value => value.SeedIndex)
                .Distinct().Count() != ordered.Length
            || ordered.Select(value => value.DeterministicSeed)
                .Distinct().Count() != ordered.Length
            || !ordered.Select(value => value.SeedIndex)
                .SequenceEqual(Enumerable.Range(0, seeds.Length))
            || !ordered.Select(value => value.DeterministicSeed)
                .SequenceEqual(seeds))
        {
            throw new InvalidOperationException(
                "A natural-clearance shard must contain the exact ordered seed "
                + "portfolio for one executable descriptor.");
        }

        ShardId = "natural-output-clearance-shard:"
            + Descriptor.Plan.DefinitionId + ":"
            + Descriptor.Plan.WorkstationTag;
        Fixtures = Array.AsReadOnly(ordered);
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-natural-shard@1");
        digest.Append(ShardId);
        digest.Append(Descriptor.SourceDigest);
        digest.Append(Fixtures.Count);
        foreach (ProductionOutputClearanceMeasurementFixture fixture in Fixtures)
            digest.Append(fixture.SourceDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public string ShardId { get; }
    public ProductionOutputClearanceExecutableDescriptor Descriptor { get; }
    public IReadOnlyList<ProductionOutputClearanceMeasurementFixture> Fixtures
        { get; }
    public string SourceDigest { get; }
}

public static class ProductionOutputClearanceCurrentPortfolioCapture
{
    public static ProductionOutputClearanceCurrentPortfolioSnapshot Capture(
        IObjectResolver container)
    {
        if (container == null)
            throw new ArgumentNullException(nameof(container));

        ProductionAuthoredThroughputFacilityScopeSnapshot authored =
            ProductionAuthoredThroughputFacilityScopeDebugScenarios.Capture(
                container);
        ProductionOutputClearanceMeasurementScopeSnapshot measurement =
            ProductionAuthoredThroughputFacilityScopeDebugScenarios
                .CaptureMeasurementScope(container, authored);
        ProductionOutputClearanceExecutableDescriptorCoverage executable =
            ProductionAuthoredThroughputFacilityScopeDebugScenarios
                .CaptureRecipeExecutableDescriptors(container, measurement);
        ProductionOutputClearanceMeasurementPortfolioSnapshot portfolio =
            ProductionOutputClearanceMeasurementPortfolioAuthority.CaptureCurrent(
                measurement);
        return new ProductionOutputClearanceCurrentPortfolioSnapshot(
            authored,
            measurement,
            executable,
            portfolio);
    }
}
#endif
