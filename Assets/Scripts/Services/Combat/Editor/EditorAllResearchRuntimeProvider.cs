#if UNITY_EDITOR
using UnityEngine;

public sealed class EditorAllResearchRuntimeProvider
{
    private static EditorAllResearchRuntimeProvider instance;
    private readonly BlueprintResearchRuntime runtime;

    private EditorAllResearchRuntimeProvider()
    {
        GameObject host = new GameObject("EditorAllResearchRuntime")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        runtime = host.AddComponent<BlueprintResearchRuntime>();
        runtime.enabled = false;
        CharacterAiEditorTestDependencies.Inject(runtime);
        CompleteCurrentCatalog();
    }

    public static ProgressionSceneRuntimeReferences Instance
    {
        get
        {
            instance ??= new EditorAllResearchRuntimeProvider();
            instance.CompleteCurrentCatalog();
            return new ProgressionSceneRuntimeReferences(
                null,
                instance.runtime,
                null);
        }
    }

    private void CompleteCurrentCatalog()
    {
        foreach (ResearchProjectSO project in
                 Resources.LoadAll<ResearchProjectSO>(ResearchProjectSO.ResourcePath))
        {
            if (project != null)
            {
                runtime.State.Projects.Complete(project.ProjectId);
            }
        }
    }
}

public static class EditorLockedResearchRuntimeReferences
{
    private static BlueprintResearchRuntime runtime;

    public static ProgressionSceneRuntimeReferences Instance
    {
        get
        {
            if (runtime == null)
            {
                GameObject host = new GameObject("EditorLockedResearchRuntime")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                runtime = host.AddComponent<BlueprintResearchRuntime>();
                runtime.enabled = false;
                CharacterAiEditorTestDependencies.Inject(runtime);
            }

            return new ProgressionSceneRuntimeReferences(null, runtime, null);
        }
    }
}
#endif
