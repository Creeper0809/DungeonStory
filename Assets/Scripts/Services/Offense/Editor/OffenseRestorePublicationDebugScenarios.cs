using System;

public static class OffenseRestorePublicationDebugScenarios
{
    public static void VerifyRegistryRollback(
        IDungeonRestoreTransactionParticipant owner, Action stageOwner,
        Action requireOriginal, Func<bool> isCandidate)
        => VerifyRegistryRollback(owner.ParticipantId, new[] { owner }, stageOwner,
            requireOriginal, isCandidate);

    public static void VerifyRegistryRollback(
        string label, System.Collections.Generic.IEnumerable<IDungeonRestoreTransactionParticipant> owners,
        Action stageOwner, Action requireOriginal, Func<bool> isCandidate)
    {
        foreach (bool failDuringCommit in new[] { true, false })
        {
            var root = new DungeonRuntimeAggregateRootStore();
            var fault = new LaterPublicationFault(isCandidate);
            var participants = new System.Collections.Generic.List<IDungeonRestoreTransactionParticipant>(owners)
                { fault };
            var registry = new DungeonSaveSectionRegistry(
                new IDungeonSaveSection[] { new OwnerStageSection(stageOwner, failDuringCommit) },
                root, participants);
            var report = new DungeonGameRestoreReport();
            bool restored = registry.RestoreAll(registry.CaptureAll(), report);
            Require(!restored && !report.Success
                && !root.IsRestoreStaging && root.PublishedRestoreRevision == 0,
                label + " accepted an injected restore failure.");
            Require(string.Join("|", report.Errors).Contains(failDuringCommit
                    ? "injected-owner-section-commit" : "injected-later-participant-publication"),
                label + " failed for an unexpected reason.");
            Require(fault.SawCandidate == !failDuringCommit,
                label + " failed to reach the expected publication boundary.");
            requireOriginal();
        }
    }

    // The save adapter and later fault are controlled; the owner under test is production code.
    private sealed class OwnerStageSection : IDungeonSaveSection,
        IDungeonStagedSaveSection, IDungeonRollbackFreeSaveSection
    {
        private readonly Action stage;
        private readonly bool failCommit;
        public OwnerStageSection(Action stage, bool failCommit)
        { this.stage = stage; this.failCommit = failCommit; }
        public string SectionId => "test.offense-owner";
        public int SectionVersion => 1;
        public DungeonSaveRestorePhase RestorePhase => DungeonSaveRestorePhase.LateRuntimeState;
        public System.Collections.Generic.IReadOnlyList<string> DependsOn => Array.Empty<string>();
        public string Capture() => "{}";
        public void Restore(string json, int version, DungeonGameRestoreReport report) =>
            StageRestore(json, version, report).Commit(report);
        public IDungeonSaveRestoreStage StageRestore(
            string json, int version, DungeonGameRestoreReport report) =>
            new DungeonDelegateSaveRestoreStage(SectionId, _ =>
            {
                stage();
                if (failCommit) throw new InvalidOperationException("injected-owner-section-commit");
            });
    }

    private sealed class LaterPublicationFault : IDungeonRestoreTransactionParticipant
    {
        private readonly Func<bool> isCandidate;
        public LaterPublicationFault(Func<bool> isCandidate) { this.isCandidate = isCandidate; }
        public string ParticipantId => "zz.test.offense-later-fault";
        public bool SawCandidate { get; private set; }
        public void BeginRestoreCandidate() { }
        public void PublishRestoreCandidate()
        {
            SawCandidate = isCandidate();
            throw new InvalidOperationException("injected-later-participant-publication");
        }
        public void DiscardRestoreCandidate() { }
    }

    public static void VerifyPublicationLifecycle()
    {
        OwnerReference firstA = new("first-a");
        OwnerReference secondA = new("second-a");
        SyntheticOwnerState stateA = new(firstA, secondA, 11);
        SyntheticPublicationOwner owner = new(stateA);

        OwnerReference firstB = new("first-b");
        OwnerReference secondB = new("second-b");
        SyntheticOwnerState stateB = new(firstB, secondB, 22);
        OwnerReference firstC = new("first-c");
        OwnerReference secondC = new("second-c");
        SyntheticOwnerState stateC = new(firstC, secondC, 33);

        Require(!owner.TryStageOwnerState(stateB),
            "Inactive publication accepted an owner candidate.");
        RequireOwnerState(owner, stateA,
            "Inactive stage changed the live owner.");

        owner.BeginOwnerRestore();
        Require(owner.TryStageOwnerState(stateB),
            "Active publication did not stage the owner candidate.");
        RequireOwnerState(owner, stateA,
            "Staging changed the live owner before publication.");
        owner.DiscardOwnerRestore();
        RequireOwnerState(owner, stateA,
            "Discarding a staged candidate changed the original owner references.");

        owner.BeginOwnerRestore();
        Require(owner.TryStageOwnerState(stateB),
            "Rollback scenario did not stage the owner candidate.");
        owner.PublishOwnerRestore();
        RequireOwnerState(owner, stateB,
            "Publication did not install the exact candidate references.");
        owner.RollbackPublishedOwnerRestore();
        RequireOwnerState(owner, stateA,
            "Rollback did not restore the exact original references and scalar.");

        owner.BeginOwnerRestore();
        Require(owner.TryStageOwnerState(stateB),
            "Completion scenario did not stage the owner candidate.");
        owner.PublishOwnerRestore();
        owner.CompleteOwnerRestore();
        RequireOwnerState(owner, stateB,
            "Completion did not keep the published owner candidate.");
        owner.BeginOwnerRestore();
        Require(owner.TryStageOwnerState(stateC),
            "A transaction could not begin after completion.");
        owner.DiscardOwnerRestore();
        RequireOwnerState(owner, stateB,
            "Discard after a completed transaction changed the published owner.");

        owner.BeginOwnerRestore();
        RequireThrows(owner.BeginOwnerRestore,
            "A duplicate owner transaction begin was accepted.");
        owner.DiscardOwnerRestore();

        owner.BeginOwnerRestore();
        Require(owner.TryStageOwnerState(stateC),
            "Duplicate-stage scenario did not stage its first candidate.");
        RequireThrows(
            () => owner.TryStageOwnerState(stateA),
            "A duplicate owner candidate stage was accepted.");
        owner.DiscardOwnerRestore();
        RequireOwnerState(owner, stateB,
            "Duplicate-stage cleanup changed the live owner.");

        owner.BeginOwnerRestore();
        Require(owner.TryStageOwnerState(stateC),
            "Duplicate-publish scenario did not stage its candidate.");
        owner.PublishOwnerRestore();
        RequireThrows(owner.PublishOwnerRestore,
            "A duplicate owner publication was accepted.");
        owner.RollbackPublishedOwnerRestore();
        RequireOwnerState(owner, stateB,
            "Duplicate-publish rollback did not restore the live owner.");

        owner.BeginOwnerRestore();
        owner.PublishOwnerRestore();
        RequireOwnerState(owner, stateB,
            "A stage-less participant publication changed the live owner.");
        owner.CompleteOwnerRestore();
        RequireOwnerState(owner, stateB,
            "A stage-less participant completion changed the live owner.");

        owner.BeginOwnerRestore();
        Require(owner.TryStageOwnerState(stateC, throwAfterFirstField: true),
            "Partial-failure scenario did not stage its candidate.");
        RequireThrows(owner.PublishOwnerRestore,
            "A partial owner publication did not surface its failure.");
        Require(ReferenceEquals(owner.First, stateC.First)
            && ReferenceEquals(owner.Second, stateB.Second)
            && owner.Scalar == stateB.Scalar,
            "The partial-failure fixture did not stop after replacing only the first field.");
        owner.DiscardOwnerRestore();
        RequireOwnerState(owner, stateB,
            "Discard after a partial publication did not restore both references and scalar.");
    }

    private static void RequireOwnerState(
        SyntheticPublicationOwner owner,
        SyntheticOwnerState expected,
        string message)
    {
        Require(ReferenceEquals(owner.First, expected.First)
            && ReferenceEquals(owner.Second, expected.Second)
            && owner.Scalar == expected.Scalar,
            message);
    }

    private static void RequireThrows(Action action, string message)
    {
        bool rejected = false;
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Require(rejected, message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }

    private sealed class SyntheticPublicationOwner
    {
        private readonly DungeonRestorePublication restorePublication = new();

        internal SyntheticPublicationOwner(SyntheticOwnerState initial)
        {
            Apply(initial, throwAfterFirstField: false);
        }

        internal OwnerReference First { get; private set; }
        internal OwnerReference Second { get; private set; }
        internal int Scalar { get; private set; }

        internal void BeginOwnerRestore() => restorePublication.Begin();

        internal bool TryStageOwnerState(
            SyntheticOwnerState candidate,
            bool throwAfterFirstField = false)
        {
            if (candidate == null)
            {
                throw new ArgumentNullException(nameof(candidate));
            }

            return restorePublication.TryStage(
                () => Apply(candidate, throwAfterFirstField),
                CaptureExactOwnerRollback);
        }

        internal void PublishOwnerRestore() => restorePublication.Publish();
        internal void RollbackPublishedOwnerRestore() =>
            restorePublication.Discard();
        internal void DiscardOwnerRestore() => restorePublication.Discard();
        internal void CompleteOwnerRestore() => restorePublication.Complete();

        private Action CaptureExactOwnerRollback()
        {
            OwnerReference previousFirst = First;
            OwnerReference previousSecond = Second;
            int previousScalar = Scalar;
            return () =>
            {
                First = previousFirst;
                Second = previousSecond;
                Scalar = previousScalar;
            };
        }

        private void Apply(
            SyntheticOwnerState candidate,
            bool throwAfterFirstField)
        {
            First = candidate.First;
            if (throwAfterFirstField)
            {
                throw new InvalidOperationException(
                    "Synthetic owner failed after replacing its first field.");
            }

            Second = candidate.Second;
            Scalar = candidate.Scalar;
        }
    }

    private sealed class SyntheticOwnerState
    {
        internal SyntheticOwnerState(
            OwnerReference first,
            OwnerReference second,
            int scalar)
        {
            First = first ?? throw new ArgumentNullException(nameof(first));
            Second = second ?? throw new ArgumentNullException(nameof(second));
            Scalar = scalar;
        }

        internal OwnerReference First { get; }
        internal OwnerReference Second { get; }
        internal int Scalar { get; }
    }

    private sealed class OwnerReference
    {
        internal OwnerReference(string id)
        {
            Id = string.IsNullOrWhiteSpace(id)
                ? throw new ArgumentException("Owner reference ID is required.", nameof(id))
                : id;
        }

        internal string Id { get; }
    }
}
