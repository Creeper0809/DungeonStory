using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;

public static class ProductionDomainOutputRestoreGuardDebugScenarios
{
    private const string Domain = "economy.qa-domain-output";
    private const string Prefix = "domain-output-batch:qa:";
    private const string Batch = Prefix + "owner:1";

    public static string VerifyAll()
    {
        VerifyJoinAdoptsAndAcknowledges();
        VerifyCertifiedSaveSectionAdoptsBeforePublication();
        VerifyCertifiedAcknowledgementFailurePreservesRuntime();
        VerifyExactBidirectionalJoin();
        VerifySectionAdoptedOwnerRemainsVisibleToGuard();
        VerifyOwnerWithoutBatchFails();
        VerifyOrphanBatchFails();
        VerifyMassDriftFails();
        VerifyMaximumMassProofDriftFails();
        VerifyGenericProductionBatchIsOutsideDomainRegistry();
        return "PRODUCTION_DOMAIN_OUTPUT_RESTORE_GUARD_PASS";
    }

    private static void VerifyCertifiedSaveSectionAdoptsBeforePublication()
    {
        ProductionDomainOutputPublicationSaveData owner = Owner();
        CertifiedSeedWorldSaveData payload = new()
        {
            nextOrderSequence = 2,
            orders = new List<CertifiedSeedOrderSaveData>
            {
                new()
                {
                    orderId = "certified-seed-order:00000001",
                    orderSequence = 1,
                    facilityInstanceId = owner.ownerFacilityId,
                    phase = CertifiedSeedOrderPhase.OutputPublished,
                    outputCapability =
                        ProductionOutputCapabilitySaveData.Freeze(Capability()),
                    outputPublication = owner
                }
            }
        };
        RecordingPublication publication = new();
        FixedCertifiedPersistence persistence = new();
        CertifiedSeedSaveSection section = new(
            persistence,
            new EmptyCertifiedInputDescriptorSource(),
            new AcceptingCertifiedInputOwners(),
            new ProductionDomainOutputRestoreJoin(
                new FixedQuery(Incoming(owner)),
                publication),
            Registry(),
            new AcceptingDetachedCapacityGuard());
        DungeonGameRestoreReport report = new();
        section.Restore(
            JsonUtility.ToJson(payload),
            CertifiedSeedWorldSaveData.CurrentVersion,
            report);
        CertifiedSeedOrderSaveData restored = persistence.RestoredOrders
            .SingleOrDefault();
        if (!report.Success
            || restored == null
            || restored.phase != CertifiedSeedOrderPhase
                .OutputRestoredAwaitingInputAcknowledgement
            || !restored.outputPublication.outputAcknowledged
            || publication.AcknowledgementCount != 1)
        {
            throw new InvalidOperationException(
                "Certified-seed save restore did not atomically adopt its pending output.");
        }
    }

    private static void VerifyCertifiedAcknowledgementFailurePreservesRuntime()
    {
        ProductionDomainOutputPublicationSaveData owner = Owner();
        CertifiedSeedWorldSaveData payload = new()
        {
            nextOrderSequence = 2,
            orders = new List<CertifiedSeedOrderSaveData>
            {
                new()
                {
                    orderId = "certified-seed-order:00000001",
                    orderSequence = 1,
                    facilityInstanceId = owner.ownerFacilityId,
                    phase = CertifiedSeedOrderPhase.OutputPublished,
                    outputCapability =
                        ProductionOutputCapabilitySaveData.Freeze(Capability()),
                    outputPublication = owner
                }
            }
        };
        FixedCertifiedPersistence persistence = new();
        RecordingPublication publication = new() { RejectAcknowledgement = true };
        CertifiedSeedSaveSection section = new(
            persistence,
            new EmptyCertifiedInputDescriptorSource(),
            new AcceptingCertifiedInputOwners(),
            new ProductionDomainOutputRestoreJoin(
                new FixedQuery(Incoming(owner)),
                publication),
            Registry(),
            new AcceptingDetachedCapacityGuard());
        DungeonGameRestoreReport report = new();
        bool rejected = false;
        try
        {
            section.Restore(
                JsonUtility.ToJson(payload),
                CertifiedSeedWorldSaveData.CurrentVersion,
                report);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        if (!rejected || persistence.RestoreCount != 0)
        {
            throw new InvalidOperationException(
                "Certified-seed restore mutated runtime before physical acknowledgement succeeded.");
        }
    }

    private static void VerifyJoinAdoptsAndAcknowledges()
    {
        ProductionDomainOutputPublicationSaveData owner = Owner();
        FacilityBufferPlannedOutputRestoreBatchSnapshot incoming =
            Incoming(owner);
        RecordingPublication publication = new();
        ProductionDomainOutputRestoreJoin join = new(
            new FixedQuery(incoming),
            publication);
        ProductionDomainOutputRestoreAcknowledgement adopted =
            join.AdoptPending(owner);
        join.Acknowledge(new[] { adopted });
        if (publication.AcknowledgementCount != 1
            || !string.Equals(
                publication.LastBatchCommitId,
                owner.batchCommitId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "The common domain restore join did not acknowledge exactly once.");
        }

        owner.outputAcknowledged = true;
        new ProductionDomainOutputRestoreJoin(
                new FixedQuery(),
                publication)
            .RequireNoPending(owner);
        RequireThrows(
            () => join.RequireNoPending(owner),
            "An acknowledged owner retained its incoming physical marker.");
    }

    private static void VerifyExactBidirectionalJoin()
    {
        ProductionDomainOutputPublicationSaveData owner = Owner();
        ProductionDomainOutputRestoreGuard.ValidateOwnerSet(
            new[] { new FixedOwnerSource(owner) },
            new FixedQuery(Incoming(owner)),
            Registry());
    }

    private static void VerifySectionAdoptedOwnerRemainsVisibleToGuard()
    {
        ProductionDomainOutputPublicationSaveData owner = Owner();
        owner.outputAcknowledged = true;
        owner.restoredInCurrentTransaction = true;
        ProductionDomainOutputRestoreGuard.ValidateOwnerSet(
            new[] { new FixedOwnerSource(owner) },
            new FixedQuery(Incoming(owner)),
            Registry());
    }

    private static void VerifyOwnerWithoutBatchFails()
    {
        RequireThrows(() => ProductionDomainOutputRestoreGuard.ValidateOwnerSet(
                new[] { new FixedOwnerSource(Owner()) },
                new FixedQuery(),
                Registry()),
            "A domain output owner without its physical batch was accepted.");
    }

    private static void VerifyOrphanBatchFails()
    {
        ProductionDomainOutputPublicationSaveData owner = Owner();
        RequireThrows(() => ProductionDomainOutputRestoreGuard.ValidateOwnerSet(
                Array.Empty<IProductionDomainOutputRestoreOwnerSource>(),
                new FixedQuery(Incoming(owner)),
                Registry()),
            "An orphan domain output physical batch was accepted.");
    }

    private static void VerifyMassDriftFails()
    {
        ProductionDomainOutputPublicationSaveData owner = Owner();
        FacilityBufferPlannedOutputRestoreBatchSnapshot incoming = new(
            owner.batchCommitId,
            owner.outcomeFingerprint,
            owner.plannedOutputFingerprint,
            1,
            owner.outputMassGrams + 1L,
            new[]
            {
                new FacilityBufferPlannedOutputRestoreStackSnapshot(
                    owner.batchCommitId,
                    owner.outcomeFingerprint,
                    owner.plannedOutputFingerprint,
                    owner.stacks[0].outputLineId,
                    0,
                    owner.stacks[0].stackId,
                    owner.stacks[0].itemId,
                    1,
                    owner.outputMassGrams + 1L,
                    "qa-component-signature",
                    WorldItemStackState.FacilityOutputBuffer,
                    new Vector2Int(owner.destinationX, owner.destinationY),
                    owner.destinationId)
            });
        RequireThrows(() => ProductionDomainOutputRestoreGuard.ValidateOwnerSet(
                new[] { new FixedOwnerSource(owner) },
                new FixedQuery(incoming),
                Registry()),
            "Domain output physical mass drift was accepted.");
    }

    private static void VerifyGenericProductionBatchIsOutsideDomainRegistry()
    {
        FacilityBufferPlannedOutputRestoreBatchSnapshot generic = new(
            "production-output-batch:qa:00000001:" + Digest('e'),
            Digest('a'),
            Digest('c'),
            1,
            50L,
            Array.Empty<FacilityBufferPlannedOutputRestoreStackSnapshot>());
        ProductionDomainOutputRestoreGuard.ValidateOwnerSet(
            Array.Empty<IProductionDomainOutputRestoreOwnerSource>(),
            new FixedQuery(generic),
            Registry());
    }

    private static void VerifyMaximumMassProofDriftFails()
    {
        ProductionDomainOutputPublicationSaveData digestDrift = Owner();
        digestDrift.maximumMassProofDigest = Digest('f');
        RequireThrows(() => ProductionDomainOutputRestoreGuard.ValidateOwnerSet(
                new[] { new FixedOwnerSource(digestDrift) },
                new FixedQuery(Incoming(digestDrift)),
                Registry()),
            "A drifted domain output maximum-mass proof digest was accepted.");

        ProductionDomainOutputPublicationSaveData authorityDrift = Owner();
        RequireThrows(() => ProductionDomainOutputRestoreGuard.ValidateOwnerSet(
                new[] { new FixedOwnerSource(authorityDrift) },
                new FixedQuery(Incoming(authorityDrift)),
                Registry(unitMassGrams: 51L)),
            "A stale domain output mass-authority projection was accepted.");
    }

    private static ProductionDomainOutputPublicationSaveData Owner()
    {
        ProductionOutputMaximumMassProjection projection = Registry()
            .CaptureDeclared(Capability(), 1);
        ProductionOutputBatchMaximumMassProof proof = new(new[] { projection });
        return new ProductionDomainOutputPublicationSaveData
        {
        schemaVersion =
            ProductionDomainOutputPublicationSaveData.CurrentSchemaVersion,
        publicationAttempt = 0,
        publicationOperationId =
            "domain-output-publication:qa:owner:1:0000",
        batchCommitId = Batch,
        outcomeFingerprint = Digest('a'),
        maximumMassProofDigest = proof.SourceDigest,
        maximumBatchMassGrams = proof.MaximumBatchMassGrams,
        capacitySourceDigest = Digest('b'),
        requiredMinimumCapacityGrams = 100L,
        outputMassGrams = 50L,
        admissionTokenId = "facility-buffer-planned-output:qa-token",
        plannedOutputFingerprint = Digest('c'),
        destinationId = "production-output:building-instance:qa",
        destinationX = 7,
        destinationY = 9,
        ownerDomain = "production-output-buffer",
        ownerOperationId = "production-output-owner:building-instance:qa",
        ownerFacilityId = "building-instance:qa",
        capacityRevision = 1L,
        outputPublished = true,
        admissionCommitted = true,
        stacks = new List<ProductionDomainPublishedStackSaveData>
        {
            new()
            {
                outputLineId = "output:qa",
                itemId = "resource:qa",
                stackId = "world-item-stack:qa-domain-output",
                quantity = 1,
                massGrams = 50L
            }
        }
        };
    }

    private static ProductionOutputCapabilityDescriptor Capability() => new(
        "output:qa",
        "resource:qa",
        ProductionOutputCapabilityIds.StandardDefinition,
        ProductionOutputCapabilityIds.StandardDefinitionVersion,
        ProductionOutputCapabilityIds.DefinitionOnlyCodec,
        ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion,
        ProductionOutputCapabilityDescriptorFingerprint.Capture(
            "output:qa",
            "resource:qa",
            ProductionOutputCapabilityIds.StandardDefinition,
            ProductionOutputCapabilityIds.StandardDefinitionVersion,
            ProductionOutputCapabilityIds.DefinitionOnlyCodec,
            ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion));

    private static IProductionOutputMaximumMassRegistry Registry(
        long unitMassGrams = 50L) =>
        new FixedMaximumMassRegistry(unitMassGrams);

    private static FacilityBufferPlannedOutputRestoreBatchSnapshot Incoming(
        ProductionDomainOutputPublicationSaveData owner) => new(
        owner.batchCommitId,
        owner.outcomeFingerprint,
        owner.plannedOutputFingerprint,
        1,
        owner.outputMassGrams,
        new[]
        {
            new FacilityBufferPlannedOutputRestoreStackSnapshot(
                owner.batchCommitId,
                owner.outcomeFingerprint,
                owner.plannedOutputFingerprint,
                owner.stacks[0].outputLineId,
                0,
                owner.stacks[0].stackId,
                owner.stacks[0].itemId,
                owner.stacks[0].quantity,
                owner.stacks[0].massGrams,
                "qa-component-signature",
                WorldItemStackState.FacilityOutputBuffer,
                new Vector2Int(owner.destinationX, owner.destinationY),
                owner.destinationId)
        });

    private static string Digest(char value) => new(value, 64);

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

    private sealed class FixedOwnerSource :
        IProductionDomainOutputRestoreOwnerSource
    {
        private readonly ProductionDomainOutputPublicationSaveData owner;

        internal FixedOwnerSource(
            ProductionDomainOutputPublicationSaveData owner) =>
            this.owner = owner;

        public string OutputOwnerDomainId => Domain;
        public string OutputBatchCommitPrefix => Prefix;

        public IReadOnlyList<ProductionDomainOutputRestoreOwnerSnapshot>
            CapturePendingOutputOwners() => new[]
            {
                new ProductionDomainOutputRestoreOwnerSnapshot(
                    "owner:1",
                    owner,
                    new[]
                    {
                        new ProductionDomainOutputMaximumMassClaim(
                            Capability(),
                            1)
                    })
            };
    }

    private sealed class FixedMaximumMassRegistry :
        IProductionOutputMaximumMassRegistry
    {
        private readonly long unitMassGrams;

        internal FixedMaximumMassRegistry(long unitMassGrams)
        {
            if (unitMassGrams <= 0L)
                throw new ArgumentOutOfRangeException(nameof(unitMassGrams));
            this.unitMassGrams = unitMassGrams;
        }

        public IReadOnlyList<string> CapabilityIds => new[]
        {
            ProductionOutputCapabilityIds.StandardDefinition
        };
        public IReadOnlyList<ProductionOutputCapabilityContractSnapshot>
            CapabilityContracts => new[]
            {
                new ProductionOutputCapabilityContractSnapshot(
                    ProductionOutputCapabilityIds.StandardDefinition,
                    ProductionOutputCapabilityIds.StandardDefinitionVersion,
                    ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                    ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion,
                    true)
            };
        public string RegistryFingerprint => Digest('e');

        public ProductionOutputMaximumMassProjection CaptureAutomatic(
            string outputLineId,
            string itemId,
            int maximumQuantity) => CaptureDeclared(
            Capability(),
            maximumQuantity);

        public ProductionOutputMaximumMassProjection CaptureDeclared(
            ProductionOutputCapabilityDescriptor descriptor,
            int maximumQuantity) => new(
            descriptor,
            maximumQuantity,
            unitMassGrams,
            checked(unitMassGrams * maximumQuantity),
            1L,
            Digest('e'));
    }

    private sealed class FixedQuery :
        IFacilityBufferPlannedOutputRestoreCandidateQuery
    {
        private readonly IReadOnlyList<
            FacilityBufferPlannedOutputRestoreBatchSnapshot> batches;
        private readonly Dictionary<string,
            FacilityBufferPlannedOutputRestoreBatchSnapshot> byId =
            new(StringComparer.Ordinal);

        internal FixedQuery(
            params FacilityBufferPlannedOutputRestoreBatchSnapshot[] batches)
        {
            this.batches = Array.AsReadOnly(batches
                ?? Array.Empty<
                    FacilityBufferPlannedOutputRestoreBatchSnapshot>());
            foreach (FacilityBufferPlannedOutputRestoreBatchSnapshot batch in
                     this.batches)
            {
                if (batch != null)
                    byId.Add(batch.BatchCommitId, batch);
            }
        }

        public bool IsCandidateAvailable => true;
        public IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot>
            Batches => batches;

        public bool TryGetBatch(
            string batchCommitId,
            out FacilityBufferPlannedOutputRestoreBatchSnapshot batch) =>
            byId.TryGetValue(batchCommitId ?? string.Empty, out batch);
    }

    private sealed class AcceptingDetachedCapacityGuard :
        IProductionOutputDetachedFacilityCapacityRestoreGuard
    {
        public ProductionOutputBufferCapacitySourceSnapshot Validate(
            string ownerStableId,
            string facilityInstanceId,
            ProductionOutputBatchMaximumMassProof maximumMassProof,
            string savedCapacitySourceDigest,
            long savedRequiredMinimumCapacityGrams) => new(
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

    private sealed class RecordingPublication :
        IFacilityBufferPlannedOutputPublicationService
    {
        internal bool RejectAcknowledgement { get; set; }
        internal int AcknowledgementCount { get; private set; }
        internal string LastBatchCommitId { get; private set; } = string.Empty;

        public bool TryAcknowledgeRestoreCandidate(
            FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason)
        {
            AcknowledgementCount++;
            LastBatchCommitId = candidate?.BatchCommitId ?? string.Empty;
            failureCode = FacilityBufferPlannedOutputPublicationFailureCode.None;
            failureReason = RejectAcknowledgement
                ? "injected-restore-acknowledgement-failure"
                : string.Empty;
            return candidate != null && !RejectAcknowledgement;
        }

        public bool TryAcknowledgeAndReleaseRestoreCandidate(
            FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
            FacilityBufferAcknowledgedOutputReleaseTarget target,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason) => TryAcknowledgeRestoreCandidate(
            candidate,
            out failureCode,
            out failureReason);

        public bool TryPublishFullBatch(
            FacilityBufferPlannedOutputToken token,
            out FacilityBufferPlannedOutputPublicationReceipt receipt,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason)
        {
            receipt = default;
            failureCode = FacilityBufferPlannedOutputPublicationFailureCode.None;
            failureReason = "unused";
            return false;
        }

        public bool TryRollbackPublishedBatch(
            FacilityBufferPlannedOutputPublicationReceipt receipt,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason)
        {
            failureCode = FacilityBufferPlannedOutputPublicationFailureCode.None;
            failureReason = "unused";
            return false;
        }

        public bool TryAcknowledgePublishedBatch(
            FacilityBufferPlannedOutputPublicationReceipt receipt,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason)
        {
            failureCode = FacilityBufferPlannedOutputPublicationFailureCode.None;
            failureReason = "unused";
            return false;
        }

        public bool TryAcknowledgeAndReleasePublishedBatch(
            FacilityBufferPlannedOutputPublicationReceipt receipt,
            FacilityBufferAcknowledgedOutputReleaseTarget target,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason)
        {
            failureCode = FacilityBufferPlannedOutputPublicationFailureCode.None;
            failureReason = "unused";
            return false;
        }

        public bool TryRollbackRestoreCandidate(
            FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason)
        {
            failureCode = FacilityBufferPlannedOutputPublicationFailureCode.None;
            failureReason = "unused";
            return false;
        }

        public bool TryCapturePendingBatch(
            string batchCommitId,
            out FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason)
        {
            candidate = null;
            failureCode = FacilityBufferPlannedOutputPublicationFailureCode.None;
            failureReason = "unused";
            return false;
        }

        public bool TryCaptureBatch(
            string batchCommitId,
            bool allowAcknowledged,
            out FacilityBufferPlannedOutputRestoreBatchSnapshot candidate,
            out bool acknowledged,
            out FacilityBufferPlannedOutputPublicationFailureCode failureCode,
            out string failureReason)
        {
            acknowledged = false;
            return TryCapturePendingBatch(
                batchCommitId,
                out candidate,
                out failureCode,
                out failureReason);
        }
    }

    private sealed class FixedCertifiedPersistence : ICertifiedSeedPersistence
    {
        internal int RestoreCount { get; private set; }
        internal IReadOnlyList<CertifiedSeedOrderSaveData> RestoredOrders {
            get;
            private set;
        } = Array.Empty<CertifiedSeedOrderSaveData>();

        public CertifiedSeedWorldSaveData Capture() => new();

        public CertifiedSeedRestoreCandidate BuildRestore(
            CertifiedSeedWorldSaveData snapshot)
        {
            ConstructorInfo constructor = typeof(CertifiedSeedRestoreCandidate)
                .GetConstructors(BindingFlags.Instance | BindingFlags.NonPublic)
                .Single();
            return (CertifiedSeedRestoreCandidate)constructor.Invoke(new object[]
            {
                snapshot.nextOrderSequence,
                (snapshot.orders ?? new List<CertifiedSeedOrderSaveData>())
                .Select(value => value.DeepClone())
                .ToArray(),
                null
            });
        }

        public void Restore(CertifiedSeedRestoreCandidate candidate)
        {
            RestoreCount++;
            PropertyInfo property = typeof(CertifiedSeedRestoreCandidate)
                .GetProperty(
                    "Orders",
                    BindingFlags.Instance | BindingFlags.NonPublic);
            RestoredOrders = ((IReadOnlyList<CertifiedSeedOrderSaveData>)
                    property.GetValue(candidate))
                .Select(value => value.DeepClone())
                .ToArray();
        }
    }

    private sealed class EmptyCertifiedInputDescriptorSource :
        ICertifiedSeedInputOwnerDescriptorSource
    {
        public IReadOnlyList<CertifiedSeedInputOwnerDescriptor>
            BuildInputOwnerDescriptors(
                IReadOnlyList<CertifiedSeedOrderSaveData> orders) =>
            Array.Empty<CertifiedSeedInputOwnerDescriptor>();
    }

    private sealed class AcceptingCertifiedInputOwners :
        ICertifiedSeedInputOwnerRuntime
    {
        public bool TryEnsure(
            CertifiedSeedInputOwnerDescriptor descriptor,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryRetire(
            CertifiedSeedInputOwnerDescriptor descriptor,
            string reasonCode,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }

        public bool TryReplaceForRestore(
            IReadOnlyList<CertifiedSeedInputOwnerDescriptor> descriptors,
            out string failureReason)
        {
            failureReason = string.Empty;
            return true;
        }
    }

}
