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
    private readonly EquipmentProgressionCommandPanel progressionCommands;
    private readonly ICombatCraftDefinitionCatalog craftDefinitions;
    private readonly IItemDefinitionCatalog itemDefinitions;
    private readonly Dictionary<string, string> expandedDefinitionByFacility =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> feedbackByFacility =
        new Dictionary<string, string>(StringComparer.Ordinal);

    public EquipmentCraftingPanelPresenter(
        ICombatEquipmentRuntime equipment,
        EquipmentProgressionCommandPanel progressionCommands,
        ICombatCraftDefinitionCatalog craftDefinitions = null,
        IItemDefinitionCatalog itemDefinitions = null)
    {
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
        this.progressionCommands = progressionCommands
            ?? throw new ArgumentNullException(nameof(progressionCommands));
        this.craftDefinitions = craftDefinitions;
        this.itemDefinitions = itemDefinitions;
    }

    public IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh)
    {
        List<GameObject> created = new List<GameObject>();
        if (parent == null || building == null)
        {
            return created;
        }
        if (EquipmentProgressionFacilityContract.IsProgressionFacility(building))
        {
            created.AddRange(progressionCommands.Render(
                parent,
                building,
                font,
                showFeedback,
                refresh));
            return created;
        }

        BuildingEquipmentCraftingAbility ability = building.BuildingData?
            .GetAbility<BuildingEquipmentCraftingAbility>();
        if (ability == null)
        {
            return created;
        }

        HashSet<string> craftableIds = new HashSet<string>(
            CombatCraftAllowlist.Capture(ability.CraftableEquipmentIds),
            StringComparer.Ordinal);
        CombatEquipmentDefinitionSO[] definitions = equipment.Definitions
            .Where(definition =>
                definition != null
                && craftableIds.Contains(definition.EquipmentId))
            .OrderBy(definition => definition.Kind)
            .ThenBy(definition => definition.DisplayName, StringComparer.Ordinal)
            .ToArray();
        CombatCraftDefinitionSnapshot[] ammunition = (craftDefinitions?.All
                ?? Array.Empty<CombatCraftDefinitionSnapshot>())
            .Where(value => value != null
                && value.Kind == CombatCraftOutputKind.GenericAmmunition
                && craftableIds.Contains(value.CraftDefinitionId))
            .OrderBy(value => value.CraftDefinitionId, StringComparer.Ordinal)
            .ToArray();
        CombatEquipmentCraftOrderSaveData[] queue = equipment.CraftQueue
            .Where(order =>
                order != null
                && order.destinationX == building.centerPos.x
                && order.destinationY == building.centerPos.y)
            .ToArray();
        if (definitions.Length == 0
            && ammunition.Length == 0
            && queue.Length == 0)
        {
            created.AddRange(progressionCommands.Render(
                parent,
                building,
                font,
                showFeedback,
                refresh));
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

        RenderQueue(
            parent,
            queue,
            font,
            facilityKey,
            showFeedback,
            refresh,
            created);

        expandedDefinitionByFacility.TryGetValue(
            facilityKey,
            out string expandedDefinitionId);
        foreach (CombatEquipmentDefinitionSO definition in definitions)
        {
            bool unlocked = equipment.IsDefinitionUnlocked(
                definition.EquipmentId,
                out string lockReason);
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
                        + $"\n{definition.Era} T{definition.Tier} · "
                        + $"{(definition.GrowthEquipment ? "성장형" : "일반형")} · "
                        + $"슬롯 {definition.ModuleSlotCount}"
                        + (unlocked ? string.Empty : $" · {lockReason}")
                    : definition.DisplayName
                        + (unlocked ? string.Empty : $"\n{lockReason}"),
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
                },
                interactable: unlocked);

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

        foreach (CombatCraftDefinitionSnapshot definition in ammunition)
        {
            string outputName = itemDefinitions != null
                && itemDefinitions.TryGet(
                    definition.OutputItemId,
                    out ItemDefinitionSO outputItem)
                ? outputItem.DisplayName
                : definition.OutputItemId.Value;
            GameObject row = CreateRow(
                parent,
                $"EquipmentCraft_{Sanitize(definition.CraftDefinitionId)}",
                44f);
            created.Add(row);
            AddLabel(
                row.transform,
                $"{outputName} ×{definition.OutputQuantity}",
                font,
                338f);
            AddButton(
                row.transform,
                "제작",
                font,
                true,
                () =>
                {
                    bool queued = equipment.TryQueueCraft(
                        definition.CraftDefinitionId,
                        building,
                        out string message);
                    string feedbackMessage = queued
                        ? $"{outputName} 제작을 예약했습니다."
                        : message;
                    feedbackByFacility[facilityKey] = feedbackMessage;
                    showFeedback?.Invoke(feedbackMessage);
                    refresh?.Invoke();
                });
        }

        created.AddRange(progressionCommands.Render(
            parent,
            building,
            font,
            showFeedback,
            refresh));

        return created;
    }

    private void RenderQueue(
        Transform parent,
        IReadOnlyList<CombatEquipmentCraftOrderSaveData> queue,
        TMP_FontAsset font,
        string facilityKey,
        Action<string> showFeedback,
        Action refresh,
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

            GameObject controls = CreateRow(
                parent,
                $"EquipmentCraftControls_{index}",
                34f);
            created.Add(controls);
            AddButton(
                controls.transform,
                $"작업자 {FormatWorkerMode(order.workerPolicy)}",
                font,
                false,
                () =>
                {
                    equipment.SetCraftWorkerPolicy(
                        order.orderId,
                        NextWorkerPolicy(order.workerPolicy),
                        out string message);
                    feedbackByFacility[facilityKey] = message;
                    showFeedback?.Invoke(message);
                    refresh?.Invoke();
                },
                126f);
            AddButton(
                controls.transform,
                $"최소 {GameplayUiPresentationText.Quality(order.minimumQuality)}",
                font,
                false,
                () => UpdateQualityTarget(
                    order,
                    (CraftsmanshipQualityTier)(((int)order.minimumQuality + 1) % 7),
                    order.rejectedDisposition,
                    order.repeatLimitMode,
                    facilityKey,
                    showFeedback,
                    refresh),
                104f);
            AddButton(
                controls.transform,
                FormatRejectedDisposition(order.rejectedDisposition),
                font,
                false,
                () => UpdateQualityTarget(
                    order,
                    order.minimumQuality,
                    NextRejectedDisposition(order.rejectedDisposition),
                    order.repeatLimitMode,
                    facilityKey,
                    showFeedback,
                    refresh),
                108f);
            AddButton(
                controls.transform,
                order.repeatLimitMode == QualityRepeatLimitMode.SafeLimits
                    ? "안전 한도"
                    : "성공까지",
                font,
                order.repeatLimitMode
                    == QualityRepeatLimitMode.UnlimitedUntilSuccess,
                () => UpdateQualityTarget(
                    order,
                    order.minimumQuality,
                    order.rejectedDisposition,
                    order.repeatLimitMode == QualityRepeatLimitMode.SafeLimits
                        ? QualityRepeatLimitMode.UnlimitedUntilSuccess
                        : QualityRepeatLimitMode.SafeLimits,
                    facilityKey,
                    showFeedback,
                    refresh),
                96f);
        }
    }

    private void UpdateQualityTarget(
        CombatEquipmentCraftOrderSaveData order,
        CraftsmanshipQualityTier minimumQuality,
        RejectedOutputDisposition disposition,
        QualityRepeatLimitMode repeatMode,
        string facilityKey,
        Action<string> showFeedback,
        Action refresh)
    {
        bool changed = equipment.SetCraftQualityTarget(
            order.orderId,
            minimumQuality,
            disposition,
            repeatMode,
            Mathf.Max(1, order.maximumAttempts),
            order.workBudget,
            Mathf.Max(1, order.requiredAcceptedCount),
            out string failure);
        string message = changed ? "품질 반복 설정을 변경했습니다." : failure;
        feedbackByFacility[facilityKey] = message;
        showFeedback?.Invoke(message);
        refresh?.Invoke();
    }

    private static string FormatWorkerMode(WorkerSelectionPolicySaveData policy)
    {
        WorkerSelectionPolicySaveData value = policy?.CloneNormalized()
            ?? WorkerSelectionPolicySaveData.Anyone();
        return value.mode == WorkerSelectionMode.RuleSet
            ? "민첩 7+"
            : value.sortMode == WorkerCandidateSortMode.BestExpectedQuality
                ? "품질"
                : "속도";
    }

    private static WorkerSelectionPolicySaveData NextWorkerPolicy(
        WorkerSelectionPolicySaveData policy)
    {
        WorkerSelectionPolicySaveData value = policy?.CloneNormalized()
            ?? WorkerSelectionPolicySaveData.Anyone();
        if (value.mode == WorkerSelectionMode.Anyone
            && value.sortMode == WorkerCandidateSortMode.Fastest)
        {
            return WorkerSelectionPolicySaveData.Anyone(
                WorkerCandidateSortMode.BestExpectedQuality);
        }
        if (value.mode == WorkerSelectionMode.Anyone)
        {
            return new WorkerSelectionPolicySaveData
            {
                mode = WorkerSelectionMode.RuleSet,
                matchMode = WorkerRequirementMatchMode.All,
                sortMode = WorkerCandidateSortMode.BestExpectedQuality,
                minimumSkillId = BuiltInCharacterProficiencyIds.Crafting.Value,
                minimumSkillExperience = 400
            };
        }
        return WorkerSelectionPolicySaveData.Anyone(
            WorkerCandidateSortMode.Fastest);
    }

    private static string FormatRejectedDisposition(
        RejectedOutputDisposition disposition) =>
        GameplayUiPresentationText.RejectedOutput(disposition);

    private static RejectedOutputDisposition NextRejectedDisposition(
        RejectedOutputDisposition disposition) => disposition switch
    {
        RejectedOutputDisposition.AutoDismantle =>
            RejectedOutputDisposition.KeepInStorage,
        RejectedOutputDisposition.KeepInStorage =>
            RejectedOutputDisposition.MarkForSale,
        _ => RejectedOutputDisposition.AutoDismantle
    };

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

    private static string GetFacilityKey(BuildableObject building)
    {
        return building.RequirePersistentInstanceId().Value;
    }

    internal static string Sanitize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Unknown"
            : value.Replace(':', '_').Replace('/', '_').Replace(' ', '_');
    }
}
