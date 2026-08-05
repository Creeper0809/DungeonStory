using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.AI
{
    [MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
    public sealed class AIExitDungeon
    {
        public static AiActionDecision Evaluate(
            AiCharacterDecisionSnapshot snapshot)
        {
            bool allowed = snapshot.Exists
                && snapshot.HasPersistentIdentity
                && !snapshot.HasWorkRole
                && snapshot.HasShopping
                && snapshot.ShouldExitDungeon;
            return allowed
                ? AiActionDecision.Allow(new AiActionRequest(
                    snapshot.CharacterId,
                    AiActionCommandKind.ExitDungeon))
                : AiActionDecision.Reject(AIActionFailureKind.CannotStart);
        }
    }
}
