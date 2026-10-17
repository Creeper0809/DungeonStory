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
                || FacilityOutputExactRouteCustodyCodec.HasAnyCustody(
                    stack.components)
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
                    stack.itemInstanceId,
                    stack.components,
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
        if (FacilityOutputExactRouteCustodyCodec.HasAnyCustody(best.components))
        {
            failureReason = "items.theft.prepared_output_route_protected";
            return false;
        }

        CharacterCarryInventorySaveData inventoryBefore = inventory.Capture();
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

        // Inventory Changed callbacks are allowed to run during publication.
        // Revalidate the physical source immediately before its quantity commit.
        if (FacilityOutputExactRouteCustodyCodec.HasAnyCustody(best.components))
        {
            inventory.Restore(inventoryBefore);
            failureReason = "items.theft.prepared_output_route_protected";
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

    public bool TryCommitVisitorExitTheft(
        CharacterActor actor,
        int searchRadius,
        CharacterVisitorExitOperationSaveData operation,
        VisitorExitPublicationRuntime publications,
        out WorldItemStackSnapshot stolenItem,
        out string failureReason)
    {
        stolenItem = null;
        failureReason = string.Empty;
        if (publications == null || operation == null || !operation.IsActive
            || operation.phase != CharacterVisitorExitPhase.ExitPending || publications.IsBusy || publications.IsRestoring)
        {
            failureReason = "items.theft.observation_owner_unavailable";
            return false;
        }
        if (actor == null || actor.characterType != CharacterType.Customer)
        {
            failureReason = "items.theft.not_customer";
            return false;
        }
        string characterId = actor.Identity?.PersistentId;
        if (string.IsNullOrWhiteSpace(characterId))
        {
            failureReason = "items.theft.character_identity_missing";
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
                || FacilityOutputExactRouteCustodyCodec.HasAnyCustody(stack.components)
                || stack.quantity - stack.reservedQuantity <= 0)
            {
                continue;
            }
            int distance = Mathf.Abs(stack.position.x - origin.x)
                + Mathf.Abs(stack.position.y - origin.y);
            if (distance > radius
                || inventory.GetMaxAcceptableQuantity(
                    stack.itemId,
                    stack.itemInstanceId,
                    stack.components,
                    1,
                    catalog,
                    haulingSettings) <= 0)
            {
                continue;
            }
            DungeonItemDefinition definition = catalog.GetDefinition(stack.itemId);
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

        WorldItemStackSnapshot exactSource = queries.CreateSnapshot(best);
        exactSource.Quantity = 1;
        int sourceBefore = best.quantity;
        string parentOperationId = operation.operationId;
        string theftOperationId = parentOperationId + ":floor-theft:1";
        string carriedStackId = theftOperationId + ":carry";
        var fact = new FloorItemStolenFact(new FloorItemStolenFactData
        {
            operationId = theftOperationId, parentOperationId = parentOperationId,
            ownerRevision = operation.ownerRevision, characterId = characterId,
            characterDisplayName = actor.Identity.DisplayName, absoluteDay = operation.absoluteDay,
            sourceStackId = exactSource.StackId, carriedStackId = carriedStackId,
            itemInstanceId = exactSource.ItemInstanceId ?? string.Empty, itemDefinitionId = exactSource.ItemId,
            componentFingerprint = exactSource.StackSignature ?? string.Empty,
            sourceQuantityBefore = sourceBefore, sourceQuantityAfter = sourceBefore - 1,
            carryQuantityBefore = 0, carryQuantityAfter = 1,
            unitMassGrams = Math.Max(1L, (long)Math.Round(exactSource.UnitWeight * 1000d)),
            positionX = best.position.x, positionY = best.position.y
        });
        if (!publications.TryPrepare(fact, out var prepared, out failureReason)) return false;
        Vector2Int sourcePosition = best.position;
        using (prepared)
        {
            CharacterCarryInventorySaveData inventoryBefore = inventory.Capture();
            bool sourceChanged = false, sourceRemoved = false;
            var phaseBefore = operation.phase;
            string keyBefore = operation.theftPublicationKey;
            prepared.SetRollback(() =>
            {
                inventory.Restore(inventoryBefore);
                if (sourceChanged)
                {
                    // A removed record still holds quantity zero: restore its value before reinserting it.
                    best.quantity = sourceBefore;
                    if (sourceRemoved)
                    {
                        repository.Add(best);
                        sourceRemoved = false;
                    }
                    repository.MarkChanged();
                    sourceChanged = false;
                }
                operation.phase = phaseBefore;
                operation.theftPublicationKey = keyBefore;
                return true;
            });
            try
            {
                if (!inventory.TryAddLeasedPartialStack(carriedStackId, exactSource.StackId, parentOperationId,
                        exactSource.ItemInstanceId, exactSource.ItemId, 1, catalog, haulingSettings,
                        best.wasteOrigin, best.contamination, best.components, out int accepted, out failureReason)
                    || accepted != 1)
                {
                    failureReason = string.IsNullOrWhiteSpace(failureReason) ? "items.theft.carry_limit" : failureReason;
                    return false;
                }
                if (!repository.RecordsById.TryGetValue(exactSource.StackId, out var currentSource)
                    || !ReferenceEquals(best, currentSource)
                    || actor.characterType != CharacterType.Customer || actor.Identity.PersistentId != characterId
                    || best.state != WorldItemStackState.Loose || best.forbidden
                    || best.quantity - best.reservedQuantity <= 0
                    || best.itemId != exactSource.ItemId || (best.itemInstanceId ?? string.Empty) != (exactSource.ItemInstanceId ?? string.Empty)
                    || best.position != sourcePosition
                    || queries.CreateSnapshot(best).StackSignature != exactSource.StackSignature
                    || FacilityOutputExactRouteCustodyCodec.HasAnyCustody(best.components) || best.quantity != sourceBefore)
                { failureReason = "items.theft.source_changed_before_commit"; return false; }
                sourceChanged = true;
                best.quantity--;
                repository.MarkChanged();
                if (best.quantity <= 0) { repository.Remove(best); sourceRemoved = true; }
                operation.theftPublicationKey = prepared.PublicationKey;
                operation.phase = CharacterVisitorExitPhase.TheftCommitted;
                var result = prepared.PublishApplied();
                if (!result.MayBeCommitted)
                { failureReason = "items.theft.observation_rejected:" + result.Detail; return false; }
            }
            catch (Exception exception) when (exception is not OutOfMemoryException
                && exception is not StackOverflowException && exception is not AccessViolationException)
            {
                failureReason = "items.theft.transfer_failed:" + exception.Message;
                return false;
            }
        }
        stolenItem = exactSource;
        // Presentation failure must not undo an already published physical transfer.
        try { markers.RefreshAt(sourcePosition); }
        catch (Exception exception) { Debug.LogError("items.theft.marker_refresh_failed:" + exception); }
        return true;
    }

    private Vector2Int ResolveActorPosition(CharacterActor actor)
    {
        return actor != null && gridProvider.TryGetGrid(out Grid grid)
            ? grid.GetXY(actor.transform.position)
            : Vector2Int.zero;
    }
}
