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
    public static string ReportPath => DungeonPersistencePaths.IsIsolatedVerification
        ? Path.Combine(Path.GetDirectoryName(DungeonPersistencePaths.Root), "full-world-report.txt")
        : "Artifacts/QA/full-world-round-trip-playmode-report.txt";
    public const string GameplayScenePath =
        "Assets/Scenes/GameplayScene.unity";

    private const string PersistenceSnapshotId =
        "full-world-round-trip-playmode";
    private const string EarlyConsoleBufferPath =
        "Temp/full-world-round-trip-playmode-console.buffer";
    private static bool isolatedLiveNarrativeRunnerActive;

    internal static void SetIsolatedLiveNarrativeRunnerActive(bool active)
    {
        isolatedLiveNarrativeRunnerActive = active
            && Application.isBatchMode
            && DungeonPersistencePaths.IsIsolatedVerification;
    }

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
        if (DungeonPersistencePaths.IsIsolatedVerification)
            throw new InvalidOperationException("Use the create-only isolated batch entry.");
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

    public static void RequestIsolatedBatch()
    {
        if (!DungeonPersistencePaths.IsIsolatedVerification)
            throw new InvalidOperationException("Explicit isolated batch persistence is required.");
        if (Directory.Exists("Temp/playmode-persistence-snapshots")
            || File.Exists(RequestPath) || File.Exists(EarlyConsoleBufferPath)
            || File.Exists(ReportPath))
            throw new InvalidOperationException("Existing verification state must be preserved.");
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        Directory.CreateDirectory("Temp");
        U1FullWorldOwnerBindingVerification.VerifyIsolationConfiguration();
        WildlifeAiHuntPlayModeVerifier.PrepareIsolatedBackgroundNarrativeDeferral();
        File.WriteAllText(RequestPath, DateTime.UtcNow.Ticks.ToString());
        EnsureEarlyConsoleCapture();
        Debug.Log("Isolated full-world verification queued; player persistence is excluded.");
    }

    public static void RequestInjuredReturneeSaveRegression()
    {
        if (!Application.isBatchMode || !DungeonPersistencePaths.IsIsolatedVerification)
            throw new InvalidOperationException("Injured visitor save regression requires isolated batch persistence.");
        Environment.SetEnvironmentVariable("DUNGEONSTORY_INJURED_RETURNEE_SAVE_REGRESSION", "1");
        RequestIsolatedBatch();
    }

    public static void RequestExteriorDepartureExposureRegression()
    {
        if (!Application.isBatchMode || !DungeonPersistencePaths.IsIsolatedVerification)
            throw new InvalidOperationException("Exterior departure regression requires isolated batch persistence.");
        Environment.SetEnvironmentVariable("DUNGEONSTORY_EXTERIOR_DEPARTURE_EXPOSURE_REGRESSION", "1");
        RequestIsolatedBatch();
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
        if (change is PlayModeStateChange.ExitingPlayMode
            or PlayModeStateChange.EnteredEditMode)
        {
            isolatedLiveNarrativeRunnerActive = false;
        }
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
        // The live runner removes RequestPath once it owns the run. Keep its
        // normal PlayerLoop awake without advancing time or activating a window.
        if (isolatedLiveNarrativeRunnerActive
            && EditorApplication.isPlaying
            && Application.isBatchMode
            && DungeonPersistencePaths.IsIsolatedVerification)
        {
            EditorApplication.QueuePlayerLoopUpdate();
        }
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
        if (!DungeonPersistencePaths.IsIsolatedVerification)
            PlayModeVerificationPersistenceSnapshot.Restore(PersistenceSnapshotId);
        Debug.LogError(
            "Full-world PlayMode round-trip setup failed. " + ReportPath);
        if (Application.isBatchMode && DungeonPersistencePaths.IsIsolatedVerification)
            EditorApplication.Exit(1);
    }
}

public sealed class DungeonFullWorldRoundTripPlayModeRunner : MonoBehaviour
{
    private const float RuntimeReadyTimeoutSeconds = 45f;

    private readonly List<string> warnings = new();
    private readonly List<string> errors = new();
    private CharacterSpawner pausedSpawner;
    private bool spawnerWasPaused;

    private void Awake()
    {
        Application.logMessageReceived += CaptureLog;
        DungeonFullWorldRoundTripPlayModeFacade.StopEarlyCaptureAndDrain(
            warnings,
            errors);
    }

    private IEnumerator Start()
    {
        if (string.Equals(Environment.GetEnvironmentVariable(
                "DUNGEONSTORY_U0_BDCD01_DOOR_TRAVERSAL_VERIFY"), "1", StringComparison.Ordinal))
        {
            yield return DoorHeldOpenOutcomeDebugScenarios.VerifyTraversalPlayMode(warnings, errors);
            Application.logMessageReceived -= CaptureLog;
            yield break;
        }
        if (string.Equals(Environment.GetEnvironmentVariable(
                "DUNGEONSTORY_EXTERIOR_DEPARTURE_EXPOSURE_REGRESSION"), "1", StringComparison.Ordinal))
        {
            yield return ExteriorActivityDebugScenarios.VerifyDepartureExposureSave(warnings, errors);
            Application.logMessageReceived -= CaptureLog;
            yield break;
        }
        if (string.Equals(Environment.GetEnvironmentVariable(
                "DUNGEONSTORY_INJURED_RETURNEE_SAVE_REGRESSION"), "1", StringComparison.Ordinal))
        {
            yield return ExteriorActivityDebugScenarios.VerifyInjuredReturneeSave(warnings, errors);
            Application.logMessageReceived -= CaptureLog;
            yield break;
        }
        if (string.Equals(Environment.GetEnvironmentVariable(
                "DUNGEONSTORY_TERMINAL_DEATH_SAVE_VERIFY"), "1", StringComparison.Ordinal))
        {
            yield return TerminalDeathSavePlayModeVerifier.Verify(warnings, errors);
            Application.logMessageReceived -= CaptureLog;
            yield break;
        }
        if (CharacterVisitorControlJourneyPlayModeVerifier.ExitOnlyRequested
            || CharacterVisitorControlJourneyPlayModeVerifier
                .VandalismOnlyRequested)
        {
            CharacterVisitorControlJourneyPlayModeVerifier
                .StartIsolatedFromFacade(warnings, errors);
            Application.logMessageReceived -= CaptureLog;
            yield break;
        }
        if (CharacterAiSelfCarePlayModeVerifier.A09SafeDrinkRequested)
        {
            CharacterAiSelfCarePlayModeVerifier.StartIsolatedFromFacade(
                warnings,
                errors);
            Application.logMessageReceived -= CaptureLog;
            yield break;
        }
        if (CharacterDeprivationProductionBtPlayModeVerifier
                .VandalismOnlyRequested
            || CharacterDeprivationProductionBtPlayModeVerifier
                .FullMatrixRequested
            || CharacterDeprivationProductionBtPlayModeVerifier.CorpseEatingOnlyRequested)
        {
            CharacterDeprivationProductionBtPlayModeVerifier
                .StartIsolatedFromFacade(warnings, errors);
            Application.logMessageReceived -= CaptureLog;
            yield break;
        }
        if (string.Equals(Environment.GetEnvironmentVariable(
                "DUNGEONSTORY_U0_G12_GOLEM_UPKEEP_VERIFY"),
                "1",
                StringComparison.Ordinal))
        {
            yield return G12GolemUpkeepPlayModeVerifier.Verify(
                warnings,
                errors);
            Application.logMessageReceived -= CaptureLog;
            yield break;
        }
        if (string.Equals(Environment.GetEnvironmentVariable(
                "DUNGEONSTORY_U0_E01_REGISTRY_INVENTORY_VERIFY"),
                "1",
                StringComparison.Ordinal))
        {
            yield return E01GameplayOutcomeRegistryPlayModeVerifier.Verify(
                warnings,
                errors);
            Application.logMessageReceived -= CaptureLog;
            yield break;
        }
        if (string.Equals(Environment.GetEnvironmentVariable(
                "DUNGEONSTORY_U0_E09_ENVIRONMENT_EXPOSURE_VERIFY"),
                "1",
                StringComparison.Ordinal))
        {
            yield return E09EnvironmentExposurePlayModeVerifier.Verify(
                warnings,
                errors);
            Application.logMessageReceived -= CaptureLog;
            yield break;
        }
        if (string.Equals(Environment.GetEnvironmentVariable(
                "DUNGEONSTORY_U0_E11_WILDLIFE_VERIFY"),
                "1",
                StringComparison.Ordinal))
        {
            yield return WildlifeAiHuntPlayModeVerifier.Verify(
                warnings,
                errors);
            Application.logMessageReceived -= CaptureLog;
            yield break;
        }
        if (string.Equals(Environment.GetEnvironmentVariable("DUNGEONSTORY_EEC03_DRIFT_CARGO_VERIFY"), "1", StringComparison.Ordinal))
        {
            Wim009SeasonalDriftCargoNaturalPlayModeVerifier.StartFocused(
                warnings,
                errors);
            Application.logMessageReceived -= CaptureLog;
            yield break;
        }
        if (string.Equals(Environment.GetEnvironmentVariable("DUNGEONSTORY_EEC05_APPAREL_VERIFY"), "1", StringComparison.Ordinal))
        {
            Eec05ApparelPlayModeVerifier.StartFocused(warnings, errors);
            Application.logMessageReceived -= CaptureLog;
            yield break;
        }
        if (CharacterAiWorkTypeLiveMatrixPlayModeVerifier.IsW03MedicalLaborVerification)
        {
            CharacterAiWorkTypeLiveMatrixPlayModeRunner verifier =
                new GameObject("W03 Medical Labor PlayMode Runner")
                    .AddComponent<CharacterAiWorkTypeLiveMatrixPlayModeRunner>();
            verifier.W03MedicalLaborOnly = true;
            verifier.AddInitialConsoleIssues(warnings, errors);
            Application.logMessageReceived -= CaptureLog;
            yield break;
        }
        if (CharacterAiWorkTypeLiveMatrixPlayModeVerifier.IsW04RepairLiveVerification)
        {
            CharacterAiWorkTypeLiveMatrixPlayModeRunner verifier =
                new GameObject("W04 Repair Live PlayMode Runner")
                    .AddComponent<CharacterAiWorkTypeLiveMatrixPlayModeRunner>();
            verifier.W04RepairLiveOnly = true;
            verifier.AddInitialConsoleIssues(warnings, errors);
            Application.logMessageReceived -= CaptureLog;
            yield break;
        }
        if (CharacterAiWorkTypeLiveMatrixPlayModeVerifier.IsW06ResourceCausalityVerification)
        {
            CharacterAiWorkTypeLiveMatrixPlayModeRunner verifier =
                new GameObject("W06 Resource Causality PlayMode Runner")
                    .AddComponent<CharacterAiWorkTypeLiveMatrixPlayModeRunner>();
            verifier.W06ResourceCausalityOnly = true;
            verifier.AddInitialConsoleIssues(warnings, errors);
            Application.logMessageReceived -= CaptureLog;
            yield break;
        }
        if (CharacterAiWorkTypeLiveMatrixPlayModeVerifier.IsW01WorkTerminalJoinVerification)
        {
            CharacterAiWorkTypeLiveMatrixPlayModeRunner verifier =
                new GameObject("W01 Work Terminal Join PlayMode Runner")
                    .AddComponent<CharacterAiWorkTypeLiveMatrixPlayModeRunner>();
            verifier.W01WorkTerminalJoinOnly = true;
            verifier.AddInitialConsoleIssues(warnings, errors);
            Application.logMessageReceived -= CaptureLog;
            yield break;
        }
        if (string.Equals(Environment.GetEnvironmentVariable("DUNGEONSTORY_W05_HARVEST_IDENTITY_VERIFY"), "1", StringComparison.Ordinal))
        {
            CharacterAiWorkTypeLiveMatrixPlayModeRunner verifier =
                new GameObject("W05 Harvest Identity PlayMode Runner")
                    .AddComponent<CharacterAiWorkTypeLiveMatrixPlayModeRunner>();
            verifier.W05HarvestIdentityOnly = true;
            verifier.AddInitialConsoleIssues(warnings, errors);
            Application.logMessageReceived -= CaptureLog;
            yield break;
        }
        if (string.Equals(Environment.GetEnvironmentVariable("DUNGEONSTORY_W05_SURGERY_IDENTITY_VERIFY"), "1", StringComparison.Ordinal))
        {
            SurgeryPlayModeVerificationRunner verifier =
                new GameObject("W05 Surgery Identity PlayMode Runner")
                    .AddComponent<SurgeryPlayModeVerificationRunner>();
            verifier.AddInitialConsoleIssues(warnings, errors);
            Application.logMessageReceived -= CaptureLog;
            yield break;
        }
        if (string.Equals(Environment.GetEnvironmentVariable("DUNGEONSTORY_PERSISTENT_COOK_PAUSE_VERIFY"), "1", StringComparison.Ordinal))
        {
            CharacterAiWorkTypeLiveMatrixPlayModeRunner verifier =
                new GameObject("Persistent Cook Pause PlayMode Runner")
                    .AddComponent<CharacterAiWorkTypeLiveMatrixPlayModeRunner>();
            verifier.P15AutomationModesOnly = true;
            verifier.AddInitialConsoleIssues(warnings, errors);
            Application.logMessageReceived -= CaptureLog;
            yield break;
        }
        if (string.Equals(Environment.GetEnvironmentVariable("DUNGEONSTORY_MEAL_CONTINUATION_VERIFY"), "1", StringComparison.Ordinal))
        {
            Application.logMessageReceived -= CaptureLog;
            yield return new MealUseContinuationPlayModeRunner(warnings, errors).Run();
            yield break;
        }
        if (string.Equals(Environment.GetEnvironmentVariable("DUNGEONSTORY_DRINK_CONTINUATION_VERIFY"), "1", StringComparison.Ordinal))
        {
            Application.logMessageReceived -= CaptureLog;
            yield return new MealUseContinuationPlayModeRunner(warnings, errors).Run();
            yield break;
        }
        if (string.Equals(Environment.GetEnvironmentVariable("DUNGEONSTORY_SHOP_CONTINUATION_VERIFY"), "1", StringComparison.Ordinal))
        {
            Application.logMessageReceived -= CaptureLog;
            yield return new ShopUseContinuationPlayModeRunner(warnings, errors).Run();
            yield break;
        }
        if (string.Equals(Environment.GetEnvironmentVariable("DUNGEONSTORY_MEDICAL_CONTINUATION_VERIFY"), "1", StringComparison.Ordinal))
        {
            Application.logMessageReceived -= CaptureLog;
            yield return new MedicalTreatmentContinuationPlayModeRunner(warnings, errors).Run();
            yield break;
        }
        if (string.Equals(Environment.GetEnvironmentVariable("DUNGEONSTORY_FACILITY_CONTINUATION_VERIFY"), "1", StringComparison.Ordinal))
        {
            Application.logMessageReceived -= CaptureLog;
            yield return new FacilityUseContinuationPlayModeRunner(warnings, errors).Run();
            yield break;
        }
        if (string.Equals(Environment.GetEnvironmentVariable("DUNGEONSTORY_LIVE_NARRATIVE_CAPTURE"), "1", StringComparison.Ordinal))
        {
            Application.logMessageReceived -= CaptureLog;
            DungeonFullWorldRoundTripPlayModeFacade
                .SetIsolatedLiveNarrativeRunnerActive(true);
            try
            {
                yield return new DungeonLiveNarrativeCampaignRunner(warnings, errors).Run();
            }
            finally
            {
                DungeonFullWorldRoundTripPlayModeFacade
                    .SetIsolatedLiveNarrativeRunnerActive(false);
            }
            yield break;
        }
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
            ICharacterSpawnerProvider spawnerProvider =
                scope.Container.Resolve<ICharacterSpawnerProvider>();
            if (spawnerProvider.TryGetSpawner(out pausedSpawner)
                && pausedSpawner != null)
            {
                spawnerWasPaused = pausedSpawner
                    .DeterministicSimulationPausedForDiagnostics;
                pausedSpawner.ConfigureDeterministicSimulationForDiagnostics(
                    paused: true);
            }

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

            if (!StartPartyPreparationPlayModeVerifier
                    .TryReconcileTierZeroForDirectGameplayFixture(
                        scope,
                        out string tierZeroDetail))
            {
                throw new InvalidOperationException(
                    "Full-world direct-entry Tier-0 reconciliation failed: "
                    + tierZeroDetail);
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
            int removedOrphanExposures = DungeonGameSaveDebugScenarios
                .RemoveDanglingPopulationHealthExposuresForDebug();
            if (removedOrphanExposures > 0)
            {
                Debug.Log(
                    "Full-world QA removed "
                    + $"{removedOrphanExposures} orphan disease-exposure records "
                    + "left by prior editor fixtures before capturing its baseline.");
            }
            // Body-health currently materializes its aggregate entry lazily on
            // the first actor query. Establish that canonical state before the
            // outer baseline so the nested progression contract cannot change
            // the baseline merely by performing its own first query.
            scope.Container.Resolve<ICharacterBodyHealthQuery>()
                .GetSnapshot(baselineOwnerManager.CurrentOwnerActor);
            DungeonGameSaveData baseline = saves.Capture();
            capturedSections = baseline.sections?.Count ?? 0;
            if (registeredSections <= 0
                || capturedSections != registeredSections)
            {
                throw new InvalidOperationException(
                    "Live save manifest and captured save-section counts differ: registered="
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

            if (DungeonPersistencePaths.IsIsolatedVerification)
            {
                U1FullWorldOwnerBindingVerification.Run(scope, saves);
                U1FullWorldOwnerBindingVerification.RunTrait(scope, saves);
                U1FullWorldOwnerBindingVerification.RunPrisonerIdentity(scope, saves);
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
            if (postRoundTripSections != registeredSections)
            {
                errors.Add(
                    "Baseline restoration changed the live save-section count: "
                    + $"registered={registeredSections}; "
                    + $"captured={postRoundTripSections}.");
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

            if (DungeonPersistencePaths.IsIsolatedVerification)
                U1FullWorldOwnerBindingVerification.VerifyDurableAutosave(scope);

            passed = characterProgressionContractsPassed
                && roundTripReturned
                && canonicalBaselineMatched
                && baselineRestored
                && warnings.Count == 0
                && errors.Count == 0;
            detail = passed
                ? $"Live {registeredSections}-section full-world round trip and baseline restoration passed."
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
        if (pausedSpawner != null)
        {
            pausedSpawner.ConfigureDeterministicSimulationForDiagnostics(
                spawnerWasPaused);
        }
        DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
        Directory.CreateDirectory("Artifacts/QA");
        List<string> report = new()
        {
            passed ? "RESULT=PASS" : "RESULT=FAIL",
            "target=FULL_WORLD_ROUND_TRIP",
            "isolatedPersistence=" + DungeonPersistencePaths.IsIsolatedVerification,
            "setup=controlled-start-party-fixture; not-training-source",
            "humanApprovalClaimed=false",
            "trainingEligible=false",
            "scene=" + DungeonFullWorldRoundTripPlayModeFacade.GameplayScenePath,
            "registeredSections=" + registeredSections,
            "capturedSections=" + capturedSections,
            "postRoundTripSections=" + postRoundTripSections,
            "sectionCoverageMatched=" + (registeredSections > 0
                && capturedSections == registeredSections
                && postRoundTripSections == registeredSections),
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
        bool ownedByUnityTestRunner = Environment.GetCommandLineArgs()
            .Any(argument => string.Equals(
                argument,
                "-runTests",
                StringComparison.OrdinalIgnoreCase));
        if (ownedByUnityTestRunner)
        {
            return;
        }
        EditorApplication.ExitPlaymode();
        if (Application.isBatchMode)
        {
            EditorApplication.delayCall += () =>
                EditorApplication.Exit(passed ? 0 : 1);
        }
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
            AppendField(builder, CanonicalizeSectionPayload(section));
        }
        return builder.ToString();
    }

    private static string CanonicalizeSectionPayload(
        DungeonSaveSectionEnvelope section)
    {
        if (!string.Equals(
                section.sectionId,
                GameplayOutcomeLedgerSaveSection.Id,
                StringComparison.Ordinal))
        {
            return section.payloadJson;
        }

        // A successful load advances the ledger epoch so stale workers cannot
        // publish into the restored world. It is deliberately volatile and is
        // not part of the durable-state equality asserted by this verifier.
        GameplayOutcomeLedgerSaveData ledger = JsonUtility.FromJson<
            GameplayOutcomeLedgerSaveData>(section.payloadJson);
        ledger.worldEpoch = 0L;
        foreach (GameplayOutcomeConsolidationJobSnapshot job in
                 ledger.consolidationJobs
                 ?? new List<GameplayOutcomeConsolidationJobSnapshot>())
        {
            if (job != null)
                job.worldEpoch = 0L;
        }
        return JsonUtility.ToJson(ledger);
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
