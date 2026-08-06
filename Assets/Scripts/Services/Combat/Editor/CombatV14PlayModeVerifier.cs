using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
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
        private ICharacterMedicalQuery medicalQuery;
        private ICharacterMedicalCommand medicalCommands;
        private ICharacterMedicalPersistence medicalPersistence;
        private CharacterBodyHealthRuntime bodyHealthRuntime;
        private ICharacterCombatCommandRuntime combatCommandRuntime;
        private IDefenseTacticalCoordinator defenseTacticalRuntime;
        private ICombatEquipmentRuntime combatEquipmentRuntime;
        private ICombatEquipmentMaintenanceRuntime equipmentMaintenanceRuntime;
        private ICaptivityRuntime captivityRuntime;
        private ICaptivityPersistence captivityPersistence;
        private ICircusRuntime circusRuntime;
        private ICircusPersistence circusPersistence;
        private IDoorAccessQuery doorAccessRuntime;
        private DungeonRuntimeAggregateRootStore aggregateRootStore;
        private IWorldItemStackRuntime itemStackRuntime;
        private IResourceEconomyContentCatalog resourceCatalog;
        private IDungeonGameSaveService saveService;
        private IDungeonSaveSectionRegistry saveRegistry;
        private IGameEventBus gameEventBus;
        private ICharacterAiWorldRegistry characterWorldRegistry;
        private IRestoreWorldCandidateQuery restoreWorldCandidates;
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
        private DungeonAutomationInputTestCapability automationInput;
        private IDisposable rescueNoticeSubscription;
        private IDisposable rescueTerminalSubscription;
        private string lastRescueNotice = string.Empty;
        private string lastRescueTerminal = string.Empty;

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
                && medicalQuery != null
                && medicalQuery.TryGetOrder(
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
                ? $"{current.state}/{current.statusCode}/rescuer={current.rescuerId}/"
                    + $"stab={current.completedStabilizationWork:0.##}/"
                    + $"{current.requiredStabilizationWork:0.##}"
                : "none";
            string medicalOrders = medicalPersistence != null
                ? string.Join(
                    ",",
                    medicalPersistence.Capture().orders.Select(order =>
                        $"{order.orderId}:{order.state}:patient={order.patientId}:"
                        + $"rescuer={order.rescuerId}:carried={order.carried}"))
                : "unavailable";
            CharacterBodyHealthSnapshot patientBody = patient != null
                && bodyHealthRuntime != null
                    ? bodyHealthRuntime.GetSnapshot(patient)
                    : default;
            string bodyText = patientBody.Parts != null
                ? $"downed={patientBody.Downed}/c={patientBody.Consciousness:0.##}/"
                    + $"m={patientBody.Mobility:0.##}/parts="
                    + string.Join(
                        ",",
                        patientBody.Parts.Select(part =>
                            $"{part.bodyPart}:{part.currentHealth:0.##}/{part.maxHealth:0.##}"))
                : "none";
            string registryText = DescribeCharacterRegistry(patient);

            return $"rescuer={GetName(rescuer)} cell={rescuer?.GetNowXY()} "
                + $"active={rescuer?.CurrentLifecycleState} aiPaused={rescuer?.IsAiPaused()} "
                + $"stance={combatCommandRuntime?.IsInCombatStance(rescuer)}; "
                + $"command={commandText}; "
                + $"ability={abilityText}; "
                + $"patient={GetName(patient)} cell={patient?.GetNowXY()} "
                + $"state={patient?.CurrentLifecycleState}; "
                + $"order={orderText}; body={bodyText}; orders={medicalOrders}; "
                + registryText;
        }

        private IEnumerator Start()
        {
            Application.logMessageReceived += OnLogMessageReceived;
            originalTimeScale = Time.timeScale;
            originalGameViewSizeIndex = GameViewResolutionController.SelectedSizeIndex;
            GameViewResolutionController.Select(1600, 900);
            SetupInput();
            automationInput = new DungeonAutomationInputTestCapability();
            automationInput.Enable();
            Time.timeScale = 1f;

            yield return WaitForRuntime();
            if (failures.Count > 0)
            {
                Finish();
                yield break;
            }

            VerifyInvalidMedicalPreflightPreservesLiveOrders();
            VerifyInvalidCombatCommandPreflightPreservesLiveCommands();
            VerifyInvalidDefenseTacticalPreflightPreservesReservations();
            VerifyInvalidEquipmentMaintenancePreflightPreservesState();
            VerifyInvalidCaptivityPreflightPreservesState();
            VerifyInvalidCircusPreflightPreservesState();
            VerifyRestoreCandidatesDiscardedAfterPreflightFailure();
            VerifyCombatCommandLateParticipantRollbackAndComplete();
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
            VerifySequenceOverflowGuards();
            Finish();
        }

        private IEnumerator WaitForRuntime()
        {
            float timeout = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < timeout)
            {
                medicalQuery ??= ResolveService<ICharacterMedicalQuery>();
                medicalCommands ??= ResolveService<ICharacterMedicalCommand>();
                medicalPersistence ??= ResolveService<ICharacterMedicalPersistence>();
                bodyHealthRuntime ??= ResolveService<CharacterBodyHealthRuntime>();
                combatCommandRuntime ??= ResolveService<ICharacterCombatCommandRuntime>();
                defenseTacticalRuntime ??= ResolveService<IDefenseTacticalCoordinator>();
                combatEquipmentRuntime ??= ResolveService<ICombatEquipmentRuntime>();
                equipmentMaintenanceRuntime ??=
                    ResolveService<ICombatEquipmentMaintenanceRuntime>();
                captivityRuntime ??= ResolveService<ICaptivityRuntime>();
                captivityPersistence ??= ResolveService<ICaptivityPersistence>();
                circusRuntime ??= ResolveService<ICircusRuntime>();
                circusPersistence ??= ResolveService<ICircusPersistence>();
                doorAccessRuntime ??= ResolveService<IDoorAccessQuery>();
                aggregateRootStore ??=
                    ResolveService<DungeonRuntimeAggregateRootStore>();
                itemStackRuntime ??= ResolveService<IWorldItemStackRuntime>();
                resourceCatalog ??= ResolveService<IResourceEconomyContentCatalog>();
                saveService ??= ResolveService<IDungeonGameSaveService>();
                saveRegistry ??= ResolveService<IDungeonSaveSectionRegistry>();
                gameEventBus ??= ResolveService<IGameEventBus>();
                characterWorldRegistry ??= ResolveService<ICharacterAiWorldRegistry>();
                restoreWorldCandidates ??= ResolveService<IRestoreWorldCandidateQuery>();
                commands = UnityEngine.Object.FindFirstObjectByType<OwnerCommandController>(
                    FindObjectsInactive.Include);
                gameplayCamera = UnityEngine.Object.FindFirstObjectByType<Camera>(
                    FindObjectsInactive.Include);
                CharacterActor[] staff = GetActiveStaff();
                bool servicesReady = medicalQuery != null
                    && medicalCommands != null
                    && medicalPersistence != null
                    && bodyHealthRuntime != null
                    && combatCommandRuntime != null
                    && defenseTacticalRuntime != null
                    && combatEquipmentRuntime != null
                    && equipmentMaintenanceRuntime != null
                    && captivityRuntime != null
                    && captivityPersistence != null
                    && circusRuntime != null
                    && circusPersistence != null
                    && doorAccessRuntime != null
                    && aggregateRootStore != null
                    && itemStackRuntime != null
                    && resourceCatalog != null
                    && saveService != null
                    && saveRegistry != null
                    && gameEventBus != null
                    && characterWorldRegistry != null
                    && restoreWorldCandidates != null;
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
                    + $"medicalQuery={medicalQuery != null}; "
                    + $"medicalCommands={medicalCommands != null}; "
                    + $"medicalPersistence={medicalPersistence != null}; "
                + $"body={bodyHealthRuntime != null}; "
                + $"combat={combatCommandRuntime != null}; "
                    + $"defenseTactics={defenseTacticalRuntime != null}; "
                    + $"maintenance={equipmentMaintenanceRuntime != null}; "
                    + $"captivity={captivityRuntime != null}; "
                    + $"save={saveService != null}; "
                    + $"actors={GetActiveStaff().Length}");
        }

        private void VerifySequenceOverflowGuards()
        {
            VerifyCombatCommandSequenceOverflow();
            VerifyDefenseTacticalSequenceOverflow();
            VerifyCharacterMedicalSequenceOverflow();
        }

        private void VerifyCombatCommandSequenceOverflow()
        {
            IDungeonRestoreTransactionParticipant participant =
                combatCommandRuntime as IDungeonRestoreTransactionParticipant;
            DungeonSaveSectionRegistry registry = CreateIsolatedRegistry(
                CharacterCombatCommandSaveSection.Id,
                participant);
            List<DungeonSaveSectionEnvelope> original = registry.CaptureAll();
            string actorId = GetId(rescuer);
            try
            {
                CharacterCombatCommandSaveData exhausted = new()
                {
                    commandSequence = int.MaxValue,
                    stanceCharacterIds = new List<string> { actorId },
                    revisions = new List<CharacterCombatCommandRevisionSaveData>
                    {
                        new()
                        {
                            actorId = actorId,
                            revision = 1
                        }
                    },
                    commands = new List<CharacterCombatCommand>
                    {
                        new()
                        {
                            commandId = $"combat-command:{int.MaxValue}",
                            actorId = actorId,
                            type = CombatCommandType.Move,
                            state = CharacterCombatCommandState.Queued,
                            hasTargetCell = true,
                            targetX = rescuer.GetNowXY().x,
                            targetY = rescuer.GetNowXY().y,
                            revision = 1
                        }
                    }
                };
                List<DungeonSaveSectionEnvelope> candidate =
                    registry.CaptureAll();
                DungeonSaveSectionEnvelope envelope = candidate.Single(item =>
                    item.sectionId == CharacterCombatCommandSaveSection.Id);
                envelope.payloadJson = JsonUtility.ToJson(exhausted);
                DungeonGameRestoreReport restoreReport = new();
                bool restoredMax = registry.RestoreAll(
                    candidate,
                    restoreReport);
                IReadOnlyList<CharacterCombatCommand> beforeView =
                    combatCommandRuntime.ActiveCommands;
                string before = JsonUtility.ToJson(
                    combatCommandRuntime.Capture());

                bool issued = combatCommandRuntime.TryIssueForceFireAtCell(
                    rescuer,
                    rescuer.GetNowXY(),
                    out string failureReason);

                IReadOnlyList<CharacterCombatCommand> afterView =
                    combatCommandRuntime.ActiveCommands;
                string after = JsonUtility.ToJson(
                    combatCommandRuntime.Capture());
                Check(restoredMax
                        && restoreReport.Success
                        && !issued
                        && !string.IsNullOrWhiteSpace(failureReason)
                        && ReferenceEquals(beforeView, afterView)
                        && string.Equals(before, after, StringComparison.Ordinal)
                        && combatCommandRuntime.Capture().commandSequence
                            == int.MaxValue,
                    "COMBAT_COMMAND_SEQUENCE_EXHAUSTED_ATOMIC",
                    $"restored={restoredMax}; issued={issued}; "
                        + $"reason={failureReason}; "
                        + $"sequence={combatCommandRuntime.Capture().commandSequence}");
            }
            finally
            {
                DungeonGameRestoreReport cleanupReport = new();
                registry.RestoreAll(original, cleanupReport);
            }
        }

        private void VerifyDefenseTacticalSequenceOverflow()
        {
            DefenseTacticalCoordinatorSaveData original =
                defenseTacticalRuntime.Capture();
            string existingActorId = GetId(rescuer);
            string nextActorId = GetId(patient);
            Vector2Int existingCell = rescuer.GetNowXY();
            Vector2Int nextCell = patient.GetNowXY();
            try
            {
                DefenseTacticalCoordinatorSaveData lastAvailable = new()
                {
                    sequence = int.MaxValue - 1
                };
                defenseTacticalRuntime.PublishRestore(
                    defenseTacticalRuntime.PrepareRestore(lastAvailable));
                bool reachedIssuancePath = defenseTacticalRuntime.TryReserve(
                    nextActorId,
                    string.Empty,
                    nextCell,
                    CombatPositionReservationKind.Move,
                    0f,
                    out string setupFailure);

                DefenseTacticalCoordinatorSaveData exhausted = new()
                {
                    sequence = int.MaxValue,
                    reservations = new List<CombatPositionReservation>
                    {
                        new()
                        {
                            reservationId =
                                $"combat-position:{int.MaxValue}",
                            actorId = existingActorId,
                            targetId = string.Empty,
                            kind = CombatPositionReservationKind.Move,
                            x = existingCell.x,
                            y = existingCell.y
                        }
                    }
                };
                defenseTacticalRuntime.PublishRestore(
                    defenseTacticalRuntime.PrepareRestore(exhausted));
                IReadOnlyList<CombatPositionReservation> beforeView =
                    defenseTacticalRuntime.Reservations;
                string before = JsonUtility.ToJson(
                    defenseTacticalRuntime.Capture());

                bool reserved = defenseTacticalRuntime.TryReserve(
                    nextActorId,
                    string.Empty,
                    nextCell,
                    CombatPositionReservationKind.Move,
                    0f,
                    out string failureReason);

                IReadOnlyList<CombatPositionReservation> afterView =
                    defenseTacticalRuntime.Reservations;
                string after = JsonUtility.ToJson(
                    defenseTacticalRuntime.Capture());
                Check(reachedIssuancePath
                        && !reserved
                        && !string.IsNullOrWhiteSpace(failureReason)
                        && ReferenceEquals(beforeView, afterView)
                        && string.Equals(before, after, StringComparison.Ordinal)
                        && defenseTacticalRuntime.Capture().sequence
                            == int.MaxValue,
                    "DEFENSE_TACTICAL_SEQUENCE_EXHAUSTED_ATOMIC",
                    $"setup={reachedIssuancePath}/{setupFailure}; "
                        + $"reserved={reserved}; reason={failureReason}; "
                        + $"sequence={defenseTacticalRuntime.Capture().sequence}");
            }
            finally
            {
                defenseTacticalRuntime.PublishRestore(
                    defenseTacticalRuntime.PrepareRestore(original));
            }
        }

        private void VerifyCharacterMedicalSequenceOverflow()
        {
            IDungeonRestoreTransactionParticipant participant =
                medicalPersistence as IDungeonRestoreTransactionParticipant;
            DungeonSaveSectionRegistry registry = CreateIsolatedRegistry(
                CharacterMedicalSaveSection.Id,
                participant);
            List<DungeonSaveSectionEnvelope> original = registry.CaptureAll();
            CharacterBodyHealthSnapshot originalBody =
                bodyHealthRuntime.GetSnapshot(patient);
            try
            {
                bodyHealthRuntime.ApplySnapshot(
                    patient,
                    new CharacterBodyHealthSnapshot(
                        originalBody.Parts.Select(ClonePart).ToList(),
                        5f,
                        0f,
                        1f,
                        1f,
                        0.08f,
                        true),
                    "medical sequence overflow fixture");

                DungeonCharacterMedicalSaveData exhausted = new()
                {
                    version = DungeonCharacterMedicalSaveData.CurrentVersion,
                    orderSequence = int.MaxValue,
                    orders = new List<CharacterMedicalOrder>
                    {
                        new()
                        {
                            orderId = $"medical:{int.MaxValue}",
                            patientId = GetId(rescuer),
                            state = CharacterMedicalOrderState.Completed,
                            statusCode =
                                CharacterMedicalStatusCode.TreatmentCompleted
                        }
                    }
                };
                List<DungeonSaveSectionEnvelope> candidate =
                    registry.CaptureAll();
                DungeonSaveSectionEnvelope envelope = candidate.Single(item =>
                    item.sectionId == CharacterMedicalSaveSection.Id);
                envelope.payloadJson = JsonUtility.ToJson(exhausted);
                DungeonGameRestoreReport restoreReport = new();
                bool restoredMax = registry.RestoreAll(
                    candidate,
                    restoreReport);
                IReadOnlyList<CharacterMedicalOrder> beforeView =
                    medicalQuery.ActiveOrders;
                string before = JsonUtility.ToJson(
                    medicalPersistence.Capture());
                CharacterLifecycleState lifecycleBefore =
                    patient.CurrentLifecycleState;
                bool aiPausedBefore = patient.IsAiPaused();
                bool failedExplicitly = false;
                try
                {
                    medicalCommands.NotifyCharacterDowned(patient);
                }
                catch (InvalidOperationException exception)
                {
                    failedExplicitly = exception.Message.Contains(
                        "sequence is exhausted",
                        StringComparison.Ordinal);
                }

                IReadOnlyList<CharacterMedicalOrder> afterView =
                    medicalQuery.ActiveOrders;
                string after = JsonUtility.ToJson(
                    medicalPersistence.Capture());
                Check(restoredMax
                        && restoreReport.Success
                        && failedExplicitly
                        && ReferenceEquals(beforeView, afterView)
                        && string.Equals(before, after, StringComparison.Ordinal)
                        && medicalPersistence.Capture().orderSequence
                            == int.MaxValue
                        && patient.CurrentLifecycleState == lifecycleBefore
                        && patient.IsAiPaused() == aiPausedBefore,
                    "CHARACTER_MEDICAL_SEQUENCE_EXHAUSTED_ATOMIC",
                    $"restored={restoredMax}; failed={failedExplicitly}; "
                        + $"sequence={medicalPersistence.Capture().orderSequence}; "
                        + $"lifecycle={lifecycleBefore}->{patient.CurrentLifecycleState}; "
                        + $"aiPaused={aiPausedBefore}->{patient.IsAiPaused()}");
            }
            finally
            {
                DungeonGameRestoreReport cleanupReport = new();
                registry.RestoreAll(original, cleanupReport);
                bodyHealthRuntime.ApplySnapshot(
                    patient,
                    originalBody,
                    "medical sequence overflow cleanup");
            }
        }

        private DungeonSaveSectionRegistry CreateIsolatedRegistry(
            string sectionId,
            IDungeonRestoreTransactionParticipant participant)
        {
            IDungeonSaveSection section = saveRegistry.OrderedSections.Single(
                candidate => candidate.SectionId == sectionId);
            List<IDungeonSaveSection> sections = section.DependsOn
                .Distinct(StringComparer.Ordinal)
                .Select(dependency =>
                    (IDungeonSaveSection)new CombatDependencyMarkerSection(
                        dependency))
                .Append(section)
                .ToList();
            return new DungeonSaveSectionRegistry(
                sections,
                aggregateRootStore,
                participant != null
                    ? new[] { participant }
                    : Array.Empty<IDungeonRestoreTransactionParticipant>());
        }

        private void VerifyInvalidMedicalPreflightPreservesLiveOrders()
        {
            IReadOnlyList<CharacterMedicalOrder> beforeView =
                medicalQuery.ActiveOrders;
            string before = JsonUtility.ToJson(medicalPersistence.Capture());
            DungeonGameSaveData invalid = saveService.Capture();
            if (!DungeonSaveSectionPayload.TryRead(
                    invalid,
                    CharacterMedicalSaveSection.Id,
                    out DungeonCharacterMedicalSaveData medical))
            {
                Check(
                    false,
                    "MEDICAL_PREFLIGHT_ATOMIC",
                    "의료 저장 섹션을 찾지 못했습니다.");
                return;
            }

            medical.version = DungeonCharacterMedicalSaveData.CurrentVersion - 1;
            DungeonSaveSectionPayload.Write(
                invalid,
                CharacterMedicalSaveSection.Id,
                DungeonCharacterMedicalSaveData.CurrentVersion,
                DungeonSaveRestorePhase.RuntimeState,
                medical);
            invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);

            bool restored = saveService.TryRestore(
                invalid,
                out DungeonGameRestoreReport restoreReport);
            IReadOnlyList<CharacterMedicalOrder> afterView =
                medicalQuery.ActiveOrders;
            string after = JsonUtility.ToJson(medicalPersistence.Capture());
            Check(
                !restored
                    && restoreReport != null
                    && !restoreReport.Success
                    && ReferenceEquals(beforeView, afterView)
                    && string.Equals(before, after, StringComparison.Ordinal),
                "MEDICAL_PREFLIGHT_ATOMIC",
                restored
                    ? "잘못된 의료 payload가 승인됐습니다."
                    : $"orders={afterView.Count}; errors="
                        + $"{string.Join(" | ", restoreReport?.Errors ?? Array.Empty<string>())}");
        }

        private void VerifyInvalidCombatCommandPreflightPreservesLiveCommands()
        {
            IReadOnlyList<CharacterCombatCommand> beforeView =
                combatCommandRuntime.ActiveCommands;
            string before = JsonUtility.ToJson(combatCommandRuntime.Capture());
            DungeonGameSaveData invalid = saveService.Capture();
            if (!DungeonSaveSectionPayload.TryRead(
                    invalid,
                    CharacterCombatCommandSaveSection.Id,
                    out CharacterCombatCommandSaveData commands))
            {
                Check(false, "COMBAT_COMMAND_PREFLIGHT_ATOMIC",
                    "전투 명령 저장 섹션을 찾지 못했습니다.");
                return;
            }

            commands.commandSequence = -1;
            DungeonSaveSectionPayload.Write(
                invalid,
                CharacterCombatCommandSaveSection.Id,
                2,
                DungeonSaveRestorePhase.LateRuntimeState,
                commands);
            invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);

            bool restored = saveService.TryRestore(
                invalid,
                out DungeonGameRestoreReport restoreReport);
            IReadOnlyList<CharacterCombatCommand> afterView =
                combatCommandRuntime.ActiveCommands;
            string after = JsonUtility.ToJson(combatCommandRuntime.Capture());
            Check(
                !restored
                    && restoreReport != null
                    && !restoreReport.Success
                    && ReferenceEquals(beforeView, afterView)
                    && string.Equals(before, after, StringComparison.Ordinal),
                "COMBAT_COMMAND_PREFLIGHT_ATOMIC",
                restored
                    ? "잘못된 전투 명령 payload가 승인됐습니다."
                    : $"commands={afterView.Count}; errors="
                        + $"{string.Join(" | ", restoreReport?.Errors ?? Array.Empty<string>())}");
        }

        private void VerifyCombatCommandLateParticipantRollbackAndComplete()
        {
            IDungeonRestoreTransactionParticipant combatParticipant =
                combatCommandRuntime as IDungeonRestoreTransactionParticipant;
            IDungeonSaveSection combatSection = saveRegistry?.OrderedSections
                .SingleOrDefault(section =>
                    section.SectionId == CharacterCombatCommandSaveSection.Id);
            if (combatParticipant == null
                || combatSection == null
                || rescuer == null
                || rescuer.CurrentLifecycleState != CharacterLifecycleState.Active)
            {
                Check(
                    false,
                    "COMBAT_COMMAND_LATE_PARTICIPANT",
                    $"participant={combatParticipant != null}; section={combatSection != null}; actor={rescuer != null}");
                return;
            }

            DefenseCombatPresentation presentation =
                DefenseCombatPresentation.Ensure(rescuer);
            CombatActorProjectionProbe expected = new(rescuer, presentation);
            IReadOnlyList<CharacterCombatCommand> previousView =
                combatCommandRuntime.ActiveCommands;
            string previousCapture = JsonUtility.ToJson(
                combatCommandRuntime.Capture());
            int previousRevision = aggregateRootStore.PublishedRestoreRevision;
            LateCombatParticipantFaultProbe lateParticipant = new(
                expected,
                () => ReferenceEquals(
                    previousView,
                    combatCommandRuntime.ActiveCommands));
            DungeonSaveSectionRegistry registry = new(
                new IDungeonSaveSection[]
                {
                    new CombatDependencyMarkerSection(
                        CharacterBodyHealthSaveSection.Id),
                    new CombatDependencyMarkerSection(
                        CombatEquipmentSaveSection.Id),
                    new CombatDependencyMarkerSection(
                        DefenseTacticalSaveSection.Id),
                    combatSection
                },
                aggregateRootStore,
                new[] { combatParticipant, lateParticipant });

            List<DungeonSaveSectionEnvelope> envelopes = registry.CaptureAll();
            DungeonSaveSectionEnvelope commandEnvelope = envelopes.Single(
                envelope =>
                    envelope.sectionId == CharacterCombatCommandSaveSection.Id);
            CharacterCombatCommandSaveData candidate =
                JsonUtility.FromJson<CharacterCombatCommandSaveData>(
                    commandEnvelope.payloadJson);
            string actorId = CharacterPersistentIdentity.Require(rescuer).Value;
            bool candidateStance = !candidate.stanceCharacterIds.Contains(
                actorId,
                StringComparer.Ordinal);
            candidate.stanceCharacterIds.RemoveAll(id => string.Equals(
                id,
                actorId,
                StringComparison.Ordinal));
            if (candidateStance)
            {
                candidate.stanceCharacterIds.Add(actorId);
            }
            commandEnvelope.payloadJson = JsonUtility.ToJson(candidate);

            DungeonGameRestoreReport failureReport = new();
            bool failedRestoreAccepted = registry.RestoreAll(
                envelopes,
                failureReport);
            bool exactRollback = !failedRestoreAccepted
                && !failureReport.Success
                && failureReport.Errors.Any(error => error.Contains(
                    LateCombatParticipantFaultProbe.FailureMessage,
                    StringComparison.Ordinal))
                && lateParticipant.PublishCount == 1
                && lateParticipant.RollbackCount == 1
                && lateParticipant.CompleteCount == 0
                && lateParticipant.ObservedExactBeforeFailure
                && expected.MatchesExact()
                && ReferenceEquals(
                    previousView,
                    combatCommandRuntime.ActiveCommands)
                && string.Equals(
                    previousCapture,
                    JsonUtility.ToJson(combatCommandRuntime.Capture()),
                    StringComparison.Ordinal)
                && aggregateRootStore.PublishedRestoreRevision
                    == previousRevision
                && !aggregateRootStore.IsRestoreStaging;
            if (!exactRollback)
            {
                expected.RestoreForCleanup();
                Check(
                    false,
                    "COMBAT_COMMAND_LATE_PARTICIPANT",
                    $"accepted={failedRestoreAccepted}; errors={string.Join(" | ", failureReport.Errors)}; "
                    + $"publish={lateParticipant.PublishCount}; rollback={lateParticipant.RollbackCount}; "
                    + $"exactAtFault={lateParticipant.ObservedExactBeforeFailure}; exactAfter={expected.MatchesExact()}");
                return;
            }

            DungeonGameRestoreReport successReport = new();
            bool completed = registry.RestoreAll(envelopes, successReport);
            DefenseCombatPresentation completedPresentation =
                rescuer.GetComponent<DefenseCombatPresentation>();
            bool successProjectedOnlyAfterCompletion = completed
                && successReport.Success
                && lateParticipant.PublishCount == 2
                && lateParticipant.RollbackCount == 1
                && lateParticipant.CompleteCount == 1
                && lateParticipant.ObservedExactBeforeSuccessfulCompletion
                && combatCommandRuntime.IsInCombatStance(rescuer)
                    == candidateStance
                && rescuer.IsAiPaused() == candidateStance
                && completedPresentation != null
                && completedPresentation.IsCombatActive == candidateStance
                && (candidateStance
                    ? !string.IsNullOrWhiteSpace(
                        completedPresentation.CurrentStatus)
                    : string.IsNullOrWhiteSpace(
                        completedPresentation.CurrentStatus))
                && aggregateRootStore.PublishedRestoreRevision
                    == previousRevision + 1
                && !aggregateRootStore.IsRestoreStaging;
            Check(
                successProjectedOnlyAfterCompletion,
                "COMBAT_COMMAND_LATE_PARTICIPANT",
                $"rollbackExact={exactRollback}; completed={completed}; candidateStance={candidateStance}; "
                + $"aiPaused={rescuer.IsAiPaused()}; combatActive={completedPresentation?.IsCombatActive}; "
                + $"publish={lateParticipant.PublishCount}; complete={lateParticipant.CompleteCount}; "
                + $"errors={string.Join(" | ", successReport.Errors)}");
        }

        private void VerifyInvalidDefenseTacticalPreflightPreservesReservations()
        {
            IReadOnlyList<CombatPositionReservation> beforeView =
                defenseTacticalRuntime.Reservations;
            string before = JsonUtility.ToJson(defenseTacticalRuntime.Capture());
            DungeonGameSaveData invalid = saveService.Capture();
            if (!DungeonSaveSectionPayload.TryRead(
                    invalid,
                    DefenseTacticalSaveSection.Id,
                    out DefenseTacticalCoordinatorSaveData tactics))
            {
                Check(false, "DEFENSE_TACTICAL_PREFLIGHT_ATOMIC",
                    "방어 전술 저장 섹션을 찾지 못했습니다.");
                return;
            }

            tactics.sequence = -1;
            DungeonSaveSectionPayload.Write(
                invalid,
                DefenseTacticalSaveSection.Id,
                2,
                DungeonSaveRestorePhase.RuntimeState,
                tactics);
            invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);

            bool restored = saveService.TryRestore(
                invalid,
                out DungeonGameRestoreReport restoreReport);
            IReadOnlyList<CombatPositionReservation> afterView =
                defenseTacticalRuntime.Reservations;
            string after = JsonUtility.ToJson(defenseTacticalRuntime.Capture());
            Check(
                !restored
                    && restoreReport != null
                    && !restoreReport.Success
                    && ReferenceEquals(beforeView, afterView)
                    && string.Equals(before, after, StringComparison.Ordinal),
                "DEFENSE_TACTICAL_PREFLIGHT_ATOMIC",
                restored
                    ? "잘못된 방어 전술 payload가 승인됐습니다."
                    : $"reservations={afterView.Count}; errors="
                        + $"{string.Join(" | ", restoreReport?.Errors ?? Array.Empty<string>())}");
        }

        private void VerifyInvalidEquipmentMaintenancePreflightPreservesState()
        {
            string before = JsonUtility.ToJson(equipmentMaintenanceRuntime.Capture());
            DungeonGameSaveData invalid = saveService.Capture();
            if (!DungeonSaveSectionPayload.TryRead(
                    invalid,
                    EquipmentMaintenanceSaveSection.Id,
                    out CombatEquipmentMaintenanceSaveData maintenance))
            {
                Check(false, "EQUIPMENT_MAINTENANCE_PREFLIGHT_ATOMIC",
                    "장비 정비 저장 섹션을 찾지 못했습니다.");
                return;
            }

            maintenance.policySequence = -1;
            DungeonSaveSectionPayload.Write(
                invalid,
                EquipmentMaintenanceSaveSection.Id,
                2,
                DungeonSaveRestorePhase.RuntimeState,
                maintenance);
            invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);

            bool restored = saveService.TryRestore(
                invalid,
                out DungeonGameRestoreReport restoreReport);
            string after = JsonUtility.ToJson(equipmentMaintenanceRuntime.Capture());
            Check(
                !restored
                    && restoreReport != null
                    && !restoreReport.Success
                    && string.Equals(before, after, StringComparison.Ordinal),
                "EQUIPMENT_MAINTENANCE_PREFLIGHT_ATOMIC",
                restored
                    ? "잘못된 장비 정비 payload가 승인됐습니다."
                    : $"orders={equipmentMaintenanceRuntime.Orders.Count}; errors="
                        + $"{string.Join(" | ", restoreReport?.Errors ?? Array.Empty<string>())}");
        }

        private void VerifyInvalidCaptivityPreflightPreservesState()
        {
            string before = JsonUtility.ToJson(captivityPersistence.Capture());
            int beforeRevision = aggregateRootStore.PublishedRestoreRevision;
            int beforeDoorVersion = doorAccessRuntime.DoorAccessVersion;
            DungeonGameSaveData invalid = saveService.Capture();
            if (!DungeonSaveSectionPayload.TryRead(
                    invalid,
                    CaptivitySaveSection.Id,
                    out CaptivitySaveData captivity))
            {
                Check(false, "CAPTIVITY_PREFLIGHT_ATOMIC",
                    "포로 저장 섹션을 찾지 못했습니다.");
                return;
            }

            captivity.captureSequence = -1;
            DungeonSaveSectionPayload.Write(
                invalid,
                CaptivitySaveSection.Id,
                CaptivitySaveData.CurrentVersion,
                DungeonSaveRestorePhase.LateRuntimeState,
                captivity);
            invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);

            bool restored = saveService.TryRestore(
                invalid,
                out DungeonGameRestoreReport restoreReport);
            string after = JsonUtility.ToJson(captivityPersistence.Capture());
            Check(
                !restored
                    && restoreReport != null
                    && !restoreReport.Success
                    && aggregateRootStore.PublishedRestoreRevision
                        == beforeRevision
                    && doorAccessRuntime.DoorAccessVersion
                        == beforeDoorVersion
                    && string.Equals(before, after, StringComparison.Ordinal),
                "CAPTIVITY_PREFLIGHT_ATOMIC",
                restored
                    ? "잘못된 포로 payload가 승인됐습니다."
                    : $"captives={captivityRuntime.Captives.Count}; revision="
                        + $"{aggregateRootStore.PublishedRestoreRevision}; errors="
                        + $"{string.Join(" | ", restoreReport?.Errors ?? Array.Empty<string>())}");
        }

        private void VerifyInvalidCircusPreflightPreservesState()
        {
            string before = JsonUtility.ToJson(circusPersistence.Capture());
            int beforeRevision = aggregateRootStore.PublishedRestoreRevision;
            int beforeDoorVersion = doorAccessRuntime.DoorAccessVersion;
            DungeonGameSaveData invalid = saveService.Capture();
            if (!DungeonSaveSectionPayload.TryRead(
                    invalid,
                    CircusSaveSection.Id,
                    out CircusSaveData circus))
            {
                Check(false, "CIRCUS_PREFLIGHT_ATOMIC",
                    "서커스 저장 섹션을 찾지 못했습니다.");
                return;
            }

            circus.nextOrderSequence = -1;
            DungeonSaveSectionPayload.Write(
                invalid,
                CircusSaveSection.Id,
                CircusSaveData.CurrentVersion,
                DungeonSaveRestorePhase.LateRuntimeState,
                circus);
            invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);

            bool restored = saveService.TryRestore(
                invalid,
                out DungeonGameRestoreReport restoreReport);
            string after = JsonUtility.ToJson(circusPersistence.Capture());
            Check(
                !restored
                    && restoreReport != null
                    && !restoreReport.Success
                    && aggregateRootStore.PublishedRestoreRevision
                        == beforeRevision
                    && doorAccessRuntime.DoorAccessVersion
                        == beforeDoorVersion
                    && string.Equals(before, after, StringComparison.Ordinal),
                "CIRCUS_PREFLIGHT_ATOMIC",
                restored
                    ? "잘못된 서커스 payload가 승인됐습니다."
                    : $"orders={circusRuntime.Orders.Count}; revision="
                        + $"{aggregateRootStore.PublishedRestoreRevision}; errors="
                        + $"{string.Join(" | ", restoreReport?.Errors ?? Array.Empty<string>())}");
        }

        private void VerifyRestoreCandidatesDiscardedAfterPreflightFailure()
        {
            bool hasCharacterCandidate = restoreWorldCandidates.TryGetCharacters(out _);
            Check(
                !aggregateRootStore.IsRestoreStaging && !hasCharacterCandidate,
                "RESTORE_CANDIDATE_CLEANUP",
                $"aggregateStaging={aggregateRootStore.IsRestoreStaging}; "
                    + $"characterCandidate={hasCharacterCandidate}");
        }

        private void AssignActors(IReadOnlyList<CharacterActor> staff)
        {
            CharacterActor[] available = staff
                .Where(actor => actor != null)
                .OrderBy(GetId, StringComparer.Ordinal)
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

            for (int attempt = 0; attempt < 3; attempt++)
            {
                yield return ClickActor(rescuer, additive: false);
                yield return ClickActor(secondSelected, additive: true);
                HashSet<string> observed = commands.SelectedActors
                    .Select(GetId)
                    .ToHashSet(StringComparer.Ordinal);
                if (observed.Contains(GetId(rescuer))
                    && observed.Contains(GetId(secondSelected)))
                {
                    break;
                }

                yield return null;
                yield return null;
            }

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
            BuildableObject[] medicalFacilities = UnityEngine.Object
                .FindObjectsByType<BuildableObject>(
                    FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None)
                .Where(building => building != null
                    && !building.isDestroy
                    && building.BuildingData?.GetAbility<BuildingMedicalAbility>() != null)
                .ToArray();
            HashSet<Vector2Int> facilityCells = medicalFacilities
                .SelectMany(building => building.buildPoses)
                .ToHashSet();
            List<Vector2Int> candidates = grid.GetCells()
                .Where(cell => cell != null
                    && cell.AreaType == GridCellAreaType.DungeonInterior
                    && grid.IsWalkable(cell.Position)
                    && !occupiedByOthers.Contains(cell.Position)
                    && !facilityCells.Contains(cell.Position)
                    && medicalFacilities.All(building =>
                        Mathf.Abs(cell.Position.x - building.centerPos.x)
                            + Mathf.Abs(cell.Position.y - building.centerPos.y) >= 3))
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
            BlueprintResearchRuntime research =
                UnityEngine.Object.FindFirstObjectByType<BlueprintResearchRuntime>(
                    FindObjectsInactive.Include);
            if (scope == null || scope.Container == null || research == null)
            {
                Check(
                    false,
                    "RANGED_LOADOUT",
                    $"scope={scope != null}; container={scope?.Container != null}; research={research != null}");
                return;
            }

            research.State.Projects.Complete(
                new ResearchProjectId("research:equipment:bowyery"));

            IDungeonItemCatalogProvider itemCatalog =
                scope.Container.Resolve<IDungeonItemCatalogProvider>();
            IItemHaulingSettingsProvider haulingSettings =
                scope.Container.Resolve<IItemHaulingSettingsProvider>();
            foreach (CharacterActor actor in new[] { rescuer, secondSelected }.Distinct())
            {
                string actorId = GetId(actor);
                CombatEquipmentInstance bow;
                try
                {
                    bow = combatEquipmentRuntime.CreateInstance(
                        "weapon:shortbow",
                        CombatEquipmentQuality.Normal);
                }
                catch (Exception exception)
                {
                    Check(
                        false,
                        "RANGED_LOADOUT",
                        $"{GetName(actor)}: {exception.GetType().Name}: {exception.Message}");
                    continue;
                }
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
            foreach (CharacterActor other in UnityEngine.Object
                         .FindObjectsByType<CharacterActor>(
                             FindObjectsInactive.Exclude,
                             FindObjectsSortMode.None)
                         .Select(CharacterActorCollection.GetCanonical)
                         .Where(actor => actor != null
                             && actor != rescuer
                             && actor != patient
                             && !actor.IsDead
                             && actor.characterType is not CharacterType.Customer
                                 and not CharacterType.Intruder)
                         .Distinct())
            {
                other.SetAiPaused(true);
                other.GetComponent<AbilityRescue>()?.StopRescue(
                    CharacterMedicalStatusCode.ReservationReleased);
            }

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

            medicalOrder = medicalQuery.ActiveOrders.FirstOrDefault(order =>
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

            bool rescueStarted = false;
            int rescueAttempts = 0;
            Button rescueButton = null;
            lastRescueNotice = string.Empty;
            lastRescueTerminal = string.Empty;
            rescueNoticeSubscription?.Dispose();
            rescueNoticeSubscription = gameEventBus.Subscribe<NoticeFeedEvent>(notice =>
            {
                lastRescueNotice = notice.notice ?? string.Empty;
            });
            rescueTerminalSubscription?.Dispose();
            rescueTerminalSubscription = gameEventBus.Subscribe<
                CharacterCombatCommandTerminatedEvent>(gameEvent =>
            {
                if (gameEvent.Type == CombatCommandType.Rescue
                    && string.Equals(
                        gameEvent.ActorId,
                        GetId(rescuer),
                        StringComparison.Ordinal))
                {
                    lastRescueTerminal = $"{gameEvent.FinalState}:{gameEvent.Status}";
                }
            });
            for (int attempt = 0; attempt < 3 && !rescueStarted; attempt++)
            {
                rescueAttempts = attempt + 1;
                yield return ClickActor(rescuer, additive: false);
                bool selectedOnlyRescuer = commands.SelectedActors.Count == 1
                    && string.Equals(
                        GetId(commands.SelectedActors[0]),
                        GetId(rescuer),
                        StringComparison.Ordinal);
                if (!selectedOnlyRescuer)
                {
                    yield return null;
                    yield return null;
                    continue;
                }

                if (!combatCommandRuntime.IsInCombatStance(rescuer))
                {
                    Button stanceButton = FindRuntimeButton("CombatStanceButton");
                    yield return ClickButton(stanceButton);
                }

                rescueButton = FindRuntimeButton("CombatMode_Rescue");
                if (rescueButton == null)
                {
                    break;
                }

                yield return ClickButton(rescueButton);
                yield return RightClickActor(patient);
                yield return null;
                yield return null;
                rescueStarted = rescuer.GetComponent<AbilityRescue>()?.IsRescuing == true
                    || combatCommandRuntime.TryGetCommand(rescuer, out _);
            }

            rescueNoticeSubscription.Dispose();
            rescueNoticeSubscription = null;
            rescueTerminalSubscription.Dispose();
            rescueTerminalSubscription = null;

            Check(rescueButton != null, "RESCUE_BUTTON", "구조 명령 버튼 표시");
            if (rescueButton == null)
            {
                yield break;
            }

            int seededMedicine = SeedTreatmentSupplyForOrder(medicalOrder);
            Check(
                seededMedicine > 0,
                "MEDICINE",
                $"physical-buffered={seededMedicine}");
            Check(
                rescueStarted,
                "POINTER_RESCUE_COMMAND",
                $"attempts={rescueAttempts}; selected="
                    + $"{string.Join(",", commands.SelectedActors.Select(GetName))}; "
                    + $"mode={commands.CombatInputMode}; "
                    + $"stance={combatCommandRuntime.IsInCombatStance(rescuer)}; "
                    + $"notice={lastRescueNotice}; terminal={lastRescueTerminal}");
            if (!rescueStarted)
            {
                yield break;
            }

            Time.timeScale = 4f;
            float timeout = Time.realtimeSinceStartup + 60f;
            while (Time.realtimeSinceStartup < timeout)
            {
                FillSafeConditions(rescuer);
                CharacterMedicalOrder current = medicalQuery.ActiveOrders
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

                    report = $"RUNNING: {current.state} · {current.statusCode} · "
                        + $"안정화 {Percent(current.completedStabilizationWork, current.requiredStabilizationWork)}% · "
                        + $"치료 {Percent(current.completedTreatmentWork, current.requiredTreatmentWork)}%";
                }

                if (patient.CurrentLifecycleState == CharacterLifecycleState.Active)
                {
                    break;
                }

                yield return null;
            }

            yield return null;
            yield return null;
            Check(
                !combatCommandRuntime.TryGetCommand(rescuer, out _)
                    && rescuer.GetComponent<AbilityRescue>()?.IsRescuing != true,
                "RESCUE_COMMAND_RELEASED",
                "회복 이벤트에서 구조 명령과 구조 코루틴 정리");

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
            automationInput.MovePointer(screen);
            ApplyMouseState(new MouseState { position = screen });
            yield return null;
            automationInput.ClickPointer(1);
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
            automationInput.MovePointer(screen);
            ApplyMouseState(new MouseState { position = screen });
            yield return null;
            if (additive)
            {
                automationInput.HoldKey(KeyCode.LeftShift, 0.35f);
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
                automationInput.ReleaseKey(KeyCode.LeftShift);
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
            int beds = buildings.Count(building =>
                building != null
                && !building.isDestroy
                && building.BuildingData?.GetAbility<BuildingMedicalAbility>() != null);
            Check(beds > 0, "MEDICAL_FACILITY", $"available={beds}");
        }

        private int SeedTreatmentSupplyForOrder(CharacterMedicalOrder order)
        {
            if (order == null || itemStackRuntime == null || resourceCatalog == null)
            {
                return 0;
            }

            string destinationId = WorldItemStackRuntime.FacilityInputDestinationPrefix
                + $"medical:{order.orderId}";
            int spawnedTotal = 0;
            foreach (ResourceItemDefinitionSO medicine in resourceCatalog.Items
                         .Where(item => item != null
                             && item.Kind == ResourceItemKind.Medicine
                             && item.SupportsInjuryTreatment)
                         .OrderBy(item => item.ItemId, StringComparer.Ordinal))
            {
                if (itemStackRuntime.SpawnItemAt(
                        medicine.ItemId,
                        1,
                        order.BedPosition,
                        WorldItemStackState.FacilityBuffer,
                        destinationId,
                        out int spawned))
                {
                    spawnedTotal += spawned;
                }
            }

            return spawnedTotal;
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
            automationInput?.Dispose();
            automationInput = null;
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
            rescueNoticeSubscription?.Dispose();
            rescueNoticeSubscription = null;
            rescueTerminalSubscription?.Dispose();
            rescueTerminalSubscription = null;
            Application.logMessageReceived -= OnLogMessageReceived;
            automationInput?.Dispose();
            automationInput = null;
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

        private string DescribeCharacterRegistry(CharacterActor target)
        {
            if (target == null || characterWorldRegistry == null)
            {
                return "registry=unavailable";
            }

            string targetId = GetId(target);
            string active = DescribeCharacterMatches(
                characterWorldRegistry.Characters,
                targetId);
            string lifetime = DescribeCharacterMatches(
                characterWorldRegistry.AllCharacters,
                targetId);
            bool hasCandidate = restoreWorldCandidates?.TryGetCharacters(out _) == true;
            return $"registryTarget={target.GetType().Name}@{target.GetInstanceID()}; "
                + $"active=[{active}]; lifetime=[{lifetime}]; "
                + $"candidate={hasCandidate}; "
                + $"aggregateStaging={aggregateRootStore?.IsRestoreStaging}";
        }

        private static string DescribeCharacterMatches(
            IEnumerable<CharacterActor> actors,
            string targetId)
        {
            return string.Join(
                ",",
                (actors ?? Array.Empty<CharacterActor>())
                    .Where(actor => actor != null
                        && string.Equals(GetId(actor), targetId, StringComparison.Ordinal))
                    .Select(actor =>
                    {
                        CharacterActor canonical = CharacterActorCollection.GetCanonical(actor);
                        return $"{actor.GetType().Name}@{actor.GetInstanceID()}:"
                            + $"{actor.CurrentLifecycleState}:canonical="
                            + $"{canonical?.GetType().Name}@{canonical?.GetInstanceID()}:"
                            + $"{canonical?.CurrentLifecycleState}";
                    }));
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

        private sealed class CombatDependencyMarkerSection :
            DungeonDebugStagedSaveSection,
            IDungeonRollbackFreeSaveSection
        {
            private readonly string id;

            internal CombatDependencyMarkerSection(string id)
            {
                this.id = id ?? throw new ArgumentNullException(nameof(id));
            }

            public override string SectionId => id;
            public override DungeonSaveRestorePhase RestorePhase =>
                DungeonSaveRestorePhase.RuntimeState;

            protected override void CommitMarker(
                DungeonGameRestoreReport report)
            {
            }
        }

        private sealed class LateCombatParticipantFaultProbe :
            IDungeonRestoreTransactionParticipant
        {
            internal const string FailureMessage =
                "Intentional later participant failure after combat-command publication.";

            private readonly CombatActorProjectionProbe expected;
            private readonly Func<bool> viewIsExact;
            private bool failNextPublish = true;

            internal LateCombatParticipantFaultProbe(
                CombatActorProjectionProbe expected,
                Func<bool> viewIsExact)
            {
                this.expected = expected
                    ?? throw new ArgumentNullException(nameof(expected));
                this.viewIsExact = viewIsExact
                    ?? throw new ArgumentNullException(nameof(viewIsExact));
            }

            public string ParticipantId =>
                "999.debug.combat-command-late-participant";
            internal int PublishCount { get; private set; }
            internal int RollbackCount { get; private set; }
            internal int CompleteCount { get; private set; }
            internal bool ObservedExactBeforeFailure { get; private set; }
            internal bool ObservedExactBeforeSuccessfulCompletion
            {
                get;
                private set;
            }

            public void BeginRestoreCandidate()
            {
            }

            public void PublishRestoreCandidate()
            {
                PublishCount++;
                bool exact = expected.MatchesExact() && viewIsExact();
                if (failNextPublish)
                {
                    ObservedExactBeforeFailure = exact;
                    failNextPublish = false;
                    throw new InvalidOperationException(FailureMessage);
                }

                ObservedExactBeforeSuccessfulCompletion = exact;
            }

            public void RollbackPublishedRestoreCandidate()
            {
                RollbackCount++;
            }

            public void CompleteRestoreCandidate()
            {
                CompleteCount++;
            }

            public void DiscardRestoreCandidate()
            {
            }
        }

        private sealed class CombatActorProjectionProbe
        {
            private readonly CharacterActor actor;
            private readonly DefenseCombatPresentation presentation;
            private readonly CharacterLifecycleState lifecycleState;
            private readonly bool aiPaused;
            private readonly CharacterDecisionState decisionState;
            private readonly AIAction bestAction;
            private readonly bool isExecuted;
            private readonly bool isBestActionEnd;
            private readonly string phase;
            private readonly string phaseDetail;
            private readonly int brainDebugVersion;
            private readonly string presentationStatus;
            private readonly bool presentationCombatActive;

            internal CombatActorProjectionProbe(
                CharacterActor actor,
                DefenseCombatPresentation presentation)
            {
                this.actor = actor
                    ?? throw new ArgumentNullException(nameof(actor));
                this.presentation = presentation
                    ?? throw new ArgumentNullException(nameof(presentation));
                lifecycleState = actor.CurrentLifecycleState;
                aiPaused = actor.IsAiPaused();
                decisionState = actor.State;
                bestAction = actor.Brain?.bestAction;
                isExecuted = actor.Brain?.isExecuted == true;
                isBestActionEnd = actor.Brain?.isBestActionEnd == true;
                phase = actor.Brain?.CurrentActionPhase ?? string.Empty;
                phaseDetail = actor.Brain?.CurrentActionPhaseDetail
                    ?? string.Empty;
                brainDebugVersion = actor.Brain?.DebugVersion ?? 0;
                presentationStatus = presentation.CurrentStatus;
                presentationCombatActive = presentation.IsCombatActive;
            }

            internal bool MatchesExact()
            {
                AIBrain brain = actor.Brain;
                return actor.CurrentLifecycleState == lifecycleState
                    && actor.IsAiPaused() == aiPaused
                    && actor.State == decisionState
                    && ReferenceEquals(brain?.bestAction, bestAction)
                    && (brain?.isExecuted == true) == isExecuted
                    && (brain?.isBestActionEnd == true) == isBestActionEnd
                    && (brain?.CurrentActionPhase ?? string.Empty) == phase
                    && (brain?.CurrentActionPhaseDetail ?? string.Empty)
                        == phaseDetail
                    && (brain?.DebugVersion ?? 0) == brainDebugVersion
                    && presentation.CurrentStatus == presentationStatus
                    && presentation.IsCombatActive
                        == presentationCombatActive;
            }

            internal void RestoreForCleanup()
            {
                if (actor.CurrentLifecycleState != lifecycleState)
                {
                    actor.SetLifecycleState(lifecycleState);
                }
                if (actor.IsAiPaused() != aiPaused)
                {
                    actor.SetAiPaused(aiPaused);
                }
                actor.state = decisionState;
                if (actor.Brain != null)
                {
                    actor.Brain.bestAction = bestAction;
                    actor.Brain.isExecuted = isExecuted;
                    actor.Brain.isBestActionEnd = isBestActionEnd;
                    actor.Brain.SetActionPhase(phase, detail: phaseDetail);
                }
                presentation.SetStatus(
                    presentationStatus,
                    presentationCombatActive);
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
