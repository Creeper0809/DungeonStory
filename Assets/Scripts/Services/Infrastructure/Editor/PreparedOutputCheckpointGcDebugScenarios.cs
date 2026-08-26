#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class PreparedOutputCheckpointGcDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Save/Run Prepared Output Checkpoint GC")]
    public static void RunAll()
    {
        VerifyWriteAndReplaceFailureNeverRunsGc();
        VerifyPublishFaultRollsBackEveryPublishedParticipant();
        VerifySequenceMismatchFailsLoudly();
        VerifyCandidateAuthorityMustMatchExactly();
        VerifyCandidateIdsMustBeCanonicalOrderedUnique();
        VerifyPrepareResultSequenceMustMatchContext();
        VerifyPublishResultAndAuthorityStateMustMatch();
        VerifyAlreadyAppliedRequiresActualStateAgreement();
        VerifyDurableHookDigestIdempotency();
        Debug.Log("V27_PREPARED_OUTPUT_CHECKPOINT_GC=PASS");
    }

    private static void VerifyWriteAndReplaceFailureNeverRunsGc()
    {
        string root = Path.Combine(
            Path.GetTempPath(),
            "DungeonStoryCheckpointGcFixture",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            RecordingCoordinator coordinator = new();
            string parentFile = Path.Combine(root, "parent-is-file");
            File.WriteAllText(parentFile, "fixture");
            DungeonGameSaveSlotService writeSlots = CreateSlots(
                coordinator,
                Path.Combine(parentFile, "write-failure.json"));
            RequireThrows(() => writeSlots.Save("write-failure"),
                "temporary write failure was swallowed");
            Require(coordinator.CallCount == 0,
                "GC ran before temporary bytes were durable");

            string existingDirectory = Path.Combine(root, "replace-target");
            Directory.CreateDirectory(existingDirectory);
            DungeonGameSaveSlotService replaceSlots = CreateSlots(
                coordinator,
                existingDirectory);
            RequireThrows(() => replaceSlots.Save("replace-failure"),
                "atomic replace failure was swallowed");
            Require(coordinator.CallCount == 0,
                "GC ran after a failed atomic replace");

            string successPath = Path.Combine(root, "durable-success.json");
            DungeonGameSaveSlotService successSlots = CreateSlots(
                coordinator,
                successPath);
            successSlots.Save("durable-success");
            Require(File.Exists(successPath)
                    && coordinator.CallCount == 1
                    && coordinator.LastDigest.Length == 64,
                "durable bytes did not trigger one digest-bound GC callback");
        }
        finally
        {
            if (Directory.Exists(root))
                Directory.Delete(root, recursive: true);
        }
    }

    private static void VerifyPublishFaultRollsBackEveryPublishedParticipant()
    {
        FakeParticipant economy = new(
            PreparedOutputCheckpointGcParticipantKind.EconomyRoutingAuthority,
            throwOnPublish: false);
        FakeParticipant items = new(
            PreparedOutputCheckpointGcParticipantKind.ItemsExactRouteAuthority,
            throwOnPublish: true);
        PreparedOutputCheckpointGcCoordinator coordinator = new(
            new IPreparedOutputCheckpointGcParticipant[] { economy, items });
        PreparedOutputCheckpointGcResult result = coordinator
            .OnDurableSaveCommitted("publish-fault", Digest('a'));
        Require(result.Status == PreparedOutputCheckpointGcStatus.Corruption
                && result.Reason == PreparedOutputCheckpointGcReason
                    .ParticipantPublishFailed
                && economy.PublishCount == 1
                && economy.RollbackCount == 1
                && economy.CompleteCount == 1
                && items.RollbackCount == 1
                && items.CompleteCount == 1,
            "participant publish fault did not reverse the published prefix");
    }

    private static void VerifySequenceMismatchFailsLoudly()
    {
        FakeParticipant economy = new(
            PreparedOutputCheckpointGcParticipantKind.EconomyRoutingAuthority,
            throwOnPublish: false,
            sequence: 4L);
        FakeParticipant items = new(
            PreparedOutputCheckpointGcParticipantKind.ItemsExactRouteAuthority,
            throwOnPublish: false,
            sequence: 5L);
        PreparedOutputCheckpointGcCoordinator coordinator = new(
            new IPreparedOutputCheckpointGcParticipant[] { economy, items });
        PreparedOutputCheckpointGcResult result = coordinator
            .OnDurableSaveCommitted("stale-fixture", Digest('b'));
        Require(result.Status == PreparedOutputCheckpointGcStatus.Corruption
                && result.Reason == PreparedOutputCheckpointGcReason
                    .ParticipantSequenceMismatch
                && economy.PrepareCount == 0
                && items.PrepareCount == 0,
            "mismatched participant sequence reached candidate preparation");
    }

    private static void VerifyCandidateAuthorityMustMatchExactly()
    {
        FakeParticipant economy = new(
            PreparedOutputCheckpointGcParticipantKind.EconomyRoutingAuthority,
            false,
            batchIds: new[] { "batch:a" },
            routeOperationIds: new[] { "route:a" });
        FakeParticipant items = new(
            PreparedOutputCheckpointGcParticipantKind.ItemsExactRouteAuthority,
            false,
            batchIds: new[] { "batch:b" },
            routeOperationIds: new[] { "route:a" });
        PreparedOutputCheckpointGcResult result =
            new PreparedOutputCheckpointGcCoordinator(
                    new IPreparedOutputCheckpointGcParticipant[]
                        { economy, items })
                .OnDurableSaveCommitted("different-id", Digest('c'));
        Require(result.Status == PreparedOutputCheckpointGcStatus.Corruption
                && result.Reason == PreparedOutputCheckpointGcReason
                    .PartialAuthorityCoverage
                && economy.PublishCount == 0
                && items.PublishCount == 0
                && economy.CompleteCount == 1
                && items.CompleteCount == 1,
            "same-count different candidate IDs reached publication");
    }

    private static void VerifyCandidateIdsMustBeCanonicalOrderedUnique()
    {
        VerifyInvalidCandidateIds(
            new[] { "batch:a", "batch:a" },
            "duplicate candidate ID was accepted");
        VerifyInvalidCandidateIds(
            new[] { "batch:b", "batch:a" },
            "reordered candidate IDs were accepted");
    }

    private static void VerifyInvalidCandidateIds(
        IReadOnlyList<string> invalidBatchIds,
        string message)
    {
        FakeParticipant economy = new(
            PreparedOutputCheckpointGcParticipantKind.EconomyRoutingAuthority,
            false,
            batchIds: invalidBatchIds,
            routeOperationIds: new[] { "route:a" });
        FakeParticipant items = new(
            PreparedOutputCheckpointGcParticipantKind.ItemsExactRouteAuthority,
            false,
            batchIds: invalidBatchIds,
            routeOperationIds: new[] { "route:a" });
        PreparedOutputCheckpointGcResult result =
            new PreparedOutputCheckpointGcCoordinator(
                    new IPreparedOutputCheckpointGcParticipant[]
                        { economy, items })
                .OnDurableSaveCommitted("invalid-ids", Digest('d'));
        Require(result.Status == PreparedOutputCheckpointGcStatus.Corruption
                && result.Reason == PreparedOutputCheckpointGcReason
                    .PartialAuthorityCoverage
                && economy.PublishCount == 0
                && items.PublishCount == 0
                && economy.CompleteCount == 1,
            message);
    }

    private static void VerifyPrepareResultSequenceMustMatchContext()
    {
        FakeParticipant economy = new(
            PreparedOutputCheckpointGcParticipantKind.EconomyRoutingAuthority,
            false,
            resultSequenceDelta: 1L);
        FakeParticipant items = new(
            PreparedOutputCheckpointGcParticipantKind.ItemsExactRouteAuthority,
            false);
        PreparedOutputCheckpointGcResult result =
            new PreparedOutputCheckpointGcCoordinator(
                    new IPreparedOutputCheckpointGcParticipant[]
                        { economy, items })
                .OnDurableSaveCommitted("wrong-result-sequence", Digest('e'));
        Require(result.Status == PreparedOutputCheckpointGcStatus.Corruption
                && result.Reason == PreparedOutputCheckpointGcReason
                    .PartialAuthorityCoverage
                && economy.PublishCount == 0
                && items.PrepareCount == 0
                && economy.CompleteCount == 1,
            "participant prepare result sequence mismatch was accepted");
    }

    private static void VerifyAlreadyAppliedRequiresActualStateAgreement()
    {
        FakeParticipant economy = new(
            PreparedOutputCheckpointGcParticipantKind.EconomyRoutingAuthority,
            false,
            prepareStatus: PreparedOutputCheckpointGcStatus.AlreadyApplied);
        FakeParticipant items = new(
            PreparedOutputCheckpointGcParticipantKind.ItemsExactRouteAuthority,
            false,
            prepareStatus: PreparedOutputCheckpointGcStatus.AlreadyApplied);
        PreparedOutputCheckpointGcResult result =
            new PreparedOutputCheckpointGcCoordinator(
                    new IPreparedOutputCheckpointGcParticipant[]
                        { economy, items })
                .OnDurableSaveCommitted("false-replay", Digest('f'));
        Require(result.Status == PreparedOutputCheckpointGcStatus.Corruption
                && result.Reason == PreparedOutputCheckpointGcReason
                    .PartialAuthorityCoverage
                && economy.PublishCount == 0
                && items.PrepareCount == 0,
            "AlreadyApplied result without matching participant state was accepted");
    }

    private static void VerifyPublishResultAndAuthorityStateMustMatch()
    {
        FakeParticipant wrongResult = new(
            PreparedOutputCheckpointGcParticipantKind.EconomyRoutingAuthority,
            false,
            publishResultSequenceDelta: 1L);
        FakeParticipant items = new(
            PreparedOutputCheckpointGcParticipantKind.ItemsExactRouteAuthority,
            false);
        PreparedOutputCheckpointGcResult result =
            new PreparedOutputCheckpointGcCoordinator(
                    new IPreparedOutputCheckpointGcParticipant[]
                        { wrongResult, items })
                .OnDurableSaveCommitted("wrong-publish-result", Digest('7'));
        Require(result.Status == PreparedOutputCheckpointGcStatus.Corruption
                && result.Reason == PreparedOutputCheckpointGcReason
                    .ParticipantPublishFailed
                && wrongResult.RollbackCount == 1
                && wrongResult.LastConfirmedCheckpointSequence == 0L
                && items.PublishCount == 0,
            "publish result sequence mismatch escaped rollback");

        FakeParticipant economy = new(
            PreparedOutputCheckpointGcParticipantKind.EconomyRoutingAuthority,
            false,
            publishStateSequenceDelta: 1L);
        FakeParticipant untouchedItems = new(
            PreparedOutputCheckpointGcParticipantKind.ItemsExactRouteAuthority,
            false);
        PreparedOutputCheckpointGcResult stateResult =
            new PreparedOutputCheckpointGcCoordinator(
                    new IPreparedOutputCheckpointGcParticipant[]
                        { economy, untouchedItems })
                .OnDurableSaveCommitted("wrong-publish-state", Digest('8'));
        Require(stateResult.Status == PreparedOutputCheckpointGcStatus.Corruption
                && stateResult.Reason == PreparedOutputCheckpointGcReason
                    .ParticipantPublishFailed
                && economy.RollbackCount == 1
                && economy.LastConfirmedCheckpointSequence == 0L
                && untouchedItems.PublishCount == 0,
            "published participant authority mismatch escaped rollback");
    }

    private static void VerifyDurableHookDigestIdempotency()
    {
        FakeParticipant economy = new(
            PreparedOutputCheckpointGcParticipantKind.EconomyRoutingAuthority,
            false);
        FakeParticipant items = new(
            PreparedOutputCheckpointGcParticipantKind.ItemsExactRouteAuthority,
            false);
        PreparedOutputCheckpointGcCoordinator coordinator = new(
            new IPreparedOutputCheckpointGcParticipant[] { economy, items });

        PreparedOutputCheckpointGcResult first = coordinator
            .OnDurableSaveCommitted("duplicate-hook", Digest('1'));
        int economyPublished = economy.PublishCount;
        int itemsPublished = items.PublishCount;
        PreparedOutputCheckpointGcResult replay = coordinator
            .OnDurableSaveCommitted("duplicate-hook", Digest('1'));
        Require(first.Status == PreparedOutputCheckpointGcStatus.Applied
                && first.CheckpointSequence == 1L
                && replay.Status == PreparedOutputCheckpointGcStatus.AlreadyApplied
                && replay.CheckpointSequence == 1L
                && economy.PublishCount == economyPublished
                && items.PublishCount == itemsPublished
                && economy.PrepareCount == 1
                && items.PrepareCount == 1,
            "duplicate durable callback advanced or republished checkpoint state");

        PreparedOutputCheckpointGcResult next = coordinator
            .OnDurableSaveCommitted("duplicate-hook", Digest('2'));
        Require(next.Status == PreparedOutputCheckpointGcStatus.Applied
                && next.CheckpointSequence == 2L
                && economy.PublishCount == economyPublished + 1
                && items.PublishCount == itemsPublished + 1
                && economy.LastConfirmedCheckpointSequence == 2L
                && items.LastConfirmedCheckpointSequence == 2L,
            "different durable digest did not advance exactly one checkpoint");
    }

    private static DungeonGameSaveSlotService CreateSlots(
        IPreparedOutputCheckpointGcCoordinator coordinator,
        string savePath) => new(
            new FakeSaveService(),
            new FixedSlotCatalog(savePath),
            coordinator);

    private static string Digest(char character) => new string(character, 64);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private sealed class FakeSaveService : IDungeonGameSaveService
    {
        public DungeonGameSaveData Capture() => new();
        public string ToJson(DungeonGameSaveData saveData, bool prettyPrint = false)
            => "{\"fixture\":true}";
        public DungeonGameSaveData FromJson(string json) => new();
        public bool TryRestore(
            DungeonGameSaveData saveData,
            out DungeonGameRestoreReport report)
        {
            report = new DungeonGameRestoreReport();
            return true;
        }
    }

    private sealed class RecordingCoordinator :
        IPreparedOutputCheckpointGcCoordinator
    {
        internal int CallCount { get; private set; }
        internal string LastDigest { get; private set; } = string.Empty;

        public PreparedOutputCheckpointGcResult OnDurableSaveCommitted(
            string slotId,
            string serializedByteDigest)
        {
            CallCount++;
            LastDigest = serializedByteDigest;
            return new PreparedOutputCheckpointGcResult(
                PreparedOutputCheckpointGcStatus.Deferred,
                PreparedOutputCheckpointGcReason.MissingParticipant,
                0L,
                "Fixture intentionally has no live participants.");
        }
    }

    private sealed class FixedSlotCatalog : IDungeonSaveSlotCatalog
    {
        private readonly string path;

        internal FixedSlotCatalog(string path)
        {
            this.path = path ?? throw new ArgumentNullException(nameof(path));
        }

        public bool HasSave(string slotId) => File.Exists(path);
        public IReadOnlyList<DungeonSaveSlotInfo> GetSlots() =>
            Array.Empty<DungeonSaveSlotInfo>();
        public bool Delete(string slotId) => false;
        public string GetPath(string slotId) => path;
    }

    private sealed class FakeParticipant :
        IPreparedOutputCheckpointGcParticipant
    {
        private readonly bool throwOnPublish;
        private long sequence;
        private string confirmedDigest;
        private long preparedPreviousSequence;
        private string preparedPreviousDigest;
        private readonly IReadOnlyList<string> batchIds;
        private readonly IReadOnlyList<string> routeOperationIds;
        private readonly long resultSequenceDelta;
        private readonly long publishResultSequenceDelta;
        private readonly long publishStateSequenceDelta;
        private readonly PreparedOutputCheckpointGcStatus prepareStatus;

        internal FakeParticipant(
            PreparedOutputCheckpointGcParticipantKind kind,
            bool throwOnPublish,
            long sequence = 0L,
            IReadOnlyList<string> batchIds = null,
            IReadOnlyList<string> routeOperationIds = null,
            long resultSequenceDelta = 0L,
            long publishResultSequenceDelta = 0L,
            long publishStateSequenceDelta = 0L,
            PreparedOutputCheckpointGcStatus prepareStatus =
                PreparedOutputCheckpointGcStatus.Applied)
        {
            CheckpointGcParticipantKind = kind;
            this.throwOnPublish = throwOnPublish;
            this.sequence = sequence;
            confirmedDigest = sequence == 0L ? string.Empty : Digest('f');
            this.batchIds = batchIds ?? Array.Empty<string>();
            this.routeOperationIds = routeOperationIds ?? Array.Empty<string>();
            this.resultSequenceDelta = resultSequenceDelta;
            this.publishResultSequenceDelta = publishResultSequenceDelta;
            this.publishStateSequenceDelta = publishStateSequenceDelta;
            this.prepareStatus = prepareStatus;
        }

        public string CheckpointGcParticipantId =>
            $"fixture:{(int)CheckpointGcParticipantKind}";
        public PreparedOutputCheckpointGcParticipantKind
            CheckpointGcParticipantKind { get; }
        public long LastConfirmedCheckpointSequence => sequence;
        public string LastConfirmedSerializedByteDigest => confirmedDigest;
        internal int PrepareCount { get; private set; }
        internal int PublishCount { get; private set; }
        internal int RollbackCount { get; private set; }
        internal int CompleteCount { get; private set; }

        public PreparedOutputCheckpointGcResult
            PrepareCheckpointGarbageCollection(
                PreparedOutputCheckpointGcContext context,
                out IPreparedOutputCheckpointGcCandidate candidate)
        {
            PrepareCount++;
            preparedPreviousSequence = sequence;
            preparedPreviousDigest = confirmedDigest;
            candidate = prepareStatus == PreparedOutputCheckpointGcStatus.Applied
                ? new FakeCandidate(
                    CheckpointGcParticipantId,
                    CheckpointGcParticipantKind,
                    context,
                    batchIds,
                    routeOperationIds)
                : null;
            return new PreparedOutputCheckpointGcResult(
                prepareStatus,
                prepareStatus == PreparedOutputCheckpointGcStatus.Applied
                    ? PreparedOutputCheckpointGcReason.NoEligibleWholeBatch
                    : PreparedOutputCheckpointGcReason.None,
                checked(context.CheckpointSequence + resultSequenceDelta),
                "fixture prepared");
        }

        public PreparedOutputCheckpointGcResult
            PublishCheckpointGarbageCollection(
                IPreparedOutputCheckpointGcCandidate candidate)
        {
            PublishCount++;
            if (throwOnPublish)
                throw new InvalidOperationException("fixture publish failure");
            sequence = checked(
                candidate.CheckpointSequence + publishStateSequenceDelta);
            confirmedDigest = candidate.SerializedByteDigest;
            return new PreparedOutputCheckpointGcResult(
                PreparedOutputCheckpointGcStatus.Applied,
                PreparedOutputCheckpointGcReason.None,
                checked(candidate.CheckpointSequence
                    + publishResultSequenceDelta),
                "fixture published",
                candidate.BatchCommitIds.Count);
        }

        public void RollbackCheckpointGarbageCollection(
            IPreparedOutputCheckpointGcCandidate candidate)
        {
            RollbackCount++;
            sequence = preparedPreviousSequence;
            confirmedDigest = preparedPreviousDigest;
        }

        public void CompleteCheckpointGarbageCollection(
            IPreparedOutputCheckpointGcCandidate candidate) => CompleteCount++;
    }

    private sealed class FakeCandidate : IPreparedOutputCheckpointGcCandidate
    {
        internal FakeCandidate(
            string participantId,
            PreparedOutputCheckpointGcParticipantKind participantKind,
            PreparedOutputCheckpointGcContext context,
            IReadOnlyList<string> batchCommitIds,
            IReadOnlyList<string> routeOperationIds)
        {
            ParticipantId = participantId;
            ParticipantKind = participantKind;
            CheckpointSequence = context.CheckpointSequence;
            SerializedByteDigest = context.SerializedByteDigest;
            BatchCommitIds = batchCommitIds;
            RouteOperationIds = routeOperationIds;
        }

        public string ParticipantId { get; }
        public PreparedOutputCheckpointGcParticipantKind ParticipantKind { get; }
        public long CheckpointSequence { get; }
        public string SerializedByteDigest { get; }
        public IReadOnlyList<string> BatchCommitIds { get; }
        public IReadOnlyList<string> RouteOperationIds { get; }
    }
}
#endif
