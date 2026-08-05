using UnityEngine;
using UnityEngine.SceneManagement;

public static class DungeonRuntimeHierarchy
{
    public const string RootName = "__Runtime";
    public const string Buildings = "Buildings";
    public const string Characters = "Characters";
    public const string CharacterComposition = "Character Composition";
    public const string RestoreCandidates = "Restore Candidates";
    public const string Construction = "Construction";
    public const string Exterior = "Exterior";
    public const string Items = "Items";
    public const string Survival = "Survival";
    public const string Wildlife = "Wildlife";
    public const string Combat = "Combat";
    public const string WorldUi = "World UI";
    public const string Debug = "Debug";

    public static void Parent(GameObject child, string categoryName)
    {
        if (!Application.isPlaying || child == null || string.IsNullOrWhiteSpace(categoryName))
        {
            return;
        }

        Transform parent = GetCategory(categoryName, child);
        if (parent == null || child.transform.parent == parent)
        {
            return;
        }

        child.transform.SetParent(parent, true);
    }

    public static Transform GetCategory(string categoryName, GameObject sceneHint = null)
    {
        Scene scene = ResolveScene(sceneHint);
        Transform root = GetOrCreateRoot(scene);
        return GetOrCreateChild(root, categoryName);
    }

    private static Scene ResolveScene(GameObject sceneHint)
    {
        if (sceneHint != null && sceneHint.scene.IsValid())
        {
            return sceneHint.scene;
        }

        Scene activeScene = SceneManager.GetActiveScene();
        return activeScene.IsValid() ? activeScene : default;
    }

    private static Transform GetOrCreateRoot(Scene scene)
    {
        if (scene.IsValid())
        {
            foreach (GameObject rootObject in scene.GetRootGameObjects())
            {
                if (rootObject != null && rootObject.name == RootName)
                {
                    return rootObject.transform;
                }
            }
        }

        GameObject runtimeRoot = new GameObject(RootName);
        if (scene.IsValid() && runtimeRoot.scene != scene)
        {
            SceneManager.MoveGameObjectToScene(runtimeRoot, scene);
        }

        return runtimeRoot.transform;
    }

    private static Transform GetOrCreateChild(Transform root, string childName)
    {
        Transform child = root.Find(childName);
        if (child != null)
        {
            return child;
        }

        GameObject childObject = new GameObject(childName);
        childObject.transform.SetParent(root, false);
        return childObject.transform;
    }
}
