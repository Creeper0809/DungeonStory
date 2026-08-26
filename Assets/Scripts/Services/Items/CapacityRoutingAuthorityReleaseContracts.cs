internal enum ExactAuthorityReleaseStatus
{
    Applied = 0,
    Replay = 1,
    Conflict = 2
}

public interface IProductionCapacityRoutingOperationAuthorityReleaseCoordinator
{
    [GameplayInternalOnly(
        "Synchronously quiesces and releases the complete canonical actor set so no saveable state can contain Loose cargo with live haul authority.",
        "Production capacity-routing destructive-drain participant only")]
    ProductionCapacityRoutingDrainResult TryQuiesceAndReleaseAllActors(
        string stepOperationId,
        string drainRequestFingerprint);
}
