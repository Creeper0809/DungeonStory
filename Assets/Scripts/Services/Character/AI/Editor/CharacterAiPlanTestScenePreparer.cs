using System.Linq;
using BehaviorDesigner.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

internal static class CharacterAiPlanTestScenePreparer
{
    public static string Prepare()
    {
        AssetDatabase.Refresh();
        int prefabChanges = EnsureCharacterPrefabs();
        string testScenePath = CopySampleScene();
        Scene scene = EditorSceneManager.OpenScene(testScenePath, OpenSceneMode.Single);
        int sceneChanges = EnsureSceneCharacters(scene);
        EnsureSceneMarker(scene);
        CharacterAiBehaviorDesignerGraphBuilder.BuildVisualCharacterAiBehaviorTrees();
        EditorSceneManager.SaveScene(scene);
        AssetDatabase.SaveAssets();
        return $"Prepared character AI test scene: {testScenePath}. Prefab changes: {prefabChanges}, scene changes: {sceneChanges}";
    }

    private static int EnsureCharacterPrefabs()
    {
        int changes = 0;
        string[] prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/Prefabs" });
        foreach (string guid in prefabGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                CharacterActor[] actors = root.GetComponentsInChildren<CharacterActor>(true);
                if (actors.Length == 0)
                {
                    continue;
                }

                bool changed = false;
                foreach (CharacterActor actor in actors)
                {
                    changed |= EnsureCharacterAiComponents(actor.gameObject);
                }

                if (changed)
                {
                    PrefabUtility.SaveAsPrefabAsset(root, path);
                    changes++;
                    Debug.Log($"Updated character AI prefab components: {path}");
                }
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        return changes;
    }

    private static string CopySampleScene()
    {
        const string sourcePath = "Assets/Scenes/SampleScene.unity";
        const string targetPath = "Assets/Scenes/CharacterAiTestScene.unity";
        if (AssetDatabase.LoadAssetAtPath<SceneAsset>(targetPath) != null)
        {
            Debug.Log($"Using existing character AI test scene: {targetPath}");
            return targetPath;
        }

        if (!AssetDatabase.CopyAsset(sourcePath, targetPath))
        {
            throw new System.InvalidOperationException("Failed to copy SampleScene to character AI test scene.");
        }

        Debug.Log($"Copied {sourcePath} -> {targetPath}");
        return targetPath;
    }

    private static int EnsureSceneCharacters(Scene scene)
    {
        int changes = 0;
        foreach (GameObject root in scene.GetRootGameObjects())
        {
            foreach (CharacterActor actor in root.GetComponentsInChildren<CharacterActor>(true))
            {
                if (EnsureCharacterAiComponents(actor.gameObject))
                {
                    EditorUtility.SetDirty(actor.gameObject);
                    changes++;
                }
            }
        }

        return changes;
    }

    private static bool EnsureCharacterAiComponents(GameObject target)
    {
        bool changed = false;
        if (target.GetComponent<CharacterBlackboard>() == null)
        {
            target.AddComponent<CharacterBlackboard>();
            changed = true;
        }

        if (target.GetComponent<CustomerPersonaRuntime>() == null)
        {
            target.AddComponent<CustomerPersonaRuntime>();
            changed = true;
        }

        if (target.GetComponent<CharacterDialogueRuntime>() == null)
        {
            target.AddComponent<CharacterDialogueRuntime>();
            changed = true;
        }

        if (target.GetComponent<CharacterSocialMemory>() == null)
        {
            target.AddComponent<CharacterSocialMemory>();
            changed = true;
        }

        BehaviorTree tree = target.GetComponent<BehaviorTree>();
        if (tree == null)
        {
            tree = target.AddComponent<BehaviorTree>();
            changed = true;
        }

        tree.StartWhenEnabled = false;
        return changed;
    }

    private static void EnsureSceneMarker(Scene scene)
    {
        GameObject marker = scene.GetRootGameObjects()
            .FirstOrDefault((go) => go.name == "CharacterAiTestSceneMarker");
        if (marker == null)
        {
            marker = new GameObject("CharacterAiTestSceneMarker");
            SceneManager.MoveGameObjectToScene(marker, scene);
            Debug.Log("Added CharacterAiTestSceneMarker.");
        }

        bool changed = false;
        changed |= EnsureComponent<CharacterAiScheduler>(marker);
        changed |= EnsureComponent<LocalLlmRequestQueue>(marker);
        changed |= EnsureComponent<AiDirectorRuntime>(marker);
        changed |= EnsureComponent<SocialReputationRuntime>(marker);
        if (changed)
        {
            EditorUtility.SetDirty(marker);
            Debug.Log("Ensured CharacterAiTestSceneMarker AI runtime components.");
        }
    }

    private static bool EnsureComponent<T>(GameObject target)
        where T : Component
    {
        if (target.GetComponent<T>() != null)
        {
            return false;
        }

        target.AddComponent<T>();
        return true;
    }
}
