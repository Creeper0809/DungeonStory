using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public interface IEquipmentCraftingPanelPresenter
{
    IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh);
}

public sealed class EquipmentCraftingPanelPresenter :
    IEquipmentCraftingPanelPresenter
{
    private readonly ICombatEquipmentRuntime equipment;
    private readonly Dictionary<string, string> expandedDefinitionByFacility =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> feedbackByFacility =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public EquipmentCraftingPanelPresenter(ICombatEquipmentRuntime equipment)
    {
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
    }

    public IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh)
    {
        List<GameObject> created = new List<GameObject>();
        BuildingEquipmentCraftingAbility ability = building?
            .BuildingData?
            .GetAbility<BuildingEquipmentCraftingAbility>();
        if (parent == null || building == null || ability == null)
        {
            return created;
        }

        HashSet<string> craftableIds = new HashSet<string>(
            ability.CraftableEquipmentIds
                .Where(id => !string.IsNullOrWhiteSpace(id)),
            StringComparer.Ordinal);
        CombatEquipmentDefinitionSO[] definitions = equipment.Definitions
            .Where(definition =>
                definition != null
                && craftableIds.Contains(definition.EquipmentId))
            .OrderBy(definition => definition.Kind)
            .ThenBy(definition => definition.DisplayName, StringComparer.Ordinal)
            .ToArray();
        CombatEquipmentCraftOrderSaveData[] queue = equipment.CraftQueue
            .Where(order =>
                order != null
                && order.destinationX == building.centerPos.x
                && order.destinationY == building.centerPos.y)
            .ToArray();
        if (definitions.Length == 0 && queue.Length == 0)
        {
            return created;
        }

        AddText(parent, "장비 제작", font, 21f, DungeonUiTheme.TextPrimary, 34f, created);
        AddText(
            parent,
            $"대기열 {queue.Length}건 · 재질별 성능과 원료를 비교해 주문합니다.",
            font,
            14f,
            DungeonUiTheme.TextSecondary,
            30f,
            created);
        string facilityKey = GetFacilityKey(building);
        if (feedbackByFacility.TryGetValue(
                facilityKey,
                out string feedback)
            && !string.IsNullOrWhiteSpace(feedback))
        {
            AddText(
                parent,
                feedback,
                font,
                14f,
                DungeonUiTheme.Warning,
                28f,
                created);
        }

        RenderQueue(parent, queue, font, created);

        expandedDefinitionByFacility.TryGetValue(
            facilityKey,
            out string expandedDefinitionId);
        foreach (CombatEquipmentDefinitionSO definition in definitions)
        {
            IReadOnlyList<CraftMaterialDefinitionSO> materials =
                equipment.GetAllowedMaterials(definition.EquipmentId);
            if (materials.Count == 0)
            {
                continue;
            }

            CombatEquipmentCraftMaterialPolicySaveData policy =
                equipment.GetCraftMaterialPolicy(
                    definition.EquipmentId,
                    building);
            CraftMaterialDefinitionSO preferred = ResolvePreferredMaterial(
                materials,
                policy);
            GameObject row = CreateRow(
                parent,
                $"EquipmentCraft_{Sanitize(definition.EquipmentId)}",
                54f);
            created.Add(row);
            AddLabel(
                row.transform,
                preferred != null
                    ? $"{preferred.DisplayName} {definition.DisplayName}"
                    : definition.DisplayName,
                font,
                238f);
            AddButton(
                row.transform,
                string.Equals(
                    expandedDefinitionId,
                    definition.EquipmentId,
                    StringComparison.Ordinal)
                        ? "접기"
                        : "재질",
                font,
                false,
                () =>
                {
                    expandedDefinitionByFacility[facilityKey] =
                        string.Equals(
                            expandedDefinitionByFacility.TryGetValue(
                                facilityKey,
                                out string current)
                                    ? current
                                    : string.Empty,
                            definition.EquipmentId,
                            StringComparison.Ordinal)
                                ? string.Empty
                                : definition.EquipmentId;
                    refresh?.Invoke();
                });
            AddButton(
                row.transform,
                "제작",
                font,
                true,
                () =>
                {
                    bool queued = equipment.TryQueueCraft(
                        definition.EquipmentId,
                        building,
                        out string message);
                    string feedbackMessage = queued
                        ? $"{definition.DisplayName} 제작을 예약했습니다."
                        : message;
                    feedbackByFacility[facilityKey] = feedbackMessage;
                    showFeedback?.Invoke(feedbackMessage);
                    refresh?.Invoke();
                });

            if (string.Equals(
                    expandedDefinitionId,
                    definition.EquipmentId,
                    StringComparison.Ordinal))
            {
                RenderMaterialPolicy(
                    parent,
                    building,
                    definition,
                    materials,
                    policy,
                    font,
                    showFeedback,
                    refresh,
                    created);
            }
        }

        return created;
    }

    private void RenderQueue(
        Transform parent,
        IReadOnlyList<CombatEquipmentCraftOrderSaveData> queue,
        TMP_FontAsset font,
        ICollection<GameObject> created)
    {
        for (int index = 0; index < queue.Count; index++)
        {
            CombatEquipmentCraftOrderSaveData order = queue[index];
            equipment.TryGetDefinition(
                order.definitionId,
                out CombatEquipmentDefinitionSO definition);
            string materialName = equipment.GetAllowedMaterials(order.definitionId)
                .FirstOrDefault(material => string.Equals(
                    material.MaterialId,
                    order.materialId,
                    StringComparison.Ordinal))
                ?.DisplayName ?? order.materialId;
            float progress = order.requiredWork > 0f
                ? Mathf.Clamp01(order.completedWork / order.requiredWork)
                : 0f;
            string status = order.materialsReady
                ? $"제작 {progress:P0}"
                : "재료 운반 대기";
            AddText(
                parent,
                $"{index + 1}. {materialName} {definition?.DisplayName ?? order.definitionId}"
                + $" · {status}",
                font,
                14f,
                order.materialsReady
                    ? DungeonUiTheme.TextPrimary
                    : DungeonUiTheme.Warning,
                28f,
                created);
        }
    }

    private void RenderMaterialPolicy(
        Transform parent,
        BuildableObject building,
        CombatEquipmentDefinitionSO definition,
        IReadOnlyList<CraftMaterialDefinitionSO> materials,
        CombatEquipmentCraftMaterialPolicySaveData policy,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh,
        ICollection<GameObject> created)
    {
        Dictionary<string, CraftMaterialDefinitionSO> byId = materials
            .ToDictionary(
                material => material.MaterialId,
                material => material,
                StringComparer.Ordinal);
        for (int index = 0; index < policy.priorityMaterialIds.Count; index++)
        {
            string materialId = policy.priorityMaterialIds[index];
            if (!byId.TryGetValue(
                    materialId,
                    out CraftMaterialDefinitionSO material))
            {
                continue;
            }

            equipment.TryGetPreviewStats(
                definition.EquipmentId,
                material.MaterialId,
                out CombatEquipmentDerivedStats stats);
            bool allowed = policy.allowedMaterialIds.Contains(
                material.MaterialId,
                StringComparer.Ordinal);
            GameObject row = CreateMaterialRow(
                parent,
                material,
                stats,
                index + 1,
                allowed,
                font,
                isAllowed =>
                {
                    equipment.SetCraftMaterialAllowed(
                        definition.EquipmentId,
                        material.MaterialId,
                        building,
                        isAllowed,
                        out string message);
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        feedbackByFacility[GetFacilityKey(building)] = message;
                        showFeedback?.Invoke(message);
                    }

                    refresh?.Invoke();
                },
                offset =>
                {
                    equipment.MoveCraftMaterialPriority(
                        definition.EquipmentId,
                        material.MaterialId,
                        building,
                        offset,
                        out string message);
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        feedbackByFacility[GetFacilityKey(building)] = message;
                        showFeedback?.Invoke(message);
                    }

                    refresh?.Invoke();
                });
            created.Add(row);
        }
    }

    private static CraftMaterialDefinitionSO ResolvePreferredMaterial(
        IReadOnlyList<CraftMaterialDefinitionSO> materials,
        CombatEquipmentCraftMaterialPolicySaveData policy)
    {
        return policy.priorityMaterialIds
            .Where(id => policy.allowedMaterialIds.Contains(
                id,
                StringComparer.Ordinal))
            .Select(id => materials.FirstOrDefault(material =>
                string.Equals(
                    material.MaterialId,
                    id,
                    StringComparison.Ordinal)))
            .FirstOrDefault(material => material != null);
    }

    private static GameObject CreateMaterialRow(
        Transform parent,
        CraftMaterialDefinitionSO material,
        CombatEquipmentDerivedStats stats,
        int priority,
        bool allowed,
        TMP_FontAsset font,
        Action<bool> setAllowed,
        Action<int> movePriority)
    {
        GameObject row = CreateRow(
            parent,
            $"EquipmentMaterial_{Sanitize(material.MaterialId)}",
            58f);
        Image background = row.AddComponent<Image>();
        background.color = allowed
            ? Color.Lerp(DungeonUiTheme.Surface, stats.Tint, 0.18f)
            : DungeonUiTheme.SurfaceMuted;

        GameObject toggleObject = new GameObject(
            "Allowed",
            typeof(RectTransform),
            typeof(Toggle),
            typeof(LayoutElement));
        toggleObject.transform.SetParent(row.transform, false);
        toggleObject.GetComponent<LayoutElement>().preferredWidth = 34f;
        Toggle toggle = toggleObject.GetComponent<Toggle>();

        GameObject checkObject = new GameObject(
            "Checkmark",
            typeof(RectTransform),
            typeof(Image));
        checkObject.transform.SetParent(toggleObject.transform, false);
        RectTransform checkRect = checkObject.GetComponent<RectTransform>();
        checkRect.anchorMin = new Vector2(0.5f, 0.5f);
        checkRect.anchorMax = new Vector2(0.5f, 0.5f);
        checkRect.sizeDelta = new Vector2(20f, 20f);
        Image check = checkObject.GetComponent<Image>();
        check.color = DungeonUiTheme.Accent;
        toggle.graphic = check;
        toggle.SetIsOnWithoutNotify(allowed);
        toggle.onValueChanged.AddListener(value => setAllowed?.Invoke(value));

        string rare = material.RareMaterial ? " · 희귀" : string.Empty;
        AddLabel(
            row.transform,
            $"{priority}. {material.DisplayName}{rare}\n"
            + $"피해 ×{stats.DamageMultiplier:0.00} · 관통/방어 ×"
            + $"{stats.PenetrationDefenseMultiplier:0.00} · 내구 "
            + $"{stats.MaxDurability:0.#} · 무게 {stats.Weight:0.##} · 가치 ×"
            + $"{stats.ValueMultiplier:0.00}",
            font,
            354f);
        AddButton(row.transform, "↑", font, false, () => movePriority?.Invoke(-1), 38f);
        AddButton(row.transform, "↓", font, false, () => movePriority?.Invoke(1), 38f);
        return row;
    }

    private static GameObject CreateRow(
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

    private static void AddLabel(
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

    private static void AddButton(
        Transform parent,
        string label,
        TMP_FontAsset font,
        bool selected,
        Action action,
        float width = 82f)
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

    private static void AddText(
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

    private static string GetFacilityKey(BuildableObject building)
    {
        return $"{building.id}:{building.centerPos.x}:{building.centerPos.y}";
    }

    private static string Sanitize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Unknown"
            : value.Replace(':', '_').Replace('/', '_').Replace(' ', '_');
    }
}
