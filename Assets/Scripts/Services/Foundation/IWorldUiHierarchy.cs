using UnityEngine;

/// <summary>
/// Cross-domain port for parenting transient world-space UI without exposing
/// the scene hierarchy implementation to gameplay or presentation services.
/// </summary>
public interface IWorldUiHierarchy
{
    Transform GetWorldUiRoot(GameObject sceneHint = null);
    void ParentToWorldUi(GameObject child);
}
