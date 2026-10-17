using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation.Facts;
using UnityEngine;

/// <summary>
/// Resolves immutable production output candidates and verifies their source proofs.
/// Reservation, physical publication, acknowledgement, and restore state remain
/// with the execution adapter and their existing domain authorities.
/// </summary>
internal sealed class ProductionPreparedOutputResolutionPlanner
{
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionOutputPlanningService outputPlanning;
    private readonly IProductionAssemblyBridge bridge;
    private readonly IGrandProjectBenefitQuery grandProjectBenefits;
    private readonly CanonicalProductionOutputResolver resolver;
    private readonly IProductionPreparedOutputMaterializerRegistry materializers;
    private readonly IPhysicalItemMassQuery massQuery;
    private readonly ProductionOutputBufferCapacityProjector capacityProjector;
    private readonly ProductionMassExplanationCapabilityRegistry massExplanations;

    internal ProductionPreparedOutputResolutionPlanner(
        IResourceEconomyContentCatalog catalog,
        IProductionOutputPlanningService outputPlanning,
        IProductionAssemblyBridge bridge,
        IGrandProjectBenefitQuery grandProjectBenefits,
        CanonicalProductionOutputResolver resolver,
        IProductionPreparedOutputMaterializerRegistry materializers,
        IPhysicalItemMassQuery massQuery,
        ProductionOutputBufferCapacityProjector capacityProjector,
        ProductionMassExplanationCapabilityRegistry massExplanations)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.outputPlanning = outputPlanning ?? throw new ArgumentNullException(nameof(outputPlanning));
        this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        this.grandProjectBenefits = grandProjectBenefits ?? throw new ArgumentNullException(nameof(grandProjectBenefits));
        this.resolver = resolver ?? throw new ArgumentNullException(nameof(resolver));
        this.materializers = materializers ?? throw new ArgumentNullException(nameof(materializers));
        this.massQuery = massQuery ?? throw new ArgumentNullException(nameof(massQuery));
        this.capacityProjector = capacityProjector ?? throw new ArgumentNullException(nameof(capacityProjector));
        this.massExplanations = massExplanations ?? throw new ArgumentNullException(nameof(massExplanations));
    }

    internal bool TryResolveBatch(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out ProductionPreparedOutputBatchSaveData batch,
        out DomainFailure failure)
    {
        if (record.preparedOutput != null
            && record.preparedOutput.phase != ProductionPreparedOutputPhase.Unresolved)
        {
            try
            {
                batch = record.preparedOutput.Clone();
                ValidateResolvedSourceAuthority(
                    record,
                    recipe,
                    facility,
                    "prepared-output-existing-batch");
                failure = DomainFailure.None;
                return true;
            }
            catch (Exception exception) when (exception is ArgumentException
                                               or InvalidOperationException
                                               or OverflowException)
            {
                batch = null;
                failure = Fail(
                    record,
                    "prepared-output-persisted-batch-invalid",
                    exception.Message);
                return false;
            }
        }
        try
        {
            ProductionOutputFactor multiplier = ProductionOutputFactorAuthority
                .ResolveCurrent(grandProjectBenefits, recipe.FacilityTag)
                .Multiply(ProductionOutputFactor.FromAuthoredMultiplier(
                    outputPlanning.ResolveSupportModifier(
                        facility,
                        recipe,
                        ProductionSupportModifierKind.Output,
                        1f,
                        multiply: true)));
            CanonicalProductionOutputResolution resolution = resolver.Resolve(
                record.billId,
                record.cycleSequence,
                recipe.RecipeId,
                recipe.CaptureCanonicalOutputs(),
                multiplier,
                recipe.ProcessKind,
                record.batchIntegrity);
            string recipeDigest = ComputeRecipeDigest(recipe);
            List<ProductionPreparedOutputLineSaveData> lines = new();
            long physicalMass = 0L;
            foreach (CanonicalProductionResolvedOutputLine resolved in
                     resolution.Lines.OrderBy(value => value.OutputLineId, StringComparer.Ordinal))
            {
                if (ProductionOutputRoleRules.IsNonPhysical(resolved.Role))
                {
                    throw new InvalidOperationException(
                        "Authored non-physical output lines cannot enter the physical prepared-output resolver.");
                }
                if (!catalog.TryGetItem(
                        resolved.ItemId,
                        out ResourceItemDefinitionSO definition))
                {
                    throw new InvalidOperationException(
                        $"Prepared output item '{resolved.ItemId}' is missing.");
                }
                ProductionOutputCapabilityDescriptor capability =
                    bridge.CaptureOutputCapability(
                        resolved.OutputLineId,
                        resolved.ItemId);
                ProductionPreparedOutputComponentProjection projection =
                    materializers.Create(capability, definition);
                long exactMass = resolved.ResolvedQuantity == 0
                    ? 0L
                    : massQuery.GetQuantityMass(
                        (ItemDefinitionId)resolved.ItemId,
                        projection.MassSubject,
                        resolved.ResolvedQuantity).Value;
                physicalMass = checked(physicalMass + exactMass);
                lines.Add(new ProductionPreparedOutputLineSaveData
                {
                    outputLineId = resolved.OutputLineId,
                    role = resolved.Role,
                    itemId = resolved.ItemId,
                    outputCapabilityId = capability.CapabilityId,
                    outputCapabilityVersion = capability.CapabilityVersion,
                    outputComponentCodecId = capability.ComponentCodecId,
                    outputComponentCodecVersion = capability.ComponentCodecVersion,
                    outputCapabilityFingerprint = capability.Fingerprint,
                    quantity = resolved.ResolvedQuantity,
                    componentPayload = projection.CanonicalPayload,
                    componentFingerprint = projection.Fingerprint,
                    qualityPermille = 1000,
                    rollKind = "canonical-keyed",
                    rollValue = 0L,
                    rollUpperExclusive = 1L,
                    rollSucceeded = resolved.Included,
                    exactMassGrams = exactMass
                });
            }
            if (physicalMass <= 0L)
                throw new InvalidOperationException("Prepared output resolved no physical mass.");

            ProductionPreparedOutputCapacityClaim capacityClaim =
                capacityProjector.CapturePreparedClaim(
                    record.billId.Value,
                    record.cycleSequence,
                    recipe,
                    lines);
            if (capacityClaim.ExactBatchMassGrams != physicalMass)
            {
                throw new InvalidOperationException(
                    "Prepared output exact mass drifted from its capacity claim.");
            }
            ProductionOutputBufferCapacitySourceSnapshot capacitySource =
                capacityProjector.CaptureSource(facility, capacityClaim);

            long declaredLossMass = 0L;
            long declaredExternalInputMass = 0L;
            ProductionMassExplanationAuthoringSnapshot massAuthoring =
                recipe.MassExplanation;
            if (!massAuthoring.IsEmpty)
            {
                ProductionMassExplanationDisposition disposition =
                    massExplanations.Resolve(
                        massAuthoring,
                        new ProductionMassExplanationEquationSubject(
                            recipe.RecipeId,
                            record.wipInputMassGrams,
                            0L,
                            record.processCleanWaterMassGrams,
                            physicalMass,
                            record.processWastewaterMassGrams,
                            0L));
                if (!disposition.HasDisposition)
                {
                    throw new InvalidOperationException(
                        "Authored production mass explanation resolved no disposition.");
                }
                declaredLossMass = disposition.DeclaredLossGrams;
                declaredExternalInputMass =
                    disposition.DeclaredExternalInputGrams;
                if (declaredLossMass > 0L)
                {
                    lines.Add(new ProductionPreparedOutputLineSaveData
                    {
                        outputLineId = BuildProcessLossOutputLineId(
                            recipe.RecipeId),
                        role = ProductionOutputRole.DeclaredLoss,
                        itemId = string.Empty,
                        quantity = 0,
                        componentPayload = disposition.CanonicalReceiptPayload,
                        componentFingerprint = disposition.Fingerprint,
                        qualityPermille = 1000,
                        rollKind = "process-loss",
                        rollValue = 0L,
                        rollUpperExclusive = 1L,
                        rollSucceeded = true,
                        exactMassGrams = declaredLossMass
                    });
                }
                else if (declaredExternalInputMass > 0L)
                {
                    lines.Add(new ProductionPreparedOutputLineSaveData
                    {
                        outputLineId = BuildProcessExternalInputOutputLineId(
                            recipe.RecipeId),
                        role = ProductionOutputRole.DeclaredExternalInput,
                        itemId = string.Empty,
                        quantity = 0,
                        componentPayload = disposition.CanonicalReceiptPayload,
                        componentFingerprint = disposition.Fingerprint,
                        qualityPermille = 1000,
                        rollKind = "process-addition",
                        rollValue = 0L,
                        rollUpperExclusive = 1L,
                        rollSucceeded = true,
                        exactMassGrams = declaredExternalInputMass
                    });
                }
                lines = lines
                    .OrderBy(value => value.outputLineId, StringComparer.Ordinal)
                    .ToList();
            }

            string outcomeFingerprint = ComputeOutcomeFingerprint(
                resolution,
                lines);
            string batchCommitId = ProductionPreparedOutputIdentity.BuildBatchCommitId(
                record.billId,
                record.cycleSequence,
                outcomeFingerprint);
            foreach (ProductionPreparedOutputLineSaveData line in lines)
            {
                line.lineCommitId = ProductionPreparedOutputIdentity.BuildLineCommitId(
                    batchCommitId,
                    line.outputLineId);
            }
            batch = new ProductionPreparedOutputBatchSaveData
            {
                phase = ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace,
                billId = record.billId.Value,
                cycleSequence = record.cycleSequence,
                recipeId = recipe.RecipeId,
                destinationId = record.outputDestinationId,
                recipeDefinitionDigest = recipeDigest,
                migrationProfileDigest = ProductionPreparedOutputMigrationScope
                    .CaptureProfileDigest(recipe),
                capacitySourceDigest = capacitySource.SourceDigest,
                maximumMassProofDigest = capacityClaim.MaximumMassProof
                    .SourceDigest,
                maximumBatchMassGrams = capacityClaim.MaximumMassProof
                    .MaximumBatchMassGrams,
                capacityClaimDigest = capacityClaim.SourceDigest,
                outputBufferCycleCapacity = capacitySource.CycleCapacity,
                projectedPortfolioCapacityGrams = capacitySource
                    .ProjectedPortfolioCapacityGrams,
                requiredMinimumCapacityGrams = capacitySource
                    .RequiredMinimumCapacityGrams,
                outcomeFingerprint = outcomeFingerprint,
                batchCommitId = batchCommitId,
                totalPhysicalMassGrams = physicalMass,
                totalDeclaredLossMassGrams = declaredLossMass,
                totalDeclaredExternalInputMassGrams =
                    declaredExternalInputMass,
                lines = lines
            };
            ProductionPreparedOutputContract.ValidateForBill(
                batch,
                record.billId,
                recipe.RecipeId,
                record.cycleSequence,
                record.outputDestinationId);
            failure = DomainFailure.None;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or KeyNotFoundException
                                           or OverflowException)
        {
            batch = null;
            failure = Fail(record, "prepared-output-resolution-failed", exception.Message);
            return false;
        }
    }

    internal bool TryResolveRuinedBatch(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        ProductionRuinedBatchDispositionPlan disposition,
        ProductionRuinedOutputCapacityClaim capacityClaim,
        out ProductionPreparedOutputBatchSaveData batch,
        out DomainFailure failure)
    {
        if (record.preparedOutput != null
            && record.preparedOutput.phase != ProductionPreparedOutputPhase.Unresolved)
        {
            batch = record.preparedOutput.Clone();
            try
            {
                ValidateResolvedSourceAuthority(
                    record,
                    recipe,
                    facility,
                    "ruined-output-existing-batch");
            }
            catch (Exception exception) when (exception is ArgumentException
                                               or InvalidOperationException
                                               or OverflowException)
            {
                batch = null;
                failure = Fail(
                    record,
                    "prepared-output-persisted-batch-invalid",
                    exception.Message);
                return false;
            }
            if (!IsExactRuinedBatch(batch, recipe, disposition))
            {
                failure = Fail(record, "ruined-output-existing-batch-conflict");
                return false;
            }
            failure = DomainFailure.None;
            return true;
        }

        try
        {
            List<ProductionPreparedOutputLineSaveData> lines = new();
            foreach (ProductionOutputDefinition authored in
                     recipe.CaptureCanonicalOutputs()
                         .OrderBy(value => value.OutputLineId, StringComparer.Ordinal))
            {
                if (!catalog.TryGetItem(
                        authored.ItemId,
                        out ResourceItemDefinitionSO definition))
                {
                    throw new InvalidOperationException(
                        $"Ruined output item '{authored.ItemId}' is missing.");
                }
                ProductionOutputCapabilityDescriptor capability =
                    bridge.CaptureOutputCapability(
                        authored.OutputLineId,
                        authored.ItemId);
                ProductionPreparedOutputComponentProjection projection =
                    materializers.Create(capability, definition);
                lines.Add(new ProductionPreparedOutputLineSaveData
                {
                    outputLineId = authored.OutputLineId,
                    role = authored.Role,
                    itemId = authored.ItemId,
                    outputCapabilityId = capability.CapabilityId,
                    outputCapabilityVersion = capability.CapabilityVersion,
                    outputComponentCodecId = capability.ComponentCodecId,
                    outputComponentCodecVersion = capability.ComponentCodecVersion,
                    outputCapabilityFingerprint = capability.Fingerprint,
                    quantity = 0,
                    componentPayload = projection.CanonicalPayload,
                    componentFingerprint = projection.Fingerprint,
                    qualityPermille = 1000,
                    rollKind = "ruined-batch",
                    rollValue = 0L,
                    rollUpperExclusive = 1L,
                    rollSucceeded = false,
                    exactMassGrams = 0L
                });
            }

            if (!catalog.TryGetItem(
                    disposition.SpoilageItemId,
                    out ResourceItemDefinitionSO spoilageDefinition))
            {
                throw new InvalidOperationException(
                    $"Ruined recoverable item '{disposition.SpoilageItemId}' is missing.");
            }
            ProductionOutputCapabilityDescriptor wasteCapability =
                bridge.CaptureOutputCapability(
                    ProductionRuinedBatchDispositionPlan
                        .RecoverableWasteOutputLineId,
                    disposition.SpoilageItemId);
            ProductionPreparedOutputComponentProjection wasteProjection =
                materializers.Create(wasteCapability, spoilageDefinition);
            long queriedWasteMass = massQuery.GetQuantityMass(
                    (ItemDefinitionId)disposition.SpoilageItemId,
                    wasteProjection.MassSubject,
                    disposition.RecoverableWasteQuantity)
                .Value;
            if (queriedWasteMass != disposition.RecoverableWasteMassGrams)
            {
                throw new InvalidOperationException(
                    "Ruined recoverable-waste authored and queried masses conflict.");
            }
            lines.Add(new ProductionPreparedOutputLineSaveData
            {
                outputLineId = ProductionRuinedBatchDispositionPlan
                    .RecoverableWasteOutputLineId,
                role = ProductionOutputRole.RecoverableWaste,
                itemId = disposition.SpoilageItemId,
                outputCapabilityId = wasteCapability.CapabilityId,
                outputCapabilityVersion = wasteCapability.CapabilityVersion,
                outputComponentCodecId = wasteCapability.ComponentCodecId,
                outputComponentCodecVersion = wasteCapability.ComponentCodecVersion,
                outputCapabilityFingerprint = wasteCapability.Fingerprint,
                quantity = disposition.RecoverableWasteQuantity,
                componentPayload = wasteProjection.CanonicalPayload,
                componentFingerprint = wasteProjection.Fingerprint,
                qualityPermille = 1000,
                rollKind = "ruined-batch",
                rollValue = 0L,
                rollUpperExclusive = 1L,
                rollSucceeded = true,
                exactMassGrams = queriedWasteMass
            });

            if (disposition.DeclaredLossMassGrams > 0L)
            {
                const string lossPayload =
                    "production-ruined-declared-loss@1|reason=passive-batch-ruin-remainder";
                lines.Add(new ProductionPreparedOutputLineSaveData
                {
                    outputLineId = ProductionRuinedBatchDispositionPlan
                        .DeclaredLossOutputLineId,
                    role = ProductionOutputRole.DeclaredLoss,
                    itemId = string.Empty,
                    quantity = 0,
                    componentPayload = lossPayload,
                    componentFingerprint = Sha256(lossPayload),
                    qualityPermille = 1000,
                    rollKind = "ruined-batch",
                    rollValue = 0L,
                    rollUpperExclusive = 1L,
                    rollSucceeded = true,
                    exactMassGrams = disposition.DeclaredLossMassGrams
                });
            }

            lines = lines
                .OrderBy(value => value.outputLineId, StringComparer.Ordinal)
                .ToList();
            string recipeDigest = ComputeRecipeDigest(recipe);
            if (capacityClaim == null
                || capacityClaim.Disposition.RecoverableWasteQuantity
                    != disposition.RecoverableWasteQuantity
                || capacityClaim.Disposition.RecoverableWasteMassGrams
                    != disposition.RecoverableWasteMassGrams)
            {
                throw new InvalidOperationException(
                    "Ruined output capacity claim drifted before batch resolution.");
            }
            ProductionOutputBufferCapacitySourceSnapshot capacitySource =
                capacityProjector.CaptureSource(facility, capacityClaim);
            if (disposition.RecoverableWasteMassGrams
                > capacityClaim.MaximumMassProof.MaximumBatchMassGrams)
            {
                throw new InvalidOperationException(
                    "Ruined output exceeds its pre-publication maximum-mass proof.");
            }
            string outcomeFingerprint = ComputeRuinedOutcomeFingerprint(
                record,
                recipe,
                disposition,
                capacityClaim,
                lines);
            string batchCommitId = ProductionPreparedOutputIdentity.BuildBatchCommitId(
                record.billId,
                record.cycleSequence,
                outcomeFingerprint);
            foreach (ProductionPreparedOutputLineSaveData line in lines)
            {
                line.lineCommitId = ProductionPreparedOutputIdentity.BuildLineCommitId(
                    batchCommitId,
                    line.outputLineId);
            }
            batch = new ProductionPreparedOutputBatchSaveData
            {
                phase = ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace,
                billId = record.billId.Value,
                cycleSequence = record.cycleSequence,
                recipeId = recipe.RecipeId,
                destinationId = record.outputDestinationId,
                recipeDefinitionDigest = recipeDigest,
                migrationProfileDigest = ProductionPreparedOutputMigrationScope
                    .CaptureProfileDigest(recipe),
                capacitySourceDigest = capacitySource.SourceDigest,
                maximumMassProofDigest = capacityClaim.MaximumMassProof
                    .SourceDigest,
                maximumBatchMassGrams = capacityClaim.MaximumMassProof
                    .MaximumBatchMassGrams,
                capacityClaimDigest = capacityClaim.SourceDigest,
                outputBufferCycleCapacity = capacitySource.CycleCapacity,
                projectedPortfolioCapacityGrams = capacitySource
                    .ProjectedPortfolioCapacityGrams,
                requiredMinimumCapacityGrams = capacitySource
                    .RequiredMinimumCapacityGrams,
                outcomeFingerprint = outcomeFingerprint,
                batchCommitId = batchCommitId,
                totalPhysicalMassGrams = disposition.RecoverableWasteMassGrams,
                totalDeclaredLossMassGrams = disposition.DeclaredLossMassGrams,
                lines = lines
            };
            ProductionPreparedOutputContract.ValidateForBill(
                batch,
                record.billId,
                recipe.RecipeId,
                record.cycleSequence,
                record.outputDestinationId);
            if (!IsExactRuinedBatch(batch, recipe, disposition))
                throw new InvalidOperationException("Ruined output batch is not exact.");
            failure = DomainFailure.None;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or InvalidOperationException
                                           or KeyNotFoundException
                                           or OverflowException)
        {
            batch = null;
            failure = Fail(record, "ruined-output-resolution-failed", exception.Message);
            return false;
        }
    }

    internal ProductionOutputBufferCapacitySourceSnapshot ValidateCapacitySource(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionPreparedOutputBatchSaveData batch,
        ProductionFacilityHandle facility,
        string context)
    {
        if (record == null || recipe == null || batch == null)
            throw new ArgumentNullException(nameof(batch));
        ProductionOutputBufferCapacitySourceSnapshot current;
        ProductionPreparedOutputLineSaveData ruinedWaste = batch.lines?
            .SingleOrDefault(value => value != null
                && value.role == ProductionOutputRole.RecoverableWaste
                && string.Equals(
                    value.outputLineId,
                    ProductionRuinedBatchDispositionPlan
                        .RecoverableWasteOutputLineId,
                    StringComparison.Ordinal));
        if (ruinedWaste != null)
        {
            ProductionOutputCapabilityDescriptor descriptor = new(
                ruinedWaste.outputLineId,
                ruinedWaste.itemId,
                ruinedWaste.outputCapabilityId,
                ruinedWaste.outputCapabilityVersion,
                ruinedWaste.outputComponentCodecId,
                ruinedWaste.outputComponentCodecVersion,
                ruinedWaste.outputCapabilityFingerprint);
            ProductionRuinedOutputCapacityClaim claim = capacityProjector
                .CaptureRuinedClaim(record, recipe, descriptor);
            if (!string.Equals(
                    batch.maximumMassProofDigest,
                    claim.MaximumMassProof.SourceDigest,
                    StringComparison.Ordinal)
                || batch.maximumBatchMassGrams
                    != claim.MaximumMassProof.MaximumBatchMassGrams
                || !string.Equals(
                    batch.capacityClaimDigest,
                    claim.SourceDigest,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    (context ?? string.Empty)
                    + ":prepared-output-capacity-proof-stale");
            }
            current = capacityProjector.CaptureSource(facility, claim);
        }
        else
        {
            ProductionPreparedOutputCapacityClaim claim = capacityProjector
                .CapturePreparedClaim(batch, recipe);
            if (!string.Equals(
                    batch.maximumMassProofDigest,
                    claim.MaximumMassProof.SourceDigest,
                    StringComparison.Ordinal)
                || batch.maximumBatchMassGrams
                    != claim.MaximumMassProof.MaximumBatchMassGrams
                || !string.Equals(
                    batch.capacityClaimDigest,
                    claim.SourceDigest,
                    StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    (context ?? string.Empty)
                    + ":prepared-output-capacity-proof-stale");
            }
            current = capacityProjector.CaptureSource(facility, claim);
        }
        return ProductionOutputBufferCapacitySourceGuard.ValidateSaved(
            batch,
            current,
            context);
    }

    internal void ValidateResolvedSourceAuthority(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        string context)
    {
        if (record == null || recipe == null || facility == null)
            throw new ArgumentNullException(nameof(record));
        ProductionPreparedOutputBatchSaveData batch = record.preparedOutput
            ?? throw new InvalidOperationException(
                "Prepared output source validation has no batch.");
        ProductionPreparedOutputContract.ValidateForBill(
            batch,
            record.billId,
            recipe.RecipeId,
            record.cycleSequence,
            record.outputDestinationId);
        ProductionPreparedOutputMigrationScope.ValidateCanonicalProfileOrThrow(
            recipe);
        ProductionPreparedOutputSourceRevisionGuard.ValidateResolvedBatch(
            batch,
            recipe,
            context);
        ProductionPreparedOutputMigrationScope.ValidateSavedProfileDigest(
            batch,
            recipe,
            context);
        foreach (ProductionPreparedOutputLineSaveData line in batch.lines
                     .Where(value => ProductionOutputRoleRules.IsPhysical(
                         value.role)))
        {
            ProductionOutputCapabilityDescriptor capability = new(
                line.outputLineId,
                line.itemId,
                line.outputCapabilityId,
                line.outputCapabilityVersion,
                line.outputComponentCodecId,
                line.outputComponentCodecVersion,
                line.outputCapabilityFingerprint);
            if (!bridge.TryValidateOutputCapability(
                    capability,
                    out DomainFailure capabilityFailure))
            {
                throw new InvalidOperationException(
                    $"Prepared output line '{line.outputLineId}' has a missing or drifted output capability: {capabilityFailure.Code}.");
            }
            if (!catalog.TryGetItem(
                    line.itemId,
                    out ResourceItemDefinitionSO definition))
            {
                throw new InvalidOperationException(
                    $"Prepared output item '{line.itemId}' is missing.");
            }
            materializers.ValidateAndDecode(
                capability,
                definition,
                line.componentPayload,
                line.componentFingerprint);
        }
        ValidateCapacitySource(record, recipe, batch, facility, context);
    }

    internal bool TryValidateScope(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out DomainFailure failure)
    {
        if (record == null
            || recipe == null
            || facility == null
            || facility.IsDestroyed
            || ProductionPreparedOutputMigrationScope
                .HasLegacyOutputAuthority(record)
            || !ProductionPreparedOutputCapabilitySelection
                .UsesPreparedOutputMaterializer(recipe, bridge)
            || !string.Equals(record.recipeId, recipe.RecipeId, StringComparison.Ordinal)
            || !record.buildingInstanceId.Equals(facility.InstanceId)
            || !string.Equals(
                record.outputDestinationId,
                ProductionBillIdentity.OutputDestinationPrefix + facility.InstanceId.Value,
                StringComparison.Ordinal))
        {
            failure = Fail(record, "prepared-output-scope-invalid");
            return false;
        }
        failure = DomainFailure.None;
        return true;
    }

    internal bool TryCreateRuinedDisposition(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        out ProductionRuinedBatchDispositionPlan disposition,
        out ProductionRuinedOutputCapacityClaim capacityClaim,
        out DomainFailure failure)
    {
        disposition = default;
        capacityClaim = null;
        if (!TryValidateScope(record, recipe, facility, out failure))
            return false;
        if (recipe.ProcessKind != ProductionProcessKind.PassiveBatch
            || record.batchIntegrity > 0f
            || record.wipInputMassGrams <= 0L
            || record.processCleanWaterMassGrams < 0L
            || record.processWastewaterMassGrams < 0L
            || ProductionPreparedOutputMigrationScope
                .HasLegacyOutputAuthority(record))
        {
            failure = Fail(record, "ruined-output-capability-contract-invalid");
            return false;
        }
        if (recipe.CaptureCanonicalOutputs().Any(output => string.Equals(
                    output.OutputLineId,
                    ProductionRuinedBatchDispositionPlan
                        .RecoverableWasteOutputLineId,
                    StringComparison.Ordinal)
                || string.Equals(
                    output.OutputLineId,
                    ProductionRuinedBatchDispositionPlan.DeclaredLossOutputLineId,
                    StringComparison.Ordinal)))
        {
            failure = Fail(record, "ruined-output-reserved-line-id-conflict");
            return false;
        }

        try
        {
            if (!catalog.TryGetItem(
                    recipe.SpoilageItemId,
                    out ResourceItemDefinitionSO spoilageDefinition))
            {
                failure = Fail(
                    record,
                    "ruined-output-spoilage-item-missing",
                    recipe.SpoilageItemId);
                return false;
            }
            ProductionOutputCapabilityDescriptor spoilageCapability =
                bridge.CaptureOutputCapability(
                    ProductionRuinedBatchDispositionPlan
                        .RecoverableWasteOutputLineId,
                    recipe.SpoilageItemId);
            try
            {
                materializers.ValidateDescriptor(spoilageCapability);
            }
            catch (InvalidOperationException exception)
            {
                failure = Fail(
                    record,
                    "ruined-output-spoilage-capability-unsupported",
                    recipe.SpoilageItemId,
                    exception.Message);
                return false;
            }
            capacityClaim = capacityProjector.CaptureRuinedClaim(
                record,
                recipe,
                spoilageCapability);
            disposition = capacityClaim.Disposition;
            if (disposition.SpoilageUnitMassGrams <= 0L
                || checked(
                    disposition.RecoverableWasteMassGrams
                    + disposition.ProcessWastewaterMassGrams
                    + disposition.DeclaredLossMassGrams)
                    != disposition.AvailableMassGrams)
            {
                failure = Fail(record, "ruined-output-capability-mass-invalid");
                disposition = default;
                capacityClaim = null;
                return false;
            }
            failure = DomainFailure.None;
            return true;
        }
        catch (Exception exception) when (exception is ArgumentException
                                           or KeyNotFoundException
                                           or InvalidOperationException
                                           or OverflowException)
        {
            failure = Fail(
                record,
                "ruined-output-capability-plan-failed",
                exception.Message);
            disposition = default;
            capacityClaim = null;
            return false;
        }
    }

    internal static bool IsExactRuinedBatch(
        ProductionPreparedOutputBatchSaveData batch,
        ProductionRecipeSO recipe,
        ProductionRuinedBatchDispositionPlan disposition)
    {
        if (batch == null
            || recipe == null
            || !string.Equals(
                batch.recipeId,
                recipe.RecipeId,
                StringComparison.Ordinal)
            || batch.totalPhysicalMassGrams !=
                disposition.RecoverableWasteMassGrams
            || batch.totalDeclaredLossMassGrams !=
                disposition.DeclaredLossMassGrams
            || batch.lines == null
            || batch.lines.Any(value => value == null)
            || batch.lines.Select(value => value.outputLineId)
                .Distinct(StringComparer.Ordinal).Count() != batch.lines.Count)
        {
            return false;
        }
        ProductionOutputDefinition[] authored = recipe.CaptureCanonicalOutputs()
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        int expectedLineCount = authored.Length + 1
            + (disposition.DeclaredLossMassGrams > 0L ? 1 : 0);
        if (batch.lines.Count != expectedLineCount)
            return false;
        foreach (ProductionOutputDefinition output in authored)
        {
            ProductionPreparedOutputLineSaveData failed = batch.lines
                .SingleOrDefault(value => string.Equals(
                    value.outputLineId,
                    output.OutputLineId,
                    StringComparison.Ordinal));
            if (failed == null
                || failed.role != output.Role
                || !string.Equals(
                    failed.itemId,
                    output.ItemId,
                    StringComparison.Ordinal)
                || failed.rollSucceeded
                || failed.quantity != 0
                || failed.exactMassGrams != 0L)
            {
                return false;
            }
        }
        ProductionPreparedOutputLineSaveData waste = batch.lines.SingleOrDefault(
            value => value != null
                && value.role == ProductionOutputRole.RecoverableWaste
                && string.Equals(
                    value.outputLineId,
                    ProductionRuinedBatchDispositionPlan
                        .RecoverableWasteOutputLineId,
                    StringComparison.Ordinal));
        ProductionPreparedOutputLineSaveData loss = batch.lines.SingleOrDefault(
            value => value != null
                && value.role == ProductionOutputRole.DeclaredLoss
                && string.Equals(
                    value.outputLineId,
                    ProductionRuinedBatchDispositionPlan
                        .DeclaredLossOutputLineId,
                    StringComparison.Ordinal));
        return waste != null
            && string.Equals(
                waste.itemId,
                disposition.SpoilageItemId,
                StringComparison.Ordinal)
            && waste.rollSucceeded
            && waste.quantity == disposition.RecoverableWasteQuantity
            && waste.exactMassGrams == disposition.RecoverableWasteMassGrams
            && (disposition.DeclaredLossMassGrams == 0L
                ? loss == null
                : loss != null
                    && loss.rollSucceeded
                    && loss.quantity == 0
                    && loss.itemId.Length == 0
                    && loss.exactMassGrams ==
                        disposition.DeclaredLossMassGrams);
    }

    internal static DomainFailure Fail(
        ProductionBillRecord record,
        params string[] details)
    {
        string[] parameters = new[] { record?.billId.Value ?? string.Empty }
            .Concat(details ?? Array.Empty<string>())
            .ToArray();
        return new DomainFailure(FailureCode.ProductionOutputUnavailable, parameters);
    }

    private static string ComputeRecipeDigest(ProductionRecipeSO recipe)
        => ProductionRecipeSemanticDigest.Capture(recipe);

    private static string BuildProcessLossOutputLineId(string recipeId) =>
        "output:" + recipeId + "/999/declared-loss/process-loss";

    private static string BuildProcessExternalInputOutputLineId(
        string recipeId) =>
        "output:" + recipeId
        + "/999/declared-external-input/process-addition";

    private static string ComputeOutcomeFingerprint(
        CanonicalProductionOutputResolution resolution,
        IEnumerable<ProductionPreparedOutputLineSaveData> lines)
    {
        StringBuilder text = new();
        Append(text, "production-prepared-outcome-v2");
        Append(text, resolution.RootSeed.ToString(CultureInfo.InvariantCulture));
        Append(text, resolution.BillId.Value);
        Append(text, resolution.CycleSequence.ToString(CultureInfo.InvariantCulture));
        Append(text, resolution.RecipeId);
        Append(text, resolution.OutputFactorNumerator.ToString(CultureInfo.InvariantCulture));
        Append(text, resolution.OutputFactorDenominator.ToString(CultureInfo.InvariantCulture));
        Append(text, resolution.PassiveBatchIntegrity.ToString("R", CultureInfo.InvariantCulture));
        foreach (ProductionPreparedOutputLineSaveData line in lines
                     .OrderBy(value => value.outputLineId, StringComparer.Ordinal))
        {
            Append(text, line.outputLineId);
            Append(text, ((int)line.role).ToString(CultureInfo.InvariantCulture));
            Append(text, line.itemId);
            Append(text, line.outputCapabilityId);
            Append(text, line.outputCapabilityVersion.ToString(
                CultureInfo.InvariantCulture));
            Append(text, line.outputComponentCodecId);
            Append(text, line.outputComponentCodecVersion.ToString(
                CultureInfo.InvariantCulture));
            Append(text, line.outputCapabilityFingerprint);
            Append(text, line.quantity.ToString(CultureInfo.InvariantCulture));
            Append(text, line.exactMassGrams.ToString(CultureInfo.InvariantCulture));
            Append(text, line.componentFingerprint);
            Append(text, line.rollSucceeded ? "1" : "0");
        }
        return Sha256(text.ToString());
    }

    private static string ComputeRuinedOutcomeFingerprint(
        ProductionBillRecord record,
        ProductionRecipeSO recipe,
        ProductionRuinedBatchDispositionPlan disposition,
        ProductionRuinedOutputCapacityClaim capacityClaim,
        IEnumerable<ProductionPreparedOutputLineSaveData> lines)
    {
        StringBuilder text = new();
        Append(text, "production-ruined-outcome-v4");
        Append(text, record.billId.Value);
        Append(text, record.cycleSequence.ToString(CultureInfo.InvariantCulture));
        Append(text, recipe.RecipeId);
        Append(text, record.wipInputMassGrams.ToString(CultureInfo.InvariantCulture));
        Append(text, record.processCleanWaterMassGrams.ToString(
            CultureInfo.InvariantCulture));
        Append(text, record.processWastewaterMassGrams.ToString(
            CultureInfo.InvariantCulture));
        Append(text, disposition.SpoilageItemId);
        Append(text, capacityClaim.SourceDigest);
        Append(text, capacityClaim.MaximumMassProof.SourceDigest);
        Append(text, capacityClaim.MaximumMassProof.MaximumBatchMassGrams
            .ToString(CultureInfo.InvariantCulture));
        foreach (ProductionPreparedOutputLineSaveData line in lines
                     .OrderBy(value => value.outputLineId, StringComparer.Ordinal))
        {
            Append(text, line.outputLineId);
            Append(text, ((int)line.role).ToString(CultureInfo.InvariantCulture));
            Append(text, line.itemId);
            Append(text, line.outputCapabilityId);
            Append(text, line.outputCapabilityVersion.ToString(
                CultureInfo.InvariantCulture));
            Append(text, line.outputComponentCodecId);
            Append(text, line.outputComponentCodecVersion.ToString(
                CultureInfo.InvariantCulture));
            Append(text, line.outputCapabilityFingerprint);
            Append(text, line.quantity.ToString(CultureInfo.InvariantCulture));
            Append(text, line.exactMassGrams.ToString(CultureInfo.InvariantCulture));
            Append(text, line.componentFingerprint);
            Append(text, line.rollSucceeded ? "1" : "0");
        }
        return Sha256(text.ToString());
    }

    private static void Append(StringBuilder target, string value)
    {
        string canonical = value ?? string.Empty;
        target.Append(Encoding.UTF8.GetByteCount(canonical).ToString(
            CultureInfo.InvariantCulture));
        target.Append(':').Append(canonical).Append('|');
    }

    private static string Sha256(string value)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(Encoding.UTF8.GetBytes(value));
        StringBuilder result = new(digest.Length * 2);
        foreach (byte part in digest)
            result.Append(part.ToString("x2", CultureInfo.InvariantCulture));
        return result.ToString();
    }
}
