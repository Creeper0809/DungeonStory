using System;

/// <summary>
/// Reads only the aggregate-root journal state so owner-creation gates do not
/// create a journal -> registry -> participant -> runtime dependency cycle.
/// </summary>
public sealed class ProductionFacilityDestructiveDrainOpenOperationQuery :
    IProductionFacilityDestructiveDrainOpenOperationQuery
{
    private readonly DungeonRuntimeAggregateRootStore roots;

    public ProductionFacilityDestructiveDrainOpenOperationQuery(
        DungeonRuntimeAggregateRootStore roots)
    {
        this.roots = roots ?? throw new ArgumentNullException(nameof(roots));
    }

    public int Revision => State.Version;

    public bool IsOpen(BuildingInstanceId facilityId) =>
        TryCapture(facilityId, out _);

    public bool TryCapture(
        BuildingInstanceId facilityId,
        out ProductionFacilityDestructiveDrainOpenOperationSnapshot snapshot)
    {
        snapshot = default;
        if (!facilityId.IsValid)
            return false;

        ProductionFacilityDestructiveDrainOperationId operationId =
            ProductionFacilityDestructiveDrainOperationId.FromFacility(
                facilityId);
        if (!State.Entries.TryGetValue(
                operationId.Value,
                out ProductionFacilityDestructiveDrainEntrySaveData entry)
            || entry == null
            || !string.Equals(
                entry.facilityId,
                facilityId.Value,
                StringComparison.Ordinal)
            || entry.revision <= 0L
            || !Enum.IsDefined(
                typeof(ProductionFacilityDestructiveDrainPhase),
                entry.phase)
            || entry.phase == ProductionFacilityDestructiveDrainPhase.None)
        {
            return false;
        }

        snapshot = new ProductionFacilityDestructiveDrainOpenOperationSnapshot(
            operationId,
            facilityId,
            entry.phase,
            entry.revision);
        return true;
    }

    private ProductionFacilityDestructiveDrainAggregateState State =>
        roots.GetOrCreate(
            () => new ProductionFacilityDestructiveDrainAggregateState());
}
