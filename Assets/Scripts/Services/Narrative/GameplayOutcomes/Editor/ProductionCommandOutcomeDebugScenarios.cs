#if UNITY_EDITOR
using System;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class ProductionCommandOutcomeDebugScenarios
{
    private const string FacilityId = "building:qa-production-command";
    private const string BeforeValue =
        "suspended=false;reserved-worker=none";
    private const string AfterValue =
        "suspended=true;reserved-worker=none";

    public static bool RunAll(bool logSuccess = false)
    {
        GameplayOutcomeRegistry registry = new(
            new IGameplayOutcomeDescriptor[]
            {
                new ProductionCommandOutcomeDescriptor(
                    new KoreanJosaFormatter())
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new ProductionCommandOutcomeAdapter()
            });
        GameplayOutcomeLedger ledger = new(
            registry,
            new GameplayOutcomeBufferLimits(
                smallPageCount: 8,
                largePageCount: 0,
                knownResultKeyCapacity: 128),
            new GameplayOutcomeRunId("run:production-command-editor"),
            1L);
        GameplayOutcomeRecorder recorder = new(
            ledger,
            registry,
            new GameEventBus());
        GameplayDomainFactCommitGroup commits = new(recorder, ledger);
        using DomainFactDispatcher facts = new();
        using IDisposable subscription = facts.Subscribe(
            "production.command-observer",
            new GameplayDomainFactObserver<
                ProductionCommandAppliedFact,
                ProductionCommandOutcomeReceipt>(
                ProductionCommandFactObservation.Capture,
                registry,
                ledger,
                recorder,
                commits));

        ProductionCommandAppliedFact fact = CreateFact(suspendedAfter: true);
        Require(
            facts.TryStage(
                fact,
                out IDomainFactStage stage,
                out string prepareFailure),
            "production command fact staging failed: " + prepareFailure);
        DomainFactDelivery committed = stage.Commit();
        Require(
            committed.Completed
            && committed.OperationKey
                == ProductionCommandOutcomeIds.ResultKey(1L).ToString()
            && committed.PublicationId.Length > 0
            && committed.Fingerprint.Length == 64,
            "production command fact commit did not return its durable opaque proof: "
            + committed.Detail);

        GameplayEntityId facility = new(
            ProductionCommandOutcomeIds.FacilityKind,
            FacilityId);
        GameplayOutcomeQueryPage global = ledger.GetGlobal(
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        GameplayOutcomeQueryPage facilityPage = ledger.GetForEntity(
            facility,
            OutcomeCursor.FirstPage(10),
            OutcomeFilter.All);
        Require(
            global.Items.Count == 1
            && facilityPage.Items.Count == 1
            && global.Items[0].Exact != null,
            "production command queries did not expose one exact outcome");
        GameplayOutcomeSnapshot exact = global.Items[0].Exact;
        Require(
            exact.outcomeTypeId == ProductionCommandOutcomeIds.Applied.Value
            && exact.ownerRevision == 1L
            && exact.commitRevision == 1L
            && exact.absoluteDay == 12
            && exact.locationX == 7
            && exact.locationY == 4
            && exact.participants.Count == 1
            && exact.participants[0].displayText == "대장간"
            && exact.facts.Any(value =>
                value.factId == ProductionCommandOutcomeIds.CommandKindFact.Value
                && value.value == "suspension-changed")
            && exact.facts.Any(value =>
                value.factId == ProductionCommandOutcomeIds.BeforeValueFact.Value
                && value.value == BeforeValue)
            && exact.facts.Any(value =>
                value.factId == ProductionCommandOutcomeIds.AfterValueFact.Value
                && value.value == AfterValue),
            "production command outcome lost its frozen typed fact data");

        GameplayOutcomeId outcomeId = new(
            new GameplayOutcomeRunId(exact.runId),
            exact.sequence);
        Require(
            ledger.TryProject(
                outcomeId,
                new NarrativePerspectiveContext(
                    facility,
                    NarrativePerspectiveKind.Facility,
                    "ko-KR"),
                out NarrativeView view)
            && view.Text.Contains("대장간은", StringComparison.Ordinal)
            && view.Text.Contains("시험 주괴 주문", StringComparison.Ordinal)
            && view.Text.Contains("가동 상태 변경", StringComparison.Ordinal),
            "production command Korean projection was not stable");

        var replayResults = new DomainFactDelivery[1];
        Require(
            facts.ReplayBatch(
                new[]
                {
                    DomainFactReplay.Create(committed.OperationKey, fact)
                },
                replayResults,
                out string replayFailure)
            && replayResults[0].Completed
            && ledger.GetGlobal(
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Count == 1,
            "production command replay duplicated or rejected the canonical result: "
            + replayFailure);

        ProductionCommandAppliedFact drifted = CreateFact(
            suspendedAfter: false);
        var driftResults = new DomainFactDelivery[1];
        Require(
            !facts.ReplayBatch(
                new[]
                {
                    DomainFactReplay.Create(committed.OperationKey, drifted)
                },
                driftResults,
                out _)
            && !driftResults[0].MayBeCommitted,
            "same production command result key accepted a drifted typed fact");

        GameplayOutcomeLedgerSaveData saved = ledger.CaptureGameplayOutcomes();
        GameplayOutcomeLedger restored = new(
            registry,
            new GameplayOutcomeBufferLimits(
                smallPageCount: 8,
                largePageCount: 0,
                knownResultKeyCapacity: 128),
            new GameplayOutcomeRunId("run:production-command-restore"),
            1L);
        restored.BeginRestoreCandidate();
        GameplayOutcomeLedgerRestoreCandidate candidate =
            restored.PrepareGameplayOutcomeRestore(saved);
        restored.PublishGameplayOutcomeRestore(candidate);
        restored.PublishRestoreCandidate();
        restored.CompleteRestoreCandidate();
        Require(
            restored.GetForEntity(
                facility,
                OutcomeCursor.FirstPage(10),
                OutcomeFilter.All).Items.Single().Exact.immutablePayloadHash
            == exact.immutablePayloadHash,
            "production command outcome changed across save round trip");

        if (logSuccess)
            Debug.Log("[Production Command Outcome] PASS");
        return true;
    }

    private static ProductionCommandAppliedFact CreateFact(
        bool suspendedAfter)
    {
        ProductionCommandStateData before = CreateState(suspended: false);
        ProductionCommandStateData after = CreateState(suspendedAfter);
        return new ProductionCommandAppliedFact(new ProductionCommandFactData
        {
            ownerRevision = 1L,
            kind = ProductionCommandKind.SuspensionChanged,
            billId = "production-bill:qa",
            recipeId = "recipe:qa-ingot",
            recipeDisplayName = "시험 주괴",
            facilityId = FacilityId,
            facilityDisplayName = "대장간",
            facilityX = 7,
            facilityY = 4,
            absoluteDay = 12,
            before = before,
            after = after
        });
    }

    private static ProductionCommandStateData CreateState(bool suspended) =>
        new()
        {
            exists = true,
            mode = ProductionOrderMode.RepeatCount,
            pendingMode = ProductionOrderMode.RepeatCount,
            remainingCycles = 1,
            minimumQuality = -1,
            suspended = suspended,
            distributionMode = ProductionDistributionMode.DemandWeighted,
            workerPolicy = WorkerSelectionPolicySaveData.Anyone()
        };

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
