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

public interface ICharacterSurgicalPartGameplayEffectSourceQuery
{
    IReadOnlyList<IGameplayEffectSource> GetInstalledPartSources(
        CharacterActor actor);
}

public static class CharacterIncrementalGameplayEffectAuthority
{
    public const string Schema =
        "character-incremental-gameplay-effect-authority@1";
    public const float EmbeddedNeutralThreshold = 0.0001f;

    public static float Resolve(float complete, float embedded)
    {
        if (float.IsNaN(complete)
            || float.IsInfinity(complete)
            || float.IsNaN(embedded)
            || float.IsInfinity(embedded))
        {
            throw new InvalidOperationException(
                "Incremental gameplay-effect projection must be finite.");
        }
        float result = Mathf.Abs(embedded) <= EmbeddedNeutralThreshold
            ? complete
            : complete / embedded;
        if (float.IsNaN(result) || float.IsInfinity(result))
            throw new InvalidOperationException(
                "Incremental gameplay-effect multiplier is not finite.");
        return result;
    }

    public static double ResolveAbsoluteMaximum(double completeAbsoluteMaximum)
    {
        if (double.IsNaN(completeAbsoluteMaximum)
            || double.IsInfinity(completeAbsoluteMaximum)
            || completeAbsoluteMaximum < 0d)
        {
            throw new ArgumentOutOfRangeException(
                nameof(completeAbsoluteMaximum));
        }
        return Math.Max(
            completeAbsoluteMaximum,
            completeAbsoluteMaximum / EmbeddedNeutralThreshold);
    }
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

public sealed class CharacterSurgicalPartGameplayEffectSourceQuery :
    ICharacterSurgicalPartGameplayEffectSourceQuery
{
    private sealed class InstalledPartEffectSource : IGameplayEffectSource
    {
        public InstalledPartEffectSource(
            string partInstanceId,
            IReadOnlyList<GameplayEffectBinding> effects)
        {
            SourceRef = new GameplayEffectSourceRef(
                GameplayEffectSourceKind.SurgicalPart,
                partInstanceId);
            Effects = effects ?? Array.Empty<GameplayEffectBinding>();
        }

        public GameplayEffectSourceRef SourceRef { get; }
        public IReadOnlyList<GameplayEffectBinding> Effects { get; }
    }

    private readonly Func<ISurgicalPartRuntime> partRuntime;
    private readonly IItemDefinitionCatalog items;
    private readonly IAnatomyHealthRuntime anatomy;
    private readonly IAnatomyProfileCatalog anatomyProfiles;
    private readonly ISurgeryOrderDemandQuery surgeryOrders;

    public CharacterSurgicalPartGameplayEffectSourceQuery(
        Func<ISurgicalPartRuntime> partRuntime,
        IItemDefinitionCatalog items,
        IAnatomyHealthRuntime anatomy,
        IAnatomyProfileCatalog anatomyProfiles,
        ISurgeryOrderDemandQuery surgeryOrders)
    {
        this.partRuntime = partRuntime
            ?? throw new ArgumentNullException(nameof(partRuntime));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
        this.anatomy = anatomy ?? throw new ArgumentNullException(nameof(anatomy));
        this.anatomyProfiles = anatomyProfiles
            ?? throw new ArgumentNullException(nameof(anatomyProfiles));
        this.surgeryOrders = surgeryOrders
            ?? throw new ArgumentNullException(nameof(surgeryOrders));
    }

    public IReadOnlyList<IGameplayEffectSource> GetInstalledPartSources(
        CharacterActor actor)
    {
        if (actor == null) throw new ArgumentNullException(nameof(actor));
        string characterId = actor.Identity?.PersistentId?.Trim()
            ?? string.Empty;
        if (characterId.Length == 0)
            return Array.Empty<IGameplayEffectSource>();

        ISurgicalPartRuntime parts = partRuntime()
            ?? throw new InvalidOperationException(
                "Installed surgical-part effect projection requires the surgical part runtime.");

        AnatomyHealthSnapshot snapshot = anatomy.GetAnatomySnapshot(actor);
        IReadOnlyList<AnatomyNodeHealthState> snapshotNodes = snapshot.Nodes
            ?? throw new InvalidOperationException(
                $"Installed surgical-part projection has no anatomy nodes for '{characterId}'.");
        AnatomyNodeHealthState[] installedNodes = snapshotNodes
            .Where(value => value != null
                && !string.IsNullOrWhiteSpace(value.installedPartId))
            .ToArray();
        IReadOnlyList<SurgicalPartInstance> runtimeParts = parts.Parts
            ?? throw new InvalidOperationException(
                "Installed surgical-part effect projection requires the surgical part collection.");
        SurgicalPartInstance[] installedParts = runtimeParts.Where(value =>
                value != null
                && value.installed
                && string.Equals(
                    value.installedSubjectId,
                    characterId,
                    StringComparison.Ordinal))
            .ToArray();
        if (installedNodes.Length == 0)
        {
            if (installedParts.Length != 0)
            {
                throw new InvalidOperationException(
                    $"Installed surgical part '{installedParts[0].partInstanceId}' has no unique anatomy forward reference for '{characterId}'.");
            }
            return Array.Empty<IGameplayEffectSource>();
        }
        if (!anatomyProfiles.TryGet(
                snapshot.ProfileId,
                out AnatomyProfileDefinition profile))
        {
            throw new InvalidOperationException(
                $"Installed surgical-part projection references unknown anatomy profile "
                + $"'{snapshot.ProfileId}' for '{characterId}'.");
        }
        IReadOnlyList<SurgeryOrder> activeOrders = surgeryOrders.ActiveOrders
            ?? throw new InvalidOperationException(
                "Installed surgical-part projection requires the active surgery-order collection.");

        if (installedNodes.GroupBy(
                node => node.installedPartId,
                StringComparer.Ordinal)
            .Any(group => group.Count() != 1))
        {
            throw new InvalidOperationException(
                $"Installed surgical-part ownership has duplicate anatomy forward references for '{characterId}'.");
        }

        List<IGameplayEffectSource> sources = new();
        foreach (AnatomyNodeHealthState node in installedNodes
                     .OrderBy(value => value.nodeId, StringComparer.Ordinal))
        {
            SurgicalPartInstance[] matchingParts = runtimeParts
                .Where(value => value != null
                    && string.Equals(
                        value.partInstanceId,
                        node.installedPartId,
                        StringComparison.Ordinal))
                .ToArray();
            if (matchingParts.Length != 1)
            {
                throw new InvalidOperationException(
                    $"Installed surgical-part ownership has a missing or duplicate surgery owner "
                    + $"for '{characterId}:{node.nodeId}'.");
            }

            SurgicalPartInstance part = matchingParts[0];
            bool hasBodyCommittedIncomingOrder =
                HasBodyCommittedIncomingOrder(
                    activeOrders,
                    characterId,
                    node,
                    part);
            bool bodyCommittedIncoming = hasBodyCommittedIncomingOrder
                && IsBodyCommittedReplacementIncoming(
                    activeOrders,
                    runtimeParts,
                    installedNodes,
                    characterId,
                    node,
                    part);
            bool ownershipDrifted =
                (!part.installed && !bodyCommittedIncoming)
                || hasBodyCommittedIncomingOrder && !bodyCommittedIncoming
                || part.installed
                    && !string.Equals(
                        part.installedSubjectId,
                        characterId,
                        StringComparison.Ordinal)
                || part.kind != node.installedPartKind
                || !SurgicalPartAnatomyCompatibility.IsCompatible(
                    profile,
                    part.nodeId,
                    node.nodeId);
            if (ownershipDrifted)
            {
                throw new InvalidOperationException(
                    $"Installed surgical-part ownership drifted for '{characterId}:{node.nodeId}'.");
            }
            if (!part.installed)
                continue;

            string itemId = part.itemDefinitionId?.Trim() ?? string.Empty;
            if (itemId.Length == 0)
                continue;
            if (!items.TryGet(new ItemDefinitionId(itemId), out ItemDefinitionSO item))
            {
                throw new InvalidOperationException(
                    $"Installed surgical part '{part.partInstanceId}' references unknown item '{itemId}'.");
            }
            if (!item.TryGetFeature(
                    out InstalledSurgicalPartEffectItemFeature feature))
            {
                continue;
            }

            int compatibleSlotCount = profile.Nodes.Count(candidate =>
                candidate != null
                && SurgicalPartAnatomyCompatibility.IsCompatible(
                    profile,
                    part.nodeId,
                    candidate.NodeId));
            if (compatibleSlotCount <= 0)
            {
                throw new InvalidOperationException(
                    $"Installed surgical part '{part.partInstanceId}' has no authored compatible anatomy slot.");
            }

            float strength = Mathf.Clamp(
                node.ConditionFactor
                    * Mathf.Max(0f, node.installedPartEfficiency),
                0f,
                CharacterAnatomyStateBounds.MaximumInstalledPartEfficiency);
            GameplayEffectBinding[] bindings = (feature.effects
                    ?? new List<GameplayEffectBinding>())
                .Where(value => value?.definition != null)
                .Select(value => Scale(
                    value,
                    strength,
                    compatibleSlotCount))
                .ToArray();
            if (bindings.Length > 0)
            {
                sources.Add(new InstalledPartEffectSource(
                    part.partInstanceId,
                    bindings));
            }
        }

        foreach (SurgicalPartInstance part in installedParts)
        {
            int forwardReferenceCount = installedNodes.Count(node => string.Equals(
                node.installedPartId,
                part.partInstanceId,
                StringComparison.Ordinal));
            if (forwardReferenceCount == 1)
                continue;
            if (forwardReferenceCount == 0
                && IsBodyCommittedReplacementOld(
                    activeOrders,
                    runtimeParts,
                    installedNodes,
                    characterId,
                    part))
            {
                continue;
            }
            if (forwardReferenceCount != 1)
            {
                throw new InvalidOperationException(
                    $"Installed surgical part '{part.partInstanceId}' has no unique anatomy forward reference for '{characterId}'.");
            }
        }

        return sources;
    }

    private static bool HasBodyCommittedIncomingOrder(
        IReadOnlyList<SurgeryOrder> activeOrders,
        string characterId,
        AnatomyNodeHealthState node,
        SurgicalPartInstance incoming)
    {
        return activeOrders.Any(order => order != null
            && order.replacementPhase ==
                SurgicalPartReplacementPhase.BodyCommitted
            && string.Equals(
                order.subject?.subjectId,
                characterId,
                StringComparison.Ordinal)
            && string.Equals(
                order.targetNodeId,
                node.nodeId,
                StringComparison.Ordinal)
            && string.Equals(
                order.replacementIncomingPartId,
                incoming.partInstanceId,
                StringComparison.Ordinal));
    }

    private static bool IsBodyCommittedReplacementIncoming(
        IReadOnlyList<SurgeryOrder> activeOrders,
        IReadOnlyList<SurgicalPartInstance> runtimeParts,
        IReadOnlyList<AnatomyNodeHealthState> installedNodes,
        string characterId,
        AnatomyNodeHealthState node,
        SurgicalPartInstance incoming)
    {
        SurgeryOrder[] matches = activeOrders.Where(order => order != null
                && order.replacementPhase ==
                    SurgicalPartReplacementPhase.BodyCommitted
                && string.Equals(
                    order.subject?.subjectId,
                    characterId,
                    StringComparison.Ordinal)
                && string.Equals(
                    order.targetNodeId,
                    node.nodeId,
                    StringComparison.Ordinal)
                && string.Equals(
                    order.replacementIncomingPartId,
                    incoming.partInstanceId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1
            || string.IsNullOrWhiteSpace(
                matches[0].replacementExpectedOldPartId))
        {
            return false;
        }

        SurgicalPartInstance[] previous = runtimeParts.Where(part => part != null
                && string.Equals(
                    part.partInstanceId,
                    matches[0].replacementExpectedOldPartId,
                    StringComparison.Ordinal))
            .ToArray();
        return previous.Length == 1
            && previous[0].installed
            && string.Equals(
                previous[0].installedSubjectId,
                characterId,
                StringComparison.Ordinal)
            && !string.Equals(
                previous[0].partInstanceId,
                incoming.partInstanceId,
                StringComparison.Ordinal)
            && installedNodes.Count(nodeState => string.Equals(
                nodeState.installedPartId,
                previous[0].partInstanceId,
                StringComparison.Ordinal)) == 0;
    }

    private static bool IsBodyCommittedReplacementOld(
        IReadOnlyList<SurgeryOrder> activeOrders,
        IReadOnlyList<SurgicalPartInstance> runtimeParts,
        IReadOnlyList<AnatomyNodeHealthState> installedNodes,
        string characterId,
        SurgicalPartInstance previous)
    {
        SurgeryOrder[] matches = activeOrders.Where(order => order != null
                && order.replacementPhase ==
                    SurgicalPartReplacementPhase.BodyCommitted
                && string.Equals(
                    order.subject?.subjectId,
                    characterId,
                    StringComparison.Ordinal)
                && string.Equals(
                    order.replacementExpectedOldPartId,
                    previous.partInstanceId,
                    StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1
            || string.IsNullOrWhiteSpace(matches[0].replacementIncomingPartId)
            || string.Equals(
                matches[0].replacementIncomingPartId,
                previous.partInstanceId,
                StringComparison.Ordinal))
        {
            return false;
        }

        SurgicalPartInstance[] incoming = runtimeParts.Where(part => part != null
                && string.Equals(
                    part.partInstanceId,
                    matches[0].replacementIncomingPartId,
                    StringComparison.Ordinal))
            .ToArray();
        return incoming.Length == 1
            && installedNodes.Count(node => string.Equals(
                node.nodeId,
                matches[0].targetNodeId,
                StringComparison.Ordinal)
                && string.Equals(
                    node.installedPartId,
                    incoming[0].partInstanceId,
                    StringComparison.Ordinal)) == 1;
    }

    private static GameplayEffectBinding Scale(
        GameplayEffectBinding source,
        float strength,
        int compatibleSlotCount)
    {
        if (compatibleSlotCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(compatibleSlotCount));

        float scaledValue = source.definition.Operation switch
        {
            GameplayEffectOperation.Multiply =>
                1f + (source.value - 1f) * strength,
            GameplayEffectOperation.AddFlat or GameplayEffectOperation.AddPercent =>
                source.value * strength,
            _ => throw new InvalidOperationException(
                $"Installed surgical-part effect '{source.bindingId}' uses unsupported operation "
                + source.definition.Operation)
        };
        float value = scaledValue;
        if (compatibleSlotCount > 1)
        {
            if (source.definition.Operation == GameplayEffectOperation.Multiply)
            {
                if (float.IsNaN(scaledValue)
                    || float.IsInfinity(scaledValue)
                    || scaledValue <= 0f)
                {
                    throw new InvalidOperationException(
                        $"Installed surgical-part effect '{source.bindingId}' cannot allocate nonpositive or non-finite multiplier '{scaledValue}' across {compatibleSlotCount} compatible slots.");
                }
                value = Mathf.Pow(scaledValue, 1f / compatibleSlotCount);
            }
            else
            {
                value = scaledValue / compatibleSlotCount;
            }
        }
        return new GameplayEffectBinding
        {
            bindingId = source.bindingId,
            definition = source.definition,
            value = value,
            condition = source.condition
        };
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
    private readonly ICharacterSurgicalPartGameplayEffectSourceQuery surgicalParts;
    private readonly ExtremeTraitRuntime extremeTraits;
    private readonly IGameClock gameClock;
    private readonly Dictionary<string, CharacterDerivedStatsSnapshot> cache =
        new(StringComparer.Ordinal);

    public CharacterDerivedStatsSnapshotProjector(
        IGameContentDefinitionSource content,
        ICharacterEquipmentGameplayEffectSourceQuery equipment,
        ICharacterTransientGameplayEffectSourceQuery transient,
        ExtremeTraitRuntime extremeTraits,
        IGameClock gameClock,
        ICharacterSurgicalPartGameplayEffectSourceQuery surgicalParts = null)
    {
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.equipment = equipment ?? throw new ArgumentNullException(nameof(equipment));
        this.transient = transient ?? throw new ArgumentNullException(nameof(transient));
        this.extremeTraits = extremeTraits
            ?? throw new ArgumentNullException(nameof(extremeTraits));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.surgicalParts = surgicalParts
            ?? NeutralCharacterSurgicalPartGameplayEffectSourceQuery.Instance;
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
        if (actor.Progression != null)
        {
            CharacterAcquiredTraitAggregateState acquired =
                actor.Progression.CaptureAcquiredTraitState();
            CharacterAcquiredTraitSettingsSO settings = null;
            if (acquired.HasPersistentData)
            {
                CharacterAcquiredTraitSettingsSO[] authoredSettings =
                    content.GetAll<CharacterAcquiredTraitSettingsSO>()
                    .Where(value => value != null)
                    .ToArray();
                if (authoredSettings.Length != 1)
                {
                    throw new InvalidOperationException(
                        "Non-empty acquired-trait state requires exactly one authored "
                        + $"settings authority, but found {authoredSettings.Length}.");
                }
                settings = authoredSettings[0];
            }
            sources.AddRange(CharacterAcquiredTraitEffectSourceProjection.Project(
                acquired,
                actor.Progression.NarrativeLedger,
                settings,
                content.GetAll<CharacterAcquiredTraitModuleSO>()));
            sources.AddRange(
                CharacterSkillDrawbackEffectSourceProjection.Project(
                    actor.Progression));
        }
        CharacterSpeciesId speciesId = actor.profile?.PhenotypeSpeciesId ?? default;
        CharacterSpeciesSO species = content.GetAll<CharacterSpeciesSO>()
            .FirstOrDefault(value => value != null
                && value.DefinitionId.Equals(speciesId));
        if (species != null) sources.Add(species);

        sources.AddRange(equipment.GetEquipmentSources(actor)
            ?? Array.Empty<IGameplayEffectSource>());
        sources.AddRange(surgicalParts.GetInstalledPartSources(actor)
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
        return CharacterIncrementalGameplayEffectAuthority.Resolve(
            complete,
            embedded);
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

public sealed class NeutralCharacterSurgicalPartGameplayEffectSourceQuery :
    ICharacterSurgicalPartGameplayEffectSourceQuery
{
    public static readonly NeutralCharacterSurgicalPartGameplayEffectSourceQuery
        Instance = new();
    private NeutralCharacterSurgicalPartGameplayEffectSourceQuery() { }

    public IReadOnlyList<IGameplayEffectSource> GetInstalledPartSources(
        CharacterActor actor) => Array.Empty<IGameplayEffectSource>();
}
