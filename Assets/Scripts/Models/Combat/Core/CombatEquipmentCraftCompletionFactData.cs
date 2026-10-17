using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>Raw, frozen completion data owned by the crafting attempt, not a log payload.</summary>
[Serializable]
public sealed class CombatEquipmentCraftCompletionFactData
{
    public string orderId = string.Empty;
    public int qualityAttemptIndex;
    public string facilityPersistentId = string.Empty;
    public string definitionId = string.Empty;
    public string materialId = string.Empty;
    public float completedWork;
    public CombatEquipmentQuality resolvedQuality;
    public CraftsmanshipQualityTier minimumQuality;
    public RejectedOutputDisposition rejectedDisposition;
    public string resolvedMakerCharacterId = string.Empty;
    public string resolvedMakerDisplayName = string.Empty;
    public int resolvedAbsoluteDay;
    public string materialTransferCommitId = string.Empty;
    public long materialTransferMassGrams;
    public List<CombatEquipmentCraftMaterialTransferInput> materialTransferInputs = new();
    public List<CraftContributionSaveData> contributions = new();
    public List<CombatCraftCommandReceiptSaveData> commandReceipts = new();
    public string qualityOutcomeResultKey = string.Empty;
    public string outputCommitId = string.Empty;
    public int outputQuantity;
    public long outputMassGrams;
    public long ownerRevision;
    public string outcomeFingerprint = string.Empty;
    public List<ProductionDomainPublishedStackSaveData> outputs = new();

    public bool IsEmpty => orderId == string.Empty && qualityAttemptIndex == 0
        && facilityPersistentId == string.Empty && definitionId == string.Empty && materialId == string.Empty
        && completedWork == 0 && resolvedQuality == default && minimumQuality == default && rejectedDisposition == default
        && resolvedMakerCharacterId == string.Empty && resolvedMakerDisplayName == string.Empty && resolvedAbsoluteDay == 0
        && materialTransferCommitId == string.Empty && materialTransferMassGrams == 0
        && materialTransferInputs?.Count == 0 && contributions?.Count == 0 && commandReceipts?.Count == 0
        && qualityOutcomeResultKey == string.Empty && outputCommitId == string.Empty && outputQuantity == 0
        && outputMassGrams == 0 && ownerRevision == 0 && outcomeFingerprint == string.Empty && outputs?.Count == 0;

    public CombatEquipmentCraftCompletionFactData Clone()
    {
        var copy = (CombatEquipmentCraftCompletionFactData)MemberwiseClone();
        // Preserve invalid data for rejection. No DTO normalization may repair a saved fact.
        copy.materialTransferInputs = materialTransferInputs?.Select(v => v == null ? null : new CombatEquipmentCraftMaterialTransferInput
            { itemId = v.itemId, sourceStackId = v.sourceStackId, quantity = v.quantity }).ToList();
        copy.contributions = contributions?.Select(v => v == null ? null : new CraftContributionSaveData
            { characterId = v.characterId, contributedWork = v.contributedWork, relevantSkill = v.relevantSkill }).ToList();
        copy.commandReceipts = commandReceipts?.Select(v => v == null ? null : new CombatCraftCommandReceiptSaveData
            { commandKind = v.commandKind, revision = v.revision, beforeValue = v.beforeValue,
                afterValue = v.afterValue, absoluteDay = v.absoluteDay }).ToList();
        copy.outputs = outputs?.Select(v => v == null ? null : new ProductionDomainPublishedStackSaveData
            { outputLineId = v.outputLineId, itemId = v.itemId, itemInstanceId = v.itemInstanceId,
                stackId = v.stackId, quantity = v.quantity, massGrams = v.massGrams, displayText = v.displayText }).ToList();
        return copy;
    }
}
