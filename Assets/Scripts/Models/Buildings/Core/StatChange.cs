using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, null, "Assembly-CSharp", "StatChange")]
[CreateAssetMenu(menuName = "DungeonStory/Item/On Buy/Need Change", order = 0)]
public class StatChange : OnBuyItemSO
{
    [CharacterNeedId]
    public string needId = "need:hunger";
    public int value;

    public override void Onbuy(IBuildingVisitorPort actor)
    {
        actor?.ApplyNeedDelta(needId, value);
    }
}

public sealed class CharacterNeedIdAttribute : PropertyAttribute
{
}
