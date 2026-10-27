using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using DungeonStory.Factions;
using Newtonsoft.Json;
using UnityEngine;

// Controlled query-boundary tests. Uninitialized services are used only to
// isolate reads; actual commands and restore lifecycles run in separate suites.
public static class R28RemainingQuerySnapshotDebugScenarios
{
    public static void RunOrThrow()
    {
        VerifyBuildingAndGrowth();
        VerifyMedical();
        VerifyPaidFacility();
        VerifyExteriorAndReturn();
        VerifyInvasion();
        VerifyCampaignAndFactions();
        VerifyHouseholdAndKinship();
        Debug.Log("R28_REMAINING_QUERY_SNAPSHOTS=PASS; boundary=controlled reads; command/restore evidence separate");
    }

    private static void VerifyBuildingAndGrowth()
    {
        var go = new GameObject("R28-query-boundary");
        go.SetActive(false);
        try
        {
            var building = go.AddComponent<BuildableObject>();
            var owned = Field(building, "facilityState");
            if (owned == null)
            {
                var field = FindField(building.GetType(), "facilityState");
                owned = Activator.CreateInstance(field.FieldType);
                field.SetValue(building, owned);
            }
            Check(owned, building.FacilityState, "facility-state");
            var progression = go.AddComponent<CharacterProgression>();
            var growth = (CharacterGrowthState)Field(progression, "growthState");
            growth.activeSkills.Add(new CharacterSkillInstance { id = "r28:active" });
            growth.passiveSkills.Add(new CharacterSkillInstance { id = "r28:passive" });
            growth.ultimate = new CharacterSkillInstance { id = "r28:ultimate" };
            growth.drafts.Add(new CharacterSkillDraft { requestKey = "r28:draft" });
            growth.pendingRequestKeys.Add("r28:pending");
            Check(growth, progression.GrowthState, "growth");
            Check(growth.activeSkills[0], progression.ActiveSkills.Single(), "active-skills");
            Check(growth.passiveSkills[0], progression.PassiveSkills.Single(), "passive-skills");
            Check(growth.ultimate, progression.Ultimate, "ultimate");
            Check(growth.drafts[0], progression.Drafts.Single(), "drafts");
        }
        finally { UnityEngine.Object.DestroyImmediate(go); }
    }

    private static void VerifyMedical()
    {
        var root = new DungeonRuntimeAggregateRootStore();
        var medical = Bare<CharacterMedicalRuntime>("aggregateRootStore", root);
        var state = Property(medical, "writableAggregateState");
        var medicalOrder = SeedList(state, "Orders");
        Set(medicalOrder, "orderId", "r28:medical");
        Check(medicalOrder, medical.ActiveOrders.Single(), "medical-list");
        Require(medical.TryGetOrder("r28:medical", out var medicalQuery), "Medical lookup missing.");
        Check(medicalOrder, medicalQuery, "medical-lookup");
        Require(medical.TrySetSocietyResponseBinding("r28:medical", "", "response:r28", "", out _),
            "Medical owner binding failed.");
        Require(medical.TrySetSocietyResponseBinding("r28:medical", "", "response:r28", "", out _),
            "Medical duplicate binding failed.");
        string bound = Json(medicalOrder);
        Require(!medical.TrySetSocietyResponseBinding("r28:medical", "", "response:other", "", out _)
            && Json(medicalOrder) == bound, "Conflicting medical binding changed owner.");
        Require(medical.TrySetSocietyResponseBinding("r28:medical", "response:r28",
            "response:r28", "receipt:r28", out _), "Medical receipt command failed.");
        Require(medical.TryGetOrder("r28:medical", out var boundQuery)
            && boundQuery.societyResponseReceiptId == "receipt:r28", "Medical receipt was not owned.");
        Require(medical.TrySetSocietyResponseBinding("r28:medical", "response:r28", "", "", out _),
            "Medical binding release failed.");
        Require(medical.TryGetOrder("r28:medical", out var released)
            && released.societyResponseOperationId == "" && released.societyResponseReceiptId == "",
            "Medical binding release changed only a detached copy.");

        var store = new SurgeryAggregateStateStore(new DungeonRuntimeAggregateRootStore());
        var surgeryOrder = SeedList(store.State, "Orders");
        Set(surgeryOrder, "orderId", "r28:surgery");
        var surgery = Bare<SurgeryRuntime>("stateStore", store);
        Check(surgeryOrder, surgery.ActiveOrders.Single(), "surgery-list");
        Require(surgery.TryGetOrder("r28:surgery", out var surgeryQuery), "Surgery lookup missing.");
        Check(surgeryOrder, surgeryQuery, "surgery-lookup");
    }

    private static void VerifyPaidFacility()
    {
        var store = new TreasuryEconomyAggregateStateStore(new DungeonRuntimeAggregateRootStore());
        var contract = SeedList(store.Current, "FacilityContracts");
        var runtime = Bare<PaidFacilityContractRuntime>("stateStore", store);
        Check(contract, runtime.Contracts.Single(), "paid-facility");
    }

    private static void VerifyExteriorAndReturn()
    {
        var exterior = Bare<ExteriorActivityRuntime>();
        var aggregate = typeof(ExteriorActivityRuntime).GetMethod(
            "CreateIncidentAggregate", BindingFlags.Static | BindingFlags.NonPublic).Invoke(null, null);
        Set(exterior, "incidentAggregate", aggregate);
        var incident = new ExteriorIncidentRuntimeState();
        aggregate.GetType().GetMethod("ReplaceAll").Invoke(aggregate,
            new object[] { new[] { incident } });
        Check(incident, exterior.IncidentStates.Single(), "exterior-incident");

        var arrivalRuntime = Bare<OffenseReturnArrivalRuntime>(
            "aggregateRootStore", new DungeonRuntimeAggregateRootStore());
        var arrival = SeedList(Property(arrivalRuntime, "writableAggregateState"), "Arrivals");
        Check(arrival, arrivalRuntime.Arrivals.Single(), "offense-arrival");
    }

    private static void VerifyInvasion()
    {
        var store = new InvasionAggregateStateStore(new DungeonRuntimeAggregateRootStore());
        var runtime = Bare<InvasionCampaignRuntime>("aggregateStateStore", store);
        var branches = (IDictionary)Field(store.Campaign, "Branches");
        var branch = new HumanInvasionBranchState { branchId = "r28:branch" };
        branches.Add(branch.branchId, branch);
        Check(branch, runtime.Branches.Single(), "invasion-branch");
        Require(runtime.TryGetBranch(branch.branchId, out var query), "Invasion lookup missing.");
        Check(branch, query, "invasion-branch-lookup");
        Check(SeedList(store.Campaign, "SupportSites"), runtime.SupportSites.Single(), "support-site");
        Check(SeedList(store.Campaign, "Operations"), runtime.Operations.Single(), "invasion-operation");
    }

    private static void VerifyCampaignAndFactions()
    {
        var catalog = new V20StoryContentCatalog(
            new ResourceGameContentCatalog(new UnityGameContentRootLoader()));
        var campaign = new V20CampaignRuntime(new DungeonRuntimeAggregateRootStore(), catalog);
        var seasonal = Field(Property(campaign, "Seasonal"), "Data");
        var occurrence = (V20ActiveEventSaveData)SeedList(seasonal, "activeEvents");
        occurrence.seasonalWildlifeArrival.observation.wildlifeIds = new[] { "wildlife:r28:first", "wildlife:r28:second" };
        var seasonalQuery = campaign.ActiveSeasonalEvents.Single();
        Check(occurrence, seasonalQuery, "seasonal-event");
        seasonalQuery.seasonalWildlifeArrival.observation.wildlifeIds[0] = "query-only";
        Require(occurrence.seasonalWildlifeArrival.observation.wildlifeIds[0] == "wildlife:r28:first",
            "Seasonal nested array query changed its owner.");
        var societyStore = Field(campaign, "societyState");
        var society = Field(Property(societyStore, "Current"), "Data");
        Check(SeedList(society, "activeEvents"), campaign.ActiveSocietyEvents.Single(), "society-active");
        Check(SeedList(society, "recentResolvedEvents"), campaign.RecentResolvedSocietyEvents.Single(), "society-recent");
        var factionState = SeedList(Field(Property(campaign, "Factions"), "Data"), "factions");
        Set(factionState, "factionId", "r28:faction");
        Require(campaign.TryGetFaction("r28:faction", out var factionQuery), "Campaign faction missing.");
        Check(factionState, factionQuery, "campaign-faction");

        var domain = new FactionDomainRuntime(new DungeonRuntimeAggregateRootStore());
        var state = Property(domain, "State");
        var ownedFaction = new DungeonFactionState { factionId = "r28:faction" };
        ((IDictionary)Field(state, "Factions")).Add("r28:faction", ownedFaction);
        var adapter = Bare<FactionRuntimeApplicationAdapter>("domain", domain);
        Set(adapter, "campaignQuery", campaign);
        Require(adapter.TryGetFaction("r28:faction", out var adapterQuery), "Faction adapter missing.");
        Check(ownedFaction, adapterQuery, "faction-adapter-lookup");
        Check(SeedList(state, "Routes"), adapter.Routes.Single(), "faction-routes");
    }

    private static void VerifyHouseholdAndKinship()
    {
        var household = new CharacterHouseholdAggregate();
        var id = new CharacterId("character:r28:child");
        household.Assign(id, new HouseholdId("household:r28:family"),
            new BuildingInstanceId("building:r28:room"), new BuildingInstanceId("building:r28:bed"));
        Require(household.TryGet(id, out var assignment), "Household assignment missing.");
        string before = Json(household.Capture());
        assignment.bedSlotIndex = 99;
        Require(Json(household.Capture()) == before, "Household query mutated owner.");
        household.MarkPending(id, new HouseholdId("household:r28:family"),
            new CharacterId("character:r28:parent"), default, default, 1, "r28-pending");
        Require(household.TryGetPending(id, out var pending), "Pending household assignment missing.");
        before = Json(household.Capture());
        pending.reasonCode = "query-only";
        Require(Json(household.Capture()) == before, "Pending household query mutated owner.");

        var kinship = new CharacterKinshipAggregate();
        kinship.ArchiveDeath(id, new CharacterSpeciesId("Human"), 1, 2, true, default, 0);
        Require(kinship.TryGetTombstone(id, out var tombstone), "Tombstone missing.");
        before = Json(kinship.Capture());
        tombstone.generation = 99;
        kinship.Tombstones.Single().deathAbsoluteDay = 999;
        Require(Json(kinship.Capture()) == before, "Kinship query mutated owner.");
        var restored = CharacterKinshipAggregate.Restore(
            JsonConvert.DeserializeObject<KinshipWorldSaveData>(before));
        Require(Json(restored.Capture()) == before, "Kinship JSON restore changed state.");
        restored.ClearPartner(id);
        Require(Json(kinship.Capture()) == before, "Restored kinship command changed old owner.");
    }

    private static T Bare<T>(string field = null, object value = null)
    {
        var result = (T)FormatterServices.GetUninitializedObject(typeof(T));
        if (field != null) Set(result, field, value);
        return result;
    }
    private static object SeedList(object owner, string field)
    {
        var list = (IList)Field(owner, field);
        Type itemType = list.GetType().GetGenericArguments().Single();
        var item = Activator.CreateInstance(itemType);
        list.Add(item);
        return item;
    }
    private static object Property(object owner, string name) =>
        owner.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            ?.GetValue(owner) ?? throw new InvalidOperationException("Missing fixture property " + name);
    private static FieldInfo FindField(Type type, string name)
    {
        for (; type != null; type = type.BaseType)
        {
            var field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field != null) return field;
        }
        throw new InvalidOperationException("Missing fixture field " + name);
    }
    private static object Field(object owner, string name)
    {
        var property = owner.GetType().GetProperty(name,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        return property != null ? property.GetValue(owner) : FindField(owner.GetType(), name).GetValue(owner);
    }
    private static void Set(object owner, string name, object value) => FindField(owner.GetType(), name).SetValue(owner, value);
    private static string Json(object value) => JsonConvert.SerializeObject(value);
    private static void Check(object owner, object query, string boundary)
    {
        Require(owner != null && query != null, boundary + " returned no state.");
        string before = Json(owner);
        CheckReferences(owner, query, boundary, 0);
        var text = query.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public)
            .FirstOrDefault(field => field.FieldType == typeof(string) && !field.IsInitOnly);
        if (text != null) text.SetValue(query, "r28-query-mutation");
        Require(Json(owner) == before, boundary + " mutated owner through query.");
    }
    private static void CheckReferences(object owner, object query, string path, int depth)
    {
        if (owner == null || query == null) return;
        Type type = owner.GetType();
        if (type.IsValueType || owner is string || owner is UnityEngine.Object) return;
        // A fixed-size empty array has no mutable elements; Array.Empty<T>() is safe to share.
        if (owner is Array empty && empty.Length == 0) return;
        Require(depth < 40 && !ReferenceEquals(owner, query), path + " shares mutable state.");
        if (owner is IList left && query is IList right)
        {
            for (int i = 0; i < Math.Min(left.Count, right.Count); i++)
                CheckReferences(left[i], right[i], path + "[" + i + "]", depth + 1);
            return;
        }
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public))
            if (!field.IsStatic) CheckReferences(field.GetValue(owner), field.GetValue(query), path + "." + field.Name, depth + 1);
    }
    private static void Require(bool value, string reason)
    { if (!value) throw new InvalidOperationException(reason); }
}

