using UnityEngine;
[CreateAssetMenu(menuName = "DungeonStory/AI/Consideration/Visitable", order = 0)]
public class ConsiderationIsVisitable : Consideration
{
    [SerializeField] private FacilityRole role = FacilityRole.None;
    public override float ScoreConsideration(CharacterActor actor)
    {
        AbilityShopping shopping = null;
        actor?.TryGetAbility(out shopping);
        bool mayEvaluate = shopping != null && shopping.visitCount > 0;
        FacilityRole visitorRoles = role != FacilityRole.None
            ? role
            : shopping != null
                ? shopping.GetInterestRoles()
                    | FacilityRole.Meal
                    | FacilityRole.Rest
                : FacilityRole.None;
        DungeonStory.AI.AiCharacterDecisionSnapshot snapshot = new(
            AiDecisionSceneSnapshotFactory.CaptureId(actor),
            actor != null,
            hasShopping: shopping != null,
            visitCount: shopping != null ? shopping.visitCount : 0,
            hasCandidate: mayEvaluate
                && FacilityCandidateScorer.HasCandidate(
                    actor,
                    null,
                    visitorRoles));
        return DungeonStory.AI.ConsiderationIsVisitable.Score(snapshot);
    }
}
