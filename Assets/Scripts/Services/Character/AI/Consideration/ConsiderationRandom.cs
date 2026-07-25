using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/AI/Consideration/Random", order = 0)]
public class ConsiderationRandom : Consideration
{
    [SerializeField][Range(0,1)] private float maxNum;
    [SerializeField][Range(0, 1)] private float minNum;
    public override float ScoreConsideration(CharacterActor actor)
    {
        return actor?.Brain != null
            ? Mathf.Clamp01(actor.Brain.NextRandom(minNum, maxNum))
            : 0f;
    }
}
