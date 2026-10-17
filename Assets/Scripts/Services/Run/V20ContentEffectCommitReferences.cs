using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Operation;
using UnityEngine;


internal sealed class V20ContentEffectCommitReferences
{
    internal static V20ContentEffectCommitReferences From(
        V20ContentResolutionRecoverySaveData recovery) => new()
    {
        MoneyTransactionId = recovery?.moneyTransactionId ?? string.Empty,
        ItemConsumptionOperationId = recovery?.itemConsumptionOperationId
            ?? string.Empty,
        ItemGrantBatchCommitId = recovery?.itemGrantBatchCommitId
            ?? string.Empty,
        ItemConsumptionCommitId = recovery?.itemConsumptionCommitId ?? string.Empty,
        ItemGrantReceiptId = recovery?.itemGrantReceiptId ?? string.Empty,
        DurableEquipmentStackId = recovery?.durableEquipmentStackId
            ?? string.Empty,
        DurableEquipmentItemId = recovery?.durableEquipmentItemId
            ?? string.Empty,
        DurableEquipmentContext = recovery?.durableEquipmentContext
            ?? string.Empty
    };

    internal string MoneyTransactionId { get; set; } = string.Empty;
    internal string ItemConsumptionOperationId { get; set; } = string.Empty;
    internal string ItemGrantBatchCommitId { get; set; } = string.Empty;
    internal string ItemConsumptionCommitId { get; set; } = string.Empty;
    internal string ItemGrantReceiptId { get; set; } = string.Empty;
    internal string DurableEquipmentStackId { get; private set; } =
        string.Empty;
    internal string DurableEquipmentItemId { get; private set; } =
        string.Empty;
    internal string DurableEquipmentContext { get; private set; } =
        string.Empty;

    internal void SetDurableEquipment(
        DurableFacilityEquipmentUseEvidence equipment)
    {
        if (equipment == null)
            return;
        DurableEquipmentStackId = equipment.StackId;
        DurableEquipmentItemId = equipment.ItemId;
        DurableEquipmentContext = equipment.ToCanonicalString();
    }

    internal string ChildCommitId(V20ContentEffectKind kind) => kind switch
    {
        V20ContentEffectKind.Money => MoneyTransactionId,
        V20ContentEffectKind.ItemConsume => ItemConsumptionOperationId,
        V20ContentEffectKind.ItemGrant => ItemGrantBatchCommitId,
        _ => string.Empty
    };
}
