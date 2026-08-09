using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class EnemyEncounterComposition
{
    public EnemyEncounterComposition(
        OffenseEncounterSO encounter,
        IReadOnlyList<OffenseBattleCombatant> combatants,
        IReadOnlyList<EnemyIndividualSaveData> individuals,
        OffenseBattleEncounterRules rules)
    {
        Encounter = encounter ?? throw new ArgumentNullException(nameof(encounter));
        Combatants = combatants ?? throw new ArgumentNullException(nameof(combatants));
        Individuals = individuals ?? throw new ArgumentNullException(nameof(individuals));
        Rules = rules ?? throw new ArgumentNullException(nameof(rules));
    }

    public OffenseEncounterSO Encounter { get; }
    public IReadOnlyList<OffenseBattleCombatant> Combatants { get; }
    public IReadOnlyList<EnemyIndividualSaveData> Individuals { get; }
    public OffenseBattleEncounterRules Rules { get; }
}

public interface IEnemyEncounterFactory
{
    EnemyEncounterComposition Create(
        OffenseTargetDefinition target,
        DungeonDifficulty difficulty,
        string encounterContext,
        OffenseRouteNode routeNode = null,
        OffenseStrategicPressureSnapshot pressure = default);

    EnemyEncounterComposition Restore(
        string encounterId,
        IEnumerable<EnemyIndividualSaveData> individuals,
        DungeonDifficulty difficulty,
        OffenseRouteNode routeNode = null,
        OffenseStrategicPressureSnapshot pressure = default);

    string GetSummary(OffenseTargetDefinition target, string context);
}

public sealed class EnemyEncounterFactory : IEnemyEncounterFactory
{
    private readonly IEnemyArchetypeCatalog archetypes;
    private readonly IEncounterCatalog encounters;
    private readonly IEnemyAbilityCatalog abilities;
    private readonly IBattlefieldModifierCatalog battlefieldModifiers;
    private readonly IEnemyIndividualFactory individuals;
    private readonly IMilestoneGameplayModifierQuery milestoneModifiers;
    private readonly ICombatEquipmentRuntime combatEquipment;

    public EnemyEncounterFactory(
        IEnemyArchetypeCatalog archetypes,
        IEncounterCatalog encounters,
        IEnemyAbilityCatalog abilities,
        IBattlefieldModifierCatalog battlefieldModifiers,
        IEnemyIndividualFactory individuals,
        IMilestoneGameplayModifierQuery milestoneModifiers = null,
        ICombatEquipmentRuntime combatEquipment = null)
    {
        this.archetypes = archetypes ?? throw new ArgumentNullException(nameof(archetypes));
        this.encounters = encounters ?? throw new ArgumentNullException(nameof(encounters));
        this.abilities = abilities ?? throw new ArgumentNullException(nameof(abilities));
        this.battlefieldModifiers = battlefieldModifiers
            ?? throw new ArgumentNullException(nameof(battlefieldModifiers));
        this.individuals = individuals ?? throw new ArgumentNullException(nameof(individuals));
        this.milestoneModifiers = milestoneModifiers
            ?? NeutralMilestoneGameplayModifierQuery.Instance;
        this.combatEquipment = combatEquipment;
    }

    public EnemyEncounterComposition Create(
        OffenseTargetDefinition target,
        DungeonDifficulty difficulty,
        string encounterContext,
        OffenseRouteNode routeNode = null,
        OffenseStrategicPressureSnapshot pressure = default)
    {
        if (target == null) throw new ArgumentNullException(nameof(target));
        string context = Normalize(encounterContext);
        if (context.Length == 0)
            throw new ArgumentException("Encounter context is required.", nameof(encounterContext));

        OffenseEncounterSO encounter = SelectEncounter(target, context, routeNode);
        List<EnemyIndividualSaveData> created = new();
        int sequence = 0;
        foreach (OffenseEnemyArchetypeEntry entry in encounter.enemies)
        {
            int count = DeterministicCount(entry, context, sequence);
            for (int index = 0; index < count; index++)
            {
                CharacterId id = CharacterId.FromStableSuffix(
                    $"battle:{context}:{encounter.encounterId}:{sequence++}:{index}");
                created.Add(individuals.Create(
                    entry.enemyArchetypeId,
                    id,
                    $"battle:{context}:{encounter.encounterId}"));
            }
        }

        int maximum = routeNode == null || routeNode.IsBoss
            ? created.Count
            : Math.Min(created.Count, Math.Max(1, routeNode.Depth + 1));
        if (!target.revealsTruth && pressure.Manpower >= 40f)
        {
            maximum = Mathf.Clamp(
                Mathf.CeilToInt(maximum * Mathf.Clamp(
                    1f - pressure.Manpower * 0.005f,
                    0.5f,
                    1f)),
                1,
                maximum);
        }

        return Build(
            encounter,
            created.Take(maximum),
            difficulty,
            routeNode,
            pressure);
    }

    public EnemyEncounterComposition Restore(
        string encounterId,
        IEnumerable<EnemyIndividualSaveData> savedIndividuals,
        DungeonDifficulty difficulty,
        OffenseRouteNode routeNode = null,
        OffenseStrategicPressureSnapshot pressure = default)
    {
        OffenseEncounterSO encounter = encounters.Require(encounterId);
        EnemyIndividualSaveData[] restored = (savedIndividuals
                ?? Array.Empty<EnemyIndividualSaveData>())
            .Select(value => individuals.RequireBlueprint(value).SaveData)
            .ToArray();
        if (restored.Length == 0)
            throw new InvalidOperationException(
                "A persisted encounter requires enemy individuals.");

        HashSet<string> allowed = encounter.enemies
            .Select(value => value.enemyArchetypeId)
            .ToHashSet(StringComparer.Ordinal);
        if (restored.Any(value => !allowed.Contains(value.enemyArchetypeId)))
            throw new InvalidOperationException(
                $"Encounter '{encounterId}' contains an unauthored enemy individual.");

        return Build(encounter, restored, difficulty, routeNode, pressure);
    }

    public string GetSummary(OffenseTargetDefinition target, string context)
    {
        OffenseEncounterSO encounter = SelectEncounter(
            target ?? throw new ArgumentNullException(nameof(target)),
            Normalize(context),
            null);
        if (!milestoneModifiers.EnemyCounterIntelVisible)
        {
            return encounter.displayName;
        }

        EnemyArchetypeDefinitionSO[] enemyDefinitions = encounter.enemies
            .Select(value => archetypes.Require(value.enemyArchetypeId))
            .Distinct()
            .ToArray();
        string abilitySummary = string.Join(", ", enemyDefinitions
            .SelectMany(value => value.abilityIds)
            .Distinct(StringComparer.Ordinal)
            .Select(value => abilities.Require(value).displayName));
        string counterSummary = string.Join(", ", encounter.counterTags
            .Concat(enemyDefinitions.SelectMany(value => value.counterTags))
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Distinct(StringComparer.Ordinal));
        return $"{encounter.displayName}\n적 능력: {abilitySummary}\n대응 정보: {counterSummary}";
    }

    private EnemyEncounterComposition Build(
        OffenseEncounterSO encounter,
        IEnumerable<EnemyIndividualSaveData> source,
        DungeonDifficulty difficulty,
        OffenseRouteNode routeNode,
        OffenseStrategicPressureSnapshot pressure)
    {
        DungeonDifficultyMultipliers difficultyScale =
            DungeonDifficultyRules.GetOffenseMultipliers(difficulty);
        float encounterScale = routeNode == null || routeNode.IsBoss
            ? 1f
            : Mathf.Clamp(routeNode.DangerMultiplier, 0.65f, 1.1f);
        float healthPressure = Mathf.Clamp(
            1f - pressure.Manpower * 0.002f,
            0.8f,
            1f);
        float armamentPressure = Mathf.Clamp(
            1f - pressure.Armament * 0.002f,
            0.8f,
            1f);
        float readinessPressure = Mathf.Clamp(
            1f - (pressure.Logistics + pressure.Intelligence) * 0.00075f,
            0.85f,
            1f);

        List<EnemyIndividualSaveData> saved = source
            .Select(value => value.Clone())
            .ToList();
        List<OffenseBattleCombatant> combatants = new();
        for (int index = 0; index < saved.Count; index++)
        {
            EnemyIndividualSaveData individual = saved[index];
            EnemyArchetypeDefinitionSO archetype =
                archetypes.Require(individual.enemyArchetypeId);
            individuals.EnsureCharacterDomains(
                individuals.RequireBlueprint(individual));
            float personal = individual.combatStatMultiplier;
            float maxHealth = archetype.maxHealth
                * personal
                * difficultyScale.EnemyHealth
                * encounterScale
                * healthPressure;
            CharacterCombatAbilityDefinition[] authoredAbilities =
                archetype.abilityIds
                    .Select(id => ProjectAbility(abilities.Require(id)))
                    .ToArray();

            OffenseBattleCombatant combatant = new OffenseBattleCombatant(
                individual.characterId,
                individual.displayName,
                individual.phenotypeSpeciesId,
                OffenseBattleTeam.Enemies,
                new OffenseBattleStats(
                    maxHealth,
                    ScaleAttack(archetype.attack),
                    ScaleAttack(archetype.strength),
                    archetype.toughness * personal * encounterScale
                        * armamentPressure,
                    ScaleInitiative(archetype.dexterity),
                    ScaleInitiative(archetype.moveSpeed),
                    ShootingAptitude(individual, archetype)
                        * personal * encounterScale,
                    archetype.moveSpeed * personal * encounterScale),
                maxHealth,
                authoredAbilities,
                formation: FormationFor(archetype.role, index));
            if (combatEquipment != null)
            {
                combatEquipment.TryGetActiveWeapon(
                    individual.characterId,
                    out CombatWeaponSnapshot weapon);
                combatant.SetCombatEquipment(
                    weapon,
                    combatEquipment.GetArmor(individual.characterId),
                    combatEquipment.GetShield(individual.characterId));
            }
            combatants.Add(combatant);
            float ScaleAttack(float value) => value
                * personal
                * difficultyScale.EnemyAttack
                * encounterScale
                * armamentPressure;
            float ScaleInitiative(float value) => value
                * personal
                * difficultyScale.EnemyInitiative
                * encounterScale
                * readinessPressure;
        }

        string objectiveCombatantId = ResolveObjectiveCombatantId(encounter, saved);
        if (encounter.objective == OffenseEncounterObjective.CaptureLeader
            && string.IsNullOrWhiteSpace(objectiveCombatantId))
        {
            throw new InvalidOperationException(
                $"Encounter '{encounter.encounterId}' has no generated capture target for archetype '{encounter.objectiveTargetId}'.");
        }
        if (encounter.objective == OffenseEncounterObjective.SabotageTarget)
        {
            float deviceHealth = Mathf.Max(
                60f,
                combatants.Select(value => value.Stats.MaxHealth)
                    .DefaultIfEmpty(80f)
                    .Average() * 1.25f);
            objectiveCombatantId = $"objective:{encounter.encounterId}:device";
            combatants.Add(new OffenseBattleCombatant(
                objectiveCombatantId,
                "전장 핵심 장치",
                "objective-device",
                OffenseBattleTeam.Enemies,
                new OffenseBattleStats(
                    deviceHealth,
                    0f,
                    0f,
                    12f,
                    0f,
                    0f),
                deviceHealth,
                formation: OffenseFormationSlot.Rear,
                participatesInInitiative: false));
        }

        BattlefieldModifierDefinitionSO[] resolvedModifiers =
            (encounter.battlefieldModifierIds ?? new List<string>())
                .Select(battlefieldModifiers.Require)
                .ToArray();
        OffenseBattleEncounterRules rules = new(
            encounter.objective,
            encounter.objectiveRoundLimit,
            encounter.objectiveTargetId,
            objectiveCombatantId,
            resolvedModifiers,
            encounter.counterTags,
            encounter.rewardItemIds);

        return new EnemyEncounterComposition(encounter, combatants, saved, rules);
    }

    private static string ResolveObjectiveCombatantId(
        OffenseEncounterSO encounter,
        IReadOnlyList<EnemyIndividualSaveData> saved)
    {
        if (encounter.objective != OffenseEncounterObjective.CaptureLeader)
        {
            return string.Empty;
        }

        return saved.FirstOrDefault(value => string.Equals(
            value.enemyArchetypeId,
            encounter.objectiveTargetId,
            StringComparison.Ordinal))?.characterId ?? string.Empty;
    }

    private OffenseEncounterSO SelectEncounter(
        OffenseTargetDefinition target,
        string context,
        OffenseRouteNode routeNode)
    {
        int campaign = Mathf.Clamp(target.campaignOrder, 1, 6);
        int variant = (int)(PersistentEntityId.GetStableHash32(
            $"{target.id}:{context}:{routeNode?.Depth ?? 0}") % 6u);
        return encounters.Require(
            $"encounter:{((campaign - 1) * 6 + variant + 1):00}");
    }

    private static int DeterministicCount(
        OffenseEnemyArchetypeEntry entry,
        string context,
        int sequence)
    {
        int span = entry.maximumCount - entry.minimumCount + 1;
        return entry.minimumCount + (int)(PersistentEntityId.GetStableHash32(
            $"{context}:{entry.enemyArchetypeId}:{sequence}") % (uint)span);
    }

    private static CharacterCombatAbilityDefinition ProjectAbility(
        EnemyAbilityDefinitionSO definition) =>
        new(
            definition.stableId,
            definition.displayName,
            definition.description,
            definition.cooldownRounds,
            definition.targetRule,
            definition.effects.Select(ProjectEffect).ToArray());

    private static OffenseCombatEffectModule ProjectEffect(
        EnemyAbilityEffectRecord effect) => effect.kind switch
        {
            EnemyAbilityEffectKind.Damage =>
                new OffenseDamageEffect(effect.magnitude),
            EnemyAbilityEffectKind.DamageOverTime =>
                new OffenseDamageOverTimeEffect(
                    effect.magnitude,
                    Math.Max(1, effect.durationRounds)),
            EnemyAbilityEffectKind.Heal =>
                new OffenseHealEffect(effect.magnitude),
            EnemyAbilityEffectKind.Delay =>
                new OffenseDelayEffect(effect.magnitude),
            EnemyAbilityEffectKind.Vulnerability =>
                new OffenseVulnerabilityEffect(
                    effect.magnitude,
                    Math.Max(1, effect.durationRounds)),
            EnemyAbilityEffectKind.Guard =>
                new OffenseGuardEffect(
                    Mathf.Clamp01(effect.magnitude - 1f),
                    Math.Max(1, effect.durationRounds)),
            EnemyAbilityEffectKind.Dispel =>
                new OffenseCleanseEffect(Math.Max(1, effect.durationRounds)),
            EnemyAbilityEffectKind.Suppression =>
                new OffenseDelayEffect(Mathf.Max(1f, effect.magnitude * 2f)),
            EnemyAbilityEffectKind.Smoke =>
                new OffenseSmokeEffect(
                    Mathf.Clamp(effect.magnitude * 0.5f, 0.1f, 0.8f),
                    Math.Max(1, effect.durationRounds)),
            EnemyAbilityEffectKind.Summon =>
                new OffenseSummonEffect(
                    Mathf.Max(1f, effect.magnitude * 20f),
                    Math.Max(3, effect.durationRounds)),
            _ => new OffenseDamageEffect(Mathf.Max(0.1f, effect.magnitude))
        };

    private static float ShootingAptitude(
        EnemyIndividualSaveData individual,
        EnemyArchetypeDefinitionSO archetype)
    {
        int aptitude = individual.innateAptitudes?.FirstOrDefault(value =>
            string.Equals(
                value.skillId,
                "skill:shooting",
                StringComparison.Ordinal))?.value ?? 40;
        return archetype.attack * Mathf.Lerp(0.75f, 1.25f, aptitude / 100f);
    }

    private static OffenseFormationSlot FormationFor(
        EnemyCombatRole role,
        int index) => role switch
        {
            EnemyCombatRole.Marksman or EnemyCombatRole.Support =>
                OffenseFormationSlot.Rear,
            EnemyCombatRole.Controller => OffenseFormationSlot.Middle,
            _ => (OffenseFormationSlot)Mathf.Clamp(index, 0, 2)
        };

    private static string Normalize(string value) => value?.Trim() ?? string.Empty;
}
