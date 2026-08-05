using System.Collections.Generic;

public interface IAnimalPenCompatibilityQuery
{
    AnimalPenCompatibilityResult EvaluatePen(BuildingInstanceId penId);
}

public interface IAnimalHusbandryQuery : IAnimalPenCompatibilityQuery
{
    IReadOnlyList<HusbandryAnimalState> Animals { get; }
    IReadOnlyList<AnimalPenPolicyData> PenPolicies { get; }
    bool TryGetAnimal(WildlifeInstanceId animalId, out HusbandryAnimalState state);
    AnimalPenPolicyData GetPenPolicy(BuildingInstanceId penId);
    int GetEffectivePenCapacity(BuildingInstanceId penId);
    bool TryGetWork(
        BuildableObject pen,
        CharacterActor worker,
        out AnimalHusbandryWorkSnapshot work);
}

public interface IAnimalHusbandryCommand
{
    bool SetPenPolicy(
        AnimalPenPolicyData policy,
        out AnimalHusbandryFailure failure);
    bool DesignateSlaughter(
        WildlifeInstanceId animalId,
        bool designated,
        out AnimalHusbandryFailure failure);
    bool ApplyWork(
        BuildableObject pen,
        CharacterActor worker,
        WildlifeInstanceId animalId,
        AnimalHusbandryWorkKind kind,
        float amount,
        out bool completed);
}

public interface IAnimalHusbandryPersistence
{
    DungeonAnimalHusbandrySaveData Capture();
    AnimalHusbandryRestoreCandidate BuildRestore(
        DungeonAnimalHusbandrySaveData saveData);
    void Restore(AnimalHusbandryRestoreCandidate candidate);
}
