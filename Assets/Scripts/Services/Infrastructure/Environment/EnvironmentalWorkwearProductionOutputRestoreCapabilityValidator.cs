using System;
using System.Globalization;
using System.Linq;
using DungeonStory.Foundation;

internal static class EnvironmentalWorkwearProductionOutputSemantics
{
    internal const string PublicationOperationPrefix =
        "environmental-workwear-output-publication:";
    private const string OutcomeSchema =
        "environmental-workwear-output-outcome@2";

    internal static CraftsmanshipQualityTier ResolveCraftsmanship(
        float qualityModifier) => qualityModifier switch
    {
        >= 0.90f => CraftsmanshipQualityTier.Legendary,
        >= 0.65f => CraftsmanshipQualityTier.Masterwork,
        >= 0.35f => CraftsmanshipQualityTier.Excellent,
        >= 0.15f => CraftsmanshipQualityTier.Good,
        < -0.65f => CraftsmanshipQualityTier.Awful,
        < -0.30f => CraftsmanshipQualityTier.Poor,
        _ => CraftsmanshipQualityTier.Normal
    };

    internal static TextileSourceKind ResolveSourceKind(
        TextileMaterialTag tags)
    {
        if ((tags & TextileMaterialTag.Animal) != 0)
            return TextileSourceKind.Animal;
        if ((tags & TextileMaterialTag.Arcane) != 0)
            return TextileSourceKind.Arcane;
        if ((tags & TextileMaterialTag.Plant) != 0)
            return TextileSourceKind.Crop;
        return TextileSourceKind.Unknown;
    }

    internal static ulong DeterministicHash(
        string recipeId,
        string facilityId,
        string commitId,
        int craftedDay,
        int outputIndex)
    {
        const ulong offset = 1469598103934665603UL;
        const ulong prime = 1099511628211UL;
        ulong hash = offset;
        string source = string.Join(
            "|",
            recipeId ?? string.Empty,
            facilityId ?? string.Empty,
            commitId ?? string.Empty,
            craftedDay.ToString(CultureInfo.InvariantCulture),
            outputIndex.ToString(CultureInfo.InvariantCulture));
        for (int index = 0; index < source.Length; index++)
        {
            hash ^= source[index];
            hash *= prime;
        }
        return hash;
    }

    internal static string FormatUnitOutputLineId(
        string outputLineId,
        int outputIndex) => outputLineId
        + ":unit:"
        + outputIndex.ToString("D4", CultureInfo.InvariantCulture);

    internal static string CreateOutcomeFingerprint(
        string commitId,
        string outputLineId,
        string itemId,
        int amount,
        string outputDestinationId,
        string recipeId,
        string facilityId,
        string materialId,
        ProductionOutputBatchMaximumMassProof maximumMassProof,
        ProductionOutputBufferCapacitySourceSnapshot capacity,
        float qualityModifier,
        float workerQuality)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(OutcomeSchema);
        digest.Append(commitId);
        digest.Append(outputLineId);
        digest.Append(itemId);
        digest.Append(amount);
        digest.Append(outputDestinationId);
        digest.Append(recipeId ?? string.Empty);
        digest.Append(facilityId);
        digest.Append(materialId ?? string.Empty);
        digest.Append(maximumMassProof.SourceDigest);
        digest.Append(maximumMassProof.MaximumBatchMassGrams);
        digest.Append(capacity.SourceDigest);
        digest.Append(capacity.RequiredMinimumCapacityGrams);
        digest.Append(BitConverter.SingleToInt32Bits(qualityModifier));
        digest.Append(BitConverter.SingleToInt32Bits(workerQuality));
        return digest.ComputeSha256();
    }

    internal static string HashCanonicalComponent(string canonical)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(canonical ?? string.Empty);
        return digest.ComputeSha256();
    }
}

/// <summary>
/// Reprojects one detached environmental-workwear pending unit from authored
/// catalogs and immutable physical restore data. Validation is pure: it never
/// reserves capacity, acknowledges publication, or mutates either aggregate.
/// </summary>
public sealed class
    EnvironmentalWorkwearProductionOutputRestoreCapabilityValidator :
    IProductionResolvedOutputRestoreCapabilityValidator
{
    private const int UnitAmount = 1;

    private readonly IResourceEconomyContentCatalog economyCatalog;
    private readonly IApparelDefinitionCatalog apparelCatalog;
    private readonly ITextileMaterialCatalog materialCatalog;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly IFacilityBufferPlannedOutputProjectionQuery
        plannedOutputProjection;

    public EnvironmentalWorkwearProductionOutputRestoreCapabilityValidator(
        IResourceEconomyContentCatalog economyCatalog,
        IApparelDefinitionCatalog apparelCatalog,
        ITextileMaterialCatalog materialCatalog,
        IPhysicalItemMassQuery massQuery,
        IFacilityBufferPlannedOutputProjectionQuery plannedOutputProjection)
    {
        this.economyCatalog = economyCatalog
            ?? throw new ArgumentNullException(nameof(economyCatalog));
        this.apparelCatalog = apparelCatalog
            ?? throw new ArgumentNullException(nameof(apparelCatalog));
        this.materialCatalog = materialCatalog
            ?? throw new ArgumentNullException(nameof(materialCatalog));
        this.massQuery = massQuery
            ?? throw new ArgumentNullException(nameof(massQuery));
        this.plannedOutputProjection = plannedOutputProjection
            ?? throw new ArgumentNullException(nameof(plannedOutputProjection));
    }

    public string CapabilityId =>
        EnvironmentalWorkwearProductionOutputHandler.HandlerCapabilityId;
    public int ContractVersion =>
        EnvironmentalWorkwearProductionOutputHandler.HandlerContractVersion;
    public string ComponentCodecId =>
        EnvironmentalWorkwearProductionOutputHandler.HandlerComponentCodecId;
    public int ComponentCodecVersion =>
        EnvironmentalWorkwearProductionOutputHandler
            .HandlerComponentCodecVersion;

    public void Validate(
        ProductionResolvedOutputRestoreValidationContext context)
    {
        if (context == null)
            throw new ArgumentNullException(nameof(context));

        ProductionBillSaveData bill = context.Bill;
        ProductionResolvedOutputSaveData output = context.Output;
        ProductionOutputCapabilityDescriptor descriptor = context.Descriptor;
        ProductionOutputBatchMaximumMassProof maximumMassProof =
            context.MaximumMassProof;
        ProductionOutputDetachedFacilityCapacityProjection facility =
            context.FacilityCapacity;
        ProductionOutputBufferCapacitySourceSnapshot capacity =
            facility.Capacity;
        FacilityBufferPlannedOutputRestoreBatchSnapshot physical =
            context.Physical;

        RequireDescriptor(descriptor, output);
        if (!economyCatalog.TryGetRecipe(
                bill.recipeId,
                out ProductionRecipeSO recipe)
            || recipe == null
            || !string.Equals(
                recipe.RecipeId,
                bill.recipeId,
                StringComparison.Ordinal))
        {
            Fail(output, "recipe-authority-missing");
        }

        ProductionOutputDefinition[] authoredLines = recipe
            .CaptureCanonicalOutputs()
            .Where(value => string.Equals(
                value.OutputLineId,
                output.outputLineId,
                StringComparison.Ordinal))
            .ToArray();
        if (authoredLines.Length != 1
            || !string.Equals(
                authoredLines[0].ItemId,
                output.itemId,
                StringComparison.Ordinal)
            || ProductionOutputRoleRules.IsNonPhysical(authoredLines[0].Role))
        {
            Fail(output, "recipe-output-authority-mismatch");
        }

        if (!apparelCatalog.TryGetByItemId(
                output.itemId,
                out ApparelDefinitionSO apparel)
            || apparel == null
            || !string.Equals(
                apparel.PhysicalItemId,
                output.itemId,
                StringComparison.Ordinal))
        {
            Fail(output, "apparel-authority-missing");
        }

        TextileMaterialDefinitionSO material = ResolvePrimaryMaterial(recipe);
        if (material == null
            || (material.Tags & apparel.AllowedMaterialTags) == 0)
        {
            Fail(output, "material-authority-invalid");
        }

        string facilityId = bill.buildingInstanceId ?? string.Empty;
        if (!((BuildingInstanceId)facilityId).IsValid
            || !string.Equals(
                facility.FacilityInstanceId,
                facilityId,
                StringComparison.Ordinal))
        {
            Fail(output, "facility-authority-mismatch");
        }
        string destinationId = ProductionOutputDestinationId
            .FromFacility((BuildingInstanceId)facilityId)
            .Value;
        if (!string.Equals(
                bill.outputDestinationId,
                destinationId,
                StringComparison.Ordinal)
            || capacity.RequiredMinimumCapacityGrams <= 0L
            || capacity.MaximumBatchMassGrams
                != maximumMassProof.MaximumBatchMassGrams)
        {
            Fail(output, "destination-capacity-authority-mismatch");
        }

        RequireMaximumMassProof(maximumMassProof, descriptor, output);
        string expectedOutcome = EnvironmentalWorkwearProductionOutputSemantics
            .CreateOutcomeFingerprint(
                output.pendingCommitId,
                output.outputLineId,
                output.itemId,
                UnitAmount,
                destinationId,
                recipe.RecipeId,
                facilityId,
                material.MaterialId,
                maximumMassProof,
                capacity,
                output.qualityModifier,
                output.workerQuality);

        FacilityBufferPlannedOutputRestoreStackSnapshot stack =
            RequirePhysicalBatch(
                context,
                expectedOutcome,
                destinationId,
                facilityId);
        ItemInstanceComponentSaveData apparelComponent =
            RequireApparelComponent(stack, output);
        ApparelInstanceState apparelState = DecodeApparelState(
            stack,
            output);
        RequireDeterministicState(
            apparelState,
            apparelComponent,
            apparel,
            material,
            recipe.RecipeId,
            facilityId,
            output);

        string expectedPreparedFingerprint =
            EnvironmentalWorkwearProductionOutputSemantics
                .HashCanonicalComponent(apparelComponent.ToCanonicalString());
        if (!string.Equals(
                stack.PreparedComponentFingerprint,
                expectedPreparedFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                stack.ComponentSignature,
                FacilityBufferPlannedOutputPublicationService
                    .CreateRuntimeComponentSignature(stack.Components),
                StringComparison.Ordinal))
        {
            Fail(output, "component-fingerprint-mismatch");
        }

        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            massQuery,
            (ItemDefinitionId)output.itemId,
            stack.ItemInstanceId,
            stack.Components);
        long exactMassGrams = massQuery.GetQuantityMass(
            (ItemDefinitionId)output.itemId,
            subject,
            UnitAmount).Value;
        if (exactMassGrams != stack.MassGrams
            || exactMassGrams != physical.TotalMassGrams
            || exactMassGrams > maximumMassProof.MaximumBatchMassGrams)
        {
            Fail(output, "exact-mass-mismatch");
        }

        FacilityBufferPlannedOutputSlice slice = new(
            stack.OutputLineId,
            subject,
            UnitAmount,
            stack.Components,
            expectedPreparedFingerprint);
        FacilityBufferPlannedOutputRequest request = new(
            EnvironmentalWorkwearProductionOutputSemantics
                .PublicationOperationPrefix + output.pendingCommitId,
            output.pendingCommitId,
            expectedOutcome,
            destinationId,
            facility.FacilityPosition,
            ProductionOutputDestinationAuthorityRuntime.OwnerDomain,
            destinationId,
            facilityId,
            ProductionOutputDestinationAuthorityRuntime
                .CapacitySchemaRevision,
            new[] { slice },
            capacity.SourceDigest,
            capacity.RequiredMinimumCapacityGrams,
            capacity.ClearanceGateDigest);
        if (!plannedOutputProjection.TryProjectPlannedOutput(
                request,
                out FacilityBufferPlannedOutputSnapshot planned,
                out FacilityBufferMassAdmissionFailureCode failureCode,
                out string failureReason))
        {
            Fail(
                output,
                "planned-output-projection-failed:"
                + failureCode.ToString()
                + ":"
                + (failureReason ?? string.Empty));
        }
        if (!string.Equals(
                planned.Fingerprint,
                physical.PlannedOutputFingerprint,
                StringComparison.Ordinal)
            || planned.TotalQuantity != UnitAmount
            || planned.TotalMassGrams != exactMassGrams
            || planned.Slices.Count != 1
            || !string.Equals(
                planned.Slices[0].OutputLineId,
                stack.OutputLineId,
                StringComparison.Ordinal)
            || !planned.Slices[0].ItemDefinitionId.Equals(
                (ItemDefinitionId)output.itemId)
            || planned.Slices[0].Quantity != UnitAmount
            || planned.Slices[0].ExactMassGrams != exactMassGrams)
        {
            Fail(output, "planned-output-fingerprint-mismatch");
        }
    }

    private TextileMaterialDefinitionSO ResolvePrimaryMaterial(
        ProductionRecipeSO recipe)
    {
        foreach (ItemAmountDefinition input in recipe?.Inputs
                     ?? Array.Empty<ItemAmountDefinition>())
        {
            if (input != null
                && materialCatalog.TryGetByItemId(
                    input.ItemId,
                    out TextileMaterialDefinitionSO material))
            {
                return material;
            }
        }
        return null;
    }

    private void RequireDescriptor(
        ProductionOutputCapabilityDescriptor descriptor,
        ProductionResolvedOutputSaveData output)
    {
        if (!string.Equals(
                descriptor.OutputLineId,
                output.outputLineId,
                StringComparison.Ordinal)
            || !string.Equals(
                descriptor.ItemId,
                output.itemId,
                StringComparison.Ordinal)
            || !string.Equals(
                descriptor.CapabilityId,
                CapabilityId,
                StringComparison.Ordinal)
            || descriptor.CapabilityVersion != ContractVersion
            || !string.Equals(
                descriptor.ComponentCodecId,
                ComponentCodecId,
                StringComparison.Ordinal)
            || descriptor.ComponentCodecVersion != ComponentCodecVersion
            || !string.Equals(
                output.outputCapabilityId,
                CapabilityId,
                StringComparison.Ordinal)
            || output.outputCapabilityVersion != ContractVersion
            || !string.Equals(
                output.outputComponentCodecId,
                ComponentCodecId,
                StringComparison.Ordinal)
            || output.outputComponentCodecVersion != ComponentCodecVersion
            || !string.Equals(
                descriptor.Fingerprint,
                output.outputCapabilityFingerprint,
                StringComparison.Ordinal))
        {
            Fail(output, "capability-descriptor-mismatch");
        }
    }

    private static void RequireMaximumMassProof(
        ProductionOutputBatchMaximumMassProof proof,
        ProductionOutputCapabilityDescriptor descriptor,
        ProductionResolvedOutputSaveData output)
    {
        if (proof.Projections.Count != 1)
            Fail(output, "maximum-mass-projection-count-mismatch");
        ProductionOutputMaximumMassProjection projection = proof.Projections[0];
        ProductionOutputCapabilityDescriptor projected = projection.Descriptor;
        if (projection.MaximumQuantity != UnitAmount
            || projection.MaximumMassGrams
                != projection.DefinitionUnitMassGrams
            || projection.MaximumMassGrams != proof.MaximumBatchMassGrams
            || !string.Equals(
                projected.OutputLineId,
                descriptor.OutputLineId,
                StringComparison.Ordinal)
            || !string.Equals(
                projected.ItemId,
                descriptor.ItemId,
                StringComparison.Ordinal)
            || !string.Equals(
                projected.CapabilityId,
                descriptor.CapabilityId,
                StringComparison.Ordinal)
            || projected.CapabilityVersion != descriptor.CapabilityVersion
            || !string.Equals(
                projected.ComponentCodecId,
                descriptor.ComponentCodecId,
                StringComparison.Ordinal)
            || projected.ComponentCodecVersion
                != descriptor.ComponentCodecVersion
            || !string.Equals(
                projected.Fingerprint,
                descriptor.Fingerprint,
                StringComparison.Ordinal))
        {
            Fail(output, "maximum-mass-projection-mismatch");
        }
    }

    private static FacilityBufferPlannedOutputRestoreStackSnapshot
        RequirePhysicalBatch(
            ProductionResolvedOutputRestoreValidationContext context,
            string expectedOutcome,
            string expectedDestination,
            string facilityId)
    {
        ProductionResolvedOutputSaveData output = context.Output;
        FacilityBufferPlannedOutputRestoreBatchSnapshot physical =
            context.Physical;
        string unitLineId = EnvironmentalWorkwearProductionOutputSemantics
            .FormatUnitOutputLineId(output.outputLineId, 0);
        if (!string.Equals(
                physical.BatchCommitId,
                output.pendingCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                physical.OutcomeFingerprint,
                expectedOutcome,
                StringComparison.Ordinal)
            || physical.TotalQuantity != UnitAmount
            || physical.TotalMassGrams <= 0L
            || physical.Stacks.Count != 1)
        {
            Fail(output, "physical-batch-mismatch");
        }

        FacilityBufferPlannedOutputRestoreStackSnapshot stack =
            physical.Stacks[0];
        if (stack == null
            || !string.Equals(
                stack.BatchCommitId,
                output.pendingCommitId,
                StringComparison.Ordinal)
            || !string.Equals(
                stack.OutcomeFingerprint,
                expectedOutcome,
                StringComparison.Ordinal)
            || !string.Equals(
                stack.PlannedOutputFingerprint,
                physical.PlannedOutputFingerprint,
                StringComparison.Ordinal)
            || !string.Equals(
                stack.OutputLineId,
                unitLineId,
                StringComparison.Ordinal)
            || stack.StackOrdinal != 0
            || !string.Equals(
                stack.ItemId,
                output.itemId,
                StringComparison.Ordinal)
            || stack.Quantity != UnitAmount
            || stack.MassGrams != physical.TotalMassGrams
            || !((ItemInstanceId)stack.ItemInstanceId).IsValid
            || string.IsNullOrEmpty(stack.StackId))
        {
            Fail(output, "physical-stack-mismatch");
        }
        if (context.IsPendingPhysical
            && (stack.State != WorldItemStackState.FacilityOutputBuffer
                || stack.Position != context.FacilityCapacity.FacilityPosition
                || !string.Equals(
                    stack.DestinationId,
                    expectedDestination,
                    StringComparison.Ordinal)))
        {
            Fail(output, "pending-physical-location-mismatch:" + facilityId);
        }
        return stack;
    }

    private static ItemInstanceComponentSaveData RequireApparelComponent(
        FacilityBufferPlannedOutputRestoreStackSnapshot stack,
        ProductionResolvedOutputSaveData output)
    {
        if (stack.Components.Count != 1)
            Fail(output, "apparel-component-count-mismatch");
        ItemInstanceComponentSaveData component = stack.Components[0];
        if (component == null
            || !string.Equals(
                component.componentTypeId,
                ItemInstanceComponentIds.Apparel,
                StringComparison.Ordinal)
            || component.schemaVersion != ApparelItemStateCodec.SchemaVersion
            || !component.affectsStacking)
        {
            Fail(output, "apparel-component-contract-mismatch");
        }
        return component;
    }

    private static ApparelInstanceState DecodeApparelState(
        FacilityBufferPlannedOutputRestoreStackSnapshot stack,
        ProductionResolvedOutputSaveData output)
    {
        if (!ApparelItemStateCodec.TryRead(
                stack.Components,
                out ApparelInstanceState state)
            || state == null)
        {
            Fail(output, "apparel-component-decode-failed");
        }
        return state;
    }

    private static void RequireDeterministicState(
        ApparelInstanceState actual,
        ItemInstanceComponentSaveData actualComponent,
        ApparelDefinitionSO apparel,
        TextileMaterialDefinitionSO material,
        string recipeId,
        string facilityId,
        ProductionResolvedOutputSaveData output)
    {
        ApparelInstanceState expected = new()
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
            designatedWearerCharacterId = string.Empty,
            craftedAbsoluteDay = actual.craftedAbsoluteDay,
            deterministicBatchHash =
                EnvironmentalWorkwearProductionOutputSemantics
                    .DeterministicHash(
                        recipeId,
                        facilityId,
                        output.pendingCommitId,
                        actual.craftedAbsoluteDay,
                        0),
            mythicProvenance = null
        };
        ItemInstanceComponentSaveData expectedComponent =
            ApparelItemStateCodec.Create(expected);
        if (actual.craftedAbsoluteDay < 0
            || !string.Equals(
                actualComponent.ToCanonicalString(),
                expectedComponent.ToCanonicalString(),
                StringComparison.Ordinal))
        {
            Fail(output, "apparel-deterministic-state-mismatch");
        }
    }

    private static void Fail(
        ProductionResolvedOutputSaveData output,
        string reason)
    {
        throw new InvalidOperationException(
            "Environmental workwear production-output restore validation "
            + "failed:"
            + (output?.pendingCommitId ?? string.Empty)
            + ":"
            + (reason ?? string.Empty));
    }
}
