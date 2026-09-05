using System;

/// <summary>
/// Descriptor-only capability for definition-only outputs. Physical execution
/// belongs exclusively to the prepared-output batch coordinator, which owns
/// whole-vector gram admission, publication, routing and acknowledgement.
/// Implementing IProductionOutputHandler here would recreate the legacy
/// per-line FacilityOutputBuffer bypass.
/// </summary>
public sealed class StandardDefinitionProductionOutputCapability :
    IProductionOutputCapability,
    IProductionOutputMaximumMassCapability,
    IProductionPreparedOutputParticipantCapability
{
    private readonly IResourceEconomyContentCatalog catalog;
    private readonly IProductionPreparedOutputComponentCodec componentCodec;

    public StandardDefinitionProductionOutputCapability(
        IResourceEconomyContentCatalog catalog,
        IProductionPreparedOutputComponentCodec componentCodec)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.componentCodec = componentCodec
            ?? throw new ArgumentNullException(nameof(componentCodec));
    }

    public string CapabilityId =>
        ProductionOutputCapabilityIds.StandardDefinition;
    public int ContractVersion =>
        ProductionOutputCapabilityIds.StandardDefinitionVersion;
    public string ComponentCodecId =>
        ProductionOutputCapabilityIds.DefinitionOnlyCodec;
    public int ComponentCodecVersion =>
        ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion;
    public bool SupportsAutomaticSelection => true;

    public bool CanHandle(string itemId)
    {
        if (!catalog.TryGetItem(itemId, out ResourceItemDefinitionSO definition))
            return false;
        try
        {
            componentCodec.Create(definition);
            return true;
        }
        catch (ProductionPreparedOutputComponentCodecException)
        {
            return false;
        }
    }

    public ProductionOutputMaximumMassProjection CaptureDefinitionMaximum(
        ProductionOutputCapabilityDescriptor descriptor,
        int maximumQuantity,
        IPhysicalItemMassQuery massQuery) =>
        ProductionOutputDefinitionMaximumMassProjection.Capture(
            this,
            descriptor,
            maximumQuantity,
            massQuery);
}
