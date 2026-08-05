using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "DungeonStory/AI/Consideration/Stat", order = 0)]
public class ConsiderationStat : Consideration
{
    [SerializeField] private CharacterCondition affectedStat;
    [SerializeField] private AnimationCurve curve;
    public override float ScoreConsideration(CharacterActor actor)
    {
        CharacterStats stats = actor != null ? actor.Stats : null;
        float value = 0f;
        bool hasStat = stats != null
            && stats.Stats != null
            && stats.Stats.TryGetValue(affectedStat, out value);
        float input = DungeonStory.AI.ConsiderationStat.ResolveCurveInput(
            hasStat,
            value);
        return curve.Evaluate(input);
    }
}
