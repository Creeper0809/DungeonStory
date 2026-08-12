using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class EmbeddedWorkValueRecipeBreakdown
{
    public EmbeddedWorkValueRecipeBreakdown(
        ProductionRecipeSO recipe,
        float inputWork,
        float directWork,
        float logisticsWork,
        float infrastructureWork,
        float expectedLossWork,
        float expectedOutputUnits,
        float outputUnitWork)
    {
        Recipe = recipe ?? throw new ArgumentNullException(nameof(recipe));
        InputWork = Mathf.Max(0f, inputWork);
        DirectWork = Mathf.Max(0f, directWork);
        LogisticsWork = Mathf.Max(0f, logisticsWork);
        InfrastructureWork = Mathf.Max(0f, infrastructureWork);
        ExpectedLossWork = Mathf.Max(0f, expectedLossWork);
        ExpectedOutputUnits = Mathf.Max(0.0001f, expectedOutputUnits);
        OutputUnitWork = Mathf.Max(0f, outputUnitWork);
    }

    public ProductionRecipeSO Recipe { get; }
    public float InputWork { get; }
    public float DirectWork { get; }
    public float LogisticsWork { get; }
    public float InfrastructureWork { get; }
    public float ExpectedLossWork { get; }
    public float ExpectedOutputUnits { get; }
    public float OutputUnitWork { get; }
    public float TotalWork => InputWork
        + DirectWork
        + LogisticsWork
        + InfrastructureWork
        + ExpectedLossWork;
}

public sealed class EmbeddedWorkValueSnapshot
{
    public EmbeddedWorkValueSnapshot(
        IReadOnlyDictionary<string, float> itemWork,
        IReadOnlyDictionary<string, EmbeddedWorkValueRecipeBreakdown> recipes,
        IReadOnlyCollection<string> externalSeedItemIds,
        IReadOnlyCollection<string> unresolvedItemIds,
        IReadOnlyCollection<string> nonConvergentRecipeIds)
    {
        ItemWork = itemWork
            ?? throw new ArgumentNullException(nameof(itemWork));
        Recipes = recipes
            ?? throw new ArgumentNullException(nameof(recipes));
        ExternalSeedItemIds = externalSeedItemIds
            ?? Array.Empty<string>();
        UnresolvedItemIds = unresolvedItemIds
            ?? Array.Empty<string>();
        NonConvergentRecipeIds = nonConvergentRecipeIds
            ?? Array.Empty<string>();
    }

    public IReadOnlyDictionary<string, float> ItemWork { get; }
    public IReadOnlyDictionary<string, EmbeddedWorkValueRecipeBreakdown> Recipes { get; }
    public IReadOnlyCollection<string> ExternalSeedItemIds { get; }
    public IReadOnlyCollection<string> UnresolvedItemIds { get; }
    public IReadOnlyCollection<string> NonConvergentRecipeIds { get; }

    public bool TryGetItemWork(string itemId, out float work) =>
        ItemWork.TryGetValue(itemId?.Trim() ?? string.Empty, out work);
}

/// <summary>
/// Computes the development-only embedded work value (EWU) from authored
/// physical recipes. Market prices are deliberately excluded from the cost
/// propagation so gold cannot become the production-cost authority.
/// </summary>
public sealed class V23EmbeddedWorkValueCalculator
{
    private const float UpdateEpsilon = 0.001f;
    private readonly ProductionRecipeSO[] recipes;
    private readonly CombatEquipmentDefinitionSO[] equipment;
    private readonly Dictionary<string, ItemDefinitionSO> items;
    private readonly Dictionary<string, CraftMaterialDefinitionSO> materials;
    private readonly IBalanceWorkCalculator workCalculator;

    public V23EmbeddedWorkValueCalculator(
        IEnumerable<ProductionRecipeSO> recipes,
        IEnumerable<ItemDefinitionSO> items,
        IEnumerable<CombatEquipmentDefinitionSO> equipment,
        IEnumerable<CraftMaterialDefinitionSO> materials,
        IBalanceWorkCalculator workCalculator)
    {
        this.recipes = (recipes ?? throw new ArgumentNullException(nameof(recipes)))
            .Where(value => value != null)
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        this.items = (items ?? throw new ArgumentNullException(nameof(items)))
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.ItemId))
            .GroupBy(value => value.ItemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.First(),
                StringComparer.Ordinal);
        this.equipment = (equipment ?? throw new ArgumentNullException(nameof(equipment)))
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.ItemId))
            .OrderBy(value => value.EquipmentId, StringComparer.Ordinal)
            .ToArray();
        this.materials = (materials ?? throw new ArgumentNullException(nameof(materials)))
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.MaterialId))
            .GroupBy(value => value.MaterialId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        this.workCalculator = workCalculator
            ?? throw new ArgumentNullException(nameof(workCalculator));
    }

    public EmbeddedWorkValueSnapshot Calculate()
    {
        Dictionary<string, float> itemWork = new(StringComparer.Ordinal);
        Dictionary<string, EmbeddedWorkValueRecipeBreakdown> breakdowns =
            new(StringComparer.Ordinal);
        HashSet<string> producedItemIds = recipes
            .SelectMany(recipe => recipe.Outputs)
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.ItemId))
            .Select(value => value.ItemId)
            .ToHashSet(StringComparer.Ordinal);
        string[] externalSeedItemIds = recipes
            .SelectMany(recipe => recipe.Inputs)
            .Where(value => value != null && !string.IsNullOrWhiteSpace(value.ItemId))
            .Select(value => value.ItemId)
            .Where(value => !producedItemIds.Contains(value) && items.ContainsKey(value))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        foreach (string itemId in externalSeedItemIds)
            itemWork[itemId] = CalculateExternalAcquisitionWork(items[itemId]);

        int maximumPasses = Mathf.Max(16, items.Count * 4 + recipes.Length);
        bool updated = false;
        HashSet<string> updatedOnLastPass = new(StringComparer.Ordinal);
        for (int pass = 0; pass < maximumPasses; pass++)
        {
            updated = false;
            updatedOnLastPass.Clear();
            foreach (ProductionRecipeSO recipe in recipes)
            {
                if (recipe.FlowRole == ProductionFlowRole.Sink
                    || recipe.Outputs.Count == 0)
                {
                    continue;
                }

                if (!TryCalculateRecipe(recipe, itemWork, out EmbeddedWorkValueRecipeBreakdown value))
                {
                    continue;
                }

                breakdowns[recipe.RecipeId] = value;
                foreach (ProductionOutputDefinition output in recipe.Outputs)
                {
                    string itemId = output?.ItemId ?? string.Empty;
                    if (itemId.Length == 0)
                    {
                        continue;
                    }

                    if (!itemWork.TryGetValue(itemId, out float current)
                        || value.OutputUnitWork + UpdateEpsilon < current)
                    {
                        itemWork[itemId] = value.OutputUnitWork;
                        updated = true;
                        updatedOnLastPass.Add(recipe.RecipeId);
                    }
                }
            }

            if (!updated)
            {
                break;
            }
        }

        HashSet<string> referencedItems = recipes
            .SelectMany(recipe => recipe.Inputs.Select(value => value.ItemId)
                .Concat(recipe.Outputs.Select(value => value.ItemId)))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
        string[] unresolved = referencedItems
            .Where(value => !itemWork.ContainsKey(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        AddEquipmentWork(itemWork);
        unresolved = unresolved
            .Concat(equipment.Select(value => value.ItemId)
                .Where(value => !itemWork.ContainsKey(value)))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] nonConvergent = updated
            ? updatedOnLastPass.OrderBy(value => value, StringComparer.Ordinal).ToArray()
            : Array.Empty<string>();
        return new EmbeddedWorkValueSnapshot(
            itemWork,
            breakdowns,
            externalSeedItemIds,
            unresolved,
            nonConvergent);
    }

    private void AddEquipmentWork(IDictionary<string, float> itemWork)
    {
        foreach (CombatEquipmentDefinitionSO definition in equipment)
        {
            if (!materials.TryGetValue(
                    definition.DefaultMaterialId,
                    out CraftMaterialDefinitionSO material)
                || !itemWork.TryGetValue(material.ItemId, out float materialWork))
            {
                continue;
            }

            float inputWork = materialWork * definition.PrimaryMaterialAmount;
            bool resolved = true;
            foreach (ItemAmountDefinition component in definition.RequiredComponentInputs)
            {
                if (component == null
                    || !itemWork.TryGetValue(component.ItemId, out float componentWork))
                {
                    resolved = false;
                    break;
                }
                inputWork += componentWork * component.Amount;
            }
            if (!resolved)
                continue;

            float directWork = workCalculator.CalculateEquipment(
                definition,
                material.ItemId);
            float logisticsWork = 3f
                + (definition.RequiredComponentInputs.Count + 1) * 0.75f
                + Mathf.Sqrt(Mathf.Max(0f, definition.Weight)) * 0.60f;
            float infrastructureWork = directWork * (definition.Era switch
            {
                EquipmentEra.RuneAbyssal => 0.25f,
                EquipmentEra.MatureIndustrial => 0.22f,
                EquipmentEra.EarlyIndustrial => 0.18f,
                _ => 0.16f
            });
            float total = (inputWork + directWork + logisticsWork + infrastructureWork)
                * 1.02f;
            itemWork[definition.ItemId] = Mathf.Max(1f, total);
        }
    }

    private bool TryCalculateRecipe(
        ProductionRecipeSO recipe,
        IReadOnlyDictionary<string, float> itemWork,
        out EmbeddedWorkValueRecipeBreakdown result)
    {
        float inputWork = 0f;
        foreach (ItemAmountDefinition input in recipe.Inputs)
        {
            if (input == null
                || !itemWork.TryGetValue(input.ItemId, out float unitWork))
            {
                result = null;
                return false;
            }
            inputWork += unitWork * input.Amount;
        }

        float expectedOutputUnits = recipe.Outputs
            .Where(value => value != null)
            .Sum(value => value.Amount * value.Probability);
        if (expectedOutputUnits <= 0f)
        {
            result = null;
            return false;
        }

        float directWork = workCalculator.CalculateRecipe(recipe);
        float logisticsWork = CalculateStandardLogisticsWork(recipe);
        float infrastructureWork = CalculateInfrastructureWork(recipe, directWork);
        float subtotal = inputWork + directWork + logisticsWork + infrastructureWork;
        float lossRate = recipe.FlowRole == ProductionFlowRole.Source
            ? 0.01f
            : recipe.ProcessKind == ProductionProcessKind.PassiveBatch
                ? 0.05f
                : 0.02f;
        float expectedLossWork = subtotal * lossRate;
        float outputUnitWork = (subtotal + expectedLossWork) / expectedOutputUnits;
        result = new EmbeddedWorkValueRecipeBreakdown(
            recipe,
            inputWork,
            directWork,
            logisticsWork,
            infrastructureWork,
            expectedLossWork,
            expectedOutputUnits,
            outputUnitWork);
        return true;
    }

    private float CalculateStandardLogisticsWork(ProductionRecipeSO recipe)
    {
        float totalWeight = recipe.Inputs.Sum(value =>
                value.Amount * ResolveWeight(value.ItemId))
            + recipe.Outputs.Sum(value =>
                value.Amount * value.Probability * ResolveWeight(value.ItemId));
        int inputKinds = recipe.Inputs.Count;
        int outputKinds = recipe.Outputs.Count;
        return 3f
            + inputKinds * 0.75f
            + outputKinds * 0.50f
            + Mathf.Sqrt(Mathf.Max(0f, totalWeight)) * 0.60f;
    }

    private static float CalculateInfrastructureWork(
        ProductionRecipeSO recipe,
        float directWork)
    {
        float rate = recipe.ProcessClass switch
        {
            ProductionProcessClass.Gathering => 0.05f,
            ProductionProcessClass.CuttingGrindingWashing => 0.08f,
            ProductionProcessClass.CookingSimpleMixing => 0.12f,
            ProductionProcessClass.SpinningWeavingWoodworking => 0.10f,
            ProductionProcessClass.ForgingHeavyAssembly => 0.16f,
            ProductionProcessClass.Chemical => 0.18f,
            ProductionProcessClass.Precision => 0.15f,
            ProductionProcessClass.Medical => 0.20f,
            ProductionProcessClass.Rune => 0.25f,
            ProductionProcessClass.HeavyIndustrial => 0.22f,
            _ => 0.10f
        };
        float passiveOccupation = recipe.ProcessKind == ProductionProcessKind.PassiveBatch
            ? recipe.ProcessingGameHours * 0.25f
            : 0f;
        float utilities = recipe.CleanWaterPerCycle * 0.5f
            + recipe.WastewaterPerCycle * 0.35f;
        return directWork * rate + passiveOccupation + utilities;
    }

    private float ResolveWeight(string itemId) =>
        items.TryGetValue(itemId?.Trim() ?? string.Empty, out ItemDefinitionSO item)
            ? item.UnitWeight
            : 1f;

    private static float CalculateExternalAcquisitionWork(ItemDefinitionSO item)
    {
        float baseWork = item is ResourceItemDefinitionSO resource
            ? resource.Kind switch
            {
                ResourceItemKind.Waste => 2f,
                ResourceItemKind.Raw => 8f,
                ResourceItemKind.AnimalProduct => 10f,
                ResourceItemKind.Food => 12f,
                ResourceItemKind.Substance => 14f,
                ResourceItemKind.Intermediate => 18f,
                ResourceItemKind.Ammunition => 24f,
                ResourceItemKind.FinishedGood => 24f,
                ResourceItemKind.Medicine => 30f,
                _ => 12f
            }
            : 16f;
        if (item is ResourceItemDefinitionSO tagged)
        {
            if ((tagged.IngredientTags & ResourceIngredientTag.Mineral) != 0)
                baseWork += 3f;
            if ((tagged.IngredientTags & ResourceIngredientTag.Arcane) != 0)
                baseWork += 6f;
            if ((tagged.IngredientTags & ResourceIngredientTag.Forbidden) != 0)
                baseWork += 6f;
        }
        float handling = Mathf.Sqrt(item.UnitWeight) * 2f
            + (item.MaxStack == 1 ? 4f : 0f);
        return Mathf.Max(1f, baseWork + handling);
    }
}
