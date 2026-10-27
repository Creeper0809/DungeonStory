#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using DungeonStory.Foundation;
using Newtonsoft.Json;
using UnityEngine;

public static class R28OwnedQuerySnapshotDebugScenarios
{
    public static void RunOrThrow()
    {
        ICombatEquipmentRuntime equipment =
            OffenseEditorTestDependencies.CreateCombatEquipmentRuntime();
        VerifyCraftQueue(equipment);
        VerifyApparelOrders();
        VerifyEmployment(equipment);
        VerifyGrandProject();
        VerifyOffenseRegions();
        VerifyOffenseWorldAndTravel();
        Debug.Log("R28_OWNED_QUERY_SNAPSHOTS=PASS");
    }

    private static void VerifyCraftQueue(ICombatEquipmentRuntime equipment)
    {
        var crafting = (CombatEquipmentCraftingRuntime)typeof(CombatEquipmentRuntime)
            .GetField("crafting", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(equipment);
        var orders = (List<CombatEquipmentCraftOrderSaveData>)
            typeof(CombatEquipmentCraftingRuntime)
                .GetProperty("orders", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(crafting);
        var seeded = (CombatEquipmentCraftOrderSaveData)Call(
            typeof(CombatEquipmentCraftTransactionFixture), "CreateOrder", "r28-snapshot");
        seeded.contributions.Add(new CraftContributionSaveData
        {
            characterId = "character:r28:worker", contributedWork = 1f
        });
        orders.Add(seeded);
        string before = Json(crafting.CaptureOrders());
        long revision = seeded.commandRevision;

        CombatEquipmentCraftOrderSaveData query = equipment.CraftQueue.Single();
        query.completedWork = 3f;
        query.workerPolicy.minimumCareerRank = 99;
        query.workerPolicy.specificCharacterIds.Add("character:r28:query-only");
        query.contributions[0].contributedWork = 99f;
        query.contributions.Clear();
        Require(Json(crafting.CaptureOrders()) == before
            && Json(equipment.CraftQueue) == before
            && seeded.commandRevision == revision,
            "Craft queue query changed live scalar, nested policy, contribution or revision.");

        Require(equipment.SetCraftWorkerPolicy(
                seeded.orderId,
                WorkerSelectionPolicySaveData.Anyone(WorkerCandidateSortMode.Nearest),
                out string failure),
            "Craft worker policy command failed: " + failure);
        CombatEquipmentCraftOrderSaveData changed = equipment.CraftQueue.Single();
        Require(changed.workerPolicy.sortMode == WorkerCandidateSortMode.Nearest
            && changed.commandRevision == revision + 1
            && Json(crafting.CaptureOrders()) != before,
            "Craft command changed only a detached query copy.");
    }

    private static void VerifyApparelOrders()
    {
        var runtime = (ApparelWorkOrderRuntime)Call(
            typeof(ApparelTerminalAuthorityDebugScenarios), "CreateRuntime", new object[] { null });
        var seeded = (ApparelWorkOrderSaveData)Call(
            typeof(ApparelTerminalAuthorityDebugScenarios), "SeedOrder", runtime, "r28-snapshot");
        seeded.facilityInstanceId = "building:qa:r28-apparel-snapshot";
        runtime.PublishRestoreState(runtime.PrepareRestoreState(
            new[] { seeded }, runtime.CaptureTerminalStates()));
        string before = Json(runtime.CaptureOrders());
        int version = runtime.Version;
        ApparelWorkOrderSaveData query = runtime.Orders.Single();
        query.completedWork = 99f;
        query.workerPolicy.minimumCareerRank = 99;
        query.workerPolicy.specificCharacterIds.Add("character:r28:query-only");
        query.rejectedRecoveryStackIds.Add("stack:r28:query-only");
        Require(Json(runtime.CaptureOrders()) == before
            && Json(runtime.Orders) == before
            && runtime.Version == version,
            "Apparel query changed live scalar, nested policy, stack list or version.");

        bool cancelled = runtime.Cancel(seeded.orderId, out DomainFailure cancelFailure);
        Require(cancelled
            && runtime.Orders.Count == 0
            && runtime.CaptureOrders().Length == 0
            && runtime.Version == version + 1,
            "Apparel cancellation did not remove the owner order: "
            + cancelFailure.Code + ", remaining=" + runtime.Orders.Count
            + ", version=" + runtime.Version + ", expectedVersion=" + (version + 1));
    }

    private static void VerifyEmployment(ICombatEquipmentRuntime equipment)
    {
        const string employeeId = "character:r28:employee";
        const string mercenaryId = "character:r28:mercenary";
        object world = CreateNested(
            typeof(EmploymentSettlementG02DebugScenarios), "MutableCharacterWorld",
            Array.Empty<CharacterActor>(), new[] { employeeId, mercenaryId });
        var treasury = new TreasuryEconomyAggregateStateStore(
            new DungeonRuntimeAggregateRootStore());
        treasury.Current.Wages.Add(employeeId, new EmployeeWageState
        {
            characterId = employeeId, rolePremium = 3
        });
        treasury.Current.Mercenaries.Add(mercenaryId, new MercenaryContract
        {
            characterId = mercenaryId, hiredDay = 1, lastRenewedDay = 2
        });
        var runtime = new EmploymentContractRuntime(
            (ICharacterWorldQuery)world,
            (ICharacterWorldPersistenceIdentityQuery)world,
            equipment,
            Proxy<IGameMoneyAccount>(),
            new GameEventBus(),
            treasury);
        string before = Json(runtime.Capture());
        EmployeeWageState wage = runtime.WageStates.Single();
        MercenaryContract mercenary = runtime.MercenaryContracts.Single();
        wage.rolePremium = 99;
        wage.departurePending = true;
        mercenary.active = false;
        mercenary.lastRenewedDay = 99;
        Require(Json(runtime.Capture()) == before
            && runtime.WageStates.Single().rolePremium == 3
            && runtime.MercenaryContracts.Single().active
            && runtime.MercenaryContracts.Single().lastRenewedDay == 2,
            "Employment queries changed live wage or mercenary state.");

        Require(runtime.SetEmployeeRolePremium(employeeId, 7, out string failure)
            && runtime.WageStates.Single().rolePremium == 7
            && runtime.Capture().wageStates.Single().rolePremium == 7,
            "Employment premium command changed only a detached query copy: " + failure);
    }

    private static void VerifyGrandProject()
    {
        object port = CreateNested(
            typeof(ProductionEconomyDebugScenarios), "GrandProjectPhysicalFixturePort",
            false, default(GrandProjectPhysicalInputReceipt));
        var owners = new EconomyProjectInputOwnerFixtureAuthority(
            new PhysicalItemMassQuery(EditorItemCatalogFactory.Create()));
        var runtime = new GrandProjectRuntime(
            (IGrandProjectWorldPort)port,
            (IGrandProjectOperationsPort)port,
            Proxy<IGameClock>(),
            new DungeonRuntimeAggregateRootStore(),
            owners.Runtime);
        runtime.Initialize();
        GrandProjectRestoreCandidate candidate = runtime.BuildRestore(new DungeonGrandProjectSaveData
        {
            state = new GrandProjectRuntimeState
            {
                completedProjectIds = new List<string> { GrandProjectRuntime.RegionalTradePostId }
            }
        });
        string candidateBefore = Json(candidate.RuntimeState);
        GrandProjectRuntimeState detachedQuery = candidate.RuntimeState;
        MutateGrandProjectQuery(detachedQuery);
        Require(Json(candidate.RuntimeState) == candidateBefore,
            "Detached grand-project candidate query changed its publication authority.");
        runtime.PublishRestoreCandidate(candidate);
        Require(Json(runtime.State) == candidateBefore,
            "Grand-project publication used a mutated query copy.");
        string before = Json(runtime.Capture());
        string stateBefore = Json(runtime.State);
        int version = runtime.Version;
        MutateGrandProjectQuery(runtime.State);
        MutateGrandProjectQuery(candidate.RuntimeState);
        Require(Json(runtime.Capture()) == before
            && Json(runtime.State) == stateBefore
            && runtime.Version == version,
            "Grand-project query changed live scalar, nested commit, list or version.");

        Require(runtime.Start(GrandProjectRuntime.DeepMiningNetworkId, out string startFailure)
            && runtime.State.activeProjectId == GrandProjectRuntime.DeepMiningNetworkId
            && runtime.Version > version,
            "Grand-project start changed only a detached query copy: " + startFailure);
        int startedVersion = runtime.Version;
        Require(runtime.CancelActive(out string cancelFailure)
            && string.IsNullOrEmpty(runtime.State.activeProjectId)
            && runtime.Version > startedVersion,
            "Grand-project cancel changed only a detached query copy: " + cancelFailure);
    }

    private static void MutateGrandProjectQuery(GrandProjectRuntimeState query)
    {
        query.lastStatus = "query-only";
        query.completedWork = 99f;
        query.completedProjectIds.Add("project:r28:query-only");
        query.pendingPhysicalCommit.operationId = "operation:r28:query-only";
        query.pendingPhysicalCommit.sourceStackIds.Add("stack:r28:query-only");
    }

    private static void VerifyOffenseRegions()
    {
        var runtime = new OffenseRegionRuntime();
        string before = Json(runtime.Capture());
        OffenseRegionState query = runtime.Regions.First();
        string regionId = query.regionId;
        query.displayName = "query-only";
        query.AddDamage(StrategicPressureAxis.Intelligence, 99f);
        Require(Json(runtime.Capture()) == before
            && runtime.Regions.First(region => region.regionId == regionId)
                .intelligenceDamage == 0f,
            "Region query mutation reached the owner.");
        Require(runtime.TryApplyReconnaissance(regionId, 3f, out float applied)
            && applied == 3f
            && runtime.Regions.First(region => region.regionId == regionId)
                .intelligenceDamage == 3f,
            "Region command changed only a detached query copy.");
    }

    private static void VerifyOffenseWorldAndTravel()
    {
        Type fixture = typeof(OffenseStrategicDebugScenarios);
        object catalogFixture = Call(fixture, "LoadCatalog");
        var catalog = (IOffenseContentCatalog)catalogFixture;
        using var world = (OffenseHexWorldSimulation)Call(
            fixture, "CreateWorld", catalogFixture, 71231);
        var destination = (OffenseHexCoord)Call(
            fixture, "FindReachableEmptyCoord", world);
        OffenseUrgentSiteDefinitionSO definition = catalog.UrgentSites
            .First(value => value.maximumMitigation > 0f);
        Require(world.TrySpawnUrgentSite(definition.urgentSiteId, destination,
            out string urgentId), "Snapshot fixture urgent site was not created.");
        var seasonalSite = new OffenseWorldSiteStateData
        {
            siteId = "site:r28:snapshot", q = destination.Q, r = destination.R,
            displayName = "Snapshot", state = OffenseWorldSiteState.Revealed,
            seasonalOffer = new OffenseSeasonalExpeditionOfferData
            {
                configured = true, occurrenceInstanceId = "occurrence:r28",
                definitionId = "season:r28", offerDeadlineAbsoluteDay = 5,
                description = "Snapshot", durationSeconds = 1f, requiredMembers = 1,
                campaignOrder = 1, authoredEncounterId = "encounter:r28",
                encounterPreviewText = "Snapshot", encounterRewardPreviewText = "Reward",
                physicalRewards = new List<OffenseSeasonalPhysicalRewardData>
                {
                    new() { itemId = "material:lumber", displayLabel = "Lumber", exactQuantity = 1 }
                }
            }
        };
        Require(world.TryRegisterStrategicSite(seasonalSite),
            "Snapshot fixture seasonal site was not registered.");
        string before = Json(world.Capture());
        int changed = 0;
        world.Changed += () => changed++;
        world.Tiles.First(tile => tile.Coord == destination).blocked = true;
        world.Sites.First(site => site.siteId == seasonalSite.siteId)
            .seasonalOffer.physicalRewards[0].exactQuantity = 99;
        world.UrgentSites.Single(site => site.siteId == urgentId).mitigation = 99f;
        Require(world.TryGetTile(destination, out OffenseHexTileState tileQuery),
            "Snapshot tile lookup failed.");
        tileQuery.regionId = "query-only";
        Require(world.TryGetSite(seasonalSite.siteId, out OffenseWorldSiteStateData siteQuery),
            "Snapshot site lookup failed.");
        siteQuery.state = OffenseWorldSiteState.Resolved;
        siteQuery.seasonalOffer.description = "query-only";
        siteQuery.seasonalOffer.physicalRewards.Clear();
        Require(world.TryGetUrgentSite(urgentId, out OffenseUrgentSiteStateData urgentQuery),
            "Snapshot urgent lookup failed.");
        urgentQuery.stage = OffenseUrgentSiteStage.Destroyed;
        Require(Json(world.Capture()) == before && changed == 0
            && world.TryGetTile(destination, out tileQuery) && !tileQuery.blocked
            && world.TryGetSite(seasonalSite.siteId, out siteQuery)
            && siteQuery.state == OffenseWorldSiteState.Revealed
            && siteQuery.seasonalOffer.physicalRewards.Single().exactQuantity == 1
            && world.TryGetUrgentSite(urgentId, out urgentQuery) && urgentQuery.IsActive,
            "World query or lookup mutation reached the owner.");
        string hiddenId = world.Sites.First(site => site.state == OffenseWorldSiteState.Hidden).siteId;
        Require(world.TryRevealSite(hiddenId)
            && world.TryGetSite(hiddenId, out OffenseWorldSiteStateData revealed)
            && revealed.state == OffenseWorldSiteState.Revealed
            && world.TryEngageSite(seasonalSite.siteId)
            && world.TryGetSite(seasonalSite.siteId, out siteQuery)
            && siteQuery.state == OffenseWorldSiteState.Engaged
            && world.TryResolveSite(seasonalSite.siteId)
            && world.TryGetSite(seasonalSite.siteId, out siteQuery)
            && siteQuery.state == OffenseWorldSiteState.Resolved
            && world.TryMitigateUrgentSite(urgentId, 0.1f)
            && world.TryGetUrgentSite(urgentId, out urgentQuery) && urgentQuery.mitigation > 0f
            && changed == 4,
            "World owner commands changed only detached query copies.");

        var travel = (OffenseTravelRuntime)Call(fixture, "CreateTravelRuntime",
            world, new OffenseReturnSafetyRuntime(world), null, null, null, null);
        const string expeditionId = "expedition:r28:snapshot";
        Require(travel.TryCreateExpedition(expeditionId, out string reason), reason);
        Require(travel.TrySetDestination(expeditionId, destination, string.Empty,
            OffenseTravelProfile.Default, false, out reason), reason);
        before = Json(travel.Capture());
        OffenseTravelStateData travelQuery = travel.ActiveTravel.Single();
        OffenseHexCoord firstStep = travelQuery.remainingPath.First().ToCoord();
        travelQuery.exposure = 99f;
        travelQuery.remainingPath[0].q = int.MinValue;
        Require(travel.TryGetState(expeditionId, out travelQuery),
            "Snapshot travel lookup failed.");
        travelQuery.pausedForBattle = true;
        travelQuery.remainingPath.Clear();
        Require(Json(travel.Capture()) == before
            && travel.TryGetState(expeditionId, out travelQuery)
            && !travelQuery.pausedForBattle && travelQuery.remainingPath.Count > 0,
            "Travel query or nested path mutation reached the owner.");
        Require(travel.TryAdjustExposure(expeditionId, 3f, out float exposure)
            && exposure == 3f && travel.TryPauseForDecision(expeditionId)
            && travel.TryGetState(expeditionId, out travelQuery) && travelQuery.pausedForDecision
            && travel.TryResumeAfterDecision(expeditionId)
            && travel.TryAdvanceOneStep(expeditionId, true, out _, out reason)
            && travel.TryGetState(expeditionId, out travelQuery)
            && travelQuery.CurrentCoord == firstStep,
            "Travel owner commands changed only detached query copies: " + reason);

        VerifyUrgentMitigationOrders(world, catalog, urgentId);
        Require(world.TryDestroyUrgentSite(urgentId)
            && world.TryGetUrgentSite(urgentId, out urgentQuery) && !urgentQuery.IsActive,
            "Urgent destroy changed only a detached query copy.");
    }

    private static void VerifyUrgentMitigationOrders(
        OffenseHexWorldSimulation world, IOffenseContentCatalog catalog, string siteId)
    {
        Type fixture = typeof(OffenseStrategicDebugScenarios);
        var runtime = new OffenseUrgentMitigationRuntime(world, catalog,
            (IBuildingWorldQuery)CreateNested(fixture, "FixedBuildingWorldQuery",
                new object[] { Array.Empty<BuildableObject>() }),
            Proxy<IProductionItemGateway>(),
            (IOffenseUrgentMitigationInputOwnerRuntime)CreateNested(fixture,
                "RecordingUrgentMitigationInputOwnerRuntime"),
            Proxy<IGameClock>(), null, null);
        var orders = (List<OffenseUrgentMitigationOrderStateData>)
            typeof(OffenseUrgentMitigationRuntime)
                .GetField("orders", BindingFlags.Instance | BindingFlags.NonPublic)
                .GetValue(runtime);
        var seeded = (OffenseUrgentMitigationOrderStateData)Call(
            typeof(OffenseUrgentMitigationInputOwnerDebugScenarios), "CreateOrder");
        seeded.siteId = siteId;
        orders.Add(seeded);
        string before = Json(runtime.Capture());
        int version = runtime.Version;
        runtime.Orders.Single().completedWork = 99f;
        Require(runtime.TryGetOrder(siteId, out OffenseUrgentMitigationOrderStateData query),
            "Snapshot mitigation lookup failed.");
        query.physicalCommitPhase = (int)OffenseUrgentMitigationCommitPhase.OutcomePublished;
        query.siteId = "query-only";
        Require(Json(runtime.Capture()) == before && runtime.Version == version
            && runtime.TryGetOrder(siteId, out query) && query.completedWork == 0f,
            "Mitigation query mutation reached the owner.");
        Require(runtime.TryCancel(siteId, out string reason)
            && runtime.Orders.Count == 0 && runtime.Capture().Count == 0
            && !runtime.TryGetOrder(siteId, out _) && runtime.Version > version,
            "Mitigation cancel removed only a detached query copy: " + reason);
    }

    // 기존 Editor instance fixture를 재사용하며 생산 생성 경계는 추가하지 않는다.
    private static object Call(Type owner, string methodName, params object[] arguments)
    {
        MethodInfo method = owner.GetMethod(methodName,
            BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(owner.FullName, methodName);
        try { return method.Invoke(null, arguments); }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static object CreateNested(Type owner, string typeName, params object[] arguments)
    {
        Type type = owner.GetNestedType(typeName, BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Missing fixture: " + typeName);
        return Activator.CreateInstance(type,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
            null, arguments, null);
    }

    private static T Proxy<T>() where T : class =>
        BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy.Create<T>();
    private static string Json(object value) => JsonConvert.SerializeObject(value);
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
