using System;

public static class EquipmentModuleSelectedFactMapping
{
    public const string ObserverId = "log:equipment.module-selected";

    public static GameplayResultKey ResultKey(
        EquipmentModuleSelectedFactData data)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        data.Validate();
        return new GameplayResultKey(
            EquipmentModuleSelectionOutcomeIds.ProducerId,
            new GameplayOperationId(data.operationId),
            data.afterRevision,
            0);
    }

    public static EquipmentModuleSelectionOutcomeReceipt Capture(
        EquipmentModuleSelectedFact fact)
    {
        EquipmentModuleSelectedFactData data = fact.Snapshot();
        data.Validate();
        return new EquipmentModuleSelectionOutcomeReceipt(
            data.operationId,
            data.selectionId,
            data.beforeRevision,
            data.afterRevision,
            data.nodeId,
            data.nodeDisplayName,
            data.equipmentInstanceId,
            data.equipmentDefinitionId,
            data.currentOwnerId,
            data.attunementOwnerId,
            data.reforgeOrderId,
            data.historical,
            data.generationBefore,
            data.generationAfter,
            data.masteryBefore,
            data.masteryAfter,
            data.positiveModuleIds,
            data.drawbackModuleIds,
            data.selectedModuleIds,
            data.capabilityIds,
            data.effectIds,
            data.evidenceFactIds,
            data.absoluteDay);
    }

    public static GameplayDomainFactObserverBinding<
        EquipmentModuleSelectedFact,
        EquipmentModuleSelectionOutcomeReceipt> CreateBinding(
            IGameplayOutcomeRegistry registry,
            GameplayOutcomeLedger ledger,
            IGameplayOutcomeRecorder recorder,
            GameplayDomainFactCommitGroup commits) => new(
                ObserverId,
                Capture,
                registry,
                ledger,
                recorder,
                commits);
}
