using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Scripting.APIUpdating;

public interface IAnimalHusbandryCommandState
{
    bool TryGetMutableAnimal(
        WildlifeInstanceId animalId,
        out HusbandryAnimalState state);
    AnimalPenPolicyData GetMutablePolicy(BuildingInstanceId penId);
    void StorePolicy(AnimalPenPolicyData policy);
    void RefreshAutoSlaughterDesignations();
}

[MovedFrom(true, sourceAssembly: "Assembly-CSharp")]
public static class AnimalHusbandryCommandPolicy
{
    public static bool TryNormalizePolicy(
        AnimalPenPolicyData policy,
        int? physicalCapacity,
        Func<WildlifeSpeciesId, bool> isKnownSpecies,
        out AnimalPenPolicyData normalized,
        out AnimalHusbandryFailure failure)
    {
        failure = AnimalHusbandryFailure.None;
        normalized = null;
        BuildingInstanceId penId = policy?.PenId ?? default;
        if (!penId.IsValid)
        {
            failure = new AnimalHusbandryFailure(
                AnimalHusbandryFailureCode.InvalidPenId,
                penId.Value);
            return false;
        }
        if (isKnownSpecies == null)
        {
            throw new ArgumentNullException(nameof(isKnownSpecies));
        }

        AnimalPenPolicyData candidate = policy.Clone();
        candidate.PenId = penId;
        candidate.maximumAnimals = Math.Max(1, candidate.maximumAnimals);
        if (physicalCapacity.HasValue)
        {
            candidate.maximumAnimals = Math.Min(
                candidate.maximumAnimals,
                physicalCapacity.Value);
        }
        candidate.adultFemaleLimit = Math.Max(0, candidate.adultFemaleLimit);
        candidate.adultMaleLimit = Math.Max(0, candidate.adultMaleLimit);
        candidate.juvenileLimit = Math.Max(0, candidate.juvenileLimit);
        candidate.minimumBreedingFemales = Math.Max(
            0,
            candidate.minimumBreedingFemales);
        candidate.minimumBreedingMales = Math.Max(
            0,
            candidate.minimumBreedingMales);

        IReadOnlyList<WildlifeSpeciesId> requestedSpecies =
            candidate.AllowedSpeciesIds ?? new List<WildlifeSpeciesId>();
        WildlifeSpeciesId unknownSpecies = requestedSpecies.FirstOrDefault(
            speciesId => !speciesId.IsValid || !isKnownSpecies(speciesId));
        if (unknownSpecies.Value.Length > 0
            || requestedSpecies.Any(speciesId => !speciesId.IsValid))
        {
            failure = new AnimalHusbandryFailure(
                AnimalHusbandryFailureCode.UnknownSpecies,
                unknownSpecies.Value);
            return false;
        }

        candidate.AllowedSpeciesIds = requestedSpecies
            .Distinct()
            .OrderBy(speciesId => speciesId.Value, StringComparer.Ordinal)
            .ToList();
        normalized = candidate;
        return true;
    }

    public static bool TryApplySlaughterDesignation(
        HusbandryAnimalState state,
        AnimalPenPolicyData policy,
        bool designated,
        out AnimalHusbandryFailure failure)
    {
        failure = AnimalHusbandryFailure.None;
        if (state == null)
        {
            failure = new AnimalHusbandryFailure(
                AnimalHusbandryFailureCode.AnimalNotFound);
            return false;
        }
        if (designated && state.Pregnant && policy?.protectPregnant == true)
        {
            failure = new AnimalHusbandryFailure(
                AnimalHusbandryFailureCode.PregnantAnimalProtected,
                state.AnimalId.Value,
                state.PenId.Value);
            return false;
        }

        state.SlaughterDesignated = designated;
        if (!designated)
        {
            state.AutoSlaughterDesignated = false;
        }
        AnimalHusbandryWorkRules.SetStatus(
            state,
            designated
                ? AnimalHusbandryStatusCode.SlaughterDesignated
                : AnimalHusbandryStatusCode.SlaughterDesignationCleared);
        return true;
    }
}
