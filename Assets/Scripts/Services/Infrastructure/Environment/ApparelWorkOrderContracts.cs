using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public enum ApparelWorkOrderKind
{
    Craft = 0,
    Laundry = 1,
    Drying = 2,
    Repair = 3,
    Alteration = 4
}

public enum ApparelWorkOrderState
{
    NeedsRevalidation = 0,
    WaitingForMaterials = 1,
    Ready = 2,
    InProgress = 3,
    Completed = 4,
    Failed = 5,
    WaitingForOutputSpace = 6,
    WaitingForEligibleWorker = 7,
    TargetCurrentlyUnreachable = 8,
    WaitingForDispositionFinalization = 9
}

public enum ApparelRepairCommitPhase
{
    None = 0,
    MaterialCommitted = 1,
    RepairApplied = 2
}

[Serializable]
public sealed class ApparelWorkOrderOutcomeItemStateSaveData
{
    public string itemInstanceId = string.Empty;
    public string stackId = string.Empty;
    public string apparelDisplayName = string.Empty;
    public string beforeStatePayload = string.Empty;
    public string afterStatePayload = string.Empty;
}

[Serializable]
public sealed class ApparelWorkOrderSaveData
{
    public string orderId = string.Empty;
    public ApparelWorkOrderKind kind;
    public ApparelWorkOrderState state;
    public string apparelDefinitionId = string.Empty;
    public string materialDefinitionId = string.Empty;
    public ApparelMaterialSelectionPolicy materialPolicy;
    public CraftsmanshipQualityTier minimumCraftsmanshipQuality;
    public WorkerSelectionPolicySaveData workerPolicy =
        WorkerSelectionPolicySaveData.Anyone(
            WorkerCandidateSortMode.BestExpectedQuality);
    public List<CraftContributionSaveData> contributions = new();
    public string lastWorkerCharacterId = string.Empty;
    public CraftQualityRollSaveData qualityRoll;
    public int qualityAttemptIndex;
    public RejectedOutputDisposition rejectedDisposition =
        RejectedOutputDisposition.AutoDismantle;
    public QualityRepeatLimitMode repeatLimitMode =
        QualityRepeatLimitMode.SafeLimits;
    public int maximumAttempts = 10;
    public float workBudget;
    public float consumedWork;
    public int requiredAcceptedCount = 1;
    public int acceptedCount;
    public bool dismantlingRejectedOutput;
    public bool rejectedOutputConsumed;
    public string rejectedOutputStackId = string.Empty;
    public string rejectedOutputInstanceId = string.Empty;
    public float craftWorkPerAttempt;
    public int rejectedMaterialAmount;
    public int rejectedMaterialSpawned;
    public string rejectedRecoveryItemId = string.Empty;
    public string rejectedDismantleOperationId = string.Empty;
    public string rejectedDismantleCommitId = string.Empty;
    public string rejectedDismantleRequestFingerprint = string.Empty;
    public long rejectedDismantleInputMassGrams;
    public string rejectedRecoveryOperationId = string.Empty;
    public string rejectedRecoveryCommitId = string.Empty;
    public long rejectedRecoveryOutputMassGrams;
    public int rejectedRecoveryPublicationAttempt;
    public string rejectedRecoveryOutcomeFingerprint = string.Empty;
    public string rejectedRecoveryAdmissionTokenId = string.Empty;
    public ProductionOutputCapabilitySaveData rejectedRecoveryOutputCapability = new();
    public string rejectedRecoveryMaximumMassProofDigest = string.Empty;
    public long rejectedRecoveryMaximumBatchMassGrams;
    public string rejectedRecoveryCapacitySourceDigest = string.Empty;
    public long rejectedRecoveryRequiredMinimumCapacityGrams;
    public string rejectedRecoveryPlannedOutputFingerprint = string.Empty;
    public List<string> rejectedRecoveryStackIds = new();
    public bool rejectedRecoveryPublished;
    public bool rejectedRecoveryAdmissionCommitted;
    public bool rejectedRecoveryOutputAcknowledged;
    public bool rejectedDismantleAcknowledged;
    public int craftPublicationAttempt;
    public string craftPublicationOperationId = string.Empty;
    public string craftOutputBatchCommitId = string.Empty;
    public string craftOutcomeFingerprint = string.Empty;
    public string craftOutputComponentFingerprint = string.Empty;
    public int craftQualityOutcomeSchemaVersion;
    public string craftMakerCharacterId = string.Empty;
    public string craftMakerDisplayName = string.Empty;
    public CraftsmanshipQualityTier craftResolvedQuality =
        CraftsmanshipQualityTier.Normal;
    public bool craftRejectedBelowMinimum;
    public ProductionOutputCapabilitySaveData craftOutputCapability = new();
    public string craftAdmissionTokenId = string.Empty;
    public string craftMaximumMassProofDigest = string.Empty;
    public long craftMaximumBatchMassGrams;
    public string craftCapacitySourceDigest = string.Empty;
    public long craftRequiredMinimumCapacityGrams;
    public string craftPlannedOutputFingerprint = string.Empty;
    public string craftOutputStackId = string.Empty;
    public string craftOutputInstanceId = string.Empty;
    public string craftInputCommitId = string.Empty;
    public string craftInputRequestFingerprint = string.Empty;
    public long craftInputMassGrams;
    public long craftOutputMassGrams;
    public bool craftInputPending;
    public bool craftOutputPublished;
    public bool craftAdmissionCommitted;
    public bool craftInputAcknowledged;
    public bool craftOutputAcknowledged;
    public bool craftMarketRouted;
    public string rejectedOutputLeaseId = string.Empty;
    public ApparelSizeClass targetSize;
    public ApparelModificationKind targetModifications;
    public string facilityInstanceId = string.Empty;
    public string targetItemInstanceId = string.Empty;
    public List<string> targetItemInstanceIds = new();
    public List<string> materialStackIds = new();
    public List<int> materialStackAmounts = new();
    public float requiredWork;
    public float completedWork;
    public int retryCount;
    public float nextRetryGameHour;
    public bool powered;
    public bool shortWardrobeOperation;
    public ApparelRepairCommitPhase repairCommitPhase;
    public string repairOperationId = string.Empty;
    public string repairReasonCode = string.Empty;
    public string repairCommitId = string.Empty;
    public List<string> repairSourceStackIds = new();
    public int repairInputQuantity;
    public long repairInputMassGrams;
    public string repairTargetStackId = string.Empty;
    public string repairOriginalStatePayload = string.Empty;
    public string repairResolvedStatePayload = string.Empty;
    public bool terminalOutcomeReady;
    public int terminalAbsoluteDay;
    public string terminalPublicationKey = string.Empty;
    public bool terminalPublicationConfirmed;
    public string terminalMaterialOperationId = string.Empty;
    public string terminalMaterialCommitId = string.Empty;
    public List<string> terminalMaterialSourceStackIds = new();
    public int terminalMaterialQuantity;
    public long terminalMaterialMassGrams;
    public List<ApparelWorkOrderOutcomeItemStateSaveData>
        terminalOutcomeItemStates = new();
}

[Serializable]
public sealed class ApparelWorkOrderTerminalStateSaveData
{
    public const int CurrentSchemaVersion = 3;

    public int schemaVersion = CurrentSchemaVersion;
    public ApparelWorkOrderSaveData sourceOrder;
    public string sourceOrderFingerprint = string.Empty;
    public ProductionApparelOrderPendingEffectIdentity pendingEffect;
    public ProductionApparelOrderTerminalEffectReceipt terminalEffectReceipt;
    public ProductionApparelOrderSourceTerminalReceipt sourceTerminalReceipt;

    public ApparelWorkOrderTerminalStateSaveData Clone() => new()
    {
        schemaVersion = schemaVersion,
        sourceOrder = ProductionApparelOrderTerminalDrainCanonical.CloneOrder(
            sourceOrder),
        sourceOrderFingerprint = sourceOrderFingerprint ?? string.Empty,
        pendingEffect = ProductionApparelOrderTerminalDrainCanonical
            .CloneOptionalPendingEffect(pendingEffect),
        terminalEffectReceipt = ProductionApparelOrderTerminalDrainCanonical
            .CloneOptionalTerminalEffectReceipt(terminalEffectReceipt),
        sourceTerminalReceipt = ProductionApparelOrderTerminalDrainCanonical
            .CloneOptionalSourceTerminalReceipt(sourceTerminalReceipt)
    };
}

public sealed class ApparelWorkOrderRestoreCandidate
{
    internal ApparelWorkOrderRestoreCandidate(
        IReadOnlyList<ApparelWorkOrderSaveData> orders,
        IReadOnlyList<ApparelWorkOrderTerminalStateSaveData> terminalStates)
    {
        Orders = orders ?? throw new ArgumentNullException(nameof(orders));
        TerminalStates = terminalStates
            ?? throw new ArgumentNullException(nameof(terminalStates));
    }

    internal IReadOnlyList<ApparelWorkOrderSaveData> Orders { get; }
    internal IReadOnlyList<ApparelWorkOrderTerminalStateSaveData>
        TerminalStates { get; }
}

public readonly struct ApparelCraftOrderRequest
{
    public ApparelCraftOrderRequest(
        string apparelDefinitionId,
        ApparelSizeClass size,
        ApparelModificationKind modifications,
        ApparelMaterialSelectionPolicy materialPolicy,
        string exactMaterialDefinitionId = "",
        CraftsmanshipQualityTier minimumCraftsmanshipQuality =
            CraftsmanshipQualityTier.Normal,
        WorkerSelectionPolicySaveData workerPolicy = null,
        RejectedOutputDisposition rejectedDisposition =
            RejectedOutputDisposition.AutoDismantle,
        QualityRepeatLimitMode repeatLimitMode =
            QualityRepeatLimitMode.SafeLimits,
        int maximumAttempts = 10,
        float workBudget = 0f,
        int requiredAcceptedCount = 1)
    {
        ApparelDefinitionId = apparelDefinitionId?.Trim() ?? string.Empty;
        Size = size;
        Modifications = modifications;
        MaterialPolicy = materialPolicy;
        ExactMaterialDefinitionId = exactMaterialDefinitionId?.Trim() ?? string.Empty;
        MinimumCraftsmanshipQuality = minimumCraftsmanshipQuality;
        WorkerPolicy = workerPolicy?.CloneNormalized()
            ?? WorkerSelectionPolicySaveData.Anyone(
                WorkerCandidateSortMode.BestExpectedQuality);
        RejectedDisposition = rejectedDisposition;
        RepeatLimitMode = repeatLimitMode;
        MaximumAttempts = Mathf.Max(1, maximumAttempts);
        WorkBudget = Mathf.Max(0f, workBudget);
        RequiredAcceptedCount = Mathf.Max(1, requiredAcceptedCount);
    }

    public string ApparelDefinitionId { get; }
    public ApparelSizeClass Size { get; }
    public ApparelModificationKind Modifications { get; }
    public ApparelMaterialSelectionPolicy MaterialPolicy { get; }
    public string ExactMaterialDefinitionId { get; }
    public CraftsmanshipQualityTier MinimumCraftsmanshipQuality { get; }
    public WorkerSelectionPolicySaveData WorkerPolicy { get; }
    public RejectedOutputDisposition RejectedDisposition { get; }
    public QualityRepeatLimitMode RepeatLimitMode { get; }
    public int MaximumAttempts { get; }
    public float WorkBudget { get; }
    public int RequiredAcceptedCount { get; }
}

public interface IApparelWorkOrderCommand
{
    bool CreateCraft(
        ApparelCraftOrderRequest request,
        out string orderId,
        out DomainFailure failure);
    bool CreateLaundry(
        IReadOnlyList<ItemInstanceId> items,
        bool powered,
        out string orderId,
        out DomainFailure failure);
    bool CreateDrying(
        IReadOnlyList<ItemInstanceId> items,
        out string orderId,
        out DomainFailure failure);
    bool CreateRepair(
        ItemInstanceId item,
        out string orderId,
        out DomainFailure failure);
    bool CreateAlteration(
        ItemInstanceId item,
        ApparelSizeClass size,
        ApparelModificationKind modifications,
        bool shortWardrobeOperation,
        out string orderId,
        out DomainFailure failure);
    bool ApplyWork(string orderId, float amount, out DomainFailure failure);
    bool ApplyWork(
        string orderId,
        CharacterActor worker,
        float amount,
        out DomainFailure failure);
    bool Cancel(string orderId);
    bool Cancel(string orderId, out DomainFailure failure);
}

public interface IApparelWorkOrderQuery
{
    int Version { get; }
    IReadOnlyList<ApparelWorkOrderSaveData> Orders { get; }
    CraftQualityAttemptEstimate CaptureQualityEstimate(string orderId);
}

public interface IApparelWorkOrderPersistence
{
    ApparelWorkOrderSaveData[] CaptureOrders();
    ApparelWorkOrderTerminalStateSaveData[] CaptureTerminalStates();
    ApparelWorkOrderRestoreCandidate PrepareRestoreState(
        IEnumerable<ApparelWorkOrderSaveData> orders,
        IEnumerable<ApparelWorkOrderTerminalStateSaveData> terminalStates);
    void PublishRestoreState(ApparelWorkOrderRestoreCandidate candidate);
    IReadOnlyList<ApparelWorkOrderSaveData> PrepareRestoreOrders(
        IEnumerable<ApparelWorkOrderSaveData> source);
    void PublishRestoreOrders(IEnumerable<ApparelWorkOrderSaveData> source);
    void ResetOrders();
}

/// <summary>Canonical deep copies and identity equality shared by both order owners.</summary>
internal static class ApparelWorkOrderSnapshots
{
    internal static ApparelWorkOrderSaveData Clone(ApparelWorkOrderSaveData value) => new()
    {
        orderId = value?.orderId?.Trim() ?? string.Empty,
        kind = value?.kind ?? ApparelWorkOrderKind.Craft,
        state = value?.state ?? ApparelWorkOrderState.NeedsRevalidation,
        apparelDefinitionId = value?.apparelDefinitionId?.Trim() ?? string.Empty,
        materialDefinitionId = value?.materialDefinitionId?.Trim() ?? string.Empty,
        materialPolicy = value?.materialPolicy ?? ApparelMaterialSelectionPolicy.ExactMaterial,
        minimumCraftsmanshipQuality = value?.minimumCraftsmanshipQuality
            ?? CraftsmanshipQualityTier.Normal,
        workerPolicy = value?.workerPolicy?.CloneNormalized()
            ?? WorkerSelectionPolicySaveData.Anyone(
                WorkerCandidateSortMode.BestExpectedQuality),
        contributions = value?.contributions?
            .Where(contribution => contribution != null)
            .Select(contribution => contribution.Clone())
            .ToList() ?? new List<CraftContributionSaveData>(),
        lastWorkerCharacterId = value?.lastWorkerCharacterId?.Trim() ?? string.Empty,
        qualityRoll = value?.qualityRoll == null ? null : new CraftQualityRollSaveData
        {
            attemptIndex = value.qualityRoll.attemptIndex,
            randomA = value.qualityRoll.randomA,
            randomB = value.qualityRoll.randomB,
            randomC = value.qualityRoll.randomC
        },
        qualityAttemptIndex = Mathf.Max(0, value?.qualityAttemptIndex ?? 0),
        rejectedDisposition = value?.rejectedDisposition
            ?? RejectedOutputDisposition.AutoDismantle,
        repeatLimitMode = value?.repeatLimitMode
            ?? QualityRepeatLimitMode.SafeLimits,
        maximumAttempts = Mathf.Max(1, value?.maximumAttempts ?? 10),
        workBudget = Mathf.Max(0f, value?.workBudget ?? 0f),
        consumedWork = Mathf.Max(0f, value?.consumedWork ?? 0f),
        requiredAcceptedCount = Mathf.Max(
            1,
            value?.requiredAcceptedCount ?? 1),
        acceptedCount = Mathf.Max(0, value?.acceptedCount ?? 0),
        dismantlingRejectedOutput =
            value?.dismantlingRejectedOutput ?? false,
        rejectedOutputConsumed =
            value?.rejectedOutputConsumed ?? false,
        rejectedOutputStackId =
            value?.rejectedOutputStackId?.Trim() ?? string.Empty,
        rejectedOutputInstanceId =
            value?.rejectedOutputInstanceId?.Trim() ?? string.Empty,
        craftWorkPerAttempt = Mathf.Max(
            0f,
            value?.craftWorkPerAttempt ?? 0f),
        rejectedMaterialAmount = Mathf.Max(
            0,
            value?.rejectedMaterialAmount ?? 0),
        rejectedMaterialSpawned = Mathf.Clamp(
            value?.rejectedMaterialSpawned ?? 0,
            0,
            Mathf.Max(0, value?.rejectedMaterialAmount ?? 0)),
        rejectedRecoveryItemId =
            value?.rejectedRecoveryItemId?.Trim() ?? string.Empty,
        rejectedDismantleOperationId =
            value?.rejectedDismantleOperationId?.Trim() ?? string.Empty,
        rejectedDismantleCommitId =
            value?.rejectedDismantleCommitId?.Trim() ?? string.Empty,
        rejectedDismantleRequestFingerprint =
            value?.rejectedDismantleRequestFingerprint?.Trim()
            ?? string.Empty,
        rejectedDismantleInputMassGrams = Math.Max(
            0L,
            value?.rejectedDismantleInputMassGrams ?? 0L),
        rejectedRecoveryOperationId =
            value?.rejectedRecoveryOperationId?.Trim() ?? string.Empty,
        rejectedRecoveryCommitId =
            value?.rejectedRecoveryCommitId?.Trim() ?? string.Empty,
        rejectedRecoveryOutputMassGrams = Math.Max(
            0L,
            value?.rejectedRecoveryOutputMassGrams ?? 0L),
        rejectedRecoveryPublicationAttempt = Mathf.Max(
            0,
            value?.rejectedRecoveryPublicationAttempt ?? 0),
        rejectedRecoveryOutcomeFingerprint =
            value?.rejectedRecoveryOutcomeFingerprint?.Trim() ?? string.Empty,
        rejectedRecoveryAdmissionTokenId =
            value?.rejectedRecoveryAdmissionTokenId?.Trim() ?? string.Empty,
        rejectedRecoveryOutputCapability =
            value?.rejectedRecoveryOutputCapability?.Clone()
            ?? new ProductionOutputCapabilitySaveData(),
        rejectedRecoveryMaximumMassProofDigest =
            value?.rejectedRecoveryMaximumMassProofDigest ?? string.Empty,
        rejectedRecoveryMaximumBatchMassGrams =
            value?.rejectedRecoveryMaximumBatchMassGrams ?? 0L,
        rejectedRecoveryCapacitySourceDigest =
            value?.rejectedRecoveryCapacitySourceDigest?.Trim() ?? string.Empty,
        rejectedRecoveryRequiredMinimumCapacityGrams = Math.Max(
            0L,
            value?.rejectedRecoveryRequiredMinimumCapacityGrams ?? 0L),
        rejectedRecoveryPlannedOutputFingerprint =
            value?.rejectedRecoveryPlannedOutputFingerprint?.Trim()
            ?? string.Empty,
        rejectedRecoveryStackIds = value?.rejectedRecoveryStackIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList()
            ?? new List<string>(),
        rejectedRecoveryPublished =
            value?.rejectedRecoveryPublished ?? false,
        rejectedRecoveryAdmissionCommitted =
            value?.rejectedRecoveryAdmissionCommitted ?? false,
        rejectedRecoveryOutputAcknowledged =
            value?.rejectedRecoveryOutputAcknowledged ?? false,
        rejectedDismantleAcknowledged =
            value?.rejectedDismantleAcknowledged ?? false,
        craftPublicationAttempt = Mathf.Max(
            0,
            value?.craftPublicationAttempt ?? 0),
        craftPublicationOperationId =
            value?.craftPublicationOperationId?.Trim() ?? string.Empty,
        craftOutputBatchCommitId =
            value?.craftOutputBatchCommitId?.Trim() ?? string.Empty,
        craftOutcomeFingerprint =
            value?.craftOutcomeFingerprint?.Trim() ?? string.Empty,
        craftOutputComponentFingerprint =
            value?.craftOutputComponentFingerprint?.Trim() ?? string.Empty,
        craftQualityOutcomeSchemaVersion = Mathf.Clamp(
            value?.craftQualityOutcomeSchemaVersion ?? 0,
            0,
            1),
        craftMakerCharacterId =
            value?.craftMakerCharacterId?.Trim() ?? string.Empty,
        craftMakerDisplayName =
            value?.craftMakerDisplayName?.Trim() ?? string.Empty,
        craftResolvedQuality = value?.craftResolvedQuality
            ?? CraftsmanshipQualityTier.Normal,
        craftRejectedBelowMinimum =
            value?.craftRejectedBelowMinimum ?? false,
        craftOutputCapability = value?.craftOutputCapability?.Clone()
            ?? new ProductionOutputCapabilitySaveData(),
        craftAdmissionTokenId =
            value?.craftAdmissionTokenId?.Trim() ?? string.Empty,
        craftMaximumMassProofDigest =
            value?.craftMaximumMassProofDigest ?? string.Empty,
        craftMaximumBatchMassGrams =
            value?.craftMaximumBatchMassGrams ?? 0L,
        craftCapacitySourceDigest =
            value?.craftCapacitySourceDigest?.Trim() ?? string.Empty,
        craftRequiredMinimumCapacityGrams = Math.Max(
            0L,
            value?.craftRequiredMinimumCapacityGrams ?? 0L),
        craftPlannedOutputFingerprint =
            value?.craftPlannedOutputFingerprint?.Trim() ?? string.Empty,
        craftOutputStackId =
            value?.craftOutputStackId?.Trim() ?? string.Empty,
        craftOutputInstanceId =
            value?.craftOutputInstanceId?.Trim() ?? string.Empty,
        craftInputCommitId =
            value?.craftInputCommitId?.Trim() ?? string.Empty,
        craftInputRequestFingerprint =
            value?.craftInputRequestFingerprint?.Trim() ?? string.Empty,
        craftInputMassGrams = Math.Max(
            0L,
            value?.craftInputMassGrams ?? 0L),
        craftOutputMassGrams = Math.Max(
            0L,
            value?.craftOutputMassGrams ?? 0L),
        craftInputPending = value?.craftInputPending ?? false,
        craftOutputPublished = value?.craftOutputPublished ?? false,
        craftAdmissionCommitted = value?.craftAdmissionCommitted ?? false,
        craftInputAcknowledged = value?.craftInputAcknowledged ?? false,
        craftOutputAcknowledged = value?.craftOutputAcknowledged ?? false,
        craftMarketRouted = value?.craftMarketRouted ?? false,
        rejectedOutputLeaseId =
            value?.rejectedOutputLeaseId?.Trim() ?? string.Empty,
        targetSize = value?.targetSize ?? ApparelSizeClass.Medium,
        targetModifications = value?.targetModifications ?? ApparelModificationKind.None,
        facilityInstanceId = value?.facilityInstanceId?.Trim() ?? string.Empty,
        targetItemInstanceId = value?.targetItemInstanceId?.Trim() ?? string.Empty,
        targetItemInstanceIds = value?.targetItemInstanceIds?
            .Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList()
            ?? new List<string>(),
        materialStackIds = value?.materialStackIds?
            .Where(id => !string.IsNullOrWhiteSpace(id)).Select(id => id.Trim()).ToList()
            ?? new List<string>(),
        materialStackAmounts = value?.materialStackAmounts?.ToList() ?? new List<int>(),
        requiredWork = value?.requiredWork ?? 0f,
        completedWork = value?.completedWork ?? 0f,
        retryCount = value?.retryCount ?? 0,
        nextRetryGameHour = value?.nextRetryGameHour ?? 0f,
        powered = value?.powered ?? false,
        shortWardrobeOperation = value?.shortWardrobeOperation ?? false,
        repairCommitPhase = value?.repairCommitPhase
            ?? ApparelRepairCommitPhase.None,
        repairOperationId = value?.repairOperationId?.Trim() ?? string.Empty,
        repairReasonCode = value?.repairReasonCode?.Trim() ?? string.Empty,
        repairCommitId = value?.repairCommitId?.Trim() ?? string.Empty,
        repairSourceStackIds = value?.repairSourceStackIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .ToList() ?? new List<string>(),
        repairInputQuantity = value?.repairInputQuantity ?? 0,
        repairInputMassGrams = value?.repairInputMassGrams ?? 0L,
        repairTargetStackId = value?.repairTargetStackId?.Trim() ?? string.Empty,
        repairOriginalStatePayload = value?.repairOriginalStatePayload
            ?? string.Empty,
        repairResolvedStatePayload = value?.repairResolvedStatePayload
            ?? string.Empty,
        terminalOutcomeReady = value?.terminalOutcomeReady ?? false,
        terminalAbsoluteDay = value?.terminalAbsoluteDay ?? 0,
        terminalPublicationKey = value?.terminalPublicationKey,
        terminalPublicationConfirmed = value?.terminalPublicationConfirmed ?? false,
        terminalMaterialOperationId = value?.terminalMaterialOperationId?.Trim()
            ?? string.Empty,
        terminalMaterialCommitId = value?.terminalMaterialCommitId?.Trim()
            ?? string.Empty,
        terminalMaterialSourceStackIds = value?.terminalMaterialSourceStackIds?
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Select(id => id.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToList() ?? new List<string>(),
        terminalMaterialQuantity = Math.Max(
            0,
            value?.terminalMaterialQuantity ?? 0),
        terminalMaterialMassGrams = Math.Max(
            0L,
            value?.terminalMaterialMassGrams ?? 0L),
        terminalOutcomeItemStates = value?.terminalOutcomeItemStates?
            .Select(CloneOutcomeItemState)
            .OrderBy(state => state.itemInstanceId, StringComparer.Ordinal)
            .ToList() ?? new List<ApparelWorkOrderOutcomeItemStateSaveData>()
    };

    internal static ApparelWorkOrderOutcomeItemStateSaveData CloneOutcomeItemState(
        ApparelWorkOrderOutcomeItemStateSaveData value) => ApparelWorkCompletedData.CloneItem(value);

    internal static bool PendingEffectEquals(
        ProductionApparelOrderPendingEffectIdentity left,
        ProductionApparelOrderPendingEffectIdentity right)
    {
        if (left == null || right == null)
            return left == null && right == null;
        return string.Equals(
                left.identityFingerprint,
                right.identityFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                JsonUtility.ToJson(left),
                JsonUtility.ToJson(right),
                StringComparison.Ordinal);
    }
}
