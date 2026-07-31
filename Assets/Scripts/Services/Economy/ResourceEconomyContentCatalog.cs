using System;
using System.Collections.Generic;
using System.Linq;
using VContainer;

public sealed class ResourceEconomyContentCatalog : IResourceEconomyContentCatalog
{
    private readonly IReadOnlyList<ResourceItemDefinitionSO> items;
    private readonly IReadOnlyList<ProductionRecipeSO> recipes;
    private readonly IReadOnlyList<CropDefinitionSO> crops;
    private readonly IReadOnlyList<CraftMaterialDefinitionSO> materials;
    private readonly IReadOnlyList<SubstanceDefinitionSO> substances;
    private readonly IReadOnlyDictionary<string, ResourceItemDefinitionSO> itemsById;
    private readonly IReadOnlyDictionary<string, ProductionRecipeSO> recipesById;
    private readonly IReadOnlyDictionary<string, CropDefinitionSO> cropsById;
    private readonly IReadOnlyDictionary<string, CraftMaterialDefinitionSO> materialsById;
    private readonly IReadOnlyDictionary<string, SubstanceDefinitionSO> substancesById;

    [Inject]
    public ResourceEconomyContentCatalog(IResourcesAssetLoader loader)
        : this(
            loader?.LoadAllOptional<ResourceItemDefinitionSO>(ResourceItemDefinitionSO.ResourcePath),
            loader?.LoadAllOptional<ProductionRecipeSO>(ProductionRecipeSO.ResourcePath),
            loader?.LoadAllOptional<CropDefinitionSO>(CropDefinitionSO.ResourcePath),
            loader?.LoadAllOptional<CraftMaterialDefinitionSO>(CraftMaterialDefinitionSO.ResourcePath),
            loader?.LoadAllOptional<SubstanceDefinitionSO>(SubstanceDefinitionSO.ResourcePath))
    {
    }

    public ResourceEconomyContentCatalog(
        IEnumerable<ResourceItemDefinitionSO> itemDefinitions,
        IEnumerable<ProductionRecipeSO> recipeDefinitions,
        IEnumerable<CropDefinitionSO> cropDefinitions,
        IEnumerable<CraftMaterialDefinitionSO> materialDefinitions,
        IEnumerable<SubstanceDefinitionSO> substanceDefinitions)
    {
        items = Normalize(
            itemDefinitions,
            item => item.ItemId,
            "resource item");
        recipes = Normalize(
            recipeDefinitions,
            recipe => recipe.RecipeId,
            "production recipe");
        crops = Normalize(
            cropDefinitions,
            crop => crop.CropId,
            "crop");
        materials = Normalize(
            materialDefinitions,
            material => material.MaterialId,
            "craft material");
        substances = Normalize(
            substanceDefinitions,
            substance => substance.SubstanceId,
            "substance");

        itemsById = items.ToDictionary(item => item.ItemId, StringComparer.Ordinal);
        recipesById = recipes.ToDictionary(recipe => recipe.RecipeId, StringComparer.Ordinal);
        cropsById = crops.ToDictionary(crop => crop.CropId, StringComparer.Ordinal);
        materialsById = materials.ToDictionary(material => material.MaterialId, StringComparer.Ordinal);
        substancesById = substances.ToDictionary(
            substance => substance.SubstanceId,
            StringComparer.Ordinal);
    }

    public IReadOnlyList<ResourceItemDefinitionSO> Items => items;
    public IReadOnlyList<ProductionRecipeSO> Recipes => recipes;
    public IReadOnlyList<CropDefinitionSO> Crops => crops;
    public IReadOnlyList<CraftMaterialDefinitionSO> Materials => materials;
    public IReadOnlyList<SubstanceDefinitionSO> Substances => substances;

    public bool TryGetItem(string itemId, out ResourceItemDefinitionSO definition)
    {
        return itemsById.TryGetValue(itemId?.Trim() ?? string.Empty, out definition);
    }

    public bool TryGetRecipe(string recipeId, out ProductionRecipeSO definition)
    {
        return recipesById.TryGetValue(recipeId?.Trim() ?? string.Empty, out definition);
    }

    public bool TryGetCrop(string cropId, out CropDefinitionSO definition)
    {
        return cropsById.TryGetValue(cropId?.Trim() ?? string.Empty, out definition);
    }

    public bool TryGetMaterial(string materialId, out CraftMaterialDefinitionSO definition)
    {
        return materialsById.TryGetValue(materialId?.Trim() ?? string.Empty, out definition);
    }

    public bool TryGetSubstance(string substanceId, out SubstanceDefinitionSO definition)
    {
        return substancesById.TryGetValue(substanceId?.Trim() ?? string.Empty, out definition);
    }

    private static IReadOnlyList<T> Normalize<T>(
        IEnumerable<T> source,
        Func<T, string> getId,
        string label)
        where T : class
    {
        T[] normalized = (source ?? Array.Empty<T>())
            .Where(item => item != null)
            .OrderBy(getId, StringComparer.Ordinal)
            .ToArray();
        string invalid = normalized
            .Select(getId)
            .FirstOrDefault(string.IsNullOrWhiteSpace);
        if (invalid != null)
        {
            throw new InvalidOperationException($"A {label} has no stable ID.");
        }

        IGrouping<string, T> duplicate = normalized
            .GroupBy(getId, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicate != null)
        {
            throw new InvalidOperationException(
                $"Duplicate {label} ID '{duplicate.Key}'.");
        }

        return normalized;
    }
}

public sealed class ResourceUsageIndex : IResourceUsageIndex
{
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly Dictionary<string, StaticUsage> staticEntries =
        new Dictionary<string, StaticUsage>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> reservationCache =
        new Dictionary<string, int>(StringComparer.Ordinal);
    private int cachedItemVersion = -1;

    public ResourceUsageIndex(
        IResourceEconomyContentCatalog catalog,
        IWorldItemStackRuntime itemRuntime)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.itemRuntime = itemRuntime ?? throw new ArgumentNullException(nameof(itemRuntime));
        BuildStaticIndex();
    }

    public ResourceUsageEntry Get(string itemId)
    {
        string normalized = itemId?.Trim() ?? string.Empty;
        RefreshReservationCache();
        if (!staticEntries.TryGetValue(normalized, out StaticUsage usage))
        {
            return new ResourceUsageEntry { ItemId = normalized };
        }

        reservationCache.TryGetValue(normalized, out int reserved);
        return new ResourceUsageEntry
        {
            ItemId = normalized,
            ProducerIds = usage.Producers,
            ConsumerIds = usage.Consumers,
            RequiredResearchIds = usage.ResearchIds,
            ReservedQuantity = reserved
        };
    }

    public IReadOnlyList<string> ValidateContentGraph()
    {
        List<string> errors = new List<string>();
        HashSet<string> itemIds = catalog.Items
            .Select(item => item.ItemId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (ProductionRecipeSO recipe in catalog.Recipes)
        {
            if (recipe.Inputs.Count == 0 && recipe.Outputs.Count == 0)
            {
                errors.Add($"{recipe.RecipeId}: 입력과 출력이 모두 없습니다.");
            }

            foreach (ItemAmountDefinition input in recipe.Inputs)
            {
                if (input == null
                    || (!itemIds.Contains(input.ItemId)
                        && !DungeonItemCatalogSO.TryGetStockCategoryFromItemId(
                            input.ItemId,
                            out _)))
                {
                    errors.Add($"{recipe.RecipeId}: 알 수 없는 입력 아이템 '{input?.ItemId}'.");
                }
            }

            foreach (ProductionOutputDefinition output in recipe.Outputs)
            {
                if (output == null || !itemIds.Contains(output.ItemId))
                {
                    errors.Add($"{recipe.RecipeId}: 알 수 없는 출력 아이템 '{output?.ItemId}'.");
                }
            }
        }

        foreach (CropDefinitionSO crop in catalog.Crops)
        {
            if (!itemIds.Contains(crop.HarvestItemId))
            {
                errors.Add($"{crop.CropId}: 수확 아이템 '{crop.HarvestItemId}'이 없습니다.");
            }
        }

        foreach (ResourceItemDefinitionSO item in catalog.Items)
        {
            StaticUsage usage = staticEntries[item.ItemId];
            if (usage.Producers.Count == 0)
            {
                errors.Add($"{item.ItemId}: 생산처가 없습니다.");
            }

            int minimumConsumers = item.Kind is ResourceItemKind.Raw
                or ResourceItemKind.AnimalProduct
                    ? 2
                    : 1;
            if (usage.Consumers.Count < minimumConsumers)
            {
                errors.Add(
                    $"{item.ItemId}: 사용처가 {usage.Consumers.Count}개뿐입니다. "
                    + $"최소 {minimumConsumers}개가 필요합니다.");
            }
        }

        foreach (CraftMaterialDefinitionSO material in catalog.Materials)
        {
            if (!itemIds.Contains(material.ItemId))
            {
                errors.Add($"{material.MaterialId}: 재질 아이템 '{material.ItemId}'이 없습니다.");
            }
        }

        foreach (SubstanceDefinitionSO substance in catalog.Substances)
        {
            if (!itemIds.Contains(substance.ItemId))
            {
                errors.Add($"{substance.SubstanceId}: 약물 아이템 '{substance.ItemId}'이 없습니다.");
            }
        }

        return errors;
    }

    public void InvalidateReservations()
    {
        cachedItemVersion = -1;
    }

    private void BuildStaticIndex()
    {
        foreach (ResourceItemDefinitionSO item in catalog.Items)
        {
            staticEntries[item.ItemId] = new StaticUsage();
        }

        foreach (ProductionRecipeSO recipe in catalog.Recipes)
        {
            foreach (ItemAmountDefinition input in recipe.Inputs)
            {
                if (input != null && staticEntries.TryGetValue(input.ItemId, out StaticUsage usage))
                {
                    usage.AddConsumer(recipe.RecipeId, recipe.RequiredResearchId);
                }
            }

            foreach (ProductionOutputDefinition output in recipe.Outputs)
            {
                if (output != null && staticEntries.TryGetValue(output.ItemId, out StaticUsage usage))
                {
                    usage.AddProducer(recipe.RecipeId, recipe.RequiredResearchId);
                }
            }
        }

        foreach (CropDefinitionSO crop in catalog.Crops)
        {
            if (staticEntries.TryGetValue(crop.HarvestItemId, out StaticUsage usage))
            {
                usage.AddProducer($"crop:{crop.CropId}", crop.RequiredResearchId);
            }
        }

        foreach (CraftMaterialDefinitionSO material in catalog.Materials)
        {
            if (staticEntries.TryGetValue(material.ItemId, out StaticUsage usage))
            {
                usage.AddConsumer(
                    $"sink:equipment-material:{material.MaterialId}",
                    material.RequiredResearchId);
            }
        }

        foreach (ResourceItemDefinitionSO item in catalog.Items)
        {
            StaticUsage usage = staticEntries[item.ItemId];
            if (string.Equals(
                item.ItemId,
                "offense:unappraised-loot",
                StringComparison.Ordinal))
            {
                usage.AddProducer(
                    "source:expedition-loot",
                    string.Empty);
            }
            else if (string.Equals(
                item.ItemId,
                "resource:rune-dust",
                StringComparison.Ordinal))
            {
                usage.AddProducer(
                    "source:high-risk-wildlife",
                    "research:husbandry:capture");
            }

            foreach (string sink in GetBuiltInSinks(item))
            {
                usage.AddConsumer(sink, item.RequiredResearchId);
            }
        }
    }

    private static IEnumerable<string> GetBuiltInSinks(ResourceItemDefinitionSO item)
    {
        if (item.Kind == ResourceItemKind.Food)
        {
            yield return "sink:character-meal";
            yield return "sink:guest-meal";
        }
        if (item.Kind == ResourceItemKind.Medicine)
        {
            yield return "sink:medical-treatment";
        }
        if (item.Kind == ResourceItemKind.Substance)
        {
            yield return "sink:substance-policy";
        }
        if (item.Kind == ResourceItemKind.Ammunition)
        {
            yield return "sink:combat-ammunition";
        }
        if (item.Kind == ResourceItemKind.FinishedGood)
        {
            yield return "sink:trade-contract";
        }
        if ((item.IngredientTags & ResourceIngredientTag.Fuel) != 0)
        {
            yield return "sink:facility-fuel";
        }
        if ((item.IngredientTags & ResourceIngredientTag.Spoiled) != 0)
        {
            yield return "sink:waste-policy";
        }
    }

    private void RefreshReservationCache()
    {
        if (cachedItemVersion == itemRuntime.ItemStackVersion)
        {
            return;
        }

        reservationCache.Clear();
        foreach (WorldItemStackSnapshot stack in itemRuntime.GetAllStacks())
        {
            if (stack == null || !stack.IsReserved || stack.Quantity <= 0)
            {
                continue;
            }

            reservationCache.TryGetValue(stack.ItemId, out int current);
            reservationCache[stack.ItemId] = current + stack.Quantity;
        }
        cachedItemVersion = itemRuntime.ItemStackVersion;
    }

    private sealed class StaticUsage
    {
        private readonly HashSet<string> producers = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> consumers = new HashSet<string>(StringComparer.Ordinal);
        private readonly HashSet<string> researchIds = new HashSet<string>(StringComparer.Ordinal);

        public IReadOnlyList<string> Producers => producers.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        public IReadOnlyList<string> Consumers => consumers.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        public IReadOnlyList<string> ResearchIds => researchIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();

        public void AddProducer(string id, string researchId)
        {
            Add(producers, id, researchId);
        }

        public void AddConsumer(string id, string researchId)
        {
            Add(consumers, id, researchId);
        }

        private void Add(ISet<string> target, string id, string researchId)
        {
            if (!string.IsNullOrWhiteSpace(id))
            {
                target.Add(id.Trim());
            }
            if (!string.IsNullOrWhiteSpace(researchId))
            {
                researchIds.Add(researchId.Trim());
            }
        }
    }
}
