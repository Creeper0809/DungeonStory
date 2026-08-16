using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>
/// Faults the physical authority immediately before an action commit.  The
/// success-path verifiers prove the complete animation; this matrix proves
/// that a stale plan cannot spend, duplicate, or commit after its authority
/// has disappeared.
/// </summary>
public static class CharacterAiCrossActionFaultPlayModeVerifier
{
    public const string ReportPath =
        "Artifacts/QA/character-ai-cross-action-fault-playmode.txt";
    private const string PendingFlagPath =
        "Temp/character-ai-cross-action-fault-playmode.flag";

    [MenuItem("DungeonStory/Debug/QA/Run AI Cross-Action Fault PlayMode Matrix")]
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
                CharacterAiCrossActionFaultPlayModeRunner>() != null)
            return;
        new GameObject("AI Cross-Action Fault Matrix")
            .AddComponent<CharacterAiCrossActionFaultPlayModeRunner>();
    }
}

public sealed class CharacterAiCrossActionFaultPlayModeRunner : MonoBehaviour
{
    private const float SetupTimeout = 12f;
    private const float OverallTimeout = 120f;
    private readonly List<string> evidence = new();
    private readonly List<string> failures = new();
    private readonly List<UnityEngine.Object> temporary = new();
    private readonly List<CharacterActor> temporaryActors = new();
    private readonly List<PausedBehaviour> paused = new();
    private readonly List<FaultWall> faultWalls = new();

    private DungeonRuntimeLifetimeScope scope;
    private CharacterActor actor;
    private IWorldItemStackRuntime items;
    private WorldItemRepository repository;
    private IItemQuantityReservationService reservations;
    private IFieldMealConsumptionCommand fieldMeals;
    private ICharacterSubstanceRuntime substances;
    private ICharacterMedicalQuery medicalQuery;
    private ICharacterMedicalCommand medicalCommands;
    private ICharacterMedicalPersistence medicalPersistence;
    private CharacterBodyHealthRuntime bodyHealth;
    private WildlifeRuntime wildlife;
    private IResourceEconomyContentCatalog resources;
    private Grid grid;
    private IDungeonSaveSectionRegistry saveRegistry;
    private ICharacterAiWorldRegistry aiWorld;
    private ICharacterAiSchedulingService aiScheduling;
    private CharacterAiScheduler scheduler;
    private bool schedulerWasEnabled;
    private ICharacterNarrativeQuery narrativeQuery;
    private ICharacterNarrativeCommand narrativeCommands;
    private DungeonSaveSectionRegistry medicalRestoreRegistry;
    private List<DungeonSaveSectionEnvelope> worldSnapshot;
    private float originalTimeScale;

    private IEnumerator Start()
    {
        originalTimeScale = Time.timeScale;
        Time.timeScale = 8f;
        Directory.CreateDirectory("Artifacts/QA");
        File.WriteAllText(CharacterAiCrossActionFaultPlayModeVerifier.ReportPath,
            "# AI Cross-Action Fault PlayMode Matrix\nresult=RUNNING\n");
        yield return RunGuarded();
        try
        {
            Cleanup();
        }
        catch (Exception exception)
        {
            failures.Add("cleanup: " + exception);
        }
        finally
        {
            Time.timeScale = originalTimeScale;
            try
            {
                WriteReport();
            }
            catch (Exception exception)
            {
                Debug.LogError("AI Cross-Action final report failed: " + exception);
            }
            Destroy(gameObject);
            EditorApplication.delayCall += () =>
            {
                if (EditorApplication.isPlaying) EditorApplication.isPlaying = false;
            };
        }
    }

    private IEnumerator RunGuarded()
    {
        Stack<IEnumerator> stack = new();
        stack.Push(Run());
        float deadline = Time.realtimeSinceStartup + OverallTimeout;
        while (stack.Count > 0)
        {
            if (Time.realtimeSinceStartup >= deadline)
            {
                failures.Add($"overall timeout exceeded {OverallTimeout:0}s");
                AppendProgress("FAIL\toverall timeout");
                yield break;
            }
            object current;
            try
            {
                IEnumerator routine = stack.Peek();
                if (!routine.MoveNext())
                {
                    stack.Pop();
                    continue;
                }
                current = routine.Current;
            }
            catch (Exception exception)
            {
                failures.Add(exception.ToString());
                AppendProgress("FAIL\t" + exception.GetType().Name + ": "
                    + exception.Message);
                yield break;
            }
            if (current is IEnumerator nested) stack.Push(nested);
            else yield return current;
        }
    }

    private IEnumerator Run()
    {
        yield return ResolveWorld();
        if (failures.Count > 0) yield break;

        PauseAi();
        QuiesceLiveActionsForSnapshot();
        yield return null;
        worldSnapshot = saveRegistry?.CaptureAll();
        Check(worldSnapshot != null,
            "stable full-world snapshot captured after AI quiescence");
        if (worldSnapshot == null) yield break;
        yield return VerifyHaulSourceFault(remove: true);
        yield return VerifyHaulSourceFault(remove: false);
        yield return VerifyHaulDestinationFault();
        yield return VerifyRescueLifecycleAndRestore();
        yield return VerifyRescuePatientDeath();
        yield return VerifyRescueBedDestroyed();
        yield return VerifyRescueMedicineLost();
        yield return VerifyHuntLifecycle();
        VerifyDrinkCommitAfterItemLoss();
        VerifyFieldMealCommitAfterItemLoss();
        VerifySubstanceCommitAfterItemLoss();
    }

    private IEnumerator ResolveWorld()
    {
        float deadline = Time.realtimeSinceStartup + SetupTimeout;
        bool prepared = false;
        while (Time.realtimeSinceStartup < deadline)
        {
            scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>(
                FindObjectsInactive.Include);
            actor = LiveActors().FirstOrDefault();
            if (scope?.Container != null && actor != null) break;
            if (!prepared && scope?.Container != null)
            {
                prepared = true;
                StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            }
            yield return null;
        }

        Check(scope?.Container != null, "live scope");
        Check(actor != null, "live actor");
        if (scope?.Container == null || actor == null) yield break;
        items = scope.Container.Resolve<IWorldItemStackRuntime>();
        repository = scope.Container.Resolve<WorldItemRepository>();
        reservations = scope.Container.Resolve<IItemQuantityReservationService>();
        fieldMeals = scope.Container.Resolve<IFieldMealConsumptionCommand>();
        substances = scope.Container.Resolve<ICharacterSubstanceRuntime>();
        medicalQuery = scope.Container.Resolve<ICharacterMedicalQuery>();
        medicalCommands = scope.Container.Resolve<ICharacterMedicalCommand>();
        medicalPersistence = scope.Container.Resolve<ICharacterMedicalPersistence>();
        bodyHealth = scope.Container.Resolve<CharacterBodyHealthRuntime>();
        wildlife = scope.Container.Resolve<WildlifeRuntime>();
        resources = scope.Container.Resolve<IResourceEconomyContentCatalog>();
        scope.Container.Resolve<IGridSystemProvider>().TryGetGrid(out grid);
        saveRegistry = scope.Container.Resolve<IDungeonSaveSectionRegistry>();
        aiWorld = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        aiScheduling = scope.Container.Resolve<ICharacterAiSchedulingService>();
        scheduler = FindFirstObjectByType<CharacterAiScheduler>(
            FindObjectsInactive.Include);
        narrativeQuery = scope.Container.Resolve<ICharacterNarrativeQuery>();
        narrativeCommands = scope.Container.Resolve<ICharacterNarrativeCommand>();
        DungeonRuntimeAggregateRootStore aggregateRootStore =
            scope.Container.Resolve<DungeonRuntimeAggregateRootStore>();
        medicalRestoreRegistry = CreateMedicalRestoreRegistry(aggregateRootStore);
        Check(items != null && repository != null && reservations != null,
            "physical item authorities resolved");
        Check(medicalQuery != null && medicalCommands != null
                && medicalPersistence != null
                && bodyHealth != null && wildlife != null,
            "medical and wildlife authorities resolved");
    }

    private IEnumerator VerifyHaulSourceFault(bool remove)
    {
        string row = remove ? "haul-source-despawn" : "haul-source-quantity-shrink";
        string itemId = FindStackableItemId();
        Check(!string.IsNullOrWhiteSpace(itemId), row + ": seed item found");
        if (string.IsNullOrWhiteSpace(itemId)) yield break;

        Vector2Int position = actor.GetNowXY();
        HashSet<string> beforeIds = items.GetAllStacks()
            .Where(stack => stack != null).Select(stack => stack.StackId).ToHashSet();
        Check(items.SpawnItemAt(itemId, 2, position, WorldItemStackState.Loose,
            string.Empty, out int spawned) && spawned == 2, row + ": physical seed spawned");
        WorldItemStackSnapshot source = items.GetAllStacks().FirstOrDefault(stack =>
            stack != null && !beforeIds.Contains(stack.StackId)
            && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal));
        Check(source != null, row + ": source stack identified");
        if (source == null) yield break;

        string operation = $"qa:{row}:{Guid.NewGuid():N}";
        IWorldItemQuantityLeaseRuntime leaseRuntime =
            items as IWorldItemQuantityLeaseRuntime;
        WorldItemReservedStackQuantity lease = default;
        string reserveFailure = "lease-runtime-unavailable";
        bool reserved = leaseRuntime != null
            && leaseRuntime.TryReserveAvailableItemForDirectPickup(
                actor, itemId, 2, ItemReservationPurpose.Hauling, operation,
                out lease, out _, out reserveFailure);
        Check(reserved,
            row + ": quantity lease acquired; " + reserveFailure);
        if (!lease.IsValid) yield break;

        WorldItemStackSnapshot leasedStack = items.GetAllStacks().FirstOrDefault(stack =>
            stack != null && string.Equals(stack.StackId, lease.StackId,
                StringComparison.Ordinal));
        Check(leasedStack != null && leasedStack.TotalQuantity >= 2,
            row + ": leased physical slice resolved");
        if (leasedStack == null) yield break;
        int totalBeforeFault = TotalQuantity(itemId);
        int removedByFault = remove
            ? leasedStack.TotalQuantity
            : leasedStack.TotalQuantity - 1;
        if (remove) WorldItemRepositoryEditorAccess.RemoveStack(repository, lease.StackId);
        else WorldItemRepositoryEditorAccess.SetQuantity(repository, lease.StackId, 1);

        CharacterCarryInventory carry = CharacterCarryInventory.Ensure(actor);
        int carriedBefore = carry.CountItem(itemId);
        bool committed = items.TryPickupReservedStackQuantity(
            actor, carry, lease, out int picked, out _);
        Check(!committed && picked == 0 && carry.CountItem(itemId) == carriedBefore,
            row + ": stale pickup performed no late commit");
        leaseRuntime.ReleaseQuantityLease(
            lease.LeaseId, ItemReservationReleaseReason.StackInvalidated);
        Check(reservations.GetReservedQuantity(new ItemStackId(lease.StackId)) == 0,
            row + ": lease released exactly once");
        Check(TotalQuantity(itemId) == totalBeforeFault - removedByFault,
            row + ": quantity conserved across injected physical loss");
        evidence.Add(row + "=PASS");
        yield return null;
    }

    private IEnumerator VerifyHaulDestinationFault()
    {
        Facility destination = CreateTemporaryWarehouse();
        Check(destination != null, "haul-destination-destroy: temporary warehouse created");
        if (destination == null) yield break;
        string itemId = FindStackableItemId();
        Vector2Int sourceCell = actor.GetNowXY();
        HashSet<string> beforeIds = items.GetAllStacks()
            .Where(stack => stack != null).Select(stack => stack.StackId).ToHashSet();
        items.SpawnItemAt(itemId, 1, sourceCell, WorldItemStackState.Loose,
            string.Empty, out _);
        WorldItemStackSnapshot source = items.GetAllStacks().FirstOrDefault(stack =>
            stack != null && !beforeIds.Contains(stack.StackId)
            && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal));
        if (source != null) items.PrioritizeHaul(source.StackId);
        AbilityHaul haul = AbilityHaul.Ensure(actor);
        haul.StartHauling();
        float deadline = Time.realtimeSinceStartup + 5f;
        while (haul.IsHauling
            && !haul.CurrentExecutionStage.StartsWith("배송", StringComparison.Ordinal)
            && Time.realtimeSinceStartup < deadline)
            yield return null;
        destination.DestroySelf();
        yield return null;
        haul.StopHauling("qa-destination-destroyed");
        yield return null;
        Check(!haul.IsHauling && actor.CarryInventory?.HasItems != true,
            "haul-destination-destroy: carry recovered and coroutine terminal");
        if (source != null)
            Check(reservations.GetReservedQuantity(new ItemStackId(source.StackId)) == 0,
                "haul-destination-destroy: source lease released exactly once");
        evidence.Add("haul-destination-destroy=PASS");
    }

    private IEnumerator VerifyRescueLifecycleAndRestore()
    {
        CharacterActor patient = LiveActors().FirstOrDefault(candidate => candidate != actor);
        if (patient == null)
        {
            evidence.Add("rescue-faults=DELEGATED:requires-two-live-actors");
            yield break;
        }
        CharacterBodyHealthSnapshot original = bodyHealth.GetSnapshot(patient);
        List<CharacterBodyPartHealthState> injured = original.Parts.Select(ClonePart).ToList();
        foreach (CharacterBodyPartHealthState part in injured)
            if (part.bodyPart is CombatBodyPart.LeftLeg or CombatBodyPart.RightLeg)
                part.currentHealth = Mathf.Max(0.1f, part.maxHealth * 0.05f);
        bodyHealth.ApplySnapshot(patient,
            new CharacterBodyHealthSnapshot(injured, 10f, 0f, 0.05f, 0.2f, 0.05f, true),
            "qa-cross-action-rescue");
        yield return null;
        AbilityRescue rescue = AbilityRescue.Ensure(actor);
        rescue.StartRescue(patient);
        yield return null;
        CharacterMedicalOrder order = medicalQuery.ActiveOrders.FirstOrDefault(value =>
            value != null && value.IsActive && string.Equals(value.patientId,
                patient.Identity?.PersistentId, StringComparison.Ordinal));
        List<DungeonSaveSectionEnvelope> captured =
            medicalRestoreRegistry.CaptureAll();
        rescue.StopRescue(CharacterMedicalStatusCode.RescueInterrupted);
        DungeonGameRestoreReport restoreReport = new();
        bool restored = medicalRestoreRegistry.RestoreAll(captured, restoreReport);
        Check(restored && restoreReport.Success,
            "rescue-save-load: medical aggregate restored through V18 transaction boundary");
        yield return null;
        patient.SetLifecycleState(CharacterLifecycleState.Despawned);
        yield return null;
        Check(!rescue.IsRescuing,
            "rescue-patient-despawn/save-load: coroutine and reservation terminal");
        if (order != null && medicalQuery.TryGetOrder(order.orderId, out CharacterMedicalOrder after))
            Check(!after.IsActive || string.IsNullOrWhiteSpace(after.rescuerId),
                "rescue-patient-despawn/save-load: medical ownership released");
        patient.SetLifecycleState(CharacterLifecycleState.Active);
        bodyHealth.ApplySnapshot(patient, original, "qa-cross-action-rescue-restore");
        evidence.Add("rescue-patient-despawn-save-load=PASS");
    }

    private IEnumerator VerifyHuntLifecycle()
    {
        wildlife.Tick();
        WildlifeActor prey = wildlife.Wildlife.FirstOrDefault(value =>
            value != null && value.IsAlive);
        if (prey == null)
        {
            evidence.Add("hunt-faults=DELEGATED:no-live-wildlife");
            yield break;
        }
        wildlife.DesignateHunt(prey.WildlifeId, true, priority: true);
        AbilityHunt hunt = AbilityHunt.Ensure(actor, wildlife);
        hunt.StartHunting();
        yield return null;
        int healthBefore = prey.CurrentHealth;
        actor.SetLifecycleState(CharacterLifecycleState.Downed);
        yield return null;
        Check(!hunt.IsHunting && (prey == null || prey.CurrentHealth == healthBefore),
            "hunt-hunter-downed: reservation released and no late hit");
        actor.SetLifecycleState(CharacterLifecycleState.Active);
        hunt.StopHunting("qa-idempotent-second-cleanup");
        evidence.Add("hunt-hunter-downed=PASS");

        CharacterActor doomedHunter = CreateTemporaryActor("HuntDeadFault", 990821);
        if (doomedHunter != null && prey != null && prey.IsAlive)
        {
            wildlife.DesignateHunt(prey.WildlifeId, true, priority: true);
            AbilityHunt doomedHunt = AbilityHunt.Ensure(doomedHunter, wildlife);
            doomedHunt.StartHunting();
            yield return null;
            int beforeDeath = prey.CurrentHealth;
            doomedHunter.Die(CharacterDeathCauseCode.Combat,
                "qa-cross-action-hunter-dead");
            yield return null;
            yield return null;
            Check(!doomedHunt.IsHunting
                    && (prey == null || prey.CurrentHealth == beforeDeath),
                "hunt-hunter-dead: reservation released and no late hit");
            doomedHunt.StopHunting("qa-idempotent-second-cleanup");
            evidence.Add("hunt-hunter-dead=PASS");
        }

        WildlifeActor despawnPrey = SpawnIsolatedHuntPrey(
            wildlife.Wildlife.FirstOrDefault(value => value != null && value.IsAlive));
        Check(despawnPrey != null,
            "hunt-prey-despawn: isolated prey fixture available");
        if (despawnPrey == null) yield break;

        CharacterActor despawnHunter = CreateTemporaryActor(
            "HuntPreyDespawnFault", 990822);
        Check(despawnHunter != null, "hunt-prey-despawn: temporary hunter created");
        if (despawnHunter == null) yield break;
        Check(TryPositionOutsideAttackRange(despawnHunter, despawnPrey),
            "hunt-prey-despawn: hunter positioned outside immediate attack range");
        int carcassesBefore = CountCarcasses();
        AbilityHunt despawnHunt = AbilityHunt.Ensure(despawnHunter, wildlife);
        despawnHunt.StartHunting();
        // StartCoroutine runs synchronously to its first movement yield.  Destroying
        // here faults the reserved target before any later movement/attack commit.
        Destroy(despawnPrey.gameObject);
        yield return null;
        yield return null;
        Check(!despawnHunt.IsHunting && CountCarcasses() == carcassesBefore,
            "hunt-prey-despawn: failed terminal without carcass late commit");
        despawnHunt.StopHunting("qa-idempotent-second-cleanup");
        evidence.Add("hunt-prey-despawn=PASS");

        WildlifeActor pathPrey = SpawnIsolatedHuntPrey(
            wildlife.Wildlife.FirstOrDefault(value => value != null && value.IsAlive));
        CharacterActor blockedHunter = CreateTemporaryActor(
            "HuntPathInvalidationFault", 990823);
        if (pathPrey != null && blockedHunter != null && grid != null)
        {
            Check(TryPositionOutsideAttackRange(blockedHunter, pathPrey),
                "hunt-path-invalidation: hunter positioned outside attack range");
            AbilityHunt blockedHunt = AbilityHunt.Ensure(blockedHunter, wildlife);
            IGridPathSearchBroker previousBroker = blockedHunt
                .DebugReplacePathSearchBroker(new NoPathSearchBroker());
            int pathHealthBefore = pathPrey.CurrentHealth;
            try
            {
                blockedHunt.StartHunting();
                yield return null;
                Check(!blockedHunt.IsHunting
                        && pathPrey.CurrentHealth == pathHealthBefore,
                    "hunt-path-invalidation: no-path terminal without late hit");
                blockedHunt.StopHunting("qa-idempotent-second-cleanup");
            }
            finally
            {
                blockedHunt.DebugReplacePathSearchBroker(previousBroker);
            }
            evidence.Add("hunt-path-invalidation=PASS");
        }
    }

    private IEnumerator VerifyRescuePatientDeath()
    {
        CharacterActor rescuer = CreateTemporaryActor("RescueDeathRescuer", 990813);
        CharacterActor patient = CreateTemporaryActor("RescueDeathPatient", 990814);
        Check(rescuer != null && patient != null,
            "rescue-patient-death: temporary participants created");
        if (rescuer == null || patient == null) yield break;
        if (grid != null)
        {
            Vector2Int origin = actor.GetNowXY();
            rescuer.transform.position = grid.GetWorldPos(origin);
            patient.transform.position = grid.GetWorldPos(origin + Vector2Int.right);
        }
        CharacterBodyHealthSnapshot baseline = bodyHealth.GetSnapshot(patient);
        List<CharacterBodyPartHealthState> injured = baseline.Parts
            .Select(ClonePart).ToList();
        foreach (CharacterBodyPartHealthState part in injured)
            if (part.bodyPart is CombatBodyPart.LeftLeg or CombatBodyPart.RightLeg)
                part.currentHealth = Mathf.Max(0.1f, part.maxHealth * 0.05f);
        bodyHealth.ApplySnapshot(patient,
            new CharacterBodyHealthSnapshot(injured, 15f, 0f, 0.03f,
                0.15f, 0.03f, true), "qa-rescue-patient-death");
        yield return null;
        yield return null;
        CharacterMedicalOrder order = medicalQuery.ActiveOrders.FirstOrDefault(value =>
            value != null && value.IsActive && string.Equals(value.patientId,
                patient.Identity?.PersistentId, StringComparison.Ordinal));
        Check(order != null, "rescue-patient-death: medical order created");
        if (order == null) yield break;
        AbilityRescue rescue = AbilityRescue.Ensure(rescuer);
        rescue.StartRescue(patient);
        yield return null;
        patient.Die(CharacterDeathCauseCode.Combat,
            "qa-cross-action-patient-dead");
        yield return null;
        yield return null;
        Check(!rescue.IsRescuing,
            "rescue-patient-death: coroutine terminal without late treatment");
        if (medicalQuery.TryGetOrder(order.orderId, out CharacterMedicalOrder after))
            Check(!after.IsActive || string.IsNullOrWhiteSpace(after.rescuerId),
                "rescue-patient-death: reservation released exactly once");
        rescue.StopRescue(CharacterMedicalStatusCode.RescueInterrupted);
        evidence.Add("rescue-patient-death=PASS");
    }

    private IEnumerator VerifyRescueBedDestroyed()
    {
        CharacterActor rescuer = CreateTemporaryActor("RescueBedRescuer", 990815);
        CharacterActor patient = CreateTemporaryActor("RescueBedPatient", 990816);
        Check(rescuer != null && patient != null,
            "rescue-bed-destroy: actors created");
        if (rescuer == null || patient == null) yield break;
        DownForRescue(patient, "qa-rescue-bed-destroy");
        yield return null;
        yield return null;
        CharacterMedicalOrder order = FindOrder(patient);
        Check(order != null, "rescue-bed-destroy: medical order created");
        if (order == null) yield break;

        // Create the fault target only after the target patient's order exists.
        // Registering a free medical bed before yielding lets another active
        // patient legitimately reserve it and makes this row test scheduler
        // timing instead of destruction of this order's assigned destination.
        Facility bed = CreateTemporaryMedicalFacility();
        Check(bed != null, "rescue-bed-destroy: temporary bed created");
        if (bed == null) yield break;
        PositionRescueParticipants(rescuer, patient, bed.centerPos);
        bool assigned = medicalCommands.TryAssignSpecificTreatmentFacility(
            order.orderId,
            bed,
            out DomainFailure assignmentFailure);
        Check(assigned,
            "rescue-bed-destroy: temporary bed explicitly assigned; "
            + assignmentFailure.Code);
        if (!assigned) yield break;
        AbilityRescue rescue = AbilityRescue.Ensure(rescuer);
        rescue.StartRescue(patient);
        float deadline = Time.realtimeSinceStartup + 6f;
        while (rescue.IsRescuing && order.state < CharacterMedicalOrderState.Carrying
            && Time.realtimeSinceStartup < deadline)
            yield return null;
        Check(medicalQuery.TryGetTreatmentFacility(order, out BuildableObject selected)
                && selected == bed,
            "rescue-bed-destroy: temporary bed owns destination leg");
        float workBefore = order.completedTreatmentWork;
        bed.DestroySelf();
        yield return null;
        yield return null;
        yield return new WaitForSecondsRealtime(0.25f);
        Check(!rescue.IsRescuing
                && !patient.transform.IsChildOf(rescuer.transform)
                && Mathf.Approximately(order.completedTreatmentWork, workBefore),
            "rescue-bed-destroy: carry released and late treatment commit is zero");
        rescue.StopRescue(CharacterMedicalStatusCode.RescueInterrupted);
        evidence.Add("rescue-bed-destroy=PASS");
    }

    private IEnumerator VerifyRescueMedicineLost()
    {
        Facility bed = CreateTemporaryMedicalFacility();
        CharacterActor rescuer = CreateTemporaryActor("RescueMedicineRescuer", 990817);
        CharacterActor patient = CreateTemporaryActor("RescueMedicinePatient", 990818);
        Check(bed != null && rescuer != null && patient != null,
            "rescue-medicine-loss: fixture created");
        if (bed == null || rescuer == null || patient == null) yield break;
        PositionRescueParticipants(rescuer, patient, bed.centerPos);
        DownForRescue(patient, "qa-rescue-medicine-loss");
        yield return null;
        yield return null;
        CharacterMedicalOrder order = FindOrder(patient);
        Check(order != null, "rescue-medicine-loss: medical order created");
        if (order == null) yield break;
        string destination = WorldItemStackRuntime.FacilityInputDestinationPrefix
            + $"medical:{order.orderId}";
        ResourceItemDefinitionSO medicine = resources.Items
            .Where(item => item != null && item.Kind == ResourceItemKind.Medicine
                && item.SupportsInjuryTreatment)
            .OrderBy(item => item.ItemId, StringComparer.Ordinal)
            .FirstOrDefault();
        Check(medicine != null && items.SpawnItemAt(medicine.ItemId, 1,
                order.BedPosition, WorldItemStackState.FacilityBuffer,
                destination, out int spawned) && spawned == 1,
            "rescue-medicine-loss: physical medicine seeded");
        if (medicine == null) yield break;
        AbilityRescue rescue = AbilityRescue.Ensure(rescuer);
        rescue.StartRescue(patient);
        float deadline = Time.realtimeSinceStartup + 6f;
        while (rescue.IsRescuing && order.state < CharacterMedicalOrderState.Carrying
            && Time.realtimeSinceStartup < deadline)
            yield return null;
        WorldItemStackSnapshot supply = items.GetAllStacks().FirstOrDefault(stack =>
            stack != null && string.Equals(stack.DestinationId, destination,
                StringComparison.Ordinal));
        Check(supply != null, "rescue-medicine-loss: reserved medicine located");
        if (supply != null) WorldItemRepositoryEditorAccess.RemoveStack(
            repository, supply.StackId);
        float treatmentBefore = order.completedTreatmentWork;
        yield return new WaitForSecondsRealtime(0.25f);
        Check(!order.treatmentSupplyConsumed
                && Mathf.Approximately(order.completedTreatmentWork, treatmentBefore),
            "rescue-medicine-loss: no treatment progress or late supply commit");
        rescue.StopRescue(CharacterMedicalStatusCode.TreatmentInterrupted);
        yield return null;
        Check(string.IsNullOrWhiteSpace(order.rescuerId) || !order.IsActive,
            "rescue-medicine-loss: medical ownership released exactly once");
        evidence.Add("rescue-medicine-loss=PASS");
    }

    private void VerifyDrinkCommitAfterItemLoss()
    {
        WorldItemStackSnapshot source = FindConsumableStack();
        if (source == null)
        {
            evidence.Add("drink-item-loss=DELEGATED:no-consumable-stack");
            return;
        }
        string stackId = source.StackId;
        int before = TotalQuantity(source.ItemId);
        WorldItemRepositoryEditorAccess.RemoveStack(repository, stackId);
        bool committed = items.TryConsumeStackQuantity(stackId, 1, out _);
        Check(!committed && TotalQuantity(source.ItemId) == before - source.TotalQuantity,
            "drink-item-loss: commit rejected without duplicate spend");
        evidence.Add("drink-item-loss=PASS");
    }

    private void VerifyFieldMealCommitAfterItemLoss()
    {
        ResourceItemDefinitionSO meal = resources.Items
            .Where(item => item != null
                && item.TryGetFeature(out FoodItemFeature food)
                && food.freshnessSeconds > 4.25f
                && food.servingRole != MealServingRole.EmergencyOnly)
            .OrderBy(item => item.ItemId, StringComparer.Ordinal)
            .FirstOrDefault();
        Check(meal != null, "primitive-field-meal-item-loss: authored meal found");
        if (meal == null) return;

        HashSet<string> beforeIds = items.GetAllStacks()
            .Where(stack => stack != null)
            .Select(stack => stack.StackId)
            .ToHashSet(StringComparer.Ordinal);
        bool spawned = items.SpawnItemAt(
            meal.ItemId,
            1,
            FindIsolatedItemSeedPosition(),
            WorldItemStackState.Loose,
            string.Empty,
            out int spawnedCount);
        Check(spawned && spawnedCount == 1,
            "primitive-field-meal-item-loss: physical meal seeded");
        if (!spawned || spawnedCount != 1) return;
        WorldItemStackSnapshot source = items.GetAllStacks().FirstOrDefault(stack =>
            stack != null
            && !beforeIds.Contains(stack.StackId)
            && string.Equals(stack.ItemId, meal.ItemId, StringComparison.Ordinal));
        Check(source != null,
            "primitive-field-meal-item-loss: isolated source identified");
        if (source == null) return;

        float originalHunger = actor.Stats.Stats[CharacterCondition.HUNGER];
        actor.Stats.Stats[CharacterCondition.HUNGER] = 0f;
        ItemStackId stackId = new(source.StackId);
        WorldItemRepositoryEditorAccess.RemoveStack(repository, source.StackId);
        bool committed = fieldMeals.TryConsumeFieldMeal(
            actor,
            stackId,
            out MealConsumptionResult consumeResult);
        actor.Stats.Stats[CharacterCondition.HUNGER] = originalHunger;
        Check(!committed
                && consumeResult.FailureCode
                    is CharacterConsumablesFailureCode.ItemNotConsumable
                    or CharacterConsumablesFailureCode.ItemStackMissing
                    or CharacterConsumablesFailureCode.PhysicalConsumptionFailed,
            "primitive-field-meal-item-loss: late commit rejected by physical authority");
        evidence.Add("primitive-field-meal-item-loss=PASS");
    }

    private void VerifySubstanceCommitAfterItemLoss()
    {
        ResourceItemDefinitionSO item = resources.Items
            .Where(candidate => candidate != null
                && candidate.TryGetFeature(out SubstanceItemFeature feature)
                && !string.IsNullOrWhiteSpace(feature.substanceId))
            .OrderBy(candidate => candidate.ItemId, StringComparer.Ordinal)
            .FirstOrDefault();
        Check(item != null, "substance-item-loss: authored substance found");
        if (item == null) return;
        SubstanceItemFeature substance = item.GetFeatureOrDefault<SubstanceItemFeature>();
        HashSet<string> beforeIds = items.GetAllStacks()
            .Where(stack => stack != null)
            .Select(stack => stack.StackId)
            .ToHashSet(StringComparer.Ordinal);
        bool spawned = items.SpawnItemAt(
            item.ItemId,
            1,
            FindIsolatedItemSeedPosition(),
            WorldItemStackState.Loose,
            string.Empty,
            out int spawnedCount);
        Check(spawned && spawnedCount == 1,
            "substance-item-loss: physical substance seeded");
        if (!spawned || spawnedCount != 1) return;
        WorldItemStackSnapshot source = items.GetAllStacks().FirstOrDefault(stack =>
            stack != null
            && !beforeIds.Contains(stack.StackId)
            && string.Equals(stack.ItemId, item.ItemId, StringComparison.Ordinal));
        Check(source != null, "substance-item-loss: isolated source identified");
        if (source == null) return;

        CharacterSubstancePolicyState originalPolicy = substances.GetPolicy(
            actor,
            substance.substanceId);
        substances.SetPolicy(
            actor,
            substance.substanceId,
            SubstancePolicyMode.Scheduled,
            originalPolicy.moodThreshold,
            originalPolicy.scheduledHour);
        WorldItemRepositoryEditorAccess.RemoveStack(repository, source.StackId);
        bool committed = substances.TryConsume(new ConsumeSubstanceCommand(
            new ConsumableOperationId("consumable-operation:qa-substance-item-loss:"
                + Guid.NewGuid().ToString("N")),
            CharacterPersistentIdentity.Require(actor),
            item.StableId,
            new ItemStackId(source.StackId),
            medicalContext: false,
            combatContext: false), out SubstanceUseResult result);
        substances.SetPolicy(
            actor,
            substance.substanceId,
            originalPolicy.mode,
            originalPolicy.moodThreshold,
            originalPolicy.scheduledHour);
        Check(!committed
                && result.FailureCode == CharacterConsumablesFailureCode.ItemStackMissing,
            "substance-item-loss: late commit rejected by missing physical stack");
        evidence.Add("substance-item-loss=PASS");
    }

    private Facility CreateTemporaryWarehouse()
    {
        BuildingSO data = AssetDatabase.FindAssets("t:BuildingSO")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .FirstOrDefault(asset => asset != null
                && asset.GetStorageCapacity() > 0
                && asset.StoresAllCategories());
        GridSystemManager gridManager = FindFirstObjectByType<GridSystemManager>(
            FindObjectsInactive.Include);
        if (data == null || gridManager?.grid == null) return null;
        Vector2Int actorPosition = actor.GetNowXY();
        Vector2Int? reachable = gridManager.grid.SearchPath(actorPosition)
            .GetReachablePositions()
            .Where(position => gridManager.grid.IsValidGridPos(position)
                && gridManager.grid.IsWalkable(position))
            .Distinct()
            .OrderBy(position => Mathf.Abs(position.x - actorPosition.x)
                + Mathf.Abs(position.y - actorPosition.y))
            .Skip(1)
            .Select(position => (Vector2Int?)position)
            .FirstOrDefault();
        if (!reachable.HasValue) return null;
        Vector2Int position = reachable.Value;
        GameObject obj = new("QA Cross-Action Warehouse");
        temporary.Add(obj);
        Facility facility = obj.AddComponent<Facility>();
        scope.Container.InjectGameObject(obj);
        facility.SetGrid(gridManager.grid);
        facility.Initialization(data, position);
        obj.transform.position = gridManager.grid.GetWorldPos(position);
        return facility;
    }

    private Facility CreateTemporaryMedicalFacility()
    {
        BuildingSO data = AssetDatabase.FindAssets("t:BuildingSO")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .FirstOrDefault(asset => asset != null
                && asset.GetAbility<BuildingMedicalAbility>() != null);
        if (data == null || grid == null) return null;
        Vector2Int actorPosition = actor.GetNowXY();
        Vector2Int? reachable = grid.SearchPath(actorPosition)
            .GetReachablePositions()
            .Where(position => grid.IsValidGridPos(position)
                && grid.IsWalkable(position))
            .Distinct()
            .OrderBy(position => Mathf.Abs(position.x - actorPosition.x)
                + Mathf.Abs(position.y - actorPosition.y))
            .Skip(2)
            .Select(position => (Vector2Int?)position)
            .FirstOrDefault();
        if (!reachable.HasValue) return null;
        GameObject obj = new("QA Cross-Action Medical Bed");
        temporary.Add(obj);
        Facility facility = obj.AddComponent<Facility>();
        scope.Container.InjectGameObject(obj);
        facility.SetGrid(grid);
        facility.Initialization(data, reachable.Value);
        obj.transform.position = grid.GetWorldPos(reachable.Value);
        return facility;
    }

    private void PositionRescueParticipants(
        CharacterActor rescuer,
        CharacterActor patient,
        Vector2Int destination)
    {
        if (grid == null || rescuer == null || patient == null) return;
        Vector2Int[] stands = grid.SearchPath(actor.GetNowXY())
            .GetReachablePositions()
            .Where(position => grid.IsValidGridPos(position)
                && grid.IsWalkable(position))
            .Distinct()
            .OrderBy(position => Mathf.Abs(position.x - destination.x)
                + Mathf.Abs(position.y - destination.y))
            .Take(2)
            .ToArray();
        if (stands.Length > 0) rescuer.transform.position = grid.GetWorldPos(stands[0]);
        if (stands.Length > 1) patient.transform.position = grid.GetWorldPos(stands[1]);
    }

    private void DownForRescue(CharacterActor patient, string reason)
    {
        CharacterBodyHealthSnapshot baseline = bodyHealth.GetSnapshot(patient);
        List<CharacterBodyPartHealthState> injured = baseline.Parts
            .Select(ClonePart).ToList();
        foreach (CharacterBodyPartHealthState part in injured)
            if (part.bodyPart is CombatBodyPart.LeftLeg or CombatBodyPart.RightLeg)
                part.currentHealth = Mathf.Max(0.1f, part.maxHealth * 0.05f);
        bodyHealth.ApplySnapshot(patient,
            new CharacterBodyHealthSnapshot(injured, 12f, 0f, 0.04f,
                0.2f, 0.04f, true), reason);
    }

    private CharacterMedicalOrder FindOrder(CharacterActor patient) =>
        medicalQuery.ActiveOrders.FirstOrDefault(value => value != null
            && value.IsActive
            && string.Equals(value.patientId, patient?.Identity?.PersistentId,
                StringComparison.Ordinal));

    private DungeonSaveSectionRegistry CreateMedicalRestoreRegistry(
        DungeonRuntimeAggregateRootStore aggregateRootStore) =>
        CreateIsolatedRestoreRegistry(
            CharacterMedicalSaveSection.Id,
            medicalPersistence as IDungeonRestoreTransactionParticipant,
            aggregateRootStore);

    private DungeonSaveSectionRegistry CreateIsolatedRestoreRegistry(
        string sectionId,
        IDungeonRestoreTransactionParticipant participant,
        DungeonRuntimeAggregateRootStore aggregateRootStore)
    {
        IDungeonSaveSection section = saveRegistry.OrderedSections.Single(
            candidate => string.Equals(candidate.SectionId, sectionId,
                StringComparison.Ordinal));
        List<IDungeonSaveSection> sections = section.DependsOn
            .Distinct(StringComparer.Ordinal)
            .Select(dependency =>
                (IDungeonSaveSection)new DependencyMarkerSection(dependency))
            .Append(section)
            .ToList();
        return new DungeonSaveSectionRegistry(
            sections,
            aggregateRootStore,
            participant != null
                ? new[] { participant }
                : Array.Empty<IDungeonRestoreTransactionParticipant>());
    }

    private WildlifeActor SpawnIsolatedHuntPrey(WildlifeActor speciesSource)
    {
        if (speciesSource == null || grid == null || wildlife == null)
        {
            return null;
        }

        foreach (WildlifeActor candidate in wildlife.Wildlife
                     .Where(value => value != null))
        {
            wildlife.DesignateHunt(candidate.WildlifeId, false);
        }

        HashSet<string> existingIds = wildlife.Wildlife
            .Where(value => value != null)
            .Select(value => value.WildlifeId)
            .ToHashSet(StringComparer.Ordinal);
        Vector2Int[] spawnCandidates = grid.GetCells()
            .Where(cell => cell != null && grid.IsWalkable(cell.Position))
            .OrderByDescending(cell => Mathf.Abs(cell.Position.x - actor.GetNowXY().x)
                + Mathf.Abs(cell.Position.y - actor.GetNowXY().y))
            .Select(cell => cell.Position)
            .ToArray();
        bool spawned = false;
        int spawnedCount = 0;
        string spawnMessage = "no eligible spawn candidate";
        for (int index = 0; index < spawnCandidates.Length && !spawned; index++)
        {
            spawned = wildlife.DebugSpawn(
                speciesSource.SpeciesId,
                1,
                spawnCandidates[index],
                out spawnedCount,
                out spawnMessage);
        }
        Check(spawned && spawnedCount == 1,
            "hunt fixture: isolated prey spawned; " + spawnMessage);
        if (!spawned || spawnedCount != 1)
        {
            return null;
        }

        WildlifeActor isolated = wildlife.Wildlife.FirstOrDefault(value =>
            value != null && !existingIds.Contains(value.WildlifeId));
        Check(isolated != null
                && wildlife.DesignateHunt(isolated.WildlifeId, true, priority: true),
            "hunt fixture: only isolated prey designated");
        return isolated;
    }

    private bool TryPositionOutsideAttackRange(
        CharacterActor hunter,
        WildlifeActor prey)
    {
        if (hunter == null || prey == null || grid == null)
        {
            return false;
        }

        Vector2Int? position = grid.SearchPath(hunter.GetNowXY())
            .GetReachablePositions()
            .Where(candidate => grid.IsWalkable(candidate)
                && !wildlife.CanAttackHuntTargetFrom(hunter, prey, grid, candidate))
            .OrderByDescending(candidate =>
                Mathf.Abs(candidate.x - prey.GridPosition.x)
                + Mathf.Abs(candidate.y - prey.GridPosition.y))
            .Select(candidate => (Vector2Int?)candidate)
            .FirstOrDefault();
        if (!position.HasValue)
        {
            return false;
        }

        hunter.transform.position = grid.GetWorldPos(position.Value);
        return hunter.GetNowXY() == position.Value
            && !wildlife.CanAttackHuntTargetFrom(
                hunter,
                prey,
                grid,
                position.Value);
    }

    private CharacterActor CreateTemporaryActor(string displayName, int id)
    {
        if (actor?.data == null || scope?.Container == null) return null;
        CharacterSO data = Instantiate(actor.data);
        data.id = id;
        data.characterName = displayName;
        data.characterType = CharacterType.NPC;
        data.role = CharacterRole.Regular;
        temporary.Add(data);
        GameObject obj = CharacterAiPlanDebugFixtures.CreatePlayActorObject(displayName);
        temporary.Add(obj);
        obj.SetActive(false);
        scope.Container.InjectGameObject(obj);
        CharacterActor created = obj.GetComponent<CharacterActor>();
        created.RefreshAbilityCache();
        created.Initialization(data);
        created.EnsureRuntimeState();
        scope.Container.InjectGameObject(obj);
        created.RefreshAbilityCache();

        // The first enable happens before CharacterData is assigned. Remove
        // that provisional publication so the second registration publishes
        // the initialized growth/proficiency authority instead of retaining a
        // character record with no canonical proficiencies.
        aiScheduling?.Unregister(created);
        aiWorld?.UnregisterCharacter(created);
        aiWorld?.UnregisterCharacterLifetime(created);
        if (actor?.Progression != null && created.Progression != null)
        {
            created.Progression.RestorePersistentState(
                actor.Progression.CapturePersistentState());
        }
        AbilityWork work = obj.GetComponent<AbilityWork>()
            ?? obj.AddComponent<AbilityWork>();
        scope.Container.InjectGameObject(obj);
        created.RefreshAbilityCache();
        created.SetLifecycleState(CharacterLifecycleState.Active);
        aiWorld?.RegisterCharacterLifetime(created);
        aiWorld?.RegisterCharacter(created);
        CharacterId createdId = CharacterPersistentIdentity.Require(created);
        if (narrativeQuery != null
            && narrativeCommands != null
            && !narrativeQuery.TryGet(createdId, out _))
        {
            narrativeCommands.Register(
                createdId,
                new CharacterSpeciesId(created.SpeciesTag),
                Array.Empty<string>(),
                Array.Empty<string>(),
                created.Progression?.GrowthState?.startingProficiencies);
        }
        work.Initializtion(data);
        obj.SetActive(true);
        aiScheduling?.Unregister(created);
        temporaryActors.Add(created);

        // The verifier drives the selected ability explicitly. Registration
        // is needed by medical/wildlife authorities, not by the AI scheduler.
        if (created.Brain != null) created.Brain.enabled = false;
        if (created.BehaviorTree != null) created.BehaviorTree.enabled = false;
        return created;
    }

    private void TryAddFaultWall(Vector2Int position, string label)
    {
        if (grid == null || !grid.IsValidGridPos(position)) return;
        FaultWall wall = new(label + ":" + position, new[] { position });
        if (grid.RegisterOccupant(wall, GridLayer.Building, wall.Positions,
                connectPositions: false))
            faultWalls.Add(wall);
    }

    private void RemoveFaultWalls()
    {
        if (grid == null) return;
        foreach (FaultWall wall in faultWalls)
            grid.RemoveOccupant(wall, GridLayer.Building, wall.Positions,
                disconnectPositions: false);
        faultWalls.Clear();
    }

    private string FindStackableItemId() => items.GetAllStacks()
        .Where(stack => stack != null && stack.AvailableQuantity > 0
            && !stack.Forbidden && !string.IsNullOrWhiteSpace(stack.ItemId))
        .Select(stack => stack.ItemId).FirstOrDefault();

    private Vector2Int FindIsolatedItemSeedPosition()
    {
        Vector2Int fallback = actor != null ? actor.GetNowXY() : Vector2Int.zero;
        if (grid == null)
        {
            return fallback;
        }

        HashSet<Vector2Int> occupied = items.GetAllStacks()
            .Where(stack => stack != null)
            .Select(stack => stack.Position)
            .ToHashSet();
        Vector2Int? selected = grid.SearchPath(fallback)
            .GetReachablePositions()
            .Where(position => grid.IsWalkable(position) && !occupied.Contains(position))
            .OrderBy(position => Mathf.Abs(position.x - fallback.x)
                + Mathf.Abs(position.y - fallback.y))
            .Select(position => (Vector2Int?)position)
            .FirstOrDefault();
        return selected ?? fallback;
    }

    private WorldItemStackSnapshot FindConsumableStack() => items.GetAllStacks()
        .FirstOrDefault(stack => stack != null && stack.AvailableQuantity > 0
            && !stack.Forbidden && !string.IsNullOrWhiteSpace(stack.ItemId));

    private int TotalQuantity(string itemId) => items.GetAllStacks()
        .Where(stack => stack != null
            && string.Equals(stack.ItemId, itemId, StringComparison.Ordinal))
        .Sum(stack => stack.TotalQuantity);

    private int CountCarcasses() => items.GetAllStacks().Count(stack =>
        stack != null && WildlifeItemDefinitions.TryGetSpeciesIdFromCarcass(
            stack.ItemId, out _));

    private void PauseAi()
    {
        if (scheduler != null)
        {
            schedulerWasEnabled = scheduler.enabled;
            scheduler.enabled = false;
        }
        foreach (CharacterActor candidate in LiveActors())
        {
            if (candidate.Brain != null)
            {
                paused.Add(new PausedBehaviour(candidate.Brain, candidate.Brain.enabled));
                candidate.Brain.enabled = false;
            }
            if (candidate.BehaviorTree != null)
            {
                paused.Add(new PausedBehaviour(candidate.BehaviorTree,
                    candidate.BehaviorTree.enabled));
                candidate.BehaviorTree.enabled = false;
            }
        }
    }

    private void QuiesceLiveActionsForSnapshot()
    {
        foreach (CharacterActor candidate in LiveActors())
        {
            candidate.Brain?.StopAllAiForLifecycleTransition(
                "qa-cross-action-snapshot");
            candidate.GetComponent<AbilityShopping>()?.StopShopping(
                "qa-cross-action-snapshot");
            candidate.GetComponent<AbilityHaul>()?.StopHauling(
                "qa-cross-action-snapshot");
            candidate.GetComponent<AbilityRescue>()?.StopRescue(
                CharacterMedicalStatusCode.RescueInterrupted);
            candidate.GetComponent<AbilityHunt>()?.StopHunting(
                "qa-cross-action-snapshot");
            candidate.GetComponent<AbilityUseSubstance>()?.StopUse(
                "qa-cross-action-snapshot");
            candidate.GetComponent<AbilityMove>()?.CancelActiveMovement();
            aiScheduling?.Unregister(candidate);
        }
    }

    private static CharacterActor[] LiveActors() =>
        CharacterActorCollection.DistinctByGameObject(
            FindObjectsByType<CharacterActor>(FindObjectsInactive.Exclude,
                FindObjectsSortMode.None))
        .Where(value => value != null && !value.IsDead
            && value.CurrentLifecycleState == CharacterLifecycleState.Active)
        .ToArray();

    private static CharacterBodyPartHealthState ClonePart(
        CharacterBodyPartHealthState source) => new()
    {
        bodyPart = source.bodyPart,
        maxHealth = source.maxHealth,
        currentHealth = source.currentHealth,
        bleedingPerSecond = source.bleedingPerSecond
    };

    private void Check(bool condition, string detail)
    {
        string line = (condition ? "PASS\t" : "FAIL\t") + detail;
        evidence.Add(line);
        AppendProgress(line);
        if (!condition) failures.Add(detail);
    }

    private static void AppendProgress(string line)
    {
        try
        {
            File.AppendAllText(
                CharacterAiCrossActionFaultPlayModeVerifier.ReportPath,
                (line ?? string.Empty) + Environment.NewLine);
        }
        catch (Exception exception)
        {
            Debug.LogError("AI Cross-Action incremental report failed: "
                + exception.Message);
        }
    }

    private void Cleanup()
    {
        AbilityHaul.Ensure(actor)?.StopHauling("qa-cleanup");
        AbilityRescue.Ensure(actor)?.StopRescue(CharacterMedicalStatusCode.RescueInterrupted);
        AbilityHunt.Ensure(actor, wildlife)?.StopHunting("qa-cleanup");
        AbilityUseSubstance.Ensure(actor)?.StopUse("qa-cleanup");
        RemoveFaultWalls();
        foreach (CharacterActor temporaryActor in temporaryActors)
        {
            if (temporaryActor == null) continue;
            aiWorld?.UnregisterCharacter(temporaryActor);
            aiWorld?.UnregisterCharacterLifetime(temporaryActor);
        }
        temporaryActors.Clear();
        if (worldSnapshot != null && saveRegistry != null)
        {
            DungeonGameRestoreReport restoreReport = new();
            bool restored = saveRegistry.RestoreAll(worldSnapshot, restoreReport);
            if (!restored || !restoreReport.Success)
            {
                failures.Add("cleanup full-world restore failed: "
                    + string.Join(" | ", restoreReport.Errors));
            }
            else
            {
                Check(true,
                    "hunt-prey-despawn: wildlife restored through full V18 transaction boundary");
            }
        }
        foreach (PausedBehaviour state in paused) state.Restore();
        if (scheduler != null) scheduler.enabled = schedulerWasEnabled;
        for (int index = temporary.Count - 1; index >= 0; index--)
            if (temporary[index] != null) Destroy(temporary[index]);
    }

    private void WriteReport()
    {
        StringBuilder report = new();
        report.AppendLine("# AI Cross-Action Fault PlayMode Matrix");
        report.AppendLine("authority=production-runtime");
        report.AppendLine("rows=haul-source-despawn,haul-source-shrink,haul-destination-destroy,rescue-patient-despawn,rescue-save-load,rescue-patient-death,rescue-bed-destroy,rescue-medicine-loss,hunt-hunter-downed,hunt-hunter-dead,hunt-prey-despawn,hunt-path-invalidation,drink-item-loss,field-meal-item-loss,substance-item-loss");
        foreach (string line in evidence) report.AppendLine(line);
        report.AppendLine("lateCommit=" + (failures.Count == 0 ? "0" : "FAILED"));
        report.AppendLine("result=" + (failures.Count == 0 ? "PASS" : "FAIL"));
        if (failures.Count > 0) report.AppendLine("failures=" + string.Join(" | ", failures));
        File.WriteAllText(CharacterAiCrossActionFaultPlayModeVerifier.ReportPath,
            report.ToString());
        if (failures.Count == 0) Debug.Log("AI_CROSS_ACTION_FAULT_MATRIX_PASS");
        else Debug.LogError("AI_CROSS_ACTION_FAULT_MATRIX_FAIL: "
            + string.Join(" | ", failures));
    }

    private readonly struct PausedBehaviour
    {
        private readonly Behaviour behaviour;
        private readonly bool enabled;
        public PausedBehaviour(Behaviour behaviour, bool enabled)
        {
            this.behaviour = behaviour;
            this.enabled = enabled;
        }
        public void Restore()
        {
            if (behaviour != null) behaviour.enabled = enabled;
        }
    }

    private sealed class NoPathSearchBroker : IGridPathSearchBroker
    {
        public int SearchesThisFrame => 0;
        public int UrgentOverdraftSearchesThisFrame => 0;
        public int UnboundedSearchesThisFrame => 0;
        public int CacheHitsThisFrame => 0;
        public int BudgetDeferralsThisFrame => 0;
        public double SearchMillisecondsThisFrame => 0d;

        public void BeginFrame(
            int searchBudget,
            bool enforceBudget,
            double searchTimeBudgetMilliseconds = double.PositiveInfinity)
        {
        }

        public bool TryGetSearch(
            Grid grid,
            Vector2Int start,
            out GridPathSearchResult result,
            GridPathSearchPriority priority = GridPathSearchPriority.Normal,
            GridTraversalContext traversalContext = default)
        {
            result = null;
            return false;
        }

        public Queue<GridMoveStep> GetMovePath(
            Grid grid,
            Vector2Int start,
            Func<Vector2Int, bool> terminateEndCondition,
            GridPathSearchPriority priority = GridPathSearchPriority.Normal,
            GridTraversalContext traversalContext = default) => new();

        public Queue<GridMoveStep> GetMovePathTo(
            Grid grid,
            Vector2Int start,
            Vector2Int destination,
            GridPathSearchPriority priority = GridPathSearchPriority.Normal,
            GridTraversalContext traversalContext = default) => new();

        public GridPathRequestStatus RequestMovePathTo(
            Grid grid,
            Vector2Int start,
            Vector2Int destination,
            out Queue<GridMoveStep> path,
            GridPathSearchPriority priority = GridPathSearchPriority.Normal,
            GridTraversalContext traversalContext = default)
        {
            path = new Queue<GridMoveStep>();
            return GridPathRequestStatus.Unreachable;
        }

        public void Clear()
        {
        }
    }

    private sealed class FaultWall : IGridBuildingOccupantCapability
    {
        public FaultWall(string label, IReadOnlyList<Vector2Int> positions)
        {
            Label = label;
            Positions = positions?.ToArray() ?? Array.Empty<Vector2Int>();
        }
        public string Label { get; }
        public IReadOnlyList<Vector2Int> Positions { get; }
        public int GridId => Label.GetHashCode();
        public bool IsGridDestroyed => false;
        public bool IsGridVisitable => false;
        public bool IsGridMovement => false;
        public bool BlocksGridMovement => true;
        public bool AllowsInteriorWalkability => false;
    }

    private sealed class DependencyMarkerSection :
        DungeonDebugStagedSaveSection,
        IDungeonRollbackFreeSaveSection
    {
        private readonly string id;

        public DependencyMarkerSection(string id)
        {
            this.id = id ?? throw new ArgumentNullException(nameof(id));
        }

        public override string SectionId => id;
        public override DungeonSaveRestorePhase RestorePhase =>
            DungeonSaveRestorePhase.RuntimeState;

        protected override void CommitMarker(DungeonGameRestoreReport report)
        {
        }
    }
}
