using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

// Frozen gameplay progress, independent of alert layout, severity, and the durable outcome ledger.
public readonly struct SeasonalWildlifeProgressedFact
{
    public SeasonalWildlifeProgressedFact(string occurrenceId, string eventName, string speciesName,
        SeasonalWildlifeArrivalQualification qualification, int exactCount, int spawnedCount,
        IEnumerable<Vector2Int?> positions, int deadlineDay, int absoluteDay, string failureReason)
    {
        OccurrenceId = occurrenceId; EventName = eventName; SpeciesName = speciesName;
        Qualification = qualification; ExactCount = exactCount; SpawnedCount = spawnedCount;
        Positions = Array.AsReadOnly((positions ?? throw new ArgumentNullException(nameof(positions))).ToArray());
        DeadlineDay = deadlineDay; AbsoluteDay = absoluteDay; FailureReason = failureReason;
    }
    public string OccurrenceId { get; }
    public string EventName { get; }
    public string SpeciesName { get; }
    public SeasonalWildlifeArrivalQualification Qualification { get; }
    public int ExactCount { get; }
    public int SpawnedCount { get; }
    public IReadOnlyList<Vector2Int?> Positions { get; }
    public int DeadlineDay { get; }
    public int AbsoluteDay { get; }
    public string FailureReason { get; }
}

public readonly struct SeasonalWildlifeExpiredFact
{
    public SeasonalWildlifeExpiredFact(string occurrenceId, string eventName,
        SeasonalWildlifeArrivalQualification qualification, int absoluteDay)
    { OccurrenceId = occurrenceId; EventName = eventName; Qualification = qualification; AbsoluteDay = absoluteDay; }
    public string OccurrenceId { get; }
    public string EventName { get; }
    public SeasonalWildlifeArrivalQualification Qualification { get; }
    public int AbsoluteDay { get; }
}

public readonly struct SeasonalCargoProgressedFact
{
    public SeasonalCargoProgressedFact(string occurrenceId, string eventName, string itemName,
        int exactQuantity, Vector2Int? position, int remainingQuantity, long unitMassGrams,
        int securedQuantity, int availableQuantity, int deadlineDay, int absoluteDay, string failureReason)
    {
        OccurrenceId = occurrenceId; EventName = eventName; ItemName = itemName;
        ExactQuantity = exactQuantity; Position = position; RemainingQuantity = remainingQuantity;
        UnitMassGrams = unitMassGrams; SecuredQuantity = securedQuantity; AvailableQuantity = availableQuantity;
        DeadlineDay = deadlineDay; AbsoluteDay = absoluteDay; FailureReason = failureReason;
    }
    public string OccurrenceId { get; }
    public string EventName { get; }
    public string ItemName { get; }
    public int ExactQuantity { get; }
    public Vector2Int? Position { get; }
    public int RemainingQuantity { get; }
    public long UnitMassGrams { get; }
    public int SecuredQuantity { get; }
    public int AvailableQuantity { get; }
    public int DeadlineDay { get; }
    public int AbsoluteDay { get; }
    public string FailureReason { get; }
}

public readonly struct SeasonalCargoExpiredFact
{
    public SeasonalCargoExpiredFact(string occurrenceId, string eventName, string itemName,
        int securedQuantity, int expiredQuantity, long expiredMassGrams, int absoluteDay)
    {
        OccurrenceId = occurrenceId; EventName = eventName; ItemName = itemName;
        SecuredQuantity = securedQuantity; ExpiredQuantity = expiredQuantity;
        ExpiredMassGrams = expiredMassGrams; AbsoluteDay = absoluteDay;
    }
    public string OccurrenceId { get; }
    public string EventName { get; }
    public string ItemName { get; }
    public int SecuredQuantity { get; }
    public int ExpiredQuantity { get; }
    public long ExpiredMassGrams { get; }
    public int AbsoluteDay { get; }
}
