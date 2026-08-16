#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;

/// <summary>
/// Production-path proof for Red-alert work suspension, two independent
/// downgrade windows, and one-shot return to the same persistent work order.
/// </summary>
public static class CharacterAlarmResponsePlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/character-alarm-response-playmode.txt";
    private const string PendingFlagPath =
        "Temp/character-alarm-response-playmode.flag";

    [MenuItem("DungeonStory/Debug/QA/Run Character Alarm Response PlayMode Verification")]
    public static void RunFromMenu() => RequestRun();

    public static void RequestRun()
    {
        if (EditorApplication.isPlaying)
        {
            StartRunner();
            return;
        }

        Directory.CreateDirectory("Temp");
        File.WriteAllText(PendingFlagPath, DateTime.UtcNow.ToString("O"));
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BootstrapPendingRun()
    {
        if (!File.Exists(PendingFlagPath)) return;
        File.Delete(PendingFlagPath);
        StartRunner();
    }

    private static void StartRunner()
    {
        if (UnityEngine.Object.FindFirstObjectByType<
                CharacterAlarmResponsePlayModeRunner>() != null)
            return;
        new GameObject("Character Alarm Response PlayMode Runner")
            .AddComponent<CharacterAlarmResponsePlayModeRunner>();
    }
}

public sealed class CharacterAlarmResponsePlayModeRunner : MonoBehaviour
{
    private const string IncidentId = "incident:qa:work-suspension";
    private const string InvasionIncidentId = "incident:invasion:active";
    private const string MedicalIncidentId = "incident:medical-capacity";
    private readonly List<string> checks = new();
    private readonly List<string> failures = new();
    private readonly List<string> consoleIssues = new();
    private readonly List<MonoBehaviourState> pausedAi = new();
    private readonly Dictionary<CharacterCondition, float> workerStats = new();

    private DungeonRuntimeLifetimeScope scope;
    private CharacterActor worker;
    private AbilityWork work;
    private AIBrain brain;
    private AIAction[] originalActions;
    private AIAction workAction;
    private Grid grid;
    private ConstructionSite site;
    private BuildingSO siteDefinition;
    private ExteriorZoneMarker genericWorkTarget;
    private BuildingSO genericWorkDefinition;
    private string orderId = string.Empty;
    private IWorkOrderRuntime orders;
    private IWorldItemStackRuntime items;
    private ISettlementAlertService alerts;
    private SettlementAlertRuntime alertRuntime;
    private CharacterAlarmResponseRuntime alarmRuntime;
    private IGameCalendar calendar;
    private float originalTimeScale;
    private int originalDay;
    private int originalHour;

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Artifacts/QA");
        originalTimeScale = Time.timeScale;
        Application.logMessageReceived += CaptureIssue;
        try
        {
            yield return ResolveWorld();
            if (failures.Count == 0)
            {
                Time.timeScale = 8f;
                yield return VerifyInlineTimedWorkSaveAndResume();
                if (failures.Count == 0)
                    Check(CreateLongConstructionFixture(out string fixtureFailure),
                        "LONG_WORK_FIXTURE", fixtureFailure);
                if (failures.Count == 0)
                    yield return VerifyAlertSuspensionAndReturn();
                if (failures.Count == 0)
                    yield return VerifyDestroyedSuspendedTargetAbandonsCleanly();
            }
        }
        finally
        {
            Cleanup();
            Application.logMessageReceived -= CaptureIssue;
            Time.timeScale = originalTimeScale;
            WriteReport();
            Destroy(gameObject);
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlaying)
                    EditorApplication.isPlaying = false;
            };
        }
    }

    private IEnumerator ResolveWorld()
    {
        bool attemptedPreparation = false;
        float deadline = Time.realtimeSinceStartup + 15f;
        while (Time.realtimeSinceStartup < deadline)
        {
            scope = UnityEngine.Object.FindFirstObjectByType<
                DungeonRuntimeLifetimeScope>(FindObjectsInactive.Include);
            // CharacterAlarmResponseRuntime selects eligible responders by
            // persistent id. Use the same deterministic authority so this
            // scenario exercises the worker that the real responder planner
            // will interrupt instead of whichever actor Unity enumerates first.
            worker = LiveWorkers()
                .Where(candidate =>
                    candidate?.Brain?.availableActions?.Any(action =>
                        action?.actionset is AIWork) == true
                    && candidate.Stats?.EvaluatePerformance(
                        CharacterPerformanceFormulaIds.AlarmResponse)
                        ?.IsApplicable == true)
                .OrderBy(candidate => candidate.Identity.PersistentId,
                    StringComparer.Ordinal)
                .FirstOrDefault();
            if (scope?.Container != null && worker != null) break;
            if (!attemptedPreparation && scope?.Container != null)
            {
                attemptedPreparation = true;
                checks.Add("SETUP\tINFO\t"
                    + StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug());
            }
            yield return null;
        }

        Check(scope?.Container != null, "LIVE_SCOPE", scope?.name ?? "missing");
        Check(worker != null, "LIVE_WORKER", worker?.name ?? "missing");
        if (scope?.Container == null || worker == null) yield break;

        orders = scope.Container.Resolve<IWorkOrderRuntime>();
        items = scope.Container.Resolve<IWorldItemStackRuntime>();
        alerts = scope.Container.Resolve<ISettlementAlertService>();
        alertRuntime = scope.Container.Resolve<SettlementAlertRuntime>();
        alarmRuntime = scope.Container.Resolve<CharacterAlarmResponseRuntime>();
        calendar = scope.Container.Resolve<IGameCalendar>();
        grid = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>(
            FindObjectsInactive.Include)?.grid;
        Check(orders != null && items != null && alerts != null
                && alertRuntime != null && alarmRuntime != null && calendar != null,
            "ALERT_AUTHORITIES", "resolved");
        Check(grid != null, "LIVE_GRID", grid == null ? "missing" : "resolved");
        if (grid == null) yield break;

        originalDay = calendar.Day;
        originalHour = calendar.Hour;
        work = worker.GetComponent<AbilityWork>();
        brain = worker.Brain;
        originalActions = brain.availableActions;
        workAction = originalActions.First(action => action?.actionset is AIWork);
        foreach (KeyValuePair<CharacterCondition, float> pair
                 in worker.Stats.StatSnapshot)
            workerStats[pair.Key] = pair.Value;

        PauseUnrelatedAi();
        worker.SetAiPaused(true);
        brain.enabled = true;
        if (worker.BehaviorTree != null) worker.BehaviorTree.enabled = true;
        Neutralize(worker);
        work.SetDutyState(AbilityWork.DutyState.OnDuty);
        work.WorkPriorities.SetPriority(
            BuiltInWorkTypeIds.Construct,
            WorkPriorityLevel.Priority1);

    }

    private IEnumerator VerifyAlertSuspensionAndReturn()
    {
        bool priorityAccepted = work.TrySetPriorityWorkTarget(
            site,
            BuiltInWorkTypeIds.Construct,
            grid.SearchPath(worker.GetNowXY()),
            out string priorityFailure);
        Check(priorityAccepted, "WORK_PRIORITY_ASSIGNED",
            priorityAccepted ? orderId : priorityFailure);
        if (!priorityAccepted) yield break;

        brain.StopCurrentActionForReplan("alert-verifier-ready");
        brain.availableActions = new[] { workAction };
        brain.PreferWorkActionOnNextDecision(
            BuiltInWorkTypeIds.Construct,
            120f);
        worker.SetAiPaused(false);
        brain.RequestImmediateReplan(clearFailures: true);

        float startDeadline = Time.realtimeSinceStartup + 20f;
        long progressBefore = work.ApprovedWorkProgressRevisionForDiagnostics;
        while (Time.realtimeSinceStartup < startDeadline
            && (!work.isWorking
                || work.ApprovedWorkProgressRevisionForDiagnostics
                    <= progressBefore))
        {
            Neutralize(worker);
            yield return null;
        }
        Check(work.isWorking
                && work.AssignedWorkTypeId == BuiltInWorkTypeIds.Construct,
            "LIVE_WORK_STARTED",
            $"working={work.isWorking}; type={work.AssignedWorkTypeId}; "
            + $"target={work.assignedShop?.name}; progress="
            + $"{progressBefore}->{work.ApprovedWorkProgressRevisionForDiagnostics}");
        if (!work.isWorking) yield break;

        CharacterAiRuntimeGateSnapshot gateBeforeAlert =
            brain.CaptureRuntimeGateSnapshot();

        WorkOrderProgressState beforeAlert = default;
        bool beforeAvailable = orders.TryGetOrderFor(
            site,
            BuiltInWorkTypeIds.Construct,
            out beforeAlert);
        EmergencyAccountingResult published = alerts.PublishIncidentSignal(
            new SettlementIncidentSignal(
                IncidentId,
                SettlementThreatAlertLevel.Red,
                alerts.GetNextIncidentRevision(IncidentId),
                "qa",
                "live work suspension"));
        Check(published.Success
                && alerts.Capture().CommittedLevel
                    == SettlementThreatAlertLevel.Red,
            "RED_ESCALATION_IMMEDIATE",
            $"{published.Code}; level={alerts.Capture().CommittedLevel}");

        float suspendedDeadline = Time.realtimeSinceStartup + 15f;
        SettlementSuspendedWorkSnapshot suspended = default;
        string workerId = worker.Identity.PersistentId;
        while (Time.realtimeSinceStartup < suspendedDeadline
            && (!alerts.TryGetSuspendedWork(workerId, out suspended)
                || work.isWorking
                || work.HasActiveWorkRoutineForDiagnostics))
        {
            Neutralize(worker);
            alarmRuntime.Tick();
            yield return null;
        }
        WorkOrderProgressState atSuspension = default;
        bool suspendedOrderAvailable = orders.TryGetOrderFor(
            site,
            BuiltInWorkTypeIds.Construct,
            out atSuspension);
        Check(suspended.WorkTypeId == BuiltInWorkTypeIds.Construct
                && string.Equals(
                    suspended.TargetBuildingId,
                    site.PersistentInstanceId.Value,
                    StringComparison.Ordinal)
                && !work.isWorking
                && !work.HasActiveWorkRoutineForDiagnostics,
            "WORK_SUSPENDED_AT_CHECKPOINT",
            $"journal={suspended.WorkTypeId}/{suspended.TargetBuildingId}; "
            + $"working={work.isWorking}; routine="
            + $"{work.HasActiveWorkRoutineForDiagnostics}; phase={brain.CurrentActionPhase}");
        Check(beforeAvailable && suspendedOrderAvailable
                && atSuspension.CompletedWork + 0.001f
                    >= beforeAlert.CompletedWork
                && atSuspension.CompletedWork + 0.001f
                    < atSuspension.RequiredWork,
            "PERSISTENT_PROGRESS_PRESERVED",
            $"before={beforeAlert.CompletedWork:0.###}; "
            + $"suspended={atSuspension.CompletedWork:0.###}/"
            + $"{atSuspension.RequiredWork:0.###}");

        EmergencyAccountingResult resolved = alerts.ResolveIncident(
            IncidentId,
            alerts.GetNextIncidentRevision(IncidentId));
        Check(resolved.Success, "INCIDENT_RESOLVED", resolved.Code);
        long epoch = alerts.Capture().AlertEpochId;
        int baseDay = calendar.Day;
        int baseHour = calendar.Hour;
        SetCalendarOffset(baseDay, baseHour, 1);
        alertRuntime.Tick();
        alarmRuntime.Tick();
        Check(alerts.Capture().CommittedLevel == SettlementThreatAlertLevel.Red,
            "RED_HYSTERESIS_ONE_HOUR",
            $"level={alerts.Capture().CommittedLevel}");
        SetCalendarOffset(baseDay, baseHour, 2);
        alertRuntime.Tick();
        alarmRuntime.Tick();
        Check(alerts.Capture().CommittedLevel == SettlementThreatAlertLevel.Amber,
            "RED_TO_AMBER_AFTER_TWO_HOURS",
            $"level={alerts.Capture().CommittedLevel}");
        Check(!work.isWorking
                && work.HasEmergencyResponseWorkGateForDiagnostics
                && work.EmergencyResponseWorkEpochForDiagnostics == epoch,
            "NO_PREMATURE_RETURN_AT_AMBER",
            $"working={work.isWorking}; epoch={epoch}; gate="
            + $"{work.EmergencyResponseWorkEpochForDiagnostics}/"
            + work.EmergencyResponseOnlyWorkTypeForDiagnostics);

        long gateRevisionBeforeReEscalation =
            work.EmergencyResponseWorkGateRevisionForDiagnostics;
        EmergencyAccountingResult reEscalated = alerts.PublishIncidentSignal(
            new SettlementIncidentSignal(
                InvasionIncidentId,
                SettlementThreatAlertLevel.Red,
                alerts.GetNextIncidentRevision(InvasionIncidentId),
                "qa",
                "re-escalate while suspended return is pending"));
        SettlementAlertSnapshot reEscalatedAlert = alerts.Capture();
        bool migratedJournalAvailable = alerts.TryGetSuspendedWork(
            workerId,
            out SettlementSuspendedWorkSnapshot migratedJournal);
        Check(reEscalated.Success
                && reEscalatedAlert.CommittedLevel
                    == SettlementThreatAlertLevel.Red
                && reEscalatedAlert.AlertEpochId == epoch + 1L
                && migratedJournalAvailable
                && migratedJournal.AlertEpochId
                    == reEscalatedAlert.AlertEpochId
                && work.HasEmergencyResponseWorkGateForDiagnostics
                && work.EmergencyResponseWorkEpochForDiagnostics
                    == reEscalatedAlert.AlertEpochId
                && work.EmergencyResponseOnlyWorkTypeForDiagnostics
                    == BuiltInWorkTypeIds.Guard
                && work.EmergencyResponseWorkGateRevisionForDiagnostics
                    == gateRevisionBeforeReEscalation + 1L,
            "REESCALATION_CARRIES_RESPONDER_JOURNAL_AND_GATE",
            $"publish={reEscalated.Code}; level={reEscalatedAlert.CommittedLevel}; "
            + $"epoch={epoch}->{reEscalatedAlert.AlertEpochId}; journal="
            + $"{migratedJournalAvailable}:{migratedJournal.AlertEpochId}; gate="
            + $"{work.EmergencyResponseWorkEpochForDiagnostics}/"
            + work.EmergencyResponseOnlyWorkTypeForDiagnostics
            + $"; revision={gateRevisionBeforeReEscalation}->"
            + work.EmergencyResponseWorkGateRevisionForDiagnostics
            + $"; assigned={alarmRuntime.AssignedResponderCountForDiagnostics}; "
            + $"pending={alarmRuntime.PendingResponderCountForDiagnostics}; "
            + $"returning={alarmRuntime.ReturningResponderCountForDiagnostics}");

        long gateRevisionBeforeRetarget =
            work.EmergencyResponseWorkGateRevisionForDiagnostics;
        EmergencyAccountingResult medicalAdded = alerts.PublishIncidentSignal(
            new SettlementIncidentSignal(
                MedicalIncidentId,
                SettlementThreatAlertLevel.Red,
                alerts.GetNextIncidentRevision(MedicalIncidentId),
                "qa",
                "same epoch response type handoff"));
        EmergencyAccountingResult invasionResolved = alerts.ResolveIncident(
            InvasionIncidentId,
            alerts.GetNextIncidentRevision(InvasionIncidentId));
        SettlementAlertSnapshot retargetedAlert = alerts.Capture();
        Check(medicalAdded.Success
                && invasionResolved.Success
                && retargetedAlert.CommittedLevel
                    == SettlementThreatAlertLevel.Red
                && retargetedAlert.AlertEpochId
                    == reEscalatedAlert.AlertEpochId
                && work.EmergencyResponseWorkEpochForDiagnostics
                    == retargetedAlert.AlertEpochId
                && work.EmergencyResponseOnlyWorkTypeForDiagnostics
                    == BuiltInWorkTypeIds.Rescue
                && work.EmergencyResponseWorkGateRevisionForDiagnostics
                    == gateRevisionBeforeRetarget + 1L,
            "SAME_EPOCH_RESPONSE_TYPE_RETARGET",
            $"medical={medicalAdded.Code}; invasion={invasionResolved.Code}; "
            + $"epoch={retargetedAlert.AlertEpochId}; gate="
            + $"{work.EmergencyResponseWorkEpochForDiagnostics}/"
            + work.EmergencyResponseOnlyWorkTypeForDiagnostics
            + $"; revision={gateRevisionBeforeRetarget}->"
            + work.EmergencyResponseWorkGateRevisionForDiagnostics);

        EmergencyAccountingResult medicalResolved = alerts.ResolveIncident(
            MedicalIncidentId,
            alerts.GetNextIncidentRevision(MedicalIncidentId));
        Check(medicalResolved.Success,
            "REESCALATED_INCIDENT_RESOLVED",
            medicalResolved.Code);
        epoch = retargetedAlert.AlertEpochId;
        int reEscalatedBaseDay = calendar.Day;
        int reEscalatedBaseHour = calendar.Hour;
        SetCalendarOffset(reEscalatedBaseDay, reEscalatedBaseHour, 1);
        alertRuntime.Tick();
        alarmRuntime.Tick();
        Check(alerts.Capture().CommittedLevel == SettlementThreatAlertLevel.Red,
            "REESCALATED_RED_HYSTERESIS_ONE_HOUR",
            $"level={alerts.Capture().CommittedLevel}");
        SetCalendarOffset(reEscalatedBaseDay, reEscalatedBaseHour, 2);
        alertRuntime.Tick();
        alarmRuntime.Tick();
        Check(alerts.Capture().CommittedLevel == SettlementThreatAlertLevel.Amber,
            "REESCALATED_RED_TO_AMBER_AFTER_TWO_HOURS",
            $"level={alerts.Capture().CommittedLevel}");
        SetCalendarOffset(reEscalatedBaseDay, reEscalatedBaseHour, 3);
        alertRuntime.Tick();
        alarmRuntime.Tick();
        Check(alerts.Capture().CommittedLevel == SettlementThreatAlertLevel.Amber,
            "AMBER_HYSTERESIS_ONE_HOUR",
            $"level={alerts.Capture().CommittedLevel}");
        SetCalendarOffset(reEscalatedBaseDay, reEscalatedBaseHour, 4);
        alertRuntime.Tick();
        Check(alerts.Capture().CommittedLevel == SettlementThreatAlertLevel.Green,
            "AMBER_TO_GREEN_AFTER_TWO_HOURS",
            $"level={alerts.Capture().CommittedLevel}");

        int queuedReturnsBeforeReEscalation =
            alarmRuntime.ReturningResponderCountForDiagnostics;
        long gateRevisionBeforeReturnReEscalation =
            work.EmergencyResponseWorkGateRevisionForDiagnostics;
        EmergencyAccountingResult returnReEscalated = alerts.PublishIncidentSignal(
            new SettlementIncidentSignal(
                InvasionIncidentId,
                SettlementThreatAlertLevel.Red,
                alerts.GetNextIncidentRevision(InvasionIncidentId),
                "qa",
                "re-escalate before queued Green return commits"));
        SettlementAlertSnapshot returnReEscalatedAlert = alerts.Capture();
        bool returnJournalAvailable = alerts.TryGetSuspendedWork(
            workerId,
            out SettlementSuspendedWorkSnapshot returnMigratedJournal);
        Check(returnReEscalated.Success
                && queuedReturnsBeforeReEscalation > 0
                && returnReEscalatedAlert.CommittedLevel
                    == SettlementThreatAlertLevel.Red
                && returnReEscalatedAlert.AlertEpochId == epoch + 1L
                && alarmRuntime.ReturningResponderCountForDiagnostics == 0
                && alarmRuntime.AssignedResponderCountForDiagnostics > 0
                && returnJournalAvailable
                && returnMigratedJournal.AlertEpochId
                    == returnReEscalatedAlert.AlertEpochId
                && work.EmergencyResponseWorkEpochForDiagnostics
                    == returnReEscalatedAlert.AlertEpochId
                && work.EmergencyResponseOnlyWorkTypeForDiagnostics
                    == BuiltInWorkTypeIds.Guard
                && work.EmergencyResponseWorkGateRevisionForDiagnostics
                    == gateRevisionBeforeReturnReEscalation + 1L,
            "GREEN_RETURN_QUEUE_REESCALATION_CARRIED_ONCE",
            $"publish={returnReEscalated.Code}; queued="
            + $"{queuedReturnsBeforeReEscalation}->"
            + alarmRuntime.ReturningResponderCountForDiagnostics
            + $"; assigned={alarmRuntime.AssignedResponderCountForDiagnostics}; "
            + $"epoch={epoch}->{returnReEscalatedAlert.AlertEpochId}; journal="
            + $"{returnJournalAvailable}:{returnMigratedJournal.AlertEpochId}; gate="
            + $"{work.EmergencyResponseWorkEpochForDiagnostics}/"
            + work.EmergencyResponseOnlyWorkTypeForDiagnostics
            + $"; revision={gateRevisionBeforeReturnReEscalation}->"
            + work.EmergencyResponseWorkGateRevisionForDiagnostics);

        EmergencyAccountingResult returnReEscalationResolved =
            alerts.ResolveIncident(
                InvasionIncidentId,
                alerts.GetNextIncidentRevision(InvasionIncidentId));
        Check(returnReEscalationResolved.Success,
            "RETURN_QUEUE_REESCALATION_RESOLVED",
            returnReEscalationResolved.Code);
        epoch = returnReEscalatedAlert.AlertEpochId;
        int finalBaseDay = calendar.Day;
        int finalBaseHour = calendar.Hour;
        for (int offset = 1; offset <= 4; offset++)
        {
            SetCalendarOffset(finalBaseDay, finalBaseHour, offset);
            alertRuntime.Tick();
            alarmRuntime.Tick();
            yield return null;
        }
        Check(alerts.Capture().CommittedLevel == SettlementThreatAlertLevel.Green,
            "RETURN_QUEUE_REESCALATION_RETURNED_GREEN",
            $"level={alerts.Capture().CommittedLevel}; epoch={epoch}");

        float returnDeadline = Time.realtimeSinceStartup + 20f;
        WorkOrderProgressState afterReturn = default;
        bool afterAvailable = false;
        bool originalReturnObserved = false;
        while (Time.realtimeSinceStartup < returnDeadline
            && !originalReturnObserved)
        {
            Neutralize(worker);
            alarmRuntime.Tick();
            yield return null;

            afterAvailable = orders.TryGetOrderFor(
                site,
                BuiltInWorkTypeIds.Construct,
                out afterReturn);
            originalReturnObserved =
                alerts.Capture().CommittedLevel
                    == SettlementThreatAlertLevel.Green
                && alarmRuntime.PendingResponderCountForDiagnostics == 0
                && alarmRuntime.ReturningResponderCountForDiagnostics == 0
                && alarmRuntime.AssignedResponderCountForDiagnostics == 0
                && CountLiveEmergencyWorkGates() == 0
                && !work.HasEmergencyResponseWorkGateForDiagnostics
                && !alerts.TryGetSuspendedWork(workerId, out _)
                && work.PriorityWorkTypeId == BuiltInWorkTypeIds.Construct
                && work.PriorityWorkTarget == site
                && work.isWorking
                && work.AssignedWorkTypeId == BuiltInWorkTypeIds.Construct
                && work.assignedShop == site
                && afterAvailable
                && afterReturn.CompletedWork > atSuspension.CompletedWork;
        }
        afterAvailable = orders.TryGetOrderFor(
            site,
            BuiltInWorkTypeIds.Construct,
            out afterReturn);
        bool hasPreferredWorkType = brain.TryGetPreferredWorkType(
            out WorkTypeId preferredWorkType);
        Check(work.isWorking
                && work.AssignedWorkTypeId == BuiltInWorkTypeIds.Construct
                && work.assignedShop == site
                && work.PriorityWorkTypeId == BuiltInWorkTypeIds.Construct
                && work.PriorityWorkTarget == site,
            "ORIGINAL_WORK_RETURNED",
            $"working={work.isWorking}; type={work.AssignedWorkTypeId}; "
            + $"target={work.assignedShop?.name}; priority="
            + $"{work.PriorityWorkTypeId}/{work.PriorityWorkTarget?.name}; "
            + $"preferred={hasPreferredWorkType}:{preferredWorkType}; "
            + $"action={brain.CurrentActionDebugLabel}; phase="
            + brain.CurrentActionPhase);
        Check(afterAvailable
                && afterReturn.CompletedWork > atSuspension.CompletedWork,
            "WORK_PROGRESS_CONTINUES_AFTER_RETURN",
            $"suspended={atSuspension.CompletedWork:0.###}; "
            + $"returned={afterReturn.CompletedWork:0.###}/"
            + $"{afterReturn.RequiredWork:0.###}");

        float returnedJournalDeadline = Time.realtimeSinceStartup + 5f;
        while (Time.realtimeSinceStartup < returnedJournalDeadline
            && alerts.TryGetSuspendedWork(workerId, out _))
        {
            Neutralize(worker);
            alarmRuntime.Tick();
            yield return null;
        }
        bool returnedJournalCleared =
            !alerts.TryGetSuspendedWork(workerId, out _);
        Check(returnedJournalCleared,
            "SUSPENSION_JOURNAL_CLEARED_ON_RETURN",
            $"epoch={epoch}; cleared={returnedJournalCleared}");

        float ownershipDrainDeadline = Time.realtimeSinceStartup + 5f;
        int liveEmergencyGates = CountLiveEmergencyWorkGates();
        while (Time.realtimeSinceStartup < ownershipDrainDeadline
            && (alarmRuntime.PendingResponderCountForDiagnostics > 0
                || alarmRuntime.ReturningResponderCountForDiagnostics > 0
                || alarmRuntime.AssignedResponderCountForDiagnostics > 0
                || liveEmergencyGates > 0))
        {
            Neutralize(worker);
            alarmRuntime.Tick();
            yield return null;
            liveEmergencyGates = CountLiveEmergencyWorkGates();
        }
        Check(alarmRuntime.PendingResponderCountForDiagnostics == 0
                && alarmRuntime.ReturningResponderCountForDiagnostics == 0
                && alarmRuntime.AssignedResponderCountForDiagnostics == 0
                && liveEmergencyGates == 0
                && !work.HasEmergencyResponseWorkGateForDiagnostics,
            "GREEN_RETURN_OWNERSHIP_CLEAN",
            $"pending={alarmRuntime.PendingResponderCountForDiagnostics}; "
            + $"returning={alarmRuntime.ReturningResponderCountForDiagnostics}; "
            + $"assigned={alarmRuntime.AssignedResponderCountForDiagnostics}; "
            + $"gates={liveEmergencyGates}; workerGate="
            + work.HasEmergencyResponseWorkGateForDiagnostics);

        long settledGateRevision =
            work.EmergencyResponseWorkGateRevisionForDiagnostics;
        long settledCancellationCount = work.ActiveWorkCancellationCountForDiagnostics;
        for (int index = 0; index < 8; index++)
        {
            Neutralize(worker);
            alarmRuntime.Tick();
            yield return null;
        }
        int gatesAfterIdempotenceTicks = CountLiveEmergencyWorkGates();
        Check(work.EmergencyResponseWorkGateRevisionForDiagnostics
                    == settledGateRevision
                && work.ActiveWorkCancellationCountForDiagnostics
                    == settledCancellationCount
                && alarmRuntime.PendingResponderCountForDiagnostics == 0
                && alarmRuntime.ReturningResponderCountForDiagnostics == 0
                && alarmRuntime.AssignedResponderCountForDiagnostics == 0
                && gatesAfterIdempotenceTicks == 0
                && !alerts.TryGetSuspendedWork(workerId, out _),
            "GREEN_FINAL_EIGHT_TICKS_IDEMPOTENT",
            $"gateRevision={settledGateRevision}->"
            + work.EmergencyResponseWorkGateRevisionForDiagnostics
            + $"; cancellations={settledCancellationCount}->"
            + work.ActiveWorkCancellationCountForDiagnostics
            + $"; pending={alarmRuntime.PendingResponderCountForDiagnostics}; "
            + $"returning={alarmRuntime.ReturningResponderCountForDiagnostics}; "
            + $"assigned={alarmRuntime.AssignedResponderCountForDiagnostics}; "
            + $"gates={gatesAfterIdempotenceTicks}");
        CharacterAiRuntimeGateSnapshot gate = brain.CaptureRuntimeGateSnapshot();
        Check(gate.InvariantAnomalies == gateBeforeAlert.InvariantAnomalies
                && gate.LivePathRequests == 0,
            "ALERT_AI_INVARIANTS",
            $"invariants={gateBeforeAlert.InvariantAnomalies}"
            + $"->{gate.InvariantAnomalies}; paths={gate.LivePathRequests}; "
            + $"reservations={gate.LiveReservations}; trace="
            + brain.CaptureRuntimeDiagnostics().FormatRecentTrace());
    }

    private IEnumerator VerifyDestroyedSuspendedTargetAbandonsCleanly()
    {
        string workerId = worker.Identity.PersistentId;
        float readyDeadline = Time.realtimeSinceStartup + 15f;
        while (Time.realtimeSinceStartup < readyDeadline
            && (!work.isWorking || work.assignedShop != site))
        {
            Neutralize(worker);
            alarmRuntime.Tick();
            yield return null;
        }
        Check(work.isWorking && work.assignedShop == site,
            "DESTROY_CASE_WORK_READY",
            $"working={work.isWorking}; target={work.assignedShop?.name}");
        if (!work.isWorking || site == null) yield break;

        CharacterAiRuntimeGateSnapshot gateBefore =
            brain.CaptureRuntimeGateSnapshot();
        EmergencyAccountingResult published = alerts.PublishIncidentSignal(
            new SettlementIncidentSignal(
                IncidentId,
                SettlementThreatAlertLevel.Red,
                alerts.GetNextIncidentRevision(IncidentId),
                "qa",
                "destroy suspended work target"));
        Check(published.Success,
            "DESTROY_CASE_RED_ESCALATION",
            $"{published.Code}; epoch={alerts.Capture().AlertEpochId}");

        SettlementSuspendedWorkSnapshot suspended = default;
        float suspendedDeadline = Time.realtimeSinceStartup + 15f;
        while (Time.realtimeSinceStartup < suspendedDeadline
            && (!alerts.TryGetSuspendedWork(workerId, out suspended)
                || work.isWorking
                || work.HasActiveWorkRoutineForDiagnostics))
        {
            Neutralize(worker);
            alarmRuntime.Tick();
            yield return null;
        }
        Check(suspended.TargetBuildingId == site.PersistentInstanceId.Value
                && !work.isWorking
                && !work.HasActiveWorkRoutineForDiagnostics,
            "DESTROY_CASE_SUSPENDED",
            $"journal={suspended.TargetBuildingId}; working={work.isWorking}; "
            + $"routine={work.HasActiveWorkRoutineForDiagnostics}");
        if (string.IsNullOrWhiteSpace(suspended.TargetBuildingId)) yield break;

        long epoch = alerts.Capture().AlertEpochId;
        ConstructionSite destroyedTarget = site;
        destroyedTarget.DestroySelf();
        yield return null;
        Check(destroyedTarget == null || destroyedTarget.isDestroy,
            "SUSPENDED_TARGET_DESTROYED",
            $"targetMissing={destroyedTarget == null}; destroyed={destroyedTarget?.isDestroy}");

        EmergencyAccountingResult resolved = alerts.ResolveIncident(
            IncidentId,
            alerts.GetNextIncidentRevision(IncidentId));
        Check(resolved.Success,
            "DESTROY_CASE_INCIDENT_RESOLVED",
            resolved.Code);
        int baseDay = calendar.Day;
        int baseHour = calendar.Hour;
        for (int offset = 1; offset <= 4; offset++)
        {
            SetCalendarOffset(baseDay, baseHour, offset);
            alertRuntime.Tick();
            alarmRuntime.Tick();
            yield return null;
        }
        Check(alerts.Capture().CommittedLevel == SettlementThreatAlertLevel.Green,
            "DESTROY_CASE_RETURNED_GREEN",
            $"level={alerts.Capture().CommittedLevel}; epoch={epoch}");

        for (int tick = 0; tick < 8; tick++)
        {
            alarmRuntime.Tick();
            yield return null;
        }
        Check(!alerts.TryGetSuspendedWork(workerId, out _),
            "DESTROYED_WORK_JOURNAL_ABANDONED",
            $"epoch={epoch}; priority={work.PriorityWorkTarget?.name}");
        Check(work.PriorityWorkTarget == null
                || work.PriorityWorkTarget != destroyedTarget,
            "DESTROYED_WORK_NOT_FALSELY_RESTORED",
            $"priority={work.PriorityWorkTarget?.name}; assigned={work.assignedShop?.name}");
        CharacterAiRuntimeGateSnapshot gateAfter =
            brain.CaptureRuntimeGateSnapshot();
        Check(gateAfter.InvariantAnomalies == gateBefore.InvariantAnomalies
                && gateAfter.LivePathRequests == 0,
            "DESTROY_CASE_AI_INVARIANTS",
            $"invariants={gateBefore.InvariantAnomalies}->{gateAfter.InvariantAnomalies}; "
            + $"paths={gateAfter.LivePathRequests}; reservations={gateAfter.LiveReservations}");
        site = null;
    }

    private IEnumerator VerifyInlineTimedWorkSaveAndResume()
    {
        worker.SetAiPaused(true);
        brain.StopCurrentActionForReplan("inline-progress-fixture-switch");
        work.ClearPriorityWorkTarget();
        float idleDeadline = Time.realtimeSinceStartup + 5f;
        while (Time.realtimeSinceStartup < idleDeadline
            && (work.isWorking || work.HasActiveWorkRoutineForDiagnostics))
        {
            Neutralize(worker);
            yield return null;
        }
        Check(!work.isWorking && !work.HasActiveWorkRoutineForDiagnostics,
            "INLINE_PREVIOUS_WORK_STOPPED",
            $"working={work.isWorking}; routine="
            + work.HasActiveWorkRoutineForDiagnostics);
        if (work.isWorking || work.HasActiveWorkRoutineForDiagnostics)
            yield break;
        Check(CreateInlineTimedWorkFixture(out string fixtureDetail),
            "INLINE_WORK_FIXTURE", fixtureDetail);
        if (genericWorkTarget == null) yield break;

        brain.StopCurrentActionForReplan("inline-progress-start");
        brain.RequestImmediateReplan(clearFailures: true);
        brain.availableActions = new[] { workAction };
        brain.enabled = false;
        if (worker.BehaviorTree != null) worker.BehaviorTree.enabled = false;
        work.SetWorkPriority(
            BuiltInWorkTypeIds.Clean,
            WorkPriorityLevel.Priority1);
        bool priorityAccepted = work.TrySetPriorityWorkTarget(
            genericWorkTarget,
            BuiltInWorkTypeIds.Clean,
            grid.SearchPath(worker.GetNowXY()),
            out string priorityFailure);
        Check(priorityAccepted, "INLINE_WORK_PRIORITY_ASSIGNED",
            priorityAccepted ? genericWorkTarget.ZoneId : priorityFailure);
        if (!priorityAccepted) yield break;
        brain.PreferWorkActionOnNextDecision(BuiltInWorkTypeIds.Clean, 120f);
        worker.SetAiPaused(false);

        float selectionDeadline = Time.realtimeSinceStartup + 20f;
        while (Time.realtimeSinceStartup < selectionDeadline
            && (brain.bestAction?.actionset is not AIWork
                || !brain.bestAction.HasStarted
                || !work.isWorking))
        {
            Neutralize(worker);
            yield return null;
        }

        bool selectedInlineAction = brain.bestAction?.actionset is AIWork
            && brain.bestAction.HasStarted
            && work.isWorking;
        Check(selectedInlineAction
                && brain.bestAction?.actionset is AIWork,
            "INLINE_WORK_ACTION_SELECTED",
            $"selected={selectedInlineAction}; action="
            + $"{brain.CurrentActionDebugLabel}; destination="
            + $"{brain.CurrentDestinationDebugLabel}; failure="
            + $"{brain.LastActionFailure}; schedulerStarted={selectedInlineAction}; "
            + $"priority={work.WorkPriorities?.GetPriority(BuiltInWorkTypeIds.Clean)}; "
            + $"priorityTarget={work.PriorityWorkTarget?.name}; "
            + $"assigned={work.AssignedWorkTypeId}/{work.assignedShop?.name}");
        if (!selectedInlineAction) yield break;
        Check(work.HasActiveWorkRoutineForDiagnostics,
            "INLINE_WORK_EXECUTION_STARTED",
            $"working={work.isWorking}; routine={work.HasActiveWorkRoutineForDiagnostics}; "
            + $"action={brain.CurrentActionDebugLabel}; phase={brain.CurrentActionPhase}");

        float progressDeadline = Time.realtimeSinceStartup + 20f;
        while (Time.realtimeSinceStartup < progressDeadline
            && (!work.HasActiveGenericProgressForDiagnostics
                || work.GenericCompletedWorkForDiagnostics < 2f))
        {
            Neutralize(worker);
            yield return null;
        }
        float beforeSuspend = work.GenericCompletedWorkForDiagnostics;
        float required = work.GenericRequiredWorkForDiagnostics;
        Check(work.HasActiveGenericProgressForDiagnostics
                && beforeSuspend >= 2f
                && beforeSuspend + 0.001f < required,
            "INLINE_WORK_PARTIAL_PROGRESS",
            $"completed={beforeSuspend:0.###}; required={required:0.###}; "
            + $"working={work.isWorking}; action={brain.CurrentActionDebugLabel}; "
            + $"phase={brain.CurrentActionPhase}; failure="
            + $"{brain.LastActionFailure.Kind}:{brain.LastActionFailure.Reason}; "
            + $"priority={work.PriorityWorkTarget?.name}; assigned="
            + $"{work.assignedShop?.name}; trace="
            + brain.CaptureRuntimeDiagnostics().FormatRecentTrace());
        if (!work.HasActiveGenericProgressForDiagnostics) yield break;

        EmergencyAccountingResult published = alerts.PublishIncidentSignal(
            new SettlementIncidentSignal(
                IncidentId,
                SettlementThreatAlertLevel.Red,
                alerts.GetNextIncidentRevision(IncidentId),
                "qa",
                "inline timed work save and resume"));
        Check(published.Success, "INLINE_WORK_RED_ESCALATION", published.Code);

        string workerId = worker.Identity.PersistentId;
        SettlementSuspendedWorkSnapshot suspended = default;
        float suspendedDeadline = Time.realtimeSinceStartup + 15f;
        while (Time.realtimeSinceStartup < suspendedDeadline
            && !alerts.TryGetSuspendedWork(workerId, out suspended))
        {
            Neutralize(worker);
            alarmRuntime.Tick();
            yield return null;
        }
        Check(suspended.HasInlineProgress
                && !suspended.ProgressExternallyPersisted
                && suspended.WorkTypeId == BuiltInWorkTypeIds.Clean
                && string.Equals(
                    suspended.TargetBuildingId,
                    genericWorkTarget.PersistentInstanceId.Value,
                    StringComparison.Ordinal)
                && suspended.InlineCompletedWork + 0.001f >= beforeSuspend
                && suspended.InlineCompletedWork < suspended.InlineRequiredWork,
            "INLINE_PROGRESS_CAPTURED",
            $"external={suspended.ProgressExternallyPersisted}; "
            + $"completed={suspended.InlineCompletedWork:0.###}/"
            + $"{suspended.InlineRequiredWork:0.###}; target="
            + suspended.TargetBuildingId);
        if (!suspended.HasInlineProgress) yield break;

        DungeonStory.Infrastructure.SettlementThreatAlertSaveData saved =
            alertRuntime.CaptureAlertSaveData();
        alertRuntime.RestoreAlertSaveData(saved);
        bool restoredAvailable = alerts.TryGetSuspendedWork(
            workerId,
            out SettlementSuspendedWorkSnapshot restored);
        Check(restoredAvailable
                && restored.HasInlineProgress
                && Mathf.Approximately(
                    restored.InlineCompletedWork,
                    suspended.InlineCompletedWork)
                && Mathf.Approximately(
                    restored.InlineRequiredWork,
                    suspended.InlineRequiredWork)
                && restored.AlertEpochId == suspended.AlertEpochId,
            "INLINE_PROGRESS_SAVE_ROUNDTRIP",
            $"available={restoredAvailable}; completed="
            + $"{restored.InlineCompletedWork:0.###}/"
            + $"{restored.InlineRequiredWork:0.###}; epoch="
            + $"{suspended.AlertEpochId}->{restored.AlertEpochId}");

        EmergencyAccountingResult resolved = alerts.ResolveIncident(
            IncidentId,
            alerts.GetNextIncidentRevision(IncidentId));
        Check(resolved.Success, "INLINE_WORK_INCIDENT_RESOLVED", resolved.Code);
        int baseDay = calendar.Day;
        int baseHour = calendar.Hour;
        for (int offset = 1; offset <= 4; offset++)
        {
            SetCalendarOffset(baseDay, baseHour, offset);
            alertRuntime.Tick();
            alarmRuntime.Tick();
            yield return null;
        }
        Check(alerts.Capture().CommittedLevel == SettlementThreatAlertLevel.Green,
            "INLINE_WORK_RETURNED_GREEN",
            $"level={alerts.Capture().CommittedLevel}");

        float resumeDeadline = Time.realtimeSinceStartup + 20f;
        while (Time.realtimeSinceStartup < resumeDeadline
            && (!work.HasActiveGenericProgressForDiagnostics
                || work.GenericCompletedWorkForDiagnostics
                    <= restored.InlineCompletedWork))
        {
            Neutralize(worker);
            alarmRuntime.Tick();
            yield return null;
        }
        float resumedProgress = work.GenericCompletedWorkForDiagnostics;
        Check(work.HasActiveGenericProgressForDiagnostics
                && resumedProgress + 0.001f
                    >= restored.InlineCompletedWork,
            "INLINE_PROGRESS_RESUMED_NOT_RESTARTED",
            $"saved={restored.InlineCompletedWork:0.###}; "
            + $"resumed={resumedProgress:0.###}/"
            + $"{work.GenericRequiredWorkForDiagnostics:0.###}; "
            + $"pending={work.HasPendingResumedGenericProgressForDiagnostics}");

        float completionDeadline = Time.realtimeSinceStartup + 20f;
        while (Time.realtimeSinceStartup < completionDeadline
            && genericWorkTarget != null
            && genericWorkTarget.CanRunExteriorCleanWork)
        {
            Neutralize(worker);
            yield return null;
        }

        float journalCleanupDeadline = Time.realtimeSinceStartup + 5f;
        while (Time.realtimeSinceStartup < journalCleanupDeadline
            && alerts.TryGetSuspendedWork(workerId, out _))
        {
            Neutralize(worker);
            alarmRuntime.Tick();
            yield return null;
        }
        bool suspendedJournalCleared =
            !alerts.TryGetSuspendedWork(workerId, out _);
        Check(genericWorkTarget != null
                && !genericWorkTarget.CanRunExteriorCleanWork
                && suspendedJournalCleared,
            "INLINE_WORK_COMPLETED_ONCE_AFTER_RESUME",
            $"cleanliness={genericWorkTarget?.Cleanliness:0.###}; "
            + $"working={work.isWorking}; priority={work.PriorityWorkTarget?.name}; "
            + $"suspendedJournalCleared={suspendedJournalCleared}");

        brain.StopCurrentActionForReplan("inline-progress-fixture-complete");
        work.ClearPriorityWorkTarget();
        RemoveInlineTimedWorkFixture();
        yield return null;

        work.SetWorkPriority(
            BuiltInWorkTypeIds.Construct,
            WorkPriorityLevel.Priority1);
        brain.enabled = true;
        if (worker.BehaviorTree != null) worker.BehaviorTree.enabled = true;
        worker.SetAiPaused(false);
    }

    private bool CreateInlineTimedWorkFixture(out string detail)
    {
        detail = string.Empty;
        Vector2Int workerCell = worker.GetNowXY();
        Vector2Int? positionCandidate = grid.GetCells()
            .Where(cell => cell != null
                && cell.AreaType == GridCellAreaType.DungeonInterior
                && cell.CanOccupy(GridLayer.Building)
                && grid.SearchPath(workerCell)?.GetMoveCostTo(cell.Position)
                    != int.MaxValue)
            .Select(cell => cell.Position)
            .OrderBy(cell => Mathf.Abs(cell.x - workerCell.x)
                + Mathf.Abs(cell.y - workerCell.y))
            .ThenBy(cell => cell.y)
            .ThenBy(cell => cell.x)
            .Select(cell => (Vector2Int?)cell)
            .FirstOrDefault();
        if (!positionCandidate.HasValue)
        {
            detail = "no reachable exterior-work cell";
            return false;
        }

        genericWorkDefinition = ScriptableObject.CreateInstance<BuildingSO>();
        genericWorkDefinition.id = 992002;
        genericWorkDefinition.objectName = "QA inline timed clean target";
        genericWorkDefinition.width = 1;
        genericWorkDefinition.height = 1;
        genericWorkDefinition.layer = GridLayer.Building;
        genericWorkDefinition.category = BuildingCategory.Shop;
        genericWorkDefinition.unlocked = true;
        genericWorkDefinition.ConfigureGameplayExecution(
            FacilityUseClassification.Logistics,
            ResearchFacilityCommandKind.None);
        genericWorkDefinition.Facility = new FacilityData
        {
            roles = FacilityRole.None,
            capacity = 0,
            useDuration = 1f,
            requiredWorkers = 1,
            disabledWhenDamaged = false
        };
        genericWorkDefinition.Facility.SetSupportedWorkTypeIds(new[]
        {
            BuiltInWorkTypeIds.Clean
        });
        genericWorkDefinition.AbilityModules.Add(
            new BuildingExteriorMaintenanceAbility
            {
                cleanWorkSeconds = 30f,
                cleanlinessGain = 100f
            });

        GameObject targetObject = new("QA_InlineTimedExteriorWork");
        genericWorkTarget = targetObject.AddComponent<ExteriorZoneMarker>();
        Inject(targetObject);
        genericWorkTarget.ConstructDebugRules(DisabledDungeonDebugRuleQuery.Instance);
        genericWorkTarget.InitializeRuntime(
            grid,
            positionCandidate.Value,
            ExteriorZoneType.DropZone,
            genericWorkDefinition);
        genericWorkTarget.ApplyExteriorWear(60f, 0f);
        detail = $"target={genericWorkTarget.PersistentInstanceId.Value}; "
            + $"position={positionCandidate.Value}; cleanliness="
            + $"{genericWorkTarget.Cleanliness:0.###}";
        return genericWorkTarget.CanRunExteriorCleanWork;
    }

    private void RemoveInlineTimedWorkFixture()
    {
        if (grid != null && genericWorkTarget != null
            && genericWorkDefinition != null)
        {
            grid.RemoveOccupant(
                genericWorkTarget,
                genericWorkDefinition.layer,
                genericWorkDefinition.GetGridPosList(genericWorkTarget.centerPos),
                false);
        }
        if (genericWorkTarget != null) Destroy(genericWorkTarget.gameObject);
        if (genericWorkDefinition != null) Destroy(genericWorkDefinition);
        genericWorkTarget = null;
        genericWorkDefinition = null;
    }

    private bool CreateLongConstructionFixture(out string detail)
    {
        detail = string.Empty;
        Vector2Int workerCell = worker.GetNowXY();
        Vector2Int? positionCandidate = grid.GetCells()
            .Where(cell => cell != null
                && cell.AreaType == GridCellAreaType.DungeonInterior
                && cell.CanOccupy(GridLayer.Construction)
                && grid.SearchPath(workerCell)?.GetMoveCostTo(cell.Position)
                    != int.MaxValue)
            .Select(cell => cell.Position)
            .OrderBy(cell => Mathf.Abs(cell.x - workerCell.x)
                + Mathf.Abs(cell.y - workerCell.y))
            .ThenBy(cell => cell.y)
            .ThenBy(cell => cell.x)
            .Select(cell => (Vector2Int?)cell)
            .FirstOrDefault();
        if (!positionCandidate.HasValue)
        {
            detail = "no reachable construction cell";
            return false;
        }
        Vector2Int position = positionCandidate.Value;

        siteDefinition = ScriptableObject.CreateInstance<BuildingSO>();
        siteDefinition.id = 992001;
        siteDefinition.objectName = "QA alert persistent construction";
        siteDefinition.width = 1;
        siteDefinition.height = 1;
        siteDefinition.layer = GridLayer.Building;
        siteDefinition.category = BuildingCategory.Shop;
        siteDefinition.unlocked = true;
        siteDefinition.ConfigureGameplayExecution(
            FacilityUseClassification.Logistics,
            ResearchFacilityCommandKind.None);
        BuildingWorkAmountAbility amount = new()
        {
            constructionWorkRequired = 100000f,
            repairWorkRequired = 8f,
            cleanWorkRequired = 4f,
            researchWorkRequired = 8f
        };
        amount.SetConstructionProjectScale(ProjectScale.IndustrialFacility);
        amount.SetConstructionMaterials(new[]
        {
            new ItemAmountDefinition("material:lumber", 1)
        });
        siteDefinition.AbilityModules.Add(amount);

        GameObject siteObject = new("QA_Alert_LongConstructionSite");
        site = siteObject.AddComponent<ConstructionSite>();
        Inject(siteObject);
        site.ConstructDebugRules(DisabledDungeonDebugRuleQuery.Instance);
        site.SetGrid(grid);
        site.Initialization(siteDefinition, position);
        siteObject.transform.position = grid.GetWorldPos(position);
        if (!grid.RegisterOccupant(
                site,
                GridLayer.Construction,
                siteDefinition.GetGridPosList(position),
                false))
        {
            detail = "construction-site registration failed";
            return false;
        }

        // The production balance calculator keys the long work amount from the
        // authored category. Width is expanded only while the order snapshots
        // that cost; the physical verifier site remains one cell.
        siteDefinition.width = 3;
        siteDefinition.height = 3;
        bool created;
        try
        {
            created = orders.TryCreateConstructionOrder(
                site,
                siteDefinition,
                position,
                out orderId,
                out detail);
        }
        finally
        {
            siteDefinition.width = 1;
            siteDefinition.height = 1;
        }
        if (!created) return false;
        site.ConfigureSite(orderId, () => true, () => { });
        if (!orders.TryGetOrderFor(
                site,
                BuiltInWorkTypeIds.Construct,
                out WorkOrderProgressState order)
            || !items.SpawnItemAt(
                "material:lumber",
                1,
                position,
                WorldItemStackState.FacilityBuffer,
                order.MaterialDestinationId,
                out int spawned)
            || spawned != 1
            || !orders.RefreshMaterialsReady(site))
        {
            detail = "construction material was not committed";
            return false;
        }

        detail = $"order={orderId}; position={position}; required="
            + $"{order.RequiredWork:0.###}";
        return true;
    }

    private void PauseUnrelatedAi()
    {
        foreach (CharacterActor candidate in LiveWorkers())
        {
            if (candidate == worker) continue;
            if (candidate.Brain != null)
            {
                pausedAi.Add(new MonoBehaviourState(
                    candidate.Brain,
                    candidate.Brain.enabled));
                candidate.Brain.enabled = false;
            }
            if (candidate.BehaviorTree != null)
            {
                pausedAi.Add(new MonoBehaviourState(
                    candidate.BehaviorTree,
                    candidate.BehaviorTree.enabled));
                candidate.BehaviorTree.enabled = false;
            }
        }
    }

    private void Cleanup()
    {
        worker?.SetAiPaused(true);
        brain?.StopCurrentActionForReplan("alert-verifier-cleanup");
        if (brain != null)
        {
            brain.availableActions = originalActions;
            brain.RequestImmediateReplan(clearFailures: true);
        }
        if (worker?.Stats != null) worker.Stats.Stats = workerStats;
        if (calendar != null)
            calendar.SetDateTime(originalDay, originalHour);
        if (alerts != null)
        {
            SettlementAlertSnapshot snapshot = alerts.Capture();
            if (snapshot.ActiveIncidentIds.Contains(IncidentId))
                alerts.ResolveIncident(
                    IncidentId,
                    alerts.GetNextIncidentRevision(IncidentId));
            snapshot = alerts.Capture();
            if (snapshot.ActiveIncidentIds.Contains(InvasionIncidentId))
                alerts.ResolveIncident(
                    InvasionIncidentId,
                    alerts.GetNextIncidentRevision(InvasionIncidentId));
            snapshot = alerts.Capture();
            if (snapshot.ActiveIncidentIds.Contains(MedicalIncidentId))
                alerts.ResolveIncident(
                    MedicalIncidentId,
                    alerts.GetNextIncidentRevision(MedicalIncidentId));
        }
        if (orders != null && !string.IsNullOrWhiteSpace(orderId))
            orders.CancelOrder(orderId, refundDeliveredMaterials: false);
        RemoveInlineTimedWorkFixture();
        if (grid != null && site != null && siteDefinition != null)
            grid.RemoveOccupant(
                site,
                GridLayer.Construction,
                siteDefinition.GetGridPosList(site.centerPos),
                false);
        if (site != null) Destroy(site.gameObject);
        if (siteDefinition != null) Destroy(siteDefinition);
        foreach (MonoBehaviourState state in pausedAi)
            if (state.Component != null)
                state.Component.enabled = state.WasEnabled;
        worker?.SetAiPaused(false);
    }

    private void SetCalendarOffset(int day, int hour, int offset)
    {
        int total = hour + offset;
        calendar.SetDateTime(day + total / 24, total % 24);
    }

    private void Inject(GameObject target)
    {
        foreach (MonoBehaviour component
                 in target.GetComponentsInChildren<MonoBehaviour>(true))
            if (component != null) scope.Container.Inject(component);
    }

    private static void Neutralize(CharacterActor target)
    {
        if (target?.Stats == null) return;
        foreach (CharacterCondition condition
                 in target.Stats.StatSnapshot.Keys.ToArray())
            target.Stats.Stats[condition] = 100f;
    }

    private static int CountLiveEmergencyWorkGates()
    {
        int count = 0;
        CharacterActor[] actors = LiveWorkers();
        for (int index = 0; index < actors.Length; index++)
        {
            AbilityWork actorWork = actors[index].GetComponent<AbilityWork>();
            if (actorWork?.HasEmergencyResponseWorkGateForDiagnostics == true)
            {
                count++;
            }
        }
        return count;
    }

    private static CharacterActor[] LiveWorkers() => UnityEngine.Object
        .FindObjectsByType<CharacterActor>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None)
        .Select(CharacterActorCollection.GetCanonical)
        .Where(candidate => candidate != null
            && !candidate.IsDead
            && candidate.characterType is not CharacterType.Customer
                and not CharacterType.Intruder
            && candidate.CurrentLifecycleState == CharacterLifecycleState.Active)
        .Distinct()
        .ToArray();

    private void CaptureIssue(string condition, string stack, LogType type)
    {
        if (type is LogType.Error or LogType.Exception or LogType.Assert
            || type == LogType.Warning)
            consoleIssues.Add($"{type}:{condition}");
    }

    private void Check(bool passed, string id, string detail)
    {
        checks.Add($"{id}\t{(passed ? "PASS" : "FAIL")}\t{detail}");
        if (!passed) failures.Add($"{id}: {detail}");
    }

    private void WriteReport()
    {
        Check(consoleIssues.Count == 0, "CONSOLE_WARNING_ERROR_ZERO",
            string.Join(" | ", consoleIssues));
        List<string> lines = new()
        {
            "CHARACTER_ALARM_RESPONSE_PLAYMODE",
            $"checks={checks.Count}; failures={failures.Count}; "
                + $"consoleIssues={consoleIssues.Count}",
            "case\tresult\tdetail"
        };
        lines.AddRange(checks);
        lines.Add($"RESULT={(failures.Count == 0 ? "PASS" : "FAIL")}; "
            + $"failures={failures.Count}");
        if (failures.Count > 0)
        {
            lines.Add("FAILURES");
            lines.AddRange(failures);
        }
        File.WriteAllLines(CharacterAlarmResponsePlayModeVerifier.ReportPath, lines);
        Debug.Log("CHARACTER_ALARM_RESPONSE="
            + $"{(failures.Count == 0 ? "PASS" : "FAIL")}; "
            + $"failures={failures.Count}");
    }

    private readonly struct MonoBehaviourState
    {
        public MonoBehaviourState(MonoBehaviour component, bool wasEnabled)
        {
            Component = component;
            WasEnabled = wasEnabled;
        }

        public MonoBehaviour Component { get; }
        public bool WasEnabled { get; }
    }
}
#endif
