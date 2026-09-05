using System;
using System.Collections.Generic;
using System.Linq;

public sealed class ApparelFacilityOutputCapacityContributor :
    IProductionFacilityOutputCapacityContributor
{
    public const string Id =
        "production-facility-output-capacity:apparel";
    public const int Version = 1;

    private readonly IApparelDefinitionCatalog apparel;
    private readonly ITextileMaterialCatalog materials;
    private readonly IPhysicalItemMassQuery mass;
    private readonly IGameplayEffectResultBoundsQuery effectBounds;

    public ApparelFacilityOutputCapacityContributor(
        IApparelDefinitionCatalog apparel,
        ITextileMaterialCatalog materials,
        IPhysicalItemMassQuery mass,
        IGameplayEffectResultBoundsQuery effectBounds)
    {
        this.apparel = apparel ?? throw new ArgumentNullException(nameof(apparel));
        this.materials = materials
            ?? throw new ArgumentNullException(nameof(materials));
        this.mass = mass ?? throw new ArgumentNullException(nameof(mass));
        this.effectBounds = effectBounds
            ?? throw new ArgumentNullException(nameof(effectBounds));
    }

    public string ContributorId => Id;
    public int ContractVersion => Version;

    public ProductionFacilityOutputCapacityContribution Capture(
        ProductionFacilityCapacitySubject subject)
    {
        bool applies = ApparelTailoringFacilityEligibility.IsEligible(subject);
        if (!applies)
        {
            return new ProductionFacilityOutputCapacityContribution(
                Id,
                Version,
                false,
                Array.Empty<ProductionFacilityOutputCapacityBranch>());
        }

        List<ProductionFacilityOutputCapacityBranch> branches = new();
        ApparelDefinitionSO[] orderedApparel = apparel.Definitions
            .Where(value => value != null)
            .OrderBy(value => value.ApparelId, StringComparer.Ordinal)
            .ToArray();
        TextileMaterialDefinitionSO[] orderedMaterials = materials.Definitions
            .Where(value => value != null)
            .OrderBy(value => value.MaterialId, StringComparer.Ordinal)
            .ToArray();
        float salvageMaximum = effectBounds.RequireFiniteMaximum(
            GameplayEffectTargetIds.SalvageYield);
        if (salvageMaximum < 0f)
        {
            throw new InvalidOperationException(
                "Apparel salvage maximum cannot be negative.");
        }

        foreach (ApparelDefinitionSO definition in orderedApparel)
        {
            branches.Add(new ProductionFacilityOutputCapacityBranch(
                ApparelFacilityOutputBranchIdentity.Craft(
                    definition.ApparelId),
                new[]
                {
                    new ProductionFacilityOutputMaximumMassRequest(
                        ApparelPhysicalTransaction.OutputLineId,
                        definition.PhysicalItemId,
                        ProductionOutputCapabilityIds.ApparelWorkOrder,
                        1)
                }));

            long apparelMass = mass.GetDefinitionUnitMass(
                (ItemDefinitionId)definition.PhysicalItemId).Value;
            int requiredInput = UnityEngine.Mathf.CeilToInt(
                2f * definition.TailoringCoefficient);
            int effectQuantity = checked((int)Math.Floor(
                requiredInput * 0.5d * salvageMaximum));
            foreach (TextileMaterialDefinitionSO material in orderedMaterials)
            {
                if ((material.Tags & definition.AllowedMaterialTags) == 0)
                    continue;
                long materialMass = mass.GetDefinitionUnitMass(
                    (ItemDefinitionId)material.PhysicalItemId).Value;
                if (apparelMass <= 0L || materialMass <= 0L)
                {
                    throw new InvalidOperationException(
                        "Apparel capacity projection requires positive item mass.");
                }
                int massQuantity = checked((int)(apparelMass / materialMass));
                int quantity = Math.Min(effectQuantity, massQuantity);
                if (quantity <= 0)
                    continue;
                branches.Add(new ProductionFacilityOutputCapacityBranch(
                    ApparelFacilityOutputBranchIdentity.RejectedRecovery(
                        definition.ApparelId,
                        material.MaterialId),
                    new[]
                    {
                        new ProductionFacilityOutputMaximumMassRequest(
                            ApparelPhysicalTransaction.RejectedRecoveryOutputLineId,
                            material.PhysicalItemId,
                            ProductionOutputCapabilityIds.StandardDefinition,
                            quantity)
                    }));
            }
        }

        return new ProductionFacilityOutputCapacityContribution(
            Id,
            Version,
            true,
            branches);
    }
}
