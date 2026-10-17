#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Environment;
using DungeonStory.Foundation;
using UnityEngine;
using VContainer;
using static UnityEngine.Object;

// Main-owned integration witness. Facility construction inputs, one Loose
// silage unit, one same-cell exposure placement, and one large-fire water lot
// are controlled preparation. Storage, seasonal selection, physical fuel
// loss, ignition, damage, alarm/hazard publication, evacuation, hauling and
// suppression use production paths. The large-fire half invokes the common
// ignition entry point only to select the authored water-response branch.
public sealed class Fire01SeasonalResponseLiveRunner
{
    public const string ReportPath =
        "Artifacts/QA/wim-implementation/fire-01-seasonal-response-live.txt";

    private const string DefinitionId =
        "seasonal:autumn-spoiled-silage";
    private const string SilageItemId = "feed:silage";
    private static bool running;
    private readonly List<string> lines = new();
    private readonly List<V20ContentEffectsResolvedEvent> resolved = new();
    private DungeonRuntimeLifetimeScope scope;
    private IGameTimeScaleController timeScale;
    private IGameClock clock;
    private ICharacterAiWorldRegistry world;
    private IWorldItemStackRuntime items;
    private V20CampaignRuntime campaign;
    private V20StoryContentCatalog catalog;
    private IGameCalendar calendar;
    private IGameEventBus events;
    private IDisposable resolvedSubscription;

    public static string StartFocused()
    {
        Require(Application.isPlaying && !running,
            "Start once inside a fresh protected main Play session.");
        DungeonRuntimeLifetimeScope current =
            FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(current?.Container != null,
            "Main runtime is not initialized.");
        IDisposable persistence =
            current.Container.Resolve<IDungeonSaveCommandService>() as IDisposable;
        Require(persistence != null, "Cannot protect user save files.");
        persistence.Dispose();
        current.Container.Resolve<MetaProfilePersistenceService>().Dispose();
        GameManager host = FindFirstObjectByType<GameManager>();
        Require(host != null, "Missing main coroutine host.");
        host.isPause = true;
        current.Container.Resolve<IGameTimeScaleController>().Scale = 0f;
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllText(ReportPath, "result=RUNNING\n");
        running = true;
        try
        {
            host.StartCoroutine(
                new Fire01SeasonalResponseLiveRunner().Observe());
        }
        catch
        {
            running = false;
            throw;
        }
        return "RUNNING " + ReportPath;
    }

    private IEnumerator Observe()
    {
        Stack<IEnumerator> pending = new();
        pending.Push(Run());
        Exception failure = null;
        while (pending.Count > 0)
        {
            object current = null;
            bool moved;
            try
            {
                moved = pending.Peek().MoveNext();
                if (moved) current = pending.Peek().Current;
            }
            catch (Exception error)
            {
                failure = error;
                break;
            }
            if (!moved)
            {
                (pending.Pop() as IDisposable)?.Dispose();
                continue;
            }
            if (current is IEnumerator nested) pending.Push(nested);
            else yield return current;
        }

        while (pending.Count > 0)
            (pending.Pop() as IDisposable)?.Dispose();
        resolvedSubscription?.Dispose();
        resolvedSubscription = null;
        Pause();
        lines.Add(failure == null ? "result=PASS" : "result=FAIL\n" + failure);
        lines.Add(
            "scope=controlled L01 construction inputs, one Loose silage seed, one same-cell character exposure placement and one clean-water lot; actual exact-stack hauls, no-stock eligibility, production OperatingDayStartedEvent, occurrence-bound feed self-heating ignition and exact fuel Sink, structural/body damage, alert/hazard publication, fire-only resident evacuation capture, initial attack and large-fire water suppression. Not natural silage crafting, free-running calendar duration, natural large-fire producer, spread topology or six-adult balance.");
        lines.Add(
            "cleanup=paused disposable protected Play; persistence disabled before owner preparation; operator stops without scene/profile/save writes");
        File.WriteAllLines(ReportPath, lines);
        Debug.Log(string.Join("\n", lines));
        running = false;
    }

    private IEnumerator Run()
    {
        scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(scope?.Container != null,
            "Main runtime disappeared during the witness.");
        timeScale = scope.Container.Resolve<IGameTimeScaleController>();
        clock = scope.Container.Resolve<IGameClock>();
        world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        items = scope.Container.Resolve<IWorldItemStackRuntime>();
        campaign = scope.Container.Resolve<V20CampaignRuntime>();
        catalog = scope.Container.Resolve<V20StoryContentCatalog>();
        calendar = scope.Container.Resolve<IGameCalendar>();
        events = scope.Container.Resolve<IGameEventBus>();
        resolvedSubscription = events.Subscribe<V20ContentEffectsResolvedEvent>(
            value => resolved.Add(value));

        IDisposable runFlowSubscriptions =
            scope.Container.Resolve<IDungeonRunFlowRuntime>() as IDisposable;
        Require(runFlowSubscriptions != null,
            "Run-flow subscription isolation is unavailable.");
        runFlowSubscriptions.Dispose();
        InvasionThreatRuntime threat = FindFirstObjectByType<InvasionThreatRuntime>();
        Require(threat != null
            && !threat.IsCandidatePending
            && !threat.CapturePersistentState().CandidateRaisedThisCycle,
            "The witness requires a fresh invasion-threat cycle.");
        threat.enabled = false;

        OwnerRunManager owner = FindFirstObjectByType<OwnerRunManager>();
        Require(owner != null, "Owner preparation is unavailable.");
        if (owner.CurrentOwnerActor == null)
        {
            Require(scope.Container.Resolve<IDungeonSpaceExpansionCommand>()
                    .TryReconcileNewRunTierZero(out _, out string expansionFailure),
                "Tier-zero preparation failed: " + expansionFailure);
            Click("OwnerOption_1001");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible(30f);
            Pause();
            Require(owner.CurrentOwnerActor != null,
                "Normal party UI did not publish an owner.");
        }

        CharacterActor worker = world.Characters
            .Where(value => value != null
                && !value.IsOwner
                && !value.IsDead
                && value.CurrentLifecycleState == CharacterLifecycleState.Active
                && value.Brain != null
                && value.GetComponent<AbilityHaul>() != null
                && value.GetComponent<AbilityWork>() != null)
            .OrderBy(value => value.Identity.PersistentId,
                StringComparer.Ordinal)
            .FirstOrDefault();
        Require(worker != null,
            "No active authored staff hauler/responder is available.");
        PauseAllActors();

        BuildableObject warehouse = EnsureFireWarehouse(worker);
        IBuildingStructuralIntegrityRuntime integrity =
            scope.Container.Resolve<IBuildingStructuralIntegrityRuntime>();
        Require(integrity.TryGet(
                warehouse,
                out BuildingStructuralIntegritySnapshot structuralBefore),
            "Authored L01 did not publish structural integrity.");
        SeasonalWorldEventDefinitionSO definition = catalog.SeasonalEvents
            .Single(value => string.Equals(
                value.StableId,
                DefinitionId,
                StringComparison.Ordinal));
        ISeasonalFeedSelfHeatingTargetQuery targets = scope.Container
            .Resolve<ISeasonalFeedSelfHeatingTargetQuery>();
        Require(!targets.TrySelect(
                definition.feedSelfHeatingFireProfile,
                out _,
                out SeasonalFeedSelfHeatingTargetFailure noStock)
            && noStock.Code == SeasonalFeedSelfHeatingTargetFailureCode
                .NoEligiblePhysicalStock,
            "Feed self-heating was eligible without physical Stored feed: "
            + noStock.Code + ": " + noStock.Reason);
        lines.Add("[PASS] operational fire-capable L01 without Stored feed is not seasonally eligible");

        string destination = WarehouseStorageIdentity.RequireDestinationId(
            (IWarehouseFacility)warehouse);
        HashSet<string> beforeStackIds = items.GetAllStacks()
            .Where(value => value != null)
            .Select(value => value.StackId)
            .ToHashSet(StringComparer.Ordinal);
        IItemTransferService transfers =
            scope.Container.Resolve<IItemTransferService>();
        Vector2Int source = worker.GetNowXY();
        Require(transfers.TrySpawnItem(
                SilageItemId,
                1,
                source,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawned)
            && spawned == 1,
            "Controlled physical silage seed failed.");
        WorldItemStackSnapshot silage = items.GetAllStacks()
            .Single(value => value != null
                && !beforeStackIds.Contains(value.StackId)
                && value.ItemId == SilageItemId
                && value.Quantity == 1
                && value.State == WorldItemStackState.Loose);
        Require(transfers.TryRequestStackDelivery(
                new ItemStackId(silage.StackId),
                1,
                warehouse.centerPos,
                destination,
                out int requested,
                out DomainFailure deliveryFailure)
            && requested == 1,
            "Exact silage delivery request failed: " + deliveryFailure);

        AbilityHaul haul = worker.GetComponent<AbilityHaul>();
        Require(haul != null, "The selected worker has no haul authority.");
        long haulTerminalBefore = haul.RuntimeHaulTerminalCount;
        ConfigureHauler(worker);
        Resume(4f);
        float haulWallStart = Time.realtimeSinceStartup;
        float haulGameStart = clock.Time;
        bool observedCarried = false;
        while (Time.realtimeSinceStartup - haulWallStart < 45f
            && clock.Time - haulGameStart < 180f)
        {
            PauseOtherActors(worker);
            WorldItemStackSnapshot current = FindStack(silage.StackId);
            observedCarried |= current?.State == WorldItemStackState.Carried
                && string.Equals(
                    current.DestinationId,
                    worker.Identity.PersistentId,
                    StringComparison.Ordinal);
            if (current?.State == WorldItemStackState.Stored
                && string.Equals(
                    current.DestinationId,
                    destination,
                    StringComparison.Ordinal)
                && !haul.IsHauling
                && haul.RuntimeHaulTerminalCount > haulTerminalBefore)
                break;
            yield return null;
        }
        Pause();
        silage = FindStack(silage.StackId);
        Require(observedCarried
            && silage?.State == WorldItemStackState.Stored
            && string.Equals(
                silage.DestinationId,
                destination,
                StringComparison.Ordinal)
            && !haul.IsHauling
            && haul.RuntimeHaulTerminalCount > haulTerminalBefore,
            "Ordinary AI did not carry and store the exact silage stack.");
        Require(targets.TrySelect(
                definition.feedSelfHeatingFireProfile,
                out SeasonalFeedSelfHeatingTarget selected,
                out SeasonalFeedSelfHeatingTargetFailure selectionFailure)
            && selected.StackId == silage.StackId
            && selected.FacilityInstanceId.Equals(
                warehouse.PersistentInstanceId),
            "Stored silage did not become the exact seasonal target: "
            + selectionFailure.Code + ": " + selectionFailure.Reason);
        lines.Add("[PASS] exact Loose silage -> AI Carried -> L01 Stored; target query selected the same stack/facility");

        // Keep one real responder AI-eligible when the incident is published.
        // CharacterAlarmResponseRuntime deliberately excludes ai-paused actors;
        // pausing everybody here would prevent the production Red-alert gate
        // from ever assigning a firefighter, then make a later soft preference
        // compete with ordinary hauling.
        PrepareWorker(worker);
        worker.GetComponent<AbilityWork>().SetWorkPriority(
            BuiltInWorkTypeIds.ThreatMitigation,
            WorkPriorityLevel.Priority1);
        PauseOtherActors(worker);
        int autumnDay = Enumerable.Range(0, GameCalendarRules.DaysPerYear)
            .First(day => GameCalendarRules.Project(day, 0).Season
                == Season.Autumn);
        PrepareOnlyTargetForDay(autumnDay);
        resolved.Clear();
        calendar.SetDateTime(autumnDay, 8);
        events.Publish(new OperatingDayStartedEvent(autumnDay));
        IV20DailyEvaluationDiagnostic daily = scope.Container
            .Resolve<IV20DailyEvaluationDiagnostic>();
        Require(daily.LastDailyEvaluationAbsoluteDay == autumnDay
            && daily.LastDailyEvaluationSucceeded,
            "Production daily dispatcher rejected the Autumn day: "
            + daily.LastDailyEvaluationFailure);
        V20ActiveEventSaveData occurrence = campaign.ActiveSeasonalEvents
            .Single(value => string.Equals(
                value.definitionId,
                DefinitionId,
                StringComparison.Ordinal));
        IEnvironmentalFireQuery fires =
            scope.Container.Resolve<IEnvironmentalFireQuery>();
        EnvironmentalFireSnapshot fire = fires.ActiveFires.Single(value =>
            value.IgnitionKind == EnvironmentalFireIgnitionKind.FeedSelfHeating
            && value.ProducerId == occurrence.instanceId
            && value.Target.TargetId == warehouse.PersistentInstanceId.Value);
        Require(resolved.Count(value => value.DefinitionId == DefinitionId
                && value.OccurrenceInstanceId == occurrence.instanceId
                && value.PhysicalEffectsApplied) == 1
            && fire.EvidenceId.Contains(silage.StackId,
                StringComparison.Ordinal),
            "Seasonal resolution did not ignite the selected physical feed receipt exactly once.");
        Require(
            fire.FuelLossTarget.Equals(new EnvironmentalFireTargetRef(
                EnvironmentalFireTargetKind.ItemStack,
                silage.StackId))
            && fire.FuelLossQuantity == silage.Quantity
            && fire.FuelLossMassGrams > 0L
            && !string.IsNullOrWhiteSpace(fire.FuelLossCommitId)
            && fire.FuelLossAcknowledged
            && FindStack(silage.StackId) == null,
            "Seasonal ignition did not consume and acknowledge the exact physical silage lot once.");
        lines.Add("[PASS] seasonal ignition consumed the selected silage lot through one exact acknowledged physical Sink receipt");

        CharacterActor exposureVictim = world.Characters
            .Where(value => value != null
                && value != worker
                && !value.IsDead
                && value.CurrentLifecycleState == CharacterLifecycleState.Active)
            .OrderBy(value => value.Identity?.PersistentId ?? string.Empty,
                StringComparer.Ordinal)
            .FirstOrDefault()
            ?? owner.CurrentOwnerActor;
        Require(exposureVictim != null && exposureVictim != worker,
            "No live secondary character was available for same-cell fire exposure.");
        PauseAllActors();
        Vector3 exposureOriginalPosition = exposureVictim.transform.position;
        float exposureHealthBefore = exposureVictim.CurrentHealth;
        exposureVictim.transform.position = warehouse.Grid.GetWorldPos(fire.Position);
        EnvironmentalFireTickReport exposureTick = scope.Container
            .Resolve<EnvironmentalFireRuntime>()
            .Advance(5f);
        exposureVictim.transform.position = exposureOriginalPosition;
        Require(exposureVictim.CurrentHealth < exposureHealthBefore
            && exposureTick.DamageCommits >= 2
            && exposureTick.Failures.Count == 0
            && fires.TryGet(fire.FireId, out EnvironmentalFireSnapshot exposedFire)
            && exposedFire.StepIndex == 1,
            "The production fire tick did not apply one structural and one same-cell body damage commit.");
        lines.Add("[PASS] one production fire tick applied structural damage plus heat-protection-aware damage to the actual same-cell character");

        PrepareWorker(worker);
        worker.GetComponent<AbilityWork>().SetWorkPriority(
            BuiltInWorkTypeIds.ThreatMitigation,
            WorkPriorityLevel.Priority1);

        for (int frame = 0; frame < 3; frame++)
            yield return null;
        string incidentId = "environmental-fire-incident:" + fire.FireId;
        SettlementAlertSnapshot alert = scope.Container
            .Resolve<ISettlementAlertService>().Capture();
        WorldHazardSnapshot hazard = scope.Container
            .Resolve<IWorldHazardZoneQuery>()
            .GetHazard(
                new CharacterId(worker.Identity.PersistentId),
                fire.Position);
        Require(alert.ActiveIncidentIds.Contains(incidentId)
            && (hazard.Flags & WorldHazardFlags.Fire) != 0
            && hazard.Level == WorldHazardLevel.Forbidden,
            "Active fire did not publish its exact alert and forbidden hazard overlay.");

        AbilityWork responseWork = worker.GetComponent<AbilityWork>();
        GridPathSearchResult responseSearch = warehouse.Grid.SearchPath(
            worker.GetNowXY());
        bool responseCandidateFound = responseWork.TryGetBestWorkCandidate(
            BuiltInWorkTypeIds.ThreatMitigation,
            responseSearch,
            out WorkTargetCandidate responseCandidate);
        WorkTargetCandidate responseRejected =
            responseWork.LastRejectedWorkCandidate;
        Require(responseCandidateFound && responseCandidate.IsValid,
            "The active fire was not exposed as a ThreatMitigation work "
            + "candidate before AI dispatch: candidate="
            + responseCandidate.FailureKind + ":"
            + responseCandidate.FailureReason + "; rejected="
            + responseRejected.FailureKind + ":"
            + responseRejected.FailureReason);

        Resume(4f);
        float responseWallStart = Time.realtimeSinceStartup;
        float responseGameStart = clock.Time;
        bool observedStructuralDamage = integrity.TryGet(
                warehouse,
                out BuildingStructuralIntegritySnapshot afterExposure)
            && afterExposure.CurrentHitPoints < structuralBefore.CurrentHitPoints;
        while (Time.realtimeSinceStartup - responseWallStart < 45f
            && clock.Time - responseGameStart < 180f
            && fires.ActiveFires.Any(value => value.FireId == fire.FireId))
        {
            PauseOtherActors(worker);
            if (fires.TryGet(fire.FireId, out EnvironmentalFireSnapshot active)
                && active.StepIndex >= 1
                && active.TotalDamage > 0f
                && integrity.TryGet(
                    warehouse,
                    out BuildingStructuralIntegritySnapshot damaged)
                && damaged.CurrentHitPoints
                    < structuralBefore.CurrentHitPoints)
            {
                observedStructuralDamage = true;
            }
            yield return null;
        }
        Pause();
        EnvironmentalFireHistorySnapshot history = fires.History
            .SingleOrDefault(value => value?.Fire?.FireId == fire.FireId);
        Require(observedStructuralDamage,
            "The actual fire did not retain a nonzero structural damage step before suppression.");
        lines.Add("[PASS] seasonal occurrence ignited exact L01; alert/hazard published; at least one structural damage tick retained");
        Require(history?.EndReason == EnvironmentalFireEndReason.Suppressed
            && history.Fire.TotalDamage > 0f
            && history.Fire.TotalSuppressionWork > 0f
            && history.Fire.TotalWaterConsumed == 0
            && !fires.ActiveFires.Any(value => value.FireId == fire.FireId),
            "The sole live staff responder did not suppress the damaged fire through ThreatMitigation without water hauling.");
        DungeonEnvironmentalFireSaveData saved = scope.Container
            .Resolve<IEnvironmentalFirePersistence>().Capture();
        Require(saved.history.Count(value => value?.fire?.fireId == fire.FireId
                && value.endReason == (int)EnvironmentalFireEndReason.Suppressed)
                == 1
            && saved.activeFires.All(value => value.fireId != fire.FireId),
            "Fire history/persistence capture did not retain the exact suppressed result.");
        lines.Add("[PASS] sole unpaused staff Brain->AIWork->ThreatMitigation suppressed the damaged fire without water hauling; exact history/persistence capture retained once");

        yield return VerifyLargeFireWaterAndEvacuation(warehouse, worker);
    }

    private IEnumerator VerifyLargeFireWaterAndEvacuation(
        BuildableObject warehouse,
        CharacterActor worker)
    {
        PauseAllActors();
        IEnvironmentalFireQuery fires = scope.Container.Resolve<IEnvironmentalFireQuery>();
        Require(fires.ActiveFires.Count == 0,
            "The large-fire witness requires the seasonal fire to be terminal.");
        IBuildingStructuralIntegrityRuntime integrity = scope.Container
            .Resolve<IBuildingStructuralIntegrityRuntime>();
        Require(integrity.TryApplyRepairWork(
                warehouse,
                100f,
                out bool repairCompleted,
                out BuildingStructuralIntegritySnapshot repaired)
            && repairCompleted
            && Mathf.Approximately(
                repaired.CurrentHitPoints,
                repaired.MaxHitPoints),
            "Controlled inter-scenario repair did not restore the L01 test target.");

        HashSet<string> beforeWaterIds = items.GetAllStacks()
            .Where(value => value != null)
            .Select(value => value.StackId)
            .ToHashSet(StringComparer.Ordinal);
        IItemTransferService transfers = scope.Container.Resolve<IItemTransferService>();
        Require(warehouse.TryGetNearestWorkAccessGridPosition(
                warehouse.Grid,
                warehouse.centerPos,
                out Vector2Int waterSource),
            "The L01 has no legal work-access cell for the controlled water source.");
        worker.transform.position = warehouse.Grid.GetWorldPos(waterSource);
        Require(transfers.TrySpawnItem(
                EnvironmentalFireWorldAdapter.CleanWaterItemId,
                1,
                waterSource,
                WorldItemStackState.Loose,
                string.Empty,
                out int spawnedWater)
            && spawnedWater == 1,
            "Controlled clean-water lot failed to spawn.");
        WorldItemStackSnapshot water = items.GetAllStacks().Single(value =>
            value != null
            && !beforeWaterIds.Contains(value.StackId)
            && value.ItemId == EnvironmentalFireWorldAdapter.CleanWaterItemId
            && value.Quantity == 1
            && value.State == WorldItemStackState.Loose);

        // This protected witness owns one exact water lot. Exclude unrelated
        // pre-existing clean-water stocks so the production response does not
        // choose a distant lot and let the low-HP L01 burn down before the
        // physical delivery can be observed. The prohibition is runtime-only:
        // persistence was disabled before setup and the session is disposable.
        foreach (WorldItemStackSnapshot competingWater in items.GetAllStacks()
                     .Where(value => value != null
                         && value.ItemId
                            == EnvironmentalFireWorldAdapter.CleanWaterItemId
                         && value.StackId != water.StackId
                         && !value.Forbidden)
                     .OrderBy(value => value.StackId, StringComparer.Ordinal))
        {
            Require(items.SetForbidden(competingWater.StackId, true),
                "Controlled water-source isolation failed: "
                + competingWater.StackId);
        }

        var target = new EnvironmentalFireTargetRef(
            EnvironmentalFireTargetKind.Building,
            warehouse.PersistentInstanceId.Value);
        string causeId = "qa:fire01:large-water:"
            + warehouse.PersistentInstanceId.Value;
        EnvironmentalFireIgnitionResult ignition = scope.Container
            .Resolve<IEnvironmentalFireCommand>()
            .TryIgnite(new EnvironmentalFireIgnitionRequest(
                causeId,
                EnvironmentalFireIgnitionKind.ActiveHeatSource,
                "qa:fire01:large-water",
                target,
                0.4f,
                "qa:physical-water-and-evacuation"));
        Require(ignition.Created,
            "Controlled authored large-fire branch did not ignite: "
            + ignition.Disposition + ": " + ignition.Reason);

        string destination = EnvironmentalFireWaterDestinationIdentity.Create(
            ignition.FireId);
        ConfigureHauler(worker);
        AbilityWork workerWork = worker.GetComponent<AbilityWork>();
        workerWork.SetWorkPriority(
            BuiltInWorkTypeIds.ThreatMitigation,
            WorkPriorityLevel.Priority1);
        EnvironmentalFireResponseRuntime response = scope.Container
            .Resolve<IEnvironmentalFireSuppressionWorkRuntime>()
            as EnvironmentalFireResponseRuntime;
        Require(response != null,
            "The registered environmental-fire response runtime is unavailable.");
        response.Tick();
        WorldItemStackSnapshot requestedWater = items.GetAllStacks()
            .Where(value => value != null
                && value.ItemId == EnvironmentalFireWorldAdapter.CleanWaterItemId
                && value.AvailableQuantity >= 1
                && string.Equals(
                    value.DestinationId,
                    destination,
                    StringComparison.Ordinal))
            .OrderBy(value => value.StackId, StringComparer.Ordinal)
            .FirstOrDefault();
        Require(requestedWater != null,
            "The large fire did not publish an exact physical clean-water delivery request.");
        Require(requestedWater.StackId == water.StackId,
            "The response selected a clean-water lot outside the controlled "
            + "physical source boundary: " + requestedWater.StackId);
        for (int frame = 0; frame < 3; frame++)
            yield return null;
        Require(workerWork.HasEmergencyResponseWorkGateForDiagnostics,
            "The live fire alarm did not reserve the active responder before evacuation selection.");

        IInvasionOwnerEvacuationService evacuation = scope.Container
            .Resolve<IInvasionOwnerEvacuationService>();
        BuildingFeatureRoomRow room = scope.Container
            .Resolve<IBuildingFeatureQueryService>()
            .Capture()
            .Rooms
            .Where(value => value?.Grid != null
                && value.Room != null
                && value.Room.IsUsable
                && !value.Room.IsSelfContained
                && value.Room.Cells.Count > 0)
            .OrderByDescending(value => value.Room.Cells.Count)
            .ThenBy(value => value.Room.Id)
            .FirstOrDefault();
        Require(room != null,
            "No usable authored room was available for fire evacuation.");
        Require(evacuation.TryDesignateResidentEvacuationRoom(
                room.Grid,
                room.Room,
                out string designationFailure),
            "Fire-only resident room designation failed: "
            + designationFailure);
        Require(evacuation.TryRequestResidentEvacuation(
                out string evacuationFailure),
            "Fire-only resident evacuation request failed: "
            + evacuationFailure);
        ResidentEvacuationParticipantView[] participants = evacuation
            .ResidentEvacuationParticipants
            .Where(value => value.HasTarget)
            .ToArray();
        string workerId = worker.Identity.PersistentId;
        Require(participants.Length > 0
            && participants.All(value => !string.Equals(
                value.CharacterId,
                workerId,
                StringComparison.Ordinal))
            && participants.All(value => value.Target != warehouse.centerPos),
            "Fire evacuation did not route residents away from the fire while preserving the responder.");
        OwnerEvacuationSaveSnapshot evacuationSave = evacuation.Capture();
        DungeonEnvironmentalFireSaveData fireSave = scope.Container
            .Resolve<IEnvironmentalFirePersistence>()
            .Capture();
        Require(evacuationSave.residentParticipants.Count == participants.Length
            && fireSave.activeFires.Count(value => value.fireId == ignition.FireId) == 1,
            "Fire-only current capture did not preserve both active fire and evacuation participants.");
        lines.Add("[PASS] active fire routed ordinary residents away from the fire, excluded the responder, and captured both authorities together");

        CharacterActor evacuee = world.Characters.FirstOrDefault(value =>
            value != null
            && participants.Any(participant => string.Equals(
                participant.CharacterId,
                value.Identity?.PersistentId,
                StringComparison.Ordinal)));
        Require(evacuee != null,
            "The fire evacuation participant did not join the live character world.");
        evacuee.SetAiPaused(false);

        AbilityHaul haul = worker.GetComponent<AbilityHaul>();
        long terminalBefore = haul.RuntimeHaulTerminalCount;
        Resume(4f);
        float haulWallStart = Time.realtimeSinceStartup;
        float haulGameStart = clock.Time;
        bool observedCarried = false;
        bool observedEvacuation = false;
        while (Time.realtimeSinceStartup - haulWallStart < 45f
            && clock.Time - haulGameStart < 180f)
        {
            PauseOtherActors(worker, evacuee);
            WorldItemStackSnapshot current = FindStack(water.StackId);
            observedCarried |= current?.State == WorldItemStackState.Carried;
            observedEvacuation |= evacuation.ResidentEvacuationParticipants.Any(
                value => value.CharacterId == evacuee.Identity.PersistentId
                    && value.HasTarget
                    && value.Status is ResidentEvacuationParticipantStatus.Moving
                        or ResidentEvacuationParticipantStatus.Holding);
            if (current?.State == WorldItemStackState.FacilityBuffer
                && current.Position == warehouse.centerPos
                && string.Equals(
                    current.DestinationId,
                    destination,
                    StringComparison.Ordinal)
                && !haul.IsHauling
                && haul.RuntimeHaulTerminalCount > terminalBefore)
            {
                break;
            }
            yield return null;
        }
        Pause();
        water = FindStack(water.StackId);
        Require(observedCarried
            && observedEvacuation
            && water?.State == WorldItemStackState.FacilityBuffer
            && water.Position == warehouse.centerPos
            && string.Equals(water.DestinationId, destination,
                StringComparison.Ordinal),
            "Actual AI haul or concurrent fire evacuation did not reach its production state.");

        PrepareWorker(worker);
        workerWork.SetWorkPriority(
            BuiltInWorkTypeIds.ThreatMitigation,
            WorkPriorityLevel.Priority1);
        worker.Brain.RequestImmediateReplan(clearFailures: true);
        Resume(4f);
        float responseWallStart = Time.realtimeSinceStartup;
        float responseGameStart = clock.Time;
        while (Time.realtimeSinceStartup - responseWallStart < 45f
            && clock.Time - responseGameStart < 180f
            && fires.ActiveFires.Any(value => value.FireId == ignition.FireId))
        {
            PauseOtherActors(worker);
            yield return null;
        }
        Pause();
        EnvironmentalFireHistorySnapshot history = fires.History
            .SingleOrDefault(value => value?.Fire?.FireId == ignition.FireId);
        Require(history?.EndReason == EnvironmentalFireEndReason.Suppressed
            && history.Fire.TotalWaterConsumed == 1
            && history.Fire.TotalSuppressionWork > 0f
            && FindStack(water.StackId) == null,
            "Large fire did not consume the exact delivered water lot and terminate once.");
        for (int frame = 0; frame < 3; frame++)
            yield return null;
        Require(evacuation.ResidentEvacuationParticipants.Count == 0,
            "Residents remained evacuation-owned after the only fire ended.");
        lines.Add("[PASS] large fire requested one exact clean-water lot; actual AI carried it, suppression consumed it once, and fire-only evacuation released after recovery");
    }

    private BuildableObject EnsureFireWarehouse(CharacterActor worker)
    {
        BuildableObject existing = scope.Container
            .Resolve<IWarehouseWorldQuery>().Warehouses
            .OfType<BuildableObject>()
            .Where(value => value != null
                && value.id == 1050
                && !value.isDestroy)
            .OrderBy(value => value.PersistentInstanceId.Value,
                StringComparer.Ordinal)
            .FirstOrDefault();
        if (existing != null)
            return existing;

        BuildingSO definition = Resources.Load<BuildingSO>(
            "SO/Building/Modular/L01_대형보관선반");
        Require(definition != null
            && definition.Abilities.OfType<BuildingEnvironmentalFireAbility>()
                .Any(value => value.Accepts(
                    EnvironmentalFireIgnitionKind.FeedSelfHeating))
            && definition.Abilities.OfType<BuildingStructuralIntegrityAbility>()
                .Any(),
            "Authored L01 fire/structural capabilities are missing.");
        Require(scope.Container.Resolve<IGridSystemProvider>()
                .TryGetGrid(out Grid grid)
            && grid != null,
            "Main grid unavailable for controlled L01 construction.");
        Vector2Int anchor = FindLegalAnchor(grid, definition, worker.GetNowXY());
        DungeonStoryGridBuildingController controller =
            FindFirstObjectByType<DungeonStoryGridBuildingController>();
        Require(controller != null,
            "Dungeon grid building controller is unavailable.");
        bool sitePlaced = controller.TryPlaceConstructionSite(
            definition,
            anchor,
            out string placementFailure);
        Require(sitePlaced,
            "L01 construction-site placement failed: " + placementFailure);
        ConstructionSite site = world.Buildings.OfType<ConstructionSite>()
            .Single(value => value != null
                && value.id == definition.id
                && value.centerPos == anchor);
        IWorkOrderRuntime orders = scope.Container.Resolve<IWorkOrderRuntime>();
        Require(orders.TryGetOrderFor(
                site,
                BuiltInWorkTypeIds.Construct,
                out WorkOrderProgressState order),
            "L01 construction order was not published.");
        foreach (KeyValuePair<string, int> material in
                 order.ItemMaterialRequirements.OrderBy(
                     value => value.Key,
                     StringComparer.Ordinal))
        {
            Require(items.SpawnItemAt(
                    material.Key,
                    material.Value,
                    anchor,
                    WorldItemStackState.FacilityBuffer,
                    order.MaterialDestinationId,
                    out int materialSpawned)
                && materialSpawned == material.Value,
                "L01 controlled physical construction input failed: "
                + material.Key);
        }
        Require(orders.RefreshMaterialsReady(site),
            "L01 exact physical BOM did not become ready.");
        Require(orders.ApplyWork(
                worker,
                site,
                BuiltInWorkTypeIds.Construct,
                order.RequiredWork,
                out bool completed,
                out bool effectsApplied,
                out string workFailure)
            && completed
            && effectsApplied,
            "L01 controlled construction work failed: " + workFailure);
        BuildableObject placed = world.Buildings.Single(value => value != null
            && value is not ConstructionSite
            && value.id == definition.id
            && value.centerPos == anchor);
        Require(placed is IWarehouseFacility warehouse
            && warehouse.HasWarehouseInventory,
            "Constructed L01 did not publish operational warehouse inventory.");
        return placed;
    }

    private Vector2Int FindLegalAnchor(
        Grid grid,
        BuildingSO definition,
        Vector2Int origin)
    {
        GridPlacementValidator geometry = new();
        IRoomLayoutCache rooms = scope.Container.Resolve<IRoomLayoutCache>();
        return Enumerable.Range(0, grid.height)
            .SelectMany(y => Enumerable.Range(0, grid.width)
                .Select(x => new Vector2Int(x, y)))
            .OrderBy(value => Mathf.Abs(value.x - origin.x)
                + Mathf.Abs(value.y - origin.y))
            .ThenBy(value => value.y)
            .ThenBy(value => value.x)
            .First(candidate =>
            {
                IReadOnlyList<Vector2Int> footprint =
                    definition.GetGridPosList(candidate);
                return rooms.TryGetRoom(grid, candidate, out RoomInstance room)
                    && room != null
                    && room.IsUsable
                    && footprint.All(room.ContainsCell)
                    && geometry.AreInsideHorizontalBounds(grid, footprint, 1)
                    && geometry.CanBuildInArea(grid, definition, footprint)
                    && geometry.CanOccupy(
                        grid,
                        definition.Placement.Layer,
                        footprint)
                    && geometry.HasSupportBelow(grid, footprint)
                    && BuildingWorkAccessRules.EnumerateCandidates(
                            footprint,
                            definition.IsGridMovement)
                        .Any(access => grid.IsValidGridPos(access)
                            && grid.IsWalkable(access)
                            && grid.GetMovePathTo(origin, access)?.Count > 0);
            });
    }

    private void PrepareOnlyTargetForDay(int absoluteDay)
    {
        SeasonalEventWorldSaveData state = campaign.CaptureSeasonal();
        state.activeEvents.Clear();
        state.completedEventIds = catalog.SeasonalEvents
            .Where(value => value.season == Season.Autumn
                && !string.Equals(
                    value.StableId,
                    DefinitionId,
                    StringComparison.Ordinal))
            .Select(value => value.StableId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        state.cycle = absoluteDay / GameCalendarRules.DaysPerYear;
        state.lastEvaluationAbsoluteDay = absoluteDay - 1;
        campaign.PublishSeasonal(campaign.PrepareSeasonal(state));
    }

    private WorldItemStackSnapshot FindStack(string stackId) => items
        .GetAllStacks()
        .SingleOrDefault(value => value != null
            && string.Equals(value.StackId, stackId, StringComparison.Ordinal));

    private void ConfigureHauler(CharacterActor worker)
    {
        PrepareWorker(worker);
        AbilityWork work = worker.GetComponent<AbilityWork>();
        work.SetWorkPriority(BuiltInWorkTypeIds.Haul,
            WorkPriorityLevel.Priority1);
        Require(worker.Brain.PreferActionOnNextDecision<AIHaul>(),
            "The selected worker has no authored haul action.");
        worker.Brain.RequestImmediateReplan(clearFailures: true);
    }

    private void PrepareWorker(CharacterActor worker)
    {
        PauseOtherActors(worker);
        worker.SetAiPaused(false);
        if (worker.Brain.HasRunningAction)
        {
            Require(worker.Brain.StopCurrentActionForReplan(
                    "fire01-focused-ready"),
                "The selected worker's prior action could not be cancelled.");
        }
        foreach (CharacterCondition need in new[]
                 {
                     CharacterCondition.HUNGER,
                     CharacterCondition.THIRST,
                     CharacterCondition.SLEEP,
                     CharacterCondition.HYGIENE,
                     CharacterCondition.EXCRETION,
                     CharacterCondition.FUN
                 })
        {
            worker.Stats.ChangesStat(
                need,
                100f - worker.Stats.GetConditionValue(need, 0f));
        }
        AbilityWork work = worker.GetComponent<AbilityWork>();
        Require(work != null, "The selected worker has no work authority.");
        work.ClearPriorityWorkTarget();
        work.SetDutyState(AbilityWork.DutyState.OnDuty);
    }

    private void PauseAllActors()
    {
        foreach (CharacterActor actor in world.Characters.Where(
                     value => value != null))
            actor.SetAiPaused(true);
    }

    private void PauseOtherActors(params CharacterActor[] activeActors)
    {
        foreach (CharacterActor actor in world.Characters.Where(
                     value => value != null
                         && (activeActors == null
                             || !activeActors.Contains(value))))
            actor.SetAiPaused(true);
    }

    private void Pause()
    {
        GameManager host = FindFirstObjectByType<GameManager>();
        if (host != null) host.isPause = true;
        if (timeScale != null) timeScale.Scale = 0f;
    }

    private void Resume(float scale)
    {
        GameManager host = FindFirstObjectByType<GameManager>();
        Require(host != null, "Missing main time host.");
        host.isPause = false;
        timeScale.Scale = scale;
    }

    private static void Click(string objectName)
    {
        UnityEngine.UI.Button button = Resources.FindObjectsOfTypeAll<
                UnityEngine.UI.Button>()
            .FirstOrDefault(value => value != null
                && value.gameObject.scene.IsValid()
                && value.gameObject.activeInHierarchy
                && string.Equals(value.name, objectName,
                    StringComparison.Ordinal));
        Require(button != null && button.interactable,
            "Missing UI button: " + objectName);
        button.onClick.Invoke();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
