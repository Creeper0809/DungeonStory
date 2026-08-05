using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.AI
{
    [MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
    public sealed class AIWait
    {
        public static bool CanStart(AiCharacterDecisionSnapshot snapshot) =>
            snapshot.Exists
            && snapshot.HasPersistentIdentity
            && snapshot.HasWorkRole;

        public static float AdjustScore(
            AiCharacterDecisionSnapshot snapshot,
            float baseScore,
            float onDutyWorkAvailableScore,
            float offDutyVisitAvailableScore)
        {
            if (!snapshot.HasWorkRole)
                return AiDecisionMath.Clamp01(baseScore);
            if (snapshot.IsOffDuty && snapshot.HasOffDutyVisitCandidate)
                return AiDecisionMath.ScoreAtMost(
                    offDutyVisitAvailableScore,
                    baseScore);
            if (!snapshot.IsOffDuty && snapshot.WorkUtility > 0f)
                return AiDecisionMath.ScoreAtMost(
                    onDutyWorkAvailableScore,
                    baseScore);
            return AiDecisionMath.Clamp01(baseScore);
        }
    }
}
