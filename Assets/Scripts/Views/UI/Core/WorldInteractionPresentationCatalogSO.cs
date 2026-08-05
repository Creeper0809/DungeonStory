using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CharacterCarryVisualKind
{
    None = 0,
    Tray = 1,
    Crate = 2,
    Sack = 3,
    Backpack = 4
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CharacterPropSortingMode
{
    Front = 0,
    Back = 1,
    FacingSide = 2
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CharacterWorldActionKind
{
    Idle = 0,
    Move = 1,
    Carry = 2,
    Construct = 3,
    Repair = 4,
    Clean = 5,
    Craft = 6,
    Cook = 7,
    Eat = 8,
    Drink = 9,
    Hygiene = 10,
    Rest = 11,
    Reception = 12,
    Payment = 13,
    Combat = 14,
    Medical = 15
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class CharacterPropAttachmentProfile
{
    public string speciesOrAnatomyId = "default";
    public CharacterCarryVisualKind carryKind;
    public Vector2 rightFacingOffsetPixels = new Vector2(5f, 7f);
    public bool mirrorOffsetX = true;
    public bool synchronizeFlipX = true;
    public CharacterPropSortingMode sortingMode = CharacterPropSortingMode.Front;
    public int sortingOrderOffset = 1;
    [Range(1, 4)] public int minimumBodyOverlapPixels = 1;
    public Vector3 lightLoadScale = Vector3.one * 0.82f;
    public Vector3 normalLoadScale = Vector3.one;
    public Vector3 overloadedScale = Vector3.one * 1.12f;

    public void Normalize()
    {
        speciesOrAnatomyId = string.IsNullOrWhiteSpace(speciesOrAnatomyId)
            ? "default"
            : speciesOrAnatomyId.Trim();
        minimumBodyOverlapPixels = Mathf.Clamp(minimumBodyOverlapPixels, 1, 4);
        if (sortingMode == CharacterPropSortingMode.Front)
        {
            sortingOrderOffset = Mathf.Max(1, sortingOrderOffset);
        }
        else if (sortingMode == CharacterPropSortingMode.Back)
        {
            sortingOrderOffset = Mathf.Min(-1, sortingOrderOffset);
        }

        lightLoadScale = ClampScale(lightLoadScale, 0.65f, 1.2f);
        normalLoadScale = ClampScale(normalLoadScale, 0.75f, 1.25f);
        overloadedScale = ClampScale(overloadedScale, 0.85f, 1.35f);
    }

    private static Vector3 ClampScale(Vector3 value, float minimum, float maximum)
    {
        return new Vector3(
            Mathf.Clamp(value.x, minimum, maximum),
            Mathf.Clamp(value.y, minimum, maximum),
            1f);
    }
}

[CreateAssetMenu(
    fileName = "WorldInteractionPresentationCatalog",
    menuName = "DungeonStory/Presentation/World Interaction Catalog")]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class WorldInteractionPresentationCatalogSO : ScriptableObject
{
    public const int PixelsPerUnit = 16;
    public const string ResourcePath =
        "SO/Presentation/WorldInteractionPresentationCatalog";

    [SerializeField, Min(0.05f)] private float walkFrameSeconds = 0.11f;
    [SerializeField, Range(0f, 5f)] private float maximumImpactRotation = 5f;
    [SerializeField, Range(0f, 0.25f)] private float impactDuration = 0.1f;
    [Header("World Simulation Tiles")]
    [SerializeField] private Tile worldWaterTile;
    [SerializeField] private Tile worldFilthTile;
    [SerializeField] private List<CharacterPropAttachmentProfile> propProfiles =
        new List<CharacterPropAttachmentProfile>();

    private static readonly int[] WalkYOffsetPixels = { 0, 1, 1, 0 };
    private static readonly int[] WalkXOffsetPixels = { 0, 0, 1, 0 };
    private static readonly float[] WalkSquash = { 1f, 0.96f, 1f, 1f };

    public float WalkFrameSeconds => Mathf.Max(0.05f, walkFrameSeconds);
    public float MaximumImpactRotation => Mathf.Clamp(maximumImpactRotation, 0f, 5f);
    public float ImpactDuration => Mathf.Clamp(impactDuration, 0f, 0.25f);
    public Tile WorldWaterTile => worldWaterTile;
    public Tile WorldFilthTile => worldFilthTile;
    public IReadOnlyList<CharacterPropAttachmentProfile> PropProfiles => propProfiles;

    public int GetWalkFrameIndex(float gameTime)
    {
        int frameCount = WalkYOffsetPixels.Length;
        return Mathf.Abs(Mathf.FloorToInt(gameTime / WalkFrameSeconds)) % frameCount;
    }

    public int GetWalkYOffsetPixels(int frameIndex)
    {
        return WalkYOffsetPixels[NormalizeFrame(frameIndex)];
    }

    public int GetWalkXOffsetPixels(int frameIndex)
    {
        return WalkXOffsetPixels[NormalizeFrame(frameIndex)];
    }

    public float GetWalkSquash(int frameIndex)
    {
        return WalkSquash[NormalizeFrame(frameIndex)];
    }

    public CharacterPropAttachmentProfile ResolvePropProfile(
        string speciesOrAnatomyId,
        CharacterCarryVisualKind kind)
    {
        string requested = string.IsNullOrWhiteSpace(speciesOrAnatomyId)
            ? "default"
            : speciesOrAnatomyId.Trim();
        CharacterPropAttachmentProfile fallback = null;
        for (int i = 0; i < propProfiles.Count; i++)
        {
            CharacterPropAttachmentProfile profile = propProfiles[i];
            if (profile == null || profile.carryKind != kind)
            {
                continue;
            }

            if (string.Equals(
                    profile.speciesOrAnatomyId,
                    requested,
                    StringComparison.OrdinalIgnoreCase))
            {
                return profile;
            }

            if (string.Equals(
                    profile.speciesOrAnatomyId,
                    "default",
                    StringComparison.OrdinalIgnoreCase))
            {
                fallback = profile;
            }
        }

        return fallback ?? CreateDefaultProfile(kind);
    }

    public void InitializeDefaults()
    {
        propProfiles.Clear();
        foreach (CharacterCarryVisualKind kind in Enum.GetValues(
                     typeof(CharacterCarryVisualKind)))
        {
            if (kind != CharacterCarryVisualKind.None)
            {
                propProfiles.Add(CreateDefaultProfile(kind));
            }
        }

        CharacterPropAttachmentProfile slimeCrate =
            CreateDefaultProfile(CharacterCarryVisualKind.Crate);
        slimeCrate.speciesOrAnatomyId = "Slime";
        slimeCrate.rightFacingOffsetPixels = new Vector2(4f, 5f);
        slimeCrate.minimumBodyOverlapPixels = 2;
        slimeCrate.Normalize();
        propProfiles.Add(slimeCrate);
    }

    private void OnEnable()
    {
        if (propProfiles.Count == 0)
        {
            InitializeDefaults();
        }

        for (int i = 0; i < propProfiles.Count; i++)
        {
            propProfiles[i]?.Normalize();
        }
    }

    private static CharacterPropAttachmentProfile CreateDefaultProfile(
        CharacterCarryVisualKind kind)
    {
        CharacterPropAttachmentProfile profile = new CharacterPropAttachmentProfile
        {
            carryKind = kind,
            sortingMode = kind == CharacterCarryVisualKind.Backpack
                ? CharacterPropSortingMode.Back
                : CharacterPropSortingMode.Front,
            sortingOrderOffset = kind == CharacterCarryVisualKind.Backpack ? -1 : 1
        };

        switch (kind)
        {
            case CharacterCarryVisualKind.Tray:
                profile.rightFacingOffsetPixels = new Vector2(6f, 7f);
                break;
            case CharacterCarryVisualKind.Sack:
                profile.rightFacingOffsetPixels = new Vector2(5f, 6f);
                break;
            case CharacterCarryVisualKind.Backpack:
                profile.rightFacingOffsetPixels = new Vector2(-4f, 7f);
                break;
            default:
                profile.rightFacingOffsetPixels = new Vector2(5f, 7f);
                break;
        }

        profile.Normalize();
        return profile;
    }

    private static int NormalizeFrame(int frameIndex)
    {
        int frameCount = WalkYOffsetPixels.Length;
        int normalized = frameIndex % frameCount;
        return normalized < 0 ? normalized + frameCount : normalized;
    }
}
