using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Backward-compatible settings-aware facade. Color/style values are authored
/// only by DungeonUiThemePalette in the Presentation assembly.
/// </summary>
public static class DungeonUiTheme
{
    private const bool HighContrast = false;

    public static Color CanvasScrim =>
        DungeonUiThemePalette.CanvasScrim(HighContrast);
    public static Color Panel => DungeonUiThemePalette.Panel(HighContrast);
    public static Color Surface => DungeonUiThemePalette.Surface(HighContrast);
    public static Color SurfaceRaised =>
        DungeonUiThemePalette.SurfaceRaised(HighContrast);
    public static Color SurfaceMuted =>
        DungeonUiThemePalette.SurfaceMuted(HighContrast);
    public static Color Border => DungeonUiThemePalette.Border(HighContrast);
    public static Color TextPrimary =>
        DungeonUiThemePalette.TextPrimary(HighContrast);
    public static Color TextSecondary =>
        DungeonUiThemePalette.TextSecondary(HighContrast);
    public static Color Accent => DungeonUiThemePalette.Accent(HighContrast);
    public static Color AccentHover =>
        DungeonUiThemePalette.AccentHover(HighContrast);
    public static Color AccentPressed =>
        DungeonUiThemePalette.AccentPressed(HighContrast);
    public static Color Warning => DungeonUiThemePalette.Warning(HighContrast);
    public static Color Danger => DungeonUiThemePalette.Danger(HighContrast);
    public static Color Good => DungeonUiThemePalette.Good(HighContrast);
    public static float ModalScrimAlpha =>
        DungeonUiThemePalette.ModalScrimAlpha(HighContrast);
    public static float OwnerSelectionScrimAlpha =>
        DungeonUiThemePalette.OwnerSelectionScrimAlpha(HighContrast);
    public static float ResultScrimAlpha =>
        DungeonUiThemePalette.ResultScrimAlpha(HighContrast);
    public static Color ModalScrim =>
        DungeonUiThemePalette.ModalScrim(HighContrast);
    public static Color OwnerSelectionScrim =>
        DungeonUiThemePalette.OwnerSelectionScrim(HighContrast);
    public static Color ResultScrim =>
        DungeonUiThemePalette.ResultScrim(HighContrast);
    public static Color GetMeterColor(float value) =>
        DungeonUiThemePalette.GetMeterColor(value, HighContrast);

    public static void StyleButton(
        Button button,
        bool selected = false,
        bool destructive = false) =>
        DungeonUiThemePalette.StyleButton(
            button,
            HighContrast,
            reducedMotion: false,
            selected,
            destructive);
}
