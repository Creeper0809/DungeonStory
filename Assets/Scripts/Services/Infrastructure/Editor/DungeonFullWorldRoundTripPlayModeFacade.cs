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
            bool gameplaySceneWasAlreadyOpen = string.Equals(
                SceneManager.GetActiveScene().path,
                GameplayScenePath,
                StringComparison.OrdinalIgnoreCase);
            OpenGameplayScene();
            if (!gameplaySceneWasAlreadyOpen)
            {
                // Opening a scene and entering PlayMode in the same editor update
                // can deadlock Unity's scene transition. The next update observes
                // the authored gameplay scene and performs the mode transition.
                return;
            }
            if (File.Exists(RequestPath)
                && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                EditorApplication.EnterPlaymode();
            }
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
        int postRoundTripSections = 0;
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
            if (!scope.Container.Resolve<IOwnerRunManagerProvider>()
                    .TryGetManager(out OwnerRunManager baselineOwnerManager)
                || baselineOwnerManager.CurrentOwnerActor == null)
            {
                throw new InvalidOperationException(
                    "Full-world baseline requires an initialized owner actor.");
            }
            // Body-health currently materializes its aggregate entry lazily on
            // the first actor query. Establish that canonical state before the
            // outer baseline so the nested progression contract cannot change
            // the baseline merely by performing its own first query.
            scope.Container.Resolve<ICharacterBodyHealthQuery>()
                .GetSnapshot(baselineOwnerManager.CurrentOwnerActor);
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

            DungeonGameSaveData afterCharacterContracts = saves.Capture();
            bool characterContractsPreservedBaseline = string.Equals(
                baselineCanonical,
                Canonicalize(afterCharacterContracts),
                StringComparison.Ordinal);
            if (!characterContractsPreservedBaseline)
            {
                errors.Add(
                    "Character progression save contracts changed the full-world "
                    + "baseline: "
                    + DescribeSaveDifferences(
                        baseline,
                        afterCharacterContracts));
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
            postRoundTripSections = afterRoundTrip.sections?.Count ?? 0;
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
                    "Full-world scenario did not restore its canonical baseline: "
                    + DescribeSaveDifferences(baseline, afterRoundTrip));
                bool recoverySucceeded = saves.TryRestore(
                    baseline,
                    out DungeonGameRestoreReport recoveryReport);
                DungeonGameSaveData afterExplicitRecovery = saves.Capture();
                bool recoveryCanonicalMatched = string.Equals(
                    baselineCanonical,
                    Canonicalize(afterExplicitRecovery),
                    StringComparison.Ordinal);
                baselineRestored = recoverySucceeded
                    && recoveryCanonicalMatched;
                if (!baselineRestored)
                {
                    errors.Add(
                        "Explicit baseline recovery failed: returned="
                        + recoverySucceeded
                        + "; canonicalMatched="
                        + recoveryCanonicalMatched
                        + "; errors="
                        + string.Join(" | ", recoveryReport.Errors)
                        + "; differences="
                        + DescribeSaveDifferences(
                            baseline,
                            afterExplicitRecovery));
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
            postRoundTripSections,
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
        int postRoundTripSections,
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
            "postRoundTripSections=" + postRoundTripSections,
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

    private static string DescribeSaveDifferences(
        DungeonGameSaveData expected,
        DungeonGameSaveData actual)
    {
        Dictionary<string, DungeonSaveSectionEnvelope> expectedById =
            (expected?.sections ?? new List<DungeonSaveSectionEnvelope>())
            .Where(section => section != null)
            .GroupBy(section => section.sectionId ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        Dictionary<string, DungeonSaveSectionEnvelope> actualById =
            (actual?.sections ?? new List<DungeonSaveSectionEnvelope>())
            .Where(section => section != null)
            .GroupBy(section => section.sectionId ?? string.Empty, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        List<string> differences = new List<string>();
        foreach (string sectionId in expectedById.Keys
                     .Union(actualById.Keys, StringComparer.Ordinal)
                     .OrderBy(id => id, StringComparer.Ordinal))
        {
            if (!expectedById.TryGetValue(
                    sectionId,
                    out DungeonSaveSectionEnvelope expectedSection))
            {
                differences.Add("added:" + sectionId);
                continue;
            }
            if (!actualById.TryGetValue(
                    sectionId,
                    out DungeonSaveSectionEnvelope actualSection))
            {
                differences.Add("removed:" + sectionId);
                continue;
            }
            if (!string.Equals(
                    expectedSection.payloadJson ?? string.Empty,
                    actualSection.payloadJson ?? string.Empty,
                    StringComparison.Ordinal))
            {
                string expectedPayload = expectedSection.payloadJson
                    ?? string.Empty;
                string actualPayload = actualSection.payloadJson
                    ?? string.Empty;
                differences.Add(
                    "changed:"
                    + sectionId
                    + "[length "
                    + expectedPayload.Length
                    + "->"
                    + actualPayload.Length
                    + "; "
                    + DescribeFirstTextDifference(
                        expectedPayload,
                        actualPayload)
                    + "]");
            }
        }

        return differences.Count == 0
            ? "no section payload difference"
            : string.Join(" | ", differences);
    }

    private static string DescribeFirstTextDifference(
        string expected,
        string actual)
    {
        int sharedLength = Math.Min(expected.Length, actual.Length);
        int index = 0;
        while (index < sharedLength && expected[index] == actual[index])
        {
            index++;
        }

        int start = Math.Max(0, index - 48);
        int expectedLength = Math.Min(112, expected.Length - start);
        int actualLength = Math.Min(112, actual.Length - start);
        string expectedSnippet = expected.Substring(start, expectedLength)
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
        string actualSnippet = actual.Substring(start, actualLength)
            .Replace("\r", "\\r")
            .Replace("\n", "\\n");
        return "firstDiff="
            + index
            + "; expected='"
            + expectedSnippet
            + "'; actual='"
            + actualSnippet
            + "'";
    }

    private static void AppendField(StringBuilder builder, string value)
    {
        string normalized = value ?? string.Empty;
        builder.Append(normalized.Length).Append(':').Append(normalized)
            .Append('\n');
    }
}
#endif
