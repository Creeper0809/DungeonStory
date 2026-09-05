using System;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivityPerformerDefaultPort : ICaptivityPerformerPort
{
    private readonly ICaptivityActorEffectsPort actors;
    private readonly ICaptivityEventEffectsPort events;

    public CaptivityPerformerDefaultPort(
        ICaptivityActorEffectsPort actors,
        ICaptivityEventEffectsPort events)
    {
        this.actors = actors ?? throw new ArgumentNullException(nameof(actors));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public bool IsActorAvailable(string captiveId) => actors.IsAvailable(captiveId);

    public void ApplyAssignmentState(string captiveId, bool assigned)
    {
        actors.SetPerformerAssignment(captiveId, assigned);
    }

    public void Publish(CaptivePerformerMilestoneEvent gameEvent) =>
        events.Publish(gameEvent);

    public void RaiseAlert(
        string title,
        string message,
        CaptivityMilestoneImportance importance,
        string category) =>
        events.RaiseAlert(title, message, importance, category);
}
