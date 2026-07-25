using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class OffensePanelUiFactory
{
    public static GameObject CreateOverlayCanvas(
        string name,
        int sortingOrder,
        Vector2 referenceResolution)
    {
        GameObject canvasObject = new GameObject(name, typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = referenceResolution;
        return canvasObject;
    }

    public static GameObject CreatePanel(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Color color)
    {
        GameObject panelObject = new GameObject(name, typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);
        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = anchorMin;
        panelRect.anchorMax = anchorMax;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;
        panelObject.GetComponent<Image>().color = color;
        return panelObject;
    }

    public static GameObject CreateVerticalRoot(
        Transform parent,
        string name,
        Vector2 anchorMin,
        Vector2 anchorMax,
        Vector2 offsetMin,
        Vector2 offsetMax,
        float spacing)
    {
        GameObject rootObject = new GameObject(name, typeof(RectTransform), typeof(VerticalLayoutGroup));
        rootObject.transform.SetParent(parent, false);
        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = anchorMin;
        rootRect.anchorMax = anchorMax;
        rootRect.offsetMin = offsetMin;
        rootRect.offsetMax = offsetMax;

        VerticalLayoutGroup layout = rootObject.GetComponent<VerticalLayoutGroup>();
        layout.spacing = spacing;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandHeight = false;
        return rootObject;
    }

    public static GameObject CreateText(
        Transform parent,
        string name,
        float fontSize,
        TextAlignmentOptions alignment,
        ITmpKoreanFontService tmpKoreanFontService)
    {
        if (tmpKoreanFontService == null)
        {
            throw new ArgumentNullException(nameof(tmpKoreanFontService));
        }

        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TextMeshProUGUI text = textObject.GetComponent<TextMeshProUGUI>();
        tmpKoreanFontService.Apply(text);
        text.fontSize = fontSize;
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        return textObject;
    }

    public static GameObject CreateButton(
        RectTransform parent,
        string label,
        float fontSize,
        Action callback,
        ITmpKoreanFontService tmpKoreanFontService)
    {
        GameObject buttonObject = new GameObject($"Button_{label}", typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<Image>().color = DungeonUiTheme.Accent;
        buttonObject.GetComponent<LayoutElement>().preferredHeight = 42f;

        Button button = buttonObject.GetComponent<Button>();
        DungeonUiTheme.StyleButton(button, selected: true);
        button.onClick.AddListener(() => callback?.Invoke());

        GameObject textObject = CreateText(buttonObject.transform, "Label", fontSize, TextAlignmentOptions.Center, tmpKoreanFontService);
        RectTransform textRect = textObject.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(8f, 4f);
        textRect.offsetMax = new Vector2(-8f, -4f);
        textObject.GetComponent<TMP_Text>().text = label;
        return buttonObject;
    }
}

public interface IOffensePanelButtonFactory
{
    GameObject CreateButton(RectTransform parent, string label, float fontSize, Action callback);
    void Release(GameObject buttonObject);
}

public sealed class OffensePanelButtonFactory : IOffensePanelButtonFactory
{
    private readonly ITmpKoreanFontService tmpKoreanFontService;

    public OffensePanelButtonFactory(ITmpKoreanFontService tmpKoreanFontService)
    {
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
    }

    public GameObject CreateButton(RectTransform parent, string label, float fontSize, Action callback)
    {
        return OffensePanelUiFactory.CreateButton(
            parent,
            label,
            fontSize,
            callback,
            tmpKoreanFontService);
    }

    public void Release(GameObject buttonObject)
    {
        if (buttonObject == null)
        {
            return;
        }

        buttonObject.SetActive(false);
        buttonObject.transform.SetParent(null, false);

        if (Application.isPlaying)
        {
            UnityEngine.Object.Destroy(buttonObject);
        }
        else
        {
            UnityEngine.Object.DestroyImmediate(buttonObject);
        }
    }
}
