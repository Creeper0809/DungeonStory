using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

public static class DefenseEngagementPlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/defense-engagement-playmode.txt";
    public const string Wim048ReportPath =
        "Artifacts/QA/wim-implementation/wim-048-resident-room-evacuation.txt";
    private const string PendingFlagPath =
        "Temp/defense-engagement-playmode.flag";
    private const string Wim048PendingFlagPath =
        "Temp/wim-048-resident-room-evacuation.flag";
    private const string GameplayScenePath = "Assets/Scenes/GameplayScene.unity";
    private const int PendingSchema = 1;
    private static readonly TimeSpan PendingMaximumAge = TimeSpan.FromMinutes(30);
    private const float StartupTimeoutSeconds = 15f;
    private const float EngagementTimeoutSeconds = 180f;
    private static string lastReport = "방어 교전 PlayMode 검증을 실행하지 않았습니다.";
    private static bool completed;
    private static CharacterActor policyProbeOldLead;
    private static InvasionIntruderRuntime policyProbeIntruder;
    private static Vector2Int policyProbeIntruderCell;
    private static int policyProbeFacilityDamage;
    private static bool policyUiProbeCompleted;
    private static string policyUiProbeReport = "방어 정책 UI 포인터 검증을 실행하지 않았습니다.";
    private static CharacterActor ownerFinalProbeOwner;
    private static InvasionIntruderRuntime ownerFinalProbeIntruder;
    private static float ownerFinalProbeOwnerHealth;
    private static float ownerFinalProbeIntruderHealth;
    private static bool ownerEvacuationProbeDurabilityBoosted;
    private static string wim048Report =
        "WIM-048 주민 방 대피 검증을 실행하지 않았습니다.";
    private static bool wim048Completed;

    [MenuItem("DungeonStory/Debug/Invasion/Start Defense Engagement PlayMode Verification")]
    public static void StartFromMenu()
    {
        if (!Application.isPlaying)
        {
            if (!TryCaptureCleanSceneRequest(
                    out PendingRunRequest request,
                    out string failureReason))
            {
                DeletePendingFlag();
                lastReport = "FAIL: " + failureReason;
                completed = true;
                WriteImmediateFailure(lastReport);
                return;
            }
            Directory.CreateDirectory("Temp");
            File.WriteAllLines(PendingFlagPath, request.Serialize());
            EditorApplication.EnterPlaymode();
            return;
        }

        RejectUnauthorisedRuntimeStart(
            "EditMode clean-scene preflight evidence is required.");
    }

    [MenuItem("DungeonStory/Debug/Invasion/Run WIM-048 Resident Room Evacuation Focused")]
    public static void RunWim048ResidentRoomEvacuationFocused()
    {
        if (!Application.isPlaying)
        {
            if (!TryCaptureCleanSceneRequest(
                    out PendingRunRequest request,
                    out string failureReason))
            {
                DeleteWim048PendingFlag();
                FinishWim048Immediately(false, failureReason);
                return;
            }

            Directory.CreateDirectory("Temp");
            File.WriteAllLines(Wim048PendingFlagPath, request.Serialize());
            EditorApplication.EnterPlaymode();
            return;
        }

        FinishWim048Immediately(
            false,
            "EditMode clean-scene preflight evidence is required.");
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapPendingRun()
    {
        if (!File.Exists(PendingFlagPath))
            return;
        PendingRunRequest request = default;
        string failureReason = string.Empty;
        try
        {
            if (!PendingRunRequest.TryParse(
                    File.ReadAllLines(PendingFlagPath),
                    out request,
                    out failureReason)
                || !request.ValidateRuntime(out failureReason))
            {
                lastReport = "FAIL: " + failureReason;
                completed = true;
                WriteImmediateFailure(lastReport);
                return;
            }
        }
        finally
        {
            DeletePendingFlag();
        }
        StartRuntimeProbe(request);
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapWim048PendingRun()
    {
        if (!File.Exists(Wim048PendingFlagPath))
        {
            return;
        }

        PendingRunRequest request = default;
        string failureReason = string.Empty;
        try
        {
            if (!PendingRunRequest.TryParse(
                    File.ReadAllLines(Wim048PendingFlagPath),
                    out request,
                    out failureReason)
                || !request.ValidateRuntime(out failureReason))
            {
                FinishWim048Immediately(false, failureReason);
                return;
            }
        }
        finally
        {
            DeleteWim048PendingFlag();
        }

        foreach (Wim048Runner existing in
                 UnityEngine.Object.FindObjectsByType<Wim048Runner>(
                     FindObjectsInactive.Include,
                     FindObjectsSortMode.None))
        {
            if (existing != null)
            {
                UnityEngine.Object.Destroy(existing.gameObject);
            }
        }

        wim048Completed = false;
        wim048Report = "RUNNING: WIM-048 런타임 준비 중";
        Directory.CreateDirectory(Path.GetDirectoryName(Wim048ReportPath)
            ?? "Artifacts/QA/wim-implementation");
        if (File.Exists(Wim048ReportPath))
        {
            File.Delete(Wim048ReportPath);
        }
        Time.timeScale = 1f;
        GameObject root = new GameObject("WIM-048 Resident Room Evacuation Verifier");
        Wim048Runner runner = root.AddComponent<Wim048Runner>();
        runner.SetCleanBootEvidence(request);
    }

    public static string StartRuntimeProbe()
    {
        return RejectUnauthorisedRuntimeStart(
            Application.isPlaying
                ? "EditMode clean-scene preflight evidence is required."
                : "PlayMode가 아닙니다.");
    }

    private static string StartRuntimeProbe(PendingRunRequest request)
    {
        if (!Application.isPlaying)
            return RejectUnauthorisedRuntimeStart("PlayMode가 아닙니다.");

        foreach (Runner existing in UnityEngine.Object.FindObjectsByType<Runner>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None))
        {
            if (existing != null)
            {
                UnityEngine.Object.Destroy(existing.gameObject);
            }
        }

        completed = false;
        lastReport = "RUNNING: 게임 초기화를 기다리는 중";
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)
            ?? "Artifacts/QA");
        if (File.Exists(ReportPath))
        {
            File.Delete(ReportPath);
        }
        ownerEvacuationProbeDurabilityBoosted = false;
        Time.timeScale = 1f;
        GameObject root = new GameObject("Defense Engagement PlayMode Verifier");
        Runner runner = root.AddComponent<Runner>();
        runner.SetCleanBootEvidence(request);
        return lastReport;
    }

    private static string RejectUnauthorisedRuntimeStart(string reason)
    {
        lastReport = "FAIL: " + (reason ?? string.Empty);
        completed = true;
        WriteImmediateFailure(lastReport);
        return lastReport;
    }

    private static bool TryCaptureCleanSceneRequest(
        out PendingRunRequest request,
        out string failureReason)
    {
        request = default;
        failureReason = string.Empty;
        Scene active = SceneManager.GetActiveScene();
        if (!active.IsValid()
            || !active.isLoaded
            || !string.Equals(
                active.path,
                GameplayScenePath,
                StringComparison.Ordinal))
        {
            failureReason = "The canonical GameplayScene must be active before verification.";
            return false;
        }
        if (SceneManager.sceneCount != 1)
        {
            failureReason = "Exactly one loaded scene is required before verification.";
            return false;
        }
        for (int index = 0; index < SceneManager.sceneCount; index++)
        {
            Scene loaded = SceneManager.GetSceneAt(index);
            if (loaded.IsValid() && loaded.isLoaded && loaded.isDirty)
            {
                failureReason = "Loaded scenes contain unsaved changes; verification did not save or discard them.";
                return false;
            }
        }
        request = new PendingRunRequest(
            DateTime.UtcNow.Ticks,
            GameplayScenePath,
            SceneManager.sceneCount,
            sceneDirty: false);
        return true;
    }

    private static void DeletePendingFlag()
    {
        if (File.Exists(PendingFlagPath))
            File.Delete(PendingFlagPath);
    }

    private static void DeleteWim048PendingFlag()
    {
        if (File.Exists(Wim048PendingFlagPath))
        {
            File.Delete(Wim048PendingFlagPath);
        }
    }

    public static string GetWim048ResidentRoomEvacuationReport() =>
        $"completed={wim048Completed}; {wim048Report}";

    private static void FinishWim048Immediately(bool success, string detail)
    {
        wim048Completed = true;
        wim048Report = $"{(success ? "PASS" : "FAIL")}: {detail ?? string.Empty}";
        Directory.CreateDirectory(Path.GetDirectoryName(Wim048ReportPath)
            ?? "Artifacts/QA/wim-implementation");
        File.WriteAllLines(Wim048ReportPath, new[]
        {
            "# WIM-048 resident room evacuation focused verification",
            "result=" + (success ? "PASS" : "FAIL"),
            "utc=" + DateTime.UtcNow.ToString("O"),
            "terminal=" + wim048Report
        });
    }

    private readonly struct PendingRunRequest
    {
        internal PendingRunRequest(
            long requestedUtcTicks,
            string scenePath,
            int sceneCount,
            bool sceneDirty)
        {
            RequestedUtcTicks = requestedUtcTicks;
            ScenePath = scenePath ?? string.Empty;
            SceneCount = sceneCount;
            SceneDirty = sceneDirty;
        }

        internal long RequestedUtcTicks { get; }
        internal string ScenePath { get; }
        internal int SceneCount { get; }
        internal bool SceneDirty { get; }

        internal string[] Serialize() => new[]
        {
            "schema=" + PendingSchema.ToString(CultureInfo.InvariantCulture),
            "requestedUtcTicks="
                + RequestedUtcTicks.ToString(CultureInfo.InvariantCulture),
            "scenePath=" + ScenePath,
            "sceneCount=" + SceneCount.ToString(CultureInfo.InvariantCulture),
            "sceneDirty=" + (SceneDirty ? "1" : "0")
        };

        internal bool ValidateRuntime(out string failureReason)
        {
            failureReason = string.Empty;
            long now = DateTime.UtcNow.Ticks;
            if (RequestedUtcTicks <= 0L
                || RequestedUtcTicks > now
                || now - RequestedUtcTicks > PendingMaximumAge.Ticks)
            {
                failureReason = "Pending verification request is stale or from the future.";
                return false;
            }
            if (SceneDirty
                || SceneCount != 1
                || !string.Equals(
                    ScenePath,
                    GameplayScenePath,
                    StringComparison.Ordinal))
            {
                failureReason = "Pending verification scene contract is invalid.";
                return false;
            }
            Scene active = SceneManager.GetActiveScene();
            if (!active.IsValid()
                || !active.isLoaded
                || SceneManager.sceneCount != SceneCount
                || !string.Equals(
                    active.path,
                    ScenePath,
                    StringComparison.Ordinal))
            {
                failureReason = "Runtime scene does not match the clean EditMode request.";
                return false;
            }
            return true;
        }

        internal static bool TryParse(
            IReadOnlyList<string> lines,
            out PendingRunRequest request,
            out string failureReason)
        {
            request = default;
            failureReason = string.Empty;
            if (lines == null || lines.Count != 5
                || !TryRead(lines[0], "schema", out string schemaToken)
                || !TryRead(
                    lines[1],
                    "requestedUtcTicks",
                    out string ticksToken)
                || !TryRead(lines[2], "scenePath", out string scenePath)
                || !TryRead(lines[3], "sceneCount", out string countToken)
                || !TryRead(lines[4], "sceneDirty", out string dirtyToken)
                || !int.TryParse(
                    schemaToken,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int schema)
                || schema != PendingSchema
                || !long.TryParse(
                    ticksToken,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out long ticks)
                || !int.TryParse(
                    countToken,
                    NumberStyles.None,
                    CultureInfo.InvariantCulture,
                    out int count)
                || (dirtyToken != "0" && dirtyToken != "1"))
            {
                failureReason = "Pending verification request is malformed.";
                return false;
            }
            request = new PendingRunRequest(
                ticks,
                scenePath,
                count,
                dirtyToken == "1");
            return true;
        }

        private static bool TryRead(
            string line,
            string key,
            out string value)
        {
            string prefix = key + "=";
            if (line == null
                || !line.StartsWith(prefix, StringComparison.Ordinal))
            {
                value = string.Empty;
                return false;
            }
            value = line.Substring(prefix.Length);
            return string.Equals(line, prefix + value, StringComparison.Ordinal);
        }
    }

    private static void WriteImmediateFailure(string detail)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)
            ?? "Artifacts/QA");
        File.WriteAllLines(ReportPath, new[]
        {
            "# Defense engagement production-live PlayMode verification",
            "result=FAIL",
            "scope=production-invasion+defense-engagement+medical-recovery",
            "utc=" + DateTime.UtcNow.ToString("O"),
            "terminal=" + (detail ?? string.Empty)
        });
    }

    public static string GetReport()
    {
        return $"completed={completed}; {lastReport}";
    }

    public static string GetRecoveryDiagnostic()
    {
        Runner runner = UnityEngine.Object.FindFirstObjectByType<Runner>(
            FindObjectsInactive.Include);
        return runner != null
            ? runner.DescribeRecoveryRuntime()
            : "방어 복구 검증 러너가 없습니다.";
    }

    private static IDefenseEngagementRuntime ResolveRuntime()
    {
        DungeonRuntimeLifetimeScope scope =
            UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include);
        return scope?.Container != null
            ? scope.Container.Resolve<IDefenseEngagementRuntime>()
            : null;
    }

    private static T ResolveService<T>() where T : class
    {
        DungeonRuntimeLifetimeScope scope =
            UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include);
        if (scope?.Container == null)
        {
            return null;
        }

        try
        {
            return scope.Container.Resolve<T>();
        }
        catch
        {
            return null;
        }
    }

    public static string TriggerPolicySwitchProbe()
    {
        IDefenseEngagementRuntime runtime = ResolveRuntime();
        DefenseEngagement engagement = runtime?.ActiveEngagements.FirstOrDefault(item =>
            item != null
            && item.State == DefenseEngagementState.Engaged
            && item.LeadGuard != null
            && item.ReserveGuard != null);
        if (runtime == null || engagement == null)
        {
            return "FAIL: 교전 중인 선두/예비 경비가 없습니다.";
        }

        if (!runtime.PolicyRuntime.TryCreatePolicy("교대 검증", out DefenseResponsePolicyData policy))
        {
            return "FAIL: 교대 검증 정책을 만들지 못했습니다.";
        }

        policy.minimumDispatchHealthRatio = 0.01f;
        policy.retreatHealthRatio = 0.99f;
        policy.rejoinHealthRatio = 1f;
        policy.holdWithoutReplacement = true;
        runtime.PolicyRuntime.TryUpdatePolicy(policy);
        if (!runtime.PolicyRuntime.AssignPolicy(engagement.LeadGuard, policy.id))
        {
            return "FAIL: 선두 경비에게 교대 검증 정책을 배정하지 못했습니다.";
        }

        engagement.IntruderActor.ScaleMaxHealth(10f);
        engagement.IntruderActor.Heal(engagement.IntruderActor.MaxHealth);
        policyProbeOldLead = engagement.LeadGuard;
        policyProbeIntruder = engagement.Intruder;
        policyProbeIntruderCell = engagement.IntruderStopCell;
        policyProbeFacilityDamage = engagement.Intruder.FacilityDamageCount;
        Time.timeScale = 1f;
        return $"RUNNING: {policyProbeOldLead.Identity?.DisplayName ?? policyProbeOldLead.name} 교대 조건 적용";
    }

    public static string GetPolicySwitchReport()
    {
        IDefenseEngagementRuntime runtime = ResolveRuntime();
        DefenseEngagement engagement = runtime?.ActiveEngagements.FirstOrDefault(item =>
            item != null && item.Intruder == policyProbeIntruder);
        if (runtime == null || policyProbeOldLead == null || policyProbeIntruder == null)
        {
            Time.timeScale = 0f;
            return $"FAIL: 교대 검증 상태 유실 · runtime={runtime != null}; "
                + $"oldLead={policyProbeOldLead != null}; intruder={policyProbeIntruder != null}";
        }

        if (engagement == null)
        {
            Time.timeScale = 0f;
            return $"FAIL: 교대 검증 중 전선 소실 · active={runtime.ActiveEngagements.Count}; "
                + $"intruderState={policyProbeIntruder.State}; "
                + $"intruderDead={policyProbeIntruder.IntruderActor == null || policyProbeIntruder.IntruderActor.IsDead}; "
                + $"oldLeadDead={policyProbeOldLead.IsDead}; "
                + $"intruderCell={policyProbeIntruder.IntruderActor?.GetNowXY()}";
        }

        if (engagement.State == DefenseEngagementState.Switching
            || engagement.LeadGuard == policyProbeOldLead)
        {
            return $"RUNNING: state={engagement.State}; leadChanged={engagement.LeadGuard != policyProbeOldLead}";
        }

        bool valid = engagement.State == DefenseEngagementState.Engaged
            && engagement.LeadGuard != null
            && engagement.LeadGuard.GetNowXY() == engagement.GuardCell
            && engagement.IntruderActor.GetNowXY() == policyProbeIntruderCell
            && engagement.Intruder.FacilityDamageCount == policyProbeFacilityDamage
            && !policyProbeOldLead.IsAiPaused();
        Time.timeScale = 0f;
        return $"{(valid ? "PASS" : "FAIL")}: state={engagement.State}; "
            + $"leadChanged={engagement.LeadGuard != policyProbeOldLead}; "
            + $"cells={engagement.IntruderActor.GetNowXY()}/{engagement.LeadGuard?.GetNowXY()}; "
            + $"facilityLocked={engagement.Intruder.FacilityDamageCount == policyProbeFacilityDamage}; "
            + $"oldLeadResumed={!policyProbeOldLead.IsAiPaused()}";
    }

    public static string VerifyActiveSaveRoundTrip()
    {
        IDefenseEngagementRuntime runtime = ResolveRuntime();
        DefenseEngagement before = runtime?.ActiveEngagements.FirstOrDefault(item =>
            item != null && item.State == DefenseEngagementState.Engaged);
        if (runtime == null || before == null)
        {
            return "FAIL: 저장 복원할 활성 교전이 없습니다.";
        }

        string engagementId = before.Id;
        string intruderId = before.IntruderActor?.Identity?.PersistentId ?? string.Empty;
        string leadId = before.LeadGuard?.Identity?.PersistentId ?? string.Empty;
        Vector2Int intruderCell = before.IntruderStopCell;
        Vector2Int guardCell = before.GuardCell;
        int exchangeCount = before.ExchangeCount;
        string policyId = runtime.PolicyRuntime.GetAssignedPolicyId(before.LeadGuard);
        OwnerEvacuationSaveSnapshot owner = runtime.OwnerEvacuation.Capture();
        IDungeonGameSaveService saveService =
            ResolveService<IDungeonGameSaveService>();
        if (saveService == null)
        {
            return "FAIL: V18 저장 서비스를 찾지 못했습니다.";
        }
        DungeonGameSaveData saveData = saveService.FromJson(
            saveService.ToJson(saveService.Capture()));
        if (!saveService.TryRestore(saveData, out DungeonGameRestoreReport report))
        {
            return "FAIL: V18 원자 복원 실패 · "
                + string.Join(" | ", report.Errors);
        }

        DefenseEngagement after = runtime.ActiveEngagements.FirstOrDefault(item =>
            item != null && string.Equals(item.Id, engagementId, StringComparison.Ordinal));
        bool valid = after != null
            && string.Equals(after.IntruderActor?.Identity?.PersistentId, intruderId, StringComparison.Ordinal)
            && string.Equals(after.LeadGuard?.Identity?.PersistentId, leadId, StringComparison.Ordinal)
            && after.IntruderStopCell == intruderCell
            && after.GuardCell == guardCell
            && after.ExchangeCount == exchangeCount
            && after.NextGuardAttackAt >= Time.time
            && after.NextIntruderAttackAt >= Time.time
            && runtime.ShouldHoldIntruder(after.Intruder)
            && string.Equals(
                runtime.PolicyRuntime.GetAssignedPolicyId(after.LeadGuard),
                policyId,
                StringComparison.Ordinal)
            && runtime.OwnerEvacuation.IsEvacuating == owner.active
            && runtime.OwnerEvacuation.TargetCell == new Vector2Int(owner.targetX, owner.targetY)
            && report.Warnings.Count == 0;
        Time.timeScale = 0f;
        return $"{(valid ? "PASS" : "FAIL")}: id={after?.Id}; "
            + $"lead={after?.LeadGuard?.Identity?.PersistentId}; "
            + $"cells={after?.IntruderStopCell}/{after?.GuardCell}; "
            + $"exchanges={after?.ExchangeCount}; "
            + $"hold={(after != null && runtime.ShouldHoldIntruder(after.Intruder))}; "
            + $"policy={runtime.PolicyRuntime.GetAssignedPolicyId(after?.LeadGuard)}; "
            + $"owner={runtime.OwnerEvacuation.IsEvacuating}/{runtime.OwnerEvacuation.TargetCell}; "
            + $"warnings=[{string.Join(" | ", report.Warnings)}]";
    }

    public static string ResumeOwnerEvacuationProbe()
    {
        IDefenseEngagementRuntime runtime = ResolveRuntime();
        if (runtime?.OwnerEvacuation == null || !runtime.OwnerEvacuation.IsEvacuating)
        {
            return "FAIL: 진행 중인 사장 대피가 없습니다.";
        }

        if (!ownerEvacuationProbeDurabilityBoosted)
        {
            DefenseEngagement engagement = runtime.ActiveEngagements.FirstOrDefault(item =>
                item != null && item.IntruderActor != null && !item.IntruderActor.IsDead);
            if (engagement?.IntruderActor != null)
            {
                engagement.IntruderActor.ScaleMaxHealth(20f);
                engagement.IntruderActor.Heal(engagement.IntruderActor.MaxHealth);
            }

            ownerEvacuationProbeDurabilityBoosted = true;
        }

        Time.timeScale = 1f;
        return $"RUNNING: owner={runtime.OwnerEvacuation.Owner?.Identity?.DisplayName}; "
            + $"target={runtime.OwnerEvacuation.TargetCell}";
    }

    public static string GetOwnerEvacuationReport()
    {
        IDefenseEngagementRuntime runtime = ResolveRuntime();
        IInvasionOwnerEvacuationService evacuation = runtime?.OwnerEvacuation;
        if (evacuation == null || evacuation.Owner == null)
        {
            Time.timeScale = 0f;
            return "FAIL: 사장 대피 상태를 찾지 못했습니다.";
        }

        bool reached = evacuation.HasReachedTarget;
        Time.timeScale = 0f;
        return $"{(reached ? "PASS" : "RUNNING")}: active={evacuation.IsEvacuating}; "
            + $"reached={reached}; cell={evacuation.Owner.GetNowXY()}; "
            + $"target={evacuation.TargetCell}; status={evacuation.StatusText}; "
            + $"engagements={runtime.ActiveEngagements.Count}";
    }

    public static string StartPolicyUiPointerProbe()
    {
        if (!Application.isPlaying)
        {
            policyUiProbeCompleted = true;
            policyUiProbeReport = "FAIL: PlayMode가 아닙니다.";
            return policyUiProbeReport;
        }

        foreach (PolicyUiRunner existing in UnityEngine.Object.FindObjectsByType<PolicyUiRunner>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None))
        {
            if (existing != null)
            {
                UnityEngine.Object.Destroy(existing.gameObject);
            }
        }

        policyUiProbeCompleted = false;
        policyUiProbeReport = "RUNNING: 방어 정책 UI를 실제 포인터 이벤트로 조작하는 중";
        GameObject root = new GameObject("Defense Policy UI Pointer Verifier");
        root.AddComponent<PolicyUiRunner>();
        return policyUiProbeReport;
    }

    public static string GetPolicyUiPointerReport()
    {
        return $"completed={policyUiProbeCompleted}; {policyUiProbeReport}";
    }

    public static string TriggerOwnerFinalDefenseProbe()
    {
        IDefenseEngagementRuntime runtime = ResolveRuntime();
        IInvasionOwnerEvacuationService evacuation = runtime?.OwnerEvacuation;
        DefenseEngagement active = runtime?.ActiveEngagements.FirstOrDefault(engagement =>
            engagement != null && engagement.Intruder != null && engagement.IntruderActor != null
                && !engagement.IntruderActor.IsDead);
        if (runtime == null
            || evacuation?.Owner == null
            || !evacuation.HasReachedTarget
            || active?.Intruder == null)
        {
            return "FAIL: 사장 대피 완료 상태와 활성 경비 전선이 필요합니다.";
        }

        ownerFinalProbeOwner = evacuation.Owner;
        ownerFinalProbeIntruder = active.Intruder;
        ownerFinalProbeOwner.ScaleMaxHealth(10f);
        ownerFinalProbeOwner.Heal(ownerFinalProbeOwner.MaxHealth);
        ownerFinalProbeIntruder.IntruderActor.Heal(ownerFinalProbeIntruder.IntruderActor.MaxHealth);
        ownerFinalProbeOwnerHealth = ownerFinalProbeOwner.CurrentHealth;
        ownerFinalProbeIntruderHealth = ownerFinalProbeIntruder.IntruderActor.CurrentHealth;

        if (!active.IsOwnerFinalDefense)
        {
            foreach (CharacterActor guard in UnityEngine.Object.FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None).Where(actor => actor != null
                    && !actor.IsDead
                    && !actor.IsOwner
                    && actor.characterType != CharacterType.Customer
                    && actor.characterType != CharacterType.Intruder
                    && CharacterWorkRoleUtility.TryGetWork(actor, out _)))
            {
                guard.ApplyDamage(guard.MaxHealth * 2f, "방어 최종 교전 검증");
            }
        }

        Time.timeScale = 1f;
        return $"RUNNING: owner={ownerFinalProbeOwner.Identity?.DisplayName}; "
            + $"intruder={ownerFinalProbeIntruder.IntruderActor.Identity?.DisplayName}; "
            + $"alreadyFinal={active.IsOwnerFinalDefense}";
    }

    public static string GetOwnerFinalDefenseReport()
    {
        IDefenseEngagementRuntime runtime = ResolveRuntime();
        DefenseEngagement engagement = runtime?.ActiveEngagements.FirstOrDefault(item =>
            item != null
            && item.IsOwnerFinalDefense
            && item.Intruder == ownerFinalProbeIntruder);
        if (runtime == null || ownerFinalProbeOwner == null || ownerFinalProbeIntruder == null)
        {
            Time.timeScale = 0f;
            return "FAIL: 사장 최종 방어 검증 상태가 없습니다.";
        }

        if (engagement == null || engagement.State != DefenseEngagementState.Engaged)
        {
            return $"RUNNING: engagement={engagement?.State.ToString() ?? "none"}; "
                + $"ownerCell={ownerFinalProbeOwner.GetNowXY()}; "
                + $"intruderCell={ownerFinalProbeIntruder.IntruderActor.GetNowXY()}";
        }

        int distance = Mathf.Abs(engagement.GuardCell.x - engagement.IntruderStopCell.x)
            + Mathf.Abs(engagement.GuardCell.y - engagement.IntruderStopCell.y);
        bool bothDamaged = ownerFinalProbeOwner.CurrentHealth < ownerFinalProbeOwnerHealth
            && ownerFinalProbeIntruder.IntruderActor.CurrentHealth < ownerFinalProbeIntruderHealth;
        bool valid = engagement.LeadGuard == ownerFinalProbeOwner
            && engagement.ReserveGuard == null
            && distance == 1
            && engagement.LeadGuard.GetNowXY() == engagement.GuardCell
            && engagement.IntruderActor.GetNowXY() == engagement.IntruderStopCell
            && engagement.ExchangeCount >= 2
            && bothDamaged
            && runtime.ShouldHoldIntruder(ownerFinalProbeIntruder);
        if (!valid && engagement.ExchangeCount < 2)
        {
            return $"RUNNING: state={engagement.State}; exchanges={engagement.ExchangeCount}; "
                + $"cells={engagement.IntruderActor.GetNowXY()}/{engagement.LeadGuard.GetNowXY()}";
        }

        Time.timeScale = 0f;
        return $"{(valid ? "PASS" : "FAIL")}: state={engagement.State}; "
            + $"exchanges={engagement.ExchangeCount}; adjacent={distance == 1}; "
            + $"bothDamaged={bothDamaged}; reserve={engagement.ReserveGuard != null}; "
            + $"held={runtime.ShouldHoldIntruder(ownerFinalProbeIntruder)}; "
            + $"cells={engagement.IntruderActor.GetNowXY()}/{engagement.LeadGuard.GetNowXY()}";
    }

    private sealed class PolicyUiRunner : MonoBehaviour
    {
        private IEnumerator Start()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();

            IDefenseEngagementRuntime engagementRuntime = ResolveRuntime();
            IDefenseResponsePolicyRuntime policyRuntime = engagementRuntime?.PolicyRuntime;
            if (policyRuntime == null)
            {
                Finish(false, "방어 정책 런타임을 찾지 못했습니다.");
                yield break;
            }

            HashSet<string> policyIdsBefore = new HashSet<string>(
                policyRuntime.Policies
                    .Where(policy => policy != null)
                    .Select(policy => policy.id),
                StringComparer.Ordinal);
            int countBefore = policyRuntime.Policies.Count;

            Button defenseTab = FindActiveButton("TopTabButton_Defense_방어");
            bool tabClicked = ClickByPointer(defenseTab);
            yield return null;
            Canvas.ForceUpdateCanvases();

            Button createButton = FindActiveButton("P1Action_DefensePolicyCreate");
            if (createButton == null && tabClicked)
            {
                defenseTab = FindActiveButton("TopTabButton_Defense_방어");
                tabClicked = ClickByPointer(defenseTab);
                yield return null;
                Canvas.ForceUpdateCanvases();
                createButton = FindActiveButton("P1Action_DefensePolicyCreate");
            }

            bool createClicked = ClickByPointer(createButton);
            yield return null;
            Canvas.ForceUpdateCanvases();

            DefenseResponsePolicyData created = policyRuntime.Policies.FirstOrDefault(policy =>
                policy != null && !policyIdsBefore.Contains(policy.id));
            Button assignButton = FindActiveButtons("P1Action_DefensePolicyAssign_")
                .OrderBy(button => button.name, StringComparer.Ordinal)
                .FirstOrDefault();
            bool assignClicked = created != null && ClickByPointer(assignButton);
            yield return null;
            Canvas.ForceUpdateCanvases();

            CharacterActor assignedGuard = created == null
                ? null
                : UnityEngine.Object.FindObjectsByType<CharacterActor>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .FirstOrDefault(actor => actor != null
                        && !actor.IsOwner
                        && string.Equals(
                            policyRuntime.GetAssignedPolicyId(actor),
                            created.id,
                            StringComparison.Ordinal));
            bool countIncreased = policyRuntime.Policies.Count == countBefore + 1;
            bool controlsRemainVisible = FindActiveButton("P1Action_DefensePolicyCreate") != null;
            bool valid = tabClicked
                && createClicked
                && assignClicked
                && created != null
                && countIncreased
                && assignedGuard != null
                && controlsRemainVisible;
            string createdId = created?.id ?? "<none>";
            string assignedName = assignedGuard?.Identity?.DisplayName ?? assignedGuard?.name ?? "<none>";

            if (created != null)
            {
                policyRuntime.TryDeletePolicy(created.id, reassignToStandard: true);
            }

            Finish(
                valid,
                $"tab={tabClicked}; create={createClicked}; assign={assignClicked}; "
                + $"count={countBefore}->{policyRuntime.Policies.Count} (created={countIncreased}); "
                + $"policy={createdId}; guard={assignedName}; controls={controlsRemainVisible}");
        }

        private static Button FindActiveButton(string objectName)
        {
            return UnityEngine.Object.FindObjectsByType<Button>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(button => button != null
                    && button.gameObject.activeInHierarchy
                    && string.Equals(button.name, objectName, StringComparison.Ordinal));
        }

        private static IEnumerable<Button> FindActiveButtons(string prefix)
        {
            return UnityEngine.Object.FindObjectsByType<Button>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where(button => button != null
                    && button.gameObject.activeInHierarchy
                    && button.name.StartsWith(prefix, StringComparison.Ordinal));
        }

        private static bool ClickByPointer(Button button)
        {
            if (button == null || !button.IsInteractable())
            {
                return false;
            }

            RectTransform rect = button.transform as RectTransform;
            Canvas canvas = button.GetComponentInParent<Canvas>();
            Camera eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera ?? Camera.main
                : null;
            Vector2 screenPosition = rect != null
                ? RectTransformUtility.WorldToScreenPoint(eventCamera, rect.TransformPoint(rect.rect.center))
                : Vector2.zero;
            return PlayModeVerificationFrameWait.DispatchPointerClick(button.gameObject, screenPosition);
        }

        private void Finish(bool success, string detail)
        {
            policyUiProbeCompleted = true;
            policyUiProbeReport = $"{(success ? "PASS" : "FAIL")}: {detail}";
            Debug.Log($"DEFENSE_POLICY_UI_POINTER {policyUiProbeReport}");
            Destroy(gameObject);
        }
    }

    private sealed class Wim048Runner : MonoBehaviour
    {
        private bool cleanBootEvidence;
        private string cleanBootScenePath = string.Empty;
        private DungeonGameSaveData baseline;
        private IDungeonGameSaveService saveService;
        private ICharacterCombatCommandRuntime combatCommands;
        private CharacterActor excludedActor;
        private bool excludedActorWasInCombat;
        private InvasionThreatRuntime naturalThreat;
        private bool naturalThreatWasEnabled;
        private bool cleanupComplete;
        private readonly List<string> evidence = new();

        internal void SetCleanBootEvidence(PendingRunRequest request)
        {
            cleanBootEvidence = !request.SceneDirty
                && request.SceneCount == 1
                && string.Equals(
                    request.ScenePath,
                    GameplayScenePath,
                    StringComparison.Ordinal);
            cleanBootScenePath = request.ScenePath;
        }

        private IEnumerator Start()
        {
            DontDestroyOnLoad(gameObject);
            float deadline = Time.realtimeSinceStartup + StartupTimeoutSeconds;
            bool attemptedPartySetup = false;
            IBuildingFeatureQueryService buildingQuery = null;
            IBuildingFeatureCommandService buildingCommands = null;
            IDefenseFeatureCommandService defenseCommands = null;
            IDefenseEngagementRuntime defenseRuntime = null;
            ICharacterSettlementStandingQuery standings = null;
            InvasionDirectorRuntime director = null;
            List<CharacterActor> residents = null;
            BuildingFeatureRoomRow roomRow = null;

            while (Time.realtimeSinceStartup < deadline)
            {
                buildingQuery ??= ResolveService<IBuildingFeatureQueryService>();
                buildingCommands ??= ResolveService<IBuildingFeatureCommandService>();
                defenseCommands ??= ResolveService<IDefenseFeatureCommandService>();
                defenseRuntime ??= ResolveRuntime();
                standings ??= ResolveService<ICharacterSettlementStandingQuery>();
                saveService ??= ResolveService<IDungeonGameSaveService>();
                combatCommands ??= ResolveService<ICharacterCombatCommandRuntime>();
                director ??= FindFirstObjectByType<InvasionDirectorRuntime>(
                    FindObjectsInactive.Include);
                residents = FindObjectsByType<CharacterActor>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .Where(actor => IsNormalResident(actor, standings))
                    .OrderBy(actor => actor.Identity?.PersistentId ?? string.Empty,
                        StringComparer.Ordinal)
                    .ToList();
                roomRow = buildingQuery?.Capture().Rooms
                    .Where(row => row?.Room != null
                        && row.Room.IsUsable
                        && !row.Room.IsSelfContained
                        && row.Room.Cells.Count >= Math.Max(1, residents.Count))
                    .OrderByDescending(row => row.Room.Cells.Count)
                    .ThenBy(row => row.Room.Id)
                    .FirstOrDefault();
                if (buildingCommands != null
                    && defenseCommands != null
                    && defenseRuntime?.OwnerEvacuation != null
                    && standings != null
                    && saveService != null
                    && combatCommands != null
                    && director != null
                    && residents.Count >= 2
                    && roomRow != null)
                {
                    break;
                }

                if (!attemptedPartySetup
                    && Time.realtimeSinceStartup + 1f < deadline
                    && residents.Count < 2)
                {
                    attemptedPartySetup = true;
                    Runner.TryPrepareStartParty(out string setupMessage);
                    evidence.Add("party-setup=" + setupMessage);
                }
                yield return null;
            }

            if (buildingCommands == null
                || defenseCommands == null
                || defenseRuntime?.OwnerEvacuation == null
                || standings == null
                || saveService == null
                || combatCommands == null
                || director == null
                || residents == null
                || residents.Count < 2
                || roomRow == null)
            {
                Finish(false,
                    "실제 방/UI/침입 런타임 준비 실패 · residents="
                    + (residents?.Count ?? 0)
                    + ";room=" + (roomRow?.Room != null));
                yield break;
            }

            naturalThreat = FindFirstObjectByType<InvasionThreatRuntime>(
                FindObjectsInactive.Include);
            if (naturalThreat != null)
            {
                naturalThreatWasEnabled = naturalThreat.enabled;
                naturalThreat.enabled = false;
            }

            try
            {
                baseline = saveService.Capture();
            }
            catch (Exception exception)
            {
                Finish(false,
                    "변경 전 전체 저장 기준선을 캡처하지 못했습니다: "
                    + exception.GetType().Name + " " + exception.Message);
                yield break;
            }

            CharacterActor subject = residents.FirstOrDefault(actor =>
                CharacterWorkRoleUtility.TryGetWork(actor, out _));
            excludedActor = residents.FirstOrDefault(actor => actor != subject);
            if (subject == null
                || excludedActor == null
                || !CharacterWorkRoleUtility.TryGetWork(
                    subject,
                    out AbilityWork subjectWork))
            {
                Finish(false,
                    "Guard-Off 주민과 대표 전투 제외 주민을 준비하지 못했습니다.");
                yield break;
            }
            string subjectId = CharacterPersistentIdentity.Require(subject).Value;
            string excludedId = CharacterPersistentIdentity.Require(excludedActor).Value;
            excludedActorWasInCombat = combatCommands.IsInCombatStance(excludedActor);
            if (!combatCommands.SetCombatStance(
                    excludedActor,
                    true,
                    out string stanceMessage))
            {
                Finish(false,
                    "대표 비대상 전투 태세를 설정하지 못했습니다: " + stanceMessage);
                yield break;
            }

            subjectWork.SetWorkPriority(
                BuiltInWorkTypeIds.Guard,
                WorkPriorityLevel.Off);
            if (subjectWork.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Guard)
                != WorkPriorityLevel.Off)
            {
                Finish(false, "대표 비전투 주민의 Guard-Off 정책이 적용되지 않았습니다.");
                yield break;
            }

            BuildingFeatureCommandResult designation =
                buildingCommands.DesignateResidentEvacuationRoom(
                    roomRow.Grid,
                    roomRow.Room);
            if (!designation.Success)
            {
                Finish(false,
                    "실제 방 UI 지정 명령 실패: " + designation.Message);
                yield break;
            }

            if (director.ActiveIntruders.Count != 0)
            {
                Finish(false, "대표 시나리오는 침입 없는 기준선이 필요합니다.");
                yield break;
            }
            InvasionThreatSnapshot threat = new(
                125f,
                InvasionThreatStage.Candidate,
                new InvasionThreatFactors(6f, 4f, 3f, 1f),
                0f,
                0f);
            if (!director.TrySpawnIntruder(threat, out CharacterActor intruder)
                || intruder == null)
            {
                Finish(false, "실제 침입 생성 명령이 침입자를 만들지 못했습니다.");
                yield break;
            }

            yield return null;
            yield return null;
            if (subjectWork.HasEmergencyResponseWorkGateForDiagnostics)
            {
                Finish(false,
                    "Guard-Off 주민을 침입 경보 responder가 잘못 소유했습니다: epoch="
                    + subjectWork.EmergencyResponseWorkEpochForDiagnostics
                    + ";work="
                    + subjectWork.EmergencyResponseOnlyWorkTypeForDiagnostics);
                yield break;
            }

            CharacterActor alarmResponder = FindGuardAlarmResponder(subject);
            if (alarmResponder == null
                || !CharacterWorkRoleUtility.TryGetWork(
                    alarmResponder,
                    out AbilityWork responderWork))
            {
                Finish(false, "Guard-enabled 침입 경보 responder를 찾지 못했습니다.");
                yield break;
            }
            WorkPriorityLevel responderGuardPriority =
                responderWork.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Guard);
            long responderEpoch =
                responderWork.EmergencyResponseWorkEpochForDiagnostics;
            yield return null;
            yield return null;
            if (!responderWork.HasEmergencyResponseWorkGateForDiagnostics
                || responderWork.EmergencyResponseWorkEpochForDiagnostics
                    != responderEpoch
                || responderWork.EmergencyResponseOnlyWorkTypeForDiagnostics
                    != BuiltInWorkTypeIds.Guard)
            {
                Finish(false,
                    "Guard-enabled responder의 기존 경보 ownership이 유지되지 않았습니다.");
                yield break;
            }

            responderWork.SetWorkPriority(
                BuiltInWorkTypeIds.Guard,
                WorkPriorityLevel.Off);
            float responderReleaseDeadline = Time.realtimeSinceStartup + 5f;
            while (responderWork.HasEmergencyResponseWorkGateForDiagnostics
                   && Time.realtimeSinceStartup < responderReleaseDeadline)
            {
                yield return null;
            }
            if (responderWork.HasEmergencyResponseWorkGateForDiagnostics
                || !alarmResponder.CanRunAi
                || alarmResponder.IsDead
                || alarmResponder.CurrentLifecycleState
                    != CharacterLifecycleState.Active
                || responderWork.WorkPriorities.IsEnabled(BuiltInWorkTypeIds.Guard))
            {
                Finish(false,
                    "Guard 우선순위 변경만으로 responder ownership을 해제하지 못했습니다.");
                yield break;
            }
            responderWork.SetWorkPriority(
                BuiltInWorkTypeIds.Guard,
                responderGuardPriority);
            evidence.Add("alarm-guard-priority-ownership=PASS");

            DefenseFeatureCommandResult request =
                defenseCommands.RequestResidentEvacuation();
            if (!request.Succeeded)
            {
                Finish(false, "실제 방어 UI 대피 명령 실패: " + request.Message);
                yield break;
            }

            IInvasionOwnerEvacuationService evacuation =
                defenseRuntime.OwnerEvacuation;
            if (!evacuation.ResidentEvacuationParticipants.Any(item =>
                    string.Equals(item.CharacterId, subjectId, StringComparison.Ordinal))
                || evacuation.ResidentEvacuationParticipants.Any(item =>
                    string.Equals(item.CharacterId, excludedId, StringComparison.Ordinal)))
            {
                Finish(false,
                    "실제 대상/전투 제외 투영이 일치하지 않습니다: "
                    + evacuation.ResidentEvacuationStatusText);
                yield break;
            }

            yield return WaitForResidentStatus(
                evacuation,
                subjectId,
                ResidentEvacuationParticipantStatus.Holding,
                30f);
            if (!HasResidentStatus(
                    evacuation,
                    subjectId,
                    ResidentEvacuationParticipantStatus.Holding))
            {
                Finish(false,
                    "대상 주민이 실제 actor-aware 경로로 대기 칸에 도착하지 못했습니다: "
                    + evacuation.ResidentEvacuationStatusText);
                yield break;
            }
            evidence.Add("ui-route-holding=PASS;subject=" + subjectId);

            if (!subject.Brain.TryBeginExternallyDrivenAction(
                    "qa:wim048:emergency-preemption",
                    CharacterActionIntentKind.EmergencyPhysicalImminent,
                    "WIM-048 긴급 선점",
                    "Emergency",
                    "주민 대피 stale epoch 보호",
                    out CharacterActionIntentLease emergencyLease))
            {
                Finish(false, "긴급 행동이 주민 대피 RoutineNeed를 선점하지 못했습니다.");
                yield break;
            }
            yield return null;
            if (!subject.Brain.IsExternalIntentCurrent(emergencyLease)
                || !HasResidentStatus(
                    evacuation,
                    subjectId,
                    ResidentEvacuationParticipantStatus.PreemptedEmergency))
            {
                Finish(false,
                    "stale 대피 실행이 긴급 이동 epoch를 보존하지 못했습니다.");
                yield break;
            }
            if (!subject.Brain.EndExternallyDrivenAction(emergencyLease))
            {
                Finish(false, "대표 긴급 행동 epoch를 정상 종료하지 못했습니다.");
                yield break;
            }
            yield return WaitForResidentStatus(
                evacuation,
                subjectId,
                ResidentEvacuationParticipantStatus.Holding,
                30f);
            if (!HasResidentStatus(
                    evacuation,
                    subjectId,
                    ResidentEvacuationParticipantStatus.Holding))
            {
                Finish(false, "긴급 종료 뒤 주민 대피 lease를 재취득하지 못했습니다.");
                yield break;
            }
            evidence.Add("emergency-preempt-reacquire=PASS");

            ResidentEvacuationParticipantView participantBeforeRestore =
                evacuation.ResidentEvacuationParticipants.FirstOrDefault(item =>
                    string.Equals(
                        item.CharacterId,
                        subjectId,
                        StringComparison.Ordinal));
            if (string.IsNullOrEmpty(participantBeforeRestore.CharacterId)
                || !participantBeforeRestore.HasTarget)
            {
                Finish(false,
                    "현재 저장 전 주민 대피 target 권위를 찾지 못했습니다.");
                yield break;
            }
            Vector2Int targetBeforeRestore = participantBeforeRestore.Target;
            DungeonGameSaveData activeSave = saveService.Capture();
            string intruderId = CharacterPersistentIdentity.Require(intruder).Value;
            if (!TryReadActiveIntruderPersistenceJoin(
                    activeSave,
                    intruderId,
                    out string intruderJoinReason))
            {
                Finish(false,
                    "현재 침입자 life/world 저장 join이 일치하지 않습니다: "
                    + intruderJoinReason);
                yield break;
            }
            if (!TryReadResidentSave(
                    activeSave,
                    subjectId,
                    ResidentEvacuationZoneStatus.Active,
                    out string saveReason))
            {
                Finish(false, "현재 대피 저장이 권위 상태와 다릅니다: " + saveReason);
                yield break;
            }
            evidence.Add("active-intruder-life-without-world-actor=PASS");
            string activeSaveJson = saveService.ToJson(activeSave);
            DungeonGameSaveData parsedActiveSave =
                saveService.FromJson(activeSaveJson);
            if (!saveService.TryRestore(
                    parsedActiveSave,
                    out DungeonGameRestoreReport activeRestore)
                || !activeRestore.Success)
            {
                Finish(false,
                    "현재 대피 whole-save 복원 실패: "
                    + string.Join(" | ", activeRestore.Errors));
                yield break;
            }

            defenseRuntime = ResolveRuntime();
            evacuation = defenseRuntime?.OwnerEvacuation;
            CharacterActor restoredSubject = FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(actor => actor != null
                    && string.Equals(
                        actor.Identity?.PersistentId,
                        subjectId,
                        StringComparison.Ordinal));
            if (restoredSubject == null
                || !CharacterWorkRoleUtility.TryGetWork(
                    restoredSubject,
                    out AbilityWork restoredSubjectWork)
                || restoredSubjectWork.WorkPriorities.GetPriority(
                    BuiltInWorkTypeIds.Guard) != WorkPriorityLevel.Off
                || restoredSubjectWork.HasEmergencyResponseWorkGateForDiagnostics)
            {
                Finish(false,
                    "현재 저장 복원 뒤 Guard-Off 주민에게 경보 gate가 재부착되었습니다.");
                yield break;
            }
            ResidentEvacuationParticipantView participantAfterRestore =
                evacuation != null
                    ? evacuation.ResidentEvacuationParticipants.FirstOrDefault(
                        item => string.Equals(
                            item.CharacterId,
                            subjectId,
                            StringComparison.Ordinal))
                    : default;
            if (evacuation?.ResidentZoneStatus
                    != ResidentEvacuationZoneStatus.Active
                || !restoredSubject.gameObject.activeInHierarchy
                || string.IsNullOrEmpty(participantAfterRestore.CharacterId)
                || !participantAfterRestore.HasTarget
                || participantAfterRestore.Target != targetBeforeRestore)
            {
                Finish(false,
                    "전체복원 완료 hook 뒤 actor/방/주민 target join이 유지되지 않았습니다: zone="
                    + evacuation?.ResidentZoneStatus
                    + ";target=" + participantAfterRestore.Target);
                yield break;
            }
            yield return null;
            if (evacuation.ResidentZoneStatus
                    != ResidentEvacuationZoneStatus.Active
                || !evacuation.ResidentEvacuationParticipants.Any(item =>
                    string.Equals(
                        item.CharacterId,
                        subjectId,
                        StringComparison.Ordinal)
                    && item.HasTarget
                    && item.Target == targetBeforeRestore))
            {
                Finish(false,
                    "전체복원 완료 다음 정상 프레임에 대피 topology/participant가 유실되었습니다.");
                yield break;
            }
            yield return WaitForResidentStatus(
                evacuation,
                subjectId,
                ResidentEvacuationParticipantStatus.Holding,
                30f);
            if (!HasResidentStatus(
                    evacuation,
                    subjectId,
                    ResidentEvacuationParticipantStatus.Holding))
            {
                Finish(false, "현재 대피 whole-save 복원 뒤 대기 상태가 복구되지 않았습니다.");
                yield break;
            }
            evidence.Add(
                "current-whole-save-json-completed-hook=PASS;target="
                + targetBeforeRestore);

            buildingQuery = ResolveService<IBuildingFeatureQueryService>();
            BuildingFeatureRoomRow restoredRoom = buildingQuery?.Capture().Rooms
                .FirstOrDefault(row => row != null
                    && row.IsResidentEvacuationRoom);
            BuildableObject[] boundaryDoors = restoredRoom?.Room?.Doors
                .Where(door => door != null && !door.isDestroy)
                .ToArray() ?? Array.Empty<BuildableObject>();
            if (boundaryDoors.Length == 0)
            {
                Finish(false, "복원된 지정 방에서 구조 경계 문을 찾지 못했습니다.");
                yield break;
            }
            foreach (BuildableObject door in boundaryDoors)
            {
                door.DestroySelf();
            }
            float topologyDeadline = Time.realtimeSinceStartup + 5f;
            while (evacuation.ResidentZoneStatus
                       != ResidentEvacuationZoneStatus.LostByTopology
                   && Time.realtimeSinceStartup < topologyDeadline)
            {
                yield return null;
            }
            if (evacuation.ResidentZoneStatus
                != ResidentEvacuationZoneStatus.LostByTopology)
            {
                Finish(false, "실제 방 경계 유실이 LostByTopology로 전이되지 않았습니다.");
                yield break;
            }

            DungeonGameSaveData lostSave = saveService.Capture();
            DungeonGameRestoreReport lostRestore = null;
            if (!TryReadResidentSave(
                    lostSave,
                    subjectId: null,
                    expectedZone: ResidentEvacuationZoneStatus.LostByTopology,
                    reason: out string lostReason)
                || !saveService.TryRestore(
                    lostSave,
                    out lostRestore)
                || !lostRestore.Success)
            {
                Finish(false,
                    "LostByTopology 현재 저장/복원 실패: " + lostReason
                    + " | " + string.Join(" | ", lostRestore?.Errors
                        ?? Array.Empty<string>()));
                yield break;
            }
            evidence.Add("topology-loss-current-save=PASS");

            if (!TryRestoreBaseline(out string cleanupReason))
            {
                Finish(false, "대표 시나리오 정리 복원 실패: " + cleanupReason);
                yield break;
            }
            Finish(true, string.Join(";", evidence));
        }

        private static CharacterActor FindGuardAlarmResponder(
            CharacterActor excluded) =>
            FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where(actor => actor != null
                    && actor != excluded
                    && !actor.IsDead
                    && actor.CurrentLifecycleState == CharacterLifecycleState.Active
                    && actor.CanRunAi
                    && actor.characterType != CharacterType.Customer
                    && actor.characterType != CharacterType.Intruder
                    && CharacterWorkRoleUtility.TryGetWork(
                        actor,
                        out AbilityWork work)
                    && work.WorkPriorities.IsEnabled(BuiltInWorkTypeIds.Guard)
                    && work.HasEmergencyResponseWorkGateForDiagnostics
                    && work.EmergencyResponseOnlyWorkTypeForDiagnostics
                        == BuiltInWorkTypeIds.Guard)
                .OrderBy(actor => actor.Identity?.PersistentId ?? string.Empty,
                    StringComparer.Ordinal)
                .FirstOrDefault();

        private static bool IsNormalResident(
            CharacterActor actor,
            ICharacterSettlementStandingQuery standings) =>
            actor != null
            && !actor.IsDead
            && actor.CurrentLifecycleState == CharacterLifecycleState.Active
            && !actor.IsOwner
            && actor.characterType != CharacterType.Customer
            && actor.characterType != CharacterType.Intruder
            && standings != null
            && (standings.IsFormalResident(actor) || standings.IsMinion(actor));

        private static IEnumerator WaitForResidentStatus(
            IInvasionOwnerEvacuationService evacuation,
            string actorId,
            ResidentEvacuationParticipantStatus expected,
            float timeoutSeconds)
        {
            float deadline = Time.realtimeSinceStartup + timeoutSeconds;
            while (!HasResidentStatus(evacuation, actorId, expected)
                   && Time.realtimeSinceStartup < deadline)
            {
                yield return null;
            }
        }

        private static bool HasResidentStatus(
            IInvasionOwnerEvacuationService evacuation,
            string actorId,
            ResidentEvacuationParticipantStatus expected) =>
            evacuation?.ResidentEvacuationParticipants.Any(item =>
                string.Equals(item.CharacterId, actorId, StringComparison.Ordinal)
                && item.Status == expected) == true;

        private static bool TryReadResidentSave(
            DungeonGameSaveData save,
            string subjectId,
            ResidentEvacuationZoneStatus expectedZone,
            out string reason)
        {
            reason = string.Empty;
            if (!DungeonSaveSectionPayload.TryRead(
                    save,
                    InvasionSaveSection.Id,
                    out DungeonInvasionSaveData invasion)
                || invasion?.ownerEvacuation?.residentZone == null)
            {
                reason = "invasion section missing";
                return false;
            }

            IReadOnlyList<ResidentEvacuationParticipantSaveData> participants =
                invasion.ownerEvacuation.residentParticipants
                ?? new List<ResidentEvacuationParticipantSaveData>();
            bool valid = invasion.version == DungeonInvasionSaveData.CurrentVersion
                && invasion.ownerEvacuation.residentZone.status == expectedZone
                && (subjectId == null
                    ? participants.Count == 0
                    : participants.Any(item => item != null
                        && string.Equals(
                            item.characterId,
                            subjectId,
                            StringComparison.Ordinal)));
            reason = $"version={invasion.version};zone="
                + invasion.ownerEvacuation.residentZone.status
                + ";participants=" + participants.Count;
            return valid;
        }

        private static bool TryReadActiveIntruderPersistenceJoin(
            DungeonGameSaveData save,
            string intruderId,
            out string reason)
        {
            reason = string.Empty;
            if (!DungeonSaveSectionPayload.TryRead(
                    save,
                    InvasionSaveSection.Id,
                    out DungeonInvasionSaveData invasion)
                || !DungeonSaveSectionPayload.TryRead(
                    save,
                    CharacterLifeSaveSection.Id,
                    out CharacterLifeWorldSaveData life)
                || !DungeonSaveSectionPayload.TryRead(
                    save,
                    CharacterWorldSaveSection.Id,
                    out DungeonCharacterWorldSaveData characterWorld))
            {
                reason = "invasion/life/character-world section missing";
                return false;
            }

            int activeInvasionCount = (invasion.activeIntruders
                    ?? new List<DungeonInvasionIntruderSaveData>())
                .Count(item => item?.enemyIndividual != null
                    && string.Equals(
                        item.enemyIndividual.characterId,
                        intruderId,
                        StringComparison.Ordinal));
            int lifeCount = (life.characters
                    ?? new List<CharacterLifeRecordSaveData>())
                .Count(item => item != null
                    && string.Equals(
                        item.characterId,
                        intruderId,
                        StringComparison.Ordinal));
            int characterWorldActorCount = (characterWorld.actors
                    ?? new List<DungeonCharacterSaveData>())
                .Count(item => item != null
                    && string.Equals(
                        item.persistentId,
                        intruderId,
                        StringComparison.Ordinal));
            reason = $"activeInvasion={activeInvasionCount};life={lifeCount};"
                + $"characterWorldActors={characterWorldActorCount};id={intruderId}";
            return activeInvasionCount == 1
                && lifeCount == 1
                && characterWorldActorCount == 0;
        }

        private bool TryRestoreBaseline(out string reason)
        {
            if (cleanupComplete)
            {
                reason = string.Empty;
                return true;
            }
            cleanupComplete = true;
            bool restored = baseline != null
                && saveService != null
                && saveService.TryRestore(
                    baseline,
                    out DungeonGameRestoreReport report)
                && report.Success;
            if (!restored && excludedActor != null && combatCommands != null)
            {
                combatCommands.SetCombatStance(
                    excludedActor,
                    excludedActorWasInCombat,
                    out _);
            }
            if (naturalThreat != null)
            {
                naturalThreat.enabled = naturalThreatWasEnabled;
            }
            reason = restored
                ? string.Empty
                : "whole-save baseline restore rejected";
            return restored;
        }

        private void Finish(bool success, string detail)
        {
            success &= cleanBootEvidence;
            if (!cleanupComplete)
            {
                bool cleanup = TryRestoreBaseline(out string cleanupReason);
                success &= cleanup;
                if (!cleanup)
                {
                    detail += ";cleanup=" + cleanupReason;
                }
            }
            FinishWim048Immediately(success,
                "cleanBoot=" + cleanBootEvidence
                + ";scene=" + cleanBootScenePath
                + ";" + detail);
            Time.timeScale = 0f;
            Debug.Log("WIM048_RESIDENT_EVACUATION " + wim048Report);
        }

        private void OnDestroy()
        {
            if (!cleanupComplete)
            {
                TryRestoreBaseline(out _);
            }
        }
    }

    private sealed class Runner : MonoBehaviour
    {
        private readonly Dictionary<CharacterActor, float> healthBefore =
            new Dictionary<CharacterActor, float>();
        private float startedAt;
        private float intruderSpawnedAt;
        private InvasionDirectorRuntime director;
        private InvasionIntruderRuntime intruder;
        private IDefenseEngagementRuntime engagementRuntime;
        private IInvasionOwnerEvacuationService ownerEvacuation;
        private ICharacterMedicalQuery medicalQuery;
        private IWorldItemStackRuntime worldItems;
        private IResourceEconomyContentCatalog resourceCatalog;
        private CharacterBodyHealthRuntime bodyHealthRuntime;
        private ICharacterDeprivationRuntime deprivationRuntime;
        private ICharacterCombatCommandRuntime combatCommandRuntime;
        private ICombatAmmoResupplyRuntime ammoResupplyRuntime;
        private CharacterActor isolatedOwner;
        private bool isolatedOwnerWasPaused;
        private bool ownerIsolationApplied;
        private InvasionThreatRuntime naturalThreatRuntime;
        private bool naturalThreatWasEnabled;
        private Vector2Int heldIntruderCell;
        private bool observedEngagement;
        private bool combatContractSatisfied;
        private bool intruderStayedStill = true;
        private bool separateAdjacentCells = true;
        private int facilityDamageAtEngagement;
        private int baselineExchangeCount;
        private int lastObservedExchangeCount = -1;
        private string observedEngagementId = string.Empty;
        private readonly List<string> exchangeTrace = new List<string>();
        private float leadHealthAtObservation;
        private float intruderHealthAtObservation;
        private bool attemptedPartySetup;
        private string partySetupMessage = string.Empty;
        private bool observedRallying;
        private bool observedApproachWithoutDispatch;
        private CharacterActor downedGuard;
        private CharacterActor rescueWorker;
        private string medicalOrderId = string.Empty;
        private bool observedDowned;
        private bool observedStabilization;
        private bool observedPhysicalCarry;
        private bool observedTreatment;
        private bool observedRecovery;
        private bool observedPhysicalCarryAttachment;
        private bool observedPreStabilizedPatient;
        private bool observedTreatmentSupplyReady;
        private bool observedTreatmentSupplyConsumed;
        private bool observedTreatmentCompletedStatus;
        private float maxObservedStabilizationWork;
        private float maxObservedTreatmentWork;
        private int seededTreatmentSupply;
        private string controlledPatientId = string.Empty;
        private string controlledRescuerId = string.Empty;
        private readonly List<string> medicalStateTrace = new List<string>();
        private CharacterMedicalOrderState? lastMedicalState;
        private CharacterMedicalStatusCode? lastMedicalStatus;
        private float nextSurvivalRefreshAt;
        private float postCombatNoDownStartedAt = -1f;
        private bool controlledMedicalTriggerAttempted;
        private bool controlledMedicalTriggerUsed;
        private bool controlledSuppressionRequested;
        private bool controlledSuppressionCompleted;
        private string controlledSuppressionFailure = string.Empty;
        private bool cleanBootEvidence;
        private string cleanBootScenePath = string.Empty;

        internal void SetCleanBootEvidence(PendingRunRequest request)
        {
            cleanBootEvidence = !request.SceneDirty
                && request.SceneCount == 1
                && string.Equals(
                    request.ScenePath,
                    GameplayScenePath,
                    StringComparison.Ordinal);
            cleanBootScenePath = request.ScenePath;
        }

        private void Awake()
        {
            startedAt = Time.realtimeSinceStartup;
            DontDestroyOnLoad(gameObject);
        }

        private void Update()
        {
            if (completed)
            {
                return;
            }

            MaintainSurvivalIsolation();
            if (intruder == null)
            {
                if (combatContractSatisfied)
                {
                    ObserveRecovery();
                }
                else
                {
                    TryStartInvasion();
                }

                return;
            }

            ObserveEngagement();
        }

        private void TryStartInvasion()
        {
            if (Time.realtimeSinceStartup - startedAt > StartupTimeoutSeconds)
            {
                Finish(
                    false,
                    $"게임 런타임 또는 당직 경비를 준비하지 못했습니다. "
                    + $"director={director != null}; engagement={engagementRuntime != null}; "
                    + $"party={partySetupMessage}");
                return;
            }

            director ??= FindFirstObjectByType<InvasionDirectorRuntime>(FindObjectsInactive.Include);
            DisableNaturalThreatForVerification();
            engagementRuntime ??= ResolveRuntime();
            ownerEvacuation ??= engagementRuntime?.OwnerEvacuation;
            medicalQuery ??= ResolveService<ICharacterMedicalQuery>();
            worldItems ??= ResolveService<IWorldItemStackRuntime>();
            resourceCatalog ??= ResolveService<IResourceEconomyContentCatalog>();
            bodyHealthRuntime ??= ResolveService<CharacterBodyHealthRuntime>();
            deprivationRuntime ??= ResolveService<ICharacterDeprivationRuntime>();
            combatCommandRuntime ??= ResolveService<ICharacterCombatCommandRuntime>();
            ammoResupplyRuntime ??= ResolveService<ICombatAmmoResupplyRuntime>();
            if (director == null
                || engagementRuntime == null
                || medicalQuery == null
                || worldItems == null
                || resourceCatalog == null
                || bodyHealthRuntime == null
                || deprivationRuntime == null
                || combatCommandRuntime == null)
            {
                return;
            }

            EnsureVerificationMedicine();

            List<CharacterActor> guards = FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where(actor => actor != null
                    && !actor.IsDead
                    && actor.CurrentLifecycleState == CharacterLifecycleState.Active
                    && !actor.IsOwner
                    && actor.characterType != CharacterType.Customer
                    && actor.characterType != CharacterType.Intruder
                    && CharacterWorkRoleUtility.TryGetWork(actor, out _))
                .OrderBy(actor => actor.Identity?.PersistentId ?? string.Empty,
                    StringComparer.Ordinal)
                .Take(3)
                .ToList();
            if (guards.Count == 0)
            {
                if (!attemptedPartySetup && Time.realtimeSinceStartup - startedAt >= 1f)
                {
                    attemptedPartySetup = true;
                    TryPrepareStartParty(out partySetupMessage);
                }

                return;
            }

            foreach (CharacterActor guard in guards)
            {
                CharacterWorkRoleUtility.TryGetWork(guard, out AbilityWork work);
                work.SetDutyState(AbilityWork.DutyState.OnDuty);
                work.WorkPriorities.SetPriority(BuiltInWorkTypeIds.Guard, WorkPriorityLevel.Priority1);
                work.WorkPriorities.SetPriority(BuiltInWorkTypeIds.Rescue, WorkPriorityLevel.Priority1);
                if (combatCommandRuntime.IsInCombatStance(guard))
                {
                    combatCommandRuntime.SetCombatStance(guard, false, out _);
                }
                else
                {
                    guard.SetAiPaused(false);
                }
                guard.Heal(guard.MaxHealth);
                bodyHealthRuntime.Heal(guard, guard.MaxHealth * 10f, stopBleeding: true);
                RefillNeeds(guard);
                deprivationRuntime.DebugClearBreakdown(guard);
                healthBefore[guard] = guard.CurrentHealth;
            }

            isolatedOwner = FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .FirstOrDefault(actor => actor != null
                    && actor.IsOwner
                    && !actor.IsDead
                    && actor.CurrentLifecycleState == CharacterLifecycleState.Active);
            if (isolatedOwner == null)
            {
                Finish(false, "The live owner target was unavailable for invasion isolation.");
                return;
            }

            isolatedOwnerWasPaused = isolatedOwner.IsAiPaused();
            ownerIsolationApplied = true;
            isolatedOwner.SetAiPaused(true);
            isolatedOwner.Brain?.StopCurrentActionForReplan(
                "defense-verifier-owner-route-isolation");
            isolatedOwner.GetAbility<AbilityMove>()?.CancelActiveMovement(
                "defense-verifier-owner-route-isolation");

            InvasionThreatSnapshot snapshot = new InvasionThreatSnapshot(
                125f,
                InvasionThreatStage.Candidate,
                new InvasionThreatFactors(6f, 4f, 3f, 1f),
                0f,
                0f);
            if (!director.TrySpawnIntruder(snapshot, out CharacterActor spawned)
                || spawned == null
                || !spawned.TryGetComponent(out intruder))
            {
                Finish(false, "실제 침입자를 생성하지 못했습니다.");
                return;
            }

            spawned.ScaleMaxHealth(4f);
            healthBefore[spawned] = spawned.CurrentHealth;
            intruderSpawnedAt = Time.realtimeSinceStartup;
            lastReport = $"RUNNING: 침입자 {spawned.Identity?.DisplayName ?? spawned.name} 진입 중";
        }

        internal static bool TryPrepareStartParty(out string message)
        {
            message = string.Empty;
            DungeonRuntimeLifetimeScope scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include);
            if (scope == null || scope.Container == null)
            {
                message = "lifetime scope 없음";
                return false;
            }

            try
            {
                IStartPartyPreparationService preparation =
                    scope.Container.Resolve<IStartPartyPreparationService>();
                IPreparedStartPartyGameplayApplier applier =
                    scope.Container.Resolve<IPreparedStartPartyGameplayApplier>();
                IOwnerCandidateCatalog catalog = scope.Container.Resolve<IOwnerCandidateCatalog>();
                CharacterSO owner = catalog.OwnerCandidates.FirstOrDefault(candidate => candidate != null);
                if (owner == null)
                {
                    message = "사장 후보 없음";
                    return false;
                }

                if (!preparation.Begin(owner, out string beginMessage))
                {
                    message = beginMessage;
                    return false;
                }

                int seed = Environment.TickCount == 0 ? 1 : Environment.TickCount;
                bool prepared = preparation.TryCreatePreparedSnapshot(
                    DungeonDifficulty.Normal,
                    seed,
                    out PreparedStartPartySnapshot snapshot,
                    out string snapshotMessage);
                preparation.Cancel();
                if (!prepared)
                {
                    message = snapshotMessage;
                    return false;
                }

                bool applied = applier.TryApply(snapshot, out string applyMessage);
                message = applyMessage;
                return applied;
            }
            catch (Exception exception)
            {
                message = $"시작 파티 준비 예외: {exception.GetType().Name} {exception.Message}";
                return false;
            }
        }

        private void ObserveEngagement()
        {
            if ((intruder == null || intruder.IntruderActor == null)
                && combatContractSatisfied)
            {
                ObserveRecovery();
                return;
            }
            if (intruder == null || intruder.IntruderActor == null)
            {
                Finish(false, "교전 전에 침입자 런타임이 사라졌습니다.");
                return;
            }

            if (!combatContractSatisfied || controlledMedicalTriggerUsed)
            {
                ObserveMedicalProgress();
            }
            if (intruder.State == InvasionIntruderState.Finished
                || intruder.IntruderActor.IsDead)
            {
                if (combatContractSatisfied)
                {
                    ObserveRecovery();
                }
                else
                {
                    bool retained = engagementRuntime.TryGetEngagement(
                        intruder,
                        out DefenseEngagement finishedEngagement);
                    int totalExchanges = retained
                        ? finishedEngagement.ExchangeCount
                        : lastObservedExchangeCount;
                    string guardState = retained
                        && finishedEngagement.LeadGuard != null
                        ? $"{finishedEngagement.LeadGuard.Identity?.DisplayName ?? finishedEngagement.LeadGuard.name}:"
                            + $"hp={finishedEngagement.LeadGuard.CurrentHealth:0.##}:"
                            + $"state={finishedEngagement.LeadGuard.CurrentLifecycleState}"
                        : "none";
                    Debug.Log(
                        "DEFENSE_ENGAGEMENT_EARLY_FINISH_DIAGNOSTIC "
                        + $"exchanges={totalExchanges - baselineExchangeCount}; "
                        + $"total={totalExchanges}; intruderState={intruder.State}; "
                        + $"intruderHp={intruder.IntruderActor.CurrentHealth:0.##}; "
                        + $"guard={guardState}; trace=[{string.Join(",", exchangeTrace)}]; "
                        + $"runtime={engagementRuntime.BuildDebugSummary()}");
                    Finish(false, "3회 상호 공격을 확인하기 전에 침공이 종료되었습니다.");
                }

                return;
            }

            if (Time.realtimeSinceStartup - intruderSpawnedAt > EngagementTimeoutSeconds)
            {
                string debug = engagementRuntime != null
                    ? engagementRuntime.BuildDebugSummary()
                    : "engagement runtime missing";
                Finish(false, $"제한 시간 안에 3회 공방을 확인하지 못했습니다. {debug}");
                return;
            }

            if (!intruder.HasBreachedDungeonInterior)
            {
                if (engagementRuntime.TryGetEngagement(intruder, out _))
                {
                    Finish(false, "침입자가 던전 밖에 있는데 경비 교전이 생성됐습니다.");
                    return;
                }

                observedRallying |= intruder.State == InvasionIntruderState.Rallying;
                observedApproachWithoutDispatch |= intruder.State == InvasionIntruderState.Entering;
                lastReport = intruder.State == InvasionIntruderState.Rallying
                    ? $"RUNNING: 외부 집결 {intruder.RallySecondsRemaining:0.0}/{intruder.ConfiguredRallyDurationSeconds:0.0}초 / 경비 대기"
                    : $"RUNNING: 입구 접근 중 / 집결 설정 {intruder.ConfiguredRallyDurationSeconds:0.0}초 / 경비 대기";
                return;
            }

            if (!engagementRuntime.TryGetEngagement(intruder, out DefenseEngagement engagement))
            {
                lastReport = $"RUNNING: 경비 출동 대기 · "
                    + $"intruder={intruder.State}@{intruder.IntruderActor.GetNowXY()}; "
                    + $"owner={isolatedOwner?.GetNowXY().ToString() ?? "restored"}; "
                    + engagementRuntime.BuildDebugSummary();
                return;
            }

            if (engagement.State != DefenseEngagementState.Engaged)
            {
                lastReport = $"RUNNING: {engagement.State} · {engagement.StatusText}";
                return;
            }

            if (combatContractSatisfied)
            {
                lastReport =
                    $"RUNNING: 실제 교전 뒤 경비 쓰러짐 대기 · exchanges={engagement.ExchangeCount}; "
                    + BuildRecoverySummary();
                return;
            }

            if (!observedEngagement)
            {
                if (engagement.LeadGuard == null
                    || engagement.LeadGuard.GetNowXY() != engagement.GuardCell
                    || intruder.IntruderActor.GetNowXY() != engagement.IntruderStopCell)
                {
                    lastReport = "RUNNING: 교전 칸 안정화를 기다리는 중";
                    return;
                }

                observedEngagement = true;
                observedEngagementId = engagement.Id;
                heldIntruderCell = intruder.IntruderActor.GetNowXY();
                facilityDamageAtEngagement = intruder.FacilityDamageCount;
                baselineExchangeCount = engagement.ExchangeCount;
                lastObservedExchangeCount = engagement.ExchangeCount;
                leadHealthAtObservation = engagement.LeadGuard.CurrentHealth;
                intruderHealthAtObservation = intruder.IntruderActor.CurrentHealth;
                if (engagement.LeadGuard != null && !healthBefore.ContainsKey(engagement.LeadGuard))
                {
                    healthBefore[engagement.LeadGuard] = engagement.LeadGuard.CurrentHealth;
                }
            }

            else if (!string.Equals(observedEngagementId, engagement.Id, StringComparison.Ordinal))
            {
                Finish(false, "3회 공방을 채우기 전에 전선이 붕괴하고 새 교전이 만들어졌습니다.");
                return;
            }

            Vector2Int intruderCell = intruder.IntruderActor.GetNowXY();
            Vector2Int guardCell = engagement.LeadGuard != null
                ? engagement.LeadGuard.GetNowXY()
                : new Vector2Int(int.MinValue, int.MinValue);
            if (engagement.ExchangeCount != lastObservedExchangeCount)
            {
                intruderStayedStill &= intruderCell == heldIntruderCell;
                separateAdjacentCells &= intruderCell != guardCell
                    && Mathf.Abs(intruderCell.x - guardCell.x) + Mathf.Abs(intruderCell.y - guardCell.y) == 1;
                exchangeTrace.Add($"e{engagement.ExchangeCount}:{intruderCell}/{guardCell}:{engagement.State}");
                lastObservedExchangeCount = engagement.ExchangeCount;
            }

            bool guardDamaged = engagement.LeadGuard != null
                && engagement.LeadGuard.CurrentHealth < leadHealthAtObservation;
            bool intruderDamaged = intruder.IntruderActor.CurrentHealth < intruderHealthAtObservation;
            bool noFacilityDamage = intruder.FacilityDamageCount == facilityDamageAtEngagement;
            bool oneLeadOneReserve = engagement.LeadGuard != null
                && engagement.ReserveGuard != engagement.LeadGuard;
            bool saveCaptured = engagementRuntime.Capture().engagements.Count > 0;
            bool presentationVisible = HasVisibleCombatPresentation(engagement.LeadGuard, expectStatus: true)
                && HasVisibleCombatPresentation(intruder.IntruderActor, expectStatus: false);

            lastReport =
                $"RUNNING: exchanges={engagement.ExchangeCount}; cells={intruderCell}/{guardCell}; "
                + $"hp={engagement.LeadGuard?.CurrentHealth:0.#}/{intruder.IntruderActor.CurrentHealth:0.#}; "
                + $"reserve={engagement.ReserveGuard?.Identity?.DisplayName ?? "없음"}";

            int observedExchanges = engagement.ExchangeCount - baselineExchangeCount;
            // ExchangeCount is advanced only after the production combat core
            // executes an attack. The journey contract requires three actual
            // exchanges; shield/armor outcomes do not have to reduce both
            // actors' aggregate health in the same rendered observation frame.
            if (observedExchanges < 3)
            {
                return;
            }

            bool valid = intruderStayedStill
                && separateAdjacentCells
                && noFacilityDamage
                && oneLeadOneReserve
                && saveCaptured
                && presentationVisible
                && observedRallying
                && observedApproachWithoutDispatch;
            string ownerState = ownerEvacuation != null
                ? $"ownerEvac={ownerEvacuation.IsEvacuating}/{ownerEvacuation.HasReachedTarget}:{ownerEvacuation.StatusText}"
                : "ownerEvac=missing";
            if (!valid)
            {
                Finish(
                    false,
                    $"exchanges={observedExchanges}({engagement.ExchangeCount} total); held={intruderStayedStill}; adjacent={separateAdjacentCells}; "
                    + $"bothDamaged={guardDamaged && intruderDamaged}; facilityLocked={noFacilityDamage}; "
                    + $"leadReserveValid={oneLeadOneReserve}; save={saveCaptured}; presentation={presentationVisible}; "
                    + $"rally={observedRallying}; approachHeld={observedApproachWithoutDispatch}; "
                    + $"cells={intruderCell}/{guardCell}; trace=[{string.Join(",", exchangeTrace)}]; {ownerState}");
                return;
            }

            combatContractSatisfied = true;
            Time.timeScale = 5f;
            // The required production exchanges are complete. Resolve through
            // the defense runtime's normal victory boundary so encounter
            // rewards, passives, guard release, engagement removal and intruder
            // suppression remain one atomic production terminal.
            if (intruder != null
                && intruder.State != InvasionIntruderState.Finished
                && engagement.LeadGuard != null)
            {
                controlledSuppressionRequested = true;
                controlledSuppressionCompleted =
                    engagementRuntime.TryResolveIntruderDefeated(
                        intruder,
                        out controlledSuppressionFailure);
                if (!controlledSuppressionCompleted)
                {
                    Finish(
                        false,
                        "Production defense victory terminal failed after the "
                        + $"required exchanges: {controlledSuppressionFailure}");
                    return;
                }

                RestoreIsolatedOwner();
            }
            lastReport =
                $"RUNNING: 교전 계약 통과, 자연 쓰러짐·구조 대기 · "
                + $"exchanges={observedExchanges}; cells={intruderCell}/{guardCell}; "
                + $"suppression={controlledSuppressionCompleted}; {ownerState}";
        }

        private void ObserveMedicalProgress()
        {
            if (medicalQuery == null)
            {
                return;
            }

            CharacterMedicalOrder order = null;
            if (!string.IsNullOrWhiteSpace(medicalOrderId))
            {
                medicalQuery.TryGetOrder(medicalOrderId, out order);
            }

            if (order == null && !controlledMedicalTriggerUsed)
            {
                foreach (CharacterMedicalOrder candidate in medicalQuery.ActiveOrders)
                {
                    if (candidate == null
                        || !medicalQuery.TryGetPatient(candidate, out CharacterActor patient)
                        || patient == null
                        || patient == intruder?.IntruderActor)
                    {
                        continue;
                    }

                    order = candidate;
                    downedGuard = patient;
                    medicalOrderId = candidate.orderId;
                    break;
                }
            }

            if (order == null
                || !medicalQuery.TryGetPatient(order, out CharacterActor currentPatient)
                || currentPatient == null)
            {
                return;
            }

            string patientId = currentPatient.Identity?.PersistentId ?? string.Empty;
            if (controlledMedicalTriggerUsed
                && (!string.Equals(order.orderId, medicalOrderId, StringComparison.Ordinal)
                    || !string.Equals(patientId, controlledPatientId, StringComparison.Ordinal)))
            {
                Finish(
                    false,
                    $"Medical authority changed during the controlled journey: "
                    + $"order={medicalOrderId}->{order.orderId}; "
                    + $"patient={controlledPatientId}->{patientId}.");
                return;
            }

            if (rescueWorker == null && !string.IsNullOrWhiteSpace(order.rescuerId))
            {
                rescueWorker = FindObjectsByType<CharacterActor>(
                        FindObjectsInactive.Exclude,
                        FindObjectsSortMode.None)
                    .FirstOrDefault(actor => actor != null
                        && string.Equals(
                            actor.Identity?.PersistentId,
                            order.rescuerId,
                            StringComparison.Ordinal));
            }

            if (!string.IsNullOrWhiteSpace(order.rescuerId))
            {
                if (string.IsNullOrWhiteSpace(controlledRescuerId))
                {
                    controlledRescuerId = order.rescuerId;
                }
                else if (!string.Equals(
                             controlledRescuerId,
                             order.rescuerId,
                             StringComparison.Ordinal))
                {
                    medicalStateTrace.Add(
                        $"rescuer-change:{controlledRescuerId}->{order.rescuerId}");
                    controlledRescuerId = order.rescuerId;
                }
            }

            if (lastMedicalState != order.state
                || lastMedicalStatus != order.statusCode)
            {
                medicalStateTrace.Add(
                    $"{order.state}/{order.statusCode}:"
                    + $"stab={order.completedStabilizationWork:0.###}/"
                    + $"{order.requiredStabilizationWork:0.###}:"
                    + $"treat={order.completedTreatmentWork:0.###}/"
                    + $"{order.requiredTreatmentWork:0.###}:"
                    + $"rescuer={order.rescuerId}");
                lastMedicalState = order.state;
                lastMedicalStatus = order.statusCode;
            }

            downedGuard ??= currentPatient;
            observedDowned |= currentPatient.CurrentLifecycleState
                == CharacterLifecycleState.Downed
                || bodyHealthRuntime?.GetSnapshot(currentPatient).Downed == true;
            maxObservedStabilizationWork = Mathf.Max(
                maxObservedStabilizationWork,
                order.completedStabilizationWork);
            maxObservedTreatmentWork = Mathf.Max(
                maxObservedTreatmentWork,
                order.completedTreatmentWork);
            observedPreStabilizedPatient |= order.stabilized
                && order.completedStabilizationWork <= 0.001f;
            observedStabilization |= order.stabilized;
            bool physicallyParented = rescueWorker != null
                && currentPatient.transform.IsChildOf(rescueWorker.transform);
            observedPhysicalCarryAttachment |= order.carried && physicallyParented;
            observedPhysicalCarry |= observedPhysicalCarryAttachment;
            observedTreatmentSupplyReady |= order.treatmentSupply
                    != CharacterMedicalSupplyKind.None
                && !string.IsNullOrWhiteSpace(order.treatmentItemId);
            observedTreatmentSupplyConsumed |= order.treatmentSupplyConsumed
                || (observedTreatmentSupplyReady
                    && order.completedTreatmentWork > 0.001f);
            observedTreatment |= order.completedTreatmentWork > 0f;
            observedTreatmentCompletedStatus |= order.statusCode
                == CharacterMedicalStatusCode.TreatmentCompleted;
            observedRecovery |= order.state == CharacterMedicalOrderState.Completed
                && order.statusCode == CharacterMedicalStatusCode.TreatmentCompleted
                && order.requiredTreatmentWork > 0f
                && order.completedTreatmentWork + 0.001f >= order.requiredTreatmentWork
                && observedTreatmentSupplyConsumed
                && currentPatient.CurrentLifecycleState == CharacterLifecycleState.Active
                && !currentPatient.IsDead
                && bodyHealthRuntime?.GetSnapshot(currentPatient).Downed == false;
        }

        private void ObserveRecovery()
        {
            if (Time.realtimeSinceStartup - intruderSpawnedAt > EngagementTimeoutSeconds)
            {
                Finish(
                    false,
                    "실제 교전 뒤 구조·치료가 제한 시간 안에 완료되지 않았습니다. "
                    + BuildRecoverySummary());
                return;
            }

            if (!controlledMedicalTriggerAttempted)
            {
                if (postCombatNoDownStartedAt < 0f)
                {
                    postCombatNoDownStartedAt = Time.realtimeSinceStartup;
                }

                if (Time.realtimeSinceStartup - postCombatNoDownStartedAt >= 2f)
                {
                    controlledMedicalTriggerAttempted = true;
                    if (!TryCreateControlledPostCombatInjury(out string failureReason))
                    {
                        Finish(
                            false,
                            $"Real combat passed, but the controlled post-combat medical trigger failed: "
                            + $"{failureReason}; {BuildRecoverySummary()}");
                        return;
                    }

                    ObserveMedicalProgress();
                }

                lastReport = "RUNNING: 침공 종료 뒤 실제 경비 쓰러짐을 기다리는 중 · "
                    + BuildRecoverySummary();
                return;
            }

            ObserveMedicalProgress();
            if (!controlledMedicalTriggerUsed
                || !observedDowned
                || downedGuard == null)
            {
                lastReport =
                    "RUNNING: controlled medical order publication pending · "
                    + BuildRecoverySummary();
                return;
            }

            if (!observedRecovery)
            {
                lastReport = "RUNNING: 자동 구조·치료 진행 중 · " + BuildRecoverySummary();
                return;
            }

            bool patientAiResumed = !downedGuard.IsAiPaused()
                && downedGuard.Brain != null
                && downedGuard.CurrentLifecycleState == CharacterLifecycleState.Active
                && !downedGuard.IsDead;
            bool rescuerAiResumed = rescueWorker != null
                && !rescueWorker.IsAiPaused()
                && rescueWorker.Brain != null
                && rescueWorker.CurrentLifecycleState == CharacterLifecycleState.Active
                && !rescueWorker.IsDead
                && rescueWorker.GetComponent<AbilityRescue>()?.IsRescuing != true;
            if (!patientAiResumed || !rescuerAiResumed)
            {
                lastReport =
                    "RUNNING: medical terminal committed; waiting for AI ownership cleanup · "
                    + $"patientAi={patientAiResumed}; rescuerAi={rescuerAiResumed}; "
                    + BuildRecoverySummary();
                return;
            }

            bool valid = combatContractSatisfied
                && controlledSuppressionRequested
                && controlledSuppressionCompleted
                && observedRallying
                && observedApproachWithoutDispatch
                && observedDowned
                && observedStabilization
                && observedPhysicalCarry
                && observedPhysicalCarryAttachment
                && observedTreatment
                && observedTreatmentSupplyConsumed
                && observedTreatmentCompletedStatus
                && observedRecovery
                && patientAiResumed
                && rescuerAiResumed;
            Finish(
                valid,
                $"{BuildRecoverySummary()}; patientAi={patientAiResumed}; "
                + $"rescuerAi={rescuerAiResumed}; "
                + $"patient={downedGuard.Identity?.DisplayName ?? downedGuard.name}; "
                + $"rescuer={rescueWorker?.Identity?.DisplayName ?? rescueWorker?.name ?? "<none>"}");
        }

        private string BuildRecoverySummary()
        {
            CharacterMedicalOrder order = null;
            if (!string.IsNullOrWhiteSpace(medicalOrderId))
            {
                medicalQuery?.TryGetOrder(medicalOrderId, out order);
            }

            return $"downed={observedDowned}; stabilization={observedStabilization}; "
                + $"stabWork={maxObservedStabilizationWork:0.###}/"
                + $"{(order?.requiredStabilizationWork ?? 0f):0.###}; "
                + $"carry={observedPhysicalCarry}; "
                + $"carryAttached={observedPhysicalCarryAttachment}; "
                + $"treatment={observedTreatment}; "
                + $"treatWork={maxObservedTreatmentWork:0.###}/"
                + $"{(order?.requiredTreatmentWork ?? 0f):0.###}; "
                + $"supplyConsumed={observedTreatmentSupplyConsumed}; "
                + $"supplyReady={observedTreatmentSupplyReady}; "
                + $"seededSupply={seededTreatmentSupply}; "
                + $"preStabilized={observedPreStabilizedPatient}; "
                + $"treatmentCompletedStatus={observedTreatmentCompletedStatus}; "
                + $"recovery={observedRecovery}; order={order?.state.ToString() ?? "none"}; "
                + $"medicalTrigger={(controlledMedicalTriggerUsed ? "controlled-post-combat" : "pending")}; "
                + $"patient={controlledPatientId}; rescuer={controlledRescuerId}; "
                + $"status={order?.statusCode.ToString() ?? "none"}; "
                + $"rescueDiag={rescueWorker?.GetComponent<AbilityRescue>()?.LastRescueTerminalForDiagnostics ?? "none"}; "
                + $"trace=[{string.Join(",", medicalStateTrace)}]";
        }

        private bool TryCreateControlledPostCombatInjury(out string failureReason)
        {
            failureReason = string.Empty;
            if (bodyHealthRuntime == null)
            {
                failureReason = "body health runtime is missing";
                return false;
            }

            List<CharacterActor> availableWorkers = FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where(actor => actor != null
                    && !actor.IsDead
                    && !actor.IsOwner
                    && actor.characterType != CharacterType.Customer
                    && actor.characterType != CharacterType.Intruder
                    && actor.CurrentLifecycleState == CharacterLifecycleState.Active
                    && actor.Brain != null)
                .OrderBy(actor => actor.CurrentHealth / Mathf.Max(1f, actor.MaxHealth))
                .ToList();
            if (availableWorkers.Count < 2)
            {
                failureReason = $"at least two active workers are required; found={availableWorkers.Count}";
                return false;
            }

            foreach (CharacterActor worker in availableWorkers)
            {
                if (!ResetPersistentNeedsAndMood(worker, out string resetFailure))
                {
                    failureReason = resetFailure;
                    return false;
                }
            }

            CharacterActor patient = availableWorkers[0];
            patient.Heal(patient.MaxHealth);
            bodyHealthRuntime.Heal(patient, patient.MaxHealth * 10f, stopBleeding: true);
            CharacterBodyHealthSnapshot source = bodyHealthRuntime.GetSnapshot(patient);
            if (source.Parts == null || source.Parts.Count == 0)
            {
                failureReason = $"body snapshot is empty for {patient.name}";
                return false;
            }

            List<CharacterBodyPartHealthState> injuredParts = source.Parts
                .Select(part => new CharacterBodyPartHealthState
                {
                    bodyPart = part.bodyPart,
                    maxHealth = part.maxHealth,
                    currentHealth = part.currentHealth,
                    bleedingPerSecond = part.bleedingPerSecond
                })
                .ToList();
            foreach (CharacterBodyPartHealthState leg in injuredParts.Where(part =>
                         part.bodyPart == CombatBodyPart.LeftLeg
                         || part.bodyPart == CombatBodyPart.RightLeg))
            {
                leg.currentHealth = Mathf.Min(leg.currentHealth, leg.maxHealth * 0.18f);
            }

            CharacterBodyPartHealthState arm = injuredParts.FirstOrDefault(part =>
                part.bodyPart == CombatBodyPart.LeftArm);
            if (arm != null)
            {
                arm.currentHealth = Mathf.Min(arm.currentHealth, arm.maxHealth * 0.55f);
                arm.bleedingPerSecond = Mathf.Max(arm.bleedingPerSecond, 0.01f);
            }

            CharacterBodyHealthSnapshot injury = new CharacterBodyHealthSnapshot(
                injuredParts,
                5f,
                source.Suppression,
                source.Consciousness,
                source.Manipulation,
                0.18f,
                downed: true);
            bodyHealthRuntime.ApplySnapshot(
                patient,
                injury,
                "Defense integration verification: post-combat leg injury");

            CharacterBodyHealthSnapshot applied = bodyHealthRuntime.GetSnapshot(patient);
            if (!applied.Downed
                || patient.CurrentLifecycleState != CharacterLifecycleState.Downed)
            {
                failureReason =
                    $"injury did not produce a downed state; mobility={applied.Mobility:0.###}";
                return false;
            }

            controlledMedicalTriggerUsed = true;
            downedGuard = patient;
            observedDowned = true;
            string patientId = !string.IsNullOrWhiteSpace(patient.Identity?.PersistentId)
                ? patient.Identity.PersistentId
                : $"scene-actor:{patient.GetInstanceID()}";
            controlledPatientId = patientId;
            CharacterMedicalOrder createdOrder = medicalQuery?.ActiveOrders.FirstOrDefault(order =>
                order != null
                && order.IsActive
                && string.Equals(order.patientId, patientId, StringComparison.Ordinal));
            if (createdOrder == null)
            {
                failureReason =
                    $"medical order was not published for controlled patient {patientId}";
                return false;
            }
            medicalOrderId = createdOrder.orderId;
            seededTreatmentSupply = SeedControlledTreatmentSupply(createdOrder);
            if (seededTreatmentSupply <= 0)
            {
                failureReason =
                    $"no physical treatment supply could be seeded for {createdOrder.orderId}";
                return false;
            }

            rescueWorker = availableWorkers
                .Where(actor => actor != patient
                    && actor.Brain != null
                    && !actor.Brain.IsExternallyDrivenActionActive
                    && actor.Brain.availableActions?.Any(action =>
                        action?.actionset is AIRescue) == true)
                .OrderBy(actor => actor.Identity?.PersistentId ?? string.Empty,
                    StringComparer.Ordinal)
                .FirstOrDefault();
            if (rescueWorker == null)
            {
                failureReason =
                    $"no autonomous AIRescue worker was available for {createdOrder.orderId}";
                return false;
            }

            if (rescueWorker.TryGetAbility(out AbilityWork rescueWork))
            {
                rescueWork.SetDutyState(AbilityWork.DutyState.OnDuty);
                rescueWork.WorkPriorities.SetPriority(
                    BuiltInWorkTypeIds.Rescue,
                    WorkPriorityLevel.Priority1);
            }

            rescueWorker.Brain.StopCurrentActionForReplan(
                "defense-medical-controlled-order");
            rescueWorker.Brain.PreferActionOnNextDecision<AIRescue>(300f);
            rescueWorker.Brain.RequestImmediateReplan(clearFailures: true);

            lastReport =
                $"RUNNING: controlled post-combat injury applied to "
                + $"{patient.Identity?.DisplayName ?? patient.name}; "
                + $"mobility={applied.Mobility:0.###}";
            return true;
        }

        private int SeedControlledTreatmentSupply(CharacterMedicalOrder order)
        {
            if (order == null || worldItems == null || resourceCatalog == null)
            {
                return 0;
            }

            string destination = WorldItemStackRuntime.FacilityInputDestinationPrefix
                + $"medical:{order.orderId}";
            int total = 0;
            foreach (ResourceItemDefinitionSO medicine in resourceCatalog.Items
                         .Where(item => item != null
                             && item.Kind == ResourceItemKind.Medicine
                             && item.SupportsInjuryTreatment)
                         .OrderBy(item => item.ItemId, StringComparer.Ordinal))
            {
                if (worldItems.SpawnItemAt(
                        medicine.ItemId,
                        2,
                        order.BedPosition,
                        WorldItemStackState.FacilityBuffer,
                        destination,
                        out int spawned))
                {
                    total += spawned;
                }
            }

            return total;
        }

        private bool ResetPersistentNeedsAndMood(
            CharacterActor actor,
            out string failureReason)
        {
            failureReason = string.Empty;
            if (actor?.Stats == null || deprivationRuntime == null)
            {
                failureReason =
                    $"medical isolation authority is missing for {actor?.name ?? "<null>"}";
                return false;
            }

            actor.Brain?.StopCurrentActionForReplan(
                "defense-medical-neutral-state");
            Dictionary<CharacterCondition, float> restoredStats =
                actor.Stats.StatSnapshot.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value);
            restoredStats[CharacterCondition.HUNGER] = 100f;
            restoredStats[CharacterCondition.THIRST] = 100f;
            restoredStats[CharacterCondition.SLEEP] = 100f;
            restoredStats[CharacterCondition.FUN] = 100f;
            restoredStats[CharacterCondition.MOOD] = 100f;
            restoredStats[CharacterCondition.EXCRETION] = 100f;
            restoredStats[CharacterCondition.HYGIENE] = 100f;
            actor.Stats.RestorePersistentState(
                restoredStats,
                actor.CurrentHealth,
                actor.InjurySeverity,
                100f,
                Array.Empty<CharacterMoodFactorSnapshot>());
            if (!deprivationRuntime.DebugResetForDeterministicScenario(actor))
            {
                failureReason =
                    $"deprivation reset rejected {actor.Identity?.PersistentId ?? actor.name}";
                return false;
            }

            if (CharacterMoodImpulseUtility.GetMood01(actor) < 0.9f)
            {
                failureReason =
                    $"neutral mood precondition failed for "
                    + $"{actor.Identity?.PersistentId ?? actor.name}";
                return false;
            }

            return true;
        }

        public string DescribeRecoveryRuntime()
        {
            int activeMedicalOrders = medicalQuery?.ActiveOrders?.Count ?? 0;
            List<string> actorStates = FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where(actor => actor != null
                    && actor.characterType != CharacterType.Customer
                    && actor.characterType != CharacterType.Intruder)
                .Select(actor =>
                {
                    AbilityWork work = actor.GetAbility<AbilityWork>();
                    AbilityRescue rescue = AbilityRescue.Ensure(actor);
                    bool canRescue = rescue != null
                        && rescue.CanStartRescue(out DomainFailure _);
                    bool combatStance = combatCommandRuntime?.IsInCombatStance(actor) == true;
                    bool resupplying = ammoResupplyRuntime?.IsResupplying(actor) == true;
                    return $"{actor.Identity?.DisplayName ?? actor.name}:"
                        + $"owner={actor.IsOwner},state={actor.CurrentLifecycleState},dead={actor.IsDead},"
                        + $"paused={actor.IsAiPaused()},offDuty={work?.IsOffDuty},"
                        + $"stance={combatStance},resupply={resupplying},"
                        + $"rescuePriority={work?.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Rescue)},"
                        + $"canRescue={canRescue},"
                        + $"action={actor.Brain?.CurrentActionDebugLabel}/"
                        + $"{actor.Brain?.CurrentActionPhase},"
                        + $"brain={actor.Brain?.GetDebugSummary(2)}";
                })
                .ToList();
            return $"{BuildRecoverySummary()}; medicalOrders={activeMedicalOrders} || "
                + $"{string.Join(" || ", actorStates)}";
        }

        private void MaintainSurvivalIsolation()
        {
            if (Time.realtimeSinceStartup < nextSurvivalRefreshAt)
            {
                return;
            }

            nextSurvivalRefreshAt = Time.realtimeSinceStartup + 1f;
            deprivationRuntime ??= ResolveService<ICharacterDeprivationRuntime>();
            foreach (CharacterActor actor in FindObjectsByType<CharacterActor>(
                         FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                if (actor == null
                    || actor.IsDead
                    || actor.characterType == CharacterType.Customer
                    || actor.characterType == CharacterType.Intruder)
                {
                    continue;
                }

                RefillNeeds(actor);
                MaintainNeutralMood(actor);
                deprivationRuntime?.DebugClearBreakdown(actor);
                AbilityWork work = actor.GetAbility<AbilityWork>();
                work?.WorkPriorities.SetPriority(
                    BuiltInWorkTypeIds.Rescue,
                    WorkPriorityLevel.Priority1);
            }
        }

        private static void RefillNeeds(CharacterActor actor)
        {
            if (actor?.Stats?.Stats == null)
            {
                return;
            }

            CharacterCondition[] needs =
            {
                CharacterCondition.HUNGER,
                CharacterCondition.THIRST,
                CharacterCondition.SLEEP,
                CharacterCondition.FUN,
                CharacterCondition.EXCRETION,
                CharacterCondition.HYGIENE
            };
            foreach (CharacterCondition condition in needs)
            {
                float current = actor.Stats.Stats.TryGetValue(condition, out float value)
                    ? value
                    : 0f;
                actor.ChangesStat(condition, 100f - current);
            }
        }

        private static void MaintainNeutralMood(CharacterActor actor)
        {
            if (actor?.Stats == null)
            {
                return;
            }

            Dictionary<CharacterCondition, float> restoredStats =
                actor.Stats.StatSnapshot.ToDictionary(
                    pair => pair.Key,
                    pair => pair.Value);
            restoredStats[CharacterCondition.MOOD] = 100f;
            actor.Stats.RestorePersistentState(
                restoredStats,
                actor.CurrentHealth,
                actor.InjurySeverity,
                100f,
                Array.Empty<CharacterMoodFactorSnapshot>());
        }

        private static bool HasVisibleCombatPresentation(CharacterActor actor, bool expectStatus)
        {
            if (actor == null)
            {
                return false;
            }

            Transform marker = actor.transform.Find("DefenseEngagementMarker");
            Transform health = actor.transform.Find("WorldNameplate/HealthBackground");
            return marker != null
                && marker.gameObject.activeInHierarchy == expectStatus
                && health != null
                && health.gameObject.activeInHierarchy;
        }

        private void Finish(bool success, string detail)
        {
            RestoreIsolatedOwner();
            RestoreNaturalThreat();
            completed = true;
            lastReport = $"{(success ? "PASS" : "FAIL")}: {detail}";
            WriteDurableReport(success, detail);
            Time.timeScale = 0f;
            Debug.Log($"DEFENSE_ENGAGEMENT_PLAYMODE {lastReport}");
        }

        private void WriteDurableReport(bool success, string detail)
        {
            bool patientAiResumed = downedGuard != null
                && !downedGuard.IsAiPaused()
                && downedGuard.Brain != null;
            bool rescuerAiResumed = rescueWorker != null
                && !rescueWorker.IsAiPaused()
                && rescueWorker.Brain != null;
            string Row(bool passed, string id, string value) =>
                (passed ? "PASS" : "FAIL") + "\t" + id + "\t" + value;

            Directory.CreateDirectory(Path.GetDirectoryName(ReportPath)
                ?? "Artifacts/QA");
            File.WriteAllLines(ReportPath, new[]
            {
                "# Defense engagement production-live PlayMode verification",
                "result=" + (success ? "PASS" : "FAIL"),
                "scope=production-invasion+defense-engagement+medical-recovery",
                "utc=" + DateTime.UtcNow.ToString("O"),
                "authority=InvasionDirectorRuntime.TrySpawnIntruder->IDefenseEngagementRuntime->production combat exchanges->autonomous medical terminal",
                Row(cleanBootEvidence, "GAMEPLAY_SCENE_CLEAN_BOOT", "scene=" + cleanBootScenePath),
                Row(intruderSpawnedAt > 0f, "COMMAND_INTRUDER_SPAWN", "spawned=" + (intruderSpawnedAt > 0f)),
                Row(observedRallying && observedApproachWithoutDispatch, "INVASION_ENTRY_PHASES", "rally=" + observedRallying + "; approach=" + observedApproachWithoutDispatch),
                Row(observedEngagement, "ENGAGEMENT_STARTED", "engagementId=" + observedEngagementId),
                Row(combatContractSatisfied, "COMBAT_3_EXCHANGES_OBSERVED", "exchanges=" + Math.Max(0, lastObservedExchangeCount - baselineExchangeCount)),
                Row(controlledSuppressionRequested && controlledSuppressionCompleted, "PRODUCTION_DEFENSE_VICTORY_TERMINAL", "requested=" + controlledSuppressionRequested + "; completed=" + controlledSuppressionCompleted + "; failure=" + controlledSuppressionFailure),
                Row(observedDowned, "MEDICAL_DOWNED", "observed=" + observedDowned),
                Row(observedStabilization, "MEDICAL_STABILIZATION", "observed=" + observedStabilization + "; preStabilized=" + observedPreStabilizedPatient + "; work=" + maxObservedStabilizationWork),
                Row(observedPhysicalCarry && observedPhysicalCarryAttachment, "MEDICAL_PHYSICAL_CARRY", "observed=" + observedPhysicalCarry + "; attached=" + observedPhysicalCarryAttachment),
                Row(observedTreatment && observedTreatmentSupplyReady && observedTreatmentSupplyConsumed, "MEDICAL_TREATMENT", "observed=" + observedTreatment + "; work=" + maxObservedTreatmentWork + "; supplyReady=" + observedTreatmentSupplyReady + "; supplyConsumed=" + observedTreatmentSupplyConsumed + "; seeded=" + seededTreatmentSupply),
                Row(observedRecovery && observedTreatmentCompletedStatus, "MEDICAL_RECOVERY_TERMINAL", "observed=" + observedRecovery + "; treatmentCompleted=" + observedTreatmentCompletedStatus),
                Row(patientAiResumed && rescuerAiResumed, "AI_OWNERSHIP_RESUMED", "patient=" + patientAiResumed + "; rescuer=" + rescuerAiResumed),
                "terminal=" + (success ? "PASS: " : "FAIL: ") + (detail ?? string.Empty)
            });
        }

        private void OnDestroy()
        {
            RestoreIsolatedOwner();
            RestoreNaturalThreat();
        }

        private void RestoreIsolatedOwner()
        {
            if (!ownerIsolationApplied)
            {
                return;
            }

            ownerIsolationApplied = false;
            if (isolatedOwner == null
                || isolatedOwner.IsDead
                || isolatedOwner.CurrentLifecycleState
                    != CharacterLifecycleState.Active)
            {
                isolatedOwner = null;
                return;
            }

            isolatedOwner.SetAiPaused(isolatedOwnerWasPaused);
            if (!isolatedOwnerWasPaused)
            {
                isolatedOwner.Brain?.RequestImmediateReplan(clearFailures: false);
            }
            isolatedOwner = null;
        }

        private void DisableNaturalThreatForVerification()
        {
            if (naturalThreatRuntime != null)
            {
                return;
            }

            naturalThreatRuntime = FindFirstObjectByType<InvasionThreatRuntime>(
                FindObjectsInactive.Include);
            if (naturalThreatRuntime == null)
            {
                return;
            }

            naturalThreatWasEnabled = naturalThreatRuntime.enabled;
            naturalThreatRuntime.enabled = false;
        }

        private void RestoreNaturalThreat()
        {
            if (naturalThreatRuntime == null)
            {
                return;
            }

            naturalThreatRuntime.enabled = naturalThreatWasEnabled;
            naturalThreatRuntime = null;
        }

        private static void EnsureVerificationMedicine()
        {
            IWarehouseWorldQuery warehouseQuery = ResolveService<IWarehouseWorldQuery>();
            foreach (IWarehouseFacility warehouse in warehouseQuery?.Warehouses
                         ?? Array.Empty<IWarehouseFacility>())
            {
                warehouse?.Inventory?.SeedPhysicalStockForTest(StockCategory.Medicine, 12);
            }
        }
    }
}
