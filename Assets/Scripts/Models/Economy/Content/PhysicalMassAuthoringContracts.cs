using System;

public enum ItemUnitSemanticKind
{
    LiquidPortion = 0,
    SeedPacket = 1,
    ProduceBundle = 2,
    AnimalProductPortion = 3,
    MealPortion = 4,
    MedicineDoseOrKit = 5,
    AmmunitionUnitOrPack = 6,
    TextileRollOrSheet = 7,
    LogSection = 8,
    ProcessedLumberBundle = 9,
    OreChunkOrBasket = 10,
    StoneOrBrickBlock = 11,
    MetalIngot = 12,
    SmallComponent = 13,
    LargeComponent = 14,
    WasteBundle = 15,
    ApparelPiece = 16,
    Weapon = 17,
    Shield = 18,
    ArmorPiece = 19,
    OversizeEquipment = 20,
    FacilityInstallationKit = 21,
    BlueprintOrRecord = 22,
    CatalystOrRelic = 23,
    OtherExplicitPhysicalUnit = 24
}

public enum PackageTareDisposition
{
    None = 0,
    ReusableContainerReturn = 1,
    DisposableWasteByproduct = 2,
    DestroyedDuringUse = 3,
    TransferredWithOutput = 4,
    BulkInfrastructureNotInUnit = 5
}

public enum PhysicalMassDerivationKind
{
    ExplicitPrimitive = 0,
    VolumeDensity = 1,
    RecipeMassBalance = 2,
    EquipmentShapeAndMaterial = 3,
    ApparelShapeAndTextile = 4,
    PackedFacilitySubassembly = 5,
    WorldSource = 6,
    DerivedByproduct = 7
}

public enum PhysicalHaulMassClass
{
    MicroUrgent = 0,
    Ordinary = 1,
    Heavy = 2,
    OversizeEquipment = 3,
    DedicatedTransport = 4,
    IndividualEquipment = 5
}

public enum PhysicalMassLossKind
{
    None = 0,
    MoistureEvaporation = 1,
    CuttingWaste = 2,
    SmeltingByproduct = 3,
    Spoilage = 4,
    Combustion = 5,
    DisposablePackaging = 6,
    ExplicitSink = 7,
    MillingByproduct = 8,
    FermentationGasLoss = 9,
    ExtractionResidue = 10
}

public readonly struct CanonicalItemUnitSemantic
{
    public CanonicalItemUnitSemantic(
        string itemId,
        ItemUnitSemanticKind unitSemanticKind,
        string unitLabel,
        string unitDescription,
        int nominalVolumeMilliLiters,
        int packageTareGrams,
        PackageTareDisposition packageTareDisposition,
        string packageContainerItemId,
        string primaryMaterialId,
        PhysicalMassDerivationKind massDerivationKind,
        PhysicalMassGrams canonicalUnitMass,
        PhysicalHaulMassClass haulClass,
        string massBalanceSourceId)
    {
        ItemId = RequireCanonicalToken(itemId, nameof(itemId));
        UnitLabel = RequireText(unitLabel, nameof(unitLabel));
        UnitDescription = RequireText(unitDescription, nameof(unitDescription));
        if (nominalVolumeMilliLiters < 0)
            throw new ArgumentOutOfRangeException(nameof(nominalVolumeMilliLiters));
        if (packageTareGrams < 0)
            throw new ArgumentOutOfRangeException(nameof(packageTareGrams));

        string containerId = packageContainerItemId ?? string.Empty;
        string materialId = primaryMaterialId ?? string.Empty;
        string sourceId = RequireCanonicalToken(massBalanceSourceId, nameof(massBalanceSourceId));
        ValidateTare(packageTareGrams, packageTareDisposition, containerId);

        UnitSemanticKind = unitSemanticKind;
        NominalVolumeMilliLiters = nominalVolumeMilliLiters;
        PackageTareGrams = packageTareGrams;
        PackageTareDisposition = packageTareDisposition;
        PackageContainerItemId = containerId;
        PrimaryMaterialId = materialId;
        MassDerivationKind = massDerivationKind;
        CanonicalUnitMass = canonicalUnitMass;
        HaulClass = haulClass;
        MassBalanceSourceId = sourceId;
    }

    public string ItemId { get; }
    public ItemUnitSemanticKind UnitSemanticKind { get; }
    public string UnitLabel { get; }
    public string UnitDescription { get; }
    public int NominalVolumeMilliLiters { get; }
    public int PackageTareGrams { get; }
    public PackageTareDisposition PackageTareDisposition { get; }
    public string PackageContainerItemId { get; }
    public string PrimaryMaterialId { get; }
    public PhysicalMassDerivationKind MassDerivationKind { get; }
    public PhysicalMassGrams CanonicalUnitMass { get; }
    public PhysicalHaulMassClass HaulClass { get; }
    public string MassBalanceSourceId { get; }

    private static void ValidateTare(
        int tareGrams,
        PackageTareDisposition disposition,
        string containerItemId)
    {
        if (disposition == PackageTareDisposition.None && tareGrams != 0)
            throw new ArgumentException("Tare mass requires an explicit disposition.");
        if (disposition == PackageTareDisposition.BulkInfrastructureNotInUnit && tareGrams != 0)
            throw new ArgumentException("Bulk infrastructure tare cannot be included in item unit mass.");
        if (tareGrams == 0
            && disposition != PackageTareDisposition.None
            && disposition != PackageTareDisposition.BulkInfrastructureNotInUnit)
        {
            throw new ArgumentException("A physical tare disposition requires positive tare mass.");
        }
        if (tareGrams > 0
            && (disposition == PackageTareDisposition.ReusableContainerReturn
                || disposition == PackageTareDisposition.DisposableWasteByproduct
                || disposition == PackageTareDisposition.TransferredWithOutput)
            && string.IsNullOrWhiteSpace(containerItemId))
        {
            throw new ArgumentException("Returned, wasted, or transferred tare requires a physical container item ID.");
        }
    }

    private static string RequireCanonicalToken(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("A non-empty pre-canonicalized token is required.", parameter);
        return value;
    }

    private static string RequireText(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value) || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
            throw new ArgumentException("A non-empty pre-normalized value is required.", parameter);
        return value;
    }
}

public readonly struct MaterialMassProfile
{
    public MaterialMassProfile(
        string materialId,
        int densityGramsPerLiter,
        int defaultMoisturePermille,
        int packingEfficiencyPermille,
        int defaultProcessYieldPermille)
    {
        if (string.IsNullOrWhiteSpace(materialId)
            || !string.Equals(materialId, materialId.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("A non-empty pre-canonicalized material ID is required.", nameof(materialId));
        }
        if (densityGramsPerLiter <= 0)
            throw new ArgumentOutOfRangeException(nameof(densityGramsPerLiter));
        RequirePermille(defaultMoisturePermille, nameof(defaultMoisturePermille), allowZero: true);
        RequirePermille(packingEfficiencyPermille, nameof(packingEfficiencyPermille), allowZero: false);
        RequirePermille(defaultProcessYieldPermille, nameof(defaultProcessYieldPermille), allowZero: false);

        MaterialId = materialId;
        DensityGramsPerLiter = densityGramsPerLiter;
        DefaultMoisturePermille = defaultMoisturePermille;
        PackingEfficiencyPermille = packingEfficiencyPermille;
        DefaultProcessYieldPermille = defaultProcessYieldPermille;
    }

    public string MaterialId { get; }
    public int DensityGramsPerLiter { get; }
    public int DefaultMoisturePermille { get; }
    public int PackingEfficiencyPermille { get; }
    public int DefaultProcessYieldPermille { get; }

    private static void RequirePermille(int value, string parameter, bool allowZero)
    {
        int minimum = allowZero ? 0 : 1;
        if (value < minimum || value > 1000)
            throw new ArgumentOutOfRangeException(parameter, value, $"Permille must be in [{minimum},1000].");
    }
}

public readonly struct PhysicalMassTransformContract
{
    public PhysicalMassTransformContract(
        string transformId,
        long physicalInputGrams,
        long infrastructureInputGrams,
        long physicalOutputGrams,
        long byproductGrams,
        long declaredLossGrams,
        PhysicalMassLossKind lossKind,
        string evidence)
    {
        TransformId = RequireCanonicalToken(transformId, nameof(transformId));
        Evidence = RequireText(evidence, nameof(evidence));
        RequireNonNegative(physicalInputGrams, nameof(physicalInputGrams));
        RequireNonNegative(infrastructureInputGrams, nameof(infrastructureInputGrams));
        RequireNonNegative(physicalOutputGrams, nameof(physicalOutputGrams));
        RequireNonNegative(byproductGrams, nameof(byproductGrams));
        RequireNonNegative(declaredLossGrams, nameof(declaredLossGrams));

        long totalInput = checked(physicalInputGrams + infrastructureInputGrams);
        long totalDisposition = checked(physicalOutputGrams + byproductGrams);
        totalDisposition = checked(totalDisposition + declaredLossGrams);
        if (totalInput <= 0)
            throw new ArgumentException("A transform requires positive physical mass input.");
        if (totalInput != totalDisposition)
        {
            throw new ArgumentException(
                $"Physical mass transform must conserve grams exactly: "
                + $"input={totalInput}, disposition={totalDisposition}.");
        }
        if (declaredLossGrams == 0 && lossKind != PhysicalMassLossKind.None)
            throw new ArgumentException("A typed physical mass loss requires positive grams.");
        if (declaredLossGrams > 0 && lossKind == PhysicalMassLossKind.None)
            throw new ArgumentException("Positive physical mass loss requires a typed cause.");

        PhysicalInputGrams = physicalInputGrams;
        InfrastructureInputGrams = infrastructureInputGrams;
        PhysicalOutputGrams = physicalOutputGrams;
        ByproductGrams = byproductGrams;
        DeclaredLossGrams = declaredLossGrams;
        LossKind = lossKind;
    }

    public string TransformId { get; }
    public long PhysicalInputGrams { get; }
    public long InfrastructureInputGrams { get; }
    public long PhysicalOutputGrams { get; }
    public long ByproductGrams { get; }
    public long DeclaredLossGrams { get; }
    public PhysicalMassLossKind LossKind { get; }
    public string Evidence { get; }
    public long TotalInputGrams => checked(PhysicalInputGrams + InfrastructureInputGrams);
    public long TotalDispositionGrams => checked(
        checked(PhysicalOutputGrams + ByproductGrams) + DeclaredLossGrams);

    private static string RequireCanonicalToken(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A non-empty pre-canonicalized token is required.",
                parameter);
        }
        return value;
    }

    private static string RequireText(string value, string parameter)
    {
        if (string.IsNullOrWhiteSpace(value)
            || !string.Equals(value, value.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException("A non-empty pre-normalized value is required.", parameter);
        }
        return value;
    }

    private static void RequireNonNegative(long value, string parameter)
    {
        if (value < 0)
            throw new ArgumentOutOfRangeException(parameter, value, "Mass cannot be negative.");
    }
}
