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
        Run("enemy target priority", VerifyEnemyTargetPriority, errors);
        Run("death retreat and command idempotence", VerifyOutcomesAndIdempotence, errors);
        Run("exact battle persistence", VerifyExactPersistence, errors);
        Run("fixed difficulty multipliers", VerifyDifficultyMultipliers, errors);
        Run("formation constraints", VerifyFormationConstraints, errors);
        Run("body injury and persistence", VerifyBodyInjuryAndPersistence, errors);
        Run("heavy suppression skips turn", VerifyHeavySuppressionSkipsTurn, errors);
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
                Hit(CombatBodyPart.Torso, damage: 12f, bleeding: 0f, suppression: 0f)));
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
            new[] { restoredAlly, restoredEnemy });

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
        OffenseTargetDefinition target = new OffenseTargetDefinition
        {
            id = "difficulty-test",
            title = "Difficulty",
            campaignOrder = 1,
            requiredMembers = 1
        };
        OffenseBattleCombatant easy = OffenseEncounterCatalog.CreateEnemies(target, DungeonDifficulty.Easy).Single();
        OffenseBattleCombatant normal = OffenseEncounterCatalog.CreateEnemies(target, DungeonDifficulty.Normal).Single();
        OffenseBattleCombatant hard = OffenseEncounterCatalog.CreateEnemies(target, DungeonDifficulty.Hard).Single();
        Require(Mathf.Approximately(easy.Stats.MaxHealth, normal.Stats.MaxHealth * 0.8f),
            "Easy enemy health multiplier is incorrect.");
        Require(Mathf.Approximately(hard.Stats.MaxHealth, normal.Stats.MaxHealth * 1.25f),
            "Hard enemy health multiplier is incorrect.");
        Require(Mathf.Approximately(hard.Stats.Attack, normal.Stats.Attack * 1.2f),
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
            resolver);
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
            resolver);
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
                Hit(CombatBodyPart.Torso, damage: 1f, bleeding: 0f, suppression: 80f)));

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

    private static OffenseBattleSession Session(params OffenseBattleCombatant[] combatants)
    {
        return new OffenseBattleSession(
            Guid.NewGuid().ToString("N"),
            "expedition:test",
            "target:test",
            "Test",
            DungeonDifficulty.Normal,
            combatants);
    }

    private static bool VerifyEquipmentReservation()
    {
        ResourceCombatEquipmentCatalog catalog = new ResourceCombatEquipmentCatalog();
        CombatEquipmentRuntime runtime = new CombatEquipmentRuntime(catalog);
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
        CombatEquipmentRuntime restored = new CombatEquipmentRuntime(catalog);
        restored.Restore(JsonUtility.FromJson<DungeonCombatEquipmentSaveData>(json));
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
        ResourceCombatEquipmentCatalog catalog = new ResourceCombatEquipmentCatalog();
        CombatEquipmentRuntime runtime = new CombatEquipmentRuntime(catalog);
        runtime.Restore(new DungeonCombatEquipmentSaveData
        {
            craftOrders = new List<CombatEquipmentCraftOrderSaveData>
            {
                new CombatEquipmentCraftOrderSaveData
                {
                    orderId = "qa:combat-craft",
                    definitionId = "weapon:dagger",
                    requiredWork = 6f,
                    completedWork = 2f,
                    materialsReady = true
                }
            }
        });

        string json = JsonUtility.ToJson(runtime.Capture());
        CombatEquipmentRuntime restored = new CombatEquipmentRuntime(catalog);
        restored.Restore(JsonUtility.FromJson<DungeonCombatEquipmentSaveData>(json));
        Require(restored.CraftQueue.Count == 1
                && Mathf.Approximately(restored.CraftQueue[0].RemainingWork, 4f),
            "Craft queue remaining work did not survive save/restore.");
        Require(restored.ApplyCraftWork(
                new[] { "weapon:dagger" },
                4.1f,
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
        try
        {
            ResourceCombatEquipmentCatalog combatCatalog =
                new ResourceCombatEquipmentCatalog();
            CombatEquipmentRuntime runtime = new CombatEquipmentRuntime(combatCatalog);
            runtime.Restore(new DungeonCombatEquipmentSaveData
            {
                craftOrders = new List<CombatEquipmentCraftOrderSaveData>
                {
                    new CombatEquipmentCraftOrderSaveData
                    {
                        orderId = "qa:building-craft",
                        definitionId = "weapon:dagger",
                        requiredWork = 6f,
                        completedWork = 0f,
                        materialsReady = true
                    }
                }
            });
            data.id = -9811;
            data.objectName = "Test Forge";
            data.width = 1;
            data.height = 1;
            data.layer = GridLayer.Building;
            data.category = BuildingCategory.Shop;
            data.type = typeof(BuildableObject);
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
                workUnitsPerCycle = 6f
            });

            BuildableObject building = gameObject.AddComponent<BuildableObject>();
            BuildingAbilityRuntimeDispatcher abilityDispatcher =
                new BuildingAbilityRuntimeDispatcher(
                    new IBuildingAbilityWorkCompletedHandler[]
                    {
                        new EquipmentCraftingBuildingAbilityHandler(
                            runtime,
                            combatCatalog)
                    },
                    Array.Empty<IBuildingWorkCompletionFallbackHandler>());
            building.ConstructBuildableObject(
                new NoopBlueprintResearchWorkService(),
                new NoopWorldInfoClickSelector(),
                new FacilityCandidateCacheStore(CharacterAiEditorTestDependencies.WorldRegistry),
                new RoomFacilityPolicyService(RoomRegistry.EditorCache),
                runtime,
                abilityRuntimeDispatcher: abilityDispatcher);
            building.SetGrid(new Grid(4, 1));
            building.Initialization(data, new Vector2Int(1, 0));

            Require(building.HasPendingEquipmentCraftWork(),
                "BuildableObject did not detect pending equipment craft work.");
            Require(building.GetWorkUrgency(BuiltInWorkTypeIds.Craft) > 0f,
                "Craft work did not contribute work urgency.");
            Require(ModularFacilityRuntimeEffects.ApplyWorkCompleted(
                    null,
                    building,
                    BuiltInWorkTypeIds.Craft) == 1
                && runtime.CraftQueue.Count == 0
                && !building.HasPendingEquipmentCraftWork(),
                "Craft work completion did not consume the common work-unit order.");
            return true;
        }
        finally
        {
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
