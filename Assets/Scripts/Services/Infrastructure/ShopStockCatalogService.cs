using System;
using System.Collections.Generic;
using System.Linq;

public interface IShopStockCatalog
{
    bool TryGetStockInfoForShop(int shopId, out StockInfo stockInfo);
    bool TryGetSaleItem(int saleItemId, out SaleItem saleItem);
    StockCategory GetStockCategory(int saleItemId);
}

public sealed class ShopStockCatalog : IShopStockCatalog
{
    private readonly IDataCatalog dataCatalog;
    private readonly IItemDefinitionCatalog itemDefinitions;

    public ShopStockCatalog(
        IDataCatalog dataCatalog,
        IItemDefinitionCatalog itemDefinitions)
    {
        this.dataCatalog = dataCatalog
            ?? throw new ArgumentNullException(nameof(dataCatalog));
        this.itemDefinitions = itemDefinitions
            ?? throw new ArgumentNullException(nameof(itemDefinitions));
        ValidateSaleItems();
    }

    public bool TryGetStockInfoForShop(int shopId, out StockInfo stockInfo)
    {
        IReadOnlyDictionary<int, StockInfo> stockInfos = dataCatalog.GetData<StockInfo>();
        stockInfo = stockInfos.Values.FirstOrDefault((candidate) => candidate != null && candidate.shopId == shopId);
        return stockInfo != null;
    }

    public bool TryGetSaleItem(int saleItemId, out SaleItem saleItem)
    {
        IReadOnlyDictionary<int, SaleItem> saleItems = dataCatalog.GetData<SaleItem>();
        return saleItems.TryGetValue(saleItemId, out saleItem);
    }

    public StockCategory GetStockCategory(int saleItemId)
    {
        return TryGetSaleItem(saleItemId, out SaleItem saleItem)
            ? itemDefinitions.GetRequired(saleItem.ItemDefinitionId).StockCategory
            : StockCategory.General;
    }

    private void ValidateSaleItems()
    {
        foreach (SaleItem saleItem in dataCatalog.GetData<SaleItem>().Values)
        {
            if (saleItem == null)
            {
                throw new InvalidOperationException(
                    "Shop stock catalog contains a null SaleItem.");
            }

            ItemDefinitionId itemId = saleItem.ItemDefinitionId;
            if (!itemId.IsValid
                || !string.Equals(
                    saleItem.AuthoredItemDefinitionId,
                    itemId.Value,
                    StringComparison.Ordinal)
                || saleItem.AuthoredItemDefinitionId.StartsWith(
                    "stock-item:",
                    StringComparison.Ordinal)
                || !itemDefinitions.TryGet(itemId, out ItemDefinitionSO itemDefinition))
            {
                throw new InvalidOperationException(
                    $"SaleItem '{saleItem.name}' ({saleItem.id}) references missing or invalid physical item '{itemId.Value}'.");
            }

            if (saleItem.category != itemDefinition.StockCategory)
            {
                throw new InvalidOperationException(
                    $"SaleItem '{saleItem.name}' ({saleItem.id}) category '{saleItem.category}' does not match physical item '{itemId.Value}' category '{itemDefinition.StockCategory}'.");
            }
        }
    }
}
