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
        AbilityShopping shopping = null;
        bool hasShopping = actor != null
            && actor.TryGetAbility(out shopping);
        bool mayIgnoreVisitBudget = hasShopping
            && shopping.visitCount <= 0
            && CanEvaluateWithoutVisitBudget(actor, role);
        bool mayEvaluate = hasShopping
            && (shopping.visitCount > 0 || mayIgnoreVisitBudget);
        bool hasCandidate = mayEvaluate
            && FacilityCandidateScorer.HasCandidate(actor, null, role);
        DungeonStory.AI.AiCharacterDecisionSnapshot snapshot = new(
            AiDecisionSceneSnapshotFactory.CaptureId(actor),
            actor != null,
            hasShopping: hasShopping,
            hasWorkRole: mayIgnoreVisitBudget,
            visitCount: hasShopping ? shopping.visitCount : 0,
            facilityNeed: hasCandidate
                ? FacilityCandidateScorer.GetNeedScore(actor, role)
                : 0f,
            hasCandidate: hasCandidate);
        return DungeonStory.AI.ConsiderationFacilityNeed.Score(
            snapshot,
            minimumScoreWhenAvailable,
            mayIgnoreVisitBudget);
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
