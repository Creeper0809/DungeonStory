using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ClimateDurableEquipmentDebugScenarios
{
    private static readonly BuildingInstanceId TowerId =
        new("building:qa-weather-tower");
    private static readonly Vector2Int TowerPosition = new(23, 4);

    [MenuItem("Tools/Dungeon Story/QA/Climate Durable Equipment")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("CLIMATE_DURABLE_EQUIPMENT_PASS");
    }

    public static void RunAll()
    {
        DurableFacilityEquipmentUseRuntimeDebugScenarios.RunAll();
        VerifyExactPairAssignmentAndOperationalProjection();
        VerifyPairedWearContract();
        VerifySecondToolFailureRejectsObservation();
        VerifyMissingPolicyFailsLoudly();
        VerifyLostTowerClosesSlot();
        VerifyLifecycleConflictBlocksSave();
    }

    private static void VerifyExactPairAssignmentAndOperationalProjection()
    {
        Fixture fixture = new();
        ClimateDurableEquipmentRuntime runtime = fixture.CreateRuntime();

        Require(runtime.TryMaintain(TowerId, TowerPosition, false),
            "Climate equipment supply did not become ready.");
        DurableFacilityEquipmentAssignment assignment =
            fixture.Slots.LastAssignment;
        Require(assignment != null
            && assignment.Key.LogicalOwnerDomain ==
                ClimateDurableEquipmentPolicySource.LogicalOwnerDomain
            && assignment.Key.OwnerSubjectId == TowerId.Value
            && assignment.OwnerFacilityId.Equals(TowerId)
            && assignment.DropPosition == TowerPosition
            && assignment.Requirements.Count == 2,
            "Climate equipment assignment lost exact tower identity.");
        DurableFacilityEquipmentRequirement almanac = assignment.Requirements
            .Single(value => value.RequirementId ==
                ClimateDurableEquipmentPolicySource.AlmanacRequirementId);
        DurableFacilityEquipmentRequirement kit = assignment.Requirements
            .Single(value => value.RequirementId ==
                ClimateDurableEquipmentPolicySource.ObservationKitRequirementId);
        Require(almanac.ItemId.Equals(
                (ItemDefinitionId)DurableToolItemRules.SeasonalAlmanac)
            && almanac.RequiredQuantity == 1
            && kit.ItemId.Equals(
                (ItemDefinitionId)DurableToolItemRules.WeatherObservationKit)
            && kit.RequiredQuantity == 1,
            "Climate equipment policy lost exact item/quantity identity.");
        Require(runtime.IsOperational(TowerId),
            "A ready climate equipment pair was not projected operational.");
    }

    private static void VerifyPairedWearContract()
    {
        Fixture fixture = new();
        ClimateDurableEquipmentRuntime runtime = fixture.CreateRuntime();

        Require(runtime.TryMaintain(TowerId, TowerPosition, true),
            "The paired climate observation wear did not commit.");
        Require(fixture.Use.Calls.Count == 2,
            "Climate observation did not wear exactly two requirements.");
        WearCall almanac = fixture.Use.Calls[0];
        WearCall kit = fixture.Use.Calls[1];
        Require(almanac.RequirementId ==
                ClimateDurableEquipmentPolicySource.AlmanacRequirementId
            && Math.Abs(almanac.Wear
                - ClimateDurableEquipmentRuntime.AlmanacWearPerObservationDay)
                <= 0.000001d
            && kit.RequirementId ==
                ClimateDurableEquipmentPolicySource.ObservationKitRequirementId
            && Math.Abs(kit.Wear
                - ClimateDurableEquipmentRuntime.ObservationKitWearPerObservationDay)
                <= 0.000001d,
            "Climate observation wear order or amount drifted.");
    }

    private static void VerifySecondToolFailureRejectsObservation()
    {
        Fixture fixture = new();
        fixture.Use.FailRequirementId =
            ClimateDurableEquipmentPolicySource.ObservationKitRequirementId;
        ClimateDurableEquipmentRuntime runtime = fixture.CreateRuntime();

        Require(!runtime.TryMaintain(TowerId, TowerPosition, true)
            && fixture.Use.Calls.Count == 2,
            "A failed observation-kit wear was reported as a completed pair.");
    }

    private static void VerifyMissingPolicyFailsLoudly()
    {
        RecordingSlots slots = new();
        ClimateDurableEquipmentRuntime runtime = new(
            new MissingPolicyQuery(),
            slots,
            slots,
            new RecordingUse(slots));
        bool threw = false;
        try
        {
            runtime.TryMaintain(TowerId, TowerPosition, false);
        }
        catch (InvalidOperationException exception)
        {
            threw = exception.Message.Contains(
                "climate observation durable-equipment policy",
                StringComparison.Ordinal);
        }
        Require(threw, "A missing climate equipment policy did not fail loudly.");
    }

    private static void VerifyLostTowerClosesSlot()
    {
        BuildableObject tower = CreateWeatherTower(out GameObject root);
        try
        {
            Fixture fixture = new();
            BuildingInstanceId towerId = tower.RequirePersistentInstanceId();
            Require(fixture.CreateRuntime().TryMaintain(
                    towerId,
                    tower.centerPos,
                    false),
                "Climate lifecycle fixture could not establish its slot.");
            MutableBuildingWorld world = new(tower);
            ClimateDurableEquipmentLifecycleRuntime lifecycle = new(
                world,
                fixture.Slots,
                fixture.Slots);

            lifecycle.Start();
            world.RemoveAll();
            lifecycle.Tick();

            Require(fixture.Slots.CloseCalls == 1
                && fixture.Slots.LastCloseReason ==
                    "climate-observation-tower-lost",
                "A lost weather tower did not close its climate slot exactly once.");
            lifecycle.ValidateBeforeCapture();
        }
        finally
        {
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static void VerifyLifecycleConflictBlocksSave()
    {
        BuildableObject tower = CreateWeatherTower(out GameObject root);
        try
        {
            Fixture fixture = new();
            fixture.CreateRuntime().TryMaintain(
                tower.RequirePersistentInstanceId(),
                tower.centerPos,
                false);
            fixture.Slots.RejectClose = true;
            MutableBuildingWorld world = new(tower);
            ClimateDurableEquipmentLifecycleRuntime lifecycle = new(
                world,
                fixture.Slots,
                fixture.Slots);
            world.RemoveAll();

            bool threw = false;
            try
            {
                lifecycle.ValidateBeforeCapture();
            }
            catch (InvalidOperationException exception)
            {
                threw = exception.Message.Contains(
                    "fixture-close-conflict",
                    StringComparison.Ordinal);
            }
            Require(threw,
                "An unresolved climate slot close conflict allowed save capture.");
        }
        finally
        {
            if (root != null)
                UnityEngine.Object.DestroyImmediate(root);
        }
    }

    private static BuildableObject CreateWeatherTower(out GameObject root)
    {
        BuildingSO definition = AssetDatabase.FindAssets("t:BuildingSO")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .SingleOrDefault(value => value != null
                && value.id ==
                    ClimateDurableEquipmentLifecycleRuntime.WeatherTowerDefinitionId)
            ?? throw new InvalidOperationException(
                "The authored weather observation tower BuildingSO is missing or duplicate.");
        root = new GameObject("QA Climate Durable Equipment Tower");
        BuildableObject tower = root.AddComponent<BuildableObject>();
        CharacterAiEditorTestDependencies.Inject(tower);
        tower.Initialization(definition, TowerPosition);
        return tower;
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class Fixture
    {
        internal Fixture()
        {
            Policies = new DurableFacilityEquipmentPolicyRegistry(
                new IDurableFacilityEquipmentPolicySource[]
                {
                    new ClimateDurableEquipmentPolicySource()
                });
            Slots = new RecordingSlots();
            Use = new RecordingUse(Slots);
        }

        internal DurableFacilityEquipmentPolicyRegistry Policies { get; }
        internal RecordingSlots Slots { get; }
        internal RecordingUse Use { get; }

        internal ClimateDurableEquipmentRuntime CreateRuntime() => new(
            Policies,
            Slots,
            Slots,
            Use);
    }

    private sealed class RecordingSlots :
        IDurableFacilityEquipmentSlotCommand,
        IDurableFacilityEquipmentSlotQuery
    {
        internal DurableFacilityEquipmentAssignment LastAssignment { get; private set; }
        internal DurableFacilityEquipmentSlotSnapshot Snapshot { get; private set; }
        internal int CloseCalls { get; private set; }
        internal string LastCloseReason { get; private set; } = string.Empty;
        internal bool RejectClose { get; set; }

        public DurableFacilityEquipmentSlotResult TryReconcile(
            DurableFacilityEquipmentAssignment desired)
        {
            LastAssignment = desired;
            Snapshot = CreateSnapshot(desired, ready: true);
            return Success(DurableFacilityEquipmentSlotStatus.Applied);
        }

        public DurableFacilityEquipmentSlotResult TryEnsureSupply(
            DurableFacilityEquipmentSlotKey key)
        {
            Require(Snapshot != null && key.Equals(Snapshot.Key),
                "Climate supply targeted another slot.");
            return Success(DurableFacilityEquipmentSlotStatus.Replay);
        }

        public bool TryCapture(
            DurableFacilityEquipmentSlotKey key,
            out DurableFacilityEquipmentSlotSnapshot snapshot)
        {
            snapshot = Snapshot != null && key.Equals(Snapshot.Key)
                ? Snapshot
                : null;
            return snapshot != null;
        }

        public IReadOnlyList<DurableFacilityEquipmentSlotSnapshot> CaptureAll() =>
            Snapshot == null
                ? Array.Empty<DurableFacilityEquipmentSlotSnapshot>()
                : new[] { Snapshot };

        public DurableFacilityEquipmentSlotResult TryClose(
            DurableFacilityEquipmentSlotKey key,
            string reasonCode)
        {
            Require(Snapshot != null && key.Equals(Snapshot.Key),
                "Climate close targeted another slot.");
            CloseCalls++;
            LastCloseReason = reasonCode;
            if (RejectClose)
            {
                return new DurableFacilityEquipmentSlotResult(
                    DurableFacilityEquipmentSlotStatus.Conflict,
                    Snapshot,
                    "fixture-close-conflict");
            }
            DurableFacilityEquipmentSlotSnapshot closed = Snapshot;
            Snapshot = null;
            return new DurableFacilityEquipmentSlotResult(
                DurableFacilityEquipmentSlotStatus.Applied,
                closed,
                string.Empty);
        }

        public IReadOnlyList<DurableFacilityEquipmentSlotResult>
            TryAdvancePending() => Array.Empty<DurableFacilityEquipmentSlotResult>();

        private DurableFacilityEquipmentSlotResult Success(
            DurableFacilityEquipmentSlotStatus status) => new(
            status,
            Snapshot,
            string.Empty);

        private static DurableFacilityEquipmentSlotSnapshot CreateSnapshot(
            DurableFacilityEquipmentAssignment assignment,
            bool ready)
        {
            const long sequence = 1L;
            return new DurableFacilityEquipmentSlotSnapshot(
                assignment,
                sequence,
                DurableFacilityEquipmentSlotIdentity.BuildDestinationId(
                    assignment.Key,
                    sequence),
                DurableFacilityEquipmentSlotIdentity.BuildOwnerOperationId(
                    assignment.Key,
                    sequence),
                DurableFacilityEquipmentFingerprint.CreateAssignment(assignment),
                new DurableFacilityEquipmentCapacityProjection(
                    DurableFacilityEquipmentSlotIdentity.DefinitionMassPolicyKind,
                    new PhysicalMassGrams(2_000L),
                    1L,
                    new string('b', 64)),
                assignment.Requirements.Select(requirement =>
                    new DurableFacilityEquipmentRequirementStatus(
                        requirement,
                        pendingQuantity: ready ? 0 : 1,
                        bufferedUsableQuantity: ready ? 1 : 0)));
        }
    }

    private sealed class MutableBuildingWorld : IBuildingWorldQuery
    {
        private readonly List<BuildableObject> buildings = new();

        internal MutableBuildingWorld(BuildableObject building)
        {
            buildings.Add(building);
        }

        public int BuildingVersion { get; private set; } = 1;
        public IReadOnlyList<BuildableObject> Buildings => buildings;

        internal void RemoveAll()
        {
            buildings.Clear();
            BuildingVersion++;
        }
    }

    private readonly struct WearCall
    {
        internal WearCall(string requirementId, double wear)
        {
            RequirementId = requirementId;
            Wear = wear;
        }

        internal string RequirementId { get; }
        internal double Wear { get; }
    }

    private sealed class RecordingUse : IDurableFacilityEquipmentUseCommand
    {
        private readonly RecordingSlots slots;

        internal RecordingUse(RecordingSlots slots)
        {
            this.slots = slots;
        }

        internal List<WearCall> Calls { get; } = new();
        internal string FailRequirementId { get; set; } = string.Empty;

        public DurableFacilityEquipmentUseResult TryApplyWearAndEffect(
            DurableFacilityEquipmentSlotKey key,
            string requirementId,
            double wearAmount,
            IDurableFacilityEquipmentEffectCommit effect)
        {
            DurableFacilityEquipmentSlotSnapshot slot = slots.Snapshot;
            DurableFacilityEquipmentRequirement requirement = slot.Assignment
                .Requirements.Single(value => value.RequirementId == requirementId);
            Calls.Add(new WearCall(requirementId, wearAmount));
            if (string.Equals(
                    FailRequirementId,
                    requirementId,
                    StringComparison.Ordinal))
            {
                return Failure(slot, "fixture-wear-rejected");
            }

            DurableFacilityEquipmentUseSubject before = new(
                "stack:" + requirementId,
                1L,
                requirement.ItemId,
                1,
                Array.Empty<DurableFacilityEquipmentComponentSnapshot>());
            if (!effect.TryPreflight(
                    slot,
                    requirement,
                    before,
                    wearAmount,
                    out string preflightFailure))
            {
                return Failure(slot, preflightFailure);
            }
            DurableFacilityEquipmentUseSubject after = new(
                before.StackId,
                2L,
                before.ItemId,
                before.Quantity,
                before.Components);
            DurableFacilityEquipmentUseContext context = new(
                slot,
                requirement,
                before,
                after,
                wearAmount);
            if (!effect.TryCommit(context, out string effectFailure))
                return Failure(slot, effectFailure);
            return new DurableFacilityEquipmentUseResult(
                DurableFacilityEquipmentUseStatus.Applied,
                slot,
                before.StackId,
                string.Empty);
        }

        private static DurableFacilityEquipmentUseResult Failure(
            DurableFacilityEquipmentSlotSnapshot slot,
            string reason) => new(
            DurableFacilityEquipmentUseStatus.Deferred,
            slot,
            string.Empty,
            string.IsNullOrWhiteSpace(reason)
                ? "fixture-wear-failed"
                : reason);
    }

    private sealed class MissingPolicyQuery :
        IDurableFacilityEquipmentPolicyQuery
    {
        public long Revision => 1L;

        public bool TryGetPolicy(
            string policyId,
            out DurableFacilityEquipmentPolicy policy)
        {
            policy = null;
            return false;
        }

        public IReadOnlyList<DurableFacilityEquipmentPolicy> CapturePolicies() =>
            Array.Empty<DurableFacilityEquipmentPolicy>();
    }
}
