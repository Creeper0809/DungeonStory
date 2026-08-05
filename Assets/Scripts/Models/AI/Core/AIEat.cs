using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.AI
{
    [MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
    public sealed class AIEat
    {
        public static bool CanStart(AiCharacterDecisionSnapshot snapshot) =>
            snapshot.Exists
            && snapshot.HasPersistentIdentity
            && snapshot.HasShopping
            && (!snapshot.HasWorkRole
                || snapshot.IsOffDuty
                || snapshot.HungerUtility > 0f);

        public static AiActionDecision ResolveDestination(
            bool selected,
            bool pending) =>
            AiDecisionMath.ResolveDestination(selected, pending);
    }
}
