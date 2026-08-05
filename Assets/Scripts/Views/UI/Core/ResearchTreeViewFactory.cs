using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class ResearchTreeViewFactory
{
    private readonly ITmpKoreanFontService fontService;

    public ResearchTreeViewFactory(ITmpKoreanFontService fontService)
    {
        this.fontService = fontService
            ?? throw new ArgumentNullException(nameof(fontService));
    }

    public TMP_InputField CreateInput(
        Transform parent,
        string name,
        string placeholder)
    {
        RectTransform root = CreateRect(name, parent);
        CreateImage(
            root.gameObject,
            DungeonUiThemePalette.SurfaceMuted(highContrast: false));
        TMP_Text placeholderText = CreateText(
            root,
            "Placeholder",
            placeholder,
            16f,
            TextAlignmentOptions.MidlineLeft);
        placeholderText.color = DungeonUiThemePalette.TextSecondary(
            highContrast: false);
        SetRect(
            placeholderText.rectTransform,
            Vector2.zero,
            Vector2.one,
            12f,
            0f,
            -10f,
            0f);
        TMP_Text value = CreateText(
            root,
            "Text",
            string.Empty,
            16f,
            TextAlignmentOptions.MidlineLeft);
        SetRect(
            value.rectTransform,
            Vector2.zero,
            Vector2.one,
            12f,
            0f,
            -10f,
            0f);
        TMP_InputField input = root.gameObject.AddComponent<TMP_InputField>();
        input.textComponent = value;
        input.placeholder = placeholderText;
        input.lineType = TMP_InputField.LineType.SingleLine;
        return input;
    }

    public Button CreateButton(
        Transform parent,
        string name,
        string label,
        Action onClick)
    {
        RectTransform root = CreateRect(name, parent);
        Image image = CreateImage(
            root.gameObject,
            DungeonUiThemePalette.SurfaceRaised(highContrast: false));
        Button button = root.gameObject.AddComponent<Button>();
        button.targetGraphic = image;
        ColorBlock colors = button.colors;
        colors.normalColor = DungeonUiThemePalette.SurfaceRaised(
            highContrast: false);
        colors.highlightedColor = DungeonUiThemePalette.AccentHover(
            highContrast: false);
        colors.pressedColor = DungeonUiThemePalette.AccentPressed(
            highContrast: false);
        colors.selectedColor = DungeonUiThemePalette.Accent(
            highContrast: false);
        button.colors = colors;
        button.onClick.AddListener(() => onClick?.Invoke());
        TMP_Text text = CreateText(
            root,
            "Label",
            label,
            16f,
            TextAlignmentOptions.Center);
        Stretch(text.rectTransform);
        return button;
    }

    public TMP_Text CreateText(
        Transform parent,
        string name,
        string value,
        float size,
        TextAlignmentOptions alignment)
    {
        RectTransform rect = CreateRect(name, parent);
        TextMeshProUGUI text = rect.gameObject.AddComponent<TextMeshProUGUI>();
        fontService.Apply(text);
        text.text = value;
        text.fontSize = size;
        text.color = DungeonUiThemePalette.TextPrimary(
            highContrast: false);
        text.alignment = alignment;
        text.raycastTarget = false;
        return text;
    }

    public static RectTransform CreateRect(string name, Transform parent)
    {
        GameObject created = new GameObject(name, typeof(RectTransform));
        created.transform.SetParent(parent, false);
        return created.GetComponent<RectTransform>();
    }

    public static Image CreateImage(GameObject target, Color color)
    {
        Image image = target.GetComponent<Image>() ?? target.AddComponent<Image>();
        image.color = color;
        return image;
    }

    public static void SetButtonColor(Button button, Color color)
    {
        if (button?.targetGraphic != null)
        {
            button.targetGraphic.color = color;
        }
    }

    public static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }

    public static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    public static void SetRect(
        RectTransform rect,
        Vector2 anchorMin,
        Vector2 anchorMax,
        float left,
        float bottom,
        float right,
        float top)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = new Vector2(left, bottom);
        rect.offsetMax = new Vector2(right, top);
    }
}
