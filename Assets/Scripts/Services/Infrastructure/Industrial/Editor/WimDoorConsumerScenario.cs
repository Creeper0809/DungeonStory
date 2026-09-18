#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;

internal static class WimDoorConsumerScenario
{
    private const float TimeoutSeconds = 40f;
    private const string RationItemId = "food:preserved-ration";

    internal static IEnumerator Run(
        DungeonRuntimeLifetimeScope scope,
        CharacterActor actor,
        Grid grid,
        Func<BuildingSO, Vector2Int, BuildableObject> place,
        List<string> report) => RunInternal(scope, actor, grid, place, report, includeInvasion: true);

    internal static IEnumerator RunRemaining(
        DungeonRuntimeLifetimeScope scope,
        CharacterActor actor,
        Grid grid,
        Func<BuildingSO, Vector2Int, BuildableObject> place,
        List<string> report) => RunInternal(scope, actor, grid, place, report, includeInvasion: false);

    private static IEnumerator RunInternal(
        DungeonRuntimeLifetimeScope scope,
        CharacterActor actor,
        Grid grid,
        Func<BuildingSO, Vector2Int, BuildableObject> place,
        List<string> report,
        bool includeInvasion)
    {
        Require(scope?.Container != null, "Door consumer verification requires the production scope.");
        Require(actor != null && !actor.IsDead, "Door consumer verification requires a live worker.");
        Require(grid != null, "Door consumer verification requires the live grid.");

        Dictionary<CharacterActor, bool> pauseStates = CharacterActorCollection
            .DistinctByGameObject(UnityEngine.Object.FindObjectsByType<CharacterActor>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None))
            .Where(value => value != null && !value.IsDead)
            .ToDictionary(value => value, value => value.IsAiPaused());
        try
        {
            foreach (CharacterActor liveActor in pauseStates.Keys)
            {
                liveActor.SetAiPaused(true);
                liveActor.Brain?.StopAllAiForLifecycleTransition("qa:wim045:consumer-isolation");
                liveActor.GetAbility<AbilityMove>()?.CancelActiveMovement("qa:wim045:consumer-isolation");
            }

            VerifyOptionalFeedbackCameraTeardown(actor, report);
            if (includeInvasion)
                yield return VerifyInvasionArrival(scope, actor, grid, place, report);
            yield return VerifyRestockPickupFailure(scope, actor, grid, place, report);
            yield return VerifyFullLaunchAndMissingPath(scope, actor, grid, place, report);
            report.Add(includeInvasion
                ? "scope=production-invasion-director+work-priority-restock+full-offense-launch+physical-supply+exterior-departure"
                : "scope=production-work-priority-restock+full-offense-launch+physical-supply+exterior-departure;invasion=preserved-in-prior-focused-report");
        }
        finally
        {
            foreach (KeyValuePair<CharacterActor, bool> pair in pauseStates)
            {
                if (pair.Key != null)
                    pair.Key.SetAiPaused(pair.Value);
            }
        }
    }

    private static void VerifyOptionalFeedbackCameraTeardown(
        CharacterActor actor,
        List<string> report)
    {
        DungeonSceneRuntimeReferences references = new(
            new DungeonSceneServiceReferences(null, null, null, null),
            new DungeonSceneViewReferences(
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                null));
        IMainCameraProvider cameraProvider = new SceneMainCameraProvider(
            references);
        Require(!cameraProvider.TryGetCamera(out Camera missingCamera)
                && missingCamera == null,
            "Optional main-camera availability returned a Camera without registration.");
        bool strictGetterFailed = false;
        try
        {
            _ = cameraProvider.Camera;
        }
        catch (InvalidOperationException exception)
        {
            strictGetterFailed = exception.Message.Contains(
                nameof(IMainCameraProvider),
                StringComparison.Ordinal);
        }
        Require(strictGetterFailed,
            "Required main-camera getter did not fail without registration.");

        GameObject host = new GameObject(
            "WIM045 Optional Feedback Camera Contract")
        {
            hideFlags = HideFlags.HideAndDontSave
        };
        host.SetActive(false);
        try
        {
            CharacterAiScheduler scheduler = host.AddComponent<CharacterAiScheduler>();
            CharacterAiEditorTestDependencies.InjectFeedbackCameraForDiagnostics(
                scheduler,
                cameraProvider);
            Require(!scheduler.ShouldShowCharacterFeedbackFor(actor),
                "Camera-less limited feedback did not fail closed during teardown.");

            SerializedObject serializedScheduler = new SerializedObject(scheduler);
            serializedScheduler.Update();
            SerializedProperty visibilityLimit = serializedScheduler.FindProperty(
                "limitFeedbackToVisibleCharacters");
            Require(visibilityLimit != null,
                "Scheduler feedback visibility field is unavailable to the focused fixture.");
            visibilityLimit.boolValue = false;
            serializedScheduler.ApplyModifiedPropertiesWithoutUndo();
            Require(!scheduler.ShouldShowCharacterFeedbackFor(actor),
                "Disabling the visibility limit reopened camera-less feedback during teardown.");

            scheduler.enabled = false;
            Require(!scheduler.ShouldShowCharacterFeedbackFor(actor),
                "Disabling the scheduler reopened camera-less feedback during teardown.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
        }
        report.Add("optional-feedback-camera-teardown=PASS"
            + ";requiredGetter=throws;availability=false"
            + ";limited=false;visibilityLimitOff=false;schedulerDisabled=false");
    }

    private static IEnumerator VerifyInvasionArrival(
        DungeonRuntimeLifetimeScope scope,
        CharacterActor actor,
        Grid grid,
        Func<BuildingSO, Vector2Int, BuildableObject> place,
        List<string> report)
    {
        ICharacterSpawnerProvider spawnerProvider = scope.Container.Resolve<ICharacterSpawnerProvider>();
        Require(spawnerProvider.TryGetSpawner(out CharacterSpawner spawner) && spawner != null,
            "Invasion consumer fixture requires the production character spawner.");
        Require(scope.Container.Resolve<IWorldDropZoneQuery>().TryGetVisitorEntryPoint(out WorldGridEntryPoint originalEntry),
            "Invasion consumer fixture requires the production visitor entry.");
        InvasionDirectorRuntime director = UnityEngine.Object.FindFirstObjectByType<InvasionDirectorRuntime>(
            FindObjectsInactive.Include);
        Require(director != null, "Production invasion director is missing.");
        Require(director.ActiveIntruders.Count == 0,
            "Focused invasion arrival requires no pre-existing active intruder.");
        IGameEventBus events = scope.Container.Resolve<IGameEventBus>();
        IDoorAccessCommandService doors = scope.Container.Resolve<IDoorAccessCommandService>();
        BuildingSO doorAsset = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/InteriorDoor.asset");
        Require(doorAsset != null, "Authored InteriorDoor is missing.");

        Vector2Int center = originalEntry.GridPosition;
        Require(grid.GetGridCell(center)?.AreaType == GridCellAreaType.Entrance,
            "The manager-owned visitor entry is not the single live Entrance cell: " + center);
        Vector2Int outside = ResolveAlignedOutside(grid, center, originalEntry.OutsidePosition);
        SerializedObject serialized = new SerializedObject(spawner);
        UnityEngine.Object originalDoorPoint = serialized.FindProperty("entryDoorPoint").objectReferenceValue;
        UnityEngine.Object originalOutsidePoint = serialized.FindProperty("outsideSpawnPoint").objectReferenceValue;
        GameObject doorPoint = new GameObject("Wim045 Invasion Door Point");
        GameObject outsidePoint = new GameObject("Wim045 Invasion Outside Point");
        Door door = grid.GetGridCell(center)?.GetOccupant(GridLayer.Building) as Door;
        bool ownsDoor = door == null;
        bool originalIntruderAccess = door?.AccessPolicy?.IsGroupAllowed(DoorAccessGroup.Intruder) ?? true;
        IDisposable breachSubscription = null;
        int breachEvents = 0;
        InvasionDungeonBreachedEvent lastBreach = default;
        CharacterActor focusedIntruder = null;
        try
        {
            if (door == null)
            {
                Require(CanPlace(grid, doorAsset, center),
                    "The single manager-owned entrance cannot host the authored threshold door: " + center);
                door = place(doorAsset, center) as Door;
            }
            Require(door != null, "Could not publish the authored invasion threshold door.");

            doorPoint.transform.position = grid.GetWorldPos(center);
            outsidePoint.transform.position = grid.GetWorldPos(outside);
            serialized.FindProperty("entryDoorPoint").objectReferenceValue = doorPoint.transform;
            serialized.FindProperty("outsideSpawnPoint").objectReferenceValue = outsidePoint.transform;
            serialized.ApplyModifiedPropertiesWithoutUndo();

            Require(doors.SetGroupAllowed(door, DoorAccessGroup.Intruder, false),
                "Could not deny the real Intruder door group.");
            breachSubscription = events.Subscribe<InvasionDungeonBreachedEvent>(value =>
            {
                if (value.intruderActor == focusedIntruder)
                {
                    breachEvents++;
                    lastBreach = value;
                }
            });

            InvasionThreatSnapshot threat = new InvasionThreatSnapshot(
                125f,
                InvasionThreatStage.Candidate,
                new InvasionThreatFactors(6f, 4f, 3f, 1f),
                0f,
                0f);
            Require(director.TrySpawnIntruder(threat, out CharacterActor intruder) && intruder != null,
                "Production invasion director did not spawn the focused intruder: "
                + director.LastSpawnFailureReason);
            focusedIntruder = intruder;
            Require(intruder.TryGetComponent(out InvasionIntruderRuntime runtime),
                "Spawned intruder has no production runtime.");
            AbilityMove intruderMove = intruder.GetAbility<AbilityMove>();
            Require(intruderMove != null, "Spawned intruder has no movement authority.");

            yield return WaitUntil(
                () => intruderMove.LastGridMoveFailureReason == GridMoveFailureReason.DoorDenied,
                "Denied invasion arrival never reported DoorDenied.");
            Require(breachEvents == 0
                    && !runtime.HasBreachedDungeonInterior
                    && grid.GetGridCell(intruder.GetNowXY())?.AreaType != GridCellAreaType.DungeonInterior,
                "Denied invasion arrival published or claimed an interior breach.");
            report.Add("invasion-denied-arrival-no-success-event=PASS;events=0;failure=DoorDenied;position="
                + intruder.GetNowXY());

            Require(doors.SetGroupAllowed(door, DoorAccessGroup.Intruder, true),
                "Could not release the Intruder threshold for planner recovery.");
            yield return WaitUntil(
                () => breachEvents == 1,
                () => "Allowed existing invasion planner never published the actual interior breach."
                    + ";state=" + runtime.State
                    + ";breachTarget=" + (runtime.CurrentBreachTarget != null
                        ? runtime.CurrentBreachTarget.name
                        : "none")
                    + ";position=" + intruder.GetNowXY()
                    + ";entry=" + center
                    + ";doorCell=" + grid.GetXY(spawner.GetEntryDoorWorldPosition())
                    + ";outside=" + outside);
            Require(lastBreach.intruderRuntime == runtime
                    && lastBreach.intruderActor == intruder
                    && runtime.HasBreachedDungeonInterior
                    && grid.GetGridCell(intruder.GetNowXY())?.AreaType == GridCellAreaType.DungeonInterior,
                "Invasion breach event was not tied to the actual runtime/interior position.");
            yield return null;
            Require(breachEvents == 1, "Invasion interior breach event was published more than once.");
            report.Add("invasion-existing-planner-actual-interior-breach-once=PASS;events=1;position="
                + intruder.GetNowXY());
        }
        finally
        {
            breachSubscription?.Dispose();
            director.WithdrawActiveIntrudersForFinalInvasion();
            if (door != null)
            {
                doors.SetGroupAllowed(door, DoorAccessGroup.Intruder, originalIntruderAccess);
                if (ownsDoor)
                    door.DestroySelf();
            }
            serialized.Update();
            serialized.FindProperty("entryDoorPoint").objectReferenceValue = originalDoorPoint;
            serialized.FindProperty("outsideSpawnPoint").objectReferenceValue = originalOutsidePoint;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            UnityEngine.Object.Destroy(doorPoint);
            UnityEngine.Object.Destroy(outsidePoint);
            actor.SetAiPaused(true);
        }
        yield return null;
    }

    private static IEnumerator VerifyRestockPickupFailure(
        DungeonRuntimeLifetimeScope scope,
        CharacterActor actor,
        Grid grid,
        Func<BuildingSO, Vector2Int, BuildableObject> place,
        List<string> report)
    {
        IShopStockCatalog stockCatalog = scope.Container.Resolve<IShopStockCatalog>();
        IWorldItemStackRuntime items = scope.Container.Resolve<IWorldItemStackRuntime>();
        IItemQuantityReservationService reservations = scope.Container.Resolve<IItemQuantityReservationService>();
        IDoorAccessCommandService doors = scope.Container.Resolve<IDoorAccessCommandService>();
        IDoorAccessQuery doorAccess = scope.Container.Resolve<IDoorAccessQuery>();
        AbilityWork work = actor.GetAbility<AbilityWork>();
        AbilityMove move = actor.GetAbility<AbilityMove>();
        Require(work != null && move != null && actor.Brain != null
                && actor.PathSearchBroker != null
                && doorAccess != null,
            "Restock consumer fixture requires worker, movement, and brain authorities.");

        IWorldDropZoneQuery dropZones = scope.Container.Resolve<IWorldDropZoneQuery>();
        Require(dropZones.TryGetVisitorEntryPoint(out WorldGridEntryPoint entry),
            "Restock pickup failure requires the production visitor entry.");
        Door door = ResolveProductionEntryDoor(
            grid,
            entry,
            out Vector2Int doorPosition,
            out string doorDiagnostics);
        Require(door != null && door.IsDungeonEntrance,
            "The production DoorPosition does not resolve the authored entrance Door: "
            + doorDiagnostics);
        CharacterId actorIdentity = CharacterPersistentIdentity.Require(actor);
        string actorId = actorIdentity.Value;
        GridTraversalContext actorTraversal = GridTraversalContext.ForCharacter(
            actorIdentity);
        GridTraversalContext stagingTraversal = GridTraversalContext.ForCharacter(
            actorIdentity,
            DoorAccessOverrideKind.DirectCommand);
        Vector2Int outside = ResolveAlignedOutside(
            grid,
            entry.GridPosition,
            entry.OutsidePosition,
            door);
        DoorAccessIndividualRule originalDoorRule = door.AccessPolicy?
            .GetIndividualRule(actorId)
            ?? DoorAccessIndividualRule.GroupDefault;
        CharacterCarryInventory carry = CharacterCarryInventory.Ensure(actor);
        Vector2Int originalPosition = actor.GetNowXY();

        Shop shop = PlaceAuthoredShop(
            grid,
            actor,
            place,
            stockCatalog,
            outside,
            out SaleItem saleItem,
            out string shopFixture);
        Require(shop != null && saleItem != null,
            "Could not publish an authored physical-stock shop: " + shopFixture);
        report.Add("restock-authored-shop-fixture=" + shopFixture);
        shop.DebugClearStock();
        IWarehouseFacility warehouse = PlaceAuthoredWarehouse(
            grid,
            actor,
            place,
            saleItem.category,
            2,
            outside,
            entry.GridPosition,
            door,
            out Vector2Int pickupStand,
            out string warehouseFixture);
        BuildableObject warehouseBuilding = warehouse as BuildableObject;
        Require(warehouseBuilding != null,
            "Could not publish an authored compatible warehouse: " + warehouseFixture);
        report.Add("restock-authored-warehouse-fixture=" + warehouseFixture);
        string warehouseDestination = WarehouseStorageIdentity.RequireDestinationId(warehouse);
        HashSet<string> beforeIds = items.GetAllStacks()
            .Where(value => value != null)
            .Select(value => value.StackId)
            .ToHashSet(StringComparer.Ordinal);
        int seedAmount = Mathf.Clamp(shop.MissingStock, 2, 4);
        Require(items.SpawnItemAt(
                saleItem.ItemDefinitionId.Value,
                seedAmount,
                warehouseBuilding.centerPos,
                WorldItemStackState.Stored,
                warehouseDestination,
                out int created)
                && created == seedAmount,
            "Could not publish exact stored restock stock.");
        WorldItemStackSnapshot sourceBefore = items.GetAllStacks().Single(value => value != null
            && !beforeIds.Contains(value.StackId)
            && string.Equals(value.ItemId, saleItem.ItemDefinitionId.Value, StringComparison.Ordinal)
            && string.Equals(value.DestinationId, warehouseDestination, StringComparison.Ordinal));

        Require(ResolvePickupStand(grid, sourceBefore.Position) == pickupStand,
            "The physical stack pickup authority changed after exact stored-stock publication.");

        try
        {
            doors.SetIndividualRule(
                door,
                actorId,
                DoorAccessIndividualRule.Allow);
            Require(doorAccess.CanUse(
                    door,
                    actorTraversal,
                    out string denialReason),
                "The production actor permission did not admit the authored entrance Door: "
                + "door=" + door.name
                + ";doorPosition=" + doorPosition
                + ";managerEntry=" + entry.GridPosition
                + ";rule=" + door.AccessPolicy?.GetIndividualRule(actorId)
                + ";denial=" + denialReason
                + ";route=" + DescribeRestockRoute(
                    grid,
                    doorAccess,
                    actorTraversal,
                    outside,
                    entry.GridPosition,
                    pickupStand));

            Queue<GridMoveStep> gridEntryPath = grid.GetMovePathTo(
                outside,
                entry.GridPosition);
            Queue<GridMoveStep> gridPickupPath = grid.GetMovePathTo(
                outside,
                pickupStand);
            bool gridCrossesDoor = PathTraversesDoor(gridEntryPath, door);
            bool gridPickupCrossesDoor = PathTraversesDoor(gridPickupPath, door);
            bool gridPickupCrossesManagerEntry = gridPickupPath != null
                && gridPickupPath.Any(step => step.To == entry.GridPosition);
            Require(gridEntryPath != null
                    && gridEntryPath.Count > 0
                    && gridEntryPath.Any(step => step.To == entry.GridPosition)
                    && gridCrossesDoor
                    && gridPickupPath != null
                    && gridPickupPath.Count > 0
                    && gridPickupCrossesDoor
                    && gridPickupCrossesManagerEntry,
                "The selected authored restock fixture has no legal outside-to-pickup route through the production Door and manager waypoint: "
                + DescribeRestockRoute(
                    grid,
                    doorAccess,
                    actorTraversal,
                    outside,
                    entry.GridPosition,
                    pickupStand));

            Queue<GridMoveStep> stagingPath = null;
            GridPathRequestStatus stagingStatus = GridPathRequestStatus.Pending;
            Vector2Int stagingOrigin = actor.GetNowXY();
            yield return WaitUntil(() =>
            {
                if (stagingStatus != GridPathRequestStatus.Pending)
                    return true;
                stagingStatus = actor.PathSearchBroker.RequestMovePathTo(
                    grid,
                    stagingOrigin,
                    outside,
                    out stagingPath,
                    GridPathSearchPriority.Urgent,
                    stagingTraversal);
                return stagingStatus != GridPathRequestStatus.Pending;
            }, () => "Actor-aware restock staging path query did not reach a terminal result. "
                + "status=" + stagingStatus
                + ";broker=" + DescribeActorPathRequest(
                    actor.PathSearchBroker,
                    grid,
                    stagingOrigin,
                    outside,
                    stagingTraversal));
            Require(stagingOrigin == outside
                    || (stagingStatus == GridPathRequestStatus.Reachable
                        && stagingPath != null
                        && stagingPath.Count > 0),
                "Actor-aware restock staging path reached a terminal non-reachable result: "
                + "status=" + stagingStatus
                + ";origin=" + stagingOrigin
                + ";outside=" + outside
                + ";broker=" + DescribeActorPathRequest(
                    actor.PathSearchBroker,
                    grid,
                    stagingOrigin,
                    outside,
                    stagingTraversal));
            Require(move.TryStartSystemMove(outside, DoorAccessOverrideKind.DirectCommand, out string moveFailure),
                "Could not stage restock worker: " + moveFailure);
            yield return WaitUntil(() => actor.GetNowXY() == outside && !move.IsSystemMoveInProgress,
                "Restock worker did not reach its staging cell.");
            Require(actor.CurrentLifecycleState == CharacterLifecycleState.Active
                    && !actor.IsOnExpedition,
                "Outside restock staging changed the resident's active lifecycle authority: "
                + actor.CurrentLifecycleState);
            report.Add("restock-active-resident-outside-stage=PASS;lifecycle="
                + actor.CurrentLifecycleState + ";position=" + actor.GetNowXY());
            actor.SetAiPaused(true);
            actor.Brain.StopAllAiForLifecycleTransition("qa:wim045:restock-stage");

            Queue<GridMoveStep> allowedPickupPath = null;
            GridPathRequestStatus allowedPickupStatus = GridPathRequestStatus.Pending;
            Vector2Int actorPathOrigin = actor.GetNowXY();
            Require(actorPathOrigin == outside,
                "Actor-aware restock preflight origin is not the actor's actual staged position: "
                + "actual=" + actorPathOrigin + ";expected=" + outside);
            yield return WaitUntil(() =>
            {
                if (allowedPickupStatus != GridPathRequestStatus.Pending)
                    return true;
                allowedPickupStatus = actor.PathSearchBroker.RequestMovePathTo(
                    grid,
                    actorPathOrigin,
                    pickupStand,
                    out allowedPickupPath,
                    GridPathSearchPriority.Normal,
                    actorTraversal);
                return allowedPickupStatus != GridPathRequestStatus.Pending;
            }, () => "Actor-aware restock pickup path query did not reach a terminal result. "
                + "status=" + allowedPickupStatus
                + ";broker=" + DescribeActorPathRequest(
                    actor.PathSearchBroker,
                    grid,
                    actorPathOrigin,
                    pickupStand,
                    actorTraversal)
                + ";route=" + DescribeRestockRoute(
                    grid,
                    doorAccess,
                    actorTraversal,
                    outside,
                    entry.GridPosition,
                    pickupStand));
            Require(allowedPickupStatus == GridPathRequestStatus.Reachable
                    && allowedPickupPath != null
                    && allowedPickupPath.Count > 0,
                "Actor-aware restock pickup path reached a terminal non-reachable result: "
                + "status=" + allowedPickupStatus
                + ";actor=" + actorId
                + ";origin=" + actorPathOrigin
                + ";pickup=" + pickupStand
                + ";broker=" + DescribeActorPathRequest(
                    actor.PathSearchBroker,
                    grid,
                    actorPathOrigin,
                    pickupStand,
                    actorTraversal)
                + ";route=" + DescribeRestockRoute(
                    grid,
                    doorAccess,
                    actorTraversal,
                    outside,
                    entry.GridPosition,
                    pickupStand));
            bool actorCrossesDoor = PathTraversesDoor(allowedPickupPath, door);
            bool actorCrossesManagerEntry = allowedPickupPath.Any(step =>
                step.To == entry.GridPosition);
            GridPathSearchResult finalTopologySearch = grid.SearchPath(
                actor.GetNowXY());
            bool finalShopReachable = WorkTargetSelectionRules.IsReachable(
                shop,
                finalTopologySearch);
            bool finalWarehouseReachable = WorkTargetSelectionRules.IsReachable(
                warehouseBuilding,
                finalTopologySearch);
            Require(gridEntryPath != null
                    && gridEntryPath.Count > 0
                    && gridEntryPath.Any(step => step.To == entry.GridPosition)
                    && gridCrossesDoor
                    && gridPickupPath != null
                    && gridPickupPath.Count > 0
                    && allowedPickupPath.Count > 0
                    && actorCrossesDoor
                    && actorCrossesManagerEntry
                    && finalShopReachable
                    && finalWarehouseReachable,
                "Allowed pickup path did not separately traverse the authored Door and manager waypoint: "
                + "actor=" + actorId
                + ";actorPosition=" + actor.GetNowXY()
                + ";lifecycle=" + actor.CurrentLifecycleState
                + ";outside=" + outside
                + ";doorPosition=" + doorPosition
                + ";managerEntry=" + entry.GridPosition
                + ";pickup=" + pickupStand
                + ";entrySteps=" + gridEntryPath.Count
                + ";gridSteps=" + (gridPickupPath?.Count ?? -1)
                + ";actorSteps=" + allowedPickupPath.Count
                + ";actorPathStatus=" + allowedPickupStatus
                + ";gridCrossesDoor=" + gridCrossesDoor
                + ";actorCrossesDoor=" + actorCrossesDoor
                + ";actorCrossesManagerEntry=" + actorCrossesManagerEntry
                + ";finalShopReachable=" + finalShopReachable
                + ";finalWarehouseReachable=" + finalWarehouseReachable
                + ";route=" + DescribeRestockRoute(
                    grid,
                    doorAccess,
                    actorTraversal,
                    outside,
                    entry.GridPosition,
                    pickupStand));
            report.Add("restock-authored-pickup-route-fixture=outside=" + outside
                + ";doorPosition=" + doorPosition
                + ";doorFootprint=" + string.Join(",", door.BuildingData
                    .GetGridPosList(door.centerPos))
                + ";managerEntry=" + entry.GridPosition
                + ";door=" + door.name
                + ";pickup=" + pickupStand
                + ";entrySteps=" + gridEntryPath.Count
                + ";gridSteps=" + (gridPickupPath?.Count ?? -1)
                + ";actorSteps=" + allowedPickupPath.Count
                + ";actorPathStatus=" + allowedPickupStatus
                + ";actorAware=true;actualActorOrigin=true;doorAndManagerWaypointValidatedSeparately=true"
                + ";finalShopReachable=" + finalShopReachable
                + ";finalWarehouseReachable=" + finalWarehouseReachable
                + ";route="
                + DescribeRestockRoute(
                    grid,
                    doorAccess,
                    actorTraversal,
                    outside,
                    entry.GridPosition,
                    pickupStand));

            GridPathSearchBroker controlledBroker = actor.PathSearchBroker
                as GridPathSearchBroker;
            Require(controlledBroker != null,
                "Controlled Pending fixture requires the production GridPathSearchBroker.");
            long terminalFailureCountBefore = actor.Brain.RuntimeExecutionFailureCount;
            yield return StartCommittedRestockWithForcedPending(
                actor,
                work,
                shop,
                grid,
                controlledBroker,
                report,
                "terminal-no-path");
            Require(shop.ActiveRestockOperationCount == 1
                    && reservations.GetLeasesForStack(new ItemStackId(sourceBefore.StackId)).Count > 0,
                "Pending restock path did not retain its physical quantity lease/operation.");
            Require(carry.CountItem(saleItem.ItemDefinitionId.Value) == 0,
                "Pending restock path created carried cargo before pickup.");

            Require(doors.SetIndividualRule(door, actorId, DoorAccessIndividualRule.Deny),
                "Could not deny the restock worker during its Pending path request.");
            yield return WaitUntil(
                () =>
                {
                    bool terminal = !work.HasActiveWorkRoutineForDiagnostics
                        && actor.Brain.RuntimeExecutionFailureCount
                            > terminalFailureCountBefore;
                    if (terminal)
                        actor.SetAiPaused(true);
                    return terminal;
                },
                "Terminally unreachable restock path did not stop the production work owner.");
            Require(actor.Brain.LastActionFailure.Kind == AIActionFailureKind.NoPath
                    && string.Equals(
                        actor.Brain.LastActionFailure.Reason,
                        "restock-pickup-path-unreachable",
                        StringComparison.Ordinal),
                "Terminally unreachable restock path did not report typed NoPath: "
                + actor.Brain.LastActionFailure);
            WorldItemStackSnapshot terminalSourceAfter = items.GetAllStacks()
                .SingleOrDefault(value => value != null
                    && string.Equals(value.StackId, sourceBefore.StackId, StringComparison.Ordinal));
            Require(terminalSourceAfter != null
                    && terminalSourceAfter.Quantity == sourceBefore.Quantity
                    && terminalSourceAfter.State == sourceBefore.State
                    && terminalSourceAfter.Position == sourceBefore.Position
                    && string.Equals(
                        terminalSourceAfter.DestinationId,
                        sourceBefore.DestinationId,
                        StringComparison.Ordinal)
                    && carry.CountItem(saleItem.ItemDefinitionId.Value) == 0
                    && reservations.GetLeasesForStack(
                        new ItemStackId(sourceBefore.StackId)).Count == 0
                    && shop.ActiveRestockOperationCount == 0,
                "Terminal NoPath mutated pickup stock or leaked restock ownership.");
            report.Add("restock-pending-to-terminal-no-path=PASS;controlledBudget=0"
                + ";budgetDeferrals=1;pickup=0;leases=0;operations=0;failure="
                + actor.Brain.LastActionFailure.Kind);

            actor.SetAiPaused(true);
            actor.Brain.StopAllAiForLifecycleTransition(
                "qa:wim045:restock-terminal-no-path-complete");
            work.ClearPriorityWorkTarget();
            Require(doors.SetIndividualRule(door, actorId, DoorAccessIndividualRule.Allow),
                "Could not reopen the restock route after terminal NoPath verification.");
            yield return null;

            long reachableFailureCountBefore = actor.Brain.RuntimeExecutionFailureCount;
            yield return StartCommittedRestockWithForcedPending(
                actor,
                work,
                shop,
                grid,
                controlledBroker,
                report,
                "pending-to-reachable");
            bool sawReachableRun = false;
            yield return WaitUntil(() =>
            {
                string phase = actor.Brain.CurrentActionPhaseDetail;
                sawReachableRun |= work.HasActiveWorkRoutineForDiagnostics;
                return (work.HasActiveWorkRoutineForDiagnostics
                        && string.Equals(
                            phase,
                            "restock:move-to-stock",
                            StringComparison.Ordinal))
                    || actor.Brain.RuntimeExecutionFailureCount
                        > reachableFailureCountBefore
                    || (sawReachableRun
                        && !work.HasActiveWorkRoutineForDiagnostics);
            }, "Production restock did not resolve Pending to Reachable.");
            Require(work.HasActiveWorkRoutineForDiagnostics
                    && string.Equals(
                        actor.Brain.CurrentActionPhaseDetail,
                        "restock:move-to-stock",
                        StringComparison.Ordinal),
                "Production restock did not preserve its lease through Pending-to-Reachable: "
                + "phase=" + actor.Brain.CurrentActionPhaseDetail
                + ";failure=" + actor.Brain.LastActionFailure);
            Require(shop.ActiveRestockOperationCount == 1
                    && reservations.GetLeasesForStack(
                        new ItemStackId(sourceBefore.StackId)).Count > 0,
                "Reachable restock move phase did not own the physical quantity lease/operation.");
            Require(actor.GetNowXY() != entry.GridPosition && actor.GetNowXY() != pickupStand,
                "Restock worker crossed the validated threshold before denial: position="
                + actor.GetNowXY() + ";threshold=" + entry.GridPosition
                + ";pickup=" + pickupStand);

            Require(doors.SetIndividualRule(door, actorId, DoorAccessIndividualRule.Deny),
                "Could not deny the restock worker after route publication.");
            actor.SetAiPaused(true);
            bool sawDoorDenied = false;
            yield return WaitUntil(() =>
            {
                sawDoorDenied |= move.LastGridMoveFailureReason == GridMoveFailureReason.DoorDenied;
                return sawDoorDenied && !work.HasActiveWorkRoutineForDiagnostics;
            }, "Restock pickup failure did not terminate through the production work owner.");

            WorldItemStackSnapshot sourceAfter = items.GetAllStacks()
                .SingleOrDefault(value => value != null
                    && string.Equals(value.StackId, sourceBefore.StackId, StringComparison.Ordinal));
            Require(sourceAfter != null
                    && sourceAfter.Quantity == sourceBefore.Quantity
                    && sourceAfter.State == sourceBefore.State
                    && sourceAfter.Position == sourceBefore.Position
                    && string.Equals(sourceAfter.DestinationId, sourceBefore.DestinationId, StringComparison.Ordinal),
                "Failed restock pickup mutated the physical source stack.");
            Require(carry.CountItem(saleItem.ItemDefinitionId.Value) == 0,
                "Failed restock pickup created carried cargo.");
            Require(reservations.GetLeasesForStack(new ItemStackId(sourceBefore.StackId)).Count == 0,
                "Failed restock pickup retained its quantity lease.");
            Require(shop.ActiveRestockOperationCount == 0,
                "Failed restock pickup retained its retail operation owner.");
            Require(actor.GetNowXY() != pickupStand,
                "Denied restock worker reached the physical pickup stand.");
            report.Add("restock-pending-to-reachable-door-cancel-before-physical-pickup=PASS"
                + ";controlledBudget=0;budgetDeferrals=1;source="
                + sourceBefore.StackId + ";quantity=" + sourceBefore.Quantity
                + ";carry=0;leases=0;operations=0;failure=DoorDenied");

            actor.Brain.StopAllAiForLifecycleTransition(
                "qa:wim045:restock-door-cancel-complete");
            work.ClearPriorityWorkTarget();
            Require(doors.SetIndividualRule(
                    door,
                    actorId,
                    DoorAccessIndividualRule.Allow),
                "Could not reopen the authored entrance Door for successful restock.");
            yield return null;

            int shopStockBeforeSuccess = shop.CurrentStock;
            int sourceQuantityBeforeSuccess = CountDestination(
                items,
                warehouseDestination,
                saleItem.ItemDefinitionId.Value);
            int carryQuantityBeforeSuccess = carry.CountItem(
                saleItem.ItemDefinitionId.Value);
            int looseQuantityBeforeSuccess = CountState(
                items,
                saleItem.ItemDefinitionId.Value,
                WorldItemStackState.Loose);
            int accountedQuantityBeforeSuccess = sourceQuantityBeforeSuccess
                + carryQuantityBeforeSuccess
                + looseQuantityBeforeSuccess
                + shopStockBeforeSuccess;
            int expectedRestocked = Mathf.Min(
                shop.MissingStock,
                sourceQuantityBeforeSuccess);
            Require(expectedRestocked > 0,
                "Successful restock retry has no physical quantity to transfer.");
            GridPathSearchResult successSearch = grid.SearchPath(actor.GetNowXY());
            Require(work.TrySetPriorityWorkTarget(
                    shop,
                    BuiltInWorkTypeIds.Restock,
                    successSearch,
                    out string successPriorityFailure),
                "Successful production restock priority command was rejected: "
                + successPriorityFailure);
            long successFailureCountBefore = actor.Brain.RuntimeExecutionFailureCount;
            bool sawPickupArrival = false;
            bool sawPhysicalCarry = false;
            bool sawSourceWithdrawal = false;
            bool sawDeliveryArrival = false;
            actor.SetAiPaused(false);
            Require(actor.Brain.PreferWorkActionOnNextDecision(
                    BuiltInWorkTypeIds.Restock,
                    45f),
                "Brain rejected the natural successful restock retry.");
            yield return WaitUntil(() =>
            {
                int currentCarry = carry.CountItem(saleItem.ItemDefinitionId.Value);
                int currentSource = CountDestination(
                    items,
                    warehouseDestination,
                    saleItem.ItemDefinitionId.Value);
                sawPickupArrival |= actor.GetNowXY() == pickupStand;
                sawPhysicalCarry |= currentCarry > 0;
                sawSourceWithdrawal |= currentSource < sourceQuantityBeforeSuccess;
                sawDeliveryArrival |= shop.IsWorkAccessGridPosition(
                    grid,
                    actor.GetNowXY());
                bool completed = !work.HasActiveWorkRoutineForDiagnostics
                    && shop.CurrentStock
                        == shopStockBeforeSuccess + expectedRestocked;
                if (completed)
                    actor.SetAiPaused(true);
                return completed
                    || actor.Brain.RuntimeExecutionFailureCount
                        > successFailureCountBefore;
            }, "Natural production restock did not complete from pickup to shop.");
            int sourceQuantityAfterSuccess = CountDestination(
                items,
                warehouseDestination,
                saleItem.ItemDefinitionId.Value);
            int carryQuantityAfterSuccess = carry.CountItem(
                saleItem.ItemDefinitionId.Value);
            int looseQuantityAfterSuccess = CountState(
                items,
                saleItem.ItemDefinitionId.Value,
                WorldItemStackState.Loose);
            int accountedQuantityAfterSuccess = sourceQuantityAfterSuccess
                + carryQuantityAfterSuccess
                + looseQuantityAfterSuccess
                + shop.CurrentStock;
            bool sawTerminalFailure = actor.Brain.RuntimeExecutionFailureCount
                > successFailureCountBefore;
            string terminalFailureKind = sawTerminalFailure
                ? actor.Brain.LastActionFailure.Kind.ToString()
                : "None";
            string terminalFailureReason = sawTerminalFailure
                ? actor.Brain.LastActionFailure.Reason
                : string.Empty;
            string terminalPhase = actor.Brain.CurrentActionPhaseDetail;
            string looseStacksAfterSuccess = DescribeStateStacks(
                items,
                saleItem.ItemDefinitionId.Value,
                WorldItemStackState.Loose);
            report.Add("restock-natural-terminal-accounting="
                + "source=" + sourceQuantityBeforeSuccess
                + "->" + sourceQuantityAfterSuccess
                + ";carry=" + carryQuantityBeforeSuccess
                + "->" + carryQuantityAfterSuccess
                + ";loose=" + looseQuantityBeforeSuccess
                + "->" + looseQuantityAfterSuccess
                + ";shop=" + shopStockBeforeSuccess
                + "->" + shop.CurrentStock
                + ";total=" + accountedQuantityBeforeSuccess
                + "->" + accountedQuantityAfterSuccess
                + ";phase=" + terminalPhase
                + ";failureKind=" + terminalFailureKind
                + ";failureReason=" + terminalFailureReason
                + ";looseStacks=" + looseStacksAfterSuccess);
            Require(shop.CurrentStock
                        == shopStockBeforeSuccess + expectedRestocked
                    && sourceQuantityAfterSuccess
                        == sourceQuantityBeforeSuccess - expectedRestocked
                    && sawPickupArrival
                    && sawPhysicalCarry
                    && sawSourceWithdrawal
                    && sawDeliveryArrival
                    && !sawTerminalFailure
                    && carryQuantityAfterSuccess == carryQuantityBeforeSuccess
                    && looseQuantityAfterSuccess == looseQuantityBeforeSuccess
                    && accountedQuantityAfterSuccess
                        == accountedQuantityBeforeSuccess
                    && reservations.GetLeasesForStack(
                        new ItemStackId(sourceBefore.StackId)).Count == 0
                    && shop.ActiveRestockOperationCount == 0,
                "Natural restock did not conserve source/carry/Loose/shop ownership: "
                + "source=" + sourceQuantityBeforeSuccess
                + "->" + sourceQuantityAfterSuccess
                + ";shop=" + shopStockBeforeSuccess
                + "->" + shop.CurrentStock
                + ";expected=" + expectedRestocked
                + ";sawPickupArrival=" + sawPickupArrival
                + ";sawPhysicalCarry=" + sawPhysicalCarry
                + ";sawSourceWithdrawal=" + sawSourceWithdrawal
                + ";sawDeliveryArrival=" + sawDeliveryArrival
                + ";carry=" + carryQuantityBeforeSuccess
                + "->" + carryQuantityAfterSuccess
                + ";loose=" + looseQuantityBeforeSuccess
                + "->" + looseQuantityAfterSuccess
                + ";total=" + accountedQuantityBeforeSuccess
                + "->" + accountedQuantityAfterSuccess
                + ";leases=" + reservations.GetLeasesForStack(
                    new ItemStackId(sourceBefore.StackId)).Count
                + ";operations=" + shop.ActiveRestockOperationCount
                + ";phase=" + terminalPhase
                + ";failureKind=" + terminalFailureKind
                + ";failureReason=" + terminalFailureReason
                + ";looseStacks=" + looseStacksAfterSuccess);
            report.Add("restock-natural-arrival-pickup-shop-success=PASS;source="
                + sourceQuantityBeforeSuccess + "->" + sourceQuantityAfterSuccess
                + ";shop=" + shopStockBeforeSuccess + "->" + shop.CurrentStock
                + ";pickupArrivalObserved=true;deliveryArrivalObserved=true"
                + ";physicalCarryObserved=true"
                + ";sourceWithdrawalObserved=true;carry="
                + carryQuantityBeforeSuccess + "->" + carryQuantityAfterSuccess
                + ";loose=" + looseQuantityBeforeSuccess
                + "->" + looseQuantityAfterSuccess
                + ";total=" + accountedQuantityBeforeSuccess
                + "->" + accountedQuantityAfterSuccess
                + ";leases=0;operations=0");

            actor.SetAiPaused(true);
            bool stoppedCompletedRestockAction = actor.Brain.StopCurrentActionForReplan(
                "qa:wim045:restock-success-return");
            work.ClearPriorityWorkTarget();
            move.CancelActiveMovement("qa:wim045:restock-success-return");
            yield return WaitUntil(
                () => !work.HasActiveWorkRoutineForDiagnostics
                    && !move.HasActiveMovementRoutineForDiagnostics
                    && !actor.Brain.HasRunningAction
                    && !actor.Brain.IsExternallyDrivenActionActive,
                () => "Completed Restock action did not retire before the focused return command. "
                    + "stoppedForReplan=" + stoppedCompletedRestockAction
                    + ";workRoutine=" + work.HasActiveWorkRoutineForDiagnostics
                    + ";moveRoutine=" + move.HasActiveMovementRoutineForDiagnostics
                    + ";runningAction=" + actor.Brain.HasRunningAction
                    + ";externalAction=" + actor.Brain.IsExternallyDrivenActionActive
                    + ";phase=" + actor.Brain.CurrentActionPhaseDetail);

            Require(doors.SetIndividualRule(
                    door, actorId, originalDoorRule),
                "Could not restore the authored entrance Door after restock verification.");
            Require(doorAccess.CanUse(
                    door,
                    stagingTraversal,
                    out string returnDoorDenial),
                "The restored authored entrance Door rejected the DirectCommand return: "
                + "rule=" + door.AccessPolicy?.GetIndividualRule(actorId)
                + ";denial=" + returnDoorDenial);

            Vector2Int returnLogicalOrigin = actor.GetNowXY();
            Vector2Int returnPhysicalOrigin = grid.GetXY(move.transform.position);
            int returnGridVersion = grid.TraversalVersion;
            int returnDoorVersion = doorAccess.DoorAccessVersion;
            Queue<GridMoveStep> returnPath = null;
            GridPathRequestStatus returnStatus = GridPathRequestStatus.Pending;
            int returnTypedAttempts = 0;
            int returnTypedPending = 0;
            yield return WaitUntil(() =>
            {
                if (returnStatus != GridPathRequestStatus.Pending)
                    return true;
                returnTypedAttempts++;
                returnStatus = actor.PathSearchBroker.RequestMovePathTo(
                    grid,
                    returnPhysicalOrigin,
                    originalPosition,
                    out returnPath,
                    GridPathSearchPriority.Urgent,
                    stagingTraversal);
                if (returnStatus == GridPathRequestStatus.Pending)
                    returnTypedPending++;
                return returnStatus != GridPathRequestStatus.Pending;
            }, () => "Physical-origin restock return path query did not reach a terminal result. "
                + "status=" + returnStatus
                + ";logicalOrigin=" + returnLogicalOrigin
                + ";physicalOrigin=" + returnPhysicalOrigin
                + ";destination=" + originalPosition
                + ";gridVersion=" + returnGridVersion
                + "->" + grid.TraversalVersion
                + ";doorVersion=" + returnDoorVersion
                + "->" + doorAccess.DoorAccessVersion
                + ";attempts=" + returnTypedAttempts
                + ";pending=" + returnTypedPending
                + ";broker=" + DescribeActorPathRequest(
                    actor.PathSearchBroker,
                    grid,
                    returnPhysicalOrigin,
                    originalPosition,
                    stagingTraversal));
            bool returnCrossesDoor = returnPath != null
                && PathTraversesDoor(returnPath, door);
            Require(returnPhysicalOrigin == originalPosition
                    || (returnStatus == GridPathRequestStatus.Reachable
                        && returnPath != null
                        && returnPath.Count > 0),
                "Physical-origin restock return path reached a terminal non-reachable result: "
                + "status=" + returnStatus
                + ";logicalOrigin=" + returnLogicalOrigin
                + ";physicalOrigin=" + returnPhysicalOrigin
                + ";destination=" + originalPosition
                + ";crossesDoor=" + returnCrossesDoor
                + ";gridVersion=" + returnGridVersion
                + "->" + grid.TraversalVersion
                + ";doorVersion=" + returnDoorVersion
                + "->" + doorAccess.DoorAccessVersion
                + ";attempts=" + returnTypedAttempts
                + ";pending=" + returnTypedPending
                + ";broker=" + DescribeActorPathRequest(
                    actor.PathSearchBroker,
                    grid,
                    returnPhysicalOrigin,
                    originalPosition,
                    stagingTraversal));

            bool returnCommandStarted = false;
            int returnStartAttempts = 0;
            int returnCommandPending = 0;
            int returnCommandReachableRetries = 0;
            GridPathRequestStatus returnCommandStatus =
                GridPathRequestStatus.Pending;
            string returnFailure = string.Empty;
            string returnCommandFailure = string.Empty;
            string returnBrokerStatus = string.Empty;
            yield return WaitUntil(() =>
            {
                if (returnCommandStarted
                    || !string.IsNullOrEmpty(returnCommandFailure))
                    return true;
                Vector2Int logicalNow = actor.GetNowXY();
                Vector2Int physicalNow = grid.GetXY(move.transform.position);
                bool doorAllowed = doorAccess.CanUse(
                    door,
                    stagingTraversal,
                    out string denial);
                if (logicalNow != returnLogicalOrigin
                    || physicalNow != returnPhysicalOrigin
                    || !doorAllowed)
                {
                    returnCommandFailure = "return authority changed before command admission"
                        + ";logical=" + returnLogicalOrigin + "->" + logicalNow
                        + ";physical=" + returnPhysicalOrigin + "->" + physicalNow
                        + ";gridVersion=" + returnGridVersion
                        + "->" + grid.TraversalVersion
                        + ";doorVersion=" + returnDoorVersion
                        + "->" + doorAccess.DoorAccessVersion
                        + ";doorDenial=" + denial;
                    return true;
                }

                returnStartAttempts++;
                returnCommandStarted = move.TryStartSystemMove(
                    originalPosition,
                    DoorAccessOverrideKind.DirectCommand,
                    out returnFailure);
                returnBrokerStatus = DescribeActorPathRequest(
                    actor.PathSearchBroker,
                    grid,
                    returnPhysicalOrigin,
                    originalPosition,
                    stagingTraversal);
                if (returnCommandStarted)
                {
                    returnCommandStatus = GridPathRequestStatus.Reachable;
                    return true;
                }

                returnCommandStatus = actor.PathSearchBroker.RequestMovePathTo(
                    grid,
                    returnPhysicalOrigin,
                    originalPosition,
                    out Queue<GridMoveStep> observedReturnPath,
                    GridPathSearchPriority.Urgent,
                    stagingTraversal);
                returnBrokerStatus = DescribeActorPathRequest(
                    actor.PathSearchBroker,
                    grid,
                    returnPhysicalOrigin,
                    originalPosition,
                    stagingTraversal);
                if (returnCommandStatus == GridPathRequestStatus.Unreachable)
                {
                    returnCommandFailure = "actual return command reached typed terminal Unreachable"
                        + ";message=" + returnFailure
                        + ";broker=" + returnBrokerStatus;
                    return true;
                }
                if (returnCommandStatus == GridPathRequestStatus.Reachable)
                {
                    if (returnPhysicalOrigin != originalPosition
                        && (observedReturnPath == null
                            || observedReturnPath.Count == 0))
                    {
                        returnCommandFailure = "typed Reachable return result had no path"
                            + ";message=" + returnFailure
                            + ";broker=" + returnBrokerStatus;
                        return true;
                    }

                    returnCommandReachableRetries++;
                    return false;
                }

                returnCommandPending++;
                return false;
            }, () => "Actual DirectCommand return did not start across valid Pending results. "
                + "logicalOrigin=" + returnLogicalOrigin
                + ";physicalOrigin=" + returnPhysicalOrigin
                + ";destination=" + originalPosition
                + ";gridVersion=" + returnGridVersion
                + "->" + grid.TraversalVersion
                + ";doorVersion=" + returnDoorVersion
                + "->" + doorAccess.DoorAccessVersion
                + ";startAttempts=" + returnStartAttempts
                + ";pending=" + returnCommandPending
                + ";reachableRetries=" + returnCommandReachableRetries
                + ";typedStatus=" + returnCommandStatus
                + ";message=" + returnFailure
                + ";broker=" + returnBrokerStatus);
            Require(returnCommandStarted
                    && string.IsNullOrEmpty(returnCommandFailure),
                "Could not start the actual DirectCommand restock return: "
                + returnCommandFailure
                + ";logicalOrigin=" + returnLogicalOrigin
                + ";physicalOrigin=" + returnPhysicalOrigin
                + ";destination=" + originalPosition
                + ";gridVersion=" + returnGridVersion
                + "->" + grid.TraversalVersion
                + ";doorVersion=" + returnDoorVersion
                + "->" + doorAccess.DoorAccessVersion
                + ";typedStatus=" + returnStatus
                + ";typedAttempts=" + returnTypedAttempts
                + ";typedPending=" + returnTypedPending
                + ";startAttempts=" + returnStartAttempts
                + ";commandPending=" + returnCommandPending
                + ";commandReachableRetries=" + returnCommandReachableRetries
                + ";commandTypedStatus=" + returnCommandStatus
                + ";message=" + returnFailure
                + ";broker=" + returnBrokerStatus);
            yield return WaitUntil(
                () => actor.GetNowXY() == originalPosition
                    && grid.GetXY(move.transform.position) == originalPosition
                    && !move.IsSystemMoveInProgress,
                () => "Restock worker did not physically return to its original interior cell. "
                    + "logical=" + actor.GetNowXY()
                    + ";physical=" + grid.GetXY(move.transform.position)
                    + ";destination=" + originalPosition
                    + ";moveFailure=" + move.LastGridMoveFailureReason
                    + ";startAttempts=" + returnStartAttempts
                    + ";commandPending=" + returnCommandPending
                    + ";commandReachableRetries=" + returnCommandReachableRetries
                    + ";commandTypedStatus=" + returnCommandStatus);
            Require(returnPhysicalOrigin == originalPosition
                    || move.LastGridMoveFailureReason == GridMoveFailureReason.None,
                "Actual DirectCommand restock return terminated with a movement failure: "
                + move.LastGridMoveFailureReason);
            actor.SetAiPaused(true);
            report.Add("restock-worker-returned-after-success=PASS;returnedInterior="
                + originalPosition
                + ";logicalOrigin=" + returnLogicalOrigin
                + ";physicalOrigin=" + returnPhysicalOrigin
                + ";gridVersion=" + returnGridVersion
                + ";doorVersion=" + returnDoorVersion
                + ";typedStatus=" + returnStatus
                + ";typedAttempts=" + returnTypedAttempts
                + ";typedPending=" + returnTypedPending
                + ";startAttempts=" + returnStartAttempts
                + ";commandPending=" + returnCommandPending
                + ";commandReachableRetries=" + returnCommandReachableRetries
                + ";commandTypedStatus=" + returnCommandStatus
                + ";actualDirectCommand=true;doorCrossed=" + returnCrossesDoor
                + ";stoppedCompletedAction=" + stoppedCompletedRestockAction
                + ";brokerStatus=" + returnBrokerStatus);
        }
        finally
        {
            actor.SetAiPaused(true);
            actor.Brain?.StopCurrentActionForReplan("qa:wim045:restock-cleanup");
            work.ClearPriorityWorkTarget();
            move.CancelActiveMovement("qa:wim045:restock-cleanup");
            if (door != null && !door.isDestroy)
            {
                doors.SetIndividualRule(door, actorId, originalDoorRule);
            }
            if (items.GetAllStacks().Any(value => value != null
                    && string.Equals(value.StackId, sourceBefore.StackId, StringComparison.Ordinal)))
                items.DeleteStack(sourceBefore.StackId);
            shop.DestroySelf();
            warehouseBuilding.DestroySelf();
        }
        yield return null;
    }

    private static IEnumerator VerifyFullLaunchAndMissingPath(
        DungeonRuntimeLifetimeScope scope,
        CharacterActor actor,
        Grid grid,
        Func<BuildingSO, Vector2Int, BuildableObject> place,
        List<string> report)
    {
        OffenseExpeditionRuntime expeditions = UnityEngine.Object.FindFirstObjectByType<OffenseExpeditionRuntime>(
            FindObjectsInactive.Include);
        OffenseWorldMapRuntime worldMap = UnityEngine.Object.FindFirstObjectByType<OffenseWorldMapRuntime>(
            FindObjectsInactive.Include);
        Require(expeditions != null && worldMap != null,
            "Production offense runtime/world map is missing.");
        Require(expeditions.ActiveExpeditions.Count == 0,
            "Focused full launch requires no pre-existing active expedition.");

        IBlueprintResearchStateService research = scope.Container.Resolve<IBlueprintResearchStateService>();
        bool researchWasAlreadyComplete = OffenseExpeditionAccessRules.IsUnlocked(research.GetState());
        if (!researchWasAlreadyComplete)
        {
            research.GetState().Projects.RestoreCompleted(
                new ResearchProjectId(OffenseExpeditionAccessRules.RequiredResearchId));
        }
        Require(OffenseExpeditionAccessRules.IsUnlocked(research.GetState()),
            "Field-rations research fixture did not publish through the live research authority.");

        HashSet<string> visibleIds = worldMap.VisibleTargets
            .Where(value => value != null)
            .Select(value => value.id)
            .ToHashSet(StringComparer.Ordinal);
        IReadOnlyList<CharacterActor> available = expeditions.GetAvailableMemberActors();
        OffenseTargetDefinition target = worldMap.TargetDefinitions
            .Where(value => value != null
                && visibleIds.Contains(value.id)
                && value.requiredMembers <= available.Count)
            .OrderBy(value => value.requiredMembers)
            .ThenBy(value => value.campaignOrder)
            .ThenBy(value => value.id, StringComparer.Ordinal)
            .FirstOrDefault();
        Require(target != null, "No visible authored target fits the live expedition roster.");
        CharacterActor[] party = available
            .Where(value => value != null && !value.IsDead)
            .OrderBy(value => value == actor ? 0 : 1)
            .ThenBy(value => CharacterPersistentIdentity.Require(value).Value, StringComparer.Ordinal)
            .Take(Mathf.Max(1, target.requiredMembers))
            .ToArray();
        Require(party.Contains(actor), "Focused worker is not available to the production expedition command.");

        IWorldDropZoneQuery dropZones = scope.Container.Resolve<IWorldDropZoneQuery>();
        Require(dropZones.TryGetVisitorEntryPoint(out WorldGridEntryPoint entry),
            "Full launch requires the production visitor entry.");
        IExteriorZoneQuery exteriorZones = scope.Container.Resolve<IExteriorZoneQuery>();
        Require(exteriorZones.TryGetZone(ExteriorZoneType.ExpeditionStaging, out ExteriorZoneMarker staging)
                && staging != null,
            "Full launch MissingPath fixture requires the production expedition staging marker.");
        Door entryDoor = ResolveProductionEntryDoor(
            grid,
            entry,
            out Vector2Int entryDoorPosition,
            out string entryDoorDiagnostics);
        Require(entryDoor != null && entryDoor.IsDungeonEntrance,
            "Full launch MissingPath fixture requires the authored production DoorPosition: "
            + entryDoorDiagnostics);
        Vector2Int outside = ResolveAlignedOutside(
            grid,
            entry.GridPosition,
            entry.OutsidePosition,
            entryDoor);
        Queue<GridMoveStep> originalOutsideEntryPath = grid.GetMovePathTo(
            outside,
            entry.GridPosition);
        Require(originalOutsideEntryPath.Count > 0
                && originalOutsideEntryPath.Any(step =>
                    step.To == entry.GridPosition)
                && PathTraversesDoor(originalOutsideEntryPath, entryDoor),
            "The unchanged authored topology does not connect the production outside, Door, and manager waypoint: "
            + "outside=" + outside
            + ";doorPosition=" + entryDoorPosition
            + ";managerEntry=" + entry.GridPosition
            + ";steps=" + originalOutsideEntryPath.Count
            + ";doorDiagnostics=" + entryDoorDiagnostics);
        BuildingSO wallAsset = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Wall.asset");
        Require(wallAsset != null, "Authored Wall asset is missing.");

        TemporaryExteriorStagingPlacement temporaryStaging =
            staging.centerPos == entry.GridPosition
                ? TemporaryExteriorStagingPlacement.RelocateToNearestInteriorStand(
                    grid,
                    staging,
                    entry.GridPosition)
                : null;
        try
        {

            IProductionItemGateway production = scope.Container.Resolve<IProductionItemGateway>();
            IWorldItemStackRuntime items = scope.Container.Resolve<IWorldItemStackRuntime>();
            IOffensePreparationService preparation = scope.Container.Resolve<IOffensePreparationService>();
            int rationBeforeSeed = CountItem(items, RationItemId);
            Vector2Int supplySource = FindReachableStart(grid, actor, staging.centerPos, minimumDistance: 3);
            Require(production.SpawnOutput(RationItemId, 1, supplySource),
                "Production item gateway could not publish the authored ration fixture.");
            int rationAfterSeed = CountItem(items, RationItemId);
            Require(rationAfterSeed == rationBeforeSeed + 1,
                "Authored ration fixture did not increase physical stock by exactly one.");

            OffenseSupplyLoadout loadout = new OffenseSupplyLoadout();
            loadout.Add(OffenseSupplyType.Rations, 1);
            OffenseExpeditionPreparation launchPreparation =
                new OffenseExpeditionPreparation(supplyCapacity: 1);
            Require(expeditions.TryStartExpedition(
                    target.id,
                    party,
                    loadout,
                    launchPreparation,
                    out OffenseExpeditionRun expedition,
                    out string launchMessage),
                "Full production expedition Launch failed: " + launchMessage);
            Require(expedition != null
                    && expeditions.ActiveExpeditions.Count == 1
                    && !expedition.DepartureCompleted,
                "Full Launch did not publish one pending physical departure.");
            string destinationId = "expedition:" + expedition.ExpeditionId;
            report.Add("full-launch-command=PASS;target=" + target.id
                + ";expedition=" + expedition.ExpeditionId
                + ";researchFixture=" + (!researchWasAlreadyComplete)
                + ";supply=" + RationItemId + "x1;message=" + launchMessage);

            yield return RunRepeatedHaul(actor, preparation, expedition.ExpeditionId);
            OffenseSupplyPackingSnapshot packed = preparation.GetPackingSnapshot(expedition.ExpeditionId);
            Require(packed.IsReady && packed.Required == 1 && packed.Delivered == 1,
                "Full Launch did not reach exact physical package readiness.");
            Require(CountDestination(items, destinationId, RationItemId) == 1
                    && CountItem(items, RationItemId) == rationAfterSeed,
                "Packing readiness consumed or duplicated physical supply before departure.");

            BuildableObject blocker = PlaceDepartureWallBlocker(
                grid,
                wallAsset,
                staging.centerPos,
                entry.GridPosition,
                entryDoor,
                place,
                out Vector2Int blockerPosition,
                out string blockerDiagnostics);
            Require(blocker != null,
                "No legal authored Wall isolates the staging-to-manager entry path while preserving the production Door and Stair: "
                + blockerDiagnostics);
            report.Add("departure-authored-missingpath-fixture=managerEntry="
                + entry.GridPosition
                + ";doorPosition=" + entryDoorPosition
                + ";door=" + entryDoor.name
                + ";doorPreserved=true"
                + ";blockerPosition=" + blockerPosition
                + ";blocker=" + blocker.name
                + ";staging=" + staging.centerPos
                + ";temporaryStaging=" + (temporaryStaging != null)
                + ";diagnostics=" + blockerDiagnostics);

            AbilityMove move = actor.GetAbility<AbilityMove>();
            Require(move != null, "Expedition member lost movement authority.");
            bool sawMissingPath = false;
            yield return WaitUntil(() =>
            {
                sawMissingPath |= move.LastGridMoveFailureReason == GridMoveFailureReason.MissingPath;
                return sawMissingPath;
            }, "Blocked production departure never reported MissingPath.");
            Require(actor.CurrentLifecycleState == CharacterLifecycleState.DepartingExpedition
                    && !expedition.DepartureCompleted
                    && !actor.IsOnExpedition
                    && CountDestination(items, destinationId, RationItemId) == 1
                    && CountItem(items, RationItemId) == rationAfterSeed,
                "MissingPath advanced lifecycle or consumed packed supply before outside completion.");
            report.Add("departure-missingpath-retains-segment-and-package=PASS;lifecycle="
                + actor.CurrentLifecycleState + ";destinationQuantity=1;departureCompleted=false");

            blocker.DestroySelf();
            Require(!entryDoor.isDestroy
                    && entryDoor.BuildingData.GetGridPosList(entryDoor.centerPos)
                        .All(position => ReferenceEquals(
                            grid.GetGridCell(position)?
                                .GetOccupant(GridLayer.Building),
                            entryDoor)),
                "Removing the focused Wall did not preserve the authored entrance Door: "
                + entryDoorDiagnostics);
            yield return WaitUntil(() => expedition.DepartureCompleted,
                "Removing the authored blocker did not resume the retained departure segment.");
            Require(party.All(member => member != null
                        && member.IsOnExpedition
                        && member.transform.position == entry.OutsidePosition)
                    && CountDestination(items, destinationId, RationItemId) == 0
                    && CountItem(items, RationItemId) == rationAfterSeed - 1
                    && expeditions.ActiveExpeditions.Count == 1,
                "Outside completion did not consume exactly one packed ration and complete party departure once.");
            report.Add("full-launch-outside-completion-consumes-physical-supply-once=PASS;"
                + "departureCompleted=true;destinationQuantity=0;physicalDelta=-1;outside="
                + entry.OutsidePosition);
        }
        finally
        {
            temporaryStaging?.Dispose();
        }
    }

    private static IEnumerator RunRepeatedHaul(
        CharacterActor hauler,
        IOffensePreparationService preparation,
        string packageId)
    {
        float startedAt = Time.realtimeSinceStartup;
        while (!preparation.IsPackageReady(packageId)
               && Time.realtimeSinceStartup - startedAt < TimeoutSeconds)
        {
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
                while (!preparation.IsPackageReady(packageId)
                       && ability != null
                       && ability.IsHauling
                       && Time.realtimeSinceStartup - startedAt < TimeoutSeconds)
                    yield return null;
            }
            finally
            {
                UnityEngine.Object.Destroy(action);
            }
            yield return null;
        }
        Require(preparation.IsPackageReady(packageId),
            "Production haul did not make the expedition package ready.");
    }

    private static IEnumerator StartCommittedRestockWithForcedPending(
        CharacterActor actor,
        AbilityWork work,
        Shop shop,
        Grid grid,
        GridPathSearchBroker broker,
        List<string> report,
        string stage)
    {
        Require(actor?.Brain != null
                && work != null
                && shop != null
                && grid != null
                && broker != null
                && report != null,
            "Controlled restock Pending fixture is missing runtime authority: "
            + stage);
        actor.SetAiPaused(true);
        GridPathSearchResult search = grid.SearchPath(actor.GetNowXY());
        if (!work.TrySetPriorityWorkTarget(
                shop,
                BuiltInWorkTypeIds.Restock,
                search,
                out string priorityFailure))
        {
            Require(work.TrySetPriorityWorkTarget(
                    shop,
                    BuiltInWorkTypeIds.Restock,
                    search,
                    out string confirmedFailure),
                "Production restock priority command rejected twice during "
                + stage + ": " + priorityFailure + " | " + confirmedFailure);
        }
        Require(ReferenceEquals(work.PriorityWorkTarget, shop)
                && work.PriorityWorkTypeId == BuiltInWorkTypeIds.Restock,
            "Production restock priority command did not retain its exact target/type during "
            + stage + ".");

        const int admissionAttemptLimit = 32;
        Vector2Int admissionOrigin = actor.GetNowXY();
        Predicate<AIActionSet> workMatcher = actionSet => actionSet is AIWork;
        AIAction committedAction = null;
        AIActionFailure lastAdmissionFailure = AIActionFailure.None;
        int admissionAttempts = 0;
        int admissionDeferrals = 0;
        Require(actor.Brain.PreferWorkActionOnNextDecision(
                BuiltInWorkTypeIds.Restock,
                45f),
            "Brain rejected controlled restock preference during " + stage + ".");
        while (committedAction == null
               && admissionAttempts < admissionAttemptLimit)
        {
            admissionAttempts++;
            Require(actor.GetNowXY() == admissionOrigin
                    && ReferenceEquals(work.PriorityWorkTarget, shop)
                    && work.PriorityWorkTypeId == BuiltInWorkTypeIds.Restock
                    && !work.HasActiveWorkRoutineForDiagnostics
                    && shop.ActiveRestockOperationCount == 0,
                "Production Restock admission changed actor position or command ownership during "
                + stage + ": origin=" + admissionOrigin
                + ";position=" + actor.GetNowXY()
                + ";attempt=" + admissionAttempts + ".");

            CharacterAiActionCandidate candidate = default;
            bool selected = false;
            actor.SetAiPaused(false);
            try
            {
                Require(actor.CanRunAi,
                    "Controlled restock actor cannot select AI work after unpause during "
                    + stage + ": lifecycle=" + actor.CurrentLifecycleState
                    + ";brainEnabled=" + actor.Brain.enabled);
                selected = actor.Brain.TryFindBestScoredAction(
                    workMatcher,
                    out candidate);
                if (selected)
                {
                    Require(candidate.ActionSet is AIWork
                            && ReferenceEquals(candidate.Destination, shop),
                        "Production Restock admission selected a different action/target during "
                        + stage + ": candidate=" + candidate.DebugLabel
                        + ";failure=" + candidate.Failure);
                    if (actor.Brain.TryCommitActionCandidate(
                            candidate,
                            out AIActionFailure commitFailure))
                    {
                        committedAction = candidate.Action;
                        Require(ReferenceEquals(actor.Brain.bestAction, committedAction)
                                && !actor.Brain.isBestActionEnd
                                && !committedAction.HasStarted
                                && ReferenceEquals(committedAction.destination, shop),
                            "Production Restock commit did not retain the selected action/target during "
                            + stage + ".");
                    }
                    else
                    {
                        lastAdmissionFailure = commitFailure;
                    }
                }
                else
                {
                    lastAdmissionFailure = candidate.Failure;
                }
            }
            finally
            {
                actor.SetAiPaused(true);
            }

            if (committedAction != null)
                break;

            Require(lastAdmissionFailure.HasFailure
                    && lastAdmissionFailure.IsDeferred,
                "Production Restock admission failed terminally during " + stage
                + ": selected=" + selected
                + ";attempt=" + admissionAttempts
                + ";candidate=" + candidate.DebugLabel
                + ";failure=" + lastAdmissionFailure);
            Require(actor.Brain.IsActionPreferredForNextDecision<AIWork>()
                    && actor.Brain.TryGetPreferredWorkType(
                        out WorkTypeId deferredWorkType)
                    && deferredWorkType == BuiltInWorkTypeIds.Restock
                    && ReferenceEquals(work.PriorityWorkTarget, shop),
                "Deferred Restock admission lost its preferred work/target during "
                + stage + ": attempt=" + admissionAttempts
                + ";failure=" + lastAdmissionFailure);
            Require(lastAdmissionFailure.Kind != AIActionFailureKind.PathSearchDeferred
                    || actor.Brain.IsPathSearchDeferred,
                "Deferred Restock commit lost its production path-search ownership during "
                + stage + ": attempt=" + admissionAttempts
                + ";failure=" + lastAdmissionFailure);
            admissionDeferrals++;
            yield return null;
        }

        Require(committedAction != null,
            "Production Restock admission did not commit within its bounded frame budget during "
            + stage + ": attempts=" + admissionAttempts
            + ";deferrals=" + admissionDeferrals
            + ";lastFailure=" + lastAdmissionFailure);
        report.Add("restock-command-admission=PASS;stage=" + stage
            + ";attempts=" + admissionAttempts
            + ";deferrals=" + admissionDeferrals
            + ";originPreserved=true;target=shop;ownership=priority-restock"
            + ";pickupBudgetAppliedAfterCommit=true");

        broker.BeginFrame(0, enforceBudget: true);
        committedAction.actionset.Execute(actor);
        Require(work.HasActiveWorkRoutineForDiagnostics
                && string.Equals(
                    actor.Brain.CurrentActionPhaseDetail,
                    "restock:pickup-path-pending",
                    StringComparison.Ordinal)
                && broker.SearchesThisFrame == 0
                && broker.BudgetDeferralsThisFrame == 1,
            "The zero-budget production Restock command did not expose exactly one "
            + "real broker deferral during " + stage
            + ": phase=" + actor.Brain.CurrentActionPhaseDetail
            + ";searches=" + broker.SearchesThisFrame
            + ";deferrals=" + broker.BudgetDeferralsThisFrame
            + ";failure=" + actor.Brain.LastActionFailure);
    }

    private static Shop PlaceAuthoredShop(
        Grid grid,
        CharacterActor actor,
        Func<BuildingSO, Vector2Int, BuildableObject> place,
        IShopStockCatalog stockCatalog,
        Vector2Int routeStart,
        out SaleItem saleItem,
        out string diagnostics)
    {
        saleItem = null;
        diagnostics = string.Empty;
        const string preferredPath =
            "Assets/Resources/SO/Building/Modular/S01_판매카운터.asset";
        BuildingSO preferred = AssetDatabase.LoadAssetAtPath<BuildingSO>(preferredPath);
        BuildingSO[] assets = preferred != null
            && preferred.id == 1012
            && preferred.runtimeArchetype == BuildingRuntimeArchetypeKind.Shop
                ? new[] { preferred }
                : Array.Empty<BuildingSO>();
        List<string> failures = new();
        foreach (BuildingSO asset in assets)
        {
            Vector2Int[] candidates = FindPlacementCandidates(grid, actor, asset)
                .OrderBy(value => Manhattan(value, routeStart))
                .Take(8)
                .ToArray();
            if (candidates.Length == 0)
            {
                failures.Add(asset.name + "=no-reachable-placement");
                continue;
            }
            foreach (Vector2Int position in candidates)
            {
                BuildableObject placed = place(asset, position);
                if (placed is not Shop shop)
                {
                    failures.Add(asset.name + "@" + position + "=runtime-not-shop");
                    placed?.DestroySelf();
                    continue;
                }
                RetailProductSnapshot[] products = shop.ProductSnapshots.ToArray();
                string productDiagnostics = string.Join(",", products.Select(product =>
                {
                    bool resolved = stockCatalog.TryGetSaleItem(product.Id, out SaleItem resolvedItem);
                    return product.Id + ":resolved=" + resolved
                        + ":item=" + (resolvedItem != null
                            ? resolvedItem.ItemDefinitionId.Value
                            : "none");
                }));
                saleItem = products
                    .Select(product => stockCatalog.TryGetSaleItem(product.Id, out SaleItem item)
                        ? item
                        : null)
                    .FirstOrDefault(item => item != null
                        && item.ItemDefinitionId.IsValid
                        && !PhysicalItemIds.TryGetEquipmentDefinitionId(item.ItemDefinitionId.Value, out _)
                        && !PhysicalItemIds.IsEquipmentModule(item.ItemDefinitionId.Value));
                bool actorReachable = WorkTargetSelectionRules.IsReachable(
                    shop, grid.SearchPath(actor.GetNowXY()));
                bool routeReachable = WorkTargetSelectionRules.IsReachable(
                    shop, grid.SearchPath(routeStart));
                if (saleItem != null && actorReachable && routeReachable)
                {
                    diagnostics = "asset=" + asset.name
                        + ";anchor=" + position
                        + ";shopId=" + shop.BuildingData.id
                        + ";products=" + products.Length + "[" + productDiagnostics + "]"
                        + ";product=" + saleItem.ItemDefinitionId.Value
                        + ";candidates=" + candidates.Length
                        + ";actorReachable=true"
                        + ";outsideReachable=true";
                    return shop;
                }
                failures.Add(asset.name + "@" + position
                    + ";shopId=" + shop.BuildingData.id
                    + ";products=" + products.Length + "[" + productDiagnostics + "]"
                    + (saleItem == null ? "=no-physical-sale"
                        : "=post-placement-unreachable/actor=" + actorReachable
                            + "/outside=" + routeReachable));
                shop.DestroySelf();
                saleItem = null;
            }
        }
        diagnostics = "preferred=" + preferredPath
            + ";asset=" + (preferred != null ? preferred.name : "missing")
            + ";assets=" + assets.Length
            + ";attempts=" + string.Join(" | ", failures);
        return null;
    }

    private static IWarehouseFacility PlaceAuthoredWarehouse(
        Grid grid,
        CharacterActor actor,
        Func<BuildingSO, Vector2Int, BuildableObject> place,
        StockCategory category,
        int minimumCapacity,
        Vector2Int routeStart,
        Vector2Int managerEntry,
        Door entranceDoor,
        out Vector2Int pickupStand,
        out string diagnostics)
    {
        pickupStand = default;
        diagnostics = string.Empty;
        List<string> failures = new();
        GridPathSearchResult routeComponent = grid.SearchPath(routeStart);
        foreach (BuildingSO asset in AssetDatabase.FindAssets(
                     "t:BuildingSO", new[] { "Assets/Resources/SO/Building" })
                 .Select(AssetDatabase.GUIDToAssetPath)
                 .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
                 .Where(value => value != null
                     && value.runtimeArchetype == BuildingRuntimeArchetypeKind.Facility
                     && value.GetStorageCapacity() >= minimumCapacity
                     && (value.StoresAllCategories() || value.GetStorageCategory() == category))
                 .OrderBy(value => value.id))
        {
            Vector2Int[] candidates = FindPlacementCandidates(grid, actor, asset)
                .Where(candidate => asset.GetGridPosList(candidate).Any(position =>
                    routeComponent.ContainsPosition(position)
                    || Adjacent(position).Any(routeComponent.ContainsPosition)))
                .OrderBy(candidate => Manhattan(candidate, routeStart))
                .Take(24)
                .ToArray();
            if (candidates.Length == 0)
            {
                failures.Add(asset.name + "=no-actor-and-outside-reachable-placement");
                continue;
            }
            foreach (Vector2Int position in candidates)
            {
                BuildableObject placed = place(asset, position);
                if (placed is IWarehouseFacility warehouse
                    && warehouse.HasWarehouseInventory
                    && warehouse.Inventory != null
                    && warehouse.Inventory.Accepts(category)
                    && warehouse.Inventory.MaxMassGrams > 0L
                    && TryResolvePickupStand(grid, placed.centerPos, out Vector2Int stand)
                    && grid.GetGridCell(stand)?.AreaType == GridCellAreaType.DungeonInterior)
                {
                    Queue<GridMoveStep> route = grid.GetMovePathTo(routeStart, stand);
                    bool crossesDoor = PathTraversesDoor(route, entranceDoor);
                    bool crossesManagerEntry = route != null
                        && route.Any(step => step.To == managerEntry);
                    if (route != null
                        && route.Count > 0
                        && crossesDoor
                        && crossesManagerEntry)
                    {
                        pickupStand = stand;
                        diagnostics = "asset=" + asset.name
                            + ";anchor=" + position
                            + ";destination=" + WarehouseStorageIdentity.RequireDestinationId(warehouse)
                            + ";pickupStand=" + pickupStand
                            + ";candidates=" + candidates.Length
                            + ";outsideSteps=" + route.Count
                            + ";doorAndManagerEntry=true";
                        return warehouse;
                    }

                    failures.Add(asset.name + "@" + position
                        + "=outside-route-invalid/stand=" + stand
                        + "/steps=" + (route?.Count ?? -1)
                        + "/door=" + crossesDoor
                        + "/entry=" + crossesManagerEntry);
                    placed.DestroySelf();
                    continue;
                }
                failures.Add(asset.name + "@" + position
                    + "=no-physical-pickup-stand");
                placed?.DestroySelf();
            }
        }
        diagnostics = "attempts=" + string.Join(" | ", failures);
        return null;
    }

    private static IEnumerable<Vector2Int> FindPlacementCandidates(
        Grid grid,
        CharacterActor actor,
        BuildingSO asset)
    {
        if (grid == null || actor == null || asset == null)
            yield break;
        GridPathSearchResult search = grid.SearchPath(actor.GetNowXY());
        foreach (Vector2Int candidate in grid.GetCells()
                     .Where(cell => cell != null
                         && cell.AreaType == GridCellAreaType.DungeonInterior)
                     .Select(cell => cell.Position)
                     .OrderByDescending(value => Manhattan(value, actor.GetNowXY())))
        {
            IReadOnlyList<Vector2Int> positions = asset.GetGridPosList(candidate);
            if (positions.Count == 0
                || !positions.All(cell => grid.GetGridCell(cell) is GridCell gridCell
                    && gridCell.CanBuildInArea(asset)
                    && gridCell.CanOccupy(asset.Placement.Layer)))
                continue;
            bool reachable = asset.IsGridMovement
                ? positions.Any(search.ContainsPosition)
                : positions.Any(cell => Adjacent(cell).Any(adjacent =>
                    grid.IsValidGridPos(adjacent)
                    && grid.IsWalkable(adjacent)
                    && search.ContainsPosition(adjacent)));
            if (reachable)
                yield return candidate;
        }
    }

    private static Vector2Int ResolveAlignedOutside(
        Grid grid,
        Vector2Int entry,
        Vector3 authoredOutsidePosition,
        Door entranceDoor = null)
    {
        Vector2Int authoredOutside = grid.GetXY(authoredOutsidePosition);
        Queue<GridMoveStep> authoredPath = grid.IsValidGridPos(authoredOutside)
            ? grid.GetMovePathTo(authoredOutside, entry)
            : null;
        if (grid.IsValidGridPos(authoredOutside)
            && authoredOutside != entry
            && entranceDoor?.ContainsCell(authoredOutside) != true
            && grid.IsWalkable(authoredOutside)
            && grid.GetGridCell(authoredOutside)?.AreaType != GridCellAreaType.DungeonInterior
            && authoredPath != null
            && authoredPath.Count > 0
            && authoredPath.Any(step => step.To == entry)
            && (entranceDoor == null
                || PathTraversesDoor(authoredPath, entranceDoor)))
            return authoredOutside;

        IEnumerable<Vector2Int> structuralAnchors = new[] { entry };
        if (entranceDoor?.BuildingData != null)
        {
            structuralAnchors = structuralAnchors.Concat(
                entranceDoor.BuildingData.GetGridPosList(
                    entranceDoor.centerPos));
        }
        Vector2Int[] candidates = structuralAnchors
            .SelectMany(Adjacent)
            .Distinct()
            .Where(grid.IsValidGridPos)
            .Where(position => grid.IsWalkable(position)
                && grid.GetGridCell(position)?.AreaType != GridCellAreaType.DungeonInterior
                && grid.GetGridCell(position)?.GetOccupant(GridLayer.Building) == null
                && entranceDoor?.ContainsCell(position) != true)
            .Select(position => new
            {
                Position = position,
                Path = grid.GetMovePathTo(position, entry)
            })
            .Where(candidate => candidate.Path != null
                && candidate.Path.Count > 0
                && candidate.Path.Any(step => step.To == entry)
                && (entranceDoor == null
                    || PathTraversesDoor(candidate.Path, entranceDoor)))
            .OrderBy(candidate => grid.GetGridCell(candidate.Position)?.AreaType == GridCellAreaType.ExteriorPath ? 0 : 1)
            .ThenBy(candidate => Manhattan(candidate.Position, authoredOutside))
            .ThenBy(candidate => candidate.Position.y)
            .ThenBy(candidate => candidate.Position.x)
            .Select(candidate => candidate.Position)
            .ToArray();
        Require(candidates.Length > 0,
            "The production entrance has no explicit outside approach with a non-empty path to the manager waypoint: "
            + "authoredOutside=" + authoredOutside
            + ";authoredValid=" + grid.IsValidGridPos(authoredOutside)
            + ";authoredWalkable=" + grid.IsWalkable(authoredOutside)
            + ";authoredArea=" + grid.GetGridCell(authoredOutside)?.AreaType
            + ";authoredInDoor=" + (entranceDoor?.ContainsCell(authoredOutside) == true)
            + ";authoredSteps=" + (authoredPath?.Count ?? -1)
            + ";entry=" + entry
            + ";door=" + (entranceDoor != null
                ? entranceDoor.centerPos.ToString()
                : "none"));
        return candidates[0];
    }

    private static Door ResolveProductionEntryDoor(
        Grid grid,
        WorldGridEntryPoint entry,
        out Vector2Int doorPosition,
        out string diagnostics)
    {
        doorPosition = grid.GetXY(entry.DoorPosition);
        GridCell cell = grid.GetGridCell(doorPosition);
        IGridOccupant occupant = cell?.GetOccupant(GridLayer.Building);
        Door door = occupant as Door;
        diagnostics = "doorWorld=" + entry.DoorPosition
            + ";doorPosition=" + doorPosition
            + ";managerEntry=" + entry.GridPosition
            + ";outside=" + grid.GetXY(entry.OutsidePosition)
            + ";cell=" + (cell != null)
            + ";area=" + cell?.AreaType
            + ";walkable=" + grid.IsWalkable(doorPosition)
            + ";occupant=" + (occupant == null
                ? "none"
                : occupant.GetType().Name)
            + ";occupantCenter=" + (occupant is BuildableObject building
                ? building.centerPos.ToString()
                : "none")
            + ";footprint=" + (door?.BuildingData != null
                ? string.Join(",", door.BuildingData.GetGridPosList(door.centerPos))
                : "none");
        return door;
    }

    private static IEnumerable<Vector2Int> EnumerateManhattanTrace(
        Vector2Int from,
        Vector2Int to)
    {
        Vector2Int current = from;
        yield return current;
        while (current.x != to.x)
        {
            current.x += current.x < to.x ? 1 : -1;
            yield return current;
        }
        while (current.y != to.y)
        {
            current.y += current.y < to.y ? 1 : -1;
            yield return current;
        }
    }

    private static string DescribeRestockRoute(
        Grid grid,
        IDoorAccessQuery doorAccess,
        GridTraversalContext traversal,
        Vector2Int outside,
        Vector2Int entry,
        Vector2Int pickup)
    {
        Queue<GridMoveStep> entryPath = grid.GetMovePathTo(outside, entry);
        Queue<GridMoveStep> pickupPath = grid.GetMovePathTo(outside, pickup);
        IEnumerable<Vector2Int> pathPositions = (entryPath ?? new Queue<GridMoveStep>())
            .Concat(pickupPath ?? new Queue<GridMoveStep>())
            .SelectMany(step => new[] { step.From, step.To });
        Vector2Int[] relevantPositions = pathPositions
            .Concat(EnumerateManhattanTrace(outside, entry))
            .Concat(EnumerateManhattanTrace(entry, pickup))
            .Concat(new[] { outside, entry, pickup })
            .SelectMany(position => new[] { position }.Concat(Adjacent(position)))
            .Where(grid.IsValidGridPos)
            .Distinct()
            .Take(192)
            .ToArray();
        string cells = string.Join(
            ",",
            relevantPositions.Select(position =>
            {
                GridCell cell = grid.GetGridCell(position);
                IGridOccupant occupant = cell?.GetOccupant(GridLayer.Building);
                bool allowed = doorAccess.CanTraverse(
                    grid,
                    position,
                    traversal,
                    out string denial);
                return position
                    + ":area=" + cell?.AreaType
                    + ":walkable=" + grid.IsWalkable(position)
                    + ":occupant=" + (occupant == null
                        ? "none"
                        : occupant.GetType().Name)
                    + ":allowed=" + allowed
                    + ":denial=" + denial
                    + ":links=[" + string.Join(",", cell?.TraversalLinks
                        .Select(link => link.To
                            + "/" + link.MoveType
                            + "/" + (link.Through == null
                                ? "none"
                                : link.Through.GetType().Name))
                        ?? Array.Empty<string>()) + "]";
            }));
        GridCell pickupCell = grid.GetGridCell(pickup);
        return "outside=" + outside
            + ";entry=" + entry
            + ";pickup=" + pickup
            + ";pickupArea=" + pickupCell?.AreaType
            + ";pickupWalkable=" + grid.IsWalkable(pickup)
            + ";pickupOccupant=" + pickupCell?
                .GetOccupant(GridLayer.Building)?.GetType().Name
            + ";entrySteps=" + (entryPath?.Count ?? -1)
            + ";pickupSteps=" + (pickupPath?.Count ?? -1)
            + ";entryPath=[" + DescribePath(entryPath) + "]"
            + ";pickupPath=[" + DescribePath(pickupPath) + "]"
            + ";relevantCells=" + relevantPositions.Length
            + ";cells=[" + cells + "]";
    }

    private static string DescribeActorPathRequest(
        IGridPathSearchBroker broker,
        Grid grid,
        Vector2Int start,
        Vector2Int destination,
        GridTraversalContext traversalContext) => broker is GridPathSearchBroker concrete
            ? concrete.DescribeExactSearchForDiagnostics(
                grid,
                start,
                destination,
                traversalContext)
            : "broker=" + (broker?.GetType().Name ?? "none");

    private static bool PathTraversesDoor(
        IEnumerable<GridMoveStep> path,
        Door door) => path != null
        && door != null
        && path.Any(step => door.ContainsCell(step.From)
            || door.ContainsCell(step.To)
            || ReferenceEquals(step.MovementOccupant, door)
            || ReferenceEquals(step.DestinationOccupant, door));

    private static Vector2Int ResolvePickupStand(Grid grid, Vector2Int itemPosition)
    {
        Require(TryResolvePickupStand(grid, itemPosition, out Vector2Int stand),
            "Stored physical stack has no real pickup stand.");
        return stand;
    }

    private static bool TryResolvePickupStand(
        Grid grid,
        Vector2Int itemPosition,
        out Vector2Int stand)
    {
        stand = default;
        if (grid.IsValidGridPos(itemPosition) && grid.IsWalkable(itemPosition))
        {
            stand = itemPosition;
            return true;
        }
        return grid.TryFindNearbyWalkablePositionOnSameFloor(itemPosition, out stand, 1);
    }

    private static Vector2Int FindReachableStart(
        Grid grid,
        CharacterActor actor,
        Vector2Int destination,
        int minimumDistance)
    {
        GridPathSearchResult reachable = grid.SearchPath(actor.GetNowXY());
        Vector2Int start = reachable.GetReachablePositions()
            .Where(value => value != destination
                && Manhattan(value, destination) >= minimumDistance
                && grid.GetGridCell(value)?.GetOccupant(GridLayer.Building) == null)
            .OrderByDescending(value => Manhattan(value, destination))
            .FirstOrDefault();
        Require(grid.IsValidGridPos(start) && reachable.ContainsPosition(start),
            "No reachable staging cell exists for the focused consumer route.");
        return start;
    }

    private sealed class TemporaryExteriorStagingPlacement : IDisposable
    {
        private readonly Grid grid;
        private readonly ExteriorZoneMarker staging;
        private readonly GridLayer layer;
        private readonly Vector2Int originalPosition;
        private readonly Vector2Int[] originalCells;
        private readonly ExteriorZoneSaveData originalState;
        private bool disposed;

        private TemporaryExteriorStagingPlacement(
            Grid grid,
            ExteriorZoneMarker staging,
            GridLayer layer,
            Vector2Int originalPosition,
            Vector2Int[] originalCells,
            ExteriorZoneSaveData originalState)
        {
            this.grid = grid;
            this.staging = staging;
            this.layer = layer;
            this.originalPosition = originalPosition;
            this.originalCells = originalCells;
            this.originalState = originalState;
        }

        public static TemporaryExteriorStagingPlacement RelocateToNearestInteriorStand(
            Grid grid,
            ExteriorZoneMarker staging,
            Vector2Int managerEntry)
        {
            Require(grid != null && staging != null && staging.BuildingData != null,
                "MissingPath fixture cannot relocate an unconfigured expedition staging marker.");

            BuildingSO definition = staging.BuildingData;
            GridLayer layer = definition.Placement.Layer;
            Vector2Int originalPosition = staging.centerPos;
            Vector2Int[] originalCells = staging.buildPoses.ToArray();
            Require(originalCells.Length > 0,
                "MissingPath fixture cannot relocate an expedition staging marker without grid occupancy.");
            Vector2Int fixturePosition = FindNearestInteriorStand(
                grid,
                definition,
                managerEntry);
            TemporaryExteriorStagingPlacement placement = new(
                grid,
                staging,
                layer,
                originalPosition,
                originalCells,
                staging.CreateSaveData());

            Require(grid.RemoveOccupant(staging, layer, originalCells, false),
                "MissingPath fixture could not release the production expedition staging occupancy.");
            staging.SetRuntimeGridPosition(fixturePosition);
            if (grid.RegisterOccupant(staging, layer, staging.buildPoses, false))
            {
                return placement;
            }

            staging.SetRuntimeGridPosition(originalPosition);
            bool restored = grid.RegisterOccupant(staging, layer, originalCells, false);
            staging.ApplySaveData(placement.originalState);
            Require(restored,
                "MissingPath fixture could not restore staging after its temporary interior placement was rejected.");
            throw new InvalidOperationException(
                "MissingPath fixture could not register the production staging marker at its interior stand.");
        }

        public void Dispose()
        {
            if (disposed)
            {
                return;
            }

            disposed = true;
            bool releasedFixture = grid.RemoveOccupant(
                staging,
                layer,
                staging.buildPoses,
                false);
            staging.SetRuntimeGridPosition(originalPosition);
            bool restored = grid.RegisterOccupant(
                staging,
                layer,
                originalCells,
                false);
            staging.ApplySaveData(originalState);
            Require(releasedFixture
                    && restored
                    && staging.centerPos == originalPosition
                    && staging.buildPoses.SequenceEqual(originalCells),
                "MissingPath fixture did not restore the production expedition staging marker.");
        }
    }

    private static Vector2Int FindNearestInteriorStand(
        Grid grid,
        BuildingSO stagingDefinition,
        Vector2Int managerEntry)
    {
        foreach (Vector2Int candidate in Adjacent(managerEntry)
                     .OrderBy(position => position.y)
                     .ThenBy(position => position.x))
        {
            Vector2Int[] footprint = stagingDefinition.GetGridPosList(candidate).ToArray();
            if (footprint.Length == 0
                || !grid.IsWalkable(candidate)
                || footprint.Any(position => position == managerEntry)
                || footprint.Any(position => grid.GetGridCell(position) is not GridCell cell
                    || cell.AreaType != GridCellAreaType.DungeonInterior
                    || !cell.CanOccupy(stagingDefinition.Placement.Layer)))
            {
                continue;
            }

            Queue<GridMoveStep> path = grid.GetMovePathTo(candidate, managerEntry);
            if (path != null && path.Count > 0)
            {
                return candidate;
            }
        }

        throw new InvalidOperationException(
            "MissingPath fixture requires an adjacent walkable DungeonInterior stand for the existing expedition staging marker."
            + " managerEntry=" + managerEntry);
    }

    private static bool CanPlace(Grid grid, BuildingSO asset, Vector2Int position) =>
        grid != null
        && asset != null
        && asset.GetGridPosList(position).Count > 0
        && asset.GetGridPosList(position).All(cell => grid.GetGridCell(cell) is GridCell gridCell
            && gridCell.CanBuildInArea(asset)
            && gridCell.CanOccupy(asset.Placement.Layer));

    private static BuildableObject PlaceDepartureWallBlocker(
        Grid grid,
        BuildingSO wallAsset,
        Vector2Int staging,
        Vector2Int managerEntry,
        Door entryDoor,
        Func<BuildingSO, Vector2Int, BuildableObject> place,
        out Vector2Int blockerPosition,
        out string diagnostics)
    {
        blockerPosition = default;
        Queue<GridMoveStep> originalPath = grid.GetMovePathTo(
            staging,
            managerEntry);
        List<string> attempts = new();
        if (wallAsset == null || originalPath == null || originalPath.Count == 0)
        {
            diagnostics = "wallAsset=" + (wallAsset != null)
                + ";staging=" + staging
                + ";managerEntry=" + managerEntry
                + ";originalSteps=" + (originalPath?.Count ?? -1);
            return null;
        }

        Vector2Int[] candidates = originalPath
            .SelectMany(step => new[] { step.From, step.To })
            .Where(position => position != staging
                && !entryDoor.ContainsCell(position))
            .Distinct()
            .OrderBy(position => Manhattan(position, managerEntry))
            .ThenBy(position => position.y)
            .ThenBy(position => position.x)
            .ToArray();
        foreach (Vector2Int candidate in candidates)
        {
            GridCell cell = grid.GetGridCell(candidate);
            IGridOccupant occupant = cell?.GetOccupant(
                wallAsset.Placement.Layer);
            if (!CanPlace(grid, wallAsset, candidate))
            {
                attempts.Add(candidate
                    + "=placement-rejected/area=" + cell?.AreaType
                    + "/occupant=" + (occupant == null
                        ? "none"
                        : occupant.GetType().Name));
                continue;
            }

            BuildableObject blocker = place(wallAsset, candidate);
            Queue<GridMoveStep> blockedPath = grid.GetMovePathTo(
                staging,
                managerEntry);
            bool doorPreserved = !entryDoor.isDestroy
                && entryDoor.BuildingData.GetGridPosList(entryDoor.centerPos)
                    .All(position => ReferenceEquals(
                        grid.GetGridCell(position)?
                            .GetOccupant(GridLayer.Building),
                        entryDoor));
            attempts.Add(candidate
                + "=placed/remainingSteps=" + (blockedPath?.Count ?? -1)
                + "/doorPreserved=" + doorPreserved);
            if (blocker != null
                && blockedPath != null
                && blockedPath.Count == 0
                && doorPreserved)
            {
                blockerPosition = candidate;
                diagnostics = "staging=" + staging
                    + ";managerEntry=" + managerEntry
                    + ";originalSteps=" + originalPath.Count
                    + ";originalPath=" + DescribePath(originalPath)
                    + ";attempts=[" + string.Join(" | ", attempts) + "]";
                return blocker;
            }

            blocker?.DestroySelf();
        }

        diagnostics = "staging=" + staging
            + ";managerEntry=" + managerEntry
            + ";originalSteps=" + originalPath.Count
            + ";originalPath=" + DescribePath(originalPath)
            + ";attempts=[" + string.Join(" | ", attempts) + "]";
        return null;
    }

    private static string DescribePath(IEnumerable<GridMoveStep> path) =>
        string.Join(
            ">",
            (path ?? Array.Empty<GridMoveStep>()).Select(step =>
                step.From
                + "->" + step.To
                + "/" + step.MoveType
                + "/movement=" + (step.MovementOccupant == null
                    ? "none"
                    : step.MovementOccupant.GetType().Name)
                + "/destination=" + (step.DestinationOccupant == null
                    ? "none"
                    : step.DestinationOccupant.GetType().Name)));

    private static IEnumerable<Vector2Int> Adjacent(Vector2Int position)
    {
        yield return position + Vector2Int.left;
        yield return position + Vector2Int.right;
        yield return position + Vector2Int.up;
        yield return position + Vector2Int.down;
    }

    private static int CountItem(IWorldItemStackRuntime items, string itemId) =>
        items.GetAllStacks()
            .Where(value => value != null
                && string.Equals(value.ItemId, itemId, StringComparison.Ordinal))
            .Sum(value => value.Quantity);

    private static int CountDestination(
        IWorldItemStackRuntime items,
        string destinationId,
        string itemId) => items.GetAllStacks()
        .Where(value => value != null
            && string.Equals(value.ItemId, itemId, StringComparison.Ordinal)
            && string.Equals(value.DestinationId, destinationId, StringComparison.Ordinal))
        .Sum(value => value.Quantity);

    private static int CountState(
        IWorldItemStackRuntime items,
        string itemId,
        WorldItemStackState state) => items.GetAllStacks()
        .Where(value => value != null
            && string.Equals(value.ItemId, itemId, StringComparison.Ordinal)
            && value.State == state)
        .Sum(value => value.Quantity);

    private static string DescribeStateStacks(
        IWorldItemStackRuntime items,
        string itemId,
        WorldItemStackState state)
    {
        string[] stacks = items.GetAllStacks()
            .Where(value => value != null
                && string.Equals(value.ItemId, itemId, StringComparison.Ordinal)
                && value.State == state)
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .Select(value => value.StackId
                + "@" + value.Position
                + "/quantity=" + value.Quantity
                + "/destination=" + value.DestinationId
                + "/recoveryOwner=" + value.RecoveryOwnerOperationId
                + "/recoverySource=" + value.RecoverySourceStackId)
            .ToArray();
        return stacks.Length == 0 ? "none" : string.Join(",", stacks);
    }

    private static int Manhattan(Vector2Int a, Vector2Int b) =>
        Mathf.Abs(a.x - b.x) + Mathf.Abs(a.y - b.y);

    private static IEnumerator WaitUntil(Func<bool> condition, string failure)
    {
        float startedAt = Time.realtimeSinceStartup;
        while (!condition() && Time.realtimeSinceStartup - startedAt < TimeoutSeconds)
            yield return null;
        Require(condition(), failure);
    }

    private static IEnumerator WaitUntil(Func<bool> condition, Func<string> failure)
    {
        float startedAt = Time.realtimeSinceStartup;
        while (!condition() && Time.realtimeSinceStartup - startedAt < TimeoutSeconds)
            yield return null;
        Require(condition(), failure());
    }

    private static void Require(bool condition, string failure)
    {
        if (!condition)
            throw new InvalidOperationException(failure);
    }
}
#endif
