using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class DefenseFacilityNetworkSnapshot
{
    public DefenseFacilityNetworkSnapshot(
        DefenseFacility facility,
        DefenseFacility detector,
        DefenseFacility controlDesk,
        DefenseFacility supplyDepot,
        DefenseFacility maintenanceBench,
        bool powered)
    {
        Facility = facility;
        Detector = detector;
        ControlDesk = controlDesk;
        SupplyDepot = supplyDepot;
        MaintenanceBench = maintenanceBench;
        Powered = powered;
    }

    public DefenseFacility Facility { get; }
    public DefenseFacility Detector { get; }
    public DefenseFacility ControlDesk { get; }
    public DefenseFacility SupplyDepot { get; }
    public DefenseFacility MaintenanceBench { get; }
    public bool Powered { get; }
    public bool HasDetectionLink => Detector != null;
    public bool HasControlLink => ControlDesk != null;
    public bool HasSupplyLink => SupplyDepot != null;
    public bool HasMaintenanceLink => MaintenanceBench != null;
}

public interface IDefenseFacilityNetworkRuntime
{
    int Version { get; }
    DefenseFacilityNetworkSnapshot GetSnapshot(
        DefenseFacility facility);
    bool HasAutomaticControl(DefenseFacility facility);
    bool HasMaintenanceCoverage(DefenseFacility facility);
    bool HasSupplyCoverage(DefenseFacility facility);
    void DetectIntruder(
        string raidId,
        Vector2Int position,
        IDefenseRaidAwarenessRuntime awareness);
}

public sealed class DefenseFacilityNetworkRuntime :
    IDefenseFacilityNetworkRuntime
{
    private const int DefaultSupplyRange = 5;
    private const int DefaultMaintenanceRange = 4;
    private const string DetectionFamily = "defense:detection";
    private const string ControlFamily = "defense:control";
    private const string SupplyFamily = "defense:supply";
    private const string MaintenanceFamily = "defense:maintenance";

    private readonly IBuildingWorldQuery buildings;
    private readonly IPowerInfrastructureQuery power;
    private readonly List<DefenseFacility> facilities =
        new List<DefenseFacility>();
    private int cachedBuildingVersion = int.MinValue;
    private int version;

    public DefenseFacilityNetworkRuntime(
        IBuildingWorldQuery buildings,
        IPowerInfrastructureQuery power)
    {
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.power = power;
    }

    public int Version
    {
        get
        {
            RebuildIfNeeded();
            return version;
        }
    }

    public DefenseFacilityNetworkSnapshot GetSnapshot(
        DefenseFacility facility)
    {
        RebuildIfNeeded();
        if (facility == null || facility.isDestroy)
        {
            return new DefenseFacilityNetworkSnapshot(
                facility,
                null,
                null,
                null,
                null,
                false);
        }

        DefenseFacility detector = FindNearestLinked(
            facility,
            DetectionFamily,
            ResolveRange,
            requirePowered: false);
        DefenseFacility control = FindNearestLinked(
            facility,
            ControlFamily,
            ResolveRange,
            requirePowered: true);
        DefenseFacility supply = FindNearestLinked(
            facility,
            SupplyFamily,
            _ => DefaultSupplyRange,
            requirePowered: false);
        DefenseFacility maintenance = FindNearestLinked(
            facility,
            MaintenanceFamily,
            _ => DefaultMaintenanceRange,
            requirePowered: false);
        return new DefenseFacilityNetworkSnapshot(
            facility,
            detector,
            control,
            supply,
            maintenance,
            IsPowered(facility));
    }

    public bool HasAutomaticControl(DefenseFacility facility)
    {
        return facility != null
            && (facility.BuildingData?.id != 1805
                || GetSnapshot(facility).HasControlLink);
    }

    public bool HasMaintenanceCoverage(DefenseFacility facility)
    {
        return GetSnapshot(facility).HasMaintenanceLink;
    }

    public bool HasSupplyCoverage(DefenseFacility facility)
    {
        return GetSnapshot(facility).HasSupplyLink;
    }

    public void DetectIntruder(
        string raidId,
        Vector2Int position,
        IDefenseRaidAwarenessRuntime awareness)
    {
        if (awareness == null)
        {
            return;
        }

        RebuildIfNeeded();
        foreach (DefenseFacility detector in facilities
                     .Where(value => IsFamily(
                         value,
                         DetectionFamily))
                     .OrderBy(value => value.centerPos.y)
                     .ThenBy(value => value.centerPos.x)
                     .ThenBy(value => value.BuildingData?.id ?? 0))
        {
            int range = ResolveRange(detector);
            if (Manhattan(detector.centerPos, position) > range
                || !IsPowered(detector))
            {
                continue;
            }

            awareness.IdentifyOperation(raidId, 2);
            DefenseFacilityNetworkSnapshot snapshot =
                GetSnapshot(detector);
            if (snapshot.HasControlLink)
            {
                awareness.IdentifyOperation(raidId, 3);
            }
        }
    }

    private DefenseFacility FindNearestLinked(
        DefenseFacility target,
        string family,
        Func<DefenseFacility, int> rangeResolver,
        bool requirePowered)
    {
        return facilities
            .Where(candidate => candidate != null
                && candidate != target
                && !candidate.isDestroy
                && IsFamily(candidate, family)
                && (!requirePowered || IsPowered(candidate))
                && Manhattan(
                    candidate.centerPos,
                    target.centerPos)
                    <= Mathf.Max(1, rangeResolver(candidate)))
            .OrderBy(candidate => Manhattan(
                candidate.centerPos,
                target.centerPos))
            .ThenBy(candidate => candidate.BuildingData?.id ?? 0)
            .ThenBy(candidate => candidate.centerPos.y)
            .ThenBy(candidate => candidate.centerPos.x)
            .FirstOrDefault();
    }

    private void RebuildIfNeeded()
    {
        if (cachedBuildingVersion == buildings.BuildingVersion)
        {
            return;
        }

        cachedBuildingVersion = buildings.BuildingVersion;
        facilities.Clear();
        facilities.AddRange(buildings.Buildings
            .OfType<DefenseFacility>()
            .Where(value => value != null && !value.isDestroy));
        version++;
    }

    private bool IsPowered(DefenseFacility facility)
    {
        return facility?.Defense?.requiresPower != true
            || power?.IsPowered(facility) == true;
    }

    private static int ResolveRange(DefenseFacility facility)
    {
        return Mathf.Max(1, facility?.Defense?.range ?? 1);
    }

    private static bool IsFamily(
        DefenseFacility facility,
        string family)
    {
        return string.Equals(
            facility?.Defense?.facilityFamilyId,
            family,
            StringComparison.Ordinal);
    }

    private static int Manhattan(
        Vector2Int left,
        Vector2Int right)
    {
        return Mathf.Abs(left.x - right.x)
            + Mathf.Abs(left.y - right.y);
    }
}
