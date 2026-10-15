#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionDomainOutputPublicationDebugScenarios
{
    private const string FacilityId = "building:qa:production";

    [MenuItem("DungeonStory/V27/Physical Mass/Verify Domain Output Publication")]
    public static void RunFromMenu()
    {
        Debug.Log(VerifyAll());
    }

    public static string VerifyAll()
    {
        VerifyEmptyOwnerJsonRoundTrip();
        VerifyExactCommitAndIdempotentAcknowledgement();
        VerifyPendingAndAcknowledgedRestoreJoins();
        VerifyReleaseRejectsDriftAndPreservesExactBatch();
        VerifyAcknowledgementFaultRollsBackMarkerAndRoute();
        VerifyCapacityWaitPreservesFrozenOwner();
        VerifyPublicationFaultRollsBackReservationAndStacks();
        VerifyUniqueIdentityOwnerRoundTripAndDuplicateRejection();
        VerifyCapabilityMaximumRejectsHeavyPreparedOutputAtomically();
        VerifyMaximumProofOrderingAndDuplicateGuard();
        return "PRODUCTION_DOMAIN_OUTPUT_PUBLICATION_PASS";
    }

    private static void VerifyEmptyOwnerJsonRoundTrip()
    {
        ProductionDomainOutputPublicationSaveData empty = JsonUtility.FromJson<
            ProductionDomainOutputPublicationSaveData>(JsonUtility.ToJson(
                new ProductionDomainOutputPublicationSaveData()));
        bool committed = false;
        string failureReason = "owner-null";
        bool emptyRestorable = empty != null
            && ProductionDomainOutputPublicationService
                .TryValidateRestorableOwner(
                    empty,
                    out committed,
                    out failureReason);
        Require(empty != null
                && empty.IsEmpty
                && emptyRestorable
                && !committed,
            "A semantically empty gameplay-outcome attachment stopped an empty "
            + "domain-output owner from surviving JSON round-trip: "
            + failureReason);

        DomainFixture fixture = new(occupiedMassGrams: 6_000L);
        ProductionDomainOutputPublicationSaveData waiting = new();
        ProductionDomainOutputPublicationResult result = Service(fixture)
            .EnsureCommitted(waiting, Plan(quantity: 5));
        ProductionDomainOutputPublicationSaveData restored = JsonUtility.FromJson<
            ProductionDomainOutputPublicationSaveData>(JsonUtility.ToJson(waiting));
        bool restoredCommitted = false;
        string restoredFailure = "owner-null";
        bool waitingRestorable = restored != null
            && ProductionDomainOutputPublicationService
                .TryValidateRestorableOwner(
                    restored,
                    out restoredCommitted,
                    out restoredFailure);
        Require(result.Status ==
                ProductionDomainOutputPublicationStatus.WaitingForOutputSpace
            && restored != null
            && waitingRestorable
            && !restoredCommitted,
            "A waiting domain-output owner stopped being restorable after JSON "
            + "round-trip: "
            + restoredFailure);
    }

    private static void VerifyExactCommitAndIdempotentAcknowledgement()
    {
        DomainFixture fixture = new();
        IProductionDomainOutputPublicationService service = Service(fixture);
        ProductionDomainOutputPublicationSaveData owner = new();
        ProductionDomainOutputPublicationResult result = service.EnsureCommitted(
            owner,
            Plan(quantity: 1));
        Require(result.IsCommitted,
            "The common domain output did not commit an exact batch: "
            + result.Status + ":" + result.FailureReason);
        Require(ProductionDomainOutputPublicationService
                .TryValidateCommittedOwner(owner, out _)
            && owner.outputMassGrams == 1_000L
            && owner.maximumBatchMassGrams == 1_000L
            && owner.maximumMassProofDigest.Length == 64
            && owner.stacks.Count == 1
            && owner.stacks[0].quantity == 1
            && owner.stacks[0].massGrams == 1_000L,
            "The committed domain output owner did not freeze exact grams.");
        Require(service.TryAcknowledge(owner, out _)
            && service.TryAcknowledge(owner, out _)
            && fixture.Publication.CaptureEditorTestSnapshot().Stacks.All(
                value => value.MarkerCount == 1
                    && !value.MarkerAffectsStacking)
            && fixture.Query.GetAllStacks().All(value =>
                value.State == WorldItemStackState.Loose
                && string.IsNullOrEmpty(value.DestinationId)
                && !value.HasDestinationPosition),
            "Domain output acknowledgement was not exact and idempotent.");
    }

    private static void VerifyPendingAndAcknowledgedRestoreJoins()
    {
        DomainFixture fixture = new();
        IProductionDomainOutputPublicationService service = Service(fixture);
        ProductionDomainOutputPublicationSaveData owner = new();
        Require(service.EnsureCommitted(owner, Plan(quantity: 1)).IsCommitted,
            "The restore-join fixture did not publish its pending batch.");
        FacilityBufferPlannedOutputRestoreBatchSnapshot pending = fixture
            .Publication.CapturePendingRestoreBatchesForEditorTest().Single();
        ProductionDomainOutputRestoreGuard.ValidateIncoming(owner, pending);
        RequireRestoreRejected(
            () => ProductionDomainOutputRestoreGuard.ValidateAcknowledged(
                owner,
                pending),
            "A pending domain-output batch was accepted as acknowledged.");

        Require(service.TryAcknowledge(owner, out string acknowledgementFailure)
                && owner.outputAcknowledged,
            "The restore-join fixture did not acknowledge its output: "
            + acknowledgementFailure);
        FacilityBufferPlannedOutputRestoreBatchSnapshot acknowledged = fixture
            .Publication.CaptureAcknowledgedRestoreBatchesForEditorTest()
            .Single();
        ProductionDomainOutputRestoreGuard.ValidateAcknowledged(owner, acknowledged);
        RequireRestoreRejected(
            () => ProductionDomainOutputRestoreGuard.ValidateIncoming(
                owner,
                acknowledged),
            "An acknowledged domain-output batch was accepted as pending.");

        FacilityBufferPlannedOutputRestoreStackSnapshot stack =
            acknowledged.Stacks.Single();
        Require(stack.State == WorldItemStackState.Loose
                && string.IsNullOrEmpty(stack.DestinationId)
                && stack.Position == new Vector2Int(
                    owner.destinationX,
                    owner.destinationY),
            "The released domain-output restore batch lost its frozen route.");

        RequireRestoreRejected(
            () => ProductionDomainOutputRestoreGuard.ValidateAcknowledged(
                owner,
                ReplaceRestoreBatch(
                    acknowledged,
                    new string('0', 64),
                    acknowledged.PlannedOutputFingerprint,
                    acknowledged.TotalQuantity,
                    acknowledged.TotalMassGrams,
                    acknowledged.Stacks)),
            "An acknowledged domain-output batch accepted a fingerprint drift.");
        RequireRestoreRejected(
            () => ProductionDomainOutputRestoreGuard.ValidateAcknowledged(
                owner,
                ReplaceSingleRestoreStack(
                    acknowledged,
                    ReplaceRestoreStack(
                        stack,
                        state: WorldItemStackState.FacilityOutputBuffer))),
            "An acknowledged domain-output batch accepted a buffer state.");
        RequireRestoreRejected(
            () => ProductionDomainOutputRestoreGuard.ValidateAcknowledged(
                owner,
                ReplaceSingleRestoreStack(
                    acknowledged,
                    ReplaceRestoreStack(
                        stack,
                        destinationId: DomainFixture.DestinationId))),
            "An acknowledged domain-output batch accepted a destination drift.");
        RequireRestoreRejected(
            () => ProductionDomainOutputRestoreGuard.ValidateAcknowledged(
                owner,
                ReplaceSingleRestoreStack(
                    acknowledged,
                    ReplaceRestoreStack(
                        stack,
                        position: new Vector2Int(
                            owner.destinationX + 1,
                            owner.destinationY)))),
            "An acknowledged domain-output batch accepted a position drift.");
        RequireRestoreRejected(
            () => ProductionDomainOutputRestoreGuard.ValidateAcknowledged(
                owner,
                ReplaceRestoreBatch(
                    acknowledged,
                    acknowledged.OutcomeFingerprint,
                    acknowledged.PlannedOutputFingerprint,
                    acknowledged.TotalQuantity + 1,
                    acknowledged.TotalMassGrams,
                    new[] { ReplaceRestoreStack(
                        stack,
                        quantity: stack.Quantity + 1) })),
            "An acknowledged domain-output batch accepted a quantity drift.");
        RequireRestoreRejected(
            () => ProductionDomainOutputRestoreGuard.ValidateAcknowledged(
                owner,
                ReplaceRestoreBatch(
                    acknowledged,
                    acknowledged.OutcomeFingerprint,
                    acknowledged.PlannedOutputFingerprint,
                    acknowledged.TotalQuantity,
                    acknowledged.TotalMassGrams + 1L,
                    new[] { ReplaceRestoreStack(
                        stack,
                        massGrams: stack.MassGrams + 1L) })),
            "An acknowledged domain-output batch accepted a mass drift.");
        RequireRestoreRejected(
            () => ProductionDomainOutputRestoreGuard.ValidateAcknowledged(
                owner,
                ReplaceSingleRestoreStack(
                    acknowledged,
                    ReplaceRestoreStack(
                        stack,
                        itemInstanceId: "item-instance:qa:wrong"))),
            "An acknowledged domain-output batch accepted an item identity drift.");

        DomainFixture retainedFixture = new();
        IProductionDomainOutputPublicationService retainedService =
            Service(retainedFixture);
        ProductionDomainOutputPublicationSaveData retainedOwner = new();
        Require(retainedService.EnsureCommitted(
                    retainedOwner,
                    Plan(
                        quantity: 1,
                        acknowledgementDisposition:
                            ProductionDomainOutputAcknowledgementDisposition
                                .RetainFacilityOutputBuffer)).IsCommitted
                && retainedService.TryAcknowledge(retainedOwner, out _),
            "The retained restore-join fixture did not publish and acknowledge.");
        FacilityBufferPlannedOutputRestoreBatchSnapshot retained = retainedFixture
            .Publication.CaptureAcknowledgedRestoreBatchesForEditorTest()
            .Single();
        ProductionDomainOutputRestoreGuard.ValidateAcknowledged(
            retainedOwner,
            retained);
        Require(retained.Stacks.All(value =>
                    value.State == WorldItemStackState.FacilityOutputBuffer
                    && string.Equals(
                        value.DestinationId,
                        retainedOwner.destinationId,
                        StringComparison.Ordinal)
                    && value.Position == new Vector2Int(
                        retainedOwner.destinationX,
                        retainedOwner.destinationY)),
            "The retained domain-output restore batch lost its buffer route.");
    }

    private static void VerifyReleaseRejectsDriftAndPreservesExactBatch()
    {
        DomainFixture fixture = new();
        ProductionDomainOutputPublicationSaveData owner = new();
        ProductionDomainOutputPublicationResult result = Service(fixture)
            .EnsureCommitted(owner, TwoLinePlan());
        Require(result.IsCommitted && owner.stacks.Count == 2,
            "The release fixture did not commit its exact two-stack batch.");

        FacilityBufferPlannedOutputPublicationReceipt exact = Receipt(owner);
        FacilityBufferPublishedOutputStackReceipt[] tamperedStacks = exact.Stacks
            .Select((value, index) => new FacilityBufferPublishedOutputStackReceipt(
                value.StackId,
                value.OutputLineId,
                value.ItemDefinitionId,
                value.Quantity,
                new PhysicalMassGrams(value.MassGrams + (index == 0 ? 1L : 0L)),
                value.ItemInstanceId))
            .ToArray();
        FacilityBufferPlannedOutputPublicationReceipt tampered = new(
            exact.AdmissionTokenId,
            exact.BatchCommitId,
            exact.OutcomeFingerprint,
            exact.DestinationId,
            exact.DropPosition,
            exact.OwnerDomain,
            exact.OwnerOperationId,
            exact.OwnerFacilityId,
            exact.CapacityRevision,
            exact.PlannedOutputFingerprint,
            tamperedStacks);
        Require(!fixture.Publication.TryAcknowledgeAndReleasePublishedBatch(
                    tampered,
                    FacilityBufferAcknowledgedOutputReleaseTarget.Unassigned,
                    out _,
                    out _)
                && fixture.Query.GetAllStacks().All(value =>
                    value.State == WorldItemStackState.FacilityOutputBuffer
                    && string.Equals(
                        value.DestinationId,
                        DomainFixture.DestinationId,
                        StringComparison.Ordinal)),
            "A 1 g release drift partially changed the physical batch.");

        Require(fixture.Publication.TryAcknowledgeAndReleasePublishedBatch(
                exact,
                FacilityBufferAcknowledgedOutputReleaseTarget.Unassigned,
                out _,
                out _),
            "The exact acknowledged batch was not released for hauling.");
        WorldItemStackSnapshot[] released = fixture.Query.GetAllStacks()
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        string[] componentSignatures = released
            .Select(value => FacilityBufferPlannedOutputPublicationService
                .CreateRuntimeComponentSignature(value.Components))
            .ToArray();
        bool replay = fixture.Publication.TryAcknowledgeAndReleasePublishedBatch(
            exact,
            FacilityBufferAcknowledgedOutputReleaseTarget.Unassigned,
            out FacilityBufferPlannedOutputPublicationFailureCode replayCode,
            out string replayFailure);
        bool stackIdsStable = released.Select(value => value.StackId).SequenceEqual(
            fixture.Query.GetAllStacks()
                .OrderBy(value => value.StackId, StringComparer.Ordinal)
                .Select(value => value.StackId));
        bool componentsStable = componentSignatures.SequenceEqual(
            fixture.Query.GetAllStacks()
                .OrderBy(value => value.StackId, StringComparer.Ordinal)
                .Select(value => FacilityBufferPlannedOutputPublicationService
                    .CreateRuntimeComponentSignature(value.Components)));
        Require(released.Length == 2
                && released.All(value =>
                    value.State == WorldItemStackState.Loose
                    && string.IsNullOrEmpty(value.DestinationId)
                    && !value.HasDestinationPosition)
                && replay
                && stackIdsStable
                && componentsStable,
            "Release replay recreated a stack or changed its components: "
                + $"replay={replay}/{replayCode}/{replayFailure},"
                + $"stackIds={stackIdsStable},components={componentsStable},"
                + $"count={released.Length}.");
    }

    private static void VerifyAcknowledgementFaultRollsBackMarkerAndRoute()
    {
        DomainFixture fixture = new(
            acknowledgementFault: new FailOnceBeforeSecondMutation());
        IProductionDomainOutputPublicationService service = Service(fixture);
        ProductionDomainOutputPublicationSaveData owner = new();
        Require(service.EnsureCommitted(owner, TwoLinePlan()).IsCommitted,
            "The atomic acknowledgement-fault fixture did not commit.");

        Require(!service.TryAcknowledge(owner, out _)
                && !owner.outputAcknowledged
                && fixture.Query.GetAllStacks().All(value =>
                    value.State == WorldItemStackState.FacilityOutputBuffer
                    && string.Equals(
                        value.DestinationId,
                        DomainFixture.DestinationId,
                        StringComparison.Ordinal))
                && fixture.Publication
                    .CapturePendingRestoreBatchesForEditorTest().Count == 1,
            "An acknowledgement fault left a provenance/route split-brain.");
        Require(service.TryAcknowledge(owner, out _)
                && owner.outputAcknowledged
                && fixture.Query.GetAllStacks().All(value =>
                    value.State == WorldItemStackState.Loose
                    && string.IsNullOrEmpty(value.DestinationId))
                && fixture.Publication
                    .CapturePendingRestoreBatchesForEditorTest().Count == 0,
            "The exact acknowledgement retry did not converge after rollback.");
    }

    private static void VerifyCapacityWaitPreservesFrozenOwner()
    {
        DomainFixture fixture = new(occupiedMassGrams: 6_000L);
        IProductionDomainOutputPublicationService service = Service(fixture);
        ProductionDomainOutputPublicationSaveData owner = new();
        ProductionDomainOutputPublicationResult result = service.EnsureCommitted(
            owner,
            Plan(quantity: 5));
        Require(result.Status ==
                ProductionDomainOutputPublicationStatus.WaitingForOutputSpace
            && ProductionDomainOutputPublicationService
                .TryValidateRestorableOwner(owner, out bool committed, out _)
            && !committed
            && owner.outputMassGrams == 5_000L
            && owner.maximumBatchMassGrams == 5_000L
            && owner.maximumMassProofDigest.Length == 64
            && string.IsNullOrEmpty(owner.admissionTokenId)
            && !owner.outputPublished
            && !owner.admissionCommitted
            && owner.stacks.Count == 0,
            "Capacity pressure did not preserve a retryable frozen owner.");
    }

    private static void VerifyPublicationFaultRollsBackReservationAndStacks()
    {
        DomainFixture fixture = new(
            fault: new FailFirstStack());
        IProductionDomainOutputPublicationService service = Service(fixture);
        ProductionDomainOutputPublicationSaveData owner = new();
        ProductionDomainOutputPublicationResult result = service.EnsureCommitted(
            owner,
            Plan(quantity: 1));
        Require(result.Status == ProductionDomainOutputPublicationStatus.Pending
            && ProductionDomainOutputPublicationService
                .TryValidateRestorableOwner(owner, out bool committed, out _)
            && !committed
            && string.IsNullOrEmpty(owner.admissionTokenId)
            && !owner.outputPublished
            && !owner.admissionCommitted
            && fixture.Publication.CaptureEditorTestSnapshot().Stacks.Count == 0,
            "A failed domain publication left a stack or admission owner behind.");
    }

    private static void VerifyUniqueIdentityOwnerRoundTripAndDuplicateRejection()
    {
        const string InstanceId = "item-instance:domain-output:dagger:001";
        DomainFixture fixture = new();
        IProductionDomainOutputPublicationService service = Service(fixture);
        ProductionDomainOutputPublicationSaveData owner = new();
        ProductionDomainOutputPublicationResult result = service.EnsureCommitted(
            owner,
            UniquePlan(InstanceId));
        Require(result.IsCommitted
                && owner.stacks.Count == 1
                && owner.stacks[0].itemInstanceId == InstanceId,
            "Common domain output did not preserve unique item identity.");
        Require(fixture.Admission.TryGetPlannedOutputToken(
                owner.admissionTokenId,
                out FacilityBufferPlannedOutputToken uniqueToken,
                out FacilityBufferMassAdmissionTokenStatus uniqueStatus)
            && uniqueStatus == FacilityBufferMassAdmissionTokenStatus.Routed,
            "Common domain output did not retain its terminal admission receipt.");
        FacilityBufferPlannedOutputPublicationReceipt exactReceipt = Receipt(owner);
        FacilityBufferPublishedOutputStackReceipt exactStack =
            exactReceipt.Stacks.Single();
        FacilityBufferPlannedOutputPublicationReceipt wrongInstanceReceipt = new(
            exactReceipt.AdmissionTokenId,
            exactReceipt.BatchCommitId,
            exactReceipt.OutcomeFingerprint,
            exactReceipt.DestinationId,
            exactReceipt.DropPosition,
            exactReceipt.OwnerDomain,
            exactReceipt.OwnerOperationId,
            exactReceipt.OwnerFacilityId,
            exactReceipt.CapacityRevision,
            exactReceipt.PlannedOutputFingerprint,
            new[]
            {
                new FacilityBufferPublishedOutputStackReceipt(
                    exactStack.StackId,
                    exactStack.OutputLineId,
                    exactStack.ItemDefinitionId,
                    exactStack.Quantity,
                    exactStack.Mass,
                    InstanceId + ":tampered")
            });
        Require(!fixture.Admission.TryCommitPlannedOutput(
                uniqueToken,
                wrongInstanceReceipt,
                out _,
                out FacilityBufferMassAdmissionFailureCode instanceFailure,
                out _)
            && instanceFailure == FacilityBufferMassAdmissionFailureCode.TokenMismatch,
            "Common admission accepted a publication receipt for a different unique item instance.");
        ProductionDomainOutputPublicationSaveData restored = JsonUtility.FromJson<
            ProductionDomainOutputPublicationSaveData>(JsonUtility.ToJson(owner));
        Require(
            ProductionDomainOutputPublicationService.TryValidateCommittedOwner(
                restored,
                out _)
            && restored.schemaVersion ==
                ProductionDomainOutputPublicationSaveData.CurrentSchemaVersion
            && restored.maximumBatchMassGrams
                == owner.maximumBatchMassGrams
            && restored.maximumMassProofDigest
                == owner.maximumMassProofDigest
            && restored.stacks.Single().itemInstanceId == InstanceId,
            "Common domain-output schema lost itemInstanceId on JSON round-trip.");

        ProductionDomainOutputPublicationSaveData duplicate = owner.Clone();
        ProductionDomainPublishedStackSaveData duplicateStack =
            duplicate.stacks[0].Clone();
        duplicateStack.stackId += ":duplicate";
        duplicate.stacks.Add(duplicateStack);
        duplicate.outputMassGrams = checked(
            duplicate.outputMassGrams + duplicateStack.massGrams);
        Require(
            !ProductionDomainOutputPublicationService.TryValidateCommittedOwner(
                duplicate,
                out _),
            "Common domain-output owner accepted a duplicate unique identity.");

        FacilityBufferPlannedOutputRestoreBatchSnapshot incoming = fixture
            .Publication.CapturePendingRestoreBatchesForEditorTest().Single();
        FacilityBufferPlannedOutputRestoreStackSnapshot stack =
            incoming.Stacks.Single();
        FacilityBufferPlannedOutputRestoreBatchSnapshot tampered = new(
            incoming.BatchCommitId,
            incoming.OutcomeFingerprint,
            incoming.PlannedOutputFingerprint,
            incoming.TotalQuantity,
            incoming.TotalMassGrams,
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
        bool rejected = false;
        try
        {
            ProductionDomainOutputRestoreGuard.ValidateIncoming(owner, tampered);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }
        Require(rejected,
            "Common restore join accepted a mismatched unique identity.");

        ProductionDomainOutputPublicationSaveData underreported = owner.Clone();
        underreported.maximumBatchMassGrams = owner.outputMassGrams - 1L;
        Require(
            !ProductionDomainOutputPublicationService.TryValidateCommittedOwner(
                underreported,
                out _),
            "Common domain-output owner accepted an underreported maximum mass.");

        ProductionDomainOutputPublicationSaveData invalidDigest = owner.Clone();
        invalidDigest.maximumMassProofDigest = new string('A', 64);
        Require(
            !ProductionDomainOutputPublicationService.TryValidateCommittedOwner(
                invalidDigest,
                out _),
            "Common domain-output owner accepted a non-canonical proof digest.");
    }

    private static void VerifyCapabilityMaximumRejectsHeavyPreparedOutputAtomically()
    {
        DomainFixture fixture = new();
        ProductionDomainOutputPublicationSaveData owner = new();
        ProductionDomainOutputPublicationResult result = Service(
                fixture,
                maximumDefinitionUnitMassGrams: 999L)
            .EnsureCommitted(owner, Plan(quantity: 1));
        Require(result.Status == ProductionDomainOutputPublicationStatus.Conflict
                && result.FailureReason ==
                    "domain-output-line-mass-exceeds-capability-maximum"
                && owner.IsEmpty
                && fixture.Query.GetAllStacks().Count == 0
                && fixture.Publication.CaptureEditorTestSnapshot().Stacks.Count == 0,
            "A prepared output above its capability maximum mutated publication state: "
                + result.Status + ":" + result.FailureReason);
    }

    private static void VerifyMaximumProofOrderingAndDuplicateGuard()
    {
        DomainFixture fixture = new();
        FixedMaximumMassRegistry registry = new(fixture.Mass);
        ProductionOutputMaximumMassProjection a = registry.CaptureDeclared(
            Capability("output:qa:a", "item:qa:a"), 1);
        ProductionOutputMaximumMassProjection b = registry.CaptureDeclared(
            Capability("output:qa:b", "item:qa:b"), 2);
        ProductionOutputBatchMaximumMassProof forward = new(new[] { a, b });
        ProductionOutputBatchMaximumMassProof reverse = new(new[] { b, a });
        Require(forward.MaximumBatchMassGrams == 2_000L
                && reverse.MaximumBatchMassGrams == 2_000L
                && forward.SourceDigest == reverse.SourceDigest,
            "Maximum-mass proof changed with input line ordering.");
        bool duplicateRejected = false;
        try
        {
            _ = new ProductionOutputBatchMaximumMassProof(new[] { a, a });
        }
        catch (InvalidOperationException)
        {
            duplicateRejected = true;
        }
        Require(duplicateRejected,
            "Maximum-mass proof accepted a duplicate output line.");
    }

    internal static IProductionDomainOutputPublicationService Service(
        DomainFixture fixture,
        long maximumDefinitionUnitMassGrams = 0L,
        DungeonStory.Foundation.Facts.IDomainFactStagingPublisher facts = null,
        DungeonStory.Foundation.Facts.IDomainFactRecoveryPublisher recovery = null,
        DungeonStory.Foundation.Facts.IDomainFactDeliveryQuery deliveries = null) =>
        new ProductionDomainOutputPublicationService(
            new FixedFacilityQuery(fixture.Handle),
            new FixedDestinationAuthority(fixture.Handle, fixture.Profile),
            new FixedCapacityProjector(),
            new FixedMaximumMassRegistry(
                fixture.Mass,
                maximumDefinitionUnitMassGrams),
            fixture.Admission,
            fixture.Publication,
            fixture.Mass,
            fixture.Publication,
            fixture.Admission,
            facts, recovery, deliveries);

    private static ProductionDomainOutputPublicationPlan Plan(
        int quantity,
        ProductionDomainOutputAcknowledgementDisposition
            acknowledgementDisposition =
                ProductionDomainOutputAcknowledgementDisposition
                    .ReleaseLooseOrDestination) => new(
            "domain-output-publication:qa:",
            "owner:1",
            "domain-output-batch:qa:owner:1",
            Digest('a'),
            new object(),
            new[]
            {
                new ProductionDomainOutputLine(
                    "output:qa",
                    "item:qa:a",
                    quantity,
                    string.Empty,
                    Array.Empty<ItemInstanceComponentSaveData>(),
                    Capability("output:qa", "item:qa:a"))
            },
            acknowledgementDisposition: acknowledgementDisposition);

    private static ProductionDomainOutputPublicationPlan TwoLinePlan() => new(
        "domain-output-publication:qa-two-line:",
        "owner:two-line:1",
        "domain-output-batch:qa:owner:two-line:1",
        Digest('d'),
        new object(),
        new[]
        {
            new ProductionDomainOutputLine(
                "output:qa:a",
                "item:qa:a",
                1,
                string.Empty,
                Array.Empty<ItemInstanceComponentSaveData>(),
                Capability("output:qa:a", "item:qa:a")),
            new ProductionDomainOutputLine(
                "output:qa:b",
                "item:qa:b",
                1,
                string.Empty,
                Array.Empty<ItemInstanceComponentSaveData>(),
                Capability("output:qa:b", "item:qa:b"))
        });

    private static FacilityBufferPlannedOutputPublicationReceipt Receipt(
        ProductionDomainOutputPublicationSaveData owner) => new(
        owner.admissionTokenId,
        owner.batchCommitId,
        owner.outcomeFingerprint,
        owner.destinationId,
        new Vector2Int(owner.destinationX, owner.destinationY),
        owner.ownerDomain,
        owner.ownerOperationId,
        owner.ownerFacilityId,
        owner.capacityRevision,
        owner.plannedOutputFingerprint,
        owner.stacks.Select(value =>
            new FacilityBufferPublishedOutputStackReceipt(
                value.stackId,
                value.outputLineId,
                (ItemDefinitionId)value.itemId,
                value.quantity,
                new PhysicalMassGrams(value.massGrams),
                value.itemInstanceId))
            .ToArray());

    private static FacilityBufferPlannedOutputRestoreBatchSnapshot
        ReplaceSingleRestoreStack(
            FacilityBufferPlannedOutputRestoreBatchSnapshot source,
            FacilityBufferPlannedOutputRestoreStackSnapshot replacement) =>
        ReplaceRestoreBatch(
            source,
            source.OutcomeFingerprint,
            source.PlannedOutputFingerprint,
            source.TotalQuantity,
            source.TotalMassGrams,
            new[] { replacement });

    private static FacilityBufferPlannedOutputRestoreBatchSnapshot
        ReplaceRestoreBatch(
            FacilityBufferPlannedOutputRestoreBatchSnapshot source,
            string outcomeFingerprint,
            string plannedOutputFingerprint,
            int totalQuantity,
            long totalMassGrams,
            IReadOnlyList<FacilityBufferPlannedOutputRestoreStackSnapshot>
                stacks) => new(
            source.BatchCommitId,
            outcomeFingerprint,
            plannedOutputFingerprint,
            totalQuantity,
            totalMassGrams,
            stacks);

    private static FacilityBufferPlannedOutputRestoreStackSnapshot
        ReplaceRestoreStack(
            FacilityBufferPlannedOutputRestoreStackSnapshot source,
            WorldItemStackState? state = null,
            Vector2Int? position = null,
            string destinationId = null,
            int? quantity = null,
            long? massGrams = null,
            string itemInstanceId = null) => new(
            source.BatchCommitId,
            source.OutcomeFingerprint,
            source.PlannedOutputFingerprint,
            source.OutputLineId,
            source.StackOrdinal,
            source.StackId,
            source.ItemId,
            quantity ?? source.Quantity,
            massGrams ?? source.MassGrams,
            source.ComponentSignature,
            state ?? source.State,
            position ?? source.Position,
            destinationId ?? source.DestinationId,
            itemInstanceId ?? source.ItemInstanceId,
            source.Components,
            source.PreparedComponentFingerprint);

    private static void RequireRestoreRejected(
        Action action,
        string message)
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

    private static ProductionDomainOutputPublicationPlan UniquePlan(
        string instanceId)
    {
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
        return new ProductionDomainOutputPublicationPlan(
            "domain-output-publication:qa-unique:",
            "owner:unique:1",
            "domain-output-batch:qa:owner:unique:1",
            Digest('c'),
            new object(),
            new[]
            {
                new ProductionDomainOutputLine(
                    "output:equipment",
                    PhysicalItemIds.ForEquipment("weapon:dagger"),
                    1,
                    instanceId,
                    new[] { EquipmentItemStateCodec.Encode(prepared) },
                    Capability(
                        "output:equipment",
                        PhysicalItemIds.ForEquipment("weapon:dagger")))
            });
    }

    private static ProductionOutputCapabilityDescriptor Capability(
        string outputLineId,
        string itemId) => new(
        outputLineId,
        itemId,
        ProductionOutputCapabilityIds.StandardDefinition,
        ProductionOutputCapabilityIds.StandardDefinitionVersion,
        ProductionOutputCapabilityIds.DefinitionOnlyCodec,
        ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion,
        ProductionOutputCapabilityDescriptorFingerprint.Capture(
            outputLineId,
            itemId,
            ProductionOutputCapabilityIds.StandardDefinition,
            ProductionOutputCapabilityIds.StandardDefinitionVersion,
            ProductionOutputCapabilityIds.DefinitionOnlyCodec,
            ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion));

    private static string Digest(char value) => new(value, 64);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class FixedFacilityQuery : IProductionFacilityHandleQuery
    {
        private readonly ProductionFacilityHandle handle;
        internal FixedFacilityQuery(ProductionFacilityHandle handle) =>
            this.handle = handle;
        public ProductionFacilityHandle CaptureFacility(object runtimeObject) =>
            runtimeObject == null ? null : handle;
    }

    internal sealed class DomainFixture
    {
        internal const string DestinationId =
            "production:qa:domain-output-buffer";
        internal const string OwnerDomain = "economy.production-output";
        internal static readonly Vector2Int DropPosition = new(9, 4);

        internal DomainFixture(
            long occupiedMassGrams = 0L,
            IFacilityBufferPlannedOutputPublicationFaultInjector fault = null,
            IFacilityBufferPlannedOutputAcknowledgementFaultInjector
                acknowledgementFault = null,
            object runtimeObject = null,
            IDungeonItemCatalogProvider itemCatalog = null)
        {
            IDungeonItemCatalogProvider resolvedCatalog = itemCatalog
                ?? new FacilityBufferPlannedOutputPublicationDebugScenarios
                    .FakeCatalog();
            Mass = itemCatalog == null
                ? new FacilityBufferPlannedOutputPublicationDebugScenarios
                    .FakeMassQuery()
                : new PhysicalItemMassQuery(itemCatalog);
            Repository = new WorldItemRepository(
                new GuidPersistentIdGenerator(),
                new DungeonRuntimeAggregateRootStore());
            Query = new WorldItemQueryService(
                resolvedCatalog,
                Mass,
                Repository,
                EditorNullItemMarkerPresenter.Instance);
            Claims = new FacilityBufferDestinationClaimRegistry();
            Require(Claims.TryClaim(
                    new FacilityBufferDestinationClaim(
                        DestinationId,
                        DropPosition,
                        OwnerDomain,
                        DestinationId,
                        FacilityId,
                        FacilityBufferDestinationAnchorKind.LiveBuilding),
                    out _,
                    out _),
                "Domain publication fixture could not claim its destination.");
            Admission = new FacilityBufferMassAdmissionService(
                Claims,
                new FixedOccupancy(occupiedMassGrams),
                Mass);
            Profile = new FacilityBufferCapacityProfile(
                DestinationId,
                DropPosition,
                OwnerDomain,
                DestinationId,
                FacilityId,
                new PhysicalMassGrams(10_000L),
                1L);
            Require(Admission.TryReplaceOwnedProfiles(
                    OwnerDomain,
                    new[] { Profile },
                    out _,
                    out _),
                "Domain publication fixture could not publish capacity.");
            Publication = new FacilityBufferPlannedOutputPublicationService(
                Repository,
                resolvedCatalog,
                Mass,
                Admission,
                fault,
                acknowledgementFault);
            Handle = new ProductionFacilityHandle(
                runtimeObject ?? new object(),
                (BuildingInstanceId)FacilityId,
                DropPosition,
                false,
                string.Empty,
                false,
                Vector2Int.zero,
                "building-definition:qa-production",
                "workstation:qa-production",
                2,
                workstationLaneProfile:
                    ProductionFacilityWorkstationLaneCapacityProfile
                        .SingleManualWithDetachedBatchProcessors);
        }

        internal ProductionFacilityHandle Handle { get; }
        internal WorldItemRepository Repository { get; }
        internal WorldItemQueryService Query { get; }
        internal FacilityBufferCapacityProfile Profile { get; }
        internal FacilityBufferMassAdmissionService Admission { get; }
        internal FacilityBufferDestinationClaimRegistry Claims { get; }
        internal FacilityBufferPlannedOutputPublicationService Publication
            { get; }
        internal IPhysicalItemMassQuery Mass { get; }
    }

    private sealed class FixedOccupancy :
        IFacilityBufferPhysicalOccupancyQuery
    {
        private readonly long occupiedMassGrams;
        internal FixedOccupancy(long occupiedMassGrams) =>
            this.occupiedMassGrams = occupiedMassGrams;

        public FacilityBufferPhysicalOccupancySnapshot Capture(
            string destinationId) => new(occupiedMassGrams, 0L);

        public bool TryCaptureExactLot(
            IReadOnlyList<FacilityBufferMassLotSlice> slices,
            out FacilityBufferExactLotSnapshot lot,
            out string failureReason)
        {
            lot = default;
            failureReason = "qa-not-used";
            return false;
        }
    }

    private sealed class FixedCapacityProjector :
        IProductionOutputBufferCapacityProjector
    {
        public ProductionOutputBufferCapacitySourceSnapshot CaptureSource(
            ProductionFacilityHandle facility,
            ProductionOutputBatchMaximumMassProof maximumMassProof) => new(
            2,
            maximumMassProof.MaximumBatchMassGrams,
            10_000L,
            checked(maximumMassProof.MaximumBatchMassGrams * 2L),
            Math.Max(
                10_000L,
                checked(maximumMassProof.MaximumBatchMassGrams * 2L)),
            Digest('b'));
    }

    private sealed class FixedMaximumMassRegistry :
        IProductionOutputMaximumMassRegistry
    {
        private readonly IPhysicalItemMassQuery mass;
        private readonly long maximumDefinitionUnitMassGrams;

        internal FixedMaximumMassRegistry(
            IPhysicalItemMassQuery mass,
            long maximumDefinitionUnitMassGrams = 0L)
        {
            this.mass = mass;
            this.maximumDefinitionUnitMassGrams =
                maximumDefinitionUnitMassGrams;
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
        public string RegistryFingerprint => Digest('f');

        public ProductionOutputMaximumMassProjection CaptureAutomatic(
            string outputLineId,
            string itemId,
            int maximumQuantity) => CaptureDeclared(
            Capability(outputLineId, itemId), maximumQuantity);

        public ProductionOutputMaximumMassProjection CaptureDeclared(
            ProductionOutputCapabilityDescriptor descriptor,
            int maximumQuantity)
        {
            long unitMass = mass.GetDefinitionUnitMass(
                (ItemDefinitionId)descriptor.ItemId).Value;
            if (maximumDefinitionUnitMassGrams > 0L)
                unitMass = maximumDefinitionUnitMassGrams;
            return new ProductionOutputMaximumMassProjection(
                descriptor,
                maximumQuantity,
                unitMass,
                checked(unitMass * maximumQuantity),
                mass.AuthorityRevision,
                Digest('e'));
        }
    }

    private sealed class FixedDestinationAuthority :
        IProductionOutputDestinationAuthorityRuntime
    {
        private readonly ProductionFacilityHandle expected;
        private readonly FacilityBufferCapacityProfile profile;

        internal FixedDestinationAuthority(
            ProductionFacilityHandle expected,
            FacilityBufferCapacityProfile profile)
        {
            this.expected = expected;
            this.profile = profile;
        }

        public bool TryEnsure(
            ProductionFacilityHandle facility,
            long minimumMassCapacityGrams,
            out FacilityBufferCapacityProfile result,
            out string failureReason)
        {
            bool valid = ReferenceEquals(facility, expected)
                && minimumMassCapacityGrams == 10_000L;
            result = valid ? profile : null;
            failureReason = valid ? string.Empty : "qa-destination-drift";
            return valid;
        }

        public bool TryValidate(
            ProductionFacilityHandle facility,
            out FacilityBufferCapacityProfile result,
            out string failureReason) => TryEnsure(
            facility,
            10_000L,
            out result,
            out failureReason);

        public bool TryReplaceProjected(
            IReadOnlyList<ProductionFacilityHandle> facilities,
            IReadOnlyDictionary<string, long> capacityGramsByFacilityId,
            out string failureReason)
        {
            failureReason = "qa-not-used";
            return false;
        }

        public bool TryRevoke(
            BuildingInstanceId facilityId,
            out string failureReason)
        {
            failureReason = "qa-not-used";
            return false;
        }
    }

    private sealed class FailFirstStack :
        IFacilityBufferPlannedOutputPublicationFaultInjector
    {
        public bool FailBeforeRepositoryAdd(int zeroBasedStackIndex) =>
            zeroBasedStackIndex == 0;
    }

    private sealed class FailOnceBeforeSecondMutation :
        IFacilityBufferPlannedOutputAcknowledgementFaultInjector
    {
        private bool failed;

        public bool FailBeforeRepositoryMutation(int zeroBasedStackIndex)
        {
            if (failed || zeroBasedStackIndex != 1)
                return false;
            failed = true;
            return true;
        }
    }
}
#endif
