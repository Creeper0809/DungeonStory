using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/AI/Consideration/CanLookAround", order = 0)]
public class ConsiderationCanLookAround : Consideration
{
    public override float ScoreConsideration(CharacterActor actor)
    {
        return AILookAround.CanUseVisitLookAround(actor) ? 1f : 0f;
    }
}
