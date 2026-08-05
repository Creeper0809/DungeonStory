using System;
using System.Collections.Generic;
using UnityEngine;

public interface IFacilityEvolutionRecordEventRecorder
{
    void RecordVisit(FacilityVisitEvent eventType, int highTurnoverVisitStep);
    void RecordRevenue(FacilityRevenueEvent eventType, int highValueRevenueThreshold);
    void RecordStockConsumed(FacilityStockConsumedEvent eventType);
    void RecordCrime(FacilityCrimeEvent eventType);
    void RecordRestock(FacilityRestockEvent eventType);
    void RecordDefenseTriggered(DefenseFacilityTriggeredEvent eventType);
    void RecordInvasionDamage(InvasionFacilityDamagedEvent eventType);
    void CompleteOperatingDay(int cleanServiceMinVisits);
}

public sealed class FacilityEvolutionRecordEventRecorder : IFacilityEvolutionRecordEventRecorder
{
    private readonly Dictionary<BuildingInstanceId, HashSet<string>> uniqueVisitorsByFacility =
        new Dictionary<BuildingInstanceId, HashSet<string>>();
    private readonly Dictionary<BuildingInstanceId, FacilityDayRecord> dayRecords =
        new Dictionary<BuildingInstanceId, FacilityDayRecord>();
    private readonly Dictionary<BuildingInstanceId, int> stockConsumedByFacility =
        new Dictionary<BuildingInstanceId, int>();

    private readonly IFacilityCandidateCache facilityCandidateCache;
    private readonly IFacilityEvolutionRecordComponentService recordComponentService;
    private readonly IFacilityEvolutionRuntime instanceEvolutionRuntime;

    public FacilityEvolutionRecordEventRecorder(
        IFacilityCandidateCache facilityCandidateCache,
        IFacilityEvolutionRecordComponentService recordComponentService,
        IFacilityEvolutionRuntime instanceEvolutionRuntime)
    {
        this.facilityCandidateCache = facilityCandidateCache
            ?? throw new ArgumentNullException(nameof(facilityCandidateCache));
        this.recordComponentService = recordComponentService
            ?? throw new ArgumentNullException(nameof(recordComponentService));
        this.instanceEvolutionRuntime = instanceEvolutionRuntime;
    }

    public void RecordVisit(FacilityVisitEvent eventType, int highTurnoverVisitStep)
    {
        BuildableObject facility = eventType.facility;
        if (!IsValidFacility(facility))
        {
            return;
        }

        FacilityEvolutionRecordComponent record = GetOrAddRecord(facility);
        float previousVisits = GetMetric(record, FacilityEvolutionTerms.VisitCount);
        float nextVisits = previousVisits + 1f;
        SetMetric(record, FacilityEvolutionTerms.VisitCount, nextVisits);

        FacilityDayRecord day = GetDayRecord(facility);
        day.visits++;

        CharacterActor visitor = eventType.visitorActor;
        if (visitor != null)
        {
            string visitorId = GetVisitorId(visitor);
            if (!string.IsNullOrWhiteSpace(visitorId))
            {
                HashSet<string> uniqueVisitors = GetUniqueVisitors(facility);
                bool repeatVisit = !uniqueVisitors.Add(visitorId);
                SetMetric(record, FacilityEvolutionTerms.UniqueVisitorCount, uniqueVisitors.Count);
                SetMetric(
                    record,
                    FacilityEvolutionTerms.RepeatVisitorRatio,
                    UpdateRatio(GetMetric(record, FacilityEvolutionTerms.RepeatVisitorRatio), previousVisits, nextVisits, repeatVisit));
            }

            float mood = GetCondition(visitor, CharacterCondition.MOOD, 50f);
            SetMetric(
                record,
                FacilityEvolutionTerms.AverageSatisfaction,
                UpdateAverage(GetMetric(record, FacilityEvolutionTerms.AverageSatisfaction), previousVisits, nextVisits, mood));

            bool combatVisitor = IsCombatVisitor(visitor);
            bool nobleVisitor = IsNobleVisitor(visitor);
            SetMetric(
                record,
                FacilityEvolutionTerms.CombatVisitorRatio,
                UpdateRatio(GetMetric(record, FacilityEvolutionTerms.CombatVisitorRatio), previousVisits, nextVisits, combatVisitor));
            SetMetric(
                record,
                FacilityEvolutionTerms.NobleVisitorRatio,
                UpdateRatio(GetMetric(record, FacilityEvolutionTerms.NobleVisitorRatio), previousVisits, nextVisits, nobleVisitor));

            if (combatVisitor && SupportsRole(facility, FacilityRole.Meal))
            {
                record.AddToken(FacilityEvolutionTerms.MercenaryHangout, 1);
                record.AddRecentEvent($"{GetActorName(visitor)} visited {GetFacilityName(facility)} as a combat-oriented guest.");
            }

            if (nobleVisitor)
            {
                record.AddToken(FacilityEvolutionTerms.NoblePatronage, 1);
            }
        }

        if (highTurnoverVisitStep > 0 && Mathf.RoundToInt(nextVisits) % highTurnoverVisitStep == 0)
        {
            record.AddToken(FacilityEvolutionTerms.HighTurnoverService, 1);
        }

        RecordInstanceUsage(
            facility,
            "facility.visit",
            1f,
            1f,
            visitor != null ? GetVisitorId(visitor) : string.Empty,
            "service",
            "visit");
        MarkDynamicStateDirty();
    }

    public void RecordRevenue(FacilityRevenueEvent eventType, int highValueRevenueThreshold)
    {
        BuildableObject facility = eventType.facility;
        if (!IsValidFacility(facility) || eventType.revenue <= 0)
        {
            return;
        }

        FacilityEvolutionRecordComponent record = GetOrAddRecord(facility);
        IncrementMetric(record, FacilityEvolutionTerms.TotalRevenue, eventType.revenue);
        float visitCount = Mathf.Max(1f, GetMetric(record, FacilityEvolutionTerms.VisitCount));
        float totalRevenue = GetMetric(record, FacilityEvolutionTerms.TotalRevenue);
        SetMetric(record, FacilityEvolutionTerms.AverageSpend, totalRevenue / visitCount);
        SetMetric(record, FacilityEvolutionTerms.ProfitPerVisit, totalRevenue / visitCount);

        if (eventType.revenue >= highValueRevenueThreshold)
        {
            IncrementMetric(record, FacilityEvolutionTerms.HighValueTransactionCount, 1f);
            record.AddToken(FacilityEvolutionTerms.NoblePatronage, 1);
        }

        GetDayRecord(facility).revenue += eventType.revenue;
        RecordInstanceUsage(
            facility,
            "facility.revenue",
            Mathf.Max(1f, eventType.revenue * 0.02f),
            eventType.revenue,
            string.Empty,
            "service",
            "revenue");
        MarkDynamicStateDirty();
    }

    public void RecordStockConsumed(FacilityStockConsumedEvent eventType)
    {
        BuildableObject facility = eventType.facility;
        if (!IsValidFacility(facility) || eventType.amount <= 0)
        {
            return;
        }

        FacilityEvolutionRecordComponent record = GetOrAddRecord(facility);
        BuildingInstanceId key = GetFacilityKey(facility);
        stockConsumedByFacility.TryGetValue(key, out int consumedStock);
        consumedStock += eventType.amount;
        stockConsumedByFacility[key] = consumedStock;
        float visitCount = Mathf.Max(1f, GetMetric(record, FacilityEvolutionTerms.VisitCount));
        SetMetric(record, FacilityEvolutionTerms.StockCostPerVisit, consumedStock / visitCount);
        if (eventType.category == StockCategory.Food && SupportsRole(facility, FacilityRole.Meal))
        {
            record.AddToken(FacilityEvolutionTerms.HighMeatConsumption, Mathf.Max(1, eventType.amount));
        }

        RecordInstanceUsage(
            facility,
            "facility.stock-consumed",
            Mathf.Max(0.5f, eventType.amount * 0.5f),
            eventType.amount,
            string.Empty,
            "production",
            eventType.category.ToString());
        MarkDynamicStateDirty();
    }

    public void RecordCrime(FacilityCrimeEvent eventType)
    {
        BuildableObject facility = eventType.facility;
        if (!IsValidFacility(facility))
        {
            return;
        }

        FacilityEvolutionRecordComponent record = GetOrAddRecord(facility);
        IncrementMetric(record, FacilityEvolutionTerms.CrimeCount, 1f);
        IncrementMetric(record, FacilityEvolutionTerms.NegativeMentionCount, 1f);
        if (eventType.kind == FacilityCrimeKind.Shoplifting)
        {
            IncrementMetric(record, FacilityEvolutionTerms.TheftCount, 1f);
        }

        float visits = Mathf.Max(1f, GetMetric(record, FacilityEvolutionTerms.VisitCount));
        SetMetric(
            record,
            FacilityEvolutionTerms.CriminalVisitorRatio,
            Mathf.Clamp01(GetMetric(record, FacilityEvolutionTerms.CrimeCount) / visits));
        record.AddToken(FacilityEvolutionTerms.OutlawRumor, 1);
        record.AddRecentEvent(string.IsNullOrWhiteSpace(eventType.detail)
            ? $"Crime occurred at {GetFacilityName(facility)}."
            : eventType.detail);
        GetDayRecord(facility).incidents++;
        RecordInstanceUsage(
            facility,
            "facility.crime",
            2f,
            1f,
            string.Empty,
            "accident",
            "service");
        MarkDynamicStateDirty();
    }

    public void RecordRestock(FacilityRestockEvent eventType)
    {
        BuildableObject facility = eventType.facility;
        if (!IsValidFacility(facility))
        {
            return;
        }

        if (eventType.requestedAmount > 0 && eventType.restockedAmount <= 0)
        {
            FacilityEvolutionRecordComponent record = GetOrAddRecord(facility);
            IncrementMetric(record, FacilityEvolutionTerms.StockoutCount, 1f);
            record.AddRecentEvent(string.IsNullOrWhiteSpace(eventType.message)
                ? $"{GetFacilityName(facility)} failed to restock."
                : eventType.message);
            GetDayRecord(facility).incidents++;
            RecordInstanceUsage(
                facility,
                "facility.restock-failed",
                1f,
                1f,
                string.Empty,
                "accident",
                "logistics");
            MarkDynamicStateDirty();
        }
    }

    public void RecordDefenseTriggered(DefenseFacilityTriggeredEvent eventType)
    {
        DefenseActivationSnapshot report = eventType.report;
        DefenseFacility facility = report?.SourceFacility;
        if (!IsValidFacility(facility))
        {
            return;
        }

        FacilityEvolutionRecordComponent record = GetOrAddRecord(facility);
        IncrementMetric(record, FacilityEvolutionTerms.IntruderDamageDealt, report.TotalDamage);
        IncrementMetric(record, FacilityEvolutionTerms.IntruderDelayTime, report.MovementDelaySeconds);
        if (report.TotalDamage > 0f)
        {
            record.AddToken(FacilityEvolutionTerms.IntruderBloodied, 1);
        }

        if (report.Concept == DefenseAttackConcept.Guard)
        {
            record.AddToken(FacilityEvolutionTerms.GuardRallyPoint, 1);
        }

        record.AddRecentEvent(report.FormatSummary());
        RecordInstanceUsage(
            facility,
            "facility.defense-triggered",
            Mathf.Max(2f, report.TotalDamage * 0.5f),
            Mathf.Max(1f, report.TotalDamage),
            string.Empty,
            "defense",
            "combat");
        MarkDynamicStateDirty();
    }

    public void RecordInvasionDamage(InvasionFacilityDamagedEvent eventType)
    {
        BuildableObject facility = eventType.facility;
        if (!IsValidFacility(facility))
        {
            return;
        }

        FacilityEvolutionRecordComponent record = GetOrAddRecord(facility);
        IncrementMetric(record, FacilityEvolutionTerms.FacilityDamageTaken, 1f);
        IncrementMetric(record, FacilityEvolutionTerms.NegativeMentionCount, 1f);
        record.AddRecentEvent($"{GetFacilityName(facility)} was damaged during an invasion.");
        GetDayRecord(facility).incidents++;
        RecordInstanceUsage(
            facility,
            "facility.invasion-damage",
            1f,
            1f,
            string.Empty,
            "defense",
            "accident");
        MarkDynamicStateDirty();
    }

    public void CompleteOperatingDay(int cleanServiceMinVisits)
    {
        foreach (KeyValuePair<BuildingInstanceId, FacilityDayRecord> entry in dayRecords)
        {
            FacilityDayRecord day = entry.Value;
            if (day.facility == null || day.facility.isDestroy)
            {
                continue;
            }

            FacilityEvolutionRecordComponent record = GetOrAddRecord(day.facility);
            if (day.visits >= cleanServiceMinVisits && day.incidents == 0)
            {
                record.AddToken(FacilityEvolutionTerms.CleanServiceStreak, 1);
                IncrementMetric(record, FacilityEvolutionTerms.PositiveMentionCount, 1f);
                RecordInstanceUsage(
                    day.facility,
                    "facility.clean-service-day",
                    2f,
                    day.visits,
                    string.Empty,
                    "service",
                    "clean");
            }
        }

        dayRecords.Clear();
        MarkDynamicStateDirty();
    }

    private FacilityDayRecord GetDayRecord(BuildableObject facility)
    {
        BuildingInstanceId key = GetFacilityKey(facility);
        if (!dayRecords.TryGetValue(key, out FacilityDayRecord record))
        {
            record = new FacilityDayRecord(facility);
            dayRecords[key] = record;
        }

        return record;
    }

    private void MarkDynamicStateDirty()
    {
        facilityCandidateCache.MarkDynamicStateDirty();
    }

    private HashSet<string> GetUniqueVisitors(BuildableObject facility)
    {
        BuildingInstanceId key = GetFacilityKey(facility);
        if (!uniqueVisitorsByFacility.TryGetValue(key, out HashSet<string> visitors))
        {
            visitors = new HashSet<string>(StringComparer.Ordinal);
            uniqueVisitorsByFacility[key] = visitors;
        }

        return visitors;
    }

    private FacilityEvolutionRecordComponent GetOrAddRecord(BuildableObject facility)
    {
        return recordComponentService.GetOrAdd(facility);
    }

    private void RecordInstanceUsage(
        BuildableObject facility,
        string eventId,
        float mastery,
        float amount,
        string actorId,
        params string[] sourceTags)
    {
        instanceEvolutionRuntime?.RecordUsage(
            facility,
            eventId,
            mastery,
            amount,
            actorId,
            sourceTags);
    }

    private static void IncrementMetric(FacilityEvolutionRecordComponent record, string key, float delta)
    {
        if (record == null || string.IsNullOrWhiteSpace(key) || Mathf.Approximately(delta, 0f))
        {
            return;
        }

        SetMetric(record, key, GetMetric(record, key) + delta);
    }

    private static void SetMetric(FacilityEvolutionRecordComponent record, string key, float value)
    {
        record?.SetMetric(key, value);
    }

    private static float GetMetric(FacilityEvolutionRecordComponent record, string key)
    {
        return record != null ? record.GetRecord(null).GetMetric(key) : 0f;
    }

    private static float UpdateAverage(float previousAverage, float previousCount, float nextCount, float value)
    {
        if (nextCount <= 0f)
        {
            return value;
        }

        return ((previousAverage * Mathf.Max(0f, previousCount)) + value) / nextCount;
    }

    private static float UpdateRatio(float previousRatio, float previousTotal, float nextTotal, bool increment)
    {
        if (nextTotal <= 0f)
        {
            return 0f;
        }

        float previousCount = Mathf.Clamp01(previousRatio) * Mathf.Max(0f, previousTotal);
        return Mathf.Clamp01((previousCount + (increment ? 1f : 0f)) / nextTotal);
    }

    private static bool IsValidFacility(BuildableObject facility)
    {
        return facility != null && !facility.isDestroy;
    }

    private static BuildingInstanceId GetFacilityKey(BuildableObject facility)
    {
        return facility != null
            ? facility.RequirePersistentInstanceId()
            : throw new ArgumentNullException(nameof(facility));
    }

    private static string GetVisitorId(CharacterActor actor)
    {
        return actor != null && actor.Identity != null
            ? actor.Identity.PersistentId
            : string.Empty;
    }

    private static bool SupportsRole(BuildableObject facility, FacilityRole role)
    {
        return facility != null && facility.SupportsFacilityRole(role);
    }

    private static bool IsCombatVisitor(CharacterActor actor)
    {
        if (actor == null)
        {
            return false;
        }

        string species = actor.SpeciesTag ?? string.Empty;
        return actor.characterType == CharacterType.Intruder
            || actor.GetCombatPowerMultiplier() >= 1.15f
            || species.IndexOf("Orc", StringComparison.OrdinalIgnoreCase) >= 0
            || species.IndexOf("Mercenary", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsNobleVisitor(CharacterActor actor)
    {
        if (actor == null)
        {
            return false;
        }

        string species = actor.SpeciesTag ?? string.Empty;
        return actor.profile != null && actor.profile.GetSpendingMultiplier() >= 1.2f
            || species.IndexOf("Vampire", StringComparison.OrdinalIgnoreCase) >= 0
            || species.IndexOf("Noble", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static float GetCondition(CharacterActor actor, CharacterCondition condition, float defaultValue)
    {
        if (actor == null || actor.stats == null)
        {
            return defaultValue;
        }

        return actor.stats.TryGetValue(condition, out float value) ? value : defaultValue;
    }

    private static string GetActorName(CharacterActor actor)
    {
        return actor != null && actor.Identity != null ? actor.Identity.DisplayName : "Unknown";
    }

    private static string GetFacilityName(BuildableObject facility)
    {
        return FacilityShopService.GetBuildingName(facility != null ? facility.BuildingData : null);
    }

    private sealed class FacilityDayRecord
    {
        public FacilityDayRecord(BuildableObject facility)
        {
            this.facility = facility;
        }

        public readonly BuildableObject facility;
        public int visits;
        public int incidents;
        public int revenue;
    }
}
