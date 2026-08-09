using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[Flags]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum AnatomyAttachmentPoint : uint
{
    None = 0,
    Head = 1u << 0,
    Face = 1u << 1,
    Neck = 1u << 2,
    Torso = 1u << 3,
    Pelvis = 1u << 4,
    ArmLeft = 1u << 5,
    ArmRight = 1u << 6,
    HandLeft = 1u << 7,
    HandRight = 1u << 8,
    LegLeft = 1u << 9,
    LegRight = 1u << 10,
    FootLeft = 1u << 11,
    FootRight = 1u << 12,
    Back = 1u << 13,
    Tail = 1u << 14,
    WingLeft = 1u << 15,
    WingRight = 1u << 16,
    HornSet = 1u << 17,

    Arms = ArmLeft | ArmRight,
    Hands = HandLeft | HandRight,
    Legs = LegLeft | LegRight,
    Feet = FootLeft | FootRight,
    Wings = WingLeft | WingRight,
    OptionalAppendages = Tail | Wings | HornSet
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ApparelLayer
{
    Underwear = 0,
    Inner = 1,
    Outer = 2,
    Armor = 3,
    Accessory = 4
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ApparelSizeClass
{
    Small = 0,
    Medium = 1,
    Large = 2
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ApparelFitMode
{
    Sized = 0,
    Adjustable = 1,
    Accessory = 2
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ApparelBodyForm
{
    Humanoid = 0,
    Construct = 1,
    Any = 2
}

[Flags]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ApparelModificationKind
{
    None = 0,
    TailOpening = 1 << 0,
    WingSlits = 1 << 1,
    HornClearance = 1 << 2
}

[Flags]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ApparelUseTag
{
    None = 0,
    Underwear = 1 << 0,
    Sleep = 1 << 1,
    Daily = 1 << 2,
    Work = 1 << 3,
    Cold = 1 << 4,
    Heat = 1 << 5,
    Wet = 1 << 6,
    Medical = 1 << 7,
    Formal = 1 << 8,
    Cultural = 1 << 9,
    Accessory = 1 << 10,
    Protective = 1 << 11
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum TextileConditionBand
{
    Ready = 0,
    Wet = 1,
    Contaminated = 2
}

[Flags]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum TextileMaterialTag
{
    None = 0,
    Woven = 1 << 0,
    NonWoven = 1 << 1,
    Plant = 1 << 2,
    Animal = 1 << 3,
    Arcane = 1 << 4,
    Cold = 1 << 5,
    Heat = 1 << 6,
    Wet = 1 << 7,
    Sterile = 1 << 8,
    Durable = 1 << 9,
    Light = 1 << 10,
    Airborne = 1 << 11
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum TextileSourceKind
{
    Unknown = 0,
    Crop = 1,
    Animal = 2,
    Synthetic = 3,
    Arcane = 4,
    Salvaged = 5
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ApparelMaterialSelectionPolicy
{
    ExactMaterial = 0,
    LowestHandlingDifficulty = 1,
    LowestCost = 2,
    HighestWarmth = 3,
    LowestWeight = 4,
    HighestDurability = 5
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class TextileConditionRules
{
    public static TextileConditionBand ResolveCondition(
        float moisture,
        float contamination)
    {
        if (contamination > 0f)
        {
            return TextileConditionBand.Contaminated;
        }

        return moisture >= 20f
            ? TextileConditionBand.Wet
            : TextileConditionBand.Ready;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct ApparelMaterialProvenance :
    IEquatable<ApparelMaterialProvenance>
{
    public ApparelMaterialProvenance(
        int materialCatalogIndex,
        TextileSourceKind sourceKind,
        int sourceCatalogIndex,
        int craftedAbsoluteDay,
        ulong deterministicBatchHash)
    {
        MaterialCatalogIndex = Math.Max(-1, materialCatalogIndex);
        SourceKind = sourceKind;
        SourceCatalogIndex = Math.Max(-1, sourceCatalogIndex);
        CraftedAbsoluteDay = Math.Max(0, craftedAbsoluteDay);
        DeterministicBatchHash = deterministicBatchHash;
    }

    public int MaterialCatalogIndex { get; }
    public TextileSourceKind SourceKind { get; }
    public int SourceCatalogIndex { get; }
    public int CraftedAbsoluteDay { get; }
    public ulong DeterministicBatchHash { get; }

    public bool Equals(ApparelMaterialProvenance other) =>
        MaterialCatalogIndex == other.MaterialCatalogIndex
        && SourceKind == other.SourceKind
        && SourceCatalogIndex == other.SourceCatalogIndex
        && CraftedAbsoluteDay == other.CraftedAbsoluteDay
        && DeterministicBatchHash == other.DeterministicBatchHash;

    public override bool Equals(object obj) =>
        obj is ApparelMaterialProvenance other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(
        MaterialCatalogIndex,
        (int)SourceKind,
        SourceCatalogIndex,
        CraftedAbsoluteDay,
        DeterministicBatchHash);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct ApparelDerivedStats
{
    public ApparelDerivedStats(
        float warmth,
        float heatResistance,
        float waterResistance,
        float airborneResistance,
        float sterility,
        float durability,
        float weight,
        float comfort,
        float movementMultiplier)
    {
        Warmth = Mathf.Clamp01(warmth);
        HeatResistance = Mathf.Clamp01(heatResistance);
        WaterResistance = Mathf.Clamp01(waterResistance);
        AirborneResistance = Mathf.Clamp01(airborneResistance);
        Sterility = Mathf.Clamp01(sterility);
        Durability = Mathf.Max(1f, durability);
        Weight = Mathf.Max(0.01f, weight);
        Comfort = Mathf.Clamp(comfort, -100f, 100f);
        MovementMultiplier = Mathf.Clamp(movementMultiplier, 0.1f, 2f);
    }

    public float Warmth { get; }
    public float HeatResistance { get; }
    public float WaterResistance { get; }
    public float AirborneResistance { get; }
    public float Sterility { get; }
    public float Durability { get; }
    public float Weight { get; }
    public float Comfort { get; }
    public float MovementMultiplier { get; }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ApparelInstanceState
{
    public string apparelDefinitionId = string.Empty;
    public string primaryMaterialId = string.Empty;
    public CraftsmanshipQualityTier craftsmanshipQuality =
        CraftsmanshipQualityTier.Normal;
    public TextileSourceKind sourceKind;
    public string sourceDefinitionId = string.Empty;
    public ApparelSizeClass size = ApparelSizeClass.Medium;
    public ApparelModificationKind modifications;
    public ApparelModificationKind closedOpenings;
    [Range(0f, 100f)] public float durability = 100f;
    [Range(0f, 100f)] public float moisture;
    [Range(0f, 100f)] public float contamination;
    public string designatedWearerCharacterId = string.Empty;
    public int craftedAbsoluteDay;
    public ulong deterministicBatchHash;
}

[CreateAssetMenu(
    fileName = "Apparel",
    menuName = "DungeonStory/Apparel/Apparel Definition")]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class ApparelDefinitionSO : DataScriptableObject
{
    [SerializeField] private string apparelId = string.Empty;
    [SerializeField] private string physicalItemId = string.Empty;
    [SerializeField] private string displayName = string.Empty;
    [TextArea, SerializeField] private string description = string.Empty;
    [SerializeField] private ApparelBodyForm bodyForm = ApparelBodyForm.Humanoid;
    [SerializeField] private ApparelLayer layer = ApparelLayer.Inner;
    [SerializeField] private ApparelFitMode fitMode = ApparelFitMode.Sized;
    [SerializeField] private AnatomyAttachmentPoint requiredPoints;
    [SerializeField] private AnatomyAttachmentPoint occupiedPoints;
    [SerializeField] private AnatomyAttachmentPoint sealedOptionalPoints;
    [SerializeField] private ApparelModificationKind supportedModifications;
    [SerializeField] private ApparelUseTag useTags;
    [SerializeField] private TextileMaterialTag allowedMaterialTags =
        TextileMaterialTag.Woven | TextileMaterialTag.NonWoven;
    [Min(0.05f), SerializeField] private float tailoringCoefficient = 1f;
    [Min(0.01f), SerializeField] private float baseWeight = 0.5f;
    [SerializeField] private string requiredResearchId = string.Empty;
    [SerializeField] private Sprite sprite;

    public string ApparelId => apparelId?.Trim() ?? string.Empty;
    public string PhysicalItemId => physicalItemId?.Trim() ?? string.Empty;
    public string DisplayName => string.IsNullOrWhiteSpace(displayName)
        ? name
        : displayName.Trim();
    public string Description => description?.Trim() ?? string.Empty;
    public ApparelBodyForm BodyForm => bodyForm;
    public ApparelLayer Layer => layer;
    public ApparelFitMode FitMode => fitMode;
    public AnatomyAttachmentPoint RequiredPoints => requiredPoints;
    public AnatomyAttachmentPoint OccupiedPoints => occupiedPoints;
    public AnatomyAttachmentPoint SealedOptionalPoints => sealedOptionalPoints;
    public ApparelModificationKind SupportedModifications => supportedModifications;
    public ApparelUseTag UseTags => useTags;
    public TextileMaterialTag AllowedMaterialTags => allowedMaterialTags;
    public float TailoringCoefficient => Mathf.Max(0.05f, tailoringCoefficient);
    public float BaseWeight => Mathf.Max(0.01f, baseWeight);
    public string RequiredResearchId => requiredResearchId?.Trim() ?? string.Empty;
    public Sprite Sprite => sprite;

#if UNITY_EDITOR
    public void Configure(
        string stableId,
        string itemId,
        string name,
        string details,
        ApparelBodyForm form,
        ApparelLayer apparelLayer,
        ApparelFitMode fitting,
        AnatomyAttachmentPoint required,
        AnatomyAttachmentPoint occupied,
        AnatomyAttachmentPoint sealedPoints,
        ApparelModificationKind alterations,
        ApparelUseTag tags,
        TextileMaterialTag materialTags,
        float coefficient,
        float weight,
        string researchId,
        Sprite icon = null)
    {
        apparelId = stableId?.Trim() ?? string.Empty;
        physicalItemId = itemId?.Trim() ?? string.Empty;
        displayName = name?.Trim() ?? string.Empty;
        description = details?.Trim() ?? string.Empty;
        bodyForm = form;
        layer = apparelLayer;
        fitMode = fitting;
        requiredPoints = required;
        occupiedPoints = occupied;
        sealedOptionalPoints = sealedPoints;
        supportedModifications = alterations;
        useTags = tags;
        allowedMaterialTags = materialTags;
        tailoringCoefficient = Mathf.Max(0.05f, coefficient);
        baseWeight = Mathf.Max(0.01f, weight);
        requiredResearchId = researchId?.Trim() ?? string.Empty;
        sprite = icon;
    }
#endif
}

public interface IApparelDefinitionCatalog
{
    IReadOnlyList<ApparelDefinitionSO> Definitions { get; }
    bool TryGet(string apparelId, out ApparelDefinitionSO definition);
    bool TryGetByItemId(string itemId, out ApparelDefinitionSO definition);
    int GetIndex(string apparelId);
}

public interface ITextileMaterialCatalog
{
    IReadOnlyList<TextileMaterialDefinitionSO> Definitions { get; }
    bool TryGet(string materialId, out TextileMaterialDefinitionSO definition);
    bool TryGetByItemId(string itemId, out TextileMaterialDefinitionSO definition);
    int GetIndex(string materialId);
}

public sealed class ResourceApparelDefinitionCatalog : IApparelDefinitionCatalog
{
    private readonly ApparelDefinitionSO[] definitions;
    private readonly Dictionary<string, ApparelDefinitionSO> byId;
    private readonly Dictionary<string, ApparelDefinitionSO> byItemId;
    private readonly Dictionary<string, int> indices;

    public ResourceApparelDefinitionCatalog(IGameContentDefinitionSource content)
    {
        definitions = (content ?? throw new ArgumentNullException(nameof(content)))
            .GetAll<ApparelDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.ApparelId, StringComparer.Ordinal)
            .ToArray();
        ValidateUnique(definitions.Select(value => value.ApparelId), "apparel");
        ValidateUnique(definitions.Select(value => value.PhysicalItemId), "apparel item");
        byId = definitions.ToDictionary(value => value.ApparelId, StringComparer.Ordinal);
        byItemId = definitions.ToDictionary(value => value.PhysicalItemId, StringComparer.Ordinal);
        indices = definitions.Select((value, index) => (value, index))
            .ToDictionary(value => value.value.ApparelId, value => value.index, StringComparer.Ordinal);
    }

    public IReadOnlyList<ApparelDefinitionSO> Definitions => definitions;
    public bool TryGet(string apparelId, out ApparelDefinitionSO definition) =>
        byId.TryGetValue(apparelId?.Trim() ?? string.Empty, out definition);
    public bool TryGetByItemId(string itemId, out ApparelDefinitionSO definition) =>
        byItemId.TryGetValue(itemId?.Trim() ?? string.Empty, out definition);
    public int GetIndex(string apparelId) => indices.TryGetValue(
        apparelId?.Trim() ?? string.Empty,
        out int index) ? index : -1;

    private static void ValidateUnique(IEnumerable<string> source, string label)
    {
        string[] values = source.ToArray();
        if (values.Any(string.IsNullOrWhiteSpace)
            || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
        {
            throw new InvalidOperationException(
                $"V22 {label} definitions require non-empty unique stable IDs.");
        }
    }
}

public sealed class ResourceTextileMaterialCatalog : ITextileMaterialCatalog
{
    private readonly TextileMaterialDefinitionSO[] definitions;
    private readonly Dictionary<string, TextileMaterialDefinitionSO> byId;
    private readonly Dictionary<string, TextileMaterialDefinitionSO> byItemId;
    private readonly Dictionary<string, int> indices;

    public ResourceTextileMaterialCatalog(IGameContentDefinitionSource content)
    {
        definitions = (content ?? throw new ArgumentNullException(nameof(content)))
            .GetAll<TextileMaterialDefinitionSO>()
            .Where(value => value != null)
            .OrderBy(value => value.MaterialId, StringComparer.Ordinal)
            .ToArray();
        ValidateUnique(definitions.Select(value => value.MaterialId), "textile");
        ValidateUnique(definitions.Select(value => value.PhysicalItemId), "textile item");
        byId = definitions.ToDictionary(value => value.MaterialId, StringComparer.Ordinal);
        byItemId = definitions.ToDictionary(value => value.PhysicalItemId, StringComparer.Ordinal);
        indices = definitions.Select((value, index) => (value, index))
            .ToDictionary(value => value.value.MaterialId, value => value.index, StringComparer.Ordinal);
    }

    public IReadOnlyList<TextileMaterialDefinitionSO> Definitions => definitions;
    public bool TryGet(string materialId, out TextileMaterialDefinitionSO definition) =>
        byId.TryGetValue(materialId?.Trim() ?? string.Empty, out definition);
    public bool TryGetByItemId(string itemId, out TextileMaterialDefinitionSO definition) =>
        byItemId.TryGetValue(itemId?.Trim() ?? string.Empty, out definition);
    public int GetIndex(string materialId) => indices.TryGetValue(
        materialId?.Trim() ?? string.Empty,
        out int index) ? index : -1;

    private static void ValidateUnique(IEnumerable<string> source, string label)
    {
        string[] values = source.ToArray();
        if (values.Any(string.IsNullOrWhiteSpace)
            || values.Distinct(StringComparer.Ordinal).Count() != values.Length)
        {
            throw new InvalidOperationException(
                $"V22 {label} definitions require non-empty unique stable IDs.");
        }
    }
}
