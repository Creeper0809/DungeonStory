using System;
using System.Collections.Generic;
using System.Linq;

internal sealed class InvasionFacilityTargetRuntimeProjection
{
    private readonly Dictionary<BuildingInstanceId, BuildableObject> targets;

    private InvasionFacilityTargetRuntimeProjection(
        IReadOnlyList<InvasionIntruderFacilityTargetSnapshot> snapshots,
        Dictionary<BuildingInstanceId, BuildableObject> targets)
    {
        Snapshots = snapshots
            ?? throw new ArgumentNullException(nameof(snapshots));
        this.targets = targets
            ?? throw new ArgumentNullException(nameof(targets));
    }

    public IReadOnlyList<InvasionIntruderFacilityTargetSnapshot> Snapshots { get; }

    public static InvasionFacilityTargetRuntimeProjection Capture(
        IEnumerable<BuildableObject> buildings,
        Func<BuildableObject, int> moveCost = null)
    {
        Dictionary<BuildingInstanceId, BuildableObject> targets = new();
        List<InvasionIntruderFacilityTargetSnapshot> snapshots = new();
        foreach (BuildableObject building in (buildings
                     ?? Enumerable.Empty<BuildableObject>())
                 .Where(candidate => candidate != null)
                 .Distinct())
        {
            BuildingInstanceId targetId = building.RequirePersistentInstanceId();
            if (targets.TryGetValue(targetId, out BuildableObject existing)
                && existing != building)
            {
                throw new InvalidOperationException(
                    $"Invasion target ID '{targetId}' resolves to multiple buildings.");
            }

            targets[targetId] = building;
            bool damageable = IsDamageableFacility(building);
            DefenseBreachTargetSnapshot breachTarget = new(
                targetId,
                building.buildPoses,
                building.isDestroy,
                damageable ? 1f : 0f,
                1f,
                0f,
                damageable);
            snapshots.Add(new InvasionIntruderFacilityTargetSnapshot(
                breachTarget,
                building.IsDamaged,
                building.IsGridMovement,
                building.Facility != null,
                building.BuildingData?.Defense?.IsDefenseFacility == true,
                building.GetConstructionValue(),
                moveCost != null ? moveCost(building) : int.MaxValue));
        }

        return new InvasionFacilityTargetRuntimeProjection(
            snapshots.AsReadOnly(),
            targets);
    }

    public bool TryResolve(
        BuildingInstanceId targetId,
        out BuildableObject target)
    {
        target = null;
        return targetId.IsValid
            && targets.TryGetValue(targetId, out target)
            && target != null;
    }

    internal static bool IsDamageableFacility(BuildableObject building)
    {
        return building != null
            && !building.isDestroy
            && !building.IsDamaged
            && !building.IsGridMovement
            && building.Facility != null;
    }
}
