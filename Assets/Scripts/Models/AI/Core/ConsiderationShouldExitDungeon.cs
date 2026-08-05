using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.AI
{
    [MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
    public sealed class ConsiderationShouldExitDungeon
    {
        public static float Score(AiCharacterDecisionSnapshot snapshot) =>
            snapshot.Exists
            && snapshot.HasPersistentIdentity
            && !snapshot.HasWorkRole
            && snapshot.HasShopping
            && snapshot.ShouldExitDungeon
                ? 1f
                : 0f;
    }
}
