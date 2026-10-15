using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEditor;
using UnityEngine;

public static class ResearchOutcomeTransactionDebugScenarios
{
    public static void RunAll(Action<string> completedGroup = null)
    {
        Run("progress-completion-unlocks-replay", VerifyProgressCompletionAndReplay);
        Run("reject-rollback/unknown-commit/json-fresh-owner/canonical-and-torn-save-rejection", VerifyRollbackAndRetry);
        Run("delivery-pending-save-continuation", VerifyPendingSaveAndContinuation);
        Run("capacity-before-mutation", VerifyCapacityBeforeMutation);
        Run("blueprint-and-invalid-input", VerifyLegacyAndInvalidInput);
        Run("leap-variants-replay", VerifyLeapVariantsAndReplay);
        Run("leap-faults-capacity-save", VerifyLeapFaultsCapacityAndSave);
        Run("immediate-completion", VerifyImmediateCompletion);
        Run("knowledge-progress", VerifyKnowledgeProgressTransaction);
        Run("research-save-sequence-join", VerifyResearchSaveSequenceJoin);
        Debug.Log("RESEARCH_OUTCOME_TRANSACTION=PASS (10 groups)");
        void Run(string name, Action action) { action(); completedGroup?.Invoke(name + "=PASS"); }
    }

    public static BlueprintResearchOutcomeTransaction CreateTransactionForRuntimeFixture(
        out IDisposable observationLifetime)
    {
        GameplayOutcomeRegistry registry = CreateRegistry();
        GameplayOutcomeLedger ledger = new(registry, GameplayOutcomeBufferLimits.Default);
        GameplayOutcomeRecorder recorder = new(
            ledger,
            registry,
            new GameEventBus());
        DomainFactDispatcher dispatcher = ConnectResearchObserver(
            registry,
            ledger,
            recorder);
        observationLifetime = dispatcher;
        return new BlueprintResearchOutcomeTransaction(dispatcher, dispatcher);
    }

    private static GameplayOutcomeRegistry CreateRegistry() => new(
        new IGameplayOutcomeDescriptor[] { new ResearchWorkOutcomeDescriptor() },
        new IGameplayOutcomeAdapterRegistration[] { new ResearchWorkOutcomeAdapter() });

    private static DomainFactDispatcher ConnectResearchObserver(
        IGameplayOutcomeRegistry registry,
        GameplayOutcomeLedger ledger,
        IGameplayOutcomeRecorder recorder)
    {
        DomainFactDispatcher dispatcher = new();
        try
        {
            dispatcher.Subscribe(
                "research-work-gameplay-outcome",
                new GameplayDomainFactObserver<
                    ResearchWorkAppliedFact,
                    ResearchWorkOutcomeReceipt>(
                    ResearchWorkFactObservation.Capture,
                    registry,
                    ledger,
                    recorder,
                    new GameplayDomainFactCommitGroup(recorder, ledger)));
            return dispatcher;
        }
        catch
        {
            dispatcher.Dispose();
            throw;
        }
    }

    private static void VerifyResearchSaveSequenceJoin()
    {
        using Fixture f = new();
        Require(f.Apply(2, out _), "save join published setup");
        f.Faults.FailDelivery = true;
        Require(f.Apply(2, out _), "save join pending setup");
        GameplayOutcomeLedgerSaveData live = f.Ledger.CaptureGameplayOutcomes();
        string original = JsonUtility.ToJson(live);
        ResearchOutcomeSavePreflight validator = new();
        bool Accept(long sequence, GameplayOutcomeLedgerSaveData ledger)
        {
            DungeonGameSaveData save = new();
            DungeonSaveSectionPayload.Write(save, BlueprintResearchSaveSection.Id, 7,
                DungeonSaveRestorePhase.RuntimeState, new DungeonResearchSaveData { outcomeSequence = sequence });
            DungeonSaveSectionPayload.Write(save, GameplayOutcomeLedgerSaveSection.Id, 1,
                DungeonSaveRestorePhase.RuntimeState, ledger);
            string before = JsonUtility.ToJson(save);
            DungeonGameRestoreReport capture = new(), restore = new();
            ((IDungeonCapturedSavePreflightValidator)validator).Validate(save, capture);
            ((IDungeonSavePreflightValidator)validator).Validate(save, restore);
            Require(capture.Success == restore.Success && JsonUtility.ToJson(save) == before,
                "same non-mutating join on capture and restore");
            return capture.Success;
        }
        Require(Accept(0, new GameplayOutcomeLedgerSaveData()), "legacy zero/empty join");
        Require(Accept(2, live), "matching research/ledger sequence");
        Require(!Accept(1, live) && !Accept(3, live) && !Accept(-1, live), "torn/negative research sequence rejected");
        Require(!Accept(2, new GameplayOutcomeLedgerSaveData()), "missing ledger rejected");
        GameplayOutcomeLedgerSaveData Clone() => JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(original);
        var missing = Clone();
        missing.exactOutcomes.RemoveAt(0);
        Require(!Accept(2, missing), "interior missing research command rejected");
        var duplicate = Clone();
        duplicate.outbox.Add(duplicate.exactOutcomes[0]);
        Require(!Accept(2, duplicate), "duplicate research command rejected");
        var invalidKey = Clone();
        invalidKey.exactOutcomes[0].operationId = "research-work:99";
        Require(!Accept(2, invalidKey), "noncanonical research key rejected");
        var pending = Clone();
        Require(pending.outbox.Count == 1 && Accept(2, pending), "pending receipt counts toward durable sequence");
        var retired = Clone();
        var source = retired.exactOutcomes[0];
        retired.tombstones.Add(new GameplayOutcomeTombstoneSnapshot
        {
            runId = source.runId, sequence = source.sequence,
            producerId = source.producerId, operationId = source.operationId,
            commitRevision = source.commitRevision, localResultIndex = source.localResultIndex,
            ownerRevision = source.ownerRevision, terminalTier = NarrativeMemoryTier.Forgotten,
            canonicalHash = source.immutablePayloadHash
        });
        retired.exactOutcomes.RemoveAt(0);
        Require(Accept(2, retired), "retired replay identity preserves sequence join");
        Require(JsonUtility.ToJson(f.Ledger.CaptureGameplayOutcomes()) == original
            && f.State.OutcomeSequence == 2, "rejected save joins cannot mutate live owners");
    }

    private static void VerifyKnowledgeProgressTransaction()
    {
        using Fixture f = new(pageCount: 2);
        DungeonRuntimeAggregateRootStore root = new();
        KnowledgeResidueAggregateState initial = new();
        initial.AddTask(new KnowledgeResidueTaskSaveData
        {
            taskId = "knowledge-00001", requiredWork = 24,
            facilityInstanceId = "facility:research-lab"
        });
        root.Replace(initial);
        BlueprintResearchState sequence = new(root);
        KnowledgeResidueAggregateState Current() => root.GetOrCreate(() => new KnowledgeResidueAggregateState());
        bool Apply(float work, out BlueprintResearchWorkResult result) => f.Transaction.TryApplyKnowledgeProgress(
            root, work, 1, "기억 잔재 분석", "character:research-worker", "연구자",
            "facility:research-lab", "연구실", null, out result);

        f.Faults.RejectCommit = true;
        Require(!Apply(3, out _) && Current().FirstTask.completedWork == 0
            && sequence.OutcomeSequence == 0 && f.Ledger.CaptureGameplayOutcomes().outbox.Count == 0,
            "knowledge rejection rolls back both owners");
        f.Faults.RejectCommit = false;
        f.Faults.ThrowBeforeCommit = true;
        Require(Apply(3, out var uncertain)
            && uncertain.Success
            && uncertain.PublicationPending
            && Current().FirstTask.completedWork == 3
            && sequence.OutcomeSequence == 1
            && sequence.HasPendingPublication
            && f.Ledger.CaptureGameplayOutcomes().outbox.Count == 0
            && f.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 0,
            "knowledge commit uncertainty retains the frozen fact and applied owner");
        Require(!Apply(3, out var blocked)
            && blocked.Message.Contains("research-fact-publication-pending")
            && Current().FirstTask.completedWork == 3
            && sequence.OutcomeSequence == 1,
            "knowledge pending publication blocks the next owner mutation");
        f.Faults.ThrowBeforeCommit = false;
        Require(f.Transaction.TryRecoverPending(
                sequence,
                out ResearchWorkAppliedFact recovered,
                out string recoveredKey,
                out string recoveryFailure)
            && recovered != null
            && recovered.Revision == 1
            && recovered.Cause == ResearchWorkCause.KnowledgeProgress
            && recovered.After == 3f
            && recovered.ApprovedWork == 3f
            && recoveredKey == uncertain.CommittedResultKey
            && recoveryFailure.Length == 0
            && !sequence.HasPendingPublication,
            "knowledge pending fact did not recover with its exact identity");
        f.Faults.ThrowAfterCommit = true;
        Require(Apply(3, out var committed) && committed.Success && !committed.Completed
            && !committed.PublicationPending
            && Current().FirstTask.completedWork == 6 && sequence.OutcomeSequence == 2
            && initial.FirstTask.completedWork == 0, "knowledge post-commit ambiguity retains detached publication");
        f.Faults.ThrowAfterCommit = false;
        // A confirmed commit is now reconciled/acknowledged by the observer.
        // Exercise genuine backpressure explicitly, not a leaked prepared page.
        using (Fixture bounded = new(pageCount: 1))
        {
            bounded.Faults.FailDelivery = true;
            DungeonRuntimeAggregateRootStore boundedRoot = new();
            KnowledgeResidueAggregateState boundedKnowledge = new();
            boundedKnowledge.AddTask(new KnowledgeResidueTaskSaveData
            {
                taskId = "knowledge-00001", requiredWork = 24,
                facilityInstanceId = "facility:research-lab"
            });
            boundedRoot.Replace(boundedKnowledge);
            bool BoundedApply(out BlueprintResearchWorkResult value) => bounded.Transaction.TryApplyKnowledgeProgress(
                boundedRoot, 3, 1, "기억 잔재 분석", "character:research-worker", "연구자",
                "facility:research-lab", "연구실", null, out value);
            Require(BoundedApply(out _) && !BoundedApply(out var deferred) && deferred.CapacityDeferred
                && boundedRoot.GetOrCreate(() => new KnowledgeResidueAggregateState()).FirstTask.completedWork == 3
                && new BlueprintResearchState(boundedRoot).OutcomeSequence == 1
                && bounded.Ledger.CaptureGameplayOutcomes().outbox.Count == 1,
                "knowledge capacity before mutation with an actual pending-delivery page");
        }

        string domainJson = JsonUtility.ToJson(new DungeonResearchSaveData
        {
            outcomeSequence = sequence.OutcomeSequence,
            knowledgeTasks = Current().Tasks.ToList()
        });
        string ledgerJson = JsonUtility.ToJson(f.Ledger.CaptureGameplayOutcomes());
        DungeonResearchSaveData saved = JsonUtility.FromJson<DungeonResearchSaveData>(domainJson);
        DungeonRuntimeAggregateRootStore restoredRoot = new();
        KnowledgeResidueAggregateState restoredKnowledge = new();
        foreach (var task in saved.knowledgeTasks) restoredKnowledge.AddTask(task);
        restoredRoot.Replace(restoredKnowledge);
        BlueprintResearchState restoredSequence = new(restoredRoot);
        restoredSequence.RestoreOutcomeSequence(saved.outcomeSequence);
        GameplayOutcomeRegistry registry = CreateRegistry();
        GameplayOutcomeLedger restoredLedger = new(registry, GameplayOutcomeBufferLimits.Default);
        var candidate = restoredLedger.PrepareGameplayOutcomeRestore(
            JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(ledgerJson));
        restoredLedger.BeginRestoreCandidate();
        restoredLedger.PublishGameplayOutcomeRestore(candidate);
        restoredLedger.PublishRestoreCandidate();
        restoredLedger.CompleteRestoreCandidate();
        GameplayOutcomeRecorder restoredRecorder = new(restoredLedger, registry, new GameEventBus());
        restoredRecorder.RetryPendingDeliveries(10);
        restoredRecorder.RetryPendingDeliveries(10);
        Require(restoredLedger.CaptureGameplayOutcomes().exactOutcomes.Count == 2,
            "knowledge pending save replays once");
        using DomainFactDispatcher restoredFacts = ConnectResearchObserver(
            registry,
            restoredLedger,
            restoredRecorder);
        BlueprintResearchOutcomeTransaction restoredTransaction = new(
            restoredFacts,
            restoredFacts);
        Require(restoredTransaction.TryApplyKnowledgeProgress(restoredRoot, 99, 1, "기억 잔재 분석",
                "character:research-worker", "연구자", "facility:research-lab", "연구실", null, out var full)
            && full.AddedProgress == 18 && full.TotalProgress == 24 && !full.Completed
            && restoredSequence.OutcomeSequence == 3, "knowledge full progress is not reward completion");
        var exact = restoredLedger.CaptureGameplayOutcomes().exactOutcomes.Last();
        Require(exact.anchors.Count == 0 && exact.facts.Any(value => value.factId == "research.cause"
                && value.value == "knowledge-progress")
            && exact.facts.Any(value => value.factId == "research.completed" && value.value == "false"),
            "knowledge progress truth shape");
        GameplayEntityId subject = new(ResearchWorkOutcomeIds.KnowledgeKind, "knowledge-00001");
        Require(restoredLedger.GetForEntity(subject, OutcomeCursor.FirstPage(), OutcomeFilter.All).Items.Count == 3
            && restoredLedger.TryProject(new GameplayOutcomeId(new GameplayOutcomeRunId(exact.runId), exact.sequence),
                new NarrativePerspectiveContext(default, NarrativePerspectiveKind.Global, "ko-KR"), out var view)
            && view.Text.Contains("결과 처리 대기"), "knowledge query and neutral pending projection");
        Require(!restoredTransaction.TryApplyKnowledgeProgress(restoredRoot, 1, 1, "기억 잔재 분석",
            "character:research-worker", "연구자", "facility:research-lab", "연구실", null, out _)
            && restoredSequence.OutcomeSequence == 3, "full knowledge task never pays for more work");
    }

    private static void VerifyProgressCompletionAndReplay()
    {
        using Fixture f = new();
        using var observer = f.Events.Subscribe<GameplayOutcomePublishedRangeEvent>(
            _ => throw new InvalidOperationException("injected-observer"));
        Require(f.Apply(3, out var progress) && !progress.Completed
            && progress.TotalProgress == 3 && f.State.OutcomeSequence == 1, "progress: " + progress.Message);
        Require(f.Apply(99, out var complete) && complete.Completed
            && complete.AddedProgress == 7 && f.State.Projects.IsCompleted(f.Project.ProjectId)
            && f.State.UnlockedBuildingIds.Contains(f.Building.id)
            && f.Shop.IsBasicPurchaseUnlocked(f.Building.id)
            && f.State.UnlockedRecipeIds.Contains("recipe:research-test"), "completion/unlocks: " + complete.Message);
        GameplayOutcomeLedgerSaveData snapshot = f.Ledger.CaptureGameplayOutcomes();
        Require(snapshot.exactOutcomes.Count == 2 && snapshot.outbox.Count == 0, "exact publication");
        var exact = snapshot.exactOutcomes[1];
        Require(exact.facts.Count == 7 && exact.metrics.Count == 4 && exact.participants.Count == 3
            && exact.anchors.Count == 1 && snapshot.notificationFaultCount == 2, "typed facts and observer isolation");
        GameplayEntityId researcher = new(new GameplayEntityKindId("character"), "character:research-worker");
        GameplayEntityId facility = new(new GameplayEntityKindId("facility"), "facility:research-lab");
        Require(f.Ledger.GetForEntity(researcher, OutcomeCursor.FirstPage(), OutcomeFilter.All).Items.Count == 2
            && f.Ledger.GetForEntity(facility, OutcomeCursor.FirstPage(), OutcomeFilter.All).Items.Count == 2,
            "shared character/facility query");
        GameplayOutcomeId outcomeId = new(new GameplayOutcomeRunId(exact.runId), exact.sequence);
        Require(f.Ledger.TryProject(outcomeId,
                new NarrativePerspectiveContext(researcher, NarrativePerspectiveKind.Character, "ko-KR"), out var view)
            && view.Text.Contains("기록 연구") && view.Text.Contains("완료"), "character projection");
        Require(!f.Apply(99, out _) && f.State.OutcomeSequence == 2
            && f.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 2, "terminal replay");
        f.Recorder.RetryPendingDeliveries(20);
        Require(f.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 2, "delivery idempotence");
    }

    private static void VerifyRollbackAndRetry()
    {
        using Fixture f = new();
        f.Faults.RejectCommit = true;
        Require(!f.Apply(10, out _), "commit rejection must fail");
        f.RequireUnchanged();
        f.Faults.RejectCommit = false;
        f.Faults.ThrowBeforeCommit = true;
        Require(f.Apply(10, out var uncertain)
            && uncertain.Completed
            && uncertain.PublicationPending
            && f.State.HasPendingPublication
            && f.State.OutcomeSequence == 1
            && f.Shop.IsBasicPurchaseUnlocked(f.Building.id)
            && f.Ledger.CaptureGameplayOutcomes().outbox.Count == 0
            && f.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 0,
            "commit uncertainty must retain the applied owner and frozen fact");
        Require(!f.Apply(1, out var blocked)
            && blocked.Message.Contains("research-fact-publication-pending")
            && f.State.OutcomeSequence == 1,
            "pending fact must block the next research mutation");

        DungeonResearchSaveData pendingSave = JsonUtility.FromJson<DungeonResearchSaveData>(
            JsonUtility.ToJson(new DungeonResearchSaveData
            {
                outcomeSequence = f.State.OutcomeSequence,
                pendingWorkPublication = f.State.PendingPublication,
                projectProgress = new List<DungeonResearchProjectProgressSaveData>
                {
                    new()
                    {
                        projectId = f.Project.ProjectId.Value,
                        progress = f.State.Projects.GetProgress(f.Project.ProjectId).Progress,
                        requiredWorkAtCapture = f.Project.RequiredWork
                    }
                },
                completedProjectIds = f.State.Projects.CompletedProjectIds.ToList(),
                completedProjectResults = f.State.CompletedProjectResultKeys
                    .Select(pair => new DungeonResearchCompletionResultSaveData
                    {
                        projectId = pair.Key,
                        publicationKey = pair.Value
                    })
                    .ToList(),
                unlockedBuildingIds = f.State.UnlockedBuildingIds.ToList(),
                unlockedRecipeIds = f.State.UnlockedRecipeIds.ToList()
            }));
        GameplayOutcomeLedgerSaveData pendingLedger =
            JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(
                JsonUtility.ToJson(f.Ledger.CaptureGameplayOutcomes()));
        DungeonGameSaveData joinedSave = new();
        DungeonSaveSectionPayload.Write(
            joinedSave,
            BlueprintResearchSaveSection.Id,
            7,
            DungeonSaveRestorePhase.RuntimeState,
            pendingSave);
        DungeonSaveSectionPayload.Write(
            joinedSave,
            GameplayOutcomeLedgerSaveSection.Id,
            1,
            DungeonSaveRestorePhase.RuntimeState,
            pendingLedger);
        DungeonGameRestoreReport pendingReport = new();
        ((IDungeonCapturedSavePreflightValidator)new ResearchOutcomeSavePreflight())
            .Validate(joinedSave, pendingReport);
        Require(pendingReport.Success
            && pendingSave.pendingWorkPublication?.active == true
            && pendingSave.projectProgress.Single().progress == 10f
            && pendingSave.completedProjectIds.Single()
                == f.Project.ProjectId.Value
            && pendingSave.completedProjectResults.Single().publicationKey
                == uncertain.CommittedResultKey
            && pendingSave.unlockedBuildingIds.Contains(f.Building.id)
            && pendingSave.unlockedRecipeIds.Contains("recipe:research-test"),
            "pending project save did not preserve its frozen owner join");

        f.Faults.ThrowBeforeCommit = false;
        Require(f.Transaction.TryRecoverPending(
                f.State,
                out ResearchWorkAppliedFact recovered,
                out string recoveredKey,
                out string recoveryFailure)
            && recovered != null
            && recovered.Completed
            && recovered.Revision == 1
            && recovered.ApprovedWork == 10f
            && JsonUtility.ToJson(recovered.Capture())
                == JsonUtility.ToJson(pendingSave.pendingWorkPublication.fact)
            && recoveredKey == uncertain.CommittedResultKey
            && recoveryFailure.Length == 0
            && !f.State.HasPendingPublication
            && f.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 1,
            "pending fact recovery must redeliver the same frozen fact once");

        var confirmedLedger = f.Ledger.CaptureGameplayOutcomes();
        DungeonGameRestoreReport confirmedPending = new();
        ResearchOutcomeSavePreflight.ValidateSequence(pendingSave, confirmedLedger, confirmedPending);
        Require(confirmedPending.Success, "confirmed pending save canonical comparison failed: "
            + JsonUtility.ToJson(confirmedPending));
        var altered = JsonUtility.FromJson<DungeonResearchSaveData>(JsonUtility.ToJson(pendingSave));
        altered.pendingWorkPublication.fact.researcherName += "-altered";
        DungeonGameRestoreReport alteredReport = new();
        ResearchOutcomeSavePreflight.ValidateSequence(altered, confirmedLedger, alteredReport);
        Require(!alteredReport.Success, "pending save must reject changed canonical participant evidence");
        altered = JsonUtility.FromJson<DungeonResearchSaveData>(JsonUtility.ToJson(pendingSave));
        altered.projectProgress[0].progress = 9;
        DungeonGameRestoreReport tornOwner = new();
        ResearchOutcomeSavePreflight.ValidateSequence(altered, pendingLedger, tornOwner);
        Require(!tornOwner.Success, "pending save must reject torn owner progress before publication");
        string raw = JsonUtility.ToJson(pendingSave);
        BlueprintResearchSaveSection.ValidatePublicationJson(raw);
        var missing = Newtonsoft.Json.Linq.JObject.Parse(raw);
        ((Newtonsoft.Json.Linq.JObject)missing["pendingWorkPublication"]["fact"]).Remove("hasEquipment");
        bool rejectedMissing = false;
        try { BlueprintResearchSaveSection.ValidatePublicationJson(missing.ToString()); }
        catch (InvalidOperationException) { rejectedMissing = true; }
        Require(rejectedMissing, "pending save cannot default a missing frozen-fact field");

        BlueprintResearchState freshState = new();
        freshState.RestoreOutcomeSequence(pendingSave.outcomeSequence);
        foreach (DungeonResearchProjectProgressSaveData savedProgress
                 in pendingSave.projectProgress)
        {
            freshState.Projects.GetProgress(new ResearchProjectId(
                    savedProgress.projectId))
                .Restore(savedProgress.progress, f.Project);
        }
        foreach (string projectId in pendingSave.completedProjectIds)
            freshState.Projects.RestoreCompleted(new ResearchProjectId(projectId));
        foreach (DungeonResearchCompletionResultSaveData savedResult
                 in pendingSave.completedProjectResults)
        {
            freshState.RecordCompletedProjectResultKey(
                savedResult.projectId,
                savedResult.publicationKey);
        }
        foreach (int buildingId in pendingSave.unlockedBuildingIds)
            freshState.RestoreUnlockedBuildingId(buildingId);
        foreach (string recipeId in pendingSave.unlockedRecipeIds)
            freshState.UnlockRecipe(recipeId);
        freshState.RestorePendingPublication(
            pendingSave.pendingWorkPublication?.active == true
                ? pendingSave.pendingWorkPublication
                : null);

        GameplayOutcomeRegistry freshRegistry = CreateRegistry();
        GameplayOutcomeLedger freshLedger = new(
            freshRegistry,
            GameplayOutcomeBufferLimits.Default);
        GameplayOutcomeLedgerRestoreCandidate freshCandidate =
            freshLedger.PrepareGameplayOutcomeRestore(pendingLedger);
        freshLedger.BeginRestoreCandidate();
        freshLedger.PublishGameplayOutcomeRestore(freshCandidate);
        freshLedger.PublishRestoreCandidate();
        freshLedger.CompleteRestoreCandidate();
        GameplayOutcomeRecorder freshRecorder = new(
            freshLedger,
            freshRegistry,
            new GameEventBus());
        using DomainFactDispatcher freshFacts = ConnectResearchObserver(
            freshRegistry,
            freshLedger,
            freshRecorder);
        BlueprintResearchOutcomeTransaction freshTransaction = new(
            freshFacts,
            freshFacts);
        Require(freshState.HasPendingPublication
            && !freshState.TryGetCompletedProjectResultKey(
                f.Project.ProjectId.Value,
                out _)
            && freshTransaction.TryRecoverPending(
                freshState,
                out ResearchWorkAppliedFact freshRecovered,
                out string freshRecoveredKey,
                out string freshRecoveryFailure)
            && freshRecovered != null
            && freshRecovered.Revision == recovered.Revision
            && freshRecovered.SubjectId == recovered.SubjectId
            && freshRecovered.ApprovedWork == recovered.ApprovedWork
            && JsonUtility.ToJson(freshRecovered.Capture())
                == JsonUtility.ToJson(pendingSave.pendingWorkPublication.fact)
            && freshRecoveredKey == recoveredKey
            && freshRecoveryFailure.Length == 0
            && !freshState.HasPendingPublication
            && freshState.TryGetCompletedProjectResultKey(
                f.Project.ProjectId.Value,
                out string visibleCompletedKey)
            && visibleCompletedKey == uncertain.CommittedResultKey
            && freshLedger.CaptureGameplayOutcomes().exactOutcomes.Count == 1,
            "fresh restore did not recover the identical pending fact once");

        using Fixture committedFixture = new();
        committedFixture.Faults.ThrowAfterCommit = true;
        Require(committedFixture.Apply(10, out var committed)
            && committed.Completed
            && !committed.PublicationPending,
            "post-commit ambiguity must reconcile as committed");
        GameplayOutcomeLedgerSaveData committedLedger =
            committedFixture.Ledger.CaptureGameplayOutcomes();
        Require(committedFixture.State.OutcomeSequence == 1
            && committedFixture.Shop.IsBasicPurchaseUnlocked(
                committedFixture.Building.id)
            && !committedFixture.State.HasPendingPublication
            && committedLedger.outbox.Count
                + committedLedger.exactOutcomes.Count == 1,
            "committed payload was not preserved exactly once");
    }

    private static void VerifyPendingSaveAndContinuation()
    {
        using Fixture f = new();
        f.Faults.FailDelivery = true;
        Require(f.Apply(3, out var pending) && pending.Message.Contains("pending"), "pending surfaced");
        string ledgerJson = JsonUtility.ToJson(f.Ledger.CaptureGameplayOutcomes());
        // Exercise the current research sequence DTO and continuation boundary.
        string researchJson = JsonUtility.ToJson(new DungeonResearchSaveData
        {
            outcomeSequence = f.State.OutcomeSequence,
            projectProgress = new List<DungeonResearchProjectProgressSaveData>
            {
                new() { projectId = f.Project.ProjectId.Value, progress = 3, requiredWorkAtCapture = 10 }
            }
        });
        DungeonResearchSaveData restored = JsonUtility.FromJson<DungeonResearchSaveData>(researchJson);
        BlueprintResearchState restoredState = new();
        restoredState.RestoreOutcomeSequence(restored.outcomeSequence);
        restoredState.Projects.GetProgress(f.Project.ProjectId).Restore(restored.projectProgress[0].progress, f.Project);
        GameplayOutcomeRegistry registry = CreateRegistry();
        GameplayOutcomeLedger restoredLedger = new(registry, GameplayOutcomeBufferLimits.Default);
        var candidate = restoredLedger.PrepareGameplayOutcomeRestore(
            JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(ledgerJson));
        restoredLedger.BeginRestoreCandidate();
        restoredLedger.PublishGameplayOutcomeRestore(candidate);
        restoredLedger.PublishRestoreCandidate();
        restoredLedger.CompleteRestoreCandidate();
        GameplayOutcomeRecorder restoredRecorder = new(restoredLedger, registry, new GameEventBus());
        restoredRecorder.RetryPendingDeliveries(10);
        using DomainFactDispatcher restoredFacts = ConnectResearchObserver(
            registry,
            restoredLedger,
            restoredRecorder);
        BlueprintResearchOutcomeTransaction transaction = new(
            restoredFacts,
            restoredFacts);
        Require(transaction.TryApply(restoredState, f.Shop, f.Catalog, f.Project, null, 7, 1,
            "character:research-worker", "연구자", "facility:research-lab", "연구실", out var complete, out _)
            && complete.Completed && restoredState.OutcomeSequence == 2
            && restoredLedger.CaptureGameplayOutcomes().exactOutcomes.Count == 2, "save continuation");
        Require(JsonUtility.FromJson<DungeonResearchSaveData>("{}").outcomeSequence == 0, "empty DTO starts at zero; not a legacy-save acceptance test");
        bool invalid = false;
        try { restoredState.RestoreOutcomeSequence(-1); }
        catch (ArgumentOutOfRangeException) { invalid = true; }
        Require(invalid && restoredState.OutcomeSequence == 2, "invalid sequence rejection");
    }

    private static void VerifyCapacityBeforeMutation()
    {
        using Fixture f = new(pageCount: 1);
        f.Faults.FailDelivery = true;
        Require(f.Apply(2, out _), "first bounded page");
        Require(!f.Apply(8, out var rejected) && rejected.CapacityDeferred
            && rejected.Message.Contains("CapacityDeferred"), "capacity result");
        Require(f.State.Projects.GetProgress(f.Project.ProjectId).Progress == 2
            && f.State.OutcomeSequence == 1 && !f.Shop.IsBasicPurchaseUnlocked(f.Building.id)
            && f.Ledger.CaptureGameplayOutcomes().outbox.Count == 1, "capacity no gameplay mutation");
    }

    private static void VerifyLegacyAndInvalidInput()
    {
        using Fixture f = new();
        FacilityBlueprintSO blueprint = ScriptableObject.CreateInstance<FacilityBlueprintSO>();
        try
        {
            blueprint.id = 73001;
            blueprint.name = "검증 설계도";
            blueprint.researchWorkRequired = 5;
            f.State.EnqueueBlueprint(blueprint);
            Require(!f.Apply(float.NaN, out _) && !f.Apply(float.PositiveInfinity, out _), "nonfinite work");
            f.RequireUnchanged();
            Require(f.Transaction.TryApply(f.State, f.Shop, f.Catalog, null, blueprint, 5, 1,
                string.Empty, string.Empty, "facility:research-lab", "연구실", out var complete, out _)
                && complete.Completed && f.State.IsCompleted(blueprint)
                && f.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 1, "legacy blueprint");
        }
        finally { UnityEngine.Object.DestroyImmediate(blueprint); }
    }

    private static void VerifyLeapVariantsAndReplay()
    {
        using LeapActor actor = new();
        foreach (ExtremeRiskOutcome expected in new[] {
                     ExtremeRiskOutcome.Breakthrough, ExtremeRiskOutcome.Setback, ExtremeRiskOutcome.Normal })
        {
            using Fixture f = new();
            CharacterIdentityStateStore store = new();
            ExtremeTraitRuntime traits = new(store);
            float before = expected == ExtremeRiskOutcome.Breakthrough ? 9f : 4f;
            f.State.Projects.GetProgress(f.Project.ProjectId).Restore(before, f.Project);
            ulong seed = FindLeapSeed(traits, actor.Actor, f.Project, expected);
            Require(store.Capture().Count == 0, "preparation must not consume the trait");
            Require(f.Leap(traits, actor.Actor, seed, out var risk, out var result)
                && risk.Outcome == expected, "leap variant: " + result.Message);
            float after = Math.Clamp(before + risk.ProgressDelta * f.Project.RequiredWork, 0f, 10f);
            Require(result.TotalProgress == after && result.AddedProgress == after - before
                && f.State.OutcomeSequence == 1 && store.Capture().Count == 1, "leap joint owners");
            var snapshot = f.Ledger.CaptureGameplayOutcomes();
            var exact = snapshot.exactOutcomes.Single();
            Require(exact.facts.Any(fact => fact.factId == ResearchWorkOutcomeIds.LeapHashFact.Value
                    && fact.value == risk.FixedRollHash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture)),
                "fixed roll truth");
            GameplayOutcomeId id = new(new GameplayOutcomeRunId(exact.runId), exact.sequence);
            Require(f.Ledger.TryProject(id, new NarrativePerspectiveContext(
                    new GameplayEntityId(new GameplayEntityKindId("character"), actor.Actor.Identity.PersistentId),
                    NarrativePerspectiveKind.Character, "ko-KR"), out var view)
                && view.Text.Contains("금단의 도약")
                && view.Text.Contains(expected == ExtremeRiskOutcome.Setback ? "후퇴"
                    : expected == ExtremeRiskOutcome.Normal ? "진척 변화 없음" : "돌파"), "risk projection");
            Require(!f.Leap(traits, actor.Actor, seed, out _, out _)
                && f.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 1, "once per project");
        }
        // A setback at the lower clamp still consumes the attempt and needs a receipt.
        using Fixture floor = new();
        ExtremeTraitRuntime floorTraits = new(new CharacterIdentityStateStore());
        ulong floorSeed = FindLeapSeed(floorTraits, actor.Actor, floor.Project, ExtremeRiskOutcome.Setback);
        Require(floor.Leap(floorTraits, actor.Actor, floorSeed, out _, out var floorResult)
            && floorResult.TotalProgress == 0 && floorResult.AddedProgress == 0
            && floor.State.OutcomeSequence == 1, "clamped setback is still a committed attempt");
    }

    private static void VerifyLeapFaultsCapacityAndSave()
    {
        using LeapActor actor = new();
        using Fixture f = new();
        CharacterIdentityStateStore store = new();
        ExtremeTraitRuntime traits = new(store);
        ulong seed = FindLeapSeed(traits, actor.Actor, f.Project, ExtremeRiskOutcome.Normal);
        traits.TryPrepareForbiddenResearchLeap(actor.Actor, f.Project.ProjectId.Value, seed, 30f, out var original);
        f.Faults.RejectCommit = true;
        Require(!f.Leap(traits, actor.Actor, seed, out _, out _), "leap reject");
        f.RequireUnchanged();
        Require(store.Capture().Count == 0, "leap rollback removes newly created state");
        f.Faults.RejectCommit = false;

        using (Fixture uncertain = new())
        {
            CharacterIdentityStateStore uncertainStore = new();
            ExtremeTraitRuntime uncertainTraits = new(uncertainStore);
            uncertain.Faults.ThrowBeforeCommit = true;
            Require(uncertain.Leap(
                    uncertainTraits,
                    actor.Actor,
                    seed,
                    out ExtremeRiskResolution uncertainResolution,
                    out BlueprintResearchWorkResult uncertainResult)
                && uncertainResult.PublicationPending
                && uncertain.State.HasPendingPublication
                && uncertainStore.Capture().Count == 1
                && uncertain.State.OutcomeSequence == 1,
                "leap commit uncertainty must retain the applied trait and frozen fact");
            uncertain.Faults.ThrowBeforeCommit = false;
            Require(uncertain.Transaction.TryRecoverPending(
                    uncertain.State,
                    out ResearchWorkAppliedFact recovered,
                    out string recoveredKey,
                    out string recoveryFailure)
                && recovered != null
                && recovered.Cause == ResearchWorkCause.ForbiddenLeap
                && recovered.Leap.FixedRollHash
                    == uncertainResolution.FixedRollHash
                && recoveredKey == uncertainResult.CommittedResultKey
                && recoveryFailure.Length == 0
                && !uncertain.State.HasPendingPublication,
                "leap pending fact did not recover with its exact roll");
        }

        f.Faults.ThrowAfterCommit = true;
        Require(f.Leap(traits, actor.Actor, seed, out var committed,
                out BlueprintResearchWorkResult committedResult)
            && committed.FixedRollHash == original.Resolution.FixedRollHash
            && !committedResult.PublicationPending
            && store.Capture().Count == 1 && f.State.OutcomeSequence == 1
            && !f.State.HasPendingPublication
            && f.Ledger.CaptureGameplayOutcomes().outbox.Count
                + f.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 1,
            "leap commit then throw");
        var identitySave = new CharacterNarrativeWorldSaveData { identityStates = store.Capture().ToList() };
        var restoredIdentity = JsonUtility.FromJson<CharacterNarrativeWorldSaveData>(JsonUtility.ToJson(identitySave));
        CharacterIdentityStateStore restoredStore = new();
        restoredStore.Restore(restoredIdentity.identityStates, actor.Actor.Progression.ResolveSelectedTraits());
        ExtremeTraitRuntime restoredTraits = new(restoredStore);
        Require(!restoredTraits.TryPrepareForbiddenResearchLeap(actor.Actor, f.Project.ProjectId.Value,
                seed, 30f, out _)
            && restoredTraits.GetActiveConditionIds(actor.Actor, 31f).Contains("state:forbidden-leap-aftermath"),
            "save preserves used attempt and aftermath");
        GameplayOutcomeRegistry restoredRegistry = CreateRegistry();
        GameplayOutcomeLedger restoredLedger = new(restoredRegistry, GameplayOutcomeBufferLimits.Default);
        var savedLedger = JsonUtility.FromJson<GameplayOutcomeLedgerSaveData>(
            JsonUtility.ToJson(f.Ledger.CaptureGameplayOutcomes()));
        var restore = restoredLedger.PrepareGameplayOutcomeRestore(savedLedger);
        restoredLedger.BeginRestoreCandidate();
        restoredLedger.PublishGameplayOutcomeRestore(restore);
        restoredLedger.PublishRestoreCandidate();
        restoredLedger.CompleteRestoreCandidate();
        GameplayOutcomeRecorder restoredRecorder = new(restoredLedger, restoredRegistry, new GameEventBus());
        restoredRecorder.RetryPendingDeliveries(10);
        restoredRecorder.RetryPendingDeliveries(10);
        var restoredExact = restoredLedger.CaptureGameplayOutcomes().exactOutcomes.Single();
        Require(restoredExact.facts.Any(fact => fact.factId == ResearchWorkOutcomeIds.LeapHashFact.Value
                && fact.value == committed.FixedRollHash.ToString("x16", System.Globalization.CultureInfo.InvariantCulture))
            && restoredLedger.CaptureGameplayOutcomes().outbox.Count == 0,
            "pending special receipt survives JSON restore and delivers once");
        f.Recorder.RetryPendingDeliveries(10);
        f.Recorder.RetryPendingDeliveries(10);
        Require(f.Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 1, "leap delivery does not rerun trait");

        using Fixture bounded = new(pageCount: 1);
        bounded.Faults.FailDelivery = true;
        Require(bounded.Apply(2, out _), "fill bounded page");
        CharacterIdentityStateStore boundedStore = new();
        ExtremeTraitRuntime boundedTraits = new(boundedStore);
        Require(!bounded.Leap(boundedTraits, actor.Actor, seed, out _, out var rejected)
            && rejected.CapacityDeferred && boundedStore.Capture().Count == 0
            && bounded.State.Projects.GetProgress(bounded.Project.ProjectId).Progress == 2
            && bounded.State.OutcomeSequence == 1, "capacity keeps trait, progress and sequence unchanged");

        // Rollback must retain an existing rule, not erase earlier projects/aftermath.
        CharacterTraitSO trait = actor.Actor.Progression.ResolveSelectedTraits().Single(value => value.id == 302);
        string preimage = JsonUtility.ToJson(new ForbiddenResearchLeapRuntimeState
            { usedProjectIds = new List<string> { "research:earlier" }, aftermathUntilSeconds = 17f });
        CharacterIdentityStateStore existing = new();
        existing.Set(actor.Actor.Identity.PersistentId, trait.DefinitionId.Value, ExtremeTraitRuntime.ForbiddenLeapRuleId, 1, preimage);
        using Fixture rollback = new();
        rollback.Faults.RejectCommit = true;
        Require(!rollback.Leap(new ExtremeTraitRuntime(existing), actor.Actor, seed, out _, out _), "existing rule rejection");
        Require(existing.TryGet(actor.Actor.Identity.PersistentId, trait.DefinitionId.Value,
            ExtremeTraitRuntime.ForbiddenLeapRuleId, out var old) && old.statePayload == preimage, "exact existing rule preimage");

        ExtremeTraitRuntime staleTraits = new(new CharacterIdentityStateStore());
        ForbiddenResearchLeapPreparation second = null;
        Require(staleTraits.TryPrepareForbiddenResearchLeap(actor.Actor, "research:stale", seed, 30f, out var first)
            && staleTraits.TryPrepareForbiddenResearchLeap(actor.Actor, "research:stale", seed, 30f, out second)
            && first.TryPublish() && !second.TryPublish(), "stale preparation rejects duplicate publication");
        first.Rollback();
        Require(second.IsCurrent, "rollback restores original preparation preimage");
    }

    private static void VerifyImmediateCompletion()
    {
        using Fixture f = new();
        f.Faults.RejectCommit = true;
        Require(!f.Transaction.TryCompleteImmediately(f.State, f.Shop, f.Catalog, f.Project, null, 1,
            out _, out _), "immediate rejection");
        f.RequireUnchanged();
        f.Faults.RejectCommit = false;
        Require(f.Transaction.TryCompleteImmediately(f.State, f.Shop, f.Catalog, f.Project, null, 1,
            out var completed, out _) && completed.Completed && f.Shop.IsBasicPurchaseUnlocked(f.Building.id)
            && f.Ledger.CaptureGameplayOutcomes().exactOutcomes.Single().facts
                .Any(fact => fact.value == "immediate-completion"), "immediate project receipt");
        Require(!f.Transaction.TryCompleteImmediately(f.State, f.Shop, f.Catalog, f.Project, null, 1,
            out _, out _) && f.State.OutcomeSequence == 1, "immediate terminal replay");
        FacilityBlueprintSO blueprint = ScriptableObject.CreateInstance<FacilityBlueprintSO>();
        try
        {
            blueprint.id = 73002;
            blueprint.name = "미등록 작업 설계도";
            blueprint.researchWorkRequired = 5;
            Require(f.Transaction.TryCompleteImmediately(f.State, f.Shop, f.Catalog, null, blueprint, 1,
                out var legacy, out _) && legacy.Completed && f.State.IsCompleted(blueprint), "immediate unqueued legacy");
        }
        finally { UnityEngine.Object.DestroyImmediate(blueprint); }
    }

    private static ulong FindLeapSeed(ExtremeTraitRuntime traits, CharacterActor actor,
        ResearchProjectSO project, ExtremeRiskOutcome expected)
    {
        for (ulong seed = 0; seed < 4096; seed++)
            if (traits.TryPrepareForbiddenResearchLeap(actor, project.ProjectId.Value, seed, 30f, out var prepared)
                && prepared.Resolution.Outcome == expected) return seed;
        throw new InvalidOperationException("Authored leap has no deterministic witness for " + expected);
    }

    private sealed class LeapActor : IDisposable
    {
        private readonly GameObject host = new("Research leap outcome fixture");
        public readonly CharacterActor Actor;
        public LeapActor()
        {
            Actor = host.AddComponent<CharacterActor>();
            Actor.PrepareForComposition();
            Actor.EnsureRuntimeState();
            CharacterAiEditorTestDependencies.Inject(host);
            Actor.Identity.SetPersistentId("character:research-leap-test");
            Actor.Initialize(AssetDatabase.FindAssets("t:CharacterSO")
                .Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<CharacterSO>)
                .First(value => value != null && value.id > 0 && value.species != null));
            Actor.Progression.ApplyPreparedIdentity("도약 연구자", "검증", new[] { 302 },
                CharacterPotentialGrade.Ordinary, 147, autoChooseDrafts: false);
        }
        public void Dispose() => UnityEngine.Object.DestroyImmediate(host);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException("Research outcome regression: " + message);
    }

    private sealed class Fixture : IDisposable
    {
        public readonly BlueprintResearchState State = new();
        public readonly FacilityShopUnlockState Shop = new();
        public readonly BuildingSO Building = ScriptableObject.CreateInstance<BuildingSO>();
        public readonly ResearchProjectSO Project = ScriptableObject.CreateInstance<ResearchProjectSO>();
        public readonly IFacilityShopCatalog Catalog;
        public readonly GameEventBus Events = new();
        public readonly GameplayOutcomeLedger Ledger;
        public readonly GameplayOutcomeRecorder Recorder;
        public readonly FaultRecorder Faults;
        public readonly DomainFactDispatcher Facts;
        public readonly BlueprintResearchOutcomeTransaction Transaction;

        public Fixture(int pageCount = 8)
        {
            Building.id = 73000;
            Building.name = "검증 시설";
            BlueprintUnlockCollection unlocks = new();
            unlocks.Add(new BlueprintBuildingUnlock { buildingId = Building.id });
            unlocks.Add(new BlueprintBasicPurchaseUnlock { buildingId = Building.id });
            unlocks.Add(new BlueprintRecipeUnlock { recipeId = "recipe:research-test" });
            Project.Configure("research:outcome-test", "기록 연구", "검증", default, 10,
                ResearchBlueprintRule.None, null, Array.Empty<ResearchProjectSO>(), unlocks);
            Catalog = new CatalogStub(Building);
            GameplayOutcomeRegistry registry = CreateRegistry();
            Ledger = new GameplayOutcomeLedger(registry, new GameplayOutcomeBufferLimits(
                smallPageCount: pageCount, largePageCount: 0, smallFacts: 32, largeFacts: 32));
            Recorder = new GameplayOutcomeRecorder(Ledger, registry, Events);
            Faults = new FaultRecorder(Recorder);
            Facts = ConnectResearchObserver(registry, Ledger, Faults);
            Transaction = new BlueprintResearchOutcomeTransaction(Facts, Facts);
        }
        public bool Apply(float work, out BlueprintResearchWorkResult result) => Transaction.TryApply(
            State, Shop, Catalog, Project, null, work, 1,
            "character:research-worker", "연구자", "facility:research-lab", "연구실", out result, out _);
        public bool Leap(ExtremeTraitRuntime traits, CharacterActor actor, ulong seed,
            out ExtremeRiskResolution resolution, out BlueprintResearchWorkResult result) =>
            Transaction.TryApplyForbiddenLeap(State, Shop, Catalog, Project, traits, actor,
                seed, 30f, 1, out resolution, out result, out _);
        public void RequireUnchanged() => Require(State.OutcomeSequence == 0
            && !State.HasPendingPublication
            && State.Projects.GetProgress(Project.ProjectId).Progress == 0
            && !State.Projects.IsCompleted(Project.ProjectId)
            && !Shop.IsBasicPurchaseUnlocked(Building.id) && State.UnlockedBuildingIds.Count == 0
            && State.UnlockedRecipeIds.Count == 0 && Ledger.CaptureGameplayOutcomes().outbox.Count == 0
            && Ledger.CaptureGameplayOutcomes().exactOutcomes.Count == 0, "rollback all owners");
        public void Dispose()
        {
            Facts.Dispose();
            UnityEngine.Object.DestroyImmediate(Project);
            UnityEngine.Object.DestroyImmediate(Building);
        }
    }

    private sealed class CatalogStub : IFacilityShopCatalog
    {
        private readonly BuildingSO building;
        public CatalogStub(BuildingSO building) => this.building = building;
        public IReadOnlyCollection<BuildingSO> Buildings => new[] { building };
        public IReadOnlyCollection<FacilityBlueprintSO> Blueprints => Array.Empty<FacilityBlueprintSO>();
        public BuildingSO FindBuildingById(int id) => id == building.id ? building : null;
    }

    private sealed class FaultRecorder : IGameplayOutcomeRecorder
    {
        private readonly IGameplayOutcomeRecorder inner;
        public bool RejectCommit, ThrowBeforeCommit, ThrowAfterCommit, FailDelivery;
        public FaultRecorder(IGameplayOutcomeRecorder inner) => this.inner = inner;
        public OutcomePrepareResult TryPrepare<T>(in T receipt, out PreparedOutcomeToken token) => inner.TryPrepare(receipt, out token);
        public OutcomePrepareResult TryReserve(in OutcomeWriteRequirements requirements, out PreparedOutcomeReservation reservation) => inner.TryReserve(requirements, out reservation);
        public OutcomePrepareResult TryWriteReserved<T>(in T receipt, in PreparedOutcomeReservation reservation, out PreparedOutcomeToken token) => inner.TryWriteReserved(receipt, reservation, out token);
        public void CancelReservation(in PreparedOutcomeReservation reservation) => inner.CancelReservation(reservation);
        public void CancelPrepared(in PreparedOutcomeToken token) => inner.CancelPrepared(token);
        public OutcomeCommitResult CommitPrepared(in PreparedOutcomeToken token, long revision, out CommittedOutcomeToken committed)
        {
            committed = default;
            if (RejectCommit) return new OutcomeCommitResult(OutcomeCommitCode.OwnerRevisionMismatch, "injected");
            if (ThrowBeforeCommit) throw new InvalidOperationException("injected-before-commit");
            var result = inner.CommitPrepared(token, revision, out committed);
            if (ThrowAfterCommit)
            {
                committed = default;
                throw new InvalidOperationException("injected-after-commit");
            }
            return result;
        }
        public OutcomeCommitResult CommitPreparedBatch(
            PreparedOutcomeToken[] prepared,
            long[] revisions,
            CommittedOutcomeToken[] committed)
        {
            if (RejectCommit)
                return new OutcomeCommitResult(
                    OutcomeCommitCode.OwnerRevisionMismatch,
                    "injected");
            if (ThrowBeforeCommit)
                throw new InvalidOperationException("injected-before-commit");
            OutcomeCommitResult result = inner.CommitPreparedBatch(
                prepared,
                revisions,
                committed);
            if (ThrowAfterCommit)
            {
                Array.Clear(committed, 0, committed.Length);
                throw new InvalidOperationException("injected-after-commit");
            }
            return result;
        }
        public OutcomeDeliveryResult TryDeliver(in CommittedOutcomeToken committed) => FailDelivery
            ? new OutcomeDeliveryResult(OutcomeDeliveryCode.PendingDeliveryFault, committed.OutcomeId, "injected")
            : inner.TryDeliver(committed);
        public OutcomeAcknowledgeResult Acknowledge(in CommittedOutcomeToken committed) => inner.Acknowledge(committed);
        public int RetryPendingDeliveries(int count) => inner.RetryPendingDeliveries(count);
    }
}
