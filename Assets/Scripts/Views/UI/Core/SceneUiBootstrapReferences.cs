using System;
using UnityEngine.EventSystems;

public sealed class SceneUiBootstrapReferences
{
    public SceneUiBootstrapReferences(EventSystem eventSystem)
    {
        EventSystem = eventSystem;
    }

    public EventSystem EventSystem { get; private set; }

    public void RegisterEventSystem(EventSystem eventSystem)
    {
        EventSystem = eventSystem
            ?? throw new ArgumentNullException(nameof(eventSystem));
    }
}
