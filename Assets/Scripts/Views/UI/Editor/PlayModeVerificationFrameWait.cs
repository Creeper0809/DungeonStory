using System.Collections;
using System;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public static class PlayModeVerificationFrameWait
{
    public static IEnumerator CaptureReady()
    {
        if (!Application.isBatchMode)
        {
            yield return new WaitForEndOfFrame();
            yield break;
        }

        Canvas.ForceUpdateCanvases();
        yield return null;
        Canvas.ForceUpdateCanvases();
        yield return null;
    }

    public static Texture2D CaptureScreenshotAsTexture()
    {
        if (!Application.isBatchMode)
        {
            return ScreenCapture.CaptureScreenshotAsTexture();
        }

        Camera camera = Camera.main
            ?? UnityEngine.Object.FindFirstObjectByType<Camera>(FindObjectsInactive.Include);
        if (camera == null)
        {
            return null;
        }

        int width = Mathf.Max(320, Screen.width);
        int height = Mathf.Max(240, Screen.height);
        RenderTexture renderTexture = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
        RenderTexture previousActive = RenderTexture.active;
        RenderTexture previousTarget = camera.targetTexture;
        try
        {
            camera.targetTexture = renderTexture;
            camera.Render();
            RenderTexture.active = renderTexture;
            Texture2D capture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            capture.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            capture.Apply(false);
            return capture;
        }
        finally
        {
            camera.targetTexture = previousTarget;
            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(renderTexture);
        }
    }

    public static bool DispatchPointerClick(GameObject target, Vector2 position)
    {
        if (target == null || EventSystem.current == null)
        {
            return false;
        }

        PointerEventData eventData = new PointerEventData(EventSystem.current)
        {
            position = position,
            button = PointerEventData.InputButton.Left
        };
        ExecuteEvents.ExecuteHierarchy(target, eventData, ExecuteEvents.pointerDownHandler);
        ExecuteEvents.ExecuteHierarchy(target, eventData, ExecuteEvents.pointerUpHandler);
        ExecuteEvents.ExecuteHierarchy(target, eventData, ExecuteEvents.pointerClickHandler);
        return true;
    }
}

public static class StartPartyPlayModeTestDriver
{
    public static IEnumerator CompleteIfVisible(float timeoutSeconds = 120f)
    {
        bool usesPreparationScene = FindButton("PreparationStartRunButton", requireInteractable: false) != null;
        bool usesGameplayFallback = FindButton("StartPartyConfirm", requireInteractable: false) != null;
        if (!usesPreparationScene && !usesGameplayFallback)
        {
            yield break;
        }

        float confirmDeadline = Time.realtimeSinceStartup + Mathf.Max(1f, timeoutSeconds);
        Button confirm = null;
        while (Time.realtimeSinceStartup < confirmDeadline)
        {
            confirm = FindStartButton(requireInteractable: true);
            if (confirm != null)
            {
                break;
            }

            if (usesGameplayFallback)
            {
                Button candidate = FindFirstLegacySkillCandidate();
                if (candidate != null)
                {
                    string candidateName = candidate.name;
                    Click(candidate);
                    yield return null;
                    candidate = FindButton(candidateName, requireInteractable: true);
                    if (candidate != null)
                    {
                        Click(candidate);
                    }
                }
            }

            yield return new WaitForSecondsRealtime(0.25f);
        }

        if (confirm == null)
        {
            Debug.LogError("Start party did not become ready after generated active and passive preparation.");
            yield break;
        }

        Click(confirm);
        yield return new WaitForSecondsRealtime(0.5f);
    }

    private static Button FindStartButton(bool requireInteractable)
    {
        return FindButton("PreparationStartRunButton", requireInteractable)
            ?? FindButton("StartPartyConfirm", requireInteractable);
    }

    private static Button FindFirstLegacySkillCandidate()
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .Where(button => button != null
                && button.gameObject.scene.IsValid()
                && button.gameObject.activeInHierarchy
                && button.interactable
                && button.name.StartsWith("StartSkillCandidate_", StringComparison.Ordinal))
            .OrderBy(button => button.name, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    public static Button FindButton(string objectName, bool requireInteractable)
    {
        return Resources.FindObjectsOfTypeAll<Button>()
            .FirstOrDefault(button => button != null
                && button.gameObject.scene.IsValid()
                && button.gameObject.activeInHierarchy
                && (!requireInteractable || button.interactable)
                && string.Equals(button.name, objectName, StringComparison.Ordinal));
    }

    private static void Click(Button button)
    {
        if (button == null)
        {
            return;
        }

        RectTransform rect = button.transform as RectTransform;
        Vector2 position = rect != null
            ? RectTransformUtility.WorldToScreenPoint(null, rect.TransformPoint(rect.rect.center))
            : Vector2.zero;
        PlayModeVerificationFrameWait.DispatchPointerClick(button.gameObject, position);
    }
}
