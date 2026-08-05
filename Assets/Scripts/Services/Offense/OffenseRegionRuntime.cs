using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

[Serializable]
public sealed class OffenseRegionState
{
    public string regionId = string.Empty;
    public string displayName = string.Empty;
    public string factionId = string.Empty;
    [Range(0f, 100f)] public float logisticsDamage;
    [Range(0f, 100f)] public float armamentDamage;
    [Range(0f, 100f)] public float manpowerDamage;
    [Range(0f, 100f)] public float intelligenceDamage;

    public float GetDamage(StrategicPressureAxis axis)
    {
        return axis switch
        {
            StrategicPressureAxis.Logistics => logisticsDamage,
            StrategicPressureAxis.Armament => armamentDamage,
            StrategicPressureAxis.Manpower => manpowerDamage,
            StrategicPressureAxis.Intelligence => intelligenceDamage,
            _ => 0f
        };
    }

    public void AddDamage(StrategicPressureAxis axis, float amount)
    {
        float safeAmount = Mathf.Max(0f, amount);
        switch (axis)
        {
            case StrategicPressureAxis.Logistics:
                logisticsDamage = Mathf.Clamp(logisticsDamage + safeAmount, 0f, 100f);
                break;
            case StrategicPressureAxis.Armament:
                armamentDamage = Mathf.Clamp(armamentDamage + safeAmount, 0f, 100f);
                break;
            case StrategicPressureAxis.Manpower:
                manpowerDamage = Mathf.Clamp(manpowerDamage + safeAmount, 0f, 100f);
                break;
            case StrategicPressureAxis.Intelligence:
                intelligenceDamage = Mathf.Clamp(intelligenceDamage + safeAmount, 0f, 100f);
                break;
        }
    }

    public OffenseRegionState Clone()
    {
        return new OffenseRegionState
        {
            regionId = regionId,
            displayName = displayName,
            factionId = factionId,
            logisticsDamage = logisticsDamage,
            armamentDamage = armamentDamage,
            manpowerDamage = manpowerDamage,
            intelligenceDamage = intelligenceDamage
        };
    }
}

public readonly struct OffenseStrategicPressureSnapshot
{
    public OffenseStrategicPressureSnapshot(
        string regionId,
        string regionName,
        string factionId,
        float logistics,
        float armament,
        float manpower,
        float intelligence)
    {
        RegionId = regionId ?? string.Empty;
        RegionName = regionName ?? string.Empty;
        FactionId = factionId ?? string.Empty;
        Logistics = Mathf.Clamp(logistics, 0f, 100f);
        Armament = Mathf.Clamp(armament, 0f, 100f);
        Manpower = Mathf.Clamp(manpower, 0f, 100f);
        Intelligence = Mathf.Clamp(intelligence, 0f, 100f);
    }

    public string RegionId { get; }
    public string RegionName { get; }
    public string FactionId { get; }
    public float Logistics { get; }
    public float Armament { get; }
    public float Manpower { get; }
    public float Intelligence { get; }

    public float Get(StrategicPressureAxis axis)
    {
        return axis switch
        {
            StrategicPressureAxis.Logistics => Logistics,
            StrategicPressureAxis.Armament => Armament,
            StrategicPressureAxis.Manpower => Manpower,
            StrategicPressureAxis.Intelligence => Intelligence,
            _ => 0f
        };
    }
}

[Serializable]
public sealed class DungeonOffenseRegionSaveData
{
    public const int CurrentVersion = 1;

    public int version = CurrentVersion;
    public List<OffenseRegionState> regions = new List<OffenseRegionState>();
}

public interface IOffenseRegionRuntime
{
    IReadOnlyList<OffenseRegionState> Regions { get; }
    bool TryApplyTargetPressure(
        OffenseTargetDefinition target,
        int rewardMultiplier,
        out StrategicPressureAxis axis,
        out float appliedAmount);
    bool TryApplyReconnaissance(
        string regionId,
        float amount,
        out float appliedAmount);
    OffenseStrategicPressureSnapshot GetPressureForTarget(OffenseTargetDefinition target);
    OffenseStrategicPressureSnapshot GetFactionPressure(string factionId);
    DungeonOffenseRegionSaveData Capture();
}

public sealed class OffenseRegionRestoreCandidate
{
    internal OffenseRegionRestoreCandidate(List<OffenseRegionState> regions)
    {
        Regions = regions ?? throw new ArgumentNullException(nameof(regions));
    }

    internal List<OffenseRegionState> Regions { get; }
}

public sealed class OffenseRegionRuntime : IOffenseRegionRuntime
{
    public const string BorderTradeRegionId = "border-trade";
    public const string RivalOutpostRegionId = "rival-outpost";
    public const string SealedZoneRegionId = "sealed-zone";
    public const string HumanFactionId = "human";
    public const string RivalFactionId = "rival";
    public const string SealFactionId = "seal";

    private const float FactionSpilloverWeight = 0.25f;
    private List<OffenseRegionState> regions = new List<OffenseRegionState>();

    public OffenseRegionRuntime()
    {
        EnsureDefaultRegions();
    }

    public IReadOnlyList<OffenseRegionState> Regions => regions;

    public bool TryApplyTargetPressure(
        OffenseTargetDefinition target,
        int rewardMultiplier,
        out StrategicPressureAxis axis,
        out float appliedAmount)
    {
        axis = target?.strategicPressureAxis ?? StrategicPressureAxis.None;
        appliedAmount = 0f;
        if (target == null
            || target.revealsTruth
            || axis == StrategicPressureAxis.None
            || string.IsNullOrWhiteSpace(target.regionId))
        {
            return false;
        }

        OffenseRegionState region = GetOrCreateRegion(
            target.regionId,
            target.regionDisplayName,
            target.factionId);
        appliedAmount = Mathf.Clamp(
            Mathf.Max(0f, target.strategicPressureAmount)
            * Mathf.Max(1, rewardMultiplier),
            0f,
            100f);
        if (appliedAmount <= 0f)
        {
            return false;
        }

        float before = region.GetDamage(axis);
        region.AddDamage(axis, appliedAmount);
        appliedAmount = region.GetDamage(axis) - before;
        return appliedAmount > 0f;
    }

    public bool TryApplyReconnaissance(
        string regionId,
        float amount,
        out float appliedAmount)
    {
        appliedAmount = 0f;
        string normalizedId = regionId?.Trim() ?? string.Empty;
        OffenseRegionState region = regions.FirstOrDefault(candidate =>
            string.Equals(candidate.regionId, normalizedId, StringComparison.Ordinal));
        if (region == null || amount <= 0f)
        {
            return false;
        }

        float before = region.intelligenceDamage;
        region.AddDamage(StrategicPressureAxis.Intelligence, amount);
        appliedAmount = region.intelligenceDamage - before;
        return appliedAmount > 0f;
    }

    public OffenseStrategicPressureSnapshot GetPressureForTarget(OffenseTargetDefinition target)
    {
        return CreatePressureForTarget(target, regions);
    }

    internal static OffenseStrategicPressureSnapshot CreatePressureForTarget(
        OffenseTargetDefinition target,
        IReadOnlyList<OffenseRegionState> sourceRegions)
    {
        if (target == null)
        {
            return default;
        }

        OffenseRegionState region = sourceRegions?.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(candidate.regionId, target.regionId, StringComparison.Ordinal));
        return region != null
            ? CreateEffectiveSnapshot(region, sourceRegions)
            : new OffenseStrategicPressureSnapshot(
                target.regionId,
                target.regionDisplayName,
                target.factionId,
                0f,
                0f,
                0f,
                0f);
    }

    public OffenseStrategicPressureSnapshot GetFactionPressure(string factionId)
    {
        OffenseRegionState[] factionRegions = regions
            .Where(region => region != null
                && string.Equals(region.factionId, factionId, StringComparison.Ordinal))
            .ToArray();
        if (factionRegions.Length == 0)
        {
            return default;
        }

        return new OffenseStrategicPressureSnapshot(
            string.Empty,
            string.Empty,
            factionId,
            factionRegions.Average(region => region.logisticsDamage),
            factionRegions.Average(region => region.armamentDamage),
            factionRegions.Average(region => region.manpowerDamage),
            factionRegions.Average(region => region.intelligenceDamage));
    }

    public DungeonOffenseRegionSaveData Capture()
    {
        return new DungeonOffenseRegionSaveData
        {
            version = DungeonOffenseRegionSaveData.CurrentVersion,
            regions = regions
                .Where(region => region != null)
                .Select(region => region.Clone())
                .ToList()
        };
    }

    internal OffenseRegionRestoreCandidate PrepareRestore(
        DungeonOffenseRegionSaveData saveData)
    {
        List<OffenseRegionState> candidate = new List<OffenseRegionState>();
        if (saveData == null || saveData.version != DungeonOffenseRegionSaveData.CurrentVersion)
        {
            if (saveData != null)
            {
                throw new InvalidOperationException(
                    $"Unsupported offense region payload version {saveData.version}; expected {DungeonOffenseRegionSaveData.CurrentVersion}.");
            }

            throw new InvalidOperationException(
                "Offense region payload is null.");
        }

        foreach (OffenseRegionState saved in saveData.regions)
        {
            if (saved == null || string.IsNullOrWhiteSpace(saved.regionId))
            {
                throw new InvalidOperationException(
                    "Offense region payload contains a null or empty region ID.");
            }

            OffenseRegionState restored = saved.Clone();
            if (!IsPressure(restored.logisticsDamage)
                || !IsPressure(restored.armamentDamage)
                || !IsPressure(restored.manpowerDamage)
                || !IsPressure(restored.intelligenceDamage)
                || string.IsNullOrWhiteSpace(restored.displayName)
                || string.IsNullOrWhiteSpace(restored.factionId))
            {
                throw new InvalidOperationException(
                    $"Offense region '{restored.regionId}' has invalid pressure or authored identity.");
            }
            if (candidate.Any(region => string.Equals(
                    region.regionId,
                    restored.regionId,
                    StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Duplicate offense region '{restored.regionId}'.");
            }

            candidate.Add(restored);
        }

        string[] requiredRegionIds =
        {
            BorderTradeRegionId,
            RivalOutpostRegionId,
            SealedZoneRegionId
        };
        if (requiredRegionIds.Any(id => !candidate.Any(region =>
                string.Equals(region.regionId, id, StringComparison.Ordinal))))
        {
            throw new InvalidOperationException(
                "Offense region payload is missing a required authored region.");
        }

        return new OffenseRegionRestoreCandidate(candidate);
    }

    public OffenseRegionRestoreCandidate BuildRestoreCandidate(
        DungeonOffenseRegionSaveData saveData) =>
        PrepareRestore(saveData);

    internal void PublishRestore(OffenseRegionRestoreCandidate candidate)
    {
        regions = (candidate ?? throw new ArgumentNullException(nameof(candidate)))
            .Regions;
    }

    public void PublishRestoreCandidate(
        OffenseRegionRestoreCandidate candidate) =>
        PublishRestore(candidate);

    private static bool IsPressure(float value)
    {
        return !float.IsNaN(value)
            && !float.IsInfinity(value)
            && value >= 0f
            && value <= 100f;
    }

    private static OffenseStrategicPressureSnapshot CreateEffectiveSnapshot(
        OffenseRegionState region,
        IReadOnlyList<OffenseRegionState> sourceRegions)
    {
        OffenseRegionState[] peers = (sourceRegions ?? Array.Empty<OffenseRegionState>())
            .Where(candidate => candidate != null
                && !ReferenceEquals(candidate, region)
                && string.Equals(candidate.factionId, region.factionId, StringComparison.Ordinal))
            .ToArray();
        return new OffenseStrategicPressureSnapshot(
            region.regionId,
            region.displayName,
            region.factionId,
            Effective(region.logisticsDamage, peers, StrategicPressureAxis.Logistics),
            Effective(region.armamentDamage, peers, StrategicPressureAxis.Armament),
            Effective(region.manpowerDamage, peers, StrategicPressureAxis.Manpower),
            Effective(region.intelligenceDamage, peers, StrategicPressureAxis.Intelligence));
    }

    private static float Effective(
        float localDamage,
        IReadOnlyList<OffenseRegionState> peers,
        StrategicPressureAxis axis)
    {
        float spillover = peers != null && peers.Count > 0
            ? peers.Average(region => region.GetDamage(axis)) * FactionSpilloverWeight
            : 0f;
        return Mathf.Clamp(localDamage + spillover, 0f, 100f);
    }

    private OffenseRegionState GetOrCreateRegion(
        string regionId,
        string displayName,
        string factionId)
    {
        OffenseRegionState region = regions.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(candidate.regionId, regionId, StringComparison.Ordinal));
        if (region != null)
        {
            return region;
        }

        region = new OffenseRegionState
        {
            regionId = regionId ?? string.Empty,
            displayName = displayName ?? string.Empty,
            factionId = factionId ?? string.Empty
        };
        regions.Add(region);
        return region;
    }

    private void EnsureDefaultRegions()
    {
        EnsureRegion(BorderTradeRegionId, "변경 교역권", HumanFactionId);
        EnsureRegion(RivalOutpostRegionId, "경쟁 던전 전초권", RivalFactionId);
        EnsureRegion(SealedZoneRegionId, "봉인 지대", SealFactionId);
    }

    private void EnsureRegion(string regionId, string displayName, string factionId)
    {
        OffenseRegionState existing = regions.FirstOrDefault(region =>
            region != null && string.Equals(region.regionId, regionId, StringComparison.Ordinal));
        if (existing == null)
        {
            regions.Add(new OffenseRegionState
            {
                regionId = regionId,
                displayName = displayName,
                factionId = factionId
            });
            return;
        }

        if (string.IsNullOrWhiteSpace(existing.displayName))
        {
            existing.displayName = displayName;
        }

        if (string.IsNullOrWhiteSpace(existing.factionId))
        {
            existing.factionId = factionId;
        }
    }
}
