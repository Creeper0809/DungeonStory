using System;
using UnityEngine.Scripting.APIUpdating;

// Shared anatomy/garment fit vocabulary. These numeric values are persisted
// by authored assets and saves; neither medical nor economy owns a copy.
[Flags]
[MovedFrom(true, sourceAssembly: "DungeonStory.Economy")]
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

[MovedFrom(true, sourceAssembly: "DungeonStory.Economy")]
public enum ApparelSizeClass
{
    Small = 0,
    Medium = 1,
    Large = 2
}

[MovedFrom(true, sourceAssembly: "DungeonStory.Economy")]
public enum ApparelBodyForm
{
    Humanoid = 0,
    Construct = 1,
    Any = 2
}

