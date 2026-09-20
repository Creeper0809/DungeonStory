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
    public string memoryErasureSealAwardOperationId = string.Empty;
    public bool memoryErasureSealAwardPublished;

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
            intelligenceDamage = intelligenceDamage,
            memoryErasureSealAwardOperationId =
                memoryErasureSealAwardOperationId,
            memoryErasureSealAwardPublished =
                memoryErasureSealAwardPublished
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
        out float requestedAmount,
        out float appliedAmount);
    bool TryApplyReconnaissance(
        string regionId,
        float amount,
        out float appliedAmount);
    bool TryPrepareReconnaissance(string regionId, float amount,
        out OffenseRegionRuntime.ReconnaissancePreparation preparation);
    OffenseStrategicPressureSnapshot GetPressureForTarget(OffenseTargetDefinition target);
    OffenseStrategicPressureSnapshot GetFactionPressure(string factionId);
    DungeonOffenseRegionSaveData Capture();
    OffenseRegionRestoreCandidate BuildRestoreCandidate(
        DungeonOffenseRegionSaveData saveData);
    void PublishRestoreCandidate(OffenseRegionRestoreCandidate candidate);
}

public sealed class OffenseRegionRestoreCandidate
{
    internal OffenseRegionRestoreCandidate(List<OffenseRegionState> regions)
    {
        Regions = regions ?? throw new ArgumentNullException(nameof(regions));
    }

    internal List<OffenseRegionState> Regions { get; }
}

public sealed class OffenseRegionRuntime :
    IOffenseRegionRuntime,
    IOffenseRegionMemoryErasureSealAwardAuthority
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
        out float requestedAmount,
        out float appliedAmount)
    {
        axis = target?.strategicPressureAxis ?? StrategicPressureAxis.None;
        requestedAmount = 0f;
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
        requestedAmount = Mathf.Clamp(
            Mathf.Max(0f, target.strategicPressureAmount)
            * Mathf.Max(1, rewardMultiplier),
            0f,
            100f);
        if (requestedAmount <= 0f)
        {
            return false;
        }

        float before = region.GetDamage(axis);
        region.AddDamage(axis, requestedAmount);
        appliedAmount = region.GetDamage(axis) - before;
        return appliedAmount > 0f;
    }

    public bool TryPrepareReconnaissance(string regionId, float amount,
        out ReconnaissancePreparation preparation)
    {
        preparation = null;
        var region = regions.FirstOrDefault(value => value.regionId == regionId);
        if (region == null || !float.IsFinite(amount) || amount <= 0
            || !float.IsFinite(region.intelligenceDamage) || region.intelligenceDamage < 0
            || region.intelligenceDamage >= 100) return false;
        preparation = new ReconnaissancePreparation(this, region, Math.Min(100, region.intelligenceDamage + amount));
        return true;
    }

    public sealed class ReconnaissancePreparation
    {
        private readonly OffenseRegionRuntime owner;
        private readonly OffenseRegionState region;
        private readonly string regionId, regionName;
        private bool published;
        internal ReconnaissancePreparation(OffenseRegionRuntime owner, OffenseRegionState region, float after)
        { this.owner = owner; this.region = region; regionId = region.regionId; regionName = region.displayName;
          Before = region.intelligenceDamage; After = after; }
        public string RegionId => regionId;
        public string RegionName => regionName;
        public float Before { get; }
        public float After { get; }
        public bool TryPublish()
        {
            if (published || !owner.regions.Contains(region) || region.intelligenceDamage != Before
                || region.regionId != regionId || region.displayName != regionName) return false;
            region.intelligenceDamage = After;
            published = true;
            return true;
        }
        public void Rollback()
        {
            if (!published) return;
            if (!owner.regions.Contains(region) || region.intelligenceDamage != After)
                throw new InvalidOperationException("knowledge-region-rollback-conflict");
            region.intelligenceDamage = Before;
            published = false;
        }
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

    [GameplayInternalOnly(
        "The boss-award lifecycle enumerates persisted claims that still need their deterministic physical publication completed.",
        "MemoryErasureSealBossAwardService")]
    public IReadOnlyList<string>
        CapturePendingMemoryErasureSealAwardRegionIds()
    {
        return regions
            .Where(region => region != null
                && !region.memoryErasureSealAwardPublished
                && !string.IsNullOrWhiteSpace(
                    region.memoryErasureSealAwardOperationId))
            .Select(region => region.regionId)
            .OrderBy(regionId => regionId, StringComparer.Ordinal)
            .ToArray();
    }

    [GameplayInternalOnly(
        "A confirmed region-boss victory claims the region's one-time physical seal publication operation.",
        "MemoryErasureSealBossAwardService")]
    public bool TryBeginMemoryErasureSealAward(
        string regionId,
        out string operationId,
        out string regionDisplayName,
        out bool alreadyPublished,
        out string failureReason)
    {
        operationId = string.Empty;
        regionDisplayName = string.Empty;
        alreadyPublished = false;
        failureReason = string.Empty;
        string requiredRegionId = regionId?.Trim() ?? string.Empty;
        OffenseRegionState region = regions.SingleOrDefault(value =>
            value != null
            && string.Equals(
                value.regionId,
                requiredRegionId,
                StringComparison.Ordinal));
        if (region == null)
        {
            failureReason =
                "memory-erasure-seal-region-authority-missing:"
                + requiredRegionId;
            return false;
        }

        regionDisplayName = region.displayName?.Trim() ?? string.Empty;
        if (regionDisplayName.Length == 0)
        {
            failureReason =
                "memory-erasure-seal-region-display-name-missing:"
                + requiredRegionId;
            return false;
        }
        string expectedOperation =
            MemoryErasureSealBossAwardRules.BuildOperationId(requiredRegionId);
        string existing =
            region.memoryErasureSealAwardOperationId?.Trim() ?? string.Empty;
        if (existing.Length == 0)
        {
            region.memoryErasureSealAwardOperationId = expectedOperation;
            region.memoryErasureSealAwardPublished = false;
        }
        else if (!string.Equals(
                     existing,
                     expectedOperation,
                     StringComparison.Ordinal))
        {
            failureReason =
                "memory-erasure-seal-region-operation-conflict:"
                + requiredRegionId;
            return false;
        }

        operationId = expectedOperation;
        alreadyPublished = region.memoryErasureSealAwardPublished;
        return true;
    }

    [GameplayInternalOnly(
        "The exact physical Source receipt closes the already-claimed region award.",
        "MemoryErasureSealBossAwardService")]
    public bool TryCompleteMemoryErasureSealAward(
        string regionId,
        string operationId,
        out string failureReason)
    {
        failureReason = string.Empty;
        string requiredRegionId = regionId?.Trim() ?? string.Empty;
        string operation = operationId?.Trim() ?? string.Empty;
        OffenseRegionState region = regions.SingleOrDefault(value =>
            value != null
            && string.Equals(
                value.regionId,
                requiredRegionId,
                StringComparison.Ordinal));
        string expectedOperation =
            MemoryErasureSealBossAwardRules.BuildOperationId(requiredRegionId);
        if (region == null
            || !string.Equals(
                operation,
                expectedOperation,
                StringComparison.Ordinal)
            || !string.Equals(
                region.memoryErasureSealAwardOperationId,
                expectedOperation,
                StringComparison.Ordinal))
        {
            failureReason =
                "memory-erasure-seal-region-completion-conflict:"
                + requiredRegionId;
            return false;
        }

        region.memoryErasureSealAwardPublished = true;
        return true;
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
            string awardOperation =
                restored.memoryErasureSealAwardOperationId?.Trim()
                ?? string.Empty;
            string expectedAwardOperation =
                MemoryErasureSealBossAwardRules.BuildOperationId(
                    restored.regionId);
            if (restored.memoryErasureSealAwardPublished
                    && awardOperation.Length == 0
                || awardOperation.Length > 0
                    && (!string.Equals(
                            restored.memoryErasureSealAwardOperationId,
                            awardOperation,
                            StringComparison.Ordinal)
                        || !string.Equals(
                            awardOperation,
                            expectedAwardOperation,
                            StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Offense region '{restored.regionId}' has invalid memory-erasure seal award authority.");
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
