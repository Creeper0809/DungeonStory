#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using DungeonStory.Foundation;
using DungeonStory.Foundation.Facts;
using UnityEngine;

// Only the death/taboo event routes are executed. Unused domains fail loudly;
// physical items, blood/water state, deprivation and their restore ports are real.
public static class TabooButcheryCausalTestPorts
{
    internal static CharacterDeprivationRuntime Create(
        Grid grid, WorldItemStackRuntime items, CharacterBodyHealthRuntime health,
        DungeonRuntimeAggregateRootStore roots, IGameEventBus events, IGameClock clock,
        IGameCalendar calendar, IDomainFactPublisher primitiveFacts,
        IDomainFactPublisher notifications, out WorldFilthRuntime filth,
        out WorldWaterRuntime water)
    {
        var content = new ResourceGameContentCatalog(new UnityGameContentRootLoader());
        var registry = CharacterAiEditorTestDependencies.WorldRegistry;
        var gridProvider = new SuppliedGrid(grid);
        var rooms = new RoomLayoutCache();
        var facilityCache = Dependency<IFacilityCandidateCache>("FacilityCandidates");
        var zones = Guard<IExteriorZoneQuery>((method, args) => method.Name == "get_Zones"
            ? Array.Empty<ExteriorZoneMarker>() : throw Unsupported(method));
        filth = new WorldFilthRuntime(
            new WorldFilthSpatialDependencies(gridProvider, zones,
                (IBuildingFacilityStateChangePort)facilityCache, new RoomFacilityPolicyService(rooms)),
            new WorldFilthGameplayDependencies(
                new BuildingResearchWorkPortAdapter(Dependency<IBlueprintResearchWorkService>("BlueprintResearch")),
                () => Guard<IBuildingEquipmentCraftingRuntimePort>(),
                (IBuildingWorldRegistryPort)registry, Guard<IBuildingItemStackPort>(),
                () => CharacterAiEditorTestDependencies.BuildingAbilityRuntimeDispatcher,
                new FacilityEvolutionStateComponentFactory()),
            new WorldFilthRuntimeDependencies(Dependency<IPaidFacilityContractRuntime>("PaidFacilities"),
                clock, new RuntimeBuildingArchetypeCatalog(content), content), roots);
        water = new WorldWaterRuntime(gridProvider, clock, Guard<ISeasonalEventQuery>(),
            content, roots, Guard<IDiseaseDefinitionCatalog>());
        var primitive = new CharacterPrimitiveSurvivalDependencies(
            Guard<IFieldMealConsumptionCommand>(), Guard<IItemQuantityReservationService>(),
            Guard<IReservedItemTransferService>(), calendar, primitiveFacts);
        var runtime = new CharacterDeprivationRuntime(
            new CharacterDeprivationWorldDependencies(gridProvider, items, filth, water, rooms,
                registry, registry, facilityCache, Guard<ISurvivalFoodQuery>(),
                Dependency<IEnvironmentWorkPolicy>("EnvironmentWork")),
            new CharacterDeprivationSystemDependencies(events, clock,
                new DynamicFrameWorkBudget(clock, new UnityUiClock()), new RandomStreamProvider(104729),
                new UnityUiClock(), Guard<IDoorAccessQuery>(), DefaultCharacterNeedBalanceRuntime.Instance,
                DisabledDungeonDebugRuleQuery.Instance, Guard<IHeritableTraitEffectQuery>(), Guard<IReproductionService>()),
            new CharacterDeprivationAuthorityDependencies(new ResourceItemDefinitionCatalog(content),
                new CharacterDeprivationStateStore(roots), health,
                CharacterAiEditorTestDependencies.NeutralPerformance, primitive,
                notifications: notifications));
        runtime.Initialize();
        return runtime;
    }

    internal static T Dependency<T>(string name) => (T)(typeof(CharacterAiEditorTestDependencies)
        .GetField(name, BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(null)
        ?? throw new InvalidOperationException("Missing existing Editor composition dependency: " + name));

    internal static T Guard<T>(Func<MethodInfo, object[], object> handler = null) where T : class
    {
        T result = DispatchProxy.Create<T, RejectedDomain>();
        ((RejectedDomain)(object)result).Handler = handler;
        return result;
    }

    public class RejectedDomain : DispatchProxy
    {
        internal Func<MethodInfo, object[], object> Handler;
        protected override object Invoke(MethodInfo method, object[] args) =>
            Handler != null ? Handler(method, args) : throw Unsupported(method);
    }

    private static Exception Unsupported(MethodInfo method) => new InvalidOperationException(
        "Taboo owner attempted an uncomposed domain: " + method.DeclaringType?.Name + "." + method.Name);

    internal sealed class SuppliedGrid : IGridSystemProvider
    {
        private readonly Grid grid;
        internal SuppliedGrid(Grid grid) => this.grid = grid;
        public Grid Grid => grid;
        public GridSystemManager Manager => throw new InvalidOperationException("Scene manager is outside this owner.");
        public bool TryGetGrid(out Grid value) { value = grid; return value != null; }
        public bool TryGetManager(out GridSystemManager value) => throw new InvalidOperationException("Scene manager is outside this owner.");
    }
}
#endif
