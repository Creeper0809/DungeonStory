using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonRuntimeWorldUiHierarchyAdapter : IWorldUiHierarchy
{
    public Transform GetWorldUiRoot(GameObject sceneHint = null)
    {
        return DungeonRuntimeHierarchy.GetCategory(
            DungeonRuntimeHierarchy.WorldUi,
            sceneHint);
    }

    public void ParentToWorldUi(GameObject child)
    {
        DungeonRuntimeHierarchy.Parent(
            child,
            DungeonRuntimeHierarchy.WorldUi);
    }
}
