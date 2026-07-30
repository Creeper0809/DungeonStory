using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class DungeonItemDefinition
{
    private static readonly ICombatEquipmentCatalog DefaultCombatEquipmentCatalog =
        new ResourceCombatEquipmentCatalog();

    [SerializeField] private string itemId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [SerializeField] private string description = string.Empty;
    [SerializeField] private StockCategory stockCategory = StockCategory.General;
    [SerializeField] private int unitPrice = 1;
    [SerializeField] private Sprite sprite;
    [SerializeField] private float unitWeight = 1f;
    [SerializeField] private int maxStack = 75;
    [SerializeField] private string equipmentId = string.Empty;

    public string ItemId => itemId?.Trim() ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName) ? ItemId : displayName.Trim();
    public string Description => description?.Trim() ?? string.Empty;
    public StockCategory StockCategory => stockCategory;
    public int UnitPrice => Mathf.Max(0, unitPrice);
    public Sprite Sprite => sprite;
    public float UnitWeight => Mathf.Max(0.01f, unitWeight);
    public int MaxStack => Mathf.Max(1, maxStack);
    public string EquipmentId => equipmentId?.Trim() ?? string.Empty;

    public DungeonItemDefinition()
    {
    }

    public DungeonItemDefinition(
        string itemId,
        string displayName,
        string description,
        StockCategory stockCategory,
        int unitPrice,
        Sprite sprite,
        float unitWeight,
        int maxStack,
        string equipmentId = "")
    {
        this.itemId = itemId;
        this.displayName = displayName;
        this.description = description;
        this.stockCategory = stockCategory;
        this.unitPrice = Mathf.Max(0, unitPrice);
        this.sprite = sprite;
        this.unitWeight = Mathf.Max(0.01f, unitWeight);
        this.maxStack = Mathf.Max(1, maxStack);
        this.equipmentId = equipmentId ?? string.Empty;
    }

    public static DungeonItemDefinition FromStockCategory(StockCategory category)
    {
        StockCategoryCatalog.TryGet(category, out StockCategoryDefinition stockDefinition);
        string itemId = DungeonItemCatalogSO.StockItemId(category);
        string name = stockDefinition != null ? stockDefinition.DisplayName : category.ToString();
        int price = stockDefinition != null ? Mathf.Max(1, stockDefinition.DailyUnitCost) : 1;
        float unitWeight = category switch
        {
            StockCategory.Food => 0.55f,
            StockCategory.Water => 1.0f,
            StockCategory.Medicine => 0.25f,
            StockCategory.Weapon => 2.25f,
            StockCategory.Ammunition => 0.08f,
            StockCategory.Fuel => 0.8f,
            StockCategory.Mana => 0.35f,
            StockCategory.Biological => 0.5f,
            StockCategory.Knowledge => 0.2f,
            StockCategory.Blueprint => 0.15f,
            _ => 1f
        };

        return new DungeonItemDefinition(
            itemId,
            name,
            $"{name} 재고",
            category,
            price,
            null,
            unitWeight,
            75);
    }

    public static DungeonItemDefinition FromEquipmentId(string equipmentId)
    {
        string normalized = equipmentId?.Trim() ?? string.Empty;
        if (DefaultCombatEquipmentCatalog.TryGet(
            normalized,
            out CombatEquipmentDefinitionSO combatDefinition))
        {
            StockCategory category = combatDefinition.Kind is CombatEquipmentKind.RangedWeapon
                or CombatEquipmentKind.RecoverableThrowingWeapon
                ? StockCategory.Weapon
                : StockCategory.Weapon;
            return new DungeonItemDefinition(
                DungeonItemCatalogSO.EquipmentItemId(normalized),
                combatDefinition.DisplayName,
                combatDefinition.Description,
                category,
                Mathf.Max(1, Mathf.RoundToInt(combatDefinition.Weight * 18f)),
                null,
                Mathf.Max(0.1f, combatDefinition.Weight),
                1,
                normalized);
        }

        string displayName = string.IsNullOrWhiteSpace(normalized)
            ? "Equipment"
            : normalized.Replace("weapon:", string.Empty)
                .Replace("armor:", string.Empty)
                .Replace('-', ' ');
        return new DungeonItemDefinition(
            DungeonItemCatalogSO.EquipmentItemId(normalized),
            displayName,
            "Crafted expedition equipment",
            StockCategory.Weapon,
            0,
            null,
            3.5f,
            1,
            normalized);
    }
}

[CreateAssetMenu(menuName = "DungeonStory/Items/Dungeon Item Catalog", order = 0)]
public sealed class DungeonItemCatalogSO : ScriptableObject
{
    public const string ResourcePath = "SO/Items/DungeonItemCatalog";
    private const string StockPrefix = "stock-item:";
    private const string EquipmentPrefix = "equipment-item:";

    [SerializeField] private List<DungeonItemDefinition> items = new List<DungeonItemDefinition>();

    public IReadOnlyList<DungeonItemDefinition> Items => items;

    public bool TryGetDefinition(string itemId, out DungeonItemDefinition definition)
    {
        string normalized = itemId?.Trim() ?? string.Empty;
        definition = items.FirstOrDefault(item => item != null
            && string.Equals(item.ItemId, normalized, StringComparison.Ordinal));
        if (definition != null)
        {
            return true;
        }

        if (SurvivalItemDefinitions.TryGetDefinition(normalized, out definition))
        {
            return true;
        }

        if (WildlifeItemDefinitions.TryGetDefinition(normalized, out definition))
        {
            return true;
        }

        if (DarkSurvivalItemDefinitions.TryGetDefinition(normalized, out definition))
        {
            return true;
        }

        if (CombatItemDefinitions.TryGetDefinition(normalized, out definition))
        {
            return true;
        }

        if (CaptivityItemDefinitions.TryGetDefinition(normalized, out definition))
        {
            return true;
        }

        if (EvolutionCatalystItemDefinitions.TryGetDefinition(
                normalized,
                out definition))
        {
            return true;
        }

        if (SurgeryItemDefinitions.TryGetDefinition(normalized, out definition))
        {
            return true;
        }

        if (IndustrialItemDefinitions.TryGetDefinition(
                normalized,
                out definition))
        {
            return true;
        }

        if (TryGetStockCategoryFromItemId(normalized, out StockCategory category))
        {
            definition = DungeonItemDefinition.FromStockCategory(category);
            return true;
        }

        if (TryGetEquipmentIdFromItemId(normalized, out string equipmentId))
        {
            definition = DungeonItemDefinition.FromEquipmentId(equipmentId);
            return true;
        }

        definition = null;
        return false;
    }

    public DungeonItemDefinition GetDefinitionOrDefault(string itemId)
    {
        return TryGetDefinition(itemId, out DungeonItemDefinition definition)
            ? definition
            : new DungeonItemDefinition(
                string.IsNullOrWhiteSpace(itemId) ? "item:unknown" : itemId.Trim(),
                string.IsNullOrWhiteSpace(itemId) ? "Unknown Item" : itemId.Trim(),
                string.Empty,
                StockCategory.General,
                1,
                null,
                1f,
                75);
    }

    public DungeonItemDefinition GetStockDefinition(StockCategory category)
    {
        string itemId = StockItemId(category);
        return TryGetDefinition(itemId, out DungeonItemDefinition definition)
            ? definition
            : DungeonItemDefinition.FromStockCategory(category);
    }

    public static string StockItemId(StockCategory category)
    {
        return StockPrefix + Convert.ToInt32(category, CultureInfo.InvariantCulture)
            .ToString(CultureInfo.InvariantCulture);
    }

    public static string EquipmentItemId(string equipmentId)
    {
        return EquipmentPrefix + (equipmentId?.Trim() ?? string.Empty);
    }

    public static bool TryGetStockCategoryFromItemId(string itemId, out StockCategory category)
    {
        string normalized = itemId?.Trim() ?? string.Empty;
        if (normalized.StartsWith(StockPrefix, StringComparison.Ordinal)
            && int.TryParse(
                normalized.Substring(StockPrefix.Length),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int numeric))
        {
            category = (StockCategory)numeric;
            return Enum.IsDefined(typeof(StockCategory), category);
        }

        return StockCategoryPersistenceId.TryParse(normalized, out category)
            || SurvivalItemDefinitions.TryGetStockCategory(normalized, out category);
    }

    public static bool TryGetEquipmentIdFromItemId(string itemId, out string equipmentId)
    {
        string normalized = itemId?.Trim() ?? string.Empty;
        if (normalized.StartsWith(EquipmentPrefix, StringComparison.Ordinal))
        {
            equipmentId = normalized.Substring(EquipmentPrefix.Length).Trim();
            return !string.IsNullOrWhiteSpace(equipmentId);
        }

        equipmentId = string.Empty;
        return false;
    }
}

public interface IDungeonItemCatalogProvider
{
    DungeonItemCatalogSO Catalog { get; }
    DungeonItemDefinition GetDefinition(string itemId);
    DungeonItemDefinition GetDefinition(StockCategory category);
    bool TryGetDefinition(string itemId, out DungeonItemDefinition definition);
}

public sealed class ResourceDungeonItemCatalogProvider : IDungeonItemCatalogProvider
{
    private readonly IResourcesAssetLoader resourcesAssetLoader;
    private readonly IFacilityShopCatalog facilityShopCatalog;
    private readonly IResourceEconomyContentCatalog resourceEconomyCatalog;
    private DungeonItemCatalogSO catalog;

    public ResourceDungeonItemCatalogProvider(
        IResourcesAssetLoader resourcesAssetLoader = null,
        IFacilityShopCatalog facilityShopCatalog = null,
        IResourceEconomyContentCatalog resourceEconomyCatalog = null)
    {
        this.resourcesAssetLoader = resourcesAssetLoader ?? new UnityResourcesAssetLoader();
        this.facilityShopCatalog = facilityShopCatalog;
        this.resourceEconomyCatalog = resourceEconomyCatalog;
    }

    public DungeonItemCatalogSO Catalog
    {
        get
        {
            if (catalog == null)
            {
                catalog = resourcesAssetLoader.LoadOptional<DungeonItemCatalogSO>(
                    DungeonItemCatalogSO.ResourcePath);
            }

            return catalog;
        }
    }

    public DungeonItemDefinition GetDefinition(string itemId)
    {
        if (resourceEconomyCatalog != null
            && resourceEconomyCatalog.TryGetItem(
                itemId,
                out ResourceItemDefinitionSO economyDefinition))
        {
            return economyDefinition.ToDungeonItemDefinition();
        }

        if (ResearchBlueprintItemDefinitions.TryGetDefinition(
                itemId,
                facilityShopCatalog,
                out DungeonItemDefinition blueprintDefinition))
        {
            return blueprintDefinition;
        }

        if (FacilityInstallationKitItemIds.TryGetBuildingId(
                itemId,
                out int kitBuildingId))
        {
            BuildingSO building =
                facilityShopCatalog?.FindBuildingById(kitBuildingId);
            string buildingName = building != null
                ? FacilityShopService.GetBuildingName(building)
                : $"시설 {kitBuildingId}";
            return new DungeonItemDefinition(
                FacilityInstallationKitItemIds.ForBuilding(kitBuildingId),
                $"{buildingName} 설치 키트",
                "외부 시장에서 조달한 시설 부품 묶음. 공사 현장까지 직접 운반해야 한다.",
                StockCategory.General,
                building != null
                    ? Mathf.Max(1, building.GetConstructionValue())
                    : 1,
                null,
                8f,
                1);
        }

        if (Catalog != null)
        {
            return Catalog.GetDefinitionOrDefault(itemId);
        }

        if (SurvivalItemDefinitions.TryGetDefinition(itemId, out DungeonItemDefinition survivalDefinition))
        {
            return survivalDefinition;
        }

        if (WildlifeItemDefinitions.TryGetDefinition(itemId, out DungeonItemDefinition wildlifeDefinition))
        {
            return wildlifeDefinition;
        }

        if (DarkSurvivalItemDefinitions.TryGetDefinition(itemId, out DungeonItemDefinition darkSurvivalDefinition))
        {
            return darkSurvivalDefinition;
        }

        if (CaptivityItemDefinitions.TryGetDefinition(itemId, out DungeonItemDefinition captivityDefinition))
        {
            return captivityDefinition;
        }

        if (EvolutionCatalystItemDefinitions.TryGetDefinition(
                itemId,
                out DungeonItemDefinition evolutionDefinition))
        {
            return evolutionDefinition;
        }

        if (SurgeryItemDefinitions.TryGetDefinition(
                itemId,
                out DungeonItemDefinition surgeryDefinition))
        {
            return surgeryDefinition;
        }

        return DungeonItemCatalogSO.TryGetStockCategoryFromItemId(itemId, out StockCategory category)
            ? DungeonItemDefinition.FromStockCategory(category)
            : new DungeonItemDefinition(itemId, itemId, string.Empty, StockCategory.General, 1, null, 1f, 75);
    }

    public DungeonItemDefinition GetDefinition(StockCategory category)
    {
        return Catalog != null
            ? Catalog.GetStockDefinition(category)
            : DungeonItemDefinition.FromStockCategory(category);
    }

    public bool TryGetDefinition(string itemId, out DungeonItemDefinition definition)
    {
        definition = GetDefinition(itemId);
        return definition != null;
    }
}
