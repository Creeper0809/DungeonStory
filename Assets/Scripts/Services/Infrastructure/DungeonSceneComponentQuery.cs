using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public sealed class DungeonSceneComponentQuery
{
    private readonly int prioritySceneHandle;
    private readonly Scene priorityScene;

    public DungeonSceneComponentQuery()
    {
    }

    public DungeonSceneComponentQuery(Scene priorityScene)
    {
        this.priorityScene = priorityScene;
        prioritySceneHandle = priorityScene.IsValid() ? priorityScene.handle : 0;
    }

    public T First<T>(bool includeInactive = false) where T : Component
    {
        foreach (T component in EnumerateLoadedSceneComponents<T>(includeInactive))
        {
            return component;
        }

        return null;
    }

    public IReadOnlyList<T> All<T>(bool includeInactive = false) where T : Component
    {
        List<T> results = new List<T>();
        HashSet<int> seen = new HashSet<int>();

        foreach (T component in EnumerateLoadedSceneComponents<T>(includeInactive))
        {
            int instanceId = component.GetInstanceID();
            if (seen.Add(instanceId))
            {
                results.Add(component);
            }
        }

        return results;
    }

    private IEnumerable<T> EnumerateLoadedSceneComponents<T>(bool includeInactive) where T : Component
    {
        foreach (Scene scene in EnumerateLoadedScenesByPriority())
        {
            if (!scene.IsValid()
                || (!scene.isLoaded && scene.handle != prioritySceneHandle))
            {
                continue;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            foreach (T component in EnumerateRootComponents<T>(roots, includeInactive, activeRootsOnly: true))
            {
                yield return component;
            }

            if (!includeInactive)
            {
                continue;
            }

            foreach (T component in EnumerateRootComponents<T>(roots, includeInactive: true, activeRootsOnly: false))
            {
                yield return component;
            }
        }
    }

    private IEnumerable<Scene> EnumerateLoadedScenesByPriority()
    {
        Scene activeScene = SceneManager.GetActiveScene();
        Scene priorityScene = FindPriorityScene();
        if (priorityScene.IsValid())
        {
            yield return priorityScene;
        }

        if (activeScene.IsValid() && activeScene.isLoaded)
        {
            if (!priorityScene.IsValid() || activeScene.handle != priorityScene.handle)
            {
                yield return activeScene;
            }
        }

        for (int i = 0; i < SceneManager.sceneCount; i++)
        {
            Scene scene = SceneManager.GetSceneAt(i);
            if (!scene.IsValid()
                || !scene.isLoaded
                || (priorityScene.IsValid() && scene.handle == priorityScene.handle)
                || (activeScene.IsValid() && scene.handle == activeScene.handle))
            {
                continue;
            }

            yield return scene;
        }
    }

    private Scene FindPriorityScene()
    {
        return prioritySceneHandle != 0 && priorityScene.IsValid()
            ? priorityScene
            : default;
    }

    private static IEnumerable<T> EnumerateRootComponents<T>(
        IEnumerable<GameObject> roots,
        bool includeInactive,
        bool activeRootsOnly) where T : Component
    {
        foreach (GameObject root in roots)
        {
            if (root == null)
            {
                continue;
            }

            bool rootActive = root.activeInHierarchy;
            if (activeRootsOnly != rootActive)
            {
                continue;
            }

            foreach (T component in root.GetComponentsInChildren<T>(includeInactive))
            {
                if (component != null)
                {
                    yield return component;
                }
            }
        }
    }
}
