using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class WasteProcessingStackSnapshot
{
    public ItemStackId StackId { get; set; }
    public string ItemId { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public WorldItemStackState State { get; set; }
    public Vector2Int Position { get; set; }
    public string DestinationId { get; set; } = string.Empty;
    public bool Forbidden { get; set; }
    public bool IsReserved { get; set; }
    public WasteOriginKind WasteOrigin { get; set; }
    public float Contamination { get; set; }
    public bool IsWaste => WasteOrigin != WasteOriginKind.Unknown;
}

/// <summary>
/// Economy-owned application boundary over authoritative physical item stacks.
/// The implementation lives in the composition assembly so the Economy domain
/// never depends on legacy world-item runtime DTOs or services.
/// </summary>
public interface IWasteProcessingInventoryPort
{
    IReadOnlyList<WasteProcessingStackSnapshot> GetAllStacks();

    bool TryRequestStackDelivery(
        ItemStackId stackId,
        int amount,
        Vector2Int destinationPosition,
        string destinationId,
        out int requested,
        out DomainFailure failure);

    bool TryConsumeStackQuantity(
        ItemStackId stackId,
        int quantity,
        out WasteProcessingStackSnapshot consumed,
        out DomainFailure failure);
}

/// <summary>
/// Economy-owned boundary for the scene-facing facility and production-bill
/// implementation. Facility selection and duplicate prevention are one atomic
/// application operation from the waste domain's point of view.
/// </summary>
public interface IWasteProcessingProductionPort
{
    int CountBillsMatching(Func<string, bool> recipePredicate);
    void EnsureSingleBill(ProductionRecipeSO recipe);
}
