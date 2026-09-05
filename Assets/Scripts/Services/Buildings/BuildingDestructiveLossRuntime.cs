using System;

public enum BuildingDestructiveLossDisposition
{
    DeferredAccepted = 0,
    Conflict = 1,
    RemovedAwaitingCheckpointGc = 2,
    CommittedWithNotificationFailure = 3
}

public readonly struct BuildingDestructiveLossResult
{
    public BuildingDestructiveLossResult(
        BuildingDestructiveLossDisposition disposition,
        string failureReason)
    {
        Disposition = disposition;
        FailureReason = failureReason ?? string.Empty;
    }

    public BuildingDestructiveLossDisposition Disposition { get; }
    public string FailureReason { get; }
    public bool Removed => Disposition is
        BuildingDestructiveLossDisposition.RemovedAwaitingCheckpointGc
        or BuildingDestructiveLossDisposition.CommittedWithNotificationFailure;
    public bool Accepted => Disposition != BuildingDestructiveLossDisposition.Conflict;
}

public interface IBuildingDestructiveLossRuntime
{
    BuildingDestructiveLossResult Apply(
        BuildableObject building,
        ProductionFacilityDestructiveDrainCause cause);
}

/// <summary>
/// Typed building-domain facade over the single durable production drain and
/// forward world-removal state machine.
/// </summary>
public sealed class BuildingDestructiveLossRuntime :
    IBuildingDestructiveLossRuntime
{
    private readonly IProductionFacilityDestructiveDrainRecoveryRuntime drains;

    public BuildingDestructiveLossRuntime(
        IProductionFacilityDestructiveDrainRecoveryRuntime drains)
    {
        this.drains = drains ?? throw new ArgumentNullException(nameof(drains));
    }

    public BuildingDestructiveLossResult Apply(
        BuildableObject building,
        ProductionFacilityDestructiveDrainCause cause)
    {
        ProductionFacilityDestructiveRemovalResult result =
            drains.RequestAndDrive(building, cause);
        return new BuildingDestructiveLossResult(
            result.Status switch
            {
                ProductionFacilityDestructiveRemovalStatus.Conflict =>
                    BuildingDestructiveLossDisposition.Conflict,
                ProductionFacilityDestructiveRemovalStatus.DeferredAccepted =>
                    BuildingDestructiveLossDisposition.DeferredAccepted,
                ProductionFacilityDestructiveRemovalStatus
                    .RemovedWithNotificationFailure =>
                    BuildingDestructiveLossDisposition
                        .CommittedWithNotificationFailure,
                _ => BuildingDestructiveLossDisposition
                    .RemovedAwaitingCheckpointGc
            },
            result.FailureReason);
    }
}
