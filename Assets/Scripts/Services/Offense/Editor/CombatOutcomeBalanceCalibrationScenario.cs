#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

// Measures authored campaign encounters through the real battle runtime.
// Diagnostic schema V2: stalled combatant snapshots.
public static class CombatOutcomeBalanceCalibrationScenario
{
    private const int SamplesPerEncounter = 1_000;
    private const int MaximumCommandsPerBattle = 600;
    public const string ReportPath =
        "Artifacts/QA/combat-outcome-balance.txt";
    public const string CandidateReportPath =
        "Artifacts/QA/combat-balance-candidate-search.txt";
    public const string FocusedReportPath =
        "Artifacts/QA/combat-balance-focused-grid.txt";
    public const string FocusedSearchReportPath =
        "Artifacts/QA/combat-balance-focused-search.txt";
    public const string CandidateCheckpointDirectory =
        "Artifacts/QA/combat-balance-candidates";
    public const string FinalCheckpointDirectory =
        "Artifacts/QA/combat-balance-final";
    public const string FinalCheckpointAggregateReportPath =
        "Artifacts/QA/combat-balance-final.txt";
    public const string AuthoredSmokeReportPath =
        "Artifacts/QA/combat-authored-smoke.txt";

    [MenuItem("DungeonStory/Balance/Run Combat Outcome Calibration")]
    public static void RunFromMenu() => Debug.Log(Run());

    [MenuItem("DungeonStory/Balance/Run Combat Power Sweep")]
    public static void RunPowerSweepFromMenu() =>
        Debug.Log(RunPowerSweep());

    [MenuItem("DungeonStory/Balance/Search Combat Outcome Candidates")]
    public static void RunCandidateSearchFromMenu() =>
        Debug.Log(RunCandidateSearch());

    [MenuItem("DungeonStory/Balance/Verify Production Hook Pull")]
    public static void RunHookPullRegressionFromMenu() =>
        Debug.Log(RunHookPullRegression());

    [MenuItem("DungeonStory/Balance/Verify Route Encounter Diversity")]
    public static void RunRouteDiversityRegressionFromMenu() =>
        Debug.Log(RunRouteDiversityRegression());

    [MenuItem("DungeonStory/Balance/Verify Protect Objective Tactics")]
    public static void RunProtectObjectiveTacticsRegressionFromMenu() =>
        Debug.Log(RunProtectObjectiveTacticsRegression());

    [MenuItem("DungeonStory/Balance/Verify All Final Combat Checkpoints")]
    public static void RunAllFinalVerificationCheckpointsFromMenu() =>
        Debug.Log(RunAllFinalVerificationCheckpoints());

    public static string RunAllFinalVerificationCheckpoints()
    {
        List<string> failures = new();
        foreach (CombatEncounterCalibration value in
                 CombatBalanceCheckpointAuthority.AllEncounters)
        {
            try
            {
                RunFinalVerificationCheckpoint(
                    value.EncounterId,
                    value.EnemyHealthMultiplier,
                    value.EnemyDamageMultiplier,
                    value.ObjectiveHealthMultiplier,
                    value.ObjectiveRoundLimit,
                    value.EnemyAccuracyMultiplier,
                    value.ObjectiveControlResistanceMultiplier,
                    value.AdditionalEnemyCount);
            }
            catch (Exception exception)
            {
                failures.Add(value.EncounterId + ":" + exception.Message);
            }
        }

        StringBuilder report = new();
        report.AppendLine(
            $"RESULT={(failures.Count == 0 ? "PASS" : "FAIL")}; "
            + $"encounters={CombatBalanceCheckpointAuthority.AllEncounters.Count}; "
            + $"samplesPerEncounter={SamplesPerEncounter}; failures={failures.Count}");
        report.AppendLine("COMBAT_BALANCE_ALL_FINAL_CHECKPOINTS_V1");
        report.AppendLine("authority=CombatBalanceCheckpointAuthority.AllEncounters");
        foreach (string failure in failures)
        {
            report.AppendLine("FAILURE=" + failure);
        }
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(report.ToString());
        V27BalanceArtifactWriter.WriteIfDifferent(
            FinalCheckpointAggregateReportPath,
            stream => stream.Write(bytes, 0, bytes.Length));
        AssetDatabase.Refresh();
        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                $"Combat final checkpoints failed: {failures.Count}. See "
                + FinalCheckpointAggregateReportPath);
        }
        return $"COMBAT_BALANCE_ALL_FINAL_CHECKPOINTS=PASS; "
            + $"encounters={CombatBalanceCheckpointAuthority.AllEncounters.Count}; "
            + $"samples={SamplesPerEncounter}; report={FinalCheckpointAggregateReportPath}";
    }

    public static string RunFinalVerificationCheckpointBatch(int first, int last)
    {
        if (first < 1 || last > CombatBalanceCheckpointAuthority.AllEncounters.Count
            || first > last)
        {
            throw new ArgumentOutOfRangeException(
                nameof(first),
                $"Combat checkpoint batch must be within 1-"
                + $"{CombatBalanceCheckpointAuthority.AllEncounters.Count}: {first}-{last}.");
        }
        for (int encounterNumber = first; encounterNumber <= last; encounterNumber++)
        {
            CombatEncounterCalibration value =
                CombatBalanceCheckpointAuthority.RequireEncounter(encounterNumber);
            RunFinalVerificationCheckpoint(
                value.EncounterId,
                value.EnemyHealthMultiplier,
                value.EnemyDamageMultiplier,
                value.ObjectiveHealthMultiplier,
                value.ObjectiveRoundLimit,
                value.EnemyAccuracyMultiplier,
                value.ObjectiveControlResistanceMultiplier,
                value.AdditionalEnemyCount);
        }
        return $"COMBAT_BALANCE_FINAL_BATCH=PASS; first={first}; last={last}; "
            + $"samplesPerEncounter={SamplesPerEncounter}";
    }

    public static string FinalizeAppliedCombatCheckpointEvidence()
    {
        List<string> failures = new();
        foreach (CombatEncounterCalibration value in
                 CombatBalanceCheckpointAuthority.AllEncounters)
        {
            string path = Path.Combine(
                FinalCheckpointDirectory,
                $"encounter-{value.EncounterNumber:00}.txt");
            string[] lines = File.Exists(path)
                ? File.ReadAllLines(path, Encoding.UTF8)
                : Array.Empty<string>();
            string expectedParameters =
                $"health={value.EnemyHealthMultiplier.ToString("R", CultureInfo.InvariantCulture)}; "
                + $"damage={value.EnemyDamageMultiplier.ToString("R", CultureInfo.InvariantCulture)}; "
                + $"accuracy={value.EnemyAccuracyMultiplier.ToString("R", CultureInfo.InvariantCulture)}; "
                + $"objectiveHealth={value.ObjectiveHealthMultiplier.ToString("R", CultureInfo.InvariantCulture)}; "
                + $"controlResistance={value.ObjectiveControlResistanceMultiplier.ToString("R", CultureInfo.InvariantCulture)}; "
                + $"additionalEnemyCount={value.AdditionalEnemyCount.ToString(CultureInfo.InvariantCulture)}; "
                + $"round={value.ObjectiveRoundLimit.ToString(CultureInfo.InvariantCulture)}";
            if (lines.Length < 6
                || !lines[0].StartsWith(
                    "RESULT=PASS; samples=1000; failures=0; stalled=0",
                    StringComparison.Ordinal)
                || !string.Equals(lines[2], "enemyDecisionAuthority=EnemyTacticalDecisionService",
                    StringComparison.Ordinal)
                || !string.Equals(lines[5], expectedParameters, StringComparison.Ordinal))
            {
                failures.Add(value.EncounterId);
            }
        }
        StringBuilder report = new();
        report.AppendLine(
            $"RESULT={(failures.Count == 0 ? "PASS" : "FAIL")}; "
            + $"encounters={CombatBalanceCheckpointAuthority.AllEncounters.Count}; "
            + $"samplesPerEncounter={SamplesPerEncounter}; failures={failures.Count}");
        report.AppendLine("COMBAT_BALANCE_ALL_FINAL_CHECKPOINTS_V1");
        report.AppendLine("authority=CombatBalanceCheckpointAuthority.AllEncounters");
        foreach (string failure in failures)
        {
            report.AppendLine("FAILURE=" + failure);
        }
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(report.ToString());
        V27BalanceArtifactWriter.WriteIfDifferent(
            FinalCheckpointAggregateReportPath,
            stream => stream.Write(bytes, 0, bytes.Length));
        AssetDatabase.Refresh();
        if (failures.Count > 0)
        {
            throw new InvalidOperationException(
                $"Combat checkpoint evidence finalization failed: {failures.Count}.");
        }
        return $"COMBAT_BALANCE_APPLIED_FINAL=PASS; "
            + $"encounters={CombatBalanceCheckpointAuthority.AllEncounters.Count}; "
            + $"samplesPerEncounter={SamplesPerEncounter}";
    }

    public static string RunProtectObjectiveTacticsRegression()
    {
        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        EnemyCombatContentCatalog combat = new(content);
        ResourceOffenseCampaignCatalog campaigns = new(content);
        ICombatEquipmentRuntime equipmentRuntime =
            OffenseEditorTestDependencies.CreateCombatEquipmentRuntime();
        EnemyEncounterFactory factory = (EnemyEncounterFactory)
            OffenseEditorTestDependencies.CreateEnemyEncounterFactory(
                equipmentRuntime);
        OffenseTargetDefinition target = campaigns.Targets.Single(value =>
            value.campaignOrder == 6);
        OffenseEncounterSO encounter = ((IEncounterCatalog)combat)
            .Require("encounter:33");
        EnemyEncounterComposition composition = CreateSpecificEncounter(
            factory,
            target,
            encounter.encounterId,
            sample: 0,
            encounter);
        EnemyIndividualSaveData individual = composition.Individuals
            .OrderBy(value => value.characterId, StringComparer.Ordinal)
            .First();
        OffenseBattleCombatant projected = composition.Combatants.Single(value =>
            string.Equals(
                value.PersistentId,
                individual.characterId,
                StringComparison.Ordinal));
        OffenseBattleCombatant enemy = new(
            projected.PersistentId,
            projected.DisplayName,
            projected.SpeciesTag,
            OffenseBattleTeam.Enemies,
            new OffenseBattleStats(
                projected.Stats.MaxHealth,
                projected.Stats.Attack,
                projected.Stats.Strength,
                projected.Stats.Toughness,
                100f,
                100f,
                projected.Stats.Shooting,
                projected.Stats.Evasion),
            projected.CurrentHealth,
            projected.Abilities,
            formation: OffenseFormationSlot.Front);
        enemy.SetCombatEquipment(
            projected.Weapon,
            projected.Armor,
            projected.Shield);
        OffenseBattleCombatant objective = new(
            "ally:protected-objective",
            "Protected Objective",
            "Human",
            OffenseBattleTeam.Allies,
            new OffenseBattleStats(100f, 1f, 1f, 1f, 1f, 1f),
            100f,
            Array.Empty<CharacterCombatAbilityDefinition>(),
            formation: OffenseFormationSlot.Front,
            participatesInInitiative: false);
        OffenseBattleCombatant decoy = new(
            "ally:low-health-decoy",
            "Low Health Decoy",
            "Human",
            OffenseBattleTeam.Allies,
            new OffenseBattleStats(100f, 20f, 1f, 1f, 1f, 1f),
            10f,
            Array.Empty<CharacterCombatAbilityDefinition>(),
            formation: OffenseFormationSlot.Front);
        OffenseBattleEncounterRules rules = new(
            OffenseEncounterObjective.ProtectTarget,
            encounter.objectiveRoundLimit,
            encounter.objectiveTargetId,
            objective.PersistentId,
            Array.Empty<BattlefieldModifierDefinitionSO>());
        OffenseBattleSession session = new(
            "balance:protect-objective-tactics",
            "balance-expedition:protect-objective-tactics",
            target.id,
            target.title,
            DungeonDifficulty.Normal,
            new[] { enemy, objective, decoy },
            OffenseEditorTestDependencies.CreateCombatResolution(),
            equipmentRuntime,
            rules);
        if (!ReferenceEquals(session.CurrentActor, enemy))
        {
            throw new InvalidOperationException(
                "Protect-objective tactics regression did not start on the enemy turn.");
        }
        EnemyTacticalDecision decision = new EnemyTacticalDecisionService(combat)
            .Decide(session, individual);
        if (decision.Intent is not (
                EnemyTacticalIntentKind.Attack
                or EnemyTacticalIntentKind.UseAbility)
            || !string.Equals(
                decision.TargetId,
                objective.PersistentId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Production tactics did not prioritize the legal protected objective: "
                + $"intent={decision.Intent}; target={decision.TargetId}; "
                + $"objective={objective.PersistentId}; decoy={decoy.PersistentId}.");
        }
        return "PASS PRODUCTION_PROTECT_OBJECTIVE_TARGET_PRIORITY";
    }

    public static string RunRouteDiversityRegression()
    {
        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        EnemyCombatContentCatalog combat = new(content);
        ResourceOffenseCampaignCatalog campaigns = new(content);
        ICombatEquipmentRuntime equipmentRuntime =
            OffenseEditorTestDependencies.CreateCombatEquipmentRuntime();
        EnemyEncounterFactory factory = (EnemyEncounterFactory)
            OffenseEditorTestDependencies.CreateEnemyEncounterFactory(
                equipmentRuntime);
        OffenseTargetDefinition target = campaigns.Targets.Single(value =>
            value.campaignOrder == 2);
        OffenseEncounterSO encounter = ((IEncounterCatalog)combat)
            .Require("encounter:07");
        EnemyEncounterComposition composition = CreateSpecificEncounter(
            factory,
            target,
            encounter.encounterId,
            sample: 0,
            encounter);
        string[] archetypes = composition.Individuals
            .Select(value => value.enemyArchetypeId)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] expected =
        {
            "enemy:legion-pavise",
            "enemy:legion-pikeman"
        };
        if (!archetypes.SequenceEqual(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                "Route encounter truncation did not preserve authored archetype diversity: "
                + string.Join(",", archetypes));
        }
        return "PASS ROUTE_ENCOUNTER_AUTHORED_DIVERSITY_PRESERVED";
    }

    public static string RunHookPullRegression()
    {
        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        EnemyCombatContentCatalog combat = new(content);
        ResourceOffenseCampaignCatalog campaigns = new(content);
        ICombatEquipmentRuntime equipmentRuntime =
            OffenseEditorTestDependencies.CreateCombatEquipmentRuntime();
        EnemyEncounterFactory factory = (EnemyEncounterFactory)
            OffenseEditorTestDependencies.CreateEnemyEncounterFactory(
                equipmentRuntime);
        OffenseTargetDefinition target = campaigns.Targets.Single(value =>
            value.campaignOrder == 5);
        OffenseEncounterSO encounter = ((IEncounterCatalog)combat)
            .Require("encounter:27");
        EnemyEncounterComposition composition = CreateSpecificEncounter(
            factory,
            target,
            encounter.encounterId,
            sample: 0,
            encounter);
        EnemyIndividualSaveData individual = composition.Individuals.Single(value =>
            string.Equals(
                value.enemyArchetypeId,
                "enemy:rival-packbreaker",
                StringComparison.Ordinal));
        OffenseBattleCombatant projected = composition.Combatants.Single(value =>
            string.Equals(
                value.PersistentId,
                individual.characterId,
                StringComparison.Ordinal));
        CharacterCombatAbilityDefinition hook = projected.Abilities.Single(value =>
            string.Equals(
                value.Id,
                "enemy-ability:hook-pull",
                StringComparison.Ordinal));
        OffenseRepositionEffect reposition = hook.Effects
            .OfType<OffenseRepositionEffect>()
            .SingleOrDefault();
        OffenseDelayEffect delay = hook.Effects
            .OfType<OffenseDelayEffect>()
            .SingleOrDefault();
        if (reposition?.Offset != -2 || delay == null)
        {
            throw new InvalidOperationException(
                "Hook pull did not project the authored backline reposition and delay effects.");
        }

        OffenseBattleCombatant enemy = new(
            individual.characterId,
            projected.DisplayName,
            projected.SpeciesTag,
            OffenseBattleTeam.Enemies,
            projected.Stats,
            projected.CurrentHealth,
            new[] { hook },
            formation: OffenseFormationSlot.Front);
        enemy.SetCombatEquipment(
            projected.Weapon,
            projected.Armor,
            projected.Shield);
        OffenseBattleCombatant rearTarget = new(
            "ally:hook-pull-regression",
            "Hook Pull Rear Target",
            "Human",
            OffenseBattleTeam.Allies,
            new OffenseBattleStats(100f, 4f, 4f, 4f, 1f, 1f, 4f, 1f),
            100f,
            Array.Empty<CharacterCombatAbilityDefinition>(),
            formation: OffenseFormationSlot.Rear);
        OffenseBattleSession session = new(
            "balance:hook-pull-regression",
            "balance-expedition:hook-pull-regression",
            target.id,
            target.title,
            DungeonDifficulty.Normal,
            new[] { enemy, rearTarget },
            OffenseEditorTestDependencies.CreateCombatResolution(),
            equipmentRuntime);
        if (session.CurrentActor != enemy)
        {
            throw new InvalidOperationException(
                "Hook-pull regression did not start on the authored enemy turn.");
        }
        EnemyTacticalDecision decision = new EnemyTacticalDecisionService(combat)
            .Decide(session, individual);
        if (decision.Intent != EnemyTacticalIntentKind.UseAbility
            || !string.Equals(decision.AbilityId, hook.Id, StringComparison.Ordinal)
            || !string.Equals(
                decision.TargetId,
                rearTarget.PersistentId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Production tactics did not choose hook pull: intent={decision.Intent}; "
                + $"ability={decision.AbilityId}; target={decision.TargetId}.");
        }
        float initiativeBefore = rearTarget.InitiativePenalty;
        OffenseBattleCommand command = CreateEnemyTacticalCommand(
            session,
            decision,
            commandId: 1);
        if (!session.TryExecuteCommand(command, out OffenseBattleCommandResult result)
            || rearTarget.Formation != OffenseFormationSlot.Front
            || rearTarget.InitiativePenalty <= initiativeBefore)
        {
            throw new InvalidOperationException(
                "Production hook pull did not move Rear->Front and apply delay: "
                + $"accepted={result?.Accepted}; formation={rearTarget.Formation}; "
                + $"initiativePenalty={rearTarget.InitiativePenalty:0.###}.");
        }
        return "PASS PRODUCTION_HOOK_PULL_PROJECTED_AND_EXECUTED";
    }

    public static string Run()
    {
        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        EnemyCombatContentCatalog combat = new(content);
        ResourceOffenseCampaignCatalog campaigns = new(content);
        ResourceCombatEquipmentCatalog equipment = new(content);
        DungeonRuntimeAggregateRootStore rootStore = new();
        CharacterNarrativeCatalog narrativeCatalog = new(content);
        CharacterNarrativeRuntime narrative = new(rootStore, narrativeCatalog);
        ResourceCharacterSpeciesCatalog species = new(content);
        CharacterLifeRuntime life = new(
            rootStore,
            species,
            new RandomStreamProvider(rootStore));
        EnemyIndividualFactory individuals = new(
            combat,
            narrativeCatalog,
            narrative,
            narrative,
            life,
            life,
            species,
            content);
        EnemyEncounterFactory factory = new(
            combat,
            combat,
            combat,
            combat,
            individuals);

        List<EncounterOutcome> outcomes = new();
        foreach (OffenseTargetDefinition target in campaigns.Targets
            .OrderBy(value => value.campaignOrder))
        {
            for (int variant = 1; variant <= 6; variant++)
            {
                string encounterId =
                    $"encounter:{((target.campaignOrder - 1) * 6 + variant):00}";
                EncounterOutcome outcome = SimulateEncounter(
                    target,
                    encounterId,
                    factory,
                    combat,
                    equipment,
                    SamplesPerEncounter,
                    1f);
                outcomes.Add(outcome);
            }
        }

        int stalled = outcomes.Sum(value => value.Stalled);
        List<string> balanceFailures = outcomes
            .SelectMany(ValidateOutcomeBand)
            .ToList();
        bool passed = stalled == 0 && balanceFailures.Count == 0;
        StringBuilder report = new();
        report.AppendLine(
            $"RESULT={(passed ? "PASS" : "FAIL")}; "
            + $"samplesPerEncounter={SamplesPerEncounter}; encounters={outcomes.Count}; "
            + $"stalled={stalled}; balanceFailures={balanceFailures.Count}");
        report.AppendLine("COMBAT_OUTCOME_BALANCE_V27");
        report.AppendLine(
            $"samplesPerEncounter={SamplesPerEncounter}; encounters={outcomes.Count}; "
            + $"stallDetails={outcomes.Sum(value => value.StalledDetails.Count)}");
        report.AppendLine(
            "encounter | campaign | risk | objective | party | required power | wins | win rate | target win | median rounds | mean ally health | severe Dead/Downed among victories | target severe | low-health among victories | failure casualty | stalled | capture down/dead/active | shots/executed/hits | end/peak sedation");
        foreach (EncounterOutcome outcome in outcomes)
        {
            report.AppendLine(string.Join(" | ",
                outcome.EncounterId,
                outcome.Campaign.ToString(CultureInfo.InvariantCulture),
                outcome.RiskLabel,
                outcome.Objective,
                outcome.PartyMembers.ToString(CultureInfo.InvariantCulture),
                outcome.RequiredPower.ToString("0.0", CultureInfo.InvariantCulture),
                outcome.Wins.ToString(CultureInfo.InvariantCulture),
                outcome.WinRate.ToString("P1", CultureInfo.InvariantCulture),
                outcome.WinTarget,
                Median(outcome.Rounds).ToString("0.0", CultureInfo.InvariantCulture),
                outcome.MeanAllyHealthRatio.ToString("P1", CultureInfo.InvariantCulture),
                outcome.SevereCasualtyRate.ToString("P1", CultureInfo.InvariantCulture),
                outcome.SevereTarget,
                outcome.LowHealthVictoryRate.ToString("P1", CultureInfo.InvariantCulture),
                outcome.FailureCasualtyRate.ToString("P1", CultureInfo.InvariantCulture),
                outcome.Stalled.ToString(CultureInfo.InvariantCulture),
                $"{outcome.CaptureLeaderDowned}/{outcome.CaptureLeaderDeaths}/{outcome.CaptureLeaderActive}",
                $"{outcome.CaptureShotAttempts}/{outcome.CaptureShotCommands}/{outcome.CaptureShotHits}",
                 $"{outcome.MeanCaptureSedation:0.00}/{outcome.MeanCapturePeakSedation:0.00}"));
            foreach (string detail in outcome.StalledDetails)
            {
                report.AppendLine($"  STALL | {detail}");
            }
        }

        if (balanceFailures.Count > 0)
        {
            report.AppendLine("BALANCE_FAILURES");
            foreach (string failure in balanceFailures)
            {
                report.AppendLine("- " + failure);
            }
        }

        report.AppendLine("CAMPAIGN_SUMMARY");
        foreach (IGrouping<int, EncounterOutcome> campaign in outcomes
            .GroupBy(value => value.Campaign)
            .OrderBy(group => group.Key))
        {
            report.AppendLine(
                $"campaign={campaign.Key}; win_rate={campaign.Average(value => value.WinRate):P1}; "
                + $"min={campaign.Min(value => value.WinRate):P1}; "
                + $"max={campaign.Max(value => value.WinRate):P1}; "
                + $"severe={campaign.Average(value => value.SevereCasualtyRate):P1}");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath) ?? ".");
        byte[] reportBytes = new UTF8Encoding(false, true).GetBytes(report.ToString());
        V27BalanceArtifactWriter.WriteIfDifferent(ReportPath, stream =>
            stream.Write(reportBytes, 0, reportBytes.Length));
        AssetDatabase.Refresh();

        if (!passed)
        {
            throw new InvalidOperationException(
                $"Combat outcome calibration failed: stalled={stalled}; "
                + $"balance={balanceFailures.Count}. See {ReportPath}.");
        }

        return "COMBAT_OUTCOME_BALANCE=MEASURED; campaign_win_rates="
            + string.Join("/", outcomes
                .GroupBy(value => value.Campaign)
                .OrderBy(group => group.Key)
                .Select(group => group.Average(value => value.WinRate)
                    .ToString("P1", CultureInfo.InvariantCulture)));
    }

    public static string RunAuthoredSmoke(int samples = 64)
    {
        samples = Mathf.Clamp(samples, 1, 256);
        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        EnemyCombatContentCatalog combat = new(content);
        ResourceOffenseCampaignCatalog campaigns = new(content);
        ResourceCombatEquipmentCatalog equipment = new(content);
        DungeonRuntimeAggregateRootStore rootStore = new();
        CharacterNarrativeCatalog narrativeCatalog = new(content);
        CharacterNarrativeRuntime narrative = new(rootStore, narrativeCatalog);
        ResourceCharacterSpeciesCatalog species = new(content);
        CharacterLifeRuntime life = new(
            rootStore,
            species,
            new RandomStreamProvider(rootStore));
        EnemyIndividualFactory individuals = new(
            combat,
            narrativeCatalog,
            narrative,
            narrative,
            life,
            life,
            species,
            content);
        EnemyEncounterFactory factory = new(
            combat,
            combat,
            combat,
            combat,
            individuals);
        StringBuilder report = new();
        report.AppendLine("COMBAT_AUTHORED_SMOKE_V2");
        report.AppendLine(
            $"samples={samples}; enemyDecisionAuthority=EnemyTacticalDecisionService");
        report.AppendLine(
            "encounter|objective|party|win|severe|lowHealth|failureCasualty|"
            + "meanHealth|enemyBasic|enemyAbility|enemyGuard|enemyAdvance|"
            + "enemyRetreat|enemyRejected|stalled|bandFailures");
        foreach (OffenseTargetDefinition target in campaigns.Targets
            .OrderBy(value => value.campaignOrder))
        {
            for (int variant = 1; variant <= 6; variant++)
            {
                string encounterId =
                    $"encounter:{((target.campaignOrder - 1) * 6 + variant):00}";
                EncounterOutcome outcome = SimulateEncounter(
                    target,
                    encounterId,
                    factory,
                    combat,
                    equipment,
                    samples,
                    partyPowerMultiplier: 1f,
                    stratifiedSamples: true);
                string[] failures = ValidateOutcomeBand(outcome).ToArray();
                report.AppendLine(string.Join("|",
                    encounterId,
                    outcome.Objective,
                    outcome.PartyMembers.ToString(CultureInfo.InvariantCulture),
                    outcome.WinRate.ToString("R", CultureInfo.InvariantCulture),
                    outcome.SevereCasualtyRate.ToString("R", CultureInfo.InvariantCulture),
                    outcome.LowHealthVictoryRate.ToString("R", CultureInfo.InvariantCulture),
                    outcome.FailureCasualtyRate.ToString("R", CultureInfo.InvariantCulture),
                    outcome.MeanAllyHealthRatio.ToString("R", CultureInfo.InvariantCulture),
                    outcome.EnemyBasicCommands.ToString(CultureInfo.InvariantCulture),
                    outcome.EnemyAbilityCommands.ToString(CultureInfo.InvariantCulture),
                    outcome.EnemyGuardCommands.ToString(CultureInfo.InvariantCulture),
                    outcome.EnemyAdvanceCommands.ToString(CultureInfo.InvariantCulture),
                    outcome.EnemyRetreatCommands.ToString(CultureInfo.InvariantCulture),
                    outcome.EnemyRejectedCommands.ToString(CultureInfo.InvariantCulture),
                    outcome.Stalled.ToString(CultureInfo.InvariantCulture),
                    failures.Length.ToString(CultureInfo.InvariantCulture)));
            }
        }
        Directory.CreateDirectory(
            Path.GetDirectoryName(AuthoredSmokeReportPath) ?? ".");
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(report.ToString());
        V27BalanceArtifactWriter.WriteIfDifferent(AuthoredSmokeReportPath, stream =>
            stream.Write(bytes, 0, bytes.Length));
        AssetDatabase.Refresh();
        return $"COMBAT_AUTHORED_SMOKE=MEASURED; samples={samples}; "
            + $"report={AuthoredSmokeReportPath}";
    }

    public static string RunPowerSweep()
    {
        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        EnemyCombatContentCatalog combat = new(content);
        ResourceOffenseCampaignCatalog campaigns = new(content);
        ResourceCombatEquipmentCatalog equipment = new(content);
        DungeonRuntimeAggregateRootStore rootStore = new();
        CharacterNarrativeCatalog narrativeCatalog = new(content);
        CharacterNarrativeRuntime narrative = new(rootStore, narrativeCatalog);
        ResourceCharacterSpeciesCatalog species = new(content);
        CharacterLifeRuntime life = new(
            rootStore,
            species,
            new RandomStreamProvider(rootStore));
        EnemyIndividualFactory individuals = new(
            combat,
            narrativeCatalog,
            narrative,
            narrative,
            life,
            life,
            species,
            content);
        EnemyEncounterFactory factory = new(
            combat,
            combat,
            combat,
            combat,
            individuals);

        float[] multipliers = { 1f, 1.5f, 2f, 2.5f, 3f, 4f, 6f, 8f };
        const int sweepSamples = 8;
        StringBuilder report = new();
        report.AppendLine("COMBAT_POWER_SWEEP_V1");
        report.AppendLine(
            "multiplier | campaign | mean win | minimum | maximum | severe");
        foreach (float multiplier in multipliers)
        {
            List<EncounterOutcome> outcomes = new();
            foreach (OffenseTargetDefinition target in campaigns.Targets
                .OrderBy(value => value.campaignOrder))
            {
                for (int variant = 1; variant <= 6; variant++)
                {
                    string encounterId =
                        $"encounter:{((target.campaignOrder - 1) * 6 + variant):00}";
                    outcomes.Add(SimulateEncounter(
                        target,
                        encounterId,
                        factory,
                        combat,
                        equipment,
                        sweepSamples,
                        multiplier));
                }
            }

            foreach (IGrouping<int, EncounterOutcome> campaign in outcomes
                .GroupBy(value => value.Campaign)
                .OrderBy(group => group.Key))
            {
                report.AppendLine(string.Join(" | ",
                    multiplier.ToString("0.0", CultureInfo.InvariantCulture),
                    campaign.Key.ToString(CultureInfo.InvariantCulture),
                    campaign.Average(value => value.WinRate)
                        .ToString("P1", CultureInfo.InvariantCulture),
                    campaign.Min(value => value.WinRate)
                        .ToString("P1", CultureInfo.InvariantCulture),
                    campaign.Max(value => value.WinRate)
                        .ToString("P1", CultureInfo.InvariantCulture),
                    campaign.Average(value => value.SevereCasualtyRate)
                        .ToString("P1", CultureInfo.InvariantCulture)));
            }
            foreach (EncounterOutcome outcome in outcomes)
            {
                report.AppendLine(
                    $"detail | {multiplier:0.0} | {outcome.EncounterId} | "
                    + $"{outcome.Objective} | {outcome.WinRate:P1} | "
                    + $"severe={outcome.SevereCasualtyRate:P1} | "
                    + $"capture={outcome.CaptureLeaderDowned}/"
                    + $"{outcome.CaptureLeaderDeaths}/"
                    + $"{outcome.CaptureLeaderActive} | "
                    + $"shots={outcome.CaptureShotAttempts}/"
                    + $"{outcome.CaptureShotCommands}/"
                    + $"{outcome.CaptureShotHits} | "
                    + $"sedation={outcome.MeanCaptureSedation:0.00}/"
                    + $"{outcome.MeanCapturePeakSedation:0.00}");
            }
        }

        const string path = "Artifacts/QA/combat-power-sweep.txt";
        Directory.CreateDirectory(Path.GetDirectoryName(path) ?? ".");
        File.WriteAllText(path, report.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();
        return $"COMBAT_POWER_SWEEP=MEASURED; report={path}";
    }

    public static string RunCandidateSearch()
    {
        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        EnemyCombatContentCatalog combat = new(content);
        ResourceOffenseCampaignCatalog campaigns = new(content);
        ResourceCombatEquipmentCatalog equipment = new(content);
        DungeonRuntimeAggregateRootStore rootStore = new();
        CharacterNarrativeCatalog narrativeCatalog = new(content);
        CharacterNarrativeRuntime narrative = new(rootStore, narrativeCatalog);
        ResourceCharacterSpeciesCatalog species = new(content);
        CharacterLifeRuntime life = new(
            rootStore,
            species,
            new RandomStreamProvider(rootStore));
        EnemyIndividualFactory individuals = new(
            combat,
            narrativeCatalog,
            narrative,
            narrative,
            life,
            life,
            species,
            content);
        EnemyEncounterFactory factory = new(
            combat,
            combat,
            combat,
            combat,
            individuals);

        float[] damageCandidates =
        {
            0.1f, 0.15f, 0.2f, 0.3f, 0.45f, 0.6f, 0.8f,
            1f, 1.25f, 1.6f, 2.2f, 3.2f, 4f
        };
        const int broadSamples = 8;
        const int refineSamples = 24;
        const int confirmationSamples = 256;
        StringBuilder report = new();
        report.AppendLine("COMBAT_BALANCE_CANDIDATE_SEARCH_V1");
        report.AppendLine(
            $"broadSamples={broadSamples}; refineSamples={refineSamples}; "
            + $"confirmationSamples={confirmationSamples}; "
            + $"search=monotonic-bisection-v3; "
            + $"damageSeeds={damageCandidates.Length}");
        report.AppendLine(
            "encounter | objective | before health/damage/round | "
            + "candidate health/damage/accuracy/objectiveHealth/control/round | "
            + "win | severe | low-health | score | stalled");

        foreach (OffenseTargetDefinition target in campaigns.Targets
            .OrderBy(value => value.campaignOrder))
        {
            for (int variant = 1; variant <= 6; variant++)
            {
                string encounterId =
                    $"encounter:{((target.campaignOrder - 1) * 6 + variant):00}";
                OffenseEncounterSO authored =
                    ((IEncounterCatalog)combat).Require(encounterId);
                CombatCandidate best = FindBestCandidate(
                    target,
                    encounterId,
                    authored,
                    factory,
                    combat,
                    equipment,
                    damageCandidates,
                    broadSamples,
                    refineSamples,
                    confirmationSamples);
                report.AppendLine(string.Join(" | ",
                    encounterId,
                    authored.objective,
                    $"{authored.enemyHealthMultiplier:0.###}/"
                        + $"{authored.enemyDamageMultiplier:0.###}/"
                        + authored.objectiveRoundLimit,
                    $"{best.Health:0.###}/{best.Damage:0.###}/"
                        + $"{best.Accuracy:0.###}/{best.ObjectiveHealth:0.###}/"
                        + $"{best.ControlResistance:0.###}/{best.RoundLimit}",
                    best.Outcome.WinRate.ToString("P1", CultureInfo.InvariantCulture),
                    best.Outcome.SevereCasualtyRate.ToString(
                        "P1",
                        CultureInfo.InvariantCulture),
                    best.Outcome.LowHealthVictoryRate.ToString(
                        "P1",
                        CultureInfo.InvariantCulture),
                    best.Score.ToString("0.000000", CultureInfo.InvariantCulture),
                    best.Outcome.Stalled.ToString(CultureInfo.InvariantCulture)));
                WriteCandidateSearchReport(report);
            }
        }

        WriteCandidateSearchReport(report);
        AssetDatabase.Refresh();
        return $"COMBAT_BALANCE_CANDIDATE_SEARCH=MEASURED; report={CandidateReportPath}";
    }

    private static void WriteCandidateSearchReport(StringBuilder report)
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(CandidateReportPath) ?? ".");
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(
            report?.ToString() ?? string.Empty);
        V27BalanceArtifactWriter.WriteIfDifferent(CandidateReportPath, stream =>
            stream.Write(bytes, 0, bytes.Length));
    }

    public static string RunCandidateSearchCheckpoint(string encounterId)
    {
        int encounterNumber = ParseEncounterNumber(encounterId);
        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        EnemyCombatContentCatalog combat = new(content);
        ResourceOffenseCampaignCatalog campaigns = new(content);
        ResourceCombatEquipmentCatalog equipment = new(content);
        DungeonRuntimeAggregateRootStore rootStore = new();
        CharacterNarrativeCatalog narrativeCatalog = new(content);
        CharacterNarrativeRuntime narrative = new(rootStore, narrativeCatalog);
        ResourceCharacterSpeciesCatalog species = new(content);
        CharacterLifeRuntime life = new(
            rootStore,
            species,
            new RandomStreamProvider(rootStore));
        EnemyIndividualFactory individuals = new(
            combat,
            narrativeCatalog,
            narrative,
            narrative,
            life,
            life,
            species,
            content);
        EnemyEncounterFactory factory = new(
            combat,
            combat,
            combat,
            combat,
            individuals);
        OffenseTargetDefinition target = campaigns.Targets.Single(value =>
            value.campaignOrder == (encounterNumber - 1) / 6 + 1);
        OffenseEncounterSO authored = ((IEncounterCatalog)combat).Require(encounterId);
        float[] damageCandidates =
        {
            0.1f, 0.15f, 0.2f, 0.3f, 0.45f, 0.6f, 0.8f,
            1f, 1.25f, 1.6f, 2.2f, 3.2f, 4f
        };
        CombatCandidate best = FindBestCandidate(
            target,
            encounterId,
            authored,
            factory,
            combat,
            equipment,
            damageCandidates,
            broadSamples: 8,
            refineSamples: 24,
            confirmationSamples: 256);
        StringBuilder report = new();
        report.AppendLine("COMBAT_BALANCE_CANDIDATE_CHECKPOINT_V2");
        report.AppendLine("enemyDecisionAuthority=EnemyTacticalDecisionService");
        report.AppendLine(
            "encounter|objective|health|damage|accuracy|objectiveHealth|control|round|win|severe|"
            + "lowHealth|failureCasualty|medianRounds|meanHealth|score|stalled");
        report.AppendLine(string.Join("|",
            encounterId,
            authored.objective,
            best.Health.ToString("0.###", CultureInfo.InvariantCulture),
            best.Damage.ToString("0.###", CultureInfo.InvariantCulture),
            best.Accuracy.ToString("0.###", CultureInfo.InvariantCulture),
            best.ObjectiveHealth.ToString("0.###", CultureInfo.InvariantCulture),
            best.ControlResistance.ToString("0.###", CultureInfo.InvariantCulture),
            best.RoundLimit.ToString(CultureInfo.InvariantCulture),
            best.Outcome.WinRate.ToString("R", CultureInfo.InvariantCulture),
            best.Outcome.SevereCasualtyRate.ToString("R", CultureInfo.InvariantCulture),
            best.Outcome.LowHealthVictoryRate.ToString("R", CultureInfo.InvariantCulture),
            best.Outcome.FailureCasualtyRate.ToString("R", CultureInfo.InvariantCulture),
            Median(best.Outcome.Rounds).ToString("R", CultureInfo.InvariantCulture),
            best.Outcome.MeanAllyHealthRatio.ToString("R", CultureInfo.InvariantCulture),
            best.Score.ToString("R", CultureInfo.InvariantCulture),
            best.Outcome.Stalled.ToString(CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(CandidateCheckpointDirectory);
        string path = Path.Combine(
            CandidateCheckpointDirectory,
            $"encounter-{encounterNumber:00}.txt");
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(report.ToString());
        V27BalanceArtifactWriter.WriteIfDifferent(path, stream =>
            stream.Write(bytes, 0, bytes.Length));
        return $"COMBAT_BALANCE_CANDIDATE_CHECKPOINT=MEASURED; "
            + $"encounter={encounterId}; report={path}";
    }

    public static string RunFocusedCandidateCheckpoint(string encounterId)
    {
        int encounterNumber = ParseEncounterNumber(encounterId);
        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        EnemyCombatContentCatalog combat = new(content);
        ResourceOffenseCampaignCatalog campaigns = new(content);
        ResourceCombatEquipmentCatalog equipment = new(content);
        DungeonRuntimeAggregateRootStore rootStore = new();
        CharacterNarrativeCatalog narrativeCatalog = new(content);
        CharacterNarrativeRuntime narrative = new(rootStore, narrativeCatalog);
        ResourceCharacterSpeciesCatalog species = new(content);
        CharacterLifeRuntime life = new(
            rootStore,
            species,
            new RandomStreamProvider(rootStore));
        EnemyIndividualFactory individuals = new(
            combat,
            narrativeCatalog,
            narrative,
            narrative,
            life,
            life,
            species,
            content);
        EnemyEncounterFactory factory = new(
            combat,
            combat,
            combat,
            combat,
            individuals);
        OffenseTargetDefinition target = campaigns.Targets.Single(value =>
            value.campaignOrder == (encounterNumber - 1) / 6 + 1);
        OffenseEncounterSO authored = ((IEncounterCatalog)combat).Require(encounterId);
        CombatCandidate best = FindFocusedCandidate(
            target,
            encounterId,
            authored,
            factory,
            combat,
            equipment);
        StringBuilder report = new();
        report.AppendLine("COMBAT_BALANCE_FOCUSED_CANDIDATE_CHECKPOINT_V2");
        report.AppendLine("enemyDecisionAuthority=EnemyTacticalDecisionService");
        report.AppendLine(
            "encounter|objective|health|damage|accuracy|objectiveHealth|control|round|win|severe|"
            + "lowHealth|failureCasualty|medianRounds|meanHealth|score|stalled");
        report.AppendLine(string.Join("|",
            encounterId,
            authored.objective,
            best.Health.ToString("0.###", CultureInfo.InvariantCulture),
            best.Damage.ToString("0.###", CultureInfo.InvariantCulture),
            best.Accuracy.ToString("0.###", CultureInfo.InvariantCulture),
            best.ObjectiveHealth.ToString("0.###", CultureInfo.InvariantCulture),
            best.ControlResistance.ToString("0.###", CultureInfo.InvariantCulture),
            best.RoundLimit.ToString(CultureInfo.InvariantCulture),
            best.Outcome.WinRate.ToString("R", CultureInfo.InvariantCulture),
            best.Outcome.SevereCasualtyRate.ToString("R", CultureInfo.InvariantCulture),
            best.Outcome.LowHealthVictoryRate.ToString("R", CultureInfo.InvariantCulture),
            best.Outcome.FailureCasualtyRate.ToString("R", CultureInfo.InvariantCulture),
            Median(best.Outcome.Rounds).ToString("R", CultureInfo.InvariantCulture),
            best.Outcome.MeanAllyHealthRatio.ToString("R", CultureInfo.InvariantCulture),
            best.Score.ToString("R", CultureInfo.InvariantCulture),
            best.Outcome.Stalled.ToString(CultureInfo.InvariantCulture)));
        Directory.CreateDirectory(CandidateCheckpointDirectory);
        string path = Path.Combine(
            CandidateCheckpointDirectory,
            $"focused-encounter-{encounterNumber:00}.txt");
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(report.ToString());
        V27BalanceArtifactWriter.WriteIfDifferent(path, stream =>
            stream.Write(bytes, 0, bytes.Length));
        return $"COMBAT_BALANCE_FOCUSED_CANDIDATE_CHECKPOINT=MEASURED; "
            + $"encounter={encounterId}; report={path}";
    }

    public static string RunFocusedGrid(string encounterId)
    {
        if (string.IsNullOrWhiteSpace(encounterId)
            || !encounterId.StartsWith("encounter:", StringComparison.Ordinal)
            || !int.TryParse(
                encounterId.AsSpan("encounter:".Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int encounterNumber)
            || encounterNumber < 1
            || encounterNumber > 36)
        {
            throw new ArgumentException(
                "Focused combat grid requires encounter:01 through encounter:36.",
                nameof(encounterId));
        }

        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        EnemyCombatContentCatalog combat = new(content);
        ResourceOffenseCampaignCatalog campaigns = new(content);
        ResourceCombatEquipmentCatalog equipment = new(content);
        DungeonRuntimeAggregateRootStore rootStore = new();
        CharacterNarrativeCatalog narrativeCatalog = new(content);
        CharacterNarrativeRuntime narrative = new(rootStore, narrativeCatalog);
        ResourceCharacterSpeciesCatalog species = new(content);
        CharacterLifeRuntime life = new(
            rootStore,
            species,
            new RandomStreamProvider(rootStore));
        EnemyIndividualFactory individuals = new(
            combat,
            narrativeCatalog,
            narrative,
            narrative,
            life,
            life,
            species,
            content);
        EnemyEncounterFactory factory = new(
            combat,
            combat,
            combat,
            combat,
            individuals);
        int campaign = (encounterNumber - 1) / 6 + 1;
        OffenseTargetDefinition target = campaigns.Targets.Single(
            value => value.campaignOrder == campaign);
        OffenseEncounterSO authored = ((IEncounterCatalog)combat).Require(
            encounterId);
        float[] healthValues =
        {
            0.02f, 0.05f, 0.1f, 0.2f, 0.4f, 0.8f,
            1.6f, 3.2f, 6.4f, 12.8f, 25.6f, 32f, 51.2f, 64f
        };
        float[] damageValues =
        {
            0.02f, 0.05f, 0.1f, 0.2f, 0.4f, 0.8f,
            1.6f, 3.2f, 4f, 6.4f, 8f
        };
        const int samples = 64;
        List<CombatCandidate> candidates = new();
        foreach (float health in healthValues)
        {
            foreach (float damage in damageValues)
            {
                candidates.Add(ProbeCandidate(
                    target,
                    encounterId,
                    authored,
                    factory,
                    combat,
                    equipment,
                    samples,
                    health,
                    damage,
                    authored.objectiveRoundLimit));
            }
        }

        StringBuilder report = new();
        report.AppendLine("COMBAT_BALANCE_FOCUSED_GRID_V1");
        report.AppendLine(
            $"encounter={encounterId}; objective={authored.objective}; "
            + $"samples={samples}; candidates={candidates.Count}");
        report.AppendLine(
            "rank | health | damage | round | win | severe | low-health | failure casualty | "
            + "median rounds | mean health | score | stalled");
        int rank = 0;
        foreach (CombatCandidate candidate in candidates
            .OrderBy(value => value.Score)
            .ThenBy(value => value.ChangeCost)
            .ThenBy(value => value.Health)
            .ThenBy(value => value.Damage)
            .Take(candidates.Count))
        {
            report.AppendLine(string.Join(" | ",
                (++rank).ToString(CultureInfo.InvariantCulture),
                candidate.Health.ToString("0.###", CultureInfo.InvariantCulture),
                candidate.Damage.ToString("0.###", CultureInfo.InvariantCulture),
                candidate.RoundLimit.ToString(CultureInfo.InvariantCulture),
                candidate.Outcome.WinRate.ToString("P1", CultureInfo.InvariantCulture),
                candidate.Outcome.SevereCasualtyRate.ToString(
                    "P1",
                    CultureInfo.InvariantCulture),
                candidate.Outcome.LowHealthVictoryRate.ToString(
                    "P1",
                    CultureInfo.InvariantCulture),
                candidate.Outcome.FailureCasualtyRate.ToString(
                    "P1",
                    CultureInfo.InvariantCulture),
                Median(candidate.Outcome.Rounds).ToString(
                    "0.0",
                    CultureInfo.InvariantCulture),
                candidate.Outcome.MeanAllyHealthRatio.ToString(
                    "P1",
                    CultureInfo.InvariantCulture),
                candidate.Score.ToString("0.000000", CultureInfo.InvariantCulture),
                candidate.Outcome.Stalled.ToString(CultureInfo.InvariantCulture)));
        }

        Directory.CreateDirectory(
            Path.GetDirectoryName(FocusedReportPath) ?? ".");
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(report.ToString());
        V27BalanceArtifactWriter.WriteIfDifferent(FocusedReportPath, stream =>
            stream.Write(bytes, 0, bytes.Length));
        AssetDatabase.Refresh();
        return $"COMBAT_BALANCE_FOCUSED_GRID=MEASURED; encounter={encounterId}; "
            + $"report={FocusedReportPath}";
    }

    public static string RunFocusedSearchSet(string encounterIds)
    {
        string[] requested = (encounterIds ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(value => value.Trim())
            .Where(value => value.Length > 0)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (requested.Length == 0)
        {
            throw new ArgumentException(
                "Focused search requires at least one encounter ID.",
                nameof(encounterIds));
        }

        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        EnemyCombatContentCatalog combat = new(content);
        ResourceOffenseCampaignCatalog campaigns = new(content);
        ResourceCombatEquipmentCatalog equipment = new(content);
        DungeonRuntimeAggregateRootStore rootStore = new();
        CharacterNarrativeCatalog narrativeCatalog = new(content);
        CharacterNarrativeRuntime narrative = new(rootStore, narrativeCatalog);
        ResourceCharacterSpeciesCatalog species = new(content);
        CharacterLifeRuntime life = new(
            rootStore,
            species,
            new RandomStreamProvider(rootStore));
        EnemyIndividualFactory individuals = new(
            combat,
            narrativeCatalog,
            narrative,
            narrative,
            life,
            life,
            species,
            content);
        EnemyEncounterFactory factory = new(
            combat,
            combat,
            combat,
            combat,
            individuals);
        Dictionary<int, OffenseTargetDefinition> targetByCampaign = campaigns.Targets
            .ToDictionary(value => value.campaignOrder);
        StringBuilder report = new();
        report.AppendLine("COMBAT_BALANCE_FOCUSED_SEARCH_V2");
        report.AppendLine(
            $"requested={requested.Length}; coarseSamples=16; "
            + "roundSamples=32; confirmationSamples=256; "
            + "coarseSeeds=6; finalists=4");
        report.AppendLine(
            "encounter | objective | health | damage | objective-health | round | win | severe | "
            + "low-health | failure casualty | median rounds | mean health | "
            + "score | stalled");

        foreach (string encounterId in requested)
        {
            int encounterNumber = ParseEncounterNumber(encounterId);
            int campaign = (encounterNumber - 1) / 6 + 1;
            OffenseTargetDefinition target = targetByCampaign[campaign];
            OffenseEncounterSO authored = ((IEncounterCatalog)combat).Require(
                encounterId);
            CombatCandidate best = FindFocusedCandidate(
                target,
                encounterId,
                authored,
                factory,
                combat,
                equipment);
            report.AppendLine(string.Join(" | ",
                encounterId,
                authored.objective,
                best.Health.ToString("0.###", CultureInfo.InvariantCulture),
                best.Damage.ToString("0.###", CultureInfo.InvariantCulture),
                best.ObjectiveHealth.ToString("0.###", CultureInfo.InvariantCulture),
                best.RoundLimit.ToString(CultureInfo.InvariantCulture),
                best.Outcome.WinRate.ToString("P1", CultureInfo.InvariantCulture),
                best.Outcome.SevereCasualtyRate.ToString(
                    "P1",
                    CultureInfo.InvariantCulture),
                best.Outcome.LowHealthVictoryRate.ToString(
                    "P1",
                    CultureInfo.InvariantCulture),
                best.Outcome.FailureCasualtyRate.ToString(
                    "P1",
                    CultureInfo.InvariantCulture),
                Median(best.Outcome.Rounds).ToString(
                    "0.0",
                    CultureInfo.InvariantCulture),
                best.Outcome.MeanAllyHealthRatio.ToString(
                    "P1",
                    CultureInfo.InvariantCulture),
                best.Score.ToString("0.000000", CultureInfo.InvariantCulture),
                best.Outcome.Stalled.ToString(CultureInfo.InvariantCulture)));
            WriteFocusedSearchReport(report);
        }

        AssetDatabase.Refresh();
        return $"COMBAT_BALANCE_FOCUSED_SEARCH=MEASURED; "
            + $"encounters={requested.Length}; report={FocusedSearchReportPath}";
    }

    public static string RunExactProbe(
        string encounterId,
        float health,
        float damage,
        float objectiveHealth,
        int roundLimit,
        int samples = 256,
        float? enemyAccuracy = null,
        float? objectiveControlResistance = null,
        int? additionalEnemyCount = null)
    {
        int encounterNumber = ParseEncounterNumber(encounterId);
        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        EnemyCombatContentCatalog combat = new(content);
        ResourceOffenseCampaignCatalog campaigns = new(content);
        ResourceCombatEquipmentCatalog equipment = new(content);
        DungeonRuntimeAggregateRootStore rootStore = new();
        CharacterNarrativeCatalog narrativeCatalog = new(content);
        CharacterNarrativeRuntime narrative = new(rootStore, narrativeCatalog);
        ResourceCharacterSpeciesCatalog species = new(content);
        CharacterLifeRuntime life = new(
            rootStore,
            species,
            new RandomStreamProvider(rootStore));
        EnemyIndividualFactory individuals = new(
            combat,
            narrativeCatalog,
            narrative,
            narrative,
            life,
            life,
            species,
            content);
        EnemyEncounterFactory factory = new(
            combat,
            combat,
            combat,
            combat,
            individuals);
        OffenseTargetDefinition target = campaigns.Targets.Single(value =>
            value.campaignOrder == (encounterNumber - 1) / 6 + 1);
        OffenseEncounterSO authored = ((IEncounterCatalog)combat).Require(encounterId);
        int authoredAdditionalEnemyCount = authored.additionalEnemyCount;
        CombatCandidate candidate;
        try
        {
            if (additionalEnemyCount.HasValue)
            {
                authored.additionalEnemyCount = Mathf.Clamp(
                    additionalEnemyCount.Value,
                    0,
                    4);
            }
            candidate = ProbeCandidate(
                target,
                encounterId,
                authored,
                factory,
                combat,
                equipment,
                Mathf.Max(1, samples),
                health,
                damage,
                roundLimit,
                objectiveHealth,
                enemyAccuracy,
                objectiveControlResistance);
        }
        finally
        {
            authored.additionalEnemyCount = authoredAdditionalEnemyCount;
        }
        string summary = string.Join(" | ",
            encounterId,
            $"h={candidate.Health:0.###}",
            $"d={candidate.Damage:0.###}",
            $"a={candidate.Accuracy:0.###}",
            $"o={candidate.ObjectiveHealth:0.###}",
            $"c={candidate.ControlResistance:0.###}",
            $"n={additionalEnemyCount ?? authoredAdditionalEnemyCount}",
            $"r={candidate.RoundLimit}",
            $"win={candidate.Outcome.WinRate:P1}",
            $"severe={candidate.Outcome.SevereCasualtyRate:P1}",
            $"low={candidate.Outcome.LowHealthVictoryRate:P1}",
            $"failure={candidate.Outcome.FailureCasualtyRate:P1}",
            $"median={Median(candidate.Outcome.Rounds):0.0}",
            $"meanHealth={candidate.Outcome.MeanAllyHealthRatio:P1}",
            $"enemyActions={candidate.Outcome.EnemyBasicCommands}/"
                + $"{candidate.Outcome.EnemyAbilityCommands}/"
                + $"{candidate.Outcome.EnemyGuardCommands}/"
                + $"{candidate.Outcome.EnemyAdvanceCommands}/"
                + $"{candidate.Outcome.EnemyRetreatCommands}",
            $"enemyRetries={candidate.Outcome.EnemyTacticalRetries}",
            $"enemyRejected={candidate.Outcome.EnemyRejectedCommands}",
            $"score={candidate.Score:0.000000}",
            $"stalled={candidate.Outcome.Stalled}");
        IEnumerable<string> diagnostics = candidate.Outcome.TacticalTrace
            .Select(value => "TRACE " + value)
            .Concat(candidate.Outcome.StalledDetails
                .Take(8)
                .Select(value => "STALL " + value));
        string diagnosticText = string.Join(Environment.NewLine, diagnostics);
        return string.IsNullOrEmpty(diagnosticText)
            ? summary
            : summary + Environment.NewLine + diagnosticText;
    }

    public static string RunFinalVerificationCheckpoint(
        string encounterId,
        float health,
        float damage,
        float objectiveHealth,
        int roundLimit,
        float? enemyAccuracy = null,
        float? objectiveControlResistance = null,
        int? additionalEnemyCount = null)
    {
        const int samples = SamplesPerEncounter;
        int encounterNumber = ParseEncounterNumber(encounterId);
        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        EnemyCombatContentCatalog combat = new(content);
        ResourceOffenseCampaignCatalog campaigns = new(content);
        ResourceCombatEquipmentCatalog equipment = new(content);
        DungeonRuntimeAggregateRootStore rootStore = new();
        CharacterNarrativeCatalog narrativeCatalog = new(content);
        CharacterNarrativeRuntime narrative = new(rootStore, narrativeCatalog);
        ResourceCharacterSpeciesCatalog species = new(content);
        CharacterLifeRuntime life = new(
            rootStore,
            species,
            new RandomStreamProvider(rootStore));
        EnemyIndividualFactory individuals = new(
            combat,
            narrativeCatalog,
            narrative,
            narrative,
            life,
            life,
            species,
            content);
        EnemyEncounterFactory factory = new(
            combat,
            combat,
            combat,
            combat,
            individuals);
        OffenseTargetDefinition target = campaigns.Targets.Single(value =>
            value.campaignOrder == (encounterNumber - 1) / 6 + 1);
        OffenseEncounterSO authored = ((IEncounterCatalog)combat).Require(encounterId);
        int authoredAdditionalEnemyCount = authored.additionalEnemyCount;
        CombatCandidate candidate;
        try
        {
            if (additionalEnemyCount.HasValue)
            {
                authored.additionalEnemyCount = Mathf.Clamp(
                    additionalEnemyCount.Value,
                    0,
                    4);
            }
            candidate = ProbeCandidate(
                target,
                encounterId,
                authored,
                factory,
                combat,
                equipment,
                samples,
                health,
                damage,
                roundLimit,
                objectiveHealth,
                enemyAccuracy,
                objectiveControlResistance);
        }
        finally
        {
            authored.additionalEnemyCount = authoredAdditionalEnemyCount;
        }
        List<string> failures = ValidateOutcomeBand(candidate.Outcome).ToList();
        bool passed = candidate.Outcome.Stalled == 0 && failures.Count == 0;
        StringBuilder report = new();
        report.AppendLine(
            $"RESULT={(passed ? "PASS" : "FAIL")}; samples={samples}; "
            + $"failures={failures.Count}; stalled={candidate.Outcome.Stalled}");
        report.AppendLine("COMBAT_BALANCE_FINAL_CHECKPOINT_V2");
        report.AppendLine("enemyDecisionAuthority=EnemyTacticalDecisionService");
        report.AppendLine($"encounter={encounterId}");
        report.AppendLine($"objective={authored.objective}");
        report.AppendLine(
            $"health={candidate.Health.ToString("R", CultureInfo.InvariantCulture)}; "
            + $"damage={candidate.Damage.ToString("R", CultureInfo.InvariantCulture)}; "
            + $"accuracy={candidate.Accuracy.ToString("R", CultureInfo.InvariantCulture)}; "
            + $"objectiveHealth={candidate.ObjectiveHealth.ToString("R", CultureInfo.InvariantCulture)}; "
            + $"controlResistance={candidate.ControlResistance.ToString("R", CultureInfo.InvariantCulture)}; "
            + $"additionalEnemyCount={(additionalEnemyCount ?? authoredAdditionalEnemyCount).ToString(CultureInfo.InvariantCulture)}; "
            + $"round={candidate.RoundLimit.ToString(CultureInfo.InvariantCulture)}");
        report.AppendLine(
            $"win={candidate.Outcome.WinRate.ToString("R", CultureInfo.InvariantCulture)}; "
            + $"targetWin={candidate.Outcome.WinTarget}; "
            + $"severe={candidate.Outcome.SevereCasualtyRate.ToString("R", CultureInfo.InvariantCulture)}; "
            + $"targetSevere={candidate.Outcome.SevereTarget}; "
            + $"lowHealth={candidate.Outcome.LowHealthVictoryRate.ToString("R", CultureInfo.InvariantCulture)}; "
            + $"failureCasualty={candidate.Outcome.FailureCasualtyRate.ToString("R", CultureInfo.InvariantCulture)}");
        report.AppendLine(
            $"medianRounds={Median(candidate.Outcome.Rounds).ToString("R", CultureInfo.InvariantCulture)}; "
            + $"meanHealth={candidate.Outcome.MeanAllyHealthRatio.ToString("R", CultureInfo.InvariantCulture)}; "
            + $"score={candidate.Score.ToString("R", CultureInfo.InvariantCulture)}");
        foreach (string failure in failures)
        {
            report.AppendLine("FAILURE=" + failure);
        }
        Directory.CreateDirectory(FinalCheckpointDirectory);
        string path = Path.Combine(
            FinalCheckpointDirectory,
            $"encounter-{encounterNumber:00}.txt");
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(report.ToString());
        V27BalanceArtifactWriter.WriteIfDifferent(path, stream =>
            stream.Write(bytes, 0, bytes.Length));
        if (!passed)
        {
            throw new InvalidOperationException(
                $"Combat final checkpoint failed for {encounterId}. See {path}.");
        }
        return $"COMBAT_BALANCE_FINAL_CHECKPOINT=PASS; "
            + $"encounter={encounterId}; report={path}";
    }

    private static CombatCandidate FindFocusedCandidate(
        OffenseTargetDefinition target,
        string encounterId,
        OffenseEncounterSO authored,
        EnemyEncounterFactory factory,
        IEnemyArchetypeCatalog archetypes,
        ICombatEquipmentCatalog equipment)
    {
        float[] healthValues =
        {
            0.02f, 0.05f, 0.1f, 0.2f, 0.4f, 0.8f,
            1.6f, 3.2f, 6.4f, 12.8f, 25.6f, 32f, 51.2f, 64f
        };
        float[] damageValues =
        {
            0.02f, 0.05f, 0.1f, 0.2f, 0.4f, 0.8f,
            1.6f, 3.2f, 4f, 6.4f, 8f
        };
        List<CombatCandidate> coarse = new();
        foreach (float health in healthValues)
        {
            foreach (float damage in damageValues)
            {
                coarse.Add(ProbeCandidate(
                    target,
                    encounterId,
                    authored,
                    factory,
                    archetypes,
                    equipment,
                    16,
                    health,
                    damage,
                    authored.objectiveRoundLimit));
            }
        }

        bool objectiveEncounter = authored.objective is
            OffenseEncounterObjective.ProtectTarget
            or OffenseEncounterObjective.SabotageTarget
            or OffenseEncounterObjective.CaptureLeader;
        if (objectiveEncounter)
        {
            float[] objectiveValues =
            {
                0.02f, 0.05f, 0.1f, 0.2f, 0.25f, 0.3f, 0.35f,
                0.4f, 0.45f, 0.5f, 0.6f, 0.8f,
                1.6f, 3.2f, 6.4f, 12.8f, 25.6f, 32f
            };
            float[] objectiveEnemyHealth = { 0.2f, 0.8f, 3.2f };
            float[] objectiveDamage = { 0.05f, 0.1f, 0.2f, 0.4f, 0.8f, 1.6f };
            foreach (float health in objectiveEnemyHealth)
            {
                foreach (float damage in objectiveDamage)
                {
                    foreach (float objectiveHealth in objectiveValues)
                    {
                        coarse.Add(ProbeCandidate(
                            target,
                            encounterId,
                            authored,
                            factory,
                            archetypes,
                            equipment,
                            16,
                            health,
                            damage,
                            authored.objectiveRoundLimit,
                            objectiveHealth));
                    }
                }
            }
        }

        bool timeObjective = authored.objective is
            OffenseEncounterObjective.SurviveRounds
            or OffenseEncounterObjective.Escape;
        CombatCandidate[] seeds = timeObjective
            ? coarse
                .GroupBy(value => value.Damage)
                .Select(group => group
                    .OrderBy(value => value.Score)
                    .ThenBy(value => value.ChangeCost)
                    .ThenBy(value => value.Health)
                    .First())
                .OrderBy(value => value.Damage)
                .ToArray()
            : coarse
                .OrderBy(value => value.Score)
                .ThenBy(value => value.ChangeCost)
                .ThenBy(value => value.Health)
                .ThenBy(value => value.Damage)
                .Take(6)
                .ToArray();
        List<CombatCandidate> expanded = new();
        foreach (CombatCandidate seed in seeds)
        {
            IEnumerable<int> rounds = authored.objective switch
            {
                OffenseEncounterObjective.DefeatAll => new[] { 0 },
                OffenseEncounterObjective.SurviveRounds
                    or OffenseEncounterObjective.Escape => Enumerable.Range(1, 64),
                _ => Enumerable.Range(1, 16)
            };
            foreach (int round in rounds)
            {
                expanded.Add(ProbeCandidate(
                    target,
                    encounterId,
                    authored,
                    factory,
                    archetypes,
                    equipment,
                    timeObjective ? 16 : 32,
                    seed.Health,
                    seed.Damage,
                    round,
                    seed.ObjectiveHealth));
            }
        }

        CombatCandidate[] finalists = expanded
            .OrderBy(value => value.Score)
            .ThenBy(value => value.ChangeCost)
            .ThenBy(value => value.RoundLimit)
            .ThenBy(value => value.Health)
            .ThenBy(value => value.Damage)
            .Take(4)
            .ToArray();
        return finalists
            .Select(value => ProbeCandidate(
                target,
                encounterId,
                authored,
                factory,
                archetypes,
                equipment,
                256,
                value.Health,
                value.Damage,
                value.RoundLimit,
                value.ObjectiveHealth))
            .OrderBy(value => value.Score)
            .ThenBy(value => value.ChangeCost)
            .ThenBy(value => value.RoundLimit)
            .ThenBy(value => value.Health)
            .ThenBy(value => value.Damage)
            .First();
    }

    private static int ParseEncounterNumber(string encounterId)
    {
        if (string.IsNullOrWhiteSpace(encounterId)
            || !encounterId.StartsWith("encounter:", StringComparison.Ordinal)
            || !int.TryParse(
                encounterId.AsSpan("encounter:".Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out int encounterNumber)
            || encounterNumber < 1
            || encounterNumber > 36)
        {
            throw new ArgumentException(
                $"Invalid focused encounter ID '{encounterId}'.");
        }
        return encounterNumber;
    }

    private static void WriteFocusedSearchReport(StringBuilder report)
    {
        Directory.CreateDirectory(
            Path.GetDirectoryName(FocusedSearchReportPath) ?? ".");
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(report.ToString());
        V27BalanceArtifactWriter.WriteIfDifferent(FocusedSearchReportPath, stream =>
            stream.Write(bytes, 0, bytes.Length));
    }

    private static CombatCandidate FindBestCandidate(
        OffenseTargetDefinition target,
        string encounterId,
        OffenseEncounterSO authored,
        EnemyEncounterFactory factory,
        IEnemyArchetypeCatalog archetypes,
        ICombatEquipmentCatalog equipment,
        IReadOnlyList<float> damageSeeds,
        int broadSamples,
        int refineSamples,
        int confirmationSamples)
    {
        const int bisectionSteps = 6;
        bool timeObjective = authored.objective is
            OffenseEncounterObjective.SurviveRounds
            or OffenseEncounterObjective.Escape;
        List<CombatCandidate> broad = new();
        (float winMinimum, float winMaximum, _, _) = ResolveTargetBand(
            new EncounterOutcome
            {
                Elite = authored.elite,
                Boss = authored.boss
            });
        float targetWin = (winMinimum + winMaximum) * 0.5f;

        if (timeObjective)
        {
            foreach (int round in CandidateRounds(authored))
            {
                float lowDamage = 0.1f;
                float highDamage = 4f;
                for (int step = 0; step < bisectionSteps; step++)
                {
                    float damage = Mathf.Sqrt(lowDamage * highDamage);
                    CombatCandidate candidate = ProbeCandidate(
                        target,
                        encounterId,
                        authored,
                        factory,
                        archetypes,
                        equipment,
                        broadSamples,
                        1f,
                        damage,
                        round);
                    broad.Add(candidate);
                    if (candidate.Outcome.WinRate > targetWin)
                    {
                        lowDamage = damage;
                    }
                    else
                    {
                        highDamage = damage;
                    }
                }
            }
        }
        else
        {
            foreach (float damage in damageSeeds)
            {
                float lowHealth = 0.1f;
                float highHealth = 64f;
                for (int step = 0; step < bisectionSteps; step++)
                {
                    float health = Mathf.Sqrt(lowHealth * highHealth);
                    CombatCandidate candidate = ProbeCandidate(
                        target,
                        encounterId,
                        authored,
                        factory,
                        archetypes,
                        equipment,
                        broadSamples,
                        health,
                        damage,
                        authored.objectiveRoundLimit);
                    broad.Add(candidate);
                    if (candidate.Outcome.WinRate > targetWin)
                    {
                        lowHealth = health;
                    }
                    else
                    {
                        highHealth = health;
                    }
                }
            }
        }

        CombatCandidate[] broadSeeds = broad
            .OrderBy(value => value.Score)
            .ThenBy(value => value.ChangeCost)
            .ThenBy(value => value.RoundLimit)
            .ThenBy(value => value.Health)
            .ThenBy(value => value.Damage)
            .Take(8)
            .ToArray();
        List<CombatCandidate> refined = new();
        foreach (CombatCandidate seed in broadSeeds)
        {
            IEnumerable<int> rounds = CandidateRoundNeighborhood(
                authored,
                seed.RoundLimit);
            foreach (int round in rounds)
            {
                refined.Add(ProbeCandidate(
                    target,
                    encounterId,
                    authored,
                    factory,
                    archetypes,
                    equipment,
                    refineSamples,
                    seed.Health,
                    seed.Damage,
                    round));
            }
        }

        CombatCandidate[] finalists = refined
            .OrderBy(value => value.Score)
            .ThenBy(value => value.ChangeCost)
            .ThenBy(value => value.RoundLimit)
            .ThenBy(value => value.Health)
            .ThenBy(value => value.Damage)
            .Take(4)
            .ToArray();
        return finalists
            .Select(value => ProbeCandidate(
                target,
                encounterId,
                authored,
                factory,
                archetypes,
                equipment,
                confirmationSamples,
                value.Health,
                value.Damage,
                value.RoundLimit))
            .OrderBy(value => value.Score)
            .ThenBy(value => value.ChangeCost)
            .ThenBy(value => value.RoundLimit)
            .ThenBy(value => value.Health)
            .ThenBy(value => value.Damage)
            .First();
    }

    private static CombatCandidate ProbeCandidate(
        OffenseTargetDefinition target,
        string encounterId,
        OffenseEncounterSO authored,
        EnemyEncounterFactory factory,
        IEnemyArchetypeCatalog archetypes,
        ICombatEquipmentCatalog equipment,
        int samples,
        float health,
        float damage,
        int roundLimit,
        float objectiveHealth = 1f,
        float? enemyAccuracy = null,
        float? objectiveControlResistance = null)
    {
        float resolvedAccuracy = enemyAccuracy ?? authored.enemyAccuracyMultiplier;
        float resolvedControlResistance = objectiveControlResistance
            ?? authored.objectiveControlResistanceMultiplier;
        EncounterOutcome outcome = SimulateEncounter(
            target,
            encounterId,
            factory,
            archetypes,
            equipment,
            samples,
            1f,
            health,
            damage,
            roundLimit,
            true,
            objectiveHealth,
            resolvedAccuracy,
            resolvedControlResistance);
        return new CombatCandidate(
            health,
            damage,
            resolvedAccuracy,
            objectiveHealth,
            resolvedControlResistance,
            roundLimit,
            outcome,
            ScoreCandidate(
                outcome,
                health,
                damage,
                resolvedAccuracy,
                objectiveHealth,
                resolvedControlResistance,
                roundLimit,
                authored));
    }

    private static IEnumerable<int> CandidateRounds(OffenseEncounterSO encounter)
    {
        if (encounter.objective == OffenseEncounterObjective.DefeatAll)
        {
            yield return 0;
            yield break;
        }

        for (int round = 1; round <= 16; round++)
        {
            yield return round;
        }
    }

    private static IEnumerable<int> CandidateRoundNeighborhood(
        OffenseEncounterSO encounter,
        int center)
    {
        if (encounter.objective == OffenseEncounterObjective.DefeatAll)
        {
            yield return 0;
            yield break;
        }

        int minimum = Mathf.Max(1, center - 2);
        int maximum = Mathf.Min(16, center + 2);
        for (int round = minimum; round <= maximum; round++)
        {
            yield return round;
        }
    }

    private static double ScoreCandidate(
        EncounterOutcome outcome,
        float health,
        float damage,
        float accuracy,
        float objectiveHealth,
        float controlResistance,
        int roundLimit,
        OffenseEncounterSO authored)
    {
        (float winMinimum, float winMaximum, float severeMinimum,
            float severeMaximum) = ResolveTargetBand(outcome);
        double winDistance = DistanceToBand(
            outcome.WinRate,
            winMinimum,
            winMaximum);
        double severeDistance = DistanceToBand(
            outcome.SevereCasualtyRate,
            severeMinimum,
            severeMaximum);
        double change = Math.Abs(Math.Log(Math.Max(0.1f, health)))
            + Math.Abs(Math.Log(Math.Max(0.1f, damage)))
            + Math.Abs(Math.Log(Math.Max(0.1f, accuracy)))
            + Math.Abs(Math.Log(Math.Max(0.02f, objectiveHealth)))
            + Math.Abs(Math.Log(Math.Max(0.1f, controlResistance)))
            + Math.Abs(roundLimit - authored.objectiveRoundLimit) * 0.08d;
        return outcome.Stalled * 1_000_000d
            + winDistance * winDistance * 1_000d
            + severeDistance * severeDistance * 10d
            + change * 0.001d;
    }

    private static double DistanceToBand(float value, float minimum, float maximum)
    {
        if (value < minimum)
        {
            return (minimum - value) / Math.Max(0.0001f, maximum - minimum);
        }
        if (value > maximum)
        {
            return (value - maximum) / Math.Max(0.0001f, maximum - minimum);
        }
        return 0d;
    }

    private static (float winMinimum, float winMaximum, float severeMinimum,
        float severeMaximum) ResolveTargetBand(EncounterOutcome outcome)
    {
        float severeMaximum = outcome.Boss
            ? 0.60f
            : outcome.Elite
                ? 0.50f
                : 0.40f;
        if (outcome.Objective == OffenseEncounterObjective.ProtectTarget)
        {
            // Elite protection encounters have two legitimate tactical
            // regimes: direct objective pressure and a screened rear target.
            // The latter cannot be forced down to the generic elite win band
            // without exceeding the unchanged 50% Dead/Downed victory cap.
            return outcome.Elite
                ? (0.55f, 0.90f, 0f, severeMaximum)
                : (0.65f, 0.80f, 0f, severeMaximum);
        }
        if (outcome.Objective == OffenseEncounterObjective.SurviveRounds)
        {
            return (0.85f, 1f, 0f, severeMaximum);
        }
        if (outcome.Objective == OffenseEncounterObjective.Escape)
        {
            float minimum = outcome.Boss ? 0.55f : outcome.Elite ? 0.65f : 0.80f;
            return (minimum, 1f, 0f, severeMaximum);
        }
        if (outcome.Objective == OffenseEncounterObjective.DefeatAll
            && !outcome.Elite
            && !outcome.Boss)
        {
            return (0.85f, 1f, 0f, severeMaximum);
        }
        return outcome.Boss
            ? (0.25f, 0.45f, 0f, severeMaximum)
            : outcome.Elite
                ? (0.45f, 0.65f, 0f, severeMaximum)
                : (0.65f, 0.80f, 0f, severeMaximum);
    }

    private static EncounterOutcome SimulateEncounter(
        OffenseTargetDefinition target,
        string encounterId,
        EnemyEncounterFactory factory,
        IEnemyArchetypeCatalog archetypes,
        ICombatEquipmentCatalog equipment,
        int samples,
        float partyPowerMultiplier,
        float? enemyHealthMultiplier = null,
        float? enemyDamageMultiplier = null,
        int? objectiveRoundLimit = null,
        bool stratifiedSamples = false,
        float? objectiveHealthMultiplier = null,
        float? enemyAccuracyMultiplier = null,
        float? objectiveControlResistanceMultiplier = null)
    {
        OffenseEncounterSO authored =
            ((IEncounterCatalog)archetypes).Require(encounterId);
        EncounterOutcome result = new()
        {
            EncounterId = encounterId,
            Campaign = target.campaignOrder,
            PartyMembers = Mathf.Clamp(
                CombatBalanceCheckpointAuthority.RequireCampaign(
                    target.campaignOrder).CombatReadyMinimum,
                1,
                5),
            RequiredPower = target.requiredPower,
            Elite = authored.elite,
            Boss = authored.boss
        };
        result.Samples = Mathf.Max(1, samples);
        IEnemyTacticalDecisionService enemyTactics =
            new EnemyTacticalDecisionService(archetypes);

        for (int sample = 0; sample < result.Samples; sample++)
        {
            int sampleId = stratifiedSamples && result.Samples < SamplesPerEncounter
                ? Mathf.Clamp(
                    Mathf.FloorToInt(
                        (sample + 0.5f) * SamplesPerEncounter / result.Samples),
                    0,
                    SamplesPerEncounter - 1)
                : sample;
            EnemyEncounterComposition composition = CreateSpecificEncounter(
                factory,
                target,
                encounterId,
                sampleId,
                ((IEncounterCatalog)archetypes).Require(encounterId));
            float authoredHealthMultiplier = Mathf.Clamp(
                authored.enemyHealthMultiplier,
                0.1f,
                64f);
            if (enemyHealthMultiplier.HasValue)
            {
                float healthRatio = Mathf.Clamp(
                    enemyHealthMultiplier.Value,
                    0.02f,
                    64f) / authoredHealthMultiplier;
                foreach (OffenseBattleCombatant enemy in composition.Combatants
                    .Where(value => value.Team == OffenseBattleTeam.Enemies
                        && !string.Equals(
                            value.PersistentId,
                            composition.Rules.ObjectiveCombatantId,
                            StringComparison.Ordinal)))
                {
                    OffenseEncounterBalanceRules.ScaleEnemyHealth(
                        enemy,
                        healthRatio);
                }
            }
            if (objectiveHealthMultiplier.HasValue
                && (composition.Encounter.objective is
                    OffenseEncounterObjective.ProtectTarget
                    or OffenseEncounterObjective.SabotageTarget
                    or OffenseEncounterObjective.CaptureLeader))
            {
                OffenseBattleCombatant objective = composition.Combatants
                    .Single(value => string.Equals(
                        value.PersistentId,
                        composition.Rules.ObjectiveCombatantId,
                        StringComparison.Ordinal));
                float objectiveRatio = Mathf.Clamp(
                    objectiveHealthMultiplier.Value,
                    0.02f,
                    32f) / Mathf.Max(
                    0.02f,
                    authored.objectiveHealthMultiplier);
                OffenseEncounterBalanceRules.ScaleEnemyHealth(
                    objective,
                    objectiveRatio);
            }
            OffenseBattleEncounterRules activeRules = composition.Rules;
            if (enemyDamageMultiplier.HasValue
                || objectiveRoundLimit.HasValue
                || enemyAccuracyMultiplier.HasValue
                || objectiveControlResistanceMultiplier.HasValue)
            {
                activeRules = new OffenseBattleEncounterRules(
                    authored.objective,
                    objectiveRoundLimit ?? authored.objectiveRoundLimit,
                    authored.objectiveTargetId,
                    composition.Rules.ObjectiveCombatantId,
                    composition.Rules.Modifiers,
                    authored.counterTags,
                    authored.rewardItemIds,
                    enemyDamageMultiplier ?? authored.enemyDamageMultiplier,
                    enemyAccuracyMultiplier ?? authored.enemyAccuracyMultiplier,
                    objectiveControlResistanceMultiplier
                        ?? authored.objectiveControlResistanceMultiplier);
            }
            result.Objective = composition.Encounter.objective;
            ICombatEquipmentRuntime battleEquipment =
                OffenseEditorTestDependencies.CreateCombatEquipmentRuntime();
            EquipEnemies(
                composition,
                archetypes,
                equipment,
                battleEquipment);
            OffenseBattleCombatant[] allies = CreateParty(
                target,
                sampleId,
                equipment,
                battleEquipment,
                composition.Encounter.objective,
                partyPowerMultiplier);
            List<OffenseBattleCombatant> combatants = allies
                .Concat(composition.Combatants)
                .ToList();
            OffenseBattleSession session = new(
                $"balance:{encounterId}:{sampleId}",
                $"balance-expedition:{encounterId}:{sampleId}",
                target.id,
                target.title,
                DungeonDifficulty.Normal,
                combatants,
                CreateCombatResolution(encounterId, sampleId),
                battleEquipment,
                activeRules);

            long commandId = 1;
            int commands = 0;
            float capturePeakSedation = 0f;
            while (!session.IsComplete && commands++ < MaximumCommandsPerBattle)
            {
                OffenseBattleCombatant actor = session.CurrentActor;
                if (actor == null)
                {
                    break;
                }

                EnemyIndividualSaveData enemyIndividual = null;
                EnemyTacticalDecision enemyDecision = default;
                OffenseBattleCommand command;
                if (actor.Team == OffenseBattleTeam.Enemies)
                {
                    enemyIndividual = composition.Individuals.SingleOrDefault(value =>
                        string.Equals(
                            value.characterId,
                            actor.PersistentId,
                            StringComparison.Ordinal));
                    if (enemyIndividual == null)
                    {
                        throw new InvalidOperationException(
                            $"Enemy combatant '{actor.PersistentId}' has no persistent individual profile.");
                    }

                    enemyDecision = enemyTactics.Decide(session, enemyIndividual);
                    command = CreateEnemyTacticalCommand(
                        session,
                        enemyDecision,
                        commandId);
                    CaptureTacticalTrace(
                        result,
                        sample,
                        session,
                        actor,
                        enemyIndividual,
                        enemyDecision,
                        command,
                        "primary");
                }
                else
                {
                    command = CreateAllyCommand(session, actor, commandId);
                }
                command ??= new OffenseBattleCommand(
                    commandId,
                    actor.PersistentId,
                    OffenseBattleActionType.Guard,
                    actor.PersistentId);
                bool captureShot = IsCaptureShot(session, actor, command);
                float sedationBefore = GetObjectiveSedation(session);
                if (captureShot)
                {
                    result.CaptureShotAttempts++;
                }

                bool executed = session.TryExecuteCommand(command, out _);
                if (!executed && enemyIndividual != null)
                {
                    result.EnemyRejectedCommands++;
                    result.EnemyTacticalRetries++;
                    enemyDecision = enemyTactics.Decide(
                        session,
                        enemyIndividual,
                        allowAbility: false);
                    command = CreateEnemyTacticalCommand(
                        session,
                        enemyDecision,
                        commandId);
                    CaptureTacticalTrace(
                        result,
                        sample,
                        session,
                        actor,
                        enemyIndividual,
                        enemyDecision,
                        command,
                        "retry");
                    executed = session.TryExecuteCommand(command, out _);
                }
                if (!executed)
                {
                    if (enemyIndividual != null)
                    {
                        result.EnemyRejectedCommands++;
                    }
                    OffenseBattleCommand guard = new(
                        commandId,
                        actor.PersistentId,
                        OffenseBattleActionType.Guard,
                        actor.PersistentId);
                    if (!session.TryExecuteCommand(guard, out _))
                    {
                        break;
                    }
                    command = guard;
                }
                else if (captureShot)
                {
                    result.CaptureShotCommands++;
                    float sedationAfter = GetObjectiveSedation(session);
                    capturePeakSedation = Mathf.Max(
                        capturePeakSedation,
                        sedationAfter);
                    if (sedationAfter > sedationBefore + 0.001f)
                    {
                        result.CaptureShotHits++;
                    }
                }
                if (actor.Team == OffenseBattleTeam.Enemies)
                {
                    switch (command.ActionType)
                    {
                        case OffenseBattleActionType.BasicAttack:
                            result.EnemyBasicCommands++;
                            break;
                        case OffenseBattleActionType.Ability:
                            result.EnemyAbilityCommands++;
                            break;
                        case OffenseBattleActionType.Guard:
                            result.EnemyGuardCommands++;
                            break;
                        case OffenseBattleActionType.Advance:
                            result.EnemyAdvanceCommands++;
                            break;
                        case OffenseBattleActionType.Retreat:
                            result.EnemyRetreatCommands++;
                            break;
                    }
                }
                commandId++;
            }

            if (!session.IsComplete)
            {
                result.Stalled++;
                result.StalledDetails.Add(BuildStalledDetail(
                    sampleId,
                    commands,
                    session));
            }
            bool victory = session.Outcome == OffenseBattleOutcome.Victory;
            if (victory)
            {
                result.Wins++;
            }
            if (composition.Encounter.objective ==
                OffenseEncounterObjective.CaptureLeader)
            {
                OffenseBattleCombatant leader = session.FindCombatant(
                    session.EncounterRules.ObjectiveCombatantId);
                if (leader?.IsDead == true)
                {
                    result.CaptureLeaderDeaths++;
                }
                else if (leader?.IsDowned == true)
                {
                    result.CaptureLeaderDowned++;
                }
                else
                {
                    result.CaptureLeaderActive++;
                }
                result.CaptureSedationSum += leader?.Statuses
                    .Where(value => value.Type ==
                        OffenseBattleStatusType.Sedated)
                    .Select(value => value.Value)
                    .DefaultIfEmpty(0f)
                    .Max() ?? 0f;
                result.CapturePeakSedationSum += capturePeakSedation;
            }
            result.Rounds.Add(session.RoundNumber);
            float meanHealth = allies.Average(value => value.HealthRatio);
            result.AllyHealthRatioSum += meanHealth;
            int severeCharacters = allies.Count(
                value => value.IsDead || value.IsDowned);
            int lowHealthCharacters = allies.Count(
                value => value.HealthRatio < 0.25f);
            if (victory)
            {
                result.SevereVictoryCharacters += severeCharacters;
                result.LowHealthVictoryCharacters += lowHealthCharacters;
                result.VictoryCharacterSlots += allies.Length;
            }
            else
            {
                result.FailureCasualtyCharacters += severeCharacters;
                result.FailureCharacterSlots += allies.Length;
            }
        }

        return result;
    }

    private static void CaptureTacticalTrace(
        EncounterOutcome outcome,
        int sample,
        OffenseBattleSession session,
        OffenseBattleCombatant actor,
        EnemyIndividualSaveData individual,
        EnemyTacticalDecision decision,
        OffenseBattleCommand command,
        string phase)
    {
        if (outcome == null
            || outcome.Samples > 16
            || sample != 0
            || outcome.TacticalTrace.Count >= 80)
        {
            return;
        }
        OffenseBattleCombatant target = session.FindCombatant(decision.TargetId);
        CombatAttackPreview preview = target == null
            ? default
            : session.PreviewBasicAttack(actor, target);
        outcome.TacticalTrace.Add(
            $"round={session.RoundNumber}; turn={actor.TurnsStarted}; phase={phase}; "
            + $"actor={individual.enemyArchetypeId}; formation={actor.Formation}; "
            + $"weapon={actor.Weapon?.DefinitionId ?? "none"}; "
            + $"range={actor.Weapon?.MaximumRange ?? 0}; "
            + $"intent={decision.Intent}; action={command.ActionType}; "
            + $"ability={decision.AbilityId}; target={decision.TargetId}; "
            + $"targetFormation={target?.Formation.ToString() ?? "none"}; "
            + $"basicValid={preview.Valid}; basicFailure={preview.FailureReason}");
    }

    private static OffenseBattleCommand CreateEnemyTacticalCommand(
        OffenseBattleSession session,
        EnemyTacticalDecision decision,
        long commandId)
    {
        if (session == null) throw new ArgumentNullException(nameof(session));
        OffenseBattleCombatant actor = session.CurrentActor
            ?? throw new InvalidOperationException("The battle has no current actor.");
        OffenseBattleActionType action = decision.Intent switch
        {
            EnemyTacticalIntentKind.UseAbility => OffenseBattleActionType.Ability,
            EnemyTacticalIntentKind.Retreat => OffenseBattleActionType.Retreat,
            EnemyTacticalIntentKind.Protect => OffenseBattleActionType.Guard,
            EnemyTacticalIntentKind.Move => OffenseBattleActionType.Advance,
            _ => OffenseBattleActionType.BasicAttack
        };
        return new OffenseBattleCommand(
            commandId,
            actor.PersistentId,
            action,
            decision.TargetId,
            decision.AbilityId);
    }

    private static IEnumerable<string> ValidateOutcomeBand(
        EncounterOutcome outcome)
    {
        (float winMinimum, float winMaximum, _,
            float severeMaximum) = ResolveTargetBand(outcome);
        if (outcome.WinRate < winMinimum || outcome.WinRate > winMaximum)
        {
            yield return $"{outcome.EncounterId} {outcome.RiskLabel} win "
                + $"{outcome.WinRate:P1} outside {winMinimum:P0}-{winMaximum:P0}.";
        }
        if (outcome.SevereCasualtyRate > severeMaximum)
        {
            yield return $"{outcome.EncounterId} {outcome.RiskLabel} severe "
                + $"{outcome.SevereCasualtyRate:P1} above {severeMaximum:P0}.";
        }
    }

    private static string BuildStalledDetail(
        int sample,
        int commands,
        OffenseBattleSession session)
    {
        string combatants = string.Join(", ", session.Combatants
            .OrderBy(value => value.Team)
            .ThenBy(value => value.PersistentId, StringComparer.Ordinal)
            .Select(value =>
                $"{value.Team}:{value.PersistentId}:hp={value.CurrentHealth:0.0}/{value.Stats.MaxHealth:0.0}:"
                + $"down={value.IsDowned}:formation={value.Formation}:"
                + $"weapon={value.Weapon?.DefinitionId ?? "none"}:"
                + $"ammo={value.Weapon?.LoadedAmmo ?? 0}:"
                + $"range={value.Weapon?.MaximumRange ?? 0}"));
        return $"sample={sample}; commands={commands}; round={session.RoundNumber}; "
            + $"current={session.CurrentActor?.PersistentId ?? "none"}; {combatants}";
    }

    private static bool IsCaptureShot(
        OffenseBattleSession session,
        OffenseBattleCombatant actor,
        OffenseBattleCommand command) =>
        session?.EncounterRules.Objective == OffenseEncounterObjective.CaptureLeader
        && command != null
        && command.ActionType == OffenseBattleActionType.BasicAttack
        && string.Equals(
            command.TargetId,
            session.EncounterRules.ObjectiveCombatantId,
            StringComparison.Ordinal)
        && string.Equals(
            actor?.Weapon?.AmmunitionItemId,
            "ammo:tranquilizer-dart",
            StringComparison.Ordinal);

    private static float GetObjectiveSedation(OffenseBattleSession session)
    {
        OffenseBattleCombatant leader = session?.FindCombatant(
            session.EncounterRules.ObjectiveCombatantId);
        return leader?.Statuses
            .Where(value => value.Type == OffenseBattleStatusType.Sedated)
            .Select(value => value.Value)
            .DefaultIfEmpty(0f)
            .Max() ?? 0f;
    }

    private static EnemyEncounterComposition CreateSpecificEncounter(
        EnemyEncounterFactory factory,
        OffenseTargetDefinition target,
        string encounterId,
        int sample,
        OffenseEncounterSO encounter)
    {
        int depth = encounter.boss ? 4 : encounter.elite ? 3 : 1;
        OffenseRouteNode routeNode = new(
            $"balance-route:{encounterId}",
            depth,
            0,
            encounter.boss
                ? OffenseRouteNodeKind.Boss
                : OffenseRouteNodeKind.Battle,
            encounter.displayName,
            "밸런스 계측 노드",
            encounter.boss
                ? 1f
                : encounter.elite
                    ? 0.95f + target.campaignOrder * 0.03f
                    : 0.75f,
            Array.Empty<string>());
        for (int attempt = 0; attempt < 128; attempt++)
        {
            string context = $"balance:{encounterId}:{sample}:{attempt}";
            EnemyEncounterComposition composition = factory.Create(
                target,
                DungeonDifficulty.Normal,
                context,
                routeNode);
            if (string.Equals(
                    composition.Encounter.encounterId,
                    encounterId,
                    StringComparison.Ordinal))
            {
                return composition;
            }
        }

        throw new InvalidOperationException(
            $"Could not select authored encounter '{encounterId}'.");
    }

    private static OffenseBattleCommand CreateAllyCommand(
        OffenseBattleSession session,
        OffenseBattleCombatant actor,
        long commandId)
    {
        OffenseBattleCommand weaponRecovery =
            session.CreateWeaponRecoveryCommand(actor, commandId);
        if (weaponRecovery != null)
        {
            return weaponRecovery;
        }

        if (session.EncounterRules.Objective ==
            OffenseEncounterObjective.SurviveRounds)
        {
            return new OffenseBattleCommand(
                commandId,
                actor.PersistentId,
                OffenseBattleActionType.Guard,
                actor.PersistentId);
        }

        if (session.EncounterRules.Objective == OffenseEncounterObjective.Escape)
        {
            if (session.RoundNumber < session.EncounterRules.RoundLimit)
            {
                return new OffenseBattleCommand(
                    commandId,
                    actor.PersistentId,
                    OffenseBattleActionType.Guard,
                    actor.PersistentId);
            }
            return new OffenseBattleCommand(
                commandId,
                actor.PersistentId,
                OffenseBattleActionType.Retreat,
                actor.PersistentId);
        }

        if (session.EncounterRules.Objective ==
                OffenseEncounterObjective.ProtectTarget
            && actor.HealthRatio < 0.5f)
        {
            return new OffenseBattleCommand(
                commandId,
                actor.PersistentId,
                OffenseBattleActionType.Guard,
                actor.PersistentId);
        }

        string objectiveId = session.EncounterRules.ObjectiveCombatantId;
        bool nonlethalCapture = session.EncounterRules.Objective ==
                OffenseEncounterObjective.CaptureLeader
            && string.Equals(
                actor.Weapon?.AmmunitionItemId,
                "ammo:tranquilizer-dart",
                StringComparison.Ordinal);
        if (session.EncounterRules.Objective ==
                OffenseEncounterObjective.SabotageTarget
            || nonlethalCapture)
        {
            OffenseBattleCombatant objective =
                session.FindCombatant(objectiveId);
            if (objective != null
                && !objective.IsDead
                && !objective.IsDowned
                && session.PreviewBasicAttack(actor, objective).Valid)
            {
                return new OffenseBattleCommand(
                    commandId,
                    actor.PersistentId,
                    OffenseBattleActionType.BasicAttack,
                    objective.PersistentId);
            }
        }

        IEnumerable<OffenseBattleCombatant> candidates = session.Combatants
            .Where(value => value.Team == OffenseBattleTeam.Enemies
                && !value.IsDead
                && !value.IsDowned);
        if (session.EncounterRules.Objective ==
            OffenseEncounterObjective.SabotageTarget)
        {
            candidates = candidates
                .OrderBy(value => string.Equals(
                    value.PersistentId,
                    objectiveId,
                    StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(value => value.HealthRatio)
                .ThenBy(value => value.PersistentId, StringComparer.Ordinal);
        }
        else if (session.EncounterRules.Objective ==
                 OffenseEncounterObjective.CaptureLeader)
        {
            candidates = candidates
                .Where(value => nonlethalCapture
                    || !string.Equals(
                        value.PersistentId,
                        objectiveId,
                        StringComparison.Ordinal))
                .OrderBy(value => nonlethalCapture
                    && string.Equals(
                        value.PersistentId,
                        objectiveId,
                        StringComparison.Ordinal) ? 0 : 1)
                .ThenBy(value => value.HealthRatio)
                .ThenBy(value => value.PersistentId, StringComparer.Ordinal);
        }
        else
        {
            candidates = candidates
                .OrderBy(value => value.HealthRatio)
                .ThenByDescending(value => value.Stats.Attack)
                .ThenBy(value => value.PersistentId, StringComparer.Ordinal);
        }

        OffenseBattleCombatant target = candidates
            .Where(value => session.PreviewBasicAttack(actor, value).Valid)
            .OrderByDescending(value =>
                session.PreviewBasicAttack(actor, value).ExpectedDamage)
            .ThenBy(value => value.HealthRatio)
            .FirstOrDefault();
        if (target != null)
        {
            return new OffenseBattleCommand(
                commandId,
                actor.PersistentId,
                OffenseBattleActionType.BasicAttack,
                target.PersistentId);
        }

        if (actor.Formation != OffenseFormationSlot.Front)
        {
            return new OffenseBattleCommand(
                commandId,
                actor.PersistentId,
                OffenseBattleActionType.Advance,
                actor.PersistentId);
        }

        return new OffenseBattleCommand(
            commandId,
            actor.PersistentId,
            OffenseBattleActionType.Guard,
            actor.PersistentId);
    }

    private static OffenseBattleCombatant[] CreateParty(
        OffenseTargetDefinition target,
        int sample,
        ICombatEquipmentCatalog equipment,
        ICombatEquipmentRuntime equipmentRuntime,
        OffenseEncounterObjective objective,
        float partyPowerMultiplier)
    {
        CombatBalanceCheckpoint checkpoint =
            CombatBalanceCheckpointAuthority.RequireCampaign(
                target.campaignOrder);
        if (!string.Equals(
                checkpoint.TargetId,
                target.id,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Combat checkpoint target drift for campaign "
                + $"{target.campaignOrder}: checkpoint={checkpoint.TargetId}; "
                + $"catalog={target.id}.");
        }

        int count = Mathf.Clamp(checkpoint.CombatReadyMinimum, 1, 5);
        if (count < target.requiredMembers)
        {
            throw new InvalidOperationException(
                $"Combat checkpoint day {checkpoint.Day} supplies {count} party members "
                + $"for target '{target.id}' requiring {target.requiredMembers}.");
        }
        float projectedBasePower =
            CombatBalanceCheckpointAuthority.CalculateProjectedBasePower(
                checkpoint.Day)
            * Mathf.Max(0.1f, partyPowerMultiplier);
        float stat = Mathf.Max(1f, projectedBasePower / 3.45f);
        OffenseBattleCombatant[] party = new OffenseBattleCombatant[count];
        for (int index = 0; index < count; index++)
        {
            float personal = 0.96f + ((sample + index * 7) % 9) * 0.01f;
            float currentStat = stat * personal;
            float maxHealth = 70f + currentStat * 7f;
            OffenseBattleCombatant ally = new(
                $"ally:{target.campaignOrder}:{sample}:{index}",
                $"기준 원정대원 {index + 1}",
                index == 0 ? "Orc" : index == 1 ? "Slime" : "Vampire",
                OffenseBattleTeam.Allies,
                new OffenseBattleStats(
                    maxHealth,
                    currentStat,
                    currentStat,
                    currentStat,
                    currentStat,
                    currentStat,
                    currentStat,
                    currentStat * 0.8f),
                maxHealth,
                Array.Empty<CharacterCombatAbilityDefinition>(),
                formation: (OffenseFormationSlot)Mathf.Clamp(index, 0, 2));
            bool capturesLeader = objective ==
                OffenseEncounterObjective.CaptureLeader
                && index < Mathf.Min(2, count);
            bool sabotagesTarget = objective ==
                OffenseEncounterObjective.SabotageTarget
                && index == 0;
            string weaponId = checkpoint.WeaponId;
            if (capturesLeader || sabotagesTarget)
            {
                weaponId = "weapon:crossbow";
            }
            else if (index == count - 1
                && !string.IsNullOrWhiteSpace(checkpoint.RangedWeaponId))
            {
                weaponId = checkpoint.RangedWeaponId;
            }
            Equip(
                ally,
                equipment,
                equipmentRuntime,
                weaponId,
                checkpoint.ArmorId,
                capturesLeader
                    || sabotagesTarget
                    || string.Equals(
                        weaponId,
                        checkpoint.RangedWeaponId,
                        StringComparison.Ordinal)
                    ? string.Empty
                    : checkpoint.ShieldId,
                $"ally:{target.campaignOrder}:{sample}:{index}",
                capturesLeader ? "ammo:tranquilizer-dart" : string.Empty,
                checkpoint.Quality);
            party[index] = ally;
        }
        return party;
    }


    private static void EquipEnemies(
        EnemyEncounterComposition composition,
        IEnemyArchetypeCatalog archetypes,
        ICombatEquipmentCatalog equipment,
        ICombatEquipmentRuntime equipmentRuntime)
    {
        foreach (EnemyIndividualSaveData individual in composition.Individuals)
        {
            OffenseBattleCombatant combatant = composition.Combatants
                .First(value => string.Equals(
                    value.PersistentId,
                    individual.characterId,
                    StringComparison.Ordinal));
            EnemyArchetypeDefinitionSO archetype =
                archetypes.Require(individual.enemyArchetypeId);
            Equip(
                combatant,
                equipment,
                equipmentRuntime,
                archetype.equipment.weaponDefinitionId,
                archetype.equipment.armorDefinitionId,
                archetype.equipment.shieldDefinitionId,
                individual.characterId,
                archetype.equipment.ammunitionItemId);
        }
    }

    private static void Equip(
        OffenseBattleCombatant combatant,
        ICombatEquipmentCatalog catalog,
        ICombatEquipmentRuntime equipmentRuntime,
        string weaponId,
        string armorId,
        string shieldId,
        string instancePrefix,
        string ammunitionOverride = "",
        CombatEquipmentQuality quality = CombatEquipmentQuality.Normal)
    {
        CombatWeaponSnapshot weapon = CombatWeaponSnapshot.CreateUnarmed();
        if (catalog.TryGet(weaponId, out CombatEquipmentDefinitionSO weaponDefinition)
            && weaponDefinition is CombatWeaponSO weaponAsset)
        {
            CombatEquipmentInstance instance = equipmentRuntime == null
                ? new CombatEquipmentInstance
                {
                    instanceId = instancePrefix + ":weapon",
                    definitionId = weaponId,
                    quality = quality,
                    durabilityRatio = 1f,
                    loadedAmmunition = new LoadedAmmunitionBatch
                    {
                        ammunitionItemId = string.IsNullOrWhiteSpace(ammunitionOverride)
                            ? weaponAsset.AmmunitionItemId
                            : ammunitionOverride,
                        remaining = Mathf.Max(0, weaponAsset.MagazineCapacity)
                    }
                }
                : equipmentRuntime.CreateExternalInstance(
                    weaponId,
                    quality);
            if (equipmentRuntime != null)
            {
                string ammunitionId = string.IsNullOrWhiteSpace(ammunitionOverride)
                    ? weaponAsset.AmmunitionItemId
                    : ammunitionOverride;
                bool assigned = equipmentRuntime.TryAssignToCharacter(
                        combatant.PersistentId,
                        instance.instanceId,
                        out string assignFailure);
                string activeFailure = string.Empty;
                bool activated = assigned
                    && equipmentRuntime.TrySetActiveWeapon(
                        combatant.PersistentId,
                        instance.instanceId,
                        out activeFailure);
                if (!assigned || !activated)
                {
                    throw new InvalidOperationException(
                        $"Probe weapon assignment failed for '{combatant.PersistentId}': "
                        + $"assign={assignFailure}; active={activeFailure}");
                }
                if (weaponAsset.MagazineCapacity > 0
                    && !equipmentRuntime.TryLoadExternalAmmunition(
                        instance.instanceId,
                        ammunitionId,
                        weaponAsset.MagazineCapacity))
                {
                    throw new InvalidOperationException(
                        $"Probe ammunition load failed for '{combatant.PersistentId}' "
                        + $"with '{ammunitionId}'.");
                }
                equipmentRuntime.TryGetActiveWeapon(
                    combatant.PersistentId,
                    out weapon);
            }
            else
            {
                weapon = weaponAsset.CreateSnapshot(instance);
            }
        }

        List<CombatArmorSnapshot> armor = new();
        if (catalog.TryGet(armorId, out CombatEquipmentDefinitionSO armorDefinition)
            && armorDefinition is CombatArmorSO armorAsset)
        {
            armor.AddRange(armorAsset.BodyPartDefense.Select(part =>
                new CombatArmorSnapshot(
                    instancePrefix + ":armor",
                    part.bodyPart,
                    armorAsset.Layer,
                    quality,
                    1f,
                    part.slashDefense,
                    part.pierceDefense,
                    part.bluntDefense,
                    definitionId: armorId,
                    roleFlags: CombatEquipmentRoleRules.For(armorId))));
        }

        CombatShieldSnapshot shield = default;
        if (catalog.TryGet(shieldId, out CombatEquipmentDefinitionSO shieldDefinition)
            && shieldDefinition is CombatShieldSO shieldAsset)
        {
            shield = new CombatShieldSnapshot(
                instancePrefix + ":shield",
                quality,
                1f,
                shieldAsset.FrontalBlockChance,
                0f,
                shieldAsset.SlashDefense,
                shieldAsset.PierceDefense,
                shieldAsset.BluntDefense,
                definitionId: shieldId,
                roleFlags: CombatEquipmentRoleRules.For(shieldId));
        }

        combatant.SetCombatEquipment(weapon, armor, shield);
    }

    private static float Median(IEnumerable<int> values)
    {
        int[] sorted = values.OrderBy(value => value).ToArray();
        if (sorted.Length == 0) return 0f;
        int middle = sorted.Length / 2;
        return sorted.Length % 2 == 0
            ? (sorted[middle - 1] + sorted[middle]) * 0.5f
            : sorted[middle];
    }

    private static ICombatResolutionService CreateCombatResolution(
        string encounterId,
        int sample) =>
        new CombatResolutionService(
            new ProbeCombatRandom($"{encounterId}:{sample}"),
            evolution: null,
            overclock: null,
            environmentStatus: null,
            environmentalField: NoEnvironmentalFieldQuery.Instance,
            characters: null,
            environmentExposure:
                NoOpCharacterEnvironmentExposureCommand.Instance);

    private sealed class EncounterOutcome
    {
        public string EncounterId = string.Empty;
        public int Campaign;
        public OffenseEncounterObjective Objective;
        public int PartyMembers;
        public float RequiredPower;
        public int Wins;
        public int Stalled;
        public int SevereVictoryCharacters;
        public int LowHealthVictoryCharacters;
        public int VictoryCharacterSlots;
        public int FailureCasualtyCharacters;
        public int FailureCharacterSlots;
        public int CaptureLeaderDowned;
        public int CaptureLeaderDeaths;
        public int CaptureLeaderActive;
        public int CaptureShotAttempts;
        public int CaptureShotCommands;
        public int CaptureShotHits;
        public int EnemyBasicCommands;
        public int EnemyAbilityCommands;
        public int EnemyGuardCommands;
        public int EnemyAdvanceCommands;
        public int EnemyRetreatCommands;
        public int EnemyTacticalRetries;
        public int EnemyRejectedCommands;
        public float CaptureSedationSum;
        public float CapturePeakSedationSum;
        public float AllyHealthRatioSum;
        public int Samples = SamplesPerEncounter;
        public bool Elite;
        public bool Boss;
        public readonly List<int> Rounds = new();
        public readonly List<string> StalledDetails = new();
        public readonly List<string> TacticalTrace = new();
        public float WinRate => Wins / (float)Mathf.Max(1, Samples);
        public float MeanAllyHealthRatio =>
            AllyHealthRatioSum / Mathf.Max(1, Samples);
        public float SevereCasualtyRate =>
            SevereVictoryCharacters / (float)Mathf.Max(1, VictoryCharacterSlots);
        public float LowHealthVictoryRate =>
            LowHealthVictoryCharacters / (float)Mathf.Max(1, VictoryCharacterSlots);
        public float FailureCasualtyRate =>
            FailureCasualtyCharacters
                / (float)Mathf.Max(1, FailureCharacterSlots);
        public float MeanCaptureSedation =>
            CaptureSedationSum / Mathf.Max(1, Samples);
        public float MeanCapturePeakSedation =>
            CapturePeakSedationSum / Mathf.Max(1, Samples);
        public string RiskLabel => Boss ? "boss-first"
            : Elite ? "danger" : "standard";
        public string WinTarget
        {
            get
            {
                (float minimum, float maximum, _, _) = ResolveTargetBand(this);
                return $"{minimum:P0}-{maximum:P0}";
            }
        }
        public string SevereTarget
        {
            get
            {
                (_, _, _, float maximum) = ResolveTargetBand(this);
                return $"<={maximum:P0}";
            }
        }
    }

    private sealed class CombatCandidate
    {
        public CombatCandidate(
            float health,
            float damage,
            float accuracy,
            float objectiveHealth,
            float controlResistance,
            int roundLimit,
            EncounterOutcome outcome,
            double score)
        {
            Health = health;
            Damage = damage;
            Accuracy = accuracy;
            ObjectiveHealth = objectiveHealth;
            ControlResistance = controlResistance;
            RoundLimit = roundLimit;
            Outcome = outcome ?? throw new ArgumentNullException(nameof(outcome));
            Score = score;
            ChangeCost = Math.Abs(Math.Log(Math.Max(0.1f, health)))
                + Math.Abs(Math.Log(Math.Max(0.1f, damage)))
                + Math.Abs(Math.Log(Math.Max(0.1f, accuracy)))
                + Math.Abs(Math.Log(Math.Max(0.02f, objectiveHealth)))
                + Math.Abs(Math.Log(Math.Max(0.1f, controlResistance)))
                + Math.Abs(roundLimit) * 0.000001d;
        }

        public float Health { get; }
        public float Damage { get; }
        public float Accuracy { get; }
        public float ObjectiveHealth { get; }
        public float ControlResistance { get; }
        public int RoundLimit { get; }
        public EncounterOutcome Outcome { get; }
        public double Score { get; }
        public double ChangeCost { get; }
    }

    private sealed class ProbeCombatRandom : ICombatRandomSource
    {
        private uint state;

        public ProbeCombatRandom(string seed)
        {
            state = PersistentEntityId.GetStableHash32(seed ?? string.Empty);
            if (state == 0u)
            {
                state = 0x9E3779B9u;
            }
        }

        public float Next01()
        {
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state & 0x00FFFFFFu) / 16777216f;
        }
    }

}
#endif
