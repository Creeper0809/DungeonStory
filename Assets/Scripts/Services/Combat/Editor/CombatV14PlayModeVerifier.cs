using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using VContainer;

public static class CombatV14PlayModeVerifier
{
    public const string ReportPath = "Artifacts/QA/combat-v14-playmode-report.txt";
    public const string CommandCapturePath = "Artifacts/QA/combat-v14-command-bar.png";
    public const string RescueCapturePath = "Artifacts/QA/combat-v14-rescue-carry.png";
    public const string TreatmentCapturePath = "Artifacts/QA/combat-v14-treatment.png";

    private static string report = "V14 PlayMode 검증을 실행하지 않았습니다.";
    private static bool completed;

    [MenuItem("DungeonStory/Debug/Combat/Start V14 PlayMode Verification")]
    public static void StartFromMenu()
    {
        if (!Application.isPlaying)
        {
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(
                "Assets/Scenes/GameplayScene.unity");
            EditorApplication.EnterPlaymode();
            EditorApplication.delayCall += () => StartRuntimeProbe();
            return;
        }

        StartRuntimeProbe();
    }

    public static string StartRuntimeProbe()
    {
        if (!Application.isPlaying)
        {
            completed = true;
            report = "FAIL: PlayMode가 아닙니다.";
            return report;
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

        Directory.CreateDirectory("Artifacts/QA");
        completed = false;
        report = "RUNNING: V14 런타임 준비 중";
        GameObject host = new GameObject("Combat V14 PlayMode Verifier");
        host.AddComponent<Runner>();
        return report;
    }

    public static string GetReport()
    {
        return $"completed={completed}; {report}";
    }

    public static string GetDiagnostic()
    {
        Runner runner = UnityEngine.Object.FindFirstObjectByType<Runner>(
            FindObjectsInactive.Include);
        return runner != null
            ? runner.DescribeRuntimeState()
            : "V14 verifier runner가 없습니다.";
    }

    private sealed class Runner : MonoBehaviour
    {
        private readonly List<string> checks = new List<string>();
        private readonly List<string> failures = new List<string>();
        private readonly List<string> capturedErrors = new List<string>();
        private readonly List<string> capturedWarnings = new List<string>();
        private readonly Dictionary<string, string> testWeaponIds =
            new Dictionary<string, string>(StringComparer.Ordinal);

        private float originalTimeScale;
        private int originalGameViewSizeIndex = -1;
        private bool preparedRun;
        private bool attemptedPartySetup;
        private OwnerCommandController commands;
        private CharacterActor rescuer;
        private CharacterActor patient;
        private CharacterActor secondSelected;
        private CharacterMedicalOrder medicalOrder;
        private ICharacterMedicalRuntime medicalRuntime;
        private ICharacterBodyHealthRuntime bodyHealthRuntime;
        private ICharacterCombatCommandRuntime combatCommandRuntime;
        private ICombatEquipmentRuntime combatEquipmentRuntime;
        private ISurvivalFoodRuntime survivalFoodRuntime;
        private Camera gameplayCamera;
        private bool sawStabilizing;
        private bool sawCarrying;
        private bool sawPhysicalCarry;
        private bool sawTreating;
        private bool capturedCarry;
        private bool capturedTreatment;
        private InputSettings.EditorInputBehaviorInPlayMode originalInputBehavior;
        private Mouse originalMouse;
        private Keyboard originalKeyboard;
        private Mouse verificationMouse;
        private Keyboard verificationKeyboard;

        public string DescribeRuntimeState()
        {
            CharacterCombatCommand command = null;
            bool hasCommand = rescuer != null
                && combatCommandRuntime != null
                && combatCommandRuntime.TryGetCommand(
                    rescuer,
                    out command);
            AbilityRescue rescue = rescuer != null
                ? rescuer.GetComponent<AbilityRescue>()
                : null;
            CharacterMedicalOrder current = medicalOrder != null
                && medicalRuntime != null
                && medicalRuntime.TryGetOrder(
                    medicalOrder.orderId,
                    out CharacterMedicalOrder found)
                    ? found
                    : null;
            string commandText = hasCommand
                ? $"{command.type}/{command.state}/{command.status}"
                : "none";
            string abilityText = rescue != null
                ? $"exists/rescuing={rescue.IsRescuing}"
                : "none";
            string orderText = current != null
                ? $"{current.state}/{current.status}/rescuer={current.rescuerId}/"
                    + $"stab={current.completedStabilizationWork:0.##}/"
                    + $"{current.requiredStabilizationWork:0.##}"
                : "none";

            return $"rescuer={GetName(rescuer)} cell={rescuer?.GetNowXY()} "
                + $"active={rescuer?.CurrentLifecycleState} aiPaused={rescuer?.IsAiPaused()} "
                + $"stance={combatCommandRuntime?.IsInCombatStance(rescuer)}; "
                + $"command={commandText}; "
                + $"ability={abilityText}; "
                + $"patient={GetName(patient)} cell={patient?.GetNowXY()} "
                + $"state={patient?.CurrentLifecycleState}; "
                + $"order={orderText}";
        }

        private IEnumerator Start()
        {
            Application.logMessageReceived += OnLogMessageReceived;
            originalTimeScale = Time.timeScale;
            originalGameViewSizeIndex = GameViewResolutionController.SelectedSizeIndex;
            GameViewResolutionController.Select(1600, 900);
            SetupInput();
            DungeonAutomationInputState.Enable(
                new DungeonStory.Foundation.UnityGameClock(),
                new DungeonStory.Foundation.UnityUiClock());
            Time.timeScale = 1f;

            yield return WaitForRuntime();
            if (failures.Count > 0)
            {
                Finish();
                yield break;
            }

            yield return VerifyTacticalPointerControls();
            if (failures.Count > 0)
            {
                Finish();
                yield break;
            }

            yield return VerifyDownedRescueTreatment();
            Finish();
        }

        private IEnumerator WaitForRuntime()
        {
            float timeout = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < timeout)
            {
                medicalRuntime ??= ResolveService<ICharacterMedicalRuntime>();
                bodyHealthRuntime ??= ResolveService<ICharacterBodyHealthRuntime>();
                combatCommandRuntime ??= ResolveService<ICharacterCombatCommandRuntime>();
                combatEquipmentRuntime ??= ResolveService<ICombatEquipmentRuntime>();
                survivalFoodRuntime ??= ResolveService<ISurvivalFoodRuntime>();
                commands = UnityEngine.Object.FindFirstObjectByType<OwnerCommandController>(
                    FindObjectsInactive.Include);
                gameplayCamera = UnityEngine.Object.FindFirstObjectByType<Camera>(
                    FindObjectsInactive.Include);
                CharacterActor[] staff = GetActiveStaff();
                bool servicesReady = medicalRuntime != null
                    && bodyHealthRuntime != null
                    && combatCommandRuntime != null
                    && combatEquipmentRuntime != null;
                if (commands != null && gameplayCamera != null && servicesReady && staff.Length >= 2)
                {
                    AssignActors(staff);
                    Check(true, "RUNTIME", $"actors={staff.Length}; scene={SceneManager.GetActiveScene().name}");
                    yield break;
                }

                if (!attemptedPartySetup && staff.Length < 2 && Time.realtimeSinceStartup + 13f < timeout)
                {
                    attemptedPartySetup = true;
                    preparedRun = TryPrepareStartParty(out string setup);
                    checks.Add($"PARTY_SETUP={(preparedRun ? "PASS" : "WAIT")}; {setup}");
                }

                yield return null;
            }

            Check(false, "RUNTIME", $"commands={commands != null}; camera={gameplayCamera != null}; "
                + $"medical={medicalRuntime != null}; "
                + $"body={bodyHealthRuntime != null}; "
                + $"combat={combatCommandRuntime != null}; "
                + $"actors={GetActiveStaff().Length}");
        }

        private void AssignActors(IReadOnlyList<CharacterActor> staff)
        {
            CharacterActor[] available = staff
                .Where(actor => actor != null)
                .ToArray();
            int bestDistance = int.MaxValue;
            for (int first = 0; first < available.Length; first++)
            {
                for (int second = first + 1; second < available.Length; second++)
                {
                    Vector2Int firstCell = available[first].GetNowXY();
                    Vector2Int secondCell = available[second].GetNowXY();
                    int distance = Mathf.Abs(firstCell.x - secondCell.x)
                        + Mathf.Abs(firstCell.y - secondCell.y);
                    if (distance >= bestDistance)
                    {
                        continue;
                    }

                    bestDistance = distance;
                    rescuer = available[first];
                    patient = available[second];
                }
            }

            secondSelected = patient;
        }

        private IEnumerator VerifyTacticalPointerControls()
        {
            ArrangeActorsForPointerTest();
            yield return null;
            yield return null;
            EnsureRangedTestEquipment();
            if (failures.Count > 0)
            {
                yield break;
            }

            yield return ClickActor(rescuer, additive: false);
            yield return ClickActor(secondSelected, additive: true);
            HashSet<string> selectedIds = commands.SelectedActors
                .Select(GetId)
                .ToHashSet(StringComparer.Ordinal);
            Check(
                commands.SelectedActors.Count >= 2
                    && selectedIds.Contains(GetId(rescuer))
                    && selectedIds.Contains(GetId(secondSelected)),
                "POINTER_MULTI_SELECT",
                $"selected={string.Join(",", commands.SelectedActors.Select(GetName))}");

            Button stanceButton = FindRuntimeButton("CombatStanceButton");
            Check(stanceButton != null, "COMMAND_BAR", "전투 명령 바와 태세 버튼 표시");
            if (stanceButton == null)
            {
                yield break;
            }

            yield return ClickButton(stanceButton);
            bool bothInStance = combatCommandRuntime.IsInCombatStance(rescuer)
                && combatCommandRuntime.IsInCombatStance(secondSelected);
            Check(bothInStance, "POINTER_COMBAT_STANCE", "다중 선택 전투 태세 활성화");

            Button reloadButton = FindRuntimeButton("CombatReloadButton");
            Check(reloadButton != null, "RELOAD_BUTTON", "재장전 버튼 표시");
            if (reloadButton != null)
            {
                yield return ClickButton(reloadButton);
                float reloadTimeout = Time.realtimeSinceStartup + 4f;
                while (Time.realtimeSinceStartup < reloadTimeout
                    && !BothTestWeaponsLoaded())
                {
                    yield return null;
                }
            }

            Check(
                BothTestWeaponsLoaded(),
                "POINTER_RELOAD",
                DescribeTestWeaponAmmo());

            CharacterCombatLoadoutProfile rescuerBefore =
                combatEquipmentRuntime.GetActiveProfileSnapshot(GetId(rescuer));
            CharacterCombatLoadoutProfile secondBefore =
                combatEquipmentRuntime.GetActiveProfileSnapshot(GetId(secondSelected));
            CombatFireMode rescuerModeBefore = rescuerBefore?.fireMode ?? CombatFireMode.Aimed;
            CombatFireMode secondModeBefore = secondBefore?.fireMode ?? CombatFireMode.Aimed;
            bool rescuerHoldBefore = rescuerBefore?.holdFire ?? false;
            bool secondHoldBefore = secondBefore?.holdFire ?? false;

            Button fireModeButton = FindRuntimeButton("CombatFireModeButton");
            Button holdFireButton = FindRuntimeButton("CombatHoldFireButton");
            Check(fireModeButton != null && holdFireButton != null, "TACTICAL_BUTTONS",
                "사격 모드와 사격 중지 버튼 표시");
            if (fireModeButton != null)
            {
                yield return ClickButton(fireModeButton);
            }

            if (holdFireButton != null)
            {
                yield return ClickButton(holdFireButton);
            }

            CharacterCombatLoadoutProfile rescuerProfile =
                combatEquipmentRuntime.GetActiveProfileSnapshot(GetId(rescuer));
            CharacterCombatLoadoutProfile secondProfile =
                combatEquipmentRuntime.GetActiveProfileSnapshot(GetId(secondSelected));
            Check(
                rescuerProfile != null
                    && secondProfile != null
                    && rescuerProfile.fireMode != rescuerModeBefore
                    && secondProfile.fireMode != secondModeBefore
                    && rescuerProfile.holdFire != rescuerHoldBefore
                    && secondProfile.holdFire != secondHoldBefore,
                "TACTICAL_PROFILE",
                $"rescuer={rescuerModeBefore}->{rescuerProfile?.fireMode}/"
                + $"{rescuerHoldBefore}->{rescuerProfile?.holdFire}; "
                + $"second={secondModeBefore}->{secondProfile?.fireMode}/"
                + $"{secondHoldBefore}->{secondProfile?.holdFire}");

            yield return Capture(CommandCapturePath);
            yield return ClickButton(stanceButton);
            Check(
                !combatCommandRuntime.IsInCombatStance(rescuer)
                    && !combatCommandRuntime.IsInCombatStance(secondSelected),
                "STANCE_RELEASE",
                "생활 AI 복귀 전 태세와 명령 해제");
        }

        private void ArrangeActorsForPointerTest()
        {
            GridSystemManager gridManager = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>(
                FindObjectsInactive.Include);
            Grid grid = gridManager != null ? gridManager.grid : null;
            if (grid == null)
            {
                Check(false, "POINTER_ACTOR_LAYOUT", "Grid 없음");
                return;
            }

            HashSet<Vector2Int> occupiedByOthers = UnityEngine.Object
                .FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Select(CharacterActorCollection.GetCanonical)
                .Where(actor => actor != null
                    && actor != rescuer
                    && actor != secondSelected
                    && !actor.IsDead)
                .Select(actor => actor.GetNowXY())
                .ToHashSet();
            List<Vector2Int> candidates = grid.GetCells()
                .Where(cell => cell != null
                    && cell.AreaType == GridCellAreaType.DungeonInterior
                    && grid.IsWalkable(cell.Position)
                    && !occupiedByOthers.Contains(cell.Position))
                .Select(cell => cell.Position)
                .OrderBy(position => position.y)
                .ThenBy(position => position.x)
                .ToList();

            Vector2Int first = default;
            Vector2Int second = default;
            bool found = false;
            foreach (Vector2Int candidate in candidates)
            {
                int partnerIndex = candidates.FindIndex(other =>
                    other.y == candidate.y
                    && Mathf.Abs(other.x - candidate.x) >= 4
                    && Mathf.Abs(other.x - candidate.x) <= 8
                    && HasClearHorizontalWalk(grid, candidate, other));
                if (partnerIndex < 0)
                {
                    continue;
                }

                first = candidate;
                second = candidates[partnerIndex];
                found = true;
                break;
            }

            if (!found)
            {
                Check(false, "POINTER_ACTOR_LAYOUT", $"candidates={candidates.Count}");
                return;
            }

            PositionActor(rescuer, grid, first);
            PositionActor(secondSelected, grid, second);
            Check(
                rescuer.GetNowXY() == first && secondSelected.GetNowXY() == second,
                "POINTER_ACTOR_LAYOUT",
                $"{GetName(rescuer)}={first}; {GetName(secondSelected)}={second}");
        }

        private static void PositionActor(CharacterActor actor, Grid grid, Vector2Int position)
        {
            actor?.GetComponent<AbilityMove>()?.CancelActiveMovement();
            actor?.Brain?.StopCurrentActionForReplan("V14 포인터 검증 위치 정리");
            if (actor == null)
            {
                return;
            }

            Vector3 world = grid.GetWorldPos(position);
            world.z = actor.transform.position.z;
            actor.transform.position = world;
            actor.Brain?.ClearPathSearchCache();
        }

        private static bool HasClearHorizontalWalk(
            Grid grid,
            Vector2Int first,
            Vector2Int second)
        {
            int minX = Mathf.Min(first.x, second.x);
            int maxX = Mathf.Max(first.x, second.x);
            for (int x = minX; x <= maxX; x++)
            {
                if (!grid.IsWalkable(new Vector2Int(x, first.y)))
                {
                    return false;
                }
            }

            return true;
        }

        private void EnsureRangedTestEquipment()
        {
            DungeonRuntimeLifetimeScope scope =
                UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                    FindObjectsInactive.Include);
            if (scope == null || scope.Container == null)
            {
                Check(false, "RANGED_LOADOUT", "DungeonRuntimeLifetimeScope 없음");
                return;
            }

            IDungeonItemCatalogProvider itemCatalog =
                scope.Container.Resolve<IDungeonItemCatalogProvider>();
            IItemHaulingSettingsProvider haulingSettings =
                scope.Container.Resolve<IItemHaulingSettingsProvider>();
            foreach (CharacterActor actor in new[] { rescuer, secondSelected }.Distinct())
            {
                string actorId = GetId(actor);
                CombatEquipmentInstance bow = combatEquipmentRuntime.CreateInstance(
                    "weapon:shortbow",
                    CombatEquipmentQuality.Normal);
                bool assigned = combatEquipmentRuntime.TryAssignToCharacter(
                    actorId,
                    bow.instanceId,
                    out string assignFailure);
                string activeFailure = string.Empty;
                bool activated = assigned
                    && combatEquipmentRuntime.TrySetActiveWeapon(
                        actorId,
                        bow.instanceId,
                        out activeFailure);
                if (!assigned)
                {
                    activeFailure = assignFailure;
                }

                CharacterCarryInventory inventory = CharacterCarryInventory.Ensure(actor);
                string ammoFailure = string.Empty;
                bool ammoAdded = inventory != null
                    && inventory.TryAdd(
                        $"v14-ammo:{actorId}",
                        "ammo:arrow",
                        6,
                        itemCatalog,
                        haulingSettings,
                        out ammoFailure);
                if (inventory == null)
                {
                    ammoFailure = "소지품 컴포넌트 없음";
                }

                if (assigned && activated && ammoAdded)
                {
                    testWeaponIds[actorId] = bow.instanceId;
                }

                Check(
                    assigned && activated && ammoAdded,
                    "RANGED_LOADOUT",
                    $"{GetName(actor)}: assigned={assigned}; active={activated}; "
                    + $"ammo={ammoAdded}; reason={activeFailure ?? ammoFailure}");
            }
        }

        private bool BothTestWeaponsLoaded()
        {
            return testWeaponIds.Count == 2
                && testWeaponIds.Values.All(instanceId =>
                    combatEquipmentRuntime.TryGetInstance(
                        instanceId,
                        out CombatEquipmentInstance instance)
                    && instance.loadedAmmo > 0);
        }

        private string DescribeTestWeaponAmmo()
        {
            return string.Join(
                ", ",
                testWeaponIds.Select(pair =>
                {
                    combatEquipmentRuntime.TryGetInstance(
                        pair.Value,
                        out CombatEquipmentInstance instance);
                    return $"{pair.Key}={instance?.loadedAmmo ?? 0}";
                }));
        }

        private IEnumerator VerifyDownedRescueTreatment()
        {
            ArrangeActorsForPointerTest();
            rescuer.SetAiPaused(true);
            patient.SetAiPaused(true);
            yield return null;
            yield return null;

            AddMedicalSupplies();
            CharacterWorkRoleUtility.TryGetWork(rescuer, out AbilityWork rescuerWork);
            rescuerWork?.SetDutyState(AbilityWork.DutyState.OnDuty);
            rescuerWork?.WorkPriorities.SetPriority(
                BuiltInWorkTypeIds.Rescue,
                WorkPriorityLevel.Priority1);
            CharacterWorkRoleUtility.TryGetWork(patient, out AbilityWork patientWork);
            patientWork?.SetDutyState(AbilityWork.DutyState.OnDuty);
            rescuer.GetComponent<AbilityMove>()?.CancelActiveMovement();
            patient.GetComponent<AbilityMove>()?.CancelActiveMovement();
            FillSafeConditions(rescuer);
            FillSafeConditions(patient);

            CharacterBodyHealthSnapshot before =
                bodyHealthRuntime.GetSnapshot(patient);
            List<CharacterBodyPartHealthState> injuredParts = before.Parts
                .Select(ClonePart)
                .ToList();
            foreach (CharacterBodyPartHealthState part in injuredParts)
            {
                if (part.bodyPart is CombatBodyPart.LeftLeg or CombatBodyPart.RightLeg)
                {
                    part.currentHealth = Mathf.Max(0.5f, part.maxHealth * 0.18f);
                }
                else if (part.bodyPart == CombatBodyPart.LeftArm)
                {
                    part.currentHealth = Mathf.Max(1f, part.maxHealth * 0.55f);
                    part.bleedingPerSecond = 0.01f;
                }
            }

            bodyHealthRuntime.ApplySnapshot(
                patient,
                new CharacterBodyHealthSnapshot(
                    injuredParts,
                    5f,
                    0f,
                    1f,
                    1f,
                    0.08f,
                    true),
                "V14 구조 검증 부상");
            yield return null;
            yield return null;

            medicalOrder = medicalRuntime.ActiveOrders.FirstOrDefault(order =>
                order != null
                && order.IsActive
                && string.Equals(order.patientId, GetId(patient), StringComparison.Ordinal));
            Check(
                patient.CurrentLifecycleState == CharacterLifecycleState.Downed
                    && medicalOrder != null
                    && !patient.CanRunAi,
                "DOWNED",
                $"state={patient.CurrentLifecycleState}; order={medicalOrder?.orderId}; "
                + $"canRunAi={patient.CanRunAi}");
            if (medicalOrder == null)
            {
                yield break;
            }

            yield return ClickActor(rescuer, additive: false);
            Button stanceButton = FindRuntimeButton("CombatStanceButton");
            yield return ClickButton(stanceButton);
            Button rescueButton = FindRuntimeButton("CombatMode_Rescue");
            Check(rescueButton != null, "RESCUE_BUTTON", "구조 명령 버튼 표시");
            if (rescueButton == null)
            {
                yield break;
            }

            yield return ClickButton(rescueButton);
            yield return RightClickActor(patient);
            yield return null;
            Check(
                rescuer.GetComponent<AbilityRescue>()?.IsRescuing == true
                    || combatCommandRuntime.TryGetCommand(rescuer, out _),
                "POINTER_RESCUE_COMMAND",
                "구조 버튼과 쓰러진 환자 정확 클릭으로 명령 시작");

            Time.timeScale = 4f;
            float timeout = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < timeout)
            {
                FillSafeConditions(rescuer);
                CharacterMedicalOrder current = medicalRuntime.ActiveOrders
                    .FirstOrDefault(order => order != null
                        && string.Equals(order.orderId, medicalOrder.orderId, StringComparison.Ordinal));
                if (current != null)
                {
                    sawStabilizing |= current.state == CharacterMedicalOrderState.Stabilizing
                        || current.stabilized;
                    sawCarrying |= current.state == CharacterMedicalOrderState.Carrying
                        || current.carried;
                    sawPhysicalCarry |= current.carried && patient.transform.IsChildOf(rescuer.transform);
                    sawTreating |= current.state == CharacterMedicalOrderState.Treating
                        || current.state == CharacterMedicalOrderState.Recovering
                        || current.state == CharacterMedicalOrderState.Completed;

                    if (current.carried && !capturedCarry)
                    {
                        capturedCarry = true;
                        FocusCamera(rescuer.transform.position);
                        yield return Capture(RescueCapturePath);
                    }

                    if (current.state == CharacterMedicalOrderState.Treating && !capturedTreatment)
                    {
                        capturedTreatment = true;
                        FocusCamera(patient.transform.position);
                        yield return Capture(TreatmentCapturePath);
                    }

                    report = $"RUNNING: {current.state} · {current.status} · "
                        + $"안정화 {Percent(current.completedStabilizationWork, current.requiredStabilizationWork)}% · "
                        + $"치료 {Percent(current.completedTreatmentWork, current.requiredTreatmentWork)}%";
                }

                if (patient.CurrentLifecycleState == CharacterLifecycleState.Active)
                {
                    break;
                }

                yield return null;
            }

            CharacterBodyHealthSnapshot after =
                bodyHealthRuntime.GetSnapshot(patient);
            Check(sawStabilizing, "FIELD_STABILIZATION", "현장 안정화 진행 또는 완료");
            Check(sawCarrying && sawPhysicalCarry, "PHYSICAL_RESCUE",
                $"carrying={sawCarrying}; parented={sawPhysicalCarry}");
            Check(sawTreating, "BED_TREATMENT", $"treating={sawTreating}");
            Check(
                patient.CurrentLifecycleState == CharacterLifecycleState.Active
                    && !after.Downed
                    && after.Consciousness >= 0.35f
                    && after.Mobility >= 0.3f
                    && after.BloodLoss < 70f,
                "RECOVERY_HYSTERESIS",
                $"state={patient.CurrentLifecycleState}; downed={after.Downed}; "
                + $"consciousness={after.Consciousness:0.##}; mobility={after.Mobility:0.##}; "
                + $"blood={after.BloodLoss:0.#}");
        }

        private IEnumerator RightClickActor(CharacterActor actor)
        {
            if (actor == null || gameplayCamera == null)
            {
                yield break;
            }

            Collider2D collider = actor.GetComponentsInChildren<Collider2D>(true)
                .FirstOrDefault(candidate => candidate != null && candidate.enabled);
            if (collider == null)
            {
                Check(false, "ACTOR_COLLIDER", GetName(actor));
                yield break;
            }

            FocusCamera(collider.bounds.center);
            yield return null;
            yield return null;

            Vector3 screen = gameplayCamera.WorldToScreenPoint(collider.bounds.center);
            DungeonAutomationInputState.MovePointer(screen);
            ApplyMouseState(new MouseState { position = screen });
            yield return null;
            DungeonAutomationInputState.ClickPointer(1);
            ApplyMouseState(
                new MouseState { position = screen }.WithButton(MouseButton.Right, true));
            yield return null;
            yield return null;
            ApplyMouseState(new MouseState { position = screen });
            yield return null;
            yield return null;
        }

        private IEnumerator ClickActor(
            CharacterActor actor,
            bool additive,
            bool preserveCamera = false)
        {
            if (actor == null || gameplayCamera == null)
            {
                yield break;
            }

            Collider2D collider = actor.GetComponentsInChildren<Collider2D>(true)
                .FirstOrDefault(candidate => candidate != null && candidate.enabled);
            if (collider == null)
            {
                Check(false, "ACTOR_COLLIDER", GetName(actor));
                yield break;
            }

            if (!preserveCamera)
            {
                FocusCamera(collider.bounds.center);
                yield return null;
                yield return null;
            }

            Vector3 screen = gameplayCamera.WorldToScreenPoint(collider.bounds.center);
            DungeonAutomationInputState.MovePointer(screen);
            ApplyMouseState(new MouseState { position = screen });
            yield return null;
            if (additive)
            {
                DungeonAutomationInputState.HoldKey(KeyCode.LeftShift, 0.35f);
                QueueKeyboard(new KeyboardState(Key.LeftShift));
            }

            ApplyMouseState(
                new MouseState { position = screen }.WithButton(MouseButton.Left, true));
            yield return null;
            yield return null;
            ApplyMouseState(new MouseState { position = screen });
            yield return null;
            yield return null;

            if (additive)
            {
                QueueKeyboard(new KeyboardState());
                DungeonAutomationInputState.ReleaseKey(KeyCode.LeftShift);
            }
        }

        private static IEnumerator ClickButton(Button button)
        {
            if (button == null)
            {
                yield break;
            }

            Canvas.ForceUpdateCanvases();
            PointerEventData pointer = new PointerEventData(EventSystem.current)
            {
                button = PointerEventData.InputButton.Left,
                position = RectTransformUtility.WorldToScreenPoint(
                    null,
                    button.transform.position)
            };
            ExecuteEvents.Execute(
                button.gameObject,
                pointer,
                ExecuteEvents.pointerClickHandler);
            yield return null;
            yield return null;
        }

        private static Button FindRuntimeButton(string name)
        {
            return Resources.FindObjectsOfTypeAll<Button>()
                .FirstOrDefault(button => button != null
                    && button.gameObject.scene.IsValid()
                    && button.gameObject.activeInHierarchy
                    && string.Equals(button.name, name, StringComparison.Ordinal));
        }

        private void AddMedicalSupplies()
        {
            BuildableObject[] buildings = UnityEngine.Object.FindObjectsByType<BuildableObject>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None);
            foreach (IWarehouseFacility warehouse in buildings.OfType<IWarehouseFacility>())
            {
                warehouse?.Inventory?.AddStock(StockCategory.Medicine, 12);
            }

            int beds = buildings.Count(building =>
                building != null
                && !building.isDestroy
                && building.BuildingData?.GetAbility<BuildingMedicalAbility>() != null);
            Check(beds > 0, "MEDICAL_FACILITY", $"available={beds}");
            Check(
                survivalFoodRuntime?.GetStoredStockCount(StockCategory.Medicine) > 0,
                "MEDICINE",
                $"stored={survivalFoodRuntime?.GetStoredStockCount(StockCategory.Medicine) ?? 0}");
        }

        private static void FillSafeConditions(CharacterActor actor)
        {
            CharacterStats stats = actor?.Stats;
            if (stats == null)
            {
                return;
            }

            foreach (CharacterCondition condition in Enum.GetValues(typeof(CharacterCondition)))
            {
                if (stats.Stats.ContainsKey(condition))
                {
                    stats.Stats[condition] = 100f;
                }
            }
        }

        private void FocusCamera(Vector3 world)
        {
            if (gameplayCamera == null)
            {
                return;
            }

            gameplayCamera.transform.position = new Vector3(
                world.x,
                world.y,
                gameplayCamera.transform.position.z);
            gameplayCamera.GetComponent<CameraManager>()?.ClampToCurrentBounds();
        }

        private IEnumerator Capture(string path)
        {
            yield return new WaitForEndOfFrame();
            Texture2D capture = PlayModeVerificationFrameWait.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, capture.EncodeToPNG());
            Destroy(capture);
            checks.Add($"CAPTURE=PASS; {path}");
        }

        private void Finish()
        {
            Time.timeScale = 0f;
            DungeonAutomationInputState.Disable();
            TeardownInput();
            Application.logMessageReceived -= OnLogMessageReceived;
            Check(capturedErrors.Count == 0, "CONSOLE_ERRORS", string.Join(" | ", capturedErrors));
            Check(capturedWarnings.Count == 0, "CONSOLE_WARNINGS", string.Join(" | ", capturedWarnings));

            bool passed = failures.Count == 0;
            checks.Add($"RESULT={(passed ? "PASS" : "FAIL")}; failures={failures.Count}; "
                + string.Join(" | ", failures));
            File.WriteAllText(ReportPath, string.Join(Environment.NewLine, checks));
            completed = true;
            report = $"{(passed ? "PASS" : "FAIL")}: {string.Join(" | ", failures)}; "
                + $"stabilized={sawStabilizing}; carried={sawPhysicalCarry}; "
                + $"treated={sawTreating}; recovered={patient != null && patient.CurrentLifecycleState == CharacterLifecycleState.Active}";

            if (originalGameViewSizeIndex >= 0)
            {
                GameViewResolutionController.SelectedSizeIndex = originalGameViewSizeIndex;
            }

            if (passed)
            {
                Debug.Log($"COMBAT_V14_PLAYMODE {report}");
            }
            else
            {
                Debug.LogError($"COMBAT_V14_PLAYMODE {report}");
            }
        }

        private void OnDestroy()
        {
            Application.logMessageReceived -= OnLogMessageReceived;
            DungeonAutomationInputState.Disable();
            TeardownInput();
            if (!completed)
            {
                Time.timeScale = originalTimeScale;
            }
        }

        private void OnLogMessageReceived(string condition, string stackTrace, LogType type)
        {
            if (condition != null && condition.Contains("COMBAT_V14_PLAYMODE", StringComparison.Ordinal))
            {
                return;
            }

            if (type is LogType.Error or LogType.Exception or LogType.Assert)
            {
                capturedErrors.Add(condition ?? type.ToString());
            }
            else if (type == LogType.Warning)
            {
                capturedWarnings.Add(condition ?? "Warning");
            }
        }

        private void Check(bool passed, string key, string detail)
        {
            checks.Add($"{key}={(passed ? "PASS" : "FAIL")}; {detail}");
            if (!passed)
            {
                failures.Add($"{key}: {detail}");
            }
        }

        private static CharacterActor[] GetActiveStaff()
        {
            return UnityEngine.Object.FindObjectsByType<CharacterActor>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Select(CharacterActorCollection.GetCanonical)
                .Where(actor => actor != null
                    && !actor.IsDead
                    && !actor.IsOwner
                    && actor.characterType != CharacterType.Customer
                    && actor.characterType != CharacterType.Intruder
                    && actor.CurrentLifecycleState == CharacterLifecycleState.Active)
                .Distinct()
                .ToArray();
        }

        private static CharacterBodyPartHealthState ClonePart(CharacterBodyPartHealthState part)
        {
            return new CharacterBodyPartHealthState
            {
                bodyPart = part.bodyPart,
                maxHealth = part.maxHealth,
                currentHealth = part.currentHealth,
                bleedingPerSecond = part.bleedingPerSecond
            };
        }

        private static string GetId(CharacterActor actor)
        {
            return actor?.Identity?.PersistentId ?? string.Empty;
        }

        private static string GetName(CharacterActor actor)
        {
            return actor?.Identity?.DisplayName ?? actor?.name ?? "없음";
        }

        private static int Percent(float completed, float required)
        {
            return Mathf.RoundToInt(Mathf.Clamp01(completed / Mathf.Max(0.01f, required)) * 100f);
        }

        private static bool TryPrepareStartParty(out string message)
        {
            message = string.Empty;
            DungeonRuntimeLifetimeScope scope =
                UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                    FindObjectsInactive.Include);
            if (scope == null || scope.Container == null)
            {
                message = "LifetimeScope 없음";
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
                if (owner == null || !preparation.Begin(owner, out message))
                {
                    return false;
                }

                bool prepared = preparation.TryCreatePreparedSnapshot(
                    DungeonDifficulty.Normal,
                    Environment.TickCount == 0 ? 1 : Environment.TickCount,
                    out PreparedStartPartySnapshot snapshot,
                    out message);
                preparation.Cancel();
                return prepared && applier.TryApply(snapshot, out message);
            }
            catch (Exception exception)
            {
                message = $"{exception.GetType().Name}: {exception.Message}";
                return false;
            }
        }

        private static TService ResolveService<TService>()
            where TService : class
        {
            DungeonRuntimeLifetimeScope scope =
                UnityEngine.Object.FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                    FindObjectsInactive.Include);
            return scope?.Container != null
                ? scope.Container.Resolve<TService>()
                : null;
        }

        private void SetupInput()
        {
            originalInputBehavior = InputSystem.settings.editorInputBehaviorInPlayMode;
            InputSystem.settings.editorInputBehaviorInPlayMode =
                InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            originalMouse = Mouse.current;
            originalKeyboard = Keyboard.current;
            if (originalMouse != null)
            {
                InputSystem.DisableDevice(originalMouse);
            }

            if (originalKeyboard != null)
            {
                InputSystem.DisableDevice(originalKeyboard);
            }

            verificationMouse = InputSystem.AddDevice<Mouse>(
                "CombatV14VerificationMouse");
            verificationKeyboard = InputSystem.AddDevice<Keyboard>(
                "CombatV14VerificationKeyboard");
            verificationMouse.MakeCurrent();
            verificationKeyboard.MakeCurrent();
        }

        private void ApplyMouseState(MouseState state)
        {
            if (verificationMouse == null || !verificationMouse.added)
            {
                return;
            }

            verificationMouse.MakeCurrent();
            InputState.Change(verificationMouse, state);
            InputSystem.QueueStateEvent(verificationMouse, state);
            InputSystem.Update();
        }

        private void QueueKeyboard(KeyboardState state)
        {
            if (verificationKeyboard == null || !verificationKeyboard.added)
            {
                return;
            }

            verificationKeyboard.MakeCurrent();
            InputSystem.QueueStateEvent(verificationKeyboard, state);
            InputSystem.Update();
        }

        private void TeardownInput()
        {
            if (verificationMouse != null && verificationMouse.added)
            {
                InputSystem.RemoveDevice(verificationMouse);
            }

            if (verificationKeyboard != null && verificationKeyboard.added)
            {
                InputSystem.RemoveDevice(verificationKeyboard);
            }

            verificationMouse = null;
            verificationKeyboard = null;
            if (originalMouse != null && originalMouse.added)
            {
                InputSystem.EnableDevice(originalMouse);
                originalMouse.MakeCurrent();
            }

            if (originalKeyboard != null && originalKeyboard.added)
            {
                InputSystem.EnableDevice(originalKeyboard);
                originalKeyboard.MakeCurrent();
            }

            InputSystem.settings.editorInputBehaviorInPlayMode = originalInputBehavior;
            originalMouse = null;
            originalKeyboard = null;
        }
    }
}
