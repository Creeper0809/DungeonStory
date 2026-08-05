#if UNITY_EDITOR
using System;
using UnityEditor;
using UnityEngine;

public static class PersistentIdentityDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Infrastructure/Run V18 Persistent Identity Contracts")]
    public static void RunAll()
    {
        IPersistentIdGenerator generator = new GuidPersistentIdGenerator();
        ItemStackId stackA = generator.NewItemStackId();
        ItemStackId stackB = generator.NewItemStackId();
        ItemInstanceId item = generator.NewItemInstanceId();
        CharacterId character = generator.NewCharacterId();
        BuildingInstanceId buildingId = generator.NewBuildingInstanceId();

        Require(stackA.IsValid && stackB.IsValid && !stackA.Equals(stackB),
            "Item stack IDs are invalid or duplicated.");
        Require(item.IsValid && character.IsValid && buildingId.IsValid,
            "A typed persistent ID was invalid.");

        GameObject characterObject = new("V18 Character Identity Contract");
        GameObject buildingObject = new("V18 Building Identity Contract");
        try
        {
            CharacterIdentity identity = characterObject.AddComponent<CharacterIdentity>();
            identity.SetPersistentId(character);
            Require(identity.TypedPersistentId.Equals(character),
                "CharacterIdentity did not retain its typed ID.");

            Facility building = buildingObject.AddComponent<Facility>();
            building.ConstructPersistentIdentity(generator);
            BuildingInstanceId assigned = building.RequirePersistentInstanceId();
            Require(assigned.IsValid,
                "BuildableObject did not receive a persistent building ID.");

            ModularFacilityBuildingSaveData save = new()
            {
                persistentInstanceId = assigned.Value,
                buildingId = 42
            };
            ModularFacilityBuildingSaveData restored =
                JsonUtility.FromJson<ModularFacilityBuildingSaveData>(
                    JsonUtility.ToJson(save));
            Require(restored != null
                    && ((BuildingInstanceId)restored.persistentInstanceId).Equals(assigned),
                "Building persistent ID did not survive DTO serialization.");

            string warehouseDestination =
                WarehouseStorageIdentity.RequireDestinationId(building);
            Require(string.Equals(
                    warehouseDestination,
                    WorldItemStackRuntime.WarehouseStorageDestinationPrefix + assigned.Value,
                    StringComparison.Ordinal),
                "Warehouse storage identity did not use the building instance ID.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(characterObject);
            UnityEngine.Object.DestroyImmediate(buildingObject);
        }

        Debug.Log(
            $"V18 PERSISTENT ID PASS: stack={stackA.Value}, item={item.Value}, "
            + $"character={character.Value}, building={buildingId.Value}");
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
