using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public enum StrategicPressureAxis
{
    None = 0,
    Logistics = 1,
    Armament = 2,
    Manpower = 3,
    Intelligence = 4
}

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
    void Restore(DungeonOffenseRegionSaveData saveData, DungeonGameRestoreReport report = null);
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
    private readonly List<OffenseRegionState> regions = new List<OffenseRegionState>();
    private readonly IReadOnlyList<OffenseRegionState> regionsView;

    public OffenseRegionRuntime()
    {
        regionsView = regions.AsReadOnly();
        EnsureDefaultRegions();
    }

    public IReadOnlyList<OffenseRegionState> Regions => regionsView;

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
        if (target == null)
        {
            return default;
        }

        OffenseRegionState region = regions.FirstOrDefault(candidate =>
            candidate != null
            && string.Equals(candidate.regionId, target.regionId, StringComparison.Ordinal));
        return region != null
            ? CreateEffectiveSnapshot(region)
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

    public void Restore(DungeonOffenseRegionSaveData saveData, DungeonGameRestoreReport report = null)
    {
        regions.Clear();
        if (saveData == null || saveData.version != DungeonOffenseRegionSaveData.CurrentVersion)
        {
            if (saveData != null)
            {
                report?.AddWarning(
                    $"지원하지 않는 지역 압력 데이터 버전 {saveData.version}입니다. 기본 지역 상태로 복원합니다.");
            }

            EnsureDefaultRegions();
            return;
        }

        foreach (OffenseRegionState saved in saveData.regions ?? new List<OffenseRegionState>())
        {
            if (saved == null || string.IsNullOrWhiteSpace(saved.regionId))
            {
                continue;
            }

            OffenseRegionState restored = saved.Clone();
            restored.logisticsDamage = Mathf.Clamp(restored.logisticsDamage, 0f, 100f);
            restored.armamentDamage = Mathf.Clamp(restored.armamentDamage, 0f, 100f);
            restored.manpowerDamage = Mathf.Clamp(restored.manpowerDamage, 0f, 100f);
            restored.intelligenceDamage = Mathf.Clamp(restored.intelligenceDamage, 0f, 100f);
            if (regions.Any(region => string.Equals(
                    region.regionId,
                    restored.regionId,
                    StringComparison.Ordinal)))
            {
                report?.AddWarning($"중복 지역 압력 상태를 건너뜁니다: {restored.regionId}");
                continue;
            }

            regions.Add(restored);
        }

        EnsureDefaultRegions();
    }

    private OffenseStrategicPressureSnapshot CreateEffectiveSnapshot(OffenseRegionState region)
    {
        OffenseRegionState[] peers = regions
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

public sealed class OffenseRegionSaveSection : IDungeonSaveSection
{
    public const string Id = "offense.regions";
    private readonly IOffenseRegionRuntime runtime;

    public OffenseRegionSaveSection(IOffenseRegionRuntime runtime)
    {
        this.runtime = runtime ?? throw new ArgumentNullException(nameof(runtime));
    }

    public string SectionId => Id;
    public int SectionVersion => 1;
    public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => new[]
    {
        OffenseSaveSection.Id,
        OffenseReturnArrivalSaveSection.Id,
        ExteriorActivitySaveSection.Id
    };
    public string Capture() => JsonUtility.ToJson(runtime.Capture());

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        if (sectionVersion != SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {Id} section version {sectionVersion}.");
        }

        runtime.Restore(
            JsonUtility.FromJson<DungeonOffenseRegionSaveData>(payloadJson ?? string.Empty)
            ?? new DungeonOffenseRegionSaveData(),
            report);
    }
}
