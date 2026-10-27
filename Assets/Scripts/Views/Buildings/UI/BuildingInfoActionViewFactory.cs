using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

internal static class BuildingInfoActionViewFactory
{
    public static GameObject CreateCraftButton(
        Transform parent,
        string label,
        Action callback,
        TMP_FontAsset font)
    {
        GameObject root = CreateRoot(parent, "BuildingCraftButton", 180f, 46f, true);
        Button button = root.GetComponent<Button>();
        DungeonUiTheme.StyleButton(button, selected: true);
        button.onClick.AddListener(() => callback?.Invoke());
        TMP_Text text = CreateLabel(root.transform, font);
        text.text = label;
        text.fontSizeMin = 13f;
        text.fontSizeMax = 20f;
        text.textWrappingMode = TextWrappingModes.Normal;
        return root;
    }

    public static GameObject CreateConstructionProgressBar(
        Transform parent,
        WorkOrderProgressState order,
        TMP_FontAsset font)
    {
        GameObject root = CreateRoot(parent, "BuildingConstructionProgress", 360f, 38f, false);
        CreateFill(root.transform, order?.ProgressRatio ?? 0f);
        TMP_Text text = CreateLabel(root.transform, font);
        text.text = $"공사 진행 {Mathf.RoundToInt((order?.ProgressRatio ?? 0f) * 100f)}%";
        text.fontSizeMin = 12f;
        text.fontSizeMax = 18f;
        return root;
    }

    public static GameObject CreateMaintenanceProgressBar(
        Transform parent,
        string equipmentName,
        CombatEquipmentRepairOrder order,
        TMP_FontAsset font)
    {
        GameObject root = CreateRoot(parent, "BuildingMaintenanceProgress", 360f, 48f, false);
        CreateFill(root.transform, order.ProgressRatio);
        TMP_Text text = CreateLabel(root.transform, font);
        text.text = $"{equipmentName} · {FormatRepairState(order.state)}"
            + $" · {order.ProgressRatio:P0} · 재료 {order.requiredMaterialAmount}";
        text.fontSizeMin = 11f;
        text.fontSizeMax = 17f;
        return root;
    }

    public static GameObject CreateCraftStatus(
        Transform parent,
        string message,
        TMP_FontAsset font)
    {
        GameObject root = new(
            "BuildingCraftStatus",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        LayoutElement layout = root.GetComponent<LayoutElement>();
        layout.preferredWidth = 360f;
        layout.preferredHeight = 46f;
        TMP_Text text = root.GetComponent<TMP_Text>();
        text.text = message;
        text.color = DungeonUiTheme.TextSecondary;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.fontSizeMin = 12f;
        text.fontSizeMax = 18f;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.font = font;
        return root;
    }

    internal static GameObject CreateRow(
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

    internal static void AddLabel(
        Transform parent,
        string value,
        TMP_FontAsset font,
        float width)
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
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
    }

    internal static void AddButton(
        Transform parent,
        string label,
        TMP_FontAsset font,
        bool selected,
        Action action,
        float width = 82f,
        bool interactable = true,
        string objectName = "Button")
    {
        GameObject buttonObject = new GameObject(
            objectName,
            typeof(RectTransform),
            typeof(Image),
            typeof(Button),
            typeof(LayoutElement));
        buttonObject.transform.SetParent(parent, false);
        buttonObject.GetComponent<LayoutElement>().preferredWidth = width;
        Button button = buttonObject.GetComponent<Button>();
        DungeonUiTheme.StyleButton(button, selected);
        button.interactable = interactable;
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
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.Center;
        text.raycastTarget = false;
    }

    internal static void AddText(
        Transform parent,
        string value,
        TMP_FontAsset font,
        float fontSize,
        Color color,
        float height,
        ICollection<GameObject> created)
    {
        GameObject textObject = new GameObject(
            "EquipmentCraftingText",
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

    internal static string Sanitize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Unknown"
            : value.Replace(':', '_').Replace('/', '_').Replace(' ', '_');
    }

    private static GameObject CreateRoot(
        Transform parent,
        string name,
        float width,
        float height,
        bool button)
    {
        GameObject root = button
            ? new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement))
            : new GameObject(name, typeof(RectTransform), typeof(Image), typeof(LayoutElement));
        root.transform.SetParent(parent, false);
        LayoutElement layout = root.GetComponent<LayoutElement>();
        layout.preferredWidth = width;
        layout.preferredHeight = height;
        root.GetComponent<Image>().color = button ? DungeonUiTheme.Accent : DungeonUiTheme.Panel;
        return root;
    }

    private static void CreateFill(Transform parent, float progress)
    {
        GameObject fillObject = new("Fill", typeof(RectTransform), typeof(Image));
        fillObject.transform.SetParent(parent, false);
        RectTransform rect = fillObject.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = new Vector2(Mathf.Clamp01(progress), 1f);
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        Image fill = fillObject.GetComponent<Image>();
        fill.color = DungeonUiTheme.Accent;
        fill.raycastTarget = false;
    }

    private static TMP_Text CreateLabel(Transform parent, TMP_FontAsset font)
    {
        GameObject label = new("Label", typeof(RectTransform), typeof(TextMeshProUGUI));
        label.transform.SetParent(parent, false);
        RectTransform rect = label.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = new Vector2(8f, 2f);
        rect.offsetMax = new Vector2(-8f, -2f);
        TMP_Text text = label.GetComponent<TMP_Text>();
        text.color = DungeonUiTheme.TextPrimary;
        text.alignment = TextAlignmentOptions.Center;
        text.enableAutoSizing = true;
        text.raycastTarget = false;
        text.font = font;
        return text;
    }

    private static string FormatRepairState(CombatEquipmentRepairOrderState state) => state switch
    {
        CombatEquipmentRepairOrderState.PendingCombatEnd => "교전 종료 대기",
        CombatEquipmentRepairOrderState.WaitingForDelivery => "재료 운반 대기",
        CombatEquipmentRepairOrderState.Ready => "수리 준비 완료",
        CombatEquipmentRepairOrderState.InProgress => "수리 중",
        _ => "대기"
    };
}
