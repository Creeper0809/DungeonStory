using System;
using System.Collections.Generic;
using UnityEngine;
using VContainer.Unity;

public sealed class SceneBuildableLeakValidator : IInitializable
{
    private readonly SceneValidationReferences references;

    public SceneBuildableLeakValidator(SceneValidationReferences references)
    {
        this.references = references
            ?? throw new ArgumentNullException(nameof(references));
    }

    public void Initialize()
    {
        List<string> invalidSceneObjects = new List<string>();

        CollectMissingScriptObjects(invalidSceneObjects);
        CollectLeakedFacilities(invalidSceneObjects);
        CollectDuplicateRuntimeServices(
            nameof(LocalLlmRequestQueue),
            references.LocalLlmQueues.Count,
            invalidSceneObjects);

        if (invalidSceneObjects.Count == 0)
        {
            return;
        }

        throw new InvalidOperationException(
            "Invalid scene objects are saved in the active scene. " +
            "These objects can overlap runtime grid buildings and pollute click/collider selection. " +
            "Remove or repair the following scene objects before PlayMode:\n" +
            string.Join("\n", invalidSceneObjects));
    }

    private static void CollectDuplicateRuntimeServices(
        string serviceName,
        int serviceCount,
        List<string> invalidSceneObjects)
    {
        if (serviceCount <= 1)
        {
            return;
        }

        invalidSceneObjects.Add(
            $"- Duplicate runtime service: {serviceName} has {serviceCount} loaded instances.");
    }

    private void CollectLeakedFacilities(List<string> invalidSceneObjects)
    {
        IReadOnlyList<BuildableObject> buildables = references.Buildables;

        for (int i = 0; i < buildables.Count; i++)
        {
            BuildableObject buildable = buildables[i];
            if (!IsLeakedFacilityRoot(buildable))
            {
                continue;
            }

            invalidSceneObjects.Add(DescribeLeakedBuildable(buildable));
        }
    }

    private void CollectMissingScriptObjects(List<string> invalidSceneObjects)
    {
        IReadOnlyList<GameObject> roots = references.Roots;
        for (int i = 0; i < roots.Count; i++)
        {
            CollectMissingScriptObjects(roots[i], invalidSceneObjects);
        }
    }

    private static void CollectMissingScriptObjects(GameObject gameObject, List<string> invalidSceneObjects)
    {
        if (gameObject == null)
        {
            return;
        }

        Component[] components = gameObject.GetComponents<Component>();
        for (int i = 0; i < components.Length; i++)
        {
            if (components[i] == null)
            {
                invalidSceneObjects.Add(DescribeMissingScript(gameObject));
                break;
            }
        }

        Transform transform = gameObject.transform;
        for (int i = 0; i < transform.childCount; i++)
        {
            CollectMissingScriptObjects(transform.GetChild(i).gameObject, invalidSceneObjects);
        }
    }

    private static bool IsLeakedFacilityRoot(BuildableObject buildable)
    {
        if (buildable == null || buildable.gameObject == null)
        {
            return false;
        }

        // CharacterSpawner inherits BuildableObject for grid entry helpers, but it is a
        // legitimate scene service rather than a placed building instance.
        if (buildable is CharacterSpawner)
        {
            return false;
        }

        return buildable.id == 0
            && (buildable.buildPoses == null || buildable.buildPoses.Count == 0)
            && buildable.transform.parent == null;
    }

    private static string DescribeLeakedBuildable(BuildableObject buildable)
    {
        GameObject gameObject = buildable.gameObject;
        return $"- Uninitialized buildable: {gameObject.scene.path} :: {GetHierarchyPath(gameObject)} ({buildable.GetType().Name})";
    }

    private static string DescribeMissingScript(GameObject gameObject)
    {
        return $"- Missing script: {gameObject.scene.path} :: {GetHierarchyPath(gameObject)}";
    }

    private static string GetHierarchyPath(GameObject gameObject)
    {
        if (gameObject == null)
        {
            return "<null>";
        }

        Stack<string> names = new Stack<string>();
        Transform current = gameObject.transform;
        while (current != null)
        {
            names.Push(current.name);
            current = current.parent;
        }

        return string.Join("/", names);
    }
}
