#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VContainer;
using VContainer.Unity;

/// <summary>Bounded live coverage for warden progress and escape/recapture cleanup.</summary>
public static class CaptivityAiPlayModeVerifier
{
    public const string ReportPath = "Artifacts/QA/captivity-ai-playmode.txt";
    private const string PendingPath = "Temp/captivity-ai-playmode.flag";

    [MenuItem("DungeonStory/Debug/QA/Run Captivity AI PlayMode Verification")]
    public static void RequestRun()
    {
        if (EditorApplication.isPlaying) { StartRunner(); return; }
        Directory.CreateDirectory("Temp");
        File.WriteAllText(PendingPath, DateTime.UtcNow.ToString("O"));
        EditorApplication.EnterPlaymode();
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (!File.Exists(PendingPath)) return;
        File.Delete(PendingPath);
        StartRunner();
    }

    private static void StartRunner()
    {
        if (UnityEngine.Object.FindFirstObjectByType<CaptivityAiPlayModeRunner>() == null)
            new GameObject("Captivity AI PlayMode Runner").AddComponent<CaptivityAiPlayModeRunner>();
    }
}

public sealed class CaptivityAiPlayModeRunner : MonoBehaviour
{
    private readonly List<string> checks = new List<string>();
    private readonly List<string> failures = new List<string>();
    private ICaptivityRuntime runtime;
    private ICaptivityPersistence persistence;
    private ICaptivityCommandService commands;
    private ICaptivityWorkReadinessQuery workReadiness;
    private ICaptivityEscapeRuntime escape;
    private IDungeonRestoreTransactionParticipant restoreParticipant;
    private IDungeonSaveSectionRegistry saveRegistry;
    private ICharacterAiWorldRegistry worldRegistry;
    private WorldItemRepository itemRepository;
    private string fixtureInteractionInputStackId = string.Empty;
    private List<DungeonSaveSectionEnvelope> originalWorld;
    private readonly List<BuildableObject> fixtureBuildings = new List<BuildableObject>();
    private readonly List<DisplacedMovementSnapshot> displacedMovementBuildings = new List<DisplacedMovementSnapshot>();
    private readonly List<FixtureAreaSnapshot> fixtureAreaSnapshots = new List<FixtureAreaSnapshot>();
    private Grid fixtureGrid;
    private Door fixtureCellDoor;
    private RoomInstance fixtureRoom;
    private string activeCaptiveId = string.Empty;
    private string fixtureStage = "not-started";
    private string placementFailureDetail = string.Empty;
    private CharacterActor captive;
    private CharacterActor warden;
    private CharacterType oldType;
    private CharacterLifecycleState oldLifecycle;
    private bool oldPaused;
    private AbilityWork work;
    private AbilityWork.DutyState oldDuty;
    private WorkPriorityLevel oldPriority;
    private CharacterCarryInventory inventory;
    private bool addedRestraint;
    private int baselineRestraintCount;
    private bool baselineRestraintCountCaptured;
    private bool baselinePromoted;
    private float oldTimeScale;

    private IEnumerator Start()
    {
        oldTimeScale = Time.timeScale;
        Time.timeScale = 8f;
        yield return RunGuarded();
        Cleanup();
        WriteReport();
        Time.timeScale = oldTimeScale;
        Destroy(gameObject);
        EditorApplication.delayCall += () => { if (EditorApplication.isPlaying) EditorApplication.isPlaying = false; };
    }

    private IEnumerator RunGuarded()
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        DungeonRuntimeLifetimeScope scope = null;
        while (Time.realtimeSinceStartup < deadline)
        {
            scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>(FindObjectsInactive.Include);
            if (scope?.Container != null) break;
            yield return null;
        }
        if (scope?.Container == null) { Fail("SETUP", "runtime scope unavailable"); yield break; }

        IEnumerator scenario = null;
        try
        {
            scenario = RunScenario(scope);
        }
        catch (Exception exception)
        {
            Fail("EXCEPTION", exception.ToString());
        }
        if (scenario == null) yield break;

        while (true)
        {
            object current;
            bool moved;
            bool failed = false;
            try
            {
                moved = scenario.MoveNext();
                current = moved ? scenario.Current : null;
            }
            catch (Exception exception)
            {
                Fail("EXCEPTION", exception.ToString());
                moved = false;
                current = null;
                failed = true;
            }
            if (failed) yield break;
            if (!moved) yield break;
            yield return current;
        }
    }

    private IEnumerator RunScenario(DungeonRuntimeLifetimeScope scope)
    {
            persistence = scope.Container.Resolve<ICaptivityPersistence>();
            runtime = persistence as ICaptivityRuntime;
            commands = persistence as ICaptivityCommandService;
            workReadiness = persistence as ICaptivityWorkReadinessQuery;
            escape = persistence as ICaptivityEscapeRuntime;
            restoreParticipant = persistence as IDungeonRestoreTransactionParticipant;
            if (runtime == null || commands == null || workReadiness == null || escape == null
                || restoreParticipant == null)
            {
                Fail("CAPTIVITY_AUTHORITY", "registered captivity interfaces do not share one production authority");
                yield break;
            }
            ICharacterAiWorldRegistry world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
            worldRegistry = world;
            saveRegistry = scope.Container.Resolve<IDungeonSaveSectionRegistry>();
            itemRepository = scope.Container.Resolve<WorldItemRepository>();
            CharacterActor[] actors = world.Characters.Where(value => value != null && !value.IsDead).ToArray();
            if (actors.Length < 2)
            {
                string promotion = StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
                baselinePromoted = true;
                checks.Add("INFO\tBASELINE_PROMOTION\t" + promotion);
                for (int frame = 0; frame < 8; frame++) yield return null;
                actors = world.Characters.Where(value => value != null && !value.IsDead).ToArray();
            }
            int ownerCount = actors.Count(value => value.Role == CharacterRole.Owner);
            Check(actors.Length >= 3 && ownerCount == 1,
                "STARTED_PARTY_BASELINE",
                $"promoted={baselinePromoted}; actors={actors.Length}; owners={ownerCount}");
            if (actors.Length < 3 || ownerCount != 1) yield break;
            originalWorld = saveRegistry.CaptureAll();
            BuildableObject housing = CreateAuthoredHousingFixture(scope, world);
            if (actors.Length < 2 || housing == null)
            {
                Fail("FIXTURE", $"actors={actors.Length}; housing={housing != null}; stage={fixtureStage}");
                yield break;
            }
            checks.Add("INFO\tFIXTURE_STAGE\t" + fixtureStage);
            warden = actors.FirstOrDefault(value => value.characterType == CharacterType.NPC);
            captive = actors.FirstOrDefault(value => value != warden && value.characterType == CharacterType.NPC);
            if (warden == null || captive == null)
            {
                Fail("FIXTURE_ACTORS", $"npcActors={actors.Count(value => value.characterType == CharacterType.NPC)}");
                yield break;
            }
            if (!TryPlaceFixtureActors(housing, warden, captive, out string placementReason))
            {
                Fail("FIXTURE_ACTOR_PLACEMENT", placementReason);
                yield break;
            }
            Check(true,
                "FIXTURE_ACTOR_PLACEMENT",
                $"warden={warden.GetNowXY()}; captive={captive.GetNowXY()}; room={fixtureRoom?.Id}");
            oldType = captive.characterType; oldLifecycle = captive.CurrentLifecycleState; oldPaused = captive.IsAiPaused();
            captive.characterType = CharacterType.Intruder;
            captive.SetLifecycleState(CharacterLifecycleState.Downed);
            captive.SetAiPaused(true);
            IDoorAccessCommandService doorCommands = scope.Container.Resolve<IDoorAccessCommandService>();
            IDoorAccessQuery doorQuery = scope.Container.Resolve<IDoorAccessQuery>();
            bool cellPolicyApplied = fixtureCellDoor != null
                && doorCommands.ApplyPreset(fixtureCellDoor, DoorAccessPreset.Cell);
            Check(cellPolicyApplied,
                "CELL_DOOR_POLICY",
                $"door={fixtureCellDoor != null}; preset={DoorAccessPreset.Cell}");
            if (!cellPolicyApplied) yield break;

            GridTraversalContext captiveDoorContext = GridTraversalContext.ForCharacter(
                CharacterPersistentIdentity.Require(captive));
            bool captiveCanUseDoor = doorQuery.CanUse(
                fixtureCellDoor,
                captiveDoorContext,
                out string captiveDoorReason);
            DoorAccessSubjectRef captiveDoorSubject = doorQuery.ResolveSubject(captiveDoorContext);
            Check(!captiveCanUseDoor,
                "CELL_DOOR_CAPTIVE_DENIED",
                $"group={captiveDoorSubject.Group}; reason={captiveDoorReason}");

            GridTraversalContext staffDoorContext = GridTraversalContext.ForCharacter(
                CharacterPersistentIdentity.Require(warden));
            bool staffCanUseDoor = doorQuery.CanUse(
                fixtureCellDoor,
                staffDoorContext,
                out string staffDoorReason);
            DoorAccessSubjectRef staffDoorSubject = doorQuery.ResolveSubject(staffDoorContext);
            Check(staffCanUseDoor,
                "CELL_DOOR_STAFF_ALLOWED",
                $"group={staffDoorSubject.Group}; reason={staffDoorReason}");
            if (captiveCanUseDoor || !staffCanUseDoor) yield break;

            inventory = CharacterCarryInventory.Ensure(warden);
            baselineRestraintCount = inventory.CountItem(CaptivityItemDefinitions.RestraintsItemId);
            baselineRestraintCountCaptured = true;
            IDungeonItemCatalogProvider catalog = scope.Container.Resolve<IDungeonItemCatalogProvider>();
            IItemHaulingSettingsProvider hauling = scope.Container.Resolve<IItemHaulingSettingsProvider>();
            string reason;
            addedRestraint = inventory.TryAdd("coverage:initial-restraint", CaptivityItemDefinitions.RestraintsItemId,
                1, catalog, hauling, out reason);
            Check(addedRestraint, "CAPTURE_RESTRAINT", reason);
            bool captureStarted = addedRestraint && commands.TryOrderCapture(captive, warden, out reason);
            Check(captureStarted, "CAPTURE_COMMAND", reason);
            string captiveId = CharacterPersistentIdentity.Require(captive).Value;
            activeCaptiveId = captiveId;
            float captureDeadline = Time.realtimeSinceStartup + 25f;
            while (captureStarted && Time.realtimeSinceStartup < captureDeadline)
            {
                runtime.TryGetCaptive(captiveId, out CaptiveState state);
                if (state?.status == CaptivityStatus.Confined) break;
                yield return null;
            }
            runtime.TryGetCaptive(captiveId, out CaptiveState captured);
            AbilityCaptiveEscort escortAbility = warden.GetComponent<AbilityCaptiveEscort>();
            Check(captured?.status == CaptivityStatus.Confined,
                "CAPTURE_TERMINAL",
                $"status={captured?.status}; lastResult={captured?.lastResult}; wardenPos={warden.GetNowXY()}; captivePos={captive.GetNowXY()}; housingPos={captured?.housingPosition}; escorting={escortAbility?.IsEscorting == true}");
            if (captured?.status != CaptivityStatus.Confined) yield break;

            // The official start stock is at the opposite end of the tiny QA grid
            // and is deliberately disconnected from this isolated authored cell.
            // Supply one real, quantity-tracked food stack on the reachable side so
            // the scenario verifies the production reserve -> pickup -> carry ->
            // facility-buffer path instead of failing for an unrelated fixture gap.
            fixtureInteractionInputStackId = WorldItemRepositoryEditorAccess.AddStack(
                itemRepository,
                "food:preserved-ration",
                1,
                WorldItemStackState.Loose,
                position: warden.GetNowXY());
            Check(!string.IsNullOrWhiteSpace(fixtureInteractionInputStackId),
                "WARDEN_INPUT_SOURCE",
                $"stack={fixtureInteractionInputStackId}; pos={warden.GetNowXY()}");
            if (string.IsNullOrWhiteSpace(fixtureInteractionInputStackId)) yield break;

            if (!warden.TryGetAbility(out work))
            {
                Fail("WARDEN_ABILITY", "live warden has no AbilityWork");
                yield break;
            }
            oldDuty = work.CurrentDutyState;
            oldPriority = work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Warden);
            work.SetDutyState(AbilityWork.DutyState.OnDuty);
            work.SetWorkPriority(BuiltInWorkTypeIds.Warden, WorkPriorityLevel.Priority1);
            bool started = commands.TryStartInteraction(
                captiveId,
                "captivity:persuasion", warden, housing, out reason);
            Check(started, "WARDEN_START", reason);
            if (!started) yield break;
            CharacterAiDecisionTickResult lastDeliveryDecision = default;
            int deliveryDecisionAttempts = 0;
            int deliveryFrames = 0;
            float deliveryDeadline = Time.realtimeSinceStartup + 15f;
            while (Time.realtimeSinceStartup < deliveryDeadline
                   && !workReadiness.IsInteractionReady(captiveId, out _))
            {
                // The production scheduler owns a running action. Re-entering the
                // decision tree every frame competes with the haul coroutine for the
                // shared path-search budget and can manufacture a permanent Deferred
                // result that never occurs in normal play. Only bootstrap a new
                // decision when no action currently owns the actor.
                if ((warden.Brain?.bestAction == null
                     || warden.Brain.isBestActionEnd)
                    && (deliveryFrames & 7) == 0)
                {
                    lastDeliveryDecision = warden.Brain.RunDecisionTreeDirect();
                    deliveryDecisionAttempts++;
                }
                deliveryFrames++;
                yield return null;
            }
            bool interactionReady = workReadiness.IsInteractionReady(
                captiveId,
                out string readinessReason);
            AbilityHaul wardenHaul = warden.GetComponent<AbilityHaul>();
            string carrySummary = string.Join(",", inventory.Items
                .Where(item => item != null && item.quantity > 0)
                .Select(item =>
                    $"{item.itemId}x{item.quantity}@{item.ownerOperationId}"));
            Check(interactionReady,
                "WARDEN_INPUT_DELIVERY",
                $"frames={deliveryFrames}; attempts={deliveryDecisionAttempts}; decision={lastDeliveryDecision.Status}; "
                + $"reason={readinessReason}; action={warden.Brain?.bestAction?.actionset?.GetType().Name}; "
                + $"phase={warden.Brain?.CurrentActionPhase}; brainFailure={warden.Brain?.LastActionFailure}; "
                + $"haulStage={wardenHaul?.CurrentExecutionStage}; haulFailure={wardenHaul?.LastFailureReason}; "
                + $"haulPlan={wardenHaul?.CurrentPlanSummary}; carry={carrySummary}");
            if (!interactionReady) yield break;
            bool preferredWardenWork = warden.Brain?.PreferWorkActionOnNextDecision(
                BuiltInWorkTypeIds.Warden,
                120f) == true;
            Check(preferredWardenWork,
                "WARDEN_AI_ASSIGNMENT",
                $"brain={warden.Brain != null}; priority={work.WorkPriorities.GetPriority(BuiltInWorkTypeIds.Warden)}");
            CharacterAiDecisionTickResult lastWardenDecision = default;
            int wardenDecisionAttempts = 0;
            float wardenStartDeadline = Time.realtimeSinceStartup + 5f;
            while (preferredWardenWork
                   && Time.realtimeSinceStartup < wardenStartDeadline
                   && !(work.IsAssignedWork(BuiltInWorkTypeIds.Warden)
                        && warden.Brain?.HasRunningWorkAction == true))
            {
                lastWardenDecision = warden.Brain.RunDecisionTreeDirect();
                wardenDecisionAttempts++;
                yield return null;
            }
            bool wardenAiStarted = work.IsAssignedWork(BuiltInWorkTypeIds.Warden)
                && warden.Brain?.HasRunningWorkAction == true;
            Check(wardenAiStarted,
                "WARDEN_AI_START",
                $"attempts={wardenDecisionAttempts}; decision={lastWardenDecision.Status}; "
                + $"action={warden.Brain?.bestAction?.actionset?.GetType().Name}; "
                + $"phase={warden.Brain?.CurrentActionPhase}; "
                + $"failure={warden.Brain?.LastActionFailure}; assigned={work.AssignedWorkTypeId}; "
                + $"actorGrid={warden.GetNowXY()}; actorWorld={warden.transform.position}; "
                + $"target={warden.Brain?.bestAction?.destination?.centerPos}; "
                + $"targetCells={string.Join(",", warden.Brain?.bestAction?.destination?.buildPoses ?? new List<Vector2Int>())}; "
                + $"plan={warden.Brain?.bestAction?.planKind}; "
                + $"steps={warden.Brain?.bestAction?.pathSteps?.Count ?? -1}");
            if (!wardenAiStarted) yield break;
            float progressDeadline = Time.realtimeSinceStartup + 25f;
            bool progressed = false;
            while (Time.realtimeSinceStartup < progressDeadline)
            {
                runtime.TryGetCaptive(CharacterPersistentIdentity.Require(captive).Value, out CaptiveState state);
                progressed |= state != null && state.completedInteractionWork > 0f;
                if (state?.status == CaptivityStatus.Confined) break;
                yield return null;
            }
            runtime.TryGetCaptive(CharacterPersistentIdentity.Require(captive).Value, out CaptiveState afterWork);
            AbilityMove wardenMove = warden.GetComponent<AbilityMove>();
            string wardenRuntimeDetail =
                $"work={afterWork?.completedInteractionWork:0.##}; required={afterWork?.requiredInteractionWork:0.##}; "
                + $"materialsConsumed={afterWork?.interactionMaterialsConsumed}; "
                + $"isWorking={work.isWorking}; activeRoutine={work.HasActiveWorkRoutineForDiagnostics}; "
                + $"assigned={work.AssignedWorkTypeId}@{work.assignedShop?.centerPos}; "
                + $"actorGrid={warden.GetNowXY()}; actorWorld={warden.transform.position}; "
                + $"phase={warden.Brain?.CurrentActionPhase}; detail={warden.Brain?.CurrentActionPhaseDetail}; "
                + $"decisionPending={warden.Brain?.isBestActionEnd}; "
                + $"movement={wardenMove?.HasActiveMovementRoutineForDiagnostics}; "
                + $"moveFailure={wardenMove?.LastGridMoveFailureReason}";
            Check(progressed, "WARDEN_PROGRESS", wardenRuntimeDetail);
            Check(afterWork?.status == CaptivityStatus.Confined,
                "WARDEN_TERMINAL",
                $"status={afterWork?.status}; {wardenRuntimeDetail}");

            escape.FailEscape(captiveId, captive, "coverage injected path failure");
            runtime.TryGetCaptive(captiveId, out CaptiveState failedEscape);
            Check(failedEscape?.status == CaptivityStatus.Confined && failedEscape.failedEscapeAttempts == 1,
                "ESCAPE_FAILURE_TERMINAL", $"status={failedEscape?.status}; attempts={failedEscape?.failedEscapeAttempts}");

            Check(commands.TryRelease(captiveId, out reason), "ESCAPE_RELEASE_TRANSITION", reason);
            captive.characterType = CharacterType.Intruder;
            captive.SetLifecycleState(CharacterLifecycleState.Downed);
            captive.SetAiPaused(true);
            addedRestraint = inventory.TryAdd("coverage:restraint", CaptivityItemDefinitions.RestraintsItemId,
                1, catalog, hauling, out reason);
            Check(addedRestraint, "RECAPTURE_RESTRAINT", reason);
            bool recapture = addedRestraint && commands.TryOrderCapture(captive, warden, out reason);
            Check(recapture, "RECAPTURE_RESERVATION", reason);
            runtime.TryGetCaptive(captiveId, out CaptiveState recaptureState);
            Check(recaptureState?.status is CaptivityStatus.Stabilizing or CaptivityStatus.AwaitingEscort or CaptivityStatus.Escorting,
                "RECAPTURE_LIVE",
                $"status={recaptureState?.status}; lastResult={recaptureState?.lastResult}; "
                + $"wardenAction={warden.Brain?.bestAction?.actionset?.Branch}; "
                + $"escort={escortAbility?.IsEscorting == true}");
            Check(!recapture || commands.CancelCapture(captiveId, "coverage cleanup"), "RECAPTURE_CANCEL_TERMINAL", "cancel releases active capture");
    }

    private BuildableObject CreateAuthoredHousingFixture(
        DungeonRuntimeLifetimeScope scope,
        ICharacterAiWorldRegistry world)
    {
        BuildableObject existing = world.Buildings.FirstOrDefault(value => value != null
            && !value.isDestroy && value.BuildingData.GetCaptiveHousingAbility()?.IsValid == true);
        if (existing != null)
        {
            fixtureStage = "existing-authored-housing";
            return existing;
        }
        if (!world.TryGetGrid(out Grid grid) || grid == null)
        {
            fixtureStage = "grid-unavailable";
            return null;
        }
        fixtureGrid = grid;

        BuildingSO hallway = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/Resources/SO/Building/Hallway.asset");
        BuildingSO door = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/Resources/SO/Building/InteriorDoor.asset");
        BuildingSO wall = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/Resources/SO/Building/Wall.asset");
        BuildingSO housing = AssetDatabase.FindAssets(
                "CP01 t:BuildingSO",
                new[] { "Assets/Resources/SO/Building/Captivity" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .FirstOrDefault(value => value != null
                && value.GetCaptiveHousingAbility()?.IsValid == true);
        if (hallway == null || door == null || !door.IsInteriorDoor
            || door.Placement.Width != 1 || wall == null || housing == null)
        {
            fixtureStage = $"asset-invalid:hallway={hallway != null},door={door != null},interiorDoor={door?.IsInteriorDoor == true},doorWidth={door?.Placement.Width},wall={wall != null},housing={housing != null}";
            return null;
        }

        if (grid.height < 3)
        {
            fixtureStage = $"grid-too-short:height={grid.height}";
            return null;
        }

        int start = -1;
        HashSet<BuildableObject> selectedDisplacements = null;
        string lastRejectedCell = "none";
        for (int x = 1; x <= grid.width - 8 && start < 0; x++)
        {
            bool free = true;
            bool hasExteriorCell = false;
            HashSet<BuildableObject> candidates = new HashSet<BuildableObject>();
            for (int y = 0; y < 3 && free; y++)
            {
                for (int offset = 0; offset < 7; offset++)
                {
                    Vector2Int cell = new Vector2Int(x + offset, y);
                    if (!grid.IsValidGridPos(cell))
                    {
                        free = false;
                        break;
                    }

                    GridCell gridCell = grid.GetGridCell(cell);
                    if (gridCell == null)
                    {
                        free = false;
                        lastRejectedCell = $"missing:{cell}";
                        break;
                    }
                    hasExteriorCell |= gridCell.AreaType != GridCellAreaType.DungeonInterior;

                    foreach (GridLayer layer in Enum.GetValues(typeof(GridLayer)))
                    {
                        IGridOccupant occupant = gridCell.GetOccupant(layer);
                        if (occupant == null) continue;
                        if ((layer != GridLayer.Building && layer != GridLayer.Hallway)
                            || occupant is not BuildableObject movement
                            || movement is Facility
                            || movement is Door
                            || movement.Facility != null
                            || !movement.IsGridMovement
                            || movement.BlocksGridMovement)
                        {
                            free = false;
                            lastRejectedCell = $"hard-occupant:{cell}:layer={layer}:type={occupant.GetType().Name}";
                            break;
                        }
                        candidates.Add(movement);
                    }
                }
            }
            if (free && hasExteriorCell)
            {
                start = x;
                selectedDisplacements = candidates;
            }
        }
        if (start < 0)
        {
            fixtureStage = "no-seven-cell-safe-exterior-span:last=" + lastRejectedCell;
            return null;
        }

        for (int y = 0; y < 3; y++)
        {
            for (int offset = 0; offset < 7; offset++)
            {
                Vector2Int position = new Vector2Int(start + offset, y);
                GridCell cell = grid.GetGridCell(position);
                fixtureAreaSnapshots.Add(new FixtureAreaSnapshot(position, cell.AreaType));
                grid.SetAreaType(position, GridCellAreaType.DungeonInterior);
                if (cell.AreaType != GridCellAreaType.DungeonInterior)
                {
                    fixtureStage = $"area-mutation-failed:{position}:actual={cell.AreaType}";
                    return null;
                }
            }
        }

        foreach (BuildableObject movement in selectedDisplacements
            .OrderBy(value => value.centerPos.y)
            .ThenBy(value => value.centerPos.x)
            .ThenBy(value => value.GridId))
        {
            GridLayer layer = movement.BuildingData.Placement.Layer;
            Vector2Int[] positions = movement.buildPoses.ToArray();
            if (!grid.RemoveOccupant(
                    movement,
                    layer,
                    positions,
                    movement.BuildingData.Placement.IsMovement))
            {
                fixtureStage = $"movement-displacement-failed:{movement.GridId}:layer={layer}";
                return null;
            }
            bool removedExactly = positions.All(position =>
                !grid.GetGridCell(position).ContainsOccupant(layer, movement));
            if (!removedExactly)
            {
                fixtureStage = $"movement-displacement-verification-failed:{movement.GridId}:layer={layer}";
                return null;
            }
            displacedMovementBuildings.Add(new DisplacedMovementSnapshot(
                movement,
                layer,
                positions,
                movement.BuildingData.Placement.IsMovement));
        }

        GridBuildingFactory factory = new GridBuildingFactory(
            created => scope.Container.InjectGameObject(created.gameObject));
        for (int offset = 0; offset < 7; offset++)
        {
            Vector2Int bottom = new Vector2Int(start + offset, 2);
            if (PlaceFixture(factory, grid, world, wall, bottom) == null)
            {
                fixtureStage = $"perimeter-wall-placement-failed:{bottom};{placementFailureDetail}";
                return null;
            }

            Vector2Int top = new Vector2Int(start + offset, 0);
            if (PlaceFixture(factory, grid, world, wall, top) == null)
            {
                fixtureStage = $"perimeter-wall-placement-failed:{top};{placementFailureDetail}";
                return null;
            }
        }

        Vector2Int doorCell = new Vector2Int(start, 1);
        Vector2Int endpointWallCell = new Vector2Int(start + 6, 1);
        BuildableObject placedDoor = PlaceFixture(factory, grid, world, door, doorCell);
        fixtureCellDoor = placedDoor as Door;
        if (fixtureCellDoor == null)
        {
            fixtureStage = $"perimeter-door-placement-failed:{doorCell};{placementFailureDetail}";
            return null;
        }
        if (PlaceFixture(factory, grid, world, wall, endpointWallCell) == null)
        {
            fixtureStage = $"endpoint-wall-placement-failed:{endpointWallCell};{placementFailureDetail}";
            return null;
        }

        for (int offset = 1; offset < 6; offset++)
        {
            Vector2Int interior = new Vector2Int(start + offset, 1);
            if (grid.GetGridCell(interior)?.GetOccupant(GridLayer.Hallway) == null
                && PlaceFixture(factory, grid, world, hallway, interior) == null)
            {
                fixtureStage = $"interior-hallway-placement-failed:{interior};{placementFailureDetail}";
                return null;
            }
        }

        BuildableObject result = PlaceFixture(
            factory, grid, world, housing, new Vector2Int(start + 3, 1));
        IRoomLayoutCache rooms = scope.Container.Resolve<IRoomLayoutCache>();
        rooms.Clear();
        if (result == null)
        {
            fixtureStage = "housing-placement-failed;" + placementFailureDetail;
            return null;
        }
        if (!rooms.TryGetRoom(result, out RoomInstance room))
        {
            fixtureStage = "housing-room-not-found";
            return null;
        }
        if (!room.IsUsable)
        {
            fixtureStage = $"housing-room-not-usable:cells={room.Cells.Count};doors={room.Doors.Count};walls={room.Walls.Count};openBoundary={room.OpenBoundaryCount};solidBoundary={room.SolidBoundaryCount};closed={room.IsClosed};hasDoor={room.HasDoor}";
            return null;
        }
        fixtureRoom = room;
        fixtureStage = $"created-authored-housing:bounds={start},0..{start + 6},2;areaSnapshots={fixtureAreaSnapshots.Count};displaced={displacedMovementBuildings.Count}";
        return result;
    }

    private bool TryPlaceFixtureActors(
        BuildableObject housing,
        CharacterActor staff,
        CharacterActor subject,
        out string reason)
    {
        reason = string.Empty;
        if (fixtureGrid == null || fixtureRoom == null || housing == null
            || staff == null || subject == null)
        {
            reason = "fixture grid, room, housing, staff, or subject is unavailable";
            return false;
        }

        HashSet<Vector2Int> housingFootprint = new HashSet<Vector2Int>(
            housing.BuildingData.GetGridPosList(housing.centerPos));
        Vector2Int[] cells = fixtureRoom.Cells
            .Where(position => !housingFootprint.Contains(position))
            .Where(position => fixtureGrid.IsValidGridPos(position))
            .Where(position => fixtureGrid.IsWalkable(position))
            .Where(position =>
                fixtureGrid.GetGridCell(position)?.GetOccupant(GridLayer.Character) == null
                && fixtureGrid.GetGridCell(position)?.GetOccupant(GridLayer.DownedCharacter) == null)
            .OrderBy(position => position.y)
            .ThenBy(position => position.x)
            .ToArray();
        if (cells.Length < 2)
        {
            reason = $"fewer than two distinct walkable room cells; roomCells={fixtureRoom.Cells.Count}; eligible={cells.Length}; housing={string.Join(",", housingFootprint)}";
            return false;
        }

        Vector2Int staffCell = cells[0];
        Vector2Int subjectCell = cells[cells.Length - 1];
        if (staffCell == subjectCell)
        {
            reason = $"fixture actor cells are not distinct: {staffCell}";
            return false;
        }

        staff.transform.position = fixtureGrid.GetWorldPos(staffCell);
        subject.transform.position = fixtureGrid.GetWorldPos(subjectCell);
        staff.Brain?.ClearPathSearchCache();
        subject.Brain?.ClearPathSearchCache();
        staff.Brain?.RequestImmediateReplan(clearFailures: true);
        subject.Brain?.RequestImmediateReplan(clearFailures: true);
        if (staff.GetNowXY() != staffCell || subject.GetNowXY() != subjectCell)
        {
            reason = $"actor placement verification failed: staff={staff.GetNowXY()}/{staffCell}; subject={subject.GetNowXY()}/{subjectCell}";
            return false;
        }

        return true;
    }

    private BuildableObject PlaceFixture(
        GridBuildingFactory factory,
        Grid grid,
        ICharacterAiWorldRegistry world,
        BuildingSO data,
        Vector2Int position)
    {
        placementFailureDetail = string.Empty;
        IReadOnlyList<Vector2Int> positions = data.GetGridPosList(position);
        for (int index = 0; index < positions.Count; index++)
        {
            Vector2Int footprintCell = positions[index];
            GridCell cell = grid.GetGridCell(footprintCell);
            if (cell != null && cell.CanOccupy(data.Placement.Layer)) continue;

            IGridOccupant occupant = cell?.GetOccupant(data.Placement.Layer);
            placementFailureDetail = $"precheck:data={data.name},anchor={position},cell={footprintCell},layer={data.Placement.Layer},exists={cell != null},canOccupy={cell?.CanOccupy(data.Placement.Layer) == true},area={cell?.AreaType},occupant={occupant?.GetType().Name ?? "none"}";
            return null;
        }

        BuildableObject building = factory.Create(grid, data, position);
        if (building == null)
        {
            placementFailureDetail = $"factory-null:data={data.name},anchor={position}";
            return null;
        }
        building.SetGrid(grid);
        building.Initialization(data, position);
        if (!grid.RegisterOccupant(
                building,
                data.Placement.Layer,
                positions,
                data.Placement.IsMovement))
        {
            placementFailureDetail = $"register-failed:data={data.name},anchor={position},cells={string.Join(",", positions)}";
            Destroy(building.gameObject);
            return null;
        }
        world.RegisterBuilding(building);
        fixtureBuildings.Add(building);
        return building;
    }

    private void Cleanup()
    {
        if (itemRepository != null
            && !string.IsNullOrWhiteSpace(fixtureInteractionInputStackId))
        {
            WorldItemRepositoryEditorAccess.TryRemoveStack(
                itemRepository,
                fixtureInteractionInputStackId);
            fixtureInteractionInputStackId = string.Empty;
        }

        if (commands != null
            && runtime != null
            && !string.IsNullOrWhiteSpace(activeCaptiveId)
            && runtime.TryGetCaptive(activeCaptiveId, out CaptiveState captureState)
            && captureState?.status is CaptivityStatus.AwaitingCapture
                or CaptivityStatus.Stabilizing
                or CaptivityStatus.AwaitingEscort
                or CaptivityStatus.Escorting)
        {
            commands.CancelCapture(activeCaptiveId, "coverage cleanup before baseline restore");
        }
        AbilityCaptiveEscort liveEscort = warden != null
            ? warden.GetComponent<AbilityCaptiveEscort>()
            : null;
        if (liveEscort?.IsEscorting == true)
        {
            liveEscort.StopEscort("coverage cleanup after capture cancellation");
        }

        if (baselineRestraintCountCaptured && inventory != null)
        {
            int current = inventory.CountItem(CaptivityItemDefinitions.RestraintsItemId);
            int excess = Mathf.Max(0, current - baselineRestraintCount);
            bool removed = excess == 0
                || inventory.TryConsumeItem(CaptivityItemDefinitions.RestraintsItemId, excess);
            int final = inventory.CountItem(CaptivityItemDefinitions.RestraintsItemId);
            Check(removed && final == baselineRestraintCount,
                "FIXTURE_RESTRAINT_RESTORE",
                $"baseline={baselineRestraintCount}; before={current}; excess={excess}; final={final}");
        }

        if (work != null) { work.SetDutyState(oldDuty); work.SetWorkPriority(BuiltInWorkTypeIds.Warden, oldPriority); }
        if (captive != null) { captive.characterType = oldType; captive.SetLifecycleState(oldLifecycle); captive.SetAiPaused(oldPaused); }

        for (int index = fixtureBuildings.Count - 1; index >= 0; index--)
        {
            BuildableObject building = fixtureBuildings[index];
            if (building == null) continue;
            worldRegistry?.UnregisterBuilding(building);
            building.Grid?.RemoveOccupant(
                building,
                building.BuildingData.Placement.Layer,
                building.BuildingData.GetGridPosList(building.centerPos),
                building.BuildingData.Placement.IsMovement);
            Destroy(building.gameObject);
        }
        fixtureBuildings.Clear();

        bool occupantsRestored = true;
        for (int index = 0; index < displacedMovementBuildings.Count; index++)
        {
            DisplacedMovementSnapshot displaced = displacedMovementBuildings[index];
            bool registered = displaced.Building != null
                && fixtureGrid != null
                && fixtureGrid.RegisterOccupant(
                    displaced.Building,
                    displaced.Layer,
                    displaced.Positions,
                    displaced.ConnectPositions);
            bool exact = registered && displaced.Positions.All(position =>
                fixtureGrid.GetGridCell(position)?.ContainsOccupant(
                    displaced.Layer,
                    displaced.Building) == true);
            occupantsRestored &= exact;
        }
        Check(occupantsRestored,
            "FIXTURE_MOVEMENT_RESTORE",
            $"count={displacedMovementBuildings.Count}; exact={occupantsRestored}");

        bool areasRestored = true;
        for (int index = 0; index < fixtureAreaSnapshots.Count; index++)
        {
            FixtureAreaSnapshot snapshot = fixtureAreaSnapshots[index];
            GridCell cell = fixtureGrid?.GetGridCell(snapshot.Position);
            if (cell == null)
            {
                areasRestored = false;
                continue;
            }
            fixtureGrid.SetAreaType(snapshot.Position, snapshot.AreaType);
            areasRestored &= cell.AreaType == snapshot.AreaType;
        }
        Check(areasRestored,
            "FIXTURE_AREA_RESTORE",
            $"count={fixtureAreaSnapshots.Count}; exact={areasRestored}");

        try
        {
            if (saveRegistry != null && originalWorld != null)
            {
                DungeonGameRestoreReport report = new DungeonGameRestoreReport();
                if (!saveRegistry.RestoreAll(originalWorld, report))
                    Fail("RESTORE", string.Join(" | ", report.Errors));
            }
        }
        catch (Exception exception) { Fail("RESTORE", exception.Message); }
        displacedMovementBuildings.Clear();
        fixtureAreaSnapshots.Clear();
        fixtureGrid = null;
        fixtureCellDoor = null;
        fixtureRoom = null;
        activeCaptiveId = string.Empty;
    }

    private readonly struct DisplacedMovementSnapshot
    {
        public BuildableObject Building { get; }
        public GridLayer Layer { get; }
        public IReadOnlyList<Vector2Int> Positions { get; }
        public bool ConnectPositions { get; }

        public DisplacedMovementSnapshot(
            BuildableObject building,
            GridLayer layer,
            IReadOnlyList<Vector2Int> positions,
            bool connectPositions)
        {
            Building = building;
            Layer = layer;
            Positions = positions;
            ConnectPositions = connectPositions;
        }
    }

    private readonly struct FixtureAreaSnapshot
    {
        public Vector2Int Position { get; }
        public GridCellAreaType AreaType { get; }

        public FixtureAreaSnapshot(Vector2Int position, GridCellAreaType areaType)
        {
            Position = position;
            AreaType = areaType;
        }
    }

    private void Check(bool value, string id, string detail)
    {
        checks.Add($"{(value ? "PASS" : "FAIL")}\t{id}\t{detail}");
        if (!value) failures.Add(id + ": " + detail);
    }
    private void Fail(string id, string detail) => Check(false, id, detail);
    private void WriteReport()
    {
        Directory.CreateDirectory("Artifacts/QA");
        List<string> lines = new List<string> { "# Captivity AI live PlayMode", $"RESULT={(failures.Count == 0 ? "PASS" : "FAIL")}; failures={failures.Count}" };
        lines.AddRange(checks); lines.AddRange(failures.Select(value => "FAILURE\t" + value));
        File.WriteAllLines(CaptivityAiPlayModeVerifier.ReportPath, lines);
        Debug.Log(failures.Count == 0 ? "CAPTIVITY_AI_PLAYMODE=PASS" : "CAPTIVITY_AI_PLAYMODE=FAIL; " + string.Join(" | ", failures));
    }
}
#endif
