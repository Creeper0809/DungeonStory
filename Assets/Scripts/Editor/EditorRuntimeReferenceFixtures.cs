#if UNITY_EDITOR
using UnityEngine;

public static class EditorRuntimeReferenceFixtures
{
    private static RunVariableRuntime runVariables;
    private static OffenseExpeditionRuntime offenseExpedition;
    private static InvasionThreatRuntime invasionThreat;
    private static InvasionDirectorRuntime invasionDirector;
    private static InvasionCombatReportRuntime invasionReports;

    public static DungeonSceneRuntimeReferences DungeonWithRunVariables
    {
        get
        {
            if (runVariables == null)
            {
                GameObject host = new GameObject("EditorRunVariableRuntime")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                runVariables = host.AddComponent<RunVariableRuntime>();
                runVariables.enabled = false;
            }

            return new DungeonSceneRuntimeReferences(
                new DungeonSceneServiceReferences(
                    null,
                    null,
                    null,
                    runVariables),
                new DungeonSceneViewReferences(
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    null));
        }
    }

    public static OffenseSceneRuntimeReferences OffenseWithExpedition
    {
        get
        {
            if (offenseExpedition == null)
            {
                GameObject host = new GameObject("EditorOffenseExpeditionRuntime")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                offenseExpedition = host.AddComponent<OffenseExpeditionRuntime>();
                offenseExpedition.enabled = false;
            }

            return new OffenseSceneRuntimeReferences(
                null,
                null,
                offenseExpedition,
                null,
                null);
        }
    }

    public static InvasionSceneRuntimeReferences Invasion
    {
        get
        {
            if (invasionThreat == null)
            {
                GameObject host = new GameObject("EditorInvasionRuntimes")
                {
                    hideFlags = HideFlags.HideAndDontSave
                };
                invasionThreat = host.AddComponent<InvasionThreatRuntime>();
                invasionDirector = host.AddComponent<InvasionDirectorRuntime>();
                invasionReports = host.AddComponent<InvasionCombatReportRuntime>();
                invasionThreat.enabled = false;
                invasionDirector.enabled = false;
                invasionReports.enabled = false;
            }

            return new InvasionSceneRuntimeReferences(
                invasionThreat,
                invasionDirector,
                invasionReports);
        }
    }
}
#endif
