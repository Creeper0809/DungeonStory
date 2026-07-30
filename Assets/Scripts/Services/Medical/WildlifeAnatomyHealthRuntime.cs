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
    private readonly Dictionary<string, WildlifeAnatomyState> states =
        new(StringComparer.Ordinal);

    private float nextComplicationTickAt;

    public WildlifeAnatomyHealthRuntime(
        IAnatomyProfileCatalog profiles,
        IWildlifeWorldQuery wildlife,
        IGameClock clock)
    {
        this.profiles = profiles ?? throw new ArgumentNullException(nameof(profiles));
        this.wildlife = wildlife ?? throw new ArgumentNullException(nameof(wildlife));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
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
        out string failureReason)
    {
        removedNode = null;
        failureReason = string.Empty;
        if (actor == null || !actor.IsAlive)
        {
            failureReason = "살아 있는 동물 환자가 없습니다.";
            return false;
        }

        WildlifeAnatomyState state = GetOrCreate(actor);
        AnatomyProfileDefinition profile = ResolveProfile(state.profileId);
        if (!profile.TryGetNode(nodeId, out AnatomyNodeDefinition definition)
            || !definition.Removable)
        {
            failureReason = "적출하거나 절단할 수 없는 부위입니다.";
            return false;
        }

        AnatomyNodeHealthState node = Find(state, nodeId);
        if (node == null || node.missing)
        {
            failureReason = "이미 결손된 부위입니다.";
            return false;
        }

        removedNode = CloneNode(node);
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
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null || !actor.IsAlive)
        {
            failureReason = "살아 있는 동물 환자가 없습니다.";
            return false;
        }

        AnatomyNodeHealthState node = Find(GetOrCreate(actor), nodeId);
        if (node == null)
        {
            failureReason = "대상 동물의 해부 구조에 해당 부위가 없습니다.";
            return false;
        }

        node.missing = false;
        node.currentHealth = node.maxHealth;
        node.bleedingPerSecond = 0f;
        node.installedPartId = partInstanceId?.Trim() ?? string.Empty;
        node.installedPartKind = partKind;
        node.installedPartEfficiency = Mathf.Clamp(efficiency, 0.1f, 1.5f);
        return true;
    }

    public bool TryAddNodeBurden(
        WildlifeActor actor,
        string nodeId,
        float rejection,
        float mutation,
        float infection,
        out string failureReason)
    {
        failureReason = string.Empty;
        AnatomyNodeHealthState node = actor != null
            ? Find(GetOrCreate(actor), nodeId)
            : null;
        if (node == null || node.missing)
        {
            failureReason = "부담을 적용할 동물 부위가 없습니다.";
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
        out string failureReason)
    {
        failureReason = string.Empty;
        AnatomyNodeHealthState node = actor != null
            ? Find(GetOrCreate(actor), nodeId)
            : null;
        if (node == null || node.missing)
        {
            failureReason = "부담을 줄일 동물 신체 부위를 찾을 수 없습니다.";
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
            .Select(CloneState)
            .ToArray();
    }

    public void Restore(
        IEnumerable<WildlifeAnatomyState> restored,
        IList<string> warnings)
    {
        states.Clear();
        foreach (WildlifeAnatomyState source in
                 restored ?? Array.Empty<WildlifeAnatomyState>())
        {
            if (source == null
                || string.IsNullOrWhiteSpace(source.wildlifeId)
                || states.ContainsKey(source.wildlifeId))
            {
                warnings?.Add("중복되거나 대상이 없는 동물 해부 상태를 제외했습니다.");
                continue;
            }

            WildlifeAnatomyState clone = CloneState(source);
            EnsureNodes(clone, ResolveProfile(clone.profileId));
            states.Add(clone.wildlifeId, clone);
        }
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
            state.nodes.Select(CloneNode).ToArray(),
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

    private static WildlifeAnatomyState CloneState(WildlifeAnatomyState source)
    {
        return new WildlifeAnatomyState
        {
            wildlifeId = source.wildlifeId ?? string.Empty,
            profileId = source.profileId ?? string.Empty,
            nodes = (source.nodes ?? new List<AnatomyNodeHealthState>())
                .Where(node => node != null)
                .Select(CloneNode)
                .ToList()
        };
    }

    private static AnatomyNodeHealthState CloneNode(AnatomyNodeHealthState source)
    {
        return new AnatomyNodeHealthState
        {
            nodeId = source.nodeId ?? string.Empty,
            maxHealth = source.maxHealth,
            currentHealth = source.currentHealth,
            bleedingPerSecond = source.bleedingPerSecond,
            infection = source.infection,
            missing = source.missing,
            installedPartId = source.installedPartId ?? string.Empty,
            installedPartKind = source.installedPartKind,
            installedPartEfficiency = source.installedPartEfficiency,
            rejectionBurden = source.rejectionBurden,
            mutationBurden = source.mutationBurden
        };
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
