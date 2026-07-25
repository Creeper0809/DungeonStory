using System;
using System.Collections.Generic;
using UnityEngine;

public static class FacilityEvolutionTerms
{
    public const string Dining = "Dining";
    public const string Cooking = "Cooking";
    public const string Meat = "Meat";
    public const string Luxury = "Luxury";
    public const string Service = "Service";
    public const string Training = "Training";
    public const string Combat = "Combat";
    public const string Defense = "Defense";
    public const string Storage = "Storage";
    public const string Hygiene = "Hygiene";
    public const string Rest = "Rest";
    public const string Research = "Research";
    public const string Mana = "Mana";
    public const string Logistics = "Logistics";
    public const string Brutal = "Brutal";
    public const string Outlaw = "Outlaw";
    public const string Noble = "Noble";
    public const string Sacred = "Sacred";
    public const string Fear = "Fear";
    public const string Crowd = "Crowd";
    public const string Quiet = "Quiet";
    public const string Security = "Security";
    public const string Ritual = "Ritual";

    public const string RoomArea = "RoomArea";
    public const string DoorCount = "DoorCount";
    public const string FurnitureCount = "FurnitureCount";
    public const string SeatCount = "SeatCount";
    public const string TableCount = "TableCount";
    public const string LargeTableCount = "LargeTableCount";
    public const string CounterCount = "CounterCount";
    public const string PrivateSeatCount = "PrivateSeatCount";
    public const string SeatDensity = "SeatDensity";
    public const string TableDensity = "TableDensity";
    public const string SeatPerTable = "SeatPerTable";
    public const string LargeTableRatio = "LargeTableRatio";
    public const string CounterRatio = "CounterRatio";
    public const string PrivateSeatRatio = "PrivateSeatRatio";
    public const string AverageSeatSpacing = "AverageSeatSpacing";
    public const string LuxuryPerSeat = "LuxuryPerSeat";
    public const string ServiceScorePerSeat = "ServiceScorePerSeat";
    public const string CookingScorePerSeat = "CookingScorePerSeat";
    public const string DecorToUtilityRatio = "DecorToUtilityRatio";
    public const string ClutterScore = "ClutterScore";
    public const string VisitCount = "VisitCount";
    public const string UniqueVisitorCount = "UniqueVisitorCount";
    public const string RepeatVisitorRatio = "RepeatVisitorRatio";
    public const string AverageSatisfaction = "AverageSatisfaction";
    public const string AverageWaitTime = "AverageWaitTime";
    public const string CombatVisitorRatio = "CombatVisitorRatio";
    public const string NobleVisitorRatio = "NobleVisitorRatio";
    public const string CriminalVisitorRatio = "CriminalVisitorRatio";
    public const string RegularCustomerRatio = "RegularCustomerRatio";
    public const string AverageSpend = "AverageSpend";
    public const string AverageSpendByArchetype = "AverageSpendByArchetype";
    public const string TurnoverRate = "TurnoverRate";
    public const string AverageStayDuration = "AverageStayDuration";
    public const string FailedVisitCount = "FailedVisitCount";
    public const string NoPathFailureCount = "NoPathFailureCount";
    public const string StockoutCount = "StockoutCount";
    public const string MaintenanceDowntime = "MaintenanceDowntime";
    public const string StaffedRatio = "StaffedRatio";
    public const string PrimaryWorkerSkill = "PrimaryWorkerSkill";
    public const string WorkerMoodAverage = "WorkerMoodAverage";
    public const string WorkerStressAverage = "WorkerStressAverage";
    public const string ServiceQuality = "ServiceQuality";
    public const string RepairResponseTime = "RepairResponseTime";
    public const string CleaningFrequency = "CleaningFrequency";
    public const string TotalRevenue = "TotalRevenue";
    public const string ProfitPerVisit = "ProfitPerVisit";
    public const string StockCostPerVisit = "StockCostPerVisit";
    public const string PremiumItemRatio = "PremiumItemRatio";
    public const string DiscountUseRatio = "DiscountUseRatio";
    public const string WasteRatio = "WasteRatio";
    public const string HighValueTransactionCount = "HighValueTransactionCount";
    public const string BrawlCount = "BrawlCount";
    public const string CrimeCount = "CrimeCount";
    public const string TheftCount = "TheftCount";
    public const string VandalismCount = "VandalismCount";
    public const string StaffConflictCount = "StaffConflictCount";
    public const string IntruderDamageDealt = "IntruderDamageDealt";
    public const string IntruderDelayTime = "IntruderDelayTime";
    public const string FacilityDamageTaken = "FacilityDamageTaken";
    public const string RepairCount = "RepairCount";
    public const string DeathOrSevereIncidentCount = "DeathOrSevereIncidentCount";
    public const string DistanceToEntrance = "DistanceToEntrance";
    public const string DistanceToStorage = "DistanceToStorage";
    public const string DistanceToTraining = "DistanceToTraining";
    public const string DistanceToGuardPost = "DistanceToGuardPost";
    public const string TrafficThroughRoom = "TrafficThroughRoom";
    public const string ZoneSafetyScore = "ZoneSafetyScore";
    public const string PositiveMentionCount = "PositiveMentionCount";
    public const string NegativeMentionCount = "NegativeMentionCount";

    public const string MercenaryHangout = "MercenaryHangout";
    public const string HighMeatConsumption = "HighMeatConsumption";
    public const string FrequentBrawls = "FrequentBrawls";
    public const string NoblePatronage = "NoblePatronage";
    public const string CleanServiceStreak = "CleanServiceStreak";
    public const string GuardRallyPoint = "GuardRallyPoint";
    public const string IntruderBloodied = "IntruderBloodied";
    public const string CrowdedDining = "CrowdedDining";
    public const string QuietFineDining = "QuietFineDining";
    public const string OutlawRumor = "OutlawRumor";
    public const string HighTurnoverService = "HighTurnoverService";
}

public static class FacilityEvolutionMetricOwnership
{
    private static readonly HashSet<string> StructureOwnedMetrics = new HashSet<string>(StringComparer.Ordinal)
    {
        FacilityEvolutionTerms.RoomArea,
        FacilityEvolutionTerms.DoorCount,
        FacilityEvolutionTerms.SeatCount,
        FacilityEvolutionTerms.TableCount,
        FacilityEvolutionTerms.LargeTableCount,
        FacilityEvolutionTerms.CounterCount,
        FacilityEvolutionTerms.PrivateSeatCount
    };

    private static readonly HashSet<string> DerivedRoomMetrics = new HashSet<string>(StringComparer.Ordinal)
    {
        FacilityEvolutionTerms.FurnitureCount,
        FacilityEvolutionTerms.SeatDensity,
        FacilityEvolutionTerms.TableDensity,
        FacilityEvolutionTerms.SeatPerTable,
        FacilityEvolutionTerms.LargeTableRatio,
        FacilityEvolutionTerms.CounterRatio,
        FacilityEvolutionTerms.PrivateSeatRatio,
        FacilityEvolutionTerms.AverageSeatSpacing,
        FacilityEvolutionTerms.LuxuryPerSeat,
        FacilityEvolutionTerms.ServiceScorePerSeat,
        FacilityEvolutionTerms.CookingScorePerSeat,
        FacilityEvolutionTerms.DecorToUtilityRatio,
        FacilityEvolutionTerms.ClutterScore
    };

    public static bool IsStructureOwned(string key)
    {
        return !string.IsNullOrWhiteSpace(key) && StructureOwnedMetrics.Contains(key);
    }

    public static bool IsDerivedRoomMetric(string key)
    {
        return !string.IsNullOrWhiteSpace(key) && DerivedRoomMetrics.Contains(key);
    }

    public static bool IsRoomOwned(string key)
    {
        return IsStructureOwned(key) || IsDerivedRoomMetric(key);
    }
}

[Serializable]
public struct FacilityEvolutionValue
{
    public string key;
    public float value;

    public FacilityEvolutionValue(string key, float value)
    {
        this.key = key;
        this.value = value;
    }
}

[Serializable]
public struct FacilityEvolutionTokenValue
{
    public string key;
    public int count;

    public FacilityEvolutionTokenValue(string key, int count)
    {
        this.key = key;
        this.count = count;
    }
}

[Serializable]
public class FacilityEvolutionContributionData
{
    public bool contributesToRoomProfile = true;
    public string[] tags = Array.Empty<string>();
    public FacilityEvolutionValue[] scores = Array.Empty<FacilityEvolutionValue>();
    public FacilityEvolutionValue[] metrics = Array.Empty<FacilityEvolutionValue>();

    public bool HasExplicitData =>
        (tags != null && tags.Length > 0)
        || (scores != null && scores.Length > 0)
        || (metrics != null && metrics.Length > 0);
}

[Serializable]
public struct FacilityEvolutionMetricRequirement
{
    public string key;
    public bool requireMin;
    public float minValue;
    public bool requireMax;
    public float maxValue;

    public bool IsSatisfied(IReadOnlyDictionary<string, float> values, out string reason)
    {
        float current = 0f;
        if (values == null || string.IsNullOrWhiteSpace(key) || !values.TryGetValue(key, out current))
        {
            current = 0f;
        }

        if (requireMin && current < minValue)
        {
            reason = $"{key} {current:0.##}/{minValue:0.##}";
            return false;
        }

        if (requireMax && current > maxValue)
        {
            reason = $"{key} {current:0.##}>{maxValue:0.##}";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}

[Serializable]
public struct FacilityEvolutionTokenRequirement
{
    public string key;
    [Min(1)] public int minCount;

    public bool IsSatisfied(IReadOnlyDictionary<string, int> values, out string reason)
    {
        int current = 0;
        if (values == null || string.IsNullOrWhiteSpace(key) || !values.TryGetValue(key, out current))
        {
            current = 0;
        }

        int required = Mathf.Max(1, minCount);
        if (current < required)
        {
            reason = $"{key} {current}/{required}";
            return false;
        }

        reason = string.Empty;
        return true;
    }
}

[Serializable]
public struct FacilityEvolutionMaterialRequirement
{
    public string materialId;
    [Min(1)] public int amount;
}
