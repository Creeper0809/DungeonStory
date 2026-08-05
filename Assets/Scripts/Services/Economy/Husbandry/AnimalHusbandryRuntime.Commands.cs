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
                out AnimalPenPolicyData normalized,
                out failure))
        {
            return false;
        }

        state.StorePolicy(normalized);
        state.RefreshAutoSlaughterDesignations();
        return true;
    }

    internal bool DesignateSlaughter(
        WildlifeInstanceId animalId,
        bool designated,
        out AnimalHusbandryFailure failure)
    {
        if (!animalId.IsValid
            || !state.TryGetMutableAnimal(
                animalId,
                out HusbandryAnimalState animal))
        {
            failure = new AnimalHusbandryFailure(
                AnimalHusbandryFailureCode.AnimalNotFound,
                animalId.Value);
            return false;
        }

        return AnimalHusbandryCommandPolicy.TryApplySlaughterDesignation(
            animal,
            state.GetMutablePolicy(animal.PenId),
            designated,
            out failure);
    }

    private bool IsKnownSpecies(WildlifeSpeciesId speciesId) =>
        speciesId.IsValid
        && speciesCatalog.TryGetSpecies(speciesId.Value, out _);
}
