#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionActiveMultiFacilityRetargetDebugScenarios
{
    [MenuItem(
        "DungeonStory/V27/Physical Mass/Verify Active Multi-Facility Retarget")]
    public static void VerifyFromMenu()
    {
        VerifyNFacilityProjectionPreservesExactIdentityAndCustody();
        VerifyAllSucceedInStableOrder();
        VerifyOneFailureRestoresExactAuthoritySet();
        Debug.Log(
            "[V27][PASS] Active bill/WIP/physical-custody N-facility retarget is sorted, all-or-none, exact-identity preserving, carried/claimed aware, and exact on rollback.");
    }

    private static void
        VerifyNFacilityProjectionPreservesExactIdentityAndCustody()
    {
        Fixture fixture = Fixture.Create();
        Require(ProductionActiveFacilityRetargetSnapshotProjector.TryProject(
                fixture.Source,
                fixture.Requests,
                fixture.Bindings,
                out ProductionActiveFacilityRetargetSnapshot projected,
                out string failureReason),
            "Active authority projection failed: " + failureReason);

        string targetId = fixture.Target.InstanceId.Value;
        ProductionBillSaveData[] projectedBills = projected.Bills.bills
            .OrderBy(value => value.billId, StringComparer.Ordinal)
            .ToArray();
        ProductionBillSaveData[] sourceBills = fixture.Source.Bills.bills
            .OrderBy(value => value.billId, StringComparer.Ordinal)
            .ToArray();
        Require(projectedBills.Length == 3
            && projectedBills.All(value => value.buildingInstanceId == targetId)
            && projectedBills.Select(value => value.billId)
                .SequenceEqual(sourceBills.Select(value => value.billId))
            && projectedBills.Select(value => value.wipInputCommitId)
                .SequenceEqual(sourceBills.Select(value => value.wipInputCommitId))
            && projectedBills.Select(value => value.wipInputQuantity)
                .SequenceEqual(sourceBills.Select(value => value.wipInputQuantity))
            && projectedBills.Select(value => value.wipInputMassGrams)
                .SequenceEqual(sourceBills.Select(value => value.wipInputMassGrams))
            && projectedBills.Select(value => value.completedWork)
                .SequenceEqual(sourceBills.Select(value => value.completedWork)),
            "Bill or active-WIP identity drifted during N-to-one projection.");

        Dictionary<string, WorldItemStackSaveData> beforeStacks =
            fixture.Source.PhysicalItems.stacks.ToDictionary(
                value => value.stackId,
                StringComparer.Ordinal);
        Dictionary<string, WorldItemStackSaveData> afterStacks =
            projected.PhysicalItems.stacks.ToDictionary(
                value => value.stackId,
                StringComparer.Ordinal);
        Require(beforeStacks.Keys.OrderBy(value => value, StringComparer.Ordinal)
                .SequenceEqual(afterStacks.Keys.OrderBy(
                    value => value,
                    StringComparer.Ordinal))
            && beforeStacks.All(pair =>
                afterStacks[pair.Key].itemId == pair.Value.itemId
                && afterStacks[pair.Key].itemInstanceId
                    == pair.Value.itemInstanceId
                && afterStacks[pair.Key].quantity == pair.Value.quantity
                && afterStacks[pair.Key].reservedByPersistentId
                    == pair.Value.reservedByPersistentId),
            "Physical stack identity, quantity, unique identity, or claim drifted.");

        WorldItemStackSaveData claimed = afterStacks["stack:claimed-input"];
        Require(claimed.state == WorldItemStackState.FacilityBuffer
            && claimed.gridX == fixture.Target.Position.x
            && claimed.gridY == fixture.Target.Position.y
            && claimed.destinationId == "production-input:bill:a"
            && claimed.reservedByPersistentId == "character:claim-owner",
            "Claimed input custody was released or left at the retired facility.");

        WorldItemStackSaveData carried = afterStacks["stack:carried-output"];
        HaulDeliveryIntentSaveData carriedIntent = projected.HaulIntents.Single();
        Require(carried.state == WorldItemStackState.Carried
            && carried.gridX == beforeStacks[carried.stackId].gridX
            && carried.gridY == beforeStacks[carried.stackId].gridY
            && carried.destinationId == "character:carrier"
            && carriedIntent.commitments.Single().carriedStackId
                == carried.stackId
            && carriedIntent.destinationId ==
                ProductionBillRuntime.OutputDestinationPrefix + targetId
            && carriedIntent.deliveryGridX == fixture.Target.Position.x
            && carriedIntent.deliveryGridY == fixture.Target.Position.y,
            "Carried custody moved physically or lost its exact delivery commitment.");

        WorldItemStackSaveData buffered = afterStacks["stack:buffered-output"];
        Require(buffered.state == WorldItemStackState.FacilityOutputBuffer
            && buffered.destinationId ==
                ProductionBillRuntime.OutputDestinationPrefix + targetId
            && buffered.gridX == fixture.Target.Position.x
            && buffered.gridY == fixture.Target.Position.y,
            "Facility-buffer physical custody was not retargeted exactly.");
    }

    private static void VerifyAllSucceedInStableOrder()
    {
        Fixture fixture = Fixture.Create();
        FakeStateStore store = new(fixture.Source);
        ProductionFacilityMutationEpochRuntime epochs = new();
        ProductionFacilityRetargetTransaction transaction = Runtime(
            store,
            epochs,
            failAfterAuthorityMutation: false);

        Require(transaction.TryBegin(
                fixture.Requests.Reverse().ToArray(),
                "qa:active-retarget:success",
                out ProductionFacilityRetargetTransactionState state,
                out string beginFailure),
            "Active retarget begin failed: " + beginFailure);
        Require(transaction.TryCommit(
                state,
                fixture.Bindings.Reverse().ToArray(),
                out string commitFailure),
            "Active retarget commit failed: " + commitFailure);
        Require(transaction.TryComplete(state, out string completeFailure),
            "Active retarget completion failed: " + completeFailure);
        Require(store.LastAppliedSources.SequenceEqual(new[]
            {
                "building:qa:active-retarget:a",
                "building:qa:active-retarget:b",
                "building:qa:active-retarget:c"
            })
            && store.Current.Bills.bills.All(value =>
                value.buildingInstanceId == fixture.Target.InstanceId.Value)
            && fixture.Requests.All(value => !epochs.IsFrozen(
                value.SourceFacilityId)),
            "Successful active retarget was not source-sorted or fully committed.");
    }

    private static void VerifyOneFailureRestoresExactAuthoritySet()
    {
        Fixture fixture = Fixture.Create();
        FakeStateStore store = new(fixture.Source);
        string before = store.Current.Fingerprint;
        ProductionFacilityMutationEpochRuntime epochs = new();
        ProductionFacilityRetargetTransaction transaction = Runtime(
            store,
            epochs,
            failAfterAuthorityMutation: true);

        Require(transaction.TryBegin(
                fixture.Requests,
                "qa:active-retarget:failure",
                out ProductionFacilityRetargetTransactionState state,
                out string beginFailure),
            "Failure fixture begin failed: " + beginFailure);
        Require(!transaction.TryCommit(
                state,
                fixture.Bindings,
                out string failureReason)
            && failureReason.StartsWith(
                "production-facility-retarget-commit-failed:z-injected-failure:",
                StringComparison.Ordinal),
            "Injected post-authority failure did not fail the transaction.");
        Require(string.Equals(
                store.Current.Fingerprint,
                before,
                StringComparison.Ordinal)
            && Equivalent(fixture.Source.Bills, store.Current.Bills)
            && Equivalent(fixture.Source.PhysicalItems,
                store.Current.PhysicalItems)
            && fixture.Source.HaulIntents.Single().destinationId
                == store.Current.HaulIntents.Single().destinationId,
            "Failure left partial bill, WIP, physical, claim, or carry ownership drift.");
        Require(transaction.TryRollback(state, out string rollbackFailure)
            && fixture.Requests.All(value => !epochs.IsFrozen(
                value.SourceFacilityId)),
            "Failed transaction did not close all source epochs: "
            + rollbackFailure);
    }

    private static ProductionFacilityRetargetTransaction Runtime(
        FakeStateStore store,
        ProductionFacilityMutationEpochRuntime epochs,
        bool failAfterAuthorityMutation)
    {
        ProductionActiveMultiFacilityRetargetAdapter adapter = new(store);
        ProductionFacilityRetargetAuthorityParticipant authority = new(
            new[] { adapter });
        InjectedFailureParticipant failure = new(failAfterAuthorityMutation);
        return new ProductionFacilityRetargetTransaction(
            new ProductionFacilityRetargetParticipantRegistry(
                new IProductionFacilityRetargetParticipant[]
                {
                    failure,
                    authority
                }),
            epochs);
    }

    private static bool Equivalent(object left, object right) =>
        string.Equals(
            JsonUtility.ToJson(left),
            JsonUtility.ToJson(right),
            StringComparison.Ordinal);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        private Fixture(
            ProductionActiveFacilityRetargetSnapshot source,
            ProductionFacilityHandle target,
            ProductionFacilityRetargetRequest[] requests,
            ProductionFacilityRetargetBinding[] bindings)
        {
            Source = source;
            Target = target;
            Requests = requests;
            Bindings = bindings;
        }

        internal ProductionActiveFacilityRetargetSnapshot Source { get; }
        internal ProductionFacilityHandle Target { get; }
        internal ProductionFacilityRetargetRequest[] Requests { get; }
        internal ProductionFacilityRetargetBinding[] Bindings { get; }

        internal static Fixture Create()
        {
            ProductionFacilityHandle sourceA = Facility(
                "building:qa:active-retarget:a",
                new Vector2Int(3, 4));
            ProductionFacilityHandle sourceB = Facility(
                "building:qa:active-retarget:b",
                new Vector2Int(8, 4));
            ProductionFacilityHandle sourceC = Facility(
                "building:qa:active-retarget:c",
                new Vector2Int(13, 4));
            ProductionFacilityHandle target = Facility(
                sourceA.InstanceId.Value,
                new Vector2Int(21, 9));
            ProductionFacilityRetargetRequest[] requests =
            {
                new(sourceA, ProductionFacilityMutationKind.Synthesis),
                new(sourceB, ProductionFacilityMutationKind.Synthesis),
                new(sourceC, ProductionFacilityMutationKind.Synthesis)
            };
            ProductionFacilityRetargetBinding[] bindings =
            {
                new(sourceA.InstanceId, target),
                new(sourceB.InstanceId, target),
                new(sourceC.InstanceId, target)
            };

            DungeonProductionBillSaveData bills = new()
            {
                bills = new List<ProductionBillSaveData>
                {
                    Bill("a", sourceA, 2, 1375L, 1.25f),
                    Bill("b", sourceB, 3, 2050L, 2.5f),
                    Bill("c", sourceC, 1, 725L, 0.75f)
                }
            };
            DungeonPhysicalItemSaveData physical = new()
            {
                stacks = new List<WorldItemStackSaveData>
                {
                    new()
                    {
                        stackId = "stack:claimed-input",
                        itemId = "item:ore",
                        quantity = 2,
                        state = WorldItemStackState.FacilityBuffer,
                        gridX = sourceA.Position.x,
                        gridY = sourceA.Position.y,
                        destinationId = "production-input:bill:a",
                        hasDestinationPosition = true,
                        destinationGridX = sourceA.Position.x,
                        destinationGridY = sourceA.Position.y,
                        reservedByPersistentId = "character:claim-owner"
                    },
                    new()
                    {
                        stackId = "stack:carried-output",
                        itemInstanceId = "item-instance:carried-output",
                        itemId = "item:crafted",
                        quantity = 1,
                        state = WorldItemStackState.Carried,
                        gridX = 9,
                        gridY = 7,
                        destinationId = "character:carrier"
                    },
                    new()
                    {
                        stackId = "stack:buffered-output",
                        itemId = "item:crafted",
                        quantity = 4,
                        state = WorldItemStackState.FacilityOutputBuffer,
                        gridX = sourceC.Position.x,
                        gridY = sourceC.Position.y,
                        destinationId = ProductionBillRuntime.OutputDestinationPrefix
                            + sourceC.InstanceId.Value,
                        hasDestinationPosition = true,
                        destinationGridX = sourceC.Position.x,
                        destinationGridY = sourceC.Position.y
                    }
                }
            };
            HaulDeliveryIntentSaveData intent = new()
            {
                operationId = "haul:character:carrier:000000000001",
                ownerCharacterId = "character:carrier",
                destinationKind = WorldItemHaulDestinationKind.FacilityBuffer,
                destinationId = ProductionBillRuntime.OutputDestinationPrefix
                    + sourceB.InstanceId.Value,
                deliveryGridX = sourceB.Position.x,
                deliveryGridY = sourceB.Position.y,
                dropGridX = sourceB.Position.x,
                dropGridY = sourceB.Position.y,
                commitments = new List<HaulDeliveryItemCommitmentSaveData>
                {
                    new()
                    {
                        carriedStackId = "stack:carried-output",
                        sourceStackId = "stack:source-output",
                        itemId = "item:crafted",
                        expectedStackSignature = "signature:crafted",
                        quantity = 1
                    }
                }
            };
            return new Fixture(
                new ProductionActiveFacilityRetargetSnapshot(
                    bills,
                    physical,
                    new[] { intent }),
                target,
                requests,
                bindings);
        }

        private static ProductionBillSaveData Bill(
            string suffix,
            ProductionFacilityHandle facility,
            int quantity,
            long grams,
            float work) => new()
        {
            billId = "production-bill:" + suffix,
            recipeId = "recipe:" + suffix,
            buildingInstanceId = facility.InstanceId.Value,
            materialsConsumed = true,
            wipInputCommitId = "wip-commit:" + suffix,
            wipInputQuantity = quantity,
            wipInputMassGrams = grams,
            completedWork = work,
            materialDestinationId = "production-input:bill:" + suffix,
            outputDestinationId = ProductionBillRuntime.OutputDestinationPrefix
                + facility.InstanceId.Value,
            preparedOutput = new ProductionPreparedOutputBatchSaveData()
        };

        private static ProductionFacilityHandle Facility(
            string id,
            Vector2Int position) => new(
            new object(),
            (BuildingInstanceId)id,
            position,
            false,
            string.Empty,
            false,
            default,
            "building-definition:qa",
            "workstation:qa",
            2);
    }

    private sealed class FakeStateStore :
        IProductionActiveFacilityRetargetStateStore
    {
        internal FakeStateStore(
            ProductionActiveFacilityRetargetSnapshot source) => Current = source;

        internal ProductionActiveFacilityRetargetSnapshot Current { get; private set; }
        internal IReadOnlyList<string> LastAppliedSources { get; private set; } =
            Array.Empty<string>();

        public bool TryCapture(
            IReadOnlyList<ProductionFacilityRetargetRequest> orderedRequests,
            out ProductionActiveFacilityRetargetSnapshot snapshot,
            out string failureReason)
        {
            snapshot = Current;
            failureReason = string.Empty;
            return true;
        }

        public bool TryApply(
            ProductionActiveFacilityRetargetSnapshot source,
            IReadOnlyList<ProductionFacilityRetargetRequest> orderedRequests,
            IReadOnlyList<ProductionFacilityRetargetBinding> orderedBindings,
            out ProductionActiveFacilityRetargetSnapshot published,
            out string failureReason)
        {
            LastAppliedSources = orderedRequests
                .Select(value => value.SourceFacilityId.Value)
                .ToArray();
            if (!ProductionActiveFacilityRetargetSnapshotProjector.TryProject(
                    source,
                    orderedRequests,
                    orderedBindings,
                    out published,
                    out failureReason))
            {
                return false;
            }
            Current = published;
            return true;
        }

        public bool TryRestore(
            ProductionActiveFacilityRetargetSnapshot source,
            IReadOnlyList<ProductionFacilityRetargetRequest> orderedRequests,
            out string restoredFingerprint,
            out string failureReason)
        {
            Current = source;
            restoredFingerprint = Current.Fingerprint;
            failureReason = string.Empty;
            return true;
        }

        public bool TryCaptureCurrentFingerprint(
            out string fingerprint,
            out string failureReason)
        {
            fingerprint = Current.Fingerprint;
            failureReason = string.Empty;
            return true;
        }
    }

    private sealed class InjectedFailureParticipant :
        IProductionFacilityRetargetParticipant
    {
        private readonly bool fail;

        internal InjectedFailureParticipant(bool fail) => this.fail = fail;

        public string ParticipantId => "z-injected-failure";

        public bool TryPrepare(
            IReadOnlyList<ProductionFacilityRetargetRequest> orderedRequests,
            string operationId,
            out ProductionFacilityRetargetParticipantPlan plan,
            out string failureReason)
        {
            FixtureState state = new(Fingerprint("failure:staged"));
            plan = ProductionFacilityRetargetParticipantPlan.Create(
                ParticipantId,
                state.Fingerprint,
                state);
            failureReason = string.Empty;
            return true;
        }

        public bool TryCommit(
            ProductionFacilityRetargetParticipantPlan plan,
            IReadOnlyList<ProductionFacilityRetargetBinding> orderedBindings,
            out string committedFingerprint,
            out string failureReason)
        {
            FixtureState state = (FixtureState)plan.ParticipantState;
            state.Fingerprint = Fingerprint("failure:committed");
            committedFingerprint = state.Fingerprint;
            failureReason = fail ? "injected-after-active-authority" : string.Empty;
            return !fail;
        }

        public bool TryRollback(
            ProductionFacilityRetargetParticipantPlan plan,
            out string rolledBackFingerprint,
            out string failureReason)
        {
            FixtureState state = (FixtureState)plan.ParticipantState;
            state.Fingerprint = Fingerprint("failure:staged");
            rolledBackFingerprint = state.Fingerprint;
            failureReason = string.Empty;
            return true;
        }

        public bool TryCaptureCurrentFingerprint(
            ProductionFacilityRetargetParticipantPlan plan,
            out string currentFingerprint,
            out string failureReason)
        {
            currentFingerprint = ((FixtureState)plan.ParticipantState).Fingerprint;
            failureReason = string.Empty;
            return true;
        }

        private static string Fingerprint(string value) =>
            ProductionFacilityDestructiveDrainCanonical.ComputeFingerprint(value);

        private sealed class FixtureState
        {
            internal FixtureState(string fingerprint) => Fingerprint = fingerprint;
            internal string Fingerprint { get; set; }
        }
    }
}
#endif
