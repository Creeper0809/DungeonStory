using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.AI
{
    [MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
    public sealed class AILookAround
    {
        public const float FallbackScore = 0.05f;

        public static bool CanStart(AiCharacterDecisionSnapshot snapshot) =>
            snapshot.Exists
            && snapshot.HasPersistentIdentity
            && snapshot.HasShopping
            && snapshot.CanLookAround
            && (!snapshot.HasWorkRole || snapshot.IsOffDuty);

        public static float AdjustScore(float baseScore) =>
            AiDecisionMath.ScoreAtMost(FallbackScore, baseScore);

        public static AiActionRequest CreateRequest(
            AiCharacterDecisionSnapshot snapshot,
            float duration) =>
            new(
                snapshot.CharacterId,
                AiActionCommandKind.LookAround,
                duration: duration);
    }
}
