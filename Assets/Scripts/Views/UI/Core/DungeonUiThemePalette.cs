using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Immutable palette/style authority. Runtime settings are explicit inputs so
/// this named presentation assembly does not depend on a mutable global.
/// </summary>
public static class DungeonUiThemePalette
{
    private static readonly Color StandardCanvasScrim = Hex("40565BFF");
    private static readonly Color StandardPanel = Hex("435D64FF");
    private static readonly Color StandardSurface = Hex("567179FF");
    private static readonly Color StandardSurfaceRaised = Hex("789198FF");
    private static readonly Color StandardSurfaceMuted = Hex("34484EFF");
    private static readonly Color StandardBorder = Hex("A9BEC2FF");
    private static readonly Color StandardTextPrimary = Hex("F5F7F4FF");
    private static readonly Color StandardTextSecondary = Hex("D2DDD9FF");
    private static readonly Color StandardAccent = Hex("56B892FF");

    public static Color CanvasScrim(bool highContrast) => highContrast ? Hex("182428FF") : StandardCanvasScrim;
    public static Color Panel(bool highContrast) => highContrast ? Hex("172126FF") : StandardPanel;
    public static Color Surface(bool highContrast) => highContrast ? Hex("223137FF") : StandardSurface;
    public static Color SurfaceRaised(bool highContrast) => highContrast ? Hex("344951FF") : StandardSurfaceRaised;
    public static Color SurfaceMuted(bool highContrast) => highContrast ? Hex("10191DFF") : StandardSurfaceMuted;
    public static Color Border(bool highContrast) => highContrast ? Hex("D8E4E1FF") : StandardBorder;
    public static Color TextPrimary(bool highContrast) => highContrast ? Color.white : StandardTextPrimary;
    public static Color TextSecondary(bool highContrast) => highContrast ? Hex("D8E4E1FF") : StandardTextSecondary;
    public static Color Accent(bool highContrast) => highContrast ? Hex("46D69BFF") : StandardAccent;
    public static Color AccentHover(bool highContrast) => highContrast ? Hex("79F0BFFF") : Hex("8CE0BFFF");
    public static Color AccentPressed(bool highContrast) => highContrast ? Hex("2CA879FF") : Hex("3E8B70FF");
    public static Color Warning(bool highContrast) => highContrast ? Hex("FFD15AFF") : Hex("D2A449FF");
    public static Color Danger(bool highContrast) => highContrast ? Hex("FF7770FF") : Hex("C95E5AFF");
    public static Color Good(bool highContrast) => highContrast ? Hex("55E6A7FF") : Hex("4CB88BFF");
    public static float ModalScrimAlpha(bool highContrast) => highContrast ? 0.56f : 0.34f;
    public static float OwnerSelectionScrimAlpha(bool highContrast) => highContrast ? 0.62f : 0.42f;
    public static float ResultScrimAlpha(bool highContrast) => highContrast ? 0.60f : 0.40f;
    public static Color ModalScrim(bool highContrast) => WithAlpha(CanvasScrim(highContrast), ModalScrimAlpha(highContrast));
    public static Color OwnerSelectionScrim(bool highContrast) => WithAlpha(CanvasScrim(highContrast), OwnerSelectionScrimAlpha(highContrast));
    public static Color ResultScrim(bool highContrast) => WithAlpha(CanvasScrim(highContrast), ResultScrimAlpha(highContrast));

    public static Color GetMeterColor(float value, bool highContrast) =>
        value < 0.25f ? Danger(highContrast) : value < 0.5f ? Warning(highContrast) : Good(highContrast);

    public static void StyleButton(
        Button button,
        bool highContrast,
        bool reducedMotion,
        bool selected = false,
        bool destructive = false)
    {
        if (button == null) return;
        Image image = button.targetGraphic as Image ?? button.GetComponent<Image>();
        if (image == null) return;
        image.color = destructive
            ? Danger(highContrast)
            : selected ? Accent(highContrast) : SurfaceRaised(highContrast);
        button.targetGraphic = image;
        button.transition = Selectable.Transition.ColorTint;
        button.colors = new ColorBlock
        {
            normalColor = Color.white,
            highlightedColor = destructive ? Hex("FFBAB7FF") : selected ? Hex("C5F0DEFF") : Hex("C8D7DAFF"),
            pressedColor = destructive ? Hex("C47B78FF") : selected ? Hex("8FCDB5FF") : Hex("91A5AAFF"),
            selectedColor = Color.white,
            disabledColor = Hex("3F4E53CC"),
            colorMultiplier = 1f,
            fadeDuration = reducedMotion ? 0f : 0.08f
        };
        TMP_Text label = button.GetComponentInChildren<TMP_Text>(true);
        if (label != null)
        {
            label.color = TextPrimary(highContrast);
            label.fontStyle = FontStyles.Bold;
            label.characterSpacing = 0f;
        }
    }

    private static Color Hex(string html) => ColorUtility.TryParseHtmlString("#" + html, out Color color)
        ? color : throw new InvalidOperationException($"Invalid UI theme color: {html}");
    private static Color WithAlpha(Color color, float alpha) =>
        new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
}
