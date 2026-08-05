using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.AI
{
    [MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
    public sealed class AIShopping
    {
        public static bool CanStart(AiCharacterDecisionSnapshot snapshot) =>
            snapshot.Exists
            && snapshot.HasPersistentIdentity
            && snapshot.HasShopping
            && (!snapshot.HasWorkRole || snapshot.IsOffDuty);

        public static AiActionDecision ResolveDestination(
            bool supported,
            bool selected,
            bool pending) =>
            AiDecisionMath.ResolveDestination(selected, pending, supported);
    }
}
