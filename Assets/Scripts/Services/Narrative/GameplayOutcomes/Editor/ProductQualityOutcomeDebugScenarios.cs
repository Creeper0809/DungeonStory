#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEngine;

public static class ProductQualityOutcomeDebugScenarios
{
    public static bool RunAll(bool logSuccess = false)
    {
        EvolutionGameplayOutcomeEditorFixture fixture = new(
            "run:product-quality-editor");
        const string MakerId = "character:qa:product-maker";
        string operationId = CombatEquipmentCraftOutputOutbox.FormatOperationId(
            "combat-craft:qa-product-quality",
            2);
        ProductQualityOutcomeReceipt receipt = new(
            operationId,
            2,
            MakerId,
            "품질 장인",
            "weapon:qa-product-quality",
            CraftsmanshipQualityTier.Masterwork,
            2,
            9,
            rejectedBelowMinimum: false);
        IProductQualityOutcomeCommitter committer = fixture.Bridge;
        Require(
            committer.TryPrepare(
                receipt,
                out PreparedEvolutionOutcome prepared,
                out string prepareFailure),
            "prepare failed: " + prepareFailure);
        Require(
            committer.TryCommit(prepared, 2, out string commitFailure),
            "commit failed: " + commitFailure);

        bool replayPrepared = committer.TryPrepare(
            receipt,
            out PreparedEvolutionOutcome replay,
            out string replayPrepareFailure);
        string replayCommitFailure = string.Empty;
        bool replayCommitted = replayPrepared
            && committer.TryCommit(
                replay,
                2,
                out replayCommitFailure);
        Require(
            replayCommitted,
            "canonical replay failed: "
            + replayPrepareFailure + "/" + replayCommitFailure);

        ProductQualityOutcomeReceipt conflict = new(
            operationId,
            2,
            MakerId,
            "품질 장인",
            "weapon:qa-product-quality",
            CraftsmanshipQualityTier.Normal,
            2,
            9,
            rejectedBelowMinimum: true);
        Require(
            !committer.TryPrepare(conflict, out _, out _),
            "same result key accepted a conflicting quality payload");

        GameplayEntityId maker = new(
            EvolutionOutcomeIds.CharacterKind,
            MakerId);
        GameplayOutcomeQueryPage makerPage = fixture.Ledger.GetForEntity(
            maker,
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        GameplayOutcomeQueryPage operationPage = fixture.Ledger.GetForOperation(
            new GameplayOperationId(operationId));
        Require(
            makerPage.Items.Count == 1
            && operationPage.Items.Count == 1
            && makerPage.Items[0].Exact.sequence
                == operationPage.Items[0].Exact.sequence,
            "maker and operation views did not share one exact outcome");
        GameplayOutcomeSnapshot snapshot = makerPage.Items[0].Exact;
        Require(
            snapshot.outcomeTypeId
                == EvolutionOutcomeIds.ProductQualityResolved.Value
            && snapshot.participants.Count == 1
            && snapshot.subjects.Count == 1
            && snapshot.metrics.Count == 3
            && snapshot.tags.Count == 2
            && snapshot.anchors.Count == 1
            && snapshot.metrics.Any(value =>
                value.metricId
                    == EvolutionOutcomeIds.ProductQualityTierMetric.Value
                && value.value == (int)CraftsmanshipQualityTier.Masterwork)
            && snapshot.metrics.Any(value =>
                value.metricId
                    == EvolutionOutcomeIds.ProductQualityAttemptMetric.Value
                && value.value == 2d)
            && snapshot.metrics.Any(value =>
                value.metricId
                    == EvolutionOutcomeIds.ProductRejectedMetric.Value
                && value.value == 0d),
            "product quality outcome lost its exact typed payload");

        GameplayOutcomeLedgerSaveData saved = fixture.Ledger
            .CaptureGameplayOutcomes();
        EvolutionGameplayOutcomeEditorFixture restored = new(
            "run:product-quality-restore-target");
        restored.Ledger.BeginRestoreCandidate();
        GameplayOutcomeLedgerRestoreCandidate candidate = restored.Ledger
            .PrepareGameplayOutcomeRestore(saved);
        restored.Ledger.PublishGameplayOutcomeRestore(candidate);
        restored.Ledger.PublishRestoreCandidate();
        restored.Ledger.CompleteRestoreCandidate();
        GameplayOutcomeQueryPage restoredPage = restored.Ledger.GetForEntity(
            maker,
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        Require(
            restoredPage.Items.Count == 1
            && restoredPage.Items[0].Exact.immutablePayloadHash
                == snapshot.immutablePayloadHash,
            "product quality outcome changed across ledger save round trip");

        if (logSuccess)
            Debug.Log("[Product Quality Outcome] PASS");
        return true;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
