using System;

public sealed class AutomationPowerDemandRegistry
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

    public float ResolveDemand(
        string facilityId,
        AutomationPowerDemandProfile profile)
    {
        return AutomationPowerDemandRules.Resolve(
            GetMode(facilityId),
            profile);
    }
}
