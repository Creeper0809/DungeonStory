#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Focused, side-effect-free restore validation scenarios for the surgical-part
/// exact-output capability. This intentionally lives in the runtime assembly so
/// it can exercise the internal prepared-component codec used by production.
/// </summary>
public static class
    SurgicalPartProductionOutputRestoreCapabilityValidatorDebugScenarios
{
    private const string BillId = "production-bill:qa-surgical-restore";
    private const string OutputLineId = "output:main";
    private const string FacilityId = "building-instance:qa-surgical-restore";
    private const string DestinationId =
        "production-output:building-instance:qa-surgical-restore";
    private const string PartInstanceId = "surgical-part:qa-arm-left";
    private const string StackId = "world-item-stack:qa-surgical-restore";
    private const long ExactMassGrams = 2400L;
    private const long RequiredCapacityGrams = 4800L;
    private const long MassAuthorityRevision = 7L;
    private static readonly Vector2Int FacilityPosition = new(7, 9);

    [MenuItem(
        "Tools/Dungeon Story/QA/Run Surgical Part Restore Capability Scenarios")]
    public static void RunAll()
    {
        VerifyValidPendingCandidate();
        VerifyValidAcknowledgedCandidate();

        VerifyRejected(Tamper.OutcomeFingerprint);
        VerifyRejected(Tamper.PlannedOutputFingerprint);
        VerifyRejected(Tamper.PairedOutcomeAndPlannedFingerprints);
        VerifyRejected(Tamper.Node);
        VerifyRejected(Tamper.Kind);
        VerifyRejected(Tamper.Quality);
        VerifyRejected(Tamper.Commit);
        VerifyRejected(Tamper.Component);
        VerifyRejected(Tamper.PreparedComponentFingerprint);
        VerifyRejected(Tamper.Mass);
        VerifyRejected(Tamper.Facility);
        VerifyRejected(Tamper.Destination);
        VerifyRejected(Tamper.Capacity);

        Debug.Log(
            "Surgical part production-output restore capability scenarios passed.");
    }

    private static void VerifyValidPendingCandidate()
    {
        Fixture fixture = Fixture.Create(Tamper.None, isPendingPhysical: true);
        string before = CaptureExternalState(fixture);

        fixture.Validator.Validate(fixture.Context);

        Require(
            string.Equals(before, CaptureExternalState(fixture),
                StringComparison.Ordinal),
            "Valid pending validation mutated external state.");
        Require(fixture.Projection.SemanticMutationRevision == 0L,
            "Valid pending validation mutated projection authority.");
    }

    private static void VerifyValidAcknowledgedCandidate()
    {
        Fixture fixture = Fixture.Create(Tamper.None, isPendingPhysical: false);
        string before = CaptureExternalState(fixture);

        fixture.Validator.Validate(fixture.Context);

        Require(
            string.Equals(before, CaptureExternalState(fixture),
                StringComparison.Ordinal),
            "Valid acknowledged validation mutated external state.");
        Require(fixture.Projection.SemanticMutationRevision == 0L,
            "Valid acknowledged validation mutated projection authority.");
    }

    private static void VerifyRejected(Tamper tamper)
    {
        Fixture fixture = Fixture.Create(tamper, isPendingPhysical: true);
        string before = CaptureExternalState(fixture);
        long projectionRevision = fixture.Projection.SemanticMutationRevision;
        bool rejected = false;
        try
        {
            fixture.Validator.Validate(fixture.Context);
        }
        catch (InvalidOperationException)
        {
            rejected = true;
        }

        Require(rejected, "Expected surgical restore rejection: " + tamper);
        Require(
            string.Equals(before, CaptureExternalState(fixture),
                StringComparison.Ordinal),
            "Rejected surgical restore validation mutated its inputs: " + tamper);
        Require(
            projectionRevision == fixture.Projection.SemanticMutationRevision,
            "Rejected surgical restore validation mutated projection authority: "
            + tamper);
    }

    private static string CaptureExternalState(Fixture fixture)
    {
        ProductionResolvedOutputRestoreValidationContext context = fixture.Context;
        StringBuilder state = new();
        Append(state, fixture.Projection.SemanticMutationRevision);
        Append(state, context.Bill.billId);
        Append(state, context.Bill.buildingInstanceId);
        Append(state, context.Bill.outputDestinationId);
        Append(state, context.Output.outputLineId);
        Append(state, context.Output.itemId);
        Append(state, context.Output.pendingCommitId);
        Append(state, context.Output.workerQuality);
        Append(state, context.MaximumMassProof.SourceDigest);
        Append(state, context.MaximumMassProof.MaximumBatchMassGrams);
        Append(state, context.FacilityCapacity.FacilityInstanceId);
        Append(state, context.FacilityCapacity.FacilityPosition.x);
        Append(state, context.FacilityCapacity.FacilityPosition.y);
        Append(state, context.FacilityCapacity.Capacity.SourceDigest);
        Append(state,
            context.FacilityCapacity.Capacity.RequiredMinimumCapacityGrams);
        Append(state, context.Physical.BatchCommitId);
        Append(state, context.Physical.OutcomeFingerprint);
        Append(state, context.Physical.PlannedOutputFingerprint);
        Append(state, context.Physical.TotalQuantity);
        Append(state, context.Physical.TotalMassGrams);
        Append(state, context.IsPendingPhysical ? 1 : 0);
        foreach (FacilityBufferPlannedOutputRestoreStackSnapshot stack in
                 context.Physical.Stacks)
        {
            Append(state, stack.BatchCommitId);
            Append(state, stack.OutcomeFingerprint);
            Append(state, stack.PlannedOutputFingerprint);
            Append(state, stack.OutputLineId);
            Append(state, stack.StackOrdinal);
            Append(state, stack.StackId);
            Append(state, stack.ItemId);
            Append(state, stack.Quantity);
            Append(state, stack.MassGrams);
            Append(state, (int)stack.State);
            Append(state, stack.Position.x);
            Append(state, stack.Position.y);
            Append(state, stack.DestinationId);
            Append(state, stack.PreparedComponentFingerprint);
            foreach (ItemInstanceComponentSaveData component in stack.Components)
                Append(state, component?.ToCanonicalString());
        }
        return state.ToString();
    }

    private static void Append(StringBuilder state, object value)
    {
        string text = value?.ToString() ?? string.Empty;
        state.Append(text.Length).Append(':').Append(text).Append('|');
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static string Digest(char value) => new(value, 64);

    private enum Tamper
    {
        None,
        OutcomeFingerprint,
        PlannedOutputFingerprint,
        PairedOutcomeAndPlannedFingerprints,
        Node,
        Kind,
        Quality,
        Commit,
        Component,
        PreparedComponentFingerprint,
        Mass,
        Facility,
        Destination,
        Capacity
    }

    private sealed class Fixture
    {
        private Fixture(
            SurgicalPartProductionOutputRestoreCapabilityValidator validator,
            PureFixedProjectionQuery projection,
            ProductionResolvedOutputRestoreValidationContext context)
        {
            Validator = validator;
            Projection = projection;
            Context = context;
        }

        internal SurgicalPartProductionOutputRestoreCapabilityValidator Validator
        {
            get;
        }
        internal PureFixedProjectionQuery Projection { get; }
        internal ProductionResolvedOutputRestoreValidationContext Context { get; }

        internal static Fixture Create(Tamper tamper, bool isPendingPhysical)
        {
            string itemId =
                SurgicalPartProductionOutputHandler.ProstheticArmOutputId;
            string descriptorFingerprint =
                ProductionOutputCapabilityDescriptorFingerprint.Capture(
                    OutputLineId,
                    itemId,
                    SurgicalPartProductionOutputHandler.HandlerCapabilityId,
                    SurgicalPartProductionOutputHandler.HandlerContractVersion,
                    SurgicalPartProductionOutputHandler.HandlerComponentCodecId,
                    SurgicalPartProductionOutputHandler
                        .HandlerComponentCodecVersion);
            ProductionOutputCapabilityDescriptor descriptor = new(
                OutputLineId,
                itemId,
                SurgicalPartProductionOutputHandler.HandlerCapabilityId,
                SurgicalPartProductionOutputHandler.HandlerContractVersion,
                SurgicalPartProductionOutputHandler.HandlerComponentCodecId,
                SurgicalPartProductionOutputHandler.HandlerComponentCodecVersion,
                descriptorFingerprint);
            ProductionOutputMaximumMassProjection maximumProjection = new(
                descriptor,
                1,
                ExactMassGrams,
                ExactMassGrams,
                MassAuthorityRevision,
                Digest('a'));
            ProductionOutputBatchMaximumMassProof maximumProof = new(new[]
            {
                maximumProjection
            });
            string capacityDigest = tamper == Tamper.Capacity
                ? Digest('d')
                : Digest('c');
            ProductionOutputBufferCapacitySourceSnapshot capacity = new(
                cycleCapacity: 2,
                maximumBatchMassGrams: ExactMassGrams,
                projectedPortfolioCapacityGrams: RequiredCapacityGrams,
                batchMinimumCapacityGrams: ExactMassGrams,
                requiredMinimumCapacityGrams: RequiredCapacityGrams,
                sourceDigest: capacityDigest);

            string commitId = ProductionOutputCommitIdentity.Format(
                (ProductionBillId)BillId,
                1,
                OutputLineId,
                itemId,
                0);
            string componentCommitId = tamper == Tamper.Commit
                ? commitId + ":tampered"
                : commitId;
            SurgicalPartPreparedOutput prepared = new()
            {
                ItemId = itemId,
                PartInstanceId = PartInstanceId,
                NodeId = tamper == Tamper.Node ? "leg:left" : "arm:left",
                DisplayName = "QA left arm prosthetic",
                Kind = tamper == Tamper.Kind
                    ? SurgicalPartKind.Implant
                    : SurgicalPartKind.Prosthetic,
                Quality = tamper == Tamper.Quality ? 1.25f : 1.125f,
                CommitId = componentCommitId,
                ExpectedSequence = 1,
                IsReplay = true
            };
            ItemInstanceComponentSaveData component =
                SurgicalPartPreparedOutputComponentCodec.Create(prepared);
            if (tamper == Tamper.Component)
                component.schemaVersion = 2;
            string componentFingerprint = component.ToCanonicalString();

            ProductionOutputBufferCapacitySourceSnapshot baselineCapacity = new(
                cycleCapacity: 2,
                maximumBatchMassGrams: ExactMassGrams,
                projectedPortfolioCapacityGrams: RequiredCapacityGrams,
                batchMinimumCapacityGrams: ExactMassGrams,
                requiredMinimumCapacityGrams: RequiredCapacityGrams,
                sourceDigest: Digest('c'));
            string baselineComponentFingerprint =
                SurgicalPartPreparedOutputComponentCodec.Create(new
                    SurgicalPartPreparedOutput
                    {
                        ItemId = itemId,
                        PartInstanceId = PartInstanceId,
                        NodeId = "arm:left",
                        DisplayName = "QA left arm prosthetic",
                        Kind = SurgicalPartKind.Prosthetic,
                        Quality = 1.125f,
                        CommitId = commitId,
                        ExpectedSequence = 1,
                        IsReplay = true
                    }).ToCanonicalString();
            string baselineOutcome =
                SurgicalPartProductionOutputSemantics.CreateOutcomeFingerprint(
                    commitId,
                    OutputLineId,
                    itemId,
                    baselineComponentFingerprint,
                    maximumProof,
                    baselineCapacity);
            string physicalOutcome = tamper is Tamper.OutcomeFingerprint
                or Tamper.PairedOutcomeAndPlannedFingerprints
                    ? Digest('e')
                    : baselineOutcome;
            string plannedFingerprint = tamper is Tamper.PlannedOutputFingerprint
                or Tamper.PairedOutcomeAndPlannedFingerprints
                    ? Digest('f')
                    : Digest('b');
            long physicalMass = tamper == Tamper.Mass
                ? ExactMassGrams + 1L
                : ExactMassGrams;
            string stackDestination = tamper == Tamper.Destination
                ? DestinationId + ":tampered"
                : DestinationId;
            string facilityId = tamper == Tamper.Facility
                ? FacilityId + ":tampered"
                : FacilityId;
            string preparedFingerprint =
                tamper == Tamper.PreparedComponentFingerprint
                    ? Digest('9')
                    : componentFingerprint;
            WorldItemStackState state = isPendingPhysical
                ? WorldItemStackState.FacilityOutputBuffer
                : WorldItemStackState.Stored;
            Vector2Int stackPosition = isPendingPhysical
                ? FacilityPosition
                : FacilityPosition + Vector2Int.one;

            FacilityBufferPlannedOutputRestoreStackSnapshot stack = new(
                commitId,
                physicalOutcome,
                plannedFingerprint,
                OutputLineId,
                0,
                StackId,
                itemId,
                1,
                physicalMass,
                ItemStackSignature.Create(itemId, new[] { component }),
                state,
                stackPosition,
                stackDestination,
                PartInstanceId,
                new[] { component },
                preparedFingerprint);
            FacilityBufferPlannedOutputRestoreBatchSnapshot physical = new(
                commitId,
                physicalOutcome,
                plannedFingerprint,
                1,
                physicalMass,
                new[] { stack });
            ProductionResolvedOutputSaveData output = new()
            {
                outputLineId = OutputLineId,
                itemId = itemId,
                outputCapabilityId = descriptor.CapabilityId,
                outputCapabilityVersion = descriptor.CapabilityVersion,
                outputComponentCodecId = descriptor.ComponentCodecId,
                outputComponentCodecVersion = descriptor.ComponentCodecVersion,
                outputCapabilityFingerprint = descriptor.Fingerprint,
                amount = 1,
                pendingCommitId = commitId,
                workerQuality = 1.125f
            };
            ProductionBillSaveData bill = new()
            {
                billId = BillId,
                recipeId = "recipe:qa-surgical-restore",
                buildingInstanceId = FacilityId,
                cycleSequence = 1,
                outputDestinationId = DestinationId,
                resolvedOutputs = new List<ProductionResolvedOutputSaveData>
                {
                    output
                }
            };
            ProductionExactOutputPublicationSaveData envelope = new()
            {
                phase = ProductionExactOutputPublicationPhase.Published,
                ownerStableId = BillId,
                commitId = commitId,
                facilityInstanceId = FacilityId,
                outputCapabilityId = descriptor.CapabilityId,
                outputCapabilityVersion = descriptor.CapabilityVersion,
                outputComponentCodecId = descriptor.ComponentCodecId,
                outputComponentCodecVersion = descriptor.ComponentCodecVersion,
                maximumProofDigest = maximumProof.SourceDigest,
                maximumMassGrams = maximumProof.MaximumBatchMassGrams,
                capacitySourceDigest = baselineCapacity.SourceDigest,
                requiredMinimumCapacityGrams = RequiredCapacityGrams,
                exactMassGrams = ExactMassGrams,
                outcomeFingerprint = baselineOutcome,
                plannedOutputFingerprint = Digest('b'),
                destinationId = DestinationId,
                dropPositionX = FacilityPosition.x,
                dropPositionY = FacilityPosition.y,
                ownerDomain = ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
                ownerOperationId = DestinationId,
                ownerFacilityId = FacilityId,
                capacityRevision =
                    ProductionOutputDestinationAuthorityRuntime.CapacitySchemaRevision,
                acknowledgedAtCapture = !isPendingPhysical
            };
            PureFixedProjectionQuery projection = new(
                Digest('b'), ExactMassGrams);
            SurgicalPartProductionOutputRestoreCapabilityValidator validator =
                new(projection);
            ProductionResolvedOutputRestoreValidationContext context = new(
                bill,
                output,
                descriptor,
                maximumProof,
                new ProductionOutputDetachedFacilityCapacityProjection(
                    facilityId,
                    FacilityPosition,
                    capacity),
                physical,
                isPendingPhysical,
                envelope);
            return new Fixture(validator, projection, context);
        }
    }

    private sealed class PureFixedProjectionQuery :
        IFacilityBufferPlannedOutputProjectionQuery
    {
        private readonly string fingerprint;
        private readonly long exactMassGrams;

        internal PureFixedProjectionQuery(
            string fingerprint,
            long exactMassGrams)
        {
            this.fingerprint = fingerprint;
            this.exactMassGrams = exactMassGrams;
        }

        internal long SemanticMutationRevision => 0L;

        public bool TryProjectPlannedOutput(
            FacilityBufferPlannedOutputRequest request,
            out FacilityBufferPlannedOutputSnapshot planned,
            out FacilityBufferMassAdmissionFailureCode failureCode,
            out string failureReason)
        {
            FacilityBufferPlannedOutputSlice slice = request.Slices.Single();
            FacilityBufferPlannedOutputSliceSnapshot projectedSlice = new(
                slice,
                new PhysicalMassGrams(exactMassGrams));
            planned = new FacilityBufferPlannedOutputSnapshot(
                fingerprint,
                new[] { projectedSlice },
                1,
                new PhysicalMassGrams(exactMassGrams));
            failureCode = FacilityBufferMassAdmissionFailureCode.None;
            failureReason = string.Empty;
            return true;
        }
    }
}
#endif
