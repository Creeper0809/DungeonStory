using Sirenix.OdinInspector;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[CreateAssetMenu(menuName = "DungeonStory/Building/SaleItem", order = 0)]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public class SaleItem : DataScriptableObject
{
    public string itemName;
    [SerializeField] private string itemDefinitionId = string.Empty;
    public StockCategory category;
    public int cost;
    public Sprite itemSprite;
    public OnBuyItemSO[] buyevent = new OnBuyItemSO[0];

    public string AuthoredItemDefinitionId => itemDefinitionId ?? string.Empty;
    public ItemDefinitionId ItemDefinitionId => new(itemDefinitionId);

#if UNITY_EDITOR
    public void SetItemDefinitionId(string value)
    {
        itemDefinitionId = global::ItemDefinitionId.Normalize(value);
    }
#endif

}
