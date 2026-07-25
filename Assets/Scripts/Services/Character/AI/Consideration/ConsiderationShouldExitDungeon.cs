using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/AI/Consideration/ShouldExitDungeon", order = 0)]
public class ConsiderationShouldExitDungeon : Consideration
{
    public override float ScoreConsideration(CharacterActor actor)
    {
        if (CharacterWorkRoleUtility.TryGetWork(actor, out _))
        {
            return 0f;
        }

        AbilityShopping shopping = null;
        actor?.TryGetAbility(out shopping);
        return shopping != null && shopping.ShouldExitDungeon() ? 1f : 0f;
    }
}
