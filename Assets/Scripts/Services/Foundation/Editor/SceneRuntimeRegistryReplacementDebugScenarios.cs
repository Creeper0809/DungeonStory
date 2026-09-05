#if UNITY_EDITOR
using System;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;

public static class SceneRuntimeRegistryReplacementDebugScenarios
{
    [MenuItem("DungeonStory/Debug/Foundation/Verify Scene Registry Replacement")]
    public static void RunFromMenu()
    {
        Verify();
        Debug.Log("SCENE_RUNTIME_REGISTRY_REPLACEMENT=PASS");
    }

    public static void Verify()
    {
        SceneRuntimeRegistry<object> registry = new();
        object source = new();
        object replacement = new();
        object foreign = new();
        Require(registry.Register(source) && registry.Version == 1,
            "Registry fixture source was not registered.");
        Require(!registry.TryReplace(foreign, replacement)
                && registry.Version == 1
                && ReferenceEquals(registry.Entries[0], source),
            "Missing-source replacement mutated the registry.");
        Require(registry.TryReplace(source, replacement)
                && registry.Version == 2
                && registry.Entries.Count == 1
                && ReferenceEquals(registry.Entries[0], replacement),
            "Atomic replacement did not preserve slot/order/version semantics.");
        Require(!registry.TryReplace(source, foreign)
                && !registry.TryReplace(replacement, replacement)
                && registry.Version == 2
                && ReferenceEquals(registry.Entries[0], replacement),
            "Replay or self-replacement changed registry authority.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
