using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

public readonly struct CharacterDirectOrderCostPreview
{
    public CharacterDirectOrderCostPreview(
        float moodDelta,
        float stressDelta,
        IReadOnlyList<string> ruleIds,
        int durationDays)
    {
        MoodDelta = moodDelta;
        StressDelta = stressDelta;
        RuleIds = ruleIds ?? Array.Empty<string>();
        DurationDays = Math.Max(1, durationDays);
    }

    public float MoodDelta { get; }
    public float StressDelta { get; }
    public IReadOnlyList<string> RuleIds { get; }
    public int DurationDays { get; }
    public bool HasCost => MoodDelta < 0f || StressDelta > 0f;
}

public sealed class CharacterDirectOrderCostPreviewService
{
    private readonly CharacterIdentityRuleRouter router;
    private readonly CharacterMoodPolicyService moods;

    public CharacterDirectOrderCostPreviewService(
        CharacterIdentityRuleRouter router,
        CharacterMoodPolicyService moods)
    {
        this.router = router ?? throw new ArgumentNullException(nameof(router));
        this.moods = moods ?? throw new ArgumentNullException(nameof(moods));
    }

    public CharacterDirectOrderCostPreview Preview(CharacterActor actor, string actionTag)
    {
        PostActionConsequenceRule[] rules = router.ResolvePostAction(
                actor,
                actionTag,
                CharacterCommandOrigin.DirectPlayerOrder)
            .ToArray();
        return new CharacterDirectOrderCostPreview(
            rules.Sum(rule => rule.moodDelta),
            rules.Sum(rule => rule.stressDelta),
            rules.Select(rule => rule.ruleId).ToArray(),
            rules.Length > 0
                ? rules.Max(rule => Math.Max(1, rule.durationDays))
                : 1);
    }

    [GameplayInternalOnly(
        "Work command completion applies the exact cost previously exposed by the UI preview.",
        "WorkIdentityEventAdapter")]
    public CharacterDirectOrderCostPreview Apply(CharacterActor actor, string actionTag)
    {
        CharacterDirectOrderCostPreview preview = Preview(actor, actionTag);
        if (!Mathf.Approximately(preview.MoodDelta, 0f))
            moods.Apply(
                actor,
                $"post-action:{actionTag?.Trim()}",
                preview.MoodDelta,
                preview.DurationDays,
                "직접 명령의 후유증");
        if (!Mathf.Approximately(preview.StressDelta, 0f))
            actor?.Lifecycle?.ApplyStressDelta(preview.StressDelta);
        return preview;
    }
}

[Serializable]
public sealed class CharacterRelationshipMemoryEntrySaveData
{
    public string offenderCharacterId = string.Empty;
    public string eventId = string.Empty;
    public float severity;
    public int createdDay;
}

[Serializable]
public sealed class CharacterRelationshipMemoryRuleState
{
    public List<CharacterRelationshipMemoryEntrySaveData> entries = new();
}

public sealed class CharacterRelationshipMemoryService
{
    private readonly CharacterIdentityRuleRouter router;
    private readonly CharacterIdentityStateStore states;

    public CharacterRelationshipMemoryService(
        CharacterIdentityRuleRouter router,
        CharacterIdentityStateStore states)
    {
        this.router = router ?? throw new ArgumentNullException(nameof(router));
        this.states = states ?? throw new ArgumentNullException(nameof(states));
    }

    [GameplayInternalOnly(
        "Social and combat adapters create relationship memories from typed events.",
        "SocialIdentityEventAdapter|CombatResolutionService")]
    public void Remember(
        CharacterActor recipient,
        CharacterActor offender,
        string eventId,
        float severity,
        int absoluteDay)
    {
        if (recipient == null || offender == null || recipient == offender) return;
        string normalizedEvent = eventId?.Trim() ?? string.Empty;
        foreach ((int traitId, CharacterIdentityRule identityRule) in router.Resolve(recipient))
        {
            if (identityRule is not RelationshipMemoryRule rule
                || (!string.Equals(rule.eventId, normalizedEvent, StringComparison.Ordinal)
                    && !(rule.apologyCanClear
                        && string.Equals(
                            normalizedEvent,
                            "betrayal-or-assault",
                            StringComparison.Ordinal))))
                continue;

            CharacterTraitSO trait = recipient.Progression.ResolveSelectedTraits()
                .First(value => value != null && value.id == traitId);
            string recipientId = ActorId(recipient);
            CharacterRelationshipMemoryRuleState state = Read(recipientId, trait, rule);
            state.entries.RemoveAll(entry =>
                string.Equals(entry.offenderCharacterId, ActorId(offender), StringComparison.Ordinal)
                && string.Equals(entry.eventId, normalizedEvent, StringComparison.Ordinal));
            state.entries.Add(new CharacterRelationshipMemoryEntrySaveData
            {
                offenderCharacterId = ActorId(offender),
                eventId = normalizedEvent,
                severity = Mathf.Max(0f, severity),
                createdDay = Math.Max(0, absoluteDay)
            });
            Write(recipientId, trait, rule, state);
            CharacterPerformanceSnapshot recovery = recipient.Stats.EvaluatePerformance(
                CharacterPerformanceFormulaIds.RelationshipRecovery,
                new CharacterPerformanceEvaluationContext
                {
                    GameplayEffectContext = new GameplayEffectContext(
                        new[] { "relationship:negative" })
                });
            if (!recovery.IsApplicable)
                throw new InvalidOperationException(
                    recovery.Failure?.Message
                    ?? "Relationship recovery is unavailable.");
            float recoveryRate = rule.dailyDecay * recovery.Value;
            float memoryDuration = recoveryRate <= 0f
                ? 0f
                : Mathf.Max(1f, 20f / recoveryRate)
                    * GameCalendarRules.SecondsPerDay;
            recipient.SocialMemory?.RememberCharacterExperience(
                offender,
                Mathf.Clamp(rule.relationshipDelta / 20f, -1f, 1f),
                "관계 사건을 기억함",
                durationSeconds: memoryDuration);
            CharacterPerformanceExecutionTrace.Record(
                CharacterPerformanceFormulaIds.RelationshipRecovery,
                "CharacterRelationshipMemoryService.Remember",
                rule.dailyDecay,
                memoryDuration,
                normalizedEvent);
        }
    }

    [GameplayInternalOnly(
        "The apology event adapter owns forgiveness after a validated player apology.",
        "SocialIdentityEventAdapter")]
    public bool TryForgive(
        CharacterActor recipient,
        CharacterActor offender,
        string offenseId,
        bool restitutionProvided)
    {
        if (recipient == null || offender == null) return false;
        bool changed = false;
        foreach ((int traitId, CharacterIdentityRule identityRule) in router.Resolve(recipient))
        {
            if (identityRule is not RelationshipMemoryRule rule || !rule.apologyCanClear
                || (rule.restitutionRequired && !restitutionProvided))
                continue;
            CharacterTraitSO trait = recipient.Progression.ResolveSelectedTraits()
                .First(value => value != null && value.id == traitId);
            string recipientId = ActorId(recipient);
            CharacterRelationshipMemoryRuleState state = Read(recipientId, trait, rule);
            int removed = state.entries.RemoveAll(entry =>
                string.Equals(entry.offenderCharacterId, ActorId(offender), StringComparison.Ordinal)
                && (string.IsNullOrWhiteSpace(offenseId)
                    || string.Equals(entry.eventId, offenseId.Trim(), StringComparison.Ordinal)));
            if (removed <= 0) continue;
            Write(recipientId, trait, rule, state);
            CharacterPerformanceSnapshot recovery = recipient.Stats.EvaluatePerformance(
                CharacterPerformanceFormulaIds.RelationshipRecovery,
                new CharacterPerformanceEvaluationContext
                {
                    GameplayEffectContext = new GameplayEffectContext(
                        new[] { "relationship:first-apology" })
                });
            if (!recovery.IsApplicable)
                throw new InvalidOperationException(
                    recovery.Failure?.Message
                    ?? "Relationship recovery is unavailable.");
            float apologyRecovery = Mathf.Abs(rule.relationshipDelta)
                * 1.5f / 20f * recovery.Value;
            recipient.SocialMemory?.RememberCharacterExperience(
                offender,
                Mathf.Clamp(apologyRecovery, 0f, 1f),
                "사과와 보상을 받아들임",
                3f * GameCalendarRules.SecondsPerDay);
            CharacterPerformanceExecutionTrace.Record(
                CharacterPerformanceFormulaIds.RelationshipRecovery,
                "CharacterRelationshipMemoryService.TryForgive",
                Mathf.Abs(rule.relationshipDelta) * 1.5f / 20f,
                apologyRecovery,
                offenseId);
            changed = true;
        }
        return changed;
    }

    public bool CanForgive(
        CharacterActor recipient,
        CharacterActor offender,
        bool restitutionProvided)
    {
        if (recipient == null || offender == null || recipient == offender)
            return false;
        string offenderId = ActorId(offender);
        foreach ((int traitId, CharacterIdentityRule identityRule) in router.Resolve(recipient))
        {
            if (identityRule is not RelationshipMemoryRule rule
                || !rule.apologyCanClear
                || (rule.restitutionRequired && !restitutionProvided))
                continue;
            CharacterTraitSO trait = recipient.Progression.ResolveSelectedTraits()
                .First(value => value != null && value.id == traitId);
            CharacterRelationshipMemoryRuleState state = Read(
                ActorId(recipient),
                trait,
                rule);
            if (state.entries.Any(entry => string.Equals(
                    entry.offenderCharacterId,
                    offenderId,
                    StringComparison.Ordinal)))
                return true;
        }
        return false;
    }

    private CharacterRelationshipMemoryRuleState Read(
        string characterId,
        CharacterTraitSO trait,
        RelationshipMemoryRule rule)
    {
        if (states.TryGet(characterId, trait.DefinitionId.Value, rule.ruleId, out CharacterIdentityRuleStateSaveData saved)
            && !string.IsNullOrWhiteSpace(saved.statePayload))
            return JsonUtility.FromJson<CharacterRelationshipMemoryRuleState>(saved.statePayload)
                ?? new CharacterRelationshipMemoryRuleState();
        return new CharacterRelationshipMemoryRuleState();
    }

    private void Write(
        string characterId,
        CharacterTraitSO trait,
        RelationshipMemoryRule rule,
        CharacterRelationshipMemoryRuleState state) =>
        states.Set(characterId, trait.DefinitionId.Value, rule.ruleId, 1, JsonUtility.ToJson(state));

    private static string ActorId(CharacterActor actor) =>
        !string.IsNullOrWhiteSpace(actor?.Identity?.PersistentId)
            ? actor.Identity.PersistentId.Trim()
            : throw new InvalidOperationException("Identity memory requires a persistent character id.");
}

public abstract class CharacterIdentityEventAdapterBase : IStartable, IDisposable
{
    private readonly List<IDisposable> subscriptions = new();
    protected readonly IGameEventBus Events;
    protected readonly ICharacterWorldQuery World;
    protected readonly CharacterMoodPolicyService Moods;

    protected CharacterIdentityEventAdapterBase(
        IGameEventBus events,
        ICharacterWorldQuery world,
        CharacterMoodPolicyService moods)
    {
        Events = events ?? throw new ArgumentNullException(nameof(events));
        World = world ?? throw new ArgumentNullException(nameof(world));
        Moods = moods ?? throw new ArgumentNullException(nameof(moods));
    }

    [GameplayInternalOnly(
        "The runtime entry-point container starts each registered identity adapter exactly once.",
        "IStartable|DungeonCharacterRegistration")]
    public abstract void Start();

    [GameplayInternalOnly(
        "The runtime lifetime container disposes event subscriptions for registered identity adapters.",
        "IDisposable|DungeonCharacterRegistration")]
    public void Dispose()
    {
        foreach (IDisposable subscription in subscriptions) subscription?.Dispose();
        subscriptions.Clear();
    }

    protected void Subscribe<T>(Action<T> handler) =>
        subscriptions.Add(Events.Subscribe<T>(handler));

    protected CharacterActor Find(CharacterId id) => World.Characters.FirstOrDefault(actor =>
        CharacterPersistentIdentity.TryGet(actor, out CharacterId candidate) && candidate.Equals(id));
}

public sealed class MealIdentityEventAdapter : CharacterIdentityEventAdapterBase
{
    public MealIdentityEventAdapter(IGameEventBus events, ICharacterWorldQuery world, CharacterMoodPolicyService moods)
        : base(events, world, moods) { }
    [GameplayInternalOnly(
        "The runtime entry-point container starts this registered identity adapter exactly once.",
        "IStartable|DungeonCharacterRegistration")]
    public override void Start()
    {
        Subscribe<MealConsumedEvent>(OnConsumed);
        Subscribe<MealMissedEvent>(OnMissed);
    }
    private void OnConsumed(MealConsumedEvent e)
    {
        CharacterActor actor = Find(e.Character);
        if (e.WasSufficient) Moods.Apply(actor, "food:sated", 0f, 1, "충분한 식사");
        if (e.Tags.Contains("sweet", StringComparer.Ordinal)) Moods.Apply(actor, "food:sweet", 0f, 1, "단 음식");
        if (e.Tags.Contains("salted", StringComparer.Ordinal)) Moods.Apply(actor, "food:salted", 0f, 1, "염장식");
        if (e.Tags.Contains("unfamiliar", StringComparer.Ordinal)) Moods.Apply(actor, "food:new-meal", 0f, 1, "새로운 식사");
        Moods.Apply(
            actor,
            e.Tags.Contains("luxury", StringComparer.Ordinal)
                ? "living:luxury-satisfied"
                : "living:basic-only",
            0f,
            2,
            e.Tags.Contains("luxury", StringComparer.Ordinal)
                ? "호화로운 식사"
                : "기본 생활 식사");
    }
    private void OnMissed(MealMissedEvent e) =>
        Moods.Apply(Find(e.Character), "food:meal-missed", 0f, 1, "식사 부족");
}

public sealed class CharacterDeathIdentityEventAdapter :
    CharacterIdentityEventAdapterBase
{
    private readonly CharacterIdentityStateStore states;
    private readonly ICharacterIdentityDeathStateRetentionPolicy[]
        retentionPolicies;

    public CharacterDeathIdentityEventAdapter(
        IGameEventBus events,
        ICharacterWorldQuery world,
        CharacterMoodPolicyService moods,
        CharacterIdentityStateStore states,
        IEnumerable<ICharacterIdentityDeathStateRetentionPolicy>
            retentionPolicies)
        : base(events, world, moods)
    {
        this.states = states ?? throw new ArgumentNullException(nameof(states));
        this.retentionPolicies = (retentionPolicies
                ?? Array.Empty<ICharacterIdentityDeathStateRetentionPolicy>())
            .Where(value => value != null)
            .ToArray();
    }

    [GameplayInternalOnly(
        "The runtime entry-point container starts this registered identity adapter exactly once.",
        "IStartable|DungeonCharacterRegistration")]
    public override void Start() =>
        Subscribe<CharacterDiedEvent>(OnDied);

    private void OnDied(CharacterDiedEvent e) =>
        states.RemoveCharacter(e.Character.Value, retentionPolicies);
}

public sealed class WorkIdentityEventAdapter :
    CharacterIdentityEventAdapterBase,
    IWorkCompletionIdentityDeliveryCommand
{
    private readonly CharacterDirectOrderCostPreviewService directOrders;
    private readonly CharacterIdentityStateStore states;
    private readonly ICharacterEnvironmentStatusQuery environment;
    private readonly CharacterPersistentNeedRuntime persistentNeeds;
    private readonly WorkCompletionIdentityDeliveryLedger completionDeliveries;
    private readonly ICharacterLifetimeQuery characterLifetime;

    [Serializable]
    private sealed class WorkEventHistoryState
    {
        public List<string> completedProcessIds = new();
        public int lastSmallSuccessDay = -1;
    }

    public WorkIdentityEventAdapter(
        IGameEventBus events,
        ICharacterWorldQuery world,
        CharacterMoodPolicyService moods,
        CharacterDirectOrderCostPreviewService directOrders,
        CharacterIdentityStateStore states,
        ICharacterEnvironmentStatusQuery environment,
        CharacterPersistentNeedRuntime persistentNeeds,
        WorkCompletionIdentityDeliveryLedger completionDeliveries,
        ICharacterLifetimeQuery characterLifetime)
        : base(events, world, moods)
    {
        this.directOrders = directOrders
            ?? throw new ArgumentNullException(nameof(directOrders));
        this.states = states ?? throw new ArgumentNullException(nameof(states));
        this.environment = environment
            ?? throw new ArgumentNullException(nameof(environment));
        this.persistentNeeds = persistentNeeds
            ?? throw new ArgumentNullException(nameof(persistentNeeds));
        this.completionDeliveries = completionDeliveries
            ?? throw new ArgumentNullException(nameof(completionDeliveries));
        this.characterLifetime = characterLifetime
            ?? throw new ArgumentNullException(nameof(characterLifetime));
    }

    [GameplayInternalOnly(
        "The runtime entry-point container starts this registered identity adapter exactly once.",
        "IStartable|DungeonCharacterRegistration")]
    public override void Start()
    {
        Subscribe<WorkStartedIdentityEvent>(OnStarted);
        Subscribe<WorkCompletedIdentityEvent>(OnCompleted);
    }

    private void OnStarted(WorkStartedIdentityEvent e)
    {
        CharacterActor actor = Find(e.Character);
        Moods.Apply(
            actor,
            $"work:started:{e.WorkId}",
            0f,
            1,
            "작업 시작");
        if (actor == null) return;

        if (IsMedicalWork(e.WorkId))
            Moods.Apply(actor, "medical:entered-clinic", 0f, 1, "의료 작업 시작");

        if (e.Origin != CharacterCommandOrigin.DirectPlayerOrder)
            return;
        if (IsDangerousWork(e.WorkId))
            Moods.Apply(actor, "danger:directly-assigned", 0f, 2, "위험 작업 직접 배정");

        IReadOnlyList<string> workConditions = actor.TryGetAbility(
                out AbilityWork work)
            ? work.GetActiveGameplayEffectConditionIds()
            : Array.Empty<string>();
        if (!workConditions.Contains("work:on-schedule", StringComparer.Ordinal))
        {
            Moods.Apply(
                actor,
                "schedule:sudden-reassignment",
                0f,
                1,
                "일정 밖 직접 배정");
        }

        float daySeconds = GameCalendarRules.SecondsPerDay;
        float time = actor.GameClock?.Time ?? 0f;
        float hour = daySeconds <= 0f
            ? 12f
            : time % daySeconds / daySeconds * 24f;
        if (hour >= 6f && hour < 18f)
            Moods.Apply(actor, "shift:forced-day", 0f, 1, "주간 강제 교대");
    }

    public WorkCompletionIdentityDeliveryResult EnsureApplied(
        WorkCompletionIdentityDeliveryRequest request)
    {
        if (request.Origin != CharacterCommandOrigin.Autonomous
            || !string.Equals(
                request.WorkId,
                BuiltInWorkTypeIds.Harvest.Value,
                StringComparison.Ordinal))
            return new(
                WorkCompletionIdentityDeliveryStatus.Conflict,
                "Durable work completion currently supports autonomous harvest only.");
        WorkCompletionIdentityDeliveryStatus status = completionDeliveries
            .Inspect(request, out string failureReason);
        if (status == WorkCompletionIdentityDeliveryStatus.AlreadyApplied)
            return new(status);
        if (status == WorkCompletionIdentityDeliveryStatus.Conflict)
            return new(status, failureReason);
        CharacterActor actor = Find(request.Character);
        if (actor == null)
        {
            CharacterActor lifetimeActor = characterLifetime.AllCharacters
                .FirstOrDefault(candidate => candidate != null
                    && candidate.Identity != null
                    && candidate.Identity.TypedPersistentId.Equals(
                        request.Character));
            bool terminal = lifetimeActor == null
                || lifetimeActor.IsDead
                || lifetimeActor.CurrentLifecycleState ==
                    CharacterLifecycleState.Despawned;
            if (!terminal)
                return new(
                    WorkCompletionIdentityDeliveryStatus.Deferred,
                    "The completion character is temporarily unavailable in the live world.");
            WorkCompletionIdentityDeliveryStatus terminalCommit =
                completionDeliveries.Commit(
                    request,
                    out failureReason,
                    WorkCompletionIdentityDeliveryDisposition
                        .TerminalRecipientUnavailable);
            if (terminalCommit == WorkCompletionIdentityDeliveryStatus.Conflict)
                return new(terminalCommit, failureReason);
            return new(terminalCommit);
        }

        completionDeliveries.BeginApply(request);
        IReadOnlyList<CharacterIdentityRuntimeStateSaveData> identityBefore = null;
        CharacterMoodDeliveryTransactionSnapshot moodBefore = null;
        CharacterProgressionSnapshot progressionBefore = null;
        try
        {
            identityBefore = states.Capture();
            moodBefore = actor.Stats?.CaptureMoodDeliveryTransactionState();
            progressionBefore = actor.Progression?.CapturePersistentState();
            ApplyCompleted(request.ToEvent(), actor);
            WorkCompletionIdentityDeliveryStatus committed =
                completionDeliveries.Commit(request, out failureReason);
            if (committed == WorkCompletionIdentityDeliveryStatus.Conflict)
                throw new InvalidOperationException(
                    "Work-completion delivery changed during synchronous apply: "
                    + failureReason);
            return new(committed);
        }
        catch (Exception error)
        {
            List<Exception> rollbackFailures = new();
            try
            {
                if (identityBefore != null)
                    states.RestoreTrustedTransactionSnapshot(identityBefore);
            }
            catch (Exception rollbackError)
            {
                rollbackFailures.Add(rollbackError);
            }
            try
            {
                if (progressionBefore != null)
                    actor.Progression?.RestorePersistentState(progressionBefore);
            }
            catch (Exception rollbackError)
            {
                rollbackFailures.Add(rollbackError);
            }
            try
            {
                if (moodBefore != null)
                    actor.Stats?.RestoreMoodDeliveryTransactionState(moodBefore);
            }
            catch (Exception rollbackError)
            {
                rollbackFailures.Add(rollbackError);
            }
            if (rollbackFailures.Count != 0)
            {
                rollbackFailures.Insert(0, error);
                throw new AggregateException(
                    "Work-completion delivery apply and rollback failed.",
                    rollbackFailures);
            }
            throw;
        }
        finally
        {
            completionDeliveries.EndApply(request);
        }
    }

    public bool RetireProducerStream(string producerStreamId) =>
        completionDeliveries.RetireProducerStream(producerStreamId);

    private void OnCompleted(WorkCompletedIdentityEvent e)
    {
        CharacterActor actor = Find(e.Character);
        if (actor == null) return;
        ApplyCompleted(e, actor);
    }

    private void ApplyCompleted(
        WorkCompletedIdentityEvent e,
        CharacterActor actor)
    {
        if (e.Origin == CharacterCommandOrigin.DirectPlayerOrder)
            directOrders.Apply(actor, e.WorkId);

        bool failed = e.ProductId.StartsWith(
            "outcome:",
            StringComparison.Ordinal);
        if (failed)
        {
            Moods.Apply(actor, "work:failed", 0f, 5, "작업 실패");
            return;
        }

        Moods.Apply(actor, $"work:completed:{e.WorkId}", 0f, 1, "작업 완료");
        if (string.Equals(
                e.WorkId,
                "work:combat-training",
                StringComparison.Ordinal))
        {
            persistentNeeds.MarkSatisfied(actor, "need:combat-action");
        }
        ApplyOncePerDaySmallSuccess(actor, e.AbsoluteDay);
        ApplyFirstProcessSuccess(actor, e.WorkId);

        if (IsDangerousWork(e.WorkId))
        {
            Moods.Apply(actor, "danger:safe-return", 0f, 1, "안전 복귀");
            Moods.Apply(actor, "danger:success", 0f, 2, "위험 작업 성공");
        }

        if (string.Equals(
                e.WorkId,
                BuiltInWorkTypeIds.Rescue.Value,
                StringComparison.Ordinal)
            && IsOnRoughTerrain(actor))
        {
            Moods.Apply(
                actor,
                "terrain:rough-crossed-safely",
                0f,
                1,
                "험지 구조 완료");
        }

        CharacterEnvironmentExposure exposure = environment.GetExposure(
            e.Character);
        if ((exposure?.coldExposure ?? 0f) > 0.001f
            && environment.GetPhysiologicalBand(e.Character)
                < EnvironmentalExposureBand.Impaired)
        {
            Moods.Apply(
                actor,
                "temperature:safe-cold-work-complete",
                0f,
                1,
                "안전한 냉기 작업 완료");
        }
    }

    private void ApplyOncePerDaySmallSuccess(CharacterActor actor, int day)
    {
        foreach ((CharacterTraitSO trait, EventMoodRule rule) in ResolveEventRules(
                     actor,
                     "work:small-success"))
        {
            WorkEventHistoryState history = ReadHistory(actor, trait, rule);
            if (history.lastSmallSuccessDay == day) continue;
            history.lastSmallSuccessDay = day;
            WriteHistory(actor, trait, rule, history);
            Moods.Apply(actor, "work:small-success", 0f, 1, "하루 첫 작은 성공");
        }
    }

    private void ApplyFirstProcessSuccess(CharacterActor actor, string workId)
    {
        foreach ((CharacterTraitSO trait, EventMoodRule rule) in ResolveEventRules(
                     actor,
                     "work:first-process-success"))
        {
            WorkEventHistoryState history = ReadHistory(actor, trait, rule);
            if (history.completedProcessIds.Contains(workId, StringComparer.Ordinal))
                continue;
            history.completedProcessIds.Add(workId);
            history.completedProcessIds.Sort(StringComparer.Ordinal);
            WriteHistory(actor, trait, rule, history);
            Moods.Apply(
                actor,
                "work:first-process-success",
                0f,
                1,
                "공정 첫 성공");
        }
    }

    private static IEnumerable<(CharacterTraitSO Trait, EventMoodRule Rule)>
        ResolveEventRules(CharacterActor actor, string eventId) =>
        (actor.Progression?.ResolveSelectedTraits()
            ?? Array.Empty<CharacterTraitSO>())
        .Where(trait => trait != null)
        .OrderBy(trait => trait.id)
        .SelectMany(trait => (trait.identityRules
                ?? new List<CharacterIdentityRule>())
            .OfType<EventMoodRule>()
            .Where(rule => string.Equals(
                rule.eventId,
                eventId,
                StringComparison.Ordinal))
            .Select(rule => (trait, rule)));

    private WorkEventHistoryState ReadHistory(
        CharacterActor actor,
        CharacterTraitSO trait,
        EventMoodRule rule)
    {
        if (states.TryGet(
                actor.Identity.PersistentId,
                trait.DefinitionId.Value,
                rule.ruleId,
                out CharacterIdentityRuleStateSaveData saved)
            && !string.IsNullOrWhiteSpace(saved.statePayload))
        {
            return JsonUtility.FromJson<WorkEventHistoryState>(saved.statePayload)
                ?? new WorkEventHistoryState();
        }
        return new WorkEventHistoryState();
    }

    private void WriteHistory(
        CharacterActor actor,
        CharacterTraitSO trait,
        EventMoodRule rule,
        WorkEventHistoryState history) => states.Set(
        actor.Identity.PersistentId,
        trait.DefinitionId.Value,
        rule.ruleId,
        1,
        JsonUtility.ToJson(history));

    private static bool IsDangerousWork(string workId) =>
        string.Equals(workId, BuiltInWorkTypeIds.Guard.Value, StringComparison.Ordinal)
        || string.Equals(workId, BuiltInWorkTypeIds.Hunt.Value, StringComparison.Ordinal)
        || string.Equals(workId, BuiltInWorkTypeIds.Rescue.Value, StringComparison.Ordinal)
        || string.Equals(workId, BuiltInWorkTypeIds.ThreatMitigation.Value, StringComparison.Ordinal);

    private static bool IsMedicalWork(string workId) =>
        string.Equals(workId, BuiltInWorkTypeIds.Treat.Value, StringComparison.Ordinal)
        || string.Equals(workId, BuiltInWorkTypeIds.Surgery.Value, StringComparison.Ordinal)
        || workId.StartsWith("surgery:", StringComparison.Ordinal);

    private static bool IsOnRoughTerrain(CharacterActor actor)
    {
        if (actor == null
            || !actor.TryGetAbility(out AbilityWork work)
            || work.CachedGrid == null)
            return false;
        GridCell cell = work.CachedGrid.GetGridCell(
            work.CachedGrid.GetXY(actor.transform.position));
        return cell != null && cell.TerrainType != GridCellTerrainType.Dry;
    }
}

public sealed class ProductIdentityEventAdapter : CharacterIdentityEventAdapterBase
{
    public ProductIdentityEventAdapter(
        IGameEventBus events,
        ICharacterWorldQuery world,
        CharacterMoodPolicyService moods)
        : base(events, world, moods) { }

    [GameplayInternalOnly(
        "The runtime entry-point container starts this registered identity adapter exactly once.",
        "IStartable|DungeonCharacterRegistration")]
    public override void Start() =>
        Subscribe<ProductQualityResolvedEvent>(OnResolved);

    private void OnResolved(ProductQualityResolvedEvent e)
    {
        CharacterActor maker = Find(e.Maker);
        if (e.Quality <= CraftsmanshipQualityTier.Poor)
        {
            Moods.Apply(maker, "product:quality-low", 0f, 2, "낮은 완제품 품질");
            Moods.Apply(maker, "product:defect-found", 0f, 1, "완제품 결함 발견");
        }
        if (e.RejectedBelowMinimum)
        {
            Moods.Apply(
                maker,
                "product:defect-caught-before-release",
                0f,
                1,
                "출고 전 결함 차단");
        }
        if (e.Quality >= CraftsmanshipQualityTier.Masterwork)
            Moods.Apply(
                maker,
                "product:quality-masterwork",
                0f,
                2,
                "뛰어난 완제품 품질");
    }
}

public sealed class RestIdentityEventAdapter : CharacterIdentityEventAdapterBase
{
    public RestIdentityEventAdapter(
        IGameEventBus events,
        ICharacterWorldQuery world,
        CharacterMoodPolicyService moods)
        : base(events, world, moods) { }

    [GameplayInternalOnly(
        "The runtime entry-point container starts this registered identity adapter exactly once.",
        "IStartable|DungeonCharacterRegistration")]
    public override void Start() =>
        Subscribe<RestOutcomeIdentityEvent>(OnRestOutcome);

    private void OnRestOutcome(RestOutcomeIdentityEvent e)
    {
        CharacterActor actor = Find(e.Character);
        if (actor == null) return;
        if (e.ConditionIds.Contains("room:noise", StringComparer.Ordinal))
            Moods.Apply(actor, "sleep:noisy", 0f, 1, "소음 속 수면");
        if (e.ConditionIds.Contains("room:private", StringComparer.Ordinal))
            Moods.Apply(actor, "rest:private", 0f, 1, "개인 휴식");
        if (e.PreviousSleep < 80f && e.CurrentSleep >= 80f)
            Moods.Apply(actor, "rest:sufficient", 0f, 1, "충분한 휴식");
    }
}

public sealed class ResearchIdentityEventAdapter : CharacterIdentityEventAdapterBase
{
    private readonly CharacterPersistentNeedRuntime persistentNeeds;

    public ResearchIdentityEventAdapter(
        IGameEventBus events,
        ICharacterWorldQuery world,
        CharacterMoodPolicyService moods,
        CharacterPersistentNeedRuntime persistentNeeds)
        : base(events, world, moods)
    {
        this.persistentNeeds = persistentNeeds
            ?? throw new ArgumentNullException(nameof(persistentNeeds));
    }
    [GameplayInternalOnly(
        "The runtime entry-point container starts this registered identity adapter exactly once.",
        "IStartable|DungeonCharacterRegistration")]
    public override void Start()
    {
        Subscribe<ResearchProgressEvent>(OnProgress);
        Subscribe<ResearchOutcomeEvent>(e =>
            Moods.Apply(
                Find(e.Researcher),
                $"research:{e.OutcomeId}",
                0f,
                2,
                "연구 결과"));
    }
    private void OnProgress(ResearchProgressEvent e)
    {
        if (e.ApprovedWork > 0f && e.ProgressDelta > 0f)
            persistentNeeds.MarkSatisfied(
                Find(e.Researcher),
                "need:research-access");
    }
}

public sealed class SocialIdentityEventAdapter : CharacterIdentityEventAdapterBase
{
    private readonly CharacterRelationshipMemoryService memories;
    public SocialIdentityEventAdapter(IGameEventBus events, ICharacterWorldQuery world, CharacterMoodPolicyService moods, CharacterRelationshipMemoryService memories)
        : base(events, world, moods) => this.memories = memories;
    [GameplayInternalOnly(
        "The runtime entry-point container starts this registered identity adapter exactly once.",
        "IStartable|DungeonCharacterRegistration")]
    public override void Start()
    {
        Subscribe<SocialConflictEvent>(OnConflict);
        Subscribe<ApologyEvent>(OnApology);
    }
    private void OnConflict(SocialConflictEvent e)
    {
        CharacterActor instigator = Find(e.Instigator);
        CharacterActor target = Find(e.Target);
        bool publicQuestion = string.Equals(
            e.ConflictId,
            "public-question",
            StringComparison.Ordinal);
        if (!publicQuestion)
            memories.Remember(target, instigator, e.ConflictId, e.Severity, e.AbsoluteDay);
        string eventId = $"social:{e.ConflictId}";
        bool hasAuthoredMood = (target?.Progression?.ResolveSelectedTraits()
                ?? Array.Empty<CharacterTraitSO>())
            .Where(trait => trait != null)
            .SelectMany(trait => trait.identityRules
                ?? new List<CharacterIdentityRule>())
            .OfType<EventMoodRule>()
            .Any(rule => string.Equals(
                rule.eventId,
                eventId,
                StringComparison.Ordinal));
        Moods.Apply(
            target,
            eventId,
            hasAuthoredMood || publicQuestion
                ? 0f
                : -Mathf.Max(1f, e.Severity),
            2,
            "사회적 충돌");

        if (string.Equals(e.ConflictId, "insulted", StringComparison.Ordinal)
            && HasBehaviorTag(target, "social:answer-insult"))
        {
            target.AddActivity(CharacterActivityEvent.Create(
                CharacterActivityKinds.Social,
                CharacterActivityOutcomes.Responded,
                $"{instigator?.Identity?.DisplayName ?? "상대"}의 모욕에 즉시 맞섰다.",
                actionId: "social:answer-insult",
                reasonCode: "trait:hot-blooded",
                sentiment: 0.35f,
                bubbleEligible: true));
            Moods.Apply(
                target,
                "social:insult-answered",
                0f,
                1,
                "모욕에 맞섬");
        }
    }

    private static bool HasBehaviorTag(CharacterActor actor, string behaviorTag) =>
        (actor?.Progression?.ResolveSelectedTraits()
            ?? Array.Empty<CharacterTraitSO>())
        .Where(trait => trait != null)
        .SelectMany(trait => trait.identityRules
            ?? new List<CharacterIdentityRule>())
        .OfType<BehaviorUtilityRule>()
        .Any(rule => string.Equals(
            rule.behaviorTag,
            behaviorTag,
            StringComparison.Ordinal));
    private void OnApology(ApologyEvent e)
    {
        CharacterActor recipient = Find(e.Recipient);
        if (memories.TryForgive(recipient, Find(e.Offender), e.OffenseId, e.RestitutionProvided))
            Moods.Apply(recipient, "social:sincere-apology", 0f, 2, "진정한 사과");
    }
}

public sealed class FestivalIdentityEventAdapter : CharacterIdentityEventAdapterBase
{
    public FestivalIdentityEventAdapter(IGameEventBus events, ICharacterWorldQuery world, CharacterMoodPolicyService moods)
        : base(events, world, moods) { }
    [GameplayInternalOnly(
        "The runtime entry-point container starts this registered identity adapter exactly once.",
        "IStartable|DungeonCharacterRegistration")]
    public override void Start() => Subscribe<FestivalOutcomeEvent>(e =>
        Moods.Apply(Find(e.Participant), $"festival:{e.OutcomeId}", 0f, 2, "축제 결과"));
}

public sealed class CaptivityIdentityEventAdapter : CharacterIdentityEventAdapterBase
{
    public CaptivityIdentityEventAdapter(IGameEventBus events, ICharacterWorldQuery world, CharacterMoodPolicyService moods)
        : base(events, world, moods) { }
    [GameplayInternalOnly(
        "The runtime entry-point container starts this registered identity adapter exactly once.",
        "IStartable|DungeonCharacterRegistration")]
    public override void Start() => Subscribe<PrisonerDecisionEvent>(e =>
        Moods.Apply(Find(e.Decider), $"prisoner:{e.DecisionId}", 0f, 2, "포로 결정"));
}

public sealed class ExpeditionIdentityEventAdapter : CharacterIdentityEventAdapterBase
{
    public ExpeditionIdentityEventAdapter(
        IGameEventBus events,
        ICharacterWorldQuery world,
        CharacterMoodPolicyService moods)
        : base(events, world, moods) { }

    [GameplayInternalOnly(
        "The runtime entry-point container starts this registered identity adapter exactly once.",
        "IStartable|DungeonCharacterRegistration")]
    public override void Start() => Subscribe<ExpeditionOutcomeEvent>(OnOutcome);

    private void OnOutcome(ExpeditionOutcomeEvent e)
    {
        bool success = string.Equals(
            e.OutcomeId,
            "success",
            StringComparison.Ordinal);
        foreach (CharacterId participant in e.Participants)
        {
            CharacterActor actor = Find(participant);
            Moods.Apply(
                actor,
                success ? "danger:success" : "expedition:failure",
                success ? 0f : -3f,
                2,
                success ? "위험한 원정 성공" : "원정 실패");
            if (success)
                Moods.Apply(actor, "combat:victory", 0f, 2, "원정 전투 승리");
        }
    }
}

public sealed class ApparelIdentityEventAdapter : CharacterIdentityEventAdapterBase
{
    public ApparelIdentityEventAdapter(IGameEventBus events, ICharacterWorldQuery world, CharacterMoodPolicyService moods)
        : base(events, world, moods) { }
    [GameplayInternalOnly(
        "The runtime entry-point container starts this registered identity adapter exactly once.",
        "IStartable|DungeonCharacterRegistration")]
    public override void Start() => Subscribe<ApparelChangedEvent>(e =>
        Moods.Apply(Find(e.Character), e.Equipped ? "apparel:equipped" : "apparel:removed", 0f, 1, "의복 변경"));
}

public sealed class RoomIdentityEventAdapter : CharacterIdentityEventAdapterBase
{
    public RoomIdentityEventAdapter(IGameEventBus events, ICharacterWorldQuery world, CharacterMoodPolicyService moods)
        : base(events, world, moods) { }
    [GameplayInternalOnly(
        "The runtime entry-point container starts this registered identity adapter exactly once.",
        "IStartable|DungeonCharacterRegistration")]
    public override void Start() => Subscribe<RoomConditionChangedEvent>(e =>
        Moods.Apply(Find(e.Observer), e.CurrentCleanliness >= e.PreviousCleanliness ? "room:cleaned" : "room:dirty", 0f, 1, "방 상태 변화"));
}

public sealed class HealthIdentityEventAdapter : CharacterIdentityEventAdapterBase
{
    public HealthIdentityEventAdapter(
        IGameEventBus events,
        ICharacterWorldQuery world,
        CharacterMoodPolicyService moods)
        : base(events, world, moods) { }

    [GameplayInternalOnly(
        "The runtime entry-point container starts this registered identity adapter exactly once.",
        "IStartable|DungeonCharacterRegistration")]
    public override void Start()
    {
        Subscribe<HealthThresholdCrossedEvent>(OnThresholdCrossed);
        Subscribe<CharacterInjuredIdentityEvent>(OnInjured);
    }

    private void OnThresholdCrossed(HealthThresholdCrossedEvent e)
    {
        CharacterActor actor = Find(e.Character);
        if (e.CurrentRatio > e.PreviousRatio)
            Moods.Apply(actor, "medical:severity-reduced", 0f, 2, "중상 단계 회복");
        else
            Moods.Apply(actor, "health:critical", 0f, 1, "생명력 위기");
    }

    private void OnInjured(CharacterInjuredIdentityEvent e)
    {
        if (e.AppliedDamage <= 0f)
            return;
        if (e.DamageType == CombatDamageType.Blunt)
            Moods.Apply(Find(e.Character), "injury:blunt", 0f, 2, "둔중 부상");
    }
}
