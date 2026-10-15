using System;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

/// <summary>Post-commit mood notifications, activity bubbles and alerts observe delivered facts, never reapplying staff or mood state.</summary>
public sealed class StaffDiscontentPresentationObserver : IInitializable, IDisposable
{
    private readonly ICharacterLifetimeQuery world;
    private readonly IGameEventBus events;
    private IDisposable transitions, responses, guards;
    public StaffDiscontentPresentationObserver(ICharacterLifetimeQuery world, IGameEventBus events)
    { this.world = world ?? throw new ArgumentNullException(nameof(world)); this.events = events ?? throw new ArgumentNullException(nameof(events)); }
    public void Initialize()
    {
        if (transitions != null) return;
        transitions = events.Subscribe<StaffDiscontentTransitionedFact>(Observe);
        responses = events.Subscribe<StaffRebellionResponseResolvedFact>(Observe);
        guards = events.Subscribe<StaffRebellionGuardsAssignedFact>(value =>
            events.RaiseStaffComplaint($"{value.StaffName}: 자동 제압 {value.Count}명 배정", EventAlertImportance.Medium));
    }
    public void Dispose()
    { transitions?.Dispose(); responses?.Dispose(); guards?.Dispose(); transitions = responses = guards = null; }

    private CharacterActor Find(string id) => world.AllCharacters.FirstOrDefault(value => value != null
        && StaffDiscontentService.GetStaffId(value) == id);
    private void Observe(StaffDiscontentTransitionedFact fact)
    {
        var value = fact.Snapshot();
        var actor = Find(fact.StaffId);
        string outcome, text, reason, label;
        float sentiment;
        EventAlertImportance importance;
        switch (value.transition)
        {
            case StaffDiscontentOutcome.Warning:
                outcome = CharacterActivityOutcomes.Changed; text = "직원 불만: 만족도 낮음"; reason = "low-satisfaction";
                label = "만족도 낮음"; sentiment = -0.45f; importance = EventAlertImportance.Low; break;
            case StaffDiscontentOutcome.EfficiencyPenalty:
                outcome = CharacterActivityOutcomes.Changed; text = "직원 불만: 효율 저하"; reason = "efficiency-penalty";
                label = "효율 저하"; sentiment = -0.55f; importance = EventAlertImportance.Medium; break;
            case StaffDiscontentOutcome.WorkDisruption:
                outcome = CharacterActivityOutcomes.Blocked; text = "직원 불만: 태업/결근"; reason = "work-disruption";
                label = "태업/결근"; sentiment = -0.7f; importance = EventAlertImportance.Medium; break;
            case StaffDiscontentOutcome.PermanentDeparture:
                outcome = CharacterActivityOutcomes.Departed; text = "직원 이탈: 영구 손실"; reason = "permanent-departure";
                label = "이탈"; sentiment = -1f; importance = EventAlertImportance.High; break;
            case StaffDiscontentOutcome.LocalRebellion:
                outcome = CharacterActivityOutcomes.Started; text = "국지 반란: 주변 피해 시작"; reason = "local-rebellion";
                label = "국지 반란"; sentiment = -1f; importance = EventAlertImportance.High; break;
            case StaffDiscontentOutcome.OwnerThreat:
                outcome = CharacterActivityOutcomes.Changed; text = "반란 확산: 사장 위협"; reason = "owner-threat";
                label = "반란 확산"; sentiment = -1f; importance = EventAlertImportance.High; break;
            default: throw new InvalidOperationException("staff-discontent-notification-kind-invalid");
        }
        TryNotify(() => Write(actor, outcome, text, reason, sentiment));
        TryNotify(() => events.RaiseStaffComplaint($"{value.after.displayName}: {label}", importance));
    }
    private void Observe(StaffRebellionResponseResolvedFact fact)
    {
        var value = fact.Snapshot();
        var actor = Find(fact.StaffId);
        if (value.moodApplied)
            TryNotify(() => actor?.Stats?.PublishCommittedMoodFactor("management:calmed", value.resolvedMoodDelta, value.absoluteDay));
        if (value.responseType == StaffRebellionResponseType.Isolate)
            TryNotify(() => Write(actor, CharacterActivityOutcomes.Completed, "반란 대응: 격리", "rebellion-isolated", 0.1f));
        else if (value.responseType == StaffRebellionResponseType.Calm)
            TryNotify(() => Write(actor, CharacterActivityOutcomes.Completed, "반란 대응: 진정", "rebellion-calmed", 0.35f));
        string label = value.responseType switch
        { StaffRebellionResponseType.Calm => "진정", StaffRebellionResponseType.Isolate => "격리", _ => "제압 완료" };
        TryNotify(() => events.RaiseStaffComplaint($"{value.after.displayName}: {label}",
            value.responseType == StaffRebellionResponseType.Calm ? EventAlertImportance.Low : EventAlertImportance.Medium));
    }
    private static void Write(CharacterActor actor, string outcome, string text, string reason, float sentiment) =>
        actor?.AddActivity(CharacterActivityEvent.Create(CharacterActivityKinds.Social, outcome, text,
            actionId: "staff-discontent", reasonCode: reason, sentiment: sentiment, bubbleEligible: true));
    private static void TryNotify(Action notify)
    {
        try { notify(); }
        catch (Exception exception) when (exception is not OutOfMemoryException
            && exception is not StackOverflowException && exception is not AccessViolationException)
        { Debug.LogException(exception); }
    }
}
