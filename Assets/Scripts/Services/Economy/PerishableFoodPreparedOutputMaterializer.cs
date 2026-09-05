using System;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>
/// Creates the initial freshness state before a perishable output enters
/// FacilityBuffer/exact-route custody. Freshness affects stacking and custody
/// identity but not physical mass, so the mass subject remains definition-only.
/// </summary>
public sealed class PerishableFoodPreparedOutputMaterializer :
    IProductionPreparedOutputMaterializer
{
    private const string ProfileSchema =
        "production-prepared-output-perishable-food-profile@1";
    private const string PayloadPrefix =
        "production-prepared-output-components@1|kind=perishable-food|item=";

    public string CapabilityId =>
        ProductionOutputCapabilityIds.PerishableFood;
    public int CapabilityVersion =>
        ProductionOutputCapabilityIds.PerishableFoodVersion;
    public string ComponentCodecId =>
        ProductionOutputCapabilityIds.PerishableFoodFreshnessCodec;
    public int ComponentCodecVersion =>
        ProductionOutputCapabilityIds.PerishableFoodFreshnessCodecVersion;

    public ProductionPreparedOutputComponentProjection Create(
        ProductionOutputCapabilityDescriptor descriptor,
        ItemDefinitionSO definition)
    {
        ResourceItemDefinitionSO resource = RequireDefinition(
            descriptor,
            definition,
            out FoodItemFeature food);
        ItemInstanceComponentSaveData freshness =
            FoodFreshnessComponentCodec.Create(
                food.freshnessSeconds,
                food.preserved);
        string payload = BuildPayload(resource.ItemId, freshness);
        return CreateProjection(
            resource,
            freshness,
            payload,
            CaptureFingerprint(resource, payload));
    }

    public ProductionPreparedOutputComponentProjection ValidateAndDecode(
        ProductionOutputCapabilityDescriptor descriptor,
        ItemDefinitionSO definition,
        string canonicalPayload,
        string fingerprint)
    {
        ResourceItemDefinitionSO resource = RequireDefinition(
            descriptor,
            definition,
            out FoodItemFeature food);
        ItemInstanceComponentSaveData freshness =
            FoodFreshnessComponentCodec.Create(
                food.freshnessSeconds,
                food.preserved);
        string expectedPayload = BuildPayload(resource.ItemId, freshness);
        if (!string.Equals(
                canonicalPayload,
                expectedPayload,
                StringComparison.Ordinal))
        {
            throw new ProductionPreparedOutputComponentCodecException(
                ProductionPreparedOutputComponentFailureCode
                    .NonCanonicalPayload,
                $"Prepared perishable food '{resource.ItemId}' has a noncanonical freshness payload.");
        }

        string expectedFingerprint = CaptureFingerprint(
            resource,
            expectedPayload);
        if (!IsLowercaseSha256(fingerprint)
            || !string.Equals(
                fingerprint,
                expectedFingerprint,
                StringComparison.Ordinal))
        {
            throw new ProductionPreparedOutputComponentCodecException(
                ProductionPreparedOutputComponentFailureCode
                    .FingerprintMismatch,
                $"Prepared perishable food '{resource.ItemId}' has a mismatched freshness fingerprint.");
        }

        return CreateProjection(
            resource,
            freshness,
            expectedPayload,
            expectedFingerprint);
    }

    private static ResourceItemDefinitionSO RequireDefinition(
        ProductionOutputCapabilityDescriptor descriptor,
        ItemDefinitionSO definition,
        out FoodItemFeature food)
    {
        food = null;
        if (definition is not ResourceItemDefinitionSO resource
            || !Canonical(resource.ItemId)
            || !string.Equals(
                descriptor.ItemId,
                resource.ItemId,
                StringComparison.Ordinal)
            || !string.Equals(
                descriptor.CapabilityId,
                ProductionOutputCapabilityIds.PerishableFood,
                StringComparison.Ordinal)
            || descriptor.CapabilityVersion !=
                ProductionOutputCapabilityIds.PerishableFoodVersion
            || !string.Equals(
                descriptor.ComponentCodecId,
                ProductionOutputCapabilityIds.PerishableFoodFreshnessCodec,
                StringComparison.Ordinal)
            || descriptor.ComponentCodecVersion !=
                ProductionOutputCapabilityIds
                    .PerishableFoodFreshnessCodecVersion
            || resource.StockCategory != StockCategory.Food
            || !resource.TryGetFeature(out food)
            || !(food.freshnessSeconds > 0f)
            || float.IsNaN(food.freshnessSeconds)
            || float.IsInfinity(food.freshnessSeconds)
            || !resource.TryGetFeature(out ProductionItemFeature _)
            || resource.ValidateDefinition().Count != 0)
        {
            throw new ProductionPreparedOutputComponentCodecException(
                ProductionPreparedOutputComponentFailureCode
                    .InvalidDefinition,
                "Prepared perishable output requires an exact valid food definition and descriptor.");
        }
        return resource;
    }

    private static ProductionPreparedOutputComponentProjection CreateProjection(
        ResourceItemDefinitionSO resource,
        ItemInstanceComponentSaveData freshness,
        string payload,
        string fingerprint) => new(
        payload,
        ResourceItemSemanticDigest.Capture(resource),
        fingerprint,
        PhysicalItemMassSubject.ForDefinition(
            (ItemDefinitionId)resource.ItemId),
        new[] { freshness });

    private static string BuildPayload(
        string itemId,
        ItemInstanceComponentSaveData freshness)
    {
        string component = freshness.ToCanonicalString();
        return PayloadPrefix
            + Encoding.UTF8.GetByteCount(itemId).ToString(
                CultureInfo.InvariantCulture)
            + ":"
            + itemId
            + "|component="
            + Encoding.UTF8.GetByteCount(component).ToString(
                CultureInfo.InvariantCulture)
            + ":"
            + component;
    }

    private static string CaptureFingerprint(
        ResourceItemDefinitionSO resource,
        string payload)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(ProfileSchema);
        digest.Append(ResourceItemSemanticDigest.Capture(resource));
        digest.Append(payload);
        return digest.ComputeSha256();
    }

    private static bool Canonical(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsLowercaseSha256(string value) =>
        value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f');
}
