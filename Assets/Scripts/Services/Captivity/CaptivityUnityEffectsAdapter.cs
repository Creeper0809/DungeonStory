using System;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class CaptivityUnityEffectsAdapter :
    ICaptivityActorEffectsPort,
    ICaptivityPerformerSupplyPort,
    ICaptivityEventEffectsPort
{
    private readonly Func<string, CharacterActor> findActor;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly IGameEventBus events;

    internal CaptivityUnityEffectsAdapter(
        Func<string, CharacterActor> findActor,
        IWorldItemStackRuntime itemRuntime,
        IGameEventBus events)
    {
        this.findActor = findActor ?? throw new ArgumentNullException(nameof(findActor));
        this.itemRuntime = itemRuntime ?? throw new ArgumentNullException(nameof(itemRuntime));
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

    public bool TryRequestFacilityDelivery(
        CaptivityFacilitySupplyKind supplyKind,
        int amount,
        Vector2Int destination,
        string destinationId,
        out int requested,
        out string failureReason) =>
        itemRuntime.TryRequestFacilityDelivery(
            ToStockCategory(supplyKind),
            amount,
            destination,
            destinationId,
            out requested,
            out failureReason);

    private static StockCategory ToStockCategory(CaptivityFacilitySupplyKind supplyKind) =>
        supplyKind switch
        {
            CaptivityFacilitySupplyKind.Food => StockCategory.Food,
            _ => throw new ArgumentOutOfRangeException(nameof(supplyKind), supplyKind, null)
        };

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
