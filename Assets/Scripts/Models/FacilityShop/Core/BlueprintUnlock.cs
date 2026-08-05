using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class BlueprintUnlockTypeIds
{
    public const string Building = "blueprint.building";
    public const string BasicPurchase = "blueprint.basic-purchase";
    public const string Recipe = "blueprint.recipe";
}

[AttributeUsage(AttributeTargets.Class, Inherited = false)]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class BlueprintUnlockDisplayNameAttribute : Attribute
{
    public BlueprintUnlockDisplayNameAttribute(string displayName)
    {
        DisplayName = displayName;
    }

    public string DisplayName { get; }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public abstract class BlueprintUnlock
{
    public abstract string UnlockTypeId { get; }
    public abstract bool IsConfigured { get; }
    public abstract BlueprintUnlockRecord Apply(BlueprintUnlockContext context);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public interface IBlueprintBuildingUnlock
{
    int BuildingId { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class BlueprintUnlockContext
{
    private readonly Func<int, BlueprintUnlockRecord> unlockBuilding;
    private readonly Func<int, BlueprintUnlockRecord> unlockBasicPurchase;
    private readonly Func<string, BlueprintUnlockRecord> unlockRecipe;

    public BlueprintUnlockContext(
        Func<int, BlueprintUnlockRecord> unlockBuilding,
        Func<int, BlueprintUnlockRecord> unlockBasicPurchase,
        Func<string, BlueprintUnlockRecord> unlockRecipe)
    {
        this.unlockBuilding = unlockBuilding
            ?? throw new ArgumentNullException(nameof(unlockBuilding));
        this.unlockBasicPurchase = unlockBasicPurchase
            ?? throw new ArgumentNullException(nameof(unlockBasicPurchase));
        this.unlockRecipe = unlockRecipe
            ?? throw new ArgumentNullException(nameof(unlockRecipe));
    }

    public BlueprintUnlockRecord UnlockBuilding(int buildingId) =>
        unlockBuilding(buildingId);
    public BlueprintUnlockRecord UnlockBasicPurchase(int buildingId) =>
        unlockBasicPurchase(buildingId);
    public BlueprintUnlockRecord UnlockRecipe(string recipeId) =>
        unlockRecipe(recipeId);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct BlueprintUnlockRecord
{
    public BlueprintUnlockRecord(
        string unlockTypeId,
        string categoryLabel,
        string valueId,
        string displayName,
        UnityEngine.Object facility = null,
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
    public UnityEngine.Object Facility { get; }
    public string CodexDetail { get; }
    public bool IsApplied => !string.IsNullOrWhiteSpace(UnlockTypeId);
}

[Serializable]
[BlueprintUnlockDisplayName("시설 해금")]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class BlueprintBuildingUnlock : BlueprintUnlock, IBlueprintBuildingUnlock
{
    [Min(0), InspectorName("시설 ID")] public int buildingId;

    public override string UnlockTypeId => BlueprintUnlockTypeIds.Building;
    public override bool IsConfigured => buildingId >= 0;
    public int BuildingId => buildingId;

    public override BlueprintUnlockRecord Apply(BlueprintUnlockContext context)
    {
        return context?.UnlockBuilding(buildingId) ?? default;
    }
}

[Serializable]
[BlueprintUnlockDisplayName("기본 구매 해금")]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class BlueprintBasicPurchaseUnlock : BlueprintUnlock, IBlueprintBuildingUnlock
{
    [Min(0), InspectorName("시설 ID")] public int buildingId;

    public override string UnlockTypeId => BlueprintUnlockTypeIds.BasicPurchase;
    public override bool IsConfigured => buildingId >= 0;
    public int BuildingId => buildingId;

    public override BlueprintUnlockRecord Apply(BlueprintUnlockContext context)
    {
        return context?.UnlockBasicPurchase(buildingId) ?? default;
    }
}

[Serializable]
[BlueprintUnlockDisplayName("조합식 해금")]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class BlueprintRecipeUnlock : BlueprintUnlock
{
    [InspectorName("조합식 ID")] public string recipeId;

    public override string UnlockTypeId => BlueprintUnlockTypeIds.Recipe;
    public override bool IsConfigured => !string.IsNullOrWhiteSpace(recipeId);

    public override BlueprintUnlockRecord Apply(BlueprintUnlockContext context)
    {
        return context?.UnlockRecipe(recipeId) ?? default;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
