using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum WorkOrderStatus
{
    WaitingForMaterials = 0,
    Ready = 1,
    InProgress = 2,
    Blocked = 3,
    Completed = 4,
    Cancelled = 5,
    WaitingForEligibleWorker = 6,
    TargetCurrentlyUnreachable = 7,
    WaitingForOutputSpace = 8
}

public enum WorkOrderMaterialTransferPhase
{
    None = 0,
    InputCommitted = 1,
    CustodyPublished = 2,
    Acknowledged = 3,
    RestitutionPending = 4
}

[Serializable]
public sealed class WorkOrderMaterialSourceSaveData
{
    public string itemId = string.Empty;
    public string stackId = string.Empty;
    public int quantity;
}

[Serializable]
public sealed class WorkOrderMaterialTransferSaveData
{
    public WorkOrderMaterialTransferPhase phase;
    public string operationId = string.Empty;
    public string reasonCode = string.Empty;
    public string requestFingerprint = string.Empty;
    public string commitId = string.Empty;
    public int inputQuantity;
    public long inputMassGrams;
    public List<WorkOrderMaterialSourceSaveData> sources = new();
    public string restitutionOperationId = string.Empty;
}

[Serializable]
public sealed class WorkOrderItemMaterialSaveData
{
    public string itemId = string.Empty;
    public int required;
    public int delivered;
}

[Serializable]
public sealed class WorkOrderSaveData
{
    public string workOrderId = string.Empty;
    public string workTypeId = string.Empty;
    public int targetBuildingId;
    public int gridX;
    public int gridY;
    public float requiredWork;
    public float completedWork;
    public string materialDestinationId = string.Empty;
    public string constructionSitePersistentId = string.Empty;
    public long materialBufferCapacityGrams;
    public long materialMassAuthorityRevision;
    public string materialCapacityFingerprint = string.Empty;
    public string reservedWorkerPersistentId = string.Empty;
    public WorkerSelectionPolicySaveData workerPolicy =
        WorkerSelectionPolicySaveData.Anyone();
    public List<CraftContributionSaveData> contributions = new();
    public CraftQualityRollSaveData qualityRoll;
    public string qualityPipelineId = string.Empty;
    public int qualityAttemptIndex;
    public string destructiveDrainOperationId = string.Empty;
    public bool facilityRemovedForRetry;
    public bool cancelRebuildAfterDestructiveDrain;
    public List<WorkOrderItemMaterialSaveData> recoveryOutputs = new();
    public WorkOrderStatus status = WorkOrderStatus.WaitingForMaterials;
    public List<WorkOrderItemMaterialSaveData> itemMaterials =
        new List<WorkOrderItemMaterialSaveData>();
    public WorkOrderMaterialTransferSaveData materialTransfer = new();
}

[Serializable]
public sealed class DungeonWorkOrderSaveData
{
    public const int CurrentVersion = 8;

    public int version = CurrentVersion;
    public int nextOrderSequence = 1;
    public List<WorkOrderSaveData> orders = new List<WorkOrderSaveData>();
    public List<QualityTargetPipelineSaveData> qualityPipelines = new();
}

public sealed class WorkOrderProgressState
{
    public string WorkOrderId { get; set; }
    public WorkTypeId WorkTypeId { get; set; }
    public int TargetBuildingId { get; set; }
    public Vector2Int Position { get; set; }
    public float RequiredWork { get; set; }
    public float CompletedWork { get; set; }
    public string MaterialDestinationId { get; set; }
    public string ReservedWorkerPersistentId { get; set; }
    public WorkerSelectionPolicySaveData WorkerPolicy { get; set; }
    public IReadOnlyList<CraftContributionSaveData> Contributions { get; set; }
    public CraftQualityRollSaveData QualityRoll { get; set; }
    public string QualityPipelineId { get; set; }
    public int QualityAttemptIndex { get; set; }
    public WorkOrderStatus Status { get; set; }
    public IReadOnlyDictionary<string, int> ItemMaterialRequirements { get; set; }
    public IReadOnlyDictionary<string, int> DeliveredItemMaterials { get; set; }
    public WorkOrderMaterialTransferPhase MaterialTransferPhase { get; set; }
    public long MaterialInputMassGrams { get; set; }
    public float ProgressRatio => RequiredWork <= 0f ? 1f : Mathf.Clamp01(CompletedWork / RequiredWork);
}

public interface IWorkOrderWorkerPolicyQuery
{
    bool IsWorkerEligible(
        string orderId,
        CharacterActor actor,
        out string failureReason);
}

public interface IWorkOrderWorkerPolicyCommand
{
    bool SetWorkerPolicy(
        string orderId,
        WorkerSelectionPolicySaveData policy,
        out DomainFailure failure);
}

public interface IWorkOrderQuery
{
    int Version { get; }
    IReadOnlyList<WorkOrderProgressState> ActiveOrders { get; }
}

public interface IWorkOrderRuntime
{
    int WorkOrderCandidateVersion { get; }
    DungeonWorkOrderSaveData Capture();
    void ValidateRestorePayload(DungeonWorkOrderSaveData snapshot);
    WorkOrderRestoreCandidate PrepareRestoreCandidate(
        DungeonWorkOrderSaveData snapshot);
    void PublishRestoreCandidate(WorkOrderRestoreCandidate candidate);
    bool TryCreateConstructionOrder(ConstructionSite site, BuildingSO building, Vector2Int position, out string orderId, out string failureReason);
    bool TryGetOrderFor(BuildableObject target, WorkTypeId workTypeId, out WorkOrderProgressState order);
    bool ApplyWork(CharacterActor worker, BuildableObject target, WorkTypeId workTypeId, float amount, out bool completed, out bool appliedCompletionEffects, out string message);
    bool RefreshMaterialsReady(ConstructionSite site);
    bool CancelOrder(string orderId, bool refundDeliveredMaterials);
    bool DebugCompleteOrder(string orderId, out string message);
    int DebugCompleteAllOrders();
}

public interface IConstructionProjectWorkforceRuntime
{
    bool TryJoinConstructionProject(
        BuildableObject target,
        CharacterActor worker,
        out ProjectWorkerLease lease,
        out string failureReason);
    bool UpdateConstructionWorkerRate(
        BuildableObject target,
        CharacterActor worker,
        float wuPerSecond);
    float GetConstructionContributionMultiplier(
        BuildableObject target,
        CharacterActor worker);
    bool TryCaptureConstructionProject(
        BuildableObject target,
        out ProjectWorkforceSnapshot snapshot);
}

internal sealed class WorkOrderRecord
{
    public string workOrderId = string.Empty;
    public WorkTypeId workTypeId;
    public int targetBuildingId;
    public Vector2Int position;
    public float requiredWork;
    public float completedWork;
    // Runtime accumulation authority. Repeatedly adding small deltas to a
    // float produced visible long-run drift from the milli-WU labor ledger.
    // Save contracts remain float-compatible; restore seeds this value once.
    public double preciseCompletedWork;
    public string materialDestinationId = string.Empty;
    public string constructionSitePersistentId = string.Empty;
    public long materialBufferCapacityGrams;
    public long materialMassAuthorityRevision;
    public string materialCapacityFingerprint = string.Empty;
    public string reservedWorkerPersistentId = string.Empty;
    public WorkerSelectionPolicySaveData workerPolicy =
        WorkerSelectionPolicySaveData.Anyone();
    public readonly List<CraftContributionSaveData> contributions = new();
    public CraftQualityRollSaveData qualityRoll;
    public string qualityPipelineId = string.Empty;
    public int qualityAttemptIndex;
    public string destructiveDrainOperationId = string.Empty;
    public bool facilityRemovedForRetry;
    public bool cancelRebuildAfterDestructiveDrain;
    public WorkOrderStatus status = WorkOrderStatus.WaitingForMaterials;
    public readonly Dictionary<string, int> requiredItemMaterials =
        new Dictionary<string, int>(StringComparer.Ordinal);
    public readonly Dictionary<string, int> deliveredItemMaterials =
        new Dictionary<string, int>(StringComparer.Ordinal);
    public readonly Dictionary<string, int> requiredRecoveryOutputs =
        new Dictionary<string, int>(StringComparer.Ordinal);
    public readonly Dictionary<string, int> spawnedRecoveryOutputs =
        new Dictionary<string, int>(StringComparer.Ordinal);
    public WorkOrderMaterialTransferState materialTransfer = new();

    public WorkOrderRecord DeepClone()
    {
        WorkOrderRecord clone = new WorkOrderRecord
        {
            workOrderId = workOrderId,
            workTypeId = workTypeId,
            targetBuildingId = targetBuildingId,
            position = position,
            requiredWork = requiredWork,
            completedWork = completedWork,
            preciseCompletedWork = preciseCompletedWork,
            materialDestinationId = materialDestinationId,
            constructionSitePersistentId = constructionSitePersistentId,
            materialBufferCapacityGrams = materialBufferCapacityGrams,
            materialMassAuthorityRevision = materialMassAuthorityRevision,
            materialCapacityFingerprint = materialCapacityFingerprint,
            reservedWorkerPersistentId = reservedWorkerPersistentId,
            workerPolicy = workerPolicy?.CloneNormalized()
                ?? WorkerSelectionPolicySaveData.Anyone(),
            qualityRoll = qualityRoll == null ? null : new CraftQualityRollSaveData
            {
                attemptIndex = qualityRoll.attemptIndex,
                randomA = qualityRoll.randomA,
                randomB = qualityRoll.randomB,
                randomC = qualityRoll.randomC
            },
            qualityPipelineId = qualityPipelineId ?? string.Empty,
            qualityAttemptIndex = Math.Max(0, qualityAttemptIndex),
            destructiveDrainOperationId =
                destructiveDrainOperationId ?? string.Empty,
            facilityRemovedForRetry = facilityRemovedForRetry,
            cancelRebuildAfterDestructiveDrain =
                cancelRebuildAfterDestructiveDrain,
            status = status,
            materialTransfer = materialTransfer?.DeepClone()
                ?? new WorkOrderMaterialTransferState()
        };
        clone.contributions.AddRange(contributions.Select(value => value?.Clone())
            .Where(value => value != null));
        Copy(requiredItemMaterials, clone.requiredItemMaterials);
        Copy(deliveredItemMaterials, clone.deliveredItemMaterials);
        Copy(requiredRecoveryOutputs, clone.requiredRecoveryOutputs);
        Copy(spawnedRecoveryOutputs, clone.spawnedRecoveryOutputs);
        return clone;
    }

    private static void Copy<TKey>(
        IReadOnlyDictionary<TKey, int> source,
        IDictionary<TKey, int> destination)
    {
        foreach (KeyValuePair<TKey, int> pair in source)
        {
            destination.Add(pair.Key, pair.Value);
        }
    }
}

internal sealed class WorkOrderMaterialSourceState
{
    internal string ItemId = string.Empty;
    internal string StackId = string.Empty;
    internal int Quantity;

    internal WorkOrderMaterialSourceState DeepClone() => new()
    {
        ItemId = ItemId,
        StackId = StackId,
        Quantity = Quantity
    };
}

internal sealed class WorkOrderMaterialTransferState
{
    internal WorkOrderMaterialTransferPhase Phase;
    internal string OperationId = string.Empty;
    internal string ReasonCode = string.Empty;
    internal string RequestFingerprint = string.Empty;
    internal string CommitId = string.Empty;
    internal int InputQuantity;
    internal long InputMassGrams;
    internal readonly List<WorkOrderMaterialSourceState> Sources = new();
    internal string RestitutionOperationId = string.Empty;

    internal bool HasCustody => Phase is
        WorkOrderMaterialTransferPhase.InputCommitted
        or WorkOrderMaterialTransferPhase.CustodyPublished
        or WorkOrderMaterialTransferPhase.Acknowledged
        or WorkOrderMaterialTransferPhase.RestitutionPending;

    internal WorkOrderMaterialTransferState DeepClone()
    {
        WorkOrderMaterialTransferState clone = new()
        {
            Phase = Phase,
            OperationId = OperationId,
            ReasonCode = ReasonCode,
            RequestFingerprint = RequestFingerprint,
            CommitId = CommitId,
            InputQuantity = InputQuantity,
            InputMassGrams = InputMassGrams,
            RestitutionOperationId = RestitutionOperationId
        };
        clone.Sources.AddRange(Sources.Select(value => value.DeepClone()));
        return clone;
    }
}
