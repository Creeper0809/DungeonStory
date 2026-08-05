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

    DomainFailure ProduceAll(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        ProductionWorkerHandle worker,
        float batchIntegrity,
        string outputDestinationId);
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

    public DomainFailure ProduceAll(
        ProductionRecipeSO recipe,
        ProductionFacilityHandle facility,
        ProductionWorkerHandle worker,
        float batchIntegrity,
        string outputDestinationId)
    {
        if (recipe == null || facility == null)
        {
            return new DomainFailure(FailureCode.ProductionOutputUnavailable);
        }

        foreach (ProductionOutputDefinition output in recipe.Outputs)
        {
            if (output == null || !random.Chance(output.Probability))
            {
                continue;
            }

            int outputAmount = ResolveOutputAmount(
                output.Amount,
                grandProjectBenefits.GetProductionOutputMultiplier(
                    recipe.FacilityTag)
                * outputPlanning.ResolveSupportModifier(
                    facility,
                    recipe,
                    ProductionSupportModifierKind.Output,
                    1f,
                    multiply: true));
            if (recipe.ProcessKind == ProductionProcessKind.PassiveBatch
                && batchIntegrity < 50f)
            {
                outputAmount = Mathf.Max(
                    1,
                    Mathf.FloorToInt(outputAmount * 0.5f));
            }

            float qualityModifier = outputPlanning.ResolveSupportModifier(
                facility,
                recipe,
                ProductionSupportModifierKind.Quality,
                0f,
                multiply: false);
            if (!bridge.TryHandleOutput(
                    recipe,
                    facility,
                    worker,
                    output.ItemId,
                    outputAmount,
                    qualityModifier,
                    out bool handled,
                    out DomainFailure failure))
            {
                return failure.IsFailure
                    ? failure
                    : new DomainFailure(
                        FailureCode.ProductionOutputUnavailable,
                        output.ItemId);
            }
            if (handled)
            {
                continue;
            }

            bool spawned = bridge.SpawnBufferedOutput(
                output.ItemId,
                outputAmount,
                facility.Position,
                outputDestinationId);
            if (!spawned)
            {
                return new DomainFailure(
                    FailureCode.ProductionOutputUnavailable,
                    output.ItemId);
            }
        }

        return DomainFailure.None;
    }

    private int ResolveOutputAmount(int baseAmount, float multiplier)
    {
        float scaled = Mathf.Max(0f, baseAmount) * Mathf.Max(0f, multiplier);
        int whole = Mathf.FloorToInt(scaled);
        float remainder = scaled - whole;
        return Mathf.Max(
            1,
            whole + (remainder > 0f && random.Chance(remainder) ? 1 : 0));
    }
}
