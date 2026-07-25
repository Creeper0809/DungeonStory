using System;
using UnityEngine;

public static class RuntimeDependency
{
    public static void Ensure(bool condition, Component owner, string requirement)
    {
        if (condition)
        {
            return;
        }

        string ownerName = owner != null ? owner.GetType().Name : "Unknown component";
        string location = owner != null ? GetLocation(owner) : "unknown scene object";
        throw new InvalidOperationException(
            $"{ownerName} on '{location}' requires {requirement}. "
            + "Create the object through the DungeonRuntimeLifetimeScope or configure it explicitly in tests.");
    }

    public static T Require<T>(T dependency, Component owner, string dependencyName = null)
        where T : class
    {
        bool missing = dependency == null
            || (dependency is UnityEngine.Object unityObject && unityObject == null);
        if (!missing)
        {
            return dependency;
        }

        string ownerName = owner != null ? owner.GetType().Name : "Unknown component";
        string location = owner != null ? GetLocation(owner) : "unknown scene object";
        string requiredName = string.IsNullOrWhiteSpace(dependencyName)
            ? typeof(T).Name
            : dependencyName;
        throw new InvalidOperationException(
            $"{ownerName} on '{location}' requires {requiredName}, but it was not injected. "
            + "Create the object through the DungeonRuntimeLifetimeScope or inject it explicitly in tests.");
    }

    private static string GetLocation(Component owner)
    {
        Transform current = owner.transform;
        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }

        string sceneName = owner.gameObject.scene.IsValid()
            ? owner.gameObject.scene.name
            : "NoScene";
        return sceneName + ":" + path;
    }
}
