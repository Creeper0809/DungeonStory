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

    public ShopStockCatalog(IDataCatalog dataCatalog)
    {
        this.dataCatalog = dataCatalog
            ?? throw new ArgumentNullException(nameof(dataCatalog));
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
            ? saleItem.category
            : StockCategory.General;
    }
}
