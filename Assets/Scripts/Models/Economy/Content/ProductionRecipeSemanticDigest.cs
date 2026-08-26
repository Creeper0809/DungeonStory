using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Canonical gameplay-semantic digest for one production recipe. Presentation,
/// asset path, Unity object identity and serialized field order are deliberately
/// excluded. Every field that can change execution, custody, output capacity or
/// WIP recovery is included explicitly.
/// </summary>
public static class ProductionRecipeSemanticDigest
{
    public const string SchemaToken = "production-recipe-semantic@2";

    public static string Capture(ProductionRecipeSO recipe)
    {
        if (recipe == null)
            throw new ArgumentNullException(nameof(recipe));

        RequireEnum(recipe.ProcessKind, nameof(recipe.ProcessKind));
        RequireEnum(recipe.FlowRole, nameof(recipe.FlowRole));
        RequireEnum(recipe.ProcessClass, nameof(recipe.ProcessClass));
        RequireEnum(recipe.WastewaterComposition,
            nameof(recipe.WastewaterComposition));
        RequireCanonicalRequired(recipe.RecipeId, nameof(recipe.RecipeId));
        RequireCanonicalRequired(recipe.FacilityTag, nameof(recipe.FacilityTag));
        RequireCanonicalRequired(recipe.WorkstationTag,
            nameof(recipe.WorkstationTag));
        RequireCanonicalRequired(recipe.WorkTypeId.Value,
            nameof(recipe.WorkTypeId));
        RequireCanonicalOptional(recipe.RequiredResearchId,
            nameof(recipe.RequiredResearchId));
        RequireCanonicalOptional(recipe.BatchSupportTag,
            nameof(recipe.BatchSupportTag));
        RequireCanonicalRequired(recipe.SpoilageItemId,
            nameof(recipe.SpoilageItemId));

        ProficiencyWorkProfileAuthoring proficiency = recipe.Proficiency
            ?? throw new InvalidOperationException(
                $"Production recipe '{recipe.RecipeId}' has no proficiency profile.");
        if (!proficiency.IsValid)
        {
            throw new InvalidOperationException(
                $"Production recipe '{recipe.RecipeId}' has an invalid proficiency profile.");
        }
        RequireEnum(proficiency.CombinationMode,
            nameof(proficiency.CombinationMode));
        RequireEnum(proficiency.RecommendedRank,
            nameof(proficiency.RecommendedRank));
        RequireEnum(proficiency.MinimumRiskRank,
            nameof(proficiency.MinimumRiskRank));
        RequireCanonicalRequired(proficiency.Primary.Value,
            "proficiency.primary");
        RequireCanonicalOptional(proficiency.Secondary.Value,
            "proficiency.secondary");

        string[] supportTags = CaptureCanonicalDistinctIds(
            recipe.RequiredSupportTags,
            "required support tag");
        ItemAmountDefinition[] inputs = CaptureInputs(recipe);
        ProductionOutputDefinition[] outputs = CaptureOutputs(recipe);

        CanonicalSemanticDigestBuilder canonical = new();
        canonical.Append(SchemaToken);
        canonical.Append(recipe.RecipeId);
        canonical.AppendEnum(recipe.ProcessKind);
        canonical.AppendEnum(recipe.FlowRole);
        canonical.AppendEnum(recipe.ProcessClass);
        canonical.Append(recipe.HasAuthoredProcessClass);
        canonical.Append(recipe.FacilityTag);
        canonical.Append(recipe.WorkstationTag);
        canonical.Append(recipe.WorkTypeId.Value);
        canonical.Append(recipe.RequiredResearchId);
        canonical.AppendFloat(recipe.RequiredWork);
        canonical.AppendFloat(recipe.PreparationWork);
        canonical.AppendFloat(recipe.FinishingWork);
        canonical.AppendFloat(recipe.ProcessingGameHours);
        canonical.AppendFloat(recipe.OptimalTemperatureMinimum);
        canonical.AppendFloat(recipe.OptimalTemperatureMaximum);
        canonical.AppendFloat(recipe.WarningTemperatureMinimum);
        canonical.AppendFloat(recipe.WarningTemperatureMaximum);
        canonical.AppendFloat(recipe.CleanWaterPerCycle);
        canonical.AppendFloat(recipe.WastewaterPerCycle);
        canonical.AppendEnum(recipe.WastewaterComposition);
        canonical.Append(recipe.AllowsManualWaterFallback);
        canonical.Append(recipe.BatchSupportTag);
        canonical.Append(recipe.SpoilageItemId);
        canonical.Append(proficiency.Primary.Value);
        canonical.Append(proficiency.Secondary.Value ?? string.Empty);
        canonical.AppendFloat(proficiency.PrimaryWeight);
        canonical.AppendEnum(proficiency.CombinationMode);
        canonical.AppendEnum(proficiency.RecommendedRank);
        canonical.AppendEnum(proficiency.MinimumRiskRank);

        canonical.Append(supportTags.Length);
        foreach (string supportTag in supportTags)
            canonical.Append(supportTag);

        canonical.Append(inputs.Length);
        foreach (ItemAmountDefinition input in inputs)
        {
            canonical.Append(input.ItemId);
            canonical.Append(input.Amount);
        }

        canonical.Append(outputs.Length);
        foreach (ProductionOutputDefinition output in outputs)
        {
            canonical.Append(output.OutputLineId);
            canonical.AppendEnum(output.Role);
            canonical.Append(output.ItemId);
            canonical.Append(output.Amount);
            canonical.AppendFloat(output.Probability);
        }

        return canonical.ComputeSha256();
    }

    private static ItemAmountDefinition[] CaptureInputs(
        ProductionRecipeSO recipe)
    {
        ItemAmountDefinition[] inputs = (recipe.Inputs
                ?? Array.Empty<ItemAmountDefinition>())
            .ToArray();
        if (inputs.Any(value => value == null
                || !value.HasCanonicalAuthoredValue))
        {
            throw new InvalidOperationException(
                $"Production recipe '{recipe.RecipeId}' has a noncanonical input.");
        }
        ItemAmountDefinition[] canonical = inputs
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        if (canonical.Select(value => value.ItemId)
            .Distinct(StringComparer.Ordinal).Count() != canonical.Length)
        {
            throw new InvalidOperationException(
                $"Production recipe '{recipe.RecipeId}' has duplicate input item IDs.");
        }
        return canonical;
    }

    private static ProductionOutputDefinition[] CaptureOutputs(
        ProductionRecipeSO recipe)
    {
        ProductionOutputDefinition[] outputs = recipe
            .CaptureCanonicalOutputs()
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        if (outputs.Length == 0)
        {
            throw new InvalidOperationException(
                $"Production recipe '{recipe.RecipeId}' has no canonical output.");
        }
        return outputs;
    }

    private static string[] CaptureCanonicalDistinctIds(
        IEnumerable<string> values,
        string role)
    {
        string[] source = (values ?? Array.Empty<string>()).ToArray();
        for (int index = 0; index < source.Length; index++)
            RequireCanonicalRequired(source[index], role);
        string[] canonical = source
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (canonical.Distinct(StringComparer.Ordinal).Count()
            != canonical.Length)
        {
            throw new InvalidOperationException(
                $"Production recipe has duplicate {role}s.");
        }
        return canonical;
    }

    private static void RequireCanonicalRequired(string value, string role)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Production recipe has a noncanonical {role}.");
        }
    }

    private static void RequireCanonicalOptional(string value, string role)
    {
        if (value == null
            || value.Length > 0
                && (string.IsNullOrWhiteSpace(value)
                    || !string.Equals(
                        value,
                        value.Trim(),
                        StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                $"Production recipe has a noncanonical {role}.");
        }
    }

    private static void RequireEnum<T>(T value, string role)
        where T : struct, Enum
    {
        if (!Enum.IsDefined(typeof(T), value))
        {
            throw new InvalidOperationException(
                $"Production recipe has an invalid {role}.");
        }
    }

}

/// <summary>
/// Restore/pre-publication guard that prevents a resolved prepared-output batch
/// from being interpreted under a different live recipe definition.
/// </summary>
public static class ProductionPreparedOutputSourceRevisionGuard
{
    public const string StaleFailureToken =
        "prepared-output-source-revision-stale";

    public static void ValidateResolvedBatch(
        ProductionPreparedOutputBatchSaveData batch,
        ProductionRecipeSO liveRecipe,
        string context)
    {
        if (batch == null)
            throw new ArgumentNullException(nameof(batch));
        if (liveRecipe == null)
            throw new ArgumentNullException(nameof(liveRecipe));
        if (batch.phase == ProductionPreparedOutputPhase.Unresolved)
            return;

        string current = ProductionRecipeSemanticDigest.Capture(liveRecipe);
        if (!string.Equals(
                batch.recipeDefinitionDigest,
                current,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                (context ?? string.Empty) + ":" + StaleFailureToken);
        }
    }
}
