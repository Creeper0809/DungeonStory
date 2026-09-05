#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Foundation;
using DungeonStory.Rooms;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

[InitializeOnLoad]
public static class PhysicalItemLogisticsPlayModeVerifier
{
    public const string RequestPath = "Temp/physical-item-logistics-playmode.request";
    public const string ReportPath = "Artifacts/QA/physical-item-logistics-playmode-report.txt";
    public const string ConstructionRequestPath = "Temp/construction-project-playmode.request";
    public const string ConstructionReportPath = "Artifacts/QA/construction-project-playmode-report.txt";
    public const string L02RequestPath = "Temp/l02-mass-admission-playmode.request";
    public const string L02ReportPath = "Artifacts/QA/l02-mass-admission-playmode-report.txt";
    public const string ProductionInputMassRequestPath =
        "Temp/production-input-buffer-mass-playmode.request";
    public const string ProductionInputMassReportPath =
        "Artifacts/QA/production-input-buffer-mass-playmode-report.txt";
    public const string EquipmentRepairRequestPath =
        "Temp/equipment-repair-buffer-mass-playmode.request";
    public const string EquipmentRepairReportPath =
        "Artifacts/QA/equipment-repair-buffer-mass-playmode-report.txt";
    public const string PreparedOutputWarehouseRequestPath =
        "Temp/prepared-output-warehouse-live-playmode.request";
    public const string NaturalOutputPortfolioRequestPath =
        "Temp/v27-production-output-clearance-natural-portfolio.request";
    public const string NaturalOutputPortfolioRunnerReportPath =
        "Artifacts/QA/v27-production-output-clearance-natural-portfolio-runner.txt";
    private const string NaturalOutputPortfolioRequestToken =
        "v27-natural-output-portfolio@2";
    public const string PreparedOutputWarehouseReportPath =
        "Artifacts/QA/prepared-output-warehouse-live-playmode-report.txt";
    public const string P17PreparedOutputWarehouseReportPath =
        "Artifacts/QA/prepared-output-p17-live-playmode-report.txt";
    public const string P17OutputClearanceFocusedReportPath =
        "Artifacts/QA/v27-production-output-clearance-natural-focused.txt";
    public const string P17OutputClearance32SeedReportPath =
        "Artifacts/QA/v27-production-output-clearance-natural-32-seed.txt";
    public const string P17OutputClearance32SeedCsvPath =
        "Artifacts/QA/v27-production-output-clearance-natural-32-seed.csv";
    public const string CropOutputClearanceFocusedReportPath =
        "Artifacts/QA/v27-production-output-clearance-crop-focused.txt";
    public const string SawmillPreparedOutputWarehouseReportPath =
        "Artifacts/QA/prepared-output-sawmill-live-playmode-report.txt";
    public const string M06PreparedOutputWarehouseReportPath =
        "Artifacts/QA/prepared-output-m06-live-playmode-report.txt";
    public const string DestructiveDrainPreparedOutputReportPath =
        "Artifacts/QA/prepared-output-destructive-drain-live-playmode-report.txt";
    public const string CarryCapturePath = "Artifacts/QA/physical-item-carry-ui.png";
    private const string TitleScenePath = "Assets/Scenes/TitleScene.unity";
    private const string PersistenceSnapshotId =
        "physical-item-logistics-playmode";
    private const string StartSceneLeaseOwnerId =
        "qa:physical-item-logistics-playmode";
    private const string PersistenceOwnedKey =
        "DungeonStory.PhysicalItemLogistics.PersistenceOwned";
    private static bool runnerCreated;

    static PhysicalItemLogisticsPlayModeVerifier()
    {
        EditorApplication.update -= OnEditorUpdate;
        EditorApplication.update += OnEditorUpdate;
        EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        EditorApplication.delayCall -= RecoverStaleStartSceneLeaseIfOrphaned;
        EditorApplication.delayCall += RecoverStaleStartSceneLeaseIfOrphaned;
    }

    [MenuItem("DungeonStory/Debug/QA/Request Physical Item Logistics Verification")]
    public static void RequestRunFromMenu()
    {
        PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(ReportPath);
        File.WriteAllText(RequestPath, DateTime.UtcNow.ToString("O"));
    }

    [MenuItem("DungeonStory/Debug/QA/Request Construction Project Verification")]
    public static void RequestConstructionRunFromMenu()
    {
        PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(ConstructionReportPath);
        File.WriteAllText(ConstructionRequestPath, DateTime.UtcNow.ToString("O"));
    }

    [MenuItem("DungeonStory/Debug/QA/Request L02 Mass Admission Verification")]
    public static void RequestL02RunFromMenu()
    {
        PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(L02ReportPath);
        File.WriteAllText(L02RequestPath, DateTime.UtcNow.ToString("O"));
    }

    [MenuItem("DungeonStory/Debug/QA/Request Production Input Buffer Mass Verification")]
    public static void RequestProductionInputMassRunFromMenu()
    {
        PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(ProductionInputMassReportPath);
        File.WriteAllText(
            ProductionInputMassRequestPath,
            DateTime.UtcNow.ToString("O"));
    }

    [MenuItem("DungeonStory/Debug/QA/Request Equipment Repair Buffer Mass Verification")]
    public static void RequestEquipmentRepairRunFromMenu()
    {
        PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(EquipmentRepairReportPath);
        File.WriteAllText(
            EquipmentRepairRequestPath,
            DateTime.UtcNow.ToString("O"));
    }

    [MenuItem("DungeonStory/Debug/QA/Request P17 Prepared Output Live Verification")]
    public static void RequestPreparedOutputWarehouseRunFromMenu() =>
        RequestAuthoredPreparedOutputWarehouseRun(
            PreparedOutputWarehouseVerificationRequest.P17Mode,
            P17PreparedOutputWarehouseReportPath);

    [MenuItem("DungeonStory/Debug/QA/Request P17 Natural Output Clearance Focused Verification")]
    public static void RequestP17OutputClearanceFocusedRunFromMenu() =>
        RequestAuthoredPreparedOutputWarehouseRun(
            PreparedOutputWarehouseVerificationRequest.P17ClearanceFocusedMode,
            P17OutputClearanceFocusedReportPath);

    [MenuItem("DungeonStory/Debug/QA/Request P17 Natural Output Clearance 32-Seed Verification")]
    public static void RequestP17OutputClearance32SeedRunFromMenu() =>
        RequestAuthoredPreparedOutputWarehouseRun(
            PreparedOutputWarehouseVerificationRequest.P17Clearance32SeedMode,
            P17OutputClearance32SeedReportPath);

    [MenuItem("DungeonStory/Debug/QA/Request Crop Natural Output Clearance Focused Verification")]
    public static void RequestCropOutputClearanceFocusedRunFromMenu() =>
        RequestAuthoredPreparedOutputWarehouseRun(
            PreparedOutputWarehouseVerificationRequest.CropClearanceFocusedMode,
            CropOutputClearanceFocusedReportPath);

    [MenuItem("DungeonStory/Debug/QA/Request Sawmill Prepared Output Live Verification")]
    public static void RequestSawmillPreparedOutputWarehouseRunFromMenu() =>
        RequestAuthoredPreparedOutputWarehouseRun(
            PreparedOutputWarehouseVerificationRequest.SawmillMode,
            SawmillPreparedOutputWarehouseReportPath);

    [MenuItem("DungeonStory/Debug/QA/Request M06 Prepared Output Live Verification")]
    public static void RequestM06PreparedOutputWarehouseRunFromMenu() =>
        RequestAuthoredPreparedOutputWarehouseRun(
            PreparedOutputWarehouseVerificationRequest.M06Mode,
            M06PreparedOutputWarehouseReportPath);

    [MenuItem("DungeonStory/Debug/QA/Request Sawmill Destructive Drain Live Verification")]
    public static void RequestDestructiveDrainPreparedOutputRunFromMenu() =>
        RequestAuthoredPreparedOutputWarehouseRun(
            PreparedOutputWarehouseVerificationRequest.DestructiveDrainMode,
            DestructiveDrainPreparedOutputReportPath);

    [MenuItem("DungeonStory/Debug/QA/Request V27 Natural Output Portfolio Verification")]
    public static void RequestNaturalOutputPortfolioRunFromMenu()
    {
        if (HasAnyRequest())
        {
            throw new InvalidOperationException(
                "NATURAL_PORTFOLIO_CONCURRENT_DURABLE_REQUEST");
        }
        PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        InvalidateNaturalOutputPortfolioArtifacts();
        byte[] requestBytes = new UTF8Encoding(false, true).GetBytes(
            NaturalOutputPortfolioRequestToken);
        V27BalanceArtifactWriter.WriteIfDifferent(
            NaturalOutputPortfolioRequestPath,
            stream => stream.Write(requestBytes, 0, requestBytes.Length));
    }

    private static void RequestAuthoredPreparedOutputWarehouseRun(
        string mode,
        string reportPath)
    {
        PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(reportPath);
        File.WriteAllText(
            PreparedOutputWarehouseRequestPath,
            JsonUtility.ToJson(new PreparedOutputWarehouseVerificationRequest
            {
                mode = mode
            }));
    }

    internal static void RequestSyntheticPreparedOutputWarehouseRun(
        string transactionId,
        string recipeId,
        string itemId,
        string transactionNonce,
        string itemAssetGuid,
        string recipeAssetGuid,
        string augmentedItemCatalogSha256,
        string augmentedDomainCatalogSha256,
        string transactionSourceSha256,
        string verifierSourceSha256)
    {
        if (string.IsNullOrWhiteSpace(transactionId)
            || string.IsNullOrWhiteSpace(recipeId)
            || string.IsNullOrWhiteSpace(itemId)
            || !IsCanonicalHexToken(transactionNonce, 32)
            || !IsCanonicalHexToken(itemAssetGuid, 32)
            || !IsCanonicalHexToken(recipeAssetGuid, 32)
            || !IsCanonicalHexToken(augmentedItemCatalogSha256, 64)
            || !IsCanonicalHexToken(augmentedDomainCatalogSha256, 64)
            || !IsCanonicalHexToken(transactionSourceSha256, 64)
            || !IsCanonicalHexToken(verifierSourceSha256, 64))
        {
            throw new ArgumentException(
                "Synthetic prepared-output requests require canonical transaction, recipe, and item IDs.");
        }

        PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
        Directory.CreateDirectory("Temp");
        Directory.CreateDirectory("Artifacts/QA");
        File.Delete(PreparedOutputWarehouseReportPath);
        File.WriteAllText(
            PreparedOutputWarehouseRequestPath,
            JsonUtility.ToJson(new PreparedOutputWarehouseVerificationRequest
            {
                mode = PreparedOutputWarehouseVerificationRequest.SyntheticMode,
                transactionId = transactionId,
                recipeId = recipeId,
                itemId = itemId,
                transactionNonce = transactionNonce,
                itemAssetGuid = itemAssetGuid,
                recipeAssetGuid = recipeAssetGuid,
                augmentedItemCatalogSha256 = augmentedItemCatalogSha256,
                augmentedDomainCatalogSha256 = augmentedDomainCatalogSha256,
                transactionSourceSha256 = transactionSourceSha256,
                verifierSourceSha256 = verifierSourceSha256
            }));
    }

    internal static bool TryReadPreparedOutputWarehouseCase(
        out PreparedOutputLiveRouteCase verificationCase,
        out string failure)
    {
        verificationCase = PreparedOutputLiveRouteCase.HayFeed;
        failure = string.Empty;
        if (!File.Exists(PreparedOutputWarehouseRequestPath))
        {
            failure = "prepared-output request file is missing";
            return false;
        }

        string payload = File.ReadAllText(PreparedOutputWarehouseRequestPath);
        if (!payload.TrimStart().StartsWith("{", StringComparison.Ordinal))
            return true;

        PreparedOutputWarehouseVerificationRequest request;
        try
        {
            request = JsonUtility.FromJson<PreparedOutputWarehouseVerificationRequest>(
                payload);
        }
        catch (Exception exception)
        {
            failure = "prepared-output request JSON is invalid: " + exception.Message;
            return false;
        }

        if (request != null
            && string.Equals(
                request.mode,
                PreparedOutputWarehouseVerificationRequest.P17Mode,
                StringComparison.Ordinal))
        {
            verificationCase = PreparedOutputLiveRouteCase.HayFeed;
            return true;
        }
        if (request != null
            && string.Equals(
                request.mode,
                PreparedOutputWarehouseVerificationRequest.P17ClearanceFocusedMode,
                StringComparison.Ordinal))
        {
            verificationCase = PreparedOutputLiveRouteCase.HayFeedClearanceFocused;
            return true;
        }
        if (request != null
            && string.Equals(
                request.mode,
                PreparedOutputWarehouseVerificationRequest.P17Clearance32SeedMode,
                StringComparison.Ordinal))
        {
            verificationCase = PreparedOutputLiveRouteCase.HayFeedClearance32Seed;
            return true;
        }
        if (request != null
            && string.Equals(
                request.mode,
                PreparedOutputWarehouseVerificationRequest.CropClearanceFocusedMode,
                StringComparison.Ordinal))
        {
            verificationCase = PreparedOutputLiveRouteCase.CropHarvestClearanceFocused;
            return true;
        }
        if (request != null
            && string.Equals(
                request.mode,
                PreparedOutputWarehouseVerificationRequest.SawmillMode,
                StringComparison.Ordinal))
        {
            verificationCase = PreparedOutputLiveRouteCase.Sawmill;
            return true;
        }
        if (request != null
            && string.Equals(
                request.mode,
                PreparedOutputWarehouseVerificationRequest.M06Mode,
                StringComparison.Ordinal))
        {
            verificationCase = PreparedOutputLiveRouteCase.M06ProstheticArm;
            return true;
        }
        if (request != null
            && string.Equals(
                request.mode,
                PreparedOutputWarehouseVerificationRequest.DestructiveDrainMode,
                StringComparison.Ordinal))
        {
            verificationCase = PreparedOutputLiveRouteCase.DestructiveDrain;
            return true;
        }

        if (request == null
            || !string.Equals(request.mode,
                PreparedOutputWarehouseVerificationRequest.SyntheticMode,
                StringComparison.Ordinal)
            || !IsCanonicalRequestToken(request.transactionId)
            || !IsCanonicalRequestToken(request.recipeId)
            || !IsCanonicalRequestToken(request.itemId)
            || !IsCanonicalHexToken(request.transactionNonce, 32)
            || !IsCanonicalHexToken(request.itemAssetGuid, 32)
            || !IsCanonicalHexToken(request.recipeAssetGuid, 32)
            || !IsCanonicalHexToken(request.augmentedItemCatalogSha256, 64)
            || !IsCanonicalHexToken(request.augmentedDomainCatalogSha256, 64)
            || !IsCanonicalHexToken(request.transactionSourceSha256, 64)
            || !IsCanonicalHexToken(request.verifierSourceSha256, 64))
        {
            failure = "prepared-output synthetic request fields are missing or noncanonical";
            return false;
        }

        verificationCase = new PreparedOutputLiveRouteCase(
            caseId: "synthetic-definition-only",
            isSynthetic: true,
            requiresSanitizedScene: true,
            runsTransportFaultMatrix: true,
            verifiesPostDeliverySaveRoundTrip: true,
            usesPreparedRouteAuthority: true,
            transactionId: request.transactionId,
            recipeId: request.recipeId,
            itemId: request.itemId,
            facilityAssetPath:
                "Assets/Resources/SO/Building/Modular/P17_사료배합대.asset",
            facilityObjectName: "QA_Prepared_Output_Synthetic",
            expectedOutputBufferCycleCapacity: 4,
            expectedFacilityCapacityGrams: 0L,
            reportPath: PreparedOutputWarehouseReportPath,
            transactionNonce: request.transactionNonce,
            itemAssetGuid: request.itemAssetGuid,
            recipeAssetGuid: request.recipeAssetGuid,
            augmentedItemCatalogSha256: request.augmentedItemCatalogSha256,
            augmentedDomainCatalogSha256: request.augmentedDomainCatalogSha256,
            transactionSourceSha256: request.transactionSourceSha256,
            verifierSourceSha256: request.verifierSourceSha256);
        return true;
    }

    private static bool IsCanonicalRequestToken(string value) =>
        !string.IsNullOrWhiteSpace(value)
        && string.Equals(value, value.Trim(), StringComparison.Ordinal);

    private static bool IsCanonicalHexToken(string value, int length) =>
        !string.IsNullOrEmpty(value)
        && value.Length == length
        && value.All(Uri.IsHexDigit);

    private static void OnEditorUpdate()
    {
        if ((!File.Exists(RequestPath)
                && !File.Exists(ConstructionRequestPath)
                && !File.Exists(L02RequestPath)
                && !File.Exists(ProductionInputMassRequestPath)
                && !File.Exists(EquipmentRepairRequestPath)
                && !File.Exists(PreparedOutputWarehouseRequestPath)
                && !File.Exists(NaturalOutputPortfolioRequestPath))
            || EditorApplication.isPlayingOrWillChangePlaymode)
        {
            return;
        }

        try
        {
            string[] pendingRequests = CapturePendingRequestPaths();
            if (pendingRequests.Length != 1)
            {
                throw new InvalidOperationException(
                    "PHYSICAL_LOGISTICS_CONCURRENT_REQUESTS_NOT_ALLOWED: count="
                    + pendingRequests.Length);
            }
            if (string.Equals(pendingRequests[0],
                    NaturalOutputPortfolioRequestPath, StringComparison.Ordinal)
                && !TryValidateNaturalOutputPortfolioRequest(out string failure))
            {
                throw new InvalidDataException(failure);
            }
            if (SessionState.GetBool(PersistenceOwnedKey, false)
                && !PlayModeVerificationPersistenceSnapshot.Exists(
                    PersistenceSnapshotId))
            {
                SessionState.EraseBool(PersistenceOwnedKey);
            }

            if (!DungeonFinalPlayModeAcceptanceRequestFacade
                    .IsPersistenceCoordinatorActive
                && !SessionState.GetBool(PersistenceOwnedKey, false))
            {
                PlayModeVerificationPersistenceSnapshot.CaptureCurrent(
                    PersistenceSnapshotId);
                SessionState.SetBool(PersistenceOwnedKey, true);
            }

            if (!AcquireSyntheticGameplaySceneLeaseIfRequired())
            {
                // Changing EditorBuildSettings and entering PlayMode in the same
                // editor update leaves the scene-name lookup result unproven.
                // A synthetic lease therefore settles for one full update before
                // the product boot lease is acquired and PlayMode is requested.
                return;
            }
            RequireProductBootScenesEnabled();
            PlayModeVerificationStartSceneLease.Acquire(
                StartSceneLeaseOwnerId,
                TitleScenePath);
            EditorApplication.EnterPlaymode();
        }
        catch (Exception exception)
        {
            FailBeforePlay("EDITOR_BOOT_PREPARE_FAILED: " + exception);
        }
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange change)
    {
        if (change == PlayModeStateChange.EnteredEditMode)
        {
            bool ownsReturnedPlayMode =
                SessionState.GetBool(PersistenceOwnedKey, false)
                || HasAnyRequest()
                || PlayModeVerificationStartSceneLease.IsOwnedBy(
                    StartSceneLeaseOwnerId);
            if (!ownsReturnedPlayMode)
                return;
            runnerCreated = false;
            string cleanupFailure = string.Empty;
            try
            {
                PlayModeVerificationStartSceneLease.RestoreOwned(
                    StartSceneLeaseOwnerId);
            }
            catch (Exception exception)
            {
                cleanupFailure = "START_SCENE_RESTORE_FAILED: " + exception;
            }
            try
            {
                SyntheticPreparedOutputCanaryGameplaySceneLease.RestoreOwned();
            }
            catch (Exception exception)
            {
                cleanupFailure += " | GAMEPLAY_SCENE_LEASE_RESTORE_FAILED: "
                    + exception;
            }
            try
            {
                RestoreOwnedPersistence();
            }
            catch (Exception exception)
            {
                cleanupFailure += " | PERSISTENCE_RESTORE_FAILED: " + exception;
            }
            PlayModeVerificationInputCleanup.CleanupStaleVerificationMice();
            if (HasAnyRequest())
            {
                FailBeforePlay(
                    "PLAYMODE_ABORTED verifier returned to EditMode before completion"
                    + (cleanupFailure.Length > 0
                        ? " | " + cleanupFailure
                        : string.Empty));
            }
            else if (cleanupFailure.Length > 0)
            {
                Debug.LogError(cleanupFailure);
            }
            return;
        }

        if (change == PlayModeStateChange.EnteredPlayMode
            && runnerCreated
            && UnityEngine.Object.FindFirstObjectByType<
                PlayModeVerificationCoroutineHost>(
                    FindObjectsInactive.Include) == null)
        {
            // Enter Play Mode Options may preserve static fields while the
            // previous runner GameObject was destroyed on the EditMode
            // boundary. Repair that stale latch so a durable request cannot
            // enter PlayMode without an executing verifier.
            runnerCreated = false;
        }

        if (change != PlayModeStateChange.EnteredPlayMode
            || runnerCreated
            || !File.Exists(RequestPath)
                && !File.Exists(ConstructionRequestPath)
                && !File.Exists(L02RequestPath)
                 && !File.Exists(ProductionInputMassRequestPath)
                 && !File.Exists(EquipmentRepairRequestPath)
                 && !File.Exists(PreparedOutputWarehouseRequestPath)
                 && !File.Exists(NaturalOutputPortfolioRequestPath))
        {
            return;
        }

        try
        {
            PlayModeVerificationStartSceneLease.RestoreOwned(
                StartSceneLeaseOwnerId);
        }
        catch (Exception exception)
        {
            FailBeforePlay("START_SCENE_RESTORE_FAILED: " + exception);
            EditorApplication.ExitPlaymode();
            return;
        }
        if (!string.Equals(
                SceneManager.GetActiveScene().path,
                TitleScenePath,
                StringComparison.OrdinalIgnoreCase))
        {
            FailBeforePlay(
                "BOOT_TITLE_SCENE_MISMATCH: active="
                + SceneManager.GetActiveScene().path);
            EditorApplication.ExitPlaymode();
            return;
        }
        if (TryRequireSyntheticGameplaySceneLease(out string leaseFailure)
            && leaseFailure.Length > 0)
        {
            FailBeforePlay("SYNTHETIC_GAMEPLAY_SCENE_LEASE_INVALID: "
                + leaseFailure);
            EditorApplication.ExitPlaymode();
            return;
        }

        runnerCreated = true;
        GameObject runner = new(
            "Physical Item Logistics PlayMode Verification Runner");
        UnityEngine.Object.DontDestroyOnLoad(runner);
        PlayModeVerificationCoroutineHost.RunFactory = () =>
            new PhysicalItemLogisticsPlayModeVerificationRunner().Run();
        runner.AddComponent<PlayModeVerificationCoroutineHost>();
    }

    private static void FailBeforePlay(string detail)
    {
        try
        {
            PlayModeVerificationStartSceneLease.RestoreOwned(
                StartSceneLeaseOwnerId);
        }
        catch (Exception restoreException)
        {
            detail += " | START_SCENE_RESTORE_FAILED: " + restoreException;
        }
        if (!EditorApplication.isPlayingOrWillChangePlaymode)
        {
            try
            {
                SyntheticPreparedOutputCanaryGameplaySceneLease.RestoreOwned();
            }
            catch (Exception restoreException)
            {
                detail += " | GAMEPLAY_SCENE_LEASE_RESTORE_FAILED: "
                    + restoreException;
            }
        }
        try
        {
            RestoreOwnedPersistence();
        }
        catch (Exception restoreException)
        {
            detail += " | PERSISTENCE_RESTORE_FAILED: " + restoreException;
        }

        Directory.CreateDirectory("Artifacts/QA");
        string requestPath = File.Exists(ConstructionRequestPath)
            ? ConstructionRequestPath
            : File.Exists(L02RequestPath)
                ? L02RequestPath
                : File.Exists(ProductionInputMassRequestPath)
                    ? ProductionInputMassRequestPath
                    : File.Exists(EquipmentRepairRequestPath)
                        ? EquipmentRepairRequestPath
                        : File.Exists(NaturalOutputPortfolioRequestPath)
                            ? NaturalOutputPortfolioRequestPath
                            : File.Exists(PreparedOutputWarehouseRequestPath)
                                ? PreparedOutputWarehouseRequestPath
                                : RequestPath;
        string reportPath = File.Exists(ConstructionRequestPath)
            ? ConstructionReportPath
            : File.Exists(L02RequestPath)
                ? L02ReportPath
                : File.Exists(ProductionInputMassRequestPath)
                    ? ProductionInputMassReportPath
                    : File.Exists(EquipmentRepairRequestPath)
                        ? EquipmentRepairReportPath
                        : File.Exists(NaturalOutputPortfolioRequestPath)
                            ? NaturalOutputPortfolioRunnerReportPath
                        : File.Exists(PreparedOutputWarehouseRequestPath)
                            ? ResolvePreparedOutputReportPath()
                            : ReportPath;
        string failureReport =
            "Physical Item Logistics PlayMode Verification\n"
            + "[FAIL] EDITOR_BOOT_GUARD: " + detail + "\n"
            + "RESULT=FAIL; failures=1\n";
        bool naturalOutputPortfolioRequest = string.Equals(
            requestPath,
            NaturalOutputPortfolioRequestPath,
            StringComparison.Ordinal);
        PublishBootFailureReportForDiagnostics(
            reportPath,
            failureReport,
            naturalOutputPortfolioRequest
                ? InvalidateNaturalOutputPortfolioArtifacts
                : null);
        File.Delete(requestPath);
        Debug.LogError(detail);
    }

    internal static void PublishBootFailureReportForDiagnostics(
        string reportPath,
        string failureReport,
        Action invalidateArtifacts)
    {
        if (string.IsNullOrWhiteSpace(reportPath))
            throw new ArgumentException(
                "A boot-failure report path is required.",
                nameof(reportPath));
        if (failureReport == null)
            throw new ArgumentNullException(nameof(failureReport));

        // Invalidation must happen first because the natural portfolio cleanup
        // intentionally removes every previous terminal artifact, including its
        // runner report. Publishing first would immediately delete the only
        // fail-loud evidence for an Editor boot failure.
        invalidateArtifacts?.Invoke();
        File.WriteAllText(reportPath, failureReport);
    }

    private static bool HasAnyRequest() =>
        File.Exists(RequestPath)
        || File.Exists(ConstructionRequestPath)
        || File.Exists(L02RequestPath)
        || File.Exists(ProductionInputMassRequestPath)
        || File.Exists(EquipmentRepairRequestPath)
        || File.Exists(PreparedOutputWarehouseRequestPath)
        || File.Exists(NaturalOutputPortfolioRequestPath);

    internal static bool HasPendingDurableRun => HasAnyRequest();

    private static string ResolvePreparedOutputReportPath()
    {
        return TryReadPreparedOutputWarehouseCase(
                out PreparedOutputLiveRouteCase verificationCase,
                out _)
            && !string.IsNullOrWhiteSpace(verificationCase.ReportPath)
                ? verificationCase.ReportPath
                : PreparedOutputWarehouseReportPath;
    }

    private static string[] CapturePendingRequestPaths()
    {
        string[] paths =
        {
            RequestPath,
            ConstructionRequestPath,
            L02RequestPath,
            ProductionInputMassRequestPath,
            EquipmentRepairRequestPath,
            PreparedOutputWarehouseRequestPath,
            NaturalOutputPortfolioRequestPath
        };
        return paths.Where(File.Exists).ToArray();
    }

    internal static bool TryValidateNaturalOutputPortfolioRequest(
        out string failure)
    {
        failure = string.Empty;
        try
        {
            if (!File.Exists(NaturalOutputPortfolioRequestPath)
                || !string.Equals(
                    File.ReadAllText(
                        NaturalOutputPortfolioRequestPath,
                        new UTF8Encoding(false, true)),
                    NaturalOutputPortfolioRequestToken,
                    StringComparison.Ordinal))
            {
                failure = "NATURAL_PORTFOLIO_REQUEST_TOKEN_INVALID";
                return false;
            }
            return true;
        }
        catch (Exception exception) when (exception is IOException
                                           or UnauthorizedAccessException
                                           or DecoderFallbackException)
        {
            failure = "NATURAL_PORTFOLIO_REQUEST_READ_FAILED: "
                + exception.Message;
            return false;
        }
    }

    private static void InvalidateNaturalOutputPortfolioArtifacts()
    {
        File.Delete(NaturalOutputPortfolioRunnerReportPath);
        File.Delete(ProductionOutputClearanceNaturalPortfolioCoordinator.ReportPath);
        File.Delete(
            ProductionOutputClearanceNaturalPortfolioCoordinator
                .ObservationsCsvPath);
        File.Delete(
            ProductionOutputClearanceNaturalPortfolioCoordinator
                .OutputSlicesCsvPath);
    }

    private static void RestoreOwnedPersistence()
    {
        if (!SessionState.GetBool(PersistenceOwnedKey, false))
        {
            return;
        }

        PlayModeVerificationPersistenceSnapshot.Restore(
            PersistenceSnapshotId);
        SessionState.EraseBool(PersistenceOwnedKey);
    }

    private static void RequireProductBootScenesEnabled()
    {
        string[] requiredSceneNames =
        {
            DungeonSceneNavigator.TitleSceneName,
            DungeonSceneNavigator.PreparationSceneName,
            DungeonSceneNavigator.GameplaySceneName
        };
        HashSet<string> enabledScenes = EditorBuildSettings.scenes
            .Where(scene => scene.enabled)
            .Select(scene => Path.GetFileNameWithoutExtension(scene.path))
            .ToHashSet(StringComparer.Ordinal);
        string[] missing = requiredSceneNames
            .Where(sceneName => !enabledScenes.Contains(sceneName))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                "Required product boot scenes are not enabled: "
                + string.Join(", ", missing));
        }
    }

    private static bool AcquireSyntheticGameplaySceneLeaseIfRequired()
    {
        // Every physical-logistics verification boots the authored Gameplay
        // scene. The current authored scene intentionally retains editor-only
        // lifecycle fixtures, so all of these verifiers must use the same
        // byte-preserving sanitized scene lease. Limiting the lease to the
        // synthetic prepared-output case lets L02/full-logistics runs fail in
        // SceneBuildableLeakValidator before their actual contract is tested.
        bool wasActive = SyntheticPreparedOutputCanaryGameplaySceneLease.IsActive;
        SyntheticPreparedOutputCanaryGameplaySceneLease.Acquire();
        return wasActive;
    }

    private static bool TryRequireSyntheticGameplaySceneLease(
        out string failure)
    {
        failure = string.Empty;
        if (!SyntheticPreparedOutputCanaryGameplaySceneLease.IsActive)
            return false;
        try
        {
            SyntheticPreparedOutputCanaryGameplaySceneLease.ValidateActive();
        }
        catch (Exception exception)
        {
            failure = exception.Message;
        }
        return true;
    }

    private static void RecoverStaleStartSceneLeaseIfOrphaned()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode
            || HasAnyRequest()
            || !PlayModeVerificationStartSceneLease.IsOwnedBy(
                StartSceneLeaseOwnerId))
        {
            return;
        }

        try
        {
            PlayModeVerificationStartSceneLease.RestoreOwned(
                StartSceneLeaseOwnerId);
            SyntheticPreparedOutputCanaryGameplaySceneLease.RestoreOwned();
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Failed to recover an orphaned physical-logistics start-scene lease: "
                + exception);
        }
    }
}

[Serializable]
internal sealed class PreparedOutputWarehouseVerificationRequest
{
    internal const string SyntheticMode = "synthetic-definition-only";
    internal const string P17Mode = "authored-p17-hay-feed";
    internal const string P17ClearanceFocusedMode =
        "authored-p17-natural-clearance-focused";
    internal const string P17Clearance32SeedMode =
        "authored-p17-natural-clearance-32-seed";
    internal const string CropClearanceFocusedMode =
        "authored-crop-natural-clearance-focused";
    internal const string SawmillMode = "authored-sawmill";
    internal const string M06Mode = "authored-m06-prosthetic-arm";
    internal const string DestructiveDrainMode =
        "authored-p17-destructive-drain";

    public string mode = string.Empty;
    public string transactionId = string.Empty;
    public string recipeId = string.Empty;
    public string itemId = string.Empty;
    public string transactionNonce = string.Empty;
    public string itemAssetGuid = string.Empty;
    public string recipeAssetGuid = string.Empty;
    public string augmentedItemCatalogSha256 = string.Empty;
    public string augmentedDomainCatalogSha256 = string.Empty;
    public string transactionSourceSha256 = string.Empty;
    public string verifierSourceSha256 = string.Empty;
}

internal readonly struct PreparedOutputLiveRouteCase
{
    internal static PreparedOutputLiveRouteCase HayFeed => new(
        caseId: "hay-feed",
        isSynthetic: false,
        requiresSanitizedScene: true,
        runsTransportFaultMatrix: false,
        verifiesPostDeliverySaveRoundTrip: true,
        usesPreparedRouteAuthority: true,
        transactionId: string.Empty,
        recipeId: "recipe:hay-feed",
        itemId: "feed:hay",
        facilityAssetPath:
            "Assets/Resources/SO/Building/Modular/P17_사료배합대.asset",
        facilityObjectName: "QA_Prepared_Output_Feedbench",
        expectedOutputBufferCycleCapacity: 4,
        expectedFacilityCapacityGrams: 4_200L,
        reportPath: PhysicalItemLogisticsPlayModeVerifier
            .P17PreparedOutputWarehouseReportPath);

    internal static PreparedOutputLiveRouteCase HayFeedClearanceFocused => new(
        caseId: "hay-feed-natural-clearance-focused",
        isSynthetic: false,
        requiresSanitizedScene: true,
        runsTransportFaultMatrix: false,
        verifiesPostDeliverySaveRoundTrip: false,
        usesPreparedRouteAuthority: true,
        transactionId: string.Empty,
        recipeId: "recipe:hay-feed",
        itemId: "feed:hay",
        facilityAssetPath:
            "Assets/Resources/SO/Building/Modular/P17_사료배합대.asset",
        facilityObjectName: "QA_Prepared_Output_Clearance_Feedbench",
        expectedOutputBufferCycleCapacity: 4,
        expectedFacilityCapacityGrams: 4_200L,
        reportPath: PhysicalItemLogisticsPlayModeVerifier
            .P17OutputClearanceFocusedReportPath,
        verifiesClearanceMeasurement: true);

    internal static PreparedOutputLiveRouteCase HayFeedClearance32Seed => new(
        caseId: "hay-feed-natural-clearance-32-seed",
        isSynthetic: false,
        requiresSanitizedScene: true,
        runsTransportFaultMatrix: false,
        verifiesPostDeliverySaveRoundTrip: false,
        usesPreparedRouteAuthority: true,
        transactionId: string.Empty,
        recipeId: "recipe:hay-feed",
        itemId: "feed:hay",
        facilityAssetPath:
            "Assets/Resources/SO/Building/Modular/P17_사료배합대.asset",
        facilityObjectName: "QA_Prepared_Output_Clearance_32_Seed_Feedbench",
        expectedOutputBufferCycleCapacity: 4,
        expectedFacilityCapacityGrams: 4_200L,
        reportPath: PhysicalItemLogisticsPlayModeVerifier
            .P17OutputClearance32SeedReportPath,
        verifiesClearanceMeasurement: true,
        clearanceSeedCount: 32);

    internal static PreparedOutputLiveRouteCase CropHarvestClearanceFocused => new(
        caseId: "crop-harvest-natural-clearance-focused",
        isSynthetic: false,
        requiresSanitizedScene: true,
        runsTransportFaultMatrix: false,
        verifiesPostDeliverySaveRoundTrip: false,
        usesPreparedRouteAuthority: false,
        transactionId: string.Empty,
        recipeId: string.Empty,
        itemId: string.Empty,
        facilityAssetPath:
            "Assets/Resources/SO/Building/Modular/P23_야외경작지.asset",
        facilityObjectName: "QA_Crop_Output_Clearance_P23",
        expectedOutputBufferCycleCapacity: 4,
        expectedFacilityCapacityGrams: 51_800L,
        reportPath: PhysicalItemLogisticsPlayModeVerifier
            .CropOutputClearanceFocusedReportPath,
        verifiesClearanceMeasurement: true,
        isCropNaturalClearance: true);

    internal static PreparedOutputLiveRouteCase Sawmill => new(
        caseId: "sawmill-lumber",
        isSynthetic: false,
        requiresSanitizedScene: true,
        runsTransportFaultMatrix: true,
        verifiesPostDeliverySaveRoundTrip: true,
        usesPreparedRouteAuthority: true,
        transactionId: string.Empty,
        recipeId: "recipe:sawmill-lumber",
        itemId: "material:lumber",
        facilityAssetPath:
            "Assets/Resources/SO/Building/Modular/P03_제재소.asset",
        facilityObjectName: "QA_Prepared_Output_Sawmill",
        expectedOutputBufferCycleCapacity: 4,
        expectedFacilityCapacityGrams: 14_400L,
        reportPath: PhysicalItemLogisticsPlayModeVerifier
            .SawmillPreparedOutputWarehouseReportPath);

    internal static PreparedOutputLiveRouteCase M06ProstheticArm => new(
        caseId: "m06-prosthetic-arm",
        isSynthetic: false,
        requiresSanitizedScene: true,
        runsTransportFaultMatrix: false,
        verifiesPostDeliverySaveRoundTrip: true,
        usesPreparedRouteAuthority: false,
        transactionId: string.Empty,
        recipeId: "recipe:surgery:prosthetic-arm",
        itemId: "surgery:prosthetic:arm:left",
        facilityAssetPath:
            "Assets/Resources/SO/Building/Medical/M06_보철조립대.asset",
        facilityObjectName: "QA_Prepared_Output_M06",
        expectedOutputBufferCycleCapacity: 4,
        expectedFacilityCapacityGrams: 7_200L,
        reportPath: PhysicalItemLogisticsPlayModeVerifier
            .M06PreparedOutputWarehouseReportPath,
        expectedRuntimeComponentTypeId: "medical:surgical-part-output");

    internal static PreparedOutputLiveRouteCase DestructiveDrain => new(
        caseId: "sawmill-destructive-drain",
        isSynthetic: false,
        requiresSanitizedScene: true,
        runsTransportFaultMatrix: true,
        verifiesPostDeliverySaveRoundTrip: false,
        usesPreparedRouteAuthority: true,
        transactionId: string.Empty,
        recipeId: "recipe:sawmill-lumber",
        itemId: "material:lumber",
        facilityAssetPath:
            "Assets/Resources/SO/Building/Modular/P03_제재소.asset",
        facilityObjectName: "QA_Sawmill_Destructive_Drain",
        expectedOutputBufferCycleCapacity: 4,
        expectedFacilityCapacityGrams: 14_400L,
        reportPath: PhysicalItemLogisticsPlayModeVerifier
            .DestructiveDrainPreparedOutputReportPath,
        verifiesDestructiveDrain: true);

    internal PreparedOutputLiveRouteCase(
        bool isSynthetic,
        string transactionId,
        string recipeId,
        string itemId,
        string transactionNonce = "",
        string itemAssetGuid = "",
        string recipeAssetGuid = "",
        string augmentedItemCatalogSha256 = "",
        string augmentedDomainCatalogSha256 = "",
        string transactionSourceSha256 = "",
        string verifierSourceSha256 = "",
        bool verifiesDestructiveDrain = false,
        bool verifiesClearanceMeasurement = false,
        int clearanceSeedCount = 1,
        bool isCropNaturalClearance = false)
        : this(
            caseId: isSynthetic ? "synthetic-definition-only" : "legacy-authored",
            isSynthetic: isSynthetic,
            requiresSanitizedScene: isSynthetic,
            runsTransportFaultMatrix: isSynthetic,
            verifiesPostDeliverySaveRoundTrip: isSynthetic,
            usesPreparedRouteAuthority: isSynthetic,
            transactionId: transactionId,
            recipeId: recipeId,
            itemId: itemId,
            facilityAssetPath:
                "Assets/Resources/SO/Building/Modular/P17_사료배합대.asset",
            facilityObjectName: "QA_Prepared_Output_Synthetic",
            expectedOutputBufferCycleCapacity: 4,
            expectedFacilityCapacityGrams: 0L,
            reportPath: PhysicalItemLogisticsPlayModeVerifier
                .PreparedOutputWarehouseReportPath,
            transactionNonce: transactionNonce,
            itemAssetGuid: itemAssetGuid,
            recipeAssetGuid: recipeAssetGuid,
            augmentedItemCatalogSha256: augmentedItemCatalogSha256,
            augmentedDomainCatalogSha256: augmentedDomainCatalogSha256,
            transactionSourceSha256: transactionSourceSha256,
            verifierSourceSha256: verifierSourceSha256,
            verifiesDestructiveDrain: verifiesDestructiveDrain,
            verifiesClearanceMeasurement: verifiesClearanceMeasurement,
            clearanceSeedCount: clearanceSeedCount,
            isCropNaturalClearance: isCropNaturalClearance)
    {
    }

    internal PreparedOutputLiveRouteCase(
        string caseId,
        bool isSynthetic,
        bool requiresSanitizedScene,
        bool runsTransportFaultMatrix,
        bool verifiesPostDeliverySaveRoundTrip,
        bool usesPreparedRouteAuthority,
        string transactionId,
        string recipeId,
        string itemId,
        string facilityAssetPath,
        string facilityObjectName,
        int expectedOutputBufferCycleCapacity,
        long expectedFacilityCapacityGrams,
        string reportPath,
        string expectedRuntimeComponentTypeId = "",
        string transactionNonce = "",
        string itemAssetGuid = "",
        string recipeAssetGuid = "",
        string augmentedItemCatalogSha256 = "",
        string augmentedDomainCatalogSha256 = "",
        string transactionSourceSha256 = "",
        string verifierSourceSha256 = "",
        bool verifiesDestructiveDrain = false,
        bool verifiesClearanceMeasurement = false,
        int clearanceSeedCount = 1,
        bool isCropNaturalClearance = false)
    {
        CaseId = caseId ?? string.Empty;
        IsSynthetic = isSynthetic;
        RequiresSanitizedScene = requiresSanitizedScene;
        RunsTransportFaultMatrix = runsTransportFaultMatrix;
        VerifiesPostDeliverySaveRoundTrip = verifiesPostDeliverySaveRoundTrip;
        UsesPreparedRouteAuthority = usesPreparedRouteAuthority;
        TransactionId = transactionId ?? string.Empty;
        RecipeId = recipeId ?? string.Empty;
        ItemId = itemId ?? string.Empty;
        FacilityAssetPath = facilityAssetPath ?? string.Empty;
        FacilityObjectName = facilityObjectName ?? string.Empty;
        ExpectedOutputBufferCycleCapacity = expectedOutputBufferCycleCapacity;
        ExpectedFacilityCapacityGrams = expectedFacilityCapacityGrams;
        ReportPath = reportPath ?? string.Empty;
        ExpectedRuntimeComponentTypeId = expectedRuntimeComponentTypeId ?? string.Empty;
        TransactionNonce = transactionNonce ?? string.Empty;
        ItemAssetGuid = itemAssetGuid ?? string.Empty;
        RecipeAssetGuid = recipeAssetGuid ?? string.Empty;
        AugmentedItemCatalogSha256 = augmentedItemCatalogSha256 ?? string.Empty;
        AugmentedDomainCatalogSha256 = augmentedDomainCatalogSha256 ?? string.Empty;
        TransactionSourceSha256 = transactionSourceSha256 ?? string.Empty;
        VerifierSourceSha256 = verifierSourceSha256 ?? string.Empty;
        VerifiesDestructiveDrain = verifiesDestructiveDrain;
        VerifiesClearanceMeasurement = verifiesClearanceMeasurement;
        IsCropNaturalClearance = isCropNaturalClearance;
        if (clearanceSeedCount <= 0
            || !verifiesClearanceMeasurement && clearanceSeedCount != 1)
        {
            throw new ArgumentOutOfRangeException(nameof(clearanceSeedCount));
        }
        ClearanceSeedCount = clearanceSeedCount;
    }

    internal string CaseId { get; }
    internal bool IsSynthetic { get; }
    internal bool RequiresSanitizedScene { get; }
    internal bool RunsTransportFaultMatrix { get; }
    internal bool VerifiesPostDeliverySaveRoundTrip { get; }
    internal bool UsesPreparedRouteAuthority { get; }
    internal string TransactionId { get; }
    internal string RecipeId { get; }
    internal string ItemId { get; }
    internal string FacilityAssetPath { get; }
    internal string FacilityObjectName { get; }
    internal int ExpectedOutputBufferCycleCapacity { get; }
    internal long ExpectedFacilityCapacityGrams { get; }
    internal string ReportPath { get; }
    internal string ExpectedRuntimeComponentTypeId { get; }
    internal string TransactionNonce { get; }
    internal string ItemAssetGuid { get; }
    internal string RecipeAssetGuid { get; }
    internal string AugmentedItemCatalogSha256 { get; }
    internal string AugmentedDomainCatalogSha256 { get; }
    internal string TransactionSourceSha256 { get; }
    internal string VerifierSourceSha256 { get; }
    internal bool VerifiesDestructiveDrain { get; }
    internal bool VerifiesClearanceMeasurement { get; }
    internal int ClearanceSeedCount { get; }
    internal bool IsCropNaturalClearance { get; }
}

public sealed partial class PhysicalItemLogisticsPlayModeVerificationRunner
{
    private const string PreservedRationItemId = "food:preserved-ration";
    private const string DaggerItemId = "equipment-item:weapon:dagger";
    private const string DaggerId = "weapon:dagger";
    private const string RepairEquipmentId = "shield:wood";
    private const string InoculatedLogItemId = "supply:inoculated-log";
    private const string PreparedOutputCustodyComponentTypeId =
        "item-state:prepared-output-route-slice";
    private const string PreparedOutputPublicationComponentTypeId =
        "item-state:facility-buffer-planned-output-publication";
    private const string PreparedOutputProvenanceComponentTypeId =
        "item-state:facility-buffer-planned-output-provenance";
    private const int NaturalClearanceFirstSeed = 157181;
    private const int NaturalClearanceRequiredSeedCount = 32;
    private const float HaulTimeoutSeconds = 30f;
    private const int NaturalHaulReadinessMaximumTurns = 300;
    private const int NaturalHaulSequentialSliceReplanDelayTurns = 60;
    private const int NaturalHaulMaximumSchedulingTurnsPerSlice = 4_096;
    // Natural-portfolio progress bounds are simulation-turn budgets. The
    // coordinator fixes captureDeltaTime and enables deterministic AI/path
    // scheduling, so wall-clock deadlines would make the same seed depend on
    // editor load and concurrent worker count.
    private const int NaturalSpecialDriveMaximumTurns = 4_096;
    private const int NaturalSpecialCropGrowthMaximumTurns = 4_096;
    private const int NaturalRoutePublicationMaximumTurns = 4_096;
    private const int NaturalRecipeProductionMaximumTurns = 4_096;
    private const float NaturalPortfolioAcceleratedTimeScale = 16f;
    private const int NaturalPortfolioMaximumSchedulerOwners = 1;
    private const int VerificationBootMaximumTurns = 4_096;

    private readonly List<string> report = new List<string>();
    private readonly List<string> failures = new List<string>();
    private readonly List<string> capturedErrors = new List<string>();
    private readonly List<string> capturedWarnings = new List<string>();
    private readonly List<GameObject> temporaryObjects = new List<GameObject>();
    private readonly List<(ICharacterAiWorldRegistry Registry,
        IWarehouseFacility Warehouse)> temporaryWarehouseRegistrations = new();
    private readonly Dictionary<WarehouseInventory, WarehouseInventorySnapshot> warehouseSnapshots =
        new Dictionary<WarehouseInventory, WarehouseInventorySnapshot>();

    private DungeonPhysicalItemSaveData physicalSnapshot;
    private DungeonCombatEquipmentSaveData equipmentSnapshot;
    private Mouse originalMouse;
    private Mouse verificationMouse;
    private int verificationMouseSerial;
    private readonly Dictionary<CharacterActor, bool> isolatedAiPauseStates =
        new Dictionary<CharacterActor, bool>();
    private readonly Dictionary<AIBrain, bool> isolatedLogisticsMeasurementStates =
        new Dictionary<AIBrain, bool>();
    private readonly Dictionary<CharacterActor,
        Dictionary<CharacterCondition, float>> isolatedNeedStates = new();
    private readonly List<CharacterActor> verificationActors = new List<CharacterActor>();
    private float originalTimeScale;
    private IWorldItemStackRuntime itemRuntime;
    private bool constructionOnly;
    private bool l02Only;
    private bool productionInputMassOnly;
    private bool equipmentRepairOnly;
    private bool preparedOutputWarehouseOnly;
    private bool naturalOutputPortfolioOnly;
    private int naturalOutputPortfolioPlanCount = -1;
    private bool naturalClearanceOnly;
    private PreparedOutputLiveRouteCase preparedOutputCase;
    private IDungeonDebugModeService debugMode;
    private bool originalFreezeNeeds;
    private bool originalFriendlyInvincible;
    private bool originalPreventBreakdowns;
    private bool productBootSucceeded;
    private NaturalClearanceSeedRunState activeNaturalClearanceSeedRun;
    private IGameClockDiagnosticsControl naturalClearanceCheckpointClock;
    private CharacterSpawner naturalClearanceSpawner;
    private bool naturalClearanceSpawnerStateCaptured;
    private bool naturalClearanceSpawnerOriginallyPaused;
    private CharacterAiScheduler naturalClearanceScheduler;
    private bool naturalClearanceSchedulerStateCaptured;
    private bool naturalClearanceSchedulerOriginallyDeterministic;
    private InvasionThreatRuntime naturalClearanceInvasionThreat;
    private bool naturalClearanceInvasionThreatStateCaptured;
    private bool naturalClearanceInvasionThreatOriginallyEnabled;
    private CharacterConsumablesRuntime naturalClearanceConsumables;
    private bool naturalClearanceConsumablesStateCaptured;
    private bool naturalClearanceMealDeliveryOriginallyPaused;
    private IDungeonUserSettingsService naturalClearanceUserSettings;
    private bool naturalClearanceUserSettingsStateCaptured;
    private bool naturalClearanceDeveloperModeOriginallyEnabled;

    private sealed class NaturalClearanceSeedRunState
    {
        internal int SeedIndex;
        internal int DeterministicSeed;
        internal string RuntimeFacilityId = string.Empty;
        internal string DefinitionId = string.Empty;
        internal string WorkstationTag = string.Empty;
        internal string RecipeId = string.Empty;
        internal string OutputLineId = string.Empty;
        internal string ItemId = string.Empty;
        internal int OutputQuantity;
        internal long BatchMassGrams;
        internal string TopologySourceDigest = string.Empty;
        internal string RuntimeTopologyBeforeDigest = string.Empty;
        internal string RuntimeTopologyAfterDigest = string.Empty;
        internal string OwnerRosterKey = string.Empty;
        internal string RuntimeOperationId = string.Empty;
        internal long ActionEpochDelta;
        internal long ActionStartDelta;
        internal long HaulStartDelta;
        internal bool SchedulerProvenanceExact;
        internal bool DeliveryExact;
        internal IReadOnlyList<RandomStreamDiagnosticSnapshot> RandomBefore =
            Array.Empty<RandomStreamDiagnosticSnapshot>();
    }

    private sealed class NaturalClearanceSeedArtifactRow
    {
        internal const string Schema =
            "v27-production-output-clearance-natural-seed@2";

        internal NaturalClearanceSeedArtifactRow(
            NaturalClearanceSeedRunState run,
            FacilityOutputClearanceTelemetrySnapshot telemetry,
            FacilityOutputClearanceSampleSnapshot sample,
            string randomStateDigest,
            long randomDrawDelta)
        {
            if (run == null)
                throw new ArgumentNullException(nameof(run));
            SeedIndex = run.SeedIndex;
            DeterministicSeed = run.DeterministicSeed;
            DefinitionId = run.DefinitionId;
            WorkstationTag = run.WorkstationTag;
            RecipeId = run.RecipeId;
            OutputLineId = run.OutputLineId;
            ItemId = run.ItemId;
            OutputQuantity = run.OutputQuantity;
            BatchMassGrams = run.BatchMassGrams;
            TopologySourceDigest = run.TopologySourceDigest;
            TopologyStable = string.Equals(
                run.RuntimeTopologyBeforeDigest,
                run.RuntimeTopologyAfterDigest,
                StringComparison.Ordinal);
            FacilityAttributionExact = string.Equals(
                sample.FacilityId,
                run.RuntimeFacilityId,
                StringComparison.Ordinal);
            OwnerRosterKey = run.OwnerRosterKey;
            ObservationId =
                $"output-clearance-observation:{DefinitionId}:{WorkstationTag}:{DeterministicSeed}";
            BatchCommitId = sample.BatchCommitId;
            ActionEpochDelta = run.ActionEpochDelta;
            ActionStartDelta = run.ActionStartDelta;
            HaulStartDelta = run.HaulStartDelta;
            ClearanceMicroHours = sample.ClearanceMicroHours;
            ClearanceMilliHours = sample.ClearanceMilliHours;
            TelemetryCompletedCount = telemetry.Completed?.Count ?? 0;
            TelemetryActiveCount = telemetry.ActiveBatchCount;
            OrphanPickupCount = telemetry.OrphanPickupCount;
            ConflictingPublicationCount = telemetry.ConflictingPublicationCount;
            OverPickupCount = telemetry.OverPickupCount;
            CapacityExceededCount = telemetry.CapacityExceededCount;
            RestoreInterruptionCount = telemetry.RestoreInterruptionCount;
            TelemetryClean = telemetry.IsClean;
            SchedulerProvenanceExact = run.SchedulerProvenanceExact;
            DeliveryExact = run.DeliveryExact;
            RandomStateDigest = randomStateDigest ?? string.Empty;
            RandomDrawDelta = randomDrawDelta;

            CanonicalSemanticDigestBuilder source = new();
            source.Append(Schema);
            source.Append(SeedIndex);
            source.Append(DeterministicSeed);
            source.Append(DefinitionId);
            source.Append(WorkstationTag);
            source.Append(RecipeId);
            source.Append(OutputLineId);
            source.Append(ItemId);
            source.Append(OutputQuantity);
            source.Append(BatchMassGrams);
            source.Append(TopologySourceDigest);
            source.Append(OwnerRosterKey);
            source.Append(BatchCommitId);
            source.Append(RandomStateDigest);
            RunSourceDigest = source.ComputeSha256();
        }

        internal int SeedIndex { get; }
        internal int DeterministicSeed { get; }
        internal string DefinitionId { get; }
        internal string WorkstationTag { get; }
        internal string RecipeId { get; }
        internal string OutputLineId { get; }
        internal string ItemId { get; }
        internal int OutputQuantity { get; }
        internal long BatchMassGrams { get; }
        internal string TopologySourceDigest { get; }
        internal bool TopologyStable { get; }
        internal bool FacilityAttributionExact { get; }
        internal string OwnerRosterKey { get; }
        internal string ObservationId { get; }
        internal string BatchCommitId { get; }
        internal long ActionEpochDelta { get; }
        internal long ActionStartDelta { get; }
        internal long HaulStartDelta { get; }
        internal long ClearanceMicroHours { get; }
        internal long ClearanceMilliHours { get; }
        internal int TelemetryCompletedCount { get; }
        internal int TelemetryActiveCount { get; }
        internal int OrphanPickupCount { get; }
        internal int ConflictingPublicationCount { get; }
        internal int OverPickupCount { get; }
        internal int CapacityExceededCount { get; }
        internal int RestoreInterruptionCount { get; }
        internal bool TelemetryClean { get; }
        internal bool SchedulerProvenanceExact { get; }
        internal bool DeliveryExact { get; }
        internal string RandomStateDigest { get; }
        internal long RandomDrawDelta { get; }
        internal string RunSourceDigest { get; }

        internal bool IsExact => TopologyStable
            && FacilityAttributionExact
            && !string.IsNullOrWhiteSpace(BatchCommitId)
            && string.Equals(
                BatchCommitId,
                BatchCommitId.Trim(),
                StringComparison.Ordinal)
            && BatchMassGrams > 0L
            && ClearanceMicroHours > 0L
            && TelemetryCompletedCount == 1
            && TelemetryActiveCount == 0
            && OrphanPickupCount == 0
            && ConflictingPublicationCount == 0
            && OverPickupCount == 0
            && CapacityExceededCount == 0
            && RestoreInterruptionCount == 0
            && TelemetryClean
            && SchedulerProvenanceExact
            && DeliveryExact
            && IsNaturalClearanceLowercaseSha256(TopologySourceDigest)
            && IsNaturalClearanceLowercaseSha256(RandomStateDigest)
            && IsNaturalClearanceLowercaseSha256(RunSourceDigest);
    }

    private sealed class NaturalClearanceActorProbe
    {
        internal NaturalClearanceActorProbe(
            CharacterActor actor,
            AIBrain brain,
            AIHaul action,
            AbilityHaul haul)
        {
            Actor = actor;
            Brain = brain;
            Action = action;
            Haul = haul;
            CharacterId = actor.BuildingCharacterId.Value;
            StartEpoch = brain.RuntimeActionEpoch;
            StartCount = brain.RuntimeActionStartCount;
            HaulStartCount = haul.RuntimeHaulStartCount;
        }

        internal CharacterActor Actor { get; }
        internal AIBrain Brain { get; }
        internal AIHaul Action { get; }
        internal AbilityHaul Haul { get; }
        internal string CharacterId { get; }
        internal long StartEpoch { get; }
        internal long StartCount { get; }
        internal long HaulStartCount { get; }
    }

    private sealed class NaturalClearanceExpectedSlice
    {
        internal NaturalClearanceExpectedSlice(
            string stackId,
            string itemId,
            int quantity,
            long massGrams)
        {
            if (string.IsNullOrWhiteSpace(stackId)
                || !string.Equals(stackId, stackId.Trim(), StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(itemId)
                || !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal)
                || quantity <= 0
                || massGrams <= 0L)
            {
                throw new ArgumentException(
                    "Natural-clearance expected slice is invalid.");
            }
            StackId = stackId;
            ItemId = itemId;
            Quantity = quantity;
            MassGrams = massGrams;
        }

        internal string StackId { get; }
        internal string ItemId { get; }
        internal int Quantity { get; }
        internal long MassGrams { get; }
    }

    private sealed class NaturalClearanceAdmissionWitness
    {
        internal NaturalClearanceAdmissionWitness(
            string haulOperationId,
            int admissionIndex,
            WarehouseHaulAdmissionSaveData admission)
        {
            HaulOperationId = haulOperationId ?? string.Empty;
            AdmissionIndex = admissionIndex;
            TokenId = admission?.tokenId ?? string.Empty;
            OwnerAdmissionOperationId = admission?.ownerAdmissionOperationId
                ?? string.Empty;
            WarehouseId = admission?.warehouseId ?? string.Empty;
            SourceStackId = admission?.sourceStackId ?? string.Empty;
            ItemId = admission?.itemId ?? string.Empty;
            ItemInstanceId = admission?.itemInstanceId ?? string.Empty;
            LotFingerprint = admission?.lotFingerprint ?? string.Empty;
            Quantity = admission?.quantity ?? 0;
            ReservedMassGrams = admission?.reservedMassGrams ?? 0L;
            CatalogRevision = admission?.catalogRevision ?? 0L;
            SourceRevision = admission?.sourceRevision ?? 0L;
        }

        internal string HaulOperationId { get; }
        internal int AdmissionIndex { get; }
        internal string TokenId { get; }
        internal string OwnerAdmissionOperationId { get; }
        internal string WarehouseId { get; }
        internal string SourceStackId { get; }
        internal string ItemId { get; }
        internal string ItemInstanceId { get; }
        internal string LotFingerprint { get; }
        internal int Quantity { get; }
        internal long ReservedMassGrams { get; }
        internal long CatalogRevision { get; }
        internal long SourceRevision { get; }

        internal bool Matches(
            string haulOperationId,
            int admissionIndex,
            WarehouseHaulAdmissionSaveData admission) =>
            admission != null
            && AdmissionIndex == admissionIndex
            && string.Equals(
                HaulOperationId,
                haulOperationId,
                StringComparison.Ordinal)
            && string.Equals(TokenId, admission.tokenId, StringComparison.Ordinal)
            && string.Equals(
                OwnerAdmissionOperationId,
                admission.ownerAdmissionOperationId,
                StringComparison.Ordinal)
            && string.Equals(
                WarehouseId,
                admission.warehouseId,
                StringComparison.Ordinal)
            && string.Equals(
                SourceStackId,
                admission.sourceStackId,
                StringComparison.Ordinal)
            && string.Equals(ItemId, admission.itemId, StringComparison.Ordinal)
            && string.Equals(
                ItemInstanceId,
                admission.itemInstanceId,
                StringComparison.Ordinal)
            && string.Equals(
                LotFingerprint,
                admission.lotFingerprint,
                StringComparison.Ordinal)
            && Quantity == admission.quantity
            && ReservedMassGrams == admission.reservedMassGrams
            && CatalogRevision == admission.catalogRevision
            && SourceRevision == admission.sourceRevision;
    }

    private bool TryEnsureNaturalPortfolioResearchSpace(
        DungeonRuntimeLifetimeScope scope,
        GridSystemManager gridSystem,
        out Grid grid)
    {
        grid = gridSystem != null ? gridSystem.grid : null;
        DungeonSpaceExpansionRuntime expansion =
            Resolve<DungeonSpaceExpansionRuntime>(scope);
        DungeonInteriorLayoutSnapshot beforeLayout = default;
        string beforeCaptureFailure = "expansion-runtime-missing";
        bool beforeCaptured = expansion != null
            && expansion.TryCaptureLayout(
                out beforeLayout,
                out beforeCaptureFailure);
        DungeonSpaceExpansionDefinition target =
            DungeonSpaceExpansionCatalog.All[
                DungeonSpaceExpansionCatalog.All.Count - 1];
        ProgressionSceneRuntimeReferences progression =
            Resolve<ProgressionSceneRuntimeReferences>(scope);
        bool researchAuthorityReady = progression?.BlueprintResearch != null;
        bool researchCompletionReady = researchAuthorityReady;
        if (researchAuthorityReady)
        {
            foreach (DungeonSpaceExpansionDefinition definition in
                     DungeonSpaceExpansionCatalog.All.OrderBy(value => value.Tier))
            {
                if (!progression.BlueprintResearch
                    .TryCompleteProjectImmediatelyForVerification(
                        new ResearchProjectId(definition.ResearchProjectId),
                        out string completionFailure))
                {
                    researchCompletionReady = false;
                    Debug.LogError(
                        "V27_NATURAL_RESEARCH_COMPLETION_FAILED "
                        + completionFailure);
                    break;
                }
            }
        }
        bool researchAligned = researchCompletionReady
            && DungeonSpaceExpansionCatalog.All.All(definition =>
                progression.BlueprintResearch.State.Projects.IsCompleted(
                    new ResearchProjectId(definition.ResearchProjectId)));
        DungeonSpaceExpansionResult result = default;
        string applyFailure = "expansion-runtime-missing";
        bool applied = expansion != null
            && researchAligned
            && expansion.TryApply(
                target,
                out result,
                out applyFailure);
        grid = gridSystem != null ? gridSystem.grid : null;
        DungeonInteriorLayoutSnapshot layout = default;
        string captureFailure = "expansion-runtime-missing";
        bool captured = expansion != null
            && expansion.TryCaptureLayout(
                out layout,
                out captureFailure);
        bool exact = beforeCaptured
            && researchAligned
            && applied
            && captured
            && grid != null
            && layout.ColumnCount == target.TargetInteriorColumns;
        Check(
            exact,
            "NATURAL_PORTFOLIO_RESEARCH_SPACE_READY",
            exact
                ? $"research={target.ResearchProjectId};tier={target.Tier};"
                  + $"columns={layout.ColumnCount};grid={grid.width}x{grid.height};"
                  + $"changed={result.Changed}"
                : $"research={target.ResearchProjectId};"
                  + $"researchAuthority={researchAuthorityReady};"
                  + $"researchAligned={researchAligned};"
                  + $"beforeCaptured={beforeCaptured};"
                  + $"beforeFailure={beforeCaptureFailure};"
                  + $"applied={applied};applyFailure={applyFailure};"
                  + $"captured={captured};captureFailure={captureFailure};"
                  + $"grid={(grid != null ? grid.width + "x" + grid.height : "missing")}");
        if (!exact)
            return false;

        bool hallwaysReady = TryProvisionNaturalPortfolioHallways(
            scope,
            grid,
            beforeLayout.EndExclusiveX,
            out int createdHallways,
            out int reachableCells,
            out int reachableExpandedCells,
            out string hallwayFailure);
        Check(
            hallwaysReady,
            "NATURAL_PORTFOLIO_AUTHORED_HALLWAYS_READY",
            hallwaysReady
                ? $"created={createdHallways};reachable={reachableCells};"
                  + $"expandedReachable={reachableExpandedCells};"
                  + $"expandedStart={beforeLayout.EndExclusiveX};"
                  + $"grid={grid.width}x{grid.height}"
                : hallwayFailure);
        return hallwaysReady;
    }

    private bool TryProvisionNaturalPortfolioHallways(
        DungeonRuntimeLifetimeScope scope,
        Grid grid,
        int expandedStartX,
        out int createdHallways,
        out int reachableCells,
        out int reachableExpandedCells,
        out string failureReason)
    {
        createdHallways = 0;
        reachableCells = 0;
        reachableExpandedCells = 0;
        failureReason = string.Empty;
        IGridBuildingObjectFactory factory =
            Resolve<IGridBuildingObjectFactory>(scope);
        IGameSessionStateProvider session =
            Resolve<IGameSessionStateProvider>(scope);
        ProgressionSceneRuntimeReferences progression =
            Resolve<ProgressionSceneRuntimeReferences>(scope);
        BuildingSO hallway = FindBuildingAsset(value => value != null
            && value.id == StarterBuildingDefinitionIds.Hallway
            && value.Placement.Layer == GridLayer.Hallway);
        const int InteriorDoorDefinitionId = 8;
        BuildingSO interiorDoor = FindBuildingAsset(value => value != null
            && value.id == InteriorDoorDefinitionId
            && value.IsInteriorDoor);
        if (grid == null
            || factory == null
            || session == null
            || progression == null
            || hallway == null
            || interiorDoor == null)
        {
            failureReason =
                $"authored-hallway-authority-invalid:grid={grid != null};"
                + $"factory={factory != null};session={session != null};"
                + $"progression={progression != null};"
                + $"hallway={hallway != null};door={interiorDoor != null}";
            return false;
        }

        CharacterActor actor = FindHauler();
        if (actor == null)
        {
            failureReason = "authored-hallway-hauler-missing";
            return false;
        }

        BuildingPlacementValidator validator = new(
            new GridPlacementValidator(),
            () =>
            {
                GameSessionState gameData = null;
                session.TryGetSessionState(out gameData);
                return new BuildingConditionContext(
                    gameData,
                    progression.BlueprintResearch.State,
                    null,
                    NaturalPortfolioSpaceFixtureDebugRules.Instance);
            });
        GridBuildingFactory buildingFactory = new(
            null,
            building =>
            {
                if (building == null)
                {
                    return;
                }
                temporaryObjects.Add(building.gameObject);
                InjectGameObject(scope, building.gameObject);
            },
            factory);
        GridBuildingPlacementService placement = new(
            grid,
            hallway,
            id => FindBuildingAsset(value => value != null && value.id == id),
            buildingFactory,
            validator,
            workOrderRuntime: null);

        Vector2Int actorPosition = actor.GetNowXY();
        BuildableObject boundaryWall = null;
        for (int x = actorPosition.x + 1; x < grid.width; x++)
        {
            GridCell cell = grid.GetGridCell(new Vector2Int(x, actorPosition.y));
            BuildableObject candidate =
                cell?.GetOccupant(GridLayer.Building) as BuildableObject;
            if (candidate != null
                && candidate.BuildingData != null
                && candidate.BuildingData.IsStructuralWall)
            {
                boundaryWall = candidate;
                break;
            }
        }
        if (boundaryWall == null)
        {
            failureReason = "authored-hallway-boundary-wall-missing";
            return false;
        }

        int corridorY = actorPosition.y;
        int corridorStartX = boundaryWall.centerPos.x + 1;
        int corridorEndExclusiveX = grid.width - 1;
        for (int x = corridorStartX; x < corridorEndExclusiveX; x++)
        {
            Vector2Int position = new(x, corridorY);
            GridCell cell = grid.GetGridCell(position);
            if (cell == null
                || cell.AreaType != GridCellAreaType.DungeonInterior)
            {
                failureReason =
                    $"authored-hallway-corridor-area-invalid:{position.x}:{position.y}";
                return false;
            }
            if (cell.HasOccupantInLayer(GridLayer.Hallway))
            {
                continue;
            }
            if (!placement.CanPlaceBuilding(
                    hallway,
                    position,
                    out string canBuildFailure))
            {
                failureReason =
                    $"authored-hallway-validator-rejected:{position.x}:{position.y}:"
                    + canBuildFailure;
                return false;
            }
            if (!placement.TryPlaceBuildingImmediateUnchecked(
                    hallway,
                    position,
                    chargeCost: false,
                    out string placementFailure))
            {
                failureReason =
                    $"authored-hallway-placement-failed:{position.x}:{position.y}:"
                    + placementFailure;
                return false;
            }
            createdHallways++;
        }

        Vector2Int doorPosition = boundaryWall.centerPos;
        if (!placement.CanPlaceBuilding(
                interiorDoor,
                doorPosition,
                out string doorValidationFailure))
        {
            failureReason =
                $"authored-hallway-door-validator-rejected:{doorPosition.x}:"
                + $"{doorPosition.y}:{doorValidationFailure}";
            return false;
        }
        if (!placement.TryPlaceBuildingImmediateUnchecked(
                interiorDoor,
                doorPosition,
                chargeCost: false,
                out string doorPlacementFailure))
        {
            failureReason =
                $"authored-hallway-door-placement-failed:{doorPosition.x}:"
                + $"{doorPosition.y}:{doorPlacementFailure}";
            return false;
        }

        Vector2Int[] reachable = grid.SearchPath(actorPosition)
            .GetReachablePositions()
            .Where(position => grid.IsValidGridPos(position)
                && grid.IsWalkable(position))
            .Distinct()
            .ToArray();
        reachableCells = reachable.Length;
        reachableExpandedCells = reachable.Count(position =>
            position.x >= expandedStartX);
        const int MinimumExpandedReachableCells = 12;
        if (reachableExpandedCells < MinimumExpandedReachableCells)
        {
            failureReason =
                $"authored-hallway-expanded-reachability-insufficient:"
                + $"expanded={reachableExpandedCells};"
                + $"required={MinimumExpandedReachableCells};"
                + $"total={reachableCells};expandedStart={expandedStartX};"
                + $"door={doorPosition.x}:{doorPosition.y}";
            return false;
        }
        return true;
    }

    private sealed class NaturalPortfolioSpaceFixtureDebugRules :
        IDungeonDebugRuleQuery
    {
        internal static readonly NaturalPortfolioSpaceFixtureDebugRules Instance =
            new();

        public bool IsExecutingCommand => false;
        public bool IsEnabled(DungeonDebugCheat cheat) =>
            cheat == DungeonDebugCheat.IgnoreUnlocks;
        public bool ShouldFreezeNeed(
            CharacterCondition condition,
            float delta) => false;
        public bool ShouldBlockFriendlyDamage(CharacterActor actor) => false;
        public bool ShouldBlockFacilityDamage(bool damaged) => false;
        public bool ShouldSkipCosts() => false;
    }

    public IEnumerator Run()
    {
        Directory.CreateDirectory("Artifacts/QA");
        constructionOnly = File.Exists(PhysicalItemLogisticsPlayModeVerifier.ConstructionRequestPath);
        l02Only = File.Exists(PhysicalItemLogisticsPlayModeVerifier.L02RequestPath);
        productionInputMassOnly = File.Exists(
            PhysicalItemLogisticsPlayModeVerifier.ProductionInputMassRequestPath);
        equipmentRepairOnly = File.Exists(
            PhysicalItemLogisticsPlayModeVerifier.EquipmentRepairRequestPath);
        preparedOutputWarehouseOnly = File.Exists(
            PhysicalItemLogisticsPlayModeVerifier.PreparedOutputWarehouseRequestPath);
        naturalOutputPortfolioOnly = File.Exists(
            PhysicalItemLogisticsPlayModeVerifier.NaturalOutputPortfolioRequestPath);
        naturalClearanceOnly = naturalOutputPortfolioOnly
            || (preparedOutputWarehouseOnly
                && PhysicalItemLogisticsPlayModeVerifier
                    .TryReadPreparedOutputWarehouseCase(
                        out PreparedOutputLiveRouteCase earlyPreparedOutputCase,
                        out _)
                && earlyPreparedOutputCase.VerifiesClearanceMeasurement);
        Application.logMessageReceived += OnLogMessageReceived;
        EnsureEventSystem();
        SetupInput();
        originalTimeScale = Time.timeScale;
        Time.timeScale = 8f;
        if (naturalOutputPortfolioOnly
            && !PhysicalItemLogisticsPlayModeVerifier
                .TryValidateNaturalOutputPortfolioRequest(
                    out string naturalRequestFailure))
        {
            failures.Add(naturalRequestFailure);
            Finish();
            yield break;
        }

        yield return EnsureProductBoot();
        if (!productBootSucceeded)
        {
            Finish();
            yield break;
        }
        if (!VerifyProductGameplayFacilityAuthorityBaseline())
        {
            Finish();
            yield break;
        }

        DungeonRuntimeLifetimeScope scope = null;
        OwnerRunManager authoredOwnerManager = null;
        string compositionDetail = "runtime composition was not observed";
        for (int turn = 0; turn <= VerificationBootMaximumTurns; turn++)
        {
            if (TryFindReadyComposition(
                    out scope,
                    out authoredOwnerManager,
                    out compositionDetail))
            {
                break;
            }
            if (turn == VerificationBootMaximumTurns)
                break;
            yield return null;
        }
        Check(
            scope != null && authoredOwnerManager != null,
            "RUNTIME_COMPOSITION_READY",
            compositionDetail);

        itemRuntime = Resolve<IWorldItemStackRuntime>(scope);
        IWorkOrderRuntime workOrderRuntime = Resolve<IWorkOrderRuntime>(scope);
        ICombatEquipmentRuntime equipment = Resolve<ICombatEquipmentRuntime>(scope);
        ICombatEquipmentMaintenanceRuntime equipmentMaintenance =
            Resolve<ICombatEquipmentMaintenanceRuntime>(scope);
        IResourceEconomyContentCatalog economyCatalog =
            Resolve<IResourceEconomyContentCatalog>(scope);
        IOffensePreparationService preparation = Resolve<IOffensePreparationService>(scope);
        IFacilityBufferDestinationClaimQuery destinationClaims =
            Resolve<IFacilityBufferDestinationClaimQuery>(scope);
        IWarehouseMassAdmissionService warehouseMassAdmission =
            Resolve<IWarehouseMassAdmissionService>(scope);
        debugMode = Resolve<IDungeonDebugModeService>(scope);
        GridSystemManager gridSystem = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>();
        Grid grid = gridSystem != null ? gridSystem.grid : null;

        Check(scope != null && scope.Container != null, "SCOPE_READY", "gameplay LifetimeScope resolved");
        Check(itemRuntime != null, "ITEM_RUNTIME_READY", "world item runtime resolved");
        Check(workOrderRuntime != null, "WORK_ORDER_RUNTIME_READY", "work order runtime resolved");
        Check(equipment != null, "EQUIPMENT_RUNTIME_READY", "common combat equipment runtime resolved");
        Check(equipmentMaintenance != null,
            "EQUIPMENT_MAINTENANCE_READY",
            "equipment maintenance runtime resolved");
        Check(economyCatalog != null, "ECONOMY_CATALOG_READY", "resource economy catalog resolved");
        Check(preparation != null, "PREPARATION_RUNTIME_READY", "offense preparation service resolved");
        Check(destinationClaims != null,
            "DESTINATION_CLAIM_RUNTIME_READY",
            "haul destination claim authority resolved");
        Check(warehouseMassAdmission != null,
            "WAREHOUSE_MASS_ADMISSION_RUNTIME_READY",
            "warehouse gram admission authority resolved");
        Check(debugMode != null, "DEBUG_MODE_READY", "debug mode service resolved");
        Check(grid != null, "GRID_READY", "grid resolved");
        if (scope == null
            || itemRuntime == null
            || workOrderRuntime == null
            || equipment == null
            || equipmentMaintenance == null
            || economyCatalog == null
            || preparation == null
            || destinationClaims == null
            || warehouseMassAdmission == null
            || debugMode == null
            || grid == null)
        {
            Finish();
            yield break;
        }

        yield return EnsurePlayableRun();
        if (naturalOutputPortfolioOnly
            && !TryEnsureNaturalPortfolioResearchSpace(
                scope,
                gridSystem,
                out grid))
        {
            Finish();
            yield break;
        }
        CharacterActor hauler = FindHauler();
        Check(hauler != null, "HAULER_READY", hauler != null ? hauler.name : "no staff/owner hauler");
        if (hauler == null)
        {
            Finish();
            yield break;
        }

        CaptureRuntimeState(itemRuntime, equipment);
        ConfigureVerificationDebugMode();
        if (naturalClearanceOnly)
        {
            ConfigureNaturalClearanceAiMeasurement();
            yield return QuiesceNaturalClearanceAiPoolBeforeFixture();
        }
        else
            DisableBrainForDeterministicHauling(hauler);
        yield return null;
        yield return null;
        if (naturalClearanceOnly)
        {
            Check(verificationActors.Any(actor => actor != null
                    && !actor.IsDead
                    && actor.IsAiPaused()
                    && actor.Brain?.LogisticsMeasurementOnlyForDiagnostics == true),
                "OUTPUT_CLEARANCE_NATURAL_AI_POOL_READY",
                $"actors={verificationActors.Count}; "
                + string.Join(",", verificationActors
                    .Where(actor => actor != null)
                    .Select(actor =>
                        $"{actor.BuildingCharacterId}:paused={actor.IsAiPaused()}:"
                        + $"logisticsOnly="
                        + $"{actor.Brain?.LogisticsMeasurementOnlyForDiagnostics == true}:"
                        + $"haul={actor.GetComponent<AbilityHaul>()?.IsHauling == true}")));
        }
        else
        {
            Check(verificationActors
                    .Where(actor => actor != null && !actor.IsDead)
                    .All(actor => actor.IsAiPaused()
                        && actor.GetComponent<AbilityMove>()
                            ?.HasActiveMovementRoutineForDiagnostics != true
                        && actor.GetComponent<AbilityHaul>()?.IsHauling != true),
                "HAUL_FIXTURE_AI_OWNERSHIP_ISOLATED",
                $"actors={verificationActors.Count}; "
                + string.Join(",", verificationActors
                    .Where(actor => actor != null)
                    .Select(actor =>
                        $"{actor.BuildingCharacterId}:paused={actor.IsAiPaused()}:"
                        + $"move={actor.GetComponent<AbilityMove>()?.HasActiveMovementRoutineForDiagnostics == true}:"
                        + $"haul={actor.GetComponent<AbilityHaul>()?.IsHauling == true}")));
        }

        if (l02Only)
        {
            try
            {
                itemRuntime.Restore(new DungeonPhysicalItemSaveData());
                CharacterCarryInventory.Ensure(hauler)?.RemoveAllItems();
                yield return VerifyL02MassAdmissionAndPickupRejection(
                    scope,
                    itemRuntime,
                    warehouseMassAdmission,
                    destinationClaims,
                    grid,
                    hauler);
            }
            finally
            {
                RestoreRuntimeState(itemRuntime, equipment);
            }

            yield return null;
            Finish();
            yield break;
        }


        if (productionInputMassOnly)
        {
            try
            {
                itemRuntime.Restore(new DungeonPhysicalItemSaveData());
                CharacterCarryInventory.Ensure(hauler)?.RemoveAllItems();
                yield return VerifyProductionInputBufferMassAdmission(
                    scope,
                    itemRuntime,
                    grid,
                    hauler);
            }
            finally
            {
                RestoreRuntimeState(itemRuntime, equipment);
            }

            yield return null;
            Finish();
            yield break;
        }

        if (preparedOutputWarehouseOnly)
        {
            bool requestReady = PhysicalItemLogisticsPlayModeVerifier
                .TryReadPreparedOutputWarehouseCase(
                    out preparedOutputCase,
                    out string requestFailure);
            Check(requestReady,
                "PREPARED_OUTPUT_REQUEST_CANONICAL",
                requestReady
                    ? $"synthetic={preparedOutputCase.IsSynthetic}; recipe="
                        + $"{preparedOutputCase.RecipeId}; item={preparedOutputCase.ItemId}"
                    : requestFailure);
            if (!requestReady)
            {
                Finish();
                yield break;
            }
            string transactionIdentityFailure = string.Empty;
            bool transactionIdentityReady = !preparedOutputCase.IsSynthetic
                || SyntheticPreparedOutputCanaryAssetTransaction
                    .TryValidateActiveRunIdentity(
                        preparedOutputCase,
                        out transactionIdentityFailure);
            Check(transactionIdentityReady,
                "PREPARED_OUTPUT_CANARY_TRANSACTION_IDENTITY_EXACT",
                transactionIdentityReady
                    ? $"nonce={preparedOutputCase.TransactionNonce}; itemGuid="
                        + $"{preparedOutputCase.ItemAssetGuid}; recipeGuid="
                        + $"{preparedOutputCase.RecipeAssetGuid}; itemCatalog="
                        + $"{preparedOutputCase.AugmentedItemCatalogSha256}; domainCatalog="
                        + preparedOutputCase.AugmentedDomainCatalogSha256
                    : transactionIdentityFailure);
            if (!transactionIdentityReady)
            {
                Finish();
                yield break;
            }

            IDungeonSaveSectionRegistry preparedSaveRegistry =
                Resolve<IDungeonSaveSectionRegistry>(scope);
            List<DungeonSaveSectionEnvelope> preparedSceneBaseline =
                preparedOutputCase.RequiresSanitizedScene
                    ? preparedSaveRegistry?.CaptureAll()
                    : null;
            bool syntheticBaselineReady = preparedSaveRegistry != null
                && (!preparedOutputCase.RequiresSanitizedScene
                    || preparedSceneBaseline != null);
            Check(syntheticBaselineReady,
                "PREPARED_OUTPUT_CANARY_PRE_QUARANTINE_BASELINE_READY",
                syntheticBaselineReady
                    ? $"synthetic={preparedOutputCase.IsSynthetic}; sections="
                        + (preparedSceneBaseline?.Count ?? 0)
                    : "save registry or baseline missing");
            if (!syntheticBaselineReady)
            {
                Finish();
                yield break;
            }

            WorldItemStackSnapshot[] preexistingStacks = itemRuntime.GetAllStacks()
                .Where(value => value != null)
                .ToArray();
            IFacilityOutputExactRouteOutboxQuery exactRoutes =
                Resolve<IFacilityOutputExactRouteOutboxQuery>(scope);
            CharacterActor[] fixtureActors = CharacterActorCollection
                .DistinctByGameObject(
                    UnityEngine.Object.FindObjectsByType<CharacterActor>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None))
                .Where(actor => actor != null && !actor.IsDead)
                .ToArray();
            int custodyStackCount = preexistingStacks.Count(stack =>
                (stack.Components ?? Array.Empty<ItemInstanceComponentSaveData>())
                .Any(component => component != null
                    && string.Equals(
                        component.componentTypeId,
                        PreparedOutputCustodyComponentTypeId,
                        StringComparison.Ordinal)));
            int pendingRouteCount = exactRoutes?.CapturePendingRoutes()?.Count ?? -1;
            int committedHaulCount = fixtureActors.Count(actor =>
                actor.GetComponent<AbilityHaul>()?.CaptureDeliveryIntentForSave()
                    != null);
            int activeHaulerCount = fixtureActors.Count(actor =>
                actor.GetComponent<AbilityHaul>()?.IsHauling == true);
            bool fixtureBoundaryClear = exactRoutes != null
                && custodyStackCount == 0
                && pendingRouteCount == 0
                && committedHaulCount == 0
                && activeHaulerCount == 0;
            Check(fixtureBoundaryClear,
                "PREPARED_OUTPUT_FIXTURE_BOUNDARY_CLEAR",
                $"stacks={preexistingStacks.Length}; custody={custodyStackCount}; "
                + $"routes={pendingRouteCount}; committedHauls={committedHaulCount}; "
                + $"activeHaulers={activeHaulerCount}");
            if (!fixtureBoundaryClear)
            {
                Finish();
                yield break;
            }

            int quarantineFailures = 0;
            foreach (WorldItemStackSnapshot existing in preexistingStacks)
            {
                if (!itemRuntime.SetForbidden(existing.StackId, true))
                {
                    quarantineFailures++;
                }
            }
            Check(quarantineFailures == 0,
                "PREPARED_OUTPUT_FIXTURE_EXISTING_STACKS_QUARANTINED",
                $"stacks={preexistingStacks.Length}; failures={quarantineFailures}");
            if (quarantineFailures != 0)
            {
                RestorePreparedOutputCanaryBaseline(
                    preparedSaveRegistry,
                    preparedSceneBaseline);
                Finish();
                yield break;
            }

            if (!preparedOutputCase.VerifiesClearanceMeasurement)
                QuiesceHaulingBeforeDirectStateFixture();
            CharacterCarryInventory carry = CharacterCarryInventory.Ensure(hauler);
            Check(carry?.Items.Count == 0,
                "PREPARED_OUTPUT_FIXTURE_CARRY_EMPTY",
                $"carried={carry?.Items.Count ?? -1}");
            if (carry?.Items.Count != 0)
            {
                RestorePreparedOutputCanaryBaseline(
                    preparedSaveRegistry,
                    preparedSceneBaseline);
                Finish();
                yield break;
            }
            yield return VerifyPreparedOutputWarehouseLiveRoute(
                scope,
                itemRuntime,
                grid,
                hauler,
                warehouseMassAdmission,
                preparedOutputCase,
                preparedSaveRegistry,
                preparedSceneBaseline);
            yield return null;
            Finish();
            yield break;
        }

        if (naturalOutputPortfolioOnly)
        {
            yield return VerifyNaturalOutputPortfolio(scope);
            Finish();
            yield break;
        }

        try
        {
            itemRuntime.Restore(new DungeonPhysicalItemSaveData());
            CharacterCarryInventory.Ensure(hauler)?.RemoveAllItems();

            Vector2Int actorPos = hauler.GetNowXY();
            IReadOnlyList<Vector2Int> positions = FindReachableCells(grid, actorPos, 48);
            Check(positions.Count >= 3, "REACHABLE_TEST_CELLS", $"count={positions.Count}; actor={actorPos}");
            if (positions.Count < 3)
            {
                Finish();
                yield break;
            }

            BuildingSO warehouseAsset = FindWarehouseAsset();
            BuildingSO benchAsset = FindCraftBenchAsset();
            bool warehouseCellFound = TryFindRegisterablePosition(
                grid,
                warehouseAsset,
                positions,
                out Vector2Int warehousePosition);
            HashSet<Vector2Int> warehouseFootprint = warehouseCellFound
                ? warehouseAsset.GetGridPosList(warehousePosition).ToHashSet()
                : new HashSet<Vector2Int>();
            Vector2Int[] remainingPositions = positions
                .Where(position => !warehouseFootprint.Contains(position))
                .ToArray();
            Vector2Int benchPosition = remainingPositions.FirstOrDefault();
            Vector2Int testPosition = remainingPositions.Skip(1).FirstOrDefault();
            Facility warehouse = CreateInjectedFacility(
                scope,
                grid,
                warehouseAsset,
                warehousePosition,
                "QA_Physical_Logistics_Warehouse",
                registerOnGrid: true);
            Facility bench = CreateInjectedFacility(
                scope,
                grid,
                benchAsset,
                benchPosition,
                "QA_Physical_Logistics_Bench");
            Check(warehouseCellFound && remainingPositions.Length >= 2,
                "TEMP_WAREHOUSE_GRID_CELL_READY",
                $"found={warehouseCellFound}; remaining={remainingPositions.Length}; warehouse={warehousePosition}");
            Check(warehouse != null && warehouse.Inventory != null,
                "TEMP_WAREHOUSE_READY",
                warehouse != null ? warehouse.name : "missing warehouse");
            Check(bench != null && bench.BuildingData != null
                    && bench.BuildingData.GetAbility<BuildingEquipmentCraftingAbility>() != null,
                "TEMP_CRAFT_BENCH_READY",
                bench != null ? bench.name : "missing bench");
            if (warehouse == null || warehouse.Inventory == null || bench == null)
            {
                Finish();
                yield break;
            }

            ClearInventory(warehouse.Inventory);
            if (equipmentRepairOnly)
            {
                Check(SeedStoredCraftMaterial(
                        itemRuntime,
                        economyCatalog,
                        warehouse,
                        "material:blacksteel",
                        8,
                        out string repairMaterialSeedDetails),
                    "MATERIAL_REPAIR_STOCK_SEEDED",
                    repairMaterialSeedDetails);
                yield return VerifyMaterialRepairAndSalvage(
                    itemRuntime,
                    equipment,
                    equipmentMaintenance,
                    economyCatalog,
                    destinationClaims,
                    scope,
                    grid,
                    hauler,
                    warehouse,
                    warehouse.centerPos);
                yield return null;
                Finish();
                yield break;
            }

            long warehouseRevisionBeforeSeed =
                warehouseMassAdmission.GetWarehouseCapacityRevision(
                    warehouse.PersistentInstanceId);
            if (constructionOnly)
            {
                Check(SeedStoredCraftMaterial(
                        itemRuntime,
                        economyCatalog,
                        warehouse,
                        "material:wood",
                        4,
                        out string woodSeedDetails),
                    "CONSTRUCTION_WOOD_SEEDED",
                    woodSeedDetails);
                yield return VerifyConstructionMaterialDelivery(
                    itemRuntime,
                    workOrderRuntime,
                    scope,
                    grid,
                    hauler,
                    warehouse,
                    testPosition);
            }
            else
            {
            Check(itemRuntime.SpawnStockInWarehouse(
                        warehouse,
                        StockCategory.General,
                        4,
                        out int seededGeneral)
                    && itemRuntime.SpawnStockInWarehouse(
                        warehouse,
                        StockCategory.Food,
                        5,
                        out int seededFood)
                    && seededGeneral == 4
                    && seededFood == 5,
                "TEMP_WAREHOUSE_SEEDED",
                $"food={warehouse.Inventory.GetStock(StockCategory.Food)}; general={warehouse.Inventory.GetStock(StockCategory.General)}; weapon={warehouse.Inventory.GetStock(StockCategory.Weapon)}");
            Check(
                warehouseMassAdmission.GetWarehouseCapacityRevision(
                    warehouse.PersistentInstanceId) > warehouseRevisionBeforeSeed
                && warehouse.Inventory.StoredMassGrams > 0L
                && warehouse.Inventory.ReservedInboundMassGrams == 0L,
                "WAREHOUSE_MASS_ADMISSION_PRODUCTION_INGRESS_COMMITTED",
                $"revision={warehouseRevisionBeforeSeed}->"
                + $"{warehouseMassAdmission.GetWarehouseCapacityRevision(warehouse.PersistentInstanceId)}; "
                + $"stored={warehouse.Inventory.StoredMassGrams}; "
                + $"reserved={warehouse.Inventory.ReservedInboundMassGrams}");
            IBuildingSummaryFormatter buildingSummary =
                scope.Container.Resolve<IBuildingSummaryFormatter>();
            BuildingSummaryPresentation warehousePresentation =
                buildingSummary.Format(warehouse);
            Check(warehousePresentation.StockText.Contains(
                        "12kg/25kg",
                        StringComparison.Ordinal)
                    && !warehousePresentation.StockText.Contains(
                        "/60",
                        StringComparison.Ordinal),
                "WAREHOUSE_MASS_UI_PRODUCTION_EXACT_KG",
                warehousePresentation.StockText.Replace('\n', ' '));
            IDungeonGridBuildingControllerProvider buildingControllerProvider =
                scope.Container.Resolve<IDungeonGridBuildingControllerProvider>();
            bool nonEmptyWarehouseDestroyed =
                buildingControllerProvider.Controller.TryDestroyBuilding(
                    warehouse,
                    out string nonEmptyDestroyFailure);
            Check(!nonEmptyWarehouseDestroyed
                    && !warehouse.isDestroy
                    && warehouse.Inventory.StoredMassGrams > 0L
                    && nonEmptyDestroyFailure.Contains(
                        "warehouse-lifecycle-not-empty",
                        StringComparison.Ordinal),
                "WAREHOUSE_NONEMPTY_DEMOLITION_REJECTED",
                $"destroyed={nonEmptyWarehouseDestroyed}; "
                + $"stored={warehouse.Inventory.StoredMassGrams}; "
                + $"failure={nonEmptyDestroyFailure}");
            IFacilityRelocationWorldService relocationWorld =
                scope.Container.Resolve<IFacilityRelocationWorldService>();
            bool nonEmptyWarehouseRelocatable = relocationWorld.CanRelocate(
                warehouse,
                testPosition,
                out string nonEmptyRelocationFailure);
            Check(!nonEmptyWarehouseRelocatable
                    && !warehouse.isDestroy
                    && warehouse.Inventory.StoredMassGrams > 0L
                    && nonEmptyRelocationFailure.Contains(
                        "warehouse-lifecycle-not-empty",
                        StringComparison.Ordinal),
                "WAREHOUSE_NONEMPTY_RELOCATION_REJECTED",
                $"relocatable={nonEmptyWarehouseRelocatable}; "
                + $"stored={warehouse.Inventory.StoredMassGrams}; "
                + $"failure={nonEmptyRelocationFailure}");
            Check(SeedStoredCraftMaterial(
                    itemRuntime,
                    economyCatalog,
                    warehouse,
                    "material:iron",
                    6,
                    out string materialSeedDetails),
                "TEMP_WAREHOUSE_IRON_SEEDED",
                materialSeedDetails);
            Check(SeedStoredCraftMaterial(
                    itemRuntime,
                    economyCatalog,
                    warehouse,
                    "material:wood",
                    4,
                    out string woodSeedDetails),
                "TEMP_WAREHOUSE_WOOD_SEEDED",
                woodSeedDetails);
            yield return VerifyLooseStackToWarehouse(itemRuntime, grid, hauler, warehouse, testPosition);
            yield return VerifyFacilityInputDelivery(itemRuntime, hauler, warehouse, bench);
            yield return VerifyConstructionMaterialDelivery(
                itemRuntime,
                workOrderRuntime,
                scope,
                grid,
                hauler,
                warehouse,
                testPosition);
            yield return VerifyCraftMaterialsOutputAndEquipmentDeposit(itemRuntime, equipment, hauler, warehouse, bench);
            yield return VerifyMaterialRepairAndSalvage(
                itemRuntime,
                equipment,
                equipmentMaintenance,
                economyCatalog,
                destinationClaims,
                scope,
                grid,
                hauler,
                warehouse,
                warehouse.centerPos);
            yield return VerifyExpeditionPacking(
                preparation,
                itemRuntime,
                destinationClaims,
                warehouse,
                hauler);
            QuiesceHaulingBeforeDirectStateFixture();
            yield return null;
            yield return VerifyL02MassAdmissionAndPickupRejection(
                scope,
                itemRuntime,
                warehouseMassAdmission,
                destinationClaims,
                grid,
                hauler);
            QuiesceHaulingBeforeDirectStateFixture();
            yield return null;
            yield return VerifyCarryUi(itemRuntime, hauler);
            VerifyWarehouseTransactionalRestoreBoundary(
                scope,
                itemRuntime,
                warehouse);
            yield return VerifyWarehouseOfficialRestoreBoundary(
                scope,
                itemRuntime,
                warehouse,
                hauler);
            }
        }
        finally
        {
            RestoreRuntimeState(itemRuntime, equipment);
        }

        yield return null;
        Finish();
    }

    private void VerifyWarehouseTransactionalRestoreBoundary(
        DungeonRuntimeLifetimeScope scope,
        IWorldItemStackRuntime runtime,
        Facility warehouse)
    {
        DungeonPhysicalItemSaveData baseline = runtime.Capture();
        string baselineJson = JsonUtility.ToJson(baseline);
        ICharacterAiWorldRegistry world =
            Resolve<ICharacterAiWorldRegistry>(scope);
        StaticRestoreWorldCandidates candidates = new(
            (world?.Warehouses ?? Array.Empty<IWarehouseFacility>())
                .OfType<BuildableObject>()
                .ToArray());
        IPhysicalItemRestoreStaging staging = runtime as IPhysicalItemRestoreStaging;
        Check(staging != null,
            "WAREHOUSE_RESTORE_TRANSACTIONAL_STAGING_AVAILABLE",
            staging != null ? "resolved" : "missing");
        if (staging == null)
        {
            return;
        }

        WorldItemStackSaveData stored = baseline.stacks
            .FirstOrDefault(stack => stack != null
                && stack.state == WorldItemStackState.Stored
                && string.Equals(
                    string.IsNullOrWhiteSpace(stack.sourceStorageDestinationId)
                        ? stack.destinationId
                        : stack.sourceStorageDestinationId,
                    WarehouseStorageIdentity.RequireDestinationId(warehouse),
                    StringComparison.Ordinal));
        Check(stored != null,
            "WAREHOUSE_RESTORE_FIXTURE_STORED_STACK_AVAILABLE",
            stored != null ? $"stack={stored.stackId}; item={stored.itemId}" : "missing");
        if (stored == null)
        {
            return;
        }

        DungeonPhysicalItemSaveData orphaned = ClonePhysicalSnapshot(baseline);
        WorldItemStackSaveData orphanedStack = orphaned.stacks.Single(
            stack => string.Equals(stack.stackId, stored.stackId, StringComparison.Ordinal));
        if (string.IsNullOrWhiteSpace(orphanedStack.sourceStorageDestinationId))
        {
            orphanedStack.destinationId = "warehouse:building:qa-orphan";
        }
        else
        {
            orphanedStack.sourceStorageDestinationId =
                "warehouse:building:qa-orphan";
        }
        bool orphanRejected = TryStageExpectedWarehouseRestoreFailure(
            staging,
            orphaned,
            candidates,
            "items.restore.warehouse_owner_missing",
            out string orphanFailure);
        Check(orphanRejected
                && string.Equals(
                    JsonUtility.ToJson(runtime.Capture()),
                    baselineJson,
                    StringComparison.Ordinal),
            "WAREHOUSE_RESTORE_INVALID_DESTINATION_ATOMIC",
            $"rejected={orphanRejected}; failure={orphanFailure}");

        DungeonPhysicalItemSaveData shifted = ClonePhysicalSnapshot(baseline);
        string storageDestination = WarehouseStorageIdentity.RequireDestinationId(warehouse);
        foreach (WorldItemStackSaveData shiftedStack in shifted.stacks.Where(
                     stack => stack != null
                         && stack.state == WorldItemStackState.Stored
                         && string.Equals(
                             string.IsNullOrWhiteSpace(stack.sourceStorageDestinationId)
                                 ? stack.destinationId
                                 : stack.sourceStorageDestinationId,
                             storageDestination,
                             StringComparison.Ordinal)))
        {
            shiftedStack.gridX += 1;
        }
        SortPhysicalStacks(shifted);
        bool positionRejected = TryStageExpectedWarehouseRestoreFailure(
            staging,
            shifted,
            candidates,
            "items.restore.warehouse_position_mismatch",
            out string positionFailure);
        Check(positionRejected
                && string.Equals(
                    JsonUtility.ToJson(runtime.Capture()),
                    baselineJson,
                    StringComparison.Ordinal),
            "WAREHOUSE_RESTORE_POSITION_MISMATCH_ATOMIC",
            $"rejected={positionRejected}; failure={positionFailure}");

        DungeonPhysicalItemSaveData overCapacity = ClonePhysicalSnapshot(baseline);
        WorldItemStackSaveData overCapacityStack = overCapacity.stacks.Single(
            stack => string.Equals(stack.stackId, stored.stackId, StringComparison.Ordinal));
        DungeonItemDefinition definition = runtime.CatalogProvider.GetDefinition(
            overCapacityStack.itemId);
        long unitMass = runtime.MassQuery
            .GetDefinitionUnitMass((ItemDefinitionId)overCapacityStack.itemId)
            .Value;
        overCapacityStack.quantity = definition.MaxStack;
        long expectedStoredMass = CalculateStoredMass(
            overCapacity,
            runtime,
            storageDestination);
        if (expectedStoredMass <= warehouse.Inventory.MaxMassGrams)
        {
            WorldItemStackSaveData extra =
                JsonUtility.FromJson<WorldItemStackSaveData>(
                    JsonUtility.ToJson(overCapacityStack));
            extra.stackId = "stack:qa-over-capacity-0001";
            long deficit = checked(
                warehouse.Inventory.MaxMassGrams - expectedStoredMass + 1L);
            extra.quantity = checked((int)Math.Min(
                definition.MaxStack,
                (deficit + unitMass - 1L) / unitMass));
            overCapacity.stacks.Add(extra);
            SortPhysicalStacks(overCapacity);
            expectedStoredMass = CalculateStoredMass(
                overCapacity,
                runtime,
                storageDestination);
        }
        bool fixtureCanExceed = expectedStoredMass
            > warehouse.Inventory.MaxMassGrams;
        Check(fixtureCanExceed,
            "WAREHOUSE_RESTORE_OVER_CAPACITY_FIXTURE_VALID",
            $"item={overCapacityStack.itemId}; unit={unitMass}; expected={expectedStoredMass}; max={warehouse.Inventory.MaxMassGrams}");
        if (!fixtureCanExceed)
        {
            return;
        }
        try
        {
            IDungeonSaveRestoreStage overCapacityStage =
                staging.StageTransactionalRestore(overCapacity, candidates);
            CommitPhysicalStageWithRegisteredParticipants(
                scope,
                runtime,
                overCapacityStage);
            long restoredMass = warehouse.Inventory.StoredMassGrams;
            Check(restoredMass > warehouse.Inventory.MaxMassGrams
                    && warehouse.Inventory.RemainingMassGrams == 0L,
                "WAREHOUSE_RESTORE_OVER_CAPACITY_PRESERVED",
                $"stored={restoredMass}; max={warehouse.Inventory.MaxMassGrams}; remaining={warehouse.Inventory.RemainingMassGrams}");

            bool admitted = runtime.SpawnStockInWarehouse(
                warehouse,
                definition.StockCategory,
                1,
                out int spawned);
            Check(!admitted
                    && spawned == 0
                    && warehouse.Inventory.StoredMassGrams == restoredMass,
                "WAREHOUSE_RESTORE_OVER_CAPACITY_ADMISSION_BLOCKED",
                $"admitted={admitted}; spawned={spawned}; stored={warehouse.Inventory.StoredMassGrams}");
        }
        finally
        {
            runtime.Restore(baseline);
        }
    }

    private void CommitPhysicalStageWithRegisteredParticipants(
        DungeonRuntimeLifetimeScope scope,
        IWorldItemStackRuntime runtime,
        IDungeonSaveRestoreStage stage)
    {
        IDungeonRestoreTransactionParticipant physicalParticipant =
            runtime as IDungeonRestoreTransactionParticipant
            ?? throw new InvalidOperationException(
                "Physical-item runtime does not expose its restore-candidate lifetime participant.");
        IDungeonRestoreTransactionParticipant exactRouteParticipant =
            Resolve<IFacilityOutputExactRouteOutboxPersistence>(scope)
                as IDungeonRestoreTransactionParticipant
            ?? throw new InvalidOperationException(
                "Exact-route outbox does not expose its restore transaction participant.");
        IDungeonRestoreTransactionParticipant[] participants =
        {
            exactRouteParticipant,
            physicalParticipant
        };
        List<IDungeonRestoreTransactionParticipant> begun = new();
        List<IDungeonRestoreTransactionParticipant> published = new();
        bool committed = false;
        try
        {
            foreach (IDungeonRestoreTransactionParticipant participant in
                     participants.OrderBy(
                         value => value.ParticipantId,
                         StringComparer.Ordinal))
            {
                participant.BeginRestoreCandidate();
                begun.Add(participant);
            }

            stage.Commit(new DungeonGameRestoreReport());
            committed = true;
            foreach (IDungeonRestoreTransactionParticipant participant in begun)
            {
                participant.PublishRestoreCandidate();
                published.Add(participant);
            }
            foreach (IDungeonRestoreTransactionParticipant participant in begun)
                participant.CompleteRestoreCandidate();
        }
        catch
        {
            foreach (IDungeonRestoreTransactionParticipant participant in
                     published.AsEnumerable().Reverse())
            {
                participant.RollbackPublishedRestoreCandidate();
            }
            foreach (IDungeonRestoreTransactionParticipant participant in
                     begun.Except(published).Reverse())
            {
                participant.DiscardRestoreCandidate();
            }
            if (!committed
                && stage is IDungeonDiscardableSaveRestoreStage discardable)
            {
                discardable.Discard();
            }
            throw;
        }
    }

    private static bool TryStageExpectedWarehouseRestoreFailure(
        IPhysicalItemRestoreStaging staging,
        DungeonPhysicalItemSaveData snapshot,
        IRestoreWorldCandidateQuery candidates,
        string expectedCode,
        out string failure)
    {
        try
        {
            staging.StageTransactionalRestore(snapshot, candidates);
            failure = "accepted";
            return false;
        }
        catch (InvalidOperationException exception)
        {
            failure = exception.Message;
            return exception.Message.Contains(
                expectedCode,
                StringComparison.Ordinal);
        }
    }

    private IEnumerator VerifyWarehouseOfficialRestoreBoundary(
        DungeonRuntimeLifetimeScope scope,
        IWorldItemStackRuntime runtime,
        Facility warehouse,
        CharacterActor originalHauler)
    {
        IDungeonSaveSectionRegistry registry =
            Resolve<IDungeonSaveSectionRegistry>(scope);
        IWarehouseOverCapacityEvacuationQuery evacuation =
            Resolve<IWarehouseOverCapacityEvacuationQuery>(scope);
        Check(registry != null && evacuation != null,
            "WAREHOUSE_RESTORE_OFFICIAL_RUNTIME_READY",
            $"registry={registry != null}; evacuation={evacuation != null}");
        if (registry == null || evacuation == null)
        {
            yield break;
        }

        Grid grid = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>()?.grid;
        BuildingSO warehouseAsset = FindWarehouseAsset();
        IReadOnlyList<Vector2Int> targetCandidates = grid == null
            ? Array.Empty<Vector2Int>()
            : FindReachableCells(grid, warehouse.centerPos, 96);
        Vector2Int targetPosition = default;
        bool targetCellFound = grid != null
            && TryFindRegisterablePosition(
                grid,
                warehouseAsset,
                targetCandidates,
                out targetPosition);
        Facility targetWarehouse = targetCellFound
            ? CreateInjectedFacility(
                scope,
                grid,
                warehouseAsset,
                targetPosition,
                "QA_Physical_Logistics_Evacuation_Warehouse",
                registerOnGrid: true)
            : null;
        Check(targetWarehouse?.Inventory?.HasMassCapacityAuthority == true,
            "WAREHOUSE_EVACUATION_TARGET_READY",
            targetWarehouse == null
                ? $"found={targetCellFound}"
                : $"id={targetWarehouse.PersistentInstanceId.Value}; max={targetWarehouse.Inventory.MaxMassGrams}");
        if (targetWarehouse?.Inventory?.HasMassCapacityAuthority != true)
        {
            yield break;
        }
        ClearInventory(targetWarehouse.Inventory);
        IWarehouseLifecycleOccupancyQuery lifecycleOccupancy =
            Resolve<IWarehouseLifecycleOccupancyQuery>(scope);
        WarehouseLifecycleOccupancySnapshot emptyOccupancy = default;
        string emptyFailure = "lifecycle query missing";
        bool emptyTargetAccepted = lifecycleOccupancy != null
            && lifecycleOccupancy.TryRequireEmpty(
                targetWarehouse,
                out emptyOccupancy,
                out emptyFailure);
        Check(emptyTargetAccepted,
            "WAREHOUSE_EMPTY_LIFECYCLE_GATE_OPEN",
            lifecycleOccupancy == null
                ? "lifecycle query missing"
                : $"stored={emptyOccupancy.StoredMassGrams}; "
                    + $"reserved={emptyOccupancy.ReservedInboundMassGrams}; "
                    + $"stacks={emptyOccupancy.ReferencedPhysicalStackCount}; "
                    + $"intents={emptyOccupancy.ActiveHaulIntentCount}; "
                    + $"failure={emptyFailure}");
        string targetWarehouseOwnerId = targetWarehouse.PersistentInstanceId.Value;

        List<DungeonSaveSectionEnvelope> baseline = registry.CaptureAll();
        DungeonSaveSectionEnvelope physicalEnvelope = baseline.SingleOrDefault(
            envelope => string.Equals(
                envelope.sectionId,
                PhysicalItemsSaveSection.Id,
                StringComparison.Ordinal));
        if (physicalEnvelope == null)
        {
            Check(false,
                "WAREHOUSE_RESTORE_OFFICIAL_ENVELOPE_AVAILABLE",
                "items.physical missing");
            yield break;
        }

        string destinationId = WarehouseStorageIdentity.RequireDestinationId(
            warehouse);
        string warehouseOwnerId = warehouse.PersistentInstanceId.Value;
        DungeonPhysicalItemSaveData overCapacity =
            JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
                physicalEnvelope.payloadJson);
        WorldItemStackSaveData stored = overCapacity.stacks
            .Where(stack => stack != null
                && stack.state == WorldItemStackState.Stored
                && string.Equals(
                    string.IsNullOrWhiteSpace(stack.sourceStorageDestinationId)
                        ? stack.destinationId
                        : stack.sourceStorageDestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            .FirstOrDefault(stack => runtime.CatalogProvider
                .GetDefinition(stack.itemId).MaxStack > 1);
        Check(stored != null,
            "WAREHOUSE_RESTORE_OFFICIAL_ENVELOPE_AVAILABLE",
            stored != null ? $"stack={stored.stackId}" : "generic stored stack missing");
        if (stored == null)
        {
            yield break;
        }

        DungeonItemDefinition definition = runtime.CatalogProvider.GetDefinition(
            stored.itemId);
        long unitMass = runtime.MassQuery
            .GetDefinitionUnitMass((ItemDefinitionId)stored.itemId)
            .Value;
        stored.quantity = definition.MaxStack;
        long expectedMass = CalculateStoredMass(
            overCapacity,
            runtime,
            destinationId);
        int extraOrdinal = 1;
        while (expectedMass <= warehouse.Inventory.MaxMassGrams)
        {
            WorldItemStackSaveData extra =
                JsonUtility.FromJson<WorldItemStackSaveData>(
                    JsonUtility.ToJson(stored));
            extra.stackId =
                $"stack:qa-official-over-capacity-{extraOrdinal++:D4}";
            long deficit = checked(
                warehouse.Inventory.MaxMassGrams - expectedMass + 1L);
            extra.quantity = checked((int)Math.Min(
                definition.MaxStack,
                Math.Max(1L, (deficit + unitMass - 1L) / unitMass)));
            overCapacity.stacks.Add(extra);
            expectedMass = checked(
                expectedMass + unitMass * extra.quantity);
        }
        SortPhysicalStacks(overCapacity);

        List<DungeonSaveSectionEnvelope> modified = baseline
            .Select(envelope => new DungeonSaveSectionEnvelope
            {
                sectionId = envelope.sectionId,
                sectionVersion = envelope.sectionVersion,
                restorePhase = envelope.restorePhase,
                optional = envelope.optional,
                payloadJson = string.Equals(
                    envelope.sectionId,
                    PhysicalItemsSaveSection.Id,
                    StringComparison.Ordinal)
                        ? JsonUtility.ToJson(overCapacity)
                        : envelope.payloadJson
            })
            .ToList();

        bool restored = false;
        string cleanupErrors = string.Empty;
        try
        {
            DungeonGameRestoreReport report = new();
            restored = registry.RestoreAll(modified, report) && report.Success;
            if (restored)
            {
                // Restore publishes replacement actor objects. Re-establish
                // verifier isolation before the next frame so autonomous AI
                // cannot reserve an unrelated destination gram token.
                QuiesceHaulingBeforeDirectStateFixture();
            }
            ICharacterAiWorldRegistry world =
                Resolve<ICharacterAiWorldRegistry>(scope);
            IWarehouseFacility restoredWarehouse = world?.Warehouses
                .FirstOrDefault(candidate => candidate != null
                    && string.Equals(
                        candidate.PersistentInstanceId.Value,
                        warehouseOwnerId,
                        StringComparison.Ordinal));
            IWarehouseFacility restoredTargetWarehouse = world?.Warehouses
                .FirstOrDefault(candidate => candidate != null
                    && string.Equals(
                        candidate.PersistentInstanceId.Value,
                        targetWarehouseOwnerId,
                        StringComparison.Ordinal));
            Check(restored,
                "WAREHOUSE_RESTORE_OFFICIAL_FULL_ROUNDTRIP",
                $"restored={restored}; errors={string.Join(" | ", report.Errors)}");
            Check(restored
                    && restoredWarehouse?.Inventory != null
                    && restoredWarehouse.Inventory.StoredMassGrams == expectedMass
                    && restoredWarehouse.Inventory.RemainingMassGrams == 0L,
                "WAREHOUSE_RESTORE_OFFICIAL_OVER_CAPACITY_PRESERVED",
                $"expected={expectedMass}; actual={restoredWarehouse?.Inventory?.StoredMassGrams ?? -1L}; remaining={restoredWarehouse?.Inventory?.RemainingMassGrams ?? -1L}");
            Check(restored
                    && evacuation.IsPending(destinationId)
                    && evacuation.CapturePendingWarehouseIds().Count(id =>
                        string.Equals(id, destinationId, StringComparison.Ordinal)) == 1,
                "WAREHOUSE_RESTORE_EVACUATION_PUBLISHED_AFTER_ROOT_SWAP",
                $"revision={evacuation.Revision}; pending={string.Join(",", evacuation.CapturePendingWarehouseIds())}");

            long targetMassBeforeEvacuation =
                restoredTargetWarehouse?.Inventory?.StoredMassGrams ?? 0L;
            Vector2Int restoredSourcePosition =
                (restoredWarehouse as BuildableObject)?.centerPos ?? default;
            Vector2Int restoredTargetPosition =
                (restoredTargetWarehouse as BuildableObject)?.centerPos ?? default;
            int sourceItemQuantityBeforeEvacuation = GetStoredItemQuantity(
                runtime,
                stored.itemId,
                restoredSourcePosition);
            int targetItemQuantityBeforeEvacuation = GetStoredItemQuantity(
                runtime,
                stored.itemId,
                restoredTargetPosition);
            CharacterActor restoredHauler = FindHauler();
            Check(restored
                    && restoredHauler != null
                    && restoredTargetWarehouse?.Inventory != null,
                "WAREHOUSE_EVACUATION_LIVE_FIXTURE_READY",
                $"hauler={restoredHauler?.BuildingCharacterId}; "
                + $"source={DescribeWarehouse(restoredWarehouse)}; "
                + $"target={DescribeWarehouse(restoredTargetWarehouse)}");
            if (restored
                && restoredHauler != null
                && restoredWarehouse?.Inventory != null
                && restoredTargetWarehouse?.Inventory != null)
            {
                yield return RunRepeatedHaul(
                    restoredHauler,
                    () => restoredWarehouse.Inventory.StoredMassGrams
                            <= restoredWarehouse.Inventory.MaxMassGrams
                        && restoredTargetWarehouse.Inventory.StoredMassGrams
                            > targetMassBeforeEvacuation
                        && !evacuation.IsPending(destinationId));
                int sourceItemQuantityAfterEvacuation = GetStoredItemQuantity(
                    runtime,
                    stored.itemId,
                    restoredSourcePosition);
                int targetItemQuantityAfterEvacuation = GetStoredItemQuantity(
                    runtime,
                    stored.itemId,
                    restoredTargetPosition);
                int sourceQuantityMoved = sourceItemQuantityBeforeEvacuation
                    - sourceItemQuantityAfterEvacuation;
                int targetQuantityReceived = targetItemQuantityAfterEvacuation
                    - targetItemQuantityBeforeEvacuation;
                Check(restoredWarehouse.Inventory.StoredMassGrams
                            <= restoredWarehouse.Inventory.MaxMassGrams
                        && restoredTargetWarehouse.Inventory.StoredMassGrams
                            > targetMassBeforeEvacuation,
                    "WAREHOUSE_EVACUATION_AI_HAUL_COMPLETED",
                    $"source={restoredWarehouse.Inventory.StoredMassGrams}/{restoredWarehouse.Inventory.MaxMassGrams}; "
                    + $"target={restoredTargetWarehouse.Inventory.StoredMassGrams}/{restoredTargetWarehouse.Inventory.MaxMassGrams}");
                Check(sourceQuantityMoved > 0
                        && targetQuantityReceived == sourceQuantityMoved
                        && restoredWarehouse.Inventory.ReservedInboundMassGrams == 0L
                        && restoredTargetWarehouse.Inventory.ReservedInboundMassGrams == 0L
                        && !evacuation.IsPending(destinationId),
                    "WAREHOUSE_EVACUATION_GRAM_TOKEN_CONSERVATION_EXACT",
                    $"item={stored.itemId}; quantity={sourceQuantityMoved}->{targetQuantityReceived}; "
                    + $"mass={unitMass * sourceQuantityMoved}->{unitMass * targetQuantityReceived}; "
                    + $"sourceReserved={restoredWarehouse.Inventory.ReservedInboundMassGrams}; "
                    + $"targetReserved={restoredTargetWarehouse.Inventory.ReservedInboundMassGrams}; "
                    + $"pending={string.Join(",", evacuation.CapturePendingWarehouseIds())}");
            }
        }
        finally
        {
            DungeonGameRestoreReport cleanup = new();
            bool cleaned = registry.RestoreAll(baseline, cleanup)
                && cleanup.Success;
            cleanupErrors = string.Join(" | ", cleanup.Errors);
            Check(cleaned
                    && !evacuation.IsPending(destinationId),
                "WAREHOUSE_RESTORE_EVACUATION_CLEANUP_EXACT",
                $"restored={cleaned}; pending={string.Join(",", evacuation.CapturePendingWarehouseIds())}; errors={cleanupErrors}");
        }
    }

    private static DungeonPhysicalItemSaveData ClonePhysicalSnapshot(
        DungeonPhysicalItemSaveData snapshot) =>
        JsonUtility.FromJson<DungeonPhysicalItemSaveData>(
            JsonUtility.ToJson(snapshot));

    private static long CalculateStoredMass(
        DungeonPhysicalItemSaveData snapshot,
        IWorldItemStackRuntime runtime,
        string storageDestination)
    {
        long total = 0L;
        foreach (WorldItemStackSaveData stack in snapshot.stacks.Where(
                     stack => stack != null
                         && stack.quantity > 0
                         && stack.state == WorldItemStackState.Stored
                         && string.Equals(
                             string.IsNullOrWhiteSpace(stack.sourceStorageDestinationId)
                                 ? stack.destinationId
                                 : stack.sourceStorageDestinationId,
                             storageDestination,
                             StringComparison.Ordinal)))
        {
            total = checked(total + runtime.MassQuery
                .GetDefinitionUnitMass((ItemDefinitionId)stack.itemId)
                .Multiply(stack.quantity)
                .Value);
        }
        return total;
    }

    private static void SortPhysicalStacks(DungeonPhysicalItemSaveData snapshot)
    {
        snapshot.stacks = snapshot.stacks
            .OrderBy(stack => stack.gridY)
            .ThenBy(stack => stack.gridX)
            .ThenBy(stack => stack.itemId, StringComparer.Ordinal)
            .ThenBy(stack => stack.stackId, StringComparer.Ordinal)
            .ToList();
    }

    private sealed class StaticRestoreWorldCandidates :
        IRestoreWorldCandidateQuery
    {
        private readonly IReadOnlyList<BuildableObject> buildings;

        internal StaticRestoreWorldCandidates(params BuildableObject[] buildings)
        {
            this.buildings = (buildings ?? Array.Empty<BuildableObject>())
                .Where(building => building != null)
                .ToArray();
        }

        public int Revision => 1;
        public bool TryGetGrid(out Grid grid)
        {
            grid = null;
            return false;
        }
        public bool TryGetBuildings(out IReadOnlyList<BuildableObject> value)
        {
            value = buildings;
            return true;
        }
        public bool TryGetCharacters(out IReadOnlyList<CharacterActor> characters)
        {
            characters = null;
            return false;
        }
        public bool TryGetWildlife(out IReadOnlyList<WildlifeActor> wildlife)
        {
            wildlife = null;
            return false;
        }
        public bool TryGetExteriorZones(out IReadOnlyList<ExteriorZoneMarker> zones)
        {
            zones = null;
            return false;
        }
    }

    private IEnumerator VerifyLooseStackToWarehouse(
        IWorldItemStackRuntime itemRuntime,
        Grid grid,
        CharacterActor hauler,
        Facility warehouse,
        Vector2Int itemPosition)
    {
        int before = GetTotalWarehouseStock(StockCategory.Food);
        int targetBefore = warehouse.Inventory.GetStock(StockCategory.Food);
        bool spawned = itemRuntime.SpawnItemAt(
            PreservedRationItemId,
            3,
            itemPosition,
            WorldItemStackState.Loose,
            string.Empty,
            out int amount);
        Check(spawned && amount == 3, "LOOSE_STACK_SPAWNED", $"pos={itemPosition}; amount={amount}");
        WorldItemStackSnapshot looseTarget = itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.Loose
                && stack.Position == itemPosition
                && string.Equals(
                    stack.ItemId,
                    PreservedRationItemId,
                    StringComparison.Ordinal))
            .OrderByDescending(stack => stack.Quantity)
            .FirstOrDefault();
        Check(looseTarget != null
                && itemRuntime.PrioritizeHaul(looseTarget.StackId),
            "LOOSE_STACK_PRIORITIZED",
            looseTarget != null ? looseTarget.StackId : "missing loose target");

        AIHaul action = ScriptableObject.CreateInstance<AIHaul>();
        try
        {
            Check(action.CanStart(hauler), "AI_HAUL_CAN_START_WAREHOUSE", DescribeHaulState(itemRuntime, hauler));
            yield return RunHaul(action, hauler, () =>
                GetTotalWarehouseStock(StockCategory.Food) >= before + 3
                && !itemRuntime.GetAllStacks().Any(stack =>
                    stack.State == WorldItemStackState.Loose
                    && string.Equals(stack.ItemId, PreservedRationItemId, StringComparison.Ordinal)));
        }
        finally
        {
            Destroy(action);
        }

        int after = GetTotalWarehouseStock(StockCategory.Food);
        int targetAfter = warehouse.Inventory.GetStock(StockCategory.Food);
        Check(after == before + 3,
            "AI_HAUL_DEPOSITED_TO_WAREHOUSE",
            $"totalFood={before}->{after}; testWarehouseFood={targetBefore}->{targetAfter}; carry={DescribeCarry(hauler, itemRuntime)}");
    }

    private IEnumerator VerifyFacilityInputDelivery(
        IWorldItemStackRuntime itemRuntime,
        CharacterActor hauler,
        Facility warehouse,
        Facility bench)
    {
        string destinationId = WorldItemStackRuntime.FacilityInputDestinationPrefix + "qa-logistics-input";
        int generalBefore = warehouse.Inventory.GetStock(StockCategory.General);
        bool requested = itemRuntime.TryRequestFacilityDelivery(
            StockCategory.General,
            2,
            bench.centerPos,
            destinationId,
            out int requestedAmount,
            out string reason);
        Check(requested && requestedAmount == 2,
            "FACILITY_DELIVERY_REQUESTED",
            $"requested={requestedAmount}; reason={reason}; general={generalBefore}->{warehouse.Inventory.GetStock(StockCategory.General)}");
        Check(warehouse.Inventory.GetStock(StockCategory.General) == generalBefore,
            "FACILITY_STOCK_HELD_UNTIL_PICKUP",
            $"general={generalBefore}->{warehouse.Inventory.GetStock(StockCategory.General)}");
        Check(!itemRuntime.GetAllStacks().Any(stack =>
                stack.State == WorldItemStackState.Loose
                && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)),
            "FACILITY_REQUEST_NO_LOOSE_PILE",
            DescribeStacks(itemRuntime));
        Check(itemRuntime.GetAllStacks().Any(stack =>
                stack.State == WorldItemStackState.Stored
                && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(stack.SourceStorageDestinationId)),
            "FACILITY_REQUEST_RESERVED_IN_STORAGE",
            DescribeStacks(itemRuntime));

        AIHaul action = ScriptableObject.CreateInstance<AIHaul>();
        try
        {
            Check(action.CanStart(hauler), "AI_HAUL_CAN_START_FACILITY", DescribeHaulState(itemRuntime, hauler));
            yield return RunHaul(action, hauler, () => itemRuntime.GetAllStacks()
                .Where(stack =>
                    stack.State == WorldItemStackState.FacilityBuffer
                    && string.Equals(
                        stack.DestinationId,
                        destinationId,
                        StringComparison.Ordinal))
                .Sum(stack => stack.Quantity) >= 2);
        }
        finally
        {
            Destroy(action);
        }

        bool bufferReady = itemRuntime.GetAllStacks().Any(stack =>
            stack.State == WorldItemStackState.FacilityBuffer
            && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)
            && stack.Position == bench.centerPos);
        Check(bufferReady, "AI_HAUL_DEPOSITED_TO_FACILITY_BUFFER", DescribeStacks(itemRuntime));
        Check(warehouse.Inventory.GetStock(StockCategory.General) == generalBefore - 2,
            "FACILITY_STOCK_WITHDRAWN_ON_PICKUP",
            $"general={generalBefore}->{warehouse.Inventory.GetStock(StockCategory.General)}");
        Check(itemRuntime.TryConsumeFacilityBuffer(
                destinationId,
                new Dictionary<StockCategory, int> { [StockCategory.General] = 2 },
                out string consumeReason),
            "FACILITY_BUFFER_CONSUMED",
            consumeReason);
    }

    private IEnumerator VerifyConstructionMaterialDelivery(
        IWorldItemStackRuntime itemRuntime,
        IWorkOrderRuntime workOrderRuntime,
        DungeonRuntimeLifetimeScope scope,
        Grid grid,
        CharacterActor hauler,
        Facility warehouse,
        Vector2Int sitePosition)
    {
        const int materialAmount = 2;
        BuildingSO building = ScriptableObject.CreateInstance<BuildingSO>();
        building.id = 99121;
        building.objectName = "QA 건설 자재 운반 시설";
        building.width = 1;
        building.height = 1;
        building.layer = GridLayer.Building;
        building.category = BuildingCategory.Shop;
        building.unlocked = true;
        BuildingWorkAmountAbility workAmount = new BuildingWorkAmountAbility
        {
            constructionWorkRequired = 5f,
            repairWorkRequired = 3f,
            cleanWorkRequired = 2f,
            researchWorkRequired = 6f
        };
        workAmount.SetConstructionProjectScale(ProjectScale.IndustrialFacility);
        workAmount.SetConstructionMaterials(new[]
        {
            new ItemAmountDefinition("material:lumber", materialAmount)
        });
        building.AbilityModules.Add(workAmount);

        GameObject siteObject = new GameObject("QA_Physical_Logistics_ConstructionSite");
        temporaryObjects.Add(siteObject);
        ConstructionSite site = siteObject.AddComponent<ConstructionSite>();
        InjectGameObject(scope, siteObject);
        site.SetGrid(grid);
        site.Initialization(building, sitePosition);
        siteObject.transform.position = grid.GetWorldPos(sitePosition);
        bool registered = grid.RegisterOccupant(
            site,
            GridLayer.Construction,
            building.GetGridPosList(sitePosition),
            false);
        Check(registered, "CONSTRUCTION_SITE_REGISTERED", $"pos={sitePosition}");

        string orderId = string.Empty;
        List<ProjectWorkerLease> projectWorkerLeases = new List<ProjectWorkerLease>();
        try
        {
            const string materialItemId = "material:lumber";
            int materialBefore = GetStoredItemQuantity(
                itemRuntime,
                materialItemId,
                warehouse.centerPos);
            string failureReason = string.Empty;
            bool created = registered
                && workOrderRuntime.TryCreateConstructionOrder(
                    site,
                    building,
                    sitePosition,
                    out orderId,
                    out failureReason);
            Check(created,
                "CONSTRUCTION_ORDER_CREATED",
                created ? $"order={orderId}" : failureReason);
            if (created)
            {
                site.ConfigureSite(orderId, () => true, () => { });
                Check(string.Equals(
                        site.WorkOrderId,
                        orderId,
                        StringComparison.Ordinal),
                    "CONSTRUCTION_SITE_ORDER_AUTHORITY_PUBLISHED",
                    $"siteOrder={site.WorkOrderId}; order={orderId}");
            }
            if (!created
                || !workOrderRuntime.TryGetOrderFor(
                    site,
                    BuiltInWorkTypeIds.Construct,
                    out WorkOrderProgressState order))
            {
                yield break;
            }

            string destinationId = order.MaterialDestinationId;
            Check(order.Status == WorkOrderStatus.WaitingForMaterials,
                "CONSTRUCTION_WAITS_FOR_MATERIALS",
                $"status={order.Status}; destination={destinationId}");
            Check(GetStoredItemQuantity(itemRuntime, materialItemId, warehouse.centerPos) == materialBefore,
                "CONSTRUCTION_STOCK_HELD_UNTIL_PICKUP",
                $"item={materialItemId}; quantity={materialBefore}->{GetStoredItemQuantity(itemRuntime, materialItemId, warehouse.centerPos)}");
            Check(!itemRuntime.GetAllStacks().Any(stack =>
                    stack.State == WorldItemStackState.Loose
                    && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)),
                "CONSTRUCTION_REQUEST_NO_LOOSE_PILE",
                DescribeStacks(itemRuntime));
            Check(itemRuntime.GetAllStacks().Any(stack =>
                    stack.State == WorldItemStackState.Stored
                    && string.Equals(stack.ItemId, materialItemId, StringComparison.Ordinal)
                    && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(stack.SourceStorageDestinationId)
                    && stack.Quantity >= materialAmount),
                "CONSTRUCTION_MATERIAL_RESERVED_IN_STORAGE",
                DescribeStacks(itemRuntime));

            AIHaul action = ScriptableObject.CreateInstance<AIHaul>();
            try
            {
                Check(action.CanStart(hauler),
                    "AI_HAUL_CAN_START_CONSTRUCTION",
                    DescribeHaulState(itemRuntime, hauler));
                yield return RunHaul(action, hauler, () =>
                    itemRuntime.GetAllStacks().Any(stack =>
                        stack.State == WorldItemStackState.FacilityBuffer
                        && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal)
                        && stack.Position == sitePosition
                        && stack.Quantity >= materialAmount)
                    || workOrderRuntime.TryGetOrderFor(
                        site,
                        BuiltInWorkTypeIds.Construct,
                        out WorkOrderProgressState deliveredOrder)
                    && deliveredOrder.DeliveredItemMaterials.TryGetValue(
                        "material:lumber",
                        out int deliveredAmount)
                    && deliveredAmount >= materialAmount);
            }
            finally
            {
                Destroy(action);
            }

            Check(GetStoredItemQuantity(itemRuntime, materialItemId, warehouse.centerPos)
                    == materialBefore - materialAmount,
                "CONSTRUCTION_STOCK_WITHDRAWN_ON_PICKUP",
                $"item={materialItemId}; quantity={materialBefore}->{GetStoredItemQuantity(itemRuntime, materialItemId, warehouse.centerPos)}");
            Check(workOrderRuntime.RefreshMaterialsReady(site)
                    && workOrderRuntime.TryGetOrderFor(
                        site,
                        BuiltInWorkTypeIds.Construct,
                        out order)
                    && order.Status == WorkOrderStatus.Ready
                    && order.DeliveredItemMaterials.TryGetValue("material:lumber", out int delivered)
                    && delivered == materialAmount,
                "CONSTRUCTION_READY_AFTER_PHYSICAL_DELIVERY",
                order != null
                    ? $"status={order.Status}; delivered={order.DeliveredItemMaterials.GetValueOrDefault("material:lumber")}"
                    : "order missing");

            VerifyLiveConstructionProjectContribution(
                workOrderRuntime,
                site,
                hauler,
                projectWorkerLeases);
        }
        finally
        {
            for (int index = projectWorkerLeases.Count - 1; index >= 0; index--)
            {
                projectWorkerLeases[index]?.Dispose();
            }

            if (!string.IsNullOrWhiteSpace(orderId))
            {
                Check(workOrderRuntime.CancelOrder(
                        orderId,
                        refundDeliveredMaterials: true),
                    "CONSTRUCTION_FIXTURE_ORDER_CANCELLED",
                    $"order={orderId}; refundDeliveredMaterials=true");
            }

            if (registered)
            {
                grid.RemoveOccupant(
                    site,
                    GridLayer.Construction,
                    building.GetGridPosList(sitePosition),
                    false);
            }

            Destroy(building);
        }
    }

    private void VerifyLiveConstructionProjectContribution(
        IWorkOrderRuntime workOrderRuntime,
        ConstructionSite site,
        CharacterActor preferredWorker,
        List<ProjectWorkerLease> leases)
    {
        IConstructionProjectWorkforceRuntime workforce =
            workOrderRuntime as IConstructionProjectWorkforceRuntime;
        Check(workforce != null,
            "CONSTRUCTION_PROJECT_WORKFORCE_READY",
            workforce != null ? "runtime resolved" : "runtime missing");
        Check(site != null && site.MaximumWorkers == 4,
            "CONSTRUCTION_INDUSTRIAL_WORKER_CAP",
            site != null ? $"maximum={site.MaximumWorkers}" : "site missing");
        if (workforce == null || site == null)
        {
            return;
        }

        CharacterActor[] candidates = verificationActors
            .Where(actor => actor != null
                && !actor.IsDead
                && CharacterPersistentIdentity.TryGet(actor, out _)
                && (actor.TryGetAbility(out AbilityWork _)
                    || actor.Identity != null && actor.Identity.Role == CharacterRole.Owner))
            .OrderBy(actor => ReferenceEquals(actor, preferredWorker) ? 0 : 1)
            .ThenBy(actor => actor.Identity?.PersistentId, StringComparer.Ordinal)
            .Take(site.MaximumWorkers)
            .ToArray();
        Check(candidates.Length >= 3,
            "CONSTRUCTION_LIVE_WORKER_SAMPLE",
            $"workers={candidates.Length}; required>=3; maximum={site.MaximumWorkers}");
        if (candidates.Length == 0)
        {
            return;
        }

        for (int index = 0; index < candidates.Length; index++)
        {
            bool joined = workforce.TryJoinConstructionProject(
                site,
                candidates[index],
                out ProjectWorkerLease lease,
                out string failureReason);
            Check(joined,
                $"CONSTRUCTION_PROJECT_WORKER_{index + 1}_JOINED",
                joined
                    ? $"worker={candidates[index].Identity?.PersistentId}"
                    : failureReason);
            if (!joined)
            {
                continue;
            }

            leases.Add(lease);
            Check(workforce.UpdateConstructionWorkerRate(site, candidates[index], 1f),
                $"CONSTRUCTION_PROJECT_WORKER_{index + 1}_RATE",
                "authored=1.00 WU/s");
        }

        int joinedCount = leases.Count;
        float expectedEffectiveWorkers = 0f;
        for (int index = 0; index < joinedCount; index++)
        {
            expectedEffectiveWorkers += SettlementLaborBalanceRules.GetWorkerContribution(
                ProjectScale.IndustrialFacility,
                index);
        }

        bool captured = workforce.TryCaptureConstructionProject(site, out ProjectWorkforceSnapshot snapshot);
        Check(captured
                && snapshot.ActiveWorkers == joinedCount
                && snapshot.MaximumWorkers == 4
                && snapshot.DefaultAutomaticWorkerLimit == 4
                && Mathf.Abs(snapshot.EffectiveWorkerCount - expectedEffectiveWorkers) <= 0.0001f
                && Mathf.Abs(snapshot.EffectiveWuPerSecond - expectedEffectiveWorkers) <= 0.0001f,
            "CONSTRUCTION_PROJECT_LIVE_SNAPSHOT",
            captured
                ? $"active={snapshot.ActiveWorkers}; maximum={snapshot.MaximumWorkers}; automatic={snapshot.DefaultAutomaticWorkerLimit}; effectiveWorkers={snapshot.EffectiveWorkerCount:0.00}; effectiveRate={snapshot.EffectiveWuPerSecond:0.00}"
                : "snapshot unavailable");

        if (!workOrderRuntime.TryGetOrderFor(
                site,
                BuiltInWorkTypeIds.Construct,
                out WorkOrderProgressState before))
        {
            Check(false, "CONSTRUCTION_PROJECT_PROGRESS_BASELINE", "order missing");
            return;
        }

        float expectedAcceptedWork = 0f;
        for (int index = 0; index < joinedCount; index++)
        {
            float multiplier = workforce.GetConstructionContributionMultiplier(
                site,
                candidates[index]);
            expectedAcceptedWork += multiplier;
            bool applied = workOrderRuntime.ApplyWork(
                candidates[index],
                site,
                BuiltInWorkTypeIds.Construct,
                multiplier,
                out bool completed,
                out _,
                out string message);
            Check(applied && !completed,
                $"CONSTRUCTION_PROJECT_WORKER_{index + 1}_PROGRESS",
                $"multiplier={multiplier:0.00}; completed={completed}; message={message}");
        }

        bool progressCaptured = workOrderRuntime.TryGetOrderFor(
            site,
            BuiltInWorkTypeIds.Construct,
            out WorkOrderProgressState after);
        float actualAcceptedWork = progressCaptured
            ? after.CompletedWork - before.CompletedWork
            : float.NaN;
        Check(progressCaptured
                && Mathf.Abs(actualAcceptedWork - expectedAcceptedWork) <= 0.001f,
            "CONSTRUCTION_PROJECT_DIMINISHING_PROGRESS_APPLIED",
            progressCaptured
                ? $"rawWorkers={joinedCount}; accepted={actualAcceptedWork:0.00}; expected={expectedAcceptedWork:0.00}"
                : "order missing after progress");
    }

    private IEnumerator VerifyCraftMaterialsOutputAndEquipmentDeposit(
        IWorldItemStackRuntime itemRuntime,
        ICombatEquipmentRuntime equipment,
        CharacterActor hauler,
        Facility warehouse,
        Facility bench)
    {
        string outputItemId = PhysicalItemIds.ForEquipment(DaggerId);
        int inventoryBefore = equipment.GetAvailableCount(DaggerId);
        Check(equipment.TryQueueCraft(DaggerId, bench, out string queueMessage),
            "CRAFT_QUEUE_REQUESTED_PHYSICAL_MATERIALS",
            queueMessage);

        CombatEquipmentCraftOrderSaveData order = equipment.CraftQueue
            .FirstOrDefault(item => item != null
                && string.Equals(item.definitionId, DaggerId, StringComparison.Ordinal)
                && !item.materialsReady);
        Check(order != null
                && string.Equals(order.materialId, "material:iron", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(order.materialDestinationId)
                && itemRuntime.GetAllStacks().Any(stack =>
                    string.Equals(stack.ItemId, "material:iron-ingot", StringComparison.Ordinal)
                    &&
                    stack.HasDestinationPosition
                    && string.Equals(stack.DestinationId, order.materialDestinationId, StringComparison.Ordinal)),
            "CRAFT_MATERIAL_STACK_CREATED",
            order != null ? $"destination={order.materialDestinationId}" : "missing order");

        AIHaul action = ScriptableObject.CreateInstance<AIHaul>();
        try
        {
            Check(action.CanStart(hauler), "AI_HAUL_CAN_START_CRAFT_MATERIALS", DescribeHaulState(itemRuntime, hauler));
            yield return RunHaul(action, hauler, () => order != null && itemRuntime.GetAllStacks().Any(stack =>
                stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(stack.DestinationId, order.materialDestinationId, StringComparison.Ordinal)));
        }
        finally
        {
            Destroy(action);
        }

        Check(equipment.HasPendingCraftWork(new[] { DaggerId }),
            "CRAFT_MATERIALS_READY_AFTER_HAUL",
            order != null ? $"ready={order.materialsReady}" : "missing order");

        int guard = 0;
        while (equipment.CraftQueue.Any(item => item != null
                   && string.Equals(item.definitionId, DaggerId, StringComparison.Ordinal))
               && guard++ < 40)
        {
            ModularFacilityRuntimeEffects.ApplyWorkCompleted(
                hauler.BuildingVisitor,
                bench,
                BuiltInWorkTypeIds.Craft);
            yield return null;
        }

        bool queueTerminal = !equipment.CraftQueue.Any(item => item != null
            && string.Equals(item.definitionId, DaggerId, StringComparison.Ordinal));
        ProductionDomainOutputPublicationSaveData publication = order?.outputPublication;
        ProductionDomainPublishedStackSaveData frozenOutput = publication?.stacks != null
            && publication.stacks.Count == 1
                ? publication.stacks[0]
                : null;
        WorldItemStackSnapshot[] exactOutputs = string.IsNullOrWhiteSpace(order?.outputStackId)
            ? Array.Empty<WorldItemStackSnapshot>()
            : itemRuntime.GetAllStacks().Where(stack => stack != null
                && string.Equals(stack.StackId, order.outputStackId, StringComparison.Ordinal))
                .ToArray();
        WorldItemStackSnapshot output = exactOutputs.Length == 1
            ? exactOutputs[0]
            : null;
        PreparedOutputPublicationIdentity outputProvenance = default;
        bool hasExactProvenance = output != null
            && TryReadPreparedOutputPublicationIdentity(
                output.Components,
                out outputProvenance);
        bool outputAuthorityExact = queueTerminal
            && guard <= 40
            && publication is { outputAcknowledged: true }
            && frozenOutput != null
            && exactOutputs.Length == 1
            && string.Equals(order.outputStackId, frozenOutput.stackId, StringComparison.Ordinal)
            && string.Equals(order.outputInstanceId, frozenOutput.itemInstanceId, StringComparison.Ordinal)
            && output.State == WorldItemStackState.Loose
            && !output.HasDestinationPosition
            && string.IsNullOrEmpty(output.DestinationId)
            && output.Position == new Vector2Int(order.destinationX, order.destinationY)
            && string.Equals(output.ItemId, outputItemId, StringComparison.Ordinal)
            && string.Equals(output.ItemId, frozenOutput.itemId, StringComparison.Ordinal)
            && string.Equals(output.ItemInstanceId, frozenOutput.itemInstanceId, StringComparison.Ordinal)
            && output.Quantity == frozenOutput.quantity
            && hasExactProvenance
            && outputProvenance.Acknowledged
            && string.Equals(outputProvenance.BatchCommitId, publication.batchCommitId, StringComparison.Ordinal)
            && string.Equals(outputProvenance.OutcomeFingerprint, publication.outcomeFingerprint, StringComparison.Ordinal)
            && string.Equals(outputProvenance.PlannedOutputFingerprint, publication.plannedOutputFingerprint, StringComparison.Ordinal)
            && string.Equals(outputProvenance.OutputLineId, frozenOutput.outputLineId, StringComparison.Ordinal)
            && string.Equals(outputProvenance.ItemId, frozenOutput.itemId, StringComparison.Ordinal)
            && outputProvenance.StackOrdinal == 0
            && outputProvenance.BatchStackCount == 1
            && outputProvenance.BatchQuantity == frozenOutput.quantity
            && outputProvenance.BatchMassGrams == frozenOutput.massGrams
            && outputProvenance.LineStackCount == 1
            && outputProvenance.LineQuantity == frozenOutput.quantity
            && outputProvenance.LineMassGrams == frozenOutput.massGrams
            && outputProvenance.Quantity == frozenOutput.quantity
            && outputProvenance.MassGrams == frozenOutput.massGrams;
        Check(outputAuthorityExact,
            "CRAFT_OUTPUT_WORLD_STACK_CREATED",
            output != null
                ? $"queueTerminal={queueTerminal}; stack={output.StackId}; state={output.State}; "
                    + $"pos={output.Position}; acknowledged={publication?.outputAcknowledged}; "
                    + $"provenance={hasExactProvenance}"
                : $"queueTerminal={queueTerminal}; exactStacks={exactOutputs.Length}; "
                    + $"publicationStacks={publication?.stacks?.Count ?? 0}");
        Check(output != null
                && equipment.TryGetInstanceBySourceStack(
                    output.StackId,
                    out CombatEquipmentInstance crafted)
                && string.Equals(
                    crafted.materialId,
                    "material:iron",
                    StringComparison.Ordinal),
            "CRAFT_OUTPUT_RETAINED_SELECTED_MATERIAL",
            output != null ? $"stack={output.StackId}" : "missing output stack");

        action = ScriptableObject.CreateInstance<AIHaul>();
        try
        {
            Check(action.CanStart(hauler), "AI_HAUL_CAN_START_CRAFT_OUTPUT", DescribeHaulState(itemRuntime, hauler));
            yield return RunHaul(action, hauler, () => equipment.GetAvailableCount(DaggerId) >= inventoryBefore + 1);
        }
        finally
        {
            Destroy(action);
        }

        int inventoryAfter = equipment.GetAvailableCount(DaggerId);
        Check(inventoryAfter == inventoryBefore + 1,
            "AI_HAUL_DEPOSITED_EQUIPMENT_TO_INVENTORY",
            $"Dagger={inventoryBefore}->{inventoryAfter}; warehouseWeapon={warehouse.Inventory.GetStock(StockCategory.Weapon)}");

        try
        {
            WorldItemStackSnapshot deposited = output == null
                ? null
                : itemRuntime.GetAllStacks().FirstOrDefault(stack =>
                    stack != null
                    && string.Equals(
                        stack.StackId,
                        output.StackId,
                        StringComparison.Ordinal));
            IPhysicalItemMassQuery massQuery =
                Resolve<IPhysicalItemMassQuery>(FindScope());
            PhysicalItemMassSubject subject = deposited == null
                ? default
                : PhysicalItemMassSubjectAdapter.Create(
                    massQuery,
                    (ItemDefinitionId)deposited.ItemId,
                    deposited.ItemInstanceId,
                    deposited.Components);
            long projectedMass = deposited == null || massQuery == null
                ? 0L
                : massQuery.GetStackUnitMass(
                    (ItemDefinitionId)deposited.ItemId,
                    subject).Value;
            long baseMass = massQuery?.GetDefinitionUnitMass(
                (ItemDefinitionId)outputItemId).Value ?? 0L;
            bool depositedProvenanceExact = deposited != null
                && TryReadPreparedOutputPublicationIdentity(
                    deposited.Components,
                    out PreparedOutputPublicationIdentity depositedProvenance)
                && depositedProvenance.Acknowledged
                && hasExactProvenance
                && string.Equals(
                    depositedProvenance.BatchCommitId,
                    outputProvenance.BatchCommitId,
                    StringComparison.Ordinal)
                && string.Equals(
                    depositedProvenance.OutcomeFingerprint,
                    outputProvenance.OutcomeFingerprint,
                    StringComparison.Ordinal)
                && string.Equals(
                    depositedProvenance.PlannedOutputFingerprint,
                    outputProvenance.PlannedOutputFingerprint,
                    StringComparison.Ordinal)
                && string.Equals(
                    depositedProvenance.OutputLineId,
                    outputProvenance.OutputLineId,
                    StringComparison.Ordinal)
                && string.Equals(
                    depositedProvenance.ItemId,
                    outputProvenance.ItemId,
                    StringComparison.Ordinal)
                && depositedProvenance.StackOrdinal == outputProvenance.StackOrdinal
                && depositedProvenance.BatchStackCount == outputProvenance.BatchStackCount
                && depositedProvenance.BatchQuantity == outputProvenance.BatchQuantity
                && depositedProvenance.BatchMassGrams == outputProvenance.BatchMassGrams
                && depositedProvenance.LineStackCount == outputProvenance.LineStackCount
                && depositedProvenance.LineQuantity == outputProvenance.LineQuantity
                && depositedProvenance.LineMassGrams == outputProvenance.LineMassGrams
                && depositedProvenance.Quantity == outputProvenance.Quantity
                && depositedProvenance.MassGrams == outputProvenance.MassGrams;
            Check(deposited != null
                    && deposited.State == WorldItemStackState.Stored
                    && projectedMass == baseMass
                    && Mathf.Approximately(
                        deposited.UnitWeight,
                        projectedMass / 1000f)
                    && depositedProvenanceExact
                    && warehouse.Inventory.ReservedInboundMassGrams == 0L,
                "COMBAT_EQUIPMENT_STATEFUL_WAREHOUSE_MASS_EXACT",
                $"stack={deposited?.StackId}; state={deposited?.State}; "
                    + $"projected={projectedMass}; base={baseMass}; "
                    + $"unitKg={deposited?.UnitWeight}; provenance={depositedProvenanceExact}; "
                    + $"reserved={warehouse.Inventory.ReservedInboundMassGrams}");
        }
        catch (Exception exception)
        {
            Check(false,
                "COMBAT_EQUIPMENT_STATEFUL_WAREHOUSE_MASS_EXACT",
                exception.Message);
        }

    }

    private IEnumerator VerifyL02MassAdmissionAndPickupRejection(
        DungeonRuntimeLifetimeScope scope,
        IWorldItemStackRuntime runtime,
        IWarehouseMassAdmissionService warehouseMassAdmission,
        IFacilityBufferDestinationClaimQuery destinationClaims,
        Grid grid,
        CharacterActor hauler)
    {
        BuildingSO asset = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Modular/L02_상자더미.asset");
        IReadOnlyList<Vector2Int> candidates = FindReachableCells(
            grid,
            hauler.GetNowXY(),
            96);
        bool cellFound = TryFindRegisterablePosition(
            grid,
            asset,
            candidates,
            out Vector2Int position);
        Facility l02 = cellFound
            ? CreateInjectedFacility(
                scope,
                grid,
                asset,
                position,
                "QA_L02_Mass_Warehouse",
                registerOnGrid: true)
            : null;
        Check(l02?.Inventory?.HasMassCapacityAuthority == true
                && l02.Inventory.MaxMassGrams == 12_500L,
            "L02_PLAYMODE_MASS_AUTHORITY_READY",
            l02 == null
                ? $"cellFound={cellFound}; position={position}"
                : $"id={l02.PersistentInstanceId.Value}; max={l02.Inventory.MaxMassGrams}");
        if (l02?.Inventory?.HasMassCapacityAuthority != true)
        {
            yield break;
        }

        WorldItemWarehouseService warehouseService =
            Resolve<WorldItemWarehouseService>(scope);
        Check(warehouseService != null,
            "L02_PLAYMODE_EXACT_INGRESS_SERVICE_READY",
            warehouseService != null ? "resolved" : "missing");
        if (warehouseService == null)
        {
            yield break;
        }

        string ingressOperationId =
            "qa:l02-playmode:inoculated-log:ingress";
        bool stored = warehouseService.SpawnItemStock(
            l02,
            InoculatedLogItemId,
            17,
            ingressOperationId,
            "generic:supply:inoculated-log",
            out int spawned,
            out WarehouseMassAdmissionReceipt receipt,
            out DomainFailure ingressFailure);
        Check(stored
                && spawned == 17
                && receipt.CommittedQuantity == 17
                && receipt.CommittedMassGrams == 11_900L
                && l02.Inventory.StoredMassGrams == 11_900L
                && l02.Inventory.RemainingMassGrams == 600L
                && l02.Inventory.GetAcceptableQuantity(
                    InoculatedLogItemId,
                    1) == 0,
            "L02_PLAYMODE_17X700G_INGRESS_EXACT",
            $"stored={stored}; spawned={spawned}; committed={receipt.CommittedQuantity}x{receipt.CommittedMassGrams}g; "
            + $"mass={l02.Inventory.StoredMassGrams}/{l02.Inventory.MaxMassGrams}; remaining={l02.Inventory.RemainingMassGrams}; failure={ingressFailure.Code}");
        if (!stored)
        {
            yield break;
        }

        string destinationId = WarehouseStorageIdentity.RequireDestinationId(l02);
        DungeonPhysicalItemSaveData checkpoint = runtime.Capture();
        string[] storedStackIds = checkpoint.stacks
            .Where(stack => stack != null
                && stack.state == WorldItemStackState.Stored
                && string.Equals(
                    string.IsNullOrWhiteSpace(stack.sourceStorageDestinationId)
                        ? stack.destinationId
                        : stack.sourceStorageDestinationId,
                    destinationId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.itemId,
                    InoculatedLogItemId,
                    StringComparison.Ordinal))
            .Select(stack => stack.stackId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        runtime.Restore(checkpoint);
        yield return null;
        WorldItemStackSnapshot[] restoredStacks = runtime.GetAllStacks()
            .Where(stack => stack != null
                && storedStackIds.Contains(stack.StackId, StringComparer.Ordinal))
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .ToArray();
        Check(restoredStacks.Length == storedStackIds.Length
                && restoredStacks.Sum(stack => stack.Quantity) == 17
                && restoredStacks.All(stack => stack.State == WorldItemStackState.Stored
                    && string.Equals(
                        stack.DestinationId,
                        destinationId,
                        StringComparison.Ordinal))
                && l02.Inventory.StoredMassGrams == 11_900L
                && l02.Inventory.RemainingMassGrams == 600L,
            "L02_PLAYMODE_CURRENT_FORMAT_RESTORE_EXACT",
            $"ids={storedStackIds.Length}->{restoredStacks.Length}; quantity={restoredStacks.Sum(stack => stack.Quantity)}; "
            + $"mass={l02.Inventory.StoredMassGrams}; remaining={l02.Inventory.RemainingMassGrams}");

        Vector2Int loosePosition = candidates.FirstOrDefault(candidate =>
            grid.GetGridCell(candidate)?.CanOccupy(GridLayer.FloorOverlay) == true
            && candidate != position);
        bool looseSpawned = runtime.SpawnItemAt(
            InoculatedLogItemId,
            1,
            loosePosition,
            WorldItemStackState.Loose,
            string.Empty,
            out int looseAmount);
        WorldItemStackSnapshot loose = runtime.GetAllStacks()
            .FirstOrDefault(stack => stack != null
                && stack.State == WorldItemStackState.Loose
                && stack.Position == loosePosition
                && string.Equals(
                    stack.ItemId,
                    InoculatedLogItemId,
                    StringComparison.Ordinal));
        Check(looseSpawned && looseAmount == 1 && loose != null,
            "L02_PLAYMODE_OVERFILL_LOOSE_STACK_READY",
            $"spawned={looseSpawned}; amount={looseAmount}; position={loosePosition}; stack={loose?.StackId ?? "missing"}");
        if (loose == null)
        {
            yield break;
        }
        runtime.PrioritizeHaul(loose.StackId);

        ICharacterAiWorldRegistry liveWorld =
            Resolve<ICharacterAiWorldRegistry>(scope);
        IGridSystemProvider gridProvider = Resolve<IGridSystemProvider>(scope);
        IDungeonItemCatalogProvider catalog =
            Resolve<IDungeonItemCatalogProvider>(scope);
        IPhysicalItemMassQuery massQuery =
            Resolve<IPhysicalItemMassQuery>(scope);
        IItemHaulingSettingsProvider haulingSettings =
            Resolve<IItemHaulingSettingsProvider>(scope);
        ICharacterIdRegistry characterIds =
            Resolve<ICharacterIdRegistry>(scope);
        WorldItemRepository repository = Resolve<WorldItemRepository>(scope);
        IItemQuantityReservationService reservations =
            Resolve<IItemQuantityReservationService>(scope);
        IGridPathSearchBroker pathBroker = hauler.PathSearchBroker;
        bool dependenciesReady = liveWorld != null
            && gridProvider != null
            && catalog != null
            && massQuery != null
            && haulingSettings != null
            && characterIds != null
            && repository != null
            && reservations != null
            && destinationClaims != null
            && warehouseMassAdmission != null
            && pathBroker != null;
        Check(dependenciesReady,
            "L02_PLAYMODE_ISOLATED_PRODUCTION_PLANNER_READY",
            $"world={liveWorld != null}; grid={gridProvider != null}; catalog={catalog != null}; mass={massQuery != null}; "
            + $"settings={haulingSettings != null}; ids={characterIds != null}; repository={repository != null}; "
            + $"reservations={reservations != null}; claims={destinationClaims != null}; admission={warehouseMassAdmission != null}; path={pathBroker != null}");
        if (!dependenciesReady)
        {
            yield break;
        }

        WorldItemHaulPlanningService isolatedPlanner = new(
            gridProvider,
            catalog,
            massQuery,
            haulingSettings,
            characterIds,
            pathBroker,
            new SingleWarehouseWorldRegistry(liveWorld, l02),
            repository,
            reservations,
            destinationClaims,
            warehouseMassAdmission);
        CharacterCarryInventory carry = CharacterCarryInventory.Ensure(hauler);
        int carryBefore = carry?.Items.Count ?? 0;
        bool previewed = isolatedPlanner.TryPreviewBestPlan(
            hauler,
            out WorldItemHaulPlan previewPlan,
            out string previewFailure);
        bool overfillPreviewed = previewed
            && previewPlan?.ReservedStackQuantities?.Any(reservation =>
                string.Equals(
                    reservation.StackId,
                    loose.StackId,
                    StringComparison.Ordinal)) == true;
        bool reserved = false;
        string reserveFailure =
            "not-attempted:overfill-stack-was-not-previewed";
        if (overfillPreviewed)
        {
            reserved = isolatedPlanner.TryReserveBestPlan(
                hauler,
                out _,
                out reserveFailure);
        }
        WorldItemStackSnapshot after = runtime.GetAllStacks()
            .FirstOrDefault(stack => string.Equals(
                stack.StackId,
                loose.StackId,
                StringComparison.Ordinal));
        Check(!overfillPreviewed
                && !reserved
                && after != null
                && after.State == WorldItemStackState.Loose
                && after.Quantity == 1
                && after.ReservedQuantity == 0
                && (carry?.Items.Count ?? 0) == carryBefore
                && l02.Inventory.ReservedInboundMassGrams == 0L
                && l02.Inventory.StoredMassGrams == 11_900L,
            "L02_PLAYMODE_OVERFILL_REJECTED_BEFORE_PICKUP",
            $"preview={previewed}; overfillPreview={overfillPreviewed}:{previewFailure}; "
            + $"reserve={reserved}:{reserveFailure}; "
            + $"stack={after?.State.ToString() ?? "missing"}x{after?.Quantity ?? 0}; reservedQuantity={after?.ReservedQuantity ?? -1}; "
            + $"carry={carryBefore}->{carry?.Items.Count ?? 0}; inbound={l02.Inventory.ReservedInboundMassGrams}; stored={l02.Inventory.StoredMassGrams}");
    }

    private IEnumerator VerifyProductionInputBufferMassAdmission(
        DungeonRuntimeLifetimeScope scope,
        IWorldItemStackRuntime runtime,
        Grid grid,
        CharacterActor hauler)
    {
        const string treatedLumber = "material:treated-lumber";
        const string caveMushroom = "resource:cave-mushroom";
        const string destination =
            "production:production-bill:qa-input-buffer-mass";
        const long capacityGrams = 4_200L;

        IProductionItemGateway gateway = Resolve<IProductionItemGateway>(scope);
        IFacilityBufferDestinationLifecycleCommand lifecycle =
            Resolve<IFacilityBufferDestinationLifecycleCommand>(scope);
        IFacilityBufferMassCapacityQuery capacities =
            Resolve<IFacilityBufferMassCapacityQuery>(scope);
        IFacilityBufferDestinationClaimAuthorityQuery claimAuthority =
            Resolve<IFacilityBufferDestinationClaimAuthorityQuery>(scope);
        IFacilityBufferMassCapacityAuthorityQuery capacityAuthority =
            Resolve<IFacilityBufferMassCapacityAuthorityQuery>(scope);
        IReadOnlyList<Vector2Int> positions = FindReachableCells(
            grid,
            hauler.GetNowXY(),
            48);
        Vector2Int sourcePosition = positions.FirstOrDefault();
        Vector2Int destinationPosition = positions
            .FirstOrDefault(value => value != sourcePosition);
        BuildingSO productionAsset = FindBuildingAsset(asset =>
            asset?.Facility != null
            && asset.GetAbility<BuildingProductionWorkstationAbility>() != null);
        Facility productionFacility = CreateInjectedFacility(
            scope,
            grid,
            productionAsset,
            destinationPosition,
            "QA_Production_Input_Buffer_Facility");
        string facilityId = productionFacility?.PersistentInstanceId.IsValid == true
            ? productionFacility.PersistentInstanceId.Value
            : string.Empty;
        Check(gateway != null
                && lifecycle != null
                && capacities != null
                && claimAuthority != null
                && capacityAuthority != null
                && productionFacility != null
                && facilityId.Length > 0
                && positions.Count >= 2,
            "PRODUCTION_INPUT_BUFFER_MASS_RUNTIME_READY",
            $"gateway={gateway != null}; lifecycle={lifecycle != null}; "
            + $"capacities={capacities != null}; "
            + $"authority={claimAuthority != null}/{capacityAuthority != null}; "
            + $"facility={facilityId}; "
            + $"positions={positions.Count}");
        if (gateway == null
            || lifecycle == null
            || capacities == null
            || claimAuthority == null
            || capacityAuthority == null
            || productionFacility == null
            || facilityId.Length == 0
            || positions.Count < 2)
        {
            yield break;
        }

        destinationPosition = productionFacility.centerPos;

        FacilityBufferDestinationClaim destinationClaim = new(
            destination,
            destinationPosition,
            ProductionInputDestinationClaimRuntime.OwnerDomain,
            "production-bill:qa-input-buffer-mass",
            facilityId,
            FacilityBufferDestinationAnchorKind.LiveFacility);
        FacilityBufferCapacityProfile capacityProfile = new(
            destination,
            destinationPosition,
            ProductionInputDestinationClaimRuntime.OwnerDomain,
            "production-bill:qa-input-buffer-mass",
            facilityId,
            new PhysicalMassGrams(capacityGrams),
            ProductionInputDestinationClaimRuntime
                .InputBufferCapacitySchemaRevision);
        FacilityBufferDestinationClaim[] previousClaims = claimAuthority
            .CaptureAuthorityClaims()
            .Where(value => string.Equals(
                value.OwnerDomain,
                ProductionInputDestinationClaimRuntime.OwnerDomain,
                StringComparison.Ordinal))
            .ToArray();
        FacilityBufferCapacityProfile[] previousProfiles = capacityAuthority
            .CaptureAuthorityProfiles()
            .Where(value => string.Equals(
                value.OwnerDomain,
                ProductionInputDestinationClaimRuntime.OwnerDomain,
                StringComparison.Ordinal))
            .ToArray();
        bool claimed = lifecycle.TryReplaceOwnedAuthorities(
            ProductionInputDestinationClaimRuntime.OwnerDomain,
            previousClaims.Append(destinationClaim).ToArray(),
            previousProfiles.Append(capacityProfile).ToArray(),
            out string claimFailure);
        Check(claimed,
            "PRODUCTION_INPUT_BUFFER_EXACT_AUTHORITY_PUBLISHED",
            $"claimed={claimed}; failure={claimFailure}");
        if (!claimed)
            yield break;

        bool lumberSpawned = runtime.SpawnItemAt(
            treatedLumber,
            4,
            sourcePosition,
            WorldItemStackState.Loose,
            string.Empty,
            out int lumberAmount);
        bool mushroomSpawned = runtime.SpawnItemAt(
            caveMushroom,
            4,
            sourcePosition,
            WorldItemStackState.Loose,
            string.Empty,
            out int mushroomAmount);
        Check(lumberSpawned && lumberAmount == 4
                && mushroomSpawned && mushroomAmount == 4,
            "PRODUCTION_INPUT_BUFFER_MASS_SOURCES_READY",
            $"lumber={lumberSpawned}:{lumberAmount}; mushroom={mushroomSpawned}:{mushroomAmount}");
        if (!lumberSpawned || !mushroomSpawned)
        {
            yield break;
        }

        bool lumberRequested = gateway.RequestDelivery(
            treatedLumber,
            3,
            destinationPosition,
            destination,
            out int requestedLumber,
            out string lumberFailure);
        bool mushroomRequested = gateway.RequestDelivery(
            caveMushroom,
            3,
            destinationPosition,
            destination,
            out int requestedMushroom,
            out string mushroomFailure);
        long fullMass = gateway.CountPendingMassGrams(destination);
        Check(lumberRequested && requestedLumber == 3
                && mushroomRequested && requestedMushroom == 3
                && fullMass == capacityGrams
                && capacities.TryGetCapacity(
                    destination,
                    destinationPosition,
                    out FacilityBufferMassCapacitySnapshot fullCapacity)
                && fullCapacity.ReservedMassGrams == 0L,
            "PRODUCTION_INPUT_BUFFER_EXACT_TOKEN_4200G_ADMITTED",
            $"lumber={lumberRequested}:{requestedLumber}:{lumberFailure}; "
            + $"mushroom={mushroomRequested}:{requestedMushroom}:{mushroomFailure}; mass={fullMass}");

        WorldItemStackSnapshot unboundBefore = runtime.GetAllStacks()
            .Single(stack => stack != null
                && stack.State == WorldItemStackState.Loose
                && string.IsNullOrWhiteSpace(stack.DestinationId)
                && string.Equals(stack.ItemId, caveMushroom, StringComparison.Ordinal));
        bool overflowAccepted = gateway.RequestDelivery(
            caveMushroom,
            1,
            destinationPosition,
            destination,
            out int overflowRequested,
            out string overflowFailure);
        WorldItemStackSnapshot unboundAfter = runtime.GetAllStacks()
            .Single(stack => string.Equals(
                stack.StackId,
                unboundBefore.StackId,
                StringComparison.Ordinal));
        Check(!overflowAccepted
                && overflowRequested == 0
                && string.Equals(
                    overflowFailure,
                    FailureCode.ItemTransferRequestFailed.ToString(),
                    StringComparison.Ordinal)
                && unboundAfter.State == WorldItemStackState.Loose
                && string.IsNullOrWhiteSpace(unboundAfter.DestinationId)
                && unboundAfter.Quantity == 1
                && unboundAfter.ReservedQuantity == 0
                && gateway.CountPendingMassGrams(destination) == capacityGrams,
            "PRODUCTION_INPUT_BUFFER_MASS_OVERFLOW_REJECTED_BEFORE_PICKUP",
            $"accepted={overflowAccepted}; requested={overflowRequested}; failure={overflowFailure}; "
            + $"source={unboundAfter.State}:{unboundAfter.Quantity}:{unboundAfter.DestinationId}; "
            + $"reserved={unboundAfter.ReservedQuantity}; mass={gateway.CountPendingMassGrams(destination)}");

        DungeonPhysicalItemSaveData checkpoint = runtime.Capture();
        runtime.Restore(checkpoint);
        yield return null;
        Check(gateway.CountPendingMassGrams(destination) == capacityGrams,
            "PRODUCTION_INPUT_BUFFER_MASS_CURRENT_FORMAT_RESTORE_EXACT",
            $"mass={gateway.CountPendingMassGrams(destination)}");

        foreach (WorldItemStackSnapshot unbound in runtime.GetAllStacks()
                     .Where(stack => stack != null
                         && stack.State == WorldItemStackState.Loose
                         && string.IsNullOrWhiteSpace(stack.DestinationId)))
        {
            runtime.SetForbidden(unbound.StackId, true);
        }

        AIHaul action = ScriptableObject.CreateInstance<AIHaul>();
        HaulDeliveryIntentSaveData committedIntent = null;
        try
        {
            bool canStart = action.CanStart(hauler);
            Check(canStart,
                "PRODUCTION_INPUT_BUFFER_ACTUAL_AIHAUL_CAN_START",
                DescribeHaulState(runtime, hauler));
            if (canStart)
            {
                action.Execute(hauler);
                float startedAt = Time.realtimeSinceStartup;
                while (Time.realtimeSinceStartup - startedAt < HaulTimeoutSeconds)
                {
                    EnsureVerificationTimeScale();
                    committedIntent = runtime
                        .CaptureHaulDeliveryIntentsByDestination(destination)
                        .FirstOrDefault(intent => intent.HasCommittedPickup);
                    if (committedIntent != null)
                        break;
                    yield return null;
                }
            }

            long carriedMass = gateway.CountPendingMassGrams(destination);
            Vector2Int cancellationPosition = hauler.GetNowXY();
            string[] committedStackIds = committedIntent?.commitments
                .Select(commitment => commitment.carriedStackId)
                .ToArray() ?? Array.Empty<string>();
            Check(committedIntent != null && carriedMass == capacityGrams,
                "PRODUCTION_INPUT_BUFFER_PICKUP_MASS_IDENTITY",
                $"intent={committedIntent?.operationId ?? "missing"}; mass={carriedMass}; "
                + DescribeHaulState(runtime, hauler));

            bool releasedAtomically = gateway.TryReleaseDestinationAtomically(
                destination,
                destinationPosition,
                out int released,
                out string releaseFailure);
            WorldItemStackSnapshot[] recoveryDrops = runtime.GetAllStacks()
                .Where(stack => committedStackIds.Contains(
                    stack.StackId,
                    StringComparer.Ordinal))
                .ToArray();
            int totalQuantity = runtime.GetAllStacks()
                .Where(stack => string.Equals(stack.ItemId, treatedLumber, StringComparison.Ordinal)
                    || string.Equals(stack.ItemId, caveMushroom, StringComparison.Ordinal))
                .Sum(stack => stack.Quantity);
            Check(committedIntent != null
                    && releasedAtomically
                    && gateway.CountPendingMassGrams(destination) == 0L
                    && runtime.CaptureHaulDeliveryIntentsByDestination(destination).Count == 0
                    && recoveryDrops.Length == committedStackIds.Length
                    && recoveryDrops.All(stack => stack.State == WorldItemStackState.Loose
                        && stack.Position == cancellationPosition)
                    && hauler.CarryInventory.Items.All(item => !string.Equals(
                        item.ownerOperationId,
                        committedIntent?.operationId,
                        StringComparison.Ordinal))
                    && totalQuantity == 8,
                "PRODUCTION_INPUT_BUFFER_PICKUP_CANCEL_PHYSICAL_RECOVERY",
                $"released={releasedAtomically}:{released}:{releaseFailure}; "
                + $"mass={gateway.CountPendingMassGrams(destination)}; "
                + $"drops={recoveryDrops.Length}/{committedStackIds.Length}@{cancellationPosition}; "
                + $"quantity={totalQuantity}");

            DungeonPhysicalItemSaveData afterCancel = runtime.Capture();
            runtime.Restore(afterCancel);
            yield return null;
            Check(gateway.CountPendingMassGrams(destination) == 0L
                    && runtime.CaptureHaulDeliveryIntentsByDestination(destination).Count == 0
                    && runtime.GetAllStacks().Where(stack => stack != null)
                        .All(stack => !string.Equals(
                            stack.DestinationId,
                            destination,
                            StringComparison.Ordinal)),
                "PRODUCTION_INPUT_BUFFER_CANCEL_SAVE_RESTORE_NO_ORPHAN",
                $"mass={gateway.CountPendingMassGrams(destination)}; "
                + $"intents={runtime.CaptureHaulDeliveryIntentsByDestination(destination).Count}");

            bool wipLumberSpawned = runtime.SpawnItemAt(
                treatedLumber,
                1,
                destinationPosition,
                WorldItemStackState.FacilityBuffer,
                destination,
                out int wipLumberAmount);
            bool wipMushroomSpawned = runtime.SpawnItemAt(
                caveMushroom,
                1,
                destinationPosition,
                WorldItemStackState.FacilityBuffer,
                destination,
                out int wipMushroomAmount);
            const string wipOperation =
                "production-wip-input:qa-input-buffer-mass:cycle-1";
            bool wipCommitted = gateway.ConsumeDeliveredToWip(
                destination,
                new Dictionary<string, int>(StringComparer.Ordinal)
                {
                    [treatedLumber] = 1,
                    [caveMushroom] = 1
                },
                wipOperation,
                out ProductionWipInputReceipt wipReceipt,
                out string wipFailure);
            DungeonPhysicalItemSaveData pendingWipSave = runtime.Capture();
            runtime.Restore(pendingWipSave);
            yield return null;
            bool wipAcknowledged = gateway.AcknowledgeWipInput(
                wipReceipt.CommitId,
                out string acknowledgeFailure);
            int afterWipQuantity = runtime.GetAllStacks()
                .Where(stack => string.Equals(stack.ItemId, treatedLumber, StringComparison.Ordinal)
                    || string.Equals(stack.ItemId, caveMushroom, StringComparison.Ordinal))
                .Sum(stack => stack.Quantity);
            Check(wipLumberSpawned && wipLumberAmount == 1
                    && wipMushroomSpawned && wipMushroomAmount == 1
                    && wipCommitted
                    && wipReceipt.IsCommitted
                    && wipReceipt.Quantity == 2
                    && wipReceipt.InputMassGrams == 1_400L
                    && gateway.CountPendingMassGrams(destination) == 0L
                    && wipAcknowledged
                    && afterWipQuantity == 8,
                "PRODUCTION_INPUT_BUFFER_WIP_CONSUME_RESTORE_EXACT",
                $"spawn={wipLumberSpawned}:{wipLumberAmount}/{wipMushroomSpawned}:{wipMushroomAmount}; "
                + $"commit={wipCommitted}:{wipReceipt.CommitId}:{wipReceipt.Quantity}:{wipReceipt.InputMassGrams}:{wipFailure}; "
                + $"ack={wipAcknowledged}:{acknowledgeFailure}; mass={gateway.CountPendingMassGrams(destination)}; "
                + $"quantity={afterWipQuantity}");
        }
        finally
        {
            Destroy(action);
            if (!lifecycle.TryReplaceOwnedAuthorities(
                    ProductionInputDestinationClaimRuntime.OwnerDomain,
                    previousClaims,
                    previousProfiles,
                    out string cleanupFailure))
            {
                Debug.LogError(
                    "Production input-buffer verifier authority cleanup failed: "
                    + cleanupFailure);
            }
        }
    }

    private IEnumerator VerifyPreparedOutputWarehouseLiveRoute(
        DungeonRuntimeLifetimeScope scope,
        IWorldItemStackRuntime runtime,
        Grid grid,
        CharacterActor hauler,
        IWarehouseMassAdmissionService warehouseMassAdmission,
        PreparedOutputLiveRouteCase verificationCase,
        IDungeonSaveSectionRegistry saveRegistry,
        List<DungeonSaveSectionEnvelope> sceneBaseline)
    {
        if (verificationCase.IsSynthetic)
        {
            Check(saveRegistry != null && sceneBaseline != null,
                "PREPARED_OUTPUT_CANARY_SAVE_REGISTRY_READY",
                saveRegistry != null && sceneBaseline != null
                    ? $"registry=ready; sections={sceneBaseline.Count}"
                    : "registry or pre-quarantine baseline missing");
            if (saveRegistry == null || sceneBaseline == null)
                yield break;
        }

        IFacilityOutputClearanceTelemetryControl clearanceTelemetry =
            verificationCase.VerifiesClearanceMeasurement
                ? Resolve<IFacilityOutputClearanceTelemetryControl>(scope)
                : null;
        if (verificationCase.VerifiesClearanceMeasurement)
        {
            Check(clearanceTelemetry != null,
                "OUTPUT_CLEARANCE_TELEMETRY_RUNTIME_READY",
                clearanceTelemetry != null
                    ? "live telemetry control resolved"
                    : "live telemetry control missing");
            if (clearanceTelemetry == null)
                yield break;
        }

        if (verificationCase.IsCropNaturalClearance)
        {
            IEnumerator cropFocused = VerifyCropOutputClearanceNaturalFocused(
                scope,
                runtime,
                grid,
                hauler,
                warehouseMassAdmission,
                verificationCase,
                clearanceTelemetry);
            try
            {
                while (cropFocused.MoveNext())
                    yield return cropFocused.Current;
            }
            finally
            {
                (cropFocused as IDisposable)?.Dispose();
                if (clearanceTelemetry?.IsCaptureActive == true)
                {
                    FacilityOutputClearanceTelemetrySnapshot interrupted =
                        clearanceTelemetry.EndCapture();
                    Check(false,
                        "OUTPUT_CLEARANCE_CROP_FOCUSED_CAPTURE_NOT_TERMINAL",
                        $"active={interrupted.ActiveBatchCount}; completed="
                        + interrupted.Completed.Count);
                }
                if (sceneBaseline != null)
                {
                    RestorePreparedOutputCanaryBaseline(
                        saveRegistry,
                        sceneBaseline);
                    DiscardRestoredPreparedOutputFixtureReferences();
                }
            }
            yield break;
        }

        if (verificationCase.ClearanceSeedCount > 1)
        {
            IEnumerator multiSeed = VerifyPreparedOutputWarehouseNatural32Seeds(
                scope,
                runtime,
                hauler,
                warehouseMassAdmission,
                verificationCase,
                saveRegistry,
                clearanceTelemetry);
            try
            {
                while (multiSeed.MoveNext())
                    yield return multiSeed.Current;
            }
            finally
            {
                (multiSeed as IDisposable)?.Dispose();
                if (clearanceTelemetry?.IsCaptureActive == true)
                {
                    FacilityOutputClearanceTelemetrySnapshot interrupted =
                        clearanceTelemetry.EndCapture();
                    Check(false,
                        "OUTPUT_CLEARANCE_32_SEED_CAPTURE_NOT_TERMINAL",
                        $"active={interrupted.ActiveBatchCount}; completed="
                        + interrupted.Completed.Count);
                }
                if (sceneBaseline != null)
                {
                    RestorePreparedOutputCanaryBaseline(
                        saveRegistry,
                        sceneBaseline);
                    DiscardRestoredPreparedOutputFixtureReferences();
                }
            }
            yield break;
        }

        IEnumerator core = VerifyPreparedOutputWarehouseLiveRouteCore(
            scope,
            runtime,
            grid,
            hauler,
            warehouseMassAdmission,
            verificationCase,
            saveRegistry);
        try
        {
            while (core.MoveNext())
                yield return core.Current;
        }
        finally
        {
            (core as IDisposable)?.Dispose();
            if (clearanceTelemetry?.IsCaptureActive == true)
            {
                FacilityOutputClearanceTelemetrySnapshot clearance =
                    clearanceTelemetry.EndCapture();
                FacilityOutputClearanceSampleSnapshot sample =
                    clearance.Completed.Count == 1
                        ? clearance.Completed[0]
                        : default;
                bool exactNaturalSample = clearance.IsClean
                    && clearance.Completed.Count == 1
                    && sample.BatchMassGrams > 0L
                    && sample.ClearanceMicroHours > 0L;
                Check(exactNaturalSample,
                    "OUTPUT_CLEARANCE_NATURAL_SAMPLE_EXACT",
                    $"samples={clearance.Completed.Count}; clean={clearance.IsClean}; "
                    + $"active={clearance.ActiveBatchCount}; orphan="
                    + $"{clearance.OrphanPickupCount}; conflict="
                    + $"{clearance.ConflictingPublicationCount}; overPickup="
                    + $"{clearance.OverPickupCount}; capacity="
                    + $"{clearance.CapacityExceededCount}; restore="
                    + $"{clearance.RestoreInterruptionCount}; batch="
                    + $"{sample.BatchCommitId ?? "missing"}; facility="
                    + $"{sample.FacilityId ?? "missing"}; grams="
                    + $"{sample.BatchMassGrams}; microHours="
                    + $"{sample.ClearanceMicroHours}; milliHours="
                    + sample.ClearanceMilliHours);
            }
            if (sceneBaseline != null)
                RestorePreparedOutputCanaryBaseline(saveRegistry, sceneBaseline);
        }
    }

    private IEnumerator VerifyCropOutputClearanceNaturalFocused(
        DungeonRuntimeLifetimeScope scope,
        IWorldItemStackRuntime runtime,
        Grid grid,
        CharacterActor hauler,
        IWarehouseMassAdmissionService warehouseMassAdmission,
        PreparedOutputLiveRouteCase verificationCase,
        IFacilityOutputClearanceTelemetryControl clearanceTelemetry)
    {
        const string expectedDefinitionId = "building:1095";
        const string expectedWorkstationTag = "workstation:crop-plot";
        const string expectedCropId = "crop:ember-root";
        const string heavyGenomeId = "genome:ember-root:heavy";
        const long expectedBatchMassGrams = 12_950L;
        const long expectedCapacityGrams = 51_800L;
        const int goldenHarvestTraitId = 304;

        CropPlotRuntime crops = Resolve<CropPlotRuntime>(scope);
        IResourceEconomyContentCatalog content =
            Resolve<IResourceEconomyContentCatalog>(scope);
        IItemTransferService transfers = Resolve<IItemTransferService>(scope);
        IWorkExecutionHandlerRegistry workHandlers =
            Resolve<IWorkExecutionHandlerRegistry>(scope);
        IFacilityBufferMassCapacityQuery capacities =
            Resolve<IFacilityBufferMassCapacityQuery>(scope);
        IFacilityBufferPlannedOutputPublicationService publication =
            Resolve<IFacilityBufferPlannedOutputPublicationService>(scope);
        IProductionAssemblyBridge productionBridge =
            Resolve<IProductionAssemblyBridge>(scope);
        IProductionWorkshopRuntime workshops =
            Resolve<IProductionWorkshopRuntime>(scope);
        IFacilityCapabilityQuery facilityCapabilities =
            Resolve<IFacilityCapabilityQuery>(scope);
        IRoomLayoutCache roomLayouts = Resolve<IRoomLayoutCache>(scope);
        IRandomStreamProvider random = Resolve<IRandomStreamProvider>(scope);
        IRandomStreamDiagnosticsQuery randomDiagnostics =
            Resolve<IRandomStreamDiagnosticsQuery>(scope);
        IRunSeedProvider runSeed = Resolve<IRunSeedProvider>(scope);
        IGameClock gameClock = Resolve<IGameClock>(scope);
        IGameClockDiagnosticsControl gameClockDiagnostics =
            gameClock as IGameClockDiagnosticsControl;
        ISurvivalEnvironmentQuery survivalEnvironment =
            Resolve<ISurvivalEnvironmentQuery>(scope);
        ISurvivalFoodDebugCommand survivalDebug =
            Resolve<ISurvivalFoodDebugCommand>(scope);
        ProgressionSceneRuntimeReferences progression =
            Resolve<ProgressionSceneRuntimeReferences>(scope);

        ProductionOutputClearanceMeasurementScopeSnapshot measurementScope =
            ProductionAuthoredThroughputFacilityScopeDebugScenarios
                .CaptureMeasurementScope(scope.Container);
        ProductionOutputClearanceMeasurementPortfolioSnapshot portfolio =
            ProductionOutputClearanceMeasurementPortfolioAuthority
                .CaptureCurrent(measurementScope);
        ProductionOutputClearanceExecutableDescriptorCoverage coverage =
            ProductionAuthoredThroughputFacilityScopeDebugScenarios
                .CaptureRecipeExecutableDescriptors(
                    scope.Container,
                    measurementScope);
        ProductionOutputClearanceExecutableDescriptor descriptor = coverage
            .Descriptors
            .SingleOrDefault(value => value?.Payload is
                    ProductionOutputClearanceCropHarvestExecutablePayload payload
                && string.Equals(
                    value.Plan.DefinitionId,
                    expectedDefinitionId,
                    StringComparison.Ordinal)
                && string.Equals(
                    value.Plan.WorkstationTag,
                    expectedWorkstationTag,
                    StringComparison.Ordinal)
                && string.Equals(
                    payload.CropId,
                    expectedCropId,
                    StringComparison.Ordinal)
                && !payload.Indoor);
        ProductionOutputClearanceCropHarvestExecutablePayload cropPayload =
            descriptor?.Payload as
                ProductionOutputClearanceCropHarvestExecutablePayload;
        ProductionOutputClearanceMeasurementFixture fixture = portfolio
            .Fixtures
            .SingleOrDefault(value => value.SeedIndex == 3
                && value.DeterministicSeed == 157184
                && descriptor != null
                && string.Equals(
                    value.Plan.SourceDigest,
                    descriptor.Plan.SourceDigest,
                    StringComparison.Ordinal));
        bool descriptorExact = coverage.Gaps.Count == 0
            && descriptor != null
            && cropPayload != null
            && fixture != null
            && descriptor.Plan.Winner.Source.MaximumSingleCompletionMassGrams
                == expectedBatchMassGrams
            && descriptor.OutputBufferCycleCapacity == 4
            && cropPayload.Outputs.Count == 2
            && cropPayload.Outputs.Sum(value => value.MassGrams)
                == expectedBatchMassGrams;
        Check(descriptorExact,
            "OUTPUT_CLEARANCE_CROP_DESCRIPTOR_EXACT",
            $"descriptors={coverage.Descriptors.Count}; gaps={coverage.Gaps.Count}; "
            + $"descriptor={descriptor != null}; fixture="
            + $"{fixture?.ObservationId ?? "missing"}; outputs="
            + $"{cropPayload?.Outputs.Count ?? 0}; mass="
            + $"{cropPayload?.Outputs.Sum(value => value.MassGrams) ?? 0L}; "
            + $"capacityCycles={descriptor?.OutputBufferCycleCapacity ?? 0}");

        bool runtimeReady = descriptorExact
            && verificationCase.IsCropNaturalClearance
            && runtime != null
            && grid != null
            && hauler != null
            && warehouseMassAdmission != null
            && clearanceTelemetry != null
            && crops != null
            && content != null
            && transfers != null
            && workHandlers != null
            && capacities != null
            && publication != null
            && productionBridge != null
            && workshops != null
            && facilityCapabilities != null
            && roomLayouts != null
            && random != null
            && randomDiagnostics != null
            && runSeed != null
            && gameClock != null
            && gameClockDiagnostics != null
            && survivalEnvironment != null
            && survivalDebug != null
            && progression?.BlueprintResearch != null;
        Check(runtimeReady,
            "OUTPUT_CLEARANCE_CROP_RUNTIME_READY",
            $"descriptor={descriptorExact}; crop={crops != null}; content="
            + $"{content != null}; transfers={transfers != null}; handlers="
            + $"{workHandlers != null}; capacity={capacities != null}; "
            + $"publication={publication != null}; bridge="
            + $"{productionBridge != null}; workshops={workshops != null}; "
            + $"capabilities={facilityCapabilities != null}; rooms="
            + $"{roomLayouts != null}; random="
            + $"{random != null}/{randomDiagnostics != null}; runSeed="
            + $"{runSeed?.RunSeed}; clock={gameClockDiagnostics != null}; "
            + $"weather={survivalEnvironment != null}/{survivalDebug != null}; "
            + $"research={progression?.BlueprintResearch != null}");
        if (!runtimeReady)
            yield break;

        BuildingSO cropAsset = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            verificationCase.FacilityAssetPath);
        BuildingSO soilDiagnosticsAsset =
            AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/ResearchOverhaul/RF52_토양_검사대.asset");
        BuildingSO seedSelectionAsset =
            AssetDatabase.LoadAssetAtPath<BuildingSO>(
                "Assets/Resources/SO/Building/ResearchOverhaul/RF90_종자_선별대.asset");
        BuildingSO warehouseAsset = FindWarehouseAsset();
        progression.BlueprintResearch.State.Projects.RestoreCompleted(
            new ResearchProjectId("research:agriculture:field"));

        IReadOnlyList<Vector2Int> positions = FindReachableCells(
            grid,
            hauler.GetNowXY(),
            256);
        Vector2Int[] usableRoomPositions = positions
            .Where(position => roomLayouts.TryGetRoom(
                    grid,
                    position,
                    out RoomInstance room)
                && room != null
                && room.IsUsable)
            .ToArray();
        bool cropPositionReady = TryFindRegisterablePosition(
            grid,
            cropAsset,
            positions,
            out Vector2Int cropPosition);
        Facility plot = CreateInjectedFacility(
            scope,
            grid,
            cropAsset,
            cropPosition,
            "QA_Crop_Output_Clearance_P23",
            registerOnGrid: true);
        Vector2Int warehousePosition = default;
        bool warehousePositionReady = plot != null
            && TryFindRegisterablePosition(
                grid,
                warehouseAsset,
                positions,
                out warehousePosition);
        Facility warehouse = CreateInjectedFacility(
            scope,
            grid,
            warehouseAsset,
            warehousePosition,
            "QA_Crop_Output_Clearance_Warehouse",
            registerOnGrid: true);
        RegisterTemporaryWarehouse(scope, warehouse);
        Vector2Int soilPosition = default;
        bool soilPositionReady = warehouse != null
            && TryFindRegisterablePosition(
                grid,
                soilDiagnosticsAsset,
                usableRoomPositions,
                out soilPosition);
        Facility soilDiagnostics = CreateInjectedFacility(
            scope,
            grid,
            soilDiagnosticsAsset,
            soilPosition,
            "QA_Crop_Output_Clearance_RF52",
            registerOnGrid: true);
        Vector2Int seedPosition = default;
        bool seedPositionReady = soilDiagnostics != null
            && TryFindRegisterablePosition(
                grid,
                seedSelectionAsset,
                usableRoomPositions,
                out seedPosition);
        Facility seedSelection = CreateInjectedFacility(
            scope,
            grid,
            seedSelectionAsset,
            seedPosition,
            "QA_Crop_Output_Clearance_RF90",
            registerOnGrid: true);
        crops.Tick();
        ClearInventory(warehouse?.Inventory);

        bool soilOperational = false;
        bool seedOperational = false;
        float facilityPublicationDeadline = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < facilityPublicationDeadline)
        {
            soilOperational = facilityCapabilities
                .FindOperational(ResearchFacilityCommandKind.SoilDiagnostics)
                .Count(value => ReferenceEquals(value, soilDiagnostics)) == 1;
            seedOperational = facilityCapabilities
                .FindOperational(ResearchFacilityCommandKind.SeedSelection)
                .Count(value => ReferenceEquals(value, seedSelection)) == 1;
            if (soilOperational && seedOperational)
                break;
            yield return null;
        }

        bool facilitiesExact = cropPositionReady
            && warehousePositionReady
            && soilPositionReady
            && seedPositionReady
            && plot != null
            && warehouse?.Inventory?.HasMassCapacityAuthority == true
            && soilDiagnostics != null
            && seedSelection != null
            && soilOperational
            && seedOperational;
        Check(facilitiesExact,
            "OUTPUT_CLEARANCE_CROP_FACILITIES_EXACT",
            $"plot={plot?.PersistentInstanceId.Value ?? "missing"}@{cropPosition}; "
            + $"warehouse={DescribeWarehouse(warehouse)}; soil="
            + $"{soilDiagnostics?.PersistentInstanceId.Value ?? "missing"}; seed="
            + $"{seedSelection?.PersistentInstanceId.Value ?? "missing"}; "
            + $"positions={cropPositionReady}/{warehousePositionReady}/"
            + $"{soilPositionReady}/{seedPositionReady}; warehouseMass="
            + $"{warehouse?.Inventory?.HasMassCapacityAuthority}; operational="
            + $"{soilOperational}/{seedOperational}; usableRoomCells="
            + usableRoomPositions.Length);
        if (!facilitiesExact)
            yield break;

        string plotId = plot.RequirePersistentInstanceId().Value;
        string outputDestinationId = ProductionOutputDestinationId
            .FromFacility(plot.RequirePersistentInstanceId())
            .Value;
        FacilityBufferMassCapacitySnapshot outputCapacity = default;
        float capacityDeadline = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < capacityDeadline
            && (!capacities.TryGetCapacity(
                    outputDestinationId,
                    plot.centerPos,
                    out outputCapacity)
                || outputCapacity.Profile == null
                || outputCapacity.Profile.MaxMassGrams
                    != expectedCapacityGrams))
        {
            yield return null;
        }
        bool capacityExact = outputCapacity.Profile != null
            && outputCapacity.Profile.MaxMassGrams == expectedCapacityGrams
            && outputCapacity.ReservedMassGrams == 0L;
        Check(capacityExact,
            "OUTPUT_CLEARANCE_CROP_BUFFER_4_CYCLES_EXACT",
            $"destination={outputDestinationId}; capacity="
            + $"{outputCapacity.Profile?.MaxMassGrams ?? 0L}/"
            + $"{expectedCapacityGrams}; reserved="
            + outputCapacity.ReservedMassGrams);
        if (!capacityExact)
            yield break;

        CharacterActor cropWorker = null;
        CharacterSO cropWorkerData = null;
        GameObject cropWorkerObject = null;
        int originalRandomRoot = random.RootSeed;
        IReadOnlyList<RandomStreamStateSnapshot> originalRandomStates =
            random.CaptureStates();
        SurvivalWeatherType originalWeather = survivalEnvironment
            .GetEnvironmentSnapshot()
            .Weather;
        float originalFocusedTimeScale = Time.timeScale;
        try
        {
            survivalDebug.DebugSetWeather(cropPayload.Weather);
            string cropWorkerId = Enumerable.Range(0, 10_000)
                .Select(index =>
                    $"character:qa:crop-clearance-witness:{index:D4}")
                .First(value => GoldenHarvestDeterministicOutcomeAuthority
                    .CaptureRoll01(
                        unchecked((ulong)(uint)runSeed.RunSeed),
                        plotId,
                        0,
                        value) < 0.12f);
            cropWorkerObject = CharacterAiPlanDebugFixtures.CreateActorObject(
                "Crop Output Clearance Witness");
            cropWorkerObject.SetActive(false);
            if (cropWorkerObject.GetComponent<AbilityWork>() == null)
                cropWorkerObject.AddComponent<AbilityWork>();
            InjectGameObject(scope, cropWorkerObject);
            cropWorkerData = CharacterAiEditorTestDependencies
                .CreateCharacterFixtureData(
                    CharacterType.NPC,
                    "Crop Output Clearance Witness",
                    "Beastkin");
            cropWorker = cropWorkerObject.GetComponent<CharacterActor>();
            cropWorker.EnsureRuntimeState();
            cropWorker.Identity.SetPersistentId(new CharacterId(cropWorkerId));
            scope.Container.Resolve<ICharacterNarrativeCommand>().Register(
                new CharacterId(cropWorkerId),
                new CharacterSpeciesId("Beastkin"),
                Array.Empty<string>(),
                Array.Empty<string>(),
                BuiltInCharacterProficiencyIds.All.Select(id =>
                    new CharacterStartingProficiencyExperience
                    {
                        proficiencyId = id.Value,
                        experience = 100,
                        learningMultiplier = 1f
                    }).ToArray());
            cropWorkerObject.SetActive(true);
            cropWorker.RefreshAbilityCache();
            cropWorker.Initialize(cropWorkerData);
            cropWorker.Progression.ApplyPreparedIdentity(
                "Crop Output Clearance Witness",
                "Beastkin",
                new[] { goldenHarvestTraitId },
                CharacterPotentialGrade.Ordinary,
                fixture.DeterministicSeed,
                autoChooseDrafts: false);
            cropWorker.SetLifecycleState(CharacterLifecycleState.Active);
            cropWorker.SetAiPaused(true);
            ICharacterProficiencyCommand proficiencies =
                Resolve<ICharacterProficiencyCommand>(scope);
            IGameCalendar calendar = Resolve<IGameCalendar>(scope);
            CharacterId workerCharacterId =
                CharacterPersistentIdentity.Require(cropWorker);
            proficiencies.AddDirectExperience(
                workerCharacterId,
                BuiltInCharacterProficiencyIds.FoodProduction,
                3060f,
                calendar.AbsoluteHour,
                applyLearningMultiplier: false);
            proficiencies.AddDirectExperience(
                workerCharacterId,
                BuiltInCharacterProficiencyIds.Fieldwork,
                3060f,
                calendar.AbsoluteHour,
                applyLearningMultiplier: false);
            Check(GoldenHarvestDeterministicOutcomeAuthority.CaptureRoll01(
                    unchecked((ulong)(uint)runSeed.RunSeed),
                    plotId,
                    0,
                    cropWorkerId) < 0.12f,
                "OUTPUT_CLEARANCE_CROP_GOLDEN_KEY_REACHABLE",
                $"runSeed={runSeed.RunSeed}; plot={plotId}; attempt=0; "
                + $"actor={cropWorkerId}");

            bool cropReady = content.TryGetCrop(
                expectedCropId,
                out CropDefinitionSO crop)
                && crop != null
                && crops.TrySetCrop(
                    plot,
                    expectedCropId,
                    out string setCropMessage)
                && crops.TryBindNextCycle(
                    fixture.ObservationId,
                    plotId,
                    expectedCropId,
                    out string bindFailure);
            crops.Tick();
            CropPlotSnapshot waiting = crops.Plots.SingleOrDefault(value =>
                string.Equals(value.PlotId, plotId, StringComparison.Ordinal));
            KeyValuePair<string, int>[] expectedInputs = cropPayload.Inputs
                .Select(value => new KeyValuePair<string, int>(
                    value.ItemId,
                    value.Quantity))
                .OrderBy(value => value.Key, StringComparer.Ordinal)
                .ToArray();
            KeyValuePair<string, int>[] runtimeInputs = waiting?
                .RequiredMaterials
                .OrderBy(value => value.Key, StringComparer.Ordinal)
                .ToArray()
                ?? Array.Empty<KeyValuePair<string, int>>();
            bool inputVectorExact = cropReady
                && waiting != null
                && runtimeInputs.SequenceEqual(expectedInputs);
            Check(inputVectorExact,
                "OUTPUT_CLEARANCE_CROP_INPUT_VECTOR_EXACT",
                $"ready={cropReady}; phase={waiting?.Phase}; destination="
                + $"{waiting?.MaterialDestinationId ?? "missing"}; expected="
                + string.Join("|", expectedInputs.Select(value =>
                    value.Key + "x" + value.Value))
                + "; actual=" + string.Join("|", runtimeInputs.Select(value =>
                    value.Key + "x" + value.Value)));
            if (!inputVectorExact)
                yield break;

            bool inputsSpawned = true;
            foreach (KeyValuePair<string, int> input in runtimeInputs)
            {
                bool spawned = string.Equals(
                        input.Key,
                        crop.SeedItemId,
                        StringComparison.Ordinal)
                    ? transfers.TrySpawnItemWithComponents(
                        input.Key,
                        input.Value,
                        plot.centerPos,
                        WorldItemStackState.FacilityBuffer,
                        waiting.MaterialDestinationId,
                        new[]
                        {
                            SeedLotItemStateCodec.Encode(new SeedLotState
                            {
                                cropId = crop.CropId,
                                cultivarGenomeId = heavyGenomeId,
                                generation = 0,
                                pathogenLoad = 0f
                            })
                        },
                        out int spawnedSeed)
                        && spawnedSeed == input.Value
                    : runtime.SpawnItemAt(
                        input.Key,
                        input.Value,
                        plot.centerPos,
                        WorldItemStackState.FacilityBuffer,
                        waiting.MaterialDestinationId,
                        out int spawnedMaterial)
                        && spawnedMaterial == input.Value;
                inputsSpawned &= spawned;
            }
            crops.Tick();
            WorkExecutionResult sowResult = null;
            string sowFailure = string.Empty;
            bool sowExecuted = inputsSpawned
                && ExecuteCropWorkThroughProductionHandler(
                    workHandlers,
                    cropWorker,
                    plot,
                    BuiltInWorkTypeIds.Sow,
                    out sowResult,
                    out sowFailure)
                && sowResult.CompletedSuccessfully;
            Check(sowExecuted,
                "OUTPUT_CLEARANCE_CROP_SOW_PRODUCTION_HANDLER_EXACT",
                $"spawned={inputsSpawned}; completed="
                + $"{sowResult?.CompletedSuccessfully}; failure={sowFailure}");
            if (!sowExecuted)
                yield break;

            Time.timeScale = 64f;
            CropPlotWorkSnapshot harvestWork = default;
            CropPlotSnapshot growthSnapshot = null;
            bool harvestReady = false;
            float accumulatedGrowthDeltaTime = 0f;
            int growthFrames = 0;
            // The focused fixture intentionally exercises the full production
            // graph, so one editor frame can exceed a second on a cold run.
            // Keep natural clock-driven growth and allow enough wall time for
            // the final frame instead of mutating CropPlotSaveData or bypassing
            // the production growth authority.
            float growthDeadline = Time.realtimeSinceStartup + 30f;
            while (Time.realtimeSinceStartup < growthDeadline)
            {
                EnsureVerificationTimeScale();
                accumulatedGrowthDeltaTime += gameClock.DeltaTime;
                growthFrames++;
                crops.Tick();
                growthSnapshot = crops.Plots.SingleOrDefault(value =>
                    value.PlotId == plotId);
                if (crops.TryGetWork(
                        plot,
                        BuiltInWorkTypeIds.Harvest,
                        out harvestWork)
                    && harvestWork.Available)
                {
                    harvestReady = true;
                    break;
                }
                yield return null;
            }
            Check(harvestReady,
                "OUTPUT_CLEARANCE_CROP_NATURAL_GROWTH_READY",
                $"ready={harvestReady}; required={harvestWork.RequiredWork:R}; "
                + $"completed={harvestWork.CompletedWork:R}; phase="
                + $"{growthSnapshot?.Phase}; progress="
                + $"{growthSnapshot?.GrowthProgress:R}; blocked="
                + $"{growthSnapshot?.BlockedReason}; frames={growthFrames}; "
                + $"accumulatedDelta={accumulatedGrowthDeltaTime:R}; "
                + $"clockDelta={gameClock.DeltaTime:R}; scale={Time.timeScale:R}");
            if (!harvestReady)
                yield break;

            bool goldenScheduled = crops.TryScheduleGoldenHarvest(
                plot,
                cropWorker,
                out string goldenScheduleReason);
            if (goldenScheduled)
            {
                gameClockDiagnostics.RebaseDeterministicCheckpointTime(
                    gameClock.Time + GameCalendarRules.SecondsPerDay,
                    checked(gameClock.FrameCount + 1));
            }
            float goldenRemainingSeconds = float.PositiveInfinity;
            bool goldenMature = goldenScheduled
                && !crops.TryGetGoldenHarvestDelay(
                    plot,
                    cropWorker,
                    out goldenRemainingSeconds);
            Check(goldenMature,
                "OUTPUT_CLEARANCE_CROP_GOLDEN_MATURE",
                $"scheduled={goldenScheduled}; reason={goldenScheduleReason}; "
                + $"remaining={goldenRemainingSeconds:R}; clock="
                + $"{gameClock.Time:R}/{gameClock.FrameCount}");
            if (!goldenMature)
                yield break;

            string topologyBefore = CaptureOutputClearanceTopologyDigest(
                productionBridge,
                workshops,
                plot);
            string topologySource =
                CaptureOutputClearanceTopologySourceDigest(
                    productionBridge,
                    workshops,
                    plot);
            // Growth uses an accelerated game clock. Return to real time and
            // cross one frame boundary before beginning the clearance-only
            // measurement so the operating-day calendar cannot attribute the
            // final accelerated frame (and its autosave) to haul clearance.
            Time.timeScale = 1f;
            for (int settlementFrame = 0; settlementFrame < 4; settlementFrame++)
                yield return null;
            random.Reseed(fixture.DeterministicSeed);
            IReadOnlyList<RandomStreamDiagnosticSnapshot> randomBefore =
                randomDiagnostics.Capture();
            activeNaturalClearanceSeedRun = new NaturalClearanceSeedRunState
            {
                SeedIndex = fixture.SeedIndex,
                DeterministicSeed = fixture.DeterministicSeed,
                RuntimeFacilityId = plotId,
                DefinitionId = descriptor.Plan.DefinitionId,
                WorkstationTag = descriptor.Plan.WorkstationTag,
                RecipeId = cropPayload.BranchId,
                OutputLineId = string.Join(
                    "+",
                    cropPayload.Outputs.Select(value => value.OutputLineId)),
                ItemId = string.Join(
                    "+",
                    cropPayload.Outputs.Select(value => value.ItemId)),
                OutputQuantity = cropPayload.Outputs.Sum(value => value.Quantity),
                BatchMassGrams = expectedBatchMassGrams,
                TopologySourceDigest = topologySource,
                RuntimeTopologyBeforeDigest = topologyBefore,
                RandomBefore = randomBefore
            };
            clearanceTelemetry.BeginCapture(
                "v27.output-clearance.crop.focused");
            WorkExecutionResult harvestResult = null;
            string harvestFailure = string.Empty;
            bool harvested = ExecuteCropWorkThroughProductionHandler(
                    workHandlers,
                    cropWorker,
                    plot,
                    BuiltInWorkTypeIds.Harvest,
                    out harvestResult,
                    out harvestFailure)
                && harvestResult.CompletedSuccessfully;
            ProductionOutputClearanceNaturalMeasurementHandlerRegistry handlers =
                new(new IProductionOutputClearanceNaturalMeasurementHandler[]
                {
                    new ProductionOutputClearanceCropHarvestNaturalMeasurementHandler(
                        crops,
                        crops,
                        publication)
                });
            ProductionOutputClearanceExecutionReceiptSnapshot receipt = null;
            string receiptFailure = string.Empty;
            bool receiptCaptured = harvested
                && handlers.TryCaptureCompleted(
                    descriptor,
                    fixture.ObservationId,
                    out receipt,
                    out receiptFailure);
            bool receiptExact = receiptCaptured
                && receipt != null
                && receipt.ActualBatchMassGrams == expectedBatchMassGrams
                && receipt.Outputs.Count == 2
                && receipt.Outputs.Sum(value => value.MassGrams)
                    == expectedBatchMassGrams;
            Check(receiptExact,
                "OUTPUT_CLEARANCE_CROP_RECEIPT_PHYSICAL_VECTOR_EXACT",
                $"harvested={harvested}; completion="
                + $"{harvestResult?.CompletedSuccessfully}; harvestFailure="
                + $"{harvestFailure}; captured={receiptCaptured}; "
                + $"receiptFailure={receiptFailure}; operation="
                + $"{receipt?.OperationId ?? "missing"}; batch="
                + $"{receipt?.BatchCommitId ?? "missing"}; outputs="
                + $"{receipt?.Outputs.Count ?? 0}; mass="
                + $"{receipt?.ActualBatchMassGrams ?? 0L}");
            if (!receiptExact)
                yield break;

            IEnumerator naturalClearance =
                VerifySchedulerOwnedPreparedOutputClearance(
                    runtime,
                    hauler,
                    warehouse,
                    receipt.Outputs.Select(value =>
                        new NaturalClearanceExpectedSlice(
                            value.StackId,
                            value.ItemId,
                            value.Quantity,
                            value.MassGrams))
                        .ToArray(),
                    expectedBatchMassGrams,
                    WarehouseStorageIdentity.RequireDestinationId(warehouse));
            try
            {
                while (naturalClearance.MoveNext())
                    yield return naturalClearance.Current;
            }
            finally
            {
                (naturalClearance as IDisposable)?.Dispose();
            }

            activeNaturalClearanceSeedRun.RuntimeTopologyAfterDigest =
                CaptureOutputClearanceTopologyDigest(
                    productionBridge,
                    workshops,
                    plot);
            FacilityOutputClearanceTelemetrySnapshot telemetry =
                clearanceTelemetry.EndCapture();
            FacilityOutputClearanceSampleSnapshot sample =
                telemetry.Completed.Count == 1
                    ? telemetry.Completed[0]
                    : default;
            IReadOnlyList<RandomStreamDiagnosticSnapshot> randomAfter =
                randomDiagnostics.Capture();
            string randomDigest = CaptureNaturalClearanceRandomDigest(
                randomAfter);
            long randomDrawDelta = CaptureNaturalClearanceRandomDrawDelta(
                randomBefore,
                randomAfter);
            bool observationIdentityReady =
                !string.IsNullOrWhiteSpace(
                    activeNaturalClearanceSeedRun.OwnerRosterKey)
                && sample.ClearanceMicroHours > 0L;
            Check(observationIdentityReady,
                "OUTPUT_CLEARANCE_CROP_OBSERVATION_IDENTITY_READY",
                $"ownerRosterKey="
                + $"{activeNaturalClearanceSeedRun.OwnerRosterKey}; "
                + $"clearanceMicroHours={sample.ClearanceMicroHours}; "
                + $"telemetryCompleted={telemetry.Completed.Count}; "
                + $"deliveryExact="
                + $"{activeNaturalClearanceSeedRun.DeliveryExact}; "
                + $"schedulerExact="
                + $"{activeNaturalClearanceSeedRun.SchedulerProvenanceExact}");
            if (!observationIdentityReady)
                yield break;
            ProductionOutputClearanceNaturalObservationRecord observation =
                new(
                    fixture,
                    receipt.RuntimeFacilityId,
                    receipt.ResolvedOutputVectorDigest,
                    receipt.ActualBatchMassGrams,
                    receipt.BatchCommitId,
                    topologySource,
                    string.Equals(
                        activeNaturalClearanceSeedRun
                            .RuntimeTopologyBeforeDigest,
                        activeNaturalClearanceSeedRun
                            .RuntimeTopologyAfterDigest,
                        StringComparison.Ordinal),
                    string.Equals(
                        sample.FacilityId,
                        receipt.RuntimeFacilityId,
                        StringComparison.Ordinal),
                    activeNaturalClearanceSeedRun.OwnerRosterKey,
                    activeNaturalClearanceSeedRun.ActionEpochDelta,
                    activeNaturalClearanceSeedRun.ActionStartDelta,
                    activeNaturalClearanceSeedRun.HaulStartDelta,
                    sample.ClearanceMicroHours,
                    telemetry.Completed.Count,
                    telemetry.ActiveBatchCount,
                    telemetry.OrphanPickupCount,
                    telemetry.ConflictingPublicationCount,
                    telemetry.OverPickupCount,
                    telemetry.CapacityExceededCount,
                    telemetry.RestoreInterruptionCount,
                    telemetry.IsClean,
                    activeNaturalClearanceSeedRun.SchedulerProvenanceExact,
                    activeNaturalClearanceSeedRun.DeliveryExact,
                    randomDigest,
                    randomDrawDelta);
            bool accepted = observation.IsExact;
            string acknowledgeFailure = string.Empty;
            bool acknowledged = accepted
                && handlers.TryAcknowledgeAccepted(
                    receipt,
                    out acknowledgeFailure);
            string duplicateAcknowledgeFailure = string.Empty;
            bool duplicateRejected = acknowledged
                && !handlers.TryAcknowledgeAccepted(
                    receipt,
                    out duplicateAcknowledgeFailure);
            Check(accepted && acknowledged && duplicateRejected,
                "OUTPUT_CLEARANCE_CROP_OBSERVATION_AT4_ACK_EXACT_ONCE",
                $"schema={ProductionOutputClearanceNaturalObservationRecord.Schema}; "
                + $"exact={accepted}; acknowledged={acknowledged}:"
                + $"{acknowledgeFailure}; duplicateRejected="
                + $"{duplicateRejected}:{duplicateAcknowledgeFailure}; "
                + $"microHours={observation.ClearanceMicroHours}; "
                + $"randomDrawDelta={observation.RandomDrawDelta}; digest="
                + observation.RunSourceDigest);
        }
        finally
        {
            activeNaturalClearanceSeedRun = null;
            Time.timeScale = originalFocusedTimeScale;
            survivalDebug.DebugSetWeather(originalWeather);
            random.RestoreStates(originalRandomRoot, originalRandomStates);
            if (cropWorkerObject != null)
                Destroy(cropWorkerObject);
            if (cropWorkerData != null)
                Destroy(cropWorkerData);
        }
    }

    private static bool ExecuteCropWorkThroughProductionHandler(
        IWorkExecutionHandlerRegistry registry,
        CharacterActor actor,
        BuildableObject target,
        WorkTypeId workTypeId,
        out WorkExecutionResult result,
        out string failureReason)
    {
        result = new WorkExecutionResult();
        failureReason = string.Empty;
        if (registry == null
            || actor == null
            || target == null
            || !registry.TryGet(workTypeId, out IWorkExecutionHandler handler)
            || handler == null)
        {
            failureReason = "crop-work-handler-unavailable";
            result.CompletedSuccessfully = false;
            return false;
        }

        AbilityWork work = actor.GetComponent<AbilityWork>();
        if (work == null)
        {
            failureReason = "crop-work-ability-unavailable";
            result.CompletedSuccessfully = false;
            return false;
        }

        WorkExecutionContext context = new(
            runId: 1,
            work,
            actor,
            target,
            workTypeId,
            ExecuteImmediateWorkAmount,
            canContinue: () => true,
            executePersistentWorkAmount: ExecuteImmediatePersistentWorkAmount);
        try
        {
            DrainImmediateWorkEnumerator(handler.Execute(context, result));
            if (!result.CompletedSuccessfully)
                failureReason = "crop-work-handler-terminal-failure";
            return result.CompletedSuccessfully;
        }
        catch (Exception exception)
        {
            result.CompletedSuccessfully = false;
            failureReason = exception.GetType().Name + ":" + exception.Message;
            return false;
        }
    }

    private static IEnumerator ExecuteImmediateWorkAmount(
        float requiredWork,
        string label,
        float extraMultiplier)
    {
        if (!float.IsFinite(requiredWork)
            || requiredWork <= 0f
            || !float.IsFinite(extraMultiplier)
            || extraMultiplier <= 0f)
        {
            throw new InvalidOperationException(
                "Immediate crop work requires finite positive authority values.");
        }
        yield break;
    }

    private static IEnumerator ExecuteImmediatePersistentWorkAmount(
        float requiredWork,
        float completedWork,
        string label,
        float extraMultiplier,
        Func<float, bool> applyDelta)
    {
        float delta = requiredWork - completedWork;
        if (!float.IsFinite(requiredWork)
            || !float.IsFinite(completedWork)
            || !float.IsFinite(extraMultiplier)
            || extraMultiplier <= 0f
            || delta <= 0f
            || applyDelta == null
            || !applyDelta(delta))
        {
            throw new InvalidOperationException(
                "Immediate crop persistent work was rejected by its production owner.");
        }
        yield break;
    }

    private static void DrainImmediateWorkEnumerator(IEnumerator root)
    {
        if (root == null)
            throw new ArgumentNullException(nameof(root));
        Stack<IEnumerator> stack = new();
        stack.Push(root);
        try
        {
            while (stack.Count > 0)
            {
                IEnumerator current = stack.Peek();
                if (!current.MoveNext())
                {
                    (current as IDisposable)?.Dispose();
                    stack.Pop();
                    continue;
                }
                if (current.Current is IEnumerator nested)
                {
                    stack.Push(nested);
                    continue;
                }
                if (current.Current != null)
                {
                    throw new InvalidOperationException(
                        "Immediate crop work unexpectedly yielded a frame-bound instruction.");
                }
            }
        }
        finally
        {
            while (stack.Count > 0)
                (stack.Pop() as IDisposable)?.Dispose();
        }
    }

    private IEnumerator VerifyPreparedOutputWarehouseNatural32Seeds(
        DungeonRuntimeLifetimeScope scope,
        IWorldItemStackRuntime runtime,
        CharacterActor initialHauler,
        IWarehouseMassAdmissionService warehouseMassAdmission,
        PreparedOutputLiveRouteCase verificationCase,
        IDungeonSaveSectionRegistry saveRegistry,
        IFacilityOutputClearanceTelemetryControl clearanceTelemetry)
    {
        bool contractReady = verificationCase.VerifiesClearanceMeasurement
            && verificationCase.ClearanceSeedCount
                == NaturalClearanceRequiredSeedCount
            && saveRegistry != null
            && clearanceTelemetry != null
            && initialHauler != null;
        Check(contractReady,
            "OUTPUT_CLEARANCE_32_SEED_CONTRACT_READY",
            $"measurement={verificationCase.VerifiesClearanceMeasurement}; "
            + $"seeds={verificationCase.ClearanceSeedCount}; registry="
            + $"{saveRegistry != null}; telemetry={clearanceTelemetry != null}; "
            + $"hauler={initialHauler?.BuildingCharacterId.Value ?? "missing"}");
        if (!contractReady)
            yield break;

        IGameClock checkpointGameClock = Resolve<IGameClock>(scope);
        naturalClearanceCheckpointClock = checkpointGameClock
            as IGameClockDiagnosticsControl;
        float checkpointTime = checkpointGameClock?.Time ?? 0f;
        int checkpointFrame = checkpointGameClock?.FrameCount ?? 0;
        naturalClearanceCheckpointClock?.RebaseDeterministicCheckpointTime(
            checkpointTime,
            checkpointFrame);
        List<DungeonSaveSectionEnvelope> measurementBaseline =
            saveRegistry.CaptureAll();
        string measurementBaselineFingerprint =
            ComputeTextSha256(CaptureWholeRootSaveFingerprint(measurementBaseline));
        string initialHaulerId = initialHauler.BuildingCharacterId.Value;
        IRandomStreamProvider random = Resolve<IRandomStreamProvider>(scope);
        IRandomStreamDiagnosticsQuery randomDiagnostics =
            Resolve<IRandomStreamDiagnosticsQuery>(scope);
        CharacterBodyHealthRuntime bodyHealthDiagnostics =
            Resolve<CharacterBodyHealthRuntime>(scope);
        bool authorityReady = measurementBaseline.Count > 0
            && measurementBaselineFingerprint.Length == 64
            && random != null
            && randomDiagnostics != null
            && bodyHealthDiagnostics != null
            && naturalClearanceCheckpointClock != null;
        Check(authorityReady,
            "OUTPUT_CLEARANCE_32_SEED_BASELINE_FROZEN",
            $"sections={measurementBaseline.Count}; fingerprint="
            + $"{measurementBaselineFingerprint}; random={random != null}; "
            + $"diagnostics={randomDiagnostics != null}; clock="
            + $"body-health-diagnostics={bodyHealthDiagnostics != null}; "
            + $"{naturalClearanceCheckpointClock != null}:"
            + $"{checkpointTime:R}/{checkpointFrame}");
        if (!authorityReady)
            yield break;

        List<NaturalClearanceSeedArtifactRow> rows = new(
            NaturalClearanceRequiredSeedCount);
        for (int seedIndex = 0;
             seedIndex < NaturalClearanceRequiredSeedCount;
             seedIndex++)
        {
            int deterministicSeed = checked(NaturalClearanceFirstSeed + seedIndex);
            int failuresBeforeSeed = failures.Count;
            RestoreBrain();
            if (clearanceTelemetry.IsCaptureActive)
            {
                FacilityOutputClearanceTelemetrySnapshot leaked =
                    clearanceTelemetry.EndCapture();
                Check(false,
                    "OUTPUT_CLEARANCE_32_SEED_PRE_RESTORE_CAPTURE_INACTIVE",
                    $"seed={deterministicSeed}; active={leaked.ActiveBatchCount}; "
                    + $"completed={leaked.Completed.Count}");
                yield break;
            }

            DungeonGameRestoreReport restoreReport = new();
            naturalClearanceCheckpointClock.RebaseDeterministicCheckpointTime(
                checkpointTime,
                checkpointFrame);
            bodyHealthDiagnostics.BeginCreationDiagnosticsForEditor();
            bool restored;
            List<DungeonSaveSectionEnvelope> restoredBaseline;
            string bodyHealthCreationDiagnostics;
            try
            {
                restored = saveRegistry.RestoreAll(
                        measurementBaseline,
                        restoreReport)
                    && restoreReport.Success;
                naturalClearanceCheckpointClock.RebaseDeterministicCheckpointTime(
                    checkpointTime,
                    checkpointFrame);
                restoredBaseline = restored
                    ? saveRegistry.CaptureAll()
                    : new List<DungeonSaveSectionEnvelope>();
            }
            finally
            {
                bodyHealthCreationDiagnostics =
                    bodyHealthDiagnostics.EndCreationDiagnosticsForEditor();
            }
            string restoredFingerprint = restored
                ? ComputeTextSha256(
                    CaptureWholeRootSaveFingerprint(restoredBaseline))
                : string.Empty;
            bool restoreExact = restored
                && string.Equals(
                    restoredFingerprint,
                    measurementBaselineFingerprint,
                    StringComparison.Ordinal);
            Check(restoreExact,
                "OUTPUT_CLEARANCE_32_SEED_CHECKPOINT_RESTORED",
                $"seed={deterministicSeed}; restored={restored}; fingerprint="
                + $"{measurementBaselineFingerprint}->{restoredFingerprint}; errors="
                + string.Join(" | ", restoreReport.Errors)
                + "; diff="
                + DescribeWholeRootSaveDifference(
                    measurementBaseline,
                    restoredBaseline)
                + "; bodyHealthCreates="
                + bodyHealthCreationDiagnostics);
            if (!restoreExact)
                yield break;

            DiscardRestoredPreparedOutputFixtureReferences();
            ICharacterAiWorldRegistry world =
                Resolve<ICharacterAiWorldRegistry>(scope);
            Grid restoredGrid = null;
            bool gridReady = world != null
                && world.TryGetGrid(out restoredGrid)
                && restoredGrid != null;
            CharacterActor[] matchingHaulers = world?.Characters?
                .Where(value => value != null
                    && !value.IsDead
                    && string.Equals(
                        value.BuildingCharacterId.Value,
                        initialHaulerId,
                        StringComparison.Ordinal))
                .ToArray()
                ?? Array.Empty<CharacterActor>();
            CharacterActor restoredHauler = matchingHaulers.Length == 1
                ? matchingHaulers[0]
                : null;
            Check(gridReady && restoredHauler != null,
                "OUTPUT_CLEARANCE_32_SEED_WORLD_REACQUIRED",
                $"seed={deterministicSeed}; grid={gridReady}; haulers="
                + $"{matchingHaulers.Length}; id={initialHaulerId}");
            if (!gridReady || restoredHauler == null)
                yield break;

            ConfigureNaturalClearanceAiMeasurement();
            IEnumerator quiesce = QuiesceNaturalClearanceAiPoolBeforeFixture();
            try
            {
                while (quiesce.MoveNext())
                    yield return quiesce.Current;
            }
            finally
            {
                (quiesce as IDisposable)?.Dispose();
            }
            if (failures.Count != failuresBeforeSeed)
                yield break;

            random.Reseed(deterministicSeed);
            IReadOnlyList<RandomStreamDiagnosticSnapshot> randomBefore =
                randomDiagnostics.Capture();
            activeNaturalClearanceSeedRun = new NaturalClearanceSeedRunState
            {
                SeedIndex = seedIndex,
                DeterministicSeed = deterministicSeed,
                RandomBefore = randomBefore
            };

            IEnumerator core = VerifyPreparedOutputWarehouseLiveRouteCore(
                scope,
                runtime,
                restoredGrid,
                restoredHauler,
                warehouseMassAdmission,
                verificationCase,
                saveRegistry);
            try
            {
                while (core.MoveNext())
                    yield return core.Current;
            }
            finally
            {
                (core as IDisposable)?.Dispose();
            }

            FacilityOutputClearanceTelemetrySnapshot telemetry =
                clearanceTelemetry.IsCaptureActive
                    ? clearanceTelemetry.EndCapture()
                    : default;
            FacilityOutputClearanceSampleSnapshot sample = telemetry.Completed != null
                && telemetry.Completed.Count == 1
                    ? telemetry.Completed[0]
                    : default;
            IReadOnlyList<RandomStreamDiagnosticSnapshot> randomAfter =
                randomDiagnostics.Capture();
            string randomStateDigest = CaptureNaturalClearanceRandomDigest(
                randomAfter);
            long randomDrawDelta = CaptureNaturalClearanceRandomDrawDelta(
                randomBefore,
                randomAfter);
            NaturalClearanceSeedArtifactRow row =
                activeNaturalClearanceSeedRun == null
                    ? null
                    : new NaturalClearanceSeedArtifactRow(
                        activeNaturalClearanceSeedRun,
                        telemetry,
                        sample,
                        randomStateDigest,
                        randomDrawDelta);
            bool seedExact = row?.IsExact == true
                && failures.Count == failuresBeforeSeed;
            Check(seedExact,
                "OUTPUT_CLEARANCE_32_SEED_SAMPLE_EXACT",
                $"seed={deterministicSeed}; exact={seedExact}; owner="
                + $"{row?.OwnerRosterKey ?? "missing"}; grams="
                + $"{row?.BatchMassGrams ?? 0L}; microHours="
                + $"{row?.ClearanceMicroHours ?? 0L}; randomDrawDelta="
                + $"{row?.RandomDrawDelta ?? -1L}; topology="
                + $"{row?.TopologyStable}; facility={row?.FacilityAttributionExact}; "
                + $"telemetry={row?.TelemetryClean}");
            if (!seedExact)
                yield break;

            rows.Add(row);
            activeNaturalClearanceSeedRun = null;
        }

        bool complete = rows.Count == NaturalClearanceRequiredSeedCount
            && rows.Select(value => value.DeterministicSeed)
                .Distinct()
                .Count() == NaturalClearanceRequiredSeedCount
            && rows.All(value => value.IsExact);
        Check(complete,
            "OUTPUT_CLEARANCE_32_SEED_COMPLETE",
            $"rows={rows.Count}/{NaturalClearanceRequiredSeedCount}; distinct="
            + rows.Select(value => value.DeterministicSeed).Distinct().Count());
        if (!complete)
            yield break;

        WriteNaturalClearanceSeedArtifact(rows);
        DisableNaturalClearanceCheckpointTime();
    }

    private bool RestorePreparedOutputCanaryBaseline(
        IDungeonSaveSectionRegistry saveRegistry,
        List<DungeonSaveSectionEnvelope> sceneBaseline)
    {
        if (saveRegistry == null || sceneBaseline == null)
            return true;
        DungeonGameRestoreReport cleanup = new();
        bool restored = saveRegistry.RestoreAll(sceneBaseline, cleanup)
            && cleanup.Success;
        if (restored)
            QuiesceHaulingBeforeDirectStateFixture();
        Check(restored,
            "PREPARED_OUTPUT_CANARY_SCENE_BASELINE_RESTORED",
            restored
                ? $"sections={sceneBaseline.Count}"
                : string.Join(" | ", cleanup.Errors));
        return restored;
    }

    private IEnumerator VerifyPreparedOutputWarehouseLiveRouteCore(
        DungeonRuntimeLifetimeScope scope,
        IWorldItemStackRuntime runtime,
        Grid grid,
        CharacterActor hauler,
        IWarehouseMassAdmissionService warehouseMassAdmission,
        PreparedOutputLiveRouteCase verificationCase,
        IDungeonSaveSectionRegistry saveRegistry)
    {
        string recipeId = verificationCase.RecipeId;
        IResourceEconomyContentCatalog content =
            Resolve<IResourceEconomyContentCatalog>(scope);
        IProductionBillOrderCommand orders =
            Resolve<IProductionBillOrderCommand>(scope);
        IProductionBillQuery bills = Resolve<IProductionBillQuery>(scope);
        IProductionBillWorkExecution work =
            Resolve<IProductionBillWorkExecution>(scope);
        IProductionAssemblyBridge productionBridge =
            Resolve<IProductionAssemblyBridge>(scope);
        IProductionWorkshopRuntime workshops =
            verificationCase.VerifiesClearanceMeasurement
                ? Resolve<IProductionWorkshopRuntime>(scope)
                : null;
        IProductionDistributionQuery distributionQuery =
            Resolve<IProductionDistributionQuery>(scope);
        IProductionPreparedOutputRoutingAuthority routing =
            Resolve<IProductionPreparedOutputRoutingAuthority>(scope);
        IFacilityOutputExactRouteOutboxQuery exactRoutes =
            Resolve<IFacilityOutputExactRouteOutboxQuery>(scope);
        IProductionPreparedOutputDeliveryCoordinator delivery =
            Resolve<IProductionPreparedOutputDeliveryCoordinator>(scope);
        IFacilityBufferMassCapacityQuery outputCapacities =
            Resolve<IFacilityBufferMassCapacityQuery>(scope);
        IFacilityBufferPhysicalOccupancyQuery outputOccupancy =
            Resolve<IFacilityBufferPhysicalOccupancyQuery>(scope);
        IPhysicalItemBatchDispositionService dispositions =
            Resolve<IPhysicalItemBatchDispositionService>(scope);
        IProductionBillPersistence billPersistence =
            Resolve<IProductionBillPersistence>(scope);
        IWorldItemHaulPlanningService planning =
            Resolve<IWorldItemHaulPlanningService>(scope);
        IItemQuantityReservationService reservations =
            Resolve<IItemQuantityReservationService>(scope);
        ProgressionSceneRuntimeReferences progression =
            Resolve<ProgressionSceneRuntimeReferences>(scope);
        IGameClock gameClock = Resolve<IGameClock>(scope);
        string clearanceTopologyDigest = string.Empty;
        int clearanceTopologyVersion = -1;
        ProductionDistributionRuntime distribution =
            distributionQuery as ProductionDistributionRuntime;
        ProductionRecipeSO recipe = null;
        bool recipeReady = content != null
            && content.TryGetRecipe(recipeId, out recipe)
            && recipe != null;
        ProductionOutputDefinition expectedOutput = recipeReady
            ? recipe.CaptureCanonicalOutputs().SingleOrDefault(output =>
                ProductionOutputRoleRules.IsPhysical(output.Role)
                && string.Equals(
                    output.ItemId,
                    verificationCase.ItemId,
                    StringComparison.Ordinal))
            : null;
        long expectedBatchMassGrams = expectedOutput != null
            ? checked(runtime.MassQuery
                .GetDefinitionUnitMass((ItemDefinitionId)expectedOutput.ItemId)
                .Value * expectedOutput.Amount)
            : 0L;
        int expectedOutputBufferCycleCapacity =
            verificationCase.ExpectedOutputBufferCycleCapacity;
        const long expectedSyntheticBatchMassGrams = 20_000L;
        bool expectedBatchMassReady = verificationCase.IsSynthetic
            ? expectedBatchMassGrams == expectedSyntheticBatchMassGrams
            : expectedBatchMassGrams > 0L;
        Check(recipeReady
                && expectedOutput != null
                && expectedOutput.Probability == 1f
                && expectedBatchMassReady
                && expectedOutputBufferCycleCapacity is >= 2 and <= 4
                && !string.IsNullOrWhiteSpace(verificationCase.FacilityAssetPath)
                && !string.IsNullOrWhiteSpace(verificationCase.FacilityObjectName)
                && orders != null
                && bills != null
                && work != null
                && productionBridge != null
                && (!verificationCase.VerifiesClearanceMeasurement
                    || workshops != null)
                && distribution != null
                && routing != null
                && exactRoutes != null
                && delivery != null
                && outputCapacities != null
                && (!verificationCase.IsSynthetic
                    || outputOccupancy != null && dispositions != null)
                && (!verificationCase.RunsTransportFaultMatrix
                    || reservations != null && warehouseMassAdmission != null)
                && (!verificationCase.VerifiesPostDeliverySaveRoundTrip
                    || billPersistence != null && saveRegistry != null)
                && planning != null
                && gameClock != null
                && progression?.BlueprintResearch != null,
            "PREPARED_OUTPUT_LIVE_RUNTIME_READY",
            $"recipe={recipeReady}; output={expectedOutput?.OutputLineId ?? "missing"}:"
            + $"{expectedOutput?.ItemId ?? "missing"}x{expectedOutput?.Amount ?? 0}:"
            + $"{expectedBatchMassGrams}g; orders={orders != null}; bills={bills != null}; "
            + $"work={work != null}; distribution={distribution != null}; "
            + $"bridge={productionBridge != null}; "
            + $"workshops={workshops != null}; "
            + $"routing={routing != null}; routes={exactRoutes != null}; "
            + $"delivery={delivery != null}; planning={planning != null}; "
            + $"capacity={outputCapacities != null}; "
            + $"occupancy={outputOccupancy != null}; disposition={dispositions != null}; "
            + $"persistence={billPersistence != null}; "
            + $"reservations={reservations != null}; warehouseAdmission="
            + $"{warehouseMassAdmission != null}; "
            + $"clock={gameClock != null}; research={progression?.BlueprintResearch != null}");
        if (!recipeReady
            || expectedOutput == null
            || expectedOutput.Probability != 1f
            || !expectedBatchMassReady
            || expectedOutputBufferCycleCapacity is < 2 or > 4
            || string.IsNullOrWhiteSpace(verificationCase.FacilityAssetPath)
            || string.IsNullOrWhiteSpace(verificationCase.FacilityObjectName)
            || orders == null
            || bills == null
            || work == null
            || productionBridge == null
            || verificationCase.VerifiesClearanceMeasurement
                && workshops == null
            || distribution == null
            || routing == null
            || exactRoutes == null
            || delivery == null
            || outputCapacities == null
            || verificationCase.IsSynthetic
                && (outputOccupancy == null || dispositions == null)
            || verificationCase.RunsTransportFaultMatrix
                && (reservations == null || warehouseMassAdmission == null)
            || verificationCase.VerifiesPostDeliverySaveRoundTrip
                && (billPersistence == null || saveRegistry == null)
            || planning == null
            || gameClock == null
            || progression?.BlueprintResearch == null)
        {
            yield break;
        }

        if (!string.IsNullOrEmpty(recipe.RequiredResearchId))
        {
            progression.BlueprintResearch.State.Projects.RestoreCompleted(
                new ResearchProjectId(recipe.RequiredResearchId));
        }
        IReadOnlyList<Vector2Int> positions = FindReachableCells(
            grid,
            hauler.GetNowXY(),
            64);
        BuildingSO warehouseAsset = FindWarehouseAsset();
        BuildingSO feedbenchAsset = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            verificationCase.FacilityAssetPath);
        BuildingProductionBufferAbility feedbenchBuffer =
            feedbenchAsset?.GetProductionBufferAbility();
        long expectedFacilityCapacityGrams =
            verificationCase.ExpectedFacilityCapacityGrams > 0L
                ? verificationCase.ExpectedFacilityCapacityGrams
                : checked(expectedBatchMassGrams
                    * expectedOutputBufferCycleCapacity);
        bool warehousePositionReady = TryFindRegisterablePosition(
            grid,
            warehouseAsset,
            positions,
            out Vector2Int warehousePosition);
        Facility warehouse = CreateInjectedFacility(
            scope,
            grid,
            warehouseAsset,
            warehousePosition,
            "QA_Prepared_Output_Warehouse",
            registerOnGrid: true);
        RegisterTemporaryWarehouse(scope, warehouse);
        Vector2Int feedbenchPosition = default;
        bool feedbenchPositionReady = warehouse != null
            && TryFindRegisterablePosition(
                grid,
                feedbenchAsset,
                positions,
                out feedbenchPosition);
        Facility feedbench = CreateInjectedFacility(
            scope,
            grid,
            feedbenchAsset,
            feedbenchPosition,
            verificationCase.FacilityObjectName,
            registerOnGrid: true);
        bool feedbenchGridAuthorityExact = feedbench != null
            && grid.FindAllOccupants(value => ReferenceEquals(value, feedbench))
                .Count == 1;
        Check(warehousePositionReady
                && feedbenchPositionReady
                && feedbenchGridAuthorityExact
                && warehouse?.Inventory?.HasMassCapacityAuthority == true
                && feedbench != null
                && feedbenchBuffer?.physicalOutputBufferCycleCapacity
                    == expectedOutputBufferCycleCapacity
                && feedbench.MatchesProductionWorkstation(recipe),
            "PREPARED_OUTPUT_LIVE_FACILITIES_READY",
            $"warehouse={DescribeWarehouse(warehouse)}; feedbench="
            + $"{feedbench?.PersistentInstanceId.Value ?? "missing"}@{feedbenchPosition}; "
            + $"gridAuthority={feedbenchGridAuthorityExact}");
        if (!warehousePositionReady
            || !feedbenchPositionReady
            || !feedbenchGridAuthorityExact
            || warehouse?.Inventory?.HasMassCapacityAuthority != true
            || feedbench == null
            || feedbenchBuffer?.physicalOutputBufferCycleCapacity
                != expectedOutputBufferCycleCapacity
            || !feedbench.MatchesProductionWorkstation(recipe))
        {
            yield break;
        }
        ClearInventory(warehouse.Inventory);

        string feedbenchOutputDestination =
            ProductionBillRuntime.OutputDestinationPrefix
            + feedbench.PersistentInstanceId.Value;
        FacilityBufferMassCapacitySnapshot preBillCapacity = default;
        float preBillCapacityDeadline = Time.realtimeSinceStartup + 3f;
        while (Time.realtimeSinceStartup < preBillCapacityDeadline
            && (!outputCapacities.TryGetCapacity(
                    feedbenchOutputDestination,
                    feedbench.centerPos,
                    out preBillCapacity)
                || preBillCapacity.Profile == null
                || preBillCapacity.Profile.MaxMassGrams
                    != expectedFacilityCapacityGrams))
        {
            yield return null;
        }
        bool noBillCapacityReady = preBillCapacity.Profile != null
            && preBillCapacity.Profile.MaxMassGrams
                == expectedFacilityCapacityGrams
            && preBillCapacity.ReservedMassGrams == 0L;
        Check(noBillCapacityReady,
            "PREPARED_OUTPUT_LIVE_NO_BILL_GRAM_CAPACITY",
            noBillCapacityReady
                ? $"destination={feedbenchOutputDestination}; max="
                    + $"{preBillCapacity.Profile.MaxMassGrams}; reserved="
                    + preBillCapacity.ReservedMassGrams
                : $"destination={feedbenchOutputDestination}; capacity=missing-or-nonexact");
        if (!noBillCapacityReady)
            yield break;

        ProductionBillCommandResult added = orders.AddBill(
            feedbench,
            recipeId,
            ProductionOrderMode.RepeatCount,
            1);
        ProductionBillSnapshot bill = added.Succeeded
            ? bills.GetBills(feedbench).SingleOrDefault(value =>
                value.BillId == added.BillId)
            : null;
        Check(added.Succeeded && bill != null,
            "PREPARED_OUTPUT_LIVE_BILL_CREATED",
            $"result={added.Outcome}; failure={added.Failure}; bill={added.BillId.Value}");
        if (!added.Succeeded || bill == null)
            yield break;

        bool inputsReady = true;
        foreach (ItemAmountDefinition input in bill.Inputs)
        {
            bool spawned = runtime.SpawnItemAt(
                input.ItemId,
                input.Amount,
                feedbench.centerPos,
                WorldItemStackState.FacilityBuffer,
                bill.MaterialDestinationId,
                out int amount);
            inputsReady &= spawned && amount == input.Amount;
        }
        Check(inputsReady,
            "PREPARED_OUTPUT_LIVE_INPUTS_PHYSICAL",
            $"destination={bill.MaterialDestinationId}; inputs="
            + string.Join(",", bill.Inputs.Select(value =>
                $"{value.ItemId}x{value.Amount}")));
        if (!inputsReady)
            yield break;

        if (verificationCase.VerifiesClearanceMeasurement)
        {
            IFacilityOutputClearanceTelemetryControl clearanceTelemetry =
                Resolve<IFacilityOutputClearanceTelemetryControl>(scope);
            bool captureBoundaryReady = clearanceTelemetry != null
                && !clearanceTelemetry.IsCaptureActive
                && feedbench.PersistentInstanceId.IsValid
                && !string.IsNullOrWhiteSpace(recipe.RecipeId)
                && !string.IsNullOrWhiteSpace(recipe.WorkstationTag)
                && bill.Inputs.All(value => runtime.GetAllStacks()
                    .Where(stack => stack != null
                        && stack.State == WorldItemStackState.FacilityBuffer
                        && stack.Position == feedbench.centerPos
                        && string.Equals(
                            stack.DestinationId,
                            bill.MaterialDestinationId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            stack.ItemId,
                            value.ItemId,
                            StringComparison.Ordinal))
                    .Sum(stack => stack.Quantity) >= value.Amount);
            if (captureBoundaryReady)
            {
                clearanceTopologyDigest =
                    CaptureOutputClearanceTopologyDigest(
                        productionBridge,
                        workshops,
                        feedbench);
                if (activeNaturalClearanceSeedRun != null)
                {
                    ProductionFacilityHandle clearanceFacility =
                        productionBridge.CaptureFacility(feedbench);
                    activeNaturalClearanceSeedRun.RuntimeFacilityId =
                        clearanceFacility.InstanceId.Value;
                    activeNaturalClearanceSeedRun.DefinitionId =
                        clearanceFacility.DefinitionId;
                    activeNaturalClearanceSeedRun.WorkstationTag =
                        clearanceFacility.WorkstationTag;
                    activeNaturalClearanceSeedRun.RecipeId = recipe.RecipeId;
                    activeNaturalClearanceSeedRun.OutputLineId =
                        expectedOutput.OutputLineId;
                    activeNaturalClearanceSeedRun.ItemId = expectedOutput.ItemId;
                    activeNaturalClearanceSeedRun.OutputQuantity =
                        expectedOutput.Amount;
                    activeNaturalClearanceSeedRun.BatchMassGrams =
                        expectedBatchMassGrams;
                    activeNaturalClearanceSeedRun.RuntimeTopologyBeforeDigest =
                        clearanceTopologyDigest;
                    activeNaturalClearanceSeedRun.TopologySourceDigest =
                        CaptureOutputClearanceTopologySourceDigest(
                            productionBridge,
                            workshops,
                            feedbench);
                }
                clearanceTopologyVersion = workshops.Version;
                captureBoundaryReady = clearanceTopologyDigest.Length == 64
                    && clearanceTopologyDigest.All(character =>
                        character >= '0' && character <= '9'
                        || character >= 'a' && character <= 'f');
            }
            Check(captureBoundaryReady,
                "OUTPUT_CLEARANCE_CAPTURE_BOUNDARY_FROZEN",
                $"telemetry={clearanceTelemetry != null}; active="
                + $"{clearanceTelemetry?.IsCaptureActive}; facility="
                + $"{feedbench.PersistentInstanceId.Value}; recipe="
                + $"{recipe.RecipeId}; workstation={recipe.WorkstationTag}; "
                + $"capacity={preBillCapacity.Profile.MaxMassGrams}; inputs="
                + string.Join(",", bill.Inputs.Select(value =>
                    $"{value.ItemId}x{value.Amount}"))
                + $"; topology={clearanceTopologyDigest}; workshopVersion="
                + clearanceTopologyVersion);
            if (!captureBoundaryReady)
                yield break;
            clearanceTelemetry.BeginCapture(
                "v27.output-clearance.p17.focused");
        }

        ProductionWorkAvailabilityResult available = work.CheckWorkAvailability(
            feedbench,
            recipe.WorkTypeId);
        ProductionWorkBeginResult begun = available.Available
            ? work.BeginWork(hauler, feedbench, recipe.WorkTypeId)
            : default;
        string warehouseOwnerId = warehouse.PersistentInstanceId.Value;
        ProductionWorkExecutionResult completed = default;
        bool syntheticCapacityWaitPassed = !verificationCase.IsSynthetic;
        if (begun.Succeeded && verificationCase.IsSynthetic)
        {
            completed = VerifySyntheticPreparedOutputCapacityWait(
                scope,
                saveRegistry,
                warehouseOwnerId,
                content,
                runtime,
                bills,
                work,
                billPersistence,
                outputCapacities,
                outputOccupancy,
                dispositions,
                hauler,
                feedbench,
                recipe,
                bill,
                expectedOutput,
                expectedBatchMassGrams,
                expectedFacilityCapacityGrams,
                out syntheticCapacityWaitPassed,
                out CharacterActor restoredWorker,
                out Facility restoredFacility,
                out Facility restoredWarehouse,
                out ProductionBillSnapshot restoredBill);
            if (restoredWorker != null)
                hauler = restoredWorker;
            if (restoredFacility != null)
                feedbench = restoredFacility;
            if (restoredWarehouse != null)
                warehouse = restoredWarehouse;
            if (restoredBill != null)
                bill = restoredBill;
        }
        else if (begun.Succeeded)
        {
            completed = work.ExecuteWork(
                hauler,
                feedbench,
                bill.BillId,
                recipe.RequiredWork + 1f);
        }
        Check(available.Available
                && begun.Succeeded
                && syntheticCapacityWaitPassed
                && completed.Succeeded
                && completed.CycleCompleted,
            "PREPARED_OUTPUT_LIVE_BATCH_COMPLETED",
            $"available={available.Available}:{available.Failure.Code}("
            + string.Join(",", available.Failure.Parameters.ToArray())
            + $"); begun={begun.Succeeded}:{begun.Failure.Code}("
            + string.Join(",", begun.Failure.Parameters.ToArray())
            + "); "
            + $"capacityWait={syntheticCapacityWaitPassed}; "
            + $"completed={completed.Succeeded}/{completed.CycleCompleted}:"
            + $"{completed.Failure.Code}("
            + string.Join(",", completed.Failure.Parameters.ToArray())
            + $"), outcome={completed.Outcome}; parameters="
            + string.Join(",", completed.Parameters ?? Array.Empty<string>()));
        if (!completed.Succeeded || !completed.CycleCompleted)
            yield break;

        bool exactFacilityCapacity = outputCapacities.TryGetCapacity(
                bill.OutputDestinationId,
                feedbench.centerPos,
                out FacilityBufferMassCapacitySnapshot outputCapacity)
            && outputCapacity.Profile != null
            && outputCapacity.Profile.MaxMassGrams
                == preBillCapacity.Profile.MaxMassGrams
            && outputCapacity.Profile.MaxMassGrams
                == expectedFacilityCapacityGrams
            && outputCapacity.ReservedMassGrams == 0L;
        Check(exactFacilityCapacity,
            "PREPARED_OUTPUT_LIVE_CAPACITY_SOURCE_STABLE",
            exactFacilityCapacity
                ? $"destination={bill.OutputDestinationId}; max="
                    + $"{outputCapacity.Profile.MaxMassGrams}; reserved="
                    + outputCapacity.ReservedMassGrams
                : $"destination={bill.OutputDestinationId}; capacity=missing-or-nonexact");
        if (!exactFacilityCapacity)
            yield break;

        FacilityOutputExactRoutePendingSnapshot route = null;
        WorldItemStackSnapshot routedStack = null;
        string targetDestinationId = WarehouseStorageIdentity
            .RequireDestinationId(warehouse);
        DomainFailure lastDistributionFailure = DomainFailure.None;
        float routeDeadline = Time.realtimeSinceStartup + 10f;
        while (Time.realtimeSinceStartup < routeDeadline)
        {
            distribution.Tick();
            ProductionBillSnapshot diagnosticBill = bills.GetBills(feedbench)
                .SingleOrDefault(value => value.BillId == bill.BillId);
            if (diagnosticBill?.BlockedFailure.IsFailure == true)
                lastDistributionFailure = diagnosticBill.BlockedFailure;
            if (verificationCase.UsesPreparedRouteAuthority)
            {
                route = exactRoutes.CapturePendingRoutes()
                    .Where(value => value?.Receipt != null
                        && value.Phase == FacilityOutputExactRoutePhase.Routable
                        && value.Receipt.TotalQuantity == expectedOutput.Amount
                        && value.Receipt.TotalMassGrams == expectedBatchMassGrams
                        && value.Receipt.Slices.Count == 1
                        && string.Equals(
                            value.Receipt.Slices[0].OutputLineId,
                            expectedOutput.OutputLineId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            value.Receipt.Slices[0].ItemId,
                            expectedOutput.ItemId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            value.DeliveryRevision.TargetDestinationId,
                            targetDestinationId,
                            StringComparison.Ordinal))
                    .OrderBy(value => value.Receipt.RouteOperationId,
                        StringComparer.Ordinal)
                    .FirstOrDefault();
                if (route != null)
                {
                    routedStack = runtime.GetAllStacks().FirstOrDefault(stack =>
                        stack != null
                        && string.Equals(
                            stack.StackId,
                            route.Receipt.Slices[0].RoutedStackId,
                            StringComparison.Ordinal)
                        && stack.State == WorldItemStackState.Loose
                        && string.Equals(
                            stack.DestinationId,
                            targetDestinationId,
                            StringComparison.Ordinal)
                        && stack.Components.Any(component =>
                            component != null
                            && string.Equals(
                                component.componentTypeId,
                                PreparedOutputCustodyComponentTypeId,
                                StringComparison.Ordinal)));
                }
            }
            else
            {
                // Exact stateful capabilities own their publication lifecycle
                // but still route the committed physical lot through the common
                // FacilityOutputBuffer distribution gateway. No prepared-route
                // custody row exists for that path; the exact loose lot is the
                // authoritative hand-off to AIHaul.
                routedStack = runtime.GetAllStacks()
                    .Where(stack => stack != null
                        && stack.State == WorldItemStackState.Loose
                        && stack.Quantity == expectedOutput.Amount
                        && stack.Position == feedbench.centerPos
                        && string.Equals(
                            stack.ItemId,
                            expectedOutput.ItemId,
                            StringComparison.Ordinal)
                        && string.IsNullOrEmpty(stack.DestinationId)
                        && !stack.HasDestinationPosition)
                    .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
                    .FirstOrDefault();
            }
            if (routedStack != null)
                break;
            yield return null;
        }
        ProductionPreparedOutputRoutingLineSnapshot[] routingLines = routing
            .CaptureBill(bill.BillId)
            .OrderBy(value => value.LineCommitId, StringComparer.Ordinal)
            .ToArray();
        HashSet<string> liveLineIds = routingLines
            .Select(value => value.LineCommitId)
            .ToHashSet(StringComparer.Ordinal);
        ProductionPreparedOutputRouteRequestSnapshot[] routeOperations = routing
            .CaptureRouteOperations()
            .Where(value => liveLineIds.Contains(value.LineCommitId))
            .OrderBy(value => value.RouteOperationId, StringComparer.Ordinal)
            .ToArray();
        FacilityOutputExactRoutePendingSnapshot[] physicalRoutes = exactRoutes
            .CapturePendingRoutes()
            .OrderBy(value => value.Receipt.RouteOperationId, StringComparer.Ordinal)
            .ToArray();
        WorldItemStackSnapshot[] matchingPhysicalOutputs = runtime.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    expectedOutput.ItemId,
                    StringComparison.Ordinal))
            .OrderBy(stack => stack.StackId, StringComparer.Ordinal)
            .ToArray();
        ProductionBillSnapshot[] liveBills = bills.GetBills(feedbench)
            .OrderBy(value => value.BillId.Value, StringComparer.Ordinal)
            .ToArray();
        int bridgeBuffered = productionBridge.CountBufferedOutput(
            expectedOutput.ItemId,
            bill.OutputDestinationId);
        ProductionOutputCapabilityRoute capabilityRoute =
            ProductionPreparedOutputCapabilitySelection.ClassifyPhysicalCapabilities(
                recipe.CaptureCanonicalOutputs()
                    .Where(value => value != null
                        && ProductionOutputRoleRules.IsPhysical(value.Role)
                        && value.Amount > 0
                        && value.Probability > 0f)
                    .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
                    .Select(value => productionBridge.CaptureOutputCapability(
                        value.OutputLineId,
                        value.ItemId))
                    .ToArray(),
                productionBridge.OutputCapabilityContracts);
        bool bridgeFacilityVisible = productionBridge.Facilities.Any(value =>
            value != null
            && value.InstanceId.Equals((BuildingInstanceId)feedbench.PersistentInstanceId.Value));
        bool bridgeWarehouseCompatible = productionBridge
            .HasCompatibleWarehouse(expectedOutput.ItemId);
        bool expectedItemDefinitionReady = runtime.CatalogProvider.TryGetDefinition(
            expectedOutput.ItemId,
            out DungeonItemDefinition expectedItemDefinition);
        ICharacterAiWorldRegistry liveWorldRegistry =
            Resolve<ICharacterAiWorldRegistry>(scope);
        string warehouseCompatibilityDetails = string.Join("|",
            (liveWorldRegistry?.Warehouses ?? Array.Empty<IWarehouseFacility>())
            .Where(value => value != null)
            .Select(value =>
            {
                WarehouseInventory inventory = value.Inventory;
                return $"{value.PersistentInstanceId.Value}:target="
                    + $"{value.PersistentInstanceId.Equals(warehouse.PersistentInstanceId)}:"
                    + $"inventory={inventory != null}:accepts="
                    + $"{expectedItemDefinitionReady && inventory?.Accepts(expectedItemDefinition.StockCategory) == true}:"
                    + $"canStore={inventory?.CanStoreItem(expectedOutput.ItemId, 1)}:"
                    + $"mass={inventory?.StoredMassGrams}/"
                    + $"{inventory?.ReservedInboundMassGrams}/"
                    + $"{inventory?.RemainingMassGrams}";
            }));
        bool expectedRuntimeComponentExact = routedStack != null
            && (string.IsNullOrEmpty(
                    verificationCase.ExpectedRuntimeComponentTypeId)
                || routedStack.Components.Count(component => component != null
                    && string.Equals(
                        component.componentTypeId,
                        verificationCase.ExpectedRuntimeComponentTypeId,
                        StringComparison.Ordinal)) == 1);
        bool routeAuthorityExact = verificationCase.UsesPreparedRouteAuthority
            ? route != null
            : route == null;
        string routeOperationId = route?.Receipt?.RouteOperationId
            ?? (routedStack == null ? "missing" : "exact-capability:" + routedStack.StackId);
        Check(routeAuthorityExact && routedStack != null && expectedRuntimeComponentExact,
            "PREPARED_OUTPUT_LIVE_EXACT_WAREHOUSE_TARGET",
            $"route={routeOperationId}; stack="
            + $"{routedStack?.StackId ?? "missing"}; warehouse="
            + targetDestinationId
            + $"; clock={gameClock.IsPaused}/{gameClock.DeltaTime:0.###}; "
            + $"lines={routingLines.Length}; operations="
            + string.Join(",", routeOperations.Select(value =>
                $"{value.RouteOperationId}:{value.Phase}:r{value.CurrentDeliveryRevision}:"
                + $"{value.CurrentTargetDestinationId}"))
            + "; physical="
            + string.Join(",", physicalRoutes.Select(value =>
                $"{value.Receipt.RouteOperationId}:{value.Phase}:"
                + $"{value.DeliveryRevision.TargetDestinationId}"))
            + "; outputs="
            + string.Join(",", matchingPhysicalOutputs.Select(value =>
                $"{value.StackId}:{value.State}:q{value.Quantity}:"
                + $"p{value.Position}:d={value.DestinationId}:"
                + $"dp={value.HasDestinationPosition}/{value.DestinationPosition}:"
                + $"i={value.ItemInstanceId}:c="
                + string.Join("+", value.Components.Select(component =>
                    component?.componentTypeId ?? "null"))))
            + "; bills="
            + string.Join(",", liveBills.Select(value =>
                $"{value.BillId.Value}:remaining={value.RemainingCycles}:"
                + $"output={value.OutputDestinationId}:blocked="
                + $"{value.BlockedFailure.Code}("
                + string.Join(",", value.BlockedFailure.Parameters.ToArray())
                + ")"))
            + $"; bridgeBuffered={bridgeBuffered}; capabilityRoute={capabilityRoute}"
            + $"; bridgeFacilityVisible={bridgeFacilityVisible}; "
            + $"bridgeWarehouseCompatible={bridgeWarehouseCompatible}"
            + $"; warehouseCompatibility={warehouseCompatibilityDetails}"
            + $"; lastDistributionFailure={lastDistributionFailure.Code}("
            + string.Join(",", lastDistributionFailure.Parameters.ToArray())
            + ")"
            + $"; case={verificationCase.CaseId}; component="
            + $"{verificationCase.ExpectedRuntimeComponentTypeId}:"
            + expectedRuntimeComponentExact);
        if (!routeAuthorityExact || routedStack == null || !expectedRuntimeComponentExact)
            yield break;

        bool previewed = planning.TryPreviewBestPlan(
            hauler,
            out WorldItemHaulPlan previewPlan,
            out string previewFailure);
        string candidateFailure = string.Empty;
        if (!previewed
            && planning is WorldItemHaulPlanningService concretePlanning
            && routedStack != null
            && !concretePlanning.TryExplainCandidateForEditorTest(
                hauler,
                routedStack.StackId,
                out candidateFailure))
        {
            previewFailure += $"; candidate={candidateFailure}";
        }
        WorldItemReservedStackQuantity[] expectedReservations = previewed
            ? previewPlan.ReservedStackQuantities
                .Where(value => string.Equals(
                        value.StackId,
                        routedStack.StackId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        value.ItemId,
                        expectedOutput.ItemId,
                        StringComparison.Ordinal)
                    && value.Quantity == expectedOutput.Amount
                    && value.DestinationKind
                        == WorldItemHaulDestinationKind.Warehouse
                    && string.Equals(
                        value.DestinationId,
                        targetDestinationId,
                        StringComparison.Ordinal))
                .ToArray()
            : Array.Empty<WorldItemReservedStackQuantity>();
        bool exactPreview = previewed
            && previewPlan != null
            && previewPlan.IsValid
            && previewPlan.PrimaryDestination
                == WorldItemHaulDestinationKind.Warehouse
            && string.Equals(
                previewPlan.PrimaryDestinationId,
                targetDestinationId,
                StringComparison.Ordinal)
            && expectedReservations.Length == 1;
        Check(exactPreview,
            verificationCase.IsSynthetic
                ? "PREPARED_OUTPUT_CANARY_AI_PLANNER_EXACT_ROUTE"
                : "PREPARED_OUTPUT_LIVE_AI_PLANNER_EXACT_ROUTE",
            exactPreview ? DescribePreviewPlan(previewPlan) : previewFailure);
        if (!exactPreview)
            yield break;

        if (verificationCase.VerifiesClearanceMeasurement)
        {
            IEnumerator naturalClearance =
                VerifySchedulerOwnedPreparedOutputClearance(
                    runtime,
                    hauler,
                    warehouse,
                    new[]
                    {
                        new NaturalClearanceExpectedSlice(
                            routedStack.StackId,
                            expectedOutput.ItemId,
                            expectedOutput.Amount,
                            expectedBatchMassGrams)
                    },
                    expectedBatchMassGrams,
                    targetDestinationId);
            try
            {
                while (naturalClearance.MoveNext())
                    yield return naturalClearance.Current;
            }
            finally
            {
                (naturalClearance as IDisposable)?.Dispose();
            }
            string afterTopologyDigest =
                CaptureOutputClearanceTopologyDigest(
                    productionBridge,
                    workshops,
                    feedbench);
            if (activeNaturalClearanceSeedRun != null)
            {
                activeNaturalClearanceSeedRun.RuntimeTopologyAfterDigest =
                    afterTopologyDigest;
            }
            Check(string.Equals(
                    clearanceTopologyDigest,
                    afterTopologyDigest,
                    StringComparison.Ordinal),
                "OUTPUT_CLEARANCE_TOPOLOGY_STABLE",
                $"before={clearanceTopologyDigest}; after="
                + $"{afterTopologyDigest}; workshopVersion="
                + $"{clearanceTopologyVersion}->{workshops.Version}");
            yield break;
        }

        if (verificationCase.RunsTransportFaultMatrix
            && !VerifySyntheticPreparedOutputPrePickupCancel(
                runtime,
                exactRoutes,
                hauler,
                warehouse,
                routedStack,
                route,
                expectedOutput.Amount,
                expectedBatchMassGrams,
                reservations,
                warehouseMassAdmission))
        {
            yield break;
        }

        PreparedOutputDestinationRevisionDriftResult revisionDrift = new();
        if (verificationCase.RunsTransportFaultMatrix
            && !VerifyPreparedOutputDestinationRevisionDrift(
                scope,
                runtime,
                hauler,
                warehouse,
                routedStack,
                route,
                expectedOutput.Amount,
                expectedBatchMassGrams,
                reservations,
                warehouseMassAdmission,
                revisionDrift))
        {
            yield break;
        }

        AIHaul action = ScriptableObject.CreateInstance<AIHaul>();
        HaulDeliveryIntentSaveData committedIntent = null;
        string currentTargetDestinationId = targetDestinationId;
        try
        {
            bool canStart = false;
            string postCancelPreviewFailure = string.Empty;
            int postCancelReplanFrames = 0;
            float postCancelReplanDeadline = Time.realtimeSinceStartup + 3f;
            while (Time.realtimeSinceStartup < postCancelReplanDeadline)
            {
                canStart = action.CanStart(hauler);
                if (canStart)
                    break;
                planning.TryPreviewBestPlan(
                    hauler,
                    out _,
                    out postCancelPreviewFailure);
                postCancelReplanFrames++;
                yield return null;
            }
            Check(canStart,
                "PREPARED_OUTPUT_LIVE_AIHAUL_CAN_START",
                $"replanFrames={postCancelReplanFrames}; preview="
                    + $"{postCancelPreviewFailure}; "
                    + DescribeHaulState(runtime, hauler));
            if (!canStart)
                yield break;
            action.Execute(hauler);
            float deadline = Time.realtimeSinceStartup + HaulTimeoutSeconds;
            int expectedQuantity = expectedOutput.Amount;
            while (Time.realtimeSinceStartup < deadline)
            {
                EnsureVerificationTimeScale();
                HaulDeliveryIntentSaveData[] committedCandidates = runtime
                    .CaptureHaulDeliveryIntentsByDestination(
                        currentTargetDestinationId)
                    .Where(intent => intent?.HasCommittedPickup == true
                        && string.Equals(
                            intent.ownerCharacterId,
                            hauler.BuildingCharacterId.Value,
                            StringComparison.Ordinal)
                        && intent.commitments != null
                        && intent.commitments
                            .Where(value => value != null
                                && string.Equals(
                                    value.itemId,
                                    expectedOutput.ItemId,
                                    StringComparison.Ordinal))
                            .Sum(value => value.quantity) == expectedQuantity)
                    .ToArray();
                committedIntent = committedCandidates.Length == 1
                    ? committedCandidates[0]
                    : null;
                if (verificationCase.RunsTransportFaultMatrix
                    && committedIntent != null)
                    break;
                if (GetStoredItemQuantity(
                        runtime,
                        expectedOutput.ItemId,
                        warehouse.centerPos) >= expectedQuantity)
                {
                    break;
                }
                yield return null;
            }

            if (verificationCase.RunsTransportFaultMatrix)
            {
                AbilityHaul activeHaul = AbilityHaul.Ensure(hauler);
                AbilityMove activeMove = hauler?.GetComponent<AbilityMove>();
                WorldItemReservedStackQuantity? committedReservation = activeHaul?
                    .ActiveReservationsForDiagnostics?
                    .SingleOrDefault();
                HaulDeliveryItemCommitmentSaveData committedCargo =
                    committedIntent?.commitments?.SingleOrDefault();
                WarehouseHaulAdmissionSaveData committedAdmission =
                    committedIntent?.warehouseAdmissions?.SingleOrDefault();
                long reservedMassBeforeCancel = warehouse.Inventory
                    .ReservedInboundMassGrams;
                WarehouseMassAdmissionStatusSnapshot admissionBeforeCancel = default;
                bool authorityReadyBeforeCancel = committedReservation.HasValue
                    && committedCargo != null
                    && committedAdmission != null
                    && reservations.TryGetLeasesByOwner(
                        committedIntent.operationId,
                        out IReadOnlyList<ItemQuantityLease> leasesBeforeCancel)
                    && leasesBeforeCancel.Count == 1
                    && string.Equals(
                        leasesBeforeCancel[0].leaseId,
                        committedReservation.Value.LeaseId,
                        StringComparison.Ordinal)
                    && leasesBeforeCancel[0].remainingQuantity
                        == committedCargo.quantity
                    && leasesBeforeCancel[0].slices?.Count == 1
                    && string.Equals(
                        leasesBeforeCancel[0].slices[0].stackId,
                        committedCargo.carriedStackId,
                        StringComparison.Ordinal)
                    && leasesBeforeCancel[0].slices[0].quantity
                        == committedCargo.quantity
                    && warehouseMassAdmission.TryGetStatus(
                        committedAdmission.tokenId,
                        out admissionBeforeCancel)
                    && admissionBeforeCancel.Status
                        == WarehouseMassAdmissionTokenStatus.Reserved
                    && admissionBeforeCancel.Token.ReservedMassGrams
                        == expectedBatchMassGrams
                    && reservedMassBeforeCancel == expectedBatchMassGrams;
                bool revisionDriftReplanned = !revisionDrift.Succeeded
                    || authorityReadyBeforeCancel
                    && !string.Equals(
                        committedIntent.operationId,
                        revisionDrift.StaleOperationId,
                        StringComparison.Ordinal)
                    && !string.Equals(
                        committedAdmission.tokenId,
                        revisionDrift.StaleAdmissionTokenId,
                        StringComparison.Ordinal)
                    && admissionBeforeCancel.Token.WarehouseCapacityRevision
                        > revisionDrift.StaleAdmissionRevision
                    && admissionBeforeCancel.Token.WarehouseCapacityRevision
                        >= revisionDrift.LiveRevisionAfterDrift
                    && admissionBeforeCancel.Token.WarehouseCapacityRevision
                        == warehouseMassAdmission.GetWarehouseCapacityRevision(
                            warehouse.PersistentInstanceId);
                Check(revisionDriftReplanned,
                    "PREPARED_OUTPUT_CANARY_DESTINATION_REVISION_REPLAN_FRESH_AUTHORITY",
                    $"oldOperation={revisionDrift.StaleOperationId}; newOperation="
                        + $"{committedIntent?.operationId ?? "missing"}; oldToken="
                        + $"{revisionDrift.StaleAdmissionTokenId}:"
                        + $"{revisionDrift.StaleAdmissionRevision}; newToken="
                        + $"{committedAdmission?.tokenId ?? "missing"}:"
                        + $"{admissionBeforeCancel.Token.WarehouseCapacityRevision}; "
                        + $"liveRevision={warehouseMassAdmission.GetWarehouseCapacityRevision(warehouse.PersistentInstanceId)}; "
                        + $"carried={committedCargo?.quantity ?? 0}/{expectedOutput.Amount}");
                if (!revisionDriftReplanned)
                    yield break;
                action.OnStop(
                    hauler,
                    null,
                    "qa-prepared-output-active-actor-cancel");
                HaulDeliveryIntentSaveData retainedIntent = activeHaul?
                    .CaptureDeliveryIntentForSave();
                int retainedQuantity = hauler?.CarryInventory?.Items?
                    .Where(value => value != null
                        && string.Equals(
                            value.itemId,
                            expectedOutput.ItemId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            value.ownerOperationId,
                            committedIntent?.operationId,
                            StringComparison.Ordinal))
                    .Sum(value => value.quantity) ?? 0;
                bool retainedLeaseExact = committedReservation.HasValue
                    && committedCargo != null
                    && reservations.TryGetLeasesByOwner(
                        committedIntent.operationId,
                        out IReadOnlyList<ItemQuantityLease> retainedLeases)
                    && retainedLeases.Count == 1
                    && string.Equals(
                        retainedLeases[0].leaseId,
                        committedReservation.Value.LeaseId,
                        StringComparison.Ordinal)
                    && retainedLeases[0].remainingQuantity
                        == committedCargo.quantity
                    && retainedLeases[0].slices?.Count == 1
                    && string.Equals(
                        retainedLeases[0].slices[0].stackId,
                        committedCargo.carriedStackId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        retainedLeases[0].slices[0].expectedStackSignature,
                        committedCargo.expectedStackSignature,
                        StringComparison.Ordinal)
                    && retainedLeases[0].slices[0].quantity
                        == committedCargo.quantity
                    && reservations.Revalidate(
                        committedReservation.Value.LeaseId,
                        out ItemQuantityLease revalidatedLease,
                        out _)
                    && string.Equals(revalidatedLease.leaseId,
                        committedReservation.Value.LeaseId, StringComparison.Ordinal);
                bool retainedAdmissionExact = committedAdmission != null
                    && warehouseMassAdmission.TryGetStatus(
                        committedAdmission.tokenId,
                        out WarehouseMassAdmissionStatusSnapshot
                            retainedAdmission)
                    && retainedAdmission.Status
                        == WarehouseMassAdmissionTokenStatus.Reserved
                    && string.Equals(retainedAdmission.Token.TokenId,
                        committedAdmission.tokenId, StringComparison.Ordinal)
                    && string.Equals(retainedAdmission.Token.OwnerOperationId,
                        committedAdmission.ownerAdmissionOperationId,
                        StringComparison.Ordinal)
                    && retainedAdmission.Token.AcceptedQuantity
                        == expectedOutput.Amount
                    && retainedAdmission.Token.ReservedMassGrams
                        == expectedBatchMassGrams
                    && warehouse.Inventory.ReservedInboundMassGrams
                        == reservedMassBeforeCancel;
                bool activeCancelRetained = authorityReadyBeforeCancel
                    && activeHaul != null
                    && !activeHaul.IsHauling
                    && !activeHaul.HasHaulingRoutineForDiagnostics
                    && activeMove?.HasActiveMovementRoutineForDiagnostics != true
                    && activeHaul.HasBoundDeliveryIntent
                    && activeHaul.LastInterruptionDisposition
                        == HaulInterruptionDisposition
                            .ReleaseUnpickedAndRetainCarriedForReplan
                    && retainedIntent?.HasCommittedPickup == true
                    && string.Equals(
                        retainedIntent.operationId,
                        committedIntent?.operationId,
                        StringComparison.Ordinal)
                    && retainedQuantity == expectedOutput.Amount
                    && retainedIntent.warehouseAdmissions?.Count == 1
                    && retainedIntent.warehouseAdmissions[0].reservedMassGrams
                        == expectedBatchMassGrams
                    && warehouse.Inventory.ReservedInboundMassGrams
                        == expectedBatchMassGrams
                    && activeHaul.CaptureActiveHaulOperationIds().Count == 1
                    && string.Equals(
                        activeHaul.CaptureActiveHaulOperationIds()[0],
                        committedIntent.operationId,
                        StringComparison.Ordinal)
                    && activeHaul.ActiveReservationsForDiagnostics.Count == 1
                    && string.Equals(
                        activeHaul.ActiveReservationsForDiagnostics[0].LeaseId,
                        committedReservation.Value.LeaseId,
                        StringComparison.Ordinal)
                    && retainedLeaseExact
                    && retainedAdmissionExact;
                Check(activeCancelRetained,
                    "PREPARED_OUTPUT_CANARY_ACTIVE_CANCEL_RETAINS_CARRIED_AUTHORITY",
                    $"hauling={activeHaul?.IsHauling}; bound="
                        + $"{activeHaul?.HasBoundDeliveryIntent}; disposition="
                        + $"{activeHaul?.LastInterruptionDisposition}; operation="
                        + $"{retainedIntent?.operationId ?? "missing"}; quantity="
                    + $"{retainedQuantity}/{expectedOutput.Amount}; reserved="
                    + $"{warehouse.Inventory.ReservedInboundMassGrams}/"
                    + $"{expectedBatchMassGrams}; authorityBefore="
                    + $"{authorityReadyBeforeCancel}; lease={retainedLeaseExact}; "
                    + $"admission={retainedAdmissionExact}; routine="
                    + $"{activeHaul?.HasHaulingRoutineForDiagnostics}; movement="
                    + $"{activeMove?.HasActiveMovementRoutineForDiagnostics}");
                if (!activeCancelRetained)
                    yield break;
                committedIntent = retainedIntent;

                if (verificationCase.VerifiesDestructiveDrain)
                {
                    IEnumerator destructive =
                        VerifyPreparedOutputDestructiveDrainLiveRoute(
                            scope,
                            runtime,
                            grid,
                            feedbench,
                            warehouse,
                            hauler,
                            bill,
                            expectedOutput.ItemId,
                            expectedOutput.Amount,
                            expectedBatchMassGrams,
                            committedIntent,
                            reservations,
                            warehouseMassAdmission);
                    while (destructive.MoveNext())
                        yield return destructive.Current;
                    (destructive as IDisposable)?.Dispose();
                    yield break;
                }

                PreparedOutputDownedRecoveryResult downedRecovery = new();
                IEnumerator downed = VerifyPreparedOutputDownedCurrentCellRecovery(
                    scope,
                    runtime,
                    grid,
                    saveRegistry,
                    exactRoutes,
                    reservations,
                    warehouseMassAdmission,
                    hauler,
                    warehouse,
                    routedStack,
                    route,
                    expectedOutput.ItemId,
                    expectedOutput.Amount,
                    expectedBatchMassGrams,
                    committedIntent,
                    downedRecovery);
                while (downed.MoveNext())
                    yield return downed.Current;
                (downed as IDisposable)?.Dispose();
                if (!downedRecovery.Succeeded)
                    yield break;
                hauler = downedRecovery.Hauler;
                warehouse = downedRecovery.Warehouse;
                committedIntent = downedRecovery.Intent;

                SyntheticPreparedOutputMidCarryRestoreResult midCarry = new();
                IEnumerator restore = VerifySyntheticPreparedOutputMidCarryRestore(
                    scope,
                    runtime,
                    saveRegistry,
                    hauler,
                    warehouseOwnerId,
                    expectedOutput.ItemId,
                    expectedOutput.Amount,
                    expectedBatchMassGrams,
                    committedIntent,
                    midCarry);
                while (restore.MoveNext())
                    yield return restore.Current;
                (restore as IDisposable)?.Dispose();
                if (!midCarry.Succeeded)
                    yield break;

                Destroy(action);
                action = null;
                hauler = midCarry.Hauler;
                warehouse = midCarry.Warehouse;
                committedIntent = midCarry.Intent;
                AIBrain restoredBrain = hauler?.Brain;
                AbilityHaul restoredHaul = AbilityHaul.Ensure(hauler);
                AbilityMove restoredMove = hauler?.GetComponent<AbilityMove>();
                AIAction[] restoredAvailableActions = restoredBrain?.availableActions;
                AIAction restoredHaulAction = restoredAvailableActions?
                    .FirstOrDefault(value => value?.actionset is AIHaul);
                bool inertBeforeWake = restoredBrain != null
                    && restoredHaul != null
                    && restoredHaulAction != null
                    && !restoredBrain.HasRunningAction
                    && !restoredHaul.IsHauling
                    && restoredHaul.RoutineHeartbeat == 0
                    && string.IsNullOrWhiteSpace(restoredHaul.ActivePathDebug)
                    && restoredMove != null
                    && !restoredMove.IsSystemMoveInProgress
                    && !restoredMove.HasActiveMovementRoutineForDiagnostics;
                Check(inertBeforeWake,
                    "PREPARED_OUTPUT_CANARY_RESTORED_AIHAUL_INERT_BEFORE_BRAIN_WAKE",
                    $"actor={hauler?.BuildingCharacterId.Value ?? "missing"}; operation="
                        + $"{committedIntent?.operationId ?? "missing"}; action="
                        + $"{restoredHaulAction?.actionset?.GetType().Name ?? "missing"}; "
                        + $"brainRunning={restoredBrain?.HasRunningAction}; "
                        + $"hauling={restoredHaul?.IsHauling}; heartbeat="
                        + $"{restoredHaul?.RoutineHeartbeat ?? -1}; moveActive="
                        + $"{restoredMove?.HasActiveMovementRoutineForDiagnostics}");
                if (!inertBeforeWake)
                    yield break;

                restoredBrain.availableActions = new[] { restoredHaulAction };
                long actionStartsBeforeWake = restoredBrain.RuntimeActionStartCount;
                bool preferred = restoredBrain.PreferActionOnNextDecision<AIHaul>(180f);
                Check(preferred,
                    "PREPARED_OUTPUT_CANARY_RESTORED_AIHAUL_BRAIN_WAKE_READY",
                    $"actor={hauler.BuildingCharacterId.Value}; operation="
                        + $"{committedIntent.operationId}; preferred={preferred}; "
                        + $"actionStarts={actionStartsBeforeWake}");
                if (!preferred)
                {
                    restoredBrain.availableActions = restoredAvailableActions;
                    yield break;
                }

                bool sawBrainHaul = false;
                try
                {
                    hauler.SetAiPaused(false);
                    restoredBrain.RequestImmediateDecision(
                        "Prepared-output canary wakes restored committed haul.");
                    Time.timeScale = 8f;

                    float resumedDeadline = Time.realtimeSinceStartup + HaulTimeoutSeconds;
                    while (Time.realtimeSinceStartup < resumedDeadline
                        && GetStoredItemQuantity(
                            runtime,
                            expectedOutput.ItemId,
                            warehouse.centerPos) < expectedQuantity)
                    {
                        sawBrainHaul |= restoredBrain.bestAction?.actionset is AIHaul;
                        EnsureVerificationTimeScale();
                        yield return null;
                    }
                    sawBrainHaul |= restoredBrain.bestAction?.actionset is AIHaul;
                }
                finally
                {
                    restoredBrain.availableActions = restoredAvailableActions;
                }

                bool resumedByBrain = sawBrainHaul
                    || restoredBrain.RuntimeActionStartCount > actionStartsBeforeWake;
                Check(resumedByBrain,
                    "PREPARED_OUTPUT_CANARY_RESTORED_AIHAUL_RESUMED_BY_BRAIN",
                    $"actor={hauler.BuildingCharacterId.Value}; operation="
                        + $"{committedIntent.operationId}; observed={sawBrainHaul}; "
                        + $"actionStarts={actionStartsBeforeWake}->"
                        + restoredBrain.RuntimeActionStartCount);
                if (!resumedByBrain)
                    yield break;
            }

            int stored = GetStoredItemQuantity(
                runtime,
                expectedOutput.ItemId,
                warehouse.centerPos);
            WarehouseHaulAdmissionSaveData admission = committedIntent?
                .warehouseAdmissions?
                .SingleOrDefault();
            HaulDeliveryIntentSaveData[] activeDeliveryIntents = runtime
                .CaptureHaulDeliveryIntentsByDestination(
                    currentTargetDestinationId)
                .Where(value => value != null)
                .ToArray();
            bool capturedAdmissionEvidenceExact = admission == null
                || admission.quantity == expectedQuantity
                    && admission.reservedMassGrams == expectedBatchMassGrams;
            Check(stored == expectedQuantity
                    && capturedAdmissionEvidenceExact
                    && activeDeliveryIntents.Length == 0
                    && warehouse.Inventory.ReservedInboundMassGrams == 0L,
                "PREPARED_OUTPUT_LIVE_STORED_WITH_RETIRED_ADMISSION",
                $"stored={stored}/{expectedQuantity}; intent="
                + $"{committedIntent?.operationId ?? "missing"}; admission="
                + $"{admission?.tokenId ?? "missing"}:"
                + $"{admission?.reservedMassGrams ?? 0}; routeMass="
                + $"{expectedBatchMassGrams}; inbound="
                + warehouse.Inventory.ReservedInboundMassGrams
                + $"; activeIntents={activeDeliveryIntents.Length}"
                + "; haulState=" + DescribeHaulState(runtime, hauler));

            if (revisionDrift.Succeeded)
            {
                int outputTotalAfter = runtime.GetAllStacks()
                    .Where(value => value != null
                        && string.Equals(
                            value.ItemId,
                            expectedOutput.ItemId,
                            StringComparison.Ordinal))
                    .Sum(value => value.Quantity);
                int driftTotalAfter = runtime.GetAllStacks()
                    .Where(value => value != null
                        && string.Equals(
                            value.ItemId,
                            revisionDrift.DriftItemId,
                            StringComparison.Ordinal))
                    .Sum(value => value.Quantity);
                bool oldIntentRetired = !runtime.TryCaptureHaulDeliveryIntent(
                    revisionDrift.StaleOperationId,
                    out _);
                bool noLossDelivered = stored == expectedQuantity
                    && outputTotalAfter == revisionDrift.OutputQuantityBefore
                    && driftTotalAfter == checked(
                        revisionDrift.DriftQuantityBefore + 1)
                    && activeDeliveryIntents.Length == 0
                    && oldIntentRetired
                    && hauler?.CarryInventory?.HasItems != true
                    && warehouse.Inventory.ReservedInboundMassGrams == 0L;
                Check(noLossDelivered,
                    "PREPARED_OUTPUT_CANARY_DESTINATION_REVISION_REPLAN_NO_LOSS_DELIVERED",
                    $"stored={stored}/{expectedQuantity}; outputTotal="
                        + $"{outputTotalAfter}/{revisionDrift.OutputQuantityBefore}; "
                        + $"drift={revisionDrift.DriftItemId}:"
                        + $"{driftTotalAfter}/{revisionDrift.DriftQuantityBefore + 1}; "
                        + $"oldIntentRetired={oldIntentRetired}; activeIntents="
                        + $"{activeDeliveryIntents.Length}; carry="
                        + $"{hauler?.CarryInventory?.Items?.Sum(value => value?.quantity ?? 0) ?? 0}; "
                        + $"inbound={warehouse.Inventory.ReservedInboundMassGrams}");
                if (!noLossDelivered)
                    yield break;
            }

            if (verificationCase.VerifiesPostDeliverySaveRoundTrip
                && stored == expectedQuantity
                && capturedAdmissionEvidenceExact
                && activeDeliveryIntents.Length == 0
                && warehouse.Inventory.ReservedInboundMassGrams == 0L)
            {
                bool terminalBillRetired = false;
                int retirementFrames = 0;
                float retirementDeadline = Time.realtimeSinceStartup + 3f;
                while (Time.realtimeSinceStartup < retirementDeadline)
                {
                    terminalBillRetired = billPersistence.Capture().bills
                        .All(value => value == null
                            || !string.Equals(
                                value.billId,
                                bill.BillId.Value,
                                StringComparison.Ordinal));
                    if (terminalBillRetired)
                        break;
                    retirementFrames++;
                    yield return null;
                }
                Check(terminalBillRetired,
                    "PREPARED_OUTPUT_CANARY_TERMINAL_BILL_RETIRED_AFTER_DELIVERY",
                    $"bill={bill.BillId.Value}; retired={terminalBillRetired}; "
                        + $"frames={retirementFrames}; stored={stored}; "
                        + $"route={routeOperationId}");
                if (!terminalBillRetired)
                    yield break;

                yield return VerifySyntheticPreparedOutputSaveRoundTrip(
                    scope,
                    runtime,
                    saveRegistry,
                    warehouse.PersistentInstanceId.Value,
                    expectedOutput.ItemId,
                    expectedOutput.Amount,
                    checked(
                        expectedBatchMassGrams
                        + revisionDrift.DriftAddedMassGrams));
            }
        }
        finally
        {
            Destroy(action);
        }
    }

    private static string CaptureOutputClearanceTopologyDigest(
        IProductionAssemblyBridge productionBridge,
        IProductionWorkshopRuntime workshops,
        BuildableObject facility)
    {
        if (productionBridge == null
            || workshops == null
            || facility == null)
        {
            throw new InvalidOperationException(
                "Output-clearance topology authority is missing.");
        }

        ProductionFacilityHandle handle =
            productionBridge.CaptureFacility(facility);
        if (handle == null
            || handle.IsDestroyed
            || !handle.InstanceId.IsValid
            || string.IsNullOrWhiteSpace(handle.DefinitionId)
            || string.IsNullOrWhiteSpace(handle.WorkstationTag))
        {
            throw new InvalidOperationException(
                "Output-clearance facility handle is incomplete.");
        }

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-topology@1");
        digest.Append(handle.InstanceId.Value);
        digest.Append(handle.DefinitionId);
        digest.Append(handle.WorkstationTag);
        digest.Append(handle.Position.x);
        digest.Append(handle.Position.y);
        digest.Append(handle.OutputBufferCycleCapacity);
        digest.Append(handle.StockSensorInstallationItemId);
        digest.Append(handle.AllowsOverflowDump);
        digest.Append(handle.OverflowOffset.x);
        digest.Append(handle.OverflowOffset.y);
        digest.Append(handle.ProcessFluidProfile.SourceDigest);

        ProductionSupportLinkSnapshot[] links = workshops.GetLinks(facility)
            .Where(value => value?.Support != null)
            .OrderBy(
                value => value.Support.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .ThenBy(value => value.SupportId, StringComparer.Ordinal)
            .ToArray();
        digest.Append(links.Length);
        foreach (ProductionSupportLinkSnapshot link in links)
        {
            string[] featureTags = (link.FeatureTags ?? Array.Empty<string>())
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value.Trim())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            digest.Append(link.Support.PersistentInstanceId.Value);
            digest.Append(link.Support.BuildingData?.id ?? -1);
            digest.Append(link.WorkstationTag ?? string.Empty);
            digest.Append(link.SupportId ?? string.Empty);
            digest.Append(featureTags.Length);
            foreach (string featureTag in featureTags)
                digest.Append(featureTag);
        }
        return digest.ComputeSha256();
    }

    private static string CaptureOutputClearanceTopologySourceDigest(
        IProductionAssemblyBridge productionBridge,
        IProductionWorkshopRuntime workshops,
        BuildableObject facility)
    {
        if (productionBridge == null
            || workshops == null
            || facility == null)
        {
            throw new InvalidOperationException(
                "Output-clearance topology source authority is missing.");
        }

        ProductionFacilityHandle handle =
            productionBridge.CaptureFacility(facility);
        if (handle == null
            || handle.IsDestroyed
            || string.IsNullOrWhiteSpace(handle.DefinitionId)
            || string.IsNullOrWhiteSpace(handle.WorkstationTag)
            || handle.ProcessFluidProfile == null)
        {
            throw new InvalidOperationException(
                "Output-clearance facility source authority is incomplete.");
        }

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-topology-source@1");
        digest.Append(handle.DefinitionId);
        digest.Append(handle.WorkstationTag);
        digest.Append(handle.OutputBufferCycleCapacity);
        digest.Append(handle.StockSensorInstallationItemId);
        digest.Append(handle.AllowsOverflowDump);
        digest.Append(handle.OverflowOffset.x);
        digest.Append(handle.OverflowOffset.y);
        digest.Append(handle.ProcessFluidProfile.SourceDigest);

        ProductionSupportLinkSnapshot[] links = workshops.GetLinks(facility)
            .Where(value => value?.Support?.BuildingData != null)
            .OrderBy(
                value => ProductionFacilityDefinitionIdentity.Resolve(
                    value.Support.BuildingData),
                StringComparer.Ordinal)
            .ThenBy(value => value.SupportId, StringComparer.Ordinal)
            .ThenBy(value => value.WorkstationTag, StringComparer.Ordinal)
            .ThenBy(
                value => value.Support.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .ToArray();
        digest.Append(links.Length);
        foreach (ProductionSupportLinkSnapshot link in links)
        {
            BuildingProductionSupportAbility ability =
                link.Support.BuildingData.GetProductionSupportAbility();
            if (ability == null || !ability.IsValid)
            {
                throw new InvalidOperationException(
                    "Output-clearance linked support authoring is incomplete: "
                    + ProductionFacilityDefinitionIdentity.Resolve(
                        link.Support.BuildingData));
            }

            string supportDefinitionId =
                ProductionFacilityDefinitionIdentity.Resolve(
                    link.Support.BuildingData);
            string[] linkedFeatures = CanonicalNaturalClearanceTokens(
                link.FeatureTags);
            string[] authoredFeatures = CanonicalNaturalClearanceTokens(
                ability.featureTags);
            string[] compatibleTags = CanonicalNaturalClearanceTokens(
                ability.compatibleWorkstationTags);

            digest.Append(supportDefinitionId);
            digest.Append(link.WorkstationTag ?? string.Empty);
            digest.Append(link.SupportId ?? string.Empty);
            digest.AppendEnum(ability.kind);
            digest.Append(ability.batchCapacity);
            digest.Append(ability.requiresPower);
            digest.AppendFloat(ability.cleanWaterPerCycle);
            digest.AppendFloat(ability.wastewaterPerCycle);
            digest.AppendEnum(ability.wastewaterComposition);
            digest.Append(ability.allowsManualWaterFallback);
            digest.Append(ability.requiresFuel);
            digest.Append(ability.fuelItemId ?? string.Empty);
            digest.Append(ability.fuelPerCycle);
            digest.AppendFloat(ability.workSpeedMultiplier);
            digest.AppendFloat(ability.outputMultiplier);
            digest.AppendFloat(ability.qualityModifier);
            AppendNaturalClearanceTokens(digest, linkedFeatures);
            AppendNaturalClearanceTokens(digest, authoredFeatures);
            AppendNaturalClearanceTokens(digest, compatibleTags);
        }

        return digest.ComputeSha256();
    }

    private static string[] CanonicalNaturalClearanceTokens(
        IEnumerable<string> values)
    {
        return (values ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AppendNaturalClearanceTokens(
        CanonicalSemanticDigestBuilder digest,
        IReadOnlyList<string> values)
    {
        digest.Append(values?.Count ?? 0);
        if (values == null)
            return;
        for (int index = 0; index < values.Count; index++)
            digest.Append(values[index]);
    }

    private static string ResolveNaturalClearanceRosterKey(string characterId)
    {
        if (string.Equals(characterId, "owner", StringComparison.Ordinal))
            return "owner";
        if (characterId?.EndsWith(":01", StringComparison.Ordinal) == true)
            return "staff:01";
        if (characterId?.EndsWith(":02", StringComparison.Ordinal) == true)
            return "staff:02";
        throw new InvalidOperationException(
            "Natural-clearance actor is outside the canonical initial-party roster: "
            + (characterId ?? "<null>"));
    }

    private static string NormalizeNaturalClearanceRandomStreamId(
        string streamId)
    {
        if (string.IsNullOrWhiteSpace(streamId)
            || !string.Equals(streamId, streamId.Trim(), StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Natural-clearance random stream ID is noncanonical.");
        }

        const string DecisionPrefix = "character-ai:";
        const string MovementPrefix = "character-movement:";
        if (streamId.StartsWith(DecisionPrefix, StringComparison.Ordinal))
        {
            return DecisionPrefix + ResolveNaturalClearanceRosterKey(
                streamId.Substring(DecisionPrefix.Length));
        }
        if (streamId.StartsWith(MovementPrefix, StringComparison.Ordinal))
        {
            return MovementPrefix + ResolveNaturalClearanceRosterKey(
                streamId.Substring(MovementPrefix.Length));
        }
        return streamId;
    }

    private static string CaptureNaturalClearanceRandomDigest(
        IReadOnlyList<RandomStreamDiagnosticSnapshot> snapshots)
    {
        RandomStreamDiagnosticSnapshot[] ordered =
            (snapshots ?? Array.Empty<RandomStreamDiagnosticSnapshot>())
            .OrderBy(
                value => NormalizeNaturalClearanceRandomStreamId(value.StreamId),
                StringComparer.Ordinal)
            .ThenBy(value => value.StreamId, StringComparer.Ordinal)
            .ToArray();
        string[] normalizedIds = ordered
            .Select(value => NormalizeNaturalClearanceRandomStreamId(value.StreamId))
            .ToArray();
        if (normalizedIds.Distinct(StringComparer.Ordinal).Count()
            != normalizedIds.Length)
        {
            throw new InvalidOperationException(
                "Natural-clearance random stream normalization produced duplicate IDs.");
        }

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-output-clearance-random-state@1");
        digest.Append(ordered.Length);
        for (int index = 0; index < ordered.Length; index++)
        {
            RandomStreamDiagnosticSnapshot snapshot = ordered[index];
            if (snapshot.DrawCount < 0L)
            {
                throw new InvalidOperationException(
                    "Natural-clearance random draw count is negative.");
            }
            digest.Append(normalizedIds[index]);
            digest.Append(snapshot.State.ToString(
                "x16",
                CultureInfo.InvariantCulture));
            digest.Append(snapshot.DrawCount);
        }
        return digest.ComputeSha256();
    }

    private static long CaptureNaturalClearanceRandomDrawDelta(
        IReadOnlyList<RandomStreamDiagnosticSnapshot> before,
        IReadOnlyList<RandomStreamDiagnosticSnapshot> after)
    {
        Dictionary<string, RandomStreamDiagnosticSnapshot> beforeById =
            (before ?? Array.Empty<RandomStreamDiagnosticSnapshot>())
            .ToDictionary(value => value.StreamId, StringComparer.Ordinal);
        HashSet<string> afterIds = new(StringComparer.Ordinal);
        long total = 0L;
        foreach (RandomStreamDiagnosticSnapshot snapshot in
                 after ?? Array.Empty<RandomStreamDiagnosticSnapshot>())
        {
            if (!afterIds.Add(snapshot.StreamId))
            {
                throw new InvalidOperationException(
                    "Natural-clearance random diagnostics contain duplicate stream IDs.");
            }
            long previous = beforeById.TryGetValue(
                    snapshot.StreamId,
                    out RandomStreamDiagnosticSnapshot baseline)
                ? baseline.DrawCount
                : 0L;
            long delta = checked(snapshot.DrawCount - previous);
            if (delta < 0L)
            {
                throw new InvalidOperationException(
                    "Natural-clearance random draw count regressed for stream: "
                    + snapshot.StreamId);
            }
            total = checked(total + delta);
        }

        string[] missingAfter = beforeById.Keys
            .Where(value => !afterIds.Contains(value))
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (missingAfter.Length != 0)
        {
            throw new InvalidOperationException(
                "Natural-clearance random streams disappeared during the seed run: "
                + string.Join(",", missingAfter));
        }
        return total;
    }

    private void DiscardRestoredPreparedOutputFixtureReferences()
    {
        temporaryWarehouseRegistrations.Clear();
        temporaryObjects.Clear();
        verificationActors.Clear();
        isolatedAiPauseStates.Clear();
        isolatedLogisticsMeasurementStates.Clear();
        warehouseSnapshots.Clear();
    }

    private void WriteNaturalClearanceSeedArtifact(
        IReadOnlyList<NaturalClearanceSeedArtifactRow> sourceRows)
    {
        NaturalClearanceSeedArtifactRow[] rows =
            (sourceRows ?? Array.Empty<NaturalClearanceSeedArtifactRow>())
            .OrderBy(value => value.SeedIndex)
            .ThenBy(value => value.DeterministicSeed)
            .ToArray();
        byte[] first = SerializeNaturalClearanceSeedArtifact(rows);
        byte[] second = SerializeNaturalClearanceSeedArtifact(rows);
        bool byteIdentical = first.AsSpan().SequenceEqual(second);
        Check(byteIdentical,
            "OUTPUT_CLEARANCE_32_SEED_SERIALIZATION_BYTE_IDENTICAL",
            $"rows={rows.Length}; bytes={first.Length}/{second.Length}");
        if (!byteIdentical)
            return;

        bool firstChanged = V27BalanceArtifactWriter.WriteIfDifferent(
            PhysicalItemLogisticsPlayModeVerifier.P17OutputClearance32SeedCsvPath,
            stream => stream.Write(first, 0, first.Length));
        string artifactPath = Path.GetFullPath(
            PhysicalItemLogisticsPlayModeVerifier.P17OutputClearance32SeedCsvPath);
        DateTime firstWriteTime = File.GetLastWriteTimeUtc(artifactPath);
        long firstLength = new FileInfo(artifactPath).Length;
        bool secondChanged = V27BalanceArtifactWriter.WriteIfDifferent(
            PhysicalItemLogisticsPlayModeVerifier.P17OutputClearance32SeedCsvPath,
            stream => stream.Write(second, 0, second.Length));
        DateTime secondWriteTime = File.GetLastWriteTimeUtc(artifactPath);
        long secondLength = new FileInfo(artifactPath).Length;
        string artifactDigest = V27BalanceArtifactWriter.ComputeSha256(
            PhysicalItemLogisticsPlayModeVerifier.P17OutputClearance32SeedCsvPath);
        bool noOpExact = !secondChanged
            && firstWriteTime == secondWriteTime
            && firstLength == secondLength
            && IsNaturalClearanceLowercaseSha256(artifactDigest);
        Check(noOpExact,
            "OUTPUT_CLEARANCE_32_SEED_ARTIFACT_NO_OP_EXACT",
            $"firstChanged={firstChanged}; secondChanged={secondChanged}; "
            + $"mtime={firstWriteTime.Ticks}->{secondWriteTime.Ticks}; "
            + $"bytes={firstLength}->{secondLength}; sha256={artifactDigest}");
    }

    private static byte[] SerializeNaturalClearanceSeedArtifact(
        IReadOnlyList<NaturalClearanceSeedArtifactRow> rows)
    {
        using MemoryStream stream = new();
        V27Utf8CsvWriter writer = new(stream, 8192);
        WriteNaturalClearanceSeedCsvRow(writer, new[]
        {
            "schemaVersion", "seedIndex", "deterministicSeed", "definitionId",
            "workstationTag", "recipeId", "outputLineId", "itemId",
            "outputQuantity", "batchMassGrams", "topologySourceDigest",
            "topologyStable", "facilityAttributionExact", "ownerRosterKey",
            "observationId", "batchCommitId", "actionEpochDelta", "actionStartDelta",
            "haulStartDelta", "clearanceMicroHours", "clearanceMilliHours",
            "telemetryCompletedCount", "telemetryActiveCount",
            "orphanPickupCount", "conflictingPublicationCount",
            "overPickupCount", "capacityExceededCount",
            "restoreInterruptionCount", "telemetryClean",
            "schedulerProvenanceExact", "deliveryExact", "randomStateDigest",
            "randomDrawDelta", "runSourceDigest", "result"
        });
        foreach (NaturalClearanceSeedArtifactRow row in rows)
        {
            WriteNaturalClearanceSeedCsvRow(writer, new[]
            {
                NaturalClearanceSeedArtifactRow.Schema,
                row.SeedIndex.ToString(CultureInfo.InvariantCulture),
                row.DeterministicSeed.ToString(CultureInfo.InvariantCulture),
                row.DefinitionId,
                row.WorkstationTag,
                row.RecipeId,
                row.OutputLineId,
                row.ItemId,
                row.OutputQuantity.ToString(CultureInfo.InvariantCulture),
                row.BatchMassGrams.ToString(CultureInfo.InvariantCulture),
                row.TopologySourceDigest,
                NaturalClearanceBooleanToken(row.TopologyStable),
                NaturalClearanceBooleanToken(row.FacilityAttributionExact),
                row.OwnerRosterKey,
                row.ObservationId,
                row.BatchCommitId,
                row.ActionEpochDelta.ToString(CultureInfo.InvariantCulture),
                row.ActionStartDelta.ToString(CultureInfo.InvariantCulture),
                row.HaulStartDelta.ToString(CultureInfo.InvariantCulture),
                row.ClearanceMicroHours.ToString(CultureInfo.InvariantCulture),
                row.ClearanceMilliHours.ToString(CultureInfo.InvariantCulture),
                row.TelemetryCompletedCount.ToString(CultureInfo.InvariantCulture),
                row.TelemetryActiveCount.ToString(CultureInfo.InvariantCulture),
                row.OrphanPickupCount.ToString(CultureInfo.InvariantCulture),
                row.ConflictingPublicationCount.ToString(CultureInfo.InvariantCulture),
                row.OverPickupCount.ToString(CultureInfo.InvariantCulture),
                row.CapacityExceededCount.ToString(CultureInfo.InvariantCulture),
                row.RestoreInterruptionCount.ToString(CultureInfo.InvariantCulture),
                NaturalClearanceBooleanToken(row.TelemetryClean),
                NaturalClearanceBooleanToken(row.SchedulerProvenanceExact),
                NaturalClearanceBooleanToken(row.DeliveryExact),
                row.RandomStateDigest,
                row.RandomDrawDelta.ToString(CultureInfo.InvariantCulture),
                row.RunSourceDigest,
                row.IsExact ? "PASS" : "FAIL"
            });
        }
        writer.Flush();
        return stream.ToArray();
    }

    private static void WriteNaturalClearanceSeedCsvRow(
        V27Utf8CsvWriter writer,
        IReadOnlyList<string> fields)
    {
        for (int index = 0; index < fields.Count; index++)
        {
            if (index != 0)
                writer.WriteAscii(',');
            writer.WriteEscapedField((fields[index] ?? string.Empty).AsSpan());
        }
        writer.WriteCrLf();
    }

    private static string NaturalClearanceBooleanToken(bool value) =>
        value ? "true" : "false";

    private static bool IsNaturalClearanceLowercaseSha256(string value)
    {
        if (value == null || value.Length != 64)
            return false;
        for (int index = 0; index < value.Length; index++)
        {
            char character = value[index];
            if (!(character is >= '0' and <= '9'
                || character is >= 'a' and <= 'f'))
            {
                return false;
            }
        }
        return true;
    }

    private IEnumerator VerifySchedulerOwnedPreparedOutputClearance(
        IWorldItemStackRuntime runtime,
        CharacterActor hauler,
        Facility warehouse,
        IReadOnlyList<NaturalClearanceExpectedSlice> expectedSlices,
        long expectedBatchMassGrams,
        string targetDestinationId,
        float verificationTimeScale = 8f,
        int maximumSchedulerOwners = int.MaxValue)
    {
        if (!float.IsFinite(verificationTimeScale)
            || verificationTimeScale < 0.1f)
        {
            throw new ArgumentOutOfRangeException(nameof(verificationTimeScale));
        }
        if (maximumSchedulerOwners <= 0)
            throw new ArgumentOutOfRangeException(nameof(maximumSchedulerOwners));
        NaturalClearanceExpectedSlice[] slices = (expectedSlices
                ?? Array.Empty<NaturalClearanceExpectedSlice>())
            .OrderBy(value => value?.StackId, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, NaturalClearanceExpectedSlice> expectedByStack = slices
            .Where(value => value != null)
            .ToDictionary(value => value.StackId, StringComparer.Ordinal);
        Dictionary<string, int> expectedByItem = slices
            .Where(value => value != null)
            .GroupBy(value => value.ItemId, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => checked(group.Sum(value => value.Quantity)),
                StringComparer.Ordinal);
        bool exactSlicesPrioritized = expectedByStack.Keys
            .OrderBy(value => value, StringComparer.Ordinal)
            .All(runtime.PrioritizeHaul);
        Check(
            exactSlicesPrioritized,
            "OUTPUT_CLEARANCE_NATURAL_EXACT_SLICES_PRIORITIZED",
            $"prioritized={exactSlicesPrioritized}; slices={expectedByStack.Count}");
        if (!exactSlicesPrioritized)
            yield break;
        CharacterActor[] actorPool = verificationActors
            .Where(value => value != null && !value.IsDead)
            .Concat(hauler == null
                ? Array.Empty<CharacterActor>()
                : new[] { hauler })
            .GroupBy(value => value.GetInstanceID())
            .Select(group => group.First())
            .OrderBy(value => value.BuildingCharacterId.Value, StringComparer.Ordinal)
            .ToArray();
        ICharacterDeprivationCommand clearanceDeprivationCommand =
            Resolve<ICharacterDeprivationCommand>(FindScope());
        ICharacterDeprivationQuery clearanceDeprivationQuery =
            Resolve<ICharacterDeprivationQuery>(FindScope());
        bool decisionStateReset = true;
        List<string> decisionStateResetDetails = new();
        foreach (CharacterActor actor in actorPool)
        {
            bool actorReset = TryResetNaturalClearanceDecisionState(
                actor,
                clearanceDeprivationCommand,
                clearanceDeprivationQuery,
                out string actorResetDetails);
            decisionStateReset &= actorReset;
            decisionStateResetDetails.Add(actorResetDetails);
        }
        Check(
            decisionStateReset && actorPool.Length > 0,
            "OUTPUT_CLEARANCE_NATURAL_DECISION_STATE_RESET",
            decisionStateResetDetails.Count == 0
                ? "no-live-actors"
                : string.Join("|", decisionStateResetDetails));
        if (!decisionStateReset || actorPool.Length == 0)
            yield break;
        bool initialDecisionIsolation =
            TryValidateNaturalClearanceDecisionIsolation(
                actorPool,
                out string initialDecisionIsolationDetails);
        Check(
            initialDecisionIsolation,
            "OUTPUT_CLEARANCE_NATURAL_DECISION_ISOLATION_READY",
            initialDecisionIsolationDetails);
        if (!initialDecisionIsolation)
            yield break;
        IWorldItemHaulPlanningService haulPlanning =
            Resolve<IWorldItemHaulPlanningService>(FindScope());
        WorldItemHaulPlanningService diagnosticPlanning =
            haulPlanning as WorldItemHaulPlanningService;
        IWarehouseMassAdmissionService warehouseAdmissions =
            Resolve<IWarehouseMassAdmissionService>(FindScope());
        List<NaturalClearanceActorProbe> candidates = new();
        List<string> rejectedCandidates = new();
        int readinessTurns = 0;
        while (true)
        {
            candidates.Clear();
            rejectedCandidates.Clear();
            foreach (CharacterActor actor in actorPool)
            {
                AIBrain candidateBrain = actor.Brain;
                AIHaul candidateAction = candidateBrain?.availableActions?
                    .Select(value => value?.actionset)
                    .OfType<AIHaul>()
                    .FirstOrDefault();
                AbilityHaul candidateHaul = AbilityHaul.Ensure(actor);
                AbilityMove candidateMovement = actor.GetComponent<AbilityMove>();
                bool actionCanStart = candidateAction != null
                    && candidateAction.CanStart(actor);
                string exactPreviewFailure = haulPlanning == null
                    ? "planner-unavailable"
                    : string.Empty;
                bool exactPreviewReady = haulPlanning != null
                    && haulPlanning.TryPreviewBestPlan(
                        actor,
                        out WorldItemHaulPlan exactPreview,
                        out exactPreviewFailure)
                    && TryMatchNaturalClearancePreview(
                        exactPreview,
                        expectedByStack,
                        targetDestinationId,
                        out exactPreviewFailure);
                if (candidateBrain == null
                    || candidateAction == null
                    || candidateHaul == null
                    || candidateMovement == null
                    || candidateBrain.HasRunningAction
                    || candidateHaul.IsHauling
                    || !actionCanStart
                    || !exactPreviewReady)
                {
                    string previewFailure = haulPlanning == null
                        ? "planner-unavailable"
                        : string.Empty;
                    bool previewReady = haulPlanning != null
                        && haulPlanning.TryPreviewBestPlan(
                            actor,
                            out _,
                            out previewFailure);
                    string stackFailures = diagnosticPlanning == null
                        ? "planner-diagnostics-unavailable"
                        : string.Join(
                            ",",
                            slices.Select(value =>
                            {
                                bool accepted = diagnosticPlanning
                                    .TryExplainCandidateForEditorTest(
                                        actor,
                                        value.StackId,
                                        out string reason);
                                return value.StackId + "="
                                    + (accepted
                                        ? "candidate-ready"
                                        : string.IsNullOrWhiteSpace(reason)
                                            ? "candidate-rejected"
                                            : reason);
                            }));
                    string haulFailure = candidateHaul == null
                        ? "haul-missing"
                        : candidateHaul.CanStartHauling(out string detail)
                            ? "haul-ready"
                            : string.IsNullOrEmpty(detail)
                                ? "haul-not-ready"
                                : detail;
                    rejectedCandidates.Add(string.Join(
                        ":",
                        actor.BuildingCharacterId.Value,
                        candidateBrain == null ? "brain-missing" : "brain-ready",
                        candidateAction == null ? "action-missing" : "action-ready",
                        candidateMovement == null ? "move-missing" : "move-ready",
                        candidateBrain?.HasRunningAction == true ? "running" : "idle",
                        candidateHaul?.IsHauling == true ? "hauling" : "not-hauling",
                        actionCanStart ? "action-can-start" : "action-blocked",
                        exactPreviewReady
                            ? "exact-preview-ready"
                            : "exact-preview=" + exactPreviewFailure,
                        haulFailure,
                        previewReady
                            ? "preview-ready"
                            : "preview=" + (string.IsNullOrWhiteSpace(previewFailure)
                                ? "not-ready"
                                : previewFailure),
                        "stacks=" + stackFailures));
                    continue;
                }
                candidates.Add(new NaturalClearanceActorProbe(
                    actor,
                    candidateBrain,
                    candidateAction,
                    candidateHaul));
            }

            // Path search broker deferral is explicitly non-authoritative
            // NoWork. Actors remain paused at this boundary, so waiting only
            // lets the production broker finish its queued search; it cannot
            // start or bypass the scheduler-owned physical haul.
            if (candidates.Count > 0
                || readinessTurns >= NaturalHaulReadinessMaximumTurns)
            {
                break;
            }
            readinessTurns++;
            yield return null;
        }
        if (candidates.Count > maximumSchedulerOwners)
        {
            candidates = candidates
                .OrderBy(value => value.CharacterId, StringComparer.Ordinal)
                .Take(maximumSchedulerOwners)
                .ToList();
        }

        bool ready = candidates.Count > 0
            && warehouse?.Inventory != null
            && slices.Length > 0
            && slices.All(value => value != null)
            && expectedByStack.Count == slices.Length
            && expectedByItem.Count > 0
            && slices.Sum(value => value.MassGrams) == expectedBatchMassGrams
            && expectedBatchMassGrams > 0L
            && !string.IsNullOrWhiteSpace(targetDestinationId);
        Check(ready,
            "OUTPUT_CLEARANCE_NATURAL_AIHAUL_READY",
            $"candidates={candidates.Count}; pool={actorPool.Length}; actors="
            + string.Join(",", candidates.Select(value => value.CharacterId))
            + "; rejected=" + (rejectedCandidates.Count == 0
                ? "none"
                : string.Join(",", rejectedCandidates))
            + $"; target={targetDestinationId}; "
            + $"readinessTurns={readinessTurns}/"
            + $"{NaturalHaulReadinessMaximumTurns}; "
            + $"slices={slices.Length}; quantity={expectedByItem.Values.Sum()}; "
            + $"grams={expectedBatchMassGrams}; warehouseMass="
            + $"{warehouse?.Inventory?.StoredMassGrams ?? -1L}/"
            + $"{warehouse?.Inventory?.ReservedInboundMassGrams ?? -1L}/"
            + $"{warehouse?.Inventory?.RemainingMassGrams ?? -1L}/"
            + $"{warehouse?.Inventory?.MaxMassGrams ?? -1L}; contents="
            + (warehouse?.Inventory == null
                ? "warehouse-missing"
                : DescribeStoredItemContents(runtime, warehouse.centerPos)));
        if (!ready)
            yield break;

        Dictionary<string, int> storedBeforeByItem = expectedByItem.Keys
            .ToDictionary(
                value => value,
                value => GetStoredItemQuantity(runtime, value, warehouse.centerPos),
                StringComparer.Ordinal);
        long storedMassBefore = warehouse.Inventory.StoredMassGrams;
        string storedContentsBefore = DescribeStoredItemContents(
            runtime,
            warehouse.centerPos);
        Dictionary<string, NaturalClearanceActorProbe> winners = new(
            StringComparer.Ordinal);
        HashSet<string> provenanceStackIds = new(StringComparer.Ordinal);
        HashSet<string> observedOperationIds = new(StringComparer.Ordinal);
        Dictionary<string, int> observedCommitmentQuantityByOperationAndStack =
            new(StringComparer.Ordinal);
        Dictionary<string, int> provenanceQuantityByStack = expectedByStack.Keys
            .ToDictionary(value => value, _ => 0, StringComparer.Ordinal);
        Dictionary<string, NaturalClearanceAdmissionWitness>
            targetAdmissionWitnesses = new(StringComparer.Ordinal);
        Dictionary<string, NaturalClearanceActorProbe>
            schedulerOwnerByOperation = new(StringComparer.Ordinal);
        Dictionary<string, bool> schedulerSameFrameByOperation =
            new(StringComparer.Ordinal);
        Dictionary<string, WorldItemReservedStackQuantity[]>
            stagedReservationsByOperation = new(StringComparer.Ordinal);
        Dictionary<string, NaturalClearanceAdmissionWitness[]>
            stagedAdmissionsByOperation = new(StringComparer.Ordinal);
        HashSet<string> promotedOperationIds = new(StringComparer.Ordinal);
        Dictionary<AbilityHaul, Action<WorldItemHaulPlan>> previousHaulHooks =
            new();
        List<string> schedulerHookDiagnostics = new();
        Dictionary<string, HashSet<string>> prePickupOperationsByStack =
            new(StringComparer.Ordinal);
        bool multipleCommittedOwners = false;
        bool invalidCommittedVector = false;
        bool invalidTargetAdmissionVector = false;
        bool sequentialReplanPreferenceFailed = false;
        bool unexplainedPrePickupRestart = false;
        int nextSequentialSliceReplanTurn =
            NaturalHaulSequentialSliceReplanDelayTurns;
        try
        {
            foreach (NaturalClearanceActorProbe candidate in candidates)
            {
                NaturalClearanceActorProbe probe = candidate;
                Action<WorldItemHaulPlan> previous =
                    probe.Haul.DebugBeforeHaulRoutineStart;
                previousHaulHooks.Add(probe.Haul, previous);
                probe.Haul.DebugBeforeHaulRoutineStart = plan =>
                {
                    previous?.Invoke(plan);
                    string[] planOperationIds = (plan?.ReservedStackQuantities
                            ?? Array.Empty<WorldItemReservedStackQuantity>())
                        .Select(value => value.OwnerOperationId)
                        .Where(value => !string.IsNullOrWhiteSpace(value))
                        .Distinct(StringComparer.Ordinal)
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray();
                    HaulDeliveryIntentSaveData intent = null;
                    if (planOperationIds.Length == 1)
                    {
                        runtime.TryCaptureHaulDeliveryIntent(
                            planOperationIds[0],
                            out intent);
                    }
                    string[] planStacks = (plan?.ReservedStackQuantities
                            ?? Array.Empty<WorldItemReservedStackQuantity>())
                        .Select(value => value.StackId ?? "<null>")
                        .OrderBy(value => value, StringComparer.Ordinal)
                        .ToArray();
                    schedulerHookDiagnostics.Add(
                        probe.CharacterId + ":destination="
                        + (plan?.PrimaryDestinationId ?? "<none>")
                        + ":stacks=" + string.Join(",", planStacks)
                        + ":operations=" + string.Join(",", planOperationIds)
                        + ":intent=" + (intent?.operationId ?? "<none>"));
                    if (!TryMatchNaturalClearancePrePickupPlan(
                            plan,
                            intent,
                            expectedByStack,
                            targetDestinationId,
                            probe.CharacterId,
                            out WorldItemReservedStackQuantity[] reservations,
                            out WarehouseHaulAdmissionSaveData[] admissions,
                            out string hookFailure))
                    {
                        schedulerHookDiagnostics.Add(
                            probe.CharacterId + ":rejected=" + hookFailure);
                        return;
                    }

                    bool sameFrame = probe.Brain.bestAction?.actionset is AIHaul
                        && probe.Brain.bestAction.HasStarted
                        && probe.Brain.RuntimeActionEpoch > probe.StartEpoch
                        && probe.Brain.RuntimeActionStartCount > probe.StartCount
                        && probe.Haul.RuntimeHaulStartCount > probe.HaulStartCount
                        && probe.Haul.IsHauling;
                    if (!sameFrame)
                    {
                        schedulerHookDiagnostics.Add(
                            probe.CharacterId + ":rejected=not-scheduler-same-frame");
                        return;
                    }
                    foreach (WorldItemReservedStackQuantity reservation in
                             reservations)
                    {
                        if (!prePickupOperationsByStack.TryGetValue(
                                reservation.StackId,
                                out HashSet<string> operations))
                        {
                            operations = new HashSet<string>(
                                StringComparer.Ordinal);
                            prePickupOperationsByStack.Add(
                                reservation.StackId,
                                operations);
                        }
                        operations.Add(intent.operationId);
                        if (operations.Count <= 1)
                            continue;

                        unexplainedPrePickupRestart = true;
                        TryValidateNaturalClearanceDecisionIsolation(
                            actorPool,
                            out string isolationDetails);
                        schedulerHookDiagnostics.Add(
                            probe.CharacterId
                            + ":rejected=unexplained-pre-pickup-restart:stack="
                            + reservation.StackId
                            + ":operations="
                            + string.Join(",", operations.OrderBy(
                                value => value,
                                StringComparer.Ordinal))
                            + ":brain=" + probe.Brain.LastActionFailure
                            + ":replan=" + probe.Brain.CaptureRuntimeDiagnostics()
                                .LastInterruptedReplanDetail
                            + ":haul=" + probe.Haul.LastFailureReason
                            + ":terminal=" + probe.Haul.LastTerminalDiagnostics
                            + ":isolation=" + isolationDetails);
                        return;
                    }
                    if (schedulerOwnerByOperation.TryGetValue(
                            intent.operationId,
                            out NaturalClearanceActorProbe existingOwner)
                        && !ReferenceEquals(existingOwner, probe))
                    {
                        multipleCommittedOwners = true;
                        return;
                    }
                    schedulerOwnerByOperation[intent.operationId] = probe;
                    schedulerSameFrameByOperation[intent.operationId] = sameFrame;
                    if (stagedReservationsByOperation.ContainsKey(intent.operationId)
                        || stagedAdmissionsByOperation.ContainsKey(intent.operationId))
                    {
                        invalidCommittedVector = true;
                        schedulerHookDiagnostics.Add(
                            probe.CharacterId + ":rejected=duplicate-operation-hook:"
                            + intent.operationId);
                        return;
                    }
                    stagedReservationsByOperation.Add(
                        intent.operationId,
                        reservations);
                    stagedAdmissionsByOperation.Add(
                        intent.operationId,
                        admissions.Select((admission, admissionIndex) =>
                            new NaturalClearanceAdmissionWitness(
                                intent.operationId,
                                admissionIndex,
                                admission)).ToArray());
                };
            }

            bool allPreferred = true;
            foreach (NaturalClearanceActorProbe candidate in candidates)
            {
                bool preferred = candidate.Brain
                    .PreferActionOnNextDecision<AIHaul>(180f);
                allPreferred &= preferred;
                if (!preferred)
                    continue;
                candidate.Actor.SetAiPaused(false);
                candidate.Brain.RequestImmediateReplan(clearFailures: true);
            }
            Check(allPreferred,
                "OUTPUT_CLEARANCE_NATURAL_AIHAUL_POOL_PREFERRED",
                $"preferred={allPreferred}; candidates={candidates.Count}; actors="
                + string.Join(",", candidates.Select(value => value.CharacterId)));
            if (!allPreferred)
                yield break;

            Time.timeScale = verificationTimeScale;

            int schedulerStartFrame = Time.frameCount;
            int schedulerLoopTurns = 0;
            int schedulingTurnsSinceProgress = 0;
            int maximumSchedulingTurns = checked(
                NaturalHaulMaximumSchedulingTurnsPerSlice
                * Mathf.Max(1, slices.Length));
            int progressHeartbeat = candidates.Sum(value =>
                value.Haul.RoutineHeartbeat);
            long progressStoredMass = warehouse.Inventory.StoredMassGrams;
            int progressStagedOperationCount = stagedReservationsByOperation.Count;
            int progressCommittedIntentCount = 0;
            int observedStartedOperationCount = 0;
            while (schedulerLoopTurns < maximumSchedulingTurns)
            {
                schedulerLoopTurns++;
                int currentHeartbeat = candidates.Sum(value =>
                    value.Haul.RoutineHeartbeat);
                long currentStoredMass = warehouse.Inventory.StoredMassGrams;
                int currentStagedOperationCount =
                    stagedReservationsByOperation.Count;
                int currentCommittedIntentCount = runtime
                    .CaptureHaulDeliveryIntentsByDestination(targetDestinationId)
                    .Count(value => value?.HasCommittedPickup == true);
                bool schedulerProgressed = currentHeartbeat != progressHeartbeat
                    || currentStoredMass != progressStoredMass
                    || currentStagedOperationCount != progressStagedOperationCount
                    || currentCommittedIntentCount != progressCommittedIntentCount;
                if (schedulerProgressed)
                {
                    progressHeartbeat = currentHeartbeat;
                    progressStoredMass = currentStoredMass;
                    progressStagedOperationCount = currentStagedOperationCount;
                    progressCommittedIntentCount = currentCommittedIntentCount;
                    schedulingTurnsSinceProgress = 0;
                }
                else
                {
                    schedulingTurnsSinceProgress++;
                }

                // Exact slices are intentionally routed sequentially. Give a
                // newly staged physical operation its own deterministic turn
                // window. Wall-clock time is deliberately absent: host load
                // may change render throughput but not the simulated frame path.
                if (stagedReservationsByOperation.Count
                    > observedStartedOperationCount)
                {
                    observedStartedOperationCount =
                        stagedReservationsByOperation.Count;
                }
                if (!Mathf.Approximately(
                        Time.timeScale,
                        verificationTimeScale))
                {
                    Time.timeScale = verificationTimeScale;
                }
                foreach (KeyValuePair<string, WorldItemReservedStackQuantity[]>
                         staged in stagedReservationsByOperation
                             .OrderBy(value => value.Key, StringComparer.Ordinal))
                {
                    string operationId = staged.Key;
                    if (promotedOperationIds.Contains(operationId))
                        continue;
                    if (!stagedAdmissionsByOperation.TryGetValue(
                            operationId,
                            out NaturalClearanceAdmissionWitness[] stagedAdmissions)
                        || stagedAdmissions.Length != staged.Value.Length
                        || !stagedAdmissions.All(witness =>
                            TryValidateNaturalClearanceAdmissionTerminal(
                                warehouseAdmissions,
                                witness,
                                out _)))
                    {
                        continue;
                    }
                    if (!schedulerOwnerByOperation.TryGetValue(
                            operationId,
                            out NaturalClearanceActorProbe stagedOwner)
                        || stagedOwner == null
                        || !schedulerSameFrameByOperation.TryGetValue(
                            operationId,
                            out bool stagedSameFrame)
                        || !stagedSameFrame)
                    {
                        invalidCommittedVector = true;
                        break;
                    }

                    foreach (NaturalClearanceAdmissionWitness witness in
                             stagedAdmissions)
                    {
                        if (targetAdmissionWitnesses.TryGetValue(
                                witness.TokenId,
                                out NaturalClearanceAdmissionWitness existing)
                            && !ReferenceEquals(existing, witness))
                        {
                            invalidTargetAdmissionVector = true;
                            break;
                        }
                        targetAdmissionWitnesses.TryAdd(
                            witness.TokenId,
                            witness);
                    }
                    if (invalidTargetAdmissionVector)
                        break;

                    winners[stagedOwner.CharacterId] = stagedOwner;
                    observedOperationIds.Add(operationId);
                    foreach (WorldItemReservedStackQuantity reservation in
                             staged.Value)
                    {
                        string observationKey = operationId
                            + "\u001f"
                            + reservation.StackId;
                        observedCommitmentQuantityByOperationAndStack.TryGetValue(
                            observationKey,
                            out int previouslyObservedQuantity);
                        if (reservation.Quantity < previouslyObservedQuantity)
                        {
                            invalidCommittedVector = true;
                            break;
                        }
                        int newlyObservedQuantity = reservation.Quantity
                            - previouslyObservedQuantity;
                        observedCommitmentQuantityByOperationAndStack[
                            observationKey] = reservation.Quantity;
                        provenanceStackIds.Add(reservation.StackId);
                        provenanceQuantityByStack[reservation.StackId] = checked(
                            provenanceQuantityByStack[reservation.StackId]
                            + newlyObservedQuantity);
                        if (provenanceQuantityByStack[reservation.StackId]
                            > expectedByStack[reservation.StackId].Quantity)
                        {
                            invalidCommittedVector = true;
                            break;
                        }
                    }
                    if (invalidCommittedVector)
                        break;
                    promotedOperationIds.Add(operationId);
                }
                if (invalidCommittedVector
                    || invalidTargetAdmissionVector
                    || unexplainedPrePickupRestart)
                    break;

                HaulDeliveryIntentSaveData[] exactIntents = runtime
                    .CaptureHaulDeliveryIntentsByDestination(targetDestinationId)
                    .Where(value => value?.HasCommittedPickup == true
                        && value.destinationKind
                            == WorldItemHaulDestinationKind.Warehouse
                        && string.Equals(
                            value.destinationId,
                            targetDestinationId,
                            StringComparison.Ordinal))
                    .OrderBy(value => value.operationId, StringComparer.Ordinal)
                    .ToArray();
                HaulDeliveryIntentSaveData[] targetIntents = exactIntents
                    .Where(value => (value.commitments
                            ?? new List<HaulDeliveryItemCommitmentSaveData>())
                        .Any(commitment => commitment != null
                            && expectedByStack.ContainsKey(
                                commitment.sourceStackId)))
                    .ToArray();
                HashSet<string> liveTargetOperationIds = targetIntents
                    .Select(value => value.operationId)
                    .ToHashSet(StringComparer.Ordinal);
                HaulDeliveryIntentSaveData[] evidenceIntents = targetIntents;
                Dictionary<string, string> liveStackOwners = new(
                    StringComparer.Ordinal);
                foreach (HaulDeliveryIntentSaveData liveIntent in evidenceIntents)
                {
                    if (!TryMatchNaturalClearanceCommitments(
                            liveIntent.commitments,
                            expectedByStack,
                            out string[] intentStackIds))
                    {
                        invalidCommittedVector = true;
                        break;
                    }
                    foreach (string stackId in intentStackIds)
                    {
                        if (liveTargetOperationIds.Contains(liveIntent.operationId)
                            && liveStackOwners.TryGetValue(
                                stackId,
                                out string existingOwner)
                            && !string.Equals(
                                existingOwner,
                                liveIntent.operationId,
                                StringComparison.Ordinal))
                        {
                            multipleCommittedOwners = true;
                            break;
                        }
                        if (liveTargetOperationIds.Contains(liveIntent.operationId))
                            liveStackOwners[stackId] = liveIntent.operationId;
                    }
                    if (multipleCommittedOwners)
                        break;

                    NaturalClearanceActorProbe[] matchingOwners = candidates
                        .Where(value => string.Equals(
                            value.CharacterId,
                            liveIntent.ownerCharacterId,
                            StringComparison.Ordinal))
                        .ToArray();
                    if (matchingOwners.Length != 1)
                    {
                        multipleCommittedOwners = true;
                        break;
                    }
                    NaturalClearanceActorProbe observedOwner =
                        matchingOwners.SingleOrDefault();
                    HaulDeliveryIntentSaveData ownerIntent = observedOwner?.Haul
                        .CaptureDeliveryIntentForSave();
                    bool exactLiveIntentOwner = observedOwner != null
                        && ownerIntent?.HasCommittedPickup == true
                        && string.Equals(
                            ownerIntent.operationId,
                            liveIntent.operationId,
                            StringComparison.Ordinal)
                        && string.Equals(
                            ownerIntent.ownerCharacterId,
                            liveIntent.ownerCharacterId,
                            StringComparison.Ordinal)
                        && TryMatchNaturalClearanceCommitments(
                            ownerIntent.commitments,
                            expectedByStack,
                            out string[] ownerStackIds)
                        && ownerStackIds.SequenceEqual(
                            intentStackIds,
                            StringComparer.Ordinal);
                    bool exactHookOwner = observedOwner != null
                        && schedulerOwnerByOperation.TryGetValue(
                            liveIntent.operationId,
                            out NaturalClearanceActorProbe hookOwner)
                        && ReferenceEquals(hookOwner, observedOwner)
                        && schedulerSameFrameByOperation.TryGetValue(
                            liveIntent.operationId,
                            out bool hookSameFrame)
                        && hookSameFrame;
                    bool sameFrameProvenance = exactHookOwner
                        || exactLiveIntentOwner
                        && observedOwner.Brain.bestAction?.actionset is AIHaul
                        && observedOwner.Brain.bestAction.HasStarted
                        && observedOwner.Brain.RuntimeActionEpoch
                            > observedOwner.StartEpoch
                        && observedOwner.Brain.RuntimeActionStartCount
                            > observedOwner.StartCount
                        && observedOwner.Haul.RuntimeHaulStartCount
                            > observedOwner.HaulStartCount
                        && observedOwner.Haul.IsHauling;
                    if (sameFrameProvenance)
                    {
                        HaulDeliveryItemCommitmentSaveData[] orderedCommitments =
                            (liveIntent.commitments
                                ?? new List<HaulDeliveryItemCommitmentSaveData>())
                            .OrderBy(
                                value => value.sourceStackId,
                                StringComparer.Ordinal)
                            .ToArray();
                        WarehouseHaulAdmissionSaveData[] orderedAdmissions =
                            (liveIntent.warehouseAdmissions
                                ?? new List<WarehouseHaulAdmissionSaveData>())
                            .OrderBy(
                                value => value?.sourceStackId,
                                StringComparer.Ordinal)
                            .ToArray();
                        if (orderedAdmissions.Length != orderedCommitments.Length
                            || orderedAdmissions.Any(value => value == null))
                        {
                            invalidTargetAdmissionVector = true;
                            break;
                        }
                        NaturalClearanceAdmissionWitness[] terminalWitnesses =
                            orderedAdmissions.Select((admission, admissionIndex) =>
                                new NaturalClearanceAdmissionWitness(
                                    liveIntent.operationId,
                                    admissionIndex,
                                    admission)).ToArray();
                        if (!terminalWitnesses.All(witness =>
                                TryValidateNaturalClearanceAdmissionTerminal(
                                    warehouseAdmissions,
                                    witness,
                                    out _)))
                        {
                            // A live committed pickup is not yet a delivered
                            // operation. The pre-pickup hook has staged its
                            // plan; only the immutable terminal admission
                            // receipt may promote it into final provenance.
                            continue;
                        }
                        for (int admissionIndex = 0;
                             admissionIndex < orderedCommitments.Length;
                             admissionIndex++)
                        {
                            HaulDeliveryItemCommitmentSaveData commitment =
                                orderedCommitments[admissionIndex];
                            WarehouseHaulAdmissionSaveData admission =
                                orderedAdmissions[admissionIndex];
                            bool exactAdmission = expectedByStack.TryGetValue(
                                    commitment.sourceStackId,
                                    out NaturalClearanceExpectedSlice expected)
                                && expected.Quantity > 0
                                && expected.MassGrams > 0L
                                && expected.MassGrams % expected.Quantity == 0L
                                && commitment.quantity > 0
                                && commitment.quantity <= expected.Quantity
                                && string.Equals(
                                    admission.sourceStackId,
                                    commitment.sourceStackId,
                                    StringComparison.Ordinal)
                                && string.Equals(
                                    admission.itemId,
                                    expected.ItemId,
                                    StringComparison.Ordinal)
                                && string.Equals(
                                    admission.lotFingerprint,
                                    commitment.expectedStackSignature,
                                    StringComparison.Ordinal)
                                && admission.quantity == commitment.quantity
                                && admission.reservedMassGrams == checked(
                                    expected.MassGrams / expected.Quantity
                                    * commitment.quantity)
                                && string.Equals(
                                    admission.ownerAdmissionOperationId,
                                    liveIntent.operationId
                                        + ":warehouse-admission:"
                                        + admissionIndex.ToString(
                                            "D2",
                                            CultureInfo.InvariantCulture),
                                    StringComparison.Ordinal)
                                && !string.IsNullOrWhiteSpace(admission.tokenId);
                            if (!exactAdmission
                                || targetAdmissionWitnesses.TryGetValue(
                                    admission.tokenId,
                                    out NaturalClearanceAdmissionWitness existing)
                                    && !existing.Matches(
                                        liveIntent.operationId,
                                        admissionIndex,
                                        admission))
                            {
                                invalidTargetAdmissionVector = true;
                                break;
                            }
                            targetAdmissionWitnesses.TryAdd(
                                admission.tokenId,
                                new NaturalClearanceAdmissionWitness(
                                    liveIntent.operationId,
                                    admissionIndex,
                                    admission));
                        }
                        if (invalidTargetAdmissionVector)
                            break;
                        winners[observedOwner.CharacterId] = observedOwner;
                        observedOperationIds.Add(liveIntent.operationId);
                        foreach (HaulDeliveryItemCommitmentSaveData commitment in
                                 liveIntent.commitments)
                        {
                            string observationKey = liveIntent.operationId
                                + "\u001f"
                                + commitment.sourceStackId;
                            observedCommitmentQuantityByOperationAndStack.TryGetValue(
                                observationKey,
                                out int previouslyObservedQuantity);
                            if (commitment.quantity < previouslyObservedQuantity)
                            {
                                invalidCommittedVector = true;
                                break;
                            }
                            int newlyObservedQuantity = commitment.quantity
                                - previouslyObservedQuantity;
                            observedCommitmentQuantityByOperationAndStack[
                                observationKey] = commitment.quantity;
                            provenanceStackIds.Add(commitment.sourceStackId);
                            provenanceQuantityByStack[commitment.sourceStackId] =
                                checked(
                                    provenanceQuantityByStack[
                                        commitment.sourceStackId]
                                    + newlyObservedQuantity);
                            if (provenanceQuantityByStack[
                                    commitment.sourceStackId]
                                > expectedByStack[
                                    commitment.sourceStackId].Quantity)
                            {
                                invalidCommittedVector = true;
                                break;
                            }
                        }
                    }
                }
                if (invalidCommittedVector
                    || invalidTargetAdmissionVector
                    || multipleCommittedOwners)
                    break;

                bool allDelivered = expectedByItem.All(pair =>
                    GetStoredItemQuantity(runtime, pair.Key, warehouse.centerPos)
                        - storedBeforeByItem[pair.Key] >= pair.Value);
                bool anyDelivered = expectedByItem.Any(pair =>
                    GetStoredItemQuantity(runtime, pair.Key, warehouse.centerPos)
                        - storedBeforeByItem[pair.Key] > 0);
                bool targetAdmissionsCommitted =
                    TryValidateNaturalClearanceTargetAdmissions(
                        warehouseAdmissions,
                        expectedByStack,
                        targetAdmissionWitnesses,
                        warehouse.PersistentInstanceId.Value,
                        out _);
                bool observedTargetIntentsRetired = observedOperationIds.All(
                    operationId => !runtime.TryCaptureHaulDeliveryIntent(
                        operationId,
                        out _));
                if (allDelivered
                    && provenanceStackIds.Count == slices.Length
                    && targetIntents.Length == 0
                    && observedTargetIntentsRetired
                    && targetAdmissionsCommitted)
                {
                    foreach (NaturalClearanceActorProbe candidate in candidates)
                        candidate.Actor.SetAiPaused(true);
                    break;
                }
                bool stagedTargetPlanActive = stagedReservationsByOperation.Keys
                    .Where(operationId =>
                        !promotedOperationIds.Contains(operationId))
                    .Any(operationId => runtime.TryCaptureHaulDeliveryIntent(
                        operationId,
                        out _));
                bool needsNextExactSlice = !allDelivered
                    && targetIntents.Length == 0
                    && !stagedTargetPlanActive
                    && candidates.All(value => !value.Haul.IsHauling)
                    && schedulerLoopTurns >= nextSequentialSliceReplanTurn;
                if (needsNextExactSlice)
                {
                    nextSequentialSliceReplanTurn = checked(
                        schedulerLoopTurns
                        + NaturalHaulSequentialSliceReplanDelayTurns);
                    bool decisionIsolation =
                        TryValidateNaturalClearanceDecisionIsolation(
                            actorPool,
                            out string decisionIsolationDetails);
                    if (!decisionIsolation)
                    {
                        sequentialReplanPreferenceFailed = true;
                        schedulerHookDiagnostics.Add(
                            "sequential-slice-isolation-failed:"
                            + decisionIsolationDetails);
                        break;
                    }
                    bool exactPreviewAvailable = candidates.Any(candidate =>
                    {
                        string previewFailure = string.Empty;
                        return haulPlanning.TryPreviewBestPlan(
                                candidate.Actor,
                                out WorldItemHaulPlan preview,
                                out previewFailure)
                            && TryMatchNaturalClearancePreview(
                                preview,
                                expectedByStack,
                                targetDestinationId,
                                out _);
                    });
                    if (!exactPreviewAvailable)
                    {
                        yield return null;
                        continue;
                    }
                    foreach (NaturalClearanceActorProbe candidate in candidates)
                    {
                        bool preferred = candidate.Brain
                            .PreferActionOnNextDecision<AIHaul>(180f);
                        if (!preferred)
                        {
                            sequentialReplanPreferenceFailed = true;
                            break;
                        }
                        candidate.Actor.SetAiPaused(false);
                        if (candidate.Brain.HasRunningAction)
                        {
                            candidate.Brain.StopCurrentActionForReplan(
                                "다중 출력의 다음 exact slice 운반");
                        }
                        candidate.Brain.RequestImmediateReplan(
                            clearFailures: true);
                    }
                    if (sequentialReplanPreferenceFailed)
                        break;
                }
                yield return null;
            }
            bool exactSchedulerProvenance = !invalidCommittedVector
                && !invalidTargetAdmissionVector
                && !multipleCommittedOwners
                && !sequentialReplanPreferenceFailed
                && !unexplainedPrePickupRestart
                && provenanceStackIds.SetEquals(expectedByStack.Keys)
                && expectedByStack.All(pair =>
                    provenanceQuantityByStack[pair.Key]
                        == pair.Value.Quantity)
                && winners.Count > 0;
            string schedulerRuntimeDetails = string.Join(
                "|",
                candidates.Select(value =>
                {
                    string previewFailure = haulPlanning == null
                        ? "planner-unavailable"
                        : string.Empty;
                    bool previewReady = haulPlanning != null
                        && haulPlanning.TryPreviewBestPlan(
                            value.Actor,
                            out _,
                            out previewFailure);
                    bool haulCanStart = value.Haul.CanStartHauling(
                        out string haulFailure);
                    AbilityMove movement = value.Actor.GetComponent<AbilityMove>();
                    return value.CharacterId
                        + $":paused={value.Actor.IsAiPaused()}"
                        + $":canRun={value.Actor.CanRunAi}"
                        + $":lifecycle={value.Actor.CurrentLifecycleState}"
                        + $":dead={value.Actor.IsDead}"
                        + $":preferred={value.Brain.IsActionPreferredForNextDecision<AIHaul>()}"
                        + $":preferredDisposition={value.Brain.RuntimePreferredActionDisposition}"
                        + $":immediateRequests={value.Brain.ImmediateDecisionRequestCount}"
                        + $":epoch={value.Brain.RuntimeActionEpoch - value.StartEpoch}"
                        + $":starts={value.Brain.RuntimeActionStartCount - value.StartCount}"
                        + $":haulStarts={value.Haul.RuntimeHaulStartCount - value.HaulStartCount}"
                        + $":heartbeat={value.Haul.RoutineHeartbeat}"
                        + $":routine={value.Haul.HasHaulingRoutineForDiagnostics}"
                        + $":running={value.Brain.HasRunningAction}"
                        + $":best={value.Brain.bestAction?.actionset?.GetType().Name ?? "none"}"
                        + $":phase={value.Brain.CurrentActionPhase}"
                        + $":failure={value.Brain.LastActionFailure}"
                        + $":haulStage={value.Haul.CurrentExecutionStage}"
                        + $":haulFailure={value.Haul.LastFailureReason}"
                        + $":haulTerminal={value.Haul.LastTerminalDiagnostics}"
                        + $":moveFailure={movement?.LastGridMoveFailureReason}"
                        + $":haulCanStart={haulCanStart}:{haulFailure}"
                        + $":preview={previewReady}:{previewFailure}";
                }));
            Check(exactSchedulerProvenance,
                "OUTPUT_CLEARANCE_NATURAL_AIHAUL_SCHEDULER_OWNED",
                $"exactSameFrame={exactSchedulerProvenance}; operations="
                + $"{string.Join("|", observedOperationIds.OrderBy(value => value, StringComparer.Ordinal))}; "
                + $"actors={string.Join("|", winners.Keys.OrderBy(value => value, StringComparer.Ordinal))}; "
                + $"multipleOwners={multipleCommittedOwners}; invalidVector={invalidCommittedVector}; "
                + $"prePickupRestart={unexplainedPrePickupRestart}; "
                + $"frames={schedulerStartFrame}->{Time.frameCount}; loopTurns={schedulerLoopTurns}/"
                + $"{maximumSchedulingTurns}; turnsSinceProgress={schedulingTurnsSinceProgress}; "
                + $"slices={provenanceStackIds.Count}/{slices.Length}; quantities="
                + string.Join("|", provenanceQuantityByStack
                    .OrderBy(value => value.Key, StringComparer.Ordinal)
                    .Select(value => value.Key + ":" + value.Value + "/"
                        + expectedByStack[value.Key].Quantity))
                + "; hooks=" + (schedulerHookDiagnostics.Count == 0
                    ? "none"
                    : string.Join("|", schedulerHookDiagnostics))
                + "; runtime=" + schedulerRuntimeDetails);
            bool targetAdmissionsExact =
                TryValidateNaturalClearanceTargetAdmissions(
                    warehouseAdmissions,
                    expectedByStack,
                    targetAdmissionWitnesses,
                    warehouse.PersistentInstanceId.Value,
                    out string targetAdmissionDetails);
            bool destinationTargetIntentStillActive = runtime
                .CaptureHaulDeliveryIntentsByDestination(targetDestinationId)
                .Where(value => value?.HasCommittedPickup == true)
                .Any(value => (value.commitments
                        ?? new List<HaulDeliveryItemCommitmentSaveData>())
                    .Any(commitment => commitment != null
                        && expectedByStack.ContainsKey(
                            commitment.sourceStackId)));
            string[] retainedObservedIntentIds = observedOperationIds
                .Where(operationId => runtime.TryCaptureHaulDeliveryIntent(
                    operationId,
                    out _))
                .OrderBy(value => value, StringComparer.Ordinal)
                .ToArray();
            bool targetIntentStillActive = destinationTargetIntentStillActive
                || retainedObservedIntentIds.Length > 0;
            bool deliveryExact = expectedByItem.All(pair =>
                    GetStoredItemQuantity(runtime, pair.Key, warehouse.centerPos)
                        - storedBeforeByItem[pair.Key] == pair.Value)
                && targetAdmissionsExact
                && !targetIntentStillActive
                && winners.Count > 0
                && exactSchedulerProvenance;
            if (activeNaturalClearanceSeedRun != null)
            {
                NaturalClearanceActorProbe[] orderedWinners = winners.Values
                    .OrderBy(value => value.CharacterId, StringComparer.Ordinal)
                    .ToArray();
                activeNaturalClearanceSeedRun.OwnerRosterKey = string.Join(
                    "+",
                    orderedWinners.Select(value =>
                        ProductionOutputClearanceNaturalDiagnostics
                            .ResolveRosterKey(value.CharacterId)));
                activeNaturalClearanceSeedRun.RuntimeOperationId = string.Join(
                    "|",
                    observedOperationIds.OrderBy(value => value, StringComparer.Ordinal));
                activeNaturalClearanceSeedRun.ActionEpochDelta = orderedWinners
                    .Sum(value => checked(
                        value.Brain.RuntimeActionEpoch - value.StartEpoch));
                activeNaturalClearanceSeedRun.ActionStartDelta = orderedWinners
                    .Sum(value => checked(
                        value.Brain.RuntimeActionStartCount - value.StartCount));
                activeNaturalClearanceSeedRun.HaulStartDelta = orderedWinners
                    .Sum(value => checked(
                        value.Haul.RuntimeHaulStartCount - value.HaulStartCount));
                activeNaturalClearanceSeedRun.SchedulerProvenanceExact =
                    exactSchedulerProvenance;
                activeNaturalClearanceSeedRun.DeliveryExact = deliveryExact;
            }
            Check(deliveryExact,
                "OUTPUT_CLEARANCE_NATURAL_AIHAUL_DELIVERED_EXACT",
                $"storedDelta={string.Join("|", expectedByItem.OrderBy(value => value.Key, StringComparer.Ordinal).Select(pair => pair.Key + ":" + (GetStoredItemQuantity(runtime, pair.Key, warehouse.centerPos) - storedBeforeByItem[pair.Key]) + "/" + pair.Value))}; storedMass="
                + $"{storedMassBefore}->{warehouse.Inventory.StoredMassGrams}"
                + $"/{warehouse.Inventory.MaxMassGrams}; contentsBefore="
                + storedContentsBefore + "; contentsAfter="
                + DescribeStoredItemContents(runtime, warehouse.centerPos)
                + "; expectedBatchMass="
                + $"{expectedBatchMassGrams}; reservedInbound="
                + $"{warehouse.Inventory.ReservedInboundMassGrams}; hauling="
                + $"{candidates.Count(value => value.Haul.IsHauling)}; actors={winners.Count}; "
                + $"target={targetDestinationId}; targetIntentActive="
                + $"{targetIntentStillActive}; targetAdmissions="
                + targetAdmissionDetails + "; retainedOperations="
                + (retainedObservedIntentIds.Length == 0
                    ? "none"
                    : string.Join("|", retainedObservedIntentIds)));
        }
        finally
        {
            foreach (KeyValuePair<AbilityHaul, Action<WorldItemHaulPlan>> hook in
                     previousHaulHooks)
            {
                if (hook.Key != null)
                    hook.Key.DebugBeforeHaulRoutineStart = hook.Value;
            }
            foreach (NaturalClearanceActorProbe candidate in candidates)
                candidate.Actor?.SetAiPaused(true);
        }
    }

    private static bool TryMatchNaturalClearancePreview(
        WorldItemHaulPlan plan,
        IReadOnlyDictionary<string, NaturalClearanceExpectedSlice>
            expectedByStack,
        string expectedDestinationId,
        out string failureReason)
    {
        failureReason = string.Empty;
        if (plan?.IsValid != true
            || expectedByStack == null
            || expectedByStack.Count == 0
            || string.IsNullOrWhiteSpace(expectedDestinationId))
        {
            failureReason = "invalid-preview-contract";
            return false;
        }
        if (plan.PrimaryDestination != WorldItemHaulDestinationKind.Warehouse
            || !string.Equals(
                plan.PrimaryDestinationId,
                expectedDestinationId,
                StringComparison.Ordinal))
        {
            failureReason = "preview-destination-mismatch:"
                + plan.PrimaryDestination + ":" + plan.PrimaryDestinationId;
            return false;
        }

        Dictionary<string, int> quantities = new(StringComparer.Ordinal);
        foreach (WorldItemReservedStackQuantity reservation in
                 plan.ReservedStackQuantities)
        {
            if (!expectedByStack.TryGetValue(
                    reservation.StackId,
                    out NaturalClearanceExpectedSlice expected))
            {
                failureReason = "preview-foreign-slice:"
                    + reservation.StackId;
                return false;
            }
            if (!string.Equals(
                    reservation.ItemId,
                    expected.ItemId,
                    StringComparison.Ordinal)
                || reservation.DestinationKind
                    != WorldItemHaulDestinationKind.Warehouse
                || !string.Equals(
                    reservation.DestinationId,
                    expectedDestinationId,
                    StringComparison.Ordinal))
            {
                failureReason = "preview-slice-identity-mismatch:"
                    + reservation.StackId;
                return false;
            }
            quantities.TryGetValue(reservation.StackId, out int current);
            quantities[reservation.StackId] = checked(
                current + reservation.Quantity);
        }

        string[] invalidSlices = quantities
            .Where(pair => !expectedByStack.TryGetValue(
                    pair.Key,
                    out NaturalClearanceExpectedSlice expected)
                || pair.Value <= 0
                || pair.Value > expected.Quantity)
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .Select(pair => pair.Key + ":" + pair.Value + "/"
                + (expectedByStack.TryGetValue(
                    pair.Key,
                    out NaturalClearanceExpectedSlice expected)
                    ? expected.Quantity
                    : 0))
            .ToArray();
        if (quantities.Count == 0 || invalidSlices.Length != 0)
        {
            failureReason = quantities.Count == 0
                ? "preview-has-no-expected-slice"
                : "preview-invalid-partial-slice:"
                    + string.Join("|", invalidSlices);
            return false;
        }
        return true;
    }

    private static bool TryMatchNaturalClearancePrePickupPlan(
        WorldItemHaulPlan plan,
        HaulDeliveryIntentSaveData intent,
        IReadOnlyDictionary<string, NaturalClearanceExpectedSlice>
            expectedByStack,
        string expectedDestinationId,
        string expectedOwnerCharacterId,
        out WorldItemReservedStackQuantity[] reservations,
        out WarehouseHaulAdmissionSaveData[] admissions,
        out string failureReason)
    {
        reservations = Array.Empty<WorldItemReservedStackQuantity>();
        admissions = Array.Empty<WarehouseHaulAdmissionSaveData>();
        failureReason = string.Empty;
        if (plan?.IsValid != true
            || plan.IsDeliveryOnlyResume
            || intent == null
            || expectedByStack == null
            || expectedByStack.Count == 0
            || string.IsNullOrWhiteSpace(expectedDestinationId)
            || string.IsNullOrWhiteSpace(expectedOwnerCharacterId))
        {
            failureReason = "invalid-pre-pickup-contract";
            return false;
        }
        if (plan.PrimaryDestination != WorldItemHaulDestinationKind.Warehouse
            || !string.Equals(
                plan.PrimaryDestinationId,
                expectedDestinationId,
                StringComparison.Ordinal)
            || intent.destinationKind
                != WorldItemHaulDestinationKind.Warehouse
            || !string.Equals(
                intent.destinationId,
                expectedDestinationId,
                StringComparison.Ordinal))
        {
            failureReason = "pre-pickup-destination-mismatch";
            return false;
        }
        if (string.IsNullOrWhiteSpace(intent.operationId)
            || !string.Equals(
                intent.operationId,
                intent.operationId.Trim(),
                StringComparison.Ordinal)
            || !string.Equals(
                intent.ownerCharacterId,
                expectedOwnerCharacterId,
                StringComparison.Ordinal)
            || intent.HasCommittedPickup
            || (intent.commitments?.Count ?? 0) != 0)
        {
            failureReason = "pre-pickup-intent-state-mismatch";
            return false;
        }

        reservations = (plan.ReservedStackQuantities
                ?? Array.Empty<WorldItemReservedStackQuantity>())
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        admissions = (intent.warehouseAdmissions
                ?? new List<WarehouseHaulAdmissionSaveData>())
            .OrderBy(value => value?.sourceStackId, StringComparer.Ordinal)
            .ToArray();
        if (reservations.Length == 0
            || reservations.Length != admissions.Length
            || admissions.Any(value => value == null)
            || reservations.Select(value => value.StackId)
                .Distinct(StringComparer.Ordinal).Count() != reservations.Length
            || admissions.Select(value => value.tokenId)
                .Distinct(StringComparer.Ordinal).Count() != admissions.Length)
        {
            failureReason = "pre-pickup-vector-shape-mismatch";
            return false;
        }

        for (int index = 0; index < reservations.Length; index++)
        {
            WorldItemReservedStackQuantity reservation = reservations[index];
            WarehouseHaulAdmissionSaveData admission = admissions[index];
            if (!expectedByStack.TryGetValue(
                    reservation.StackId,
                    out NaturalClearanceExpectedSlice expected)
                || expected.Quantity <= 0
                || expected.MassGrams <= 0L
                || expected.MassGrams % expected.Quantity != 0L
                || reservation.Quantity <= 0
                || reservation.Quantity > expected.Quantity
                || !string.Equals(
                    reservation.ItemId,
                    expected.ItemId,
                    StringComparison.Ordinal)
                || reservation.DestinationKind
                    != WorldItemHaulDestinationKind.Warehouse
                || !string.Equals(
                    reservation.DestinationId,
                    expectedDestinationId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    reservation.OwnerOperationId,
                    intent.operationId,
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(reservation.LeaseId)
                || !string.Equals(
                    admission.sourceStackId,
                    reservation.StackId,
                    StringComparison.Ordinal)
                || !string.Equals(
                    admission.itemId,
                    expected.ItemId,
                    StringComparison.Ordinal)
                || admission.quantity != reservation.Quantity
                || admission.reservedMassGrams != checked(
                    expected.MassGrams / expected.Quantity
                    * reservation.Quantity)
                || !string.Equals(
                    admission.ownerAdmissionOperationId,
                    intent.operationId + ":warehouse-admission:"
                        + index.ToString("D2", CultureInfo.InvariantCulture),
                    StringComparison.Ordinal)
                || string.IsNullOrWhiteSpace(admission.tokenId)
                || string.IsNullOrWhiteSpace(admission.lotFingerprint)
                || admission.catalogRevision <= 0L
                || admission.sourceRevision <= 0L)
            {
                failureReason = "pre-pickup-slice-mismatch:"
                    + (reservation.StackId ?? "<null>")
                    + ":" + index.ToString(CultureInfo.InvariantCulture);
                reservations = Array.Empty<WorldItemReservedStackQuantity>();
                admissions = Array.Empty<WarehouseHaulAdmissionSaveData>();
                return false;
            }
        }
        return true;
    }

    private static bool TryValidateNaturalClearanceAdmissionTerminal(
        IWarehouseMassAdmissionService admissionService,
        NaturalClearanceAdmissionWitness witness,
        out string details)
    {
        if (admissionService == null || witness == null)
        {
            details = "terminal-admission-contract-missing";
            return false;
        }
        bool statusCaptured = admissionService.TryGetStatus(
            witness.TokenId,
            out WarehouseMassAdmissionStatusSnapshot status);
        bool receiptCaptured = admissionService.TryGetReceipt(
            witness.TokenId,
            out WarehouseMassAdmissionReceipt receipt);
        bool exact = !string.IsNullOrWhiteSpace(witness.TokenId)
            && !string.IsNullOrWhiteSpace(witness.HaulOperationId)
            && witness.AdmissionIndex >= 0
            && witness.Quantity > 0
            && witness.ReservedMassGrams > 0L
            && witness.CatalogRevision > 0L
            && witness.SourceRevision > 0L
            && statusCaptured
            && status.Status == WarehouseMassAdmissionTokenStatus.Committed
            && string.Equals(
                status.Token.TokenId,
                witness.TokenId,
                StringComparison.Ordinal)
            && string.Equals(
                status.Token.WarehouseId.Value,
                witness.WarehouseId,
                StringComparison.Ordinal)
            && string.Equals(
                status.Token.OwnerOperationId,
                witness.OwnerAdmissionOperationId,
                StringComparison.Ordinal)
            && string.Equals(
                status.Token.ItemId.Value,
                witness.ItemId,
                StringComparison.Ordinal)
            && string.Equals(
                status.Token.ItemInstanceId,
                witness.ItemInstanceId,
                StringComparison.Ordinal)
            && string.Equals(
                status.Token.LotFingerprint,
                witness.LotFingerprint,
                StringComparison.Ordinal)
            && status.Token.AcceptedQuantity == witness.Quantity
            && status.Token.ReservedMassGrams == witness.ReservedMassGrams
            && status.Token.CatalogRevision == witness.CatalogRevision
            && status.Token.SourceRevision == witness.SourceRevision
            && receiptCaptured
            && string.Equals(
                receipt.TokenId,
                witness.TokenId,
                StringComparison.Ordinal)
            && string.Equals(
                receipt.CommitId,
                witness.HaulOperationId + ":warehouse-deposit",
                StringComparison.Ordinal)
            && string.Equals(
                receipt.WarehouseId.Value,
                witness.WarehouseId,
                StringComparison.Ordinal)
            && string.Equals(
                receipt.OwnerOperationId,
                witness.OwnerAdmissionOperationId,
                StringComparison.Ordinal)
            && string.Equals(
                receipt.ItemId.Value,
                witness.ItemId,
                StringComparison.Ordinal)
            && string.Equals(
                receipt.LotFingerprint,
                witness.LotFingerprint,
                StringComparison.Ordinal)
            && receipt.CommittedQuantity == witness.Quantity
            && receipt.CommittedMassGrams == witness.ReservedMassGrams
            && receipt.ResultWarehouseCapacityRevision
                > status.Token.WarehouseCapacityRevision;
        details = witness.TokenId + ":"
            + (statusCaptured ? status.Status.ToString() : "missing-status")
            + ":" + (receiptCaptured ? "receipt" : "missing-receipt")
            + ":" + (exact ? "exact" : "invalid");
        return exact;
    }

    private static bool TryValidateNaturalClearanceTargetAdmissions(
        IWarehouseMassAdmissionService admissionService,
        IReadOnlyDictionary<string, NaturalClearanceExpectedSlice> expectedByStack,
        IReadOnlyDictionary<string, NaturalClearanceAdmissionWitness> witnesses,
        string expectedWarehouseId,
        out string details)
    {
        if (admissionService == null
            || expectedByStack == null
            || expectedByStack.Count == 0
            || witnesses == null
            || witnesses.Count == 0
            || string.IsNullOrWhiteSpace(expectedWarehouseId))
        {
            details = $"service={admissionService != null}; expected="
                + $"{expectedByStack?.Count ?? 0}; witnesses="
                + $"{witnesses?.Count ?? 0}; warehouse="
                + (expectedWarehouseId ?? "missing");
            return false;
        }

        List<string> witnessDetails = new();
        bool exact = witnesses.Values.All(value => value != null
            && expectedByStack.ContainsKey(value.SourceStackId));
        foreach (KeyValuePair<string, NaturalClearanceExpectedSlice> pair in
                 expectedByStack.OrderBy(value => value.Key, StringComparer.Ordinal))
        {
            NaturalClearanceAdmissionWitness[] stackWitnesses = witnesses.Values
                .Where(value => value != null
                    && string.Equals(
                        value.SourceStackId,
                        pair.Key,
                        StringComparison.Ordinal))
                .OrderBy(value => value.HaulOperationId, StringComparer.Ordinal)
                .ThenBy(value => value.AdmissionIndex)
                .ToArray();
            if (stackWitnesses.Length == 0)
            {
                exact = false;
                witnessDetails.Add(pair.Key + ":missing-witness");
                continue;
            }

            NaturalClearanceExpectedSlice expected = pair.Value;
            int stackQuantity = 0;
            long stackMassGrams = 0L;
            foreach (NaturalClearanceAdmissionWitness witness in stackWitnesses)
            {
                bool expectedWitness = string.Equals(
                        witness.ItemId,
                        expected.ItemId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        witness.WarehouseId,
                        expectedWarehouseId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        witness.OwnerAdmissionOperationId,
                        witness.HaulOperationId
                            + ":warehouse-admission:"
                            + witness.AdmissionIndex.ToString(
                                "D2",
                                CultureInfo.InvariantCulture),
                        StringComparison.Ordinal)
                    && !string.IsNullOrWhiteSpace(witness.TokenId)
                    && !string.IsNullOrWhiteSpace(witness.HaulOperationId)
                    && witness.AdmissionIndex >= 0
                    && witness.Quantity > 0
                    && witness.ReservedMassGrams > 0L
                    && witness.CatalogRevision > 0L
                    && witness.SourceRevision > 0L;
                bool statusCaptured = admissionService.TryGetStatus(
                    witness.TokenId,
                    out WarehouseMassAdmissionStatusSnapshot status);
                bool receiptCaptured = admissionService.TryGetReceipt(
                    witness.TokenId,
                    out WarehouseMassAdmissionReceipt receipt);
                bool terminalExact = statusCaptured
                    && status.Status == WarehouseMassAdmissionTokenStatus.Committed
                    && string.Equals(
                        status.Token.TokenId,
                        witness.TokenId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        status.Token.WarehouseId.Value,
                        witness.WarehouseId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        status.Token.OwnerOperationId,
                        witness.OwnerAdmissionOperationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        status.Token.ItemId.Value,
                        witness.ItemId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        status.Token.ItemInstanceId,
                        witness.ItemInstanceId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        status.Token.LotFingerprint,
                        witness.LotFingerprint,
                        StringComparison.Ordinal)
                    && status.Token.AcceptedQuantity == witness.Quantity
                    && status.Token.ReservedMassGrams
                        == witness.ReservedMassGrams
                    && status.Token.CatalogRevision == witness.CatalogRevision
                    && status.Token.SourceRevision == witness.SourceRevision
                    && receiptCaptured
                    && string.Equals(
                        receipt.TokenId,
                        witness.TokenId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        receipt.CommitId,
                        witness.HaulOperationId + ":warehouse-deposit",
                        StringComparison.Ordinal)
                    && string.Equals(
                        receipt.WarehouseId.Value,
                        witness.WarehouseId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        receipt.OwnerOperationId,
                        witness.OwnerAdmissionOperationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        receipt.ItemId.Value,
                        witness.ItemId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        receipt.LotFingerprint,
                        witness.LotFingerprint,
                        StringComparison.Ordinal)
                    && receipt.CommittedQuantity == witness.Quantity
                    && receipt.CommittedMassGrams == witness.ReservedMassGrams
                    && receipt.ResultWarehouseCapacityRevision
                        > status.Token.WarehouseCapacityRevision;
                bool witnessExact = expectedWitness && terminalExact;
                exact &= witnessExact;
                if (witnessExact)
                {
                    stackQuantity = checked(stackQuantity + witness.Quantity);
                    stackMassGrams = checked(
                        stackMassGrams + witness.ReservedMassGrams);
                }
                witnessDetails.Add(witness.TokenId
                    + ":" + witness.SourceStackId
                    + ":" + witness.Quantity.ToString(
                        CultureInfo.InvariantCulture)
                    + ":" + witness.ReservedMassGrams.ToString(
                        CultureInfo.InvariantCulture)
                    + ":" + (statusCaptured
                        ? status.Status.ToString()
                        : "missing-status")
                    + ":" + (receiptCaptured
                        ? "receipt"
                        : "missing-receipt")
                    + ":" + (witnessExact ? "exact" : "invalid"));
            }
            bool aggregateExact = stackQuantity == expected.Quantity
                && stackMassGrams == expected.MassGrams;
            exact &= aggregateExact;
            witnessDetails.Add(pair.Key + ":aggregate="
                + stackQuantity.ToString(CultureInfo.InvariantCulture)
                + "/" + expected.Quantity.ToString(CultureInfo.InvariantCulture)
                + ":" + stackMassGrams.ToString(CultureInfo.InvariantCulture)
                + "/" + expected.MassGrams.ToString(CultureInfo.InvariantCulture)
                + ":" + (aggregateExact ? "exact" : "invalid"));
        }

        details = "exact=" + exact
            + "; tokens=" + string.Join("|", witnessDetails);
        return exact;
    }

    private static bool TryMatchNaturalClearanceCommitments(
        IReadOnlyList<HaulDeliveryItemCommitmentSaveData> commitments,
        IReadOnlyDictionary<string, NaturalClearanceExpectedSlice> expectedByStack,
        out string[] matchedStackIds)
    {
        matchedStackIds = Array.Empty<string>();
        HaulDeliveryItemCommitmentSaveData[] ordered = (commitments
                ?? Array.Empty<HaulDeliveryItemCommitmentSaveData>())
            .OrderBy(value => value?.sourceStackId, StringComparer.Ordinal)
            .ToArray();
        if (ordered.Length == 0
            || ordered.Any(value => value == null)
            || ordered.Select(value => value.sourceStackId)
                .Distinct(StringComparer.Ordinal).Count() != ordered.Length)
        {
            return false;
        }
        if (ordered.Any(value => !expectedByStack.ContainsKey(value.sourceStackId)))
            return false;
        foreach (HaulDeliveryItemCommitmentSaveData commitment in ordered)
        {
            if (!expectedByStack.TryGetValue(
                    commitment.sourceStackId,
                    out NaturalClearanceExpectedSlice expected)
                || !string.Equals(
                    commitment.itemId,
                    expected.ItemId,
                    StringComparison.Ordinal)
                || commitment.quantity <= 0
                || commitment.quantity > expected.Quantity
                || string.IsNullOrWhiteSpace(commitment.carriedStackId))
            {
                return false;
            }
        }
        matchedStackIds = ordered
            .Select(value => value.sourceStackId)
            .ToArray();
        return true;
    }

    private bool VerifySyntheticPreparedOutputPrePickupCancel(
        IWorldItemStackRuntime runtime,
        IFacilityOutputExactRouteOutboxQuery exactRoutes,
        CharacterActor hauler,
        Facility warehouse,
        WorldItemStackSnapshot routedStack,
        FacilityOutputExactRoutePendingSnapshot route,
        int expectedQuantity,
        long expectedMassGrams,
        IItemQuantityReservationService reservations,
        IWarehouseMassAdmissionService warehouseAdmissions)
    {
        AbilityHaul ability = AbilityHaul.Ensure(hauler);
        AIHaul cancelAction = ScriptableObject.CreateInstance<AIHaul>();
        bool hookInvoked = false;
        bool hookPlanExact = false;
        bool hookAuthorityExact = false;
        WorldItemReservedStackQuantity? capturedReservation = null;
        WarehouseHaulAdmissionSaveData capturedAdmission = null;
        string expectedStackSignature = routedStack?.StackSignature ?? string.Empty;
        int heartbeatBefore = ability?.RoutineHeartbeat ?? -1;
        long terminalCountBefore = ability?.RuntimeHaulTerminalCount ?? -1L;
        try
        {
            if (ability == null
                || routedStack == null
                || route?.Receipt == null
                || warehouse?.Inventory == null
                || reservations == null
                || warehouseAdmissions == null
                || !cancelAction.CanStart(hauler))
            {
                Check(false,
                    "PREPARED_OUTPUT_CANARY_PRE_PICKUP_CANCEL_READY",
                    $"ability={ability != null}; stack={routedStack?.StackId ?? "missing"}; "
                        + $"route={route?.Receipt?.RouteOperationId ?? "missing"}; "
                        + $"warehouse={warehouse != null}; reservations="
                        + $"{reservations != null}; admissions="
                        + $"{warehouseAdmissions != null}; canStart="
                        + cancelAction.CanStart(hauler));
                return false;
            }

            Check(true,
                "PREPARED_OUTPUT_CANARY_PRE_PICKUP_CANCEL_READY",
                $"ability=true; stack={routedStack.StackId}; "
                    + $"route={route.Receipt.RouteOperationId}; "
                    + "warehouse=true; reservations=true; admissions=true; canStart=true");

            ability.DebugBeforeHaulRoutineStart = plan =>
            {
                hookInvoked = true;
                capturedReservation = plan?.ReservedStackQuantities?
                    .SingleOrDefault();
                string ownerOperationId = capturedReservation?
                    .OwnerOperationId ?? string.Empty;
                HaulDeliveryIntentSaveData intent = null;
                ItemQuantityLease lease = null;
                WarehouseMassAdmissionStatusSnapshot admissionStatus = default;
                hookPlanExact = plan?.IsValid == true
                    && !plan.IsDeliveryOnlyResume
                    && plan.PickupLegs.Count == 1
                    && plan.DeliveryLegs.Count == 1
                    && plan.ReservedStackQuantities.Count == 1
                    && string.Equals(
                        plan.ReservedStackQuantities[0].StackId,
                        routedStack.StackId,
                        StringComparison.Ordinal)
                    && plan.ReservedStackQuantities[0].Quantity
                        == expectedQuantity
                    && string.Equals(
                        plan.PrimaryDestinationId,
                        route.DeliveryRevision.TargetDestinationId,
                        StringComparison.Ordinal);
                bool leaseExact = hookPlanExact
                    && reservations.TryGetLeasesByOwner(
                        ownerOperationId,
                        out IReadOnlyList<ItemQuantityLease> ownerLeases)
                    && ownerLeases.Count == 1
                    && (lease = ownerLeases[0]) != null
                    && string.Equals(lease.leaseId,
                        capturedReservation.Value.LeaseId, StringComparison.Ordinal)
                    && string.Equals(lease.ownerOperationId,
                        ownerOperationId, StringComparison.Ordinal)
                    && lease.remainingQuantity == expectedQuantity
                    && lease.slices?.Count == 1
                    && string.Equals(lease.slices[0].stackId,
                        routedStack.StackId, StringComparison.Ordinal)
                    && lease.slices[0].quantity == expectedQuantity;
                bool intentExact = hookPlanExact
                    && runtime.TryCaptureHaulDeliveryIntent(
                        ownerOperationId, out intent)
                    && intent != null
                    && !intent.HasCommittedPickup
                    && intent.warehouseAdmissions?.Count == 1
                    && (capturedAdmission = intent.warehouseAdmissions[0]) != null
                    && capturedAdmission.quantity == expectedQuantity
                    && capturedAdmission.reservedMassGrams == expectedMassGrams
                    && warehouseAdmissions.TryGetStatus(
                        capturedAdmission.tokenId, out admissionStatus)
                    && admissionStatus.Status
                        == WarehouseMassAdmissionTokenStatus.Reserved
                    && string.Equals(admissionStatus.Token.TokenId,
                        capturedAdmission.tokenId, StringComparison.Ordinal)
                    && string.Equals(admissionStatus.Token.OwnerOperationId,
                        capturedAdmission.ownerAdmissionOperationId,
                        StringComparison.Ordinal)
                    && admissionStatus.Token.AcceptedQuantity == expectedQuantity
                    && admissionStatus.Token.ReservedMassGrams
                        == expectedMassGrams;
                hookAuthorityExact = leaseExact && intentExact;
                cancelAction.OnStop(
                    hauler,
                    null,
                    "qa-prepared-output-pre-pickup-cancel");
            };
            cancelAction.Execute(hauler);
        }
        finally
        {
            if (ability != null)
                ability.DebugBeforeHaulRoutineStart = null;
            Destroy(cancelAction);
        }

        WorldItemStackSnapshot after = runtime.GetAllStacks().SingleOrDefault(value =>
            value != null
            && string.Equals(value.StackId, routedStack.StackId,
                StringComparison.Ordinal));
        FacilityOutputExactRoutePendingSnapshot afterRoute = exactRoutes
            .CapturePendingRoutes()
            .SingleOrDefault(value => value?.Receipt != null
                && string.Equals(
                    value.Receipt.RouteOperationId,
                    route.Receipt.RouteOperationId,
                    StringComparison.Ordinal));
        int remainingIntentCount = runtime
            .CaptureHaulDeliveryIntentsByDestination(
                route.DeliveryRevision.TargetDestinationId)
            .Count;
        bool ownerLeasesReleased = capturedReservation.HasValue
            && (!reservations.TryGetLeasesByOwner(
                    capturedReservation.Value.OwnerOperationId,
                    out IReadOnlyList<ItemQuantityLease> remainingOwnerLeases)
                || remainingOwnerLeases.Count == 0)
            && !reservations.Revalidate(
                capturedReservation.Value.LeaseId,
                out _,
                out _)
            && reservations.GetReservedQuantity(
                new ItemStackId(routedStack.StackId)) == 0;
        bool intentReleased = capturedReservation.HasValue
            && !runtime.TryCaptureHaulDeliveryIntent(
                capturedReservation.Value.OwnerOperationId,
                out _);
        bool admissionReleased = capturedAdmission != null
            && warehouseAdmissions.TryGetStatus(
                capturedAdmission.tokenId,
                out WarehouseMassAdmissionStatusSnapshot releasedAdmission)
            && releasedAdmission.Status
                == WarehouseMassAdmissionTokenStatus.Released
            && releasedAdmission.ReleaseReason
                == WarehouseMassAdmissionReleaseReason.CancelledBeforePickup
            && string.Equals(releasedAdmission.Token.TokenId,
                capturedAdmission.tokenId, StringComparison.Ordinal)
            && string.Equals(releasedAdmission.Token.OwnerOperationId,
                capturedAdmission.ownerAdmissionOperationId,
                StringComparison.Ordinal)
            && releasedAdmission.Token.AcceptedQuantity == expectedQuantity
            && releasedAdmission.Token.ReservedMassGrams == expectedMassGrams
            && warehouseAdmissions.HasOwnerOperationHistory(
                capturedAdmission.ownerAdmissionOperationId);
        bool routedAuthorityPreserved = after != null
            && after.State == WorldItemStackState.Loose
            && after.Quantity == expectedQuantity
            && after.ReservedQuantity == 0
            && string.IsNullOrWhiteSpace(after.ReservedByPersistentId)
            && string.Equals(after.DestinationId,
                route.DeliveryRevision.TargetDestinationId,
                StringComparison.Ordinal)
            && string.Equals(after.StackSignature,
                expectedStackSignature, StringComparison.Ordinal)
            && afterRoute?.Phase == FacilityOutputExactRoutePhase.Routable
            && string.Equals(
                afterRoute.Receipt.PhysicalReceiptFingerprint,
                route.Receipt.PhysicalReceiptFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                afterRoute.DeliveryRevision.RevisionFingerprint,
                route.DeliveryRevision.RevisionFingerprint,
                StringComparison.Ordinal);
        bool cancelExact = hookInvoked
            && hookPlanExact
            && hookAuthorityExact
            && routedAuthorityPreserved
            && ownerLeasesReleased
            && intentReleased
            && admissionReleased
            && ability != null
            && !ability.IsHauling
            && !ability.HasHaulingRoutineForDiagnostics
            && !ability.HasBoundDeliveryIntent
            && ability.LastInterruptionDisposition
                == HaulInterruptionDisposition
                    .ReleaseUnpickedAndDropCarriedAtActor
            && ability.RoutineHeartbeat == heartbeatBefore
            && ability.RuntimeHaulTerminalCount == terminalCountBefore
            && ability.CaptureActiveHaulOperationIds().Count == 0
            && hauler?.CarryInventory?.HasItems != true
            && remainingIntentCount == 0
            && warehouse.Inventory.ReservedInboundMassGrams == 0L;
        Check(cancelExact,
            "PREPARED_OUTPUT_CANARY_PRE_PICKUP_CANCEL_RELEASES_ONLY_LEASE",
            $"hook={hookInvoked}/{hookPlanExact}/{hookAuthorityExact}; stack={after?.StackId ?? "missing"}:"
                + $"{after?.State}:{after?.Quantity}/{expectedQuantity}:reserved="
                + $"{after?.ReservedQuantity ?? -1}; signature="
                + $"{string.Equals(after?.StackSignature, expectedStackSignature, StringComparison.Ordinal)}; "
                + $"route={afterRoute?.Phase}; intents={remainingIntentCount}; "
                + $"leaseReleased={ownerLeasesReleased}; intentReleased="
                + $"{intentReleased}; admissionReleased={admissionReleased}; "
                + $"activeOps={ability?.CaptureActiveHaulOperationIds().Count ?? -1}; "
                + $"routine={ability?.HasHaulingRoutineForDiagnostics}; heartbeat="
                + $"{heartbeatBefore}->{ability?.RoutineHeartbeat}; terminal="
                + $"{terminalCountBefore}->{ability?.RuntimeHaulTerminalCount}; "
                + $"carry={hauler?.CarryInventory?.Items?.Sum(value => value?.quantity ?? 0) ?? 0}; "
                + $"inbound={warehouse.Inventory.ReservedInboundMassGrams}/0; "
                + $"expectedMass={expectedMassGrams}");
        return cancelExact;
    }

    private bool VerifyPreparedOutputDestinationRevisionDrift(
        DungeonRuntimeLifetimeScope scope,
        IWorldItemStackRuntime runtime,
        CharacterActor hauler,
        Facility warehouse,
        WorldItemStackSnapshot routedStack,
        FacilityOutputExactRoutePendingSnapshot route,
        int expectedQuantity,
        long expectedMassGrams,
        IItemQuantityReservationService reservations,
        IWarehouseMassAdmissionService warehouseAdmissions,
        PreparedOutputDestinationRevisionDriftResult result)
    {
        WorldItemWarehouseService warehouseService =
            Resolve<WorldItemWarehouseService>(scope);
        DungeonItemDefinition outputDefinition = routedStack != null
            && runtime?.CatalogProvider != null
            && runtime.CatalogProvider.TryGetDefinition(
                routedStack.ItemId,
                out DungeonItemDefinition foundOutput)
                ? foundOutput
                : null;
        long remainingMass = warehouse?.Inventory?.RemainingMassGrams ?? 0L;
        long driftCapacity = Math.Max(
            0L,
            checked(remainingMass - expectedMassGrams));
        DungeonItemDefinition driftDefinition = runtime?.CatalogProvider?.All?
            .Where(value => value != null
                && value.MaxStack > 1
                && string.IsNullOrWhiteSpace(value.EquipmentId)
                && outputDefinition != null
                && !string.Equals(
                    value.ItemId,
                    outputDefinition.ItemId,
                    StringComparison.Ordinal)
                && warehouse?.Inventory?.Accepts(value.StockCategory) == true)
            .Select(value => new
            {
                Definition = value,
                UnitMass = runtime.MassQuery.GetDefinitionUnitMass(
                    (ItemDefinitionId)value.ItemId).Value
            })
            .Where(value => value.UnitMass > 0L
                && value.UnitMass <= driftCapacity)
            .OrderBy(value => value.Definition.ItemId, StringComparer.Ordinal)
            .Select(value => value.Definition)
            .FirstOrDefault();
        AIHaul action = ScriptableObject.CreateInstance<AIHaul>();
        AbilityHaul ability = AbilityHaul.Ensure(hauler);
        bool hookInvoked = false;
        bool planExact = false;
        bool revisionAdvanced = false;
        bool typedStaleRejected = false;
        bool sourceUnchangedBeforeCancel = false;
        bool driftIngressCommitted = false;
        string pickupFailure = string.Empty;
        int pickedUp = -1;
        WorldItemReservedStackQuantity? capturedReservation = null;
        WarehouseHaulAdmissionSaveData staleAdmission = null;
        string staleOperationId = string.Empty;
        long staleRevision = 0L;
        long liveRevision = 0L;
        int outputQuantityBefore = runtime?.GetAllStacks()
            .Where(value => value != null
                && routedStack != null
                && string.Equals(
                    value.ItemId,
                    routedStack.ItemId,
                    StringComparison.Ordinal))
            .Sum(value => value.Quantity) ?? 0;
        int driftQuantityBefore = runtime?.GetAllStacks()
            .Where(value => value != null
                && driftDefinition != null
                && string.Equals(
                    value.ItemId,
                    driftDefinition.ItemId,
                    StringComparison.Ordinal))
            .Sum(value => value.Quantity) ?? 0;
        try
        {
            bool ready = ability != null
                && warehouseService != null
                && routedStack != null
                && route?.Receipt != null
                && warehouse?.Inventory?.HasMassCapacityAuthority == true
                && reservations != null
                && warehouseAdmissions != null
                && driftDefinition != null
                && action.CanStart(hauler);
            Check(ready,
                "PREPARED_OUTPUT_CANARY_DESTINATION_REVISION_DRIFT_READY",
                $"ability={ability != null}; service={warehouseService != null}; "
                    + $"stack={routedStack?.StackId ?? "missing"}; route="
                    + $"{route?.Receipt?.RouteOperationId ?? "missing"}; warehouse="
                    + $"{warehouse?.PersistentInstanceId.Value ?? "missing"}; "
                    + $"driftItem={driftDefinition?.ItemId ?? "missing"}; "
                    + $"remainingMass={remainingMass}; driftCapacity={driftCapacity}; canStart="
                    + $"{(ability != null && action.CanStart(hauler))}");
            if (!ready)
                return false;

            ability.DebugBeforeHaulRoutineStart = plan =>
            {
                hookInvoked = true;
                capturedReservation = plan?.ReservedStackQuantities?
                    .SingleOrDefault();
                staleOperationId = capturedReservation?.OwnerOperationId
                    ?? string.Empty;
                HaulDeliveryIntentSaveData intent = null;
                ItemQuantityLease lease = null;
                WarehouseMassAdmissionStatusSnapshot staleStatus = default;
                planExact = plan?.IsValid == true
                    && !plan.IsDeliveryOnlyResume
                    && plan.PickupLegs.Count == 1
                    && plan.DeliveryLegs.Count == 1
                    && capturedReservation.HasValue
                    && string.Equals(
                        capturedReservation.Value.StackId,
                        routedStack.StackId,
                        StringComparison.Ordinal)
                    && capturedReservation.Value.Quantity == expectedQuantity
                    && reservations.TryGetLeasesByOwner(
                        staleOperationId,
                        out IReadOnlyList<ItemQuantityLease> ownerLeases)
                    && ownerLeases.Count == 1
                    && (lease = ownerLeases[0]) != null
                    && runtime.TryCaptureHaulDeliveryIntent(
                        staleOperationId,
                        out intent)
                    && intent?.warehouseAdmissions?.Count == 1
                    && (staleAdmission = intent.warehouseAdmissions[0]) != null
                    && staleAdmission.reservedMassGrams == expectedMassGrams
                    && warehouseAdmissions.TryGetStatus(
                        staleAdmission.tokenId,
                        out staleStatus)
                    && staleStatus.Status
                        == WarehouseMassAdmissionTokenStatus.Reserved;
                if (!planExact)
                    return;

                staleRevision = staleStatus.Token.WarehouseCapacityRevision;
                WorldItemStackSnapshot sourceBefore = runtime.GetAllStacks()
                    .Single(value => value != null
                        && string.Equals(
                            value.StackId,
                            routedStack.StackId,
                            StringComparison.Ordinal));
                string ingressOperationId =
                    "qa:prepared-output:destination-revision-drift";
                driftIngressCommitted = warehouseService.SpawnItemStock(
                    warehouse,
                    driftDefinition.ItemId,
                    1,
                    ingressOperationId,
                    "generic:" + driftDefinition.ItemId,
                    out int driftSpawned,
                    out WarehouseMassAdmissionReceipt driftReceipt,
                    out DomainFailure driftFailure)
                    && driftSpawned == 1
                    && driftReceipt.CommittedQuantity == 1
                    && driftFailure.Code == FailureCode.None;
                liveRevision = warehouseAdmissions.GetWarehouseCapacityRevision(
                    warehouse.PersistentInstanceId);
                revisionAdvanced = driftIngressCommitted
                    && liveRevision > staleRevision;

                bool pickupSucceeded = runtime.TryPickupReservedStackQuantity(
                    hauler,
                    hauler.CarryInventory,
                    capturedReservation.Value,
                    out pickedUp,
                    out pickupFailure);
                typedStaleRejected = !pickupSucceeded
                    && pickedUp == 0
                    && pickupFailure.Contains(
                        "prepared_output_pickup_boundary:DestinationAuthorityStale",
                        StringComparison.Ordinal);
                WorldItemStackSnapshot sourceAfter = runtime.GetAllStacks()
                    .SingleOrDefault(value => value != null
                        && string.Equals(
                            value.StackId,
                            routedStack.StackId,
                            StringComparison.Ordinal));
                sourceUnchangedBeforeCancel = sourceAfter != null
                    && sourceAfter.State == sourceBefore.State
                    && sourceAfter.Quantity == sourceBefore.Quantity
                    && sourceAfter.ReservedQuantity == sourceBefore.ReservedQuantity
                    && string.Equals(
                        sourceAfter.StackSignature,
                        sourceBefore.StackSignature,
                        StringComparison.Ordinal)
                    && string.Equals(
                        sourceAfter.DestinationId,
                        sourceBefore.DestinationId,
                        StringComparison.Ordinal)
                    && hauler.CarryInventory?.HasItems != true;
                action.OnStop(
                    hauler,
                    null,
                    "qa-prepared-output-destination-revision-replan");
            };
            action.Execute(hauler);
        }
        finally
        {
            if (ability != null)
                ability.DebugBeforeHaulRoutineStart = null;
            Destroy(action);
        }

        WorldItemStackSnapshot sourceAfterCancel = runtime?.GetAllStacks()
            .SingleOrDefault(value => value != null
                && routedStack != null
                && string.Equals(
                    value.StackId,
                    routedStack.StackId,
                    StringComparison.Ordinal));
        bool staleAuthorityReleased = staleAdmission != null
            && warehouseAdmissions.TryGetStatus(
                staleAdmission.tokenId,
                out WarehouseMassAdmissionStatusSnapshot releasedStatus)
            && releasedStatus.Status == WarehouseMassAdmissionTokenStatus.Released
            && releasedStatus.ReleaseReason
                == WarehouseMassAdmissionReleaseReason.CancelledBeforePickup;
        bool oldIntentReleased = staleOperationId.Length > 0
            && !runtime.TryCaptureHaulDeliveryIntent(staleOperationId, out _);
        int outputQuantityAfterReject = runtime.GetAllStacks()
            .Where(value => value != null
                && string.Equals(
                    value.ItemId,
                    routedStack.ItemId,
                    StringComparison.Ordinal))
            .Sum(value => value.Quantity);
        int driftQuantityAfterReject = runtime.GetAllStacks()
            .Where(value => value != null
                && string.Equals(
                    value.ItemId,
                    driftDefinition.ItemId,
                    StringComparison.Ordinal))
            .Sum(value => value.Quantity);
        bool exact = hookInvoked
            && planExact
            && driftIngressCommitted
            && revisionAdvanced
            && typedStaleRejected
            && sourceUnchangedBeforeCancel
            && sourceAfterCancel != null
            && sourceAfterCancel.Quantity == expectedQuantity
            && sourceAfterCancel.ReservedQuantity == 0
            && sourceAfterCancel.State == WorldItemStackState.Loose
            && staleAuthorityReleased
            && oldIntentReleased
            && ability != null
            && !ability.IsHauling
            && !ability.HasBoundDeliveryIntent
            && hauler?.CarryInventory?.HasItems != true
            && outputQuantityAfterReject == outputQuantityBefore
            && driftQuantityAfterReject == checked(driftQuantityBefore + 1);
        Check(exact,
            "PREPARED_OUTPUT_CANARY_DESTINATION_REVISION_LIVE_STALE_REJECT",
            $"hook={hookInvoked}; plan={planExact}; operation={staleOperationId}; "
                + $"token={staleAdmission?.tokenId ?? "missing"}; revision="
                + $"{staleRevision}->{liveRevision}; ingress={driftIngressCommitted}; "
                + $"pickup={pickedUp}:{pickupFailure}; unchanged="
                + $"{sourceUnchangedBeforeCancel}; released={staleAuthorityReleased}/"
                + $"{oldIntentReleased}; output={outputQuantityAfterReject}/"
                + $"{outputQuantityBefore}; drift={driftDefinition?.ItemId ?? "missing"}:"
                + $"{driftQuantityAfterReject}/{driftQuantityBefore + 1}");
        if (!exact)
            return false;

        result.Succeeded = true;
        result.StaleOperationId = staleOperationId;
        result.StaleAdmissionTokenId = staleAdmission.tokenId;
        result.StaleAdmissionRevision = staleRevision;
        result.LiveRevisionAfterDrift = liveRevision;
        result.OutputQuantityBefore = outputQuantityBefore;
        result.DriftItemId = driftDefinition.ItemId;
        result.DriftQuantityBefore = driftQuantityBefore;
        result.DriftAddedMassGrams = runtime.MassQuery
            .GetDefinitionUnitMass(
                (ItemDefinitionId)driftDefinition.ItemId)
            .Value;
        return true;
    }

    private static bool TryBuildDeterministicPhysicalBlockerPlan(
        IResourceEconomyContentCatalog content,
        IPhysicalItemMassQuery massQuery,
        IReadOnlyCollection<string> excludedItemIds,
        long targetMassGrams,
        out CapacityBlockerPlanLine[] plan,
        out string candidateDigest,
        out string failure)
    {
        const long maximumSolverTargetGrams = 1_000_000L;
        plan = Array.Empty<CapacityBlockerPlanLine>();
        candidateDigest = string.Empty;
        failure = string.Empty;
        if (content == null || massQuery == null)
        {
            failure = "content-or-mass-authority-missing";
            return false;
        }
        if (targetMassGrams <= 0L
            || targetMassGrams > maximumSolverTargetGrams)
        {
            failure = $"target-out-of-range:{targetMassGrams}:max="
                + maximumSolverTargetGrams;
            return false;
        }

        HashSet<string> excluded = new(
            excludedItemIds ?? Array.Empty<string>(),
            StringComparer.Ordinal);
        Dictionary<long, CapacityBlockerCandidate> firstByUnitMass = new();
        foreach (ResourceItemDefinitionSO definition in (content.Items
                     ?? Array.Empty<ResourceItemDefinitionSO>())
                 .Where(value => value != null)
                 .OrderBy(value => value.ItemId, StringComparer.Ordinal))
        {
            string itemId = definition.ItemId;
            if (string.IsNullOrWhiteSpace(itemId)
                || !string.Equals(itemId, itemId.Trim(), StringComparison.Ordinal)
                || excluded.Contains(itemId)
                || PhysicalItemIds.TryGetEquipmentDefinitionId(itemId, out _)
                || PhysicalItemIds.IsEquipmentModule(itemId)
                || definition.Features?.Any(value =>
                    value?.RequiresProductionOutputInstanceState == true) == true)
            {
                continue;
            }

            ItemDefinitionId typedItemId = (ItemDefinitionId)itemId;
            PhysicalItemMassSubject subject;
            long unitMassGrams;
            try
            {
                subject = PhysicalItemMassSubjectAdapter.Create(
                    massQuery,
                    typedItemId,
                    string.Empty,
                    Array.Empty<ItemInstanceComponentSaveData>());
                unitMassGrams = massQuery.GetDefinitionUnitMass(typedItemId).Value;
                if (subject.Kind != PhysicalItemMassSubjectKind.GenericDefinition
                    || unitMassGrams <= 0L
                    || unitMassGrams > targetMassGrams
                    || massQuery.GetQuantityMass(typedItemId, subject, 1).Value
                        != unitMassGrams
                    || massQuery.GetQuantityMass(typedItemId, subject, 2).Value
                        != checked(unitMassGrams * 2L))
                {
                    continue;
                }
            }
            catch (Exception)
            {
                continue;
            }

            if (!firstByUnitMass.ContainsKey(unitMassGrams))
            {
                firstByUnitMass.Add(
                    unitMassGrams,
                    new CapacityBlockerCandidate(
                        itemId,
                        unitMassGrams,
                        definition.MaxStack));
            }
        }

        CapacityBlockerCandidate[] candidates = firstByUnitMass.Values
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        candidateDigest = ComputeTextSha256(string.Join(
            "\n",
            candidates.Select(value =>
                $"{value.ItemId}|{value.UnitMassGrams}|{value.MaxStack}")));
        if (candidates.Length == 0)
        {
            failure = "no-generic-linear-mass-candidates";
            return false;
        }

        long candidateGcd = candidates[0].UnitMassGrams;
        for (int index = 1; index < candidates.Length; index++)
        {
            candidateGcd = GreatestCommonDivisor(
                candidateGcd,
                candidates[index].UnitMassGrams);
        }
        if (targetMassGrams % candidateGcd != 0L)
        {
            failure = $"exact-mass-unrepresentable:gcd={candidateGcd}:target="
                + targetMassGrams;
            return false;
        }

        int target = checked((int)targetMassGrams);
        int[] minimumItemCount = Enumerable.Repeat(int.MaxValue, target + 1)
            .ToArray();
        int[] previousMass = Enumerable.Repeat(-1, target + 1).ToArray();
        int[] previousCandidate = Enumerable.Repeat(-1, target + 1).ToArray();
        minimumItemCount[0] = 0;
        for (int mass = 1; mass <= target; mass++)
        {
            for (int candidateIndex = 0;
                 candidateIndex < candidates.Length;
                 candidateIndex++)
            {
                int unitMass = checked((int)candidates[candidateIndex]
                    .UnitMassGrams);
                int priorMass = mass - unitMass;
                if (priorMass < 0 || minimumItemCount[priorMass] == int.MaxValue)
                    continue;
                int proposedCount = minimumItemCount[priorMass] + 1;
                if (proposedCount < minimumItemCount[mass]
                    || proposedCount == minimumItemCount[mass]
                    && (previousCandidate[mass] < 0
                        || candidateIndex < previousCandidate[mass]))
                {
                    minimumItemCount[mass] = proposedCount;
                    previousMass[mass] = priorMass;
                    previousCandidate[mass] = candidateIndex;
                }
            }
        }

        if (minimumItemCount[target] == int.MaxValue)
        {
            failure = $"exact-mass-unrepresentable:finite-target={targetMassGrams}:"
                + $"gcd={candidateGcd}:candidates={candidates.Length}:authority="
                + massQuery.AuthorityRevision;
            return false;
        }

        int[] quantities = new int[candidates.Length];
        int cursor = target;
        while (cursor > 0)
        {
            int candidateIndex = previousCandidate[cursor];
            int next = previousMass[cursor];
            if (candidateIndex < 0 || next < 0 || next >= cursor)
            {
                failure = $"solver-predecessor-invalid:cursor={cursor}:candidate="
                    + $"{candidateIndex}:next={next}";
                return false;
            }
            quantities[candidateIndex] = checked(quantities[candidateIndex] + 1);
            cursor = next;
        }

        plan = candidates
            .Select((value, index) => new CapacityBlockerPlanLine(
                value.ItemId,
                value.UnitMassGrams,
                quantities[index],
                value.MaxStack))
            .Where(value => value.Quantity > 0)
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .ToArray();
        long plannedMass = plan.Sum(value => checked(
            value.UnitMassGrams * value.Quantity));
        if (plannedMass != targetMassGrams)
        {
            failure = $"solver-mass-mismatch:{plannedMass}/{targetMassGrams}";
            plan = Array.Empty<CapacityBlockerPlanLine>();
            return false;
        }
        return true;
    }

    private static long GreatestCommonDivisor(long left, long right)
    {
        left = Math.Abs(left);
        right = Math.Abs(right);
        while (right != 0L)
        {
            long remainder = left % right;
            left = right;
            right = remainder;
        }
        return left;
    }

    private static long CapturePhysicalStackMassGrams(
        IPhysicalItemMassQuery massQuery,
        WorldItemStackSnapshot stack)
    {
        if (massQuery == null || stack == null || stack.Quantity <= 0)
            return 0L;
        ItemDefinitionId itemId = (ItemDefinitionId)stack.ItemId;
        PhysicalItemMassSubject subject = PhysicalItemMassSubjectAdapter.Create(
            massQuery,
            itemId,
            stack.ItemInstanceId,
            stack.Components);
        return massQuery.GetQuantityMass(itemId, subject, stack.Quantity).Value;
    }

    private readonly struct CapacityBlockerCandidate
    {
        public CapacityBlockerCandidate(
            string itemId,
            long unitMassGrams,
            int maxStack)
        {
            ItemId = itemId ?? string.Empty;
            UnitMassGrams = unitMassGrams;
            MaxStack = maxStack;
        }

        public string ItemId { get; }
        public long UnitMassGrams { get; }
        public int MaxStack { get; }
    }

    private readonly struct CapacityBlockerPlanLine
    {
        public CapacityBlockerPlanLine(
            string itemId,
            long unitMassGrams,
            int quantity,
            int maxStack)
        {
            ItemId = itemId ?? string.Empty;
            UnitMassGrams = unitMassGrams;
            Quantity = quantity;
            MaxStack = maxStack;
        }

        public string ItemId { get; }
        public long UnitMassGrams { get; }
        public int Quantity { get; }
        public int MaxStack { get; }
    }

    private ProductionWorkExecutionResult VerifySyntheticPreparedOutputCapacityWait(
        DungeonRuntimeLifetimeScope scope,
        IDungeonSaveSectionRegistry saveRegistry,
        string warehouseOwnerId,
        IResourceEconomyContentCatalog content,
        IWorldItemStackRuntime runtime,
        IProductionBillQuery bills,
        IProductionBillWorkExecution work,
        IProductionBillPersistence persistence,
        IFacilityBufferMassCapacityQuery capacities,
        IFacilityBufferPhysicalOccupancyQuery occupancy,
        IPhysicalItemBatchDispositionService dispositions,
        CharacterActor worker,
        Facility facility,
        ProductionRecipeSO recipe,
        ProductionBillSnapshot bill,
        ProductionOutputDefinition expectedOutput,
        long expectedBatchMassGrams,
        long expectedFacilityCapacityGrams,
        out bool succeeded,
        out CharacterActor activeWorker,
        out Facility activeFacility,
        out Facility activeWarehouse,
        out ProductionBillSnapshot activeBill)
    {
        succeeded = false;
        activeWorker = worker;
        activeFacility = facility;
        activeWarehouse = null;
        activeBill = bill;
        if (scope == null
            || saveRegistry == null
            || string.IsNullOrWhiteSpace(warehouseOwnerId)
            || content == null
            || runtime == null
            || bills == null
            || work == null
            || persistence == null
            || capacities == null
            || occupancy == null
            || dispositions == null
            || worker == null
            || facility == null
            || recipe == null
            || bill == null
            || expectedOutput == null)
        {
            Check(false,
                "PREPARED_OUTPUT_CANARY_CAPACITY_WAIT_AUTHORITIES_READY",
                "one or more required live authorities are missing");
            return default;
        }

        ProductionBillSaveData wipBefore = persistence.Capture().bills
            .SingleOrDefault(value => value != null
                && string.Equals(value.billId, bill.BillId.Value,
                    StringComparison.Ordinal));
        int expectedWipInputQuantity = 0;
        long expectedWipInputMassGrams = 0L;
        HashSet<string> expectedInputItemIds = new(StringComparer.Ordinal);
        foreach (ItemAmountDefinition input in bill.Inputs
                     ?? Array.Empty<ItemAmountDefinition>())
        {
            expectedInputItemIds.Add(input.ItemId);
            expectedWipInputQuantity = checked(
                expectedWipInputQuantity + input.Amount);
            expectedWipInputMassGrams = checked(
                expectedWipInputMassGrams
                + runtime.MassQuery.GetDefinitionUnitMass(
                    (ItemDefinitionId)input.ItemId).Value * input.Amount);
        }
        int inputQuantityBefore = runtime.GetAllStacks()
            .Where(value => value != null
                && value.State == WorldItemStackState.FacilityBuffer
                && string.Equals(value.DestinationId, bill.MaterialDestinationId,
                    StringComparison.Ordinal))
            .Sum(value => value.Quantity);
        int allWorldInputQuantityBefore = runtime.GetAllStacks()
            .Where(value => value != null
                && expectedInputItemIds.Contains(value.ItemId))
            .Sum(value => value.Quantity);
        bool wipReady = wipBefore?.materialsConsumed == true
            && !string.IsNullOrWhiteSpace(wipBefore.wipInputCommitId)
            && expectedWipInputQuantity > 0
            && expectedWipInputMassGrams > 0L
            && wipBefore.wipInputQuantity == expectedWipInputQuantity
            && wipBefore.wipInputMassGrams == expectedWipInputMassGrams
            && inputQuantityBefore == 0
            && allWorldInputQuantityBefore == 0;
        Check(wipReady,
            "PREPARED_OUTPUT_CANARY_CAPACITY_WAIT_WIP_CAPTURED",
            $"materials={wipBefore?.materialsConsumed}; commit="
                + $"{wipBefore?.wipInputCommitId ?? "missing"}; quantity="
                + $"{wipBefore?.wipInputQuantity ?? 0}/{expectedWipInputQuantity}; mass="
                + $"{wipBefore?.wipInputMassGrams ?? 0}/{expectedWipInputMassGrams}; "
                + $"inputBuffer={inputQuantityBefore}; allWorldInput="
                + allWorldInputQuantityBefore);
        if (!wipReady)
            return default;

        FacilityBufferPhysicalOccupancySnapshot initialOccupancy =
            occupancy.Capture(bill.OutputDestinationId);
        FacilityBufferMassCapacitySnapshot initialCapacity = default;
        bool initialCapacityExact = capacities.TryGetCapacity(
                bill.OutputDestinationId,
                facility.centerPos,
                out initialCapacity)
            && initialCapacity.Profile?.MaxMassGrams
                == expectedFacilityCapacityGrams
            && initialCapacity.ReservedMassGrams == 0L
            && initialOccupancy.TotalMassGrams == 0L;
        Check(initialCapacityExact,
            "PREPARED_OUTPUT_CANARY_CAPACITY_WAIT_INITIAL_EMPTY",
            $"occupancy={initialOccupancy.TotalMassGrams}; max="
                + $"{initialCapacity.Profile?.MaxMassGrams ?? 0}; reserved="
                + initialCapacity.ReservedMassGrams);
        if (!initialCapacityExact)
            return default;

        long blockerMassGrams = checked(
            initialCapacity.Profile.MaxMassGrams - expectedBatchMassGrams + 1L);
        HashSet<string> blockerExcludedItemIds = new(expectedInputItemIds,
            StringComparer.Ordinal)
        {
            expectedOutput.ItemId
        };
        bool blockerPlanReady = TryBuildDeterministicPhysicalBlockerPlan(
            content,
            runtime.MassQuery,
            blockerExcludedItemIds,
            blockerMassGrams,
            out CapacityBlockerPlanLine[] blockerPlan,
            out string blockerCandidateDigest,
            out string blockerPlanFailure);
        int blockerQuantity = blockerPlanReady
            ? blockerPlan.Sum(value => value.Quantity)
            : 0;
        Check(blockerPlanReady,
            "PREPARED_OUTPUT_CANARY_CAPACITY_BLOCKER_PLAN_EXACT",
            blockerPlanReady
                ? $"target={blockerMassGrams}; candidates={blockerCandidateDigest}; plan="
                    + string.Join(",", blockerPlan.Select(value =>
                        $"{value.ItemId}x{value.Quantity}@{value.UnitMassGrams}g"))
                : $"target={blockerMassGrams}; digest={blockerCandidateDigest}; "
                    + $"failure={blockerPlanFailure}");
        if (!blockerPlanReady)
            return default;

        HashSet<string> destinationStackIdsBefore = runtime.GetAllStacks()
            .Where(value => value != null
                && string.Equals(value.DestinationId, bill.OutputDestinationId,
                    StringComparison.Ordinal))
            .Select(value => value.StackId)
            .ToHashSet(StringComparer.Ordinal);
        bool blockerSpawned = true;
        List<string> blockerSpawnResults = new();
        foreach (CapacityBlockerPlanLine line in blockerPlan)
        {
            bool spawned = runtime.SpawnItemAt(
                line.ItemId,
                line.Quantity,
                facility.centerPos,
                WorldItemStackState.FacilityOutputBuffer,
                bill.OutputDestinationId,
                out int spawnedQuantity);
            blockerSpawned &= spawned && spawnedQuantity == line.Quantity;
            blockerSpawnResults.Add(
                $"{line.ItemId}:{spawnedQuantity}/{line.Quantity}:{spawned}");
        }
        WorldItemStackSnapshot[] blockerStacks = runtime.GetAllStacks()
            .Where(value => value != null
                && !destinationStackIdsBefore.Contains(value.StackId)
                && value.State == WorldItemStackState.FacilityOutputBuffer
                && string.Equals(value.DestinationId, bill.OutputDestinationId,
                    StringComparison.Ordinal))
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        long actualBlockerMassGrams = blockerStacks.Sum(value =>
            CapturePhysicalStackMassGrams(runtime.MassQuery, value));
        FacilityBufferPhysicalOccupancySnapshot blockedOccupancy =
            occupancy.Capture(bill.OutputDestinationId);
        FacilityBufferMassCapacitySnapshot blockedCapacity = default;
        bool blockedCapacityCaptured = capacities.TryGetCapacity(
            bill.OutputDestinationId,
            facility.centerPos,
            out blockedCapacity);
        long blockedFreeMassGrams = blockedCapacityCaptured
            && blockedCapacity.Profile != null
                ? checked(blockedCapacity.Profile.MaxMassGrams
                    - blockedOccupancy.TotalMassGrams
                    - blockedCapacity.ReservedMassGrams)
                : long.MinValue;
        long blockedShortageGrams = blockedFreeMassGrams >= 0L
            ? checked(expectedBatchMassGrams - blockedFreeMassGrams)
            : long.MinValue;
        bool blockerExact = blockerSpawned
            && blockerStacks.Sum(value => value.Quantity) == blockerQuantity
            && actualBlockerMassGrams == blockerMassGrams
            && blockedOccupancy.NonCarriedMassGrams == blockerMassGrams
            && blockedOccupancy.CommittedCarriedMassGrams == 0L
            && blockedCapacityCaptured
            && blockedCapacity.Profile?.MaxMassGrams
                == expectedFacilityCapacityGrams
            && blockedCapacity.ReservedMassGrams == 0L
            && blockedFreeMassGrams == expectedBatchMassGrams - 1L
            && blockedShortageGrams == 1L;
        Check(blockerExact,
            "PREPARED_OUTPUT_CANARY_CAPACITY_BLOCKER_EXACT_PHYSICAL",
            $"spawned={string.Join(",", blockerSpawnResults)}; stacks="
                + $"{blockerStacks.Length}; quantity="
                + $"{blockerStacks.Sum(value => value.Quantity)}/{blockerQuantity}; "
                + $"mass={actualBlockerMassGrams}/{blockerMassGrams}; occupancy="
                + $"{blockedOccupancy.NonCarriedMassGrams}/"
                + $"{blockedOccupancy.CommittedCarriedMassGrams}; capacity="
                + $"{blockedCapacity.Profile?.MaxMassGrams ?? 0}; reserved="
                + $"{blockedCapacity.ReservedMassGrams}; free={blockedFreeMassGrams}; "
                + $"shortage={blockedShortageGrams}");
        if (!blockerExact)
            return default;

        int itemVersionBeforeWait = runtime.ItemStackVersion;
        ProductionWorkExecutionResult firstWait = work.ExecuteWork(
            worker,
            facility,
            bill.BillId,
            recipe.RequiredWork + 1f);
        ProductionBillSnapshot waitingBill = bills.GetBills(facility)
            .SingleOrDefault(value => value.BillId == bill.BillId);
        ProductionBillSaveData waitingSave = persistence.Capture().bills
            .SingleOrDefault(value => value != null
                && string.Equals(value.billId, bill.BillId.Value,
                    StringComparison.Ordinal));
        ProductionPreparedOutputBatchSaveData frozenPrepared =
            waitingSave?.preparedOutput?.Clone();
        string frozenPreparedJson = frozenPrepared == null
            ? string.Empty
            : JsonUtility.ToJson(frozenPrepared);
        string frozenBillJson = waitingSave == null
            ? string.Empty
            : JsonUtility.ToJson(waitingSave);
        FacilityBufferPhysicalOccupancySnapshot occupancyAfterFirstWait =
            occupancy.Capture(bill.OutputDestinationId);
        FacilityBufferMassCapacitySnapshot capacityAfterFirstWait = default;
        bool firstWaitCapacityCaptured = capacities.TryGetCapacity(
            bill.OutputDestinationId,
            facility.centerPos,
            out capacityAfterFirstWait);
        long freeMassAfterFirstWait = firstWaitCapacityCaptured
            && capacityAfterFirstWait.Profile != null
                ? checked(capacityAfterFirstWait.Profile.MaxMassGrams
                    - occupancyAfterFirstWait.TotalMassGrams
                    - capacityAfterFirstWait.ReservedMassGrams)
                : long.MinValue;
        int preparedAuthorityStackCount = runtime.GetAllStacks().Count(value =>
            value != null
            && string.Equals(value.DestinationId, bill.OutputDestinationId,
                StringComparison.Ordinal)
            && HasAnyPreparedOutputAuthority(value.Components));
        bool firstWaitExact = !firstWait.Succeeded
            && !firstWait.CycleCompleted
            && firstWait.Failure.Code == FailureCode.ProductionOutputSpaceUnavailable
            && waitingBill?.Status == ProductionBillStatus.WaitingForOutputSpace
            && waitingSave?.blocked?.code
                == FailureCode.ProductionOutputSpaceUnavailable
            && frozenPrepared?.phase
                == ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace
            && frozenPrepared.totalPhysicalMassGrams == expectedBatchMassGrams
            && !string.IsNullOrWhiteSpace(frozenPrepared.batchCommitId)
            && !string.IsNullOrWhiteSpace(frozenPrepared.outcomeFingerprint)
            && waitingSave.materialsConsumed
            && string.Equals(waitingSave.wipInputCommitId,
                wipBefore.wipInputCommitId, StringComparison.Ordinal)
            && waitingSave.wipInputQuantity == wipBefore.wipInputQuantity
            && waitingSave.wipInputMassGrams == wipBefore.wipInputMassGrams
            && waitingSave.cycleSequence == wipBefore.cycleSequence
            && runtime.ItemStackVersion == itemVersionBeforeWait
            && firstWaitCapacityCaptured
            && capacityAfterFirstWait.Profile?.MaxMassGrams
                == expectedFacilityCapacityGrams
            && capacityAfterFirstWait.ReservedMassGrams == 0L
            && occupancyAfterFirstWait.TotalMassGrams == blockerMassGrams
            && freeMassAfterFirstWait == expectedBatchMassGrams - 1L
            && preparedAuthorityStackCount == 0;
        Check(firstWaitExact,
            "PREPARED_OUTPUT_CANARY_CAPACITY_WAIT_EXACT_1G_SHORT",
            $"result={firstWait.Succeeded}/{firstWait.CycleCompleted}:"
                + $"{firstWait.Failure.Code}; status={waitingBill?.Status}; "
                + $"phase={frozenPrepared?.phase}; batch="
                + $"{frozenPrepared?.batchCommitId ?? "missing"}; outcome="
                + $"{frozenPrepared?.outcomeFingerprint ?? "missing"}; "
                + $"mass={frozenPrepared?.totalPhysicalMassGrams ?? 0}; "
                + $"itemVersion={itemVersionBeforeWait}->{runtime.ItemStackVersion}; "
                + $"capacity={capacityAfterFirstWait.Profile?.MaxMassGrams ?? 0}; "
                + $"reserved={capacityAfterFirstWait.ReservedMassGrams}; "
                + $"free={freeMassAfterFirstWait}; authority="
                + preparedAuthorityStackCount);
        if (!firstWaitExact)
            return firstWait;

        string workerPersistentId = worker.BuildingCharacterId.Value;
        string facilityPersistentId = facility.PersistentInstanceId.Value;
        string billPersistentId = bill.BillId.Value;
        string outputDestinationId = bill.OutputDestinationId;
        string materialDestinationId = bill.MaterialDestinationId;
        Vector2Int facilityPosition = facility.centerPos;
        string[] blockerStackIds = blockerStacks
            .Select(value => value.StackId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        Dictionary<string, string> blockerSignatures = blockerStacks.ToDictionary(
            value => value.StackId,
            value => value.StackSignature,
            StringComparer.Ordinal);
        Dictionary<string, int> blockerQuantities = blockerStacks.ToDictionary(
            value => value.StackId,
            value => value.Quantity,
            StringComparer.Ordinal);
        List<DungeonSaveSectionEnvelope> waitingCheckpoint = saveRegistry.CaptureAll();
        string waitingRootFingerprint = CaptureWholeRootSaveFingerprint(
            waitingCheckpoint);
        DungeonGameRestoreReport waitingRestoreReport = new();
        bool waitingRestored = saveRegistry.RestoreAll(
                waitingCheckpoint,
                waitingRestoreReport)
            && waitingRestoreReport.Success;
        ICharacterAiWorldRegistry restoredWorld = Resolve<ICharacterAiWorldRegistry>(
            scope);
        PauseWorldActorsForCanary(restoredWorld);
        CharacterActor restoredWorker = restoredWorld?.Characters.SingleOrDefault(
            value => value != null
                && string.Equals(
                    value.BuildingCharacterId.Value,
                    workerPersistentId,
                    StringComparison.Ordinal));
        Facility restoredFacility = restoredWorld?.Buildings
            .OfType<Facility>()
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.PersistentInstanceId.Value,
                    facilityPersistentId,
                    StringComparison.Ordinal));
        Facility restoredWarehouse = restoredWorld?.Warehouses
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.PersistentInstanceId.Value,
                    warehouseOwnerId,
                    StringComparison.Ordinal)) as Facility;
        ProductionBillSnapshot restoredBill = restoredFacility != null
            ? bills.GetBills(restoredFacility).SingleOrDefault(value =>
                value.BillId.Value == billPersistentId)
            : null;
        ProductionBillSaveData restoredSave = persistence.Capture().bills
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.billId,
                    billPersistentId,
                    StringComparison.Ordinal));
        string restoredBillJson = restoredSave == null
            ? string.Empty
            : JsonUtility.ToJson(restoredSave);
        string restoredPreparedJson = restoredSave?.preparedOutput == null
            ? string.Empty
            : JsonUtility.ToJson(restoredSave.preparedOutput);
        WorldItemStackSnapshot[] restoredBlockerStacks = runtime.GetAllStacks()
            .Where(value => value != null
                && blockerStackIds.Contains(value.StackId, StringComparer.Ordinal))
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        bool blockerRowsExact = restoredBlockerStacks.Length
                == blockerStackIds.Length
            && restoredBlockerStacks.All(value =>
                value.State == WorldItemStackState.FacilityOutputBuffer
                && string.Equals(
                    value.DestinationId,
                    outputDestinationId,
                    StringComparison.Ordinal)
                && blockerSignatures.TryGetValue(
                    value.StackId,
                    out string expectedSignature)
                && string.Equals(
                    value.StackSignature,
                    expectedSignature,
                    StringComparison.Ordinal)
                && blockerQuantities.TryGetValue(
                    value.StackId,
                    out int expectedQuantity)
                && value.Quantity == expectedQuantity);
        long restoredBlockerMassGrams = restoredBlockerStacks.Sum(value =>
            CapturePhysicalStackMassGrams(runtime.MassQuery, value));
        FacilityBufferPhysicalOccupancySnapshot restoredOccupancy =
            occupancy.Capture(outputDestinationId);
        FacilityBufferMassCapacitySnapshot restoredCapacity = default;
        bool restoredCapacityCaptured = restoredFacility != null
            && capacities.TryGetCapacity(
                outputDestinationId,
                restoredFacility.centerPos,
                out restoredCapacity);
        long restoredFreeMassGrams = restoredCapacityCaptured
            && restoredCapacity.Profile != null
                ? checked(restoredCapacity.Profile.MaxMassGrams
                    - restoredOccupancy.TotalMassGrams
                    - restoredCapacity.ReservedMassGrams)
                : long.MinValue;
        int restoredInputQuantity = runtime.GetAllStacks()
            .Where(value => value != null
                && string.Equals(
                    value.DestinationId,
                    materialDestinationId,
                    StringComparison.Ordinal))
            .Sum(value => value.Quantity);
        int restoredAllWorldInputQuantity = runtime.GetAllStacks()
            .Where(value => value != null
                && expectedInputItemIds.Contains(value.ItemId))
            .Sum(value => value.Quantity);
        int restoredPreparedAuthorityCount = runtime.GetAllStacks().Count(value =>
            value != null
            && string.Equals(
                value.DestinationId,
                outputDestinationId,
                StringComparison.Ordinal)
            && HasAnyPreparedOutputAuthority(value.Components));
        string restoredRootFingerprint = waitingRestored
            ? CaptureWholeRootSaveFingerprint(saveRegistry.CaptureAll())
            : string.Empty;
        bool waitingRestoreExact = waitingRestored
            && restoredWorker != null
            && restoredFacility != null
            && restoredWarehouse?.Inventory != null
            && restoredBill?.Status == ProductionBillStatus.WaitingForOutputSpace
            && restoredFacility.centerPos == facilityPosition
            && string.Equals(
                restoredBill.OutputDestinationId,
                outputDestinationId,
                StringComparison.Ordinal)
            && string.Equals(
                restoredBill.MaterialDestinationId,
                materialDestinationId,
                StringComparison.Ordinal)
            && string.Equals(
                restoredBillJson,
                frozenBillJson,
                StringComparison.Ordinal)
            && string.Equals(
                restoredPreparedJson,
                frozenPreparedJson,
                StringComparison.Ordinal)
            && restoredSave?.materialsConsumed == true
            && string.Equals(
                restoredSave.wipInputCommitId,
                wipBefore.wipInputCommitId,
                StringComparison.Ordinal)
            && restoredSave.wipInputQuantity == expectedWipInputQuantity
            && restoredSave.wipInputMassGrams == expectedWipInputMassGrams
            && restoredSave.cycleSequence == wipBefore.cycleSequence
            && blockerRowsExact
            && restoredBlockerMassGrams == blockerMassGrams
            && restoredOccupancy.NonCarriedMassGrams == blockerMassGrams
            && restoredOccupancy.CommittedCarriedMassGrams == 0L
            && restoredCapacityCaptured
            && restoredCapacity.Profile?.MaxMassGrams
                == expectedFacilityCapacityGrams
            && restoredCapacity.ReservedMassGrams == 0L
            && restoredFreeMassGrams == expectedBatchMassGrams - 1L
            && restoredInputQuantity == 0
            && restoredAllWorldInputQuantity == 0
            && restoredPreparedAuthorityCount == 0
            && string.Equals(
                waitingRootFingerprint,
                restoredRootFingerprint,
                StringComparison.Ordinal);
        Check(waitingRestoreExact,
            "PREPARED_OUTPUT_CANARY_CAPACITY_WAIT_RESTORE_EXACT",
            $"restored={waitingRestored}; worker={restoredWorker?.BuildingCharacterId.Value ?? "missing"}; "
                + $"facility={restoredFacility?.PersistentInstanceId.Value ?? "missing"}; "
                + $"warehouse={restoredWarehouse?.PersistentInstanceId.Value ?? "missing"}; "
                + $"bill={restoredBill?.BillId.Value ?? "missing"}:{restoredBill?.Status}; "
                + $"billExact={string.Equals(restoredBillJson, frozenBillJson, StringComparison.Ordinal)}; "
                + $"preparedExact={string.Equals(restoredPreparedJson, frozenPreparedJson, StringComparison.Ordinal)}; "
                + $"blockers={restoredBlockerStacks.Length}/{blockerStackIds.Length}:"
                + $"{restoredBlockerMassGrams}/{blockerMassGrams}; occupancy="
                + $"{restoredOccupancy.TotalMassGrams}; capacity="
                + $"{restoredCapacity.Profile?.MaxMassGrams ?? 0}; reserved="
                + $"{restoredCapacity.ReservedMassGrams}; free={restoredFreeMassGrams}; "
                + $"input={restoredInputQuantity}/{restoredAllWorldInputQuantity}; authority="
                + $"{restoredPreparedAuthorityCount}; rootExact="
                + $"{string.Equals(waitingRootFingerprint, restoredRootFingerprint, StringComparison.Ordinal)}; "
                + $"errors={string.Join(" | ", waitingRestoreReport.Errors)}");
        if (!waitingRestoreExact)
            return firstWait;

        restoredWorker.SetAiPaused(true);
        worker = restoredWorker;
        facility = restoredFacility;
        activeWorker = restoredWorker;
        activeFacility = restoredFacility;
        activeWarehouse = restoredWarehouse;
        bill = restoredBill;
        activeBill = restoredBill;
        blockerStacks = restoredBlockerStacks;

        FacilityBufferPhysicalOccupancySnapshot occupancyBeforeRetry =
            occupancy.Capture(bill.OutputDestinationId);
        int itemVersionBeforeRetry = runtime.ItemStackVersion;
        ProductionWorkExecutionResult secondWait = work.ExecuteWork(
            worker,
            facility,
            bill.BillId,
            0f);
        ProductionBillSaveData retrySave = persistence.Capture().bills
            .SingleOrDefault(value => value != null
                && string.Equals(value.billId, bill.BillId.Value,
                    StringComparison.Ordinal));
        ProductionBillSnapshot retryBill = bills.GetBills(facility)
            .SingleOrDefault(value => value.BillId == bill.BillId);
        FacilityBufferPhysicalOccupancySnapshot occupancyAfterRetry =
            occupancy.Capture(bill.OutputDestinationId);
        string retryPreparedJson = retrySave?.preparedOutput == null
            ? string.Empty
            : JsonUtility.ToJson(retrySave.preparedOutput);
        string retryBillJson = retrySave == null
            ? string.Empty
            : JsonUtility.ToJson(retrySave);
        int inputQuantityAfterRetry = runtime.GetAllStacks()
            .Where(value => value != null
                && value.State == WorldItemStackState.FacilityBuffer
                && string.Equals(value.DestinationId, bill.MaterialDestinationId,
                    StringComparison.Ordinal))
            .Sum(value => value.Quantity);
        int allWorldInputQuantityAfterRetry = runtime.GetAllStacks()
            .Where(value => value != null
                && expectedInputItemIds.Contains(value.ItemId))
            .Sum(value => value.Quantity);
        int authorityAfterRetry = runtime.GetAllStacks().Count(value =>
            value != null
            && string.Equals(value.DestinationId, bill.OutputDestinationId,
                StringComparison.Ordinal)
            && HasAnyPreparedOutputAuthority(value.Components));
        FacilityBufferMassCapacitySnapshot capacityAfterRetry = default;
        bool retryCapacityCaptured = capacities.TryGetCapacity(
            bill.OutputDestinationId,
            facility.centerPos,
            out capacityAfterRetry);
        long freeMassAfterRetry = retryCapacityCaptured
            && capacityAfterRetry.Profile != null
                ? checked(capacityAfterRetry.Profile.MaxMassGrams
                    - occupancyAfterRetry.TotalMassGrams
                    - capacityAfterRetry.ReservedMassGrams)
                : long.MinValue;
        bool retryExact = !secondWait.Succeeded
            && !secondWait.CycleCompleted
            && secondWait.Failure.Code
                == FailureCode.ProductionOutputSpaceUnavailable
            && retryBill?.Status == ProductionBillStatus.WaitingForOutputSpace
            && string.Equals(frozenPreparedJson, retryPreparedJson,
                StringComparison.Ordinal)
            && string.Equals(frozenBillJson, retryBillJson,
                StringComparison.Ordinal)
            && retrySave?.materialsConsumed == true
            && string.Equals(retrySave.wipInputCommitId,
                wipBefore.wipInputCommitId, StringComparison.Ordinal)
            && retrySave.wipInputQuantity == wipBefore.wipInputQuantity
            && retrySave.wipInputMassGrams == wipBefore.wipInputMassGrams
            && retrySave.cycleSequence == wipBefore.cycleSequence
            && runtime.ItemStackVersion == itemVersionBeforeRetry
            && occupancyAfterRetry.NonCarriedMassGrams
                == occupancyBeforeRetry.NonCarriedMassGrams
            && occupancyAfterRetry.CommittedCarriedMassGrams
                == occupancyBeforeRetry.CommittedCarriedMassGrams
            && inputQuantityAfterRetry == 0
            && allWorldInputQuantityAfterRetry == 0
            && retryCapacityCaptured
            && capacityAfterRetry.Profile?.MaxMassGrams
                == expectedFacilityCapacityGrams
            && capacityAfterRetry.ReservedMassGrams == 0L
            && freeMassAfterRetry == expectedBatchMassGrams - 1L
            && authorityAfterRetry == 0;
        Check(retryExact,
            "PREPARED_OUTPUT_CANARY_CAPACITY_WAIT_RETRY_MUTATION_ZERO",
            $"result={secondWait.Succeeded}/{secondWait.CycleCompleted}:"
                + $"{secondWait.Failure.Code}; status={retryBill?.Status}; "
                + $"preparedExact={string.Equals(frozenPreparedJson, retryPreparedJson, StringComparison.Ordinal)}; "
                + $"billExact={string.Equals(frozenBillJson, retryBillJson, StringComparison.Ordinal)}; "
                + $"wip={retrySave?.wipInputCommitId ?? "missing"}:"
                + $"{retrySave?.wipInputQuantity ?? 0}:"
                + $"{retrySave?.wipInputMassGrams ?? 0}; inputBuffer="
                + $"{inputQuantityAfterRetry}; itemVersion="
                + $"{itemVersionBeforeRetry}->{runtime.ItemStackVersion}; "
                + $"allWorldInput={allWorldInputQuantityAfterRetry}; occupancy="
                + $"{occupancyBeforeRetry.TotalMassGrams}->"
                + $"{occupancyAfterRetry.TotalMassGrams}; reserved="
                + $"{capacityAfterRetry.ReservedMassGrams}; free={freeMassAfterRetry}; "
                + $"authority={authorityAfterRetry}");
        Check(retryExact,
            "PREPARED_OUTPUT_CANARY_CAPACITY_WAIT_WIP_OUTCOME_FROZEN",
            $"batch={frozenPrepared.batchCommitId}; outcome="
                + $"{frozenPrepared.outcomeFingerprint}; preparedBytes="
                + frozenPreparedJson.Length);
        if (!retryExact)
            return secondWait;

        PhysicalItemTransformInput[] blockerInputs = blockerStacks
            .Select(value => new PhysicalItemTransformInput(
                value.StackId,
                value.Quantity))
            .ToArray();
        string clearOperationId =
            "qa-prepared-output-capacity-clear:" + bill.BillId.Value;
        bool cleared = dispositions.TryCommit(
            blockerInputs,
            PhysicalItemDispositionKind.Sink,
            clearOperationId,
            "qa-capacity-blocker-cleanup",
            out PhysicalItemBatchDispositionReceipt clearReceipt,
            out string clearFailure);
        FacilityBufferPhysicalOccupancySnapshot clearedOccupancy =
            occupancy.Capture(bill.OutputDestinationId);
        FacilityBufferMassCapacitySnapshot clearedCapacity = default;
        bool clearExact = cleared
            && clearReceipt.IsCommitted
            && clearReceipt.Quantity == blockerQuantity
            && clearReceipt.InputMassGrams == blockerMassGrams
            && clearedOccupancy.TotalMassGrams == 0L
            && capacities.TryGetCapacity(
                bill.OutputDestinationId,
                facility.centerPos,
                out clearedCapacity)
            && clearedCapacity.Profile?.MaxMassGrams
                == expectedFacilityCapacityGrams
            && clearedCapacity.ReservedMassGrams == 0L;
        Check(clearExact,
            "PREPARED_OUTPUT_CANARY_CAPACITY_BLOCKER_TYPED_CLEAR_60001G",
            $"cleared={cleared}; receipt={clearReceipt.Quantity}/"
                + $"{clearReceipt.InputMassGrams}; occupancy="
                + $"{clearedOccupancy.TotalMassGrams}; capacity="
                + $"{clearedCapacity.Profile?.MaxMassGrams ?? 0}; reserved="
                + $"{clearedCapacity.ReservedMassGrams}; failure={clearFailure}");
        if (!clearExact)
            return default;

        ProductionWorkExecutionResult resumed = work.ExecuteWork(
            worker,
            facility,
            bill.BillId,
            0f);
        WorldItemStackSnapshot[] publishedStacks = runtime.GetAllStacks()
            .Where(value => value != null
                && value.State == WorldItemStackState.FacilityOutputBuffer
                && string.Equals(value.DestinationId, bill.OutputDestinationId,
                    StringComparison.Ordinal)
                && string.Equals(value.ItemId, expectedOutput.ItemId,
                    StringComparison.Ordinal))
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .ToArray();
        ProductionPreparedOutputLineSaveData frozenLine = frozenPrepared.lines?
            .SingleOrDefault();
        PreparedOutputPublicationIdentity publication = default;
        bool publicationIdentityExact = publishedStacks.Length == 1
            && TryReadPreparedOutputPublicationIdentity(
                publishedStacks[0].Components,
                out publication)
            && publication.Acknowledged
            && string.Equals(publication.BatchCommitId,
                frozenPrepared.batchCommitId, StringComparison.Ordinal)
            && string.Equals(publication.OutcomeFingerprint,
                frozenPrepared.outcomeFingerprint, StringComparison.Ordinal)
            && frozenLine != null
            && string.Equals(publication.OutputLineId,
                frozenLine.outputLineId, StringComparison.Ordinal)
            && string.Equals(publication.ItemId,
                frozenLine.itemId, StringComparison.Ordinal)
            && string.Equals(publication.ItemId,
                expectedOutput.ItemId, StringComparison.Ordinal)
            && string.Equals(publication.PreparedComponentFingerprint,
                frozenLine.componentFingerprint, StringComparison.Ordinal)
            && publication.StackOrdinal == 0
            && publication.BatchStackCount == 1
            && publication.LineStackCount == 1
            && publication.BatchQuantity == expectedOutput.Amount
            && publication.BatchMassGrams == expectedBatchMassGrams
            && publication.LineQuantity == expectedOutput.Amount
            && publication.LineMassGrams == expectedBatchMassGrams
            && publication.Quantity == expectedOutput.Amount
            && publication.MassGrams == expectedBatchMassGrams;
        ProductionBillSnapshot terminalRuntimeBill = bills.GetBills(facility)
            .SingleOrDefault(value => value.BillId == bill.BillId);
        ProductionBillSaveData terminalPersistedBill = persistence.Capture().bills
            .SingleOrDefault(value => value != null
                && string.Equals(value.billId, bill.BillId.Value,
                    StringComparison.Ordinal));
        bool terminalBillRetained = terminalRuntimeBill?.RemainingCycles == 0
            && terminalPersistedBill?.remainingCycles == 0;
        int allWorldInputQuantityAfterResume = runtime.GetAllStacks()
            .Where(value => value != null
                && expectedInputItemIds.Contains(value.ItemId))
            .Sum(value => value.Quantity);
        FacilityBufferPhysicalOccupancySnapshot resumedOccupancy =
            occupancy.Capture(bill.OutputDestinationId);
        FacilityBufferMassCapacitySnapshot resumedCapacity = default;
        bool resumedCapacityCaptured = capacities.TryGetCapacity(
            bill.OutputDestinationId,
            facility.centerPos,
            out resumedCapacity);
        bool resumedExact = resumed.Succeeded
            && resumed.CycleCompleted
            && publicationIdentityExact
            && publishedStacks[0].Quantity == expectedOutput.Amount
            && terminalBillRetained
            && allWorldInputQuantityAfterResume == 0
            && resumedOccupancy.NonCarriedMassGrams
                == expectedBatchMassGrams
            && resumedOccupancy.CommittedCarriedMassGrams == 0L
            && resumedCapacityCaptured
            && resumedCapacity.Profile?.MaxMassGrams
                == expectedFacilityCapacityGrams
            && resumedCapacity.ReservedMassGrams == 0L;
        Check(resumedExact,
            "PREPARED_OUTPUT_CANARY_CAPACITY_RESUME_SAME_BATCH",
            $"result={resumed.Succeeded}/{resumed.CycleCompleted}:"
                + $"{resumed.Failure.Code}; stacks={publishedStacks.Length}; "
                + $"quantity={(publishedStacks.Length == 1 ? publishedStacks[0].Quantity : 0)}/"
                + $"{expectedOutput.Amount}; batch="
                + $"{(publicationIdentityExact ? publication.BatchCommitId : "missing")}/"
                + $"{frozenPrepared.batchCommitId}; outcome="
                + $"{(publicationIdentityExact ? publication.OutcomeFingerprint : "missing")}/"
                + $"{frozenPrepared.outcomeFingerprint}; acknowledged="
                + $"{(publicationIdentityExact && publication.Acknowledged)}; billTerminal="
                + $"{terminalRuntimeBill?.RemainingCycles ?? -1}/"
                + $"{terminalPersistedBill?.remainingCycles ?? -1}; input="
                + $"{allWorldInputQuantityAfterResume}; occupancy="
                + $"{resumedOccupancy.TotalMassGrams}/{expectedBatchMassGrams}; reserved="
                + resumedCapacity.ReservedMassGrams);
        succeeded = resumedExact;
        return resumed;
    }

    private static bool HasPreparedOutputCustody(
        IEnumerable<ItemInstanceComponentSaveData> components) =>
        (components ?? Array.Empty<ItemInstanceComponentSaveData>()).Any(value =>
            value != null
            && string.Equals(
                value.componentTypeId,
                PreparedOutputCustodyComponentTypeId,
                StringComparison.Ordinal));

    private static bool HasAnyPreparedOutputAuthority(
        IEnumerable<ItemInstanceComponentSaveData> components) =>
        (components ?? Array.Empty<ItemInstanceComponentSaveData>()).Any(value =>
            value != null
            && (string.Equals(
                    value.componentTypeId,
                    PreparedOutputCustodyComponentTypeId,
                    StringComparison.Ordinal)
                || string.Equals(
                    value.componentTypeId,
                    PreparedOutputPublicationComponentTypeId,
                    StringComparison.Ordinal)
                || string.Equals(
                    value.componentTypeId,
                    PreparedOutputProvenanceComponentTypeId,
                    StringComparison.Ordinal)));

    private static bool TryReadPreparedOutputPublicationIdentity(
        IEnumerable<ItemInstanceComponentSaveData> components,
        out PreparedOutputPublicationIdentity identity)
    {
        identity = default;
        ItemInstanceComponentSaveData[] matches = (components
                ?? Array.Empty<ItemInstanceComponentSaveData>())
            .Where(value => value != null
                && (string.Equals(
                        value.componentTypeId,
                        PreparedOutputPublicationComponentTypeId,
                        StringComparison.Ordinal)
                    || string.Equals(
                        value.componentTypeId,
                        PreparedOutputProvenanceComponentTypeId,
                        StringComparison.Ordinal)))
            .ToArray();
        bool acknowledged = matches.Length == 1
            && string.Equals(
                matches[0].componentTypeId,
                PreparedOutputProvenanceComponentTypeId,
                StringComparison.Ordinal);
        if (matches.Length != 1
            || matches[0].schemaVersion != 2
            || matches[0].affectsStacking == acknowledged
            || matches[0].values == null
            || matches[0].values.Count != 16)
            return false;
        if (!TryReadCanonicalString(matches[0].values,
                "batch-commit-id", out string batchCommitId)
            || !TryReadCanonicalString(matches[0].values,
                "outcome-fingerprint", out string outcomeFingerprint)
            || !TryReadCanonicalString(matches[0].values,
                "planned-output-fingerprint", out string plannedOutputFingerprint)
            || !TryReadCanonicalString(matches[0].values,
                "output-line-id", out string outputLineId)
            || !TryReadCanonicalString(matches[0].values,
                "item-id", out string itemId)
            || !TryReadCanonicalStringAllowEmpty(matches[0].values,
                "component-signature", out string componentSignature)
            || !TryReadCanonicalStringAllowEmpty(matches[0].values,
                "prepared-component-fingerprint",
                out string preparedComponentFingerprint)
            || !TryReadNonNegativeInteger(matches[0].values,
                "stack-ordinal", out long rawStackOrdinal)
            || rawStackOrdinal > int.MaxValue
            || !TryReadPositiveInteger(matches[0].values,
                "batch-stack-count", out long rawBatchStackCount)
            || rawBatchStackCount > int.MaxValue
            || !TryReadPositiveInteger(matches[0].values,
                "batch-quantity", out long rawBatchQuantity)
            || rawBatchQuantity > int.MaxValue
            || !TryReadPositiveInteger(matches[0].values,
                "batch-mass-grams", out long batchMassGrams)
            || !TryReadPositiveInteger(matches[0].values,
                "line-stack-count", out long rawLineStackCount)
            || rawLineStackCount > int.MaxValue
            || !TryReadPositiveInteger(matches[0].values,
                "line-quantity", out long rawLineQuantity)
            || rawLineQuantity > int.MaxValue
            || !TryReadPositiveInteger(matches[0].values,
                "line-mass-grams", out long lineMassGrams)
            || !TryReadPositiveInteger(matches[0].values,
                "quantity", out long rawQuantity)
            || rawQuantity > int.MaxValue
            || !TryReadPositiveInteger(matches[0].values,
                "mass-grams", out long massGrams))
        {
            return false;
        }
        identity = new PreparedOutputPublicationIdentity(
            batchCommitId,
            outcomeFingerprint,
            plannedOutputFingerprint,
            outputLineId,
            checked((int)rawStackOrdinal),
            checked((int)rawBatchStackCount),
            checked((int)rawBatchQuantity),
            batchMassGrams,
            checked((int)rawLineStackCount),
            checked((int)rawLineQuantity),
            lineMassGrams,
            itemId,
            checked((int)rawQuantity),
            massGrams,
            componentSignature,
            preparedComponentFingerprint,
            acknowledged);
        return true;
    }

    private static bool TryReadCanonicalString(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out string result)
    {
        result = string.Empty;
        ItemStateValueSaveData[] matches = (values
                ?? Array.Empty<ItemStateValueSaveData>())
            .Where(value => value != null
                && value.kind == ItemStateValueKind.String
                && string.Equals(value.key, key, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1
            || string.IsNullOrWhiteSpace(matches[0].stringValue)
            || !string.Equals(
                matches[0].stringValue,
                matches[0].stringValue.Trim(),
                StringComparison.Ordinal))
        {
            return false;
        }
        result = matches[0].stringValue;
        return true;
    }

    private static bool TryReadPositiveInteger(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out long result)
    {
        result = 0L;
        ItemStateValueSaveData[] matches = (values
                ?? Array.Empty<ItemStateValueSaveData>())
            .Where(value => value != null
                && value.kind == ItemStateValueKind.Integer
                && string.Equals(value.key, key, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1 || matches[0].integerValue <= 0L)
            return false;
        result = matches[0].integerValue;
        return true;
    }

    private static bool TryReadNonNegativeInteger(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out long result)
    {
        result = 0L;
        ItemStateValueSaveData[] matches = (values
                ?? Array.Empty<ItemStateValueSaveData>())
            .Where(value => value != null
                && value.kind == ItemStateValueKind.Integer
                && string.Equals(value.key, key, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1 || matches[0].integerValue < 0L)
            return false;
        result = matches[0].integerValue;
        return true;
    }

    private static bool TryReadCanonicalStringAllowEmpty(
        IEnumerable<ItemStateValueSaveData> values,
        string key,
        out string result)
    {
        result = string.Empty;
        ItemStateValueSaveData[] matches = (values
                ?? Array.Empty<ItemStateValueSaveData>())
            .Where(value => value != null
                && value.kind == ItemStateValueKind.String
                && string.Equals(value.key, key, StringComparison.Ordinal))
            .ToArray();
        if (matches.Length != 1
            || !string.Equals(
                matches[0].stringValue ?? string.Empty,
                (matches[0].stringValue ?? string.Empty).Trim(),
                StringComparison.Ordinal))
        {
            return false;
        }
        result = matches[0].stringValue ?? string.Empty;
        return true;
    }

    private readonly struct PreparedOutputPublicationIdentity
    {
        internal PreparedOutputPublicationIdentity(
            string batchCommitId,
            string outcomeFingerprint,
            string plannedOutputFingerprint,
            string outputLineId,
            int stackOrdinal,
            int batchStackCount,
            int batchQuantity,
            long batchMassGrams,
            int lineStackCount,
            int lineQuantity,
            long lineMassGrams,
            string itemId,
            int quantity,
            long massGrams,
            string componentSignature,
            string preparedComponentFingerprint,
            bool acknowledged)
        {
            BatchCommitId = batchCommitId;
            OutcomeFingerprint = outcomeFingerprint;
            PlannedOutputFingerprint = plannedOutputFingerprint;
            OutputLineId = outputLineId;
            StackOrdinal = stackOrdinal;
            BatchStackCount = batchStackCount;
            BatchQuantity = batchQuantity;
            BatchMassGrams = batchMassGrams;
            LineStackCount = lineStackCount;
            LineQuantity = lineQuantity;
            LineMassGrams = lineMassGrams;
            ItemId = itemId;
            Quantity = quantity;
            MassGrams = massGrams;
            ComponentSignature = componentSignature;
            PreparedComponentFingerprint = preparedComponentFingerprint;
            Acknowledged = acknowledged;
        }

        internal string BatchCommitId { get; }
        internal string OutcomeFingerprint { get; }
        internal string PlannedOutputFingerprint { get; }
        internal string OutputLineId { get; }
        internal int StackOrdinal { get; }
        internal int BatchStackCount { get; }
        internal int BatchQuantity { get; }
        internal long BatchMassGrams { get; }
        internal int LineStackCount { get; }
        internal int LineQuantity { get; }
        internal long LineMassGrams { get; }
        internal string ItemId { get; }
        internal int Quantity { get; }
        internal long MassGrams { get; }
        internal string ComponentSignature { get; }
        internal string PreparedComponentFingerprint { get; }
        internal bool Acknowledged { get; }
    }

    private IEnumerator VerifyPreparedOutputDestructiveDrainLiveRoute(
        DungeonRuntimeLifetimeScope scope,
        IWorldItemStackRuntime runtime,
        Grid grid,
        Facility facility,
        Facility warehouse,
        CharacterActor hauler,
        ProductionBillSnapshot bill,
        string itemId,
        int expectedQuantity,
        long expectedMassGrams,
        HaulDeliveryIntentSaveData committedIntent,
        IItemQuantityReservationService reservations,
        IWarehouseMassAdmissionService warehouseAdmissions)
    {
        const string slotId = "qa-v27-production-destructive-drain";
        Time.timeScale = 0f;

        bool midWipCurrentFormatExact = true;
        string midWipFailure = string.Empty;
        try
        {
            ProductionGenericBillTerminalDrainOutboxDebugScenarios.RunAll();
        }
        catch (Exception exception)
        {
            midWipCurrentFormatExact = false;
            midWipFailure = exception.GetType().Name + ":" + exception.Message;
        }
        Check(
            midWipCurrentFormatExact,
            "BATCH_G_FACILITY_DESTROY_MID_WIP_CURRENT_FORMAT_EXACT",
            midWipCurrentFormatExact
                ? "input=3000g; output=0g; declaredLoss=3000g; restore=exact-once"
                : midWipFailure);
        if (!midWipCurrentFormatExact)
        {
            EnsureVerificationTimeScale();
            yield break;
        }

        IBuildingStructuralIntegrityRuntime structural =
            Resolve<IBuildingStructuralIntegrityRuntime>(scope);
        IBuildingDestructiveLossRuntime destructive =
            Resolve<IBuildingDestructiveLossRuntime>(scope);
        IProductionFacilityDestructiveDrainParticipantRegistry participants =
            Resolve<IProductionFacilityDestructiveDrainParticipantRegistry>(scope);
        IProductionFacilityDestructiveDrainAuthorityStateQuery authority =
            Resolve<IProductionFacilityDestructiveDrainAuthorityStateQuery>(scope);
        IProductionOutputDestinationLifecycleQuery lifecycle =
            Resolve<IProductionOutputDestinationLifecycleQuery>(scope);
        IProductionFacilityDestructiveDrainJournalQuery journal =
            Resolve<IProductionFacilityDestructiveDrainJournalQuery>(scope);
        IProductionCapacityRoutingDrainOutbox capacityRoutingDrains =
            Resolve<IProductionCapacityRoutingDrainOutbox>(scope);
        IProductionPhysicalCustodyDrainPort physicalCustodyDrains =
            Resolve<IProductionPhysicalCustodyDrainPort>(scope);
        IProductionCapacityRoutingOperationAuthorityReleaseCoordinator
            capacityRoutingActorAuthority = Resolve<
                IProductionCapacityRoutingOperationAuthorityReleaseCoordinator>(
                scope);
        IBuildingWorldQuery world = Resolve<IBuildingWorldQuery>(scope);
        IProductionBillPersistence billPersistence =
            Resolve<IProductionBillPersistence>(scope);
        IDungeonGameSaveSlotService slots =
            Resolve<IDungeonGameSaveSlotService>(scope);

        BuildingInstanceId facilityId = facility != null
            ? facility.PersistentInstanceId
            : default;
        string operationId = committedIntent?.operationId ?? string.Empty;
        CharacterCarriedItemSaveData carriedBefore = hauler?.CarryInventory?
            .Items?
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.ownerOperationId,
                    operationId,
                    StringComparison.Ordinal)
                && string.Equals(value.itemId, itemId, StringComparison.Ordinal));
        HaulDeliveryItemCommitmentSaveData commitment = committedIntent?
            .commitments?
            .SingleOrDefault(value => value != null
                && string.Equals(value.itemId, itemId, StringComparison.Ordinal));
        WarehouseHaulAdmissionSaveData admission = committedIntent?
            .warehouseAdmissions?
            .SingleOrDefault(value => value != null
                && string.Equals(value.itemId, itemId, StringComparison.Ordinal));
        int quantityBefore = CapturePhysicalItemQuantity(runtime, itemId);
        long massBefore = CapturePhysicalItemMassGrams(runtime, itemId);

        bool servicesReady = structural != null
            && destructive != null
            && participants != null
            && authority != null
            && lifecycle != null
            && journal != null
            && capacityRoutingDrains != null
            && physicalCustodyDrains != null
            && capacityRoutingActorAuthority != null
            && world != null
            && billPersistence != null
            && slots != null
            && facility != null
            && warehouse?.Inventory != null
            && facilityId.IsValid
            && bill != null
            && committedIntent?.HasCommittedPickup == true
            && carriedBefore != null
            && commitment != null
            && admission != null
            && carriedBefore.quantity == expectedQuantity
            && commitment.quantity == expectedQuantity
            && admission.quantity == expectedQuantity
            && admission.reservedMassGrams == expectedMassGrams
            && quantityBefore >= expectedQuantity
            && massBefore >= expectedMassGrams;
        Check(
            servicesReady,
            "PREPARED_OUTPUT_DESTRUCTIVE_RUNTIME_READY",
            $"facility={facilityId.Value}; participantRegistry={participants != null}; "
                + $"authority={authority != null}; lifecycle={lifecycle != null}; "
                + $"journal={journal != null}; save={slots != null}; "
                + $"operation={operationId}; carried={carriedBefore?.quantity ?? -1}/"
                + $"{expectedQuantity}; mass={massBefore}/{expectedMassGrams}");
        if (!servicesReady)
        {
            EnsureVerificationTimeScale();
            yield break;
        }

        string[] expectedParticipantIds =
        {
            ProductionFacilityDestructiveDrainParticipantIds.ApparelWorkOrders,
            ProductionFacilityDestructiveDrainParticipantIds
                .CapacityRoutingOutbox,
            ProductionFacilityDestructiveDrainParticipantIds
                .CombatEquipmentCrafting,
            ProductionFacilityDestructiveDrainParticipantIds
                .GenericProductionBills,
            ProductionFacilityDestructiveDrainParticipantIds
                .PhysicalCustodyCarryRecovery,
            ProductionFacilityDestructiveDrainParticipantIds
                .StockSensorEmbeddedSalvage
        };
        Array.Sort(expectedParticipantIds, StringComparer.Ordinal);
        string[] actualParticipantIds = participants.ExecutionOrder
            .Select(value => value.ParticipantId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        bool participantRegistryExact = actualParticipantIds.SequenceEqual(
            expectedParticipantIds,
            StringComparer.Ordinal);
        ProductionOutputDestinationLifecycleSnapshot lifecycleBefore =
            lifecycle.Capture(facilityId);
        ProductionFacilityDestructiveDrainAuthoritySnapshot authorityBefore =
            authority.Capture(facilityId);
        bool liveAuthorityReady = participantRegistryExact
            && lifecycleBefore.HasAnyAuthority
            && lifecycleBefore.ActiveRecordCount > 0
            && !authorityBefore.HasInvalidPair
            && !authorityBefore.AllAbsent
            && world.Buildings.Count(value => value != null
                && value.PersistentInstanceId.Equals(facilityId)) == 1;
        Check(
            liveAuthorityReady,
            "PREPARED_OUTPUT_DESTRUCTIVE_LIVE_AUTHORITY_READY",
            $"participants={string.Join(",", actualParticipantIds)}; "
                + $"authority={lifecycleBefore.HasAnyAuthority}:"
                + $"{lifecycleBefore.ActiveRecordCount}:"
                + $"{lifecycleBefore.OwnedMassGrams}; pair="
                + $"{authorityBefore.Sensor.State}/{authorityBefore.Output.State}");
        if (!liveAuthorityReady)
        {
            EnsureVerificationTimeScale();
            yield break;
        }

        BuildingStructuralIntegrity structuralState =
            BuildingStructuralIntegrity.Ensure(
                facility,
                new BuildingStructuralIntegrityAbility
                {
                    maxHitPoints = 10f,
                    toughness = 0f,
                    repairHitPointsPerWork = 1f,
                    breachable = true
                });
        int laterSubscriberCalls = 0;
        Action throwingSubscriber =
            ThrowPreparedOutputDestructiveNotification;
        Action laterSubscriber = () => laterSubscriberCalls++;
        facility.OnBuildingDestroyed += throwingSubscriber;
        facility.OnBuildingDestroyed += laterSubscriber;

        slots.Delete(slotId);
        try
        {
            BuildingStructuralDamageResult first = structural.ApplyDamage(
                facility,
                structuralState.CurrentHitPoints);
            ProductionFacilityDestructiveDrainEntrySaveData firstEntry = journal
                .CaptureOpen()
                .SingleOrDefault(value => value != null
                    && string.Equals(
                        value.facilityId,
                        facilityId.Value,
                        StringComparison.Ordinal));
            bool durableStageRequired = !first.Applied
                && !first.Destroyed
                && !string.IsNullOrWhiteSpace(first.FailureReason)
                && firstEntry != null
                && firstEntry.phase !=
                    ProductionFacilityDestructiveDrainPhase
                        .WorldRemovedAwaitingCheckpointGc
                && firstEntry.participants?.Count == expectedParticipantIds.Length
                && firstEntry.participants
                    .Select(value => value?.participantId ?? string.Empty)
                    .OrderBy(value => value, StringComparer.Ordinal)
                    .SequenceEqual(expectedParticipantIds, StringComparer.Ordinal)
                && world.Buildings.Any(value => value != null
                    && value.PersistentInstanceId.Equals(facilityId));
            Check(
                durableStageRequired,
                "PREPARED_OUTPUT_DESTRUCTIVE_DURABLE_STAGE_REQUIRED",
                $"applied={first.Applied}; destroyed={first.Destroyed}; "
                    + $"failure={first.FailureReason}; phase="
                    + $"{firstEntry?.phase.ToString() ?? "missing"}; participants="
                    + $"{firstEntry?.participants?.Count ?? -1}");
            if (!durableStageRequired)
                yield break;

            string capacityRoutingStepId = firstEntry?.participants?
                .SingleOrDefault(value => value != null
                    && string.Equals(
                        value.participantId,
                        ProductionFacilityDestructiveDrainParticipantIds
                            .CapacityRoutingOutbox,
                        StringComparison.Ordinal))?
                .owners?
                .SingleOrDefault(value => value != null
                    && !string.IsNullOrWhiteSpace(value.stepOperationId))?
                .stepOperationId ?? string.Empty;
            capacityRoutingDrains.TryCapture(
                capacityRoutingStepId,
                out ProductionCapacityRoutingDrainSaveData stagedRoutingDrain);
            string stagedSavePath;
            try
            {
                stagedSavePath = slots.Save(slotId, prettyPrint: false);
            }
            catch (Exception exception)
            {
                ProductionCapacityRoutingDrainResult actorDiagnostic =
                    stagedRoutingDrain?.phase is
                        ProductionCapacityRoutingDrainPhase.QuiescingActors
                        or ProductionCapacityRoutingDrainPhase
                            .ReleasingOperationAuthority
                        ? capacityRoutingActorAuthority
                            .TryQuiesceAndReleaseAllActors(
                                capacityRoutingStepId,
                                stagedRoutingDrain.requestFingerprint)
                        : default;
                Check(
                    false,
                    "PREPARED_OUTPUT_DESTRUCTIVE_INTERMEDIATE_CHECKPOINT_CAPTURE",
                    $"failure={exception.GetType().Name}:{exception.Message}; "
                        + $"topLevelFailure={first.FailureReason}; "
                        + $"actorDiagnostic={actorDiagnostic.Status}:"
                        + $"{actorDiagnostic.FailureReason}; "
                        + $"routingStep={capacityRoutingStepId}; routing="
                        + JsonUtility.ToJson(stagedRoutingDrain));
                yield break;
            }
            ProductionFacilityDestructiveDrainEntrySaveData stagedEntry = journal
                .CaptureOpen()
                .SingleOrDefault(value => value != null
                    && string.Equals(
                        value.facilityId,
                        facilityId.Value,
                        StringComparison.Ordinal));
            bool firstCheckpointCommitted = !string.IsNullOrWhiteSpace(
                    stagedSavePath)
                && slots.HasSave(slotId)
                && stagedEntry != null
                && stagedEntry.phase !=
                    ProductionFacilityDestructiveDrainPhase
                        .WorldRemovedAwaitingCheckpointGc;
            Check(
                firstCheckpointCommitted,
                "PREPARED_OUTPUT_DESTRUCTIVE_INTERMEDIATE_CHECKPOINT_COMMITTED",
                $"path={stagedSavePath}; phase="
                    + $"{stagedEntry?.phase.ToString() ?? "missing"}");
            if (!firstCheckpointCommitted)
                yield break;

            string physicalCustodyStepId = firstEntry?.participants?
                .SingleOrDefault(value => value != null
                    && string.Equals(
                        value.participantId,
                        ProductionFacilityDestructiveDrainParticipantIds
                            .PhysicalCustodyCarryRecovery,
                        StringComparison.Ordinal))?
                .owners?
                .SingleOrDefault(value => value != null
                    && !string.IsNullOrWhiteSpace(value.stepOperationId))?
                .stepOperationId ?? string.Empty;
            BuildingDestructiveLossResult terminal = default;
            int terminalAttempts = 0;
            const int terminalAttemptBudget = 32;
            bool terminalProgressStalled = false;
            List<string> terminalProgressTrace = new();
            ProductionFacilityDestructiveDrainEntrySaveData previousEntry =
                journal.CaptureOpen()
                    .SingleOrDefault(value => value != null
                        && string.Equals(
                            value.facilityId,
                            facilityId.Value,
                            StringComparison.Ordinal));
            physicalCustodyDrains.TryCapture(
                physicalCustodyStepId,
                out ProductionPhysicalCustodyDrainSaveData previousPhysical);
            string previousProgress = JsonUtility.ToJson(previousEntry)
                + "|" + JsonUtility.ToJson(previousPhysical);
            do
            {
                terminalAttempts++;
                terminal = destructive.Apply(
                    facility,
                    ProductionFacilityDestructiveDrainCause
                        .StructuralIntegrity);

                ProductionFacilityDestructiveDrainEntrySaveData currentEntry =
                    journal.CaptureOpen()
                        .SingleOrDefault(value => value != null
                            && string.Equals(
                                value.facilityId,
                                facilityId.Value,
                                StringComparison.Ordinal));
                physicalCustodyDrains.TryCapture(
                    physicalCustodyStepId,
                    out ProductionPhysicalCustodyDrainSaveData currentPhysical);
                string currentProgress = JsonUtility.ToJson(currentEntry)
                    + "|" + JsonUtility.ToJson(currentPhysical);
                terminalProgressTrace.Add(
                    terminalAttempts + ":"
                    + (currentEntry?.phase.ToString() ?? "missing") + ":"
                    + (currentPhysical?.phase.ToString() ?? "missing") + ":"
                    + (currentPhysical?.completedActorIds?.Count ?? -1) + ":"
                    + (currentPhysical?.releasedHaulIntentOperationIds?.Count
                        ?? -1));
                terminalProgressStalled = !terminal.Removed
                    && terminal.Disposition ==
                        BuildingDestructiveLossDisposition.DeferredAccepted
                    && string.Equals(
                        previousProgress,
                        currentProgress,
                        StringComparison.Ordinal);
                previousProgress = currentProgress;
                if (terminalProgressStalled)
                    break;
            }
            while (!terminal.Removed
                && terminal.Disposition ==
                    BuildingDestructiveLossDisposition.DeferredAccepted
                && terminalAttempts < terminalAttemptBudget);
            bool destroyFlag = facility != null && facility.isDestroy;
            ProductionFacilityDestructiveDrainEntrySaveData terminalEntry =
                journal.CaptureOpen()
                    .SingleOrDefault(value => value != null
                        && string.Equals(
                            value.facilityId,
                            facilityId.Value,
                            StringComparison.Ordinal));
            ProductionFacilityDestructiveDrainAuthoritySnapshot authorityAfter =
                authority.Capture(facilityId);
            ProductionOutputDestinationLifecycleSnapshot lifecycleAfter =
                lifecycle.Capture(facilityId);
            bool worldRemoved = world.Buildings.All(value => value == null
                    || !value.PersistentInstanceId.Equals(facilityId))
                && grid.FindAllOccupants(value => value is BuildableObject building
                        && building.PersistentInstanceId.Equals(facilityId))
                    .Count == 0
                && destroyFlag;
            bool billRemoved = (billPersistence.Capture()?.bills
                    ?? new List<ProductionBillSaveData>())
                .All(value => value == null
                    || !string.Equals(
                        value.buildingInstanceId,
                        facilityId.Value,
                        StringComparison.Ordinal));
            bool terminalExact = terminal.Disposition ==
                    BuildingDestructiveLossDisposition
                        .CommittedWithNotificationFailure
                && !terminalProgressStalled
                && terminal.Removed
                && terminal.FailureReason.Contains(
                    "production-destructive-world-removal-notification-failed",
                    StringComparison.Ordinal)
                && laterSubscriberCalls == 1
                && terminalEntry?.phase ==
                    ProductionFacilityDestructiveDrainPhase
                        .WorldRemovedAwaitingCheckpointGc
                && terminalEntry.participants?.Count
                    == expectedParticipantIds.Length
                && authorityAfter.AllAbsent
                && !lifecycleAfter.HasAnyAuthority
                && lifecycleAfter.ActiveRecordCount == 0
                && lifecycleAfter.OwnedMassGrams == 0L
                && worldRemoved
                && billRemoved;
            Check(
                terminalExact,
                "PREPARED_OUTPUT_DESTRUCTIVE_TYPED_TERMINAL_WORLD_REMOVAL",
                $"disposition={terminal.Disposition}; removed={terminal.Removed}; "
                    + $"attempts={terminalAttempts}/{terminalAttemptBudget}; "
                    + $"stalled={terminalProgressStalled}; progress="
                    + $"{string.Join(",", terminalProgressTrace)}; "
                    + $"failure={terminal.FailureReason}; subscriber="
                    + $"{laterSubscriberCalls}; phase="
                    + $"{terminalEntry?.phase.ToString() ?? "missing"}; pair="
                    + $"{authorityAfter.Sensor.State}/{authorityAfter.Output.State}; "
                    + $"lifecycle={lifecycleAfter.HasAnyAuthority}:"
                    + $"{lifecycleAfter.ActiveRecordCount}:"
                    + $"{lifecycleAfter.OwnedMassGrams}; world={worldRemoved}; "
                    + $"billRemoved={billRemoved}");
            if (!terminalExact)
                yield break;

            AbilityHaul haul = AbilityHaul.Ensure(hauler);
            bool leaseReleased = !reservations.TryGetLeasesByOwner(
                    operationId,
                    out IReadOnlyList<ItemQuantityLease> remainingLeases)
                || remainingLeases.Count == 0;
            bool intentReleased = !runtime.TryCaptureHaulDeliveryIntent(
                operationId,
                out _);
            bool admissionNotReserved = !warehouseAdmissions.TryGetStatus(
                    admission.tokenId,
                    out WarehouseMassAdmissionStatusSnapshot admissionAfter)
                || admissionAfter.Status !=
                    WarehouseMassAdmissionTokenStatus.Reserved;
            int carriedAfter = hauler?.CarryInventory?.Items?
                .Where(value => value != null
                    && string.Equals(
                        value.ownerOperationId,
                        operationId,
                        StringComparison.Ordinal))
                .Sum(value => value.quantity) ?? 0;
            int quantityAfter = CapturePhysicalItemQuantity(runtime, itemId);
            long massAfter = CapturePhysicalItemMassGrams(runtime, itemId);
            bool physicalConserved = quantityAfter == quantityBefore
                && massAfter == massBefore
                && carriedAfter == 0
                && leaseReleased
                && intentReleased
                && admissionNotReserved
                && warehouse.Inventory.ReservedInboundMassGrams == 0L
                && haul != null
                && !haul.HasBoundDeliveryIntent
                && haul.CaptureActiveHaulOperationIds().Count == 0;
            Check(
                physicalConserved,
                "PREPARED_OUTPUT_DESTRUCTIVE_PHYSICAL_AUTHORITY_CONSERVED",
                $"quantity={quantityBefore}->{quantityAfter}; mass="
                    + $"{massBefore}->{massAfter}; carried={carriedAfter}; "
                    + $"lease={leaseReleased}; intent={intentReleased}; "
                    + $"admission={admissionNotReserved}; inbound="
                    + $"{warehouse.Inventory.ReservedInboundMassGrams}");
            if (!physicalConserved)
                yield break;

            string terminalSavePath = slots.Save(slotId, prettyPrint: false);
            bool checkpointGcExact = !string.IsNullOrWhiteSpace(terminalSavePath)
                && journal.CaptureOpen().Count == 0;
            Check(
                checkpointGcExact,
                "PREPARED_OUTPUT_DESTRUCTIVE_CHECKPOINT_GC_EXACT",
                $"path={terminalSavePath}; open={journal.CaptureOpen().Count}");
            if (!checkpointGcExact)
                yield break;

            bool restored = slots.TryLoad(
                slotId,
                out DungeonGameRestoreReport restoreReport)
                && restoreReport.Success;
            ProductionFacilityDestructiveDrainEntrySaveData restoredEntry =
                journal.CaptureOpen()
                    .SingleOrDefault(value => value != null
                        && string.Equals(
                            value.facilityId,
                            facilityId.Value,
                            StringComparison.Ordinal));
            ProductionFacilityDestructiveDrainAuthoritySnapshot restoredAuthority =
                authority.Capture(facilityId);
            ProductionOutputDestinationLifecycleSnapshot restoredLifecycle =
                lifecycle.Capture(facilityId);
            int restoredQuantity = CapturePhysicalItemQuantity(runtime, itemId);
            long restoredMass = CapturePhysicalItemMassGrams(runtime, itemId);
            bool restoreNoDuplicate = restored
                && restoredEntry?.phase ==
                    ProductionFacilityDestructiveDrainPhase
                        .WorldRemovedAwaitingCheckpointGc
                && restoredAuthority.AllAbsent
                && !restoredLifecycle.HasAnyAuthority
                && restoredLifecycle.ActiveRecordCount == 0
                && restoredLifecycle.OwnedMassGrams == 0L
                && world.Buildings.All(value => value == null
                    || !value.PersistentInstanceId.Equals(facilityId))
                && restoredQuantity == quantityAfter
                && restoredMass == massAfter;
            Check(
                restoreNoDuplicate,
                "PREPARED_OUTPUT_DESTRUCTIVE_RESTORE_NO_DUPLICATE",
                $"restored={restored}; errors="
                    + $"{string.Join(" | ", restoreReport?.Errors ?? new List<string>())}; "
                    + $"phase={restoredEntry?.phase.ToString() ?? "missing"}; "
                    + $"quantity={quantityAfter}->{restoredQuantity}; mass="
                    + $"{massAfter}->{restoredMass}; pair="
                    + $"{restoredAuthority.Sensor.State}/"
                    + $"{restoredAuthority.Output.State}");
            if (!restoreNoDuplicate)
                yield break;

            string replaySavePath = slots.Save(slotId, prettyPrint: false);
            bool replayGcNoOp = !string.IsNullOrWhiteSpace(replaySavePath)
                && journal.CaptureOpen().Count == 0
                && CapturePhysicalItemQuantity(runtime, itemId)
                    == restoredQuantity
                && CapturePhysicalItemMassGrams(runtime, itemId) == restoredMass;
            Check(
                replayGcNoOp,
                "PREPARED_OUTPUT_DESTRUCTIVE_SECOND_CHECKPOINT_NO_OP",
                $"path={replaySavePath}; open={journal.CaptureOpen().Count}; "
                    + $"quantity={CapturePhysicalItemQuantity(runtime, itemId)}; "
                    + $"mass={CapturePhysicalItemMassGrams(runtime, itemId)}");
        }
        finally
        {
            slots.Delete(slotId);
            if (facility != null)
            {
                facility.OnBuildingDestroyed -= throwingSubscriber;
                facility.OnBuildingDestroyed -= laterSubscriber;
            }
            EnsureVerificationTimeScale();
        }
        yield break;
    }

    private static int CapturePhysicalItemQuantity(
        IWorldItemStackRuntime runtime,
        string itemId) => (runtime?.GetAllStacks()
            ?? Array.Empty<WorldItemStackSnapshot>())
        .Where(value => value != null
            && string.Equals(value.ItemId, itemId, StringComparison.Ordinal))
        .Sum(value => value.Quantity);

    private static long CapturePhysicalItemMassGrams(
        IWorldItemStackRuntime runtime,
        string itemId)
    {
        long total = 0L;
        foreach (WorldItemStackSnapshot stack in runtime?.GetAllStacks()
                     ?? Array.Empty<WorldItemStackSnapshot>())
        {
            if (stack == null
                || !string.Equals(stack.ItemId, itemId, StringComparison.Ordinal))
            {
                continue;
            }
            total = checked(total + CapturePhysicalStackMassGrams(
                runtime.MassQuery,
                stack));
        }
        return total;
    }

    private static void ThrowPreparedOutputDestructiveNotification() =>
        throw new InvalidOperationException(
            "qa-prepared-output-destructive-notification-failure");

    private IEnumerator VerifyPreparedOutputDownedCurrentCellRecovery(
        DungeonRuntimeLifetimeScope scope,
        IWorldItemStackRuntime runtime,
        Grid grid,
        IDungeonSaveSectionRegistry saveRegistry,
        IFacilityOutputExactRouteOutboxQuery exactRoutes,
        IItemQuantityReservationService reservations,
        IWarehouseMassAdmissionService warehouseAdmissions,
        CharacterActor hauler,
        Facility warehouse,
        WorldItemStackSnapshot routedStack,
        FacilityOutputExactRoutePendingSnapshot route,
        string itemId,
        int expectedQuantity,
        long expectedMassGrams,
        HaulDeliveryIntentSaveData committedIntent,
        PreparedOutputDownedRecoveryResult result)
    {
        Time.timeScale = 0f;
        string actorId = hauler?.BuildingCharacterId.Value ?? string.Empty;
        string warehouseOwnerId = warehouse?.PersistentInstanceId.Value
            ?? string.Empty;
        string operationId = committedIntent?.operationId ?? string.Empty;
        CharacterCarriedItemSaveData carriedBefore = hauler?.CarryInventory?.Items?
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.ownerOperationId,
                    operationId,
                    StringComparison.Ordinal)
                && string.Equals(value.itemId, itemId, StringComparison.Ordinal));
        HaulDeliveryItemCommitmentSaveData commitment = committedIntent?
            .commitments?
            .SingleOrDefault(value => value != null
                && string.Equals(value.itemId, itemId, StringComparison.Ordinal));
        WarehouseHaulAdmissionSaveData admission = committedIntent?
            .warehouseAdmissions?
            .SingleOrDefault(value => value != null
                && string.Equals(value.itemId, itemId, StringComparison.Ordinal)
                && string.Equals(
                    value.warehouseId,
                    warehouseOwnerId,
                    StringComparison.Ordinal));
        bool ready = runtime != null
            && grid != null
            && saveRegistry != null
            && exactRoutes != null
            && reservations != null
            && warehouseAdmissions != null
            && hauler != null
            && warehouse?.Inventory != null
            && routedStack != null
            && route?.Receipt != null
            && route.DeliveryRevision != null
            && committedIntent?.HasCommittedPickup == true
            && !string.IsNullOrWhiteSpace(actorId)
            && !string.IsNullOrWhiteSpace(operationId)
            && carriedBefore != null
            && commitment != null
            && admission != null
            && string.Equals(
                carriedBefore.carriedStackId,
                commitment.carriedStackId,
                StringComparison.Ordinal)
            && string.Equals(
                carriedBefore.sourceStackId,
                commitment.sourceStackId,
                StringComparison.Ordinal)
            && carriedBefore.quantity == expectedQuantity
            && commitment.quantity == expectedQuantity
            && admission.quantity == expectedQuantity
            && admission.reservedMassGrams == expectedMassGrams
            && warehouse.Inventory.ReservedInboundMassGrams
                == expectedMassGrams;
        Check(ready,
            "PREPARED_OUTPUT_TRANSPORT_DOWNED_READY",
            $"actor={actorId}; operation={operationId}; carried="
                + $"{carriedBefore?.quantity ?? -1}/{expectedQuantity}; "
                + $"commitment={commitment?.quantity ?? -1}; admission="
                + $"{admission?.reservedMassGrams ?? -1}/{expectedMassGrams}; "
                + $"route={route?.Receipt?.RouteOperationId ?? "missing"}");
        if (!ready)
        {
            EnsureVerificationTimeScale();
            yield break;
        }

        List<DungeonSaveSectionEnvelope> checkpoint = saveRegistry.CaptureAll();
        string preparedBefore = CapturePreparedOutputSaveFingerprint(checkpoint);
        string wholeRootBefore = CaptureWholeRootSaveFingerprint(checkpoint);
        Vector2Int sourcePosition = routedStack.Position;
        Vector2Int destinationPosition = warehouse.centerPos;
        Vector2Int? dropCell = FindReachableCells(grid, hauler.GetNowXY(), 64)
            .Where(value => value != sourcePosition
                && value != destinationPosition)
            .OrderByDescending(value =>
                Mathf.Abs(value.x - sourcePosition.x)
                + Mathf.Abs(value.y - sourcePosition.y))
            .Select(value => (Vector2Int?)value)
            .FirstOrDefault();
        Check(dropCell.HasValue,
            "PREPARED_OUTPUT_TRANSPORT_DOWNED_DROP_CELL_READY",
            dropCell.HasValue
                ? $"cell={dropCell.Value}; source={sourcePosition}; destination={destinationPosition}"
                : $"source={sourcePosition}; destination={destinationPosition}");
        if (!dropCell.HasValue)
        {
            EnsureVerificationTimeScale();
            yield break;
        }

        hauler.transform.position = grid.GetWorldPos(dropCell.Value);
        hauler.SetLifecycleState(CharacterLifecycleState.Downed);
        yield return null;
        yield return null;

        WorldItemStackSnapshot[] recoveries = runtime.GetAllStacks()
            .Where(value => value != null
                && value.IsTransientCarryRecoveryDrop
                && string.Equals(
                    value.RecoveryCarrierPersistentId,
                    actorId,
                    StringComparison.Ordinal)
                && value.RecoveryInterruptionKind
                    == WorldItemCarryInterruptionKind.Downed)
            .ToArray();
        WorldItemStackSnapshot recovery = recoveries.SingleOrDefault();
        int carriedAfterDrop = hauler.CarryInventory?.Items?
            .Where(value => value != null
                && string.Equals(
                    value.ownerOperationId,
                    operationId,
                    StringComparison.Ordinal))
            .Sum(value => value.quantity) ?? 0;
        long recoveryMass = recovery != null
            ? CapturePhysicalStackMassGrams(runtime.MassQuery, recovery)
            : -1L;
        int sameStackWorldCount = runtime.GetAllStacks().Count(value => value != null
            && string.Equals(
                value.StackId,
                carriedBefore.carriedStackId,
                StringComparison.Ordinal));
        bool physicalDropExact = recoveries.Length == 1
            && recovery != null
            && string.Equals(
                recovery.StackId,
                carriedBefore.carriedStackId,
                StringComparison.Ordinal)
            && recovery.State == WorldItemStackState.Loose
            && recovery.Position == dropCell.Value
            && recovery.Position != sourcePosition
            && recovery.Position != destinationPosition
            && string.Equals(recovery.ItemId, itemId, StringComparison.Ordinal)
            && recovery.Quantity == expectedQuantity
            && recoveryMass == expectedMassGrams
            && carriedAfterDrop == 0
            && sameStackWorldCount == 1;
        Check(physicalDropExact,
            "PREPARED_OUTPUT_TRANSPORT_DOWNED_CURRENT_CELL_EXACT",
            $"recoveries={recoveries.Length}; stack="
                + $"{recovery?.StackId ?? "missing"}/"
                + $"{carriedBefore.carriedStackId}; cell="
                + $"{recovery?.Position.ToString() ?? "missing"}/"
                + $"{dropCell.Value}; quantity={recovery?.Quantity ?? -1}/"
                + $"{expectedQuantity}; mass={recoveryMass}/"
                + $"{expectedMassGrams}; carried={carriedAfterDrop}; "
                + $"worldCount={sameStackWorldCount}");

        FacilityOutputExactRouteCustodyDiagnosticSnapshot custody = default;
        bool custodyReadable = recovery != null
            && FacilityOutputExactRouteCustodyDiagnostics.TryCapture(
                recovery.Components,
                out custody);
        FacilityOutputExactRoutePendingSnapshot afterRoute = exactRoutes
            .CapturePendingRoutes()
            .SingleOrDefault(value => value?.Receipt != null
                && string.Equals(
                    value.Receipt.RouteOperationId,
                    route.Receipt.RouteOperationId,
                    StringComparison.Ordinal));
        bool recoveryProvenanceExact = recovery != null
            && string.Equals(
                recovery.RecoveryOwnerOperationId,
                operationId,
                StringComparison.Ordinal)
            && string.Equals(
                recovery.RecoverySourceStackId,
                carriedBefore.sourceStackId,
                StringComparison.Ordinal)
            && string.Equals(
                recovery.RecoveryCarrierPersistentId,
                actorId,
                StringComparison.Ordinal)
            && recovery.RecoveryDeadlineGameTime > recovery.DroppedAtGameTime
            && recovery.ReservedQuantity == 0
            && string.IsNullOrWhiteSpace(recovery.ReservedByPersistentId);
        bool recoveryDestinationExact = recovery != null
            && string.Equals(
                recovery.DestinationId,
                route.DeliveryRevision.TargetDestinationId,
                StringComparison.Ordinal)
            && recovery.HasDestinationPosition
            && recovery.DestinationPosition == new Vector2Int(
                committedIntent.dropGridX,
                committedIntent.dropGridY);
        bool recoveryCustodyExact = custodyReadable
            && custody.IsRoutable
            && string.Equals(
                custody.RouteOperationId,
                route.Receipt.RouteOperationId,
                StringComparison.Ordinal)
            && string.Equals(custody.ItemId, itemId, StringComparison.Ordinal)
            && custody.Quantity == expectedQuantity
            && custody.MassGrams == expectedMassGrams
            && string.Equals(
                custody.CurrentTargetDestinationId,
                route.DeliveryRevision.TargetDestinationId,
                StringComparison.Ordinal)
            && custody.CurrentTargetPosition == new Vector2Int(
                route.DeliveryRevision.TargetPositionX,
                route.DeliveryRevision.TargetPositionY);
        bool recoveryRouteExact = afterRoute?.Phase
                == FacilityOutputExactRoutePhase.Routable
            && string.Equals(
                afterRoute.Receipt.PhysicalReceiptFingerprint,
                route.Receipt.PhysicalReceiptFingerprint,
                StringComparison.Ordinal)
            && string.Equals(
                afterRoute.DeliveryRevision.RevisionFingerprint,
                route.DeliveryRevision.RevisionFingerprint,
                StringComparison.Ordinal);
        bool provenanceAndRouteExact = recoveryProvenanceExact
            && recoveryDestinationExact
            && recoveryCustodyExact
            && recoveryRouteExact;
        Check(provenanceAndRouteExact,
            "PREPARED_OUTPUT_TRANSPORT_DOWNED_PROVENANCE_AND_ROUTE_EXACT",
            $"owner={recovery?.RecoveryOwnerOperationId ?? "missing"}/"
                + $"{operationId}; source="
                + $"{recovery?.RecoverySourceStackId ?? "missing"}/"
                + $"{carriedBefore.sourceStackId}; deadline="
                + $"{recovery?.DroppedAtGameTime ?? -1}->"
                + $"{recovery?.RecoveryDeadlineGameTime ?? -1}; destination="
                + $"{recovery?.DestinationId ?? "missing"}; custody="
                + $"{custodyReadable}; route={afterRoute?.Phase.ToString() ?? "missing"}; "
                + $"provenanceExact={recoveryProvenanceExact}; destinationExact="
                + $"{recoveryDestinationExact}; custodyExact={recoveryCustodyExact}; "
                + $"routeExact={recoveryRouteExact}; custodyTarget="
                + $"{custody.CurrentTargetDestinationId}@{custody.CurrentTargetPosition}; "
                + $"revisionTarget={route.DeliveryRevision.TargetDestinationId}@"
                + $"({route.DeliveryRevision.TargetPositionX}, "
                + $"{route.DeliveryRevision.TargetPositionY}); recoveryTarget="
                + $"{recovery?.DestinationPosition.ToString() ?? "missing"}; "
                + $"intentDelivery=({committedIntent.deliveryGridX}, "
                + $"{committedIntent.deliveryGridY}); intentDrop="
                + $"({committedIntent.dropGridX}, {committedIntent.dropGridY}); "
                + $"custodyRoute="
                + $"{custody.RouteOperationId}; custodyItem={custody.ItemId}:"
                + $"{custody.Quantity}:{custody.MassGrams}");

        AbilityHaul droppedHaul = AbilityHaul.Ensure(hauler);
        AbilityMove droppedMove = hauler.GetComponent<AbilityMove>();
        bool ownerLeasesReleased = (!reservations.TryGetLeasesByOwner(
                operationId,
                out IReadOnlyList<ItemQuantityLease> remainingOwnerLeases)
                || remainingOwnerLeases.Count == 0)
            && reservations.GetReservedQuantity(
                new ItemStackId(recovery?.StackId ?? carriedBefore.carriedStackId)) == 0;
        bool intentReleased = !runtime.TryCaptureHaulDeliveryIntent(
            operationId,
            out _);
        bool admissionReleased = warehouseAdmissions.TryGetStatus(
                admission.tokenId,
                out WarehouseMassAdmissionStatusSnapshot releasedAdmission)
            && releasedAdmission.Status == WarehouseMassAdmissionTokenStatus.Released
            && string.Equals(
                releasedAdmission.Token.TokenId,
                admission.tokenId,
                StringComparison.Ordinal)
            && string.Equals(
                releasedAdmission.Token.OwnerOperationId,
                admission.ownerAdmissionOperationId,
                StringComparison.Ordinal)
            && releasedAdmission.Token.AcceptedQuantity == expectedQuantity
            && releasedAdmission.Token.ReservedMassGrams == expectedMassGrams
            && warehouseAdmissions.HasOwnerOperationHistory(
                admission.ownerAdmissionOperationId);
        bool authorityReleased = hauler.CurrentLifecycleState
                == CharacterLifecycleState.Downed
            && droppedHaul != null
            && !droppedHaul.IsHauling
            && !droppedHaul.HasHaulingRoutineForDiagnostics
            && !droppedHaul.HasBoundDeliveryIntent
            && droppedHaul.LastInterruptionDisposition
                == HaulInterruptionDisposition.ReleaseUnpickedAndDropCarriedAtActor
            && droppedHaul.CaptureActiveHaulOperationIds().Count == 0
            && droppedHaul.ActiveReservationsForDiagnostics.Count == 0
            && droppedMove?.HasActiveMovementRoutineForDiagnostics != true
            && ownerLeasesReleased
            && intentReleased
            && admissionReleased
            && warehouse.Inventory.ReservedInboundMassGrams == 0L;
        Check(authorityReleased,
            "PREPARED_OUTPUT_TRANSPORT_DOWNED_AUTHORITY_RELEASED",
            $"lifecycle={hauler.CurrentLifecycleState}; hauling="
                + $"{droppedHaul?.IsHauling}; bound="
                + $"{droppedHaul?.HasBoundDeliveryIntent}; disposition="
                + $"{droppedHaul?.LastInterruptionDisposition}; activeOps="
                + $"{droppedHaul?.CaptureActiveHaulOperationIds().Count ?? -1}; "
                + $"leases={ownerLeasesReleased}; intent={intentReleased}; "
                + $"admission={admissionReleased}; inbound="
                + $"{warehouse.Inventory.ReservedInboundMassGrams}");

        DungeonPhysicalItemSaveData physicalSave = runtime.Capture();
        WorldItemStackSaveData savedRecovery = physicalSave?.stacks?
            .SingleOrDefault(value => value != null
                && recovery != null
                && string.Equals(
                    value.stackId,
                    recovery.StackId,
                    StringComparison.Ordinal));
        string recoveryValidationErrors = "recovery-stack-missing";
        bool recoverySaveValid = savedRecovery != null
            && PhysicalItemSaveValidationDiagnostics.IsValidRecoveryDrop(
                savedRecovery,
                savedRecovery.stackId,
                out recoveryValidationErrors);
        bool physicalSaveExact = savedRecovery != null
            && savedRecovery.state == WorldItemStackState.Loose
            && savedRecovery.gridX == dropCell.Value.x
            && savedRecovery.gridY == dropCell.Value.y
            && savedRecovery.quantity == expectedQuantity
            && savedRecovery.dropDisposition
                == WorldItemDropDisposition.TransientCarryRecoveryDrop
            && string.Equals(
                savedRecovery.recoveryOwnerOperationId,
                operationId,
                StringComparison.Ordinal)
            && string.Equals(
                savedRecovery.recoverySourceStackId,
                carriedBefore.sourceStackId,
                StringComparison.Ordinal)
            && string.Equals(
                savedRecovery.recoveryCarrierPersistentId,
                actorId,
                StringComparison.Ordinal)
            && savedRecovery.recoveryInterruptionKind
                == WorldItemCarryInterruptionKind.Downed
            && savedRecovery.recoveryDeadlineGameTime
                > savedRecovery.droppedAtGameTime
            && recoverySaveValid;
        Check(physicalSaveExact,
            "PREPARED_OUTPUT_TRANSPORT_DOWNED_PHYSICAL_SAVE_EXACT",
            $"saved={savedRecovery != null}; state={savedRecovery?.state}; "
                + $"cell={savedRecovery?.gridX},{savedRecovery?.gridY}; "
                + $"quantity={savedRecovery?.quantity ?? -1}; disposition="
                + $"{savedRecovery?.dropDisposition}; errors="
                + recoveryValidationErrors);
        if (!physicalDropExact
            || !provenanceAndRouteExact
            || !authorityReleased
            || !physicalSaveExact)
        {
            EnsureVerificationTimeScale();
            yield break;
        }

        DungeonGameRestoreReport restoreReport = new();
        bool restored = saveRegistry.RestoreAll(checkpoint, restoreReport)
            && restoreReport.Success;
        ICharacterAiWorldRegistry world = Resolve<ICharacterAiWorldRegistry>(scope);
        PauseWorldActorsForCanary(world);
        CharacterActor restoredHauler = world?.Characters.SingleOrDefault(value =>
            value != null
            && string.Equals(
                value.BuildingCharacterId.Value,
                actorId,
                StringComparison.Ordinal));
        Facility restoredWarehouse = world?.Warehouses.SingleOrDefault(value =>
            value != null
            && string.Equals(
                value.PersistentInstanceId.Value,
                warehouseOwnerId,
                StringComparison.Ordinal)) as Facility;
        if (restoredHauler != null)
            restoredHauler.SetAiPaused(true);
        AbilityHaul restoredHaul = AbilityHaul.Ensure(restoredHauler);
        HaulDeliveryIntentSaveData restoredIntent = restoredHaul?
            .CaptureDeliveryIntentForSave();
        CharacterCarriedItemSaveData restoredCarry = restoredHauler?
            .CarryInventory?.Items?
            .SingleOrDefault(value => value != null
                && string.Equals(
                    value.ownerOperationId,
                    operationId,
                    StringComparison.Ordinal));
        FacilityOutputExactRoutePendingSnapshot restoredRoute = exactRoutes
            .CapturePendingRoutes()
            .SingleOrDefault(value => value?.Receipt != null
                && string.Equals(
                    value.Receipt.RouteOperationId,
                    route.Receipt.RouteOperationId,
                    StringComparison.Ordinal));
        bool restoredAdmission = warehouseAdmissions.TryGetStatus(
                admission.tokenId,
                out WarehouseMassAdmissionStatusSnapshot reboundAdmission)
            && reboundAdmission.Status == WarehouseMassAdmissionTokenStatus.Reserved
            && reboundAdmission.Token.ReservedMassGrams == expectedMassGrams;
        List<DungeonSaveSectionEnvelope> recaptured = restored
            ? saveRegistry.CaptureAll()
            : new List<DungeonSaveSectionEnvelope>();
        string preparedAfter = restored
            ? CapturePreparedOutputSaveFingerprint(recaptured)
            : string.Empty;
        string wholeRootAfter = restored
            ? CaptureWholeRootSaveFingerprint(recaptured)
            : string.Empty;
        bool noRecoveryAfterRestore = runtime.GetAllStacks().All(value => value == null
            || !value.IsTransientCarryRecoveryDrop
            || !string.Equals(
                value.RecoveryOwnerOperationId,
                operationId,
                StringComparison.Ordinal));
        bool checkpointRestoredExact = restored
            && restoredHauler != null
            && restoredWarehouse != null
            && restoredHauler.CurrentLifecycleState == CharacterLifecycleState.Active
            && restoredHaul != null
            && !restoredHaul.IsHauling
            && !restoredHaul.HasHaulingRoutineForDiagnostics
            && restoredHaul.HasBoundDeliveryIntent
            && restoredIntent?.HasCommittedPickup == true
            && string.Equals(
                restoredIntent.operationId,
                operationId,
                StringComparison.Ordinal)
            && restoredCarry != null
            && string.Equals(
                restoredCarry.carriedStackId,
                carriedBefore.carriedStackId,
                StringComparison.Ordinal)
            && string.Equals(
                restoredCarry.sourceStackId,
                carriedBefore.sourceStackId,
                StringComparison.Ordinal)
            && restoredCarry.quantity == expectedQuantity
            && restoredAdmission
            && restoredWarehouse.Inventory.ReservedInboundMassGrams
                == expectedMassGrams
            && restoredRoute?.Phase == FacilityOutputExactRoutePhase.Routable
            && noRecoveryAfterRestore
            && string.Equals(
                preparedBefore,
                preparedAfter,
                StringComparison.Ordinal);
        Check(checkpointRestoredExact,
            "PREPARED_OUTPUT_TRANSPORT_DOWNED_CHECKPOINT_RESTORED_EXACT",
            $"restored={restored}; actor="
                + $"{restoredHauler?.BuildingCharacterId.Value ?? "missing"}; "
                + $"lifecycle={restoredHauler?.CurrentLifecycleState}; intent="
                + $"{restoredIntent?.operationId ?? "missing"}; carried="
                + $"{restoredCarry?.quantity ?? -1}/{expectedQuantity}; admission="
                + $"{restoredAdmission}; inbound="
                + $"{restoredWarehouse?.Inventory?.ReservedInboundMassGrams ?? -1}/"
                + $"{expectedMassGrams}; route="
                + $"{restoredRoute?.Phase.ToString() ?? "missing"}; recovery="
                + $"{!noRecoveryAfterRestore}; preparedFingerprint="
                + $"{string.Equals(preparedBefore, preparedAfter, StringComparison.Ordinal)}; "
                + $"wholeRootFingerprint="
                + $"{string.Equals(wholeRootBefore, wholeRootAfter, StringComparison.Ordinal)}; "
                + $"errors={string.Join(" | ", restoreReport.Errors)}");
        if (!checkpointRestoredExact)
        {
            EnsureVerificationTimeScale();
            yield break;
        }

        result.Succeeded = true;
        result.Hauler = restoredHauler;
        result.Warehouse = restoredWarehouse;
        result.Intent = restoredIntent;
        EnsureVerificationTimeScale();
        yield break;
    }

    private IEnumerator VerifySyntheticPreparedOutputMidCarryRestore(
        DungeonRuntimeLifetimeScope scope,
        IWorldItemStackRuntime runtime,
        IDungeonSaveSectionRegistry saveRegistry,
        CharacterActor hauler,
        string warehouseOwnerId,
        string itemId,
        int expectedQuantity,
        long expectedMassGrams,
        HaulDeliveryIntentSaveData committedIntent,
        SyntheticPreparedOutputMidCarryRestoreResult result)
    {
        Time.timeScale = 0f;
        string actorId = hauler?.BuildingCharacterId.Value ?? string.Empty;
        CharacterCarriedItemSaveData[] carriedBefore = hauler?.CarryInventory?.Items?
            .Where(value => value != null
                && string.Equals(value.itemId, itemId, StringComparison.Ordinal)
                && string.Equals(
                    value.ownerOperationId,
                    committedIntent?.operationId,
                    StringComparison.Ordinal))
            .ToArray() ?? Array.Empty<CharacterCarriedItemSaveData>();
        WarehouseHaulAdmissionSaveData[] admissionsBefore = committedIntent?
            .warehouseAdmissions?
            .Where(value => value != null
                && string.Equals(value.itemId, itemId, StringComparison.Ordinal)
                && string.Equals(
                    value.warehouseId,
                    warehouseOwnerId,
                    StringComparison.Ordinal))
            .ToArray() ?? Array.Empty<WarehouseHaulAdmissionSaveData>();
        HaulDeliveryItemCommitmentSaveData[] commitmentsBefore = committedIntent?
            .commitments?
            .Where(value => value != null
                && string.Equals(value.itemId, itemId, StringComparison.Ordinal))
            .ToArray() ?? Array.Empty<HaulDeliveryItemCommitmentSaveData>();
        bool exactCommitmentJoin = commitmentsBefore.Length == 1
            && carriedBefore.Length == 1
            && string.Equals(
                commitmentsBefore[0].carriedStackId,
                carriedBefore[0].carriedStackId,
                StringComparison.Ordinal)
            && string.Equals(
                commitmentsBefore[0].sourceStackId,
                carriedBefore[0].sourceStackId,
                StringComparison.Ordinal)
            && string.Equals(
                commitmentsBefore[0].expectedStackSignature,
                carriedBefore[0].GetStackSignature(),
                StringComparison.Ordinal)
            && commitmentsBefore[0].quantity == carriedBefore[0].quantity
            && commitmentsBefore[0].quantity == expectedQuantity;
        bool committedExact = committedIntent?.HasCommittedPickup == true
            && string.Equals(
                committedIntent.ownerCharacterId,
                actorId,
                StringComparison.Ordinal)
            && string.Equals(
                committedIntent.destinationId,
                WarehouseStorageIdentity.DestinationPrefix + warehouseOwnerId,
                StringComparison.Ordinal)
            && exactCommitmentJoin
            && carriedBefore.Sum(value => value.quantity) == expectedQuantity
            && admissionsBefore.Length == 1
            && string.Equals(
                admissionsBefore[0].sourceStackId,
                carriedBefore[0].sourceStackId,
                StringComparison.Ordinal)
            && string.Equals(
                admissionsBefore[0].itemId,
                itemId,
                StringComparison.Ordinal)
            && admissionsBefore[0].quantity == expectedQuantity
            && admissionsBefore[0].reservedMassGrams == expectedMassGrams;
        Check(committedExact,
            "PREPARED_OUTPUT_CANARY_MID_CARRY_AUTHORITY_EXACT",
            $"actor={actorId}; operation={committedIntent?.operationId ?? "missing"}; "
            + $"commitments={committedIntent?.commitments?.Count ?? 0}:"
            + $"{committedIntent?.commitments?.Sum(value => value?.quantity ?? 0) ?? 0}; "
            + $"carried={carriedBefore.Length}:{carriedBefore.Sum(value => value.quantity)}; "
            + $"admissions={admissionsBefore.Length}:"
            + $"{admissionsBefore.Sum(value => value.reservedMassGrams)}g");
        if (!committedExact || saveRegistry == null)
        {
            EnsureVerificationTimeScale();
            yield break;
        }

        List<DungeonSaveSectionEnvelope> captured = saveRegistry.CaptureAll();
        DungeonSaveSectionEnvelope characterEnvelope = captured.SingleOrDefault(value =>
            value != null
            && string.Equals(
                value.sectionId,
                CharacterWorldSaveSection.Id,
                StringComparison.Ordinal));
        DungeonCharacterWorldSaveData characterPayload = characterEnvelope != null
            ? JsonUtility.FromJson<DungeonCharacterWorldSaveData>(
                characterEnvelope.payloadJson)
            : null;
        DungeonCharacterSaveData actorPayload = characterPayload?.actors?
            .SingleOrDefault(value => value != null
                && string.Equals(value.persistentId, actorId, StringComparison.Ordinal));
        int savedCarriedQuantity = actorPayload?.carryInventory?.items?
            .Where(value => value != null
                && string.Equals(value.itemId, itemId, StringComparison.Ordinal)
                && string.Equals(
                    value.ownerOperationId,
                    committedIntent.operationId,
                    StringComparison.Ordinal))
            .Sum(value => value.quantity) ?? 0;
        long savedReservedMass = actorPayload?.haulDeliveryIntent?
            .warehouseAdmissions?
            .SingleOrDefault()?
            .reservedMassGrams ?? -1L;
        bool saveJoined = actorPayload?.haulDeliveryIntent?.HasCommittedPickup == true
            && string.Equals(
                actorPayload.haulDeliveryIntent.operationId,
                committedIntent.operationId,
                StringComparison.Ordinal)
            && savedCarriedQuantity == expectedQuantity
            && actorPayload.haulDeliveryIntent.warehouseAdmissions?.Count == 1
            && actorPayload.haulDeliveryIntent.warehouseAdmissions[0]
                .reservedMassGrams == expectedMassGrams;
        Check(saveJoined,
            "PREPARED_OUTPUT_CANARY_MID_CARRY_WHOLE_ROOT_JOINED",
            $"actor={actorId}; carried={savedCarriedQuantity}/{expectedQuantity}; "
            + $"intent={actorPayload?.haulDeliveryIntent?.operationId ?? "missing"}; "
            + $"mass={savedReservedMass}/{expectedMassGrams}");
        if (!saveJoined)
        {
            EnsureVerificationTimeScale();
            yield break;
        }

        string beforeFingerprint = CapturePreparedOutputSaveFingerprint(captured);
        string wholeRootBeforeFingerprint = CaptureWholeRootSaveFingerprint(captured);
        List<DungeonSaveSectionEnvelope> tampered = captured
            .Select(CloneSaveEnvelope)
            .ToList();
        DungeonSaveSectionEnvelope tamperedCharacterEnvelope = tampered.Single(value =>
            value != null
            && string.Equals(
                value.sectionId,
                CharacterWorldSaveSection.Id,
                StringComparison.Ordinal));
        DungeonCharacterWorldSaveData tamperedCharacterPayload =
            JsonUtility.FromJson<DungeonCharacterWorldSaveData>(
                tamperedCharacterEnvelope.payloadJson);
        DungeonCharacterSaveData tamperedActorPayload = tamperedCharacterPayload.actors
            .Single(value => value != null
                && string.Equals(value.persistentId, actorId, StringComparison.Ordinal));
        tamperedActorPayload.haulDeliveryIntent.warehouseAdmissions[0]
            .reservedMassGrams = checked(expectedMassGrams + 1L);
        tamperedCharacterEnvelope.payloadJson = JsonUtility.ToJson(
            tamperedCharacterPayload);

        DungeonGameRestoreReport tamperedReport = new();
        bool tamperedRestored = saveRegistry.RestoreAll(tampered, tamperedReport)
            && tamperedReport.Success;
        List<DungeonSaveSectionEnvelope> afterTamperedAttempt = saveRegistry.CaptureAll();
        string wholeRootAfterTamperedFingerprint =
            CaptureWholeRootSaveFingerprint(afterTamperedAttempt);
        ICharacterAiWorldRegistry faultWorld = Resolve<ICharacterAiWorldRegistry>(scope);
        CharacterActor faultHauler = faultWorld?.Characters.SingleOrDefault(value =>
            value != null
            && string.Equals(
                value.BuildingCharacterId.Value,
                actorId,
                StringComparison.Ordinal));
        Facility faultWarehouse = faultWorld?.Warehouses.SingleOrDefault(value =>
            value != null
            && string.Equals(
                value.PersistentInstanceId.Value,
                warehouseOwnerId,
                StringComparison.Ordinal)) as Facility;
        HaulDeliveryIntentSaveData faultIntent = faultHauler != null
            ? AbilityHaul.Ensure(faultHauler)?.CaptureDeliveryIntentForSave()
            : null;
        int faultCarriedQuantity = faultHauler?.CarryInventory?.Items?
            .Where(value => value != null
                && string.Equals(value.itemId, itemId, StringComparison.Ordinal)
                && string.Equals(
                    value.ownerOperationId,
                    committedIntent.operationId,
                    StringComparison.Ordinal))
            .Sum(value => value.quantity) ?? 0;
        bool tamperRejectedAtomically = !tamperedRestored
            && string.Equals(
                wholeRootBeforeFingerprint,
                wholeRootAfterTamperedFingerprint,
                StringComparison.Ordinal)
            && faultIntent?.HasCommittedPickup == true
            && string.Equals(
                faultIntent.operationId,
                committedIntent.operationId,
                StringComparison.Ordinal)
            && faultCarriedQuantity == expectedQuantity
            && faultIntent.warehouseAdmissions?.Count == 1
            && faultIntent.warehouseAdmissions[0].reservedMassGrams
                == expectedMassGrams
            && faultWarehouse?.Inventory?.ReservedInboundMassGrams
                == expectedMassGrams;
        Check(tamperRejectedAtomically,
            "PREPARED_OUTPUT_CANARY_MID_CARRY_ADMISSION_TAMPER_ATOMIC_REJECT",
            $"restored={tamperedRestored}; actor={faultHauler?.BuildingCharacterId.Value ?? "missing"}; "
            + $"intent={faultIntent?.operationId ?? "missing"}; carried="
            + $"{faultCarriedQuantity}/{expectedQuantity}; reserved="
            + $"{faultWarehouse?.Inventory?.ReservedInboundMassGrams ?? -1}/"
            + $"{expectedMassGrams}; fingerprint="
            + $"wholeRoot={string.Equals(wholeRootBeforeFingerprint, wholeRootAfterTamperedFingerprint, StringComparison.Ordinal)}; "
            + $"errors={string.Join(" | ", tamperedReport.Errors)}");
        if (!tamperRejectedAtomically)
        {
            EnsureVerificationTimeScale();
            yield break;
        }

        DungeonGameRestoreReport restoreReport = new();
        bool restored = saveRegistry.RestoreAll(captured, restoreReport)
            && restoreReport.Success;
        ICharacterAiWorldRegistry world = Resolve<ICharacterAiWorldRegistry>(scope);
        PauseWorldActorsForCanary(world);
        CharacterActor restoredHauler = world?.Characters.SingleOrDefault(value =>
            value != null
            && string.Equals(
                value.BuildingCharacterId.Value,
                actorId,
                StringComparison.Ordinal));
        Facility restoredWarehouse = world?.Warehouses.SingleOrDefault(value =>
            value != null
            && string.Equals(
                value.PersistentInstanceId.Value,
                warehouseOwnerId,
                StringComparison.Ordinal)) as Facility;
        if (restoredHauler != null)
        {
            // Freeze future scheduler admission without mutating any movement
            // authority that RestoreAll may have published. The caller first
            // proves the restored actor is inert, then wakes the real Brain.
            restoredHauler.SetAiPaused(true);
        }
        AbilityHaul restoredAbility = AbilityHaul.Ensure(restoredHauler);
        HaulDeliveryIntentSaveData restoredIntent = restoredAbility?
            .CaptureDeliveryIntentForSave();
        int restoredCarriedQuantity = restoredHauler?.CarryInventory?.Items?
            .Where(value => value != null
                && string.Equals(value.itemId, itemId, StringComparison.Ordinal)
                && string.Equals(
                    value.ownerOperationId,
                    restoredIntent?.operationId,
                    StringComparison.Ordinal))
            .Sum(value => value.quantity) ?? 0;
        List<DungeonSaveSectionEnvelope> recaptured = restored
            ? saveRegistry.CaptureAll()
            : new List<DungeonSaveSectionEnvelope>();
        string afterFingerprint = restored
            ? CapturePreparedOutputSaveFingerprint(recaptured)
            : string.Empty;
        bool restoredExact = restored
            && restoredHauler != null
            && restoredWarehouse != null
            && restoredIntent?.HasCommittedPickup == true
            && string.Equals(
                restoredIntent.operationId,
                committedIntent.operationId,
                StringComparison.Ordinal)
            && restoredCarriedQuantity == expectedQuantity
            && restoredIntent.warehouseAdmissions?.Count == 1
            && restoredIntent.warehouseAdmissions[0].reservedMassGrams
                == expectedMassGrams
            && restoredWarehouse.Inventory.ReservedInboundMassGrams
                == expectedMassGrams
            && string.Equals(beforeFingerprint, afterFingerprint, StringComparison.Ordinal);
        Check(restoredExact,
            "PREPARED_OUTPUT_CANARY_MID_CARRY_RESTORE_EXACT",
            $"restored={restored}; actor={restoredHauler?.BuildingCharacterId.Value ?? "missing"}; "
            + $"warehouse={restoredWarehouse?.PersistentInstanceId.Value ?? "missing"}; "
            + $"intent={restoredIntent?.operationId ?? "missing"}; "
            + $"carried={restoredCarriedQuantity}/{expectedQuantity}; "
            + $"reserved={restoredWarehouse?.Inventory?.ReservedInboundMassGrams ?? -1}/"
            + $"{expectedMassGrams}; fingerprint="
            + $"{ComputeTextSha256(beforeFingerprint) == ComputeTextSha256(afterFingerprint)}; "
            + $"diff={DescribePreparedOutputSaveDifference(captured, recaptured)}; "
            + $"errors={string.Join(" | ", restoreReport.Errors)}");
        if (!restoredExact)
        {
            EnsureVerificationTimeScale();
            yield break;
        }

        result.Succeeded = true;
        result.Hauler = restoredHauler;
        result.Warehouse = restoredWarehouse;
        result.Intent = restoredIntent;
        EnsureVerificationTimeScale();
        yield break;
    }

    private IEnumerator VerifySyntheticPreparedOutputSaveRoundTrip(
        DungeonRuntimeLifetimeScope scope,
        IWorldItemStackRuntime runtime,
        IDungeonSaveSectionRegistry saveRegistry,
        string warehouseOwnerId,
        string itemId,
        int expectedQuantity,
        long expectedMassGrams)
    {
        Check(saveRegistry != null,
            "PREPARED_OUTPUT_CANARY_SAVE_CAPTURE_READY",
            saveRegistry != null ? "registry=ready" : "registry=missing");
        if (saveRegistry == null)
            yield break;

        List<DungeonSaveSectionEnvelope> captured = saveRegistry.CaptureAll();
        string beforeFingerprint = CapturePreparedOutputSaveFingerprint(captured);
        DungeonGameRestoreReport firstReport = new();
        bool firstRestored = saveRegistry.RestoreAll(captured, firstReport)
            && firstReport.Success;
        if (firstRestored)
            QuiesceHaulingBeforeDirectStateFixture();
        Check(firstRestored,
            "PREPARED_OUTPUT_CANARY_RESTORE_ALL_SUCCEEDED",
            firstRestored
                ? $"sections={captured.Count}"
                : string.Join(" | ", firstReport.Errors));
        if (!firstRestored)
            yield break;

        ICharacterPopulationService restoredPopulation =
            Resolve<ICharacterPopulationService>(scope);
        string[] retainedVisitorLeases = (restoredPopulation?.Profiles
                ?? Array.Empty<WorldCharacterProfile>())
            .Where(profile => profile != null
                && (profile.isVisiting
                    || profile.settlementStanding
                        == CharacterSettlementStanding.Visitor))
            .Select(profile => profile.persistentId ?? string.Empty)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        bool visitorLeasesReleased = restoredPopulation != null
            && retainedVisitorLeases.Length == 0;
        Check(visitorLeasesReleased,
            "PREPARED_OUTPUT_CANARY_TRANSIENT_VISITOR_LEASE_RELEASED",
            visitorLeasesReleased
                ? "isVisiting=false; standing=PreparedCandidate-or-persistent"
                : "retained=" + string.Join(",", retainedVisitorLeases));
        if (!visitorLeasesReleased)
            yield break;

        ICharacterAiWorldRegistry world = Resolve<ICharacterAiWorldRegistry>(scope);
        IWarehouseFacility restoredWarehouse = world?.Warehouses.FirstOrDefault(value =>
            value != null
            && string.Equals(
                value.PersistentInstanceId.Value,
                warehouseOwnerId,
                StringComparison.Ordinal));
        Facility restoredFacility = restoredWarehouse as Facility;
        int restoredQuantity = restoredFacility != null
            ? GetStoredItemQuantity(runtime, itemId, restoredFacility.centerPos)
            : 0;
        long restoredMass = restoredWarehouse?.Inventory?.StoredMassGrams ?? -1L;
        string uiCapacity = restoredWarehouse?.Inventory != null
            ? WarehouseMassUiFormatter.FormatCapacity(restoredWarehouse.Inventory)
            : "missing";
        bool restoredExact = restoredWarehouse != null
            && restoredQuantity == expectedQuantity
            && restoredMass == expectedMassGrams
            && uiCapacity.StartsWith(
                WarehouseMassUiFormatter.FormatKilograms(expectedMassGrams) + "/",
                StringComparison.Ordinal);
        Check(restoredExact,
            "PREPARED_OUTPUT_CANARY_WAREHOUSE_AND_UI_RESTORED_EXACT",
            $"warehouse={warehouseOwnerId}; quantity={restoredQuantity}/{expectedQuantity}; "
            + $"mass={restoredMass}/{expectedMassGrams}; ui={uiCapacity}");
        if (!restoredExact)
            yield break;

        List<DungeonSaveSectionEnvelope> recaptured = saveRegistry.CaptureAll();
        string afterFingerprint = CapturePreparedOutputSaveFingerprint(recaptured);
        bool byteStable = string.Equals(
            beforeFingerprint,
            afterFingerprint,
            StringComparison.Ordinal);
        Check(byteStable,
            "PREPARED_OUTPUT_CANARY_SAVE_RECAPTURE_IDENTITY",
            byteStable
                ? $"sha256={ComputeTextSha256(beforeFingerprint)}; length={beforeFingerprint.Length}"
                : $"beforeSha256={ComputeTextSha256(beforeFingerprint)}; "
                    + $"beforeLength={beforeFingerprint.Length}; "
                    + $"afterSha256={ComputeTextSha256(afterFingerprint)}; "
                    + $"afterLength={afterFingerprint.Length}");
        if (!byteStable)
            yield break;

        DungeonGameRestoreReport secondReport = new();
        bool secondRestored = saveRegistry.RestoreAll(captured, secondReport)
            && secondReport.Success;
        if (secondRestored)
            QuiesceHaulingBeforeDirectStateFixture();
        ICharacterAiWorldRegistry secondWorld = Resolve<ICharacterAiWorldRegistry>(scope);
        IWarehouseFacility secondWarehouse = secondWorld?.Warehouses.FirstOrDefault(value =>
            value != null
            && string.Equals(
                value.PersistentInstanceId.Value,
                warehouseOwnerId,
                StringComparison.Ordinal));
        Facility secondFacility = secondWarehouse as Facility;
        int secondQuantity = secondFacility != null
            ? GetStoredItemQuantity(runtime, itemId, secondFacility.centerPos)
            : 0;
        bool replayExact = secondRestored
            && secondWarehouse?.Inventory?.StoredMassGrams == expectedMassGrams
            && secondQuantity == expectedQuantity;
        Check(replayExact,
            "PREPARED_OUTPUT_CANARY_SECOND_RESTORE_NO_DUPLICATE",
            $"restored={secondRestored}; quantity={secondQuantity}/{expectedQuantity}; "
            + $"mass={secondWarehouse?.Inventory?.StoredMassGrams ?? -1}/{expectedMassGrams}; "
            + $"errors={string.Join(" | ", secondReport.Errors)}");
    }

    private static string CapturePreparedOutputSaveFingerprint(
        IEnumerable<DungeonSaveSectionEnvelope> envelopes)
    {
        string[] sectionIds =
        {
            ModularFacilityWorldSaveSection.Id,
            CharacterWorldSaveSection.Id,
            PhysicalItemsSaveSection.Id,
            ProductionBillsSaveSection.Id,
            ProductionPreparedOutputRoutingSaveSection.Id
        };
        return string.Join(
            "\n",
            (envelopes ?? Array.Empty<DungeonSaveSectionEnvelope>())
            .Where(value => value != null && sectionIds.Contains(value.sectionId))
            .OrderBy(value => value.sectionId, StringComparer.Ordinal)
            .Select(value => value.sectionId + "|"
                + value.sectionVersion + "|"
                + value.restorePhase + "|"
                + GetRestoreStableRootPayload(value)));
    }

    private static string GetRestoreStableRootPayload(
        DungeonSaveSectionEnvelope envelope)
    {
        if (envelope == null
            || !string.Equals(
                envelope.sectionId,
                CharacterWorldSaveSection.Id,
                StringComparison.Ordinal))
        {
            return envelope?.payloadJson ?? string.Empty;
        }

        DungeonCharacterWorldSaveData payload = JsonUtility.FromJson<
            DungeonCharacterWorldSaveData>(envelope.payloadJson);
        foreach (WorldCharacterProfile profile in payload?.populationProfiles
                     ?? new List<WorldCharacterProfile>())
        {
            if (profile != null)
            {
                // Transient customer actors are deliberately not persistent.
                // Population restore therefore releases any captured in-world
                // visitor lease so the profile can be spawned again. Normalize
                // that documented restore transition only; actor carry and haul
                // custody remain byte-exact in this fingerprint.
                profile.isVisiting = false;
                if (profile.settlementStanding
                    == CharacterSettlementStanding.Visitor)
                {
                    profile.settlementStanding =
                        CharacterSettlementStanding.PreparedCandidate;
                }
            }
        }

        return payload != null
            ? JsonUtility.ToJson(payload)
            : envelope.payloadJson;
    }

    private static string CaptureWholeRootSaveFingerprint(
        IEnumerable<DungeonSaveSectionEnvelope> envelopes)
    {
        return string.Join(
            "\n",
            (envelopes ?? Array.Empty<DungeonSaveSectionEnvelope>())
            .Where(value => value != null)
            .OrderBy(value => value.sectionId, StringComparer.Ordinal)
            .Select(value => value.sectionId + "|"
                + value.sectionVersion + "|"
                + value.restorePhase + "|"
                + value.optional + "|"
                + value.payloadJson));
    }

    private static string CaptureRestoreStableWholeRootSaveFingerprint(
        IEnumerable<DungeonSaveSectionEnvelope> envelopes)
    {
        return string.Join(
            "\n",
            (envelopes ?? Array.Empty<DungeonSaveSectionEnvelope>())
            .Where(value => value != null)
            .OrderBy(value => value.sectionId, StringComparer.Ordinal)
            .Select(value => value.sectionId + "|"
                + value.sectionVersion + "|"
                + value.restorePhase + "|"
                + value.optional + "|"
                + GetRestoreStableRootPayload(value)));
    }

    private static string DescribeWholeRootSaveDifference(
        IEnumerable<DungeonSaveSectionEnvelope> expected,
        IEnumerable<DungeonSaveSectionEnvelope> actual)
    {
        Dictionary<string, DungeonSaveSectionEnvelope> expectedById =
            (expected ?? Array.Empty<DungeonSaveSectionEnvelope>())
            .Where(value => value != null)
            .ToDictionary(value => value.sectionId, StringComparer.Ordinal);
        Dictionary<string, DungeonSaveSectionEnvelope> actualById =
            (actual ?? Array.Empty<DungeonSaveSectionEnvelope>())
            .Where(value => value != null)
            .ToDictionary(value => value.sectionId, StringComparer.Ordinal);
        List<string> differences = new();
        foreach (string sectionId in expectedById.Keys
            .Concat(actualById.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal))
        {
            bool hasExpected = expectedById.TryGetValue(
                sectionId,
                out DungeonSaveSectionEnvelope expectedEnvelope);
            bool hasActual = actualById.TryGetValue(
                sectionId,
                out DungeonSaveSectionEnvelope actualEnvelope);
            if (!hasExpected || !hasActual)
            {
                differences.Add(
                    $"{sectionId}:expected={hasExpected}:actual={hasActual}");
                continue;
            }

            bool metadataExact = expectedEnvelope.sectionVersion
                    == actualEnvelope.sectionVersion
                && expectedEnvelope.restorePhase == actualEnvelope.restorePhase
                && expectedEnvelope.optional == actualEnvelope.optional;
            bool payloadExact = string.Equals(
                expectedEnvelope.payloadJson,
                actualEnvelope.payloadJson,
                StringComparison.Ordinal);
            if (metadataExact && payloadExact)
                continue;

            differences.Add(
                $"{sectionId}:metadata={metadataExact}:payload={payloadExact}:"
                + $"expectedSha={ComputeTextSha256(expectedEnvelope.payloadJson)}:"
                + $"actualSha={ComputeTextSha256(actualEnvelope.payloadJson)}:"
                + DescribeFirstTextDifference(
                    expectedEnvelope.payloadJson,
                    actualEnvelope.payloadJson));
        }

        return differences.Count == 0
            ? "none"
            : string.Join(" | ", differences);
    }

    private static string DescribeRestoreStableWholeRootSaveDifference(
        IEnumerable<DungeonSaveSectionEnvelope> expected,
        IEnumerable<DungeonSaveSectionEnvelope> actual)
    {
        Dictionary<string, DungeonSaveSectionEnvelope> expectedById =
            (expected ?? Array.Empty<DungeonSaveSectionEnvelope>())
            .Where(value => value != null)
            .ToDictionary(value => value.sectionId, StringComparer.Ordinal);
        Dictionary<string, DungeonSaveSectionEnvelope> actualById =
            (actual ?? Array.Empty<DungeonSaveSectionEnvelope>())
            .Where(value => value != null)
            .ToDictionary(value => value.sectionId, StringComparer.Ordinal);
        List<string> differences = new();
        foreach (string sectionId in expectedById.Keys
            .Concat(actualById.Keys)
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal))
        {
            bool hasExpected = expectedById.TryGetValue(
                sectionId,
                out DungeonSaveSectionEnvelope expectedEnvelope);
            bool hasActual = actualById.TryGetValue(
                sectionId,
                out DungeonSaveSectionEnvelope actualEnvelope);
            if (!hasExpected || !hasActual)
            {
                differences.Add(
                    $"{sectionId}:expected={hasExpected}:actual={hasActual}");
                continue;
            }

            bool metadataExact = expectedEnvelope.sectionVersion
                    == actualEnvelope.sectionVersion
                && expectedEnvelope.restorePhase == actualEnvelope.restorePhase
                && expectedEnvelope.optional == actualEnvelope.optional;
            string expectedPayload = GetRestoreStableRootPayload(
                expectedEnvelope);
            string actualPayload = GetRestoreStableRootPayload(
                actualEnvelope);
            bool payloadExact = string.Equals(
                expectedPayload,
                actualPayload,
                StringComparison.Ordinal);
            if (metadataExact && payloadExact)
                continue;

            differences.Add(
                $"{sectionId}:metadata={metadataExact}:payload={payloadExact}:"
                + $"expectedSha={ComputeTextSha256(expectedPayload)}:"
                + $"actualSha={ComputeTextSha256(actualPayload)}:"
                + DescribeFirstTextDifference(
                    expectedPayload,
                    actualPayload));
        }

        return differences.Count == 0
            ? "none"
            : string.Join(" | ", differences);
    }

    private static string DescribeFirstTextDifference(
        string expected,
        string actual)
    {
        expected ??= string.Empty;
        actual ??= string.Empty;
        int sharedLength = Math.Min(expected.Length, actual.Length);
        int firstDifference = 0;
        while (firstDifference < sharedLength
            && expected[firstDifference] == actual[firstDifference])
        {
            firstDifference++;
        }

        if (firstDifference == sharedLength
            && expected.Length == actual.Length)
        {
            return "firstDiff=none";
        }

        const int contextBefore = 80;
        const int contextLength = 240;
        int contextStart = Math.Max(0, firstDifference - contextBefore);
        int expectedLength = Math.Min(
            contextLength,
            expected.Length - contextStart);
        int actualLength = Math.Min(
            contextLength,
            actual.Length - contextStart);
        string expectedExcerpt = expectedLength > 0
            ? expected.Substring(contextStart, expectedLength)
            : string.Empty;
        string actualExcerpt = actualLength > 0
            ? actual.Substring(contextStart, actualLength)
            : string.Empty;
        return $"firstDiff={firstDifference}:expected={expectedExcerpt}:"
            + $"actual={actualExcerpt}";
    }

    private static string DescribePreparedOutputSaveDifference(
        IEnumerable<DungeonSaveSectionEnvelope> before,
        IEnumerable<DungeonSaveSectionEnvelope> after)
    {
        string[] sectionIds =
        {
            ModularFacilityWorldSaveSection.Id,
            CharacterWorldSaveSection.Id,
            PhysicalItemsSaveSection.Id,
            ProductionBillsSaveSection.Id,
            ProductionPreparedOutputRoutingSaveSection.Id
        };
        Dictionary<string, DungeonSaveSectionEnvelope> beforeById =
            (before ?? Array.Empty<DungeonSaveSectionEnvelope>())
            .Where(value => value != null && sectionIds.Contains(value.sectionId))
            .ToDictionary(value => value.sectionId, StringComparer.Ordinal);
        Dictionary<string, DungeonSaveSectionEnvelope> afterById =
            (after ?? Array.Empty<DungeonSaveSectionEnvelope>())
            .Where(value => value != null && sectionIds.Contains(value.sectionId))
            .ToDictionary(value => value.sectionId, StringComparer.Ordinal);
        foreach (string sectionId in sectionIds.OrderBy(
                     value => value,
                     StringComparer.Ordinal))
        {
            bool hasLeft = beforeById.TryGetValue(
                sectionId,
                out DungeonSaveSectionEnvelope left);
            bool hasRight = afterById.TryGetValue(
                sectionId,
                out DungeonSaveSectionEnvelope right);
            if (!hasLeft || !hasRight)
            {
                return sectionId + ":presence=" + hasLeft + "/" + hasRight;
            }
            if (left.sectionVersion != right.sectionVersion
                || left.restorePhase != right.restorePhase)
            {
                return sectionId + ":envelope=" + left.sectionVersion + "/"
                    + right.sectionVersion + ":" + left.restorePhase + "/"
                    + right.restorePhase;
            }

            string leftPayload = left.payloadJson ?? string.Empty;
            string rightPayload = right.payloadJson ?? string.Empty;
            if (string.Equals(leftPayload, rightPayload, StringComparison.Ordinal))
                continue;

            int commonLength = Math.Min(leftPayload.Length, rightPayload.Length);
            int firstDifference = 0;
            while (firstDifference < commonLength
                && leftPayload[firstDifference] == rightPayload[firstDifference])
            {
                firstDifference++;
            }
            int excerptStart = Math.Max(0, firstDifference - 48);
            int leftLength = Math.Min(128, leftPayload.Length - excerptStart);
            int rightLength = Math.Min(128, rightPayload.Length - excerptStart);
            string leftExcerpt = leftPayload.Substring(excerptStart, leftLength)
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
            string rightExcerpt = rightPayload.Substring(excerptStart, rightLength)
                .Replace("\r", "\\r")
                .Replace("\n", "\\n");
            return sectionId + ":index=" + firstDifference
                + ":length=" + leftPayload.Length + "/" + rightPayload.Length
                + ":before=" + leftExcerpt + ":after=" + rightExcerpt;
        }
        return "none";
    }

    private static DungeonSaveSectionEnvelope CloneSaveEnvelope(
        DungeonSaveSectionEnvelope source)
    {
        return source == null
            ? null
            : new DungeonSaveSectionEnvelope
            {
                sectionId = source.sectionId,
                sectionVersion = source.sectionVersion,
                restorePhase = source.restorePhase,
                optional = source.optional,
                payloadJson = source.payloadJson
            };
    }

    private static string ComputeTextSha256(string value)
    {
        using SHA256 sha = SHA256.Create();
        byte[] digest = sha.ComputeHash(
            Encoding.UTF8.GetBytes(value ?? string.Empty));
        return BitConverter.ToString(digest).Replace("-", string.Empty);
    }

    private sealed class SyntheticPreparedOutputMidCarryRestoreResult
    {
        public bool Succeeded;
        public CharacterActor Hauler;
        public Facility Warehouse;
        public HaulDeliveryIntentSaveData Intent;
    }

    private sealed class PreparedOutputDestinationRevisionDriftResult
    {
        public bool Succeeded;
        public string StaleOperationId = string.Empty;
        public string StaleAdmissionTokenId = string.Empty;
        public long StaleAdmissionRevision;
        public long LiveRevisionAfterDrift;
        public int OutputQuantityBefore;
        public string DriftItemId = string.Empty;
        public int DriftQuantityBefore;
        public long DriftAddedMassGrams;
    }

    private sealed class PreparedOutputDownedRecoveryResult
    {
        public bool Succeeded;
        public CharacterActor Hauler;
        public Facility Warehouse;
        public HaulDeliveryIntentSaveData Intent;
    }

    private IEnumerator VerifyMaterialRepairAndSalvage(
        IWorldItemStackRuntime itemRuntime,
        ICombatEquipmentRuntime equipment,
        ICombatEquipmentMaintenanceRuntime maintenance,
        IResourceEconomyContentCatalog economyCatalog,
        IFacilityBufferDestinationClaimQuery destinationClaims,
        DungeonRuntimeLifetimeScope scope,
        Grid grid,
        CharacterActor hauler,
        Facility warehouse,
        Vector2Int facilityPosition)
    {
        BuildingSO maintenanceAsset = CreateMaintenanceAsset();
        Facility maintenanceFacility = CreateInjectedFacility(
            scope,
            grid,
            maintenanceAsset,
            facilityPosition,
            "QA_Material_Equipment_Maintenance");
        try
        {
            Check(maintenanceFacility != null
                    && CombatEquipmentMaintenanceFacilityUtility.IsMaintenanceFacility(
                        maintenanceFacility),
                "MATERIAL_REPAIR_FACILITY_READY",
                maintenanceFacility != null
                    ? $"pos={maintenanceFacility.centerPos}"
                    : "missing maintenance facility");
            if (maintenanceFacility == null
                || !economyCatalog.TryGetMaterial(
                    "material:blacksteel",
                    out CraftMaterialDefinitionSO blacksteel))
            {
                yield break;
            }

            Check(SeedStoredCraftMaterial(
                    itemRuntime,
                    economyCatalog,
                    warehouse,
                    "material:blacksteel",
                    8,
                    out string seedDetails),
                "MATERIAL_REPAIR_STOCK_SEEDED",
                seedDetails);

            CombatEquipmentInstance armor = equipment.CreateInstance(
                RepairEquipmentId,
                CombatEquipmentQuality.Normal,
                CombatEquipmentWorldState.Stored,
                "material:blacksteel");
            string warehouseDestinationId =
                WarehouseStorageIdentity.RequireDestinationId(warehouse);
            bool stackSpawned = itemRuntime.SpawnExistingUniqueItemAt(
                PhysicalItemIds.ForEquipment(RepairEquipmentId),
                (ItemInstanceId)armor.instanceId,
                warehouse.centerPos,
                WorldItemStackState.Stored,
                warehouseDestinationId,
                out string armorStackId);
            Check(stackSpawned
                    && equipment.TryLinkToWorldStack(
                        armor.instanceId,
                        armorStackId,
                        CombatEquipmentWorldState.Stored)
                    && equipment.TryGetDerivedStats(
                        armor.instanceId,
                        out CombatEquipmentDerivedStats armorStats)
                    && equipment.TryApplyDurabilityDamage(
                        armor.instanceId,
                        armorStats.MaxDurability * 0.6f),
                "MATERIAL_REPAIR_ARMOR_DAMAGED",
                $"instance={armor.instanceId}; material={armor.materialId}");

            Check(maintenance.TryRequestManualRepair(
                    armor.instanceId,
                    out string repairMessage),
                "MATERIAL_REPAIR_ORDER_CREATED",
                repairMessage);
            CombatEquipmentRepairOrder order = maintenance.Orders.FirstOrDefault(candidate =>
                candidate != null
                && string.Equals(
                    candidate.equipmentInstanceId,
                    armor.instanceId,
                    StringComparison.Ordinal));
            Check(order != null
                    && string.Equals(
                        order.materialItemId,
                        blacksteel.ItemId,
                        StringComparison.Ordinal)
                    && order.requiredMaterialAmount > 0,
                "MATERIAL_REPAIR_REQUIRES_ORIGINAL_MATERIAL",
                order != null
                    ? $"item={order.materialItemId}; amount={order.requiredMaterialAmount}"
                    : "missing repair order");
            if (order == null)
            {
                yield break;
            }

            bool repairClaimExact = destinationClaims.TryGetClaim(
                    order.FacilityDestinationId,
                    maintenanceFacility.centerPos,
                    out FacilityBufferDestinationClaim repairClaim)
                && repairClaim.AnchorKind
                    == FacilityBufferDestinationAnchorKind.LiveFacility
                && string.Equals(
                    repairClaim.OwnerDomain,
                    "combat.equipment-maintenance",
                    StringComparison.Ordinal)
                && string.Equals(
                    repairClaim.OwnerOperationId,
                    order.orderId,
                    StringComparison.Ordinal)
                && string.Equals(
                    repairClaim.OwnerFacilityId,
                    order.facilityBuildingId,
                    StringComparison.Ordinal);
            Check(repairClaimExact,
                "MATERIAL_REPAIR_DESTINATION_CLAIM_EXACT",
                $"destination={order.FacilityDestinationId}; "
                + $"facility={order.facilityBuildingId}; "
                + $"drop={maintenanceFacility.centerPos}");

            IFacilityBufferMassCapacityQuery repairCapacities =
                Resolve<IFacilityBufferMassCapacityQuery>(scope);
            bool repairProfileExact = repairCapacities != null
                && repairCapacities.TryGetCapacity(
                    order.FacilityDestinationId,
                    maintenanceFacility.centerPos,
                    out FacilityBufferMassCapacitySnapshot repairCapacity)
                && repairCapacity.Profile.MaxMassGrams > 0L
                && repairCapacity.Profile.CapacityRevision == 1L
                && string.Equals(
                    repairCapacity.Profile.OwnerDomain,
                    "combat.equipment-maintenance",
                    StringComparison.Ordinal)
                && string.Equals(
                    repairCapacity.Profile.OwnerOperationId,
                    order.orderId,
                    StringComparison.Ordinal)
                && string.Equals(
                    repairCapacity.Profile.OwnerFacilityId,
                    order.facilityBuildingId,
                    StringComparison.Ordinal);
            Check(repairProfileExact,
                "MATERIAL_REPAIR_POSITIVE_GRAM_PROFILE_EXACT",
                repairCapacities != null
                    && repairCapacities.TryGetCapacity(
                        order.FacilityDestinationId,
                        maintenanceFacility.centerPos,
                        out FacilityBufferMassCapacitySnapshot detailCapacity)
                    ? $"mass={detailCapacity.Profile.MaxMassGrams}; "
                        + $"revision={detailCapacity.Profile.CapacityRevision}; "
                        + $"owner={detailCapacity.Profile.OwnerOperationId}"
                    : "capacity profile missing");

            bool repairEquipmentDestinationReady =
                equipment.TryGetInstance(
                    armor.instanceId,
                    out CombatEquipmentInstance repairInstance)
                && itemRuntime.GetAllStacks().Any(stack =>
                    stack != null
                    && string.Equals(
                        stack.StackId,
                        repairInstance.sourceStackId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        stack.DestinationId,
                        order.FacilityDestinationId,
                        StringComparison.Ordinal)
                    && stack.HasDestinationPosition
                    && stack.DestinationPosition == maintenanceFacility.centerPos);
            Check(repairEquipmentDestinationReady,
                "MATERIAL_REPAIR_EQUIPMENT_DESTINATION_READY",
                equipment.TryGetInstance(
                    armor.instanceId,
                    out CombatEquipmentInstance currentRepairInstance)
                    ? $"stack={currentRepairInstance.sourceStackId}; "
                        + DescribeStacks(itemRuntime)
                    : "repair equipment missing");

            IWorldItemHaulPlanningService haulPlanning =
                Resolve<IWorldItemHaulPlanningService>(scope);
            WorldItemHaulPlan repairPreview = null;
            string repairPreviewFailure = "haul planning service missing";
            bool repairPlanReady = false;
            for (int attempt = 0; haulPlanning != null && attempt < 16; attempt++)
            {
                repairPlanReady = haulPlanning.TryPreviewBestPlan(
                        hauler,
                        out repairPreview,
                        out repairPreviewFailure)
                    && repairPreview != null
                    && repairPreview.IsValid
                    && string.Equals(
                        repairPreview.PrimaryDestinationId,
                        order.FacilityDestinationId,
                        StringComparison.Ordinal);
                if (repairPlanReady
                    || !string.Equals(
                        repairPreviewFailure,
                        "path search deferred",
                        StringComparison.Ordinal))
                {
                    break;
                }
                yield return null;
            }
            Check(repairPlanReady,
                "MATERIAL_REPAIR_HAUL_PLAN_PREFLIGHT",
                haulPlanning == null
                    ? "haul planning service missing"
                    : repairPlanReady
                        ? $"destination={repairPreview.PrimaryDestinationId}; "
                            + $"pickups={repairPreview.PickupLegs.Count}; "
                            + $"delivery={repairPreview.DeliveryLegs[0].DeliveryPosition}"
                        : $"failure={repairPreviewFailure}; "
                            + $"previewDestination={repairPreview?.PrimaryDestinationId ?? "<none>"}");
            if (!repairPlanReady)
            {
                yield break;
            }

            yield return RunRepeatedHaul(
                hauler,
                () => IsRepairOrderReady(maintenance, order.orderId));
            Check(IsRepairOrderReady(maintenance, order.orderId),
                "MATERIAL_REPAIR_INPUTS_DELIVERED",
                DescribeStacks(itemRuntime));

            WorldItemStackSnapshot[] repairMaterialStacks = itemRuntime.GetAllStacks()
                .Where(stack => stack != null
                    && string.Equals(
                        stack.DestinationId,
                        order.FacilityDestinationId,
                        StringComparison.Ordinal)
                    && string.Equals(
                        stack.ItemId,
                        order.materialItemId,
                        StringComparison.Ordinal))
                .ToArray();
            int deliveredRepairMaterial = repairMaterialStacks
                .Where(stack => stack.State == WorldItemStackState.FacilityBuffer)
                .Sum(stack => stack.Quantity);
            int totalRequestedRepairMaterial = repairMaterialStacks
                .Sum(stack => stack.Quantity);
            Check(
                deliveredRepairMaterial == order.requiredMaterialAmount
                    && totalRequestedRepairMaterial == order.requiredMaterialAmount,
                "MATERIAL_REPAIR_NO_DUPLICATE_REQUEST",
                $"required={order.requiredMaterialAmount}; "
                    + $"delivered={deliveredRepairMaterial}; "
                    + $"destinationTotal={totalRequestedRepairMaterial}; "
                    + DescribeStacks(itemRuntime));

            bool repaired = false;
            bool repairCompleted = false;
            string applyMessage = string.Empty;
            int repairAttempts = Mathf.Max(1, maintenance.Orders.Count + 1);
            for (int attempt = 0; attempt < repairAttempts; attempt++)
            {
                bool applied = maintenance.TryApplyRepairWork(
                    hauler,
                    maintenanceFacility,
                    100f,
                    out bool completedAttempt,
                    out string attemptMessage);
                applyMessage = attemptMessage;
                repaired |= applied;
                if (equipment.TryGetInstance(
                        armor.instanceId,
                        out CombatEquipmentInstance updatedArmor)
                    && updatedArmor.durabilityRatio + 0.001f >= order.targetDurability)
                {
                    repairCompleted = true;
                    break;
                }

                if (!applied && !completedAttempt)
                {
                    break;
                }
            }
            Check(repaired
                    && repairCompleted
                    && equipment.TryGetInstance(
                        armor.instanceId,
                        out CombatEquipmentInstance repairedArmor)
                    && string.Equals(
                        repairedArmor.materialId,
                        "material:blacksteel",
                        StringComparison.Ordinal)
                    && repairedArmor.durabilityRatio + 0.001f >= order.targetDurability,
                "MATERIAL_REPAIR_PRESERVES_INSTANCE_AND_MATERIAL",
                applyMessage);
            Check(!destinationClaims.TryGetClaim(
                    order.FacilityDestinationId,
                    maintenanceFacility.centerPos,
                    out _),
                "MATERIAL_REPAIR_DESTINATION_CLAIM_REVOKED_AFTER_COMPLETE",
                $"destination={order.FacilityDestinationId}; completed={repairCompleted}");
            Check(repairCapacities != null
                    && !repairCapacities.TryGetCapacity(
                        order.FacilityDestinationId,
                        maintenanceFacility.centerPos,
                        out _),
                "MATERIAL_REPAIR_CAPACITY_PROFILE_ZERO_AFTER_COMPLETE",
                $"destination={order.FacilityDestinationId}; completed={repairCompleted}");

            Check(equipment.TrySalvage(
                    armor.instanceId,
                    maintenanceFacility.centerPos,
                    out string recoveredItemId,
                    out int recoveredAmount,
                    out string salvageReason)
                    && string.Equals(
                        recoveredItemId,
                        blacksteel.ItemId,
                        StringComparison.Ordinal)
                    && recoveredAmount > 0
                    && recoveredAmount
                        <= Mathf.FloorToInt(
                            equipment.Definitions
                                .First(definition =>
                                    string.Equals(
                                        definition.EquipmentId,
                                        RepairEquipmentId,
                                        StringComparison.Ordinal))
                                .PrimaryMaterialAmount
                            * 0.5f)
                    && !equipment.TryGetInstance(armor.instanceId, out _)
                    && itemRuntime.GetAllStacks().Any(stack =>
                        stack != null
                        && stack.State == WorldItemStackState.Loose
                        && string.Equals(
                            stack.ItemId,
                            blacksteel.ItemId,
                            StringComparison.Ordinal)
                        && stack.Position == maintenanceFacility.centerPos),
                "MATERIAL_SALVAGE_RETURNS_ORIGINAL_MATERIAL",
                $"item={recoveredItemId}; amount={recoveredAmount}; reason={salvageReason}");
        }
        finally
        {
            if (maintenanceFacility != null)
            {
                maintenanceFacility.DestroySelf();
            }

            Destroy(maintenanceAsset);
        }
    }

    private static bool IsRepairOrderReady(
        ICombatEquipmentMaintenanceRuntime maintenance,
        string orderId)
    {
        return maintenance != null
            && maintenance.Orders.Any(candidate =>
                candidate != null
                && string.Equals(
                    candidate.orderId,
                    orderId,
                    StringComparison.Ordinal)
                && candidate.state is CombatEquipmentRepairOrderState.Ready
                    or CombatEquipmentRepairOrderState.InProgress);
    }

    private IEnumerator VerifyExpeditionPacking(
        IOffensePreparationService preparation,
        IWorldItemStackRuntime itemRuntime,
        IFacilityBufferDestinationClaimQuery destinationClaims,
        Facility warehouse,
        CharacterActor hauler)
    {
        const string rationItemId = "food:preserved-ration";
        QuiesceHaulingBeforeDirectStateFixture();
        yield return null;
        int rationBefore = itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    rationItemId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
        OffenseSupplyLoadout loadout = new OffenseSupplyLoadout();
        loadout.Add(OffenseSupplyType.Rations, 2);
        string packageId = "qa-package-" + Guid.NewGuid().ToString("N");
        bool committed = preparation.TryCommitLoadout(
            loadout,
            new OffenseExpeditionPreparation(supplyCapacity: 6),
            packageId,
            out string message);
        Check(committed,
            "EXPEDITION_SUPPLY_DELIVERY_COMMITTED",
            $"message={message}; stacks={DescribeStacks(itemRuntime)}");
        if (!committed)
        {
            yield break;
        }

        OffenseSupplyPackingStateData package = preparation.CapturePackingState()
            .SingleOrDefault(candidate => candidate != null
                && string.Equals(
                    candidate.packageId,
                    packageId,
                    StringComparison.Ordinal));
        string destinationId = $"expedition:{packageId}";
        bool exactClaim = package != null
            && destinationClaims.TryGetClaim(
                destinationId,
                package.StagingPosition,
                out FacilityBufferDestinationClaim claim)
            && claim.AnchorKind
                == FacilityBufferDestinationAnchorKind.ReservedTarget
            && string.Equals(
                claim.OwnerDomain,
                "offense.expedition-supply",
                StringComparison.Ordinal)
            && string.Equals(
                claim.OwnerOperationId,
                packageId,
                StringComparison.Ordinal)
            && claim.OwnerFacilityId == null;
        Check(exactClaim,
            "EXPEDITION_RESERVED_TARGET_CLAIM_EXACT",
            package == null
                ? "package state missing"
                : $"destination={destinationId}; staging={package.StagingPosition}");
        if (!exactClaim)
            yield break;

        yield return RunRepeatedHaul(
            hauler,
            () => preparation.IsPackageReady(packageId));

        OffenseSupplyPackingSnapshot packing = preparation.GetPackingSnapshot(packageId);
        Check(packing.IsReady,
            "EXPEDITION_SUPPLIES_PACKED",
            $"delivered={packing.Delivered}/{packing.Required}; stacks={DescribeStacks(itemRuntime)}");
        int packed = itemRuntime.GetAllStacks().Where(stack =>
                stack.State == WorldItemStackState.FacilityBuffer
                && string.Equals(stack.DestinationId, destinationId, StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
        Check(packed == 2,
            "EXPEDITION_PACKED_STACK_VISIBLE",
            $"packed={packed}; destination={destinationId}");
        int committedInTransit = itemRuntime.GetCommittedHaulDeliveryQuantity(
            destinationId,
            rationItemId);
        int routedTotal = itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    rationItemId,
                    StringComparison.Ordinal)
                && string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity)
            + committedInTransit;
        Check(routedTotal == 2,
            "EXPEDITION_REPEATED_READY_POLL_NO_DUPLICATE",
            $"routed={routedTotal}; committedInTransit={committedInTransit}; "
            + $"destination={destinationId}");
        bool consumed = preparation.TryConsumePackedSupplies(packageId, out string consumeMessage);
        Check(consumed,
            "EXPEDITION_PACKED_STACK_CONSUME_COMMITTED",
            consumeMessage);
        bool removed = consumed
            && !itemRuntime.GetAllStacks().Any(stack =>
                string.Equals(
                    stack.DestinationId,
                    destinationId,
                    StringComparison.Ordinal));
        Check(removed,
            "EXPEDITION_PACKED_STACK_CONSUMED",
            $"consumed={consumed}; stacks={DescribeStacks(itemRuntime)}");
        bool claimRevoked = consumed
            && !destinationClaims.TryGetClaim(
                destinationId,
                package.StagingPosition,
                out _);
        Check(claimRevoked,
            "EXPEDITION_RESERVED_TARGET_CLAIM_REVOKED_AFTER_CONSUME",
            $"consumed={consumed}; destination={destinationId}");
        int rationAfter = itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    rationItemId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
        Check(consumed && rationBefore - rationAfter == 2,
            "EXPEDITION_SUPPLY_CONSUME_QUANTITY_CONSERVED",
            $"before={rationBefore}; after={rationAfter}; consumed={consumed}");

        string cancelPackageId = "qa-cancel-package-"
            + Guid.NewGuid().ToString("N");
        string warehouseDestinationId =
            WarehouseStorageIdentity.RequireDestinationId(warehouse);
        bool cancelStockSeeded = itemRuntime.SpawnItemAt(
            rationItemId,
            2,
            warehouse.centerPos,
            WorldItemStackState.Stored,
            warehouseDestinationId,
            out int cancelSeededAmount);
        int cancelBefore = itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    rationItemId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
        Check(cancelStockSeeded && cancelSeededAmount == 2,
            "EXPEDITION_CANCEL_STOCK_SEEDED",
            $"spawned={cancelSeededAmount}; total={cancelBefore}");
        bool cancelCommitted = preparation.TryCommitLoadout(
            loadout,
            new OffenseExpeditionPreparation(supplyCapacity: 6),
            cancelPackageId,
            out string cancelMessage);
        OffenseSupplyPackingStateData cancelPackage = preparation
            .CapturePackingState()
            .SingleOrDefault(candidate => candidate != null
                && string.Equals(
                    candidate.packageId,
                    cancelPackageId,
                    StringComparison.Ordinal));
        string cancelDestinationId = $"expedition:{cancelPackageId}";
        bool cancelClaimExact = cancelCommitted
            && cancelPackage != null
            && destinationClaims.TryGetClaim(
                cancelDestinationId,
                cancelPackage.StagingPosition,
                out FacilityBufferDestinationClaim cancelClaim)
            && cancelClaim.AnchorKind
                == FacilityBufferDestinationAnchorKind.ReservedTarget
            && string.Equals(
                cancelClaim.OwnerOperationId,
                cancelPackageId,
                StringComparison.Ordinal)
            && string.Equals(
                cancelClaim.OwnerDomain,
                "offense.expedition-supply",
                StringComparison.Ordinal)
            && cancelClaim.OwnerFacilityId == null;
        if (cancelCommitted)
        {
            preparation.ReturnSupplies(loadout, cancelPackageId);
        }
        int cancelAfter = itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    rationItemId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
        bool cancelConserved = cancelClaimExact
            && cancelBefore == cancelAfter
            && !preparation.GetPackingSnapshot(cancelPackageId).Exists
            && !itemRuntime.GetAllStacks().Any(stack => stack != null
                && string.Equals(
                    stack.DestinationId,
                    cancelDestinationId,
                    StringComparison.Ordinal))
            && !destinationClaims.TryGetClaim(
                cancelDestinationId,
                    cancelPackage.StagingPosition,
                out _)
            && itemRuntime.GetCommittedHaulDeliveryQuantity(
                cancelDestinationId,
                rationItemId) == 0;
        Check(cancelCommitted && cancelConserved,
            "EXPEDITION_CANCEL_RELEASE_CONSERVED",
            $"committed={cancelCommitted}; claim={cancelClaimExact}; "
            + $"before={cancelBefore}; after={cancelAfter}; message={cancelMessage}");

        preparation.ReturnSupplies(loadout, cancelPackageId);
        int duplicateReturnAfter = itemRuntime.GetAllStacks()
            .Where(stack => stack != null
                && string.Equals(
                    stack.ItemId,
                    rationItemId,
                    StringComparison.Ordinal))
            .Sum(stack => stack.Quantity);
        Check(cancelCommitted && duplicateReturnAfter == cancelAfter,
            "EXPEDITION_UNKNOWN_OR_DUPLICATE_RETURN_NO_MINT",
            $"before={cancelAfter}; after={duplicateReturnAfter}; package={cancelPackageId}");
    }

    private IEnumerator VerifyCarryUi(IWorldItemStackRuntime itemRuntime, CharacterActor hauler)
    {
        CharacterCarryInventory carry = CharacterCarryInventory.Ensure(hauler);
        string failure = string.Empty;
        Check(carry != null
                && carry.TryAdd(
                    "qa-carry-ui",
                    "material:lumber",
                    2,
                    itemRuntime.CatalogProvider,
                    itemRuntime.HaulingSettingsProvider,
                    out failure),
            "CARRY_UI_ITEM_SEEDED",
            carry != null ? $"failure={failure}; {DescribeCarry(hauler, itemRuntime)}" : "missing carry");

        Resolve<DungeonStory.Foundation.IGameEventBus>(FindScope())?.ShowInfo(hauler);
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();
        string sample = GetVisibleTextSample();
        string weightText = Resources.FindObjectsOfTypeAll<TMP_Text>()
            .Where(text => text != null
                && text.gameObject.scene.IsValid()
                && text.gameObject.activeInHierarchy
                && !string.IsNullOrWhiteSpace(text.text))
            .Select(text => Compact(text.text))
            .FirstOrDefault(text =>
                text.Contains("kg", StringComparison.OrdinalIgnoreCase)
                && text.Contains("/", StringComparison.Ordinal));
        Check(!string.IsNullOrWhiteSpace(weightText),
            "CARRY_UI_WEIGHT_VISIBLE",
            string.IsNullOrWhiteSpace(weightText) ? sample : weightText);
        yield return CaptureScreen(PhysicalItemLogisticsPlayModeVerifier.CarryCapturePath);
        carry?.RemoveAllItems();
    }

    private static void QuiesceHaulingBeforeDirectStateFixture()
    {
        foreach (CharacterActor actor in CharacterActorCollection.DistinctByGameObject(
                     UnityEngine.Object.FindObjectsByType<CharacterActor>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None)))
        {
            if (actor == null || actor.IsDead)
            {
                continue;
            }
            actor.SetAiPaused(true);
            actor.GetComponent<AbilityMove>()?.CancelActiveMovement();
            actor.GetComponent<AbilityHaul>()?.StopHauling(
                "qa-direct-state-fixture-boundary");
        }
    }

    private static void PauseWorldActorsForCanary(ICharacterAiWorldRegistry world)
    {
        foreach (CharacterActor actor in world?.Characters
                     ?? Array.Empty<CharacterActor>())
        {
            if (actor != null && !actor.IsDead)
                actor.SetAiPaused(true);
        }
    }

    private IEnumerator RunHaul(AIHaul action, CharacterActor hauler, Func<bool> completed)
    {
        AbilityHaul ability = AbilityHaul.Ensure(hauler);
        action.Execute(hauler);
        float startedAt = Time.realtimeSinceStartup;
        while (Time.realtimeSinceStartup - startedAt < HaulTimeoutSeconds)
        {
            EnsureVerificationTimeScale();
            if (completed())
            {
                yield return null;
                Check(true, "AI_HAUL_COMPLETED", $"elapsed={Time.realtimeSinceStartup - startedAt:0.0}s");
                yield break;
            }

            if (ability == null || !ability.IsHauling)
            {
                for (int settleFrame = 0; settleFrame < 4; settleFrame++)
                {
                    yield return null;
                    if (completed())
                    {
                        Check(true, "AI_HAUL_COMPLETED", $"elapsed={Time.realtimeSinceStartup - startedAt:0.0}s");
                        yield break;
                    }
                }

                break;
            }

            yield return null;
        }

        Check(false, "AI_HAUL_COMPLETED", DescribeHaulState(itemRuntime, hauler));
    }

    private IEnumerator RunRepeatedHaul(
        CharacterActor hauler,
        Func<bool> completed)
    {
        float startedAt = Time.realtimeSinceStartup;
        while (!completed()
            && Time.realtimeSinceStartup - startedAt < HaulTimeoutSeconds)
        {
            EnsureVerificationTimeScale();
            AIHaul action = ScriptableObject.CreateInstance<AIHaul>();
            try
            {
                if (!action.CanStart(hauler))
                {
                    yield return null;
                    continue;
                }

                AbilityHaul ability = AbilityHaul.Ensure(hauler);
                action.Execute(hauler);
                while (!completed()
                    && ability != null
                    && ability.IsHauling
                    && Time.realtimeSinceStartup - startedAt < HaulTimeoutSeconds)
                {
                    EnsureVerificationTimeScale();
                    yield return null;
                }
            }
            finally
            {
                Destroy(action);
            }

            yield return null;
        }

        Check(
            completed(),
            "AI_REPEATED_HAUL_COMPLETED",
            $"elapsed={Time.realtimeSinceStartup - startedAt:0.0}s; "
            + DescribeHaulState(itemRuntime, hauler));
    }

    private static void EnsureVerificationTimeScale()
    {
        if (Time.timeScale < 0.1f)
        {
            Time.timeScale = 8f;
        }
    }

    private bool VerifyProductGameplayFacilityAuthorityBaseline()
    {
        BuildableObject[] all = UnityEngine.Object.FindObjectsByType<
            BuildableObject>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        int productionFacilityCount = 0;
        List<KeyValuePair<BuildableObject, string>> invalid = new();
        foreach (BuildableObject value in all)
        {
            if (value == null || !value.gameObject.scene.IsValid())
            {
                continue;
            }

            try
            {
                if (!ProductionFacilityDefinitionIdentity
                    .IsProductionWorkstation(value))
                {
                    continue;
                }

                productionFacilityCount++;
                _ = ProductionFacilityDefinitionIdentity.Resolve(
                    value.BuildingData);
            }
            catch (Exception exception)
            {
                invalid.Add(new KeyValuePair<BuildableObject, string>(
                    value,
                    exception.Message));
            }
        }
        invalid = invalid
            .OrderBy(value => value.Key.gameObject.scene.path, StringComparer.Ordinal)
            .ThenBy(value => value.Key.name, StringComparer.Ordinal)
            .ThenBy(value => value.Key.GetInstanceID())
            .ToList();
        string detail = invalid.Count == 0
            ? $"scene={SceneManager.GetActiveScene().path}; "
                + $"productionFacilities={productionFacilityCount}; "
                + $"mixedWorldEntries={all.Length}"
            : $"scene={SceneManager.GetActiveScene().path}; invalid={invalid.Count}; "
                + string.Join(" || ", invalid.Take(32).Select(entry =>
                {
                    BuildableObject value = entry.Key;
                    return $"name={value.name}; objectScene={value.gameObject.scene.path}; "
                        + $"root={value.transform.root.name}; "
                        + $"definition={value.BuildingData?.name ?? "<null>"}; "
                        + $"definitionId={value.BuildingData?.ContentDefinitionId ?? "<null>"}; "
                        + $"numeric={value.BuildingData?.id.ToString() ?? "<null>"}; "
                        + $"persistent={value.PersistentInstanceId.Value}; "
                        + $"active={value.gameObject.activeInHierarchy}; "
                        + $"failure={entry.Value}";
                }));
        Check(
            invalid.Count == 0,
            "BOOT_PRODUCTION_FACILITY_AUTHORITY_BASELINE",
            detail);
        return invalid.Count == 0;
    }

    private IEnumerator EnsurePlayableRun()
    {
        OwnerRunManager ownerManager = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        int staffCount = 0;
        for (int turn = 0; turn <= VerificationBootMaximumTurns; turn++)
        {
            ownerManager = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
            staffCount = CountPreparedStaff();
            if (ownerManager?.CurrentOwnerActor != null && staffCount == 2)
                break;
            if (turn == VerificationBootMaximumTurns)
                break;
            yield return null;
        }

        bool ready = ownerManager?.CurrentOwnerActor != null && staffCount == 2;
        Check(ready,
            "RUN_READY",
            ready
                ? $"owner={ownerManager.CurrentOwnerActor.name}; staff={staffCount}"
                : $"owner={(ownerManager?.CurrentOwnerActor != null ? ownerManager.CurrentOwnerActor.name : "missing")}; staff={staffCount}");
    }

    private IEnumerator EnsureProductBoot()
    {
        DungeonTitleLifetimeScope titleScope = null;
        IDungeonSceneNavigator navigator = null;
        for (int turn = 0; turn <= VerificationBootMaximumTurns; turn++)
        {
            titleScope = UnityEngine.Object.FindFirstObjectByType<
                DungeonTitleLifetimeScope>(FindObjectsInactive.Include);
            if (titleScope?.Container != null)
            {
                try
                {
                    navigator = titleScope.Container.Resolve<
                        IDungeonSceneNavigator>();
                }
                catch (Exception exception)
                {
                    capturedErrors.Add("[BOOT-DI-ERROR] " + exception);
                }
            }
            if (navigator != null
                && string.Equals(
                    SceneManager.GetActiveScene().name,
                    DungeonSceneNavigator.TitleSceneName,
                    StringComparison.Ordinal))
            {
                break;
            }
            if (turn == VerificationBootMaximumTurns)
                break;
            yield return null;
        }

        bool titleReady = navigator != null;
        Check(
            titleReady,
            "BOOT_TITLE_READY",
            titleReady
                ? "Title scope and production scene navigator are ready."
                : "Title scope or production scene navigator was not ready.");
        if (!titleReady
            || !navigator.StartNewGame(
                DungeonDifficulty.Normal,
                DungeonSurvivalPressure.Standard))
        {
            Check(false, "BOOT_PREPARATION_REQUESTED",
                "Production StartNewGame request was rejected.");
            yield break;
        }

        Button owner = null;
        Button next = null;
        for (int turn = 0; turn <= VerificationBootMaximumTurns; turn++)
        {
            owner = Resources.FindObjectsOfTypeAll<Button>()
                .Where(candidate => candidate != null
                    && candidate.gameObject.scene.IsValid()
                    && candidate.gameObject.activeInHierarchy
                    && candidate.interactable
                    && candidate.name.StartsWith(
                        "OwnerCandidate_",
                        StringComparison.Ordinal))
                .OrderBy(candidate => candidate.name, StringComparer.Ordinal)
                .FirstOrDefault();
            next = StartPartyPlayModeTestDriver.FindButton(
                "PreparationOwnerNextButton",
                requireInteractable: false);
            if (owner != null
                && next != null
                && string.Equals(
                    SceneManager.GetActiveScene().name,
                    DungeonSceneNavigator.PreparationSceneName,
                    StringComparison.Ordinal))
            {
                break;
            }
            if (turn == VerificationBootMaximumTurns)
                break;
            yield return null;
        }

        bool preparationReady = owner != null && next != null;
        Check(preparationReady, "BOOT_PREPARATION_READY",
            preparationReady
                ? "Preparation owner selection is ready."
                : "Preparation owner selection did not become ready.");
        if (!preparationReady)
            yield break;

        ClickButton(owner);
        yield return null;
        next = StartPartyPlayModeTestDriver.FindButton(
            "PreparationOwnerNextButton",
            requireInteractable: true);
        if (next == null)
        {
            Check(false, "BOOT_PREPARATION_OWNER_SELECTED",
                "Owner selection did not enable the next command.");
            yield break;
        }
        ClickButton(next);

        Button start = null;
        for (int turn = 0; turn <= VerificationBootMaximumTurns; turn++)
        {
            start = StartPartyPlayModeTestDriver.FindButton(
                "PreparationStartRunButton",
                requireInteractable: true);
            // The preparation scene becomes interactive before the navigator's
            // fade-out completes. Starting Gameplay during that short window is
            // rejected by BeginTransition because the previous transition still
            // owns the mailbox. Wait for both the production command and its
            // navigation authority to be ready before dispatching the click.
            if (start != null && !navigator.IsTransitioning)
                break;
            start = null;
            if (turn == VerificationBootMaximumTurns)
                break;
            yield return null;
        }
        Check(start != null, "BOOT_PREPARED_START_READY",
            start != null
                ? "Prepared start command is interactable."
                : "Prepared start command did not become interactable.");
        if (start == null)
            yield break;

        ClickButton(start);
        yield return null;
        yield return null;
        bool preparedStartObserved = navigator.IsTransitioning
            || string.Equals(
                SceneManager.GetActiveScene().name,
                DungeonSceneNavigator.GameplaySceneName,
                StringComparison.Ordinal);
        Check(preparedStartObserved, "BOOT_PREPARED_START_REQUESTED",
            preparedStartObserved
                ? "PreparedNewRun was accepted through the production preparation UI."
                : $"PreparedNewRun was not accepted; scene="
                    + $"{SceneManager.GetActiveScene().name}; "
                    + $"transitioning={navigator.IsTransitioning}; "
                    + $"startPresent={StartPartyPlayModeTestDriver.FindButton("PreparationStartRunButton", requireInteractable: false) != null}; "
                    + $"startInteractable={StartPartyPlayModeTestDriver.FindButton("PreparationStartRunButton", requireInteractable: true) != null}.");
        if (!preparedStartObserved)
            yield break;

        for (int turn = 0; turn <= VerificationBootMaximumTurns; turn++)
        {
            Scene activeGameplayScene = SceneManager.GetActiveScene();
            if (string.Equals(
                    activeGameplayScene.name,
                    DungeonSceneNavigator.GameplaySceneName,
                    StringComparison.Ordinal)
                && UnityEngine.Object.FindFirstObjectByType<
                    DungeonRuntimeLifetimeScope>(FindObjectsInactive.Include)
                    ?.Container != null)
            {
                bool syntheticSceneRequired = PhysicalItemLogisticsPlayModeVerifier
                    .TryReadPreparedOutputWarehouseCase(
                        out PreparedOutputLiveRouteCase liveRouteCase,
                        out string requestFailure)
                    && liveRouteCase.RequiresSanitizedScene;
                bool syntheticSceneExact = !syntheticSceneRequired
                    || string.Equals(
                        activeGameplayScene.path,
                        SyntheticPreparedOutputCanaryGameplaySceneLease
                            .ExpectedRuntimeScenePath,
                        StringComparison.Ordinal);
                Check(
                    syntheticSceneExact,
                    "BOOT_GAMEPLAY_SCENE_PATH_EXACT",
                    syntheticSceneExact
                        ? $"active={activeGameplayScene.path}; synthetic={syntheticSceneRequired}"
                        : $"active={activeGameplayScene.path}; expected="
                            + SyntheticPreparedOutputCanaryGameplaySceneLease
                                .ExpectedRuntimeScenePath
                            + $"; request={requestFailure}");
                if (!syntheticSceneExact)
                    yield break;

                productBootSucceeded = true;
                Check(true, "BOOT_GAMEPLAY_READY",
                    "PreparedNewRun reached the exact Gameplay scene with a runtime container.");
                yield break;
            }
            if (turn == VerificationBootMaximumTurns)
                break;
            yield return null;
        }
        Check(false, "BOOT_GAMEPLAY_READY",
            "PreparedNewRun did not reach Gameplay before timeout.");
    }

    private static void ClickButton(Button button)
    {
        RectTransform rect = button?.transform as RectTransform;
        Vector2 position = rect != null
            ? RectTransformUtility.WorldToScreenPoint(
                null,
                rect.TransformPoint(rect.rect.center))
            : Vector2.zero;
        if (button == null
            || !PlayModeVerificationFrameWait.DispatchPointerClick(
                button.gameObject,
                position))
        {
            throw new InvalidOperationException(
                "Verification button click could not be dispatched.");
        }
    }

    private void CaptureRuntimeState(IWorldItemStackRuntime itemRuntime, ICombatEquipmentRuntime equipment)
    {
        physicalSnapshot = itemRuntime.Capture();
        equipmentSnapshot = equipment.Capture();
        warehouseSnapshots.Clear();
        foreach (WarehouseInventory inventory in UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None)
                 .OfType<IWarehouseFacility>()
                 .Where(facility => facility != null && facility.Inventory != null)
                 .Select(facility => facility.Inventory)
                 .Distinct())
        {
            warehouseSnapshots[inventory] = inventory.CreateSnapshot();
        }
    }

    private void RestoreRuntimeState(IWorldItemStackRuntime itemRuntime, ICombatEquipmentRuntime equipment)
    {
        CharacterSummaryInfo summary = UnityEngine.Object.FindFirstObjectByType<CharacterSummaryInfo>(
            FindObjectsInactive.Include);
        summary?.OnClose();

        foreach (KeyValuePair<WarehouseInventory, WarehouseInventorySnapshot> pair in warehouseSnapshots)
        {
            if (pair.Key != null && pair.Value != null)
            {
                pair.Key.ApplySnapshot(pair.Value);
            }
        }

        if (physicalSnapshot != null)
        {
            itemRuntime.Restore(physicalSnapshot);
        }

        if (equipmentSnapshot != null)
        {
            equipment.PublishRestoreCandidate(
                equipment.BuildRestoreCandidate(equipmentSnapshot));
        }
    }

    private void DisableBrainForDeterministicHauling(CharacterActor hauler)
    {
        isolatedAiPauseStates.Clear();
        isolatedLogisticsMeasurementStates.Clear();
        verificationActors.Clear();
        foreach (CharacterActor actor in CharacterActorCollection.DistinctByGameObject(
                     UnityEngine.Object.FindObjectsByType<CharacterActor>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None)))
        {
            if (actor == null || actor.IsDead)
            {
                continue;
            }

            verificationActors.Add(actor);
            isolatedAiPauseStates.Add(actor, actor.IsAiPaused());
            actor.SetAiPaused(true);
            AIBrain brain = actor != null ? actor.Brain : null;
            brain?.StopAllAiForLifecycleTransition(
                "qa-physical-logistics-isolation");
            actor.GetComponent<AbilityMove>()?.CancelActiveMovement();
            actor.GetComponent<AbilityShopping>()?.StopShopping(
                "qa-physical-logistics-isolation");
            actor.GetComponent<AbilityHaul>()?.StopHauling(
                "qa-physical-logistics-isolation");
        }
    }

    private void ConfigureNaturalClearanceAiMeasurement()
    {
        EnsureNaturalClearanceDeveloperMode();
        ReapplyNaturalClearanceTransientCheats();

        if (!naturalClearanceConsumablesStateCaptured)
        {
            naturalClearanceConsumables =
                Resolve<CharacterConsumablesRuntime>(FindScope());
            naturalClearanceConsumablesStateCaptured =
                naturalClearanceConsumables != null;
            naturalClearanceMealDeliveryOriginallyPaused =
                naturalClearanceConsumables?
                    .MealDeliveryPausedForDiagnostics
                ?? false;
            Check(
                naturalClearanceConsumablesStateCaptured,
                "OUTPUT_CLEARANCE_CONSUMABLE_DELIVERY_FROZEN",
                naturalClearanceConsumablesStateCaptured
                    ? $"originalPaused={naturalClearanceMealDeliveryOriginallyPaused}"
                    : "production CharacterConsumablesRuntime was not found");
        }
        naturalClearanceConsumables?
            .ConfigureMealDeliveryForDiagnostics(true);

        if (!naturalClearanceInvasionThreatStateCaptured)
        {
            naturalClearanceInvasionThreat =
                UnityEngine.Object.FindFirstObjectByType<InvasionThreatRuntime>(
                    FindObjectsInactive.Include);
            naturalClearanceInvasionThreatStateCaptured =
                naturalClearanceInvasionThreat != null;
            naturalClearanceInvasionThreatOriginallyEnabled =
                naturalClearanceInvasionThreat?.enabled ?? false;
            Check(
                naturalClearanceInvasionThreatStateCaptured,
                "OUTPUT_CLEARANCE_INVASION_THREAT_FROZEN",
                naturalClearanceInvasionThreatStateCaptured
                    ? $"originalEnabled={naturalClearanceInvasionThreatOriginallyEnabled}"
                    : "production InvasionThreatRuntime was not found");
        }
        if (naturalClearanceInvasionThreat != null)
            naturalClearanceInvasionThreat.enabled = false;

        if (!naturalClearanceSpawnerStateCaptured)
        {
            naturalClearanceSpawner = UnityEngine.Object.FindFirstObjectByType<
                CharacterSpawner>(FindObjectsInactive.Include);
            naturalClearanceSpawnerStateCaptured = naturalClearanceSpawner != null;
            naturalClearanceSpawnerOriginallyPaused =
                naturalClearanceSpawner
                    ?.DeterministicSimulationPausedForDiagnostics
                ?? false;
            Check(
                naturalClearanceSpawnerStateCaptured,
                "OUTPUT_CLEARANCE_SPAWNER_FROZEN",
                naturalClearanceSpawnerStateCaptured
                    ? $"originalPaused={naturalClearanceSpawnerOriginallyPaused}"
                    : "production CharacterSpawner was not found");
        }
        naturalClearanceSpawner
            ?.ConfigureDeterministicSimulationForDiagnostics(true);

        if (!naturalClearanceSchedulerStateCaptured)
        {
            // CharacterAiScheduler is a scene-owned MonoBehaviour. Production
            // composition exposes it through CharacterSceneRuntimeReferences,
            // not as a direct container registration, so resolve the same scene
            // authority used by DungeonRuntimeLifetimeScope.
            naturalClearanceScheduler =
                UnityEngine.Object.FindFirstObjectByType<CharacterAiScheduler>(
                    FindObjectsInactive.Include);
            naturalClearanceSchedulerStateCaptured =
                naturalClearanceScheduler != null;
            naturalClearanceSchedulerOriginallyDeterministic =
                naturalClearanceScheduler?
                    .DeterministicSimulationForDiagnostics
                ?? false;
            Check(
                naturalClearanceSchedulerStateCaptured,
                "OUTPUT_CLEARANCE_SCHEDULER_DETERMINISTIC",
                naturalClearanceSchedulerStateCaptured
                    ? $"originalDeterministic="
                        + naturalClearanceSchedulerOriginallyDeterministic
                    : "production CharacterAiScheduler was not found");
        }
        naturalClearanceScheduler?
            .ConfigureDeterministicSimulationForDiagnostics(true);

        isolatedAiPauseStates.Clear();
        isolatedLogisticsMeasurementStates.Clear();
        verificationActors.Clear();
        ICharacterDeprivationCommand deprivationCommand =
            Resolve<ICharacterDeprivationCommand>(FindScope());
        ICharacterDeprivationQuery deprivationQuery =
            Resolve<ICharacterDeprivationQuery>(FindScope());
        Check(
            deprivationCommand != null && deprivationQuery != null,
            "OUTPUT_CLEARANCE_DEPRIVATION_ISOLATION_AVAILABLE",
            $"command={deprivationCommand != null}; query={deprivationQuery != null}");
        foreach (CharacterActor actor in CharacterActorCollection.DistinctByGameObject(
                     UnityEngine.Object.FindObjectsByType<CharacterActor>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None)))
        {
            if (actor == null || actor.IsDead)
                continue;

            verificationActors.Add(actor);
            isolatedAiPauseStates.Add(actor, actor.IsAiPaused());
            bool decisionStateReset = TryResetNaturalClearanceDecisionState(
                actor,
                deprivationCommand,
                deprivationQuery,
                out string decisionStateResetDetails);
            Check(
                decisionStateReset,
                "OUTPUT_CLEARANCE_ACTOR_DECISION_STATE_ISOLATED",
                decisionStateResetDetails);
            AIBrain brain = actor.Brain;
            if (brain == null)
                continue;
            isolatedLogisticsMeasurementStates.Add(
                brain,
                brain.LogisticsMeasurementOnlyForDiagnostics);
            brain.ConfigureLogisticsMeasurementForDiagnostics(true);
        }
        naturalClearanceScheduler?
            .ResetDeterministicSimulationCheckpointForDiagnostics();
    }

    private void EnsureNaturalClearanceDeveloperMode()
    {
        if (!naturalClearanceUserSettingsStateCaptured)
        {
            naturalClearanceUserSettings =
                Resolve<IDungeonUserSettingsService>(FindScope());
            if (naturalClearanceUserSettings == null)
            {
                throw new InvalidOperationException(
                    "Natural output-clearance measurement requires user-settings authority.");
            }

            naturalClearanceDeveloperModeOriginallyEnabled =
                naturalClearanceUserSettings.Current.developerMode;
            naturalClearanceUserSettingsStateCaptured = true;
        }

        if (!naturalClearanceUserSettings.Current.developerMode)
        {
            naturalClearanceUserSettings.Update(
                value => value.developerMode = true);
        }
        if (!naturalClearanceUserSettings.Current.developerMode)
        {
            throw new InvalidOperationException(
                "Natural output-clearance measurement could not enable developer mode.");
        }
    }

    private void ReapplyNaturalClearanceTransientCheats()
    {
        // Whole-root restore deliberately clears transient debug cheats. The
        // 32-seed natural-clearance portfolio restores the same canonical
        // checkpoint before every seed, so measurement isolation must be
        // re-applied after each restore instead of relying on the initial
        // verifier setup. These switches affect only this verifier-owned
        // measurement scope and RestoreVerificationDebugMode restores the
        // user's original values at teardown.
        if (debugMode == null)
        {
            throw new InvalidOperationException(
                "Natural output-clearance measurement requires debug-mode authority.");
        }

        debugMode.SetCheat(DungeonDebugCheat.FreezeNeeds, true);
        debugMode.SetCheat(DungeonDebugCheat.FriendlyInvincible, true);
        debugMode.SetCheat(DungeonDebugCheat.PreventBreakdowns, true);
        if (!debugMode.IsCheatEnabled(DungeonDebugCheat.FreezeNeeds)
            || !debugMode.IsCheatEnabled(DungeonDebugCheat.FriendlyInvincible)
            || !debugMode.IsCheatEnabled(DungeonDebugCheat.PreventBreakdowns))
        {
            throw new InvalidOperationException(
                "Natural output-clearance measurement transient cheats were not restored.");
        }
    }

    private bool TryValidateNaturalClearanceDecisionIsolation(
        IEnumerable<CharacterActor> actors,
        out string details)
    {
        ICharacterDeprivationQuery deprivation =
            Resolve<ICharacterDeprivationQuery>(FindScope());
        if (deprivation == null)
        {
            details = "deprivation-query-missing";
            return false;
        }

        List<string> states = new();
        bool valid = true;
        foreach (CharacterActor actor in (actors ?? Array.Empty<CharacterActor>())
                     .Where(value => value != null && !value.IsDead)
                     .OrderBy(value => value.BuildingCharacterId.Value,
                         StringComparer.Ordinal))
        {
            bool breakdown = deprivation.HasActiveBreakdown(actor);
            bool mood = actor.Blackboard?.HasActiveMoodImpulse() == true;
            bool macro = actor.Blackboard?.HasActiveMacroGoal() == true;
            string interruptReason = string.Empty;
            bool survivalInterrupt = actor.Brain != null
                && actor.Brain.CanInterruptCurrentActionForSurvivalEmergency(
                    out interruptReason);
            interruptReason ??= string.Empty;
            valid &= !breakdown && !mood && !macro && !survivalInterrupt;
            states.Add(
                $"{actor.BuildingCharacterId.Value}:breakdown={breakdown}:"
                + $"mood={mood}:macro={macro}:survival={survivalInterrupt}:"
                + $"reason={interruptReason}");
        }
        details = states.Count == 0 ? "no-live-actors" : string.Join("|", states);
        return valid && states.Count > 0;
    }

    private bool TryResetNaturalClearanceDecisionState(
        CharacterActor actor,
        ICharacterDeprivationCommand deprivationCommand,
        ICharacterDeprivationQuery deprivationQuery,
        out string details)
    {
        if (actor == null || actor.IsDead)
        {
            details = "actor-missing-or-dead";
            return false;
        }

        StabilizeNaturalClearanceNeeds(actor);
        bool deprivationReset = deprivationCommand?
            .DebugResetForDeterministicScenario(actor) == true;
        actor.Blackboard?.ClearMoodImpulse(
            "natural output-clearance measurement isolation");
        actor.Blackboard?.ClearMacroGoal(
            "natural output-clearance measurement isolation");
        bool breakdown = deprivationQuery?.HasActiveBreakdown(actor) != false;
        bool mood = actor.Blackboard?.HasActiveMoodImpulse() == true;
        bool macro = actor.Blackboard?.HasActiveMacroGoal() == true;
        details = $"actor={actor.BuildingCharacterId.Value}; "
            + $"reset={deprivationReset}; breakdown={breakdown}; "
            + $"mood={mood}; macro={macro}";
        return deprivationReset && !breakdown && !mood && !macro;
    }

    private void StabilizeNaturalClearanceNeeds(CharacterActor actor)
    {
        if (actor?.Stats == null)
            return;
        if (!isolatedNeedStates.ContainsKey(actor))
        {
            isolatedNeedStates.Add(
                actor,
                new Dictionary<CharacterCondition, float>(
                    actor.Stats.StatSnapshot));
        }
        Dictionary<CharacterCondition, float> stableNeeds =
            new(actor.Stats.StatSnapshot)
            {
                [CharacterCondition.HUNGER] = 100f,
                [CharacterCondition.THIRST] = 100f,
                [CharacterCondition.SLEEP] = 100f,
                [CharacterCondition.FUN] = 100f,
                [CharacterCondition.EXCRETION] = 100f,
                [CharacterCondition.HYGIENE] = 100f,
                [CharacterCondition.MOOD] = 100f
            };
        actor.Stats.Stats = stableNeeds;
    }

    private IEnumerator QuiesceNaturalClearanceAiPoolBeforeFixture()
    {
        int activeHaulers = -1;
        int committedHauls = -1;
        int quiesceTurns = 0;
        while (quiesceTurns < NaturalHaulMaximumSchedulingTurnsPerSlice)
        {
            quiesceTurns++;
            EnsureVerificationTimeScale();
            activeHaulers = 0;
            committedHauls = 0;
            foreach (CharacterActor actor in verificationActors)
            {
                if (actor == null || actor.IsDead)
                    continue;

                AbilityHaul haul = actor.GetComponent<AbilityHaul>();
                if (haul?.IsHauling == true)
                {
                    activeHaulers++;
                    // Pausing the scheduler does not stop an already-running
                    // haul coroutine, so it may finish without selecting new
                    // ambient work afterwards.
                    actor.SetAiPaused(true);
                }
                HaulDeliveryIntentSaveData committedIntent =
                    haul?.CaptureDeliveryIntentForSave();
                if (committedIntent != null)
                {
                    committedHauls++;
                    if (haul.IsHauling)
                        continue;

                    if (haul.IsCapacityRoutingQuiescenceFrozen)
                    {
                        Check(false,
                            "OUTPUT_CLEARANCE_PREFIXTURE_FROZEN_HAUL_LEAK",
                            $"actor={actor.BuildingCharacterId.Value}; operation="
                                + $"{committedIntent.operationId}; stage="
                                + $"{haul.CurrentExecutionStage}; operations="
                                + string.Join(",",
                                    haul.CaptureActiveHaulOperationIds()));
                        yield break;
                    }

                    AIBrain brain = actor.Brain;
                    string resumeFailure = string.Empty;
                    bool resumable = haul.HasBoundDeliveryIntent
                        && brain != null
                        && haul.CanStartHauling(out resumeFailure)
                        && brain.PreferActionOnNextDecision<AIHaul>(180f);
                    Check(resumable,
                        "OUTPUT_CLEARANCE_SUSPENDED_HAUL_RESUMABLE",
                        $"actor={actor.BuildingCharacterId.Value}; operation="
                            + $"{committedIntent.operationId}; bound="
                            + $"{haul.HasBoundDeliveryIntent}; stage="
                            + $"{haul.CurrentExecutionStage}; brain="
                            + $"{brain?.HasRunningAction}; reason="
                            + $"{resumeFailure ?? string.Empty}");
                    if (!resumable)
                        yield break;

                    // A retained carried slice is an intentional
                    // delivery-only replan state. Let the normal Brain/AIHaul
                    // path finish that exact intent; direct StartHauling or
                    // intent release would bypass scheduler ownership or
                    // orphan the physical grams.
                    actor.SetAiPaused(false);
                    brain.RequestImmediateDecision(
                        "Output-clearance drain resumes committed haul.");
                    continue;
                }

                actor.SetAiPaused(true);
            }

            if (activeHaulers == 0 && committedHauls == 0)
            {
                foreach (CharacterActor actor in verificationActors)
                {
                    AIBrain brain = actor?.Brain;
                    if (brain?.HasRunningAction == true)
                    {
                        brain.StopCurrentActionForReplan(
                            "output-clearance-prefixture-idle-boundary");
                    }
                }
                bool allIdle = verificationActors.All(actor => actor == null
                    || actor.IsDead
                    || actor.Brain?.HasRunningAction != true
                        && actor.GetComponent<AbilityMove>()?
                            .HasActiveMovementRoutineForDiagnostics != true);
                if (allIdle)
                    break;
            }
            yield return null;
        }

        bool idleBoundary = activeHaulers == 0
            && committedHauls == 0
            && verificationActors.All(actor => actor == null
                || actor.IsDead
                || actor.Brain?.HasRunningAction != true
                    && actor.GetComponent<AbilityMove>()?
                        .HasActiveMovementRoutineForDiagnostics != true);
        Check(idleBoundary,
            "OUTPUT_CLEARANCE_PREFIXTURE_HAULS_DRAINED",
            $"activeHaulers={activeHaulers}; committedHauls={committedHauls}; "
            + $"idle={idleBoundary}; actors={verificationActors.Count}; "
            + $"turns={quiesceTurns}/"
            + $"{NaturalHaulMaximumSchedulingTurnsPerSlice}");
    }

    private void RestoreBrain()
    {
        if (naturalClearanceConsumablesStateCaptured
            && naturalClearanceConsumables != null)
        {
            naturalClearanceConsumables.ConfigureMealDeliveryForDiagnostics(
                naturalClearanceMealDeliveryOriginallyPaused);
        }
        naturalClearanceConsumables = null;
        naturalClearanceConsumablesStateCaptured = false;
        naturalClearanceMealDeliveryOriginallyPaused = false;

        if (naturalClearanceInvasionThreatStateCaptured
            && naturalClearanceInvasionThreat != null)
        {
            naturalClearanceInvasionThreat.enabled =
                naturalClearanceInvasionThreatOriginallyEnabled;
        }
        naturalClearanceInvasionThreat = null;
        naturalClearanceInvasionThreatStateCaptured = false;
        naturalClearanceInvasionThreatOriginallyEnabled = false;

        if (naturalClearanceSpawnerStateCaptured
            && naturalClearanceSpawner != null)
        {
            naturalClearanceSpawner.ConfigureDeterministicSimulationForDiagnostics(
                naturalClearanceSpawnerOriginallyPaused);
        }
        naturalClearanceSpawner = null;
        naturalClearanceSpawnerStateCaptured = false;
        naturalClearanceSpawnerOriginallyPaused = false;

        if (naturalClearanceSchedulerStateCaptured
            && naturalClearanceScheduler != null)
        {
            naturalClearanceScheduler
                .ConfigureDeterministicSimulationForDiagnostics(
                    naturalClearanceSchedulerOriginallyDeterministic);
        }
        naturalClearanceScheduler = null;
        naturalClearanceSchedulerStateCaptured = false;
        naturalClearanceSchedulerOriginallyDeterministic = false;

        foreach (KeyValuePair<AIBrain, bool> pair
                 in isolatedLogisticsMeasurementStates)
        {
            if (pair.Key != null)
            {
                pair.Key.ConfigureLogisticsMeasurementForDiagnostics(
                    pair.Value);
            }
        }
        foreach (KeyValuePair<CharacterActor,
                     Dictionary<CharacterCondition, float>> pair
                 in isolatedNeedStates)
        {
            if (pair.Key?.Stats != null)
                pair.Key.Stats.Stats = pair.Value;
        }
        foreach (KeyValuePair<CharacterActor, bool> pair in isolatedAiPauseStates)
        {
            if (pair.Key != null)
            {
                pair.Key.SetAiPaused(pair.Value);
            }
        }
        isolatedLogisticsMeasurementStates.Clear();
        isolatedNeedStates.Clear();
        isolatedAiPauseStates.Clear();
        verificationActors.Clear();

        if (naturalClearanceUserSettingsStateCaptured
            && naturalClearanceUserSettings != null
            && naturalClearanceUserSettings.Current.developerMode
                != naturalClearanceDeveloperModeOriginallyEnabled)
        {
            naturalClearanceUserSettings.Update(value =>
                value.developerMode =
                    naturalClearanceDeveloperModeOriginallyEnabled);
        }
        naturalClearanceUserSettings = null;
        naturalClearanceUserSettingsStateCaptured = false;
        naturalClearanceDeveloperModeOriginallyEnabled = false;
    }

    private void DisableNaturalClearanceCheckpointTime()
    {
        naturalClearanceCheckpointClock?.DisableDeterministicCheckpointTime();
        naturalClearanceCheckpointClock = null;
    }

    private void ConfigureVerificationDebugMode()
    {
        originalFreezeNeeds = debugMode.IsCheatEnabled(DungeonDebugCheat.FreezeNeeds);
        originalFriendlyInvincible = debugMode.IsCheatEnabled(DungeonDebugCheat.FriendlyInvincible);
        originalPreventBreakdowns = debugMode.IsCheatEnabled(
            DungeonDebugCheat.PreventBreakdowns);
        debugMode.SetCheat(DungeonDebugCheat.FreezeNeeds, true);
        debugMode.SetCheat(DungeonDebugCheat.FriendlyInvincible, true);
        if (naturalClearanceOnly)
            debugMode.SetCheat(DungeonDebugCheat.PreventBreakdowns, true);
    }

    private void RestoreVerificationDebugMode()
    {
        if (debugMode == null)
        {
            return;
        }

        debugMode.SetCheat(DungeonDebugCheat.FreezeNeeds, originalFreezeNeeds);
        debugMode.SetCheat(DungeonDebugCheat.FriendlyInvincible, originalFriendlyInvincible);
        debugMode.SetCheat(
            DungeonDebugCheat.PreventBreakdowns,
            originalPreventBreakdowns);
    }

    private Facility CreateInjectedFacility(
        DungeonRuntimeLifetimeScope scope,
        Grid grid,
        BuildingSO asset,
        Vector2Int position,
        string objectName,
        bool registerOnGrid = false)
    {
        if (asset == null)
        {
            return null;
        }

        GameObject obj = new GameObject(objectName);
        temporaryObjects.Add(obj);
        Facility facility = obj.AddComponent<Facility>();
        InjectGameObject(scope, obj);
        facility.SetGrid(grid);
        facility.Initialization(asset, position);
        if (registerOnGrid
            && (grid == null
                || !grid.RegisterOccupant(
                    facility,
                    asset.Placement.Layer,
                    asset.GetGridPosList(position),
                    asset.Placement.IsMovement)))
        {
            temporaryObjects.Remove(obj);
            Destroy(obj);
            return null;
        }
        Vector3 world = grid != null ? grid.GetWorldPos(position) : (Vector3)(Vector2)position;
        obj.transform.position = new Vector3(world.x, world.y, obj.transform.position.z);
        return facility;
    }

    private void RegisterTemporaryWarehouse(
        DungeonRuntimeLifetimeScope scope,
        IWarehouseFacility warehouse)
    {
        if (warehouse == null)
            return;
        ICharacterAiWorldRegistry registry = Resolve<ICharacterAiWorldRegistry>(scope);
        if (registry == null)
            throw new InvalidOperationException(
                "Temporary warehouse requires the live character AI world registry.");
        registry.RegisterWarehouse(warehouse);
        temporaryWarehouseRegistrations.Add((registry, warehouse));
    }

    private bool UnregisterTemporaryWarehouse(IWarehouseFacility warehouse)
    {
        if (warehouse == null)
            return false;
        int index = temporaryWarehouseRegistrations.FindIndex(value =>
            ReferenceEquals(value.Warehouse, warehouse));
        if (index < 0)
            return false;
        (ICharacterAiWorldRegistry registry, IWarehouseFacility registered) =
            temporaryWarehouseRegistrations[index];
        registry?.UnregisterWarehouse(registered);
        temporaryWarehouseRegistrations.RemoveAt(index);
        return true;
    }

    private static bool TryFindRegisterablePosition(
        Grid grid,
        BuildingSO asset,
        IReadOnlyList<Vector2Int> candidates,
        out Vector2Int position)
    {
        position = default;
        if (grid == null || asset == null || candidates == null)
        {
            return false;
        }

        foreach (Vector2Int candidate in candidates)
        {
            IReadOnlyList<Vector2Int> footprint = asset.GetGridPosList(candidate);
            if (footprint.All(cell => grid.GetGridCell(cell)?.CanOccupy(
                    asset.Placement.Layer) == true))
            {
                position = candidate;
                return true;
            }
        }
        return false;
    }

    private static BuildingSO FindWarehouseAsset()
    {
        return FindBuildingAsset(asset => asset.GetStorageCapacity() > 0 && asset.StoresAllCategories())
            ?? AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/Resources/SO/Building/P1/P1_Warehouse.asset");
    }

    private static BuildingSO FindCraftBenchAsset()
    {
        return FindBuildingAsset(asset =>
        {
            BuildingEquipmentCraftingAbility ability = asset.GetAbility<BuildingEquipmentCraftingAbility>();
            return ability != null && ability.CraftableEquipmentIds.Contains(DaggerId, StringComparer.Ordinal);
        });
    }

    private static BuildingSO CreateMaintenanceAsset()
    {
        BuildingSO asset = ScriptableObject.CreateInstance<BuildingSO>();
        asset.id = 99122;
        asset.objectName = "QA 장비 수리대";
        asset.width = 1;
        asset.height = 1;
        asset.layer = GridLayer.Building;
        asset.category = BuildingCategory.Production;
        asset.unlocked = true;
        asset.Facility = new FacilityData
        {
            roles = FacilityRole.Logistics,
            capacity = 1,
            useDuration = 1.5f,
            requiredWorkers = 1,
            disabledWhenDamaged = true
        };
        asset.Facility.SetSupportedWorkTypeIds(new[] { BuiltInWorkTypeIds.Repair });
        asset.AbilityModules.Add(new BuildingEquipmentMaintenanceAbility
        {
            workSpeedMultiplier = 1f,
            simultaneousRepairSlots = 1
        });
        return asset;
    }

    private static bool SeedStoredCraftMaterial(
        IWorldItemStackRuntime itemRuntime,
        IResourceEconomyContentCatalog economyCatalog,
        Facility warehouse,
        string materialId,
        int amount,
        out string details)
    {
        details = string.Empty;
        if (itemRuntime == null
            || economyCatalog == null
            || warehouse == null
            || !economyCatalog.TryGetMaterial(
                materialId,
                out CraftMaterialDefinitionSO material))
        {
            details = $"material missing: {materialId}";
            return false;
        }

        string destinationId = WarehouseStorageIdentity.RequireDestinationId(warehouse);
        bool spawned = itemRuntime.SpawnItemAt(
            material.ItemId,
            amount,
            warehouse.centerPos,
            WorldItemStackState.Stored,
            destinationId,
            out int spawnedAmount);
        details = $"item={material.ItemId}; amount={spawnedAmount}; destination={destinationId}";
        return spawned && spawnedAmount == amount;
    }

    private static BuildingSO FindBuildingAsset(Func<BuildingSO, bool> predicate)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:BuildingSO", new[] { "Assets/Resources/SO/Building" }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            BuildingSO asset = AssetDatabase.LoadAssetAtPath<BuildingSO>(path);
            if (asset != null && predicate(asset))
            {
                return asset;
            }
        }

        return null;
    }

    private static IReadOnlyList<Vector2Int> FindReachableCells(Grid grid, Vector2Int actorPos, int count)
    {
        return grid.SearchPath(actorPos)
            .GetReachablePositions()
            .Where(pos => grid.IsValidGridPos(pos) && grid.IsWalkable(pos))
            .Where(pos => Mathf.Abs(pos.x - actorPos.x) + Mathf.Abs(pos.y - actorPos.y) <= 12)
            .Distinct()
            .OrderBy(pos => Mathf.Abs(pos.x - actorPos.x) + Mathf.Abs(pos.y - actorPos.y))
            .Skip(1)
            .Take(count)
            .ToArray();
    }

    private static CharacterActor FindHauler()
    {
        return CharacterActorCollection.DistinctByGameObject(
                UnityEngine.Object.FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None))
            .Where(actor => actor != null && !actor.IsDead)
            .OrderByDescending(actor => actor.TryGetAbility(out AbilityWork _))
            .ThenBy(actor => actor.Identity != null && actor.Identity.Role == CharacterRole.Owner ? 1 : 0)
            .FirstOrDefault(actor =>
                actor.TryGetAbility(out AbilityMove _)
                && (actor.TryGetAbility(out AbilityWork _)
                    || actor.Identity != null && actor.Identity.Role == CharacterRole.Owner));
    }

    private static void ClearInventory(WarehouseInventory inventory)
    {
        if (inventory == null)
        {
            return;
        }

        foreach (KeyValuePair<StockCategory, int> pair in inventory.EnumerateStock().ToArray())
        {
            inventory.ConsumePhysicalStockForTest(pair.Key, pair.Value);
        }
    }

    private static int GetTotalWarehouseStock(StockCategory category)
    {
        return UnityEngine.Object.FindObjectsByType<MonoBehaviour>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .OfType<IWarehouseFacility>()
            .Where(facility => facility != null && facility.Inventory != null)
            .Select(facility => facility.Inventory)
            .Distinct()
            .Sum(inventory => inventory.GetStock(category));
    }

    private static int GetStoredItemQuantity(
        IWorldItemStackRuntime itemRuntime,
        string itemId,
        Vector2Int warehousePosition)
    {
        return itemRuntime?.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.Stored
                && stack.Position == warehousePosition
                && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal))
            .Sum(stack => Mathf.Max(0, stack.Quantity)) ?? 0;
    }

    private static string DescribeStoredItemContents(
        IWorldItemStackRuntime itemRuntime,
        Vector2Int warehousePosition)
    {
        string[] contents = itemRuntime?.GetAllStacks()
            .Where(stack => stack != null
                && stack.State == WorldItemStackState.Stored
                && stack.Position == warehousePosition
                && stack.Quantity > 0)
            .OrderBy(stack => stack.ItemId, StringComparer.Ordinal)
            .ThenBy(stack => stack.StackId, StringComparer.Ordinal)
            .Select(stack => stack.ItemId + ":" + stack.Quantity + ":"
                + stack.StackId)
            .ToArray() ?? Array.Empty<string>();
        return contents.Length == 0 ? "none" : string.Join("|", contents);
    }

    private string DescribeHaulState(IWorldItemStackRuntime itemRuntime, CharacterActor hauler)
    {
        AbilityHaul haul = hauler != null ? hauler.GetComponent<AbilityHaul>() : null;
        IWorldItemHaulPlanningService planning = Resolve<IWorldItemHaulPlanningService>(FindScope());
        string preview = "unavailable";
        if (planning != null && hauler != null)
        {
            bool available = planning.TryPreviewBestPlan(
                hauler,
                out WorldItemHaulPlan previewPlan,
                out string previewFailure);
            preview = available
                ? DescribePreviewPlan(previewPlan)
                : previewFailure;
        }
        string canStartReason = "unavailable";
        bool canStart = haul != null && haul.CanStartHauling(out canStartReason);
        bool runtimeAvailable = hauler != null && itemRuntime?.HasAvailableHaulJob(hauler) == true;
        return $"actor={hauler?.name}; pos={hauler?.GetNowXY().ToString() ?? "<none>"}; "
            + $"phase={hauler?.Brain?.CurrentActionPhase ?? "<none>"}"
            + $"/{hauler?.Brain?.CurrentActionPhaseDetail ?? "<none>"}; "
            + $"haul={haul?.CurrentPlanSummary ?? "<none>"}; "
            + $"unload={haul?.CurrentUnloadReason ?? "<none>"}; "
            + $"haulFailure={haul?.LastFailureReason ?? "<none>"}; "
            + $"path={haul?.ActivePathDebug ?? "<none>"}; "
            + $"preview={preview}; "
            + $"runtimeAvailable={runtimeAvailable}; canStart={canStart}:{canStartReason}; "
            + $"brainFailure={hauler?.Brain?.LastActionFailure.ToString() ?? "<none>"}; "
            + $"carry={DescribeCarry(hauler, itemRuntime)}; "
            + $"stacks={DescribeStacks(itemRuntime)}";
    }

    private static string DescribePreviewPlan(WorldItemHaulPlan plan)
    {
        if (plan == null)
        {
            return "null-plan";
        }

        string reservations = string.Join(
            ",",
            plan.ReservedStackQuantities.Select(reservation =>
                $"{reservation.StackId}:{reservation.ItemId}x{reservation.Quantity}"
                + $"->{reservation.DestinationKind}:{reservation.DestinationId}"));
        return $"valid={plan.IsValid},priority={plan.IsPriority},weight={plan.TotalWeight:0.###},"
            + $"destination={plan.PrimaryDestination}:{plan.PrimaryDestinationId},"
            + $"reservations=[{reservations}]";
    }

    private static string DescribeCarry(CharacterActor hauler, IWorldItemStackRuntime itemRuntime)
    {
        CharacterCarryInventory carry = hauler != null ? CharacterCarryInventory.Ensure(hauler) : null;
        if (carry == null)
        {
            return "none";
        }

        string itemSummary = string.Join(",", carry.Items.Select(
            item => $"{item.itemId}x{item.quantity}"));
        return $"{carry.GetCurrentWeight(itemRuntime?.CatalogProvider):0.##}/"
            + $"{carry.GetBaseCarryLimit():0.##}/"
            + $"{carry.GetMaxAllowedWeight(itemRuntime?.HaulingSettingsProvider):0.##}kg"
            + (itemSummary.Length > 0 ? " " + itemSummary : string.Empty);
    }

    private static string DescribeStacks(IWorldItemStackRuntime itemRuntime)
    {
        if (itemRuntime == null)
        {
            return "no runtime";
        }

        return string.Join(" | ", itemRuntime.GetAllStacks()
            .Take(12)
            .Select(stack => $"{stack.StackId}:{stack.ItemId}x{stack.Quantity}:{stack.State}:"
                + $"dest={stack.DestinationId}:src={stack.SourceStorageDestinationId}:pos={stack.Position}"));
    }

    private static string DescribeWarehouse(IWarehouseFacility warehouse) =>
        warehouse is BuildableObject building
            ? $"{warehouse.PersistentInstanceId.Value}@{building.centerPos}"
            : warehouse?.PersistentInstanceId.Value ?? "missing";

    private IEnumerator CaptureScreen(string path)
    {
        yield return PlayModeVerificationFrameWait.CaptureReady();
        Texture2D capture = PlayModeVerificationFrameWait.CaptureScreenshotAsTexture();
        if (capture == null)
        {
            Check(false, "SCREEN_CAPTURE", "capture returned null");
            yield break;
        }

        byte[] bytes = capture.EncodeToPNG();
        File.WriteAllBytes(path, bytes);
        Check(bytes.Length > 1000, "SCREEN_CAPTURE_NONBLANK", $"{path}; bytes={bytes.Length}");
        Destroy(capture);
    }

    private void SetupInput()
    {
        originalMouse = Mouse.current;
        if (originalMouse != null)
        {
            InputSystem.DisableDevice(originalMouse);
        }

        CreateVerificationMouse();
    }

    private void CreateVerificationMouse()
    {
        verificationMouse = InputSystem.AddDevice<Mouse>($"PhysicalItemLogisticsVerificationMouse{++verificationMouseSerial}");
        InputSystem.EnableDevice(verificationMouse);
        verificationMouse.MakeCurrent();
        InputState.Change(verificationMouse, new MouseState { position = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f) });
        InputSystem.Update();
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current != null)
        {
            return;
        }

        new GameObject("QA_EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
    }

    private static string GetVisibleTextSample()
    {
        return string.Join(" || ", Resources.FindObjectsOfTypeAll<TMP_Text>()
            .Where(text => text != null
                && text.gameObject.scene.IsValid()
                && text.gameObject.activeInHierarchy
                && !string.IsNullOrWhiteSpace(text.text))
            .Select(text => Compact(text.text))
            .Take(16));
    }

    private static void InjectGameObject(DungeonRuntimeLifetimeScope scope, GameObject target)
    {
        if (scope == null || scope.Container == null || target == null)
        {
            return;
        }

        foreach (MonoBehaviour component in target.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component != null)
            {
                scope.Container.Inject(component);
            }
        }
    }

    private sealed class SingleWarehouseWorldRegistry : ICharacterAiWorldRegistry
    {
        private readonly ICharacterAiWorldRegistry inner;
        private readonly IReadOnlyList<IWarehouseFacility> warehouses;

        internal SingleWarehouseWorldRegistry(
            ICharacterAiWorldRegistry inner,
            IWarehouseFacility warehouse)
        {
            this.inner = inner ?? throw new ArgumentNullException(nameof(inner));
            warehouses = warehouse == null
                ? Array.Empty<IWarehouseFacility>()
                : new[] { warehouse };
        }

        public int Version => inner.Version;
        public int CharacterVersion => inner.CharacterVersion;
        public int LifetimeCharacterVersion => inner.LifetimeCharacterVersion;
        public int WildlifeVersion => inner.WildlifeVersion;
        public int BuildingVersion => inner.BuildingVersion;
        public int WarehouseVersion => unchecked(inner.WarehouseVersion + 1);
        public int RetailVersion => inner.RetailVersion;
        public IReadOnlyList<CharacterActor> Characters => inner.Characters;
        public IReadOnlyList<CharacterActor> AllCharacters => inner.AllCharacters;
        public IReadOnlyList<WildlifeActor> Wildlife => inner.Wildlife;
        public IReadOnlyList<BuildableObject> Buildings => inner.Buildings;
        public IReadOnlyList<IWarehouseFacility> Warehouses => warehouses;
        public IReadOnlyList<IRetailFacility> RetailFacilities =>
            inner.RetailFacilities;

        public void RegisterCharacter(CharacterActor actor) =>
            inner.RegisterCharacter(actor);
        public void UnregisterCharacter(CharacterActor actor) =>
            inner.UnregisterCharacter(actor);
        public void RegisterCharacterLifetime(CharacterActor actor) =>
            inner.RegisterCharacterLifetime(actor);
        public void UnregisterCharacterLifetime(CharacterActor actor) =>
            inner.UnregisterCharacterLifetime(actor);
        public void RegisterWildlife(WildlifeActor actor) =>
            inner.RegisterWildlife(actor);
        public void UnregisterWildlife(WildlifeActor actor) =>
            inner.UnregisterWildlife(actor);
        public void RegisterBuilding(BuildableObject building) =>
            inner.RegisterBuilding(building);
        public void UnregisterBuilding(BuildableObject building) =>
            inner.UnregisterBuilding(building);
        public int ReleaseTransientBuildingOwnership(
            IBuildingVisitorPort visitor,
            string reason) =>
            inner.ReleaseTransientBuildingOwnership(visitor, reason);
        public int GetTransientBuildingOwnershipCount(CharacterId characterId) =>
            inner.GetTransientBuildingOwnershipCount(characterId);
        public void RegisterWarehouse(IWarehouseFacility warehouse) =>
            inner.RegisterWarehouse(warehouse);
        public void UnregisterWarehouse(IWarehouseFacility warehouse) =>
            inner.UnregisterWarehouse(warehouse);
        public void SetGrid(Grid grid) => inner.SetGrid(grid);
        public bool TryGetGrid(out Grid grid) => inner.TryGetGrid(out grid);
        public bool TryGetSessionState(out GameSessionState data) =>
            inner.TryGetSessionState(out data);
        public void Clear() => inner.Clear();
    }

    private T Resolve<T>(DungeonRuntimeLifetimeScope scope) where T : class
    {
        try
        {
            return scope != null && scope.Container != null ? scope.Container.Resolve<T>() : null;
        }
        catch (Exception exception)
        {
            report.Add(
                $"[DI-ERROR] RESOLVE {typeof(T).FullName}: "
                + $"{exception.GetType().Name}: {exception.Message}");
            return null;
        }
    }

    private static bool TryFindReadyComposition(
        out DungeonRuntimeLifetimeScope scope,
        out OwnerRunManager ownerManager,
        out string detail)
    {
        scope = FindScope();
        ownerManager = UnityEngine.Object.FindFirstObjectByType<OwnerRunManager>();
        if (scope == null || scope.Container == null)
        {
            detail = "gameplay LifetimeScope/container is not ready";
            return false;
        }
        if (ownerManager == null)
        {
            detail = "authored OwnerRunManager is not ready";
            return false;
        }

        try
        {
            IOwnerRunManagerProvider provider =
                scope.Container.Resolve<IOwnerRunManagerProvider>();
            if (provider == null
                || !provider.TryGetManager(out OwnerRunManager provided)
                || !ReferenceEquals(ownerManager, provided))
            {
                detail = "owner provider does not expose the authored manager";
                return false;
            }
        }
        catch (Exception exception)
        {
            detail = $"owner provider resolve pending: {exception.GetType().Name}: {exception.Message}";
            return false;
        }

        detail = "scope/container and authored owner provider match";
        return true;
    }

    private static int CountPreparedStaff() => CharacterActorCollection
        .DistinctByGameObject(UnityEngine.Object.FindObjectsByType<CharacterActor>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None))
        .Count(actor => actor != null
            && !actor.IsDead
            && actor.Identity != null
            && actor.Identity.PersistentId.StartsWith(
                "character:staff:",
                StringComparison.Ordinal));

    private static DungeonRuntimeLifetimeScope FindScope()
    {
        return UnityEngine.Object.FindObjectsByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(scope => scope != null && scope.Container != null);
    }

    private bool Check(bool condition, string key, string detail)
    {
        string prefix = $"[{(condition ? "PASS" : "FAIL")}] {key}";
        string line = string.IsNullOrEmpty(detail)
            ? prefix
            : $"{prefix} {detail}";
        report.Add(line);
        Debug.Log("V27_LOGISTICS_STEP " + line);
        if (!condition)
        {
            failures.Add($"{key}: {detail}");
        }

        return condition;
    }

    private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Warning)
        {
            capturedWarnings.Add(condition);
        }
        else if (type == LogType.Error || type == LogType.Exception || type == LogType.Assert)
        {
            capturedErrors.Add(string.IsNullOrWhiteSpace(stackTrace)
                ? condition
                : condition + "\n" + stackTrace);
        }
    }

    private void Finish()
    {
        Cleanup();
        Application.logMessageReceived -= OnLogMessageReceived;
        report.Add($"capturedErrors={capturedErrors.Count}; {Compact(capturedErrors)}");
        report.Add($"capturedWarnings={capturedWarnings.Count}; {Compact(capturedWarnings)}");
        bool passed = failures.Count == 0 && capturedErrors.Count == 0 && capturedWarnings.Count == 0;
        report.Add("currentSourceDigest="
            + V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest());
        report.Add("gameplaySceneSha256="
            + V27CurrentSourceEvidenceDigest.ComputeGameplaySceneDigest());
        if (naturalOutputPortfolioOnly)
        {
            report.Add("naturalRunnerSchema="
                + "v27-production-output-clearance-natural-runner@2");
            report.Add("naturalMinimumV27Plans="
                + ProductionOutputClearanceNaturalPortfolioCoordinator
                    .MinimumV27BaselinePlanCount.ToString(
                        CultureInfo.InvariantCulture));
            report.Add("naturalPlanCount="
                + naturalOutputPortfolioPlanCount.ToString(
                    CultureInfo.InvariantCulture));
            report.Add("naturalPortfolioReportSha256=" + ComputeShaOrMissing(
                ProductionOutputClearanceNaturalPortfolioCoordinator.ReportPath));
            report.Add("naturalObservationsCsvSha256=" + ComputeShaOrMissing(
                ProductionOutputClearanceNaturalPortfolioCoordinator
                    .ObservationsCsvPath));
            report.Add("naturalOutputSlicesCsvSha256=" + ComputeShaOrMissing(
                ProductionOutputClearanceNaturalPortfolioCoordinator
                    .OutputSlicesCsvPath));
        }
        report.Add($"RESULT={(passed ? "PASS" : "FAIL")}; failures={failures.Count}; {Compact(failures)}");
        string reportPath = naturalOutputPortfolioOnly
            ? PhysicalItemLogisticsPlayModeVerifier
                .NaturalOutputPortfolioRunnerReportPath
            : l02Only
            ? PhysicalItemLogisticsPlayModeVerifier.L02ReportPath
            : productionInputMassOnly
                ? PhysicalItemLogisticsPlayModeVerifier.ProductionInputMassReportPath
            : preparedOutputWarehouseOnly
                ? string.IsNullOrWhiteSpace(preparedOutputCase.ReportPath)
                    ? PhysicalItemLogisticsPlayModeVerifier
                        .PreparedOutputWarehouseReportPath
                    : preparedOutputCase.ReportPath
            : equipmentRepairOnly
                ? PhysicalItemLogisticsPlayModeVerifier.EquipmentRepairReportPath
            : constructionOnly
                ? PhysicalItemLogisticsPlayModeVerifier.ConstructionReportPath
                : PhysicalItemLogisticsPlayModeVerifier.ReportPath;
        File.WriteAllText(reportPath, string.Join("\n", report));
        string completedRequestPath = naturalOutputPortfolioOnly
            ? PhysicalItemLogisticsPlayModeVerifier.NaturalOutputPortfolioRequestPath
            : l02Only
                ? PhysicalItemLogisticsPlayModeVerifier.L02RequestPath
                : productionInputMassOnly
                    ? PhysicalItemLogisticsPlayModeVerifier
                        .ProductionInputMassRequestPath
                    : preparedOutputWarehouseOnly
                        ? PhysicalItemLogisticsPlayModeVerifier
                            .PreparedOutputWarehouseRequestPath
                        : equipmentRepairOnly
                            ? PhysicalItemLogisticsPlayModeVerifier
                                .EquipmentRepairRequestPath
                            : constructionOnly
                                ? PhysicalItemLogisticsPlayModeVerifier
                                    .ConstructionRequestPath
                                : PhysicalItemLogisticsPlayModeVerifier.RequestPath;
        File.Delete(completedRequestPath);

        if (passed)
        {
            Debug.Log("Physical item logistics PlayMode verification passed. "
                + reportPath);
        }
        else
        {
            Debug.LogError("Physical item logistics PlayMode verification failed. "
                + reportPath);
        }

        EditorApplication.ExitPlaymode();
    }

    private static void Destroy(UnityEngine.Object target) =>
        UnityEngine.Object.Destroy(target);

    private static string ComputeShaOrMissing(string path) =>
        File.Exists(path)
            ? V27BalanceArtifactWriter.ComputeSha256(path)
            : "MISSING";

    private void Cleanup()
    {
        RestoreVerificationDebugMode();
        foreach ((ICharacterAiWorldRegistry registry,
                     IWarehouseFacility warehouse) in temporaryWarehouseRegistrations)
        {
            registry?.UnregisterWarehouse(warehouse);
        }
        temporaryWarehouseRegistrations.Clear();
        foreach (GameObject obj in temporaryObjects)
        {
            if (obj != null)
            {
                Destroy(obj);
            }
        }

        temporaryObjects.Clear();
        if (verificationMouse != null && verificationMouse.added)
        {
            InputSystem.RemoveDevice(verificationMouse);
        }

        if (originalMouse != null && originalMouse.added)
        {
            InputSystem.EnableDevice(originalMouse);
            originalMouse.MakeCurrent();
        }

        Time.timeScale = originalTimeScale;
        DisableNaturalClearanceCheckpointTime();
        RestoreBrain();
    }

    private static string Compact(IEnumerable<string> values)
    {
        return Compact(string.Join(" | ", values ?? Array.Empty<string>()));
    }

    private static string Compact(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "<none>";
        }

        return value.Replace("\r", " ").Replace("\n", " ").Trim();
    }
}
#endif
