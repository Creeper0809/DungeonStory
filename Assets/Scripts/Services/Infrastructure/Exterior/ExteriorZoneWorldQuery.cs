using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Read-only projection of physical exterior-zone buildings. The exterior
/// activity coordinator owns incident behavior, while the building registry
/// remains the source of truth for which zone entities currently exist.
/// </summary>
public sealed class ExteriorZoneWorldQuery : IExteriorZoneQuery
{
    private readonly IBuildingWorldQuery buildings;
    private readonly IRestoreWorldCandidateQuery restoreCandidates;
    private int projectedBuildingVersion = int.MinValue;
    private IReadOnlyList<ExteriorZoneMarker> zones =
        Array.Empty<ExteriorZoneMarker>();

    public ExteriorZoneWorldQuery(
        IBuildingWorldQuery buildings,
        IRestoreWorldCandidateQuery restoreCandidates)
    {
        this.buildings = buildings
            ?? throw new ArgumentNullException(nameof(buildings));
        this.restoreCandidates = restoreCandidates
            ?? throw new ArgumentNullException(nameof(restoreCandidates));
    }

    public IReadOnlyList<ExteriorZoneMarker> Zones
    {
        get
        {
            RefreshProjection();
            return zones;
        }
    }

    public IEnumerable<ExteriorZoneMarker> GetZones(ExteriorZoneType zoneType)
    {
        return Zones.Where(zone => zone.ZoneType == zoneType);
    }

    public bool TryGetZone(
        ExteriorZoneType zoneType,
        out ExteriorZoneMarker marker)
    {
        marker = Zones.FirstOrDefault(zone => zone.ZoneType == zoneType);
        return marker != null;
    }

    public ExteriorActivityOverviewSnapshot GetOverview()
    {
        IReadOnlyList<ExteriorZoneMarker> activeZones = Zones;
        return new ExteriorActivityOverviewSnapshot(
            activeZones.Count,
            activeZones.Count(zone => zone.ZoneType == ExteriorZoneType.DropZone),
            activeZones.Count(zone => zone.HasActiveIncident),
            activeZones.Select(zone => zone.Cleanliness).DefaultIfEmpty(100f).Average(),
            activeZones.Select(zone => zone.Damage).DefaultIfEmpty(0f).Average(),
            activeZones.Select(zone => zone.PatrolReadiness).DefaultIfEmpty(0f).Average(),
            activeZones.Select(zone => zone.ReceptionReadiness).DefaultIfEmpty(0f).Average());
    }

    private void RefreshProjection()
    {
        if (restoreCandidates.TryGetExteriorZones(
                out IReadOnlyList<ExteriorZoneMarker> candidateZones))
        {
            projectedBuildingVersion = int.MinValue;
            zones = candidateZones ?? Array.Empty<ExteriorZoneMarker>();
            return;
        }

        int currentVersion = buildings.BuildingVersion;
        if (currentVersion == projectedBuildingVersion)
        {
            return;
        }

        zones = buildings.Buildings
            .OfType<ExteriorZoneMarker>()
            .Where(zone => zone != null)
            .OrderBy(zone => zone.ZoneType)
            .ThenBy(zone => zone.ZoneId, StringComparer.Ordinal)
            .ToArray();
        projectedBuildingVersion = currentVersion;
    }
}
