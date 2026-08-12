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
    private const int SamplesPerEncounter = 32;
    private const int MaximumCommandsPerBattle = 600;
    private const string ReportPath =
        "Artifacts/QA/combat-outcome-balance.txt";

    [MenuItem("DungeonStory/Balance/Run Combat Outcome Calibration")]
    public static void RunFromMenu() => Debug.Log(Run());

    [MenuItem("DungeonStory/Balance/Run Combat Power Sweep")]
    public static void RunPowerSweepFromMenu() =>
        Debug.Log(RunPowerSweep());

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

        StringBuilder report = new();
        report.AppendLine("COMBAT_OUTCOME_BALANCE_V1");
        report.AppendLine(
            $"samplesPerEncounter={SamplesPerEncounter}; encounters={outcomes.Count}; "
            + $"stallDetails={outcomes.Sum(value => value.StalledDetails.Count)}");
        report.AppendLine(
            "encounter | campaign | objective | party | required power | wins | win rate | median rounds | mean ally health | severe casualty | stalled | capture down/dead/active | shots/executed/hits | end/peak sedation");
        foreach (EncounterOutcome outcome in outcomes)
        {
            report.AppendLine(string.Join(" | ",
                outcome.EncounterId,
                outcome.Campaign.ToString(CultureInfo.InvariantCulture),
                outcome.Objective,
                outcome.PartyMembers.ToString(CultureInfo.InvariantCulture),
                outcome.RequiredPower.ToString("0.0", CultureInfo.InvariantCulture),
                outcome.Wins.ToString(CultureInfo.InvariantCulture),
                outcome.WinRate.ToString("P1", CultureInfo.InvariantCulture),
                Median(outcome.Rounds).ToString("0.0", CultureInfo.InvariantCulture),
                outcome.MeanAllyHealthRatio.ToString("P1", CultureInfo.InvariantCulture),
                outcome.SevereCasualtyRate.ToString("P1", CultureInfo.InvariantCulture),
                outcome.Stalled.ToString(CultureInfo.InvariantCulture),
                $"{outcome.CaptureLeaderDowned}/{outcome.CaptureLeaderDeaths}/{outcome.CaptureLeaderActive}",
                $"{outcome.CaptureShotAttempts}/{outcome.CaptureShotCommands}/{outcome.CaptureShotHits}",
                 $"{outcome.MeanCaptureSedation:0.00}/{outcome.MeanCapturePeakSedation:0.00}"));
            foreach (string detail in outcome.StalledDetails)
            {
                report.AppendLine($"  STALL | {detail}");
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
        File.WriteAllText(ReportPath, report.ToString(), Encoding.UTF8);
        AssetDatabase.Refresh();

        int stalled = outcomes.Sum(value => value.Stalled);
        if (stalled > 0)
        {
            throw new InvalidOperationException(
                $"Combat outcome calibration stalled {stalled} battles. See {ReportPath}.");
        }

        return "COMBAT_OUTCOME_BALANCE=MEASURED; campaign_win_rates="
            + string.Join("/", outcomes
                .GroupBy(value => value.Campaign)
                .OrderBy(group => group.Key)
                .Select(group => group.Average(value => value.WinRate)
                    .ToString("P1", CultureInfo.InvariantCulture)));
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

    private static EncounterOutcome SimulateEncounter(
        OffenseTargetDefinition target,
        string encounterId,
        EnemyEncounterFactory factory,
        IEnemyArchetypeCatalog archetypes,
        ICombatEquipmentCatalog equipment,
        int samples,
        float partyPowerMultiplier)
    {
        EncounterOutcome result = new()
        {
            EncounterId = encounterId,
            Campaign = target.campaignOrder,
            PartyMembers = ReferencePartySize(target.campaignOrder),
            RequiredPower = target.requiredPower
        };
        result.Samples = Mathf.Max(1, samples);

        for (int sample = 0; sample < result.Samples; sample++)
        {
            EnemyEncounterComposition composition = CreateSpecificEncounter(
                factory,
                target,
                encounterId,
                sample,
                ((IEncounterCatalog)archetypes).Require(encounterId));
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
                sample,
                equipment,
                battleEquipment,
                composition.Encounter.objective,
                partyPowerMultiplier);
            List<OffenseBattleCombatant> combatants = allies
                .Concat(composition.Combatants)
                .ToList();
            OffenseBattleSession session = new(
                $"balance:{encounterId}:{sample}",
                $"balance-expedition:{encounterId}:{sample}",
                target.id,
                target.title,
                DungeonDifficulty.Normal,
                combatants,
                CreateCombatResolution(encounterId, sample),
                battleEquipment,
                composition.Rules);

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

                OffenseBattleCommand command = actor.Team == OffenseBattleTeam.Enemies
                    ? session.CreateEnemyCommand(commandId)
                    : CreateAllyCommand(session, actor, commandId);
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

                bool executed = session.TryExecuteCommand(
                    command,
                    out _);
                if (!executed)
                {
                    OffenseBattleCommand guard = new(
                        commandId,
                        actor.PersistentId,
                        OffenseBattleActionType.Guard,
                        actor.PersistentId);
                    if (!session.TryExecuteCommand(guard, out _))
                    {
                        break;
                    }
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
                commandId++;
            }

            if (!session.IsComplete)
            {
                result.Stalled++;
                result.StalledDetails.Add(BuildStalledDetail(
                    sample,
                    commands,
                    session));
            }
            if (session.Outcome == OffenseBattleOutcome.Victory)
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
            if (allies.Any(value => value.IsDead
                    || value.IsDowned
                    || value.HealthRatio < 0.25f))
            {
                result.SevereCasualties++;
            }
        }

        return result;
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
        int count = ReferencePartySize(target.campaignOrder);
        float memberPower = target.requiredPower
            * Mathf.Max(0.1f, partyPowerMultiplier)
            / count;
        float stat = Mathf.Max(1f, memberPower / 3.45f);
        string[] weaponIds = WeaponsFor(target.campaignOrder);
        string armorId = ArmorFor(target.campaignOrder);
        string shieldId = ShieldFor(target.campaignOrder);
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
            string weaponId =
                weaponIds[Mathf.Min(index, weaponIds.Length - 1)];
            if (capturesLeader || sabotagesTarget)
            {
                weaponId = "weapon:crossbow";
            }
            Equip(
                ally,
                equipment,
                equipmentRuntime,
                weaponId,
                armorId,
                index == 0 ? shieldId : string.Empty,
                $"ally:{target.campaignOrder}:{sample}:{index}",
                capturesLeader ? "ammo:tranquilizer-dart" : string.Empty);
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
        string ammunitionOverride = "")
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
                    quality = CombatEquipmentQuality.Normal,
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
                    CombatEquipmentQuality.Normal);
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
                    CombatEquipmentQuality.Normal,
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
                CombatEquipmentQuality.Normal,
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

    private static int ReferencePartySize(int campaignOrder) =>
        Mathf.Clamp(campaignOrder, 1, 6) switch
        {
            1 => 2,
            2 or 3 => 3,
            4 => 4,
            _ => 5
        };

    private static string[] WeaponsFor(int campaign) => campaign switch
    {
        1 => new[] { "weapon:dagger" },
        2 => new[] { "weapon:longsword" },
        3 => new[] { "weapon:longsword", "weapon:crossbow" },
        4 => new[] { "weapon:warhammer", "weapon:arquebus" },
        5 => new[]
        {
            "weapon:blacksteel-poleaxe",
            "weapon:sniper-arquebus",
            "weapon:repeating-crossbow"
        },
        _ => new[] { "weapon:rune-blade", "weapon:rune-bow", "weapon:mana-lance" }
    };

    private static string ArmorFor(int campaign) => campaign switch
    {
        1 => "armor:gambeson",
        2 => "armor:leather",
        3 => "armor:mail-shirt",
        4 => "armor:brigandine",
        5 => "armor:articulated-plate",
        _ => "armor:rune-ward-mail"
    };

    private static string ShieldFor(int campaign) => campaign switch
    {
        1 or 2 => "shield:wood",
        3 or 4 => "shield:iron",
        5 => "shield:blacksteel",
        _ => "shield:rune"
    };

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
        public int SevereCasualties;
        public int CaptureLeaderDowned;
        public int CaptureLeaderDeaths;
        public int CaptureLeaderActive;
        public int CaptureShotAttempts;
        public int CaptureShotCommands;
        public int CaptureShotHits;
        public float CaptureSedationSum;
        public float CapturePeakSedationSum;
        public float AllyHealthRatioSum;
        public int Samples = SamplesPerEncounter;
        public readonly List<int> Rounds = new();
        public readonly List<string> StalledDetails = new();
        public float WinRate => Wins / (float)Mathf.Max(1, Samples);
        public float MeanAllyHealthRatio =>
            AllyHealthRatioSum / Mathf.Max(1, Samples);
        public float SevereCasualtyRate =>
            SevereCasualties / (float)Mathf.Max(1, Samples);
        public float MeanCaptureSedation =>
            CaptureSedationSum / Mathf.Max(1, Samples);
        public float MeanCapturePeakSedation =>
            CapturePeakSedationSum / Mathf.Max(1, Samples);
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
