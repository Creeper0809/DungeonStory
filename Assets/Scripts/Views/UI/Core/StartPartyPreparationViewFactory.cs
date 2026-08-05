using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public sealed class StartPartyPreparationViewFactory
{
    private readonly ITmpKoreanFontService fontService;

    public StartPartyPreparationViewFactory(ITmpKoreanFontService fontService)
    {
        this.fontService = fontService
            ?? throw new ArgumentNullException(nameof(fontService));
    }

    public Button CreateDiceButton(
        Transform parent,
        string name,
        Action clicked,
        Vector2 anchorMin,
        Vector2 anchorMax,
        bool interactable,
        string accessibleLabel)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        SetRect(rect, anchorMin, anchorMax);
        Image image = obj.GetComponent<Image>();
        image.color = interactable
            ? DungeonUiThemePalette.Surface(false)
            : DungeonUiThemePalette.SurfaceMuted(false);

        Button button = obj.GetComponent<Button>();
        button.interactable = interactable;
        button.onClick.AddListener(() => clicked?.Invoke());
        CreateDiceDot(obj.transform, "DotA", new Vector2(0.28f, 0.28f), interactable);
        CreateDiceDot(obj.transform, "DotB", new Vector2(0.72f, 0.28f), interactable);
        CreateDiceDot(obj.transform, "DotC", new Vector2(0.5f, 0.5f), interactable);
        CreateDiceDot(obj.transform, "DotD", new Vector2(0.28f, 0.72f), interactable);
        CreateDiceDot(obj.transform, "DotE", new Vector2(0.72f, 0.72f), interactable);

        TMP_Text hidden = CreateText(obj.transform, "AccessibleLabel", accessibleLabel, 1f, TextAlignmentOptions.Center);
        Stretch(hidden.rectTransform);
        hidden.color = Color.clear;
        hidden.raycastTarget = false;
        return button;
    }

    private void CreateDiceDot(Transform parent, string name, Vector2 anchor, bool enabled)
    {
        Image dot = CreateImage(
            parent,
            name,
            enabled
                ? DungeonUiThemePalette.TextPrimary(false)
                : DungeonUiThemePalette.TextSecondary(false));
        RectTransform rect = dot.rectTransform;
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.sizeDelta = new Vector2(5f, 5f);
        rect.anchoredPosition = Vector2.zero;
        dot.raycastTarget = false;
    }

    public TMP_Text CreateText(
        Transform parent,
        string name,
        string text,
        float size,
        TextAlignmentOptions alignment)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        obj.transform.SetParent(parent, false);
        TMP_Text label = obj.GetComponent<TMP_Text>();
        label.text = text ?? string.Empty;
        label.fontSize = size;
        label.alignment = alignment;
        label.color = DungeonUiThemePalette.TextPrimary(false);
        label.textWrappingMode = TextWrappingModes.NoWrap;
        label.font = fontService.Resolve();
        return label;
    }

    public Image CreateImage(Transform parent, string name, Color color)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        Image image = obj.GetComponent<Image>();
        image.color = color;
        return image;
    }

    public Transform CreatePanel(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        bool raised)
    {
        Image panel = CreateImage(
            parent,
            name,
            raised
                ? DungeonUiThemePalette.Surface(false)
                : DungeonUiThemePalette.SurfaceMuted(false));
        SetRect(panel.rectTransform, anchorMin, anchorMax);
        panel.raycastTarget = true;
        return panel.transform;
    }

    public Button CreateButton(
        Transform parent,
        string name,
        string label,
        Action clicked,
        Vector2 anchorMin,
        Vector2 anchorMax,
        bool selected = false)
    {
        GameObject obj = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
        obj.transform.SetParent(parent, false);
        RectTransform rect = obj.GetComponent<RectTransform>();
        SetRect(rect, anchorMin, anchorMax);
        Image image = obj.GetComponent<Image>();
        image.color = selected
            ? DungeonUiThemePalette.Accent(false)
            : DungeonUiThemePalette.SurfaceRaised(false);

        Button button = obj.GetComponent<Button>();
        button.onClick.AddListener(() => clicked?.Invoke());

        TMP_Text text = CreateText(obj.transform, "Label", label, 17f, TextAlignmentOptions.Center);
        Stretch(text.rectTransform, new Vector2(10f, 4f), new Vector2(-10f, -4f));
        text.textWrappingMode = TextWrappingModes.Normal;
        return button;
    }

    public static void Stretch(RectTransform rect)
    {
        Stretch(rect, Vector2.zero, Vector2.zero);
    }

    public static void Stretch(RectTransform rect, Vector2 offsetMin, Vector2 offsetMax)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = offsetMin;
        rect.offsetMax = offsetMax;
    }

    public static void SetRect(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

}
