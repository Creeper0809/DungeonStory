#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class StrategicContentBalanceCalibrationScenario
{
    private const string ReportPath =
        "Artifacts/QA/strategic-content-balance.txt";

    [MenuItem("DungeonStory/Balance/Run Strategic Content Calibration")]
    public static void RunFromMenu()
    {
        Debug.Log(Run());
    }

    public static string Run()
    {
        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        EmbeddedWorkValueSnapshot embeddedWork = CreateEmbeddedWork(content);
        FactionContractDefinitionSO[] contracts = content
            .GetAll<FactionContractDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.factionId, StringComparer.Ordinal)
            .ThenBy(value => value.kind)
            .ToArray();
        FactionChapterDefinitionSO[] chapters = content
            .GetAll<FactionChapterDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.factionId, StringComparer.Ordinal)
            .ThenBy(value => value.chapterNumber)
            .ToArray();
        EndingDefinitionSO[] milestones = content
            .GetAll<EndingDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.tier)
            .ThenBy(value => value.StableId, StringComparer.Ordinal)
            .ToArray();
        SeasonalWorldEventDefinitionSO[] seasonalEvents = content
            .GetAll<SeasonalWorldEventDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.season)
            .ThenBy(value => value.StableId, StringComparer.Ordinal)
            .ToArray();
        FestivalDefinitionSO[] festivals = content
            .GetAll<FestivalDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.season)
            .ThenBy(value => value.dayOfSeason)
            .ThenBy(value => value.StableId, StringComparer.Ordinal)
            .ToArray();
        ServiceIncidentDefinitionSO[] incidents = content
            .GetAll<ServiceIncidentDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.kind)
            .ThenBy(value => value.StableId, StringComparer.Ordinal)
            .ToArray();

        List<string> failures = new();
        StringBuilder report = new();
        report.AppendLine("STRATEGIC_CONTENT_BALANCE_V1");
        report.AppendLine(
            $"contracts={contracts.Length}; chapters={chapters.Length}; milestones={milestones.Length}");
        report.AppendLine("CONTRACTS");
        report.AppendLine(
            "id | faction | kind | deadline | item EWU | reference EWU | burden | target band");
        Require(contracts.Length == 18,
            $"Expected 18 contracts, found {contracts.Length}.", failures);
        foreach (FactionContractDefinitionSO contract in contracts)
        {
            float itemEwu = RequirementWork(
                contract.completionRequirements,
                embeddedWork,
                contract.StableId,
                failures);
            float reference = AuthoredFactionContractBalanceRules
                .CalculateReferenceProduction(contract.deadlineDays);
            float burden = itemEwu / Mathf.Max(1f, reference);
            Vector2 band = AuthoredFactionContractBalanceRules
                .BurdenBand(contract.kind);
            report.AppendLine(string.Join(" | ",
                contract.StableId,
                contract.factionId,
                contract.kind,
                contract.deadlineDays.ToString(CultureInfo.InvariantCulture),
                itemEwu.ToString("0.00", CultureInfo.InvariantCulture),
                reference.ToString("0.00", CultureInfo.InvariantCulture),
                burden.ToString("P2", CultureInfo.InvariantCulture),
                $"{band.x:P0}-{band.y:P0}"));
        }

        report.AppendLine("CHAPTER_CHOICES");
        report.AppendLine(
            "id | chapter | support EWU | bargain EWU | bargain/support");
        Require(chapters.Length == 36,
            $"Expected 36 chapters, found {chapters.Length}.", failures);
        foreach (FactionChapterDefinitionSO chapter in chapters)
        {
            V20ChoiceDefinition support = FindChoice(chapter, "support");
            V20ChoiceDefinition bargain = FindChoice(chapter, "bargain");
            float supportEwu = RequirementWork(
                support?.requirements,
                embeddedWork,
                chapter.StableId + ":support",
                failures);
            float bargainEwu = RequirementWork(
                bargain?.requirements,
                embeddedWork,
                chapter.StableId + ":bargain",
                failures);
            float ratio = bargainEwu / Mathf.Max(0.01f, supportEwu);
            Require(supportEwu > 0f && bargainEwu > 0f,
                $"{chapter.StableId} support/bargain has no priced physical burden.",
                failures);
            Require(ratio is >= 0.30f and <= 0.75f,
                $"{chapter.StableId} bargain burden is {ratio:P1} of support; expected 30-75%.",
                failures);
            report.AppendLine(string.Join(" | ",
                chapter.StableId,
                chapter.chapterNumber.ToString(CultureInfo.InvariantCulture),
                supportEwu.ToString("0.00", CultureInfo.InvariantCulture),
                bargainEwu.ToString("0.00", CultureInfo.InvariantCulture),
                ratio.ToString("P1", CultureInfo.InvariantCulture)));
        }

        report.AppendLine("MILESTONES");
        report.AppendLine("id | tier | requirements | landmark | reward | pressure");
        Require(milestones.Length == 9,
            $"Expected 9 milestones, found {milestones.Length}.", failures);
        foreach (EndingDefinitionSO milestone in milestones)
        {
            int requirementCount = CountRequirements(
                milestone.completionRequirements);
            Require(requirementCount >= 2,
                $"{milestone.StableId} has fewer than two independent requirements.",
                failures);
            report.AppendLine(string.Join(" | ",
                milestone.StableId,
                milestone.tier,
                requirementCount.ToString(CultureInfo.InvariantCulture),
                milestone.landmarkBuildingId,
                EffectSummary(milestone.permanentRewards),
                EffectSummary(milestone.counterPressures)));
        }

        ValidateSeasonalEvents(seasonalEvents, report, failures);
        ValidateFestivals(festivals, embeddedWork, report, failures);
        ValidateServiceIncidents(incidents, report, failures);

        if (failures.Count > 0)
        {
            report.AppendLine("FAILURES");
            foreach (string failure in failures)
            {
                report.AppendLine("- " + failure);
            }
        }

        WriteReport(report.ToString());
        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                $"Strategic content balance calibration failed ({failures.Count}). See {ReportPath}.");
        }

        return $"STRATEGIC_CONTENT_BALANCE=PASS; contracts={contracts.Length}; "
            + $"chapters={chapters.Length}; milestones={milestones.Length}; "
            + $"seasonal={seasonalEvents.Length}; festivals={festivals.Length}; "
            + $"incidents={incidents.Length}";
    }

    private static void ValidateSeasonalEvents(
        IReadOnlyCollection<SeasonalWorldEventDefinitionSO> events,
        StringBuilder report,
        ICollection<string> failures)
    {
        report.AppendLine("SEASONAL_EVENTS");
        report.AppendLine("id | season | duration | domains | total severity");
        Require(events.Count == 28,
            $"Expected 28 seasonal events, found {events.Count}.", failures);
        foreach (Season season in Enum.GetValues(typeof(Season)))
        {
            Require(events.Count(value => value.season == season) == 7,
                $"{season} must contain exactly seven seasonal events.", failures);
        }

        foreach (SeasonalWorldEventDefinitionSO seasonal in events)
        {
            Require(seasonal.minimumDurationDays is >= 1 and <= 6
                    && seasonal.maximumDurationDays is >= 1 and <= 6
                    && seasonal.maximumDurationDays >= seasonal.minimumDurationDays,
                $"{seasonal.StableId} duration is outside 1-6 days.", failures);
            int domains = (seasonal.affectedDomainIds ?? new List<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Distinct(StringComparer.Ordinal)
                .Count();
            Require(domains >= 2,
                $"{seasonal.StableId} affects fewer than two domains.", failures);
            float severity = EffectSeverity(seasonal.startEffects, 1)
                + EffectSeverity(
                    seasonal.dailyEffects,
                    seasonal.maximumDurationDays)
                + EffectSeverity(seasonal.endEffects, 1);
            Require(severity is >= 0f and <= 12f,
                $"{seasonal.StableId} normalized severity {severity:0.00} exceeds 12.",
                failures);
            report.AppendLine(string.Join(" | ",
                seasonal.StableId,
                seasonal.season,
                $"{seasonal.minimumDurationDays}-{seasonal.maximumDurationDays}",
                domains.ToString(CultureInfo.InvariantCulture),
                severity.ToString("0.00", CultureInfo.InvariantCulture)));
        }
    }

    private static void ValidateFestivals(
        IReadOnlyCollection<FestivalDefinitionSO> festivals,
        EmbeddedWorkValueSnapshot embeddedWork,
        StringBuilder report,
        ICollection<string> failures)
    {
        report.AppendLine("FESTIVALS");
        report.AppendLine("id | season/day | participants | input EWU | EWU/participant | success mood-days");
        Require(festivals.Count == 16,
            $"Expected 16 festivals, found {festivals.Count}.", failures);
        foreach (FestivalDefinitionSO festival in festivals)
        {
            float inputEwu = 0f;
            foreach (FestivalItemRequirement item in festival.requiredItems
                ?? new List<FestivalItemRequirement>())
            {
                if (!TryGetFestivalItemWork(
                        item.itemDefinitionId,
                        embeddedWork,
                        out float ewu))
                {
                    failures.Add(
                        $"{festival.StableId} has unpriced festival item '{item.itemDefinitionId}'.");
                    continue;
                }
                inputEwu += ewu * item.amount;
            }
            float perParticipant = inputEwu
                / Mathf.Max(1, festival.minimumParticipants);
            Require(perParticipant is >= 5f and <= 80f,
                $"{festival.StableId} costs {perParticipant:0.00} EWU per participant; expected 5-80.",
                failures);
            Require(festival.successOutcome.moodDelta
                    > festival.partialOutcome.moodDelta
                    && festival.partialOutcome.moodDelta
                    > festival.failureOutcome.moodDelta,
                $"{festival.StableId} outcome mood ordering is invalid.", failures);
            report.AppendLine(string.Join(" | ",
                festival.StableId,
                $"{festival.season}/{festival.dayOfSeason}",
                festival.minimumParticipants.ToString(CultureInfo.InvariantCulture),
                inputEwu.ToString("0.00", CultureInfo.InvariantCulture),
                perParticipant.ToString("0.00", CultureInfo.InvariantCulture),
                (festival.successOutcome.moodDelta
                    * festival.successOutcome.moodDurationDays)
                    .ToString("0.0", CultureInfo.InvariantCulture)));
        }
    }

    private static bool TryGetFestivalItemWork(
        string itemId,
        EmbeddedWorkValueSnapshot embeddedWork,
        out float work)
    {
        work = 0f;
        if (embeddedWork.TryGetItemWork(itemId, out work) && work > 0f)
        {
            return true;
        }

        const string SeedPrefix = "seed-lot:";
        if (itemId != null
            && itemId.StartsWith(SeedPrefix, StringComparison.Ordinal))
        {
            string cropItemId = "resource:" + itemId.Substring(SeedPrefix.Length);
            if (embeddedWork.TryGetItemWork(cropItemId, out float cropWork)
                && cropWork > 0f)
            {
                work = cropWork * 1.5f;
                return true;
            }
        }

        return false;
    }

    private static void ValidateServiceIncidents(
        IReadOnlyCollection<ServiceIncidentDefinitionSO> incidents,
        StringBuilder report,
        ICollection<string> failures)
    {
        report.AppendLine("SERVICE_INCIDENTS");
        report.AppendLine("id | responses | distinct mechanical outcomes | max severity");
        Require(incidents.Count == 8,
            $"Expected 8 service incidents, found {incidents.Count}.", failures);
        foreach (ServiceIncidentDefinitionSO incident in incidents)
        {
            V20ChoiceDefinition[] responses = (incident.responses
                    ?? new List<V20ChoiceDefinition>())
                .Where(value => value != null)
                .ToArray();
            string[] signatures = responses
                .Select(value => EffectSummary(value.effects))
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            float maxSeverity = responses
                .Select(value => EffectSeverity(value.effects, 1))
                .DefaultIfEmpty(0f)
                .Max();
            Require(responses.Length == 3,
                $"{incident.StableId} must expose exactly three responses.", failures);
            Require(signatures.Length == responses.Length,
                $"{incident.StableId} contains mechanically duplicate responses.", failures);
            Require(maxSeverity is >= 0.5f and <= 12f,
                $"{incident.StableId} response severity {maxSeverity:0.00} is outside 0.5-12.",
                failures);
            report.AppendLine(string.Join(" | ",
                incident.StableId,
                responses.Length.ToString(CultureInfo.InvariantCulture),
                signatures.Length.ToString(CultureInfo.InvariantCulture),
                maxSeverity.ToString("0.00", CultureInfo.InvariantCulture)));
        }
    }

    private static float EffectSeverity(
        IEnumerable<V20ContentEffect> effects,
        int repetitions) => (effects ?? Array.Empty<V20ContentEffect>())
        .Where(value => value != null
            && value.kind != V20ContentEffectKind.WorldFlag)
        .Sum(value => Mathf.Abs(value.amount) * SeverityWeight(value.kind))
        * Mathf.Max(1, repetitions);

    private static float SeverityWeight(V20ContentEffectKind kind) => kind switch
    {
        V20ContentEffectKind.Money => 1f / 50f,
        V20ContentEffectKind.ItemConsume => 1f,
        V20ContentEffectKind.WorkDelayDays => 2f,
        V20ContentEffectKind.DiseaseExposure => 0.5f,
        V20ContentEffectKind.Threat => 1f,
        V20ContentEffectKind.FactionRapport => 0.75f,
        V20ContentEffectKind.FactionGrievance => 0.75f,
        V20ContentEffectKind.Health => 0.75f,
        V20ContentEffectKind.Relationship => 0.75f,
        V20ContentEffectKind.Mood => 0.75f,
        _ => 1f
    };

    private static EmbeddedWorkValueSnapshot CreateEmbeddedWork(
        ResourceGameContentCatalog content)
    {
        ResourceMaterialEconomicProfileCatalog materialProfiles = new(content);
        V23BalanceWorkCalculator work = new(materialProfiles);
        ItemDefinitionSO[] items = content.GetAll<ItemDefinitionSO>()
            .Concat(Resources.LoadAll<ResourceItemDefinitionSO>(
                ResourceItemDefinitionSO.ResourcePath))
            .Where(value => value != null)
            .GroupBy(value => value.ItemId, StringComparer.Ordinal)
            .Select(group => group.First())
            .ToArray();
        return new V23EmbeddedWorkValueCalculator(
            content.GetAll<ProductionRecipeSO>(),
            items,
            content.GetAll<CombatEquipmentDefinitionSO>(),
            content.GetAll<CraftMaterialDefinitionSO>(),
            work).Calculate();
    }

    private static float RequirementWork(
        V20ContentRequirementSet requirements,
        EmbeddedWorkValueSnapshot embeddedWork,
        string owner,
        ICollection<string> failures)
    {
        float total = 0f;
        foreach (V20ItemAmountRequirement item in requirements?.items
            ?? new List<V20ItemAmountRequirement>())
        {
            if (item == null || !item.consume)
            {
                continue;
            }

            if (!embeddedWork.ItemWork.TryGetValue(
                    item.itemDefinitionId,
                    out float work)
                || work <= 0f)
            {
                failures.Add(
                    $"{owner} has unpriced consumed item '{item.itemDefinitionId}'.");
                continue;
            }
            total += work * item.amount;
        }
        return total;
    }

    private static V20ChoiceDefinition FindChoice(
        FactionChapterDefinitionSO chapter,
        string choiceId) => chapter?.choices?.FirstOrDefault(value =>
            value != null && string.Equals(
                value.choiceId,
                choiceId,
                StringComparison.Ordinal));

    private static int CountRequirements(V20ContentRequirementSet set) =>
        (set?.items?.Count ?? 0)
        + (set?.facilities?.Count ?? 0)
        + (set?.research?.Count ?? 0)
        + (set?.characters?.Count ?? 0)
        + (set?.factions?.Count ?? 0)
        + (set?.worldMetrics?.Count ?? 0)
        + (set?.requiredFlags?.Count ?? 0);

    private static string EffectSummary(IEnumerable<V20ContentEffect> effects) =>
        string.Join(",", (effects ?? Array.Empty<V20ContentEffect>())
            .Where(value => value != null)
            .Select(value => $"{value.kind}:{value.targetId}:{value.amount:0.##}"));

    private static void Require(
        bool condition,
        string failure,
        ICollection<string> failures)
    {
        if (!condition)
        {
            failures.Add(failure);
        }
    }

    private static void WriteReport(string text)
    {
        string absolute = Path.GetFullPath(ReportPath);
        Directory.CreateDirectory(
            Path.GetDirectoryName(absolute)
            ?? throw new InvalidOperationException("Report directory is invalid."));
        File.WriteAllText(absolute, text, new UTF8Encoding(false));
    }
}
#endif
