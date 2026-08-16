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
/// End-to-end proof that an ordinary AI decision, rather than a player combat
/// command, owns rescue, stabilization, carrying and treatment.
/// </summary>
public static class CharacterAiAutonomousMedicalPlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/character-ai-autonomous-medical-playmode.txt";
    private const string PendingFlagPath =
        "Temp/character-ai-autonomous-medical-playmode.flag";

    [MenuItem("DungeonStory/Debug/QA/Run Character AI Autonomous Medical PlayMode Verification")]
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
                CharacterAiAutonomousMedicalPlayModeRunner>() != null)
            return;

        new GameObject("Character AI Autonomous Medical PlayMode Runner")
            .AddComponent<CharacterAiAutonomousMedicalPlayModeRunner>();
    }
}

public sealed class CharacterAiAutonomousMedicalPlayModeRunner : MonoBehaviour
{
    private const float SetupTimeoutRealtime = 15f;
    private const float ScenarioTimeoutRealtime = 75f;

    private readonly List<string> checks = new();
    private readonly List<string> failures = new();
    private readonly List<string> consoleIssues = new();
    private readonly List<MonoBehaviourState> pausedAi = new();
    private readonly Dictionary<CharacterCondition, float> rescuerStats = new();

    private CharacterActor rescuer;
    private CharacterActor patient;
    private AIBrain brain;
    private AIAction[] originalActions;
    private CharacterBodyHealthRuntime bodyHealth;
    private ICharacterMedicalQuery medicalQuery;
    private ICharacterMedicalCommand medicalCommands;
    private IWorldItemStackRuntime items;
    private IResourceEconomyContentCatalog resources;
    private CharacterBodyHealthSnapshot originalPatientBody;
    private float originalTimeScale;

    private IEnumerator Start()
    {
        Directory.CreateDirectory("Artifacts/QA");
        originalTimeScale = Time.timeScale;
        Time.timeScale = 6f;
        Application.logMessageReceived += CaptureIssue;
        try
        {
            yield return ResolveWorld();
            if (failures.Count == 0)
            {
                Time.timeScale = 12f;
                yield return VerifyAutonomousRescueAndTreatment();
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
        DungeonRuntimeLifetimeScope scope = null;
        CharacterActor[] workers = Array.Empty<CharacterActor>();
        bool attemptedPreparation = false;
        float deadline = Time.realtimeSinceStartup + SetupTimeoutRealtime;
        while (Time.realtimeSinceStartup < deadline)
        {
            scope = UnityEngine.Object.FindFirstObjectByType<
                DungeonRuntimeLifetimeScope>(FindObjectsInactive.Include);
            workers = LiveWorkers();
            if (scope?.Container != null && workers.Length >= 2) break;
            if (!attemptedPreparation && scope?.Container != null && workers.Length < 2)
            {
                attemptedPreparation = true;
                checks.Add("SETUP\tINFO\t"
                    + StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug());
            }
            yield return null;
        }

        Check(scope?.Container != null, "LIVE_SCOPE", scope?.name ?? "missing");
        Check(workers.Length >= 2, "LIVE_MEDICAL_ACTORS", $"workers={workers.Length}");
        if (scope?.Container == null || workers.Length < 2) yield break;

        bodyHealth = scope.Container.Resolve<CharacterBodyHealthRuntime>();
        medicalQuery = scope.Container.Resolve<ICharacterMedicalQuery>();
        medicalCommands = scope.Container.Resolve<ICharacterMedicalCommand>();
        items = scope.Container.Resolve<IWorldItemStackRuntime>();
        resources = scope.Container.Resolve<IResourceEconomyContentCatalog>();

        CharacterActor[] eligible = workers
            .Where(candidate => candidate.Brain?.availableActions?.Any(action =>
                action?.actionset is AIRescue) == true)
            .OrderBy(candidate => candidate.Identity?.PersistentId, StringComparer.Ordinal)
            .ToArray();
        if (eligible.Length >= 2)
        {
            rescuer = eligible[0];
            patient = eligible[1];
        }
        Check(rescuer != null && patient != null, "AUTHORED_RESCUE_ACTION",
            $"eligible={eligible.Length}");
        if (rescuer == null || patient == null) yield break;

        brain = rescuer.Brain;
        originalActions = brain.availableActions;
        originalPatientBody = bodyHealth.GetSnapshot(patient);
        foreach (KeyValuePair<CharacterCondition, float> pair in rescuer.Stats.StatSnapshot)
            rescuerStats[pair.Key] = pair.Value;

        PauseUnrelatedAi();
        PositionNearTreatmentFacility();
        Neutralize(rescuer);
        Neutralize(patient);
        // Keep the scheduler from reacting to the patient's Downed event until
        // the order, bed and physical medicine fixture are all committed.
        rescuer.SetAiPaused(true);
        brain.enabled = true;
        if (rescuer.BehaviorTree != null) rescuer.BehaviorTree.enabled = true;
        if (rescuer.TryGetAbility(out AbilityWork work))
        {
            work.SetDutyState(AbilityWork.DutyState.OnDuty);
            work.WorkPriorities.SetPriority(
                BuiltInWorkTypeIds.Rescue,
                WorkPriorityLevel.Priority1);
        }
        Check(bodyHealth != null && medicalQuery != null && medicalCommands != null,
            "MEDICAL_RUNTIME", "resolved");
    }

    private IEnumerator VerifyAutonomousRescueAndTreatment()
    {
        AIAction rescueAction = originalActions.First(action => action?.actionset is AIRescue);
        CharacterBodyHealthSnapshot before = bodyHealth.GetSnapshot(patient);
        List<CharacterBodyPartHealthState> injured = before.Parts.Select(ClonePart).ToList();
        foreach (CharacterBodyPartHealthState part in injured)
        {
            if (part.bodyPart is CombatBodyPart.LeftLeg or CombatBodyPart.RightLeg)
                part.currentHealth = Mathf.Max(0.5f, part.maxHealth * 0.18f);
            else if (part.bodyPart == CombatBodyPart.LeftArm)
            {
                part.currentHealth = Mathf.Max(1f, part.maxHealth * 0.55f);
                part.bleedingPerSecond = 0.01f;
            }
        }
        bodyHealth.ApplySnapshot(
            patient,
            new CharacterBodyHealthSnapshot(injured, 5f, 0f, 1f, 1f, 0.08f, true),
            "autonomous-medical-verifier");
        yield return null;
        yield return null;

        CharacterMedicalOrder order = medicalQuery.ActiveOrders.FirstOrDefault(candidate =>
            candidate != null
            && candidate.IsActive
            && string.Equals(candidate.patientId, PatientId, StringComparison.Ordinal));
        Check(patient.CurrentLifecycleState == CharacterLifecycleState.Downed,
            "PATIENT_DOWNED", $"state={patient.CurrentLifecycleState}");
        Check(order != null, "AUTOMATIC_MEDICAL_ORDER", order?.orderId ?? "missing");
        if (order == null) yield break;

        int medicine = SeedTreatmentSupply(order);
        Check(medicine > 0, "PHYSICAL_MEDICINE_SUPPLY", $"seeded={medicine}");

        brain.StopCurrentActionForReplan("autonomous-medical-setup");
        brain.availableActions = new[] { rescueAction };
        brain.PreferActionOnNextDecision<AIRescue>(300f);
        CharacterAiRuntimeGateSnapshot gateBefore = brain.CaptureRuntimeGateSnapshot();
        rescuer.SetAiPaused(false);
        brain.RequestImmediateReplan(clearFailures: true);

        bool selected = false;
        bool abilityRan = false;
        bool stabilizing = false;
        bool carrying = false;
        bool physicallyCarried = false;
        bool treating = false;
        string observedFailure = string.Empty;
        string observedFailureTrace = string.Empty;
        string lastOrder = string.Empty;
        float deadline = Time.realtimeSinceStartup + ScenarioTimeoutRealtime;
        while (Time.realtimeSinceStartup < deadline)
        {
            Neutralize(rescuer);
            selected |= brain.bestAction?.actionset is AIRescue;
            abilityRan |= rescuer.GetComponent<AbilityRescue>()?.IsRescuing == true;
            CharacterAiRuntimeGateSnapshot liveGate = brain.CaptureRuntimeGateSnapshot();
            if (string.IsNullOrEmpty(observedFailure)
                && liveGate.ActionFailed > gateBefore.ActionFailed)
            {
                CharacterAiRuntimeDiagnosticsSnapshot diagnostics =
                    brain.CaptureRuntimeDiagnostics();
                observedFailure = brain.LastActionFailure.ToString();
                observedFailureTrace = diagnostics.FormatRecentTrace();
            }
            if (medicalQuery.TryGetOrder(order.orderId, out CharacterMedicalOrder current))
            {
                lastOrder = $"{current.state}/{current.statusCode}; "
                    + $"stab={current.completedStabilizationWork:0.##}/"
                    + $"{current.requiredStabilizationWork:0.##}; "
                    + $"treat={current.completedTreatmentWork:0.##}/"
                    + $"{current.requiredTreatmentWork:0.##}";
                stabilizing |= current.state == CharacterMedicalOrderState.Stabilizing
                    || current.stabilized;
                carrying |= current.state == CharacterMedicalOrderState.Carrying
                    || current.carried;
                physicallyCarried |= current.carried
                    && patient.transform.IsChildOf(rescuer.transform);
                treating |= current.state is CharacterMedicalOrderState.Treating
                    or CharacterMedicalOrderState.Recovering
                    or CharacterMedicalOrderState.Completed;
            }
            if (patient.CurrentLifecycleState == CharacterLifecycleState.Active) break;
            yield return null;
        }
        yield return null;
        yield return null;

        CharacterBodyHealthSnapshot afterBody = bodyHealth.GetSnapshot(patient);
        CharacterAiRuntimeGateSnapshot gateAfter = brain.CaptureRuntimeGateSnapshot();
        Check(selected, "RESCUE_BT_SELECTED",
            $"selected={selected}; phase={brain.CurrentActionPhase}; failure={brain.LastActionFailure}");
        Check(abilityRan, "RESCUE_ABILITY_RAN", $"observed={abilityRan}");
        Check(stabilizing, "AUTONOMOUS_STABILIZATION", lastOrder);
        Check(carrying && physicallyCarried, "AUTONOMOUS_PHYSICAL_CARRY",
            $"carrying={carrying}; parented={physicallyCarried}; {lastOrder}");
        Check(treating, "AUTONOMOUS_BED_TREATMENT", lastOrder);
        Check(patient.CurrentLifecycleState == CharacterLifecycleState.Active
                && !afterBody.Downed,
            "AUTONOMOUS_RECOVERY",
            $"state={patient.CurrentLifecycleState}; downed={afterBody.Downed}; "
            + $"consciousness={afterBody.Consciousness:0.##}; mobility={afterBody.Mobility:0.##}");
        Check(gateAfter.ActionStarts >= gateBefore.ActionStarts + 1,
            "RESCUE_ACTION_STARTED",
            $"starts={gateBefore.ActionStarts}->{gateAfter.ActionStarts}");
        Check(gateAfter.ActionCompleted >= gateBefore.ActionCompleted + 1
                && gateAfter.ActionFailed == gateBefore.ActionFailed,
            "RESCUE_TYPED_COMPLETION",
            $"completed={gateBefore.ActionCompleted}->{gateAfter.ActionCompleted}; "
            + $"failed={gateBefore.ActionFailed}->{gateAfter.ActionFailed}; "
            + $"observedFailure={observedFailure}; trace={observedFailureTrace}");
        Check(gateAfter.LivePathRequests == 0 && gateAfter.LiveReservations == 0,
            "RESCUE_NO_RUNTIME_OWNERSHIP_LEAK",
            $"paths={gateAfter.LivePathRequests}; reservations={gateAfter.LiveReservations}");
        Check(gateAfter.InvariantAnomalies == gateBefore.InvariantAnomalies,
            "RESCUE_NO_INVARIANT_ANOMALY",
            $"invariants={gateBefore.InvariantAnomalies}->{gateAfter.InvariantAnomalies}");
    }

    private int SeedTreatmentSupply(CharacterMedicalOrder order)
    {
        if (order == null || items == null || resources == null) return 0;
        string destination = WorldItemStackRuntime.FacilityInputDestinationPrefix
            + $"medical:{order.orderId}";
        int total = 0;
        foreach (ResourceItemDefinitionSO medicine in resources.Items
                     .Where(item => item != null
                         && item.Kind == ResourceItemKind.Medicine
                         && item.SupportsInjuryTreatment)
                     .OrderBy(item => item.ItemId, StringComparer.Ordinal))
        {
            if (items.SpawnItemAt(
                    medicine.ItemId,
                    1,
                    order.BedPosition,
                    WorldItemStackState.FacilityBuffer,
                    destination,
                    out int spawned))
                total += spawned;
        }
        return total;
    }

    private void PositionNearTreatmentFacility()
    {
        BuildableObject facility = UnityEngine.Object.FindObjectsByType<BuildableObject>(
                FindObjectsInactive.Exclude,
                FindObjectsSortMode.None)
            .FirstOrDefault(building => building != null
                && !building.isDestroy
                && building.BuildingData?.GetAbility<BuildingMedicalAbility>() != null);
        GridSystemManager manager = UnityEngine.Object.FindFirstObjectByType<GridSystemManager>(
            FindObjectsInactive.Include);
        Grid grid = manager?.grid;
        Check(facility != null && grid != null, "MEDICAL_FACILITY",
            facility?.BuildingData?.name ?? "missing");
        if (facility == null || grid == null) return;

        HashSet<Vector2Int> buildingCells = facility.buildPoses.ToHashSet();
        List<Vector2Int> cells = grid.GetCells()
            .Where(cell => cell != null
                && cell.AreaType == GridCellAreaType.DungeonInterior
                && grid.IsWalkable(cell.Position)
                && !buildingCells.Contains(cell.Position))
            .Select(cell => cell.Position)
            .OrderBy(cell => Manhattan(cell, facility.centerPos))
            .ThenBy(cell => cell.y)
            .ThenBy(cell => cell.x)
            .Take(12)
            .ToList();
        if (cells.Count < 2) return;
        PositionActor(patient, grid, cells[0]);
        PositionActor(rescuer, grid, cells[1]);
    }

    private void PauseUnrelatedAi()
    {
        foreach (CharacterActor candidate in LiveWorkers())
        {
            if (candidate == rescuer) continue;
            if (candidate.Brain != null)
            {
                pausedAi.Add(new MonoBehaviourState(candidate.Brain, candidate.Brain.enabled));
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
        rescuer?.GetComponent<AbilityRescue>()?.StopRescue(
            CharacterMedicalStatusCode.ReservationReleased);
        if (brain != null)
        {
            brain.StopCurrentActionForReplan("autonomous-medical-cleanup");
            brain.availableActions = originalActions;
            brain.RequestImmediateReplan(clearFailures: true);
        }
        if (rescuer != null) rescuer.Stats.Stats = rescuerStats;
        rescuer?.SetAiPaused(false);
        if (patient != null && originalPatientBody.Parts != null)
            bodyHealth?.ApplySnapshot(patient, originalPatientBody, "verifier-cleanup");
        foreach (MonoBehaviourState state in pausedAi)
            if (state.Component != null) state.Component.enabled = state.WasEnabled;
    }

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
            "CHARACTER_AI_AUTONOMOUS_MEDICAL_PLAYMODE",
            $"checks={checks.Count}; failures={failures.Count}; consoleIssues={consoleIssues.Count}",
            "case\tresult\tdetail"
        };
        lines.AddRange(checks);
        lines.Add($"RESULT={(failures.Count == 0 ? "PASS" : "FAIL")}; failures={failures.Count}");
        if (failures.Count > 0)
        {
            lines.Add("FAILURES");
            lines.AddRange(failures);
        }
        File.WriteAllLines(CharacterAiAutonomousMedicalPlayModeVerifier.ReportPath, lines);
        Debug.Log($"CHARACTER_AI_AUTONOMOUS_MEDICAL="
            + $"{(failures.Count == 0 ? "PASS" : "FAIL")}; failures={failures.Count}");
    }

    private static CharacterBodyPartHealthState ClonePart(CharacterBodyPartHealthState part) =>
        new()
        {
            bodyPart = part.bodyPart,
            maxHealth = part.maxHealth,
            currentHealth = part.currentHealth,
            bleedingPerSecond = part.bleedingPerSecond
        };

    private static void Neutralize(CharacterActor target)
    {
        if (target?.Stats == null) return;
        foreach (CharacterCondition condition in target.Stats.StatSnapshot.Keys.ToArray())
            target.Stats.Stats[condition] = 100f;
    }

    private static void PositionActor(CharacterActor target, Grid grid, Vector2Int cell)
    {
        if (target == null || grid == null) return;
        target.GetComponent<AbilityMove>()?.CancelActiveMovement();
        target.Brain?.StopCurrentActionForReplan("autonomous-medical-position");
        Vector3 world = grid.GetWorldPos(cell);
        world.z = target.transform.position.z;
        target.transform.position = world;
        target.Brain?.ClearPathSearchCache();
    }

    private static int Manhattan(Vector2Int first, Vector2Int second) =>
        Mathf.Abs(first.x - second.x) + Mathf.Abs(first.y - second.y);

    private static CharacterActor[] LiveWorkers() => UnityEngine.Object
        .FindObjectsByType<CharacterActor>(FindObjectsInactive.Exclude, FindObjectsSortMode.None)
        .Select(CharacterActorCollection.GetCanonical)
        .Where(candidate => candidate != null
            && !candidate.IsDead
            && candidate.characterType is not CharacterType.Customer and not CharacterType.Intruder
            && candidate.CurrentLifecycleState == CharacterLifecycleState.Active)
        .Distinct()
        .ToArray();

    private string PatientId => patient?.Identity?.PersistentId ?? string.Empty;

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
