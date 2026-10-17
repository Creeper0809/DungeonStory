#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEngine;

// Main-owned focused witness: real fluid topology/state and read selection,
// controlled immutable physical-stock snapshots. This is not a live haul test.
public static class WimWetVenueWaterDebugContract
{
    public const string ReportPath = "Artifacts/QA/wim-implementation/wim-venue-wet-water-focused.txt";

    public static string Run()
    {
        var report = new List<string>();
        try
        {
            Verify(report);
            report.Insert(0, "result=PASS");
        }
        catch (Exception error)
        {
            report.Insert(0, "result=FAIL");
            report.Add(error.ToString());
        }
        report.Add("scope=actual fluid topology/state and production read selectors; synthetic water fixtures and controlled stock snapshots; no natural bathing/hauling or six-adult balance claim");
        Directory.CreateDirectory(Path.GetDirectoryName(ReportPath));
        File.WriteAllLines(ReportPath, report);
        return string.Join("\n", report);
    }

    private static void Verify(List<string> report)
    {
        var objects = new List<UnityEngine.Object>();
        try
        {
            var grid = new Grid(4, 1);
            BuildableObject storage = Node(objects, grid, 99811, "building:test-venue-water-storage", 0, true, false);
            BuildableObject wet = Node(objects, grid, 99812, "building:test-venue-water-bath", 1, false, true);
            BuildableObject unregistered = Node(objects, grid, 99813, "building:test-venue-water-orphan", 2, false, true);
            var stock = Proxy<IWorldItemStackRuntime>();
            var stockProbe = (BoundaryProbe)(object)stock;
            var topology = new IndustrialInfrastructureTopologyRuntime(new BuildingWorld(storage, wet));
            var fluid = new FluidNetworkRuntime(topology, Proxy<IPowerInfrastructureQuery>(), stock,
                Proxy<IPhysicalItemBatchDispositionService>(), Proxy<IWorldFilthQuery>(), Proxy<IGameClock>(),
                NoEnvironmentalFieldQuery.Instance, NeutralSeasonalEventQuery.Instance,
                Proxy<IFacilityCapabilityQuery>(), Proxy<IBuildingFacilityStateChangePort>(),
                new DungeonRuntimeAggregateRootStore(), new EditorFluidFacilityInputOwnerAuthority());
            var needs = Proxy<ICharacterNeedBalanceRuntime>();
            var needProbe = (BoundaryProbe)(object)needs;
            needProbe.WaterMultiplier = 1.5f;
            var use = new WaterFixtureUseRuntime(fluid, fluid, fluid, stock,
                Proxy<IWorldFilthQuery>(), needs, Proxy<IWorkforceReplanService>());
            var ability = wet.BuildingData.GetAbility<BuildingWaterFixtureAbility>();
            Require(ability != null && ability.cleanWaterPerUse == 3f, "Fixture water demand must be authored3, not inferred from query.");
            // Establish the real topology once, before the query purity comparison.
            _ = fluid.Networks.Count;
            string before = JsonUtility.ToJson(fluid.Capture());
            int version = fluid.Version;
            Require(!use.CanBeginWetUse(null, out _), "Null fixture admitted wet use.");
            Require(!use.CanBeginWetUse(storage, out _), "Non-water-fixture storage admitted wet use.");
            Require(!use.CanBeginWetUse(wet, out _), "Empty network admitted wet use.");
            Require(JsonUtility.ToJson(fluid.Capture()) == before && fluid.Version == version,
                "Unavailable wet-use query changed fluid authority.");
            report.Add("missing/nonwater/empty-query-preserves-state=PASS");

            Require(fluid.TryAdd(storage, WorldWaterQuality.Unsafe, 6f, out float unsafeAdded)
                && unsafeAdded == 6f, "Cannot prepare actual unsafe network water.");
            Require(!use.CanBeginWetUse(wet, out _), "Unsafe water satisfied authored Clean quality.");
            Require(fluid.TryAdd(storage, WorldWaterQuality.Clean, 4f, out float cleanAdded)
                && cleanAdded == 4f, "Cannot prepare actual clean network water.");
            Require(!use.CanBeginWetUse(wet, out _), "Four clean units satisfied 3 x 1.5 = 4.5 demand.");
            Require(fluid.TryAdd(storage, WorldWaterQuality.Clean, .5f, out float fraction)
                && fraction == .5f, "Cannot prepare the last half unit.");
            before = JsonUtility.ToJson(fluid.Capture());
            version = fluid.Version;
            for (int i = 0; i < 3; i++)
                Require(use.CanBeginWetUse(wet, out _), "Exactly4.5 clean water did not satisfy the same authored demand.");
            Require(needProbe.LastWaterInput == 3f && JsonUtility.ToJson(fluid.Capture()) == before
                && fluid.Version == version, "Successful repeated query changed state or skipped the authored multiplier.");
            Require(use.TryBeginUse(wet, default, out WaterFixtureUseTicket ticket, out _)
                && ticket.SupplyKind == WaterFixtureSupplyKind.Piped,
                "Actual use disagreed with successful wet preflight.");
            Require(fluid.Networks.Single(n => n.Channel == UtilityChannel.CleanWater).CleanWater == 0f
                && !use.CanBeginWetUse(wet, out _), "Actual use did not consume exactly4.5 clean water.");
            report.Add("quality-and-authored-multiplier-exact-boundary/query-zero-debit/actual-use-exact-debit=PASS");

            // Only synthetic in-memory authoring changes: no asset writes.
            ability.allowsDryFallback = true;
            Require(!use.CanBeginWetUse(wet, out _), "Dry fallback counted as wet bathing.");
            ability.allowsManualWaterFallback = true;
            ability.cleanWaterPerUse = 1f;
            string destination = "plumbing:manual-water:" + wet.PersistentInstanceId.Value;
            var stack = new WorldItemStackSnapshot
            {
                StackId = "stack:test-venue-water", ItemId = "resource:clean-water",
                Quantity = 2, State = WorldItemStackState.FacilityBuffer,
                DestinationId = destination, Position = wet.centerPos
            };
            stockProbe.Stacks = new[] { stack };
            before = JsonUtility.ToJson(fluid.Capture());
            version = fluid.Version;
            Require(use.CanBeginWetUse(wet, out _), "Two arrived bottles failed ceil(1.5) manual demand.");
            stack.Quantity = 1;
            Require(!use.CanBeginWetUse(wet, out _), "One bottle satisfied ceil(1.5) demand.");
            stack.Quantity = 2;
            stack.ReservedQuantity = 1;
            Require(!use.CanBeginWetUse(wet, out _), "Reserved stack counted as freely available manual supply.");
            stack.ReservedQuantity = 0;
            foreach (WorldItemStackState state in new[] { WorldItemStackState.Stored, WorldItemStackState.Carried, WorldItemStackState.Loose })
            {
                stack.State = state;
                Require(!use.CanBeginWetUse(wet, out _), "Non-arrived " + state + " counted as facility supply.");
            }
            stack.State = WorldItemStackState.FacilityBuffer;
            stack.DestinationId = "plumbing:manual-water:building:other";
            Require(!use.CanBeginWetUse(wet, out _), "Another facility's supply satisfied this fixture.");
            stack.DestinationId = destination;
            stack.ItemId = "resource:dirty-water";
            Require(!use.CanBeginWetUse(wet, out _), "Non-canonical clean-water item admitted.");
            stack.ItemId = "resource:clean-water";
            Require(use.CanBeginWetUse(wet, out _), "Restored exact delivered supply did not become available.");
            Require(JsonUtility.ToJson(fluid.Capture()) == before && fluid.Version == version
                && stack.Quantity == 2 && stack.ReservedQuantity == 0 && stack.DestinationId == destination,
                "Manual read changed fluid state, physical stock or routing.");
            report.Add("manual-arrival/ceil/reservation/destination/item-kind/dry-fallback/purity=PASS");

            unregistered.BuildingData.GetAbility<BuildingWaterFixtureAbility>().allowsManualWaterFallback = true;
            stack.Quantity = 8;
            stack.DestinationId = "plumbing:manual-water:" + unregistered.PersistentInstanceId.Value;
            Require(!use.CanBeginWetUse(unregistered, out _), "Unregistered fixture admitted via forged matching manual destination.");
            Require(!fluid.CanConsumeManualContainer(null, destination, 0f, out _),
                "Null manual consumer admitted by zero-demand shortcut.");
            Require(JsonUtility.ToJson(fluid.Capture()) == before && fluid.Version == version,
                "Invalid fixture query created fluid owner/state.");
            report.Add("unregistered-and-null-manual-owner-rejected-without-publication=PASS");
        }
        finally
        {
            for (int i = objects.Count - 1; i >= 0; i--)
                if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
        }
    }

    private static BuildableObject Node(List<UnityEngine.Object> cleanup, Grid grid, int definitionId,
        string instanceId, int x, bool storage, bool fixture)
    {
        var data = ScriptableObject.CreateInstance<BuildingSO>();
        cleanup.Add(data);
        data.id = definitionId;
        data.objectName = "WIM venue water fixture";
        var abilities = new BuildingAbilityCollection();
        abilities.Add(new BuildingUtilityConnectionAbility
        { channels = UtilityChannel.CleanWater | UtilityChannel.Wastewater });
        if (storage) abilities.Add(new BuildingWaterStorageAbility
        { channels = UtilityChannel.CleanWater | UtilityChannel.Wastewater, cleanWaterCapacity = 30f, wastewaterCapacity = 30f });
        if (fixture) abilities.Add(new BuildingWaterFixtureAbility
        { cleanWaterPerUse = 3f, wastewaterPerUse = 1f, minimumQuality = WorldWaterQuality.Clean });
        data.ReplaceAbilities(abilities);
        var host = new GameObject("WIM venue water fixture");
        cleanup.Add(host);
        var building = host.AddComponent<BuildableObject>();
        building.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
        typeof(BuildableObject).GetProperty(nameof(BuildableObject.BuildingData)).SetValue(building, data);
        typeof(BuildableObject).GetProperty(nameof(BuildableObject.centerPos)).SetValue(building, new Vector2Int(x, 0));
        building.RestorePersistentIdentity(new BuildingInstanceId(instanceId));
        building.SetGrid(grid);
        return building;
    }

    private static T Proxy<T>() where T : class => DispatchProxy.Create<T, BoundaryProbe>();
    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }

    private sealed class BuildingWorld : IBuildingWorldQuery
    {
        public BuildingWorld(params BuildableObject[] buildings) { Buildings = buildings; }
        public int BuildingVersion => 1;
        public IReadOnlyList<BuildableObject> Buildings { get; }
    }

    public class BoundaryProbe : DispatchProxy
    {
        public WorldItemStackSnapshot[] Stacks = Array.Empty<WorldItemStackSnapshot>();
        public float WaterMultiplier = 1f;
        public float LastWaterInput;

        protected override object Invoke(MethodInfo method, object[] arguments)
        {
            if (method.Name == "GetAllStacks") return Stacks;
            if (method.Name == "ApplyPersonalContinuousWaterMultiplier")
            {
                LastWaterInput = (float)arguments[0];
                return LastWaterInput * WaterMultiplier;
            }
            if (method.Name == "IsPowered") return true;
            if (method.Name == "get_IsPaused") return true;
            if (method.Name == "get_DeltaTime") return 0f;
            // Unexpected command/physical disposition/replan/filth calls fail,
            // rather than using a generic no-op proxy to hide a side effect.
            throw new InvalidOperationException("Unexpected boundary call: " + method.DeclaringType.Name + "." + method.Name);
        }
    }
}
#endif
