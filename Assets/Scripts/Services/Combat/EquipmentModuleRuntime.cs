using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

/// <summary>
/// Owns the lifecycle of expedition equipment modules. Module state lives in the
/// physical item repository; this aggregate only applies validated transitions.
/// </summary>
public interface IEquipmentModuleAppraisalRecovery
{
    bool TryRecoverPendingAppraisals(out DomainFailure failure);
}

public sealed class EquipmentModuleRuntime : IEquipmentModuleAppraisalRecovery
{
    private const string MaterialTestCouponItemId = "component:material-test-coupon";
    private const string AppraisalDispositionReasonCode =
        "equipment-module-material-test";
    private readonly IItemInstanceRepository itemInstances;
    private readonly ICombatEquipmentCatalog equipmentCatalog;
    private readonly IEquipmentModuleCatalog moduleCatalog;
    private readonly BlueprintResearchRuntime research;
    private readonly CombatEquipmentPhysicalStateWriter physicalState;
    private readonly IEquipmentPhysicalItemGateway physicalItems;
    private readonly IFacilityCapabilityQuery facilities;

    private IDictionary<string, CombatEquipmentInstance> EquipmentInstances =>
        itemInstances.EquipmentInstances;
    private IDictionary<string, EquipmentModuleInstance> Modules =>
        itemInstances.EquipmentModules;

    public EquipmentModuleRuntime(
        IItemInstanceRepository itemInstances,
        ICombatEquipmentCatalog equipmentCatalog,
        IEquipmentModuleCatalog moduleCatalog,
        ProgressionSceneRuntimeReferences progressionRuntimes,
        CombatEquipmentPhysicalStateWriter physicalState,
        IEquipmentPhysicalItemGateway physicalItems,
        IFacilityCapabilityQuery facilities)
    {
        this.itemInstances = itemInstances
            ?? throw new ArgumentNullException(nameof(itemInstances));
        this.equipmentCatalog = equipmentCatalog
            ?? throw new ArgumentNullException(nameof(equipmentCatalog));
        this.moduleCatalog = moduleCatalog
            ?? throw new ArgumentNullException(nameof(moduleCatalog));
        research = (progressionRuntimes
                ?? throw new ArgumentNullException(nameof(progressionRuntimes)))
            .BlueprintResearch
            ?? throw new InvalidOperationException(
                $"{nameof(EquipmentModuleRuntime)} requires a loaded {nameof(BlueprintResearchRuntime)}.");
        this.physicalState = physicalState
            ?? throw new ArgumentNullException(nameof(physicalState));
        this.physicalItems = physicalItems
            ?? throw new ArgumentNullException(nameof(physicalItems));
        this.facilities = facilities
            ?? throw new ArgumentNullException(nameof(facilities));
    }

    public IReadOnlyCollection<EquipmentModuleInstance> Snapshots =>
        Modules.Values.Select(module => module.Clone()).ToArray();

    public EquipmentModuleInstance CreateExpeditionModule(
        string definitionId,
        int grade,
        Vector2Int deliveryPosition,
        WorldItemStackState worldState,
        string destinationId,
        bool identified)
    {
        if (!moduleCatalog.TryGet(definitionId, out _))
        {
            throw new KeyNotFoundException(
                $"Unknown equipment module definition '{definitionId}'.");
        }
        string normalizedDestination = destinationId?.Trim() ?? string.Empty;
        if (worldState == WorldItemStackState.FacilityBuffer
            && string.IsNullOrWhiteSpace(normalizedDestination))
        {
            throw new ArgumentException(
                "A facility-buffer equipment module requires a destination ID.",
                nameof(destinationId));
        }

        int safeGrade = Mathf.Clamp(grade, 1, 4);
        EquipmentModuleInstance created = new EquipmentModuleInstance
        {
            instanceId = itemInstances.AllocateItemInstanceId().Value,
            definitionId = definitionId.Trim(),
            grade = safeGrade,
            condition = identified ? Mathf.Clamp01(0.5f + safeGrade * 0.05f) : 0.5f,
            identified = identified,
            runeTuned = false,
            state = identified
                ? EquipmentModuleProcessState.IdentifiedDamaged
                : EquipmentModuleProcessState.Unidentified
        };
        Modules.Add(created.instanceId, created);
        if (!physicalItems.SpawnExistingUniqueItemAt(
                PhysicalItemIds.ForEquipmentModule(),
                (ItemInstanceId)created.instanceId,
                deliveryPosition,
                worldState,
                normalizedDestination,
                out string stackId))
        {
            Modules.Remove(created.instanceId);
            throw new InvalidOperationException(
                $"Failed to materialize equipment module '{created.instanceId}'.");
        }
        created.sourceStackId = stackId;
        if (!PersistModulePhysicalState(created))
        {
            physicalItems.TryAbsorbUniqueItemStack(
                stackId,
                (ItemInstanceId)created.instanceId);
            Modules.Remove(created.instanceId);
            throw new InvalidOperationException(
                $"Failed to persist equipment module '{created.instanceId}'.");
        }
        return created.Clone();
    }

    public bool TryAppraise(
        string moduleInstanceId,
        BuildableObject facility,
        out DomainFailure failure)
    {
        string normalizedModuleId = moduleInstanceId?.Trim() ?? string.Empty;
        if (!Modules.TryGetValue(
                normalizedModuleId,
                out EquipmentModuleInstance module))
        {
            failure = new DomainFailure(FailureCode.ModuleNotUnidentified);
            return false;
        }
        if (!TryRecoverPendingAppraisal(
                module,
                out bool recoveredOperation,
                out failure))
        {
            return false;
        }
        if (recoveredOperation)
        {
            return true;
        }

        if (!TryRequireFacility(
                facility,
                EquipmentProgressionWorkstationTags.Appraisal,
                out string destinationId,
                out failure))
        {
            return false;
        }
        if (!HasCompletedResearch("research:equipment:relic-appraisal"))
        {
            failure = new DomainFailure(
                FailureCode.RequiredResearchUnavailable,
                "research:equipment:relic-appraisal",
                "facility:equipment:appraisal-bench");
            return false;
        }
        if (module.state != EquipmentModuleProcessState.Unidentified
            || module.identified)
        {
            failure = new DomainFailure(FailureCode.ModuleNotUnidentified);
            return false;
        }
        if (!IsModuleInLocalBuffer(module, destinationId))
        {
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }

        if (!TryPrepareAppraisalSupplies(
                facility,
                destinationId,
                out WorldItemStackSnapshot coupon,
                out WorldItemStackSnapshot gauge,
                out WorldItemStackSnapshot lens,
                out failure))
        {
            return false;
        }

        EquipmentModuleInstance beforeIntent = module.Clone();
        int sequence = module.nextAppraisalOperationSequence;
        string operationId = FormatAppraisalOperationId(
            module.instanceId,
            sequence);
        float gaugeBefore = DurableToolItemRules.ReadCurrentDurability(
            gauge.ItemId,
            gauge.Components);
        float lensBefore = DurableToolItemRules.ReadCurrentDurability(
            lens.ItemId,
            lens.Components);
        module.pendingAppraisal = new EquipmentModuleAppraisalCommitSaveData
        {
            phase = (int)EquipmentModuleAppraisalCommitPhase.IntentRecorded,
            operationSequence = sequence,
            operationId = operationId,
            reasonCode = AppraisalDispositionReasonCode,
            moduleInstanceId = module.instanceId,
            destinationId = destinationId,
            couponStackId = coupon.StackId,
            couponItemId = MaterialTestCouponItemId,
            quantity = 1,
            moduleIdentifiedBefore = false,
            moduleIdentifiedAfter = true,
            moduleStateBefore = EquipmentModuleProcessState.Unidentified,
            moduleStateAfter = EquipmentModuleProcessState.IdentifiedDamaged,
            gaugeStackId = gauge.StackId,
            gaugeItemId = gauge.ItemId,
            gaugeDurabilityBefore = gaugeBefore,
            gaugeDurabilityAfter = Mathf.Max(0f, gaugeBefore - 1f),
            lensStackId = lens.StackId,
            lensItemId = lens.ItemId,
            lensDurabilityBefore = lensBefore,
            lensDurabilityAfter = Mathf.Max(0f, lensBefore - 2f)
        };
        if (!PersistModulePhysicalState(module))
        {
            RestoreModuleState(module, beforeIntent);
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }

        if (!physicalItems.TryCommitPendingBatchPhysicalDisposition(
                new[] { new PhysicalItemTransformInput(coupon.StackId, 1) },
                PhysicalItemDispositionKind.Sink,
                operationId,
                AppraisalDispositionReasonCode,
                out _,
                out _))
        {
            if (!TryClearPendingAppraisal(
                    module,
                    advanceSequence: false))
            {
                throw new InvalidOperationException(
                    $"Could not clear uncommitted appraisal intent '{operationId}'.");
            }
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }

        return TryRecoverPendingAppraisal(
                module,
                out bool completed,
                out failure)
            && completed;
    }

    private bool TryPrepareAppraisalSupplies(
        BuildableObject facility,
        string destinationId,
        out WorldItemStackSnapshot coupon,
        out WorldItemStackSnapshot gauge,
        out WorldItemStackSnapshot lens,
        out DomainFailure failure)
    {
        coupon = FindAvailableCoupon(destinationId);
        gauge = FindUsableTool(destinationId, DurableToolItemRules.InspectionGauge);
        lens = FindUsableTool(destinationId, DurableToolItemRules.RuneIdentificationLens);
        bool hasCoupon = coupon != null;
        if (gauge != null && lens != null && hasCoupon)
        {
            failure = DomainFailure.None;
            return true;
        }

        RequestMissingAppraisalSupply(
            facility,
            destinationId,
            MaterialTestCouponItemId,
            hasCoupon);
        RequestMissingAppraisalSupply(
            facility,
            destinationId,
            DurableToolItemRules.InspectionGauge,
            gauge != null);
        RequestMissingAppraisalSupply(
            facility,
            destinationId,
            DurableToolItemRules.RuneIdentificationLens,
            lens != null);
        failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
        return false;
    }

    private WorldItemStackSnapshot FindAvailableCoupon(string destinationId)
    {
        return physicalItems.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.FacilityBuffer
                && stack.ReservedQuantity == 0
                && string.IsNullOrEmpty(stack.ReservedByPersistentId)
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.ItemId,
                    MaterialTestCouponItemId,
                    StringComparison.Ordinal)
                && stack.AvailableQuantity > 0)
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    private void RequestMissingAppraisalSupply(
        BuildableObject facility,
        string destinationId,
        string itemId,
        bool available)
    {
        if (available || physicalItems.GetAllStacks().Any(stack =>
                stack != null
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
                && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)))
        {
            return;
        }

        physicalItems.TryRequestItemDelivery(
            itemId,
            1,
            facility.centerPos,
            destinationId,
            out _,
            out _);
    }

    private WorldItemStackSnapshot FindUsableTool(string destinationId, string itemId)
    {
        return physicalItems.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal)
                && DurableToolItemRules.ReadCurrentDurability(
                    stack.ItemId,
                    stack.Components) > 0f)
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
    }

    internal static string FormatAppraisalOperationId(
        string moduleInstanceId,
        int sequence) =>
        $"equipment-module-appraisal:{moduleInstanceId}:{sequence:D8}";

    public bool TryRecoverPendingAppraisals(out DomainFailure failure)
    {
        foreach (EquipmentModuleInstance module in Modules.Values
                     .Where(value => value != null)
                     .OrderBy(value => value.instanceId, StringComparer.Ordinal))
        {
            if (!TryRecoverPendingAppraisal(module, out _, out failure))
            {
                return false;
            }
        }

        failure = DomainFailure.None;
        return true;
    }

    private bool TryRecoverPendingAppraisal(
        EquipmentModuleInstance module,
        out bool recoveredOperation,
        out DomainFailure failure)
    {
        recoveredOperation = false;
        failure = DomainFailure.None;
        EquipmentModuleAppraisalCommitSaveData pending =
            module?.pendingAppraisal
            ?? new EquipmentModuleAppraisalCommitSaveData();
        EquipmentModuleAppraisalCommitPhase phase =
            (EquipmentModuleAppraisalCommitPhase)pending.phase;
        if (phase == EquipmentModuleAppraisalCommitPhase.None)
        {
            return true;
        }

        if (!AppraisalContractMatches(module, pending))
        {
            throw new InvalidOperationException(
                $"Equipment module appraisal '{pending.operationId}' conflicts with module '{module?.instanceId}'.");
        }

        bool hasReceipt = physicalItems.TryGetPendingBatchPhysicalDisposition(
            pending.operationId,
            out PhysicalItemBatchDispositionReceipt receipt);
        if (!hasReceipt)
        {
            bool terminalOutcome =
                phase == EquipmentModuleAppraisalCommitPhase.OutcomePublished;
            if (terminalOutcome && !AppraisalOutcomesMatchAfter(module, pending))
            {
                throw new InvalidOperationException(
                    $"Acknowledged appraisal '{pending.operationId}' has incomplete outcomes.");
            }
            if (!terminalOutcome && !AppraisalOutcomesMatchBefore(module, pending))
            {
                throw new InvalidOperationException(
                    $"Uncommitted appraisal '{pending.operationId}' mutated an outcome.");
            }
            if (!TryClearPendingAppraisal(
                    module,
                    advanceSequence: terminalOutcome))
            {
                failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
                return false;
            }
            recoveredOperation = terminalOutcome;
            return true;
        }

        if (!ReceiptMatches(pending, receipt))
        {
            throw new InvalidOperationException(
                $"Equipment module appraisal '{pending.operationId}' has a mismatched physical receipt.");
        }

        if (phase == EquipmentModuleAppraisalCommitPhase.IntentRecorded)
        {
            if (!TryPublishAppraisalOutcomes(module, pending, out failure))
            {
                return false;
            }

            pending.phase =
                (int)EquipmentModuleAppraisalCommitPhase.OutcomePublished;
            pending.sourceStackIds = receipt.SourceStackIds
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToList();
            pending.inputMassGrams = receipt.InputMassGrams;
            pending.commitId = receipt.CommitId;
            if (!PersistModulePhysicalState(module))
            {
                failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
                return false;
            }
        }
        else if (!AppraisalOutcomesMatchAfter(module, pending))
        {
            throw new InvalidOperationException(
                $"Published appraisal '{pending.operationId}' has drifted outcomes.");
        }

        if (!physicalItems.AcknowledgeBatchPhysicalDisposition(
                receipt.CommitId,
                out _))
        {
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }

        if (!TryClearPendingAppraisal(module, advanceSequence: true))
        {
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }
        recoveredOperation = true;
        failure = DomainFailure.None;
        return true;
    }

    private bool TryPublishAppraisalOutcomes(
        EquipmentModuleInstance module,
        EquipmentModuleAppraisalCommitSaveData pending,
        out DomainFailure failure)
    {
        if (!TryPublishModuleAppraisalOutcome(module, pending)
            || !TryPublishToolWear(
                pending.gaugeStackId,
                pending.gaugeItemId,
                pending.gaugeDurabilityBefore,
                pending.gaugeDurabilityAfter)
            || !TryPublishToolWear(
                pending.lensStackId,
                pending.lensItemId,
                pending.lensDurabilityBefore,
                pending.lensDurabilityAfter))
        {
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }

        failure = DomainFailure.None;
        return true;
    }

    private bool TryPublishModuleAppraisalOutcome(
        EquipmentModuleInstance module,
        EquipmentModuleAppraisalCommitSaveData pending)
    {
        if (ModuleStateMatches(
                module,
                pending.moduleIdentifiedAfter,
                pending.moduleStateAfter))
        {
            return true;
        }
        if (!ModuleStateMatches(
                module,
                pending.moduleIdentifiedBefore,
                pending.moduleStateBefore))
        {
            throw new InvalidOperationException(
                $"Appraisal module outcome for '{module.instanceId}' is outside its before/after envelope.");
        }

        module.identified = pending.moduleIdentifiedAfter;
        module.state = pending.moduleStateAfter;
        return PersistModulePhysicalState(module);
    }

    private bool TryPublishToolWear(
        string stackId,
        string itemId,
        float before,
        float after)
    {
        WorldItemStackSnapshot tool = physicalItems.GetAllStacks()
            .FirstOrDefault(value => value != null
                && string.Equals(value.StackId, stackId, StringComparison.Ordinal));
        if (tool == null
            || !string.Equals(tool.ItemId, itemId, StringComparison.Ordinal))
        {
            return false;
        }

        float current = DurableToolItemRules.ReadCurrentDurability(
            tool.ItemId,
            tool.Components);
        if (Approximately(current, after))
        {
            return true;
        }
        if (!Approximately(current, before))
        {
            throw new InvalidOperationException(
                $"Appraisal tool '{stackId}' durability is outside its before/after envelope.");
        }
        return physicalItems.TrySetInstanceComponent(
            stackId,
            DurableToolItemRules.CreateDurability(itemId, after));
    }

    private bool AppraisalOutcomesMatchBefore(
        EquipmentModuleInstance module,
        EquipmentModuleAppraisalCommitSaveData pending) =>
        ModuleStateMatches(
            module,
            pending.moduleIdentifiedBefore,
            pending.moduleStateBefore)
        && ToolDurabilityMatches(
            pending.gaugeStackId,
            pending.gaugeItemId,
            pending.gaugeDurabilityBefore)
        && ToolDurabilityMatches(
            pending.lensStackId,
            pending.lensItemId,
            pending.lensDurabilityBefore);

    private bool AppraisalOutcomesMatchAfter(
        EquipmentModuleInstance module,
        EquipmentModuleAppraisalCommitSaveData pending) =>
        ModuleStateMatches(
            module,
            pending.moduleIdentifiedAfter,
            pending.moduleStateAfter)
        && ToolDurabilityMatches(
            pending.gaugeStackId,
            pending.gaugeItemId,
            pending.gaugeDurabilityAfter)
        && ToolDurabilityMatches(
            pending.lensStackId,
            pending.lensItemId,
            pending.lensDurabilityAfter);

    private bool ToolDurabilityMatches(
        string stackId,
        string itemId,
        float expected)
    {
        WorldItemStackSnapshot stack = physicalItems.GetAllStacks()
            .FirstOrDefault(value => value != null
                && string.Equals(value.StackId, stackId, StringComparison.Ordinal)
                && string.Equals(value.ItemId, itemId, StringComparison.Ordinal));
        return stack != null
            && Approximately(
                DurableToolItemRules.ReadCurrentDurability(
                    stack.ItemId,
                    stack.Components),
                expected);
    }

    private static bool ModuleStateMatches(
        EquipmentModuleInstance module,
        bool identified,
        EquipmentModuleProcessState state) =>
        module != null && module.identified == identified && module.state == state;

    private static bool AppraisalContractMatches(
        EquipmentModuleInstance module,
        EquipmentModuleAppraisalCommitSaveData pending) =>
        module != null
        && pending.operationSequence == module.nextAppraisalOperationSequence
        && pending.operationSequence > 0
        && string.Equals(
            pending.operationId,
            FormatAppraisalOperationId(
                module.instanceId,
                pending.operationSequence),
            StringComparison.Ordinal)
        && string.Equals(
            pending.reasonCode,
            AppraisalDispositionReasonCode,
            StringComparison.Ordinal)
        && string.Equals(
            pending.moduleInstanceId,
            module.instanceId,
            StringComparison.Ordinal)
        && string.Equals(
            pending.couponItemId,
            MaterialTestCouponItemId,
            StringComparison.Ordinal)
        && string.Equals(
            pending.gaugeItemId,
            DurableToolItemRules.InspectionGauge,
            StringComparison.Ordinal)
        && string.Equals(
            pending.lensItemId,
            DurableToolItemRules.RuneIdentificationLens,
            StringComparison.Ordinal)
        && pending.quantity == 1;

    private static bool ReceiptMatches(
        EquipmentModuleAppraisalCommitSaveData pending,
        PhysicalItemBatchDispositionReceipt receipt) =>
        receipt.IsCommitted
        && receipt.Kind == PhysicalItemDispositionKind.Sink
        && string.Equals(
            receipt.OperationId,
            pending.operationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            pending.reasonCode,
            StringComparison.Ordinal)
        && receipt.Quantity == pending.quantity
        && receipt.SourceStackIds.Count == 1
        && string.Equals(
            receipt.SourceStackIds[0],
            pending.couponStackId,
            StringComparison.Ordinal);

    private bool TryClearPendingAppraisal(
        EquipmentModuleInstance module,
        bool advanceSequence)
    {
        int sequenceBefore = module.nextAppraisalOperationSequence;
        EquipmentModuleAppraisalCommitSaveData pendingBefore =
            module.pendingAppraisal?.Clone()
            ?? new EquipmentModuleAppraisalCommitSaveData();
        if (advanceSequence)
        {
            module.nextAppraisalOperationSequence = checked(sequenceBefore + 1);
        }
        module.pendingAppraisal = new EquipmentModuleAppraisalCommitSaveData();
        if (PersistModulePhysicalState(module))
        {
            return true;
        }

        module.nextAppraisalOperationSequence = sequenceBefore;
        module.pendingAppraisal = pendingBefore;
        return false;
    }

    private static bool Approximately(float left, float right) =>
        Mathf.Abs(left - right) <= 0.0001f;

    public bool TryRestore(
        string moduleInstanceId,
        BuildableObject facility,
        out DomainFailure failure)
    {
        if (!TryRequireFacility(
                facility,
                EquipmentProgressionWorkstationTags.Restoration,
                out string destinationId,
                out failure))
        {
            return false;
        }
        if (!HasCompletedResearch("research:equipment:relic-appraisal"))
        {
            failure = new DomainFailure(
                FailureCode.RequiredResearchUnavailable,
                "research:equipment:relic-appraisal",
                "facility:equipment:restoration-bench");
            return false;
        }
        if (!Modules.TryGetValue(moduleInstanceId?.Trim() ?? string.Empty,
                out EquipmentModuleInstance module)
            || !module.identified
            || !string.IsNullOrWhiteSpace(module.attachedEquipmentInstanceId)
            || module.state == EquipmentModuleProcessState.Lost)
        {
            failure = new DomainFailure(FailureCode.ModuleNotRestorable);
            return false;
        }
        if (!IsModuleInLocalBuffer(module, destinationId))
        {
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }

        EquipmentModuleInstance previous = module.Clone();
        module.condition = 1f;
        module.state = module.runeTuned
            ? EquipmentModuleProcessState.Tuned
            : EquipmentModuleProcessState.Restored;
        if (!PersistModulePhysicalState(module))
        {
            RestoreModuleState(module, previous);
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }
        failure = DomainFailure.None;
        return true;
    }

    public bool TryTune(
        string moduleInstanceId,
        BuildableObject facility,
        out DomainFailure failure)
    {
        if (!TryRequireFacility(
                facility,
                EquipmentProgressionWorkstationTags.RuneTuning,
                out string destinationId,
                out failure))
        {
            return false;
        }
        if (!HasCompletedResearch("research:equipment:rune-module-tuning"))
        {
            failure = new DomainFailure(
                FailureCode.RequiredResearchUnavailable,
                "research:equipment:rune-module-tuning",
                "facility:equipment:rune-tuning-room");
            return false;
        }
        if (facilities.FindOperational(
                ResearchFacilityCommandKind.ResonanceTuning).Count == 0)
        {
            failure = new DomainFailure(
                FailureCode.ServiceFeatureMissing,
                "facility:resonance-tuning");
            return false;
        }
        if (!Modules.TryGetValue(moduleInstanceId?.Trim() ?? string.Empty,
                out EquipmentModuleInstance module)
            || module.grade != 4
            || module.state != EquipmentModuleProcessState.Restored
            || module.condition < 0.75f)
        {
            failure = new DomainFailure(FailureCode.ModuleNotTunable);
            return false;
        }
        if (!IsModuleInLocalBuffer(module, destinationId))
        {
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }

        EquipmentModuleInstance previous = module.Clone();
        module.runeTuned = true;
        module.state = EquipmentModuleProcessState.Tuned;
        if (!PersistModulePhysicalState(module))
        {
            RestoreModuleState(module, previous);
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }
        failure = DomainFailure.None;
        return true;
    }

    public bool TryInstall(
        string equipmentInstanceId,
        string moduleInstanceId,
        int slotIndex,
        BuildableObject facility,
        out DomainFailure failure)
    {
        if (!TryRequireFacility(
                facility,
                EquipmentProgressionWorkstationTags.PrecisionFitting,
                out string destinationId,
                out failure))
        {
            return false;
        }
        if (!HasCompletedResearch("research:equipment:precision-fitting"))
        {
            failure = new DomainFailure(
                FailureCode.RequiredResearchUnavailable,
                "research:equipment:precision-fitting",
                "facility:equipment:precision-fitting-bench");
            return false;
        }
        if (!EquipmentInstances.TryGetValue(
                equipmentInstanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance equipment)
            || !equipmentCatalog.TryGet(equipment.definitionId,
                out CombatEquipmentDefinitionSO equipmentDefinition)
            || !Modules.TryGetValue(moduleInstanceId?.Trim() ?? string.Empty,
                out EquipmentModuleInstance module)
            || !moduleCatalog.TryGet(module.definitionId,
                out EquipmentModuleDefinitionSO moduleDefinition))
        {
            failure = new DomainFailure(FailureCode.EquipmentOrModuleMissing);
            return false;
        }
        if (slotIndex < 0 || slotIndex >= equipmentDefinition.ModuleSlotCount)
        {
            failure = new DomainFailure(
                FailureCode.ModuleSlotMissing,
                slotIndex.ToString());
            return false;
        }
        if (moduleDefinition.LineageKind != equipmentDefinition.LineageKind)
        {
            failure = new DomainFailure(FailureCode.ModuleLineageMismatch);
            return false;
        }
        if (!module.identified || module.condition < 0.75f
            || module.state is not EquipmentModuleProcessState.Restored
                and not EquipmentModuleProcessState.Tuned)
        {
            failure = new DomainFailure(FailureCode.ModuleNeedsRestoration);
            return false;
        }
        if (module.grade == 4 && !module.runeTuned)
        {
            failure = new DomainFailure(FailureCode.ModuleNeedsRuneTuning);
            return false;
        }
        if (!EquipmentModuleItemStateCodec.TryValidateAppraisalState(
                module,
                out string appraisalStateError)
            || (EquipmentModuleAppraisalCommitPhase)module.pendingAppraisal.phase
                != EquipmentModuleAppraisalCommitPhase.None)
        {
            throw new InvalidOperationException(
                $"Equipment module '{module.instanceId}' cannot be installed while its appraisal authority is invalid or pending: {appraisalStateError}");
        }
        if (!string.IsNullOrWhiteSpace(module.attachedEquipmentInstanceId))
        {
            failure = new DomainFailure(FailureCode.ModuleAlreadyAttached);
            return false;
        }
        if (!IsEquipmentInLocalBuffer(equipment, destinationId)
            || !IsModuleInLocalBuffer(module, destinationId))
        {
            failure = new DomainFailure(FailureCode.EquipmentOrModuleMissing);
            return false;
        }

        equipment.moduleSlots ??= new List<EquipmentModuleSlotState>();
        EquipmentModuleSlotState slot = equipment.moduleSlots
            .FirstOrDefault(candidate => candidate != null
                && candidate.slotIndex == slotIndex);
        if (slot == null)
        {
            slot = new EquipmentModuleSlotState { slotIndex = slotIndex };
            equipment.moduleSlots.Add(slot);
        }
        if (!string.IsNullOrWhiteSpace(slot.moduleInstanceId)
            && Modules.TryGetValue(slot.moduleInstanceId,
                out EquipmentModuleInstance replaced))
        {
            EquipmentModuleInstance incomingBefore = module.Clone();
            EquipmentModuleInstance replacedBefore = replaced.Clone();
            if (!TryMaterializeReturnedModule(
                    replaced,
                    facility,
                    destinationId,
                    out string returnedStackId))
            {
                failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
                return false;
            }
            if (!physicalItems.TryAbsorbUniqueItemStack(
                    module.sourceStackId,
                    (ItemInstanceId)module.instanceId))
            {
                physicalItems.TryAbsorbUniqueItemStack(
                    returnedStackId,
                    (ItemInstanceId)replaced.instanceId);
                failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
                return false;
            }
            replaced.attachedEquipmentInstanceId = string.Empty;
            replaced.condition = Mathf.Min(replaced.condition, 0.7f);
            replaced.state = EquipmentModuleProcessState.IdentifiedDamaged;
            replaced.sourceStackId = returnedStackId;
            if (!PersistModulePhysicalState(replaced))
            {
                physicalItems.TryAbsorbUniqueItemStack(
                    returnedStackId,
                    (ItemInstanceId)replaced.instanceId);
                if (!physicalItems.SpawnExistingUniqueItemAt(
                        PhysicalItemIds.ForEquipmentModule(),
                        (ItemInstanceId)module.instanceId,
                        facility.centerPos,
                        WorldItemStackState.FacilityBuffer,
                        destinationId,
                        out string restoredIncomingStackId))
                {
                    throw new InvalidOperationException(
                        $"Failed to roll back module '{module.instanceId}' after replacement persistence failed.");
                }
                RestoreModuleState(replaced, replacedBefore);
                RestoreModuleState(module, incomingBefore);
                module.sourceStackId = restoredIncomingStackId;
                if (!PersistModulePhysicalState(module))
                {
                    throw new InvalidOperationException(
                        $"Failed to persist rolled-back module '{module.instanceId}'.");
                }
                failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
                return false;
            }
        }
        else if (!physicalItems.TryAbsorbUniqueItemStack(
                     module.sourceStackId,
                     (ItemInstanceId)module.instanceId))
        {
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }

        slot.moduleInstanceId = module.instanceId;
        module.sourceStackId = string.Empty;
        module.attachedEquipmentInstanceId = equipment.instanceId;
        module.state = EquipmentModuleProcessState.Installed;
        physicalState.Persist(equipment);
        failure = DomainFailure.None;
        return true;
    }

    public bool TryRemove(
        string equipmentInstanceId,
        int slotIndex,
        BuildableObject facility,
        out EquipmentModuleInstance removed,
        out DomainFailure failure)
    {
        removed = null;
        if (!TryRequireFacility(
                facility,
                EquipmentProgressionWorkstationTags.PrecisionFitting,
                out string destinationId,
                out failure))
        {
            return false;
        }
        if (!EquipmentInstances.TryGetValue(
                equipmentInstanceId?.Trim() ?? string.Empty,
                out CombatEquipmentInstance equipment))
        {
            failure = new DomainFailure(FailureCode.EquipmentInstanceMissing);
            return false;
        }
        if (!IsEquipmentInLocalBuffer(equipment, destinationId))
        {
            failure = new DomainFailure(FailureCode.EquipmentInstanceMissing);
            return false;
        }

        EquipmentModuleSlotState slot = equipment.moduleSlots?
            .FirstOrDefault(candidate => candidate != null
                && candidate.slotIndex == slotIndex);
        if (slot == null || string.IsNullOrWhiteSpace(slot.moduleInstanceId)
            || !Modules.TryGetValue(slot.moduleInstanceId,
                out EquipmentModuleInstance module))
        {
            failure = new DomainFailure(
                FailureCode.ModuleSlotEmpty,
                slotIndex.ToString());
            return false;
        }
        if (!TryMaterializeReturnedModule(
                module,
                facility,
                destinationId,
                out string returnedStackId))
        {
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }

        EquipmentModuleInstance previous = module.Clone();
        module.sourceStackId = returnedStackId;
        module.attachedEquipmentInstanceId = string.Empty;
        module.condition = Mathf.Min(module.condition, 0.7f);
        module.state = EquipmentModuleProcessState.IdentifiedDamaged;
        if (!PersistModulePhysicalState(module))
        {
            physicalItems.TryAbsorbUniqueItemStack(
                returnedStackId,
                (ItemInstanceId)module.instanceId);
            RestoreModuleState(module, previous);
            failure = new DomainFailure(FailureCode.EquipmentModuleMissing);
            return false;
        }
        slot.moduleInstanceId = string.Empty;
        removed = module.Clone();
        physicalState.Persist(equipment);
        failure = DomainFailure.None;
        return true;
    }

    private bool HasCompletedResearch(string researchId)
    {
        return string.IsNullOrWhiteSpace(researchId)
            || research.State.Projects.IsCompleted(new ResearchProjectId(researchId));
    }

    private static bool TryRequireFacility(
        BuildableObject facility,
        string requiredTag,
        out string destinationId,
        out DomainFailure failure)
    {
        destinationId = EquipmentProgressionFacilityContract
            .GetLocalBufferDestinationId(facility);
        if (!EquipmentProgressionFacilityContract.Matches(
                facility,
                requiredTag))
        {
            failure = new DomainFailure(
                FailureCode.EquipmentProgressionFacilityUnavailable);
            return false;
        }
        failure = DomainFailure.None;
        return true;
    }

    private bool IsModuleInLocalBuffer(
        EquipmentModuleInstance module,
        string destinationId)
    {
        return module != null
            && !string.IsNullOrWhiteSpace(module.sourceStackId)
            && HasLocalStack(
                module.sourceStackId,
                module.instanceId,
                PhysicalItemIds.ForEquipmentModule(),
                destinationId);
    }

    private bool IsEquipmentInLocalBuffer(
        CombatEquipmentInstance equipment,
        string destinationId)
    {
        return equipment != null
            && !string.IsNullOrWhiteSpace(equipment.sourceStackId)
            && HasLocalStack(
                equipment.sourceStackId,
                equipment.instanceId,
                PhysicalItemIds.ForEquipment(equipment.definitionId),
                destinationId);
    }

    private bool HasLocalStack(
        string stackId,
        string instanceId,
        string itemId,
        string destinationId)
    {
        return physicalItems.GetAllStacks().Any(stack => stack != null
            && stack.Quantity > 0
            && !stack.Forbidden
            && stack.AvailableQuantity > 0
            && stack.State == WorldItemStackState.FacilityBuffer
            && string.Equals(stack.StackId, stackId, StringComparison.Ordinal)
            && string.Equals(
                stack.ItemInstanceId,
                instanceId,
                StringComparison.Ordinal)
            && string.Equals(
                stack.ItemId,
                itemId,
                StringComparison.Ordinal)
            && string.Equals(
                stack.DestinationId,
                destinationId,
                StringComparison.Ordinal));
    }

    private bool TryMaterializeReturnedModule(
        EquipmentModuleInstance module,
        BuildableObject facility,
        string destinationId,
        out string stackId)
    {
        stackId = string.Empty;
        return module != null
            && string.IsNullOrWhiteSpace(module.sourceStackId)
            && physicalItems.SpawnExistingUniqueItemAt(
                PhysicalItemIds.ForEquipmentModule(),
                (ItemInstanceId)module.instanceId,
                facility.centerPos,
                WorldItemStackState.FacilityBuffer,
                destinationId,
                out stackId);
    }

    private bool PersistModulePhysicalState(EquipmentModuleInstance module)
    {
        if (module != null && !string.IsNullOrWhiteSpace(module.sourceStackId))
        {
            return physicalItems.TrySetInstanceComponent(
                module.sourceStackId,
                EquipmentModuleItemStateCodec.Encode(module));
        }
        return false;
    }

    private static void RestoreModuleState(
        EquipmentModuleInstance target,
        EquipmentModuleInstance source)
    {
        target.definitionId = source.definitionId;
        target.grade = source.grade;
        target.condition = source.condition;
        target.identified = source.identified;
        target.runeTuned = source.runeTuned;
        target.state = source.state;
        target.sourceStackId = source.sourceStackId;
        target.attachedEquipmentInstanceId =
            source.attachedEquipmentInstanceId;
        target.nextAppraisalOperationSequence =
            source.nextAppraisalOperationSequence;
        target.pendingAppraisal = source.pendingAppraisal?.Clone()
            ?? new EquipmentModuleAppraisalCommitSaveData();
    }

}

public sealed class EquipmentModuleAppraisalRestoreRecovery :
    IDungeonSaveRestoreCompletedHook
{
    private readonly IEquipmentModuleAppraisalRecovery recovery;

    public EquipmentModuleAppraisalRestoreRecovery(
        IEquipmentModuleAppraisalRecovery recovery)
    {
        this.recovery = recovery
            ?? throw new ArgumentNullException(nameof(recovery));
    }

    public void OnRestoreCompleted()
    {
        if (!recovery.TryRecoverPendingAppraisals(out DomainFailure failure))
        {
            throw new InvalidOperationException(
                "Equipment-module appraisal recovery failed: " + failure);
        }
    }
}
