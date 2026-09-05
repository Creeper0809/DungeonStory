using System;
using System.Linq;

/// <summary>
/// Explicit capability metadata for domain-owned output coordinators. These
/// entries are never selected by a generic recipe from item identity alone;
/// the owning aggregate freezes the declared capability before publication.
/// </summary>
public sealed class ApparelWorkOrderOutputCapability :
    IProductionOutputCapability,
    IProductionOutputMaximumMassCapability
{
    private readonly IApparelDefinitionCatalog catalog;

    public ApparelWorkOrderOutputCapability(IApparelDefinitionCatalog catalog)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public string CapabilityId => ProductionOutputCapabilityIds.ApparelWorkOrder;
    public int ContractVersion =>
        ProductionOutputCapabilityIds.ApparelWorkOrderVersion;
    public string ComponentCodecId =>
        ProductionOutputCapabilityIds.ApparelStateCodec;
    public int ComponentCodecVersion =>
        ProductionOutputCapabilityIds.ApparelStateCodecVersion;
    public bool SupportsAutomaticSelection => false;

    public bool CanHandle(string itemId) =>
        catalog.TryGetByItemId(itemId, out ApparelDefinitionSO _);

    public ProductionOutputMaximumMassProjection CaptureDefinitionMaximum(
        ProductionOutputCapabilityDescriptor descriptor,
        int maximumQuantity,
        IPhysicalItemMassQuery massQuery) =>
        ProductionOutputDefinitionMaximumMassProjection.Capture(
            this, descriptor, maximumQuantity, massQuery);
}

public sealed class CombatEquipmentCraftOutputCapability :
    IProductionOutputCapability,
    IProductionOutputMaximumMassCapability
{
    public const string OutputLineId = "output:combat-equipment";

    private readonly ICombatEquipmentCatalog catalog;

    public CombatEquipmentCraftOutputCapability(ICombatEquipmentCatalog catalog)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public string CapabilityId =>
        ProductionOutputCapabilityIds.CombatEquipmentCraft;
    public int ContractVersion =>
        ProductionOutputCapabilityIds.CombatEquipmentCraftVersion;
    public string ComponentCodecId =>
        ProductionOutputCapabilityIds.CombatEquipmentStateCodec;
    public int ComponentCodecVersion =>
        ProductionOutputCapabilityIds.CombatEquipmentStateCodecVersion;
    public bool SupportsAutomaticSelection => false;

    public bool CanHandle(string itemId) => catalog.All.Any(definition =>
        definition != null
        && string.Equals(
            PhysicalItemIds.ForEquipment(definition.EquipmentId),
            itemId,
            StringComparison.Ordinal));

    public ProductionOutputMaximumMassProjection CaptureDefinitionMaximum(
        ProductionOutputCapabilityDescriptor descriptor,
        int maximumQuantity,
        IPhysicalItemMassQuery massQuery) =>
        ProductionOutputDefinitionMaximumMassProjection.Capture(
            this, descriptor, maximumQuantity, massQuery);
}

public sealed class CombatAmmunitionCraftOutputCapability :
    IProductionOutputCapability,
    IProductionOutputMaximumMassCapability,
    IProductionPreparedOutputParticipantCapability
{
    public const string OutputLineId = "output:combat-ammunition";

    private readonly IItemDefinitionCatalog catalog;

    public CombatAmmunitionCraftOutputCapability(
        IItemDefinitionCatalog catalog)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public string CapabilityId =>
        ProductionOutputCapabilityIds.CombatAmmunitionCraft;
    public int ContractVersion =>
        ProductionOutputCapabilityIds.CombatAmmunitionCraftVersion;
    public string ComponentCodecId =>
        ProductionOutputCapabilityIds.CombatAmmunitionStateCodec;
    public int ComponentCodecVersion =>
        ProductionOutputCapabilityIds.CombatAmmunitionStateCodecVersion;
    public bool SupportsAutomaticSelection => true;

    public bool CanHandle(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)
            || !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal)
            || !catalog.TryGet(
                (ItemDefinitionId)itemId,
                out ItemDefinitionSO definition))
        {
            return false;
        }
        return definition.TryGetFeature(out AmmunitionItemFeature _)
            && definition.ValidateDefinition().Count == 0;
    }

    public ProductionOutputMaximumMassProjection CaptureDefinitionMaximum(
        ProductionOutputCapabilityDescriptor descriptor,
        int maximumQuantity,
        IPhysicalItemMassQuery massQuery) =>
        ProductionOutputDefinitionMaximumMassProjection.Capture(
            this, descriptor, maximumQuantity, massQuery);
}

public sealed class PerishableFoodOutputCapability :
    IProductionOutputCapability,
    IProductionOutputMaximumMassCapability,
    IProductionPreparedOutputParticipantCapability
{
    private readonly IItemDefinitionCatalog catalog;

    public PerishableFoodOutputCapability(IItemDefinitionCatalog catalog)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public string CapabilityId =>
        ProductionOutputCapabilityIds.PerishableFood;
    public int ContractVersion =>
        ProductionOutputCapabilityIds.PerishableFoodVersion;
    public string ComponentCodecId =>
        ProductionOutputCapabilityIds.PerishableFoodFreshnessCodec;
    public int ComponentCodecVersion =>
        ProductionOutputCapabilityIds.PerishableFoodFreshnessCodecVersion;
    public bool SupportsAutomaticSelection => true;

    public bool CanHandle(string itemId)
    {
        if (string.IsNullOrEmpty(itemId)
            || !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal)
            || !catalog.TryGet(
                (ItemDefinitionId)itemId,
                out ItemDefinitionSO definition)
            || definition == null)
        {
            return false;
        }

        return definition.StockCategory == StockCategory.Food
            && definition.TryGetFeature(out FoodItemFeature food)
            && food.freshnessSeconds > 0f
            && !float.IsNaN(food.freshnessSeconds)
            && !float.IsInfinity(food.freshnessSeconds)
            && definition.ValidateDefinition().Count == 0;
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

public sealed class CertifiedSeedOutputCapability :
    IProductionOutputCapability,
    IProductionOutputMaximumMassCapability
{
    public const string OutputLineId = "output:certified-seed";

    private readonly IResourceEconomyContentCatalog catalog;

    public CertifiedSeedOutputCapability(IResourceEconomyContentCatalog catalog)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
    }

    public string CapabilityId => ProductionOutputCapabilityIds.CertifiedSeed;
    public int ContractVersion => ProductionOutputCapabilityIds.CertifiedSeedVersion;
    public string ComponentCodecId =>
        ProductionOutputCapabilityIds.SeedLotStateCodec;
    public int ComponentCodecVersion =>
        ProductionOutputCapabilityIds.SeedLotStateCodecVersion;
    public bool SupportsAutomaticSelection => false;

    public bool CanHandle(string itemId) => catalog.Crops.Any(crop =>
        crop != null
        && string.Equals(crop.SeedItemId, itemId, StringComparison.Ordinal));

    public ProductionOutputMaximumMassProjection CaptureDefinitionMaximum(
        ProductionOutputCapabilityDescriptor descriptor,
        int maximumQuantity,
        IPhysicalItemMassQuery massQuery) =>
        ProductionOutputDefinitionMaximumMassProjection.Capture(
            this, descriptor, maximumQuantity, massQuery);
}

/// <summary>
/// Returned cultivar seed lots from an ordinary crop harvest. This shares the
/// seed-lot component codec with certified seeds but keeps a distinct semantic
/// capability owner, so restore and maximum proofs cannot substitute one
/// production domain for the other.
/// </summary>
public sealed class CropHarvestSeedLotOutputCapability :
    IProductionOutputCapability,
    IProductionOutputMaximumMassCapability
{
    private readonly IResourceEconomyContentCatalog catalog;

    public CropHarvestSeedLotOutputCapability(
        IResourceEconomyContentCatalog catalog)
    {
        this.catalog = catalog
            ?? throw new ArgumentNullException(nameof(catalog));
    }

    public string CapabilityId =>
        ProductionOutputCapabilityIds.CropHarvestSeedLot;
    public int ContractVersion =>
        ProductionOutputCapabilityIds.CropHarvestSeedLotVersion;
    public string ComponentCodecId =>
        ProductionOutputCapabilityIds.SeedLotStateCodec;
    public int ComponentCodecVersion =>
        ProductionOutputCapabilityIds.SeedLotStateCodecVersion;
    public bool SupportsAutomaticSelection => false;

    public bool CanHandle(string itemId) => catalog.Crops.Any(crop =>
        crop != null
        && string.Equals(crop.SeedItemId, itemId, StringComparison.Ordinal));

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
