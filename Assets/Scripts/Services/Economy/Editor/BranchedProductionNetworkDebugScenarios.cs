#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class BranchedProductionNetworkDebugScenarios
{
    public static ProductionNetworkCoverageSnapshot LastCoverage { get; private set; }

    private static readonly HashSet<string> StrategicIntermediates = new(
        new[]
        {
            "material:black-powder",
            "component:machine-parts",
            "component:precision-parts",
            "component:rune-conductor",
            "component:growth-frame"
        },
        StringComparer.Ordinal);

    [MenuItem("Tools/DungeonStory/Economy/Validate Branched Production V3")]
    public static void RunFromMenu()
    {
        IReadOnlyList<string> failures = Validate();
        if (failures.Count > 0)
        {
            throw new InvalidOperationException(string.Join("\n", failures));
        }
        Debug.Log("Branched production V3 validation passed.");
    }

    public static IReadOnlyList<string> Validate()
    {
        ResourceItemDefinitionSO[] items = AssetDatabase.FindAssets("t:ResourceItemDefinitionSO")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ResourceItemDefinitionSO>)
            .Where(item => item != null)
            .ToArray();
        ProductionRecipeSO[] recipes = Resources.LoadAll<ProductionRecipeSO>(
            ProductionRecipeSO.ResourcePath);
        CombatEquipmentDefinitionSO[] equipment =
            Resources.LoadAll<CombatEquipmentDefinitionSO>(
                ResourceCombatEquipmentCatalog.ResourcePath);
        CraftMaterialDefinitionSO[] craftMaterials =
            Resources.LoadAll<CraftMaterialDefinitionSO>(
                CraftMaterialDefinitionSO.ResourcePath);
        CropDefinitionSO[] crops = Resources.LoadAll<CropDefinitionSO>(
            CropDefinitionSO.ResourcePath);
        SurgicalProcedureSO[] procedures = Resources.LoadAll<SurgicalProcedureSO>(
            SurgicalProcedureSO.ResourcePath);
        Dictionary<string, ResourceItemDefinitionSO> byId = items
            .Where(item => item != null && !string.IsNullOrWhiteSpace(item.ItemId))
            .GroupBy(item => item.ItemId, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> consumers = byId.Keys.ToDictionary(
            id => id,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        Dictionary<string, List<ProductionRecipeSO>> producers = byId.Keys.ToDictionary(
            id => id,
            _ => new List<ProductionRecipeSO>(),
            StringComparer.Ordinal);
        Dictionary<string, HashSet<string>> acquisitionProducers = byId.Keys.ToDictionary(
            id => id,
            _ => new HashSet<string>(StringComparer.Ordinal),
            StringComparer.Ordinal);
        List<string> failures = new();

        string[] duplicateIds = items
            .Where(item => item != null)
            .GroupBy(item => item.ItemId, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();
        foreach (string duplicate in duplicateIds)
        {
            failures.Add($"duplicate item id: {duplicate}");
        }

        foreach (IGrouping<string, ProductionRecipeSO> duplicate in recipes
                     .Where(recipe => recipe != null)
                     .GroupBy(recipe => recipe.RecipeId, StringComparer.Ordinal)
                     .Where(group => group.Count() > 1))
        {
            failures.Add($"duplicate recipe id: {duplicate.Key}");
        }

        foreach (IGrouping<int, ProductionRecipeSO> duplicate in recipes
                     .Where(recipe => recipe != null)
                     .GroupBy(recipe => recipe.id)
                     .Where(group => group.Count() > 1))
        {
            failures.Add(
                $"duplicate recipe numeric id: {duplicate.Key} "
                + $"({string.Join(", ", duplicate.Select(recipe => recipe.name))})");
        }

        BuildingSO[] buildings = AssetDatabase.FindAssets(
                "t:BuildingSO",
                new[] { "Assets/Resources/SO/Building" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<BuildingSO>)
            .Where(building => building != null)
            .ToArray();
        HashSet<string> workstationTags = buildings
            .Select(building => building.GetProductionWorkstationAbility()?.WorkstationTag)
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToHashSet(StringComparer.Ordinal);

        foreach (ProductionRecipeSO recipe in recipes.Where(recipe => recipe != null))
        {
            bool requiresPhysicalWorkstation = recipe.RecipeId.StartsWith(
                "recipe:",
                StringComparison.Ordinal)
                && !recipe.RecipeId.StartsWith("recipe:surgery:", StringComparison.Ordinal);
            if (requiresPhysicalWorkstation
                && !workstationTags.Contains(recipe.WorkstationTag)
                && !buildings.Any(building => building.HasSemanticTag(recipe.FacilityTag)))
            {
                failures.Add($"{recipe.RecipeId}: missing workstation tag {recipe.WorkstationTag}");
            }
            foreach (ItemAmountDefinition input in recipe.Inputs)
            {
                if (input == null)
                {
                    continue;
                }
                if (input.ItemId.StartsWith("stock-item:", StringComparison.Ordinal))
                {
                    failures.Add($"{recipe.RecipeId}: abstract input {input.ItemId}");
                }
                if (!byId.ContainsKey(input.ItemId))
                {
                    failures.Add($"{recipe.RecipeId}: unknown input {input.ItemId}");
                    continue;
                }
                consumers[input.ItemId].Add(recipe.RecipeId);
            }
            foreach (ProductionOutputDefinition output in recipe.Outputs)
            {
                if (output == null)
                {
                    failures.Add($"{recipe.RecipeId}: unknown output {output?.ItemId}");
                    continue;
                }
                if (!producers.TryGetValue(output.ItemId, out List<ProductionRecipeSO> list))
                {
                    if (!recipe.RecipeId.StartsWith(
                            "recipe:surgery:",
                            StringComparison.Ordinal))
                    {
                        failures.Add($"{recipe.RecipeId}: unknown output {output.ItemId}");
                    }
                    continue;
                }
                list.Add(recipe);
            }
        }

        foreach (CombatEquipmentDefinitionSO definition in equipment.Where(value => value != null))
        {
            foreach (CraftMaterialDefinitionSO material in craftMaterials
                         .Where(definition.AllowsMaterial))
            {
                if (consumers.TryGetValue(material.ItemId, out HashSet<string> links))
                {
                    links.Add($"equipment:{definition.EquipmentId}");
                }
            }
            foreach (ItemAmountDefinition component in definition.RequiredComponentInputs)
            {
                if (component != null && consumers.TryGetValue(component.ItemId, out HashSet<string> links))
                {
                    links.Add($"equipment:{definition.EquipmentId}");
                }
            }
            if (definition is CombatWeaponSO weapon)
            {
                foreach (ItemDefinitionId ammunitionItemId in
                         weapon.CompatibleAmmunitionItemIds)
                {
                    if (consumers.TryGetValue(
                            ammunitionItemId.Value,
                            out HashSet<string> ammunitionLinks))
                    {
                        ammunitionLinks.Add(
                            $"equipment-ammunition:{weapon.EquipmentId}");
                    }
                }
            }
        }
        foreach (SurgicalProcedureSO procedure in procedures.Where(value => value != null))
        {
            foreach (SurgicalMaterialRequirement material in procedure.Materials)
            {
                if (material != null && consumers.TryGetValue(material.itemId, out HashSet<string> links))
                {
                    links.Add($"medical:{procedure.ProcedureId}");
                }
            }
        }
        foreach (EnvironmentalWorkwearSO workwear in
                 Resources.LoadAll<EnvironmentalWorkwearSO>(
                     EnvironmentalWorkwearSO.ResourcePath))
        {
            if (workwear != null
                && consumers.TryGetValue(
                    workwear.ItemDefinitionId,
                    out HashSet<string> links))
            {
                links.Add($"environment-workwear:{workwear.WorkwearId}");
            }
        }
        ApparelDefinitionSO[] apparelDefinitions = AssetDatabase.FindAssets(
                "t:ApparelDefinitionSO",
                new[] { "Assets/Resources/SO/Apparel" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ApparelDefinitionSO>)
            .Where(value => value != null)
            .ToArray();
        TextileMaterialDefinitionSO[] textileMaterials = AssetDatabase.FindAssets(
                "t:TextileMaterialDefinitionSO",
                new[] { "Assets/Resources/SO/Apparel" })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<TextileMaterialDefinitionSO>)
            .Where(value => value != null)
            .ToArray();
        foreach (ApparelDefinitionSO apparelDefinition in apparelDefinitions)
        {
            if (consumers.TryGetValue(
                    apparelDefinition.PhysicalItemId,
                    out HashSet<string> apparelLinks))
            {
                apparelLinks.Add($"apparel-equip:{apparelDefinition.ApparelId}");
            }
            foreach (TextileMaterialDefinitionSO textileMaterial in textileMaterials)
            {
                if ((textileMaterial.Tags & apparelDefinition.AllowedMaterialTags) != 0
                    && consumers.TryGetValue(
                        textileMaterial.PhysicalItemId,
                        out HashSet<string> materialLinks))
                {
                    materialLinks.Add(
                        $"apparel-material:{apparelDefinition.ApparelId}");
                }
            }
        }
        AddConsumer(consumers, "tool:sewing-kit", "apparel-repair:tool");
        AddConsumer(consumers, "material:sewing-thread", "apparel-repair:thread");
        AddConsumer(consumers, "material:sewing-thread", "apparel-alteration:thread");
        AddConsumer(consumers, "material:mending-scrap", "apparel-repair:patch");
        AddConsumer(consumers, "material:mending-scrap", "apparel-alteration:patch");
        foreach (PhysicalItemRuntimeConsumerCatalog.Link link in
                 PhysicalItemRuntimeConsumerCatalog.All)
        {
            AddConsumer(consumers, link.ItemId, link.OwnerId);
        }
        foreach (GuestRequestDefinitionSO guest in
                 Resources.LoadAll<GuestRequestDefinitionSO>(string.Empty))
        {
            AddConsumableRequirementLinks(
                guest?.StableId,
                guest?.serviceRequirements,
                consumers);
        }
        foreach (FactionContractDefinitionSO contract in
                 Resources.LoadAll<FactionContractDefinitionSO>(string.Empty))
        {
            AddConsumableRequirementLinks(
                contract?.StableId,
                contract?.completionRequirements,
                consumers);
        }
        foreach (CropDefinitionSO crop in crops.Where(value => value != null))
        {
            if (producers.TryGetValue(crop.HarvestItemId, out List<ProductionRecipeSO> list)
                && list.Count == 0)
            {
                list.Add(null);
            }
            if (consumers.TryGetValue(crop.SeedItemId, out HashSet<string> seedLinks))
            {
                seedLinks.Add($"crop-sowing:{crop.CropId}");
            }
        }

        foreach (BuildingSO building in buildings)
        {
            BuildingWorkAmountAbility workAmount =
                building.GetAbility<BuildingWorkAmountAbility>();
            if (workAmount == null || workAmount.ConstructionMaterials.Count == 0)
            {
                failures.Add(
                    $"building:{building.id}: missing concrete construction materials");
                continue;
            }

            HashSet<string> materialIds = new(StringComparer.Ordinal);
            foreach (ItemAmountDefinition material in workAmount.ConstructionMaterials)
            {
                string itemId = material?.ItemId?.Trim() ?? string.Empty;
                if (material == null
                    || material.Amount <= 0
                    || itemId.Length == 0
                    || itemId.StartsWith("stock-item:", StringComparison.Ordinal)
                    || !consumers.TryGetValue(itemId, out HashSet<string> links))
                {
                    failures.Add(
                        $"building:{building.id}: invalid construction material {itemId}");
                    continue;
                }

                if (!materialIds.Add(itemId))
                {
                    failures.Add(
                        $"building:{building.id}: duplicate construction material {itemId}");
                    continue;
                }

                links.Add($"construction:{building.id}");
            }

            BuildingProductionWorkstationAbility workstation =
                building.GetProductionWorkstationAbility();
            if (workstation != null
                && consumers.TryGetValue(
                    workstation.StockSensorInstallationItemId,
                    out HashSet<string> stockSensorLinks))
            {
                stockSensorLinks.Add($"production-stock-sensor:{building.id}");
            }
            if (workstation != null
                && EquipmentProgressionWorkstationTags.IsModuleProcess(
                    workstation.WorkstationTag)
                && consumers.TryGetValue(
                    PhysicalItemIds.EquipmentModule,
                    out HashSet<string> moduleProcessLinks))
            {
                moduleProcessLinks.Add(
                    $"equipment-module-process:{building.id}:{workstation.WorkstationTag}");
            }

            BuildingCropPlotAbility cropPlot =
                building.GetAbility<BuildingCropPlotAbility>();
            foreach (ItemAmountDefinition cycleSupply in
                     cropPlot?.CycleSupplyInputs
                     ?? Array.Empty<ItemAmountDefinition>())
            {
                if (cycleSupply != null
                    && consumers.TryGetValue(
                        cycleSupply.ItemId,
                        out HashSet<string> cropSupplyLinks))
                {
                    cropSupplyLinks.Add($"crop-cycle-supply:{building.id}");
                }
            }

            BuildingEquipmentMaintenanceAbility maintenance =
                building.GetAbility<BuildingEquipmentMaintenanceAbility>();
            if (maintenance != null
                && consumers.TryGetValue(
                    maintenance.RepairSupplyItemId,
                    out HashSet<string> maintenanceLinks))
            {
                maintenanceLinks.Add($"equipment-maintenance:{building.id}");
            }

            DefenseFacilityData defense = building.Defense;
            if (defense?.UsesPhysicalSupply == true
                && consumers.TryGetValue(
                    defense.supplyItemId?.Trim() ?? string.Empty,
                    out HashSet<string> defenseSupplyLinks))
            {
                defenseSupplyLinks.Add($"defense-supply:{building.id}");
            }
        }

        string expeditionToolItemId = OffenseSupplyCatalog.GetPhysicalItemId(
            OffenseSupplyType.Tools);
        if (consumers.TryGetValue(
                expeditionToolItemId,
                out HashSet<string> expeditionToolLinks))
        {
            expeditionToolLinks.Add("offense-supply:tools");
        }

        if (acquisitionProducers.TryGetValue(
                PhysicalItemIds.EquipmentModule,
                out HashSet<string> moduleRewardSources))
        {
            foreach (EquipmentExpeditionRewardKind kind in
                     Enum.GetValues(typeof(EquipmentExpeditionRewardKind)))
            {
                moduleRewardSources.Add(
                    EquipmentExpeditionRewardSourceIds.ForModule(kind));
            }
        }

        foreach (ResourceItemDefinitionSO item in items.Where(value => value != null))
        {
            HashSet<string> links = consumers[item.ItemId];
            if (item.TryGetFeature(out FoodItemFeature _))
            {
                links.Add($"item-consumption:{item.ItemId}");
            }
            if (item.TryGetFeature(out MedicineItemFeature medicine)
                && (medicine.supportsInjuryTreatment
                    || medicine.treatmentPotency > 0f
                    || medicine.infectionReduction > 0f
                    || medicine.detoxReduction > 0f
                    || medicine.painReduction > 0f))
            {
                links.Add($"item-treatment:{item.ItemId}");
            }
            if (item.TryGetFeature(out MedicalProcedureSupplyItemFeature procedureSupply)
                && !string.IsNullOrWhiteSpace(procedureSupply.procedureId))
            {
                links.Add($"medical-procedure:{procedureSupply.procedureId.Trim()}");
            }
            if (item.TryGetFeature(out CropTreatmentItemFeature cropTreatment))
            {
                links.Add($"crop-treatment:{cropTreatment.treatmentKind}");
            }
            if (item.TryGetFeature(out PathogenSampleItemFeature sample)
                && !string.IsNullOrWhiteSpace(sample.diseaseId))
            {
                acquisitionProducers[item.ItemId].Add(
                    $"diagnostic-sampling:{sample.diseaseId.Trim()}");
            }
            if (string.Equals(
                item.ItemId,
                "medicine:mycelial-culture-pack",
                StringComparison.Ordinal))
            {
                acquisitionProducers[item.ItemId].Add(
                    "medical-procedure:mycelial-culture-harvest");
            }
            if (item.TryGetFeature(out SubstanceItemFeature substance)
                && !string.IsNullOrWhiteSpace(substance.substanceId))
            {
                links.Add($"substance:{substance.substanceId.Trim()}");
            }
            if (item.TryGetFeature(out InstallationItemFeature installation)
                && installation.buildingDefinitionId >= 0)
            {
                links.Add(
                    $"building-installation:{installation.buildingDefinitionId}");
            }
            if (item.TryGetFeature(out BlueprintItemFeature blueprint)
                && !string.IsNullOrWhiteSpace(blueprint.targetResearchId))
            {
                links.Add(
                    $"research-blueprint:{blueprint.targetResearchId.Trim()}");
            }
            if (item.Kind != ResourceItemKind.Intermediate
                && item.TryGetFeature(out MarketItemFeature market)
                && market.saleRate > 0f
                && item.UnitPrice > 0)
            {
                links.Add($"market-sale:{item.ItemId}");
            }
            if (string.Equals(
                item.ItemId,
                EquipmentProgressionItemIds.LineageSeal,
                StringComparison.Ordinal))
            {
                links.Add("equipment-lineage:history-transfer");
            }

            foreach (BuildingSO building in buildings)
            {
                BuildingFacilitySupplyAbility supply =
                    building.GetAbility<BuildingFacilitySupplyAbility>();
                if (supply?.profiles == null)
                {
                    continue;
                }
                foreach (FacilitySupplyProfile profile in supply.profiles)
                {
                    if (profile != null && profile.Allows(item))
                    {
                        links.Add(
                            $"facility:{building.id}:supply:{profile.kind.ToString().ToLowerInvariant()}");
                    }
                }
            }
        }

        HashSet<string> reusablePackageContainers = items
            .Where(value => value != null
                && value.TryGetFeature(out PackagedLotItemFeature package)
                && package.tareDisposition
                    == PackageTareDisposition.ReusableContainerReturn
                && !string.IsNullOrWhiteSpace(package.containerItemId))
            .Select(value =>
            {
                value.TryGetFeature(out PackagedLotItemFeature package);
                return package.containerItemId.Trim();
            })
            .ToHashSet(StringComparer.Ordinal);
        Dictionary<string, int> memo = new(StringComparer.Ordinal);
        foreach (ResourceItemDefinitionSO item in items.Where(value => value != null))
        {
            int minimum = reusablePackageContainers.Contains(item.ItemId)
                ? 1
                : StrategicIntermediates.Contains(item.ItemId)
                ? 3
                : item.Kind == ResourceItemKind.Intermediate ? 2 : 1;
            int count = consumers[item.ItemId].Count(id =>
                !id.StartsWith("sink:", StringComparison.Ordinal));
            if (count < minimum)
            {
                failures.Add($"{item.ItemId}: real consumers {count}, required {minimum}");
            }
            if (producers[item.ItemId].Count == 0
                && acquisitionProducers[item.ItemId].Count == 0
                && item.Kind != ResourceItemKind.Raw
                && item.ItemId is not "offense:unappraised-loot"
                && item.ItemId is not "resource:rune-dust"
                && item.ItemId is not EquipmentProgressionItemIds.LineageSeal)
            {
                failures.Add($"{item.ItemId}: no producer");
            }
            int depth = MeasureDepth(
                item.ItemId,
                byId,
                producers,
                memo,
                new HashSet<string>(StringComparer.Ordinal));
            if (depth > 4)
            {
                failures.Add($"{item.ItemId}: depth {depth} exceeds 4");
            }
        }

        ValidateSupplyValue(byId, "resource:log", 10f, 0f, failures);
        ValidateSupplyValue(byId, "material:low-fuel", 6f, 0f, failures);
        ValidateSupplyValue(byId, "resource:coal", 20f, 0f, failures);
        ValidateSupplyValue(byId, "material:charcoal", 24f, 0f, failures);
        ValidateSupplyValue(byId, "feed:hay", 0f, 8f, failures);
        ValidateSupplyValue(byId, "feed:silage", 4f, 14f, failures);
        ValidateSupplyValue(byId, "feed:dog-food", 0f, 14f, failures);
        if (byId.TryGetValue("material:black-powder", out ResourceItemDefinitionSO powder)
            && powder.FuelValue > 0f)
        {
            failures.Add("black powder must not be accepted as facility fuel");
        }

        ProductionConsumerRouteState blockedHigh = new()
        {
            policy = new ProductionConsumerRoutePolicy
            {
                consumerId = "high",
                priority = 100,
                weight = 10
            },
            currentDemand = 10,
            blockedReason = "other material missing"
        };
        ProductionConsumerRouteState availableLow = new()
        {
            policy = new ProductionConsumerRoutePolicy
            {
                consumerId = "low",
                priority = 10,
                weight = 1,
                waitingSeconds = 3000f
            },
            currentDemand = 2
        };
        if (ProductionDistributionPlanner.SelectNext(
                ProductionDistributionMode.DemandWeighted,
                new[] { blockedHigh, availableLow })?.consumerId != "low")
        {
            failures.Add("blocked high-priority route prevents another branch");
        }
        blockedHigh.blockedReason = string.Empty;
        blockedHigh.policy.priority = 80;
        blockedHigh.policy.waitingSeconds = 0f;
        if (ProductionDistributionPlanner.SelectNext(
                ProductionDistributionMode.StrictPriority,
                new[] { blockedHigh, availableLow })?.consumerId != "low")
        {
            failures.Add("waiting-time aging does not prevent branch starvation");
        }

        ProductionConsumerRouteState fixedRatioHeavy = new()
        {
            policy = new ProductionConsumerRoutePolicy
            {
                consumerId = "fixed-heavy",
                weight = 1
            },
            currentDemand = 12,
            reservedQuantity = 3,
            reservationLimit = 12
        };
        ProductionConsumerRouteState fixedRatioLight = new()
        {
            policy = new ProductionConsumerRoutePolicy
            {
                consumerId = "fixed-light",
                weight = 3
            },
            currentDemand = 12,
            reservedQuantity = 3,
            reservationLimit = 12
        };
        if (ProductionDistributionPlanner.SelectNext(
                ProductionDistributionMode.FixedRatio,
                new[] { fixedRatioHeavy, fixedRatioLight })?.consumerId
            != "fixed-light")
        {
            failures.Add("fixed-ratio routing ignored the authored branch weight");
        }

        ProductionConsumerRouteState targetStockRoute = new()
        {
            policy = new ProductionConsumerRoutePolicy
            {
                consumerId = "target-stock",
                priority = 100
            },
            currentDemand = 10,
            reservationLimit = 10,
            stage = ProductionDistributionStage.TargetStock
        };
        availableLow.policy.waitingSeconds = 0f;
        availableLow.stage = ProductionDistributionStage.ActiveDemand;
        if (ProductionDistributionPlanner.SelectNext(
                ProductionDistributionMode.StrictPriority,
                new[] { targetStockRoute, availableLow })?.consumerId != "low")
        {
            failures.Add("target-stock routing ran before active downstream demand");
        }

        ProductionConsumerRouteState cappedReserve = new()
        {
            policy = new ProductionConsumerRoutePolicy
            {
                consumerId = "minimum-reserve",
                minimumReserve = 5
            },
            currentDemand = 5,
            reservedQuantity = 2,
            reservationLimit = 5,
            stage = ProductionDistributionStage.MinimumReserve
        };
        if (ProductionDistributionPlanner.SelectNext(
                ProductionDistributionMode.DemandWeighted,
                new[] { cappedReserve })?.consumerId != "minimum-reserve")
        {
            failures.Add("minimum-reserve routing was blocked by an obsolete batch cap");
        }

        blockedHigh.blockedReason = "other-material-missing";
        availableLow.blockedReason = "consumer-output-full";
        if (ProductionDistributionPlanner.SelectNext(
                ProductionDistributionMode.DemandWeighted,
                new[] { blockedHigh, availableLow }) != null)
        {
            failures.Add("all blocked routes did not yield to warehouse/overflow fallback");
        }
        blockedHigh.blockedReason = string.Empty;
        availableLow.blockedReason = string.Empty;

        DungeonProductionBillSaveData save = new()
        {
            installedStockSensorFacilityIds = new List<string> { "facility:test" },
            installedStockSensors = new List<
                ProductionInstalledStockSensorSaveData>
            {
                new()
                {
                    facilityId = "facility:test",
                    itemId = "component:stock-sensor-panel",
                    inputOperationId =
                        ProductionStockSensorRuntime.BuildPhysicalOperationId(
                            "facility:test"),
                    inputCommitId = "physical-batch-disposition:3:production-stock-sensor-install:facility:test:1:1000",
                    inputSourceStackId = "stack:stock-sensor:fixture",
                    embeddedMassGrams = 1000L
                }
            },
            bills = new List<ProductionBillSaveData>
            {
                new()
                {
                    billId = "production-bill:test",
                    recipeId = "recipe:test",
                    buildingInstanceId = "building:test",
                    mode = ProductionOrderMode.RepeatForever,
                    outputDestinationId = "production-output:building:test",
                    outputReservations = new List<ProductionOutputReservationSaveData>
                    {
                        new() { itemId = "material:paper", amount = 4 }
                    },
                    distributionMode = ProductionDistributionMode.FixedRatio,
                    routePolicies = new List<ProductionConsumerRoutePolicy>
                    {
                        new() { consumerId = "recipe:branch", weight = 3 }
                    },
                    selectedSupplies = new List<ProductionSelectedSupplySaveData>
                    {
                        new() { supplyKey = "fuel:test", itemId = "material:charcoal" }
                    }
                }
            }
        };
        DungeonProductionBillSaveData roundTrip =
            JsonUtility.FromJson<DungeonProductionBillSaveData>(
                JsonUtility.ToJson(save));
        if (roundTrip?.version != DungeonProductionBillSaveData.CurrentVersion
            || roundTrip?.bills?.SingleOrDefault()?.mode
                != ProductionOrderMode.RepeatForever
            || roundTrip.bills[0].outputReservations.Single().amount != 4
            || roundTrip.bills[0].routePolicies.Single().weight != 3
            || roundTrip.bills[0].selectedSupplies.Single().itemId
                != "material:charcoal"
            || roundTrip.installedStockSensorFacilityIds.Single()
                != "facility:test")
        {
            failures.Add("production current-version save round trip lost network state");
        }

        int consumerOrphans = items.Count(item =>
        {
            int minimum = reusablePackageContainers.Contains(item.ItemId)
                ? 1
                : StrategicIntermediates.Contains(item.ItemId)
                ? 3
                : item.Kind == ResourceItemKind.Intermediate ? 2 : 1;
            return consumers[item.ItemId].Count(id =>
                !id.StartsWith("sink:", StringComparison.Ordinal)) < minimum;
        });
        int producerOrphans = items.Count(item =>
            producers[item.ItemId].Count == 0
            && acquisitionProducers[item.ItemId].Count == 0
            && item.Kind != ResourceItemKind.Raw
            && item.ItemId is not "offense:unappraised-loot"
            && item.ItemId is not "resource:rune-dust"
            && item.ItemId is not EquipmentProgressionItemIds.LineageSeal);
        LastCoverage = new ProductionNetworkCoverageSnapshot(
            byId.Count,
            recipes.Count(value => value != null),
            producers.Sum(value => value.Value.Count)
                + acquisitionProducers.Sum(value => value.Value.Count),
            consumers.Sum(value => value.Value.Count(id =>
                !id.StartsWith("sink:", StringComparison.Ordinal))),
            producerOrphans,
            consumerOrphans,
            memo.Count == 0 ? 0 : memo.Values.Max());

        return failures
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
    }

    private static void AddConsumer(
        IReadOnlyDictionary<string, HashSet<string>> consumers,
        string itemId,
        string consumerId)
    {
        if (consumers.TryGetValue(itemId, out HashSet<string> links))
        {
            links.Add(consumerId);
        }
    }

    private static void AddConsumableRequirementLinks(
        string consumerId,
        V20ContentRequirementSet requirements,
        IReadOnlyDictionary<string, HashSet<string>> consumers)
    {
        if (string.IsNullOrWhiteSpace(consumerId))
        {
            return;
        }
        foreach (V20ItemAmountRequirement requirement in
                 requirements?.items ?? new List<V20ItemAmountRequirement>())
        {
            if (requirement != null
                && requirement.consume
                && consumers.TryGetValue(
                    requirement.itemDefinitionId?.Trim() ?? string.Empty,
                    out HashSet<string> links))
            {
                links.Add(consumerId.Trim());
            }
        }
    }

    private static void ValidateSupplyValue(
        IReadOnlyDictionary<string, ResourceItemDefinitionSO> items,
        string itemId,
        float fuel,
        float nutrition,
        ICollection<string> failures)
    {
        if (!items.TryGetValue(itemId, out ResourceItemDefinitionSO item))
        {
            failures.Add($"missing facility supply item {itemId}");
            return;
        }
        if (!Mathf.Approximately(item.FuelValue, fuel)
            || !Mathf.Approximately(item.FacilityNutritionValue, nutrition))
        {
            failures.Add(
                $"{itemId}: supply values {item.FuelValue}/{item.FacilityNutritionValue}, "
                + $"expected {fuel}/{nutrition}");
        }
    }

    private static int MeasureDepth(
        string itemId,
        IReadOnlyDictionary<string, ResourceItemDefinitionSO> items,
        IReadOnlyDictionary<string, List<ProductionRecipeSO>> producers,
        IDictionary<string, int> memo,
        ISet<string> visiting)
    {
        if (memo.TryGetValue(itemId, out int cached))
        {
            return cached;
        }
        if (!items.TryGetValue(itemId, out ResourceItemDefinitionSO item)
            || item.Kind == ResourceItemKind.Raw
            || producers[itemId].All(recipe => recipe == null
                || recipe.RecipeId.StartsWith("source:", StringComparison.Ordinal)))
        {
            return 0;
        }
        if (!visiting.Add(itemId))
        {
            return 5;
        }
        int depth = 0;
        foreach (ProductionRecipeSO recipe in producers[itemId].Where(value => value != null))
        {
            int parentDepth = recipe.Inputs.Count == 0
                ? 0
                : recipe.Inputs.Max(input => input == null
                    ? 0
                    : MeasureDepth(input.ItemId, items, producers, memo, visiting));
            depth = Mathf.Max(depth, parentDepth + 1);
        }
        visiting.Remove(itemId);
        memo[itemId] = depth;
        return depth;
    }
}

public readonly struct ProductionNetworkCoverageSnapshot
{
    public ProductionNetworkCoverageSnapshot(
        int definitionCount,
        int recipeCount,
        int producerLinkCount,
        int consumerLinkCount,
        int producerOrphanCount,
        int consumerOrphanCount,
        int maximumRecipeDepth)
    {
        DefinitionCount = definitionCount;
        RecipeCount = recipeCount;
        ProducerLinkCount = producerLinkCount;
        ConsumerLinkCount = consumerLinkCount;
        ProducerOrphanCount = producerOrphanCount;
        ConsumerOrphanCount = consumerOrphanCount;
        MaximumRecipeDepth = maximumRecipeDepth;
    }

    public int DefinitionCount { get; }
    public int RecipeCount { get; }
    public int ProducerLinkCount { get; }
    public int ConsumerLinkCount { get; }
    public int ProducerOrphanCount { get; }
    public int ConsumerOrphanCount { get; }
    public int MaximumRecipeDepth { get; }
}
#endif
