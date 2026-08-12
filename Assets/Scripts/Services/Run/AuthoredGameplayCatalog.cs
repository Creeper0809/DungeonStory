using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IRunVariableDefinitionCatalog
{
    IReadOnlyCollection<RunVariableDefinition> All { get; }
    RunVariableDefinition Get(string id);
    RunVariableDefinition Require(string id);
    IReadOnlyList<RunVariableDefinition> GetByCategory(RunVariableCategory category);
}

public interface IOwnerDoctrineDefinitionCatalog
{
    IReadOnlyCollection<OwnerDoctrineDefinition> All { get; }
    OwnerDoctrineDefinition Get(string id);
    OwnerDoctrineDefinition Require(string id);
    OwnerDoctrineDefinition ResolveFor(CharacterSO owner);
    OwnerDoctrineDefinition ResolveForSpecies(string speciesTag);
}

public interface ICharacterNeedDefinitionCatalog : ICharacterNeedDefinitionQuery
{
    float GetUrgency(CharacterActor actor, CharacterCondition condition);
    float GetWeightedUrgency(CharacterActor actor, CharacterCondition condition);
    float GetStrongestUrgency(
        CharacterActor actor,
        CharacterNeedTag requiredTag,
        bool applySurvivalWeight = true);
    bool TryGetStrongestUrgency(
        CharacterActor actor,
        CharacterNeedTag requiredTag,
        out CharacterNeedDefinition strongest,
        out float urgency,
        bool applySurvivalWeight = true);
}

public interface IStockCategoryDefinitionCatalog : IWarehouseStockCategoryCatalogPort
{
    bool TryGet(string id, out StockCategoryDefinition definition);
    StockCategoryDefinition Require(StockCategory category);
    string GetDisplayName(StockCategory category);
    string GetShortName(StockCategory category);
}

public interface IBuildingCategoryDefinitionCatalog
{
    IReadOnlyList<BuildingCategoryDefinition> All { get; }
    bool TryGet(BuildingCategory category, out BuildingCategoryDefinition definition);
    bool TryResolve(string value, out BuildingCategoryDefinition definition);
    BuildingCategoryDefinition Require(BuildingCategory category);
    string GetDisplayName(BuildingCategory category, string fallback = "시설");
    int GetShopCostWeight(BuildingCategory category);
}

/// <summary>
/// Immutable runtime projection of records authored on the root domain-content SO.
/// The service owns no mutable game state and never fabricates missing definitions.
/// </summary>
public sealed class AuthoredGameplayCatalog :
    IMetaUpgradeDefinitionCatalog,
    IRunVariableDefinitionCatalog,
    IOwnerDoctrineDefinitionCatalog,
    IInvasionIntruderPatternDefinitionCatalog,
    ICharacterNeedDefinitionCatalog,
    IStockCategoryDefinitionCatalog,
    IBuildingCategoryDefinitionCatalog
{
    private readonly IReadOnlyDictionary<string, MetaUpgradeDefinition> metaById;
    private readonly IReadOnlyDictionary<string, RunVariableDefinition> runById;
    private readonly IReadOnlyDictionary<string, OwnerDoctrineDefinition> doctrineById;
    private readonly IReadOnlyDictionary<string, InvasionIntruderPatternDefinition> patternById;
    private readonly IReadOnlyDictionary<string, CharacterNeedDefinition> needById;
    private readonly IReadOnlyDictionary<CharacterCondition, CharacterNeedDefinition> needByCondition;
    private readonly IReadOnlyDictionary<string, StockCategoryDefinition> stockById;
    private readonly IReadOnlyDictionary<StockCategory, StockCategoryDefinition> stockByCategory;
    private readonly IReadOnlyDictionary<string, BuildingCategoryDefinition> buildingById;
    private readonly IReadOnlyDictionary<BuildingCategory, BuildingCategoryDefinition> buildingByCategory;
    private readonly MetaUpgradeDefinition[] metaDefinitions;
    private readonly RunVariableDefinition[] runDefinitions;
    private readonly OwnerDoctrineDefinition[] doctrineDefinitions;
    private readonly InvasionIntruderPatternDefinition[] patternDefinitions;
    private readonly CharacterNeedDefinition[] needDefinitions;
    private readonly StockCategoryDefinition[] stockDefinitions;
    private readonly BuildingCategoryDefinition[] buildingDefinitions;

    public AuthoredGameplayCatalog(IGameContentCatalog content)
    {
        GameDomainContentCatalogSO domain = (content
                ?? throw new ArgumentNullException(nameof(content)))
            .Domain
            ?? throw new InvalidOperationException("The root content catalog has no domain catalog.");

        metaDefinitions = domain.MetaUpgrades.Select(CreateMetaDefinition).ToArray();
        runDefinitions = domain.RunVariables.Select(CreateRunDefinition).ToArray();
        doctrineDefinitions = domain.OwnerDoctrines.Select(CreateDoctrineDefinition).ToArray();
        patternDefinitions = domain.InvasionPatterns.Select(CreatePatternDefinition).ToArray();
        needDefinitions = domain.CharacterNeeds
            .Select(CreateNeedDefinition)
            .OrderBy(definition => definition.SortOrder)
            .ThenBy(definition => definition.Id, StringComparer.Ordinal)
            .ToArray();
        stockDefinitions = domain.StockCategories
            .Select(CreateStockDefinition)
            .OrderBy(definition => definition.SortOrder)
            .ThenBy(definition => definition.Id, StringComparer.Ordinal)
            .ToArray();
        ValidateStockDeliveryItems(content.Items.Definitions, stockDefinitions);
        buildingDefinitions = domain.BuildingCategories
            .Select(CreateBuildingDefinition)
            .OrderBy(definition => definition.SortOrder)
            .ThenBy(definition => definition.Id, StringComparer.Ordinal)
            .ToArray();

        metaById = Index(metaDefinitions, definition => definition.id, "meta upgrade");
        runById = Index(runDefinitions, definition => definition.id, "run variable");
        doctrineById = Index(doctrineDefinitions, definition => definition.id, "owner doctrine");
        patternById = Index(patternDefinitions, definition => definition.id, "invasion pattern");
        needById = Index(needDefinitions, definition => definition.Id, "character need");
        needByCondition = IndexByValue(
            needDefinitions,
            definition => definition.Condition,
            "character need condition");
        stockById = Index(stockDefinitions, definition => definition.Id, "stock category");
        stockByCategory = IndexByValue(
            stockDefinitions,
            definition => definition.Category,
            "stock category value");
        buildingById = Index(
            buildingDefinitions,
            definition => definition.Id,
            "building category",
            StringComparer.OrdinalIgnoreCase);
        buildingByCategory = IndexByValue(
            buildingDefinitions,
            definition => definition.Category,
            "building category value");

        if (!patternById.ContainsKey(InvasionIntruderPatternIds.Hunter))
        {
            throw new InvalidOperationException(
                $"Authored invasion content requires default pattern '{InvasionIntruderPatternIds.Hunter}'.");
        }
    }

    IReadOnlyCollection<MetaUpgradeDefinition> IMetaUpgradeDefinitionCatalog.All => metaDefinitions;
    IReadOnlyCollection<RunVariableDefinition> IRunVariableDefinitionCatalog.All => runDefinitions;
    IReadOnlyCollection<OwnerDoctrineDefinition> IOwnerDoctrineDefinitionCatalog.All => doctrineDefinitions;
    IReadOnlyCollection<InvasionIntruderPatternDefinition> IInvasionIntruderPatternDefinitionCatalog.All => patternDefinitions;
    IReadOnlyList<CharacterNeedDefinition> ICharacterNeedDefinitionQuery.All => needDefinitions;
    IReadOnlyList<StockCategoryDefinition> IWarehouseStockCategoryCatalogPort.All =>
        stockDefinitions;
    IReadOnlyList<BuildingCategoryDefinition> IBuildingCategoryDefinitionCatalog.All => buildingDefinitions;

    InvasionIntruderPatternDefinition IInvasionIntruderPatternDefinitionCatalog.Default =>
        RequirePattern(InvasionIntruderPatternIds.Hunter);

    MetaUpgradeDefinition IMetaUpgradeDefinitionCatalog.Get(string id) => Get(metaById, id);
    RunVariableDefinition IRunVariableDefinitionCatalog.Get(string id) => Get(runById, id);
    OwnerDoctrineDefinition IOwnerDoctrineDefinitionCatalog.Get(string id) => Get(doctrineById, id);
    InvasionIntruderPatternDefinition IInvasionIntruderPatternDefinitionCatalog.Get(string id) => Get(patternById, id);

    bool ICharacterNeedDefinitionQuery.TryGet(
        CharacterCondition condition,
        out CharacterNeedDefinition definition) => needByCondition.TryGetValue(condition, out definition);

    bool ICharacterNeedDefinitionQuery.TryGet(
        string id,
        out CharacterNeedDefinition definition) => TryGet(needById, id, out definition);

    bool IWarehouseStockCategoryCatalogPort.TryGet(
        StockCategory category,
        out StockCategoryDefinition definition) => stockByCategory.TryGetValue(category, out definition);

    bool IStockCategoryDefinitionCatalog.TryGet(
        string id,
        out StockCategoryDefinition definition) => TryGet(stockById, id, out definition);

    bool IBuildingCategoryDefinitionCatalog.TryGet(
        BuildingCategory category,
        out BuildingCategoryDefinition definition) => buildingByCategory.TryGetValue(category, out definition);

    bool IBuildingCategoryDefinitionCatalog.TryResolve(
        string value,
        out BuildingCategoryDefinition definition) => TryResolveBuilding(value, out definition);

    MetaUpgradeDefinition IMetaUpgradeDefinitionCatalog.Require(string id) =>
        Require(metaById, id, "meta upgrade");

    RunVariableDefinition IRunVariableDefinitionCatalog.Require(string id) =>
        Require(runById, id, "run variable");

    OwnerDoctrineDefinition IOwnerDoctrineDefinitionCatalog.Require(string id) =>
        Require(doctrineById, id, "owner doctrine");

    InvasionIntruderPatternDefinition IInvasionIntruderPatternDefinitionCatalog.Require(string id) =>
        RequirePattern(id);

    CharacterNeedDefinition ICharacterNeedDefinitionQuery.Require(CharacterCondition condition) =>
        Require(needByCondition, condition, "character need condition");

    StockCategoryDefinition IStockCategoryDefinitionCatalog.Require(StockCategory category) =>
        Require(stockByCategory, category, "stock category");

    BuildingCategoryDefinition IBuildingCategoryDefinitionCatalog.Require(BuildingCategory category) =>
        Require(buildingByCategory, category, "building category");

    string IStockCategoryDefinitionCatalog.GetDisplayName(StockCategory category) =>
        stockByCategory.TryGetValue(category, out StockCategoryDefinition definition)
            ? definition.DisplayName
            : category.ToString();

    string IStockCategoryDefinitionCatalog.GetShortName(StockCategory category) =>
        stockByCategory.TryGetValue(category, out StockCategoryDefinition definition)
            ? definition.ShortName
            : category.ToString();

    string IBuildingCategoryDefinitionCatalog.GetDisplayName(
        BuildingCategory category,
        string fallback) => buildingByCategory.TryGetValue(category, out BuildingCategoryDefinition definition)
            ? definition.DisplayName
            : fallback;

    int IBuildingCategoryDefinitionCatalog.GetShopCostWeight(BuildingCategory category) =>
        buildingByCategory.TryGetValue(category, out BuildingCategoryDefinition definition)
            ? definition.ShopCostWeight
            : 100;

    float ICharacterNeedDefinitionCatalog.GetUrgency(
        CharacterActor actor,
        CharacterCondition condition) => needByCondition.TryGetValue(condition, out CharacterNeedDefinition definition)
            ? definition.GetUrgency(actor)
            : 0.5f;

    float ICharacterNeedDefinitionCatalog.GetWeightedUrgency(
        CharacterActor actor,
        CharacterCondition condition) => needByCondition.TryGetValue(condition, out CharacterNeedDefinition definition)
            ? Mathf.Clamp01(definition.GetUrgency(actor) * definition.SurvivalWeight)
            : 0f;

    float ICharacterNeedDefinitionCatalog.GetStrongestUrgency(
        CharacterActor actor,
        CharacterNeedTag requiredTag,
        bool applySurvivalWeight)
    {
        TryGetStrongestUrgency(
            actor,
            requiredTag,
            out _,
            out float urgency,
            applySurvivalWeight);
        return urgency;
    }

    bool ICharacterNeedDefinitionCatalog.TryGetStrongestUrgency(
        CharacterActor actor,
        CharacterNeedTag requiredTag,
        out CharacterNeedDefinition strongest,
        out float urgency,
        bool applySurvivalWeight) => TryGetStrongestUrgency(
            actor,
            requiredTag,
            out strongest,
            out urgency,
            applySurvivalWeight);

    IReadOnlyList<RunVariableDefinition> IRunVariableDefinitionCatalog.GetByCategory(
        RunVariableCategory category)
    {
        return runDefinitions
            .Where(definition => definition.category == category)
            .OrderBy(definition => definition.id, StringComparer.Ordinal)
            .ToArray();
    }

    OwnerDoctrineDefinition IOwnerDoctrineDefinitionCatalog.ResolveFor(CharacterSO owner)
    {
        return ResolveDoctrineForSpecies(owner?.SpeciesTag);
    }

    OwnerDoctrineDefinition IOwnerDoctrineDefinitionCatalog.ResolveForSpecies(string speciesTag)
    {
        return ResolveDoctrineForSpecies(speciesTag);
    }

    private OwnerDoctrineDefinition ResolveDoctrineForSpecies(string speciesTag)
    {
        return string.IsNullOrWhiteSpace(speciesTag)
            ? null
            : doctrineDefinitions.FirstOrDefault(definition => string.Equals(
                definition.speciesTag,
                speciesTag,
                StringComparison.OrdinalIgnoreCase));
    }

    private InvasionIntruderPatternDefinition RequirePattern(string id)
    {
        return Require(patternById, id, "invasion pattern");
    }

    private static MetaUpgradeDefinition CreateMetaDefinition(AuthoredMetaUpgradeRecord record)
    {
        RequireRecord(record, record?.id, "meta upgrade");
        IMetaUpgradeEffect[] effects = (record.effects ?? new List<AuthoredGameplayEffectRecord>())
            .Select(CreateMetaEffect)
            .ToArray();
        if (effects.Length == 0)
        {
            throw new InvalidOperationException($"Meta upgrade '{record.id}' has no effects.");
        }

        return new MetaUpgradeDefinition(
            record.id,
            record.branch,
            record.title,
            record.detail,
            record.cost,
            record.maxLevel,
            effects);
    }

    private static RunVariableDefinition CreateRunDefinition(AuthoredRunVariableRecord record)
    {
        RequireRecord(record, record?.id, "run variable");
        IRunVariableEffect[] effects = CreateRunEffects(record.effects, record.id);
        return new RunVariableDefinition(
            record.id,
            record.category,
            record.title,
            record.detail,
            record.importance,
            record.activeDays,
            effects);
    }

    private static OwnerDoctrineDefinition CreateDoctrineDefinition(AuthoredOwnerDoctrineRecord record)
    {
        RequireRecord(record, record?.id, "owner doctrine");
        if (string.IsNullOrWhiteSpace(record.speciesTag))
        {
            throw new InvalidOperationException($"Owner doctrine '{record.id}' has no species tag.");
        }

        return new OwnerDoctrineDefinition(
            record.id,
            record.speciesTag,
            record.title,
            record.benefit,
            record.tradeoff,
            CreateRunEffects(record.effects, record.id));
    }

    private static InvasionIntruderPatternDefinition CreatePatternDefinition(
        AuthoredInvasionPatternRecord record)
    {
        RequireRecord(record, record?.id, "invasion pattern");
        return new InvasionIntruderPatternDefinition(
            record.id,
            record.title,
            record.detail,
            record.targetPreference,
            record.directOwnerFocus,
            record.facilityDiversionFocus,
            record.maxFacilityDamageCount,
            record.riskTolerance,
            record.routeCommitmentSeconds,
            record.structureDamageMultiplier,
            (record.preferredFacilityFamilyIds ?? new List<string>()).ToArray());
    }

    private static CharacterNeedDefinition CreateNeedDefinition(AuthoredCharacterNeedRecord record)
    {
        RequireRecord(record, record?.id, "character need");
        return new CharacterNeedDefinition(
            new CharacterNeedIdentity(
                record.id,
                record.condition,
                record.displayName,
                record.sortOrder),
            new CharacterNeedDefaults(
                record.defaultValue,
                record.workerInitialValue),
            new CharacterNeedBehavior(
                record.relatedFacilityRole,
                record.tags,
                record.survivalWeight),
            new CharacterNeedMoodProfile(
                new CharacterNeedMoodBand(
                    record.criticalMaximum,
                    record.criticalLabel,
                    record.criticalMood),
                new CharacterNeedMoodBand(
                    record.lowMaximum,
                    record.lowLabel,
                    record.lowMood),
                new CharacterNeedMoodBand(
                    record.highMinimum,
                    record.highLabel,
                    record.highMood)));
    }

    private static StockCategoryDefinition CreateStockDefinition(AuthoredStockCategoryRecord record)
    {
        RequireRecord(record, record?.id, "stock category");
        return new StockCategoryDefinition(
            record.id,
            record.category,
            record.displayName,
            record.shortName,
            record.sortOrder,
            record.seedWeight,
            record.deliveryItemId,
            record.dailyBaseAmount,
            record.dailyUnitCost,
            record.dailyGrowthDivisor);
    }

    private static void ValidateStockDeliveryItems(
        IReadOnlyList<ItemDefinitionSO> itemDefinitions,
        IEnumerable<StockCategoryDefinition> stockDefinitions)
    {
        Dictionary<string, ItemDefinitionSO> items = (itemDefinitions
                ?? throw new ArgumentNullException(nameof(itemDefinitions)))
            .Where(value => value != null)
            .ToDictionary(value => value.ItemId, StringComparer.Ordinal);
        foreach (StockCategoryDefinition stock in stockDefinitions
                     .Where(value => value.DailyBaseAmount > 0))
        {
            if (!items.TryGetValue(stock.DeliveryItemId, out ItemDefinitionSO item))
            {
                throw new InvalidOperationException(
                    $"Stock category '{stock.Id}' delivery item "
                    + $"'{stock.DeliveryItemId}' is not authored.");
            }

            if (item.StockCategory != stock.Category)
            {
                throw new InvalidOperationException(
                    $"Stock category '{stock.Id}' delivery item '{item.ItemId}' belongs to "
                    + $"'{item.StockCategory}', expected '{stock.Category}'.");
            }

            if (item.MaxStack <= 1)
            {
                throw new InvalidOperationException(
                    $"Stock category '{stock.Id}' delivery item '{item.ItemId}' must be stackable.");
            }
        }
    }

    private static BuildingCategoryDefinition CreateBuildingDefinition(
        AuthoredBuildingCategoryRecord record)
    {
        RequireRecord(record, record?.id, "building category");
        return new BuildingCategoryDefinition(
            record.id,
            record.category,
            record.displayName,
            record.sortOrder,
            record.shopCostWeight);
    }

    private static IMetaUpgradeEffect CreateMetaEffect(AuthoredGameplayEffectRecord record)
    {
        if (record == null)
        {
            throw new InvalidOperationException("Meta effect record is null.");
        }

        return record.kind switch
        {
            AuthoredGameplayEffectKind.MetaIntegerBonus =>
                new MetaIntegerBonusEffect(record.id, Mathf.RoundToInt(record.numberValue)),
            AuthoredGameplayEffectKind.MetaMultiplierDelta =>
                new MetaMultiplierDeltaEffect(record.id, record.numberValue),
            _ => throw new InvalidOperationException(
                $"Effect kind '{record.kind}' is not valid for a meta upgrade.")
        };
    }

    private static IRunVariableEffect[] CreateRunEffects(
        IEnumerable<AuthoredGameplayEffectRecord> records,
        string ownerId)
    {
        IRunVariableEffect[] effects = (records ?? Array.Empty<AuthoredGameplayEffectRecord>())
            .Select(CreateRunEffect)
            .ToArray();
        if (effects.Length == 0)
        {
            throw new InvalidOperationException($"Authored definition '{ownerId}' has no effects.");
        }

        return effects;
    }

    private static IRunVariableEffect CreateRunEffect(AuthoredGameplayEffectRecord record)
    {
        if (record == null)
        {
            throw new InvalidOperationException("Run effect record is null.");
        }

        return record.kind switch
        {
            AuthoredGameplayEffectKind.GuestDemandMultiplier =>
                new RunGuestDemandEffect(record.textValue, record.numberValue),
            AuthoredGameplayEffectKind.StockCostMultiplier =>
                new RunStockCostEffect(record.stockCategory, record.numberValue),
            AuthoredGameplayEffectKind.FacilityShopCostMultiplier =>
                new RunFacilityShopCostEffect(record.numberValue, record.defenseOnly),
            AuthoredGameplayEffectKind.BlueprintCostMultiplier =>
                new RunBlueprintCostEffect(record.numberValue),
            AuthoredGameplayEffectKind.ThreatRiseMultiplier =>
                new RunThreatRiseEffect(record.numberValue),
            AuthoredGameplayEffectKind.WarningThresholdMultiplier =>
                new RunWarningThresholdEffect(record.numberValue),
            AuthoredGameplayEffectKind.IntruderPattern =>
                new RunIntruderPatternEffect(record.textValue),
            AuthoredGameplayEffectKind.FocusTimeMultiplier =>
                new RunFocusTimeEffect(record.numberValue),
            AuthoredGameplayEffectKind.RepathIntervalMultiplier =>
                new RunRepathIntervalEffect(record.numberValue),
            AuthoredGameplayEffectKind.FacilityDamageIntervalMultiplier =>
                new RunFacilityDamageIntervalEffect(record.numberValue),
            AuthoredGameplayEffectKind.FinalCombatDamageMultiplier =>
                new RunFinalCombatDamageEffect(record.numberValue),
            _ => throw new InvalidOperationException(
                $"Effect kind '{record.kind}' is not valid for a run definition.")
        };
    }

    private static IReadOnlyDictionary<string, T> Index<T>(
        IEnumerable<T> definitions,
        Func<T, string> idSelector,
        string label,
        IEqualityComparer<string> comparer = null)
        where T : class
    {
        Dictionary<string, T> result = new(comparer ?? StringComparer.Ordinal);
        foreach (T definition in definitions ?? Array.Empty<T>())
        {
            string id = idSelector(definition)?.Trim() ?? string.Empty;
            if (id.Length == 0 || !result.TryAdd(id, definition))
            {
                throw new InvalidOperationException($"Authored {label} id '{id}' is empty or duplicated.");
            }
        }

        return result;
    }

    private static IReadOnlyDictionary<TKey, T> IndexByValue<TKey, T>(
        IEnumerable<T> definitions,
        Func<T, TKey> keySelector,
        string label)
        where T : class
    {
        Dictionary<TKey, T> result = new();
        foreach (T definition in definitions ?? Array.Empty<T>())
        {
            TKey key = keySelector(definition);
            if (!result.TryAdd(key, definition))
            {
                throw new InvalidOperationException(
                    $"Authored {label} '{key}' is duplicated.");
            }
        }

        return result;
    }

    private static T Get<T>(IReadOnlyDictionary<string, T> definitions, string id)
        where T : class
    {
        string normalized = id?.Trim() ?? string.Empty;
        return definitions.TryGetValue(normalized, out T definition) ? definition : null;
    }

    private static bool TryGet<T>(
        IReadOnlyDictionary<string, T> definitions,
        string id,
        out T definition)
        where T : class
    {
        string normalized = id?.Trim() ?? string.Empty;
        return definitions.TryGetValue(normalized, out definition);
    }

    private static T Require<TKey, T>(
        IReadOnlyDictionary<TKey, T> definitions,
        TKey key,
        string label)
        where T : class
    {
        return definitions.TryGetValue(key, out T definition)
            ? definition
            : throw new KeyNotFoundException(
                $"Required {label} '{key}' is not authored in the root content catalog.");
    }

    private bool TryResolveBuilding(
        string value,
        out BuildingCategoryDefinition definition)
    {
        string normalized = value?.Trim() ?? string.Empty;
        if (buildingById.TryGetValue(normalized, out definition))
        {
            return true;
        }

        definition = buildingDefinitions.FirstOrDefault(candidate =>
            string.Equals(candidate.DisplayName, normalized, StringComparison.OrdinalIgnoreCase)
            || string.Equals(candidate.Category.ToString(), normalized, StringComparison.OrdinalIgnoreCase));
        return definition != null;
    }

    private bool TryGetStrongestUrgency(
        CharacterActor actor,
        CharacterNeedTag requiredTag,
        out CharacterNeedDefinition strongest,
        out float urgency,
        bool applySurvivalWeight)
    {
        strongest = null;
        urgency = 0f;
        for (int index = 0; index < needDefinitions.Length; index++)
        {
            CharacterNeedDefinition definition = needDefinitions[index];
            if (!definition.HasTag(requiredTag))
            {
                continue;
            }

            float candidate = definition.GetUrgency(actor)
                * (applySurvivalWeight ? definition.SurvivalWeight : 1f);
            if (strongest != null && candidate <= urgency)
            {
                continue;
            }

            strongest = definition;
            urgency = candidate;
        }

        urgency = Mathf.Clamp01(urgency);
        return strongest != null;
    }

    private static T Require<T>(
        IReadOnlyDictionary<string, T> definitions,
        string id,
        string label)
        where T : class
    {
        T definition = Get(definitions, id);
        return definition ?? throw new KeyNotFoundException(
            $"Required {label} '{id?.Trim()}' is not authored in the root content catalog.");
    }

    private static void RequireRecord(object record, string id, string label)
    {
        if (record == null || string.IsNullOrWhiteSpace(id))
        {
            throw new InvalidOperationException($"Authored {label} record requires a stable id.");
        }
    }
}
