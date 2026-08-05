using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonItemDefinition
{
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
}
