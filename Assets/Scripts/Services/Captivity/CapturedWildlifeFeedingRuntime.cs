using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

internal sealed class CapturedWildlifeFeedingRuntime
{
    private static readonly IReadOnlyDictionary<StockCategory, int> WaterCost =
        new Dictionary<StockCategory, int> { [StockCategory.Water] = 1 };
    private readonly CapturedWildlifeStateSession stateSession;
    private readonly ICharacterAiWorldRegistry world;
    private readonly IWorldItemStackRuntime itemRuntime;
    private readonly IResourceEconomyContentCatalog contentCatalog;
    private readonly IWildlifeSpeciesCatalogProvider speciesCatalog;
    private readonly IWasteFeedCommand wasteProcessing;
    private readonly IWasteFeedCandidateQuery wasteFeedCandidates;
    private readonly IPhysicalItemBatchDispositionService batchDispositions;
    private readonly IRandomStream random;
    private int mutationDepth;

    public CapturedWildlifeFeedingRuntime(
        CapturedWildlifeStateSession stateSession,
        ICharacterAiWorldRegistry world,
        WildlifeCaptureCareContext care,
        IRandomStream random)
    {
        this.stateSession = stateSession ?? throw new ArgumentNullException(nameof(stateSession));
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        care = care ?? throw new ArgumentNullException(nameof(care));
        itemRuntime = care.ItemRuntime;
        contentCatalog = care.ContentCatalog;
        speciesCatalog = care.SpeciesCatalog;
        wasteProcessing = care.WasteProcessing;
        wasteFeedCandidates = care.WasteFeedCandidates;
        batchDispositions = care.BatchDispositions;
        this.random = random ?? throw new ArgumentNullException(nameof(random));
    }

    public void ValidateBeforeCapture()
    {
        if (mutationDepth != 0)
            throw new InvalidOperationException("Captured wildlife feeding is still publishing physical or care ownership.");
    }

    public void SatisfyNeeds(
        CapturedWildlifeState state,
        WildlifeActor actor,
        float dailyFood,
        float dailyWater,
        Vector2Int carePosition,
        string careDestinationId,
        bool atCarePosition,
        out bool fed,
        out bool watered)
    {
        ValidateBeforeCapture();
        mutationDepth++;
        try
        {
            RefreshDeliveryPending(state, careDestinationId);
            bool hadPendingFeed = CapturedWildlifeFeedOutbox.HasPending(state);
            state.feedSicknessSeverity = Mathf.Max(0f, state.feedSicknessSeverity - 1.5f);
            fed = (hadPendingFeed || atCarePosition)
                && TrySatisfyFoodNeed(state, actor, dailyFood, actor.Hunger,
                    carePosition, careDestinationId, ref state.foodDeliveryPending);
            watered = atCarePosition
                && TrySatisfyNeed(state, actor, StockCategory.Water, dailyWater,
                    actor.Thirst, WaterCost, carePosition, careDestinationId,
                    ref state.waterDeliveryPending);
        }
        finally
        {
            mutationDepth--;
        }
    }

    private bool TrySatisfyNeed(
        CapturedWildlifeState state,
        WildlifeActor actor,
        StockCategory category,
        float dailyNeed,
        float currentNeed,
        IReadOnlyDictionary<StockCategory, int> cost,
        Vector2Int carePosition,
        string careDestinationId,
        ref bool deliveryPending)
    {
        if (dailyNeed <= 0f || currentNeed < 0.45f)
        {
            return currentNeed < 0.45f;
        }

        if (itemRuntime.TryConsumeFacilityBuffer(
                careDestinationId,
                cost,
                out _))
        {
            if (category == StockCategory.Food)
            {
                actor.SatisfyCaptiveNeeds(0.72f, 0f);
            }
            else
            {
                actor.SatisfyCaptiveNeeds(0f, 0.8f);
            }

            deliveryPending = false;
            return true;
        }

        if (!deliveryPending)
        {
            int amount = Mathf.Max(1, Mathf.CeilToInt(dailyNeed));
            deliveryPending = itemRuntime.TryRequestFacilityDelivery(
                category,
                amount,
                carePosition,
                careDestinationId,
                out int requested,
                out _)
                && requested > 0;
        }

        return false;
    }

    private bool TrySatisfyFoodNeed(
        CapturedWildlifeState state,
        WildlifeActor actor,
        float dailyNeed,
        float currentNeed,
        Vector2Int carePosition,
        string careDestinationId,
        ref bool deliveryPending)
    {
        if (CapturedWildlifeFeedOutbox.HasPending(state))
        {
            if (!CapturedWildlifeFeedOutbox.TryFinalizePending(
                    state,
                    actor,
                    batchDispositions,
                    out _,
                    out _))
            {
                return false;
            }
            deliveryPending = false;
            return true;
        }
        if (dailyNeed <= 0f || currentNeed < 0.45f)
        {
            return currentNeed < 0.45f;
        }

        WildlifeDietType diet = speciesCatalog.TryGetSpecies(
                state.speciesId,
                out WildlifeSpeciesDefinition species)
            ? species.Diet
            : WildlifeDietType.Omnivore;
        ResourceItemDefinitionSO[] candidates = contentCatalog.Items
            .Where(item => item != null
                && WildlifeFeedSelectionRules.IsAllowed(diet, item))
            .OrderBy(item => WildlifeFeedSelectionRules.GetPreference(
                diet,
                item))
            .ThenBy(item => item.UnitPrice)
            .ThenBy(item => item.ItemId, StringComparer.Ordinal)
            .ToArray();

        foreach (ResourceItemDefinitionSO candidate in candidates)
        {
            WorldItemStackSnapshot source = itemRuntime.GetAllStacks()
                .Where(stack => stack != null
                    && stack.State == WorldItemStackState.FacilityBuffer
                    && string.Equals(
                        stack.DestinationId,
                        careDestinationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        stack.ItemId,
                        candidate.ItemId,
                        StringComparison.Ordinal)
                    && stack.AvailableQuantity > 0
                    && stack.ReservedQuantity == 0
                    && string.IsNullOrEmpty(stack.ReservedByPersistentId)
                    && !stack.Forbidden)
                .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (source == null)
            {
                continue;
            }
            if (TryBeginCapturedFeed(
                    state,
                    actor,
                    source.StackId,
                    candidate.ItemId,
                    0.72f,
                    diseaseChance: 0f,
                    diseaseTriggered: false,
                    ref deliveryPending))
            {
                return true;
            }
            if (CapturedWildlifeFeedOutbox.HasPending(state))
            {
                return false;
            }
        }

        if (wasteFeedCandidates.TryGetDirectFeedCandidate(
                diet,
                careDestinationId,
                out WasteDirectFeedCandidate wasteFeed,
                out _))
        {
            bool diseaseTriggered = wasteFeed.DiseaseChance > 0f
                && random.Chance(wasteFeed.DiseaseChance);
            if (TryBeginCapturedFeed(
                    state,
                    actor,
                    wasteFeed.StackId.Value,
                    wasteFeed.ItemId,
                    Mathf.Clamp(wasteFeed.Nutrition * 0.9f, 0.35f, 0.8f),
                    wasteFeed.DiseaseChance,
                    diseaseTriggered,
                    ref deliveryPending))
            {
                return true;
            }
            if (CapturedWildlifeFeedOutbox.HasPending(state))
            {
                return false;
            }
        }

        if (!deliveryPending)
        {
            int amount = Mathf.Max(1, Mathf.CeilToInt(dailyNeed));
            foreach (ResourceItemDefinitionSO candidate in candidates
                         .Where(candidate => WildlifeFeedSelectionRules
                             .GetPreference(diet, candidate) <= 1))
            {
                if (!itemRuntime.TryRequestItemDelivery(
                        candidate.ItemId,
                        amount,
                        carePosition,
                        careDestinationId,
                        out int requested,
                        out _)
                    || requested <= 0)
                {
                    continue;
                }

                state.lastFeedItemId = candidate.ItemId;
                deliveryPending = true;
                break;
            }

            if (!deliveryPending)
            {
                WasteFeedRequestResult wasteRequest =
                    wasteProcessing.RequestDirectFeed(
                        diet,
                        carePosition,
                        careDestinationId);
                if (wasteRequest.Succeeded)
                {
                    state.lastFeedItemId = wasteRequest.ItemId;
                    deliveryPending = true;
                }
            }

            if (!deliveryPending)
            {
                foreach (ResourceItemDefinitionSO candidate in candidates
                             .Where(candidate => WildlifeFeedSelectionRules
                                 .GetPreference(diet, candidate) > 1))
                {
                    if (!itemRuntime.TryRequestItemDelivery(
                            candidate.ItemId,
                            amount,
                            carePosition,
                            careDestinationId,
                            out int requested,
                            out _)
                        || requested <= 0)
                    {
                        continue;
                    }

                    state.lastFeedItemId = candidate.ItemId;
                    deliveryPending = true;
                    break;
                }
            }
        }

        return false;
    }

    private bool TryBeginCapturedFeed(
        CapturedWildlifeState state,
        WildlifeActor actor,
        string sourceStackId,
        string itemId,
        float nutrition,
        float diseaseChance,
        bool diseaseTriggered,
        ref bool deliveryPending)
    {
        if (state == null
            || actor == null
            || string.IsNullOrWhiteSpace(sourceStackId)
            || state.nextFeedOperationSequence == int.MaxValue)
        {
            return false;
        }

        int sequence = checked(state.nextFeedOperationSequence + 1);
        state.nextFeedOperationSequence = sequence;
        string operationId = CapturedWildlifeFeedOutbox.FormatOperationId(
            state.wildlifeId,
            sequence);
        if (!batchDispositions.TryCommitPending(
                new[]
                {
                    new PhysicalItemTransformInput(sourceStackId, 1)
                },
                PhysicalItemDispositionKind.Sink,
                operationId,
                CapturedWildlifeFeedOutbox.ReasonCode,
                out PhysicalItemBatchDispositionReceipt receipt,
                out _))
        {
            return false;
        }

        CapturedWildlifeFeedOutbox.RecordPending(
            state,
            sequence,
            receipt,
            itemId,
            nutrition,
            diseaseChance,
            diseaseTriggered,
            actor);
        if (!CapturedWildlifeFeedOutbox.TryFinalizePending(
                state,
                actor,
                batchDispositions,
                out _,
                out _))
        {
            return false;
        }
        deliveryPending = false;
        return true;
    }

    public void ReconcilePendingFeedsOrThrow()
    {
        ValidateBeforeCapture();
        mutationDepth++;
        try
        {
            foreach (CapturedWildlifeState state in stateSession.Values
                         .OrderBy(value => value.wildlifeId, StringComparer.Ordinal))
            {
                if (!CapturedWildlifeFeedOutbox.HasPending(state))
                {
                    continue;
                }
                WildlifeActor actor = FindActor(state.wildlifeId);
                if (actor == null)
                {
                    throw new InvalidOperationException(
                        $"Captured wildlife feed restore reconciliation failed for '{state.wildlifeId}': actor-missing");
                }
                if (!CapturedWildlifeFeedOutbox.TryFinalizePending(
                        state,
                        actor,
                        batchDispositions,
                        out _,
                        out string failureReason))
                {
                    throw new InvalidOperationException(
                        $"Captured wildlife feed restore reconciliation failed for '{state.wildlifeId}': {failureReason}");
                }
            }

        }
        finally
        {
            mutationDepth--;
        }
    }

    private void RefreshDeliveryPending(
        CapturedWildlifeState state,
        string careDestinationId)
    {
        bool hasFood = false;
        bool hasWater = false;
        foreach (WorldItemStackSnapshot stack in itemRuntime.GetAllStacks())
        {
            if (!string.Equals(
                    stack.DestinationId,
                    careDestinationId,
                    StringComparison.Ordinal))
            {
                continue;
            }

            DungeonItemDefinition definition =
                itemRuntime.CatalogProvider.GetDefinition(stack.ItemId);
            hasFood |= string.IsNullOrWhiteSpace(state.lastFeedItemId)
                ? definition?.StockCategory == StockCategory.Food
                : string.Equals(
                    stack.ItemId,
                    state.lastFeedItemId,
                    StringComparison.Ordinal);
            hasWater |= definition?.StockCategory == StockCategory.Water;
        }

        state.foodDeliveryPending &= hasFood;
        state.waterDeliveryPending &= hasWater;
    }

    private WildlifeActor FindActor(string wildlifeId)
    {
        return world.Wildlife.FirstOrDefault(actor =>
            actor != null
            && string.Equals(actor.WildlifeId, wildlifeId, StringComparison.Ordinal));
    }
}
