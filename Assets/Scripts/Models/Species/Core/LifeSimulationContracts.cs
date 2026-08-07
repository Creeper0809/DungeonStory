using System;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum CharacterLifeStage
{
    Infant = 0,
    Child = 1,
    Adolescent = 2,
    Adult = 3,
    Elder = 4
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ReproductionMode
{
    Pregnancy = 0,
    Egg = 1,
    Spore = 2,
    CoreDivision = 3,
    GolemAssembly = 4
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ReproductiveRole
{
    None = 0,
    Carrier = 1,
    Contributor = 2,
    Layer = 3,
    Fertilizer = 4,
    SporeContributor = 5,
    DivisionCore = 6,
    Assembler = 7
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum ReproductionPhaseKind
{
    Attempt = 0,
    Pregnancy = 1,
    Delivery = 2,
    Recovery = 3,
    EggFormation = 4,
    Laying = 5,
    Incubation = 6,
    SporeMixing = 7,
    MycelialExpansion = 8,
    Fruiting = 9,
    BiomassAccumulation = 10,
    CoreDivision = 11,
    Stabilization = 12,
    FrameAssembly = 13,
    CoreInscription = 14,
    PersonalityInscription = 15,
    Activation = 16
}

[Serializable]
public sealed class ReproductionPhaseDefinition
{
    public ReproductionPhaseKind phase;
    [Min(1)] public int durationDays = 1;
}
