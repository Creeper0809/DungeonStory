using System.Collections.Generic;


public interface ISeasonalWildlifeArrivalCommand
{
    bool TryRecordSeasonalWildlifeArrivalPlan(
        string occurrenceInstanceId,
        IReadOnlyList<SeasonalWildlifeArrivalMemberSaveData> members,
        out string failureReason);
    bool TryRecordSeasonalWildlifeArrivalFailure(
        string occurrenceInstanceId,
        string failureReason);
    bool TryMarkSeasonalWildlifeArrivalSpawned(
        string occurrenceInstanceId,
        string wildlifeId,
        out string failureReason);
    bool TryObserveSeasonalWildlifeArrival(
        string occurrenceInstanceId,
        string speciesDisplayName,
        int absoluteDay,
        out string failureReason);
}
