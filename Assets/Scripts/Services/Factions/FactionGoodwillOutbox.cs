using System;
using System.Linq;
using DungeonStory.Factions;

public static class FactionGoodwillOutbox
{
    public const string TransferReason =
        "faction-goodwill-goods-transfer";

    public static string FormatOperationId(
        string factionId,
        int sequence) =>
        $"faction-goodwill:{factionId}:{sequence:D8}";

    public static bool HasProvenance(DungeonFactionState faction) =>
        faction != null
        && !string.IsNullOrEmpty(faction.goodwillTransferOperationId);

    public static void RecordPending(
        DungeonFactionState faction,
        int sequence,
        PhysicalItemBatchDispositionReceipt receipt,
        int transferredPhysicalValue,
        int campaignRapportTarget)
    {
        if (faction == null)
        {
            throw new ArgumentNullException(nameof(faction));
        }
        if (sequence <= 0
            || !receipt.IsCommitted
            || receipt.Kind != PhysicalItemDispositionKind.Transfer
            || !string.Equals(
                receipt.OperationId,
                FormatOperationId(faction.factionId, sequence),
                StringComparison.Ordinal)
            || !string.Equals(
                receipt.ReasonCode,
                TransferReason,
                StringComparison.Ordinal)
            || transferredPhysicalValue < 50
            || campaignRapportTarget is < -100 or > 100)
        {
            throw new InvalidOperationException(
                "Faction goodwill transfer receipt is not canonical.");
        }

        faction.goodwillTransferSequence = sequence;
        faction.goodwillTransferOperationId = receipt.OperationId;
        faction.goodwillTransferCommitId = receipt.CommitId;
        faction.goodwillTransferSourceStackIds = receipt.SourceStackIds
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        faction.goodwillTransferQuantity = receipt.Quantity;
        faction.goodwillTransferMassGrams = receipt.InputMassGrams;
        faction.goodwillTransferredPhysicalValue = transferredPhysicalValue;
        faction.goodwillCampaignRapportTarget = campaignRapportTarget;
        faction.goodwillTransferCompleted = false;
    }

    public static bool TryFinalizePending(
        DungeonFactionState faction,
        IPhysicalItemBatchDispositionService batchDispositions,
        IFactionCampaignQuery campaignQuery,
        IFactionCampaignCommand campaignCommand,
        Action<DungeonFactionState> acceptGoodwill,
        out bool domainAppliedNow,
        out string failureReason)
    {
        domainAppliedNow = false;
        failureReason = string.Empty;
        if (faction == null
            || batchDispositions == null
            || campaignQuery == null
            || campaignCommand == null
            || acceptGoodwill == null
            || !HasProvenance(faction)
            || faction.goodwillTransferSequence <= 0
            || !string.Equals(
                faction.goodwillTransferOperationId,
                FormatOperationId(
                    faction.factionId,
                    faction.goodwillTransferSequence),
                StringComparison.Ordinal)
            || faction.goodwillTransferSourceStackIds == null
            || faction.goodwillTransferSourceStackIds.Count == 0
            || faction.goodwillTransferQuantity <= 0
            || faction.goodwillTransferMassGrams <= 0L
            || faction.goodwillTransferredPhysicalValue < 50
            || faction.goodwillCampaignRapportTarget is < -100 or > 100)
        {
            failureReason = "faction-goodwill-outbox-invalid";
            return false;
        }

        bool pending = batchDispositions.TryGetPending(
            faction.goodwillTransferOperationId,
            out PhysicalItemBatchDispositionReceipt receipt);
        if (pending && !ReceiptMatches(faction, receipt))
        {
            failureReason = "faction-goodwill-outbox-receipt-mismatch";
            return false;
        }
        if (faction.goodwillTransferCompleted)
        {
            if (!faction.discovered
                || !TryCampaignAtOrAboveTarget(
                    faction,
                    campaignQuery,
                    out failureReason))
            {
                return false;
            }
            return !pending || batchDispositions.Acknowledge(
                faction.goodwillTransferCommitId,
                out failureReason);
        }
        if (!pending)
        {
            failureReason = "faction-goodwill-outbox-receipt-missing";
            return false;
        }

        if (!faction.discovered)
        {
            acceptGoodwill(faction);
        }
        if (!faction.discovered)
        {
            failureReason = "faction-goodwill-outbox-domain-not-applied";
            return false;
        }

        if (!campaignQuery.TryGetFaction(
                faction.factionId,
                out FactionCampaignStateSaveData campaignState)
            || campaignState == null)
        {
            failureReason = "faction-goodwill-outbox-campaign-missing";
            return false;
        }
        int target = faction.goodwillCampaignRapportTarget;
        if (campaignState.rapport < target)
        {
            campaignCommand.ApplyFactionChange(
                faction.factionId,
                rapportDelta: target - campaignState.rapport,
                grievanceDelta: 0,
                obligationDelta: 0);
            domainAppliedNow = true;
        }
        if (!TryCampaignAtOrAboveTarget(
                faction,
                campaignQuery,
                out failureReason))
        {
            return false;
        }

        faction.goodwillTransferCompleted = true;
        if (!batchDispositions.Acknowledge(
                faction.goodwillTransferCommitId,
                out failureReason))
        {
            faction.goodwillTransferCompleted = false;
            return false;
        }
        return true;
    }

    public static void ClearCompleted(DungeonFactionState faction)
    {
        if (faction == null || !faction.goodwillTransferCompleted)
        {
            throw new InvalidOperationException(
                "Only a completed goodwill transfer can be cleared.");
        }
        faction.goodwillTransferSequence = 0;
        faction.goodwillTransferOperationId = string.Empty;
        faction.goodwillTransferCommitId = string.Empty;
        faction.goodwillTransferSourceStackIds.Clear();
        faction.goodwillTransferQuantity = 0;
        faction.goodwillTransferMassGrams = 0L;
        faction.goodwillTransferredPhysicalValue = 0;
        faction.goodwillCampaignRapportTarget = 0;
        faction.goodwillTransferCompleted = false;
    }

    private static bool TryCampaignAtOrAboveTarget(
        DungeonFactionState faction,
        IFactionCampaignQuery campaignQuery,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (!campaignQuery.TryGetFaction(
                faction.factionId,
                out FactionCampaignStateSaveData campaignState)
            || campaignState == null
            || campaignState.rapport
                < faction.goodwillCampaignRapportTarget)
        {
            failureReason =
                "faction-goodwill-outbox-campaign-target-mismatch";
            return false;
        }
        return true;
    }

    private static bool ReceiptMatches(
        DungeonFactionState faction,
        PhysicalItemBatchDispositionReceipt receipt) =>
        receipt.IsCommitted
        && receipt.Kind == PhysicalItemDispositionKind.Transfer
        && string.Equals(
            receipt.OperationId,
            faction.goodwillTransferOperationId,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.ReasonCode,
            TransferReason,
            StringComparison.Ordinal)
        && string.Equals(
            receipt.CommitId,
            faction.goodwillTransferCommitId,
            StringComparison.Ordinal)
        && receipt.Quantity == faction.goodwillTransferQuantity
        && receipt.InputMassGrams == faction.goodwillTransferMassGrams
        && receipt.SourceStackIds.OrderBy(
                value => value,
                StringComparer.Ordinal)
            .SequenceEqual(
                faction.goodwillTransferSourceStackIds,
                StringComparer.Ordinal);
}
