using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public sealed class AnimalHusbandrySaveSection :
    IDungeonSaveSection,
    IDungeonSaveSectionPreflight,
    IDungeonStagedSaveSection,
    IDungeonRollbackFreeSaveSection
{
    public const string Id = "economy.animal-husbandry";

    private static readonly string[] Dependencies =
    {
        WildlifeSaveSection.Id,
        CircusSaveSection.Id,
        PhysicalItemsSaveSection.Id,
        ModularFacilityWorldSaveSection.Id
    };

    private readonly IAnimalHusbandryPersistence persistence;

    public AnimalHusbandrySaveSection(IAnimalHusbandryPersistence persistence)
    {
        this.persistence = persistence
            ?? throw new ArgumentNullException(nameof(persistence));
    }

    public string SectionId => Id;
    public int SectionVersion => DungeonAnimalHusbandrySaveData.CurrentVersion;
    public DungeonSaveRestorePhase RestorePhase =>
        DungeonSaveRestorePhase.LateRuntimeState;
    public IReadOnlyList<string> DependsOn => Dependencies;

    public string Capture() => JsonUtility.ToJson(persistence.Capture());

    public void ValidatePayload(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        RequireVersion(sectionVersion);
        persistence.BuildRestore(Parse(payloadJson));
    }

    public void Restore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        IDungeonSaveRestoreStage stage = StageRestore(
            payloadJson,
            sectionVersion,
            report);
        if (report.Success)
        {
            stage.Commit(report);
        }
    }

    public IDungeonSaveRestoreStage StageRestore(
        string payloadJson,
        int sectionVersion,
        DungeonGameRestoreReport report)
    {
        RequireVersion(sectionVersion);
        AnimalHusbandryRestoreCandidate candidate =
            persistence.BuildRestore(Parse(payloadJson));
        return new DungeonDelegateSaveRestoreStage(
            SectionId,
            _ => persistence.Restore(candidate));
    }

    private void RequireVersion(int sectionVersion)
    {
        if (sectionVersion != SectionVersion)
        {
            throw new InvalidOperationException(
                $"Unsupported {SectionId} section version {sectionVersion}; expected {SectionVersion}.");
        }
    }

    private DungeonAnimalHusbandrySaveData Parse(string payloadJson)
    {
        if (string.IsNullOrWhiteSpace(payloadJson))
        {
            throw new InvalidOperationException(
                $"{SectionId} payload is empty.");
        }
        try
        {
            return JsonUtility.FromJson<DungeonAnimalHusbandrySaveData>(payloadJson)
                ?? throw new InvalidOperationException(
                    $"{SectionId} payload deserialized to null.");
        }
        catch (Exception exception) when (exception is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                $"{SectionId} payload JSON is invalid: {exception.Message}",
                exception);
        }
    }
}

#if UNITY_EDITOR
public static class BatchBSpeciesHusbandryDebugScenarios
{
    [UnityEditor.MenuItem(
        "Tools/DungeonStory/Debug/Batch B Species Husbandry Scenarios")]
    public static void RunAll()
    {
        VerifySaveContracts();
        VerifySpeciesRoundTripAndInvalidIsolation();
        VerifyHusbandryRoundTripAndInvalidIsolation();
        Debug.Log("BATCH_B_SPECIES_HUSBANDRY=PASS");
    }

    private static void VerifySaveContracts()
    {
        Require(CharacterSpeciesRuntimeSaveData.CurrentVersion == 3,
            "Character-species payload must be exact V3.");
        Require(DungeonAnimalHusbandrySaveData.CurrentVersion == 2,
            "Animal-husbandry payload must be exact V2.");
        Require(typeof(IDungeonSaveSectionPreflight)
                .IsAssignableFrom(typeof(SpeciesRuntimeSaveSection))
            && typeof(IDungeonRollbackFreeSaveSection)
                .IsAssignableFrom(typeof(SpeciesRuntimeSaveSection)),
            "Character-species save section must preflight and be rollback-free.");
        Require(!typeof(IOptionalDungeonSaveSection)
                .IsAssignableFrom(typeof(SpeciesRuntimeSaveSection))
            && !typeof(IDungeonStagedOptionalSaveSection)
                .IsAssignableFrom(typeof(SpeciesRuntimeSaveSection)),
            "Character-species V2 must be a required save section.");
        Require(typeof(IDungeonSaveSectionPreflight)
                .IsAssignableFrom(typeof(AnimalHusbandrySaveSection))
            && typeof(IDungeonRollbackFreeSaveSection)
                .IsAssignableFrom(typeof(AnimalHusbandrySaveSection)),
            "Animal-husbandry save section must preflight and be rollback-free.");
        Require(typeof(ICharacterSpeciesQuery)
                .IsAssignableFrom(typeof(CharacterSpeciesRuntime))
            && typeof(ICharacterSpeciesCommand)
                .IsAssignableFrom(typeof(CharacterSpeciesRuntime))
            && typeof(ICharacterSpeciesPersistence)
                .IsAssignableFrom(typeof(CharacterSpeciesRuntime))
            && typeof(IAnimalHusbandryQuery)
                .IsAssignableFrom(typeof(AnimalHusbandryRuntime))
            && typeof(IAnimalHusbandryCommand)
                .IsAssignableFrom(typeof(AnimalHusbandryRuntime))
            && typeof(IAnimalHusbandryPersistence)
                .IsAssignableFrom(typeof(AnimalHusbandryRuntime)),
            "Species and husbandry runtimes must expose split query/command contracts.");
        // Arbitrary charge mutation is intentionally not part of the public
        // species command contract. Recharge is performed only through the
        // reserved facility-work service; the command surface exposes the two
        // typed mutations consumed by work and maintenance systems only.
        foreach (string methodName in new[] { "RepairIntegrity", "RecordCompletedWork" })
        {
            System.Reflection.MethodInfo method =
                typeof(ICharacterSpeciesCommand).GetMethod(methodName);
            Require(method != null
                    && method.GetParameters().Last().ParameterType
                        == typeof(DomainFailure).MakeByRefType()
                    && method.GetParameters().All(parameter =>
                        parameter.ParameterType
                            != typeof(string).MakeByRefType()),
                $"{methodName} must return a typed DomainFailure boundary.");
        }
    }

    private static void VerifySpeciesRoundTripAndInvalidIsolation()
    {
        CharacterSpeciesRuntimeSaveData payload = new()
        {
            characters = new List<CharacterSpeciesRuntimeRecordSaveData>
            {
                new()
                {
                    characterInstanceId = "character:fixture:1",
                    speciesDefinitionId = "Golem",
                    charge = 42f,
                    integrity = 81f,
                    nextIncidentAt = 30f,
                    lastIncidentId = CharacterSpeciesIncidentIds.GolemCoreOverload,
                    incidentCount = 2
                }
            }
        };
        CharacterSpeciesRestoreCandidate live = CharacterSpeciesStateCodec.BuildRestore(
            JsonUtility.FromJson<CharacterSpeciesRuntimeSaveData>(
                JsonUtility.ToJson(payload)),
            31f,
            ResolveSpeciesIncident);
        Require(live.State.Characters.Count == 1
            && live.State.Characters[new CharacterId("character:fixture:1")].Charge == 42f,
            "Character-species valid JSON round-trip lost state.");

        CharacterSpeciesRuntimeSaveData invalid = JsonUtility.FromJson<CharacterSpeciesRuntimeSaveData>(
            JsonUtility.ToJson(payload));
        invalid.characters[0].speciesDefinitionId = "UnknownSpecies";
        RequireThrows(() => CharacterSpeciesStateCodec.BuildRestore(
            invalid,
            99f,
            ResolveSpeciesIncident),
            "Unknown character species was accepted.");
        Require(live.State.Characters.Count == 1
            && live.State.NextTickAt == 31f,
            "Failed character-species build mutated the live candidate.");
    }

    private static void VerifyHusbandryRoundTripAndInvalidIsolation()
    {
        DungeonAnimalHusbandrySaveData payload = new()
        {
            animals = new List<HusbandryAnimalSaveData>
            {
                new()
                {
                    animalInstanceId = "wild:fixture:1",
                    speciesDefinitionId = "cave_rat",
                    penBuildingInstanceId = "building:fixture:pen",
                    sex = AnimalSex.Female,
                    ageDays = 5f,
                    tamed = true,
                    tamingProgress = 1f,
                    statusCode = AnimalHusbandryStatusCode.TamedAnimal
                }
            },
            penPolicies = new List<AnimalPenPolicySaveData>
            {
                new()
                {
                    penBuildingInstanceId = "building:fixture:pen",
                    allowedSpeciesDefinitionIds = new List<string> { "cave_rat" }
                }
            }
        };
        AnimalHusbandryRestoreCandidate live = BuildHusbandry(payload, 11f);
        DungeonAnimalHusbandrySaveData captured =
            AnimalHusbandryStateCodec.Capture(live.State);
        AnimalHusbandryRestoreCandidate roundTrip = BuildHusbandry(
            JsonUtility.FromJson<DungeonAnimalHusbandrySaveData>(
                JsonUtility.ToJson(captured)),
            12f);
        WildlifeInstanceId animalId = new("wild:fixture:1");
        Require(roundTrip.State.Animals.Count == 1
            && roundTrip.State.Animals[animalId].SpeciesId.Equals(
                new WildlifeSpeciesId("cave_rat")),
            "Animal-husbandry valid JSON round-trip lost typed state.");

        DungeonAnimalHusbandrySaveData invalid = JsonUtility.FromJson<DungeonAnimalHusbandrySaveData>(
            JsonUtility.ToJson(captured));
        invalid.animals[0].speciesDefinitionId = "unknown_species";
        RequireThrows(() => BuildHusbandry(invalid, 99f),
            "Unknown authored wildlife species was accepted.");
        Require(live.State.Animals.Count == 1
            && live.State.NextTickAt == 11f,
            "Failed husbandry build mutated the live candidate.");

        DungeonAnimalHusbandrySaveData unknownItem = JsonUtility.FromJson<DungeonAnimalHusbandrySaveData>(
            JsonUtility.ToJson(captured));
        unknownItem.animals[0].products.Add(new AnimalProductProgressSaveData
        {
            itemDefinitionId = "resource:invented-product"
        });
        RequireThrows(() => BuildHusbandry(unknownItem, 99f),
            "Non-catalog husbandry product was accepted.");
    }

    private static AnimalHusbandryRestoreCandidate BuildHusbandry(
        DungeonAnimalHusbandrySaveData payload,
        float nextTickAt) => AnimalHusbandryStateCodec.BuildRestore(
        payload,
        nextTickAt,
        speciesId => speciesId.Equals(new WildlifeSpeciesId("cave_rat"))
            ? Array.Empty<ItemDefinitionId>()
            : null,
        itemId => itemId.Equals(new ItemDefinitionId("resource:manure")));

    private static string ResolveSpeciesIncident(CharacterSpeciesId speciesId) =>
        speciesId.Equals(new CharacterSpeciesId("Golem"))
            ? CharacterSpeciesIncidentIds.GolemCoreOverload
            : null;

    private static void RequireThrows(Action action, string message)
    {
        try
        {
            action();
        }
        catch (InvalidOperationException)
        {
            return;
        }

        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
    }
}
#endif
