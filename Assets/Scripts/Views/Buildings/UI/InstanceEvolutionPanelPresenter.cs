using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
    private const string StabilizerItemId = "resource:mana-crystal";

    private readonly IFacilityEvolutionRuntime facilityEvolution;
    private readonly IEquipmentEvolutionRuntime equipmentEvolution;
    private readonly IAttunementRuntime attunement;
    private readonly IEvolutionModuleRegistry modules;
    private readonly ICombatEquipmentRuntime equipment;
    private readonly IWorldItemStackRuntime worldItems;
    private readonly IFacilityRelocationTargetingService relocationTargeting;
    private readonly IReforgePrecisionService precisionReforge;
    private readonly IEquipmentOverclockRuntime overclock;

    private readonly Dictionary<string, string> selectedCatalystByFacility =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly Dictionary<string, string> selectedEquipmentByFacility =
        new Dictionary<string, string>(StringComparer.Ordinal);
    private readonly HashSet<string> stabilizerEnabledFacilities =
        new HashSet<string>(StringComparer.Ordinal);
    private readonly Dictionary<string, ReforgePrecisionSelection>
        precisionByEquipment =
            new Dictionary<string, ReforgePrecisionSelection>(
                StringComparer.Ordinal);

    public InstanceEvolutionPanelPresenter(
        IFacilityEvolutionRuntime facilityEvolution,
        IEquipmentEvolutionRuntime equipmentEvolution,
        IAttunementRuntime attunement,
        IEvolutionModuleRegistry modules,
        ICombatEquipmentRuntime equipment,
        IWorldItemStackRuntime worldItems,
        IFacilityRelocationTargetingService relocationTargeting,
        IReforgePrecisionService precisionReforge,
        IEquipmentOverclockRuntime overclock)
    {
        this.facilityEvolution = facilityEvolution
            ?? throw new ArgumentNullException(nameof(facilityEvolution));
        this.equipmentEvolution = equipmentEvolution
            ?? throw new ArgumentNullException(nameof(equipmentEvolution));
        this.attunement = attunement
            ?? throw new ArgumentNullException(nameof(attunement));
        this.modules = modules
            ?? throw new ArgumentNullException(nameof(modules));
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
        this.worldItems = worldItems
            ?? throw new ArgumentNullException(nameof(worldItems));
        this.relocationTargeting = relocationTargeting
            ?? throw new ArgumentNullException(nameof(relocationTargeting));
        this.precisionReforge = precisionReforge
            ?? throw new ArgumentNullException(nameof(precisionReforge));
        this.overclock = overclock
            ?? throw new ArgumentNullException(nameof(overclock));
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
            RenderEquipmentEvolution(
                parent,
                building,
                facilityKey,
                catalysts,
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
                && node.playerVisible)
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
            string title = ResolveNodeName(node);
            string details =
                $"{(active ? "활성" : "휴면")} · {FormatModulePair(node)}"
                + $"\n{FormatActivationRule(node.activationRule)}";
            AddLabel(
                row.transform,
                $"{title}\n{details}",
                font,
                active ? 384f : 300f,
                active ? DungeonUiTheme.TextPrimary : DungeonUiTheme.Warning);
            if (!active)
            {
                int requiredPotency =
                    EquipmentEvolutionProgression.GetMinimumCatalystPotency(
                        Mathf.Max(0, node.generation - 1));
                bool catalystReady = TryGetCatalyst(
                    selectedCatalyst,
                    out EquipmentCatalystDefinition catalyst)
                    && catalyst.potency >= requiredPotency;
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
            bool needsCatalyst = candidate.minimumCatalystPotency > 0;
            bool catalystReady = !needsCatalyst
                || (TryGetCatalyst(
                        selectedCatalyst,
                        out EquipmentCatalystDefinition catalyst)
                    && catalyst.potency >= candidate.minimumCatalystPotency);
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
                ? $" · 촉매 효능 {candidate.minimumCatalystPotency}+"
                : string.Empty;
            AddLabel(
                row.transform,
                $"{FormatCandidateKind(candidate.kind)}{requirement}\n"
                + $"{FormatModule(benefitId, true)} / "
                + $"{FormatModule(candidate.burdenModuleId, false)}",
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

    private void RenderEquipmentEvolution(
        Transform parent,
        BuildableObject building,
        string facilityKey,
        IReadOnlyList<CatalystInventoryEntry> catalysts,
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
            .ThenBy(instance => GetEquipmentName(instance), StringComparer.Ordinal)
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
                $"{GetEquipmentName(instance)} · 세대 {state.generation} · "
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
            catalysts,
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
        IReadOnlyList<CatalystInventoryEntry> catalysts,
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
            $"{GetEquipmentName(instance)} · 공명 "
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
                && catalyst.potency >= preview.RequiredCatalystPotency;
            GameObject reforgeRow = CreateRow(
                parent,
                "EquipmentReforgeCommand",
                50f);
            created.Add(reforgeRow);
            AddLabel(
                reforgeRow.transform,
                $"최소 촉매 효능 {preview.RequiredCatalystPotency} · "
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
                && node.playerVisible)
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
                && catalyst.potency >=
                    EquipmentEvolutionProgression.GetMinimumCatalystPotency(
                        state.generation);
            GameObject row = CreateRow(
                parent,
                $"EquipmentHistoryNode_{Sanitize(node.nodeId)}",
                62f);
            created.Add(row);
            AddLabel(
                row.transform,
                $"{ResolveNodeName(node)} · {(active ? "공명 중" : "휴면")}\n"
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

        AddOverclockButtons(
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

    private static void AddOverclockButtons(
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

    private static string FormatOverclockTier(OverclockTier tier)
    {
        return tier switch
        {
            OverclockTier.Controlled => "통제",
            OverclockTier.Aggressive => "공격적",
            OverclockTier.Critical => "임계",
            _ => "없음"
        };
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
            .OrderByDescending(entry => entry.Definition.potency)
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

    private string GetEquipmentName(CombatEquipmentInstance instance)
    {
        string definitionName = equipment.TryGetDefinition(
                instance.definitionId,
                out CombatEquipmentDefinitionSO definition)
            ? definition.DisplayName
            : instance.definitionId;
        return $"{definitionName} ({CombatQualityRules.GetDisplayName(instance.quality)})";
    }

    private string ResolveNodeName(EvolutionNode node)
    {
        if (!string.IsNullOrWhiteSpace(node.displayName))
        {
            return node.displayName;
        }

        return modules.TryGet(node.effectId, out EvolutionModuleDefinition module)
            ? module.DisplayName
            : FormatEffectId(node.effectId);
    }

    private string FormatModulePair(EvolutionNode node)
    {
        return $"{FormatModule(node.effectId, true)} / "
            + $"{FormatModule(node.burdenEffectId, false)}";
    }

    private string FormatModule(string moduleId, bool benefit)
    {
        if (!modules.TryGet(
                moduleId,
                out EvolutionModuleDefinition definition))
        {
            return FormatEffectId(moduleId);
        }

        IReadOnlyList<EvolutionEffectModifier> modifiers = benefit
            ? definition.Benefits
            : definition.Burdens;
        string values = string.Join(
            ", ",
            modifiers.Select(modifier =>
                $"{FormatEffectId(modifier.statId)} "
                + FormatModifier(modifier)));
        return $"{definition.DisplayName}: {values}";
    }

    private static string FormatModifier(EvolutionEffectModifier modifier)
    {
        List<string> parts = new List<string>();
        if (!Mathf.Approximately(modifier.multiplier, 1f))
        {
            parts.Add($"×{modifier.multiplier:0.00}");
        }

        if (!Mathf.Approximately(modifier.additive, 0f))
        {
            parts.Add($"{modifier.additive:+0.##;-0.##}");
        }

        return parts.Count > 0 ? string.Join(" ", parts) : "변화";
    }

    private static string FormatActivationRule(
        EvolutionModuleActivationRule rule)
    {
        if (rule == null || rule.kind == EvolutionModuleActivationKind.Always)
        {
            return "방과 무관하게 적용";
        }

        List<string> parts = new List<string>();
        if (rule.requiredRoomTags.Count > 0)
        {
            parts.Add("필수 " + string.Join("+", rule.requiredRoomTags));
        }

        if (rule.forbiddenRoomTags.Count > 0)
        {
            parts.Add("금지 " + string.Join("+", rule.forbiddenRoomTags));
        }

        if (rule.minimumCleanliness > 0f)
        {
            parts.Add($"청결 {rule.minimumCleanliness:0}+");
        }

        if (rule.minimumBeauty > 0f)
        {
            parts.Add($"미관 {rule.minimumBeauty:0}+");
        }

        if (rule.minimumSpace > 0f)
        {
            parts.Add($"공간 {rule.minimumSpace:0}+");
        }

        if (rule.minimumTemperature > 0f)
        {
            parts.Add($"온도 {rule.minimumTemperature:0}+");
        }

        return parts.Count == 0
            ? "방 조건 필요"
            : string.Join(" · ", parts);
    }

    private static string FormatCandidateKind(
        FacilityGenerationCandidateKind kind)
    {
        return kind switch
        {
            FacilityGenerationCandidateKind.PrimaryRole => "주력 역할 강화",
            FacilityGenerationCandidateKind.RoomSynergy => "방 시너지 결합",
            FacilityGenerationCandidateKind.RiskyCatalyst => "고위험 촉매 개조",
            _ => "시설 개조"
        };
    }

    private static string FormatDirection(EquipmentEvolutionDirection direction)
    {
        return direction switch
        {
            EquipmentEvolutionDirection.Melee => "근접 특화",
            EquipmentEvolutionDirection.Ranged => "원거리 특화",
            EquipmentEvolutionDirection.Accuracy => "명중 특화",
            EquipmentEvolutionDirection.Execution => "처형 특화",
            EquipmentEvolutionDirection.Interception => "저지 특화",
            EquipmentEvolutionDirection.Protection => "보호 특화",
            EquipmentEvolutionDirection.Survival => "생존 특화",
            _ => "균형"
        };
    }

    private static string FormatCatalystFamily(string family)
    {
        string normalized = family?.ToLowerInvariant() ?? string.Empty;
        if (normalized.Contains("offense")) return "공세";
        if (normalized.Contains("defense")) return "방어";
        if (normalized.Contains("industry")) return "산업";
        if (normalized.Contains("survival")) return "생존";
        if (normalized.Contains("arcane")) return "비전";
        if (normalized.Contains("authority")) return "권위";
        return string.IsNullOrWhiteSpace(family) ? "범용" : family;
    }

    private static string ResolveFacilityModuleForCatalyst(string family)
    {
        string normalized = family?.ToLowerInvariant() ?? string.Empty;
        if (normalized.Contains("offense")
            || normalized.Contains("defense"))
        {
            return "facility:defense";
        }

        if (normalized.Contains("survival")) return "facility:survival";
        if (normalized.Contains("arcane")) return "facility:research";
        if (normalized.Contains("authority")) return "facility:service";
        return "facility:output";
    }

    private static string FormatEffectId(string effectId)
    {
        return effectId switch
        {
            "work.output" => "작업 산출",
            "service.speed" => "서비스 속도",
            "research.output" => "연구 산출",
            "survival.output" => "생존 지원",
            "defense.output" => "방어 성능",
            "entertainment.output" => "흥행 성능",
            "room.synergy" => "방 시너지",
            "fuel.use" => "연료 소비",
            "staff.required" => "필요 인력",
            "heat.output" => "발열",
            "maintenance.work" => "유지 작업",
            "space.use" => "공간 부담",
            "accident.risk" => "사고 위험",
            "combat.damage" => "피해",
            "combat.accuracy" => "명중",
            "combat.reload" => "재장전 부담",
            "combat.defense" => "방어",
            "combat.move" => "이동",
            "combat.durability" => "내구",
            "combat.value" => "가치",
            "combat.weight" => "무게",
            "combat.accident" => "전투 사고 위험",
            _ => string.IsNullOrWhiteSpace(effectId) ? "없음" : effectId
        };
    }

    private static string FormatRelocationPhase(FacilityRelocationPhase phase)
    {
        return phase switch
        {
            FacilityRelocationPhase.Dismantling => "해체 중",
            FacilityRelocationPhase.WaitingForPackage => "포장 운반 중",
            FacilityRelocationPhase.Reinstalling => "재설치 중",
            FacilityRelocationPhase.Blocked => "막힘",
            _ => phase.ToString()
        };
    }

    private static string FormatOrderState(EvolutionReforgeOrderState state)
    {
        return state switch
        {
            EvolutionReforgeOrderState.WaitingForMaterials => "재료 운반 중",
            EvolutionReforgeOrderState.Ready => "작업 대기",
            EvolutionReforgeOrderState.InProgress => "작업 중",
            EvolutionReforgeOrderState.Completed => "완료",
            EvolutionReforgeOrderState.Cancelled => "취소",
            EvolutionReforgeOrderState.Blocked => "막힘",
            _ => state.ToString()
        };
    }

    private static bool TryGetCatalyst(
        string itemId,
        out EquipmentCatalystDefinition catalyst)
    {
        return EvolutionCatalystItemId.TryParseCatalyst(
            itemId,
            out catalyst);
    }

    private static void AddProgress(
        Transform parent,
        string label,
        float progress,
        EvolutionReforgeOrderState state,
        TMP_FontAsset font,
        ICollection<GameObject> created)
    {
        AddText(
            parent,
            $"{label} · {FormatOrderState(state)} · {Mathf.Clamp01(progress):P0}",
            font,
            14f,
            state == EvolutionReforgeOrderState.Blocked
                ? DungeonUiTheme.Danger
                : DungeonUiTheme.Warning,
            28f,
            created);
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
        float width,
        Color color)
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
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
    }

    private static void AddButton(
        Transform parent,
        string label,
        TMP_FontAsset font,
        bool selected,
        bool enabled,
        Action action,
        float width)
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
        button.interactable = enabled;
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
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = 14f;
        text.color = enabled
            ? DungeonUiTheme.TextPrimary
            : DungeonUiTheme.TextSecondary;
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
            "InstanceEvolutionText",
            typeof(RectTransform),
            typeof(TextMeshProUGUI),
            typeof(LayoutElement));
        textObject.transform.SetParent(parent, false);
        textObject.GetComponent<LayoutElement>().preferredHeight = height;
        TMP_Text text = textObject.GetComponent<TMP_Text>();
        text.text = value;
        text.font = font;
        text.fontSize = fontSize;
        text.enableAutoSizing = true;
        text.fontSizeMin = 10f;
        text.fontSizeMax = fontSize;
        text.color = color;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.textWrappingMode = TextWrappingModes.Normal;
        text.raycastTarget = false;
        created.Add(textObject);
    }

    private static string Sanitize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? "Unknown"
            : value.Replace(':', '_').Replace('/', '_').Replace(' ', '_');
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
