using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Positive migration scope for the first production-output vertical slice.
/// Recipes outside this exact set remain on the legacy output executor.
/// </summary>
public static class ProductionPreparedOutputMigrationScope
{
    public const string ProfileDigestSchemaToken =
        "production-prepared-output-migration-profile@1";
    public const string RegistryDigestSchemaToken =
        "production-prepared-output-migration-registry@1";

    private static readonly ExactRecipeProfile[] Profiles =
    {
        Profile(
            "recipe:charcoal",
            ProductionProcessKind.WorkOnly,
            "charcoal-kiln",
            "waste:mixed-rot",
            Line(
                "output:recipe:charcoal/000/main/material:charcoal",
                ProductionOutputRole.Main,
                "material:charcoal",
                2,
                1f)),
        Profile(
            "recipe:dog-food",
            ProductionProcessKind.WorkOnly,
            "feedbench",
            "waste:mixed-rot",
            Line("output:main", ProductionOutputRole.Main, "feed:dog-food", 2, 1f)),
        Profile(
            "recipe:dog-food-fresh",
            ProductionProcessKind.WorkOnly,
            "feedbench",
            "waste:mixed-rot",
            Line("output:main", ProductionOutputRole.Main, "feed:dog-food", 2, 1f)),
        Profile(
            "recipe:hay-feed",
            ProductionProcessKind.WorkOnly,
            "feedbench",
            "waste:mixed-rot",
            Line("output:main", ProductionOutputRole.Main, "feed:hay", 3, 1f)),
        Profile(
            "recipe:milling-flour",
            ProductionProcessKind.WorkOnly,
            "mill",
            "waste:mixed-rot",
            Line(
                "output:recipe:milling-flour/000/main/material:flour",
                ProductionOutputRole.Main,
                "material:flour",
                2,
                1f)),
        Profile(
            "recipe:malt",
            ProductionProcessKind.WorkOnly,
            "mill",
            "waste:mixed-rot",
            Line(
                "output:recipe:malt/000/main/material:malt",
                ProductionOutputRole.Main,
                "material:malt",
                2,
                1f)),
        Profile(
            "recipe:sawmill-lumber",
            ProductionProcessKind.WorkOnly,
            "sawmill",
            "waste:mixed-rot",
            Line(
                "output:recipe:sawmill-lumber/000/main/material:lumber",
                ProductionOutputRole.Main,
                "material:lumber",
                3,
                1f)),
        Profile(
            "recipe:silage",
            ProductionProcessKind.PassiveBatch,
            "feedbench",
            "waste:plant-rot",
            Line("output:main", ProductionOutputRole.Main, "feed:silage", 3, 1f)),
        Profile(
            "recipe:starch",
            ProductionProcessKind.WorkOnly,
            "mill",
            "waste:mixed-rot",
            Line(
                "output:recipe:starch/000/main/material:starch",
                ProductionOutputRole.Main,
                "material:starch",
                2,
                1f)),
        Profile(
            "recipe:steel-ingot",
            ProductionProcessKind.WorkOnly,
            "steelworks",
            "waste:mixed-rot",
            Line(
                "output:recipe:steel-ingot/000/main/material:steel-ingot",
                ProductionOutputRole.Main,
                "material:steel-ingot",
                1,
                1f)),
        Profile(
            "recipe:treated-lumber",
            ProductionProcessKind.WorkOnly,
            "workstation:v3:treated-lumber",
            "waste:mixed-rot",
            Line(
                "output:recipe:treated-lumber/000/main/material:treated-lumber",
                ProductionOutputRole.Main,
                "material:treated-lumber",
                2,
                1f))
    };

    public static bool Contains(string recipeId) => Find(recipeId) != null;

    public static string CaptureProfileDigest(string recipeId)
    {
        ExactRecipeProfile profile = Find(recipeId);
        if (profile == null)
        {
            throw new InvalidOperationException(
                $"Recipe '{recipeId}' is outside the prepared-output migration profile registry.");
        }
        CanonicalSemanticDigestBuilder canonical = new();
        canonical.Append(ProfileDigestSchemaToken);
        profile.AppendTo(canonical);
        return canonical.ComputeSha256();
    }

    public static string CaptureRegistryDigest()
    {
        CanonicalSemanticDigestBuilder canonical = new();
        canonical.Append(RegistryDigestSchemaToken);
        ExactRecipeProfile[] ordered = Profiles
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        canonical.Append(ordered.Length);
        foreach (ExactRecipeProfile profile in ordered)
            profile.AppendTo(canonical);
        return canonical.ComputeSha256();
    }

    public static void ValidateSavedProfileDigest(
        ProductionPreparedOutputBatchSaveData batch,
        string context)
    {
        if (batch == null)
            throw new ArgumentNullException(nameof(batch));
        if (batch.phase == ProductionPreparedOutputPhase.Unresolved)
            return;
        string current = CaptureProfileDigest(batch.recipeId);
        if (!string.Equals(
                batch.migrationProfileDigest,
                current,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                (context ?? string.Empty)
                + ":prepared-output-migration-profile-stale");
        }
    }

    public static bool MatchesExactProfile(ProductionRecipeSO recipe)
    {
        if (recipe == null)
            return false;
        ExactRecipeProfile profile = Find(recipe.RecipeId);
        return profile != null && profile.Matches(recipe);
    }

    public static void ValidateExactProfileOrThrow(ProductionRecipeSO recipe)
    {
        if (recipe == null)
            throw new ArgumentNullException(nameof(recipe));
        ExactRecipeProfile profile = Find(recipe.RecipeId);
        if (profile == null)
        {
            throw new InvalidOperationException(
                $"Recipe '{recipe.RecipeId}' is outside the prepared-output migration profile registry.");
        }
        if (!profile.Matches(recipe))
        {
            throw new InvalidOperationException(
                $"Recipe '{recipe.RecipeId}' drifted from its exact prepared-output migration profile.");
        }
    }

    public static bool HasLegacyOutputAuthority(
        ProductionBillRecord record) => record == null
        || record.outputOutcomeResolved
        || record.resolvedOutputs.Count != 0
        || record.outputReservations.Count != 0;

    public static bool HasLegacyOutputAuthority(
        ProductionBillSaveData saved) => saved == null
        || saved.outputOutcomeResolved
        || (saved.resolvedOutputs?.Count ?? 0) != 0
        || (saved.outputReservations?.Count ?? 0) != 0;

    public static DomainFailure CreateLegacyAuthorityConflict(
        ProductionBillRecord record) => new(
        FailureCode.ProductionOutputUnavailable,
        record == null ? string.Empty : record.billId.Value,
        "prepared-output-legacy-authority-conflict");

    public static bool RequiresCycleStartCapacity(
        ProductionBillRecord record) => record != null
        && !record.materialsConsumed
        && (record.preparedOutput?.phase
            ?? ProductionPreparedOutputPhase.Unresolved)
            == ProductionPreparedOutputPhase.Unresolved;

    public static bool RequiresAdditionalOutputCapacity(
        ProductionBillRecord record)
    {
        if (record == null)
            return false;
        ProductionPreparedOutputPhase phase = record.preparedOutput?.phase
            ?? ProductionPreparedOutputPhase.Unresolved;
        return phase ==
                ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace
            || phase == ProductionPreparedOutputPhase.Unresolved
                && !record.materialsConsumed;
    }

    private static ExactRecipeProfile Find(string recipeId)
    {
        if (string.IsNullOrEmpty(recipeId))
            return null;
        for (int index = 0; index < Profiles.Length; index++)
        {
            if (string.Equals(
                    Profiles[index].RecipeId,
                    recipeId,
                    StringComparison.Ordinal))
            {
                return Profiles[index];
            }
        }
        return null;
    }

    private static ExactRecipeProfile Profile(
        string recipeId,
        ProductionProcessKind processKind,
        string facilityTag,
        string spoilageItemId,
        params ExactOutputLine[] lines) => new(
        recipeId,
        processKind,
        facilityTag,
        spoilageItemId,
        lines);

    private static ExactOutputLine Line(
        string outputLineId,
        ProductionOutputRole role,
        string itemId,
        int amount,
        float probability) => new(
        outputLineId,
        role,
        itemId,
        amount,
        probability);

    private sealed class ExactRecipeProfile
    {
        private readonly ProductionProcessKind processKind;
        private readonly string facilityTag;
        private readonly string spoilageItemId;
        private readonly ExactOutputLine[] lines;

        public ExactRecipeProfile(
            string recipeId,
            ProductionProcessKind processKind,
            string facilityTag,
            string spoilageItemId,
            IReadOnlyList<ExactOutputLine> lines)
        {
            RecipeId = recipeId;
            this.processKind = processKind;
            this.facilityTag = facilityTag;
            this.spoilageItemId = spoilageItemId;
            this.lines = (lines ?? throw new ArgumentNullException(nameof(lines)))
                .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
                .ToArray();
            if (string.IsNullOrEmpty(RecipeId)
                || string.IsNullOrEmpty(this.facilityTag)
                || string.IsNullOrEmpty(this.spoilageItemId)
                || this.lines.Length == 0)
            {
                throw new InvalidOperationException(
                    "Prepared-output exact profile is incomplete.");
            }
        }

        public string RecipeId { get; }

        public void AppendTo(CanonicalSemanticDigestBuilder canonical)
        {
            if (canonical == null)
                throw new ArgumentNullException(nameof(canonical));
            canonical.Append(RecipeId);
            canonical.AppendEnum(processKind);
            canonical.Append(facilityTag);
            canonical.Append(spoilageItemId);
            canonical.Append(lines.Length);
            foreach (ExactOutputLine line in lines)
                line.AppendTo(canonical);
        }

        public bool Matches(ProductionRecipeSO recipe)
        {
            if (recipe == null
                || recipe.ProcessKind != processKind
                || !string.Equals(recipe.FacilityTag, facilityTag, StringComparison.Ordinal)
                || !string.Equals(
                    recipe.SpoilageItemId,
                    spoilageItemId,
                    StringComparison.Ordinal))
            {
                return false;
            }

            ProductionOutputDefinition[] authored = recipe
                .CaptureCanonicalOutputs()
                .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
                .ToArray();
            if (authored.Length != lines.Length)
                return false;
            for (int index = 0; index < authored.Length; index++)
            {
                if (!lines[index].Matches(authored[index]))
                    return false;
            }
            return true;
        }
    }

    private readonly struct ExactOutputLine
    {
        public ExactOutputLine(
            string outputLineId,
            ProductionOutputRole role,
            string itemId,
            int amount,
            float probability)
        {
            OutputLineId = outputLineId;
            Role = role;
            ItemId = itemId;
            Amount = amount;
            ProbabilityBits = BitConverter.SingleToInt32Bits(probability);
        }

        public string OutputLineId { get; }
        private ProductionOutputRole Role { get; }
        private string ItemId { get; }
        private int Amount { get; }
        private int ProbabilityBits { get; }

        public void AppendTo(CanonicalSemanticDigestBuilder canonical)
        {
            if (canonical == null)
                throw new ArgumentNullException(nameof(canonical));
            canonical.Append(OutputLineId);
            canonical.AppendEnum(Role);
            canonical.Append(ItemId);
            canonical.Append(Amount);
            canonical.Append(ProbabilityBits);
        }

        public bool Matches(ProductionOutputDefinition output) => output != null
            && string.Equals(
                output.OutputLineId,
                OutputLineId,
                StringComparison.Ordinal)
            && output.Role == Role
            && string.Equals(output.ItemId, ItemId, StringComparison.Ordinal)
            && output.Amount == Amount
            && BitConverter.SingleToInt32Bits(output.Probability)
                == ProbabilityBits;
    }
}

public readonly struct ProductionPreparedOutputCapacityResult
{
    private ProductionPreparedOutputCapacityResult(
        bool isValid,
        bool canBeginCycle,
        long maximumMassGrams,
        long occupiedMassGrams,
        long reservedMassGrams,
        DomainFailure failure)
    {
        IsValid = isValid;
        CanBeginCycle = canBeginCycle;
        MaximumMassGrams = maximumMassGrams;
        OccupiedMassGrams = occupiedMassGrams;
        ReservedMassGrams = reservedMassGrams;
        Failure = failure;
    }

    public bool IsValid { get; }
    public bool CanBeginCycle { get; }
    public long MaximumMassGrams { get; }
    public long OccupiedMassGrams { get; }
    public long ReservedMassGrams { get; }
    public DomainFailure Failure { get; }

    public static ProductionPreparedOutputCapacityResult Available(
        long maximumMassGrams,
        long occupiedMassGrams,
        long reservedMassGrams)
    {
        ValidateMasses(
            maximumMassGrams,
            occupiedMassGrams,
            reservedMassGrams);
        if (checked(occupiedMassGrams + reservedMassGrams)
            > maximumMassGrams)
        {
            throw new ArgumentException(
                "An available prepared-output capacity result cannot be over capacity.");
        }
        return new ProductionPreparedOutputCapacityResult(
            true,
            true,
            maximumMassGrams,
            occupiedMassGrams,
            reservedMassGrams,
            DomainFailure.None);
    }

    public static ProductionPreparedOutputCapacityResult Blocked(
        long maximumMassGrams,
        long occupiedMassGrams,
        long reservedMassGrams,
        DomainFailure failure)
    {
        ValidateMasses(
            maximumMassGrams,
            occupiedMassGrams,
            reservedMassGrams);
        if (!failure.IsFailure)
        {
            throw new ArgumentException(
                "A blocked prepared-output capacity result requires a failure.",
                nameof(failure));
        }
        return new ProductionPreparedOutputCapacityResult(
            true,
            false,
            maximumMassGrams,
            occupiedMassGrams,
            reservedMassGrams,
            failure);
    }

    public static ProductionPreparedOutputCapacityResult Unavailable(
        DomainFailure failure)
    {
        if (!failure.IsFailure)
        {
            throw new ArgumentException(
                "An unavailable prepared-output capacity result requires a failure.",
                nameof(failure));
        }
        return new ProductionPreparedOutputCapacityResult(
            true,
            false,
            0L,
            0L,
            0L,
            failure);
    }

    private static void ValidateMasses(
        long maximumMassGrams,
        long occupiedMassGrams,
        long reservedMassGrams)
    {
        if (maximumMassGrams <= 0L
            || occupiedMassGrams < 0L
            || reservedMassGrams < 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumMassGrams),
                "Prepared-output capacity masses are invalid.");
        }
    }
}

public readonly struct ProductionPreparedOutputExecutionResult
{
    private ProductionPreparedOutputExecutionResult(
        bool isValid,
        bool cycleOutputCompleted,
        ProductionPreparedOutputPhase phase,
        DomainFailure failure)
    {
        IsValid = isValid;
        CycleOutputCompleted = cycleOutputCompleted;
        Phase = phase;
        Failure = failure;
    }

    public bool IsValid { get; }
    public bool CycleOutputCompleted { get; }
    public ProductionPreparedOutputPhase Phase { get; }
    public DomainFailure Failure { get; }

    public static ProductionPreparedOutputExecutionResult Completed() => new(
        true,
        true,
        ProductionPreparedOutputPhase.Completed,
        DomainFailure.None);

    public static ProductionPreparedOutputExecutionResult Blocked(
        ProductionPreparedOutputPhase phase,
        DomainFailure failure)
    {
        if (!Enum.IsDefined(typeof(ProductionPreparedOutputPhase), phase)
            || phase == ProductionPreparedOutputPhase.Completed)
        {
            throw new ArgumentOutOfRangeException(nameof(phase));
        }
        if (!failure.IsFailure)
        {
            throw new ArgumentException(
                "A blocked prepared-output execution result requires a failure.",
                nameof(failure));
        }
        return new ProductionPreparedOutputExecutionResult(
            true,
            false,
            phase,
            failure);
    }
}

public readonly struct ProductionPreparedOutputReleaseResult
{
    private ProductionPreparedOutputReleaseResult(
        bool isValid,
        bool released,
        bool physicalBatchCommitted,
        DomainFailure failure)
    {
        IsValid = isValid;
        Released = released;
        PhysicalBatchCommitted = physicalBatchCommitted;
        Failure = failure;
    }

    public bool IsValid { get; }
    public bool Released { get; }
    public bool PhysicalBatchCommitted { get; }
    public DomainFailure Failure { get; }

    public static ProductionPreparedOutputReleaseResult ReleasedUnpublished() =>
        new(true, true, false, DomainFailure.None);

    public static ProductionPreparedOutputReleaseResult Blocked(
        bool physicalBatchCommitted,
        DomainFailure failure)
    {
        if (!failure.IsFailure)
        {
            throw new ArgumentException(
                "A blocked prepared-output release result requires a failure.",
                nameof(failure));
        }
        return new ProductionPreparedOutputReleaseResult(
            true,
            false,
            physicalBatchCommitted,
            failure);
    }
}

/// <summary>
/// Economy-owned boundary implemented by the composition-root assembly. The
/// implementation owns all prepared-output phase transitions; the bill runtime
/// only admits a cycle reset after a validated Completed result. Capacity
/// assessments are read-only. Release is idempotent for Unresolved/unpublished state and may
/// clear unpublished authority, but must return Blocked once a physical batch
/// exists; deleting physical output is never a release fallback.
/// </summary>
public interface IProductionPreparedOutputExecutionPort
{
    void RestoreDestinationAuthorities(
        System.Collections.Generic.IReadOnlyList<ProductionBillRecord> records,
        System.Collections.Generic.IReadOnlyList<ProductionFacilityHandle> facilities);

    ProductionPreparedOutputCapacityResult AssessCycleStart(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility);

    ProductionPreparedOutputCapacityResult AssessCurrentCapacity(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility);

    ProductionPreparedOutputExecutionResult Execute(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        ProductionWorkerHandle worker);

    ProductionPreparedOutputReleaseResult Release(
        ProductionBillRecord record,
        ProductionWipTerminalReason reason);
}
