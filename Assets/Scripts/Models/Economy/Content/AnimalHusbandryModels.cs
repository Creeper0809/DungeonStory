using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct WildlifeInstanceId : IEquatable<WildlifeInstanceId>
{
    private readonly string value;

    public WildlifeInstanceId(string value) =>
        this.value = PersistentEntityId.Normalize(value);

    public string Value => value ?? string.Empty;
    public bool IsValid => PersistentEntityId.IsKind(Value, "wild");
    public bool Equals(WildlifeInstanceId other) =>
        PersistentEntityId.Equals(Value, other.Value);
    public override bool Equals(object obj) =>
        obj is WildlifeInstanceId other && Equals(other);
    public override int GetHashCode() =>
        PersistentEntityId.GetHashCode(Value);
    public override string ToString() => Value;
    public static explicit operator WildlifeInstanceId(string value) => new(value);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct WildlifeSpeciesId : IEquatable<WildlifeSpeciesId>
{
    private readonly string value;

    public WildlifeSpeciesId(string value) =>
        this.value = PersistentEntityId.Normalize(value);

    public string Value => value ?? string.Empty;
    public bool IsValid => !string.IsNullOrWhiteSpace(Value);
    public bool Equals(WildlifeSpeciesId other) =>
        PersistentEntityId.Equals(Value, other.Value);
    public override bool Equals(object obj) =>
        obj is WildlifeSpeciesId other && Equals(other);
    public override int GetHashCode() =>
        PersistentEntityId.GetHashCode(Value);
    public override string ToString() => Value;
    public static explicit operator WildlifeSpeciesId(string value) => new(value);
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum AnimalSex
{
    Female = 0,
    Male = 1
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum AnimalGrowthStage
{
    Juvenile = 0,
    Adult = 1,
    Elder = 2
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum AnimalHusbandryWorkKind
{
    None = 0,
    Tame = 1,
    CollectProduct = 2,
    CollectManure = 3,
    Slaughter = 4
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum AnimalPenCompatibilityIssueKind
{
    PredatorPrey = 0,
    Aggression = 1,
    BodySize = 2,
    Overcrowding = 3,
    FeedConflict = 4
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum AnimalHusbandryFailureCode
{
    None = 0,
    InvalidPenId,
    UnknownSpecies,
    AnimalNotFound,
    PregnantAnimalProtected,
    InvalidPen,
    NoPendingWork
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct AnimalHusbandryFailure :
    IEquatable<AnimalHusbandryFailure>
{
    private readonly string[] parameters;

    public AnimalHusbandryFailure(
        AnimalHusbandryFailureCode code,
        params string[] parameters)
    {
        Code = code;
        this.parameters = parameters ?? Array.Empty<string>();
    }

    public AnimalHusbandryFailureCode Code { get; }
    public IReadOnlyList<string> Parameters => parameters ?? Array.Empty<string>();
    public bool IsFailure => Code != AnimalHusbandryFailureCode.None;
    public static AnimalHusbandryFailure None => new(AnimalHusbandryFailureCode.None);

    public bool Equals(AnimalHusbandryFailure other)
    {
        if (Code != other.Code || Parameters.Count != other.Parameters.Count)
        {
            return false;
        }
        for (int index = 0; index < Parameters.Count; index++)
        {
            if (!string.Equals(
                    Parameters[index],
                    other.Parameters[index],
                    StringComparison.Ordinal))
            {
                return false;
            }
        }
        return true;
    }

    public override bool Equals(object obj) =>
        obj is AnimalHusbandryFailure other && Equals(other);

    public override int GetHashCode()
    {
        HashCode hash = new();
        hash.Add(Code);
        foreach (string parameter in Parameters)
        {
            hash.Add(parameter, StringComparer.Ordinal);
        }
        return hash.ToHashCode();
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public enum AnimalHusbandryStatusCode
{
    None = 0,
    SlaughterDesignated,
    SlaughterDesignationCleared,
    TamingCompleted,
    ProductCollected,
    ProductStorageUnavailable,
    ManureCollected,
    ManureStorageUnavailable,
    TamedAnimal,
    AwaitingTaming,
    Brooding,
    Pregnant,
    BirthWaitingForPenCapacity,
    HatchedJuvenile,
    NewbornJuvenile,
    HatchingCompleted,
    BirthCompleted,
    AutoSlaughterPolicyTarget
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AnimalProductProgressState
{
    public ItemDefinitionId ItemId { get; set; }
    public float ProgressDays { get; set; }
    public int ReadyCycles { get; set; }

    public AnimalProductProgressState Clone()
    {
        return (AnimalProductProgressState)MemberwiseClone();
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class HusbandryAnimalState
{
    public WildlifeInstanceId AnimalId { get; set; }
    public WildlifeSpeciesId SpeciesId { get; set; }
    public BuildingInstanceId PenId { get; set; }
    public AnimalSex Sex { get; set; }
    public float AgeDays { get; set; }
    public bool Tamed { get; set; }
    public float TamingProgress { get; set; }
    public bool Pregnant { get; set; }
    public float PregnancyProgressDays { get; set; }
    public WildlifeInstanceId OtherParentId { get; set; }
    public float BreedingCooldownDays { get; set; }
    public float ManureProgressDays { get; set; }
    public int ReadyManureCycles { get; set; }
    public bool SlaughterDesignated { get; set; }
    public bool AutoSlaughterDesignated { get; set; }
    public AnimalHusbandryWorkKind PendingWorkKind { get; set; }
    public ItemDefinitionId PendingProductItemId { get; set; }
    public float PendingWorkCompleted { get; set; }
    public AnimalHusbandryStatusCode StatusCode { get; set; }
    public List<string> StatusParameters { get; set; } = new();
    public List<AnimalProductProgressState> Products { get; set; } =
        new List<AnimalProductProgressState>();

    public HusbandryAnimalState Clone()
    {
        HusbandryAnimalState clone = (HusbandryAnimalState)MemberwiseClone();
        clone.Products = (Products ?? new List<AnimalProductProgressState>())
            .ConvertAll(item => item?.Clone());
        clone.StatusParameters = new List<string>(
            StatusParameters ?? new List<string>());
        return clone;
    }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AnimalPenPolicyData
{
    public BuildingInstanceId PenId { get; set; }
    public List<WildlifeSpeciesId> AllowedSpeciesIds { get; set; } = new();
    public bool allowHerbivores = true;
    public bool allowOmnivores = true;
    public bool allowCarnivores;
    public bool allowScavengers;
    public bool allowFemales = true;
    public bool allowMales = true;
    public bool allowJuveniles = true;
    [Min(1)] public int maximumAnimals = 8;
    public bool breedingAllowed = true;
    public bool protectPregnant = true;
    public bool allowRiskyMixing;
    [Min(0)] public int adultFemaleLimit = 6;
    [Min(0)] public int adultMaleLimit = 2;
    [Min(0)] public int juvenileLimit = 6;
    [Min(0)] public int minimumBreedingFemales = 1;
    [Min(0)] public int minimumBreedingMales = 1;

    public AnimalPenPolicyData Clone()
    {
        AnimalPenPolicyData clone = (AnimalPenPolicyData)MemberwiseClone();
        clone.AllowedSpeciesIds = new List<WildlifeSpeciesId>(
            AllowedSpeciesIds ?? new List<WildlifeSpeciesId>());
        return clone;
    }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AnimalProductProgressSaveData
{
    public string itemDefinitionId = string.Empty;
    public float progressDays;
    public int readyCycles;
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class HusbandryAnimalSaveData
{
    public string animalInstanceId = string.Empty;
    public string speciesDefinitionId = string.Empty;
    public string penBuildingInstanceId = string.Empty;
    public AnimalSex sex;
    public float ageDays;
    public bool tamed;
    public float tamingProgress;
    public bool pregnant;
    public float pregnancyProgressDays;
    public string otherParentAnimalInstanceId = string.Empty;
    public float breedingCooldownDays;
    public float manureProgressDays;
    public int readyManureCycles;
    public bool slaughterDesignated;
    public bool autoSlaughterDesignated;
    public AnimalHusbandryWorkKind pendingWorkKind;
    public string pendingProductItemDefinitionId = string.Empty;
    public float pendingWorkCompleted;
    public AnimalHusbandryStatusCode statusCode;
    public List<string> statusParameters = new();
    public List<AnimalProductProgressSaveData> products = new();
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AnimalPenPolicySaveData
{
    public string penBuildingInstanceId = string.Empty;
    public List<string> allowedSpeciesDefinitionIds = new();
    public bool allowHerbivores = true;
    public bool allowOmnivores = true;
    public bool allowCarnivores;
    public bool allowScavengers;
    public bool allowFemales = true;
    public bool allowMales = true;
    public bool allowJuveniles = true;
    public int maximumAnimals = 8;
    public bool breedingAllowed = true;
    public bool protectPregnant = true;
    public bool allowRiskyMixing;
    public int adultFemaleLimit = 6;
    public int adultMaleLimit = 2;
    public int juvenileLimit = 6;
    public int minimumBreedingFemales = 1;
    public int minimumBreedingMales = 1;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AnimalPenCompatibilityIssue
{
    public AnimalPenCompatibilityIssue(
        AnimalPenCompatibilityIssueKind kind,
        float severity,
        params string[] parameters)
    {
        Kind = kind;
        Severity = Mathf.Clamp01(severity);
        Parameters = parameters ?? Array.Empty<string>();
    }

    public AnimalPenCompatibilityIssueKind Kind { get; }
    public float Severity { get; }
    public IReadOnlyList<string> Parameters { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AnimalPenCompatibilityResult
{
    public BuildingInstanceId PenId { get; set; }
    public float Risk { get; set; }
    public IReadOnlyList<AnimalPenCompatibilityIssue> Issues { get; set; } =
        Array.Empty<AnimalPenCompatibilityIssue>();
    public bool HasDanger => Risk >= 0.5f;
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public readonly struct AnimalHusbandryWorkSnapshot
{
    public AnimalHusbandryWorkSnapshot(
        bool available,
        WildlifeInstanceId animalId,
        AnimalHusbandryWorkKind kind,
        float requiredWork,
        float completedWork,
        AnimalHusbandryFailure failure)
    {
        Available = available;
        AnimalId = animalId;
        Kind = kind;
        RequiredWork = Mathf.Max(1f, requiredWork);
        CompletedWork = Mathf.Clamp(completedWork, 0f, RequiredWork);
        Failure = failure;
    }

    public bool Available { get; }
    public WildlifeInstanceId AnimalId { get; }
    public AnimalHusbandryWorkKind Kind { get; }
    public float RequiredWork { get; }
    public float CompletedWork { get; }
    public AnimalHusbandryFailure Failure { get; }
}

[Serializable]
[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class DungeonAnimalHusbandrySaveData
{
    public const int CurrentVersion = 2;
    public int version = CurrentVersion;
    public List<HusbandryAnimalSaveData> animals = new();
    public List<AnimalPenPolicySaveData> penPolicies = new();
}
