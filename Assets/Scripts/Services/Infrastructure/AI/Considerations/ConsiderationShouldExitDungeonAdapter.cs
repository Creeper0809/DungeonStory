using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/AI/Consideration/ShouldExitDungeon", order = 0)]
public class ConsiderationShouldExitDungeon : Consideration
{
    public override float ScoreConsideration(CharacterActor actor)
    {
        bool hasWork = CharacterWorkRoleUtility.TryGetWork(actor, out _);
        AbilityShopping shopping = null;
        if (!hasWork) actor?.TryGetAbility(out shopping);
        DungeonStory.AI.AiCharacterDecisionSnapshot snapshot = new(
            AiDecisionSceneSnapshotFactory.CaptureId(actor),
            actor != null,
            hasShopping: shopping != null,
            hasWorkRole: hasWork,
            shouldExitDungeon: shopping != null
                && shopping.ShouldExitDungeon());
        return DungeonStory.AI.ConsiderationShouldExitDungeon.Score(snapshot);
    }
}
