using System;
using System.Collections.Generic;
using UnityEngine;

public enum CircusShowState
{
    Composition,
    ParticipantEscort,
    AudienceEntering,
    Performing,
    Settlement,
    CleanupAndTreatment,
    Completed,
    Cancelled
}

public enum CircusLethalityPolicy
{
    StopWhenDowned,
    AllowAccidents,
    FightToDeath,
    ExecuteDesignatedTarget
}

public enum CapturedWildlifeTransportState
{
    AwaitingTransport,
    Transporting,
    Penned,
    MovingToShow,
    Performing,
    ReturningToPen,
    Released,
    Escaped
}

[Serializable]
public sealed class CircusProgramModule
{
    public string programId = string.Empty;
    public string displayName = string.Empty;
    public bool requiresCaptive;
    public bool requiresWildlife;
    public bool usesCombat;
    public bool publiclyCruel;
    [Range(0f, 1f)] public float baseAccidentRisk;
    public float baseAudienceSatisfaction;
    public float basePerformerFame;
}

[Serializable]
public sealed class CircusShowOrder
{
    public string orderId = string.Empty;
    public string stageId = string.Empty;
    public Vector2Int stagePosition;
    public int roomId;
    public string programId = string.Empty;
    public CircusLethalityPolicy lethality;
    public CircusShowState state = CircusShowState.Composition;
    public List<string> performerIds = new List<string>();
    public List<string> wildlifeIds = new List<string>();
    public List<string> audienceIds = new List<string>();
    public List<Vector2Int> performerPositions = new List<Vector2Int>();
    public List<Vector2Int> wildlifePositions = new List<Vector2Int>();
    public List<Vector2Int> audiencePositions = new List<Vector2Int>();
    public float preparationWorkRequired;
    public float preparationWorkCompleted;
    public float elapsedShowSeconds;
    public float showDurationSeconds;
    public float nextCombatExchangeAt;
    public float phaseElapsedSeconds;
    public int ticketPrice;
    public int revenue;
    public float satisfaction;
    public float venueSatisfactionBonus;
    public float venueAccidentRiskBonus;
    public float venueAccidentDamageMultiplier = 1f;
    public float venueFilthMultiplier = 1f;
    public float venueWitnessMoodPenalty;
    public float venueGamblingVariance;
    public int venueFlatRevenuePerAudience;
    public bool accidentResolved;
    public string statusMessage = string.Empty;
    public bool cleanupRequired;
    public bool treatmentRequired;
    public bool betrayalCheckCompleted;

    public bool IsTerminal =>
        state is CircusShowState.Completed or CircusShowState.Cancelled;

    public CircusShowOrder Clone()
    {
        CircusShowOrder clone = (CircusShowOrder)MemberwiseClone();
        clone.performerIds = new List<string>(performerIds ?? new List<string>());
        clone.wildlifeIds = new List<string>(wildlifeIds ?? new List<string>());
        clone.audienceIds = new List<string>(audienceIds ?? new List<string>());
        clone.performerPositions = new List<Vector2Int>(
            performerPositions ?? new List<Vector2Int>());
        clone.wildlifePositions = new List<Vector2Int>(
            wildlifePositions ?? new List<Vector2Int>());
        clone.audiencePositions = new List<Vector2Int>(
            audiencePositions ?? new List<Vector2Int>());
        return clone;
    }
}

[Serializable]
public sealed class CircusSaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public int nextOrderSequence;
    public List<CircusShowOrder> orders = new List<CircusShowOrder>();
    public List<CapturedWildlifeState> capturedWildlife =
        new List<CapturedWildlifeState>();
}

[Serializable]
public sealed class CapturedWildlifeState
{
    public string wildlifeId = string.Empty;
    public string speciesId = string.Empty;
    public string penId = string.Empty;
    public Vector2Int penPosition;
    public Vector2Int capturePosition;
    public string reservedCarrierId = string.Empty;
    public string assignedShowOrderId = string.Empty;
    public CapturedWildlifeTransportState transportState =
        CapturedWildlifeTransportState.AwaitingTransport;
    public bool escaped;
    public Vector2Int escapeDestination;
    public float nextCareAt;
    [Range(0f, 100f)] public float escapeRisk;
    public bool foodDeliveryPending;
    public bool waterDeliveryPending;
    public string lastCareStatus = string.Empty;

    public CapturedWildlifeState Clone()
    {
        return (CapturedWildlifeState)MemberwiseClone();
    }
}

public readonly struct CircusProgramSettlement
{
    public CircusProgramSettlement(
        float satisfaction,
        float fame,
        bool cleanupRequired,
        bool treatmentRequired,
        string message)
    {
        Satisfaction = satisfaction;
        Fame = fame;
        CleanupRequired = cleanupRequired;
        TreatmentRequired = treatmentRequired;
        Message = message ?? string.Empty;
    }

    public float Satisfaction { get; }
    public float Fame { get; }
    public bool CleanupRequired { get; }
    public bool TreatmentRequired { get; }
    public string Message { get; }
}

public interface ICircusProgramHandler
{
    CircusProgramModule Definition { get; }
    bool Validate(
        CircusShowOrder order,
        IReadOnlyList<CaptiveState> performers,
        out string failureReason);
    CircusProgramSettlement Settle(
        CircusShowOrder order,
        IReadOnlyList<CaptiveState> performers);
}

public interface ICircusRuntime
{
    IReadOnlyList<CircusProgramModule> Programs { get; }
    IReadOnlyList<CircusShowOrder> Orders { get; }
    bool TrySchedule(
        BuildableObject stage,
        string programId,
        CircusLethalityPolicy lethality,
        IReadOnlyList<string> performerIds,
        IReadOnlyList<string> wildlifeIds,
        out CircusShowOrder order,
        out string failureReason);
    bool AdvancePreparation(
        string orderId,
        CharacterActor worker,
        float workAmount,
        out string status);
    bool Cancel(string orderId, string reason);
    CircusSaveData Capture();
    void Restore(CircusSaveData saveData, IList<string> warnings);
}

public interface IWildlifeCaptureRuntime
{
    bool IsCaptured(string wildlifeId);
    bool TryCapture(
        WildlifeActor wildlife,
        BuildableObject pen,
        out string failureReason);
    bool TryOrderCapture(
        WildlifeActor wildlife,
        CharacterActor carrier,
        BuildableObject pen,
        out string failureReason);
    bool TryGetCaptured(
        string wildlifeId,
        out CapturedWildlifeState state);
    bool TryRelease(string wildlifeId, out string failureReason);
    bool TryAssignToShow(string wildlifeId, string orderId, out string failureReason);
    void CompleteShowAssignment(string wildlifeId, string orderId);
    IReadOnlyList<CapturedWildlifeState> CapturedAnimals { get; }
    IReadOnlyList<CapturedWildlifeState> Capture();
    void Restore(IEnumerable<CapturedWildlifeState> states, IList<string> warnings);
}

public interface IWildlifeCaptureTransportRuntime
{
    bool TryGetTransportState(
        string wildlifeId,
        CharacterActor carrier,
        out CapturedWildlifeState state,
        out WildlifeActor wildlife,
        out string failureReason);
    IDisposable BeginTransportPass(CharacterActor carrier, string wildlifeId);
    bool TryBeginCarry(
        string wildlifeId,
        CharacterActor carrier,
        out string failureReason);
    bool TryCompleteCarry(
        string wildlifeId,
        CharacterActor carrier,
        out string failureReason);
    void FailCarry(string wildlifeId, CharacterActor carrier, string reason);
}
