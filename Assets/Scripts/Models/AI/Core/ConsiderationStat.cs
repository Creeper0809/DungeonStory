using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.AI
{
    [MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
    public sealed class ConsiderationStat
    {
        public static float ResolveCurveInput(
            bool hasStat,
            float statValue) =>
            hasStat
                ? AiDecisionMath.Clamp01(statValue / 100f)
                : 0.5f;
    }
}
