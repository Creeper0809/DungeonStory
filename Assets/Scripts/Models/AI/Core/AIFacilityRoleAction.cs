using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.AI
{
    [MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
    public sealed class AIFacilityRoleAction
    {
        public static bool CanStart(AiCharacterDecisionSnapshot snapshot) =>
            snapshot.Exists
            && snapshot.HasPersistentIdentity
            && snapshot.HasShopping
            && (!snapshot.HasWorkRole
                || snapshot.IsOffDuty
                || snapshot.FacilityNeed >= 0.1f);

        public static AiActionDecision ResolveDestination(
            bool selected,
            bool pending) =>
            AiDecisionMath.ResolveDestination(selected, pending);
    }
}
