#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Kept in the runtime assembly so focused coverage can exercise internal codecs.
public static class
    SurgicalPartProductionOutputCrossAggregateSaveValidationDebugScenarios
{
    private const string OutputLineId = "output:main";
    private const string FacilityId = "building-instance:qa-surgical-output";
    private const string DestinationId =
        "production-output:building-instance:qa-surgical-output";
    private const long OutputMassGrams = 2_400L;

    [MenuItem(
        "DungeonStory/Debug/Medical/Run V21 Surgical Output Restore Cross-Aggregate Contracts")]
    public static void RunAll()
    {
        VerifyCrashAPendingAccepted();
        VerifyCrashBPendingAccepted();
        VerifyCrashBAcknowledgedAccepted();
        VerifyMissingAndDuplicatePartRejected();
        VerifyWrongStackRejected();
        VerifyWrongComponentRejected();
        VerifyWrongNodeKindQualityAndCommitRejected();
        VerifyDuplicatePhysicalPublicationRejected();
        VerifyHistoricalProductionProvenanceAccepted();
        Debug.Log(
            "Surgical-part V21 production-output cross-aggregate save scenarios passed.");
    }

    private static void VerifyCrashAPendingAccepted()
    {
        Fixture fixture = Fixture.Create(1, applied: false, acknowledged: false);
        fixture.Validate();
    }

    private static void VerifyCrashBPendingAccepted()
    {
        Fixture fixture = Fixture.Create(2, applied: true, acknowledged: false);
        fixture.Validate();
    }

    private static void VerifyCrashBAcknowledgedAccepted()
    {
        Fixture fixture = Fixture.Create(3, applied: true, acknowledged: true);
        fixture.Validate();
    }

    private static void VerifyMissingAndDuplicatePartRejected()
    {
        Fixture missing = Fixture.Create(4, applied: false, acknowledged: false);
        missing.Surgery.parts.Clear();
        RequireRejected(missing, "missing or duplicate surgery owner");

        Fixture duplicate = Fixture.Create(5, applied: true, acknowledged: false);
        SurgicalPartInstance second = ClonePart(duplicate.Part);
        second.partInstanceId += ":duplicate";
        duplicate.Surgery.parts.Add(second);
        RequireRejected(duplicate, "missing or duplicate surgery owner");
    }

    private static void VerifyWrongStackRejected()
    {
        Fixture fixture = Fixture.Create(6, applied: true, acknowledged: true);
        fixture.Part.worldStackId = "world-item-stack:qa-surgical-output:other";
        RequireRejected(fixture, "semantic owner does not match");
    }

    private static void VerifyWrongComponentRejected()
    {
        Fixture fixture = Fixture.Create(7, applied: false, acknowledged: false);
        fixture.Stack.components.RemoveAll(component => component != null
            && string.Equals(
                component.componentTypeId,
                SurgicalPartPreparedOutputComponentCodec.ComponentTypeId,
                StringComparison.Ordinal));
        RequireRejected(fixture, "semantic owner does not match");
    }

    private static void VerifyWrongNodeKindQualityAndCommitRejected()
    {
        Fixture wrongNode = Fixture.Create(8, applied: true, acknowledged: false);
        wrongNode.Part.nodeId = "leg:left";
        RequireRejected(wrongNode, "semantic owner does not match");

        Fixture wrongKind = Fixture.Create(9, applied: true, acknowledged: true);
        wrongKind.Part.kind = SurgicalPartKind.Implant;
        RequireRejected(wrongKind, "semantic owner does not match");

        Fixture wrongQuality = Fixture.Create(10, applied: false, acknowledged: false);
        wrongQuality.Part.quality = 1.25f;
        RequireRejected(wrongQuality, "semantic owner does not match");

        Fixture wrongCommit = Fixture.Create(11, applied: true, acknowledged: false);
        ItemStateValueSaveData commitField = wrongCommit.Stack.components
            .Single(component => component != null
                && string.Equals(
                    component.componentTypeId,
                    SurgicalPartPreparedOutputComponentCodec.ComponentTypeId,
                    StringComparison.Ordinal))
            .values.Single(value => value != null
                && string.Equals(
                    value.key,
                    "production-commit-id",
                    StringComparison.Ordinal));
        commitField.stringValue = "production-output:tampered-surgical-commit";
        RequireRejected(wrongCommit, "semantic owner does not match");
    }

    private static void VerifyDuplicatePhysicalPublicationRejected()
    {
        Fixture fixture = Fixture.Create(12, applied: true, acknowledged: true);
        WorldItemStackSaveData duplicate = CloneStack(fixture.Stack);
        duplicate.stackId += ":duplicate";
        duplicate.itemInstanceId += ":duplicate";
        fixture.Physical.stacks.Add(duplicate);
        RequireRejected(fixture, "missing or duplicate physical stack");
    }

    private static void VerifyHistoricalProductionProvenanceAccepted()
    {
        Fixture fixture = Fixture.Create(13, applied: true, acknowledged: true);
        string historicalCommitId = fixture.Output.pendingCommitId;
        fixture.Output.pendingCommitId = string.Empty;
        fixture.Output.pendingCommitApplied = false;
        fixture.Output.pendingOutputPublication =
            ProductionExactOutputPublicationSaveData.Empty();

        Require(
            string.Equals(
                fixture.Part.sourceProductionCommitId,
                historicalCommitId,
                StringComparison.Ordinal),
            "Historical fixture lost its source production provenance.");
        fixture.Validate();
    }

    private static void RequireRejected(Fixture fixture, string messageFragment)
    {
        try
        {
            fixture.Validate();
        }
        catch (InvalidOperationException exception)
        {
            Require(
                exception.Message.IndexOf(
                    messageFragment,
                    StringComparison.OrdinalIgnoreCase) >= 0,
                "Unexpected surgical restore rejection: " + exception.Message);
            return;
        }

        throw new InvalidOperationException(
            "Surgical restore tamper was accepted: " + messageFragment);
    }

    private static SurgicalPartInstance ClonePart(SurgicalPartInstance source) =>
        new()
        {
            partInstanceId = source.partInstanceId,
            kind = source.kind,
            nodeId = source.nodeId,
            displayName = source.displayName,
            quality = source.quality,
            worldStackId = source.worldStackId,
            sourceProductionCommitId = source.sourceProductionCommitId
        };

    private static WorldItemStackSaveData CloneStack(WorldItemStackSaveData source) =>
        new()
        {
            stackId = source.stackId,
            itemInstanceId = source.itemInstanceId,
            itemId = source.itemId,
            quantity = source.quantity,
            state = source.state,
            gridX = source.gridX,
            gridY = source.gridY,
            destinationId = source.destinationId,
            components = (source.components
                    ?? new List<ItemInstanceComponentSaveData>())
                .Where(value => value != null)
                .Select(value => value.Clone())
                .ToList()
        };

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        private Fixture(
            DungeonProductionBillSaveData production,
            DungeonPhysicalItemSaveData physical,
            DungeonSurgerySaveData surgery,
            ProductionResolvedOutputSaveData output,
            WorldItemStackSaveData stack,
            SurgicalPartInstance part)
        {
            Production = production;
            Physical = physical;
            Surgery = surgery;
            Output = output;
            Stack = stack;
            Part = part;
        }

        internal DungeonProductionBillSaveData Production { get; }
        internal DungeonPhysicalItemSaveData Physical { get; }
        internal DungeonSurgerySaveData Surgery { get; }
        internal ProductionResolvedOutputSaveData Output { get; }
        internal WorldItemStackSaveData Stack { get; }
        internal SurgicalPartInstance Part { get; }

        internal static Fixture Create(
            int ordinal,
            bool applied,
            bool acknowledged)
        {
            string suffix = ordinal.ToString("D2");
            string billId = "production-bill:qa-surgical-output-" + suffix;
            string itemId =
                SurgicalPartProductionOutputHandler.ProstheticArmOutputId;
            string commitId = ProductionOutputCommitIdentity.Format(
                (ProductionBillId)billId,
                1,
                OutputLineId,
                itemId,
                0);
            string stackId = "world-item-stack:qa-surgical-output-" + suffix;
            string itemInstanceId =
                "item-instance:qa-surgical-output-" + suffix;
            string partId = "surgical-part:qa-output-" + suffix;
            string outcomeFingerprint =
                SurgicalPartPreparedOutputComponentCodec.Hash(
                    "outcome:" + suffix);
            string plannedFingerprint =
                SurgicalPartPreparedOutputComponentCodec.Hash(
                    "planned:" + suffix);

            SurgicalPartPreparedOutput prepared = new()
            {
                ItemId = itemId,
                PartInstanceId = partId,
                NodeId = "arm:left",
                DisplayName = "QA prosthetic arm " + suffix,
                Kind = SurgicalPartKind.Prosthetic,
                Quality = 1.125f,
                CommitId = commitId,
                ExpectedSequence = ordinal,
                IsReplay = true
            };
            ItemInstanceComponentSaveData surgicalComponent =
                SurgicalPartPreparedOutputComponentCodec.Create(prepared);
            string preparedComponentFingerprint =
                SurgicalPartPreparedOutputComponentCodec.Hash(
                    surgicalComponent.ToCanonicalString());
            ItemInstanceComponentSaveData publicationComponent =
                PlannedOutputPublicationComponentCodec.CreatePublication(
                    commitId,
                    outcomeFingerprint,
                    plannedFingerprint,
                    OutputLineId,
                    0,
                    1,
                    1,
                    OutputMassGrams,
                    1,
                    1,
                    OutputMassGrams,
                    itemId,
                    1,
                    OutputMassGrams,
                    surgicalComponent.ToCanonicalString(),
                    preparedComponentFingerprint);
            if (acknowledged)
            {
                Require(
                    PlannedOutputPublicationComponentCodec.TryRead(
                        new[] { publicationComponent },
                        out PlannedOutputPublicationMetadata metadata),
                    "Failed to materialize the current publication marker DTO.");
                publicationComponent =
                    PlannedOutputPublicationComponentCodec.CreateProvenance(
                        metadata);
            }

            List<ItemInstanceComponentSaveData> components = new()
            {
                surgicalComponent,
                publicationComponent
            };
            WorldItemStackSaveData stack = new()
            {
                stackId = stackId,
                itemInstanceId = itemInstanceId,
                itemId = itemId,
                quantity = 1,
                state = WorldItemStackState.FacilityOutputBuffer,
                gridX = 4,
                gridY = 5,
                destinationId = DestinationId,
                hasDestinationPosition = true,
                destinationGridX = 4,
                destinationGridY = 5,
                components = components
            };
            SurgicalPartInstance part = new()
            {
                partInstanceId = partId,
                kind = SurgicalPartKind.Prosthetic,
                nodeId = "arm:left",
                displayName = prepared.DisplayName,
                quality = prepared.Quality,
                worldStackId = stackId,
                sourceProductionCommitId = commitId
            };

            string capabilityFingerprint =
                ProductionOutputCapabilityDescriptorFingerprint.Capture(
                    OutputLineId,
                    itemId,
                    SurgicalPartProductionOutputHandler.HandlerCapabilityId,
                    SurgicalPartProductionOutputHandler.HandlerContractVersion,
                    SurgicalPartProductionOutputHandler.HandlerComponentCodecId,
                    SurgicalPartProductionOutputHandler.HandlerComponentCodecVersion);
            ProductionExactOutputPublicationSaveData envelope = applied
                ? new ProductionExactOutputPublicationSaveData
                {
                    phase = ProductionExactOutputPublicationPhase.Published,
                    ownerStableId = billId,
                    commitId = commitId,
                    facilityInstanceId = FacilityId,
                    outputCapabilityId =
                        SurgicalPartProductionOutputHandler.HandlerCapabilityId,
                    outputCapabilityVersion =
                        SurgicalPartProductionOutputHandler.HandlerContractVersion,
                    outputComponentCodecId =
                        SurgicalPartProductionOutputHandler.HandlerComponentCodecId,
                    outputComponentCodecVersion =
                        SurgicalPartProductionOutputHandler.HandlerComponentCodecVersion,
                    maximumProofDigest =
                        SurgicalPartPreparedOutputComponentCodec.Hash(
                            "maximum:" + suffix),
                    maximumMassGrams = OutputMassGrams,
                    capacitySourceDigest =
                        SurgicalPartPreparedOutputComponentCodec.Hash(
                            "capacity:" + suffix),
                    requiredMinimumCapacityGrams = OutputMassGrams * 2L,
                    exactMassGrams = OutputMassGrams,
                    outcomeFingerprint = outcomeFingerprint,
                    plannedOutputFingerprint = plannedFingerprint,
                    destinationId = DestinationId,
                    dropPositionX = 4,
                    dropPositionY = 5,
                    ownerDomain = "production-output-buffer",
                    ownerOperationId = "production-output-owner:" + billId,
                    ownerFacilityId = FacilityId,
                    capacityRevision = 1L,
                    acknowledgedAtCapture = acknowledged,
                    stacks = new List<
                        ProductionExactOutputPublicationStackSaveData>
                    {
                        new()
                        {
                            outputLineId = OutputLineId,
                            stackOrdinal = 0,
                            stackId = stackId,
                            itemId = itemId,
                            quantity = 1,
                            massGrams = OutputMassGrams,
                            componentSignature = ItemStackSignature.Create(
                                itemId,
                                components),
                            itemInstanceId = itemInstanceId
                        }
                    }
                }
                : ProductionExactOutputPublicationSaveData.Empty();
            ProductionResolvedOutputSaveData output = new()
            {
                outputLineId = OutputLineId,
                itemId = itemId,
                outputCapabilityId =
                    SurgicalPartProductionOutputHandler.HandlerCapabilityId,
                outputCapabilityVersion =
                    SurgicalPartProductionOutputHandler.HandlerContractVersion,
                outputComponentCodecId =
                    SurgicalPartProductionOutputHandler.HandlerComponentCodecId,
                outputComponentCodecVersion =
                    SurgicalPartProductionOutputHandler.HandlerComponentCodecVersion,
                outputCapabilityFingerprint = capabilityFingerprint,
                amount = 1,
                committedAmount = applied ? 1 : 0,
                committedMassGrams = applied ? OutputMassGrams : 0L,
                pendingCommitId = commitId,
                pendingCommitApplied = applied,
                pendingOutputPublication = envelope,
                qualityModifier = 1f,
                workerQuality = prepared.Quality
            };
            ProductionBillSaveData bill = new()
            {
                billId = billId,
                recipeId = "recipe:qa-surgical-output",
                buildingInstanceId = FacilityId,
                cycleSequence = 1,
                outputOutcomeResolved = true,
                outputDestinationId = DestinationId,
                resolvedOutputs = new List<ProductionResolvedOutputSaveData>
                {
                    output
                }
            };
            DungeonProductionBillSaveData production = new()
            {
                version = DungeonProductionBillSaveData.CurrentVersion,
                bills = new List<ProductionBillSaveData> { bill }
            };
            DungeonPhysicalItemSaveData physical = new()
            {
                version = DungeonPhysicalItemSaveData.CurrentVersion,
                stacks = new List<WorldItemStackSaveData> { stack }
            };
            DungeonSurgerySaveData surgery = new()
            {
                version = DungeonSurgerySaveData.CurrentVersion,
                parts = new List<SurgicalPartInstance> { part },
                partSequence = ordinal + 1
            };
            return new Fixture(production, physical, surgery, output, stack, part);
        }

        internal void Validate() =>
            SurgicalPartProductionOutputCrossAggregateSaveValidation
                .ValidateIfRequired(Production, Physical, Surgery);
    }
}
#endif
