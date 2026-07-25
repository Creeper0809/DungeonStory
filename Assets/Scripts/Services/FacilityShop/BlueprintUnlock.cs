using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class BlueprintUnlockTypeIds
{
    public const string Building = "blueprint.building";
    public const string BasicPurchase = "blueprint.basic-purchase";
    public const string Recipe = "blueprint.recipe";
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class BlueprintUnlockDisplayNameAttribute : Attribute
{
    public BlueprintUnlockDisplayNameAttribute(string displayName)
    {
        DisplayName = displayName;
    }

    public string DisplayName { get; }
}

[Serializable]
public sealed class BlueprintUnlockCollection
{
    [SerializeReference, SerializeField]
    private List<BlueprintUnlock> items = new List<BlueprintUnlock>();
    [NonSerialized] private IReadOnlyList<BlueprintUnlock> itemsView;

    public IReadOnlyList<BlueprintUnlock> Items
    {
        get
        {
            items ??= new List<BlueprintUnlock>();
            return itemsView ??= ReadOnlyView.List(items);
        }
    }
    public int Count => items?.Count ?? 0;

    public void Add(BlueprintUnlock unlock)
    {
        if (unlock == null)
        {
            return;
        }

        items ??= new List<BlueprintUnlock>();
        items.Add(unlock);
    }

    public int RemoveNullEntries()
    {
        return items?.RemoveAll(unlock => unlock == null) ?? 0;
    }
}

[Serializable]
public abstract class BlueprintUnlock
{
    public abstract string UnlockTypeId { get; }
    public abstract bool IsConfigured { get; }
    public abstract BlueprintUnlockRecord Apply(BlueprintUnlockContext context);
}

public interface IBlueprintBuildingUnlock
{
    int BuildingId { get; }
}

public sealed class BlueprintUnlockContext
{
    public BlueprintUnlockContext(
        BlueprintResearchState researchState,
        FacilityShopUnlockState shopUnlockState,
        IFacilityShopCatalog facilityShopCatalog)
    {
        ResearchState = researchState;
        ShopUnlockState = shopUnlockState;
        FacilityShopCatalog = facilityShopCatalog
            ?? throw new ArgumentNullException(nameof(facilityShopCatalog));
    }

    public BlueprintResearchState ResearchState { get; }
    public FacilityShopUnlockState ShopUnlockState { get; }
    public IFacilityShopCatalog FacilityShopCatalog { get; }

    public BuildingSO FindBuilding(int buildingId)
    {
        return FacilityShopService.FindBuildingById(FacilityShopCatalog, buildingId);
    }
}

public readonly struct BlueprintUnlockRecord
{
    public BlueprintUnlockRecord(
        string unlockTypeId,
        string categoryLabel,
        string valueId,
        string displayName,
        BuildingSO facility = null,
        string codexDetail = null)
    {
        UnlockTypeId = unlockTypeId ?? string.Empty;
        CategoryLabel = categoryLabel ?? string.Empty;
        ValueId = valueId ?? string.Empty;
        DisplayName = displayName ?? string.Empty;
        Facility = facility;
        CodexDetail = codexDetail ?? string.Empty;
    }

    public string UnlockTypeId { get; }
    public string CategoryLabel { get; }
    public string ValueId { get; }
    public string DisplayName { get; }
    public BuildingSO Facility { get; }
    public string CodexDetail { get; }
    public bool IsApplied => !string.IsNullOrWhiteSpace(UnlockTypeId);
}

[Serializable]
[BlueprintUnlockDisplayName("시설 해금")]
public sealed class BlueprintBuildingUnlock : BlueprintUnlock, IBlueprintBuildingUnlock
{
    [Min(0), InspectorName("시설 ID")] public int buildingId;

    public override string UnlockTypeId => BlueprintUnlockTypeIds.Building;
    public override bool IsConfigured => buildingId >= 0;
    public int BuildingId => buildingId;

    public override BlueprintUnlockRecord Apply(BlueprintUnlockContext context)
    {
        BuildingSO building = context?.FindBuilding(buildingId);
        if (building == null)
        {
            return default;
        }

        if (context.ResearchState == null || !context.ResearchState.UnlockBuilding(building.id))
        {
            return default;
        }

        return new BlueprintUnlockRecord(
            UnlockTypeId,
            "시설 해금",
            buildingId.ToString(),
            FacilityShopService.GetBuildingName(building),
            building);
    }
}

[Serializable]
[BlueprintUnlockDisplayName("기본 구매 해금")]
public sealed class BlueprintBasicPurchaseUnlock : BlueprintUnlock, IBlueprintBuildingUnlock
{
    [Min(0), InspectorName("시설 ID")] public int buildingId;

    public override string UnlockTypeId => BlueprintUnlockTypeIds.BasicPurchase;
    public override bool IsConfigured => buildingId >= 0;
    public int BuildingId => buildingId;

    public override BlueprintUnlockRecord Apply(BlueprintUnlockContext context)
    {
        BuildingSO building = context?.FindBuilding(buildingId);
        if (building == null
            || context.ShopUnlockState == null
            || !context.ShopUnlockState.UnlockBasicPurchase(building))
        {
            return default;
        }

        return new BlueprintUnlockRecord(
            UnlockTypeId,
            "기본 구매",
            buildingId.ToString(),
            FacilityShopService.GetBuildingName(building),
            building,
            "기본 구매: 연구 완료 후 구매 가능");
    }
}

[Serializable]
[BlueprintUnlockDisplayName("조합식 해금")]
public sealed class BlueprintRecipeUnlock : BlueprintUnlock
{
    [InspectorName("조합식 ID")] public string recipeId;

    public override string UnlockTypeId => BlueprintUnlockTypeIds.Recipe;
    public override bool IsConfigured => !string.IsNullOrWhiteSpace(recipeId);

    public override BlueprintUnlockRecord Apply(BlueprintUnlockContext context)
    {
        if (context?.ResearchState == null || !context.ResearchState.UnlockRecipe(recipeId))
        {
            return default;
        }

        return new BlueprintUnlockRecord(
            UnlockTypeId,
            "조합식",
            recipeId,
            recipeId);
    }
}

public readonly struct BlueprintResearchUnlockResult
{
    public BlueprintResearchUnlockResult(
        FacilityBlueprintSO blueprint,
        IReadOnlyList<BlueprintUnlockRecord> unlocks)
    {
        Blueprint = blueprint;
        Unlocks = EventPayloadSnapshot.Copy(unlocks);
    }

    public FacilityBlueprintSO Blueprint { get; }
    public IReadOnlyList<BlueprintUnlockRecord> Unlocks { get; }

    public IReadOnlyList<string> UnlockedBuildings => GetDisplayNames(BlueprintUnlockTypeIds.Building);
    public IReadOnlyList<string> UnlockedBasicPurchases => GetDisplayNames(BlueprintUnlockTypeIds.BasicPurchase);
    public IReadOnlyList<string> UnlockedRecipes => GetValueIds(BlueprintUnlockTypeIds.Recipe);

    public IReadOnlyList<string> FormatSummaryLines()
    {
        return (Unlocks ?? Array.Empty<BlueprintUnlockRecord>())
            .Where(unlock => unlock.IsApplied && !string.IsNullOrWhiteSpace(unlock.CategoryLabel))
            .GroupBy(unlock => unlock.CategoryLabel)
            .Select(group => $"{group.Key}: {string.Join(", ", group.Select(unlock => unlock.DisplayName))}")
            .ToArray();
    }

    private IReadOnlyList<string> GetDisplayNames(string unlockTypeId)
    {
        return (Unlocks ?? Array.Empty<BlueprintUnlockRecord>())
            .Where(unlock => unlock.IsApplied && unlock.UnlockTypeId == unlockTypeId)
            .Select(unlock => unlock.DisplayName)
            .ToArray();
    }

    private IReadOnlyList<string> GetValueIds(string unlockTypeId)
    {
        return (Unlocks ?? Array.Empty<BlueprintUnlockRecord>())
            .Where(unlock => unlock.IsApplied && unlock.UnlockTypeId == unlockTypeId)
            .Select(unlock => unlock.ValueId)
            .ToArray();
    }
}
