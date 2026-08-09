using System;
using System.Collections.Generic;

internal static class WorkOrderSaveValidation
{
    internal const int MaxSavedOrders = 4096;

    public static void Validate(
        DungeonWorkOrderSaveData snapshot,
        DungeonGameRestoreReport report,
        Func<int, BuildingSO> findBuilding,
        Func<string, bool> itemDefinitionExists)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (snapshot == null)
        {
            report.AddError("Work-order payload is null.");
            return;
        }

        if (snapshot.version != DungeonWorkOrderSaveData.CurrentVersion)
        {
            report.AddError(
                $"Unsupported work-order payload version {snapshot.version}; expected {DungeonWorkOrderSaveData.CurrentVersion}.");
        }

        if (snapshot.nextOrderSequence < 1)
        {
            report.AddError("Work-order next sequence must be positive.");
        }

        if (snapshot.orders == null)
        {
            report.AddError("Work-order payload has no order list.");
            return;
        }

        if (snapshot.orders.Count > MaxSavedOrders)
        {
            report.AddError(
                $"Work-order payload exceeds the {MaxSavedOrders}-order limit.");
        }

        HashSet<string> ids = new HashSet<string>(StringComparer.Ordinal);
        HashSet<string> constructionTargets =
            new HashSet<string>(StringComparer.Ordinal);
        int highestSequence = 0;
        string previousOrderId = string.Empty;
        for (int index = 0; index < snapshot.orders.Count; index++)
        {
            WorkOrderSaveData order = snapshot.orders[index];
            if (order == null)
            {
                report.AddError($"Work-order payload order {index} is null.");
                continue;
            }

            string orderId = order.workOrderId?.Trim() ?? string.Empty;
            if (!TryParseOrderSequence(orderId, out int sequence)
                || !string.Equals(
                    order.workOrderId,
                    orderId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    orderId,
                    $"work:{sequence:D6}",
                    StringComparison.Ordinal)
                || !ids.Add(orderId))
            {
                report.AddError(
                    $"Work-order payload contains invalid or duplicate ID '{orderId}'.");
            }
            else
            {
                highestSequence = Math.Max(highestSequence, sequence);
                if (index > 0
                    && string.CompareOrdinal(previousOrderId, orderId) >= 0)
                {
                    report.AddError(
                        "Work-order payload orders must use canonical ascending ID order.");
                }

                previousOrderId = orderId;
            }

            if (!WorkTypeCatalog.TryGet(
                    order.workTypeId,
                    out WorkTypeDefinition definition))
            {
                report.AddError(
                    $"Work order '{orderId}' references unknown work type '{order.workTypeId}'.");
                continue;
            }
            if (!string.Equals(
                    order.workTypeId,
                    definition.WorkTypeId.Value,
                    StringComparison.Ordinal))
            {
                report.AddError(
                    $"Work order '{orderId}' has non-canonical work type '{order.workTypeId}'.");
            }

            if (order.targetBuildingId <= 0)
            {
                report.AddError(
                    $"Work order '{orderId}' has invalid target building ID {order.targetBuildingId}.");
            }

            if (!IsFinitePositive(order.requiredWork)
                || float.IsNaN(order.completedWork)
                || float.IsInfinity(order.completedWork)
                || order.completedWork < 0f
                || order.completedWork > order.requiredWork)
            {
                report.AddError(
                    $"Work order '{orderId}' has invalid work progress {order.completedWork}/{order.requiredWork}.");
            }

            if (!Enum.IsDefined(typeof(WorkOrderStatus), order.status)
                || order.status == WorkOrderStatus.InProgress
                || order.status == WorkOrderStatus.Completed
                || order.status == WorkOrderStatus.Cancelled)
            {
                report.AddError(
                    $"Work order '{orderId}' has non-restorable status {order.status}.");
            }

            if (order.materialDestinationId == null
                || !string.Equals(
                    order.materialDestinationId,
                    order.materialDestinationId.Trim(),
                    StringComparison.Ordinal)
                || !string.IsNullOrEmpty(order.reservedWorkerPersistentId))
            {
                report.AddError(
                    $"Work order '{orderId}' has non-canonical destination or transient worker reservation state.");
            }

            ValidateWorkerPolicy(order, orderId, report);
            ValidateCraftState(order, orderId, report);

            ValidateMaterials(
                order,
                orderId,
                report,
                itemDefinitionExists);
            ValidateMaterialList(
                order.recoveryOutputs,
                $"Work order '{orderId}' recovery outputs",
                report,
                itemDefinitionExists);
            if (definition.WorkTypeId != BuiltInWorkTypeIds.Construct)
            {
                continue;
            }

            BuildingSO building = findBuilding?.Invoke(order.targetBuildingId);
            if (building == null)
            {
                report.AddError(
                    $"Construction order '{orderId}' references missing building {order.targetBuildingId}.");
                continue;
            }

            string expectedDestination =
                $"{WorkOrderRuntime.ConstructionDestinationPrefix}{building.id}:{order.gridX}:{order.gridY}";
            if (!string.Equals(
                    order.materialDestinationId,
                    expectedDestination,
                    StringComparison.Ordinal))
            {
                report.AddError(
                    $"Construction order '{orderId}' has destination '{order.materialDestinationId}', expected '{expectedDestination}'.");
            }

            string targetKey =
                $"{order.targetBuildingId}:{order.gridX}:{order.gridY}";
            if (!constructionTargets.Add(targetKey))
            {
                report.AddError(
                    $"Construction order '{orderId}' duplicates target {targetKey}.");
            }
        }

        if (snapshot.nextOrderSequence <= highestSequence)
        {
            report.AddError(
                $"Work-order next sequence {snapshot.nextOrderSequence} does not exceed existing sequence {highestSequence}.");
        }

        ValidateQualityPipelines(snapshot, report);
    }

    private static void ValidateQualityPipelines(
        DungeonWorkOrderSaveData snapshot,
        DungeonGameRestoreReport report)
    {
        if (snapshot.qualityPipelines == null)
        {
            report.AddError("Work-order payload has no quality pipeline list.");
            return;
        }
        HashSet<string> pipelineIds = new(StringComparer.Ordinal);
        string previous = string.Empty;
        foreach (QualityTargetPipelineSaveData pipeline in snapshot.qualityPipelines)
        {
            string id = pipeline?.pipelineId?.Trim() ?? string.Empty;
            if (pipeline == null
                || id.Length == 0
                || !string.Equals(id, pipeline.pipelineId, StringComparison.Ordinal)
                || !pipelineIds.Add(id)
                || pipeline.definitionId == null
                || !string.Equals(
                    pipeline.definitionId,
                    pipeline.definitionId.Trim(),
                    StringComparison.Ordinal)
                || !Enum.IsDefined(typeof(CraftsmanshipQualityTier), pipeline.minimumQuality)
                || !Enum.IsDefined(typeof(RejectedOutputDisposition), pipeline.rejectedDisposition)
                || !Enum.IsDefined(typeof(QualityRepeatLimitMode), pipeline.limitMode)
                || !Enum.IsDefined(typeof(QualityTargetPipelineStage), pipeline.stage)
                || pipeline.requiredAcceptedCount <= 0
                || pipeline.acceptedCount < 0
                || pipeline.acceptedCount > pipeline.requiredAcceptedCount
                || pipeline.attemptIndex < 0
                || pipeline.maximumAttempts <= 0
                || pipeline.workBudget < 0f
                || pipeline.consumedWork < 0f
                || pipeline.footprintWidth <= 0
                || pipeline.footprintHeight <= 0)
            {
                report.AddError($"Quality pipeline '{id}' is invalid.");
                continue;
            }
            if (previous.Length > 0 && string.CompareOrdinal(previous, id) >= 0)
            {
                report.AddError("Quality pipelines must use canonical ascending ID order.");
            }
            previous = id;

            ValidateWorkerPolicy(
                new WorkOrderSaveData { workerPolicy = pipeline.workerPolicy },
                id,
                report);
            if (pipeline.currentRoll != null
                && (pipeline.currentRoll.attemptIndex != pipeline.attemptIndex
                    || pipeline.currentRoll.randomA < -10 || pipeline.currentRoll.randomA > 10
                    || pipeline.currentRoll.randomB < -10 || pipeline.currentRoll.randomB > 10
                    || pipeline.currentRoll.randomC < -10 || pipeline.currentRoll.randomC > 10))
            {
                report.AddError($"Quality pipeline '{id}' has invalid fixed random state.");
            }
        }

        foreach (WorkOrderSaveData order in snapshot.orders)
        {
            string pipelineId = order?.qualityPipelineId?.Trim() ?? string.Empty;
            if (pipelineId.StartsWith("quality:", StringComparison.Ordinal)
                && !pipelineIds.Contains(pipelineId))
            {
                report.AddError(
                    $"Work order '{order.workOrderId}' references missing quality pipeline '{pipelineId}'.");
            }
        }
    }

    private static void ValidateMaterials(
        WorkOrderSaveData order,
        string orderId,
        DungeonGameRestoreReport report,
        Func<string, bool> itemDefinitionExists)
    {
        ValidateMaterialList(
            order.itemMaterials,
            $"Work order '{orderId}' item materials",
            report,
            itemDefinitionExists);
    }

    private static void ValidateMaterialList(
        IReadOnlyList<WorkOrderItemMaterialSaveData> materials,
        string label,
        DungeonGameRestoreReport report,
        Func<string, bool> itemDefinitionExists)
    {
        if (materials == null)
        {
            report.AddError($"{label} list is missing.");
            return;
        }

        HashSet<string> itemIds = new HashSet<string>(StringComparer.Ordinal);
        string previousItemId = string.Empty;
        foreach (WorkOrderItemMaterialSaveData material in materials)
        {
            string itemId = material?.itemId?.Trim() ?? string.Empty;
            if (material == null
                || itemId.Length == 0
                || !string.Equals(
                    material.itemId,
                    itemId,
                    StringComparison.Ordinal)
                || itemId.StartsWith("stock-item:", StringComparison.Ordinal)
                || itemDefinitionExists == null
                || !itemDefinitionExists(itemId)
                || material.required <= 0
                || material.delivered < 0
                || material.delivered > material.required
                || !itemIds.Add(itemId))
            {
                report.AddError(
                    $"{label} contains an invalid or duplicate material '{itemId}'.");
            }
            else if (previousItemId.Length > 0
                && string.CompareOrdinal(previousItemId, itemId) >= 0)
            {
                report.AddError(
                    $"{label} are not in canonical order.");
            }
            else
            {
                previousItemId = itemId;
            }
        }
    }

    private static void ValidateWorkerPolicy(
        WorkOrderSaveData order,
        string orderId,
        DungeonGameRestoreReport report)
    {
        WorkerSelectionPolicySaveData policy = order.workerPolicy;
        if (policy == null
            || !Enum.IsDefined(typeof(WorkerSelectionMode), policy.mode)
            || !Enum.IsDefined(typeof(WorkerRequirementMatchMode), policy.matchMode)
            || !Enum.IsDefined(typeof(WorkerCandidateSortMode), policy.sortMode)
            || !Enum.IsDefined(typeof(CareerRank), policy.minimumCareerRank))
        {
            report.AddError($"Work order '{orderId}' has an invalid worker policy.");
            return;
        }

        ValidateCanonicalIds(policy.specificCharacterIds, orderId, "specific worker", report);
        ValidateCanonicalIds(policy.excludedCharacterIds, orderId, "excluded worker", report);
        ValidateCanonicalIds(policy.requiredTraitIds, orderId, "required trait", report);
        ValidateCanonicalIds(policy.excludedTraitIds, orderId, "excluded trait", report);
        if (policy.minimumSkillExperience < 0
            || policy.minimumSkillId == null
            || !string.Equals(policy.minimumSkillId, policy.minimumSkillId.Trim(), StringComparison.Ordinal))
        {
            report.AddError($"Work order '{orderId}' has invalid skill requirements.");
        }

        HashSet<int> statTypes = new();
        foreach (WorkerStatRequirementSaveData requirement in
                 policy.statRequirements ?? new List<WorkerStatRequirementSaveData>())
        {
            if (requirement == null
                || !Enum.IsDefined(typeof(CharacterStatType), requirement.statType)
                || requirement.minimumValue < 0
                || !statTypes.Add(requirement.statType))
            {
                report.AddError($"Work order '{orderId}' has invalid or duplicate stat requirements.");
                break;
            }
        }
    }

    private static void ValidateCraftState(
        WorkOrderSaveData order,
        string orderId,
        DungeonGameRestoreReport report)
    {
        HashSet<string> contributorIds = new(StringComparer.Ordinal);
        foreach (CraftContributionSaveData contribution in
                 order.contributions ?? new List<CraftContributionSaveData>())
        {
            string characterId = contribution?.characterId?.Trim() ?? string.Empty;
            if (contribution == null
                || characterId.Length == 0
                || !string.Equals(characterId, contribution.characterId, StringComparison.Ordinal)
                || !contributorIds.Add(characterId)
                || !IsFinitePositive(contribution.contributedWork)
                || float.IsNaN(contribution.relevantSkill)
                || float.IsInfinity(contribution.relevantSkill)
                || contribution.relevantSkill < 0f)
            {
                report.AddError($"Work order '{orderId}' has invalid craft contribution state.");
                break;
            }
        }

        if (order.qualityAttemptIndex < 0
            || order.qualityRoll == null
            || order.qualityRoll.attemptIndex != order.qualityAttemptIndex
            || order.qualityRoll.randomA < -10 || order.qualityRoll.randomA > 10
            || order.qualityRoll.randomB < -10 || order.qualityRoll.randomB > 10
            || order.qualityRoll.randomC < -10 || order.qualityRoll.randomC > 10)
        {
            report.AddError($"Work order '{orderId}' has invalid deterministic quality roll state.");
        }

        string pipelineId = order.qualityPipelineId?.Trim() ?? string.Empty;
        if (!string.Equals(pipelineId, order.qualityPipelineId ?? string.Empty, StringComparison.Ordinal))
        {
            report.AddError($"Work order '{orderId}' has a non-canonical quality pipeline ID.");
        }
    }

    private static void ValidateCanonicalIds(
        IEnumerable<string> source,
        string orderId,
        string label,
        DungeonGameRestoreReport report)
    {
        HashSet<string> ids = new(StringComparer.Ordinal);
        foreach (string value in source ?? Array.Empty<string>())
        {
            string canonical = value?.Trim() ?? string.Empty;
            if (canonical.Length == 0
                || !string.Equals(value, canonical, StringComparison.Ordinal)
                || !ids.Add(canonical))
            {
                report.AddError($"Work order '{orderId}' has an invalid or duplicate {label} ID.");
                return;
            }
        }
    }

    private static bool TryParseOrderSequence(
        string orderId,
        out int sequence)
    {
        const string prefix = "work:";
        sequence = 0;
        return orderId.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(
                orderId.Substring(prefix.Length),
                out sequence)
            && sequence > 0;
    }

    private static bool IsFinitePositive(float value)
    {
        return !float.IsNaN(value)
            && !float.IsInfinity(value)
            && value > 0f;
    }
}
