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
        int repeatCount = 1,
        GameplayNarrativeEventContext narrativeContext = null);
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
        int repeatCount = 1,
        GameplayNarrativeEventContext narrativeContext = null);
    bool TryCommitPresentation(
        string equipmentInstanceId,
        string presentationId,
        string displayName,
        string narrativeFlavor,
        out string failureReason);
    bool TryFreezePresentationContext(
        string equipmentInstanceId,
        string presentationId,
        NarrativePublicContextMaterial material,
        out EquipmentEvolutionPresentationRequest frozenRequest,
        out string failureReason);
    bool TryCommitModuleSelection(
        string equipmentInstanceId,
        EquipmentEvolutionModuleSelectionResponseDto response,
        out string failureReason);
    bool TryRegisterPresentationFailure(
        string equipmentInstanceId,
        string presentationId,
        string reason,
        out bool awaitingNarrativeRetry);
    bool TryResumePresentation(
        string equipmentInstanceId,
        string presentationId);
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
    public static string BuildCatalyst(string family, int progressionLevel) =>
        EvolutionCatalystItemIdentity.BuildCatalyst(
            family,
            progressionLevel);

    public static string BuildResidue(int progressionLevel) =>
        EvolutionCatalystItemIdentity.BuildResidue(progressionLevel);

    public static bool TryParseCatalyst(
        string itemId,
        out EquipmentCatalystDefinition definition)
    {
        definition = null;
        if (!EvolutionCatalystItemIdentity.TryResolveCatalyst(
                itemId,
                out string family,
                out int progressionLevel,
                out int potency))
        {
            return false;
        }
        definition = new EquipmentCatalystDefinition
        {
            itemId = itemId?.Trim() ?? string.Empty,
            family = family,
            progressionLevel = progressionLevel,
            potency = potency,
            sourceTags = new List<string> { family }
        };
        return true;
    }

    public static bool TryParseResidue(
        string itemId,
        out int progressionLevel) =>
        EvolutionCatalystItemIdentity.TryResolveResidue(
            itemId,
            out progressionLevel,
            out _);
}
