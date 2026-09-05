using System;

public sealed class AutomationPowerDemandRegistry :
    IAutomationExecutionModeQuery
{
    private readonly AutomationStateSession stateSession;

    public AutomationPowerDemandRegistry(
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        stateSession = new AutomationStateSession(
            aggregateRootStore
            ?? throw new ArgumentNullException(nameof(aggregateRootStore)));
    }

    public int Version => stateSession.Version;

    public AutomationMode GetMode(string facilityId)
    {
        return !string.IsNullOrWhiteSpace(facilityId)
            && stateSession.TryGet(
                facilityId,
                out AutomationFacilityStateSession state)
                ? state.Mode
                : AutomationMode.Manual;
    }

    public AutomationMode GetMode(BuildingInstanceId facilityId)
    {
        if (!facilityId.IsValid)
        {
            throw new ArgumentException(
                "Automation execution mode requires a valid facility ID.",
                nameof(facilityId));
        }
        return GetMode(facilityId.Value);
    }

    public float ResolveDemand(
        string facilityId,
        AutomationPowerDemandProfile profile)
    {
        return AutomationPowerDemandRules.Resolve(
            GetMode(facilityId),
            profile);
    }
}
