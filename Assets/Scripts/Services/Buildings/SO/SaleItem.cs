using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Building/SaleItem", order = 0)]
public class SaleItem : DataScriptableObject
{
    public string itemName;
    public StockCategory category;
    public int cost;
    public Sprite itemSprite;
    public OnBuyItemSO[] buyevent = new OnBuyItemSO[0];
}
