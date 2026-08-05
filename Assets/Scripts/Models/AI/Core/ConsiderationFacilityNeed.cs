using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.AI
{
    [MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
    public sealed class ConsiderationFacilityNeed
    {
        public static float Score(
            AiCharacterDecisionSnapshot snapshot,
            float minimumScoreWhenAvailable,
            bool mayIgnoreVisitBudget)
        {
            if (!snapshot.Exists || !snapshot.HasPersistentIdentity
                || !snapshot.HasShopping
                || snapshot.VisitCount <= 0 && !mayIgnoreVisitBudget
                || !snapshot.HasCandidate)
            {
                return 0f;
            }

            return AiDecisionMath.ScoreAtLeast(
                minimumScoreWhenAvailable,
                snapshot.FacilityNeed);
        }
    }
}
