using System;
using System.Collections.Generic;
using UnityEngine;

public enum AnimalSex
{
    Female = 0,
    Male = 1
}

public enum AnimalGrowthStage
{
    Juvenile = 0,
    Adult = 1,
    Elder = 2
}

public enum AnimalHusbandryWorkKind
{
    None = 0,
    Tame = 1,
    CollectProduct = 2,
    CollectManure = 3,
    Slaughter = 4
}

public enum AnimalPenCompatibilityIssueKind
{
    PredatorPrey = 0,
    Aggression = 1,
    BodySize = 2,
    Overcrowding = 3,
    FeedConflict = 4
}

[Serializable]
public sealed class AnimalProductProgressState
{
    public string itemId = string.Empty;
    public float progressDays;
    public int readyCycles;

    public AnimalProductProgressState Clone()
    {
        return (AnimalProductProgressState)MemberwiseClone();
    }
}

[Serializable]
public sealed class HusbandryAnimalState
{
    public string wildlifeId = string.Empty;
    public string speciesId = string.Empty;
    public string penId = string.Empty;
    public AnimalSex sex;
    public float ageDays;
    public bool tamed;
    [Range(0f, 1f)] public float tamingProgress;
    public bool pregnant;
    public float pregnancyProgressDays;
    public string otherParentId = string.Empty;
    public float breedingCooldownDays;
    public float manureProgressDays;
    public int readyManureCycles;
    public bool slaughterDesignated;
    public bool autoSlaughterDesignated;
    public AnimalHusbandryWorkKind pendingWorkKind;
    public string pendingProductItemId = string.Empty;
    public float pendingWorkCompleted;
    public string lastStatus = string.Empty;
    public List<AnimalProductProgressState> products =
        new List<AnimalProductProgressState>();

    public HusbandryAnimalState Clone()
    {
        HusbandryAnimalState clone = (HusbandryAnimalState)MemberwiseClone();
        clone.products = (products ?? new List<AnimalProductProgressState>())
            .ConvertAll(item => item?.Clone());
        return clone;
    }
}

[Serializable]
public sealed class AnimalPenPolicyData
{
    public string penId = string.Empty;
    public List<string> allowedSpeciesIds = new List<string>();
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
        clone.allowedSpeciesIds = new List<string>(
            allowedSpeciesIds ?? new List<string>());
        return clone;
    }
}

public sealed class AnimalPenCompatibilityIssue
{
    public AnimalPenCompatibilityIssue(
        AnimalPenCompatibilityIssueKind kind,
        float severity,
        string message)
    {
        Kind = kind;
        Severity = Mathf.Clamp01(severity);
        Message = message ?? string.Empty;
    }

    public AnimalPenCompatibilityIssueKind Kind { get; }
    public float Severity { get; }
    public string Message { get; }
}

public sealed class AnimalPenCompatibilityResult
{
    public string PenId { get; set; } = string.Empty;
    public float Risk { get; set; }
    public IReadOnlyList<AnimalPenCompatibilityIssue> Issues { get; set; } =
        Array.Empty<AnimalPenCompatibilityIssue>();
    public bool HasDanger => Risk >= 0.5f;
}

public readonly struct AnimalHusbandryWorkSnapshot
{
    public AnimalHusbandryWorkSnapshot(
        bool available,
        string animalId,
        AnimalHusbandryWorkKind kind,
        string displayName,
        float requiredWork,
        float completedWork,
        string unavailableReason)
    {
        Available = available;
        AnimalId = animalId ?? string.Empty;
        Kind = kind;
        DisplayName = displayName ?? string.Empty;
        RequiredWork = Mathf.Max(1f, requiredWork);
        CompletedWork = Mathf.Clamp(completedWork, 0f, RequiredWork);
        UnavailableReason = unavailableReason ?? string.Empty;
    }

    public bool Available { get; }
    public string AnimalId { get; }
    public AnimalHusbandryWorkKind Kind { get; }
    public string DisplayName { get; }
    public float RequiredWork { get; }
    public float CompletedWork { get; }
    public string UnavailableReason { get; }
}

[Serializable]
public sealed class DungeonAnimalHusbandrySaveData
{
    public const int CurrentVersion = 1;
    public int version = CurrentVersion;
    public List<HusbandryAnimalState> animals =
        new List<HusbandryAnimalState>();
    public List<AnimalPenPolicyData> penPolicies =
        new List<AnimalPenPolicyData>();
}

public interface IAnimalPenCompatibilityQuery
{
    AnimalPenCompatibilityResult EvaluatePen(string penId);
}

public interface IAnimalHusbandryRuntime : IAnimalPenCompatibilityQuery
{
    IReadOnlyList<HusbandryAnimalState> Animals { get; }
    IReadOnlyList<AnimalPenPolicyData> PenPolicies { get; }
    bool TryGetAnimal(string wildlifeId, out HusbandryAnimalState state);
    AnimalPenPolicyData GetOrCreatePenPolicy(string penId);
    int GetEffectivePenCapacity(string penId);
    bool SetPenPolicy(AnimalPenPolicyData policy, out string failureReason);
    bool DesignateSlaughter(
        string wildlifeId,
        bool designated,
        out string failureReason);
    bool TryGetWork(
        BuildableObject pen,
        CharacterActor worker,
        out AnimalHusbandryWorkSnapshot work);
    bool ApplyWork(
        BuildableObject pen,
        CharacterActor worker,
        string wildlifeId,
        AnimalHusbandryWorkKind kind,
        float amount,
        out bool completed);
    DungeonAnimalHusbandrySaveData Capture();
    void Restore(DungeonAnimalHusbandrySaveData saveData);
}
