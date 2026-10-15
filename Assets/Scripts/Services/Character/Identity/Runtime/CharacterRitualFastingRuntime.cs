using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json.Linq;
using UnityEngine;
using VContainer.Unity;

public enum CharacterRitualFastPhase
{
    Inactive,
    Fasting,
    AwaitingPostFastMeal
}

public readonly struct CharacterRitualFastStatus
{
    public CharacterRitualFastStatus(
        bool available,
        CharacterRitualFastPhase phase,
        int startedDay,
        bool canComplete)
    {
        Available = available;
        Phase = phase;
        StartedDay = startedDay;
        CanComplete = canComplete;
    }

    public bool Available { get; }
    public CharacterRitualFastPhase Phase { get; }
    public int StartedDay { get; }
    public bool CanComplete { get; }
}

public interface ICharacterRitualFastingQuery
{
    CharacterRitualFastStatus GetStatus(CharacterActor actor);
}

public interface ICharacterRitualFastingCommand
{
    bool TryBegin(CharacterActor actor, out string reason);
    bool TryComplete(CharacterActor actor, out string reason);
    bool TryBreak(CharacterActor actor, out string reason);
    bool RecordMealConsumed(CharacterActor actor, bool directPlayerOrder,
        CharacterMealConsumedFact meal, string mealResultKey, out string failureReason);
}

[Serializable]
public sealed class CharacterRitualFastRuntimeState
{
    public int schemaVersion = 3;
    public bool active;
    public bool awaitingPostFastMeal;
    public int startedDay = -1;
    public long transitionSequence;
    public string cycleStartResultKey = string.Empty;
    public string lastTransitionResultKey = string.Empty;
    public string lastMealOperationId = string.Empty;
    public string lastMealResultKey = string.Empty;
    public CharacterRitualFastPendingFact pending = new();
}

/// <summary>
/// Saved authority for the ritual-fast identity rule. Mood factors are results,
/// never the source used to decide whether conditional gameplay effects apply.
/// </summary>
public sealed class CharacterRitualFastingRuntime :
    ICharacterRitualFastingQuery,
    ICharacterRitualFastingCommand,
    ICharacterIdentityDeathStateRetentionPolicy,
    ITickable
{
    public const string NeedId = "need:ritual-fast";
    public const string StateRuleId = "behavior:ritual-fast";
    private readonly CharacterIdentityStateStore states;
    private readonly CharacterMoodPolicyService moods;
    private readonly IGameCalendar calendar;
    private readonly ICharacterWorldQuery world;
    private readonly IFestivalDefinitionCatalog festivals;
    private readonly IDomainFactPublisher facts;
    private readonly IDomainFactRecoveryPublisher recovery;
    private int lastAutonomyDay = -1;
    private long lastRecoveryRevision = -1;
    private bool hasPendingRecovery = true;
#if UNITY_EDITOR
    internal Func<CharacterRitualFastTransition, bool> DebugRejectTransitionCommit { get; set; }
#endif

    public CharacterRitualFastingRuntime(
        CharacterIdentityStateStore states,
        CharacterMoodPolicyService moods,
        IGameCalendar calendar,
        ICharacterWorldQuery world,
        IFestivalDefinitionCatalog festivals,
        IDomainFactPublisher facts,
        IDomainFactRecoveryPublisher recovery)
    {
        this.states = states ?? throw new ArgumentNullException(nameof(states));
        this.moods = moods ?? throw new ArgumentNullException(nameof(moods));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.festivals = festivals ?? throw new ArgumentNullException(nameof(festivals));
        this.facts = facts ?? throw new ArgumentNullException(nameof(facts));
        this.recovery = recovery ?? throw new ArgumentNullException(nameof(recovery));
    }

    /// <summary>
    /// Ritual fasters autonomously begin on the day before an authored calendar
    /// festival. The saved fast state remains the authority, so load/re-tick on
    /// the same day is idempotent and cannot duplicate an outcome.
    /// </summary>
    [GameplayInternalOnly(
        "The registered ITickable entry point advances ritual-fast scheduling from the authoritative clock.",
        "ITickable|DungeonCharacterRegistration")]
    public void Tick()
    {
        // Delivery recovery is independent of the daily autonomy decision. It
        // reuses saved evidence and never re-applies mood or fasting effects.
        if (hasPendingRecovery || lastRecoveryRevision != states.Revision)
        {
            hasPendingRecovery = false;
            foreach (var entry in states.CaptureForRule(StateRuleId))
            {
                ValidateStatePayload(entry.State.statePayload, entry.CharacterId, entry.State.traitDefinitionId);
                var saved = JsonUtility.FromJson<CharacterRitualFastRuntimeState>(entry.State.statePayload);
                if (!saved.pending.active) continue;
                var actor = world.Characters.FirstOrDefault(value => value != null
                    && value.Identity?.PersistentId == entry.CharacterId);
                if (!TryDispatchPending(actor, entry.CharacterId, entry.State.traitDefinitionId, saved,
                    default, out _, out _)) hasPendingRecovery = true;
            }
            lastRecoveryRevision = states.Revision;
        }
        int today = Math.Max(1, calendar.Day);
        if (today == lastAutonomyDay)
            return;

        CalendarDateTime tomorrow = GameCalendarRules.Project(today + 1, 0);
        bool festivalTomorrow = festivals.All.Any(value => value != null
            && value.season == tomorrow.Season
            && value.dayOfSeason == tomorrow.DayOfSeason);
        if (!festivalTomorrow)
        {
            lastAutonomyDay = today;
            return;
        }

        foreach (CharacterActor actor in world.Characters
                     .Where(value => value != null && !value.IsDead)
                     .OrderBy(
                         value => value.Identity?.PersistentId ?? string.Empty,
                         StringComparer.Ordinal))
        {
            CharacterRitualFastStatus status = GetStatus(actor);
            if (HasPositiveBehaviorRule(actor, "ritual:fast") && status.Available
                && status.Phase == CharacterRitualFastPhase.Inactive
                && !TryBegin(actor, CharacterRitualFastCause.FestivalEve, out string failureReason))
            {
                Debug.LogError("ritual-fast-autonomy-pending:" + failureReason);
                return;
            }
        }
        lastAutonomyDay = today;
    }

    public CharacterRitualFastStatus GetStatus(CharacterActor actor)
    {
        if (!TryResolve(actor, out CharacterTraitSO trait, out PersistentNeedRule rule))
            return default;
        CharacterRitualFastRuntimeState state = Read(actor, trait, rule);
        CharacterRitualFastPhase phase = state.active
            ? CharacterRitualFastPhase.Fasting
            : state.awaitingPostFastMeal
                ? CharacterRitualFastPhase.AwaitingPostFastMeal
                : CharacterRitualFastPhase.Inactive;
        return new CharacterRitualFastStatus(
            true,
            phase,
            state.startedDay,
            state.active && !state.pending.active && calendar.Day > state.startedDay);
    }

    [GameplayEntryPoint(
        "StaffManagementSurfacePanel ritual-fast start button and autonomous fasting tick")]
    public bool TryBegin(CharacterActor actor, out string reason)
        => TryBegin(actor, CharacterRitualFastCause.ManualOrder, out reason);

    private bool TryBegin(CharacterActor actor, CharacterRitualFastCause cause, out string reason)
    {
        reason = string.Empty;
        if (!TryResolve(actor, out CharacterTraitSO trait, out PersistentNeedRule rule))
        {
            reason = "의식 단식 특성이 없습니다.";
            return false;
        }
        CharacterRitualFastRuntimeState state = Read(actor, trait, rule);
        bool retry = state.pending.active && state.pending.fact.transition == CharacterRitualFastTransition.Started
            && state.pending.fact.cause == cause;
        if (!TryResumePending(actor, trait, rule, state, out reason)) return false;
        if (retry) return true;
        state = Read(actor, trait, rule);
        if (state.active)
        {
            reason = "이미 의식 단식 중입니다.";
            return false;
        }
        if (state.awaitingPostFastMeal)
        {
            reason = "단식 종료 후 첫 식사를 마쳐야 다시 시작할 수 있습니다.";
            return false;
        }
        state.active = true;
        state.awaitingPostFastMeal = false;
        state.startedDay = Math.Max(0, calendar.Day);
        return TryCommitTransition(actor, trait, rule, state,
            CharacterRitualFastTransition.Started, cause, "", "", out reason);
    }

    [GameplayEntryPoint(
        "StaffManagementSurfacePanel ritual-fast complete button and festival completion")]
    public bool TryComplete(CharacterActor actor, out string reason)
    {
        reason = string.Empty;
        if (!TryResolve(actor, out CharacterTraitSO trait, out PersistentNeedRule rule))
        {
            reason = "의식 단식 특성이 없습니다.";
            return false;
        }
        CharacterRitualFastRuntimeState state = Read(actor, trait, rule);
        bool retry = state.pending.active && state.pending.fact.transition == CharacterRitualFastTransition.Completed;
        if (!TryResumePending(actor, trait, rule, state, out reason)) return false;
        if (retry) return true;
        state = Read(actor, trait, rule);
        if (!state.active)
        {
            reason = "진행 중인 의식 단식이 없습니다.";
            return false;
        }
        if (calendar.Day <= state.startedDay)
        {
            reason = "의식 단식은 다음 날부터 완수할 수 있습니다.";
            return false;
        }
        state.active = false;
        state.awaitingPostFastMeal = true;
        return TryCommitTransition(actor, trait, rule, state,
            CharacterRitualFastTransition.Completed, CharacterRitualFastCause.ManualOrder,
            "", "", out reason);
    }

    [GameplayEntryPoint(
        "StaffManagementSurfacePanel ritual-fast break button and direct meal override")]
    public bool TryBreak(CharacterActor actor, out string reason)
    {
        reason = string.Empty;
        if (!TryResolve(actor, out CharacterTraitSO trait, out PersistentNeedRule rule))
        {
            reason = "의식 단식 특성이 없습니다.";
            return false;
        }
        CharacterRitualFastRuntimeState state = Read(actor, trait, rule);
        bool retry = state.pending.active && state.pending.fact.transition == CharacterRitualFastTransition.Interrupted
            && state.pending.fact.cause == CharacterRitualFastCause.ManualOrder;
        if (!TryResumePending(actor, trait, rule, state, out reason)) return false;
        if (retry) return true;
        state = Read(actor, trait, rule);
        if (!state.active)
        {
            reason = "중단할 의식 단식이 없습니다.";
            return false;
        }
        state.active = false;
        state.awaitingPostFastMeal = false;
        state.startedDay = -1;
        return TryCommitTransition(actor, trait, rule, state,
            CharacterRitualFastTransition.Interrupted, CharacterRitualFastCause.ManualOrder,
            "", "", out reason);
    }

    [GameplayInternalOnly(
        "The physical meal-consumption adapter records fasting consequences only after an actual meal succeeds.",
        "CharacterConsumablesApplicationAdapters")]
    public bool RecordMealConsumed(CharacterActor actor, bool directPlayerOrder,
        CharacterMealConsumedFact meal, string mealResultKey, out string failureReason)
    {
        failureReason = string.Empty;
        if (meal == null || meal.CharacterId.Value != RequireCharacterId(actor))
        { failureReason = "ritual-fast-meal-recipient-mismatch"; return false; }
        string mealOperationId = meal.OperationId.Value;
        if (!TryResolve(actor, out CharacterTraitSO trait, out PersistentNeedRule rule))
            return true;
        CharacterRitualFastRuntimeState state = Read(actor, trait, rule);
        if (!TryResumePending(actor, trait, rule, state, out failureReason)) return false;
        state = Read(actor, trait, rule);
        if (state.transitionSequence == 0 && !state.active && !state.awaitingPostFastMeal)
            return true;
        if (state.lastMealOperationId == mealOperationId)
        {
            if (state.lastMealResultKey == mealResultKey)
                return true;
            failureReason = "ritual-fast-meal-identity-conflict";
            return false;
        }
        if (!TryObserveMeal(actor, meal, mealResultKey, out failureReason)) return false;
        if (state.active)
        {
            // Autonomous meals are rejected before physical consumption. Reaching
            // this branch therefore means an explicit order overrode the trait.
            if (directPlayerOrder)
            {
                state.active = false;
                state.awaitingPostFastMeal = false;
                state.startedDay = -1;
                return TryCommitTransition(actor, trait, rule, state,
                    CharacterRitualFastTransition.Interrupted, CharacterRitualFastCause.DirectMeal,
                    mealOperationId, mealResultKey, out failureReason, meal);
            }
            failureReason = "ritual-fast-autonomous-meal-conflict";
            return false;
        }
        state.lastMealOperationId = mealOperationId;
        state.lastMealResultKey = mealResultKey;
        state.awaitingPostFastMeal = false;
        state.startedDay = -1;
        Write(actor, trait, rule, state);
        return true;
    }

    private bool TryObserveMeal(CharacterActor actor, CharacterMealConsumedFact meal, string publication, out string failure)
    {
        DomainFactLease lease = default;
        try
        {
            var fact = new CharacterRitualFastMealObservedFact(RequireCharacterId(actor), meal, publication);
            if (!facts.TryReserve(fact, out lease, out failure)) return false;
            string key = lease.PublicationKey;
            DomainFactDelivery delivered = facts.Publish(lease, fact);
            if (delivered.Completed && delivered.OperationKey == key) { failure = string.Empty; return true; }
            failure = "ritual-fast-meal-observation-unconfirmed:" + delivered.Detail;
            return false;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failure = "ritual-fast-meal-observation-failed:" + exception.Message; return false; }
        finally { if (lease.IsValid) facts.Cancel(lease); }
    }

    private bool TryCommitTransition(CharacterActor actor, CharacterTraitSO trait,
        PersistentNeedRule rule, CharacterRitualFastRuntimeState after, CharacterRitualFastTransition kind,
        CharacterRitualFastCause cause, string mealOperation, string mealResult, out string failureReason,
        CharacterMealConsumedFact meal = null)
    {
        failureReason = string.Empty;
        CharacterRitualFastRuntimeState before = Read(actor, trait, rule);
        if (before.pending.active || before.transitionSequence == long.MaxValue)
        { failureReason = "ritual-fast-pending-or-sequence-exhausted"; return false; }
        var identityBefore = states.Capture();
        var moodBefore = actor.Stats.CaptureMoodDeliveryTransactionState();
        DomainFactLease lease = default;
        bool publicationStarted = false;
        try
        {
            float moodValueBefore = actor.Stats.GetConditionValue(CharacterCondition.MOOD);
            string moodEvent = kind == CharacterRitualFastTransition.Completed ? rule.satisfiedEventId
                : kind == CharacterRitualFastTransition.Interrupted ? rule.deprivedEventId : string.Empty;
            float resolvedMood = string.IsNullOrEmpty(moodEvent) ? 0f
                : moods.ApplyDaysBeforeOutcomeCommit(actor, moodEvent, 0f, rule.moodDurationDays,
                    kind == CharacterRitualFastTransition.Completed ? "의식 단식 완수" : "의식 단식 파기");
            after.transitionSequence = checked(before.transitionSequence + 1);
            var data = new CharacterRitualFastFactData
            {
                characterId = RequireCharacterId(actor), characterName = actor.Identity.DisplayName,
                traitId = trait.DefinitionId.Value, sequence = after.transitionSequence, transition = kind,
                cause = cause, before = Phase(before), after = Phase(after), day = calendar.Day,
                hour = calendar.AbsoluteHour, startBefore = before.startedDay, startAfter = after.startedDay,
                moodBefore = moodValueBefore, moodAfter = actor.Stats.GetConditionValue(CharacterCondition.MOOD),
                resolvedMood = resolvedMood, moodEvent = moodEvent, cycleStartPublicationKey = before.cycleStartResultKey,
                mealOperation = mealOperation, mealPublicationKey = mealResult,
                mealEvidence = meal == null ? new() : CharacterMealConsumedFactData.Capture(meal)
            };
            var fact = new CharacterRitualFastTransitionFact(data);
            if (!facts.TryReserve(fact, out lease, out failureReason)) { Rollback(); return false; }
#if UNITY_EDITOR
            if (DebugRejectTransitionCommit?.Invoke(kind) == true)
            {
                facts.Cancel(lease);
                Rollback();
                failureReason = "ritual-fast-debug-commit-rejected";
                return false;
            }
#endif
            after.lastTransitionResultKey = lease.PublicationKey;
            if (kind == CharacterRitualFastTransition.Started)
                after.cycleStartResultKey = lease.PublicationKey;
            if (!string.IsNullOrEmpty(mealOperation))
            { after.lastMealOperationId = mealOperation; after.lastMealResultKey = mealResult; }
            after.pending = new CharacterRitualFastPendingFact
            { active = true, fact = data, publicationKey = lease.PublicationKey };
            hasPendingRecovery = true;
            Write(actor, trait, rule, after);
            publicationStarted = true;
            bool delivered = TryDispatchPending(actor, data.characterId, data.traitId, after, lease, out bool rejected, out failureReason);
            if (rejected) Rollback();
            return delivered;
        }
        catch (Exception exception) when (Recoverable(exception))
        {
            // Once dispatch begins, an exception cannot prove no record exists.
            // The saved fact/identity stays pending; effects must not be repeated.
            if (!publicationStarted) Rollback();
            failureReason = "ritual-fast-transition-unconfirmed:" + exception.Message;
            return false;
        }
        finally { if (lease.IsValid) facts.Cancel(lease); }

        void Rollback()
        {
            states.RestoreTrustedTransactionSnapshot(identityBefore);
            actor.Stats.RestoreMoodDeliveryTransactionState(moodBefore, publish: false);
        }
    }

    private bool TryResumePending(CharacterActor actor, CharacterTraitSO trait, PersistentNeedRule rule,
        CharacterRitualFastRuntimeState state, out string failure) =>
        !state.pending.active ? Succeeded(out failure)
            : TryDispatchPending(actor, RequireCharacterId(actor), trait.DefinitionId.Value, state, default, out _, out failure);

    private bool TryDispatchPending(CharacterActor actor, string characterId, string traitId,
        CharacterRitualFastRuntimeState state, DomainFactLease reservation, out bool rejected, out string failure)
    {
        rejected = false;
        try
        {
            var pending = state.pending;
            var fact = new CharacterRitualFastTransitionFact(pending.fact);
            DomainFactDelivery delivery;
            if (reservation.IsValid) delivery = facts.Publish(reservation, fact);
            else
            {
                var results = new DomainFactDelivery[1];
                recovery.ReplayBatch(new[] { DomainFactReplay.Create(pending.publicationKey, fact) }, results, out _);
                delivery = results[0];
            }
            rejected = delivery.Phase == DomainFactDeliveryPhase.Rejected;
            if (rejected || delivery.Phase == DomainFactDeliveryPhase.InDoubt)
            { failure = "ritual-fast-observation-unconfirmed:" + delivery.Detail; return false; }
            if (delivery.OperationKey != pending.publicationKey || string.IsNullOrEmpty(delivery.PublicationId)
                || !Hash(delivery.Fingerprint)
                || pending.publicationId.Length > 0 && pending.publicationId != delivery.PublicationId
                || pending.fingerprint.Length > 0 && pending.fingerprint != delivery.Fingerprint)
            { failure = "ritual-fast-observation-identity-conflict"; return false; }
            pending.publicationId = delivery.PublicationId;
            pending.fingerprint = delivery.Fingerprint;
            if (!delivery.Completed)
            {
                WriteState(characterId, traitId, state);
                failure = "ritual-fast-observation-pending:" + delivery.Detail;
                return false;
            }
            // Close the saved owner before notifying callbacks. Re-entry or a
            // save inside a callback cannot publish this notification twice.
            state.pending = new CharacterRitualFastPendingFact();
            if (pending.retainedForDeath) states.RemoveRule(characterId, traitId, StateRuleId);
            else WriteState(characterId, traitId, state);
            if (!pending.retainedForDeath && actor != null && !actor.IsDead && !Mathf.Approximately(pending.fact.resolvedMood, 0f))
                NotifyMood(actor, fact, pending.publicationKey, state.cycleStartResultKey);
            failure = string.Empty;
            return true;
        }
        catch (Exception exception) when (Recoverable(exception))
        { failure = "ritual-fast-observation-unconfirmed:" + exception.Message; return false; }
    }

    private void NotifyMood(CharacterActor actor, CharacterRitualFastTransitionFact transition,
        string publicationKey, string cycleStartPublicationKey)
    {
        DomainFactLease lease = default;
        try
        {
            var fact = new CharacterRitualFastMoodCommittedFact(actor, transition, publicationKey, cycleStartPublicationKey);
            if (!facts.TryReserve(fact, out lease, out string failure))
                throw new InvalidOperationException(failure);
            DomainFactDelivery result = facts.Publish(lease, fact);
            if (!result.Completed) throw new InvalidOperationException(result.Detail);
        }
        catch (Exception exception) when (Recoverable(exception))
        { Debug.LogError("ritual-fast-committed-mood-observer-failed:" + exception); }
        finally { if (lease.IsValid) facts.Cancel(lease); }
    }

    private static bool Succeeded(out string failure) { failure = string.Empty; return true; }
    private static bool Hash(string value) => value?.Length == 64
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');
    private static bool Recoverable(Exception exception) => exception is not OutOfMemoryException
        and not StackOverflowException and not AccessViolationException;

    private static CharacterRitualFastPhase Phase(CharacterRitualFastRuntimeState state) => state.active
        ? CharacterRitualFastPhase.Fasting : state.awaitingPostFastMeal
            ? CharacterRitualFastPhase.AwaitingPostFastMeal : CharacterRitualFastPhase.Inactive;

    public bool TryRetainForPendingExternalOwner(string characterId, CharacterIdentityRuleStateSaveData saved)
    {
        if (saved?.ruleId != StateRuleId) return false;
        ValidateStatePayload(saved.statePayload, characterId, saved.traitDefinitionId);
        var state = JsonUtility.FromJson<CharacterRitualFastRuntimeState>(saved.statePayload);
        if (!state.pending.active) return false;
        state.pending.retainedForDeath = true;
        WriteState(characterId, saved.traitDefinitionId, state);
        hasPendingRecovery = true;
        return true;
    }

    internal static void ValidateStatePayload(string payload, string characterId = null, string traitId = null)
    {
        JObject raw = JObject.Parse(payload);
        string[] fields = { "schemaVersion", "active", "awaitingPostFastMeal", "startedDay", "transitionSequence",
            "cycleStartResultKey", "lastTransitionResultKey", "lastMealOperationId", "lastMealResultKey", "pending" };
        if (fields.Any(field => raw[field] == null))
            throw new InvalidOperationException("Ritual fasting state has missing fields.");
        var state = JsonUtility.FromJson<CharacterRitualFastRuntimeState>(payload);
        if (state == null || state.schemaVersion != 3 || state.transitionSequence <= 0
            || state.active && state.awaitingPostFastMeal || state.startedDay < -1
            || ((state.active || state.awaitingPostFastMeal) != (state.startedDay >= 0))
            || string.IsNullOrWhiteSpace(state.cycleStartResultKey) || string.IsNullOrWhiteSpace(state.lastTransitionResultKey)
            || string.IsNullOrEmpty(state.lastMealOperationId) != string.IsNullOrEmpty(state.lastMealResultKey))
            throw new InvalidOperationException("Ritual fasting state is invalid.");
        if (raw["pending"] is not JObject pendingRaw || pendingRaw["active"]?.Type != JTokenType.Boolean
            || pendingRaw["fact"] is not JObject || pendingRaw["publicationKey"]?.Type != JTokenType.String
            || pendingRaw["publicationId"]?.Type != JTokenType.String || pendingRaw["fingerprint"]?.Type != JTokenType.String
            || pendingRaw["retainedForDeath"]?.Type != JTokenType.Boolean
            || state.pending == null || state.pending.fact == null)
            throw new InvalidOperationException("Ritual fasting pending publication fields are missing.");
        var pending = state.pending;
        if (pending.active)
        {
            string[] requiredFactFields = { "characterId", "characterName", "traitId", "sequence", "transition", "cause",
                "before", "after", "day", "hour", "startBefore", "startAfter", "moodBefore", "moodAfter", "resolvedMood",
                "moodEvent", "cycleStartPublicationKey", "mealOperation", "mealPublicationKey", "mealEvidence" };
            if (requiredFactFields.Any(field => pendingRaw["fact"][field] == null
                || pendingRaw["fact"][field].Type == JTokenType.Null))
                throw new InvalidOperationException("Ritual fasting pending fact fields are missing.");
            if (pending.fact.cause == CharacterRitualFastCause.DirectMeal)
            {
                if (pendingRaw["fact"]["mealEvidence"] is not JObject mealRaw
                    || typeof(CharacterMealConsumedFactData).GetFields().Any(field => mealRaw[field.Name] == null
                        || mealRaw[field.Name].Type == JTokenType.Null))
                    throw new InvalidOperationException("Ritual fasting frozen meal evidence fields are missing.");
            }
            var fact = new CharacterRitualFastTransitionFact(pending.fact).Snapshot();
            if (string.IsNullOrWhiteSpace(pending.publicationKey) || pending.publicationKey != state.lastTransitionResultKey
                || fact.sequence != state.transitionSequence || fact.after != Phase(state) || fact.startAfter != state.startedDay
                || fact.transition == CharacterRitualFastTransition.Started && state.cycleStartResultKey != pending.publicationKey
                || fact.transition != CharacterRitualFastTransition.Started && state.cycleStartResultKey != fact.cycleStartPublicationKey
                || fact.mealOperation.Length > 0 && (state.lastMealOperationId != fact.mealOperation
                    || state.lastMealResultKey != fact.mealPublicationKey)
                || string.IsNullOrEmpty(pending.publicationId) != string.IsNullOrEmpty(pending.fingerprint)
                || pending.fingerprint.Length > 0 && !Hash(pending.fingerprint))
                throw new InvalidOperationException("Ritual fasting pending fact differs from its saved authority.");
            if (characterId != null && fact.characterId != characterId || traitId != null && fact.traitId != traitId)
                throw new InvalidOperationException("Ritual fasting pending fact belongs to another character or trait.");
        }
        else if (pending.fact.sequence != 0 || pending.publicationKey.Length != 0
            || pending.publicationId.Length != 0 || pending.fingerprint.Length != 0 || pending.retainedForDeath)
            throw new InvalidOperationException("Inactive fasting publication retains pending evidence.");
    }

    private bool TryResolve(
        CharacterActor actor,
        out CharacterTraitSO trait,
        out PersistentNeedRule rule)
    {
        trait = null;
        rule = null;
        foreach (CharacterTraitSO candidate in actor?.Progression?.ResolveSelectedTraits()
                     ?? Array.Empty<CharacterTraitSO>())
        {
            PersistentNeedRule candidateRule = (candidate?.identityRules
                    ?? new List<CharacterIdentityRule>())
                .OfType<PersistentNeedRule>()
                .FirstOrDefault(value => string.Equals(
                    value.needId,
                    NeedId,
                    StringComparison.Ordinal));
            if (candidateRule == null)
                continue;
            if (!(candidate.identityRules ?? new List<CharacterIdentityRule>())
                .Any(value => value is BehaviorUtilityRule
                    && string.Equals(
                        value.ruleId,
                        StateRuleId,
                        StringComparison.Ordinal)))
                continue;
            trait = candidate;
            rule = candidateRule;
            return true;
        }
        return false;
    }

    private CharacterRitualFastRuntimeState Read(
        CharacterActor actor,
        CharacterTraitSO trait,
        PersistentNeedRule rule)
    {
        string characterId = RequireCharacterId(actor);
        if (states.TryGet(
                characterId,
                trait.DefinitionId.Value,
                StateRuleId,
                out CharacterIdentityRuleStateSaveData saved)
            && !string.IsNullOrWhiteSpace(saved.statePayload))
        {
            ValidateStatePayload(saved.statePayload, characterId, trait.DefinitionId.Value);
            return JsonUtility.FromJson<CharacterRitualFastRuntimeState>(saved.statePayload);
        }
        return new CharacterRitualFastRuntimeState();
    }

    private void Write(
        CharacterActor actor,
        CharacterTraitSO trait,
        PersistentNeedRule rule,
        CharacterRitualFastRuntimeState state) => WriteState(RequireCharacterId(actor), trait.DefinitionId.Value, state);

    private void WriteState(string characterId, string traitId, CharacterRitualFastRuntimeState state) =>
        states.Set(
            characterId,
            traitId,
            StateRuleId,
            1,
            JsonUtility.ToJson(state));

    private static string RequireCharacterId(CharacterActor actor)
    {
        string id = actor?.Identity?.PersistentId?.Trim() ?? string.Empty;
        return id.Length > 0
            ? id
            : throw new InvalidOperationException(
                "Ritual fasting requires a persistent character id.");
    }

    private static bool HasPositiveBehaviorRule(
        CharacterActor actor,
        string behaviorTag) =>
        (actor?.Progression?.ResolveSelectedTraits()
             ?? Array.Empty<CharacterTraitSO>())
        .Where(value => value != null)
        .SelectMany(value => value.identityRules
            ?? new List<CharacterIdentityRule>())
        .OfType<BehaviorUtilityRule>()
        .Any(value => value.utilityDelta > 0f
            && string.Equals(
                value.behaviorTag,
                behaviorTag,
                StringComparison.Ordinal));
}
