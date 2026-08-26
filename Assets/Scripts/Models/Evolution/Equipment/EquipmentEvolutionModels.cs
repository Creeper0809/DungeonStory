using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum EquipmentEvolutionDirection
{
    Balanced,
    Melee,
    Ranged,
    Accuracy,
    Execution,
    Interception,
    Protection,
    Survival
}

public enum EvolutionReforgeOrderState
{
    WaitingForMaterials,
    Ready,
    InProgress,
    Completed,
    Cancelled,
    Blocked
}

[Serializable]
public sealed class EquipmentEvolutionMaterialTransferInput
{
    public string itemId = string.Empty;
    public string sourceStackId = string.Empty;
    public int quantity;

    public EquipmentEvolutionMaterialTransferInput Clone()
    {
        return new EquipmentEvolutionMaterialTransferInput
        {
            itemId = itemId ?? string.Empty,
            sourceStackId = sourceStackId ?? string.Empty,
            quantity = Mathf.Max(0, quantity)
        };
    }
}

[Serializable]
public sealed class EquipmentCatalystDefinition
{
    public string itemId = string.Empty;
    public string family = string.Empty;
    public int progressionLevel = 1;
    public int potency = 1;
    public List<string> sourceTags = new List<string>();

    public EquipmentCatalystDefinition Clone()
    {
        return new EquipmentCatalystDefinition
        {
            itemId = itemId ?? string.Empty,
            family = family ?? string.Empty,
            progressionLevel = progressionLevel,
            potency = potency,
            sourceTags = sourceTags?
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(tag => tag, StringComparer.Ordinal)
                .ToList() ?? new List<string>()
        };
    }
}

[Serializable]
public sealed class EquipmentEvolutionState
{
    public int generation;
    public float mastery;
    public UsageLedger usageLedger = new UsageLedger();
    public List<EvolutionNode> evolutionNodes = new List<EvolutionNode>();
    public List<AttunementRecord> attunements = new List<AttunementRecord>();
    public List<string> activeHistoricalNodeIds = new List<string>();
    public List<EvolutionNarrativeRequestSnapshot> narrativeRequests =
        new List<EvolutionNarrativeRequestSnapshot>();
    public EquipmentEvolutionDirection pendingDirection =
        EquipmentEvolutionDirection.Balanced;
    public string pendingHistoryHash = string.Empty;
    public bool reforgeReady;

    public int ResonanceBudget =>
        3 + Mathf.FloorToInt(Mathf.Sqrt(Mathf.Max(0, generation)));
    public float RequiredMastery =>
        EquipmentEvolutionProgression.GetRequiredMastery(generation);

    public EquipmentEvolutionState Clone()
    {
        return new EquipmentEvolutionState
        {
            generation = Mathf.Max(0, generation),
            mastery = Mathf.Max(0f, mastery),
            usageLedger = usageLedger?.Clone() ?? new UsageLedger(),
            evolutionNodes = evolutionNodes?
                .Where(node => node != null)
                .Select(node => node.Clone())
                .ToList() ?? new List<EvolutionNode>(),
            attunements = attunements?
                .Where(record => record != null)
                .Select(record => record.Clone())
                .ToList() ?? new List<AttunementRecord>(),
            activeHistoricalNodeIds = activeHistoricalNodeIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>(),
            narrativeRequests = narrativeRequests?
                .Where(request => request != null)
                .Select(request => request.Clone())
                .ToList() ?? new List<EvolutionNarrativeRequestSnapshot>(),
            pendingDirection = pendingDirection,
            pendingHistoryHash = pendingHistoryHash ?? string.Empty,
            reforgeReady = reforgeReady
        };
    }
}

[Serializable]
public sealed class EvolutionReforgeOrder
{
    public string orderId = string.Empty;
    public string equipmentInstanceId = string.Empty;
    public string facilityPersistentId = string.Empty;
    public int targetGeneration;
    public EquipmentEvolutionDirection direction;
    public string catalystItemId = string.Empty;
    public string catalystFamily = string.Empty;
    public int catalystPotency;
    public List<string> catalystSourceTags = new List<string>();
    public string primaryMaterialItemId = string.Empty;
    public int primaryMaterialAmount = 1;
    public string bindingItemId = string.Empty;
    public int bindingAmount = 1;
    public string stabilizerItemId = string.Empty;
    public int stabilizerAmount;
    public float requiredWork;
    public float completedWork;
    public EvolutionReforgeOrderState state =
        EvolutionReforgeOrderState.WaitingForMaterials;
    public string destinationId = string.Empty;
    public int destinationX;
    public int destinationY;
    public string lockedHistoryHash = string.Empty;
    public EquipmentEvolutionDirection lockedDirection;
    public bool materialsConsumed;
    public bool equipmentDelivered;
    public string materialTransferOperationId = string.Empty;
    public string materialTransferCommitId = string.Empty;
    public string materialTransferRequestFingerprint = string.Empty;
    public long materialTransferMassGrams;
    public bool materialTransferOutcomePublished;
    public List<EquipmentEvolutionMaterialTransferInput> materialTransferInputs =
        new List<EquipmentEvolutionMaterialTransferInput>();
    public bool preciseCalibration;
    public bool burdenSuppression;
    public bool externalTechnicalSupport;
    public string suppressedBurdenEffectId = string.Empty;
    public int precisionGoldCost;
    [Range(0.01f, 0.5f)] public float resultVariance = 0.12f;

    public float ProgressRatio => requiredWork <= 0f
        ? 0f
        : Mathf.Clamp01(completedWork / requiredWork);

    public EvolutionReforgeOrder Clone()
    {
        return new EvolutionReforgeOrder
        {
            orderId = orderId ?? string.Empty,
            equipmentInstanceId = equipmentInstanceId ?? string.Empty,
            facilityPersistentId = facilityPersistentId ?? string.Empty,
            targetGeneration = Mathf.Max(1, targetGeneration),
            direction = direction,
            catalystItemId = catalystItemId ?? string.Empty,
            catalystFamily = catalystFamily ?? string.Empty,
            catalystPotency = Mathf.Max(1, catalystPotency),
            catalystSourceTags = catalystSourceTags?
                .Where(tag => !string.IsNullOrWhiteSpace(tag))
                .Select(tag => tag.Trim())
                .Distinct(StringComparer.Ordinal)
                .ToList() ?? new List<string>(),
            primaryMaterialItemId = primaryMaterialItemId ?? string.Empty,
            primaryMaterialAmount = Mathf.Max(1, primaryMaterialAmount),
            bindingItemId = bindingItemId ?? string.Empty,
            bindingAmount = Mathf.Max(1, bindingAmount),
            stabilizerItemId = stabilizerItemId ?? string.Empty,
            stabilizerAmount = Mathf.Max(0, stabilizerAmount),
            requiredWork = Mathf.Max(0.1f, requiredWork),
            completedWork = Mathf.Clamp(completedWork, 0f, Mathf.Max(0.1f, requiredWork)),
            state = state,
            destinationId = destinationId ?? string.Empty,
            destinationX = destinationX,
            destinationY = destinationY,
            lockedHistoryHash = lockedHistoryHash ?? string.Empty,
            lockedDirection = lockedDirection,
            materialsConsumed = materialsConsumed,
            equipmentDelivered = equipmentDelivered,
            materialTransferOperationId =
                materialTransferOperationId ?? string.Empty,
            materialTransferCommitId =
                materialTransferCommitId ?? string.Empty,
            materialTransferRequestFingerprint =
                materialTransferRequestFingerprint ?? string.Empty,
            materialTransferMassGrams = materialTransferMassGrams,
            materialTransferOutcomePublished =
                materialTransferOutcomePublished,
            materialTransferInputs = materialTransferInputs?
                .Where(input => input != null)
                .Select(input => input.Clone())
                .ToList() ??
                new List<EquipmentEvolutionMaterialTransferInput>(),
            preciseCalibration = preciseCalibration,
            burdenSuppression = burdenSuppression,
            externalTechnicalSupport = externalTechnicalSupport,
            suppressedBurdenEffectId =
                suppressedBurdenEffectId ?? string.Empty,
            precisionGoldCost = Mathf.Max(0, precisionGoldCost),
            resultVariance = Mathf.Clamp(resultVariance, 0.01f, 0.5f)
        };
    }
}

[Serializable]
public sealed class EquipmentReattunementOrder
{
    public string orderId = string.Empty;
    public string equipmentInstanceId = string.Empty;
    public string facilityPersistentId = string.Empty;
    public string targetNodeId = string.Empty;
    public bool targetActive;
    public List<string> resultingActiveNodeIds = new List<string>();
    public string catalystItemId = string.Empty;
    public int catalystPotency;
    public float requiredWork;
    public float completedWork;
    public EvolutionReforgeOrderState state =
        EvolutionReforgeOrderState.WaitingForMaterials;
    public string destinationId = string.Empty;
    public int destinationX;
    public int destinationY;
    public string lockedStateHash = string.Empty;
    public bool materialsConsumed;
    public bool equipmentDelivered;
    public string materialTransferOperationId = string.Empty;
    public string materialTransferCommitId = string.Empty;
    public string materialTransferRequestFingerprint = string.Empty;
    public long materialTransferMassGrams;
    public bool materialTransferOutcomePublished;
    public List<EquipmentEvolutionMaterialTransferInput> materialTransferInputs =
        new List<EquipmentEvolutionMaterialTransferInput>();

    public float ProgressRatio => requiredWork <= 0f
        ? 0f
        : Mathf.Clamp01(completedWork / requiredWork);

    public EquipmentReattunementOrder Clone()
    {
        return new EquipmentReattunementOrder
        {
            orderId = orderId ?? string.Empty,
            equipmentInstanceId = equipmentInstanceId ?? string.Empty,
            facilityPersistentId = facilityPersistentId ?? string.Empty,
            targetNodeId = targetNodeId ?? string.Empty,
            targetActive = targetActive,
            resultingActiveNodeIds = resultingActiveNodeIds?
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Select(id => id.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList() ?? new List<string>(),
            catalystItemId = catalystItemId ?? string.Empty,
            catalystPotency = Mathf.Max(1, catalystPotency),
            requiredWork = Mathf.Max(0.1f, requiredWork),
            completedWork = Mathf.Clamp(
                completedWork,
                0f,
                Mathf.Max(0.1f, requiredWork)),
            state = state,
            destinationId = destinationId ?? string.Empty,
            destinationX = destinationX,
            destinationY = destinationY,
            lockedStateHash = lockedStateHash ?? string.Empty,
            materialsConsumed = materialsConsumed,
            equipmentDelivered = equipmentDelivered,
            materialTransferOperationId =
                materialTransferOperationId ?? string.Empty,
            materialTransferCommitId =
                materialTransferCommitId ?? string.Empty,
            materialTransferRequestFingerprint =
                materialTransferRequestFingerprint ?? string.Empty,
            materialTransferMassGrams = materialTransferMassGrams,
            materialTransferOutcomePublished =
                materialTransferOutcomePublished,
            materialTransferInputs = materialTransferInputs?
                .Where(input => input != null)
                .Select(input => input.Clone())
                .ToList() ??
                new List<EquipmentEvolutionMaterialTransferInput>()
        };
    }
}

[Serializable]
public sealed class FacilityRecalibrationOrder
{
    public string orderId = string.Empty;
    public string facilityPersistentId = string.Empty;
    public string nodeId = string.Empty;
    public EvolutionModuleActivationRule targetRule =
        new EvolutionModuleActivationRule();
    public string catalystItemId = string.Empty;
    public int catalystPotency;
    public float requiredWork;
    public float completedWork;
    public EvolutionReforgeOrderState state =
        EvolutionReforgeOrderState.WaitingForMaterials;
    public string destinationId = string.Empty;
    public int destinationX;
    public int destinationY;
    public bool materialsConsumed;
    public string materialTransferOperationId = string.Empty;
    public string materialTransferCommitId = string.Empty;
    public string materialTransferSourceStackId = string.Empty;
    public long materialTransferMassGrams;
    public bool materialTransferOutcomePublished;

    public FacilityRecalibrationOrder Clone()
    {
        return new FacilityRecalibrationOrder
        {
            orderId = orderId ?? string.Empty,
            facilityPersistentId = facilityPersistentId ?? string.Empty,
            nodeId = nodeId ?? string.Empty,
            targetRule = targetRule?.Clone() ??
                new EvolutionModuleActivationRule(),
            catalystItemId = catalystItemId ?? string.Empty,
            catalystPotency = Mathf.Max(1, catalystPotency),
            requiredWork = Mathf.Max(0.1f, requiredWork),
            completedWork = Mathf.Clamp(
                completedWork,
                0f,
                Mathf.Max(0.1f, requiredWork)),
            state = state,
            destinationId = destinationId ?? string.Empty,
            destinationX = destinationX,
            destinationY = destinationY,
            materialsConsumed = materialsConsumed
            ,materialTransferOperationId = materialTransferOperationId ?? string.Empty
            ,materialTransferCommitId = materialTransferCommitId ?? string.Empty
            ,materialTransferSourceStackId = materialTransferSourceStackId ?? string.Empty
            ,materialTransferMassGrams = materialTransferMassGrams
            ,materialTransferOutcomePublished = materialTransferOutcomePublished
        };
    }
}

public readonly struct EquipmentReforgePreview
{
    public EquipmentReforgePreview(
        EquipmentEvolutionDirection direction,
        float minimumMultiplier,
        float maximumMultiplier,
        IReadOnlyList<string> possibleBurdenIds,
        int requiredCatalystProgressionLevel)
    {
        Direction = direction;
        MinimumMultiplier = Mathf.Max(1f, minimumMultiplier);
        MaximumMultiplier = Mathf.Max(MinimumMultiplier, maximumMultiplier);
        PossibleBurdenIds = possibleBurdenIds ?? Array.Empty<string>();
        RequiredCatalystProgressionLevel = Mathf.Max(
            1,
            requiredCatalystProgressionLevel);
    }

    public EquipmentEvolutionDirection Direction { get; }
    public float MinimumMultiplier { get; }
    public float MaximumMultiplier { get; }
    public IReadOnlyList<string> PossibleBurdenIds { get; }
    public int RequiredCatalystProgressionLevel { get; }
}

public static class EquipmentEvolutionProgression
{
    public static float GetRequiredMastery(int generation)
    {
        return 100f + 50f * Mathf.Max(0, generation);
    }

    public static int GetMinimumCatalystProgressionLevel(int generation)
    {
        return 1 + Mathf.FloorToInt(Mathf.Max(0, generation) / 4f);
    }

    public static float GetReforgeWork(float baseCraftWork, int generation)
    {
        return Mathf.Max(1f, baseCraftWork)
            * (0.65f + 0.2f * Mathf.Sqrt(Mathf.Max(0, generation) + 1f));
    }

    public static float GetReattunementWork(float baseCraftWork, int generation)
    {
        return GetReforgeWork(baseCraftWork, generation) * 0.75f;
    }
}
