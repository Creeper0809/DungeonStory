using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AnimalHusbandryAggregateState
{
    public Dictionary<WildlifeInstanceId, HusbandryAnimalState> Animals { get; } =
        new();
    public Dictionary<BuildingInstanceId, AnimalPenPolicyData> Policies { get; } =
        new();
    public float NextTickAt { get; set; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public sealed class AnimalHusbandryRestoreCandidate
{
    public AnimalHusbandryRestoreCandidate(AnimalHusbandryAggregateState state)
    {
        State = state ?? throw new ArgumentNullException(nameof(state));
    }

    public AnimalHusbandryAggregateState State { get; }
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class AnimalHusbandryStateCodec
{
    public static AnimalHusbandryRestoreCandidate BuildRestore(
        DungeonAnimalHusbandrySaveData saveData,
        float nextTickAt,
        Func<WildlifeSpeciesId, IReadOnlyCollection<ItemDefinitionId>> getSpeciesProducts,
        Func<ItemDefinitionId, bool> containsItem)
    {
        if (saveData == null)
        {
            throw new InvalidOperationException("Animal-husbandry payload is null.");
        }
        if (saveData.version != DungeonAnimalHusbandrySaveData.CurrentVersion)
        {
            throw new InvalidOperationException(
                $"Animal-husbandry payload version {saveData.version} is unsupported.");
        }
        if (saveData.animals == null || saveData.penPolicies == null)
        {
            throw new InvalidOperationException(
                "Animal-husbandry payload collections must not be null.");
        }
        if (getSpeciesProducts == null || containsItem == null)
        {
            throw new ArgumentNullException(
                getSpeciesProducts == null ? nameof(getSpeciesProducts) : nameof(containsItem));
        }

        AnimalHusbandryAggregateState restored = new()
        {
            NextTickAt = nextTickAt
        };
        WildlifeInstanceId previousAnimalId = default;
        foreach (HusbandryAnimalSaveData saved in saveData.animals)
        {
            HusbandryAnimalState state = BuildAnimal(saved, getSpeciesProducts, containsItem);
            if (previousAnimalId.IsValid
                && string.CompareOrdinal(previousAnimalId.Value, state.AnimalId.Value) >= 0)
            {
                throw new InvalidOperationException(
                    "Animal-husbandry animals must be uniquely sorted by canonical instance ID.");
            }
            if (!restored.Animals.TryAdd(state.AnimalId, state))
            {
                throw new InvalidOperationException(
                    $"Duplicate husbandry animal '{state.AnimalId.Value}'.");
            }
            previousAnimalId = state.AnimalId;
        }

        BuildingInstanceId previousPenId = default;
        foreach (AnimalPenPolicySaveData saved in saveData.penPolicies)
        {
            AnimalPenPolicyData policy = BuildPolicy(saved, getSpeciesProducts);
            if (previousPenId.IsValid
                && string.CompareOrdinal(previousPenId.Value, policy.PenId.Value) >= 0)
            {
                throw new InvalidOperationException(
                    "Animal-husbandry pen policies must be uniquely sorted by canonical building ID.");
            }
            if (!restored.Policies.TryAdd(policy.PenId, policy))
            {
                throw new InvalidOperationException(
                    $"Duplicate husbandry pen policy '{policy.PenId.Value}'.");
            }
            previousPenId = policy.PenId;
        }

        ValidateAggregateReferences(restored);
        return new AnimalHusbandryRestoreCandidate(restored);
    }

    public static DungeonAnimalHusbandrySaveData Capture(
        AnimalHusbandryAggregateState state)
    {
        if (state == null)
        {
            throw new ArgumentNullException(nameof(state));
        }

        return new DungeonAnimalHusbandrySaveData
        {
            animals = state.Animals.Values
                .OrderBy(value => value.AnimalId.Value, StringComparer.Ordinal)
                .Select(CaptureAnimal)
                .ToList(),
            penPolicies = state.Policies.Values
                .OrderBy(value => value.PenId.Value, StringComparer.Ordinal)
                .Select(CapturePolicy)
                .ToList()
        };
    }

    private static HusbandryAnimalState BuildAnimal(
        HusbandryAnimalSaveData saved,
        Func<WildlifeSpeciesId, IReadOnlyCollection<ItemDefinitionId>> getSpeciesProducts,
        Func<ItemDefinitionId, bool> containsItem)
    {
        if (saved == null)
        {
            throw new InvalidOperationException(
                "Animal-husbandry payload contains a null animal.");
        }

        WildlifeInstanceId animalId = new(saved.animalInstanceId);
        WildlifeSpeciesId speciesId = new(saved.speciesDefinitionId);
        BuildingInstanceId penId = new(saved.penBuildingInstanceId);
        RequireCanonical(animalId.Value, saved.animalInstanceId, animalId.IsValid, "animal instance");
        RequireCanonical(speciesId.Value, saved.speciesDefinitionId, speciesId.IsValid, "species definition");
        RequireCanonical(penId.Value, saved.penBuildingInstanceId, penId.IsValid, "pen building instance");
        IReadOnlyCollection<ItemDefinitionId> authoredProducts =
            getSpeciesProducts(speciesId);
        if (authoredProducts == null)
        {
            throw new InvalidOperationException(
                $"Unknown authored wildlife species '{speciesId.Value}'.");
        }
        if (!Enum.IsDefined(typeof(AnimalSex), saved.sex)
            || !Enum.IsDefined(typeof(AnimalHusbandryWorkKind), saved.pendingWorkKind)
            || !Enum.IsDefined(typeof(AnimalHusbandryStatusCode), saved.statusCode))
        {
            throw new InvalidOperationException(
                $"Animal '{animalId.Value}' contains an unknown enum value.");
        }
        RequireFiniteNonNegative(saved.ageDays, animalId, "age");
        RequireFiniteRange(saved.tamingProgress, 0f, 1f, animalId, "taming progress");
        RequireFiniteNonNegative(saved.pregnancyProgressDays, animalId, "pregnancy progress");
        RequireFiniteNonNegative(saved.breedingCooldownDays, animalId, "breeding cooldown");
        RequireFiniteNonNegative(saved.manureProgressDays, animalId, "manure progress");
        RequireFiniteNonNegative(saved.pendingWorkCompleted, animalId, "pending work");
        if (saved.readyManureCycles < 0
            || saved.products == null
            || saved.statusParameters == null
            || saved.statusParameters.Any(parameter => parameter == null))
        {
            throw new InvalidOperationException(
                $"Animal '{animalId.Value}' contains invalid progress or status data.");
        }

        WildlifeInstanceId otherParentId = new(saved.otherParentAnimalInstanceId);
        if (otherParentId.Value.Length > 0)
        {
            RequireCanonical(
                otherParentId.Value,
                saved.otherParentAnimalInstanceId,
                otherParentId.IsValid,
                "other-parent animal instance");
        }

        ItemDefinitionId pendingProductId = new(saved.pendingProductItemDefinitionId);
        if (pendingProductId.IsValid && !containsItem(pendingProductId))
        {
            throw new InvalidOperationException(
                $"Unknown authored item '{pendingProductId.Value}'.");
        }
        if (saved.pendingWorkKind == AnimalHusbandryWorkKind.CollectProduct
            != pendingProductId.IsValid)
        {
            throw new InvalidOperationException(
                $"Animal '{animalId.Value}' has inconsistent pending product work.");
        }

        HashSet<ItemDefinitionId> productIds = new();
        List<AnimalProductProgressState> products = new(saved.products.Count);
        string previousProductId = null;
        foreach (AnimalProductProgressSaveData product in saved.products)
        {
            if (product == null)
            {
                throw new InvalidOperationException(
                    $"Animal '{animalId.Value}' contains a null product progress record.");
            }
            ItemDefinitionId itemId = new(product.itemDefinitionId);
            RequireCanonical(itemId.Value, product.itemDefinitionId, itemId.IsValid, "product item definition");
            if (!containsItem(itemId))
            {
                throw new InvalidOperationException(
                    $"Unknown authored item '{itemId.Value}'.");
            }
            if (!authoredProducts.Contains(itemId))
            {
                throw new InvalidOperationException(
                    $"Item '{itemId.Value}' is not an authored husbandry product of species '{speciesId.Value}'.");
            }
            if (previousProductId != null
                && string.CompareOrdinal(previousProductId, itemId.Value) >= 0
                || !productIds.Add(itemId))
            {
                throw new InvalidOperationException(
                    $"Animal '{animalId.Value}' product IDs must be unique and sorted.");
            }
            RequireFiniteNonNegative(product.progressDays, animalId, "product progress");
            if (product.readyCycles < 0)
            {
                throw new InvalidOperationException(
                    $"Animal '{animalId.Value}' has a negative ready product count.");
            }
            products.Add(new AnimalProductProgressState
            {
                ItemId = itemId,
                ProgressDays = product.progressDays,
                ReadyCycles = product.readyCycles
            });
            previousProductId = itemId.Value;
        }

        if (pendingProductId.IsValid && !productIds.Contains(pendingProductId))
        {
            throw new InvalidOperationException(
                $"Animal '{animalId.Value}' pending product is absent from its product state.");
        }

        return new HusbandryAnimalState
        {
            AnimalId = animalId,
            SpeciesId = speciesId,
            PenId = penId,
            Sex = saved.sex,
            AgeDays = saved.ageDays,
            Tamed = saved.tamed,
            TamingProgress = saved.tamingProgress,
            Pregnant = saved.pregnant,
            PregnancyProgressDays = saved.pregnancyProgressDays,
            OtherParentId = otherParentId,
            BreedingCooldownDays = saved.breedingCooldownDays,
            ManureProgressDays = saved.manureProgressDays,
            ReadyManureCycles = saved.readyManureCycles,
            SlaughterDesignated = saved.slaughterDesignated,
            AutoSlaughterDesignated = saved.autoSlaughterDesignated,
            PendingWorkKind = saved.pendingWorkKind,
            PendingProductItemId = pendingProductId,
            PendingWorkCompleted = saved.pendingWorkCompleted,
            StatusCode = saved.statusCode,
            StatusParameters = new List<string>(saved.statusParameters),
            Products = products
        };
    }

    private static AnimalPenPolicyData BuildPolicy(
        AnimalPenPolicySaveData saved,
        Func<WildlifeSpeciesId, IReadOnlyCollection<ItemDefinitionId>> getSpeciesProducts)
    {
        if (saved == null || saved.allowedSpeciesDefinitionIds == null)
        {
            throw new InvalidOperationException(
                "Animal-husbandry payload contains a null pen policy or species list.");
        }
        BuildingInstanceId penId = new(saved.penBuildingInstanceId);
        RequireCanonical(penId.Value, saved.penBuildingInstanceId, penId.IsValid, "pen building instance");
        if (saved.maximumAnimals < 1
            || saved.adultFemaleLimit < 0
            || saved.adultMaleLimit < 0
            || saved.juvenileLimit < 0
            || saved.minimumBreedingFemales < 0
            || saved.minimumBreedingMales < 0)
        {
            throw new InvalidOperationException(
                $"Pen '{penId.Value}' contains invalid population limits.");
        }

        List<WildlifeSpeciesId> allowedSpecies = new(saved.allowedSpeciesDefinitionIds.Count);
        string previousSpeciesId = null;
        foreach (string rawSpeciesId in saved.allowedSpeciesDefinitionIds)
        {
            WildlifeSpeciesId speciesId = new(rawSpeciesId);
            RequireCanonical(speciesId.Value, rawSpeciesId, speciesId.IsValid, "allowed species definition");
            if (getSpeciesProducts(speciesId) == null)
            {
                throw new InvalidOperationException(
                    $"Unknown authored wildlife species '{speciesId.Value}'.");
            }
            if (previousSpeciesId != null
                && string.CompareOrdinal(previousSpeciesId, speciesId.Value) >= 0)
            {
                throw new InvalidOperationException(
                    $"Pen '{penId.Value}' allowed species must be unique and sorted.");
            }
            allowedSpecies.Add(speciesId);
            previousSpeciesId = speciesId.Value;
        }

        return new AnimalPenPolicyData
        {
            PenId = penId,
            AllowedSpeciesIds = allowedSpecies,
            allowHerbivores = saved.allowHerbivores,
            allowOmnivores = saved.allowOmnivores,
            allowCarnivores = saved.allowCarnivores,
            allowScavengers = saved.allowScavengers,
            allowFemales = saved.allowFemales,
            allowMales = saved.allowMales,
            allowJuveniles = saved.allowJuveniles,
            maximumAnimals = saved.maximumAnimals,
            breedingAllowed = saved.breedingAllowed,
            protectPregnant = saved.protectPregnant,
            allowRiskyMixing = saved.allowRiskyMixing,
            adultFemaleLimit = saved.adultFemaleLimit,
            adultMaleLimit = saved.adultMaleLimit,
            juvenileLimit = saved.juvenileLimit,
            minimumBreedingFemales = saved.minimumBreedingFemales,
            minimumBreedingMales = saved.minimumBreedingMales
        };
    }

    private static void ValidateAggregateReferences(
        AnimalHusbandryAggregateState state)
    {
        foreach (HusbandryAnimalState animal in state.Animals.Values)
        {
            if (!state.Policies.ContainsKey(animal.PenId))
            {
                throw new InvalidOperationException(
                    $"Animal '{animal.AnimalId.Value}' references pen '{animal.PenId.Value}' without a saved policy.");
            }
            if (animal.OtherParentId.IsValid
                && (!state.Animals.TryGetValue(animal.OtherParentId, out HusbandryAnimalState parent)
                    || !parent.SpeciesId.Equals(animal.SpeciesId)))
            {
                throw new InvalidOperationException(
                    $"Animal '{animal.AnimalId.Value}' has an invalid other-parent reference.");
            }
        }
    }

    private static HusbandryAnimalSaveData CaptureAnimal(HusbandryAnimalState state) =>
        new()
        {
            animalInstanceId = state.AnimalId.Value,
            speciesDefinitionId = state.SpeciesId.Value,
            penBuildingInstanceId = state.PenId.Value,
            sex = state.Sex,
            ageDays = state.AgeDays,
            tamed = state.Tamed,
            tamingProgress = state.TamingProgress,
            pregnant = state.Pregnant,
            pregnancyProgressDays = state.PregnancyProgressDays,
            otherParentAnimalInstanceId = state.OtherParentId.Value,
            breedingCooldownDays = state.BreedingCooldownDays,
            manureProgressDays = state.ManureProgressDays,
            readyManureCycles = state.ReadyManureCycles,
            slaughterDesignated = state.SlaughterDesignated,
            autoSlaughterDesignated = state.AutoSlaughterDesignated,
            pendingWorkKind = state.PendingWorkKind,
            pendingProductItemDefinitionId = state.PendingProductItemId.Value,
            pendingWorkCompleted = state.PendingWorkCompleted,
            statusCode = state.StatusCode,
            statusParameters = new List<string>(
                state.StatusParameters ?? new List<string>()),
            products = (state.Products ?? new List<AnimalProductProgressState>())
                .Where(product => product != null)
                .OrderBy(product => product.ItemId.Value, StringComparer.Ordinal)
                .Select(product => new AnimalProductProgressSaveData
                {
                    itemDefinitionId = product.ItemId.Value,
                    progressDays = product.ProgressDays,
                    readyCycles = product.ReadyCycles
                })
                .ToList()
        };

    private static AnimalPenPolicySaveData CapturePolicy(AnimalPenPolicyData policy) =>
        new()
        {
            penBuildingInstanceId = policy.PenId.Value,
            allowedSpeciesDefinitionIds = (policy.AllowedSpeciesIds
                    ?? new List<WildlifeSpeciesId>())
                .Select(id => id.Value)
                .OrderBy(id => id, StringComparer.Ordinal)
                .ToList(),
            allowHerbivores = policy.allowHerbivores,
            allowOmnivores = policy.allowOmnivores,
            allowCarnivores = policy.allowCarnivores,
            allowScavengers = policy.allowScavengers,
            allowFemales = policy.allowFemales,
            allowMales = policy.allowMales,
            allowJuveniles = policy.allowJuveniles,
            maximumAnimals = policy.maximumAnimals,
            breedingAllowed = policy.breedingAllowed,
            protectPregnant = policy.protectPregnant,
            allowRiskyMixing = policy.allowRiskyMixing,
            adultFemaleLimit = policy.adultFemaleLimit,
            adultMaleLimit = policy.adultMaleLimit,
            juvenileLimit = policy.juvenileLimit,
            minimumBreedingFemales = policy.minimumBreedingFemales,
            minimumBreedingMales = policy.minimumBreedingMales
        };

    private static void RequireCanonical(
        string normalized,
        string raw,
        bool valid,
        string label)
    {
        if (!valid || !string.Equals(normalized, raw, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"Animal-husbandry payload has a non-canonical {label} ID.");
        }
    }

    private static void RequireFiniteNonNegative(
        float value,
        WildlifeInstanceId animalId,
        string label) => RequireFiniteRange(value, 0f, float.MaxValue, animalId, label);

    private static void RequireFiniteRange(
        float value,
        float minimum,
        float maximum,
        WildlifeInstanceId animalId,
        string label)
    {
        if (float.IsNaN(value)
            || float.IsInfinity(value)
            || value < minimum
            || value > maximum)
        {
            throw new InvalidOperationException(
                $"Animal '{animalId.Value}' has invalid {label}.");
        }
    }
}
