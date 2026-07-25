using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/AI/Consideration/FacilityNeed", order = 0)]
public class ConsiderationFacilityNeed : Consideration
{
    [SerializeField] private FacilityRole role;
    [SerializeField, Range(0f, 1f)] private float minimumScoreWhenAvailable = 0.05f;

    public FacilityRole Role
    {
        get => role;
        set => role = value;
    }

    public override float ScoreConsideration(CharacterActor actor)
    {
        if (actor == null || !actor.TryGetAbility(out AbilityShopping shopping))
        {
            return 0f;
        }

        if (shopping.visitCount <= 0
            && !CanEvaluateWithoutVisitBudget(actor, role))
        {
            return 0f;
        }

        GridPathSearchResult searchResult = actor.Brain != null
            ? actor.Brain.GetPathSearch(actor)
            : null;
        if (!FacilityCandidateScorer.HasCandidate(actor, searchResult, role))
        {
            return 0f;
        }

        float needScore = FacilityCandidateScorer.GetNeedScore(actor, role);
        return Mathf.Clamp01(Mathf.Max(minimumScoreWhenAvailable, needScore));
    }

    private static bool CanEvaluateWithoutVisitBudget(CharacterActor actor, FacilityRole role)
    {
        if (!CharacterWorkRoleUtility.TryGetWork(actor, out _))
        {
            return false;
        }

        return (role & FacilityRole.Rest) != 0
            || (role & FacilityRole.Hygiene) != 0
            || (role & FacilityRole.Toilet) != 0;
    }
}
