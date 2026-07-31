#if UNITY_EDITOR
using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ServiceRoomVisualValidationFacade
{
    [MenuItem("DungeonStory/QA/Service Rooms/Capture 1600x900")]
    public static void CaptureDesktop() => Capture(1600, 900);

    [MenuItem("DungeonStory/QA/Service Rooms/Capture 900x1600")]
    public static void CapturePortrait() => Capture(900, 1600);

    private static void Capture(int width, int height)
    {
        if (!Application.isPlaying)
        {
            throw new InvalidOperationException(
                "Service-room visual capture requires PlayMode.");
        }

        if (UnityEngine.Object.FindFirstObjectByType<
                ServiceRoomVisualCaptureRunner>() != null)
        {
            throw new InvalidOperationException(
                "A service-room visual capture is already running.");
        }

        EditorApplication.ExecuteMenuItem("Window/General/Game");
        new GameObject("Service Room Visual Capture Runner")
            .AddComponent<ServiceRoomVisualCaptureRunner>()
            .Initialize(width, height);
    }
}

public sealed class ServiceRoomVisualCaptureRunner : MonoBehaviour
{
    private int width;
    private int height;
    private int originalGameViewSizeIndex = -1;
    private bool initialized;

    public void Initialize(int targetWidth, int targetHeight)
    {
        width = Mathf.Max(1, targetWidth);
        height = Mathf.Max(1, targetHeight);
        initialized = true;
    }

    private IEnumerator Start()
    {
        yield return null;
        if (!initialized)
        {
            Destroy(gameObject);
            yield break;
        }

        BuildableObject hub = UnityEngine.Object
            .FindObjectsByType<BuildableObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(building =>
                building?.GetServiceHubAbility()?.serviceCategory
                    == ServiceCategory.Dining);
        UIBuildingInfo panel = UnityEngine.Object
            .FindObjectsByType<UIBuildingInfo>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault();
        if (hub == null || panel == null)
        {
            Debug.LogError(
                "GameplayScene needs a dining service hub and building panel.");
            Destroy(gameObject);
            yield break;
        }

        foreach (GameObject overlay in Resources
            .FindObjectsOfTypeAll<GameObject>()
            .Where(candidate =>
                candidate != null
                && candidate.scene.IsValid()
                && (candidate.name == "OwnerSelectionSurface"
                    || candidate.name == "OwnerSelectionPanel")))
        {
            overlay.SetActive(false);
        }

        originalGameViewSizeIndex =
            GameViewResolutionController.SelectedSizeIndex;
        GameViewResolutionController.Select(width, height);

        float deadline = Time.realtimeSinceStartup + 3f;
        while ((Screen.width != width || Screen.height != height)
            && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        panel.DisplayBuildingInfo(hub);
        Canvas.ForceUpdateCanvases();
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();

        Directory.CreateDirectory("Temp/ServiceRooms");
        string path =
            $"Temp/ServiceRooms/service-room-{width}x{height}.png";
        ScreenCapture.CaptureScreenshot(path);
        Debug.Log(
            $"Service-room visual capture requested at "
            + $"{Screen.width}x{Screen.height}: {path}");

        yield return null;
        if (originalGameViewSizeIndex >= 0)
        {
            GameViewResolutionController.SelectedSizeIndex =
                originalGameViewSizeIndex;
        }

        Destroy(gameObject);
    }
}
#endif
