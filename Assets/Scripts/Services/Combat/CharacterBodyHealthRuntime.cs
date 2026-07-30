using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;

[Serializable]
public sealed class CharacterBodyPartHealthState
{
    public CombatBodyPart bodyPart;
    [Min(1f)] public float maxHealth = 20f;
    [Min(0f)] public float currentHealth = 20f;
    [Min(0f)] public float bleedingPerSecond;

    public float HealthRatio => currentHealth / Mathf.Max(1f, maxHealth);
}

[Serializable]
public sealed class CharacterBodyHealthState
{
    public string characterId = string.Empty;
    public string anatomyProfileId = string.Empty;
    public List<CharacterBodyPartHealthState> parts = new List<CharacterBodyPartHealthState>();
    public List<AnatomyNodeHealthState> anatomyNodes = new List<AnatomyNodeHealthState>();
    [Range(0f, 100f)] public float bloodLoss;
    [Range(0f, 100f)] public float suppression;
    public bool downed;
    public string lastDamageReason = string.Empty;
}

[Serializable]
public sealed class DungeonCharacterBodyHealthSaveData
{
    public List<CharacterBodyHealthState> characters = new List<CharacterBodyHealthState>();
}

public readonly struct CharacterBodyHealthSnapshot
{
    public CharacterBodyHealthSnapshot(
        IReadOnlyList<CharacterBodyPartHealthState> parts,
        float bloodLoss,
        float suppression,
        float consciousness,
        float manipulation,
        float mobility,
        bool downed)
    {
        Parts = parts ?? Array.Empty<CharacterBodyPartHealthState>();
        BloodLoss = Mathf.Clamp(bloodLoss, 0f, 100f);
        Suppression = Mathf.Clamp(suppression, 0f, 100f);
        Consciousness = Mathf.Clamp01(consciousness);
        Manipulation = Mathf.Clamp01(manipulation);
        Mobility = Mathf.Clamp01(mobility);
        Downed = downed;
    }

    public IReadOnlyList<CharacterBodyPartHealthState> Parts { get; }
    public float BloodLoss { get; }
    public float Suppression { get; }
    public float Consciousness { get; }
    public float Manipulation { get; }
    public float Mobility { get; }
    public bool Downed { get; }
}

public interface ICharacterBodyHealthRuntime
{
    CharacterBodyHealthSnapshot GetSnapshot(CharacterActor actor);
    CharacterBodyHealthSnapshot GetSnapshot(string characterId);
    void ApplyCombatResult(CharacterActor target, CombatAttackResult result, string reason);
    void ApplySnapshot(CharacterActor target, CharacterBodyHealthSnapshot snapshot, string reason);
    void AddSuppression(CharacterActor target, float amount);
    void Heal(CharacterActor target, float amount, bool stopBleeding);
    float GetTotalBleeding(CharacterActor target);
    float GetMissingPartHealth(CharacterActor target);
    bool Stabilize(CharacterActor target);
    bool ApplyTreatment(CharacterActor target, float partHealthAmount, float bloodLossReduction);
    DungeonCharacterBodyHealthSaveData Capture();
    void Restore(DungeonCharacterBodyHealthSaveData saveData);
}

public sealed class CharacterBodyHealthRuntime :
    ICharacterBodyHealthRuntime,
    IAnatomyHealthRuntime,
    ITickable
{
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("CharacterBodyHealthRuntime.Tick");

    public readonly struct CharacterDownedEvent
    {
        public CharacterDownedEvent(CharacterActor actor)
        {
            Actor = actor;
        }

        public CharacterActor Actor { get; }
    }

    public readonly struct CharacterRecoveredEvent
    {
        public CharacterRecoveredEvent(CharacterActor actor)
        {
            Actor = actor;
        }

        public CharacterActor Actor { get; }
    }

    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IGameClock gameClock;
    private readonly IGameEventBus gameEventBus;
    private readonly IDynamicFrameWorkBudget frameWorkBudget;
    private readonly IAnatomyProfileCatalog anatomyProfiles;
    private readonly Dictionary<string, CharacterBodyHealthState> states =
        new Dictionary<string, CharacterBodyHealthState>(StringComparer.Ordinal);
    private readonly Dictionary<string, CharacterActor> trackedActors =
        new Dictionary<string, CharacterActor>(StringComparer.Ordinal);
    private readonly Dictionary<string, float> lastTickAt =
        new Dictionary<string, float>(StringComparer.Ordinal);
    private readonly List<string> tickStateIds = new List<string>();
    private int tickStateIndex;
    private bool tickPassActive;

    public CharacterBodyHealthRuntime(
        ICharacterAiWorldRegistry worldRegistry,
        IGameClock gameClock,
        IGameEventBus gameEventBus,
        IDynamicFrameWorkBudget frameWorkBudget,
        IAnatomyProfileCatalog anatomyProfiles)
    {
        this.worldRegistry = worldRegistry ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.gameEventBus = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.frameWorkBudget = frameWorkBudget
            ?? throw new ArgumentNullException(nameof(frameWorkBudget));
        this.anatomyProfiles = anatomyProfiles
            ?? throw new ArgumentNullException(nameof(anatomyProfiles));
    }

    public void Tick()
    {
        using (TickProfilerMarker.Auto())
        {
            TickRuntime();
        }
    }

    private void TickRuntime()
    {
        if (gameClock.DeltaTime <= 0f)
        {
            return;
        }

        if (!tickPassActive)
        {
            if (states.Count == 0)
            {
                frameWorkBudget.SetBacklog(
                    DynamicFrameWorkDomain.CharacterHealth,
                    0);
                return;
            }

            tickStateIds.Clear();
            foreach (string id in states.Keys)
            {
                tickStateIds.Add(id);
            }

            tickStateIndex = 0;
            tickPassActive = true;
        }

        int backlog = tickStateIds.Count - tickStateIndex;
        frameWorkBudget.SetBacklog(
            DynamicFrameWorkDomain.CharacterHealth,
            backlog);
        double sliceMilliseconds = frameWorkBudget.GetSliceMilliseconds(
            DynamicFrameWorkDomain.CharacterHealth,
            0.04,
            0.6);
        long started = System.Diagnostics.Stopwatch.GetTimestamp();
        int processed = 0;
        float now = gameClock.Time;
        while (tickStateIndex < tickStateIds.Count)
        {
            string id = tickStateIds[tickStateIndex++];
            processed++;
            if (!states.TryGetValue(id, out CharacterBodyHealthState state))
            {
                continue;
            }

            CharacterActor actor = ResolveActor(id);
            if (actor == null || actor.IsDead)
            {
                lastTickAt[id] = now;
                continue;
            }

            float previousTick = lastTickAt.TryGetValue(id, out float recorded)
                ? recorded
                : now;
            float delta = Mathf.Max(0f, now - previousTick);
            lastTickAt[id] = now;
            float bleeding = GetStateBleeding(state);

            if (bleeding > 0f)
            {
                state.bloodLoss = Mathf.Clamp(state.bloodLoss + bleeding * delta, 0f, 100f);
                actor.ApplyBodyDamage(bleeding * 0.12f * delta, "출혈");
                if (state.bloodLoss >= 100f && !actor.IsDead)
                {
                    actor.Die("과다 출혈");
                }
            }

            TickAnatomyComplications(actor, state, delta);
            state.suppression = Mathf.Max(0f, state.suppression - 5f * delta);
            bool wasDowned = state.downed;
            UpdateDowned(state);
            SyncLifecycle(actor, state, wasDowned);

            if (processed >= 4
                && ElapsedMilliseconds(started) >= sliceMilliseconds)
            {
                break;
            }
        }

        frameWorkBudget.ReportConsumed(
            DynamicFrameWorkDomain.CharacterHealth,
            ElapsedMilliseconds(started));
        if (tickStateIndex < tickStateIds.Count)
        {
            return;
        }

        tickStateIds.Clear();
        tickPassActive = false;
        frameWorkBudget.SetBacklog(
            DynamicFrameWorkDomain.CharacterHealth,
            0);
    }

    public CharacterBodyHealthSnapshot GetSnapshot(CharacterActor actor)
    {
        return actor == null
            ? EmptySnapshot()
            : BuildSnapshot(GetOrCreate(actor));
    }

    public CharacterBodyHealthSnapshot GetSnapshot(string characterId)
    {
        return !string.IsNullOrWhiteSpace(characterId)
            && states.TryGetValue(characterId, out CharacterBodyHealthState state)
                ? BuildSnapshot(state)
                : EmptySnapshot();
    }

    public void ApplyCombatResult(CharacterActor target, CombatAttackResult result, string reason)
    {
        if (target == null || target.IsDead || !result.Executed)
        {
            return;
        }

        CharacterBodyHealthState state = GetOrCreate(target);
        state.suppression = Mathf.Clamp(state.suppression + result.Suppression, 0f, 100f);
        if (!result.Hit || result.AppliedDamage <= 0f)
        {
            bool wasDowned = state.downed;
            UpdateDowned(state);
            SyncLifecycle(target, state, wasDowned);
            return;
        }

        CharacterBodyPartHealthState part = state.parts.First(item => item.bodyPart == result.BodyPart);
        part.currentHealth = Mathf.Max(0f, part.currentHealth - result.AppliedDamage);
        part.bleedingPerSecond += result.Bleeding * 0.01f;
        ApplyLegacyDamageToAnatomy(
            state,
            result.BodyPart,
            result.AppliedDamage,
            result.Bleeding * 0.01f);
        state.lastDamageReason = reason ?? string.Empty;
        target.ApplyBodyDamage(result.AppliedDamage, reason);

        if (!target.IsDead
            && (result.BodyPart == CombatBodyPart.Head || result.BodyPart == CombatBodyPart.Torso)
            && part.currentHealth <= 0f)
        {
            target.Die(result.BodyPart == CombatBodyPart.Head ? "머리 치명상" : "몸통 치명상");
        }

        bool wasDownedAfterHit = state.downed;
        UpdateDowned(state);
        SyncLifecycle(target, state, wasDownedAfterHit);
    }

    public void ApplySnapshot(
        CharacterActor target,
        CharacterBodyHealthSnapshot snapshot,
        string reason)
    {
        if (target == null || snapshot.Parts == null || snapshot.Parts.Count == 0)
        {
            return;
        }

        CharacterBodyHealthState state = GetOrCreate(target);
        state.parts = snapshot.Parts.Select(ClonePart).ToList();
        EnsureParts(state);
        SyncAnatomySurfaceNodesFromLegacy(state);
        state.bloodLoss = Mathf.Clamp(snapshot.BloodLoss, 0f, 100f);
        state.suppression = Mathf.Clamp(snapshot.Suppression, 0f, 100f);
        state.lastDamageReason = reason ?? string.Empty;
        bool wasDowned = state.downed;
        UpdateDowned(state);
        SyncLifecycle(target, state, wasDowned);
    }

    public void AddSuppression(CharacterActor target, float amount)
    {
        if (target == null || amount <= 0f)
        {
            return;
        }

        CharacterBodyHealthState state = GetOrCreate(target);
        state.suppression = Mathf.Clamp(state.suppression + amount, 0f, 100f);
        bool wasDowned = state.downed;
        UpdateDowned(state);
        SyncLifecycle(target, state, wasDowned);
    }

    public void Heal(CharacterActor target, float amount, bool stopBleeding)
    {
        if (target == null || amount <= 0f)
        {
            return;
        }

        CharacterBodyHealthState state = GetOrCreate(target);
        float remaining = amount;
        foreach (CharacterBodyPartHealthState part in state.parts.OrderBy(part => part.HealthRatio))
        {
            float restored = Mathf.Min(remaining, part.maxHealth - part.currentHealth);
            part.currentHealth += restored;
            remaining -= restored;
            if (stopBleeding)
            {
                part.bleedingPerSecond = 0f;
            }

            if (remaining <= 0f)
            {
                break;
            }
        }

        SyncAnatomySurfaceNodesFromLegacy(state);
        state.bloodLoss = Mathf.Max(0f, state.bloodLoss - amount * 0.5f);
        target.Heal(amount);
        bool wasDowned = state.downed;
        UpdateDowned(state);
        SyncLifecycle(target, state, wasDowned);
    }

    public float GetTotalBleeding(CharacterActor target)
    {
        if (target == null)
        {
            return 0f;
        }

        CharacterBodyHealthState state = GetOrCreate(target);
        return GetStateBleeding(state);
    }

    public float GetMissingPartHealth(CharacterActor target)
    {
        if (target == null)
        {
            return 0f;
        }

        CharacterBodyHealthState state = GetOrCreate(target);
        if (state.anatomyNodes != null && state.anatomyNodes.Count > 0)
        {
            return state.anatomyNodes.Sum(node =>
                Mathf.Max(0f, node.maxHealth - node.currentHealth));
        }

        return state.parts.Sum(part =>
            Mathf.Max(0f, part.maxHealth - part.currentHealth));
    }

    public bool Stabilize(CharacterActor target)
    {
        if (target == null || target.IsDead)
        {
            return false;
        }

        CharacterBodyHealthState state = GetOrCreate(target);
        bool changed = false;
        foreach (CharacterBodyPartHealthState part in state.parts)
        {
            if (part.bleedingPerSecond <= 0f)
            {
                continue;
            }

            part.bleedingPerSecond = 0f;
            changed = true;
        }

        foreach (AnatomyNodeHealthState node in state.anatomyNodes)
        {
            if (node.bleedingPerSecond <= 0f)
            {
                continue;
            }

            node.bleedingPerSecond = 0f;
            changed = true;
        }

        bool wasDowned = state.downed;
        UpdateDowned(state);
        SyncLifecycle(target, state, wasDowned);
        return changed;
    }

    public bool ApplyTreatment(
        CharacterActor target,
        float partHealthAmount,
        float bloodLossReduction)
    {
        if (target == null || target.IsDead)
        {
            return false;
        }

        CharacterBodyHealthState state = GetOrCreate(target);
        float remaining = Mathf.Max(0f, partHealthAmount);
        float restoredTotal = 0f;
        foreach (CharacterBodyPartHealthState part in state.parts.OrderBy(part => part.HealthRatio))
        {
            float restored = Mathf.Min(remaining, part.maxHealth - part.currentHealth);
            part.currentHealth += restored;
            remaining -= restored;
            restoredTotal += restored;
            if (remaining <= 0f)
            {
                break;
            }
        }

        SyncAnatomySurfaceNodesFromLegacy(state);
        float previousBloodLoss = state.bloodLoss;
        state.bloodLoss = Mathf.Max(0f, state.bloodLoss - Mathf.Max(0f, bloodLossReduction));
        if (restoredTotal > 0f)
        {
            target.Heal(restoredTotal);
        }

        bool wasDowned = state.downed;
        UpdateDowned(state);
        SyncLifecycle(target, state, wasDowned);
        return restoredTotal > 0f || state.bloodLoss < previousBloodLoss;
    }

    public DungeonCharacterBodyHealthSaveData Capture()
    {
        return new DungeonCharacterBodyHealthSaveData
        {
            characters = states.Values.Select(CloneState).ToList()
        };
    }

    public void Restore(DungeonCharacterBodyHealthSaveData saveData)
    {
        states.Clear();
        trackedActors.Clear();
        lastTickAt.Clear();
        tickStateIds.Clear();
        tickPassActive = false;
        foreach (CharacterBodyHealthState source in saveData?.characters ?? new List<CharacterBodyHealthState>())
        {
            if (source == null
                || string.IsNullOrWhiteSpace(source.characterId)
                || states.ContainsKey(source.characterId))
            {
                continue;
            }

            CharacterBodyHealthState restored = CloneState(source);
            EnsureParts(restored);
            EnsureAnatomy(restored, ResolveProfile(restored.anatomyProfileId));
            UpdateDowned(restored);
            states.Add(restored.characterId, restored);
        }

        foreach (CharacterActor actor in worldRegistry.Characters)
        {
            string id = GetId(actor);
            if (states.TryGetValue(id, out CharacterBodyHealthState state))
            {
                trackedActors[id] = actor;
                lastTickAt[id] = gameClock.Time;
                SyncLifecycle(actor, state, wasDowned: !state.downed);
            }
        }
    }

    private CharacterBodyHealthState GetOrCreate(CharacterActor actor)
    {
        string id = GetId(actor);
        if (actor != null)
        {
            trackedActors[id] = actor;
        }

        if (!lastTickAt.ContainsKey(id))
        {
            lastTickAt[id] = gameClock.Time;
        }

        CharacterBodyHealthState state = GetOrCreate(id);
        AnatomyProfileDefinition profile = anatomyProfiles.GetForSpecies(actor?.SpeciesTag);
        EnsureAnatomy(state, profile);
        return state;
    }

    private CharacterBodyHealthState GetOrCreate(string characterId)
    {
        if (states.TryGetValue(characterId, out CharacterBodyHealthState state))
        {
            EnsureParts(state);
            EnsureAnatomy(state, ResolveProfile(state.anatomyProfileId));
            return state;
        }

        state = new CharacterBodyHealthState
        {
            characterId = characterId
        };
        EnsureParts(state);
        EnsureAnatomy(state, anatomyProfiles.GetDefaultHumanoid());
        states.Add(characterId, state);
        return state;
    }

    public AnatomyHealthSnapshot GetAnatomySnapshot(CharacterActor actor)
    {
        return actor == null
            ? EmptyAnatomySnapshot()
            : BuildAnatomySnapshot(GetOrCreate(actor));
    }

    public AnatomyHealthSnapshot GetAnatomySnapshot(string characterId)
    {
        if (string.IsNullOrWhiteSpace(characterId)
            || !states.TryGetValue(characterId, out CharacterBodyHealthState state))
        {
            return EmptyAnatomySnapshot();
        }

        EnsureAnatomy(state, ResolveProfile(state.anatomyProfileId));
        return BuildAnatomySnapshot(state);
    }

    public bool TryDamageNode(
        CharacterActor actor,
        string nodeId,
        float damage,
        float bleeding,
        string reason)
    {
        if (actor == null || actor.IsDead || damage <= 0f)
        {
            return false;
        }

        CharacterBodyHealthState state = GetOrCreate(actor);
        AnatomyNodeHealthState node = FindAnatomyNode(state, nodeId);
        if (node == null || node.missing)
        {
            return false;
        }

        node.currentHealth = Mathf.Max(0f, node.currentHealth - damage);
        node.bleedingPerSecond += Mathf.Max(0f, bleeding);
        state.lastDamageReason = reason ?? string.Empty;
        actor.ApplyBodyDamage(damage, reason);
        SyncLegacySurfaceNode(state, node.nodeId);
        KillForDestroyedVitalNode(actor, state, node.nodeId);
        bool wasDowned = state.downed;
        UpdateDowned(state);
        SyncLifecycle(actor, state, wasDowned);
        return true;
    }

    public bool TryHealNode(
        CharacterActor actor,
        string nodeId,
        float health,
        float infectionReduction)
    {
        if (actor == null || actor.IsDead)
        {
            return false;
        }

        CharacterBodyHealthState state = GetOrCreate(actor);
        AnatomyNodeHealthState node = FindAnatomyNode(state, nodeId);
        if (node == null || node.missing)
        {
            return false;
        }

        float previousHealth = node.currentHealth;
        float previousInfection = node.infection;
        node.currentHealth = Mathf.Min(
            node.maxHealth,
            node.currentHealth + Mathf.Max(0f, health));
        node.infection = Mathf.Max(
            0f,
            node.infection - Mathf.Max(0f, infectionReduction));
        SyncLegacySurfaceNode(state, node.nodeId);
        float restored = node.currentHealth - previousHealth;
        if (restored > 0f)
        {
            actor.Heal(restored);
        }

        bool wasDowned = state.downed;
        UpdateDowned(state);
        SyncLifecycle(actor, state, wasDowned);
        return restored > 0f || node.infection < previousInfection;
    }

    public bool TryRemoveNode(
        CharacterActor actor,
        string nodeId,
        out AnatomyNodeHealthState removedNode,
        out string failureReason)
    {
        removedNode = null;
        failureReason = string.Empty;
        if (actor == null || actor.IsDead)
        {
            failureReason = "수술 대상이 유효하지 않습니다.";
            return false;
        }

        CharacterBodyHealthState state = GetOrCreate(actor);
        AnatomyProfileDefinition profile = ResolveProfile(state.anatomyProfileId);
        if (!profile.TryGetNode(nodeId, out AnatomyNodeDefinition definition))
        {
            failureReason = "해당 신체 부위를 찾을 수 없습니다.";
            return false;
        }

        AnatomyNodeHealthState node = FindAnatomyNode(state, nodeId);
        if (node == null || node.missing)
        {
            failureReason = "이미 결손된 부위입니다.";
            return false;
        }

        if (!definition.Removable)
        {
            failureReason = "제거할 수 없는 신체 부위입니다.";
            return false;
        }

        removedNode = CloneAnatomyNode(node);
        node.missing = true;
        node.currentHealth = 0f;
        node.bleedingPerSecond = Mathf.Max(node.bleedingPerSecond, 0.35f);
        node.installedPartId = string.Empty;
        node.installedPartEfficiency = 0f;
        SyncLegacySurfaceNode(state, node.nodeId);
        KillForDestroyedVitalNode(actor, state, node.nodeId);
        bool wasDowned = state.downed;
        UpdateDowned(state);
        SyncLifecycle(actor, state, wasDowned);
        return true;
    }

    public bool TryInstallPart(
        CharacterActor actor,
        string nodeId,
        string partInstanceId,
        SurgicalPartKind partKind,
        float efficiency,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null || actor.IsDead)
        {
            failureReason = "수술 대상이 유효하지 않습니다.";
            return false;
        }

        CharacterBodyHealthState state = GetOrCreate(actor);
        AnatomyNodeHealthState node = FindAnatomyNode(state, nodeId);
        if (node == null)
        {
            failureReason = "해당 신체 부위를 찾을 수 없습니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(partInstanceId))
        {
            failureReason = "설치할 장기 또는 보철 인스턴스가 없습니다.";
            return false;
        }

        node.missing = false;
        node.installedPartId = partInstanceId.Trim();
        node.installedPartKind = partKind;
        node.installedPartEfficiency = Mathf.Clamp(efficiency, 0.1f, 1.5f);
        node.currentHealth = Mathf.Max(node.currentHealth, node.maxHealth * 0.35f);
        node.bleedingPerSecond = Mathf.Min(node.bleedingPerSecond, 0.05f);
        SyncLegacySurfaceNode(state, node.nodeId);
        bool wasDowned = state.downed;
        UpdateDowned(state);
        SyncLifecycle(actor, state, wasDowned);
        return true;
    }

    public bool TryReplaceNodePart(
        CharacterActor actor,
        string nodeId,
        string partInstanceId,
        SurgicalPartKind partKind,
        float efficiency,
        out AnatomyNodeHealthState replacedNode,
        out string failureReason)
    {
        replacedNode = null;
        failureReason = string.Empty;
        if (actor == null || actor.IsDead)
        {
            failureReason = "수술 대상이 유효하지 않습니다.";
            return false;
        }

        CharacterBodyHealthState state = GetOrCreate(actor);
        AnatomyNodeHealthState node = FindAnatomyNode(state, nodeId);
        if (node == null)
        {
            failureReason = "해당 신체 부위를 찾을 수 없습니다.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(partInstanceId))
        {
            failureReason = "교체할 장기 또는 보철 인스턴스가 없습니다.";
            return false;
        }

        replacedNode = CloneAnatomyNode(node);
        node.missing = false;
        node.installedPartId = partInstanceId.Trim();
        node.installedPartKind = partKind;
        node.installedPartEfficiency = Mathf.Clamp(efficiency, 0.1f, 1.5f);
        node.currentHealth = Mathf.Max(node.maxHealth * 0.35f, 1f);
        node.bleedingPerSecond = Mathf.Min(node.bleedingPerSecond, 0.05f);
        SyncLegacySurfaceNode(state, node.nodeId);
        bool wasDowned = state.downed;
        UpdateDowned(state);
        SyncLifecycle(actor, state, wasDowned);
        return true;
    }

    public bool TryAddNodeBurden(
        CharacterActor actor,
        string nodeId,
        float rejection,
        float mutation,
        float infection,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null || actor.IsDead)
        {
            failureReason = "수술 대상이 유효하지 않습니다.";
            return false;
        }

        CharacterBodyHealthState state = GetOrCreate(actor);
        AnatomyNodeHealthState node = FindAnatomyNode(state, nodeId);
        if (node == null || node.missing)
        {
            failureReason = "부담을 적용할 신체 부위를 찾을 수 없습니다.";
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
        CharacterActor actor,
        string nodeId,
        float rejection,
        float mutation,
        float infection,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null || actor.IsDead)
        {
            failureReason = "수술 대상이 유효하지 않습니다.";
            return false;
        }

        AnatomyNodeHealthState node = FindAnatomyNode(
            GetOrCreate(actor),
            nodeId);
        if (node == null || node.missing)
        {
            failureReason = "부담을 줄일 신체 부위를 찾을 수 없습니다.";
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

    private void TickAnatomyComplications(
        CharacterActor actor,
        CharacterBodyHealthState state,
        float deltaTime)
    {
        if (actor == null
            || actor.IsDead
            || deltaTime <= 0f
            || state?.anatomyNodes == null)
        {
            return;
        }

        AnatomyProfileDefinition profile = ResolveProfile(
            state.anatomyProfileId);
        foreach (AnatomyNodeHealthState node in state.anatomyNodes)
        {
            if (node == null || node.missing)
            {
                continue;
            }

            float infectionDamage =
                Mathf.Clamp01((node.infection - 40f) / 60f)
                * 0.055f
                * deltaTime;
            float rejectionDamage =
                Mathf.Clamp01((node.rejectionBurden - 35f) / 65f)
                * 0.04f
                * deltaTime;
            float damage = infectionDamage + rejectionDamage;
            if (damage <= 0f)
            {
                continue;
            }

            node.currentHealth = Mathf.Max(
                0f,
                node.currentHealth - damage);
            actor.ApplyBodyDamage(damage * 0.35f, "수술 후 합병증");
            if (node.currentHealth <= 0f
                && profile.TryGetNode(
                    node.nodeId,
                    out AnatomyNodeDefinition definition)
                && definition.Vital)
            {
                actor.Die($"{definition.DisplayName} 기능 상실");
                return;
            }
        }
    }

    private CharacterActor ResolveActor(string characterId)
    {
        if (trackedActors.TryGetValue(
                characterId,
                out CharacterActor tracked)
            && tracked != null)
        {
            return tracked;
        }

        IReadOnlyList<CharacterActor> actors = worldRegistry.Characters;
        for (int i = 0; i < actors.Count; i++)
        {
            CharacterActor actor = actors[i];
            if (actor != null && GetId(actor) == characterId)
            {
                trackedActors[characterId] = actor;
                return actor;
            }
        }

        trackedActors.Remove(characterId);
        return null;
    }

    private static double ElapsedMilliseconds(long started)
    {
        return (System.Diagnostics.Stopwatch.GetTimestamp() - started)
            * 1000.0
            / System.Diagnostics.Stopwatch.Frequency;
    }

    private static void EnsureParts(CharacterBodyHealthState state)
    {
        state.parts ??= new List<CharacterBodyPartHealthState>();
        EnsurePart(state, CombatBodyPart.Head, 18f);
        EnsurePart(state, CombatBodyPart.Torso, 45f);
        EnsurePart(state, CombatBodyPart.LeftArm, 22f);
        EnsurePart(state, CombatBodyPart.RightArm, 22f);
        EnsurePart(state, CombatBodyPart.LeftLeg, 26f);
        EnsurePart(state, CombatBodyPart.RightLeg, 26f);
    }

    private static void EnsurePart(CharacterBodyHealthState state, CombatBodyPart bodyPart, float maxHealth)
    {
        CharacterBodyPartHealthState part = state.parts.FirstOrDefault(item => item.bodyPart == bodyPart);
        if (part == null)
        {
            state.parts.Add(new CharacterBodyPartHealthState
            {
                bodyPart = bodyPart,
                maxHealth = maxHealth,
                currentHealth = maxHealth
            });
            return;
        }

        part.maxHealth = Mathf.Max(1f, part.maxHealth);
        part.currentHealth = Mathf.Clamp(part.currentHealth, 0f, part.maxHealth);
        part.bleedingPerSecond = Mathf.Max(0f, part.bleedingPerSecond);
    }

    private CharacterBodyHealthSnapshot BuildSnapshot(CharacterBodyHealthState state)
    {
        CharacterBodyPartHealthState head = state.parts.First(part => part.bodyPart == CombatBodyPart.Head);
        CharacterBodyPartHealthState torso = state.parts.First(part => part.bodyPart == CombatBodyPart.Torso);
        CharacterBodyPartHealthState leftArm = state.parts.First(part => part.bodyPart == CombatBodyPart.LeftArm);
        CharacterBodyPartHealthState rightArm = state.parts.First(part => part.bodyPart == CombatBodyPart.RightArm);
        CharacterBodyPartHealthState leftLeg = state.parts.First(part => part.bodyPart == CombatBodyPart.LeftLeg);
        CharacterBodyPartHealthState rightLeg = state.parts.First(part => part.bodyPart == CombatBodyPart.RightLeg);
        GetPhysicalCapacity(
            state,
            out float consciousness,
            out float manipulation,
            out float mobility);
        return new CharacterBodyHealthSnapshot(
            state.parts.Select(ClonePart).ToArray(),
            state.bloodLoss,
            state.suppression,
            consciousness,
            manipulation,
            mobility,
            state.downed);
    }

    private void UpdateDowned(CharacterBodyHealthState state)
    {
        GetPhysicalCapacity(
            state,
            out float consciousness,
            out _,
            out float mobility);
        if (state.downed)
        {
            state.downed = consciousness < 0.35f
                || mobility < 0.3f
                || state.bloodLoss >= 70f;
            return;
        }

        state.downed = consciousness < 0.25f || mobility < 0.2f;
    }

    private void GetPhysicalCapacity(
        CharacterBodyHealthState state,
        out float consciousness,
        out float manipulation,
        out float mobility)
    {
        if (state.anatomyNodes != null && state.anatomyNodes.Count > 0)
        {
            AnatomyHealthSnapshot anatomy = BuildAnatomySnapshot(state);
            GetLegacySurfaceCapacity(
                state,
                out float surfaceConsciousness,
                out float surfaceManipulation,
                out float surfaceMobility);
            consciousness = Mathf.Min(anatomy.Consciousness, surfaceConsciousness)
                * Mathf.Lerp(1f, 0.2f, state.bloodLoss / 100f);
            manipulation = Mathf.Min(anatomy.Manipulation, surfaceManipulation);
            mobility = Mathf.Min(anatomy.Mobility, surfaceMobility);
            return;
        }

        GetLegacySurfaceCapacity(
            state,
            out consciousness,
            out manipulation,
            out mobility);
        consciousness *= Mathf.Lerp(1f, 0.2f, state.bloodLoss / 100f);
    }

    private static void GetLegacySurfaceCapacity(
        CharacterBodyHealthState state,
        out float consciousness,
        out float manipulation,
        out float mobility)
    {
        float head = 1f;
        float torso = 1f;
        float leftArm = 1f;
        float rightArm = 1f;
        float leftLeg = 1f;
        float rightLeg = 1f;

        for (int i = 0; i < state.parts.Count; i++)
        {
            CharacterBodyPartHealthState part = state.parts[i];
            if (part == null)
            {
                continue;
            }

            switch (part.bodyPart)
            {
                case CombatBodyPart.Head:
                    head = part.HealthRatio;
                    break;
                case CombatBodyPart.Torso:
                    torso = part.HealthRatio;
                    break;
                case CombatBodyPart.LeftArm:
                    leftArm = part.HealthRatio;
                    break;
                case CombatBodyPart.RightArm:
                    rightArm = part.HealthRatio;
                    break;
                case CombatBodyPart.LeftLeg:
                    leftLeg = part.HealthRatio;
                    break;
                case CombatBodyPart.RightLeg:
                    rightLeg = part.HealthRatio;
                break;
            }
        }

        consciousness = Mathf.Min(head, torso);
        manipulation = (leftArm + rightArm) * 0.5f;
        mobility = (leftLeg + rightLeg) * 0.5f;
    }

    private void SyncLifecycle(
        CharacterActor actor,
        CharacterBodyHealthState state,
        bool wasDowned)
    {
        if (actor == null || actor.IsDead)
        {
            return;
        }

        if (state.downed)
        {
            if (!wasDowned || actor.CurrentLifecycleState != CharacterLifecycleState.Downed)
            {
                gameEventBus.Publish(new CharacterDownedEvent(actor));
                if (Application.isPlaying)
                {
                    DefenseCombatPresentation presentation =
                        DefenseCombatPresentation.Ensure(actor);
                    presentation?.SetDowned(true);
                    presentation?.SetStatus("쓰러짐", combatActive: true);
                }
            }

            return;
        }

        if (wasDowned || actor.CurrentLifecycleState == CharacterLifecycleState.Downed)
        {
            gameEventBus.Publish(new CharacterRecoveredEvent(actor));
            if (Application.isPlaying)
            {
                DefenseCombatPresentation presentation =
                    DefenseCombatPresentation.Ensure(actor);
                presentation?.SetDowned(false);
                presentation?.SetStatus("회복 중", combatActive: false);
            }
        }
    }

    private static CharacterBodyHealthState CloneState(CharacterBodyHealthState source)
    {
        return new CharacterBodyHealthState
        {
            characterId = source.characterId ?? string.Empty,
            anatomyProfileId = source.anatomyProfileId ?? string.Empty,
            parts = source.parts?.Select(ClonePart).ToList() ?? new List<CharacterBodyPartHealthState>(),
            anatomyNodes = source.anatomyNodes?.Select(CloneAnatomyNode).ToList()
                ?? new List<AnatomyNodeHealthState>(),
            bloodLoss = Mathf.Clamp(source.bloodLoss, 0f, 100f),
            suppression = Mathf.Clamp(source.suppression, 0f, 100f),
            downed = source.downed,
            lastDamageReason = source.lastDamageReason ?? string.Empty
        };
    }

    private static CharacterBodyPartHealthState ClonePart(CharacterBodyPartHealthState source)
    {
        return new CharacterBodyPartHealthState
        {
            bodyPart = source.bodyPart,
            maxHealth = source.maxHealth,
            currentHealth = source.currentHealth,
            bleedingPerSecond = source.bleedingPerSecond
        };
    }

    private AnatomyProfileDefinition ResolveProfile(string profileId)
    {
        return anatomyProfiles.TryGet(profileId, out AnatomyProfileDefinition profile)
            ? profile
            : anatomyProfiles.GetDefaultHumanoid();
    }

    private static void EnsureAnatomy(
        CharacterBodyHealthState state,
        AnatomyProfileDefinition profile)
    {
        if (state == null || profile == null)
        {
            return;
        }

        state.anatomyProfileId = profile.ProfileId;
        state.anatomyNodes ??= new List<AnatomyNodeHealthState>();
        foreach (AnatomyNodeDefinition definition in profile.Nodes)
        {
            AnatomyNodeHealthState node = state.anatomyNodes.FirstOrDefault(
                candidate => string.Equals(
                    candidate.nodeId,
                    definition.NodeId,
                    StringComparison.Ordinal));
            if (node == null)
            {
                node = new AnatomyNodeHealthState
                {
                    nodeId = definition.NodeId,
                    maxHealth = definition.MaxHealth,
                    currentHealth = definition.MaxHealth,
                    installedPartKind = SurgicalPartKind.NaturalOrgan,
                    installedPartEfficiency = 1f
                };
                state.anatomyNodes.Add(node);
            }

            node.maxHealth = Mathf.Max(1f, node.maxHealth);
            node.currentHealth = Mathf.Clamp(node.currentHealth, 0f, node.maxHealth);
            node.bleedingPerSecond = Mathf.Max(0f, node.bleedingPerSecond);
            node.infection = Mathf.Clamp(node.infection, 0f, 100f);
            node.installedPartEfficiency = Mathf.Max(0f, node.installedPartEfficiency);
        }

        state.anatomyNodes.RemoveAll(node =>
            node == null || !profile.TryGetNode(node.nodeId, out _));
        SyncAnatomySurfaceNodesFromLegacy(state);
    }

    private AnatomyHealthSnapshot BuildAnatomySnapshot(CharacterBodyHealthState state)
    {
        AnatomyProfileDefinition profile = ResolveProfile(state.anatomyProfileId);
        EnsureAnatomy(state, profile);
        float consciousness = CalculateFunctionEfficiency(
            state,
            profile,
            AnatomyFunction.Consciousness,
            defaultValue: 1f);
        float sight = CalculateFunctionEfficiency(
            state,
            profile,
            AnatomyFunction.Sight,
            defaultValue: 1f);
        float breathing = CalculateFunctionEfficiency(
            state,
            profile,
            AnatomyFunction.Breathing,
            defaultValue: 1f);
        float digestion = CalculateFunctionEfficiency(
            state,
            profile,
            AnatomyFunction.Digestion,
            defaultValue: 1f);
        float filtration = CalculateFunctionEfficiency(
            state,
            profile,
            AnatomyFunction.Filtration,
            defaultValue: 1f);
        float manipulation = CalculateFunctionEfficiency(
            state,
            profile,
            AnatomyFunction.Manipulation,
            defaultValue: 1f);
        float mobility = CalculateFunctionEfficiency(
            state,
            profile,
            AnatomyFunction.Mobility,
            defaultValue: 1f);
        float core = CalculateFunctionEfficiency(
            state,
            profile,
            AnatomyFunction.Core,
            defaultValue: 1f);
        consciousness = Mathf.Min(consciousness, core);
        breathing = Mathf.Min(breathing, core);
        digestion = Mathf.Min(digestion, core);
        filtration = Mathf.Min(filtration, core);
        return new AnatomyHealthSnapshot(
            profile.ProfileId,
            state.anatomyNodes.Select(CloneAnatomyNode).ToArray(),
            consciousness,
            sight,
            breathing,
            digestion,
            filtration,
            manipulation,
            mobility);
    }

    private static float CalculateFunctionEfficiency(
        CharacterBodyHealthState state,
        AnatomyProfileDefinition profile,
        AnatomyFunction function,
        float defaultValue)
    {
        float weightedTotal = 0f;
        float totalWeight = 0f;
        foreach (AnatomyNodeDefinition definition in profile.Nodes)
        {
            if ((definition.Functions & function) == 0)
            {
                continue;
            }

            AnatomyNodeHealthState node = FindAnatomyNode(state, definition.NodeId);
            float weight = Mathf.Max(0.01f, definition.CapacityWeight);
            weightedTotal += (node?.EffectiveEfficiency ?? 0f) * weight;
            totalWeight += weight;
        }

        return totalWeight > 0f
            ? Mathf.Clamp01(weightedTotal / totalWeight)
            : Mathf.Clamp01(defaultValue);
    }

    private static float GetStateBleeding(CharacterBodyHealthState state)
    {
        if (state.anatomyNodes != null && state.anatomyNodes.Count > 0)
        {
            return state.anatomyNodes.Sum(node =>
                Mathf.Max(0f, node.bleedingPerSecond));
        }

        return state.parts.Sum(part =>
            Mathf.Max(0f, part.bleedingPerSecond));
    }

    private void ApplyLegacyDamageToAnatomy(
        CharacterBodyHealthState state,
        CombatBodyPart bodyPart,
        float damage,
        float bleeding)
    {
        AnatomyProfileDefinition profile = ResolveProfile(state.anatomyProfileId);
        AnatomyNodeDefinition definition = profile.Nodes.FirstOrDefault(node =>
            node.MapsToLegacyBodyPart && node.LegacyBodyPart == bodyPart);
        if (definition == null)
        {
            return;
        }

        AnatomyNodeHealthState node = FindAnatomyNode(state, definition.NodeId);
        if (node == null)
        {
            return;
        }

        node.currentHealth = Mathf.Max(0f, node.currentHealth - damage);
        node.bleedingPerSecond += Mathf.Max(0f, bleeding);
    }

    private static void SyncAnatomySurfaceNodesFromLegacy(CharacterBodyHealthState state)
    {
        if (state?.anatomyNodes == null || state.anatomyNodes.Count == 0)
        {
            return;
        }

        foreach (CharacterBodyPartHealthState legacy in state.parts)
        {
            string nodeId = GetSurfaceNodeId(state.anatomyProfileId, legacy.bodyPart);
            AnatomyNodeHealthState node = FindAnatomyNode(state, nodeId);
            if (node == null || node.missing)
            {
                continue;
            }

            node.currentHealth = node.maxHealth * legacy.HealthRatio;
            node.bleedingPerSecond = legacy.bleedingPerSecond;
        }
    }

    private static void SyncLegacySurfaceNode(
        CharacterBodyHealthState state,
        string nodeId)
    {
        AnatomyNodeHealthState node = FindAnatomyNode(state, nodeId);
        if (node == null)
        {
            return;
        }

        CombatBodyPart? bodyPart = GetLegacyBodyPart(state.anatomyProfileId, nodeId);
        if (!bodyPart.HasValue)
        {
            return;
        }

        CharacterBodyPartHealthState legacy = state.parts.FirstOrDefault(
            part => part.bodyPart == bodyPart.Value);
        if (legacy == null)
        {
            return;
        }

        legacy.currentHealth = legacy.maxHealth * node.HealthRatio;
        legacy.bleedingPerSecond = node.bleedingPerSecond;
    }

    private void KillForDestroyedVitalNode(
        CharacterActor actor,
        CharacterBodyHealthState state,
        string nodeId)
    {
        AnatomyProfileDefinition profile = ResolveProfile(state.anatomyProfileId);
        if (!profile.TryGetNode(nodeId, out AnatomyNodeDefinition definition)
            || !definition.Vital)
        {
            return;
        }

        AnatomyNodeHealthState node = FindAnatomyNode(state, nodeId);
        if (node != null && (node.missing || node.currentHealth <= 0f) && !actor.IsDead)
        {
            actor.Die($"{definition.DisplayName} 기능 상실");
        }
    }

    private static AnatomyNodeHealthState FindAnatomyNode(
        CharacterBodyHealthState state,
        string nodeId)
    {
        return state?.anatomyNodes?.FirstOrDefault(node =>
            node != null && string.Equals(
                node.nodeId,
                nodeId?.Trim(),
                StringComparison.Ordinal));
    }

    private static string GetSurfaceNodeId(
        string profileId,
        CombatBodyPart bodyPart)
    {
        if (string.Equals(profileId, "anatomy:slime", StringComparison.Ordinal))
        {
            return bodyPart == CombatBodyPart.Head ? "core" : "membrane";
        }

        if (string.Equals(profileId, "anatomy:quadruped", StringComparison.Ordinal))
        {
            return bodyPart switch
            {
                CombatBodyPart.Head => "head",
                CombatBodyPart.Torso => "torso",
                CombatBodyPart.LeftLeg => "forelegs",
                CombatBodyPart.RightLeg => "hindlegs",
                _ => "torso"
            };
        }

        return bodyPart switch
        {
            CombatBodyPart.Head => "head",
            CombatBodyPart.Torso => "torso",
            CombatBodyPart.LeftArm => "arm:left",
            CombatBodyPart.RightArm => "arm:right",
            CombatBodyPart.LeftLeg => "leg:left",
            CombatBodyPart.RightLeg => "leg:right",
            _ => "torso"
        };
    }

    private static CombatBodyPart? GetLegacyBodyPart(
        string profileId,
        string nodeId)
    {
        if (string.Equals(profileId, "anatomy:slime", StringComparison.Ordinal))
        {
            return nodeId == "membrane" ? CombatBodyPart.Torso : null;
        }

        if (string.Equals(profileId, "anatomy:quadruped", StringComparison.Ordinal))
        {
            return nodeId switch
            {
                "head" => CombatBodyPart.Head,
                "torso" => CombatBodyPart.Torso,
                "forelegs" => CombatBodyPart.LeftLeg,
                "hindlegs" => CombatBodyPart.RightLeg,
                _ => null
            };
        }

        return nodeId switch
        {
            "head" => CombatBodyPart.Head,
            "torso" => CombatBodyPart.Torso,
            "arm:left" => CombatBodyPart.LeftArm,
            "arm:right" => CombatBodyPart.RightArm,
            "leg:left" => CombatBodyPart.LeftLeg,
            "leg:right" => CombatBodyPart.RightLeg,
            _ => null
        };
    }

    private static AnatomyNodeHealthState CloneAnatomyNode(
        AnatomyNodeHealthState source)
    {
        if (source == null)
        {
            return null;
        }

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

    private static AnatomyHealthSnapshot EmptyAnatomySnapshot()
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

    private static CharacterBodyHealthSnapshot EmptySnapshot()
    {
        return new CharacterBodyHealthSnapshot(
            Array.Empty<CharacterBodyPartHealthState>(),
            0f,
            0f,
            1f,
            1f,
            1f,
            false);
    }

    private static string GetId(CharacterActor actor)
    {
        string id = actor?.Identity?.PersistentId;
        return !string.IsNullOrWhiteSpace(id)
            ? id
            : $"scene-actor:{actor?.GetInstanceID() ?? 0}";
    }
}
