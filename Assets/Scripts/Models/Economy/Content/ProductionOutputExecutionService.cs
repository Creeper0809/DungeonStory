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

    DomainFailure ProduceOne(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        ProductionWorkerHandle worker,
        ProductionResolvedOutputSaveData output,
        string outputDestinationId,
        string commitId,
        out long committedMassGrams);

    DomainFailure AcknowledgeOne(
        string itemId,
        string commitId);
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
        foreach (ProductionOutputDefinition output in recipe.Outputs)
        {
            if (output == null || !random.Chance(output.Probability))
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

            resolved.Add(new ProductionResolvedOutputSaveData
            {
                itemId = output.ItemId,
                amount = outputAmount,
                qualityModifier = qualityModifier,
                workerQuality = workerQuality
            });
        }
        return resolved
            .GroupBy(output => output.itemId, StringComparer.Ordinal)
            .Select(group => new ProductionResolvedOutputSaveData
            {
                itemId = group.Key,
                amount = checked(group.Sum(output => output.amount)),
                committedAmount = 0,
                qualityModifier = group.First().qualityModifier,
                workerQuality = group.First().workerQuality
            })
            .OrderBy(output => output.itemId, StringComparer.Ordinal)
            .ToArray();
    }

    public DomainFailure ProduceOne(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        ProductionWorkerHandle worker,
        ProductionResolvedOutputSaveData output,
        string outputDestinationId,
        string commitId,
        out long committedMassGrams)
    {
        committedMassGrams = 0L;
        if (recipe == null
            || facility == null
            || output == null
            || output.committedAmount >= output.amount
            || string.IsNullOrEmpty(commitId))
        {
            return new DomainFailure(FailureCode.ProductionOutputUnavailable);
        }
        if (!bridge.TryHandleOutput(
                recipe,
                facility,
                worker,
                output.itemId,
                1,
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
        if (!handled && !bridge.TryCommitBufferedOutput(
                commitId,
                output.itemId,
                1,
                facility.Position,
                outputDestinationId,
                out DomainFailure bufferedFailure))
        {
            return bufferedFailure.IsFailure
                ? bufferedFailure
                : new DomainFailure(
                    FailureCode.ProductionOutputUnavailable,
                    output.itemId);
        }

        if (!bridge.TryGetCommittedOutputMassGrams(
                output.itemId,
                commitId,
                out committedMassGrams,
                out DomainFailure massFailure)
            || committedMassGrams <= 0L)
        {
            committedMassGrams = 0L;
            return massFailure.IsFailure
                ? massFailure
                : new DomainFailure(
                    FailureCode.ProductionOutputUnavailable,
                    output.itemId,
                    "commit-mass-missing");
        }

        return DomainFailure.None;
    }

    public DomainFailure AcknowledgeOne(
        string itemId,
        string commitId)
    {
        bool succeeded = bridge.AcknowledgeHandledOutput(
            itemId,
            commitId,
            out DomainFailure failure);
        return succeeded
            ? DomainFailure.None
            : failure.IsFailure
                ? failure
                : new DomainFailure(
                    FailureCode.ProductionOutputUnavailable,
                    itemId,
                    "commit-ack-failed");
    }

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
