using System;
using System.Collections.Generic;
using System.Linq;

public enum ProductionPreparedOutputComponentFailureCode
{
    InvalidDefinition = 0,
    UnsupportedStatefulDefinition = 1,
    NonCanonicalPayload = 2,
    FingerprintMismatch = 3
}

public sealed class ProductionPreparedOutputComponentCodecException :
    InvalidOperationException
{
    public ProductionPreparedOutputComponentCodecException(
        ProductionPreparedOutputComponentFailureCode failureCode,
        string message)
        : base(message)
    {
        FailureCode = failureCode;
    }

    public ProductionPreparedOutputComponentFailureCode FailureCode { get; }
}

/// <summary>
/// Immutable bridge from one prepared production-output line to the physical-item
/// mass/admission boundary. The first slice intentionally supports only definition-only
/// generic items, so its runtime component collection is canonically empty.
/// </summary>
public sealed class ProductionPreparedOutputComponentProjection
{
    private static readonly IReadOnlyList<ItemInstanceComponentSaveData>
        EmptyRuntimeComponents = Array.AsReadOnly(
            Array.Empty<ItemInstanceComponentSaveData>());

    internal ProductionPreparedOutputComponentProjection(
        string canonicalPayload,
        string itemDefinitionDigest,
        string fingerprint,
        PhysicalItemMassSubject massSubject)
    {
        CanonicalPayload = canonicalPayload
            ?? throw new ArgumentNullException(nameof(canonicalPayload));
        ItemDefinitionDigest = itemDefinitionDigest
            ?? throw new ArgumentNullException(nameof(itemDefinitionDigest));
        Fingerprint = fingerprint
            ?? throw new ArgumentNullException(nameof(fingerprint));
        MassSubject = massSubject
            ?? throw new ArgumentNullException(nameof(massSubject));
        if (massSubject.Kind != PhysicalItemMassSubjectKind.GenericDefinition
            || massSubject.Components.Count != 0
            || massSubject.ComponentFingerprint.Length != 0)
        {
            throw new ArgumentException(
                "Definition-only prepared output requires a generic mass subject.",
                nameof(massSubject));
        }
    }

    public string CanonicalPayload { get; }
    public string ItemDefinitionDigest { get; }
    public string Fingerprint { get; }
    public PhysicalItemMassSubject MassSubject { get; }
    public IReadOnlyList<ItemInstanceComponentSaveData> RuntimeComponents =>
        EmptyRuntimeComponents;
}

public interface IProductionPreparedOutputComponentCodec
{
    ProductionPreparedOutputComponentProjection Create(
        ItemDefinitionSO definition);

    ProductionPreparedOutputComponentProjection ValidateAndDecode(
        ItemDefinitionSO definition,
        string canonicalPayload,
        string fingerprint);
}

/// <summary>
/// Canonical codec for the first standard-generic production family. Stateful definitions
/// are rejected rather than being published with missing components or default state.
/// </summary>
public sealed class ProductionPreparedOutputComponentCodec :
    IProductionPreparedOutputComponentCodec
{
    public const string ProfileSchemaToken =
        ProductionPreparedOutputComponentProfileDigest.SchemaToken;

    private static readonly HashSet<Type> DefinitionOnlyFeatureTypes = new()
    {
        typeof(ProductionItemFeature),
        typeof(MarketItemFeature),
        typeof(ResearchGateItemFeature),
        typeof(FacilitySupplyItemFeature)
    };

    // This codec is deliberately ratcheted to the first audited standard-generic
    // production family. A later family must be reviewed and added explicitly rather
    // than silently inheriting an empty component payload.
    private static readonly HashSet<string> SupportedDefinitionOnlyItemIds =
        new(StringComparer.Ordinal)
        {
            "feed:dog-food",
            "feed:hay",
            "feed:silage",
            "material:charcoal",
            "material:flour",
            "material:lumber",
            "material:malt",
            "material:starch",
            "material:steel-ingot",
            "material:treated-lumber",
            "waste:plant-rot"
        };

    public ProductionPreparedOutputComponentProjection Create(
        ItemDefinitionSO definition)
    {
        string itemId = RequireDefinitionOnly(definition);
        ResourceItemDefinitionSO resource =
            (ResourceItemDefinitionSO)definition;
        string payload = ProductionPreparedOutputComponentProfileDigest
            .BuildCanonicalPayload(resource);
        string itemDigest = ResourceItemSemanticDigest.Capture(resource);
        return CreateProjection(
            itemId,
            payload,
            itemDigest,
            ProductionPreparedOutputComponentProfileDigest.Capture(
                resource,
                payload));
    }

    public ProductionPreparedOutputComponentProjection ValidateAndDecode(
        ItemDefinitionSO definition,
        string canonicalPayload,
        string fingerprint)
    {
        string itemId = RequireDefinitionOnly(definition);
        ResourceItemDefinitionSO resource =
            (ResourceItemDefinitionSO)definition;
        string expectedPayload = ProductionPreparedOutputComponentProfileDigest
            .BuildCanonicalPayload(resource);
        if (canonicalPayload == null
            || !string.Equals(
                canonicalPayload,
                expectedPayload,
                StringComparison.Ordinal))
        {
            throw new ProductionPreparedOutputComponentCodecException(
                ProductionPreparedOutputComponentFailureCode.NonCanonicalPayload,
                $"Prepared output for '{itemId}' has a noncanonical component payload.");
        }

        string itemDigest = ResourceItemSemanticDigest.Capture(resource);
        string expectedFingerprint =
            ProductionPreparedOutputComponentProfileDigest.Capture(
                resource,
                expectedPayload);
        if (!IsLowercaseSha256(fingerprint)
            || !string.Equals(
                fingerprint,
                expectedFingerprint,
                StringComparison.Ordinal))
        {
            throw new ProductionPreparedOutputComponentCodecException(
                ProductionPreparedOutputComponentFailureCode.FingerprintMismatch,
                $"Prepared output for '{itemId}' has a mismatched component fingerprint.");
        }

        return CreateProjection(
            itemId,
            expectedPayload,
            itemDigest,
            expectedFingerprint);
    }

    private static ProductionPreparedOutputComponentProjection CreateProjection(
        string itemId,
        string payload,
        string itemDefinitionDigest,
        string fingerprint) => new(
        payload,
        itemDefinitionDigest,
        fingerprint,
        PhysicalItemMassSubject.ForDefinition((ItemDefinitionId)itemId));

    private static string RequireDefinitionOnly(ItemDefinitionSO definition)
    {
        if (definition == null
            || definition is not ResourceItemDefinitionSO
            || !IsCanonicalItemId(definition.ItemId))
        {
            throw new ProductionPreparedOutputComponentCodecException(
                ProductionPreparedOutputComponentFailureCode.InvalidDefinition,
                "Prepared generic output requires a canonical resource-item definition.");
        }

        string itemId = definition.ItemId;
        if (!SupportedDefinitionOnlyItemIds.Contains(itemId)
            || PhysicalItemIds.TryGetEquipmentDefinitionId(itemId, out _)
            || PhysicalItemIds.IsEquipmentModule(itemId)
            || definition.Features == null
            || definition.Features.Any(feature => feature == null
                || !DefinitionOnlyFeatureTypes.Contains(feature.GetType())))
        {
            throw new ProductionPreparedOutputComponentCodecException(
                ProductionPreparedOutputComponentFailureCode.UnsupportedStatefulDefinition,
                $"Prepared output '{itemId}' requires a state-specific output codec.");
        }

        if (!definition.TryGetFeature(out ProductionItemFeature _)
            || definition.ValidateDefinition().Count != 0)
        {
            throw new ProductionPreparedOutputComponentCodecException(
                ProductionPreparedOutputComponentFailureCode.InvalidDefinition,
                $"Prepared output '{itemId}' has an invalid authored definition.");
        }

        return itemId;
    }

    private static bool IsLowercaseSha256(string value)
    {
        if (value == null || value.Length != 64)
        {
            return false;
        }
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (!(character is >= '0' and <= '9')
                && !(character is >= 'a' and <= 'f'))
            {
                return false;
            }
        }
        return true;
    }

    private static bool IsCanonicalItemId(string value)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            return false;
        }
        foreach (char character in value)
        {
            if (!(character is >= 'a' and <= 'z')
                && !(character is >= '0' and <= '9')
                && character != ':'
                && character != '-'
                && character != '_'
                && character != '.')
            {
                return false;
            }
        }
        return true;
    }
}
