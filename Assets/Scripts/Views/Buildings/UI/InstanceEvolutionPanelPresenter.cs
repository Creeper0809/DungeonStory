using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static InstanceEvolutionPanelPresentation;
using static InstanceEvolutionPanelView;

public interface IInstanceEvolutionPanelPresenter
{
    IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh);
}

public sealed class InstanceEvolutionPanelPresenter :
    IInstanceEvolutionPanelPresenter
{
    private readonly IFacilityEvolutionRuntime facilityEvolution;
    private readonly IWorldItemStackRuntime worldItems;
    private readonly IFacilityRelocationTargetingService relocationTargeting;
    private readonly IEquipmentOverclockRuntime overclock;
    private readonly InstanceEvolutionPanelPresentation presentation;
    private readonly InstanceEquipmentEvolutionSection equipmentSection;

    private readonly Dictionary<string, string> selectedCatalystByFacility =
        new Dictionary<string, string>(StringComparer.Ordinal);
    public InstanceEvolutionPanelPresenter(
        IFacilityEvolutionRuntime facilityEvolution,
        IWorldItemStackRuntime worldItems,
        IFacilityRelocationTargetingService relocationTargeting,
        IEquipmentOverclockRuntime overclock,
        InstanceEvolutionPanelPresentation presentation,
        InstanceEquipmentEvolutionSection equipmentSection)
    {
        this.facilityEvolution = facilityEvolution
            ?? throw new ArgumentNullException(nameof(facilityEvolution));
        this.worldItems = worldItems
            ?? throw new ArgumentNullException(nameof(worldItems));
        this.relocationTargeting = relocationTargeting
            ?? throw new ArgumentNullException(nameof(relocationTargeting));
        this.overclock = overclock
            ?? throw new ArgumentNullException(nameof(overclock));
        this.presentation = presentation
            ?? throw new ArgumentNullException(nameof(presentation));
        this.equipmentSection = equipmentSection
            ?? throw new ArgumentNullException(nameof(equipmentSection));
    }

    public IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh)
    {
        List<GameObject> created = new List<GameObject>();
        if (parent == null
            || building == null
            || building.isDestroy
            || building is not IWorkableFacility
            || building is ConstructionSite)
        {
            return created;
        }

        facilityEvolution.RefreshRoomActivation(building);
        FacilityEvolutionState facilityState =
            facilityEvolution.GetState(building);
        string facilityKey = facilityState.facilityPersistentId;
        IReadOnlyList<CatalystInventoryEntry> catalysts =
            GetAvailableCatalysts();
        string selectedCatalyst = ResolveSelectedCatalyst(
            facilityKey,
            catalysts);

        AddText(
            parent,
            "시설 진화",
            font,
            21f,
            DungeonUiTheme.TextPrimary,
            34f,
            created);
        AddText(
            parent,
            $"세대 {facilityState.generation} · 숙련 "
            + $"{facilityState.mastery:0.#}/{facilityState.RequiredMastery:0.#}",
            font,
            15f,
            facilityState.ReadyForGeneration
                ? DungeonUiTheme.Good
                : DungeonUiTheme.TextSecondary,
            28f,
            created);
        RenderFacilityOverclock(
            parent,
            building,
            facilityState.facilityPersistentId,
            font,
            showFeedback,
            refresh,
            created);

        RenderFacilityOrders(
            parent,
            facilityState,
            font,
            created);
        RenderCatalystSelector(
            parent,
            facilityKey,
            catalysts,
            selectedCatalyst,
            font,
            refresh,
            created);
        RenderFacilityNodes(
            parent,
            building,
            facilityState,
            selectedCatalyst,
            font,
            showFeedback,
            refresh,
            created);
        RenderFacilityCandidates(
            parent,
            building,
            facilityState,
            selectedCatalyst,
            font,
            showFeedback,
            refresh,
            created);

        if (facilityState.modificationOrder == null
            && facilityState.recalibrationOrder == null
            && facilityState.relocationOrder == null)
        {
            GameObject row = CreateRow(
                parent,
                "FacilityRelocationCommand",
                42f);
            created.Add(row);
            AddLabel(
                row.transform,
                "시설 이전은 해체 25% → 포장 운반 → 재설치 50%로 진행됩니다.",
                font,
                384f,
                DungeonUiTheme.TextSecondary);
            AddButton(
                row.transform,
                "이전",
                font,
                false,
                true,
                () => relocationTargeting.Begin(
                    building,
                    showFeedback,
                    refresh),
                78f);
        }

        if (building.BuildingData?
                .GetAbility<BuildingEquipmentCraftingAbility>() != null)
        {
            equipmentSection.Render(
                parent,
                building,
                facilityKey,
                selectedCatalyst,
                font,
                showFeedback,
                refresh,
                created);
        }

        return created;
    }

    private void RenderFacilityOrders(
        Transform parent,
        FacilityEvolutionState state,
        TMP_FontAsset font,
        ICollection<GameObject> created)
    {
        if (state.modificationOrder != null)
        {
            AddProgress(
                parent,
                "시설 개조",
                state.modificationOrder.ProgressRatio,
                state.modificationOrder.state,
                font,
                created);
        }

        if (state.recalibrationOrder != null)
        {
            AddProgress(
                parent,
                "방 조건 재조율",
                state.recalibrationOrder.requiredWork <= 0f
                    ? 0f
                    : state.recalibrationOrder.completedWork
                        / state.recalibrationOrder.requiredWork,
                state.recalibrationOrder.state,
                font,
                created);
        }

        if (state.relocationOrder != null)
        {
            FacilityRelocationOrder order = state.relocationOrder;
            float progress = order.ProgressRatio;
            AddText(
                parent,
                $"시설 이전 · {FormatRelocationPhase(order.phase)} · {progress:P0}",
                font,
                14f,
                order.phase == FacilityRelocationPhase.Blocked
                    ? DungeonUiTheme.Danger
                    : DungeonUiTheme.Warning,
                28f,
                created);
        }
    }

    private void RenderFacilityNodes(
        Transform parent,
        BuildableObject building,
        FacilityEvolutionState state,
        string selectedCatalyst,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh,
        ICollection<GameObject> created)
    {
        EvolutionNode[] nodes = state.evolutionNodes
            .Where(node => node != null
                && !node.historical
                && node.uiVisible)
            .OrderBy(node => node.generation)
            .ThenBy(node => node.nodeId, StringComparer.Ordinal)
            .ToArray();
        if (nodes.Length == 0)
        {
            return;
        }

        AddText(
            parent,
            $"진화 노드 {nodes.Length}개 · 활성 {state.activeNodeIds.Count} · 휴면 {state.dormantNodeIds.Count}",
            font,
            15f,
            DungeonUiTheme.TextPrimary,
            28f,
            created);
        foreach (EvolutionNode node in nodes)
        {
            bool active = state.activeNodeIds.Contains(
                node.nodeId,
                StringComparer.Ordinal);
            GameObject row = CreateRow(
                parent,
                $"FacilityEvolutionNode_{Sanitize(node.nodeId)}",
                62f);
            created.Add(row);
            string title = presentation.ResolveNodeName(node);
            string details =
                $"{(active ? "활성" : "휴면")} · {presentation.FormatModulePair(node)}"
                + $"\n{FormatActivationRule(node.activationRule)}";
            AddLabel(
                row.transform,
                $"{title}\n{details}",
                font,
                active ? 384f : 300f,
                active ? DungeonUiTheme.TextPrimary : DungeonUiTheme.Warning);
            if (!active)
            {
                int requiredProgressionLevel =
                    EquipmentEvolutionProgression
                        .GetMinimumCatalystProgressionLevel(
                        Mathf.Max(0, node.generation - 1));
                bool catalystReady = TryGetCatalyst(
                    selectedCatalyst,
                    out EquipmentCatalystDefinition catalyst)
                    && catalyst.progressionLevel >= requiredProgressionLevel;
                EvolutionNode captured = node;
                AddButton(
                    row.transform,
                    "현재 방에 재조율",
                    font,
                    false,
                    catalystReady,
                    () =>
                    {
                        bool queued =
                            facilityEvolution.TryQueueRecalibrationToCurrentRoom(
                                building,
                                captured.nodeId,
                                selectedCatalyst,
                                out _,
                                out string message);
                        string feedback = queued
                            ? "현재 방 조건에 맞춘 재조율을 예약했습니다."
                            : message;
                        showFeedback?.Invoke(feedback);
                        refresh?.Invoke();
                    },
                    132f);
            }
        }
    }

    private void RenderFacilityCandidates(
        Transform parent,
        BuildableObject building,
        FacilityEvolutionState state,
        string selectedCatalyst,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh,
        ICollection<GameObject> created)
    {
        if (!state.ReadyForGeneration
            || state.modificationOrder != null
            || state.recalibrationOrder != null
            || state.relocationOrder != null)
        {
            return;
        }

        FacilityGenerationCandidate[] candidates =
            facilityEvolution.GetGenerationCandidates(building).ToArray();
        if (candidates.Length == 0)
        {
            return;
        }

        AddText(
            parent,
            "다음 세대 후보 3개",
            font,
            17f,
            DungeonUiTheme.Accent,
            30f,
            created);
        foreach (FacilityGenerationCandidate candidate in candidates)
        {
            bool needsCatalyst =
                candidate.minimumCatalystProgressionLevel > 0;
            bool catalystReady = !needsCatalyst
                || (TryGetCatalyst(
                        selectedCatalyst,
                        out EquipmentCatalystDefinition catalyst)
                    && catalyst.progressionLevel
                        >= candidate.minimumCatalystProgressionLevel);
            string benefitId = needsCatalyst
                && TryGetCatalyst(
                    selectedCatalyst,
                    out EquipmentCatalystDefinition selected)
                    ? ResolveFacilityModuleForCatalyst(selected.family)
                    : candidate.benefitModuleId;
            GameObject row = CreateRow(
                parent,
                $"FacilityCandidate_{Sanitize(candidate.candidateId)}",
                68f);
            created.Add(row);
            string requirement = needsCatalyst
                ? $" · 촉매 진행 {candidate.minimumCatalystProgressionLevel}+"
                : string.Empty;
            AddLabel(
                row.transform,
                $"{FormatCandidateKind(candidate.kind)}{requirement}\n"
                + $"{presentation.FormatModule(benefitId, true)} / "
                + $"{presentation.FormatModule(candidate.burdenModuleId, false)}",
                font,
                384f,
                catalystReady
                    ? DungeonUiTheme.TextPrimary
                    : DungeonUiTheme.Warning);
            FacilityGenerationCandidate captured = candidate;
            AddButton(
                row.transform,
                "개조",
                font,
                true,
                catalystReady,
                () =>
                {
                    bool queued = facilityEvolution.TryQueueCandidate(
                        building,
                        captured.candidateId,
                        needsCatalyst ? selectedCatalyst : string.Empty,
                        out _,
                        out string message);
                    string feedback = queued
                        ? $"{FormatCandidateKind(captured.kind)} 개조를 예약했습니다."
                        : message;
                    showFeedback?.Invoke(feedback);
                    refresh?.Invoke();
                },
                76f);
        }
    }

    private void RenderFacilityOverclock(
        Transform parent,
        BuildableObject building,
        string facilityPersistentId,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh,
        ICollection<GameObject> created)
    {
        if (building?.BuildingData?
                .GetAbility<BuildingOverclockableAbility>() == null
            && building?.BuildingData?
                .GetAbility<BuildingTreasuryPoweredDefenseAbility>() == null)
        {
            return;
        }

        OverclockState active = overclock.States.FirstOrDefault(state =>
            state != null
            && state.targetKind == OverclockTargetKind.Facility
            && string.Equals(
                state.targetId,
                facilityPersistentId,
                StringComparison.Ordinal)
            && state.Active);
        AddText(
            parent,
            active != null
                ? $"시설 오버클럭 {FormatOverclockTier(active.tier)} · "
                  + $"{active.remainingGameSeconds / 7.5f:0.#}시간 남음 · "
                  + $"과부하 {active.overload:0}"
                : "시설 오버클럭 · 24시간 · 연장/환불 불가",
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

        InstanceEquipmentEvolutionSection.AddOverclockButtons(
            parent,
            font,
            tier => overclock.TryActivateFacility(
                building,
                tier,
                out string reason)
                ? $"시설 {FormatOverclockTier(tier)} 오버클럭을 시작했습니다."
                : reason,
            showFeedback,
            refresh,
            created,
            "Facility");
    }

    private void RenderCatalystSelector(
        Transform parent,
        string facilityKey,
        IReadOnlyList<CatalystInventoryEntry> catalysts,
        string selectedCatalyst,
        TMP_FontAsset font,
        Action refresh,
        ICollection<GameObject> created)
    {
        AddText(
            parent,
            catalysts.Count == 0
                ? "사용 가능한 촉매가 없습니다."
                : "촉매 선택",
            font,
            15f,
            catalysts.Count == 0
                ? DungeonUiTheme.Warning
                : DungeonUiTheme.TextPrimary,
            28f,
            created);
        foreach (CatalystInventoryEntry entry in catalysts.Take(8))
        {
            bool selected = string.Equals(
                entry.ItemId,
                selectedCatalyst,
                StringComparison.Ordinal);
            GameObject row = CreateRow(
                parent,
                $"Catalyst_{Sanitize(entry.ItemId)}",
                40f);
            created.Add(row);
            AddLabel(
                row.transform,
                $"{FormatCatalystFamily(entry.Definition.family)} · "
                + $"진행 {entry.Definition.progressionLevel} · "
                + $"효능 {entry.Definition.potency} · {entry.Quantity}개",
                font,
                384f,
                selected
                    ? DungeonUiTheme.Accent
                    : DungeonUiTheme.TextPrimary);
            string capturedId = entry.ItemId;
            AddButton(
                row.transform,
                selected ? "선택됨" : "선택",
                font,
                selected,
                true,
                () =>
                {
                    selectedCatalystByFacility[facilityKey] = capturedId;
                    refresh?.Invoke();
                },
                76f);
        }
    }

    private IReadOnlyList<CatalystInventoryEntry> GetAvailableCatalysts()
    {
        return worldItems.GetAllStacks()
            .Where(stack => stack != null
                && stack.Quantity > 0
                && !stack.Forbidden
                && !stack.IsReserved
                && stack.State is WorldItemStackState.Loose
                    or WorldItemStackState.Stored
                && EvolutionCatalystItemId.TryParseCatalyst(
                    stack.ItemId,
                    out _))
            .GroupBy(stack => stack.ItemId, StringComparer.Ordinal)
            .Select(group =>
            {
                EvolutionCatalystItemId.TryParseCatalyst(
                    group.Key,
                    out EquipmentCatalystDefinition definition);
                return new CatalystInventoryEntry(
                    group.Key,
                    group.Sum(stack => stack.Quantity),
                    definition);
            })
            .OrderByDescending(entry => entry.Definition.progressionLevel)
            .ThenByDescending(entry => entry.Definition.potency)
            .ThenBy(entry => entry.Definition.family, StringComparer.Ordinal)
            .ToArray();
    }

    private string ResolveSelectedCatalyst(
        string facilityKey,
        IReadOnlyList<CatalystInventoryEntry> catalysts)
    {
        if (selectedCatalystByFacility.TryGetValue(
                facilityKey,
                out string selected)
            && catalysts.Any(entry => string.Equals(
                entry.ItemId,
                selected,
                StringComparison.Ordinal)))
        {
            return selected;
        }

        selected = catalysts.FirstOrDefault().ItemId ?? string.Empty;
        selectedCatalystByFacility[facilityKey] = selected;
        return selected;
    }

    private readonly struct CatalystInventoryEntry
    {
        public CatalystInventoryEntry(
            string itemId,
            int quantity,
            EquipmentCatalystDefinition definition)
        {
            ItemId = itemId ?? string.Empty;
            Quantity = Mathf.Max(0, quantity);
            Definition = definition ?? new EquipmentCatalystDefinition();
        }

        public string ItemId { get; }
        public int Quantity { get; }
        public EquipmentCatalystDefinition Definition { get; }
    }
}
