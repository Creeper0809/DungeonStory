using System;
using UnityEditor;
using UnityEngine;
using VContainer;
using VContainer.Unity;
using Object = UnityEngine.Object;

public static class FacilityEvolutionBuildingReplacerFactoryDebugScenarios
{
    public static bool RunPlayModeSmoke(out string report)
    {
        report = string.Empty;
        if (!EditorApplication.isPlaying)
        {
            report = "PlayMode is required.";
            return false;
        }

        LifetimeScope scope = Object.FindFirstObjectByType<LifetimeScope>(FindObjectsInactive.Include);
        if (scope == null || scope.Container == null)
        {
            report = "No active LifetimeScope/container.";
            return false;
        }

        IFacilityEvolutionBuildingReplacerFactory factory =
            scope.Container.Resolve<IFacilityEvolutionBuildingReplacerFactory>();
        FacilityFeatureSceneRuntimeReferences runtimeReferences =
            scope.Container.Resolve<FacilityFeatureSceneRuntimeReferences>();
        if (factory == null || runtimeReferences == null)
        {
            report = $"Resolve failed. factory={factory != null}, runtimeReferences={runtimeReferences != null}";
            return false;
        }

        IFacilityEvolutionBuildingReplacer replacer = factory.Create();
        bool rejectsNull = replacer != null
            && !replacer.CanReplace(null, null, out string reason)
            && !string.IsNullOrWhiteSpace(reason);

        FacilityEvolutionRuntime runtime = null;
        GameObject temporaryRuntimeObject = null;
        bool temporaryRuntimeInjected = false;
        string runtimeReason = string.Empty;
        try
        {
            runtime = runtimeReferences.Evolution;
        }
        catch (Exception ex)
        {
            runtimeReason = ex.GetType().Name;
        }

        if (runtime == null)
        {
            temporaryRuntimeObject = new GameObject("FacilityEvolutionRuntimeSmoke");
            runtime = temporaryRuntimeObject.AddComponent<FacilityEvolutionRuntime>();
            scope.Container.Inject(runtime);
            temporaryRuntimeInjected = true;
        }

        report = $"scope={scope.name}, factoryResolved=True, runtimeReferencesResolved=True, replacer={replacer?.GetType().Name ?? "null"}, rejectsNull={rejectsNull}, runtime={(runtime != null ? runtime.name : "null")}, runtimeReason={runtimeReason}, temporaryRuntimeInjected={temporaryRuntimeInjected}";

        if (temporaryRuntimeObject != null)
        {
            Object.Destroy(temporaryRuntimeObject);
        }

        return replacer != null && rejectsNull && runtime != null;
    }
}
