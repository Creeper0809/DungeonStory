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
            ? FacilityWorkTypeMap.GetRequired(definition)
            : FacilityWorkType.None;
    }

    public override float ScoreConsideration(CharacterActor actor)
    {
        AbilityWork work = null;
        bool hasWork = actor != null && actor.TryGetAbility(out work);
        float utilityScore = hasWork
            ? TryGetConfiguredWorkTypeId(out WorkTypeId workTypeId)
                ? work.GetWorkUtilityScore(workTypeId, null)
                : work.GetAnyWorkUtilityScore(null)
            : 0f;
        DungeonStory.AI.AiCharacterDecisionSnapshot snapshot = new(
            AiDecisionSceneSnapshotFactory.CaptureId(actor),
            actor != null,
            hasWorkRole: hasWork,
            workUtility: utilityScore);
        return DungeonStory.AI.ConsiderationWorkNeed.Score(
            snapshot,
            minimumScoreWhenAvailable);
    }

    private bool TryGetConfiguredWorkTypeId(out WorkTypeId workTypeId)
    {
        workTypeId = default;
        if (workType == FacilityWorkType.None)
        {
            return false;
        }

        if (!FacilityWorkTypeMap.TryGet(workType, out WorkTypeDefinition definition))
        {
            return false;
        }

        workTypeId = definition.WorkTypeId;
        return true;
    }
}
