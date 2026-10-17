#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using DungeonStory.Narrative.Korean;
using UnityEngine;

public static class FluidAutonomousOutcomeContractProbe
{
    private const string NodeId = "building:qa:fluid-autonomous";

    public static string Run(Action<string, string> observe = null)
    {
        BuildingSO data = CreateData();
        GameObject host = new("Fluid Autonomous Outcome Fixture");
        try
        {
            BuildableObject building = CreateBuilding(host, data);
            IndustrialInfrastructureTopologyRuntime topology = new(
                new BuildingWorldQuery(building));
            ProbeClock clock = new();
            MutableEnvironment environment = new(building.centerPos, 5f);
            MutableSeasonal seasonal = new();
            DungeonRuntimeAggregateRootStore root = new();
            using IndustrialTransitionObservationFixture outcomes = new(
                root,
                clock,
                "run:fluid-autonomous-probe",
                observe);
            ItemRuntimeProxy itemProxy = DispatchProxy.Create<
                IWorldItemStackRuntime,
                ItemRuntimeProxy>() as ItemRuntimeProxy;
            IWorldItemStackRuntime items = (IWorldItemStackRuntime)(object)itemProxy;
            FilthQuery filth = new();
            FluidNetworkRuntime runtime = new(
                topology,
                CreateNullProxy<IPowerInfrastructureQuery>(),
                items,
                CreateNullProxy<IPhysicalItemBatchDispositionService>(),
                filth,
                clock,
                environment,
                seasonal,
                EmptyFacilityCapabilityQuery.Instance,
                NoopFacilityStateChange.Instance,
                root,
                new EditorFluidFacilityInputOwnerAuthority(),
                outcomes);

            Require(runtime.TryAdd(
                    building,
                    WorldWaterQuality.Clean,
                    10f,
                    out float seededWater)
                && Mathf.Approximately(seededWater, 10f),
                "fluid autonomous probe could not seed clean water");
            Require(runtime.TryAddWastewater(
                    building,
                    2f,
                    out float seededWaste,
                    out DomainFailure seedFailure)
                && !seedFailure.IsFailure
                && Mathf.Approximately(seededWaste, 2f),
                "fluid autonomous probe could not seed wastewater");

            DungeonFluidInfrastructureSaveData initial = runtime.Capture();
            FluidNodeSaveData node = initial.nodes.Single(value =>
                value.buildingInstanceId == NodeId);
            node.leak = 100f;
            node.processorWork = 0f;
            runtime.Restore(runtime.PrepareRestore(initial));
            observe?.Invoke("prepared-owner", JsonUtility.ToJson(runtime.Capture()));

            clock.Advance(10f);
            runtime.Tick();
            Require(outcomes.Count(
                        InfrastructureCommandOutcomeKind
                            .WastewaterBatchCompleted) == 1
                    && outcomes.Count(
                        InfrastructureCommandOutcomeKind.FluidLeakEpisode) == 1,
                "wastewater batch or canonical leak episode was not emitted exactly once");
            InfrastructureCommandOutcomeSource batch = outcomes.Single(
                InfrastructureCommandOutcomeKind.WastewaterBatchCompleted);
            Require(batch.AfterValue.Contains("inputUsed=2", StringComparison.Ordinal)
                    && batch.AfterValue.Contains("waterProduced=1", StringComparison.Ordinal)
                    && batch.AfterValue.Contains("sludgeQuantity=1", StringComparison.Ordinal)
                    && batch.AfterValue.Contains("sludgeStacks=item-stack:fluid-sludge:0001:1", StringComparison.Ordinal),
                "wastewater outcome lost exact conservation or sludge stack identity");
            InfrastructureCommandOutcomeSource leak = outcomes.Single(
                InfrastructureCommandOutcomeKind.FluidLeakEpisode);
            Require(leak.AfterValue.Contains("totalLoss=1", StringComparison.Ordinal)
                    && leak.AfterValue.Contains("filthIds=filth:00000001", StringComparison.Ordinal),
                "leak episode lost exact water loss or sewage child identity");
            observe?.Invoke("after-batch-owner", JsonUtility.ToJson(runtime.Capture()));

            int backflowBefore = outcomes.Count(
                InfrastructureCommandOutcomeKind.WastewaterBackflow);
            Require(!runtime.TryAddWastewater(
                    building,
                    25f,
                    out float accepted,
                    out DomainFailure overflowFailure)
                    && overflowFailure.IsFailure
                    && Mathf.Approximately(accepted, 20f),
                "fluid autonomous probe did not enter exact overflow backflow");
            Require(outcomes.Count(
                        InfrastructureCommandOutcomeKind.WastewaterBackflow)
                    == backflowBefore + 1,
                "backflow threshold did not emit exactly once");
            InfrastructureCommandOutcomeSource backflow = outcomes.Last(
                InfrastructureCommandOutcomeKind.WastewaterBackflow);
            Require(backflow.AfterValue.Contains("overflow=5", StringComparison.Ordinal)
                    && backflow.AfterValue.Contains("sewageFilth=filth:00000001", StringComparison.Ordinal)
                    && backflow.AfterValue.Contains("blockage=10", StringComparison.Ordinal),
                "backflow outcome lost overflow, filth, or blockage delta");
            observe?.Invoke("after-backflow-owner", JsonUtility.ToJson(runtime.Capture()));

            runtime.TryConsumeWastewater(building, 100f, out _);
            seasonal.PipedWater = new SeasonalPipedWaterContribution(
                "seasonal:piped-freeze:qa:0001",
                "seasonal:piped-freeze:qa",
                "시험 한파",
                2,
                0.5f,
                0f,
                2f);
            environment.TemperatureC = -5f;
            clock.Advance(0.1f);
            runtime.Tick();
            Require(outcomes.Count(
                        InfrastructureCommandOutcomeKind.PipeFreezeLatched) == 1,
                "freeze threshold crossing did not latch exactly once");
            observe?.Invoke("frozen-owner", JsonUtility.ToJson(runtime.Capture()));
            clock.Advance(0.1f);
            runtime.Tick();
            Require(outcomes.Count(
                        InfrastructureCommandOutcomeKind.PipeFreezeLatched) == 1,
                "steady frozen tick duplicated the latch outcome");
            environment.TemperatureC = 3f;
            clock.Advance(0.1f);
            runtime.Tick();
            Require(outcomes.Count(
                        InfrastructureCommandOutcomeKind.PipeFreezeRecovered) == 1,
                "freeze hysteresis recovery did not emit exactly once");

            DungeonFluidInfrastructureSaveData quiet = runtime.Capture();
            observe?.Invoke("recovered-owner", JsonUtility.ToJson(quiet));
            FluidNodeSaveData quietNode = quiet.nodes.Single(value =>
                value.buildingInstanceId == NodeId);
            quietNode.leak = 0f;
            quietNode.wastewater = 0f;
            quietNode.processorWork = 0f;
            seasonal.PipedWater = SeasonalPipedWaterContribution.None;
            runtime.Restore(runtime.PrepareRestore(quiet));
            int autonomousBeforeQuietTick = outcomes.Facts.Count;
            clock.Advance(0.5f);
            runtime.Tick();
            Require(outcomes.Facts.Count == autonomousBeforeQuietTick,
                "normal fluid flow tick incorrectly emitted a gameplay result");
            observe?.Invoke("quiet-owner", JsonUtility.ToJson(runtime.Capture()));

            outcomes.RejectPrepare = true;
            Require(!runtime.TryAddWastewater(
                    building,
                    25f,
                    out _,
                    out _),
                "pending-outbox fixture did not create backflow");
            DungeonFluidInfrastructureSaveData pendingSave = runtime.Capture();
            Require(pendingSave.nodes.Single(value =>
                        value.buildingInstanceId == NodeId)
                    .pendingAutonomousOutcomes.Count == 1,
                "failed gameplay prepare was hidden instead of saved locally");
            observe?.Invoke("pending-owner", JsonUtility.ToJson(pendingSave));
            string pendingJson = JsonUtility.ToJson(pendingSave);
            DungeonFluidInfrastructureSaveData pendingRoundTrip =
                JsonUtility.FromJson<DungeonFluidInfrastructureSaveData>(
                    pendingJson);
            runtime.Restore(runtime.PrepareRestore(pendingRoundTrip));
            outcomes.RejectPrepare = false;
            int deliveredBefore = outcomes.Facts.Count;
            clock.Advance(0.1f);
            runtime.Tick();
            DungeonFluidInfrastructureSaveData resumedSave = runtime.Capture();
            Require(resumedSave.nodes.Single(value =>
                        value.buildingInstanceId == NodeId)
                    .pendingAutonomousOutcomes.Count == 0
                    && outcomes.Facts.Count == deliveredBefore + 1,
                "saved fluid autonomous outcome did not resume exactly once");
            observe?.Invoke("resumed-owner", JsonUtility.ToJson(resumedSave));

            return "wastewater=1;leak-quantum=1;backflow=1;freeze=1;"
                + "recovery=1;quiet-tick=0;save-resume=1";
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(host);
            UnityEngine.Object.DestroyImmediate(data);
        }
    }

    private static BuildingSO CreateData()
    {
        BuildingSO data = ScriptableObject.CreateInstance<BuildingSO>();
        data.id = 99872;
        data.objectName = "자율 유체 시험 설비";
        BuildingAbilityCollection abilities = new();
        abilities.Add(new BuildingUtilityConnectionAbility
        {
            channels = UtilityChannel.CleanWater | UtilityChannel.Wastewater
        });
        abilities.Add(new BuildingWaterStorageAbility
        {
            channels = UtilityChannel.CleanWater | UtilityChannel.Wastewater,
            cleanWaterCapacity = 20f,
            wastewaterCapacity = 20f
        });
        abilities.Add(new BuildingWastewaterProcessorAbility
        {
            wastewaterInput = 2f,
            waterOutput = 1f,
            outputQuality = WorldWaterQuality.Clean,
            requiresPower = false,
            sludgeItemId = "industrial:sludge",
            sludgeAmount = 1,
            secondsPerBatch = 5f
        });
        data.ReplaceAbilities(abilities);
        return data;
    }

    private static BuildableObject CreateBuilding(
        GameObject host,
        BuildingSO data)
    {
        BuildableObject building = host.AddComponent<BuildableObject>();
        building.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
        typeof(BuildableObject).GetProperty(nameof(BuildableObject.BuildingData))
            ?.SetValue(building, data);
        typeof(BuildableObject).GetProperty(nameof(BuildableObject.centerPos))
            ?.SetValue(building, new Vector2Int(4, 4));
        building.RestorePersistentIdentity(new BuildingInstanceId(NodeId));
        return building;
    }

    private static T CreateNullProxy<T>() where T : class =>
        DispatchProxy.Create<T, NullDispatchProxy>();

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class BuildingWorldQuery : IBuildingWorldQuery
    {
        internal BuildingWorldQuery(params BuildableObject[] buildings) =>
            Buildings = buildings;
        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings { get; }
    }

    private sealed class ProbeClock : IGameClock, IGameCalendar
    {
        public float DeltaTime { get; private set; }
        public float Time { get; private set; }
        public int FrameCount { get; private set; }
        public bool IsPaused => false;
        public int Day => 1;
        public int Hour => 0;
        public int Year => Current.Year;
        public int DayOfYear => Current.DayOfYear;
        public Season Season => Current.Season;
        public int DayOfSeason => Current.DayOfSeason;
        public long AbsoluteHour => Current.AbsoluteHour;
        public float ElapsedSeconds => Time;
        public TimeOfDay TimeOfDay => TimeOfDay.Night;
        public bool IsRunning { get; private set; }
        public CalendarDateTime Current => GameCalendarRules.Project(Day, Hour);
        public CalendarDateTime GetRegionalTime(int utcOffsetHours) =>
            GameCalendarRules.ProjectRegional(Day, Hour, utcOffsetHours);
        public void Start() => IsRunning = true;
        public void SetDateTime(int day, int hour) =>
            throw new NotSupportedException();
        internal void Advance(float seconds)
        {
            DeltaTime = seconds;
            Time += seconds;
            FrameCount++;
        }
    }

    private sealed class MutableEnvironment : IEnvironmentalFieldQuery
    {
        private readonly Vector2Int position;
        internal MutableEnvironment(Vector2Int position, float temperatureC)
        {
            this.position = position;
            TemperatureC = temperatureC;
        }
        internal float TemperatureC { get; set; }
        public int Version => 1;
        public bool IsInitialized => true;
        public bool TryGetCell(
            Vector2Int requested,
            out EnvironmentalCellSnapshot snapshot)
        {
            snapshot = new EnvironmentalCellSnapshot(
                requested,
                TemperatureC,
                1f,
                1f);
            return requested == position;
        }
        public bool TryGetAverage(
            IReadOnlyList<Vector2Int> positions,
            out EnvironmentalCellSnapshot snapshot) =>
            TryGetCell(position, out snapshot);
        public float GetFoodSpoilageMultiplier(Vector2Int requested) => 1f;
        public bool IsOrganPreservationSafe(Vector2Int requested) => true;
        public bool TryGetTargetTemperature(
            Vector2Int requested,
            out float targetTemperatureC)
        {
            targetTemperatureC = TemperatureC;
            return true;
        }
    }

    private sealed class MutableSeasonal : ISeasonalEventQuery
    {
        internal SeasonalPipedWaterContribution PipedWater { get; set; } =
            SeasonalPipedWaterContribution.None;
        public IReadOnlyList<V20ActiveEventSaveData> ActiveSeasonalEvents =>
            Array.Empty<V20ActiveEventSaveData>();
        public float GetWorldWaterRegenerationMultiplier() => 1f;
        public SeasonalPowerCapacityContribution GetPowerCapacityContribution() =>
            SeasonalPowerCapacityContribution.None;
        public SeasonalPipedWaterContribution GetPipedWaterContribution() =>
            PipedWater;
    }

    private sealed class EmptyFacilityCapabilityQuery : IFacilityCapabilityQuery
    {
        internal static readonly EmptyFacilityCapabilityQuery Instance = new();
        public IReadOnlyList<BuildableObject> FindOperational(
            FacilityCapabilityKind capability,
            string buildingDefinitionId = "") => Array.Empty<BuildableObject>();
        public IReadOnlyList<BuildableObject> FindOperational(
            ResearchFacilityCommandKind command) => Array.Empty<BuildableObject>();
    }

    private sealed class NoopFacilityStateChange :
        IBuildingFacilityStateChangePort
    {
        internal static readonly NoopFacilityStateChange Instance = new();
        public void MarkDynamicStateDirty()
        {
        }
    }

    private sealed class FilthQuery : IWorldFilthQuery
    {
        private readonly List<WorldFilthSnapshot> values = new();
        private int nextSequence = 1;
        public int StateVersion { get; private set; }
        public int NextFilthSequence => nextSequence;
        public IReadOnlyList<WorldFilthSnapshot> GetAll() => values.ToArray();
        public IReadOnlyList<WorldFilthSnapshot> GetAt(Vector2Int position) =>
            values.Where(value => value.Position == position).ToArray();
        public WorldFilthSnapshot AddFilth(
            WorldFilthType type,
            Vector2Int position,
            float amount,
            string sourceCharacterId,
            float infectionRisk,
            bool wallStain = false)
        {
            int index = values.FindIndex(value => value.Type == type
                && value.Position == position
                && value.WallStain == wallStain
                && value.SourceCharacterId == (sourceCharacterId ?? string.Empty));
            float safe = Mathf.Max(0.1f, amount);
            string id = index >= 0
                ? values[index].FilthId
                : $"filth:{nextSequence++:D8}";
            float prior = index >= 0 ? values[index].Amount : 0f;
            WorldFilthSnapshot snapshot = new(
                id,
                type,
                Mathf.Min(100f, prior + safe),
                position,
                sourceCharacterId,
                infectionRisk,
                wallStain);
            if (index >= 0) values[index] = snapshot;
            else values.Add(snapshot);
            StateVersion++;
            return snapshot;
        }
        public bool Clean(string filthId, float workAmount, out float remainingAmount)
        {
            remainingAmount = 0f;
            return false;
        }
        public float GetCleanlinessPenalty(Vector2Int position, int radius = 0) => 0f;
        public List<WorldFilthSaveData> CaptureFilth() => new();
        public void RestoreFilth(IEnumerable<WorldFilthSaveData> saveData, int next)
        {
            nextSequence = next;
        }
    }

    public class ItemRuntimeProxy : DispatchProxy
    {
        private readonly List<WorldItemStackSnapshot> stacks = new();
        protected override object Invoke(MethodInfo method, object[] arguments)
        {
            if (method.Name == nameof(IWorldItemStackRuntime.SpawnItemAt)
                && arguments.Length == 6)
            {
                string itemId = (string)arguments[0];
                int amount = (int)arguments[1];
                Vector2Int position = (Vector2Int)arguments[2];
                WorldItemStackSnapshot stack = stacks.FirstOrDefault(value =>
                    value.ItemId == itemId && value.Position == position);
                if (stack == null)
                {
                    stack = new WorldItemStackSnapshot
                    {
                        StackId = "item-stack:fluid-sludge:0001",
                        ItemId = itemId,
                        Quantity = 0,
                        Position = position,
                        State = (WorldItemStackState)arguments[3],
                        DestinationId = (string)arguments[4]
                    };
                    stacks.Add(stack);
                }
                stack.Quantity += amount;
                arguments[5] = amount;
                return true;
            }
            if (method.Name == nameof(IWorldItemStackRuntime.GetStacksAt))
            {
                Vector2Int position = (Vector2Int)arguments[0];
                return stacks.Where(value => value.Position == position)
                    .ToArray();
            }
            if (method.Name == nameof(IWorldItemStackRuntime.GetAllStacks))
                return stacks.ToArray();
            return NullDispatchProxy.Default(method, arguments);
        }
    }

    public class NullDispatchProxy : DispatchProxy
    {
        protected override object Invoke(MethodInfo method, object[] arguments) =>
            Default(method, arguments);

        internal static object Default(MethodInfo method, object[] arguments)
        {
            ParameterInfo[] parameters = method.GetParameters();
            for (int index = 0; index < parameters.Length; index++)
            {
                Type parameterType = parameters[index].ParameterType;
                if (!parameterType.IsByRef) continue;
                Type element = parameterType.GetElementType();
                arguments[index] = element.IsValueType
                    ? Activator.CreateInstance(element)
                    : null;
            }
            Type returnType = method.ReturnType;
            return returnType == typeof(void)
                ? null
                : returnType.IsValueType
                    ? Activator.CreateInstance(returnType)
                    : null;
        }
    }

}

internal sealed class IndustrialTransitionObservationFixture :
    IInfrastructureFactJournal,
    IInfrastructureObservationPersistence,
    IDisposable
{
    private readonly Action<string, string> observe;
    private readonly DomainFactDispatcher dispatcher;
    private readonly IDisposable binding;
    private readonly InfrastructureObservationRuntime runtime;
    private readonly Dictionary<IDomainFactStage, IndustrialTransitionFactData>
        preparedFacts = new();
    private readonly Dictionary<long, IndustrialTransitionFactData> facts = new();

    internal IndustrialTransitionObservationFixture(
        DungeonRuntimeAggregateRootStore root,
        IGameCalendar calendar,
        string runId,
        Action<string, string> observe = null)
    {
        this.observe = observe;
        GameplayOutcomeRegistry registry = new(
            new IGameplayOutcomeDescriptor[]
            {
                new InfrastructureCommandOutcomeDescriptor(
                    new KoreanJosaFormatter()),
                new InfrastructureAutonomousOutcomeDescriptor(
                    new KoreanJosaFormatter())
            },
            new IGameplayOutcomeAdapterRegistration[]
            {
                new InfrastructureCommandOutcomeAdapter(),
                new InfrastructureAutonomousOutcomeAdapter()
            });
        GameplayOutcomeLedger ledger = new(
            registry,
            new GameplayOutcomeBufferLimits(
                smallPageCount: 32,
                largePageCount: 0,
                knownResultKeyCapacity: 128),
            new GameplayOutcomeRunId(runId),
            1L,
            calendar);
        GameplayOutcomeRecorder recorder = new(
            ledger,
            registry,
            new GameEventBus());
        dispatcher = new DomainFactDispatcher();
        binding = new IndustrialTransitionFactObserverBinding(
            registry,
            ledger,
            recorder,
            new GameplayDomainFactCommitGroup(recorder, ledger))
            .Connect(dispatcher);
        runtime = new InfrastructureObservationRuntime(
            root,
            dispatcher,
            dispatcher,
            calendar);
    }

    internal bool RejectPrepare { get; set; }
    internal bool RejectNextPrepare
    {
        get => RejectPrepare;
        set => RejectPrepare = value;
    }
    internal IReadOnlyList<IndustrialTransitionFactData> Facts =>
        facts.OrderBy(value => value.Key)
            .Select(value => value.Value.Clone())
            .ToArray();
    internal int Count(InfrastructureCommandOutcomeKind kind) =>
        facts.Values.Count(value =>
            IndustrialTransitionFactObservation.MapKind(value.kind) == kind);
    internal InfrastructureCommandOutcomeSource Single(
        InfrastructureCommandOutcomeKind kind) =>
        Source(facts.Values.Single(value =>
            IndustrialTransitionFactObservation.MapKind(value.kind) == kind));
    internal InfrastructureCommandOutcomeSource Last(
        InfrastructureCommandOutcomeKind kind) =>
        Source(facts.Values.Last(value =>
            IndustrialTransitionFactObservation.MapKind(value.kind) == kind));

    public IndustrialTransitionFactData CaptureDraft(
        IndustrialTransitionKind kind,
        string targetId,
        string facilityId,
        string facilityDisplayName,
        int x,
        int y,
        IndustrialStateData before,
        IndustrialStateData after,
        IndustrialTransitionDetails details = null) =>
        runtime.CaptureDraft(
            kind,
            targetId,
            facilityId,
            facilityDisplayName,
            x,
            y,
            before,
            after,
            details);

    public bool TryPrepare(
        IndustrialTransitionFactData draft,
        out IDomainFactStage prepared,
        out string failure)
    {
        if (RejectPrepare)
        {
            RejectPrepare = false;
            prepared = null;
            failure = "qa-injected-prepare-rejection";
            return false;
        }
        if (!runtime.TryPrepare(draft, out prepared, out failure))
            return false;
        IndustrialTransitionFactData sequenced = draft.Clone();
        sequenced.ownerRevision = prepared.OwnerRevision;
        preparedFacts.Add(prepared, sequenced);
        return true;
    }

    public DomainFactDelivery CommitReversible(IDomainFactStage prepared)
    {
        DomainFactDelivery delivery = runtime.CommitReversible(prepared);
        if (delivery.MayBeCommitted) Remember(prepared);
        preparedFacts.Remove(prepared);
        return delivery;
    }

    public void QueueCommitted(IDomainFactStage prepared)
    {
        runtime.QueueCommitted(prepared);
        Remember(prepared);
    }

    public void DeliverQueued(
        long ownerRevision,
        IDomainFactStage prepared = null)
    {
        runtime.DeliverQueued(ownerRevision, prepared);
        if (prepared != null) preparedFacts.Remove(prepared);
    }

    public bool IsPending(long ownerRevision) => runtime.IsPending(ownerRevision);

    public void Cancel(IDomainFactStage prepared)
    {
        runtime.Cancel(prepared);
        if (prepared != null) preparedFacts.Remove(prepared);
    }

    public void AssertIdle() => runtime.AssertIdle();
    public DungeonInfrastructureObservationSaveData Capture() => runtime.Capture();
    public InfrastructureObservationRestoreCandidate PrepareRestore(
        DungeonInfrastructureObservationSaveData snapshot) =>
        runtime.PrepareRestore(snapshot);
    public void Restore(InfrastructureObservationRestoreCandidate candidate) =>
        runtime.Restore(candidate);

    private void Remember(IDomainFactStage prepared)
    {
        if (prepared == null
            || !preparedFacts.TryGetValue(prepared, out IndustrialTransitionFactData fact))
            return;
        if (facts.TryGetValue(fact.ownerRevision, out IndustrialTransitionFactData prior))
        {
            if (JsonUtility.ToJson(prior) != JsonUtility.ToJson(fact))
            {
                throw new InvalidOperationException(
                    "Same industrial owner revision changed its typed fact.");
            }
            return;
        }
        facts.Add(fact.ownerRevision, fact.Clone());
        observe?.Invoke(
            "outcome/" + fact.ownerRevision.ToString(
                System.Globalization.CultureInfo.InvariantCulture),
            JsonUtility.ToJson(fact));
    }

    private static InfrastructureCommandOutcomeSource Source(
        IndustrialTransitionFactData data) =>
        IndustrialTransitionFactObservation.CaptureSource(
            new IndustrialTransitionFact(data));

    public void Dispose()
    {
        binding.Dispose();
        dispatcher.Dispose();
    }
}
#endif
