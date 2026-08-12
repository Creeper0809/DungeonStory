using System;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class WorldItemTheftService
{
    private readonly IGridSystemProvider gridProvider;
    private readonly IDungeonItemCatalogProvider catalog;
    private readonly IItemHaulingSettingsProvider haulingSettings;
    private readonly IGameClock clock;
    private readonly WorldItemRepository repository;
    private readonly WorldItemQueryService queries;
    private readonly IItemMarkerPresenter markers;

    public WorldItemTheftService(
        IGridSystemProvider gridProvider,
        IDungeonItemCatalogProvider catalog,
        IItemHaulingSettingsProvider haulingSettings,
        IGameClock clock,
        WorldItemRepository repository,
        WorldItemQueryService queries,
        IItemMarkerPresenter markers)
    {
        this.gridProvider = gridProvider
            ?? throw new ArgumentNullException(nameof(gridProvider));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.haulingSettings = haulingSettings
            ?? throw new ArgumentNullException(nameof(haulingSettings));
        this.clock = clock ?? throw new ArgumentNullException(nameof(clock));
        this.repository = repository
            ?? throw new ArgumentNullException(nameof(repository));
        this.queries = queries ?? throw new ArgumentNullException(nameof(queries));
        this.markers = markers ?? throw new ArgumentNullException(nameof(markers));
    }

    public bool TryStealLooseItem(
        CharacterActor actor,
        int searchRadius,
        out WorldItemStackSnapshot stolenItem,
        out string failureReason)
    {
        stolenItem = null;
        failureReason = string.Empty;
        if (actor == null || actor.characterType != CharacterType.Customer)
        {
            failureReason = "items.theft.not_customer";
            return false;
        }
        CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(actor);
        if (inventory == null)
        {
            failureReason = "items.theft.inventory_missing";
            return false;
        }

        Vector2Int origin = ResolveActorPosition(actor);
        int radius = Mathf.Max(0, searchRadius);
        WorldItemStackRecord best = null;
        float bestScore = float.MinValue;
        foreach (WorldItemStackRecord stack in repository.Records)
        {
            if (stack == null
                || stack.quantity <= 0
                || stack.state != WorldItemStackState.Loose
                || stack.forbidden
                || stack.quantity - stack.reservedQuantity <= 0)
            {
                continue;
            }
            int distance = Mathf.Abs(stack.position.x - origin.x)
                + Mathf.Abs(stack.position.y - origin.y);
            if (distance > radius)
            {
                continue;
            }
            DungeonItemDefinition definition = catalog.GetDefinition(stack.itemId);
            if (inventory.GetMaxAcceptableQuantity(
                    stack.itemId,
                    1,
                    catalog,
                    haulingSettings) <= 0)
            {
                continue;
            }
            float score = definition.UnitPrice * 10f
                + Mathf.Min(50, stack.quantity)
                - distance * 5f;
            if (score > bestScore)
            {
                best = stack;
                bestScore = score;
            }
        }
        if (best == null)
        {
            failureReason = "items.theft.no_loose_item";
            return false;
        }
        if (!inventory.TryAddPartialStack(
                $"floor-theft:{best.stackId}:{clock.FrameCount}",
                best.itemInstanceId,
                best.itemId,
                1,
                catalog,
                haulingSettings,
                best.wasteOrigin,
                best.contamination,
                best.components,
                out int accepted,
                out failureReason)
            || accepted != 1)
        {
            failureReason = string.IsNullOrWhiteSpace(failureReason)
                ? "items.theft.carry_limit"
                : failureReason;
            return false;
        }

        stolenItem = queries.CreateSnapshot(best);
        stolenItem.Quantity = 1;
        Vector2Int position = best.position;
        best.quantity--;
        repository.MarkChanged();
        if (best.quantity <= 0)
        {
            repository.Remove(best);
        }
        markers.RefreshAt(position);
        return true;
    }

    private Vector2Int ResolveActorPosition(CharacterActor actor)
    {
        return actor != null && gridProvider.TryGetGrid(out Grid grid)
            ? grid.GetXY(actor.transform.position)
            : Vector2Int.zero;
    }
}
