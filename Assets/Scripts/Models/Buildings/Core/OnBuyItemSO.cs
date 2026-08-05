using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, null, "Assembly-CSharp", "OnBuyItemSO")]
public class OnBuyItemSO : ScriptableObject
{
    public virtual void Onbuy(IBuildingVisitorPort actor)
    {
    }
}
