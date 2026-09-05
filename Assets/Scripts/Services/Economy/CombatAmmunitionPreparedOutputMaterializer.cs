using System;
using System.Globalization;
using System.Linq;
using System.Text;

/// <summary>
/// Prepared-output projection for generic ammunition bills. Ammunition kind is
/// definition metadata, while the current physical lot has no mutable runtime
/// component. The dedicated codec keeps that distinction explicit and prevents
/// ammunition from falling through the definition-only codec.
/// </summary>
public sealed class CombatAmmunitionPreparedOutputMaterializer :
    IProductionPreparedOutputMaterializer
{
    private const string ProfileSchema =
        "production-prepared-output-combat-ammunition-profile@1";
    private const string PayloadPrefix =
        "production-prepared-output-components@1|kind=combat-ammunition|item=";

    public string CapabilityId =>
        ProductionOutputCapabilityIds.CombatAmmunitionCraft;
    public int CapabilityVersion =>
        ProductionOutputCapabilityIds.CombatAmmunitionCraftVersion;
    public string ComponentCodecId =>
        ProductionOutputCapabilityIds.CombatAmmunitionStateCodec;
    public int ComponentCodecVersion =>
        ProductionOutputCapabilityIds.CombatAmmunitionStateCodecVersion;

    public ProductionPreparedOutputComponentProjection Create(
        ProductionOutputCapabilityDescriptor descriptor,
        ItemDefinitionSO definition)
    {
        ResourceItemDefinitionSO resource = RequireDefinition(
            descriptor,
            definition,
            out AmmunitionItemFeature ammunition);
        string payload = BuildPayload(resource.ItemId, ammunition.ammunitionKindId);
        return CreateProjection(resource, payload, CaptureFingerprint(resource, payload));
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
            out AmmunitionItemFeature ammunition);
        string expectedPayload = BuildPayload(
            resource.ItemId,
            ammunition.ammunitionKindId);
        if (!string.Equals(
                canonicalPayload,
                expectedPayload,
                StringComparison.Ordinal))
        {
            throw new ProductionPreparedOutputComponentCodecException(
                ProductionPreparedOutputComponentFailureCode.NonCanonicalPayload,
                $"Prepared ammunition output for '{resource.ItemId}' has a noncanonical payload.");
        }

        string expectedFingerprint = CaptureFingerprint(resource, expectedPayload);
        if (!IsLowercaseSha256(fingerprint)
            || !string.Equals(
                fingerprint,
                expectedFingerprint,
                StringComparison.Ordinal))
        {
            throw new ProductionPreparedOutputComponentCodecException(
                ProductionPreparedOutputComponentFailureCode.FingerprintMismatch,
                $"Prepared ammunition output for '{resource.ItemId}' has a mismatched fingerprint.");
        }

        return CreateProjection(resource, expectedPayload, expectedFingerprint);
    }

    private static ResourceItemDefinitionSO RequireDefinition(
        ProductionOutputCapabilityDescriptor descriptor,
        ItemDefinitionSO definition,
        out AmmunitionItemFeature ammunition)
    {
        ammunition = null;
        if (definition is not ResourceItemDefinitionSO resource
            || !IsCanonicalToken(resource.ItemId)
            || !string.Equals(
                descriptor.ItemId,
                resource.ItemId,
                StringComparison.Ordinal)
            || !string.Equals(
                descriptor.CapabilityId,
                ProductionOutputCapabilityIds.CombatAmmunitionCraft,
                StringComparison.Ordinal)
            || descriptor.CapabilityVersion !=
                ProductionOutputCapabilityIds.CombatAmmunitionCraftVersion
            || !string.Equals(
                descriptor.ComponentCodecId,
                ProductionOutputCapabilityIds.CombatAmmunitionStateCodec,
                StringComparison.Ordinal)
            || descriptor.ComponentCodecVersion !=
                ProductionOutputCapabilityIds.CombatAmmunitionStateCodecVersion
            || !resource.TryGetFeature(out ammunition)
            || !IsCanonicalToken(ammunition.ammunitionKindId)
            || !resource.TryGetFeature(out ProductionItemFeature _)
            || resource.ValidateDefinition().Count != 0)
        {
            throw new ProductionPreparedOutputComponentCodecException(
                ProductionPreparedOutputComponentFailureCode.InvalidDefinition,
                "Prepared ammunition output requires an exact valid ammunition definition and descriptor.");
        }
        return resource;
    }

    private static ProductionPreparedOutputComponentProjection CreateProjection(
        ResourceItemDefinitionSO resource,
        string payload,
        string fingerprint) => new(
        payload,
        ResourceItemSemanticDigest.Capture(resource),
        fingerprint,
        PhysicalItemMassSubject.ForDefinition(
            (ItemDefinitionId)resource.ItemId));

    private static string BuildPayload(string itemId, string ammunitionKindId) =>
        PayloadPrefix
        + Encoding.UTF8.GetByteCount(itemId).ToString(CultureInfo.InvariantCulture)
        + ":"
        + itemId
        + "|ammunition-kind="
        + Encoding.UTF8.GetByteCount(ammunitionKindId).ToString(
            CultureInfo.InvariantCulture)
        + ":"
        + ammunitionKindId
        + "|components=0";

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

    private static bool IsCanonicalToken(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsLowercaseSha256(string value) =>
        value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f');
}
