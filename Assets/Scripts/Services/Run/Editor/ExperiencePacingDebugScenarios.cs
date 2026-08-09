#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Content.CoreSession;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class ExperiencePacingDebugScenarios
{
    [MenuItem("Tools/DungeonStory/QA/Run 30-Day Learning Rhythm Contracts")]
    public static void Run()
    {
        List<string> failures = new List<string>();
        CheckResearchContracts(failures);
        CheckPacingContracts(failures);
        CheckResearchFacilityAssets(failures);

        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                "30-day learning rhythm contracts failed:\n- "
                + string.Join("\n- ", failures));
        }

        Debug.Log(
            "long-horizon learning rhythm contracts passed: 180 visible research definitions, "
            + "facility capacity data, day 10/20/30 rehearsal profiles, day-40 boss curve, "
            + "incident gating, and pacing save round-trip.");
    }

    private static void CheckResearchContracts(ICollection<string> failures)
    {
        Require(UITabCatalog.All.Count == 11
            && UITabCatalog.All.Select(tab => tab.Id).Distinct().Count() == 11,
            "all 11 top-level product tabs must remain available from day one",
            failures);
        ResearchProjectSO[] projects = AssetDatabase
            .FindAssets("t:ResearchProjectSO", new[]
            {
                "Assets/Resources/SO/Research/Projects"
            })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ResearchProjectSO>)
            .Where(project => project != null)
            .ToArray();
        Require(projects.Length == 180, $"expected 180 research projects, got {projects.Length}", failures);
        Require(
            projects.All(project => project.FacilityRequirements.Count > 0),
            "every research project must declare facility capacity requirements",
            failures);
        Require(
            projects.SelectMany(project => project.FacilityRequirements)
                .All(requirement => requirement.requiredCount > 0),
            "research facility requirements must use positive counts",
            failures);
        Require(
            projects.All(project => project.ValidateDefinition().Count == 0),
            "research catalog definitions must validate after capacity generation",
            failures);
    }

    private static void CheckPacingContracts(ICollection<string> failures)
    {
        CoreSessionRulesDefinition rules =
            LoadRules().CreateRuntimeDefinition();
        ICoreSessionRulesProvider rulesProvider =
            new FixedRulesProvider(rules);
        Require(DungeonRunFlowRuntime.ResolveBossCycleForDay(39, rules) == 0, "boss must not start before day 40", failures);
        Require(DungeonRunFlowRuntime.ResolveBossCycleForDay(40, rules) == 1, "day 40 must be boss cycle 1", failures);
        Require(DungeonRunFlowRuntime.ResolveBossCycleForDay(50, rules) == 2, "day 50 must be boss cycle 2", failures);

        CheckProfile(10, 0.25f, rules, failures);
        CheckProfile(20, 0.50f, rules, failures);
        CheckProfile(30, 0.75f, rules, failures);
        CheckProtectedWindowAcrossSeeds(rulesProvider, failures);

        GameEventBus bus = new GameEventBus();
        ExperiencePacingRuntime pacing = CreateRuntime(
            bus,
            rulesProvider);
        pacing.AdvanceToDay(1);
        Require(!pacing.AllowsRandomInvasion, "random invasion must be blocked on day 1", failures);
        Require(pacing.MaximumConcurrentExternalProblems == 0,
            "day 1 must not allow natural external problems", failures);
        Require(!pacing.CanStartExteriorIncident(ExteriorIncidentKind.MerchantCart), "all natural incidents must be blocked through day 3", failures);
        pacing.AdvanceToDay(4);
        Require(pacing.MaximumConcurrentExternalProblems == 1,
            "days 4-9 must allow one concurrent external problem", failures);
        Require(pacing.CanStartExteriorIncident(ExteriorIncidentKind.MerchantCart), "merchant must be eligible from day 4", failures);
        Require(!pacing.CanStartExteriorIncident(ExteriorIncidentKind.Thief), "thief must be blocked before day 31", failures);
        pacing.AdvanceToDay(20);
        pacing.AdvanceToDay(4);
        Require(pacing.CurrentDay == 20,
            "experience day must be monotonic and reject stale day events",
            failures);
        Require(pacing.MaximumConcurrentExternalProblems == 2,
            "days 10-30 must allow two concurrent external problems", failures);
        Require(pacing.TryBeginRehearsal(20, out RehearsalInvasionProfile profile)
            && Mathf.Approximately(profile.PowerMultiplier, 0.5f),
            "day-20 rehearsal must arm at 50% power",
            failures);
        Require(!pacing.TryBeginRehearsal(20, out _),
            "an active rehearsal must not schedule a duplicate transition",
            failures);
        pacing.MarkExteriorIncidentStarted(ExteriorIncidentKind.CargoDamage);
        pacing.MarkExteriorIncidentStarted(ExteriorIncidentKind.CargoDamage);

        DungeonExperiencePacingSaveData saved = pacing.Capture();
        Require(saved.introducedConcepts.SequenceEqual(
                saved.introducedConcepts.Distinct().OrderBy(value => value))
            && saved.scheduledRehearsalMask == 0b010
            && saved.completedRehearsalMask == 0,
            "capture must preserve canonical concept and rehearsal state",
            failures);
        ExperiencePacingRuntime restored = CreateRuntime(
            new GameEventBus(),
            rulesProvider);
        restored.PublishRestoreCandidate(
            restored.PrepareRestoreCandidate(saved));
        Require(restored.CurrentDay == 20 && restored.ActiveRehearsalDay == 20,
            "pacing save must preserve current and active rehearsal day",
            failures);
        DungeonExperiencePacingSaveData roundTrip = restored.Capture();
        Require(roundTrip.currentDay == saved.currentDay
            && roundTrip.scheduledRehearsalMask
                == saved.scheduledRehearsalMask
            && roundTrip.completedRehearsalMask
                == saved.completedRehearsalMask
            && roundTrip.activeRehearsalDay == saved.activeRehearsalDay
            && roundTrip.introducedConcepts.SequenceEqual(
                saved.introducedConcepts),
            "pacing V18 section payload must round-trip exactly",
            failures);
        restored.ResolveRehearsal();
        restored.ResolveRehearsal();
        Require(!restored.TryBeginRehearsal(20, out _),
            "completed rehearsal must not schedule twice after restore",
            failures);
        restored.AdvanceToDay(31);
        Require(restored.AllowsRandomInvasion
            && restored.CanStartExteriorIncident(ExteriorIncidentKind.Thief)
            && restored.CanStartExteriorIncident(ExteriorIncidentKind.PredatorApproach),
            "normal hostile incident pool must open on day 31",
            failures);

        DungeonExperiencePacingSaveData invalid = restored.Capture();
        invalid.currentDay = 1;
        invalid.scheduledRehearsalMask = 0b010;
        invalid.completedRehearsalMask = 0;
        invalid.activeRehearsalDay = 20;
        bool rejectedInvalid = false;
        try
        {
            restored.PrepareRestoreCandidate(invalid);
        }
        catch (InvalidOperationException)
        {
            rejectedInvalid = true;
        }
        Require(rejectedInvalid,
            "restore candidate must reject rehearsal history ahead of its day",
            failures);
    }

    private static void CheckResearchFacilityAssets(ICollection<string> failures)
    {
        Dictionary<string, BuildingSO> byCode = AssetDatabase
            .FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(building => building != null)
            .Select(building => new
            {
                Building = building,
                Code = building.GetAbility<BuildingFacilityPartAbility>()?.code
            })
            .Where(entry => !string.IsNullOrWhiteSpace(entry.Code))
            .GroupBy(entry => entry.Code, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First().Building, StringComparer.Ordinal);

        foreach (string code in new[] { "Q01", "Q02", "Q03", "Q04", "Q05", "Q06", "P19" })
        {
            Require(byCode.TryGetValue(code, out BuildingSO building)
                && building.GetAbility<BuildingResearchCapacityAbility>() != null,
                $"{code} must provide research facility capacity",
                failures);
        }

        BuildingSO advanced = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/P1/P1_ResearchLab.asset");
        Require(advanced?.GetAbility<BuildingResearchCapacityAbility>()?
                .Contributions.Any(contribution =>
                    contribution.capability == ResearchFacilityCapabilityId.Advanced
                    && contribution.capacity >= 1) == true,
            "P1 research lab must provide Advanced capacity",
            failures);
    }

    private static void CheckProfile(
        int day,
        float expectedPower,
        CoreSessionRulesDefinition rules,
        ICollection<string> failures)
    {
        RehearsalInvasionProfile profile =
            ExperiencePacingApplicationAdapter.ResolveRehearsalProfile(
                day,
                rules);
        Require(profile.Day == day
            && Mathf.Approximately(profile.PowerMultiplier, expectedPower)
            && profile.RetreatHealthRatio > 0f,
            $"day {day} rehearsal profile is invalid",
            failures);
    }

    private static void CheckProtectedWindowAcrossSeeds(
        ICoreSessionRulesProvider rulesProvider,
        ICollection<string> failures)
    {
        Array incidentKinds = Enum.GetValues(typeof(ExteriorIncidentKind));
        for (int seed = 0; seed < 100; seed++)
        {
            System.Random random = new System.Random(seed);
            ExperiencePacingRuntime pacing = CreateRuntime(
                new GameEventBus(),
                rulesProvider);
            for (int day = 1; day <= 30; day++)
            {
                pacing.AdvanceToDay(day);
                if (pacing.AllowsRandomInvasion)
                {
                    failures.Add($"seed {seed}: natural invasion opened on day {day}");
                    return;
                }

                for (int sample = 0; sample < incidentKinds.Length; sample++)
                {
                    ExteriorIncidentKind kind = (ExteriorIncidentKind)
                        incidentKinds.GetValue(random.Next(incidentKinds.Length));
                    if (pacing.CanStartExteriorIncident(kind)
                        && kind is ExteriorIncidentKind.Thief
                            or ExteriorIncidentKind.PredatorApproach)
                    {
                        failures.Add(
                            $"seed {seed}: hostile incident {kind} opened on day {day}");
                        return;
                    }
                }
            }
        }
    }

    private static void Require(
        bool condition,
        string message,
        ICollection<string> failures)
    {
        if (!condition)
        {
            failures.Add(message);
        }
    }

    private static ExperiencePacingRuntime CreateRuntime(
        IGameEventBus gameEventBus,
        ICoreSessionRulesProvider rulesProvider) =>
        new(new ExperiencePacingApplicationAdapter(
            gameEventBus,
            new DungeonRuntimeAggregateRootStore(),
            rulesProvider));

    private static CoreSessionRulesSO LoadRules()
    {
        return AssetDatabase.LoadAssetAtPath<CoreSessionRulesSO>(
                "Assets/Resources/SO/Content/CoreSessionRules.asset")
            ?? throw new InvalidOperationException(
                "Authored CoreSessionRules asset is missing.");
    }

    private sealed class FixedRulesProvider : ICoreSessionRulesProvider
    {
        internal FixedRulesProvider(CoreSessionRulesDefinition rules)
        {
            CoreSessionRules = rules
                ?? throw new ArgumentNullException(nameof(rules));
        }

        public CoreSessionRulesDefinition CoreSessionRules { get; }
    }
}
#endif
