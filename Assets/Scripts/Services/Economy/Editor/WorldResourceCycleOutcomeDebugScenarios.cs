#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation.Facts;
using UnityEngine;

public static class WorldResourceCycleOutcomeDebugScenarios
{
    private const MigratedProducerOutcomeKind Kind =
        MigratedProducerOutcomeKind.WorldResourceCycleResult;

    public static IReadOnlyList<MigratedProducerOutcomeKind> CoveredKinds
        { get; } = new[] { Kind };

    public static bool RunAll(bool throwOnFailure = true)
    {
        try
        {
            VerifyProductionEntryFaults();
            VerifyImmutableFact();
            _ = WorldResourceTransactionFaultDebugScenarios.RunAll();
            return true;
        }
        catch
        {
            if (throwOnFailure)
                throw;
            return false;
        }
    }

    public static void RunBatch()
    {
        RunAll(throwOnFailure: true);
        Debug.Log("WORLD_RESOURCE_CYCLE_OUTCOME=PASS");
    }

    public static void RunDomainFactFocused()
    {
        VerifyProductionEntryFaults();
        VerifyImmutableFact();
        WorldResourceTransactionFaultDebugScenarios.RunDomainFactFocused();
    }

    private static void VerifyProductionEntryFaults()
    {
        var deliveryFault = new MigratedProducerOutcomeDeliveryFault
        {
            TargetKind = Kind,
            Enabled = true
        };
        var fixture = new MigratedProducerOutcomeEditorFixture(
            "run:world-resource-cycle-outcome",
            deliveryFault: deliveryFault);
        var probe = new MigratedProducerOutcomeProbeTransaction(
            fixture.Transaction,
            fixture.Recorder)
        {
            TargetKind = Kind,
            FaultMode = MigratedProducerOutcomeFaultMode.RejectReservation
        };
        using var domainFacts = new DomainFactDispatcher();
        var commits = new GameplayDomainFactCommitGroup(
            fixture.Recorder,
            fixture.Ledger);
        domainFacts.Subscribe(
            "fixture:world-resource-cycle-completed",
            new MigratedProducerDomainFactProbeObserver<
                WorldResourceCycleCompletedFact>(
                WorldResourceCycleFactMapping.Capture,
                fixture.Transaction,
                commits,
                probe));
        WorldResourcePendingOutputSaveData pending = CreatePending(1);
        var fact = new WorldResourceCycleCompletedFact(
            (BuildingInstanceId)"building:world-resource-qa",
            BuiltInWorkTypeIds.Logging,
            pending);
        Require(!domainFacts.TryReserve(
                fact,
                out _,
                out _)
            && fixture.Transaction.Capture().owners.Count == 0,
            "World-resource parent reservation fault advanced its owner.");

        probe.FaultMode = MigratedProducerOutcomeFaultMode.RejectCommit;
        pending = CreatePending(2);
        fact = new WorldResourceCycleCompletedFact(
            (BuildingInstanceId)"building:world-resource-qa",
            BuiltInWorkTypeIds.Logging,
            pending);
        Require(domainFacts.TryReserve(
                fact,
                out DomainFactLease rejectedLease,
                out string reserveFailure),
            "World-resource commit-fault reservation failed: "
                + reserveFailure);
        DomainFactDelivery rejected = domainFacts.Publish(
            rejectedLease,
            fact);
        Require(!rejected.MayBeCommitted
            && fixture.Transaction.Capture().owners.Count == 0,
            "World-resource parent commit fault advanced its owner.");

        probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
        pending = CreatePending(3);
        // This port fixture observes the real outcome owner's revision, not a
        // WorldResourceRuntime/physical-output state assembled by the fixture.
        string beforeState = JsonUtility.ToJson(fixture.Transaction.Capture());
        fact = new WorldResourceCycleCompletedFact(
            (BuildingInstanceId)"building:world-resource-qa",
            BuiltInWorkTypeIds.Logging,
            pending);
        Require(domainFacts.TryReserve(
                fact,
                out DomainFactLease committedLease,
                out string committedReserveFailure),
            "World-resource parent reservation failed: "
                + committedReserveFailure);
        DomainFactDelivery committed = domainFacts.Publish(
            committedLease,
            fact);
        Require(committed.MayBeCommitted,
            "World-resource parent did not durably commit: "
                + committed.Detail);
        MigratedProducerOutcomeE2EAssertions.RequirePendingThenRetry(
            fixture,
            Kind,
            probe,
            deliveryFault,
            beforeState,
            JsonUtility.ToJson(fixture.Transaction.Capture()));
    }

    private static void VerifyImmutableFact()
    {
        WorldResourcePendingOutputSaveData input = CreatePending(4);
        var fact = new WorldResourceCycleCompletedFact(
            (BuildingInstanceId)"building:world-resource-qa",
            BuiltInWorkTypeIds.Logging,
            input);
        string immutable = JsonUtility.ToJson(fact.CaptureDomainState());
        input.lines[0].itemId = "resource:mutated-input";
        input.contributions[0].contributedWork = 999f;
        input.publishedStacks[0].stackId = "stack:mutated-input";
        Require(JsonUtility.ToJson(fact.CaptureDomainState()) == immutable,
            "World-resource fact retained caller-owned nested input data.");

        WorldResourcePendingOutputSaveData returned = fact.CaptureDomainState();
        returned.lines.Clear();
        returned.contributions.Clear();
        returned.publishedStacks.Clear();
        Require(JsonUtility.ToJson(fact.CaptureDomainState()) == immutable,
            "World-resource fact exposed mutable nested domain state.");
    }

    private static WorldResourcePendingOutputSaveData CreatePending(int sequence)
    {
        string operation = "world-resource:building:world-resource-qa:"
            + BuiltInWorkTypeIds.Logging.Value + ":"
            + sequence.ToString("D8",
                System.Globalization.CultureInfo.InvariantCulture);
        return new WorldResourcePendingOutputSaveData
        {
            rootSeed = 157184,
            cycleSequence = sequence,
            operationId = operation,
            recipeId = "source:logging",
            recipeSourceDigest = new string('a', 64),
            maximumOutputMassGrams = 3680L,
            maximumOutputSourceDigest = new string('b', 64),
            outputFactorNumerator = 1L,
            outputFactorDenominator = 1L,
            outcomeFingerprint = new string('c', 64),
            physicalOutputMassGrams = 3680L,
            lines = new List<WorldResourceResolvedOutputLineSaveData>
            {
                new()
                {
                    deterministicOrdinal = 0,
                    outputLineId = "output:logging:log",
                    role = ProductionOutputRole.Main,
                    itemId = "resource:log",
                    authoredQuantity = 2,
                    inclusionProbability = 1f,
                    included = true,
                    resolvedQuantity = 2,
                    unitMassGrams = 1840L,
                    resolvedMassGrams = 3680L
                }
            },
            contributions = new List<CraftContributionSaveData>
            {
                new()
                {
                    characterId = "character:w06-logger-a",
                    contributedWork = 4f
                },
                new()
                {
                    characterId = "character:w06-logger-b",
                    contributedWork = 6f
                }
            },
            domainApplied = true,
            sourceBefore = 1f,
            sourceAfter = 0f,
            outputBatchCommitId = "batch:world-resource-qa:" + sequence,
            outputDestinationId = "destination:world-resource-qa",
            publishedStacks = new List<WorldResourcePublishedStackSaveData>
            {
                new()
                {
                    stackId = "stack:world-resource-qa:" + sequence,
                    outputLineId = "output:logging:log",
                    itemId = "resource:log",
                    quantity = 2,
                    massGrams = 3680L
                }
            },
            completedAbsoluteDay = 7
        };
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
