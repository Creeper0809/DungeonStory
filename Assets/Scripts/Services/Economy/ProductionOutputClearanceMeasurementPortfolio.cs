using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ProductionOutputClearanceMeasurementFixture
{
    internal ProductionOutputClearanceMeasurementFixture(
        ProductionOutputClearanceMeasurementPlan plan,
        int seedIndex,
        int deterministicSeed)
    {
        Plan = plan ?? throw new ArgumentNullException(nameof(plan));
        if (seedIndex < 0 || deterministicSeed <= 0)
            throw new ArgumentOutOfRangeException(nameof(seedIndex));
        SeedIndex = seedIndex;
        DeterministicSeed = deterministicSeed;
        ProductionOutputClearanceMeasurementCandidate winner = plan.Winner;
        ObservationId = "output-clearance-observation:"
            + plan.DefinitionId + ":" + plan.WorkstationTag + ":"
            + deterministicSeed;

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-measurement-fixture@1");
        digest.Append(plan.SourceDigest);
        digest.Append(winner.SourceDigest);
        digest.Append(seedIndex);
        digest.Append(deterministicSeed);
        digest.Append(ObservationId);
        SourceDigest = digest.ComputeSha256();
    }

    public ProductionOutputClearanceMeasurementPlan Plan { get; }
    public ProductionOutputClearanceMeasurementCandidate Winner => Plan.Winner;
    public int SeedIndex { get; }
    public int DeterministicSeed { get; }
    public string ObservationId { get; }
    public string SourceDigest { get; }
}

public sealed class ProductionOutputClearanceMeasurementPortfolioSnapshot
{
    internal ProductionOutputClearanceMeasurementPortfolioSnapshot(
        ProductionOutputClearanceMeasurementScopeSnapshot scope,
        IReadOnlyList<int> deterministicSeeds)
    {
        Scope = scope ?? throw new ArgumentNullException(nameof(scope));
        if (scope.Gaps.Count != 0
            || scope.Plans.Count == 0
            || scope.Plans.Count != scope.AuthoredScope.AutomaticProducerCount)
        {
            throw new InvalidOperationException(
                "A complete clearance measurement plan scope is required.");
        }
        int[] seeds = (deterministicSeeds
                ?? throw new ArgumentNullException(nameof(deterministicSeeds)))
            .ToArray();
        if (seeds.Length == 0
            || seeds.Any(value => value <= 0)
            || seeds.Distinct().Count() != seeds.Length)
        {
            throw new InvalidOperationException(
                "Clearance measurement portfolio seeds are empty, nonpositive, or duplicated.");
        }

        List<ProductionOutputClearanceMeasurementFixture> fixtures = new(
            checked(scope.Plans.Count * seeds.Length));
        foreach (ProductionOutputClearanceMeasurementPlan plan in scope.Plans
                     .OrderBy(value => value.DefinitionId,
                         StringComparer.Ordinal)
                     .ThenBy(value => value.WorkstationTag,
                         StringComparer.Ordinal))
        {
            for (int index = 0; index < seeds.Length; index++)
            {
                fixtures.Add(new ProductionOutputClearanceMeasurementFixture(
                    plan,
                    index,
                    seeds[index]));
            }
        }

        string[] expectedKeys = scope.Plans.Select(Key)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] actualKeys = fixtures.Select(value => Key(value.Plan))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        int expectedFixtureCount = checked(scope.Plans.Count * seeds.Length);
        if (fixtures.Count != expectedFixtureCount
            || !actualKeys.SequenceEqual(expectedKeys, StringComparer.Ordinal)
            || fixtures.Select(value => value.ObservationId)
                .Distinct(StringComparer.Ordinal).Count() != fixtures.Count
            || fixtures.GroupBy(value => Key(value.Plan), StringComparer.Ordinal)
                .Any(group => group.Select(value => value.DeterministicSeed)
                    .Distinct().Count() != seeds.Length))
        {
            throw new InvalidOperationException(
                "Clearance measurement portfolio does not form an exact plan-by-seed matrix.");
        }

        Seeds = Array.AsReadOnly(seeds);
        Fixtures = Array.AsReadOnly(fixtures.ToArray());
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-measurement-portfolio@1");
        digest.Append(scope.SourceDigest);
        digest.Append(Seeds.Count);
        foreach (int seed in Seeds)
            digest.Append(seed);
        digest.Append(Fixtures.Count);
        foreach (ProductionOutputClearanceMeasurementFixture fixture in Fixtures)
            digest.Append(fixture.SourceDigest);
        SourceDigest = digest.ComputeSha256();
    }

    public ProductionOutputClearanceMeasurementScopeSnapshot Scope { get; }
    public IReadOnlyList<int> Seeds { get; }
    public IReadOnlyList<ProductionOutputClearanceMeasurementFixture> Fixtures
        { get; }
    public string SourceDigest { get; }

    private static string Key(ProductionOutputClearanceMeasurementPlan value) =>
        value.DefinitionId + "\n" + value.WorkstationTag;
}

public static class ProductionOutputClearanceMeasurementPortfolioAuthority
{
    public const int FirstSeed = 157181;
    public const int RequiredSeedCount = 32;

    public static ProductionOutputClearanceMeasurementPortfolioSnapshot
        CaptureCurrent(
            ProductionOutputClearanceMeasurementScopeSnapshot scope)
    {
        int[] seeds = Enumerable.Range(0, RequiredSeedCount)
            .Select(index => checked(FirstSeed + index))
            .ToArray();
        return new ProductionOutputClearanceMeasurementPortfolioSnapshot(
            scope,
            seeds);
    }
}
