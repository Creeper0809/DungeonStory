using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class EquipmentEvolutionSaveData
{
    public List<EvolutionReforgeOrder> reforgeOrders =
        new List<EvolutionReforgeOrder>();
    public List<EquipmentReattunementOrder> reattunementOrders =
        new List<EquipmentReattunementOrder>();
}

internal sealed class EquipmentEvolutionAggregateState
{
    internal List<EvolutionReforgeOrder> ReforgeOrders { get; } = new();
    internal List<EquipmentReattunementOrder> ReattunementOrders { get; } = new();

    internal EquipmentEvolutionAggregateState DeepClone()
    {
        EquipmentEvolutionAggregateState clone = new();
        clone.ReforgeOrders.AddRange(ReforgeOrders.Select(order => order.Clone()));
        clone.ReattunementOrders.AddRange(
            ReattunementOrders.Select(order => order.Clone()));
        return clone;
    }
}

public sealed class EquipmentEvolutionRestoreCandidate
{
    internal EquipmentEvolutionRestoreCandidate(
        EquipmentEvolutionAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    internal EquipmentEvolutionAggregateState State { get; }
}

public interface IEquipmentEvolutionPersistence
{
    EquipmentEvolutionSaveData Capture();
    EquipmentEvolutionRestoreCandidate BuildRestoreCandidate(
        EquipmentEvolutionSaveData saveData);
    void PublishRestoreCandidate(
        EquipmentEvolutionRestoreCandidate candidate);
}

public interface IEquipmentEvolutionRuntime : IEquipmentEvolutionPersistence
{
    IReadOnlyList<EvolutionReforgeOrder> ReforgeOrders { get; }
    IReadOnlyList<EquipmentReattunementOrder> ReattunementOrders { get; }
    EquipmentEvolutionState GetState(string equipmentInstanceId);
    EquipmentEvolutionState RecordUsage(
        string equipmentInstanceId,
        string eventId,
        float mastery,
        float amount,
        string ownerPersistentId,
        int attunementPoints,
        IEnumerable<string> sourceTags = null,
        HistoricalEvidenceKind historicalEvidenceKind = HistoricalEvidenceKind.None,
        string outcomeId = "",
        int repeatCount = 1);
    bool TryRecordUsage(
        string equipmentInstanceId,
        string eventId,
        float mastery,
        float amount,
        string ownerPersistentId,
        int attunementPoints,
        IEnumerable<string> sourceTags = null,
        HistoricalEvidenceKind historicalEvidenceKind = HistoricalEvidenceKind.None,
        string outcomeId = "",
        int repeatCount = 1);
    EquipmentReforgePreview GetPreview(string equipmentInstanceId);
    bool TryGetActiveReforge(
        BuildableObject craftingFacility,
        out EvolutionReforgeOrder order);
    bool TryGetActiveReattunement(
        BuildableObject craftingFacility,
        out EquipmentReattunementOrder order);
    bool TryQueueReforge(
        string equipmentInstanceId,
        BuildableObject craftingFacility,
        string catalystItemId,
        string stabilizerItemId,
        out EvolutionReforgeOrder order,
        out string failureReason);
    bool TryConfigurePrecision(
        string orderId,
        ReforgePrecisionSelection selection,
        int goldCost,
        out string failureReason);
    bool ApplyReforgeWork(
        string orderId,
        float workUnits,
        out EvolutionNode completedNode,
        out string failureReason);
    bool ApplyReattunementWork(
        string orderId,
        float workUnits,
        out bool completed,
        out string failureReason);
    bool CancelReforge(string orderId, out string failureReason);
}

public interface IAttunementRuntime
{
    IReadOnlyList<EquipmentReattunementOrder> ReattunementOrders { get; }
    int GetAffinityScore(string equipmentInstanceId, string ownerPersistentId);
    bool TryGetActiveReattunement(
        BuildableObject craftingFacility,
        out EquipmentReattunementOrder order);
    bool TryQueueReattunement(
        string equipmentInstanceId,
        BuildableObject craftingFacility,
        string nodeId,
        bool active,
        string catalystItemId,
        out EquipmentReattunementOrder order,
        out string failureReason);
    bool ApplyReattunementWork(
        string orderId,
        float workUnits,
        out bool completed,
        out string failureReason);
    bool CancelReattunement(
        string orderId,
        out string failureReason);
}

public static class EvolutionCatalystItemId
{
    private const string CatalystPrefix = "evolution:catalyst:";
    private const string ResiduePrefix = "evolution:residue:";

    public static string BuildCatalyst(string family, int progressionLevel)
    {
        EvolutionCatalystProgression.RequireValid(progressionLevel);
        string normalized = NormalizeFamily(family);
        return $"{CatalystPrefix}{normalized}:{progressionLevel}";
    }

    public static string BuildResidue(int progressionLevel)
    {
        EvolutionCatalystProgression.RequireValid(progressionLevel);
        return $"{ResiduePrefix}{progressionLevel}";
    }

    public static bool TryParseCatalyst(
        string itemId,
        out EquipmentCatalystDefinition definition)
    {
        definition = null;
        string normalized = itemId?.Trim() ?? string.Empty;
        if (!normalized.StartsWith(CatalystPrefix, StringComparison.Ordinal))
        {
            return false;
        }

        string payload = normalized.Substring(CatalystPrefix.Length);
        int separator = payload.LastIndexOf(':');
        if (separator <= 0
            || !int.TryParse(
                payload.Substring(separator + 1),
                out int progressionLevel)
            || !EvolutionCatalystProgression.IsValid(progressionLevel))
        {
            return false;
        }

        string family = NormalizeFamily(payload.Substring(0, separator));
        definition = new EquipmentCatalystDefinition
        {
            itemId = normalized,
            family = family,
            progressionLevel = progressionLevel,
            potency = EvolutionCatalystProgression.GetPotencyGrade(
                progressionLevel),
            sourceTags = new List<string> { family }
        };
        return true;
    }

    public static bool TryParseResidue(
        string itemId,
        out int progressionLevel)
    {
        progressionLevel = 0;
        string normalized = itemId?.Trim() ?? string.Empty;
        return normalized.StartsWith(ResiduePrefix, StringComparison.Ordinal)
            && int.TryParse(
                normalized.Substring(ResiduePrefix.Length),
                out progressionLevel)
            && EvolutionCatalystProgression.IsValid(progressionLevel);
    }

    private static string NormalizeFamily(string family)
    {
        string normalized = family?.Trim().ToLowerInvariant() ?? string.Empty;
        if (normalized.StartsWith("catalyst:", StringComparison.Ordinal))
        {
            normalized = normalized.Substring("catalyst:".Length);
        }

        return string.IsNullOrWhiteSpace(normalized)
            ? "universal"
            : normalized.Replace(':', '-');
    }
}

public static class EvolutionCatalystProgression
{
    public const int MaximumLevel = 21;
    public const int MaximumPotencyGrade = 5;

    public static bool IsValid(int progressionLevel)
    {
        return progressionLevel >= 1 && progressionLevel <= MaximumLevel;
    }

    public static void RequireValid(int progressionLevel)
    {
        if (!IsValid(progressionLevel))
        {
            throw new ArgumentOutOfRangeException(
                nameof(progressionLevel),
                progressionLevel,
                $"Evolution catalyst progression must be 1-{MaximumLevel}.");
        }
    }

    public static int GetPotencyGrade(int progressionLevel)
    {
        RequireValid(progressionLevel);
        return 1
            + (progressionLevel - 1)
            * MaximumPotencyGrade
            / MaximumLevel;
    }
}
