using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

public static class R31StructureRemediationBatch
{
    public static void RunContracts()
    {
        Require(Application.isBatchMode && !Application.isPlaying
            && DungeonPersistencePaths.IsIsolatedVerification,
            "R31 contracts require isolated edit-mode persistence.");
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        Require(!string.IsNullOrWhiteSpace(output) && Directory.Exists(output),
            "Missing R31 evidence directory.");
        string source = V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest();
        List<string> issues = new();
        void Observe(string message, string stack, LogType type)
        {
            if (type is LogType.Warning or LogType.Error or LogType.Assert or LogType.Exception)
                issues.Add(message);
        }
        Application.logMessageReceived += Observe;
        try
        {
            (string Name, Action Run)[] steps =
            {
                ("visitor-acquisition", CharacterPopulationDebugScenarios.RunR31VisitorAcquisitionContracts),
                ("population-query-regression", () => Require(
                    CharacterPopulationDebugScenarios.RunR30PopulationQueryFocused(),
                    "Existing population query and binding regression failed.")),
                ("production-capacity", R31ProductionSupportDebugScenarios.RunContracts),
                ("research-selection", BlueprintResearchDebugScenarios.RunR31LeapSelectionContracts),
                ("repair-selection", R31RepairSelectionDebugScenarios.RunContracts),
                ("haul-cache", HaulPlanConstructionSafetyDebugScenarios.RunR31AvailabilityCacheContracts),
                ("room-cache", RoomEnvironmentDebugScenarios.RunR31EnvironmentCacheContracts)
            };
            List<Exception> failures = new();
            foreach (var step in steps)
            {
                Debug.Log("R31_STEP_STARTED=" + step.Name);
                var elapsed = System.Diagnostics.Stopwatch.StartNew();
                int beforeIssues = issues.Count;
                try
                {
                    step.Run();
                    Require(issues.Count == beforeIssues,
                        step.Name + ": " + string.Join(" | ", issues.Skip(beforeIssues)));
                    WriteNew(Path.Combine(output, "r31-" + step.Name + ".txt"),
                        "result=PASS\nsource=" + source + "\nboundary=controlled native runtime contracts\n");
                    Debug.Log($"R31_STEP=PASS; name={step.Name}; seconds={elapsed.Elapsed.TotalSeconds:0.###}");
                }
                catch (Exception error)
                {
                    failures.Add(new InvalidOperationException(step.Name, error));
                    WriteNew(Path.Combine(output, "r31-" + step.Name + "-failure.txt"), error.ToString());
                    Debug.Log($"R31_STEP=FAIL; name={step.Name}; error={error}");
                }
            }
            if (failures.Count != 0)
                throw new AggregateException("R31 targeted contracts failed.", failures);
            Require(issues.Count == 0, string.Join(" | ", issues));
            Require(source == V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest(),
                "Source changed during R31 verification.");
            WriteNew(Path.Combine(output, "r31-contracts.txt"),
                "result=PASS\nsource=" + source + "\nsteps=" + steps.Length
                + "\nconsoleIssues=0\nboundary=controlled native; world restore verified separately\n");
            Debug.Log("R31_CONTRACTS=PASS");
        }
        finally { Application.logMessageReceived -= Observe; }
    }

    public static T StrictProxy<T>(Func<MethodInfo, object[], object> invoke) where T : class
    {
        T proxy = DispatchProxy.Create<T, R31StrictProxy>();
        ((R31StrictProxy)(object)proxy).InvokeHandler = invoke
            ?? throw new ArgumentNullException(nameof(invoke));
        return proxy;
    }

    public class R31StrictProxy : DispatchProxy
    {
        internal Func<MethodInfo, object[], object> InvokeHandler;
        protected override object Invoke(MethodInfo targetMethod, object[] args) =>
            InvokeHandler(targetMethod, args);
    }

    public static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void WriteNew(string path, string text)
    {
        using StreamWriter writer = new(new FileStream(path, FileMode.CreateNew));
        writer.Write(text);
    }
}
