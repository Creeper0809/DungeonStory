using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class GameplayPerformanceSceneQuery
{
    public static T FindSceneComponent<T>(Scene scene) where T : Component
    {
        return FindSceneComponents<T>(scene)
            .FirstOrDefault(component => component != null);
    }

    public static T[] FindSceneComponents<T>(Scene scene) where T : Component
    {
        if (!scene.IsValid())
        {
            return Array.Empty<T>();
        }

        List<T> result = new List<T>();
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            result.AddRange(root.GetComponentsInChildren<T>(true));
        }

        return result.ToArray();
    }
}
