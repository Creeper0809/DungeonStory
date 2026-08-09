using Sirenix.OdinInspector;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

public static class FacilityAnchorPurposeIds
{
    public const string Use = "facility.use";
    public const string Work = "facility.work";
    public const string Checkout = "facility.checkout";
    public const string Exit = "facility.exit";
}

public delegate bool FacilityAnchorFallbackResolver(
    BuildableObject building,
    Vector3 fromWorld,
    out Vector3 worldPosition);

public sealed class FacilityAnchorPurposeDefinition
{
    public FacilityAnchorPurposeDefinition(string purposeId, FacilityAnchorFallbackResolver fallbackResolver)
    {
        PurposeId = string.IsNullOrWhiteSpace(purposeId)
            ? throw new ArgumentException("Anchor purpose ID is required.", nameof(purposeId))
            : purposeId;
        FallbackResolver = fallbackResolver
            ?? throw new ArgumentNullException(nameof(fallbackResolver));
    }

    public string PurposeId { get; }
    public FacilityAnchorFallbackResolver FallbackResolver { get; }
}

public static class FacilityAnchorPurposeCatalog
{
    private static readonly Dictionary<string, FacilityAnchorPurposeDefinition> Definitions =
        new Dictionary<string, FacilityAnchorPurposeDefinition>(StringComparer.Ordinal);

    static FacilityAnchorPurposeCatalog()
    {
        ResetBuiltIns();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetForSubsystemRegistration()
    {
        ResetBuiltIns();
    }

    public static bool Register(FacilityAnchorPurposeDefinition definition, bool replace = false)
    {
        if (definition == null)
        {
            throw new ArgumentNullException(nameof(definition));
        }

        if (!replace && Definitions.ContainsKey(definition.PurposeId))
        {
            return false;
        }

        Definitions[definition.PurposeId] = definition;
        return true;
    }

    public static bool Unregister(string purposeId)
    {
        return !string.IsNullOrWhiteSpace(purposeId) && Definitions.Remove(purposeId);
    }

    public static bool TryGet(string purposeId, out FacilityAnchorPurposeDefinition definition)
    {
        definition = null;
        return !string.IsNullOrWhiteSpace(purposeId)
            && Definitions.TryGetValue(purposeId, out definition);
    }

    private static void ResetBuiltIns()
    {
        Definitions.Clear();
        Register(new FacilityAnchorPurposeDefinition(FacilityAnchorPurposeIds.Use, ResolveOccupiedAnchor));
        Register(new FacilityAnchorPurposeDefinition(FacilityAnchorPurposeIds.Work, ResolveWorkAnchor));
        Register(new FacilityAnchorPurposeDefinition(FacilityAnchorPurposeIds.Checkout, ResolveCheckoutAnchor));
        Register(new FacilityAnchorPurposeDefinition(FacilityAnchorPurposeIds.Exit, ResolveOccupiedAnchor));
    }

    private static bool ResolveOccupiedAnchor(BuildableObject building, Vector3 fromWorld, out Vector3 worldPosition)
    {
        return building.TryGetFacilityOccupiedWorldPosition(fromWorld, out worldPosition)
            || building.TryGetHorizontalFootprintAnchorWorldPosition(0.5f, out worldPosition);
    }

    private static bool ResolveWorkAnchor(BuildableObject building, Vector3 fromWorld, out Vector3 worldPosition)
    {
        return building.TryGetHorizontalFootprintAnchorWorldPosition(0.85f, out worldPosition);
    }

    private static bool ResolveCheckoutAnchor(BuildableObject building, Vector3 fromWorld, out Vector3 worldPosition)
    {
        return building.TryGetHorizontalFootprintAnchorWorldPosition(0.75f, out worldPosition);
    }
}

[Serializable]
public sealed class FacilityAnchorSlot
{
    [Tooltip("이 슬롯을 사용하는 시스템의 안정적인 목적 ID")]
    public string purposeId = FacilityAnchorPurposeIds.Use;
    [Tooltip("시설 중심 칸에서 더할 그리드 좌표 오프셋")]
    public Vector2 offset;

    public bool IsValid => !string.IsNullOrWhiteSpace(purposeId);
}

[Serializable]
public sealed class FacilityAnchorData
{
    [SerializeField] private List<FacilityAnchorSlot> slots = new List<FacilityAnchorSlot>();
    [NonSerialized] private IReadOnlyList<FacilityAnchorSlot> slotsView;

    public IReadOnlyList<FacilityAnchorSlot> Slots
    {
        get
        {
            slots ??= new List<FacilityAnchorSlot>();
            return slotsView ??= ReadOnlyView.List(slots);
        }
    }

    public void Add(string purposeId, Vector2 offset)
    {
        if (string.IsNullOrWhiteSpace(purposeId))
        {
            return;
        }

        slots ??= new List<FacilityAnchorSlot>();
        slots.Add(new FacilityAnchorSlot { purposeId = purposeId, offset = offset });
    }

    public IEnumerable<FacilityAnchorSlot> Enumerate(string purposeId)
    {
        if (slots == null || string.IsNullOrWhiteSpace(purposeId))
        {
            yield break;
        }

        foreach (FacilityAnchorSlot slot in slots)
        {
            if (slot != null && slot.IsValid && string.Equals(slot.purposeId, purposeId, StringComparison.Ordinal))
            {
                yield return slot;
            }
        }
    }

    public int RemoveInvalidSlots()
    {
        return slots?.RemoveAll(slot => slot == null || !slot.IsValid) ?? 0;
    }
}

[Serializable]
public class FacilityData
{
    public FacilityRole roles;
    [Min(0)] public int capacity = 1;
    [Min(0f)] public float useDuration = 1f;
    [Min(0)] public int requiredWorkers;
    [SerializeField] internal FacilityWorkType supportedWorkTypes;
    public bool disabledWhenDamaged = true;

    public bool IsVisitorFacility => roles != FacilityRole.None && capacity > 0;
    public bool HasSupportedWorkTypes => (int)supportedWorkTypes != 0;
    public IEnumerable<WorkTypeId> SupportedWorkTypeIds
    {
        get
        {
            foreach (WorkTypeDefinition definition in FacilityWorkTypeMap.Enumerate(supportedWorkTypes))
            {
                yield return definition.WorkTypeId;
            }
        }
    }

    public void SetSupportedWorkTypeIds(IEnumerable<WorkTypeId> workTypeIds)
    {
        supportedWorkTypes = FacilityWorkType.None;
        AddSupportedWorkTypeIds(workTypeIds);
    }

    public void AddSupportedWorkTypeIds(IEnumerable<WorkTypeId> workTypeIds)
    {
        if (workTypeIds == null)
        {
            return;
        }

        foreach (WorkTypeId workTypeId in workTypeIds)
        {
            AddSupportedWorkTypeId(workTypeId);
        }
    }

    public void AddSupportedWorkTypeId(WorkTypeId workTypeId)
    {
        if (WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition))
        {
            supportedWorkTypes |= FacilityWorkTypeMap.GetRequired(definition);
        }
    }

    public bool SupportsRole(FacilityRole role)
    {
        return role != FacilityRole.None && (roles & role) != 0;
    }

    public bool SupportsWork(WorkTypeId workTypeId)
    {
        return workTypeId.IsValid
            && WorkTypeCatalog.TryGet(workTypeId, out WorkTypeDefinition definition)
            && SupportsWork(FacilityWorkTypeMap.GetRequired(definition));
    }

    internal bool SupportsWork(FacilityWorkType workType)
    {
        return workType != FacilityWorkType.None && (supportedWorkTypes & workType) != 0;
    }
}

public enum FacilityUseClassification
{
    None = 0,
    Structure = 1,
    Storage = 2,
    Production = 3,
    Service = 4,
    Environment = 5,
    Logistics = 6,
    Combat = 7,
    DomainCommand = 8,
    EventVenue = 9,
    Decoration = 10
}

/// <summary>
/// A typed gameplay entry point owned by an authored facility. This is not a
/// presentation tag: runtime commands query these values directly and the V21
/// connection audit rejects command facilities without a consumer.
/// </summary>
public enum ResearchFacilityCommandKind
{
    None = 0,
    GatheringPreparation = 1,
    BloodStageDrainage = 2,
    LoggingPreparation = 3,
    DirectionalFelling = 4,
    SelectiveBreeding = 5,
    StableHarnessing = 6,
    WildlifeTaming = 7,
    FlowMetering = 8,
    WeaponPatternAccess = 9,
    CropCalendar = 10,
    SoilDiagnostics = 11,
    BreedingSchedule = 12,
    ClimateControl = 13,
    HouseholdRegistry = 14,
    NurseryCare = 15,
    ClassroomEducation = 16,
    SupervisedApprenticeship = 17,
    GenerationArchive = 18,
    AgingAssessment = 19,
    BiologicalAgeMeasurement = 20,
    GeriatricCare = 21,
    ChronicCare = 22,
    PathogenDiagnosis = 23,
    Serology = 24,
    EpidemicBoard = 25,
    GeneticArchive = 26,
    GeneticCounseling = 27,
    FamilyPartition = 28,
    GuardianRegistry = 29,
    CorpseCare = 30,
    ClimateMapping = 31,
    ChronometricNavigation = 32,
    SeedSelection = 33,
    RetireeCare = 34,
    MentorAcademy = 35,
    ResonanceTuning = 36,
    SecureTradeVault = 37,
    DefenseControl = 38,
    ApparelTailoring = 39,
    ApparelDecoration = 40,
    HandLaundry = 41,
    IndoorDrying = 42,
    PoweredLaundry = 43,
    ApparelDisplay = 44,
    DressingChange = 45,
    ApparelRepair = 46,
    FiberSorting = 47,
    FiberScouring = 48,
    ManualSpinning = 49,
    TextileFinishing = 50,
    PoweredSpinning = 51,
    PoweredWeaving = 52
}

public readonly struct GridBuildingPlacement
{
    public int Width { get; }
    public int Height { get; }
    public GridLayer Layer { get; }
    public BuildingCategory Category { get; }
    public bool HorizontalDraggable { get; }
    public bool VerticalDraggable { get; }

    public bool IsMovement => Category == BuildingCategory.Movement;
    public bool IsWall => Category == BuildingCategory.Wall;
    public bool IsStructuralWall => Category == BuildingCategory.Wall && Layer != GridLayer.Hallway;
    public bool IsDraggable => HorizontalDraggable || VerticalDraggable;
    public bool HasEvenWidth => Width % 2 == 0;

    public GridBuildingPlacement(
        int width,
        int height,
        GridLayer layer,
        BuildingCategory category,
        bool horizontalDraggable,
        bool verticalDraggable)
    {
        Width = Mathf.Max(1, width);
        Height = Mathf.Max(1, height);
        Layer = layer;
        Category = category;
        HorizontalDraggable = horizontalDraggable;
        VerticalDraggable = verticalDraggable;
    }

    public List<Vector2Int> GetGridPosList(Vector2Int center)
    {
        List<Vector2Int> posList = new List<Vector2Int>();
        int startX = center.x - (Width / 2);

        for (int i = 0; i < Width; i++)
        {
            for (int j = 0; j < Height; j++)
            {
                posList.Add(new Vector2Int(startX + i, center.y + j));
            }
        }

        return posList;
    }
}

[CreateAssetMenu(menuName = "Grid/Building/SO", order = 0)]
public class BuildingSO : DataScriptableObject, IGridBuildAreaCapability
{
    public const string AbilityModulesFieldName = "abilityModules";

    [Header("Presentation")]
    public string objectName;
    public Sprite sprite;
    public Sprite icon;

    [Header("Authored Content Identity")]
    [SerializeField] private string contentDefinitionId = string.Empty;
    [SerializeField, Min(1)] private int authoringRevision = 1;
    [SerializeField, TextArea] private string sourceNote = string.Empty;

    [Header("Gameplay Execution")]
    [SerializeField] private FacilityUseClassification useClassification;
    [SerializeField] private ResearchFacilityCommandKind researchFacilityCommand;

    [Header("Facility Abilities")]
    [InspectorName("능력 목록")]
    [SerializeField] private BuildingAbilityCollection abilityModules = new BuildingAbilityCollection();

    [Header("Grid Placement")]
    public int width;
    public int height;
    public GridLayer layer;
    public BuildingCategory category;
    public bool horizontalDraggable;
    public bool verticalDraggable;
    public BuildingRuntimeArchetypeKind runtimeArchetype;
    public Dictionary<GridTexture.TilemapLayer, Tile> tiles;
    [Tooltip("이동 시설이 캐릭터를 통과시킬 때 기준점에 더하는 월드 좌표 오프셋")]
    public Vector2 movementAnchorOffset;
    [Min(0f)]
    public float movementTravelTime = 2f;
    public FacilityAnchorData facilityAnchors = new FacilityAnchorData();

    [Header("Game Data")]
    [SerializeField] private List<IBuildingCondition> OnBuildCondition;
    public bool unlocked;

    public GridBuildingPlacement Placement => new GridBuildingPlacement(
        width,
        height,
        layer,
        category,
        horizontalDraggable,
        verticalDraggable);

    public bool IsGridMovement => Placement.IsMovement;
    public bool IsWall => Placement.IsWall;
    public bool IsStructuralWall => Placement.IsStructuralWall;
    public bool IsDoor => runtimeArchetype == BuildingRuntimeArchetypeKind.Door
        || runtimeArchetype == BuildingRuntimeArchetypeKind.InteriorDoor;
    public bool IsInteriorDoor =>
        runtimeArchetype == BuildingRuntimeArchetypeKind.InteriorDoor;
    public GridLayer PlacementLayer => Placement.Layer;
    public bool IsEvenWidth => Placement.HasEvenWidth;
    public bool UsesIndependentRenderer => layer == GridLayer.WallFixture
        || layer == GridLayer.CeilingFixture
        || layer == GridLayer.FloorOverlay
        || layer == GridLayer.Utility
        || layer == GridLayer.Conveyor;
    public FacilityAnchorData FacilityAnchors => facilityAnchors ??= new FacilityAnchorData();
    public BuildingAbilityCollection AbilityModules =>
        abilityModules ??= new BuildingAbilityCollection();
    public string ContentDefinitionId => contentDefinitionId?.Trim() ?? string.Empty;
    public int AuthoringRevision => authoringRevision;
    public string SourceNote => sourceNote?.Trim() ?? string.Empty;
    public FacilityUseClassification UseClassification => useClassification;
    public FacilityUseClassification EffectiveUseClassification =>
        useClassification != FacilityUseClassification.None
            ? useClassification
            : InferUseClassification();
    public ResearchFacilityCommandKind ResearchFacilityCommand =>
        researchFacilityCommand;

#if UNITY_EDITOR
    public void ConfigureGameplayExecution(
        FacilityUseClassification classification,
        ResearchFacilityCommandKind command)
    {
        useClassification = classification;
        researchFacilityCommand = command;
    }
#endif

    private FacilityUseClassification InferUseClassification()
    {
        if (researchFacilityCommand != ResearchFacilityCommandKind.None)
            return FacilityUseClassification.DomainCommand;
        if (GetAbility<BuildingDefenseAbility>() != null
            || GetAbility<BuildingCoverAbility>() != null
            || GetAbility<BuildingSecurityAbility>() != null
            || GetAbility<BuildingTreasuryPoweredDefenseAbility>() != null)
            return FacilityUseClassification.Combat;
        if (GetAbility<BuildingStorageAbility>() != null
            || GetAbility<BuildingInternalStockAbility>() != null
            || GetAbility<BuildingProtectiveEquipmentLockerAbility>() != null
            || GetAbility<BuildingOrganStorageAbility>() != null)
            return FacilityUseClassification.Storage;
        if (GetAbility<BuildingProductionWorkstationAbility>() != null
            || GetAbility<BuildingProductionAbility>() != null
            || GetAbility<BuildingCropPlotAbility>() != null
            || GetAbility<BuildingButcherAbility>() != null
            || GetAbility<BuildingEquipmentCraftingAbility>() != null)
            return FacilityUseClassification.Production;
        if (GetAbility<BuildingConveyorSegmentAbility>() != null
            || GetAbility<BuildingConveyorPortAbility>() != null
            || GetAbility<BuildingConveyorOverflowAbility>() != null
            || GetAbility<BuildingAutomationAbility>() != null)
            return FacilityUseClassification.Logistics;
        if (GetAbility<BuildingThermalEmitterAbility>() != null
            || GetAbility<BuildingAirExchangeAbility>() != null
            || GetAbility<BuildingAirDuctAbility>() != null
            || GetAbility<BuildingTemperatureAbility>() != null
            || GetAbility<BuildingVentilationAbility>() != null
            || GetAbility<BuildingLightingAbility>() != null
            || GetAbility<BuildingUtilityConnectionAbility>() != null
            || GetAbility<BuildingPowerProducerAbility>() != null
            || GetAbility<BuildingPowerConsumerAbility>() != null
            || GetAbility<BuildingPowerStorageAbility>() != null
            || GetAbility<BuildingWaterProducerAbility>() != null
            || GetAbility<BuildingWaterStorageAbility>() != null
            || GetAbility<BuildingWaterFixtureAbility>() != null
            || GetAbility<BuildingWastewaterProcessorAbility>() != null)
            return FacilityUseClassification.Environment;
        if (GetAbility<BuildingCircusStageAbility>() != null
            || GetAbility<BuildingAudienceSeatingAbility>() != null
            || GetAbility<BuildingCircusTicketBoothAbility>() != null
            || GetAbility<BuildingCircusGamblingAbility>() != null
            || GetAbility<BuildingCircusAnnouncerAbility>() != null
            || GetAbility<BuildingCircusHazardAbility>() != null
            || GetAbility<BuildingPublicPunishmentAbility>() != null)
            return FacilityUseClassification.EventVenue;
        if (GetAbility<BuildingServiceAbility>() != null
            || GetAbility<BuildingStaffedServiceAbility>() != null
            || GetAbility<BuildingPaidFacilityServiceAbility>() != null
            || GetAbility<BuildingMedicalAbility>() != null
            || GetAbility<BuildingSurgeryTableAbility>() != null
            || GetAbility<BuildingNeedRecoveryAbility>() != null
            || GetAbility<BuildingTrainingAbility>() != null
            || GetAbility<BuildingReceptionAbility>() != null
            || GetAbility<BuildingCaptiveHousingAbility>() != null
            || GetAbility<BuildingBeastPenAbility>() != null
            || GetAbility<BuildingSeatingAbility>() != null
            || GetAbility<BuildingTableAbility>() != null)
            return FacilityUseClassification.Service;
        if (IsStructuralWall
            || IsDoor
            || category == BuildingCategory.Movement
            || GetAbility<BuildingStructuralIntegrityAbility>() != null)
            return FacilityUseClassification.Structure;
        return FacilityUseClassification.Decoration;
    }
    public int Maintenance
    {
        get => GetAbility<BuildingEconomyAbility>()?.maintenance ?? 0;
        set
        {
            BuildingEconomyAbility economy = GetAbility<BuildingEconomyAbility>();
            if (economy == null)
            {
                if (value <= 0)
                {
                    return;
                }

                economy = new BuildingEconomyAbility();
                (abilityModules ??= new BuildingAbilityCollection()).Add(economy);
            }

            economy.maintenance = Mathf.Max(0, value);
        }
    }

    public FacilityData Facility
    {
        get => GetAbility<BuildingFacilityAbility>()?.settings;
        set => SetDomainAbility(
            value != null ? new BuildingFacilityAbility { settings = value } : null);
    }

    public DefenseFacilityData Defense
    {
        get => GetAbility<BuildingDefenseAbility>()?.settings;
        set => SetDomainAbility(
            value != null ? new BuildingDefenseAbility { settings = value } : null);
    }

    public FacilityEvolutionContributionData Evolution
    {
        get => GetAbility<BuildingEvolutionAbility>()?.settings;
        set => SetDomainAbility(
            value != null ? new BuildingEvolutionAbility { settings = value } : null);
    }

    public IReadOnlyList<BuildingAbility> Abilities => (abilityModules ??= new BuildingAbilityCollection()).Items;

    public void ReplaceAbilities(BuildingAbilityCollection abilities)
    {
        abilityModules = abilities ?? new BuildingAbilityCollection();
    }

#if UNITY_EDITOR
    public void ConfigureAuthoredContentIdentity(
        string definitionId,
        int revision,
        string note)
    {
        contentDefinitionId = definitionId?.Trim() ?? string.Empty;
        authoringRevision = Mathf.Max(1, revision);
        sourceNote = note?.Trim() ?? string.Empty;
    }
#endif

    public IReadOnlyList<IBuildingCondition> BuildConditions => OnBuildCondition != null
        ? ReadOnlyView.List(OnBuildCondition)
        : Array.Empty<IBuildingCondition>();

    public bool TryGetAbility<TAbility>(out TAbility ability)
        where TAbility : BuildingAbility
    {
        return (abilityModules ??= new BuildingAbilityCollection()).TryGet(out ability);
    }

    public TAbility GetAbility<TAbility>()
        where TAbility : BuildingAbility
    {
        return TryGetAbility(out TAbility ability) ? ability : null;
    }

    public void ValidateAbilitiesOrThrow()
    {
        (abilityModules ??= new BuildingAbilityCollection())
            .ValidateOrThrow($"BuildingSO '{name}' (id={id})");
    }

    public List<Vector2Int> GetGridPosList(Vector2Int center)
    {
        return Placement.GetGridPosList(center);
    }

    public bool GetDraggable()
    {
        return Placement.IsDraggable;
    }

    private void SetDomainAbility<TAbility>(TAbility ability)
        where TAbility : BuildingAbility
    {
        abilityModules ??= new BuildingAbilityCollection();
        abilityModules.Remove<TAbility>();
        if (ability != null)
        {
            abilityModules.Add(ability);
        }
    }
}
