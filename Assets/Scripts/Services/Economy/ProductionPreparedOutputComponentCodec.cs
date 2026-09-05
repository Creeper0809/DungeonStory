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
/// mass/admission boundary. Runtime components describe exact business state;
/// MassSubject contains only state that changes physical mass. A generic mass
/// subject may therefore coexist with non-mass runtime components such as food
/// freshness.
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
        PhysicalItemMassSubject massSubject,
        IEnumerable<ItemInstanceComponentSaveData> runtimeComponents = null)
    {
        CanonicalPayload = canonicalPayload
            ?? throw new ArgumentNullException(nameof(canonicalPayload));
        ItemDefinitionDigest = itemDefinitionDigest
            ?? throw new ArgumentNullException(nameof(itemDefinitionDigest));
        Fingerprint = fingerprint
            ?? throw new ArgumentNullException(nameof(fingerprint));
        MassSubject = massSubject
            ?? throw new ArgumentNullException(nameof(massSubject));
        ItemInstanceComponentSaveData[] source = (runtimeComponents
                ?? Enumerable.Empty<ItemInstanceComponentSaveData>())
            .ToArray();
        if (source.Any(value => value == null))
        {
            throw new ArgumentException(
                "Prepared output runtime components cannot contain null.",
                nameof(runtimeComponents));
        }
        ItemInstanceComponentSaveData[] copied = source
            .Select(value => value.Clone())
            .ToArray();
        if (massSubject.Kind == PhysicalItemMassSubjectKind.GenericDefinition
            && (massSubject.Components.Count != 0
                || massSubject.ComponentFingerprint.Length != 0))
        {
            throw new ArgumentException(
                "Generic prepared-output mass subject cannot carry mass components.",
                nameof(massSubject));
        }
        RuntimeComponents = copied.Length == 0
            ? EmptyRuntimeComponents
            : Array.AsReadOnly(copied);
    }

    public string CanonicalPayload { get; }
    public string ItemDefinitionDigest { get; }
    public string Fingerprint { get; }
    public PhysicalItemMassSubject MassSubject { get; }
    public IReadOnlyList<ItemInstanceComponentSaveData> RuntimeComponents { get; }
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

public interface IProductionPreparedOutputMaterializer
{
    string CapabilityId { get; }
    int CapabilityVersion { get; }
    string ComponentCodecId { get; }
    int ComponentCodecVersion { get; }

    ProductionPreparedOutputComponentProjection Create(
        ProductionOutputCapabilityDescriptor descriptor,
        ItemDefinitionSO definition);

    ProductionPreparedOutputComponentProjection ValidateAndDecode(
        ProductionOutputCapabilityDescriptor descriptor,
        ItemDefinitionSO definition,
        string canonicalPayload,
        string fingerprint);
}

public interface IProductionPreparedOutputMaterializerRegistry
{
    IReadOnlyList<ProductionOutputCapabilityContractSnapshot> Contracts { get; }
    string RegistryFingerprint { get; }

    void ValidateDescriptor(ProductionOutputCapabilityDescriptor descriptor);

    ProductionPreparedOutputComponentProjection Create(
        ProductionOutputCapabilityDescriptor descriptor,
        ItemDefinitionSO definition);

    ProductionPreparedOutputComponentProjection ValidateAndDecode(
        ProductionOutputCapabilityDescriptor descriptor,
        ItemDefinitionSO definition,
        string canonicalPayload,
        string fingerprint);
}

/// <summary>
/// Deterministic dispatch boundary for capabilities that explicitly opt into
/// the common prepared-output transaction. It validates a one-to-one contract
/// join with the capability registry, so adding another supported output family
/// requires only a capability, materializer and registration—not a core branch.
/// </summary>
public sealed class ProductionPreparedOutputMaterializerRegistry :
    IProductionPreparedOutputMaterializerRegistry
{
    public const string Schema =
        "production-prepared-output-materializer-registry@1";

    private readonly IProductionOutputCapabilityRegistry capabilities;
    private readonly Dictionary<string, IProductionPreparedOutputMaterializer>
        byCapabilityId;

    public ProductionPreparedOutputMaterializerRegistry(
        IEnumerable<IProductionPreparedOutputMaterializer> materializers,
        IProductionOutputCapabilityRegistry capabilities)
    {
        this.capabilities = capabilities
            ?? throw new ArgumentNullException(nameof(capabilities));
        IProductionPreparedOutputMaterializer[] ordered = (materializers
                ?? throw new ArgumentNullException(nameof(materializers)))
            .OrderBy(value => value?.CapabilityId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Any(value => value == null))
        {
            throw new InvalidOperationException(
                "Prepared-output materializer registry contains null.");
        }
        byCapabilityId = new Dictionary<
            string,
            IProductionPreparedOutputMaterializer>(StringComparer.Ordinal);
        IReadOnlyList<ProductionOutputCapabilityContractSnapshot> contracts =
            capabilities.CapabilityContracts
            ?? throw new InvalidOperationException(
                "Output capability registry has no contract snapshot.");
        Dictionary<string, ProductionOutputCapabilityContractSnapshot>
            contractById = new(StringComparer.Ordinal);
        for (int index = 0; index < contracts.Count; index++)
        {
            ProductionOutputCapabilityContractSnapshot contract =
                contracts[index];
            if (!contractById.TryAdd(contract.CapabilityId, contract))
            {
                throw new InvalidOperationException(
                    "Output capability registry contains duplicate contracts.");
            }
        }
        foreach (IProductionPreparedOutputMaterializer materializer in ordered)
        {
            if (!byCapabilityId.TryAdd(
                    materializer.CapabilityId,
                    materializer)
                || !contractById.TryGetValue(
                    materializer.CapabilityId,
                    out ProductionOutputCapabilityContractSnapshot contract)
                || !contract.ParticipatesInPreparedOutput
                || contract.ContractVersion != materializer.CapabilityVersion
                || !string.Equals(
                    contract.ComponentCodecId,
                    materializer.ComponentCodecId,
                    StringComparison.Ordinal)
                || contract.ComponentCodecVersion !=
                    materializer.ComponentCodecVersion)
            {
                throw new InvalidOperationException(
                    "Prepared-output materializer contract is missing, duplicated, or drifted: "
                    + (materializer.CapabilityId ?? string.Empty));
            }
        }
        ProductionOutputCapabilityContractSnapshot[] participating = contracts
            .Where(value => value.ParticipatesInPreparedOutput)
            .OrderBy(value => value.CapabilityId, StringComparer.Ordinal)
            .ToArray();
        if (participating.Any(value =>
                !byCapabilityId.ContainsKey(value.CapabilityId)))
        {
            throw new InvalidOperationException(
                "A prepared-output participant has no registered materializer.");
        }
        Contracts = Array.AsReadOnly(participating);
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(Schema);
        digest.Append(participating.Length);
        foreach (ProductionOutputCapabilityContractSnapshot contract in
                 participating)
        {
            digest.Append(contract.CapabilityId);
            digest.Append(contract.ContractVersion);
            digest.Append(contract.ComponentCodecId);
            digest.Append(contract.ComponentCodecVersion);
            digest.Append(contract.SupportsAutomaticSelection);
            digest.Append(contract.ParticipatesInPreparedOutput);
            digest.Append(byCapabilityId[contract.CapabilityId]
                .GetType().FullName ?? string.Empty);
        }
        RegistryFingerprint = digest.ComputeSha256();
    }

    public IReadOnlyList<ProductionOutputCapabilityContractSnapshot> Contracts
    { get; }

    public string RegistryFingerprint { get; }

    public void ValidateDescriptor(
        ProductionOutputCapabilityDescriptor descriptor) =>
        Resolve(descriptor);

    public ProductionPreparedOutputComponentProjection Create(
        ProductionOutputCapabilityDescriptor descriptor,
        ItemDefinitionSO definition)
    {
        IProductionPreparedOutputMaterializer materializer = Resolve(descriptor);
        return ValidateProjection(
            descriptor,
            materializer.Create(descriptor, definition));
    }

    public ProductionPreparedOutputComponentProjection ValidateAndDecode(
        ProductionOutputCapabilityDescriptor descriptor,
        ItemDefinitionSO definition,
        string canonicalPayload,
        string fingerprint)
    {
        IProductionPreparedOutputMaterializer materializer = Resolve(descriptor);
        return ValidateProjection(
            descriptor,
            materializer.ValidateAndDecode(
                descriptor,
                definition,
                canonicalPayload,
                fingerprint));
    }

    private IProductionPreparedOutputMaterializer Resolve(
        ProductionOutputCapabilityDescriptor descriptor)
    {
        if (!capabilities.TryValidateExact(
                descriptor,
                out IProductionOutputCapability capability,
                out DomainFailure failure)
            || capability is not IProductionPreparedOutputParticipantCapability
            || !byCapabilityId.TryGetValue(
                descriptor.CapabilityId,
                out IProductionPreparedOutputMaterializer materializer)
            || materializer.CapabilityVersion != descriptor.CapabilityVersion
            || !string.Equals(
                materializer.ComponentCodecId,
                descriptor.ComponentCodecId,
                StringComparison.Ordinal)
            || materializer.ComponentCodecVersion !=
                descriptor.ComponentCodecVersion)
        {
            throw new InvalidOperationException(
                "Prepared-output materializer is missing or drifted: "
                + descriptor.CapabilityId
                + "/"
                + failure.Code);
        }
        return materializer;
    }

    private static ProductionPreparedOutputComponentProjection
        ValidateProjection(
            ProductionOutputCapabilityDescriptor descriptor,
            ProductionPreparedOutputComponentProjection projection)
    {
        if (projection == null
            || projection.MassSubject == null
            || !string.Equals(
                projection.MassSubject.ItemId.Value,
                descriptor.ItemId,
                StringComparison.Ordinal)
            || string.IsNullOrEmpty(projection.CanonicalPayload)
            || !IsLowercaseSha256(projection.Fingerprint))
        {
            throw new InvalidOperationException(
                "Prepared-output materializer returned an invalid projection: "
                + descriptor.CapabilityId);
        }
        return projection;
    }

    private static bool IsLowercaseSha256(string value) =>
        value != null
        && value.Length == 64
        && value.All(character => character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f');
}

/// <summary>
/// Canonical codec for standard definition-only production output. Any new
/// definition composed solely from the audited definition-only feature set is
/// admitted without an item-ID allowlist. Stateful definitions fail loudly.
/// </summary>
public sealed class ProductionPreparedOutputComponentCodec :
    IProductionPreparedOutputComponentCodec,
    IProductionPreparedOutputMaterializer
{
    public const string ProfileSchemaToken =
        ProductionPreparedOutputComponentProfileDigest.SchemaToken;

    public string CapabilityId =>
        ProductionOutputCapabilityIds.StandardDefinition;
    public int CapabilityVersion =>
        ProductionOutputCapabilityIds.StandardDefinitionVersion;
    public string ComponentCodecId =>
        ProductionOutputCapabilityIds.DefinitionOnlyCodec;
    public int ComponentCodecVersion =>
        ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion;

    ProductionPreparedOutputComponentProjection
        IProductionPreparedOutputMaterializer.Create(
            ProductionOutputCapabilityDescriptor descriptor,
            ItemDefinitionSO definition)
    {
        ValidateDescriptor(descriptor, definition);
        return Create(definition);
    }

    ProductionPreparedOutputComponentProjection
        IProductionPreparedOutputMaterializer.ValidateAndDecode(
            ProductionOutputCapabilityDescriptor descriptor,
            ItemDefinitionSO definition,
            string canonicalPayload,
            string fingerprint)
    {
        ValidateDescriptor(descriptor, definition);
        return ValidateAndDecode(definition, canonicalPayload, fingerprint);
    }

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
        if (PhysicalItemIds.TryGetEquipmentDefinitionId(itemId, out _)
            || PhysicalItemIds.IsEquipmentModule(itemId)
            || definition.Features == null
            || definition.Features.Any(feature => feature == null
                || feature.RequiresProductionOutputInstanceState))
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

    private static void ValidateDescriptor(
        ProductionOutputCapabilityDescriptor descriptor,
        ItemDefinitionSO definition)
    {
        if (definition == null
            || !string.Equals(
                descriptor.ItemId,
                definition.ItemId,
                StringComparison.Ordinal)
            || !string.Equals(
                descriptor.CapabilityId,
                ProductionOutputCapabilityIds.StandardDefinition,
                StringComparison.Ordinal)
            || descriptor.CapabilityVersion !=
                ProductionOutputCapabilityIds.StandardDefinitionVersion
            || !string.Equals(
                descriptor.ComponentCodecId,
                ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                StringComparison.Ordinal)
            || descriptor.ComponentCodecVersion !=
                ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion)
        {
            throw new ProductionPreparedOutputComponentCodecException(
                ProductionPreparedOutputComponentFailureCode.InvalidDefinition,
                "Prepared-output descriptor does not match the definition-only materializer.");
        }
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
