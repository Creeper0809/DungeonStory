using System;
using System.Linq;

public sealed class CertifiedSeedFacilityOutputCapacityContributor :
    IProductionFacilityOutputCapacityContributor
{
    public const string Id =
        "production-facility-output-capacity:certified-seed";
    public const int Version = 1;
    private readonly IResourceEconomyContentCatalog catalog;

    public CertifiedSeedFacilityOutputCapacityContributor(
        IResourceEconomyContentCatalog catalog)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public string ContributorId => Id;
    public int ContractVersion => Version;

    public ProductionFacilityOutputCapacityContribution Capture(
        ProductionFacilityCapacitySubject subject)
    {
        bool applies = CertifiedSeedFacilityEligibility.IsEligible(subject);
        ProductionFacilityOutputCapacityBranch[] branches = !applies
            ? Array.Empty<ProductionFacilityOutputCapacityBranch>()
            : (catalog.Crops ?? Array.Empty<CropDefinitionSO>())
                .Where(value => value != null)
                .OrderBy(value => value.CropId, StringComparer.Ordinal)
                .Select(value => new ProductionFacilityOutputCapacityBranch(
                    CertifiedSeedFacilityOutputBranchIdentity.ForCrop(
                        value.CropId),
                    new[]
                    {
                        new ProductionFacilityOutputMaximumMassRequest(
                            CertifiedSeedOutputCapability.OutputLineId,
                            value.SeedItemId,
                            ProductionOutputCapabilityIds.CertifiedSeed,
                            1)
                    }))
                .ToArray();
        return new ProductionFacilityOutputCapacityContribution(
            Id,
            Version,
            applies,
            branches);
    }
}
