using System;
using System.Collections.Generic;
using System.Linq;

public enum FestivalExecutionChange { Scheduled, Declined, Advanced }

public readonly struct FestivalAttendanceProgress
{
    public FestivalAttendanceProgress(string characterId, float attendedSeconds)
    { CharacterId = characterId; AttendedSeconds = attendedSeconds; }
    public string CharacterId { get; }
    public float AttendedSeconds { get; }
}

// Only progress values are copied; no live owner, asset, save DTO, or presentation cache escapes.
public sealed class FestivalExecutionProgressedFact
{
    public FestivalExecutionProgressedFact(FestivalExecutionOccurrenceSaveData state, string festivalName,
        long stateGeneration, FestivalExecutionChange change)
    {
        if (state == null) throw new ArgumentNullException(nameof(state));
        OccurrenceId = state.occurrenceId; FestivalName = festivalName;
        StateGeneration = stateGeneration; Change = change; Phase = state.phase;
        VenueFacilityInstanceId = state.venueFacilityInstanceId; VenueCapacity = state.venueCapacity;
        CompletedPreparationWork = state.completedPreparationWork; RequiredPreparationWork = state.requiredPreparationWork;
        DeadlineDay = state.deadlineAbsoluteDay; DeadlineHour = state.deadlineHour;
        PlannedDurationSeconds = state.plannedDurationSeconds; ElapsedFestivalSeconds = state.elapsedFestivalSeconds;
        InputOwnerActive = state.inputOwnerActive; MaterialReceiptPending = state.materialReceiptPending;
        LastStatus = state.lastStatus; ResultSummary = state.resultSummary;
        Attendance = Array.AsReadOnly(state.attendance.Select(value =>
            new FestivalAttendanceProgress(value.characterId, value.attendedSeconds)).ToArray());
    }
    public string OccurrenceId { get; }
    public string FestivalName { get; }
    public long StateGeneration { get; }
    public FestivalExecutionChange Change { get; }
    public FestivalExecutionPhase Phase { get; }
    public string VenueFacilityInstanceId { get; }
    public int VenueCapacity { get; }
    public float CompletedPreparationWork { get; }
    public float RequiredPreparationWork { get; }
    public int DeadlineDay { get; }
    public int DeadlineHour { get; }
    public float PlannedDurationSeconds { get; }
    public float ElapsedFestivalSeconds { get; }
    public bool InputOwnerActive { get; }
    public bool MaterialReceiptPending { get; }
    public string LastStatus { get; }
    public string ResultSummary { get; }
    public IReadOnlyList<FestivalAttendanceProgress> Attendance { get; }
}
