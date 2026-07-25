using System;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/Item/On Buy/Need Change", order = 0)]
public class StatChange : OnBuyItemSO
{
    [CharacterNeedId]
    public string needId = "need:hunger";
    public int value;

    public override void Onbuy(CharacterActor actor)
    {
        if (actor == null)
        {
            return;
        }

        if (!CharacterNeedCatalog.TryGet(needId, out CharacterNeedDefinition definition))
        {
            throw new InvalidOperationException(
                $"Purchase effect '{name}' targets unknown character need '{needId}'.");
        }

        actor.ChangesStat(definition.Condition, value);
    }
}

public sealed class CharacterNeedIdAttribute : PropertyAttribute
{
}
