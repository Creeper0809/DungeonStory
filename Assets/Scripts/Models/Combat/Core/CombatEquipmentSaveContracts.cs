using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonCombatEquipmentSaveData
{
    public int nextCraftSequence;
    public List<CharacterCombatLoadoutState> loadouts = new List<CharacterCombatLoadoutState>();
    public List<CombatEquipmentCraftOrderSaveData> craftOrders =
        new List<CombatEquipmentCraftOrderSaveData>();
    public List<CombatEquipmentCraftMaterialPolicySaveData> craftMaterialPolicies =
        new List<CombatEquipmentCraftMaterialPolicySaveData>();
    public List<CombatEquipmentCraftTerminalEffectSaveData> craftTerminalEffects =
        new List<CombatEquipmentCraftTerminalEffectSaveData>();
    public List<EquipmentHistoryTransferOrder> historyTransferOrders =
        new List<EquipmentHistoryTransferOrder>();
    public List<string> claimedLineageSealRegionIds = new List<string>();
}

public enum CombatEquipmentCraftTerminalEffectPhase
{
    WipPreparedAwaitingInputDispositionAcknowledgement = 0,
    InputDispositionAcknowledgedAwaitingDestinationClose = 1,
    DestinationClosedAwaitingSourceRemoval = 2,
    SourceRemoved = 3
}

public enum CombatEquipmentCraftOutputPhase
{
    None = 0,
    ResolvedWaitingForPublication = 1,
    PublishedAwaitingInputAcknowledgement = 2,
    RestoredOutputAwaitingInputAcknowledgement = 3,
    LegacyUniqueOutput = 4
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CombatEquipmentCraftTerminalEffectSaveData
{
    public const int CurrentSchemaVersion = 1;

    public int schemaVersion = CurrentSchemaVersion;
    public string ownerStableId = string.Empty;
    public string sourceId = string.Empty;
    public string facilityId = string.Empty;
    public string frozenSourcePayload = string.Empty;
    public string sourceFingerprint = string.Empty;
    public string inputDispositionStepOperationId = string.Empty;
    public string inputDispositionRequestFingerprint = string.Empty;
    public string inputDispositionCommitId = string.Empty;
    public string inputDispositionReceiptFingerprint = string.Empty;
    public int releasedInputQuantity;
    public long releasedInputMassGrams;
    public string wipLossCommitId = string.Empty;
    public string wipLossReceiptFingerprint = string.Empty;
    public int wipInputQuantity;
    public long wipInputMassGrams;
    public long committedOutputMassGrams;
    public long declaredLossMassGrams;
    public int terminalReason;
    public int lossKind;
    public string sourceRemovalCommitId = string.Empty;
    public string sourceRemovalReceiptFingerprint = string.Empty;
    public CombatEquipmentCraftTerminalEffectPhase phase;

    public CombatEquipmentCraftTerminalEffectSaveData Clone() => new()
    {
        schemaVersion = schemaVersion,
        ownerStableId = ownerStableId ?? string.Empty,
        sourceId = sourceId ?? string.Empty,
        facilityId = facilityId ?? string.Empty,
        frozenSourcePayload = frozenSourcePayload ?? string.Empty,
        sourceFingerprint = sourceFingerprint ?? string.Empty,
        inputDispositionStepOperationId =
            inputDispositionStepOperationId ?? string.Empty,
        inputDispositionRequestFingerprint =
            inputDispositionRequestFingerprint ?? string.Empty,
        inputDispositionCommitId = inputDispositionCommitId ?? string.Empty,
        inputDispositionReceiptFingerprint =
            inputDispositionReceiptFingerprint ?? string.Empty,
        releasedInputQuantity = releasedInputQuantity,
        releasedInputMassGrams = releasedInputMassGrams,
        wipLossCommitId = wipLossCommitId ?? string.Empty,
        wipLossReceiptFingerprint = wipLossReceiptFingerprint ?? string.Empty,
        wipInputQuantity = wipInputQuantity,
        wipInputMassGrams = wipInputMassGrams,
        committedOutputMassGrams = committedOutputMassGrams,
        declaredLossMassGrams = declaredLossMassGrams,
        terminalReason = terminalReason,
        lossKind = lossKind,
        sourceRemovalCommitId = sourceRemovalCommitId ?? string.Empty,
        sourceRemovalReceiptFingerprint =
            sourceRemovalReceiptFingerprint ?? string.Empty,
        phase = phase
    };
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CombatEquipmentCraftMaterialPolicySaveData
{
    public string facilityKey = string.Empty;
    public string definitionId = string.Empty;
    public List<string> priorityMaterialIds = new List<string>();
    public List<string> allowedMaterialIds = new List<string>();

    public CombatEquipmentCraftMaterialPolicySaveData Clone()
    {
        return new CombatEquipmentCraftMaterialPolicySaveData
        {
            facilityKey = facilityKey ?? string.Empty,
            definitionId = definitionId ?? string.Empty,
            priorityMaterialIds = priorityMaterialIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>(),
            allowedMaterialIds = allowedMaterialIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>()
        };
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CombatCraftRecoveryOutputSaveData
{
    public string itemId = string.Empty;
    public int amount;

    public CombatCraftRecoveryOutputSaveData Clone() => new()
    {
        itemId = itemId ?? string.Empty,
        amount = Mathf.Max(0, amount)
    };
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CombatEquipmentCraftMaterialTransferInput
{
    public string itemId = string.Empty;
    public string sourceStackId = string.Empty;
    public int quantity;

    public CombatEquipmentCraftMaterialTransferInput Clone() => new()
    {
        itemId = itemId ?? string.Empty,
        sourceStackId = sourceStackId ?? string.Empty,
        quantity = Mathf.Max(0, quantity)
    };
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CombatEquipmentCraftOrderSaveData
{
    public string orderId = string.Empty;
    public string definitionId = string.Empty;
    public string materialId = string.Empty;
    public float requiredWork;
    public float completedWork;
    public bool materialsReady;
    public string materialDestinationId = string.Empty;
    public long materialBufferCapacityGrams;
    public long materialMassAuthorityRevision;
    public string materialCapacityFingerprint = string.Empty;
    public string facilityPersistentId = string.Empty;
    public int destinationX;
    public int destinationY;
    public WorkerSelectionPolicySaveData workerPolicy =
        WorkerSelectionPolicySaveData.Anyone(
            WorkerCandidateSortMode.BestExpectedQuality);
    public List<CraftContributionSaveData> contributions = new();
    public CraftQualityRollSaveData qualityRoll;
    public CraftsmanshipQualityTier minimumQuality =
        CraftsmanshipQualityTier.Awful;
    public float facilityQualityBonus;
    public RejectedOutputDisposition rejectedDisposition =
        RejectedOutputDisposition.AutoDismantle;
    public QualityRepeatLimitMode repeatLimitMode =
        QualityRepeatLimitMode.SafeLimits;
    public int maximumAttempts = 10;
    public float workBudget;
    public float consumedWork;
    public int qualityAttemptIndex;
    public int requiredAcceptedCount = 1;
    public int acceptedCount;
    public QualityTargetPipelineStage qualityStage =
        QualityTargetPipelineStage.WaitingForMaterials;
    public bool dismantlingRejectedOutput;
    public bool rejectedOutputConsumed;
    public string rejectedInstanceId = string.Empty;
    public string rejectedStackId = string.Empty;
    public float craftWorkPerAttempt;
    public List<CombatCraftRecoveryOutputSaveData> recoveryOutputs = new();
    public List<int> spawnedRecoveryAmounts = new();
    public bool rejectedRecoveryFactorsCaptured;
    public bool rejectedRecoveryProjected;
    public float rejectedRecoveryWorkerSkill;
    public float rejectedRecoverySalvageMultiplier;
    public long rejectedRecoveryDesiredMassGrams;
    public long rejectedRecoveryOutputMassGrams;
    public string rejectedRecoverySourceDigest = string.Empty;
    public string rejectedDismantleOperationId = string.Empty;
    public string rejectedDismantleCommitId = string.Empty;
    public string rejectedDismantleRequestFingerprint = string.Empty;
    public long rejectedDismantleInputMassGrams;
    public bool rejectedRecoveryPublished;
    public bool rejectedDismantleAcknowledged;
    public string materialTransferOperationId = string.Empty;
    public string materialTransferCommitId = string.Empty;
    public string materialTransferRequestFingerprint = string.Empty;
    public long materialTransferMassGrams;
    public List<CombatEquipmentCraftMaterialTransferInput>
        materialTransferInputs = new();
    public bool materialTransferAcknowledged;
    public bool attemptOutcomeResolved;
    public CombatEquipmentQuality resolvedQuality =
        CombatEquipmentQuality.Normal;
    public MythicProvenanceSaveData resolvedMythicProvenance;
    public string resolvedMakerCharacterId = string.Empty;
    public bool resolvedHadInspiration;
    public bool completionEffectsPublished;
    public bool outputPublished;
    public string outputOperationId = string.Empty;
    public string outputItemId = string.Empty;
    public int outputQuantity;
    public ProductionOutputCapabilitySaveData outputCapability = new();
    public CombatEquipmentCraftOutputPhase outputPhase;
    public ProductionDomainOutputPublicationSaveData outputPublication = new();
    public bool outputMarketRouted;
    public ItemInstanceComponentSaveData outputPreparedComponent;
    public string outputCommitId = string.Empty;
    public string outputInstanceId = string.Empty;
    public string outputStackId = string.Empty;

    public float RemainingWork => Mathf.Max(0f, requiredWork - completedWork);

    public CombatEquipmentCraftOrderSaveData Clone()
    {
        return new CombatEquipmentCraftOrderSaveData
        {
            orderId = orderId ?? string.Empty,
            definitionId = definitionId ?? string.Empty,
            materialId = materialId ?? string.Empty,
            requiredWork = Mathf.Max(0.1f, requiredWork),
            completedWork = Mathf.Clamp(completedWork, 0f, Mathf.Max(0.1f, requiredWork)),
            materialsReady = materialsReady,
            materialDestinationId = materialDestinationId ?? string.Empty,
            materialBufferCapacityGrams = Math.Max(
                0L,
                materialBufferCapacityGrams),
            materialMassAuthorityRevision = Math.Max(
                0L,
                materialMassAuthorityRevision),
            materialCapacityFingerprint =
                materialCapacityFingerprint ?? string.Empty,
            facilityPersistentId = facilityPersistentId ?? string.Empty,
            destinationX = destinationX,
            destinationY = destinationY,
            workerPolicy = workerPolicy?.CloneNormalized()
                ?? WorkerSelectionPolicySaveData.Anyone(
                    WorkerCandidateSortMode.BestExpectedQuality),
            contributions = contributions?
                .Where(value => value != null)
                .Select(value => value.Clone())
                .ToList() ?? new List<CraftContributionSaveData>(),
            qualityRoll = qualityRoll?.Clone(),
            minimumQuality = minimumQuality,
            facilityQualityBonus = facilityQualityBonus,
            rejectedDisposition = rejectedDisposition,
            repeatLimitMode = repeatLimitMode,
            maximumAttempts = Mathf.Max(1, maximumAttempts),
            workBudget = Mathf.Max(0f, workBudget),
            consumedWork = Mathf.Max(0f, consumedWork),
            qualityAttemptIndex = Mathf.Max(0, qualityAttemptIndex),
            requiredAcceptedCount = Mathf.Max(1, requiredAcceptedCount),
            acceptedCount = Mathf.Max(0, acceptedCount),
            qualityStage = qualityStage,
            dismantlingRejectedOutput = dismantlingRejectedOutput,
            rejectedOutputConsumed = rejectedOutputConsumed,
            rejectedInstanceId = rejectedInstanceId ?? string.Empty,
            rejectedStackId = rejectedStackId ?? string.Empty,
            craftWorkPerAttempt = Mathf.Max(0f, craftWorkPerAttempt),
            recoveryOutputs = recoveryOutputs?
                .Where(value => value != null && value.amount > 0)
                .Select(value => value.Clone())
                .ToList() ?? new List<CombatCraftRecoveryOutputSaveData>(),
            spawnedRecoveryAmounts = spawnedRecoveryAmounts?.
                Select(value => Mathf.Max(0, value)).ToList()
                ?? new List<int>(),
            rejectedRecoveryFactorsCaptured =
                rejectedRecoveryFactorsCaptured,
            rejectedRecoveryProjected = rejectedRecoveryProjected,
            rejectedRecoveryWorkerSkill = rejectedRecoveryWorkerSkill,
            rejectedRecoverySalvageMultiplier =
                rejectedRecoverySalvageMultiplier,
            rejectedRecoveryDesiredMassGrams =
                Math.Max(0L, rejectedRecoveryDesiredMassGrams),
            rejectedRecoveryOutputMassGrams =
                Math.Max(0L, rejectedRecoveryOutputMassGrams),
            rejectedRecoverySourceDigest =
                rejectedRecoverySourceDigest ?? string.Empty,
            rejectedDismantleOperationId =
                rejectedDismantleOperationId ?? string.Empty,
            rejectedDismantleCommitId =
                rejectedDismantleCommitId ?? string.Empty,
            rejectedDismantleRequestFingerprint =
                rejectedDismantleRequestFingerprint ?? string.Empty,
            rejectedDismantleInputMassGrams =
                Math.Max(0L, rejectedDismantleInputMassGrams),
            rejectedRecoveryPublished = rejectedRecoveryPublished,
            rejectedDismantleAcknowledged =
                rejectedDismantleAcknowledged,
            materialTransferOperationId =
                materialTransferOperationId ?? string.Empty,
            materialTransferCommitId = materialTransferCommitId ?? string.Empty,
            materialTransferRequestFingerprint =
                materialTransferRequestFingerprint ?? string.Empty,
            materialTransferMassGrams = Math.Max(0L, materialTransferMassGrams),
            materialTransferInputs = materialTransferInputs?
                .Where(value => value != null)
                .Select(value => value.Clone())
                .ToList()
                ?? new List<CombatEquipmentCraftMaterialTransferInput>(),
            materialTransferAcknowledged = materialTransferAcknowledged,
            attemptOutcomeResolved = attemptOutcomeResolved,
            resolvedQuality = resolvedQuality,
            resolvedMythicProvenance = resolvedMythicProvenance?.Clone(),
            resolvedMakerCharacterId = resolvedMakerCharacterId ?? string.Empty,
            resolvedHadInspiration = resolvedHadInspiration,
            completionEffectsPublished = completionEffectsPublished,
            outputPublished = outputPublished,
            outputOperationId = outputOperationId ?? string.Empty,
            outputItemId = outputItemId ?? string.Empty,
            outputQuantity = Mathf.Max(0, outputQuantity),
            outputCapability = outputCapability?.Clone()
                ?? new ProductionOutputCapabilitySaveData(),
            outputPhase = outputPhase,
            outputPublication = outputPublication?.Clone()
                ?? new ProductionDomainOutputPublicationSaveData(),
            outputMarketRouted = outputMarketRouted,
            outputPreparedComponent = outputPreparedComponent?.Clone(),
            outputCommitId = outputCommitId ?? string.Empty,
            outputInstanceId = outputInstanceId ?? string.Empty,
            outputStackId = outputStackId ?? string.Empty
        };
    }
}
