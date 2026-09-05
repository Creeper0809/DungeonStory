using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionExactCapabilityOutputRestoreJoinDebugScenarios
{
    private const string ItemId = "item:qa-exact-capability";
    private const string CapabilityId =
        "production-output:qa-exact-capability";
    private const int CapabilityVersion = 3;
    private const string CodecId =
        "production-output-codec:qa-exact-capability";
    private const int CodecVersion = 2;
    private const string FacilityId = "building-instance:qa-exact-capability";
    private const string DestinationId =
        "production-output:building-instance:qa-exact-capability";
    private const long UnitMassGrams = 200L;
    private const long ExactMassGrams = 120L;
    private const long RequiredCapacityGrams = 400L;
    private static readonly string CapacityDigest = Digest('c');

    [MenuItem(
        "DungeonStory/Debug/Economy/Run V21 Exact Capability Restore Join Contracts")]
    public static void RunAll()
    {
        VerifyCrashAPendingAccepted();
        VerifyCrashBAcknowledgedEnvelopeAccepted();
        VerifyBothAndMissingPhysicalStatesRejected();
        VerifyPhysicalTamperRejected();
        VerifyProofAndCapacityTamperRejected();
        VerifyDuplicateOwnerCommitRejected();
        VerifyPendingPhysicalOrphanRejected();
        VerifyLateRowFailureIsReadOnly();
        Debug.Log(
            "Production V21 exact-capability restore join scenarios passed.");
    }

    private static void VerifyCrashAPendingAccepted()
    {
        Harness harness = new();
        OwnerCase owner = harness.CreateOwner(1, applied: false);

        harness.CreateJoin(
                new[] { owner.Physical },
                Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>())
            .Validate(Payload(owner));

        RequireNoExecution(harness.Handler,
            "Crash A validation executed the output capability.");
    }

    private static void VerifyCrashBAcknowledgedEnvelopeAccepted()
    {
        Harness harness = new();
        OwnerCase owner = harness.CreateOwner(2, applied: true);

        harness.CreateJoin(
                Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>(),
                new[] { owner.Physical })
            .Validate(Payload(owner));

        RequireNoExecution(harness.Handler,
            "Crash B validation executed or acknowledged the output capability.");
    }

    private static void VerifyBothAndMissingPhysicalStatesRejected()
    {
        Harness bothHarness = new();
        OwnerCase both = bothHarness.CreateOwner(3, applied: true);
        RequireThrows(
            () => bothHarness.CreateJoin(
                    new[] { both.Physical },
                    new[] { both.Physical })
                .Validate(Payload(both)),
            "exactly one physical lifecycle state");

        Harness missingHarness = new();
        OwnerCase missing = missingHarness.CreateOwner(4, applied: false);
        RequireThrows(
            () => missingHarness.CreateJoin(
                    Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>(),
                    Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>())
                .Validate(Payload(missing)),
            "exactly one physical lifecycle state");
    }

    private static void VerifyPhysicalTamperRejected()
    {
        Harness stackHarness = new();
        OwnerCase stack = stackHarness.CreateOwner(5, applied: true);
        FacilityBufferPlannedOutputRestoreBatchSnapshot stackTamper =
            stackHarness.Physical(stack, stackId: "world-item-stack:tampered");
        RequireThrows(
            () => stackHarness.CreateJoin(
                    Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>(),
                    new[] { stackTamper })
                .Validate(Payload(stack)),
            "does not match physical publication");

        Harness massHarness = new();
        OwnerCase mass = massHarness.CreateOwner(6, applied: true);
        FacilityBufferPlannedOutputRestoreBatchSnapshot massTamper =
            massHarness.Physical(
                mass,
                totalMassGrams: ExactMassGrams + 1L);
        RequireThrows(
            () => massHarness.CreateJoin(
                    Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>(),
                    new[] { massTamper })
                .Validate(Payload(mass)),
            "does not match physical publication");

        Harness destinationHarness = new();
        OwnerCase destination = destinationHarness.CreateOwner(
            7,
            applied: false);
        FacilityBufferPlannedOutputRestoreBatchSnapshot destinationTamper =
            destinationHarness.Physical(
                destination,
                destinationId: "production-output:tampered-destination");
        RequireThrows(
            () => destinationHarness.CreateJoin(
                    new[] { destinationTamper },
                    Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>())
                .Validate(Payload(destination)),
            "physical publication is inconsistent");

        Harness componentHarness = new();
        OwnerCase component = componentHarness.CreateOwner(8, applied: true);
        FacilityBufferPlannedOutputRestoreBatchSnapshot componentTamper =
            componentHarness.Physical(
                component,
                componentSignature: "component-signature:tampered");
        RequireThrows(
            () => componentHarness.CreateJoin(
                    Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>(),
                    new[] { componentTamper })
                .Validate(Payload(component)),
            "does not match physical publication");
    }

    private static void VerifyProofAndCapacityTamperRejected()
    {
        Harness proofHarness = new();
        OwnerCase proof = proofHarness.CreateOwner(9, applied: true);
        proof.Output.pendingOutputPublication.maximumProofDigest = Digest('f');
        RequireThrows(
            () => proofHarness.CreateJoin(
                    Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>(),
                    new[] { proof.Physical })
                .Validate(Payload(proof)),
            "maximum-mass proof drifted");

        Harness capacityHarness = new();
        OwnerCase capacity = capacityHarness.CreateOwner(10, applied: true);
        capacity.Output.pendingOutputPublication.capacitySourceDigest =
            Digest('e');
        RequireThrows(
            () => capacityHarness.CreateJoin(
                    Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>(),
                    new[] { capacity.Physical })
                .Validate(Payload(capacity)),
            "capacity source drifted");
    }

    private static void VerifyDuplicateOwnerCommitRejected()
    {
        Harness harness = new();
        OwnerCase first = harness.CreateOwner(11, applied: false);
        OwnerCase duplicate = harness.CreateOwner(12, applied: false);
        duplicate.Output.pendingCommitId = first.Output.pendingCommitId;

        RequireThrows(
            () => harness.CreateJoin(
                    new[] { first.Physical },
                    Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>())
                .Validate(Payload(first, duplicate)),
            "Duplicate or invalid exact-output restore owner");
    }

    private static void VerifyPendingPhysicalOrphanRejected()
    {
        Harness harness = new();
        OwnerCase orphan = harness.CreateOwner(13, applied: false);
        DungeonProductionBillSaveData noOwners = new()
        {
            version = DungeonProductionBillSaveData.CurrentVersion,
            bills = new List<ProductionBillSaveData>()
        };

        RequireThrows(
            () => harness.CreateJoin(
                    new[] { orphan.Physical },
                    Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>())
                .Validate(noOwners),
            "Orphan pending exact-output physical batch");
    }

    private static void VerifyLateRowFailureIsReadOnly()
    {
        Harness harness = new();
        OwnerCase first = harness.CreateOwner(14, applied: true);
        OwnerCase late = harness.CreateOwner(15, applied: true);
        late.Output.pendingOutputPublication.maximumMassGrams++;
        DungeonProductionBillSaveData payload = Payload(first, late);
        string before = JsonUtility.ToJson(payload);

        RequireThrows(
            () => harness.CreateJoin(
                    Array.Empty<FacilityBufferPlannedOutputRestoreBatchSnapshot>(),
                    new[] { first.Physical, late.Physical })
                .Validate(payload),
            "maximum-mass proof drifted");

        Require(string.Equals(
                before,
                JsonUtility.ToJson(payload),
                StringComparison.Ordinal),
            "A late-row failure mutated the detached Production payload.");
        RequireNoExecution(harness.Handler,
            "A late-row failure executed, captured, or acknowledged output.");
    }

    private static DungeonProductionBillSaveData Payload(
        params OwnerCase[] owners) => new()
    {
        version = DungeonProductionBillSaveData.CurrentVersion,
        nextBillSequence = owners.Length + 1,
        bills = owners
            .Select(value => value.Bill)
            .OrderBy(value => value.billId, StringComparer.Ordinal)
            .ToList()
    };

    private static void RequireNoExecution(
        SyntheticExactOutputHandler handler,
        string message)
    {
        Require(handler.LegacyProduceCount == 0
                && handler.IdempotentProduceCount == 0
                && handler.AcknowledgeCount == 0
                && handler.CaptureCount == 0,
            message);
    }

    private static void RequireThrows(Action action, string token)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            Require(exception.Message.Contains(token, StringComparison.Ordinal),
                "Restore join failed with the wrong reason. Expected token '"
                + token
                + "', actual: "
                + exception.Message);
            return;
        }

        throw new InvalidOperationException(
            "Expected restore-join rejection was not observed: " + token);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static string Digest(char value) => new(value, 64);

    private sealed class Harness
    {
        private readonly ProductionOutputHandlerRegistry capabilities;
        private readonly ProductionOutputMaximumMassRegistry maximumMass;

        internal Harness()
        {
            Handler = new SyntheticExactOutputHandler();
            capabilities = new ProductionOutputHandlerRegistry(new
                IProductionOutputCapability[]
                {
                    new SyntheticStandardOutputCapability(),
                    Handler
                });
            maximumMass = new ProductionOutputMaximumMassRegistry(new
                IProductionOutputMaximumMassCapability[]
                {
                    new SyntheticStandardMaximumMassCapability(),
                    new SyntheticExactMaximumMassCapability()
                },
                new FixedMassQuery());
        }

        internal SyntheticExactOutputHandler Handler { get; }

        internal OwnerCase CreateOwner(int ordinal, bool applied)
        {
            string suffix = ordinal.ToString("D2");
            string billId = "production-bill:qa-exact-" + suffix;
            string outputLineId = "output:qa-exact:" + suffix;
            ProductionOutputCapabilityDescriptor descriptor =
                capabilities.CaptureDeclaredDescriptor(
                    outputLineId,
                    ItemId,
                    CapabilityId);
            ProductionOutputBatchMaximumMassProof proof = new(new[]
            {
                maximumMass.CaptureDeclared(descriptor, 1)
            });
            string commitId = ProductionOutputCommitIdentity.Format(
                (ProductionBillId)billId,
                1,
                outputLineId,
                ItemId,
                0);
            string outcomeFingerprint = Digest(
                (char)('a' + ordinal % 5));
            string plannedOutputFingerprint = Digest(
                (char)('f' + ordinal % 5));
            string stackId = "world-item-stack:qa-exact-" + suffix;
            string componentSignature =
                "component-signature:qa-exact-" + suffix;
            ProductionResolvedOutputSaveData output = new()
            {
                outputLineId = outputLineId,
                itemId = ItemId,
                outputCapabilityId = descriptor.CapabilityId,
                outputCapabilityVersion = descriptor.CapabilityVersion,
                outputComponentCodecId = descriptor.ComponentCodecId,
                outputComponentCodecVersion =
                    descriptor.ComponentCodecVersion,
                outputCapabilityFingerprint = descriptor.Fingerprint,
                amount = 1,
                committedAmount = applied ? 1 : 0,
                committedMassGrams = applied ? ExactMassGrams : 0L,
                pendingCommitId = commitId,
                pendingCommitApplied = applied,
                pendingOutputPublication = applied
                    ? new ProductionExactOutputPublicationSaveData
                    {
                        phase = ProductionExactOutputPublicationPhase.Published,
                        ownerStableId = billId,
                        commitId = commitId,
                        facilityInstanceId = FacilityId,
                        outputCapabilityId = descriptor.CapabilityId,
                        outputCapabilityVersion = descriptor.CapabilityVersion,
                        outputComponentCodecId = descriptor.ComponentCodecId,
                        outputComponentCodecVersion =
                            descriptor.ComponentCodecVersion,
                        maximumProofDigest = proof.SourceDigest,
                        maximumMassGrams = proof.MaximumBatchMassGrams,
                        capacitySourceDigest = CapacityDigest,
                        requiredMinimumCapacityGrams =
                            RequiredCapacityGrams,
                        exactMassGrams = ExactMassGrams,
                        outcomeFingerprint = outcomeFingerprint,
                        plannedOutputFingerprint = plannedOutputFingerprint,
                        destinationId = DestinationId,
                        dropPositionX = 4,
                        dropPositionY = 5,
                        ownerDomain = "production-output-buffer",
                        ownerOperationId =
                            "production-output-owner:" + billId,
                        ownerFacilityId = FacilityId,
                        capacityRevision = 7L,
                        acknowledgedAtCapture = true,
                        stacks = new List<
                            ProductionExactOutputPublicationStackSaveData>
                        {
                            new()
                            {
                                outputLineId = outputLineId,
                                stackOrdinal = 0,
                                stackId = stackId,
                                itemId = ItemId,
                                quantity = 1,
                                massGrams = ExactMassGrams,
                                componentSignature = componentSignature,
                                itemInstanceId = string.Empty
                            }
                        }
                    }
                    : ProductionExactOutputPublicationSaveData.Empty()
            };
            ProductionBillSaveData bill = new()
            {
                billId = billId,
                recipeId = "recipe:qa-exact-capability",
                buildingInstanceId = FacilityId,
                cycleSequence = 1,
                outputDestinationId = DestinationId,
                resolvedOutputs = new List<ProductionResolvedOutputSaveData>
                {
                    output
                }
            };
            OwnerCase owner = new(bill, output, proof);
            owner.Physical = Physical(owner);
            return owner;
        }

        internal FacilityBufferPlannedOutputRestoreBatchSnapshot Physical(
            OwnerCase owner,
            string stackId = null,
            long? totalMassGrams = null,
            long? stackMassGrams = null,
            string destinationId = null,
            string componentSignature = null)
        {
            ProductionExactOutputPublicationSaveData envelope =
                owner.Output.pendingOutputPublication;
            ProductionExactOutputPublicationStackSaveData expected =
                envelope.phase == ProductionExactOutputPublicationPhase.Published
                    ? envelope.stacks.Single()
                    : null;
            string resolvedStackId = stackId
                ?? expected?.stackId
                ?? "world-item-stack:qa-exact-pending";
            string resolvedComponent = componentSignature
                ?? expected?.componentSignature
                ?? "component-signature:qa-exact-pending";
            long resolvedStackMass = stackMassGrams ?? ExactMassGrams;
            return new FacilityBufferPlannedOutputRestoreBatchSnapshot(
                owner.Output.pendingCommitId,
                expected == null
                    ? Digest('a')
                    : envelope.outcomeFingerprint,
                expected == null
                    ? Digest('b')
                    : envelope.plannedOutputFingerprint,
                1,
                totalMassGrams ?? ExactMassGrams,
                new[]
                {
                    new FacilityBufferPlannedOutputRestoreStackSnapshot(
                        owner.Output.pendingCommitId,
                        expected == null
                            ? Digest('a')
                            : envelope.outcomeFingerprint,
                        expected == null
                            ? Digest('b')
                            : envelope.plannedOutputFingerprint,
                        owner.Output.outputLineId,
                        0,
                        resolvedStackId,
                        owner.Output.itemId,
                        1,
                        resolvedStackMass,
                        resolvedComponent,
                        WorldItemStackState.FacilityOutputBuffer,
                        new Vector2Int(4, 5),
                        destinationId ?? DestinationId)
                });
        }

        internal ProductionExactCapabilityOutputRestoreJoin CreateJoin(
            IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot>
                pending,
            IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot>
                acknowledged) => new(
            new FixedBatchQuery(pending),
            new FixedBatchQuery(acknowledged),
            capabilities,
            maximumMass,
            new FixedDetachedCapacityGuard());
    }

    private sealed class OwnerCase
    {
        internal OwnerCase(
            ProductionBillSaveData bill,
            ProductionResolvedOutputSaveData output,
            ProductionOutputBatchMaximumMassProof proof)
        {
            Bill = bill;
            Output = output;
            Proof = proof;
        }

        internal ProductionBillSaveData Bill { get; }
        internal ProductionResolvedOutputSaveData Output { get; }
        internal ProductionOutputBatchMaximumMassProof Proof { get; }
        internal FacilityBufferPlannedOutputRestoreBatchSnapshot Physical
        {
            get;
            set;
        }
    }

    private sealed class FixedBatchQuery :
        IFacilityBufferPlannedOutputRestoreCandidateQuery,
        IFacilityBufferAcknowledgedOutputRestoreCandidateQuery
    {
        private readonly IReadOnlyList<
            FacilityBufferPlannedOutputRestoreBatchSnapshot> batches;
        private readonly IReadOnlyDictionary<string,
            FacilityBufferPlannedOutputRestoreBatchSnapshot> byCommitId;

        internal FixedBatchQuery(
            IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot>
                batches)
        {
            FacilityBufferPlannedOutputRestoreBatchSnapshot[] frozen =
                (batches
                    ?? Array.Empty<
                        FacilityBufferPlannedOutputRestoreBatchSnapshot>())
                .ToArray();
            this.batches = Array.AsReadOnly(frozen);
            byCommitId = frozen
                .Where(value => value != null)
                .ToDictionary(
                    value => value.BatchCommitId,
                    value => value,
                    StringComparer.Ordinal);
        }

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot>
            Batches => batches;

        public bool TryGetBatch(
            string batchCommitId,
            out FacilityBufferPlannedOutputRestoreBatchSnapshot batch) =>
            byCommitId.TryGetValue(batchCommitId ?? string.Empty, out batch);
    }

    private sealed class FixedDetachedCapacityGuard :
        IProductionOutputDetachedFacilityCapacityRestoreGuard
    {
        public ProductionOutputBufferCapacitySourceSnapshot Validate(
            string ownerStableId,
            string facilityInstanceId,
            ProductionOutputBatchMaximumMassProof maximumMassProof,
            string savedCapacitySourceDigest,
            long savedRequiredMinimumCapacityGrams)
        {
            if (string.IsNullOrEmpty(ownerStableId)
                || !string.Equals(
                    facilityInstanceId,
                    FacilityId,
                    StringComparison.Ordinal)
                || maximumMassProof == null
                || maximumMassProof.MaximumBatchMassGrams != UnitMassGrams
                || !string.Equals(
                    savedCapacitySourceDigest,
                    CapacityDigest,
                    StringComparison.Ordinal)
                || savedRequiredMinimumCapacityGrams !=
                    RequiredCapacityGrams)
            {
                throw new InvalidOperationException(
                    "Synthetic detached capacity source drifted.");
            }

            return new ProductionOutputBufferCapacitySourceSnapshot(
                cycleCapacity: 2,
                maximumBatchMassGrams:
                    maximumMassProof.MaximumBatchMassGrams,
                projectedPortfolioCapacityGrams:
                    savedRequiredMinimumCapacityGrams,
                batchMinimumCapacityGrams: 0L,
                requiredMinimumCapacityGrams:
                    savedRequiredMinimumCapacityGrams,
                sourceDigest: savedCapacitySourceDigest);
        }
    }

    private sealed class SyntheticExactOutputHandler :
        IProductionOutputHandler,
        IIdempotentProductionOutputHandler
    {
        public string CapabilityId =>
            ProductionExactCapabilityOutputRestoreJoinDebugScenarios
                .CapabilityId;
        public int ContractVersion => CapabilityVersion;
        public string ComponentCodecId => CodecId;
        public int ComponentCodecVersion => CodecVersion;
        public bool SupportsAutomaticSelection => false;
        public int LegacyProduceCount { get; private set; }
        public int IdempotentProduceCount { get; private set; }
        public int AcknowledgeCount { get; private set; }
        public int CaptureCount { get; private set; }

        public bool CanHandle(string itemId) =>
            !string.IsNullOrEmpty(itemId)
            && string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal);

        public bool TryProduce(
            ProductionOutputContext context,
            out string failureReason)
        {
            LegacyProduceCount++;
            failureReason = "synthetic-handler-must-not-execute";
            return false;
        }

        public bool TryProduceIdempotent(
            ProductionOutputContext context,
            out DomainFailure failure)
        {
            IdempotentProduceCount++;
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                context.ItemId,
                "synthetic-handler-must-not-execute");
            return false;
        }

        public bool TryAcknowledge(
            string commitId,
            out DomainFailure failure)
        {
            AcknowledgeCount++;
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                commitId,
                "synthetic-handler-must-not-acknowledge");
            return false;
        }

        public bool TryCaptureCommittedOutput(
            ProductionOutputContext context,
            out ProductionCommittedOutputSnapshot snapshot,
            out DomainFailure failure)
        {
            CaptureCount++;
            snapshot = null;
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                context.CommitId,
                "synthetic-handler-must-not-capture");
            return false;
        }
    }

    private sealed class SyntheticStandardOutputCapability :
        IProductionOutputCapability
    {
        public string CapabilityId =>
            ProductionOutputCapabilityIds.StandardDefinition;
        public int ContractVersion =>
            ProductionOutputCapabilityIds.StandardDefinitionVersion;
        public string ComponentCodecId =>
            ProductionOutputCapabilityIds.DefinitionOnlyCodec;
        public int ComponentCodecVersion =>
            ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion;
        public bool SupportsAutomaticSelection => true;
        public bool CanHandle(string itemId) => false;
    }

    private sealed class SyntheticExactMaximumMassCapability :
        IProductionOutputMaximumMassCapability
    {
        public string CapabilityId =>
            ProductionExactCapabilityOutputRestoreJoinDebugScenarios
                .CapabilityId;
        public int ContractVersion => CapabilityVersion;
        public string ComponentCodecId => CodecId;
        public int ComponentCodecVersion => CodecVersion;
        public bool SupportsAutomaticSelection => false;
        public bool CanHandle(string itemId) =>
            !string.IsNullOrEmpty(itemId)
            && string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal);

        public ProductionOutputMaximumMassProjection CaptureDefinitionMaximum(
            ProductionOutputCapabilityDescriptor descriptor,
            int maximumQuantity,
            IPhysicalItemMassQuery massQuery) =>
            ProductionOutputDefinitionMaximumMassProjection.Capture(
                this,
                descriptor,
                maximumQuantity,
                massQuery);
    }

    private sealed class SyntheticStandardMaximumMassCapability :
        IProductionOutputMaximumMassCapability
    {
        public string CapabilityId =>
            ProductionOutputCapabilityIds.StandardDefinition;
        public int ContractVersion =>
            ProductionOutputCapabilityIds.StandardDefinitionVersion;
        public string ComponentCodecId =>
            ProductionOutputCapabilityIds.DefinitionOnlyCodec;
        public int ComponentCodecVersion =>
            ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion;
        public bool SupportsAutomaticSelection => true;
        public bool CanHandle(string itemId) => false;

        public ProductionOutputMaximumMassProjection CaptureDefinitionMaximum(
            ProductionOutputCapabilityDescriptor descriptor,
            int maximumQuantity,
            IPhysicalItemMassQuery massQuery) =>
            ProductionOutputDefinitionMaximumMassProjection.Capture(
                this,
                descriptor,
                maximumQuantity,
                massQuery);
    }

    private sealed class FixedMassQuery : IPhysicalItemMassQuery
    {
        public long AuthorityRevision => 23L;

        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId)
            => new(UnitMassGrams);

        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) => new(UnitMassGrams);

        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) => new(UnitMassGrams);

        public PhysicalMassGrams GetStackTotalMass(
            PhysicalItemLotSnapshot lot) =>
            new PhysicalMassGrams(UnitMassGrams).Multiply(lot.Quantity);

        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => new PhysicalMassGrams(UnitMassGrams)
            .Multiply(quantity);
    }
}
