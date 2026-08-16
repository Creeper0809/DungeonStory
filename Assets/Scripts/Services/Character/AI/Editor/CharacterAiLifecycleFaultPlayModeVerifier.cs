using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using BehaviorDesigner.Runtime;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;

/// <summary>
/// Runs destructive lifecycle faults through the production decision pipeline.
/// Each row owns a real AIBrain action, AbilityMove coroutine and (when the
/// authored scene has an admissible facility) occupancy/queue/worker slots.
/// </summary>
public static class CharacterAiLifecycleFaultPlayModeVerifier
{
    public const string ReportPath =
        CharacterAiLifecycleFaultPlayModeRunner.ReportPath;

    public static void RunFromMenu() =>
        CharacterAiLifecycleFaultPlayModeRunner.RequestRun();

    public static void RequestRun() =>
        CharacterAiLifecycleFaultPlayModeRunner.RequestRun();
}

public sealed class CharacterAiLifecycleFaultPlayModeRunner : MonoBehaviour
{
    public const string ReportPath =
        "Artifacts/QA/character-ai-lifecycle-fault-playmode.txt";
    private const string PendingFlagPath =
        "Temp/character-ai-lifecycle-fault-playmode.flag";
    private enum FaultKind
    {
        Downed,
        Dead,
        Despawned,
        Disabled,
        Destroyed
    }

    private readonly List<UnityEngine.Object> temporaryObjects = new();
    private Coroutine pendingLateCommit;
    private int lateCommitGeneration;

    [MenuItem("DungeonStory/Debug/QA/Run AI Lifecycle Fault PlayMode Matrix")]
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

    public static CharacterAiLifecycleFaultPlayModeRunner AttachRunner(GameObject runner)
    {
        if (runner == null) throw new ArgumentNullException(nameof(runner));
        CharacterAiLifecycleFaultPlayModeRunner component =
            runner.AddComponent<CharacterAiLifecycleFaultPlayModeRunner>();
        if (component == null)
        {
            throw new InvalidOperationException(
                "Unity did not attach CharacterAiLifecycleFaultPlayModeRunner.");
        }
        return component;
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
        if (FindFirstObjectByType<CharacterAiLifecycleFaultPlayModeRunner>() != null)
        {
            Debug.LogWarning("AI lifecycle fault matrix is already running.");
            return;
        }

        GameObject runner = new(nameof(CharacterAiLifecycleFaultPlayModeRunner));
        runner.SetActive(false);
        CharacterAiLifecycleFaultPlayModeRunner component = AttachRunner(runner);
        runner.SetActive(true);
        component.BeginVerification();
    }

    public static string PublicEntrypoint =>
        "DungeonStory/Debug/QA/Run AI Lifecycle Fault PlayMode Matrix";

    private bool verificationStarted;

    private void Start() => BeginVerification();

    public void BeginVerification()
    {
        if (verificationStarted) return;
        verificationStarted = true;
        Directory.CreateDirectory("Artifacts/QA");
        File.WriteAllText(ReportPath,
            "# Character AI Lifecycle Fault PlayMode Matrix\nresult=RUNNING\n");
        StartCoroutine(RunVerification());
    }

    private IEnumerator RunVerification()
    {
        Directory.CreateDirectory("Artifacts/QA");
        Exception failure = null;
        yield return RunSafely(exception => failure = exception);
        string result = failure == null ? "PASS" : "FAIL";
        StringBuilder report = new(1024);
        report.AppendLine("# Character AI Lifecycle Fault PlayMode Matrix");
        report.AppendLine("rows=Downed,Dead,Despawned,Disabled,Destroyed");
        report.AppendLine("pipeline=CharacterAiDecisionPipeline+AIBrain+AbilityMove+item-lease+emergency-ledger");
        report.AppendLine("target-lifecycle=CharacterAiFaultRecoveryPlayModeVerifier:approach,queue,interaction");
        report.AppendLine("result=" + result);
        report.AppendLine(failure == null
            ? "action=5/5; movement=5/5; ownership=5/5; cleanup=exactly-once; second-cleanup=idempotent; late-commit=0"
            : failure.ToString());
        File.WriteAllText(ReportPath, report.ToString());
        if (failure == null)
        {
            Debug.Log("AI_LIFECYCLE_FAULT_MATRIX_PASS; rows=5; action=5/5; movement=5/5; "
                + "ownership=5/5; cleanup=exactly-once; second-cleanup=idempotent; late-commit=0");
        }
        else
        {
            Debug.LogError($"AI_LIFECYCLE_FAULT_MATRIX_FAIL: {failure}");
        }

        CleanupTemporaryObjects();
        Destroy(gameObject);
    }

    private IEnumerator RunSafely(Action<Exception> capture)
    {
        Stack<IEnumerator> routines = new();
        routines.Push(RunMatrix());
        while (routines.Count > 0)
        {
            object current;
            try
            {
                IEnumerator routine = routines.Peek();
                if (!routine.MoveNext())
                {
                    routines.Pop();
                    continue;
                }
                current = routine.Current;
            }
            catch (Exception exception)
            {
                capture(exception);
                yield break;
            }
            if (current is IEnumerator nested)
            {
                routines.Push(nested);
                continue;
            }
            yield return current;
        }
    }

    private IEnumerator RunMatrix()
    {
        DungeonRuntimeLifetimeScope scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
            FindObjectsInactive.Include);
        CharacterActor authored = FindAuthoredActor();
        float deadline = Time.realtimeSinceStartup + 10f;
        bool attemptedStartParty = false;
        while ((scope?.Container == null || authored == null)
            && Time.realtimeSinceStartup < deadline)
        {
            if (!attemptedStartParty && scope?.Container != null && authored == null)
            {
                attemptedStartParty = true;
                StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            }
            yield return null;
            scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include);
            authored = FindAuthoredActor();
        }
        Require(scope != null && scope.Container != null,
            "DungeonRuntimeLifetimeScope/container is unavailable.");
        Require(authored != null, "An authored live character is required as the profile source.");

        ICharacterAiWorldRegistry worldRegistry =
            scope.Container.Resolve<ICharacterAiWorldRegistry>();
        foreach (FaultKind fault in Enum.GetValues(typeof(FaultKind)))
        {
            yield return RunRow(scope, worldRegistry, authored.data, fault);
        }
    }

    private static CharacterActor FindAuthoredActor() =>
        CharacterActorCollection.DistinctByGameObject(
                FindObjectsByType<CharacterActor>(FindObjectsInactive.Exclude,
                    FindObjectsSortMode.None))
            .FirstOrDefault(actor => actor != null && actor.data != null && !actor.IsDead);

    private IEnumerator RunRow(
        DungeonRuntimeLifetimeScope scope,
        ICharacterAiWorldRegistry worldRegistry,
        CharacterSO sourceData,
        FaultKind fault)
    {
        CharacterActor actor = CreateActor(scope, sourceData, fault);
        AIBrain brain = actor.Brain;
        AbilityMove move = actor.GetComponent<AbilityMove>();
        LifecycleProbeAction actionSet = ScriptableObject.CreateInstance<LifecycleProbeAction>();
        actionSet.Owner = this;
        IWorldItemStackRuntime itemRuntime = scope.Container.Resolve<IWorldItemStackRuntime>();
        IItemQuantityReservationService reservations =
            scope.Container.Resolve<IItemQuantityReservationService>();
        IEmergencyWorkAccountingService emergencyAccounting =
            scope.Container.Resolve<IEmergencyWorkAccountingService>();
        WorldItemStackSnapshot leaseTarget = itemRuntime.GetAllStacks()
            .FirstOrDefault(stack => stack != null
                && stack.AvailableQuantity > 0
                && !stack.Forbidden
                && !string.IsNullOrWhiteSpace(stack.ReservationSignature));
        Require(leaseTarget != null, $"{fault}: no physical item is available for lease cleanup verification.");
        string probeOperationId = $"fault-verifier:{actor.Identity.PersistentId}:{fault}";
        actionSet.ConfigureOwnershipProbe(
            reservations,
            emergencyAccounting,
            leaseTarget,
            probeOperationId,
            actor.Identity.PersistentId);
        int ledgerBefore = emergencyAccounting.CaptureSnapshot().ActiveOperationCount;
        temporaryObjects.Add(actionSet);
        AIAction action = new(actionSet, AIActionPlan.WithoutDestination);
        brain.availableActions = new[] { action };
        brain.bestAction = action;
        brain.isBestActionEnd = false;
        brain.isExecuted = false;

        BuildableObject ownedBuilding = TryAcquireBuildingOwnership(actor, worldRegistry);
        Require(ownedBuilding != null,
            $"{fault}: no authored building accepted the occupancy/queue/worker ownership probe.");
        int usersBeforeFault = ownedBuilding != null ? ownedBuilding.CurrentUserCount : 0;
        int reservationsBeforeFault = ownedBuilding != null
            ? ownedBuilding.ActiveVisitReservationCount
            : 0;

        CharacterAiDecisionTickResult execution = new CharacterAiDecisionPipeline(
                NoCharacterDeprivationBoundary.Instance,
                NoCharacterDeprivationBoundary.Instance)
            .RunSelectedAction(actor, $"lifecycle-fault:{fault}");
        Require(execution.Handled, $"{fault}: production decision pipeline did not handle action.");
        Require(actionSet.ExecuteCount == 1 && brain.bestAction == action,
            $"{fault}: action did not enter the production brain lifecycle exactly once.");
        Require(move.HasActiveMovementRoutineForDiagnostics,
            $"{fault}: AbilityMove coroutine was not running before fault injection.");
        Require(emergencyAccounting.CaptureSnapshot().ActiveOperationCount == ledgerBefore + 1,
            $"{fault}: emergency work ownership was not registered.");
        Require(reservations.TryGetLeasesByOwner(probeOperationId, out IReadOnlyList<ItemQuantityLease> liveLeases)
            && liveLeases.Count == 1,
            $"{fault}: item quantity lease was not registered.");
        Require(worldRegistry.GetTransientBuildingOwnershipCount(
                    new CharacterId(actor.Identity.PersistentId)) == 1,
            $"{fault}: actor-owned building index did not contain exactly one target.");

        string actorId = actor.Identity.PersistentId;
        Vector3 positionAtFault = actor.transform.position;
        switch (fault)
        {
            case FaultKind.Downed:
                actor.SetLifecycleState(CharacterLifecycleState.Downed);
                break;
            case FaultKind.Dead:
                actor.Die(
                    CharacterDeathCauseCode.Combat,
                    "qa:ai-lifecycle-fault");
                break;
            case FaultKind.Despawned:
                actor.SetLifecycleState(CharacterLifecycleState.Despawned);
                break;
            case FaultKind.Disabled:
                actor.gameObject.SetActive(false);
                break;
            case FaultKind.Destroyed:
                Destroy(actor.gameObject);
                break;
        }

        yield return null;
        yield return null;
        Require(actionSet.StopCount == 1,
            $"{fault}: action cleanup count was {actionSet.StopCount}, expected exactly one.");
        if (actor != null)
        {
            Require(actor.Brain.bestAction == null && !actor.Brain.isExecuted,
                $"{fault}: AIBrain retained the terminal action.");
            Require(!move.HasActiveMovementRoutineForDiagnostics,
                $"{fault}: movement coroutine survived lifecycle cleanup.");
        }
        Require(IsBuildingOwnershipReleased(ownedBuilding, actorId, usersBeforeFault, reservationsBeforeFault),
            $"{fault}: facility occupancy/queue/worker ownership survived cleanup.");
        Require(emergencyAccounting.CaptureSnapshot().ActiveOperationCount == ledgerBefore,
            $"{fault}: emergency ledger ownership survived cleanup.");
        Require(!reservations.TryGetLeasesByOwner(probeOperationId, out IReadOnlyList<ItemQuantityLease> leasesAfter)
            || leasesAfter.Count == 0,
            $"{fault}: item lease survived cleanup.");
        Require(worldRegistry.GetTransientBuildingOwnershipCount(
                    new CharacterId(actorId)) == 0,
            $"{fault}: actor-owned building index survived cleanup.");

        // Explicitly repeat cleanup. No stop callback, counter or ownership edge
        // may change on this second pass.
        if (actor != null)
        {
            actor.ReleaseTransientAiOwnership($"verifier-second-cleanup:{fault}");
        }
        yield return null;
        Require(actionSet.StopCount == 1,
            $"{fault}: second cleanup was not idempotent (stop={actionSet.StopCount}).");
        Require(IsBuildingOwnershipReleased(ownedBuilding, actorId, usersBeforeFault, reservationsBeforeFault),
            $"{fault}: second cleanup recreated facility ownership.");
        Require(emergencyAccounting.CaptureSnapshot().ActiveOperationCount == ledgerBefore,
            $"{fault}: second cleanup drifted emergency accounting.");
        Require(worldRegistry.GetTransientBuildingOwnershipCount(
                    new CharacterId(actorId)) == 0,
            $"{fault}: second cleanup recreated the actor-owned building index.");

        // Leave enough time for a stopped wait coroutine to have resumed if its
        // handle was leaked. Position and commit counters must remain stable.
        Vector3 settledPosition = actor != null ? actor.transform.position : positionAtFault;
        // The authored QA world may deliberately pause game time while the
        // verifier owns the actors. Cleanup must still prove that no delayed
        // coroutine resumes, so use the editor/player clock rather than the
        // scaled simulation clock here.
        yield return new WaitForSecondsRealtime(0.25f);
        Require(actionSet.LateCommitCount == 0,
            $"{fault}: stopped coroutine performed a late commit.");
        if (actor != null)
        {
            Require(actor.transform.position == settledPosition,
                $"{fault}: actor moved after terminal cleanup.");
            Destroy(actor.gameObject);
            yield return null;
        }
    }

    private CharacterActor CreateActor(
        DungeonRuntimeLifetimeScope scope,
        CharacterSO sourceData,
        FaultKind fault)
    {
        CharacterSO data = Instantiate(sourceData);
        data.id = 990000 + (int)fault;
        data.characterName = $"LifecycleFault_{fault}";
        data.role = CharacterRole.Regular;
        temporaryObjects.Add(data);

        GameObject obj = CharacterAiPlanDebugFixtures.CreatePlayActorObject(data.characterName);
        temporaryObjects.Add(obj);
        BehaviorTree tree = obj.GetComponent<BehaviorTree>();
        if (tree != null) tree.StartWhenEnabled = false;
        Inject(scope, obj);
        CharacterActor actor = obj.GetComponent<CharacterActor>();
        actor.RefreshAbilityCache();
        actor.Initialization(data);
        actor.EnsureRuntimeState();
        Inject(scope, obj);
        actor.RefreshAbilityCache();
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        return actor;
    }

    private static BuildableObject TryAcquireBuildingOwnership(
        CharacterActor actor,
        ICharacterAiWorldRegistry worldRegistry)
    {
        IReadOnlyList<BuildableObject> buildings = worldRegistry?.Buildings;
        if (buildings == null) return null;
        for (int index = 0; index < buildings.Count; index++)
        {
            BuildableObject building = buildings[index];
            if (building == null
                || !building.TryReserveVisit(actor.BuildingVisitor, out _))
            {
                continue;
            }
            building.TryBeginUse(actor.BuildingVisitor, out _);
            if (!building.TryReserveWorker(actor.BuildingVisitor, out _))
            {
                building.ReleaseTransientCharacterOwnership(
                    actor.BuildingVisitor,
                    "fault-verifier-probe-rejected");
                continue;
            }
            return building;
        }
        return null;
    }

    private static bool IsBuildingOwnershipReleased(
        BuildableObject building,
        string actorId,
        int usersBeforeFault,
        int reservationsBeforeFault)
    {
        if (building == null) return true;
        return building.CurrentUserCount <= Math.Max(0, usersBeforeFault - 1)
            && building.ActiveVisitReservationCount <= Math.Max(0, reservationsBeforeFault - 1)
            && !building.HasWorkerReservationForOther(DetachedBuildingCharacter.Instance);
    }

    private static void Inject(DungeonRuntimeLifetimeScope scope, GameObject target)
    {
        foreach (MonoBehaviour component in target.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (component != null) scope.Container.Inject(component);
        }
    }

    private void CleanupTemporaryObjects()
    {
        for (int index = temporaryObjects.Count - 1; index >= 0; index--)
        {
            if (temporaryObjects[index] != null) Destroy(temporaryObjects[index]);
        }
        temporaryObjects.Clear();
    }

    private void BeginLateCommitProbe(LifecycleProbeAction action)
    {
        CancelLateCommitProbe();
        int generation = ++lateCommitGeneration;
        pendingLateCommit = StartCoroutine(LateCommitAfterDelay(action, generation));
    }

    private IEnumerator LateCommitAfterDelay(
        LifecycleProbeAction action,
        int generation)
    {
        yield return new WaitForSecondsRealtime(0.12f);
        if (generation != lateCommitGeneration)
        {
            yield break;
        }
        pendingLateCommit = null;
        action.RecordLateCommit();
    }

    private void CancelLateCommitProbe()
    {
        lateCommitGeneration++;
        if (pendingLateCommit == null)
        {
            return;
        }
        StopCoroutine(pendingLateCommit);
        pendingLateCommit = null;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class LifecycleProbeAction : AIActionSet
    {
        public CharacterAiLifecycleFaultPlayModeRunner Owner { get; set; }
        public int ExecuteCount { get; private set; }
        public int StopCount { get; private set; }
        public int LateCommitCount { get; private set; }
        public override bool RequiresDestination => false;
        private IItemQuantityReservationService reservations;
        private IEmergencyWorkAccountingService emergencyAccounting;
        private WorldItemStackSnapshot leaseTarget;
        private string operationId;
        private string workerId;
        private string leaseId;
        private long accountingSequence;

        public void ConfigureOwnershipProbe(
            IItemQuantityReservationService reservations,
            IEmergencyWorkAccountingService emergencyAccounting,
            WorldItemStackSnapshot leaseTarget,
            string operationId,
            string workerId)
        {
            this.reservations = reservations;
            this.emergencyAccounting = emergencyAccounting;
            this.leaseTarget = leaseTarget;
            this.operationId = operationId;
            this.workerId = workerId;
        }

        public override void Execute(CharacterActor actor)
        {
            ExecuteCount++;
            EmergencyAccountingResult registered = emergencyAccounting.Register(
                new EmergencyWorkLedgerEntry(
                    operationId,
                    workerId,
                    BuiltInWorkTypeIds.Haul,
                    EmergencyWorkFlags.ReserveEligible | EmergencyWorkFlags.InterruptImmediately,
                    EmergencyWuUnits.FromWu(10f),
                    EmergencyWuUnits.FromWu(10f),
                    classificationRevision: 0,
                    mutationSequence: accountingSequence));
            Require(registered.Success, $"ledger registration failed: {registered.Code}");
            Require(reservations.TryReserve(
                    operationId,
                    workerId,
                    ItemReservationPurpose.Hauling,
                    string.Empty,
                    new ItemQuantityReservationRequest(
                        new ItemStackId(leaseTarget.StackId),
                        1,
                        leaseTarget.ReservationSignature),
                    out ItemQuantityLease lease,
                    out DomainFailure leaseFailure),
                $"lease registration failed: {leaseFailure.Code}");
            leaseId = lease.leaseId;
            actor.GetComponent<AbilityMove>()?.StartWait(30f);
            Owner?.BeginLateCommitProbe(this);
        }

        public override void OnStop(CharacterActor actor, AIAction runningAction, string reason)
        {
            StopCount++;
            Owner?.CancelLateCommitProbe();
            actor?.GetComponent<AbilityMove>()?.CancelActiveMovement();
            if (!string.IsNullOrWhiteSpace(leaseId))
            {
                reservations.Release(leaseId, ItemReservationReleaseReason.Cancelled);
                leaseId = string.Empty;
            }
            accountingSequence++;
            EmergencyAccountingResult removed = emergencyAccounting.Remove(
                new EmergencyWorkCompletion(
                    operationId,
                    $"{operationId}:lifecycle-cleanup",
                    accountingSequence));
            Require(removed.Success, $"ledger cleanup failed: {removed.Code}");
        }

        public void RecordLateCommit() => LateCommitCount++;

        private static void Require(bool condition, string message)
        {
            if (!condition) throw new InvalidOperationException(message);
        }
    }

    private sealed class DetachedBuildingCharacter : IBuildingCharacterPort
    {
        public static readonly DetachedBuildingCharacter Instance = new();
        public CharacterId BuildingCharacterId { get; } =
            new("character:lifecycle-fault-verifier-detached");
        public string BuildingDisplayName => "Lifecycle fault verifier";
        public bool IsBuildingInteractionAvailable => true;
    }
}
