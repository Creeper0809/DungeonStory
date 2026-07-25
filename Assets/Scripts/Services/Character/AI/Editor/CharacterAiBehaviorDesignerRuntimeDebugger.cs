using BehaviorDesigner.Editor;
using BehaviorDesigner.Runtime;
using BehaviorDesigner.Runtime.Tasks;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class CharacterAiBehaviorDesignerRuntimeDebugger
{
    static CharacterAiBehaviorDesignerRuntimeDebugger()
    {
        EditorApplication.update -= RefreshActiveRuntimeTree;
        EditorApplication.update += RefreshActiveRuntimeTree;
    }

    private static void RefreshActiveRuntimeTree()
    {
        if (!EditorApplication.isPlaying)
        {
            return;
        }

        BehaviorDesignerWindow window = BehaviorDesignerWindow.instance;
        if (window == null)
        {
            return;
        }

        BehaviorTree tree = GetActiveRuntimeTree(window) ?? GetSelectedRuntimeTree();
        if (tree == null)
        {
            return;
        }

        CharacterActor actor = tree.GetComponent<CharacterActor>();
        if (actor == null)
        {
            return;
        }

        if (tree.ExecutionStatus != TaskStatus.Running)
        {
            LoadExternalSource(window, tree);
            return;
        }

        try
        {
            if (window.ActiveBehaviorSource == null || !ReferenceEquals(window.ActiveBehaviorSource.Owner, tree))
            {
                window.LoadBehavior(tree.GetBehaviorSource(), false, false);
            }

            tree.DungeonStoryRefreshVisualStatus(actor);
            window.Repaint();
        }
        catch (System.Exception exception)
        {
            Debug.Log($"Recovered Behavior Designer runtime debug view: {exception.Message}", tree);
            LoadExternalSource(window, tree);
        }
    }

    private static BehaviorTree GetActiveRuntimeTree(BehaviorDesignerWindow window)
    {
        IBehavior owner = window.ActiveBehaviorSource != null
            ? window.ActiveBehaviorSource.Owner
            : null;
        return owner as BehaviorTree;
    }

    private static BehaviorTree GetSelectedRuntimeTree()
    {
        GameObject selected = Selection.activeGameObject;
        if (selected == null)
        {
            return null;
        }

        return selected.GetComponent<CharacterActor>() != null
            ? selected.GetComponent<BehaviorTree>()
            : null;
    }

    private static void LoadExternalSource(BehaviorDesignerWindow window, BehaviorTree tree)
    {
        ExternalBehaviorTree external = tree != null
            ? tree.ExternalBehavior as ExternalBehaviorTree
            : null;
        if (window == null || external == null || external.BehaviorSource == null)
        {
            return;
        }

        window.LoadBehavior(external.BehaviorSource, false, false);
        window.Repaint();
    }
}
