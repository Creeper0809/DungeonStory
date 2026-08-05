#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class CharacterSkillRuntimePlayModeVerifier
{
    [MenuItem("DungeonStory/Debug/QA/Run Character Skill Runtime PlayMode Probe")]
    public static void RunFromMenu()
    {
        try
        {
            Debug.Log(RunProbe());
        }
        catch (Exception exception)
        {
            Debug.LogError($"Character skill runtime PlayMode probe failed: {exception}");
        }
    }

    public static string RunProbe()
    {
        Require(Application.isPlaying, "The skill runtime probe requires PlayMode.");
        CharacterActor[] actors = CharacterActorCollection.DistinctByGameObject(
                UnityEngine.Object.FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None))
            .Where(actor => actor != null && !actor.IsDead && actor.Progression != null)
            .Take(3)
            .ToArray();
        Require(actors.Length >= 2, "At least two active characters are required.");

        Dictionary<CharacterActor, CharacterProgressionSnapshot> snapshots = actors
            .ToDictionary(actor => actor, actor => actor.Progression.CapturePersistentState());
        CharacterActor defender = actors[0];
        CharacterActor intruder = actors[1];
        float intruderHealthBefore = intruder.Stats.CurrentHealth;
        EventAlertRuntime alerts = UnityEngine.Object.FindFirstObjectByType<EventAlertRuntime>();
        Require(alerts != null, "EventAlertRuntime is missing.");
        DungeonRuntimeLifetimeScope scope =
            UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include);
        Require(scope != null && scope.Container != null, "Gameplay lifetime scope is missing.");
        IGameEventBus gameEventBus = scope.Container.Resolve<IGameEventBus>();

        try
        {
            foreach (CharacterActor actor in actors)
            {
                actor.Progression.GrowthState.ultimate = null;
                actor.Progression.GrowthState.useLimits.managementOperatingDay = -1;
                actor.Progression.GrowthState.useLimits.defenseInvasionSerial = -1;
            }

            CharacterSkillInstance management = CreateUltimate(
                "qa-management-runtime",
                "QA Daybreak",
                CharacterUltimateDomain.Management,
                CharacterSkillTrigger.OperatingDayStarted,
                CharacterSkillTarget.Dungeon,
                "output",
                "large");
            defender.Progression.GrowthState.ultimate = management;
            int managementAlertsBefore = CountAlerts(alerts, management.displayName);
            gameEventBus.Publish(new OperatingDayStartedEvent(7001));
            int firstManagementCount = CountAlerts(alerts, management.displayName);
            Require(defender.Progression.GrowthState.useLimits.managementOperatingDay == 7001,
                "Management ultimate did not mark the operating day.");
            Require(Mathf.Approximately(
                    CharacterSkillRuntimeEffects.GetProductionOutputMultiplier(defender),
                    1.3f),
                "Management ultimate did not activate its real output modifier.");
            Require(firstManagementCount == managementAlertsBefore + 1,
                "Management ultimate did not publish exactly one player event.");

            gameEventBus.Publish(new OperatingDayStartedEvent(7001));
            Require(CountAlerts(alerts, management.displayName) == firstManagementCount,
                "Management ultimate fired twice during one operating day.");
            gameEventBus.Publish(new OperatingDayStartedEvent(7002));
            Require(defender.Progression.GrowthState.useLimits.managementOperatingDay == 7002
                && CountAlerts(alerts, management.displayName) == firstManagementCount + 1,
                "Management ultimate did not reset on the next operating day.");

            CharacterSkillInstance defense = CreateUltimate(
                "qa-defense-runtime",
                "QA Bastion",
                CharacterUltimateDomain.Defense,
                CharacterSkillTrigger.InvasionStarted,
                CharacterSkillTarget.Enemy,
                "damage",
                "light");
            defender.Progression.GrowthState.ultimate = defense;
            defender.Progression.GrowthState.useLimits.defenseInvasionSerial = -1;
            int defenseAlertsBefore = CountAlerts(alerts, defense.displayName);
            gameEventBus.Publish(
                new InvasionSpawnedEvent(
                    intruder,
                    new InvasionThreatSnapshot(
                        100f,
                        InvasionThreatStage.Candidate,
                        new InvasionThreatFactors(1f, 1f, 1f, 1f),
                        0f,
                        0f)));
            Require(intruder.Stats.CurrentHealth < intruderHealthBefore,
                "Defense ultimate did not damage the spawned intruder.");
            Require(defender.Progression.GrowthState.useLimits.defenseInvasionSerial >= 0,
                "Defense ultimate did not consume its invasion use.");
            Require(CountAlerts(alerts, defense.displayName) == defenseAlertsBefore + 1,
                "Defense ultimate did not publish its player event.");

            VerifyOffenseUltimateCommand();
            return $"CHARACTER_SKILL_RUNTIME_PLAYMODE_PASS; managementDay={defender.Progression.GrowthState.useLimits.managementOperatingDay}; "
                + $"defenseSerial={defender.Progression.GrowthState.useLimits.defenseInvasionSerial}; "
                + $"intruderHealth={intruderHealthBefore:0.##}->{intruder.Stats.CurrentHealth:0.##}; offenseCooldown=999";
        }
        finally
        {
            foreach (KeyValuePair<CharacterActor, CharacterProgressionSnapshot> entry in snapshots)
            {
                if (entry.Key != null)
                {
                    entry.Key.Progression.RestorePersistentState(entry.Value);
                }
            }

            if (intruder != null && intruder.Stats.CurrentHealth < intruderHealthBefore)
            {
                intruder.Stats.Heal(intruderHealthBefore - intruder.Stats.CurrentHealth);
            }
        }
    }

    private static void VerifyOffenseUltimateCommand()
    {
        CharacterSkillInstance skill = CreateUltimate(
            "qa-offense-runtime",
            "QA Finale",
            CharacterUltimateDomain.Offense,
            CharacterSkillTrigger.ManualCombat,
            CharacterSkillTarget.Enemy,
            "damage",
            "standard");
        CharacterCombatAbilityDefinition ability = CharacterSkillRuntimeEffects.ToCombatAbility(
            skill,
            EditorCharacterSkillSettingsFactory.CreateTransientDefaults());
        Require(ability != null && ability.CooldownTurns == 999,
            "Offense ultimate was not converted to a once-per-battle command.");

        OffenseBattleCombatant ally = new OffenseBattleCombatant(
            "qa:ultimate:ally",
            "Ultimate User",
            "Slime",
            OffenseBattleTeam.Allies,
            new OffenseBattleStats(100f, 12f, 8f, 6f, 20f, 8f),
            100f,
            new[] { ability });
        OffenseBattleCombatant enemy = new OffenseBattleCombatant(
            "qa:ultimate:enemy",
            "Ultimate Target",
            "Enemy",
            OffenseBattleTeam.Enemies,
            new OffenseBattleStats(300f, 6f, 6f, 6f, 2f, 2f),
            300f);
        OffenseBattleSession session = new OffenseBattleSession(
            Guid.NewGuid().ToString("N"),
            "qa:expedition",
            "qa:target",
            "QA Ultimate Battle",
            DungeonDifficulty.Normal,
            new[] { ally, enemy }, combatResolution: null, combatEquipmentRuntime: null);
        float healthBefore = enemy.CurrentHealth;
        Require(session.TryExecuteCommand(
                new OffenseBattleCommand(
                    1,
                    ally.PersistentId,
                    OffenseBattleActionType.Ability,
                    enemy.PersistentId,
                    ability.Id),
                out OffenseBattleCommandResult first)
            && first.Accepted
            && enemy.CurrentHealth < healthBefore
            && ally.GetCooldown(ability.Id) == 999,
            "The direct offense ultimate command did not deal damage or start its cooldown.");
        Require(session.TryExecuteCommand(
                new OffenseBattleCommand(
                    2,
                    enemy.PersistentId,
                    OffenseBattleActionType.Guard,
                    enemy.PersistentId),
                out _),
            "The enemy turn could not advance the ultimate probe.");
        Require(!session.TryExecuteCommand(
                new OffenseBattleCommand(
                    3,
                    ally.PersistentId,
                    OffenseBattleActionType.Ability,
                    enemy.PersistentId,
                    ability.Id),
                out _),
            "The offense ultimate was reusable in the same battle.");
    }

    private static CharacterSkillInstance CreateUltimate(
        string id,
        string name,
        CharacterUltimateDomain domain,
        CharacterSkillTrigger trigger,
        CharacterSkillTarget target,
        string moduleId,
        string variantId)
    {
        return new CharacterSkillInstance
        {
            id = id,
            displayName = name,
            description = "Runtime verification ultimate.",
            narrativeReason = "Runtime verification milestone.",
            kind = CharacterSkillKind.Ultimate,
            rarity = CharacterSkillRarity.Legendary,
            ultimateDomain = domain,
            trigger = trigger,
            target = target,
            modules = new List<CharacterSkillModuleSelection>
            {
                new CharacterSkillModuleSelection { moduleId = moduleId, variantId = variantId }
            }
        };
    }

    private static int CountAlerts(EventAlertRuntime runtime, string title)
    {
        return runtime.EventLog
            .Where(record => record != null && string.Equals(record.Title, title, StringComparison.Ordinal))
            .Sum(record => record.Count);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
