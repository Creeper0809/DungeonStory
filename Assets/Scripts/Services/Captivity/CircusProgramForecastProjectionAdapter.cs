using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

internal delegate bool TryGetCircusRoom(
    BuildableObject stage,
    out RoomInstance room,
    out string failureReason);

internal sealed class CircusProgramForecastProjectionAdapter
{
    private readonly ICaptivityRuntime captivity;
    private readonly IWildlifeCaptureRuntime wildlifeCapture;
    private readonly TryGetCircusRoom tryGetRoom;
    private readonly Func<RoomInstance, IEnumerable<CharacterActor>> selectAudience;

    public CircusProgramForecastProjectionAdapter(
        ICaptivityRuntime captivity,
        IWildlifeCaptureRuntime wildlifeCapture,
        TryGetCircusRoom tryGetRoom,
        Func<RoomInstance, IEnumerable<CharacterActor>> selectAudience)
    {
        this.captivity = captivity ?? throw new ArgumentNullException(nameof(captivity));
        this.wildlifeCapture = wildlifeCapture
            ?? throw new ArgumentNullException(nameof(wildlifeCapture));
        this.tryGetRoom = tryGetRoom ?? throw new ArgumentNullException(nameof(tryGetRoom));
        this.selectAudience = selectAudience
            ?? throw new ArgumentNullException(nameof(selectAudience));
    }

    public bool TryProject(
        BuildableObject stage,
        bool publiclyCruel,
        IReadOnlyList<string> performerIds,
        IReadOnlyList<string> wildlifeIds,
        out CircusProgramForecastContext context,
        out string failureReason)
    {
        context = null;
        BuildingCircusStageAbility stageAbility =
            stage?.BuildingData.GetCircusStageAbility();
        if (stage == null || stageAbility == null || !stageAbility.IsValid)
        {
            failureReason = "유효한 서커스 무대가 아닙니다.";
            return false;
        }

        if (!tryGetRoom(stage, out RoomInstance room, out failureReason))
        {
            return false;
        }

        List<CaptiveState> performers = (performerIds ?? Array.Empty<string>())
            .Select(id => captivity.TryGetCaptive(id, out CaptiveState captive)
                ? captive
                : null)
            .Where(captive => captive != null && captive.IsActive)
            .Take(stageAbility.performerCapacity)
            .ToList();
        List<string> animals = (wildlifeIds ?? Array.Empty<string>())
            .Where(wildlifeCapture.IsCaptured)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        context = new CircusProgramForecastContext(
            stageAbility.baseTicketPrice,
            selectAudience(room).Count(),
            CircusVenueEvaluator.Evaluate(room, publiclyCruel),
            performers,
            animals);
        failureReason = string.Empty;
        return true;
    }
}

internal static class CircusVenueEvaluator
{
    public static CircusVenueModifiers Evaluate(
        RoomInstance room,
        bool publiclyCruel)
    {
        CircusVenueModifiers result = CircusVenueModifiers.Default;
        foreach (BuildableObject part in room?.Furniture
                     ?? Array.Empty<BuildableObject>())
        {
            BuildingSO data = part?.BuildingData;
            if (data?.ResearchFacilityCommand ==
                ResearchFacilityCommandKind.BloodStageDrainage)
            {
                // The drain is a built venue part, so its benefit is earned by
                // placing it in this exact circus room rather than by merely
                // owning the research definition somewhere in the dungeon.
                result.FilthMultiplier *= 0.35f;
            }
            BuildingCircusTicketBoothAbility ticket =
                data.GetCircusTicketBoothAbility();
            if (ticket != null)
            {
                result.RevenueMultiplier *= Mathf.Max(1f, ticket.revenueMultiplier);
                result.FlatRevenuePerAudience += Mathf.Max(
                    0,
                    ticket.flatRevenuePerAudience);
            }

            BuildingCircusGamblingAbility gambling =
                data.GetCircusGamblingAbility();
            if (gambling != null)
            {
                result.FlatRevenuePerAudience += Mathf.Max(
                    0,
                    gambling.revenuePerAudience);
                result.GamblingVariance += Mathf.Max(
                    0f,
                    gambling.satisfactionVariance);
            }

            BuildingCircusAnnouncerAbility announcer =
                data.GetCircusAnnouncerAbility();
            if (announcer != null)
            {
                result.SatisfactionBonus += Mathf.Max(
                    0f,
                    announcer.satisfactionBonus);
                result.PreparationWorkMultiplier *= Mathf.Clamp(
                    announcer.preparationWorkMultiplier,
                    0.5f,
                    1f);
            }

            BuildingCircusHazardAbility hazard = data.GetCircusHazardAbility();
            if (hazard != null)
            {
                result.AccidentRiskBonus += Mathf.Max(
                    0f,
                    hazard.accidentRiskBonus);
                result.SatisfactionBonus += Mathf.Max(
                    0f,
                    hazard.satisfactionBonus);
            }

            BuildingCircusTreatmentZoneAbility treatment =
                data.GetCircusTreatmentZoneAbility();
            if (treatment != null)
            {
                result.AccidentDamageMultiplier *= Mathf.Clamp(
                    treatment.accidentDamageMultiplier,
                    0.25f,
                    1f);
            }

            BuildingPublicPunishmentAbility punishment =
                data.GetPublicPunishmentAbility();
            if (publiclyCruel && punishment != null)
            {
                result.SatisfactionBonus += Mathf.Max(
                    0f,
                    punishment.cruelSatisfactionBonus);
                result.FilthMultiplier *= Mathf.Max(1f, punishment.filthMultiplier);
                result.WitnessMoodPenalty = Mathf.Max(
                    result.WitnessMoodPenalty,
                    punishment.witnessMoodPenalty);
            }
        }

        result.RevenueMultiplier = Mathf.Clamp(result.RevenueMultiplier, 1f, 2.5f);
        result.SatisfactionBonus = Mathf.Clamp(result.SatisfactionBonus, 0f, 35f);
        result.AccidentRiskBonus = Mathf.Clamp(result.AccidentRiskBonus, 0f, 0.5f);
        result.AccidentDamageMultiplier = Mathf.Clamp(
            result.AccidentDamageMultiplier,
            0.25f,
            1f);
        return result;
    }
}
