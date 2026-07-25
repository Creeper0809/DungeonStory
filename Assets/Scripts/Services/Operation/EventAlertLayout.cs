using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public static class EventAlertLayout
{
    public const float AlertButtonWidth = 146f;
    public const float AlertButtonHeight = 38f;
    public const float AlertButtonSpacing = 5f;

    private const float AlertListWidth = 154f;
    private const float AlertListMaxHeight = 500f;
    private const int AlertListMinVisibleRows = 4;
    private const float AlertListRightOffset = 16f;
    private const float AlertListBottomOffset = 78f;
    private const float AlertListTopReserved = 116f;
    private const float AlertListVerticalPadding = 6f;

    public static void LayoutButtons(
        Transform buttonRoot,
        RectTransform buttonContentRect,
        RectTransform buttonViewportRect,
        ScrollRect buttonScrollRect)
    {
        if (buttonRoot == null)
        {
            return;
        }

        List<RectTransform> rows = new List<RectTransform>();
        foreach (Transform child in buttonRoot)
        {
            RectTransform rect = child as RectTransform;
            if (rect != null)
            {
                rows.Add(rect);
            }
        }

        RectTransform contentRect = buttonContentRect != null
            ? buttonContentRect
            : buttonRoot as RectTransform;
        float contentHeight = Mathf.Max(
            GetButtonViewportHeight(buttonViewportRect),
            GetContentHeightForRows(rows.Count));
        if (contentRect != null)
        {
            contentRect.sizeDelta = new Vector2(AlertButtonWidth, contentHeight);
        }

        for (int i = 0; i < rows.Count; i++)
        {
            rows[i].anchorMin = new Vector2(0f, 0f);
            rows[i].anchorMax = new Vector2(0f, 0f);
            rows[i].pivot = new Vector2(0f, 0f);
            rows[i].sizeDelta = new Vector2(AlertButtonWidth, AlertButtonHeight);
            rows[i].anchoredPosition = new Vector2(0f, AlertListVerticalPadding + (i * (AlertButtonHeight + AlertButtonSpacing)));
        }

        if (buttonScrollRect != null)
        {
            buttonScrollRect.verticalNormalizedPosition = 0f;
        }
    }

    public static void ConfigureButtonViewport(
        Canvas canvas,
        RectTransform buttonViewportRect,
        RectTransform buttonContentRect)
    {
        if (buttonViewportRect == null)
        {
            return;
        }

        buttonViewportRect.anchorMin = new Vector2(1f, 0f);
        buttonViewportRect.anchorMax = new Vector2(1f, 0f);
        buttonViewportRect.pivot = new Vector2(1f, 0f);
        buttonViewportRect.anchoredPosition = new Vector2(-AlertListRightOffset, AlertListBottomOffset);
        buttonViewportRect.sizeDelta = new Vector2(AlertListWidth, GetAlertListHeight(canvas));

        if (buttonContentRect != null)
        {
            buttonContentRect.anchorMin = new Vector2(0f, 0f);
            buttonContentRect.anchorMax = new Vector2(0f, 0f);
            buttonContentRect.pivot = new Vector2(0f, 0f);
            buttonContentRect.anchoredPosition = Vector2.zero;
            buttonContentRect.sizeDelta = new Vector2(AlertButtonWidth, buttonViewportRect.sizeDelta.y);
        }
    }

    private static float GetAlertListHeight(Canvas canvas)
    {
        RectTransform canvasRect = canvas != null ? canvas.transform as RectTransform : null;
        if (canvasRect == null || canvasRect.rect.height <= 0f)
        {
            return GetContentHeightForRows(GetMaxVisibleRows());
        }

        float availableHeight = canvasRect.rect.height - AlertListTopReserved - AlertListBottomOffset;
        int visibleRows = GetVisibleRowsForHeight(availableHeight);
        return GetContentHeightForRows(visibleRows);
    }

    private static float GetButtonViewportHeight(RectTransform buttonViewportRect)
    {
        if (buttonViewportRect != null && buttonViewportRect.rect.height > 0f)
        {
            return buttonViewportRect.rect.height;
        }

        return GetContentHeightForRows(GetMaxVisibleRows());
    }

    private static int GetVisibleRowsForHeight(float availableHeight)
    {
        int maxRows = GetMaxVisibleRows();
        if (availableHeight <= AlertListVerticalPadding * 2f + AlertButtonHeight)
        {
            return 1;
        }

        int rowsBySpace = Mathf.FloorToInt(
            (availableHeight - (AlertListVerticalPadding * 2f) + AlertButtonSpacing)
            / (AlertButtonHeight + AlertButtonSpacing));
        int minRows = Mathf.Min(AlertListMinVisibleRows, maxRows);
        return Mathf.Clamp(rowsBySpace, minRows, maxRows);
    }

    private static int GetMaxVisibleRows()
    {
        return Mathf.Max(1, Mathf.FloorToInt(
            (AlertListMaxHeight - (AlertListVerticalPadding * 2f) + AlertButtonSpacing)
            / (AlertButtonHeight + AlertButtonSpacing)));
    }

    private static float GetContentHeightForRows(int rowCount)
    {
        int rows = Mathf.Max(1, rowCount);
        return (AlertListVerticalPadding * 2f)
            + (rows * AlertButtonHeight)
            + ((rows - 1) * AlertButtonSpacing);
    }
}
