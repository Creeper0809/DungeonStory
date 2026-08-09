using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using VContainer;

public sealed class ResourceEconomyContentCatalog : IResourceEconomyContentCatalog
{
    private readonly IReadOnlyList<ResourceItemDefinitionSO> items;
    private readonly IReadOnlyList<ProductionRecipeSO> recipes;
    private readonly IReadOnlyList<CropDefinitionSO> crops;
    private readonly IReadOnlyList<CraftMaterialDefinitionSO> materials;
    private readonly IReadOnlyList<SubstanceDefinitionView> substances;
    private readonly IReadOnlyDictionary<string, ResourceItemDefinitionSO> itemsById;
    private readonly IReadOnlyDictionary<string, ProductionRecipeSO> recipesById;
    private readonly IReadOnlyDictionary<string, CropDefinitionSO> cropsById;
    private readonly IReadOnlyDictionary<string, CraftMaterialDefinitionSO> materialsById;
    private readonly IReadOnlyDictionary<string, SubstanceDefinitionView> substancesById;

    [Inject]
    public ResourceEconomyContentCatalog(IGameContentCatalog content)
        : this(
            (content ?? throw new ArgumentNullException(nameof(content)))
                .Items.Definitions.OfType<ResourceItemDefinitionSO>(),
            content.GetAll<ProductionRecipeSO>(),
            content.GetAll<CropDefinitionSO>(),
            content.GetAll<CraftMaterialDefinitionSO>())
    {
    }

    public ResourceEconomyContentCatalog(
        IEnumerable<ResourceItemDefinitionSO> itemDefinitions,
        IEnumerable<ProductionRecipeSO> recipeDefinitions,
        IEnumerable<CropDefinitionSO> cropDefinitions,
        IEnumerable<CraftMaterialDefinitionSO> materialDefinitions)
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
            items
                .Where(item => item.TryGetFeature(out SubstanceItemFeature _))
                .Select(CreateSubstanceView),
            substance => substance.SubstanceId,
            "item substance feature");

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
    public IReadOnlyList<SubstanceDefinitionView> Substances => substances;

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

    public bool TryGetSubstance(string substanceId, out SubstanceDefinitionView definition)
    {
        return substancesById.TryGetValue(substanceId?.Trim() ?? string.Empty, out definition);
    }

    private static SubstanceDefinitionView CreateSubstanceView(
        ResourceItemDefinitionSO item)
    {
        if (item == null
            || !item.TryGetFeature(out SubstanceItemFeature feature))
        {
            throw new InvalidOperationException(
                "A substance projection requires an authored item substance feature.");
        }

        return new SubstanceDefinitionView(
            feature.substanceId,
            item.ItemId,
            item.DisplayName,
            feature.useClass,
            feature.addictionChance,
            feature.overdoseChance,
            feature.toleranceGain,
            feature.withdrawalPerHour,
            feature.moodEffect,
            feature.workSpeedEffect,
            feature.combatEffect,
            feature.durationSeconds,
            item.RequiredResearchId);
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

public sealed class ResourceUsageIndex :
    IResourceUsageIndex,
    IProductionDependencyCatalog
{
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly ICombatEquipmentCatalog equipmentCatalog;
    private readonly IGameContentCatalog content;
    private readonly Dictionary<string, StaticUsage> staticEntries =
        new Dictionary<string, StaticUsage>(StringComparer.Ordinal);
    private readonly Dictionary<string, int> reservationCache =
        new Dictionary<string, int>(StringComparer.Ordinal);
    private int cachedItemVersion = -1;

    public ResourceUsageIndex(
        IResourceEconomyContentCatalog catalog,
        IWorldItemStackRuntime itemRuntime,
        ICombatEquipmentCatalog equipmentCatalog,
        IGameContentCatalog content)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.itemRuntime = itemRuntime ?? throw new ArgumentNullException(nameof(itemRuntime));
        this.equipmentCatalog = equipmentCatalog
            ?? throw new ArgumentNullException(nameof(equipmentCatalog));
        this.content = content ?? throw new ArgumentNullException(nameof(content));
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
            ReservedQuantity = reserved,
            ConsumerLinks = usage.ConsumerLinks,
            DirectBranchCount = usage.ConsumerLinks.Count(link => link.IsRealConsumer),
            LongestProductionDepth = GetLongestProductionDepth(normalized)
        };
    }

    public ResourceUsageEntry GetDependency(string itemId) => Get(itemId);

    public IReadOnlyList<ProductionConsumerLink> GetConsumers(string itemId) =>
        Get(itemId).ConsumerLinks;

    public IReadOnlyList<string> ValidateProductionGraph() =>
        ValidateContentGraph();

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
                if (input != null
                    && input.ItemId.StartsWith("stock-item:", StringComparison.Ordinal))
                {
                    errors.Add(
                        $"{recipe.RecipeId}: abstract recipe input '{input.ItemId}' is forbidden.");
                    continue;
                }
                if (input == null
                    || !itemIds.Contains(input.ItemId))
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

            int minimumConsumers = item.Kind == ResourceItemKind.Intermediate
                ? 2
                : 1;
            int realConsumers = usage.ConsumerLinks.Count(link =>
                link.IsRealConsumer);
            if (realConsumers < minimumConsumers)
            {
                errors.Add(
                    $"{item.ItemId}: 사용처가 {usage.Consumers.Count}개뿐입니다. "
                    + $"최소 {minimumConsumers}개가 필요합니다.");
            }

            int depth = GetLongestProductionDepth(item.ItemId);
            if (depth > 4)
            {
                errors.Add(
                    $"{item.ItemId}: production depth {depth} exceeds four transformations.");
            }
        }

        foreach (CraftMaterialDefinitionSO material in catalog.Materials)
        {
            if (!itemIds.Contains(material.ItemId))
            {
                errors.Add($"{material.MaterialId}: 재질 아이템 '{material.ItemId}'이 없습니다.");
            }
        }

        foreach (SubstanceDefinitionView substance in catalog.Substances)
        {
            if (!itemIds.Contains(substance.ItemId))
            {
                errors.Add($"{substance.SubstanceId}: 약물 아이템 '{substance.ItemId}'이 없습니다.");
            }
        }

        foreach (BuildingSO building in content.GetAll<BuildingSO>())
        {
            if (building == null)
            {
                continue;
            }

            BuildingWorkAmountAbility workAmount =
                building.GetAbility<BuildingWorkAmountAbility>();
            if (workAmount == null)
            {
                errors.Add(
                    $"building:{building.id}: construction material authority is missing.");
                continue;
            }

            IReadOnlyList<ItemAmountDefinition> constructionMaterials =
                workAmount.ConstructionMaterials;
            if (constructionMaterials == null || constructionMaterials.Count == 0)
            {
                errors.Add(
                    $"building:{building.id}: at least one concrete construction material is required.");
                continue;
            }

            HashSet<string> materialIds = new(StringComparer.Ordinal);
            foreach (ItemAmountDefinition material in constructionMaterials)
            {
                string materialId = material?.ItemId?.Trim() ?? string.Empty;
                if (material == null
                    || material.Amount <= 0
                    || materialId.Length == 0
                    || materialId.StartsWith("stock-item:", StringComparison.Ordinal)
                    || !itemIds.Contains(materialId))
                {
                    errors.Add(
                        $"building:{building.id}: invalid concrete construction material '{materialId}'.");
                    continue;
                }

                if (!materialIds.Add(materialId))
                {
                    errors.Add(
                        $"building:{building.id}: duplicate construction material '{materialId}'.");
                }
            }
        }

        return errors;
    }

    public int GetLongestProductionDepth(string itemId)
    {
        string normalized = itemId?.Trim() ?? string.Empty;
        Dictionary<string, int> memo = new Dictionary<string, int>(
            StringComparer.Ordinal);
        return MeasureProductionDepth(
            normalized,
            memo,
            new HashSet<string>(StringComparer.Ordinal));
    }

    private int MeasureProductionDepth(
        string itemId,
        IDictionary<string, int> memo,
        ISet<string> visiting)
    {
        if (string.IsNullOrWhiteSpace(itemId))
        {
            return 0;
        }
        if (catalog.TryGetItem(itemId, out ResourceItemDefinitionSO item)
            && item.Kind == ResourceItemKind.Raw)
        {
            return 0;
        }
        if (memo.TryGetValue(itemId, out int cached))
        {
            return cached;
        }
        if (!visiting.Add(itemId))
        {
            return 5;
        }

        ProductionRecipeSO[] producers = catalog.Recipes
            .Where(recipe => recipe.Outputs.Any(output =>
                output != null
                && string.Equals(
                    output.ItemId,
                    itemId,
                    StringComparison.Ordinal)))
            .ToArray();
        if (producers.Length > 0
            && producers.All(producer => producer.RecipeId.StartsWith(
                "source:",
                StringComparison.Ordinal)))
        {
            visiting.Remove(itemId);
            memo[itemId] = 0;
            return 0;
        }
        int depth = 0;
        foreach (ProductionRecipeSO producer in producers)
        {
            int inputDepth = producer.Inputs.Count == 0
                ? 0
                : producer.Inputs.Max(input => input == null
                    ? 0
                    : MeasureProductionDepth(input.ItemId, memo, visiting));
            depth = Mathf.Max(depth, inputDepth + 1);
        }

        visiting.Remove(itemId);
        memo[itemId] = depth;
        return depth;
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
                    usage.AddConsumer(
                        recipe.RecipeId,
                        recipe.RequiredResearchId,
                        ProductionConsumerKind.RecipeInput,
                        recipe.DisplayName);
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
            if (staticEntries.TryGetValue(crop.SeedItemId, out StaticUsage seedUsage))
            {
                seedUsage.AddConsumer(
                    $"crop-sowing:{crop.CropId}",
                    crop.RequiredResearchId,
                    ProductionConsumerKind.CropSowing,
                    $"{crop.DisplayName} 파종");
            }
        }

        foreach (CombatEquipmentDefinitionSO equipment in
                 equipmentCatalog?.All
                 ?? Array.Empty<CombatEquipmentDefinitionSO>())
        {
            foreach (CraftMaterialDefinitionSO material in catalog.Materials
                         .Where(equipment.AllowsMaterial))
            {
                if (staticEntries.TryGetValue(
                        material.ItemId,
                        out StaticUsage materialUsage))
                {
                    materialUsage.AddConsumer(
                        $"equipment:{equipment.EquipmentId}",
                        equipment.RequiredResearchId,
                        ProductionConsumerKind.EquipmentMaterial,
                        equipment.DisplayName);
                }
            }

            foreach (ItemAmountDefinition component in
                     equipment.RequiredComponentInputs)
            {
                if (component != null
                    && staticEntries.TryGetValue(
                        component.ItemId,
                        out StaticUsage componentUsage))
                {
                    componentUsage.AddConsumer(
                        $"equipment:{equipment.EquipmentId}",
                        equipment.RequiredResearchId,
                        ProductionConsumerKind.EquipmentMaterial,
                        equipment.DisplayName);
                }
            }

            if (equipment is CombatWeaponSO weapon)
            {
                foreach (ItemDefinitionId ammunitionItemId in
                         weapon.CompatibleAmmunitionItemIds)
                {
                    if (!staticEntries.TryGetValue(
                            ammunitionItemId.Value,
                            out StaticUsage ammunitionUsage))
                    {
                        continue;
                    }

                    ammunitionUsage.AddConsumer(
                        $"equipment-ammunition:{weapon.EquipmentId}",
                        weapon.RequiredResearchId,
                        ProductionConsumerKind.DefenseAmmunition,
                        weapon.DisplayName);
                }
            }
        }

        foreach (SurgicalProcedureSO procedure in content.GetAll<SurgicalProcedureSO>())
        {
            foreach (SurgicalMaterialRequirement requirement in procedure.Materials)
            {
                if (requirement != null
                    && staticEntries.TryGetValue(
                        requirement.itemId?.Trim() ?? string.Empty,
                        out StaticUsage usage))
                {
                    usage.AddConsumer(
                        $"medical:{procedure.ProcedureId}",
                        procedure.RequiredResearchId,
                        ProductionConsumerKind.MedicalProcedure,
                        procedure.DisplayName);
                }
            }
        }

        foreach (EnvironmentalWorkwearSO workwear in
                 content.GetAll<EnvironmentalWorkwearSO>())
        {
            if (workwear != null
                && staticEntries.TryGetValue(
                    workwear.ItemDefinitionId,
                    out StaticUsage workwearUsage))
            {
                workwearUsage.AddConsumer(
                    $"environment-workwear:{workwear.WorkwearId}",
                    workwear.RequiredResearchId,
                    ProductionConsumerKind.EquipmentUse,
                    workwear.DisplayName);
            }
        }

        foreach (ApparelDefinitionSO apparel in
                 content.GetAll<ApparelDefinitionSO>())
        {
            if (apparel == null)
            {
                continue;
            }
            if (staticEntries.TryGetValue(
                    apparel.PhysicalItemId,
                    out StaticUsage apparelUsage))
            {
                apparelUsage.AddConsumer(
                    $"apparel-equip:{apparel.ApparelId}",
                    apparel.RequiredResearchId,
                    ProductionConsumerKind.EquipmentUse,
                    apparel.DisplayName);
            }
            foreach (TextileMaterialDefinitionSO material in
                     content.GetAll<TextileMaterialDefinitionSO>())
            {
                if (material != null
                    && (material.Tags & apparel.AllowedMaterialTags) != 0
                    && staticEntries.TryGetValue(
                        material.PhysicalItemId,
                        out StaticUsage materialUsage))
                {
                    materialUsage.AddConsumer(
                        $"apparel-material:{apparel.ApparelId}",
                        apparel.RequiredResearchId,
                        ProductionConsumerKind.EquipmentMaterial,
                        apparel.DisplayName);
                }
            }
        }

        IndexApparelMaintenanceSupply(
            "tool:sewing-kit",
            "apparel-repair:tool",
            ProductionConsumerKind.EquipmentUse);
        IndexApparelMaintenanceSupply(
            "material:sewing-thread",
            "apparel-repair:thread",
            ProductionConsumerKind.EquipmentMaterial);
        IndexApparelMaintenanceSupply(
            "material:sewing-thread",
            "apparel-alteration:thread",
            ProductionConsumerKind.EquipmentMaterial);
        IndexApparelMaintenanceSupply(
            "material:mending-scrap",
            "apparel-repair:patch",
            ProductionConsumerKind.EquipmentMaterial);
        IndexApparelMaintenanceSupply(
            "material:mending-scrap",
            "apparel-alteration:patch",
            ProductionConsumerKind.EquipmentMaterial);

        foreach (GuestRequestDefinitionSO guest in
                 content.GetAll<GuestRequestDefinitionSO>())
        {
            IndexConsumableRequirements(
                guest.StableId,
                guest.DisplayName,
                guest.serviceRequirements);
        }
        foreach (FactionContractDefinitionSO contract in
                 content.GetAll<FactionContractDefinitionSO>())
        {
            IndexConsumableRequirements(
                contract.StableId,
                contract.DisplayName,
                contract.completionRequirements);
        }

        foreach (ResourceItemDefinitionSO item in catalog.Items)
        {
            if (item != null
                && item.TryGetFeature(out SubstanceItemFeature substance)
                && staticEntries.TryGetValue(item.ItemId, out StaticUsage usage))
            {
                usage.AddConsumer(
                    $"substance:{substance.substanceId}",
                    item.RequiredResearchId,
                    ProductionConsumerKind.CharacterConsumption,
                    item.DisplayName);
            }
        }

        foreach (BuildingSO building in content.GetAll<BuildingSO>())
        {
            if (building == null)
            {
                continue;
            }

            BuildingWorkAmountAbility workAmount =
                building.GetAbility<BuildingWorkAmountAbility>();
            foreach (ItemAmountDefinition material in
                     workAmount?.ConstructionMaterials
                     ?? Array.Empty<ItemAmountDefinition>())
            {
                if (material != null
                    && staticEntries.TryGetValue(
                        material.ItemId,
                        out StaticUsage constructionUsage))
                {
                    constructionUsage.AddConsumer(
                        $"construction:{building.id}",
                        string.Empty,
                        ProductionConsumerKind.ConstructionMaterial,
                        string.IsNullOrWhiteSpace(building.objectName)
                            ? $"Building {building.id}"
                            : building.objectName);
                }
            }

            BuildingProductionWorkstationAbility workstation =
                building.GetProductionWorkstationAbility();
            if (workstation != null
                && staticEntries.TryGetValue(
                    workstation.StockSensorInstallationItemId,
                    out StaticUsage stockSensorUsage))
            {
                stockSensorUsage.AddConsumer(
                    $"production-stock-sensor:{building.id}",
                    string.Empty,
                    ProductionConsumerKind.FacilitySupply,
                    string.IsNullOrWhiteSpace(building.objectName)
                        ? $"Building {building.id} stock sensor"
                        : $"{building.objectName} stock sensor");
            }
            if (workstation != null
                && EquipmentProgressionWorkstationTags.IsModuleProcess(
                    workstation.WorkstationTag)
                && staticEntries.TryGetValue(
                    PhysicalItemIds.EquipmentModule,
                    out StaticUsage moduleProcessUsage))
            {
                moduleProcessUsage.AddConsumer(
                    $"equipment-module-process:{building.id}:{workstation.WorkstationTag}",
                    string.Empty,
                    ProductionConsumerKind.EquipmentProcessing,
                    string.IsNullOrWhiteSpace(building.objectName)
                        ? $"Building {building.id} equipment module process"
                        : building.objectName);
            }

            BuildingCropPlotAbility cropPlot =
                building.GetAbility<BuildingCropPlotAbility>();
            foreach (ItemAmountDefinition cycleSupply in
                     cropPlot?.CycleSupplyInputs
                     ?? Array.Empty<ItemAmountDefinition>())
            {
                if (cycleSupply != null
                    && staticEntries.TryGetValue(
                        cycleSupply.ItemId,
                        out StaticUsage cropSupplyUsage))
                {
                    cropSupplyUsage.AddConsumer(
                        $"crop-cycle-supply:{building.id}",
                        string.Empty,
                        ProductionConsumerKind.FacilitySupply,
                        string.IsNullOrWhiteSpace(building.objectName)
                            ? $"Building {building.id} crop supply"
                            : $"{building.objectName} crop supply");
                }
            }

            BuildingEquipmentMaintenanceAbility maintenance =
                building.GetAbility<BuildingEquipmentMaintenanceAbility>();
            if (maintenance != null
                && staticEntries.TryGetValue(
                    maintenance.RepairSupplyItemId,
                    out StaticUsage maintenanceUsage))
            {
                maintenanceUsage.AddConsumer(
                    $"equipment-maintenance:{building.id}",
                    string.Empty,
                    ProductionConsumerKind.FacilitySupply,
                    string.IsNullOrWhiteSpace(building.objectName)
                        ? $"Building {building.id} maintenance"
                        : $"{building.objectName} maintenance");
            }

            DefenseFacilityData defense = building.Defense;
            if (defense?.UsesPhysicalSupply == true
                && staticEntries.TryGetValue(
                    defense.supplyItemId?.Trim() ?? string.Empty,
                    out StaticUsage defenseSupplyUsage))
            {
                defenseSupplyUsage.AddConsumer(
                    $"defense-supply:{building.id}",
                    string.Empty,
                    ProductionConsumerKind.DefenseAmmunition,
                    string.IsNullOrWhiteSpace(building.objectName)
                        ? $"Building {building.id} defense supply"
                        : $"{building.objectName} defense supply");
            }

            BuildingFacilitySupplyAbility supply =
                building.GetAbility<BuildingFacilitySupplyAbility>();
            if (supply?.profiles == null)
            {
                continue;
            }

            foreach (FacilitySupplyProfile profile in supply.profiles)
            {
                if (profile == null)
                {
                    continue;
                }

                foreach (ResourceItemDefinitionSO item in catalog.Items)
                {
                    if (item == null
                        || !profile.Allows(item)
                        || !staticEntries.TryGetValue(
                            item.ItemId,
                            out StaticUsage usage))
                    {
                        continue;
                    }

                    usage.AddConsumer(
                        $"facility:{building.id}:supply:{profile.kind.ToString().ToLowerInvariant()}",
                        string.Empty,
                        ProductionConsumerKind.FacilitySupply,
                        string.IsNullOrWhiteSpace(building.objectName)
                            ? $"Building {building.id} {profile.kind}"
                            : $"{building.objectName} {profile.kind}");
                }
            }
        }

        string expeditionToolItemId = OffenseSupplyCatalog.GetPhysicalItemId(
            OffenseSupplyType.Tools);
        if (staticEntries.TryGetValue(
                expeditionToolItemId,
                out StaticUsage expeditionToolUsage))
        {
            expeditionToolUsage.AddConsumer(
                "offense-supply:tools",
                string.Empty,
                ProductionConsumerKind.FacilitySupply,
                "Expedition field tools");
        }

        foreach (ResourceItemDefinitionSO item in catalog.Items)
        {
            StaticUsage usage = staticEntries[item.ItemId];
            if (item.TryGetFeature(out FoodItemFeature _))
            {
                usage.AddConsumer(
                    $"item-consumption:{item.ItemId}",
                    item.RequiredResearchId,
                    ProductionConsumerKind.CharacterConsumption,
                    item.DisplayName);
            }
            if (item.TryGetFeature(out MedicineItemFeature medicine)
                && (medicine.supportsInjuryTreatment
                    || medicine.treatmentPotency > 0f
                    || medicine.infectionReduction > 0f
                    || medicine.detoxReduction > 0f
                    || medicine.painReduction > 0f))
            {
                usage.AddConsumer(
                    $"item-treatment:{item.ItemId}",
                    item.RequiredResearchId,
                    ProductionConsumerKind.MedicalProcedure,
                    item.DisplayName);
            }
            if (item.TryGetFeature(out MedicalProcedureSupplyItemFeature procedureSupply)
                && !string.IsNullOrWhiteSpace(procedureSupply.procedureId))
            {
                usage.AddConsumer(
                    $"medical-procedure:{procedureSupply.procedureId.Trim()}",
                    item.RequiredResearchId,
                    ProductionConsumerKind.MedicalProcedure,
                    item.DisplayName);
            }
            if (item.TryGetFeature(out CropTreatmentItemFeature cropTreatment))
            {
                usage.AddConsumer(
                    $"crop-treatment:{cropTreatment.treatmentKind}",
                    item.RequiredResearchId,
                    ProductionConsumerKind.CropTreatment,
                    item.DisplayName);
            }
            if (item.TryGetFeature(out InstallationItemFeature installation)
                && installation.buildingDefinitionId >= 0)
            {
                usage.AddConsumer(
                    $"building-installation:{installation.buildingDefinitionId}",
                    item.RequiredResearchId,
                    ProductionConsumerKind.Installation,
                    item.DisplayName);
            }
            if (item.TryGetFeature(out BlueprintItemFeature blueprint)
                && !string.IsNullOrWhiteSpace(blueprint.targetResearchId))
            {
                usage.AddConsumer(
                    $"research-blueprint:{blueprint.targetResearchId.Trim()}",
                    item.RequiredResearchId,
                    ProductionConsumerKind.Installation,
                    item.DisplayName);
            }
            if (item.Kind != ResourceItemKind.Intermediate
                && item.TryGetFeature(out MarketItemFeature market)
                && market.saleRate > 0f
                && item.UnitPrice > 0)
            {
                usage.AddConsumer(
                    $"market-sale:{item.ItemId}",
                    item.RequiredResearchId,
                    ProductionConsumerKind.MarketSale,
                    item.DisplayName);
            }
            if (string.Equals(
                item.ItemId,
                EquipmentProgressionItemIds.LineageSeal,
                StringComparison.Ordinal))
            {
                usage.AddConsumer(
                    "equipment-lineage:history-transfer",
                    item.RequiredResearchId,
                    ProductionConsumerKind.LineageTransfer,
                    item.DisplayName);
            }

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
            else if (PhysicalItemIds.IsEquipmentModule(item.ItemId))
            {
                foreach (EquipmentExpeditionRewardKind kind in
                         Enum.GetValues(typeof(EquipmentExpeditionRewardKind)))
                {
                    usage.AddProducer(
                        EquipmentExpeditionRewardSourceIds.ForModule(kind),
                        string.Empty);
                }
            }
            else if (item.TryGetFeature(out PathogenSampleItemFeature sample)
                && !string.IsNullOrWhiteSpace(sample.diseaseId))
            {
                usage.AddProducer(
                    $"diagnostic-sampling:{sample.diseaseId.Trim()}",
                    "research:health:pathogen-observation");
            }
            else if (string.Equals(
                item.ItemId,
                "medicine:mycelial-culture-pack",
                StringComparison.Ordinal))
            {
                usage.AddProducer(
                    "medical-procedure:mycelial-culture-harvest",
                    "research:medical:mycelial-grafting");
            }

        }
    }

    private void IndexApparelMaintenanceSupply(
        string itemId,
        string consumerId,
        ProductionConsumerKind kind)
    {
        if (staticEntries.TryGetValue(itemId, out StaticUsage usage))
        {
            usage.AddConsumer(
                consumerId,
                "research:textile:tailoring",
                kind,
                consumerId);
        }
    }

    private void IndexConsumableRequirements(
        string consumerId,
        string displayName,
        V20ContentRequirementSet requirements)
    {
        foreach (V20ItemAmountRequirement requirement in
                 requirements?.items ?? new List<V20ItemAmountRequirement>())
        {
            if (requirement == null
                || !requirement.consume
                || !staticEntries.TryGetValue(
                    requirement.itemDefinitionId?.Trim() ?? string.Empty,
                    out StaticUsage usage))
            {
                continue;
            }

            usage.AddConsumer(
                consumerId,
                string.Empty,
                ProductionConsumerKind.SocietyEvent,
                displayName);
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
        private readonly Dictionary<string, ProductionConsumerLink> consumerLinks =
            new Dictionary<string, ProductionConsumerLink>(StringComparer.Ordinal);

        public IReadOnlyList<string> Producers => producers.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        public IReadOnlyList<string> Consumers => consumers.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        public IReadOnlyList<string> ResearchIds => researchIds.OrderBy(id => id, StringComparer.Ordinal).ToArray();
        public IReadOnlyList<ProductionConsumerLink> ConsumerLinks => consumerLinks.Values
            .OrderBy(link => link.kind)
            .ThenBy(link => link.consumerId, StringComparer.Ordinal)
            .ToArray();

        public void AddProducer(string id, string researchId)
        {
            Add(producers, id, researchId);
        }

        public void AddConsumer(
            string id,
            string researchId,
            ProductionConsumerKind kind = ProductionConsumerKind.RecipeInput,
            string displayName = "")
        {
            Add(consumers, id, researchId);
            string normalized = id?.Trim() ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(normalized))
            {
                consumerLinks[normalized] = new ProductionConsumerLink
                {
                    consumerId = normalized,
                    kind = kind,
                    requiredResearchId = researchId?.Trim() ?? string.Empty,
                    displayName = displayName?.Trim() ?? string.Empty
                };
            }
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
