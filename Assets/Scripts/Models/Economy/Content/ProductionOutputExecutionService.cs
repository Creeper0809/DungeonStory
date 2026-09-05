using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

public interface IProductionOutputExecutionService
{
    float ResolveWorkSpeedMultiplier(
        ProductionFacilityHandle facility,
        ProductionRecipeSO recipe);

    IReadOnlyList<ProductionResolvedOutputSaveData> ResolveAll(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        ProductionWorkerHandle worker,
        float batchIntegrity);

    DomainFailure ValidateOne(ProductionResolvedOutputSaveData output);

    DomainFailure ProduceOne(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        ProductionWorkerHandle worker,
        ProductionResolvedOutputSaveData output,
        string outputDestinationId,
        string commitId,
        out ProductionCommittedOutputSnapshot committedOutput,
        out ProductionOutputPublicationExposure publicationExposure);

    DomainFailure AcknowledgeOne(
        ProductionResolvedOutputSaveData output,
        string commitId);
}

public enum ProductionOutputPublicationExposure
{
    None = 0,
    PhysicalCommitMayExist = 1
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ProductionOutputExecutionService :
    IProductionOutputExecutionService
{
    private readonly IProductionAssemblyBridge bridge;
    private readonly IGrandProjectBenefitQuery grandProjectBenefits;
    private readonly IProductionOutputPlanningService outputPlanning;
    private readonly IRandomStream random;

    public ProductionOutputExecutionService(
        IProductionAssemblyBridge bridge,
        IGrandProjectBenefitQuery grandProjectBenefits,
        IProductionOutputPlanningService outputPlanning,
        IRandomStreamProvider randomStreamProvider)
    {
        this.bridge = bridge ?? throw new ArgumentNullException(nameof(bridge));
        this.grandProjectBenefits = grandProjectBenefits
            ?? throw new ArgumentNullException(nameof(grandProjectBenefits));
        this.outputPlanning = outputPlanning
            ?? throw new ArgumentNullException(nameof(outputPlanning));
        random = (randomStreamProvider
                ?? throw new ArgumentNullException(nameof(randomStreamProvider)))
            .Get("economy:production");
    }

    public float ResolveWorkSpeedMultiplier(
        ProductionFacilityHandle facility,
        ProductionRecipeSO recipe) => outputPlanning.ResolveSupportModifier(
            facility,
            recipe,
            ProductionSupportModifierKind.WorkSpeed,
            1f,
            multiply: true);

    public IReadOnlyList<ProductionResolvedOutputSaveData> ResolveAll(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        ProductionWorkerHandle worker,
        float batchIntegrity)
    {
        if (recipe == null || facility == null)
        {
            return Array.Empty<ProductionResolvedOutputSaveData>();
        }

        ProductionOutputDefinition[] physical = recipe
            .CaptureCanonicalOutputs()
            .Where(output => output != null
                && ProductionOutputRoleRules.IsPhysical(output.Role)
                && output.Amount > 0
                && output.Probability > 0f)
            .OrderBy(output => output.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        ProductionOutputCapabilityDescriptor[] capabilities =
            new ProductionOutputCapabilityDescriptor[physical.Length];
        for (int i = 0; i < physical.Length; i++)
        {
            capabilities[i] = bridge.CaptureOutputCapability(
                physical[i].OutputLineId,
                physical[i].ItemId);
        }
        ProductionOutputCapabilityRoute route =
            ProductionPreparedOutputCapabilitySelection
                .ClassifyPhysicalCapabilities(
                    capabilities,
                    bridge.OutputCapabilityContracts);
        if (route != ProductionOutputCapabilityRoute.ExactCapability)
        {
            throw new InvalidOperationException(
                route == ProductionOutputCapabilityRoute.PreparedBatch
                    ? "materialized-output-requires-prepared-batch"
                    : "production-output-capability-route-empty");
        }

        List<ProductionResolvedOutputSaveData> resolved = new();
        float qualityModifier = outputPlanning.ResolveSupportModifier(
            facility,
            recipe,
            ProductionSupportModifierKind.Quality,
            0f,
            multiply: false);
        float workerQuality = Mathf.Clamp(
            bridge.GetRelevantCraftSkill(worker, recipe) / 58f,
            0.7f,
            1.25f);
        for (int i = 0; i < physical.Length; i++)
        {
            ProductionOutputDefinition output = physical[i];
            if (!random.Chance(output.Probability))
            {
                continue;
            }

            int outputAmount = ResolveOutputAmount(
                output.Amount,
                ProductionOutputFactorAuthority
                    .ResolveCurrent(grandProjectBenefits, recipe.FacilityTag)
                    .Multiply(ProductionOutputFactor.FromAuthoredMultiplier(
                        outputPlanning.ResolveSupportModifier(
                            facility,
                            recipe,
                            ProductionSupportModifierKind.Output,
                            1f,
                            multiply: true))));
            if (recipe.ProcessKind == ProductionProcessKind.PassiveBatch
                && batchIntegrity < 50f)
            {
                outputAmount = Mathf.Max(
                    1,
                    Mathf.FloorToInt(outputAmount * 0.5f));
            }

            ProductionOutputCapabilityDescriptor capability = capabilities[i];
            resolved.Add(new ProductionResolvedOutputSaveData
            {
                outputLineId = output.OutputLineId,
                itemId = output.ItemId,
                outputCapabilityId = capability.CapabilityId,
                outputCapabilityVersion = capability.CapabilityVersion,
                outputComponentCodecId = capability.ComponentCodecId,
                outputComponentCodecVersion = capability.ComponentCodecVersion,
                outputCapabilityFingerprint = capability.Fingerprint,
                amount = outputAmount,
                qualityModifier = qualityModifier,
                workerQuality = workerQuality
            });
        }
        return resolved
            .OrderBy(output => output.outputLineId, StringComparer.Ordinal)
            .ToArray();
    }

    public DomainFailure ProduceOne(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        ProductionWorkerHandle worker,
        ProductionResolvedOutputSaveData output,
        string outputDestinationId,
        string commitId,
        out ProductionCommittedOutputSnapshot committedOutput,
        out ProductionOutputPublicationExposure publicationExposure)
    {
        committedOutput = null;
        publicationExposure = ProductionOutputPublicationExposure.None;
        if (recipe == null
            || facility == null
            || output == null
            || output.committedAmount >= output.amount
            || string.IsNullOrEmpty(commitId))
        {
            return new DomainFailure(FailureCode.ProductionOutputUnavailable);
        }
        ProductionOutputCapabilityDescriptor capability = ToDescriptor(output);
        if (!bridge.TryHandleOutput(
                recipe,
                facility,
                worker,
                capability,
                1,
                outputDestinationId,
                output.qualityModifier,
                output.workerQuality,
                commitId,
                out bool handled,
                out DomainFailure failure))
        {
            return failure.IsFailure
                ? failure
                : new DomainFailure(
                    FailureCode.ProductionOutputUnavailable,
                    output.itemId);
        }
        if (!handled)
        {
            return new DomainFailure(
                FailureCode.ProductionOutputUnavailable,
                output.itemId,
                "frozen-output-capability-not-handled");
        }

        publicationExposure =
            ProductionOutputPublicationExposure.PhysicalCommitMayExist;

        if (!bridge.TryCaptureCommittedOutput(
                recipe,
                facility,
                worker,
                capability,
                1,
                outputDestinationId,
                output.qualityModifier,
                output.workerQuality,
                commitId,
                out committedOutput,
                out DomainFailure massFailure)
            || committedOutput == null
            || committedOutput.ExactMassGrams <= 0L)
        {
            committedOutput = null;
            return massFailure.IsFailure
                ? massFailure
                : new DomainFailure(
                    FailureCode.ProductionOutputUnavailable,
                    output.itemId,
                    "commit-mass-missing");
        }

        return DomainFailure.None;
    }

    public DomainFailure ValidateOne(ProductionResolvedOutputSaveData output)
    {
        if (output == null)
            return new DomainFailure(FailureCode.ProductionOutputUnavailable);
        return bridge.TryValidateOutputCapability(
            ToDescriptor(output),
            out DomainFailure failure)
            ? DomainFailure.None
            : failure.IsFailure
                ? failure
                : new DomainFailure(
                    FailureCode.ProductionOutputUnavailable,
                    output.itemId,
                    "output-capability-validation-failed");
    }

    public DomainFailure AcknowledgeOne(
        ProductionResolvedOutputSaveData output,
        string commitId)
    {
        if (output == null)
            return new DomainFailure(FailureCode.ProductionOutputUnavailable);
        bool succeeded = bridge.AcknowledgeHandledOutput(
            ToDescriptor(output),
            commitId,
            out DomainFailure failure);
        return succeeded
            ? DomainFailure.None
            : failure.IsFailure
                ? failure
                : new DomainFailure(
                    FailureCode.ProductionOutputUnavailable,
                    output.itemId,
                    "commit-ack-failed");
    }

    private static ProductionOutputCapabilityDescriptor ToDescriptor(
        ProductionResolvedOutputSaveData output) => new(
        output.outputLineId,
        output.itemId,
        output.outputCapabilityId,
        output.outputCapabilityVersion,
        output.outputComponentCodecId,
        output.outputComponentCodecVersion,
        output.outputCapabilityFingerprint);

    private int ResolveOutputAmount(
        int baseAmount,
        ProductionOutputFactor multiplier)
    {
        decimal scaled = multiplier.Scale(Mathf.Max(0, baseAmount));
        decimal whole = decimal.Floor(scaled);
        if (whole > int.MaxValue)
            throw new OverflowException("Production output quantity exceeds Int32.");
        decimal remainder = scaled - whole;
        return Mathf.Max(
            1,
            checked((int)whole)
            + (remainder > 0m && random.Chance((float)remainder) ? 1 : 0));
    }
}
