using System;

public readonly struct BuildingCraftsmanshipDefinitionMaximumSnapshot
{
    public BuildingCraftsmanshipDefinitionMaximumSnapshot(
        string facilityDefinitionId,
        CraftsmanshipQualityTier maximumTier,
        double maximumMultiplier,
        string sourceDigest)
    {
        if (string.IsNullOrWhiteSpace(facilityDefinitionId)
            || !string.Equals(
                facilityDefinitionId,
                facilityDefinitionId.Trim(),
                StringComparison.Ordinal)
            || !Enum.IsDefined(typeof(CraftsmanshipQualityTier), maximumTier)
            || double.IsNaN(maximumMultiplier)
            || double.IsInfinity(maximumMultiplier)
            || maximumMultiplier <= 0d
            || sourceDigest == null
            || sourceDigest.Length != 64)
        {
            throw new ArgumentException(
                "Building craftsmanship definition maximum is invalid.");
        }
        FacilityDefinitionId = facilityDefinitionId;
        MaximumTier = maximumTier;
        MaximumMultiplier = maximumMultiplier;
        SourceDigest = sourceDigest;
    }

    public string FacilityDefinitionId { get; }
    public CraftsmanshipQualityTier MaximumTier { get; }
    public double MaximumMultiplier { get; }
    public string SourceDigest { get; }
}

public interface IBuildingCraftsmanshipDefinitionMaximumQuery
{
    BuildingCraftsmanshipDefinitionMaximumSnapshot Capture(
        string facilityDefinitionId);
}

/// <summary>
/// Definition-only maximum across every quality tier accepted by current
/// building restore validation. New enum values must receive an authored
/// ProjectionMultiplier or this query fails loudly.
/// </summary>
public sealed class BuildingCraftsmanshipDefinitionMaximumQuery :
    IBuildingCraftsmanshipDefinitionMaximumQuery
{
    public const string Schema =
        "building-craftsmanship-definition-maximum@1";

    public BuildingCraftsmanshipDefinitionMaximumSnapshot Capture(
        string facilityDefinitionId)
    {
        if (string.IsNullOrWhiteSpace(facilityDefinitionId)
            || !string.Equals(
                facilityDefinitionId,
                facilityDefinitionId.Trim(),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical facility definition ID is required.",
                nameof(facilityDefinitionId));
        }

        CraftsmanshipQualityTier maximumTier = default;
        double maximum = double.NegativeInfinity;
        CraftsmanshipQualityTier[] tiers =
            (CraftsmanshipQualityTier[])Enum.GetValues(
                typeof(CraftsmanshipQualityTier));
        Array.Sort(tiers);
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(facilityDefinitionId);
        digest.Append(tiers.Length);
        foreach (CraftsmanshipQualityTier tier in tiers)
        {
            double multiplier = CraftsmanshipQualityRules
                .ProjectionMultiplier(tier);
            if (double.IsNaN(multiplier)
                || double.IsInfinity(multiplier)
                || multiplier <= 0d)
            {
                throw new InvalidOperationException(
                    "Craftsmanship tier has no finite positive multiplier: "
                    + tier);
            }
            digest.AppendEnum(tier);
            digest.AppendDouble(multiplier);
            if (multiplier > maximum)
            {
                maximum = multiplier;
                maximumTier = tier;
            }
        }
        digest.AppendEnum(maximumTier);
        digest.AppendDouble(maximum);
        return new BuildingCraftsmanshipDefinitionMaximumSnapshot(
            facilityDefinitionId,
            maximumTier,
            maximum,
            digest.ComputeSha256());
    }
}
