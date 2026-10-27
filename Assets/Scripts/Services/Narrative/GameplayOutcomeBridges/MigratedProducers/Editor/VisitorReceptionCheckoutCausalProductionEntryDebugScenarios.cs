#if UNITY_EDITOR
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.Serialization;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using Object = UnityEngine.Object;

// Controlled entered visitor + assigned worker, NOT spawning/travel/full-world telemetry.
// Reception -> canonical per-owner save -> real checkout. Checkout then commits a
// separate facility visit; these results are sequential, never atomic siblings.
public static class VisitorReceptionCheckoutCausalProductionEntryDebugScenarios
{
    internal const string OwnerKind = "owner-kind:visitor-reception-checkout";
    internal const string RestoreKind = "owner-family:visitor-reception-checkout";
    internal const string ReceptionActionId = "action:visitor-reception";
    internal const string CheckoutActionId = "action:visitor-checkout-crime";

    // Nine significant digits round-trip every binary32 value without widening
    // a float into a 17-digit double lexeme that Mono/Python may spell differently.
    // Full authoritative DTOs remain unchanged; only observed scalar JSON uses this.
    internal static double StateNumber(float value) => double.Parse(
        value.ToString("G9", System.Globalization.CultureInfo.InvariantCulture),
        System.Globalization.CultureInfo.InvariantCulture);
    private const string Schema = "dungeonstory-visitor-reception-checkout-state@1";
    private const string FocusedOwner = "visitor-reception:focused";
    private const string IncidentId = "incident:Informant:1";
    private const string ZoneId = "exterior:ReceptionPoint:30:0";
    private const int Day = 7;

    internal static IReadOnlyList<IProductionCausalActionFixture> CreateFixtures(
        string causalRunId, string ownerId = null, JObject ownerCheckpoint = null)
    {
        CausalGameplaySyntax.RequireId(causalRunId, nameof(causalRunId));
        bool persistent = !string.IsNullOrWhiteSpace(ownerId);
        Require(persistent || ownerCheckpoint == null, "Owner checkpoint requires a persistent binding.");
        var context = new Context(persistent ? ownerId : "visitor-checkout:" + causalRunId, ownerCheckpoint);
        var lifetime = new SharedProductionCausalFixtureLifetime(2, context.Dispose);
        return new IProductionCausalActionFixture[]
        {
            new DelegateProductionCausalActionFixture(
                ReceptionActionId, "external", MigratedProducerOutcomeKind.ExteriorVisitorReceptionAppliedEvent,
                context.CaptureState, context.Receive,
                () => context.RequireExact(MigratedProducerOutcomeKind.ExteriorVisitorReceptionAppliedEvent),
                context.CaptureOutcomeEvidence, lifetime.Release,
                persistent ? ownerId : null, persistent ? OwnerKind : null,
                persistent ? context.Checkpoint : null,
                persistent ? RestoreKind : null, persistent ? context : null),
            new DelegateProductionCausalActionFixture(
                CheckoutActionId, "service", MigratedProducerOutcomeKind.FacilityCrimeEvent,
                context.CaptureState, context.Checkout,
                () => context.RequireExact(MigratedProducerOutcomeKind.FacilityCrimeEvent),
                context.CaptureOutcomeEvidence, lifetime.Release,
                persistent ? ownerId : null, persistent ? OwnerKind : null,
                persistent ? context.Checkpoint : null,
                persistent ? RestoreKind : null, persistent ? context : null,
                new[] { MigratedProducerOutcomeKind.FacilityVisitEvent },
                () => new[] { context.RequireExact(MigratedProducerOutcomeKind.FacilityVisitEvent) },
                new[] { "post-commit-continuation" })
        };
    }

    public static void RunBatchFromEnvironment()
    {
        string output = RequiredEnvironment("DUNGEONSTORY_VISITOR_CHECKOUT_OUTPUT");
        Directory.CreateDirectory(output);
        JObject initial, first, terminal;
        JArray evidence;
        using (var context = new Context(FocusedOwner))
        {
            initial = context.Checkpoint();
            Require(context.Receive(), "Production reception did not commit.");
            first = context.Checkpoint();
            Require(!context.Receive() && JToken.DeepEquals(first, context.Checkpoint()),
                "Reception replay mutated canonical state.");
        }
        WriteNew(Path.Combine(output, "initial-checkpoint.json"), initial);
        WriteNew(Path.Combine(output, "first-checkpoint.json"), first);
        using (var context = new Context(FocusedOwner, Copy(first)))
        {
            ProductionCausalOwnerCheckpoint.RequireRestoredEquivalent(first, context.Checkpoint());
            Require(context.Checkout(), "Live checkout did not commit crime and visit.");
            terminal = context.Checkpoint();
            evidence = context.OrderedEvidence();
        }
        using (var context = new Context(FocusedOwner, Copy(terminal)))
        {
            JObject restoredTerminal = context.Checkpoint();
            ProductionCausalOwnerCheckpoint.RequireRestoredEquivalent(terminal, restoredTerminal);
            Require(!context.Receive() && !context.Checkout()
                && JToken.DeepEquals(restoredTerminal, context.Checkpoint()), "Terminal replay was not inert.");
        }
        var rejections = new JArray();
        WriteNew(Path.Combine(output, "successful-path.json"), new JObject
        {
            ["status"] = "PASS_SUCCESS_PATH_ONLY", ["terminal"] = terminal,
            ["orderedOutcomes"] = evidence, ["failureTestsPassed"] = false,
            ["humanApprovalClaimed"] = false, ["trainingEligible"] = false
        });
        foreach (var fault in new[] { MigratedProducerOutcomeFaultMode.RejectReservation,
                                     MigratedProducerOutcomeFaultMode.RejectCommit })
        {
            using var context = new Context(FocusedOwner, Copy(initial));
            JObject before = context.Domain();
            context.Probe.TargetKind = MigratedProducerOutcomeKind.ExteriorVisitorReceptionAppliedEvent;
            context.Probe.FaultMode = fault;
            Require(!context.Receive(), "Rejected reception was accepted.");
            context.Probe.FaultMode = MigratedProducerOutcomeFaultMode.None;
            JObject after = context.Domain();
            WriteNew(Path.Combine(output, "rejection-" + fault + "-before.json"), before);
            WriteNew(Path.Combine(output, "rejection-" + fault + "-after.json"), after);
            string mismatch = before.Properties().FirstOrDefault(property =>
                !JToken.DeepEquals(property.Value, after[property.Name]))?.Name;
            Require(mismatch == null && context.ExactCount == 0,
                "Rejected reception changed actor/exterior/shop: " + fault + "/" + mismatch
                + "; exactCount=" + context.ExactCount
                + "; expected=" + (mismatch == null ? "" : before[mismatch].ToString(Formatting.None))
                + "; actual=" + (mismatch == null ? "" : after[mismatch].ToString(Formatting.None)));
            rejections.Add(new JObject { ["fault"] = fault.ToString(), ["domainUnchanged"] = true });
        }
        using (var ordinary = new Context(FocusedOwner, Copy(initial)))
            ordinary.VerifyImmediateMoodPublication();
        var observerFaults = new JArray();
        foreach (string channel in new[] { "progression", "invalidated", "stats", "mood" })
        {
            using var context = new Context(FocusedOwner, Copy(initial));
            observerFaults.Add(context.VerifyCommittedObserverFailure(channel));
        }
        var mutations = new Dictionary<string, Action<JObject>>
        {
            ["visitor-id"] = value => value["domainState"]["visitor"]["persistentId"] = "character:world:wrong",
            ["actor-definition"] = value => value["domainState"]["visitor"]["dataId"] = -1,
            ["actor-unknown-field"] = value => value["domainState"]["visitor"]["invented"] = true,
            ["reception-owner"] = value => value["domainState"]["exterior"]["incidentStates"][0]["actorIds"][0] = "character:world:wrong",
            ["reception-flag"] = value => value["domainState"]["exterior"]["incidentStates"][0]["receptionApplied"] = false,
            ["reception-factor"] = value => value["domainState"]["visitor"]["moodFactors"] = new JArray(),
            ["exact-lot-operation"] = value => value["domainState"]["shopStock"]["lots"][0]["sourceOperationId"] = "wrong:operation",
            ["visit-count"] = value => value["domainState"]["facility"]["completedUses"] = 1
        };
        var tamper = new JArray();
        foreach (var mutation in mutations)
        {
            JObject changed = Copy(first);
            mutation.Value(changed);
            bool rejected = false;
            try { using var ignored = new Context(FocusedOwner, changed); }
            catch (InvalidOperationException) { rejected = true; }
            Require(rejected, "Changed checkpoint accepted: " + mutation.Key);
            tamper.Add(mutation.Key);
        }
        WriteNew(Path.Combine(output, "direct-report.json"), new JObject
        {
            ["status"] = "PASS", ["initial"] = initial, ["first"] = first, ["terminal"] = terminal,
            ["orderedOutcomes"] = evidence, ["receptionRejections"] = rejections, ["tamperCases"] = tamper,
            ["receptionPublicationsAfterDurableCommitOnly"] = true,
            ["ordinaryMoodImmediatePublicationPreserved"] = true,
            ["committedObserverFailureProbes"] = observerFaults,
            ["controlledSimulation"] = true, ["naturalPlayTelemetry"] = false,
            ["wholeWorldSave"] = false, ["humanApprovalClaimed"] = false,
            ["trainingEligible"] = false, ["releaseGate"] = "REJECT_BEFORE_DPO"
        });
        Debug.Log("VISITOR_RECEPTION_CHECKOUT_DIRECT=PASS; actions=2; outcomes=3; restores=2");
    }

    public static void RunFreshFromEnvironment()
    {
        string output = RequiredEnvironment("DUNGEONSTORY_VISITOR_CHECKOUT_OUTPUT");
        string input = RequiredEnvironment("DUNGEONSTORY_VISITOR_CHECKOUT_INPUT");
        Directory.CreateDirectory(output);
        JObject first = JObject.Parse(File.ReadAllText(input));
        using var context = new Context(FocusedOwner, first);
        Require(context.Stage == 1, "Separate-process first stage did not round-trip.");
        ProductionCausalOwnerCheckpoint.RequireRestoredEquivalent(first, context.Checkpoint());
        Require(context.Checkout(), "Separate-process checkout failed.");
        WriteNew(Path.Combine(output, "fresh-report.json"), new JObject
        {
            ["status"] = "PASS", ["sourceCheckpoint"] = Path.GetFullPath(input),
            ["terminal"] = context.Checkpoint(), ["orderedOutcomes"] = context.OrderedEvidence(),
            ["humanApprovalClaimed"] = false, ["trainingEligible"] = false
        });
        Debug.Log("VISITOR_RECEPTION_CHECKOUT_FRESH=PASS; outcomes=3");
    }

    internal sealed class Context : IDisposable
    {
        private readonly string ownerId;
        private readonly CustomerAiDebugScenarios.CustomerAiScenarioWorld world;
        private readonly CharacterWorldSaveService actorSave;
        private readonly Shop shop;
        private readonly CharacterActor visitor;
        private readonly CharacterActor worker;
        private readonly AbilityShopping shopping;
        private readonly ExteriorActivityRuntime exterior;
        private readonly ShopStockStateModule stockSave;
        private readonly FacilityRuntimeStateModule facilitySave;
        private readonly MigratedProducerOutcomeEditorFixture outcomes;
        private readonly DomainFactDispatcher domainFacts = new();
        private readonly RetailPurchaseFactEditorFixture purchaseFacts;
        private readonly ShopliftingFactEditorFixture shopliftingFacts;
        private readonly EditorFacilityUseFactFixture facilityUseFacts;
        private readonly ShopliftingActivityObserver shopliftingActivities;
        private readonly Clock clock = new();
        private readonly ObservedCrimeRisk risk = new();
        private readonly GameEventBus events = new();
        private bool disposed;
        internal MigratedProducerOutcomeProbeTransaction Probe { get; }
        private string VisitorId => "character:world:visitor:" + ownerId;
        private string WorkerId => "character:world:worker:" + ownerId;
        private string ShopId => "building:visitor-checkout:" + ownerId;
        private string LotOperation => "retail-source:visitor-checkout:" + ownerId;
        private ExteriorIncidentRuntimeState Incident => exterior.IncidentStates.Single();
        internal int Stage => !Incident.receptionApplied ? 0 : shop.GetStockCount() > 0 ? 1 : 2;
        internal int ExactCount => Exact().Count;

        internal Context(string ownerId, JObject checkpoint = null)
        {
            this.ownerId = CausalGameplaySyntax.RequireId(ownerId, nameof(ownerId));
            outcomes = new MigratedProducerOutcomeEditorFixture(
                ProductionCausalOwnerCheckpoint.LedgerRunId(ownerId, OwnerKind));
            Probe = new MigratedProducerOutcomeProbeTransaction(outcomes.Transaction, outcomes.Recorder);
            facilityUseFacts = new EditorFacilityUseFactFixture(
                outcomes,
                Probe);
            domainFacts.Subscribe<ExteriorVisitorReceptionFact>("test:exterior.visitor-reception",
                new MigratedProducerDomainFactProbeObserver<ExteriorVisitorReceptionFact>(
                    ExteriorVisitorReceptionFactMapping.Capture, outcomes.Transaction,
                    new GameplayDomainFactCommitGroup(outcomes.Recorder, outcomes.Ledger), Probe));
            world = new CustomerAiDebugScenarios.CustomerAiScenarioWorld();
            try
            {
                // 캐릭터 단위 저장과 복원은 같은 이동 provenance 소유자를 사용한다.
                actorSave = (CharacterWorldSaveService)FormatterServices.GetUninitializedObject(
                    typeof(CharacterWorldSaveService));
                Field(typeof(CharacterWorldSaveService), "responseMovements").SetValue(
                    actorSave, new SocietyResponseMovementProvenanceRuntime());
                shop = world.Place("S01_판매카운터", new Vector2Int(4, 0)) as Shop
                    ?? throw new InvalidOperationException("Authored staffed retail counter is missing.");
                shop.RestorePersistentIdentity(new BuildingInstanceId(ShopId));
                Field(typeof(BuildableObject), "gameClock").SetValue(shop, clock);
                visitor = world.CreateCustomer("Slime", Vector2Int.zero, 90f, 90f, 10f, 20f, clock, VisitorId,
                    includeCarryInventory: true);
                worker = world.StaffShop(shop, clock, WorkerId, includeCarryInventory: true)
                    ?? throw new InvalidOperationException("Controlled assigned worker is missing.");
                shopping = visitor.GetAbility<AbilityShopping>()
                    ?? throw new InvalidOperationException("Shopping component missing.");
                shopping.RestorePersistentState(2, 0, 500);
                var session = new FixedGameSessionStateProvider(Day);
                purchaseFacts = new RetailPurchaseFactEditorFixture(outcomes, Probe);
                shopping.ConstructRetailPurchaseFacts(
                    purchaseFacts.Publisher,
                    purchaseFacts.Recovery,
                    session);
                shopliftingFacts = new ShopliftingFactEditorFixture(
                    outcomes,
                    Probe);
                shop.ConstructShopCrimeFacts(shopliftingFacts.Journal, session);
                shop.ConstructBuildableObjectEventBus(events,
                    new BuildingVisitEventPublisher(
                        events,
                        session,
                        facilityUseFacts.Facts,
                        facilityUseFacts.Facts),
                    new BuildingInfoPresentationAdapter(events));
                shopliftingActivities = new ShopliftingActivityObserver(events);
                shopliftingActivities.Initialize();
                shop.ConfigureCrimeForDiagnostics(risk, new ZeroRoll());
                visitor.transform.position = shop.GetFacilityAnchorWorldPosition(
                    FacilityAnchorPurposeIds.Use, visitor.transform.position);
                worker.transform.position = visitor.transform.position;
                Require(world.Grid.IsWalkable(world.Grid.GetXY(visitor.transform.position)),
                    "Controlled visitor/staff restore cell is not walkable.");
                ShopStockStateSnapshot authored = shop.CreateStockSnapshot();
                RetailStockLotSnapshot lot = authored.lots.FirstOrDefault(value => value != null
                    && value.quantity > 0 && string.IsNullOrEmpty(value.itemInstanceId))?.Clone()
                    ?? throw new InvalidOperationException("Authored stackable lot missing.");
                lot.quantity = 1;
                // Controlled initial stock, with stable bootstrap receipt identity.
                lot.sourceOperationId = LotOperation;
                shop.ApplyStockSnapshot(new ShopStockStateSnapshot
                {
                    schemaVersion = ShopStockStateSnapshot.CurrentSchemaVersion,
                    activatedAuthoredSaleItemIds = new List<int>(authored.activatedAuthoredSaleItemIds),
                    lots = new List<RetailStockLotSnapshot> { lot }
                });
                stockSave = new ShopStockStateModule(shop);
                facilitySave = new FacilityRuntimeStateModule(shop);
                var calendar = new FixedCalendar();
                var handler = new InformantExteriorIncidentHandler(new EnteredVisitor(visitor),
                    new OffenseRegionRuntime(), events, calendar, domainFacts);
                exterior = VisitorReceptionCheckoutCausalTestPorts.CreateExterior(world.Grid,
                    new BuildableObject[] { shop }, new[] { visitor, worker }, clock, calendar,
                    new RandomStreamProvider(104729), new ExteriorIncidentHandlerRegistry(new[] { handler }), out _);
                if (checkpoint == null) RestoreExterior(InitialExterior());
                else Restore(checkpoint);
                Validate();
            }
            catch { Dispose(); throw; }
        }

        private DungeonExteriorActivitySaveData InitialExterior() => new()
        {
            nextIncidentSequence = 2,
            zones = new List<ExteriorZoneSaveData> { new()
            {
                zoneId = ZoneId, buildingInstanceId = "building:visitor-reception-zone:" + ownerId,
                zoneType = ExteriorZoneType.ReceptionPoint, gridX = 30, gridY = 0,
                receptionReadiness = 55f, cleanliness = 100f
            } },
            incidentStates = new List<ExteriorIncidentRuntimeState> { new()
            {
                incidentId = IncidentId, kind = ExteriorIncidentKind.Informant, zoneId = ZoneId,
                stage = ExteriorIncidentStage.Active, durationSeconds = 150f, remainingSeconds = 150f,
                text = "안으로 들어온 정보상을 응대한다.", actorIds = new List<string> { VisitorId }
            } }
        };

        private void RestoreExterior(DungeonExteriorActivitySaveData dto)
        {
            var candidate = exterior.BuildRestoreCandidate(dto);
            exterior.BeginRestoreCandidate();
            try
            {
                exterior.PublishRestoreCandidate(candidate);
                exterior.PublishRestoreCandidate();
                exterior.CompleteRestoreCandidate();
            }
            catch
            {
                exterior.RollbackPublishedRestoreCandidate();
                exterior.DiscardRestoreCandidate();
                throw;
            }
            Require(JToken.DeepEquals(Dto(dto), Dto(exterior.Capture())), "Exterior canonical restore changed payload.");
        }

        internal bool Receive()
        {
            if (Incident.receptionApplied) return false;
            int publications = 0;
            bool premature = false;
            void Observe()
            {
                publications++;
                var saved = outcomes.Ledger.CaptureGameplayOutcomes();
                if (!saved.exactOutcomes.Concat(saved.outbox).Any(row => row.outcomeTypeId
                    == Type(MigratedProducerOutcomeKind.ExteriorVisitorReceptionAppliedEvent)))
                    premature = true;
            }
            void ObserveStats(IReadOnlyDictionary<CharacterCondition, float> _) => Observe();
            void ObserveMood(CharacterMoodSnapshot _) => Observe();
            visitor.Progression.Changed += Observe;
            visitor.Stats.OnStatsInvalidated += Observe;
            visitor.Stats.OnStatChange += ObserveStats;
            visitor.Stats.OnMoodChange += ObserveMood;
            // Invoke the production aggregate tick; never mutate a detached state projection.
            try { Invoke(typeof(ExteriorActivityRuntime), "TickIncidentStates", exterior, 0f); }
            finally
            {
                visitor.Progression.Changed -= Observe;
                visitor.Stats.OnStatsInvalidated -= Observe;
                visitor.Stats.OnStatChange -= ObserveStats;
                visitor.Stats.OnMoodChange -= ObserveMood;
            }
            Require(!premature && (Incident.receptionApplied ? publications >= 4 : publications == 0),
                "Reception published narrative/stats before commit, on rejection, or omitted committed publications.");
            Validate();
            return Incident.receptionApplied;
        }

        internal JObject VerifyCommittedObserverFailure(string channel)
        {
            int progression = 0, invalidated = 0, stats = 0, mood = 0, receptionEvents = 0;
            void OnProgression() { if (channel == "progression") throw new InvalidOperationException("injected-progression-observer"); progression++; }
            void OnInvalidated() { if (channel == "invalidated") throw new InvalidOperationException("injected-invalidated-observer"); invalidated++; }
            void OnStats(IReadOnlyDictionary<CharacterCondition, float> _) { if (channel == "stats") throw new InvalidOperationException("injected-stats-observer"); stats++; }
            void OnMood(CharacterMoodSnapshot _) { if (channel == "mood") throw new InvalidOperationException("injected-mood-observer"); mood++; }
            using var subscription = events.Subscribe<ExteriorVisitorReceptionAppliedEvent>(_ => receptionEvents++);
            visitor.Progression.Changed += OnProgression;
            visitor.Stats.OnStatsInvalidated += OnInvalidated;
            visitor.Stats.OnStatChange += OnStats;
            visitor.Stats.OnMoodChange += OnMood;
            try { Invoke(typeof(ExteriorActivityRuntime), "TickIncidentStates", exterior, 0f); }
            finally
            {
                visitor.Progression.Changed -= OnProgression;
                visitor.Stats.OnStatsInvalidated -= OnInvalidated;
                visitor.Stats.OnStatChange -= OnStats;
                visitor.Stats.OnMoodChange -= OnMood;
            }
            Validate();
            Require(Stage == 1 && ExactCount == 1 && receptionEvents == 1
                && progression == (channel == "progression" ? 0 : 1)
                && invalidated == (channel == "invalidated" ? 0 : 1)
                && stats == (channel == "stats" ? 0 : 1) && mood == (channel == "mood" ? 0 : 1),
                "A committed failing observer suppressed another channel or rolled back reception: " + channel);
            return new JObject { ["injectedFailure"] = channel, ["durablyCommitted"] = true,
                ["otherChannelsAttempted"] = true, ["receptionEventCount"] = receptionEvents };
        }

        internal void VerifyImmediateMoodPublication()
        {
            const string id = "test:ordinary-mood-publication";
            int changed = 0, moodPublished = 0;
            void OnChanged() => changed++;
            void OnMood(CharacterMoodSnapshot _) => moodPublished++;
            visitor.Progression.Changed += OnChanged;
            visitor.Stats.OnMoodChange += OnMood;
            try { visitor.ApplyMoodFactor(id, "일반 기분 경로 회귀 검사", 1f, 180f, 1); }
            finally
            {
                visitor.Progression.Changed -= OnChanged;
                visitor.Stats.OnMoodChange -= OnMood;
            }
            Require(changed == 1 && moodPublished == 1 && ExactCount == 0
                && visitor.Stats.GetMoodSnapshot().Factors.Count(value => value.Id == id) == 1
                && CaptureActor(visitor)["narrative"]["facts"].Count(value => (string)value["factId"] == id) == 1,
                "Ordinary nontransactional mood no longer records/publishes immediately.");
        }

        internal bool Checkout()
        {
            if (Stage != 1) return false;
            Require(shop.HasServingWorker && shop.CanServeCustomer(visitor.BuildingVisitor, out _),
                "Restored assigned worker cannot serve visitor.");
            AIActionSet actionSet = Resources.Load<AIActionSet>("SO/AI/Action/Shopping")
                ?? throw new InvalidOperationException("Authored shopping action missing.");
            visitor.ai.SetSelectedActionForDebug(new AIAction(actionSet, AIActionPlan.AtDestination(shop)));
            visitor.ai.SetDecisionPendingForDebug(false);
            shopping.BeginVisitInteraction(shop);
            var routines = new Stack<IEnumerator>();
            routines.Push(shop.Interact(visitor.BuildingVisitor));
            int steps = 0;
            try
            {
                while (routines.Count > 0)
                {
                    Require(++steps <= 10000, "Controlled checkout did not terminate.");
                    clock.Advance();
                    Invoke(typeof(ExteriorActivityRuntime), "TickIncidentStates", exterior, clock.DeltaTime);
                    IEnumerator current = routines.Peek();
                    if (!current.MoveNext())
                    {
                        routines.Pop();
                        (current as IDisposable)?.Dispose();
                    }
                    else if (current.Current is IEnumerator nested) routines.Push(nested);
                }
            }
            finally { while (routines.Count > 0) (routines.Pop() as IDisposable)?.Dispose(); }
            Require(risk.Draws == 1 && risk.LastChance > 0f && risk.LastRoll == 0f,
                "Checkout did not use actual authored risk and explicit controlled roll.");
            Require(shopping.LastVisitOutcome == ShoppingVisitOutcome.Completed && shop.CurrentUserCount == 0,
                "Checkout did not complete/release its visit.");
            Validate();
            return Stage == 2;
        }

        internal IReadOnlyDictionary<string, object> CaptureState()
        {
            Validate();
            var factor = visitor.Stats.GetMoodSnapshot().Factors.SingleOrDefault(value =>
                value.Id == "exterior:reception:" + IncidentId);
            return new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["visitorCheckout.ownerId"] = ownerId,
                ["visitorCheckout.stage"] = Stage,
                ["visitorCheckout.absoluteDay"] = Day,
                ["visitorCheckout.actorId"] = visitor.Identity.PersistentId,
                ["visitorCheckout.actorName"] = visitor.Identity.DisplayName,
                ["visitorCheckout.workerId"] = worker.Identity.PersistentId,
                ["visitorCheckout.shopId"] = shop.RequirePersistentInstanceId().Value,
                ["visitorCheckout.incidentId"] = Incident.incidentId,
                ["visitorCheckout.receptionApplied"] = Incident.receptionApplied,
                ["visitorCheckout.receptionMood"] = StateNumber(factor?.Value ?? 0f),
                ["visitorCheckout.receptionRemainingSeconds"] = StateNumber(factor?.RemainingSeconds ?? 0f),
                ["visitorCheckout.incidentRemainingSeconds"] = StateNumber(Incident.remainingSeconds),
                ["visitorCheckout.incidentProgress"] = StateNumber(Incident.progress),
                ["visitorCheckout.stockQuantity"] = shop.GetStockCount(),
                ["visitorCheckout.completedUses"] = shop.FacilityState.completedUses,
                ["visitorCheckout.holdingMoney"] = shopping.HoldingMoney,
                ["visitorCheckout.currentUsers"] = shop.CurrentUserCount,
                ["visitorCheckout.crimeDraws"] = risk.Draws,
                ["visitorCheckout.crimeChance"] = StateNumber(risk.LastChance),
                ["visitorCheckout.crimeRoll"] = StateNumber(risk.LastRoll),
                ["visitorCheckout.exactOutcomeCount"] = ExactCount,
                ["visitorCheckout.clockTime"] = StateNumber(clock.Time),
                ["visitorCheckout.clockFrame"] = clock.FrameCount
            };
        }

        internal GameplayOutcomeReplayIdentity RequireExact(MigratedProducerOutcomeKind kind)
        {
            GameplayOutcomeSnapshot exact = Exact().Single(value => value.outcomeTypeId == Type(kind));
            var key = new GameplayResultKey(exact.producerId, new GameplayOperationId(exact.operationId),
                exact.commitRevision, exact.localResultIndex);
            Require(outcomes.Ledger.TryGetResultIdentity(key, out var identity)
                && identity.State == GameplayOutcomeReplayState.PublishedAcknowledged
                && identity.HasCanonicalPayloadHash, "Canonical visitor outcome identity is missing.");
            return identity;
        }

        internal JObject CaptureOutcomeEvidence(GameplayOutcomeReplayIdentity identity) =>
            ProductionCausalOutcomeEvidence.Capture(outcomes.Ledger, identity);

        internal JObject Checkpoint()
        {
            Validate();
            return ProductionCausalOwnerCheckpoint.CaptureFamily(ownerId, OwnerKind, RestoreKind,
                Domain(), outcomes.Ledger);
        }

        internal JObject Domain() => new()
        {
            ["schemaVersion"] = 1, ["schema"] = Schema, ["stage"] = Stage,
            ["day"] = Day, ["clockTime"] = clock.Time, ["clockFrame"] = clock.FrameCount,
            ["shopId"] = ShopId, ["workerId"] = WorkerId,
            ["visitor"] = CaptureActor(visitor), ["worker"] = CaptureActor(worker),
            ["visitorFullMood"] = CanonicalMood(visitor),
            ["workerFullMood"] = CanonicalMood(worker),
            ["exterior"] = Dto(exterior.Capture()),
            ["shopStockVersion"] = stockSave.CurrentVersion,
            ["shopStock"] = JObject.Parse(stockSave.CaptureState()),
            ["facilityVersion"] = facilitySave.CurrentVersion,
            ["facility"] = JObject.Parse(facilitySave.CaptureState()),
            ["crimeEvaluation"] = risk.Capture(),
            ["migratedOutcomeOwners"] = Dto(outcomes.Transaction.Capture())
        };

        private JObject CaptureActor(CharacterActor actor)
        {
            Require(actor.Identity != null && actor.Stats != null && actor.Lifecycle != null
                && actor.Progression != null && actor.SocialMemory != null && actor.LogComponent != null
                && actor.GetComponent<CharacterCarryInventory>() != null
                && actor.GetAbility<AbilityShopping>() != null && actor.GetAbility<AbilityMove>() != null,
                "Canonical actor capture requires all touched runtime components.");
            return Dto((DungeonCharacterSaveData)Invoke(typeof(CharacterWorldSaveService),
                "CaptureActor", actorSave, world.Grid, actor));
        }

        private void RestoreActor(CharacterActor actor, JToken source)
        {
            DungeonCharacterSaveData dto = ReadDto<DungeonCharacterSaveData>(source);
            Require(dto.persistentId == actor.Identity.PersistentId
                && dto.dataId == actor.Identity.Data.id && dto.displayName == actor.Identity.DisplayName
                && dto.characterType == actor.Identity.CharacterType && dto.role == actor.Identity.Role
                && !dto.isOwner, "Actor identity/authored profile changed.");
            Require(dto.acquiredTraits != null && !dto.acquiredTraits.HasPersistentData,
                "This controlled slice does not restore nonempty acquired-trait module authorities.");
            var report = new DungeonGameRestoreReport();
            Invoke(typeof(CharacterWorldRestorePayloadValidator), "ValidateActor", null,
                world.Grid, dto, new Dictionary<int, CharacterSO> { [dto.dataId] = actor.Identity.Data }, report, false);
            Require(report.Success, "Canonical actor validation: " + string.Join("; ", report.Errors));
            CaptureActor(actor); // assert dependencies before null-conditional production Apply.
            Invoke(typeof(CharacterWorldSaveService), "ApplyActorState", actorSave, world.Grid, actor, dto);
            Require(JToken.DeepEquals(source, CaptureActor(actor)),
                "Canonical actor payload did not round-trip: " + dto.persistentId);
        }

        private void Restore(JObject checkpoint)
        {
            JObject state = ProductionCausalOwnerCheckpoint.RestoreFamilyCommon(
                checkpoint, ownerId, OwnerKind, RestoreKind, outcomes.Ledger);
            RequireFields(state, "schemaVersion", "schema", "stage", "day", "clockTime", "clockFrame",
                "shopId", "workerId", "visitor", "worker", "visitorFullMood", "workerFullMood", "exterior",
                "shopStockVersion", "shopStock", "facilityVersion", "facility", "crimeEvaluation", "migratedOutcomeOwners");
            Require((int)state["schemaVersion"] == 1 && (string)state["schema"] == Schema
                && (int)state["day"] == Day && (string)state["shopId"] == ShopId
                && (string)state["workerId"] == WorkerId, "Saved owner/schema binding changed.");
            clock.Restore((float)state["clockTime"], (int)state["clockFrame"]);
            outcomes.Transaction.Restore(outcomes.Transaction.PrepareRestore(
                ReadDto<DungeonMigratedProducerOutcomeSaveData>(state["migratedOutcomeOwners"])));
            RestoreActor(visitor, state["visitor"]);
            RestoreActor(worker, state["worker"]);
            ReadDto<ShopStockStateSnapshot>(state["shopStock"]);
            ReadDto<FacilityRuntimeState>(state["facility"]);
            Require(stockSave.TryRestoreState((int)state["shopStockVersion"],
                state["shopStock"].ToString(Formatting.None), out string stockError), "Stock restore: " + stockError);
            Require(facilitySave.TryRestoreState((int)state["facilityVersion"],
                state["facility"].ToString(Formatting.None), out string facilityError), "Facility restore: " + facilityError);
            risk.Restore(state["crimeEvaluation"] as JObject);
            RestoreExterior(ReadDto<DungeonExteriorActivitySaveData>(state["exterior"]));
            Validate();
            JObject restored = Domain();
            string mismatch = state.Properties().FirstOrDefault(property =>
                !JToken.DeepEquals(property.Value, restored[property.Name]))?.Name;
            Require((int)state["stage"] == Stage && mismatch == null,
                "Full per-owner domain did not round-trip: " + mismatch
                + "; expected=" + (mismatch == null ? "" : state[mismatch].ToString(Formatting.None))
                + "; actual=" + (mismatch == null ? "" : restored[mismatch].ToString(Formatting.None)));
        }

        internal JArray OrderedEvidence()
        {
            var values = new JArray();
            string previous = null;
            foreach (GameplayOutcomeSnapshot row in Exact())
            {
                var key = new GameplayResultKey(row.producerId, new GameplayOperationId(row.operationId),
                    row.commitRevision, row.localResultIndex);
                Require(outcomes.Ledger.TryGetResultIdentity(key, out var identity)
                    && identity.State == GameplayOutcomeReplayState.PublishedAcknowledged
                    && identity.HasCanonicalPayloadHash, "Canonical replay identity missing.");
                JObject evidence = ProductionCausalOutcomeEvidence.Capture(outcomes.Ledger, identity);
                values.Add(new JObject
                {
                    ["outcomeEvidence"] = evidence, ["atomicSibling"] = false,
                    ["relation"] = row.outcomeTypeId == Type(MigratedProducerOutcomeKind.FacilityVisitEvent)
                        ? "post-commit-continuation" : previous == null ? "root-command" : "restored-state-context",
                    ["predecessorOutcomeId"] = previous == null ? JValue.CreateNull() : new JValue(previous)
                });
                previous = (string)evidence["outcomeId"];
            }
            return values;
        }

        private List<GameplayOutcomeSnapshot> Exact() => outcomes.Ledger.CaptureGameplayOutcomes().exactOutcomes
            .OrderBy(value => value.sequence).ToList();

        internal void Validate()
        {
            Require(!disposed, "Disposed visitor owner.");
            int stage = Stage;
            Require(visitor.Identity.PersistentId == VisitorId && worker.Identity.PersistentId == WorkerId
                && shop.RequirePersistentInstanceId().Value == ShopId && shop.HasServingWorker
                && visitor.CurrentLifecycleState == CharacterLifecycleState.Active
                && worker.CurrentLifecycleState == CharacterLifecycleState.Active,
                "Restored actor/shop/staff identity or lifecycle diverged.");
            Require(Incident.incidentId == IncidentId && Incident.zoneId == ZoneId
                && Incident.kind == ExteriorIncidentKind.Informant && Incident.stage == ExteriorIncidentStage.Active
                && Incident.outcome == ExteriorIncidentOutcome.None && Incident.durationSeconds == 150f
                && Incident.actorIds.SequenceEqual(new[] { VisitorId }) && Incident.wildlifeIds.Count == 0
                && Incident.itemStackIds.Count == 0 && exterior.Capture().nextIncidentSequence == 2
                && Mathf.Abs(Incident.remainingSeconds - (150f - clock.Time)) < 0.0001f
                && Mathf.Abs(Incident.progress - (stage > 0 ? 16f + 0.22f * clock.Time : 0f)) < 0.001f,
                "Canonical informant identity/reception/progress/timer diverged.");
            ExteriorZoneSaveData zone = exterior.Capture().zones.Single();
            Require(zone.zoneId == ZoneId && zone.receptionReadiness == 55f
                && zone.buildingInstanceId == "building:visitor-reception-zone:" + ownerId,
                "Reception zone identity/readiness changed.");
            var factor = visitor.Stats.GetMoodSnapshot().Factors.SingleOrDefault(value =>
                value.Id == "exterior:reception:" + IncidentId);
            Require(stage == 0 ? factor == null : factor != null && Mathf.Approximately(factor.Value, 2.65f)
                && Mathf.Approximately(factor.RemainingSeconds, 180f - clock.Time),
                "Reception mood value/duration no longer agrees with actual event.");
            var moodFacts = CaptureActor(visitor)["narrative"]["facts"]
                .Where(value => (string)value["factId"] == "exterior:reception:" + IncidentId).ToArray();
            Require(stage == 0 ? moodFacts.Length == 0 : moodFacts.Length == 1
                && (int)moodFacts[0]["lastDay"] == Day && (int)moodFacts[0]["count"] == 1,
                "Reception narrative fact is missing, duplicated or dated differently from its exact outcome.");
            var crimeFacts = CaptureActor(visitor)["narrative"]["facts"]
                .Where(value => (string)value["factId"] == "crime:shoplifting").ToArray();
            Require(stage < 2 ? crimeFacts.Length == 0 : crimeFacts.Length == 1
                && (int)crimeFacts[0]["lastDay"] == Day && (int)crimeFacts[0]["count"] == 1,
                "Crime narrative fact is missing, duplicated or dated differently from its exact outcome.");
            ShopStockStateSnapshot stock = shop.CreateStockSnapshot();
            Require(shop.GetStockCount() == (stage == 2 ? 0 : 1)
                && shop.FacilityState.completedUses == (stage == 2 ? 1 : 0)
                && shopping.HoldingMoney == 500 && shop.CurrentUserCount == 0,
                "Stock, visit completion, money or active-use ownership diverged.");
            if (stage < 2)
                Require(stock.lots.Count == 1 && stock.lots[0].quantity == 1
                    && stock.lots[0].sourceOperationId == LotOperation
                    && string.IsNullOrEmpty(stock.lots[0].itemInstanceId), "Initial exact retail lot identity changed.");
            Require(risk.Draws == (stage == 2 ? 1 : 0), "Checkout risk execution count differs from committed stage.");
            var kinds = new List<MigratedProducerOutcomeKind>();
            if (stage > 0) kinds.Add(MigratedProducerOutcomeKind.ExteriorVisitorReceptionAppliedEvent);
            if (stage == 2)
            {
                kinds.Add(MigratedProducerOutcomeKind.FacilityCrimeEvent);
                kinds.Add(MigratedProducerOutcomeKind.FacilityVisitEvent);
            }
            var exact = Exact();
            Require(exact.Count == kinds.Count, "A touched production outcome is missing or duplicated.");
            for (int index = 0; index < kinds.Count; index++)
            {
                string subject = index == 0 ? VisitorId : ShopId;
                GameplayOutcomeSnapshot row = exact[index];
                Require(row.outcomeTypeId == Type(kinds[index]) && row.absoluteDay == Day
                    && row.status == GameplayOutcomeStatus.Succeeded && row.sequence == index + 1
                    && row.subjects.Count == 1 && row.subjects[0].entityId == subject,
                    "Ordered reception/checkout/visit exact identity changed.");
            }
            OrderedEvidence();
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            shopliftingActivities?.Dispose();
            shopliftingFacts?.Dispose();
            purchaseFacts?.Dispose();
            facilityUseFacts?.Dispose();
            domainFacts.Dispose();
            if (exterior != null)
            {
                // Batch EditMode requires immediate teardown, before the next grid composition.
                foreach (var zone in exterior.Zones.ToArray())
                    if (zone != null) Object.DestroyImmediate(zone.gameObject);
                exterior.Dispose();
            }
            world?.Dispose();
        }
    }

    private sealed class Clock : IGameClock
    {
        public float DeltaTime => 0.25f;
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused => false;
        internal void Advance() { Time += DeltaTime; FrameCount++; }
        internal void Restore(float time, int frame)
        {
            Require(frame >= 0 && frame < 480 && time == frame * DeltaTime, "Controlled clock is invalid.");
            Time = time; FrameCount = frame;
        }
    }

    private sealed class FixedCalendar : IGameCalendar
    {
        public int Day => VisitorReceptionCheckoutCausalProductionEntryDebugScenarios.Day;
        public int Hour => 0;
        public int Year => 1;
        public int DayOfYear => Day;
        public Season Season => default;
        public int DayOfSeason => Day;
        public long AbsoluteHour => Day * 24L;
        public float ElapsedSeconds => 0f;
        public TimeOfDay TimeOfDay => default;
        public bool IsRunning => false;
        public CalendarDateTime Current => default;
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) => throw new NotSupportedException();
        public void Start() => throw new NotSupportedException();
        public void SetDateTime(int day, int hour) => throw new NotSupportedException();
    }

    private sealed class EnteredVisitor : IExteriorIncidentActorService
    {
        private readonly CharacterActor visitor;
        internal EnteredVisitor(CharacterActor visitor) => this.visitor = visitor;
        public bool TryFind(string actorId, out CharacterActor actor)
        { actor = actorId == visitor.Identity.PersistentId ? visitor : null; return actor != null; }
        public bool HasEnteredDungeon(string actorId) => actorId == visitor.Identity.PersistentId;
        public bool TrySpawn(ExteriorIncidentKind kind, string actorId, Vector2Int position, bool downed,
            out CharacterActor actor, out string failureReason) => throw new NotSupportedException("Visitor entry is controlled initial state.");
        public void Despawn(string actorId) => throw new NotSupportedException("Informant departure is outside this slice.");
    }

    private sealed class ZeroRoll : IRandomStream
    {
        public ulong State => 0UL;
        public int NextInt(int minInclusive, int maxExclusive) => throw new NotSupportedException();
        public float NextFloat() => 0f;
        public bool Chance(float probability) => throw new NotSupportedException();
        public void Restore(ulong state) { Require(state == 0, "Only the explicit zero roll is supported."); }
    }

    private sealed class ObservedCrimeRisk : IFacilityCrimeRiskEvaluator
    {
        private readonly IFacilityCrimeRiskEvaluator actual = FacilityCrimeEditorTestDependencies.Evaluator;
        private JObject evaluation = new() { ["draws"] = 0 };
        internal int Draws => (int)evaluation["draws"];
        internal float LastChance => (float?)evaluation["chance"] ?? 0f;
        internal float LastRoll => (float?)evaluation["roll"] ?? -1f;
        public float CalculateShopliftingChance(FacilityCrimeRiskContext context)
        {
            float chance = actual.CalculateShopliftingChance(context);
            CharacterActor actor = CharacterBuildingVisitorAdapter.GetActorOrNull(context.actor)
                ?? throw new InvalidOperationException("Risk calculation lost its actual actor.");
            evaluation = new JObject
            {
                ["draws"] = Draws, ["chance"] = chance, ["roll"] = -1f,
                ["visitorId"] = actor.Identity.PersistentId, ["fullMood"] = CanonicalMood(actor),
                ["hasServingWorker"] = context.hasServingWorker, ["hasWaitingCheckout"] = context.hasWaitingCheckout,
                ["currentUserCount"] = context.currentUserCount, ["cartItemCount"] = context.cartItemCount,
                ["cartValue"] = context.cartValue, ["currentStock"] = context.currentStock, ["isDamaged"] = context.isDamaged
            };
            return chance;
        }
        public float CalculateOperationalRisk(FacilityCrimeRiskContext context) => actual.CalculateOperationalRisk(context);
        public bool ShouldTriggerCrime(float chance, float roll)
        {
            evaluation["draws"] = Draws + 1; evaluation["roll"] = roll;
            return actual.ShouldTriggerCrime(chance, roll);
        }
        internal JObject Capture() => Copy(evaluation);
        internal void Restore(JObject value)
        {
            Require(value != null && (int?)value["draws"] is >= 0 and <= 1, "Saved risk observation invalid.");
            evaluation = Copy(value);
            Require(Draws == 0 ? value.Count == 1 : LastChance > 0f && LastChance <= 1f && LastRoll == 0f,
                "Controlled risk result is inconsistent.");
        }
    }

    private static string Type(MigratedProducerOutcomeKind kind) => MigratedProducerOutcomeCatalog.Get(kind).OutcomeTypeId.Value;
    private static JObject Dto(object value) => JObject.Parse(JsonUtility.ToJson(value));
    private static T ReadDto<T>(JToken token)
    {
        Require(token is JObject, "Missing typed canonical DTO: " + typeof(T).Name);
        T value = JsonUtility.FromJson<T>(token.ToString(Formatting.None));
        Require(value != null && JToken.DeepEquals(token, Dto(value)), "Noncanonical/unknown DTO field: " + typeof(T).Name);
        return value;
    }
    private static JObject Copy(JObject value) => JObject.Parse(value.ToString(Formatting.None));
    // Compare persisted JSON tokens, not boxed Single versus reparsed Double values.
    // Every factor/value/duration remains present, with no epsilon or skipped field.
    private static JObject CanonicalMood(CharacterActor actor) =>
        Copy(JObject.FromObject(actor.Stats.GetMoodSnapshot()));
    private static void RequireFields(JObject value, params string[] fields) => Require(
        value != null && value.Properties().Select(field => field.Name).OrderBy(name => name)
            .SequenceEqual(fields.OrderBy(name => name)), "Unknown/missing owner state fields.");
    private static FieldInfo Field(Type type, string name) => type.GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Required Editor binding missing: " + type.Name + "." + name);
    private static object Invoke(Type type, string name, object target, params object[] args)
    {
        MethodInfo method = type.GetMethod(name, BindingFlags.Static | BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("Production persistence/tick method missing: " + type.Name + "." + name);
        try { return method.Invoke(target, args); }
        catch (TargetInvocationException exception) when (exception.InnerException != null)
        { ExceptionDispatchInfo.Capture(exception.InnerException).Throw(); throw; }
    }
    private static string RequiredEnvironment(string name)
    {
        string value = Environment.GetEnvironmentVariable(name);
        Require(!string.IsNullOrWhiteSpace(value), "Missing environment: " + name);
        return value;
    }
    private static void WriteNew(string path, JToken value)
    {
        using var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        using var writer = new StreamWriter(stream, new System.Text.UTF8Encoding(false));
        writer.Write(value.ToString(Formatting.Indented));
    }
    private static void Require(bool condition, string message)
    { if (!condition) throw new InvalidOperationException(message); }
}
#endif
