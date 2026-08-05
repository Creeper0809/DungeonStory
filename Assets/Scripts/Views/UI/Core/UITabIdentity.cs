using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[DisallowMultipleComponent]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class UITabIdentity : MonoBehaviour
{
    [SerializeField] private TabId tabId;

    public TabId Id => tabId;

    public void Set(TabId id)
    {
        tabId = id;
    }
}

[DisallowMultipleComponent]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class UITabButtonBinding : MonoBehaviour
{
    [SerializeField] private TabId tabId;

    public TabId Id => tabId;

    public void Set(TabId id)
    {
        tabId = id;
    }
}
