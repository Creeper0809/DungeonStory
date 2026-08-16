using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class OffenseBattleDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Offense/Run Turn Battle Scenarios")]
    public static void RunFromMenu()
    {
        if (!RunAll(true)) Debug.LogError("Offense turn battle scenarios failed.");
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> errors = new List<string>();
        Run("damage and initiative", VerifyDamageAndInitiative, errors);
        Run("heal target and drain source", VerifyHealTargetAndDrainSource, errors);
        Run("guard and cooldown", VerifyGuardAndCooldown, errors);
        Run("planned round begins every participant once", VerifyPlannedRoundFinalization, errors);
        Run("enemy target priority", VerifyEnemyTargetPriority, errors);
        Run("enemy tactical tags and boss phase", VerifyEnemyTacticalTagsAndBossPhase, errors);
        Run("smoke and summon are dedicated effects", VerifySmokeAndSummonEffects, errors);
        Run("pavise deployment and persistence", VerifyPaviseDeployment, errors);
        Run("encounter counters use actual party equipment", VerifyEncounterCounterEvaluation, errors);
        Run("death retreat and command idempotence", VerifyOutcomesAndIdempotence, errors);
        Run("exact battle persistence", VerifyExactPersistence, errors);
        Run("fixed difficulty multipliers", VerifyDifficultyMultipliers, errors);
        Run("formation constraints", VerifyFormationConstraints, errors);
        Run("body injury and persistence", VerifyBodyInjuryAndPersistence, errors);
        Run("heavy suppression skips turn", VerifyHeavySuppressionSkipsTurn, errors);
        Run("empty ranged weapon recovery", VerifyEmptyRangedWeaponRecovery, errors);
        Run("combat equipment ownership", VerifyEquipmentReservation, errors);
        Run("combat equipment craft queue persistence", VerifyEquipmentCraftQueuePersistence, errors);
        Run("building equipment crafting work", VerifyBuildingEquipmentCraftingWork, errors);

        foreach (string error in errors) Debug.LogError(error);
        if (errors.Count == 0 && logSuccess) Debug.Log("Offense turn battle scenarios passed.");
        return errors.Count == 0;
    }

    private static bool VerifyDamageAndInitiative()
    {
        OffenseBattleCombatant ally = Combatant(
            "ally:a", "Ally", OffenseBattleTeam.Allies,
            100f, 10f, 6f, 5f, 10f, 5f);
        OffenseBattleCombatant enemy = Combatant(
            "enemy:a", "Enemy", OffenseBattleTeam.Enemies,
            100f, 8f, 5f, 8f, 5f, 4f);
        OffenseBattleSession session = Session(ally, enemy);

        float damage = session.CalculateBasicDamage(ally, enemy);
        Require(damage >= 10f && damage <= 15f,
            $"Expected a normal unarmed damage estimate, got {damage}.");
        Require(session.CurrentActor == ally, "Higher initiative ally did not act first.");

        OffenseBattleCombatant tieB = Combatant(
            "tie:b", "B", OffenseBattleTeam.Allies,
            100f, 5f, 5f, 5f, 5f, 5f);
        OffenseBattleCombatant tieA = Combatant(
            "tie:a", "A", OffenseBattleTeam.Enemies,
            100f, 5f, 5f, 5f, 5f, 5f);
        OffenseBattleSession tie = Session(tieB, tieA);
        Require(tie.CurrentActor == tieA, "Initiative ties are not ordered by persistent ID.");
        return true;
    }

    private static bool VerifyHealTargetAndDrainSource()
    {
        CharacterCombatAbilityDefinition fieldDressing = CharacterCombatAbilityCatalog.CreateFieldDressing();
        OffenseBattleCombatant healer = Combatant(
            "ally:healer", "Healer", OffenseBattleTeam.Allies,
            100f, 8f, 5f, 5f, 20f, 5f,
            fieldDressing);
        OffenseBattleCombatant wounded = Combatant(
            "ally:wounded", "Wounded", OffenseBattleTeam.Allies,
            100f, 8f, 5f, 5f, 10f, 5f,
            currentHealth: 40f);
        OffenseBattleCombatant enemy = Combatant(
            "enemy:heal-check", "Enemy", OffenseBattleTeam.Enemies,
            100f, 4f, 4f, 4f, 1f, 1f);
        OffenseBattleSession healSession = Session(healer, wounded, enemy);
        Require(healSession.TryExecuteCommand(
            new OffenseBattleCommand(
                1,
                healer.PersistentId,
                OffenseBattleActionType.Ability,
                wounded.PersistentId,
                fieldDressing.Id),
            out _), "Field dressing command was rejected.");
        Require(Mathf.Approximately(wounded.CurrentHealth, 58f),
            $"Field dressing healed {wounded.CurrentHealth}, expected target health 58.");
        Require(Mathf.Approximately(healer.CurrentHealth, 100f),
            "Field dressing healed the source instead of the target.");

        CharacterCombatAbilityDefinition drain = CharacterCombatAbilityCatalog.CreateVampireDrain();
        OffenseBattleCombatant vampire = Combatant(
            "ally:vampire", "Vampire", OffenseBattleTeam.Allies,
            100f, 10f, 8f, 5f, 20f, 5f,
            drain,
            currentHealth: 50f,
            formation: OffenseFormationSlot.Middle);
        OffenseBattleCombatant target = Combatant(
            "enemy:drain-check", "Drain Target", OffenseBattleTeam.Enemies,
            100f, 4f, 4f, 4f, 1f, 1f);
        OffenseBattleSession drainSession = Session(vampire, target);
        float vampireBefore = vampire.CurrentHealth;
        Require(drainSession.TryExecuteCommand(
            new OffenseBattleCommand(
                1,
                vampire.PersistentId,
                OffenseBattleActionType.Ability,
                target.PersistentId,
                drain.Id),
            out _), "Vampire drain command was rejected.");
        Require(vampire.CurrentHealth > vampireBefore,
            "Drain did not heal the source.");
        Require(target.CurrentHealth < target.Stats.MaxHealth,
            "Drain did not damage the target.");
        return true;
    }

    private static bool VerifyGuardAndCooldown()
    {
        CharacterCombatAbilityDefinition crush = CharacterCombatAbilityCatalog.CreateOrcCrush();
        OffenseBattleCombatant ally = Combatant(
            "ally:orc", "Orc", OffenseBattleTeam.Allies,
            120f, 10f, 8f, 8f, 10f, 5f, crush);
        OffenseBattleCombatant enemy = Combatant(
            "enemy:guard-test", "Enemy", OffenseBattleTeam.Enemies,
            120f, 10f, 6f, 5f, 5f, 4f);
        OffenseBattleSession guardSession = new OffenseBattleSession(
            Guid.NewGuid().ToString("N"),
            "expedition:guard-test",
            "target:guard-test",
            "Guard Test",
            DungeonDifficulty.Normal,
            new[] { ally, enemy },
            new FixedCombatResolutionService(
                Hit(CombatBodyPart.Torso, damage: 12f, bleeding: 0f, suppression: 0f)),
            OffenseEditorTestDependencies.CreateCombatEquipmentRuntime());
        Require(guardSession.TryExecuteCommand(
            new OffenseBattleCommand(1, ally.PersistentId, OffenseBattleActionType.Guard, ally.PersistentId),
            out _), "Guard command was rejected.");
        float before = ally.CurrentHealth;
        Require(guardSession.TryExecuteCommand(
            new OffenseBattleCommand(2, enemy.PersistentId, OffenseBattleActionType.BasicAttack, ally.PersistentId),
            out _), "Enemy attack was rejected.");
        float guardedDamage = before - ally.CurrentHealth;
        Require(Mathf.Approximately(guardedDamage, 6f),
            $"Guard reduced a fixed 12 damage hit to {guardedDamage}, expected 6.");

        OffenseBattleCombatant abilityAlly = Combatant(
            "ally:ability", "Orc", OffenseBattleTeam.Allies,
            120f, 10f, 8f, 8f, 10f, 5f, crush);
        OffenseBattleCombatant abilityEnemy = Combatant(
            "enemy:ability", "Enemy", OffenseBattleTeam.Enemies,
            300f, 5f, 5f, 6f, 5f, 4f);
        OffenseBattleSession abilitySession = Session(abilityAlly, abilityEnemy);
        Require(abilitySession.TryExecuteCommand(
            new OffenseBattleCommand(
                1,
                abilityAlly.PersistentId,
                OffenseBattleActionType.Ability,
                abilityEnemy.PersistentId,
                crush.Id),
            out _), "Ability command was rejected.");
        Require(abilityAlly.GetCooldown(crush.Id) == 2, "Ability cooldown was not applied.");
        return true;
    }

    private static bool VerifyPlannedRoundFinalization()
    {
        OffenseBattleCombatant ally = Combatant(
            "ally:planned-round", "Planned Ally", OffenseBattleTeam.Allies,
            120f, 10f, 8f, 8f, 10f, 5f);
        OffenseBattleCombatant enemy = Combatant(
            "enemy:planned-round", "Planned Enemy", OffenseBattleTeam.Enemies,
            120f, 10f, 8f, 8f, 9f, 5f);
        OffenseBattleSession session = Session(ally, enemy);
        Require(session.PreparePlannedRound(1, out string preparationFailure),
            preparationFailure);
        Require(ally.TurnsStarted == 1 && enemy.TurnsStarted == 1,
            "The first planned round did not prepare every participant symmetrically.");
        ally.SetCooldown("qa:planned-cooldown", 2);
        enemy.SetCooldown("qa:planned-cooldown", 2);
        int roundBefore = session.RoundNumber;
        int allyTurnsBefore = ally.TurnsStarted;
        int enemyTurnsBefore = enemy.TurnsStarted;

        Require(session.FinalizePlannedRound(1, out string finalizationFailure),
            finalizationFailure);
        Require(session.RoundNumber == roundBefore + 1,
            "Planned round did not advance exactly one round.");
        Require(ally.TurnsStarted == allyTurnsBefore + 1,
            "Planned round did not begin the ally exactly once.");
        Require(enemy.TurnsStarted == enemyTurnsBefore + 1,
            "Planned round did not begin the enemy exactly once.");
        Require(ally.GetCooldown("qa:planned-cooldown") == 1
            && enemy.GetCooldown("qa:planned-cooldown") == 1,
            "The first planned round decremented participant cooldowns asymmetrically.");
        int finalizedRound = session.RoundNumber;
        int finalizedAllyTurns = ally.TurnsStarted;
        int finalizedEnemyTurns = enemy.TurnsStarted;
        Require(session.FinalizePlannedRound(1, out finalizationFailure),
            finalizationFailure);
        Require(session.RoundNumber == finalizedRound
            && ally.TurnsStarted == finalizedAllyTurns
            && enemy.TurnsStarted == finalizedEnemyTurns,
            "Retrying the same planned-turn token advanced BeginTurn twice.");

        OffenseBattlePersistenceState saved = session.CapturePersistentState();
        Require(saved.preparedPlannedTurn == 2
            && saved.finalizedPlannedTurn == 1,
            "Planned-turn preparation/finalization tokens were not captured.");
        OffenseBattleCombatant restoredAlly = Combatant(
            ally.PersistentId, "Restored Planned Ally", OffenseBattleTeam.Allies,
            120f, 10f, 8f, 8f, 10f, 5f);
        OffenseBattleCombatant restoredEnemy = Combatant(
            enemy.PersistentId, "Restored Planned Enemy", OffenseBattleTeam.Enemies,
            120f, 10f, 8f, 8f, 9f, 5f);
        OffenseBattleSession restored = OffenseBattleSession.Restore(
            saved,
            new[] { restoredAlly, restoredEnemy },
            new FixedCombatResolutionService(
                Hit(CombatBodyPart.Torso, damage: 1f, bleeding: 0f, suppression: 0f)),
            OffenseEditorTestDependencies.CreateCombatEquipmentRuntime());
        int restoredRound = restored.RoundNumber;
        int restoredAllyTurns = restoredAlly.TurnsStarted;
        int restoredEnemyTurns = restoredEnemy.TurnsStarted;
        Require(restored.FinalizePlannedRound(1, out finalizationFailure),
            finalizationFailure);
        Require(restored.RoundNumber == restoredRound
            && restoredAlly.TurnsStarted == restoredAllyTurns
            && restoredEnemy.TurnsStarted == restoredEnemyTurns,
            "Save/restore replayed an already finalized planned turn.");
        return true;
    }

    private static bool VerifyEnemyTargetPriority()
    {
        OffenseBattleCombatant enemy = Combatant(
            "enemy:ai", "AI", OffenseBattleTeam.Enemies,
            100f, 12f, 8f, 5f, 20f, 8f);
        OffenseBattleCombatant lethal = Combatant(
            "ally:lethal", "Low", OffenseBattleTeam.Allies,
            100f, 5f, 5f, 2f, 5f, 4f,
            currentHealth: 4f);
        OffenseBattleCombatant healthy = Combatant(
            "ally:healthy", "Healthy", OffenseBattleTeam.Allies,
            100f, 20f, 5f, 2f, 5f, 4f);
        OffenseBattleSession session = Session(enemy, lethal, healthy);
        OffenseBattleCommand command = session.CreateEnemyCommand(1);
        Require(command != null && command.TargetId == lethal.PersistentId,
            "Enemy AI did not prioritize a lethal target.");
        return true;
    }

    private static bool VerifyEnemyTacticalTagsAndBossPhase()
    {
        CharacterCombatAbilityDefinition phaseAbility = new(
            "qa:phase-ability",
            "Phase",
            "Boss phase ability.",
            0,
            OffenseBattleTargetRule.Enemy,
            new OffenseDamageEffect(0.2f));
        CharacterCombatAbilityDefinition ordinaryAbility = new(
            "qa:ordinary-ability",
            "Ordinary",
            "Ordinary ability.",
            0,
            OffenseBattleTargetRule.Enemy,
            new OffenseDamageEffect(3f));
        EnemyArchetypeDefinitionSO bossDefinition =
            ScriptableObject.CreateInstance<EnemyArchetypeDefinitionSO>();
        try
        {
            bossDefinition.stableId = "enemy:qa-boss";
            bossDefinition.tacticalProfile = new EnemyTacticalProfile
            {
                attackWeight = 0.1f,
                protectWeight = 0.1f,
                abilityWeight = 9f,
                formationTag = "front",
                preferredTargetTags = new List<string> { "nearest" },
                avoidedTargetTags = new List<string> { "shielded" }
            };
            bossDefinition.bossPhases = new List<EnemyBossPhaseRecord>
            {
                new EnemyBossPhaseRecord
                {
                    healthThreshold = 0.5f,
                    abilityIds = new List<string> { phaseAbility.Id },
                    tacticalProfileOverrideTag = "desperate"
                }
            };
            OffenseBattleCombatant boss = Combatant(
                "enemy:qa-boss:1",
                "Boss",
                OffenseBattleTeam.Enemies,
                100f,
                8f,
                7f,
                7f,
                30f,
                5f,
                currentHealth: 40f);
            boss = new OffenseBattleCombatant(
                boss.PersistentId,
                boss.DisplayName,
                boss.SpeciesTag,
                boss.Team,
                boss.Stats,
                boss.CurrentHealth,
                new[] { ordinaryAbility, phaseAbility });
            OffenseBattleCombatant shielded = Combatant(
                "ally:shielded",
                "Shielded",
                OffenseBattleTeam.Allies,
                100f,
                6f,
                5f,
                5f,
                5f,
                4f);
            shielded.SetCombatEquipment(
                CombatWeaponSnapshot.CreateUnarmed(),
                Array.Empty<CombatArmorSnapshot>(),
                new CombatShieldSnapshot(
                    "qa:shield",
                    CombatEquipmentQuality.Normal,
                    1f,
                    0.5f,
                    0f,
                    2f,
                    2f,
                    2f));
            OffenseBattleCombatant exposed = Combatant(
                "ally:exposed",
                "Exposed",
                OffenseBattleTeam.Allies,
                100f,
                6f,
                5f,
                5f,
                5f,
                4f);
            OffenseBattleSession session = Session(boss, shielded, exposed);
            EnemyTacticalDecision decision = new EnemyTacticalDecisionService(
                    new SingleEnemyCatalog(bossDefinition))
                .Decide(
                    session,
                    new EnemyIndividualSaveData
                    {
                        characterId = boss.PersistentId,
                        enemyArchetypeId = bossDefinition.stableId
                    });
            Require(decision.Intent == EnemyTacticalIntentKind.UseAbility
                    && decision.AbilityId == phaseAbility.Id,
                "Active boss phase did not prioritize its authored ability.");
            Require(decision.TargetId == exposed.PersistentId,
                "Avoided shielded target tag did not affect target selection.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(bossDefinition);
        }
    }

    private static bool VerifySmokeAndSummonEffects()
    {
        CharacterCombatAbilityDefinition smoke = new(
            "qa:smoke",
            "Smoke",
            "Smoke test.",
            0,
            OffenseBattleTargetRule.Self,
            new OffenseSmokeEffect(0.6f, 2));
        OffenseBattleCombatant smokeUser = Combatant(
            "enemy:smoke",
            "Smoke User",
            OffenseBattleTeam.Enemies,
            100f,
            5f,
            5f,
            5f,
            30f,
            5f,
            smoke);
        OffenseBattleCombatant shooter = Combatant(
            "ally:shooter",
            "Shooter",
            OffenseBattleTeam.Allies,
            100f,
            10f,
            6f,
            5f,
            10f,
            5f);
        shooter.SetCombatEquipment(
            CreateTestRangedWeapon(),
            Array.Empty<CombatArmorSnapshot>());
        OffenseBattleSession smokeSession = Session(smokeUser, shooter);
        CombatAttackPreview clear = smokeSession.PreviewBasicAttack(shooter, smokeUser);
        Require(smokeSession.TryExecuteCommand(
                new OffenseBattleCommand(
                    1,
                    smokeUser.PersistentId,
                    OffenseBattleActionType.Ability,
                    smokeUser.PersistentId,
                    smoke.Id),
                out _),
            "Smoke ability was rejected.");
        CombatAttackPreview obscured = smokeSession.PreviewBasicAttack(shooter, smokeUser);
        Require(smokeUser.Statuses.Any(value =>
                value.Type == OffenseBattleStatusType.SmokeObscured),
            "Smoke did not create its dedicated persistent status.");
        Require(obscured.HitChance < clear.HitChance
                && obscured.CoverBlockChance > clear.CoverBlockChance,
            "Smoke did not reduce ranged accuracy and add battlefield cover.");

        CharacterCombatAbilityDefinition summon = new(
            "qa:summon",
            "Summon",
            "Summon test.",
            0,
            OffenseBattleTargetRule.Self,
            new OffenseSummonEffect(20f, 3));
        OffenseBattleCombatant summoner = Combatant(
            "enemy:summoner",
            "Summoner",
            OffenseBattleTeam.Enemies,
            100f,
            5f,
            5f,
            5f,
            30f,
            5f,
            summon);
        OffenseBattleCombatant attacker = Combatant(
            "ally:summon-attacker",
            "Attacker",
            OffenseBattleTeam.Allies,
            100f,
            10f,
            6f,
            5f,
            10f,
            5f);
        OffenseBattleSession summonSession = new(
            Guid.NewGuid().ToString("N"),
            "expedition:summon-test",
            "target:summon-test",
            "Summon Test",
            DungeonDifficulty.Normal,
            new[] { summoner, attacker },
            new FixedCombatResolutionService(
                Hit(CombatBodyPart.Torso, 12f, 0f, 0f)),
            OffenseEditorTestDependencies.CreateCombatEquipmentRuntime());
        Require(summonSession.TryExecuteCommand(
                new OffenseBattleCommand(
                    1,
                    summoner.PersistentId,
                    OffenseBattleActionType.Ability,
                    summoner.PersistentId,
                    summon.Id),
                out _),
            "Summon ability was rejected.");
        Require(summonSession.CapturePersistentState().combatants
                .Single(value => value.persistentId == summoner.PersistentId)
                .statuses.Any(value =>
                    value.type == OffenseBattleStatusType.SummonedGuard
                    && Mathf.Approximately(value.value, 20f)),
            "Summoned guard pool was not captured by battle persistence.");
        float before = summoner.CurrentHealth;
        Require(summonSession.TryExecuteCommand(
                new OffenseBattleCommand(
                    2,
                    attacker.PersistentId,
                    OffenseBattleActionType.BasicAttack,
                    summoner.PersistentId),
                out _),
            "Attack against summoned guard was rejected.");
        Require(Mathf.Approximately(summoner.CurrentHealth, before),
            "Summoned guard pool did not intercept incoming damage.");
        return true;
    }

    private static bool VerifyPaviseDeployment()
    {
        OffenseBattleCombatant paviseBearer = Combatant(
            "ally:pavise",
            "Pavise Bearer",
            OffenseBattleTeam.Allies,
            100f,
            8f,
            6f,
            7f,
            20f,
            4f);
        paviseBearer.SetCombatEquipment(
            CombatWeaponSnapshot.CreateUnarmed(),
            Array.Empty<CombatArmorSnapshot>(),
            new CombatShieldSnapshot(
                "equipment:pavise",
                CombatEquipmentQuality.Normal,
                1f,
                0.68f,
                0f,
                34f,
                31f,
                26f,
                definitionId: "shield:pavise",
                roleFlags: CombatEquipmentRoleFlags.DeployableCover));
        OffenseBattleCombatant enemy = Combatant(
            "enemy:pavise",
            "Enemy",
            OffenseBattleTeam.Enemies,
            100f,
            6f,
            5f,
            5f,
            5f,
            4f);
        OffenseBattleSession session = Session(paviseBearer, enemy);
        Require(session.TryExecuteCommand(
                new OffenseBattleCommand(
                    1,
                    paviseBearer.PersistentId,
                    OffenseBattleActionType.DeployCover,
                    paviseBearer.PersistentId),
                out _),
            "Pavise deployment command was rejected.");
        Require(paviseBearer.CoverBlockChance >= 0.55f,
            "Pavise did not create meaningful cover.");
        OffenseBattleCombatantPersistenceState saved = session
            .CapturePersistentState()
            .combatants
            .Single(value => value.persistentId == paviseBearer.PersistentId);
        Require(Mathf.Approximately(
                saved.coverBlockChance,
                paviseBearer.CoverBlockChance),
            "Deployed pavise cover was not persisted.");
        return true;
    }

    private static bool VerifyOutcomesAndIdempotence()
    {
        OffenseBattleCombatant ally = Combatant(
            "ally:strong", "Strong", OffenseBattleTeam.Allies,
            100f, 50f, 20f, 5f, 10f, 5f);
        OffenseBattleCombatant enemy = Combatant(
            "enemy:weak", "Weak", OffenseBattleTeam.Enemies,
            20f, 1f, 1f, 1f, 1f, 1f);
        OffenseBattleSession victory = Session(ally, enemy);
        OffenseBattleCommand kill = new OffenseBattleCommand(
            1, ally.PersistentId, OffenseBattleActionType.BasicAttack, enemy.PersistentId);
        Require(victory.TryExecuteCommand(kill, out _), "Killing attack was rejected.");
        Require(victory.Outcome == OffenseBattleOutcome.Victory, "Enemy wipe did not produce battle victory.");
        Require(!victory.TryExecuteCommand(kill, out _), "Duplicate command was processed twice.");
        Require(victory.LastProcessedCommandId == 1, "Duplicate command changed the processed command ID.");

        OffenseBattleCombatant retreatAlly = Combatant(
            "ally:retreat", "Retreat", OffenseBattleTeam.Allies,
            100f, 5f, 5f, 5f, 10f, 5f);
        OffenseBattleCombatant retreatEnemy = Combatant(
            "enemy:retreat", "Enemy", OffenseBattleTeam.Enemies,
            100f, 5f, 5f, 5f, 1f, 1f);
        OffenseBattleSession retreat = Session(retreatAlly, retreatEnemy);
        Require(retreat.TryExecuteCommand(
            new OffenseBattleCommand(
                1,
                retreatAlly.PersistentId,
                OffenseBattleActionType.Retreat,
                retreatAlly.PersistentId),
            out _), "Retreat command was rejected.");
        Require(retreat.Outcome == OffenseBattleOutcome.Retreated, "Retreat did not end the battle as failure.");
        return true;
    }

    private static bool VerifyExactPersistence()
    {
        OffenseBattleCombatant ally = Combatant(
            "ally:save", "Saver", OffenseBattleTeam.Allies,
            100f, 8f, 6f, 6f, 10f, 5f,
            CharacterCombatAbilityCatalog.CreateSlimeBarrier());
        OffenseBattleCombatant enemy = Combatant(
            "enemy:save", "Enemy", OffenseBattleTeam.Enemies,
            140f, 9f, 6f, 6f, 5f, 4f);
        OffenseBattleSession original = Session(ally, enemy);
        Require(original.TryExecuteCommand(
            new OffenseBattleCommand(1, ally.PersistentId, OffenseBattleActionType.Guard, ally.PersistentId),
            out _), "Pre-save command failed.");

        OffenseBattlePersistenceState state = original.CapturePersistentState();
        OffenseBattleCombatant restoredAlly = Combatant(
            "ally:save", "Saver", OffenseBattleTeam.Allies,
            100f, 8f, 6f, 6f, 10f, 5f,
            CharacterCombatAbilityCatalog.CreateSlimeBarrier());
        OffenseBattleCombatant restoredEnemy = Combatant(
            "enemy:save", "Enemy", OffenseBattleTeam.Enemies,
            140f, 9f, 6f, 6f, 5f, 4f);
        OffenseBattleSession restored = OffenseBattleSession.Restore(
            state,
            new[] { restoredAlly, restoredEnemy },
            OffenseEditorTestDependencies.CreateCombatResolution(),
            OffenseEditorTestDependencies.CreateCombatEquipmentRuntime());

        Require(restored.BattleId == original.BattleId, "Battle ID changed during restore.");
        Require(restored.CurrentActor?.PersistentId == original.CurrentActor?.PersistentId,
            "Current actor changed during restore.");
        Require(restored.RoundNumber == original.RoundNumber, "Round changed during restore.");
        Require(restored.LastProcessedCommandId == original.LastProcessedCommandId,
            "Last command ID changed during restore.");
        Require(restoredAlly.Statuses.Count == ally.Statuses.Count,
            "Statuses changed during restore.");
        Require(!restored.TryExecuteCommand(
            new OffenseBattleCommand(1, ally.PersistentId, OffenseBattleActionType.Guard, ally.PersistentId),
            out _), "Restored battle reprocessed a completed command.");
        return true;
    }

    private static bool VerifyDifficultyMultipliers()
    {
        DungeonDifficultyMultipliers easy =
            DungeonDifficultyRules.GetOffenseMultipliers(DungeonDifficulty.Easy);
        DungeonDifficultyMultipliers normal =
            DungeonDifficultyRules.GetOffenseMultipliers(DungeonDifficulty.Normal);
        DungeonDifficultyMultipliers hard =
            DungeonDifficultyRules.GetOffenseMultipliers(DungeonDifficulty.Hard);
        Require(Mathf.Approximately(easy.EnemyHealth, normal.EnemyHealth * 0.8f),
            "Easy enemy health multiplier is incorrect.");
        Require(Mathf.Approximately(hard.EnemyHealth, normal.EnemyHealth * 1.25f),
            "Hard enemy health multiplier is incorrect.");
        Require(Mathf.Approximately(hard.EnemyAttack, normal.EnemyAttack * 1.2f),
            "Hard enemy attack multiplier is incorrect.");
        return true;
    }

    private static bool VerifyFormationConstraints()
    {
        CharacterCombatAbilityDefinition crush = CharacterCombatAbilityCatalog.CreateOrcCrush();
        OffenseBattleCombatant rearOrc = new OffenseBattleCombatant(
            "ally:rear-orc",
            "Rear Orc",
            "Orc",
            OffenseBattleTeam.Allies,
            new OffenseBattleStats(120f, 10f, 8f, 8f, 20f, 5f),
            120f,
            new[] { crush },
            formation: OffenseFormationSlot.Rear);
        OffenseBattleCombatant frontEnemy = new OffenseBattleCombatant(
            "enemy:front",
            "Front",
            "Human",
            OffenseBattleTeam.Enemies,
            new OffenseBattleStats(60f, 2f, 2f, 2f, 2f, 2f),
            60f,
            formation: OffenseFormationSlot.Front);
        OffenseBattleCombatant rearEnemy = new OffenseBattleCombatant(
            "enemy:rear",
            "Rear",
            "Human",
            OffenseBattleTeam.Enemies,
            new OffenseBattleStats(60f, 2f, 2f, 2f, 2f, 2f),
            60f,
            formation: OffenseFormationSlot.Rear);
        OffenseBattleSession session = Session(rearOrc, frontEnemy, rearEnemy);

        Require(!session.TryExecuteCommand(
            new OffenseBattleCommand(
                1,
                rearOrc.PersistentId,
                OffenseBattleActionType.Ability,
                frontEnemy.PersistentId,
                crush.Id),
            out _), "Rear formation used a front/middle-only ability.");
        Require(!session.TryExecuteCommand(
            new OffenseBattleCommand(
                2,
                rearOrc.PersistentId,
                OffenseBattleActionType.BasicAttack,
                rearEnemy.PersistentId),
            out _), "Basic attack bypassed a living front target.");
        return true;
    }

    private static bool VerifyBodyInjuryAndPersistence()
    {
        OffenseBattleCombatant ally = Combatant(
            "ally:body-test",
            "Attacker",
            OffenseBattleTeam.Allies,
            100f,
            8f,
            6f,
            5f,
            20f,
            5f);
        OffenseBattleCombatant enemy = Combatant(
            "enemy:body-test",
            "Defender",
            OffenseBattleTeam.Enemies,
            100f,
            5f,
            5f,
            5f,
            5f,
            4f);
        FixedCombatResolutionService resolver = new FixedCombatResolutionService(
            Hit(CombatBodyPart.LeftArm, damage: 11f, bleeding: 2f, suppression: 8f));
        OffenseBattleSession session = new OffenseBattleSession(
            Guid.NewGuid().ToString("N"),
            "expedition:body-test",
            "target:body-test",
            "Body Test",
            DungeonDifficulty.Normal,
            new[] { ally, enemy },
            resolver,
            OffenseEditorTestDependencies.CreateCombatEquipmentRuntime());
        Require(session.TryExecuteCommand(
            new OffenseBattleCommand(
                1,
                ally.PersistentId,
                OffenseBattleActionType.BasicAttack,
                enemy.PersistentId),
            out _), "Body-part attack was rejected.");

        CharacterBodyPartHealthState injuredArm = enemy.BodyParts.Single(
            part => part.bodyPart == CombatBodyPart.LeftArm);
        Require(Mathf.Approximately(injuredArm.currentHealth, 11f),
            $"Left arm health was {injuredArm.currentHealth}, expected 11.");
        Require(enemy.Manipulation < 1f && enemy.Manipulation > 0.7f,
            "Arm injury did not reduce manipulation.");
        Require(enemy.BloodLoss > 0f,
            "Bleeding hit did not increase blood loss.");

        OffenseBattlePersistenceState state = session.CapturePersistentState();
        OffenseBattleCombatant restoredAlly = Combatant(
            ally.PersistentId,
            ally.DisplayName,
            ally.Team,
            100f,
            8f,
            6f,
            5f,
            20f,
            5f);
        OffenseBattleCombatant restoredEnemy = Combatant(
            enemy.PersistentId,
            enemy.DisplayName,
            enemy.Team,
            100f,
            5f,
            5f,
            5f,
            5f,
            4f);
        OffenseBattleSession restored = OffenseBattleSession.Restore(
            state,
            new[] { restoredAlly, restoredEnemy },
            resolver,
            OffenseEditorTestDependencies.CreateCombatEquipmentRuntime());
        CharacterBodyPartHealthState restoredArm = restoredEnemy.BodyParts.Single(
            part => part.bodyPart == CombatBodyPart.LeftArm);
        Require(Mathf.Approximately(restoredArm.currentHealth, injuredArm.currentHealth),
            "Body-part health changed during battle restore.");
        Require(Mathf.Approximately(restoredEnemy.BloodLoss, enemy.BloodLoss),
            "Blood loss changed during battle restore.");
        Require(Mathf.Approximately(restoredEnemy.Manipulation, enemy.Manipulation),
            "Limb penalties changed during battle restore.");
        return true;
    }

    private static bool VerifyHeavySuppressionSkipsTurn()
    {
        OffenseBattleCombatant ally = Combatant(
            "ally:suppression-test",
            "Suppressor",
            OffenseBattleTeam.Allies,
            100f,
            8f,
            6f,
            5f,
            20f,
            5f);
        OffenseBattleCombatant enemy = Combatant(
            "enemy:suppression-test",
            "Pinned Target",
            OffenseBattleTeam.Enemies,
            100f,
            5f,
            5f,
            5f,
            5f,
            4f);
        OffenseBattleSession session = new OffenseBattleSession(
            Guid.NewGuid().ToString("N"),
            "expedition:suppression-test",
            "target:suppression-test",
            "Suppression Test",
            DungeonDifficulty.Normal,
            new[] { ally, enemy },
            new FixedCombatResolutionService(
                Hit(CombatBodyPart.Torso, damage: 1f, bleeding: 0f, suppression: 80f)),
            OffenseEditorTestDependencies.CreateCombatEquipmentRuntime());

        Require(session.TryExecuteCommand(
            new OffenseBattleCommand(
                1,
                ally.PersistentId,
                OffenseBattleActionType.BasicAttack,
                enemy.PersistentId),
            out _), "Suppressive attack was rejected.");
        Require(enemy.PinnedThisTurn,
            "Suppression 75 or higher did not pin the target.");
        Require(enemy.TurnsStarted == 1,
            "Pinned target did not begin exactly one skipped turn.");
        Require(session.CurrentActor == ally,
            "Pinned target retained the current turn instead of being skipped.");
        Require(session.Log.Any(entry => entry.Contains("제압", StringComparison.Ordinal)),
            "Pinned turn did not leave a readable combat log.");
        return true;
    }

    private static bool VerifyEncounterCounterEvaluation()
    {
        BattlefieldModifierDefinitionSO modifier =
            ScriptableObject.CreateInstance<BattlefieldModifierDefinitionSO>();
        try
        {
            modifier.stableId = "battlefield:test-counter";
            modifier.displayName = "Counter Test";
            modifier.accuracyMultiplier = 0.5f;
            modifier.movementMultiplier = 1f;
            modifier.damageMultiplier = 1f;
            modifier.requiredCounterTag = "counter:precision";

            OffenseBattleCombatant prepared = Combatant(
                "ally:counter-prepared",
                "Prepared",
                OffenseBattleTeam.Allies,
                100f, 8f, 6f, 5f, 10f, 5f,
                formation: OffenseFormationSlot.Rear);
            prepared.SetCombatEquipment(
                CreateTestRangedWeapon(),
                Array.Empty<CombatArmorSnapshot>());
            OffenseBattleCombatant enemy = Combatant(
                "enemy:counter-test",
                "Enemy",
                OffenseBattleTeam.Enemies,
                100f, 8f, 6f, 5f, 5f, 4f);
            OffenseBattleEncounterRules preparedRules = new(
                OffenseEncounterObjective.DefeatAll,
                0,
                string.Empty,
                string.Empty,
                new[] { modifier },
                new[] { "counter:precision" });
            _ = new OffenseBattleSession(
                "battle:counter-prepared",
                "expedition:counter-prepared",
                "target:counter-prepared",
                "Counter Prepared",
                DungeonDifficulty.Normal,
                new[] { prepared, enemy },
                OffenseEditorTestDependencies.CreateCombatResolution(),
                OffenseEditorTestDependencies.CreateCombatEquipmentRuntime(),
                preparedRules);

            Require(preparedRules.MatchedCounterTags.Contains("counter:precision"),
                "A precision ranged weapon did not satisfy the authored counter.");
            Require(preparedRules.GetAccuracyMultiplier(OffenseBattleTeam.Allies) > 1f,
                "Matched counter did not neutralize the allied accuracy penalty.");
            Require(Mathf.Approximately(
                    preparedRules.GetAccuracyMultiplier(OffenseBattleTeam.Enemies),
                    0.5f),
                "An allied counter incorrectly removed the enemy battlefield modifier.");

            OffenseBattleEncounterRules unpreparedRules = new(
                OffenseEncounterObjective.DefeatAll,
                0,
                string.Empty,
                string.Empty,
                new[] { modifier },
                new[] { "counter:precision" });
            unpreparedRules.EvaluatePartyCounters(new[]
            {
                Combatant(
                    "ally:counter-unprepared",
                    "Unprepared",
                    OffenseBattleTeam.Allies,
                    100f, 8f, 6f, 5f, 10f, 5f)
            });
            Require(Mathf.Approximately(
                    unpreparedRules.GetAccuracyMultiplier(OffenseBattleTeam.Allies),
                    0.5f),
                "An unprepared party bypassed the authored battlefield penalty.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(modifier);
        }
    }

    private static OffenseBattleSession Session(params OffenseBattleCombatant[] combatants)
    {
        return new OffenseBattleSession(
            Guid.NewGuid().ToString("N"),
            "expedition:test",
            "target:test",
            "Test",
            DungeonDifficulty.Normal,
            combatants,
            OffenseEditorTestDependencies.CreateCombatResolution(),
            OffenseEditorTestDependencies.CreateCombatEquipmentRuntime());
    }

    private static bool VerifyEmptyRangedWeaponRecovery()
    {
        ICombatEquipmentRuntime runtime =
            OffenseEditorTestDependencies.CreateCombatEquipmentRuntime();
        OffenseBattleCombatant enemy = Combatant(
            "enemy:empty-ranged",
            "Empty Ranged",
            OffenseBattleTeam.Enemies,
            100f,
            8f,
            6f,
            5f,
            20f,
            5f);
        OffenseBattleCombatant ally = Combatant(
            "ally:empty-ranged",
            "Ally",
            OffenseBattleTeam.Allies,
            100f,
            5f,
            5f,
            5f,
            1f,
            1f);
        CombatEquipmentInstance crossbow = runtime.CreateExternalInstance(
            "weapon:crossbow",
            CombatEquipmentQuality.Normal);
        CombatWeaponSnapshot emptyCrossbow = null;
        Require(runtime.TryAssignToCharacter(
                enemy.PersistentId,
                crossbow.instanceId,
                out _)
            && runtime.TrySetActiveWeapon(
                enemy.PersistentId,
                crossbow.instanceId,
                out _)
            && runtime.TryGetActiveWeapon(
                enemy.PersistentId,
                out emptyCrossbow),
            "Could not prepare the empty ranged weapon.");
        enemy.SetCombatEquipment(
            emptyCrossbow,
            runtime.GetArmor(enemy.PersistentId),
            runtime.GetShield(enemy.PersistentId));
        OffenseBattleSession unarmedSession = new(
            "battle:empty-ranged",
            "expedition:empty-ranged",
            "target:empty-ranged",
            "Empty Ranged",
            DungeonDifficulty.Normal,
            new[] { enemy, ally },
            OffenseEditorTestDependencies.CreateCombatResolution(),
            runtime);
        OffenseBattleCommand unarmed = unarmedSession.CreateEnemyCommand(1);
        Require(unarmed?.ActionType == OffenseBattleActionType.SwitchWeapon
                && string.Equals(
                    unarmed.AbilityId,
                    "combat:unarmed",
                    StringComparison.Ordinal)
                && unarmedSession.TryExecuteCommand(unarmed, out _)
                && string.Equals(
                    enemy.Weapon.DefinitionId,
                    "combat:unarmed",
                    StringComparison.Ordinal),
            "An empty ranged-only enemy did not spend a turn switching to unarmed combat.");

        ICombatEquipmentRuntime fallbackRuntime =
            OffenseEditorTestDependencies.CreateCombatEquipmentRuntime();
        OffenseBattleCombatant armedEnemy = Combatant(
            "enemy:melee-fallback",
            "Melee Fallback",
            OffenseBattleTeam.Enemies,
            100f,
            8f,
            6f,
            5f,
            20f,
            5f);
        OffenseBattleCombatant secondAlly = Combatant(
            "ally:melee-fallback",
            "Second Ally",
            OffenseBattleTeam.Allies,
            100f,
            5f,
            5f,
            5f,
            1f,
            1f);
        CombatEquipmentInstance emptyWeapon =
            fallbackRuntime.CreateExternalInstance(
                "weapon:crossbow",
                CombatEquipmentQuality.Normal);
        CombatEquipmentInstance dagger = fallbackRuntime.CreateExternalInstance(
            "weapon:dagger",
            CombatEquipmentQuality.Normal);
        CombatWeaponSnapshot secondEmptyCrossbow = null;
        Require(fallbackRuntime.TryAssignToCharacter(
                armedEnemy.PersistentId,
                emptyWeapon.instanceId,
                out _)
            && fallbackRuntime.TryAssignToCharacter(
                armedEnemy.PersistentId,
                dagger.instanceId,
                out _)
            && fallbackRuntime.TrySetActiveWeapon(
                armedEnemy.PersistentId,
                emptyWeapon.instanceId,
                out _)
            && fallbackRuntime.TryGetActiveWeapon(
                armedEnemy.PersistentId,
                out secondEmptyCrossbow),
            "Could not prepare the owned melee fallback.");
        armedEnemy.SetCombatEquipment(
            secondEmptyCrossbow,
            fallbackRuntime.GetArmor(armedEnemy.PersistentId),
            fallbackRuntime.GetShield(armedEnemy.PersistentId));
        OffenseBattleSession fallbackSession = new(
            "battle:melee-fallback",
            "expedition:melee-fallback",
            "target:melee-fallback",
            "Melee Fallback",
            DungeonDifficulty.Normal,
            new[] { armedEnemy, secondAlly },
            OffenseEditorTestDependencies.CreateCombatResolution(),
            fallbackRuntime);
        OffenseBattleCommand switchCommand = fallbackSession.CreateEnemyCommand(1);
        Require(switchCommand?.ActionType == OffenseBattleActionType.SwitchWeapon
                && string.Equals(
                    switchCommand.AbilityId,
                    dagger.instanceId,
                    StringComparison.Ordinal)
                && fallbackSession.TryExecuteCommand(switchCommand, out _)
                && string.Equals(
                    armedEnemy.Weapon.DefinitionId,
                    "weapon:dagger",
                    StringComparison.Ordinal),
            "An empty ranged weapon did not switch to an owned usable melee weapon.");
        return true;
    }

    private static bool VerifyEquipmentReservation()
    {
        ResourceCombatEquipmentCatalog catalog = new ResourceCombatEquipmentCatalog(new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        WorldItemRepository itemRepository =
            new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
        CombatEquipmentRuntime runtime = CombatEquipmentEditorTestFactory.Create(
            catalog,
            itemRepository,
            new CharacterCarryInventoryRegistry(),
            researchProvider: EditorAllResearchRuntimeProvider.Instance, materialCatalog: EmptyResourceEconomyContentCatalog.Instance, evolutionModules: EmptyEvolutionModuleRegistry.Instance, moduleCatalog: EmptyEquipmentModuleCatalog.Instance, itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance);
        CombatEquipmentInstance weapon = runtime.CreateInstance(
            "weapon:dagger",
            CombatEquipmentQuality.Good);
        CombatEquipmentInstance armor = runtime.CreateInstance(
            "armor:gambeson",
            CombatEquipmentQuality.Normal);
        Require(runtime.TryAssignToCharacter("staff:a", weapon.instanceId, out _),
            "Could not equip the available weapon.");
        Require(!runtime.TryAssignToCharacter("staff:b", weapon.instanceId, out _),
            "A unique weapon was assigned to two characters.");
        Require(runtime.TryAssignToCharacter("staff:a", armor.instanceId, out _),
            "Could not equip the available armor.");

        string json = JsonUtility.ToJson(runtime.Capture());
        CombatEquipmentRuntime restored = CombatEquipmentEditorTestFactory.Create(
            catalog,
            itemRepository,
            new CharacterCarryInventoryRegistry(),
            researchProvider: EditorAllResearchRuntimeProvider.Instance, materialCatalog: EmptyResourceEconomyContentCatalog.Instance, evolutionModules: EmptyEvolutionModuleRegistry.Instance, moduleCatalog: EmptyEquipmentModuleCatalog.Instance, itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance);
        restored.PublishRestoreCandidate(restored.BuildRestoreCandidate(
            JsonUtility.FromJson<DungeonCombatEquipmentSaveData>(json)));
        CharacterCombatLoadoutProfile restoredProfile =
            restored.GetActiveProfileSnapshot("staff:a");
        Require(restoredProfile != null
                && restoredProfile.weaponInstanceIds.Contains(weapon.instanceId)
                && restoredProfile.armorInstanceIds.Contains(armor.instanceId),
            "Equipment instances or loadout did not survive save/restore.");

        Require(restored.TryMarkLost(weapon.instanceId)
                && !restored.GetActiveProfileSnapshot("staff:a")
                    .weaponInstanceIds.Contains(weapon.instanceId)
                && restored.GetAvailableCount("weapon:dagger") == 0,
            "Lost equipment remained assigned or available.");
        return true;
    }

    private static bool VerifyEquipmentCraftQueuePersistence()
    {
        ResourceCombatEquipmentCatalog catalog = new ResourceCombatEquipmentCatalog(new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        Require(catalog.TryGet("weapon:dagger", out CombatEquipmentDefinitionSO dagger),
            "Dagger definition is missing from the combat equipment catalog.");
        float requiredWork = dagger.RequiredCraftWork;
        float completedWork = Mathf.Min(2f, requiredWork * 0.25f);
        float remainingWork = requiredWork - completedWork;
        CombatEquipmentRuntime runtime = CombatEquipmentEditorTestFactory.Create(
            catalog,
            new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore()),
            new CharacterCarryInventoryRegistry(),
            researchProvider: EditorAllResearchRuntimeProvider.Instance, materialCatalog: EmptyResourceEconomyContentCatalog.Instance, evolutionModules: EmptyEvolutionModuleRegistry.Instance, moduleCatalog: EmptyEquipmentModuleCatalog.Instance, itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance);
        runtime.PublishRestoreCandidate(runtime.BuildRestoreCandidate(
            new DungeonCombatEquipmentSaveData
        {
            craftOrders = new List<CombatEquipmentCraftOrderSaveData>
            {
                new CombatEquipmentCraftOrderSaveData
                {
                    orderId = "qa:combat-craft",
                    definitionId = "weapon:dagger",
                    requiredWork = requiredWork,
                    completedWork = completedWork,
                    materialsReady = true,
                    materialDestinationId =
                        "facility-input:qa:combat-craft",
                    qualityRoll = new CraftQualityRollSaveData
                    {
                        attemptIndex = 0,
                        randomA = -3,
                        randomB = 1,
                        randomC = 4
                    },
                    qualityStage = QualityTargetPipelineStage.Working,
                    craftWorkPerAttempt = requiredWork
                }
            }
        }));

        string json = JsonUtility.ToJson(runtime.Capture());
        CombatEquipmentRuntime restored = CombatEquipmentEditorTestFactory.Create(
            catalog,
            new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore()),
            new CharacterCarryInventoryRegistry(),
            researchProvider: EditorAllResearchRuntimeProvider.Instance, materialCatalog: EmptyResourceEconomyContentCatalog.Instance, evolutionModules: EmptyEvolutionModuleRegistry.Instance, moduleCatalog: EmptyEquipmentModuleCatalog.Instance, itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance);
        restored.PublishRestoreCandidate(restored.BuildRestoreCandidate(
            JsonUtility.FromJson<DungeonCombatEquipmentSaveData>(json)));
        Require(restored.CraftQueue.Count == 1
                && Mathf.Approximately(restored.CraftQueue[0].RemainingWork, remainingWork),
            "Craft queue remaining work did not survive save/restore.");
        Require(restored.ApplyCraftWork(
                new[] { "weapon:dagger" },
                remainingWork + 0.1f,
                out string completedEquipmentId) == 1
            && completedEquipmentId == "weapon:dagger"
            && restored.CraftQueue.Count == 0,
            "Restored craft queue did not complete through the common work-unit runtime.");
        return true;
    }

    private static bool VerifyBuildingEquipmentCraftingWork()
    {
        BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
        GameObject gameObject = new GameObject("Crafting Work Fixture");
        GameObject workerObject = new GameObject("Crafting Worker Fixture");
        try
        {
            ResourceCombatEquipmentCatalog combatCatalog =
                new ResourceCombatEquipmentCatalog(new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
            Require(combatCatalog.TryGet("weapon:dagger", out CombatEquipmentDefinitionSO dagger),
                "Dagger definition is missing from the combat equipment catalog.");
            float requiredWork = dagger.RequiredCraftWork;
            CombatEquipmentRuntime runtime = CombatEquipmentEditorTestFactory.Create(
                combatCatalog,
                new WorldItemRepository(
                    new GuidPersistentIdGenerator(),
                    new DungeonRuntimeAggregateRootStore()),
            new CharacterCarryInventoryRegistry(),
                researchProvider: EditorAllResearchRuntimeProvider.Instance, materialCatalog: EmptyResourceEconomyContentCatalog.Instance, evolutionModules: EmptyEvolutionModuleRegistry.Instance, moduleCatalog: EmptyEquipmentModuleCatalog.Instance, itemStackRuntime: UnavailableEquipmentPhysicalItemGateway.Instance);
            runtime.PublishRestoreCandidate(runtime.BuildRestoreCandidate(
                new DungeonCombatEquipmentSaveData
            {
                craftOrders = new List<CombatEquipmentCraftOrderSaveData>
                {
                    new CombatEquipmentCraftOrderSaveData
                    {
                        orderId = "qa:building-craft",
                        definitionId = "weapon:dagger",
                        requiredWork = requiredWork,
                        completedWork = 0f,
                        materialsReady = true,
                        materialDestinationId =
                            "facility-input:qa:building-craft",
                        qualityRoll = new CraftQualityRollSaveData
                        {
                            attemptIndex = 0,
                            randomA = -3,
                            randomB = 1,
                            randomC = 4
                        },
                        qualityStage = QualityTargetPipelineStage.Working,
                        craftWorkPerAttempt = requiredWork
                    }
                }
            }));
            data.id = -9811;
            data.objectName = "Test Forge";
            data.width = 1;
            data.height = 1;
            data.layer = GridLayer.Building;
            data.category = BuildingCategory.Shop;
            data.runtimeArchetype = BuildingRuntimeArchetypeKind.Generic;
            data.Facility = new FacilityData
            {
                requiredWorkers = 1
            };
            data.Facility.SetSupportedWorkTypeIds(new[]
            {
                BuiltInWorkTypeIds.Craft,
                BuiltInWorkTypeIds.Repair
            });
            data.AbilityModules.Add(new BuildingEquipmentCraftingAbility
            {
                craftableEquipmentIds = new[] { "weapon:dagger" },
                workUnitsPerCycle = requiredWork
            });

            BuildableObject building = gameObject.AddComponent<BuildableObject>();
            building.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
            BuildingAbilityRuntimeDispatcher abilityDispatcher =
                new BuildingAbilityRuntimeDispatcher(
                    new IBuildingAbilityWorkCompletedHandler[]
                    {
                        new EquipmentCraftingBuildingAbilityHandler(
                            runtime,
                            combatCatalog,
                            NeutralCharacterEnvironmentStatusQuery.Instance,
                            performance: CharacterAiEditorTestDependencies.NeutralPerformance)
                    },
                    Array.Empty<IBuildingWorkCompletionFallbackHandler>());
            building.ConstructBuildableObject(
                new BuildingResearchWorkPortAdapter(
                    new NoopBlueprintResearchWorkService()),
                new FacilityCandidateCacheStore(CharacterAiEditorTestDependencies.WorldRegistry, frameWorkBudget: null),
                new RoomFacilityPolicyService(RoomRegistry.EditorCache),
                runtime,
                abilityRuntimeDispatcher: abilityDispatcher, worldRegistry: null, worldItemStackRuntime: null, gameClock: null, paidFacilityContracts: null, evolutionState: new FacilityEvolutionStateComponentFactory());
            building.SetGrid(new Grid(4, 1));
            building.Initialization(data, new Vector2Int(1, 0));

            CharacterAiEditorTestDependencies.EnsureCharacterProgression(workerObject);
            workerObject.AddComponent<SpriteRenderer>();
            CharacterActor worker = workerObject.AddComponent<CharacterActor>();
            workerObject.AddComponent<AbilityMove>();
            workerObject.AddComponent<AbilityWork>();
            CharacterAiEditorTestDependencies.Inject(workerObject);
            worker.Identity.SetPersistentId(
                new GuidPersistentIdGenerator().NewCharacterId());
            worker.RefreshAbilityCache();
            worker.Initialization(
                OffenseEditorTestDependencies.RequireCharacterArchetype("Orc"));
            worker.characterType = CharacterType.NPC;
            worker.SetLifecycleState(CharacterLifecycleState.Active);

            Require(building.HasPendingEquipmentCraftWork(),
                "BuildableObject did not detect pending equipment craft work.");
            Require(building.GetWorkUrgency(BuiltInWorkTypeIds.Craft) > 0f,
                "Craft work did not contribute work urgency.");
            Require(ModularFacilityRuntimeEffects.ApplyWorkCompleted(
                    worker.BuildingVisitor,
                    building,
                    BuiltInWorkTypeIds.Craft) == 1
                && runtime.CraftQueue.Count == 0
                && !building.HasPendingEquipmentCraftWork(),
                "Craft work completion did not consume the authoritative work-unit order.");
            return true;
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(workerObject);
            UnityEngine.Object.DestroyImmediate(gameObject);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private static OffenseBattleCombatant Combatant(
        string id,
        string name,
        OffenseBattleTeam team,
        float health,
        float attack,
        float strength,
        float toughness,
        float dexterity,
        float moveSpeed,
        CharacterCombatAbilityDefinition ability = null,
        float? currentHealth = null,
        OffenseFormationSlot formation = OffenseFormationSlot.Front)
    {
        return new OffenseBattleCombatant(
            id,
            name,
            team.ToString(),
            team,
            new OffenseBattleStats(health, attack, strength, toughness, dexterity, moveSpeed),
            currentHealth ?? health,
            ability != null ? new[] { ability } : Array.Empty<CharacterCombatAbilityDefinition>(),
            formation: formation);
    }

    private static CombatWeaponSnapshot CreateTestRangedWeapon()
    {
        return new CombatWeaponSnapshot(
            "weapon:qa-throw",
            "equipment:qa-throw",
            CombatEquipmentKind.RecoverableThrowingWeapon,
            new RecoverableThrowVerb
            {
                attackTime = 1f,
                baseDamage = 8f,
                penetration = 2f,
                damageType = CombatDamageType.Pierce,
                tracking = 0.05f
            },
            Enum.GetValues(typeof(CombatRangeBand))
                .Cast<CombatRangeBand>()
                .Where(value => value != CombatRangeBand.OutOfRange)
                .Select(value => new CombatRangeProfile
                {
                    band = value,
                    accuracyMultiplier = 1f,
                    damageMultiplier = 1f
                })
                .ToArray(),
            20,
            CombatEquipmentQuality.Normal,
            string.Empty,
            0,
            0,
            0f,
            true,
            false,
            false);
    }

    private sealed class SingleEnemyCatalog : IEnemyArchetypeCatalog
    {
        private readonly EnemyArchetypeDefinitionSO definition;

        public SingleEnemyCatalog(EnemyArchetypeDefinitionSO definition)
        {
            this.definition = definition;
        }

        public IReadOnlyList<EnemyArchetypeDefinitionSO> All =>
            new[] { definition };

        public bool TryGet(string id, out EnemyArchetypeDefinitionSO value)
        {
            value = string.Equals(id, definition.stableId, StringComparison.Ordinal)
                ? definition
                : null;
            return value != null;
        }

        public EnemyArchetypeDefinitionSO Require(string id) =>
            TryGet(id, out EnemyArchetypeDefinitionSO value)
                ? value
                : throw new KeyNotFoundException(id);
    }

    private static void Run(string name, Func<bool> scenario, ICollection<string> errors)
    {
        try
        {
            if (!scenario()) errors.Add(name);
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            errors.Add($"{name}: {exception.Message}");
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static CombatAttackResult Hit(
        CombatBodyPart bodyPart,
        float damage,
        float bleeding,
        float suppression)
    {
        return new CombatAttackResult(
            executed: true,
            hit: true,
            coverBlocked: false,
            evaded: false,
            bodyPart: bodyPart,
            rawDamage: damage,
            appliedDamage: damage,
            bleeding: bleeding,
            suppression: suppression,
            armorDurabilityDamage: 0f,
            armorInstanceId: string.Empty,
            failureReason: string.Empty);
    }

    private sealed class FixedCombatResolutionService : ICombatResolutionService
    {
        private readonly CombatAttackResult result;

        public FixedCombatResolutionService(CombatAttackResult result)
        {
            this.result = result;
        }

        public CombatAttackResult Resolve(CombatAttackRequest request)
        {
            return result;
        }

        public CombatAttackPreview Preview(CombatAttackRequest request)
        {
            return new CombatAttackPreview(
                result.Executed,
                result.FailureReason,
                CombatRangeRules.GetBand(request.Distance),
                result.Hit ? 1f : 0f,
                result.CoverBlocked ? 1f : 0f,
                result.ShieldBlocked ? 1f : 0f,
                result.Evaded ? 1f : 0f,
                result.AppliedDamage,
                result.AppliedDamage);
        }

        public float CalculateAttackInterval(
            CombatStatSnapshot attacker,
            CombatWeaponSnapshot weapon,
            CombatFireMode mode)
        {
            return 1f;
        }

        public float CalculateReloadTime(
            CombatStatSnapshot actor,
            CombatWeaponSnapshot weapon)
        {
            return 1f;
        }

        public float CalculateWeaponSwitchTime(
            CombatStatSnapshot actor,
            float weaponWeight)
        {
            return 1f;
        }
    }

    private sealed class NoopBlueprintResearchWorkService : IBlueprintResearchWorkService
    {
        public bool HasResearchWorkFor(BuildableObject facility)
        {
            return false;
        }

        public BlueprintResearchWorkResult ApplyResearchWork(
            CharacterActor researcher,
            BuildableObject researchFacility,
            float seconds)
        {
            return new BlueprintResearchWorkResult(false, null, 0f, 0f, 1f, false, "No research runtime.");
        }

        public BlueprintResearchWorkResult ApplyApprovedResearchWork(
            CharacterActor researcher,
            BuildableObject researchFacility,
            float approvedWorkUnits) =>
            ApplyResearchWork(researcher, researchFacility, approvedWorkUnits);
    }

    private sealed class NoopWorldInfoClickSelector : IWorldInfoClickSelector
    {
        public bool TryHandleWorldInfoClick() => false;
        public bool TryTriggerCharacterUnderPointer() => false;

        public bool TryGetPreferredCharacterUnderPointer(out CharacterActor actor)
        {
            actor = null;
            return false;
        }

        public bool TryGetPreferredCharacterAtScreenPosition(
            Vector3 screenPosition,
            Camera camera,
            out CharacterActor actor)
        {
            actor = null;
            return false;
        }

        public bool TryGetPreferredCharacter(Collider2D[] hits, out CharacterActor actor)
        {
            actor = null;
            return false;
        }
    }
}
