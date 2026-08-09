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
        GameObject root = new(
            $"ProductionBill_{queueIndex}",
            typeof(RectTransform),
            typeof(Image),
            typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        root.GetComponent<LayoutElement>().preferredHeight = 52f;
        root.GetComponent<Image>().color = DungeonUiThemePalette.Panel(false);

        GameObject fillObject = new("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(root.transform, false);
        RectTransform fillRect = fillObject.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        float progress = bill.Status is ProductionBillStatus.Processing
            or ProductionBillStatus.WaitingForUtilities
            ? bill.ProcessingProgressRatio
            : bill.ProgressRatio;
        fillRect.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        Image fill = fillObject.GetComponent<Image>();
        fill.color = DungeonUiThemePalette.Accent(false);
        fill.raycastTarget = false;

        GameObject labelObject = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        labelObject.transform.SetParent(root.transform, false);
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = Vector2.zero;
        labelRect.anchorMax = Vector2.one;
        labelRect.offsetMin = new Vector2(8f, 3f);
        labelRect.offsetMax = new Vector2(-8f, -3f);
        TMP_Text text = labelObject.GetComponent<TMP_Text>();
        text.text = $"{queueIndex}. {bill.RecipeName} · {FormatStatus(bill.Status)} · {bill.ProgressRatio:P0}"
            + (bill.OutputCapacity > 0
                ? $" · 출력 {bill.OutputBufferedQuantity}(+{bill.ReservedOutputQuantity})/{bill.OutputCapacity}"
                : string.Empty)
            + (string.IsNullOrWhiteSpace(blockedMessage) ? string.Empty : $"\n{blockedMessage}");
        text.font = font;
        text.fontSize = 15f;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 15f;
        text.color = DungeonUiThemePalette.TextPrimary(false);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        return root;
    }

    public static string FormatStatus(ProductionBillStatus status) => status switch
    {
        ProductionBillStatus.WaitingForMaterials => "재료 운반 대기",
        ProductionBillStatus.Ready => "작업 준비 완료",
        ProductionBillStatus.InProgress => "제작 중",
        ProductionBillStatus.Suspended => "일시정지",
        ProductionBillStatus.Completed => "완료",
        ProductionBillStatus.Cancelled => "취소됨",
        ProductionBillStatus.WaitingForSupports => "연결 시설 대기",
        ProductionBillStatus.WaitingForUtilities => "전력·용수 대기",
        ProductionBillStatus.Processing => "시간 공정 진행 중",
        ProductionBillStatus.WaitingForFinishing => "마감 작업 대기",
        ProductionBillStatus.WaitingForOutputSpace => "출력 공간 대기",
        ProductionBillStatus.WaitingForStockSensor => "재고 감지반 필요",
        ProductionBillStatus.WaitingForDistributionRoute => "배출 경로 대기",
        ProductionBillStatus.WaitingForEligibleWorker => "조건에 맞는 작업자 대기",
        _ => "대기"
    };

    public static GameObject CreateRow(Transform parent, string name, float height)
    {
        GameObject row = new(
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

    public static void AddRecipeText(Transform parent, string value, TMP_FontAsset font) =>
        AddSizedText(parent, "ProductionRecipeLabel", value, font, 330f, 14f,
            DungeonUiThemePalette.TextPrimary(false));

    public static void AddRecipeProcessText(Transform parent, string value, TMP_FontAsset font) =>
        AddSizedText(parent, "ProductionProcessLabel", value, font, 245f, 13f,
            DungeonUiThemePalette.TextSecondary(false));

    public static void AddButton(
        Transform parent,
        string label,
        TMP_FontAsset font,
        bool selected,
        Action action,
        string objectName = "ProductionButton",
        float preferredWidth = 118f)
    {
        GameObject root = new(
            string.IsNullOrWhiteSpace(objectName) ? "ProductionButton" : objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        root.GetComponent<LayoutElement>().preferredWidth = Mathf.Max(52f, preferredWidth);
        Button button = root.GetComponent<Button>();
        DungeonUiThemePalette.StyleButton(button, false, false, selected);
        button.onClick.AddListener(() => action?.Invoke());
        AddButtonLabel(root.transform, label, font);
    }

    public static GameObject CreateRouteEditor(
        Transform parent,
        string name,
        string consumerLabel,
        TMP_FontAsset font,
        float preferredHeight = 52f)
    {
        GameObject root = new(
            name,
            typeof(RectTransform),
            typeof(HorizontalLayoutGroup),
            typeof(LayoutElement),
            typeof(Image));
        root.transform.SetParent(parent, false);
        root.GetComponent<LayoutElement>().preferredHeight = Mathf.Clamp(preferredHeight, 24f, 52f);
        root.GetComponent<Image>().color = DungeonUiThemePalette.Panel(false);
        HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(6, 6, 3, 3);
        layout.spacing = 4f;
        layout.childAlignment = TextAnchor.MiddleLeft;
        layout.childControlWidth = false;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;
        AddSizedText(root.transform, "ProductionRouteLabel", consumerLabel, font,
            220f, 13f, DungeonUiThemePalette.TextPrimary(false), flexible: true);
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
        GameObject root = new(
            "ProductionText",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        root.GetComponent<LayoutElement>().preferredHeight = height;
        TMP_Text text = root.GetComponent<TMP_Text>();
        ConfigureText(text, value, font, fontSize, color, TextAlignmentOptions.MidlineLeft);
        created.Add(root);
    }

    private static void AddSizedText(
        Transform parent,
        string name,
        string value,
        TMP_FontAsset font,
        float width,
        float size,
        Color color,
        bool flexible = false)
    {
        GameObject root = new(name, typeof(RectTransform), typeof(TextMeshProUGUI), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        LayoutElement layout = root.GetComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.flexibleWidth = flexible ? 1f : 0f;
        TMP_Text text = root.GetComponent<TMP_Text>();
        ConfigureText(text, value, font, size, color, TextAlignmentOptions.MidlineLeft);
        text.overflowMode = flexible ? TextOverflowModes.Truncate : TextOverflowModes.Overflow;
    }

    private static void AddButtonLabel(Transform parent, string value, TMP_FontAsset font)
    {
        GameObject root = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        root.transform.SetParent(parent, false);
        RectTransform rect = root.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(5f, 2f);
        rect.offsetMax = new Vector2(-5f, -2f);
        ConfigureText(root.GetComponent<TMP_Text>(), value, font, 14f,
            DungeonUiThemePalette.TextPrimary(false), TextAlignmentOptions.Center);
    }

    private static void ConfigureText(
        TMP_Text text,
        string value,
        TMP_FontAsset font,
        float size,
        Color color,
        TextAlignmentOptions alignment)
    {
        text.text = value;
        text.font = font;
        text.fontSize = size;
        text.enableAutoSizing = true;
        text.fontSizeMin = Mathf.Max(9f, size - 4f);
        text.fontSizeMax = size;
        text.color = color;
        text.alignment = alignment;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
    }
}
