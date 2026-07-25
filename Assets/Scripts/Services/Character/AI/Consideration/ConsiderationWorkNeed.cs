using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/AI/Consideration/WorkNeed", order = 0)]
public class ConsiderationWorkNeed : Consideration
{
    [SerializeField] private FacilityWorkType workType = FacilityWorkType.None;
    [SerializeField, Range(0f, 1f)] private float minimumScoreWhenAvailable = 0.05f;

    public WorkTypeId WorkTypeId
    {
        get => TryGetConfiguredWorkTypeId(out WorkTypeId workTypeId)
            ? workTypeId
            : default;
        set => workType = WorkTypeCatalog.TryGet(value, out WorkTypeDefinition definition)
            ? definition.Type
            : FacilityWorkType.None;
    }

    public override float ScoreConsideration(CharacterActor actor)
    {
        if (actor == null || !actor.TryGetAbility(out AbilityWork work))
        {
            return 0f;
        }

        GridPathSearchResult searchResult = actor.Brain != null
            ? actor.Brain.GetPathSearch(actor)
            : null;
        float utilityScore = TryGetConfiguredWorkTypeId(out WorkTypeId workTypeId)
            ? work.GetWorkUtilityScore(workTypeId, searchResult)
            : work.GetAnyWorkUtilityScore(searchResult);
        if (utilityScore <= 0f)
        {
            return 0f;
        }

        return Mathf.Clamp01(Mathf.Max(minimumScoreWhenAvailable, utilityScore));
    }

    private bool TryGetConfiguredWorkTypeId(out WorkTypeId workTypeId)
    {
        workTypeId = default;
        if (workType == FacilityWorkType.None)
        {
            return false;
        }

        if (!WorkTypeCatalog.TryGet(workType, out WorkTypeDefinition definition))
        {
            return false;
        }

        workTypeId = definition.WorkTypeId;
        return true;
    }
}
