using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using BehaviorDesigner.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

public static class DungeonRuntimeCompositionDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Infrastructure/Run Runtime Composition Scenarios")]
    public static void RunFromMenu()
    {
        if (!RunAll(true))
        {
            Debug.LogError("Runtime composition scenarios failed.");
        }
    }

    public static bool RunAll(bool logSuccess)
    {
        try
        {
            bool success = VerifyUnlistedInactiveComponentInjection()
                && VerifyMissingRequiredDependencyFailsWithContext()
                && VerifyDestroyedSceneComponentCacheRefreshes()
                && VerifyBehaviorSourceOwnershipRestoresBeforeDeserialization()
                && VerifyExpectedOccupantRemovalDoesNotDeleteReplacement()
                && VerifyUninitializedBuildableValidationIsPolymorphic()
                && VerifyRuntimeUnityObjectLifetimePolicy();
            if (success && logSuccess)
            {
                Debug.Log("Runtime composition scenarios passed.");
            }

            return success;
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            return false;
        }
    }

    private static bool VerifyUnlistedInactiveComponentInjection()
    {
        DungeonRuntimeInjectionProbeDependency dependency = new DungeonRuntimeInjectionProbeDependency();
        ContainerBuilder builder = new ContainerBuilder();
        builder.RegisterInstance(dependency);

        using IObjectResolver resolver = builder.Build();
        Scene probeScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        GameObject root = new GameObject("Runtime Composition Root Probe");
        GameObject child = new GameObject("Inactive Unlisted Injection Probe");
        child.transform.SetParent(root.transform, false);
        child.SetActive(false);
        DungeonRuntimeInjectionProbe probe = child.AddComponent<DungeonRuntimeInjectionProbe>();

        try
        {
            SceneManager.MoveGameObjectToScene(root, probeScene);

            MethodInfo injectionMethod = typeof(DungeonRuntimeLifetimeScope).GetMethod(
                "InjectSceneHierarchy",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (injectionMethod == null)
            {
                throw new MissingMethodException(
                    typeof(DungeonRuntimeLifetimeScope).FullName,
                    "InjectSceneHierarchy");
            }

            injectionMethod.Invoke(null, new object[] { resolver, probeScene });
            return ReferenceEquals(probe.Dependency, dependency);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(root);
            EditorSceneManager.CloseScene(probeScene, true);
        }
    }

    private static bool VerifyMissingRequiredDependencyFailsWithContext()
    {
        Scene probeScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        GameObject buildingObject = new GameObject("Missing Dependency Building Probe");
        SceneManager.MoveGameObjectToScene(buildingObject, probeScene);
        BuildableObject building = buildingObject.AddComponent<BuildableObject>();

        try
        {
            _ = building.EffectiveCapacity;
            return false;
        }
        catch (InvalidOperationException exception)
        {
            return exception.Message.Contains(nameof(BuildableObject), StringComparison.Ordinal)
                && exception.Message.Contains(probeScene.name, StringComparison.Ordinal)
                && exception.Message.Contains(buildingObject.name, StringComparison.Ordinal)
                && exception.Message.Contains(nameof(IRoomFacilityPolicy), StringComparison.Ordinal);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(buildingObject);
            EditorSceneManager.CloseScene(probeScene, true);
        }
    }

    private static bool VerifyDestroyedSceneComponentCacheRefreshes()
    {
        Scene probeScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        GameObject firstObject = new GameObject("First Cached Camera Probe", typeof(Camera));
        SceneManager.MoveGameObjectToScene(firstObject, probeScene);
        Camera firstCamera = firstObject.GetComponent<Camera>();
        DungeonSceneRuntimeReferences references = new DungeonSceneRuntimeReferences(
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            firstCamera,
            null);
        SceneMainCameraProvider provider = new SceneMainCameraProvider(
            references);

        try
        {
            if (!ReferenceEquals(provider.Camera, firstCamera))
            {
                return false;
            }

            UnityEngine.Object.DestroyImmediate(firstObject);
            firstObject = null;

            GameObject secondObject = new GameObject("Replacement Cached Camera Probe", typeof(Camera));
            SceneManager.MoveGameObjectToScene(secondObject, probeScene);
            try
            {
                references.RegisterMainCamera(secondObject.GetComponent<Camera>());
                return ReferenceEquals(provider.Camera, secondObject.GetComponent<Camera>());
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(secondObject);
            }
        }
        finally
        {
            if (firstObject != null)
            {
                UnityEngine.Object.DestroyImmediate(firstObject);
            }

            EditorSceneManager.CloseScene(probeScene, true);
        }
    }

    private static bool VerifyBehaviorSourceOwnershipRestoresBeforeDeserialization()
    {
        GameObject probeObject = new GameObject("Behavior Source Ownership Probe");
        try
        {
            BehaviorTree tree = probeObject.AddComponent<BehaviorTree>();
            tree.SetBehaviorSource(new BehaviorSource());
            if (tree.GetBehaviorSource().Owner != null)
            {
                return false;
            }

            tree.DungeonStoryEnsureBehaviorSourceOwnership();
            return ReferenceEquals(tree.GetBehaviorSource().Owner, tree);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(probeObject);
        }
    }

    private static bool VerifyExpectedOccupantRemovalDoesNotDeleteReplacement()
    {
        Grid grid = new Grid(1, 1);
        Vector2Int[] positions = { Vector2Int.zero };
        DungeonGridOccupantProbe source = new DungeonGridOccupantProbe(1);
        DungeonGridOccupantProbe replacement = new DungeonGridOccupantProbe(2);

        if (!grid.RegisterOccupant(source, GridLayer.Building, positions, false)
            || !grid.RemoveOccupant(source, GridLayer.Building, positions, false)
            || !grid.RegisterOccupant(replacement, GridLayer.Building, positions, false))
        {
            return false;
        }

        bool staleRemovalChangedGrid = grid.RemoveOccupant(
            source,
            GridLayer.Building,
            positions,
            false);
        return !staleRemovalChangedGrid
            && ReferenceEquals(grid.GetGridCell(Vector2Int.zero).GetOccupant(GridLayer.Building), replacement);
    }

    private static bool VerifyUninitializedBuildableValidationIsPolymorphic()
    {
        GameObject probeObject = new GameObject("Uninitialized Custom Buildable Probe");
        try
        {
            DungeonRuntimeBuildableProbe probe = probeObject.AddComponent<DungeonRuntimeBuildableProbe>();
            MethodInfo validationMethod = typeof(SceneBuildableLeakValidator).GetMethod(
                "IsLeakedFacilityRoot",
                BindingFlags.Static | BindingFlags.NonPublic);
            if (validationMethod == null)
            {
                throw new MissingMethodException(
                    typeof(SceneBuildableLeakValidator).FullName,
                    "IsLeakedFacilityRoot");
            }

            return (bool)validationMethod.Invoke(null, new object[] { probe });
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(probeObject);
        }
    }

    private static bool VerifyRuntimeUnityObjectLifetimePolicy()
    {
        FieldInfo[] cameraFields = typeof(CameraManager).GetFields(
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (cameraFields.Any(field => field.Name.StartsWith("fallback", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        MethodInfo construct = typeof(DungeonSceneBackdropFitter).GetMethod(
            "Construct",
            BindingFlags.Instance | BindingFlags.Public);
        if (construct == null
            || construct.GetParameters().Any(parameter =>
                parameter.ParameterType.Name.Contains("BackdropReferenceProvider", StringComparison.Ordinal)))
        {
            return false;
        }

        string scriptsRoot = Path.GetFullPath("Assets/Scripts");
        string[] forbiddenPatterns =
        {
            "??= sceneQuery.First<",
            "??= FindFirstObject",
            "??= GetComponent<",
            "??= Resources.Load<",
            "?.name",
            "Camera.main"
        };
        List<string> violations = new List<string>();
        foreach (string path in Directory.GetFiles(scriptsRoot, "*.cs", SearchOption.AllDirectories))
        {
            string normalized = path.Replace('\\', '/');
            if (normalized.Contains("/Editor/", StringComparison.Ordinal))
            {
                continue;
            }

            string source = File.ReadAllText(path);
            foreach (string pattern in forbiddenPatterns)
            {
                if (source.Contains(pattern, StringComparison.Ordinal))
                {
                    violations.Add($"{normalized}: {pattern}");
                }
            }

        }

        foreach (Type eventType in typeof(DungeonRuntimeLifetimeScope).Assembly.GetTypes()
                     .Where(type => type.IsValueType && type.Name.EndsWith("Event", StringComparison.Ordinal)))
        {
            FieldInfo[] staticPayloadFields = eventType.GetFields(
                BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            foreach (FieldInfo field in staticPayloadFields)
            {
                if (field.FieldType == eventType)
                {
                    violations.Add($"{eventType.FullName}.{field.Name}: static event payload cache");
                }
            }
        }

        if (violations.Count > 0)
        {
            throw new InvalidOperationException(
                "Runtime Unity object lifetime policy violations:\n" + string.Join("\n", violations));
        }

        string backdropPath = Path.Combine(
            scriptsRoot,
            "Views",
            "UI",
            "DungeonSceneBackdropFitter.cs");
        if (!File.Exists(backdropPath))
        {
            throw new FileNotFoundException(
                "Dungeon scene backdrop source is missing.",
                backdropPath);
        }

        string backdropSource = File.ReadAllText(backdropPath);
        return !backdropSource.Contains("BackGround", StringComparison.Ordinal)
            && !backdropSource.Contains("FindTilemap(\"Ground\"", StringComparison.Ordinal);
    }
}

public sealed class DungeonRuntimeInjectionProbeDependency { }

public sealed class DungeonRuntimeInjectionProbe : MonoBehaviour
{
    public DungeonRuntimeInjectionProbeDependency Dependency { get; private set; }

    [Inject]
    public void Construct(DungeonRuntimeInjectionProbeDependency dependency)
    {
        Dependency = dependency ?? throw new ArgumentNullException(nameof(dependency));
    }
}

public sealed class DungeonRuntimeBuildableProbe : BuildableObject { }

public sealed class DungeonGridOccupantProbe : IGridOccupant
{
    public DungeonGridOccupantProbe(int id)
    {
        GridId = id;
    }

    public int GridId { get; }
    public bool IsGridDestroyed => false;
    public bool IsGridVisitable => true;
    public bool IsGridMovement => false;
}
