using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;

public static class DungeonSceneHierarchyOrganizer
{
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private const string SceneRoot = "__Scene";
    private const string SystemsRoot = "__Systems";
    private const string RuntimeRoot = "__Runtime";
    private const string DebugRoot = "__Debug";
    private const string Cameras = "Cameras";
    private const string Input = "Input";
    private const string Grid = "Grid";
    private const string Ui = "UI";
    private const string World = "World";
    private const string Managers = "Managers";
    private const string Spawners = "Spawners";
    private const string RuntimeServices = "Runtime Services";
    private const string RuntimeBuildings = "Buildings";
    private const string RuntimeCharacters = "Characters";
    private const string RuntimeConstruction = "Construction";
    private const string RuntimeExterior = "Exterior";
    private const string RuntimeItems = "Items";
    private const string RuntimeSurvival = "Survival";
    private const string RuntimeWildlife = "Wildlife";
    private const string RuntimeCombat = "Combat";
    private const string RuntimeWorldUi = "World UI";
    private const string RuntimeDebug = "Debug";
    private const string Fixtures = "Fixtures";
    private const string Misc = "Misc";
    private const string PlacementGridTilePath = "Assets/Images/Using/Palette/whitebox.asset";
    private const string GroundSurfaceTilePath = "Assets/Images/Palette/TILESET SUMMER DAY_1.asset";
    private const string GroundFillTilePath = "Assets/Images/Palette/TILESET SUMMER DAY_9.asset";

    private static readonly string[] ManagedRoots =
    {
        SceneRoot,
        SystemsRoot,
        RuntimeRoot,
        DebugRoot
    };

    [MenuItem("DungeonStory/Scene/Organize Active Hierarchy")]
    public static void OrganizeActiveHierarchyMenu()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid())
        {
            Debug.LogWarning("활성 씬이 없어 하이어리키를 정리하지 못했습니다.");
            return;
        }

        int movedCount = OrganizeScene(scene);
        SanitizeTransientSceneVisuals(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        Debug.Log($"하이어리키 정리 완료: {scene.name}, 이동 {movedCount}개");
    }

    [MenuItem("DungeonStory/Scene/Organize Gameplay Hierarchy")]
    public static void OrganizeGameplayHierarchyMenu()
    {
        Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        int movedCount = OrganizeScene(scene);
        SanitizeTransientSceneVisuals(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"GameplayScene 하이어리키 정리 완료: 이동 {movedCount}개");
    }

    public static void OrganizeGameplaySceneForBatchMode()
    {
        Scene scene = EditorSceneManager.OpenScene(GameplayScenePath, OpenSceneMode.Single);
        int movedCount = OrganizeScene(scene);
        SanitizeTransientSceneVisuals(scene);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
        Debug.Log($"GameplayScene hierarchy organized for batch mode. Moved {movedCount} objects.");
    }

    private static int OrganizeScene(Scene scene)
    {
        Dictionary<string, Transform> folders = CreateFolderMap(scene);
        List<GameObject> roots = CollectOrganizableObjects(scene, folders);
        int movedCount = 0;

        foreach (GameObject rootObject in roots)
        {
            if (rootObject == null || IsManagedRoot(rootObject.name))
            {
                continue;
            }

            string folderKey = ResolveFolderKey(rootObject);
            if (!folders.TryGetValue(folderKey, out Transform targetParent) || targetParent == null)
            {
                targetParent = folders[DebugRoot + "/" + Misc];
            }

            if (rootObject.transform.parent == targetParent)
            {
                continue;
            }

            Undo.SetTransformParent(rootObject.transform, targetParent, "Organize Dungeon Hierarchy");
            movedCount++;
        }

        SetRootOrder(scene);
        return movedCount;
    }

    private static void SanitizeTransientSceneVisuals(Scene scene)
    {
        TileBase placementGridTile = AssetDatabase.LoadAssetAtPath<TileBase>(PlacementGridTilePath);
        TileBase groundSurfaceTile = AssetDatabase.LoadAssetAtPath<TileBase>(GroundSurfaceTilePath);
        TileBase groundFillTile = AssetDatabase.LoadAssetAtPath<TileBase>(GroundFillTilePath);
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject == null)
            {
                continue;
            }

            foreach (MonoBehaviour component in rootObject.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (component == null)
                {
                    continue;
                }

                string typeName = component.GetType().Name;
                if (typeName == "GridTexture")
                {
                    SanitizeGridTexture(component);
                }
                else if (typeName == "GridUIManager")
                {
                    SanitizePlacementGrid(component, placementGridTile);
                }
            }

            foreach (Tilemap tilemap in rootObject.GetComponentsInChildren<Tilemap>(true))
            {
                if (tilemap != null && tilemap.name == "Ground")
                {
                    RestoreGroundTiles(tilemap, groundSurfaceTile, groundFillTile);
                }
            }
        }
    }

    private static void RestoreGroundTiles(
        Tilemap groundTilemap,
        TileBase surfaceTile,
        TileBase fillTile)
    {
        if (groundTilemap == null || surfaceTile == null || fillTile == null)
        {
            return;
        }

        int topOccupiedY = int.MinValue;
        foreach (Vector3Int position in groundTilemap.cellBounds.allPositionsWithin)
        {
            if (groundTilemap.HasTile(position))
            {
                topOccupiedY = Mathf.Max(topOccupiedY, position.y);
            }
        }

        if (topOccupiedY == int.MinValue)
        {
            return;
        }

        foreach (Vector3Int position in groundTilemap.cellBounds.allPositionsWithin)
        {
            if (!groundTilemap.HasTile(position))
            {
                continue;
            }

            groundTilemap.SetTile(
                position,
                position.y == topOccupiedY ? surfaceTile : fillTile);
            groundTilemap.SetTileFlags(position, TileFlags.None);
            groundTilemap.SetColor(position, Color.white);
            groundTilemap.SetTileFlags(position, TileFlags.LockColor);
        }

        groundTilemap.color = Color.white;
        EditorUtility.SetDirty(groundTilemap);
    }

    private static void SanitizeGridTexture(MonoBehaviour gridTexture)
    {
        Type type = gridTexture.GetType();
        Tilemap wallTilemap = GetFieldValue<Tilemap>(type, gridTexture, "wallTilemap");
        TileBase wallTile = GetFieldValue<TileBase>(type, gridTexture, "wall");
        TileBase floorTile = GetFieldValue<TileBase>(type, gridTexture, "floor");
        if (wallTilemap == null)
        {
            return;
        }

        foreach (Vector3Int position in wallTilemap.cellBounds.allPositionsWithin)
        {
            TileBase tile = wallTilemap.GetTile(position);
            if (tile == null || tile == wallTile || tile == floorTile)
            {
                continue;
            }

            wallTilemap.SetTile(position, null);
        }

        EditorUtility.SetDirty(wallTilemap);
    }

    private static void SanitizePlacementGrid(MonoBehaviour gridUiManager, TileBase placementGridTile)
    {
        SerializedObject serializedManager = new SerializedObject(gridUiManager);
        SerializedProperty canvasProperty = serializedManager.FindProperty("gridTextureCanvas");
        SerializedProperty tileProperty = serializedManager.FindProperty("gridOverlayTile");
        GameObject overlayObject = canvasProperty?.objectReferenceValue as GameObject;

        if (tileProperty != null && placementGridTile != null)
        {
            tileProperty.objectReferenceValue = placementGridTile;
            serializedManager.ApplyModifiedPropertiesWithoutUndo();
        }

        if (overlayObject == null)
        {
            return;
        }

        Tilemap overlayTilemap = overlayObject.GetComponent<Tilemap>()
            ?? overlayObject.GetComponentInChildren<Tilemap>(true);
        if (overlayTilemap != null)
        {
            overlayTilemap.ClearAllTiles();
            overlayTilemap.color = Color.white;
            EditorUtility.SetDirty(overlayTilemap);
        }

        overlayObject.SetActive(false);
        EditorUtility.SetDirty(overlayObject);
    }

    private static T GetFieldValue<T>(Type type, object instance, string fieldName)
        where T : UnityEngine.Object
    {
        FieldInfo field = type.GetField(
            fieldName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return field?.GetValue(instance) as T;
    }

    private static List<GameObject> CollectOrganizableObjects(
        Scene scene,
        IReadOnlyDictionary<string, Transform> folders)
    {
        HashSet<Transform> folderTransforms = new HashSet<Transform>(folders.Values);
        List<GameObject> objects = new List<GameObject>();
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject == null)
            {
                continue;
            }

            if (!IsManagedRoot(rootObject.name))
            {
                objects.Add(rootObject);
            }
        }

        foreach (Transform folder in folderTransforms)
        {
            if (folder == null)
            {
                continue;
            }

            for (int i = 0; i < folder.childCount; i++)
            {
                Transform child = folder.GetChild(i);
                if (child == null || folderTransforms.Contains(child))
                {
                    continue;
                }

                objects.Add(child.gameObject);
            }
        }

        return objects;
    }

    private static Dictionary<string, Transform> CreateFolderMap(Scene scene)
    {
        Dictionary<string, Transform> folders = new Dictionary<string, Transform>(StringComparer.Ordinal)
        {
            [SceneRoot] = GetOrCreateRoot(scene, SceneRoot),
            [SystemsRoot] = GetOrCreateRoot(scene, SystemsRoot),
            [RuntimeRoot] = GetOrCreateRoot(scene, RuntimeRoot),
            [DebugRoot] = GetOrCreateRoot(scene, DebugRoot)
        };

        AddChild(folders, SceneRoot, Cameras);
        AddChild(folders, SceneRoot, Input);
        AddChild(folders, SceneRoot, Grid);
        AddChild(folders, SceneRoot, Ui);
        AddChild(folders, SceneRoot, World);
        AddChild(folders, SceneRoot, Misc);

        AddChild(folders, SystemsRoot, Managers);
        AddChild(folders, SystemsRoot, Spawners);
        AddChild(folders, SystemsRoot, RuntimeServices);

        AddChild(folders, RuntimeRoot, RuntimeBuildings);
        AddChild(folders, RuntimeRoot, RuntimeCharacters);
        AddChild(folders, RuntimeRoot, RuntimeConstruction);
        AddChild(folders, RuntimeRoot, RuntimeExterior);
        AddChild(folders, RuntimeRoot, RuntimeItems);
        AddChild(folders, RuntimeRoot, RuntimeSurvival);
        AddChild(folders, RuntimeRoot, RuntimeWildlife);
        AddChild(folders, RuntimeRoot, RuntimeCombat);
        AddChild(folders, RuntimeRoot, RuntimeWorldUi);
        AddChild(folders, RuntimeRoot, RuntimeDebug);

        AddChild(folders, DebugRoot, Fixtures);
        AddChild(folders, DebugRoot, Misc);
        return folders;
    }

    private static void AddChild(IDictionary<string, Transform> folders, string rootName, string childName)
    {
        Transform root = folders[rootName];
        Transform child = root.Find(childName);
        if (child == null)
        {
            GameObject childObject = new GameObject(childName);
            childObject.transform.SetParent(root, false);
            child = childObject.transform;
        }

        folders[rootName + "/" + childName] = child;
    }

    private static Transform GetOrCreateRoot(Scene scene, string rootName)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject != null && rootObject.name == rootName)
            {
                return rootObject.transform;
            }
        }

        GameObject folderObject = new GameObject(rootName);
        SceneManager.MoveGameObjectToScene(folderObject, scene);
        return folderObject.transform;
    }

    private static void SetRootOrder(Scene scene)
    {
        for (int i = 0; i < ManagedRoots.Length; i++)
        {
            Transform root = GetRootTransform(scene, ManagedRoots[i]);
            if (root != null)
            {
                root.SetSiblingIndex(i);
            }
        }
    }

    private static Transform GetRootTransform(Scene scene, string rootName)
    {
        foreach (GameObject rootObject in scene.GetRootGameObjects())
        {
            if (rootObject != null && rootObject.name == rootName)
            {
                return rootObject.transform;
            }
        }

        return null;
    }

    private static string ResolveFolderKey(GameObject rootObject)
    {
        string objectName = rootObject.name ?? string.Empty;
        if (rootObject.GetComponent<Camera>() != null)
        {
            return SceneRoot + "/" + Cameras;
        }

        if (HasComponentNamed(rootObject, "EventSystem")
            || Contains(objectName, "Command")
            || Contains(objectName, "Input"))
        {
            return SceneRoot + "/" + Input;
        }

        if (string.Equals(objectName, "Grid", StringComparison.Ordinal)
            || HasComponentNamed(rootObject, "GridSystemManager"))
        {
            return SceneRoot + "/" + Grid;
        }

        if (string.Equals(objectName, "UI", StringComparison.Ordinal)
            || rootObject.GetComponentInChildren<Canvas>(true) != null)
        {
            return SceneRoot + "/" + Ui;
        }

        if (Contains(objectName, "BackGround")
            || Contains(objectName, "Background")
            || Contains(objectName, "Tilemap")
            || Contains(objectName, "Ground"))
        {
            return SceneRoot + "/" + World;
        }

        if (HasComponentNamed(rootObject, "CharacterActor"))
        {
            return RuntimeRoot + "/" + RuntimeCharacters;
        }

        if (HasComponentNamed(rootObject, "WildlifeActor"))
        {
            return RuntimeRoot + "/" + RuntimeWildlife;
        }

        if (HasComponentNamed(rootObject, "BuildableObject"))
        {
            return RuntimeRoot + "/" + RuntimeBuildings;
        }

        if (Contains(objectName, "Spawner"))
        {
            return SystemsRoot + "/" + Spawners;
        }

        if (Contains(objectName, "Manager")
            || Contains(objectName, "Manger")
            || HasComponentNamed(rootObject, "GameManager"))
        {
            return SystemsRoot + "/" + Managers;
        }

        if (Contains(objectName, "Runtime")
            || Contains(objectName, "System")
            || Contains(objectName, "Service")
            || Contains(objectName, "Scope"))
        {
            return SystemsRoot + "/" + RuntimeServices;
        }

        if (LooksLikeCharacterFixture(objectName))
        {
            return RuntimeRoot + "/" + RuntimeCharacters;
        }

        if (Contains(objectName, "QA")
            || Contains(objectName, "Test")
            || Contains(objectName, "Debug")
            || Contains(objectName, "Fixture"))
        {
            return DebugRoot + "/" + Fixtures;
        }

        return SceneRoot + "/" + Misc;
    }

    private static bool LooksLikeCharacterFixture(string objectName)
    {
        return Contains(objectName, "Candidate")
            || Contains(objectName, "Customer")
            || Contains(objectName, "Visitor")
            || Contains(objectName, "Recruit")
            || Contains(objectName, "Regular");
    }

    private static bool HasComponentNamed(GameObject rootObject, string componentTypeName)
    {
        Component[] components = rootObject.GetComponentsInChildren<Component>(true);
        foreach (Component component in components)
        {
            if (component == null)
            {
                continue;
            }

            Type type = component.GetType();
            if (type.Name == componentTypeName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsManagedRoot(string objectName)
    {
        foreach (string rootName in ManagedRoots)
        {
            if (string.Equals(objectName, rootName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static bool Contains(string source, string value)
    {
        return source.IndexOf(value, StringComparison.OrdinalIgnoreCase) >= 0;
    }
}
