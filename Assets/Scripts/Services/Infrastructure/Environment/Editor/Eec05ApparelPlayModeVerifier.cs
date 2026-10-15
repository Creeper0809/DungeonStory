#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;
using static UnityEngine.Object;

public sealed class Eec05ApparelPlayModeVerifier
{
    private static bool running;
    private readonly List<string> warnings = new();
    private readonly List<string> errors = new();
    private JObject report;

    public static string ReportPath => DungeonPersistencePaths
        .IsIsolatedVerification
        ? Path.Combine(
            Path.GetDirectoryName(DungeonPersistencePaths.Root),
            "report.json")
        : "Artifacts/QA/wim-implementation/eec05-apparel-report.json";

    public static string StartFocused(
        IEnumerable<string> initialWarnings = null,
        IEnumerable<string> initialErrors = null)
    {
        Require(Application.isPlaying && !running,
            "Start once inside a fresh protected main Play session.");
        DungeonRuntimeLifetimeScope scope =
            FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(scope?.Container != null,
            "Main runtime is not initialized.");
        IDisposable persistence =
            scope.Container.Resolve<IDungeonSaveCommandService>() as IDisposable;
        Require(persistence != null, "Cannot protect user save files.");
        persistence.Dispose();
        scope.Container.Resolve<MetaProfilePersistenceService>().Dispose();
        GameManager host = FindFirstObjectByType<GameManager>();
        Require(host != null, "Missing main coroutine host.");
        host.isPause = true;
        scope.Container.Resolve<IGameTimeScaleController>().Scale = 0f;
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        if (DungeonPersistencePaths.IsIsolatedVerification
            && File.Exists(ReportPath))
        {
            throw new InvalidOperationException(
                "Create-only EEC05 report target already exists.");
        }
        File.WriteAllText(
            ReportPath,
            "{\"passed\":false,\"state\":\"RUNNING\"}\n");
        running = true;
        var verifier = new Eec05ApparelPlayModeVerifier();
        if (initialWarnings != null)
            verifier.warnings.AddRange(initialWarnings);
        if (initialErrors != null)
            verifier.errors.AddRange(initialErrors);
        Application.logMessageReceived += verifier.CaptureLog;
        try
        {
            host.StartCoroutine(verifier.Observe());
        }
        catch
        {
            Application.logMessageReceived -= verifier.CaptureLog;
            running = false;
            throw;
        }
        return "RUNNING " + ReportPath;
    }

    private IEnumerator Observe()
    {
        Stack<IEnumerator> pending = new();
        pending.Push(Run());
        Exception failure = null;
        while (pending.Count > 0)
        {
            object current = null;
            bool moved;
            try
            {
                moved = pending.Peek().MoveNext();
                if (moved)
                    current = pending.Peek().Current;
            }
            catch (Exception error)
            {
                failure = error;
                break;
            }
            if (!moved)
            {
                (pending.Pop() as IDisposable)?.Dispose();
                continue;
            }
            if (current is IEnumerator nested)
                pending.Push(nested);
            else
                yield return current;
        }
        while (pending.Count > 0)
            (pending.Pop() as IDisposable)?.Dispose();

        Pause();
        Application.logMessageReceived -= CaptureLog;
        if (failure == null && (warnings.Count > 0 || errors.Count > 0))
        {
            failure = new InvalidOperationException(
                $"EEC05 captured warnings={warnings.Count}, errors={errors.Count}.");
        }
        if (failure != null)
        {
            report = new JObject
            {
                ["passed"] = false,
                ["test"] = "apparel-work-order-eec05",
                ["failure"] = failure.ToString(),
                ["fullAuditClosure"] = false,
                ["humanApprovalClaimed"] = false,
                ["trainingEligible"] = false
            };
        }
        report ??= new JObject
        {
            ["passed"] = false,
            ["test"] = "apparel-work-order-eec05",
            ["failure"] = "focused verifier produced no report",
            ["fullAuditClosure"] = false,
            ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false
        };
        report["capturedWarningCount"] = warnings.Count;
        report["capturedErrorCount"] = errors.Count;
        report["capturedWarnings"] = new JArray(warnings);
        report["capturedErrors"] = new JArray(errors);
        File.WriteAllText(
            ReportPath,
            report.ToString(Formatting.Indented) + Environment.NewLine);
        Debug.Log(report.ToString(Formatting.None));
        running = false;
        if (Application.isBatchMode
            && DungeonPersistencePaths.IsIsolatedVerification)
        {
            DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
            EditorApplication.Exit(failure == null ? 0 : 1);
        }
    }

    private IEnumerator Run()
    {
        DungeonRuntimeLifetimeScope scope =
            FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(scope?.Container != null,
            "Main runtime disappeared during the witness.");
        OwnerRunManager owner = FindFirstObjectByType<OwnerRunManager>();
        Require(owner != null, "Owner preparation is unavailable.");
        if (owner.CurrentOwnerActor == null)
        {
            string fastCommit =
                StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            for (int frame = 0; frame < 8; frame++)
                yield return null;
            Pause();
            owner = FindFirstObjectByType<OwnerRunManager>();
            Require(owner != null && owner.CurrentOwnerActor != null,
                "The current direct-gameplay party fixture did not publish an owner. "
                    + fastCommit);
        }
        foreach (CharacterActor actor in scope.Container
                     .Resolve<ICharacterWorldQuery>()
                     .Characters
                     .Where(value => value != null))
        {
            actor.SetAiPaused(true);
            actor.Brain?.StopCurrentActionForReplan(
                "eec05-apparel-focused-verification");
            actor.GetAbility<AbilityMove>()?.CancelActiveMovement();
        }
        Pause();
        report = Wim003ApparelConditionRuntimeDebugScenarios
            .RunProtectedEec05(scope);
        report["scope"] = "protected current GameplayScene party, real physical/apparel authorities, public condition runtime and seven apparel work-order entries; controlled weather, work context, facilities, and material supply";
        report["fullAuditClosure"] = false;
        report["humanApprovalClaimed"] = false;
        report["trainingEligible"] = false;
    }

    private void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Warning)
            warnings.Add(condition ?? string.Empty);
        else if (type is LogType.Error or LogType.Exception or LogType.Assert)
            errors.Add((condition ?? string.Empty) + "\n"
                + (stackTrace ?? string.Empty));
    }

    private static void Pause()
    {
        GameManager host = FindFirstObjectByType<GameManager>();
        if (host != null)
            host.isPause = true;
        DungeonRuntimeLifetimeScope scope =
            FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        if (scope?.Container != null)
            scope.Container.Resolve<IGameTimeScaleController>().Scale = 0f;
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition)
            throw new InvalidOperationException("EEC05: " + reason);
    }
}
#endif
