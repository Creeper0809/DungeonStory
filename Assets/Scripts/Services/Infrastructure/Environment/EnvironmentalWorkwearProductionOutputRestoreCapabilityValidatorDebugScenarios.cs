#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class
    EnvironmentalWorkwearProductionOutputRestoreCapabilityValidatorDebugScenarios
{
    [MenuItem(
        "DungeonStory/Debug/Environment/Run Workwear Output Restore Capability Contracts")]
    public static void RunAll()
    {
        VerifyCrashCandidatesAccepted();
        VerifyOutcomeAndPlannedFingerprintTamperRejected();
        VerifyCatalogAndQualityTamperRejected();
        VerifyComponentDayAndHashTamperRejected();
        VerifyMassFacilityDestinationAndCapacityTamperRejected();
        VerifyLateRowFailureIsReadOnly();
        Debug.Log(
            "Environmental workwear output restore capability scenarios passed.");
    }

    private static void VerifyCrashCandidatesAccepted()
    {
        using Fixture crashA = new();
        OwnerCase pending = crashA.CreateOwner(1, applied: false);
        crashA.CreateJoin(new[] { pending.Physical }, Array.Empty<
                FacilityBufferPlannedOutputRestoreBatchSnapshot>())
            .Validate(Payload(pending));
        RequireNoExecution(crashA.Handler);

        using Fixture crashB = new();
        OwnerCase acknowledged = crashB.CreateOwner(2, applied: true);
        crashB.CreateJoin(Array.Empty<
                FacilityBufferPlannedOutputRestoreBatchSnapshot>(),
                new[] { acknowledged.Physical })
            .Validate(Payload(acknowledged));
        RequireNoExecution(crashB.Handler);
    }

    private static void VerifyOutcomeAndPlannedFingerprintTamperRejected()
    {
        using Fixture outcomeFixture = new();
        OwnerCase outcome = outcomeFixture.CreateOwner(3, applied: false);
        FacilityBufferPlannedOutputRestoreBatchSnapshot outcomeTamper =
            ClonePhysical(outcome.Physical, outcomeFingerprint: Digest('e'));
        RequireThrows(
            () => outcomeFixture.ValidateDirect(outcome, outcomeTamper),
            "physical-batch-mismatch");

        using Fixture plannedFixture = new();
        OwnerCase planned = plannedFixture.CreateOwner(4, applied: false);
        FacilityBufferPlannedOutputRestoreBatchSnapshot plannedTamper =
            ClonePhysical(
                planned.Physical,
                plannedOutputFingerprint: Digest('f'));
        RequireThrows(
            () => plannedFixture.ValidateDirect(planned, plannedTamper),
            "planned-output-fingerprint-mismatch");
    }

    private static void VerifyCatalogAndQualityTamperRejected()
    {
        using Fixture recipeFixture = new();
        OwnerCase recipe = recipeFixture.CreateOwner(5, applied: false);
        recipe.Bill.recipeId = "recipe:qa:missing-workwear";
        RequireThrows(
            () => recipeFixture.ValidateDirect(recipe),
            "recipe-authority-missing");

        using Fixture materialFixture = new(materialAvailable: false);
        OwnerCase material = materialFixture.CreateOwner(6, applied: false);
        RequireThrows(
            () => materialFixture.ValidateDirect(material),
            "material-authority-invalid");

        using Fixture qualityFixture = new();
        OwnerCase quality = qualityFixture.CreateOwner(7, applied: false);
        quality.Output.qualityModifier = 0.8f;
        RequireThrows(
            () => qualityFixture.ValidateDirect(quality),
            "physical-batch-mismatch");
    }

    private static void VerifyComponentDayAndHashTamperRejected()
    {
        using Fixture componentFixture = new();
        OwnerCase component = componentFixture.CreateOwner(8, applied: false);
        ItemInstanceComponentSaveData unexpectedComponent = new()
        {
            componentTypeId = "qa:unexpected-workwear-state",
            schemaVersion = 1,
            affectsStacking = true,
            values = new List<ItemStateValueSaveData>()
        };
        FacilityBufferPlannedOutputRestoreBatchSnapshot componentTamper =
            componentFixture.RebuildPhysical(
                component,
                new[] { component.Component, unexpectedComponent });
        RequireThrows(
            () => componentFixture.ValidateDirect(component, componentTamper),
            "apparel-component-count-mismatch");

        using Fixture dayFixture = new();
        OwnerCase day = dayFixture.CreateOwner(9, applied: false);
        ApparelInstanceState dayState = ReadState(day.Component);
        dayState.craftedAbsoluteDay++;
        FacilityBufferPlannedOutputRestoreBatchSnapshot dayTamper =
            dayFixture.RebuildPhysical(
                day,
                ApparelItemStateCodec.Create(dayState));
        RequireThrows(
            () => dayFixture.ValidateDirect(day, dayTamper),
            "apparel-deterministic-state-mismatch");

        using Fixture hashFixture = new();
        OwnerCase hash = hashFixture.CreateOwner(10, applied: false);
        ApparelInstanceState hashState = ReadState(hash.Component);
        hashState.deterministicBatchHash ^= 1UL;
        FacilityBufferPlannedOutputRestoreBatchSnapshot hashTamper =
            hashFixture.RebuildPhysical(
                hash,
                ApparelItemStateCodec.Create(hashState));
        RequireThrows(
            () => hashFixture.ValidateDirect(hash, hashTamper),
            "apparel-deterministic-state-mismatch");
    }

    private static void VerifyMassFacilityDestinationAndCapacityTamperRejected()
    {
        using Fixture massFixture = new();
        OwnerCase mass = massFixture.CreateOwner(11, applied: false);
        FacilityBufferPlannedOutputRestoreBatchSnapshot massTamper =
            ClonePhysical(
                mass.Physical,
                totalMassGrams: Fixture.UnitMassGrams + 1L,
                stackMassGrams: Fixture.UnitMassGrams + 1L);
        RequireThrows(
            () => massFixture.ValidateDirect(mass, massTamper),
            "exact-mass-mismatch");

        using Fixture facilityFixture = new();
        OwnerCase facility = facilityFixture.CreateOwner(12, applied: false);
        ProductionOutputDetachedFacilityCapacityProjection wrongFacility = new(
            "building:qa:wrong-workwear",
            Fixture.Position,
            facility.Capacity);
        RequireThrows(
            () => facilityFixture.ValidateDirect(
                facility,
                facility.Physical,
                wrongFacility),
            "facility-authority-mismatch");

        using Fixture destinationFixture = new();
        OwnerCase destination = destinationFixture.CreateOwner(
            13,
            applied: false);
        FacilityBufferPlannedOutputRestoreBatchSnapshot destinationTamper =
            ClonePhysical(
                destination.Physical,
                destinationId: "production-output:building:qa:wrong-workwear");
        RequireThrows(
            () => destinationFixture.ValidateDirect(
                destination,
                destinationTamper),
            "pending-physical-location-mismatch");

        using Fixture capacityFixture = new();
        OwnerCase capacity = capacityFixture.CreateOwner(14, applied: true);
        capacity.Output.pendingOutputPublication.capacitySourceDigest =
            Digest('c');
        RequireThrows(
            () => capacityFixture.CreateJoin(
                    Array.Empty<
                        FacilityBufferPlannedOutputRestoreBatchSnapshot>(),
                    new[] { capacity.Physical })
                .Validate(Payload(capacity)),
            "Detached workwear capacity drifted");
    }

    private static void VerifyLateRowFailureIsReadOnly()
    {
        using Fixture fixture = new();
        OwnerCase first = fixture.CreateOwner(20, applied: true);
        OwnerCase late = fixture.CreateOwner(21, applied: true);
        ApparelInstanceState state = ReadState(late.Component);
        state.deterministicBatchHash ^= 1UL;
        late.Component = ApparelItemStateCodec.Create(state);
        late.Physical = fixture.RebuildPhysical(late, late.Component);
        fixture.SynchronizeEnvelope(late);

        DungeonProductionBillSaveData payload = Payload(first, late);
        string before = JsonUtility.ToJson(payload);
        RequireThrows(
            () => fixture.CreateJoin(
                    Array.Empty<
                        FacilityBufferPlannedOutputRestoreBatchSnapshot>(),
                    new[] { first.Physical, late.Physical })
                .Validate(payload),
            "apparel-deterministic-state-mismatch");
        Require(string.Equals(
                before,
                JsonUtility.ToJson(payload),
                StringComparison.Ordinal),
            "A late workwear semantic failure mutated the Production DTO.");
        RequireNoExecution(fixture.Handler);
    }

    private static DungeonProductionBillSaveData Payload(
        params OwnerCase[] owners) => new()
    {
        version = DungeonProductionBillSaveData.CurrentVersion,
        nextBillSequence = owners.Length + 1,
        bills = owners.Select(value => value.Bill).ToList()
    };

    private static FacilityBufferPlannedOutputRestoreBatchSnapshot
        ClonePhysical(
            FacilityBufferPlannedOutputRestoreBatchSnapshot source,
            string outcomeFingerprint = null,
            string plannedOutputFingerprint = null,
            long? totalMassGrams = null,
            long? stackMassGrams = null,
            string destinationId = null)
    {
        FacilityBufferPlannedOutputRestoreStackSnapshot stack =
            source.Stacks.Single();
        string outcome = outcomeFingerprint ?? source.OutcomeFingerprint;
        string planned = plannedOutputFingerprint
            ?? source.PlannedOutputFingerprint;
        FacilityBufferPlannedOutputRestoreStackSnapshot cloned = new(
            stack.BatchCommitId,
            outcome,
            planned,
            stack.OutputLineId,
            stack.StackOrdinal,
            stack.StackId,
            stack.ItemId,
            stack.Quantity,
            stackMassGrams ?? stack.MassGrams,
            stack.ComponentSignature,
            stack.State,
            stack.Position,
            destinationId ?? stack.DestinationId,
            stack.ItemInstanceId,
            stack.Components,
            stack.PreparedComponentFingerprint);
        return new FacilityBufferPlannedOutputRestoreBatchSnapshot(
            source.BatchCommitId,
            outcome,
            planned,
            source.TotalQuantity,
            totalMassGrams ?? source.TotalMassGrams,
            new[] { cloned });
    }

    private static ApparelInstanceState ReadState(
        ItemInstanceComponentSaveData component)
    {
        Require(ApparelItemStateCodec.TryRead(
                new[] { component },
                out ApparelInstanceState state)
            && state != null,
            "Fixture apparel component could not be decoded.");
        return state;
    }

    private static void RequireNoExecution(WorkwearHandlerProbe handler) =>
        Require(handler.ProduceCount == 0
                && handler.AcknowledgeCount == 0
                && handler.CaptureCount == 0,
            "Restore validation executed or acknowledged workwear output.");

    private static void RequireThrows(Action action, string token)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException exception)
        {
            Require(exception.Message.Contains(token, StringComparison.Ordinal),
                "Wrong restore rejection. Expected '"
                + token
                + "', actual: "
                + exception.Message);
            return;
        }
        throw new InvalidOperationException(
            "Expected workwear restore rejection was not observed: " + token);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private static string Digest(char value) => new(value, 64);

    private sealed class Fixture : IDisposable
    {
        internal const string ItemId = "item:qa:restore-workwear";
        internal const string TextileItemId = "item:qa:restore-textile";
        internal const string RecipeId = "recipe:qa:restore-workwear";
        internal const string OutputLineId = "output:qa/restore-workwear";
        internal const string FacilityId = "building:qa:restore-workwear";
        internal const long UnitMassGrams = 1_150L;
        internal const int CraftedDay = 37;
        internal static readonly Vector2Int Position = new(9, 6);
        private static readonly string DestinationId =
            ProductionOutputDestinationId
                .FromFacility((BuildingInstanceId)FacilityId)
                .Value;

        private readonly ApparelDefinitionSO apparel;
        private readonly TextileMaterialDefinitionSO material;
        private readonly ProductionRecipeSO recipe;
        private readonly FixedMassQuery mass;
        private readonly FacilityBufferMassAdmissionService projection;
        private readonly ProductionOutputHandlerRegistry capabilities;
        private readonly ProductionOutputMaximumMassRegistry maximumMass;
        private readonly EnvironmentalWorkwearProductionOutputRestoreCapabilityValidator
            validator;
        private readonly FixedCapacityAuthority capacityAuthority;
        private readonly ProductionResolvedOutputRestoreCapabilityValidatorRegistry
            validators;

        internal Fixture(bool materialAvailable = true)
        {
            apparel = ScriptableObject.CreateInstance<ApparelDefinitionSO>();
            apparel.Configure(
                "apparel:qa:restore-workwear",
                ItemId,
                "QA Restore Workwear",
                string.Empty,
                ApparelBodyForm.Humanoid,
                ApparelLayer.Accessory,
                ApparelFitMode.Adjustable,
                AnatomyAttachmentPoint.Torso,
                AnatomyAttachmentPoint.Torso,
                AnatomyAttachmentPoint.None,
                ApparelModificationKind.None,
                ApparelUseTag.Work,
                TextileMaterialTag.Woven | TextileMaterialTag.Plant,
                1f,
                1.15f,
                string.Empty);
            material = ScriptableObject.CreateInstance<
                TextileMaterialDefinitionSO>();
            material.Configure(
                "textile:qa:restore-plant",
                TextileItemId,
                "QA Restore Textile",
                string.Empty,
                TextileMaterialTag.Woven | TextileMaterialTag.Plant,
                0.4f,
                0.3f,
                0.2f,
                0.1f,
                0.1f,
                50f,
                1f,
                1f,
                string.Empty);
            recipe = ScriptableObject.CreateInstance<ProductionRecipeSO>();
            recipe.Configure(
                RecipeId,
                "QA Restore Workwear",
                string.Empty,
                "facility:qa:tailoring",
                "work:craft",
                string.Empty,
                1f,
                new[] { new ItemAmountDefinition(TextileItemId, 2) },
                new[]
                {
                    new ProductionOutputDefinition(
                        OutputLineId,
                        ProductionOutputRole.Main,
                        ItemId,
                        1,
                        1f)
                });

            FixedApparelCatalog apparelCatalog = new(apparel);
            FixedMaterialCatalog materialCatalog = new(
                material,
                materialAvailable);
            mass = new FixedMassQuery();
            projection = new FacilityBufferMassAdmissionService(
                new FacilityBufferDestinationClaimRegistry(),
                new EmptyOccupancy(),
                mass);
            validator = new
                EnvironmentalWorkwearProductionOutputRestoreCapabilityValidator(
                    new FixedEconomyCatalog(recipe),
                    apparelCatalog,
                    materialCatalog,
                    mass,
                    projection);
            Handler = new WorkwearHandlerProbe(apparelCatalog);
            capabilities = new ProductionOutputHandlerRegistry(new
                IProductionOutputCapability[]
                {
                    new StandardCapability(),
                    Handler
                });
            maximumMass = new ProductionOutputMaximumMassRegistry(new
                IProductionOutputMaximumMassCapability[]
                {
                    new StandardMaximumMassCapability(),
                    new
                        EnvironmentalWorkwearProductionOutputMaximumMassCapability(
                            apparelCatalog)
                },
                mass);
            capacityAuthority = new FixedCapacityAuthority();
            validators = new
                ProductionResolvedOutputRestoreCapabilityValidatorRegistry(
                    new[]
                    {
                        validator
                    });
        }

        internal WorkwearHandlerProbe Handler { get; }

        internal OwnerCase CreateOwner(int ordinal, bool applied)
        {
            string suffix = ordinal.ToString("D2");
            string billId = "production-bill:qa-workwear-restore-" + suffix;
            ProductionOutputCapabilityDescriptor descriptor = capabilities
                .CaptureDeclaredDescriptor(
                    OutputLineId,
                    ItemId,
                    Handler.CapabilityId);
            ProductionOutputBatchMaximumMassProof proof = new(new[]
            {
                maximumMass.CaptureDeclared(descriptor, 1)
            });
            string commitId = ProductionOutputCommitIdentity.Format(
                (ProductionBillId)billId,
                1,
                OutputLineId,
                ItemId,
                0);
            ProductionResolvedOutputSaveData output = new()
            {
                outputLineId = OutputLineId,
                itemId = ItemId,
                outputCapabilityId = descriptor.CapabilityId,
                outputCapabilityVersion = descriptor.CapabilityVersion,
                outputComponentCodecId = descriptor.ComponentCodecId,
                outputComponentCodecVersion = descriptor.ComponentCodecVersion,
                outputCapabilityFingerprint = descriptor.Fingerprint,
                amount = 1,
                committedAmount = applied ? 1 : 0,
                committedMassGrams = applied ? UnitMassGrams : 0L,
                pendingCommitId = commitId,
                pendingCommitApplied = applied,
                qualityModifier = 0.2f,
                workerQuality = 0.7f,
                pendingOutputPublication =
                    ProductionExactOutputPublicationSaveData.Empty()
            };
            ProductionBillSaveData bill = new()
            {
                billId = billId,
                recipeId = RecipeId,
                buildingInstanceId = FacilityId,
                cycleSequence = 1,
                outputDestinationId = DestinationId,
                resolvedOutputs = new List<ProductionResolvedOutputSaveData>
                {
                    output
                }
            };
            ItemInstanceComponentSaveData component = ApparelItemStateCodec.Create(
                new ApparelInstanceState
                {
                    apparelDefinitionId = apparel.ApparelId,
                    primaryMaterialId = material.MaterialId,
                    craftsmanshipQuality =
                        EnvironmentalWorkwearProductionOutputSemantics
                            .ResolveCraftsmanship(output.qualityModifier),
                    sourceKind = EnvironmentalWorkwearProductionOutputSemantics
                        .ResolveSourceKind(material.Tags),
                    sourceDefinitionId = material.MaterialId,
                    size = ApparelSizeClass.Medium,
                    modifications = ApparelModificationKind.None,
                    closedOpenings = ApparelModificationKind.None,
                    durability = 100f,
                    moisture = 0f,
                    contamination = 0f,
                    craftedAbsoluteDay = CraftedDay,
                    deterministicBatchHash =
                        EnvironmentalWorkwearProductionOutputSemantics
                            .DeterministicHash(
                                RecipeId,
                                FacilityId,
                                commitId,
                                CraftedDay,
                                0)
                });
            OwnerCase owner = new(
                bill,
                output,
                descriptor,
                proof,
                capacityAuthority.Capacity,
                component,
                "item-instance:qa-workwear-restore-" + suffix,
                "stack:qa-workwear-restore-" + suffix);
            owner.Physical = RebuildPhysical(owner, component, applied);
            if (applied)
                SynchronizeEnvelope(owner);
            return owner;
        }

        internal FacilityBufferPlannedOutputRestoreBatchSnapshot RebuildPhysical(
            OwnerCase owner,
            ItemInstanceComponentSaveData component,
            bool? acknowledged = null) => RebuildPhysical(
            owner,
            new[] { component },
            acknowledged);

        internal FacilityBufferPlannedOutputRestoreBatchSnapshot RebuildPhysical(
            OwnerCase owner,
            IReadOnlyList<ItemInstanceComponentSaveData> components,
            bool? acknowledged = null)
        {
            ItemInstanceComponentSaveData component = (components
                    ?? Array.Empty<ItemInstanceComponentSaveData>())
                .Single(value => value != null
                    && string.Equals(
                        value.componentTypeId,
                        ItemInstanceComponentIds.Apparel,
                        StringComparison.Ordinal));
            string preparedFingerprint =
                EnvironmentalWorkwearProductionOutputSemantics
                    .HashCanonicalComponent(component.ToCanonicalString());
            PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
                mass,
                (ItemDefinitionId)ItemId,
                owner.ItemInstanceId,
                components);
            string outcome = EnvironmentalWorkwearProductionOutputSemantics
                .CreateOutcomeFingerprint(
                    owner.Output.pendingCommitId,
                    OutputLineId,
                    ItemId,
                    1,
                    DestinationId,
                    RecipeId,
                    FacilityId,
                    material.MaterialId,
                    owner.Proof,
                    owner.Capacity,
                    owner.Output.qualityModifier,
                    owner.Output.workerQuality);
            string unitLine = EnvironmentalWorkwearProductionOutputSemantics
                .FormatUnitOutputLineId(OutputLineId, 0);
            FacilityBufferPlannedOutputRequest request = new(
                EnvironmentalWorkwearProductionOutputSemantics
                    .PublicationOperationPrefix + owner.Output.pendingCommitId,
                owner.Output.pendingCommitId,
                outcome,
                DestinationId,
                Position,
                ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
                DestinationId,
                FacilityId,
                ProductionOutputDestinationAuthorityRuntime
                    .CapacitySchemaRevision,
                new[]
                {
                    new FacilityBufferPlannedOutputSlice(
                        unitLine,
                        subject,
                        1,
                        components,
                        preparedFingerprint)
                },
                owner.Capacity.SourceDigest,
                owner.Capacity.RequiredMinimumCapacityGrams);
            Require(projection.TryProjectPlannedOutput(
                    request,
                    out FacilityBufferPlannedOutputSnapshot planned,
                    out _,
                    out string failure),
                "Fixture planned output projection failed: " + failure);
            bool isAcknowledged = acknowledged ?? owner.Output.pendingCommitApplied;
            FacilityBufferPlannedOutputRestoreStackSnapshot stack = new(
                owner.Output.pendingCommitId,
                outcome,
                planned.Fingerprint,
                unitLine,
                0,
                owner.StackId,
                ItemId,
                1,
                UnitMassGrams,
                FacilityBufferPlannedOutputPublicationService
                    .CreateRuntimeComponentSignature(components),
                isAcknowledged
                    ? WorldItemStackState.Stored
                    : WorldItemStackState.FacilityOutputBuffer,
                isAcknowledged ? new Vector2Int(3, 2) : Position,
                isAcknowledged ? string.Empty : DestinationId,
                owner.ItemInstanceId,
                components,
                preparedFingerprint);
            return new FacilityBufferPlannedOutputRestoreBatchSnapshot(
                owner.Output.pendingCommitId,
                outcome,
                planned.Fingerprint,
                1,
                UnitMassGrams,
                new[] { stack });
        }

        internal void SynchronizeEnvelope(OwnerCase owner)
        {
            FacilityBufferPlannedOutputRestoreStackSnapshot stack =
                owner.Physical.Stacks.Single();
            owner.Output.pendingOutputPublication = new
                ProductionExactOutputPublicationSaveData
            {
                phase = ProductionExactOutputPublicationPhase.Published,
                ownerStableId = owner.Bill.billId,
                commitId = owner.Output.pendingCommitId,
                facilityInstanceId = FacilityId,
                outputCapabilityId = owner.Descriptor.CapabilityId,
                outputCapabilityVersion = owner.Descriptor.CapabilityVersion,
                outputComponentCodecId = owner.Descriptor.ComponentCodecId,
                outputComponentCodecVersion =
                    owner.Descriptor.ComponentCodecVersion,
                maximumProofDigest = owner.Proof.SourceDigest,
                maximumMassGrams = owner.Proof.MaximumBatchMassGrams,
                capacitySourceDigest = owner.Capacity.SourceDigest,
                requiredMinimumCapacityGrams =
                    owner.Capacity.RequiredMinimumCapacityGrams,
                exactMassGrams = owner.Physical.TotalMassGrams,
                outcomeFingerprint = owner.Physical.OutcomeFingerprint,
                plannedOutputFingerprint =
                    owner.Physical.PlannedOutputFingerprint,
                destinationId = DestinationId,
                dropPositionX = Position.x,
                dropPositionY = Position.y,
                ownerDomain =
                    ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
                ownerOperationId = DestinationId,
                ownerFacilityId = FacilityId,
                capacityRevision = ProductionOutputDestinationAuthorityRuntime
                    .CapacitySchemaRevision,
                acknowledgedAtCapture = true,
                stacks = new List<ProductionExactOutputPublicationStackSaveData>
                {
                    new()
                    {
                        outputLineId = stack.OutputLineId,
                        stackOrdinal = stack.StackOrdinal,
                        stackId = stack.StackId,
                        itemId = stack.ItemId,
                        quantity = stack.Quantity,
                        massGrams = stack.MassGrams,
                        componentSignature = stack.ComponentSignature,
                        itemInstanceId = stack.ItemInstanceId
                    }
                }
            };
        }

        internal void ValidateDirect(
            OwnerCase owner,
            FacilityBufferPlannedOutputRestoreBatchSnapshot physical = null,
            ProductionOutputDetachedFacilityCapacityProjection? facility = null)
        {
            validator.Validate(new ProductionResolvedOutputRestoreValidationContext(
                owner.Bill,
                owner.Output,
                owner.Descriptor,
                owner.Proof,
                facility ?? capacityAuthority.Capture(
                    owner.Bill.billId,
                    owner.Bill.buildingInstanceId,
                    owner.Proof),
                physical ?? owner.Physical,
                !owner.Output.pendingCommitApplied,
                owner.Output.pendingOutputPublication));
        }

        internal ProductionExactCapabilityOutputRestoreJoin CreateJoin(
            IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot> pending,
            IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot>
                acknowledged) => new(
            new FixedBatchQuery(pending),
            new FixedBatchQuery(acknowledged),
            capabilities,
            maximumMass,
            capacityAuthority,
            capacityAuthority,
            validators);

        public void Dispose()
        {
            UnityEngine.Object.DestroyImmediate(recipe);
            UnityEngine.Object.DestroyImmediate(material);
            UnityEngine.Object.DestroyImmediate(apparel);
        }
    }

    private sealed class OwnerCase
    {
        internal OwnerCase(
            ProductionBillSaveData bill,
            ProductionResolvedOutputSaveData output,
            ProductionOutputCapabilityDescriptor descriptor,
            ProductionOutputBatchMaximumMassProof proof,
            ProductionOutputBufferCapacitySourceSnapshot capacity,
            ItemInstanceComponentSaveData component,
            string itemInstanceId,
            string stackId)
        {
            Bill = bill;
            Output = output;
            Descriptor = descriptor;
            Proof = proof;
            Capacity = capacity;
            Component = component;
            ItemInstanceId = itemInstanceId;
            StackId = stackId;
        }

        internal ProductionBillSaveData Bill { get; }
        internal ProductionResolvedOutputSaveData Output { get; }
        internal ProductionOutputCapabilityDescriptor Descriptor { get; }
        internal ProductionOutputBatchMaximumMassProof Proof { get; }
        internal ProductionOutputBufferCapacitySourceSnapshot Capacity { get; }
        internal ItemInstanceComponentSaveData Component { get; set; }
        internal string ItemInstanceId { get; }
        internal string StackId { get; }
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
            FacilityBufferPlannedOutputRestoreBatchSnapshot> byCommit;

        internal FixedBatchQuery(
            IReadOnlyList<FacilityBufferPlannedOutputRestoreBatchSnapshot> source)
        {
            FacilityBufferPlannedOutputRestoreBatchSnapshot[] values =
                (source ?? Array.Empty<
                    FacilityBufferPlannedOutputRestoreBatchSnapshot>())
                .ToArray();
            batches = Array.AsReadOnly(values);
            byCommit = values.ToDictionary(
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
            byCommit.TryGetValue(batchCommitId ?? string.Empty, out batch);
    }

    private sealed class FixedCapacityAuthority :
        IProductionOutputDetachedFacilityCapacityRestoreGuard,
        IProductionOutputDetachedFacilityCapacityProjectionQuery
    {
        internal FixedCapacityAuthority()
        {
            Capacity = new ProductionOutputBufferCapacitySourceSnapshot(
                2,
                Fixture.UnitMassGrams,
                Fixture.UnitMassGrams * 2L,
                Fixture.UnitMassGrams * 2L,
                Fixture.UnitMassGrams * 2L,
                Digest('a'));
        }

        internal ProductionOutputBufferCapacitySourceSnapshot Capacity { get; }

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
                    Fixture.FacilityId,
                    StringComparison.Ordinal)
                || maximumMassProof.MaximumBatchMassGrams
                    != Fixture.UnitMassGrams
                || !string.Equals(
                    savedCapacitySourceDigest,
                    Capacity.SourceDigest,
                    StringComparison.Ordinal)
                || savedRequiredMinimumCapacityGrams
                    != Capacity.RequiredMinimumCapacityGrams)
            {
                throw new InvalidOperationException(
                    "Detached workwear capacity drifted.");
            }
            return Capacity;
        }

        public ProductionOutputDetachedFacilityCapacityProjection Capture(
            string ownerStableId,
            string facilityInstanceId,
            ProductionOutputBatchMaximumMassProof maximumMassProof)
        {
            if (string.IsNullOrEmpty(ownerStableId)
                || !string.Equals(
                    facilityInstanceId,
                    Fixture.FacilityId,
                    StringComparison.Ordinal)
                || maximumMassProof.MaximumBatchMassGrams
                    != Fixture.UnitMassGrams)
            {
                throw new InvalidOperationException(
                    "Detached workwear facility projection drifted.");
            }
            return new ProductionOutputDetachedFacilityCapacityProjection(
                Fixture.FacilityId,
                Fixture.Position,
                Capacity);
        }
    }

    private sealed class WorkwearHandlerProbe :
        IProductionOutputHandler,
        IIdempotentProductionOutputHandler
    {
        private readonly IApparelDefinitionCatalog apparel;

        internal WorkwearHandlerProbe(IApparelDefinitionCatalog apparel) =>
            this.apparel = apparel;
        public string CapabilityId =>
            EnvironmentalWorkwearProductionOutputHandler.HandlerCapabilityId;
        public int ContractVersion =>
            EnvironmentalWorkwearProductionOutputHandler.HandlerContractVersion;
        public string ComponentCodecId =>
            EnvironmentalWorkwearProductionOutputHandler.HandlerComponentCodecId;
        public int ComponentCodecVersion =>
            EnvironmentalWorkwearProductionOutputHandler
                .HandlerComponentCodecVersion;
        public bool SupportsAutomaticSelection => true;
        public int ProduceCount { get; private set; }
        public int AcknowledgeCount { get; private set; }
        public int CaptureCount { get; private set; }
        public bool CanHandle(string itemId) =>
            apparel.TryGetByItemId(itemId, out _);
        public bool TryProduce(
            ProductionOutputContext context,
            out string failureReason)
        {
            ProduceCount++;
            failureReason = "restore-probe-must-not-execute";
            return false;
        }
        public bool TryProduceIdempotent(
            ProductionOutputContext context,
            out DomainFailure failure)
        {
            ProduceCount++;
            failure = new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                context.ItemId,
                "restore-probe-must-not-execute");
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
                "restore-probe-must-not-acknowledge");
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
                "restore-probe-must-not-capture");
            return false;
        }
    }

    private sealed class StandardCapability : IProductionOutputCapability
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

    private sealed class StandardMaximumMassCapability :
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
        public long AuthorityRevision => 1L;
        public PhysicalMassGrams GetDefinitionUnitMass(ItemDefinitionId itemId)
            => new(Fixture.UnitMassGrams);
        public PhysicalMassGrams GetPreparedStackUnitMass(
            PhysicalItemMassSubject subject) => new(Fixture.UnitMassGrams);
        public PhysicalMassGrams GetStackUnitMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject) => new(Fixture.UnitMassGrams);
        public PhysicalMassGrams GetStackTotalMass(PhysicalItemLotSnapshot lot)
            => new PhysicalMassGrams(Fixture.UnitMassGrams)
                .Multiply(lot.Quantity);
        public PhysicalMassGrams GetQuantityMass(
            ItemDefinitionId itemId,
            PhysicalItemMassSubject subject,
            int quantity) => new PhysicalMassGrams(Fixture.UnitMassGrams)
            .Multiply(quantity);
    }

    private sealed class EmptyOccupancy :
        IFacilityBufferPhysicalOccupancyQuery
    {
        public FacilityBufferPhysicalOccupancySnapshot Capture(
            string destinationId) => new(0L, 0L);
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

    private sealed class FixedApparelCatalog : IApparelDefinitionCatalog
    {
        private readonly ApparelDefinitionSO definition;
        internal FixedApparelCatalog(ApparelDefinitionSO definition) =>
            this.definition = definition;
        public IReadOnlyList<ApparelDefinitionSO> Definitions =>
            new[] { definition };
        public bool TryGet(string apparelId, out ApparelDefinitionSO value)
        {
            value = string.Equals(
                apparelId,
                definition.ApparelId,
                StringComparison.Ordinal) ? definition : null;
            return value != null;
        }
        public bool TryGetByItemId(
            string itemId,
            out ApparelDefinitionSO value)
        {
            value = string.Equals(
                itemId,
                definition.PhysicalItemId,
                StringComparison.Ordinal) ? definition : null;
            return value != null;
        }
        public int GetIndex(string apparelId) =>
            string.Equals(
                apparelId,
                definition.ApparelId,
                StringComparison.Ordinal) ? 0 : -1;
    }

    private sealed class FixedMaterialCatalog : ITextileMaterialCatalog
    {
        private readonly TextileMaterialDefinitionSO definition;
        private readonly bool available;
        internal FixedMaterialCatalog(
            TextileMaterialDefinitionSO definition,
            bool available)
        {
            this.definition = definition;
            this.available = available;
        }
        public IReadOnlyList<TextileMaterialDefinitionSO> Definitions =>
            available ? new[] { definition } : Array.Empty<
                TextileMaterialDefinitionSO>();
        public bool TryGet(
            string materialId,
            out TextileMaterialDefinitionSO value)
        {
            value = available && string.Equals(
                materialId,
                definition.MaterialId,
                StringComparison.Ordinal) ? definition : null;
            return value != null;
        }
        public bool TryGetByItemId(
            string itemId,
            out TextileMaterialDefinitionSO value)
        {
            value = available && string.Equals(
                itemId,
                definition.PhysicalItemId,
                StringComparison.Ordinal) ? definition : null;
            return value != null;
        }
        public int GetIndex(string materialId) => available && string.Equals(
            materialId,
            definition.MaterialId,
            StringComparison.Ordinal) ? 0 : -1;
    }

    private sealed class FixedEconomyCatalog : IResourceEconomyContentCatalog
    {
        private readonly ProductionRecipeSO recipe;
        internal FixedEconomyCatalog(ProductionRecipeSO recipe) =>
            this.recipe = recipe;
        public IReadOnlyList<ResourceItemDefinitionSO> Items =>
            Array.Empty<ResourceItemDefinitionSO>();
        public IReadOnlyList<ProductionRecipeSO> Recipes => new[] { recipe };
        public IReadOnlyList<CropDefinitionSO> Crops =>
            Array.Empty<CropDefinitionSO>();
        public IReadOnlyList<CraftMaterialDefinitionSO> Materials =>
            Array.Empty<CraftMaterialDefinitionSO>();
        public IReadOnlyList<SubstanceDefinitionView> Substances =>
            Array.Empty<SubstanceDefinitionView>();
        public bool TryGetItem(
            string itemId,
            out ResourceItemDefinitionSO definition)
        {
            definition = null;
            return false;
        }
        public bool TryGetRecipe(
            string recipeId,
            out ProductionRecipeSO definition)
        {
            definition = string.Equals(
                recipeId,
                recipe.RecipeId,
                StringComparison.Ordinal) ? recipe : null;
            return definition != null;
        }
        public bool TryGetCrop(
            string cropId,
            out CropDefinitionSO definition)
        {
            definition = null;
            return false;
        }
        public bool TryGetMaterial(
            string materialId,
            out CraftMaterialDefinitionSO definition)
        {
            definition = null;
            return false;
        }
        public bool TryGetSubstance(
            string substanceId,
            out SubstanceDefinitionView definition)
        {
            definition = default;
            return false;
        }
    }
}
#endif
