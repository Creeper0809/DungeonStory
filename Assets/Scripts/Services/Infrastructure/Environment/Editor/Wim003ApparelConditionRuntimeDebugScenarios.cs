using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using DungeonStory.Foundation;
using UnityEditor;
using UnityEngine;
using VContainer;

/// <summary>Real physical/apparel authorities; controlled weather, time and one failed write.</summary>
public static class Wim003ApparelConditionRuntimeDebugScenarios
{
    public static void RunProtectedPlay(DungeonRuntimeLifetimeScope scope)
        => RunProtected(scope, false);

    public static void RunProtectedSelectionAndRecovery(DungeonRuntimeLifetimeScope scope)
        => RunProtected(scope, true);

    private static void RunProtected(DungeonRuntimeLifetimeScope scope, bool selectionOnly)
    {
        Require(EditorApplication.isPlaying && Time.timeScale == 0f && scope?.Container != null,
            "Requires paused protected main Play after StartParty, with persistence writers disabled.");
        IObjectResolver services = scope.Container;
        WorldItemStackRuntime items = services.Resolve<IWorldItemStackRuntime>() as WorldItemStackRuntime;
        Require(items != null, "Physical authority interface is not backed by the current runtime.");
        ICharacterApparelQuery apparel = services.Resolve<ICharacterApparelQuery>();
        ICharacterApparelCommand command = services.Resolve<ICharacterApparelCommand>();
        ICharacterApparelPersistence persistence = services.Resolve<ICharacterApparelPersistence>();
        DungeonPhysicalItemSaveData originalPhysical = items.Capture();
        EquippedApparelSaveData[] originalApparel = persistence.CaptureApparel().ToArray();
        CharacterApparelPolicySaveData[] originalPolicies = persistence.CaptureApparelPolicies().ToArray();
        string[] evidence = null;
        try
        {
        command.RefreshAutomaticSelections();
        CharacterActor actor = services.Resolve<ICharacterWorldQuery>().Characters
            .Where(value => value != null && !value.IsDead && !value.IsOnExpedition)
            .OrderBy(value => value.Identity.PersistentId, StringComparer.Ordinal)
            .FirstOrDefault(value => items.GetAllStacks().Any(stack =>
                ApparelItemStateCodec.TryRead(stack.Components, out ApparelInstanceState state)
                && state.designatedWearerCharacterId == value.Identity.PersistentId));
        Require(actor != null, "StartParty has no actor with real designated starter apparel stock.");
        CharacterId id = CharacterPersistentIdentity.Require(actor);
        IApparelDefinitionCatalog definitions = services.Resolve<IApparelDefinitionCatalog>();
        ITextileMaterialCatalog materials = services.Resolve<ITextileMaterialCatalog>();
        IAnatomyAttachmentQuery anatomy = services.Resolve<IAnatomyAttachmentQuery>();
        // Starter garments have only Underwear tags, not Daily. Equip them via
        // the real command; do not assume Daily auto-selection supplies them.
        WorldItemStackSnapshot[] starter = items.GetAllStacks()
            .Where(stack => ApparelItemStateCodec.TryRead(stack.Components, out ApparelInstanceState state)
                && state.designatedWearerCharacterId == id.Value)
            .GroupBy(stack => stack.ItemId).Select(group => group.First()).ToArray();
        Require(starter.Length == 2, "Expected the two authored starter underwear definitions.");
        foreach (WorldItemStackSnapshot garment in starter)
        {
            Require(command.TryPlanChange(id, new ItemInstanceId(garment.ItemInstanceId),
                    out ApparelChangePlan starterPlan, out DomainFailure starterFailure)
                && command.TryCommitChange(starterPlan, out starterFailure),
                "Actual starter equip failed: " + starterFailure.Code);
        }
        EquippedApparelSnapshot lower = apparel.GetEquipped(id).First(value => value.Layer == ApparelLayer.Underwear);
        ApparelDefinitionSO outerDefinition = definitions.Definitions
            .Where(value => value.Layer > ApparelLayer.Underwear && value.Layer != ApparelLayer.Accessory
                && (value.UseTags & ApparelUseTag.Daily) != 0
                && (value.OccupiedPoints & lower.OccupiedPoints) == lower.OccupiedPoints)
            .OrderBy(value => value.ApparelId, StringComparer.Ordinal)
            .FirstOrDefault(value => anatomy.CanEquip(id, value, NewState(value), out _, out _));
        Require(outerDefinition != null, "No authored daily garment fits and covers starter underwear.");
        Require(items.SpawnUniqueItemAt(outerDefinition.PhysicalItemId, actor.GetNowXY(),
            WorldItemStackState.Loose, string.Empty, out string outerStackId), "QA physical source failed.");
        Require(items.TrySetInstanceComponent(outerStackId, ApparelItemStateCodec.Create(NewState(outerDefinition))),
            "QA apparel component authoring failed.");
        ItemInstanceId outerId = new(items.GetAllStacks().Single(value => value.StackId == outerStackId).ItemInstanceId);
        Require(command.TryPlanChange(id, outerId, out ApparelChangePlan plan, out DomainFailure failure)
            && command.TryCommitChange(plan, out failure), "Real outer-layer equip: " + failure);
        // Every other live actor can legitimately claim one garment. Supply
        // that demand plus one spare; do not disable their normal auto policy.
        for (int spare = 0; spare < services.Resolve<ICharacterWorldQuery>().Characters.Count; spare++)
            Require(items.SpawnUniqueItemAt(outerDefinition.PhysicalItemId, actor.GetNowXY(),
                    WorldItemStackState.Loose, string.Empty, out string spareStackId)
                && items.TrySetInstanceComponent(spareStackId, ApparelItemStateCodec.Create(NewState(outerDefinition))),
                "QA daily-spare physical source failed.");

        IClimateDefinitionCatalog climateDefinitions = services.Resolve<IClimateDefinitionCatalog>();
        ControlledClimate climate = new(climateDefinitions.Fronts.First(value => value.Kind == WeatherFrontKind.Clear).Id);
        string rainId = climateDefinitions.Fronts.First(value => value.Kind == WeatherFrontKind.Rain).Id;
        Grid grid = new(Mathf.Max(1, actor.GetNowXY().x + 1), Mathf.Max(1, actor.GetNowXY().y + 1));
        grid.SetAreaType(actor.GetNowXY(), GridCellAreaType.DungeonInterior);
        IGridSystemProvider gridProvider = Proxy<IGridSystemProvider>((method, args) =>
        {
            if (method.Name == "TryGetGrid") { args[0] = grid; return true; }
            if (method.Name == "get_Grid") return grid;
            throw Unexpected(method);
        });
        ICharacterWorldQuery world = new WitnessWorld(actor);
        ICharacterApparelQuery scopedApparel = Proxy<ICharacterApparelQuery>((method, args) =>
            method.Name == "GetAllEquipped" ? apparel.GetEquipped(id) : Forward(method, apparel, args));
        ControlledField field = new();
        Dictionary<CharacterId, EnvironmentalWorkKind> work = new();
        ApparelConditionRuntime runtime = Create(items);
        Require(services.Resolve<ApparelConditionRuntime>() != null, "Live condition runtime DI not registered.");
        ItemInstanceId[] worn = apparel.GetEquipped(id).Select(value => value.ItemInstanceId).ToArray();
        string ownership = Ownership(items);
        string[] wornValues = worn.Select(value => value.Value).ToArray();
        string untouched = Conditions(items.GetAllStacks().Where(value => !wornValues.Contains(value.ItemInstanceId)));
        float hygiene = actor.stats[CharacterCondition.HYGIENE];
        try
        {
            if (!selectionOnly)
            {
            ResetWorn();
            actor.stats[CharacterCondition.HYGIENE] = 100f;
            Advance(GameCalendarRules.SecondsPerDay);
            float cleanContact = Read(lower.ItemInstanceId).contamination;
            Near(cleanContact, 8f, "Clean covered underwear body contact");
            float durability = Read(lower.ItemInstanceId).durability;
            Require(durability < 100f && durability > 95f, "Actual material wear not applied.");
            ResetWorn();
            actor.stats[CharacterCondition.HYGIENE] = 0f;
            Advance(GameCalendarRules.SecondsPerDay);
            Near(Read(lower.ItemInstanceId).contamination, 16f, "Dirty body doubles contact dirt");
            Near(Read(lower.ItemInstanceId).durability, durability, "Body hygiene must not change fabric wear");
            actor.stats[CharacterCondition.HYGIENE] = 100f;
            Near(Read(lower.ItemInstanceId).contamination, 16f, "Bath must not instantly launder apparel");
            Require(untouched == Conditions(items.GetAllStacks().Where(value => !wornValues.Contains(value.ItemInstanceId))),
                "Stored/unrelated garments accumulated worn condition.");

            ResetWorn();
            work[id] = EnvironmentalWorkKind.General;
            Advance(GameCalendarRules.SecondsPerDay);
            float idleLayerContact = 8f * LayerContact(outerDefinition.Layer);
            Near(Read(outerId).contamination, idleLayerContact + 8f, "Indoor physical work external dirt");
            Require(Read(lower.ItemInstanceId).durability < durability, "Working fabric wear missing.");
            work.Clear();
            ResetWorn();
            climate.Set(rainId);
            grid.SetAreaType(actor.GetNowXY(), GridCellAreaType.ExteriorPath);
            Advance(GameCalendarRules.SecondsPerDay / 20f);
            Require(Read(outerId).moisture > 0f, "Authored outer material did not receive exterior rain.");
            Near(Read(lower.ItemInstanceId).moisture, 0f, "Higher layer must shield covered underwear");
            Near(Read(outerId).contamination, (idleLayerContact + 4f) / 20f, "Rain dirt attribution");
            Set(lower.ItemInstanceId, 100f, 80f, 0f);
            Advance(GameCalendarRules.SecondsPerDay);
            Require(materials.TryGet(Read(lower.ItemInstanceId).primaryMaterialId, out TextileMaterialDefinitionSO textile),
                "Authored textile absent.");
            Near(Read(lower.ItemInstanceId).moisture, Mathf.Max(0f, 80f - 20f * textile.DryingRate),
                "Covered wet underwear still dries under rain shielding");
            ResetWorn();
            grid.SetAreaType(actor.GetNowXY(), GridCellAreaType.Entrance);
            Advance(GameCalendarRules.SecondsPerDay / 20f);
            Near(Read(outerId).moisture, 0f, "Entrance shelters against rain");

            climate.Set(climateDefinitions.Fronts.First(value => value.Kind == WeatherFrontKind.Clear).Id);
            grid.SetAreaType(actor.GetNowXY(), GridCellAreaType.DungeonInterior);
            ResetWorn();
            string beforeFailure = Conditions(items.GetAllStacks());
            int attempts = 0;
            IWorldItemStackRuntime failOnce = Proxy<IWorldItemStackRuntime>((method, args) =>
            {
                if (method.Name == "TrySetInstanceComponent" && ++attempts == 2) return false;
                return Forward(method, items, args);
            });
            Require(!Create(failOnce).TryAdvance(GameCalendarRules.SecondsPerDay, work, out failure)
                && failure.Code == FailureCode.ApparelTransferFailed && attempts >= 3,
                "Second write rejection must fail typed and roll back the first write.");
            Require(beforeFailure == Conditions(items.GetAllStacks()), "Partial condition batch survived rollback.");
            Advance(GameCalendarRules.SecondsPerDay);
            Near(Read(lower.ItemInstanceId).contamination, 8f, "Retry must advance exactly once");
            Require(ownership == Ownership(items), "Condition updates changed count, weight, custody or physical identity.");
            string beforeRestore = Conditions(items.GetAllStacks());
            DungeonPhysicalItemSaveData current = items.Capture();
            items.Restore(JsonUtility.FromJson<DungeonPhysicalItemSaveData>(JsonUtility.ToJson(current)));
            Require(beforeRestore == Conditions(items.GetAllStacks()) && ownership == Ownership(items),
                "Current physical save roundtrip lost conditions or physical ownership.");
            }

            VerifyAutomaticSelection();
            evidence = new[]
            {
                "scope=main-protected-play;real-start-party-physical-items-and-apparel-aggregate",
                "controlled=weather/time/grid-exposure;authored-daily-QA-sources-cover-party-and-one-spare;manual-starter-equip",
                selectionOnly ? "condition-and-physical-tests=NOT_RERUN;use-prior-separate-pass-evidence" : "body-hygiene/contact/work/rain/material-drying/wear=PASS",
                selectionOnly ? "scope=selection-and-service-only" : "real-layer-coverage/entrance-shelter/stored-state-invariance=PASS",
                selectionOnly ? "rollback-retry-current-roundtrip=NOT_RERUN" : "batch-rollback/retry/current-physical-roundtrip/custody-and-count=PASS",
                "automatic-retain/no-spare-preservation/replacement/direct-preference=PASS",
                "actual-service-recovery/partial-work-cancel/no-early-recovery=PASS;controlled-facility-readiness",
                "not-covered=natural-weather-wait;natural-AI-laundry;whole-world-restore;6-adult-economy",
                "result=PASS"
            };
        }
        finally { actor.stats[CharacterCondition.HYGIENE] = hygiene; }

        ApparelInstanceState NewState(ApparelDefinitionSO definition) => new()
        {
            apparelDefinitionId = definition.ApparelId, primaryMaterialId = "textile:shade-cloth",
            sourceKind = TextileSourceKind.Crop, sourceDefinitionId = "textile:shade-cloth",
            craftsmanshipQuality = CraftsmanshipQualityTier.Normal, durability = 100f,
            size = anatomy.GetSize(id), modifications = definition.SupportedModifications,
            designatedWearerCharacterId = id.Value, deterministicBatchHash = 3003UL
        };
        ApparelConditionRuntime Create(IWorldItemStackRuntime port) => new(field, world, scopedApparel,
            port, climate, climateDefinitions, gridProvider, definitions, materials,
            services.Resolve<IApparelMaterialProjector>(), anatomy);
        WorldItemStackSnapshot Stack(ItemInstanceId instance) => items.GetAllStacks()
            .Single(value => value.ItemInstanceId == instance.Value);
        ApparelInstanceState Read(ItemInstanceId instance) => ReadState(Stack(instance));
        void Set(ItemInstanceId instance, float d, float m, float c)
        {
            ApparelInstanceState state = Read(instance);
            state.durability = d; state.moisture = m; state.contamination = c;
            Require(items.TrySetInstanceComponent(Stack(instance).StackId, ApparelItemStateCodec.Create(state)),
                "Controlled starting state failed: " + instance.Value);
        }
        void ResetWorn() { foreach (ItemInstanceId instance in worn) Set(instance, 100f, 0f, 0f); }
        void Advance(float seconds) => Require(runtime.TryAdvance(seconds, work, out DomainFailure value),
            "Condition advance: " + value);
        bool Equipped(ItemInstanceId instance) => apparel.GetEquipped(id).Any(value => value.ItemInstanceId.Equals(instance));
        void VerifyAutomaticSelection()
        {
            ResetWorn();
            ItemInstanceId preferred = outerId;
            Require(command.TrySetDirectPreference(id, ApparelSelectionPurpose.Daily, preferred, out failure),
                "Set actual direct preference: " + failure);
            Set(preferred, 100f, 0f, 40f);
            command.RefreshAutomaticSelections();
            Require(Equipped(preferred), "Harmless dirt caused premature replacement.");
            command.RefreshAutomaticSelections();
            Require(Equipped(preferred), "Unchanged state caused automatic oscillation.");
            WorldItemStackSnapshot[] alternatives = apparel.GetPolicyChoices(id, ApparelSelectionPurpose.Daily)
                .Where(value => value.Layer == outerDefinition.Layer && (value.OccupiedPoints & outerDefinition.OccupiedPoints) != 0
                    && !value.IsEquipped)
                .Select(value => Stack(value.ItemInstanceId))
                .Where(value => !value.Forbidden && value.State is WorldItemStackState.Loose
                    or WorldItemStackState.Stored or WorldItemStackState.FacilityOutputBuffer)
                .ToArray();
            Require(alternatives.Length > 0, "Controlled stock must retain a real spare after other actors are clothed.");
            try
            {
                foreach (WorldItemStackSnapshot alternative in alternatives) items.SetForbidden(alternative.StackId, true);
                Set(preferred, 100f, 0f, 60f);
                command.RefreshAutomaticSelections();
                Require(Equipped(preferred) && apparel.GetPolicy(id).LastFailure.Code != FailureCode.None,
                    "No spare must retain physical garment and report unfulfilled replacement.");
            }
            finally
            {
                foreach (WorldItemStackSnapshot alternative in alternatives)
                    items.SetForbidden(alternative.StackId, alternative.Forbidden);
            }
            command.RefreshAutomaticSelections();
            Require(!Equipped(preferred), "Restored spare availability did not replace dirty apparel.");
            Require(Stack(preferred).Quantity == 1, "Replacement erased displaced garment.");
            Set(preferred, 70f, 0f, 60f);
            VerifyServiceRecovery(services, items, preferred);
            command.RefreshAutomaticSelections();
            Require(Equipped(preferred), "Eligible direct preference did not regain priority.");
        }
        }
        finally
        {
            // The paused test owns this protected Play window. Restore physical
            // authority first, then its exact apparel/policy projection.
            items.Restore(originalPhysical);
            DungeonGameRestoreReport report = new();
            CharacterApparelRestoreCandidate candidate = persistence.PrepareRestoreApparel(
                originalApparel, originalPolicies, report);
            Require(report.Success, "Test cleanup apparel restore: " + string.Join(" | ", report.Errors));
            persistence.PublishRestoreApparel(candidate);
        }
        Require(evidence != null, "Missing completed test evidence.");
        System.IO.Directory.CreateDirectory("Artifacts/QA/wim-implementation");
        System.IO.File.WriteAllLines("Artifacts/QA/wim-implementation/wim-003-apparel-condition-runtime.txt", evidence);
    }

    private static void VerifyServiceRecovery(IObjectResolver services, WorldItemStackRuntime items, ItemInstanceId target)
    {
        Require(services.Resolve<IApparelWorkOrderQuery>().Orders.Count == 0,
            "Protected setup contains pre-existing apparel orders; cannot share lease owner sequence.");
        List<GameObject> objects = new();
        List<BuildableObject> facilities = new();
        ApparelWorkOrderRuntime runtime = null;
        try
        {
            AddFacility("V22_9303_손세탁_수조.asset", ResearchFacilityCommandKind.HandLaundry);
            AddFacility("V22_9304_실내_건조대.asset", ResearchFacilityCommandKind.IndoorDrying);
            AddFacility("V22_9308_수선_접수대.asset", ResearchFacilityCommandKind.ApparelRepair);
            IFacilityCapabilityQuery facilityQuery = Proxy<IFacilityCapabilityQuery>((method, args) =>
            {
                if (args.Length == 1 && args[0] is ResearchFacilityCommandKind kind)
                    return facilities.Where(value => value.BuildingData.ResearchFacilityCommand == kind).ToArray();
                if (args.Length == 2) return Array.Empty<BuildableObject>();
                throw Unexpected(method);
            });
            runtime = new(
                services.Resolve<IApparelDefinitionCatalog>(), services.Resolve<ITextileMaterialCatalog>(),
                items, services.Resolve<ILeasedItemReservationService>(), facilityQuery, services.Resolve<IGameClock>(),
                services.Resolve<IPhysicalItemBatchDispositionService>(), services.Resolve<IApparelPhysicalTransaction>(),
                services.Resolve<IProductionOutputMaximumMassRegistry>(), services.Resolve<IProductionFacilityMutationEpochQuery>(),
                services.Resolve<IBalanceWorkCalculator>(), performance: services.Resolve<ICharacterPerformanceQuery>());
            string custody = Ownership(items);
            string starting = JsonUtility.ToJson(State());
            Require(runtime.CreateLaundry(new[] { target }, false, out string cancelled, out DomainFailure failure),
                "Create cancel witness: " + failure);
            Require(runtime.ApplyWork(cancelled, 6f, out failure), "Partial laundry: " + failure);
            Require(starting == JsonUtility.ToJson(State()), "Partial work applied premature laundering.");
            Require(runtime.Cancel(cancelled, out failure), "Cancel laundry: " + failure);
            Require(starting == JsonUtility.ToJson(State()) && custody == Ownership(items),
                "Cancel changed garment or leaked lease.");
            Require(runtime.CreateLaundry(new[] { target }, false, out string laundry, out failure), "Create laundry: " + failure);
            Require(runtime.ApplyWork(laundry, 11f, out failure), "Laundry partial: " + failure);
            Near(State().contamination, 60f, "Laundry cannot finish before twelve WU");
            Require(runtime.ApplyWork(laundry, 1f, out failure), "Laundry complete: " + failure);
            Near(State().contamination, 0f, "Hand laundry dirt");
            Near(State().moisture, 100f, "Hand laundry wets fabric");
            Near(State().durability, 70f, "Laundry must not repair wear");
            Require(runtime.CreateDrying(new[] { target }, out string drying, out failure), "Create drying: " + failure);
            Require(runtime.ApplyWork(drying, 24f, out failure), "Drying complete: " + failure);
            Near(State().moisture, 0f, "Drying moisture");
            Near(State().durability, 70f, "Drying must not repair wear");
            Require(runtime.CreateRepair(target, out string repair, out failure), "Create repair: " + failure);
            Require(runtime.ApplyWork(repair, 8f, out failure), "Repair complete: " + failure);
            Near(State().durability, 95f, "Repair sixty-plus band recovers twenty-five durability");
            Near(State().contamination, 0f, "Repair must not add dirt");
            Require(custody == Ownership(items), "Recovery changed custody, count or leaked reservations.");

            ApparelInstanceState State() => ReadState(items.GetAllStacks().Single(value => value.ItemInstanceId == target.Value));
            void AddFacility(string filename, ResearchFacilityCommandKind expected)
            {
                BuildingSO data = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/Resources/SO/Building/V22Apparel/" + filename);
                Require(data != null && data.ResearchFacilityCommand == expected, "Authored service facility missing: " + filename);
                GameObject obj = new("WIM003 detached service " + expected);
                objects.Add(obj);
                BuildableObject facility = obj.AddComponent<BuildableObject>();
                facility.ConstructPersistentIdentity(new GuidPersistentIdGenerator());
                facility.ConstructBuildableObject(
                    BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy.Create<IBuildingResearchWorkPort>(),
                    BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy.Create<IBuildingFacilityStateChangePort>(),
                    BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy.Create<IBuildingRoomPolicyPort>(),
                    BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy.Create<IBuildingEquipmentCraftingRuntimePort>(),
                    BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy.Create<IBuildingWorldRegistryPort>(),
                    BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy.Create<IBuildingItemStackPort>(),
                    BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy.Create<IBuildingAbilityRuntimeDispatcher>(),
                    services.Resolve<IGameClock>(),
                    BatchACoreSessionSaveDebugScenarios.DefaultInterfaceProxy.Create<IBuildingPaidFacilityContractPort>(),
                    new FacilityEvolutionStateComponentFactory());
                facility.Initialization(data, new Vector2Int(6, 2));
                facilities.Add(facility);
            }
        }
        finally
        {
            try { runtime?.ResetOrders(); }
            finally { foreach (GameObject obj in objects) UnityEngine.Object.DestroyImmediate(obj); }
        }
    }

    private static ApparelInstanceState ReadState(WorldItemStackSnapshot stack)
    {
        Require(ApparelItemStateCodec.TryRead(stack.Components, out ApparelInstanceState value), "Apparel component missing.");
        return value;
    }
    private static string Conditions(IEnumerable<WorldItemStackSnapshot> values) => string.Join("|", values
        .Where(value => ApparelItemStateCodec.TryRead(value.Components, out _))
        .OrderBy(value => value.StackId, StringComparer.Ordinal)
        .Select(value => value.StackId + ":" + JsonUtility.ToJson(ReadState(value))));
    private static string Ownership(WorldItemStackRuntime items) => string.Join("|", items.GetAllStacks()
        .OrderBy(value => value.StackId, StringComparer.Ordinal)
        .Select(value => value.StackId + ":" + value.ItemInstanceId + ":" + value.ItemId + ":" + value.Quantity
            + ":" + value.UnitWeight.ToString("R", System.Globalization.CultureInfo.InvariantCulture)
            + ":" + value.State + ":" + value.DestinationId + ":" + value.ReservedQuantity));
    private static float LayerContact(ApparelLayer layer) => layer switch
    { ApparelLayer.Underwear => 1f, ApparelLayer.Inner => .75f, ApparelLayer.Outer => .25f, _ => .1f };
    private static void Near(float actual, float expected, string reason) => Require(
        !float.IsNaN(actual) && !float.IsInfinity(actual) && Mathf.Abs(actual - expected) < .0001f,
        reason + ": expected=" + expected + ", actual=" + actual);
    private static void Require(bool condition, string reason)
    { if (!condition) throw new InvalidOperationException("WIM003: " + reason); }
    private static object Forward(MethodInfo method, object target, object[] args)
    {
        try { return method.Invoke(target, args); }
        catch (TargetInvocationException error) when (error.InnerException != null)
        { System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error.InnerException).Throw(); throw; }
    }
    private static Exception Unexpected(MethodInfo method) => new InvalidOperationException("Unexpected test port: " + method.Name);
    private static T Proxy<T>(Func<MethodInfo, object[], object> invoke) where T : class
    {
        T instance = DispatchProxy.Create<T, QueryProxy>();
        ((QueryProxy)(object)instance).Handler = invoke;
        return instance;
    }
    public class QueryProxy : DispatchProxy
    {
        public Func<MethodInfo, object[], object> Handler;
        protected override object Invoke(MethodInfo method, object[] args) => Handler(method, args);
    }
    private sealed class WitnessWorld : ICharacterWorldQuery
    {
        public WitnessWorld(CharacterActor actor) { Characters = new[] { actor }; }
        public int CharacterVersion => 1;
        public IReadOnlyList<CharacterActor> Characters { get; }
    }
    private sealed class ControlledClimate : IClimateQuery
    {
        public ControlledClimate(string front) { WeatherFrontId = front; }
        public void Set(string front) { WeatherFrontId = front; Version++; }
        public int Version { get; private set; }
        public int AbsoluteDay => 1;
        public string ClimateZoneId => "climate:temperate";
        public string WeatherFrontId { get; private set; }
        public int FrontRemainingDays => 1;
        public float OutdoorTemperatureC => 20f;
    }
    private sealed class ControlledField : IEnvironmentalFieldQuery
    {
        public int Version => 1;
        public bool IsInitialized => true;
        public bool TryGetCell(Vector2Int position, out EnvironmentalCellSnapshot value)
        { value = new EnvironmentalCellSnapshot(position, 20f, 100f, 60f); return true; }
        public bool TryGetAverage(IReadOnlyList<Vector2Int> positions, out EnvironmentalCellSnapshot value)
            => TryGetCell(positions.Count > 0 ? positions[0] : Vector2Int.zero, out value);
        public float GetFoodSpoilageMultiplier(Vector2Int position) => 1f;
        public bool IsOrganPreservationSafe(Vector2Int position) => false;
        public bool TryGetTargetTemperature(Vector2Int position, out float value) { value = 0f; return false; }
    }
}
