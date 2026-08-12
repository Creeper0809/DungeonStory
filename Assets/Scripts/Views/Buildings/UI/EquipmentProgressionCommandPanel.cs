using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;

/// <summary>
/// Renders equipment-module and lineage commands. This class owns only UI
/// selection state; all mutations are validated by ICombatEquipmentRuntime.
/// </summary>
public sealed class EquipmentProgressionCommandPanel
{
    private sealed class LineageSelection
    {
        public string SourceId = string.Empty;
        public string TargetId = string.Empty;
        public string SealStackId = string.Empty;
    }

    private readonly ICombatEquipmentRuntime equipment;
    private readonly IStockQuery stock;
    private readonly IDomainFailureLocalizer failureLocalizer;
    private readonly Dictionary<string, LineageSelection> lineageByFacility =
        new(StringComparer.Ordinal);

    public EquipmentProgressionCommandPanel(
        ICombatEquipmentRuntime equipment,
        IStockQuery stock,
        IDomainFailureLocalizer failureLocalizer)
    {
        this.equipment = equipment
            ?? throw new ArgumentNullException(nameof(equipment));
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.failureLocalizer = failureLocalizer
            ?? throw new ArgumentNullException(nameof(failureLocalizer));
    }

    public IReadOnlyList<GameObject> Render(
        Transform parent,
        BuildableObject building,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh)
    {
        List<GameObject> created = new();
        if (parent == null || building == null)
        {
            return created;
        }

        string workstationTag = building.GetProductionWorkstationTag();
        if (workstationTag == EquipmentProgressionWorkstationTags.Appraisal
            || workstationTag == EquipmentProgressionWorkstationTags.Restoration
            || workstationTag == EquipmentProgressionWorkstationTags.PrecisionFitting
            || workstationTag == EquipmentProgressionWorkstationTags.RuneTuning)
        {
            RenderModules(
                parent,
                building,
                workstationTag,
                font,
                showFeedback,
                refresh,
                created);
        }
        else if (workstationTag
                 == EquipmentProgressionWorkstationTags.LineageArchive)
        {
            RenderLineage(
                parent,
                building,
                font,
                showFeedback,
                refresh,
                created);
        }
        return created;
    }

    private void RenderModules(
        Transform parent,
        BuildableObject facility,
        string workstationTag,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh,
        ICollection<GameObject> created)
    {
        string destinationId = EquipmentProgressionFacilityContract
            .GetLocalBufferDestinationId(facility);
        WorldItemStackSnapshot[] localStacks = stock.GetAllStacks()
            .Where(stack => stack != null
                && stack.Quantity > 0
                && !stack.Forbidden
                && stack.AvailableQuantity > 0
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            .ToArray();
        EquipmentModuleInstance[] modules = equipment.ModuleInstances
            .Where(module => module != null
                && module.state != EquipmentModuleProcessState.Lost
                && IsModuleVisibleAt(module, workstationTag)
                && localStacks.Any(stack =>
                    stack.StackId == module.sourceStackId
                    && stack.ItemInstanceId == module.instanceId))
            .OrderBy(module => module.state)
            .ThenBy(module => module.definitionId, StringComparer.Ordinal)
            .ThenBy(module => module.instanceId, StringComparer.Ordinal)
            .ToArray();
        CombatEquipmentInstance[] instances = equipment.Instances
            .Where(instance => instance != null
                && instance.worldState != CombatEquipmentWorldState.Lost)
            .Where(instance => localStacks.Any(stack =>
                stack.StackId == instance.sourceStackId
                && stack.ItemInstanceId == instance.instanceId))
            .OrderBy(EquipmentName, StringComparer.Ordinal)
            .ThenBy(instance => instance.instanceId, StringComparer.Ordinal)
            .ToArray();
        if (modules.Length == 0 && !instances.Any(HasInstalledModule))
        {
            return;
        }

        EquipmentCraftingPanelPresenter.AddText(
            parent,
            "원정 개량 부품",
            font,
            21f,
            DungeonUiTheme.TextPrimary,
            34f,
            created);

        foreach (EquipmentModuleInstance module in modules)
        {
            RenderModuleRow(
                parent,
                module,
                facility,
                workstationTag,
                font,
                showFeedback,
                refresh,
                created);
            if (workstationTag
                    == EquipmentProgressionWorkstationTags.PrecisionFitting
                && IsReadyForInstallation(module))
            {
                RenderInstallationTargets(
                    parent,
                    module,
                    instances,
                    facility,
                    font,
                    showFeedback,
                    refresh,
                    created);
            }
        }

        foreach (CombatEquipmentInstance instance in instances.Where(instance =>
                     workstationTag
                        == EquipmentProgressionWorkstationTags.PrecisionFitting
                     && HasInstalledModule(instance)))
        {
            RenderRemovalRows(
                parent,
                instance,
                facility,
                font,
                showFeedback,
                refresh,
                created);
        }
    }

    private void RenderModuleRow(
        Transform parent,
        EquipmentModuleInstance module,
        BuildableObject facility,
        string workstationTag,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh,
        ICollection<GameObject> created)
    {
        equipment.TryGetModuleDefinition(
            module.definitionId,
            out EquipmentModuleDefinitionSO definition);
        GameObject row = EquipmentCraftingPanelPresenter.CreateRow(
            parent,
            "EquipmentModule_"
                + EquipmentCraftingPanelPresenter.Sanitize(module.instanceId),
            58f);
        created.Add(row);
        EquipmentCraftingPanelPresenter.AddLabel(
            row.transform,
            $"{definition?.DisplayName ?? module.definitionId} · {module.grade}등급\n"
                + DescribeModuleState(module),
            font,
            328f);

        if (workstationTag == EquipmentProgressionWorkstationTags.Appraisal
            && module.state == EquipmentModuleProcessState.Unidentified)
        {
            AddModuleCommand(
                row.transform,
                "감정",
                "EquipmentModuleAppraise_",
                module.instanceId,
                () => equipment.TryAppraiseModule(
                    module.instanceId,
                    facility,
                    out DomainFailure failure)
                    ? Success("부품 감정을 완료했습니다.", showFeedback)
                    : Fail(failure, showFeedback),
                font,
                refresh);
        }
        else if (workstationTag == EquipmentProgressionWorkstationTags.Restoration
                 && module.state
                    == EquipmentModuleProcessState.IdentifiedDamaged)
        {
            AddModuleCommand(
                row.transform,
                "복원",
                "EquipmentModuleRestore_",
                module.instanceId,
                () => equipment.TryRestoreModule(
                    module.instanceId,
                    facility,
                    out DomainFailure failure)
                    ? Success("부품 복원을 완료했습니다.", showFeedback)
                    : Fail(failure, showFeedback),
                font,
                refresh);
        }
        else if (workstationTag == EquipmentProgressionWorkstationTags.RuneTuning
                 && module.state == EquipmentModuleProcessState.Restored
                 && module.grade == 4
                 && !module.runeTuned)
        {
            AddModuleCommand(
                row.transform,
                "룬 조율",
                "EquipmentModuleTune_",
                module.instanceId,
                () => equipment.TryTuneModule(
                    module.instanceId,
                    facility,
                    out DomainFailure failure)
                    ? Success("4등급 부품의 룬 조율을 완료했습니다.", showFeedback)
                    : Fail(failure, showFeedback),
                font,
                refresh,
                96f);
        }
    }

    private void RenderInstallationTargets(
        Transform parent,
        EquipmentModuleInstance module,
        IEnumerable<CombatEquipmentInstance> instances,
        BuildableObject facility,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh,
        ICollection<GameObject> created)
    {
        if (!equipment.TryGetModuleDefinition(
                module.definitionId,
                out EquipmentModuleDefinitionSO moduleDefinition))
        {
            return;
        }

        foreach (CombatEquipmentInstance instance in instances)
        {
            if (!equipment.TryGetDefinition(
                    instance.definitionId,
                    out CombatEquipmentDefinitionSO equipmentDefinition)
                || equipmentDefinition.ModuleSlotCount <= 0
                || equipmentDefinition.LineageKind != moduleDefinition.LineageKind)
            {
                continue;
            }

            for (int slotIndex = 0;
                 slotIndex < equipmentDefinition.ModuleSlotCount;
                 slotIndex++)
            {
                int capturedSlot = slotIndex;
                GameObject row = EquipmentCraftingPanelPresenter.CreateRow(
                    parent,
                    "EquipmentModuleInstallTarget_"
                        + EquipmentCraftingPanelPresenter.Sanitize(module.instanceId)
                        + "_"
                        + EquipmentCraftingPanelPresenter.Sanitize(instance.instanceId)
                        + "_"
                        + capturedSlot,
                    42f);
                created.Add(row);
                EquipmentCraftingPanelPresenter.AddLabel(
                    row.transform,
                    $"장착 대상: {EquipmentName(instance)} · 빈 슬롯 {capturedSlot + 1}",
                    font,
                    328f);
                EquipmentCraftingPanelPresenter.AddButton(
                    row.transform,
                    "장착",
                    font,
                    false,
                    () =>
                    {
                        if (equipment.TryInstallModule(
                                instance.instanceId,
                                module.instanceId,
                                capturedSlot,
                                facility,
                                out DomainFailure failure))
                        {
                            Success("부품을 장착했습니다.", showFeedback);
                        }
                        else
                        {
                            Fail(failure, showFeedback);
                        }
                        refresh?.Invoke();
                    },
                    objectName: "EquipmentModuleInstall_"
                        + EquipmentCraftingPanelPresenter.Sanitize(module.instanceId)
                        + "_"
                        + EquipmentCraftingPanelPresenter.Sanitize(instance.instanceId)
                        + "_"
                        + capturedSlot);
            }
        }
    }

    private void RenderRemovalRows(
        Transform parent,
        CombatEquipmentInstance instance,
        BuildableObject facility,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh,
        ICollection<GameObject> created)
    {
        IEnumerable<EquipmentModuleSlotState> installed =
            (instance.moduleSlots ?? new List<EquipmentModuleSlotState>())
            .Where(slot => slot != null
                && !string.IsNullOrWhiteSpace(slot.moduleInstanceId));
        foreach (EquipmentModuleSlotState slot in installed)
        {
            int capturedSlot = slot.slotIndex;
            GameObject row = EquipmentCraftingPanelPresenter.CreateRow(
                parent,
                "EquipmentModuleInstalled_"
                    + EquipmentCraftingPanelPresenter.Sanitize(instance.instanceId)
                    + "_"
                    + capturedSlot,
                42f);
            created.Add(row);
            EquipmentCraftingPanelPresenter.AddLabel(
                row.transform,
                $"{EquipmentName(instance)} · 슬롯 {capturedSlot + 1} · "
                    + slot.moduleInstanceId,
                font,
                328f);
            EquipmentCraftingPanelPresenter.AddButton(
                row.transform,
                "제거",
                font,
                false,
                () =>
                {
                    if (equipment.TryRemoveModule(
                            instance.instanceId,
                            capturedSlot,
                            facility,
                            out EquipmentModuleInstance removed,
                            out DomainFailure failure))
                    {
                        Success(
                            $"부품을 제거했습니다. 상태 {removed.condition:P0}로 반환합니다.",
                            showFeedback);
                    }
                    else
                    {
                        Fail(failure, showFeedback);
                    }
                    refresh?.Invoke();
                },
                objectName: "EquipmentModuleRemove_"
                    + EquipmentCraftingPanelPresenter.Sanitize(instance.instanceId)
                    + "_"
                    + capturedSlot);
        }
    }

    private void RenderLineage(
        Transform parent,
        BuildableObject facility,
        TMP_FontAsset font,
        Action<string> showFeedback,
        Action refresh,
        ICollection<GameObject> created)
    {
        string facilityId = facility.RequirePersistentInstanceId().Value;
        WorldItemStackSnapshot[] physicalStacks = stock.GetAllStacks()
            .Where(stack => stack != null
                && stack.Quantity > 0
                && !stack.Forbidden
                && stack.AvailableQuantity > 0
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(
                    stack.DestinationId,
                    facilityId,
                    StringComparison.Ordinal))
            .ToArray();
        CombatEquipmentInstance[] candidates = equipment.Instances
            .Where(instance => instance != null
                && instance.worldState != CombatEquipmentWorldState.Lost
                && physicalStacks.Any(stack =>
                    string.Equals(
                        stack.ItemInstanceId,
                        instance.instanceId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        stack.StackId,
                        instance.sourceStackId,
                        StringComparison.Ordinal)))
            .OrderBy(EquipmentName, StringComparer.Ordinal)
            .ThenBy(instance => instance.instanceId, StringComparer.Ordinal)
            .ToArray();
        WorldItemStackSnapshot[] seals = physicalStacks
            .Where(stack => !stack.Forbidden
                && stack.AvailableQuantity > 0
                && string.Equals(
                    stack.ItemId,
                    EquipmentProgressionItemIds.LineageSeal,
                    StringComparison.Ordinal))
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .ToArray();
        if (candidates.Length == 0
            && seals.Length == 0
            && equipment.HistoryTransferOrders.All(order => order.completed))
        {
            return;
        }

        EquipmentCraftingPanelPresenter.AddText(
            parent,
            "장비 계보 이전",
            font,
            21f,
            DungeonUiTheme.TextPrimary,
            34f,
            created);
        LineageSelection selection = GetSelection(facilityId);
        NormalizeSelection(selection, candidates, seals);
        foreach (CombatEquipmentInstance candidate in candidates)
        {
            RenderLineageCandidate(parent, candidate, selection, true, font, refresh, created);
            RenderLineageCandidate(parent, candidate, selection, false, font, refresh, created);
        }

        foreach (WorldItemStackSnapshot seal in seals)
        {
            bool selected = string.Equals(
                selection.SealStackId,
                seal.StackId,
                StringComparison.Ordinal);
            GameObject row = EquipmentCraftingPanelPresenter.CreateRow(
                parent,
                "EquipmentLineageSealRow_"
                    + EquipmentCraftingPanelPresenter.Sanitize(seal.StackId),
                42f);
            created.Add(row);
            EquipmentCraftingPanelPresenter.AddLabel(
                row.transform,
                $"계보 인장 · {seal.State} · {seal.StackId}",
                font,
                328f);
            EquipmentCraftingPanelPresenter.AddButton(
                row.transform,
                selected ? "선택됨" : "인장 선택",
                font,
                selected,
                () =>
                {
                    selection.SealStackId = seal.StackId;
                    refresh?.Invoke();
                },
                width: 96f,
                objectName: "EquipmentLineageSeal_"
                    + EquipmentCraftingPanelPresenter.Sanitize(seal.StackId));
        }

        RenderLineageComparison(parent, selection, candidates, font, created);
        EquipmentCraftingPanelPresenter.AddButton(
            parent,
            "계보 이전 주문",
            font,
            true,
            () =>
            {
                if (equipment.TryQueueHistoryTransfer(
                        selection.SourceId,
                        selection.TargetId,
                        selection.SealStackId,
                        facility,
                        out _,
                        out DomainFailure failure))
                {
                    Success("계보 이전 작업을 주문했습니다.", showFeedback);
                    selection.SourceId = string.Empty;
                    selection.TargetId = string.Empty;
                    selection.SealStackId = string.Empty;
                }
                else
                {
                    Fail(failure, showFeedback);
                }
                refresh?.Invoke();
            },
            width: 148f,
            interactable: !string.IsNullOrWhiteSpace(selection.SourceId)
                && !string.IsNullOrWhiteSpace(selection.TargetId)
                && !string.IsNullOrWhiteSpace(selection.SealStackId),
            objectName: "EquipmentLineageConfirm");

        foreach (EquipmentHistoryTransferOrder order in
                 equipment.HistoryTransferOrders.Where(order =>
                     order != null && !order.completed))
        {
            float progress = order.requiredWork > 0f
                ? Mathf.Clamp01(order.completedWork / order.requiredWork)
                : 0f;
            EquipmentCraftingPanelPresenter.AddText(
                parent,
                $"진행 중: {order.sourceEquipmentInstanceId} + 인장 → "
                    + $"{order.targetEquipmentInstanceId} · {progress:P0}",
                font,
                14f,
                DungeonUiTheme.Warning,
                30f,
                created);
        }
    }

    private void RenderLineageCandidate(
        Transform parent,
        CombatEquipmentInstance candidate,
        LineageSelection selection,
        bool source,
        TMP_FontAsset font,
        Action refresh,
        ICollection<GameObject> created)
    {
        string selectedId = source ? selection.SourceId : selection.TargetId;
        string prefix = source
            ? "EquipmentLineageSource_"
            : "EquipmentLineageTarget_";
        bool selected = string.Equals(
            selectedId,
            candidate.instanceId,
            StringComparison.Ordinal);
        GameObject row = EquipmentCraftingPanelPresenter.CreateRow(
            parent,
            prefix + "Row_"
                + EquipmentCraftingPanelPresenter.Sanitize(candidate.instanceId),
            46f);
        created.Add(row);
        EquipmentCraftingPanelPresenter.AddLabel(
            row.transform,
            $"{(source ? "소비 원본" : "승계 대상")}: "
                + DescribeEquipment(candidate),
            font,
            328f);
        EquipmentCraftingPanelPresenter.AddButton(
            row.transform,
            selected ? "선택됨" : "선택",
            font,
            selected,
            () =>
            {
                if (source)
                {
                    selection.SourceId = candidate.instanceId;
                }
                else
                {
                    selection.TargetId = candidate.instanceId;
                }
                refresh?.Invoke();
            },
            objectName: prefix
                + EquipmentCraftingPanelPresenter.Sanitize(candidate.instanceId));
    }

    private void RenderLineageComparison(
        Transform parent,
        LineageSelection selection,
        IReadOnlyList<CombatEquipmentInstance> candidates,
        TMP_FontAsset font,
        ICollection<GameObject> created)
    {
        CombatEquipmentInstance source = candidates.FirstOrDefault(instance =>
            string.Equals(
                instance.instanceId,
                selection.SourceId,
                StringComparison.Ordinal));
        CombatEquipmentInstance target = candidates.FirstOrDefault(instance =>
            string.Equals(
                instance.instanceId,
                selection.TargetId,
                StringComparison.Ordinal));
        if (source == null || target == null)
        {
            EquipmentCraftingPanelPresenter.AddText(
                parent,
                "원본과 대상을 선택하면 소비·이전·유지 항목을 비교할 수 있습니다.",
                font,
                14f,
                DungeonUiTheme.TextSecondary,
                30f,
                created);
            return;
        }

        equipment.TryGetDefinition(
            source.definitionId,
            out CombatEquipmentDefinitionSO sourceDefinition);
        equipment.TryGetDefinition(
            target.definitionId,
            out CombatEquipmentDefinitionSO targetDefinition);
        bool sameLineage = sourceDefinition != null
            && targetDefinition != null
            && sourceDefinition.LineageKind == targetDefinition.LineageKind;
        bool sourceHasModules = HasInstalledModule(source);
        bool activeOrder = equipment.HistoryTransferOrders.Any(order =>
            order != null
            && !order.completed
            && (order.sourceEquipmentInstanceId == source.instanceId
                || order.targetEquipmentInstanceId == source.instanceId
                || order.sourceEquipmentInstanceId == target.instanceId
                || order.targetEquipmentInstanceId == target.instanceId));
        EquipmentEvolutionState history = source.evolution
            ?? new EquipmentEvolutionState();
        string comparison =
            "소비: 원본 장비 + 계보 인장\n"
            + $"이전: 세대 {history.generation}, 숙련 {history.mastery:0.#}, "
            + $"진화 {history.evolutionNodes?.Count ?? 0}, 조율 "
            + $"{history.attunements?.Count ?? 0}, 역사 "
            + $"{history.activeHistoricalNodeIds?.Count ?? 0}\n"
            + $"유지: 대상의 형태 {targetDefinition?.DisplayName ?? target.definitionId}, "
            + $"재질 {target.materialId}, 품질 {target.quality}, "
            + $"내구 {target.durabilityRatio:P0}, 장착 부품 "
            + $"{CountInstalledModules(target)}\n"
            + $"검증: 계열 {(sameLineage ? "일치" : "불일치")}, "
            + $"원본 부품 {(sourceHasModules ? "제거 필요" : "없음")}, "
            + $"진행 주문 {(activeOrder ? "충돌" : "없음")}";
        EquipmentCraftingPanelPresenter.AddText(
            parent,
            comparison,
            font,
            14f,
            sameLineage && !sourceHasModules && !activeOrder
                ? DungeonUiTheme.TextPrimary
                : DungeonUiTheme.Warning,
            108f,
            created);
    }

    private void AddModuleCommand(
        Transform parent,
        string label,
        string prefix,
        string instanceId,
        Func<bool> command,
        TMP_FontAsset font,
        Action refresh,
        float width = 82f)
    {
        EquipmentCraftingPanelPresenter.AddButton(
            parent,
            label,
            font,
            false,
            () =>
            {
                command();
                refresh?.Invoke();
            },
            width,
            objectName: prefix
                + EquipmentCraftingPanelPresenter.Sanitize(instanceId));
    }

    private LineageSelection GetSelection(string facilityId)
    {
        if (!lineageByFacility.TryGetValue(facilityId, out LineageSelection state))
        {
            state = new LineageSelection();
            lineageByFacility.Add(facilityId, state);
        }
        return state;
    }

    private static void NormalizeSelection(
        LineageSelection selection,
        IReadOnlyList<CombatEquipmentInstance> candidates,
        IReadOnlyList<WorldItemStackSnapshot> seals)
    {
        if (!candidates.Any(instance => instance.instanceId == selection.SourceId))
        {
            selection.SourceId = string.Empty;
        }
        if (!candidates.Any(instance => instance.instanceId == selection.TargetId))
        {
            selection.TargetId = string.Empty;
        }
        if (!seals.Any(stack => stack.StackId == selection.SealStackId))
        {
            selection.SealStackId = string.Empty;
        }
    }

    private string EquipmentName(CombatEquipmentInstance instance)
    {
        return equipment.TryGetDefinition(
                instance?.definitionId,
                out CombatEquipmentDefinitionSO definition)
            ? definition.DisplayName
            : instance?.definitionId ?? string.Empty;
    }

    private string DescribeEquipment(CombatEquipmentInstance instance)
    {
        equipment.TryGetDefinition(
            instance.definitionId,
            out CombatEquipmentDefinitionSO definition);
        return $"{definition?.DisplayName ?? instance.definitionId} · "
            + $"{instance.materialId} · {instance.quality} · "
            + $"내구 {instance.durabilityRatio:P0} · 슬롯 "
            + $"{CountInstalledModules(instance)}/{definition?.ModuleSlotCount ?? 0}";
    }

    private static string DescribeModuleState(EquipmentModuleInstance module)
    {
        if (module.state == EquipmentModuleProcessState.Unidentified)
        {
            return "미확인 · 다음 시설: 부품 감정대";
        }
        if (module.state == EquipmentModuleProcessState.IdentifiedDamaged)
        {
            return $"손상 {module.condition:P0} · 다음 시설: 부품 복원 작업대";
        }
        if (module.state == EquipmentModuleProcessState.Restored
            && module.grade == 4
            && !module.runeTuned)
        {
            return "복원됨 · 다음 시설: 룬 조율실";
        }
        if (module.state == EquipmentModuleProcessState.Restored
            || module.state == EquipmentModuleProcessState.Tuned)
        {
            return "장착 가능 · 다음 시설: 정밀 장착대";
        }
        if (module.state == EquipmentModuleProcessState.Installed)
        {
            return $"장착 중 · {module.attachedEquipmentInstanceId}";
        }
        return module.state.ToString();
    }

    private static bool IsReadyForInstallation(EquipmentModuleInstance module)
    {
        return module.identified
            && module.condition >= 0.75f
            && (module.state == EquipmentModuleProcessState.Restored
                || module.state == EquipmentModuleProcessState.Tuned)
            && (module.grade != 4 || module.runeTuned);
    }

    private static bool IsModuleVisibleAt(
        EquipmentModuleInstance module,
        string workstationTag)
    {
        return workstationTag switch
        {
            EquipmentProgressionWorkstationTags.Appraisal =>
                module.state == EquipmentModuleProcessState.Unidentified,
            EquipmentProgressionWorkstationTags.Restoration =>
                module.state == EquipmentModuleProcessState.IdentifiedDamaged,
            EquipmentProgressionWorkstationTags.RuneTuning =>
                module.state == EquipmentModuleProcessState.Restored
                && module.grade == 4
                && !module.runeTuned,
            EquipmentProgressionWorkstationTags.PrecisionFitting =>
                IsReadyForInstallation(module)
                || module.state == EquipmentModuleProcessState.Installed,
            _ => false
        };
    }

    private static bool HasInstalledModule(CombatEquipmentInstance instance)
    {
        return CountInstalledModules(instance) > 0;
    }

    private static int CountInstalledModules(CombatEquipmentInstance instance)
    {
        return instance?.moduleSlots?.Count(slot => slot != null
            && !string.IsNullOrWhiteSpace(slot.moduleInstanceId)) ?? 0;
    }

    private bool Success(string message, Action<string> showFeedback)
    {
        showFeedback?.Invoke(message);
        return true;
    }

    private bool Fail(DomainFailure failure, Action<string> showFeedback)
    {
        showFeedback?.Invoke(failureLocalizer.Localize(failure));
        return false;
    }
}
