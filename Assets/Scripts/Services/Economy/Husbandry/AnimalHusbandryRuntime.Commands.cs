using System;

internal sealed class AnimalHusbandryCommandService
{
    private readonly IAnimalHusbandryCommandState state;
    private readonly IWildlifeCaptureRuntime captureRuntime;
    private readonly IWildlifeSpeciesCatalogProvider speciesCatalog;

    internal AnimalHusbandryCommandService(
        IAnimalHusbandryCommandState state,
        IWildlifeCaptureRuntime captureRuntime,
        IWildlifeSpeciesCatalogProvider speciesCatalog)
    {
        this.state = state ?? throw new ArgumentNullException(nameof(state));
        this.captureRuntime = captureRuntime
            ?? throw new ArgumentNullException(nameof(captureRuntime));
        this.speciesCatalog = speciesCatalog
            ?? throw new ArgumentNullException(nameof(speciesCatalog));
    }

    internal bool SetPenPolicy(
        AnimalPenPolicyData policy,
        out AnimalHusbandryFailure failure)
    {
        if (!TryNormalizePenPolicy(policy, out AnimalPenPolicyData normalized, out failure))
        {
            return false;
        }

        ApplyPenPolicy(normalized);
        return true;
    }

    internal bool TryNormalizePenPolicy(
        AnimalPenPolicyData policy,
        out AnimalPenPolicyData normalized,
        out AnimalHusbandryFailure failure)
    {
        BuildingInstanceId penId = policy?.PenId ?? default;
        int? physicalCapacity = penId.IsValid
            && captureRuntime.TryGetPenCapacity(
                penId.Value,
                out int capacity)
            ? capacity
            : null;
        if (!AnimalHusbandryCommandPolicy.TryNormalizePolicy(
                policy,
                physicalCapacity,
                IsKnownSpecies,
                out normalized,
                out failure))
        {
            return false;
        }
        return true;
    }

    internal void ApplyPenPolicy(AnimalPenPolicyData normalized)
    {
        state.StorePolicy(normalized
            ?? throw new ArgumentNullException(nameof(normalized)));
        state.RefreshAutoSlaughterDesignations();
    }

    internal bool DesignateSlaughter(
        WildlifeInstanceId animalId,
        bool designated,
        out AnimalHusbandryFailure failure)
    {
        if (!TryGetSlaughterDesignationTarget(
                animalId,
                designated,
                out HusbandryAnimalState animal,
                out failure))
        {
            return false;
        }

        ApplySlaughterDesignation(animal, designated);
        return true;
    }

    internal bool TryGetSlaughterDesignationTarget(
        WildlifeInstanceId animalId,
        bool designated,
        out HusbandryAnimalState animal,
        out AnimalHusbandryFailure failure)
    {
        animal = null;
        if (!animalId.IsValid
            || !state.TryGetMutableAnimal(animalId, out animal))
        {
            failure = new AnimalHusbandryFailure(
                AnimalHusbandryFailureCode.AnimalNotFound,
                animalId.Value);
            return false;
        }
        if (designated
            && animal.Pregnant
            && state.GetMutablePolicy(animal.PenId)?.protectPregnant == true)
        {
            failure = new AnimalHusbandryFailure(
                AnimalHusbandryFailureCode.PregnantAnimalProtected,
                animal.AnimalId.Value,
                animal.PenId.Value);
            return false;
        }
        failure = AnimalHusbandryFailure.None;
        return true;
    }

    internal static void ApplySlaughterDesignation(
        HusbandryAnimalState animal,
        bool designated)
    {
        if (!AnimalHusbandryCommandPolicy.TryApplySlaughterDesignation(
            animal,
            policy: null,
            designated: designated,
            out AnimalHusbandryFailure failure))
        {
            throw new InvalidOperationException(
                "Validated slaughter designation could not be applied: "
                + failure.Code);
        }
    }

    private bool IsKnownSpecies(WildlifeSpeciesId speciesId) =>
        speciesId.IsValid
        && speciesCatalog.TryGetSpecies(speciesId.Value, out _);
}
