using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum FacilityGenerationCandidateKind
{
    PrimaryRole,
    RoomSynergy,
    RiskyCatalyst
}

public enum FacilityRelocationPhase
{
    Dismantling,
    WaitingForPackage,
    Reinstalling,
    Blocked
}

[Serializable]
public sealed class FacilityGenerationCandidate
{
    public string candidateId = string.Empty;
    public FacilityGenerationCandidateKind kind;
    public int targetGeneration;
    public string benefitModuleId = string.Empty;
    public string burdenModuleId = string.Empty;
    public string catalystFamily = string.Empty;
    public int minimumCatalystPotency;
    public string historyHash = string.Empty;
    public EvolutionModuleActivationRule activationRule =
        new EvolutionModuleActivationRule();

    public FacilityGenerationCandidate Clone()
    {
        return new FacilityGenerationCandidate
        {
            candidateId = candidateId ?? string.Empty,
            kind = kind,
            targetGeneration = Mathf.Max(1, targetGeneration),
            benefitModuleId = benefitModuleId ?? string.Empty,
            burdenModuleId = burdenModuleId ?? string.Empty,
            catalystFamily = catalystFamily ?? string.Empty,
            minimumCatalystPotency = Mathf.Max(0, minimumCatalystPotency),
            historyHash = historyHash ?? string.Empty,
            activationRule = activationRule?.Clone() ??
                new EvolutionModuleActivationRule()
        };
    }
}

[Serializable]
public sealed class FacilityEvolutionState
{
    public string facilityPersistentId = string.Empty;
    public int generation;
    public float mastery;
    public UsageLedger usageLedger = new UsageLedger();
    public List<EvolutionNode> evolutionNodes = new List<EvolutionNode>();
    public List<FacilityGenerationCandidate> pendingCandidates =
        new List<FacilityGenerationCandidate>();
    public string pendingHistoryHash = string.Empty;
    public int roomStructureVersion = -1;
    public int facilityStateVersion = -1;
    public List<string> activeNodeIds = new List<string>();
    public List<string> dormantNodeIds = new List<string>();
    public List<EvolutionNarrativeRequestSnapshot> narrativeRequests =
        new List<EvolutionNarrativeRequestSnapshot>();
    public FacilityModificationOrder modificationOrder;
    public FacilityRecalibrationOrder recalibrationOrder;
    public FacilityRelocationOrder relocationOrder;

    public float RequiredMastery => FacilityEvolutionProgression.GetRequiredMastery(
        generation);
    public bool ReadyForGeneration => mastery + 0.001f >= RequiredMastery;

    public FacilityEvolutionState Clone()
    {
        return new FacilityEvolutionState
        {
            facilityPersistentId = facilityPersistentId ?? string.Empty,
            generation = Mathf.Max(0, generation),
            mastery = Mathf.Max(0f, mastery),
            usageLedger = usageLedger?.Clone() ?? new UsageLedger(),
            evolutionNodes = evolutionNodes?
                .Where(node => node != null)
                .Select(node => node.Clone())
                .ToList() ?? new List<EvolutionNode>(),
            pendingCandidates = pendingCandidates?
                .Where(candidate => candidate != null)
                .Select(candidate => candidate.Clone())
                .ToList() ?? new List<FacilityGenerationCandidate>(),
            pendingHistoryHash = pendingHistoryHash ?? string.Empty,
            roomStructureVersion = roomStructureVersion,
            facilityStateVersion = facilityStateVersion,
            activeNodeIds = Normalize(activeNodeIds),
            dormantNodeIds = Normalize(dormantNodeIds),
            narrativeRequests = narrativeRequests?
                .Where(request => request != null)
                .Select(request => request.Clone())
                .ToList() ?? new List<EvolutionNarrativeRequestSnapshot>(),
            modificationOrder = HasOrderId(modificationOrder?.orderId)
                ? modificationOrder.Clone()
                : null,
            recalibrationOrder = HasOrderId(recalibrationOrder?.orderId)
                ? recalibrationOrder.Clone()
                : null,
            relocationOrder = HasOrderId(relocationOrder?.orderId)
                ? relocationOrder.Clone()
                : null
        };
    }

    private static bool HasOrderId(string orderId)
    {
        return !string.IsNullOrWhiteSpace(orderId);
    }

    private static List<string> Normalize(IEnumerable<string> values)
    {
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList() ?? new List<string>();
    }
}

[Serializable]
public sealed class FacilityModificationOrder
{
    public string orderId = string.Empty;
    public string facilityPersistentId = string.Empty;
    public FacilityGenerationCandidate candidate =
        new FacilityGenerationCandidate();
    public string bindingItemId = string.Empty;
    public int bindingAmount;
    public string catalystItemId = string.Empty;
    public int catalystAmount;
    public float requiredWork;
    public float completedWork;
    public EvolutionReforgeOrderState state =
        EvolutionReforgeOrderState.WaitingForMaterials;
    public string destinationId = string.Empty;
    public int destinationX;
    public int destinationY;
    public bool materialsConsumed;

    public float ProgressRatio => requiredWork <= 0f
        ? 0f
        : Mathf.Clamp01(completedWork / requiredWork);

    public FacilityModificationOrder Clone()
    {
        return new FacilityModificationOrder
        {
            orderId = orderId ?? string.Empty,
            facilityPersistentId = facilityPersistentId ?? string.Empty,
            candidate = candidate?.Clone() ?? new FacilityGenerationCandidate(),
            bindingItemId = bindingItemId ?? string.Empty,
            bindingAmount = Mathf.Max(0, bindingAmount),
            catalystItemId = catalystItemId ?? string.Empty,
            catalystAmount = Mathf.Max(0, catalystAmount),
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
        };
    }
}

[Serializable]
public sealed class FacilityRelocationOrder
{
    public string orderId = string.Empty;
    public string facilityPersistentId = string.Empty;
    public string packageItemId = string.Empty;
    public string packageStackId = string.Empty;
    public string destinationId = string.Empty;
    public int sourceX;
    public int sourceY;
    public int destinationX;
    public int destinationY;
    public float dismantleRequiredWork;
    public float dismantleCompletedWork;
    public float reinstallRequiredWork;
    public float reinstallCompletedWork;
    public FacilityRelocationPhase phase = FacilityRelocationPhase.Dismantling;
    public bool packageConsumed;

    public Vector2Int SourcePosition => new Vector2Int(sourceX, sourceY);
    public Vector2Int DestinationPosition =>
        new Vector2Int(destinationX, destinationY);
    public float ActiveRequiredWork =>
        phase == FacilityRelocationPhase.Dismantling
            ? Mathf.Max(0.1f, dismantleRequiredWork)
            : Mathf.Max(0.1f, reinstallRequiredWork);
    public float ActiveCompletedWork =>
        phase == FacilityRelocationPhase.Dismantling
            ? Mathf.Max(0f, dismantleCompletedWork)
            : Mathf.Max(0f, reinstallCompletedWork);
    public float ProgressRatio => Mathf.Clamp01(
        ActiveCompletedWork / ActiveRequiredWork);

    public FacilityRelocationOrder Clone()
    {
        return new FacilityRelocationOrder
        {
            orderId = orderId ?? string.Empty,
            facilityPersistentId = facilityPersistentId ?? string.Empty,
            packageItemId = packageItemId ?? string.Empty,
            packageStackId = packageStackId ?? string.Empty,
            destinationId = destinationId ?? string.Empty,
            sourceX = sourceX,
            sourceY = sourceY,
            destinationX = destinationX,
            destinationY = destinationY,
            dismantleRequiredWork = Mathf.Max(0.1f, dismantleRequiredWork),
            dismantleCompletedWork = Mathf.Clamp(
                dismantleCompletedWork,
                0f,
                Mathf.Max(0.1f, dismantleRequiredWork)),
            reinstallRequiredWork = Mathf.Max(0.1f, reinstallRequiredWork),
            reinstallCompletedWork = Mathf.Clamp(
                reinstallCompletedWork,
                0f,
                Mathf.Max(0.1f, reinstallRequiredWork)),
            phase = phase,
            packageConsumed = packageConsumed
        };
    }
}

public static class FacilityEvolutionProgression
{
    public static float GetRequiredMastery(int generation)
    {
        return 120f + 60f * Mathf.Max(0, generation);
    }

    public static float GetModificationWork(
        float baseConstructionWork,
        int generation)
    {
        int normalizedGeneration = Mathf.Max(0, generation);
        return Mathf.Max(1f, baseConstructionWork)
            * (0.5f + 0.15f * Mathf.Sqrt(normalizedGeneration + 1f));
    }

    public static float GetRecalibrationWork(
        float baseConstructionWork,
        int generation)
    {
        return GetModificationWork(baseConstructionWork, generation) * 0.75f;
    }

    public static float GetRelocationDismantleWork(float baseConstructionWork)
    {
        return Mathf.Max(1f, baseConstructionWork) * 0.25f;
    }

    public static float GetRelocationReinstallWork(float baseConstructionWork)
    {
        return Mathf.Max(1f, baseConstructionWork) * 0.5f;
    }
}
