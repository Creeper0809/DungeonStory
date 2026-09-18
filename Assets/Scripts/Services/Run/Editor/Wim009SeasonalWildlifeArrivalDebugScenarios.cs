#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;

public static class Wim009SeasonalWildlifeArrivalDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/wim-implementation/wim-009-seasonal-wildlife-arrival-rules.txt";

    private readonly struct Expected
    {
        public Expected(
            string eventId,
            Season season,
            string speciesId,
            int exactCount,
            string habitatId,
            SeasonalWildlifeArrivalQualification qualification,
            int minimumDuration,
            int maximumDuration)
        {
            EventId = eventId;
            Season = season;
            SpeciesId = speciesId;
            ExactCount = exactCount;
            HabitatId = habitatId;
            Qualification = qualification;
            MinimumDuration = minimumDuration;
            MaximumDuration = maximumDuration;
        }

        public string EventId { get; }
        public Season Season { get; }
        public string SpeciesId { get; }
        public int ExactCount { get; }
        public string HabitatId { get; }
        public SeasonalWildlifeArrivalQualification Qualification { get; }
        public int MinimumDuration { get; }
        public int MaximumDuration { get; }
    }

    private static readonly Expected[] Cases =
    {
        new(
            "seasonal:autumn-predator-descent",
            Season.Autumn,
            "carrion_drake",
            2,
            "lair",
            SeasonalWildlifeArrivalQualification.Predatory,
            3,
            5),
        new(
            "seasonal:winter-hungry-pack",
            Season.Winter,
            "cave_hound",
            3,
            "lair",
            SeasonalWildlifeArrivalQualification.Predatory,
            3,
            5),
        new(
            "seasonal:spring-migrant-herd",
            Season.Spring,
            "deep_goat",
            3,
            "grass",
            SeasonalWildlifeArrivalQualification.NonHostile,
            2,
            4),
        new(
            "seasonal:autumn-migration-window",
            Season.Autumn,
            "spore_elk",
            2,
            "grass",
            SeasonalWildlifeArrivalQualification.NonHostile,
            2,
            3)
    };

    public static bool Run(out string report)
    {
        List<string> lines = new()
        {
            "WIM009 seasonal wildlife arrival rules/authoring",
            "scope=real catalog plus isolated campaign planning/save/expiry; live grid materialization, capture, death, AI behavior, alert merge, and full save coordinator NOT_RUN"
        };
        string current = "not-started";
        int passed = 0;
        try
        {
            Require(
                Application.isPlaying,
                "Run inside the protected, paused main Play session.");
            var source = new ResourceGameContentCatalog(
                new UnityGameContentRootLoader());
            var catalog = new V20StoryContentCatalog(source);

            current = "actual-catalog-authoring";
            SeasonalWorldEventDefinitionSO[] arrivals = catalog.SeasonalEvents
                .Where(value =>
                    value.wildlifeArrivalProfile?.IsConfigured == true)
                .OrderBy(value => value.StableId, StringComparer.Ordinal)
                .ToArray();
            Require(arrivals.Length == Cases.Length,
                $"Expected exactly {Cases.Length} seasonal wildlife arrivals, found {arrivals.Length}.");
            foreach (Expected expected in Cases)
            {
                SeasonalWorldEventDefinitionSO authored = arrivals.Single(
                    value => string.Equals(
                        value.StableId,
                        expected.EventId,
                        StringComparison.Ordinal));
                SeasonalWildlifeArrivalProfile profile =
                    authored.wildlifeArrivalProfile;
                Require(authored.season == expected.Season
                        && authored.minimumDurationDays
                            == expected.MinimumDuration
                        && authored.maximumDurationDays
                            == expected.MaximumDuration,
                    $"{expected.EventId} season/duration changed.");
                Require(string.Equals(
                            profile.speciesId,
                            expected.SpeciesId,
                            StringComparison.Ordinal)
                        && profile.exactCount == expected.ExactCount
                        && string.Equals(
                            profile.requiredHabitatId,
                            expected.HabitatId,
                            StringComparison.Ordinal)
                        && profile.qualification == expected.Qualification,
                    $"{expected.EventId} arrival profile changed.");
                Require(authored.startEffects != null
                        && authored.startEffects.Count == 0,
                    $"{expected.EventId} still carries a legacy start effect.");
                WildlifeSpeciesSO speciesAsset = catalog.Wildlife.Single(
                    value => string.Equals(
                        value.SpeciesId,
                        expected.SpeciesId,
                        StringComparison.Ordinal));
                SeasonalWildlifeArrivalRules.RequireValidAuthoredProfile(
                    authored,
                    speciesAsset.ToDefinition());
            }
            passed++;

            current = "frozen-plan-replay-save-expiry";
            int nextSequence = 1000;
            foreach (Expected expected in Cases)
            {
                V20CampaignRuntime campaign = StartOnly(
                    catalog,
                    expected,
                    absoluteDay: 100,
                    runSeed: 78001 + nextSequence);
                V20ActiveEventSaveData occurrence = campaign
                    .ActiveSeasonalEvents.Single();
                SeasonalWildlifeArrivalSaveData frozen =
                    occurrence.seasonalWildlifeArrival;
                Require(frozen.configured
                        && frozen.members.Count == 0
                        && frozen.exactCount == expected.ExactCount
                        && string.Equals(
                            frozen.speciesId,
                            expected.SpeciesId,
                            StringComparison.Ordinal),
                    $"{expected.EventId} did not freeze its exact profile on occurrence creation.");

                SeasonalWildlifeArrivalMemberSaveData[] plan =
                    Enumerable.Range(0, expected.ExactCount)
                    .Select(index =>
                        new SeasonalWildlifeArrivalMemberSaveData
                        {
                            wildlifeId = $"wild:{nextSequence + index}",
                            positionX = 20 + index,
                            positionY = 3
                        })
                    .ToArray();
                nextSequence += expected.ExactCount + 10;
                Require(campaign.TryRecordSeasonalWildlifeArrivalPlan(
                        occurrence.instanceId,
                        plan,
                        out string failure),
                    failure);
                string plannedJson = JsonUtility.ToJson(
                    campaign.CaptureSeasonal());
                Require(campaign.TryRecordSeasonalWildlifeArrivalPlan(
                        occurrence.instanceId,
                        plan,
                        out failure)
                    && string.Equals(
                        plannedJson,
                        JsonUtility.ToJson(campaign.CaptureSeasonal()),
                        StringComparison.Ordinal),
                    "Exact plan replay was not a mutation-free success.");
                SeasonalWildlifeArrivalMemberSaveData[] conflicting = plan
                    .Select(SeasonalWildlifeArrivalRules.CloneMember)
                    .ToArray();
                conflicting[0].positionX++;
                Require(!campaign.TryRecordSeasonalWildlifeArrivalPlan(
                        occurrence.instanceId,
                        conflicting,
                        out _)
                    && string.Equals(
                        plannedJson,
                        JsonUtility.ToJson(campaign.CaptureSeasonal()),
                        StringComparison.Ordinal),
                    "Conflicting plan replay changed the frozen plan.");

                Require(campaign.TryMarkSeasonalWildlifeArrivalSpawned(
                        occurrence.instanceId,
                        plan[0].wildlifeId,
                        out failure),
                    failure);
                string markedJson = JsonUtility.ToJson(
                    campaign.CaptureSeasonal());
                Require(campaign.TryMarkSeasonalWildlifeArrivalSpawned(
                        occurrence.instanceId,
                        plan[0].wildlifeId,
                        out failure)
                    && string.Equals(
                        markedJson,
                        JsonUtility.ToJson(campaign.CaptureSeasonal()),
                        StringComparison.Ordinal),
                    "Spawn acknowledgement replay was not a mutation-free success.");

                SeasonalEventWorldSaveData saved = campaign.CaptureSeasonal();
                var restored = new V20CampaignRuntime(
                    new DungeonRuntimeAggregateRootStore(),
                    catalog);
                SeasonalEventAggregateState staged =
                    restored.PrepareSeasonalRestore(saved);
                restored.PublishSeasonalRestore(staged);
                Require(string.Equals(
                        JsonUtility.ToJson(saved),
                        JsonUtility.ToJson(restored.CaptureSeasonal()),
                        StringComparison.Ordinal),
                    "Frozen occurrence plan did not round-trip exactly.");
                SeasonalWildlifeArrivalRules.ValidateRestoreJoin(
                    saved,
                    Array.Empty<WildlifeActor>());

                SeasonalEventWorldSaveData malformed = JsonUtility
                    .FromJson<SeasonalEventWorldSaveData>(
                        JsonUtility.ToJson(saved));
                malformed.activeEvents[0]
                    .seasonalWildlifeArrival.members[1].wildlifeId =
                    malformed.activeEvents[0]
                        .seasonalWildlifeArrival.members[0].wildlifeId;
                string beforeReject = JsonUtility.ToJson(
                    restored.CaptureSeasonal());
                RequireThrows(() => restored.PrepareSeasonal(malformed));
                Require(string.Equals(
                        beforeReject,
                        JsonUtility.ToJson(restored.CaptureSeasonal()),
                        StringComparison.Ordinal),
                    "Malformed plan validation mutated live state.");

                int expiryDay = occurrence.deadlineAbsoluteDay + 1;
                IReadOnlyList<V20ResolvedEventResult> expired =
                    restored.EvaluateDaily(new V20DailyEventContext
                    {
                        AbsoluteDay = expiryDay,
                        RunSeed = 78001 + nextSequence,
                        Season = expected.Season
                    });
                Require(restored.ActiveSeasonalEvents.Count == 0
                        && expired.Any(value =>
                            string.Equals(
                                value.DefinitionId,
                                expected.EventId,
                                StringComparison.Ordinal)
                            && string.Equals(
                                value.ResolutionId,
                                "completed",
                                StringComparison.Ordinal)
                            && string.Equals(
                                value.OccurrenceInstanceId,
                                occurrence.instanceId,
                                StringComparison.Ordinal)),
                    "Expiry did not cancel the occurrence with its stable UI source ID.");
            }
            passed++;

            lines.Add("result=PASS");
            lines.Add(
                "passed=" + passed
                + "; occurrences=4; predatorCounts=2,3; nonHostileCounts=3,2"
                + "; immutablePlan=true; replayDuplicate=0; currentSaveVersion="
                + SeasonalEventWorldSaveData.CurrentVersion);
            report = string.Join("\n", lines);
            WriteReport(report);
            return true;
        }
        catch (Exception error)
        {
            lines.Add("result=FAIL");
            lines.Add("case=" + current + "; passed=" + passed
                + "; " + error.GetType().Name + ": " + error.Message);
            report = string.Join("\n", lines);
            WriteReport(report);
            return false;
        }
    }

    private static V20CampaignRuntime StartOnly(
        V20StoryContentCatalog catalog,
        Expected expected,
        int absoluteDay,
        int runSeed)
    {
        var campaign = new V20CampaignRuntime(
            new DungeonRuntimeAggregateRootStore(),
            catalog);
        SeasonalEventWorldSaveData state = campaign.CaptureSeasonal();
        state.cycle = absoluteDay / GameCalendarRules.DaysPerYear;
        state.completedEventIds.AddRange(catalog.SeasonalEvents
            .Where(value => !string.Equals(
                value.StableId,
                expected.EventId,
                StringComparison.Ordinal))
            .Select(value => value.StableId));
        campaign.PublishSeasonal(campaign.PrepareSeasonal(state));
        IReadOnlyList<V20ResolvedEventResult> results = campaign
            .EvaluateDaily(new V20DailyEventContext
            {
                AbsoluteDay = absoluteDay,
                RunSeed = runSeed,
                Season = expected.Season
            });
        Require(results.Count(value =>
                string.Equals(
                    value.DefinitionId,
                    expected.EventId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.ResolutionId,
                    "started",
                    StringComparison.Ordinal)) == 1,
            $"{expected.EventId} did not start exactly once.");
        return campaign;
    }

    private static void RequireThrows(Action action)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(
            "Expected invalid seasonal save rejection.");
    }

    private static void WriteReport(string report)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, report + "\n");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
