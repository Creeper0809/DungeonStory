using System;
using System.Linq;

// Post-commit domain observations; presentation selects its own queries and layout.
public readonly struct CampaignIncidentCapturedFact
{
    public CampaignIncidentCapturedFact(ObservedMealIncidentCaptureResult result, int absoluteDay)
    {
        if (result.Disposition is not ObservedMealIncidentCaptureDisposition.Created
            and not ObservedMealIncidentCaptureDisposition.ExactReplay)
            throw new ArgumentException("A captured incident fact requires a confirmed occurrence.", nameof(result));
        Disposition = result.Disposition; IncidentKind = result.IncidentKind;
        OccurrenceInstanceId = result.OccurrenceInstanceId; AbsoluteDay = absoluteDay;
    }
    public ObservedMealIncidentCaptureDisposition Disposition { get; }
    public ServiceIncidentKind IncidentKind { get; }
    public string OccurrenceInstanceId { get; }
    public int AbsoluteDay { get; }
}

public readonly struct CampaignDailyEvaluatedFact
{
    public CampaignDailyEvaluatedFact(int absoluteDay, bool succeeded)
    { AbsoluteDay = absoluteDay; Succeeded = succeeded; }
    public int AbsoluteDay { get; }
    public bool Succeeded { get; }
}

public sealed class EndlessCrisisEvaluatedFact
{
    public EndlessCrisisEvaluatedFact(int absoluteDay, EndlessCrisisSnapshot value)
    {
        if (value == null) throw new ArgumentNullException(nameof(value));
        AbsoluteDay = absoluteDay;
        Crisis = new EndlessCrisisSnapshot(value.Phase, value.InstanceId,
            Array.AsReadOnly(value.Axes.ToArray()), value.StartedAbsoluteDay, value.PressureEndAbsoluteDay,
            value.LastActualEndAbsoluteDay, value.RecoveryEndAbsoluteDay, value.LastEvaluationAbsoluteDay,
            value.AuthoredRecoveryDays, value.PendingInvasionSourceOwnerId, value.DirectResponseOwnerId);
    }
    public int AbsoluteDay { get; }
    // Axis elements have getter-only scalar/string properties.
    public EndlessCrisisSnapshot Crisis { get; }
}
