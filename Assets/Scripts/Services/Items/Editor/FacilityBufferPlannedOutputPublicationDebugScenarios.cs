#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public static class FacilityBufferPlannedOutputPublicationDebugScenarios
{
    public static void RunAll()
    {
        VerifyAtomicPublicationAndReplay();
        VerifyConflictingPartialPublicationIsPreserved();
        VerifyInjectedFailureRollsBackEveryStack();
        VerifyExactRestoreCandidateAndPartialFailure();
        VerifyExactRollbackAndDurableAcknowledgement();
        VerifyRestoreCandidateRollbackAndAcknowledgement();
        VerifyUniqueEquipmentAtomicPublicationAndRestore();
        VerifyGenericMaxStackOneAllocatesAndCommitsIdentity();
    }

    private static void VerifyAtomicPublicationAndReplay()
    {
        Fixture fixture = new();
        FacilityBufferPlannedOutputToken token = fixture.Reserve("batch:atomic:001");
        Require(
            fixture.Publication.TryPublishFullBatch(
                token,
                out FacilityBufferPlannedOutputPublicationReceipt first,
                out _,
                out _),
            "Atomic planned-output publication failed.");
        Require(
            first.Stacks.Count == 4
            && first.Stacks.Select(value => value.Quantity)
                .SequenceEqual(new[] { 2, 2, 1, 2 })
            && fixture.Publication.CaptureEditorTestSnapshot().Stacks.Count == 4
            && fixture.Publication.CaptureEditorTestSnapshot().Stacks.All(record =>
                record.State == WorldItemStackState.FacilityOutputBuffer
                && record.DestinationId == Fixture.DestinationId
                && record.Position == Fixture.DropPosition),
            "Planned-output deterministic stack split was not exact.");
        string[] identities = first.Stacks.Select(value => value.StackId).ToArray();
        Require(
            fixture.Publication.TryPublishFullBatch(
                token,
                out FacilityBufferPlannedOutputPublicationReceipt replay,
                out _,
                out _)
            && replay.Stacks.Select(value => value.StackId).SequenceEqual(identities)
            && fixture.Publication.CaptureEditorTestSnapshot().Stacks.Count == 4,
            "Exact batch replay duplicated or replaced physical stacks.");
        Require(
            fixture.Admission.TryCommitPlannedOutput(
                token,
                replay,
                out FacilityBufferPlannedOutputReceipt committed,
                out _,
                out _)
            && committed.CommittedMassGrams == 6_000L,
            "Atomic publication receipt did not close planned admission exactly.");
        Require(
            fixture.Publication.TryPublishFullBatch(
                token,
                out FacilityBufferPlannedOutputPublicationReceipt committedReplay,
                out _,
                out _)
            && committedReplay.Stacks.Select(value => value.StackId)
                .SequenceEqual(identities)
            && fixture.Publication.CaptureEditorTestSnapshot().Stacks.Count == 4,
            "Committed exact replay was not idempotent by batch commit ID.");
    }

    private static void VerifyConflictingPartialPublicationIsPreserved()
    {
        Fixture fixture = new();
        FacilityBufferPlannedOutputToken token = fixture.Reserve("batch:conflict:001");
        Require(
            fixture.Publication.TryPublishFullBatch(token, out _, out _, out _),
            "Conflict fixture did not publish its initial batch.");
        string firstStackId = fixture.Publication.CaptureEditorTestSnapshot()
            .Stacks.First().StackId;
        fixture.Publication.DecrementFirstStackQuantityForEditorTest();
        int countBefore = fixture.Publication.CaptureEditorTestSnapshot()
            .Stacks.Count;
        Require(
            !fixture.Publication.TryPublishFullBatch(
                token,
                out _,
                out FacilityBufferPlannedOutputPublicationFailureCode failure,
                out _)
            && failure
                == FacilityBufferPlannedOutputPublicationFailureCode.ExistingPublicationConflict
            && fixture.Publication.CaptureEditorTestSnapshot().Stacks.Count
                == countBefore
            && fixture.Publication.CaptureEditorTestSnapshot().Stacks.Any(value =>
                value.StackId == firstStackId),
            "Conflicting partial publication was deleted or accepted.");
    }

    private static void VerifyInjectedFailureRollsBackEveryStack()
    {
        Fixture fixture = new(new FailAtStackIndex(1));
        FacilityBufferPlannedOutputToken token = fixture.Reserve("batch:rollback:001");
        int versionBefore = fixture.Repository.ItemStackVersion;
        Require(
            !fixture.Publication.TryPublishFullBatch(
                token,
                out _,
                out FacilityBufferPlannedOutputPublicationFailureCode failure,
                out _)
            && failure
                == FacilityBufferPlannedOutputPublicationFailureCode.RepositoryTransactionFailed
            && fixture.Publication.CaptureEditorTestSnapshot().Stacks.Count == 0
            && fixture.Repository.ItemStackVersion == versionBefore
            && fixture.Admission.TryValidatePlannedOutputReservation(token, out _, out _),
            "Injected publication failure left a physical prefix or consumed admission.");
    }

    private static void VerifyExactRestoreCandidateAndPartialFailure()
    {
        Fixture fixture = new();
        FacilityBufferPlannedOutputToken token = fixture.Reserve("batch:restore:001");
        Require(
            fixture.Publication.TryPublishFullBatch(token, out _, out _, out _),
            "Restore-candidate fixture did not publish.");
        IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot> batches =
            fixture.Publication.CapturePendingRestoreBatchesForEditorTest();
        Require(
            batches.Count == 1
            && batches[0].Stacks.Count == 4
            && batches[0].TotalQuantity == 7
            && batches[0].TotalMassGrams == 6_000L,
            "Restore candidate did not expose the exact physical batch.");

        fixture.Publication.RemoveFirstStackForEditorTest();
        RequireThrows(
            () => fixture.Publication
                .CapturePendingRestoreBatchesForEditorTest(),
            "Partial planned-output restore candidate did not fail loudly.");
    }

    private static void VerifyExactRollbackAndDurableAcknowledgement()
    {
        Fixture rollbackFixture = new();
        FacilityBufferPlannedOutputToken rollbackToken =
            rollbackFixture.Reserve("batch:rollback-exact:001");
        Require(
            rollbackFixture.Publication.TryPublishFullBatch(
                rollbackToken,
                out FacilityBufferPlannedOutputPublicationReceipt rollbackReceipt,
                out _,
                out _),
            "Exact rollback fixture did not publish.");
        FacilityBufferPublishedOutputStackReceipt first = rollbackReceipt.Stacks[0];
        FacilityBufferPublishedOutputStackReceipt[] tamperedStacks =
            rollbackReceipt.Stacks.ToArray();
        tamperedStacks[0] = new FacilityBufferPublishedOutputStackReceipt(
            first.StackId,
            first.OutputLineId,
            first.ItemDefinitionId,
            first.Quantity + 1,
            first.Mass);
        FacilityBufferPlannedOutputPublicationReceipt tampered = new(
            rollbackReceipt.AdmissionTokenId,
            rollbackReceipt.BatchCommitId,
            rollbackReceipt.OutcomeFingerprint,
            rollbackReceipt.DestinationId,
            rollbackReceipt.DropPosition,
            rollbackReceipt.OwnerDomain,
            rollbackReceipt.OwnerOperationId,
            rollbackReceipt.OwnerFacilityId,
            rollbackReceipt.CapacityRevision,
            rollbackReceipt.PlannedOutputFingerprint,
            tamperedStacks);
        Require(
            !rollbackFixture.Publication.TryRollbackPublishedBatch(
                tampered,
                out FacilityBufferPlannedOutputPublicationFailureCode tamperedFailure,
                out _)
            && tamperedFailure ==
                FacilityBufferPlannedOutputPublicationFailureCode.ExistingPublicationConflict
            && rollbackFixture.Publication.CaptureEditorTestSnapshot().Stacks.Count
                == 4,
            "Tampered rollback receipt removed or altered the physical batch.");
        Require(
            rollbackFixture.Publication.TryRollbackPublishedBatch(
                rollbackReceipt,
                out _,
                out _)
            && rollbackFixture.Publication.CaptureEditorTestSnapshot().Stacks.Count
                == 0,
            "Exact receipt rollback did not remove the complete batch atomically.");

        Fixture acknowledgementFixture = new();
        FacilityBufferPlannedOutputToken acknowledgementToken =
            acknowledgementFixture.Reserve("batch:ack:001");
        Require(
            acknowledgementFixture.Publication.TryPublishFullBatch(
                acknowledgementToken,
                out FacilityBufferPlannedOutputPublicationReceipt receipt,
                out _,
                out _)
            && acknowledgementFixture.Admission.TryCommitPlannedOutput(
                acknowledgementToken,
                receipt,
                out _,
                out _,
                out _)
            && !acknowledgementFixture.Publication.TryRollbackPublishedBatch(
                receipt,
                out FacilityBufferPlannedOutputPublicationFailureCode committedRollback,
                out _)
            && committedRollback ==
                FacilityBufferPlannedOutputPublicationFailureCode.InvalidToken
            && acknowledgementFixture.Publication.CaptureEditorTestSnapshot()
                .Stacks.Count == 4
            && acknowledgementFixture.Publication.TryAcknowledgePublishedBatch(
                receipt,
                out _,
                out _)
            && acknowledgementFixture.Publication.TryAcknowledgePublishedBatch(
                receipt,
                out _,
                out _)
            && acknowledgementFixture.Publication.TryPublishFullBatch(
                acknowledgementToken,
                out FacilityBufferPlannedOutputPublicationReceipt
                    acknowledgedReplay,
                out _,
                out _)
            && acknowledgedReplay.Stacks.Select(value => value.StackId)
                .SequenceEqual(receipt.Stacks.Select(value => value.StackId)),
            "Planned-output acknowledgement was not atomic and idempotent.");
        Require(
            acknowledgementFixture.Publication.CaptureEditorTestSnapshot()
                .Stacks.All(record => record.MarkerCount == 1
                    && record.MarkerAffectsStacking == false)
            && acknowledgementFixture.Publication
                .CapturePendingRestoreBatchesForEditorTest().Count == 0,
            "Acknowledgement did not convert the marker to non-stacking provenance.");
    }

    private static void VerifyRestoreCandidateRollbackAndAcknowledgement()
    {
        Fixture rollbackFixture = new();
        rollbackFixture.Publication.TryPublishFullBatch(
            rollbackFixture.Reserve("batch:restore-rollback:001"),
            out _,
            out _,
            out _);
        FacilityBufferPlannedOutputRestoreBatchSnapshot rollbackCandidate =
            rollbackFixture.Publication
                .CapturePendingRestoreBatchesForEditorTest().Single();
        Require(
            rollbackFixture.Publication.TryRollbackRestoreCandidate(
                rollbackCandidate,
                out _,
                out _)
            && rollbackFixture.Publication.CaptureEditorTestSnapshot().Stacks.Count
                == 0,
            "Exact restored batch rollback was not atomic.");

        Fixture acknowledgementFixture = new();
        acknowledgementFixture.Publication.TryPublishFullBatch(
            acknowledgementFixture.Reserve("batch:restore-ack:001"),
            out _,
            out _,
            out _);
        FacilityBufferPlannedOutputRestoreBatchSnapshot acknowledgementCandidate =
            acknowledgementFixture.Publication
                .CapturePendingRestoreBatchesForEditorTest().Single();
        Require(
            acknowledgementFixture.Publication.TryAcknowledgeRestoreCandidate(
                acknowledgementCandidate,
                out _,
                out _)
            && acknowledgementFixture.Publication.TryAcknowledgeRestoreCandidate(
                acknowledgementCandidate,
                out _,
                out _)
            && acknowledgementFixture.Publication.CaptureEditorTestSnapshot()
                .Stacks.All(record => record.MarkerCount == 1
                    && record.MarkerAffectsStacking == false),
            "Restored batch acknowledgement was not exact and idempotent.");
    }

    private static void VerifyUniqueEquipmentAtomicPublicationAndRestore()
    {
        const string InstanceId = "item-instance:planned-output:dagger:001";
        Fixture failureFixture = new(new FailAtStackIndex(0));
        FacilityBufferPlannedOutputToken failureToken =
            failureFixture.ReserveUnique(
                "batch:unique:repository-failure",
                InstanceId + ":failed");
        Require(
            !failureFixture.Publication.TryPublishFullBatch(
                failureToken,
                out _,
                out FacilityBufferPlannedOutputPublicationFailureCode failure,
                out _)
            && failure == FacilityBufferPlannedOutputPublicationFailureCode
                .RepositoryTransactionFailed
            && failureFixture.Publication.CaptureEditorTestSnapshot().Stacks.Count
                == 0
            && failureFixture.Repository.EquipmentInstances.Count == 0
            && failureFixture.Admission.TryValidatePlannedOutputReservation(
                failureToken,
                out _,
                out _),
            "Unique publication failure did not roll back stack and equipment aggregate together.");

        Fixture fixture = new();
        FacilityBufferPlannedOutputToken token = fixture.ReserveUnique(
            "batch:unique:success",
            InstanceId);
        Require(
            fixture.Publication.TryPublishFullBatch(
                token,
                out FacilityBufferPlannedOutputPublicationReceipt receipt,
                out _,
                out _)
            && receipt.Stacks.Count == 1
            && receipt.Stacks[0].ItemInstanceId == InstanceId
            && fixture.Repository.EquipmentInstances.TryGetValue(
                InstanceId,
                out CombatEquipmentInstance aggregate)
            && aggregate.sourceStackId == receipt.Stacks[0].StackId,
            "Unique publication did not atomically bind stack and equipment identity.");
        WorldItemStackSnapshot published = fixture.Query.GetAllStacks().Single();
        Require(
            published.ItemInstanceId == InstanceId
            && EquipmentItemStateCodec.TryDecode(
                published.Components.Single(value => value != null
                    && value.componentTypeId == ItemInstanceComponentIds.Equipment),
                out CombatEquipmentInstance decoded,
                out _)
            && decoded.sourceStackId == published.StackId
            && decoded.instanceId == InstanceId,
            "Unique publication stack component did not match its aggregate.");
        Require(
            fixture.Publication.TryPublishFullBatch(
                token,
                out FacilityBufferPlannedOutputPublicationReceipt replay,
                out _,
                out _)
            && replay.Stacks[0].StackId == receipt.Stacks[0].StackId
            && replay.Stacks[0].ItemInstanceId == InstanceId
            && fixture.Repository.EquipmentInstances.Count == 1,
            "Unique publication replay duplicated or changed aggregate identity.");

        FacilityBufferPlannedOutputRestoreBatchSnapshot candidate = fixture
            .Publication.CapturePendingRestoreBatchesForEditorTest().Single();
        FacilityBufferPlannedOutputRestoreStackSnapshot stack =
            candidate.Stacks.Single();
        FacilityBufferPlannedOutputRestoreBatchSnapshot tampered = new(
            candidate.BatchCommitId,
            candidate.OutcomeFingerprint,
            candidate.PlannedOutputFingerprint,
            candidate.TotalQuantity,
            candidate.TotalMassGrams,
            new[]
            {
                new FacilityBufferPlannedOutputRestoreStackSnapshot(
                    stack.BatchCommitId,
                    stack.OutcomeFingerprint,
                    stack.PlannedOutputFingerprint,
                    stack.OutputLineId,
                    stack.StackOrdinal,
                    stack.StackId,
                    stack.ItemId,
                    stack.Quantity,
                    stack.MassGrams,
                    stack.ComponentSignature,
                    stack.State,
                    stack.Position,
                    stack.DestinationId,
                    InstanceId + ":tampered")
            });
        Require(
            !fixture.Publication.TryAcknowledgeRestoreCandidate(
                tampered,
                out FacilityBufferPlannedOutputPublicationFailureCode
                    tamperedFailure,
                out _)
            && tamperedFailure == FacilityBufferPlannedOutputPublicationFailureCode
                .ExistingPublicationConflict,
            "Unique restore candidate accepted a mismatched item-instance identity.");

        Require(
            fixture.Admission.TryCommitPlannedOutput(
                token,
                receipt,
                out _,
                out _,
                out _),
            "Unique fixture could not commit planned admission.");
        CombatEquipmentInstance exact = fixture.Repository.EquipmentInstances[
            InstanceId].Clone();
        fixture.Repository.EquipmentInstances[InstanceId].sourceStackId =
            "stack:tampered";
        Require(
            !fixture.Publication.TryAcknowledgePublishedBatch(
                receipt,
                out FacilityBufferPlannedOutputPublicationFailureCode
                    aggregateFailure,
                out _)
            && aggregateFailure == FacilityBufferPlannedOutputPublicationFailureCode
                .ExistingPublicationConflict,
            "Unique acknowledgement accepted equipment aggregate drift.");
        fixture.Repository.EquipmentInstances[InstanceId] = exact;
        Require(
            fixture.Publication.TryAcknowledgePublishedBatch(
                receipt,
                out _,
                out _)
            && fixture.Publication.TryAcknowledgePublishedBatch(
                receipt,
                out _,
                out _)
            && fixture.Repository.EquipmentInstances.Count == 1
            && fixture.Publication.CaptureEditorTestSnapshot().Stacks.Count == 1,
            "Unique acknowledgement was not idempotent or removed physical authority.");

        fixture.Publication.SetUniqueEquipmentWorldStateForEditorTest(
            InstanceId,
            WorldItemStackState.Stored,
            CombatEquipmentWorldState.Stored);
        IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot>
            acknowledgedCandidates = fixture.Publication
                .CaptureAcknowledgedRestoreBatchesForEditorTest();
        Require(
            acknowledgedCandidates.Count == 1
            && acknowledgedCandidates[0].Stacks.Count == 1
            && acknowledgedCandidates[0].Stacks[0].State
                == WorldItemStackState.Stored
            && acknowledgedCandidates[0].Stacks[0].MassGrams
                == receipt.Stacks[0].Mass.Value,
            "Acknowledged provenance became a second writer for mutable equipment world state.");

        Fixture rollbackFixture = new();
        FacilityBufferPlannedOutputToken rollbackToken =
            rollbackFixture.ReserveUnique(
                "batch:unique:restore-rollback",
                InstanceId + ":rollback");
        Require(
            rollbackFixture.Publication.TryPublishFullBatch(
                rollbackToken,
                out _,
                out _,
                out _),
            "Unique restore rollback fixture did not publish.");
        FacilityBufferPlannedOutputRestoreBatchSnapshot rollbackCandidate =
            rollbackFixture.Publication
                .CapturePendingRestoreBatchesForEditorTest().Single();
        Require(
            rollbackFixture.Publication.TryRollbackRestoreCandidate(
                rollbackCandidate,
                out _,
                out _)
            && rollbackFixture.Publication.CaptureEditorTestSnapshot().Stacks.Count
                == 0
            && rollbackFixture.Repository.EquipmentInstances.Count == 0,
            "Unique restore rollback did not remove stack and aggregate atomically.");
    }

    private static void VerifyGenericMaxStackOneAllocatesAndCommitsIdentity()
    {
        Fixture fixture = new();
        FacilityBufferPlannedOutputToken token =
            fixture.ReserveGenericMaxStackOne("batch:generic-unique:001");
        Require(
            fixture.Publication.TryPublishFullBatch(
                token,
                out FacilityBufferPlannedOutputPublicationReceipt publication,
                out _,
                out _)
            && publication.Stacks.Count == 1
            && ((ItemInstanceId)publication.Stacks[0].ItemInstanceId).IsValid,
            "Generic max-stack-one publication did not allocate a physical instance identity.");
        Require(
            fixture.Admission.TryCommitPlannedOutput(
                token,
                publication,
                out FacilityBufferPlannedOutputReceipt receipt,
                out _,
                out _)
            && receipt.CommittedMassGrams == 2_350L,
            "Generic max-stack-one publication identity was rejected by planned admission.");
    }

    internal sealed class Fixture
    {
        internal const string DestinationId = "production:qa:output-buffer";
        internal const string OwnerDomain = "production.generic";
        internal static readonly Vector2Int DropPosition = new(9, 4);

        internal Fixture(
            IFacilityBufferPlannedOutputPublicationFaultInjector fault = null)
        {
            FakeCatalog catalog = new();
            Mass = new FakeMassQuery();
            Repository = new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            Query = new WorldItemQueryService(
                catalog,
                Mass,
                Repository,
                EditorNullItemMarkerPresenter.Instance);
            FacilityBufferDestinationClaimRegistry claims = new();
            Require(
                claims.TryClaim(
                    new FacilityBufferDestinationClaim(
                        DestinationId,
                        DropPosition,
                        OwnerDomain,
                        DestinationId,
                        "building:qa:production",
                        FacilityBufferDestinationAnchorKind.LiveBuilding),
                    out _,
                    out _),
                "Publication fixture failed to claim its destination.");
            Admission = new FacilityBufferMassAdmissionService(
                claims,
                new EmptyOccupancy(),
                Mass);
            Require(
                Admission.TryReplaceOwnedProfiles(
                    OwnerDomain,
                    new[]
                    {
                        new FacilityBufferCapacityProfile(
                            DestinationId,
                            DropPosition,
                            OwnerDomain,
                            DestinationId,
                            "building:qa:production",
                            new PhysicalMassGrams(10_000L),
                            1L)
                    },
                    out _,
                    out _),
                "Publication fixture failed to publish capacity.");
            Publication = new FacilityBufferPlannedOutputPublicationService(
                Repository,
                catalog,
                Mass,
                Admission,
                fault);
        }

        internal WorldItemRepository Repository { get; }
        internal WorldItemQueryService Query { get; }
        internal FacilityBufferMassAdmissionService Admission { get; }
        internal FacilityBufferPlannedOutputPublicationService Publication { get; }
        internal IPhysicalItemMassQuery Mass { get; }

        internal FacilityBufferPlannedOutputToken Reserve(string batchCommitId)
        {
            FacilityBufferPlannedOutputRequest request = new(
                $"operation:{batchCommitId}",
                batchCommitId,
                $"outcome:{batchCommitId}",
                DestinationId,
                DropPosition,
                OwnerDomain,
                DestinationId,
                "building:qa:production",
                1L,
                new[]
                {
                    new FacilityBufferPlannedOutputSlice(
                        "line:a",
                        PhysicalItemMassSubject.ForDefinition((ItemDefinitionId)"item:qa:a"),
                        5),
                    new FacilityBufferPlannedOutputSlice(
                        "line:b",
                        PhysicalItemMassSubject.ForDefinition((ItemDefinitionId)"item:qa:b"),
                        2)
                });
            Require(
                Admission.TryReservePlannedOutput(
                    request,
                    out FacilityBufferPlannedOutputToken token,
                    out _,
                    out _),
                "Publication fixture failed to reserve planned mass.");
            return token;
        }

        internal FacilityBufferPlannedOutputToken ReserveUnique(
            string batchCommitId,
            string instanceId)
        {
            string itemId = PhysicalItemIds.ForEquipment("weapon:dagger");
            CombatEquipmentInstance prepared = new()
            {
                instanceId = instanceId,
                definitionId = "weapon:dagger",
                materialId = "material:iron",
                quality = CombatEquipmentQuality.Normal,
                durabilityRatio = 1f,
                worldState = CombatEquipmentWorldState.Loose,
                ownerCharacterId = string.Empty,
                sourceStackId = string.Empty,
                evolution = new EquipmentEvolutionState(),
                moduleSlots = new List<EquipmentModuleSlotState>()
            };
            ItemInstanceComponentSaveData component =
                EquipmentItemStateCodec.Encode(prepared);
            PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter
                .Create(
                    Mass,
                    (ItemDefinitionId)itemId,
                    instanceId,
                    new[] { component });
            FacilityBufferPlannedOutputRequest request = new(
                $"operation:{batchCommitId}",
                batchCommitId,
                $"outcome:{batchCommitId}",
                DestinationId,
                DropPosition,
                OwnerDomain,
                DestinationId,
                "building:qa:production",
                1L,
                new[]
                {
                    new FacilityBufferPlannedOutputSlice(
                        "line:equipment",
                        subject,
                        1,
                        new[] { component })
                });
            bool reserved = Admission.TryReservePlannedOutput(
                request,
                out FacilityBufferPlannedOutputToken token,
                out FacilityBufferMassAdmissionFailureCode failure,
                out string failureReason);
            Require(
                reserved,
                "Unique publication fixture failed to reserve planned mass: "
                + failure + "/" + failureReason);
            return token;
        }

        internal FacilityBufferPlannedOutputToken ReserveGenericMaxStackOne(
            string batchCommitId)
        {
            FacilityBufferPlannedOutputRequest request = new(
                $"operation:{batchCommitId}",
                batchCommitId,
                $"outcome:{batchCommitId}",
                DestinationId,
                DropPosition,
                OwnerDomain,
                DestinationId,
                "building:qa:production",
                1L,
                new[]
                {
                    new FacilityBufferPlannedOutputSlice(
                        "line:generic-unique",
                        PhysicalItemMassSubject.ForDefinition(
                            (ItemDefinitionId)"item:qa:generic-unique"),
                        1)
                });
            Require(
                Admission.TryReservePlannedOutput(
                    request,
                    out FacilityBufferPlannedOutputToken token,
                    out _,
                    out _),
                "Generic max-stack-one fixture failed to reserve planned mass.");
            return token;
        }
    }

    internal sealed class FakeCatalog : IDungeonItemCatalogProvider
    {
        private readonly Dictionary<string, DungeonItemDefinition> definitions = new(
            StringComparer.Ordinal)
        {
            ["item:qa:a"] = new DungeonItemDefinition(
                "item:qa:a", "A", string.Empty, StockCategory.General,
                1, null, 1f, 2),
            ["item:qa:b"] = new DungeonItemDefinition(
                "item:qa:b", "B", string.Empty, StockCategory.General,
                1, null, 0.5f, 3),
            ["item:qa:generic-unique"] = new DungeonItemDefinition(
                "item:qa:generic-unique", "Generic Unique", string.Empty,
                StockCategory.General, 1, null, 2.35f, 1),
            ["equipment-item:weapon:dagger"] = new DungeonItemDefinition(
                "equipment-item:weapon:dagger", "Dagger", string.Empty,
                StockCategory.Weapon, 1, null, 1.5f, 1)
        };

        public IReadOnlyList<DungeonItemDefinition> All => definitions.Values.ToArray();
        public DungeonItemDefinition GetDefinition(string itemId) => definitions[itemId];
        public bool TryGetDefinition(string itemId, out DungeonItemDefinition definition) =>
            definitions.TryGetValue(itemId ?? string.Empty, out definition);
    }

    internal sealed class FakeMassQuery : IPhysicalItemMassQuery
    {
        public long AuthorityRevision => 1L;
        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId) =>
            new(string.Equals(
                    itemId.Value,
                    "equipment-item:weapon:dagger",
                    StringComparison.Ordinal)
                ? 1_500L
                : string.Equals(itemId.Value, "item:qa:a", StringComparison.Ordinal)
                    ? 1_000L
                    : string.Equals(
                        itemId.Value,
                        "item:qa:generic-unique",
                        StringComparison.Ordinal)
                        ? 2_350L
                        : 500L);
        public PhysicalMassGrams GetPreparedStackUnitMass(PhysicalItemMassSubject subject) =>
            GetDefinitionUnitMass(subject.ItemId);
        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) => GetDefinitionUnitMass(itemId);
        public PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot) =>
            GetQuantityMass(lot.Subject.ItemId, lot.Subject, lot.Quantity);
        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => GetDefinitionUnitMass(itemId).Multiply(quantity);
    }

    internal sealed class EmptyOccupancy : IFacilityBufferPhysicalOccupancyQuery
    {
        public FacilityBufferPhysicalOccupancySnapshot Capture(string destinationId) =>
            new(0L, 0L);
        public bool TryCaptureExactLot(
            IReadOnlyList<FacilityBufferMassLotSlice> slices,
            out FacilityBufferExactLotSnapshot lot,
            out string failureReason)
        {
            lot = default;
            failureReason = "not-used";
            return false;
        }
    }

    private sealed class FailAtStackIndex :
        IFacilityBufferPlannedOutputPublicationFaultInjector
    {
        private readonly int index;
        internal FailAtStackIndex(int index) => this.index = index;
        public bool FailBeforeRepositoryAdd(int zeroBasedStackIndex) =>
            zeroBasedStackIndex == index;
    }

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
}
#endif
