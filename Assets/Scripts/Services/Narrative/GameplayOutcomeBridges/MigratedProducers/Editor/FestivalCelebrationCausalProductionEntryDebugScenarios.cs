#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization;
using DungeonStory.Buildings;
using DungeonStory.Foundation;
using DungeonStory.Operation;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

/// <summary>
/// Runs the authored long-night memorial through the public production festival
/// state machine. The only controlled setup is the isolated world, ten eligible
/// residents, the authored RF87 venue projection, and placement of the authored
/// candle requirement into the real facility buffer. Scheduling, preparation,
/// physical consumption, attendance, effects, outcome commit, cleanup and
/// current-format save/restore are production authorities.
/// </summary>
public static class FestivalCelebrationCausalProductionEntryDebugScenarios
{
    internal const string OwnerKind = "owner-kind:festival-execution";
    internal const string RestoreKind = "owner-family:festival-execution";
    internal const string FirstCelebrationActionId =
        "action:celebrate-authored-long-night-memorial-year-one";
    internal const string SecondCelebrationActionId =
        "action:restore-and-celebrate-authored-long-night-memorial-year-two";

    private const string Schema =
        "dungeonstory-causal-festival-execution-owner-state@1";
    private const string FestivalId = "festival:long-night-memorial";
    private const string CandleItemId = "craft:candle";
    private const string VenueDefinitionId = "building:8887";
    private const string VenueInstanceId = "building:causal-festival-rf87";
    private const string FestivalAlertCategory = "V21 축제";
    private const string RememberedCharacterId =
        "character:causal-festival:remembered";
    private const int FirstYear = 2;
    private const int SecondYear = 3;
    private const int ParticipantCount = 10;
    private const int CandleCount = 12;

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId,
        string ownerId = null,
        JObject ownerCheckpoint = null)
    {
        if (string.IsNullOrWhiteSpace(causalRunId))
            throw new ArgumentException("A causal run ID is required.", nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        if (!persistent && ownerCheckpoint != null)
            throw new ArgumentException(
                "A festival checkpoint requires an owner ID.",
                nameof(ownerCheckpoint));

        var context = new Context(
            causalRunId,
            persistent ? ownerId : "festival-execution:" + causalRunId,
            ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(2, context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                FirstCelebrationActionId,
                "society",
                MigratedProducerOutcomeKind.FestivalCelebratedEvent,
                context.CaptureState,
                context.CelebrateFirstYear,
                () => context.RequireExactOutcome(FirstYear),
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                persistent ? context.OwnerId : null,
                persistent ? OwnerKind : null,
                persistent ? context.CaptureOwnerCheckpoint : null,
                persistent ? RestoreKind : null,
                persistent ? context : null),
            new DelegateProductionCausalActionFixture(
                SecondCelebrationActionId,
                "society",
                MigratedProducerOutcomeKind.FestivalCelebratedEvent,
                context.CaptureState,
                context.CelebrateSecondYear,
                () => context.RequireExactOutcome(SecondYear),
                context.CaptureOutcomeEvidence,
                lifetime.Release,
                persistent ? context.OwnerId : null,
                persistent ? OwnerKind : null,
                persistent ? context.CaptureOwnerCheckpoint : null,
                persistent ? RestoreKind : null,
                persistent ? context : null)
        };
    }

    internal static bool RunCausalOwnerFamily(bool log = false)
    {
        const string ownerId = "owner:causal-festival-execution:focused";
        JObject first;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-festival-execution:first",
                   ownerId)))
        {
            IProductionCausalActionFixture firstAction = fixtures.Get(
                FirstCelebrationActionId);
            Require((int)firstAction.CaptureState()["festival.stage"] == 0,
                "Festival owner did not start before the first authored occurrence.");
            Require(firstAction.Execute(),
                "The first authored long-night memorial did not resolve.");
            Require(firstAction.RequireExactOutcome().HasCanonicalPayloadHash,
                "The first festival outcome has no canonical hash.");
            IReadOnlyDictionary<string, object> after = firstAction.CaptureState();
            Require(
                (int)after["festival.stage"] == 1
                && (int)after["festival.resolvedOccurrenceCount"] == 1
                && (int)after["festival.successfulAttendanceCount"] == ParticipantCount
                && (int)after["festival.candleQuantity"] == 0
                && (int)after["festival.exactOutcomeCount"] == 1,
                "The first production festival did not preserve its terminal effects.");
            first = ((IProductionCausalOwnerCheckpointFixture)firstAction)
                .CaptureOwnerCheckpoint();
        }

        JObject terminal;
        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-festival-execution:second",
                   ownerId,
                   first)))
        {
            IProductionCausalActionFixture firstAction = fixtures.Get(
                FirstCelebrationActionId);
            IProductionCausalActionFixture secondAction = fixtures.Get(
                SecondCelebrationActionId);
            Require(!firstAction.Execute(),
                "Fresh restore replayed the first festival occurrence.");
            Require(secondAction.Execute(),
                "The restored owner did not resolve the second authored occurrence.");
            Require(secondAction.RequireExactOutcome().HasCanonicalPayloadHash,
                "The second festival outcome has no canonical hash.");
            IReadOnlyDictionary<string, object> after = secondAction.CaptureState();
            Require(
                (int)after["festival.stage"] == 2
                && (int)after["festival.resolvedOccurrenceCount"] == 2
                && (int)after["festival.successfulAttendanceCount"] == ParticipantCount * 2
                && (int)after["festival.candleQuantity"] == 0
                && (int)after["festival.exactOutcomeCount"] == 2,
                "The restored festival owner lost cumulative physical or social effects.");
            terminal = ((IProductionCausalOwnerCheckpointFixture)secondAction)
                .CaptureOwnerCheckpoint();
        }

        using (var fixtures = new FixtureSet(CreateFixtures(
                   "run:causal-festival-execution:terminal",
                   ownerId,
                   terminal)))
        {
            IProductionCausalActionFixture firstAction = fixtures.Get(
                FirstCelebrationActionId);
            IProductionCausalActionFixture secondAction = fixtures.Get(
                SecondCelebrationActionId);
            JObject before = ((IProductionCausalOwnerCheckpointFixture)firstAction)
                .CaptureOwnerCheckpoint();
            ProductionCausalOwnerCheckpoint.RequireRestoredEquivalent(
                terminal,
                before);
            Require(!firstAction.Execute() && !secondAction.Execute(),
                "Terminal festival owner was not idempotent.");
            JObject after = ((IProductionCausalOwnerCheckpointFixture)secondAction)
                .CaptureOwnerCheckpoint();
            Require(JToken.DeepEquals(before, after),
                "Rejected terminal festival replay mutated its checkpoint.");
        }

        foreach (MigratedProducerOutcomeFaultMode fault in new[]
                 {
                     MigratedProducerOutcomeFaultMode.RejectReservation,
                     MigratedProducerOutcomeFaultMode.RejectCommit
                 })
        {
            using var context = new Context(
                "run:causal-festival-execution:fault:" + fault,
                ownerId,
                first);
            Require(context.ExerciseSecondCelebrationFault(fault),
                "Festival resolution did not roll back effects and retry exactly once after "
                + fault + ".");
        }

        JObject tamperedFestival = (JObject)first.DeepClone();
        ((JObject)tamperedFestival["domainState"])["festivalId"] =
            "festival:tampered";
        Require(Reject(ownerId, tamperedFestival, "festival-id-tamper"),
            "A changed authored festival ID was accepted.");

        JObject tamperedYear = (JObject)first.DeepClone();
        JObject domain = (JObject)tamperedYear["domainState"];
        JObject execution = (JObject)domain["festivalExecutions"];
        ((JObject)((JArray)execution["occurrences"])[0])["occurrenceYear"] = 99;
        Require(Reject(ownerId, tamperedYear, "occurrence-year-tamper"),
            "A changed terminal occurrence year was accepted.");

        if (log)
        {
            Debug.Log(
                "Authored long-night memorial year 2 -> current-format save/new composition restore -> year 3; actual candles, attendance, mood, grief, faction and exact ledger causal owner PASS");
        }
        return true;
    }

    public static void RunCausalBatch()
    {
        try
        {
            Require(RunCausalOwnerFamily(log: true),
                "Festival causal owner family returned false.");
        }
        catch (Exception exception)
        {
            Debug.LogException(exception);
            throw;
        }
    }

    public static void RunPresentationOwnerForBatch()
    {
        using (var disposed = new Context(
                   "run:causal-festival-presentation:disposed",
                   "owner:causal-festival-presentation:disposed",
                   null))
        {
            disposed.RequireFactsWithoutPresentation();
        }

        using (var initializedOnce = new Context(
                   "run:causal-festival-presentation:initialized-once",
                   "owner:causal-festival-presentation:initialized-once",
                   null))
        {
            initializedOnce.RequireReinitializedPresentation(1);
        }

        using (var initializedTwice = new Context(
                   "run:causal-festival-presentation:initialized-twice",
                   "owner:causal-festival-presentation:initialized-twice",
                   null))
        {
            initializedTwice.RequireReinitializedPresentation(2);
        }

        using (var restored = new Context(
                   "run:causal-festival-presentation:restored",
                   "owner:causal-festival-presentation:restored",
                   null))
        {
            restored.RequireRestoredPresentationGeneration();
        }
    }

    private static bool Reject(string ownerId, JObject checkpoint, string suffix)
    {
        try
        {
            using var ignored = new FixtureSet(CreateFixtures(
                "run:causal-festival-execution:" + suffix,
                ownerId,
                checkpoint));
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }

    private sealed class Context : IDisposable
    {
        private readonly string causalRunId;
        private readonly CustomerAiDebugScenarios.CustomerAiScenarioWorld world;
        private readonly CharacterWorldSaveService actorSave;
        private readonly DungeonRuntimeAggregateRootStore aggregateRoots = new();
        private readonly MigratedProducerOutcomeEditorFixture outcomes;
        private readonly MigratedProducerOutcomeProbeTransaction probe;
        private readonly SocialRitualFactEditorFixture socialFacts;
        private readonly ResourceGameContentCatalog content;
        private readonly FestivalDefinitionCatalog festivals;
        private readonly FestivalDefinitionSO festival;
        private readonly MutableCalendar calendar = new();
        private readonly MutableClock clock = new();
        private readonly GameEventBus events = new();
        private readonly List<FestivalExecutionProgressedFact> presentationFacts = new();
        private readonly List<EventAlertRequest> presentationAlerts = new();
        private readonly IDisposable presentationFactSubscription;
        private readonly IDisposable presentationAlertSubscription;
        private readonly SeasonalSocialPresentationObserver presentation;
        private readonly List<CharacterActor> actors = new();
        private readonly BuildableObject venueFacility;
        private readonly BuildingSO venueDefinition;
        private readonly SocietyVenueCandidateSnapshot venue;
        private readonly WorldItemStackRuntime items;
        private readonly WorldItemRepository repository;
        private readonly IDungeonRestoreTransactionParticipant exactRouteRestore;
        private readonly IPhysicalItemBatchDispositionService batch;
        private readonly PhysicalFacilityItemSinkGateway materialSink;
        private readonly EconomyProjectInputOwnerFixtureAuthority inputOwners;
        private readonly V20CampaignRuntime campaign;
        private readonly GriefTraumaRuntime psychosocial;
        private readonly FestivalExecutionRuntime runtime;
        private readonly FestivalExecutionSaveSection festivalSave;
        private readonly Dictionary<string, int> initialFactionRapport;
        private bool presentationActive;
        private bool disposed;

        internal Context(string causalRunId, string ownerId, JObject checkpoint)
        {
            this.causalRunId = CausalGameplaySyntax.RequireId(
                causalRunId,
                nameof(causalRunId));
            OwnerId = CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            outcomes = new MigratedProducerOutcomeEditorFixture(
                ProductionCausalOwnerCheckpoint.LedgerRunId(OwnerId, OwnerKind),
                aggregateRoots);
            probe = new MigratedProducerOutcomeProbeTransaction(
                outcomes.Transaction,
                outcomes.Recorder);
            socialFacts = new SocialRitualFactEditorFixture(outcomes, probe);
            content = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
            festivals = new FestivalDefinitionCatalog(content);
            festival = festivals.Require(FestivalId);
            RequireAuthoredDefinition();
            presentationFactSubscription = events.Subscribe<FestivalExecutionProgressedFact>(
                value => presentationFacts.Add(value));
            presentationAlertSubscription = events.Subscribe<EventAlertRequestedEvent>(value =>
            {
                if (value.request != null
                    && string.Equals(
                        value.request.Category,
                        FestivalAlertCategory,
                        StringComparison.Ordinal))
                {
                    presentationAlerts.Add(value.request);
                }
            });
            presentation = new SeasonalSocialPresentationObserver(events);
            presentation.Initialize();
            presentationActive = true;

            world = new CustomerAiDebugScenarios.CustomerAiScenarioWorld();
            try
            {
                // 캐릭터 단위 저장과 복원은 같은 이동 provenance 소유자를 사용한다.
                actorSave = (CharacterWorldSaveService)FormatterServices.GetUninitializedObject(
                    typeof(CharacterWorldSaveService));
                FieldInfo responseMovements = typeof(CharacterWorldSaveService).GetField(
                    "responseMovements", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?? throw new InvalidOperationException(
                        "Character actor save requires movement provenance authority.");
                responseMovements.SetValue(actorSave, new SocietyResponseMovementProvenanceRuntime());
                venueDefinition = Object.Instantiate(
                    AssetDatabase.LoadAssetAtPath<BuildingSO>(
                        "Assets/Resources/SO/Building/ResearchOverhaul/RF87_추모실.asset")
                    ?? throw new InvalidOperationException(
                        "The authored RF87 memorial venue asset is missing."));
                venueFacility = CreateVenueFacility(venueDefinition);
                CreateActors(detachedRestore: checkpoint != null);
                venue = CreateVenueSnapshot();

                items = PhysicalItemDebugScenarios
                    .CreateDeliveryRuntimeForCrossDomainFixture(
                        new FixedGridProvider(world.Grid),
                        out repository,
                        out _,
                        out _,
                        out _,
                        out batch,
                        out _,
                        out exactRouteRestore);
                IStockQuery stock = new PhysicalStockQuery(
                    repository,
                    items.CatalogProvider,
                    items.MassQuery);
                materialSink = new PhysicalFacilityItemSinkGateway(stock, batch);
                inputOwners = new EconomyProjectInputOwnerFixtureAuthority(items.MassQuery);
                campaign = new V20CampaignRuntime(
                    aggregateRoots,
                    new V20StoryContentCatalog(content),
                    batch,
                    null,
                    socialFacts.Journal);
                initialFactionRapport = campaign.CaptureFactions().factions
                    .ToDictionary(
                        value => value.factionId,
                        value => value.rapport,
                        StringComparer.Ordinal);
                psychosocial = new GriefTraumaRuntime(aggregateRoots);
                foreach (CharacterActor actor in actors)
                    psychosocial.Require(CharacterPersistentIdentity.Require(actor));
                if (checkpoint == null)
                    SeedGrief();

                runtime = new FestivalExecutionRuntime(
                    festivals,
                    new FixedCharacterWorld(actors),
                    new FixedVenueQuery(venue),
                    EmptyNarrativeQuery.Instance,
                    new ResidentStandingQuery(actors.Count),
                    NoCombatQuery.Instance,
                    SafeHazardQuery.Instance,
                    new FixedGridProvider(world.Grid),
                    calendar,
                    clock,
                    events,
                    psychosocial,
                    campaign,
                    campaign,
                    inputOwners.Runtime,
                    items,
                    materialSink,
                    FixedWorkAmount.Instance,
                    factPublisher: socialFacts.Journal);
                festivalSave = new FestivalExecutionSaveSection(
                    runtime,
                    items,
                    inputOwners.RestoreRuntime);

                if (checkpoint != null)
                    RestoreOwnerCheckpoint(checkpoint);
                Validate();
            }
            catch
            {
                Dispose();
                throw;
            }
        }

        internal string OwnerId { get; }
        internal FestivalExecutionRuntime Runtime => runtime;
        internal MigratedProducerOutcomeEditorFixture Outcomes => outcomes;
        internal SocialRitualFactEditorFixture Facts => socialFacts;

        private int Stage => runtime?.Occurrences.Count(value =>
            value.effectsApplied
            && value.phase == FestivalExecutionPhase.Resolved
            && value.resultGrade == FestivalResolutionGrade.Success) ?? 0;

        private IReadOnlyList<GameplayOutcomeSnapshot> Exact => outcomes.Ledger
            .CaptureGameplayOutcomes()
            .exactOutcomes
            .Where(value => string.Equals(
                value.outcomeTypeId,
                Type(MigratedProducerOutcomeKind.FestivalCelebratedEvent),
                StringComparison.Ordinal))
            .OrderBy(value => value.absoluteDay)
            .ThenBy(value => value.operationId, StringComparer.Ordinal)
            .ToArray();

        internal bool CelebrateFirstYear() => Celebrate(FirstYear, expectedStage: 0);
        internal bool CelebrateSecondYear() => Celebrate(SecondYear, expectedStage: 1);

        internal void RequireFactsWithoutPresentation()
        {
            presentation.Dispose();
            presentationActive = false;
            int factCount = presentationFacts.Count;
            int alertCount = presentationAlerts.Count;
            Require(CelebrateFirstYear(),
                "Festival did not complete while its optional presentation observer was disposed.");
            Require(presentationFacts.Count == factCount + 4
                && presentationAlerts.Count == alertCount,
                "Disposed festival presentation suppressed neutral facts or emitted an alert.");
        }

        internal void RequireReinitializedPresentation(int initializeCount)
        {
            Require(initializeCount is 1 or 2,
                "Festival presentation reinitialization probe count is invalid.");
            presentation.Dispose();
            presentationActive = false;
            for (int index = 0; index < initializeCount; index++)
                presentation.Initialize();
            presentationActive = true;
            int factCount = presentationFacts.Count;
            int alertCount = presentationAlerts.Count;
            Require(CelebrateFirstYear(),
                "Festival did not complete after presentation reinitialization.");
            Require(presentationFacts.Count == factCount + 4
                && presentationAlerts.Count == alertCount + 4,
                "Festival presentation reinitialization duplicated or suppressed alerts.");
        }

        internal void RequireRestoredPresentationGeneration()
        {
            Require(CelebrateFirstYear(),
                "Festival did not complete before presentation restore probing.");
            string occurrenceId = FestivalExecutionRules.OccurrenceId(
                FestivalId,
                FirstYear);
            FestivalExecutionProgressedFact beforeFact = presentationFacts.Last(value =>
                string.Equals(value.OccurrenceId, occurrenceId, StringComparison.Ordinal));
            JObject beforeDomain = CaptureDomain();
            FestivalExecutionWorldSaveData saved = runtime.CaptureFestivalExecutions();
            FestivalExecutionAggregateState restored =
                runtime.PrepareFestivalExecutionRestore(saved);
            runtime.PublishFestivalExecutionRestore(restored);
            int factCount = presentationFacts.Count;
            int alertCount = presentationAlerts.Count;
            clock.SetDelta(0f);
            runtime.Advance();

            FestivalExecutionProgressedFact afterFact = presentationFacts
                .Skip(factCount)
                .Single(value => string.Equals(
                    value.OccurrenceId,
                    occurrenceId,
                    StringComparison.Ordinal));
            EventAlertRequest afterAlert = presentationAlerts
                .Skip(alertCount)
                .Single(value => string.Equals(
                    value.SourceId,
                    occurrenceId,
                    StringComparison.Ordinal));
            Require(afterFact.Change == FestivalExecutionChange.Advanced
                && afterFact.Phase == FestivalExecutionPhase.Resolved
                && afterFact.StateGeneration > beforeFact.StateGeneration
                && afterAlert.IsResolved
                && string.Equals(
                    afterAlert.ResultSummary,
                    afterFact.ResultSummary,
                    StringComparison.Ordinal)
                && JToken.DeepEquals(beforeDomain, CaptureDomain()),
                "Restored festival generation was suppressed by stale presentation state or replayed effects.");
        }

        private bool Celebrate(int year, int expectedStage)
        {
            if (Stage != expectedStage)
                return false;
            int factCount = presentationFacts.Count;
            int alertCount = presentationAlerts.Count;
            string occurrenceId = FestivalExecutionRules.OccurrenceId(FestivalId, year);
            FestivalExecutionOccurrenceSaveData state = runtime.Occurrences
                .SingleOrDefault(value => string.Equals(
                    value.occurrenceId,
                    occurrenceId,
                    StringComparison.Ordinal));
            bool newlyScheduled = state == null;
            if (state == null)
            {
                calendar.SetDateTime(
                    FestivalExecutionRules.AnnouncementAbsoluteDay(festival, year),
                    8);
                bool scheduled = runtime.Schedule(new FestivalScheduleRequest
                {
                    ActionId = year == FirstYear
                        ? FirstCelebrationActionId
                        : SecondCelebrationActionId,
                    FestivalId = FestivalId,
                    OccurrenceYear = year,
                    ParticipantIds = actors
                        .Select(CharacterPersistentIdentity.Require)
                        .Reverse()
                        .ToArray()
                }, out FestivalPreparedOrder order, out DomainFailure failure);
                Require(scheduled && !failure.IsFailure && order != null,
                    "Production festival scheduling failed: " + failure);
                state = runtime.Occurrences.Single(value => value.occurrenceId == occurrenceId);
                Require(state.attendance.Count == ParticipantCount
                    && state.itemCosts.Count == 1
                    && state.itemCosts[0].itemId == CandleItemId
                    && state.itemCosts[0].quantity == CandleCount
                    && state.inputOwnerActive,
                    "The authored roster, candle recipe or physical input owner changed.");

                // Controlled material placement only. The production runtime owns
                // the actual facility-buffer read, exact sink receipt and cleanup.
                repository.AddEditorTestStack(
                    CandleItemId,
                    CandleCount,
                    WorldItemStackState.FacilityBuffer,
                    state.materialDestinationId,
                    position: new Vector2Int(state.venueAnchorX, state.venueAnchorY));
                clock.SetDelta(Mathf.Max(4f, state.requiredPreparationWork + 1f));
                runtime.Advance();
                state = runtime.Occurrences.Single(value => value.occurrenceId == occurrenceId);
                Require(state.phase == FestivalExecutionPhase.Preparing
                    && state.completedPreparationWork + 0.0001f
                        >= state.requiredPreparationWork,
                    "Actual festival preparation work did not complete.");

                calendar.SetDateTime(
                    state.festivalAbsoluteDay,
                    state.startHour);
                clock.SetDelta(0f);
                runtime.Advance();
                state = runtime.Occurrences.Single(value => value.occurrenceId == occurrenceId);
                Require(state.phase == FestivalExecutionPhase.Running
                    && state.materialReceiptPending
                    && CandleQuantity() == 0,
                    "The production festival did not consume the exact candle lot.");
            }

            if (state.phase == FestivalExecutionPhase.Running)
            {
                PositionActorsAtAssignedCells(state);
                clock.SetDelta(Mathf.Max(
                    0f,
                    state.plannedDurationSeconds - state.elapsedFestivalSeconds));
                runtime.Advance();
            }

            FestivalExecutionOccurrenceSaveData terminal = runtime.Occurrences
                .Single(value => value.occurrenceId == occurrenceId);
            Require(terminal.phase == FestivalExecutionPhase.Resolved
                && terminal.effectsApplied
                && terminal.resultGrade == FestivalResolutionGrade.Success
                && terminal.attendance.All(value =>
                    FestivalExecutionRules.IsValidAttendance(
                        value.attendedSeconds,
                        terminal.plannedDurationSeconds))
                && !terminal.inputOwnerActive
                && !terminal.materialReceiptPending
                && terminal.cleanupComplete,
                "The actual festival did not reach a clean successful terminal state.");
            RequirePresentationTimeline(
                occurrenceId,
                factCount,
                alertCount,
                terminal,
                newlyScheduled);
            Validate();
            return true;
        }

        private void RequirePresentationTimeline(
            string occurrenceId,
            int factCount,
            int alertCount,
            FestivalExecutionOccurrenceSaveData terminal,
            bool newlyScheduled)
        {
            FestivalExecutionProgressedFact[] facts = presentationFacts
                .Skip(factCount)
                .Where(value => string.Equals(
                    value.OccurrenceId,
                    occurrenceId,
                    StringComparison.Ordinal))
                .ToArray();
            EventAlertRequest[] alerts = presentationAlerts
                .Skip(alertCount)
                .Where(value => string.Equals(
                    value.SourceId,
                    occurrenceId,
                    StringComparison.Ordinal))
                .ToArray();
            if (newlyScheduled)
            {
                Require(facts.Length == 4
                    && facts[0].Change == FestivalExecutionChange.Scheduled
                    && facts[0].Phase == FestivalExecutionPhase.Preparing
                    && facts[1].Change == FestivalExecutionChange.Advanced
                    && facts[1].Phase == FestivalExecutionPhase.Preparing
                    && facts[2].Change == FestivalExecutionChange.Advanced
                    && facts[2].Phase == FestivalExecutionPhase.Running
                    && facts[3].Change == FestivalExecutionChange.Advanced
                    && facts[3].Phase == FestivalExecutionPhase.Resolved,
                    "Festival presentation facts did not preserve schedule, preparation, running and completion order.");
            }
            else
            {
                Require(facts.Length == 1
                    && facts[0].Change == FestivalExecutionChange.Advanced
                    && facts[0].Phase == FestivalExecutionPhase.Resolved,
                    "Retryable festival completion did not publish one terminal progress fact.");
            }

            FestivalExecutionProgressedFact last = facts[^1];
            Require(last.Phase == terminal.phase
                && string.Equals(last.OccurrenceId, terminal.occurrenceId, StringComparison.Ordinal)
                && string.Equals(last.LastStatus, terminal.lastStatus, StringComparison.Ordinal)
                && string.Equals(last.ResultSummary, terminal.resultSummary, StringComparison.Ordinal)
                && last.Attendance.Count == terminal.attendance.Count
                && !last.InputOwnerActive
                && !last.MaterialReceiptPending,
                "Terminal festival presentation fact retained stale execution state.");

            if (!presentationActive)
            {
                Require(alerts.Length == 0,
                    "Disposed festival presentation emitted an alert.");
                return;
            }

            Require(alerts.Length == facts.Length
                && alerts.Take(alerts.Length - 1).All(value => !value.IsResolved)
                && alerts.Count(value => value.IsResolved) == 1
                && alerts[^1].IsResolved
                && string.Equals(
                    alerts[^1].ResultSummary,
                    terminal.resultSummary,
                    StringComparison.Ordinal),
                "Festival presentation emitted a stale, missing or duplicate alert transition.");
        }

        internal bool ExerciseSecondCelebrationFault(
            MigratedProducerOutcomeFaultMode fault)
        {
            Require(Stage == 1, "Fault probe requires the restored first occurrence.");
            int rapportBefore = FactionRapportTotal();
            int attendanceBefore = TotalAttendanceRecords();
            int moodBefore = FestivalMoodFactorCount();
            int exactBefore = Exact.Count;
            probe.TargetKind = MigratedProducerOutcomeKind.FestivalCelebratedEvent;
            probe.FaultMode = fault;
            bool rejected = false;
            try
            {
                CelebrateSecondYear();
            }
            catch (InvalidOperationException)
            {
                rejected = true;
            }
            Require(rejected
                && Stage == 1
                && Exact.Count == exactBefore
                && FactionRapportTotal() == rapportBefore
                && TotalAttendanceRecords() == attendanceBefore
                && FestivalMoodFactorCount() == moodBefore
                && CandleQuantity() == 0,
                "Rejected festival outcome leaked social effects or duplicated material.");

            FestivalExecutionOccurrenceSaveData pending = runtime.Occurrences.Single(
                value => value.occurrenceYear == SecondYear);
            Require(pending.phase == FestivalExecutionPhase.Running
                && !pending.effectsApplied
                && pending.elapsedFestivalSeconds + 0.0001f
                    >= pending.plannedDurationSeconds,
                "Rejected festival resolution did not remain retryable at the exact terminal boundary.");
            probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            Require(CelebrateSecondYear()
                && Stage == 2
                && Exact.Count == exactBefore + 1,
                "Festival resolution retry did not publish exactly one durable outcome.");
            return true;
        }

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            Validate();
            IReadOnlyList<FestivalExecutionOccurrenceSaveData> occurrences =
                runtime.Occurrences;
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["festival.ownerId"] = OwnerId,
                ["festival.stage"] = Stage,
                ["festival.festivalId"] = FestivalId,
                ["festival.displayName"] = festival.displayName,
                ["festival.currentYear"] = calendar.Year,
                ["festival.currentAbsoluteDay"] = calendar.Day,
                ["festival.participantCount"] = ParticipantCount,
                ["festival.occurrenceCount"] = occurrences.Count,
                ["festival.resolvedOccurrenceCount"] = occurrences.Count(value =>
                    value.phase == FestivalExecutionPhase.Resolved),
                ["festival.successfulAttendanceCount"] = occurrences
                    .Where(value => value.resultGrade == FestivalResolutionGrade.Success)
                    .Sum(value => value.attendance.Count(attendance =>
                        FestivalExecutionRules.IsValidAttendance(
                            attendance.attendedSeconds,
                            value.plannedDurationSeconds))),
                ["festival.candleQuantity"] = CandleQuantity(),
                ["festival.activeInputOwnerCount"] = occurrences.Count(value =>
                    value.inputOwnerActive),
                ["festival.pendingMaterialReceiptCount"] = occurrences.Count(value =>
                    value.materialReceiptPending),
                ["festival.cleanupCompleteCount"] = occurrences.Count(value =>
                    value.cleanupComplete),
                ["festival.moodFactorCount"] = FestivalMoodFactorCount(),
                ["festival.attendanceRecordCount"] = TotalAttendanceRecords(),
                ["festival.griefRemainingMultiplier"] = StateNumber(
                    RememberedGriefMultiplier()),
                ["festival.factionRapportTotal"] = FactionRapportTotal(),
                ["festival.exactOutcomeCount"] = Exact.Count,
                ["festival.controlledMaterialPlacement"] = true,
                ["festival.naturalDeliveryTelemetry"] = false
            };
        }

        internal GameplayOutcomeReplayIdentity RequireExactOutcome(int year)
        {
            string occurrenceId = FestivalExecutionRules.OccurrenceId(FestivalId, year);
            GameplayOutcomeSnapshot exact = Exact.Single(value =>
                value.participants.Any(participant => string.Equals(
                    participant.entityId,
                    occurrenceId,
                    StringComparison.Ordinal)));
            var key = new GameplayResultKey(
                exact.producerId,
                new GameplayOperationId(exact.operationId),
                exact.commitRevision,
                exact.localResultIndex);
            Require(outcomes.Ledger.TryGetResultIdentity(
                    key,
                    out GameplayOutcomeReplayIdentity identity)
                && identity.State == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash,
                "Canonical festival outcome identity is missing for year " + year + ".");
            return identity;
        }

        internal JObject CaptureOutcomeEvidence(GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(outcomes.Ledger, identity);

        internal JObject CaptureOwnerCheckpoint()
        {
            Validate();
            return ProductionCausalOwnerCheckpoint.CaptureFamily(
                OwnerId,
                OwnerKind,
                RestoreKind,
                CaptureDomain(),
                outcomes.Ledger);
        }

        private JObject CaptureDomain() => new()
        {
            ["schema"] = Schema,
            ["schemaVersion"] = 1,
            ["festivalId"] = FestivalId,
            ["firstYear"] = FirstYear,
            ["secondYear"] = SecondYear,
            ["stage"] = Stage,
            ["calendarDay"] = calendar.Day,
            ["calendarHour"] = calendar.Hour,
            ["actors"] = new JArray(actors
                .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
                .Select(CaptureActor)),
            ["festivalExecutions"] = Dto(runtime.CaptureFestivalExecutions()),
            ["physicalItems"] = Dto(items.Capture()),
            ["psychosocial"] = Dto(psychosocial.Capture()),
            ["seasonal"] = Dto(campaign.CaptureSeasonal()),
            ["factions"] = Dto(campaign.CaptureFactions()),
            ["migratedOutcomeOwners"] = Dto(outcomes.Transaction.Capture())
        };

        private void RestoreOwnerCheckpoint(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint,
                OwnerId,
                OwnerKind,
                RestoreKind,
                outcomes.Ledger);
            RequireFields(
                state,
                "schema",
                "schemaVersion",
                "festivalId",
                "firstYear",
                "secondYear",
                "stage",
                "calendarDay",
                "calendarHour",
                "actors",
                "festivalExecutions",
                "physicalItems",
                "psychosocial",
                "seasonal",
                "factions",
                "migratedOutcomeOwners");
            Require(
                (string)state["schema"] == Schema
                && (int)state["schemaVersion"] == 1
                && (string)state["festivalId"] == FestivalId
                && (int)state["firstYear"] == FirstYear
                && (int)state["secondYear"] == SecondYear,
                "Festival owner schema or authored identity changed.");

            calendar.SetDateTime(
                (int)state["calendarDay"],
                (int)state["calendarHour"]);
            RestoreActors((JArray)state["actors"]);
            psychosocial.PublishRestore(psychosocial.PrepareRestore(
                Read<CharacterPsychosocialWorldSaveData>(state["psychosocial"])));
            SeasonalEventAggregateState stagedSeason = campaign.PrepareSeasonalRestore(
                Read<SeasonalEventWorldSaveData>(state["seasonal"]));
            try
            {
                FactionCampaignAggregateState stagedFactions =
                    campaign.PrepareFactionsRestore(
                        Read<FactionCampaignWorldSaveData>(state["factions"]));
                campaign.PublishSeasonalRestore(stagedSeason);
                campaign.PublishFactions(stagedFactions);
            }
            finally
            {
                stagedSeason.Discard();
            }
            outcomes.Transaction.Restore(outcomes.Transaction.PrepareRestore(
                Read<DungeonMigratedProducerOutcomeSaveData>(
                    state["migratedOutcomeOwners"])));

            DungeonPhysicalItemSaveData physical =
                Read<DungeonPhysicalItemSaveData>(state["physicalItems"]);
            FestivalExecutionWorldSaveData executions =
                Read<FestivalExecutionWorldSaveData>(state["festivalExecutions"]);
            RestoreWorldCandidateIndex candidates = new();
            candidates.SetFacilityCandidate(world.Grid, new[] { venueFacility });
            IDungeonSaveRestoreStage physicalStage = items.StageTransactionalRestore(
                physical,
                candidates);
            var report = new DungeonGameRestoreReport();
            IDungeonSaveRestoreStage festivalStage = festivalSave.StageRestore(
                JsonUtility.ToJson(executions),
                FestivalExecutionWorldSaveData.CurrentVersion,
                report);
            Require(report.Success,
                "Festival section staging failed: " + string.Join("; ", report.Errors));

            IDungeonRestoreTransactionParticipant[] ownerParticipants =
                inputOwners.RestoreParticipants.ToArray();
            exactRouteRestore.BeginRestoreCandidate();
            items.BeginRestoreCandidate();
            foreach (IDungeonRestoreTransactionParticipant participant in ownerParticipants)
                participant.BeginRestoreCandidate();
            runtime.BeginRestoreCandidate();
            bool itemsPublished = false;
            bool exactRoutePublished = false;
            int ownersPublished = 0;
            bool festivalPublished = false;
            try
            {
                physicalStage.Commit(report);
                festivalStage.Commit(report);
                foreach (IDungeonRestoreTransactionParticipant participant in ownerParticipants)
                {
                    participant.PublishRestoreCandidate();
                    ownersPublished++;
                }
                exactRouteRestore.PublishRestoreCandidate();
                exactRoutePublished = true;
                items.PublishRestoreCandidate();
                itemsPublished = true;
                runtime.PublishRestoreCandidate();
                festivalPublished = true;

                foreach (IDungeonRestoreTransactionParticipant participant in ownerParticipants)
                    participant.CompleteRestoreCandidate();
                exactRouteRestore.CompleteRestoreCandidate();
                items.CompleteRestoreCandidate();
                runtime.CompleteRestoreCandidate();
            }
            catch
            {
                if (festivalPublished)
                    runtime.RollbackPublishedRestoreCandidate();
                else
                    runtime.DiscardRestoreCandidate();
                if (itemsPublished)
                    items.RollbackPublishedRestoreCandidate();
                else
                    items.DiscardRestoreCandidate();
                if (exactRoutePublished)
                    exactRouteRestore.RollbackPublishedRestoreCandidate();
                else
                    exactRouteRestore.DiscardRestoreCandidate();
                for (int index = ownerParticipants.Length - 1; index >= 0; index--)
                {
                    if (index < ownersPublished)
                        ownerParticipants[index].RollbackPublishedRestoreCandidate();
                    else
                        ownerParticipants[index].DiscardRestoreCandidate();
                }
                throw;
            }
            finally
            {
                candidates.ClearFacilityCandidate();
            }

            Validate();
            Require((int)state["stage"] == Stage,
                "Restored festival stage differs from its checkpoint.");
            JObject actual = CaptureDomain();
            string mismatch = state.Properties()
                .FirstOrDefault(property => !JToken.DeepEquals(
                    property.Value,
                    actual[property.Name]))?.Name;
            Require(mismatch == null,
                "Canonical festival owner did not round-trip: " + mismatch);
        }

        private void RestoreActors(JArray rows)
        {
            Require(rows != null && rows.Count == actors.Count,
                "Festival actor checkpoint count changed.");
            foreach (JToken row in rows)
            {
                DungeonCharacterSaveData dto = Read<DungeonCharacterSaveData>(row);
                CharacterActor actor = actors.Single(value =>
                    value.Identity.PersistentId == dto.persistentId);
                Require(dto.dataId == actor.Identity.Data.id
                    && dto.displayName == actor.Identity.DisplayName
                    && dto.characterType == actor.Identity.CharacterType
                    && dto.role == actor.Identity.Data.role,
                    "Festival actor authored identity changed.");
                var report = new DungeonGameRestoreReport();
                Invoke(
                    typeof(CharacterWorldRestorePayloadValidator),
                    "ValidateActor",
                    null,
                    world.Grid,
                    dto,
                    new Dictionary<int, CharacterSO>
                        { [dto.dataId] = actor.Identity.Data },
                    report,
                    false);
                Require(report.Success,
                    "Festival actor restore failed: " + string.Join("; ", report.Errors));
                Invoke(
                    typeof(CharacterWorldSaveService),
                    "ApplyActorState",
                    actorSave,
                    world.Grid,
                    actor,
                    dto);
            }
            foreach (CharacterActor actor in actors)
                actor.PublishDetachedRestore();
        }

        private void Validate()
        {
            if (runtime == null)
                return;
            int stage = Stage;
            Require(stage is >= 0 and <= 2,
                "Festival stage is outside the two authored occurrences.");
            Require(Exact.Count == stage
                && Exact.All(value => string.Equals(
                    value.outcomeTypeId,
                    Type(MigratedProducerOutcomeKind.FestivalCelebratedEvent),
                    StringComparison.Ordinal)),
                "Festival exact ledger count or type diverged.");

            FestivalExecutionOccurrenceSaveData[] resolved = runtime.Occurrences
                .Where(value => value.effectsApplied)
                .OrderBy(value => value.occurrenceYear)
                .ToArray();
            Require(resolved.Length == stage,
                "Festival execution and exact outcome counts diverged.");
            for (int index = 0; index < resolved.Length; index++)
            {
                FestivalExecutionOccurrenceSaveData occurrence = resolved[index];
                int expectedYear = FirstYear + index;
                Require(occurrence.festivalId == FestivalId
                    && occurrence.occurrenceYear == expectedYear
                    && occurrence.phase == FestivalExecutionPhase.Resolved
                    && occurrence.resultGrade == FestivalResolutionGrade.Success
                    && occurrence.attendance.Count == ParticipantCount
                    && occurrence.attendance.All(value =>
                        FestivalExecutionRules.IsValidAttendance(
                            value.attendedSeconds,
                            occurrence.plannedDurationSeconds))
                    && occurrence.cleanupComplete
                    && !occurrence.inputOwnerActive
                    && !occurrence.materialReceiptPending,
                    "A resolved authored festival occurrence is incomplete.");
            }

            foreach (CharacterActor actor in actors)
            {
                CharacterId id = CharacterPersistentIdentity.Require(actor);
                CharacterGriefAggregate grief = psychosocial.Require(id);
                if (stage > 0)
                {
                    Require(grief.HasAttendedFestival(
                            FestivalId,
                            FirstYear + stage - 1),
                        "Latest festival attendance persistence is missing for " + id + ".");
                }
                for (int year = FirstYear; year < FirstYear + stage; year++)
                {
                    CharacterMoodFactorSnapshot mood = actor.Stats.GetMoodSnapshot()
                        .Factors.SingleOrDefault(value => string.Equals(
                            value.Id,
                            MoodId(year),
                            StringComparison.Ordinal));
                    Require(mood != null
                        && Mathf.Approximately(mood.Value, 6f)
                        && mood.RemainingSeconds > 0f,
                        "Authored +6 festival mood is missing for " + id + ".");
                }
            }

            foreach (FactionCampaignStateSaveData faction in
                     campaign.CaptureFactions().factions)
            {
                Require(initialFactionRapport.TryGetValue(
                        faction.factionId,
                        out int initial)
                    && faction.rapport == initial + 3 * stage,
                    "Authored faction rapport delta diverged for " + faction.factionId + ".");
            }
            Require(Mathf.Abs(
                    RememberedGriefMultiplier() - Mathf.Pow(0.75f, stage))
                <= 0.0001f,
                "Authored 25% grief conversion did not accumulate exactly.");

            if (runtime.Occurrences.All(value =>
                    value.phase != FestivalExecutionPhase.Preparing))
            {
                Require(CandleQuantity() == 0,
                    "Terminal/running festival retained an unconsumed candle stack.");
            }
        }

        private void SeedGrief()
        {
            int deathDay = FestivalExecutionRules.FestivalAbsoluteDay(
                festival,
                FirstYear) - 1;
            CharacterLifeDeathRecord death = new(
                new CharacterId(RememberedCharacterId),
                CharacterDeathCauseCode.Combat,
                deathDay,
                new CoreGridCell(
                    venue.EventCells[0].x,
                    venue.EventCells[0].y),
                actors.Select(CharacterPersistentIdentity.Require).ToArray());
            psychosocial.RecordDeath(
                CharacterPersistentIdentity.Require(actors[0]),
                death,
                GriefRelationshipKind.PartnerOrChild);
        }

        private void RequireAuthoredDefinition()
        {
            Require(festival.StableId == FestivalId
                && festival.minimumParticipants == ParticipantCount
                && festival.requiredItems.Count == 1
                && festival.requiredItems[0].itemDefinitionId == CandleItemId
                && festival.requiredItems[0].amount == CandleCount
                && festival.venueRequirements.minimumEventCells == ParticipantCount
                && festival.venueRequirements.anchor.exactBuildingDefinitionIds
                    .SequenceEqual(new[] { VenueDefinitionId })
                && Mathf.Approximately(festival.successOutcome.moodDelta, 6f)
                && festival.successOutcome.moodDurationDays == 10
                && festival.successOutcome.factionRapportDelta == 3
                && Mathf.Approximately(
                    festival.successOutcome.griefConversionPercent,
                    25f),
                "The authored long-night memorial contract changed.");
        }

        private void CreateActors(bool detachedRestore)
        {
            CharacterSO definition = CharacterAiEditorTestDependencies
                .RequireAuthoredCharacterDefinition("Slime", CharacterRole.Regular);
            for (int index = 0; index < ParticipantCount; index++)
            {
                string id = "character:causal-festival:" + (index + 1)
                    .ToString("D2", CultureInfo.InvariantCulture);
                Vector2Int position = EventCell(index);
                var gameObject = new GameObject("Causal festival resident " + id);
                gameObject.AddComponent<SpriteRenderer>();
                gameObject.AddComponent<AbilityMove>();
                gameObject.AddComponent<AbilityShopping>();
                gameObject.AddComponent<AbilityWork>();
                gameObject.AddComponent<CharacterCarryInventory>();
                AIBrain brain = gameObject.AddComponent<AIBrain>();
                brain.availableActions = AiDebugScenarioActionFactory
                    .CreateCustomerActions();
                CharacterActor actor = gameObject.AddComponent<CharacterActor>();
                actors.Add(actor);
                if (detachedRestore)
                {
                    actor.EnsureRuntimeState();
                    actor.PrepareForDetachedRestore();
                }
                CharacterAiEditorTestDependencies.Inject(
                    gameObject,
                    scheduling: null,
                    gameClock: clock,
                    pathSearchBroker: null,
                    frameWorkBudget: null);
                Invoke(typeof(CharacterActor), "Awake", actor);
                actor.Identity.SetPersistentId(id);
                gameObject.transform.position = world.Grid.GetWorldPos(position);
                actor.Initialization(definition);
                Require(actor.Identity.PersistentId == id,
                    "Festival actor initialization changed its persistent ID.");
                actor.SetLifecycleState(CharacterLifecycleState.Active);
                if (!detachedRestore)
                {
                    CharacterAiEditorTestDependencies.WorldRegistry.RegisterCharacter(actor);
                    CharacterAiEditorTestDependencies.WorldRegistry
                        .RegisterCharacterLifetime(actor);
                }
            }
        }

        private BuildableObject CreateVenueFacility(BuildingSO definition)
        {
            Require(definition != null && definition.id == 8887,
                "Authored RF87 building identity changed.");
            var gameObject = new GameObject("Causal authored RF87 memorial venue");
            BuildableObject facility = gameObject.AddComponent<BuildableObject>();
            facility.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
            CharacterAiEditorTestDependencies.Inject(facility);
            facility.Initialization(definition, new Vector2Int(20, 0));
            facility.RestorePersistentIdentity(new BuildingInstanceId(VenueInstanceId));
            return facility;
        }

        private SocietyVenueCandidateSnapshot CreateVenueSnapshot() => new()
        {
            Facility = venueFacility,
            FacilityInstanceId = VenueInstanceId,
            DisplayName = venueDefinition.objectName,
            AnchorCenter = venueFacility.centerPos,
            AccessCell = EventCell(0),
            EventCells = Enumerable.Range(0, ParticipantCount)
                .Select(EventCell)
                .ToArray()
        };

        private static Vector2Int EventCell(int index) => new(index + 2, 0);

        private void PositionActorsAtAssignedCells(
            FestivalExecutionOccurrenceSaveData state)
        {
            foreach (FestivalAttendanceProgressSaveData attendance in state.attendance)
            {
                CharacterActor actor = actors.Single(value =>
                    value.Identity.PersistentId == attendance.characterId);
                actor.transform.position = world.Grid.GetWorldPos(attendance.AssignedCell);
            }
        }

        private int CandleQuantity() => items.GetAllStacks()
            .Where(value => value != null && value.ItemId == CandleItemId)
            .Sum(value => value.TotalQuantity);

        private int FestivalMoodFactorCount() => actors.Sum(actor =>
            actor.Stats.GetMoodSnapshot().Factors.Count(value =>
                value.Id.StartsWith("festival:" + FestivalId + ":",
                    StringComparison.Ordinal)));

        private int TotalAttendanceRecords() => psychosocial.Capture().characters
            .Sum(value => value.festivalAttendance?.Count ?? 0);

        private int FactionRapportTotal() => campaign.CaptureFactions().factions
            .Sum(value => value.rapport);

        private float RememberedGriefMultiplier()
        {
            CharacterPsychosocialRecordSaveData subject = psychosocial.Capture()
                .characters.Single(value => value.characterId
                    == actors[0].Identity.PersistentId);
            GriefIncidentSaveData incident = subject.grief.Single(value =>
                value.deceasedCharacterId == RememberedCharacterId);
            return incident.remainingMultiplier;
        }

        private JObject CaptureActor(CharacterActor actor) => Dto(
            (DungeonCharacterSaveData)Invoke(
                typeof(CharacterWorldSaveService),
                "CaptureActor",
                actorSave,
                world.Grid,
                actor));

        public void Dispose()
        {
            if (disposed)
                return;
            disposed = true;
            presentation?.Dispose();
            presentationAlertSubscription?.Dispose();
            presentationFactSubscription?.Dispose();
            socialFacts?.Dispose();
            items?.Dispose();
            foreach (CharacterActor actor in actors.ToArray())
            {
                if (actor == null)
                    continue;
                CharacterAiEditorTestDependencies.WorldRegistry
                    .UnregisterCharacter(actor);
                CharacterAiEditorTestDependencies.WorldRegistry
                    .UnregisterCharacterLifetime(actor);
                Object.DestroyImmediate(actor.gameObject);
            }
            actors.Clear();
            if (venueFacility != null)
                Object.DestroyImmediate(venueFacility.gameObject);
            if (venueDefinition != null)
                Object.DestroyImmediate(venueDefinition);
            world?.Dispose();
        }
    }

    private sealed class FixedCharacterWorld : ICharacterWorldQuery
    {
        private readonly IReadOnlyList<CharacterActor> actors;
        internal FixedCharacterWorld(IReadOnlyList<CharacterActor> actors) =>
            this.actors = actors ?? throw new ArgumentNullException(nameof(actors));
        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters => actors;
    }

    private sealed class FixedVenueQuery : ISocietyVenueQuery
    {
        private readonly SocietyVenueCandidateSnapshot venue;
        internal FixedVenueQuery(SocietyVenueCandidateSnapshot venue) =>
            this.venue = venue ?? throw new ArgumentNullException(nameof(venue));

        public bool TryFindDeliveryVenues(
            FacilityVenueRequirements requirements,
            Vector2Int supplyOrigin,
            string excludedClaimOwnerId,
            out IReadOnlyList<SocietyVenueCandidateSnapshot> candidates,
            out string failureReason)
        {
            candidates = Array.Empty<SocietyVenueCandidateSnapshot>();
            failureReason = "festival-only-controlled-venue";
            return false;
        }

        public bool TryFindFestivalVenues(
            FacilityVenueRequirements requirements,
            IReadOnlyCollection<CharacterId> participantIds,
            int requiredEventCapacity,
            out IReadOnlyList<SocietyVenueCandidateSnapshot> candidates,
            out string failureReason)
        {
            bool accepted = Accept(
                requirements,
                participantIds,
                requiredEventCapacity,
                venue.FacilityInstanceId);
            candidates = accepted
                ? new[] { venue }
                : Array.Empty<SocietyVenueCandidateSnapshot>();
            failureReason = accepted
                ? string.Empty
                : "authored RF87 venue requirements were not met";
            return accepted;
        }

        public bool TryValidateDeliveryVenue(
            FacilityVenueRequirements requirements,
            Vector2Int supplyOrigin,
            string claimOwnerId,
            string facilityInstanceId,
            Vector2Int anchorCenter,
            out SocietyVenueCandidateSnapshot candidate,
            out string failureReason)
        {
            candidate = null;
            failureReason = "festival-only-controlled-venue";
            return false;
        }

        public bool TryValidateFestivalVenue(
            FacilityVenueRequirements requirements,
            IReadOnlyCollection<CharacterId> participantIds,
            int requiredEventCapacity,
            string facilityInstanceId,
            out SocietyVenueCandidateSnapshot candidate,
            out string failureReason)
        {
            bool accepted = Accept(
                requirements,
                participantIds,
                requiredEventCapacity,
                facilityInstanceId);
            candidate = accepted ? venue : null;
            failureReason = accepted
                ? string.Empty
                : "authored RF87 venue validation failed";
            return accepted;
        }

        private bool Accept(
            FacilityVenueRequirements requirements,
            IReadOnlyCollection<CharacterId> participantIds,
            int requiredEventCapacity,
            string facilityInstanceId) =>
            requirements != null
            && requirements.minimumEventCells == ParticipantCount
            && requirements.anchor.exactBuildingDefinitionIds
                .SequenceEqual(new[] { VenueDefinitionId })
            && participantIds != null
            && participantIds.Count == ParticipantCount
            && participantIds.All(value => value.IsValid)
            && requiredEventCapacity == ParticipantCount
            && venue.EventCells.Count >= requiredEventCapacity
            && string.Equals(
                facilityInstanceId,
                VenueInstanceId,
                StringComparison.Ordinal);
    }

    private sealed class EmptyNarrativeQuery : ICharacterNarrativeQuery
    {
        internal static readonly EmptyNarrativeQuery Instance = new();
        public int Version => 0;
        public IReadOnlyCollection<CharacterNarrativeSnapshot> All =>
            Array.Empty<CharacterNarrativeSnapshot>();
        public bool TryGet(
            CharacterId characterId,
            out CharacterNarrativeSnapshot snapshot)
        { snapshot = default; return false; }
        public bool CanPerformPractice(
            CharacterId characterId,
            string practiceId,
            int absoluteDay,
            out int nextAllowedAbsoluteDay)
        { nextAllowedAbsoluteDay = 0; return false; }
        public bool TryPreviewAmbitionProgress(
            CharacterId characterId,
            int amount,
            out AmbitionProgressPreview preview)
        { preview = default; return false; }
    }

    private sealed class ResidentStandingQuery : ICharacterSettlementStandingQuery
    {
        private readonly int residents;
        internal ResidentStandingQuery(int residents) => this.residents = residents;
        public CharacterSettlementStanding GetStanding(CharacterActor actor) =>
            actor == null ? CharacterSettlementStanding.Unknown
                : CharacterSettlementStanding.Resident;
        public CharacterSettlementStanding GetStanding(string persistentCharacterId) =>
            string.IsNullOrWhiteSpace(persistentCharacterId)
                ? CharacterSettlementStanding.Unknown
                : CharacterSettlementStanding.Resident;
        public CharacterSettlementPopulationSnapshot GetSettlementPopulation() =>
            new(residents, 0);
        public bool IsFormalResident(CharacterActor actor) => actor != null;
        public bool IsMinion(CharacterActor actor) => false;
        public bool CanJoinExpedition(CharacterActor actor, out string failureReason)
        { failureReason = string.Empty; return actor != null; }
        public bool CanParticipateInMentoring(
            CharacterActor actor,
            out string failureReason)
        { failureReason = string.Empty; return actor != null; }
        public bool IsWorkAllowed(
            CharacterActor actor,
            WorkTypeId workTypeId,
            out string failureReason)
        { failureReason = string.Empty; return actor != null; }
        public float GetApprovedWorkExperienceMultiplier(CharacterActor actor) => 1f;
    }

    private sealed class NoCombatQuery : ICharacterCombatStanceQuery
    {
        internal static readonly NoCombatQuery Instance = new();
        public bool IsInCombatStance(CharacterActor actor) => false;
    }

    private sealed class SafeHazardQuery : IWorldHazardZoneQuery
    {
        internal static readonly SafeHazardQuery Instance = new();
        public int Version => 0;
        public WorldHazardSnapshot GetHazard(
            CharacterId characterId,
            Vector2Int position) => new(
                position,
                WorldHazardLevel.Safe,
                WorldHazardFlags.None);
    }

    private sealed class FixedGridProvider : IGridSystemProvider
    {
        private readonly Grid grid;
        internal FixedGridProvider(Grid grid) =>
            this.grid = grid ?? throw new ArgumentNullException(nameof(grid));
        public Grid Grid => grid;
        public GridSystemManager Manager => throw Unsupported(nameof(Manager));
        public bool TryGetGrid(out Grid value) { value = grid; return true; }
        public bool TryGetManager(out GridSystemManager value)
        { value = null; return false; }
    }

    private sealed class FixedWorkAmount : IWorkAmountCalculator
    {
        internal static readonly FixedWorkAmount Instance = new();
        public float CalculateWorkPerSecond(
            CharacterActor actor,
            BuildableObject target,
            WorkTypeId workTypeId,
            float environmentDurationMultiplier) => 1f;
    }

    private sealed class MutableClock : IGameClock
    {
        public float DeltaTime { get; private set; }
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused => false;
        internal void SetDelta(float delta)
        {
            DeltaTime = Mathf.Max(0f, delta);
            Time += DeltaTime;
            FrameCount++;
        }
    }

    private sealed class MutableCalendar : IGameCalendar
    {
        public int Day { get; private set; } = 1;
        public int Hour { get; private set; }
        private CalendarDateTime Projection => GameCalendarRules.Project(Day, Hour);
        public int Year => Projection.Year;
        public int DayOfYear => Projection.DayOfYear;
        public Season Season => Projection.Season;
        public int DayOfSeason => Projection.DayOfSeason;
        public long AbsoluteHour => ((long)Day - 1L)
            * GameCalendarRules.HoursPerDay + Hour;
        public float ElapsedSeconds => ((long)Day - 1L)
            * GameCalendarRules.SecondsPerDay
            + Hour * GameCalendarRules.SecondsPerGameHour;
        public TimeOfDay TimeOfDay => global::TimeOfDay.Morning;
        public bool IsRunning => true;
        public CalendarDateTime Current => Projection;
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(Day, Hour, utcOffsetHours);
        public void Start() { }
        public void SetDateTime(int day, int hour)
        {
            Day = Math.Max(1, day);
            Hour = Math.Clamp(hour, 0, GameCalendarRules.HoursPerDay - 1);
        }
    }

    private sealed class FixtureSet : IDisposable
    {
        private readonly IProductionCausalActionFixture[] fixtures;
        internal FixtureSet(IReadOnlyList<IProductionCausalActionFixture> fixtures) =>
            this.fixtures = fixtures.ToArray();
        internal IProductionCausalActionFixture Get(string id) =>
            fixtures.Single(value => string.Equals(
                value.ActionId,
                id,
                StringComparison.Ordinal));
        public void Dispose()
        {
            foreach (IProductionCausalActionFixture fixture in fixtures)
                fixture.Dispose();
        }
    }

    private static string MoodId(int year) =>
        "festival:" + FestivalId + ":" + year.ToString(CultureInfo.InvariantCulture);

    private static string Type(MigratedProducerOutcomeKind kind) =>
        MigratedProducerOutcomeCatalog.Get(kind).OutcomeTypeId.Value;

    private static double StateNumber(float value) => double.Parse(
        value.ToString("G9", CultureInfo.InvariantCulture),
        CultureInfo.InvariantCulture);

    private static JObject Dto(object value) => JObject.Parse(
        value is DungeonCharacterSaveData
            ? DungeonCharacterWorldSaveCodec.SerializeFields(value)
            : JsonUtility.ToJson(value));

    private static T Read<T>(JToken value) where T : class
    {
        T result = JsonConvert.DeserializeObject<T>(
            value?.ToString(Formatting.None)
                ?? throw new InvalidOperationException(
                    "Festival checkpoint payload is missing."),
            new JsonSerializerSettings
            {
                MissingMemberHandling = MissingMemberHandling.Error
            });
        Require(result != null
            && JToken.DeepEquals(value, Dto(result)),
            "Incomplete or changed canonical DTO: " + typeof(T).Name);
        return result;
    }

    private static void RequireFields(JObject value, params string[] names)
    {
        Require(value != null
            && value.Properties().Select(property => property.Name)
                .OrderBy(name => name, StringComparer.Ordinal)
                .SequenceEqual(names.OrderBy(name => name, StringComparer.Ordinal)),
            "Festival owner checkpoint has unknown or missing fields.");
    }

    private static object Invoke(
        Type type,
        string methodName,
        object target,
        params object[] arguments)
    {
        MethodInfo method = type.GetMethod(
            methodName,
            BindingFlags.Instance | BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException(
                "Production method is missing: " + type.Name + "." + methodName);
        try
        {
            return method.Invoke(target, arguments);
        }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        {
            ExceptionDispatchInfo.Capture(exception.InnerException).Throw();
            throw;
        }
    }

    private static Exception Unsupported(string method) =>
        new InvalidOperationException(
            "Festival causal owner attempted an uncomposed operation: " + method);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
