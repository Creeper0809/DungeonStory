using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.AI
{
    [MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
    public sealed class ConsiderationRandom
    {
        public static float Score(bool hasRandomStream, float sampledValue) =>
            hasRandomStream ? AiDecisionMath.Clamp01(sampledValue) : 0f;
    }
}
