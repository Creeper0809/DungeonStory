#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation.Facts;
using UnityEngine;

/// <summary>
/// Focused real-ledger recovery checks for the four character-consumption facts.
/// This is an observer/ledger contract fixture, not full gameplay producer proof.
/// </summary>
public static class CharacterConsumptionFactRecoveryScenarios
{
    public static string Verify()
    {
        Facts facts = CreateFacts("normal", out List<string> sourceStackInput);
        sourceStackInput[0] = "stack:mutated-after-capture";
        sourceStackInput.Add("stack:added-after-capture");
        Require(facts.Meal.Evidence.SourceStackIds.SequenceEqual(
                new[] { "stack:recovery-normal" },
                StringComparer.Ordinal),
            "Meal fact retained an alias to the mutable source-stack list.");

        using var published = new Fixture("run:character-consumption-fact-recovery");
        string mealKey = Publish(published, facts.Meal);
        string substanceKey = Publish(published, facts.Substance);
        string detoxKey = Publish(published, facts.Detox);
        string primitiveKey = Publish(published, facts.Primitive);
        string[] publishedKeys =
        {
            mealKey,
            substanceKey,
            detoxKey,
            primitiveKey
        };
        Require(published.ExactCount == 4
                && publishedKeys.Distinct(StringComparer.Ordinal).Count() == 4
                && ExactKeys(published).SetEquals(publishedKeys),
            "Normal publication did not create four distinct exact outcomes.");
        string[] expectedTypes =
        {
            OutcomeType(MigratedProducerOutcomeKind.CharacterConsumablesMealResult),
            OutcomeType(MigratedProducerOutcomeKind.CharacterConsumablesSubstanceResult),
            OutcomeType(MigratedProducerOutcomeKind.CharacterDetoxTreatmentResult),
            OutcomeType(MigratedProducerOutcomeKind.CharacterPrimitiveSurvivalCompletedEvent)
        };
        Require(published.ExactOutcomeTypes.SetEquals(expectedTypes),
            "Normal publication did not preserve all four mapped outcome types.");

        string beforeCommittedReplay = published.CaptureLedgerJson();
        var committedReplayResults = new DomainFactDelivery[4];
        Require(published.Dispatcher.ReplayBatch(
                new DomainFactReplay[]
                {
                    DomainFactReplay.Create(mealKey, facts.Meal),
                    DomainFactReplay.Create(substanceKey, facts.Substance),
                    DomainFactReplay.Create(detoxKey, facts.Detox),
                    DomainFactReplay.Create(primitiveKey, facts.Primitive)
                },
                committedReplayResults,
                out string committedReplayFailure)
            && string.IsNullOrEmpty(committedReplayFailure)
            && committedReplayResults.All(result => result.Completed)
            && committedReplayResults.Select(result => result.OperationKey)
                .SequenceEqual(publishedKeys, StringComparer.Ordinal)
            && published.ExactCount == 4
            && string.Equals(
                beforeCommittedReplay,
                published.CaptureLedgerJson(),
                StringComparison.Ordinal),
            "Exact-key replay changed or duplicated the four committed outcomes.");

        string beforeWrongKey = published.CaptureLedgerJson();
        var wrongKeyResults = new DomainFactDelivery[1];
        Require(!published.Dispatcher.ReplayBatch(
                new[]
                {
                    DomainFactReplay.Create(mealKey + ":wrong", facts.Meal)
                },
                wrongKeyResults,
                out string wrongKeyFailure)
            && !string.IsNullOrWhiteSpace(wrongKeyFailure)
            && wrongKeyResults[0].Phase == DomainFactDeliveryPhase.Rejected
            && string.Equals(
                beforeWrongKey,
                published.CaptureLedgerJson(),
                StringComparison.Ordinal),
            "A wrong saved publication key changed the real ledger.");

        CharacterSubstanceConsumedFact changedSubstance = new(
            facts.Substance.OperationId,
            facts.Substance.CharacterId,
            facts.Substance.AbsoluteDay,
            facts.Substance.ItemId,
            facts.Substance.Tolerance + 1f,
            facts.Substance.Addiction,
            facts.Substance.Overdosed);
        string beforeChangedPayload = published.CaptureLedgerJson();
        var changedPayloadResults = new DomainFactDelivery[1];
        Require(!published.Dispatcher.ReplayBatch(
                new[]
                {
                    DomainFactReplay.Create(
                        substanceKey,
                        changedSubstance)
                },
                changedPayloadResults,
                out string changedPayloadFailure)
            && !string.IsNullOrWhiteSpace(changedPayloadFailure)
            && changedPayloadResults[0].Phase
                == DomainFactDeliveryPhase.Rejected
            && string.Equals(
                beforeChangedPayload,
                published.CaptureLedgerJson(),
                StringComparison.Ordinal),
            "A changed payload reused its saved key or changed the real ledger.");

        Facts pendingFacts = CreateFacts(
            "pending",
            out List<string> pendingSourceStackInput);
        using var pending = new Fixture(
            "run:character-consumption-fact-pending-recovery");
        DomainFactLease pendingMealLease = Reserve(
            pending,
            pendingFacts.Meal,
            out string pendingMealKey);
        DomainFactLease pendingPrimitiveLease = Reserve(
            pending,
            pendingFacts.Primitive,
            out string pendingPrimitiveKey);
        pending.Dispatcher.Cancel(pendingMealLease);
        pending.Dispatcher.Cancel(pendingPrimitiveLease);
        Require(!pendingMealLease.IsValid
                && !pendingPrimitiveLease.IsValid
                && pending.ExactCount == 0,
            "Cancelling transient reservations committed a pending fact.");

        var pendingResults = new DomainFactDelivery[2];
        Require(pending.Dispatcher.ReplayBatch(
                new DomainFactReplay[]
                {
                    DomainFactReplay.Create(
                        pendingMealKey,
                        pendingFacts.Meal),
                    DomainFactReplay.Create(
                        pendingPrimitiveKey,
                        pendingFacts.Primitive)
                },
                pendingResults,
                out string pendingFailure)
            && string.IsNullOrEmpty(pendingFailure)
            && pendingResults.All(result => result.Completed)
            && pendingResults[0].OperationKey == pendingMealKey
            && pendingResults[1].OperationKey == pendingPrimitiveKey
            && pending.ExactCount == 2
            && ExactKeys(pending).SetEquals(
                new[] { pendingMealKey, pendingPrimitiveKey }),
            "The meal+primitive pending recovery cohort did not commit both exact saved keys.");
        string afterPendingCommit = pending.CaptureLedgerJson();
        var pendingReplayResults = new DomainFactDelivery[2];
        Require(pending.Dispatcher.ReplayBatch(
                new DomainFactReplay[]
                {
                    DomainFactReplay.Create(
                        pendingMealKey,
                        pendingFacts.Meal),
                    DomainFactReplay.Create(
                        pendingPrimitiveKey,
                        pendingFacts.Primitive)
                },
                pendingReplayResults,
                out string pendingReplayFailure)
            && string.IsNullOrEmpty(pendingReplayFailure)
            && pendingReplayResults.All(result => result.Completed)
            && pending.ExactCount == 2
            && string.Equals(
                afterPendingCommit,
                pending.CaptureLedgerJson(),
                StringComparison.Ordinal),
            "Replaying the recovered pending cohort generated duplicate outcomes.");

        Require(pendingSourceStackInput.SequenceEqual(
                new[] { "stack:recovery-pending" },
                StringComparer.Ordinal),
            "Pending fixture input changed during fact publication.");
        return "Character consumption fact recovery PASS; "
            + "normal-publish=4; committed-replay-duplicates=0; "
            + "wrong-key=Rejected; changed-payload=Rejected; "
            + "pending-cohort=meal+primitive; pending-replay-duplicates=0; "
            + "source-stack-copy=deep; scope=observer-ledger-contract";
    }

    private static string Publish<TFact>(Fixture fixture, TFact fact)
    {
        DomainFactLease lease = Reserve(fixture, fact, out string key);
        DomainFactDelivery delivered = fixture.Dispatcher.Publish(lease, fact);
        Require(delivered.Completed
                && delivered.OperationKey == key
                && !lease.IsValid
                && string.IsNullOrEmpty(lease.PublicationKey),
            "Normal domain fact publication did not commit its exact saved key.");
        return key;
    }

    private static DomainFactLease Reserve<TFact>(
        Fixture fixture,
        TFact fact,
        out string publicationKey)
    {
        Require(fixture.Dispatcher.TryReserve(
                fact,
                out DomainFactLease lease,
                out string failure)
            && lease.IsValid
            && string.IsNullOrEmpty(failure),
            "Could not reserve a character-consumption fact.");
        publicationKey = lease.PublicationKey;
        Require(!string.IsNullOrWhiteSpace(publicationKey),
            "A character-consumption reservation exposed no publication key.");
        return lease;
    }

    private static Facts CreateFacts(
        string suffix,
        out List<string> sourceStackIds)
    {
        var actorId = new CharacterId("character:fact-recovery-" + suffix);
        const int AbsoluteDay = 17;
        sourceStackIds = new List<string>
        {
            "stack:recovery-" + suffix
        };
        var effects = new CharacterConsumablesEffectEvidence(
            hungerBefore: 30f,
            hungerAfter: 65f,
            healthBefore: 90f,
            healthAfter: 92f,
            moodBefore: 4f,
            moodAfter: 7f);
        var evidence = new CharacterConsumablesOutcomeEvidence(
            default,
            new ConsumableItemDefinitionId("food:recovery-ration"),
            sourceStackIds,
            "physical-commit:recovery-" + suffix,
            consumedQuantity: 1,
            consumedMassGrams: 250L,
            effects: effects,
            policyViolation: false,
            contaminated: false,
            contaminationDamageApplied: false);
        return new Facts(
            new CharacterMealConsumedFact(
                Operation("meal", suffix),
                actorId,
                AbsoluteDay,
                evidence,
                recipientActiveAtCompletion: true),
            new CharacterSubstanceConsumedFact(
                Operation("substance", suffix),
                actorId,
                AbsoluteDay,
                new ConsumableItemDefinitionId("drug:recovery-tonic"),
                tolerance: 12f,
                addiction: 3f,
                overdosed: false),
            new CharacterDetoxAppliedFact(
                Operation("detox", suffix),
                actorId,
                AbsoluteDay,
                new ConsumableItemDefinitionId("medicine:recovery-antidote"),
                appliedReduction: 20f),
            new CharacterPrimitiveMealCompletedFact(
                Operation("primitive", suffix),
                actorId,
                AbsoluteDay,
                nutrition: 35f));
    }

    private static ConsumableOperationId Operation(
        string kind,
        string suffix) => new(
        "consumable-operation:fact-recovery-" + kind + "-" + suffix);

    private static string OutcomeType(MigratedProducerOutcomeKind kind) =>
        MigratedProducerOutcomeCatalog.Get(kind).OutcomeTypeId.Value;

    private static HashSet<string> ExactKeys(Fixture fixture) => new(
        fixture.Outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes.Select(
            value => new GameplayResultKey(
                value.producerId,
                new GameplayOperationId(value.operationId),
                value.commitRevision,
                value.localResultIndex).ToString()),
        StringComparer.Ordinal);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Facts
    {
        internal Facts(
            CharacterMealConsumedFact meal,
            CharacterSubstanceConsumedFact substance,
            CharacterDetoxAppliedFact detox,
            CharacterPrimitiveMealCompletedFact primitive)
        {
            Meal = meal;
            Substance = substance;
            Detox = detox;
            Primitive = primitive;
        }

        internal CharacterMealConsumedFact Meal { get; }
        internal CharacterSubstanceConsumedFact Substance { get; }
        internal CharacterDetoxAppliedFact Detox { get; }
        internal CharacterPrimitiveMealCompletedFact Primitive { get; }
    }

    private sealed class Fixture : IDisposable
    {
        internal Fixture(string runId)
        {
            Outcomes = new MigratedProducerOutcomeEditorFixture(runId);
            var commits = new GameplayDomainFactCommitGroup(
                Outcomes.Recorder,
                Outcomes.Ledger);
            Dispatcher = new DomainFactDispatcher();
            try
            {
                Dispatcher.Subscribe(
                    "test:consumption-recovery:meal",
                    new MigratedDomainFactObserver<CharacterMealConsumedFact>(
                        CharacterConsumptionFactMapping.Capture,
                        Outcomes.Transaction,
                        commits));
                Dispatcher.Subscribe(
                    "test:consumption-recovery:substance",
                    new MigratedDomainFactObserver<
                        CharacterSubstanceConsumedFact>(
                        CharacterConsumptionFactMapping.Capture,
                        Outcomes.Transaction,
                        commits));
                Dispatcher.Subscribe(
                    "test:consumption-recovery:detox",
                    new MigratedDomainFactObserver<CharacterDetoxAppliedFact>(
                        CharacterConsumptionFactMapping.Capture,
                        Outcomes.Transaction,
                        commits));
                Dispatcher.Subscribe(
                    "test:consumption-recovery:primitive",
                    new MigratedDomainFactObserver<
                        CharacterPrimitiveMealCompletedFact>(
                        CharacterConsumptionFactMapping.Capture,
                        Outcomes.Transaction,
                        commits));
            }
            catch
            {
                Dispatcher.Dispose();
                throw;
            }
        }

        internal MigratedProducerOutcomeEditorFixture Outcomes { get; }
        internal DomainFactDispatcher Dispatcher { get; }
        internal int ExactCount => Outcomes.Ledger.CaptureGameplayOutcomes()
            .exactOutcomes.Count;
        internal HashSet<string> ExactOutcomeTypes => new(
            Outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes
                .Select(value => value.outcomeTypeId),
            StringComparer.Ordinal);
        internal string CaptureLedgerJson() => JsonUtility.ToJson(
            Outcomes.Ledger.CaptureGameplayOutcomes());
        public void Dispose() => Dispatcher.Dispose();
    }
}
#endif
