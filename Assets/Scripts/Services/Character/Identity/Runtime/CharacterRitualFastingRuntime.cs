using System;
using System.Collections.Generic;
using System.Linq;
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
    void RecordMealConsumed(CharacterActor actor, bool directPlayerOrder);
}

[Serializable]
public sealed class CharacterRitualFastRuntimeState
{
    public bool active;
    public bool awaitingPostFastMeal;
    public int startedDay = -1;
}

/// <summary>
/// Saved authority for the ritual-fast identity rule. Mood factors are results,
/// never the source used to decide whether conditional gameplay effects apply.
/// </summary>
public sealed class CharacterRitualFastingRuntime :
    ICharacterRitualFastingQuery,
    ICharacterRitualFastingCommand,
    ITickable
{
    public const string NeedId = "need:ritual-fast";
    private readonly CharacterIdentityStateStore states;
    private readonly CharacterMoodPolicyService moods;
    private readonly IGameCalendar calendar;
    private readonly ICharacterWorldQuery world;
    private readonly IFestivalDefinitionCatalog festivals;
    private int lastAutonomyDay = -1;

    public CharacterRitualFastingRuntime(
        CharacterIdentityStateStore states,
        CharacterMoodPolicyService moods,
        IGameCalendar calendar,
        ICharacterWorldQuery world,
        IFestivalDefinitionCatalog festivals)
    {
        this.states = states ?? throw new ArgumentNullException(nameof(states));
        this.moods = moods ?? throw new ArgumentNullException(nameof(moods));
        this.calendar = calendar ?? throw new ArgumentNullException(nameof(calendar));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.festivals = festivals ?? throw new ArgumentNullException(nameof(festivals));
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
        int today = Math.Max(1, calendar.Day);
        if (today == lastAutonomyDay)
            return;
        lastAutonomyDay = today;

        CalendarDateTime tomorrow = GameCalendarRules.Project(today + 1, 0);
        bool festivalTomorrow = festivals.All.Any(value => value != null
            && value.season == tomorrow.Season
            && value.dayOfSeason == tomorrow.DayOfSeason);
        if (!festivalTomorrow)
            return;

        foreach (CharacterActor actor in world.Characters
                     .Where(value => value != null && !value.IsDead)
                     .OrderBy(
                         value => value.Identity?.PersistentId ?? string.Empty,
                         StringComparer.Ordinal))
        {
            if (HasPositiveBehaviorRule(actor, "ritual:fast"))
                TryBegin(actor, out _);
        }
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
            state.active && calendar.Day > state.startedDay);
    }

    [GameplayEntryPoint(
        "StaffManagementSurfacePanel ritual-fast start button and autonomous fasting tick")]
    public bool TryBegin(CharacterActor actor, out string reason)
    {
        reason = string.Empty;
        if (!TryResolve(actor, out CharacterTraitSO trait, out PersistentNeedRule rule))
        {
            reason = "의식 단식 특성이 없습니다.";
            return false;
        }
        CharacterRitualFastRuntimeState state = Read(actor, trait, rule);
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
        Write(actor, trait, rule, state);
        return true;
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
        Write(actor, trait, rule, state);
        moods.Apply(
            actor,
            rule.satisfiedEventId,
            0f,
            rule.moodDurationDays,
            "의식 단식 완수");
        return true;
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
        if (!state.active)
        {
            reason = "중단할 의식 단식이 없습니다.";
            return false;
        }
        state.active = false;
        state.awaitingPostFastMeal = false;
        Write(actor, trait, rule, state);
        moods.Apply(
            actor,
            rule.deprivedEventId,
            0f,
            rule.moodDurationDays,
            "의식 단식 파기");
        return true;
    }

    [GameplayInternalOnly(
        "The physical meal-consumption adapter records fasting consequences only after an actual meal succeeds.",
        "CharacterConsumablesApplicationAdapters")]
    public void RecordMealConsumed(CharacterActor actor, bool directPlayerOrder)
    {
        if (!TryResolve(actor, out CharacterTraitSO trait, out PersistentNeedRule rule))
            return;
        CharacterRitualFastRuntimeState state = Read(actor, trait, rule);
        if (state.active)
        {
            // Autonomous meals are rejected before physical consumption. Reaching
            // this branch therefore means an explicit order overrode the trait.
            if (directPlayerOrder)
                TryBreak(actor, out _);
            return;
        }
        if (!state.awaitingPostFastMeal)
            return;
        state.awaitingPostFastMeal = false;
        state.startedDay = -1;
        Write(actor, trait, rule, state);
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
                rule.ruleId,
                out CharacterIdentityRuleStateSaveData saved)
            && !string.IsNullOrWhiteSpace(saved.statePayload))
        {
            return JsonUtility.FromJson<CharacterRitualFastRuntimeState>(
                       saved.statePayload)
                   ?? new CharacterRitualFastRuntimeState();
        }
        return new CharacterRitualFastRuntimeState();
    }

    private void Write(
        CharacterActor actor,
        CharacterTraitSO trait,
        PersistentNeedRule rule,
        CharacterRitualFastRuntimeState state) =>
        states.Set(
            RequireCharacterId(actor),
            trait.DefinitionId.Value,
            rule.ruleId,
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
