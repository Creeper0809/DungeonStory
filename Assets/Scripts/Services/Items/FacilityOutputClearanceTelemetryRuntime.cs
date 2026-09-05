using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;

/// <summary>
/// Content-neutral raw observation ledger for output-buffer clearance. It
/// records exact batch grams and fixed game time, while profile aggregation
/// and support-topology attribution remain Economy audit responsibilities.
/// </summary>
public sealed class FacilityOutputClearanceTelemetryRuntime :
    IFacilityOutputClearanceTelemetrySink,
    IFacilityOutputClearanceTelemetryQuery,
    IFacilityOutputClearanceTelemetryControl,
    IDungeonSaveCaptureGuard,
    IDungeonSaveRestoreCompletedHook
{
    public const string Schema = "facility-output-clearance-telemetry@2";
    public const long MicroHoursPerGameHour = 1_000_000L;
    public const int MaximumActiveBatches = 4_096;
    public const int MaximumCompletedSamples = 16_384;

    private readonly Dictionary<string, ActiveBatch> active =
        new(StringComparer.Ordinal);
    private readonly List<FacilityOutputClearanceSampleSnapshot> completed = new();
    private readonly Dictionary<string, CompletedBatch> completedBatches =
        new(StringComparer.Ordinal);
    private int orphanPickupCount;
    private int conflictingPublicationCount;
    private int overPickupCount;
    private int capacityExceededCount;
    private int restoreInterruptionCount;
    private bool captureActive;
    private string activeScenarioId = string.Empty;

    public bool IsCaptureActive => captureActive;
    public string ActiveScenarioId => activeScenarioId;

    public void BeginCapture(string scenarioId)
    {
        if (captureActive)
        {
            throw new InvalidOperationException(
                "Facility output-clearance capture is already active: "
                + activeScenarioId);
        }
        if (string.IsNullOrWhiteSpace(scenarioId)
            || !string.Equals(
                scenarioId,
                scenarioId.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Facility output-clearance scenario ID must be canonical.",
                nameof(scenarioId));
        }

        Reset();
        activeScenarioId = scenarioId;
        captureActive = true;
    }

    public FacilityOutputClearanceTelemetrySnapshot EndCapture()
    {
        if (!captureActive)
        {
            throw new InvalidOperationException(
                "Facility output-clearance capture is not active.");
        }
        FacilityOutputClearanceTelemetrySnapshot result = Capture();
        captureActive = false;
        activeScenarioId = string.Empty;
        active.Clear();
        completedBatches.Clear();
        return result;
    }

    public static long CaptureMicroGameHours(IGameClock clock)
    {
        if (clock == null)
            throw new ArgumentNullException(nameof(clock));
        float time = clock.Time;
        if (float.IsNaN(time) || float.IsInfinity(time) || time < 0f)
        {
            throw new InvalidOperationException(
                "Facility output-clearance clock is non-finite or negative.");
        }

        // 7.5 real/game seconds represent one game hour. Converting through
        // decimal once at the capture boundary prevents frame-count coupling.
        decimal microHours = (decimal)time
            * GameCalendarRules.HoursPerDay
            * MicroHoursPerGameHour
            / (decimal)GameCalendarRules.SecondsPerDay;
        return checked((long)Math.Floor(microHours));
    }

    public void RecordPublication(
        FacilityOutputBatchPublishedObservation observation)
    {
        if (!captureActive)
            return;
        if (active.TryGetValue(
                observation.BatchCommitId,
                out ActiveBatch existing))
        {
            if (!existing.Matches(observation))
                conflictingPublicationCount++;
            return;
        }
        if (completedBatches.ContainsKey(observation.BatchCommitId))
        {
            conflictingPublicationCount++;
            return;
        }

        if (active.Count >= MaximumActiveBatches)
        {
            capacityExceededCount++;
            return;
        }
        active.Add(
            observation.BatchCommitId,
            new ActiveBatch(observation));
    }

    public void RecordCommittedPickup(
        FacilityOutputBatchPickupObservation observation)
    {
        if (!captureActive)
            return;
        if (!active.TryGetValue(
                observation.BatchCommitId,
                out ActiveBatch batch))
        {
            if (completedBatches.TryGetValue(
                    observation.BatchCommitId,
                    out CompletedBatch completedBatch))
            {
                if (observation.PickedAtMicroGameHours
                    < completedBatch.PublishedAtMicroGameHours)
                {
                    conflictingPublicationCount++;
                    return;
                }
                if (completedBatch.TryMatchSlice(
                        observation.ClearanceSliceId,
                        observation.PickedMassGrams,
                        out bool completedIdentityConflict))
                {
                    return;
                }
                if (completedIdentityConflict)
                    conflictingPublicationCount++;
                else
                    overPickupCount++;
                return;
            }
            orphanPickupCount++;
            return;
        }
        if (observation.PickedAtMicroGameHours
            < batch.PublishedAtMicroGameHours)
        {
            conflictingPublicationCount++;
            return;
        }

        if (batch.TryMatchSlice(
                observation.ClearanceSliceId,
                observation.PickedMassGrams,
                out bool identityConflict))
        {
            return;
        }
        if (identityConflict)
        {
            conflictingPublicationCount++;
            return;
        }

        long next = checked(batch.PickedMassGrams + observation.PickedMassGrams);
        if (next > batch.BatchMassGrams)
        {
            overPickupCount++;
            return;
        }
        batch.AddSlice(
            observation.ClearanceSliceId,
            observation.PickedMassGrams);
        batch.PickedMassGrams = next;
        if (next != batch.BatchMassGrams)
            return;

        if (completed.Count >= MaximumCompletedSamples)
        {
            capacityExceededCount++;
            active.Remove(batch.BatchCommitId);
            return;
        }
        completed.Add(new FacilityOutputClearanceSampleSnapshot(
            batch.BatchCommitId,
            batch.FacilityId,
            batch.BatchMassGrams,
            batch.PublishedAtMicroGameHours,
            observation.PickedAtMicroGameHours));
        completedBatches.Add(
            batch.BatchCommitId,
            new CompletedBatch(batch));
        active.Remove(batch.BatchCommitId);
    }

    public void RecordPublicationRollback(string batchCommitId)
    {
        if (!captureActive)
            return;
        if (string.IsNullOrWhiteSpace(batchCommitId)
            || !string.Equals(
                batchCommitId,
                batchCommitId.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "Facility output-clearance rollback identity must be canonical.",
                nameof(batchCommitId));
        }
        active.Remove(batchCommitId);
    }

    public FacilityOutputClearanceTelemetrySnapshot Capture()
    {
        FacilityOutputClearanceSampleSnapshot[] ordered = completed
            .OrderBy(value => value.FacilityId, StringComparer.Ordinal)
            .ThenBy(value => value.PublishedAtMicroGameHours)
            .ThenBy(value => value.BatchCommitId, StringComparer.Ordinal)
            .ToArray();
        return new FacilityOutputClearanceTelemetrySnapshot(
            Array.AsReadOnly(ordered),
            active.Count,
            orphanPickupCount,
            conflictingPublicationCount,
            overPickupCount,
            capacityExceededCount,
            restoreInterruptionCount);
    }

    public void ValidateBeforeCapture()
    {
        if (captureActive)
        {
            throw new InvalidOperationException(
                "Dungeon save capture is forbidden during facility output-clearance measurement: "
                + activeScenarioId);
        }
    }

    public void OnRestoreCompleted()
    {
        if (!captureActive)
            return;
        restoreInterruptionCount++;
        active.Clear();
    }

    private void Reset()
    {
        active.Clear();
        completed.Clear();
        completedBatches.Clear();
        orphanPickupCount = 0;
        conflictingPublicationCount = 0;
        overPickupCount = 0;
        capacityExceededCount = 0;
        restoreInterruptionCount = 0;
    }

    private sealed class ActiveBatch
    {
        internal ActiveBatch(FacilityOutputBatchPublishedObservation source)
        {
            BatchCommitId = source.BatchCommitId;
            FacilityId = source.FacilityId;
            BatchMassGrams = source.BatchMassGrams;
            PublishedAtMicroGameHours = source.PublishedAtMicroGameHours;
        }

        internal string BatchCommitId { get; }
        internal string FacilityId { get; }
        internal long BatchMassGrams { get; }
        internal long PublishedAtMicroGameHours { get; }
        internal long PickedMassGrams { get; set; }
        internal IReadOnlyDictionary<string, long> PickedSlices => pickedSlices;

        private readonly Dictionary<string, long> pickedSlices =
            new(StringComparer.Ordinal);

        internal bool TryMatchSlice(
            string sliceId,
            long massGrams,
            out bool identityConflict)
        {
            identityConflict = false;
            if (!pickedSlices.TryGetValue(sliceId, out long priorMass))
                return false;
            identityConflict = priorMass != massGrams;
            return !identityConflict;
        }

        internal void AddSlice(string sliceId, long massGrams) =>
            pickedSlices.Add(sliceId, massGrams);

        internal bool Matches(
            FacilityOutputBatchPublishedObservation observation) =>
            string.Equals(
                BatchCommitId,
                observation.BatchCommitId,
                StringComparison.Ordinal)
            && string.Equals(
                FacilityId,
                observation.FacilityId,
                StringComparison.Ordinal)
            && BatchMassGrams == observation.BatchMassGrams
            && PublishedAtMicroGameHours
                == observation.PublishedAtMicroGameHours;
    }

    private sealed class CompletedBatch
    {
        private readonly Dictionary<string, long> pickedSlices;

        internal CompletedBatch(ActiveBatch source)
        {
            PublishedAtMicroGameHours = source.PublishedAtMicroGameHours;
            pickedSlices = source.PickedSlices.ToDictionary(
                value => value.Key,
                value => value.Value,
                StringComparer.Ordinal);
        }

        internal long PublishedAtMicroGameHours { get; }

        internal bool TryMatchSlice(
            string sliceId,
            long massGrams,
            out bool identityConflict)
        {
            identityConflict = false;
            if (!pickedSlices.TryGetValue(sliceId, out long priorMass))
                return false;
            identityConflict = priorMass != massGrams;
            return !identityConflict;
        }
    }
}
