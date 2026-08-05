#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using DungeonStory.Content.CoreSession;
using UnityEditor;
using UnityEngine;

public static class BatchAContentAuthorityDebugScenarios
{
    private const int ExpectedRunVariableCount = 14;
    private const int ExpectedOwnerDoctrineCount = 3;
    private const int ExpectedServiceProcessCount = 5;

    [MenuItem("DungeonStory/QA/V18/Run Batch A Content Authority Scenarios")]
    public static void RunFromMenu()
    {
        ValidateOrThrow();
        Debug.Log(
            "Batch A content authority PASS: root-authored run variables, "
            + "owner doctrines, service processes, and trail charm definition.");
    }

    public static bool RunAll(bool logSuccess)
    {
        try
        {
            ValidateOrThrow();
            if (logSuccess)
            {
                Debug.Log(
                    "Batch A content authority PASS: strict root catalog projections.");
            }
            return true;
        }
        catch (Exception exception)
        {
            Debug.LogError(
                $"Batch A content authority failed: {exception.GetType().Name} "
                + exception.Message);
            return false;
        }
    }

    public static void ValidateOrThrow()
    {
        ResourceGameContentCatalog content = new(
            new UnityGameContentRootLoader());
        CoreSessionRulesSO authoredRules = content.Domain.CoreSessionRules;
        CoreSessionRulesDefinition coreRules = content.CoreSessionRules;
        IReadOnlyList<string> ruleErrors = authoredRules.ValidateDefinition();
        if (ruleErrors.Count != 0
            || coreRules.Rehearsals.Count != 3
            || coreRules.ExternalProblemBands.Count == 0
            || coreRules.ServiceResearch.Count == 0
            || ReferenceEquals(coreRules, authoredRules)
            || coreRules.RandomInvasionStartDay
                != authoredRules.RandomInvasionStartDay)
        {
            throw new InvalidOperationException(
                "Core-session owners do not share one immutable root-authored rules projection: "
                + string.Join(" | ", ruleErrors));
        }

        ResourceItemDefinitionCatalog items = new(
            content.Items.Definitions);
        ItemDefinitionSO trailCharm = items.GetRequired(
            (ItemDefinitionId)ExternalInfluenceRuntimeApplicationAdapter.TrailCharmItemId);
        if (!string.Equals(
                trailCharm.ItemId,
                ExternalInfluenceRuntimeApplicationAdapter.TrailCharmItemId,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "External influence trail charm did not resolve through the root item catalog.");
        }

        AuthoredGameplayCatalog authored = new(content);
        IRunVariableDefinitionCatalog runVariables = authored;
        IOwnerDoctrineDefinitionCatalog doctrines = authored;
        if (runVariables.All.Count != ExpectedRunVariableCount
            || doctrines.All.Count != ExpectedOwnerDoctrineCount)
        {
            throw new InvalidOperationException(
                "Root-authored run-variable/doctrine counts changed: "
                + $"{runVariables.All.Count}/{doctrines.All.Count}.");
        }
        foreach (RunVariableDefinition definition in runVariables.All)
        {
            if (!ReferenceEquals(
                    definition,
                    runVariables.Require(definition.id)))
            {
                throw new InvalidOperationException(
                    $"Run-variable projection is not stable for '{definition.id}'.");
            }
        }
        foreach (OwnerDoctrineDefinition definition in doctrines.All)
        {
            if (!ReferenceEquals(
                    definition,
                    doctrines.Require(definition.id)))
            {
                throw new InvalidOperationException(
                    $"Owner-doctrine projection is not stable for '{definition.id}'.");
            }
        }

        ResourceServiceProcessCatalog services = new(content);
        if (services.All.Count != ExpectedServiceProcessCount)
        {
            throw new InvalidOperationException(
                $"Expected {ExpectedServiceProcessCount} service processes, "
                + $"found {services.All.Count}.");
        }
        foreach (ServiceProcessSO process in services.All)
        {
            if (process.ValidateDefinition().Count != 0
                || !ReferenceEquals(process, services.Require(process.ProcessId)))
            {
                throw new InvalidOperationException(
                    $"Service process '{process?.name}' is invalid or unstable.");
            }
        }

        GameContentDataCatalog compatibility = new(content);
        IReadOnlyDictionary<int, BuildingSO> buildings =
            compatibility.GetData<BuildingSO>();
        if (buildings.Count == 0
            || !ReferenceEquals(
                buildings,
                compatibility.GetData<BuildingSO>()))
        {
            throw new InvalidOperationException(
                "The numeric building compatibility view is missing or is not a stable root-catalog projection.");
        }
        if (buildings is IDictionary<int, BuildingSO> mutableBuildings)
        {
            BuildingSO sample = buildings.Values.First();
            ExpectFailure(
                () => mutableBuildings.Add(int.MinValue, sample),
                "mutable root-catalog compatibility view");
        }

        ExpectFailure(
            () => new ResourceServiceProcessCatalog(
                new ServiceContentFake(
                    new ServiceProcessSO[] { null })),
            "missing service process");
        ExpectFailure(
            () => new ResourceServiceProcessCatalog(
                new ServiceContentFake(
                    new[] { services.All[0], services.All[0] })),
            "duplicate service process");
        ExpectFailure(
            () => services.Require("service:missing"),
            "unknown service process");

        VerifyShopCategoryDriftFailsClosed();
    }

    private static void VerifyShopCategoryDriftFailsClosed()
    {
        ResourceItemDefinitionSO item =
            ScriptableObject.CreateInstance<ResourceItemDefinitionSO>();
        SaleItem saleItem = ScriptableObject.CreateInstance<SaleItem>();
        try
        {
            item.ConfigureCore(
                "debug:item:shop-category-authority",
                "Shop category authority fixture",
                string.Empty,
                StockCategory.General,
                1,
                1f,
                1);
            saleItem.id = 989001;
            saleItem.itemName = "Shop category authority fixture";
            saleItem.SetItemDefinitionId(item.ItemId);
            saleItem.category = StockCategory.Food;

            ResourceItemDefinitionCatalog itemCatalog = new(
                new ItemDefinitionSO[] { item });
            SaleItemDataCatalogFake dataCatalog = new(saleItem);
            bool rejectedWithExplicitReason = false;
            try
            {
                _ = new ShopStockCatalog(dataCatalog, itemCatalog);
            }
            catch (InvalidOperationException exception)
            {
                rejectedWithExplicitReason = exception.Message.Contains(
                    "does not match physical item",
                    StringComparison.Ordinal);
            }

            if (!rejectedWithExplicitReason)
            {
                throw new InvalidOperationException(
                    "Shop catalog did not explicitly reject SaleItem category drift from the canonical item definition.");
            }

            saleItem.category = StockCategory.General;
            ShopStockCatalog validCatalog = new(dataCatalog, itemCatalog);
            if (validCatalog.GetStockCategory(saleItem.id) != item.StockCategory)
            {
                throw new InvalidOperationException(
                    "Shop stock category did not project the canonical item-definition category.");
            }
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(saleItem);
            UnityEngine.Object.DestroyImmediate(item);
        }
    }

    private static void ExpectFailure(Action action, string label)
    {
        try
        {
            action();
        }
        catch (Exception)
        {
            return;
        }
        throw new InvalidOperationException(
            $"Content authority accepted {label}.");
    }

    private sealed class ServiceContentFake : IGameContentCatalog
    {
        private readonly IReadOnlyList<ServiceProcessSO> processes;

        public ServiceContentFake(IReadOnlyList<ServiceProcessSO> processes)
        {
            this.processes = processes
                ?? throw new ArgumentNullException(nameof(processes));
        }

        public GameContentCatalogSO Root => null;
        public ItemDefinitionCatalogSO Items => null;
        public WorldInteractionPresentationCatalogSO WorldPresentation => null;
        public CharacterSkillSystemSettingsSO CharacterSkillSettings => null;
        public GameMediaCatalogSO Media => null;
        public GameDomainContentCatalogSO Domain => null;
        public IReadOnlyList<ServiceProcessSO> ServiceProcesses => processes;
        public RoomEnvironmentSettingsSO RoomEnvironmentSettings => null;
        public IReadOnlyList<OffenseSiteArchetypeSO> SiteArchetypes =>
            Array.Empty<OffenseSiteArchetypeSO>();
        public IReadOnlyList<OffenseUrgentSiteDefinitionSO> UrgentSites =>
            Array.Empty<OffenseUrgentSiteDefinitionSO>();
        public IReadOnlyList<OffenseDecisionCardSO> DecisionCards =>
            Array.Empty<OffenseDecisionCardSO>();
        public IReadOnlyList<OffenseEncounterSO> Encounters =>
            Array.Empty<OffenseEncounterSO>();

        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject
        {
            return typeof(T) == typeof(ServiceProcessSO)
                ? processes.Cast<T>().ToArray()
                : Array.Empty<T>();
        }

        public T RequireSingle<T>() where T : ScriptableObject
        {
            IReadOnlyList<T> all = GetAll<T>();
            return all.Count == 1
                ? all[0]
                : throw new InvalidOperationException(
                    $"Expected one {typeof(T).Name}, found {all.Count}.");
        }
    }

    private sealed class SaleItemDataCatalogFake : IDataCatalog
    {
        private readonly IReadOnlyDictionary<int, SaleItem> saleItems;

        public SaleItemDataCatalogFake(SaleItem saleItem)
        {
            saleItems = new Dictionary<int, SaleItem>
            {
                [saleItem.id] = saleItem
            };
        }

        public IReadOnlyDictionary<int, T> GetData<T>()
            where T : DataScriptableObject
        {
            if (typeof(T) != typeof(SaleItem))
            {
                throw new InvalidOperationException(
                    $"Fixture has no {typeof(T).Name} definitions.");
            }

            return (IReadOnlyDictionary<int, T>)(object)saleItems;
        }
    }
}
#endif
