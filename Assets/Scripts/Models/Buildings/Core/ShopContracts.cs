using System;
using System.Collections.Generic;
using UnityEngine.Scripting.APIUpdating;

public interface IShopServiceSessionCompletionPort
{
    bool TryCompleteSession(string sessionId, out string failureCode);
    void CancelSession(string sessionId, string reason);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct RetailProductSnapshot
{
    public RetailProductSnapshot(int id, string name, int price, int quantity)
    {
        Id = id;
        Name = name ?? string.Empty;
        Price = Math.Max(0, price);
        Quantity = Math.Max(0, quantity);
    }

    public int Id { get; }
    public string Name { get; }
    public int Price { get; }
    public int Quantity { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class RemainStock
{
    public int id;
    public string itemName;
    public int cost;
    public int stock;
    public OnBuyItemSO[] onbuy;

    public RemainStock(
        int id,
        string itemName,
        int cost,
        int stock,
        OnBuyItemSO[] onbuy)
    {
        this.id = id;
        this.itemName = itemName;
        this.cost = cost;
        this.stock = stock;
        this.onbuy = onbuy;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public struct Stock
{
    public int id;
    public int cost;

    public Stock(int id, int cost) : this()
    {
        this.id = id;
        this.cost = cost;
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ShopStockStateSnapshot
{
    public const int CurrentSchemaVersion = 3;

    public int schemaVersion = CurrentSchemaVersion;
    public List<int> activatedAuthoredSaleItemIds = new List<int>();
    public List<string> activeRestockOperationIds = new List<string>();
    public List<RetailStockLotSnapshot> lots = new List<RetailStockLotSnapshot>();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class RetailStockLotSnapshot
{
    public int saleItemId;
    public string itemDefinitionId = string.Empty;
    public string itemInstanceId = string.Empty;
    public string sourceStackId = string.Empty;
    public int quantity;
    public long unitMassGrams;
    public string sourceOperationId = string.Empty;
    public string componentFingerprint = string.Empty;
    public List<RetailStockComponentSnapshot> components =
        new List<RetailStockComponentSnapshot>();

    public RetailStockLotSnapshot Clone() => new RetailStockLotSnapshot
    {
        saleItemId = saleItemId,
        itemDefinitionId = itemDefinitionId ?? string.Empty,
        itemInstanceId = itemInstanceId ?? string.Empty,
        sourceStackId = sourceStackId ?? string.Empty,
        quantity = quantity,
        unitMassGrams = unitMassGrams,
        sourceOperationId = sourceOperationId ?? string.Empty,
        componentFingerprint = componentFingerprint ?? string.Empty,
        components = (components ?? new List<RetailStockComponentSnapshot>())
            .FindAll(component => component != null)
            .ConvertAll(component => component.Clone())
    };
}

[Serializable]
public sealed class RetailStockComponentSnapshot
{
    public string componentTypeId = string.Empty;
    public int schemaVersion = 1;
    public bool affectsStacking = true;
    public List<RetailStockComponentValueSnapshot> values =
        new List<RetailStockComponentValueSnapshot>();

    public RetailStockComponentSnapshot Clone() => new RetailStockComponentSnapshot
    {
        componentTypeId = componentTypeId ?? string.Empty,
        schemaVersion = schemaVersion,
        affectsStacking = affectsStacking,
        values = (values ?? new List<RetailStockComponentValueSnapshot>())
            .FindAll(value => value != null)
            .ConvertAll(value => value.Clone())
    };
}

[Serializable]
public sealed class RetailStockComponentValueSnapshot
{
    public string key = string.Empty;
    public int kind;
    public string stringValue = string.Empty;
    public long integerValue;
    public double decimalValue;
    public bool booleanValue;

    public RetailStockComponentValueSnapshot Clone() =>
        new RetailStockComponentValueSnapshot
        {
            key = key ?? string.Empty,
            kind = kind,
            stringValue = stringValue ?? string.Empty,
            integerValue = integerValue,
            decimalValue = decimalValue,
            booleanValue = booleanValue
        };
}
