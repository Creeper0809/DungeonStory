using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/AI/Consideration/ShoppingCount", order = 0)]
public class ConsiderationShoppingCount : Consideration
{
    public override float ScoreConsideration(CharacterActor actor)
    {
        AbilityShopping shopping = null;
        actor?.TryGetAbility(out shopping);
        if (shopping == null || shopping.visitCount <= 0)
        {
            return 0f;
        }

        return shopping.visitCount * 0.4f;
    }
}
