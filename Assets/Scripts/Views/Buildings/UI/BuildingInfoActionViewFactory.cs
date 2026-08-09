using System;
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
