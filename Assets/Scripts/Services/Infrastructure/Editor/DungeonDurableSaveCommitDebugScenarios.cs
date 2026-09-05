#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

public static class DungeonDurableSaveCommitDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Save/Run Durable Save Commit Pipeline")]
    public static void RunAll()
    {
        VerifyDeterministicOrderAndContinuation();
        VerifyDeferredAndCorruptionStopTheSuffix();
        VerifyThrownAndConflictingResultsBecomeCorruption();
        VerifyTopologyRejectsDuplicateIdentityAndOrder();
        VerifyPreparedOutputAdapterStatusMapping();
        VerifyDestructiveDrainAdapterStatusMapping();
        Debug.Log("V27_DURABLE_SAVE_COMMIT_PIPELINE=PASS");
    }

    private static void VerifyDeterministicOrderAndContinuation()
    {
        List<string> calls = new();
        RecordingParticipant second = new(
            "200.second",
            200,
            DungeonDurableSaveCommitStatus.Applied,
            calls);
        RecordingParticipant first = new(
            "100.first",
            100,
            DungeonDurableSaveCommitStatus.AlreadyApplied,
            calls);
        DungeonDurableSaveCommitResult result =
            new DungeonDurableSaveCommitCoordinator(
                    new IDungeonDurableSaveCommitParticipant[]
                        { second, first })
                .OnDurableSaveCommitted("slot", Digest('a'));
        Require(
            result.Status == DungeonDurableSaveCommitStatus.Applied
            && calls.Count == 2
            && calls[0] == first.ParticipantId
            && calls[1] == second.ParticipantId,
            "durable-save participants did not run in stable order or continue after replay");
    }

    private static void VerifyDeferredAndCorruptionStopTheSuffix()
    {
        VerifyStop(
            DungeonDurableSaveCommitStatus.Deferred,
            "deferred durable-save participant did not stop the suffix");
        VerifyStop(
            DungeonDurableSaveCommitStatus.Corruption,
            "corrupt durable-save participant did not stop the suffix");
    }

    private static void VerifyStop(
        DungeonDurableSaveCommitStatus stoppingStatus,
        string message)
    {
        List<string> calls = new();
        RecordingParticipant first = new(
            "100.first",
            100,
            stoppingStatus,
            calls);
        RecordingParticipant second = new(
            "200.second",
            200,
            DungeonDurableSaveCommitStatus.Applied,
            calls);
        DungeonDurableSaveCommitResult result =
            new DungeonDurableSaveCommitCoordinator(
                    new IDungeonDurableSaveCommitParticipant[]
                        { second, first })
                .OnDurableSaveCommitted("slot", Digest('b'));
        Require(
            result.Status == stoppingStatus
            && calls.Count == 1
            && calls[0] == first.ParticipantId,
            message);
    }

    private static void VerifyThrownAndConflictingResultsBecomeCorruption()
    {
        DungeonDurableSaveCommitResult thrown =
            new DungeonDurableSaveCommitCoordinator(
                    new IDungeonDurableSaveCommitParticipant[]
                    {
                        new RecordingParticipant(
                            "100.throw",
                            100,
                            DungeonDurableSaveCommitStatus.Applied,
                            new List<string>(),
                            throwOnCall: true)
                    })
                .OnDurableSaveCommitted("slot", Digest('c'));
        Require(
            thrown.Status == DungeonDurableSaveCommitStatus.Corruption
            && thrown.ParticipantId == "100.throw",
            "a thrown durable-save participant escaped typed corruption");

        DungeonDurableSaveCommitResult conflicting =
            new DungeonDurableSaveCommitCoordinator(
                    new IDungeonDurableSaveCommitParticipant[]
                    {
                        new RecordingParticipant(
                            "100.owner",
                            100,
                            DungeonDurableSaveCommitStatus.Applied,
                            new List<string>(),
                            resultParticipantId: "different-owner")
                    })
                .OnDurableSaveCommitted("slot", Digest('d'));
        Require(
            conflicting.Status == DungeonDurableSaveCommitStatus.Corruption
            && conflicting.ParticipantId == "100.owner",
            "a conflicting durable-save result identity was accepted");
    }

    private static void VerifyTopologyRejectsDuplicateIdentityAndOrder()
    {
        RequireThrows(
            () => new DungeonDurableSaveCommitCoordinator(
                new IDungeonDurableSaveCommitParticipant[]
                {
                    new RecordingParticipant(
                        "same", 100,
                        DungeonDurableSaveCommitStatus.Applied,
                        new List<string>()),
                    new RecordingParticipant(
                        "same", 200,
                        DungeonDurableSaveCommitStatus.Applied,
                        new List<string>())
                }),
            "duplicate durable-save participant ID was accepted");
        RequireThrows(
            () => new DungeonDurableSaveCommitCoordinator(
                new IDungeonDurableSaveCommitParticipant[]
                {
                    new RecordingParticipant(
                        "first", 100,
                        DungeonDurableSaveCommitStatus.Applied,
                        new List<string>()),
                    new RecordingParticipant(
                        "second", 100,
                        DungeonDurableSaveCommitStatus.Applied,
                        new List<string>())
                }),
            "duplicate durable-save participant order was accepted");
    }

    private static void VerifyPreparedOutputAdapterStatusMapping()
    {
        foreach (PreparedOutputCheckpointGcStatus source in
                 (PreparedOutputCheckpointGcStatus[])Enum.GetValues(
                     typeof(PreparedOutputCheckpointGcStatus)))
        {
            PreparedOutputCheckpointGcDurableSaveParticipant adapter = new(
                new FixedPreparedCoordinator(source));
            DungeonDurableSaveCommitResult result = adapter
                .OnDurableSaveCommitted(
                    new DungeonDurableSaveCommitContext(
                        "slot",
                        Digest('e')));
            Require(
                (int)result.Status == (int)source
                && result.ParticipantId ==
                    PreparedOutputCheckpointGcDurableSaveParticipant.Id,
                "prepared-output durable-save adapter status mapping drifted: "
                + source);
        }
    }

    private static void VerifyDestructiveDrainAdapterStatusMapping()
    {
        foreach (ProductionFacilityDestructiveDrainCheckpointGcStatus source in
                 (ProductionFacilityDestructiveDrainCheckpointGcStatus[])
                 Enum.GetValues(typeof(
                     ProductionFacilityDestructiveDrainCheckpointGcStatus)))
        {
            ProductionFacilityDestructiveDrainCheckpointGcDurableSaveParticipant
                adapter = new(new FixedDestructiveDrainCoordinator(source));
            DungeonDurableSaveCommitResult result = adapter
                .OnDurableSaveCommitted(
                    new DungeonDurableSaveCommitContext(
                        "slot",
                        Digest('f')));
            Require(
                (int)result.Status == (int)source
                && result.ParticipantId ==
                    ProductionFacilityDestructiveDrainCheckpointGcDurableSaveParticipant
                        .Id
                && adapter.Order == 200,
                "destructive-drain durable-save adapter status mapping drifted: "
                + source);
        }
    }

    private static string Digest(char character) => new(character, 64);

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
        catch (InvalidOperationException)
        {
            return;
        }
        throw new InvalidOperationException(message);
    }

    private sealed class RecordingParticipant :
        IDungeonDurableSaveCommitParticipant
    {
        private readonly DungeonDurableSaveCommitStatus status;
        private readonly List<string> calls;
        private readonly bool throwOnCall;
        private readonly string resultParticipantId;

        internal RecordingParticipant(
            string participantId,
            int order,
            DungeonDurableSaveCommitStatus status,
            List<string> calls,
            bool throwOnCall = false,
            string resultParticipantId = null)
        {
            ParticipantId = participantId;
            Order = order;
            this.status = status;
            this.calls = calls;
            this.throwOnCall = throwOnCall;
            this.resultParticipantId = resultParticipantId ?? participantId;
        }

        public string ParticipantId { get; }
        public int Order { get; }

        public DungeonDurableSaveCommitResult OnDurableSaveCommitted(
            DungeonDurableSaveCommitContext context)
        {
            calls.Add(ParticipantId);
            if (throwOnCall)
                throw new InvalidOperationException("injected participant fault");
            return new DungeonDurableSaveCommitResult(
                status,
                resultParticipantId,
                "fixture");
        }
    }

    private sealed class FixedPreparedCoordinator :
        IPreparedOutputCheckpointGcCoordinator
    {
        private readonly PreparedOutputCheckpointGcStatus status;

        internal FixedPreparedCoordinator(
            PreparedOutputCheckpointGcStatus status)
        {
            this.status = status;
        }

        public PreparedOutputCheckpointGcResult OnDurableSaveCommitted(
            string slotId,
            string serializedByteDigest) => new(
            status,
            PreparedOutputCheckpointGcReason.None,
            1L,
            "fixture");
    }

    private sealed class FixedDestructiveDrainCoordinator :
        IProductionFacilityDestructiveDrainCheckpointGcCoordinator
    {
        private readonly ProductionFacilityDestructiveDrainCheckpointGcStatus
            status;

        internal FixedDestructiveDrainCoordinator(
            ProductionFacilityDestructiveDrainCheckpointGcStatus status)
        {
            this.status = status;
        }

        public ProductionFacilityDestructiveDrainCheckpointGcResult
            OnDurableSaveCommitted(
                string slotId,
                string serializedByteDigest) => new(
                status,
                ProductionFacilityDestructiveDrainCheckpointGcReason.None,
                1L,
                "fixture");
    }
}
#endif
