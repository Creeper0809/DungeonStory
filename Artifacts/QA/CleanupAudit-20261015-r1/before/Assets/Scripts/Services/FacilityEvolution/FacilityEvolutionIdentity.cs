using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public interface IFacilityIdentityPressureProvider
{
    void Apply(RoomProfile profile);
}

public readonly struct FacilityEvolutionIdentityScore
{
    public FacilityEvolutionIdentityScore(
        bool usesIdentityPressure,
        float score,
        float minimumScore,
        IReadOnlyList<string> contributions)
    {
        UsesIdentityPressure = usesIdentityPressure;
        Score = Mathf.Clamp01(score);
        MinimumScore = Mathf.Clamp01(minimumScore);
        Contributions = contributions ?? Array.Empty<string>();
    }

    public bool UsesIdentityPressure { get; }
    public float Score { get; }
    public float MinimumScore { get; }
    public IReadOnlyList<string> Contributions { get; }
    public bool Approved => !UsesIdentityPressure || Score >= MinimumScore;

    public string ToMessage()
    {
        if (!UsesIdentityPressure)
        {
            return "정체성 조건 없음";
        }

        string detail = Contributions != null && Contributions.Count > 0
            ? $" ({string.Join(", ", Contributions.Take(4))})"
            : string.Empty;
        return $"정체성 {Score:0.##}/{MinimumScore:0.##}{detail}";
    }
}

public sealed class FacilityIdentitySnapshot
{
    public FacilityIdentitySnapshot(FacilityEvolutionContext context)
    {
        FacilityName = FacilityShopService.GetBuildingName(context?.Facility != null
            ? context.Facility.BuildingData
            : null);
        CurrentFacilityId = FacilityEvolutionUtility.GetFacilityId(context?.Facility != null
            ? context.Facility.BuildingData
            : null);
        StarGrade = context?.State != null ? context.State.StarGrade : 1;
        LineageTags = context?.State != null
            ? context.State.LineageTags.ToArray()
            : Array.Empty<string>();
        RoomSummary = BuildRoomSummary(context?.Profile);
        TopMetrics = TopPairs(context?.Profile != null ? context.Profile.Metrics : null);
        TopRecordTokens = TopTokenPairs(context?.Profile != null ? context.Profile.RecordTokens : null);
        IdentityPressures = TopPairs(context?.Profile != null ? context.Profile.IdentityPressures : null);
        DominantSignals = context?.Profile != null
            ? context.Profile.DominantSignals.Take(8).ToArray()
            : Array.Empty<string>();
        ConflictingSignals = context?.Profile != null
            ? context.Profile.ConflictingSignals.Take(8).ToArray()
            : Array.Empty<string>();
        RecentEventSummary = context?.Profile != null
            ? context.Profile.RecentEvents.Take(6).ToArray()
            : Array.Empty<string>();
        CandidateEvolutionIds = context?.CandidateRecipes != null
            ? context.CandidateRecipes
                .Where((recipe) => recipe != null)
                .Select((recipe) => recipe.EffectiveId)
                .ToArray()
            : Array.Empty<string>();
    }

    public string FacilityName { get; }
    public string CurrentFacilityId { get; }
    public int StarGrade { get; }
    public IReadOnlyList<string> LineageTags { get; }
    public string RoomSummary { get; }
    public IReadOnlyDictionary<string, float> TopMetrics { get; }
    public IReadOnlyDictionary<string, int> TopRecordTokens { get; }
    public IReadOnlyDictionary<string, float> IdentityPressures { get; }
    public IReadOnlyList<string> DominantSignals { get; }
    public IReadOnlyList<string> ConflictingSignals { get; }
    public IReadOnlyList<string> RecentEventSummary { get; }
    public IReadOnlyList<string> CandidateEvolutionIds { get; }

    private static string BuildRoomSummary(RoomProfile profile)
    {
        if (profile == null)
        {
            return "no room profile";
        }

        return $"usable={profile.IsUsable} closed={profile.IsClosed} door={profile.HasDoor} area={profile.Area}";
    }

    private static IReadOnlyDictionary<string, float> TopPairs(IReadOnlyDictionary<string, float> values)
    {
        if (values == null || values.Count == 0)
        {
            return new Dictionary<string, float>();
        }

        return values
            .Where((entry) => !Mathf.Approximately(entry.Value, 0f))
            .OrderByDescending((entry) => Mathf.Abs(entry.Value))
            .Take(12)
            .ToDictionary((entry) => entry.Key, (entry) => entry.Value);
    }

    private static IReadOnlyDictionary<string, int> TopTokenPairs(IReadOnlyDictionary<string, int> values)
    {
        if (values == null || values.Count == 0)
        {
            return new Dictionary<string, int>();
        }

        return values
            .Where((entry) => entry.Value != 0)
            .OrderByDescending((entry) => entry.Value)
            .Take(12)
            .ToDictionary((entry) => entry.Key, (entry) => entry.Value);
    }
}

public sealed class DefaultFacilityIdentityPressureProvider : IFacilityIdentityPressureProvider
{
    public void Apply(RoomProfile profile)
    {
        FacilityIdentityPressureUtility.ApplyDefaultPressures(profile);
    }
}

public static class FacilityIdentityPressureUtility
{
    public static void ApplyDefaultPressures(RoomProfile profile)
    {
        if (profile == null)
        {
            return;
        }

        ApplyPressure(profile, FacilityEvolutionTerms.Crowd, CrowdSignals(profile));
        ApplyPressure(profile, FacilityEvolutionTerms.Luxury, LuxurySignals(profile));
        ApplyPressure(profile, FacilityEvolutionTerms.Combat, CombatSignals(profile));
        ApplyPressure(profile, FacilityEvolutionTerms.Outlaw, OutlawSignals(profile));
        ApplyPressure(profile, FacilityEvolutionTerms.Rest, RestSignals(profile));
        ApplyPressure(profile, FacilityEvolutionTerms.Service, ServiceSignals(profile));
        ApplyPressure(profile, FacilityEvolutionTerms.Ritual, RitualSignals(profile));
        ApplyPressure(profile, FacilityEvolutionTerms.Security, SecuritySignals(profile));
        ApplyPressure(profile, FacilityEvolutionTerms.Logistics, LogisticsSignals(profile));

        DetectConflicts(profile);
    }

    public static FacilityEvolutionIdentityScore ScoreRecipe(
        RoomProfile profile,
        FacilityEvolutionRecipeSO recipe)
    {
        if (profile == null || recipe == null)
        {
            return new FacilityEvolutionIdentityScore(false, 0f, 0f, Array.Empty<string>());
        }

        FacilityEvolutionValue[] weights = recipe.identityPressureWeights ?? Array.Empty<FacilityEvolutionValue>();
        bool usesIdentity = recipe.minimumIdentityScore > 0f
            || weights.Any((weight) => !string.IsNullOrWhiteSpace(weight.key)
                && !Mathf.Approximately(weight.value, 0f));
        if (!usesIdentity)
        {
            return new FacilityEvolutionIdentityScore(false, 1f, 0f, Array.Empty<string>());
        }

        float positiveWeight = 0f;
        float weighted = 0f;
        List<string> contributions = new List<string>();
        foreach (FacilityEvolutionValue weight in weights)
        {
            if (string.IsNullOrWhiteSpace(weight.key) || Mathf.Approximately(weight.value, 0f))
            {
                continue;
            }

            float pressure = profile.GetIdentityPressure(weight.key);
            weighted += pressure * weight.value;
            if (weight.value > 0f)
            {
                positiveWeight += weight.value;
            }

            if (pressure > 0.01f)
            {
                string prefix = weight.value >= 0f ? "+" : "-";
                contributions.Add($"{prefix}{weight.key}:{pressure:0.##}");
            }
        }

        float denominator = Mathf.Max(0.0001f, positiveWeight);
        float score = Mathf.Clamp01(weighted / denominator);
        return new FacilityEvolutionIdentityScore(
            true,
            score,
            recipe.minimumIdentityScore,
            contributions);
    }

    private static void ApplyPressure(RoomProfile profile, string key, IEnumerable<IdentitySignal> signals)
    {
        List<IdentitySignal> list = signals?
            .Where((signal) => signal.Value > 0.01f)
            .ToList()
            ?? new List<IdentitySignal>();
        if (list.Count == 0)
        {
            profile.SetIdentityPressure(key, 0f);
            return;
        }

        float score = Mathf.Clamp01(list.Average((signal) => signal.Value));
        profile.SetIdentityPressure(key, score);

        foreach (IdentitySignal signal in list
            .OrderByDescending((signal) => signal.Value)
            .Take(3))
        {
            profile.AddDominantSignal($"{key}:{signal.Label} {signal.Value:0.##}");
        }
    }

    private static IEnumerable<IdentitySignal> CrowdSignals(RoomProfile profile)
    {
        yield return Signal("seat density", Normalize(profile.GetMetric(FacilityEvolutionTerms.SeatDensity), 0.35f, 1.15f));
        yield return Signal("table density", Normalize(profile.GetMetric(FacilityEvolutionTerms.TableDensity), 0.12f, 0.45f));
        yield return Signal("turnover", profile.GetMetric(FacilityEvolutionTerms.TurnoverRate));
        yield return Signal("high turnover token", Token01(profile, FacilityEvolutionTerms.HighTurnoverService, 3));
        yield return Signal("low wait", InverseNormalize(profile.GetMetric(FacilityEvolutionTerms.AverageWaitTime), 20f, 90f));
    }

    private static IEnumerable<IdentitySignal> LuxurySignals(RoomProfile profile)
    {
        float coreLuxury = CoreLuxuryStrength(profile);
        yield return Signal("luxury score", Normalize(profile.GetScore(FacilityEvolutionTerms.Luxury), 8f, 40f));
        yield return Signal("luxury per seat", Normalize(profile.GetMetric(FacilityEvolutionTerms.LuxuryPerSeat), 0.8f, 4f));
        yield return Signal("service per seat", Normalize(profile.GetMetric(FacilityEvolutionTerms.ServiceScorePerSeat), 1f, 5f) * coreLuxury);
        yield return Signal("seat spacing", Normalize(profile.GetMetric(FacilityEvolutionTerms.AverageSeatSpacing), 1.5f, 5f) * coreLuxury);
        yield return Signal("private seats", profile.GetMetric(FacilityEvolutionTerms.PrivateSeatRatio) * coreLuxury);
        yield return Signal("noble visitors", profile.GetMetric(FacilityEvolutionTerms.NobleVisitorRatio));
        yield return Signal("average spend", Normalize(profile.GetMetric(FacilityEvolutionTerms.AverageSpend), 20f, 80f));
        yield return Signal("noble patronage", Token01(profile, FacilityEvolutionTerms.NoblePatronage, 3));
    }

    private static float CoreLuxuryStrength(RoomProfile profile)
    {
        return Mathf.Max(
            Normalize(profile.GetScore(FacilityEvolutionTerms.Luxury), 8f, 40f),
            Normalize(profile.GetMetric(FacilityEvolutionTerms.LuxuryPerSeat), 0.8f, 4f),
            profile.GetMetric(FacilityEvolutionTerms.NobleVisitorRatio),
            Normalize(profile.GetMetric(FacilityEvolutionTerms.AverageSpend), 20f, 80f),
            Token01(profile, FacilityEvolutionTerms.NoblePatronage, 3));
    }

    private static IEnumerable<IdentitySignal> CombatSignals(RoomProfile profile)
    {
        yield return Signal("combat score", Normalize(profile.GetScore(FacilityEvolutionTerms.Combat), 8f, 42f));
        yield return Signal("training score", Normalize(profile.GetScore(FacilityEvolutionTerms.Training), 8f, 35f));
        yield return Signal("combat visitors", profile.GetMetric(FacilityEvolutionTerms.CombatVisitorRatio));
        yield return Signal("mercenary hangout", Token01(profile, FacilityEvolutionTerms.MercenaryHangout, 4));
        yield return Signal("meat consumption", Token01(profile, FacilityEvolutionTerms.HighMeatConsumption, 5));
        yield return Signal("brawls", Token01(profile, FacilityEvolutionTerms.FrequentBrawls, 3));
        yield return Signal("intruder bloodied", Token01(profile, FacilityEvolutionTerms.IntruderBloodied, 3));
        yield return Signal("large tables", profile.GetMetric(FacilityEvolutionTerms.LargeTableRatio));
    }

    private static IEnumerable<IdentitySignal> OutlawSignals(RoomProfile profile)
    {
        yield return Signal("criminal visitors", profile.GetMetric(FacilityEvolutionTerms.CriminalVisitorRatio));
        yield return Signal("crime count", Normalize(profile.GetMetric(FacilityEvolutionTerms.CrimeCount), 1f, 4f));
        yield return Signal("theft count", Normalize(profile.GetMetric(FacilityEvolutionTerms.TheftCount), 1f, 3f));
        yield return Signal("outlaw rumor", Token01(profile, FacilityEvolutionTerms.OutlawRumor, 3));
        yield return Signal("negative mentions", Normalize(profile.GetMetric(FacilityEvolutionTerms.NegativeMentionCount), 1f, 5f));
        yield return Signal("vandalism", Normalize(profile.GetMetric(FacilityEvolutionTerms.VandalismCount), 1f, 3f));
    }

    private static IEnumerable<IdentitySignal> RestSignals(RoomProfile profile)
    {
        yield return Signal("rest score", Normalize(profile.GetScore(FacilityEvolutionTerms.Rest), 8f, 35f));
        yield return Signal("hygiene score", Normalize(profile.GetScore(FacilityEvolutionTerms.Hygiene), 8f, 35f));
        yield return Signal("stay duration", Normalize(profile.GetMetric(FacilityEvolutionTerms.AverageStayDuration), 25f, 160f));
        yield return Signal("satisfaction", Normalize(profile.GetMetric(FacilityEvolutionTerms.AverageSatisfaction), 55f, 95f));
        yield return Signal("quiet tag", profile.HasTag(FacilityEvolutionTerms.Quiet) ? 0.6f : 0f);
        yield return Signal("low seat density", InverseNormalize(profile.GetMetric(FacilityEvolutionTerms.SeatDensity), 0.4f, 1.1f));
    }

    private static IEnumerable<IdentitySignal> ServiceSignals(RoomProfile profile)
    {
        yield return Signal("service score", Normalize(profile.GetScore(FacilityEvolutionTerms.Service), 5f, 25f));
        yield return Signal("service per seat", Normalize(profile.GetMetric(FacilityEvolutionTerms.ServiceScorePerSeat), 1f, 5f));
        yield return Signal("service quality", Normalize(profile.GetMetric(FacilityEvolutionTerms.ServiceQuality), 40f, 100f));
        yield return Signal("satisfaction", Normalize(profile.GetMetric(FacilityEvolutionTerms.AverageSatisfaction), 55f, 95f));
        yield return Signal("clean streak", Token01(profile, FacilityEvolutionTerms.CleanServiceStreak, 3));
        yield return Signal("few stockouts", InverseNormalize(profile.GetMetric(FacilityEvolutionTerms.StockoutCount), 0f, 3f));
    }

    private static IEnumerable<IdentitySignal> RitualSignals(RoomProfile profile)
    {
        yield return Signal("mana score", Normalize(profile.GetScore(FacilityEvolutionTerms.Mana), 8f, 38f));
        yield return Signal("research score", Normalize(profile.GetScore(FacilityEvolutionTerms.Research), 8f, 38f));
        yield return Signal("sacred score", Normalize(profile.GetScore(FacilityEvolutionTerms.Sacred), 5f, 25f));
        yield return Signal("fear score", Normalize(profile.GetScore(FacilityEvolutionTerms.Fear), 5f, 25f));
        yield return Signal("ritual tag", profile.HasTag(FacilityEvolutionTerms.Ritual) ? 0.8f : 0f);
    }

    private static IEnumerable<IdentitySignal> SecuritySignals(RoomProfile profile)
    {
        yield return Signal("defense score", Normalize(profile.GetScore(FacilityEvolutionTerms.Defense), 8f, 40f));
        yield return Signal("security tag", profile.HasTag(FacilityEvolutionTerms.Security) ? 0.8f : 0f);
        yield return Signal("guard rally", Token01(profile, FacilityEvolutionTerms.GuardRallyPoint, 3));
        yield return Signal("intruder bloodied", Token01(profile, FacilityEvolutionTerms.IntruderBloodied, 3));
        yield return Signal("zone safety", Normalize(profile.GetMetric(FacilityEvolutionTerms.ZoneSafetyScore), 40f, 100f));
        yield return Signal("guard distance", InverseNormalize(profile.GetMetric(FacilityEvolutionTerms.DistanceToGuardPost), 2f, 10f));
    }

    private static IEnumerable<IdentitySignal> LogisticsSignals(RoomProfile profile)
    {
        yield return Signal("storage score", Normalize(profile.GetScore(FacilityEvolutionTerms.Storage), 8f, 35f));
        yield return Signal("logistics tag", profile.HasTag(FacilityEvolutionTerms.Logistics) ? 0.8f : 0f);
        yield return Signal("stock cost", Normalize(profile.GetMetric(FacilityEvolutionTerms.StockCostPerVisit), 0.2f, 1.5f));
        yield return Signal("low stockouts", InverseNormalize(profile.GetMetric(FacilityEvolutionTerms.StockoutCount), 0f, 4f));
        yield return Signal("storage distance", InverseNormalize(profile.GetMetric(FacilityEvolutionTerms.DistanceToStorage), 2f, 12f));
    }

    private static void DetectConflicts(RoomProfile profile)
    {
        AddConflict(profile, FacilityEvolutionTerms.Crowd, FacilityEvolutionTerms.Luxury, 0.55f);
        AddConflict(profile, FacilityEvolutionTerms.Service, FacilityEvolutionTerms.Outlaw, 0.45f);
        AddConflict(profile, FacilityEvolutionTerms.Rest, FacilityEvolutionTerms.Combat, 0.6f);
    }

    private static void AddConflict(RoomProfile profile, string a, string b, float threshold)
    {
        float av = profile.GetIdentityPressure(a);
        float bv = profile.GetIdentityPressure(b);
        if (av >= threshold && bv >= threshold)
        {
            profile.AddConflictingSignal($"{a} {av:0.##} vs {b} {bv:0.##}");
        }
    }

    private static IdentitySignal Signal(string label, float value)
    {
        return new IdentitySignal(label, Mathf.Clamp01(value));
    }

    private static float Token01(RoomProfile profile, string key, int atCount)
    {
        return Mathf.Clamp01(profile.GetToken(key) / Mathf.Max(1f, atCount));
    }

    private static float Normalize(float value, float min, float max)
    {
        if (max <= min)
        {
            return value >= min ? 1f : 0f;
        }

        return Mathf.Clamp01((value - min) / (max - min));
    }

    private static float InverseNormalize(float value, float best, float worst)
    {
        if (Mathf.Approximately(value, 0f))
        {
            return 0f;
        }

        if (worst <= best)
        {
            return value <= best ? 1f : 0f;
        }

        return Mathf.Clamp01(1f - ((value - best) / (worst - best)));
    }

    private readonly struct IdentitySignal
    {
        public IdentitySignal(string label, float value)
        {
            Label = label ?? string.Empty;
            Value = Mathf.Clamp01(value);
        }

        public string Label { get; }
        public float Value { get; }
    }
}
