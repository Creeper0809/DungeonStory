#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class V20EnemyIndividualDebugScenarios
{
    [MenuItem("DungeonStory/QA/V20 Enemy Individual Continuity")]
    public static void Run()
    {
        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        DungeonRuntimeAggregateRootStore rootStore = new();
        CharacterNarrativeCatalog narrativeCatalog = new(content);
        CharacterNarrativeRuntime narrative = new(rootStore, narrativeCatalog);
        ResourceCharacterSpeciesCatalog species = new(content);
        CharacterLifeRuntime life = new(
            rootStore,
            species,
            new RandomStreamProvider(rootStore));
        EnemyCombatContentCatalog combat = new(content);
        EnemyIndividualFactory factory = new(
            combat,
            narrativeCatalog,
            narrative,
            narrative,
            life,
            life,
            species,
            content);

        EnemyArchetypeDefinitionSO[] humanIndividuals = combat.All
            .Where(value => string.Equals(
                value.speciesTag,
                "Human",
                StringComparison.Ordinal))
            .OrderBy(value => value.stableId, StringComparer.Ordinal)
            .ToArray();
        EnemyArchetypeDefinitionSO[] humanArchetypes = humanIndividuals
            .Where(value => value.factionId.StartsWith(
                "human:",
                StringComparison.Ordinal))
            .ToArray();
        Require(humanArchetypes.Length == 25,
            $"Expected 25 five-faction human tactical archetypes, found {humanArchetypes.Length}.");
        Require(humanIndividuals.Length >= humanArchetypes.Length,
            "Neutral human recruit templates disappeared from the shared individual factory.");

        EnemyArchetypeDefinitionSO firstArchetype = humanArchetypes[0];
        CharacterId stableId = CharacterId.FromStableSuffix(
            "v20-enemy-individual-continuity");
        EnemyIndividualSaveData first = factory.Create(
            firstArchetype.stableId,
            stableId,
            "qa:continuity");
        EnemyIndividualSaveData repeated = factory.Create(
            firstArchetype.stableId,
            stableId,
            "qa:continuity");
        Require(string.Equals(
                JsonUtility.ToJson(first),
                JsonUtility.ToJson(repeated),
                StringComparison.Ordinal),
            "The same enemy identity/context did not reproduce the same profile.");

        List<EnemyIndividualSaveData> population = new();
        for (int index = 0; index < 100; index++)
        {
            EnemyArchetypeDefinitionSO archetype =
                humanIndividuals[index % humanIndividuals.Length];
            population.Add(factory.Create(
                archetype.stableId,
                CharacterId.FromStableSuffix($"v20-enemy-sample:{index}"),
                "qa:population"));
        }

        Require(population.Select(value => value.displayName)
                .Distinct(StringComparer.Ordinal).Count() >= 40,
            "Enemy display identity variation is too low.");
        string[] backgrounds = population.Select(value => value.backgroundId)
            .Distinct(StringComparer.Ordinal).OrderBy(value => value).ToArray();
        string[] cultures = population.Select(value => value.cultureId)
            .Distinct(StringComparer.Ordinal).OrderBy(value => value).ToArray();
        string[] ambitions = population.Select(value => value.ambitionId)
            .Distinct(StringComparer.Ordinal).OrderBy(value => value).ToArray();
        Require(backgrounds.Length >= 8,
            $"Enemy background variation is too low ({backgrounds.Length}): {string.Join(",", backgrounds)}");
        Require(cultures.Length >= 8,
            $"Enemy culture variation is too low ({cultures.Length}): {string.Join(",", cultures)}");
        Require(ambitions.Length >= 12,
            $"Enemy ambition variation is too low ({ambitions.Length}): {string.Join(",", ambitions)}");
        Require(population.Select(ProfileFingerprint)
                .Distinct(StringComparer.Ordinal).Count() >= 90,
            "Enemy profiles are collapsing into fixed archetype clones.");

        EnemyIndividualBlueprint blueprint = factory.RequireBlueprint(first);
        factory.EnsureCharacterDomains(blueprint);
        Require(life.TryGet(stableId, out CharacterLifeRecord lifeRecord)
                && Math.Abs(lifeRecord.BiologicalAgeDayUnits
                    - first.biologicalAgeDayUnits) < 0.001d,
            "Enemy age was not preserved in the life aggregate.");
        Require(narrative.TryGet(
                    stableId,
                    out CharacterNarrativeSnapshot narrativeState)
                && narrativeState.HasEnemyOrigin
                && narrativeState.BackgroundId.Value == first.backgroundId
                && narrativeState.CultureId.Value == first.cultureId
                && narrativeState.ActiveAmbitionId.Value == first.ambitionId
                && narrativeState.OriginEnemyArchetypeId
                    == first.enemyArchetypeId,
            "Enemy origin, culture, or ambition was not preserved in the narrative aggregate.");

        DungeonOffenseReturnArrivalSaveData returnState = new()
        {
            nextArrivalSequence = 1,
            prisonerCandidatePools = new List<OffensePrisonerCandidatePoolState>
            {
                new()
                {
                    expeditionId = "expedition:v20-enemy-continuity",
                    individuals = new List<EnemyIndividualSaveData>
                    {
                        first.Clone()
                    }
                }
            }
        };
        DungeonOffenseReturnArrivalSaveData restoredReturnState =
            JsonUtility.FromJson<DungeonOffenseReturnArrivalSaveData>(
                JsonUtility.ToJson(returnState));
        Require(restoredReturnState.prisonerCandidatePools.Count == 1
                && restoredReturnState.prisonerCandidatePools[0]
                    .individuals.Count == 1,
            "The prisoner candidate pool did not survive serialization.");
        Require(string.Equals(
                JsonUtility.ToJson(restoredReturnState.prisonerCandidatePools[0]
                    .individuals[0]),
                JsonUtility.ToJson(first),
                StringComparison.Ordinal),
            "The prisoner candidate path changed the enemy individual profile.");

        VerifyEncounterObjectives(combat);

        Debug.Log(
            "V20_ENEMY_INDIVIDUAL_CONTINUITY=PASS; "
            + "humanArchetypes=25; samples=100; persistentIdentity=true; "
            + "offenseDefenseSharedFactory=true; encounterObjectives=6; "
            + "battlefieldModifiers=12");
    }

    private static void VerifyEncounterObjectives(EnemyCombatContentCatalog combat)
    {
        IBattlefieldModifierCatalog modifiers = combat;
        Require(modifiers.All.Count == 12,
            $"Expected 12 battlefield modifiers, found {modifiers.All.Count}.");
        BattlefieldModifierDefinitionSO modifier = modifiers.All.First(value =>
            !Mathf.Approximately(value.accuracyMultiplier, 1f)
            || !Mathf.Approximately(value.damageMultiplier, 1f)
            || !Mathf.Approximately(value.movementMultiplier, 1f));
        OffenseBattleEncounterRules scaled = Rules(
            OffenseEncounterObjective.DefeatAll,
            0,
            string.Empty,
            string.Empty,
            modifier);
        Require(!Mathf.Approximately(scaled.AccuracyMultiplier, 1f)
                || !Mathf.Approximately(scaled.DamageMultiplier, 1f)
                || !Mathf.Approximately(scaled.MovementMultiplier, 1f),
            "Authored battlefield modifier did not project a mechanical multiplier.");

        OffenseBattleCombatant survivor = Combatant(
            "ally:survivor", OffenseBattleTeam.Allies, 20f);
        OffenseBattleCombatant attacker = Combatant(
            "enemy:attacker", OffenseBattleTeam.Enemies, 10f);
        OffenseBattleSession survive = Session(
            new[] { survivor, attacker },
            Rules(OffenseEncounterObjective.SurviveRounds, 1));
        GuardCurrent(survive);
        GuardCurrent(survive);
        Require(survive.Outcome == OffenseBattleOutcome.Victory,
            "Survive-round objective did not complete at its round boundary.");

        OffenseBattleCombatant protectedAlly = Combatant(
            "ally:00-protected", OffenseBattleTeam.Allies, 20f, currentHealth: 0f);
        OffenseBattleCombatant escort = Combatant(
            "ally:10-escort", OffenseBattleTeam.Allies, 18f);
        OffenseBattleSession protect = Session(
            new[] { protectedAlly, escort, Combatant("enemy:protect", OffenseBattleTeam.Enemies, 10f) },
            Rules(OffenseEncounterObjective.ProtectTarget, 3, "target:protected"));
        Require(protect.Outcome == OffenseBattleOutcome.Defeat,
            "Protect objective did not fail when its protected combatant was lost.");

        const string deviceId = "objective:test:device";
        OffenseBattleSession sabotage = Session(
            new[]
            {
                Combatant("ally:saboteur", OffenseBattleTeam.Allies, 20f),
                Combatant(deviceId, OffenseBattleTeam.Enemies, 0f, currentHealth: 0f,
                    participatesInInitiative: false)
            },
            Rules(OffenseEncounterObjective.SabotageTarget, 3, "target:device", deviceId));
        Require(sabotage.Outcome == OffenseBattleOutcome.Victory,
            "Sabotage objective did not complete when the authored device was destroyed.");

        OffenseBattleSession escape = Session(
            new[]
            {
                Combatant("ally:escape", OffenseBattleTeam.Allies, 20f),
                Combatant("enemy:pursuer", OffenseBattleTeam.Enemies, 10f)
            },
            Rules(OffenseEncounterObjective.Escape, 2));
        Require(!escape.TryExecuteCommand(
                new OffenseBattleCommand(
                    1,
                    escape.CurrentActor.PersistentId,
                    OffenseBattleActionType.Retreat),
                out _),
            "Escape objective allowed retreat before the escape round.");
        GuardCurrent(escape);
        GuardCurrent(escape);
        Require(escape.RoundNumber == 2
                && escape.TryExecuteCommand(
                    new OffenseBattleCommand(
                        escape.LastProcessedCommandId + 1,
                        escape.CurrentActor.PersistentId,
                        OffenseBattleActionType.Retreat),
                    out _)
                && escape.Outcome == OffenseBattleOutcome.Victory,
            "Escape objective did not convert an unlocked retreat into victory.");

        OffenseBattleCombatant leader = Combatant(
            "enemy:leader", OffenseBattleTeam.Enemies, 10f);
        leader.ApplyBodyHealth(new CharacterBodyHealthSnapshot(
            new[]
            {
                Part(CombatBodyPart.Head, 18f, 1f),
                Part(CombatBodyPart.Torso, 45f, 45f),
                Part(CombatBodyPart.LeftArm, 22f, 22f),
                Part(CombatBodyPart.RightArm, 22f, 22f),
                Part(CombatBodyPart.LeftLeg, 26f, 26f),
                Part(CombatBodyPart.RightLeg, 26f, 26f)
            },
            0f,
            0f,
            0.05f,
            1f,
            1f,
            true));
        OffenseBattleSession capture = Session(
            new[] { Combatant("ally:captor", OffenseBattleTeam.Allies, 20f), leader },
            Rules(OffenseEncounterObjective.CaptureLeader, 3, "enemy:leader", leader.PersistentId));
        Require(capture.Outcome == OffenseBattleOutcome.Victory,
            "Capture-leader objective did not distinguish a living downed leader.");
    }

    private static OffenseBattleEncounterRules Rules(
        OffenseEncounterObjective objective,
        int roundLimit,
        string targetId = "",
        string targetCombatantId = "",
        params BattlefieldModifierDefinitionSO[] modifiers) =>
        new(objective, roundLimit, targetId, targetCombatantId, modifiers);

    private static OffenseBattleSession Session(
        IEnumerable<OffenseBattleCombatant> combatants,
        OffenseBattleEncounterRules rules) =>
        new(
            Guid.NewGuid().ToString("N"),
            "expedition:v20-objective",
            "target:v20-objective",
            "V20 Objective",
            DungeonDifficulty.Normal,
            combatants,
            OffenseEditorTestDependencies.CreateCombatResolution(),
            OffenseEditorTestDependencies.CreateCombatEquipmentRuntime(),
            rules);

    private static OffenseBattleCombatant Combatant(
        string id,
        OffenseBattleTeam team,
        float initiative,
        float currentHealth = 100f,
        bool participatesInInitiative = true) =>
        new(
            id,
            id,
            "test",
            team,
            new OffenseBattleStats(100f, 10f, 10f, 5f, initiative, 5f),
            currentHealth,
            formation: OffenseFormationSlot.Front,
            participatesInInitiative: participatesInInitiative);

    private static CharacterBodyPartHealthState Part(
        CombatBodyPart part,
        float maximum,
        float current) => new()
    {
        bodyPart = part,
        maxHealth = maximum,
        currentHealth = current
    };

    private static void GuardCurrent(OffenseBattleSession session)
    {
        Require(session.TryExecuteCommand(
                new OffenseBattleCommand(
                    session.LastProcessedCommandId + 1,
                    session.CurrentActor.PersistentId,
                    OffenseBattleActionType.Guard),
                out _),
            "Objective test guard command was rejected.");
    }

    private static string ProfileFingerprint(EnemyIndividualSaveData value) =>
        string.Join("|",
            value.enemyArchetypeId,
            value.backgroundId,
            value.cultureId,
            value.ambitionId,
            string.Join(",", value.generalTraitIds),
            string.Join(",", value.expressedHeritableTraitIds),
            string.Join(",", value.latentHeritableTraitIds),
            string.Join(",", value.innateAptitudes.Select(aptitude =>
                aptitude.skillId + ":" + aptitude.value)));

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
