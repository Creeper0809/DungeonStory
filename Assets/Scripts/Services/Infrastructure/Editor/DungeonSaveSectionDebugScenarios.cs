#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Infrastructure;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class DungeonSaveSectionDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Save/Run Strategic Section Contracts")]
    public static void RunFromMenu()
    {
        if (!RunAll(true))
        {
            Debug.LogError("Strategic save section contracts failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        List<string> failures = new List<string>();
        Verify("dependency order", VerifyDependencyOrder, failures);
        Verify("duplicate id rejected", VerifyDuplicateRejected, failures);
        Verify("missing dependency rejected", VerifyMissingDependencyRejected, failures);
        Verify("cycle rejected", VerifyCycleRejected, failures);
        Verify("capture and restore", VerifyCaptureRestore, failures);
        Verify("aggregate restore publishes one root", VerifyAggregateRestorePublishesOneRoot, failures);
        Verify("failed staging leaves live state untouched", VerifyFailedStageLeavesLiveStateUntouched, failures);
        Verify("topological staged validation retains dependency candidates", VerifyTopologicalStagedValidationRetainsDependencyCandidates, failures);
        Verify("pre-stage participant receives staged writes", VerifyPreStageParticipantReceivesStagedWrites, failures);
        Verify("pre-stage participant discards after later stage failure", VerifyPreStageParticipantDiscardsAfterLaterStageFailure, failures);
        Verify("post-stage physical lifetime begins after candidate index", VerifyPostStagePhysicalLifetimeBeginsAfterCandidateIndex, failures);
        Verify("rollback image is captured before pre-stage begin", VerifyRollbackImageCapturedBeforePreStageBegin, failures);
        Verify("failed commit rolls back live state", VerifyFailedCommitRollsBack, failures);
        Verify("failed commit discards aggregate candidate", VerifyFailedCommitDiscardsAggregateCandidate, failures);
        Verify("copy-on-write mutation leaves live root untouched", VerifyCopyOnWriteMutationLeavesLiveRootUntouched, failures);
        Verify("random stream strict boundary", VerifyRandomStreamStrictBoundary, failures);
        Verify("random stream handle follows published root", VerifyRandomStreamHandleFollowsPublishedRoot, failures);
        Verify("failed restore preserves live random stream", VerifyFailedRestorePreservesLiveRandomStream, failures);
        Verify("restore participant publishes after commit", VerifyRestoreParticipantPublishesAfterCommit, failures);
        Verify("restore participant order is deterministic", VerifyRestoreParticipantOrder, failures);
        Verify("participant publish failure rolls back in reverse order", VerifyParticipantPublishFailureRollsBackInReverseOrder, failures);
        Verify("restore candidate index is detached and scoped", VerifyRestoreCandidateIndex, failures);
        Verify("restore participant discards failed candidate", VerifyRestoreParticipantDiscardsFailedCandidate, failures);
        Verify("duplicate restore participant rejected", VerifyDuplicateRestoreParticipantRejected, failures);
        Verify("unknown required section rejected", VerifyUnknownRequiredRejected, failures);

        foreach (string failure in failures)
        {
            Debug.LogError(failure);
        }

        if (failures.Count == 0 && logSuccess)
        {
            Debug.Log("Strategic save section contracts passed.");
        }

        return failures.Count == 0;
    }

    public static bool VerifyLiveCapture(out string details)
    {
        details = string.Empty;
        if (!Application.isPlaying)
        {
            details = "PlayMode required";
            return false;
        }

        DungeonRuntimeLifetimeScope scope =
            UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include);
        if (scope == null || scope.Container == null)
        {
            details = "gameplay scope missing";
            return false;
        }

        DungeonGameSaveData save = scope.Container.Resolve<IDungeonGameSaveService>().Capture();
        string[] required =
        {
            PhysicalItemsSaveSection.Id,
            WorkOrdersSaveSection.Id,
            WildlifeSaveSection.Id,
            SurvivalResourcesSaveSection.Id,
            DarkSurvivalSaveSection.Id,
            CharacterBodyHealthSaveSection.Id,
            CombatEquipmentSaveSection.Id,
            CharacterMedicalSaveSection.Id,
            DefenseTacticalSaveSection.Id,
            EquipmentMaintenanceSaveSection.Id,
            CharacterCombatCommandSaveSection.Id,
            ExteriorActivitySaveSection.Id,
            OffenseAggregateSaveSection.Id,
            InvasionSaveSection.Id,
            OperatingDaySettlementSaveSection.Id,
            EventAlertSaveSection.Id,
            RunFlowSaveSection.Id,
            DungeonDebugSaveSection.Id,
            BlueprintResearchSaveSection.Id,
            FacilityShopSaveSection.Id,
            RegularCustomerSaveSection.Id,
            StaffDiscontentSaveSection.Id,
            CodexSaveSection.Id,
            RunVariableSaveSection.Id,
            MetaProgressionSaveSection.Id,
            ModularFacilityWorldSaveSection.Id,
            CharacterWorldSaveSection.Id
        };
        string ids = string.Join(",", save.sections.Select(section => section.sectionId));
        details = $"version={save.version}; sections={save.sections.Count}; ids={ids}";
        return save.version == DungeonGameSaveData.CurrentVersion
            && required.All(id => save.sections.Any(section =>
                string.Equals(section.sectionId, id, StringComparison.Ordinal)));
    }

    public static bool VerifyLiveRoundTrip(out string details)
    {
        details = string.Empty;
        if (!Application.isPlaying)
        {
            details = "PlayMode required";
            return false;
        }

        DungeonRuntimeLifetimeScope scope =
            UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include);
        if (scope == null || scope.Container == null)
        {
            details = "gameplay scope missing";
            return false;
        }

        IDungeonGameSaveService saveService =
            scope.Container.Resolve<IDungeonGameSaveService>();
        IWorldItemStackRuntime itemRuntime =
            scope.Container.Resolve<IWorldItemStackRuntime>();
        DungeonGameSaveData baseline = saveService.Capture();
        try
        {
            if (!itemRuntime.SpawnStockAtDropoff(
                    StockCategory.General,
                    3,
                    "Strategic 왕복 검증",
                    out int spawned)
                || spawned != 3)
            {
                details = $"test item spawn failed: {spawned}";
                return false;
            }

            DungeonGameSaveData before = saveService.Capture();
            if (!saveService.TryRestore(before, out DungeonGameRestoreReport report)
                || report == null
                || !report.Success)
            {
                details = report == null
                    ? "restore report missing"
                    : string.Join(" | ", report.Errors);
                return false;
            }

            DungeonGameSaveData after = saveService.Capture();
            DungeonPhysicalItemSaveData baselineItems =
                DungeonSaveSectionPayload.ReadOrNew<DungeonPhysicalItemSaveData>(
                    baseline,
                    PhysicalItemsSaveSection.Id);
            DungeonPhysicalItemSaveData beforePhysicalItems =
                DungeonSaveSectionPayload.ReadOrNew<DungeonPhysicalItemSaveData>(
                    before,
                    PhysicalItemsSaveSection.Id);
            DungeonPhysicalItemSaveData afterPhysicalItems =
                DungeonSaveSectionPayload.ReadOrNew<DungeonPhysicalItemSaveData>(
                    after,
                    PhysicalItemsSaveSection.Id);
            string[] beforeIds = before.sections
                .Select(section => section.sectionId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            string[] afterIds = after.sections
                .Select(section => section.sectionId)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToArray();
            string beforeItems = BuildItemSignature(beforePhysicalItems);
            string afterItems = BuildItemSignature(afterPhysicalItems);
            details =
                $"version={before.version}->{after.version}; "
                + $"sections={string.Join(",", afterIds)}; "
                + $"itemStacks={beforePhysicalItems.stacks.Count}->{afterPhysicalItems.stacks.Count}; "
                + $"itemDiff={BuildItemSignatureDiff(beforePhysicalItems, afterPhysicalItems)}";
            return before.version == DungeonGameSaveData.CurrentVersion
                && after.version == DungeonGameSaveData.CurrentVersion
                && beforeIds.SequenceEqual(afterIds)
                && beforePhysicalItems.stacks.Count > baselineItems.stacks.Count
                && string.Equals(beforeItems, afterItems, StringComparison.Ordinal);
        }
        finally
        {
            saveService.TryRestore(baseline, out _);
        }
    }

    private static string BuildItemSignature(DungeonPhysicalItemSaveData items)
    {
        return string.Join(
            "|",
            (items?.stacks ?? new List<WorldItemStackSaveData>())
                .OrderBy(stack => stack.stackId, StringComparer.Ordinal)
                .Select(stack =>
                    $"{stack.stackId}:{stack.itemInstanceId}:{stack.itemId}:{stack.quantity}:{stack.state}:"
                    + $"{stack.gridX},{stack.gridY}:{stack.reservedByPersistentId}:"
                    + $"{stack.destinationId}:{stack.forbidden}"));
    }

    private static string BuildItemSignatureDiff(
        DungeonPhysicalItemSaveData before,
        DungeonPhysicalItemSaveData after)
    {
        Dictionary<string, string> beforeById =
            (before?.stacks ?? new List<WorldItemStackSaveData>())
                .ToDictionary(
                    stack => stack.stackId,
                    stack => BuildItemSignature(new DungeonPhysicalItemSaveData
                    {
                        stacks = new List<WorldItemStackSaveData> { stack }
                    }),
                    StringComparer.Ordinal);
        Dictionary<string, string> afterById =
            (after?.stacks ?? new List<WorldItemStackSaveData>())
                .ToDictionary(
                    stack => stack.stackId,
                    stack => BuildItemSignature(new DungeonPhysicalItemSaveData
                    {
                        stacks = new List<WorldItemStackSaveData> { stack }
                    }),
                    StringComparer.Ordinal);
        return string.Join(
            " || ",
            beforeById.Keys
                .Union(afterById.Keys)
                .OrderBy(id => id, StringComparer.Ordinal)
                .Where(id => !beforeById.TryGetValue(id, out string beforeValue)
                    || !afterById.TryGetValue(id, out string afterValue)
                    || !string.Equals(
                        beforeValue,
                        afterValue,
                        StringComparison.Ordinal))
                .Select(id =>
                    $"{id}:before={GetValue(beforeById, id)};after={GetValue(afterById, id)}"));
    }

    private static string GetValue(
        IReadOnlyDictionary<string, string> values,
        string id)
    {
        return values.TryGetValue(id, out string value)
            ? value
            : "<missing>";
    }

    private static bool VerifyDependencyOrder()
    {
        FakeSection items = new FakeSection(
            "items",
            DungeonSaveRestorePhase.Items);
        FakeSection work = new FakeSection(
            "work",
            DungeonSaveRestorePhase.RuntimeState,
            "items");
        FakeSection survival = new FakeSection(
            "survival",
            DungeonSaveRestorePhase.LateRuntimeState,
            "work");
        DungeonSaveSectionRegistry registry =
            new DungeonSaveSectionRegistry(new IDungeonSaveSection[]
            {
                survival,
                work,
                items
            }, new DungeonRuntimeAggregateRootStore());

        return string.Join(",", registry.OrderedSections.Select(section => section.SectionId))
            == "items,work,survival";
    }

    private static bool VerifyDuplicateRejected()
    {
        return Throws(() => new DungeonSaveSectionRegistry(new IDungeonSaveSection[]
        {
            new FakeSection("same", DungeonSaveRestorePhase.Items),
            new FakeSection("same", DungeonSaveRestorePhase.Items)
        }, new DungeonRuntimeAggregateRootStore()));
    }

    private static bool VerifyMissingDependencyRejected()
    {
        return Throws(() => new DungeonSaveSectionRegistry(new IDungeonSaveSection[]
        {
            new FakeSection("work", DungeonSaveRestorePhase.RuntimeState, "items")
        }, new DungeonRuntimeAggregateRootStore()));
    }

    private static bool VerifyCycleRejected()
    {
        return Throws(() => new DungeonSaveSectionRegistry(new IDungeonSaveSection[]
        {
            new FakeSection("a", DungeonSaveRestorePhase.RuntimeState, "b"),
            new FakeSection("b", DungeonSaveRestorePhase.RuntimeState, "a")
        }, new DungeonRuntimeAggregateRootStore()));
    }

    private static bool VerifyCaptureRestore()
    {
        List<string> restored = new List<string>();
        FakeSection first = new FakeSection(
            "first",
            DungeonSaveRestorePhase.Items,
            restored: restored);
        FakeSection second = new FakeSection(
            "second",
            DungeonSaveRestorePhase.RuntimeState,
            new[] { "first" },
            restored);
        DungeonSaveSectionRegistry registry =
            new DungeonSaveSectionRegistry(
                new IDungeonSaveSection[] { second, first },
                new DungeonRuntimeAggregateRootStore());
        List<DungeonSaveSectionEnvelope> envelopes = registry.CaptureAll();
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();

        return envelopes.Count == 2
            && registry.RestoreAll(envelopes, report)
            && report.Success
            && string.Join(",", restored) == "first,second";
    }

    private static bool VerifyFailedCommitRollsBack()
    {
        TransactionFakeSection first = new TransactionFakeSection("first", 10);
        TransactionFakeSection second = new TransactionFakeSection("second", 20, "first");
        TransactionFakeSection last = new TransactionFakeSection("last", 30, "second");
        DungeonSaveSectionRegistry registry = new DungeonSaveSectionRegistry(
            new IDungeonSaveSection[] { last, second, first },
            new DungeonRuntimeAggregateRootStore());

        List<DungeonSaveSectionEnvelope> incoming = registry.CaptureAll();
        incoming.Single(item => item.sectionId == "first").payloadJson = "{\"value\":101}";
        incoming.Single(item => item.sectionId == "second").payloadJson = "{\"value\":202}";
        incoming.Single(item => item.sectionId == "last").payloadJson = "{\"value\":303}";
        last.FailNextRestore = true;

        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        bool restored = registry.RestoreAll(incoming, report);
        return !restored
            && !report.Success
            && first.Value == 10
            && second.Value == 20
            && last.Value == 30;
    }

    private static bool VerifyAggregateRestorePublishesOneRoot()
    {
        DungeonRuntimeAggregateRootStore rootStore =
            new DungeonRuntimeAggregateRootStore();
        rootStore.Replace(new AggregateTransactionState { Value = 10 });
        AggregateTransactionFakeSection aggregate =
            new AggregateTransactionFakeSection("aggregate", rootStore);
        DungeonSaveSectionRegistry registry = new DungeonSaveSectionRegistry(
            new IDungeonSaveSection[] { aggregate },
            rootStore);
        List<DungeonSaveSectionEnvelope> incoming = registry.CaptureAll();
        incoming[0].payloadJson = "{\"value\":101}";

        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        return registry.RestoreAll(incoming, report)
            && report.Success
            && rootStore.PublishedRestoreRevision == 1
            && rootStore.GetOrCreate(() => new AggregateTransactionState()).Value == 101;
    }

    private static bool VerifyFailedStageLeavesLiveStateUntouched()
    {
        StagedTransactionFakeSection first =
            new StagedTransactionFakeSection("first", 10);
        StagedTransactionFakeSection second =
            new StagedTransactionFakeSection("second", 20, "first");
        StagedTransactionFakeSection last =
            new StagedTransactionFakeSection("last", 30, "second");
        DungeonSaveSectionRegistry registry = new DungeonSaveSectionRegistry(
            new IDungeonSaveSection[] { last, second, first },
            new DungeonRuntimeAggregateRootStore());

        List<DungeonSaveSectionEnvelope> incoming = registry.CaptureAll();
        incoming.Single(item => item.sectionId == "first").payloadJson = "{\"value\":101}";
        incoming.Single(item => item.sectionId == "second").payloadJson = "{\"value\":202}";
        incoming.Single(item => item.sectionId == "last").payloadJson = "{\"value\":303}";
        last.FailNextStage = true;

        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        bool restored = registry.RestoreAll(incoming, report);
        return !restored
            && !report.Success
            && first.Value == 10
            && second.Value == 20
            && last.Value == 30;
    }

    private static bool VerifyTopologicalStagedValidationRetainsDependencyCandidates()
    {
        TopologicalCandidateIndex validIndex = new();
        TopologicalSourceSection validSource = new(validIndex, 17);
        TopologicalDependentSection validDependent = new(validIndex, 17);

        // Standalone validation remains a full semantic check and must release
        // the temporary candidate it creates.
        DungeonGameRestoreReport standaloneReport = new();
        validSource.ValidatePayload(
            validSource.Capture(),
            validSource.SectionVersion,
            standaloneReport);
        if (!standaloneReport.Success || validIndex.IsAvailable)
        {
            return false;
        }

        DungeonSaveSectionRegistry validRegistry = new(
            new IDungeonSaveSection[] { validDependent, validSource },
            new DungeonRuntimeAggregateRootStore());
        DungeonGameRestoreReport validReport = new();
        bool validRestored = validRegistry.RestoreAll(
            validRegistry.CaptureAll(),
            validReport);
        if (!validRestored
            || !validReport.Success
            || validSource.PublishedValue != 17
            || validDependent.PublishedValue != 17
            || validSource.LocalValidationCount != 2
            || validDependent.LocalValidationCount != 1
            || validIndex.IsAvailable)
        {
            return false;
        }

        // A dependent mismatch fails while staging, publishes nothing, and
        // discards the already retained source candidate.
        TopologicalCandidateIndex invalidIndex = new();
        TopologicalSourceSection invalidSource = new(invalidIndex, 23);
        TopologicalDependentSection invalidDependent = new(invalidIndex, 24);
        DungeonSaveSectionRegistry invalidRegistry = new(
            new IDungeonSaveSection[] { invalidDependent, invalidSource },
            new DungeonRuntimeAggregateRootStore());
        DungeonGameRestoreReport invalidReport = new();
        bool invalidRestored = invalidRegistry.RestoreAll(
            invalidRegistry.CaptureAll(),
            invalidReport);
        return !invalidRestored
            && !invalidReport.Success
            && invalidSource.PublishedValue == 0
            && invalidDependent.PublishedValue == 0
            && !invalidIndex.IsAvailable;
    }

    private static bool VerifyPreStageParticipantReceivesStagedWrites()
    {
        List<string> events = new();
        PreStageAuthorityParticipant authority = new(10, events);
        PreStageAuthoritySection section = new(
            "qa.prestage-write",
            authority,
            capturedValue: 20,
            events: events);
        DungeonSaveSectionRegistry registry = new(
            new IDungeonSaveSection[] { section },
            new DungeonRuntimeAggregateRootStore(),
            new IDungeonRestoreTransactionParticipant[] { authority });
        DungeonGameRestoreReport report = new();

        bool restored = registry.RestoreAll(registry.CaptureAll(), report);
        bool passed = restored
            && report.Success
            && section.ObservedLiveValueDuringStage == 10
            && section.ObservedLiveValueDuringCommit == 10
            && section.PublishedValue == 20
            && authority.LiveValue == 20
            && authority.BeginCount == 1
            && authority.PublishCount == 1
            && authority.CompleteCount == 1
            && authority.DiscardCount == 0
            && !authority.HasCandidate
            && string.Join(",", events) ==
                "begin:220.world.facility-buffer-destinations,"
                + "stage:qa.prestage-write,commit:qa.prestage-write,"
                + "publish:220.world.facility-buffer-destinations,"
                + "complete:220.world.facility-buffer-destinations";
        if (passed)
        {
            Debug.Log("SAVE_PRESTAGE_PARTICIPANT_STAGE_WRITE_PASS");
        }
        return passed;
    }

    private static bool VerifyPreStageParticipantDiscardsAfterLaterStageFailure()
    {
        List<string> events = new();
        PreStageAuthorityParticipant authority = new(10, events);
        PreStageAuthoritySection writer = new(
            "qa.prestage-source",
            authority,
            capturedValue: 20,
            events: events);
        PreStageFailingSection failing = new(
            "qa.prestage-dependent-failure",
            writer.SectionId,
            events);
        DungeonSaveSectionRegistry registry = new(
            new IDungeonSaveSection[] { failing, writer },
            new DungeonRuntimeAggregateRootStore(),
            new IDungeonRestoreTransactionParticipant[] { authority });
        DungeonGameRestoreReport report = new();

        bool restored = registry.RestoreAll(registry.CaptureAll(), report);
        bool passed = !restored
            && !report.Success
            && authority.LiveValue == 10
            && authority.BeginCount == 1
            && authority.PublishCount == 0
            && authority.CompleteCount == 0
            && authority.DiscardCount == 1
            && !authority.HasCandidate
            && writer.PublishedValue == 0
            && string.Join(",", events) ==
                "begin:220.world.facility-buffer-destinations,"
                + "stage:qa.prestage-source,"
                + "stage-fail:qa.prestage-dependent-failure,"
                + "discard:220.world.facility-buffer-destinations";
        if (passed)
        {
            Debug.Log("SAVE_PRESTAGE_PARTICIPANT_FAILURE_DISCARD_PASS");
        }
        return passed;
    }

    private static bool VerifyPostStagePhysicalLifetimeBeginsAfterCandidateIndex()
    {
        List<string> events = new();
        PreStageAuthorityParticipant authority = new(10, events);
        StagedCandidateIndex index = new();
        PostStageCandidateLifetimeParticipant physicalLifetime = new(
            index,
            events);
        PostStageCandidateIndexSection section = new(
            authority,
            index,
            events);
        DungeonSaveSectionRegistry registry = new(
            new IDungeonSaveSection[] { section },
            new DungeonRuntimeAggregateRootStore(),
            new IDungeonRestoreTransactionParticipant[]
            {
                physicalLifetime,
                authority
            });
        DungeonGameRestoreReport report = new();

        bool restored = registry.RestoreAll(registry.CaptureAll(), report);
        bool passed = restored
            && report.Success
            && authority.LiveValue == 20
            && physicalLifetime.BeginCount == 1
            && physicalLifetime.PublishCount == 1
            && physicalLifetime.CompleteCount == 1
            && physicalLifetime.DiscardCount == 0
            && !authority.HasCandidate
            && !physicalLifetime.HasCandidate
            && !index.IsAvailable
            && string.Join(",", events) ==
                "begin:220.world.facility-buffer-destinations,"
                + "stage:physical-index,"
                + "begin:999.world.physical-item-restore-candidate-lifetime,"
                + "commit:physical-index,"
                + "publish:220.world.facility-buffer-destinations,"
                + "publish:999.world.physical-item-restore-candidate-lifetime,"
                + "complete:999.world.physical-item-restore-candidate-lifetime,"
                + "complete:220.world.facility-buffer-destinations";
        if (passed)
        {
            Debug.Log("SAVE_POSTSTAGE_PHYSICAL_INDEX_ORDER_PASS");
        }
        return passed;
    }

    private static bool VerifyRollbackImageCapturedBeforePreStageBegin()
    {
        List<string> events = new();
        PreStageAuthorityParticipant authority = new(10, events);
        RollbackCaptureAuthoritySection section = new(authority, events)
        {
            FailNextCommit = true
        };
        DungeonSaveSectionRegistry registry = new(
            new IDungeonSaveSection[] { section },
            new DungeonRuntimeAggregateRootStore(),
            new IDungeonRestoreTransactionParticipant[] { authority });
        DungeonSaveSectionEnvelope incoming = new()
        {
            sectionId = section.SectionId,
            sectionVersion = section.SectionVersion,
            restorePhase = section.RestorePhase,
            payloadJson = JsonUtility.ToJson(new TransactionPayload { value = 20 })
        };
        DungeonGameRestoreReport report = new();

        bool restored = registry.RestoreAll(
            new[] { incoming },
            report);
        bool passed = !restored
            && !report.Success
            && section.CapturedValues.SequenceEqual(new[] { 10 })
            && section.StagedValues.SequenceEqual(new[] { 20, 10 })
            && section.CommitAttempts.SequenceEqual(new[] { 20, 10 })
            && authority.LiveValue == 10
            && authority.BeginCount == 2
            && authority.PublishCount == 1
            && authority.CompleteCount == 1
            && authority.DiscardCount == 1
            && !authority.HasCandidate;
        if (passed)
        {
            Debug.Log("SAVE_ROLLBACK_IMAGE_PREBEGIN_PASS");
        }
        return passed;
    }

    private static bool VerifyFailedCommitDiscardsAggregateCandidate()
    {
        DungeonRuntimeAggregateRootStore rootStore =
            new DungeonRuntimeAggregateRootStore();
        rootStore.Replace(new AggregateTransactionState { Value = 10 });
        AggregateTransactionFakeSection aggregate =
            new AggregateTransactionFakeSection("aggregate", rootStore);
        RollbackFreeFailingTransactionFakeSection last =
            new RollbackFreeFailingTransactionFakeSection(
                "last",
                30,
                "aggregate");
        DungeonSaveSectionRegistry registry = new DungeonSaveSectionRegistry(
            new IDungeonSaveSection[] { last, aggregate },
            rootStore);

        List<DungeonSaveSectionEnvelope> incoming = registry.CaptureAll();
        int aggregateCaptureCount = aggregate.CaptureCount;
        int failingCaptureCount = last.CaptureCount;
        incoming.Single(item => item.sectionId == "aggregate").payloadJson =
            "{\"value\":101}";
        incoming.Single(item => item.sectionId == "last").payloadJson =
            "{\"value\":303}";
        last.FailNextRestore = true;

        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        bool restored = registry.RestoreAll(incoming, report);
        int rootValue = rootStore
            .GetOrCreate(() => new AggregateTransactionState())
            .Value;
        bool passed = !restored
            && !report.Success
            && rootStore.PublishedRestoreRevision == 0
            && rootValue == 10
            && last.Value == 30
            && aggregate.CaptureCount == aggregateCaptureCount
            && last.CaptureCount == failingCaptureCount;
        if (!passed)
        {
            Debug.LogError(
                "Aggregate-candidate discard mismatch: "
                + $"restored={restored}; success={report.Success}; "
                + $"revision={rootStore.PublishedRestoreRevision}; "
                + $"root={rootValue}; last={last.Value}; "
                + $"errors={string.Join(" | ", report.Errors)}");
        }
        return passed;
    }

    private static bool VerifyRandomStreamHandleFollowsPublishedRoot()
    {
        DungeonRuntimeAggregateRootStore rootStore =
            new DungeonRuntimeAggregateRootStore();
        RandomStreamProvider provider = new RandomStreamProvider(rootStore);
        IRandomStream stableHandle = provider.Get("save-contract");
        stableHandle.Restore(111UL);
        DungeonSaveSectionRegistry registry = CreateRandomStreamRegistry(
            provider,
            rootStore,
            failingSection: null);
        List<DungeonSaveSectionEnvelope> incoming = registry.CaptureAll();
        ReplaceRandomStreamEnvelope(incoming, rootSeed: 77, state: 222UL);
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();

        return registry.RestoreAll(incoming, report)
            && report.Success
            && ReferenceEquals(stableHandle, provider.Get("save-contract"))
            && provider.RootSeed == 77
            && stableHandle.State == 222UL;
    }

    private static bool VerifyRandomStreamStrictBoundary()
    {
        DungeonRuntimeAggregateRootStore rootStore = new();
        RandomStreamProvider provider = new(rootStore);
        IRandomStream handle = provider.Get("strict-boundary");
        handle.Restore(111UL);
        RandomStreamSaveSection section = new(provider);
        string before = section.Capture();
        DungeonRandomStreamsSaveData canonical = new()
        {
            rootSeed = 77,
            streams = new[]
            {
                new DungeonRandomStreamStateSaveData
                {
                    streamId = "strict-boundary",
                    state = "222"
                }
            }
        };
        string canonicalJson = JsonUtility.ToJson(canonical);
        DungeonGameRestoreReport stageReport = new();
        IDungeonSaveRestoreStage stage =
            ((IDungeonStagedSaveSection)section).StageRestore(
                canonicalJson,
                section.SectionVersion,
                stageReport);
        if (!stageReport.Success
            || section.Capture() != before
            || provider.RootSeed != 1
            || handle.State != 111UL)
        {
            return false;
        }

        // Commit must publish only the detached candidate prepared above. Any
        // live mutation made after staging must be replaced, not recaptured or
        // merged during commit.
        provider.Reseed(99);
        handle.Restore(333UL);
        stage.Commit(stageReport);
        if (!stageReport.Success
            || provider.RootSeed != 77
            || handle.State != 222UL)
        {
            return false;
        }

        DungeonRandomStreamsSaveData invalid =
            JsonUtility.FromJson<DungeonRandomStreamsSaveData>(canonicalJson);
        invalid.streams[0].state = "0";
        DungeonRandomStreamsSaveData legacy =
            JsonUtility.FromJson<DungeonRandomStreamsSaveData>(canonicalJson);
        legacy.version--;

        provider.Get("zeta").Restore(444UL);
        provider.Get("alpha").Restore(555UL);
        string deterministicCapture = section.Capture();
        DungeonRandomStreamsSaveData deterministicPayload =
            JsonUtility.FromJson<DungeonRandomStreamsSaveData>(
                deterministicCapture);
        bool captureIsDeterministic = deterministicPayload?.streams != null
            && deterministicPayload.streams
                .Select(saved => saved.streamId)
                .SequenceEqual(new[] { "alpha", "strict-boundary", "zeta" })
            && string.Equals(
                deterministicCapture,
                section.Capture(),
                StringComparison.Ordinal);

        return section is IDungeonSaveSectionPreflight
            && section is IDungeonRollbackFreeSaveSection
            && !typeof(IOptionalDungeonSaveSection).IsAssignableFrom(
                section.GetType())
            && !typeof(IDungeonStagedOptionalSaveSection).IsAssignableFrom(
                section.GetType())
            && captureIsDeterministic
            && RejectsStrictWithoutMutation(
                section,
                JsonUtility.ToJson(invalid),
                section.SectionVersion,
                deterministicCapture)
            && RejectsStrictWithoutMutation(
                section,
                "{\"version\":1,\"rootSeed\":77}",
                section.SectionVersion,
                deterministicCapture)
            && RejectsStrictWithoutMutation(
                section,
                canonicalJson,
                section.SectionVersion - 1,
                deterministicCapture)
            && RejectsStrictWithoutMutation(
                section,
                JsonUtility.ToJson(legacy),
                section.SectionVersion,
                deterministicCapture)
            && RejectsStrictWithoutMutation(
                section,
                string.Empty,
                section.SectionVersion,
                deterministicCapture);
    }

    private static bool VerifyCopyOnWriteMutationLeavesLiveRootUntouched()
    {
        DungeonRuntimeAggregateRootStore rootStore =
            new DungeonRuntimeAggregateRootStore();
        rootStore.Replace(new AggregateTransactionState { Value = 10 });
        CopyOnWriteTransactionFakeSection first =
            new CopyOnWriteTransactionFakeSection("copy-on-write", rootStore);
        TransactionFakeSection failing = new TransactionFakeSection(
            "after-copy-on-write",
            20,
            first.SectionId)
        {
            FailNextRestore = true
        };
        DungeonSaveSectionRegistry registry = new DungeonSaveSectionRegistry(
            new IDungeonSaveSection[] { failing, first },
            rootStore);
        List<DungeonSaveSectionEnvelope> incoming = registry.CaptureAll();
        incoming.Single(item => item.sectionId == first.SectionId).payloadJson =
            "{\"value\":101}";
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();

        return !registry.RestoreAll(incoming, report)
            && !report.Success
            && rootStore.GetOrCreate(() => new AggregateTransactionState()).Value == 10;
    }

    private static bool VerifyFailedRestorePreservesLiveRandomStream()
    {
        DungeonRuntimeAggregateRootStore rootStore =
            new DungeonRuntimeAggregateRootStore();
        RandomStreamProvider provider = new RandomStreamProvider(rootStore);
        IRandomStream stableHandle = provider.Get("save-contract");
        stableHandle.Restore(111UL);
        RollbackFreeDependencySection runVariables = new(
            RunVariableSaveSection.Id,
            DungeonSaveRestorePhase.Foundation);
        RollbackFreeFailingTransactionFakeSection failing = new(
            "after-random",
            10,
            RandomStreamSaveSection.Id)
        {
            FailNextRestore = true
        };
        RandomStreamSaveSection randomStreams = new(provider);
        DungeonSaveSectionRegistry registry = new(
            new IDungeonSaveSection[]
            {
                runVariables,
                randomStreams,
                failing
            },
            rootStore);
        List<DungeonSaveSectionEnvelope> incoming = registry.CaptureAll();
        ReplaceRandomStreamEnvelope(incoming, rootSeed: 77, state: 222UL);
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        int revisionBefore = rootStore.PublishedRestoreRevision;

        return !registry.RestoreAll(incoming, report)
            && !report.Success
            && rootStore.PublishedRestoreRevision == revisionBefore
            && ReferenceEquals(stableHandle, provider.Get("save-contract"))
            && provider.RootSeed == 1
            && stableHandle.State == 111UL;
    }

    private static bool RejectsStrictWithoutMutation(
        IDungeonSaveSection section,
        string payloadJson,
        int sectionVersion,
        string before)
    {
        try
        {
            ((IDungeonStagedSaveSection)section).StageRestore(
                payloadJson,
                sectionVersion,
                new DungeonGameRestoreReport());
            return false;
        }
        catch (InvalidOperationException)
        {
            return string.Equals(
                section.Capture(),
                before,
                StringComparison.Ordinal);
        }
    }

    private static DungeonSaveSectionRegistry CreateRandomStreamRegistry(
        RandomStreamProvider provider,
        DungeonRuntimeAggregateRootStore rootStore,
        TransactionFakeSection failingSection)
    {
        List<IDungeonSaveSection> sections = new List<IDungeonSaveSection>
        {
            new FakeSection(
                RunVariableSaveSection.Id,
                DungeonSaveRestorePhase.Foundation),
            new RandomStreamSaveSection(provider)
        };
        if (failingSection != null)
        {
            sections.Add(failingSection);
        }

        return new DungeonSaveSectionRegistry(sections, rootStore);
    }

    private static void ReplaceRandomStreamEnvelope(
        IEnumerable<DungeonSaveSectionEnvelope> envelopes,
        int rootSeed,
        ulong state)
    {
        DungeonSaveSectionEnvelope envelope = envelopes.Single(item =>
            string.Equals(
                item.sectionId,
                RandomStreamSaveSection.Id,
                StringComparison.Ordinal));
        envelope.payloadJson = JsonUtility.ToJson(new DungeonRandomStreamsSaveData
        {
            rootSeed = rootSeed,
            streams = new[]
            {
                new DungeonRandomStreamStateSaveData
                {
                    streamId = "save-contract",
                    state = state.ToString()
                }
            }
        });
    }

    private static bool VerifyUnknownRequiredRejected()
    {
        DungeonSaveSectionRegistry registry = new DungeonSaveSectionRegistry(
            new IDungeonSaveSection[]
            {
                new FakeSection("known", DungeonSaveRestorePhase.RuntimeState)
            }, new DungeonRuntimeAggregateRootStore());
        List<DungeonSaveSectionEnvelope> envelopes = registry.CaptureAll();
        envelopes.Add(new DungeonSaveSectionEnvelope
        {
            sectionId = "unknown.required",
            sectionVersion = 1,
            restorePhase = DungeonSaveRestorePhase.RuntimeState,
            optional = false,
            payloadJson = "{}"
        });

        DungeonGameRestoreReport report = new DungeonGameRestoreReport();
        return !registry.RestoreAll(envelopes, report)
            && report.Errors.Any(error => error.Contains("Unknown required", StringComparison.Ordinal));
    }

    private static bool VerifyRestoreParticipantPublishesAfterCommit()
    {
        RestoreTransactionFakeParticipant participant =
            new RestoreTransactionFakeParticipant("world");
        DungeonSaveSectionRegistry registry = new DungeonSaveSectionRegistry(
            new IDungeonSaveSection[]
            {
                new FakeSection("known", DungeonSaveRestorePhase.World)
            },
            new DungeonRuntimeAggregateRootStore(),
            new[] { participant });
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();

        return registry.RestoreAll(registry.CaptureAll(), report)
            && report.Success
            && participant.BeginCount == 1
            && participant.PublishCount == 1
            && participant.CompleteCount == 1
            && participant.DiscardCount == 0
            && !participant.HasCandidate;
    }

    private static bool VerifyRestoreParticipantDiscardsFailedCandidate()
    {
        RestoreTransactionFakeParticipant participant =
            new RestoreTransactionFakeParticipant("world");
        TransactionFakeSection failing =
            new TransactionFakeSection("known", 10)
            {
                FailNextRestore = true
            };
        DungeonSaveSectionRegistry registry = new DungeonSaveSectionRegistry(
            new IDungeonSaveSection[] { failing },
            new DungeonRuntimeAggregateRootStore(),
            new[] { participant });
        List<DungeonSaveSectionEnvelope> incoming = registry.CaptureAll();
        incoming[0].payloadJson = "{\"value\":20}";
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();

        return !registry.RestoreAll(incoming, report)
            && !report.Success
            && participant.BeginCount == 2
            && participant.PublishCount == 1
            && participant.DiscardCount == 1
            && !participant.HasCandidate;
    }

    private static bool VerifyRestoreParticipantOrder()
    {
        List<string> events = new List<string>();
        DungeonSaveSectionRegistry registry = new DungeonSaveSectionRegistry(
            new IDungeonSaveSection[]
            {
                new FakeSection(
                    "commit",
                    DungeonSaveRestorePhase.World,
                    restored: events)
            },
            new DungeonRuntimeAggregateRootStore(),
            new IDungeonRestoreTransactionParticipant[]
            {
                new RestoreTransactionFakeParticipant("200.world.characters", events),
                new RestoreTransactionFakeParticipant("050.world.characters.quiescence", events),
                new RestoreTransactionFakeParticipant("100.world.facilities", events)
            });
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();

        return registry.RestoreAll(registry.CaptureAll(), report)
            && report.Success
            && string.Join(",", events) ==
                "begin:050.world.characters.quiescence,"
                + "begin:100.world.facilities,"
                + "begin:200.world.characters,"
                + "commit,"
                + "publish:050.world.characters.quiescence,"
                + "publish:100.world.facilities,"
                + "publish:200.world.characters,"
                + "complete:200.world.characters,"
                + "complete:100.world.facilities,"
                + "complete:050.world.characters.quiescence";
    }

    private static bool VerifyParticipantPublishFailureRollsBackInReverseOrder()
    {
        List<string> events = new List<string>();
        DungeonRuntimeAggregateRootStore rootStore =
            new DungeonRuntimeAggregateRootStore();
        rootStore.Replace(new AggregateTransactionState { Value = 10 });
        AggregateTransactionFakeSection aggregate =
            new AggregateTransactionFakeSection("aggregate", rootStore);
        ReversibleFaultParticipant first = new(
            "100.first",
            events,
            initialValue: 1,
            publishedValue: 11,
            failDuringPublish: false);
        ReversibleFaultParticipant failing = new(
            "200.failing",
            events,
            initialValue: 2,
            publishedValue: 22,
            failDuringPublish: true);
        ReversibleFaultParticipant untouched = new(
            "300.untouched",
            events,
            initialValue: 3,
            publishedValue: 33,
            failDuringPublish: false);
        DungeonSaveSectionRegistry registry = new DungeonSaveSectionRegistry(
            new IDungeonSaveSection[] { aggregate },
            rootStore,
            new IDungeonRestoreTransactionParticipant[]
            {
                untouched,
                failing,
                first
            });
        List<DungeonSaveSectionEnvelope> incoming = registry.CaptureAll();
        incoming[0].payloadJson = "{\"value\":101}";
        DungeonGameRestoreReport report = new DungeonGameRestoreReport();

        bool restored = registry.RestoreAll(incoming, report);
        string trace = string.Join(",", events);
        return !restored
            && !report.Success
            && report.Errors.Any(error => error.Contains(
                "violated its non-failing publish contract",
                StringComparison.Ordinal))
            && rootStore.PublishedRestoreRevision == 0
            && !rootStore.IsRestoreStaging
            && rootStore.GetOrCreate(() => new AggregateTransactionState()).Value == 10
            && first.Value == 1
            && failing.Value == 2
            && untouched.Value == 3
            && !first.HasCandidate
            && !failing.HasCandidate
            && !untouched.HasCandidate
            && trace ==
                "begin:100.first,begin:200.failing,begin:300.untouched,"
                + "publish:100.first,publish:200.failing,"
                + "discard:300.untouched,rollback:200.failing,rollback:100.first";
    }

    private static bool VerifyRestoreCandidateIndex()
    {
        RestoreWorldCandidateIndex index = new RestoreWorldCandidateIndex();
        Grid candidateGrid = new Grid(3, 2);
        IReadOnlyList<BuildableObject> candidateBuildings =
            Array.Empty<BuildableObject>();
        IReadOnlyList<CharacterActor> candidateCharacters =
            Array.Empty<CharacterActor>();
        int initialRevision = index.Revision;

        index.SetFacilityCandidate(candidateGrid, candidateBuildings);
        index.SetCharacterCandidate(candidateCharacters);
        bool staged = index.TryGetGrid(out Grid indexedGrid)
            && ReferenceEquals(candidateGrid, indexedGrid)
            && index.TryGetBuildings(out IReadOnlyList<BuildableObject> indexedBuildings)
            && ReferenceEquals(candidateBuildings, indexedBuildings)
            && index.TryGetCharacters(out IReadOnlyList<CharacterActor> indexedCharacters)
            && ReferenceEquals(candidateCharacters, indexedCharacters)
            && index.Revision == initialRevision + 2
            && Throws(() => index.SetFacilityCandidate(
                candidateGrid,
                candidateBuildings))
            && Throws(() => index.SetCharacterCandidate(candidateCharacters));

        index.ClearCharacterCandidate();
        index.ClearFacilityCandidate();
        return staged
            && !index.TryGetGrid(out _)
            && !index.TryGetBuildings(out _)
            && !index.TryGetCharacters(out _)
            && index.Revision == initialRevision + 4;
    }

    private static bool VerifyDuplicateRestoreParticipantRejected()
    {
        return Throws(() => new DungeonSaveSectionRegistry(
            new IDungeonSaveSection[]
            {
                new FakeSection("known", DungeonSaveRestorePhase.World)
            },
            new DungeonRuntimeAggregateRootStore(),
            new IDungeonRestoreTransactionParticipant[]
            {
                new RestoreTransactionFakeParticipant("world"),
                new RestoreTransactionFakeParticipant("world")
            }));
    }

    private static void Verify(
        string name,
        Func<bool> scenario,
        ICollection<string> failures)
    {
        try
        {
            if (!scenario())
            {
                failures.Add(name);
            }
        }
        catch (Exception exception)
        {
            failures.Add($"{name}: {exception.Message}");
        }
    }

    private static bool Throws(Action action)
    {
        try
        {
            action();
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class RollbackFreeDependencySection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        public RollbackFreeDependencySection(
            string sectionId,
            DungeonSaveRestorePhase restorePhase)
        {
            SectionId = sectionId;
            RestorePhase = restorePhase;
        }

        public string SectionId { get; }
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase { get; }
        public IReadOnlyList<string> DependsOn => Array.Empty<string>();
        public string Capture() => "{}";

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion
                || string.IsNullOrWhiteSpace(payloadJson))
            {
                report.AddError("Invalid rollback-free dependency payload.");
            }
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            ValidatePayload(payloadJson, sectionVersion, report);
            return new DungeonDelegateSaveRestoreStage(SectionId, _ => { });
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report) =>
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
    }

    private sealed class FakeSection : IDungeonSaveSection, IDungeonStagedSaveSection
    {
        private readonly IReadOnlyList<string> dependencies;
        private readonly ICollection<string> restored;

        public FakeSection(
            string id,
            DungeonSaveRestorePhase phase,
            string dependency)
            : this(id, phase, new[] { dependency }, null)
        {
        }

        public FakeSection(
            string id,
            DungeonSaveRestorePhase phase,
            IReadOnlyList<string> dependencies = null,
            ICollection<string> restored = null)
        {
            SectionId = id;
            RestorePhase = phase;
            this.dependencies = dependencies ?? Array.Empty<string>();
            this.restored = restored;
        }

        public string SectionId { get; }
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase { get; }
        public IReadOnlyList<string> DependsOn => dependencies;

        public string Capture()
        {
            return $"{{\"id\":\"{SectionId}\"}}";
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion)
            {
                throw new InvalidOperationException("version mismatch");
            }

            return new DungeonDelegateSaveRestoreStage(
                SectionId,
                _ => restored?.Add(SectionId));
        }
    }

    [Serializable]
    private sealed class TransactionPayload
    {
        public int value;
    }

    [Serializable]
    private sealed class TopologicalPayload
    {
        public int value;
    }

    private sealed class TopologicalCandidateIndex
    {
        private int value;

        public bool IsAvailable { get; private set; }

        public void Set(int next)
        {
            if (IsAvailable)
            {
                throw new InvalidOperationException(
                    "Topological candidate index already contains a value.");
            }
            value = next;
            IsAvailable = true;
        }

        public int Require()
        {
            if (!IsAvailable)
            {
                throw new InvalidOperationException(
                    "Topological dependency candidate is unavailable.");
            }
            return value;
        }

        public void Clear(int expected)
        {
            if (!IsAvailable || value != expected)
            {
                throw new InvalidOperationException(
                    "Topological candidate index ownership drifted.");
            }
            value = 0;
            IsAvailable = false;
        }
    }

    private sealed class TopologicalSourceCandidate :
        IDungeonDiscardableRestoreCandidate
    {
        private TopologicalCandidateIndex index;

        public TopologicalSourceCandidate(
            TopologicalCandidateIndex index,
            int value)
        {
            this.index = index ?? throw new ArgumentNullException(nameof(index));
            Value = value;
            index.Set(value);
        }

        public int Value { get; }

        public void Discard()
        {
            if (index == null)
            {
                return;
            }
            index.Clear(Value);
            index = null;
        }
    }

    private sealed class TopologicalDependentCandidate
    {
        public TopologicalDependentCandidate(int value) => Value = value;
        public int Value { get; }
    }

    private sealed class TopologicalSourceSection :
        DungeonStrictJsonSaveSection<
            TopologicalPayload,
            TopologicalSourceCandidate>,
        IDungeonRollbackFreeSaveSection
    {
        private readonly TopologicalCandidateIndex index;
        private readonly int capturedValue;

        public TopologicalSourceSection(
            TopologicalCandidateIndex index,
            int capturedValue)
        {
            this.index = index ?? throw new ArgumentNullException(nameof(index));
            this.capturedValue = capturedValue;
        }

        public override string SectionId => "qa.topological-source";
        public override int SectionVersion => 1;
        public override DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.Foundation;
        public int LocalValidationCount { get; private set; }
        public int PublishedValue { get; private set; }

        protected override TopologicalPayload CapturePayload() =>
            new() { value = capturedValue };

        protected override void ValidateParsedPayload(
            TopologicalPayload payload)
        {
            LocalValidationCount++;
            if (payload == null || payload.value <= 0)
            {
                throw new InvalidOperationException(
                    "Topological source payload is invalid.");
            }
        }

        protected override TopologicalSourceCandidate BuildRestoreCandidate(
            TopologicalPayload payload) =>
            new(index, payload.value);

        protected override void PublishRestoreCandidate(
            TopologicalSourceCandidate candidate) =>
            PublishedValue = candidate.Value;
    }

    private sealed class TopologicalDependentSection :
        DungeonStrictJsonSaveSection<
            TopologicalPayload,
            TopologicalDependentCandidate>,
        IDungeonRollbackFreeSaveSection
    {
        private readonly TopologicalCandidateIndex index;
        private readonly int capturedValue;

        public TopologicalDependentSection(
            TopologicalCandidateIndex index,
            int capturedValue)
        {
            this.index = index ?? throw new ArgumentNullException(nameof(index));
            this.capturedValue = capturedValue;
        }

        public override string SectionId => "qa.topological-dependent";
        public override int SectionVersion => 1;
        public override DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.RuntimeState;
        public override IReadOnlyList<string> DependsOn =>
            new[] { "qa.topological-source" };
        public int LocalValidationCount { get; private set; }
        public int PublishedValue { get; private set; }

        protected override TopologicalPayload CapturePayload() =>
            new() { value = capturedValue };

        protected override void ValidateParsedPayload(
            TopologicalPayload payload)
        {
            LocalValidationCount++;
            if (payload == null || payload.value <= 0)
            {
                throw new InvalidOperationException(
                    "Topological dependent payload is invalid.");
            }
        }

        protected override TopologicalDependentCandidate BuildRestoreCandidate(
            TopologicalPayload payload)
        {
            int sourceValue = index.Require();
            if (sourceValue != payload.value)
            {
                throw new InvalidOperationException(
                    "Topological dependency candidate value mismatched.");
            }
            return new TopologicalDependentCandidate(sourceValue);
        }

        protected override void PublishRestoreCandidate(
            TopologicalDependentCandidate candidate)
        {
            PublishedValue = candidate.Value;
            index.Clear(candidate.Value);
        }
    }

    private sealed class PreStageAuthorityParticipant :
        IDungeonPreStageRestoreTransactionParticipant
    {
        private readonly ICollection<string> events;
        private int candidateValue;
        private int previousLiveValue;
        private bool hasStagedValue;
        private bool published;

        public PreStageAuthorityParticipant(
            int initialValue,
            ICollection<string> events)
        {
            LiveValue = initialValue;
            this.events = events;
        }

        public string ParticipantId =>
            "220.world.facility-buffer-destinations";
        public int LiveValue { get; private set; }
        public int AuthorityValue => HasCandidate && !published
            ? candidateValue
            : LiveValue;
        public int BeginCount { get; private set; }
        public int PublishCount { get; private set; }
        public int CompleteCount { get; private set; }
        public int DiscardCount { get; private set; }
        public bool HasCandidate { get; private set; }

        public void BeginRestoreCandidate()
        {
            if (HasCandidate)
            {
                throw new InvalidOperationException(
                    "A pre-stage authority candidate is already active.");
            }

            previousLiveValue = LiveValue;
            candidateValue = 0;
            hasStagedValue = false;
            published = false;
            HasCandidate = true;
            BeginCount++;
            events?.Add($"begin:{ParticipantId}");
        }

        public void StageValue(int value)
        {
            if (!HasCandidate || published || value <= 0)
            {
                throw new InvalidOperationException(
                    "Pre-stage authority write requires an active unpublished candidate.");
            }

            candidateValue = value;
            hasStagedValue = true;
        }

        public void PublishRestoreCandidate()
        {
            if (!HasCandidate || published || !hasStagedValue)
            {
                throw new InvalidOperationException(
                    "Pre-stage authority candidate is not ready to publish.");
            }

            LiveValue = candidateValue;
            published = true;
            PublishCount++;
            events?.Add($"publish:{ParticipantId}");
        }

        public void RollbackPublishedRestoreCandidate()
        {
            if (published)
            {
                LiveValue = previousLiveValue;
            }
            ResetCandidate();
            events?.Add($"rollback:{ParticipantId}");
        }

        public void CompleteRestoreCandidate()
        {
            if (!HasCandidate || !published)
            {
                throw new InvalidOperationException(
                    "Pre-stage authority candidate cannot complete.");
            }

            CompleteCount++;
            events?.Add($"complete:{ParticipantId}");
            ResetCandidate();
        }

        public void DiscardRestoreCandidate()
        {
            if (!HasCandidate)
            {
                return;
            }

            if (published)
            {
                LiveValue = previousLiveValue;
            }
            DiscardCount++;
            events?.Add($"discard:{ParticipantId}");
            ResetCandidate();
        }

        private void ResetCandidate()
        {
            candidateValue = 0;
            previousLiveValue = 0;
            hasStagedValue = false;
            published = false;
            HasCandidate = false;
        }
    }

    private sealed class PreStageAuthoritySection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private readonly PreStageAuthorityParticipant authority;
        private readonly int capturedValue;
        private readonly ICollection<string> events;

        public PreStageAuthoritySection(
            string sectionId,
            PreStageAuthorityParticipant authority,
            int capturedValue,
            ICollection<string> events)
        {
            SectionId = sectionId;
            this.authority = authority
                ?? throw new ArgumentNullException(nameof(authority));
            this.capturedValue = capturedValue;
            this.events = events;
        }

        public string SectionId { get; }
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.RuntimeState;
        public IReadOnlyList<string> DependsOn => Array.Empty<string>();
        public int ObservedLiveValueDuringStage { get; private set; }
        public int ObservedLiveValueDuringCommit { get; private set; }
        public int PublishedValue { get; private set; }

        public string Capture() => JsonUtility.ToJson(
            new TransactionPayload { value = capturedValue });

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report) =>
            _ = Parse(payloadJson, sectionVersion);

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            int value = Parse(payloadJson, sectionVersion);
            authority.StageValue(value);
            ObservedLiveValueDuringStage = authority.LiveValue;
            events?.Add($"stage:{SectionId}");
            return new DungeonDelegateSaveRestoreStage(SectionId, _ =>
            {
                ObservedLiveValueDuringCommit = authority.LiveValue;
                PublishedValue = value;
                events?.Add($"commit:{SectionId}");
            });
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report) =>
            StageRestore(payloadJson, sectionVersion, report).Commit(report);

        private int Parse(string payloadJson, int sectionVersion)
        {
            TransactionPayload payload = sectionVersion == SectionVersion
                ? JsonUtility.FromJson<TransactionPayload>(payloadJson)
                : null;
            if (payload == null || payload.value <= 0)
            {
                throw new InvalidOperationException(
                    "Invalid pre-stage authority payload.");
            }
            return payload.value;
        }
    }

    private sealed class PreStageFailingSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private readonly string dependency;
        private readonly ICollection<string> events;

        public PreStageFailingSection(
            string sectionId,
            string dependency,
            ICollection<string> events)
        {
            SectionId = sectionId;
            this.dependency = dependency;
            this.events = events;
        }

        public string SectionId { get; }
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.RuntimeState;
        public IReadOnlyList<string> DependsOn => new[] { dependency };
        public string Capture() => "{}";

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion
                || string.IsNullOrWhiteSpace(payloadJson))
            {
                throw new InvalidOperationException(
                    "Invalid injected stage-failure payload.");
            }
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            ValidatePayload(payloadJson, sectionVersion, report);
            events?.Add($"stage-fail:{SectionId}");
            throw new InvalidOperationException(
                "injected failure after pre-stage authority write");
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report) =>
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
    }

    private sealed class StagedCandidateIndex
    {
        public bool IsAvailable { get; private set; }

        public void Stage()
        {
            if (IsAvailable)
            {
                throw new InvalidOperationException(
                    "Post-stage candidate index is already available.");
            }
            IsAvailable = true;
        }

        public void Clear()
        {
            if (!IsAvailable)
            {
                throw new InvalidOperationException(
                    "Post-stage candidate index is unavailable.");
            }
            IsAvailable = false;
        }
    }

    private sealed class PostStageCandidateIndexSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private readonly PreStageAuthorityParticipant authority;
        private readonly StagedCandidateIndex index;
        private readonly ICollection<string> events;

        public PostStageCandidateIndexSection(
            PreStageAuthorityParticipant authority,
            StagedCandidateIndex index,
            ICollection<string> events)
        {
            this.authority = authority;
            this.index = index;
            this.events = events;
        }

        public string SectionId => "qa.physical-index-source";
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.Items;
        public IReadOnlyList<string> DependsOn => Array.Empty<string>();
        public string Capture() =>
            JsonUtility.ToJson(new TransactionPayload { value = 20 });

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion
                || JsonUtility.FromJson<TransactionPayload>(payloadJson)?.value != 20)
            {
                throw new InvalidOperationException(
                    "Invalid physical-index test payload.");
            }
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            ValidatePayload(payloadJson, sectionVersion, report);
            authority.StageValue(20);
            index.Stage();
            events?.Add("stage:physical-index");
            return new PostStageCandidateIndexStage(index, events);
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report) =>
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
    }

    private sealed class PostStageCandidateIndexStage :
        IDungeonSaveRestoreStage,
        IDungeonDiscardableSaveRestoreStage
    {
        private readonly StagedCandidateIndex index;
        private readonly ICollection<string> events;
        private bool committed;

        public PostStageCandidateIndexStage(
            StagedCandidateIndex index,
            ICollection<string> events)
        {
            this.index = index;
            this.events = events;
        }

        public string SectionId => "qa.physical-index-source";

        public void Commit(DungeonGameRestoreReport report)
        {
            committed = true;
            events?.Add("commit:physical-index");
        }

        public void Discard()
        {
            if (!committed && index.IsAvailable)
            {
                index.Clear();
            }
        }
    }

    private sealed class PostStageCandidateLifetimeParticipant :
        IDungeonRestoreTransactionParticipant
    {
        private readonly StagedCandidateIndex index;
        private readonly ICollection<string> events;
        private bool published;

        public PostStageCandidateLifetimeParticipant(
            StagedCandidateIndex index,
            ICollection<string> events)
        {
            this.index = index;
            this.events = events;
        }

        public string ParticipantId =>
            "999.world.physical-item-restore-candidate-lifetime";
        public int BeginCount { get; private set; }
        public int PublishCount { get; private set; }
        public int CompleteCount { get; private set; }
        public int DiscardCount { get; private set; }
        public bool HasCandidate { get; private set; }

        public void BeginRestoreCandidate()
        {
            if (HasCandidate || !index.IsAvailable)
            {
                throw new InvalidOperationException(
                    "Physical lifetime requires a staged candidate index.");
            }
            HasCandidate = true;
            BeginCount++;
            events?.Add($"begin:{ParticipantId}");
        }

        public void PublishRestoreCandidate()
        {
            if (!HasCandidate || published || !index.IsAvailable)
            {
                throw new InvalidOperationException(
                    "Physical lifetime is not ready to publish.");
            }
            published = true;
            PublishCount++;
            events?.Add($"publish:{ParticipantId}");
        }

        public void CompleteRestoreCandidate()
        {
            if (!HasCandidate || !published || !index.IsAvailable)
            {
                throw new InvalidOperationException(
                    "Physical lifetime cannot complete.");
            }
            index.Clear();
            HasCandidate = false;
            published = false;
            CompleteCount++;
            events?.Add($"complete:{ParticipantId}");
        }

        public void DiscardRestoreCandidate()
        {
            if (!HasCandidate)
            {
                return;
            }
            if (index.IsAvailable)
            {
                index.Clear();
            }
            HasCandidate = false;
            published = false;
            DiscardCount++;
            events?.Add($"discard:{ParticipantId}");
        }
    }

    private sealed class RollbackCaptureAuthoritySection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection
    {
        private readonly PreStageAuthorityParticipant authority;
        private readonly ICollection<string> events;

        public RollbackCaptureAuthoritySection(
            PreStageAuthorityParticipant authority,
            ICollection<string> events)
        {
            this.authority = authority;
            this.events = events;
        }

        public string SectionId => "qa.rollback-capture-authority";
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.RuntimeState;
        public IReadOnlyList<string> DependsOn => Array.Empty<string>();
        public bool FailNextCommit { get; set; }
        public List<int> CapturedValues { get; } = new();
        public List<int> StagedValues { get; } = new();
        public List<int> CommitAttempts { get; } = new();

        public string Capture()
        {
            int value = authority.AuthorityValue;
            CapturedValues.Add(value);
            return JsonUtility.ToJson(new TransactionPayload { value = value });
        }

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report) =>
            _ = Parse(payloadJson, sectionVersion);

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            int value = Parse(payloadJson, sectionVersion);
            authority.StageValue(value);
            StagedValues.Add(value);
            events?.Add($"stage:rollback:{value}");
            return new DungeonDelegateSaveRestoreStage(SectionId, _ =>
            {
                CommitAttempts.Add(value);
                events?.Add($"commit:rollback:{value}");
                if (FailNextCommit)
                {
                    FailNextCommit = false;
                    throw new InvalidOperationException(
                        "injected commit failure for rollback-image audit");
                }
            });
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report) =>
            StageRestore(payloadJson, sectionVersion, report).Commit(report);

        private int Parse(string payloadJson, int sectionVersion)
        {
            TransactionPayload payload = sectionVersion == SectionVersion
                ? JsonUtility.FromJson<TransactionPayload>(payloadJson)
                : null;
            if (payload == null || payload.value <= 0)
            {
                throw new InvalidOperationException(
                    "Invalid rollback authority payload.");
            }
            return payload.value;
        }
    }

    private sealed class TransactionFakeSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection
    {
        private readonly IReadOnlyList<string> dependencies;

        public TransactionFakeSection(string id, int value, params string[] dependencies)
        {
            SectionId = id;
            Value = value;
            this.dependencies = dependencies ?? Array.Empty<string>();
        }

        public string SectionId { get; }
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.RuntimeState;
        public IReadOnlyList<string> DependsOn => dependencies;
        public int Value { get; private set; }
        public bool FailNextRestore { get; set; }

        public string Capture() => JsonUtility.ToJson(new TransactionPayload { value = Value });

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion
                || JsonUtility.FromJson<TransactionPayload>(payloadJson) == null)
            {
                throw new InvalidOperationException("invalid transaction payload");
            }
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            TransactionPayload payload =
                JsonUtility.FromJson<TransactionPayload>(payloadJson);
            int stagedValue = payload.value;
            return new DungeonDelegateSaveRestoreStage(SectionId, _ =>
            {
                Value = stagedValue;
                if (FailNextRestore)
                {
                    FailNextRestore = false;
                    throw new InvalidOperationException("injected final-stage failure");
                }
            });
        }
    }

    private sealed class StagedTransactionFakeSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection
    {
        private readonly IReadOnlyList<string> dependencies;

        public StagedTransactionFakeSection(
            string id,
            int value,
            params string[] dependencies)
        {
            SectionId = id;
            Value = value;
            this.dependencies = dependencies ?? Array.Empty<string>();
        }

        public string SectionId { get; }
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.RuntimeState;
        public IReadOnlyList<string> DependsOn => dependencies;
        public int Value { get; private set; }
        public bool FailNextStage { get; set; }

        public string Capture() => JsonUtility.ToJson(
            new TransactionPayload { value = Value });

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion
                || JsonUtility.FromJson<TransactionPayload>(payloadJson) == null)
            {
                throw new InvalidOperationException("invalid staged transaction payload");
            }
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            TransactionPayload payload =
                JsonUtility.FromJson<TransactionPayload>(payloadJson)
                ?? throw new InvalidOperationException("missing staged transaction payload");
            if (FailNextStage)
            {
                FailNextStage = false;
                throw new InvalidOperationException("injected staging failure");
            }

            int stagedValue = payload.value;
            return new DungeonDelegateSaveRestoreStage(
                SectionId,
                _ => Value = stagedValue);
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }
    }

    private sealed class RollbackFreeFailingTransactionFakeSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private readonly IReadOnlyList<string> dependencies;

        internal RollbackFreeFailingTransactionFakeSection(
            string id,
            int value,
            params string[] dependencies)
        {
            SectionId = id;
            Value = value;
            this.dependencies = dependencies ?? Array.Empty<string>();
        }

        public string SectionId { get; }
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.RuntimeState;
        public IReadOnlyList<string> DependsOn => dependencies;
        public int Value { get; private set; }
        public bool FailNextRestore { get; set; }
        public int CaptureCount { get; private set; }

        public string Capture()
        {
            CaptureCount++;
            return JsonUtility.ToJson(new TransactionPayload { value = Value });
        }

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion
                || JsonUtility.FromJson<TransactionPayload>(payloadJson) == null)
            {
                throw new InvalidOperationException(
                    "invalid rollback-free transaction payload");
            }
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            TransactionPayload payload =
                JsonUtility.FromJson<TransactionPayload>(payloadJson)
                ?? throw new InvalidOperationException(
                    "missing rollback-free transaction payload");
            int stagedValue = payload.value;
            return new DungeonDelegateSaveRestoreStage(SectionId, _ =>
            {
                if (FailNextRestore)
                {
                    FailNextRestore = false;
                    throw new InvalidOperationException(
                        "injected rollback-free final-stage failure");
                }

                Value = stagedValue;
            });
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }
    }

    private sealed class AggregateTransactionState
    {
        public int Value { get; set; }
    }

    private sealed class AggregateTransactionFakeSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private readonly DungeonRuntimeAggregateRootStore rootStore;

        public AggregateTransactionFakeSection(
            string id,
            DungeonRuntimeAggregateRootStore rootStore)
        {
            SectionId = id;
            this.rootStore = rootStore;
        }

        public string SectionId { get; }
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.RuntimeState;
        public IReadOnlyList<string> DependsOn => Array.Empty<string>();
        public int CaptureCount { get; private set; }

        public string Capture()
        {
            CaptureCount++;
            return JsonUtility.ToJson(new TransactionPayload
            {
                value = rootStore
                    .GetOrCreate(() => new AggregateTransactionState())
                    .Value
            });
        }

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion
                || JsonUtility.FromJson<TransactionPayload>(payloadJson) == null)
            {
                throw new InvalidOperationException(
                    "invalid aggregate transaction payload");
            }
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            TransactionPayload payload =
                JsonUtility.FromJson<TransactionPayload>(payloadJson)
                ?? throw new InvalidOperationException(
                    "missing aggregate transaction payload");
            AggregateTransactionState restored = new AggregateTransactionState
            {
                Value = payload.value
            };
            return new DungeonDelegateSaveRestoreStage(
                SectionId,
                _ => rootStore.Replace(restored));
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }
    }

    private sealed class CopyOnWriteTransactionFakeSection :
        IDungeonSaveSection,
        IDungeonSaveSectionPreflight,
        IDungeonStagedSaveSection
    {
        private readonly DungeonRuntimeAggregateRootStore rootStore;

        public CopyOnWriteTransactionFakeSection(
            string id,
            DungeonRuntimeAggregateRootStore rootStore)
        {
            SectionId = id;
            this.rootStore = rootStore;
        }

        public string SectionId { get; }
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.RuntimeState;
        public IReadOnlyList<string> DependsOn => Array.Empty<string>();

        public string Capture()
        {
            return JsonUtility.ToJson(new TransactionPayload
            {
                value = rootStore
                    .GetOrCreate(() => new AggregateTransactionState())
                    .Value
            });
        }

        public void ValidatePayload(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            if (sectionVersion != SectionVersion
                || JsonUtility.FromJson<TransactionPayload>(payloadJson) == null)
            {
                throw new InvalidOperationException(
                    "invalid copy-on-write transaction payload");
            }
        }

        public IDungeonSaveRestoreStage StageRestore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            TransactionPayload payload =
                JsonUtility.FromJson<TransactionPayload>(payloadJson)
                ?? throw new InvalidOperationException(
                    "missing copy-on-write transaction payload");
            return new DungeonDelegateSaveRestoreStage(SectionId, _ =>
            {
                AggregateTransactionState writable =
                    rootStore.GetOrCreateWritable(
                        () => new AggregateTransactionState(),
                        current => new AggregateTransactionState
                        {
                            Value = current.Value
                        });
                writable.Value = payload.value;
            });
        }

        public void Restore(
            string payloadJson,
            int sectionVersion,
            DungeonGameRestoreReport report)
        {
            StageRestore(payloadJson, sectionVersion, report).Commit(report);
        }
    }

    private sealed class ReversibleFaultParticipant :
        IDungeonRestoreTransactionParticipant
    {
        private readonly ICollection<string> events;
        private readonly int publishedValue;
        private readonly bool failDuringPublish;
        private int valueBeforePublish;
        private bool publicationAttempted;

        public ReversibleFaultParticipant(
            string participantId,
            ICollection<string> events,
            int initialValue,
            int publishedValue,
            bool failDuringPublish)
        {
            ParticipantId = participantId;
            this.events = events;
            Value = initialValue;
            this.publishedValue = publishedValue;
            this.failDuringPublish = failDuringPublish;
        }

        public string ParticipantId { get; }
        public int Value { get; private set; }
        public bool HasCandidate { get; private set; }

        public void BeginRestoreCandidate()
        {
            if (HasCandidate)
            {
                throw new InvalidOperationException(
                    "A reversible restore candidate is already active.");
            }

            valueBeforePublish = Value;
            publicationAttempted = false;
            HasCandidate = true;
            events?.Add($"begin:{ParticipantId}");
        }

        public void PublishRestoreCandidate()
        {
            if (!HasCandidate)
            {
                throw new InvalidOperationException(
                    "No reversible restore candidate is active.");
            }

            publicationAttempted = true;
            Value = publishedValue;
            events?.Add($"publish:{ParticipantId}");
            if (failDuringPublish)
            {
                throw new InvalidOperationException("injected participant publish failure");
            }
        }

        public void RollbackPublishedRestoreCandidate()
        {
            if (publicationAttempted)
            {
                Value = valueBeforePublish;
            }
            publicationAttempted = false;
            HasCandidate = false;
            events?.Add($"rollback:{ParticipantId}");
        }

        public void CompleteRestoreCandidate()
        {
            publicationAttempted = false;
            HasCandidate = false;
            events?.Add($"complete:{ParticipantId}");
        }

        public void DiscardRestoreCandidate()
        {
            publicationAttempted = false;
            HasCandidate = false;
            events?.Add($"discard:{ParticipantId}");
        }
    }

    private sealed class RestoreTransactionFakeParticipant :
        IDungeonRestoreTransactionParticipant
    {
        private readonly ICollection<string> events;

        public RestoreTransactionFakeParticipant(
            string participantId,
            ICollection<string> events = null)
        {
            ParticipantId = participantId;
            this.events = events;
        }

        public string ParticipantId { get; }
        public int BeginCount { get; private set; }
        public int PublishCount { get; private set; }
        public int CompleteCount { get; private set; }
        public int DiscardCount { get; private set; }
        public bool HasCandidate { get; private set; }

        public void BeginRestoreCandidate()
        {
            if (HasCandidate)
            {
                throw new InvalidOperationException(
                    "A restore candidate is already active.");
            }

            HasCandidate = true;
            BeginCount++;
            events?.Add($"begin:{ParticipantId}");
        }

        public void PublishRestoreCandidate()
        {
            if (!HasCandidate)
            {
                throw new InvalidOperationException(
                    "No restore candidate is active.");
            }

            HasCandidate = false;
            PublishCount++;
            events?.Add($"publish:{ParticipantId}");
        }

        public void DiscardRestoreCandidate()
        {
            if (!HasCandidate)
            {
                return;
            }

            HasCandidate = false;
            DiscardCount++;
            events?.Add($"discard:{ParticipantId}");
        }

        public void CompleteRestoreCandidate()
        {
            CompleteCount++;
            events?.Add($"complete:{ParticipantId}");
        }
    }
}
#endif
