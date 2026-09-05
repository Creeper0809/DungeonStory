using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

public sealed class CanonicalProductionOutputResolution
{
    internal CanonicalProductionOutputResolution(
        int rootSeed,
        ProductionBillId billId,
        int cycleSequence,
        string recipeId,
        ProductionOutputFactor combinedOutputFactor,
        ProductionProcessKind processKind,
        float passiveBatchIntegrity,
        IReadOnlyList<CanonicalProductionResolvedOutputLine> lines)
    {
        RootSeed = rootSeed;
        BillId = billId;
        CycleSequence = cycleSequence;
        RecipeId = recipeId;
        CombinedOutputMultiplier = combinedOutputFactor.ToFloat();
        OutputFactorNumerator = combinedOutputFactor.Numerator;
        OutputFactorDenominator = combinedOutputFactor.Denominator;
        ProcessKind = processKind;
        PassiveBatchIntegrity = passiveBatchIntegrity;
        Lines = lines ?? throw new ArgumentNullException(nameof(lines));
    }

    public int RootSeed { get; }
    public ProductionBillId BillId { get; }
    public int CycleSequence { get; }
    public string RecipeId { get; }
    public float CombinedOutputMultiplier { get; }
    public long OutputFactorNumerator { get; }
    public long OutputFactorDenominator { get; }
    public ProductionProcessKind ProcessKind { get; }
    public float PassiveBatchIntegrity { get; }
    public IReadOnlyList<CanonicalProductionResolvedOutputLine> Lines { get; }
}

public readonly struct CanonicalProductionResolvedOutputLine
{
    internal CanonicalProductionResolvedOutputLine(
        int deterministicOrdinal,
        ProductionOutputDefinition definition,
        CounterfactualRandomKey inclusionKey,
        float inclusionRoll,
        bool included,
        CounterfactualRandomKey fractionalRoundingKey,
        decimal scaledQuantity,
        decimal fractionalThreshold,
        float fractionalRoundingRoll,
        bool fractionalRoundedUp,
        int quantityBeforeIntegrity,
        bool passiveIntegrityPenaltyApplied,
        int resolvedQuantity)
    {
        DeterministicOrdinal = deterministicOrdinal;
        OutputLineId = definition.OutputLineId;
        Role = definition.Role;
        ItemId = definition.ItemId;
        AuthoredQuantity = definition.Amount;
        InclusionProbability = definition.Probability;
        InclusionKey = inclusionKey;
        InclusionRoll = inclusionRoll;
        Included = included;
        FractionalRoundingKey = fractionalRoundingKey;
        ScaledQuantity = scaledQuantity;
        FractionalThreshold = fractionalThreshold;
        FractionalRoundingRoll = fractionalRoundingRoll;
        FractionalRoundedUp = fractionalRoundedUp;
        QuantityBeforeIntegrity = quantityBeforeIntegrity;
        PassiveIntegrityPenaltyApplied = passiveIntegrityPenaltyApplied;
        ResolvedQuantity = resolvedQuantity;
    }

    public int DeterministicOrdinal { get; }
    public string OutputLineId { get; }
    public ProductionOutputRole Role { get; }
    public string ItemId { get; }
    public int AuthoredQuantity { get; }
    public float InclusionProbability { get; }
    public CounterfactualRandomKey InclusionKey { get; }
    public float InclusionRoll { get; }
    public bool Included { get; }
    public CounterfactualRandomKey FractionalRoundingKey { get; }
    public decimal ScaledQuantity { get; }
    public decimal FractionalThreshold { get; }
    public float FractionalRoundingRoll { get; }
    public bool FractionalRoundedUp { get; }
    public int QuantityBeforeIntegrity { get; }
    public bool PassiveIntegrityPenaltyApplied { get; }
    public int ResolvedQuantity { get; }
    public bool IsPhysical => ProductionOutputRoleRules.IsPhysical(Role);
}

/// <summary>
/// Resolves authored production output lines from stable, key-addressed random
/// events. It does not publish physical items or mutate bill state.
/// </summary>
public sealed class CanonicalProductionOutputResolver
{
    private const string ScenarioId = "production-output-resolution";
    private const string InclusionRollKind = "inclusion";
    private const string FractionalRoundingRollKind = "fractional-rounding";

    private readonly IRandomStreamProvider randomStreamProvider;

    public CanonicalProductionOutputResolver(
        IRandomStreamProvider randomStreamProvider)
    {
        this.randomStreamProvider = randomStreamProvider
            ?? throw new ArgumentNullException(nameof(randomStreamProvider));
    }

    public CanonicalProductionOutputResolution Resolve(
        ProductionBillId billId,
        int cycleSequence,
        string recipeId,
        IEnumerable<ProductionOutputDefinition> outputDefinitions,
        float combinedOutputMultiplier,
        ProductionProcessKind processKind,
        float passiveBatchIntegrity) => Resolve(
        billId,
        cycleSequence,
        recipeId,
        outputDefinitions,
        ProductionOutputFactor.FromAuthoredMultiplier(combinedOutputMultiplier),
        processKind,
        passiveBatchIntegrity);

    public CanonicalProductionOutputResolution Resolve(
        ProductionBillId billId,
        int cycleSequence,
        string recipeId,
        IEnumerable<ProductionOutputDefinition> outputDefinitions,
        ProductionOutputFactor combinedOutputFactor,
        ProductionProcessKind processKind,
        float passiveBatchIntegrity)
        => Resolve(
            randomStreamProvider.RootSeed,
            billId,
            cycleSequence,
            recipeId,
            outputDefinitions,
            combinedOutputFactor,
            processKind,
            passiveBatchIntegrity);

    public CanonicalProductionOutputResolution Resolve(
        int rootSeed,
        ProductionBillId billId,
        int cycleSequence,
        string recipeId,
        IEnumerable<ProductionOutputDefinition> outputDefinitions,
        ProductionOutputFactor combinedOutputFactor,
        ProductionProcessKind processKind,
        float passiveBatchIntegrity)
    {
        if (rootSeed == 0)
            throw new InvalidOperationException("Production output root seed is zero.");
        if (!billId.IsValid)
            throw new ArgumentException("A valid production bill ID is required.", nameof(billId));
        if (cycleSequence <= 0)
            throw new ArgumentOutOfRangeException(nameof(cycleSequence));
        if (!IsCanonicalStableId(recipeId))
            throw new ArgumentException("A canonical recipe ID is required.", nameof(recipeId));
        if (!Enum.IsDefined(typeof(ProductionProcessKind), processKind))
            throw new ArgumentOutOfRangeException(nameof(processKind), processKind, null);
        if (combinedOutputFactor.Numerator <= 0
            || combinedOutputFactor.Denominator <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(combinedOutputFactor),
                "The combined output multiplier must be finite and positive.");
        }
        if (!IsFinite(passiveBatchIntegrity)
            || passiveBatchIntegrity < 0f
            || passiveBatchIntegrity > 100f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(passiveBatchIntegrity),
                "Passive batch integrity must be finite and within 0..100.");
        }
        if (outputDefinitions == null)
            throw new ArgumentNullException(nameof(outputDefinitions));

        ProductionOutputDefinition[] definitions = outputDefinitions.ToArray();
        if (definitions.Length == 0)
        {
            throw new ArgumentException(
                "At least one canonical production output line is required.",
                nameof(outputDefinitions));
        }

        var lineIds = new HashSet<string>(StringComparer.Ordinal);
        for (int index = 0; index < definitions.Length; index++)
        {
            ProductionOutputDefinition definition = definitions[index];
            if (definition == null || !definition.HasCanonicalAuthoredValue)
            {
                throw new InvalidOperationException(
                    $"Production output line at index {index} is not canonically authored.");
            }
            if (!lineIds.Add(definition.OutputLineId))
            {
                throw new InvalidOperationException(
                    $"Duplicate production output line ID '{definition.OutputLineId}'.");
            }
        }

        Array.Sort(
            definitions,
            (left, right) => StringComparer.Ordinal.Compare(
                left.OutputLineId,
                right.OutputLineId));

        var keySet = new CounterfactualRandomKeySet();
        var resolved = new CanonicalProductionResolvedOutputLine[definitions.Length];
        for (int index = 0; index < definitions.Length; index++)
        {
            ProductionOutputDefinition definition = definitions[index];
            string entityId = BuildEntityId(
                billId.Value,
                recipeId,
                definition.OutputLineId);
            CounterfactualRandomKey inclusionKey = new(
                rootSeed,
                ScenarioId,
                InclusionRollKind,
                entityId,
                cycleSequence,
                0);
            CounterfactualRandomKey fractionalKey = new(
                rootSeed,
                ScenarioId,
                FractionalRoundingRollKind,
                entityId,
                cycleSequence,
                0);
            float inclusionRoll = keySet.CreateUnique(inclusionKey).NextFloat();
            float fractionalRoll = keySet.CreateUnique(fractionalKey).NextFloat();
            bool included = definition.Probability >= 1f
                || definition.Probability > 0f
                && inclusionRoll < definition.Probability;

            decimal scaledQuantity = combinedOutputFactor.Scale(definition.Amount);
            decimal wholeQuantity = decimal.Floor(scaledQuantity);
            decimal fractionalThreshold = scaledQuantity - wholeQuantity;
            if (wholeQuantity > int.MaxValue)
            {
                throw new OverflowException(
                    $"Production output line '{definition.OutputLineId}' exceeds Int32 quantity.");
            }

            bool roundedUp = fractionalThreshold > 0m
                && (decimal)fractionalRoll < fractionalThreshold;
            int quantityBeforeIntegrity = included
                ? Math.Max(
                    1,
                    checked((int)wholeQuantity + (roundedUp ? 1 : 0)))
                : 0;
            bool integrityPenalty = included
                && processKind == ProductionProcessKind.PassiveBatch
                && passiveBatchIntegrity < 50f;
            int resolvedQuantity = integrityPenalty
                ? Math.Max(1, quantityBeforeIntegrity / 2)
                : quantityBeforeIntegrity;

            resolved[index] = new CanonicalProductionResolvedOutputLine(
                index,
                definition,
                inclusionKey,
                inclusionRoll,
                included,
                fractionalKey,
                scaledQuantity,
                fractionalThreshold,
                fractionalRoll,
                roundedUp,
                quantityBeforeIntegrity,
                integrityPenalty,
                resolvedQuantity);
        }

        return new CanonicalProductionOutputResolution(
            rootSeed,
            billId,
            cycleSequence,
            recipeId,
            combinedOutputFactor,
            processKind,
            passiveBatchIntegrity,
            Array.AsReadOnly(resolved));
    }

    private static string BuildEntityId(
        string billId,
        string recipeId,
        string outputLineId) => string.Concat(
        "production-output:",
        billId.Length,
        ":",
        billId,
        ":",
        recipeId.Length,
        ":",
        recipeId,
        ":",
        outputLineId.Length,
        ":",
        outputLineId);

    private static bool IsCanonicalStableId(string value)
    {
        if (string.IsNullOrEmpty(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return false;
        }

        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            bool allowed = character >= 'a' && character <= 'z'
                || character >= '0' && character <= '9'
                || character == ':' || character == '/'
                || character == '.' || character == '_'
                || character == '-';
            if (!allowed)
                return false;
        }

        return true;
    }

    private static bool IsFinite(float value) =>
        !float.IsNaN(value) && !float.IsInfinity(value);

    private static bool IsFinitePositive(float value) =>
        IsFinite(value) && value > 0f;
}
