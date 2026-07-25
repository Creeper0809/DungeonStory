using UnityEngine;

[DisallowMultipleComponent]
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
public sealed class UITabButtonBinding : MonoBehaviour
{
    [SerializeField] private TabId tabId;

    public TabId Id => tabId;

    public void Set(TabId id)
    {
        tabId = id;
    }
}
