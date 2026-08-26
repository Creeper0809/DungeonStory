using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public readonly struct PackagedLotTareOutputReceipt
{
    internal PackagedLotTareOutputReceipt(
        string parentCommitId,
        int outputQuantity,
        long outputMassGrams,
        long destroyedTareMassGrams,
        IReadOnlyList<string> outputCommitIds)
    {
        ParentCommitId = parentCommitId ?? string.Empty;
        OutputQuantity = outputQuantity;
        OutputMassGrams = outputMassGrams;
        DestroyedTareMassGrams = destroyedTareMassGrams;
        OutputCommitIds = outputCommitIds ?? Array.Empty<string>();
    }

    public string ParentCommitId { get; }
    public int OutputQuantity { get; }
    public long OutputMassGrams { get; }
    public long DestroyedTareMassGrams { get; }
    public long AccountedTareMassGrams => checked(
        OutputMassGrams + DestroyedTareMassGrams);
    public IReadOnlyList<string> OutputCommitIds { get; }
}

public interface IPackagedLotTareDispositionService
{
    bool EnsureTerminalSinkOutputs(
        IReadOnlyDictionary<string, int> consumedItems,
        Vector2Int outputPosition,
        string parentCommitId,
        out PackagedLotTareOutputReceipt receipt,
        out string failureReason);
}

public interface IPackagedLotTareOutputGateway
{
    IReadOnlyList<WorldItemStackSnapshot> GetAllStacks();

    bool SpawnOutput(
        string itemId,
        int quantity,
        Vector2Int position,
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        out int spawned);
}

public sealed class PackagedLotTareOutputGateway :
    IPackagedLotTareOutputGateway
{
    private readonly IEquipmentPhysicalItemGateway items;

    public PackagedLotTareOutputGateway(IEquipmentPhysicalItemGateway items)
    {
        this.items = items ?? throw new ArgumentNullException(nameof(items));
    }

    public IReadOnlyList<WorldItemStackSnapshot> GetAllStacks() =>
        items.GetAllStacks();

    public bool SpawnOutput(
        string itemId,
        int quantity,
        Vector2Int position,
        IReadOnlyList<ItemInstanceComponentSaveData> components,
        out int spawned) =>
        items.SpawnItemAtWithComponents(
            itemId,
            quantity,
            position,
            WorldItemStackState.Loose,
            string.Empty,
            components,
            out spawned);
}

/// <summary>
/// Publishes the physical tare owed by a committed terminal Sink. The parent
/// disposition remains the custody authority; output commit components make
/// replay after acknowledgement failure or restore idempotent.
/// </summary>
public sealed class PackagedLotTareDispositionService :
    IPackagedLotTareDispositionService
{
    private readonly IPackagedLotDefinitionQuery packagedLots;
    private readonly IPackagedLotTareOutputGateway outputs;

    public PackagedLotTareDispositionService(
        IPackagedLotDefinitionQuery packagedLots,
        IPackagedLotTareOutputGateway outputs)
    {
        this.packagedLots = packagedLots
            ?? throw new ArgumentNullException(nameof(packagedLots));
        this.outputs = outputs ?? throw new ArgumentNullException(nameof(outputs));
    }

    public bool EnsureTerminalSinkOutputs(
        IReadOnlyDictionary<string, int> consumedItems,
        Vector2Int outputPosition,
        string parentCommitId,
        out PackagedLotTareOutputReceipt receipt,
        out string failureReason)
    {
        receipt = default;
        failureReason = string.Empty;
        string parent = parentCommitId ?? string.Empty;
        if (parent.Length == 0
            || !string.Equals(parent, parent.Trim(), StringComparison.Ordinal))
        {
            failureReason = "packaged-lot-parent-commit-invalid";
            return false;
        }

        if (consumedItems == null)
        {
            failureReason = "packaged-lot-consumed-items-missing";
            return false;
        }

        Dictionary<string, OutputPlan> plannedOutputs = new(StringComparer.Ordinal);
        long destroyedTareMassGrams = 0L;
        foreach (KeyValuePair<string, int> consumed in consumedItems
                 .Where(value => value.Value > 0)
                 .OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            string itemId = consumed.Key ?? string.Empty;
            if (itemId.Length == 0
                || !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal))
            {
                failureReason = "packaged-lot-consumed-item-invalid";
                return false;
            }
            if (!packagedLots.TryGetPackagedLot(
                    (ItemDefinitionId)itemId,
                    out PackagedLotDefinitionSnapshot packagedLot))
            {
                continue;
            }

            if (packagedLot.TareDisposition is PackageTareDisposition.DestroyedDuringUse)
            {
                destroyedTareMassGrams = checked(
                    destroyedTareMassGrams
                    + packagedLot.TareMass.Value * consumed.Value);
                continue;
            }
            if (packagedLot.TareDisposition is PackageTareDisposition.TransferredWithOutput)
            {
                failureReason = "packaged-lot-transferred-tare-invalid-for-terminal-sink:" + itemId;
                return false;
            }
            if (packagedLot.TareDisposition is not (
                    PackageTareDisposition.ReusableContainerReturn
                    or PackageTareDisposition.DisposableWasteByproduct))
            {
                continue;
            }

            string outputItemId = packagedLot.ContainerItemId.Value;
            if (!plannedOutputs.TryGetValue(outputItemId, out OutputPlan output))
            {
                output = new OutputPlan(outputItemId);
                plannedOutputs.Add(outputItemId, output);
            }
            output.Quantity = checked(output.Quantity + consumed.Value);
            output.MassGrams = checked(output.MassGrams
                + packagedLot.TareMass.Value * consumed.Value);
        }

        int outputQuantity = 0;
        long outputMassGrams = 0L;
        List<string> outputCommitIds = new(plannedOutputs.Count);
        foreach (OutputPlan output in plannedOutputs.Values
                     .OrderBy(value => value.ItemId, StringComparer.Ordinal))
        {
            string outputCommitId = parent + ":tare:" + output.ItemId;
            WorldItemStackSnapshot[] existing = outputs.GetAllStacks()
                .Where(stack => stack != null
                    && string.Equals(
                        stack.ItemId,
                        output.ItemId,
                        StringComparison.Ordinal)
                    && ProductionOutputCommitComponentCodec.Matches(
                        stack.Components,
                        outputCommitId))
                .ToArray();
            if (existing.Length > 1
                || (existing.Length == 1
                    && (existing[0].Quantity != output.Quantity
                        || existing[0].State != WorldItemStackState.Loose
                        || existing[0].Position != outputPosition)))
            {
                failureReason = "packaged-lot-tare-output-conflict:" + output.ItemId;
                return false;
            }
            if (existing.Length == 0
                && (!outputs.SpawnOutput(
                        output.ItemId,
                        output.Quantity,
                        outputPosition,
                        new[]
                        {
                            ProductionOutputCommitComponentCodec.Create(
                                outputCommitId)
                        },
                        out int spawned)
                    || spawned != output.Quantity))
            {
                failureReason = "packaged-lot-tare-output-failed:" + output.ItemId;
                return false;
            }

            outputQuantity = checked(outputQuantity + output.Quantity);
            outputMassGrams = checked(outputMassGrams + output.MassGrams);
            outputCommitIds.Add(outputCommitId);
        }

        receipt = new PackagedLotTareOutputReceipt(
            parent,
            outputQuantity,
            outputMassGrams,
            destroyedTareMassGrams,
            outputCommitIds.AsReadOnly());
        return true;
    }

    private sealed class OutputPlan
    {
        internal OutputPlan(string itemId)
        {
            ItemId = itemId;
        }

        internal string ItemId { get; }
        internal int Quantity { get; set; }
        internal long MassGrams { get; set; }
    }
}
