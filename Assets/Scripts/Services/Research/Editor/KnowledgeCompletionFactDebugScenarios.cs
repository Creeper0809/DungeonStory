#if UNITY_EDITOR
using System;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

/// <summary>Synthetic fact/observer checks; no natural research task or physical mutation is run.</summary>
public static class KnowledgeCompletionFactDebugScenarios
{
    public static string Run()
    {
        VerifyFactContract();
        VerifyObserverContract();
        return "knowledge-completion-fact/codex-recon/clone/snapshot/validation/cancel/commit/replay/dedup/canonical-rejection=PASS;natural-research-and-physical-effects=NOT-RUN";
    }

    private static void VerifyFactContract()
    {
        KnowledgeResidueCompletedFact codex = Fact(
            KnowledgeResidueUse.CodexAnalysis,
            "codex",
            ownerRevision: 11,
            rewardBefore: 2f,
            rewardAfter: 3f);
        KnowledgeResidueCompletedFact recon = Fact(
            KnowledgeResidueUse.RegionReconnaissance,
            "recon",
            ownerRevision: 12,
            rewardBefore: 95f,
            rewardAfter: 100f);
        KnowledgeCompletionOutcomeReceipt codexReceipt =
            KnowledgeCompletionFactObservation.Capture(codex);
        KnowledgeCompletionOutcomeReceipt reconReceipt =
            KnowledgeCompletionFactObservation.Capture(recon);
        Require(codexReceipt.TaskLabel == "도감 단서 분석"
                && reconReceipt.TaskLabel == "지역 기억 정찰"
                && codexReceipt.ResultKey.OperationId.Value
                    == codex.Physical.Receipt.OperationId
                && reconReceipt.ResultKey.CommitRevision == 12,
            "Knowledge fact mapping lost its canonical identity or task label.");

        KnowledgeCompletionFactData snapshot = codex.Snapshot();
        KnowledgeCompletionFactData clone = snapshot.Clone();
        clone.experience.facilityName = "변경된 시설";
        clone.physical.sourceStackIds[0] = "stack:changed";
        clone.physical.sourceFacts[0].displayText = "변경된 재료";
        Require(snapshot.experience.facilityName == "합성 연구실"
                && snapshot.physical.sourceStackIds[0] == "stack:knowledge-codex"
                && snapshot.physical.sourceFacts[0].displayText == "기억 잔재",
            "Knowledge fact DTO clone shared mutable state.");

        KnowledgeResidueCompletedFact restored =
            KnowledgeResidueCompletedFact.FromSave(snapshot);
        Require(JsonUtility.ToJson(restored.Snapshot()) == JsonUtility.ToJson(snapshot),
            "Knowledge fact snapshot did not round-trip exactly.");

        KnowledgeCompletionFactData changedPhysical = snapshot.Clone();
        changedPhysical.physical.publicationExpected = true;
        RequireRejected(
            () => KnowledgeResidueCompletedFact.FromSave(changedPhysical),
            "Non-canonical physical publication metadata was accepted.");
        RequireRejected(
            () => new KnowledgeResidueCompletedFact(
                Physical("wrong-source", 13, "item:not-memory-residue"),
                Experience(KnowledgeResidueUse.CodexAnalysis, 3, 0f, 1f)),
            "A non-residue physical source was accepted.");
        RequireRejected(
            () => new KnowledgeResidueCompletedFact(
                Physical("bad-day", 14),
                Experience(KnowledgeResidueUse.CodexAnalysis, 0, 0f, 1f)),
            "A non-positive knowledge observation day was accepted.");
        RequireRejected(
            () => new KnowledgeResidueCompletedFact(
                Physical("nan-reward", 15),
                Experience(KnowledgeResidueUse.CodexAnalysis, 3,
                    float.NaN, 1f)),
            "A non-finite knowledge reward was accepted.");
        RequireRejected(
            () => new KnowledgeResidueCompletedFact(
                Physical("bad-codex-reward", 16),
                Experience(KnowledgeResidueUse.CodexAnalysis, 3, 0f, 2f)),
            "A non-canonical Codex reward delta was accepted.");
        RequireRejected(
            () => new KnowledgeResidueCompletedFact(
                Physical("bad-recon-reward", 17),
                Experience(KnowledgeResidueUse.RegionReconnaissance, 3,
                    95f, 99f)),
            "A non-canonical reconnaissance reward delta was accepted.");
    }

    private static void VerifyObserverContract()
    {
        KnowledgeResidueCompletedFact codex = Fact(
            KnowledgeResidueUse.CodexAnalysis,
            "observer-codex",
            ownerRevision: 21,
            rewardBefore: 4f,
            rewardAfter: 5f);
        KnowledgeResidueCompletedFact recon = Fact(
            KnowledgeResidueUse.RegionReconnaissance,
            "observer-recon",
            ownerRevision: 22,
            rewardBefore: 20f,
            rewardAfter: 30f);

        using (var missing = new DomainFactDispatcher())
        {
            Require(!missing.TryStage(codex, out _, out string missingFailure)
                    && missingFailure.StartsWith(
                        "domain-fact-staging-observer-missing:",
                        StringComparison.Ordinal),
                "Knowledge staging succeeded without its required observer.");
        }

        using Fixture fixture = new();
        Require(fixture.Dispatcher.TryStage(
                recon,
                out IDomainFactStage cancelled,
                out string failure),
            failure);
        cancelled.Cancel();
        Require(!cancelled.IsActive && fixture.Dispatcher.ActiveLeaseCount == 0
                && RowCount(fixture.Ledger.CaptureGameplayOutcomes()) == 0,
            "Cancelling a knowledge stage left a durable row or active lease.");

        Require(fixture.Dispatcher.TryStage(codex, out IDomainFactStage codexStage,
                out failure),
            failure);
        string codexKey = codexStage.OperationKey;
        DomainFactDelivery codexDelivery = codexStage.Commit();
        Require(codexDelivery.Completed
                && codexDelivery.OperationKey == codexKey
                && RowCount(fixture.Ledger.CaptureGameplayOutcomes()) == 1,
            "Codex knowledge fact did not commit exactly one ledger row.");

        Require(fixture.Dispatcher.TryStage(recon, out IDomainFactStage reconStage,
                out failure),
            failure);
        Require(reconStage.Commit().Completed
                && RowCount(fixture.Ledger.CaptureGameplayOutcomes()) == 2,
            "Reconnaissance knowledge fact did not commit exactly one ledger row.");

        GameplayOutcomeLedgerSaveData beforeReplay =
            fixture.Ledger.CaptureGameplayOutcomes();
        var results = new DomainFactDelivery[1];
        Require(fixture.Dispatcher.ReplayBatch(
                new[] { DomainFactReplay.Create(codexKey, codex) },
                results,
                out failure)
                && results[0].Completed
                && results[0].OperationKey == codexKey
                && fixture.Ledger.CaptureGameplayOutcomes().nextSequence
                    == beforeReplay.nextSequence
                && RowCount(fixture.Ledger.CaptureGameplayOutcomes()) == 2,
            "Knowledge replay allocated a duplicate ledger row.");

        KnowledgeCompletionExperienceData changed = codex.Experience;
        changed.facilityName = "변조된 연구실";
        var changedFact = new KnowledgeResidueCompletedFact(
            codex.Physical.Receipt,
            changed);
        Require(!fixture.Dispatcher.ReplayBatch(
                new[] { DomainFactReplay.Create(codexKey, changedFact) },
                results,
                out failure)
                && results[0].Phase == DomainFactDeliveryPhase.Rejected
                && fixture.Ledger.CaptureGameplayOutcomes().nextSequence
                    == beforeReplay.nextSequence
                && RowCount(fixture.Ledger.CaptureGameplayOutcomes()) == 2,
            "A changed canonical knowledge payload was accepted on replay.");
    }

    private static KnowledgeResidueCompletedFact Fact(
        KnowledgeResidueUse use,
        string suffix,
        long ownerRevision,
        float rewardBefore,
        float rewardAfter) => new(
        Physical(suffix, ownerRevision),
        Experience(use, 3, rewardBefore, rewardAfter, suffix));

    private static KnowledgeCompletionExperienceData Experience(
        KnowledgeResidueUse use,
        int absoluteDay,
        float rewardBefore,
        float rewardAfter,
        string suffix = "validation") => new()
    {
        taskId = "knowledge-" + suffix,
        facilityId = "facility:knowledge-" + suffix,
        facilityName = "합성 연구실",
        regionId = use == KnowledgeResidueUse.RegionReconnaissance
            ? "region:knowledge-" + suffix
            : string.Empty,
        regionName = use == KnowledgeResidueUse.RegionReconnaissance
            ? "합성 지역"
            : string.Empty,
        codexClue = use == KnowledgeResidueUse.CodexAnalysis
            ? "합성 도감 단서"
            : string.Empty,
        use = use,
        absoluteDay = absoluteDay,
        rewardBefore = rewardBefore,
        rewardAfter = rewardAfter
    };

    private static PhysicalItemBatchDispositionReceipt Physical(
        string suffix,
        long ownerRevision,
        string itemDefinitionId = null)
    {
        string stack = "stack:knowledge-" + suffix;
        const string reason = "memory-residue-research-consumed";
        return new PhysicalItemBatchDispositionReceipt(
            PhysicalItemDispositionKind.Sink,
            "research-knowledge-residue-sink:" + suffix,
            reason,
            (int)PhysicalItemDispositionKind.Sink + ":" + reason + ":"
                + stack + "=1",
            new[] { stack },
            1,
            200,
            ownerRevision,
            new[]
            {
                new PhysicalItemDispositionSourceFact(
                    stack,
                    itemDefinitionId
                        ?? KnowledgeResidueDestinationAuthority.MemoryResidueItemId,
                    string.Empty,
                    1,
                    200,
                    new Vector2Int(4, 5),
                    "기억 잔재")
            });
    }

    private static int RowCount(GameplayOutcomeLedgerSaveData value) =>
        value.outbox.Count + value.exactOutcomes.Count + value.tombstones.Count;

    private static void RequireRejected(Action action, string detail)
    {
        bool rejected = false;
        try { action(); }
        catch (ArgumentException) { rejected = true; }
        Require(rejected, detail);
    }

    private static void Require(bool condition, string detail)
    {
        if (!condition) throw new InvalidOperationException(detail);
    }

    private sealed class Fixture : IDisposable
    {
        private readonly IDisposable observation;

        internal Fixture()
        {
            var registry = new GameplayOutcomeRegistry(
                new IGameplayOutcomeDescriptor[]
                {
                    new KnowledgeCompletionOutcomeDescriptor()
                },
                new IGameplayOutcomeAdapterRegistration[]
                {
                    new KnowledgeCompletionOutcomeAdapter()
                });
            Ledger = new GameplayOutcomeLedger(
                registry,
                GameplayOutcomeBufferLimits.Default);
            var recorder = new GameplayOutcomeRecorder(
                Ledger,
                registry,
                new GameEventBus());
            Dispatcher = new DomainFactDispatcher();
            try
            {
                observation = Dispatcher.Subscribe(
                    "fixture:knowledge-completion",
                    new GameplayDomainFactObserver<
                        KnowledgeResidueCompletedFact,
                        KnowledgeCompletionOutcomeReceipt>(
                        KnowledgeCompletionFactObservation.Capture,
                        registry,
                        Ledger,
                        recorder,
                        new GameplayDomainFactCommitGroup(recorder, Ledger)));
            }
            catch
            {
                Dispatcher.Dispose();
                throw;
            }
        }

        internal DomainFactDispatcher Dispatcher { get; }
        internal GameplayOutcomeLedger Ledger { get; }

        public void Dispose()
        {
            observation?.Dispose();
            Dispatcher.Dispose();
        }
    }
}
#endif
