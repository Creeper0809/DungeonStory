#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
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
        BuildingSO[] landmarkBuildings = content.GetAll<BuildingSO>()
            .Where(value => value != null && campaign.IsLandmarkBuilding(value.ContentDefinitionId))
            .OrderBy(value => value.id)
            .ToArray();
        Require(landmarkBuildings.Length == 9,
            $"Expected 9 physical landmark buildings, found {landmarkBuildings.Length}.");
        Require(landmarkBuildings.All(value =>
                !campaign.IsLandmarkUnlocked(value.ContentDefinitionId)
                && !FacilityProgression.IsUnlocked(
                    value,
                    new GameSessionState(),
                    null,
                    DisabledDungeonDebugRuleQuery.Instance,
                    campaign)),
            "An unearned milestone landmark was constructible before completion.");
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
        Require(landmarkBuildings.All(value => FacilityProgression.IsUnlocked(
                value,
                new GameSessionState(),
                null,
                DisabledDungeonDebugRuleQuery.Instance,
                campaign)),
            "A completed milestone did not unlock construction of its physical landmark.");
        VerifyMilestoneGameplayModifiers(campaign);

        IReadOnlyList<string> firstCrisis =
            campaign.ComposeNextEndlessCrisis(1_000, 94721);
        Require(firstCrisis.Count == 5
                && firstCrisis.Distinct(StringComparer.Ordinal).Count() == 5,
            "Endless crisis composition did not select five authored domains.");

        VerifyFactionBranches(catalog);
        VerifyPersistedWorkDelay(catalog);
        VerifySelfSufficiencyStreak(catalog);
        VerifyTenYearBounds(catalog);
        VerifyCulturalPracticeOutcomePersistence(content);
        VerifyTraitAnalysisPersistence(content);
        VerifyTenThousandGeneralTraitDeterminism(content);
        VerifyTenThousandInheritanceDeterminism();
        VerifyThreeGenerationNarrativeCompression(content);
        VerifyRoundTrip(campaign);

        Debug.Log(
            "V20_CAMPAIGN_RULES=PASS; milestones=9; factions=6x6; "
            + "endlessDomains=5; selfSufficiencyDays=120; "
            + "workDelayPersisted=true; tenYearHistoryBounded=true; "
            + "practiceNeglectPersisted=true; traitAnalysisPersisted=true; "
            + "generalTraitDeterminism=10000; inheritanceDeterminism=10000; "
            + "narrativePopulation=2000x3generations; saveRoundTrip=true");
    }

    private static void VerifyTenThousandGeneralTraitDeterminism(
        ResourceGameContentCatalog content)
    {
        CharacterTraitSO[] traits = content.GetAll<CharacterTraitSO>()
            .Where(value => value != null)
            .OrderBy(value => value.id)
            .ToArray();
        CharacterSkillSystemSettingsSO settings = content
            .GetAll<CharacterSkillSystemSettingsSO>()
            .Single();
        Require(traits.Length == 56,
            $"Expected 56 general traits, found {traits.Length}.");

        for (int index = 0; index < 10_000; index++)
        {
            int seed = CharacterGrowthRules.StableHash($"qa:trait:{index}");
            IReadOnlyList<int> first = CharacterTraitSelectionRules.Select(
                traits,
                settings.traitConflicts,
                new DeterministicRandomSequence(seed));
            IReadOnlyList<int> second = CharacterTraitSelectionRules.Select(
                traits.Reverse(),
                settings.traitConflicts,
                new DeterministicRandomSequence(seed));
            Require(first.Count == 3 && first.SequenceEqual(second),
                $"General trait selection lost deterministic ordering at sample {index}.");
            Require(!settings.traitConflicts.Any(rule => rule != null
                    && first.Contains(rule.firstTraitId)
                    && first.Contains(rule.secondTraitId)),
                $"General trait selection admitted a conflicting pair at sample {index}.");
        }
    }

    private static void VerifyTenThousandInheritanceDeterminism()
    {
        string[] firstParent =
        {
            "heritable:reinforced-joints",
            "heritable:dense-bone",
            "heritable:regrowing-tissue"
        };
        string[] secondParent =
        {
            "heritable:expanded-lung",
            "heritable:efficient-digestion",
            "heritable:mana-grounding"
        };
        for (int index = 0; index < 10_000; index++)
        {
            string seed = $"qa:inheritance:{index}";
            ReproductionRules.SelectInheritedTraits(
                firstParent,
                secondParent,
                seed,
                out IReadOnlyList<string> firstExpressed,
                out IReadOnlyList<string> firstLatent);
            ReproductionRules.SelectInheritedTraits(
                firstParent.Reverse(),
                secondParent.Reverse(),
                seed,
                out IReadOnlyList<string> secondExpressed,
                out IReadOnlyList<string> secondLatent);
            Require(firstExpressed.Count <= 4
                    && firstLatent.Count <= 2
                    && firstExpressed.SequenceEqual(
                        secondExpressed,
                        StringComparer.Ordinal)
                    && firstLatent.SequenceEqual(
                        secondLatent,
                        StringComparer.Ordinal),
                $"Heritable selection lost deterministic ordering at sample {index}.");
        }
    }

    private static void VerifyThreeGenerationNarrativeCompression(
        ResourceGameContentCatalog content)
    {
        CharacterNarrativeCatalog catalog = new(content);
        CharacterNarrativeRuntime narrative = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        LifeEventDefinitionSO lifeEvent = catalog.LifeEvents.First(value =>
            value.choices != null && value.choices.Count > 0);
        string choiceId = lifeEvent.choices[0].choiceId;
        CharacterSpeciesId speciesId = new(
            catalog.Cultures[0].defaultSpeciesId);

        const int population = 2_000;
        const int recordedEvents = 15;
        for (int index = 0; index < population; index++)
        {
            int generation = index % 3;
            CharacterId characterId = CharacterId.FromStableSuffix(
                $"qa:narrative:g{generation}:{index}");
            narrative.Register(
                characterId,
                speciesId,
                Array.Empty<string>(),
                Array.Empty<string>());
            for (int eventIndex = 0; eventIndex < recordedEvents; eventIndex++)
            {
                narrative.RecordResolvedEvent(
                    characterId,
                    new NarrativeEventId(lifeEvent.StableId),
                    choiceId,
                    eventIndex + 1);
            }
        }

        CharacterNarrativeWorldSaveData captured = narrative.Capture();
        Require(captured.characters.Count == population
                && captured.characters.All(value =>
                    value.recentEvents.Count == 12
                    && value.eventSummaries.Sum(summary => summary.count)
                        == recordedEvents - 12),
            "Three-generation narrative history did not compact to 12 recent events plus summaries.");

        CharacterNarrativeRuntime restored = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        restored.PublishRestore(restored.PrepareRestore(captured));
        Require(restored.All.Count == population
                && restored.All.All(value =>
                    value.RecentEvents.Count == 12
                    && value.EventSummaries.Sum(summary => summary.count)
                        == recordedEvents - 12),
            "Compressed three-generation narrative state did not survive restoration.");
    }

    private static void VerifyCulturalPracticeOutcomePersistence(
        ResourceGameContentCatalog content)
    {
        CharacterNarrativeCatalog narrativeCatalog = new(content);
        CharacterNarrativeRuntime source = new(
            new DungeonRuntimeAggregateRootStore(),
            narrativeCatalog);
        SpeciesCultureDefinitionSO culture = narrativeCatalog.Cultures[0];
        CulturalPracticeDefinitionSO practice = narrativeCatalog.Practices
            .First(value => string.Equals(
                value.cultureId,
                culture.StableId,
                StringComparison.Ordinal));
        CharacterId characterId = new("character:qa:practice-neglect");
        source.Register(
            characterId,
            new CharacterSpeciesId(culture.defaultSpeciesId),
            Array.Empty<string>(),
            Array.Empty<string>());
        source.RecordPracticeNeglect(characterId, practice.StableId, 10);

        CharacterNarrativeWorldSaveData save = source.Capture();
        CharacterNarrativeRuntime restored = new(
            new DungeonRuntimeAggregateRootStore(),
            narrativeCatalog);
        restored.PublishRestore(restored.PrepareRestore(save));
        Require(restored.TryGet(characterId, out CharacterNarrativeSnapshot snapshot)
                && snapshot.PracticeParticipations.Count == 1
                && !snapshot.PracticeParticipations[0].performed
                && snapshot.PracticeParticipations[0].lastAbsoluteDay == 10,
            "Cultural-practice neglect outcome did not round-trip.");
        Require(!restored.CanPerformPractice(
                    characterId,
                    practice.StableId,
                    19,
                    out int nextAllowed)
                && nextAllowed == 20
                && restored.CanPerformPractice(
                    characterId,
                    practice.StableId,
                    20,
                    out _),
            "Cultural-practice neglect did not persist the authored cooldown.");
    }

    private static void VerifyTraitAnalysisPersistence(
        ResourceGameContentCatalog content)
    {
        CharacterNarrativeCatalog catalog = new(content);
        CharacterNarrativeRuntime source = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        SpeciesCultureDefinitionSO culture = catalog.Cultures[0];
        CharacterId characterId = new("character:qa:trait-analysis");
        string latentTraitId = catalog.HeritableTraits[0].traitId;
        CharacterNarrativeSnapshot before = source.Register(
            characterId,
            new CharacterSpeciesId(culture.defaultSpeciesId),
            Array.Empty<string>(),
            new[] { latentTraitId });
        Require(!before.HeritableTraitsAnalyzed
                && before.VisibleLatentHeritableTraitIds.Count == 0,
            "Latent hereditary traits were visible before physical analysis.");

        source.MarkHeritableTraitsAnalyzed(characterId);
        CharacterNarrativeWorldSaveData save = source.Capture();
        CharacterNarrativeRuntime restored = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        restored.PublishRestore(restored.PrepareRestore(save));
        Require(restored.TryGet(characterId, out CharacterNarrativeSnapshot after)
                && after.HeritableTraitsAnalyzed
                && after.VisibleLatentHeritableTraitIds.SequenceEqual(
                    new[] { latentTraitId },
                    StringComparer.Ordinal),
            "Trait-analysis reveal state did not round-trip.");
    }

    private static void VerifyMilestoneGameplayModifiers(
        V20CampaignRuntime campaign)
    {
        Require(campaign.GrantedRewardIds.Count == 9
                && campaign.ActivePressureIds.Count == 9,
            "Completed milestones did not retain all reward and pressure IDs.");
        Require(campaign.EnemyCounterIntelVisible
                && Math.Abs(campaign.ExpeditionTravelTimeMultiplier - 0.9f)
                    < 0.0001f
                && Math.Abs(campaign.FacilityMaintenanceGoldMultiplier - 0.9f)
                    < 0.0001f
                && Math.Abs(
                    campaign.WaterAndFertilizerConsumptionMultiplier - 0.9f)
                    < 0.0001f
                && campaign.MentorshipDailyXpCap == 15
                && campaign.TemporalStasisWarningDays == 3
                && Math.Abs(campaign.ManaTransferLossMultiplier - 0.9f)
                    < 0.0001f
                && Math.Abs(campaign.AutomaticMaintenanceWorkMultiplier - 0.85f)
                    < 0.0001f
                && campaign.IsAccordSignalSupportDay(
                    GameCalendarRules.DaysPerSeason),
            "A completed milestone remained a record-only reward.");
        Require(campaign.HasPressure("ending:truth-revealed")
                && campaign.HasPressure("ending:steel-apotheosis"),
            "Typed milestone pressure projection is incomplete.");
    }

    private static void VerifyPersistedWorkDelay(
        V20StoryContentCatalog catalog)
    {
        ServiceIncidentDefinitionSO incident = catalog.ServiceIncidents.First(value =>
            value.responses.Any(response => response.effects.Any(effect =>
                effect.kind == V20ContentEffectKind.WorkDelayDays
                && effect.amount > 0)));
        V20ChoiceDefinition response = incident.responses.First(value =>
            value.effects.Any(effect =>
                effect.kind == V20ContentEffectKind.WorkDelayDays
                && effect.amount > 0));
        V20CampaignRuntime campaign = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        SocietyEventWorldSaveData state = new()
        {
            lastEvaluationAbsoluteDay = 10
        };
        state.activeEvents.Add(new V20ActiveEventSaveData
        {
            instanceId = "qa:work-delay",
            definitionId = incident.StableId,
            startedAbsoluteDay = 10,
            deadlineAbsoluteDay = 12,
            generation = 0,
            contextFactionId = catalog.Arcs[0].factionId
        });
        campaign.PublishSociety(campaign.PrepareSociety(state));
        Require(campaign.TryResolveSocietyEvent(
                "qa:work-delay",
                response.choiceId,
                new RunMilestoneEvaluationSnapshot(),
                out _,
                out string failure),
            $"A work-delay response could not resolve: {failure}");
        Require(campaign.GetRemainingDays() > 0
                && campaign.GetWorkSpeedMultiplier(BuiltInWorkTypeIds.Research) < 1f,
            "The authored work-delay effect did not affect real work speed.");

        SocietyEventWorldSaveData captured = campaign.CaptureSociety();
        V20CampaignRuntime restored = new(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        restored.PublishSociety(restored.PrepareSociety(captured));
        Require(restored.GetRemainingDays() == campaign.GetRemainingDays()
                && Math.Abs(restored.GetWorkSpeedMultiplier(
                    BuiltInWorkTypeIds.Research)
                    - campaign.GetWorkSpeedMultiplier(
                        BuiltInWorkTypeIds.Research)) < 0.0001f,
            "The work-delay state did not survive society save restoration.");
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
                V20ChoiceDefinition bargain = chapter.choices.Single(value =>
                    string.Equals(value.choiceId, "bargain", StringComparison.Ordinal));
                int supportCost = choice.requirements.items
                    .Where(value => value.consume)
                    .Sum(value => value.amount);
                int bargainCost = bargain.requirements.items
                    .Where(value => value.consume)
                    .Sum(value => value.amount);
                Require(supportCost > bargainCost && bargainCost > 0,
                    $"Faction chapter costs are not mechanically distinct: {chapter.StableId}");
                int priorCrossRapport = 0;
                int priorCrossGrievance = 0;
                if (!string.IsNullOrWhiteSpace(chapter.crossFactionId)
                    && campaign.TryGetFaction(chapter.crossFactionId, out FactionCampaignStateSaveData beforeCross))
                {
                    priorCrossRapport = beforeCross.rapport;
                    priorCrossGrievance = beforeCross.grievance;
                }
                RunMilestoneEvaluationSnapshot requirements = Satisfy(
                    new[] { chapter.triggerRequirements, choice.requirements });
                Require(campaign.TryResolveChapter(
                        arc.factionId,
                        choice.choiceId,
                        requirements,
                        out V20ResolvedEventResult resolved,
                        out string failure),
                    $"Faction chapter was unreachable: {chapter.StableId}; {failure}");
                Require(resolved.Effects.Any(value =>
                        value.kind == V20ContentEffectKind.ItemConsume
                        && Mathf.RoundToInt(value.amount) == supportCost),
                    $"Faction chapter did not return its physical item consumption: {chapter.StableId}");
                if (!string.IsNullOrWhiteSpace(chapter.crossFactionId))
                {
                    Require(campaign.TryGetFaction(
                            chapter.crossFactionId,
                            out FactionCampaignStateSaveData afterCross)
                        && (afterCross.rapport != priorCrossRapport
                            || afterCross.grievance != priorCrossGrievance),
                        $"Cross-faction chapter did not change its counterpart: {chapter.StableId}");
                }
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
