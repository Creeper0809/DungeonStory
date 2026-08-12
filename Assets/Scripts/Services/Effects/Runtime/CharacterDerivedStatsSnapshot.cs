using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public interface ICharacterTransientGameplayEffectSourceQuery
{
    IReadOnlyList<IGameplayEffectSource> GetStatusSources(CharacterActor actor);
    IReadOnlyList<IGameplayEffectSource> GetCompletedResearchSources(CharacterActor actor);
}

public interface ICharacterEquipmentGameplayEffectSourceQuery
{
    IReadOnlyList<IGameplayEffectSource> GetEquipmentSources(CharacterActor actor);
}

public sealed class CharacterEquipmentGameplayEffectSourceQuery :
    ICharacterEquipmentGameplayEffectSourceQuery
{
    private sealed class EquipmentEffectSource : IGameplayEffectSource
    {
        public EquipmentEffectSource(
            GameplayEffectSourceKind kind,
            string sourceId,
            IReadOnlyList<GameplayEffectBinding> effects)
        {
            SourceRef = new GameplayEffectSourceRef(kind, sourceId);
            Effects = effects ?? Array.Empty<GameplayEffectBinding>();
        }

        public GameplayEffectSourceRef SourceRef { get; }
        public IReadOnlyList<GameplayEffectBinding> Effects { get; }
    }

    private readonly ICombatEquipmentRuntime equipment;

    public CharacterEquipmentGameplayEffectSourceQuery(
        ICombatEquipmentRuntime equipment) => this.equipment = equipment
        ?? throw new ArgumentNullException(nameof(equipment));

    public IReadOnlyList<IGameplayEffectSource> GetEquipmentSources(
        CharacterActor actor)
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        List<IGameplayEffectSource> sources = new();
        string characterId = actor.Identity?.PersistentId?.Trim() ?? string.Empty;
        CharacterCombatLoadoutProfile profile = characterId.Length > 0
            && equipment.TryGetActiveProfileSnapshot(
                characterId,
                out CharacterCombatLoadoutProfile equippedProfile)
            ? equippedProfile
            : null;
        string[] equippedIds = (profile?.weaponInstanceIds
                ?? new List<string>())
            .Concat(profile?.armorInstanceIds ?? new List<string>())
            .Append(profile?.shieldInstanceId)
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        HashSet<string> equippedSet = new(equippedIds, StringComparer.Ordinal);
        foreach (string instanceId in equippedIds)
        {
            if (!equipment.TryGetInstance(
                    instanceId,
                    out CombatEquipmentInstance instance)
                || instance.durabilityRatio <= 0f
                || !equipment.TryGetDefinition(
                    instance.definitionId,
                    out CombatEquipmentDefinitionSO definition))
            {
                equippedSet.Remove(instanceId);
                continue;
            }
            sources.Add(new EquipmentEffectSource(
                GameplayEffectSourceKind.Equipment,
                instance.instanceId,
                definition.Effects));
        }
        foreach (EquipmentModuleInstance module in equipment.ModuleInstances
                     .Where(value => value != null
                         && equippedSet.Contains(value.attachedEquipmentInstanceId))
                     .OrderBy(value => value.instanceId, StringComparer.Ordinal))
        {
            if (equipment.TryGetModuleDefinition(
                    module.definitionId,
                    out EquipmentModuleDefinitionSO definition))
            {
                sources.Add(new EquipmentEffectSource(
                    GameplayEffectSourceKind.EquipmentModule,
                    module.instanceId,
                    definition.Effects));
            }
        }
        return sources;
    }
}

public sealed class CharacterTransientGameplayEffectSourceQuery :
    ICharacterTransientGameplayEffectSourceQuery
{
    private readonly IGameContentDefinitionSource content;
    private readonly IBlueprintResearchStateService research;
    private readonly ICharacterCombatSpecialStatusQuery combatStatuses;

    public CharacterTransientGameplayEffectSourceQuery(
        IGameContentDefinitionSource content,
        IBlueprintResearchStateService research,
        ICharacterCombatSpecialStatusQuery combatStatuses)
    {
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.research = research ?? throw new ArgumentNullException(nameof(research));
        this.combatStatuses = combatStatuses
            ?? throw new ArgumentNullException(nameof(combatStatuses));
    }

    public IReadOnlyList<IGameplayEffectSource> GetStatusSources(CharacterActor actor)
    {
        CharacterId characterId = new(actor?.Identity?.PersistentId);
        if (!characterId.IsValid)
            return Array.Empty<IGameplayEffectSource>();
        CharacterCombatSpecialStatusSnapshot status =
            combatStatuses.GetCombatSpecialStatus(characterId);
        if (status.SedationRemainingSeconds <= 0f
            || status.SedationRatio <= 0f)
            return Array.Empty<IGameplayEffectSource>();

        float activityMultiplier = 1f
            - Mathf.Clamp(status.SedationRatio, 0f, 0.8f);
        string[] targets =
        {
            GameplayEffectTargetIds.MoveSpeed,
            GameplayEffectTargetIds.WorkSpeed,
            GameplayEffectTargetIds.CombatPower
        };
        List<GameplayEffectBinding> bindings = new();
        foreach (string target in targets)
        {
            GameplayEffectDefinitionSO definition = content
                .GetAll<GameplayEffectDefinitionSO>()
                .FirstOrDefault(value => value != null
                    && string.Equals(
                        value.TargetId,
                        target,
                        StringComparison.Ordinal));
            if (definition == null)
                throw new InvalidOperationException(
                    $"Sedation status requires gameplay effect target '{target}'.");
            bindings.Add(new GameplayEffectBinding
            {
                bindingId = $"status:sedation:{target}",
                definition = definition,
                value = activityMultiplier
            });
        }
        return new IGameplayEffectSource[]
        {
            new RuntimeStatusEffectSource(
                $"status:sedation:{characterId.Value}",
                bindings)
        };
    }

    public IReadOnlyList<IGameplayEffectSource> GetCompletedResearchSources(
        CharacterActor actor) => content.GetAll<ResearchProjectSO>()
        .Where(value => value != null
            && value.ProjectId.IsValid
            && value.Effects.Count > 0
            && research.GetState().Projects.IsCompleted(value.ProjectId))
        .OrderBy(value => value.ProjectId.Value, StringComparer.Ordinal)
        .Cast<IGameplayEffectSource>()
        .ToArray();

    private sealed class RuntimeStatusEffectSource : IGameplayEffectSource
    {
        public RuntimeStatusEffectSource(
            string sourceId,
            IReadOnlyList<GameplayEffectBinding> effects)
        {
            SourceRef = new GameplayEffectSourceRef(
                GameplayEffectSourceKind.Status,
                sourceId);
            Effects = effects ?? Array.Empty<GameplayEffectBinding>();
        }

        public GameplayEffectSourceRef SourceRef { get; }
        public IReadOnlyList<GameplayEffectBinding> Effects { get; }
    }
}

public sealed class CharacterDerivedStatsSnapshot
{
    public CharacterDerivedStatsSnapshot(
        string revisionKey,
        IReadOnlyDictionary<string, float> values,
        IReadOnlyList<GameplayEffectContribution> contributions)
    {
        RevisionKey = revisionKey ?? string.Empty;
        Values = values ?? throw new ArgumentNullException(nameof(values));
        Contributions = contributions
            ?? throw new ArgumentNullException(nameof(contributions));
    }

    public string RevisionKey { get; }
    public IReadOnlyDictionary<string, float> Values { get; }
    public IReadOnlyList<GameplayEffectContribution> Contributions { get; }
    public float Get(string targetId, float fallback = 1f) =>
        Values.TryGetValue(targetId?.Trim() ?? string.Empty, out float value)
            ? value
            : fallback;
}

public sealed class CharacterDerivedStatsSnapshotProjector
{
    private const int MaximumCachedSnapshots = 512;

    private readonly IGameContentDefinitionSource content;
    private readonly ICharacterEquipmentGameplayEffectSourceQuery equipment;
    private readonly ICharacterTransientGameplayEffectSourceQuery transient;
    private readonly ExtremeTraitRuntime extremeTraits;
    private readonly IGameClock gameClock;
    private readonly Dictionary<string, CharacterDerivedStatsSnapshot> cache =
        new(StringComparer.Ordinal);

    public CharacterDerivedStatsSnapshotProjector(
        IGameContentDefinitionSource content,
        ICharacterEquipmentGameplayEffectSourceQuery equipment,
        ICharacterTransientGameplayEffectSourceQuery transient,
        ExtremeTraitRuntime extremeTraits,
        IGameClock gameClock)
    {
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        this.transient = transient ?? throw new ArgumentNullException(nameof(transient));
        this.extremeTraits = extremeTraits
            ?? throw new ArgumentNullException(nameof(extremeTraits));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
    }

    public CharacterDerivedStatsSnapshot Project(
        CharacterActor actor,
        IReadOnlyDictionary<string, float> baseValues,
        GameplayEffectContext context = null)
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        if (baseValues == null) throw new ArgumentNullException(nameof(baseValues));
        IGameplayEffectSource[] sources = CollectSources(actor).ToArray();
        GameplayEffectContext effectiveContext = (context ?? new GameplayEffectContext())
            .WithConditions(extremeTraits.GetActiveConditionIds(actor, gameClock.Time));
        string revision = BuildRevisionKey(
            actor,
            sources,
            baseValues,
            effectiveContext);
        if (cache.TryGetValue(revision, out CharacterDerivedStatsSnapshot cached))
            return cached;

        Dictionary<string, float> values = new(StringComparer.Ordinal);
        List<GameplayEffectContribution> trace = new();
        foreach (KeyValuePair<string, float> target in baseValues
                     .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            GameplayEffectProjectionResult projection =
                CharacterGameplayEffectProjector.Resolve(
                    target.Key,
                    target.Value,
                    sources,
                    effectiveContext);
            values.Add(target.Key, projection.Value);
            trace.AddRange(projection.Contributions);
        }
        CharacterDerivedStatsSnapshot snapshot = new(revision, values, trace);
        if (cache.Count >= MaximumCachedSnapshots)
            cache.Clear();
        cache.Add(revision, snapshot);
        return snapshot;
    }

    public IReadOnlyList<IGameplayEffectSource> CollectSources(CharacterActor actor)
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        List<IGameplayEffectSource> sources = new();
        sources.AddRange(actor.Progression?.ResolveSelectedTraits()
            ?? Array.Empty<CharacterTraitSO>());
        CharacterSpeciesId speciesId = actor.profile?.PhenotypeSpeciesId ?? default;
        CharacterSpeciesSO species = content.GetAll<CharacterSpeciesSO>()
            .FirstOrDefault(value => value != null
                && value.DefinitionId.Equals(speciesId));
        if (species != null) sources.Add(species);

        sources.AddRange(equipment.GetEquipmentSources(actor)
            ?? Array.Empty<IGameplayEffectSource>());
        sources.AddRange(transient.GetStatusSources(actor)
            ?? Array.Empty<IGameplayEffectSource>());
        sources.AddRange(transient.GetCompletedResearchSources(actor)
            ?? Array.Empty<IGameplayEffectSource>());
        return sources.Where(value => value != null)
            .OrderBy(value => value.SourceRef.Kind)
            .ThenBy(value => value.SourceRef.SourceId, StringComparer.Ordinal)
            .ToArray();
    }

    public float ProjectIncrementalMultiplier(
        CharacterActor actor,
        string targetId,
        GameplayEffectContext context = null)
    {
        if (actor == null) return 1f;
        GameplayEffectContext effectiveContext = (context
                ?? new GameplayEffectContext())
            .WithConditions(extremeTraits.GetActiveConditionIds(
                actor,
                gameClock.Time));
        IGameplayEffectSource[] sources = CollectSources(actor).ToArray();
        float complete = ProjectValue(actor, targetId, 1f, effectiveContext).Value;
        float embedded = CharacterGameplayEffectProjector.Resolve(
            targetId,
            1f,
            sources.Where(source => source.SourceRef.Kind
                is GameplayEffectSourceKind.Trait
                or GameplayEffectSourceKind.Species),
            new GameplayEffectContext()).Value;
        return Mathf.Abs(embedded) <= .0001f
            ? complete
            : complete / embedded;
    }

    /// <summary>
    /// Projects one canonical detailed-stat value from every live gameplay-effect
    /// source. Domain systems use this query when they own the base value and no
    /// legacy CharacterModelModifiers field has already embedded trait/species
    /// effects. The returned value is the authoritative domain input; callers must
    /// not inspect trait IDs or bindings again.
    /// </summary>
    public GameplayEffectProjectionResult ProjectValue(
        CharacterActor actor,
        string targetId,
        float baseValue,
        GameplayEffectContext context = null)
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        if (string.IsNullOrWhiteSpace(targetId))
            throw new ArgumentException(
                "Gameplay effect target id is required.",
                nameof(targetId));
        if (float.IsNaN(baseValue) || float.IsInfinity(baseValue))
            throw new ArgumentOutOfRangeException(nameof(baseValue));

        string normalizedTarget = targetId.Trim();
        CharacterDerivedStatsSnapshot snapshot = Project(
            actor,
            new Dictionary<string, float>(StringComparer.Ordinal)
            {
                [normalizedTarget] = baseValue
            },
            context);
        return new GameplayEffectProjectionResult(
            snapshot.Get(normalizedTarget, baseValue),
            snapshot.Contributions);
    }

    private static string BuildRevisionKey(
        CharacterActor actor,
        IReadOnlyList<IGameplayEffectSource> sources,
        IReadOnlyDictionary<string, float> baseValues,
        GameplayEffectContext context)
    {
        System.Text.StringBuilder value = new(1024);
        Append(value, actor.Identity?.PersistentId);
        foreach (IGameplayEffectSource source in sources)
        {
            Append(value, ((int)source.SourceRef.Kind).ToString());
            Append(value, source.SourceRef.SourceId);
            foreach (GameplayEffectBinding binding in (source.Effects
                         ?? Array.Empty<GameplayEffectBinding>())
                     .Where(item => item != null)
                     .OrderBy(item => item.bindingId, StringComparer.Ordinal))
            {
                Append(value, binding.bindingId);
                Append(value, binding.definition?.EffectId);
                Append(value, binding.definition?.TargetId);
                Append(value, binding.value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
                Append(value, binding.condition?.ConditionId);
            }
        }
        foreach (string condition in context.ActiveConditionIds
                     .OrderBy(item => item, StringComparer.Ordinal))
            Append(value, condition);
        foreach (KeyValuePair<string, float> item in baseValues
                     .OrderBy(item => item.Key, StringComparer.Ordinal))
        {
            Append(value, item.Key);
            Append(value, item.Value.ToString("R", System.Globalization.CultureInfo.InvariantCulture));
        }
        return value.ToString();
    }

    private static void Append(System.Text.StringBuilder target, string value)
    {
        string normalized = value ?? string.Empty;
        target.Append(normalized.Length)
            .Append(':')
            .Append(normalized)
            .Append('|');
    }
}
