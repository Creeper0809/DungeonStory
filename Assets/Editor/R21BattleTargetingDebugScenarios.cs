#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Object = UnityEngine.Object;

public static class R21BattleTargetingDebugScenarios
{
    private static readonly MethodInfo Project = typeof(EnemyEncounterFactory)
        .GetMethod("ProjectAbility", BindingFlags.NonPublic | BindingFlags.Static);

    public static void RunAll()
    {
        var content = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
        var definitions = content.GetAll<EnemyAbilityDefinitionSO>().ToDictionary(value => value.stableId);
        Require(definitions.Count > 0, "The authored enemy ability input is empty.");
        foreach (var definition in definitions.Values)
        {
            Require(definition.ValidateDefinition().Count == 0, definition.stableId + " is invalid.");
            Require(Ability(definition).IsValid, definition.stableId + " did not project valid targeting.");
        }
        var equipment = OffenseEditorTestDependencies.CreateCombatEquipmentRuntime();
        VerifyHealingAndRetry(definitions, equipment, content);
        VerifyScope(definitions, equipment, "rally", OffenseBattleStatusType.Guard, sameFormationOnly: false);
        VerifyScope(definitions, equipment, "poison-cloud", OffenseBattleStatusType.DamageOverTime, sameFormationOnly: true);
        VerifyLiveFilters(definitions, equipment, content);
        VerifyHighestPower(definitions, equipment, content);
        Debug.Log($"R21_BATTLE_TARGETS=PASS; authoredProjection={definitions.Count}; actualAI; commands; rejected-retry; fresh-json; duplicate; team-and-formation-scope");
    }

    private static void VerifyHealingAndRetry(Dictionary<string, EnemyAbilityDefinitionSO> definitions,
        ICombatEquipmentRuntime equipment, IGameContentDefinitionSource content)
    {
        var repair = Ability(definitions["enemy-ability:core-repair"]);
        var caster = Unit("healer", OffenseBattleTeam.Enemies, abilities: new[] { repair }, speed: 100f);
        var human = Unit("human", OffenseBattleTeam.Enemies, health: 10f);
        var golem = Unit("golem", OffenseBattleTeam.Enemies, "Golem", health: 50f);
        var opponent = Unit("opponent", OffenseBattleTeam.Allies);
        var session = Session(equipment, caster, human, golem, opponent);
        Require(session.GetAbilityTargets(caster, repair).SequenceEqual(new[] { golem }), "Construct healing selected a nonconstruct.");
        string before = Json(session);
        Require(!session.TryExecuteCommand(Command(caster, human, repair), out _), "A human received construct repair.");
        Require(Json(session) == before, "Rejected repair changed battle state or consumed the command.");
        var decision = Decide(session, caster, content);
        Require(decision.Intent == EnemyTacticalIntentKind.UseAbility && decision.TargetId == golem.PersistentId,
            "The real tactical service did not select the injured construct.");
        Require(session.TryExecuteCommand(Command(caster, golem, repair), out _), "Repair retry failed.");
        Require(golem.CurrentHealth > 50f && human.CurrentHealth == 10f, "Repair affected the wrong health owner.");
        Require(caster.GetCooldown(repair.Id) == repair.CooldownTurns, "Repair cooldown did not commit once.");
        var restored = RoundTrip(session, equipment);
        string restoredBefore = Json(restored);
        Require(!restored.TryExecutePlannedCommand(Command(caster, golem, repair), out _), "Restored command executed twice.");
        Require(Json(restored) == restoredBefore, "A duplicate changed restored repair state.");
        Require(restored.FindCombatant(golem.PersistentId).CurrentHealth == golem.CurrentHealth,
            "Fresh JSON restore lost repaired health.");
    }

    private static void VerifyScope(Dictionary<string, EnemyAbilityDefinitionSO> definitions,
        ICombatEquipmentRuntime equipment, string abilityId, OffenseBattleStatusType status, bool sameFormationOnly)
    {
        var ability = Ability(definitions["enemy-ability:" + abilityId]);
        var sourceTeam = sameFormationOnly ? OffenseBattleTeam.Enemies : OffenseBattleTeam.Allies;
        var targetTeam = OffenseBattleTeam.Allies;
        var caster = Unit("scope-source", sourceTeam, abilities: new[] { ability }, speed: 100f);
        var first = Unit("scope-first", targetTeam);
        var second = Unit("scope-second", targetTeam);
        var rear = Unit("scope-rear", targetTeam, formation: OffenseFormationSlot.Rear);
        var opposition = Unit("scope-opposition", OffenseBattleTeam.Enemies);
        var session = Session(equipment, caster, first, second, rear, opposition);
        Require(session.TryExecuteCommand(Command(caster, first, ability), out _), "Scoped ability failed.");
        Require(first.Statuses.Count(value => value.Type == status) == 1
            && second.Statuses.Count(value => value.Type == status) == 1, "A scoped target was skipped or applied twice.");
        Require(rear.Statuses.Any(value => value.Type == status) == !sameFormationOnly, "Scope crossed the wrong formation boundary.");
        Require(!opposition.Statuses.Any(value => value.Type == status), "Scope crossed a team boundary.");
        var restored = RoundTrip(session, equipment);
        string before = Json(restored);
        Require(!restored.TryExecutePlannedCommand(Command(caster, first, ability), out _), "Restored scoped command executed twice.");
        Require(Json(restored) == before, "Scoped retry changed restored status/cooldown state.");
    }

    private static void VerifyLiveFilters(Dictionary<string, EnemyAbilityDefinitionSO> definitions,
        ICombatEquipmentRuntime equipment, IGameContentDefinitionSource content)
    {
        var caster = Unit("filters-source", OffenseBattleTeam.Enemies, speed: 100f);
        var front = Unit("filters-front", OffenseBattleTeam.Allies);
        var rear = Unit("filters-rear", OffenseBattleTeam.Allies, formation: OffenseFormationSlot.Rear);
        var wounded = Unit("filters-wounded", OffenseBattleTeam.Allies, health: 30f);
        var fast = Unit("filters-fast", OffenseBattleTeam.Allies, speed: 7f);
        var arcane = Unit("filters-arcane", OffenseBattleTeam.Allies);
        arcane.SetArcanePowerMultiplier(1.4f);
        var armored = Unit("filters-armored", OffenseBattleTeam.Allies);
        var armorDefinition = content.GetAll<CombatArmorSO>().First();
        var part = armorDefinition.BodyPartDefense.First();
        armored.SetCombatEquipment(CombatWeaponSnapshot.CreateUnarmed(), new[]
        {
            new CombatArmorSnapshot("equipment:r21-armor", part.bodyPart, armorDefinition.Layer,
                CombatEquipmentQuality.Normal, 1f, part.slashDefense, part.pierceDefense, part.bluntDefense,
                definitionId: armorDefinition.EquipmentId, roleFlags: armorDefinition.RoleFlags)
        });
        var retreat = Unit("filters-retreat", OffenseBattleTeam.Enemies, health: 10f, retreat: .2f);
        var session = Session(equipment, caster, front, rear, wounded, fast, arcane, armored, retreat);
        Require(session.GetAbilityTargets(caster, Ability(definitions["enemy-ability:hook-pull"])).SequenceEqual(new[] { rear }), "Backline filter failed.");
        Require(session.GetAbilityTargets(caster, Ability(definitions["enemy-ability:armor-break"])).SequenceEqual(new[] { armored }), "Armor filter failed.");
        Require(session.GetAbilityTargets(caster, Ability(definitions["enemy-ability:frost-bind"])).SequenceEqual(new[] { fast }), "Fast filter failed.");
        Require(session.GetAbilityTargets(caster, Ability(definitions["enemy-ability:arcane-null"])).SequenceEqual(new[] { arcane }), "Arcane filter failed.");
        Require(session.GetAbilityTargets(caster, Ability(definitions["enemy-ability:blood-drain"])).SequenceEqual(new[] { wounded }), "Wounded filter failed.");
        Require(session.GetAbilityTargets(caster, Ability(definitions["enemy-ability:retreat-cover"])).SequenceEqual(new[] { retreat }), "Retreat eligibility did not use live health and the authored retreat threshold.");
        Require(session.GetAbilityTargets(caster, Ability(definitions["enemy-ability:charge"])).Contains(front), "Front filter lost a front combatant.");
        Require(!session.GetAbilityTargets(caster, Ability(definitions["enemy-ability:charge"])).Contains(rear), "Front filter accepted a rear combatant.");
    }

    private static void VerifyHighestPower(Dictionary<string, EnemyAbilityDefinitionSO> definitions,
        ICombatEquipmentRuntime equipment, IGameContentDefinitionSource content)
    {
        var ward = Ability(definitions["enemy-ability:rune-ward"]);
        var caster = Unit("power-source", OffenseBattleTeam.Enemies, abilities: new[] { ward }, speed: 100f, attack: 1f);
        var weak = Unit("power-weak", OffenseBattleTeam.Enemies, health: 20f, attack: 2f);
        var strong = Unit("power-strong", OffenseBattleTeam.Enemies, health: 80f, attack: 30f);
        var opponent = Unit("power-opponent", OffenseBattleTeam.Allies);
        var session = Session(equipment, caster, weak, strong, opponent);
        var decision = Decide(session, caster, content);
        Require(decision.Intent == EnemyTacticalIntentKind.UseAbility && decision.TargetId == strong.PersistentId,
            "Priority ally was replaced by the lowest-health ally.");
        Require(session.TryExecuteCommand(Command(caster, strong, ward), out _), "Priority guard did not execute.");
        Require(strong.Statuses.Any(value => value.Type == OffenseBattleStatusType.Guard)
            && !weak.Statuses.Any(value => value.Type == OffenseBattleStatusType.Guard), "Priority guard affected the wrong ally.");
    }

    private static EnemyTacticalDecision Decide(OffenseBattleSession session, OffenseBattleCombatant caster,
        IGameContentDefinitionSource content)
    {
        var archetype = Object.Instantiate(content.GetAll<EnemyArchetypeDefinitionSO>().First(value => value.bossPhases.Count == 0));
        try
        {
            archetype.tacticalProfile.attackWeight = 0f;
            archetype.tacticalProfile.protectWeight = 0f;
            archetype.tacticalProfile.retreatWeight = 0f;
            archetype.tacticalProfile.abilityWeight = 100f;
            return new EnemyTacticalDecisionService(new SingleArchetype(archetype)).Decide(session,
                new EnemyIndividualSaveData { characterId = caster.PersistentId, enemyArchetypeId = archetype.stableId });
        }
        finally { Object.DestroyImmediate(archetype); }
    }

    private static CharacterCombatAbilityDefinition Ability(EnemyAbilityDefinitionSO definition) =>
        (CharacterCombatAbilityDefinition)(Project ?? throw new InvalidOperationException("Missing production projector."))
            .Invoke(null, new object[] { definition });

    private static OffenseBattleCombatant Unit(string id, OffenseBattleTeam team, string species = "Human",
        float health = 100f, float speed = 1f, float attack = 5f, CharacterCombatAbilityDefinition[] abilities = null,
        OffenseFormationSlot formation = OffenseFormationSlot.Front, float retreat = 0f) =>
        new("character:r21:" + id, id, species, team,
            new OffenseBattleStats(100f, attack, attack, 2f, speed, speed), health,
            abilities, formation: formation, retreatHealthFraction: retreat);

    private static OffenseBattleSession Session(ICombatEquipmentRuntime equipment, params OffenseBattleCombatant[] values) =>
        new("battle:r21", "expedition:r21", "target:r21", "R21", DungeonDifficulty.Normal,
            values, OffenseEditorTestDependencies.CreateCombatResolution(), equipment);

    private static OffenseBattleCommand Command(OffenseBattleCombatant caster, OffenseBattleCombatant target,
        CharacterCombatAbilityDefinition ability) => new(1, caster.PersistentId, OffenseBattleActionType.Ability,
            target.PersistentId, ability.Id);
    private static string Json(OffenseBattleSession session) => JsonUtility.ToJson(session.CapturePersistentState());

    private static OffenseBattleSession RoundTrip(OffenseBattleSession source, ICombatEquipmentRuntime equipment)
    {
        var saved = JsonUtility.FromJson<OffenseBattlePersistenceState>(Json(source));
        var fresh = source.Combatants.Select(value =>
        {
            var clone = new OffenseBattleCombatant(value.PersistentId, value.DisplayName, value.SpeciesTag,
                value.Team, value.Stats, value.Stats.MaxHealth, value.Abilities, formation: value.Formation,
                retreatHealthFraction: value.RetreatHealthFraction);
            clone.SetCombatEquipment(value.Weapon, value.Armor, value.Shield);
            return clone;
        }).ToArray();
        return OffenseBattleSession.Restore(saved, fresh, OffenseEditorTestDependencies.CreateCombatResolution(), equipment);
    }

    private sealed class SingleArchetype : IEnemyArchetypeCatalog
    {
        private readonly EnemyArchetypeDefinitionSO value;
        public SingleArchetype(EnemyArchetypeDefinitionSO value) => this.value = value;
        public IReadOnlyList<EnemyArchetypeDefinitionSO> All => new[] { value };
        public bool TryGet(string id, out EnemyArchetypeDefinitionSO definition)
        { definition = id == value.stableId ? value : null; return definition != null; }
        public EnemyArchetypeDefinitionSO Require(string id) => TryGet(id, out var found)
            ? found : throw new InvalidOperationException("Unknown R21 tactical archetype.");
    }
    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidOperationException(reason); }
}
#endif
