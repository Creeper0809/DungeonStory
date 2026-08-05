#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using VContainer;

[InitializeOnLoad]
public static class DungeonFullWorldRoundTripPlayModeFacade
{
    public const string RequestPath =
        "Temp/full-world-round-trip-playmode.request";
    public const string ReportPath =
        "Artifacts/QA/full-world-round-trip-playmode-report.txt";
    public const string GameplayScenePath =
        "Assets/Scenes/GameplayScene.unity";

    private const string PersistenceSnapshotId =
        "full-world-round-trip-playmode";
    private const string EarlyConsoleBufferPath =
        "Temp/full-world-round-trip-playmode-console.buffer";

    static DungeonFullWorldRoundTripPlayModeFacade()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EnsureEarlyConsoleCapture();
    }

    [MenuItem("DungeonStory/QA/Request Full World Round Trip PlayMode")]
    public static void RequestRunFromMenu()
    {
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(ReportPath);
        CleanupTransientArtifacts();
        EnsureEarlyConsoleCapture();
        if (!DungeonFinalPlayModeAcceptanceRequestFacade
                .IsPersistenceCoordinatorActive)
        {
            PlayModeVerificationPersistenceSnapshot.CaptureCurrent(
                PersistenceSnapshotId);
        }
        File.WriteAllText(
            RequestPath,
            DateTime.UtcNow.Ticks.ToString());
        Debug.Log("Full-world PlayMode round-trip request queued.");
    }

    internal static void CleanupTransientArtifacts()
    {
        File.Delete(RequestPath);
        File.Delete(EarlyConsoleBufferPath);
    }

    internal static void StopEarlyCaptureAndDrain(
        ICollection<string> warnings,
        ICollection<string> errors)
    {
        Application.logMessageReceived -= CaptureEarlyConsole;
        if (!File.Exists(EarlyConsoleBufferPath))
        {
            return;
        }

        foreach (string line in File.ReadAllLines(EarlyConsoleBufferPath))
        {
            int separator = line.IndexOf('\t');
            if (separator != 1)
            {
                errors.Add("Malformed early Console buffer entry: " + line);
                continue;
            }
            try
            {
                string value = Encoding.UTF8.GetString(
                    Convert.FromBase64String(line.Substring(separator + 1)));
                if (line[0] == 'W')
                {
                    warnings.Add(value);
                }
                else if (line[0] == 'E')
                {
                    errors.Add(value);
                }
                else
                {
                    errors.Add("Unknown early Console buffer kind: " + line[0]);
                }
            }
            catch (Exception exception)
            {
                errors.Add("Could not decode early Console buffer: " + exception);
            }
        }
        File.Delete(EarlyConsoleBufferPath);
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.ExitingEditMode
            && File.Exists(RequestPath))
        {
            EnsureEarlyConsoleCapture();
        }
    }

    private static void EnsureEarlyConsoleCapture()
    {
        Application.logMessageReceived -= CaptureEarlyConsole;
        Application.logMessageReceived += CaptureEarlyConsole;
    }

    private static void CaptureEarlyConsole(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (!File.Exists(RequestPath)
            || (!EditorApplication.isPlaying
                && !EditorApplication.isPlayingOrWillChangePlaymode))
        {
            return;
        }

        char kind;
        string value;
        if (type == LogType.Warning)
        {
            kind = 'W';
            value = condition ?? string.Empty;
        }
        else if (type is LogType.Error or LogType.Exception or LogType.Assert)
        {
            kind = 'E';
            value = (condition ?? string.Empty) + "\n"
                + (stackTrace ?? string.Empty);
        }
        else
        {
            return;
        }

        try
        {
            Directory.CreateDirectory("Temp");
            string encoded = Convert.ToBase64String(
                Encoding.UTF8.GetBytes(value));
            File.AppendAllText(
                EarlyConsoleBufferPath,
                kind + "\t" + encoded + "\n");
        }
        catch
        {
            // Logging callbacks must never recursively log or throw.
        }
    }

    private static void OnEditorUpdate()
    {
        if (!File.Exists(RequestPath))
        {
            return;
        }

        if (EditorApplication.isPlaying)
        {
            if (UnityEngine.Object.FindFirstObjectByType<
                    DungeonFullWorldRoundTripPlayModeRunner>() == null)
            {
                new GameObject("Full World Round Trip PlayMode Runner")
                    .AddComponent<DungeonFullWorldRoundTripPlayModeRunner>();
            }
            return;
        }

        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        try
        {
            OpenGameplayScene();
            EditorApplication.delayCall += () =>
            {
                if (File.Exists(RequestPath)
                    && !EditorApplication.isPlayingOrWillChangePlaymode)
                {
                    EditorApplication.EnterPlaymode();
                }
            };
        }
        catch (Exception exception)
        {
            WriteEditModeFailure(exception);
        }
    }

    private static void OpenGameplayScene()
    {
        string projectRoot = Directory.GetParent(Application.dataPath)?.FullName
            ?? Application.dataPath;
        string fullPath = Path.Combine(
            projectRoot,
            GameplayScenePath.Replace('/', Path.DirectorySeparatorChar));
        if (!File.Exists(fullPath))
        {
            throw new FileNotFoundException(
                "Full-world round-trip gameplay scene is missing.",
                fullPath);
        }
        if (!string.Equals(
                SceneManager.GetActiveScene().path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.OpenScene(
                GameplayScenePath,
                OpenSceneMode.Single);
        }
    }

    private static void WriteEditModeFailure(Exception exception)
    {
        Directory.CreateDirectory("Artifacts/QA");
        File.WriteAllLines(
            ReportPath,
            new[]
            {
                "RESULT=FAIL",
                "target=FULL_WORLD_ROUND_TRIP",
                "phase=EditMode setup",
                "exception=" + exception
            });
        CleanupTransientArtifacts();
        PlayModeVerificationPersistenceSnapshot.Restore(PersistenceSnapshotId);
        Debug.LogError(
            "Full-world PlayMode round-trip setup failed. " + ReportPath);
    }
}

public sealed class DungeonFullWorldRoundTripPlayModeRunner : MonoBehaviour
{
    private const int ExpectedSectionCount = 54;
    private const float RuntimeReadyTimeoutSeconds = 45f;

    private readonly List<string> warnings = new();
    private readonly List<string> errors = new();

    private void Awake()
    {
        Application.logMessageReceived += CaptureLog;
        DungeonFullWorldRoundTripPlayModeFacade.StopEarlyCaptureAndDrain(
            warnings,
            errors);
    }

    private IEnumerator Start()
    {
        File.Delete(DungeonFullWorldRoundTripPlayModeFacade.RequestPath);
        float deadline = Time.realtimeSinceStartup
            + RuntimeReadyTimeoutSeconds;
        DungeonRuntimeLifetimeScope scope = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            scope = FindObjectsByType<DungeonRuntimeLifetimeScope>(
                    FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .FirstOrDefault(candidate =>
                    candidate != null && candidate.Container != null);
            if (scope != null)
            {
                break;
            }
            yield return null;
        }

        bool passed = false;
        bool baselineRestored = false;
        bool canonicalBaselineMatched = false;
        bool characterProgressionContractsPassed = false;
        string characterProgressionDetail = string.Empty;
        string detail;
        int registeredSections = 0;
        int capturedSections = 0;
        if (scope != null)
        {
            OwnerRunManager ownerManager = FindFirstObjectByType<OwnerRunManager>();
            if (ownerManager == null || ownerManager.CurrentOwnerActor == null)
            {
                StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
                for (int frame = 0; frame < 10; frame++)
                {
                    yield return null;
                }
            }
        }

        try
        {
            if (scope == null)
            {
                throw new InvalidOperationException(
                    "DungeonRuntimeLifetimeScope was not ready before timeout.");
            }

            IDungeonSaveSectionRegistry registry =
                scope.Container.Resolve<IDungeonSaveSectionRegistry>();
            IDungeonGameSaveService saves =
                scope.Container.Resolve<IDungeonGameSaveService>();
            registeredSections = registry.OrderedSections.Count;
            DungeonGameSaveData baseline = saves.Capture();
            capturedSections = baseline.sections?.Count ?? 0;
            if (registeredSections != ExpectedSectionCount
                || capturedSections != ExpectedSectionCount)
            {
                throw new InvalidOperationException(
                    "Live save manifest did not contain exactly "
                    + $"{ExpectedSectionCount} sections: registered="
                    + $"{registeredSections}; captured={capturedSections}.");
            }

            string baselineCanonical = Canonicalize(baseline);
            try
            {
                characterProgressionContractsPassed =
                    CharacterProgressionSavePlayModeFacade.Run(
                        out characterProgressionDetail);
                if (!characterProgressionContractsPassed)
                {
                    errors.Add(
                        "Character progression save contracts failed: "
                        + characterProgressionDetail);
                }
            }
            catch (Exception exception)
            {
                characterProgressionDetail = exception.ToString();
                errors.Add(
                    "Character progression save contracts threw: "
                    + exception);
            }

            bool roundTripReturned = false;
            try
            {
                DungeonGameSaveDebugScenarios.RunFullGameRoundTrip();
                roundTripReturned = true;
            }
            catch (Exception exception)
            {
                errors.Add(exception.ToString());
            }

            DungeonGameSaveData afterRoundTrip = saves.Capture();
            int postRoundTripSections = afterRoundTrip.sections?.Count ?? 0;
            if (postRoundTripSections != ExpectedSectionCount)
            {
                errors.Add(
                    "Baseline restoration changed the live save-section count: "
                    + postRoundTripSections + ".");
            }
            canonicalBaselineMatched = string.Equals(
                baselineCanonical,
                Canonicalize(afterRoundTrip),
                StringComparison.Ordinal);
            if (canonicalBaselineMatched)
            {
                baselineRestored = true;
            }
            else
            {
                errors.Add(
                    "Full-world scenario did not restore its canonical baseline.");
                bool recoverySucceeded = saves.TryRestore(
                    baseline,
                    out DungeonGameRestoreReport recoveryReport);
                baselineRestored = recoverySucceeded
                    && string.Equals(
                        baselineCanonical,
                        Canonicalize(saves.Capture()),
                        StringComparison.Ordinal);
                if (!baselineRestored)
                {
                    errors.Add(
                        "Explicit baseline recovery failed: "
                        + string.Join(" | ", recoveryReport.Errors));
                }
            }

            passed = characterProgressionContractsPassed
                && roundTripReturned
                && canonicalBaselineMatched
                && baselineRestored
                && warnings.Count == 0
                && errors.Count == 0;
            detail = passed
                ? "Live 54-section full-world round trip and baseline restoration passed."
                : "The round trip emitted Console warnings or errors.";
        }
        catch (Exception exception)
        {
            errors.Add(exception.ToString());
            detail = "Full-world round trip threw an exception.";
        }

        Finish(
            passed,
            detail,
            registeredSections,
            capturedSections,
            baselineRestored,
            canonicalBaselineMatched,
            characterProgressionContractsPassed,
            characterProgressionDetail);
    }

    private void Finish(
        bool passed,
        string detail,
        int registeredSections,
        int capturedSections,
        bool baselineRestored,
        bool canonicalBaselineMatched,
        bool characterProgressionContractsPassed,
        string characterProgressionDetail)
    {
        Application.logMessageReceived -= CaptureLog;
        DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
        Directory.CreateDirectory("Artifacts/QA");
        List<string> report = new()
        {
            passed ? "RESULT=PASS" : "RESULT=FAIL",
            "target=FULL_WORLD_ROUND_TRIP",
            "scene=" + DungeonFullWorldRoundTripPlayModeFacade.GameplayScenePath,
            "registeredSections=" + registeredSections,
            "capturedSections=" + capturedSections,
            "baselineRestored=" + baselineRestored,
            "canonicalBaselineMatched=" + canonicalBaselineMatched,
            "characterProgressionContractsPassed="
                + characterProgressionContractsPassed,
            "characterProgressionDetail="
                + (characterProgressionDetail ?? string.Empty),
            "consoleWarnings=" + warnings.Count,
            "consoleErrors=" + errors.Count,
            "detail=" + detail,
            "completedUtc=" + DateTime.UtcNow.ToString("O")
        };
        report.AddRange(warnings.Select(value => "[WARNING] " + value));
        report.AddRange(errors.Select(value => "[ERROR] " + value));
        File.WriteAllLines(
            DungeonFullWorldRoundTripPlayModeFacade.ReportPath,
            report);

        string summary = report[0] + "; report="
            + DungeonFullWorldRoundTripPlayModeFacade.ReportPath;
        if (passed)
        {
            Debug.Log(summary);
        }
        else
        {
            Debug.LogError(summary);
        }

        Destroy(gameObject);
        EditorApplication.ExitPlaymode();
    }

    private void CaptureLog(
        string condition,
        string stackTrace,
        LogType type)
    {
        if (type == LogType.Warning)
        {
            warnings.Add(condition);
        }
        else if (type is LogType.Error or LogType.Exception or LogType.Assert)
        {
            errors.Add(condition + "\n" + stackTrace);
        }
    }

    private void OnDestroy()
    {
        Application.logMessageReceived -= CaptureLog;
    }

    private static string Canonicalize(DungeonGameSaveData save)
    {
        StringBuilder builder = new StringBuilder();
        builder.Append(save?.version ?? 0).Append('\n');
        AppendField(builder, save?.sceneName);
        foreach (DungeonSaveSectionEnvelope section in
                 save?.sections?
                     .Where(candidate => candidate != null)
                     .OrderBy(candidate => candidate.sectionId, StringComparer.Ordinal)
                 ?? Enumerable.Empty<DungeonSaveSectionEnvelope>())
        {
            AppendField(builder, section.sectionId);
            builder.Append(section.sectionVersion).Append('\n');
            builder.Append((int)section.restorePhase).Append('\n');
            builder.Append(section.optional ? '1' : '0').Append('\n');
            AppendField(builder, section.payloadJson);
        }
        return builder.ToString();
    }

    private static void AppendField(StringBuilder builder, string value)
    {
        string normalized = value ?? string.Empty;
        builder.Append(normalized.Length).Append(':').Append(normalized)
            .Append('\n');
    }
}
#endif
