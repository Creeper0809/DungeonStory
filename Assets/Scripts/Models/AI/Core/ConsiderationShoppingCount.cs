using UnityEngine.Scripting.APIUpdating;

namespace DungeonStory.AI
{
    [MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
    public sealed class ConsiderationShoppingCount
    {
        public static float Score(AiCharacterDecisionSnapshot snapshot) =>
            snapshot.Exists
            && snapshot.HasPersistentIdentity
            && snapshot.HasShopping
            && snapshot.VisitCount > 0
                ? snapshot.VisitCount * 0.4f
                : 0f;
    }
}
