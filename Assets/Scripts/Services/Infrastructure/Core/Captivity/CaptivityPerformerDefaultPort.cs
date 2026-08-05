using System;
using DungeonStory.Operation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CaptivityPerformerDefaultPort : ICaptivityPerformerPort
{
    private readonly ICaptivityActorEffectsPort actors;
    private readonly ICaptivityPerformerSupplyPort supplies;
    private readonly ICaptivityEventEffectsPort events;

    public CaptivityPerformerDefaultPort(
        ICaptivityActorEffectsPort actors,
        ICaptivityPerformerSupplyPort supplies,
        ICaptivityEventEffectsPort events)
    {
        this.actors = actors ?? throw new ArgumentNullException(nameof(actors));
        this.supplies = supplies ?? throw new ArgumentNullException(nameof(supplies));
        this.events = events ?? throw new ArgumentNullException(nameof(events));
    }

    public bool IsActorAvailable(string captiveId) => actors.IsAvailable(captiveId);

    public void ApplyAssignmentState(string captiveId, bool assigned)
    {
        actors.SetPerformerAssignment(captiveId, assigned);
    }

    public bool TryRequestFacilityDelivery(
        CaptivityFacilitySupplyKind supplyKind,
        int amount,
        Vector2Int destination,
        string destinationId,
        out int requested,
        out string failureReason) =>
        supplies.TryRequestFacilityDelivery(
            supplyKind,
            amount,
            destination,
            destinationId,
            out requested,
            out failureReason);

    public void Publish(CaptivePerformerMilestoneEvent gameEvent) =>
        events.Publish(gameEvent);

    public void RaiseAlert(
        string title,
        string message,
        CaptivityMilestoneImportance importance,
        string category) =>
        events.RaiseAlert(title, message, importance, category);
}
