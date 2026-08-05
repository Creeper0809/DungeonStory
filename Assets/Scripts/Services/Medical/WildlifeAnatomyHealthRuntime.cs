using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public sealed class WildlifeAnatomyHealthRuntime :
    IWildlifeAnatomyHealthRuntime,
    ITickable
{
    private readonly IAnatomyProfileCatalog profiles;
    private readonly IWildlifeWorldQuery wildlife;
    private readonly IGameClock clock;
    private readonly SurgeryAggregateStateStore stateStore;

    private float nextComplicationTickAt;

    private Dictionary<string, WildlifeAnatomyState> states =>
        stateStore.State.WildlifeAnatomy;

    public WildlifeAnatomyHealthRuntime(
        IAnatomyProfileCatalog profiles,
        IWildlifeWorldQuery wildlife,
        IGameClock clock,
        SurgeryAggregateStateStore stateStore)
    {
        this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        this.wildlife = wildlife ?? throw new ArgumentNullException(nameof(wildlife));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.stateStore = stateStore
            ?? throw new ArgumentNullException(nameof(stateStore));
    }

    public void Tick()
    {
        if (clock.IsPaused
            || clock.DeltaTime <= 0f
            || clock.Time < nextComplicationTickAt)
        {
            return;
        }

        nextComplicationTickAt = clock.Time + 5f;
        foreach (WildlifeAnatomyState state in states.Values)
        {
            WildlifeActor actor = wildlife.Wildlife.FirstOrDefault(candidate =>
                candidate != null
                && candidate.IsAlive
                && string.Equals(
                    candidate.WildlifeId,
                    state.wildlifeId,
                    StringComparison.Ordinal));
            if (actor == null)
            {
                continue;
            }

            float severity = state.nodes
                .Where(node => node != null && !node.missing)
                .Sum(node =>
                    Mathf.Clamp01((node.infection - 40f) / 60f)
                    + Mathf.Clamp01((node.rejectionBurden - 35f) / 65f));
            if (severity > 0.01f)
            {
                actor.ApplyDamage(
                    Mathf.Max(1, Mathf.CeilToInt(severity)),
                    null);
            }
        }
    }

    public AnatomyHealthSnapshot GetAnatomySnapshot(WildlifeActor actor)
    {
        if (actor == null)
        {
            return Empty();
        }

        WildlifeAnatomyState state = GetOrCreate(actor);
        AnatomyProfileDefinition profile = ResolveProfile(state.profileId);
        return BuildSnapshot(state, profile);
    }

    public bool TryHealNode(
        WildlifeActor actor,
        string nodeId,
        float health,
        float infectionReduction)
    {
        AnatomyNodeHealthState node = Find(GetOrCreate(actor), nodeId);
        if (actor == null || !actor.IsAlive || node == null || node.missing)
        {
            return false;
        }

        float before = node.currentHealth;
        node.currentHealth = Mathf.Min(
            node.maxHealth,
            node.currentHealth + Mathf.Max(0f, health));
        node.infection = Mathf.Max(
            0f,
            node.infection - Mathf.Max(0f, infectionReduction));
        actor.DebugHeal(Mathf.Max(0, Mathf.RoundToInt(node.currentHealth - before)));
        return true;
    }

    public bool TryRemoveNode(
        WildlifeActor actor,
        string nodeId,
        out AnatomyNodeHealthState removedNode,
        out DomainFailure failure)
    {
        removedNode = null;
        failure = DomainFailure.None;
        if (actor == null || !actor.IsAlive)
        {
            failure = new DomainFailure(FailureCode.SurgeryWildlifeSubjectUnavailable);
            return false;
        }

        WildlifeAnatomyState state = GetOrCreate(actor);
        AnatomyProfileDefinition profile = ResolveProfile(state.profileId);
        if (!profile.TryGetNode(nodeId, out AnatomyNodeDefinition definition)
            || !definition.Removable)
        {
            failure = new DomainFailure(FailureCode.SurgeryTargetNodeUnavailable, nodeId);
            return false;
        }

        AnatomyNodeHealthState node = Find(state, nodeId);
        if (node == null || node.missing)
        {
            failure = new DomainFailure(FailureCode.SurgeryTargetNodeUnavailable, nodeId);
            return false;
        }

        removedNode = SurgeryStateCloner.CloneAnatomyNode(node);
        node.missing = true;
        node.currentHealth = 0f;
        node.installedPartId = string.Empty;
        node.installedPartEfficiency = 0f;
        if (definition.Vital)
        {
            actor.ApplyDamage(actor.CurrentHealth, null);
        }

        return true;
    }

    public bool TryInstallPart(
        WildlifeActor actor,
        string nodeId,
        string partInstanceId,
        SurgicalPartKind partKind,
        float efficiency,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (actor == null || !actor.IsAlive)
        {
            failure = new DomainFailure(FailureCode.SurgeryWildlifeSubjectUnavailable);
            return false;
        }

        AnatomyNodeHealthState node = Find(GetOrCreate(actor), nodeId);
        if (node == null)
        {
            failure = new DomainFailure(FailureCode.SurgeryTargetNodeMissing, nodeId);
            return false;
        }

        node.missing = false;
        node.currentHealth = node.maxHealth;
        node.bleedingPerSecond = 0f;
        node.installedPartId = partInstanceId?.Trim() ?? string.Empty;
        node.installedPartKind = partKind;
        node.installedPartEfficiency = Mathf.Clamp(efficiency, 0.1f, 1.75f);
        node.moduleBonus = 0f;
        node.recoveryPolicy = partKind switch
        {
            SurgicalPartKind.NaturalOrgan => PartRecoveryPolicy.Natural,
            SurgicalPartKind.ArcaneGraft => PartRecoveryPolicy.AssistedRegeneration,
            _ => PartRecoveryPolicy.MaintenanceOnly
        };
        return true;
    }

    public bool TryAddNodeBurden(
        WildlifeActor actor,
        string nodeId,
        float rejection,
        float mutation,
        float infection,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        AnatomyNodeHealthState node = actor != null
            ? Find(GetOrCreate(actor), nodeId)
            : null;
        if (node == null || node.missing)
        {
            failure = new DomainFailure(FailureCode.SurgeryTargetNodeUnavailable, nodeId);
            return false;
        }

        node.rejectionBurden = Mathf.Clamp(
            node.rejectionBurden + Mathf.Max(0f, rejection),
            0f,
            100f);
        node.mutationBurden = Mathf.Clamp(
            node.mutationBurden + Mathf.Max(0f, mutation),
            0f,
            100f);
        node.infection = Mathf.Clamp(
            node.infection + Mathf.Max(0f, infection),
            0f,
            100f);
        return true;
    }

    public bool TryReduceNodeBurden(
        WildlifeActor actor,
        string nodeId,
        float rejection,
        float mutation,
        float infection,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        AnatomyNodeHealthState node = actor != null
            ? Find(GetOrCreate(actor), nodeId)
            : null;
        if (node == null || node.missing)
        {
            failure = new DomainFailure(FailureCode.SurgeryTargetNodeUnavailable, nodeId);
            return false;
        }

        node.rejectionBurden = Mathf.Max(
            0f,
            node.rejectionBurden - Mathf.Max(0f, rejection));
        node.mutationBurden = Mathf.Max(
            0f,
            node.mutationBurden - Mathf.Max(0f, mutation));
        node.infection = Mathf.Max(
            0f,
            node.infection - Mathf.Max(0f, infection));
        return true;
    }

    public IReadOnlyList<WildlifeAnatomyState> Capture()
    {
        return states.Values
            .OrderBy(state => state.wildlifeId, StringComparer.Ordinal)
            .Select(SurgeryStateCloner.CloneWildlifeAnatomy)
            .ToArray();
    }

    private WildlifeAnatomyState GetOrCreate(WildlifeActor actor)
    {
        if (actor == null)
        {
            return null;
        }

        string id = actor.WildlifeId;
        if (!states.TryGetValue(id, out WildlifeAnatomyState state))
        {
            AnatomyProfileDefinition profile = profiles.GetForSpecies(actor.SpeciesId);
            state = new WildlifeAnatomyState
            {
                wildlifeId = id,
                profileId = profile.ProfileId
            };
            EnsureNodes(state, profile);
            states.Add(id, state);
        }

        return state;
    }

    private AnatomyProfileDefinition ResolveProfile(string profileId)
    {
        return profiles.TryGet(profileId, out AnatomyProfileDefinition profile)
            ? profile
            : profiles.Profiles.FirstOrDefault(candidate =>
                string.Equals(
                    candidate.AnatomyFamily,
                    "quadruped",
                    StringComparison.OrdinalIgnoreCase))
                ?? profiles.GetDefaultHumanoid();
    }

    private static void EnsureNodes(
        WildlifeAnatomyState state,
        AnatomyProfileDefinition profile)
    {
        state.nodes ??= new List<AnatomyNodeHealthState>();
        foreach (AnatomyNodeDefinition definition in profile.Nodes)
        {
            AnatomyNodeHealthState node = Find(state, definition.NodeId);
            if (node == null)
            {
                state.nodes.Add(new AnatomyNodeHealthState
                {
                    nodeId = definition.NodeId,
                    maxHealth = definition.MaxHealth,
                    currentHealth = definition.MaxHealth
                });
            }
        }
    }

    private static AnatomyHealthSnapshot BuildSnapshot(
        WildlifeAnatomyState state,
        AnatomyProfileDefinition profile)
    {
        float Capacity(AnatomyFunction function)
        {
            AnatomyNodeDefinition[] contributors = profile.Nodes
                .Where(node => (node.Functions & function) != 0)
                .ToArray();
            float total = contributors.Sum(node => Mathf.Max(0.01f, node.CapacityWeight));
            if (total <= 0f)
            {
                return 1f;
            }

            return Mathf.Clamp01(contributors.Sum(definition =>
            {
                AnatomyNodeHealthState node = Find(state, definition.NodeId);
                return (node?.EffectiveEfficiency ?? 0f)
                    * Mathf.Max(0.01f, definition.CapacityWeight);
            }) / total);
        }

        return new AnatomyHealthSnapshot(
            state.profileId,
            state.nodes.Select(SurgeryStateCloner.CloneAnatomyNode).ToArray(),
            Capacity(AnatomyFunction.Consciousness),
            Capacity(AnatomyFunction.Sight),
            Capacity(AnatomyFunction.Breathing),
            Capacity(AnatomyFunction.Digestion),
            Capacity(AnatomyFunction.Filtration),
            Capacity(AnatomyFunction.Manipulation),
            Capacity(AnatomyFunction.Mobility));
    }

    private static AnatomyNodeHealthState Find(
        WildlifeAnatomyState state,
        string nodeId)
    {
        return state?.nodes?.FirstOrDefault(node =>
            node != null
            && string.Equals(node.nodeId, nodeId, StringComparison.Ordinal));
    }

    private static AnatomyHealthSnapshot Empty()
    {
        return new AnatomyHealthSnapshot(
            string.Empty,
            Array.Empty<AnatomyNodeHealthState>(),
            1f,
            1f,
            1f,
            1f,
            1f,
            1f,
            1f);
    }
}
