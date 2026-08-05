using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.AI
{
    [MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
    public sealed class AIRest
    {
        public static bool CanStart(AiCharacterDecisionSnapshot snapshot) =>
            snapshot.Exists
            && snapshot.HasPersistentIdentity
            && snapshot.HasShopping
            && (!snapshot.HasWorkRole
                || snapshot.IsOffDuty
                || snapshot.ShouldUseRestProtection
                || snapshot.SleepUtility > 0f
                || snapshot.ExpeditionRecoveryNeed >= 0.1f);

        public static AiActionDecision ResolveDestination(
            bool selected,
            bool pending) =>
            AiDecisionMath.ResolveDestination(selected, pending);
    }
}
