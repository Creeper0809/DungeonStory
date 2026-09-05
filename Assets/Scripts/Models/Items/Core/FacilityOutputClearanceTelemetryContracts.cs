using System;
using System.Collections.Generic;

/// <summary>
/// One exact physical output publication observed at a FacilityBuffer.
/// Time is expressed in fixed-point game micro-hours (1/1,000,000 game hour),
/// never in frames or wall-clock time.
/// </summary>
public readonly struct FacilityOutputBatchPublishedObservation
{
    public FacilityOutputBatchPublishedObservation(
        string batchCommitId,
        string facilityId,
        long batchMassGrams,
        long publishedAtMicroGameHours)
    {
        BatchCommitId = RequireCanonical(batchCommitId, nameof(batchCommitId));
        FacilityId = RequireCanonical(facilityId, nameof(facilityId));
        if (batchMassGrams <= 0L)
            throw new ArgumentOutOfRangeException(nameof(batchMassGrams));
        if (publishedAtMicroGameHours < 0L)
        {
            throw new ArgumentOutOfRangeException(
                nameof(publishedAtMicroGameHours));
        }

        BatchMassGrams = batchMassGrams;
        PublishedAtMicroGameHours = publishedAtMicroGameHours;
    }

    public string BatchCommitId { get; }
    public string FacilityId { get; }
    public long BatchMassGrams { get; }
    public long PublishedAtMicroGameHours { get; }

    private static string RequireCanonical(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Facility output-clearance identity must be canonical.",
                parameter);
        }
        return value;
    }
}

/// <summary>
/// A committed physical pickup slice. Multiple slices may clear one batch;
/// completion is emitted only after their exact gram sum reaches the original
/// publication mass.
/// </summary>
public readonly struct FacilityOutputBatchPickupObservation
{
    public FacilityOutputBatchPickupObservation(
        string batchCommitId,
        string clearanceSliceId,
        long pickedMassGrams,
        long pickedAtMicroGameHours)
    {
        if (string.IsNullOrWhiteSpace(batchCommitId)
            || !string.Equals(
                batchCommitId,
                batchCommitId.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Facility output-clearance batch identity must be canonical.",
                nameof(batchCommitId));
        }
        if (string.IsNullOrWhiteSpace(clearanceSliceId)
            || !string.Equals(
                clearanceSliceId,
                clearanceSliceId.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Facility output-clearance slice identity must be canonical.",
                nameof(clearanceSliceId));
        }
        if (pickedMassGrams <= 0L)
            throw new ArgumentOutOfRangeException(nameof(pickedMassGrams));
        if (pickedAtMicroGameHours < 0L)
            throw new ArgumentOutOfRangeException(nameof(pickedAtMicroGameHours));

        BatchCommitId = batchCommitId;
        ClearanceSliceId = clearanceSliceId;
        PickedMassGrams = pickedMassGrams;
        PickedAtMicroGameHours = pickedAtMicroGameHours;
    }

    public string BatchCommitId { get; }
    public string ClearanceSliceId { get; }
    public long PickedMassGrams { get; }
    public long PickedAtMicroGameHours { get; }
}

public readonly struct FacilityOutputClearanceSampleSnapshot
{
    public FacilityOutputClearanceSampleSnapshot(
        string batchCommitId,
        string facilityId,
        long batchMassGrams,
        long publishedAtMicroGameHours,
        long clearedAtMicroGameHours)
    {
        if (string.IsNullOrEmpty(batchCommitId)
            || string.IsNullOrEmpty(facilityId)
            || batchMassGrams <= 0L
            || publishedAtMicroGameHours < 0L
            || clearedAtMicroGameHours < publishedAtMicroGameHours)
        {
            throw new ArgumentException(
                "Facility output-clearance sample is inconsistent.");
        }

        BatchCommitId = batchCommitId;
        FacilityId = facilityId;
        BatchMassGrams = batchMassGrams;
        PublishedAtMicroGameHours = publishedAtMicroGameHours;
        ClearedAtMicroGameHours = clearedAtMicroGameHours;
    }

    public string BatchCommitId { get; }
    public string FacilityId { get; }
    public long BatchMassGrams { get; }
    public long PublishedAtMicroGameHours { get; }
    public long ClearedAtMicroGameHours { get; }
    public long ClearanceMicroHours => checked(
        ClearedAtMicroGameHours - PublishedAtMicroGameHours);
    public long ClearanceMilliHours => ClearanceMicroHours == 0L
        ? 0L
        : checked(1L + ((ClearanceMicroHours - 1L) / 1_000L));
}

public readonly struct FacilityOutputClearanceTelemetrySnapshot
{
    public FacilityOutputClearanceTelemetrySnapshot(
        IReadOnlyList<FacilityOutputClearanceSampleSnapshot> completed,
        int activeBatchCount,
        int orphanPickupCount,
        int conflictingPublicationCount,
        int overPickupCount,
        int capacityExceededCount,
        int restoreInterruptionCount)
    {
        Completed = completed
            ?? Array.Empty<FacilityOutputClearanceSampleSnapshot>();
        ActiveBatchCount = activeBatchCount;
        OrphanPickupCount = orphanPickupCount;
        ConflictingPublicationCount = conflictingPublicationCount;
        OverPickupCount = overPickupCount;
        CapacityExceededCount = capacityExceededCount;
        RestoreInterruptionCount = restoreInterruptionCount;
    }

    public IReadOnlyList<FacilityOutputClearanceSampleSnapshot> Completed { get; }
    public int ActiveBatchCount { get; }
    public int OrphanPickupCount { get; }
    public int ConflictingPublicationCount { get; }
    public int OverPickupCount { get; }
    public int CapacityExceededCount { get; }
    public int RestoreInterruptionCount { get; }
    public bool IsClean => ActiveBatchCount == 0
        && OrphanPickupCount == 0
        && ConflictingPublicationCount == 0
        && OverPickupCount == 0
        && CapacityExceededCount == 0
        && RestoreInterruptionCount == 0;
}

/// <summary>
/// Diagnostics-only observation sink. It is not save authority and must never
/// decide whether a gameplay transaction succeeds.
/// </summary>
public interface IFacilityOutputClearanceTelemetrySink
{
    void RecordPublication(FacilityOutputBatchPublishedObservation observation);
    void RecordCommittedPickup(FacilityOutputBatchPickupObservation observation);
    void RecordPublicationRollback(string batchCommitId);
}

public interface IFacilityOutputClearanceTelemetryQuery
{
    FacilityOutputClearanceTelemetrySnapshot Capture();
}

/// <summary>
/// Explicit diagnostics-session authority. Gameplay never enables capture by
/// default, so endless play cannot accumulate measurement state.
/// </summary>
public interface IFacilityOutputClearanceTelemetryControl
{
    bool IsCaptureActive { get; }
    string ActiveScenarioId { get; }
    void BeginCapture(string scenarioId);
    FacilityOutputClearanceTelemetrySnapshot EndCapture();
}
