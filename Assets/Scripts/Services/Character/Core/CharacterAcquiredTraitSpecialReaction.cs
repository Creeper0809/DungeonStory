using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Characters;
using UnityEngine;
using VContainer.Unity;

public enum CharacterAcquiredTraitReactionTrigger
{
    WorkCompleted = 0,
    CharacterInjured = 1,
    ExpeditionOutcome = 2,
    ApologyCompleted = 3
}

public enum CharacterAcquiredTraitReactionOwnerRole
{
    Subject = 0,
    Recipient = 1,
    Participant = 2
}

public enum CharacterAcquiredTraitReactionCondition
{
    None = 0,
    ProductCreated = 1,
    WorkAccident = 2,
    ExpeditionSucceeded = 3,
    RestitutionProvided = 4
}

public enum CharacterAcquiredTraitReactionAction
{
    GrantExperience = 0,
    ApplyMoodImpulse = 1
}

[Serializable]
public sealed class CharacterAcquiredTraitSpecialReactionDefinition
{
    [SerializeField] private string reactionId = string.Empty;
    [SerializeField] private CharacterAcquiredTraitReactionTrigger trigger;
    [SerializeField] private CharacterAcquiredTraitReactionOwnerRole ownerRole;
    [SerializeField] private CharacterAcquiredTraitReactionCondition condition;
    [SerializeField] private CharacterAcquiredTraitReactionAction action;
    [SerializeField] private float baseValue;
    [SerializeField, Min(0)] private int durationDays;
    [SerializeField, Min(0)] private int cooldownDays;
    [SerializeField, Min(0)] private int maximumTriggersPerDay = 1;
    [SerializeField, Min(0)] private int maximumLifetimeTriggers;
    [SerializeField] private string displayLabel = string.Empty;

    public string ReactionId => reactionId?.Trim() ?? string.Empty;
    public CharacterAcquiredTraitReactionTrigger Trigger => trigger;
    public CharacterAcquiredTraitReactionOwnerRole OwnerRole => ownerRole;
    public CharacterAcquiredTraitReactionCondition Condition => condition;
    public CharacterAcquiredTraitReactionAction Action => action;
    public float BaseValue => baseValue;
    public int DurationDays => durationDays;
    public int CooldownDays => cooldownDays;
    public int MaximumTriggersPerDay => maximumTriggersPerDay;
    public int MaximumLifetimeTriggers => maximumLifetimeTriggers;
    public string DisplayLabel => displayLabel?.Trim() ?? string.Empty;

    public CharacterAcquiredTraitSpecialReactionDefinition(
        string nextReactionId,
        CharacterAcquiredTraitReactionTrigger nextTrigger,
        CharacterAcquiredTraitReactionOwnerRole nextOwnerRole,
        CharacterAcquiredTraitReactionCondition nextCondition,
        CharacterAcquiredTraitReactionAction nextAction,
        float nextBaseValue,
        int nextDurationDays,
        int nextCooldownDays,
        int nextMaximumTriggersPerDay,
        int nextMaximumLifetimeTriggers,
        string nextDisplayLabel)
    {
        reactionId = nextReactionId;
        trigger = nextTrigger;
        ownerRole = nextOwnerRole;
        condition = nextCondition;
        action = nextAction;
        baseValue = nextBaseValue;
        durationDays = nextDurationDays;
        cooldownDays = nextCooldownDays;
        maximumTriggersPerDay = nextMaximumTriggersPerDay;
        maximumLifetimeTriggers = nextMaximumLifetimeTriggers;
        displayLabel = nextDisplayLabel;
    }

    public IReadOnlyList<string> ValidateDefinition(string moduleId)
    {
        List<string> errors = new();
        if (ReactionId.Length == 0
            || !string.Equals(reactionId, ReactionId, StringComparison.Ordinal))
            errors.Add($"Acquired-trait module '{moduleId}' has a non-canonical reaction ID.");
        if (!Enum.IsDefined(typeof(CharacterAcquiredTraitReactionTrigger), trigger)
            || !Enum.IsDefined(typeof(CharacterAcquiredTraitReactionOwnerRole), ownerRole)
            || !Enum.IsDefined(typeof(CharacterAcquiredTraitReactionCondition), condition)
            || !Enum.IsDefined(typeof(CharacterAcquiredTraitReactionAction), action))
            errors.Add($"Acquired-trait reaction '{ReactionId}' contains an unsupported enum value.");
        if (float.IsNaN(baseValue) || float.IsInfinity(baseValue)
            || Mathf.Approximately(baseValue, 0f))
            errors.Add($"Acquired-trait reaction '{ReactionId}' requires a finite non-zero base value.");
        if (durationDays < 0 || cooldownDays < 0 || maximumTriggersPerDay < 0
            || maximumLifetimeTriggers < 0)
            errors.Add($"Acquired-trait reaction '{ReactionId}' has a negative limit.");
        if (maximumTriggersPerDay == 0 && maximumLifetimeTriggers == 0)
            errors.Add($"Acquired-trait reaction '{ReactionId}' must have a bounded daily or lifetime limit.");
        if (action == CharacterAcquiredTraitReactionAction.GrantExperience
            && (baseValue < 1f || durationDays != 0))
            errors.Add($"Experience reaction '{ReactionId}' requires at least one XP and no duration.");
        if (action == CharacterAcquiredTraitReactionAction.ApplyMoodImpulse
            && durationDays <= 0)
            errors.Add($"Mood reaction '{ReactionId}' requires a positive duration.");
        if (DisplayLabel.Length == 0)
            errors.Add($"Acquired-trait reaction '{ReactionId}' requires a display label.");
        if (trigger == CharacterAcquiredTraitReactionTrigger.ExpeditionOutcome
            && ownerRole != CharacterAcquiredTraitReactionOwnerRole.Participant)
            errors.Add($"Expedition reaction '{ReactionId}' must target a participant owner.");
        if (trigger == CharacterAcquiredTraitReactionTrigger.ApologyCompleted
            && ownerRole != CharacterAcquiredTraitReactionOwnerRole.Recipient)
            errors.Add($"Apology reaction '{ReactionId}' must target the recipient owner.");
        if (trigger is CharacterAcquiredTraitReactionTrigger.WorkCompleted
                or CharacterAcquiredTraitReactionTrigger.CharacterInjured
            && ownerRole != CharacterAcquiredTraitReactionOwnerRole.Subject)
            errors.Add($"Subject reaction '{ReactionId}' has an incompatible owner role.");
        bool conditionMatchesTrigger = condition == CharacterAcquiredTraitReactionCondition.None
            || trigger == CharacterAcquiredTraitReactionTrigger.WorkCompleted
                && condition == CharacterAcquiredTraitReactionCondition.ProductCreated
            || trigger == CharacterAcquiredTraitReactionTrigger.CharacterInjured
                && condition == CharacterAcquiredTraitReactionCondition.WorkAccident
            || trigger == CharacterAcquiredTraitReactionTrigger.ExpeditionOutcome
                && condition == CharacterAcquiredTraitReactionCondition.ExpeditionSucceeded
            || trigger == CharacterAcquiredTraitReactionTrigger.ApologyCompleted
                && condition == CharacterAcquiredTraitReactionCondition.RestitutionProvided;
        if (!conditionMatchesTrigger)
            errors.Add($"Acquired-trait reaction '{ReactionId}' condition is incompatible with its trigger.");
        return errors;
    }
}

public readonly struct CharacterAcquiredTraitReactionExecutedEvent
{
    public CharacterAcquiredTraitReactionExecutedEvent(
        string characterId,
        string instanceId,
        string reactionId,
        CharacterAcquiredTraitReactionAction action,
        float appliedValue,
        int absoluteDay)
    {
        CharacterId = characterId ?? string.Empty;
        InstanceId = instanceId ?? string.Empty;
        ReactionId = reactionId ?? string.Empty;
        Action = action;
        AppliedValue = appliedValue;
        AbsoluteDay = absoluteDay;
    }

    public string CharacterId { get; }
    public string InstanceId { get; }
    public string ReactionId { get; }
    public CharacterAcquiredTraitReactionAction Action { get; }
    public float AppliedValue { get; }
    public int AbsoluteDay { get; }
}

public static class CharacterAcquiredTraitSpecialReactionMath
{
    public static float ResolvePotencyScale(
        CharacterAcquiredTraitInstanceState instance,
        CharacterAcquiredTraitModuleSO module)
    {
        if (instance == null || module == null || instance.formulaVersion <= 0)
            return 1f;
        CharacterAcquiredTraitFormulaCapabilityEnvelope envelope =
            (instance.formulaCapabilities
                ?? new List<CharacterAcquiredTraitFormulaCapabilityEnvelope>())
            .SingleOrDefault(value => value != null
                && string.Equals(value.capabilityId, module.CapabilityId,
                    StringComparison.Ordinal));
        CharacterAcquiredTraitFormulaParameter magnitude = envelope?.parameters?
            .SingleOrDefault(value => value != null
                && string.Equals(value.parameterId,
                    NarrativeFormulaParameterIds.Magnitude,
                    StringComparison.Ordinal));
        if (magnitude == null)
            return 1f;
        decimal resolved = module.RequireFormulaDescriptor()
            .RequireRange(NarrativeFormulaParameterIds.Magnitude)
            .ToDecimal(magnitude.units);
        double resolvedDouble = (double)resolved;
        if (resolved < 1m || double.IsNaN(resolvedDouble)
            || double.IsInfinity(resolvedDouble) || resolvedDouble > float.MaxValue)
            throw new InvalidOperationException(
                $"Acquired-trait reaction potency for '{module.ModuleId}' is invalid.");
        return (float)resolved;
    }

    public static float ResolveActionValue(
        CharacterAcquiredTraitSpecialReactionDefinition reaction,
        float potencyScale)
    {
        if (reaction == null || float.IsNaN(potencyScale)
            || float.IsInfinity(potencyScale) || potencyScale < 1f)
            throw new ArgumentException("A valid special reaction and potency are required.");
        double scaled = reaction.BaseValue * (double)potencyScale;
        if (double.IsNaN(scaled) || double.IsInfinity(scaled)
            || scaled < float.MinValue || scaled > float.MaxValue)
            throw new OverflowException("Acquired-trait reaction value overflowed.");
        return reaction.Action == CharacterAcquiredTraitReactionAction.GrantExperience
            ? Mathf.Max(1f, Mathf.Round((float)scaled))
            : Mathf.Round((float)scaled * 10f) / 10f;
    }

    public static string FormatMechanicalDescription(
        CharacterAcquiredTraitSpecialReactionDefinition reaction,
        float resolvedValue)
    {
        if (reaction == null)
            throw new ArgumentNullException(nameof(reaction));
        string triggerText = reaction.Trigger switch
        {
            CharacterAcquiredTraitReactionTrigger.WorkCompleted =>
                reaction.Condition == CharacterAcquiredTraitReactionCondition.ProductCreated
                    ? "완성품이 있는 작업을 마치면"
                    : "작업을 마치면",
            CharacterAcquiredTraitReactionTrigger.CharacterInjured =>
                reaction.Condition == CharacterAcquiredTraitReactionCondition.WorkAccident
                    ? "작업 사고로 다치면"
                    : "부상을 입으면",
            CharacterAcquiredTraitReactionTrigger.ExpeditionOutcome =>
                reaction.Condition == CharacterAcquiredTraitReactionCondition.ExpeditionSucceeded
                    ? "원정에 성공하면"
                    : "원정이 끝나면",
            CharacterAcquiredTraitReactionTrigger.ApologyCompleted =>
                reaction.Condition == CharacterAcquiredTraitReactionCondition.RestitutionProvided
                    ? "배상을 포함한 사과를 받으면"
                    : "사과를 받으면",
            _ => throw new ArgumentOutOfRangeException()
        };
        string actionText = reaction.Action switch
        {
            CharacterAcquiredTraitReactionAction.GrantExperience =>
                $"경험치를 {Mathf.RoundToInt(resolvedValue)} 얻습니다",
            CharacterAcquiredTraitReactionAction.ApplyMoodImpulse =>
                $"기분이 {resolvedValue:0.#}만큼 {reaction.DurationDays}일 동안 좋아집니다",
            _ => throw new ArgumentOutOfRangeException()
        };
        List<string> limits = new();
        if (reaction.MaximumTriggersPerDay > 0)
            limits.Add($"하루 최대 {reaction.MaximumTriggersPerDay}회");
        if (reaction.CooldownDays > 0)
            limits.Add($"재사용 {reaction.CooldownDays}일");
        if (reaction.MaximumLifetimeTriggers > 0)
            limits.Add($"평생 최대 {reaction.MaximumLifetimeTriggers}회");
        return triggerText + " " + actionText
            + (limits.Count == 0 ? string.Empty : " (" + string.Join(", ", limits) + ")")
            + ".";
    }

    public static bool CanTrigger(
        CharacterAcquiredTraitReactionState state,
        CharacterAcquiredTraitSpecialReactionDefinition reaction,
        int absoluteDay)
    {
        if (reaction == null || absoluteDay < 0)
            return false;
        if (state == null)
            return true;
        if (reaction.MaximumLifetimeTriggers > 0
            && state.totalTriggerCount >= reaction.MaximumLifetimeTriggers)
            return false;
        if (state.lastTriggerAbsoluteDay == absoluteDay
            && reaction.MaximumTriggersPerDay > 0
            && state.triggersOnLastDay >= reaction.MaximumTriggersPerDay)
            return false;
        return state.lastTriggerAbsoluteDay < 0
            || reaction.CooldownDays <= 0
            || absoluteDay - state.lastTriggerAbsoluteDay >= reaction.CooldownDays;
    }
}

/// <summary>
/// Executes authored acquired-trait reactions with one handler per action kind.
/// Trait IDs never appear in the dispatch logic, preventing per-trait branching.
/// </summary>
public sealed class CharacterAcquiredTraitSpecialReactionRuntime : IStartable, IDisposable
{
    private readonly IGameContentDefinitionSource content;
    private readonly ICharacterWorldQuery world;
    private readonly IGameEventBus events;
    private readonly CharacterMoodPolicyService moods;
    private readonly IDomainFactPublisher facts;
    private readonly CharacterTraitReactionPublicationRuntime publications;
    private bool started;
    public string LastFailure { get; private set; } = string.Empty;
    private readonly List<IDisposable> subscriptions = new();
    private CharacterAcquiredTraitSettingsSO settings;
    private CharacterAcquiredTraitModuleSO[] modules = Array.Empty<CharacterAcquiredTraitModuleSO>();
    private Dictionary<string, CharacterAcquiredTraitModuleSO> modulesById =
        new(StringComparer.Ordinal);

    public CharacterAcquiredTraitSpecialReactionRuntime(
        IGameContentDefinitionSource content,
        ICharacterWorldQuery world,
        IGameEventBus events,
        CharacterMoodPolicyService moods,
        IDomainFactPublisher facts,
        CharacterTraitReactionPublicationRuntime publications)
    {
        this.content = content ?? throw new ArgumentNullException(nameof(content));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.moods = moods ?? throw new ArgumentNullException(nameof(moods));
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.publications = publications ?? throw new ArgumentNullException(nameof(publications));
    }

    public void Start()
    {
        if (started) return;
        settings = content.RequireSingle<CharacterAcquiredTraitSettingsSO>()
            ?? throw new InvalidOperationException("Acquired-trait settings are missing.");
        modules = (content.GetAll<CharacterAcquiredTraitModuleSO>()
                ?? Array.Empty<CharacterAcquiredTraitModuleSO>())
            .Where(value => value != null)
            .OrderBy(value => value.ModuleId, StringComparer.Ordinal)
            .ToArray();
        modulesById = modules.ToDictionary(value => value.ModuleId,
            StringComparer.Ordinal);
        string[] errors = modules.SelectMany(value => value.ValidateDefinition()).ToArray();
        if (errors.Length > 0)
            throw new InvalidOperationException(
                "Acquired-trait reaction content is invalid: " + string.Join(" | ", errors));

        subscriptions.Add(events.Subscribe<WorkCompletedIdentityEvent>(OnWorkCompleted));
        subscriptions.Add(events.Subscribe<CharacterInjuredIdentityEvent>(OnCharacterInjured));
        subscriptions.Add(events.Subscribe<ExpeditionOutcomeEvent>(OnExpeditionOutcome));
        subscriptions.Add(events.Subscribe<ApologyEvent>(OnApology));
        started = true;
    }

    public void Dispose()
    {
        foreach (IDisposable subscription in subscriptions)
            subscription?.Dispose();
        subscriptions.Clear();
        started = false;
    }

    private void OnWorkCompleted(WorkCompletedIdentityEvent value) => Process(
        value.Character,
        CharacterAcquiredTraitReactionTrigger.WorkCompleted,
        value.AbsoluteDay,
        reaction => reaction.Condition != CharacterAcquiredTraitReactionCondition.ProductCreated
            || value.ProductId.Length > 0);

    private void OnCharacterInjured(CharacterInjuredIdentityEvent value) => Process(
        value.Character,
        CharacterAcquiredTraitReactionTrigger.CharacterInjured,
        value.AbsoluteDay,
        reaction => reaction.Condition != CharacterAcquiredTraitReactionCondition.WorkAccident
            || value.HasWorkAccidentEvidence);

    private void OnExpeditionOutcome(ExpeditionOutcomeEvent value)
    {
        foreach (CharacterId participant in value.Participants)
        {
            Process(participant,
                CharacterAcquiredTraitReactionTrigger.ExpeditionOutcome,
                value.AbsoluteDay,
                reaction => reaction.Condition
                        != CharacterAcquiredTraitReactionCondition.ExpeditionSucceeded
                    || string.Equals(value.OutcomeId, "success", StringComparison.Ordinal));
        }
    }

    private void OnApology(ApologyEvent value) => Process(
        value.Recipient,
        CharacterAcquiredTraitReactionTrigger.ApologyCompleted,
        value.AbsoluteDay,
        reaction => reaction.Condition
                != CharacterAcquiredTraitReactionCondition.RestitutionProvided
            || value.RestitutionProvided);

    private void Process(
        CharacterId ownerId,
        CharacterAcquiredTraitReactionTrigger trigger,
        int absoluteDay,
        Func<CharacterAcquiredTraitSpecialReactionDefinition, bool> condition)
    {
        if (!publications.RecoverPending(out string recoveryFailure))
        { LastFailure = recoveryFailure; return; }
        LastFailure = string.Empty;
        CharacterActor actor = world.Characters.FirstOrDefault(value => value != null
            && string.Equals(value.Identity?.PersistentId, ownerId.Value,
                StringComparison.Ordinal));
        CharacterProgression progression = actor?.Progression;
        if (progression == null)
            return;

        CharacterAcquiredTraitAggregateState snapshot =
            progression.CaptureAcquiredTraitState();
        foreach (CharacterAcquiredTraitInstanceState instance in snapshot.instances
                     .Where(value => value != null && value.IsActive)
                     .OrderBy(value => value.instanceId, StringComparer.Ordinal))
        {
            foreach (CharacterAcquiredTraitModuleSO module in (instance.moduleIds
                         ?? new List<string>())
                     .Where(modulesById.ContainsKey)
                     .Select(value => modulesById[value])
                     .OrderBy(value => value.ModuleId, StringComparer.Ordinal))
            {
                foreach (CharacterAcquiredTraitSpecialReactionDefinition reaction
                         in module.SpecialReactions
                             .Where(value => value != null && value.Trigger == trigger)
                             .OrderBy(value => value.ReactionId, StringComparer.Ordinal))
                {
                    if (!condition(reaction))
                        continue;
                    TryExecute(progression, actor, instance.instanceId, module,
                        reaction, absoluteDay);
                }
            }
        }
    }

    private bool TryExecute(
        CharacterProgression progression,
        CharacterActor actor,
        string instanceId,
        CharacterAcquiredTraitModuleSO module,
        CharacterAcquiredTraitSpecialReactionDefinition reaction,
        int absoluteDay)
    {
        CharacterAcquiredTraitAggregateState current =
            progression.CaptureAcquiredTraitState();
        CharacterAcquiredTraitInstanceState instance = current.instances.SingleOrDefault(
            value => value != null && value.IsActive
                && string.Equals(value.instanceId, instanceId, StringComparison.Ordinal));
        if (instance == null)
            return false;
        instance.reactionStates ??= new List<CharacterAcquiredTraitReactionState>();
        CharacterAcquiredTraitReactionState state = instance.reactionStates.SingleOrDefault(
            value => value != null && string.Equals(value.reactionId,
                reaction.ReactionId, StringComparison.Ordinal));
        if (state == null)
        {
            state = new CharacterAcquiredTraitReactionState
            {
                reactionId = reaction.ReactionId
            };
            instance.reactionStates.Add(state);
        }
        if (!CharacterAcquiredTraitSpecialReactionMath.CanTrigger(
                state,
                reaction,
                absoluteDay))
            return false;
        if (reaction.Action == CharacterAcquiredTraitReactionAction.GrantExperience
            && progression.Level >= CharacterProgression.MaxLevel)
            return false;

        float scale = CharacterAcquiredTraitSpecialReactionMath.ResolvePotencyScale(
            instance, module);
        float actionValue = CharacterAcquiredTraitSpecialReactionMath.ResolveActionValue(
            reaction, scale);
        int beforeLevel = progression.Level;
        int beforeExperience = progression.CurrentExperience;
        bool experience = reaction.Action == CharacterAcquiredTraitReactionAction.GrantExperience;
        if (experience && (actionValue >= int.MaxValue || actionValue > int.MaxValue - beforeExperience))
        { LastFailure = "trait-reaction-experience-overflow"; return false; }
        CharacterProgressionTransition transition = CharacterProgressionRules.AddExperience(
            beforeLevel, beforeExperience, experience ? (int)actionValue : 0);
        int beforeRevision = current.revision;
        int committedRevision = checked(beforeRevision + 1);
        string characterId = actor.BuildingCharacterId.Value;
        var previous = progression.CaptureAcquiredTraitState();
        int beforeTriggerDay = state.lastTriggerAbsoluteDay;
        int beforeDailyCount = state.triggersOnLastDay;
        int beforeTotalCount = state.totalTriggerCount;
        state.triggersOnLastDay = state.lastTriggerAbsoluteDay == absoluteDay
            ? checked(state.triggersOnLastDay + 1) : 1;
        state.lastTriggerAbsoluteDay = absoluteDay;
        state.totalTriggerCount = checked(state.totalTriggerCount + 1);
        current.formatVersion = CharacterAcquiredTraitAggregateState.CurrentFormatVersion;
        current.revision = committedRevision;
        if (actor.Stats == null)
        { LastFailure = "trait-reaction-stats-unavailable"; return false; }
        if (!publications.TryBegin(characterId, out string failure))
        { LastFailure = failure; return false; }
        DomainFactLease lease = default;
        CharacterMoodDeliveryTransactionSnapshot moodBefore = null;
        bool moodTouched = false, ownerApplied = false, ownerRestored = false, retained = false, moodApplied = false;
        float appliedValue = actionValue;
        string moodEvent = "acquired-trait:" + reaction.ReactionId;
        bool Rollback()
        {
            // Projection reads made by the stats owner need the unpublished state.
            // No callbacks are emitted during rollback; restore the guard on failure.
            bool restored = false;
            progression.SetTraitReactionRecoveryBlocked(false);
            try
            {
                if (ownerApplied && !ownerRestored)
                {
                    if (!progression.TryRollbackAcquiredTraitReaction(previous, committedRevision,
                            beforeLevel, beforeExperience, transition.Level, transition.CurrentExperience)) return false;
                    ownerRestored = true;
                }
                if (moodTouched) actor.Stats.RestoreMoodDeliveryTransactionState(moodBefore, publish: false);
                restored = true;
                return true;
            }
            catch (Exception error) when (Recoverable(error)) { return false; }
            finally { if (!restored) progression.SetTraitReactionRecoveryBlocked(true); }
        }
        try
        {
            float previousMood = actor.Stats.GetConditionValue(CharacterCondition.MOOD);
            if (!experience)
            {
                moodBefore = actor.Stats.CaptureMoodDeliveryTransactionState();
                moodTouched = true;
                float resolved = moods.ApplyDaysWithoutNotification(actor, moodEvent, actionValue,
                    reaction.DurationDays, reaction.DisplayLabel, out moodApplied);
                appliedValue = moodApplied ? resolved : 0f;
            }
            else
            {
                int accepted = transition.CurrentExperience - beforeExperience;
                for (int level = beforeLevel; level < transition.Level; level++)
                    accepted = checked(accepted + CharacterProgressionRules.GetExperienceRequired(level));
                appliedValue = accepted;
            }
            var fact = new CharacterTraitReactionFact(new CharacterTraitReactionFactData
            {
                characterId = characterId, characterDisplayName = actor.Identity.DisplayName,
                traitInstanceId = instanceId, reactionId = reaction.ReactionId, action = reaction.Action,
                beforeRevision = beforeRevision, afterRevision = committedRevision, absoluteDay = absoluteDay,
                requestedValue = actionValue, appliedValue = appliedValue, durationDays = reaction.DurationDays,
                beforeLevel = beforeLevel, beforeExperience = beforeExperience,
                afterLevel = transition.Level, afterExperience = transition.CurrentExperience,
                moodBefore = previousMood, moodAfter = actor.Stats.GetConditionValue(CharacterCondition.MOOD),
                beforeTriggerDay = beforeTriggerDay, beforeDailyCount = beforeDailyCount, beforeTotalCount = beforeTotalCount,
                afterDailyCount = state.triggersOnLastDay, afterTotalCount = state.totalTriggerCount
            });
            if (!facts.TryReserve(fact, out lease, out failure))
            { LastFailure = failure; return false; }
            if (!progression.TryStageAcquiredTraitReaction(current, beforeRevision, settings, modules,
                    experience ? (int)actionValue : 0, out transition, out failure))
            { LastFailure = failure; return false; }
            ownerApplied = true;
            DomainFactDelivery delivered = publications.PublishApplied(fact, lease);
            retained = delivered.MayBeCommitted;
            if (!retained) { LastFailure = delivered.Detail; return false; }
            if (!delivered.Completed) LastFailure = delivered.Detail;
        }
        catch (Exception error) when (Recoverable(error))
        { LastFailure = "trait-reaction-command:" + error.Message; return false; }
        finally
        {
            try
            {
                if (!retained && !Rollback())
                {
                    progression.SetTraitReactionRecoveryBlocked(true);
                    publications.RetainRollback(characterId, Rollback);
                    LastFailure = "trait-reaction-effect-rollback-pending:" + LastFailure;
                }
            }
            finally
            {
                try { if (lease.IsValid) facts.Cancel(lease); }
                finally { publications.End(characterId); }
            }
        }
        // These are notifications of an already applied reaction. They are never
        // rerun by durable fact recovery and cannot turn it into a fresh action.
        Notify(() => progression.PublishAcquiredTraitReaction(transition, experience));
        if (moodApplied)
            Notify(() => actor.Stats.PublishCommittedMoodFactor("identity:" + moodEvent, appliedValue, absoluteDay));
        Notify(() => events.Publish(new CharacterAcquiredTraitReactionExecutedEvent(
            characterId, instanceId, reaction.ReactionId, reaction.Action, appliedValue, absoluteDay)));
        return true;
    }

    private void Notify(Action callback)
    {
        try { callback(); }
        catch (Exception error) when (Recoverable(error))
        {
            LastFailure = (LastFailure.Length == 0 ? string.Empty : LastFailure + " | ")
                + "trait-reaction-notification:" + error.Message;
            Debug.LogException(error);
        }
    }

    private static bool Recoverable(Exception error) => error is not OutOfMemoryException
        && error is not StackOverflowException && error is not AccessViolationException;
}
