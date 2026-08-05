using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.AI
{
    [MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
    public sealed class ConsiderationWorkNeed
    {
        public static float Score(
            AiCharacterDecisionSnapshot snapshot,
            float minimumScoreWhenAvailable)
        {
            if (!snapshot.Exists || !snapshot.HasPersistentIdentity
                || !snapshot.HasWorkRole
                || snapshot.WorkUtility <= 0f)
            {
                return 0f;
            }

            return AiDecisionMath.ScoreAtLeast(
                minimumScoreWhenAvailable,
                snapshot.WorkUtility);
        }
    }
}
