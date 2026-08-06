using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static ResearchTreeViewFactory;

public sealed class ResearchTreeDetailScrollView
{
    private ResearchTreeDetailScrollView(
        ScrollRect scroll,
        RectTransform viewport,
        TMP_Text text)
    {
        Scroll = scroll;
        Viewport = viewport;
        Text = text;
    }

    public ScrollRect Scroll { get; }
    public RectTransform Viewport { get; }
    public TMP_Text Text { get; }

    public static ResearchTreeDetailScrollView Create(
        ResearchTreeViewFactory viewFactory,
        RectTransform parent)
    {
        RectTransform root = CreateRect("DetailScroll", parent);
        SetRect(root, new Vector2(0f, 0.17f), Vector2.one, 0f, 0f, 0f, 0f);
        ScrollRect scroll = root.gameObject.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Clamped;
        scroll.scrollSensitivity = 28f;

        RectTransform viewport = CreateRect("DetailViewport", root);
        Stretch(viewport);
        viewport.gameObject.AddComponent<RectMask2D>();

        TMP_Text text = viewFactory.CreateText(
            viewport,
            "DetailText",
            string.Empty,
            17f,
            TextAlignmentOptions.TopLeft);
        text.textWrappingMode = TextWrappingModes.Normal;
        text.rectTransform.anchorMin = new Vector2(0f, 1f);
        text.rectTransform.anchorMax = Vector2.one;
        text.rectTransform.pivot = new Vector2(0.5f, 1f);
        text.rectTransform.anchoredPosition = Vector2.zero;
        text.rectTransform.sizeDelta = Vector2.zero;
        scroll.viewport = viewport;
        scroll.content = text.rectTransform;
        return new ResearchTreeDetailScrollView(scroll, viewport, text);
    }

    public void RefreshLayout()
    {
        Canvas.ForceUpdateCanvases();
        Text.ForceMeshUpdate();
        float contentHeight = Mathf.Max(Viewport.rect.height, Text.preferredHeight);
        Text.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Vertical,
            contentHeight);
    }

    public void ScrollToTop()
    {
        Scroll.verticalNormalizedPosition = 1f;
    }
}
