using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VContainer;

public static class DefenseEngagementPlayModeVerifier
{
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

    [MenuItem("DungeonStory/Debug/Invasion/Start Defense Engagement PlayMode Verification")]
    public static void StartFromMenu()
    {
        if (!Application.isPlaying)
        {
            EditorApplication.EnterPlaymode();
            EditorApplication.delayCall += () =>
            {
                StartRuntimeProbe();
            };
            return;
        }

        StartRuntimeProbe();
    }

    public static string StartRuntimeProbe()
    {
        if (!Application.isPlaying)
        {
            lastReport = "FAIL: PlayMode가 아닙니다.";
            completed = true;
            return lastReport;
        }

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
        ownerEvacuationProbeDurabilityBoosted = false;
        Time.timeScale = 1f;
        GameObject root = new GameObject("Defense Engagement PlayMode Verifier");
        root.AddComponent<Runner>();
        return lastReport;
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
        DefenseResponsePolicySaveSnapshot policies = runtime.PolicyRuntime.Capture();
        OwnerEvacuationSaveSnapshot owner = runtime.OwnerEvacuation.Capture();
        DefenseEngagementSaveSnapshot engagements = runtime.Capture();
        List<string> warnings = new List<string>();

        runtime.PolicyRuntime.Restore(policies, warnings);
        runtime.OwnerEvacuation.Restore(owner, warnings);
        runtime.Restore(engagements, warnings);

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
            && warnings.Count == 0;
        Time.timeScale = 0f;
        return $"{(valid ? "PASS" : "FAIL")}: id={after?.Id}; "
            + $"lead={after?.LeadGuard?.Identity?.PersistentId}; "
            + $"cells={after?.IntruderStopCell}/{after?.GuardCell}; "
            + $"exchanges={after?.ExchangeCount}; "
            + $"hold={(after != null && runtime.ShouldHoldIntruder(after.Intruder))}; "
            + $"policy={runtime.PolicyRuntime.GetAssignedPolicyId(after?.LeadGuard)}; "
            + $"owner={runtime.OwnerEvacuation.IsEvacuating}/{runtime.OwnerEvacuation.TargetCell}; "
            + $"warnings=[{string.Join(" | ", warnings)}]";
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
        private ICharacterMedicalRuntime medicalRuntime;
        private ICharacterBodyHealthRuntime bodyHealthRuntime;
        private ICharacterDeprivationRuntime deprivationRuntime;
        private ICharacterCombatCommandRuntime combatCommandRuntime;
        private ICombatAmmoResupplyRuntime ammoResupplyRuntime;
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
        private float nextSurvivalRefreshAt;
        private float postCombatNoDownStartedAt = -1f;
        private bool controlledMedicalTriggerAttempted;
        private bool controlledMedicalTriggerUsed;

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
            medicalRuntime ??= ResolveService<ICharacterMedicalRuntime>();
            bodyHealthRuntime ??= ResolveService<ICharacterBodyHealthRuntime>();
            deprivationRuntime ??= ResolveService<ICharacterDeprivationRuntime>();
            combatCommandRuntime ??= ResolveService<ICharacterCombatCommandRuntime>();
            ammoResupplyRuntime ??= ResolveService<ICombatAmmoResupplyRuntime>();
            if (director == null
                || engagementRuntime == null
                || medicalRuntime == null
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
                    && !actor.IsOwner
                    && actor.characterType != CharacterType.Customer
                    && actor.characterType != CharacterType.Intruder
                    && CharacterWorkRoleUtility.TryGetWork(actor, out _))
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

        private static bool TryPrepareStartParty(out string message)
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

            ObserveMedicalProgress();
            if (intruder.State == InvasionIntruderState.Finished
                || intruder.IntruderActor.IsDead)
            {
                if (combatContractSatisfied)
                {
                    ObserveRecovery();
                }
                else
                {
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
                lastReport = $"RUNNING: 경비 출동 대기 · {engagementRuntime.BuildDebugSummary()}";
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
            if (observedExchanges < 3 || !guardDamaged || !intruderDamaged)
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
            lastReport =
                $"RUNNING: 교전 계약 통과, 자연 쓰러짐·구조 대기 · "
                + $"exchanges={observedExchanges}; cells={intruderCell}/{guardCell}; {ownerState}";
        }

        private void ObserveMedicalProgress()
        {
            if (medicalRuntime == null)
            {
                return;
            }

            CharacterMedicalOrder order = null;
            if (!string.IsNullOrWhiteSpace(medicalOrderId))
            {
                medicalRuntime.TryGetOrder(medicalOrderId, out order);
            }

            if (order == null)
            {
                foreach (CharacterMedicalOrder candidate in medicalRuntime.ActiveOrders)
                {
                    if (candidate == null
                        || !medicalRuntime.TryGetPatient(candidate, out CharacterActor patient)
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
                || !medicalRuntime.TryGetPatient(order, out CharacterActor currentPatient)
                || currentPatient == null)
            {
                return;
            }

            downedGuard ??= currentPatient;
            observedDowned |= currentPatient.CurrentLifecycleState
                == CharacterLifecycleState.Downed
                || bodyHealthRuntime?.GetSnapshot(currentPatient).Downed == true;
            observedStabilization |= order.stabilized
                || order.completedStabilizationWork > 0f
                || order.state is CharacterMedicalOrderState.AwaitingRescue
                    or CharacterMedicalOrderState.Carrying
                    or CharacterMedicalOrderState.AwaitingBed
                    or CharacterMedicalOrderState.Treating
                    or CharacterMedicalOrderState.Recovering
                    or CharacterMedicalOrderState.Completed;
            observedPhysicalCarry |= order.carried
                || order.state == CharacterMedicalOrderState.Carrying;
            observedTreatment |= order.completedTreatmentWork > 0f
                || order.state is CharacterMedicalOrderState.Treating
                    or CharacterMedicalOrderState.Recovering
                    or CharacterMedicalOrderState.Completed;
            observedRecovery |= order.state == CharacterMedicalOrderState.Completed
                && currentPatient.CurrentLifecycleState == CharacterLifecycleState.Active
                && bodyHealthRuntime?.GetSnapshot(currentPatient).Downed == false;

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
        }

        private void ObserveRecovery()
        {
            ObserveMedicalProgress();
            if (Time.realtimeSinceStartup - intruderSpawnedAt > EngagementTimeoutSeconds)
            {
                Finish(
                    false,
                    "실제 교전 뒤 구조·치료가 제한 시간 안에 완료되지 않았습니다. "
                    + BuildRecoverySummary());
                return;
            }

            if (!observedDowned || downedGuard == null)
            {
                if (postCombatNoDownStartedAt < 0f)
                {
                    postCombatNoDownStartedAt = Time.realtimeSinceStartup;
                }

                if (!controlledMedicalTriggerAttempted
                    && Time.realtimeSinceStartup - postCombatNoDownStartedAt >= 2f)
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

            if (!observedRecovery)
            {
                lastReport = "RUNNING: 자동 구조·치료 진행 중 · " + BuildRecoverySummary();
                return;
            }

            bool patientAiResumed = !downedGuard.IsAiPaused()
                && downedGuard.Brain != null;
            bool rescuerAiResumed = rescueWorker != null
                && !rescueWorker.IsAiPaused()
                && rescueWorker.Brain != null;
            bool valid = combatContractSatisfied
                && observedRallying
                && observedApproachWithoutDispatch
                && observedDowned
                && observedStabilization
                && observedPhysicalCarry
                && observedTreatment
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
                medicalRuntime?.TryGetOrder(medicalOrderId, out order);
            }

            return $"downed={observedDowned}; stabilization={observedStabilization}; "
                + $"carry={observedPhysicalCarry}; treatment={observedTreatment}; "
                + $"recovery={observedRecovery}; order={order?.state.ToString() ?? "none"}; "
                + $"medicalTrigger={(controlledMedicalTriggerUsed ? "controlled-post-combat" : "natural-combat")}; "
                + $"status={order?.status ?? "none"}";
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
            CharacterMedicalOrder createdOrder = medicalRuntime?.ActiveOrders.FirstOrDefault(order =>
                order != null
                && order.IsActive
                && string.Equals(order.patientId, patientId, StringComparison.Ordinal));
            if (createdOrder != null)
            {
                medicalOrderId = createdOrder.orderId;
            }

            lastReport =
                $"RUNNING: controlled post-combat injury applied to "
                + $"{patient.Identity?.DisplayName ?? patient.name}; "
                + $"mobility={applied.Mobility:0.###}";
            return true;
        }

        public string DescribeRecoveryRuntime()
        {
            int activeMedicalOrders = medicalRuntime?.ActiveOrders?.Count ?? 0;
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
                        && rescue.CanStartRescue(out string _);
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
            RestoreNaturalThreat();
            completed = true;
            lastReport = $"{(success ? "PASS" : "FAIL")}: {detail}";
            Time.timeScale = 0f;
            Debug.Log($"DEFENSE_ENGAGEMENT_PLAYMODE {lastReport}");
        }

        private void OnDestroy()
        {
            RestoreNaturalThreat();
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
                warehouse?.Inventory?.AddStock(StockCategory.Medicine, 12);
            }
        }
    }
}
