#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionFacilityRetargetTransactionDebugScenarios
{
    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Production Facility Retarget Transaction")]
    public static void VerifyFromMenu()
    {
        VerifyRegistryRejectsMissingAndDuplicateParticipants();
        VerifyDuplicateSourceFailsBeforeEpochOpen();
        VerifyManyToOneCommitAndExactReverseRollback();
        VerifyParticipantFailureRollsBackAttemptedAndCommittedOwners();
        Debug.Log(
            "[V27][PASS] Production facility retarget participant preflight, stable ordering, N-to-1 binding, exact reverse rollback, failure unwind, and epoch lifetime are exact.");
    }

    private static void VerifyRegistryRejectsMissingAndDuplicateParticipants()
    {
        RequireThrows<InvalidOperationException>(() =>
            new ProductionFacilityRetargetParticipantRegistry(
                Array.Empty<IProductionFacilityRetargetParticipant>()));
        FakeParticipant duplicateA = new("active-bill", new List<string>());
        FakeParticipant duplicateB = new("active-bill", new List<string>());
        RequireThrows<InvalidOperationException>(() =>
            new ProductionFacilityRetargetParticipantRegistry(
                new IProductionFacilityRetargetParticipant[]
                {
                    duplicateA,
                    duplicateB
                }));
    }

    private static void VerifyDuplicateSourceFailsBeforeEpochOpen()
    {
        List<string> events = new();
        ProductionFacilityMutationEpochRuntime epochs = new();
        ProductionFacilityHandle source = Facility(
            "building:qa:retarget:duplicate",
            "source-duplicate");
        ProductionFacilityRetargetTransaction runtime = Runtime(
            epochs,
            events,
            out _);
        ProductionFacilityRetargetRequest request = new(
            source,
            ProductionFacilityMutationKind.Relocation);
        Require(!runtime.TryBegin(
                new[] { request, request },
                "qa:retarget:duplicate",
                out _,
                out string failureReason)
            && string.Equals(
                failureReason,
                "production-facility-retarget-source-invalid-or-duplicate",
                StringComparison.Ordinal)
            && !epochs.IsFrozen(source.InstanceId)
            && events.Count == 0,
            "Duplicate retarget sources opened an epoch or participant preflight.");
    }

    private static void VerifyManyToOneCommitAndExactReverseRollback()
    {
        List<string> events = new();
        ProductionFacilityMutationEpochRuntime epochs = new();
        ProductionFacilityRetargetTransaction runtime = Runtime(
            epochs,
            events,
            out FakeParticipant[] participants);
        ProductionFacilityHandle sourceB = Facility(
            "building:qa:retarget:b",
            "source-b");
        ProductionFacilityHandle sourceA = Facility(
            "building:qa:retarget:a",
            "source-a");
        ProductionFacilityHandle survivorA = Facility(
            sourceA.InstanceId.Value,
            "candidate-a");
        ProductionFacilityHandle splitBrainA = Facility(
            sourceA.InstanceId.Value,
            "candidate-a-split-brain");
        ProductionFacilityRetargetRequest[] requests =
        {
            new(sourceB, ProductionFacilityMutationKind.Synthesis),
            new(sourceA, ProductionFacilityMutationKind.Synthesis)
        };

        Require(runtime.TryBegin(
                requests,
                "qa:retarget:n-to-one",
                out ProductionFacilityRetargetTransactionState transaction,
                out string beginFailure),
            "N-to-1 retarget begin failed: " + beginFailure);
        Require(transaction.Requests.Select(value => value.SourceFacilityId.Value)
                .SequenceEqual(new[]
                {
                    sourceA.InstanceId.Value,
                    sourceB.InstanceId.Value
                })
            && epochs.IsFrozen(sourceA.InstanceId)
            && epochs.IsFrozen(sourceB.InstanceId),
            "Retarget sources were not sorted or atomically frozen.");

        ProductionFacilityRetargetBinding[] bindings =
        {
            new(sourceB.InstanceId, survivorA),
            new(sourceA.InstanceId, survivorA)
        };
        Require(!runtime.TryCommit(
                transaction,
                new[]
                {
                    new ProductionFacilityRetargetBinding(
                        sourceB.InstanceId,
                        splitBrainA),
                    new ProductionFacilityRetargetBinding(
                        sourceA.InstanceId,
                        survivorA)
                },
                out string splitBrainFailure)
            && string.Equals(
                splitBrainFailure,
                "production-facility-retarget-target-split-brain",
                StringComparison.Ordinal)
            && events.Count == 3,
            "One target ID accepted multiple detached runtime objects.");
        Require(runtime.TryCommit(transaction, bindings, out string commitFailure),
            "N-to-1 retarget commit failed: " + commitFailure);
        Require(events.SequenceEqual(new[]
            {
                "prepare:active-bill",
                "prepare:active-wip",
                "prepare:physical-custody",
                "commit:active-bill",
                "commit:active-wip",
                "commit:physical-custody"
            }),
            "Retarget participants did not prepare and commit in stable order.");
        foreach (FakeParticipant participant in participants)
            Require(participant.IsCommitted, participant.ParticipantId
                + " did not retain committed candidate binding.");

        Require(runtime.TryRollback(transaction, out string rollbackFailure),
            "Retarget rollback failed: " + rollbackFailure);
        Require(events.Skip(6).SequenceEqual(new[]
            {
                "rollback:physical-custody",
                "rollback:active-wip",
                "rollback:active-bill"
            })
            && participants.All(value => !value.IsCommitted)
            && !epochs.IsFrozen(sourceA.InstanceId)
            && !epochs.IsFrozen(sourceB.InstanceId)
            && transaction.Phase ==
                ProductionFacilityRetargetTransactionPhase.RolledBack,
            "Retarget rollback was not exact, reverse ordered, and epoch closing.");

        events.Clear();
        string completeFailure = string.Empty;
        Require(runtime.TryBegin(
                requests,
                "qa:retarget:n-to-one-complete",
                out ProductionFacilityRetargetTransactionState completed,
                out beginFailure)
            && runtime.TryCommit(completed, bindings, out commitFailure)
            && runtime.TryComplete(completed, out completeFailure),
            "Retarget completion failed: " + beginFailure + commitFailure
            + completeFailure);
        Require(completed.Phase == ProductionFacilityRetargetTransactionPhase.Completed
            && participants.All(value => value.IsCommitted)
            && !epochs.IsFrozen(sourceA.InstanceId)
            && !epochs.IsFrozen(sourceB.InstanceId),
            "Completed retarget did not preserve committed owners or close epochs.");
    }

    private static void
        VerifyParticipantFailureRollsBackAttemptedAndCommittedOwners()
    {
        List<string> events = new();
        FakeParticipant bill = new("active-bill", events);
        FakeParticipant wip = new("active-wip", events)
        {
            FailCommitAfterMutation = true
        };
        FakeParticipant custody = new("physical-custody", events);
        ProductionFacilityRetargetParticipantRegistry registry = new(
            new IProductionFacilityRetargetParticipant[]
            {
                custody,
                wip,
                bill
            });
        ProductionFacilityMutationEpochRuntime epochs = new();
        ProductionFacilityRetargetTransaction runtime = new(registry, epochs);
        ProductionFacilityHandle source = Facility(
            "building:qa:retarget:failure",
            "source-failure");
        ProductionFacilityHandle target = Facility(
            source.InstanceId.Value,
            "candidate-failure");
        ProductionFacilityRetargetRequest[] requests =
        {
            new(source, ProductionFacilityMutationKind.Evolution)
        };

        Require(runtime.TryBegin(
                requests,
                "qa:retarget:failure",
                out ProductionFacilityRetargetTransactionState transaction,
                out string beginFailure),
            "Failure fixture begin failed: " + beginFailure);
        Require(!runtime.TryCommit(
                transaction,
                new[]
                {
                    new ProductionFacilityRetargetBinding(source.InstanceId, target)
                },
                out string failureReason)
            && failureReason.StartsWith(
                "production-facility-retarget-commit-failed:active-wip:",
                StringComparison.Ordinal),
            "Injected participant failure was not surfaced exactly.");
        Require(events.SequenceEqual(new[]
            {
                "prepare:active-bill",
                "prepare:active-wip",
                "prepare:physical-custody",
                "commit:active-bill",
                "commit:active-wip",
                "rollback:active-wip",
                "rollback:active-bill"
            })
            && !bill.IsCommitted
            && !wip.IsCommitted
            && !custody.IsCommitted
            && epochs.IsFrozen(source.InstanceId),
            "Failed retarget did not reverse attempted and committed participants exactly.");
        Require(runtime.TryRollback(transaction, out string rollbackFailure)
            && !epochs.IsFrozen(source.InstanceId),
            "Failed retarget transaction could not close its retained epoch: "
            + rollbackFailure);
    }

    private static ProductionFacilityRetargetTransaction Runtime(
        ProductionFacilityMutationEpochRuntime epochs,
        List<string> events,
        out FakeParticipant[] participants)
    {
        participants = new[]
        {
            new FakeParticipant("physical-custody", events),
            new FakeParticipant("active-wip", events),
            new FakeParticipant("active-bill", events)
        };
        return new ProductionFacilityRetargetTransaction(
            new ProductionFacilityRetargetParticipantRegistry(participants),
            epochs);
    }

    private static ProductionFacilityHandle Facility(string id, string label) =>
        new(
            new FixtureFacility(label),
            (BuildingInstanceId)id,
            default,
            false,
            string.Empty,
            false,
            default,
            "building-definition:" + label,
            "workstation:" + label,
            2);

    private static string Fingerprint(string value) =>
        ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(value);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static void RequireThrows<TException>(Action action)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException(
            "Expected exception was not thrown: " + typeof(TException).Name);
    }

    private sealed class FixtureFacility
    {
        internal FixtureFacility(string label) => Label = label;
        internal string Label { get; }
    }

    private sealed class FakeParticipant :
        IProductionFacilityRetargetParticipant
    {
        private readonly List<string> events;

        internal FakeParticipant(string participantId, List<string> events)
        {
            ParticipantId = participantId;
            this.events = events;
        }

        public string ParticipantId { get; }
        internal bool FailCommitAfterMutation { get; set; }
        internal bool IsCommitted { get; private set; }

        public bool TryPrepare(
            IReadOnlyList<ProductionFacilityRetargetRequest> orderedRequests,
            string operationId,
            out ProductionFacilityRetargetParticipantPlan plan,
            out string failureReason)
        {
            failureReason = string.Empty;
            events.Add("prepare:" + ParticipantId);
            FixtureState state = new(
                Fingerprint(ParticipantId + "|source|" + string.Join(
                    ",",
                    orderedRequests.Select(value =>
                        value.SourceFacilityId.Value))),
                ParticipantId);
            plan = ProductionFacilityRetargetParticipantPlan.Create(
                ParticipantId,
                state.SourceFingerprint,
                state);
            return true;
        }

        public bool TryCommit(
            ProductionFacilityRetargetParticipantPlan plan,
            IReadOnlyList<ProductionFacilityRetargetBinding> orderedBindings,
            out string committedFingerprint,
            out string failureReason)
        {
            FixtureState state = State(plan);
            events.Add("commit:" + ParticipantId);
            state.CurrentFingerprint = Fingerprint(
                ParticipantId + "|target|" + string.Join(
                    ",",
                    orderedBindings.Select(value =>
                        value.SourceFacilityId.Value + "->"
                        + value.TargetFacilityId.Value)));
            IsCommitted = true;
            committedFingerprint = state.CurrentFingerprint;
            failureReason = FailCommitAfterMutation
                ? "injected-after-mutation"
                : string.Empty;
            return !FailCommitAfterMutation;
        }

        public bool TryRollback(
            ProductionFacilityRetargetParticipantPlan plan,
            out string rolledBackFingerprint,
            out string failureReason)
        {
            FixtureState state = State(plan);
            events.Add("rollback:" + ParticipantId);
            state.CurrentFingerprint = state.SourceFingerprint;
            IsCommitted = false;
            rolledBackFingerprint = state.CurrentFingerprint;
            failureReason = string.Empty;
            return true;
        }

        public bool TryCaptureCurrentFingerprint(
            ProductionFacilityRetargetParticipantPlan plan,
            out string currentFingerprint,
            out string failureReason)
        {
            currentFingerprint = State(plan).CurrentFingerprint;
            failureReason = string.Empty;
            return true;
        }

        private FixtureState State(
            ProductionFacilityRetargetParticipantPlan plan)
        {
            if (plan == null
                || !string.Equals(
                    plan.ParticipantId,
                    ParticipantId,
                    StringComparison.Ordinal)
                || plan.ParticipantState is not FixtureState state)
            {
                throw new InvalidOperationException(
                    "Retarget fixture received another participant's plan.");
            }
            return state;
        }
    }

    private sealed class FixtureState
    {
        internal FixtureState(string sourceFingerprint, string participantId)
        {
            SourceFingerprint = sourceFingerprint;
            CurrentFingerprint = sourceFingerprint;
            ParticipantId = participantId;
        }

        internal string ParticipantId { get; }
        internal string SourceFingerprint { get; }
        internal string CurrentFingerprint { get; set; }
    }
}
#endif
