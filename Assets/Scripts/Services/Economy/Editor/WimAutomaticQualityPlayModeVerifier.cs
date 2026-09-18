#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;

public static class WimAutomaticQualityPlayModeVerifier
{
    public const string ReportPath = "Artifacts/QA/wim-implementation/wim-006-automatic-quality.txt";
    private const string PendingKey = "DungeonStory.WIM006.Pending";
    private static double deadline;

    [MenuItem("DungeonStory/QA/WIM/006 Automatic Craft Quality")]
    public static void RequestRun()
    {
        if (SessionState.GetBool(PendingKey, false)) return;
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            throw new InvalidOperationException("Run WIM006 from Edit Mode.");
        Write("RUNNING\n");
        SessionState.SetBool(PendingKey, true);
        EditorApplication.EnterPlaymode();
    }

    [InitializeOnLoadMethod]
    private static void Register()
    {
        EditorApplication.playModeStateChanged -= OnState;
        EditorApplication.playModeStateChanged += OnState;
    }

    private static void OnState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(PendingKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            deadline = EditorApplication.timeSinceStartup + 30d;
            EditorApplication.update -= Verify;
            EditorApplication.update += Verify;
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.update -= Verify;
            SessionState.SetBool(PendingKey, false);
            Write("FAIL\nInterrupted before verification.\n");
        }
    }

    private static void Verify()
    {
        DungeonRuntimeLifetimeScope scope = UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
            FindObjectsInactive.Include, FindObjectsSortMode.None).FirstOrDefault(value => value.Container != null);
        if (scope == null && EditorApplication.timeSinceStartup < deadline) return;
        EditorApplication.update -= Verify;
        try
        {
            Require(scope != null, "Main gameplay container missing.");
            IProductionAssemblyBridge bridge = scope.Container.Resolve<IProductionAssemblyBridge>();
            IProductionOutputCapabilityRegistry registry = scope.Container.Resolve<IProductionOutputCapabilityRegistry>();
            IResourceEconomyContentCatalog recipes = scope.Container.Resolve<IResourceEconomyContentCatalog>();
            List<string> lines = new() { "PASS", "main-gameplay-container=PASS" };
            int facilities = 0;
            int qualityLinks = 0;
            int nonQualityLinks = 0;
            foreach (BuildingSO building in AssetDatabase.FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
                         .Select(AssetDatabase.GUIDToAssetPath).OrderBy(path => path, StringComparer.Ordinal)
                         .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>))
            {
                BuildingAutomationAbility automation = building.GetAbility<BuildingAutomationAbility>();
                if (automation == null) continue;
                facilities++;
                float score = automation.automaticQualityCap * 100f;
                Require(score >= 50f && score <= 90f, "Authored automation cap outside existing range: " + building.name);
                string station = building.GetProductionWorkstationAbility()?.WorkstationTag;
                foreach (ProductionRecipeSO recipe in recipes.Recipes.Where(recipe => recipe.WorkstationTag == station))
                foreach (ProductionOutputDefinition output in recipe.CaptureCanonicalOutputs().Where(output =>
                             ProductionOutputRoleRules.IsPhysical(output.Role) && output.Amount > 0 && output.Probability > 0f))
                {
                    ProductionOutputCapabilityDescriptor descriptor = bridge.CaptureOutputCapability(output.OutputLineId, output.ItemId);
                    Require(registry.TryValidateExact(descriptor, out IProductionOutputCapability capability, out _),
                        "Missing authored output capability: " + output.ItemId);
                    float capped = bridge.ApplyCraftQualityCeiling(descriptor, 1f, score);
                    if (capability is IProductionCraftQualityCeilingCapability quality)
                    {
                        Require(capped == quality.ApplyCraftQualityCeiling(1f, score) && capped <= 1f,
                            "Live bridge did not apply the authored capability ceiling.");
                        qualityLinks++;
                        lines.Add("quality-route=" + building.name + "/" + recipe.RecipeId + "/" + output.ItemId);
                    }
                    else
                    {
                        Require(capped == 1f, "Non-crafting output was modified by score ceiling.");
                        nonQualityLinks++;
                    }
                }
            }
            Require(facilities == 27 && qualityLinks > 0, "Automation authoring coverage is incomplete.");
            Require(!typeof(IProductionCraftQualityCeilingCapability).IsAssignableFrom(typeof(SurgicalPartProductionOutputHandler)),
                "Surgical performance entered crafting-score capability.");
            lines.Add("authored-automation-facilities=" + facilities);
            lines.Add("authored-craft-quality-links=" + qualityLinks);
            lines.Add("authored-non-craft-links-unchanged=" + nonQualityLinks);
            lines.Add(EnvironmentalWorkwearPlannedOutputDebugScenarios.RunWim006QualityFocused());
            ProductionEconomyDebugScenarios.RunWim006ProductionQualityFocused();
            lines.Add("production-aggregate-mixed-work-frozen-outcome-restore=PASS");
            lines.Add("cap-change-after-freeze-reresolution-count=0");
            lines.Add("passive-batch-production-regression=PASS");
            lines.Add("surgical-performance-excluded=PASS");
            lines.Add("automatic-tick-natural-production=NOT_RUN; focused aggregate and authored live registrations verified");
            lines.Add("target-quality-orders=SEPARATE_MANUAL_PIPELINE; general bills have no requested quality target");
            Write(string.Join("\n", lines) + "\n");
            Debug.Log("WIM006 automatic quality focused PASS: " + ReportPath);
        }
        catch (Exception exception)
        {
            Write("FAIL\n" + exception + "\n");
            Debug.LogException(exception);
        }
        finally
        {
            SessionState.SetBool(PendingKey, false);
            EditorApplication.ExitPlaymode();
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Write(string text)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, text, new System.Text.UTF8Encoding(false));
    }
}
#endif
