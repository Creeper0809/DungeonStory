using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using DungeonStory.Foundation;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;

// 공개 조회 변조와 기존 소유자 명령을 함께 확인하는 표적 native 검사.
public static class R29StructureRemediationBatch
{
    public static void RunContracts()
    {
        Require(Application.isBatchMode && !Application.isPlaying
            && DungeonPersistencePaths.IsIsolatedVerification, "R29 requires isolated edit-mode persistence.");
        string output = Environment.GetEnvironmentVariable("DUNGEONSTORY_AUDIT_REMEDIATION_OUTPUT");
        Require(!string.IsNullOrWhiteSpace(output), "Missing evidence directory.");
        Directory.CreateDirectory(output);
        string before = V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest();
        var errors = new List<string>();
        void Observe(string text, string stack, LogType type)
        { if (type is LogType.Error or LogType.Exception or LogType.Assert) errors.Add(text); }
        Application.logMessageReceived += Observe;
        try
        {
            VerifyAuthoredConditions();
            VerifyOwnedQueries();
            VerifyLinksAndAi();
            DiseaseFieldResponseOutboxDebugScenarios.RunFromMenu();
            PhysicalVaccinationOutboxDebugScenarios.RunFromMenu();
            Require(CharacterProgressionDebugScenarios.RunAcquiredTraitAutomaticProducerResumeScenario(),
                "Acquired trait evidence consumption/restore/retry failed.");
            Require(SurgeryRestoreFaultScenarios.Run(), "Surgery atomic restore failed.");
            Debug.Log(SurgeryDebugScenarios.RunReplacementEffectProjectionFocused());
            Require(errors.Count == 0, string.Join(" | ", errors));
            Require(before == V27CurrentSourceEvidenceDigest.ComputeAllScriptsDigest(), "Source changed during verification.");
            Write("contracts.txt", "result=PASS\nsource=" + before
                + "\nconsoleErrors=0\nboundary=controlled native fixtures; actual world is verified separately\n");
            Debug.Log("R29_CONTRACTS=PASS");
        }
        catch (Exception error) { Write("contracts-failure.txt", "result=FAIL\n" + error); throw; }
        finally { Application.logMessageReceived -= Observe; }
        void Write(string file, string text)
        { using var writer = new StreamWriter(new FileStream(Path.Combine(output, file), FileMode.CreateNew)); writer.Write(text); }
    }

    private static void VerifyAuthoredConditions()
    {
        var objects = new List<GameObject>();
        BuildableObject Building(string asset, string id)
        {
            var data = AssetDatabase.LoadAssetAtPath<BuildingSO>("Assets/Resources/SO/Building/" + asset + ".asset");
            Require(data != null, "Missing authored building: " + asset);
            var go = new GameObject(id); go.SetActive(false); objects.Add(go);
            var building = go.AddComponent<BuildableObject>();
            typeof(BuildableObject).GetProperty(nameof(BuildableObject.BuildingData))
                .GetSetMethod(true).Invoke(building, new object[] { data });
            building.RestorePersistentIdentity((BuildingInstanceId)id);
            return building;
        }
        try
        {
            var clinic = Building("ResearchOverhaul/RF74_격리_병동", "building:r29:clinic");
            var lab = Building("ResearchOverhaul/RF76_백신_연구실", "building:r29:lab");
            var diagnosis = Building("ResearchOverhaul/RF73_감염_진단대", "building:r29:diagnosis");
            var query = new AuthoredFacilityQuery(clinic, lab, diagnosis);
            var disease = Resources.Load<DiseaseDefinitionSO>("SO/Population/Diseases/Disease_disease-cave-flu")
                .CreateRuntimeDefinition();
            string response = "response:vaccine:" + disease.Id;
            var candidates = DiseaseFieldResponseRuntime.FindOperationalFacilities(disease, response, query);
            Require(candidates.Contains(clinic) && !candidates.Contains(lab),
                "Vaccine administration must select a Medical buffer, not the authored research-only lab.");
            var ordinary = new DiseaseDefinition("disease:r29", "R29", DiseaseTransmissionRoute.Contact,
                0, 1, 0.1f, 50f, DiseaseTargetSystem.Core, false,
                symptomProfileId: "symptom:r29", fieldResponseIds: new[] { "response:wet-cleaning" });
            var ordinaryFacilities = DiseaseFieldResponseRuntime.FindOperationalFacilities(
                ordinary, "response:wet-cleaning", query);
            Require(ordinaryFacilities.First() == diagnosis && ordinaryFacilities.Contains(clinic),
                "Diagnosis capability and existing isolation fallback are inconsistent.");
            Require(DiseaseFieldResponseRuntime.FindOperationalFacilities(
                ordinary, "response:vaccine:" + ordinary.Id, query).Count == 0, "Unauthored response accepted.");

            var cooker = Building("Modular/P15_조리대", "building:r29:cooker");
            var recipe = Resources.Load<ProductionRecipeSO>("SO/Economy/Recipes/recipe_cheese");
            Require(recipe != null, "Missing authored cheese recipe.");
            var fluids = new ProbeFluids();
            Require(!ProductionCycleUtilityService.ValidateFacilityUtilities(
                recipe, cooker, fluids, fluids, out string blocked)
                && blocked == FailureCode.FluidWastewaterUnavailable.ToString()
                && Mathf.Approximately(fluids.LastWastewater, 0.25f),
                "Facility-only wastewater requirement was skipped.");
            fluids.Available = true;
            Require(ProductionCycleUtilityService.ValidateFacilityUtilities(
                recipe, cooker, fluids, fluids, out _)
                && Mathf.Approximately(fluids.LastWater, 0.25f), "Facility water requirements did not recover.");
        }
        finally { foreach (var go in objects) UnityEngine.Object.DestroyImmediate(go); }
    }

    private static void VerifyOwnedQueries()
    {
        var go = new GameObject("r29-owned-queries"); go.SetActive(false);
        try
        {
            var progression = go.AddComponent<CharacterProgression>();
            var ledger = (CharacterNarrativeLedger)Field(progression, "narrativeLedger");
            ledger.Record(CharacterNarrativeDomain.Work, "work:r29", "subject:r29", "completed");
            Detached(ledger, progression.NarrativeLedger, "narrative-ledger");
            Require(progression.NarrativeLedger.Facts.Count == 1, "Narrative owner changed.");

            var root = new DungeonRuntimeAggregateRootStore();
            var store = new SurgeryAggregateStateStore(root);
            var part = new SurgicalPartInstance { partInstanceId = "part:r29", quality = 0.7f,
                kind = SurgicalPartKind.NaturalOrgan, freshnessSeconds = 100f };
            store.State.Parts.Add(part);
            var parts = Bare<SurgicalPartRuntime>("stateStore", store);
            var installation = FormatterServices.GetUninitializedObject(
                typeof(SurgicalPartRuntime).Assembly.GetType("SurgicalPartInstallationRuntime", true));
            Set(installation, "stateStore", store);
            Set(parts, "installations", installation);
            Detached(part, parts.Parts.Single(), "surgical-parts");
            Require(parts.TryGet(part.partInstanceId, out var found), "Part query failed.");
            Detached(part, found, "surgical-lookup");
            Require(parts.TryReserveForOrder(part.partInstanceId, "order:r29", out _)
                && parts.Parts.Single().reservedOrderId == "order:r29", "Part owner reserve wrote a detached copy.");
            parts.ReleaseReservation(part.partInstanceId, "order:r29");
            Require(parts.Parts.Single().reservedOrderId.Length == 0, "Part owner release did not apply.");
            Detached(part, parts.CaptureParts().Single(), "surgical-save-capture");

            var treasury = new TreasuryEconomyAggregateStateStore(root);
            var procurement = Bare<AutoProcurementRuntime>("stateStore", treasury);
            treasury.Current.StockRules.Add(new AutoProcurementRule { ruleId = "stock:r29", targetQuantity = 7 });
            treasury.Current.WishlistRules.Add(new ProcurementWishlistRule { ruleId = "wish:r29", maximumPrice = 10 });
            treasury.Current.ProcurementResults.Add(new AutoProcurementResult
            { ruleId = "result:r29", physicalStacks = new List<AutoProcurementPhysicalStackRef> { new() } });
            Detached(treasury.Current.StockRules[0], procurement.StockRules[0], "procurement-stock");
            Detached(treasury.Current.WishlistRules[0], procurement.WishlistRules[0], "procurement-wishlist");
            Detached(treasury.Current.ProcurementResults[0], procurement.LastResults[0], "procurement-result");
            procurement.UpsertStockRule(new AutoProcurementRule { ruleId = "stock:r29", targetQuantity = -2 });
            Require(treasury.Current.StockRules[0].targetQuantity == 0, "Stock command normalization was lost.");

            var transactions = new EconomyTransactionLedgerRuntime(new FixedClock(), treasury);
            transactions.RecordSuccess(new EconomyTransactionContext { sourceId = "source:r29" }, 5, 10, 15);
            var owned = treasury.Current.Ledger.Records.Single();
            Detached(owned, transactions.Records.Single(), "economy-ledger");
            Require(transactions.SumSince(0, true) == 5, "Ledger sum changed through a query.");
            var savedLedger = transactions.Capture();
            Require(savedLedger.records.Single().amount == 5, "Ledger save changed through a query.");

            var policyState = root.GetOrCreate(() => new ResourceStockPolicyAggregateState());
            var policy = new ResourceStockPolicyData { itemId = "resource:r29", maximumStock = 20 };
            policyState.ByItemId.Add(policy.itemId, policy); policyState.PolicyView = new[] { policy };
            var policyRuntime = Bare<ResourceStockPolicyRuntime>("aggregateRootStore", root);
            var policyQuery = Bare<ResourceStockPolicyQuery>("aggregateRootStore", root);
            Detached(policy, policyRuntime.Policies.Single(), "policy-runtime");
            Detached(policy, policyQuery.Policies.Single(), "policy-query");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static void VerifyLinksAndAi()
    {
        var go = new GameObject("r29-links-ai"); go.SetActive(false);
        try
        {
            var building = go.AddComponent<BuildableObject>();
            var tags = new[] { "support:r29" };
            var productionLink = new ProductionSupportLinkSnapshot(building, building, "station:r29", "support:r29", tags);
            var serviceLink = new ServiceSupportLinkSnapshot(building, building, "hub:r29", "support:r29", tags);
            tags[0] = "modified";
            Require(productionLink.FeatureTags.Single() == "support:r29"
                && serviceLink.FeatureTags.Single() == "support:r29", "Link retained caller-owned tags.");
            ReadOnly((IList)productionLink.FeatureTags); ReadOnly((IList)serviceLink.FeatureTags);
            Require(typeof(ProductionSupportLinkSnapshot).GetProperties().All(p => p.GetSetMethod() == null)
                && typeof(ServiceSupportLinkSnapshot).GetProperties().All(p => p.GetSetMethod() == null),
                "Link query retains public mutation authority.");
            var world = new EmptyBuildings();
            var production = Bare<ProductionWorkshopRuntime>("buildings", world);
            Set(production, "observedBuildingVersion", 0);
            Set(production, "observedGridVersions", new Dictionary<Grid, int>());
            Set(production, "linksByWorkstation", new Dictionary<BuildableObject, List<ProductionSupportLinkSnapshot>>
            { [building] = new() { productionLink } });
            Set(production, "linksBySupport", new Dictionary<BuildableObject, ProductionSupportLinkSnapshot>
            { [building] = productionLink });
            ReadOnly((IList)production.GetLinks(building));
            Require(production.TryGetLinkForSupport(building, out var p) && p.Support == building,
                "Production link lookup changed.");
            var service = Bare<ServiceRoomLinkRuntime>("buildings", world);
            Set(service, "observedBuildingVersion", 0);
            Set(service, "observedGridVersions", new Dictionary<Grid, int>());
            Set(service, "linksByHub", new Dictionary<BuildableObject, List<ServiceSupportLinkSnapshot>>
            { [building] = new() { serviceLink } });
            Set(service, "linksBySupport", new Dictionary<BuildableObject, ServiceSupportLinkSnapshot>
            { [building] = serviceLink });
            ReadOnly((IList)service.GetLinks(building));
            Require(service.TryGetLinkForSupport(building, out var s) && s.Support == building,
                "Service link lookup changed.");

            var blackboard = go.AddComponent<CharacterBlackboard>();
            var goal = new CharacterMacroGoal { type = CharacterMacroGoalType.SeekFood, reason = "r29" };
            var impulse = new CharacterMoodImpulse { strength = 0.8f, reason = "r29" };
            Set(blackboard, "activeMacroGoal", goal); Set(blackboard, "activeMoodImpulse", impulse);
            Detached(goal, blackboard.ActiveMacroGoal, "ai-goal");
            Detached(impulse, blackboard.ActiveMoodImpulse, "ai-impulse");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static void Detached(object owned, object query, string name)
    {
        string before = JsonConvert.SerializeObject(owned);
        Require(query != null && !ReferenceEquals(owned, query), name + ": raw object returned.");
        Mutate(query);
        Require(JsonConvert.SerializeObject(owned) == before, name + ": query changed owner.");
    }
    private static void Mutate(object value)
    {
        if (value is IList list)
        {
            foreach (object item in list.Cast<object>().ToArray())
                if (item != null && !item.GetType().IsValueType) Mutate(item);
            list.Clear(); return;
        }
        foreach (var field in value.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
        {
            if (field.IsInitOnly) continue;
            if (field.FieldType == typeof(string)) field.SetValue(value, "r29:modified");
            else if (field.FieldType == typeof(int)) field.SetValue(value, 991);
            else if (field.FieldType == typeof(float)) field.SetValue(value, 991f);
            else if (field.FieldType == typeof(long)) field.SetValue(value, 991L);
            else if (field.FieldType == typeof(bool)) field.SetValue(value, !(bool)field.GetValue(value));
            else if (field.GetValue(value) is object nested && !field.FieldType.IsValueType) Mutate(nested);
        }
    }
    private static void ReadOnly(IList value)
    {
        bool rejected = false;
        try { value.Clear(); } catch (NotSupportedException) { rejected = true; }
        Require(rejected, "Query exposes a mutable owner list.");
    }
    private static T Bare<T>(string field, object value) where T : class
    { var result = (T)FormatterServices.GetUninitializedObject(typeof(T)); Set(result, field, value); return result; }
    private static object Field(object owner, string name) => owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(owner);
    private static void Set(object owner, string name, object value) => owner.GetType()
        .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(owner, value);
    private static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }
    private sealed class EmptyBuildings : IBuildingWorldQuery
    { public int BuildingVersion => 0; public IReadOnlyList<BuildableObject> Buildings => Array.Empty<BuildableObject>(); }
    private sealed class FixedClock : IGameClock
    { public float DeltaTime => 0; public float Time => 1; public int FrameCount => 1; public bool IsPaused => true; }
    private sealed class AuthoredFacilityQuery : IFacilityCapabilityQuery
    {
        private readonly BuildableObject[] buildings;
        public AuthoredFacilityQuery(params BuildableObject[] values) => buildings = values;
        public IReadOnlyList<BuildableObject> FindOperational(FacilityCapabilityKind kind, string definition = "") =>
            buildings.Where(b => FacilityCapabilityQuery.SupportsCapability(b, kind)
                && (definition.Length == 0 || definition == "building:" + b.BuildingData.id)).ToArray();
        public IReadOnlyList<BuildableObject> FindOperational(ResearchFacilityCommandKind command) =>
            buildings.Where(b => b.BuildingData.ResearchFacilityCommand == command).ToArray();
    }
    private sealed class ProbeFluids : IFluidInfrastructureTransaction, IFluidWastewaterTransaction
    {
        public bool Available; public float LastWater; public float LastWastewater;
        public bool CanConsume(BuildableObject b, WorldWaterQuality q, float amount, out DomainFailure failure)
        { LastWater = amount; failure = Available ? DomainFailure.None : new DomainFailure(FailureCode.FluidWastewaterUnavailable); return Available; }
        public bool CanAcceptWastewater(BuildableObject b, float amount, out DomainFailure failure)
        { LastWastewater = amount; failure = Available ? DomainFailure.None : new DomainFailure(FailureCode.FluidWastewaterUnavailable); return Available; }
        public bool TryConsume(BuildableObject b, WorldWaterQuality q, float amount, out WorldWaterQuality consumed, out DomainFailure failure)
        { throw new InvalidOperationException("Query consumed water."); }
        public bool TryAdd(BuildableObject b, WorldWaterQuality q, float amount, out float accepted)
        { throw new InvalidOperationException("Query added water."); }
        public bool TryConsumeManualContainer(BuildableObject b, string id, float amount, out DomainFailure failure)
        { throw new InvalidOperationException("Query consumed manual water."); }
        public bool TryAddWastewater(BuildableObject b, float amount, out float accepted, out DomainFailure failure)
        { throw new InvalidOperationException("Query added wastewater."); }
        public bool TryConsumeWastewater(BuildableObject b, float amount, out float consumed)
        { throw new InvalidOperationException("Query consumed wastewater."); }
    }
}

