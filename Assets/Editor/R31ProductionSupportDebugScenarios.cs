using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.Serialization;
using UnityEditor;
using UnityEngine;

public static class R31ProductionSupportDebugScenarios
{
    private const string TwilightBeerPath =
        "Assets/Resources/SO/Economy/Recipes/Workshop/recipe_twilight_beer.asset";
    private const string BreweryPath =
        "Assets/Resources/SO/Building/Modular/P02_양조장.asset";
    private const string FermenterPath =
        "Assets/Resources/SO/Building/ProductionSupport/WS03_수동_발효조.asset";

    public static void RunContracts()
    {
        ProductionRecipeSO recipe =
            AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>(TwilightBeerPath);
        BuildingSO brewery = AssetDatabase.LoadAssetAtPath<BuildingSO>(BreweryPath);
        BuildingSO fermenter =
            AssetDatabase.LoadAssetAtPath<BuildingSO>(FermenterPath);
        R31StructureRemediationBatch.Require(recipe != null,
            "Missing authored twilight-beer recipe.");
        R31StructureRemediationBatch.Require(brewery != null,
            "Missing authored brewery workstation.");
        R31StructureRemediationBatch.Require(fermenter != null,
            "Missing authored WS03 fermenter.");

        BuildingProductionSupportAbility supportAbility =
            fermenter.GetProductionSupportAbility();
        R31StructureRemediationBatch.Require(
            recipe.RecipeId == "recipe:twilight-beer"
            && recipe.ProcessKind == ProductionProcessKind.PassiveBatch
            && recipe.BatchSupportTag == "support:fermenter"
            && recipe.RequiredSupportTags.Count == 1
            && recipe.RequiredSupportTags[0] == "support:fermenter"
            && recipe.RequiredResearchId == "research:cuisine:fermentation",
            "Twilight-beer authored batch requirements changed.");
        R31StructureRemediationBatch.Require(
            brewery.GetProductionWorkstationAbility()?.WorkstationTag
                == "workstation:brewery"
            && supportAbility != null
            && supportAbility.SupportId == "production-support:ws03"
            && supportAbility.kind == ProductionSupportKind.BatchProcessor
            && supportAbility.BatchCapacity == 1
            && supportAbility.Provides(recipe.BatchSupportTag),
            "WS03 authored support capacity changed.");

        var objects = new List<GameObject>();
        try
        {
            BuildableObject workstation = CreateBuilding(
                objects, "r31-production-workstation", brewery,
                new BuildingInstanceId("building:r31:production-workstation"));
            BuildableObject support = CreateBuilding(
                objects, "r31-production-support", fermenter,
                new BuildingInstanceId("building:r31:production-support-ws03"));
            ProductionSupportLinkSnapshot link = new ProductionSupportLinkSnapshot(
                workstation,
                support,
                recipe.WorkstationTag,
                supportAbility.SupportId,
                supportAbility.featureTags);
            IReadOnlyList<ProductionSupportLinkSnapshot> links =
                new[] { link };
            string supportNodeId =
                IndustrialInfrastructureIdentity.GetNodeId(support);
            R31StructureRemediationBatch.Require(
                supportNodeId == "building:r31:production-support-ws03",
                "Fixture support persistent ID drifted.");

            var snapshots = new List<ProductionBillSnapshot>();
            IProductionBillQuery billQuery =
                R31StructureRemediationBatch.StrictProxy<IProductionBillQuery>(
                    (method, arguments) => method.Name == "GetBills"
                        ? snapshots
                        : Unexpected("bill query", method));
            IProductionWorkshopRuntime workshops =
                R31StructureRemediationBatch.StrictProxy<IProductionWorkshopRuntime>(
                    (method, arguments) => ResolveWorkshopCall(
                        method,
                        arguments,
                        workstation,
                        support,
                        supportAbility,
                        recipe,
                        links));
            IEnvironmentalFieldQuery environment =
                R31StructureRemediationBatch.StrictProxy<IEnvironmentalFieldQuery>(
                    (method, arguments) => ResolveEnvironmentCall(
                        method, arguments, support));
            IPowerInfrastructureQuery power =
                R31StructureRemediationBatch.StrictProxy<IPowerInfrastructureQuery>(
                    (method, arguments) => Unexpected("power query", method));
            IFluidInfrastructureTransaction water =
                R31StructureRemediationBatch.StrictProxy<IFluidInfrastructureTransaction>(
                    (method, arguments) => Unexpected("water transaction", method));
            IFluidWastewaterTransaction wastewater =
                R31StructureRemediationBatch.StrictProxy<IFluidWastewaterTransaction>(
                    (method, arguments) => Unexpected("wastewater transaction", method));
            IDomainFailureLocalizer failures =
                R31StructureRemediationBatch.StrictProxy<IDomainFailureLocalizer>(
                    (method, arguments) => Unexpected("failure localizer", method));

            GameObject researchObject = new GameObject("r31-production-research");
            researchObject.SetActive(false);
            objects.Add(researchObject);
            BlueprintResearchRuntime research =
                researchObject.AddComponent<BlueprintResearchRuntime>();
            research.StateForDebug.Projects.RestoreCompleted(
                new ResearchProjectId(recipe.RequiredResearchId));

            ProductionBuildingPanelPresenter presenter = CreatePresenter(
                billQuery,
                workshops,
                research,
                power,
                water,
                wastewater,
                environment,
                failures);
            ProductionCycleUtilityService utility = CreateUtilityService(
                workshops,
                power,
                water,
                wastewater,
                environment);

            VerifyStage(
                ProductionBatchStage.Processing,
                true,
                snapshots,
                presenter,
                utility,
                workstation,
                support,
                supportAbility,
                supportNodeId,
                recipe);
            VerifyStage(
                ProductionBatchStage.Finishing,
                false,
                snapshots,
                presenter,
                utility,
                workstation,
                support,
                supportAbility,
                supportNodeId,
                recipe);
        }
        finally
        {
            foreach (GameObject item in objects)
            {
                if (item != null)
                {
                    UnityEngine.Object.DestroyImmediate(item);
                }
            }
        }
    }

    private static BuildableObject CreateBuilding(
        ICollection<GameObject> objects,
        string name,
        BuildingSO definition,
        BuildingInstanceId persistentId)
    {
        var gameObject = new GameObject(name);
        gameObject.SetActive(false);
        objects.Add(gameObject);
        BuildableObject building = gameObject.AddComponent<BuildableObject>();
        typeof(BuildableObject)
            .GetProperty(nameof(BuildableObject.BuildingData))
            ?.GetSetMethod(true)
            ?.Invoke(building, new object[] { definition });
        R31StructureRemediationBatch.Require(
            building.BuildingData == definition,
            "Fixture building definition was not assigned.");
        CharacterAiEditorTestDependencies.Inject(building);
        building.RestorePersistentIdentity(persistentId);
        building.SetRuntimeGridPosition(Vector2Int.zero);
        return building;
    }

    private static ProductionBuildingPanelPresenter CreatePresenter(
        IProductionBillQuery billQuery,
        IProductionWorkshopRuntime workshops,
        BlueprintResearchRuntime research,
        IPowerInfrastructureQuery power,
        IFluidInfrastructureTransaction water,
        IFluidWastewaterTransaction wastewater,
        IEnvironmentalFieldQuery environment,
        IDomainFailureLocalizer failures)
    {
        var result = (ProductionBuildingPanelPresenter)
            FormatterServices.GetUninitializedObject(
                typeof(ProductionBuildingPanelPresenter));
        SetField(result, "billQuery", billQuery);
        SetField(result, "workshops", workshops);
        SetField(result, "research", research);
        SetField(result, "power", power);
        SetField(result, "water", water);
        SetField(result, "wastewater", wastewater);
        SetField(result, "environment", environment);
        SetField(result, "failureLocalizer", failures);
        return result;
    }

    private static ProductionCycleUtilityService CreateUtilityService(
        IProductionWorkshopRuntime workshops,
        IPowerInfrastructureQuery power,
        IFluidInfrastructureTransaction water,
        IFluidWastewaterTransaction wastewater,
        IEnvironmentalFieldQuery environment)
    {
        var result = (ProductionCycleUtilityService)
            FormatterServices.GetUninitializedObject(
                typeof(ProductionCycleUtilityService));
        SetField(result, "processFluids",
            R31StructureRemediationBatch.StrictProxy<IProcessFluidUseRuntime>(
                (method, arguments) => Unexpected("process fluid runtime", method)));
        SetField(result, "workshops", workshops);
        SetField(result, "power", power);
        SetField(result, "water", water);
        SetField(result, "wastewater", wastewater);
        SetField(result, "environment", environment);
        return result;
    }

    private static void VerifyStage(
        ProductionBatchStage existingStage,
        bool expectsCapacityFailure,
        List<ProductionBillSnapshot> snapshots,
        ProductionBuildingPanelPresenter presenter,
        ProductionCycleUtilityService utility,
        BuildableObject workstation,
        BuildableObject support,
        BuildingProductionSupportAbility supportAbility,
        string supportNodeId,
        ProductionRecipeSO recipe)
    {
        snapshots.Clear();
        snapshots.Add(new ProductionBillSnapshot
        {
            BillId = new ProductionBillId(
                "production-bill:r31:ui-" + existingStage.ToString().ToLowerInvariant()),
            RecipeId = recipe.RecipeId,
            BuildingInstanceId = workstation.RequirePersistentInstanceId(),
            BatchStage = existingStage,
            OccupiedSupportNodeId = supportNodeId
        });

        string uiState = Invoke<string>(
            presenter,
            "FormatSupportState",
            workstation,
            recipe);
        bool uiReportsCapacityFailure = uiState.Contains("배치 용량 부족");
        bool uiReportsReady = uiState.Contains("제작 가능");
        R31StructureRemediationBatch.Require(
            uiReportsCapacityFailure == expectsCapacityFailure,
            "UI support capacity result disagreed for " + existingStage + ": " + uiState);
        R31StructureRemediationBatch.Require(
            uiReportsReady == !expectsCapacityFailure,
            "UI support readiness result disagreed for " + existingStage + ": " + uiState);

        ProductionBillRecord current = ProductionBillRecord.Create(
            new ProductionBillId(
                "production-bill:r31:owner-" + existingStage.ToString().ToLowerInvariant()),
            recipe.RecipeId,
            workstation.RequirePersistentInstanceId(),
            ProductionOrderMode.RepeatCount,
            remainingCycles: 1,
            targetStock: 0,
            batchStage: ProductionBatchStage.Preparing,
            materialDestinationId: string.Empty);
        ProductionBillRecord existing = ProductionBillRecord.Create(
            new ProductionBillId(
                "production-bill:r31:existing-" + existingStage.ToString().ToLowerInvariant()),
            recipe.RecipeId,
            workstation.RequirePersistentInstanceId(),
            ProductionOrderMode.RepeatCount,
            remainingCycles: 1,
            targetStock: 0,
            batchStage: existingStage,
            materialDestinationId: string.Empty);
        existing.SetOccupiedSupportNode(supportNodeId);
        object[] arguments =
        {
            current,
            recipe,
            workstation,
            new[] { existing },
            null,
            null,
            null
        };
        bool ownerSelected = Invoke<bool>(
            utility,
            "TryResolveAvailableBatchSupport",
            arguments);
        BuildableObject selectedSupport = arguments[4] as BuildableObject;
        BuildingProductionSupportAbility selectedAbility =
            arguments[5] as BuildingProductionSupportAbility;
        string failureReason = arguments[6] as string;
        R31StructureRemediationBatch.Require(
            ownerSelected == !expectsCapacityFailure,
            "Owner support capacity result disagreed for " + existingStage + ".");
        R31StructureRemediationBatch.Require(
            expectsCapacityFailure
                ? failureReason == "batch-support-capacity-full"
                : selectedSupport == support
                    && selectedAbility == supportAbility
                    && string.IsNullOrEmpty(failureReason),
            "Owner support selection details disagreed for " + existingStage + ".");
    }

    private static object ResolveWorkshopCall(
        MethodInfo method,
        object[] arguments,
        BuildableObject workstation,
        BuildableObject support,
        BuildingProductionSupportAbility supportAbility,
        ProductionRecipeSO recipe,
        IReadOnlyList<ProductionSupportLinkSnapshot> links)
    {
        switch (method.Name)
        {
            case "GetLinks":
                R31StructureRemediationBatch.Require(
                    ReferenceEquals(arguments[0], workstation),
                    "Support lookup used a different workstation.");
                return links;
            case "HasRequiredSupports":
                R31StructureRemediationBatch.Require(
                    ReferenceEquals(arguments[0], workstation),
                    "Support requirement used a different workstation.");
                arguments[2] = string.Empty;
                return true;
            case "TryResolveSupport":
                R31StructureRemediationBatch.Require(
                    ReferenceEquals(arguments[0], workstation)
                    && (string)arguments[1] == recipe.BatchSupportTag,
                    "Support requirement used a different feature tag.");
                arguments[3] = support;
                arguments[4] = supportAbility;
                return true;
            default:
                return Unexpected("workshop runtime", method);
        }
    }

    private static object ResolveEnvironmentCall(
        MethodInfo method,
        object[] arguments,
        BuildableObject support)
    {
        if (method.Name != "TryGetCell")
        {
            return Unexpected("environment query", method);
        }

        R31StructureRemediationBatch.Require(
            (Vector2Int)arguments[0] == support.centerPos,
            "Temperature lookup used a different support cell.");
        arguments[1] = new EnvironmentalCellSnapshot(
            support.centerPos,
            temperatureC: 18f,
            airQuality: 100f,
            lightLevel: 100f);
        return true;
    }

    private static T Invoke<T>(object target, string name, params object[] arguments)
    {
        MethodInfo method = target.GetType().GetMethod(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        R31StructureRemediationBatch.Require(method != null,
            "Missing private production method: " + name);
        try
        {
            return (T)method.Invoke(target, arguments);
        }
        catch (TargetInvocationException error)
        {
            throw error.InnerException ?? error;
        }
    }

    private static void SetField(object target, string name, object value)
    {
        FieldInfo field = target.GetType().GetField(
            name,
            BindingFlags.Instance | BindingFlags.NonPublic);
        R31StructureRemediationBatch.Require(field != null,
            "Missing production fixture field: " + name);
        field.SetValue(target, value);
    }

    private static object Unexpected(string boundary, MethodInfo method) =>
        throw new InvalidOperationException(
            "Unexpected " + boundary + " call: " + method.Name);
}
