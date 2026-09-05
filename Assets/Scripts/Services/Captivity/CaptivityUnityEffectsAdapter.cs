using System;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class CaptivityUnityEffectsAdapter :
    ICaptivityActorEffectsPort,
    ICaptivityEventEffectsPort
{
    private readonly Func<string, CharacterActor> findActor;
    private readonly IGameEventBus events;

    internal CaptivityUnityEffectsAdapter(
        Func<string, CharacterActor> findActor,
        IGameEventBus events)
    {
        this.findActor = findActor ?? throw new ArgumentNullException(nameof(findActor));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public bool IsAvailable(string captiveId) => findActor(captiveId) != null;

    public void ConfineLaborer(string captiveId)
    {
        CharacterActor actor = findActor(captiveId);
        if (actor == null) return;
        actor.characterType = CharacterType.Intruder;
        actor.SetAiPaused(true);
        actor.SetLifecycleState(CharacterLifecycleState.Downed);
    }

    public void SetPerformerAssignment(string captiveId, bool assigned)
    {
        CharacterActor actor = findActor(captiveId);
        if (actor == null) return;
        actor.SetAiPaused(true);
        actor.SetLifecycleState(
            assigned ? CharacterLifecycleState.Active : CharacterLifecycleState.Downed);
    }

    public void Publish(CaptivePerformerMilestoneEvent gameEvent) =>
        events.Publish(gameEvent);

    public void RaiseAlert(
        string title,
        string message,
        CaptivityMilestoneImportance importance,
        string category) =>
        events.RaiseAlert(
            title,
            message,
            importance == CaptivityMilestoneImportance.High
                ? EventAlertImportance.High
                : EventAlertImportance.Medium,
            category);
}
