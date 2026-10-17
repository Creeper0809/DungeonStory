using System;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer.Unity;

/// <summary>
/// Projects an already-applied shoplifting fact into the existing facility,
/// alert, and character activity notifications. It does not mutate shop stock.
/// </summary>
public sealed class ShopliftingActivityObserver : IInitializable, IDisposable
{
    private readonly IGameEventBus events;
    private IDisposable subscription;

    public ShopliftingActivityObserver(IGameEventBus events) =>
        this.events = events ?? throw new ArgumentNullException(nameof(events));

    public void Initialize() => subscription ??=
        events.Subscribe<ShopliftingCompletedEvent>(Observe);

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    private void Observe(ShopliftingCompletedEvent value)
    {
        ShopliftingFactData data = value.Fact.Snapshot();
        data.Validate();

        string itemName = string.IsNullOrWhiteSpace(data.itemName)
            ? "item"
            : data.itemName;
        string detail = $"{data.actorName} stole {itemName} "
            + $"from {data.facilityName} "
            + $"(loss {data.lossValue}, chance {data.chance:0.##}).";
        string commitOperationId = "shoplifting-commit:"
            + data.unitOperationId;

        PublishPostCommit(
            () => events.Publish(new FacilityStockConsumedEvent(
                value.Actor,
                value.Shop,
                (StockCategory)data.stockCategory,
                1)),
            "shoplifting-stock-consumed");
        PublishPostCommit(
            () =>
            {
                events.Publish(new FacilityCrimeEvent(
                    value.Actor,
                    value.Shop,
                    FacilityCrimeKind.Shoplifting,
                    detail,
                    data.lossValue,
                    data.lot,
                    commitOperationId));
                events.RaiseAlert(
                    "도난 발생",
                    detail,
                    EventAlertImportance.Medium,
                    "범죄");
            },
            "shoplifting-crime-event");
        PublishPostCommit(
            () => value.Actor?.RecordActivity(
                value.Shop,
                new BuildingActivitySnapshot(
                    BuildingActivityKinds.Social,
                    BuildingActivityOutcomes.Damaged,
                    detail,
                    actionId: "crime:shoplifting",
                    reasonCode: "shoplifting",
                    value: data.lossValue,
                    quantity: 1,
                    bubbleEligible: true,
                    absoluteDay: data.absoluteDay)),
            "shoplifting-activity");
    }

    private static void PublishPostCommit(Action observer, string label)
    {
        try
        {
            observer?.Invoke();
        }
        catch (Exception exception) when (
            exception is not OutOfMemoryException
            && exception is not StackOverflowException
            && exception is not AccessViolationException)
        {
            Debug.LogError(label + ":" + exception.GetType().Name);
        }
    }
}
