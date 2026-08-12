using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using VContainer.Unity;

public readonly struct EmergencyRiskForecastSnapshot
{
    public EmergencyRiskForecastSnapshot(
        long highestP90MilliWu,
        string limitingSourceId,
        int sourceCount,
        long revision)
    {
        HighestP90MilliWu = highestP90MilliWu;
        LimitingSourceId = limitingSourceId ?? string.Empty;
        SourceCount = sourceCount;
        Revision = revision;
    }

    public long HighestP90MilliWu { get; }
    public string LimitingSourceId { get; }
    public int SourceCount { get; }
    public long Revision { get; }
}

public interface IEmergencyRiskForecastRegistry
{
    EmergencyAccountingResult SetP90Requirement(
        string sourceId,
        long requiredMilliWu);
    EmergencyAccountingResult Remove(string sourceId);
    EmergencyRiskForecastSnapshot Capture();
}

public readonly struct SettlementEmergencyReserveTargetSnapshot
{
    public SettlementEmergencyReserveTargetSnapshot(
        int productiveAdultCount,
        long minimumMilliWu,
        long forecastMilliWu,
        string limitingForecastSourceId,
        float alertMultiplier,
        long targetMilliWu,
        long availableMilliWu,
        float coverage,
        long revision)
    {
        ProductiveAdultCount = productiveAdultCount;
        MinimumMilliWu = minimumMilliWu;
        ForecastMilliWu = forecastMilliWu;
        LimitingForecastSourceId = limitingForecastSourceId ?? string.Empty;
        AlertMultiplier = alertMultiplier;
        TargetMilliWu = targetMilliWu;
        AvailableMilliWu = availableMilliWu;
        Coverage = coverage;
        Revision = revision;
    }

    public int ProductiveAdultCount { get; }
    public long MinimumMilliWu { get; }
    public long ForecastMilliWu { get; }
    public string LimitingForecastSourceId { get; }
    public float AlertMultiplier { get; }
    public long TargetMilliWu { get; }
    public long AvailableMilliWu { get; }
    public float Coverage { get; }
    public long Revision { get; }
}

public interface ISettlementEmergencyReserveTargetQuery
{
    SettlementEmergencyReserveTargetSnapshot CaptureTarget();
}

/// <summary>
/// Mutable forecast port for concrete fire, medical, defense and breakdown
/// systems. It deliberately has no guessed default disaster cost: sources
/// must publish an authored P90 requirement through this contract.
/// </summary>
public sealed class EmergencyRiskForecastRegistry :
    IEmergencyRiskForecastRegistry
{
    private readonly Dictionary<string, long> requirements =
        new Dictionary<string, long>(StringComparer.Ordinal);
    private long revision;
    private bool dirty = true;
    private EmergencyRiskForecastSnapshot cached;

    public EmergencyAccountingResult SetP90Requirement(
        string sourceId,
        long requiredMilliWu)
    {
        string normalized = sourceId?.Trim() ?? string.Empty;
        if (normalized.Length == 0 || requiredMilliWu < 0L)
        {
            return EmergencyAccountingResult.Fail(
                "EmergencyRiskForecastInvalid",
                "A forecast requires a source ID and non-negative milli-WU.");
        }

        if (requirements.TryGetValue(normalized, out long previous)
            && previous == requiredMilliWu)
        {
            return EmergencyAccountingResult.Ok("forecast-unchanged");
        }

        requirements[normalized] = requiredMilliWu;
        revision = checked(revision + 1L);
        dirty = true;
        return EmergencyAccountingResult.Ok("forecast-updated");
    }

    public EmergencyAccountingResult Remove(string sourceId)
    {
        string normalized = sourceId?.Trim() ?? string.Empty;
        if (normalized.Length == 0)
        {
            return EmergencyAccountingResult.Fail(
                "EmergencyRiskForecastInvalid",
                "A forecast source ID is required.");
        }

        if (!requirements.Remove(normalized))
        {
            return EmergencyAccountingResult.Ok("forecast-already-absent");
        }

        revision = checked(revision + 1L);
        dirty = true;
        return EmergencyAccountingResult.Ok("forecast-removed");
    }

    public EmergencyRiskForecastSnapshot Capture()
    {
        if (!dirty)
        {
            return cached;
        }

        long highest = 0L;
        string limiting = string.Empty;
        foreach (KeyValuePair<string, long> pair in requirements)
        {
            if (pair.Value > highest
                || pair.Value == highest
                && string.CompareOrdinal(pair.Key, limiting) < 0)
            {
                highest = pair.Value;
                limiting = pair.Key;
            }
        }

        cached = new EmergencyRiskForecastSnapshot(
            highest,
            limiting,
            requirements.Count,
            revision);
        dirty = false;
        return cached;
    }
}

/// <summary>
/// Projects live productive adults, the fixed minimum reserve rule and
/// authored P90 risk forecasts into the alert service's coverage band.
/// Expensive population scans only run when the character registry changes.
/// </summary>
public sealed class SettlementEmergencyReservePlanner :
    ISettlementEmergencyReserveTargetQuery,
    IStartable,
    IDisposable,
    ITickable
{
    private const long AbsoluteMinimumMilliWu = 12L * EmergencyWuUnits.UnitsPerWu;
    private const long PerProductiveAdultMilliWu = 3L * EmergencyWuUnits.UnitsPerWu;

    private readonly ICharacterWorldQuery world;
    private readonly ICharacterLifeQuery life;
    private readonly ICharacterBodyHealthQuery bodyHealth;
    private readonly IGameEventBus events;
    private readonly IEmergencyWorkAccountingService accounting;
    private readonly IEmergencyRiskForecastRegistry forecasts;
    private readonly ISettlementAlertService alerts;

    private int lastCharacterVersion = int.MinValue;
    private int productiveAdultCount;
    private long lastAccountingRevision = long.MinValue;
    private long lastForecastRevision = long.MinValue;
    private long lastAlertEpoch = long.MinValue;
    private SettlementThreatAlertLevel lastAlertLevel =
        (SettlementThreatAlertLevel)(-1);
    private long revision;
    private SettlementEmergencyReserveTargetSnapshot snapshot;
    private IDisposable downedSubscription;
    private IDisposable recoveredSubscription;
    private IDisposable lifeStageSubscription;
    private IDisposable deathSubscription;
    private bool productivePopulationDirty = true;

    public SettlementEmergencyReservePlanner(
        ICharacterWorldQuery world,
        ICharacterLifeQuery life,
        ICharacterBodyHealthQuery bodyHealth,
        IGameEventBus events,
        IEmergencyWorkAccountingService accounting,
        IEmergencyRiskForecastRegistry forecasts,
        ISettlementAlertService alerts)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.life = life ?? throw new ArgumentNullException(nameof(life));
        this.bodyHealth = bodyHealth
            ?? throw new ArgumentNullException(nameof(bodyHealth));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.accounting = accounting
            ?? throw new ArgumentNullException(nameof(accounting));
        this.forecasts = forecasts
            ?? throw new ArgumentNullException(nameof(forecasts));
        this.alerts = alerts ?? throw new ArgumentNullException(nameof(alerts));
    }

    public SettlementEmergencyReserveTargetSnapshot CaptureTarget() => snapshot;

    public void Start()
    {
        downedSubscription ??= events.Subscribe<CharacterBodyHealthDownedEvent>(
            _ => productivePopulationDirty = true);
        recoveredSubscription ??= events.Subscribe<CharacterBodyHealthRecoveredEvent>(
            _ => productivePopulationDirty = true);
        lifeStageSubscription ??= events.Subscribe<CharacterLifeStageChangedEvent>(
            _ => productivePopulationDirty = true);
        deathSubscription ??= events.Subscribe<CharacterDeathEvent>(
            _ => productivePopulationDirty = true);
    }

    public void Dispose()
    {
        downedSubscription?.Dispose();
        recoveredSubscription?.Dispose();
        lifeStageSubscription?.Dispose();
        deathSubscription?.Dispose();
        downedSubscription = null;
        recoveredSubscription = null;
        lifeStageSubscription = null;
        deathSubscription = null;
    }

    public void Tick()
    {
        if (productivePopulationDirty
            || lastCharacterVersion != world.CharacterVersion)
        {
            productiveAdultCount = CountProductiveAdults();
            lastCharacterVersion = world.CharacterVersion;
            productivePopulationDirty = false;
            lastAccountingRevision = long.MinValue;
        }

        EmergencyReserveSnapshot work = accounting.CaptureSnapshot();
        EmergencyRiskForecastSnapshot risk = forecasts.Capture();
        SettlementAlertSnapshot alert = alerts.Capture();
        if (lastAccountingRevision == work.AccountingRevision
            && lastForecastRevision == risk.Revision
            && lastAlertEpoch == alert.AlertEpochId
            && lastAlertLevel == alert.CommittedLevel)
        {
            return;
        }

        long minimum = Math.Max(
            AbsoluteMinimumMilliWu,
            checked(productiveAdultCount * PerProductiveAdultMilliWu));
        long unscaled = Math.Max(minimum, risk.HighestP90MilliWu);
        int multiplierNumerator = alert.CommittedLevel switch
        {
            SettlementThreatAlertLevel.Amber => 3,
            SettlementThreatAlertLevel.Red => 2,
            _ => 1
        };
        int multiplierDenominator = alert.CommittedLevel ==
            SettlementThreatAlertLevel.Amber ? 2 : 1;
        long target = checked(
            unscaled * multiplierNumerator / multiplierDenominator);
        EmergencyAccountingResult coverage = alerts.UpdateReserveCoverage(
            work.ReserveEligibleMilliWu,
            target);
        if (!coverage.Success)
        {
            throw new InvalidOperationException(coverage.Message);
        }

        revision = checked(revision + 1L);
        snapshot = new SettlementEmergencyReserveTargetSnapshot(
            productiveAdultCount,
            minimum,
            risk.HighestP90MilliWu,
            risk.LimitingSourceId,
            multiplierNumerator / (float)multiplierDenominator,
            target,
            work.ReserveEligibleMilliWu,
            work.ReserveEligibleMilliWu / (float)target,
            revision);
        lastAccountingRevision = work.AccountingRevision;
        lastForecastRevision = risk.Revision;
        lastAlertEpoch = alert.AlertEpochId;
        lastAlertLevel = alert.CommittedLevel;
    }

    private int CountProductiveAdults()
    {
        int count = 0;
        IReadOnlyList<CharacterActor> characters = world.Characters;
        for (int index = 0; index < characters.Count; index++)
        {
            CharacterActor actor = characters[index];
            if (actor == null
                || actor.IsDead
                || !CharacterPersistentIdentity.TryGet(actor, out CharacterId id)
                || !life.TryGet(id, out CharacterLifeRecord record)
                || record.LifeStage < CharacterLifeStage.Adult
                || bodyHealth.GetSnapshot(actor).Downed)
            {
                continue;
            }

            count++;
        }
        return count;
    }
}
