using System;
using System.Linq;
using UnityEditor;
using UnityEngine.InputSystem;

[InitializeOnLoad]
public static class PlayModeVerificationInputCleanup
{
    private const string VerificationMouseMarker = "VerificationMouse";

    static PlayModeVerificationInputCleanup()
    {
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        QueueCleanup();
    }

    public static void CleanupStaleVerificationMice()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        Mouse[] staleMice = InputSystem.devices
            .OfType<Mouse>()
            .Where(mouse => mouse != null
                && !mouse.native
                && mouse.name.IndexOf(VerificationMouseMarker, StringComparison.OrdinalIgnoreCase) >= 0)
            .ToArray();

        foreach (Mouse staleMouse in staleMice)
        {
            if (staleMouse.added)
            {
                InputSystem.RemoveDevice(staleMouse);
            }
        }

        Mouse nativeMouse = InputSystem.devices
            .OfType<Mouse>()
            .FirstOrDefault(mouse => mouse != null && mouse.native && mouse.added);
        if (nativeMouse == null)
        {
            return;
        }

        if (!nativeMouse.enabled)
        {
            InputSystem.EnableDevice(nativeMouse);
        }

        nativeMouse.MakeCurrent();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingPlayMode
            || change == PlayModeStateChange.EnteredEditMode)
        {
            QueueCleanup();
        }
    }

    private static void QueueCleanup()
    {
        EditorApplication.delayCall -= CleanupStaleVerificationMice;
        EditorApplication.delayCall += CleanupStaleVerificationMice;
    }
}
