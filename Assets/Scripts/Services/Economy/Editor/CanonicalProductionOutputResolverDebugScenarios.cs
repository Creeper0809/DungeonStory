#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class CanonicalProductionOutputResolverDebugScenarios
{
    private const int RootSeed = 918273;
    private const int CycleSequence = 17;
    private const string RecipeId = "recipe:test:keyed-output";
    private static readonly ProductionBillId BillId =
        (ProductionBillId)"production-bill:keyed-output";

    [MenuItem("DungeonStory/Debug/Economy/Run Canonical Output Resolver Contracts")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("Canonical production output resolver contracts passed.");
    }

    public static void RunAll()
    {
        VerifyOrderShuffleAndDeclaredLossProjection();
        VerifyUnrelatedSharedDrawIndependence();
        VerifySaveLikeReplay();
        VerifyKeyUniquenessAndInvalidInputs();
    }

    private static void VerifyOrderShuffleAndDeclaredLossProjection()
    {
        ProductionOutputDefinition[] authored = CreateDefinitions();
        CanonicalProductionOutputResolver resolver = new(
            new RandomStreamProvider(RootSeed));
        CanonicalProductionOutputResolution forward = Resolve(resolver, authored);
        CanonicalProductionOutputResolution shuffled = Resolve(
            resolver,
            new[] { authored[2], authored[0], authored[1] });

        RequireEquivalent(forward, shuffled, "output definition order shuffle");
        Require(
            forward.Lines.Select(value => value.OutputLineId)
                .SequenceEqual(
                    forward.Lines.Select(value => value.OutputLineId)
                        .OrderBy(value => value, StringComparer.Ordinal),
                    StringComparer.Ordinal),
            "resolved output lines are not in deterministic ordinal order");
        CanonicalProductionResolvedOutputLine declaredLoss = forward.Lines.Single(
            value => value.Role == ProductionOutputRole.DeclaredLoss);
        Require(!declaredLoss.IsPhysical,
            "declared loss was projected as a physical output");
        Require(forward.Lines.Where(value =>
                value.Role != ProductionOutputRole.DeclaredLoss)
            .All(value => value.IsPhysical),
            "a non-loss output was projected as non-physical");
    }

    private static void VerifyUnrelatedSharedDrawIndependence()
    {
        RandomStreamProvider provider = new(RootSeed);
        CanonicalProductionOutputResolver resolver = new(provider);
        ProductionOutputDefinition[] authored = CreateDefinitions();
        CanonicalProductionOutputResolution before = Resolve(resolver, authored);

        IRandomStream unrelated = provider.Get("economy:production");
        for (int index = 0; index < 257; index++)
            unrelated.NextFloat();

        CanonicalProductionOutputResolution after = Resolve(resolver, authored);
        RequireEquivalent(before, after, "unrelated shared RNG draws");
    }

    private static void VerifySaveLikeReplay()
    {
        ProductionOutputDefinition[] authored = CreateDefinitions();
        CanonicalProductionOutputResolution original = Resolve(
            new CanonicalProductionOutputResolver(
                new RandomStreamProvider(RootSeed)),
            authored);
        ProductionOutputDefinition[] restoredDefinitions = authored
            .Select(value => new ProductionOutputDefinition(
                value.OutputLineId,
                value.Role,
                value.ItemId,
                value.Amount,
                value.Probability))
            .Reverse()
            .ToArray();
        CanonicalProductionOutputResolution replay = Resolve(
            new CanonicalProductionOutputResolver(
                new RandomStreamProvider(original.RootSeed)),
            restoredDefinitions);

        RequireEquivalent(original, replay, "save-like replay");
    }

    private static void VerifyKeyUniquenessAndInvalidInputs()
    {
        ProductionOutputDefinition[] authored = CreateDefinitions();
        CanonicalProductionOutputResolver resolver = new(
            new RandomStreamProvider(RootSeed));
        CanonicalProductionOutputResolution resolution = Resolve(resolver, authored);
        var keys = new HashSet<CounterfactualRandomKey>();
        foreach (CanonicalProductionResolvedOutputLine line in resolution.Lines)
        {
            Require(keys.Add(line.InclusionKey),
                $"duplicate inclusion key for {line.OutputLineId}");
            Require(keys.Add(line.FractionalRoundingKey),
                $"duplicate fractional-rounding key for {line.OutputLineId}");
        }
        Require(keys.Count == resolution.Lines.Count * 2,
            "production output roll keys are not unique per line and roll kind");

        RequireThrows<InvalidOperationException>(
            () => Resolve(resolver, new[] { authored[0], authored[0] }),
            "duplicate output line IDs were accepted");
        RequireThrows<ArgumentOutOfRangeException>(
            () => resolver.Resolve(
                BillId,
                CycleSequence,
                RecipeId,
                authored,
                0f,
                ProductionProcessKind.PassiveBatch,
                42f),
            "zero output multiplier was accepted");
        RequireThrows<ArgumentException>(
            () => resolver.Resolve(
                BillId,
                CycleSequence,
                " recipe:bad",
                authored,
                1f,
                ProductionProcessKind.PassiveBatch,
                42f),
            "noncanonical recipe ID was accepted");

        ProductionOutputDefinition invalidProbability =
            new("output:invalid-probability", ProductionOutputRole.Byproduct,
                "material:test-invalid", 1, 1f);
        typeof(ProductionOutputDefinition)
            .GetField("probability", BindingFlags.Instance | BindingFlags.NonPublic)
            ?.SetValue(invalidProbability, float.NaN);
        RequireThrows<InvalidOperationException>(
            () => Resolve(resolver, new[] { invalidProbability }),
            "non-finite authored probability was accepted");
    }

    private static CanonicalProductionOutputResolution Resolve(
        CanonicalProductionOutputResolver resolver,
        IEnumerable<ProductionOutputDefinition> definitions) => resolver.Resolve(
        BillId,
        CycleSequence,
        RecipeId,
        definitions,
        1.375f,
        ProductionProcessKind.PassiveBatch,
        42f);

    private static ProductionOutputDefinition[] CreateDefinitions() => new[]
    {
        new ProductionOutputDefinition(
            "output:main",
            ProductionOutputRole.Main,
            "material:test-main",
            3,
            1f),
        new ProductionOutputDefinition(
            "output:material:test-dust",
            ProductionOutputRole.Byproduct,
            "material:test-dust",
            2,
            0.47f),
        new ProductionOutputDefinition(
            "output:fermentation-gas",
            ProductionOutputRole.DeclaredLoss,
            "loss:fermentation-gas",
            1,
            0.83f)
    };

    private static void RequireEquivalent(
        CanonicalProductionOutputResolution expected,
        CanonicalProductionOutputResolution actual,
        string context)
    {
        Require(expected.RootSeed == actual.RootSeed
            && expected.BillId == actual.BillId
            && expected.CycleSequence == actual.CycleSequence
            && string.Equals(expected.RecipeId, actual.RecipeId, StringComparison.Ordinal)
            && expected.CombinedOutputMultiplier.Equals(
                actual.CombinedOutputMultiplier)
            && expected.OutputFactorNumerator == actual.OutputFactorNumerator
            && expected.OutputFactorDenominator == actual.OutputFactorDenominator
            && expected.ProcessKind == actual.ProcessKind
            && expected.PassiveBatchIntegrity.Equals(actual.PassiveBatchIntegrity)
            && expected.Lines.Count == actual.Lines.Count,
            $"{context} changed the resolution envelope");

        for (int index = 0; index < expected.Lines.Count; index++)
        {
            CanonicalProductionResolvedOutputLine left = expected.Lines[index];
            CanonicalProductionResolvedOutputLine right = actual.Lines[index];
            Require(left.DeterministicOrdinal == right.DeterministicOrdinal
                && string.Equals(left.OutputLineId, right.OutputLineId,
                    StringComparison.Ordinal)
                && left.Role == right.Role
                && string.Equals(left.ItemId, right.ItemId, StringComparison.Ordinal)
                && left.AuthoredQuantity == right.AuthoredQuantity
                && left.InclusionProbability.Equals(right.InclusionProbability)
                && left.InclusionKey.Equals(right.InclusionKey)
                && left.InclusionRoll.Equals(right.InclusionRoll)
                && left.Included == right.Included
                && left.FractionalRoundingKey.Equals(right.FractionalRoundingKey)
                && left.ScaledQuantity == right.ScaledQuantity
                && left.FractionalThreshold == right.FractionalThreshold
                && left.FractionalRoundingRoll.Equals(right.FractionalRoundingRoll)
                && left.FractionalRoundedUp == right.FractionalRoundedUp
                && left.QuantityBeforeIntegrity == right.QuantityBeforeIntegrity
                && left.PassiveIntegrityPenaltyApplied
                    == right.PassiveIntegrityPenaltyApplied
                && left.ResolvedQuantity == right.ResolvedQuantity
                && left.IsPhysical == right.IsPhysical,
                $"{context} changed resolved line '{left.OutputLineId}'");
        }
    }

    private static void RequireThrows<TException>(
        Action action,
        string message)
        where TException : Exception
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (TException)
        {
            rejected = true;
        }
        Require(rejected, message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
