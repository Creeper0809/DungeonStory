#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEngine;

public static partial class RetailPurchaseRecoveryB0T04DebugScenarios
{
    public static void RunRetailFactObserversForBatch()
    {
        RetailPurchaseFactObserverDebugScenarios.RunOrThrow();
        Debug.Log("B107_RETAIL_PROJECTION_LIFETIME=PASS");
        ShoppingActionObserverDebugScenarios.RunOrThrow();
        RunT03OrThrow();
        RunOrThrow();
        RunBoundContinuationScenarios();
        Debug.Log("B107_RETAIL_EXISTING_EXACT_RECOVERY=PASS");
        ObservationRecovery(false);
        ObservationRecovery(true);
        RetailReentryAndInvalidRestore();
        Debug.Log("B107_RETAIL_FACT_OBSERVERS=PASS;natural-play=NOT-RUN;whole-world=NOT-RUN");
    }

    private static void ObservationRecovery(bool beforeCommit)
    {
        using var context = new Context(1);
        context.BindVisit();
        if (beforeCommit) context.Facts.BeforePublish = () => throw new InvalidOperationException("fixture-response-lost-before-commit");
        else context.Facts.HideNextAcknowledgement = true;
        RequireRetailFailure(context.RunBoundPurchase, "durably pending recovery");
        var pending = context.OnlyPending();
        if (pending.phase != RetailPurchaseRecoveryPhase.PhysicalCommitted || pending.publicationKey.Length == 0
            || pending.publicationId.Length != 0 || pending.publicationFingerprint.Length != 0
            || context.Physical.Commits != 1 || context.PhysicalInstanceExists || context.Money.Credits != 0
            || context.Shopping.HoldingMoney != StartingMoney - Cost)
            throw new InvalidOperationException("Unknown observation ACK did not preserve the exact physical/debit boundary.");
        var native = RoundTrip(context.Fixture.Ledger.CaptureGameplayOutcomes());
        var counters = RoundTrip(context.Fixture.Transaction.Capture());
        var characters = context.CharacterCheckpoint();
        RetailPurchaseFactPreflight.ValidatePending(characters, native, counters);
        int nativeCount = native.outbox.Count + native.exactOutcomes.Count + native.tombstones.Count;
        if (nativeCount != (beforeCommit ? 0 : 1)) throw new InvalidOperationException("Fault did not surround the intended native commit.");
        if (!beforeCommit)
        {
            var tampered = RoundTrip(characters);
            tampered.actors[0].pendingRetailPurchases[0].buyerMoneyBefore++;
            tampered.actors[0].pendingRetailPurchases[0].buyerMoneyAfter++;
            RequireRetailFailure(() => RetailPurchaseFactPreflight.ValidatePending(tampered, native, counters), "canonical-fact-conflict");
            var missing = RoundTrip(native);
            missing.outbox.Clear(); missing.exactOutcomes.Clear(); missing.tombstones.Clear();
            var acknowledged = RoundTrip(characters);
            var row = acknowledged.actors[0].pendingRetailPurchases[0];
            var canonical = native.exactOutcomes.Single();
            row.phase = RetailPurchaseRecoveryPhase.OutcomeCommitted;
            row.publicationId = new GameplayOutcomeId(new GameplayOutcomeRunId(canonical.runId), canonical.sequence).ToString();
            row.publicationFingerprint = canonical.immutablePayloadHash;
            RetailPurchaseFactPreflight.ValidatePending(acknowledged, native, counters);
            RequireRetailFailure(() => RetailPurchaseFactPreflight.ValidatePending(acknowledged, missing, counters), "proof-without-canonical");
            row.publicationFingerprint = new string('0', 64);
            RequireRetailFailure(() => RetailPurchaseFactPreflight.ValidatePending(acknowledged, native, counters), "proof-conflict");
        }
        string json = DungeonCharacterWorldSaveCodec.SerializeFields(characters);
        CharacterWorldSaveSection.ValidateRetailPurchaseJson(json);
        foreach (string field in new[] { "publicationKey", "publicationId", "publicationFingerprint", "absoluteDay" })
        {
            var broken = JObject.Parse(json);
            ((JObject)broken["actors"][0]["pendingRetailPurchases"][0]).Remove(field);
            RequireRetailFailure(() => CharacterWorldSaveSection.ValidateRetailPurchaseJson(broken.ToString()), "json");
        }
        context.ReplaceObservationOwners(native, counters);
        context.RestoreOwners();
        context.Facts.BlockRecovery = true;
        if (context.Shopping.TryRecoverPendingRetailPurchases(context.Shop, out _)
            || context.OnlyPending().publicationKey != pending.publicationKey || context.Physical.Commits != 1)
            throw new InvalidOperationException("Unavailable recovery lost or repeated a purchase.");
        context.Facts.BlockRecovery = false;
        int physicalAttempts = context.Physical.Attempts;
        if (!context.RunBoundPurchase().Committed || context.Physical.Attempts != physicalAttempts
            || context.Money.Credits != Cost || context.Shopping.HoldingMoney != StartingMoney - Cost
            || context.Shopping.CapturePendingRetailPurchases().Count != 0)
            throw new InvalidOperationException("Fresh observation owners did not finish the saved purchase exactly once.");
        var completed = context.Fixture.Ledger.CaptureGameplayOutcomes();
        var exact = completed.exactOutcomes.Single();
        string key = new GameplayResultKey(exact.producerId, new GameplayOperationId(exact.operationId), exact.commitRevision, exact.localResultIndex).ToString();
        if (key != pending.publicationKey || completed.outbox.Count != 0 || completed.tombstones.Count != 0
            || !beforeCommit && (exact.sequence != native.exactOutcomes[0].sequence
                || exact.immutablePayloadHash != native.exactOutcomes[0].immutablePayloadHash))
            throw new InvalidOperationException("Replay changed the saved native identity or payload.");
        context.RestoreOwners();
        int credits = context.Money.CreditAttempts;
        if (!context.RunBoundPurchase().Committed || context.Physical.Attempts != physicalAttempts
            || context.Money.CreditAttempts != credits || context.Fixture.Ledger.CaptureGameplayOutcomes().nextSequence != completed.nextSequence)
            throw new InvalidOperationException("Completed bound replay applied a second effect.");
        Debug.Log("B107_RETAIL_ACK_" + (beforeCommit ? "BEFORE" : "AFTER")
            + "_COMMIT=PASS;save-json=1;fresh-observer-ledger=1;game-owners=restored-in-place");
    }

    private static void RetailReentryAndInvalidRestore()
    {
        using var context = new Context(1);
        context.BindVisit();
        int rejected = 0;
        context.Facts.BeforePublish = () =>
        {
            RequireRetailFailure(() => context.Shopping.CapturePendingRetailPurchases(), "retail-local-transaction-active"); rejected++;
            RequireRetailFailure(() => context.Shopping.RestorePersistentState(0, 0, 0), "retail-local-transaction-active"); rejected++;
            if (context.Shopping.TryRecoverPendingRetailPurchases(context.Shop, out string reason)
                || reason != "retail-local-transaction-active") throw new InvalidOperationException("Nested recovery ran.");
            rejected++;
            RequireRetailFailure(context.RunBoundPurchase, "retail-local-transaction-active"); rejected++;
        };
        context.Money.FailNextCredit = true;
        RequireRetailFailure(context.RunBoundPurchase, "durably pending recovery");
        var pending = context.OnlyPending();
        if (rejected != 4 || pending.phase != RetailPurchaseRecoveryPhase.OutcomeCommitted
            || context.Physical.Commits != 1 || context.Outcomes.CommitCount(OutcomeKind) != 1)
            throw new InvalidOperationException("Reentry changed the purchase owner or prevented its observation.");
        var corrupt = pending.Clone(); corrupt.publicationId = string.Empty;
        RequireRetailFailure(() => context.Shopping.RestorePersistentState(0, 0, 0, new[] { corrupt }), "retail-purchase-publication-phase-invalid");
        if (context.Shopping.HoldingMoney != StartingMoney - Cost || context.OnlyPending().publicationKey != pending.publicationKey)
            throw new InvalidOperationException("Rejected restore partially mutated the live buyer.");
        context.Facts.BeforePublish = null;
        if (!context.RunBoundPurchase().Committed || !context.IsSettled(1, 1, Cost))
            throw new InvalidOperationException("Reentry fence did not release after the synchronous transaction.");
        Debug.Log("B107_RETAIL_REENTRY_ATOMIC_RESTORE=PASS;blocked=4");
    }

    private static T RoundTrip<T>(T source) where T : class => DungeonSaveJson.Deserialize<T>(DungeonCharacterWorldSaveCodec.SerializeFields(source));
    private static void RequireRetailFailure(Action action, string fragment)
    {
        try { action(); }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        { if (error.Message.Contains(fragment)) return; throw; }
        throw new InvalidOperationException("Expected failure missing: " + fragment);
    }
    private static void RequireRetailFailure(Func<BuildingRetailPurchaseCommitResult> action, string fragment) =>
        RequireRetailFailure(() => { action(); }, fragment);

    private sealed partial class Context
    {
        internal DungeonCharacterWorldSaveData CharacterCheckpoint() => RoundTrip(new DungeonCharacterWorldSaveData
        {
            actors = new List<DungeonCharacterSaveData> { new()
            { persistentId = BuyerId, pendingRetailPurchases = Shopping.CapturePendingRetailPurchases().ToList() } }
        });

        internal void ReplaceObservationOwners(GameplayOutcomeLedgerSaveData native, DungeonMigratedProducerOutcomeSaveData counters)
        {
            Facts.Dispose();
            fixture = new MigratedProducerOutcomeEditorFixture(native.runId);
            var candidate = fixture.Ledger.PrepareGameplayOutcomeRestore(native);
            fixture.Ledger.BeginRestoreCandidate(); fixture.Ledger.PublishGameplayOutcomeRestore(candidate);
            fixture.Ledger.PublishRestoreCandidate(); fixture.Ledger.CompleteRestoreCandidate();
            fixture.Transaction.Restore(fixture.Transaction.PrepareRestore(counters));
            Outcomes = new MigratedProducerOutcomeProbeTransaction(fixture.Transaction, fixture.Recorder) { TargetKind = OutcomeKind };
            Facts = new RetailPurchaseFactEditorFixture(fixture, Outcomes);
            Shopping.ConstructRetailPurchaseFacts(Facts.Publisher, Facts.Recovery, new FixedGameSessionStateProvider(7));
        }
    }
}
#endif
