using System;
using DungeonStory.Foundation;
using VContainer.Unity;

/// <summary>
/// Projects committed catalyst drops into the alert feed using the authored item name.
/// </summary>
public sealed class EvolutionCatalystDropAlertObserver :
    IInitializable,
    IDisposable
{
    private readonly IGameEventBus events;
    private readonly IItemDefinitionCatalog items;
    private IDisposable subscription;

    public EvolutionCatalystDropAlertObserver(
        IGameEventBus events,
        IItemDefinitionCatalog items)
    {
        this.events = events ?? throw new ArgumentNullException(nameof(events));
        this.items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public void Initialize() => subscription ??=
        events.Subscribe<EvolutionCatalystDroppedEvent>(Observe);

    public void Dispose()
    {
        subscription?.Dispose();
        subscription = null;
    }

    private void Observe(EvolutionCatalystDroppedEvent value)
    {
        string displayName = items
            .GetRequired((ItemDefinitionId)value.ItemId)
            .DisplayName;
        switch (value.Origin)
        {
            case EvolutionCatalystDropOrigin.Expedition:
                events.RaiseAlert(
                    "원정 촉매 회수",
                    $"{displayName}이 하차장에 도착했습니다.",
                    EventAlertImportance.Medium,
                    "진화");
                break;
            case EvolutionCatalystDropOrigin.Defense:
                events.RaiseAlert(
                    "침공 촉매 회수",
                    $"{displayName}이 하차장에 남았습니다.",
                    EventAlertImportance.Medium,
                    "진화");
                break;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(value),
                    value.Origin,
                    "Unknown catalyst drop origin.");
        }
    }
}
