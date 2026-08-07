using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public static class ProductionBuildingViewFactory
{
    public static GameObject CreateProgress(
        Transform parent,
        ProductionBillSnapshot bill,
        TMP_FontAsset font,
        int queueIndex,
        string blockedMessage)
    {
        GameObject root = new GameObject(
            $"ProductionBill_{queueIndex}",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        root.GetComponent<LayoutElement>().preferredHeight = 48f;
        root.GetComponent<Image>().color =
            DungeonUiThemePalette.Panel(highContrast: false);

        GameObject fillObject = new GameObject(
            "Fill",
            typeof(RectTransform),
            typeof(Image));
        fillObject.transform.SetParent(root.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        float visibleProgress = bill.Status == ProductionBillStatus.Processing
            || bill.Status == ProductionBillStatus.WaitingForUtilities
                ? bill.ProcessingProgressRatio
                : bill.ProgressRatio;
        fillRect.anchorMax = new Vector2(visibleProgress, 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fill = fillObject.GetComponent<Image>();
        fill.color = DungeonUiThemePalette.Accent(highContrast: false);
        fill.raycastTarget = false;

        GameObject labelObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(root.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 3f);
        labelRect.offsetMax = new Vector2(-8f, -3f);
        TMP_Text text = labelObject.GetComponent<TMP_Text>();
        text.text = $"{queueIndex}. {bill.RecipeName} · "
            + $"{FormatStatus(bill.Status)} · {bill.ProgressRatio:P0}"
            + (bill.OutputCapacity > 0
                ? $" · 출력 {bill.OutputBufferedQuantity}"
                    + $"(+{bill.ReservedOutputQuantity})/{bill.OutputCapacity}"
                : string.Empty)
            + (string.IsNullOrWhiteSpace(blockedMessage)
                ? string.Empty
                : $"\n{blockedMessage}");
        text.font = font;
        text.fontSize = 15f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 15f;
        text.color = DungeonUiThemePalette.TextPrimary(highContrast: false);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return root;
    }

    public static string FormatStatus(ProductionBillStatus status)
    {
        return status switch
        {
            ProductionBillStatus.WaitingForMaterials => "재료 운반 대기",
            ProductionBillStatus.Ready => "작업 가능",
            ProductionBillStatus.InProgress => "제작 중",
            ProductionBillStatus.Suspended => "일시 중지",
            ProductionBillStatus.Completed => "완료",
            ProductionBillStatus.Cancelled => "취소됨",
            ProductionBillStatus.WaitingForSupports => "연결 시설 대기",
            ProductionBillStatus.WaitingForUtilities => "설비 대기",
            ProductionBillStatus.Processing => "시간 공정 중",
            ProductionBillStatus.WaitingForFinishing => "마감 작업 대기",
            ProductionBillStatus.WaitingForOutputSpace => "출력 버퍼 가득 참",
            ProductionBillStatus.WaitingForStockSensor => "재고 감지반 필요",
            ProductionBillStatus.WaitingForDistributionRoute => "분기 배출 경로 대기",
            _ => status.ToString()
        };
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

    public static void AddRecipeText(
        Transform parent,
        string value,
        TMP_FontAsset font)
    {
        GameObject textObject = new GameObject(
            "ProductionRecipeLabel",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        textObject.GetComponent<LayoutElement>().preferredWidth = 330f;
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.font = font;
        text.fontSize = 14f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 14f;
        text.color = DungeonUiThemePalette.TextPrimary(highContrast: false);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
    }

    public static void AddRecipeProcessText(
        Transform parent,
        string value,
        TMP_FontAsset font)
    {
        GameObject textObject = new GameObject(
            "ProductionProcessLabel",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        textObject.GetComponent<LayoutElement>().preferredWidth = 245f;
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.font = font;
        text.fontSize = 13f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 9f;
        text.fontSizeMax = 13f;
        text.color = DungeonUiThemePalette.TextSecondary(highContrast: false);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
    }

    public static void AddButton(
        Transform parent,
        string label,
        TMP_FontAsset font,
        bool selected,
        Action action,
        string objectName = "ProductionButton",
        float preferredWidth = 118f)
    {
        GameObject buttonObject = new GameObject(
            string.IsNullOrWhiteSpace(objectName)
                ? "ProductionButton"
                : objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<LayoutElement>().preferredWidth =
            Mathf.Max(52f, preferredWidth);
        Button button = buttonObject.GetComponent<Button>();
        DungeonUiThemePalette.StyleButton(
            button,
            highContrast: false,
            reducedMotion: false,
            selected: selected);
        button.onClick.AddListener(() => action?.Invoke());

        GameObject textObject = new GameObject(
            "Label",
            typeof(RectTransform),
            typeof(TextMeshProUGUI));
        textObject.transform.SetParent(buttonObject.transform, false);
        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(5f, 2f);
        rect.offsetMax = new Vector2(-5f, -2f);
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = label;
        text.font = font;
        text.fontSize = 14f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 14f;
        text.color = DungeonUiThemePalette.TextPrimary(highContrast: false);
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
    }

    public static GameObject CreateRouteEditor(
        Transform parent,
        string name,
        string consumerLabel,
        TMP_FontAsset font,
        float preferredHeight = 52f)
    {
        GameObject root = new GameObject(
            name,
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement),
            typeof(Image));
        root.transform.SetParent(parent, false);
        root.GetComponent<LayoutElement>().preferredHeight =
            Mathf.Clamp(preferredHeight, 24f, 52f);
        root.GetComponent<Image>().color =
            DungeonUiThemePalette.Panel(highContrast: false);

        HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 3, 3);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        GameObject labelObject = new GameObject(
            "ProductionRouteLabel",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        labelObject.transform.SetParent(root.transform, false);
        LayoutElement labelLayout = labelObject.GetComponent<LayoutElement>();
        labelLayout.preferredWidth = 220f;
        labelLayout.flexibleWidth = 1f;
        TMP_Text label = labelObject.GetComponent<TMP_Text>();
        label.text = consumerLabel;
        label.font = font;
        label.fontSize = 13f;
        label.enableAutoSizing = true;
        label.fontSizeMin = 9f;
        label.fontSizeMax = 13f;
        label.color = DungeonUiThemePalette.TextPrimary(highContrast: false);
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.textWrappingMode = TextWrappingModes.Normal;
        label.overflowMode = TextOverflowModes.Truncate;
        label.raycastTarget = false;

        return root;
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
            "ProductionText",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        textObject.GetComponent<LayoutElement>().preferredHeight = height;
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        created.Add(textObject);
    }
}
