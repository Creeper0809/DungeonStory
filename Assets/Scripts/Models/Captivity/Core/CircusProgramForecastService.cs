using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CircusProgramForecastService
{
    public CircusProgramForecast GetForecast(
        ICircusProgramHandler handler,
        CircusProgramForecastContext context,
        CircusLethalityPolicy lethality)
    {
        if (handler == null)
        {
            return Unavailable("공연 프로그램을 찾을 수 없습니다.");
        }

        if (context == null)
        {
            return Unavailable("공연 예측 입력을 만들 수 없습니다.");
        }

        CircusProgramModule definition = handler.Definition;
        IReadOnlyList<CaptiveState> performers = context.Performers;
        IReadOnlyList<string> animals = context.WildlifeIds;
        CircusVenueModifiers venue = context.Venue;
        CircusShowOrder candidate = new CircusShowOrder
        {
            programId = definition.programId,
            lethality = lethality,
            performerIds = performers.Select(item => item.captiveId).ToList(),
            wildlifeIds = animals.ToList(),
            venueSatisfactionBonus = venue.SatisfactionBonus,
            venueAccidentRiskBonus = venue.AccidentRiskBonus,
            venueGamblingVariance = venue.GamblingVariance
        };
        bool valid = handler.Validate(candidate, performers, out string failureReason);
        int ticketPrice = Mathf.Max(
            1,
            Mathf.RoundToInt(context.BaseTicketPrice * venue.RevenueMultiplier));
        float skill = performers
            .Select(item => item.performerSkill)
            .DefaultIfEmpty(0f)
            .Average();
        float centerSatisfaction = Mathf.Clamp(
            definition.baseAudienceSatisfaction
            + skill * 0.12f
            + venue.SatisfactionBonus,
            0f,
            100f);
        float satisfactionVariance = Mathf.Max(0f, venue.GamblingVariance);
        float accidentChance = Mathf.Clamp01(
            definition.baseAccidentRisk + venue.AccidentRiskBonus);
        float injuryChance = definition.usesCombat
            ? Mathf.Max(0.25f, accidentChance)
            : accidentChance;
        float deathChance = lethality switch
        {
            CircusLethalityPolicy.FightToDeath => 1f,
            CircusLethalityPolicy.ExecuteDesignatedTarget => 1f,
            CircusLethalityPolicy.AllowAccidents => injuryChance * 0.2f,
            _ => 0f
        };
        float fame = Mathf.Max(1f, definition.basePerformerFame);
        string requirement =
            $"포로 {(definition.requiresCaptive ? "필수" : "선택")}"
            + $" · 야생동물 {(definition.requiresWildlife ? "필수" : "선택")}"
            + $" · 현재 포로 {performers.Count}명, 야생동물 {animals.Count}마리";
        return new CircusProgramForecast(
            context.AudienceCount * (ticketPrice + venue.FlatRevenuePerAudience),
            centerSatisfaction - satisfactionVariance,
            centerSatisfaction + satisfactionVariance,
            accidentChance,
            definition.publiclyCruel ? 0f : fame,
            definition.publiclyCruel ? Mathf.Max(3f, fame) : 0f,
            definition.publiclyCruel ? Mathf.Max(1f, fame * 0.35f) : 0f,
            injuryChance,
            deathChance,
            valid,
            requirement,
            valid ? string.Empty : failureReason);
    }

    public static CircusProgramForecast Unavailable(string reason) =>
        new CircusProgramForecast(
            0,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            0f,
            false,
            string.Empty,
            reason ?? string.Empty);
}

public sealed class CircusProgramForecastContext
{
    public CircusProgramForecastContext(
        int baseTicketPrice,
        int audienceCount,
        CircusVenueModifiers venue,
        IReadOnlyList<CaptiveState> performers,
        IReadOnlyList<string> wildlifeIds)
    {
        BaseTicketPrice = Mathf.Max(1, baseTicketPrice);
        AudienceCount = Mathf.Max(0, audienceCount);
        Venue = venue;
        Performers = performers ?? Array.Empty<CaptiveState>();
        WildlifeIds = wildlifeIds ?? Array.Empty<string>();
    }

    public int BaseTicketPrice { get; }
    public int AudienceCount { get; }
    public CircusVenueModifiers Venue { get; }
    public IReadOnlyList<CaptiveState> Performers { get; }
    public IReadOnlyList<string> WildlifeIds { get; }
}

public struct CircusVenueModifiers
{
    public float RevenueMultiplier;
    public int FlatRevenuePerAudience;
    public float SatisfactionBonus;
    public float GamblingVariance;
    public float PreparationWorkMultiplier;
    public float AccidentRiskBonus;
    public float AccidentDamageMultiplier;
    public float FilthMultiplier;
    public float WitnessMoodPenalty;

    public static CircusVenueModifiers Default => new CircusVenueModifiers
    {
        RevenueMultiplier = 1f,
        PreparationWorkMultiplier = 1f,
        AccidentDamageMultiplier = 1f,
        FilthMultiplier = 1f,
        WitnessMoodPenalty = 3f
    };
}
