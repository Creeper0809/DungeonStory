#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using VContainer;

/// <summary>Controlled setup, official full-world save/restore, no generated training rows.</summary>
public static class U1FullWorldOwnerBindingVerification
{
    public static void VerifyIsolationConfiguration()
    {
        string root = DungeonPersistencePaths.Root;
        string output = Path.GetDirectoryName(root);
        string project = Path.GetDirectoryName(Application.dataPath);
        Require(DungeonPersistencePaths.IsIsolatedVerification, "Persistence isolation is not active.");
        Require(DungeonPersistencePaths.ValidateVerificationRoot(root, output, project, true) == root,
            "Exact isolated root was not preserved.");
        RequireRejected(root, output, project, false);
        RequireRejected(Application.persistentDataPath, output, project, true);
        RequireRejected(project, output, project, true);
        RequireRejected(Path.Combine(output, "different-child"), output, project, true);
        RequireRejected(root, project, project, true);
        RequireRejected("relative", output, project, true);
        RequireRejected(root, null, project, true);
        Require(!string.Equals(root, Path.GetFullPath(Application.persistentDataPath), StringComparison.OrdinalIgnoreCase),
            "Verification was pointed at real player persistence.");
        bool refused = false;
        try { PlayModeVerificationPersistenceSnapshot.CaptureCurrent("u1-isolation-must-not-capture"); }
        catch (InvalidOperationException) { refused = true; }
        Require(refused, "Player snapshot capture remained available in isolated batch.");
        refused = false;
        try { PlayModeVerificationPersistenceSnapshot.Restore("u1-isolation-must-not-restore"); }
        catch (InvalidOperationException) { refused = true; }
        Require(refused, "Player snapshot restore remained available in isolated batch.");
        WriteNew("isolation-checks.txt", "PASS: exact root, seven invalid configurations, player snapshot capture/restore refusal.\n");
    }

    public static void Run(DungeonRuntimeLifetimeScope scope, IDungeonGameSaveService saves)
    {
        Require(DungeonPersistencePaths.IsIsolatedVerification, "World verification requires isolated persistence.");
        DungeonGameSaveData baseline = saves.Capture();
        List<string> evidence = new List<string>();
        bool passed = false;
        try
        {
            CharacterActor owner = ResolveOwner(scope);
            CharacterProgression progression = owner.Progression;
            int nextUnlock = progression.SkillSettings.activeUnlockLevels
                .Where(level => level > progression.Level).DefaultIfEmpty(-1).Min();
            Require(nextUnlock > progression.Level && nextUnlock <= CharacterProgression.MaxLevel,
                "Fixture requires a real upcoming active-skill unlock.");
            // Exercise the public progression command, not a fabricated ready draft.
            // This explicit controlled XP setup is not exported as gameplay history.
            while (progression.Level < nextUnlock)
                Require(progression.AddExperience(progression.ExperienceToNextLevel) > 0,
                    "Public progression command did not advance to the configured unlock.");
            CharacterSkillDraft pending = progression.Drafts.Single(draft => draft != null
                && draft.kind == CharacterSkillKind.Active && draft.unlockLevel == nextUnlock
                && !draft.isReady && !draft.permanentlyChosen
                && draft.presentationState != CharacterSkillPresentationState.InvalidatedOwner);
            Require(pending.ownerBinding != null && pending.ownerBinding.Matches(progression, out _),
                "Live unlock request is not bound to its actual owner.");
            string expectedId = owner.Identity.PersistentId;
            long expectedLifecycle = owner.Lifecycle.LifecycleRevision;
            string expectedKey = pending.requestKey;
            string expectedBinding = JsonUtility.ToJson(pending.ownerBinding);
            string expectedNarrative = JsonUtility.ToJson(progression.NarrativeLedger);
            bool expectedSubmitted = pending.requestSubmitted;
            string expectedOffers = JsonUtility.ToJson(pending.Clone());
            DungeonGameSaveData captured = saves.Capture();
            DungeonCharacterWorldSaveData characters = DungeonSaveSectionPayload
                .ReadOrNew<DungeonCharacterWorldSaveData>(captured, CharacterWorldSaveSection.Id);
            DungeonCharacterSaveData savedOwner = characters.actors.Single(actor => actor.isOwner);
            Require(savedOwner.persistentId == expectedId && savedOwner.lifecycleRevision == expectedLifecycle,
                "Official character DTO did not capture the exact owner lifecycle revision.");
            string healthBefore = JsonUtility.ToJson(DungeonSaveSectionPayload
                .ReadOrNew<DungeonCharacterBodyHealthSaveData>(captured, CharacterBodyHealthSaveSection.Id));
            string encoded = saves.ToJson(captured);
            WriteNew("u1-live-pending-before.json", encoded);
            Require(saves.TryRestore(saves.FromJson(encoded), out DungeonGameRestoreReport restored),
                "Official world pending restore failed: " + string.Join(" | ", restored.Errors));
            CharacterActor restoredOwner = ResolveOwner(scope);
            CharacterSkillDraft restoredDraft = restoredOwner.Progression.Drafts.Single(draft =>
                draft != null && draft.requestKey == expectedKey);
            Require(!ReferenceEquals(owner, restoredOwner), "World restore did not publish a fresh owner.");
            Require(restoredOwner.Identity.PersistentId == expectedId
                    && restoredOwner.Lifecycle.LifecycleRevision == expectedLifecycle
                    && restoredOwner.CurrentLifecycleState == savedOwner.lifecycleState,
                "Published world owner changed identity/lifecycle on restore.");
            Require(!restoredDraft.isReady && !restoredDraft.permanentlyChosen
                    && restoredDraft.ownerBinding != null
                    && restoredDraft.ownerBinding.Matches(restoredOwner.Progression, out _)
                    && JsonUtility.ToJson(restoredDraft.ownerBinding) == expectedBinding,
                "Pending request was completed, rebound or invalidated during official restore.");
            Require(JsonUtility.ToJson(restoredOwner.Progression.NarrativeLedger) == expectedNarrative,
                "World restore invented or changed character narrative facts.");
            // Transient dispatch bookkeeping may reset; mechanics and identity may not.
            CharacterSkillDraft normalizedRestored = restoredDraft.Clone();
            normalizedRestored.requestSubmitted = expectedSubmitted;
            Require(JsonUtility.ToJson(normalizedRestored) == expectedOffers,
                "Restored pending changed frozen mechanics, candidates or request metadata.");
            ICharacterWorldSaveService world = scope.Container.Resolve<ICharacterWorldSaveService>();
            Require(world.TryGetRestoredActor(expectedId, out CharacterActor indexed)
                    && ReferenceEquals(indexed, restoredOwner),
                "World actor registry is not bound to the published request owner.");
            DungeonGameSaveData recaptured = saves.Capture();
            string healthAfter = JsonUtility.ToJson(DungeonSaveSectionPayload
                .ReadOrNew<DungeonCharacterBodyHealthSaveData>(recaptured, CharacterBodyHealthSaveSection.Id));
            Require(healthAfter == healthBefore, "Body-health aggregate changed across pending world restore.");
            WriteNew("u1-live-pending-after.json", saves.ToJson(recaptured));
            evidence.Add("ownerId=" + expectedId);
            evidence.Add("lifecycleRevision=" + expectedLifecycle);
            evidence.Add("pendingRequestKey=" + expectedKey);
            evidence.Add("officialWorldRestore=true;freshPublishedOwner=true;registryExact=true;healthExact=true;pendingBindingExact=true;frozenDraftExact=true");
            passed = true;
        }
        catch (Exception exception)
        {
            evidence.Add("failure=" + exception);
            throw;
        }
        finally
        {
            bool baselineRestored = saves.TryRestore(baseline, out DungeonGameRestoreReport restored);
            evidence.Insert(0, passed && baselineRestored ? "RESULT=PASS" : "RESULT=FAIL");
            evidence.Add("baselineRestored=" + baselineRestored);
            evidence.Add("scope=controlled-live-skill-pending;not-source9");
            evidence.Add("humanApprovalClaimed=false;trainingEligible=false");
            WriteNew("u1-owner-binding-world-report.txt", string.Join("\n", evidence));
            Require(baselineRestored, "World baseline restore failed: " + string.Join(" | ", restored.Errors));
        }
    }

    public static void RunTrait(DungeonRuntimeLifetimeScope scope, IDungeonGameSaveService saves)
    {
        Require(DungeonPersistencePaths.IsIsolatedVerification, "Trait verification requires isolated persistence.");
        DungeonGameSaveData baseline = saves.Capture();
        CharacterAcquiredTraitManifestationRuntime runtime = scope.Container
            .Resolve<CharacterAcquiredTraitManifestationRuntime>();
        List<string> evidence = new();
        List<CharacterAcquiredTraitManifestationDiagnostic> diagnostics = new();
        Action<CharacterAcquiredTraitManifestationDiagnostic> observe = value => diagnostics.Add(value);
        runtime.DiagnosticPublished += observe;
        bool passed = false;
        try
        {
            CharacterActor owner = ResolveOwner(scope);
            CharacterAcquiredTraitAggregateState initial = owner.Progression.CaptureAcquiredTraitState();
            Require(initial.pendingRequests.Count == 0 && initial.ActiveCount == 0
                    && initial.processedMilestones.Count == 0,
                "Trait fixture requires an unprocessed owner, not a cleared or rewritten aggregate.");
            int day = scope.Container.Resolve<IGameCalendar>().Day;
            // Explicit controlled legacy history, not executed gameplay and never a training source.
            // Disable passive side effects; this test concerns the real milestone producer and save path.
            for (int index = 0; index < 33; index++)
                owner.Progression.RecordNarrative(CharacterNarrativeDomain.Work,
                    $"work:qa-world-pending-{index:00}", "target:qa-world-pending", "completed",
                    day: day, triggerPassives: false);
            runtime.Tick();
            CharacterAcquiredTraitAggregateState submitted = owner.Progression.CaptureAcquiredTraitState();
            Require(submitted.pendingRequests.Count == 1 && submitted.ActiveCount == 0
                    && submitted.processedMilestones.Count == 0,
                "Actual trait producer did not persist exactly one unconsumed pending request. "
                + string.Join(" | ", diagnostics.Select(value => value.Status + ":" + value.Detail)));
            CharacterAcquiredTraitPendingRequestState pending = submitted.pendingRequests.Single();
            string ownerId = owner.Identity.PersistentId;
            long lifecycleRevision = owner.Lifecycle.LifecycleRevision;
            Require(pending.manifestationMilestone == 3 && pending.ownerBinding != null
                    && pending.ownerBinding.Matches(owner.Progression, out _)
                    && diagnostics.Any(value => value.TargetPersistentId == ownerId
                        && value.RequestId == pending.requestId
                        && value.Status == CharacterAcquiredTraitManifestationStatus.SubmissionSucceeded),
                "Milestone request did not originate from the registered live producer.");
            string expectedAggregate = JsonUtility.ToJson(submitted);
            string expectedNarrative = JsonUtility.ToJson(owner.Progression.NarrativeLedger);
            DungeonGameSaveData captured = saves.Capture();
            string expectedHealth = JsonUtility.ToJson(DungeonSaveSectionPayload
                .ReadOrNew<DungeonCharacterBodyHealthSaveData>(captured, CharacterBodyHealthSaveSection.Id));
            string encoded = saves.ToJson(captured);
            WriteNew("u1-trait-pending-before.json", encoded);
            Require(saves.TryRestore(saves.FromJson(encoded), out DungeonGameRestoreReport restored),
                "Official world trait restore failed: " + string.Join(" | ", restored.Errors));
            CharacterActor restoredOwner = ResolveOwner(scope);
            Require(!ReferenceEquals(owner, restoredOwner)
                    && restoredOwner.Identity.PersistentId == ownerId
                    && restoredOwner.Lifecycle.LifecycleRevision == lifecycleRevision,
                "Trait world restore did not publish the exact lifecycle in a fresh owner.");
            Require(JsonUtility.ToJson(restoredOwner.Progression.CaptureAcquiredTraitState()) == expectedAggregate,
                "Official restore changed trait pending, evidence, formula offers or milestone state.");
            diagnostics.Clear();
            runtime.Tick();
            Require(diagnostics.Any(value => value.TargetPersistentId == ownerId
                    && value.RequestId == pending.requestId
                    && value.Status == CharacterAcquiredTraitManifestationStatus.PendingRestored),
                "Live producer did not resume the exact persisted trait request. "
                + string.Join(" | ", diagnostics.Select(value => value.Status + ":" + value.Detail)));
            runtime.Tick();
            Require(JsonUtility.ToJson(restoredOwner.Progression.CaptureAcquiredTraitState()) == expectedAggregate
                    && restoredOwner.Progression.CaptureAcquiredTraitState().pendingRequests.Single()
                        .ownerBinding.Matches(restoredOwner.Progression, out _),
                "Repeated live observation rebound the request or consumed a milestone/effect.");
            Require(JsonUtility.ToJson(restoredOwner.Progression.NarrativeLedger) == expectedNarrative,
                "Trait restore/resume changed original narrative evidence.");
            Require(scope.Container.Resolve<ICharacterWorldSaveService>()
                    .TryGetRestoredActor(ownerId, out CharacterActor indexed)
                    && ReferenceEquals(indexed, restoredOwner),
                "Trait request owner is not the actor in the restored world registry.");
            DungeonGameSaveData recaptured = saves.Capture();
            Require(JsonUtility.ToJson(DungeonSaveSectionPayload
                    .ReadOrNew<DungeonCharacterBodyHealthSaveData>(recaptured, CharacterBodyHealthSaveSection.Id))
                    == expectedHealth, "Trait pending restore/resume changed body-health state.");
            WriteNew("u1-trait-pending-after.json", saves.ToJson(recaptured));
            evidence.Add("ownerId=" + ownerId + ";lifecycleRevision=" + lifecycleRevision);
            evidence.Add("pendingRequestId=" + pending.requestId);
            evidence.Add("pendingRequestKey=" + pending.requestKey);
            evidence.Add("officialWorldRestore=true;freshPublishedOwner=true;liveProducerResumed=true;repeatedTickStable=true");
            evidence.Add("aggregateExact=true;narrativeExact=true;healthExact=true;registryExact=true;milestoneConsumed=false");
            passed = true;
        }
        catch (Exception exception)
        {
            evidence.Add("failure=" + exception);
            throw;
        }
        finally
        {
            runtime.DiagnosticPublished -= observe;
            bool baselineRestored = saves.TryRestore(baseline, out DungeonGameRestoreReport restored);
            evidence.Insert(0, passed && baselineRestored ? "RESULT=PASS" : "RESULT=FAIL");
            evidence.Add("baselineRestored=" + baselineRestored);
            evidence.Add("scope=controlled-legacy-history;registered-live-trait-producer;no-model-completion;not-source9");
            evidence.Add("humanApprovalClaimed=false;trainingEligible=false");
            WriteNew("u1-trait-binding-world-report.txt", string.Join("\n", evidence));
            Require(baselineRestored, "Trait world baseline restore failed: " + string.Join(" | ", restored.Errors));
        }
    }

    public static void VerifyDurableAutosave(DungeonRuntimeLifetimeScope scope)
    {
        Require(DungeonPersistencePaths.IsIsolatedVerification, "Durable verification requires isolated persistence.");
        string path = scope.Container.Resolve<IDungeonSaveSlotCatalog>()
            .GetPath(DungeonGameSaveSlotService.AutoSaveSlot);
        Require(Path.GetFullPath(path).StartsWith(DungeonPersistencePaths.Root + Path.DirectorySeparatorChar,
            StringComparison.OrdinalIgnoreCase), "Autosave escaped the isolated root.");
        bool saved = scope.Container.Resolve<IDungeonSaveCommandService>().TryAutoSave(out string message);
        WriteNew("u1-durable-save-report.txt", (saved ? "RESULT=PASS" : "RESULT=FAIL")
            + "\nactualAutosave=true;durableCallbacksRequired=true\n" + message);
        Require(saved && File.Exists(path), "Actual durable autosave failed: " + message);
    }

    private static CharacterActor ResolveOwner(DungeonRuntimeLifetimeScope scope)
    {
        Require(scope.Container.Resolve<IOwnerRunManagerProvider>().TryGetManager(out OwnerRunManager manager)
                && manager.CurrentOwnerActor != null, "Actual world owner is missing.");
        return manager.CurrentOwnerActor;
    }

    private static void RequireRejected(string root, string output, string project, bool batch)
    {
        bool rejected = false;
        try { DungeonPersistencePaths.ValidateVerificationRoot(root, output, project, batch); }
        catch (InvalidOperationException) { rejected = true; }
        Require(rejected, "Invalid verification root was accepted: " + root);
    }

    private static void WriteNew(string name, string value)
    {
        string path = Path.Combine(Path.GetDirectoryName(DungeonPersistencePaths.Root), name);
        using StreamWriter writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write));
        writer.Write(value);
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }
}
#endif
