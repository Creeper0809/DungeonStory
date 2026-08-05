using System;
using System.Collections.Generic;
using System.Linq;

internal static class ExteriorActivitySaveValidation
{
    internal const int MaximumZones = 64;
    internal const int MaximumIncidentHistory = 32;

    public static void Validate(
        DungeonExteriorActivitySaveData payload,
        DungeonGameRestoreReport report,
        ExteriorIncidentHandlerRegistry handlers,
        IDungeonItemCatalogProvider itemCatalog)
    {
        if (report == null)
        {
            throw new ArgumentNullException(nameof(report));
        }

        if (payload == null)
        {
            report.AddError("Exterior activity payload is null.");
            return;
        }

        if (payload.version != DungeonExteriorActivitySaveData.CurrentVersion)
        {
            report.AddError(
                $"Unsupported exterior activity payload version {payload.version}; expected {DungeonExteriorActivitySaveData.CurrentVersion}.");
        }
        if (payload.nextIncidentSequence < 1)
        {
            report.AddError(
                "Exterior activity next incident sequence must be positive.");
        }
        if (payload.zones == null || payload.incidentStates == null)
        {
            report.AddError(
                "Exterior activity payload is missing a required state list.");
            return;
        }

        ValidateNamedStructure(payload, report);

        ValidateZones(payload.zones, report);
        ValidateIncidents(
            payload,
            report,
            handlers,
            itemCatalog);
    }

    private static void ValidateNamedStructure(
        DungeonExteriorActivitySaveData payload,
        DungeonGameRestoreReport report)
    {
        if (payload.zones.Any(zone => zone == null)
            || payload.incidentStates.Any(incident => incident == null))
        {
            return;
        }

        try
        {
            DungeonStory.Exterior.ExteriorActivityRestoreRules.Prepare(
                payload.nextIncidentSequence,
                payload.zones.Select(zone =>
                    new DungeonStory.Exterior.ExteriorZoneSnapshot(
                        new DungeonStory.Exterior.ExteriorZoneId(zone.zoneId),
                        (BuildingInstanceId)zone.buildingInstanceId,
                        (DungeonStory.Exterior.ExteriorZoneKind)zone.zoneType,
                        new DungeonStory.Exterior.ExteriorZoneAddress(
                            zone.gridX,
                            zone.gridY),
                        zone.cleanliness,
                        zone.damage,
                        zone.patrolReadiness,
                        zone.receptionReadiness,
                        zone.waitingVisitors,
                        zone.firstImpressionBonus,
                        zone.completedWorks)).ToArray(),
                payload.incidentStates.Select(incident =>
                    new DungeonStory.Exterior.ExteriorIncidentSnapshot(
                        incident.incidentId,
                        (DungeonStory.Exterior.ExteriorIncidentKind)incident.kind,
                        new DungeonStory.Exterior.ExteriorZoneId(incident.zoneId),
                        (DungeonStory.Exterior.ExteriorIncidentStage)incident.stage,
                        (DungeonStory.Exterior.ExteriorIncidentOutcome)incident.outcome,
                        incident.durationSeconds,
                        incident.remainingSeconds)).ToArray());
        }
        catch (InvalidOperationException exception)
        {
            report.AddError(exception.Message);
        }
    }

    private static void ValidateZones(
        IReadOnlyList<ExteriorZoneSaveData> zones,
        DungeonGameRestoreReport report)
    {
        if (zones.Count == 0 || zones.Count > MaximumZones)
        {
            report.AddError(
                $"Exterior activity requires 1-{MaximumZones} saved zones; received {zones.Count}.");
        }

        HashSet<string> zoneIds = new(StringComparer.Ordinal);
        HashSet<string> buildingIds = new(StringComparer.Ordinal);
        HashSet<string> placements = new(StringComparer.Ordinal);
        foreach (ExteriorZoneSaveData zone in zones)
        {
            string rawZoneId = zone?.zoneId;
            string rawBuildingId = zone?.buildingInstanceId;
            string zoneId = rawZoneId?.Trim() ?? string.Empty;
            string buildingId =
                rawBuildingId?.Trim() ?? string.Empty;
            if (zone == null
                || !string.Equals(rawZoneId, zoneId, StringComparison.Ordinal)
                || !string.Equals(
                    rawBuildingId,
                    buildingId,
                    StringComparison.Ordinal)
                || !Enum.IsDefined(typeof(ExteriorZoneType), zone.zoneType)
                || !zoneIds.Add(zoneId)
                || !buildingIds.Add(buildingId)
                || !((BuildingInstanceId)buildingId).IsValid
                || !string.Equals(
                    zoneId,
                    BuildZoneId(zone.zoneType, zone.gridX, zone.gridY),
                    StringComparison.Ordinal)
                || !placements.Add(
                    $"{zone.zoneType}:{zone.gridX}:{zone.gridY}"))
            {
                report.AddError(
                    $"Exterior activity contains an invalid or duplicate zone '{zoneId}'.");
                continue;
            }

            if (!IsFiniteInRange(zone.cleanliness, 0f, 100f)
                || !IsFiniteInRange(zone.damage, 0f, 100f)
                || !IsFiniteInRange(zone.patrolReadiness, 0f, 100f)
                || !IsFiniteInRange(zone.receptionReadiness, 0f, 100f)
                || !IsFiniteInRange(zone.firstImpressionBonus, 0f, 25f)
                || zone.waitingVisitors < 0
                || zone.completedWorks < 0)
            {
                report.AddError(
                    $"Exterior zone '{zoneId}' contains invalid condition values.");
            }
        }
    }

    private static void ValidateIncidents(
        DungeonExteriorActivitySaveData payload,
        DungeonGameRestoreReport report,
        ExteriorIncidentHandlerRegistry handlers,
        IDungeonItemCatalogProvider itemCatalog)
    {
        if (payload.incidentStates.Count > MaximumIncidentHistory)
        {
            report.AddError(
                $"Exterior incident history exceeds {MaximumIncidentHistory} records.");
        }

        HashSet<string> zoneIds = new(
            payload.zones
                .Where(zone => zone != null)
                .Select(zone => zone.zoneId),
            StringComparer.Ordinal);
        HashSet<string> incidentIds = new(StringComparer.Ordinal);
        HashSet<string> activeZoneIds = new(StringComparer.Ordinal);
        int highestSequence = 0;
        foreach (ExteriorIncidentRuntimeState incident in
                 payload.incidentStates)
        {
            string rawIncidentId = incident?.incidentId;
            string rawZoneId = incident?.zoneId;
            string incidentId = rawIncidentId?.Trim() ?? string.Empty;
            string zoneId = rawZoneId?.Trim() ?? string.Empty;
            if (incident == null
                || !string.Equals(
                    rawIncidentId,
                    incidentId,
                    StringComparison.Ordinal)
                || !string.Equals(rawZoneId, zoneId, StringComparison.Ordinal)
                || !Enum.IsDefined(
                    typeof(ExteriorIncidentKind),
                    incident.kind)
                || incident.kind == ExteriorIncidentKind.None
                || handlers == null
                || !handlers.TryGet(incident.kind, out _)
                || !TryParseIncidentId(
                    incidentId,
                    incident.kind,
                    out int sequence)
                || !incidentIds.Add(incidentId)
                || !zoneIds.Contains(zoneId)
                || incident.text == null
                || !Enum.IsDefined(
                    typeof(ExteriorIncidentStage),
                    incident.stage)
                || !Enum.IsDefined(
                    typeof(ExteriorIncidentOutcome),
                    incident.outcome))
            {
                report.AddError(
                    $"Exterior activity contains invalid incident '{incidentId}'.");
                continue;
            }

            highestSequence = Math.Max(highestSequence, sequence);
            if (!incident.IsTerminal && !activeZoneIds.Add(zoneId))
            {
                report.AddError(
                    $"Exterior zone '{zoneId}' contains more than one active incident.");
            }

            if (!IsFiniteAtLeast(incident.durationSeconds, 0.01f)
                || !IsFiniteInRange(
                    incident.remainingSeconds,
                    0f,
                    incident.durationSeconds)
                || !IsFiniteAtLeast(incident.progress, 0f)
                || incident.actorIds == null
                || incident.wildlifeIds == null
                || incident.itemStackIds == null
                || incident.stolenItemId == null
                || incident.stolenItemQuantity < 0
                || incident.offerPrice < 0)
            {
                report.AddError(
                    $"Exterior incident '{incidentId}' contains invalid runtime values.");
                continue;
            }

            ValidateUniqueTextIds(
                incident.actorIds,
                incidentId,
                "actor",
                report,
                value => !string.IsNullOrWhiteSpace(value));
            ValidateUniqueTextIds(
                incident.wildlifeIds,
                incidentId,
                "wildlife",
                report,
                value => TryParseWildlifeId(value));
            ValidateUniqueTextIds(
                incident.itemStackIds,
                incidentId,
                "item stack",
                report,
                value => ((ItemStackId)value).IsValid);

            string stolenItemId = incident.stolenItemId.Trim();
            if (!string.Equals(
                    incident.stolenItemId,
                    stolenItemId,
                    StringComparison.Ordinal))
            {
                report.AddError(
                    $"Exterior incident '{incidentId}' contains a non-canonical stolen item ID.");
            }
            else if (stolenItemId.Length > 0
                && (itemCatalog == null
                    || !itemCatalog.TryGetDefinition(
                        stolenItemId,
                        out _)))
            {
                report.AddError(
                    $"Exterior incident '{incidentId}' references unknown stolen item '{stolenItemId}'.");
            }
        }

        if (payload.nextIncidentSequence <= highestSequence)
        {
            report.AddError(
                $"Exterior next incident sequence {payload.nextIncidentSequence} does not exceed saved sequence {highestSequence}.");
        }
    }

    private static void ValidateUniqueTextIds(
        IReadOnlyList<string> values,
        string incidentId,
        string label,
        DungeonGameRestoreReport report,
        Func<string, bool> isValid)
    {
        HashSet<string> unique = new(StringComparer.Ordinal);
        foreach (string raw in values)
        {
            string value = raw?.Trim() ?? string.Empty;
            if (!string.Equals(raw, value, StringComparison.Ordinal)
                || !isValid(value)
                || !unique.Add(value))
            {
                report.AddError(
                    $"Exterior incident '{incidentId}' contains invalid or duplicate {label} ID '{value}'.");
            }
        }
    }

    private static bool TryParseIncidentId(
        string incidentId,
        ExteriorIncidentKind kind,
        out int sequence)
    {
        string prefix = $"incident:{kind}:";
        sequence = 0;
        return incidentId.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(
                incidentId.Substring(prefix.Length),
                out sequence)
            && sequence > 0;
    }

    private static bool TryParseWildlifeId(string value)
    {
        const string prefix = "wild:";
        return value.StartsWith(prefix, StringComparison.Ordinal)
            && int.TryParse(value.Substring(prefix.Length), out int sequence)
            && sequence > 0;
    }

    private static string BuildZoneId(
        ExteriorZoneType type,
        int x,
        int y)
    {
        return $"exterior:{type}:{x}:{y}";
    }

    private static bool IsFiniteAtLeast(float value, float minimum)
    {
        return !float.IsNaN(value)
            && !float.IsInfinity(value)
            && value >= minimum;
    }

    private static bool IsFiniteInRange(
        float value,
        float minimum,
        float maximum)
    {
        return IsFiniteAtLeast(value, minimum) && value <= maximum;
    }
}
