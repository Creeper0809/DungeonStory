using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using DungeonStory.Foundation.Facts;

/// <summary>Pure synchronous contract smoke for the required domain fact observer dispatcher.</summary>
public static class DomainFactDispatcherSmokeScenarios
{
    public static string Run()
    {
        var verification = new Verification();
        var dispatcher = new DomainFactDispatcher();
        var proposed = new FakeFact("operation:domain-fact-smoke", 11);

        verification.Require(
            !dispatcher.TryReserve(proposed, out DomainFactLease missingLease,
                out string missingFailure)
            && !missingLease.IsValid
            && !string.IsNullOrWhiteSpace(missingFailure),
            "TryReserve accepted a fact without its required observer.");

        var firstGroup = new FakeCommitGroup();
        var firstObserver = new FakeObserver(firstGroup);
        IDisposable firstSubscription = dispatcher.Subscribe<FakeFact>(
            "observer:domain-fact-smoke",
            firstObserver);

        verification.Require(
            dispatcher.TryReserve(proposed, out DomainFactLease cancelledLease,
                out string reserveFailure)
            && cancelledLease.IsValid
            && string.IsNullOrEmpty(reserveFailure),
            "Subscribed observer did not receive a reservation.");
        verification.Require(
            firstObserver.ReserveCount == 1
            && firstObserver.LastReserved.OperationKey == proposed.OperationKey
            && firstObserver.LastReserved.Value == proposed.Value,
            "TryReserve did not synchronously forward the typed fact.");

        verification.RequireThrows<InvalidOperationException>(
            () => dispatcher.Subscribe<FakeFact>(
                "observer:duplicate-fact-type",
                new FakeObserver(new FakeCommitGroup())),
            "Dispatcher accepted two required observers for one fact type.");
        verification.RequireThrows<InvalidOperationException>(
            () => dispatcher.Subscribe<AlternateFact>(
                "observer:domain-fact-smoke",
                new AlternateObserver()),
            "Dispatcher accepted one observer identity for two fact types.");

        dispatcher.Cancel(cancelledLease);
        dispatcher.Cancel(cancelledLease);
        verification.Require(
            firstObserver.CancelCount == 1 && !cancelledLease.IsValid,
            "Cancelling one lease twice did not notify its observer exactly once.");

        verification.Require(
            dispatcher.TryReserve(
                new FakeFact("operation:subscription-dispose", 22),
                out DomainFactLease disconnectedLease,
                out _),
            "Could not create the pending lease used by the subscription lifetime smoke.");
        firstSubscription.Dispose();
        verification.Require(
            firstObserver.CancelCount == 2
            && dispatcher.ActiveLeaseCount == 0
            && !disconnectedLease.IsValid,
            "Disposing a subscription did not cancel its pending lease exactly once.");
        verification.Require(
            !dispatcher.TryReserve(
                new FakeFact("operation:disconnected", 23),
                out DomainFactLease rejectedAfterDisconnect,
                out string disconnectedFailure)
            && !rejectedAfterDisconnect.IsValid
            && !string.IsNullOrWhiteSpace(disconnectedFailure),
            "Dispatcher accepted a new fact after its required observer disconnected.");
        DomainFactDelivery disconnectedPublish = dispatcher.Publish(
            disconnectedLease,
            new FakeFact("operation:subscription-dispose", 24));
        verification.Require(
            disconnectedPublish.Phase == DomainFactDeliveryPhase.Rejected
            && firstGroup.CommitCount == 0,
            "A lease cancelled by subscription disposal reached the commit group.");

        var replacementGroup = new FakeCommitGroup();
        var replacementObserver = new FakeObserver(replacementGroup);
        IDisposable replacementSubscription = dispatcher.Subscribe<FakeFact>(
            "observer:domain-fact-smoke",
            replacementObserver);
        var actual = new FakeFact("operation:normal-publish", 32);
        verification.Require(
            dispatcher.TryReserve(
                new FakeFact(actual.OperationKey, 31),
                out DomainFactLease publishLease,
                out string replacementFailure)
            && publishLease.IsValid
            && publishLease.PublicationKey == actual.OperationKey
            && string.IsNullOrEmpty(replacementFailure),
            "A replacement observer could not reserve the fact type with its opaque publication key.");

        DomainFactDelivery delivered = dispatcher.Publish(publishLease, actual);
        verification.Require(
            delivered.Phase == DomainFactDeliveryPhase.Delivered
            && delivered.Completed
            && delivered.OperationKey == actual.OperationKey,
            "A normal publish did not return the commit group's delivered result.");
        verification.Require(
            replacementObserver.PrepareCount == 1
            && replacementObserver.LastPrepared.OperationKey == actual.OperationKey
            && replacementObserver.LastPrepared.Value == actual.Value,
            "Publish did not synchronously forward the actual typed fact.");
        verification.Require(
            replacementGroup.CommitCount == 1
            && replacementGroup.LastPublicationCount == 1
            && !publishLease.IsValid
            && string.IsNullOrEmpty(publishLease.PublicationKey),
            "A normal publish did not consume one lease in one commit-group call.");

        DomainFactDelivery repeated = dispatcher.Publish(publishLease, actual);
        verification.Require(
            repeated.Phase == DomainFactDeliveryPhase.Rejected
            && replacementGroup.CommitCount == 1
            && replacementObserver.PrepareCount == 1,
            "Dispatcher reused an already-published lease.");

        var noRecoveryDispatcher = new DomainFactDispatcher();
        var noRecoveryGroup = new FakeCommitGroup();
        var publishOnlyObserver = new PublishOnlyFakeObserver(noRecoveryGroup);
        IDisposable noRecoverySubscription = noRecoveryDispatcher.Subscribe<FakeFact>(
            "observer:domain-fact-publish-only",
            publishOnlyObserver);
        var publishOnlyFact = new FakeFact("operation:publish-without-recovery", 41);
        verification.Require(
            noRecoveryDispatcher.TryReserve(
                publishOnlyFact,
                out DomainFactLease publishOnlyLease,
                out _)
            && noRecoveryDispatcher.Publish(publishOnlyLease, publishOnlyFact).Completed
            && noRecoveryGroup.CommitCount == 1,
            "An observer without recovery support could not use normal Publish.");
        var unavailableRecoveryResults = new DomainFactDelivery[1];
        verification.Require(
            !noRecoveryDispatcher.ReplayBatch(
                new[]
                {
                    DomainFactReplay.Create(
                        publishOnlyFact.OperationKey,
                        publishOnlyFact)
                },
                unavailableRecoveryResults,
                out string unavailableRecoveryFailure)
            && !string.IsNullOrWhiteSpace(unavailableRecoveryFailure)
            && unavailableRecoveryResults[0].Phase
                == DomainFactDeliveryPhase.Rejected
            && noRecoveryGroup.CommitCount == 1,
            "ReplayBatch reached a commit group without an opted-in recovery observer.");
        noRecoverySubscription.Dispose();
        noRecoveryDispatcher.Dispose();

        var recoveryDispatcher = new DomainFactDispatcher();
        var recoveryGroup = new FakeCommitGroup();
        var recoveryObserver = new FakeObserver(recoveryGroup);
        IDisposable recoverySubscription = recoveryDispatcher.Subscribe<FakeFact>(
            "observer:domain-fact-recovery-smoke",
            recoveryObserver);
        IDomainFactRecoveryPublisher recoveryPublisher = recoveryDispatcher;
        var recoveryFirst = new FakeFact("operation:recovery-first", 81);
        var recoverySecond = new FakeFact("operation:recovery-second", 82);
        var recoveryResults = new DomainFactDelivery[2];
        int recoveryCommitBaseline = recoveryGroup.CommitCount;
        verification.Require(
            recoveryPublisher.ReplayBatch(
                new DomainFactReplay[]
                {
                    DomainFactReplay.Create(
                        recoveryFirst.OperationKey,
                        recoveryFirst),
                    DomainFactReplay.Create(
                        recoverySecond.OperationKey,
                        recoverySecond)
                },
                recoveryResults,
                out string recoveryFailure)
            && string.IsNullOrEmpty(recoveryFailure)
            && recoveryGroup.CommitCount == recoveryCommitBaseline + 1
            && recoveryGroup.LastPublicationCount == 2
            && recoveryResults[0].Completed
            && recoveryResults[0].OperationKey == recoveryFirst.OperationKey
            && recoveryResults[1].Completed
            && recoveryResults[1].OperationKey == recoverySecond.OperationKey,
            "A recovery cohort did not use one atomic commit-group call with exact keys.");

        int duplicateRecoveryCommitBaseline = recoveryGroup.CommitCount;
        var duplicateRecoveryResults = new DomainFactDelivery[2];
        verification.Require(
            !recoveryPublisher.ReplayBatch(
                new DomainFactReplay[]
                {
                    DomainFactReplay.Create(
                        "operation:recovery-duplicate",
                        recoveryFirst),
                    DomainFactReplay.Create(
                        "operation:recovery-duplicate",
                        recoverySecond)
                },
                duplicateRecoveryResults,
                out string duplicateRecoveryFailure)
            && !string.IsNullOrWhiteSpace(duplicateRecoveryFailure)
            && duplicateRecoveryResults.All(value =>
                value.Phase == DomainFactDeliveryPhase.Rejected)
            && recoveryGroup.CommitCount == duplicateRecoveryCommitBaseline,
            "Duplicate recovery keys reached the commit group.");

        int rejectedPrepareCommitBaseline = recoveryGroup.CommitCount;
        int rejectedPrepareListBaseline = recoveryObserver.RecoveryPrepared.Count;
        recoveryObserver.RejectRecoveryAttempt =
            recoveryObserver.RecoveryAttemptCount + 2;
        var rejectedPrepareResults = new DomainFactDelivery[2];
        verification.Require(
            !recoveryPublisher.ReplayBatch(
                new DomainFactReplay[]
                {
                    DomainFactReplay.Create(
                        "operation:recovery-prepare-first",
                        new FakeFact("fact:recovery-prepare-first", 83)),
                    DomainFactReplay.Create(
                        "operation:recovery-prepare-second",
                        new FakeFact("fact:recovery-prepare-second", 84))
                },
                rejectedPrepareResults,
                out string rejectedPrepareFailure)
            && !string.IsNullOrWhiteSpace(rejectedPrepareFailure)
            && recoveryGroup.CommitCount == rejectedPrepareCommitBaseline
            && recoveryObserver.RecoveryPrepared.Count
                == rejectedPrepareListBaseline + 1
            && recoveryObserver.RecoveryPrepared[rejectedPrepareListBaseline]
                .CancelCount == 1
            && rejectedPrepareResults.All(value =>
                value.Phase == DomainFactDeliveryPhase.Rejected),
            "A second recovery prepare rejection committed or failed to cancel the first preparation.");
        recoveryObserver.RejectRecoveryAttempt = 0;

        int generatedBeforeSameKeyReplay = recoveryGroup.GenerationCount;
        int sameKeyCommitBaseline = recoveryGroup.CommitCount;
        var sameKeyResults = new DomainFactDelivery[1];
        verification.Require(
            recoveryPublisher.ReplayBatch(
                new[]
                {
                    DomainFactReplay.Create(
                        recoveryFirst.OperationKey,
                        recoveryFirst)
                },
                sameKeyResults,
                out string sameKeyFailure)
            && string.IsNullOrEmpty(sameKeyFailure)
            && sameKeyResults[0].Completed
            && sameKeyResults[0].OperationKey == recoveryFirst.OperationKey
            && recoveryGroup.CommitCount == sameKeyCommitBaseline + 1
            && recoveryGroup.GenerationCount == generatedBeforeSameKeyReplay,
            "Successful same-key recovery generated a second fake durable record.");

        int throwingRecoveryCommitBaseline = recoveryGroup.CommitCount;
        int throwingRecoveryPreparedBaseline =
            recoveryObserver.RecoveryPrepared.Count;
        recoveryGroup.ThrowOnCommit = true;
        var throwingRecoveryResults = new DomainFactDelivery[2];
        bool throwingRecoveryAccepted = recoveryPublisher.ReplayBatch(
            new DomainFactReplay[]
            {
                DomainFactReplay.Create(
                    "operation:recovery-throw-first",
                    new FakeFact("fact:recovery-throw-first", 85)),
                DomainFactReplay.Create(
                    "operation:recovery-throw-second",
                    new FakeFact("fact:recovery-throw-second", 86))
            },
            throwingRecoveryResults,
            out string throwingRecoveryFailure);
        recoveryGroup.ThrowOnCommit = false;
        verification.Require(
            throwingRecoveryAccepted
            && !string.IsNullOrWhiteSpace(throwingRecoveryFailure)
            && recoveryGroup.CommitCount == throwingRecoveryCommitBaseline + 1
            && throwingRecoveryResults[0].Phase
                == DomainFactDeliveryPhase.InDoubt
            && throwingRecoveryResults[0].OperationKey
                == "operation:recovery-throw-first"
            && throwingRecoveryResults[1].Phase
                == DomainFactDeliveryPhase.InDoubt
            && throwingRecoveryResults[1].OperationKey
                == "operation:recovery-throw-second"
            && recoveryObserver.RecoveryPrepared.Count
                == throwingRecoveryPreparedBaseline + 2
            && recoveryObserver.RecoveryPrepared
                .Skip(throwingRecoveryPreparedBaseline)
                .All(value => value.CancelCount == 0),
            "A throwing recovery commit did not preserve every exact key as InDoubt without rollback.");
        recoverySubscription.Dispose();
        recoveryDispatcher.Dispose();

        var stageDispatcher = new DomainFactDispatcher();
        var stageGroup = new FakeCommitGroup();
        var stageObserver = new FakeObserver(stageGroup);
        IDisposable stageSubscription = stageDispatcher.Subscribe<FakeFact>(
            "observer:domain-fact-staging-smoke",
            stageObserver);
        var stagedFact = new FakeFact("operation:stage-cancel", 51);
        verification.Require(
            stageDispatcher.TryStage(
                stagedFact,
                out IDomainFactStage cancelledStage,
                out string stageFailure)
            && cancelledStage.IsActive
            && string.IsNullOrEmpty(stageFailure)
            && stageGroup.CommitCount == 0,
            "TryStage crossed the commit-group boundary before Commit.");
        verification.Require(
            cancelledStage.OperationKey == stagedFact.OperationKey
            && cancelledStage.OwnerRevision == stagedFact.Value
            && !cancelledStage.IsReplay,
            "A stage did not expose its observer-prepared opaque identity.");
        FakePreparedPublication cancelledPrepared =
            stageObserver.LastStagedPrepared;
        int stagedCancelBaseline = stageObserver.CancelCount;
        cancelledStage.Cancel();
        cancelledStage.Cancel();
        DomainFactDelivery cancelledStageCommit = cancelledStage.Commit();
        verification.Require(
            cancelledStageCommit.Phase == DomainFactDeliveryPhase.Rejected
            && !cancelledStage.IsActive
            && stageObserver.CancelCount == stagedCancelBaseline + 1
            && cancelledPrepared.CancelCount == 1
            && stageGroup.CommitCount == 0,
            "A cancelled stage committed or did not release its reservation once.");

        var committedStageFact = new FakeFact("operation:stage-commit", 52);
        verification.Require(
            stageDispatcher.TryStage(
                committedStageFact,
                out IDomainFactStage committedStage,
                out string committedStageFailure)
            && string.IsNullOrEmpty(committedStageFailure),
            "Could not create the stage used by the single-commit smoke.");
        int committedStageBaseline = stageGroup.CommitCount;
        DomainFactDelivery stagedDelivery = committedStage.Commit();
        DomainFactDelivery repeatedStageDelivery = committedStage.Commit();
        verification.Require(
            stagedDelivery.Phase == DomainFactDeliveryPhase.Delivered
            && stagedDelivery.OperationKey == committedStageFact.OperationKey
            && repeatedStageDelivery.Phase == DomainFactDeliveryPhase.Rejected
            && !committedStage.IsActive
            && stageGroup.CommitCount == committedStageBaseline + 1,
            "One stage did not commit exactly once or rejected-stage reuse failed.");

        var batchFirst = new FakeFact("operation:batch-first", 61);
        var batchSecond = new FakeFact("operation:batch-second", 62);
        bool reservedBatchFirst = stageDispatcher.TryReserve(
            batchFirst,
            out DomainFactLease batchFirstLease,
            out _);
        bool reservedBatchSecond = stageDispatcher.TryReserve(
            batchSecond,
            out DomainFactLease batchSecondLease,
            out _);
        verification.Require(
            reservedBatchFirst && reservedBatchSecond,
            "Could not reserve the two facts used by the atomic batch smoke.");
        int batchCommitBaseline = stageGroup.CommitCount;
        var batchResults = new DomainFactDelivery[2];
        bool batchAccepted = stageDispatcher.PublishBatch(
            new DomainFactEmission[]
            {
                DomainFactEmission.Create(batchFirstLease, batchFirst),
                DomainFactEmission.Create(batchSecondLease, batchSecond)
            },
            batchResults,
            out string batchFailure);
        verification.Require(
            batchAccepted
            && string.IsNullOrEmpty(batchFailure)
            && stageGroup.CommitCount == batchCommitBaseline + 1
            && stageGroup.LastPublicationCount == 2
            && batchResults[0].Phase == DomainFactDeliveryPhase.Delivered
            && batchResults[1].Phase == DomainFactDeliveryPhase.Delivered
            && !batchFirstLease.IsValid
            && !batchSecondLease.IsValid,
            "Two leases sharing one commit group were not published atomically once.");

        var disconnectedDispatcher = new DomainFactDispatcher();
        var disconnectedGroup = new FakeCommitGroup();
        var disconnectedObserver = new FakeObserver(disconnectedGroup);
        IDisposable disconnectedSubscription = disconnectedDispatcher.Subscribe<FakeFact>(
            "observer:staged-disconnect",
            disconnectedObserver);
        verification.Require(
            disconnectedDispatcher.TryStage(
                new FakeFact("operation:stage-disconnect", 63),
                out IDomainFactStage disconnectedStage,
                out _),
            "Could not create the stage used by the disconnect smoke.");
        FakePreparedPublication disconnectedPrepared =
            disconnectedObserver.LastStagedPrepared;
        disconnectedSubscription.Dispose();
        DomainFactDelivery disconnectedStageCommit = disconnectedStage.Commit();
        verification.Require(
            disconnectedStageCommit.Phase == DomainFactDeliveryPhase.Rejected
            && !disconnectedStage.IsActive
            && disconnectedObserver.CancelCount == 1
            && disconnectedPrepared.CancelCount == 1
            && disconnectedGroup.CommitCount == 0,
            "A disconnected stage committed or did not release its reservation.");
        disconnectedDispatcher.Dispose();

        var throwingFact = new FakeFact("operation:stage-commit-throws", 71);
        verification.Require(
            stageDispatcher.TryStage(
                throwingFact,
                out IDomainFactStage throwingStage,
                out _),
            "Could not create the stage used by the in-doubt smoke.");
        FakePreparedPublication throwingPrepared =
            stageObserver.LastStagedPrepared;
        int throwingCancelBaseline = stageObserver.CancelCount;
        int throwingCommitBaseline = stageGroup.CommitCount;
        stageGroup.ThrowOnCommit = true;
        DomainFactDelivery uncertain = throwingStage.Commit();
        stageGroup.ThrowOnCommit = false;
        verification.Require(
            uncertain.Phase == DomainFactDeliveryPhase.InDoubt
            && uncertain.MayBeCommitted
            && !throwingStage.IsActive
            && stageGroup.CommitCount == throwingCommitBaseline + 1
            && stageObserver.CancelCount == throwingCancelBaseline
            && throwingPrepared.CancelCount == 0,
            "A throwing commit group did not report InDoubt or was rolled back by cancellation.");
        stageSubscription.Dispose();
        stageDispatcher.Dispose();

        dispatcher.Dispose();
        verification.RequireThrows<ObjectDisposedException>(
            () => dispatcher.TryReserve(
                new FakeFact("operation:disposed", 40),
                out _,
                out _),
            "A disposed publisher accepted another reservation.");
        verification.RequireThrows<ObjectDisposedException>(
            () => dispatcher.Subscribe<FakeFact>(
                "observer:after-dispose",
                new FakeObserver(new FakeCommitGroup())),
            "A disposed publisher accepted another subscription.");
        replacementSubscription.Dispose();

        var report = new StringBuilder(256);
        report.AppendLine("Domain fact dispatcher smoke");
        report.AppendLine("missing observer rejected: yes");
        report.AppendLine("duplicate fact type rejected: yes");
        report.AppendLine("duplicate observer identity rejected: yes");
        report.AppendLine("idempotent lease cancellation: yes");
        report.AppendLine("subscription replacement: yes");
        report.AppendLine("single publish commit calls: 1");
        report.AppendLine("published lease reuse rejected: yes");
        report.AppendLine("reserved publication key preserved: yes");
        report.AppendLine("recovery cohort atomic commit calls: 1");
        report.AppendLine("duplicate/unavailable recovery rejected: yes");
        report.AppendLine("partial recovery prepare cancelled: yes");
        report.AppendLine("recovery commit exception keeps exact keys in doubt: yes");
        report.AppendLine("same-key fake generation unchanged: yes");
        report.AppendLine("staged identity preserved: yes");
        report.AppendLine("cancelled/disconnected stage reuse rejected: yes");
        report.AppendLine("single staged commit calls: 1");
        report.AppendLine("two-lease atomic batch commit calls: 1");
        report.AppendLine("commit exception remains in doubt: yes");
        report.AppendLine("disposed publisher reuse rejected: yes");
        report.Append("assertions: ").Append(verification.Count).AppendLine();
        return report.ToString();
    }

    private readonly struct FakeFact
    {
        internal FakeFact(string operationKey, int value)
        {
            OperationKey = operationKey;
            Value = value;
        }

        internal string OperationKey { get; }
        internal int Value { get; }
    }

    private readonly struct AlternateFact
    {
    }

    private sealed class FakeReservation : IDomainFactReservedIdentity
    {
        internal FakeReservation(in FakeFact fact, bool staged = false)
        {
            Fact = fact;
            IsStaged = staged;
        }

        internal FakeFact Fact { get; }
        internal bool IsStaged { get; }
        internal FakePreparedPublication Prepared { get; set; }
        public string PublicationKey => Fact.OperationKey;
    }

    private sealed class FakeObserver :
        IDurableDomainFactObserver<FakeFact>,
        IDurableDomainFactStagingObserver<FakeFact>,
        IDurableDomainFactRecoveryObserver<FakeFact>
    {
        private readonly FakeCommitGroup group;

        internal FakeObserver(FakeCommitGroup group) => this.group = group;

        internal int ReserveCount { get; private set; }
        internal int PrepareCount { get; private set; }
        internal int StageCount { get; private set; }
        internal int CancelCount { get; private set; }
        internal int RecoveryAttemptCount { get; private set; }
        internal int RejectRecoveryAttempt { get; set; }
        internal FakeFact LastReserved { get; private set; }
        internal FakeFact LastPrepared { get; private set; }
        internal FakePreparedPublication LastStagedPrepared { get; private set; }
        internal List<FakePreparedPublication> RecoveryPrepared { get; } = new();

        public bool TryReserve(
            in FakeFact proposed,
            out object reservation,
            out string failure)
        {
            ReserveCount++;
            LastReserved = proposed;
            reservation = new FakeReservation(proposed);
            failure = string.Empty;
            return true;
        }

        public bool TryPrepareReplay(
            string publicationKey,
            in FakeFact fact,
            out IDomainFactPreparedPublication prepared,
            out string failure)
        {
            RecoveryAttemptCount++;
            if (RecoveryAttemptCount == RejectRecoveryAttempt)
            {
                prepared = null;
                failure = "fake-recovery-prepare-rejected";
                return false;
            }
            var publication = new FakePreparedPublication(
                group,
                fact,
                publicationKey,
                isReplay: true);
            RecoveryPrepared.Add(publication);
            prepared = publication;
            failure = string.Empty;
            return true;
        }

        public bool TryStage(
            in FakeFact fact,
            out object reservation,
            out IDomainFactPreparedPublication prepared,
            out string failure)
        {
            StageCount++;
            var state = new FakeReservation(fact, staged: true);
            var publication = new FakePreparedPublication(group, fact);
            state.Prepared = publication;
            LastStagedPrepared = publication;
            reservation = state;
            prepared = publication;
            failure = string.Empty;
            return true;
        }

        public bool TryPrepare(
            in FakeFact actual,
            object reservation,
            out IDomainFactPreparedPublication prepared,
            out string failure)
        {
            if (reservation is not FakeReservation)
            {
                prepared = null;
                failure = "fake-reservation-mismatch";
                return false;
            }
            PrepareCount++;
            LastPrepared = actual;
            prepared = new FakePreparedPublication(group, actual);
            failure = string.Empty;
            return true;
        }

        public DomainFactDelivery Replay(in FakeFact fact) => new(
            DomainFactDeliveryPhase.Delivered,
            fact.OperationKey,
            "publication:replay",
            "fingerprint:" + fact.Value,
            "replayed");

        public void Cancel(object reservation)
        {
            if (reservation is not FakeReservation)
                throw new InvalidOperationException("Dispatcher returned the wrong fake reservation.");
            var state = (FakeReservation)reservation;
            if (state.IsStaged)
                state.Prepared.Cancel();
            CancelCount++;
        }
    }

    private sealed class FakePreparedPublication :
        IDomainFactPreparedPublication,
        IDomainFactPreparedIdentity
    {
        private readonly FakeCommitGroup group;

        internal FakePreparedPublication(FakeCommitGroup group, in FakeFact fact)
            : this(group, fact, fact.OperationKey, isReplay: false)
        {
        }

        internal FakePreparedPublication(
            FakeCommitGroup group,
            in FakeFact fact,
            string operationKey,
            bool isReplay)
        {
            this.group = group;
            Fact = fact;
            OperationKey = operationKey;
            IsReplay = isReplay;
        }

        internal FakeFact Fact { get; }
        public IDomainFactCommitGroup CommitGroup => group;
        public string OperationKey { get; }
        public long OwnerRevision => Fact.Value;
        public bool IsReplay { get; }
        public void Cancel() => CancelCount++;
        internal int CancelCount { get; private set; }
    }

    private sealed class FakeCommitGroup : IDomainFactCommitGroup
    {
        private readonly HashSet<string> generated = new(StringComparer.Ordinal);
        internal int CommitCount { get; private set; }
        internal int LastPublicationCount { get; private set; }
        internal int GenerationCount { get; private set; }
        internal bool ThrowOnCommit { get; set; }

        public bool Commit(
            IReadOnlyList<IDomainFactPreparedPublication> publications,
            DomainFactDelivery[] results,
            out string failure)
        {
            CommitCount++;
            LastPublicationCount = publications?.Count ?? 0;
            if (ThrowOnCommit)
                throw new InvalidOperationException("fake-commit-group-throw");
            if (publications == null || results == null
                || publications.Count == 0 || results.Length != publications.Count)
            {
                failure = "fake-commit-shape-invalid";
                return false;
            }
            for (int index = 0; index < publications.Count; index++)
            {
                if (publications[index] is not FakePreparedPublication prepared)
                {
                    failure = "fake-publication-type-invalid";
                    return false;
                }
                if (generated.Add(prepared.OperationKey))
                    GenerationCount++;
                results[index] = new DomainFactDelivery(
                    DomainFactDeliveryPhase.Delivered,
                    prepared.OperationKey,
                    "publication:" + CommitCount + ":" + index,
                    "fingerprint:" + prepared.Fact.Value,
                    "delivered");
            }
            failure = string.Empty;
            return true;
        }
    }

    private sealed class PublishOnlyFakeObserver :
        IDurableDomainFactObserver<FakeFact>
    {
        private readonly FakeObserver inner;

        internal PublishOnlyFakeObserver(FakeCommitGroup group) =>
            inner = new FakeObserver(group);

        public bool TryReserve(
            in FakeFact proposed,
            out object reservation,
            out string failure) => inner.TryReserve(
            proposed,
            out reservation,
            out failure);

        public bool TryPrepare(
            in FakeFact actual,
            object reservation,
            out IDomainFactPreparedPublication prepared,
            out string failure) => inner.TryPrepare(
            actual,
            reservation,
            out prepared,
            out failure);

        public DomainFactDelivery Replay(in FakeFact fact) => inner.Replay(fact);

        public void Cancel(object reservation) => inner.Cancel(reservation);
    }

    private sealed class AlternateObserver : IDurableDomainFactObserver<AlternateFact>
    {
        public bool TryReserve(
            in AlternateFact proposed,
            out object reservation,
            out string failure)
        {
            reservation = new object();
            failure = string.Empty;
            return true;
        }

        public bool TryPrepare(
            in AlternateFact actual,
            object reservation,
            out IDomainFactPreparedPublication prepared,
            out string failure)
        {
            prepared = null;
            failure = "alternate-observer-not-used";
            return false;
        }

        public DomainFactDelivery Replay(in AlternateFact fact) =>
            DomainFactDelivery.Reject("alternate-observer-not-used");

        public void Cancel(object reservation)
        {
        }
    }

    private sealed class Verification
    {
        internal int Count { get; private set; }

        internal void Require(bool condition, string message)
        {
            if (!condition)
                throw new InvalidOperationException(message);
            Count++;
        }

        internal void RequireThrows<TException>(Action action, string message)
            where TException : Exception
        {
            try
            {
                action();
            }
            catch (TException)
            {
                Count++;
                return;
            }
            throw new InvalidOperationException(message);
        }
    }
}
