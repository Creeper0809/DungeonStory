using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

public static class AnimalHusbandryDomainOperationIdentity
{
    private const string Prefix = "animal-husbandry:";

    public static string Format(
        AnimalHusbandryOutcomeOperationKind kind,
        string subjectId,
        long sequence)
    {
        if (sequence <= 0L || sequence >= long.MaxValue)
            throw new ArgumentOutOfRangeException(nameof(sequence));
        if (string.IsNullOrWhiteSpace(subjectId)
            || !string.Equals(subjectId, subjectId.Trim(), StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical husbandry operation subject is required.",
                nameof(subjectId));
        }
        return Prefix
            + sequence.ToString("D12", CultureInfo.InvariantCulture)
            + ":" + StableKind(kind)
            + ":" + subjectId;
    }

    public static string StableKind(
        AnimalHusbandryOutcomeOperationKind kind) => kind switch
    {
        AnimalHusbandryOutcomeOperationKind.SetPenPolicy => "set-pen-policy",
        AnimalHusbandryOutcomeOperationKind.DesignateSlaughter =>
            "designate-slaughter",
        AnimalHusbandryOutcomeOperationKind.Tame => "tame",
        AnimalHusbandryOutcomeOperationKind.CollectProduct => "collect-product",
        AnimalHusbandryOutcomeOperationKind.CollectManure => "collect-manure",
        AnimalHusbandryOutcomeOperationKind.Slaughter => "slaughter",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
    };

    internal static long RequireSequence(
        string operationId,
        AnimalHusbandryOutcomeOperationKind kind,
        string subjectId)
    {
        if (string.IsNullOrWhiteSpace(operationId)
            || !operationId.StartsWith(Prefix, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical husbandry operation identity is required.",
                nameof(operationId));
        }

        int sequenceEnd = operationId.IndexOf(':', Prefix.Length);
        int kindEnd = sequenceEnd < 0
            ? -1
            : operationId.IndexOf(':', sequenceEnd + 1);
        if (sequenceEnd < 0
            || kindEnd < 0
            || !long.TryParse(
                operationId.Substring(
                    Prefix.Length,
                    sequenceEnd - Prefix.Length),
                NumberStyles.None,
                CultureInfo.InvariantCulture,
                out long sequence)
            || sequence <= 0L
            || sequence >= long.MaxValue
            || !string.Equals(
                operationId,
                Format(kind, subjectId, sequence),
                StringComparison.Ordinal))
        {
            throw new ArgumentException(
                "A canonical husbandry operation identity is required.",
                nameof(operationId));
        }
        return sequence;
    }
}

public sealed class AnimalHusbandryCommandFact
{
    private readonly AnimalPenPolicyData beforePolicy;
    private readonly AnimalPenPolicyData afterPolicy;
    private readonly HusbandryAnimalState beforeAnimal;
    private readonly HusbandryAnimalState afterAnimal;

    public AnimalHusbandryCommandFact(
        string operationId,
        int absoluteDay,
        AnimalPenPolicyData before,
        AnimalPenPolicyData after)
    {
        if (absoluteDay < 1)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        ValidatePolicy(before, nameof(before));
        ValidatePolicy(after, nameof(after));
        if (!before.PenId.Equals(after.PenId) || PoliciesEqual(before, after))
        {
            throw new ArgumentException(
                "A policy command requires matching pen identities and a real transition.");
        }

        Kind = AnimalHusbandryOutcomeOperationKind.SetPenPolicy;
        PenId = before.PenId;
        AnimalId = default;
        SpeciesId = default;
        OperationId = operationId;
        AbsoluteDay = absoluteDay;
        Sequence = AnimalHusbandryDomainOperationIdentity.RequireSequence(
            operationId,
            Kind,
            PenId.Value);
        beforePolicy = before.Clone();
        afterPolicy = after.Clone();
    }

    public AnimalHusbandryCommandFact(
        string operationId,
        int absoluteDay,
        HusbandryAnimalState before,
        HusbandryAnimalState after)
    {
        if (absoluteDay < 1)
            throw new ArgumentOutOfRangeException(nameof(absoluteDay));
        ValidateAnimal(before, nameof(before));
        ValidateAnimal(after, nameof(after));
        if (!before.AnimalId.Equals(after.AnimalId)
            || !before.SpeciesId.Equals(after.SpeciesId)
            || !before.PenId.Equals(after.PenId))
        {
            throw new ArgumentException(
                "A designation command cannot change animal identity.");
        }

        HusbandryAnimalState expected = before.Clone();
        if (!AnimalHusbandryCommandPolicy.TryApplySlaughterDesignation(
                expected,
                policy: null,
                designated: after.SlaughterDesignated,
                out _)
            || !AnimalsEqual(expected, after)
            || before.SlaughterDesignated == after.SlaughterDesignated
                && before.AutoSlaughterDesignated
                    == after.AutoSlaughterDesignated)
        {
            throw new ArgumentException(
                "The animal states are not a canonical slaughter-designation transition.");
        }

        Kind = AnimalHusbandryOutcomeOperationKind.DesignateSlaughter;
        PenId = before.PenId;
        AnimalId = before.AnimalId;
        SpeciesId = before.SpeciesId;
        OperationId = operationId;
        AbsoluteDay = absoluteDay;
        Sequence = AnimalHusbandryDomainOperationIdentity.RequireSequence(
            operationId,
            Kind,
            AnimalId.Value);
        beforeAnimal = before.Clone();
        afterAnimal = after.Clone();
    }

    public AnimalHusbandryOutcomeOperationKind Kind { get; }
    public BuildingInstanceId PenId { get; }
    public WildlifeInstanceId AnimalId { get; }
    public WildlifeSpeciesId SpeciesId { get; }
    public string OperationId { get; }
    public int AbsoluteDay { get; }
    public long Sequence { get; }

    public AnimalPenPolicyData CaptureBeforePolicy() =>
        beforePolicy?.Clone();

    public AnimalPenPolicyData CaptureAfterPolicy() =>
        afterPolicy?.Clone();

    public HusbandryAnimalState CaptureBeforeAnimal() =>
        beforeAnimal?.Clone();

    public HusbandryAnimalState CaptureAfterAnimal() =>
        afterAnimal?.Clone();

    public AnimalHusbandryCommandFactSaveData CaptureSaveData()
    {
        var policies = new List<AnimalPenPolicySaveData>();
        var animals = new List<HusbandryAnimalSaveData>();
        if (Kind == AnimalHusbandryOutcomeOperationKind.SetPenPolicy)
        {
            policies.Add(AnimalHusbandryStateCodec.CapturePolicy(beforePolicy));
            policies.Add(AnimalHusbandryStateCodec.CapturePolicy(afterPolicy));
        }
        else
        {
            animals.Add(AnimalHusbandryStateCodec.CaptureAnimal(beforeAnimal));
            animals.Add(AnimalHusbandryStateCodec.CaptureAnimal(afterAnimal));
        }
        return new AnimalHusbandryCommandFactSaveData
        {
            kind = Kind,
            operationId = OperationId,
            absoluteDay = AbsoluteDay,
            policyStates = policies,
            animalStates = animals
        };
    }

    public static AnimalHusbandryCommandFact Restore(
        AnimalHusbandryCommandFactSaveData data,
        Func<WildlifeSpeciesId, IReadOnlyCollection<ItemDefinitionId>>
            getSpeciesProducts,
        Func<ItemDefinitionId, bool> containsItem)
    {
        if (data == null)
            throw new ArgumentNullException(nameof(data));
        if (getSpeciesProducts == null)
            throw new ArgumentNullException(nameof(getSpeciesProducts));
        if (containsItem == null)
            throw new ArgumentNullException(nameof(containsItem));
        if (data.policyStates == null || data.animalStates == null)
        {
            throw new InvalidOperationException(
                "Husbandry command fact collections must not be null.");
        }

        return data.kind switch
        {
            AnimalHusbandryOutcomeOperationKind.SetPenPolicy
                when data.policyStates.Count == 2
                    && data.animalStates.Count == 0 =>
                new AnimalHusbandryCommandFact(
                    data.operationId,
                    data.absoluteDay,
                    AnimalHusbandryStateCodec.BuildPolicy(
                        data.policyStates[0],
                        getSpeciesProducts),
                    AnimalHusbandryStateCodec.BuildPolicy(
                        data.policyStates[1],
                        getSpeciesProducts)),
            AnimalHusbandryOutcomeOperationKind.DesignateSlaughter
                when data.policyStates.Count == 0
                    && data.animalStates.Count == 2 =>
                new AnimalHusbandryCommandFact(
                    data.operationId,
                    data.absoluteDay,
                    AnimalHusbandryStateCodec.BuildAnimal(
                        data.animalStates[0],
                        getSpeciesProducts,
                        containsItem),
                    AnimalHusbandryStateCodec.BuildAnimal(
                        data.animalStates[1],
                        getSpeciesProducts,
                        containsItem)),
            _ => throw new InvalidOperationException(
                "Husbandry command fact shape or kind is invalid.")
        };
    }

    private static void ValidatePolicy(
        AnimalPenPolicyData value,
        string parameterName)
    {
        if (value == null)
            throw new ArgumentNullException(parameterName);
        IReadOnlyList<WildlifeSpeciesId> species = value.AllowedSpeciesIds;
        if (!value.PenId.IsValid
            || species == null
            || species.Any(id => !id.IsValid)
            || !species.SequenceEqual(
                species.OrderBy(id => id.Value, StringComparer.Ordinal))
            || species.Distinct().Count() != species.Count
            || value.maximumAnimals < 1
            || value.adultFemaleLimit < 0
            || value.adultMaleLimit < 0
            || value.juvenileLimit < 0
            || value.minimumBreedingFemales < 0
            || value.minimumBreedingMales < 0)
        {
            throw new ArgumentException(
                "A canonical husbandry pen policy is required.",
                parameterName);
        }
    }

    private static void ValidateAnimal(
        HusbandryAnimalState value,
        string parameterName)
    {
        if (value == null)
            throw new ArgumentNullException(parameterName);
        if (!value.AnimalId.IsValid
            || !value.SpeciesId.IsValid
            || !value.PenId.IsValid
            || !Enum.IsDefined(typeof(AnimalSex), value.Sex)
            || !Enum.IsDefined(
                typeof(AnimalHusbandryWorkKind),
                value.PendingWorkKind)
            || !Enum.IsDefined(
                typeof(AnimalHusbandryStatusCode),
                value.StatusCode)
            || !FiniteNonNegative(value.AgeDays)
            || !float.IsFinite(value.TamingProgress)
            || value.TamingProgress is < 0f or > 1f
            || !FiniteNonNegative(value.PregnancyProgressDays)
            || !FiniteNonNegative(value.BreedingCooldownDays)
            || !FiniteNonNegative(value.ManureProgressDays)
            || !FiniteNonNegative(value.PendingWorkCompleted)
            || value.ReadyManureCycles < 0
            || value.StatusParameters == null
            || value.StatusParameters.Any(item => item == null)
            || value.Products == null
            || value.Products.Any(item => item == null
                || !item.ItemId.IsValid
                || !FiniteNonNegative(item.ProgressDays)
                || item.ReadyCycles < 0)
            || value.PendingWorkContributions == null
            || value.PendingWorkContributions.Any(item => item == null
                || string.IsNullOrWhiteSpace(item.characterId)
                || !string.Equals(
                    item.characterId,
                    item.characterId.Trim(),
                    StringComparison.Ordinal)
                || !float.IsFinite(item.contributedWork)
                || item.contributedWork <= 0f
                || !float.IsFinite(item.relevantSkill)
                || item.relevantSkill < 0f))
        {
            throw new ArgumentException(
                "A canonical husbandry animal state is required.",
                parameterName);
        }
    }

    private static bool FiniteNonNegative(float value) =>
        float.IsFinite(value) && value >= 0f;

    private static bool PoliciesEqual(
        AnimalPenPolicyData left,
        AnimalPenPolicyData right) =>
        left.PenId.Equals(right.PenId)
        && left.AllowedSpeciesIds.SequenceEqual(right.AllowedSpeciesIds)
        && left.allowHerbivores == right.allowHerbivores
        && left.allowOmnivores == right.allowOmnivores
        && left.allowCarnivores == right.allowCarnivores
        && left.allowScavengers == right.allowScavengers
        && left.allowFemales == right.allowFemales
        && left.allowMales == right.allowMales
        && left.allowJuveniles == right.allowJuveniles
        && left.maximumAnimals == right.maximumAnimals
        && left.breedingAllowed == right.breedingAllowed
        && left.protectPregnant == right.protectPregnant
        && left.allowRiskyMixing == right.allowRiskyMixing
        && left.adultFemaleLimit == right.adultFemaleLimit
        && left.adultMaleLimit == right.adultMaleLimit
        && left.juvenileLimit == right.juvenileLimit
        && left.minimumBreedingFemales == right.minimumBreedingFemales
        && left.minimumBreedingMales == right.minimumBreedingMales;

    private static bool AnimalsEqual(
        HusbandryAnimalState left,
        HusbandryAnimalState right) =>
        left.AnimalId.Equals(right.AnimalId)
        && left.SpeciesId.Equals(right.SpeciesId)
        && left.PenId.Equals(right.PenId)
        && left.Sex == right.Sex
        && left.AgeDays == right.AgeDays
        && left.Tamed == right.Tamed
        && left.TamingProgress == right.TamingProgress
        && left.Pregnant == right.Pregnant
        && left.PregnancyProgressDays == right.PregnancyProgressDays
        && left.OtherParentId.Equals(right.OtherParentId)
        && left.BreedingCooldownDays == right.BreedingCooldownDays
        && left.ManureProgressDays == right.ManureProgressDays
        && left.ReadyManureCycles == right.ReadyManureCycles
        && left.SlaughterDesignated == right.SlaughterDesignated
        && left.AutoSlaughterDesignated == right.AutoSlaughterDesignated
        && left.PendingWorkKind == right.PendingWorkKind
        && left.PendingProductItemId.Equals(right.PendingProductItemId)
        && left.PendingWorkCompleted == right.PendingWorkCompleted
        && ContributionsEqual(
            left.PendingWorkContributions,
            right.PendingWorkContributions)
        && left.StatusCode == right.StatusCode
        && left.StatusParameters.SequenceEqual(right.StatusParameters)
        && ProductsEqual(left.Products, right.Products);

    private static bool ContributionsEqual(
        IReadOnlyList<CraftContributionSaveData> left,
        IReadOnlyList<CraftContributionSaveData> right) =>
        left.Count == right.Count
        && left.Select((value, index) => new { value, index }).All(pair =>
            string.Equals(
                pair.value.characterId,
                right[pair.index].characterId,
                StringComparison.Ordinal)
            && pair.value.contributedWork
                == right[pair.index].contributedWork
            && pair.value.relevantSkill == right[pair.index].relevantSkill);

    private static bool ProductsEqual(
        IReadOnlyList<AnimalProductProgressState> left,
        IReadOnlyList<AnimalProductProgressState> right) =>
        left.Count == right.Count
        && left.Select((value, index) => new { value, index }).All(pair =>
            pair.value.ItemId.Equals(right[pair.index].ItemId)
            && pair.value.ProgressDays == right[pair.index].ProgressDays
            && pair.value.ReadyCycles == right[pair.index].ReadyCycles);
}

[Serializable]
public sealed class AnimalHusbandryCommandFactSaveData
{
    public AnimalHusbandryOutcomeOperationKind kind;
    public string operationId = string.Empty;
    public int absoluteDay;
    public List<AnimalPenPolicySaveData> policyStates = new();
    public List<HusbandryAnimalSaveData> animalStates = new();
}
