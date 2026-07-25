using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public interface IEventAlertViewUiFactory
{
    GameObject CreateRuntimeRoot(Canvas canvas);

    void CreateButtonRoot(
        Transform runtimeRoot,
        Canvas canvas,
        Transform previousButtonRoot,
        out Transform buttonRoot,
        out RectTransform buttonViewportRect,
        out RectTransform buttonContentRect,
        out ScrollRect buttonScrollRect);

    void BindExistingButtonRootReferences(
        Transform buttonRoot,
        out RectTransform buttonViewportRect,
        out RectTransform buttonContentRect,
        out ScrollRect buttonScrollRect);

    bool IsButtonRootReady(
        Transform buttonRoot,
        RectTransform buttonContentRect,
        RectTransform buttonViewportRect,
        ScrollRect buttonScrollRect);

    GameObject CreateDetailPanel(Transform parent, UnityAction closeAction, out TMP_Text detailText);
}

public sealed class EventAlertViewUiFactory : IEventAlertViewUiFactory
{
    private const int RuntimeAlertSortingOrder = 480;
    private readonly ITmpKoreanFontService tmpKoreanFontService;

    public EventAlertViewUiFactory(ITmpKoreanFontService tmpKoreanFontService)
    {
        this.tmpKoreanFontService = tmpKoreanFontService
            ?? throw new ArgumentNullException(nameof(tmpKoreanFontService));
    }

    public GameObject CreateRuntimeRoot(Canvas canvas)
    {
        GameObject rootObject = new GameObject("EventAlertRuntimeUI", typeof(RectTransform));
        rootObject.transform.SetParent(canvas.transform, false);

        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Canvas alertCanvas = rootObject.AddComponent<Canvas>();
        alertCanvas.overrideSorting = true;
        alertCanvas.sortingOrder = Mathf.Max(
            RuntimeAlertSortingOrder,
            canvas != null ? canvas.sortingOrder + 1 : RuntimeAlertSortingOrder);
        rootObject.AddComponent<GraphicRaycaster>();
        return rootObject;
    }

    public void CreateButtonRoot(
        Transform runtimeRoot,
        Canvas canvas,
        Transform previousButtonRoot,
        out Transform buttonRoot,
        out RectTransform buttonViewportRect,
        out RectTransform buttonContentRect,
        out ScrollRect buttonScrollRect)
    {
        List<Transform> existingButtons = new List<Transform>();
        if (previousButtonRoot != null)
        {
            foreach (Transform child in previousButtonRoot)
            {
                existingButtons.Add(child);
            }
        }

        GameObject rootObject = new GameObject("EventAlertButtonRoot", typeof(RectTransform), typeof(RectMask2D), typeof(ScrollRect));
        rootObject.transform.SetParent(runtimeRoot, false);
        RectTransform rootRect = rootObject.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(1f, 0f);
        rootRect.anchorMax = new Vector2(1f, 0f);
        rootRect.pivot = new Vector2(1f, 0f);

        GameObject contentObject = new GameObject("Content", typeof(RectTransform));
        contentObject.transform.SetParent(rootObject.transform, false);
        RectTransform contentRect = contentObject.GetComponent<RectTransform>();
        contentRect.anchorMin = new Vector2(0f, 0f);
        contentRect.anchorMax = new Vector2(0f, 0f);
        contentRect.pivot = new Vector2(0f, 0f);
        contentRect.anchoredPosition = Vector2.zero;

        ScrollRect scrollRect = rootObject.GetComponent<ScrollRect>();
        scrollRect.horizontal = false;
        scrollRect.vertical = true;
        scrollRect.movementType = ScrollRect.MovementType.Clamped;
        scrollRect.inertia = false;
        scrollRect.scrollSensitivity = EventAlertLayout.AlertButtonHeight + EventAlertLayout.AlertButtonSpacing;
        scrollRect.viewport = rootRect;
        scrollRect.content = contentRect;

        buttonRoot = contentObject.transform;
        buttonViewportRect = rootRect;
        buttonContentRect = contentRect;
        buttonScrollRect = scrollRect;

        foreach (Transform existingButton in existingButtons)
        {
            existingButton.SetParent(buttonRoot, false);
        }

        EventAlertLayout.ConfigureButtonViewport(canvas, buttonViewportRect, buttonContentRect);
        EventAlertLayout.LayoutButtons(buttonRoot, buttonContentRect, buttonViewportRect, buttonScrollRect);
    }

    public void BindExistingButtonRootReferences(
        Transform buttonRoot,
        out RectTransform buttonViewportRect,
        out RectTransform buttonContentRect,
        out ScrollRect buttonScrollRect)
    {
        buttonContentRect = null;
        buttonViewportRect = null;
        buttonScrollRect = null;
        if (buttonRoot == null || buttonRoot.name != "Content")
        {
            return;
        }

        buttonContentRect = buttonRoot as RectTransform;
        buttonViewportRect = buttonRoot.parent as RectTransform;
        buttonScrollRect = buttonViewportRect != null
            ? buttonViewportRect.GetComponent<ScrollRect>()
            : null;
    }

    public bool IsButtonRootReady(
        Transform buttonRoot,
        RectTransform buttonContentRect,
        RectTransform buttonViewportRect,
        ScrollRect buttonScrollRect)
    {
        return buttonRoot != null
            && buttonRoot.name == "Content"
            && buttonContentRect != null
            && buttonViewportRect != null
            && buttonScrollRect != null;
    }

    public GameObject CreateDetailPanel(Transform parent, UnityAction closeAction, out TMP_Text detailText)
    {
        GameObject panelObject = new GameObject("EventAlertDetailPanel", typeof(RectTransform), typeof(Image));
        panelObject.transform.SetParent(parent, false);

        RectTransform panelRect = panelObject.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(1f, 0.5f);
        panelRect.anchorMax = new Vector2(1f, 0.5f);
        panelRect.pivot = new Vector2(1f, 0.5f);
        panelRect.anchoredPosition = new Vector2(-24f, 0f);
        panelRect.sizeDelta = new Vector2(480f, 560f);

        Image image = panelObject.GetComponent<Image>();
        image.color = DungeonUiTheme.Panel;

        GameObject closeObject = new GameObject("CloseButton", typeof(RectTransform), typeof(Image), typeof(Button));
        closeObject.transform.SetParent(panelObject.transform, false);
        RectTransform closeRect = closeObject.GetComponent<RectTransform>();
        closeRect.anchorMin = new Vector2(1f, 1f);
        closeRect.anchorMax = new Vector2(1f, 1f);
        closeRect.pivot = new Vector2(1f, 1f);
        closeRect.anchoredPosition = new Vector2(-14f, -14f);
        closeRect.sizeDelta = new Vector2(34f, 30f);
        closeObject.GetComponent<Image>().color = DungeonUiTheme.SurfaceRaised;

        Button closeButton = closeObject.GetComponent<Button>();
        DungeonUiTheme.StyleButton(closeButton);
        if (closeAction != null)
        {
            closeButton.onClick.AddListener(closeAction);
        }

        TMP_Text closeLabel = CreateText(closeObject.transform, "Label", "X", 16, TextAlignmentOptions.Center);
        RectTransform closeLabelRect = closeLabel.GetComponent<RectTransform>();
        closeLabelRect.anchorMin = Vector2.zero;
        closeLabelRect.anchorMax = Vector2.one;
        closeLabelRect.offsetMin = Vector2.zero;
        closeLabelRect.offsetMax = Vector2.zero;

        detailText = CreateText(panelObject.transform, "DetailText", string.Empty, 16, TextAlignmentOptions.TopLeft);
        RectTransform textRect = detailText.GetComponent<RectTransform>();
        textRect.anchorMin = Vector2.zero;
        textRect.anchorMax = Vector2.one;
        textRect.offsetMin = new Vector2(18f, 64f);
        textRect.offsetMax = new Vector2(-18f, -54f);
        detailText.textWrappingMode = TextWrappingModes.Normal;
        detailText.overflowMode = TextOverflowModes.Overflow;
        detailText.color = DungeonUiTheme.TextPrimary;

        return panelObject;
    }

    private TMP_Text CreateText(
        Transform parent,
        string name,
        string text,
        int fontSize,
        TextAlignmentOptions alignment)
    {
        GameObject textObject = new GameObject(name, typeof(RectTransform), typeof(TextMeshProUGUI));
        textObject.transform.SetParent(parent, false);
        TMP_Text tmp = textObject.GetComponent<TMP_Text>();
        tmpKoreanFontService.Apply(tmp);
        tmp.text = text;
        tmp.fontSize = fontSize;
        tmp.color = Color.white;
        tmp.alignment = alignment;
        return tmp;
    }
}
