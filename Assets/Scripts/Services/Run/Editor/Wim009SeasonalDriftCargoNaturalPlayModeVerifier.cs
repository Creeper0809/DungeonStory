#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;
using static UnityEngine.Object;

// Root-owned focused integration witness. Seasonal eligibility and the two
// Spring days are controlled, while daily dispatch, physical publication,
// ordinary AI hauling, storage and expiry use the registered production path.
public sealed class Wim009SeasonalDriftCargoNaturalPlayModeVerifier
{
    public static string ReportPath =>
        DungeonPersistencePaths.IsIsolatedVerification
            ? Path.Combine(
                Path.GetDirectoryName(DungeonPersistencePaths.Root),
                "eec03-drift-cargo-report.txt")
            : "Artifacts/QA/wim-implementation/wim-009-seasonal-drift-cargo-natural.txt";

    private const string DefinitionId = "seasonal:spring-drift-cargo";
    private const string ItemId = "material:lumber";
    private static bool running;
    private readonly List<string> lines = new();
    private readonly List<string> warnings = new();
    private readonly List<string> errors = new();
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
    private IItemQuantityReservationService quantityReservations;
    private GameplayOutcomeLedger outcomeLedger;
    private IDisposable resolvedSubscription;

    public static string StartFocused(
        IEnumerable<string> initialWarnings = null,
        IEnumerable<string> initialErrors = null)
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
        if (DungeonPersistencePaths.IsIsolatedVerification
            && File.Exists(ReportPath))
        {
            throw new InvalidOperationException(
                "Create-only EEC03 report target already exists.");
        }
        File.WriteAllText(ReportPath, "result=RUNNING\n");
        running = true;
        var verifier = new Wim009SeasonalDriftCargoNaturalPlayModeVerifier();
        if (initialWarnings != null)
            verifier.warnings.AddRange(initialWarnings);
        if (initialErrors != null)
            verifier.errors.AddRange(initialErrors);
        Application.logMessageReceived += verifier.CaptureLog;
        try
        {
            host.StartCoroutine(verifier.Observe());
        }
        catch
        {
            Application.logMessageReceived -= verifier.CaptureLog;
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
        Application.logMessageReceived -= CaptureLog;
        if (failure == null && (warnings.Count > 0 || errors.Count > 0))
        {
            failure = new InvalidOperationException(
                $"EEC03 captured warnings={warnings.Count}, errors={errors.Count}.");
        }
        lines.Add(failure == null ? "result=PASS" : "result=FAIL\n" + failure);
        lines.Add($"capturedWarnings={warnings.Count}");
        lines.Add($"capturedErrors={errors.Count}");
        lines.AddRange(warnings.Select(value => "warning=" + value));
        lines.AddRange(errors.Select(value => "error=" + value));
        lines.Add(
            "scope=controlled Spring eligibility/date through production OperatingDayStartedEvent; actual V20 daily resolution, seasonal application adapter, physical Loose/Carried/Stored ownership, ordinary AI full haul, exact public quantity reservation/pickup for partial acquisition, secured preservation, physical Sink child and parent ledger identity; not free-running calendar duration, all seasonal definitions, or six-adult balance");
        lines.Add(
            "cleanup=paused disposable protected Play; persistence disabled before owner preparation; operator stops without scene/profile/save writes");
        File.WriteAllLines(ReportPath, lines);
        Debug.Log(string.Join("\n", lines));
        running = false;
        if (Application.isBatchMode
            && DungeonPersistencePaths.IsIsolatedVerification)
        {
            DungeonFullWorldRoundTripPlayModeFacade.CleanupTransientArtifacts();
            EditorApplication.Exit(failure == null ? 0 : 1);
        }
    }

    private void CaptureLog(string condition, string stackTrace, LogType type)
    {
        if (type == LogType.Warning)
        {
            warnings.Add(condition ?? string.Empty);
        }
        else if (type is LogType.Error or LogType.Exception or LogType.Assert)
        {
            errors.Add((condition ?? string.Empty) + "\n"
                + (stackTrace ?? string.Empty));
        }
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
        quantityReservations =
            scope.Container.Resolve<IItemQuantityReservationService>();
        outcomeLedger = scope.Container.Resolve<GameplayOutcomeLedger>();
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
        lines.Add(
            "isolation=unrelated run-flow subscriptions and invasion threat producer disabled; V20 daily and seasonal cargo subscribers retained");

        OwnerRunManager owner = FindFirstObjectByType<OwnerRunManager>();
        Require(owner != null, "Owner preparation is unavailable.");
        if (owner.CurrentOwnerActor == null)
        {
            string fastCommit =
                StartPartyPreparationPlayModeVerifier.RunFastCommitForDebug();
            lines.Add("fixturePartyCommit=" + fastCommit);
            for (int frame = 0; frame < 8; frame++)
                yield return null;
            Pause();
            owner = FindFirstObjectByType<OwnerRunManager>();
            Require(owner != null && owner.CurrentOwnerActor != null,
                "The current direct-gameplay party fixture did not publish an owner. "
                    + fastCommit);
        }

        CharacterActor worker = world.Characters
            .Where(value => value != null
                && !value.IsOwner
                && !value.IsDead
                && value.CurrentLifecycleState == CharacterLifecycleState.Active
                && value.Brain != null
                && value.GetComponent<AbilityHaul>() != null)
            .OrderBy(value => value.Identity.PersistentId,
                StringComparer.Ordinal)
            .FirstOrDefault();
        Require(worker != null, "No active authored staff hauler is available.");
        Require(scope.Container.Resolve<IWarehouseWorldQuery>().Warehouses.Any(
                value => value != null
                    && value.HasWarehouseInventory),
            "The prepared run has no operational physical warehouse.");
        int firstDay = 10;
        resolved.Clear();
        PrepareOnlyTargetForDay(firstDay);
        calendar.SetDateTime(firstDay, 8);
        events.Publish(new OperatingDayStartedEvent(firstDay));
        IV20DailyEvaluationDiagnostic daily =
            scope.Container.Resolve<IV20DailyEvaluationDiagnostic>();
        Require(daily.LastDailyEvaluationAbsoluteDay == firstDay
            && daily.LastDailyEvaluationSucceeded,
            "Production daily dispatcher rejected the controlled Spring day: "
                + daily.LastDailyEvaluationFailure);
        V20ActiveEventSaveData first = campaign.ActiveSeasonalEvents.Single(
            value => string.Equals(value.definitionId, DefinitionId,
                StringComparison.Ordinal));
        Require(resolved.Count(value => string.Equals(
                    value.DefinitionId, DefinitionId, StringComparison.Ordinal)
                && string.Equals(value.ResolutionId, "started",
                    StringComparison.Ordinal)
                && string.Equals(value.OccurrenceInstanceId, first.instanceId,
                    StringComparison.Ordinal)) == 1,
            "The actual daily result did not publish the occurrence-bound Spring cargo event.");
        WorldItemStackSnapshot[] firstCargo = Cargo(first.instanceId);
        Require(first.seasonalDriftCargo.spawned
            && firstCargo.Sum(value => value.Quantity) == 6
            && firstCargo.All(value => value.ItemId == ItemId
                && value.State == WorldItemStackState.Loose),
            "Spring event did not publish exactly six physical Loose lumber units.");
        HashSet<string> firstCargoIds = firstCargo
            .Select(value => value.StackId)
            .ToHashSet(StringComparer.Ordinal);
        foreach (WorldItemStackSnapshot unrelated in items.GetAllStacks().Where(
                     value => value != null
                         && value.State == WorldItemStackState.Loose
                         && !firstCargoIds.Contains(value.StackId)))
            Require(items.SetForbidden(unrelated.StackId, true),
                "Could not isolate an unrelated Loose stack from focused hauling: "
                    + unrelated.StackId);
        lines.Add("[PASS] actual daily dispatch -> Spring occurrence -> exact Loose lumber6");

        ConfigureHauler(worker);
        Resume(4f);
        float wallStart = Time.realtimeSinceStartup;
        float gameStart = clock.Time;
        bool observedCarried = false;
        while (Time.realtimeSinceStartup - wallStart < 60f
            && clock.Time - gameStart < 180f)
        {
            foreach (CharacterActor actor in world.Characters.Where(value =>
                         value != null && value != worker))
                actor.SetAiPaused(true);
            firstCargo = Cargo(first.instanceId);
            observedCarried |= firstCargo.Any(value =>
                value.State == WorldItemStackState.Carried
                && string.Equals(value.DestinationId,
                    worker.Identity.PersistentId, StringComparison.Ordinal));
            if (firstCargo.Sum(value => value.State == WorldItemStackState.Stored
                    ? value.Quantity : 0) == 6)
                break;
            yield return null;
        }
        Pause();
        firstCargo = Cargo(first.instanceId);
        Require(observedCarried
            && firstCargo.Sum(value => value.State == WorldItemStackState.Stored
                ? value.Quantity : 0) == 6,
            "Ordinary AI haul did not carry and store all six event lumber units.");

        events.Publish(new OperatingDayStartedEvent(firstDay));
        first = campaign.ActiveSeasonalEvents.Single(value =>
            value.instanceId == first.instanceId);
        firstCargo = Cargo(first.instanceId);
        Require(first.seasonalDriftCargo.securedQuantity == 6
            && firstCargo.All(value => SeasonalDriftCargoRules.TryReadComponent(
                value, out string occurrenceId, out bool secured)
                && occurrenceId == first.instanceId
                && secured),
            "Stored cargo did not retain its exact occurrence component and secured marker.");
        int firstExpiryDay = first.deadlineAbsoluteDay + 1;
        string firstOccurrenceId = first.instanceId;
        calendar.SetDateTime(firstExpiryDay, 8);
        events.Publish(new OperatingDayStartedEvent(firstExpiryDay));
        first = campaign.ActiveSeasonalEvents.FirstOrDefault(value =>
                value.instanceId == firstOccurrenceId)
            ?? first;
        Require(first.seasonalDriftCargo.expirationCompleted
            && first.seasonalDriftCargo.expiredQuantity == 0
            && Cargo(first.instanceId).Sum(value => value.Quantity) == 6,
            "Secured stored cargo was removed or counted as expired.");
        lines.Add(
            "[PASS] real AI Loose -> Carried -> Stored; secured component preserved; deadline removed zero secured units");

        int secondDay = GameCalendarRules.DaysPerYear + 10;
        PrepareOnlyTargetForDay(secondDay);
        calendar.SetDateTime(secondDay, 8);
        events.Publish(new OperatingDayStartedEvent(secondDay));
        V20ActiveEventSaveData second = campaign.ActiveSeasonalEvents.Single(
            value => string.Equals(value.definitionId, DefinitionId,
                StringComparison.Ordinal));
        WorldItemStackSnapshot[] secondCargo = Cargo(second.instanceId);
        Require(secondCargo.Length == 1
            && secondCargo[0].Quantity == 6
            && secondCargo[0].State == WorldItemStackState.Loose,
            "Second Spring occurrence did not expose one exact Loose lumber6 source lot.");
        foreach (CharacterActor actor in world.Characters.Where(value =>
                     value != null))
            actor.SetAiPaused(true);
        if (worker.Brain.HasRunningAction)
        {
            Require(worker.Brain.StopCurrentActionForReplan(
                    "eec03-drift-cargo-partial-ready"),
                "The focused hauler retained an unrelated action before partial pickup.");
        }
        CharacterCarryInventory carry = worker.CarryInventory;
        Require(carry != null && carry.Items.Count == 0,
            "The focused hauler must start the partial pickup with an empty physical carry inventory.");
        WorldItemStackSnapshot source = secondCargo[0];
        string partialOperationId =
            "qa:eec03:partial:" + second.instanceId;
        string workerId = CharacterPersistentIdentity.Require(worker).Value;
        Require(quantityReservations.TryReserve(
                partialOperationId,
                workerId,
                ItemReservationPurpose.Hauling,
                "qa:eec03:partial-cargo",
                new ItemQuantityReservationRequest(
                    new ItemStackId(source.StackId),
                    2,
                    source.ReservationSignature),
                out ItemQuantityLease partialLease,
                out DomainFailure reservationFailure),
            "Exact partial cargo reservation failed: " + reservationFailure);
        var partialReservation = new WorldItemReservedStackQuantity(
            source.StackId,
            source.ItemId,
            2,
            source.Position,
            WorldItemHaulDestinationKind.Warehouse,
            string.Empty,
            partialLease.leaseId,
            partialOperationId);
        Require(items.TryPickupReservedStackQuantity(
                worker,
                carry,
                partialReservation,
                out int pickedUp,
                out string pickupFailure)
            && pickedUp == 2,
            "Exact public partial cargo pickup failed: " + pickupFailure);
        secondCargo = Cargo(second.instanceId);
        Require(secondCargo.Sum(value => value.Quantity) == 6
            && secondCargo.Sum(value =>
                value.State == WorldItemStackState.Loose
                    ? value.Quantity
                    : 0) == 4
            && secondCargo.Sum(value =>
                value.State == WorldItemStackState.Carried
                    ? value.Quantity
                    : 0) == 2
            && carry.Items.Where(value => value != null
                    && string.Equals(
                        value.ownerOperationId,
                        partialOperationId,
                        StringComparison.Ordinal))
                .Sum(value => value.quantity) == 2,
            "Physical quantity authority did not split Loose4 and Carried2 exactly.");

        events.Publish(new OperatingDayStartedEvent(secondDay));
        second = campaign.ActiveSeasonalEvents.Single(value =>
            value.instanceId == second.instanceId);
        secondCargo = Cargo(second.instanceId);
        WorldItemStackSnapshot securedPartial = secondCargo.Single(value =>
            value.State == WorldItemStackState.Carried);
        Require(second.seasonalDriftCargo.securedQuantity == 2
            && SeasonalDriftCargoRules.TryReadComponent(
                securedPartial,
                out string securedOccurrenceId,
                out bool secured)
            && securedOccurrenceId == second.instanceId
            && secured,
            "Production daily processing did not derive secured2 from the actually picked Carried stack.");
        lines.Add(
            "[PASS] exact public reservation/pickup split actual source Loose6 -> Loose4 + Carried2; production daily adapter recorded secured2");

        int secondExpiryDay = second.deadlineAbsoluteDay + 1;
        string secondOccurrenceId = second.instanceId;
        long expectedExpiredMassGrams = checked(
            items.MassQuery.GetDefinitionUnitMass(
                (ItemDefinitionId)ItemId).Value * 4L);
        Require(expectedExpiredMassGrams > 0L,
            "Current item catalog returned a non-positive lumber mass.");
        calendar.SetDateTime(secondExpiryDay, 8);
        events.Publish(new OperatingDayStartedEvent(secondExpiryDay));
        second = campaign.ActiveSeasonalEvents.FirstOrDefault(value =>
                value.instanceId == secondOccurrenceId)
            ?? second;
        WorldItemStackSnapshot[] remainingSecond = Cargo(second.instanceId);
        bool expiredSourcePresent = items.GetAllStacks().Any(value =>
            value != null && value.StackId == source.StackId);
        string expirationState =
            $"completed={second.seasonalDriftCargo.expirationCompleted};"
            + $"secured={second.seasonalDriftCargo.securedQuantity};"
            + $"expired={second.seasonalDriftCargo.expiredQuantity};"
            + $"mass={second.seasonalDriftCargo.expiredMassGrams};"
            + "sources=" + string.Join(",",
                second.seasonalDriftCargo.dispositionSourceStackIds)
            + ";remaining=" + string.Join(",", remainingSecond.Select(value =>
                $"{value.StackId}:{value.State}:{value.Quantity}"))
            + $";expectedSecured={securedPartial.StackId};"
            + $"expiredSourcePresent={expiredSourcePresent};"
            + $"priorSecured={Cargo(first.instanceId).Sum(value => value.Quantity)}";
        lines.Add("expirationState=" + expirationState);
        Require(second.seasonalDriftCargo.expirationCompleted
            && second.seasonalDriftCargo.securedQuantity == 2
            && second.seasonalDriftCargo.expiredQuantity == 4
            && second.seasonalDriftCargo.expiredMassGrams
                == expectedExpiredMassGrams
            && second.seasonalDriftCargo.dispositionSourceStackIds
                .SequenceEqual(new[] { source.StackId }, StringComparer.Ordinal)
            && remainingSecond.Length == 1
            && remainingSecond[0].StackId == securedPartial.StackId
            && remainingSecond[0].Quantity == 2
            && remainingSecond[0].State == WorldItemStackState.Carried
            && !expiredSourcePresent
            && Cargo(first.instanceId).Sum(value => value.Quantity) == 6,
            "Deadline did not sink exactly the unpicked source4 or damaged secured inventory: "
                + expirationState);

        string physicalOperationId =
            second.seasonalDriftCargo.dispositionOperationId;
        GameplayOutcomeSnapshot[] physicalChildren = ExactOutcomesForOperation(
            new GameplayOperationId(physicalOperationId));
        Require(physicalChildren.Length == 1,
            "Partial cargo expiration did not create exactly one physical child outcome.");
        GameplayOutcomeSnapshot physicalChild = physicalChildren[0];
        Require(physicalChild.producerId
                == "trade-inventory.physical-item-batch-disposition-receipt"
            && physicalChild.outcomeTypeId == TradeInventoryOutcomeIds.Result.Value
            && physicalChild.lifecycle
                == GameplayOutcomeRecordLifecycle.PublishedAcknowledged
            && physicalChild.participants.Any(value =>
                value.entityKindId == TradeInventoryOutcomeIds.ItemStackKind.Value
                && value.entityId == source.StackId
                && value.roleId == TradeInventoryOutcomeIds.SourceRole.Value)
            && physicalChild.subjects.Any(value =>
                value.entityKindId == TradeInventoryOutcomeIds.ItemStackKind.Value
                && value.entityId == source.StackId)
            && Metric(
                physicalChild,
                TradeInventoryOutcomeIds.InputQuantityMetric.Value) == 4d
            && Metric(
                physicalChild,
                TradeInventoryOutcomeIds.InputMassMetric.Value)
                == expectedExpiredMassGrams
            && Fact(
                physicalChild,
                TradeInventoryOutcomeIds.ReasonCodeFact.Value)
                == SeasonalDriftCargoRules.ExpirationReason
            && physicalChild.provenance.Any(value =>
                value.kindId == "domain-commit"
                && value.value
                    == second.seasonalDriftCargo.dispositionCommitId),
            "Physical child omitted its exact source stack, quantity, mass, reason, or commit identity.");

        GameplayResultKey parentKey = ParseResultKey(
            second.seasonalDriftCargo.outcomeResultKey);
        GameplayOutcomeReplayIdentity parentIdentity = default;
        GameplayOutcomeSnapshot parent = null;
        Require(outcomeLedger.TryGetResultIdentity(
                parentKey,
                out parentIdentity)
            && parentIdentity.State
                == GameplayOutcomeReplayState.PublishedAcknowledged
            && outcomeLedger.TryGetExact(
                parentIdentity.OutcomeId,
                out parent),
            "Partial cargo terminal parent outcome identity is not durable and exact.");
        MigratedProducerOutcomeDefinition parentDefinition =
            MigratedProducerOutcomeCatalog.Get(
                MigratedProducerOutcomeKind.SeasonalDriftCargoResult);
        Require(parent.producerId == parentDefinition.ProducerId
            && parent.outcomeTypeId == parentDefinition.OutcomeTypeId.Value
            && parent.status == GameplayOutcomeStatus.PartiallySucceeded
            && parent.lifecycle
                == GameplayOutcomeRecordLifecycle.PublishedAcknowledged
            && parent.participants.Any(value =>
                value.entityId == source.StackId)
            && parent.subjects.Any(value => value.entityId == source.StackId)
            && Metric(parent, MigratedProducerOutcomeIds.QuantityMetric.Value)
                == 6d
            && Metric(
                parent,
                MigratedProducerOutcomeIds.SecuredCountMetric.Value) == 2d
            && Metric(
                parent,
                MigratedProducerOutcomeIds.ExpiredCountMetric.Value) == 4d
            && Metric(parent, MigratedProducerOutcomeIds.MassMetric.Value)
                == expectedExpiredMassGrams
            && Fact(parent, MigratedProducerOutcomeIds.BatchFact.Value)
                == second.seasonalDriftCargo.dispositionCommitId,
            "Drift-cargo parent omitted the source occurrence settlement or exact physical-child commit link.");

        string cargoBeforeReplay = CargoIdentity(second);
        string inventoryBeforeReplay = InventoryIdentity(
            firstOccurrenceId,
            secondOccurrenceId,
            partialOperationId,
            carry);
        long physicalSequence = physicalChild.sequence;
        long parentSequence = parent.sequence;
        events.Publish(new OperatingDayStartedEvent(secondExpiryDay));
        second = campaign.ActiveSeasonalEvents.FirstOrDefault(value =>
                value.instanceId == secondOccurrenceId)
            ?? second;
        GameplayOutcomeSnapshot[] physicalAfterReplay =
            ExactOutcomesForOperation(new GameplayOperationId(
                physicalOperationId));
        Require(physicalAfterReplay.Length == 1
            && physicalAfterReplay[0].sequence == physicalSequence
            && outcomeLedger.TryGetResultIdentity(parentKey, out parentIdentity)
            && outcomeLedger.TryGetExact(parentIdentity.OutcomeId, out parent)
            && parent.sequence == parentSequence
            && CargoIdentity(second) == cargoBeforeReplay
            && InventoryIdentity(
                firstOccurrenceId,
                secondOccurrenceId,
                partialOperationId,
                carry) == inventoryBeforeReplay,
            "Duplicate deadline processing changed inventory, physical child, or terminal parent identity.");
        lines.Add(
            "[PASS] partial secured2/expired4 retained actual Carried2 and prior Stored6; exact physical child1 and parent1 stayed idempotent on duplicate processing");
    }

    private GameplayOutcomeSnapshot[] ExactOutcomesForOperation(
        GameplayOperationId operationId) => outcomeLedger
        .GetForOperation(operationId)
        .Items
        .Where(value => value.Exact != null)
        .Select(value => value.Exact)
        .OrderBy(value => value.sequence)
        .ToArray();

    private static GameplayResultKey ParseResultKey(string value)
    {
        string[] parts = (value ?? string.Empty).Split('|');
        if (parts.Length != 4
            || !long.TryParse(
                parts[2],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out long revision)
            || !int.TryParse(
                parts[3],
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int localIndex))
        {
            throw new InvalidOperationException(
                "Drift-cargo result key is not canonical: " + value);
        }
        return new GameplayResultKey(
            parts[0],
            new GameplayOperationId(parts[1]),
            revision,
            localIndex);
    }

    private static double Metric(GameplayOutcomeSnapshot outcome, string id) =>
        outcome.metrics.Single(value => value.metricId == id).value;

    private static string Fact(GameplayOutcomeSnapshot outcome, string id) =>
        outcome.facts.Single(value => value.factId == id).value;

    private static string CargoIdentity(V20ActiveEventSaveData occurrence)
    {
        SeasonalDriftCargoSaveData cargo = occurrence.seasonalDriftCargo;
        return occurrence.instanceId
            + "|sources=" + string.Join(",", cargo.sourceStackIds.OrderBy(
                value => value,
                StringComparer.Ordinal))
            + "|secured=" + cargo.securedQuantity
            + "|expired=" + cargo.expiredQuantity
            + "|mass=" + cargo.expiredMassGrams
            + "|physicalOperation=" + cargo.dispositionOperationId
            + "|physicalCommit=" + cargo.dispositionCommitId
            + "|physicalSources=" + string.Join(",",
                cargo.dispositionSourceStackIds.OrderBy(
                    value => value,
                    StringComparer.Ordinal))
            + "|outcome=" + cargo.outcomeResultKey;
    }

    private string InventoryIdentity(
        string firstOccurrenceId,
        string secondOccurrenceId,
        string partialOperationId,
        CharacterCarryInventory carry)
    {
        string physical = string.Join(",",
            Cargo(firstOccurrenceId)
                .Concat(Cargo(secondOccurrenceId))
                .OrderBy(value => value.StackId, StringComparer.Ordinal)
                .Select(value =>
                {
                    Require(SeasonalDriftCargoRules.TryReadComponent(
                            value,
                            out string occurrenceId,
                            out bool secured),
                        "Cargo inventory identity lost its occurrence component.");
                    return value.StackId + ":" + occurrenceId + ":"
                        + value.State + ":" + value.Quantity + ":"
                        + secured + ":" + value.DestinationId;
                }));
        string carried = string.Join(",",
            carry.Items
                .Where(value => value != null
                    && string.Equals(
                        value.ownerOperationId,
                        partialOperationId,
                        StringComparison.Ordinal))
                .OrderBy(value => value.carriedStackId, StringComparer.Ordinal)
                .Select(value => value.carriedStackId + ":" + value.itemId
                    + ":" + value.quantity + ":" + value.sourceStackId));
        return "physical=" + physical + "|carry=" + carried;
    }

    private void PrepareOnlyTargetForDay(int absoluteDay)
    {
        SeasonalEventWorldSaveData state = campaign.CaptureSeasonal();
        state.activeEvents.Clear();
        state.completedEventIds = catalog.SeasonalEvents
            .Where(value => value.season == Season.Spring
                && !string.Equals(value.StableId, DefinitionId,
                    StringComparison.Ordinal))
            .Select(value => value.StableId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToList();
        state.cycle = absoluteDay / GameCalendarRules.DaysPerYear;
        state.lastEvaluationAbsoluteDay = absoluteDay - 1;
        campaign.PublishSeasonal(campaign.PrepareSeasonal(state));
    }

    private WorldItemStackSnapshot[] Cargo(string occurrenceId) => items
        .GetAllStacks()
        .Where(value => value != null
            && SeasonalDriftCargoRules.TryReadComponent(
                value, out string owner, out _)
            && string.Equals(owner, occurrenceId, StringComparison.Ordinal))
        .OrderBy(value => value.StackId, StringComparer.Ordinal)
        .ToArray();

    private void ConfigureHauler(CharacterActor worker)
    {
        foreach (CharacterActor actor in world.Characters.Where(value => value != null))
            actor.SetAiPaused(actor != worker);
        if (worker.Brain.HasRunningAction)
        {
            Require(worker.Brain.StopCurrentActionForReplan(
                    "wim009-drift-cargo-ready"),
                "The selected worker's prior action could not be cancelled through its typed replan path.");
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
            worker.Stats.ChangesStat(
                need,
                100f - worker.Stats.GetConditionValue(need, 0f));
        AbilityWork work = worker.GetComponent<AbilityWork>();
        Require(work != null, "The selected hauler has no work authority.");
        work.ClearPriorityWorkTarget();
        work.SetDutyState(AbilityWork.DutyState.OnDuty);
        work.SetWorkPriority(BuiltInWorkTypeIds.Haul, WorkPriorityLevel.Priority1);
        Require(worker.Brain.PreferActionOnNextDecision<AIHaul>(),
            "The selected worker has no authored haul action.");
        worker.Brain.RequestImmediateReplan(clearFailures: true);
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

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
