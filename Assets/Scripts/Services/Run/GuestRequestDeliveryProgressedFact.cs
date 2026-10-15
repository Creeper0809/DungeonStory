using System;
using System.Linq;

public readonly struct GuestRequestDeliveryFinalizedFact
{
    public GuestRequestDeliveryFinalizedFact(string instanceId) => InstanceId = instanceId;
    public string InstanceId { get; }
}

public sealed class GuestRequestDeliveryProgressedFact
{
    private readonly GuestRequestDeliverySnapshot progress;
    public GuestRequestDeliveryProgressedFact(GuestRequestDeliverySnapshot progress,
        string requestName, string requestDescription, ExperienceEventRiskTier riskTier,
        string riskReason, int deadlineDay)
    {
        this.progress = Copy(progress ?? throw new ArgumentNullException(nameof(progress)));
        RequestName = requestName; RequestDescription = requestDescription; RiskTier = riskTier;
        RiskReason = riskReason; DeadlineDay = deadlineDay;
    }
    public string RequestName { get; }
    public string RequestDescription { get; }
    public ExperienceEventRiskTier RiskTier { get; }
    public string RiskReason { get; }
    public int DeadlineDay { get; }
    public GuestRequestDeliverySnapshot Snapshot() => Copy(progress);

    private static GuestRequestDeliverySnapshot Copy(GuestRequestDeliverySnapshot value) => new()
    {
        InstanceId = value.InstanceId, DefinitionId = value.DefinitionId, Phase = value.Phase,
        VenueFacilityInstanceId = value.VenueFacilityInstanceId, VenueDisplayName = value.VenueDisplayName,
        VenueAccessCell = value.VenueAccessCell, VenueCapacitySummary = value.VenueCapacitySummary,
        DestinationId = value.DestinationId, Destination = value.Destination,
        OperationId = value.OperationId, CommitId = value.CommitId, RequestFingerprint = value.RequestFingerprint,
        TerminalResolutionId = value.TerminalResolutionId, StatusReason = value.StatusReason,
        Items = Array.AsReadOnly(value.Items.ToArray())
    };
}
