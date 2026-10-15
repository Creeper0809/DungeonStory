#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEngine;

/// <summary>
/// Actual industrial producers and typed fact observer/ledger, with explicitly
/// bounded support inputs. No source receipt or successful parent is fabricated.
/// This is a controlled Editor integration test, not natural-play training data.
/// </summary>
internal static class E12CanonicalInfrastructureVerifier
{
    internal static void Run(string output)
    {
        if (File.Exists(output))
            throw new InvalidOperationException("E12 canonical evidence already exists: " + output);
        Report report = new();
        void OnLog(string message, string stack, LogType type)
        {
            if (type is LogType.Error or LogType.Exception or LogType.Assert)
                report.errors.Add(message);
            else if (type == LogType.Warning)
                report.warnings.Add(message);
        }
        Application.logMessageReceived += OnLog;
        try
        {
            using Context context = new(report);
            VerifyFuel(context);
            VerifyBreaker(context);
            VerifyFluid(context);
            context.VerifyRecords(9);
            report.records = context.Snapshots().ToList();
            report.infrastructureOwner = context.Outcomes.Capture();
            report.ledger = context.Ledger.CaptureGameplayOutcomes();
            context.Check(report.errors.Count == 2
                    && report.errors.Count(value => value.Contains("qa-e12-reject-delivery", StringComparison.Ordinal)) == 1
                    && report.errors.Count(value => value.Contains("qa-e12-reject-prepare", StringComparison.Ordinal)) == 1,
                "only the explicit prepare/delivery faults are logged; unexpected errors=0");
            report.passed = true;
        }
        catch (Exception exception)
        {
            report.failure = exception.ToString();
            throw;
        }
        finally
        {
            Application.logMessageReceived -= OnLog;
            using FileStream stream = new(output, FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            using StreamWriter writer = new(stream, new UTF8Encoding(false));
            writer.Write(JsonUtility.ToJson(report, true));
        }
    }

    private static void VerifyFuel(Context context)
    {
        BuildingAbilityCollection abilities = new();
        abilities.Add(new BuildingUtilityConnectionAbility { channels = UtilityChannel.Power });
        abilities.Add(new BuildingPowerProducerAbility
        {
            productionPerSecond = 10f, requiresFuel = true,
            fuelItemId = "resource:coal", secondsPerFuel = 120f
        });
        using Facility facility = new("building:qa:e12-canonical-fuel", "시험 발전기", abilities);
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        using WorldItemStackRuntime items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
            catalog, out WorldItemRepository repository, out _, out _, out _, out _,
            out IPhysicalItemBatchDispositionService batch);
        string destination = "power:" + facility.Id;
        string sourceStack = WorldItemRepositoryEditorAccess.AddStack(repository,
            "resource:coal", quantity: 2, state: WorldItemStackState.FacilityBuffer,
            destinationId: destination, position: facility.Building.centerPos);
        FailFirstAcknowledgement physical = new(new PhysicalFacilityItemSinkGateway(
            new PhysicalStockQuery(repository, catalog, items.MassQuery), batch));
        ProbeClock clock = new();
        ElectricalNetworkRuntime first = CreatePower(facility, items, physical, clock, context.Outcomes);
        DungeonPowerInfrastructureSaveData pending = first.Capture();
        PowerNodeSaveData node = pending.nodes.Single();
        context.Observe("fuel/pending-owner", pending);
        context.Observe("fuel/pending-physical", items.Capture());
        context.Check(node.pendingFuel.phase == (int)PowerFuelCommitPhase.OutcomePublished
                && node.pendingFuel.quantity == 1 && node.nextFuelOperationSequence == 1
                && node.fuelSeconds == 120f && repository.GetEditorTestQuantity(sourceStack) == 1,
            "real coal debit once; canonical publication precedes the injected physical acknowledgement failure");
        context.Check(physical.TryGetPending(ElectricalNetworkRuntime.FormatFuelOperationId(facility.Id, 1), out _),
            "physical debit receipt remains pending after acknowledgement failure");
        context.VerifyRecords(1);
        IndustrialTransitionFactData sourceFact = context.Fact(
            InfrastructureCommandOutcomeKind.PowerFuelConsumed);
        InfrastructureCommandOutcomeSource source = context.Source(
            InfrastructureCommandOutcomeKind.PowerFuelConsumed);
        context.Check(sourceFact.ownerRevision == node.pendingFuel.outcomeOwnerRevision
                && source.AfterValue.Contains(sourceStack, StringComparison.Ordinal)
                && source.AfterValue.Contains("quantity=1", StringComparison.Ordinal),
            "fuel parent uses the real debit source identity and owner revision");

        context.RoundTrip("fuel/ledger-and-owner-restored");
        ElectricalNetworkRuntime restored = CreatePower(facility, items, physical, clock, context.Outcomes);
        restored.Restore(restored.PrepareRestore(RoundTrip(pending)));
        DungeonPowerInfrastructureSaveData recovered = restored.Capture();
        context.Observe("fuel/recovered-owner", recovered);
        context.Check(recovered.nodes.Single().pendingFuel.phase == (int)PowerFuelCommitPhase.None
                && recovered.nodes.Single().nextFuelOperationSequence == 2
                && recovered.nodes.Single().fuelSeconds == 120f
                && repository.GetEditorTestQuantity(sourceStack) == 1
                && physical.SuccessfulAcknowledgements == 1
                && repository.GetEditorPendingBatchDispositionCount() == 0,
            "fresh power owner JSON restore acknowledges once without a second debit or parent");
        clock.Advance(10f);
        restored.Tick();
        context.Observe("fuel/after-tick-owner", restored.Capture());
        context.Check(restored.Capture().nodes.Single().fuelSeconds == 110f
                && repository.GetEditorTestQuantity(sourceStack) == 1,
            "fuel time progresses after recovery without consuming another coal");
        context.VerifyRecords(1);
    }

    private static void VerifyBreaker(Context context)
    {
        BuildingAbilityCollection abilities = new();
        abilities.Add(new BuildingUtilityConnectionAbility { channels = UtilityChannel.Power });
        abilities.Add(new BuildingPowerProducerAbility { productionPerSecond = 10f, requiresFuel = false });
        abilities.Add(new BuildingPowerConsumerAbility { demandPerSecond = 30f, minimumSupplyFraction = 0f });
        abilities.Add(new BuildingCircuitBreakerAbility { overloadTolerance = 1.1f, tripHeat = 61f });
        using Facility facility = new("building:qa:e12-canonical-breaker", "시험 차단기", abilities);
        IDungeonItemCatalogProvider catalog = EditorItemCatalogFactory.Create();
        using WorldItemStackRuntime items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
            catalog, out WorldItemRepository repository, out _, out _, out _, out _,
            out IPhysicalItemBatchDispositionService batch);
        IPhysicalFacilityItemSinkGateway physical = new PhysicalFacilityItemSinkGateway(
            new PhysicalStockQuery(repository, catalog, items.MassQuery), batch);
        ProbeClock clock = new();
        ElectricalNetworkRuntime first = CreatePower(facility, items, physical, clock, context.Outcomes);
        long beforeRevision = context.Outcomes.Capture().nextOwnerRevision;
        context.Committer.RejectNextPrepare = true;
        clock.Advance(2f);
        first.Tick();
        DungeonPowerInfrastructureSaveData rejected = first.Capture();
        context.Observe("breaker/rejected-owner", rejected);
        context.Check(!rejected.nodes.Single().breakerTripped
                && context.Outcomes.Capture().nextOwnerRevision == beforeRevision,
            "rejected trip preparation leaves the breaker unlatched and global revision unchanged");
        context.VerifyRecords(1);
        clock.Advance(0.25f);
        first.Tick();
        DungeonPowerInfrastructureSaveData tripped = first.Capture();
        context.Observe("breaker/tripped-owner", tripped);
        context.Check(tripped.nodes.Single().breakerTripped, "real overload threshold latches the breaker");
        context.Check(context.Source(InfrastructureCommandOutcomeKind.PowerBreakerTripped).BeforeValue
                .Contains("loadRatio=3", StringComparison.Ordinal),
            "trip source retains the actual overload ratio");
        context.VerifyRecords(2);
        clock.Advance(0.25f);
        first.Tick();
        context.VerifyRecords(2);
        context.RoundTrip("breaker/ledger-and-owner-restored");
        ElectricalNetworkRuntime restored = CreatePower(facility, items, physical, clock, context.Outcomes);
        restored.Restore(restored.PrepareRestore(RoundTrip(tripped)));
        clock.Advance(3f);
        restored.Tick();
        context.Observe("breaker/restored-owner", restored.Capture());
        context.Check(restored.Capture().nodes.Single().breakerTripped, "saved trip remains latched after fresh owner restore");
        context.VerifyRecords(2);
        context.Check(restored.ResetBreaker(facility.Building).Succeeded, "real manual breaker reset succeeds");
        context.Observe("breaker/reset-owner", restored.Capture());
        context.Check(!restored.Capture().nodes.Single().breakerTripped, "manual reset clears the owner latch");
        context.VerifyRecords(3);
        context.Check(context.Snapshots().Count(value => value.outcomeTypeId
                == InfrastructureCommandOutcomeIds.Applied.Value) == 1,
            "manual reset is separate from autonomous trip, with a shared global revision owner");
    }

    private static void VerifyFluid(Context context)
    {
        BuildingAbilityCollection abilities = new();
        abilities.Add(new BuildingUtilityConnectionAbility
            { channels = UtilityChannel.CleanWater | UtilityChannel.Wastewater });
        abilities.Add(new BuildingWaterStorageAbility
        {
            channels = UtilityChannel.CleanWater | UtilityChannel.Wastewater,
            cleanWaterCapacity = 20f, wastewaterCapacity = 20f
        });
        abilities.Add(new BuildingWastewaterProcessorAbility
        {
            wastewaterInput = 2f, waterOutput = 1f, outputQuality = WorldWaterQuality.Clean,
            requiresPower = false, sludgeItemId = "industrial:sludge", sludgeAmount = 1, secondsPerBatch = 5f
        });
        using Facility facility = new("building:qa:e12-canonical-fluid", "시험 정화 설비", abilities);
        using WorldItemStackRuntime items = PhysicalItemDebugScenarios.CreateRuntimeForCrossDomainFixture(
            EditorItemCatalogFactory.Create(), out _, out _, out _, out _, out _,
            out IPhysicalItemBatchDispositionService batch);
        ProbeClock clock = new();
        MutableEnvironment environment = new(facility.Building.centerPos);
        MutableSeasonal seasonal = new();
        FixtureFilth filth = new();
        FluidNetworkRuntime runtime = CreateFluid(facility, items, batch, filth, clock, environment, seasonal, context.Outcomes);
        context.Check(runtime.TryAdd(facility.Building, WorldWaterQuality.Clean, 10f, out float water) && water == 10f
                && runtime.TryAddWastewater(facility.Building, 2f, out float waste, out _) && waste == 2f,
            "explicit test setup seeds only clean water and wastewater through the real owner");
        DungeonFluidInfrastructureSaveData initial = runtime.Capture();
        initial.nodes.Single().leak = 100f;
        initial.nodes.Single().processorWork = 0f;
        runtime.Restore(runtime.PrepareRestore(initial));
        context.Observe("fluid/prepared-owner", runtime.Capture());
        clock.Advance(10f);
        runtime.Tick();
        context.Observe("fluid/after-batch-owner", runtime.Capture());
        context.Observe("fluid/after-batch-physical", items.Capture());
        WorldItemStackSnapshot sludge = items.GetAllStacks().Single(value => value.ItemId == "industrial:sludge");
        InfrastructureCommandOutcomeSource batchSource = context.Source(
            InfrastructureCommandOutcomeKind.WastewaterBatchCompleted);
        context.Check(sludge.Quantity == 1 && sludge.State == WorldItemStackState.Loose
                && sludge.Position == facility.Building.centerPos
                && batchSource.AfterValue.Contains("sludgeStacks=" + sludge.StackId + ":1", StringComparison.Ordinal)
                && batchSource.AfterValue.Contains("inputUsed=2", StringComparison.Ordinal)
                && batchSource.AfterValue.Contains("waterProduced=1", StringComparison.Ordinal),
            "actual wastewater batch creates one real physical sludge stack and exact conservation facts");
        context.Check(context.Source(InfrastructureCommandOutcomeKind.FluidLeakEpisode).AfterValue
                .Contains("totalLoss=1", StringComparison.Ordinal)
                && filth.GetAll().Single().Amount == 2f,
            "real leak source reports one water loss and the bounded filth child");
        context.VerifyRecords(5);
        context.Check(!runtime.TryAddWastewater(facility.Building, 25f, out float accepted, out DomainFailure overflow)
                && accepted == 20f && overflow.IsFailure,
            "real overflow rejects the excess and accepts exactly the available capacity");
        context.Observe("fluid/backflow-owner", runtime.Capture());
        context.Check(context.Source(InfrastructureCommandOutcomeKind.WastewaterBackflow).AfterValue
                .Contains("overflow=5", StringComparison.Ordinal) && filth.GetAll().Single().Amount == 42f,
            "overflow source keeps exact backflow and bounded sewage delta");
        context.VerifyRecords(6);
        runtime.TryConsumeWastewater(facility.Building, 100f, out _);
        seasonal.PipedWater = new SeasonalPipedWaterContribution(
            "seasonal:piped-freeze:qa:0001", "seasonal:piped-freeze:qa", "시험 한파", 2, 0.5f, 0f, 2f);
        environment.TemperatureC = -5f;
        clock.Advance(0.1f);
        runtime.Tick();
        context.Observe("fluid/frozen-owner", runtime.Capture());
        context.VerifyRecords(7);
        clock.Advance(0.1f);
        runtime.Tick();
        context.VerifyRecords(7);
        environment.TemperatureC = 3f;
        clock.Advance(0.1f);
        runtime.Tick();
        context.Observe("fluid/recovered-owner", runtime.Capture());
        context.VerifyRecords(8);
        DungeonFluidInfrastructureSaveData quiet = runtime.Capture();
        quiet.nodes.Single().leak = 0f;
        quiet.nodes.Single().wastewater = 0f;
        quiet.nodes.Single().processorWork = 0f;
        seasonal.PipedWater = SeasonalPipedWaterContribution.None;
        runtime.Restore(runtime.PrepareRestore(quiet));
        clock.Advance(0.5f);
        runtime.Tick();
        context.VerifyRecords(8);

        context.Committer.InDoubtNextCommit = true;
        context.Check(!runtime.TryAddWastewater(facility.Building, 25f, out float secondAccepted, out _)
                && secondAccepted == 20f, "second distinct overflow occurs through the same real owner");
        DungeonFluidInfrastructureSaveData fluidPending = runtime.Capture();
        DungeonInfrastructureObservationSaveData queued = context.Outcomes.Capture();
        context.Observe("fluid/queued-owner", fluidPending);
        context.Observe("fluid/queued-publication", queued);
        context.Check(queued.pending.Count == 1 && queued.pending.Single().fact.ownerRevision == 9L
                && queued.nextOwnerRevision == 10L && context.Snapshots().Length == 9
                && fluidPending.nodes.Single().pendingAutonomousOutcomes.Count == 0,
            "unknown acknowledgement keeps the committed parent and actual global typed fact for reconciliation");
        context.Check(filth.GetAll().Single().Amount == 82f, "second distinct backflow applies the bounded child once");
        context.RoundTrip("fluid/queued-ledger-and-owner-restored");
        FluidNetworkRuntime resumed = CreateFluid(facility, items, batch, filth, clock, environment, seasonal, context.Outcomes);
        resumed.Restore(resumed.PrepareRestore(RoundTrip(fluidPending)));
        context.Outcomes.Tick();
        context.Check(context.Outcomes.Capture().pending.Count == 0,
            "saved global typed fact drains through the real observer");
        context.VerifyRecords(9);
        clock.Advance(0.1f);
        resumed.Tick();
        context.Observe("fluid/resumed-owner", resumed.Capture());
        context.Check(items.GetAllStacks().Single(value => value.StackId == sludge.StackId).Quantity == 1
                && filth.GetAll().Single().Amount == 82f,
            "publication retry does not repeat physical sludge or sewage effects");
        context.VerifyRecords(9);
        // Explicitly model durable parent publication followed by an acknowledgement
        // crash: restore the actual saved pending row beside its now committed parent.
        context.RoundTrip("fluid/committed-parent-with-saved-pending-row", queued);
        context.Outcomes.Tick();
        context.Outcomes.Tick();
        context.Check(context.Outcomes.Capture().pending.Count == 0
                && context.Outcomes.Capture().nextOwnerRevision == 10L,
            "restored already-committed delivery reconciles the same result identity without a new revision");
        context.VerifyRecords(9);
    }

    private static ElectricalNetworkRuntime CreatePower(Facility facility, IWorldItemStackRuntime items,
        IPhysicalFacilityItemSinkGateway physical, ProbeClock clock, IInfrastructureFactJournal outcomes)
    {
        DungeonRuntimeAggregateRootStore root = new();
        FixtureBuffers buffers = new();
        return new ElectricalNetworkRuntime(facility.Topology, new FixedGrid(facility.Grid),
            new RestoreWorldCandidateIndex(), clock, items, physical, new AutomationPowerDemandRegistry(root),
            root, buffers, buffers, buffers, NeutralSeasonalEventQuery.Instance, null, outcomes);
    }

    private static FluidNetworkRuntime CreateFluid(Facility facility, IWorldItemStackRuntime items,
        IPhysicalItemBatchDispositionService batch, FixtureFilth filth, ProbeClock clock,
        MutableEnvironment environment, MutableSeasonal seasonal, IInfrastructureFactJournal outcomes) =>
        new(facility.Topology, UnusedPower.Instance, items, batch, filth, clock, environment, seasonal,
            EmptyCapabilities.Instance, NoopStateChange.Instance, new DungeonRuntimeAggregateRootStore(),
            new EditorFluidFacilityInputOwnerAuthority(), outcomes);

    private static T RoundTrip<T>(T value) where T : class => DungeonSaveJson.Deserialize<T>(JsonUtility.ToJson(value));

    private sealed class Context : IDisposable
    {
        private readonly Report report;
        private readonly GameplayOutcomeRegistry registry;
        private readonly FixedCalendar calendar = new();
        private DomainFactDispatcher transitionFacts;
        private IDisposable transitionBinding;
        internal GameplayOutcomeLedger Ledger { get; private set; }
        internal InfrastructureObservationRuntime Outcomes { get; private set; }
        internal ObservingFactPorts Committer { get; private set; }
        internal Context(Report report)
        {
            this.report = report;
            registry = new GameplayOutcomeRegistry(new IGameplayOutcomeDescriptor[]
            {
                new InfrastructureAutonomousOutcomeDescriptor(new KoreanJosaFormatter()),
                new InfrastructureCommandOutcomeDescriptor(new KoreanJosaFormatter())
            }, new IGameplayOutcomeAdapterRegistration[]
            {
                new InfrastructureAutonomousOutcomeAdapter(), new InfrastructureCommandOutcomeAdapter()
            });
            Ledger = NewLedger();
            BindOwner();
        }
        private GameplayOutcomeLedger NewLedger() => new(registry,
            new GameplayOutcomeBufferLimits(smallPageCount: 32, largePageCount: 0, knownResultKeyCapacity: 128),
            new GameplayOutcomeRunId("run:e12-canonical-current"), 1L, calendar);
        private void BindOwner()
        {
            transitionBinding?.Dispose();
            transitionFacts?.Dispose();
            GameplayOutcomeRecorder recorder = new(
                Ledger,
                registry,
                new GameEventBus());
            transitionFacts = new DomainFactDispatcher();
            transitionBinding = new IndustrialTransitionFactObserverBinding(
                registry,
                Ledger,
                recorder,
                new GameplayDomainFactCommitGroup(recorder, Ledger))
                .Connect(transitionFacts);
            Committer = new ObservingFactPorts(
                transitionFacts,
                report.sources);
            Outcomes = new InfrastructureObservationRuntime(
                new DungeonRuntimeAggregateRootStore(),
                Committer,
                Committer,
                calendar);
        }
        internal void Check(bool condition, string label)
        {
            if (!condition) throw new InvalidOperationException("E12 canonical: " + label);
            report.checks.Add(label);
        }
        internal void Observe(string key, object value) => report.observations.Add(new Observation
            { key = key, valueJson = JsonUtility.ToJson(value) });
        internal GameplayOutcomeSnapshot[] Snapshots() => Ledger.GetGlobal(OutcomeCursor.FirstPage(32), OutcomeFilter.All)
            .Items.Select(value => value.Exact ?? throw new InvalidOperationException("Expected exact canonical record."))
            .OrderBy(value => value.ownerRevision).ToArray();
        internal IndustrialTransitionFactData Fact(
            InfrastructureCommandOutcomeKind kind) =>
            report.sources.Single(value =>
                IndustrialTransitionFactObservation.MapKind(value.kind) == kind);
        internal InfrastructureCommandOutcomeSource Source(
            InfrastructureCommandOutcomeKind kind) =>
            IndustrialTransitionFactObservation.CaptureSource(
                new IndustrialTransitionFact(Fact(kind)));
        internal void RoundTrip(
            string key,
            DungeonInfrastructureObservationSaveData ownerOverride = null)
        {
            GameplayOutcomeSnapshot[] before = Snapshots();
            GameplayOutcomeLedgerSaveData ledgerSave = E12CanonicalInfrastructureVerifier.RoundTrip(Ledger.CaptureGameplayOutcomes());
            DungeonInfrastructureObservationSaveData ownerSave = E12CanonicalInfrastructureVerifier.RoundTrip(ownerOverride ?? Outcomes.Capture());
            Observe(key + "/ledger", ledgerSave);
            Observe(key + "/infrastructure-owner", ownerSave);
            Ledger = NewLedger();
            Ledger.BeginRestoreCandidate();
            Ledger.PublishGameplayOutcomeRestore(Ledger.PrepareGameplayOutcomeRestore(ledgerSave));
            Ledger.PublishRestoreCandidate();
            Ledger.CompleteRestoreCandidate();
            BindOwner();
            Outcomes.Restore(Outcomes.PrepareRestore(ownerSave));
            Check(before.Select(Signature).SequenceEqual(Snapshots().Select(Signature)),
                key + ": fresh ledger restore preserves all prior full keys, run/sequence, native facts and hashes");
        }
        internal void VerifyRecords(int count)
        {
            GameplayOutcomeSnapshot[] records = Snapshots();
            report.records = records.ToList();
            report.infrastructureOwner = Outcomes.Capture();
            Check(records.Length == count, "canonical record count=" + count + "; no duplicate publication");
            Check(records.Select(value => value.ownerRevision).SequenceEqual(Enumerable.Range(1, count).Select(value => (long)value))
                    && records.Select(value => value.runId + ":" + value.sequence).Distinct().Count() == count
                    && Outcomes.Capture().nextOwnerRevision == count + 1L
                    && Outcomes.Capture().pending.Count == 0,
                "shared global owner revisions and unique canonical run/sequence at count=" + count);
            foreach (GameplayOutcomeSnapshot record in records)
            {
                IndustrialTransitionFactData fact = report.sources.Single(
                    value => value.ownerRevision == record.ownerRevision);
                InfrastructureCommandOutcomeSource source =
                    IndustrialTransitionFactObservation.CaptureSource(
                        new IndustrialTransitionFact(fact));
                bool autonomous = IndustrialTransitionFact.IsAutonomous(fact.kind);
                GameplayResultKey key = autonomous ? InfrastructureAutonomousOutcomeIds.ResultKey(fact.ownerRevision)
                    : InfrastructureCommandOutcomeIds.ResultKey(fact.ownerRevision);
                string prefix = autonomous ? "infrastructure-autonomous." : "infrastructure-command.";
                Check(record.producerId == key.ProducerId && record.operationId == key.OperationId.Value
                        && record.commitRevision == key.CommitRevision && record.localResultIndex == key.LocalResultIndex
                        && record.outcomeTypeId == (autonomous ? InfrastructureAutonomousOutcomeIds.Transitioned.Value
                            : InfrastructureCommandOutcomeIds.Applied.Value)
                        && record.facts.Single(value => value.factId == prefix + "kind").value
                            == (autonomous ? InfrastructureAutonomousOutcomeIds.StableKind(source.Kind)
                                : InfrastructureCommandOutcomeIds.StableKind(source.Kind))
                        && record.facts.Single(value => value.factId == prefix + "target-id").value == source.TargetId
                        && record.facts.Single(value => value.factId == prefix + "before").value == source.BeforeValue
                        && record.facts.Single(value => value.factId == prefix + "after").value == source.AfterValue
                        && !string.IsNullOrWhiteSpace(record.immutablePayloadHash),
                    "actual native source/full result key/facts/hash match owner revision=" + record.ownerRevision);
            }
        }
        public void Dispose()
        {
            transitionBinding?.Dispose();
            transitionFacts?.Dispose();
        }
        private static string Signature(GameplayOutcomeSnapshot value) => JsonUtility.ToJson(value);
    }

    private sealed class ObservingFactPorts :
        IDomainFactStagingPublisher,
        IDomainFactRecoveryPublisher
    {
        private readonly DomainFactDispatcher inner;
        private readonly List<IndustrialTransitionFactData> observed;
        internal bool RejectNextPrepare;
        internal bool InDoubtNextCommit;
        internal ObservingFactPorts(
            DomainFactDispatcher inner,
            List<IndustrialTransitionFactData> observed)
        {
            this.inner = inner;
            this.observed = observed;
        }
        public bool TryStage<TFact>(
            in TFact fact,
            out IDomainFactStage stage,
            out string failure)
        {
            if (RejectNextPrepare)
            {
                RejectNextPrepare = false;
                stage = null;
                failure = "qa-e12-reject-prepare";
                return false;
            }
            if (!inner.TryStage(fact, out IDomainFactStage prepared, out failure))
            {
                stage = null;
                return false;
            }
            if (fact is not IndustrialTransitionFact transition)
            {
                prepared.Cancel();
                stage = null;
                failure = "qa-e12-industrial-fact-type-mismatch";
                return false;
            }
            Remember(transition.Snapshot());
            stage = new ObservingStage(this, prepared);
            return true;
        }
        public bool ReplayBatch(
            IReadOnlyList<DomainFactReplay> facts,
            DomainFactDelivery[] results,
            out string failure) =>
            inner.ReplayBatch(facts, results, out failure);
        private void Remember(IndustrialTransitionFactData fact)
        {
            IndustrialTransitionFactData prior = observed.SingleOrDefault(
                value => value.ownerRevision == fact.ownerRevision);
            if (prior == null) observed.Add(fact.Clone());
            else if (JsonUtility.ToJson(prior) != JsonUtility.ToJson(fact))
                throw new InvalidOperationException(
                    "Same global infrastructure revision changed its typed fact.");
        }
        private sealed class ObservingStage : IDomainFactStage
        {
            private readonly ObservingFactPorts owner;
            private readonly IDomainFactStage inner;
            internal ObservingStage(
                ObservingFactPorts owner,
                IDomainFactStage inner)
            {
                this.owner = owner;
                this.inner = inner;
            }
            public string OperationKey => inner.OperationKey;
            public long OwnerRevision => inner.OwnerRevision;
            public bool IsReplay => inner.IsReplay;
            public bool IsActive => inner.IsActive;
            public DomainFactDelivery Commit()
            {
                DomainFactDelivery result = inner.Commit();
                if (!owner.InDoubtNextCommit) return result;
                owner.InDoubtNextCommit = false;
                return new DomainFactDelivery(
                    DomainFactDeliveryPhase.InDoubt,
                    result.OperationKey,
                    result.PublicationId,
                    result.Fingerprint,
                    "qa-e12-reject-delivery");
            }
            public void Cancel() => inner.Cancel();
        }
    }

    private sealed class Facility : IDisposable
    {
        private readonly GameObject host;
        internal readonly string Id;
        internal readonly BuildingSO Data;
        internal readonly BuildableObject Building;
        internal readonly Grid Grid = new(16, 8);
        internal readonly FixedTopology Topology;
        internal Facility(string id, string name, BuildingAbilityCollection abilities)
        {
            Id = id;
            Data = ScriptableObject.CreateInstance<BuildingSO>();
            Data.id = 99874; Data.objectName = name; Data.ReplaceAbilities(abilities);
            host = new GameObject("E12 canonical fixture: " + name);
            Building = host.AddComponent<BuildableObject>();
            Building.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
            (typeof(BuildableObject).GetProperty(nameof(BuildableObject.BuildingData))
                ?? throw new InvalidOperationException("Building data setter missing.")).SetValue(Building, Data);
            (typeof(BuildableObject).GetField("facilityCandidateCache", BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("Facility state port missing.")).SetValue(Building, NoopStateChange.Instance);
            Building.RestorePersistentIdentity(new BuildingInstanceId(id));
            Building.SetGrid(Grid); Building.SetRuntimeGridPosition(new Vector2Int(7, 3));
            if (!Grid.RegisterOccupant(Building, Data.Placement.Layer, Building.buildPoses, Building.IsGridMovement))
                throw new InvalidOperationException("Fixture building grid registration failed.");
            Topology = new FixedTopology(IndustrialInfrastructureTopologyBuilder.Build(1, new[] { Building }));
        }
        public void Dispose() { UnityEngine.Object.DestroyImmediate(host); UnityEngine.Object.DestroyImmediate(Data); }
    }
    private sealed class FixedTopology : IIndustrialInfrastructureTopologyRuntime
    {
        internal FixedTopology(IndustrialTopologySnapshot snapshot) => Current = snapshot;
        public IndustrialTopologySnapshot Current { get; }
        public void MarkDirty() { }
    }
    private sealed class FixedGrid : IGridSystemProvider
    {
        internal FixedGrid(Grid grid) => Grid = grid;
        public Grid Grid { get; }
        public GridSystemManager Manager => throw new NotSupportedException("Fixture has no grid manager.");
        public bool TryGetManager(out GridSystemManager manager) { manager = null; return false; }
        public bool TryGetGrid(out Grid grid) { grid = Grid; return true; }
    }
    private sealed class ProbeClock : IGameClock
    {
        public float DeltaTime { get; private set; }
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused => false;
        internal void Advance(float seconds) { DeltaTime = seconds; Time += seconds; FrameCount++; }
    }
    private sealed class FailFirstAcknowledgement : IPhysicalFacilityItemSinkGateway
    {
        private readonly IPhysicalFacilityItemSinkGateway inner;
        private bool rejected;
        internal int SuccessfulAcknowledgements { get; private set; }
        internal FailFirstAcknowledgement(IPhysicalFacilityItemSinkGateway inner) => this.inner = inner;
        public bool TryCommitSinkPending(string destinationId, string itemId, int quantity, string operationId,
            string reasonCode, out PhysicalItemBatchDispositionReceipt receipt, out string failureReason) =>
            inner.TryCommitSinkPending(destinationId, itemId, quantity, operationId, reasonCode, out receipt, out failureReason);
        public bool TryGetPending(string operationId, out PhysicalItemBatchDispositionReceipt receipt) => inner.TryGetPending(operationId, out receipt);
        public bool Acknowledge(string commitId, out string failureReason)
        {
            if (!rejected) { rejected = true; failureReason = "qa-e12-reject-fuel-ack"; return false; }
            bool success = inner.Acknowledge(commitId, out failureReason);
            if (success) SuccessfulAcknowledgements++;
            return success;
        }
    }
    private sealed class FixtureBuffers : IFacilityBufferDestinationLifecycleCommand,
        IFacilityBufferDestinationClaimQuery, IFacilityBufferDestinationReleaseService
    {
        private FacilityBufferDestinationClaim[] claims = Array.Empty<FacilityBufferDestinationClaim>();
        public long Revision => 1L;
        public IReadOnlyList<FacilityBufferDestinationClaim> CaptureClaims() => claims;
        public bool TryGetClaim(string destinationId, Vector2Int dropPosition, out FacilityBufferDestinationClaim claim)
        {
            claim = claims.SingleOrDefault(value => value.DestinationId == destinationId && value.DropPosition == dropPosition);
            return claim != null;
        }
        public bool TryReplaceOwnedAuthorities(string ownerDomain, IReadOnlyList<FacilityBufferDestinationClaim> desiredClaims,
            IReadOnlyList<FacilityBufferCapacityProfile> profiles, out string failureReason)
        { claims = desiredClaims.ToArray(); failureReason = string.Empty; return true; }
        public bool TryReleaseAtOwnerPosition(string destinationId, Vector2Int ownerPosition, string reasonCode,
            out int releasedQuantity, out string failureReason) => throw new NotSupportedException("Terminal release is not exercised by this canonical fixture.");
    }
    private sealed class MutableEnvironment : IEnvironmentalFieldQuery
    {
        private readonly Vector2Int position;
        internal MutableEnvironment(Vector2Int position) => this.position = position;
        internal float TemperatureC = 5f;
        public int Version => 1;
        public bool IsInitialized => true;
        public bool TryGetCell(Vector2Int requested, out EnvironmentalCellSnapshot snapshot)
        { snapshot = new EnvironmentalCellSnapshot(requested, TemperatureC, 1f, 1f); return requested == position; }
        public bool TryGetAverage(IReadOnlyList<Vector2Int> positions, out EnvironmentalCellSnapshot snapshot) => TryGetCell(position, out snapshot);
        public float GetFoodSpoilageMultiplier(Vector2Int requested) => 1f;
        public bool IsOrganPreservationSafe(Vector2Int requested) => true;
        public bool TryGetTargetTemperature(Vector2Int requested, out float targetTemperatureC)
        { targetTemperatureC = TemperatureC; return requested == position; }
    }
    private sealed class MutableSeasonal : ISeasonalEventQuery
    {
        internal SeasonalPipedWaterContribution PipedWater = SeasonalPipedWaterContribution.None;
        public IReadOnlyList<V20ActiveEventSaveData> ActiveSeasonalEvents => Array.Empty<V20ActiveEventSaveData>();
        public float GetWorldWaterRegenerationMultiplier() => 1f;
        public SeasonalPowerCapacityContribution GetPowerCapacityContribution() => SeasonalPowerCapacityContribution.None;
        public SeasonalPipedWaterContribution GetPipedWaterContribution() => PipedWater;
    }
    private sealed class FixtureFilth : IWorldFilthQuery
    {
        private readonly List<WorldFilthSnapshot> values = new();
        public int StateVersion { get; private set; }
        public int NextFilthSequence => values.Count + 1;
        public IReadOnlyList<WorldFilthSnapshot> GetAll() => values.ToArray();
        public IReadOnlyList<WorldFilthSnapshot> GetAt(Vector2Int position) => values.Where(value => value.Position == position).ToArray();
        public WorldFilthSnapshot AddFilth(WorldFilthType type, Vector2Int position, float amount,
            string sourceCharacterId, float infectionRisk, bool wallStain = false)
        {
            int index = values.FindIndex(value => value.Type == type && value.Position == position
                && value.WallStain == wallStain && value.SourceCharacterId == (sourceCharacterId ?? string.Empty));
            WorldFilthSnapshot value = new(index < 0 ? "filth:qa:e12:" + NextFilthSequence : values[index].FilthId,
                type, Mathf.Min(100f, (index < 0 ? 0f : values[index].Amount) + Mathf.Max(0.1f, amount)),
                position, sourceCharacterId, infectionRisk, wallStain);
            if (index < 0) values.Add(value); else values[index] = value;
            StateVersion++; return value;
        }
        public bool Clean(string filthId, float workAmount, out float remainingAmount) => throw new NotSupportedException();
        public float GetCleanlinessPenalty(Vector2Int position, int radius = 0) => throw new NotSupportedException();
        public List<WorldFilthSaveData> CaptureFilth() => throw new NotSupportedException("Full filth save is outside this fixture.");
        public void RestoreFilth(IEnumerable<WorldFilthSaveData> saveData, int next) => throw new NotSupportedException();
    }
    private sealed class UnusedPower : IPowerInfrastructureQuery
    {
        internal static readonly UnusedPower Instance = new();
        public int Version => 0;
        public IReadOnlyList<PowerNetworkSnapshot> Networks => Array.Empty<PowerNetworkSnapshot>();
        public bool IsPowered(BuildableObject building) => throw new NotSupportedException("Processor explicitly requires no power.");
        public bool TryGetNode(BuildableObject building, out PowerNodeSnapshot snapshot) => throw new NotSupportedException();
    }
    private sealed class EmptyCapabilities : IFacilityCapabilityQuery
    {
        internal static readonly EmptyCapabilities Instance = new();
        public IReadOnlyList<BuildableObject> FindOperational(FacilityCapabilityKind capability, string buildingDefinitionId = "") => Array.Empty<BuildableObject>();
        public IReadOnlyList<BuildableObject> FindOperational(ResearchFacilityCommandKind command) => Array.Empty<BuildableObject>();
    }
    private sealed class NoopStateChange : IBuildingFacilityStateChangePort
    {
        internal static readonly NoopStateChange Instance = new();
        public void MarkDynamicStateDirty() { }
    }
    private sealed class FixedCalendar : IGameCalendar
    {
        public int Day => 17;
        public int Hour => 8;
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour;
        public float ElapsedSeconds => 0f;
        public TimeOfDay TimeOfDay => TimeOfDay.Morning;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) => GameCalendarRules.ProjectRegional(Day, Hour, utcOffsetHours);
        public void Start() => IsRunning = true;
        public void SetDateTime(int day, int hour) => throw new NotSupportedException();
    }
    [Serializable] private sealed class Observation { public string key; public string valueJson; }
    [Serializable] private sealed class Report
    {
        public string schema = "dungeonstory-e12-actual-canonical-integration@1";
        public string scope = "Controlled direct-runtime actual Electrical/Fluid producers -> real InfrastructureObservationRuntime -> generic fact dispatcher/typed observer/recorder/ledger. Fuel and sludge use the real physical repository. Weather, filth, buffer lifecycle and unrelated input authority are bounded support fixtures. Fresh owner/ledger JSON restore is same-process scoped, not full-world/cold/natural telemetry.";
        public bool passed;
        public string failure = string.Empty;
        public bool trainingEligible;
        public bool humanApprovalClaimed;
        public bool naturalGameplayClaimed;
        public bool fullGoalComplete;
        public List<string> checks = new();
        public List<string> errors = new();
        public List<string> warnings = new();
        public List<IndustrialTransitionFactData> sources = new();
        public List<GameplayOutcomeSnapshot> records = new();
        public DungeonInfrastructureObservationSaveData infrastructureOwner;
        public GameplayOutcomeLedgerSaveData ledger;
        public List<Observation> observations = new();
    }
}
#endif
