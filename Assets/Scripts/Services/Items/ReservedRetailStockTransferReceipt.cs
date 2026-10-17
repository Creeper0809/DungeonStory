using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;

public sealed class ReservedRetailStockTransferReceipt
{
    internal ReservedRetailStockTransferReceipt(
        string operationId,
        IReadOnlyList<RetailStockLotSnapshot> lots,
        IReadOnlyList<RetailStockSourceUndo> undo,
        CharacterCarryInventory carryInventory,
        IReadOnlyList<CharacterCarriedItemSaveData> carriedUndo)
    {
        OperationId = operationId;
        Lots = lots;
        Undo = undo;
        CarryInventory = carryInventory;
        CarriedUndo = carriedUndo;
    }

    public string OperationId { get; }
    public IReadOnlyList<RetailStockLotSnapshot> Lots { get; }
    internal IReadOnlyList<RetailStockSourceUndo> Undo { get; }
    internal CharacterCarryInventory CarryInventory { get; }
    internal IReadOnlyList<CharacterCarriedItemSaveData> CarriedUndo { get; }
    internal bool RolledBack { get; set; }
}

internal readonly struct RetailStockSourceUndo
{
    internal RetailStockSourceUndo(
        WorldItemStackRecord snapshot,
        int removedQuantity,
        bool recordWasRemoved)
    {
        Snapshot = snapshot;
        RemovedQuantity = removedQuantity;
        RecordWasRemoved = recordWasRemoved;
    }

    internal WorldItemStackRecord Snapshot { get; }
    internal int RemovedQuantity { get; }
    internal bool RecordWasRemoved { get; }
}
