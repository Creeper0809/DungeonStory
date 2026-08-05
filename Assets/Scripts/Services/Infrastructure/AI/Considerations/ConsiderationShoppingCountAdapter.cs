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
        DungeonStory.AI.AiCharacterDecisionSnapshot snapshot = new(
            AiDecisionSceneSnapshotFactory.CaptureId(actor),
            actor != null,
            hasShopping: shopping != null,
            visitCount: shopping != null ? shopping.visitCount : 0);
        return DungeonStory.AI.ConsiderationShoppingCount.Score(snapshot);
    }
}
