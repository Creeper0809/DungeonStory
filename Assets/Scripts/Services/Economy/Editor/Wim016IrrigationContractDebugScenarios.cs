#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using DungeonStory.Foundation;
using UnityEngine;

// Root-owned service contract witness. The path resolver and irrigation
// service are real; research eligibility and finite fluid store are controlled.
// Actual crop publication and live fluid network are separate integration gates.
public static class Wim016IrrigationContractDebugScenarios
{
    public static bool Run(out string report)
    {
        var objects = new List<UnityEngine.Object>();
        var lines = new List<string> {
            "WIM016 irrigation service ownership/rate contract",
            "scope=real irrigation service/grid paths; controlled eligibility/research/finite fluid",
            "not-tested=actual network topology/storage, main crop publication, actual research/placement/UI" };
        try
        {
            var grid = new Grid(24, 3);
            for (int y = 0; y < grid.height; ++y)
                for (int x = 0; x < grid.width; ++x)
                    grid.SetAreaType(new Vector2Int(x, y), GridCellAreaType.ExteriorPath);
            var a = Create("a", 99831, 3, true);
            var b = Create("b", 99832, 5, true);
            var first = Create("first", 99833, 7, false);
            var second = Create("second", 99834, 6, false);
            var distant = Create("far", 99835, 20, false);
            var facilities = new ControlledFacilities(a, b);
            var fluid = new ControlledFluid();
            var clock = new ControlledClock();
            var runtime = new CropIrrigationRuntime(
                facilities,
                new ControlledResearch(),
                fluid,
                fluid,
                fluid,
                clock);
            var request = new CropIrrigationRequest(first, true, 0, 2, false);

            Require(runtime.Assess(request).Status == CropIrrigationStatus.WaterUnavailable,
                "Empty finite source did not report water unavailable.");
            Require(!runtime.TryPrepareSupply(request, out _) && fluid.Consumed == 0,
                "Empty source supplied water.");
            fluid.Water = 3;
            for (int i = 0; i < 10; ++i)
                Require(runtime.Assess(request).CanSupply, "Repeated readonly query changed availability.");
            Require(fluid.Water == 3 && fluid.Consumed == 0, "Assess consumed water.");

            Reject(new CropIrrigationRequest(first, true, 0, 2, true), CropIrrigationStatus.ManualRefillActive);
            Reject(new CropIrrigationRequest(first, true, 1.5f, 2, false), CropIrrigationStatus.CapacityUnavailable);
            Reject(new CropIrrigationRequest(first, false, 0, 2, false), CropIrrigationStatus.NotRequired);
            Reject(new CropIrrigationRequest(distant, true, 0, 2, false), CropIrrigationStatus.OutOfRange);
            lines.Add("[PASS] finite shortage; readonly query; manual owner/capacity/no-demand/range rejection no debit");

            fluid.FailNextCommit = true;
            Require(!runtime.TryPrepareSupply(request, out _)
                && fluid.Water == 3 && fluid.Consumed == 0,
                "Commit rejection changed finite source.");
            Require(runtime.Assess(request).CanSupply, "Failed commit advanced cooldown.");

            Require(runtime.TryPrepareSupply(request, out PreparedCropIrrigationSupply rolledBack),
                "Eligible supply could not be prepared for rollback.");
            Require(fluid.Water == 2 && fluid.Consumed == 1,
                "Prepared supply did not stage exactly one finite-water debit.");
            runtime.RollbackPreparedSupply(rolledBack);
            Require(fluid.Water == 3 && fluid.Consumed == 0 && runtime.Assess(request).CanSupply,
                "Prepared supply rollback did not restore water and leave cooldown untouched.");

            CropIrrigationSupplyResult firstResult = PrepareAndCommit(request);
            Require(firstResult.Succeeded && firstResult.SuppliedWaterUnits == 1
                && firstResult.ConsumedQuality == WorldWaterQuality.Clean
                && firstResult.Assessment.IrrigatorId.Equals(a.PersistentInstanceId)
                && fluid.Water == 2 && fluid.Consumed == 1,
                "First successful command did not debit exactly one from selected source.");
            Reject(request, CropIrrigationStatus.RateLimited);
            // A is cooling down, but B must remain usable for a different plot.
            CropIrrigationSupplyResult secondResult = PrepareAndCommit(
                new CropIrrigationRequest(second, true, 0, 2, false));
            Require(secondResult.Succeeded && secondResult.SuppliedWaterUnits == 1
                && secondResult.Assessment.IrrigatorId.Equals(b.PersistentInstanceId)
                && fluid.Water == 1 && fluid.Consumed == 2,
                "First supplier cooldown leaked into a fresh second supplier/plot.");
            lines.Add("[PASS] rejected commit atomic; same-plot replay limited; fresh second supplier serves second plot");

            clock.Now = 1;
            CropIrrigationSupplyResult third = PrepareAndCommit(request);
            Require(third.Succeeded && fluid.Water == 0 && fluid.Consumed == 3,
                "One-second recovery did not consume remaining finite unit exactly.");
            clock.Now = 2;
            Reject(request, CropIrrigationStatus.WaterUnavailable);
            facilities.Values.Clear();
            Reject(request, CropIrrigationStatus.NoOperationalIrrigator);
            Require(fluid.Consumed == 3 && fluid.Water == 0, "Terminal checks changed conservation.");
            lines.Add("[PASS] one-second recovery; exhausted/disconnected supply no phantom water; total3 debits");
            lines.Add("result=PASS; no actual network/crop publication claim");
            report = string.Join("\n", lines);
            return true;

            void Reject(CropIrrigationRequest value, CropIrrigationStatus status)
            {
                float before = fluid.Water;
                int count = fluid.Consumed;
                var assessment = runtime.Assess(value);
                bool prepared = runtime.TryPrepareSupply(value, out PreparedCropIrrigationSupply result);
                Require(assessment.Status == status && !prepared
                    && (result == null || result.Result.Assessment.Status == status)
                    && fluid.Water == before && fluid.Consumed == count,
                    "Rejected operation changed source or wrong reason: " + status + "/" + assessment.Status);
            }

            CropIrrigationSupplyResult PrepareAndCommit(CropIrrigationRequest value)
            {
                Require(runtime.TryPrepareSupply(value, out PreparedCropIrrigationSupply prepared),
                    "Eligible supply could not be prepared: " + runtime.Assess(value).Status);
                CropIrrigationSupplyResult result = prepared.Result;
                runtime.CommitPreparedSupply(prepared);
                return result;
            }

            BuildableObject Create(string name, int id, int x, bool irrigation)
            {
                var definition = ScriptableObject.CreateInstance<BuildingSO>();
                objects.Add(definition);
                definition.id = id;
                definition.name = "Wim016_" + name;
                definition.objectName = name;
                definition.width = definition.height = 1;
                definition.unlocked = true;
                definition.layer = GridLayer.Building;
                definition.category = BuildingCategory.Resource;
                definition.runtimeArchetype = BuildingRuntimeArchetypeKind.Generic;
                definition.ReplaceAbilities(new BuildingAbilityCollection());
                if (irrigation)
                {
                    definition.AbilityModules.Add(new BuildingCropIrrigationAbility {
                        rangeManhattan = 4, waterPerRefill = 1, minimumRefillIntervalSeconds = 1 });
                    definition.AbilityModules.Add(new BuildingUtilityConnectionAbility {
                        channels = UtilityChannel.CleanWater, maxThroughput = 1 });
                }
                var host = new GameObject(definition.name);
                objects.Add(host);
                var building = host.AddComponent<BuildableObject>();
                building.RestorePersistentIdentity(new BuildingInstanceId("building:test:wim016-irrigation:" + name));
                CharacterAiEditorTestDependencies.Inject(building);
                building.SetGrid(grid);
                building.Initialization(definition, new Vector2Int(x, 1));
                return building;
            }
        }
        catch (Exception error)
        {
            lines.Add("result=FAIL\n" + error);
            report = string.Join("\n", lines);
            return false;
        }
        finally
        {
            for (int i = objects.Count - 1; i >= 0; --i)
                if (objects[i] != null) UnityEngine.Object.DestroyImmediate(objects[i]);
        }
    }

    private static void Require(bool condition, string reason)
    {
        if (!condition) throw new InvalidOperationException(reason);
    }

    private sealed class ControlledFacilities : IFacilityCapabilityQuery
    {
        public readonly List<BuildableObject> Values;
        public ControlledFacilities(params BuildableObject[] values) => Values = new List<BuildableObject>(values);
        public IReadOnlyList<BuildableObject> FindOperational(FacilityCapabilityKind capability, string definitionId = "") => Values;
        public IReadOnlyList<BuildableObject> FindOperational(ResearchFacilityCommandKind command) => throw new NotSupportedException();
    }
    private sealed class ControlledResearch : IBlueprintResearchStateService
    {
        private readonly BlueprintResearchState state = new BlueprintResearchState();
        public BlueprintResearchState GetState() => state;
    }
    private sealed class ControlledClock : IGameClock
    {
        public float Now;
        public float Time => Now;
        public float DeltaTime => 0;
        public int FrameCount => 0;
        public bool IsPaused => false;
    }
    private sealed class ControlledFluid :
        IFluidInfrastructureQuery,
        IFluidInfrastructureTransaction,
        IFluidInfrastructureMutationTransaction
    {
        private readonly Dictionary<FluidNetworkAggregateState, MutationSnapshot> mutations =
            new();
        private float water;
        private int version;

        public float Water
        {
            get => water;
            set
            {
                if (water.Equals(value)) return;
                water = value;
                version = checked(version + 1);
            }
        }
        public int Consumed;
        public bool FailNextCommit;
        public int Version => version;
        public IReadOnlyList<FluidNetworkSnapshot> Networks =>
            Array.Empty<FluidNetworkSnapshot>();
        public IReadOnlyList<WaterTransferFacilitySnapshot> WaterTransfers =>
            Array.Empty<WaterTransferFacilitySnapshot>();
        public bool TryGetNetwork(
            BuildableObject building,
            out FluidNetworkSnapshot snapshot)
        {
            snapshot = default;
            return false;
        }
        public bool TryGetMaintenance(
            BuildableObject building,
            out float blockage,
            out float leak)
        {
            blockage = 0f;
            leak = 0f;
            return false;
        }
        public bool TryGetWaterCondition(
            BuildableObject building,
            out BuildingWaterConditionSnapshot snapshot)
        {
            snapshot = default;
            return false;
        }
        public bool CanConsume(BuildableObject consumer, WorldWaterQuality quality, float amount, out DomainFailure failure)
        {
            Require(quality == WorldWaterQuality.Clean && amount == 1, "Irrigation requested non-canonical debit.");
            failure = Water >= amount ? DomainFailure.None : new DomainFailure(FailureCode.FluidInsufficientWater);
            return !failure.IsFailure;
        }
        public bool TryConsume(BuildableObject consumer, WorldWaterQuality quality, float amount,
            out WorldWaterQuality consumedQuality, out DomainFailure failure)
        {
            consumedQuality = WorldWaterQuality.Clean;
            if (FailNextCommit)
            {
                FailNextCommit = false;
                failure = new DomainFailure(FailureCode.FluidInsufficientWater);
                return false;
            }
            if (!CanConsume(consumer, quality, amount, out failure)) return false;
            Water -= amount;
            ++Consumed;
            return true;
        }
        public bool TryAdd(BuildableObject producer, WorldWaterQuality quality, float amount, out float accepted) => throw new NotSupportedException();
        public bool TryConsumeManualContainer(BuildableObject consumer, string destination, float amount, out DomainFailure failure) => throw new NotSupportedException();

        public FluidInfrastructureMutationToken CaptureMutation()
        {
            var marker = new FluidNetworkAggregateState { Version = version };
            mutations.Add(marker, new MutationSnapshot(water, Consumed, version));
            return new FluidInfrastructureMutationToken(marker, checked(version + 1));
        }

        public void RestoreMutation(in FluidInfrastructureMutationToken token)
        {
            Require(token.IsValid && token.ExpectedMutatedVersion == version,
                "Fluid rollback token did not match the exact staged mutation.");
            Require(mutations.TryGetValue(token.Before, out MutationSnapshot snapshot),
                "Fluid rollback token was not issued by this fixture.");
            water = snapshot.Water;
            Consumed = snapshot.Consumed;
            version = snapshot.Version;
            mutations.Remove(token.Before);
        }

        private readonly struct MutationSnapshot
        {
            public MutationSnapshot(float water, int consumed, int version)
            {
                Water = water;
                Consumed = consumed;
                Version = version;
            }

            public float Water { get; }
            public int Consumed { get; }
            public int Version { get; }
        }
    }
}
#endif
