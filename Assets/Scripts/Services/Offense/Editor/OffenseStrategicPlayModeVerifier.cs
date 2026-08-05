using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using TMPro;
using UnityEditor;
using UnityEngine;

public static class OffenseStrategicPlayModeVerifier
{
    public const string OutputDirectory = "Temp/OffenseStrategicValidation";
    public const string ReportPath =
        OutputDirectory + "/offense-strategic-visual-report.txt";

    [MenuItem("Tools/DungeonStory/Validation/Capture Offense Strategic UI")]
    public static void RunFromMenu()
    {
        if (!EditorApplication.isPlaying)
        {
            Debug.LogError("Offense Strategic UI capture requires PlayMode.");
            return;
        }

        if (UnityEngine.Object.FindFirstObjectByType<
                OffenseStrategicVisualVerificationRunner>() != null)
        {
            Debug.LogWarning("Offense Strategic UI capture is already running.");
            return;
        }

        EditorApplication.ExecuteMenuItem("Window/General/Game");
        new GameObject("Offense Strategic Visual Verification Runner")
            .AddComponent<OffenseStrategicVisualVerificationRunner>();
    }
}

public sealed class OffenseStrategicVisualVerificationRunner : MonoBehaviour
{
    private static readonly Vector2Int[] Resolutions =
    {
        new Vector2Int(1600, 900),
        new Vector2Int(900, 1600)
    };

    private readonly List<string> report = new List<string>();
    private readonly List<string> failures = new List<string>();
    private int originalGameViewSizeIndex = -1;

    private IEnumerator Start()
    {
        Directory.CreateDirectory(OffenseStrategicPlayModeVerifier.OutputDirectory);
        originalGameViewSizeIndex =
            GameViewResolutionController.SelectedSizeIndex;

        OffenseWorldMapPanel panel =
            FindFirstObjectByType<OffenseWorldMapPanel>();
        Check(
            panel != null && panel.gameObject.activeInHierarchy,
            "Strategic_PANEL_ACTIVE",
            panel != null ? panel.name : "missing");

        string surface = ResolveStrategicSurfaceName();
        foreach (Vector2Int resolution in Resolutions)
        {
            yield return SelectResolution(resolution);
            ValidatePanelBounds(panel, resolution);
            ValidateText(panel, resolution);
            yield return Capture(surface, resolution);
        }

        if (originalGameViewSizeIndex >= 0)
        {
            GameViewResolutionController.SelectedSizeIndex =
                originalGameViewSizeIndex;
        }

        string summary = failures.Count == 0
            ? $"PASS Offense Strategic visual verification ({surface})"
            : $"FAIL Offense Strategic visual verification ({failures.Count})";
        report.Insert(0, summary);
        File.WriteAllLines(OffenseStrategicPlayModeVerifier.ReportPath, report);
        if (failures.Count == 0)
        {
            Debug.Log(summary);
        }
        else
        {
            Debug.LogError(summary + "\n" + string.Join("\n", failures));
        }

        Destroy(gameObject);
    }

    private IEnumerator SelectResolution(Vector2Int resolution)
    {
        GameViewResolutionController.Select(resolution.x, resolution.y);
        float deadline = Time.realtimeSinceStartup + 3f;
        while ((Screen.width != resolution.x || Screen.height != resolution.y)
            && Time.realtimeSinceStartup < deadline)
        {
            yield return null;
        }

        Canvas.ForceUpdateCanvases();
        yield return PlayModeVerificationFrameWait.CaptureReady();
        Canvas.ForceUpdateCanvases();
        Check(
            Screen.width == resolution.x && Screen.height == resolution.y,
            $"RESOLUTION_{Key(resolution)}",
            $"actual={Screen.width}x{Screen.height}");
    }

    private IEnumerator Capture(string surface, Vector2Int resolution)
    {
        yield return PlayModeVerificationFrameWait.CaptureReady();
        Texture2D capture =
            PlayModeVerificationFrameWait.CaptureScreenshotAsTexture();
        if (capture == null)
        {
            Check(false, $"CAPTURE_{Key(resolution)}", "capture is null");
            yield break;
        }

        string path = Path.Combine(
            OffenseStrategicPlayModeVerifier.OutputDirectory,
            $"{surface}-{resolution.x}x{resolution.y}.png");
        File.WriteAllBytes(path, capture.EncodeToPNG());
        Color32[] pixels = capture.GetPixels32();
        int stride = Mathf.Max(1, pixels.Length / 4096);
        int nonBlack = 0;
        int colorTransitions = 0;
        Color32 previous = pixels.Length > 0
            ? pixels[0]
            : new Color32(0, 0, 0, 255);
        for (int index = 0; index < pixels.Length; index += stride)
        {
            Color32 pixel = pixels[index];
            if (pixel.r > 4 || pixel.g > 4 || pixel.b > 4)
            {
                nonBlack++;
            }

            if (Mathf.Abs(pixel.r - previous.r)
                    + Mathf.Abs(pixel.g - previous.g)
                    + Mathf.Abs(pixel.b - previous.b) > 24)
            {
                colorTransitions++;
            }

            previous = pixel;
        }

        Destroy(capture);
        Check(
            nonBlack >= 256 && colorTransitions >= 64,
            $"CAPTURE_CONTENT_{Key(resolution)}",
            $"nonBlack={nonBlack}; transitions={colorTransitions}; path={path}");
    }

    private void ValidatePanelBounds(
        OffenseWorldMapPanel panel,
        Vector2Int resolution)
    {
        RectTransform rect = panel != null
            ? panel.transform as RectTransform
            : null;
        if (rect == null)
        {
            Check(false, $"PANEL_BOUNDS_{Key(resolution)}", "missing rect");
            return;
        }

        Vector3[] corners = new Vector3[4];
        rect.GetWorldCorners(corners);
        bool inside = corners.All(corner =>
        {
            Vector2 point = RectTransformUtility.WorldToScreenPoint(null, corner);
            return point.x >= -2f
                && point.y >= -2f
                && point.x <= resolution.x + 2f
                && point.y <= resolution.y + 2f;
        });
        Check(
            inside,
            $"PANEL_BOUNDS_{Key(resolution)}",
            string.Join(", ", corners.Select(corner => corner.ToString("F1"))));
    }

    private void ValidateText(
        OffenseWorldMapPanel panel,
        Vector2Int resolution)
    {
        if (panel == null)
        {
            return;
        }

        TMP_Text[] labels = panel.GetComponentsInChildren<TMP_Text>(true);
        string[] overflowing = labels
            .Where(label => label != null
                && label.gameObject.activeInHierarchy
                && !string.IsNullOrWhiteSpace(label.text)
                && label.isTextOverflowing)
            .Select(label => label.name + ":" + label.text)
            .Take(8)
            .ToArray();
        Check(
            overflowing.Length == 0,
            $"TEXT_OVERFLOW_{Key(resolution)}",
            overflowing.Length == 0
                ? $"labels={labels.Length}"
                : string.Join(" | ", overflowing));
    }

    private static string ResolveStrategicSurfaceName()
    {
        if (GameObject.Find("BattleTitle") != null)
        {
            return "battle";
        }

        if (GameObject.Find("DecisionTitle") != null)
        {
            return "decision";
        }

        return "map";
    }

    private void Check(bool condition, string id, string detail)
    {
        string line = $"{(condition ? "PASS" : "FAIL")} {id} {detail}";
        report.Add(line);
        if (!condition)
        {
            failures.Add(line);
        }
    }

    private static string Key(Vector2Int resolution)
    {
        return $"{resolution.x}x{resolution.y}";
    }
}
