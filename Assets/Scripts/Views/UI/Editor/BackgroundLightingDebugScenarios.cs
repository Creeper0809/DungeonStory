using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class BackgroundLightingDebugScenarios
{
    [MenuItem("DungeonStory/Debug/UI/Run Background Lighting Checks")]
    public static void RunAllFromMenu()
    {
        RunAll(log: true);
    }

    public static bool RunAll(bool log = false)
    {
        List<string> errors = new List<string>();
        CheckPaletteAnchors(errors);
        CheckContinuousCycle(errors);
        CheckUiPalette(errors);
        CheckCameraCoverage(errors);

        if (errors.Count > 0)
        {
            Debug.LogError($"BackgroundLightingDebugScenarios failed:\n{string.Join("\n", errors)}");
            return false;
        }

        if (log)
        {
            Debug.Log("BackgroundLightingDebugScenarios passed: restrained palette, readable light range, and continuous 24-hour cycle.");
        }

        return true;
    }

    private static void CheckPaletteAnchors(List<string> errors)
    {
        Color midnight = BackgroundManager.EvaluateSkyColor(0f);
        Color morning = BackgroundManager.EvaluateSkyColor(9f);
        Color noon = BackgroundManager.EvaluateSkyColor(12f);
        Color sunset = BackgroundManager.EvaluateSkyColor(18f);
        Color wrappedMidnight = BackgroundManager.EvaluateSkyColor(24f);

        Color.RGBToHSV(morning, out _, out float morningSaturation, out _);
        Require(morningSaturation < 0.35f, "Morning sky is too saturated for the dungeon palette.", errors);
        Require(Luminance(noon) > Luminance(midnight) + 0.3f,
            "Daylight sky does not separate clearly from midnight.", errors);
        Require(sunset.r > sunset.b + 0.15f,
            "Sunset lost its warm counterpoint to the cool daytime palette.", errors);
        Require(ColorDistance(midnight, wrappedMidnight) < 0.001f,
            "The 24-hour sky cycle does not wrap cleanly.", errors);

        float nightLight = BackgroundManager.EvaluateGlobalLightIntensity(0f);
        float dayLight = BackgroundManager.EvaluateGlobalLightIntensity(12f);
        Require(nightLight >= 0.55f, "Night lighting crushes sprite readability.", errors);
        Require(dayLight >= 0.95f && dayLight <= 1f,
            "Day lighting is not bright enough or exceeds the intended neutral range.", errors);
        Require(dayLight > nightLight + 0.3f,
            "Day and night global-light levels are not visually distinct.", errors);
    }

    private static void CheckContinuousCycle(List<string> errors)
    {
        const float sampleStep = 0.05f;
        Color previousColor = BackgroundManager.EvaluateSkyColor(0f);
        float previousLight = BackgroundManager.EvaluateGlobalLightIntensity(0f);
        for (float hour = sampleStep; hour <= 24f; hour += sampleStep)
        {
            Color currentColor = BackgroundManager.EvaluateSkyColor(hour);
            float currentLight = BackgroundManager.EvaluateGlobalLightIntensity(hour);
            Require(ColorDistance(previousColor, currentColor) < 0.025f,
                $"Sky color jumps around hour {hour:0.00}.", errors);
            Require(Mathf.Abs(previousLight - currentLight) < 0.02f,
                $"Global light jumps around hour {hour:0.00}.", errors);
            Require(currentLight >= 0.55f && currentLight <= 1f,
                $"Global light leaves its readable range around hour {hour:0.00}.", errors);
            previousColor = currentColor;
            previousLight = currentLight;
        }
    }

    private static void CheckUiPalette(List<string> errors)
    {
        DungeonUserSettingsData previousSettings = DungeonUserSettingsRuntime.Current.Clone();
        try
        {
            DungeonUserSettingsRuntime.Publish(new DungeonUserSettingsData { highContrast = false });
            float panel = Luminance(DungeonUiTheme.Panel);
            float surface = Luminance(DungeonUiTheme.Surface);
            float raised = Luminance(DungeonUiTheme.SurfaceRaised);
            float text = Luminance(DungeonUiTheme.TextPrimary);

            Require(panel >= 0.34f, "Standard UI panel is too close to black.", errors);
            Require(surface > panel + 0.07f, "Standard UI surface does not separate from panel.", errors);
            Require(raised > surface + 0.09f, "Standard UI raised controls are too flat or too dark.", errors);
            Require(text > panel + 0.55f, "Standard UI text does not contrast enough against panels.", errors);
            Require(DungeonUiTheme.ModalScrimAlpha <= 0.36f,
                "Standard modal scrim is dark enough to look like a gamma drop.", errors);
            Require(DungeonUiTheme.OwnerSelectionScrimAlpha <= 0.44f,
                "Standard owner-selection scrim is dark enough to crush the scene.", errors);
            Require(DungeonUiTheme.ResultScrimAlpha <= 0.42f,
                "Standard result scrim is dark enough to crush the scene.", errors);
        }
        finally
        {
            DungeonUserSettingsRuntime.Publish(previousSettings);
        }
    }

    private static void CheckCameraCoverage(List<string> errors)
    {
        GameObject cameraObject = new GameObject("Backdrop Coverage Camera", typeof(Camera));
        try
        {
            Camera camera = cameraObject.GetComponent<Camera>();
            camera.orthographic = true;
            camera.aspect = 16f / 9f;
            camera.transform.position = new Vector3(-29f, 4.5f, -10f);
            camera.orthographicSize = 3.25f;
            Rect closeCoverage = DungeonSceneBackdropFitter.CalculateCoverageRect(
                -68f,
                8f,
                0f,
                9f,
                camera,
                2f);

            camera.orthographicSize = 10.5f;
            Rect wideCoverage = DungeonSceneBackdropFitter.CalculateCoverageRect(
                -68f,
                8f,
                0f,
                9f,
                camera,
                2f);
            float viewTop = camera.transform.position.y + camera.orthographicSize;
            float viewBottom = camera.transform.position.y - camera.orthographicSize;

            Require(wideCoverage.height > closeCoverage.height,
                "Sky coverage does not expand when the camera zooms out.", errors);
            Require(wideCoverage.yMin <= viewBottom - 1.99f
                    && wideCoverage.yMax >= viewTop + 1.99f,
                "Sky coverage does not contain the zoomed camera viewport and padding.", errors);
        }
        finally
        {
            Object.DestroyImmediate(cameraObject);
        }
    }

    private static float Luminance(Color color)
    {
        return color.r * 0.2126f + color.g * 0.7152f + color.b * 0.0722f;
    }

    private static float ColorDistance(Color a, Color b)
    {
        float red = a.r - b.r;
        float green = a.g - b.g;
        float blue = a.b - b.b;
        return Mathf.Sqrt(red * red + green * green + blue * blue);
    }

    private static void Require(bool condition, string message, ICollection<string> errors)
    {
        if (!condition)
        {
            errors.Add("- " + message);
        }
    }
}
