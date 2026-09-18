#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using VContainer;
using static UnityEngine.Object;

// Main-owned integration witness. Only occurrence, initial stock, facilities and
// healthy hauler are prepared; actual notice input, haul and result commit run.
public sealed class Wim037GuestDeliveryLiveRunner
{
    public const string ReportPath = "Artifacts/QA/wim-implementation/wim-037-guest-delivery-live.txt";
    public const string CancelReportPath = "Artifacts/QA/wim-implementation/wim-037-guest-delivery-cancel-live.txt";
    public const string VenueReportPath = "Artifacts/QA/wim-implementation/wim-037-venue-and-guest-delivery-live.txt";
    private const string DefinitionId = "guest-request:sealed-archive";
    private const string ItemId = "component:engineering-drawing";
    private const string BuildingId = "building:8825";
    private const string InstanceId = "event:2:guest-request:sealed-archive:wim037-live";
    private static bool running;
    private bool cancelOnly;
    private bool verifyVenue;
    private string OutputPath => verifyVenue ? VenueReportPath : cancelOnly ? CancelReportPath : ReportPath;
    private readonly List<string> lines = new();
    private readonly HashSet<string> observedDeliveryStacks = new(StringComparer.Ordinal);
    private DungeonRuntimeLifetimeScope scope;
    private V20CampaignRuntime campaign;
    private IWorldItemStackRuntime items;
    private ICharacterAiWorldRegistry world;
    private IGameClock clock;
    private IGameTimeScaleController timeScale;
    private IGameMoneyAccount money;
    private IEconomyTransactionLedger moneyLedger;
    private HashSet<string> initialTransactions;
    private Grid grid;
    private string workerId;
    private string factionId;
    private string deliveryDestination;
    private Vector2Int supplyOrigin;
    private Vector2Int preparedSource;
    private bool sawCarried;
    private bool sawArrived;

    public static string StartFocused() => Start(false);
    public static string StartCancel() => Start(true);
    public static string StartVenueFocused() => Start(false, true);
    public static string RunVenueSpatialFocused() => WimSocietyVenueLiveScenario.RunSpatialFocused();

    private static string Start(bool cancelOnly, bool verifyVenue = false)
    {
        Require(Application.isPlaying && !running, "Fresh disposable main Play required.");
        var current = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        Require(current?.Container != null, "Main scope unavailable.");
        var persistence = current.Container.Resolve<IDungeonSaveCommandService>() as IDisposable;
        Require(persistence != null, "User-save isolation unavailable.");
        persistence.Dispose();
        current.Container.Resolve<MetaProfilePersistenceService>().Dispose();
        var host = FindFirstObjectByType<GameManager>();
        Require(host != null, "Main coroutine host missing.");
        host.isPause = true;
        current.Container.Resolve<IGameTimeScaleController>().Scale = 0;
        var runner = new Wim037GuestDeliveryLiveRunner { cancelOnly = cancelOnly, verifyVenue = verifyVenue };
        Directory.CreateDirectory(Path.GetDirectoryName(runner.OutputPath));
        File.WriteAllText(runner.OutputPath, "result=RUNNING\n");
        running = true;
        try { host.StartCoroutine(runner.Observe()); }
        catch { running = false; throw; }
        return "RUNNING " + runner.OutputPath;
    }

    private IEnumerator Observe()
    {
        var pending = new Stack<IEnumerator>();
        pending.Push(Run());
        Exception failure = null;
        while (pending.Count > 0)
        {
            object value = null;
            bool moved;
            try { moved = pending.Peek().MoveNext(); if (moved) value = pending.Peek().Current; }
            catch (Exception error) { failure = error; break; }
            if (!moved) { (pending.Pop() as IDisposable)?.Dispose(); continue; }
            if (value is IEnumerator nested) pending.Push(nested); else yield return value;
        }
        try { while (pending.Count > 0) (pending.Pop() as IDisposable)?.Dispose(); }
        finally { Pause(); }
        if (items != null)
            lines.Add("final stock=" + Quantity() + "; destination=" + deliveryDestination
                + "; carried=" + sawCarried + "; arrived=" + sawArrived);
        if (initialTransactions != null)
        {
            try { lines.Add("money raw=" + money.Balance + "; requestAdjusted=" + RequestAdjustedBalance()); }
            catch (Exception attributionFailure) { failure ??= attributionFailure; }
            foreach (var entry in moneyLedger.Records.Where(x => !initialTransactions.Contains(x.transactionId)))
                lines.Add("observed money transaction=" + JsonUtility.ToJson(entry));
        }
        lines.Add(failure == null ? "result=PASS" : "result=FAIL\n" + failure);
        lines.Add(cancelOnly
            ? "scope=controlled authored occurrence/day2, RF25 buildings and two physical drawings; actual notice/accept, autonomous pickup then actual decline while carried, no same-frame teleport/debit, natural release/replan and terminal capture. Not death/drop-failure, natural request selection, construction cost, venue9 or six-adult balance."
            : "scope=controlled authored occurrence/day2, RF25 buildings and two physical drawings; actual EventSystem notice/accept, autonomous haul, terminal reward and current whole save. Not natural request selection, construction cost, venue9 authoring, onsite work or six-adult balance.");
        lines.Add("cleanup=paused disposable main Play; disk persistence disabled before owner UI; operator stops without scene/profile/save writes");
        File.WriteAllLines(OutputPath, lines);
        Debug.Log("WIM037 guest delivery witness " + (failure == null ? "PASS" : "FAIL: " + failure.Message));
        running = false;
    }

    private IEnumerator Run()
    {
        scope = FindFirstObjectByType<DungeonRuntimeLifetimeScope>();
        timeScale = scope.Container.Resolve<IGameTimeScaleController>();
        clock = scope.Container.Resolve<IGameClock>();
        world = scope.Container.Resolve<ICharacterAiWorldRegistry>();
        items = scope.Container.Resolve<IWorldItemStackRuntime>();
        campaign = scope.Container.Resolve<V20CampaignRuntime>();
        money = scope.Container.Resolve<IGameMoneyAccount>();
        moneyLedger = scope.Container.Resolve<IEconomyTransactionLedger>();
        var owner = FindFirstObjectByType<OwnerRunManager>();
        Require(owner != null, "Owner preparation unavailable.");
        if (owner.CurrentOwnerActor == null)
        {
            Require(scope.Container.Resolve<IDungeonSpaceExpansionCommand>().TryReconcileNewRunTierZero(
                out _, out string expansionFailure), "Tier zero: " + expansionFailure);
            Click("OwnerOption_1001");
            yield return StartPartyPlayModeTestDriver.CompleteIfVisible(30f);
            Pause();
            Require(owner.CurrentOwnerActor != null, "Normal owner UI did not publish a run.");
        }
        Require(scope.Container.Resolve<IGridSystemProvider>().TryGetGrid(out grid), "Main grid unavailable.");
        var hauler = world.Characters.Where(x => x != null && !x.IsOwner && !x.IsDead
                && x.characterType == CharacterType.NPC && x.Brain != null && x.GetComponent<AbilityHaul>() != null)
            .OrderBy(x => x.Identity.PersistentId, StringComparer.Ordinal).FirstOrDefault();
        Require(hauler != null, "Prepared party has no actual staff hauler.");
        workerId = hauler.Identity.PersistentId;
        ConfigureHauler();
        if (cancelOnly)
        {
            var recoveryDefinition = items.CatalogProvider.GetDefinition(ItemId);
            int recoveryCapacity = world.Warehouses.Where(x => x != null && x.HasWarehouseInventory
                    && x.Inventory != null && x.Inventory.Accepts(recoveryDefinition.StockCategory)
                    && x is BuildableObject building && !building.isDestroy)
                .Sum(x => x.Inventory.GetAcceptableQuantity(ItemId, 2));
            Require(recoveryCapacity >= 2, "Cancellation fixture needs real warehouse admission for both drawings.");
        }
        var facilities = scope.Container.Resolve<IFacilityCapabilityQuery>();
        Require(facilities.FindOperational(FacilityCapabilityKind.None, BuildingId).Count == 0,
            "Missing-venue preparation requires a fresh run without RF25.");
        foreach (var stack in items.GetAllStacks().Where(x => !x.Forbidden
                     && (x.ItemId == ItemId || x.State == WorldItemStackState.Loose)).ToArray())
            Require(items.SetForbidden(stack.StackId, true), "Cannot isolate preexisting stock: " + stack.StackId);
        Require(scope.Container.Resolve<IWorldDropZoneQuery>().TryGetDeliveryDropoff(out var dropoff),
            "Supply origin unavailable.");
        supplyOrigin = dropoff;
        var traversal = GridTraversalContext.ForCharacter(CharacterPersistentIdentity.Require(hauler));
        var access = scope.Container.Resolve<IGridTraversalAccessQuery>();
        var costs = scope.Container.Resolve<IGridTraversalCostPolicy>();
        var source = grid.GetCells().Select(x => x.Position)
            .Where(p => grid.IsWalkable(p) && Math.Abs(p.x - dropoff.x) + Math.Abs(p.y - dropoff.y) >= 4
                && !items.GetAllStacks().Any(x => x.Quantity > 0 && x.Position == p))
            .OrderBy(p => Math.Abs(p.x - dropoff.x) + Math.Abs(p.y - dropoff.y)).ThenBy(p => p.y).ThenBy(p => p.x)
            .Where(p => grid.SearchPathTo(dropoff, p,
                    cell => access.CanTraverse(grid, cell, traversal, out _), costs, traversal).GetMoveCostTo(p) != int.MaxValue)
            .Select(p => (Vector2Int?)p).FirstOrDefault();
        Require(source.HasValue, "No separated reachable physical source cell.");
        preparedSource = source.Value;
        Require(items.SpawnItemAt(ItemId, 2, source.Value, WorldItemStackState.Loose, string.Empty, out int spawned)
            && spawned == 2, "Could not prepare exact two authored drawings.");
        var initialIds = items.GetAllStacks().Where(x => x.ItemId == ItemId && x.Position == source.Value)
            .Select(x => x.StackId).ToArray();
        Require(initialIds.Length > 0 && initialIds.All(id => items.SetForbidden(id, true)), "Cannot hold prepared source before acceptance.");
        var catalog = scope.Container.Resolve<V20StoryContentCatalog>();
        var definition = catalog.GuestRequests.Single(x => x.StableId == DefinitionId);
        Require(definition.serviceRequirements.items.Count == 1 && definition.serviceRequirements.items[0].amount == 2
            && definition.serviceRequirements.items[0].consume, "Authored request changed; re-check fixture expectations.");
        factionId = catalog.Arcs.OrderBy(x => x.factionId, StringComparer.Ordinal).First().factionId;
        var society = campaign.CaptureSociety();
        Require(society.activeEvents.All(x => x.instanceId != InstanceId), "Duplicate fixture occurrence.");
        // Validate the untouched live capture first. This disposable fixture
        // replaces only unaccepted ordinary startup occurrences, rather than
        // appending beyond the real early-game one-ordinary-event limit.
        campaign.PrepareSociety(society);
        var ordinaryStartup = society.activeEvents.Where(x =>
        {
            var authored = ((ISocietyEventCatalog)catalog).Require(x.definitionId);
            return authored is not ServiceIncidentDefinitionSO
                && authored is not LifeEventDefinitionSO { emergency: true };
        }).ToArray();
        Require(ordinaryStartup.All(x => !x.resolved && string.IsNullOrEmpty(x.selectedChoiceId)
                && (x.guestDelivery == null || x.guestDelivery.phase == GuestRequestDeliveryPhase.None
                    && !x.guestDelivery.inputOwnerActive && string.IsNullOrEmpty(x.guestDelivery.operationId))),
            "Fixture cannot replace an accepted occurrence or physical delivery owner.");
        foreach (var startup in ordinaryStartup)
        {
            society.activeEvents.Remove(startup);
            lines.Add("fixture-only occurrence replacement=" + startup.instanceId
                + "; original capture valid; unaccepted and no physical delivery owner");
        }
        society.activeEvents.Add(new V20ActiveEventSaveData
        {
            instanceId = InstanceId, definitionId = DefinitionId, startedAbsoluteDay = 2,
            deadlineAbsoluteDay = 10, deterministicRoll = 37037, contextFactionId = factionId,
            riskTier = definition.riskTier, riskReason = definition.riskReason
        });
        campaign.PublishSociety(campaign.PrepareSociety(society));
        scope.Container.Resolve<IGameCalendar>().SetDateTime(2, 8);
        scope.Container.Resolve<IGameEventBus>().Publish(new OperatingDayStartedEvent(2));
        Require(scope.Container.Resolve<IV20DailyEvaluationDiagnostic>().LastDailyEvaluationSucceeded,
            "Actual daily producer rejected controlled day2.");
        var notices = FindFirstObjectByType<EventAlertRuntime>();
        var notice = notices.EventLog.Single(x => x.SourceId == InstanceId);
        int stockBefore = Quantity(), moneyBefore = money.Balance, rapportBefore = Rapport();
        initialTransactions = moneyLedger.Records.Select(x => x.transactionId).ToHashSet(StringComparer.Ordinal);
        lines.Add("baseline rawMoney=" + moneyBefore + "; rapport=" + rapportBefore + "; stock=" + stockBefore);
        Click("EventAlertButton_" + notice.Id);
        Click("EventChoice_1");
        yield return null;
        Require(campaign.ActiveSocietyEvents.Any(x => x.instanceId == InstanceId && x.selectedChoiceId == "fulfill" && !x.resolved)
            && !notices.IsDismissed(notice) && Quantity() == stockBefore && RequestAdjustedBalance() == moneyBefore
            && !RequestMoneyRecords().Any() && Rapport() == rapportBefore,
            "Missing-venue acceptance was rejected, dismissed, consumed stock or rewarded before physical delivery.");
        lines.Add("PASS actual notice acceptance with no RF25: active pending, physical debit0, reward0, notice retained");

        PlaceAuthoredFacility();
        PlaceAuthoredFacility();
        yield return null;
        if (verifyVenue)
            WimSocietyVenueLiveScenario.Verify(scope, supplyOrigin, BuildingId, lines);
        Require(facilities.FindOperational(FacilityCapabilityKind.None, BuildingId).Count == 2,
            "Two prepared actual RF25 instances did not become operational.");
        Require(initialIds.All(id => items.SetForbidden(id, false)), "Cannot release prepared drawing source.");
        var pendingBefore = items.Capture().pendingBatchDispositions.Select(x => x.operationId).ToHashSet(StringComparer.Ordinal);
        PhysicalItemBatchDispositionSaveData receipt = null;
        float wallStart = Time.realtimeSinceStartup, gameStart = clock.Time;
        Resume();
        while (Time.realtimeSinceStartup - wallStart < 90f && clock.Time - gameStart < 180f)
        {
            ObservePhysical();
            if (cancelOnly && sawCarried) break;
            // Full physical save projection is not a per-frame diagnostic. The
            // cheap actual stock debit is the trigger to inspect its receipt.
            if (Quantity() == stockBefore - 2)
                receipt = items.Capture().pendingBatchDispositions.FirstOrDefault(x => !pendingBefore.Contains(x.operationId)
                    && x.kind == (int)PhysicalItemDispositionKind.Transfer && x.quantity == 2
                    && x.sourceStackIds.Count > 0 && x.sourceStackIds.All(observedDeliveryStacks.Contains));
            if (receipt != null) break;
            Require(RequestAdjustedBalance() == moneyBefore && !RequestMoneyRecords().Any() && Rapport() == rapportBefore,
                "Reward changed before an observed physical delivery receipt.");
            yield return null;
        }
        Pause();
        lines.Add("delivery gameSeconds=" + (clock.Time - gameStart) + "; wallSeconds=" + (Time.realtimeSinceStartup - wallStart)
            + "; carried=" + sawCarried + "; arrived=" + sawArrived + "; destination=" + deliveryDestination);
        if (cancelOnly)
        {
            Require(sawCarried && receipt == null && Quantity() == stockBefore
                && RequestAdjustedBalance() == moneyBefore && !RequestMoneyRecords().Any() && Rapport() == rapportBefore,
                "Cancellation witness did not stop on actual cargo ownership before physical terminal debit.");
            yield return CancelCarried(stockBefore, moneyBefore, rapportBefore);
            yield break;
        }
        Require(receipt != null && sawCarried && !string.IsNullOrEmpty(deliveryDestination)
            && Quantity() == stockBefore - 2 && RequestAdjustedBalance() == moneyBefore && !RequestMoneyRecords().Any() && Rapport() == rapportBefore,
            "Actual haul did not reach a pending physical Transfer of two drawings before reward.");
        lines.Add("PASS actual AI pickup/move/arrival and physical commit=" + receipt.commitId);

        // Reuse the real pending owner; exercise the same state validator used
        // by capture/restore, without publishing malformed live state.
        RejectInvalidGuestState(value => value.destinationId =
            GuestRequestDeliveryOutbox.FormatDestinationId(InstanceId + ":other"), "foreign occurrence destination");
        RejectInvalidGuestState(value => value.inputOwnerActive = false, "committed receipt without active input owner");

        var saves = scope.Container.Resolve<IDungeonGameSaveService>();
        var pendingSave = saves.FromJson(saves.ToJson(saves.Capture()));
        var invalid = saves.FromJson(saves.ToJson(pendingSave));
        var invalidSociety = DungeonSaveSectionPayload.ReadOrNew<SocietyEventWorldSaveData>(invalid, SocietyEventsSaveSection.Id);
        Require(invalidSociety.activeEvents.RemoveAll(x => x.instanceId == InstanceId) == 1,
            "Physical-committed fixture no longer has exactly one active Society owner to invalidate.");
        DungeonSaveSectionPayload.Write(invalid, SocietyEventsSaveSection.Id, SocietyEventWorldSaveData.CurrentVersion,
            DungeonSaveRestorePhase.LateRuntimeState, invalidSociety);
        invalid.manifest = DungeonSaveManifest.Capture(invalid.sections);
        string societyBefore = JsonUtility.ToJson(campaign.CaptureSociety());
        string factionsBefore = JsonUtility.ToJson(campaign.CaptureFactions());
        string physicalBefore = JsonUtility.ToJson(items.Capture());
        int rawMoneyBeforeRestore = money.Balance;
        string ledgerBeforeRestore = JsonUtility.ToJson(moneyLedger.Capture());
        bool invalidAccepted = saves.TryRestore(invalid, out var rejected);
        lines.Add("invalid whole restore accepted=" + invalidAccepted
            + "; errors=" + string.Join(" | ", rejected.Errors)
            + "; sameSociety=" + (JsonUtility.ToJson(campaign.CaptureSociety()) == societyBefore)
            + "; sameFactions=" + (JsonUtility.ToJson(campaign.CaptureFactions()) == factionsBefore)
            + "; samePhysical=" + (JsonUtility.ToJson(items.Capture()) == physicalBefore)
            + "; sameMoney=" + (money.Balance == rawMoneyBeforeRestore)
            + "; sameLedger=" + (JsonUtility.ToJson(moneyLedger.Capture()) == ledgerBeforeRestore));
        Require(!invalidAccepted && !rejected.Success
            && rejected.Errors.Any(error => error.IndexOf("guest", StringComparison.OrdinalIgnoreCase) >= 0
                && (error.Contains(InstanceId, StringComparison.Ordinal)
                    || error.Contains(receipt.operationId, StringComparison.Ordinal)
                    || error.Contains(receipt.commitId, StringComparison.Ordinal)))
            && JsonUtility.ToJson(campaign.CaptureSociety()) == societyBefore
            && JsonUtility.ToJson(campaign.CaptureFactions()) == factionsBefore
            && JsonUtility.ToJson(items.Capture()) == physicalBefore && money.Balance == rawMoneyBeforeRestore
            && JsonUtility.ToJson(moneyLedger.Capture()) == ledgerBeforeRestore,
            "Orphan incoming physical request receipt accepted or failed restore mutated live owners.");
        lines.Add("PASS missing incoming Society owner rejected atomically: " + string.Join(" | ", rejected.Errors));
        Require(saves.TryRestore(pendingSave, out var restored) && restored.Success,
            "Exact current pending receipt restore failed: " + string.Join(" | ", restored.Errors));
        yield return null;
        Require(items.Capture().pendingBatchDispositions.Any(x => x.commitId == receipt.commitId)
            && Quantity() == stockBefore - 2 && RequestAdjustedBalance() == moneyBefore
            && !RequestMoneyRecords().Any() && Rapport() == rapportBefore,
            "Current whole restore lost physical receipt or changed pending rewards.");
        lines.Add("PASS exact whole pending JSON restore with normal completed hooks");
        ConfigureHauler();
        var transfers = scope.Container.Resolve<IPhysicalFacilityItemBatchTransferGateway>();
        wallStart = Time.realtimeSinceStartup;
        Resume();
        while (Time.realtimeSinceStartup - wallStart < 15f
            && (!campaign.RecentResolvedSocietyEvents.Any(x => x.instanceId == InstanceId && x.resolved)
                || transfers.TryGetPending(receipt.operationId, out _)))
            yield return null;
        Pause();
        Require(campaign.RecentResolvedSocietyEvents.Count(x => x.instanceId == InstanceId && x.resolved) == 1
            && campaign.ActiveSocietyEvents.All(x => x.instanceId != InstanceId)
            && items.Capture().pendingBatchDispositions.All(x => x.commitId != receipt.commitId)
            && Quantity() == stockBefore - 2 && RequestAdjustedBalance() == moneyBefore + 67 && Rapport() == rapportBefore + 4
            && HasExactRequestCredit(),
            "Actual terminal path did not grant authored reward exactly once and ACK physical receipt.");
        // Replay with the production dispatcher/current world, not an empty
        // requirements fixture which could itself force an unrelated rejection.
        bool replayAccepted = scope.Container.Resolve<IEventAlertChoiceActionDispatcher>()
            .TryDispatch(V21ContentAlertActionIds.Society(InstanceId, "fulfill"), out _);
        Require(RequestAdjustedBalance() == moneyBefore + 67 && HasExactRequestCredit()
            && Rapport() == rapportBefore + 4 && Quantity() == stockBefore - 2
            && campaign.RecentResolvedSocietyEvents.Count(x => x.instanceId == InstanceId && x.resolved) == 1,
            "Repeated resolved command paid or consumed again.");
        lines.Add("PASS terminal +67 money/+4 rapport, drawing debit2 once, receipt ACK, replayMutation0; replayAccepted=" + replayAccepted);
    }

    private IEnumerator CancelCarried(int stockBefore, int moneyBefore, int rapportBefore)
    {
        var carriedBefore = RequestCarriedStacks();
        Require(carriedBefore.Length > 0, "No request-owned carried slice at the cancellation boundary.");
        int grievanceBefore = ((IFactionCampaignQuery)campaign).Factions.Single(x => x.factionId == factionId).grievance;
        var notices = FindFirstObjectByType<EventAlertRuntime>();
        var notice = notices.EventLog.Single(x => x.SourceId == InstanceId);
        string declineAction = V21ContentAlertActionIds.Society(InstanceId, "decline");
        int choiceIndex = notice.Choices.Select((choice, index) => new { choice, index })
            .Where(x => x.choice.ActionId == declineAction).Select(x => x.index).Single();
        notices.CloseDetail();
        Click("EventAlertButton_" + notice.Id);
        Click("EventChoice_" + (choiceIndex + 1));
        yield return null;
        Require(carriedBefore.All(before => items.GetAllStacks().Any(after => after.StackId == before.StackId
                && after.Quantity == before.Quantity && after.Position == before.Position
                && after.State == WorldItemStackState.Carried && after.DestinationId == workerId))
            && Quantity() == stockBefore && RequestAdjustedBalance() == moneyBefore && !RequestMoneyRecords().Any() && Rapport() == rapportBefore,
            "Alive-hauler cancellation teleported, dropped, deleted or rewarded physical cargo while game time was paused.");
        // A successful synchronous retained-replan publication may retire the
        // old guest owner immediately. Do not require an artificial delay.
        if (campaign.ActiveSocietyEvents.Any(x => x.instanceId == InstanceId))
            Require(!notices.IsDismissed(notice), "Pending carried recovery lost its observable request notice.");
        lines.Add("PASS actual decline while carried; immediate exact stack/quantity/current-cell retained, no success reward");
        float wallStart = Time.realtimeSinceStartup, gameStart = clock.Time;
        Resume();
        while (Time.realtimeSinceStartup - wallStart < 45f && clock.Time - gameStart < 150f
            && (campaign.ActiveSocietyEvents.Any(x => x.instanceId == InstanceId)
                || items.GetAllStacks().Any(x => x.Quantity > 0 && x.DestinationId == deliveryDestination)
                || items.CaptureHaulDeliveryIntentsByDestination(deliveryDestination).Count != 0
                || RecoveredDrawings() != 2))
            yield return null;
        Pause();
        Require(world.Characters.Any(x => x != null && x.Identity.PersistentId == workerId && !x.IsDead)
            && RecoveredDrawings() == 2
            && campaign.ActiveSocietyEvents.All(x => x.instanceId != InstanceId)
            && campaign.RecentResolvedSocietyEvents.Count(x => x.instanceId == InstanceId
                && x.resolved && x.resolutionId == "decline") == 1
            && items.GetAllStacks().All(x => x.Quantity <= 0 || x.DestinationId != deliveryDestination)
            && items.CaptureHaulDeliveryIntentsByDestination(deliveryDestination).Count == 0
            && Quantity() == stockBefore && RequestAdjustedBalance() == moneyBefore && !RequestMoneyRecords().Any() && Rapport() == rapportBefore
            && ((IFactionCampaignQuery)campaign).Factions.Single(x => x.factionId == factionId).grievance == grievanceBefore + 5,
            "Actual decline/recovery did not retain stock, clear old ownership and publish the authored failure result exactly once.");
        // Normal capture invokes the current production before-capture joins;
        // it must not silently accept an orphaned retired guest input owner.
        var saves = scope.Container.Resolve<IDungeonGameSaveService>();
        Require(saves.FromJson(saves.ToJson(saves.Capture())) != null, "Terminal cancellation capture failed.");
        scope.Container.Resolve<IEventAlertChoiceActionDispatcher>().TryDispatch(declineAction, out _);
        Require(Quantity() == stockBefore && RequestAdjustedBalance() == moneyBefore && !RequestMoneyRecords().Any() && Rapport() == rapportBefore
            && ((IFactionCampaignQuery)campaign).Factions.Single(x => x.factionId == factionId).grievance == grievanceBefore + 5
            && RecoveredDrawings() == 2
            && campaign.ActiveSocietyEvents.All(x => x.instanceId != InstanceId)
            && campaign.RecentResolvedSocietyEvents.Count(x => x.instanceId == InstanceId && x.resolved) == 1
            && items.GetAllStacks().All(x => x.Quantity <= 0 || x.DestinationId != deliveryDestination)
            && items.CaptureHaulDeliveryIntentsByDestination(deliveryDestination).Count == 0,
            "Repeated decline changed stock or published the failure result again.");
        lines.Add("PASS alive-hauler recovery into actual warehouse/old-destination cleanup; physical stock conserved; failure grievance+5 once; capture joins valid"
            + "; gameSeconds=" + (clock.Time - gameStart) + "; wallSeconds=" + (Time.realtimeSinceStartup - wallStart));

        int RecoveredDrawings() => items.GetAllStacks().Where(x => x.ItemId == ItemId && !x.Forbidden
            && x.State == WorldItemStackState.Stored).Sum(x => x.Quantity);
    }

    private void ObservePhysical()
    {
        if (!scope.Container.Resolve<IGuestRequestDeliveryQuery>().TryGetDelivery(InstanceId, out var delivery)
            || string.IsNullOrEmpty(delivery.DestinationId)) return;
        if (deliveryDestination == null) deliveryDestination = delivery.DestinationId;
        Require(delivery.DestinationId == deliveryDestination, "An active guest delivery silently switched target.");
        foreach (var stack in items.GetAllStacks().Where(x => x.ItemId == ItemId && x.Quantity > 0
                     && !x.Forbidden && x.DestinationId == deliveryDestination))
        {
            observedDeliveryStacks.Add(stack.StackId);
            sawArrived |= stack.State == WorldItemStackState.FacilityBuffer;
        }
        var carried = RequestCarriedStacks();
        foreach (var stack in carried) observedDeliveryStacks.Add(stack.StackId);
        if (carried.Length == 0) return;
        sawCarried = true;
        Require(carried.Sum(x => x.Quantity) == 2 && Quantity() >= 2
            && delivery.Items.Single(x => x.ItemId == ItemId).Hauling == 2,
            "Actual two-drawing carrier custody differs from the request's hauling progress.");
    }

    private WorldItemStackSnapshot[] RequestCarriedStacks()
    {
        var carried = new List<WorldItemStackSnapshot>();
        foreach (var intent in items.CaptureHaulDeliveryIntentsByDestination(deliveryDestination))
        foreach (var commitment in intent.commitments.Where(x => x.itemId == ItemId && x.quantity > 0))
        {
            var stack = items.GetAllStacks().SingleOrDefault(x => x.StackId == commitment.carriedStackId);
            Require(intent.ownerCharacterId == workerId && stack != null
                && stack.State == WorldItemStackState.Carried && stack.DestinationId == workerId
                && stack.ItemId == ItemId && stack.Quantity == commitment.quantity && !stack.Forbidden,
                "Guest haul intent does not join its exact physical carrier custody.");
            carried.Add(stack);
        }
        Require(carried.Select(x => x.StackId).Distinct(StringComparer.Ordinal).Count() == carried.Count,
            "A physical carried stack was assigned to multiple guest haul commitments.");
        return carried.ToArray();
    }

    private void RejectInvalidGuestState(Action<GuestRequestDeliverySaveData> mutate, string label)
    {
        var candidate = campaign.CaptureSociety();
        string before = JsonUtility.ToJson(candidate);
        mutate(candidate.activeEvents.Single(x => x.instanceId == InstanceId).guestDelivery);
        bool rejected = false;
        try { campaign.PrepareSociety(candidate); }
        catch (InvalidOperationException error)
        {
            rejected = error.Message.Contains(InstanceId, StringComparison.Ordinal);
        }
        Require(rejected && JsonUtility.ToJson(campaign.CaptureSociety()) == before,
            "Society prepare must reject " + label + " with occurrence context without publishing state.");
        lines.Add("PASS Society candidate rejected: " + label);
    }

    private void PlaceAuthoredFacility()
    {
        var definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/ResearchOverhaul/RF25_시제품_연구실.asset");
        Require(definition != null && definition.id == 8825, "Authored RF25 missing.");
        var layout = scope.Container.Resolve<IRoomLayoutCache>().GetLayout(grid);
        if (verifyVenue)
        {
            foreach (var room in layout.Rooms.Where(r => r != null && r.IsUsable))
            {
                var free = room.Cells.Where(p => !grid.GetGridCell(p).HasOccupantInLayer(GridLayer.Building)
                    && !grid.GetGridCell(p).HasOccupantInLayer(GridLayer.Item));
                lines.Add("fixture room-space=" + room.Bounds + " free=" + string.Join(",", free));
            }
        }
        var position = layout.Rooms.Where(r => r != null && r.IsUsable)
            // Venue regression needs spare floor space as well as a legal
            // building footprint. Prefer the actual less-cluttered room;
            // normal/cancel-only witnesses retain their accepted placement.
            .OrderByDescending(r => verifyVenue ? r.Cells.Count(p =>
                !grid.GetGridCell(p).HasOccupantInLayer(GridLayer.Building)
                && !grid.GetGridCell(p).HasOccupantInLayer(GridLayer.Item)) : 0)
            .ThenBy(r => r.Bounds.yMin).ThenBy(r => r.Bounds.xMin)
            .SelectMany(r => r.Cells.OrderBy(p => p.y).ThenBy(p => p.x)
                .Where(p => definition.GetGridPosList(p).All(q => r.ContainsCell(q)
                    && grid.GetGridCell(q) is GridCell cell && cell.CanBuildInArea(definition)
                    && cell.CanOccupy(definition.Placement.Layer)) && HasReachableProspectiveAccess(definition, p)))
            .Select(p => (Vector2Int?)p).FirstOrDefault();
        Require(position.HasValue, "No legal room footprint for authored RF25.");
        var building = scope.Container.Resolve<IGridBuildingObjectFactory>().Create(grid, definition, position.Value);
        Require(building != null, "Production factory failed RF25.");
        foreach (var component in building.GetComponentsInChildren<MonoBehaviour>(true)) scope.Container.Inject(component);
        building.SetGrid(grid);
        building.Initialization(definition, position.Value);
        Require(grid.RegisterOccupant(building, definition.Placement.Layer,
            definition.GetGridPosList(position.Value), definition.Placement.IsMovement), "Grid rejected RF25.");
        lines.Add("fixture authored RF25 instance=" + building.RequirePersistentInstanceId() + "; anchor=" + position.Value);
    }

    private bool HasReachableProspectiveAccess(BuildingSO definition, Vector2Int anchor)
    {
        var worker = world.Characters.Single(x => x != null && x.Identity.PersistentId == workerId);
        var traversal = GridTraversalContext.ForCharacter(CharacterPersistentIdentity.Require(worker));
        var access = scope.Container.Resolve<IGridTraversalAccessQuery>();
        var costs = scope.Container.Resolve<IGridTraversalCostPolicy>();
        var footprint = definition.GetGridPosList(anchor);
        return BuildingWorkAccessRules.EnumerateCandidates(footprint, definition.IsGridMovement)
            .Where(p => grid.IsValidGridPos(p) && grid.IsWalkable(p))
            .Any(p => Reachable(supplyOrigin, p) && Reachable(preparedSource, p));

        bool Reachable(Vector2Int origin, Vector2Int target) => grid.SearchPathTo(origin, target,
            cell => (definition.IsGridMovement || !footprint.Contains(cell))
                && access.CanTraverse(grid, cell, traversal, out _), costs, traversal).GetMoveCostTo(target) != int.MaxValue;
    }

    private void ConfigureHauler()
    {
        var worker = world.Characters.Single(x => x != null && x.Identity.PersistentId == workerId);
        foreach (var actor in world.Characters.Where(x => x != null)) actor.SetAiPaused(actor != worker);
        foreach (var need in new[] { CharacterCondition.HUNGER, CharacterCondition.THIRST, CharacterCondition.SLEEP,
                     CharacterCondition.HYGIENE, CharacterCondition.EXCRETION, CharacterCondition.FUN })
            worker.Stats.ChangesStat(need, 100 - worker.Stats.GetConditionValue(need, 0));
        var work = worker.GetComponent<AbilityWork>();
        foreach (var type in WorkTypeCatalog.All) work.SetWorkPriority(type.WorkTypeId, WorkPriorityLevel.Off);
        work.SetDutyState(AbilityWork.DutyState.OnDuty);
        work.SetWorkPriority(BuiltInWorkTypeIds.Haul, WorkPriorityLevel.Priority1);
        Require(worker.Brain.PreferActionOnNextDecision<AIHaul>(), "Prepared worker lacks actual haul AI.");
        worker.Brain.RequestImmediateReplan(clearFailures: true);
    }

    private int Quantity() => items.GetAllStacks().Where(x => x.ItemId == ItemId).Sum(x => x.Quantity);
    private bool IsRequestMoney(EconomyTransactionRecord record) => new[] { "fulfill", "decline", "expired" }
        .Any(choice => record.sourceId == "content:" + V21ContentAlertActionIds.Society(InstanceId, choice));
    private IEnumerable<EconomyTransactionRecord> RequestMoneyRecords() => moneyLedger.Records.Where(x =>
        x.succeeded && !initialTransactions.Contains(x.transactionId) && IsRequestMoney(x));
    private bool HasExactRequestCredit()
    {
        var records = RequestMoneyRecords().ToArray();
        return records.Length == 1 && records[0].amount == 67
            && records[0].kind == EconomyTransactionKind.ContractIncome
            && records[0].sourceId == "content:" + V21ContentAlertActionIds.Society(InstanceId, "fulfill");
    }
    // Only the independently observed payroll is ambient in this witness.
    // A reward with a wrong source must fail, not disappear from attribution.
    private int RequestAdjustedBalance()
    {
        var transactions = moneyLedger.Records.Where(x =>
            x.succeeded && !initialTransactions.Contains(x.transactionId)).ToArray();
        bool IsPayroll(EconomyTransactionRecord record) => record.kind == EconomyTransactionKind.EmployeeWage
            && record.sourceId == "employment" && record.amount < 0;
        Require(transactions.All(x => IsRequestMoney(x) || IsPayroll(x)),
            "Unexpected non-payroll transaction: request reward attribution is not isolated.");
        return money.Balance - transactions.Where(IsPayroll).Sum(x => x.amount);
    }
    private int Rapport() => ((IFactionCampaignQuery)campaign).Factions.Single(x => x.factionId == factionId).rapport;
    private void Pause()
    {
        var host = FindFirstObjectByType<GameManager>();
        if (host != null) host.isPause = true;
        if (timeScale != null) timeScale.Scale = 0;
    }
    private void Resume() { FindFirstObjectByType<GameManager>().isPause = false; timeScale.Scale = 4; }
    private static void Click(string name)
    {
        var button = Resources.FindObjectsOfTypeAll<Button>().SingleOrDefault(x => x != null
            && x.gameObject.scene.isLoaded && x.gameObject.activeInHierarchy && x.name == name);
        Require(button != null && button.IsInteractable()
            && PlayModeVerificationFrameWait.DispatchPointerClick(button.gameObject, Vector2.zero), "Actual UI button unavailable: " + name);
    }
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
