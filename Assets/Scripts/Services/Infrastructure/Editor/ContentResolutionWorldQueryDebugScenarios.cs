#if UNITY_EDITOR
using System;
using System.Linq;
using Newtonsoft.Json;
using UnityEngine;
using VContainer;

internal static class ContentResolutionWorldQueryDebugScenarios
{
    internal static string VerifyActualQuery(IObjectResolver container)
    {
        var campaign = container.Resolve<V20CampaignRuntime>();
        var catalog = container.Resolve<V20StoryContentCatalog>();
        var items = container.Resolve<IWorldItemStackRuntime>();
        var query = container.Resolve<IFactionContractDeliveryQuery>();
        Require(ReferenceEquals(query, container.Resolve<IContentResolutionService>()),
            "The contract query and content commands have different production owners.");
        string factions = JsonUtility.ToJson(campaign.CaptureFactions());
        string physical = JsonUtility.ToJson(items.Capture());
        var views = catalog.Arcs.OrderBy(value => value.factionId, StringComparer.Ordinal)
            .Select(value => new { value.factionId, contracts = query.GetContracts(value.factionId) })
            .ToArray();
        Require(views.Length > 0 && views.All(value => value.contracts.Count > 0
                && value.contracts.Select(contract => contract.ContractId).Distinct().Count()
                    == value.contracts.Count),
            "The actual contract query omitted authored contracts or duplicated a row.");
        Require(query.GetContracts("qa:missing-faction").Count == 0,
            "The actual contract query invented an unknown faction.");
        Require(JsonUtility.ToJson(campaign.CaptureFactions()) == factions
                && JsonUtility.ToJson(items.Capture()) == physical,
            "Reading the actual contract screen changed campaign or physical state.");
        Debug.Log("ARCHITECTURE_CONTENT_ACTUAL_QUERY=PASS; readonly production facade; authored rows; unknown faction");
        return JsonConvert.SerializeObject(views);
    }

    internal static void VerifyRootReplacement(IObjectResolver container)
    {
        var catalog = container.Resolve<V20StoryContentCatalog>();
        var campaign = new V20CampaignRuntime(new DungeonRuntimeAggregateRootStore(), catalog);
        var query = new V20FactionContractQuery(campaign, catalog,
            container.Resolve<IWorldItemStackRuntime>(), container.Resolve<IFacilityCapabilityQuery>(),
            container.Resolve<IWorldDropZoneQuery>());
        var contract = catalog.Contracts.First(value => string.IsNullOrEmpty(value.seasonalEventId));
        var baseline = campaign.CaptureFactions();
        string original = JsonUtility.ToJson(baseline);
        FactionContractView previous = query.GetContracts(contract.factionId)
            .Single(value => value.ContractId == contract.StableId);
        Require(!previous.IsCompleted && !previous.IsActive && !previous.IsFailed,
            "The controlled contract query did not start with a fresh campaign.");

        var completed = JsonUtility.FromJson<FactionCampaignWorldSaveData>(original);
        completed.factions.Single(value => value.factionId == contract.factionId)
            .completedContractIds.Add(contract.StableId);
        campaign.PublishFactions(campaign.PrepareFactions(completed));
        string published = JsonUtility.ToJson(campaign.CaptureFactions());
        FactionContractView current = query.GetContracts(contract.factionId)
            .Single(value => value.ContractId == contract.StableId);
        Require(current.IsCompleted && !current.CanAccept && !previous.IsCompleted
                && JsonUtility.ToJson(campaign.CaptureFactions()) == published,
            "The query retained an obsolete root, rewrote its prior result, or mutated the replacement.");

        campaign.PublishFactions(campaign.PrepareFactions(baseline));
        Require(!query.GetContracts(contract.factionId)
                .Single(value => value.ContractId == contract.StableId).IsCompleted
                && JsonUtility.ToJson(campaign.CaptureFactions()) == original,
            "The same query did not follow the restored root.");
        Debug.Log("ARCHITECTURE_CONTENT_QUERY_ROOT=PASS; controlled detached campaign; production catalog/physical/facility queries; replace/restore; immutable prior result");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
#endif
