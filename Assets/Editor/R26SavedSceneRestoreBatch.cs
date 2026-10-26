using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using DungeonStory.Foundation;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

[InitializeOnLoad]
public static class R26SavedSceneRestoreBatch
{
    private const string ActiveKey = "DungeonStory.R26.ColdSceneRestore";
    internal const string InputPath = "Artifacts/QA/r26-cold-load-input/checkpoint.json";
    internal const string InputSha256 = "3ab8bd4f3a4b5486e127dde6b79ac7e213f4fd93cc426279bc3f49902eb4e903";
    internal const string SlotId = "run-r26-cold-load";

    static R26SavedSceneRestoreBatch() => EditorApplication.playModeStateChanged += OnPlayState;

    public static void RequestForBatch()
    {
        Require(Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification
            && !EditorApplication.isPlayingOrWillChangePlaymode && !SceneManager.GetActiveScene().isDirty,
            "Cold restore requires a fresh isolated batch and a saved scene.");
        Require(Hash(File.ReadAllBytes(InputPath)) == InputSha256, "The real-scene checkpoint bytes changed.");
        Require(!File.Exists(ReportPath), "Cold restore evidence already exists.");
        SessionState.SetBool(ActiveKey, true);
        EditorSceneManager.OpenScene("Assets/Scenes/TitleScene.unity", OpenSceneMode.Single);
        EditorApplication.EnterPlaymode();
    }

    private static void OnPlayState(PlayModeStateChange state)
    {
        if (!SessionState.GetBool(ActiveKey, false)) return;
        if (state == PlayModeStateChange.EnteredPlayMode)
        {
            var host = new GameObject("R26 Cold Saved Scene Restore");
            UnityEngine.Object.DontDestroyOnLoad(host);
            host.AddComponent<R26SavedSceneRestoreRunner>();
        }
        else if (state == PlayModeStateChange.EnteredEditMode)
        {
            SessionState.EraseBool(ActiveKey);
            bool passed = File.Exists(ReportPath)
                && JObject.Parse(File.ReadAllText(ReportPath))["result"]?.Value<string>() == "PASS";
            EditorApplication.Exit(passed ? 0 : 1);
        }
    }

    internal static string ReportPath => Path.Combine(EvidenceDirectory, "cold-scene-restore.json");
    internal static string EvidenceDirectory
    {
        get
        {
            string[] args = Environment.GetCommandLineArgs();
            int at = Array.IndexOf(args, "-logFile");
            Require(at >= 0 && at + 1 < args.Length, "Batch log path is missing.");
            return Path.GetDirectoryName(Path.GetFullPath(args[at + 1]));
        }
    }
    internal static string Hash(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return BitConverter.ToString(sha.ComputeHash(bytes)).Replace("-", "").ToLowerInvariant();
    }
    internal static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}

public sealed class R26SavedSceneRestoreRunner : MonoBehaviour
{
    private readonly List<string> failures = new();
    private readonly List<string> checks = new();
    private readonly List<string> restoreTrace = new();
    private string sourceBefore;
    private DungeonGameSaveData expected;
    private bool restoreStarted;

    private IEnumerator Start()
    {
        Application.logMessageReceived += Observe;
        sourceBefore = V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest();
        IEnumerator run = Run();
        try
        {
            while (true)
            {
                bool next;
                object current;
                try { next = run.MoveNext(); current = next ? run.Current : null; }
                catch (Exception error) { failures.Add(error.ToString()); break; }
                if (!next) break;
                yield return current;
            }
        }
        finally
        {
            (run as IDisposable)?.Dispose();
            DungeonSaveEditorTrace.Observer = null;
            Application.logMessageReceived -= Observe;
        }
        string sourceAfter = V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest();
        if (sourceBefore != sourceAfter) failures.Add("Source changed during cold restore.");
        var report = new JObject
        {
            ["result"] = failures.Count == 0 ? "PASS" : "FAIL",
            ["processId"] = System.Diagnostics.Process.GetCurrentProcess().Id,
            ["producer"] = "r182r26-impact-so-inspector-r26-first-play",
            ["inputSha256"] = R26SavedSceneRestoreBatch.InputSha256,
            ["sourceBefore"] = sourceBefore, ["sourceAfter"] = sourceAfter,
            ["checks"] = new JArray(checks), ["restoreTrace"] = new JArray(restoreTrace),
            ["errors"] = new JArray(failures)
        };
        using (var output = new FileStream(R26SavedSceneRestoreBatch.ReportPath, FileMode.CreateNew))
        using (var writer = new StreamWriter(output)) writer.Write(report.ToString());
        Debug.Log("R26_COLD_SCENE_RESTORE=" + (failures.Count == 0 ? "PASS" : "FAIL")
            + "; fresh Unity process; Title LoadGame; real sealed checkpoint; 104 sections; repeat slot load");
        EditorApplication.ExitPlaymode();
    }

    private IEnumerator Run()
    {
        R26SavedSceneRestoreBatch.Require(DungeonSaveEditorTrace.Observer == null,
            "Cold restore refuses to replace another trace observer.");
        DungeonSaveEditorTrace.Observer = phase =>
        {
            if (phase.StartsWith("restore", StringComparison.Ordinal)) restoreTrace.Add(phase);
            if (phase == "restore.begin") restoreStarted = true;
        };
        expected = JsonUtility.FromJson<DungeonGameSaveData>(File.ReadAllText(R26SavedSceneRestoreBatch.InputPath));
        Check(expected != null && expected.sections.Count == 104
            && DungeonSaveManifest.TryValidate(expected.manifest, expected.sections, out _),
            "REAL_INPUT_SEALED_104_SECTIONS");
        float deadline = Time.realtimeSinceStartup + 120f;
        DungeonTitleLifetimeScope title = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            title = FindFirstObjectByType<DungeonTitleLifetimeScope>();
            if (title?.Container != null) break;
            yield return null;
        }
        Check(title?.Container != null && SceneManager.GetActiveScene().name == DungeonSceneNavigator.TitleSceneName,
            "FRESH_TITLE_SCOPE");
        IDungeonSaveSlotCatalog catalog = title.Container.Resolve<IDungeonSaveSlotCatalog>();
        string slotPath = catalog.GetPath(R26SavedSceneRestoreBatch.SlotId);
        Check(Path.GetFullPath(slotPath).StartsWith(Path.GetFullPath(DungeonPersistencePaths.Root)
            + Path.DirectorySeparatorChar, StringComparison.Ordinal), "ISOLATED_SLOT_LOCATION");
        Directory.CreateDirectory(Path.GetDirectoryName(slotPath));
        Check(!File.Exists(slotPath), "NO_EXISTING_SLOT_OVERWRITE");
        File.Copy(R26SavedSceneRestoreBatch.InputPath, slotPath);
        Check(R26SavedSceneRestoreBatch.Hash(File.ReadAllBytes(slotPath)) == R26SavedSceneRestoreBatch.InputSha256,
            "EXACT_SLOT_BYTES");
        Check(title.Container.Resolve<IDungeonSceneNavigator>().LoadGame(R26SavedSceneRestoreBatch.SlotId),
            "PRODUCTION_TITLE_LOAD_REQUEST");
        DungeonRuntimeLifetimeScope scope = null;
        bool restored = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
            if (scope?.Container != null)
            {
                var manager = FindFirstObjectByType<GameManager>();
                if (manager != null) manager.isPause = true;
                scope.Container.Resolve<IGameTimeScaleController>().Scale = 0f;
                var lineage = scope.Container.Resolve<IGameWorldLineageQuery>().Snapshot;
                if (restoreStarted && lineage?.WorldInstanceId == Section(expected, "foundation.world-lineage")["worldInstanceId"]?.Value<string>())
                { restored = true; break; }
            }
            yield return null;
        }
        Check(restored && SceneManager.GetActiveScene().name == DungeonSceneNavigator.GameplaySceneName,
            "PRODUCTION_SLOT_RESTORED_IN_FRESH_GAMEPLAY");
        Check(!scope.Container.Resolve<IDungeonRestoreSafetyQuery>().RequiresRecovery, "RESTORE_SAFETY_CLEAR");
        var saves = scope.Container.Resolve<IDungeonGameSaveService>();
        VerifyCaptured(saves.Capture(), "COLD_LOAD");
        // A second official load exercises replacement of existing owners rather than adding them.
        bool reloaded = scope.Container.Resolve<IDungeonGameSaveSlotService>()
            .TryLoad(R26SavedSceneRestoreBatch.SlotId, out DungeonGameRestoreReport retry);
        Check(reloaded && retry.Success && retry.WorldPublished && !retry.RequiresRecovery,
            "OFFICIAL_SLOT_LOAD_RETRY:" + retry.FailureDescription);
        yield return null;
        yield return null; // Unity's deferred Destroy must finish before checking live duplicates.
        VerifyCaptured(saves.Capture(), "REPEATED_LOAD");
        string[] liveActors = FindObjectsByType<CharacterActor>(FindObjectsInactive.Include, FindObjectsSortMode.None)
            .Where(actor => actor != null && actor.gameObject.scene.IsValid())
            .Select(actor => actor.Identity?.PersistentId).Where(id => !string.IsNullOrEmpty(id)).ToArray();
        string[] savedActors = Section(expected, "characters.world")["actors"]
            .Select(actor => actor["persistentId"].Value<string>()).OrderBy(id => id, StringComparer.Ordinal).ToArray();
        Check(liveActors.OrderBy(id => id, StringComparer.Ordinal).SequenceEqual(savedActors),
            "LIVE_ACTOR_IDS_EXACT_NO_DUPLICATES");
        Check(R26SavedSceneRestoreBatch.Hash(File.ReadAllBytes(slotPath)) == R26SavedSceneRestoreBatch.InputSha256,
            "INPUT_SAVE_BYTES_PRESERVED");
    }

    private void VerifyCaptured(DungeonGameSaveData actual, string stage)
    {
        Check(actual.sections.Select(section => section.sectionId).OrderBy(id => id, StringComparer.Ordinal)
            .SequenceEqual(expected.sections.Select(section => section.sectionId).OrderBy(id => id, StringComparer.Ordinal)),
            stage + ":SECTION_SET_EXACT");
        Check(JToken.DeepEquals(Section(actual, "foundation.world-lineage"), Section(expected, "foundation.world-lineage")),
            stage + ":WORLD_RUN_LINEAGE_EXACT");
        Check(Section(actual, "foundation.session")["money"].Value<int>()
            == Section(expected, "foundation.session")["money"].Value<int>(), stage + ":MONEY_EXACT");
        Check(StackKeys(actual).SequenceEqual(StackKeys(expected)), stage + ":PHYSICAL_STACK_IDS_ITEMS_QUANTITIES_EXACT");
        Check(JToken.DeepEquals(Section(actual, "research.blueprints"), Section(expected, "research.blueprints")),
            stage + ":RESEARCH_STATE_EXACT");
        JObject wanted = Section(expected, DurableFacilityEquipmentPersistenceContract.SectionId);
        JObject captured = Section(actual, DurableFacilityEquipmentPersistenceContract.SectionId);
        Check(JToken.DeepEquals(captured["slots"], wanted["slots"])
            && JToken.DeepEquals(captured["nextAssignmentSequence"], wanted["nextAssignmentSequence"]),
            stage + ":CLOSED_DRAIN_RECEIPT_EXACT_NO_REDEBIT");
    }

    private static string[] StackKeys(DungeonGameSaveData save) => Section(save, "items.physical")["stacks"]
        .Select(stack => stack["stackId"].Value<string>() + "|" + stack["itemId"].Value<string>()
            + "|" + stack["quantity"].Value<int>()).OrderBy(value => value, StringComparer.Ordinal).ToArray();
    private static JObject Section(DungeonGameSaveData save, string id) =>
        JObject.Parse(save.sections.Single(section => section.sectionId == id).payloadJson);
    private void Check(bool passed, string name)
    {
        R26SavedSceneRestoreBatch.Require(passed, name);
        checks.Add(name);
    }
    private void Observe(string message, string stack, LogType kind)
    {
        if (kind is LogType.Error or LogType.Exception or LogType.Assert) failures.Add(message + "\n" + stack);
    }
}
