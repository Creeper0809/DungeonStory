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
        if (stats == null
            || stats.Stats == null
            || !stats.Stats.TryGetValue(affectedStat, out float value))
        {
            return curve.Evaluate(0.5f);
        }

        return curve.Evaluate(Mathf.Clamp01(value / 100f));
    }
}
