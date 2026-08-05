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
    public List<ShopStockItemSnapshot> items = new List<ShopStockItemSnapshot>();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ShopStockItemSnapshot
{
    public int saleItemId;
    public int amount;
}
