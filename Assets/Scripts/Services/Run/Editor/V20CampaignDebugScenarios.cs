#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class V20CampaignDebugScenarios
{
    [MenuItem("DungeonStory/QA/V20 Campaign And Endless Rules")]
    public static void Run()
    {
        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        V20StoryContentCatalog catalog = new(content);
        Require(catalog.All.Count == 9, "Milestone catalog count changed.");
        Require(catalog.Arcs.Count == 6
                && catalog.Chapters.Count == 36
                && catalog.Contracts.Count == 18,
            "Faction-story catalog counts changed.");

        V20CampaignRuntime campaign = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        List<string> completed = new();
        RunMilestoneEvaluationSnapshot allRequirements = null;
        for (int day = 1; day <= 120; day++)
        {
            allRequirements = Satisfy(
                catalog.All.Select(value => value.completionRequirements));
            allRequirements.AbsoluteDay = day;
            allRequirements.WorldFlags.Add("ecology:self-sufficient-today");
            completed.AddRange(campaign.Evaluate(allRequirements));
        }
        Require(completed.Count == 9,
            $"All authored milestone conditions completed {completed.Count}/9 milestones after the required 120-day self-sufficiency streak.");
        Require(campaign.Phase == RunProgressionPhase.EndlessAge,
            "The first grand milestone did not unlock EndlessAge.");
        Require(campaign.Evaluate(allRequirements).Count == 0,
            "A one-time milestone completed twice.");
        Require(campaign.CompletedMilestoneIds.Count == 9,
            "Milestone completion history is incomplete.");
        Require(catalog.All.All(value =>
                campaign.IsLandmarkUnlocked(value.landmarkBuildingId)),
            "A completed milestone did not unlock its physical landmark.");

        IReadOnlyList<string> firstCrisis =
            campaign.ComposeNextEndlessCrisis(1_000, 94721);
        Require(firstCrisis.Count == 5
                && firstCrisis.Distinct(StringComparer.Ordinal).Count() == 5,
            "Endless crisis composition did not select five authored domains.");

        VerifyFactionBranches(catalog);
        VerifySelfSufficiencyStreak(catalog);
        VerifyTenYearBounds(catalog);
        VerifyRoundTrip(campaign);

        Debug.Log(
            "V20_CAMPAIGN_RULES=PASS; milestones=9; factions=6x6; "
            + "endlessDomains=5; selfSufficiencyDays=120; "
            + "tenYearHistoryBounded=true; saveRoundTrip=true");
    }

    private static void VerifyFactionBranches(V20StoryContentCatalog catalog)
    {
        V20CampaignRuntime campaign = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        foreach (FactionArcDefinitionSO arc in catalog.Arcs)
        {
            for (int chapterNumber = 1; chapterNumber <= 6; chapterNumber++)
            {
                FactionChapterDefinitionSO chapter = catalog.Chapters.Single(
                    value => value.factionId == arc.factionId
                        && value.chapterNumber == chapterNumber);
                V20ChoiceDefinition choice = chapter.choices[0];
                RunMilestoneEvaluationSnapshot requirements = Satisfy(
                    new[] { chapter.triggerRequirements, choice.requirements });
                Require(campaign.TryResolveChapter(
                        arc.factionId,
                        choice.choiceId,
                        requirements,
                        out _,
                        out string failure),
                    $"Faction chapter was unreachable: {chapter.StableId}; {failure}");
            }

            FactionContractDefinitionSO contract = catalog.Contracts.First(
                value => value.factionId == arc.factionId);
            Require(campaign.TryAcceptContract(
                    arc.factionId,
                    contract.StableId,
                    100,
                    out string acceptFailure),
                $"Faction contract could not be accepted: {acceptFailure}");
            Require(campaign.TryResolveContract(
                    arc.factionId,
                    success: true,
                    Satisfy(new[] { contract.completionRequirements }),
                    out _,
                    out string resolveFailure),
                $"Faction contract could not be completed: {resolveFailure}");
        }
    }

    private static void VerifySelfSufficiencyStreak(
        V20StoryContentCatalog catalog)
    {
        V20CampaignRuntime campaign = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        List<string> completed = new();
        for (int day = 1; day <= 120; day++)
        {
            RunMilestoneEvaluationSnapshot snapshot = new()
            {
                AbsoluteDay = day,
                EligibleCharacterCount = 20
            };
            snapshot.CompletedResearchIds.Add(7255);
            snapshot.WorldFlags.Add("ecology:self-sufficient-today");
            completed.AddRange(campaign.Evaluate(snapshot));
        }
        Require(completed.Contains(
                "ending:sealed-paradise",
                StringComparer.Ordinal),
            "A continuous 120-day self-sufficient run did not unlock the sealed paradise milestone.");
    }

    private static void VerifyTenYearBounds(V20StoryContentCatalog catalog)
    {
        V20CampaignRuntime campaign = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        for (int day = 1; day <= GameCalendarRules.DaysPerYear * 10; day++)
        {
            V20DailyEventContext context = new()
            {
                AbsoluteDay = day,
                RunSeed = 7711,
                Season = GameCalendarRules.Project(day, 0).Season,
                Generation = day / GameCalendarRules.DaysPerYear
            };
            context.ParticipantCharacterIds.Add("character:qa:one");
            context.ParticipantCharacterIds.Add("character:qa:two");
            context.ParticipantCharacterIds.Add("character:qa:three");
            campaign.EvaluateDaily(context);
            Require(campaign.ActiveSocietyEvents.Count <= (day <= 30 ? 2 : 3),
                "Society event queue exceeded its ordinary/emergency cap.");
        }
        Require(campaign.RecentResolvedSocietyEvents.Count <= 256,
            "Ten-year society history exceeded the bounded recent-history cap.");
    }

    private static void VerifyRoundTrip(V20CampaignRuntime source)
    {
        SeasonalEventWorldSaveData seasonal = source.CaptureSeasonal();
        SocietyEventWorldSaveData society = source.CaptureSociety();
        FactionCampaignWorldSaveData factions = source.CaptureFactions();
        RunMilestoneWorldSaveData milestones = source.CaptureMilestones();
        V20CampaignRuntime restored = new(
            new DungeonRuntimeAggregateRootStore(),
            new V20StoryContentCatalog(new ResourceGameContentCatalog(
                new UnityGameContentRootLoader())));
        restored.PublishSeasonal(restored.PrepareSeasonal(seasonal));
        restored.PublishSociety(restored.PrepareSociety(society));
        restored.PublishFactions(restored.PrepareFactions(factions));
        restored.PublishMilestones(restored.PrepareMilestones(milestones));
        Require(string.Equals(
                JsonUtility.ToJson(restored.CaptureMilestones()),
                JsonUtility.ToJson(milestones),
                StringComparison.Ordinal),
            "Milestone aggregate did not round-trip exactly.");
    }

    private static RunMilestoneEvaluationSnapshot Satisfy(
        IEnumerable<V20ContentRequirementSet> requirementSets)
    {
        RunMilestoneEvaluationSnapshot snapshot = new()
        {
            EligibleCharacterCount = 10_000
        };
        foreach (V20ContentRequirementSet requirements in
            requirementSets.Where(value => value != null))
        {
            foreach (V20ResearchRequirement value in requirements.research)
                snapshot.CompletedResearchIds.Add(value.researchNumericId);
            foreach (string value in requirements.requiredFlags)
                snapshot.WorldFlags.Add(value);
            foreach (V20WorldMetricRequirement value in requirements.worldMetrics)
                snapshot.WorldMetrics[value.kind] = Math.Max(
                    value.minimumValue,
                    snapshot.WorldMetrics.TryGetValue(value.kind, out float current)
                        ? current
                        : 0f);
            foreach (V20ItemAmountRequirement value in requirements.items)
                snapshot.ItemQuantities[value.itemDefinitionId] = Math.Max(
                    value.amount,
                    snapshot.ItemQuantities.TryGetValue(
                        value.itemDefinitionId,
                        out int current) ? current : 0);
            foreach (V20FacilityRequirement value in requirements.facilities)
            {
                string id = !string.IsNullOrWhiteSpace(value.buildingDefinitionId)
                    ? value.buildingDefinitionId
                    : "capability:" + value.capabilityId;
                snapshot.FacilityCounts[id] = Math.Max(
                    value.minimumCount,
                    snapshot.FacilityCounts.TryGetValue(id, out int current)
                        ? current
                        : 0);
            }
            foreach (V20FactionRequirement value in requirements.factions)
                snapshot.Factions[value.factionId] = new FactionCampaignStateSaveData
                {
                    factionId = value.factionId,
                    rapport = value.minimumRapport,
                    grievance = value.maximumGrievance,
                    obligationTokens = value.minimumObligationTokens,
                    currentChapter = 1
                };
        }
        return snapshot;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
