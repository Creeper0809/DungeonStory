using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CircusLethalityPolicy
{
    StopWhenDowned,
    AllowAccidents,
    FightToDeath,
    ExecuteDesignatedTarget
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
    public int nextSupplyOperationSequence = 1;
    public CircusShowSupplyCommitPhase pendingSupplyPhase;
    public int pendingSupplyOperationSequence;
    public string pendingSupplyOperationId = string.Empty;
    public string pendingSupplyReasonCode = string.Empty;
    public string pendingSupplyCommitId = string.Empty;
    public List<string> pendingSupplySourceStackIds = new List<string>();
    public int pendingSupplyQuantity;
    public long pendingSupplyMassGrams;
    public string pendingSupplyCartStackId = string.Empty;
    public float pendingSupplyCartDurabilityBefore;
    public float pendingSupplyCartDurabilityAfter;
    public bool preparationSuppliesCommitted;
    public string preparationSupplyCommitId = string.Empty;

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
        clone.pendingSupplySourceStackIds = new List<string>(
            pendingSupplySourceStackIds ?? new List<string>());
        return clone;
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CircusSaveData
{
    public const int CurrentVersion = 4;
    public int version = CurrentVersion;
    public int nextOrderSequence;
    public List<CircusShowOrder> orders = new List<CircusShowOrder>();
    public List<CapturedWildlifeState> capturedWildlife =
        new List<CapturedWildlifeState>();
}

public enum CircusShowSupplyCommitPhase
{
    None = 0,
    ItemCommitted = 1,
    OutcomesPublished = 2
}

/// <summary>
/// Cross-assembly physical contract for one circus preparation commit. These
/// are validation constants, not authored balance writers; the corresponding
/// item assets remain the mass authority and focused tests detect drift.
/// </summary>
public static class CircusPerformanceSupplyContracts
{
    public const string PerformancePropBoxItemId =
        "supply:performance-prop-box";
    public const string BanquetCartItemId = "tool:banquet-cart";
    public const long PerformancePropBoxMassGrams = 1_950L;
    public const long BanquetCartMassGrams = 3_150L;
    public const double BanquetCartWearPerShow = 4d;
}

public enum CapturedWildlifeFeedCommitPhase
{
    None = 0,
    ItemCommitted = 1,
    CarePublished = 2
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
    public bool isTamed;
    public Vector2Int escapeDestination;
    public float nextCareAt;
    [Range(0f, 100f)] public float escapeRisk;
    public bool foodDeliveryPending;
    public bool waterDeliveryPending;
    public string lastFeedItemId = string.Empty;
    [Range(0f, 100f)] public float feedSicknessSeverity;
    [Range(0f, 1f)] public float lastFeedDiseaseChance;
    public string lastCareStatus = string.Empty;
    public int nextFeedOperationSequence;
    public int pendingFeedOperationSequence;
    public CapturedWildlifeFeedCommitPhase pendingFeedPhase;
    public string pendingFeedOperationId = string.Empty;
    public string pendingFeedReasonCode = string.Empty;
    public string pendingFeedCommitId = string.Empty;
    public List<string> pendingFeedSourceStackIds = new List<string>();
    public int pendingFeedQuantity;
    public long pendingFeedMassGrams;
    public string pendingFeedItemId = string.Empty;
    [Range(0f, 1f)] public float pendingFeedNutrition;
    [Range(0f, 1f)] public float pendingFeedDiseaseChance;
    public bool pendingFeedDiseaseTriggered;
    [Range(0f, 1f)] public float pendingFeedHungerTarget;
    [Min(0)] public int pendingFeedHealthTarget;
    [Range(0f, 100f)] public float pendingFeedSicknessTarget;

    public CapturedWildlifeState Clone()
    {
        CapturedWildlifeState clone =
            (CapturedWildlifeState)MemberwiseClone();
        clone.pendingFeedSourceStackIds = new List<string>(
            pendingFeedSourceStackIds ?? new List<string>());
        return clone;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct CircusProgramForecast
{
    public CircusProgramForecast(
        int expectedRevenue,
        float minimumSatisfaction,
        float maximumSatisfaction,
        float accidentChance,
        float renown,
        float dread,
        float hostileRumor,
        float injuryChance,
        float deathChance,
        bool canSchedule,
        string participantRequirement,
        string failureReason)
    {
        ExpectedRevenue = Mathf.Max(0, expectedRevenue);
        MinimumSatisfaction = Mathf.Clamp(minimumSatisfaction, 0f, 100f);
        MaximumSatisfaction = Mathf.Clamp(maximumSatisfaction, 0f, 100f);
        AccidentChance = Mathf.Clamp01(accidentChance);
        Renown = Mathf.Max(0f, renown);
        Dread = Mathf.Max(0f, dread);
        HostileRumor = Mathf.Max(0f, hostileRumor);
        InjuryChance = Mathf.Clamp01(injuryChance);
        DeathChance = Mathf.Clamp01(deathChance);
        CanSchedule = canSchedule;
        ParticipantRequirement = participantRequirement ?? string.Empty;
        FailureReason = failureReason ?? string.Empty;
    }

    public int ExpectedRevenue { get; }
    public float MinimumSatisfaction { get; }
    public float MaximumSatisfaction { get; }
    public float AccidentChance { get; }
    public float Renown { get; }
    public float Dread { get; }
    public float HostileRumor { get; }
    public float InjuryChance { get; }
    public float DeathChance { get; }
    public bool CanSchedule { get; }
    public string ParticipantRequirement { get; }
    public string FailureReason { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
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
