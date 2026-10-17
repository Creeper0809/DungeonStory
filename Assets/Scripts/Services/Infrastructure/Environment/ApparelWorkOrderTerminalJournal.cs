using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

using static ApparelWorkOrderSnapshots;

/// <summary>
/// Owns terminal receipt publication/replay and checkpoint-GC candidate lifetime.
/// Live order removal, receipts and revision still commit in the shared state.
/// </summary>
internal sealed class ApparelWorkOrderTerminalJournal
{
    private readonly ApparelWorkOrderAuthorityStore authorityStore;
    private TerminalStateCheckpointGcCandidate activeTerminalStateGcCandidate;

    internal ApparelWorkOrderTerminalJournal(ApparelWorkOrderAuthorityStore authorityStore)
    {
        this.authorityStore = authorityStore;
    }

    private ApparelWorkOrderAuthorityState authority
    {
        get => authorityStore.Current;
        set => authorityStore.Replace(value);
    }
    private List<ApparelWorkOrderSaveData> orders => authority.Orders;
    private Dictionary<string, ApparelWorkOrderTerminalStateSaveData> terminalStates =>
        authority.TerminalStates;
    private int Version => authority.Version;
    internal bool HasTerminalStates => terminalStates.Count != 0;

    internal ApparelWorkOrderTerminalStateSaveData[] CaptureTerminalStates() =>
        terminalStates.Values
            .OrderBy(value => value.sourceOrder.orderId, StringComparer.Ordinal)
            .Select(value => value.Clone())
            .ToArray();

    internal IReadOnlyList<ProductionApparelOrderTerminalEffectReceipt>
        CaptureTerminalEffectReceipts() => terminalStates.Values
        .Select(value => value.terminalEffectReceipt)
        .Where(value => value != null)
        .OrderBy(value => value.commitId, StringComparer.Ordinal)
        .Select(value => value.Clone())
        .ToArray();

    internal bool TryCaptureTerminalEffectReceipt(
        string commitId,
        out ProductionApparelOrderTerminalEffectReceipt receipt)
    {
        receipt = null;
        ProductionApparelOrderTerminalEffectReceipt[] matches = terminalStates
            .Values.Select(value => value.terminalEffectReceipt)
            .Where(value => value != null && string.Equals(
                value.commitId,
                commitId,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
            return false;
        receipt = matches[0].Clone();
        return true;
    }

    internal bool TryCaptureLiveOrder(
        string orderId,
        out ApparelWorkOrderSaveData sourceOrder,
        out string failureReason)
    {
        sourceOrder = null;
        failureReason = string.Empty;
        ApparelWorkOrderSaveData[] matches = orders.Where(value => value != null
                && value.state != ApparelWorkOrderState.Completed
                && string.Equals(value.orderId, orderId, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
        {
            failureReason = matches.Length == 0
                ? "production-apparel-terminal-source-order-missing"
                : "production-apparel-terminal-source-order-duplicate";
            return false;
        }
        sourceOrder = Clone(matches[0]);
        return true;
    }

    internal IReadOnlyList<ProductionApparelOrderSourceTerminalReceipt>
        CaptureSourceTerminalReceipts() => terminalStates.Values
        .Select(value => value.sourceTerminalReceipt)
        .Where(value => value != null)
        .OrderBy(value => value.commitId, StringComparer.Ordinal)
        .Select(value => value.Clone())
        .ToArray();

    internal bool TryCaptureSourceTerminalReceipt(
        string commitId,
        out ProductionApparelOrderSourceTerminalReceipt receipt)
    {
        receipt = null;
        ProductionApparelOrderSourceTerminalReceipt[] matches = terminalStates
            .Values.Select(value => value.sourceTerminalReceipt)
            .Where(value => value != null && string.Equals(
                value.commitId,
                commitId,
                StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1)
            return false;
        receipt = matches[0].Clone();
        return true;
    }

    internal bool TryPrepareCheckpointGarbageCollection(
        IReadOnlyList<ProductionApparelOrderTerminalDrainSaveData> producers,
        out IProductionApparelTerminalStateCheckpointGcCandidate candidate,
        out string failureReason)
    {
        candidate = null;
        failureReason = string.Empty;
        if (activeTerminalStateGcCandidate != null)
        {
            failureReason =
                "production-apparel-terminal-state-gc-already-active";
            return false;
        }

        ProductionApparelOrderTerminalDrainSaveData[] ordered = (producers
                ?? Array.Empty<ProductionApparelOrderTerminalDrainSaveData>())
            .OrderBy(value => value?.orderId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(value => value == null)
            || ordered.Select(value => value.orderId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            failureReason =
                "production-apparel-terminal-state-gc-producer-invalid";
            return false;
        }

        List<ApparelWorkOrderTerminalStateSaveData> rows = new(ordered.Length);
        foreach (ProductionApparelOrderTerminalDrainSaveData producer in ordered)
        {
            if (producer.phase != ProductionApparelOrderTerminalDrainPhase
                    .OwnerAcknowledgedAwaitingCheckpointGc
                || producer.terminalEffectReceipt == null
                || producer.sourceTerminalReceipt == null
                || !terminalStates.TryGetValue(
                    producer.orderId,
                    out ApparelWorkOrderTerminalStateSaveData row)
                || orders.Any(order => order != null
                    && string.Equals(
                        order.orderId,
                        producer.orderId,
                        StringComparison.Ordinal))
                || !TerminalStateMatchesProducer(row, producer))
            {
                failureReason =
                    "production-apparel-terminal-state-gc-row-missing-or-conflicting:"
                    + (producer.orderId ?? string.Empty);
                return false;
            }
            rows.Add(row.Clone());
        }

        activeTerminalStateGcCandidate = new TerminalStateCheckpointGcCandidate(
            Version,
            rows);
        candidate = activeTerminalStateGcCandidate;
        return true;
    }

    internal bool TryPublishCheckpointGarbageCollection(
        IProductionApparelTerminalStateCheckpointGcCandidate candidate,
        out string failureReason)
    {
        failureReason = string.Empty;
        TerminalStateCheckpointGcCandidate exact = RequireTerminalStateGcCandidate(
            candidate);
        if (exact.Published)
            return true;
        if (Version != exact.ExpectedVersion
            || exact.Rows.Any(row =>
                !terminalStates.TryGetValue(
                    row.sourceOrder.orderId,
                    out ApparelWorkOrderTerminalStateSaveData current)
                || !TerminalStateEquals(current, row)))
        {
            failureReason =
                "production-apparel-terminal-state-gc-live-authority-changed";
            return false;
        }

        if (exact.Rows.Count > 0)
        {
            ApparelWorkOrderAuthorityState next = authority.Clone();
            foreach (ApparelWorkOrderTerminalStateSaveData row in exact.Rows)
                next.TerminalStates.Remove(row.sourceOrder.orderId);
            next.Version = checked(next.Version + 1);
            authority = next;
        }
        exact.Published = true;
        exact.PublishedVersion = Version;
        return true;
    }

    internal void RollbackCheckpointGarbageCollection(
        IProductionApparelTerminalStateCheckpointGcCandidate candidate)
    {
        TerminalStateCheckpointGcCandidate exact = RequireTerminalStateGcCandidate(
            candidate);
        if (!exact.Published)
            return;
        if (Version != exact.PublishedVersion
            || exact.Rows.Any(row => terminalStates.ContainsKey(
                row.sourceOrder.orderId)))
        {
            throw new InvalidOperationException(
                "Production apparel terminal-state GC rollback observed authority drift.");
        }

        if (exact.Rows.Count > 0)
        {
            ApparelWorkOrderAuthorityState restored = authority.Clone();
            foreach (ApparelWorkOrderTerminalStateSaveData row in exact.Rows)
            {
                restored.TerminalStates.Add(
                    row.sourceOrder.orderId,
                    row.Clone());
            }
            restored.Version = exact.ExpectedVersion;
            authority = restored;
        }
        exact.Published = false;
        exact.PublishedVersion = exact.ExpectedVersion;
    }

    internal void CompleteCheckpointGarbageCollection(
        IProductionApparelTerminalStateCheckpointGcCandidate candidate)
    {
        RequireTerminalStateGcCandidate(candidate);
        activeTerminalStateGcCandidate = null;
    }

    [GameplayInternalOnly(
        "Publishes one apparel terminal-effect receipt in the order aggregate before source removal.",
        "Apparel destructive terminal drain producer only")]
    internal ProductionApparelOrderTerminalEffectApplyResult TryCommitTerminalEffect(
        ProductionApparelOrderTerminalEffectReceipt expectedReceipt,
        ProductionApparelOrderPendingEffectIdentity pendingEffect)
    {
        string sourceFailure = string.Empty;
        if (expectedReceipt == null
            || !TryCaptureLiveOrder(
                expectedReceipt.orderId,
                out ApparelWorkOrderSaveData source,
                out sourceFailure))
        {
            return EffectConflict(
                "production-apparel-terminal-effect-source-conflict:"
                + sourceFailure);
        }
        string sourceFingerprint =
            ProductionApparelOrderTerminalDrainCanonical
                .CreateSourceOrderFingerprint(source);
        string effectFailure = string.Empty;
        if (!ProductionApparelOrderTerminalDrainCanonical
                .TryCreatePendingEffectIdentity(
                    source,
                    out ProductionApparelOrderPendingEffectIdentity actualEffect,
                    out effectFailure)
            || !PendingEffectEquals(actualEffect, pendingEffect)
            || !ProductionApparelOrderTerminalDrainCanonical.EffectReceiptEquals(
                expectedReceipt,
                ProductionApparelOrderTerminalDrainCanonical
                    .CreateTerminalEffectReceipt(
                        expectedReceipt.stepOperationId,
                        source,
                        sourceFingerprint,
                        actualEffect)))
        {
            return EffectConflict(
                "production-apparel-terminal-effect-request-conflict:"
                + effectFailure);
        }

        if (terminalStates.TryGetValue(
                source.orderId,
                out ApparelWorkOrderTerminalStateSaveData existing))
        {
            return ProductionApparelOrderTerminalDrainCanonical
                .EffectReceiptEquals(
                    existing.terminalEffectReceipt,
                    expectedReceipt)
                && PendingEffectEquals(existing.pendingEffect, actualEffect)
                ? new ProductionApparelOrderTerminalEffectApplyResult(
                    ProductionApparelOrderTerminalDrainStatus.Replay,
                    existing.terminalEffectReceipt,
                    string.Empty)
                : EffectConflict(
                    "production-apparel-terminal-effect-row-conflict");
        }
        if (terminalStates.Values.Any(value => string.Equals(
                value.terminalEffectReceipt?.commitId,
                expectedReceipt.commitId,
                StringComparison.Ordinal)))
        {
            return EffectConflict(
                "production-apparel-terminal-effect-commit-duplicate");
        }

        ApparelWorkOrderAuthorityState next = authority.Clone();
        next.TerminalStates.Add(source.orderId, new()
        {
            sourceOrder = Clone(source),
            sourceOrderFingerprint = sourceFingerprint,
            pendingEffect = actualEffect?.Clone(),
            terminalEffectReceipt = expectedReceipt.Clone()
        });
        next.Version = checked(next.Version + 1);
        authority = next;
        return new ProductionApparelOrderTerminalEffectApplyResult(
            ProductionApparelOrderTerminalDrainStatus.Applied,
            expectedReceipt,
            string.Empty);
    }

    [GameplayInternalOnly(
        "Removes one exact frozen apparel order and publishes its source-terminal receipt in one authority swap.",
        "Apparel destructive terminal drain producer only")]
    internal ProductionApparelOrderSourceTerminalApplyResult TryCommitSourceTerminal(
        ProductionApparelOrderSourceTerminalReceipt expectedReceipt)
    {
        if (expectedReceipt == null
            || !terminalStates.TryGetValue(
                expectedReceipt.orderId,
                out ApparelWorkOrderTerminalStateSaveData terminal)
            || terminal.terminalEffectReceipt == null)
        {
            return SourceConflict(
                "production-apparel-source-terminal-effect-missing");
        }
        ProductionApparelOrderSourceTerminalReceipt canonical =
            ProductionApparelOrderTerminalDrainCanonical
                .CreateSourceTerminalReceipt(
                    expectedReceipt.stepOperationId,
                    terminal.sourceOrder,
                    terminal.sourceOrderFingerprint,
                    terminal.terminalEffectReceipt.receiptFingerprint);
        if (!ProductionApparelOrderTerminalDrainCanonical.SourceReceiptEquals(
                expectedReceipt,
                canonical))
        {
            return SourceConflict(
                "production-apparel-source-terminal-request-conflict");
        }
        ApparelWorkOrderSaveData[] live = orders.Where(value => value != null
                && string.Equals(
                    value.orderId,
                    expectedReceipt.orderId,
                    StringComparison.Ordinal))
            .ToArray();
        if (terminal.sourceTerminalReceipt != null)
        {
            return live.Length == 0
                && ProductionApparelOrderTerminalDrainCanonical
                    .SourceReceiptEquals(
                        terminal.sourceTerminalReceipt,
                        expectedReceipt)
                ? new ProductionApparelOrderSourceTerminalApplyResult(
                    ProductionApparelOrderTerminalDrainStatus.Replay,
                    terminal.sourceTerminalReceipt,
                    string.Empty)
                : SourceConflict(
                    "production-apparel-source-terminal-replay-conflict");
        }
        if (live.Length != 1
            || !string.Equals(
                ProductionApparelOrderTerminalDrainCanonical
                    .CreateSourceOrderFingerprint(live[0]),
                terminal.sourceOrderFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                JsonUtility.ToJson(live[0]),
                JsonUtility.ToJson(terminal.sourceOrder),
                StringComparison.Ordinal)
            || terminalStates.Values.Any(value => string.Equals(
                value.sourceTerminalReceipt?.commitId,
                expectedReceipt.commitId,
                StringComparison.Ordinal)))
        {
            return SourceConflict(
                "production-apparel-source-terminal-live-source-conflict");
        }

        ApparelWorkOrderAuthorityState next = authority.Clone();
        next.Orders.RemoveAll(value => string.Equals(
            value.orderId,
            expectedReceipt.orderId,
            StringComparison.Ordinal));
        next.TerminalStates[expectedReceipt.orderId].sourceTerminalReceipt =
            expectedReceipt.Clone();
        next.Version = checked(next.Version + 1);
        authority = next;
        return new ProductionApparelOrderSourceTerminalApplyResult(
            ProductionApparelOrderTerminalDrainStatus.Applied,
            expectedReceipt,
            string.Empty);
    }

    private static ProductionApparelOrderTerminalEffectApplyResult
        EffectConflict(string failureReason) => new(
            ProductionApparelOrderTerminalDrainStatus.Conflict,
            null,
            failureReason);

    private static ProductionApparelOrderSourceTerminalApplyResult
        SourceConflict(string failureReason) => new(
            ProductionApparelOrderTerminalDrainStatus.Conflict,
            null,
            failureReason);

    private static bool TerminalStateMatchesProducer(
        ApparelWorkOrderTerminalStateSaveData row,
        ProductionApparelOrderTerminalDrainSaveData producer) => row != null
        && producer != null
        && row.schemaVersion ==
            ApparelWorkOrderTerminalStateSaveData.CurrentSchemaVersion
        && row.sourceOrder != null
        && string.Equals(
            row.sourceOrderFingerprint,
            producer.sourceOrderFingerprint,
            StringComparison.Ordinal)
        && string.Equals(
            JsonUtility.ToJson(row.sourceOrder),
            JsonUtility.ToJson(producer.sourceOrder),
            StringComparison.Ordinal)
        && PendingEffectEquals(row.pendingEffect, producer.pendingEffect)
        && ProductionApparelOrderTerminalDrainCanonical.EffectReceiptEquals(
            row.terminalEffectReceipt,
            producer.terminalEffectReceipt)
        && ProductionApparelOrderTerminalDrainCanonical.SourceReceiptEquals(
            row.sourceTerminalReceipt,
            producer.sourceTerminalReceipt);

    private static bool TerminalStateEquals(
        ApparelWorkOrderTerminalStateSaveData left,
        ApparelWorkOrderTerminalStateSaveData right) => left != null
        && right != null
        && string.Equals(
            JsonUtility.ToJson(left),
            JsonUtility.ToJson(right),
            StringComparison.Ordinal);

    private TerminalStateCheckpointGcCandidate RequireTerminalStateGcCandidate(
        IProductionApparelTerminalStateCheckpointGcCandidate candidate)
    {
        if (candidate is not TerminalStateCheckpointGcCandidate exact
            || !ReferenceEquals(activeTerminalStateGcCandidate, exact))
        {
            throw new InvalidOperationException(
                "Apparel terminal-state checkpoint GC candidate is stale or foreign.");
        }
        return exact;
    }

    private sealed class TerminalStateCheckpointGcCandidate :
        IProductionApparelTerminalStateCheckpointGcCandidate
    {
        internal TerminalStateCheckpointGcCandidate(
            int expectedVersion,
            IReadOnlyList<ApparelWorkOrderTerminalStateSaveData> rows)
        {
            ExpectedVersion = expectedVersion;
            Rows = (rows
                    ?? Array.Empty<ApparelWorkOrderTerminalStateSaveData>())
                .Select(value => value.Clone())
                .OrderBy(
                    value => value.sourceOrder.orderId,
                    StringComparer.Ordinal)
                .ToArray();
            PublishedVersion = expectedVersion;
        }

        internal int ExpectedVersion { get; }
        internal IReadOnlyList<ApparelWorkOrderTerminalStateSaveData> Rows
            { get; }
        internal bool Published { get; set; }
        internal int PublishedVersion { get; set; }
    }
}
