using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Content.CoreSession;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
[CreateAssetMenu(
    fileName = "GameDomainContentCatalog",
    menuName = "DungeonStory/Content/Game Domain Content Catalog",
    order = -99)]
public sealed class GameDomainContentCatalogSO : ScriptableObject
{
    [SerializeField] private List<ScriptableObject> definitions = new();
    [SerializeField] private List<AuthoredMetaUpgradeRecord> metaUpgrades = new();
    [SerializeField] private List<AuthoredRunVariableRecord> runVariables = new();
    [SerializeField] private List<AuthoredOwnerDoctrineRecord> ownerDoctrines = new();
    [SerializeField] private List<AuthoredInvasionPatternRecord> invasionPatterns = new();
    [SerializeField] private List<AuthoredCharacterNeedRecord> characterNeeds = new();
    [SerializeField] private List<AuthoredStockCategoryRecord> stockCategories = new();
    [SerializeField] private List<AuthoredBuildingCategoryRecord> buildingCategories = new();
    [SerializeField] private CoreSessionRulesSO coreSessionRules;

    public IReadOnlyList<ScriptableObject> Definitions => definitions;
    public IReadOnlyList<AuthoredMetaUpgradeRecord> MetaUpgrades => metaUpgrades;
    public IReadOnlyList<AuthoredRunVariableRecord> RunVariables => runVariables;
    public IReadOnlyList<AuthoredOwnerDoctrineRecord> OwnerDoctrines => ownerDoctrines;
    public IReadOnlyList<AuthoredInvasionPatternRecord> InvasionPatterns => invasionPatterns;
    public IReadOnlyList<AuthoredCharacterNeedRecord> CharacterNeeds => characterNeeds;
    public IReadOnlyList<AuthoredStockCategoryRecord> StockCategories => stockCategories;
    public IReadOnlyList<AuthoredBuildingCategoryRecord> BuildingCategories => buildingCategories;
    public CoreSessionRulesSO CoreSessionRules => coreSessionRules;

    public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject
    {
        return definitions
            .OfType<T>()
            .Where(value => value != null)
            .Distinct()
            .ToArray();
    }

    public IReadOnlyList<string> ValidateCatalog()
    {
        List<string> errors = new();
        HashSet<ScriptableObject> uniqueDefinitions = new();
        for (int index = 0; index < definitions.Count; index++)
        {
            if (definitions[index] == null)
            {
                errors.Add($"Domain content reference {index} is missing.");
            }
            else if (!uniqueDefinitions.Add(definitions[index]))
            {
                errors.Add(
                    $"Domain content reference '{definitions[index].name}' is duplicated.");
            }
            else if (string.Equals(
                         definitions[index].GetType().Name,
                         "SubstanceDefinitionSO",
                         StringComparison.Ordinal))
            {
                errors.Add(
                    "Legacy SubstanceDefinitionSO content is forbidden; "
                    + "author substance data on ItemDefinitionSO instead.");
            }
        }

        ValidateRecords(metaUpgrades, record => record?.id, "meta upgrade", errors);
        ValidateRecords(runVariables, record => record?.id, "run variable", errors);
        ValidateRecords(ownerDoctrines, record => record?.id, "owner doctrine", errors);
        ValidateRecords(invasionPatterns, record => record?.id, "invasion pattern", errors);
        ValidateRecords(characterNeeds, record => record?.id, "character need", errors);
        ValidateRecords(stockCategories, record => record?.id, "stock category", errors);
        ValidateRecords(buildingCategories, record => record?.id, "building category", errors);

        if (coreSessionRules == null)
        {
            errors.Add("Core-session rules reference is missing.");
        }
        else
        {
            foreach (string error in coreSessionRules.ValidateDefinition())
            {
                errors.Add($"Core-session rules: {error}");
            }

            if (!definitions.Contains(coreSessionRules))
            {
                errors.Add(
                    "Core-session rules are not indexed by the domain definitions list.");
            }
        }

        ValidateEffects(metaUpgrades.SelectMany(record =>
            record?.effects ?? new List<AuthoredGameplayEffectRecord>()), errors);
        ValidateEffects(runVariables.SelectMany(record =>
            record?.effects ?? new List<AuthoredGameplayEffectRecord>()), errors);
        ValidateEffects(ownerDoctrines.SelectMany(record =>
            record?.effects ?? new List<AuthoredGameplayEffectRecord>()), errors);

        return errors;
    }

    private static void ValidateRecords<T>(
        IEnumerable<T> records,
        Func<T, string> idSelector,
        string label,
        ICollection<string> errors)
        where T : class
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        int index = 0;
        foreach (T record in records ?? Array.Empty<T>())
        {
            string id = record != null ? idSelector(record)?.Trim() ?? string.Empty : string.Empty;
            if (id.Length == 0)
            {
                errors.Add($"Authored {label} record {index} has an empty id.");
            }
            else if (!ids.Add(id))
            {
                errors.Add($"Authored {label} id '{id}' is duplicated.");
            }

            index++;
        }
    }

    private static void ValidateEffects(
        IEnumerable<AuthoredGameplayEffectRecord> effects,
        ICollection<string> errors)
    {
        int index = 0;
        foreach (AuthoredGameplayEffectRecord effect in effects
                     ?? Array.Empty<AuthoredGameplayEffectRecord>())
        {
            if (effect == null || effect.kind == AuthoredGameplayEffectKind.None)
            {
                errors.Add($"Authored gameplay effect {index} has no effect kind.");
            }

            index++;
        }
    }

#if UNITY_EDITOR
    public void SetDefinitions(IEnumerable<ScriptableObject> values)
    {
        definitions = (values ?? Array.Empty<ScriptableObject>())
            .Where(value => value != null && value != this)
            .Distinct()
            .OrderBy(value => value.GetType().FullName, StringComparer.Ordinal)
            .ThenBy(value => value.name, StringComparer.Ordinal)
            .ToList();
    }
#endif
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AuthoredMetaUpgradeRecord
{
    public string id = string.Empty;
    public MetaProgressionBranch branch;
    public string title = string.Empty;
    public string detail = string.Empty;
    public int cost;
    public int maxLevel = 1;
    public List<AuthoredGameplayEffectRecord> effects = new();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AuthoredRunVariableRecord
{
    public string id = string.Empty;
    public RunVariableCategory category;
    public string title = string.Empty;
    public string detail = string.Empty;
    public EventAlertImportance importance;
    public int activeDays = 1;
    public List<AuthoredGameplayEffectRecord> effects = new();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AuthoredOwnerDoctrineRecord
{
    public string id = string.Empty;
    public string speciesTag = string.Empty;
    public string title = string.Empty;
    public string benefit = string.Empty;
    public string tradeoff = string.Empty;
    public List<AuthoredGameplayEffectRecord> effects = new();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AuthoredInvasionPatternRecord
{
    public string id = string.Empty;
    public string title = string.Empty;
    public string detail = string.Empty;
    public InvasionIntruderTargetPreference targetPreference;
    public float directOwnerFocus;
    public float facilityDiversionFocus;
    public int maxFacilityDamageCount;
    public float riskTolerance = 0.55f;
    public float routeCommitmentSeconds = 2f;
    public float structureDamageMultiplier = 1f;
    public List<string> preferredFacilityFamilyIds = new();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AuthoredCharacterNeedRecord
{
    public string id = string.Empty;
    public CharacterCondition condition;
    public string displayName = string.Empty;
    public int sortOrder;
    public float defaultValue = 100f;
    public float workerInitialValue = 80f;
    public FacilityRole relatedFacilityRole;
    public CharacterNeedTag tags;
    public float survivalWeight = 1f;
    public float criticalMaximum = 15f;
    public string criticalLabel = string.Empty;
    public float criticalMood;
    public float lowMaximum = 35f;
    public string lowLabel = string.Empty;
    public float lowMood;
    public float highMinimum = 85f;
    public string highLabel = string.Empty;
    public float highMood;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AuthoredStockCategoryRecord
{
    public string id = string.Empty;
    public StockCategory category;
    public string displayName = string.Empty;
    public string shortName = string.Empty;
    public int sortOrder;
    public float seedWeight;
    public int dailyBaseAmount;
    public int dailyUnitCost;
    public int dailyGrowthDivisor = 1;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AuthoredBuildingCategoryRecord
{
    public string id = string.Empty;
    public BuildingCategory category;
    public string displayName = string.Empty;
    public int sortOrder;
    public int shopCostWeight = 100;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum AuthoredGameplayEffectKind
{
    None,
    MetaIntegerBonus,
    MetaMultiplierDelta,
    GuestDemandMultiplier,
    StockCostMultiplier,
    FacilityShopCostMultiplier,
    BlueprintCostMultiplier,
    ThreatRiseMultiplier,
    WarningThresholdMultiplier,
    IntruderPattern,
    FocusTimeMultiplier,
    RepathIntervalMultiplier,
    FacilityDamageIntervalMultiplier,
    FinalCombatDamageMultiplier
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AuthoredGameplayEffectRecord
{
    public AuthoredGameplayEffectKind kind;
    public string id = string.Empty;
    public string textValue = string.Empty;
    public float numberValue = 1f;
    public StockCategory stockCategory;
    public bool defenseOnly;
}
