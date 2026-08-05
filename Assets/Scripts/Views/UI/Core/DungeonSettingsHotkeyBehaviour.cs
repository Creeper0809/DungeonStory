using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonSettingsHotkeyBehaviour : MonoBehaviour
{
    private Action close;

    public void Initialize(Action closeAction)
    {
        close = closeAction;
    }

    private void Update()
    {
        bool escapePressed = Keyboard.current != null
            && Keyboard.current.escapeKey.wasPressedThisFrame;
        if (!escapePressed)
        {
            try
            {
                escapePressed = Input.GetKeyDown(KeyCode.Escape);
            }
            catch (InvalidOperationException)
            {
                escapePressed = false;
            }
        }

        if (escapePressed)
        {
            close?.Invoke();
        }
    }
}
