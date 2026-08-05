using System;
using System.Collections.Generic;

public enum ExpeditionIntelPaymentMethod
{
    Renown = 0,
    Gold = 1,
    ScoutingLabor = 2,
    TrailCharm = 3
}

public enum HostileRumorMitigationMethod
{
    Renown = 0,
    Gold = 1
}

public enum EcologyRaidPhase
{
    Inactive = 0,
    Scheduled = 1,
    InProgress = 2,
    Resolved = 3
}

public readonly struct CoreGridCell : IEquatable<CoreGridCell>
{
    public CoreGridCell(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }
    public int Y { get; }

    public bool Equals(CoreGridCell other) => X == other.X && Y == other.Y;
    public override bool Equals(object obj) =>
        obj is CoreGridCell other && Equals(other);
    public override int GetHashCode() => HashCode.Combine(X, Y);
}

public readonly struct EcologyRaidSnapshot
{
    public EcologyRaidSnapshot(
        EcologyRaidPhase phase,
        float remainingSeconds,
        float weatherPressure,
        float exposedFoodPressure,
        IReadOnlyList<CoreGridCell> exposedFoodPositions,
        int raiderCount,
        int stolenQuantity)
    {
        Phase = phase;
        RemainingSeconds = Math.Max(0f, remainingSeconds);
        WeatherPressure = Math.Max(0f, weatherPressure);
        ExposedFoodPressure = Math.Max(0f, exposedFoodPressure);
        ExposedFoodPositions =
            exposedFoodPositions ?? Array.Empty<CoreGridCell>();
        RaiderCount = Math.Max(0, raiderCount);
        StolenQuantity = Math.Max(0, stolenQuantity);
    }

    public EcologyRaidPhase Phase { get; }
    public float RemainingSeconds { get; }
    public float WeatherPressure { get; }
    public float ExposedFoodPressure { get; }
    public IReadOnlyList<CoreGridCell> ExposedFoodPositions { get; }
    public int RaiderCount { get; }
    public int StolenQuantity { get; }
}

[Serializable]
public sealed class DungeonExternalInfluenceSaveData
{
    public const int CurrentVersion = 3;

    public int version = CurrentVersion;
    public float renown;
    public float dread;
    public float hostileRumor;
    public float ecologyPressure;
    public float scoutingLabor;
    public bool dreadDefenseArmed;
    public bool dreadDefenseActive;
    public bool dreadDefenseBoss;
    public bool ecologyWarningIssued;
    public bool ecologyRaidScheduled;
    public bool ecologyRaidInProgress;
    public bool ecologyResolutionReported;
    public float ecologyRaidRemainingSeconds;
    public int ecologyRaidSequence;
    public float lastWeatherPressure;
    public float lastExposedFoodPressure;
    public int currentOperatingDay = -1;
    public int lastRumorMitigationDay = -1;
    public List<string> intelUnlockedSiteIds = new();
    public List<string> dreadAffectedIntruderIds = new();
}

public interface IExternalCombatInfluenceQuery
{
    float GetMoveSpeedMultiplier(string characterId);
    float GetAttackSpeedMultiplier(string characterId);
    bool IsDreadDefenseActive { get; }
}

public interface IExternalInfluenceRuntime : IExternalCombatInfluenceQuery
{
    float Renown { get; }
    float Dread { get; }
    float HostileRumor { get; }
    float EcologyPressure { get; }
    float ScoutingLabor { get; }
    bool IsDreadDefenseArmed { get; }
    EcologyRaidSnapshot GetEcologyRaidSnapshot();
    void AddRenown(float amount, string source);
    void AddDread(float amount, string source);
    void AddHostileRumor(float amount, string source);
    void AddEcologyPressure(float amount, string source);
    void AddScoutingLabor(float amount);
    bool TryMitigateHostileRumor(
        HostileRumorMitigationMethod method,
        out float reducedAmount,
        out int cost,
        out DomainFailure failure);
    bool TryArmDreadDefense(out DomainFailure failure);
    bool BeginInvasionDread(bool boss);
    bool IsIntelUnlocked(string siteId);
    bool TryUnlockIntel(
        string siteId,
        ExpeditionIntelPaymentMethod payment,
        out DomainFailure failure);
    bool TryUnlockIntelForActiveSite(
        string siteId,
        bool fixedBoss,
        int expiresDay,
        int currentDay,
        ExpeditionIntelPaymentMethod payment,
        out DomainFailure failure);
    DungeonExternalInfluenceSaveData Capture();
    ExternalInfluenceRestoreCandidate BuildRestoreCandidate(
        DungeonExternalInfluenceSaveData saveData);
    void PublishRestoreCandidate(ExternalInfluenceRestoreCandidate candidate);
    void Reset();
}

public sealed class ExternalInfluenceRestoreCandidate
{
    public ExternalInfluenceRestoreCandidate(
        DungeonExternalInfluenceSaveData data,
        IReadOnlyCollection<string> intelUnlocked,
        IReadOnlyCollection<string> dreadAffectedIntruders)
    {
        Data = data ?? throw new ArgumentNullException(nameof(data));
        IntelUnlocked = intelUnlocked
            ?? throw new ArgumentNullException(nameof(intelUnlocked));
        DreadAffectedIntruders = dreadAffectedIntruders
            ?? throw new ArgumentNullException(nameof(dreadAffectedIntruders));
    }

    public DungeonExternalInfluenceSaveData Data { get; }
    public IReadOnlyCollection<string> IntelUnlocked { get; }
    public IReadOnlyCollection<string> DreadAffectedIntruders { get; }
}

public sealed class ExternalInfluenceAggregateState
{
    public DungeonExternalInfluenceSaveData Data { get; set; } = new();
    public HashSet<string> IntelUnlocked { get; } =
        new(StringComparer.Ordinal);
    public HashSet<string> DreadAffectedIntruders { get; } =
        new(StringComparer.Ordinal);
}
