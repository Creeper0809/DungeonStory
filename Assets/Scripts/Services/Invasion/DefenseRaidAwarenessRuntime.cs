using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class DefenseRaidAwarenessRestoreCandidate
{
    internal DefenseRaidAwarenessRestoreCandidate(
        string raidId,
        int identificationStage,
        string routeChangeReason,
        IReadOnlyDictionary<Vector2Int, float> knownRisks,
        IReadOnlyDictionary<Vector2Int, BuildingInstanceId> riskSources,
        IReadOnlyList<Vector2Int> expectedPath,
        BuildableObject breachTarget)
    {
        RaidId = raidId;
        IdentificationStage = identificationStage;
        RouteChangeReason = routeChangeReason;
        KnownRisks = knownRisks;
        RiskSources = riskSources;
        ExpectedPath = expectedPath;
        BreachTarget = breachTarget;
    }

    internal string RaidId { get; }
    internal int IdentificationStage { get; }
    internal string RouteChangeReason { get; }
    internal IReadOnlyDictionary<Vector2Int, float> KnownRisks { get; }
    internal IReadOnlyDictionary<Vector2Int, BuildingInstanceId> RiskSources { get; }
    internal IReadOnlyList<Vector2Int> ExpectedPath { get; }
    internal BuildableObject BreachTarget { get; }
}

public sealed class DefenseRaidAwarenessSnapshot
{
    public DefenseRaidAwarenessSnapshot(
        string raidId,
        int identificationStage,
        int version,
        IReadOnlyDictionary<Vector2Int, float> knownRisks,
        IReadOnlyList<Vector2Int> expectedPath,
        string routeChangeReason,
        BuildableObject breachTarget)
    {
        RaidId = raidId ?? string.Empty;
        IdentificationStage = Mathf.Max(0, identificationStage);
        Version = Mathf.Max(0, version);
        KnownRisks = knownRisks
            ?? new Dictionary<Vector2Int, float>();
        ExpectedPath = expectedPath ?? Array.Empty<Vector2Int>();
        RouteChangeReason = routeChangeReason ?? string.Empty;
        BreachTarget = breachTarget;
    }

    public string RaidId { get; }
    public int IdentificationStage { get; }
    public int Version { get; }
    public IReadOnlyDictionary<Vector2Int, float> KnownRisks { get; }
    public IReadOnlyList<Vector2Int> ExpectedPath { get; }
    public string RouteChangeReason { get; }
    public BuildableObject BreachTarget { get; }
}

public interface IDefenseRaidAwarenessRuntime
{
    DefenseRaidAwarenessSnapshot GetSnapshot(string raidId);
    void IdentifyOperation(string raidId, int stage);
    void RecordObservedFacility(string raidId, DefenseFacility facility);
    void RecordTriggeredFacility(
        string raidId,
        DefenseActivationReport report);
    void SetExpectedPath(
        string raidId,
        IEnumerable<Vector2Int> path,
        string changeReason);
    void SetBreachTarget(
        string raidId,
        BuildableObject target,
        string changeReason);
    DefenseRaidAwarenessSaveData Capture(string raidId);
    DefenseRaidAwarenessRestoreCandidate PrepareRestore(
        DefenseRaidAwarenessSaveData source);
    void PublishRestore(DefenseRaidAwarenessRestoreCandidate candidate);
    void Restore(DefenseRaidAwarenessSaveData source);
    void Release(string raidId);
}

public sealed class DefenseRaidAwarenessRuntime :
    IDefenseRaidAwarenessRuntime
{
    private sealed class RaidRecord
    {
        public int IdentificationStage;
        public int Version;
        public readonly Dictionary<Vector2Int, float> KnownRisks =
            new Dictionary<Vector2Int, float>();
        public readonly Dictionary<Vector2Int, BuildingInstanceId> RiskSources =
            new Dictionary<Vector2Int, BuildingInstanceId>();
        public readonly List<Vector2Int> ExpectedPath =
            new List<Vector2Int>();
        public string RouteChangeReason = string.Empty;
        public BuildableObject BreachTarget;
    }

    private readonly Dictionary<string, RaidRecord> raids =
        new Dictionary<string, RaidRecord>(StringComparer.Ordinal);
    private readonly IBuildingWorldQuery buildingWorld;

    public DefenseRaidAwarenessRuntime(IBuildingWorldQuery buildingWorld)
    {
        this.buildingWorld = buildingWorld
            ?? throw new ArgumentNullException(nameof(buildingWorld));
    }

    public DefenseRaidAwarenessSnapshot GetSnapshot(string raidId)
    {
        string key = NormalizeRaidId(raidId);
        RaidRecord record = GetOrCreate(key);
        return new DefenseRaidAwarenessSnapshot(
            key,
            record.IdentificationStage,
            record.Version,
            new Dictionary<Vector2Int, float>(record.KnownRisks),
            record.ExpectedPath.ToArray(),
            record.RouteChangeReason,
            record.BreachTarget);
    }

    public void IdentifyOperation(string raidId, int stage)
    {
        RaidRecord record = GetOrCreate(NormalizeRaidId(raidId));
        int next = Mathf.Max(record.IdentificationStage, stage);
        if (next == record.IdentificationStage)
        {
            return;
        }

        record.IdentificationStage = next;
        record.Version++;
    }

    public void RecordObservedFacility(
        string raidId,
        DefenseFacility facility)
    {
        if (facility == null || facility.isDestroy)
        {
            return;
        }

        float severity = Mathf.Max(
            1f,
            (facility.Defense?.star ?? 1) * 3f
            + (facility.Defense?.range ?? 0) * 0.5f);
        RecordFacilityCells(
            GetOrCreate(NormalizeRaidId(raidId)),
            facility,
            severity);
    }

    public void RecordTriggeredFacility(
        string raidId,
        DefenseActivationReport report)
    {
        if (report?.Facility == null)
        {
            return;
        }

        float severity = Mathf.Max(
            2f,
            report.TotalDamage
            + report.MovementDelaySeconds * 4f
            + report.EffectTags.Count * 3f);
        RecordFacilityCells(
            GetOrCreate(NormalizeRaidId(raidId)),
            report.Facility,
            severity);
    }

    public void SetExpectedPath(
        string raidId,
        IEnumerable<Vector2Int> path,
        string changeReason)
    {
        RaidRecord record = GetOrCreate(NormalizeRaidId(raidId));
        record.ExpectedPath.Clear();
        if (path != null)
        {
            record.ExpectedPath.AddRange(path);
        }

        record.RouteChangeReason = changeReason?.Trim() ?? string.Empty;
        record.Version++;
    }

    public void SetBreachTarget(
        string raidId,
        BuildableObject target,
        string changeReason)
    {
        RaidRecord record = GetOrCreate(NormalizeRaidId(raidId));
        if (record.BreachTarget == target
            && string.Equals(
                record.RouteChangeReason,
                changeReason,
                StringComparison.Ordinal))
        {
            return;
        }

        record.BreachTarget = target;
        record.RouteChangeReason = changeReason?.Trim() ?? string.Empty;
        record.Version++;
    }

    public DefenseRaidAwarenessSaveData Capture(string raidId)
    {
        string key = NormalizeRaidId(raidId);
        RaidRecord record = GetOrCreate(key);
        return new DefenseRaidAwarenessSaveData
        {
            raidId = key,
            identificationStage = record.IdentificationStage,
            routeChangeReason = record.RouteChangeReason,
            breachTargetBuildingInstanceId = record.BreachTarget != null
                ? record.BreachTarget.RequirePersistentInstanceId().Value
                : string.Empty,
            knownRisks = record.KnownRisks
                .OrderBy(pair => pair.Key.y)
                .ThenBy(pair => pair.Key.x)
                .Select(pair => new DefenseKnownRiskSaveData
                {
                    x = pair.Key.x,
                    y = pair.Key.y,
                    severity = pair.Value,
                    facilityBuildingInstanceId = record.RiskSources.TryGetValue(
                        pair.Key,
                        out BuildingInstanceId source)
                        ? source.Value
                        : throw new InvalidOperationException(
                            $"Raid '{key}' risk at {pair.Key} has no canonical building source.")
                })
                .ToList(),
            expectedPath = record.ExpectedPath
                .Select(cell => new DefenseExpectedPathCellSaveData
                {
                    x = cell.x,
                    y = cell.y
                })
                .ToList()
        };
    }

    public DefenseRaidAwarenessRestoreCandidate PrepareRestore(
        DefenseRaidAwarenessSaveData source)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        string key = NormalizeRaidId(source.raidId);
        if (!string.Equals(key, source.raidId, StringComparison.Ordinal)
            || source.identificationStage < 0
            || source.routeChangeReason == null
            || source.knownRisks == null
            || source.expectedPath == null)
        {
            throw new InvalidOperationException(
                "Raid awareness restore payload is not canonical.");
        }

        Dictionary<Vector2Int, float> knownRisks =
            new Dictionary<Vector2Int, float>();
        Dictionary<Vector2Int, BuildingInstanceId> riskSources =
            new Dictionary<Vector2Int, BuildingInstanceId>();
        foreach (DefenseKnownRiskSaveData risk
                 in source.knownRisks)
        {
            if (risk == null
                || float.IsNaN(risk.severity)
                || float.IsInfinity(risk.severity)
                || risk.severity < 0f)
            {
                throw new InvalidOperationException(
                    $"Raid '{key}' contains invalid known-risk data.");
            }

            Vector2Int cell = new Vector2Int(risk.x, risk.y);
            if (!knownRisks.TryAdd(cell, risk.severity))
            {
                throw new InvalidOperationException(
                    $"Raid '{key}' contains duplicate risk cell {cell}.");
            }

            BuildingInstanceId sourceId = RequireCanonicalBuildingId(
                risk.facilityBuildingInstanceId,
                $"Raid '{key}' risk source");
            RequireBuilding(sourceId, $"Raid '{key}' risk source");
            riskSources.Add(cell, sourceId);
        }

        List<Vector2Int> expectedPath = new List<Vector2Int>();
        foreach (DefenseExpectedPathCellSaveData cell
                 in source.expectedPath)
        {
            if (cell == null)
            {
                throw new InvalidOperationException(
                    $"Raid '{key}' contains a null expected-path cell.");
            }
            expectedPath.Add(new Vector2Int(cell.x, cell.y));
        }

        BuildableObject breachTarget = null;
        if (!string.IsNullOrEmpty(source.breachTargetBuildingInstanceId))
        {
            BuildingInstanceId breachTargetId = RequireCanonicalBuildingId(
                source.breachTargetBuildingInstanceId,
                $"Raid '{key}' breach target");
            breachTarget = RequireBuilding(
                breachTargetId,
                $"Raid '{key}' breach target");
        }

        return new DefenseRaidAwarenessRestoreCandidate(
            key,
            source.identificationStage,
            source.routeChangeReason,
            knownRisks,
            riskSources,
            expectedPath,
            breachTarget);
    }

    public void PublishRestore(DefenseRaidAwarenessRestoreCandidate candidate)
    {
        if (candidate == null)
        {
            throw new ArgumentNullException(nameof(candidate));
        }

        int nextVersion = raids.TryGetValue(
                candidate.RaidId,
                out RaidRecord previous)
            ? previous.Version + 1
            : 1;
        RaidRecord replacement = new RaidRecord
        {
            IdentificationStage = candidate.IdentificationStage,
            Version = nextVersion,
            RouteChangeReason = candidate.RouteChangeReason,
            BreachTarget = candidate.BreachTarget
        };
        foreach (KeyValuePair<Vector2Int, float> risk in candidate.KnownRisks)
        {
            replacement.KnownRisks.Add(risk.Key, risk.Value);
        }
        foreach (KeyValuePair<Vector2Int, BuildingInstanceId> source
                 in candidate.RiskSources)
        {
            replacement.RiskSources.Add(source.Key, source.Value);
        }
        replacement.ExpectedPath.AddRange(candidate.ExpectedPath);
        raids[candidate.RaidId] = replacement;
    }

    public void Restore(DefenseRaidAwarenessSaveData source)
    {
        PublishRestore(PrepareRestore(source));
    }

    public void Release(string raidId)
    {
        raids.Remove(NormalizeRaidId(raidId));
    }

    private void RecordFacilityCells(
        RaidRecord record,
        DefenseFacility facility,
        float severity)
    {
        bool changed = false;
        foreach (Vector2Int cell in facility.buildPoses)
        {
            if (!record.KnownRisks.TryGetValue(
                    cell,
                    out float previous)
                || severity > previous + 0.001f)
            {
                record.KnownRisks[cell] = severity;
                record.RiskSources[cell] =
                    facility.RequirePersistentInstanceId();
                changed = true;
            }
        }

        if (changed)
        {
            record.Version++;
        }
    }

    private RaidRecord GetOrCreate(string raidId)
    {
        if (!raids.TryGetValue(raidId, out RaidRecord record))
        {
            record = new RaidRecord();
            raids.Add(raidId, record);
        }

        return record;
    }

    private static string NormalizeRaidId(string raidId)
    {
        return string.IsNullOrWhiteSpace(raidId)
            ? "invasion:unassigned"
            : raidId.Trim();
    }

    private static BuildingInstanceId RequireCanonicalBuildingId(
        string value,
        string label)
    {
        BuildingInstanceId id = new BuildingInstanceId(value);
        if (!id.IsValid
            || !string.Equals(id.Value, value, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{label} '{value}' is not a canonical BuildingInstanceId.");
        }
        return id;
    }

    private BuildableObject RequireBuilding(BuildingInstanceId id, string label)
    {
        BuildableObject match = null;
        foreach (BuildableObject building in buildingWorld.Buildings
                     ?? Array.Empty<BuildableObject>())
        {
            if (building == null || !building.PersistentInstanceId.Equals(id))
            {
                continue;
            }
            if (match != null)
            {
                throw new InvalidOperationException(
                    $"{label} '{id.Value}' resolves to multiple buildings.");
            }
            match = building;
        }

        return match
            ?? throw new InvalidOperationException(
                $"{label} '{id.Value}' does not resolve to a building.");
    }
}
