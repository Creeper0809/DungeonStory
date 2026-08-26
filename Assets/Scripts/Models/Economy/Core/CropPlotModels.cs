using System;
using System.Collections.Generic;
using UnityEngine;

public enum CropPlotPhase
{
    Empty = 0,
    WaitingForMaterials = 1,
    ReadyToSow = 2,
    Sowing = 3,
    Growing = 4,
    ReadyToHarvest = 5,
    Harvesting = 6,
    Blocked = 7
}

public readonly struct CropPlotWorkSnapshot
{
    public CropPlotWorkSnapshot(
        string plotId,
        WorkTypeId workTypeId,
        string displayName,
        float requiredWork,
        float completedWork,
        bool available,
        string unavailableReason)
    {
        PlotId = plotId ?? string.Empty;
        WorkTypeId = workTypeId;
        DisplayName = displayName ?? string.Empty;
        RequiredWork = Mathf.Max(0.1f, requiredWork);
        CompletedWork = Mathf.Clamp(completedWork, 0f, RequiredWork);
        Available = available;
        UnavailableReason = unavailableReason ?? string.Empty;
    }

    public string PlotId { get; }
    public WorkTypeId WorkTypeId { get; }
    public string DisplayName { get; }
    public float RequiredWork { get; }
    public float CompletedWork { get; }
    public bool Available { get; }
    public string UnavailableReason { get; }
}

public sealed class CropPlotSnapshot
{
    public string PlotId { get; set; } = string.Empty;
    public int BuildingId { get; set; }
    public Vector2Int Position { get; set; }
    public bool Indoor { get; set; }
    public string CropId { get; set; } = string.Empty;
    public string CropName { get; set; } = string.Empty;
    public string SeedItemId { get; set; } = string.Empty;
    public string CultivarGenomeId { get; set; } = string.Empty;
    public float Fertility { get; set; } = 100f;
    public float PestPressure { get; set; }
    public float DiseasePressure { get; set; }
    public CropDiseaseKind CropDisease { get; set; }
    public CropPlotPhase Phase { get; set; }
    public float SowProgress { get; set; }
    public float GrowthProgress { get; set; }
    public float HarvestProgress { get; set; }
    public string MaterialDestinationId { get; set; } = string.Empty;
    public IReadOnlyDictionary<string, int> RequiredMaterials { get; set; } =
        new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> DeliveredMaterials { get; set; } =
        new Dictionary<string, int>();
    public string BlockedReason { get; set; } = string.Empty;
    public string GoldenHarvestHarvesterId { get; set; } = string.Empty;
    public int GoldenHarvestAttemptSequence { get; set; }
    public bool TreatmentScheduled { get; set; }
    public CropTreatmentOrderPhase TreatmentPhase { get; set; }
    public string TreatmentItemId { get; set; } = string.Empty;
    public string TreatmentItemName { get; set; } = string.Empty;
    public CropTreatmentKind TreatmentKind { get; set; }
    public int TreatmentRequiredQuantity { get; set; }
    public int TreatmentDeliveredQuantity { get; set; }
    public float TreatmentRequiredWork { get; set; }
    public float TreatmentCompletedWork { get; set; }
    public float TreatmentEffectAmount { get; set; }
    public int TreatmentCooldownDays { get; set; }
    public string TreatmentDestinationId { get; set; } = string.Empty;
    public string TreatmentFailureReason { get; set; } = string.Empty;
    public int CurrentAbsoluteDay { get; set; }
    public int PestLureNextAllowedDay { get; set; }
    public int BotanicalPesticideNextAllowedDay { get; set; }
    public int FungicideNextAllowedDay { get; set; }
}

public readonly struct CropPlotVisualState
{
    public CropPlotVisualState(
        string plotId,
        BuildableObject building,
        string cropId,
        CropPlotPhase phase,
        float growthProgress)
    {
        PlotId = plotId ?? string.Empty;
        Building = building;
        CropId = cropId ?? string.Empty;
        Phase = phase;
        GrowthProgress = Mathf.Clamp01(growthProgress);
    }

    public string PlotId { get; }
    public BuildableObject Building { get; }
    public string CropId { get; }
    public CropPlotPhase Phase { get; }
    public float GrowthProgress { get; }
}

[Serializable]
public sealed class CropPlotSaveData
{
    public string buildingInstanceId = string.Empty;
    public int lastKnownGridX;
    public int lastKnownGridY;
    public string cropId = string.Empty;
    public CropPlotPhase phase;
    public float sowWork;
    public float growthHours;
    public float harvestWork;
    public bool materialsConsumed;
    public string goldenHarvestHarvesterId = string.Empty;
    public int goldenHarvestAttemptSequence;
    public int nextSowOperationSequence;
    public CropPhysicalCommitSaveData pendingSow = new();
    public int nextTreatmentOperationSequence;
    public int pestLureNextAllowedDay;
    public int botanicalPesticideNextAllowedDay;
    public int fungicideNextAllowedDay;
    public CropTreatmentOrderSaveData treatment = new();
}

public enum CropTreatmentOrderPhase
{
    None = 0,
    WaitingForDelivery = 1,
    ReadyForWork = 2,
    Working = 3,
    InputCommitted = 4,
    OutcomePublished = 5,
    PlotDestroyedLossPending = 6
}

public enum CropTreatmentTerminalDisposition
{
    None = 0,
    DestroyedWithPlotLoss = 1
}

[Serializable]
public sealed class CropTreatmentOrderSaveData
{
    public CropTreatmentOrderPhase phase;
    public int operationSequence;
    public string operationId = string.Empty;
    public string reasonCode = string.Empty;
    public string destinationId = string.Empty;
    public string itemId = string.Empty;
    public CropTreatmentKind treatmentKind;
    public int quantity;
    public float requiredWork;
    public float completedWork;
    public float effectAmount;
    public int cooldownDays;
    public int scheduledAbsoluteDay;
    public string failureReason = string.Empty;
    public List<string> sourceStackIds = new();
    public long inputMassGrams;
    public string commitId = string.Empty;
    public string requestFingerprint = string.Empty;
    public int tareOutputQuantity;
    public long tareOutputMassGrams;
    public long destroyedTareMassGrams;
    public List<string> tareOutputCommitIds = new();
    public string ecologyBeforeFingerprint = string.Empty;
    public string ecologyAfterFingerprint = string.Empty;
    public CropTreatmentTerminalDisposition terminalDisposition;
    public string terminalReasonCode = string.Empty;
    public int terminalLossQuantity;
    public long terminalLossMassGrams;

    public CropTreatmentOrderSaveData DeepClone() => new()
    {
        phase = phase,
        operationSequence = operationSequence,
        operationId = operationId ?? string.Empty,
        reasonCode = reasonCode ?? string.Empty,
        destinationId = destinationId ?? string.Empty,
        itemId = itemId ?? string.Empty,
        treatmentKind = treatmentKind,
        quantity = quantity,
        requiredWork = requiredWork,
        completedWork = completedWork,
        effectAmount = effectAmount,
        cooldownDays = cooldownDays,
        scheduledAbsoluteDay = scheduledAbsoluteDay,
        failureReason = failureReason ?? string.Empty,
        sourceStackIds = new List<string>(sourceStackIds ?? new List<string>()),
        inputMassGrams = inputMassGrams,
        commitId = commitId ?? string.Empty,
        requestFingerprint = requestFingerprint ?? string.Empty,
        tareOutputQuantity = tareOutputQuantity,
        tareOutputMassGrams = tareOutputMassGrams,
        destroyedTareMassGrams = destroyedTareMassGrams,
        tareOutputCommitIds = new List<string>(
            tareOutputCommitIds ?? new List<string>()),
        ecologyBeforeFingerprint = ecologyBeforeFingerprint ?? string.Empty,
        ecologyAfterFingerprint = ecologyAfterFingerprint ?? string.Empty,
        terminalDisposition = terminalDisposition,
        terminalReasonCode = terminalReasonCode ?? string.Empty,
        terminalLossQuantity = terminalLossQuantity,
        terminalLossMassGrams = terminalLossMassGrams
    };
}

public enum CropPhysicalCommitPhase
{
    None = 0,
    InputCommitted = 1,
    OutcomePublished = 2,
    PlotDestroyedLossPending = 3
}

public enum CropWipTerminalDisposition
{
    None = 0,
    DestroyedWithPlotLoss = 1
}

[Serializable]
public sealed class CropPhysicalInputSaveData
{
    public string itemId = string.Empty;
    public string sourceStackId = string.Empty;
    public int quantity;

    public CropPhysicalInputSaveData DeepClone() => new()
    {
        itemId = itemId ?? string.Empty,
        sourceStackId = sourceStackId ?? string.Empty,
        quantity = quantity
    };
}

/// <summary>
/// Domain-owned half of a pending physical input disposition. The matching
/// item receipt remains unacknowledged until the crop outcome is published.
/// </summary>
[Serializable]
public sealed class CropPhysicalCommitSaveData
{
    public CropPhysicalCommitPhase phase;
    public int operationSequence;
    public string operationId = string.Empty;
    public string reasonCode = string.Empty;
    public string destinationId = string.Empty;
    public string cropId = string.Empty;
    public string seedItemId = string.Empty;
    public int inputQuantity;
    public long inputMassGrams;
    public string commitId = string.Empty;
    public string requestFingerprint = string.Empty;
    public bool hasSeedLot;
    public SeedLotState seedLot;
    public string ecologyBeforeFingerprint = string.Empty;
    public string ecologyAfterFingerprint = string.Empty;
    public CropWipTerminalDisposition terminalDisposition;
    public string terminalOperationId = string.Empty;
    public string terminalReasonCode = string.Empty;
    public int terminalLossQuantity;
    public long terminalLossMassGrams;
    public List<CropPhysicalInputSaveData> inputs = new();

    public CropPhysicalCommitSaveData DeepClone() => new()
    {
        phase = phase,
        operationSequence = operationSequence,
        operationId = operationId ?? string.Empty,
        reasonCode = reasonCode ?? string.Empty,
        destinationId = destinationId ?? string.Empty,
        cropId = cropId ?? string.Empty,
        seedItemId = seedItemId ?? string.Empty,
        inputQuantity = inputQuantity,
        inputMassGrams = inputMassGrams,
        commitId = commitId ?? string.Empty,
        requestFingerprint = requestFingerprint ?? string.Empty,
        hasSeedLot = hasSeedLot,
        seedLot = hasSeedLot ? seedLot?.Clone() : null,
        ecologyBeforeFingerprint = ecologyBeforeFingerprint ?? string.Empty,
        ecologyAfterFingerprint = ecologyAfterFingerprint ?? string.Empty,
        terminalDisposition = terminalDisposition,
        terminalOperationId = terminalOperationId ?? string.Empty,
        terminalReasonCode = terminalReasonCode ?? string.Empty,
        terminalLossQuantity = terminalLossQuantity,
        terminalLossMassGrams = terminalLossMassGrams,
        inputs = (inputs ?? new List<CropPhysicalInputSaveData>())
            .ConvertAll(value => value?.DeepClone())
    };
}

[Serializable]
public sealed class DungeonCropPlotSaveData
{
    public const int CurrentVersion = 7;

    public int version = CurrentVersion;
    public List<CropPlotSaveData> plots = new List<CropPlotSaveData>();
}

public interface ICropPlotRuntime
{
    int Version { get; }
    IReadOnlyList<CropPlotSnapshot> Plots { get; }
    void CopyVisualStates(List<CropPlotVisualState> destination);
    bool TrySetCrop(
        BuildableObject plot,
        string cropId,
        out string message);
    bool CanScheduleTreatment(
        BuildableObject plot,
        string treatmentItemId,
        out string reason);
    bool TryScheduleTreatment(
        BuildableObject plot,
        string treatmentItemId,
        out string message);
    bool TryCancelTreatment(
        BuildableObject plot,
        out string message);
    bool TryGetWork(
        BuildableObject plot,
        WorkTypeId workTypeId,
        out CropPlotWorkSnapshot snapshot);
    bool ApplyWork(
        BuildableObject plot,
        WorkTypeId workTypeId,
        float amount,
        out bool cycleCompleted);
    bool ApplyWork(
        BuildableObject plot,
        WorkTypeId workTypeId,
        float amount,
        CharacterActor worker,
        out bool cycleCompleted);
    bool TryScheduleGoldenHarvest(
        BuildableObject plot,
        CharacterActor harvester,
        out string failureReason);
    bool IsGoldenHarvestWorkerEligible(
        BuildableObject plot,
        CharacterActor harvester,
        out string failureReason);
    bool TryGetGoldenHarvestDelay(
        BuildableObject plot,
        CharacterActor harvester,
        out float remainingSeconds);
}

public sealed class CropPlotRestoreCandidate
{
    internal CropPlotRestoreCandidate(CropPlotAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal CropPlotAggregateState State { get; }
}

public interface ICropPlotPersistence
{
    DungeonCropPlotSaveData Capture();
    CropPlotRestoreCandidate BuildRestore(DungeonCropPlotSaveData snapshot);
    void Restore(CropPlotRestoreCandidate candidate);
}
