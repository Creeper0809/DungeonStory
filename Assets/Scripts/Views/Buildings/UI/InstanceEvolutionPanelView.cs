using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal static class InstanceEvolutionPanelView
{
    public static void AddProgress(
        Transform parent,
        string label,
        float progress,
        EvolutionReforgeOrderState state,
        TMP_FontAsset font,
        ICollection<GameObject> created)
    {
        AddText(
            parent,
            $"{label} · {InstanceEvolutionPanelPresentation.FormatOrderState(state)} · "
            + $"{Mathf.Clamp01(progress):P0}",
            font,
            14f,
            state == EvolutionReforgeOrderState.Blocked
                ? DungeonUiTheme.Danger
                : DungeonUiTheme.Warning,
            28f,
            created);
    }

    public static GameObject CreateRow(
        Transform parent,
        string name,
        float height)
    {
        GameObject row = new GameObject(
            name,
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement));
        row.transform.SetParent(parent, false);
        HorizontalLayoutGroup layout = row.GetComponent<HorizontalLayoutGroup>();
        layout.spacing = 6f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        row.GetComponent<LayoutElement>().preferredHeight = height;
        return row;
    }

    public static void AddLabel(
        Transform parent,
        string value,
        TMP_FontAsset font,
        float width,
        Color color)
    {
        GameObject textObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        textObject.GetComponent<LayoutElement>().preferredWidth = width;
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.font = font;
        text.fontSize = 14f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 14f;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
    }

    public static void AddButton(
        Transform parent,
        string label,
        TMP_FontAsset font,
        bool selected,
        bool enabled,
        Action action,
        float width)
    {
        GameObject buttonObject = new GameObject(
            "Button",
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<LayoutElement>().preferredWidth = width;
        Button button = buttonObject.GetComponent<Button>();
        DungeonUiTheme.StyleButton(button, selected);
        button.interactable = enabled;
        button.onClick.AddListener(() => action?.Invoke());

        GameObject textObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(4f, 2f);
        rect.offsetMax = new Vector2(-4f, -2f);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = label;
        text.font = font;
        text.fontSize = 14f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 14f;
        text.color = enabled
            ? DungeonUiTheme.TextPrimary
            : DungeonUiTheme.TextSecondary;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
    }

    public static void AddText(
        Transform parent,
        string value,
        TMP_FontAsset font,
        float fontSize,
        Color color,
        float height,
        ICollection<GameObject> created)
    {
        GameObject textObject = new GameObject(
            "InstanceEvolutionText",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        textObject.GetComponent<LayoutElement>().preferredHeight = height;
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        created.Add(textObject);
    }

    public static string Sanitize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Unknown"
            : value.Replace(':', '_').Replace('/', '_').Replace(' ', '_');
    }
}
