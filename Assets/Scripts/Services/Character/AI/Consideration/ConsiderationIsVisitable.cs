using UnityEngine;
[CreateAssetMenu(menuName = "DungeonStory/AI/Consideration/Visitable", order = 0)]
public class ConsiderationIsVisitable : Consideration
{
    [SerializeField] private FacilityRole role = FacilityRole.None;
    public override float ScoreConsideration(CharacterActor actor)
    {
        AbilityShopping shopping = null;
        actor?.TryGetAbility(out shopping);
        if (shopping == null || shopping.visitCount <= 0)
        {
            return 0f;
        }

        if (role != FacilityRole.None)
        {
            return FacilityCandidateScorer.HasCandidate(actor, null, role) ? 1f : 0f;
        }

        FacilityRole visitorRoles = shopping.GetInterestRoles()
            | FacilityRole.Meal
            | FacilityRole.Rest;
        return FacilityCandidateScorer.HasCandidate(actor, null, visitorRoles)
            ? 1f
            : 0f;
    }
}
