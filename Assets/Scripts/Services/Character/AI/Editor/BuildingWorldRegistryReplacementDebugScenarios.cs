#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class BuildingWorldRegistryReplacementDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Character AI/Verify Building World Registry Replacement")]
    public static void RunFromMenu()
    {
        Verify();
        Debug.Log("BUILDING_WORLD_REGISTRY_REPLACEMENT=PASS");
    }

    public static void Verify()
    {
        BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Wall.asset");
        Require(definition != null,
            "Building replacement fixture definition is missing.");

        ICharacterAiWorldRegistry world = CharacterAiEditorTestDependencies.WorldRegistry;
        IBuildingWorldRegistryPort port =
            (IBuildingWorldRegistryPort)CharacterAiEditorTestDependencies.WorldRegistry;
        int initialCount = world.Buildings.Count;
        GameObject sourceObject = null;
        GameObject candidateObject = null;
        GameObject invalidObject = null;
        BuildableObject source = null;
        BuildableObject candidate = null;
        BuildableObject invalid = null;
        CharacterId owner = new("character:building-replacement-fixture");

        try
        {
            sourceObject = new GameObject("BuildingReplacementSource");
            source = sourceObject.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(source);
            source.Initialization(definition, Vector2Int.zero);

            candidateObject = new GameObject("BuildingReplacementCandidate");
            candidateObject.SetActive(false);
            candidate = candidateObject.AddComponent<BuildableObject>();
            candidate.PrepareForDetachedRestore();
            CharacterAiEditorTestDependencies.Inject(candidate);
            candidate.RestorePersistentIdentity(source.PersistentInstanceId);
            candidate.Initialization(definition, Vector2Int.right);

            Require(world.Buildings.Count == initialCount + 1
                    && world.Buildings.Contains(source)
                    && !world.Buildings.Contains(candidate),
                "Detached candidate became visible before registry replacement.");
            port.TrackTransientCharacterOwnership(
                source,
                owner,
                BuildingTransientOwnershipKind.VisitReservation
                | BuildingTransientOwnershipKind.ActiveUse);
            Require(world.GetTransientBuildingOwnershipCount(owner) == 1,
                "Source transient ownership fixture was not registered.");

            int versionBefore = world.BuildingVersion;
            Require(port.TryReplaceBuilding(source, candidate, out string failure)
                    && string.IsNullOrEmpty(failure)
                    && world.BuildingVersion == versionBefore + 1
                    && world.Buildings.Count == initialCount + 1
                    && !world.Buildings.Contains(source)
                    && world.Buildings.Contains(candidate)
                    && world.GetTransientBuildingOwnershipCount(owner) == 1,
                $"Atomic building-world replacement failed: {failure}");

            Require(port.TryRollbackBuildingReplacement(
                        candidate,
                        source,
                        out string rollbackFailure)
                    && string.IsNullOrEmpty(rollbackFailure)
                    && world.BuildingVersion == versionBefore + 2
                    && world.Buildings.Contains(source)
                    && !world.Buildings.Contains(candidate)
                    && world.GetTransientBuildingOwnershipCount(owner) == 1,
                $"Atomic building-world rollback failed: {rollbackFailure}");
            Require(port.TryReplaceBuilding(source, candidate, out failure)
                    && string.IsNullOrEmpty(failure)
                    && world.BuildingVersion == versionBefore + 3
                    && world.Buildings.Contains(candidate)
                    && !world.Buildings.Contains(source),
                $"Building-world replacement retry failed: {failure}");

            int versionAfter = world.BuildingVersion;
            Require(!port.TryReplaceBuilding(source, candidate, out _)
                    && !port.TryReplaceBuilding(candidate, candidate, out _)
                    && world.BuildingVersion == versionAfter
                    && world.Buildings.Contains(candidate),
                "Replacement replay or self-replacement mutated world authority.");

            candidate.gameObject.SetActive(true);
            candidate.PublishDetachedRestore();
            Require(world.BuildingVersion == versionAfter
                    && world.Buildings.Contains(candidate),
                "Detached publication re-registered the replacement candidate.");

            invalidObject = new GameObject("BuildingReplacementInvalidCandidate");
            invalid = invalidObject.AddComponent<BuildableObject>();
            CharacterAiEditorTestDependencies.Inject(invalid);
            invalid.RestorePersistentIdentity(candidate.PersistentInstanceId);
            Require(!port.TryReplaceBuilding(candidate, invalid, out string invalidFailure)
                    && string.Equals(
                        invalidFailure,
                        "building-world-replacement-registration-drift",
                        StringComparison.Ordinal)
                    && world.BuildingVersion == versionAfter
                    && world.Buildings.Contains(candidate),
                "A non-detached candidate bypassed registration preflight.");

            Object.DestroyImmediate(sourceObject);
            sourceObject = null;
            Require(world.BuildingVersion == versionAfter
                    && world.Buildings.Contains(candidate),
                "Destroying the replaced source unregistered the survivor.");

            port.UntrackTransientCharacterOwnership(
                candidate,
                owner,
                BuildingTransientOwnershipKind.VisitReservation
                | BuildingTransientOwnershipKind.ActiveUse);
            Require(world.GetTransientBuildingOwnershipCount(owner) == 0,
                "Transient ownership was not operable through the survivor.");

            candidate.DestroySelf();
            candidateObject = null;
            Require(world.Buildings.Count == initialCount,
                "Destroying the survivor through the production lifecycle leaked "
                + $"a building registry entry: initial={initialCount}; "
                + $"current={world.Buildings.Count}; "
                + $"containsCandidate={world.Buildings.Contains(candidate)}.");
        }
        finally
        {
            if (source != null)
                port.UnregisterBuilding(source);
            if (candidate != null)
                port.UnregisterBuilding(candidate);
            if (invalid != null)
                port.UnregisterBuilding(invalid);
            port.UntrackAllTransientCharacterOwnership(source);
            port.UntrackAllTransientCharacterOwnership(candidate);
            port.UntrackAllTransientCharacterOwnership(invalid);
            if (sourceObject != null)
                Object.DestroyImmediate(sourceObject);
            if (candidateObject != null)
                Object.DestroyImmediate(candidateObject);
            if (invalidObject != null)
                Object.DestroyImmediate(invalidObject);
        }

        Require(world.Buildings.Count == initialCount,
            "Building replacement fixture did not restore world registry membership.");
        VerifyWarehouseProjection();
        VerifyRetailProjection();
    }

    private static void VerifyWarehouseProjection()
    {
        BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/P1/P1_Warehouse.asset");
        Require(definition != null,
            "Warehouse replacement fixture definition is missing.");
        ICharacterAiWorldRegistry world = CharacterAiEditorTestDependencies.WorldRegistry;
        IBuildingWorldRegistryPort port = (IBuildingWorldRegistryPort)world;
        int initialBuildings = world.Buildings.Count;
        int initialWarehouses = world.Warehouses.Count;
        GameObject sourceObject = null;
        GameObject candidateObject = null;
        Facility source = null;
        Facility candidate = null;
        try
        {
            sourceObject = new GameObject("WarehouseReplacementSource");
            source = sourceObject.AddComponent<Facility>();
            CharacterAiEditorTestDependencies.Inject(source);
            source.Initialization(definition, Vector2Int.zero);

            candidateObject = new GameObject("WarehouseReplacementCandidate");
            candidateObject.SetActive(false);
            candidate = candidateObject.AddComponent<Facility>();
            candidate.PrepareForDetachedRestore();
            CharacterAiEditorTestDependencies.Inject(candidate);
            candidate.RestorePersistentIdentity(source.PersistentInstanceId);
            candidate.Initialization(definition, Vector2Int.right);

            int buildingVersion = world.BuildingVersion;
            int warehouseVersion = world.WarehouseVersion;
            Require(port.TryReplaceBuilding(source, candidate, out string failure)
                    && string.IsNullOrEmpty(failure)
                    && world.BuildingVersion == buildingVersion + 1
                    && world.WarehouseVersion == warehouseVersion + 1
                    && world.Buildings.Contains(candidate)
                    && !world.Buildings.Contains(source)
                    && world.Warehouses.Contains(candidate)
                    && !world.Warehouses.Contains(source),
                $"Warehouse projection replacement failed: {failure}");
            Require(port.TryRollbackBuildingReplacement(
                        candidate,
                        source,
                        out string rollbackFailure)
                    && string.IsNullOrEmpty(rollbackFailure)
                    && world.BuildingVersion == buildingVersion + 2
                    && world.WarehouseVersion == warehouseVersion + 2
                    && world.Buildings.Contains(source)
                    && world.Warehouses.Contains(source),
                $"Warehouse projection rollback failed: {rollbackFailure}");
            Require(port.TryReplaceBuilding(source, candidate, out failure)
                    && string.IsNullOrEmpty(failure)
                    && world.BuildingVersion == buildingVersion + 3
                    && world.WarehouseVersion == warehouseVersion + 3,
                $"Warehouse projection replacement retry failed: {failure}");

            candidateObject.SetActive(true);
            candidate.PublishDetachedRestore();
            source.DestroySelf();
            sourceObject = null;
            candidate.DestroySelf();
            candidateObject = null;
        }
        finally
        {
            if (source != null)
                port.UnregisterBuilding(source);
            if (candidate != null)
                port.UnregisterBuilding(candidate);
            if (sourceObject != null)
                Object.DestroyImmediate(sourceObject);
            if (candidateObject != null)
                Object.DestroyImmediate(candidateObject);
        }
        Require(world.Buildings.Count == initialBuildings
                && world.Warehouses.Count == initialWarehouses,
            "Warehouse replacement fixture leaked a world projection.");
    }

    private static void VerifyRetailProjection()
    {
        BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/P1/P1_GeneralStore.asset");
        Require(definition != null,
            "Retail replacement fixture definition is missing.");
        ICharacterAiWorldRegistry world = CharacterAiEditorTestDependencies.WorldRegistry;
        IBuildingWorldRegistryPort port = (IBuildingWorldRegistryPort)world;
        int initialBuildings = world.Buildings.Count;
        int initialRetail = world.RetailFacilities.Count;
        GameObject sourceObject = null;
        GameObject candidateObject = null;
        Shop source = null;
        Shop candidate = null;
        try
        {
            sourceObject = new GameObject("RetailReplacementSource");
            source = sourceObject.AddComponent<Shop>();
            CharacterAiEditorTestDependencies.Inject(source);
            CharacterAiEditorTestDependencies.InjectShop(source);
            source.Initialization(definition, Vector2Int.zero);

            candidateObject = new GameObject("RetailReplacementCandidate");
            candidateObject.SetActive(false);
            candidate = candidateObject.AddComponent<Shop>();
            candidate.PrepareForDetachedRestore();
            CharacterAiEditorTestDependencies.Inject(candidate);
            CharacterAiEditorTestDependencies.InjectShop(candidate);
            candidate.RestorePersistentIdentity(source.PersistentInstanceId);
            candidate.Initialization(definition, Vector2Int.right);

            int buildingVersion = world.BuildingVersion;
            int retailVersion = world.RetailVersion;
            Require(port.TryReplaceBuilding(source, candidate, out string failure)
                    && string.IsNullOrEmpty(failure)
                    && world.BuildingVersion == buildingVersion + 1
                    && world.RetailVersion == retailVersion + 1
                    && world.Buildings.Contains(candidate)
                    && !world.Buildings.Contains(source)
                    && world.RetailFacilities.Contains(candidate)
                    && !world.RetailFacilities.Contains(source),
                $"Retail projection replacement failed: {failure}");
            Require(port.TryRollbackBuildingReplacement(
                        candidate,
                        source,
                        out string rollbackFailure)
                    && string.IsNullOrEmpty(rollbackFailure)
                    && world.BuildingVersion == buildingVersion + 2
                    && world.RetailVersion == retailVersion + 2
                    && world.Buildings.Contains(source)
                    && world.RetailFacilities.Contains(source),
                $"Retail projection rollback failed: {rollbackFailure}");
            Require(port.TryReplaceBuilding(source, candidate, out failure)
                    && string.IsNullOrEmpty(failure)
                    && world.BuildingVersion == buildingVersion + 3
                    && world.RetailVersion == retailVersion + 3,
                $"Retail projection replacement retry failed: {failure}");

            candidateObject.SetActive(true);
            candidate.PublishDetachedRestore();
            source.DestroySelf();
            sourceObject = null;
            candidate.DestroySelf();
            candidateObject = null;
        }
        finally
        {
            if (source != null)
                port.UnregisterBuilding(source);
            if (candidate != null)
                port.UnregisterBuilding(candidate);
            if (sourceObject != null)
                Object.DestroyImmediate(sourceObject);
            if (candidateObject != null)
                Object.DestroyImmediate(candidateObject);
        }
        Require(world.Buildings.Count == initialBuildings
                && world.RetailFacilities.Count == initialRetail,
            "Retail replacement fixture leaked a world projection.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
