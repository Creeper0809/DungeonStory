using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Unity.Profiling;
using UnityEngine;
using VContainer;
using VContainer.Unity;
public sealed class CharacterBodyHealthRuntime :
    ICharacterBodyHealthQuery,
    ICharacterBodyHealthCommand,
    ICharacterCombatSpecialStatusQuery,
    ICharacterManaQuery,
    ICharacterManaCommand,
    ICharacterBodyHealthMutationTransaction,
    IAgeConditionAnatomyMutationTransaction,
    IDiseaseAnatomyBurdenMutationTransaction,
    ICharacterBodyHealthPersistence,
    IAnatomyHealthRuntime,
    ITickable
{
    private const float PanicSuppressionThreshold = 80f;
    private const float BaseManaRecoveryPerGameHour = 8f;
    private static readonly ProfilerMarker TickProfilerMarker =
        new ProfilerMarker("CharacterBodyHealthRuntime.Tick");

    private readonly ICharacterAiWorldRegistry worldRegistry;
    private readonly IGameClock gameClock;
    private readonly IGameCalendar gameCalendar;
    private readonly IGameEventBus gameEventBus;
    private readonly IDynamicFrameWorkBudget frameWorkBudget;
    private readonly CharacterBodyHealthStateRules stateRules;
    private readonly CharacterVitalsAuthority vitalsAuthority;
    private IDomainFactPublisher facts;
    private IDomainFactRecoveryPublisher factRecovery;
    private float nextHealingRecoveryTime;
    private string lastHealingFactFailure = string.Empty;
    private string lastFatalityFactFailure = string.Empty;
    private readonly Dictionary<CharacterId, CharacterActor> trackedActors =
        new Dictionary<CharacterId, CharacterActor>();
    private readonly Dictionary<CharacterId, float> lastTickAt =
        new Dictionary<CharacterId, float>();
    private readonly List<CharacterId> tickStateIds = new List<CharacterId>();
    private int tickStateIndex;
    private bool tickPassActive;
    private int observedAggregateRevision;
#if UNITY_EDITOR
    private bool creationDiagnosticsEnabledForEditor;
    private readonly List<string> creationDiagnosticsForEditor = new();
#endif

    public CharacterBodyHealthRuntime(
        ICharacterAiWorldRegistry worldRegistry,
        IGameClock gameClock,
        IGameEventBus gameEventBus,
        IDynamicFrameWorkBudget frameWorkBudget,
        IAnatomyProfileCatalog anatomyProfiles,
        DungeonRuntimeAggregateRootStore aggregateRootStore,
        IGameCalendar gameCalendar)
    {
        this.worldRegistry = worldRegistry ?? throw new ArgumentNullException(nameof(worldRegistry));
        this.gameClock = gameClock ?? throw new ArgumentNullException(nameof(gameClock));
        this.gameCalendar = gameCalendar ?? throw new ArgumentNullException(nameof(gameCalendar));
        this.gameEventBus = gameEventBus ?? throw new ArgumentNullException(nameof(gameEventBus));
        this.frameWorkBudget = frameWorkBudget
            ?? throw new ArgumentNullException(nameof(frameWorkBudget));
        stateRules = new CharacterBodyHealthStateRules(anatomyProfiles);
        vitalsAuthority = new CharacterVitalsAuthority(
            aggregateRootStore ?? throw new ArgumentNullException(nameof(aggregateRootStore)),
            stateRules);
        observedAggregateRevision = vitalsAuthority.PublishedRestoreRevision;
    }

    private CharacterVitalsAggregateState ReadState => vitalsAuthority.ReadState;

    [Inject]
    public void ConfigureFactPublication(IDomainFactPublisher publisher, IDomainFactRecoveryPublisher recovery)
    {
        facts = publisher ?? throw new ArgumentNullException(nameof(publisher));
        factRecovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
    }

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
        if (Time.realtimeSinceStartup >= nextHealingRecoveryTime)
        {
            nextHealingRecoveryTime = Time.realtimeSinceStartup + 0.5f;
            RecoverPendingHealingFacts();
            RecoverPendingFatalityFacts();
        }
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
                    Kill(actor, CharacterDeathCauseCode.Combat, "combat:blood-loss");
                }
            }

            if (state.burningRemainingSeconds > 0f
                && state.burningDamagePerSecond > 0f)
            {
                float burningDelta = Mathf.Min(
                    delta,
                    state.burningRemainingSeconds);
                state.burningRemainingSeconds = Mathf.Max(
                    0f,
                    state.burningRemainingSeconds - delta);
                ApplyAggregateDamage(
                    actor,
                    state,
                    state.burningDamagePerSecond * burningDelta,
                    "combat:incendiary-burning",
                    allowDeath: false);
                if (state.burningRemainingSeconds <= 0f)
                    state.burningDamagePerSecond = 0f;
            }
            state.sedationRemainingSeconds = Mathf.Max(
                0f,
                state.sedationRemainingSeconds - delta);
            if (state.sedationRemainingSeconds <= 0f)
                state.sedationRatio = 0f;
            bool requiresManaRecovery =
                state.manaBlockedRemainingSeconds > 0f
                || state.currentMana < state.maxMana;
            if (requiresManaRecovery)
            {
                CharacterPerformanceSnapshot manaRecovery = actor.Stats != null
                    ? actor.Stats.EvaluatePerformance(
                        CharacterPerformanceFormulaIds.ManaRecovery)
                    : null;
                float manaRecoveryMultiplier = manaRecovery?.IsApplicable == true
                    ? manaRecovery.Value
                    : 0f;
                state.manaBlockedRemainingSeconds = Mathf.Max(
                    0f,
                    state.manaBlockedRemainingSeconds
                        - delta * Mathf.Max(0f, manaRecoveryMultiplier));
                if (state.manaBlockedRemainingSeconds <= 0f
                    && state.currentMana < state.maxMana)
                {
                    float manaBeforeRecovery = state.currentMana;
                    float secondsPerGameHour =
                        GameCalendarRules.SecondsPerDay
                        / GameCalendarRules.HoursPerDay;
                    state.currentMana = Mathf.Min(
                        state.maxMana,
                        state.currentMana
                            + delta
                            * BaseManaRecoveryPerGameHour
                            / secondsPerGameHour
                            * Mathf.Max(0f, manaRecoveryMultiplier));
                    CharacterPerformanceExecutionTrace.Record(
                        CharacterPerformanceFormulaIds.ManaRecovery,
                        "CharacterBodyHealthRuntime.Tick",
                        delta,
                        state.currentMana - manaBeforeRecovery,
                        id.Value);
                }
            }

            TickAnatomyComplications(actor, state, delta);
            RefreshDerivedMaximumHealthForInstalledPart(actor, state);
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
        CharacterBodyHealthState state = GetForQuery(actor);
        return state == null
            ? stateRules.EmptySnapshot()
            : stateRules.BuildSnapshot(state);
    }

    public CharacterVitalsSnapshot GetVitals(CharacterActor actor)
    {
        CharacterBodyHealthState state = GetForQuery(actor);
        return state == null
            ? vitalsAuthority.GetVitals(default(CharacterId))
            : vitalsAuthority.GetVitals(state);
    }

    public CharacterVitalsSnapshot GetVitals(string characterId) =>
        vitalsAuthority.GetVitals((CharacterId)characterId);

    public CharacterManaSnapshot GetMana(CharacterActor actor)
    {
        CharacterBodyHealthState state = GetForQuery(actor);
        return state == null
            ? new CharacterManaSnapshot(0f, 100f, recoveryBlocked: false)
            : new CharacterManaSnapshot(
                state.currentMana,
                state.maxMana,
                state.manaBlockedRemainingSeconds > 0f);
    }

    public bool CanSpendMana(
        CharacterActor actor,
        float amount,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (actor == null || amount <= 0f
            || float.IsNaN(amount) || float.IsInfinity(amount))
        {
            failureReason = "유효한 시전자와 양의 마나 비용이 필요합니다.";
            return false;
        }
        CharacterBodyHealthState state = GetForQuery(actor);
        if (state == null || state.currentMana + 0.0001f < amount)
        {
            failureReason = $"마나 부족: {state?.currentMana ?? 0f:0.#}/{amount:0.#}";
            return false;
        }
        return true;
    }

    [GameplayInternalOnly(
        "Combat and arcane commands spend mana before resolving their atomic action.",
        "CharacterCombatCommandRuntime|ArcaneOverchargeCommandRuntime")]
    public bool TrySpendMana(
        CharacterActor actor,
        float amount,
        out string failureReason)
    {
        if (!CanSpendMana(actor, amount, out failureReason))
            return false;
        CharacterBodyHealthState state = GetOrCreate(actor);
        state.currentMana = Mathf.Max(0f, state.currentMana - amount);
        return true;
    }

    [GameplayInternalOnly(
        "Only an unexecuted combat action may compensate its immediately preceding mana spend.",
        "CharacterCombatCommandRuntime")]
    public void RefundFailedManaSpend(CharacterActor actor, float amount)
    {
        if (actor == null || !float.IsFinite(amount) || amount <= 0f)
            return;
        CharacterBodyHealthState state = GetOrCreate(actor);
        state.currentMana = Mathf.Min(
            Mathf.Max(1f, state.maxMana),
            state.currentMana + amount);
    }

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

    [GameplayInternalOnly(
        "CharacterStats projects the current derived maximum after an installed-part state change.",
        "CharacterStatsVitalsService")]
    public void RefreshMaximumHealthPreservingCurrent(
        CharacterActor actor,
        float maximumHealth)
    {
        if (actor == null)
        {
            throw new ArgumentNullException(nameof(actor));
        }
        if (!float.IsFinite(maximumHealth) || maximumHealth <= 0f)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maximumHealth),
                maximumHealth,
                "Maximum health must be finite and positive.");
        }

        vitalsAuthority.RefreshMaximumHealthPreservingCurrent(
            actor,
            GetOrCreate(actor),
            maximumHealth);
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

        ApplyAggregateDamage(actor, GetOrCreate(actor), amount,
            CharacterDeathCauseCode.Unknown, reason, allowDeath);
    }

    public void ApplyLegacyDamageWithCause(
        CharacterActor actor,
        float amount,
        CharacterDeathCauseCode deathCause,
        string reasonCode,
        bool allowDeath)
    {
        if (actor == null || amount <= 0f)
        {
            return;
        }

        ApplyAggregateDamage(actor, GetOrCreate(actor), amount,
            deathCause, reasonCode, allowDeath);
    }

    public void ApplyLocalizedDamage(
        CharacterActor actor,
        CombatBodyPart bodyPart,
        float amount,
        string reason,
        bool allowDeath)
    {
        if (actor == null || actor.IsDead || amount <= 0f)
        {
            return;
        }

        CharacterBodyHealthState state = GetOrCreate(actor);
        CharacterBodyPartHealthState part = state.parts.FirstOrDefault(value =>
            value.bodyPart == bodyPart);
        if (part == null)
        {
            return;
        }

        float appliedDamage = allowDeath
            ? amount
            : Mathf.Min(
                amount,
                Mathf.Max(0f, state.currentHealth - 1f),
                Mathf.Max(0f, part.currentHealth - 1f));
        if (appliedDamage <= 0f)
        {
            return;
        }

        bool wasDowned = state.downed;
        part.currentHealth = Mathf.Max(0f, part.currentHealth - appliedDamage);
        stateRules.ApplyLegacyDamageToAnatomy(
            state,
            bodyPart,
            appliedDamage,
            bleeding: 0f);
        state.lastDamageReason = reason ?? string.Empty;
        ApplyAggregateDamage(
            actor,
            state,
            appliedDamage,
            CharacterDeathCauseCode.Unknown,
            reason,
            allowDeath);
        if (!actor.IsDead
            && bodyPart is CombatBodyPart.Head or CombatBodyPart.Torso
            && part.currentHealth <= 0f)
        {
            Kill(actor, CharacterDeathCauseCode.Unknown, reason);
        }

        stateRules.UpdateDowned(state);
        SyncLifecycle(actor, state, wasDowned);
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
        Kill(actor, CharacterDeathCauseCode.Unknown, reason);
    }

    public void Kill(
        CharacterActor actor,
        CharacterDeathCauseCode cause,
        string reasonCode)
    {
        if (actor == null) return;
        CharacterBodyHealthState state = GetOrCreate(actor);
        if (state.currentHealth <= 0f) return;
        RequireNoPendingBodyObservation(state.characterId);
        if (facts == null || factRecovery == null)
            throw new InvalidOperationException("character-fatality-fact-publisher-missing");
        if (ReadState.PendingFatalityFacts.Count >= CharacterFatalityFact.MaximumPending)
            throw new InvalidOperationException("character-fatality-pending-capacity");

        CharacterBodyHealthMutationSnapshot before = CaptureCombatMutation(actor);
        var data = new CharacterFatalityFactData
        {
            characterId = before.CharacterId.Value,
            characterName = actor.Identity?.DisplayName ?? actor.name,
            absoluteDay = gameCalendar.Day,
            cause = cause, reasonCode = reasonCode ?? string.Empty,
            healthBefore = state.currentHealth, maximumHealth = state.maxHealth,
            healthAfter = state.currentHealth, downedBefore = state.downed, downedAfter = state.downed
        };
        var proposal = new CharacterFatalityFact(data, proposalOnly: true);
        DomainFactLease deathLease = default, criticalLease = default, downedLease = default;
        try
        {
            if (!facts.TryReserve(proposal, out deathLease, out string failure)
                || !facts.TryReserve(new CharacterFatalCriticalTransitionFact(proposal), out criticalLease, out failure)
                || !facts.TryReserve(new CharacterFatalDownedFact(proposal), out downedLease, out failure))
                throw new InvalidOperationException("character-fatality-fact-reserve:" + failure);
            if (string.IsNullOrEmpty(deathLease.PublicationKey) || string.IsNullOrEmpty(criticalLease.PublicationKey)
                || string.IsNullOrEmpty(downedLease.PublicationKey))
                throw new InvalidOperationException("character-fatality-fact-reservation-identity-missing");
        }
        catch { facts.Cancel(deathLease); facts.Cancel(criticalLease); facts.Cancel(downedLease); throw; }

        CharacterFatalityFact actual;
        try
        {
            state.currentHealth = 0f;
            state.injurySeverity = 1f;
            stateRules.UpdateDowned(state);
            vitalsAuthority.Project(actor, state);
            data.healthAfter = state.currentHealth; data.downedAfter = state.downed;
            actual = new CharacterFatalityFact(data);
        }
        catch
        {
            RestoreCombatMutation(actor, before, "character-fatality-mutation-exception-rollback");
            facts.Cancel(deathLease); facts.Cancel(criticalLease); facts.Cancel(downedLease);
            throw;
        }

        var pending = new CharacterFatalityPendingFactData
        { deathPublicationKey = deathLease.PublicationKey, fact = actual.Snapshot() };
        var emissions = new List<DomainFactEmission>(3) { DomainFactEmission.Create(deathLease, actual) };
        if (actual.CrossedCritical)
        {
            pending.criticalPublicationKey = criticalLease.PublicationKey;
            emissions.Add(DomainFactEmission.Create(criticalLease, new CharacterFatalCriticalTransitionFact(actual)));
        }
        else facts.Cancel(criticalLease);
        if (actual.BecameDowned)
        {
            pending.downedPublicationKey = downedLease.PublicationKey;
            emissions.Add(DomainFactEmission.Create(downedLease, new CharacterFatalDownedFact(actual)));
        }
        else facts.Cancel(downedLease);

        var pendingFacts = WriteState.PendingFatalityFacts;
        pendingFacts.Add(pending);
        var replies = new DomainFactDelivery[emissions.Count];
        bool accepted;
        string publicationFailure;
        try { accepted = facts.PublishBatch(emissions, replies, out publicationFailure); }
        catch (Exception exception)
        {
            accepted = true;
            publicationFailure = "character-fatality-publication-in-doubt:" + exception.GetType().Name;
            for (int i = 0; i < replies.Length; i++) replies[i] = DomainFactDelivery.Uncertain(publicationFailure);
        }
        finally { facts.Cancel(deathLease); facts.Cancel(criticalLease); facts.Cancel(downedLease); }
        if (!accepted)
        {
            RestoreCombatMutation(actor, before, "character-fatality-fact-rejected");
            pendingFacts.Remove(pending);
            throw new InvalidOperationException("character-fatality-fact-commit:" + publicationFailure);
        }
        if (FatalityDeliveryCompleted(pending, replies)) pendingFacts.Remove(pending);
        else ReportFatalityFactFailure(publicationFailure + ":" + string.Join(";", replies.Select(value => value.Detail)));

        // Apply gameplay/lifecycle notifications once. Replay below publishes frozen facts only.
        // A child observer may wait for this exact parent reference while its delivery is uncertain.
        try
        {
            actor.Stats?.NotifyAggregateDeath(cause, reasonCode, pending.deathPublicationKey);
            PublishHealthTransition(actor, actual.RatioBefore, 0f, cause, reasonCode);
            SyncLifecycle(actor, state, before.State.downed);
        }
        catch (Exception exception) { Debug.LogWarning("Committed character death observer failed: " + exception); }
    }

    internal void RecoverPendingFatalityFacts()
    {
        if (ReadState.PendingFatalityFacts.Count == 0) return;
        if (factRecovery == null) throw new InvalidOperationException("character-fatality-recovery-publisher-missing");
        var pendingFacts = WriteState.PendingFatalityFacts;
        foreach (var pending in pendingFacts.Take(8).ToArray())
        {
            var value = new CharacterFatalityFact(pending.fact);
            var requests = new List<DomainFactReplay>(3)
            { DomainFactReplay.Create(pending.deathPublicationKey, value) };
            if (pending.criticalPublicationKey.Length > 0)
                requests.Add(DomainFactReplay.Create(pending.criticalPublicationKey, new CharacterFatalCriticalTransitionFact(value)));
            if (pending.downedPublicationKey.Length > 0)
                requests.Add(DomainFactReplay.Create(pending.downedPublicationKey, new CharacterFatalDownedFact(value)));
            var replies = new DomainFactDelivery[requests.Count];
            try
            {
                if (factRecovery.ReplayBatch(requests, replies, out string failure) && FatalityDeliveryCompleted(pending, replies))
                    pendingFacts.Remove(pending);
                else ReportFatalityFactFailure("character-fatality-recovery:" + failure + ":" + string.Join(";", replies.Select(item => item.Detail)));
            }
            catch (Exception exception) { ReportFatalityFactFailure("character-fatality-recovery:" + exception.GetType().Name); }
            if (pendingFacts.Remove(pending)) pendingFacts.Add(pending);
        }
    }

    private static bool FatalityDeliveryCompleted(CharacterFatalityPendingFactData pending, DomainFactDelivery[] replies)
    {
        var keys = new[] { pending.deathPublicationKey, pending.criticalPublicationKey, pending.downedPublicationKey }
            .Where(key => key.Length > 0).ToArray();
        return replies.Length == keys.Length && replies.Select((reply, index) => reply.Completed && reply.OperationKey == keys[index]).All(value => value);
    }

    private void ReportFatalityFactFailure(string failure)
    {
        if (lastFatalityFactFailure == failure) return;
        lastFatalityFactFailure = failure;
        Debug.LogWarning("Character fatality observation: " + failure);
    }

    public CharacterBodyHealthSnapshot GetSnapshot(string characterId)
    {
        CharacterId id = (CharacterId)characterId;
        return id.IsValid
            && ReadState.TryGet(id, out CharacterBodyHealthState state)
                ? stateRules.BuildSnapshot(state)
                : stateRules.EmptySnapshot();
    }

    [GameplayInternalOnly(
        "Stages age-condition anatomy damage without irreversible lifecycle observers before the child outcome commits.",
        "AgeConditionAnatomyProjectionRuntime")]
    public CharacterPreparedAgeConditionAnatomyReceipt
        ApplyPreparedAgeConditionAnatomy(
            CharacterActor actor,
            IReadOnlyList<string> nodeIds,
            float damageFraction,
            bool destroyNode,
            string reasonCode)
    {
        if (actor == null || actor.IsDead)
            throw new InvalidOperationException(
                "Age-condition anatomy mutation requires a living actor.");
        string[] requested = (nodeIds ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        if (requested.Length == 0
            || !destroyNode && damageFraction <= 0f
            || string.IsNullOrWhiteSpace(reasonCode))
        {
            throw new InvalidOperationException(
                "Age-condition anatomy mutation input is invalid.");
        }

        CharacterBodyHealthMutationSnapshot before = CaptureCombatMutation(actor);
        CharacterBodyHealthState state = GetOrCreate(actor);
        AnatomyNodeHealthState[] nodes = requested
            .Select(value => stateRules.FindAnatomyNode(state, value))
            .ToArray();
        if (nodes.Any(value => value == null || value.missing))
        {
            throw new InvalidOperationException(
                "Age-condition anatomy mutation has an unavailable node.");
        }

        var deltas = new List<CharacterAnatomyDamageDelta>(nodes.Length);
        float aggregateBefore = state.currentHealth;
        float requestedTotal = 0f;
        for (int index = 0; index < nodes.Length; index++)
        {
            AnatomyNodeHealthState node = nodes[index];
            float damage = destroyNode
                ? node.currentHealth
                : node.maxHealth * damageFraction;
            if (damage <= 0f)
                continue;
            float healthBefore = node.currentHealth;
            float bleedingBefore = node.bleedingPerSecond;
            node.currentHealth = Mathf.Max(0f, node.currentHealth - damage);
            stateRules.SyncLegacySurfaceNode(state, node.nodeId);
            requestedTotal += damage;
            deltas.Add(new CharacterAnatomyDamageDelta(
                node.nodeId,
                node.maxHealth,
                healthBefore,
                damage,
                node.currentHealth,
                bleedingBefore,
                node.bleedingPerSecond));
        }
        if (deltas.Count == 0)
        {
            throw new InvalidOperationException(
                "Age-condition anatomy mutation produced no delta.");
        }

        state.currentHealth = Mathf.Max(1f, state.currentHealth - requestedTotal);
        state.injurySeverity = Mathf.Clamp01(
            1f - state.currentHealth / Mathf.Max(1f, state.maxHealth));
        state.lastDamageReason = reasonCode.Trim();
        stateRules.UpdateDowned(state);
        vitalsAuthority.Project(actor, state);
        return new CharacterPreparedAgeConditionAnatomyReceipt(
            before,
            deltas.ToArray(),
            aggregateBefore,
            state.currentHealth,
            reasonCode.Trim());
    }

    [GameplayInternalOnly(
        "Publishes age-condition anatomy lifecycle effects after the child outcome is durable.",
        "AgeConditionAnatomyProjectionRuntime")]
    public void CompletePreparedAgeConditionAnatomy(
        CharacterActor actor,
        in CharacterPreparedAgeConditionAnatomyReceipt receipt)
    {
        if (actor == null || !receipt.IsValid)
            throw new InvalidOperationException(
                "Prepared age-condition anatomy completion is invalid.");
        CharacterBodyHealthState state = GetOrCreate(actor);
        float applied = Mathf.Max(
            0f,
            receipt.AggregateHealthBefore - receipt.AggregateHealthAfter);
        if (applied > 0f)
        {
            actor.Stats?.NotifyAggregateDamage(
                applied,
                receipt.ReasonCode,
                false,
                CharacterDeathCauseCode.AgeConditionOrganFailure);
        }
        for (int index = 0; index < receipt.NodeDeltas.Count; index++)
        {
            stateRules.KillForDestroyedVitalNode(
                actor,
                state,
                receipt.NodeDeltas[index].NodeId,
                CharacterDeathCauseCode.AgeConditionOrganFailure,
                receipt.ReasonCode);
            if (actor.IsDead)
                break;
        }
        bool wasDowned = receipt.AggregateWasDowned;
        stateRules.UpdateDowned(state);
        PublishHealthTransition(
            actor,
            Mathf.Clamp01(
                receipt.AggregateHealthBefore
                / Mathf.Max(1f, receipt.AggregateMaximumHealth)),
            HealthRatio(state),
            CharacterDeathCauseCode.AgeConditionOrganFailure,
            receipt.ReasonCode);
        SyncLifecycle(actor, state, wasDowned);
    }

    public void RestorePreparedAgeConditionAnatomy(
        CharacterActor actor,
        in CharacterBodyHealthMutationSnapshot snapshot,
        string reason) => RestoreCombatMutation(actor, snapshot, reason);

    [GameplayInternalOnly(
        "Stages disease infection burden so the mutation can be rolled back if its outcome is rejected.",
        "DiseaseAnatomyBurdenProjectionRuntime")]
    public CharacterPreparedDiseaseAnatomyBurdenReceipt
        ApplyPreparedDiseaseAnatomyBurden(
            CharacterActor actor,
            string nodeId,
            float infectionBurden,
            string reasonCode)
    {
        if (actor == null || actor.IsDead
            || string.IsNullOrWhiteSpace(nodeId)
            || infectionBurden <= 0f
            || string.IsNullOrWhiteSpace(reasonCode))
        {
            throw new InvalidOperationException(
                "Disease anatomy burden mutation input is invalid.");
        }

        CharacterBodyHealthMutationSnapshot before = CaptureCombatMutation(actor);
        CharacterBodyHealthState state = GetOrCreate(actor);
        AnatomyNodeHealthState node = stateRules.FindAnatomyNode(
            state,
            nodeId.Trim());
        if (node == null || node.missing)
        {
            throw new InvalidOperationException(
                "Disease anatomy burden target node is unavailable.");
        }

        float infectionBefore = node.infection;
        node.infection = Mathf.Clamp(
            node.infection + infectionBurden,
            0f,
            100f);
        return new CharacterPreparedDiseaseAnatomyBurdenReceipt(
            before,
            node.nodeId,
            infectionBurden,
            infectionBefore,
            node.infection,
            reasonCode);
    }

    public void RestorePreparedDiseaseAnatomyBurden(
        CharacterActor actor,
        in CharacterBodyHealthMutationSnapshot snapshot,
        string reason) => RestoreCombatMutation(actor, snapshot, reason);

    [GameplayInternalOnly(
        "Captures an exact detached health aggregate image before combat outcome commit.",
        "CombatCommandResultApplier")]
    public CharacterBodyHealthMutationSnapshot CaptureCombatMutation(
        CharacterActor actor)
    {
        if (actor == null)
            throw new ArgumentNullException(nameof(actor));
        CharacterId characterId = GetId(actor);
        if (!characterId.IsValid)
        {
            throw new InvalidOperationException(
                "Combat health mutation requires a persistent character ID.");
        }
        return new CharacterBodyHealthMutationSnapshot(
            characterId,
            stateRules.CloneState(GetOrCreate(actor)));
    }

    [GameplayInternalOnly(
        "Rolls back only the immediately preceding uncommitted combat health mutation.",
        "CombatCommandResultApplier")]
    public void RestoreCombatMutation(
        CharacterActor actor,
        in CharacterBodyHealthMutationSnapshot snapshot,
        string reason)
    {
        if (actor == null)
            throw new ArgumentNullException(nameof(actor));
        CharacterId characterId = GetId(actor);
        if (!snapshot.IsValid || !characterId.Equals(snapshot.CharacterId))
        {
            throw new InvalidOperationException(
                "Combat health rollback snapshot is stale or belongs to another character.");
        }

        CharacterBodyHealthState restored = stateRules.CloneState(snapshot.State);
        _ = reason;
        WriteState.Set(characterId, restored);
        trackedActors[characterId] = actor;
        vitalsAuthority.Project(actor, restored);
    }

    [GameplayInternalOnly(
        "Stages exact combat health state without publishing irreversible lifecycle side effects before the outcome commit.",
        "CombatCommandResultApplier")]
    public void ApplyPreparedCombatMutation(
        CharacterActor actor,
        CombatAttackResult result,
        string reason)
    {
        if (actor == null || actor.IsDead || !result.Executed)
            return;

        CharacterBodyHealthState state = GetOrCreate(actor);
        state.suppression = Mathf.Clamp(
            state.suppression + result.Suppression,
            0f,
            100f);
        ApplySpecialStatus(state, result);
        if (result.Hit && result.AppliedDamage > 0f)
        {
            CharacterBodyPartHealthState part = state.parts.First(item =>
                item.bodyPart == result.BodyPart);
            float appliedDamage = result.Nonlethal
                ? Mathf.Min(
                    result.AppliedDamage,
                    Mathf.Max(0f, state.currentHealth - 1f),
                    Mathf.Max(0f, part.currentHealth - 1f))
                : result.AppliedDamage;
            part.currentHealth = Mathf.Max(
                0f,
                part.currentHealth - appliedDamage);
            part.bleedingPerSecond += result.Bleeding * 0.01f;
            stateRules.ApplyLegacyDamageToAnatomy(
                state,
                result.BodyPart,
                appliedDamage,
                result.Bleeding * 0.01f);
            state.lastDamageReason = reason ?? string.Empty;
            bool fatalCorePart = !result.Nonlethal
                && result.BodyPart is (CombatBodyPart.Head
                    or CombatBodyPart.Torso)
                && part.currentHealth <= 0f;
            state.currentHealth = fatalCorePart
                ? 0f
                : Mathf.Max(1f, state.currentHealth - appliedDamage);
            state.injurySeverity = Mathf.Clamp01(
                1f - state.currentHealth / Mathf.Max(1f, state.maxHealth));
        }

        stateRules.UpdateDowned(state);
        vitalsAuthority.Project(actor, state);
    }

    [GameplayInternalOnly(
        "Publishes combat lifecycle side effects only after the matching outcome commit succeeds.",
        "CombatCommandResultApplier")]
    public void CompletePreparedCombatMutation(
        CharacterActor actor,
        in CharacterBodyHealthMutationSnapshot before,
        CombatAttackResult result,
        string reason)
    {
        if (actor == null || !before.IsValid || !result.Executed)
            return;
        CharacterBodyHealthState state = GetOrCreate(actor);
        CharacterBodyHealthState previous = before.State;
        ApplyPanicMoodOnThresholdCrossing(
            actor,
            previous.suppression,
            state.suppression);
        if (result.Hit && result.AppliedDamage > 0f)
        {
            float actualDamage = Mathf.Max(
                0f,
                previous.currentHealth - state.currentHealth);
            if (actualDamage > 0f)
            {
                actor.Stats?.NotifyAggregateDamage(
                    result.AppliedDamage,
                    reason,
                    false,
                    CharacterDeathCauseCode.Combat);
            }
            if (previous.currentHealth > 0f && state.currentHealth <= 0f)
            {
                actor.Stats?.NotifyAggregateDeath(
                    CharacterDeathCauseCode.Combat,
                    result.BodyPart == CombatBodyPart.Head
                        ? "combat:fatal-head-trauma"
                        : "combat:fatal-torso-trauma");
            }
            PublishHealthTransition(
                actor,
                HealthRatio(previous),
                state.currentHealth <= 0f
                    ? Mathf.Clamp01(
                        Mathf.Max(
                            1f,
                            previous.currentHealth - result.AppliedDamage)
                        / Mathf.Max(1f, previous.maxHealth))
                    : HealthRatio(state),
                CharacterDeathCauseCode.Combat,
                reason);
        }
        SyncLifecycle(actor, state, previous.downed);
    }

    [GameplayInternalOnly(
        "Stages exact aggregate damage without publishing damage, death or threshold observers before the owning outcome commits.",
        "CaptivityInteractionRuntime")]
    public CharacterPreparedAggregateDamageReceipt ApplyPreparedAggregateDamage(
        CharacterActor actor,
        float amount,
        CharacterDeathCauseCode deathCause,
        string reasonCode,
        bool allowDeath)
    {
        if (actor == null)
            throw new ArgumentNullException(nameof(actor));
        if (!float.IsFinite(amount) || amount <= 0f)
            throw new ArgumentOutOfRangeException(nameof(amount));
        string reason = reasonCode?.Trim() ?? string.Empty;
        if (reason.Length == 0)
            throw new ArgumentException(
                "Prepared aggregate damage requires a canonical reason code.",
                nameof(reasonCode));

        CharacterId characterId = GetId(actor);
        if (!characterId.IsValid)
        {
            throw new InvalidOperationException(
                "Prepared aggregate damage requires a persistent character ID.");
        }

        CharacterBodyHealthState state = GetOrCreate(actor);
        float before = state.currentHealth;
        if (before <= 0f)
        {
            throw new InvalidOperationException(
                "Prepared aggregate damage cannot target an already-dead body.");
        }
        float after = Mathf.Max(
            allowDeath ? 0f : 1f,
            before - amount);
        state.currentHealth = after;
        state.injurySeverity = Mathf.Clamp01(
            1f - after / Mathf.Max(1f, state.maxHealth));
        vitalsAuthority.Project(actor, state);
        return new CharacterPreparedAggregateDamageReceipt(
            characterId,
            amount,
            before,
            after,
            state.maxHealth,
            deathCause,
            reason,
            allowDeath);
    }

    [GameplayInternalOnly(
        "Publishes prepared aggregate-damage observers only after the owning outcome commits; observer failures are isolated from the committed gameplay state.",
        "CaptivityInteractionRuntime")]
    public void CompletePreparedAggregateDamage(
        CharacterActor actor,
        in CharacterPreparedAggregateDamageReceipt receipt)
    {
        if (actor == null || !receipt.IsValid)
            return;
        CharacterId characterId = GetId(actor);
        if (!characterId.Equals(receipt.CharacterId))
        {
            throw new InvalidOperationException(
                "Prepared aggregate-damage receipt belongs to another character.");
        }

        try
        {
            actor.Stats?.NotifyAggregateDamage(
                receipt.RequestedDamage,
                receipt.ReasonCode,
                receipt.AllowDeath && receipt.ResultingHealth <= 0f,
                receipt.DeathCause);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "Committed aggregate-damage observer failed: " + exception);
        }

        try
        {
            PublishHealthTransition(
                actor,
                Mathf.Clamp01(
                    receipt.PreviousHealth / receipt.MaximumHealth),
                Mathf.Clamp01(
                    receipt.ResultingHealth / receipt.MaximumHealth),
                receipt.DeathCause,
                receipt.ReasonCode);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "Committed aggregate-damage health transition observer failed: "
                + exception);
        }
    }

    public void ApplyPreparedSuppressionReduction(
        CharacterActor actor,
        float amount)
    {
        if (actor == null || !float.IsFinite(amount) || amount <= 0f)
            return;
        CharacterBodyHealthState state = GetOrCreate(actor);
        state.suppression = Mathf.Max(0f, state.suppression - amount);
        stateRules.UpdateDowned(state);
        vitalsAuthority.Project(actor, state);
    }

    public void CompletePreparedSuppressionReduction(
        CharacterActor actor,
        in CharacterBodyHealthMutationSnapshot before)
    {
        if (actor == null || !before.IsValid)
            return;
        SyncLifecycle(actor, GetOrCreate(actor), before.State.downed);
    }

    public void ApplyCombatResult(CharacterActor target, CombatAttackResult result, string reason)
    {
        if (target == null || target.IsDead || !result.Executed)
        {
            return;
        }

        CharacterBodyHealthState state = GetOrCreate(target);
        float previousSuppression = state.suppression;
        state.suppression = Mathf.Clamp(state.suppression + result.Suppression, 0f, 100f);
        ApplyPanicMoodOnThresholdCrossing(target, previousSuppression, state.suppression);
        ApplySpecialStatus(state, result);
        if (!result.Hit || result.AppliedDamage <= 0f)
        {
            bool wasDowned = state.downed;
            stateRules.UpdateDowned(state);
            SyncLifecycle(target, state, wasDowned);
            return;
        }

        CharacterBodyPartHealthState part = state.parts.First(item => item.bodyPart == result.BodyPart);
        float appliedDamage = result.Nonlethal
            ? Mathf.Min(
                result.AppliedDamage,
                Mathf.Max(0f, state.currentHealth - 1f),
                Mathf.Max(0f, part.currentHealth - 1f))
            : result.AppliedDamage;
        part.currentHealth = Mathf.Max(0f, part.currentHealth - appliedDamage);
        part.bleedingPerSecond += result.Bleeding * 0.01f;
        stateRules.ApplyLegacyDamageToAnatomy(
            state,
            result.BodyPart,
            appliedDamage,
            result.Bleeding * 0.01f);
        state.lastDamageReason = reason ?? string.Empty;
        ApplyAggregateDamage(
            target,
            state,
            appliedDamage,
            reason,
            allowDeath: false);

        if (!result.Nonlethal
            && !target.IsDead
            && (result.BodyPart == CombatBodyPart.Head || result.BodyPart == CombatBodyPart.Torso)
            && part.currentHealth <= 0f)
        {
            Kill(
                target,
                CharacterDeathCauseCode.Combat,
                result.BodyPart == CombatBodyPart.Head
                    ? "combat:fatal-head-trauma"
                    : "combat:fatal-torso-trauma");
        }

        bool wasDownedAfterHit = state.downed;
        stateRules.UpdateDowned(state);
        SyncLifecycle(target, state, wasDownedAfterHit);
    }

    public CharacterCombatSpecialStatusSnapshot GetCombatSpecialStatus(
        CharacterId characterId)
    {
        return characterId.IsValid
            && ReadState.TryGet(characterId, out CharacterBodyHealthState state)
                ? new CharacterCombatSpecialStatusSnapshot(
                    state.burningDamagePerSecond,
                    state.burningRemainingSeconds,
                    state.sedationRatio,
                    state.sedationRemainingSeconds,
                    state.manaBlockedRemainingSeconds)
                : default;
    }

    private static void ApplySpecialStatus(
        CharacterBodyHealthState state,
        CombatAttackResult result)
    {
        float seconds = Mathf.Max(1f, result.StatusTurns * 3f);
        if ((result.SpecialEffects & CombatSpecialEffectFlags.Burning) != 0)
        {
            state.burningDamagePerSecond = Mathf.Max(
                state.burningDamagePerSecond,
                result.StatusPotency);
            state.burningRemainingSeconds = Mathf.Max(
                state.burningRemainingSeconds,
                seconds);
        }
        if ((result.SpecialEffects & CombatSpecialEffectFlags.Tranquilized) != 0)
        {
            state.sedationRatio = Mathf.Max(
                state.sedationRatio,
                result.StatusPotency);
            state.sedationRemainingSeconds = Mathf.Max(
                state.sedationRemainingSeconds,
                seconds);
        }
        if ((result.SpecialEffects & CombatSpecialEffectFlags.ManaBlocked) != 0)
        {
            state.manaBlockedRemainingSeconds = Mathf.Max(
                state.manaBlockedRemainingSeconds,
                seconds);
        }
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
        float previousSuppression = state.suppression;
        state.suppression = Mathf.Clamp(state.suppression + amount, 0f, 100f);
        ApplyPanicMoodOnThresholdCrossing(target, previousSuppression, state.suppression);
        bool wasDowned = state.downed;
        stateRules.UpdateDowned(state);
        SyncLifecycle(target, state, wasDowned);
    }

    public void ReduceSuppression(CharacterActor target, float amount)
    {
        if (target == null || amount <= 0f) return;
        CharacterBodyHealthState state = GetOrCreate(target);
        state.suppression = Mathf.Max(0f, state.suppression - amount);
        bool wasDowned = state.downed;
        stateRules.UpdateDowned(state);
        SyncLifecycle(target, state, wasDowned);
    }

    private static void ApplyPanicMoodOnThresholdCrossing(
        CharacterActor target,
        float previousSuppression,
        float currentSuppression)
    {
        if (target == null
            || previousSuppression >= PanicSuppressionThreshold
            || currentSuppression < PanicSuppressionThreshold)
            return;

        target.ApplyMoodFactor(
            "event:panic",
            "전투 패닉",
            -8f,
            GameCalendarRules.SecondsPerDay,
            1);
    }

    public void Heal(CharacterActor target, float amount, bool stopBleeding)
    {
        if (target == null || amount <= 0f) return;
        if (facts == null || factRecovery == null)
            throw new InvalidOperationException("character-healing-fact-publisher-missing");
        if (ReadState.PendingHealingFacts.Count >= CharacterHealingFact.MaximumPending)
            throw new InvalidOperationException("character-healing-pending-capacity");

        CharacterBodyHealthState state = GetOrCreate(target);
        CharacterBodyHealthMutationSnapshot before = CaptureCombatMutation(target);
        RequireNoPendingBodyObservation(before.CharacterId.Value);
        int absoluteDay = gameCalendar.Current.AbsoluteDay;
        var data = new CharacterHealingFactData
        {
            characterId = before.CharacterId.Value,
            characterName = target.Identity?.DisplayName ?? target.name,
            absoluteDay = absoluteDay,
            healthBefore = state.currentHealth, maximumHealthBefore = state.maxHealth,
            healthAfter = state.currentHealth, maximumHealthAfter = state.maxHealth,
            downedBefore = state.downed, downedAfter = state.downed
        };
        var proposal = new CharacterHealingFact(data, proposalOnly: true);
        DomainFactLease criticalLease = default, recoveryLease = default;
        try
        {
            if (!facts.TryReserve(new CharacterCriticalHealthTransitionFact(proposal), out criticalLease, out string failure))
                throw new InvalidOperationException("character-healing-fact-reserve:" + failure);
            if (!facts.TryReserve(new CharacterMobilityRecoveredFact(proposal), out recoveryLease, out failure))
                throw new InvalidOperationException("character-healing-fact-reserve:" + failure);
            if (string.IsNullOrEmpty(criticalLease.PublicationKey) || string.IsNullOrEmpty(recoveryLease.PublicationKey))
                throw new InvalidOperationException("character-healing-fact-reservation-identity-missing");
        }
        catch { facts.Cancel(criticalLease); facts.Cancel(recoveryLease); throw; }

        CharacterHealingFact actual;
        try
        {
            stateRules.HealSurfaceParts(state, amount, stopBleeding);
            state.bloodLoss = Mathf.Max(0f, state.bloodLoss - amount * 0.5f);
            if (state.currentHealth > 0f)
            {
                state.currentHealth = Mathf.Min(state.maxHealth, state.currentHealth + amount);
                state.injurySeverity = Mathf.Clamp01(1f - state.currentHealth / Mathf.Max(1f, state.maxHealth));
            }
            stateRules.UpdateDowned(state);
            vitalsAuthority.Project(target, state);
            data.healthAfter = state.currentHealth; data.maximumHealthAfter = state.maxHealth;
            data.downedAfter = state.downed;
            actual = new CharacterHealingFact(data);
        }
        catch
        {
            RestoreCombatMutation(target, before, "character-healing-mutation-exception-rollback");
            facts.Cancel(criticalLease); facts.Cancel(recoveryLease);
            throw;
        }

        var emissions = new List<DomainFactEmission>(2);
        var pending = new CharacterHealingPendingFactData { fact = actual.Snapshot() };
        if (actual.CrossedCritical)
        {
            pending.criticalPublicationKey = criticalLease.PublicationKey;
            emissions.Add(DomainFactEmission.Create(criticalLease, new CharacterCriticalHealthTransitionFact(actual)));
        }
        else facts.Cancel(criticalLease);
        if (actual.Recovered)
        {
            pending.recoveryPublicationKey = recoveryLease.PublicationKey;
            emissions.Add(DomainFactEmission.Create(recoveryLease, new CharacterMobilityRecoveredFact(actual)));
        }
        else facts.Cancel(recoveryLease);

        if (emissions.Count > 0)
        {
            var pendingFacts = WriteState.PendingHealingFacts;
            pendingFacts.Add(pending);
            var replies = new DomainFactDelivery[emissions.Count];
            bool accepted;
            string failure;
            try { accepted = facts.PublishBatch(emissions, replies, out failure); }
            catch (Exception exception)
            {
                accepted = true;
                failure = "character-healing-publication-in-doubt:" + exception.GetType().Name;
                for (int i = 0; i < replies.Length; i++) replies[i] = DomainFactDelivery.Uncertain(failure);
            }
            if (!accepted)
            {
                RestoreCombatMutation(target, before, "character-healing-fact-rejected");
                pendingFacts.Remove(pending);
                throw new InvalidOperationException("character-healing-fact-commit:" + failure);
            }
            if (HealingDeliveryCompleted(pending, replies)) pendingFacts.Remove(pending);
            else ReportHealingFactFailure(failure + ":" + string.Join(";", replies.Select(value => value.Detail)));
        }

        // Observation recovery never invokes these gameplay/UI/lifecycle effects.
        try
        {
            float applied = Mathf.Max(0f, state.currentHealth - before.State.currentHealth);
            if (applied > 0f) target.Stats?.NotifyAggregateHealing(applied);
            PublishHealthTransition(target, actual.RatioBefore, actual.RatioAfter, CharacterDeathCauseCode.Unknown, "healing");
            SyncLifecycle(target, state, before.State.downed);
        }
        catch (Exception exception) { Debug.LogWarning("Committed character healing observer failed: " + exception); }
    }

    internal void RecoverPendingHealingFacts()
    {
        if (ReadState.PendingHealingFacts.Count == 0) return;
        if (factRecovery == null) throw new InvalidOperationException("character-healing-recovery-publisher-missing");
        var pendingFacts = WriteState.PendingHealingFacts;
        foreach (var pending in pendingFacts.Take(8).ToArray())
        {
            var value = new CharacterHealingFact(pending.fact);
            var requests = new List<DomainFactReplay>(2);
            if (pending.criticalPublicationKey.Length > 0)
                requests.Add(DomainFactReplay.Create(pending.criticalPublicationKey, new CharacterCriticalHealthTransitionFact(value)));
            if (pending.recoveryPublicationKey.Length > 0)
                requests.Add(DomainFactReplay.Create(pending.recoveryPublicationKey, new CharacterMobilityRecoveredFact(value)));
            var replies = new DomainFactDelivery[requests.Count];
            try
            {
                if (factRecovery.ReplayBatch(requests, replies, out string failure) && HealingDeliveryCompleted(pending, replies))
                    pendingFacts.Remove(pending);
                else ReportHealingFactFailure("character-healing-recovery:" + failure + ":" + string.Join(";", replies.Select(item => item.Detail)));
            }
            catch (Exception exception) { ReportHealingFactFailure("character-healing-recovery:" + exception.GetType().Name); }
            // A failed character must not indefinitely starve the rest of this bounded queue.
            if (pendingFacts.Remove(pending)) pendingFacts.Add(pending);
        }
    }

    private static bool HealingDeliveryCompleted(CharacterHealingPendingFactData pending, DomainFactDelivery[] replies)
    {
        var keys = new[] { pending.criticalPublicationKey, pending.recoveryPublicationKey }.Where(key => key.Length > 0).ToArray();
        return replies.Length == keys.Length && replies.Select((reply, index) => reply.Completed && reply.OperationKey == keys[index]).All(value => value);
    }

    private void RequireNoPendingBodyObservation(string characterId)
    {
        if (ReadState.PendingHealingFacts.Any(value => value.fact.characterId == characterId))
            throw new InvalidOperationException("character-healing-observation-pending:" + characterId);
        if (ReadState.PendingFatalityFacts.Any(value => value.fact.characterId == characterId))
            throw new InvalidOperationException("character-fatality-observation-pending:" + characterId);
    }

    private void ReportHealingFactFailure(string failure)
    {
        if (lastHealingFactFailure == failure) return;
        lastHealingFactFailure = failure;
        Debug.LogWarning("Character healing observation: " + failure);
    }

    public float GetTotalBleeding(CharacterActor target)
    {
        if (target == null)
        {
            return 0f;
        }

        CharacterBodyHealthState state = GetForQuery(target);
        if (state == null)
        {
            return 0f;
        }
        return stateRules.GetStateBleeding(state);
    }

    public float GetMissingPartHealth(CharacterActor target)
    {
        if (target == null)
        {
            return 0f;
        }

        CharacterBodyHealthState state = GetForQuery(target);
        if (state == null)
        {
            return 0f;
        }
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

    [GameplayInternalOnly(
        "Stages medical stabilization without lifecycle observers before its outcome commits.",
        "CharacterMedicalRuntime")]
    public void ApplyPreparedMedicalStabilization(CharacterActor actor)
    {
        if (actor == null || actor.IsDead)
            return;
        CharacterBodyHealthState state = GetOrCreate(actor);
        foreach (CharacterBodyPartHealthState part in state.parts)
            part.bleedingPerSecond = 0f;
        foreach (AnatomyNodeHealthState node in state.anatomyNodes)
            node.bleedingPerSecond = 0f;
        stateRules.UpdateDowned(state);
        vitalsAuthority.Project(actor, state);
    }

    [GameplayInternalOnly(
        "Stages medical treatment without healing or lifecycle observers before its outcome commits.",
        "CharacterMedicalRuntime")]
    public void ApplyPreparedMedicalTreatment(
        CharacterActor actor,
        float partHealthAmount,
        float bloodLossReduction)
    {
        if (actor == null || actor.IsDead)
            return;
        CharacterBodyHealthState state = GetOrCreate(actor);
        float restored = stateRules.HealSurfaceParts(
            state, partHealthAmount, stopBleeding: false);
        state.bloodLoss = Mathf.Max(
            0f, state.bloodLoss - Mathf.Max(0f, bloodLossReduction));
        if (restored > 0f && state.currentHealth > 0f)
        {
            state.currentHealth = Mathf.Min(
                state.maxHealth, state.currentHealth + restored);
            state.injurySeverity = Mathf.Clamp01(
                1f - state.currentHealth / Mathf.Max(1f, state.maxHealth));
        }
        stateRules.UpdateDowned(state);
        vitalsAuthority.Project(actor, state);
    }

    [GameplayInternalOnly(
        "Publishes medical health and lifecycle observers after the outcome is durable.",
        "CharacterMedicalRuntime")]
    public void CompletePreparedMedicalMutation(
        CharacterActor actor,
        in CharacterBodyHealthMutationSnapshot before)
    {
        if (actor == null || !before.IsValid)
            return;
        CharacterBodyHealthState state = GetOrCreate(actor);
        float healing = Mathf.Max(
            0f, state.currentHealth - before.State.currentHealth);
        if (healing > 0f)
        {
            actor.Stats?.NotifyAggregateHealing(healing);
            PublishHealthTransition(
                actor,
                HealthRatio(before.State),
                HealthRatio(state),
                CharacterDeathCauseCode.Unknown,
                "healing");
        }
        SyncLifecycle(actor, state, before.State.downed);
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
        float restoredTotal = stateRules.HealSurfaceParts(
            state, partHealthAmount, stopBleeding: false);
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

#if UNITY_EDITOR
    public void BeginCreationDiagnosticsForEditor()
    {
        creationDiagnosticsForEditor.Clear();
        creationDiagnosticsEnabledForEditor = true;
    }

    public string EndCreationDiagnosticsForEditor()
    {
        creationDiagnosticsEnabledForEditor = false;
        string result = creationDiagnosticsForEditor.Count == 0
            ? "none"
            : string.Join(" || ", creationDiagnosticsForEditor);
        creationDiagnosticsForEditor.Clear();
        return result;
    }
#endif

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

        CharacterBodyHealthState state = GetOrCreate(id, actor);
        AnatomyProfileDefinition profile = stateRules.ResolveProfile(state.anatomyProfileId);
        stateRules.EnsureAnatomy(state, profile);
        vitalsAuthority.Project(actor, state);
        return state;
    }

    private CharacterBodyHealthState GetForQuery(CharacterActor actor)
    {
        if (actor == null)
        {
            return null;
        }

        EnsureAggregateRevision();
        CharacterId id = GetId(actor);
        if (ReadState.TryGet(id, out CharacterBodyHealthState existing))
        {
            stateRules.EnsureParts(existing);
            stateRules.EnsureAnatomy(
                existing,
                stateRules.ResolveProfile(existing.anatomyProfileId));
            return existing;
        }

        // Injection can assign a persistent ID before the spawn profile. A
        // query must not freeze a default anatomy during that construction gap.
        if (actor.Identity?.Profile == null) return null;

        // A retired actor can remain alive until Unity processes Destroy at
        // the end of the frame. Read-only queries during that window must not
        // recreate state that an authoritative restore just removed.
        return actor.gameObject.activeInHierarchy
            && actor.CurrentLifecycleState != CharacterLifecycleState.Despawned
            ? GetOrCreate(actor)
            : null;
    }

    private CharacterBodyHealthState GetOrCreate(
        CharacterId characterId,
        CharacterActor actorContext = null)
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

#if UNITY_EDITOR
        if (creationDiagnosticsEnabledForEditor)
        {
            bool hasActorContext = actorContext != null;
            string callStack = Environment.StackTrace
                .Replace("\r", string.Empty)
                .Replace("\n", " <- ");
            creationDiagnosticsForEditor.Add(
                $"id={characterId.Value};actor={(hasActorContext ? actorContext.name : "none")};"
                + $"type={(hasActorContext ? actorContext.Identity?.CharacterType.ToString() ?? "none" : "none")};"
                + $"owner={(hasActorContext && actorContext.IsOwner)};"
                // Do not query IsDead while diagnosing creation: for an active
                // actor it reads the body-health projection and would
                // recursively enter this exact missing-state path.
                + $"despawned={hasActorContext && actorContext.CurrentLifecycleState == CharacterLifecycleState.Despawned};"
                + $"lifecycle={(hasActorContext ? actorContext.CurrentLifecycleState.ToString() : "none")};"
                + $"activeSelf={(hasActorContext && actorContext.gameObject.activeSelf)};"
                + $"activeInHierarchy={(hasActorContext && actorContext.gameObject.activeInHierarchy)};"
                + $"stack={callStack}");
        }
#endif

        state = new CharacterBodyHealthState
        {
            characterId = characterId.Value
        };
        stateRules.EnsureParts(state);
        stateRules.EnsureAnatomy(
            state,
            actorContext != null ? stateRules.ResolveForActor(actorContext)
                : stateRules.ResolveProfile(string.Empty));
        WriteState.Set(characterId, state);
        return state;
    }

    public AnatomyHealthSnapshot GetAnatomySnapshot(CharacterActor actor)
    {
        CharacterBodyHealthState state = GetForQuery(actor);
        return state == null
            ? stateRules.EmptyAnatomySnapshot()
            : stateRules.BuildAnatomySnapshot(state);
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

    public bool TryDamageNode(
        CharacterActor actor,
        string nodeId,
        float damage,
        float bleeding,
        string reason) =>
        TryDamageNodeWithCause(
            actor,
            nodeId,
            damage,
            bleeding,
            CharacterDeathCauseCode.Unknown,
            reason);

    public bool TryDamageNodeWithCause(
        CharacterActor actor,
        string nodeId,
        float damage,
        float bleeding,
        CharacterDeathCauseCode deathCause,
        string reasonCode)
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
        state.lastDamageReason = reasonCode ?? string.Empty;
        // Publish the typed surface change before aggregate-damage side effects
        // can re-enter anatomy queries through performance evaluation.
        stateRules.SyncLegacySurfaceNode(state, node.nodeId);
        ApplyAggregateDamage(
            actor,
            state,
            damage,
            reasonCode,
            allowDeath: false);
        stateRules.KillForDestroyedVitalNode(
            actor,
            state,
            node.nodeId,
            deathCause,
            reasonCode);
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

    public bool TryStopBleeding(
        CharacterActor actor,
        string nodeId,
        out DomainFailure failure)
    {
        failure = DomainFailure.None;
        if (actor == null || actor.IsDead)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryLivingSubjectUnavailable,
                actor?.Identity?.PersistentId ?? string.Empty);
            return false;
        }

        CharacterBodyHealthState state = GetOrCreate(actor);
        AnatomyNodeHealthState node = stateRules.FindAnatomyNode(state, nodeId);
        if (node == null || node.missing)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryTargetNodeUnavailable,
                nodeId ?? string.Empty);
            return false;
        }

        node.bleedingPerSecond = 0f;
        stateRules.SyncLegacySurfaceNode(state, node.nodeId);
        bool wasDowned = state.downed;
        stateRules.UpdateDowned(state);
        SyncLifecycle(actor, state, wasDowned);
        return true;
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
        stateRules.KillForDestroyedVitalNode(
            actor,
            state,
            node.nodeId,
            CharacterDeathCauseCode.Unknown,
            $"surgery:vital-node-removed:{node.nodeId}");
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
        float restoredCurrentHealth,
        bool preserveRestoredHealth,
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

        if (preserveRestoredHealth
            && (float.IsNaN(restoredCurrentHealth)
                || float.IsInfinity(restoredCurrentHealth)
                || restoredCurrentHealth < 0f))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId,
                "installation-durability-invalid");
            return false;
        }

        node.missing = false;
        node.installedPartId = partInstanceId.Trim();
        node.installedPartKind = partKind;
        node.installedPartEfficiency = Mathf.Clamp(efficiency, 0.1f, 1.75f);
        node.moduleBonus = 0f;
        node.recoveryPolicy = stateRules.ResolveRecoveryPolicy(partKind);
        node.currentHealth = preserveRestoredHealth
            ? Mathf.Clamp(restoredCurrentHealth, 0f, node.maxHealth)
            : Mathf.Max(node.currentHealth, node.maxHealth * 0.35f);
        node.bleedingPerSecond = Mathf.Min(node.bleedingPerSecond, 0.05f);
        stateRules.SyncLegacySurfaceNode(state, node.nodeId);
        bool wasDowned = state.downed;
        stateRules.UpdateDowned(state);
        SyncLifecycle(actor, state, wasDowned);
        actor.Stats.RefreshDerivedMaximumHealthPreservingCurrent();
        return true;
    }

    public bool TryReplaceNodePart(
        CharacterActor actor,
        string nodeId,
        string expectedPartInstanceId,
        float expectedCurrentHealth,
        float expectedMaxHealth,
        string partInstanceId,
        SurgicalPartKind partKind,
        float efficiency,
        float restoredCurrentHealth,
        bool preserveRestoredHealth,
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

        string expected = expectedPartInstanceId?.Trim() ?? string.Empty;
        if (!string.Equals(
                node.installedPartId,
                expected,
                StringComparison.Ordinal))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                expected,
                node.installedPartId ?? string.Empty);
            return false;
        }

        if (float.IsNaN(expectedCurrentHealth)
            || float.IsInfinity(expectedCurrentHealth)
            || float.IsNaN(expectedMaxHealth)
            || float.IsInfinity(expectedMaxHealth)
            || expectedMaxHealth <= 0f
            || expectedCurrentHealth < 0f
            || expectedCurrentHealth > expectedMaxHealth
            || node.currentHealth != expectedCurrentHealth
            || node.maxHealth != expectedMaxHealth)
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                expected,
                "replacement-detached-health-changed");
            return false;
        }

        if (preserveRestoredHealth
            && (float.IsNaN(restoredCurrentHealth)
                || float.IsInfinity(restoredCurrentHealth)
                || restoredCurrentHealth < 0f))
        {
            failure = new DomainFailure(
                FailureCode.SurgeryPartUnavailable,
                partInstanceId,
                "replacement-durability-invalid");
            return false;
        }

        replacedNode = stateRules.CloneAnatomyNode(node);
        node.missing = false;
        node.installedPartId = partInstanceId.Trim();
        node.installedPartKind = partKind;
        node.installedPartEfficiency = Mathf.Clamp(efficiency, 0.1f, 1.75f);
        node.moduleBonus = 0f;
        node.recoveryPolicy = stateRules.ResolveRecoveryPolicy(partKind);
        node.currentHealth = preserveRestoredHealth
            ? Mathf.Clamp(restoredCurrentHealth, 0f, node.maxHealth)
            : Mathf.Max(node.maxHealth * 0.35f, 1f);
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
                Kill(
                    actor,
                    CharacterDeathCauseCode.Infection,
                    $"infection:vital-organ-failure:{definition.NodeId}");
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
        ApplyAggregateDamage(actor, state, amount,
            CharacterDeathCauseCode.Unknown, reason, allowDeath);
    }

    private void ApplyAggregateDamage(
        CharacterActor actor,
        CharacterBodyHealthState state,
        float amount,
        CharacterDeathCauseCode deathCause,
        string reasonCode,
        bool allowDeath)
    {
        float previousRatio = HealthRatio(state);
        vitalsAuthority.Damage(
            actor,
            state,
            amount,
            deathCause,
            reasonCode,
            allowDeath);
        PublishHealthTransition(
            actor,
            previousRatio,
            HealthRatio(state),
            deathCause,
            reasonCode);
    }

    private void ApplyAggregateHealing(
        CharacterActor actor,
        CharacterBodyHealthState state,
        float amount)
    {
        float previousRatio = HealthRatio(state);
        vitalsAuthority.Heal(actor, state, amount);
        PublishHealthTransition(
            actor,
            previousRatio,
            HealthRatio(state),
            CharacterDeathCauseCode.Unknown,
            "healing");
    }

    private static float HealthRatio(CharacterBodyHealthState state) =>
        state == null
            ? 0f
            : Mathf.Clamp01(
                state.currentHealth / Mathf.Max(1f, state.maxHealth));

    private static void RefreshDerivedMaximumHealthForInstalledPart(
        CharacterActor actor,
        CharacterBodyHealthState state)
    {
        if (state?.anatomyNodes == null
            || !state.anatomyNodes.Any(node => node != null
                && !string.IsNullOrWhiteSpace(node.installedPartId)))
        {
            return;
        }

        actor.Stats.RefreshDerivedMaximumHealthPreservingCurrent();
    }

    private int RequireCalendarDay()
    {
        // Elapsed engine seconds restart independently of a restored campaign.
        // Health transition dates share the same calendar as combat/death facts;
        // gameClock remains the authority for delta times and health ticking.
        int day = gameCalendar.Day;
        if (day < 0)
            throw new InvalidOperationException("Body-health calendar day is invalid.");
        return day;
    }

    private void PublishHealthTransition(
        CharacterActor actor,
        float previousRatio,
        float currentRatio,
        CharacterDeathCauseCode deathCause,
        string reasonCode)
    {
        if (!CharacterPersistentIdentity.TryGet(actor, out CharacterId id))
            return;
        int day = RequireCalendarDay();
        bool crossedCritical = previousRatio > 0.20f && currentRatio <= 0.20f;
        bool recoveredFromCritical = previousRatio <= 0.20f
            && currentRatio > 0.20f;
        if (crossedCritical || recoveredFromCritical)
        {
            gameEventBus.Publish(new HealthThresholdCrossedEvent(
                id,
                previousRatio,
                currentRatio,
                coreOrganCritical: false,
                day));
        }
        if (previousRatio > 0f && currentRatio <= 0f)
        {
            string causeId = !string.IsNullOrWhiteSpace(reasonCode)
                ? reasonCode.Trim()
                : deathCause.ToString();
            gameEventBus.Publish(new CharacterDiedEvent(id, causeId, day));
        }
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
        nextHealingRecoveryTime = 0f;
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
