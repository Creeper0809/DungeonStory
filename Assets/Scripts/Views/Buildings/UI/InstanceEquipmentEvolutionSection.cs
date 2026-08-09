using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using static InstanceEvolutionPanelPresentation;
using static InstanceEvolutionPanelView;

public sealed class InstanceEquipmentEvolutionSection
{
    private const string StabilizerItemId = "resource:mana-crystal";

    private readonly IEquipmentEvolutionRuntime equipmentEvolution;
    private readonly IAttunementRuntime attunement;
    private readonly ICombatEquipmentRuntime equipment;
    private readonly IWorldItemStackRuntime worldItems;
    private readonly IReforgePrecisionService precisionReforge;
    private readonly IEquipmentOverclockRuntime overclock;
    private readonly InstanceEvolutionPanelPresentation presentation;
    private readonly Dictionary<string, string> selectedEquipmentByFacility =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly HashSet<string> stabilizerEnabledFacilities =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, ReforgePrecisionSelection>
        precisionByEquipment = new Dictionary<string, ReforgePrecisionSelection>(
            StringComparer.Ordinal);

    public InstanceEquipmentEvolutionSection(
        IEquipmentEvolutionRuntime equipmentEvolution,
        IAttunementRuntime attunement,
        ICombatEquipmentRuntime equipment,
        IWorldItemStackRuntime worldItems,
        IReforgePrecisionService precisionReforge,
        IEquipmentOverclockRuntime overclock,
        InstanceEvolutionPanelPresentation presentation)
    {
        this.equipmentEvolution = equipmentEvolution
            ?? throw new ArgumentNullException(nameof(equipmentEvolution));
        this.attunement = attunement
            ?? throw new ArgumentNullException(nameof(attunement));
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
        this.worldItems = worldItems
            ?? throw new ArgumentNullException(nameof(worldItems));
        this.precisionReforge = precisionReforge
            ?? throw new ArgumentNullException(nameof(precisionReforge));
        this.overclock = overclock
            ?? throw new ArgumentNullException(nameof(overclock));
        this.presentation = presentation
            ?? throw new ArgumentNullException(nameof(presentation));
    }

    public void Render(
        Transform parent,
        BuildableObject building,
        string facilityKey,
        string selectedCatalyst,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh,
        ICollection<GameObject> created)
    {
        AddText(
            parent,
            "장비 재단조·재귀속",
            font,
            21f,
            DungeonUiTheme.TextPrimary,
            34f,
            created);

        if (equipmentEvolution.TryGetActiveReforge(
                building,
                out EvolutionReforgeOrder reforge))
        {
            AddProgress(
                parent,
                "장비 재단조",
                reforge.ProgressRatio,
                reforge.state,
                font,
                created);
        }
        else if (attunement.TryGetActiveReattunement(
                     building,
                     out EquipmentReattunementOrder reattunement))
        {
            AddProgress(
                parent,
                "장비 재귀속",
                reattunement.ProgressRatio,
                reattunement.state,
                font,
                created);
        }

        CombatEquipmentInstance[] available = equipment.Instances
            .Where(instance => instance != null
                && !string.IsNullOrWhiteSpace(instance.sourceStackId)
                && instance.worldState == CombatEquipmentWorldState.Stored)
            .OrderByDescending(instance => instance.evolution?.reforgeReady ?? false)
            .ThenBy(instance => presentation.GetEquipmentName(instance), StringComparer.Ordinal)
            .Take(12)
            .ToArray();
        if (available.Length == 0)
        {
            AddText(
                parent,
                "창고에 보관된 재단조 가능 장비가 없습니다.",
                font,
                14f,
                DungeonUiTheme.TextSecondary,
                28f,
                created);
            return;
        }

        string selectedId = ResolveSelectedEquipment(facilityKey, available);
        foreach (CombatEquipmentInstance instance in available)
        {
            bool selected = string.Equals(
                selectedId,
                instance.instanceId,
                StringComparison.Ordinal);
            GameObject row = CreateRow(
                parent,
                $"EquipmentEvolutionSelect_{Sanitize(instance.instanceId)}",
                46f);
            created.Add(row);
            EquipmentEvolutionState state = instance.evolution
                ?? new EquipmentEvolutionState();
            AddLabel(
                row.transform,
                $"{presentation.GetEquipmentName(instance)} · 세대 {state.generation} · "
                + $"{state.mastery:0.#}/{state.RequiredMastery:0.#}",
                font,
                384f,
                selected
                    ? DungeonUiTheme.Accent
                    : DungeonUiTheme.TextPrimary);
            string capturedId = instance.instanceId;
            AddButton(
                row.transform,
                selected ? "선택됨" : "선택",
                font,
                selected,
                true,
                () =>
                {
                    selectedEquipmentByFacility[facilityKey] = capturedId;
                    refresh?.Invoke();
                },
                76f);
        }

        CombatEquipmentInstance selectedEquipment = available.First(instance =>
            string.Equals(
                instance.instanceId,
                selectedId,
                StringComparison.Ordinal));
        RenderSelectedEquipment(
            parent,
            building,
            facilityKey,
            selectedEquipment,
            selectedCatalyst,
            font,
            showFeedback,
            refresh,
            created);
    }

    private void RenderSelectedEquipment(
        Transform parent,
        BuildableObject building,
        string facilityKey,
        CombatEquipmentInstance instance,
        string selectedCatalyst,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh,
        ICollection<GameObject> created)
    {
        EquipmentEvolutionState state = instance.evolution
            ?? new EquipmentEvolutionState();
        EquipmentReforgePreview preview =
            equipmentEvolution.GetPreview(instance.instanceId);
        AddText(
            parent,
            $"{presentation.GetEquipmentName(instance)} · 공명 "
            + $"{state.activeHistoricalNodeIds.Count}/{state.ResonanceBudget}\n"
            + $"진화 방향 {FormatDirection(preview.Direction)} · 예상 배율 "
            + $"{preview.MinimumMultiplier:0.00}~{preview.MaximumMultiplier:0.00}",
            font,
            14f,
            DungeonUiTheme.TextPrimary,
            48f,
            created);
        RenderEquipmentOverclock(
            parent,
            instance,
            font,
            showFeedback,
            refresh,
            created);

        bool hasStabilizer = worldItems.GetAllStacks().Any(stack =>
            stack != null
            && string.Equals(
                stack.ItemId,
                StabilizerItemId,
                StringComparison.Ordinal)
            && stack.Quantity > 0
            && !stack.Forbidden
            && !stack.IsReserved
            && stack.State is WorldItemStackState.Loose
                or WorldItemStackState.Stored);
        bool stabilizerEnabled =
            stabilizerEnabledFacilities.Contains(facilityKey);
        GameObject stabilizerRow = CreateRow(
            parent,
            "EquipmentEvolutionStabilizer",
            42f);
        created.Add(stabilizerRow);
        AddLabel(
            stabilizerRow.transform,
            hasStabilizer
                ? "안정제: 마나 결정 1개 · 위험 부담 억제"
                : "안정제: 마나 결정 재고 없음",
            font,
            384f,
            hasStabilizer
                ? DungeonUiTheme.TextPrimary
                : DungeonUiTheme.Warning);
        AddButton(
            stabilizerRow.transform,
            stabilizerEnabled ? "사용" : "미사용",
            font,
            stabilizerEnabled,
            hasStabilizer,
            () =>
            {
                if (!stabilizerEnabledFacilities.Add(facilityKey))
                {
                    stabilizerEnabledFacilities.Remove(facilityKey);
                }

                refresh?.Invoke();
            },
            86f);

        if (state.reforgeReady)
        {
            bool catalystReady = TryGetCatalyst(
                    selectedCatalyst,
                    out EquipmentCatalystDefinition catalyst)
                && catalyst.progressionLevel
                    >= preview.RequiredCatalystProgressionLevel;
            GameObject reforgeRow = CreateRow(
                parent,
                "EquipmentReforgeCommand",
                50f);
            created.Add(reforgeRow);
            AddLabel(
                reforgeRow.transform,
                $"최소 촉매 진행 {preview.RequiredCatalystProgressionLevel} · "
                + $"가능 부담 {string.Join(", ", preview.PossibleBurdenIds.Select(FormatEffectId))}",
                font,
                384f,
                catalystReady
                    ? DungeonUiTheme.TextPrimary
                    : DungeonUiTheme.Warning);
            ReforgePrecisionSelection precision =
                GetPrecisionSelection(instance.instanceId);
            RenderPrecisionOptions(
                parent,
                instance,
                preview,
                precision,
                font,
                showFeedback,
                refresh,
                created);
            AddButton(
                reforgeRow.transform,
                "재단조",
                font,
                true,
                catalystReady,
                () =>
                {
                    bool queued = precisionReforge.TryQueuePrecisionReforge(
                        instance.instanceId,
                        building,
                        selectedCatalyst,
                        stabilizerEnabled
                            ? StabilizerItemId
                            : string.Empty,
                        precision,
                        out _,
                        out string message);
                    string feedback = queued
                        ? "장비와 재료의 운반 및 재단조를 예약했습니다."
                        : message;
                    showFeedback?.Invoke(feedback);
                    refresh?.Invoke();
                },
                86f);
        }

        EvolutionNode[] historyNodes = state.evolutionNodes
            .Where(node => node != null
                && node.historical
                && node.uiVisible)
            .OrderBy(node => node.generation)
            .ThenBy(node => node.nodeId, StringComparer.Ordinal)
            .ToArray();
        if (historyNodes.Length == 0)
        {
            return;
        }

        AddText(
            parent,
            "귀속 역사",
            font,
            16f,
            DungeonUiTheme.Accent,
            28f,
            created);
        foreach (EvolutionNode node in historyNodes)
        {
            bool active = state.activeHistoricalNodeIds.Contains(
                node.nodeId,
                StringComparer.Ordinal);
            bool catalystReady = TryGetCatalyst(
                    selectedCatalyst,
                    out EquipmentCatalystDefinition catalyst)
                && catalyst.progressionLevel >=
                    EquipmentEvolutionProgression
                        .GetMinimumCatalystProgressionLevel(
                        state.generation);
            GameObject row = CreateRow(
                parent,
                $"EquipmentHistoryNode_{Sanitize(node.nodeId)}",
                62f);
            created.Add(row);
            AddLabel(
                row.transform,
                $"{presentation.ResolveNodeName(node)} · {(active ? "공명 중" : "휴면")}\n"
                + (string.IsNullOrWhiteSpace(node.description)
                    ? FormatEffectId(node.effectId)
                    : node.description),
                font,
                384f,
                active
                    ? DungeonUiTheme.Accent
                    : DungeonUiTheme.TextSecondary);
            EvolutionNode captured = node;
            AddButton(
                row.transform,
                active ? "끄기" : "켜기",
                font,
                active,
                catalystReady,
                () =>
                {
                    bool queued = attunement.TryQueueReattunement(
                        instance.instanceId,
                        building,
                        captured.nodeId,
                        !active,
                        selectedCatalyst,
                        out _,
                        out string message);
                    string feedback = queued
                        ? $"귀속 역사 공명 {(active ? "해제" : "활성화")} 작업을 예약했습니다."
                        : message;
                    showFeedback?.Invoke(feedback);
                    refresh?.Invoke();
                },
                72f);
        }
    }

    private void RenderEquipmentOverclock(
        Transform parent,
        CombatEquipmentInstance instance,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh,
        ICollection<GameObject> created)
    {
        OverclockState active = overclock.States.FirstOrDefault(state =>
            state != null
            && state.targetKind == OverclockTargetKind.Equipment
            && string.Equals(
                state.targetId,
                instance.instanceId,
                StringComparison.Ordinal)
            && state.Active);
        AddText(
            parent,
            active != null
                ? $"장비 오버클럭 {FormatOverclockTier(active.tier)} · "
                  + $"{active.remainingGameSeconds / 7.5f:0.#}시간 남음 · "
                  + $"과부하 {active.overload:0}"
                : $"장비 오버클럭 · 현재 과부하 "
                  + $"{overclock.GetOverload(OverclockTargetKind.Equipment, instance.instanceId):0}",
            font,
            14f,
            active != null
                ? DungeonUiTheme.Warning
                : DungeonUiTheme.TextSecondary,
            28f,
            created);
        if (active != null)
        {
            return;
        }

        AddOverclockButtons(
            parent,
            font,
            tier => overclock.TryActivateEquipment(
                instance.instanceId,
                tier,
                out string reason)
                ? $"장비 {FormatOverclockTier(tier)} 오버클럭을 시작했습니다."
                : reason,
            showFeedback,
            refresh,
            created,
            "Equipment");
    }

    internal static void AddOverclockButtons(
        Transform parent,
        TMP_FontAsset font,
        Func<OverclockTier, string> activate,
        Action<string> showFeedback,
        Action refresh,
        ICollection<GameObject> created,
        string prefix)
    {
        GameObject row = CreateRow(
            parent,
            $"{prefix}OverclockOptions",
            44f);
        created.Add(row);
        AddButton(
            row.transform,
            "통제 +10%",
            font,
            false,
            true,
            () =>
            {
                showFeedback?.Invoke(activate(OverclockTier.Controlled));
                refresh?.Invoke();
            },
            112f);
        AddButton(
            row.transform,
            "공격적 +20%",
            font,
            false,
            true,
            () =>
            {
                showFeedback?.Invoke(activate(OverclockTier.Aggressive));
                refresh?.Invoke();
            },
            126f);
        AddButton(
            row.transform,
            "임계 +35%",
            font,
            false,
            true,
            () =>
            {
                showFeedback?.Invoke(activate(OverclockTier.Critical));
                refresh?.Invoke();
            },
            112f);
    }

    private void RenderPrecisionOptions(
        Transform parent,
        CombatEquipmentInstance instance,
        EquipmentReforgePreview preview,
        ReforgePrecisionSelection selection,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh,
        ICollection<GameObject> created)
    {
        AddText(
            parent,
            $"유료 정밀 서비스 {selection.SelectedCount}/2 · 재료와 촉매는 그대로 필요",
            font,
            14f,
            DungeonUiTheme.TextSecondary,
            26f,
            created);
        GameObject row = CreateRow(
            parent,
            "ReforgePrecisionOptions",
            44f);
        created.Add(row);
        AddButton(
            row.transform,
            "정밀 교정",
            font,
            selection.preciseCalibration,
            true,
            () => TogglePrecision(
                selection,
                ReforgePrecisionOption.PreciseCalibration,
                preview,
                showFeedback,
                refresh),
            112f);
        AddButton(
            row.transform,
            "부담 억제",
            font,
            selection.burdenSuppression,
            preview.PossibleBurdenIds.Count > 0,
            () => TogglePrecision(
                selection,
                ReforgePrecisionOption.BurdenSuppression,
                preview,
                showFeedback,
                refresh),
            112f);
        AddButton(
            row.transform,
            "외부 지원",
            font,
            selection.externalTechnicalSupport,
            true,
            () => TogglePrecision(
                selection,
                ReforgePrecisionOption.ExternalTechnicalSupport,
                preview,
                showFeedback,
                refresh),
            112f);
    }

    private static void TogglePrecision(
        ReforgePrecisionSelection selection,
        ReforgePrecisionOption option,
        EquipmentReforgePreview preview,
        Action<string> showFeedback,
        Action refresh)
    {
        bool currentlyEnabled = option switch
        {
            ReforgePrecisionOption.PreciseCalibration =>
                selection.preciseCalibration,
            ReforgePrecisionOption.BurdenSuppression =>
                selection.burdenSuppression,
            ReforgePrecisionOption.ExternalTechnicalSupport =>
                selection.externalTechnicalSupport,
            _ => false
        };
        if (!currentlyEnabled && selection.SelectedCount >= 2)
        {
            showFeedback?.Invoke("유료 정밀 서비스는 최대 두 개까지 선택할 수 있습니다.");
            return;
        }

        switch (option)
        {
            case ReforgePrecisionOption.PreciseCalibration:
                selection.preciseCalibration = !currentlyEnabled;
                break;
            case ReforgePrecisionOption.BurdenSuppression:
                selection.burdenSuppression = !currentlyEnabled;
                selection.suppressedBurdenEffectId =
                    selection.burdenSuppression
                        ? preview.PossibleBurdenIds.FirstOrDefault()
                          ?? string.Empty
                        : string.Empty;
                break;
            case ReforgePrecisionOption.ExternalTechnicalSupport:
                selection.externalTechnicalSupport = !currentlyEnabled;
                break;
        }

        refresh?.Invoke();
    }

    private ReforgePrecisionSelection GetPrecisionSelection(
        string equipmentInstanceId)
    {
        if (!precisionByEquipment.TryGetValue(
                equipmentInstanceId,
                out ReforgePrecisionSelection selection))
        {
            selection = new ReforgePrecisionSelection();
            precisionByEquipment[equipmentInstanceId] = selection;
        }

        return selection;
    }

    private string ResolveSelectedEquipment(
        string facilityKey,
        IReadOnlyList<CombatEquipmentInstance> available)
    {
        if (selectedEquipmentByFacility.TryGetValue(
                facilityKey,
                out string selected)
            && available.Any(instance => string.Equals(
                instance.instanceId,
                selected,
                StringComparison.Ordinal)))
        {
            return selected;
        }

        selected = available[0].instanceId;
        selectedEquipmentByFacility[facilityKey] = selected;
        return selected;
    }

}
