using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.AI
{
    [MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
    public sealed class AIDeprivationBreakdowns
    {
        public static AiActionDecision Evaluate(
            AiCharacterDecisionSnapshot snapshot,
            int breakdownKind)
        {
            if (!snapshot.Exists || !snapshot.HasPersistentIdentity
                || !snapshot.HasDeprivationBreakdown)
            {
                return AiActionDecision.Reject(AIActionFailureKind.CannotStart);
            }

            return AiActionDecision.Allow(new AiActionRequest(
                snapshot.CharacterId,
                AiActionCommandKind.BeginBreakdown,
                argument: breakdownKind));
        }

        public static AiActionRequest CreateRequest(
            AiCharacterDecisionSnapshot snapshot,
            int breakdownKind) =>
            new(
                snapshot.CharacterId,
                AiActionCommandKind.BeginBreakdown,
                argument: breakdownKind);
    }
}
