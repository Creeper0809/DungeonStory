using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using Unity.Profiling;
using UnityEngine;
using VContainer.Unity;
public sealed class CharacterBodyHealthRuntime :
    ICharacterBodyHealthQuery,
    ICharacterBodyHealthCommand,
    ICharacterBodyHealthPersistence,
    IAnatomyHealthRuntime,
    IAnatomyEffectRuntime,
    ITickable
{
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("CharacterBodyHealthRuntime.Tick");

    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IGameClock gameClock;
    private readonly IGameEventBus gameEventBus;
    private readonly IDynamicFrameWorkBudget frameWorkBudget;
    private readonly CharacterBodyHealthStateRules stateRules;
    private readonly IAnatomyActivityProfileCatalog anatomyActivities;
    private readonly CharacterVitalsAuthority vitalsAuthority;
    private readonly Dictionary<CharacterId, CharacterActor> trackedActors =
        new Dictionary<CharacterId, CharacterActor>();
    private readonly Dictionary<CharacterId, float> lastTickAt =
        new Dictionary<CharacterId, float>();
    private readonly List<CharacterId> tickStateIds = new List<CharacterId>();
    private int tickStateIndex;
    private bool tickPassActive;
    private int observedAggregateRevision;

    public CharacterBodyHealthRuntime(
        ICharacterAiWorldRegistry worldRegistry,
        IGameClock gameClock,
        IGameEventBus gameEventBus,
        IDynamicFrameWorkBudget frameWorkBudget,
        IAnatomyProfileCatalog anatomyProfiles,
        IAnatomyActivityProfileCatalog anatomyActivities,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        this.worldRegistry = worldRegistry ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.gameEventBus = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.frameWorkBudget = frameWorkBudget
            ?? throw new ArgumentNullException(nameof(frameWorkBudget));
        stateRules = new CharacterBodyHealthStateRules(anatomyProfiles);
        this.anatomyActivities = anatomyActivities
            ?? throw new ArgumentNullException(nameof(anatomyActivities));
        vitalsAuthority = new CharacterVitalsAuthority(
            aggregateRootStore ?? throw new ArgumentNullException(nameof(aggregateRootStore)),
            stateRules);
        observedAggregateRevision = vitalsAuthority.PublishedRestoreRevision;
    }

    private CharacterVitalsAggregateState ReadState => vitalsAuthority.ReadState;

    private CharacterVitalsAggregateState WriteState => vitalsAuthority.WriteState;

    public void Tick()
    {
        using (TickProfilerMarker.Auto())
        {
            TickRuntime();
        }
    }

    private void TickRuntime()
    {
        EnsureAggregateRevision();
        if (gameClock.DeltaTime <= 0f)
        {
            return;
        }

        if (!tickPassActive)
        {
            if (ReadState.Characters.Count == 0)
            {
                frameWorkBudget.SetBacklog(
                    DynamicFrameWorkDomain.CharacterHealth,
                    0);
                return;
            }

            tickStateIds.Clear();
            foreach (CharacterId id in ReadState.Characters.Keys)
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
            CharacterId id = tickStateIds[tickStateIndex++];
            processed++;
            if (!WriteState.TryGet(id, out CharacterBodyHealthState state))
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
            float bleeding = stateRules.GetStateBleeding(state);

            if (bleeding > 0f)
            {
                state.bloodLoss = Mathf.Clamp(state.bloodLoss + bleeding * delta, 0f, 100f);
                ApplyAggregateDamage(
                    actor,
                    state,
                    bleeding * 0.12f * delta,
                    "출혈",
                    allowDeath: false);
                if (state.bloodLoss >= 100f && !actor.IsDead)
                {
                    Kill(actor, "과다 출혈");
                }
            }

            TickAnatomyComplications(actor, state, delta);
            state.suppression = Mathf.Max(0f, state.suppression - 5f * delta);
            bool wasDowned = state.downed;
            stateRules.UpdateDowned(state);
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
            ? stateRules.EmptySnapshot()
            : stateRules.BuildSnapshot(GetOrCreate(actor));
    }

    public CharacterVitalsSnapshot GetVitals(CharacterActor actor)
    {
        return actor == null
            ? vitalsAuthority.GetVitals(default(CharacterId))
            : vitalsAuthority.GetVitals(GetOrCreate(actor));
    }

    public CharacterVitalsSnapshot GetVitals(string characterId) =>
        vitalsAuthority.GetVitals((CharacterId)characterId);

    public void ConfigureVitals(
        CharacterActor actor,
        float maximumHealth,
        bool resetCurrentHealth)
    {
        if (actor == null)
        {
            return;
        }

        vitalsAuthority.Configure(
            actor,
            GetOrCreate(actor),
            maximumHealth,
            resetCurrentHealth);
    }

    public void RestoreLegacyVitalsProjection(
        CharacterActor actor,
        float maximumHealth,
        float currentHealth,
        float injurySeverity)
    {
        if (actor == null)
        {
            return;
        }

        vitalsAuthority.RestoreLegacyProjection(
            actor,
            GetOrCreate(actor),
            maximumHealth,
            currentHealth,
            injurySeverity);
    }

    public void ApplyLegacyDamage(
        CharacterActor actor,
        float amount,
        string reason,
        bool allowDeath)
    {
        if (actor == null || amount <= 0f)
        {
            return;
        }

        ApplyAggregateDamage(actor, GetOrCreate(actor), amount, reason, allowDeath);
    }

    public void HealLegacyVitals(CharacterActor actor, float amount)
    {
        if (actor == null || amount <= 0f)
        {
            return;
        }

        ApplyAggregateHealing(actor, GetOrCreate(actor), amount);
    }

    public void ScaleLegacyVitals(CharacterActor actor, float multiplier)
    {
        if (actor == null)
        {
            return;
        }

        vitalsAuthority.Scale(actor, GetOrCreate(actor), multiplier);
    }

    public void SetLegacyInjurySeverity(CharacterActor actor, float injurySeverity)
    {
        if (actor == null)
        {
            return;
        }

        vitalsAuthority.SetInjurySeverity(
            actor,
            GetOrCreate(actor),
            injurySeverity);
    }

    public void Kill(CharacterActor actor, string reason)
    {
        if (actor == null)
        {
            return;
        }

        vitalsAuthority.Kill(actor, GetOrCreate(actor), reason);
    }

    public CharacterBodyHealthSnapshot GetSnapshot(string characterId)
    {
        CharacterId id = (CharacterId)characterId;
        return id.IsValid
            && ReadState.TryGet(id, out CharacterBodyHealthState state)
                ? stateRules.BuildSnapshot(state)
                : stateRules.EmptySnapshot();
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
            stateRules.UpdateDowned(state);
            SyncLifecycle(target, state, wasDowned);
            return;
        }

        CharacterBodyPartHealthState part = state.parts.First(item => item.bodyPart == result.BodyPart);
        part.currentHealth = Mathf.Max(0f, part.currentHealth - result.AppliedDamage);
        part.bleedingPerSecond += result.Bleeding * 0.01f;
        stateRules.ApplyLegacyDamageToAnatomy(
            state,
            result.BodyPart,
            result.AppliedDamage,
            result.Bleeding * 0.01f);
        state.lastDamageReason = reason ?? string.Empty;
        ApplyAggregateDamage(
            target,
            state,
            result.AppliedDamage,
            reason,
            allowDeath: false);

        if (!target.IsDead
            && (result.BodyPart == CombatBodyPart.Head || result.BodyPart == CombatBodyPart.Torso)
            && part.currentHealth <= 0f)
        {
            Kill(
                target,
                result.BodyPart == CombatBodyPart.Head ? "머리 치명상" : "몸통 치명상");
        }

        bool wasDownedAfterHit = state.downed;
        stateRules.UpdateDowned(state);
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
        state.parts = snapshot.Parts.Select(stateRules.ClonePart).ToList();
        stateRules.EnsureParts(state);
        stateRules.SyncAnatomySurfaceNodesFromLegacy(state);
        state.bloodLoss = Mathf.Clamp(snapshot.BloodLoss, 0f, 100f);
        state.suppression = Mathf.Clamp(snapshot.Suppression, 0f, 100f);
        state.lastDamageReason = reason ?? string.Empty;
        bool wasDowned = state.downed;
        stateRules.UpdateDowned(state);
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
        stateRules.UpdateDowned(state);
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

        stateRules.SyncAnatomySurfaceNodesFromLegacy(state);
        state.bloodLoss = Mathf.Max(0f, state.bloodLoss - amount * 0.5f);
        bool wasDowned = state.downed;
        ApplyAggregateHealing(target, state, amount);
        stateRules.UpdateDowned(state);
        SyncLifecycle(target, state, wasDowned);
    }

    public float GetTotalBleeding(CharacterActor target)
    {
        if (target == null)
        {
            return 0f;
        }

        CharacterBodyHealthState state = GetOrCreate(target);
        return stateRules.GetStateBleeding(state);
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
        stateRules.UpdateDowned(state);
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

        stateRules.SyncAnatomySurfaceNodesFromLegacy(state);
        float previousBloodLoss = state.bloodLoss;
        state.bloodLoss = Mathf.Max(0f, state.bloodLoss - Mathf.Max(0f, bloodLossReduction));
        bool wasDowned = state.downed;
        if (restoredTotal > 0f)
        {
            ApplyAggregateHealing(target, state, restoredTotal);
        }

        stateRules.UpdateDowned(state);
        SyncLifecycle(target, state, wasDowned);
        return restoredTotal > 0f || state.bloodLoss < previousBloodLoss;
    }

    public DungeonCharacterBodyHealthSaveData Capture() => vitalsAuthority.Capture();

    public CharacterBodyHealthRestoreCandidate PrepareRestore(
        DungeonCharacterBodyHealthSaveData saveData) =>
        vitalsAuthority.PrepareRestore(saveData);

    public void PublishRestore(CharacterBodyHealthRestoreCandidate candidate)
    {
        vitalsAuthority.PublishRestore(candidate);
        if (!vitalsAuthority.IsRestoreStaging)
        {
            ResetDerivedCaches();
        }
    }

    private CharacterBodyHealthState GetOrCreate(CharacterActor actor)
    {
        EnsureAggregateRevision();
        CharacterId id = GetId(actor);
        if (actor != null)
        {
            trackedActors[id] = actor;
        }

        if (!lastTickAt.ContainsKey(id))
        {
            lastTickAt[id] = gameClock.Time;
        }

        CharacterBodyHealthState state = GetOrCreate(id);
        AnatomyProfileDefinition profile = stateRules.ResolveForSpecies(
            actor?.SpeciesTag);
        stateRules.EnsureAnatomy(state, profile);
        vitalsAuthority.Project(actor, state);
        return state;
    }

    private CharacterBodyHealthState GetOrCreate(CharacterId characterId)
    {
        if (!characterId.IsValid)
        {
            throw new InvalidOperationException(
                "Character vitals require a persistent CharacterId.");
        }

        if (WriteState.TryGet(characterId, out CharacterBodyHealthState state))
        {
            stateRules.EnsureParts(state);
            stateRules.EnsureAnatomy(
                state,
                stateRules.ResolveProfile(state.anatomyProfileId));
            return state;
        }

        state = new CharacterBodyHealthState
        {
            characterId = characterId.Value
        };
        stateRules.EnsureParts(state);
        stateRules.EnsureAnatomy(
            state,
            stateRules.ResolveProfile(string.Empty));
        WriteState.Set(characterId, state);
        return state;
    }

    public AnatomyHealthSnapshot GetAnatomySnapshot(CharacterActor actor)
    {
        return actor == null
            ? stateRules.EmptyAnatomySnapshot()
            : stateRules.BuildAnatomySnapshot(GetOrCreate(actor));
    }

    public AnatomyHealthSnapshot GetAnatomySnapshot(string characterId)
    {
        CharacterId id = (CharacterId)characterId;
        if (!id.IsValid
            || !ReadState.TryGet(id, out CharacterBodyHealthState state))
        {
            return stateRules.EmptyAnatomySnapshot();
        }

        stateRules.EnsureAnatomy(
            state,
            stateRules.ResolveProfile(state.anatomyProfileId));
        return stateRules.BuildAnatomySnapshot(state);
    }

    public AnatomyActionAxisSnapshot GetActionAxes(CharacterActor actor)
    {
        return actor == null
            ? stateRules.DefaultActionAxes()
            : stateRules.BuildActionAxes(GetOrCreate(actor));
    }

    public AnatomyActionAxisSnapshot GetActionAxes(string characterId)
    {
        CharacterId id = (CharacterId)characterId;
        if (!id.IsValid
            || !ReadState.TryGet(id, out CharacterBodyHealthState state))
        {
            return stateRules.DefaultActionAxes();
        }

        return stateRules.BuildActionAxes(state);
    }

    public AnatomyActivityFactorSnapshot GetActivityFactor(
        CharacterActor actor,
        AnatomyActivityId activity)
    {
        AnatomyActionAxisSnapshot axes = GetActionAxes(actor);
        AnatomyActivityProfile profile = anatomyActivities.Get(activity);
        float raw = 1f;
        foreach (AnatomyNodeAxisContribution weight in profile.AxisWeights)
        {
            if (weight == null || weight.Weight <= 0f)
            {
                continue;
            }

            raw += (axes.Get(weight.Axis) - 1f) * weight.Weight;
        }

        raw = Mathf.Max(0f, raw);
        return new AnatomyActivityFactorSnapshot(
            activity,
            raw,
            Mathf.Min(raw, profile.MaximumFactor),
            profile.MaximumFactor);
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
        AnatomyNodeHealthState node = stateRules.FindAnatomyNode(state, nodeId);
        if (node == null || node.missing)
        {
            return false;
        }

        node.currentHealth = Mathf.Max(0f, node.currentHealth - damage);
        node.bleedingPerSecond += Mathf.Max(0f, bleeding);
        state.lastDamageReason = reason ?? string.Empty;
        ApplyAggregateDamage(actor, state, damage, reason, allowDeath: false);
        stateRules.SyncLegacySurfaceNode(state, node.nodeId);
        stateRules.KillForDestroyedVitalNode(actor, state, node.nodeId);
        bool wasDowned = state.downed;
        stateRules.UpdateDowned(state);
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
        AnatomyNodeHealthState node = stateRules.FindAnatomyNode(state, nodeId);
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
        stateRules.SyncLegacySurfaceNode(state, node.nodeId);
        float restored = node.currentHealth - previousHealth;
        if (restored > 0f)
        {
            ApplyAggregateHealing(actor, state, restored);
        }

        bool wasDowned = state.downed;
        stateRules.UpdateDowned(state);
        SyncLifecycle(actor, state, wasDowned);
        return restored > 0f || node.infection < previousInfection;
    }

    public PartRecoveryPolicy GetRecoveryPolicy(
        CharacterActor actor,
        string nodeId)
    {
        AnatomyNodeHealthState node = actor != null
            ? stateRules.FindAnatomyNode(GetOrCreate(actor), nodeId)
            : null;
        return node?.recoveryPolicy ?? PartRecoveryPolicy.Natural;
    }

    public bool CanRecoverNaturally(
        CharacterActor actor,
        string nodeId)
    {
        return GetRecoveryPolicy(actor, nodeId) == PartRecoveryPolicy.Natural;
    }

    public bool TryMaintainNode(
        CharacterActor actor,
        string nodeId,
        float durability,
        float contaminationReduction,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (actor == null || actor.IsDead)
        {
            failure = new DomainFailure(FailureCode.SurgeryLivingSubjectUnavailable);
            return false;
        }

        CharacterBodyHealthState state = GetOrCreate(actor);
        AnatomyNodeHealthState node = stateRules.FindAnatomyNode(state, nodeId);
        if (node == null || node.missing)
        {
            failure = new DomainFailure(FailureCode.SurgeryTargetNodeUnavailable, nodeId);
            return false;
        }

        if (node.recoveryPolicy == PartRecoveryPolicy.Natural)
        {
            failure = new DomainFailure(FailureCode.SurgeryEffectFailed, nodeId);
            return false;
        }

        if (node.recoveryPolicy == PartRecoveryPolicy.ReplacementOnly
            && node.currentHealth <= 0f)
        {
            failure = new DomainFailure(FailureCode.SurgeryPartUnavailable, nodeId);
            return false;
        }

        float previousHealth = node.currentHealth;
        float previousContamination = node.infection;
        node.currentHealth = Mathf.Min(
            node.maxHealth,
            node.currentHealth + Mathf.Max(0f, durability));
        node.infection = Mathf.Max(
            0f,
            node.infection - Mathf.Max(0f, contaminationReduction));
        stateRules.SyncLegacySurfaceNode(state, node.nodeId);
        float restored = node.currentHealth - previousHealth;
        if (restored > 0f)
        {
            ApplyAggregateHealing(actor, state, restored);
        }

        bool wasDowned = state.downed;
        stateRules.UpdateDowned(state);
        SyncLifecycle(actor, state, wasDowned);
        return restored > 0f || node.infection < previousContamination;
    }

    public bool TryRemoveNode(
        CharacterActor actor,
        string nodeId,
        out AnatomyNodeHealthState removedNode,
        out DomainFailure failure)
    {
        removedNode = null;
        failure = DomainFailure.None;
        if (actor == null || actor.IsDead)
        {
            failure = new DomainFailure(FailureCode.SurgeryLivingSubjectUnavailable);
            return false;
        }

        CharacterBodyHealthState state = GetOrCreate(actor);
        AnatomyProfileDefinition profile = stateRules.ResolveProfile(state.anatomyProfileId);
        if (!profile.TryGetNode(nodeId, out AnatomyNodeDefinition definition))
        {
            failure = new DomainFailure(FailureCode.SurgeryTargetNodeMissing, nodeId);
            return false;
        }

        AnatomyNodeHealthState node = stateRules.FindAnatomyNode(state, nodeId);
        if (node == null || node.missing)
        {
            failure = new DomainFailure(FailureCode.SurgeryTargetNodeUnavailable, nodeId);
            return false;
        }

        if (!definition.Removable)
        {
            failure = new DomainFailure(FailureCode.SurgeryTargetNodeUnavailable, nodeId);
            return false;
        }

        removedNode = stateRules.CloneAnatomyNode(node);
        node.missing = true;
        node.currentHealth = 0f;
        node.bleedingPerSecond = Mathf.Max(node.bleedingPerSecond, 0.35f);
        node.installedPartId = string.Empty;
        node.installedPartEfficiency = 0f;
        stateRules.SyncLegacySurfaceNode(state, node.nodeId);
        stateRules.KillForDestroyedVitalNode(actor, state, node.nodeId);
        bool wasDowned = state.downed;
        stateRules.UpdateDowned(state);
        SyncLifecycle(actor, state, wasDowned);
        return true;
    }

    public bool TryInstallPart(
        CharacterActor actor,
        string nodeId,
        string partInstanceId,
        SurgicalPartKind partKind,
        float efficiency,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (actor == null || actor.IsDead)
        {
            failure = new DomainFailure(FailureCode.SurgeryLivingSubjectUnavailable);
            return false;
        }

        CharacterBodyHealthState state = GetOrCreate(actor);
        AnatomyNodeHealthState node = stateRules.FindAnatomyNode(state, nodeId);
        if (node == null)
        {
            failure = new DomainFailure(FailureCode.SurgeryTargetNodeMissing, nodeId);
            return false;
        }

        if (string.IsNullOrWhiteSpace(partInstanceId))
        {
            failure = new DomainFailure(FailureCode.SurgeryPartUnavailable);
            return false;
        }

        node.missing = false;
        node.installedPartId = partInstanceId.Trim();
        node.installedPartKind = partKind;
        node.installedPartEfficiency = Mathf.Clamp(efficiency, 0.1f, 1.75f);
        node.moduleBonus = 0f;
        node.recoveryPolicy = stateRules.ResolveRecoveryPolicy(partKind);
        node.currentHealth = Mathf.Max(node.currentHealth, node.maxHealth * 0.35f);
        node.bleedingPerSecond = Mathf.Min(node.bleedingPerSecond, 0.05f);
        stateRules.SyncLegacySurfaceNode(state, node.nodeId);
        bool wasDowned = state.downed;
        stateRules.UpdateDowned(state);
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
        out DomainFailure failure)
    {
        replacedNode = null;
        failure = DomainFailure.None;
        if (actor == null || actor.IsDead)
        {
            failure = new DomainFailure(FailureCode.SurgeryLivingSubjectUnavailable);
            return false;
        }

        CharacterBodyHealthState state = GetOrCreate(actor);
        AnatomyNodeHealthState node = stateRules.FindAnatomyNode(state, nodeId);
        if (node == null)
        {
            failure = new DomainFailure(FailureCode.SurgeryTargetNodeMissing, nodeId);
            return false;
        }

        if (string.IsNullOrWhiteSpace(partInstanceId))
        {
            failure = new DomainFailure(FailureCode.SurgeryPartUnavailable);
            return false;
        }

        replacedNode = stateRules.CloneAnatomyNode(node);
        node.missing = false;
        node.installedPartId = partInstanceId.Trim();
        node.installedPartKind = partKind;
        node.installedPartEfficiency = Mathf.Clamp(efficiency, 0.1f, 1.75f);
        node.moduleBonus = 0f;
        node.recoveryPolicy = stateRules.ResolveRecoveryPolicy(partKind);
        node.currentHealth = Mathf.Max(node.maxHealth * 0.35f, 1f);
        node.bleedingPerSecond = Mathf.Min(node.bleedingPerSecond, 0.05f);
        stateRules.SyncLegacySurfaceNode(state, node.nodeId);
        bool wasDowned = state.downed;
        stateRules.UpdateDowned(state);
        SyncLifecycle(actor, state, wasDowned);
        return true;
    }

    public bool TryAddNodeBurden(
        CharacterActor actor,
        string nodeId,
        float rejection,
        float mutation,
        float infection,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (actor == null || actor.IsDead)
        {
            failure = new DomainFailure(FailureCode.SurgeryLivingSubjectUnavailable);
            return false;
        }

        CharacterBodyHealthState state = GetOrCreate(actor);
        AnatomyNodeHealthState node = stateRules.FindAnatomyNode(state, nodeId);
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
        CharacterActor actor,
        string nodeId,
        float rejection,
        float mutation,
        float infection,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (actor == null || actor.IsDead)
        {
            failure = new DomainFailure(FailureCode.SurgeryLivingSubjectUnavailable);
            return false;
        }

        AnatomyNodeHealthState node = stateRules.FindAnatomyNode(
            GetOrCreate(actor),
            nodeId);
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

        AnatomyProfileDefinition profile = stateRules.ResolveProfile(
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
            ApplyAggregateDamage(
                actor,
                state,
                damage * 0.35f,
                "수술 후 합병증",
                allowDeath: false);
            if (node.currentHealth <= 0f
                && profile.TryGetNode(
                    node.nodeId,
                    out AnatomyNodeDefinition definition)
                && definition.Vital)
            {
                Kill(actor, $"{definition.DisplayName} 기능 상실");
                return;
            }
        }
    }

    private void ApplyAggregateDamage(
        CharacterActor actor,
        CharacterBodyHealthState state,
        float amount,
        string reason,
        bool allowDeath)
    {
        vitalsAuthority.Damage(actor, state, amount, reason, allowDeath);
    }

    private void ApplyAggregateHealing(
        CharacterActor actor,
        CharacterBodyHealthState state,
        float amount)
    {
        vitalsAuthority.Heal(actor, state, amount);
    }

    private void EnsureAggregateRevision()
    {
        int revision = vitalsAuthority.PublishedRestoreRevision;
        if (observedAggregateRevision == revision)
        {
            return;
        }

        observedAggregateRevision = revision;
        ResetDerivedCaches();
    }

    private void ResetDerivedCaches()
    {
        trackedActors.Clear();
        lastTickAt.Clear();
        tickStateIds.Clear();
        tickStateIndex = 0;
        tickPassActive = false;
    }

    private CharacterActor ResolveActor(CharacterId characterId)
    {
        EnsureAggregateRevision();
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
            if (actor != null && GetId(actor).Equals(characterId))
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
                gameEventBus.Publish(new CharacterBodyHealthDownedEvent(actor));
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
            gameEventBus.Publish(new CharacterBodyHealthRecoveredEvent(actor));
            if (Application.isPlaying)
            {
                DefenseCombatPresentation presentation =
                    DefenseCombatPresentation.Ensure(actor);
                presentation?.SetDowned(false);
                presentation?.SetStatus("회복 중", combatActive: false);
            }
        }
    }


    private static CharacterId GetId(CharacterActor actor)
    {
        return actor != null
            ? CharacterPersistentIdentity.Require(actor)
            : default;
    }
}
