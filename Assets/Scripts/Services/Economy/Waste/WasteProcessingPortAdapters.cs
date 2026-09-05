using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class WasteProcessingInventoryPortAdapter :
    IWasteProcessingInventoryPort
{
    private readonly IStockQuery stock;
    private readonly IItemTransferService transfers;
    private readonly IFacilityBufferDestinationClaimQuery claims;
    private readonly IFacilityBufferMassCapacityQuery capacities;

    public WasteProcessingInventoryPortAdapter(
        IStockQuery stock,
        IItemTransferService transfers,
        IFacilityBufferDestinationClaimQuery claims,
        IFacilityBufferMassCapacityQuery capacities)
    {
        this.stock = stock ?? throw new ArgumentNullException(nameof(stock));
        this.transfers = transfers
            ?? throw new ArgumentNullException(nameof(transfers));
        this.claims = claims ?? throw new ArgumentNullException(nameof(claims));
        this.capacities = capacities
            ?? throw new ArgumentNullException(nameof(capacities));
    }

    public bool TryGetExactWildlifeCareDestinationPosition(
        string destinationId,
        out Vector2Int destinationPosition)
    {
        FacilityBufferDestinationClaim[] matching = claims.CaptureClaims()
            .Where(value => value != null
                && string.Equals(
                    value.DestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            .ToArray();
        destinationPosition = matching.Length == 1
            ? matching[0].DropPosition
            : default;
        return matching.Length == 1
            && HasExactWildlifeCareDestinationAuthority(
                destinationId,
                destinationPosition);
    }

    public bool HasExactWildlifeCareDestinationAuthority(
        string destinationId,
        Vector2Int destinationPosition)
    {
        const string ownerDomain = "captivity.wildlife-care";
        return claims.TryGetClaim(
                destinationId,
                destinationPosition,
                out FacilityBufferDestinationClaim claim)
            && claim != null
            && claim.AnchorKind
                == FacilityBufferDestinationAnchorKind.LiveFacility
            && claim.AdmissionPolicy
                == FacilityBufferDestinationAdmissionPolicy.ExactGramRequired
            && string.Equals(
                claim.OwnerDomain,
                ownerDomain,
                StringComparison.Ordinal)
            && capacities.TryGetCapacity(
                destinationId,
                destinationPosition,
                out FacilityBufferMassCapacitySnapshot capacity)
            && capacity.Profile != null
            && capacity.Profile.MaxMassGrams > 0L
            && capacity.Profile.CapacityRevision > 0L
            && string.Equals(
                capacity.Profile.OwnerDomain,
                ownerDomain,
                StringComparison.Ordinal)
            && string.Equals(
                capacity.Profile.OwnerOperationId,
                claim.OwnerOperationId,
                StringComparison.Ordinal)
            && string.Equals(
                capacity.Profile.OwnerFacilityId,
                claim.OwnerFacilityId,
                StringComparison.Ordinal);
    }

    public IReadOnlyList<WasteProcessingStackSnapshot> GetAllStacks() =>
        stock.GetAllStacks()
            .Where(stack => stack != null)
            .Select(ToWasteSnapshot)
            .ToArray();

    public bool TryRequestStackDelivery(
        ItemStackId stackId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out DomainFailure failure) => transfers.TryRequestStackDelivery(
            stackId,
            amount,
            destinationPosition,
            destinationId,
            out requested,
            out failure);

    private static WasteProcessingStackSnapshot ToWasteSnapshot(
        WorldItemStackSnapshot source) => new()
    {
        StackId = (ItemStackId)source.StackId,
        ItemId = source.ItemId,
        Quantity = source.Quantity,
        ReservedQuantity = source.ReservedQuantity,
        State = source.State,
        Position = source.Position,
        DestinationId = source.DestinationId,
        Forbidden = source.Forbidden,
        WasteOrigin = source.WasteOrigin,
        Contamination = source.Contamination
    };
}

public sealed class WasteProcessingProductionPortAdapter :
    IWasteProcessingProductionPort
{
    private readonly ICharacterAiWorldRegistry world;
    private readonly IProductionBillQuery productionQuery;
    private readonly IProductionBillOrderCommand productionCommands;

    public WasteProcessingProductionPortAdapter(
        ICharacterAiWorldRegistry world,
        IProductionBillQuery productionQuery,
        IProductionBillOrderCommand productionCommands)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.productionQuery = productionQuery
            ?? throw new ArgumentNullException(nameof(productionQuery));
        this.productionCommands = productionCommands
            ?? throw new ArgumentNullException(nameof(productionCommands));
    }

    public int CountBillsMatching(Func<string, bool> recipePredicate)
    {
        if (recipePredicate == null)
        {
            throw new ArgumentNullException(nameof(recipePredicate));
        }

        return world.Buildings
            .Where(building => building != null)
            .Sum(building => productionQuery.GetBills(building)
                .Count(bill => recipePredicate(bill.RecipeId)));
    }

    public void EnsureSingleBill(ProductionRecipeSO recipe)
    {
        if (recipe == null)
        {
            throw new ArgumentNullException(nameof(recipe));
        }

        BuildableObject facility = world.Buildings
            .Where(building => building != null
                && !building.IsGridDestroyed
                && building.PersistentInstanceId.IsValid
                && building.HasSemanticTag(recipe.FacilityTag)
                && building.SupportsWork(recipe.WorkTypeId))
            .OrderBy(building => productionQuery.GetBills(building).Count)
            .ThenBy(building => building.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .FirstOrDefault();
        if (facility == null
            || world.Buildings
                .Where(building => building != null)
                .SelectMany(building => productionQuery.GetBills(building))
                .Any(bill => string.Equals(
                    bill.RecipeId,
                    recipe.RecipeId,
                    StringComparison.Ordinal)))
        {
            return;
        }

        productionCommands.AddBill(
            facility,
            recipe.RecipeId,
            ProductionOrderMode.RepeatCount,
            1);
    }
}
