#if UNITY_EDITOR
using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionMaximumOutputFactorCatalogDebugScenarios
{
    [MenuItem("DungeonStory/V27/Production/Run Maximum Output Factor Catalog")]
    public static void RunAll()
    {
        BuildingSO[] buildings = Resources.LoadAll<BuildingSO>("SO/Building");
        ProductionMaximumOutputFactorCatalog catalog = new(buildings);
        Require(catalog.SupportDefinitionCount == 28,
            $"Expected 28 authored production supports, got {catalog.SupportDefinitionCount}.");

        ProductionRecipeSO[] recipes = Resources
            .LoadAll<ProductionRecipeSO>(ProductionRecipeSO.ResourcePath)
            .Where(value => value != null)
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        Require(recipes.Length == 355,
            $"Expected 355 production recipes, got {recipes.Length}.");
        int affected = recipes.Count(value =>
            !catalog.ResolveMaximum(value).Equals(ProductionOutputFactor.One));
        Require(affected == 21,
            $"Expected 21 Grand Project affected recipes, got {affected}.");

        VerifyFeedbenchCapacitySource(buildings, recipes, catalog);
        VerifySawmillCapacitySource(buildings, recipes, catalog);
        VerifyWorkOnlyFamilyCapacitySources(buildings, recipes, catalog);

        ProductionRecipeSO silage = recipes.Single(value =>
            string.Equals(value.RecipeId, "recipe:silage", StringComparison.Ordinal));
        Require(catalog.ResolveMaximum(silage).Equals(ProductionOutputFactor.One),
            "Silage maximum support factor drifted from 1/1.");
        Expect<InvalidOperationException>(() =>
            new ProductionMaximumOutputFactorCatalog(Array.Empty<BuildingSO>())
                .ResolveMaximum(silage));

        BuildingSO nonUnit = ScriptableObject.CreateInstance<BuildingSO>();
        try
        {
            BuildingAbilityCollection abilities = new();
            abilities.Add(new BuildingProductionSupportAbility
            {
                supportId = "support:qa-non-unit",
                featureTags = new[] { "support:qa-feature" },
                compatibleWorkstationTags = new[] { "workstation:qa" },
                outputMultiplier = 1.1f
            });
            nonUnit.ReplaceAbilities(abilities);
            ExpectMessage<InvalidOperationException>(
                () => new ProductionMaximumOutputFactorCatalog(new[] { nonUnit }),
                ProductionMaximumOutputFactorCatalog.NonUnitSupportFailureCode);
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(nonUnit);
        }

        Debug.Log("[ProductionMaximumOutputFactorCatalog] focused scenarios passed.");
    }

    private static void VerifyFeedbenchCapacitySource(
        BuildingSO[] buildings,
        ProductionRecipeSO[] recipes,
        ProductionMaximumOutputFactorCatalog maximumFactors)
    {
        ResourceItemDefinitionSO[] items = Resources
            .LoadAll<ResourceItemDefinitionSO>(ItemDefinitionSO.UnifiedResourcePath)
            .Where(value => value != null)
            .ToArray();
        ResourceEconomyContentCatalog economy = new(
            items,
            recipes,
            Array.Empty<CropDefinitionSO>(),
            Array.Empty<CraftMaterialDefinitionSO>());
        PhysicalItemMassQuery massQuery = new(
            EditorItemCatalogFactory.Create());
        ProductionOutputBufferCapacityProjector projector = new(
            economy,
            maximumFactors,
            new ProductionPreparedOutputComponentCodec(),
            massQuery,
            facility => facility.OutputBufferCycleCapacity,
            (facility, recipe) => string.Equals(
                facility.WorkstationTag,
                recipe.WorkstationTag,
                StringComparison.Ordinal));

        BuildingSO feedbenchDefinition = buildings.Single(value =>
            value != null
            && value.GetProductionWorkstationAbility()?.WorkstationTag
                == "workstation:feedbench");
        BuildingProductionBufferAbility buffer =
            feedbenchDefinition.GetProductionBufferAbility();
        Require(buffer != null
            && buffer.physicalOutputBufferCycleCapacity == 4,
            "Feedbench has no exact four-cycle output-buffer authority.");

        ProductionFacilityHandle feedbench = Facility(
            feedbenchDefinition,
            new Vector2Int(17, 23),
            buffer.physicalOutputBufferCycleCapacity);
        ProductionOutputBufferCapacitySourceSnapshot first =
            projector.CaptureSource(feedbench, exactBatchMassGrams: 588L);
        ProductionOutputBufferCapacitySourceSnapshot repeat =
            projector.CaptureSource(feedbench, exactBatchMassGrams: 588L);
        Require(first.MaximumBatchMassGrams == 1_050L
            && first.ProjectedPortfolioCapacityGrams == 4_200L
            && first.BatchMinimumCapacityGrams == 2_352L
            && first.RequiredMinimumCapacityGrams == 4_200L
            && string.Equals(first.SourceDigest, repeat.SourceDigest,
                StringComparison.Ordinal)
            && IsLowercaseSha256(first.SourceDigest),
            "Feedbench capacity source was not deterministic at exact 4,200g.");

        ProductionFacilityCapacitySubject liveSubject =
            ProductionFacilityCapacitySubject.FromLive(feedbench);
        ModularFacilityBuildingSaveData savedFacility = new()
        {
            persistentInstanceId = feedbench.InstanceId.Value,
            buildingId = feedbenchDefinition.id,
            centerX = feedbench.Position.x,
            centerY = feedbench.Position.y,
            isDamaged = true,
            facilityLevel = 9,
            objectName = "ignored-presentation-drift"
        };
        ProductionFacilityCapacitySubject savedSubject =
            ProductionFacilityCapacitySubjectAdapter.FromSave(
                savedFacility,
                new FixedBuildingDefinitionLookup(feedbenchDefinition));
        ProductionOutputBufferCapacitySourceSnapshot detached =
            projector.CaptureSource(savedSubject, exactBatchMassGrams: 588L);
        Require(liveSubject.Equals(savedSubject)
            && detached.CycleCapacity == first.CycleCapacity
            && detached.MaximumBatchMassGrams == first.MaximumBatchMassGrams
            && detached.ProjectedPortfolioCapacityGrams ==
                first.ProjectedPortfolioCapacityGrams
            && detached.RequiredMinimumCapacityGrams ==
                first.RequiredMinimumCapacityGrams
            && string.Equals(
                detached.SourceDigest,
                first.SourceDigest,
                StringComparison.Ordinal),
            "Live and save-only capacity subjects produced different authority.");
        savedFacility.centerX++;
        ProductionFacilityCapacitySubject movedSavedSubject =
            ProductionFacilityCapacitySubjectAdapter.FromSave(
                savedFacility,
                new FixedBuildingDefinitionLookup(feedbenchDefinition));
        Require(!string.Equals(
                projector.CaptureSource(movedSavedSubject, 588L).SourceDigest,
                first.SourceDigest,
                StringComparison.Ordinal),
            "Saved facility position drift did not rebind capacity authority.");
        savedFacility.centerX = feedbench.Position.x;
        ModularFacilityWorldSaveData world = new()
        {
            buildings = new System.Collections.Generic.List<ModularFacilityBuildingSaveData>
            {
                savedFacility
            }
        };
        DungeonProductionBillSaveData production = new();
        string outputDestination = ProductionOutputDestinationId
            .FromFacility(feedbench.InstanceId).Value;
        WorldItemStackSaveData bufferedStack = new()
        {
            stackId = "stack:capacity-save:buffered",
            itemId = "feed:hay",
            quantity = 3,
            state = WorldItemStackState.FacilityOutputBuffer,
            destinationId = outputDestination
        };
        WorldItemStackSaveData carriedStack = new()
        {
            stackId = "stack:capacity-save:carried",
            itemId = "feed:hay",
            quantity = 1,
            state = WorldItemStackState.Carried,
            destinationId = "character:capacity-save:hauler"
        };
        DungeonPhysicalItemSaveData physical = new()
        {
            stacks = new System.Collections.Generic.List<WorldItemStackSaveData>
            {
                carriedStack,
                bufferedStack
            }
        };
        HaulDeliveryIntentSaveData haulIntent = new()
        {
            operationId = "haul:capacity-save",
            ownerCharacterId = "character:capacity-save:hauler",
            destinationKind = WorldItemHaulDestinationKind.FacilityBuffer,
            destinationId = outputDestination,
            commitments = new System.Collections.Generic.List<HaulDeliveryItemCommitmentSaveData>
            {
                new()
                {
                    carriedStackId = carriedStack.stackId,
                    sourceStackId = "stack:capacity-save:source",
                    itemId = carriedStack.itemId,
                    expectedStackSignature = carriedStack.GetStackSignature(),
                    quantity = carriedStack.quantity
                }
            }
        };
        DungeonCharacterWorldSaveData characters = new()
        {
            actors = new System.Collections.Generic.List<DungeonCharacterSaveData>
            {
                new()
                {
                    persistentId = haulIntent.ownerCharacterId,
                    haulDeliveryIntent = haulIntent,
                    carryInventory = new CharacterCarryInventorySaveData
                    {
                        items = new System.Collections.Generic.List<CharacterCarriedItemSaveData>
                        {
                            new()
                            {
                                carriedStackId = carriedStack.stackId,
                                sourceStackId = "stack:capacity-save:source",
                                ownerOperationId = haulIntent.operationId,
                                itemId = carriedStack.itemId,
                                quantity = carriedStack.quantity
                            }
                        }
                    }
                }
            }
        };
        ProductionPreparedOutputRoutingSaveData routing = new();
        ProductionOutputCapacityDurableProjection detachedCapacity =
            ProductionOutputDestinationDurableSaveProjector
                .ProjectCapacityRoutingFromSave(
                    feedbench.InstanceId,
                    world,
                    production,
                    physical,
                    characters,
                    routing,
                    physical.pendingExactOutputRoutes,
                    new FixedBuildingDefinitionLookup(feedbenchDefinition),
                    projector,
                    massQuery);
        Require(detachedCapacity.Profile != null
            && detachedCapacity.Profile.MaxMassGrams == 4_200L
            && detachedCapacity.Profile.DropPosition == feedbench.Position
            && detachedCapacity.Occupancy.NonCarriedMassGrams == 588L
            && detachedCapacity.Occupancy.CommittedCarriedMassGrams == 196L
            && detachedCapacity.Occupancy.TotalMassGrams == 784L,
            "Detached save capacity did not reconstruct the exact live profile.");
        string liveCapacityFingerprint =
            ProductionOutputDestinationDurableSaveProjector.ProjectCapacityRouting(
                feedbench.InstanceId,
                detachedCapacity.Profile,
                detachedCapacity.Occupancy,
                routing,
                physical.pendingExactOutputRoutes);
        Require(string.Equals(
                detachedCapacity.Fingerprint,
                liveCapacityFingerprint,
                StringComparison.Ordinal),
            "Live and save capacity-routing fingerprints diverged.");

        string aggregate =
            ProductionOutputDestinationDurableSaveProjector.ProjectAggregateFromSave(
                feedbench.InstanceId,
                world,
                production,
                new DungeonCombatEquipmentSaveData
                {
                    craftOrders = new System.Collections.Generic.List<CombatEquipmentCraftOrderSaveData>()
                },
                new CombatEquipmentMaintenanceSaveData(),
                new DungeonCharacterEnvironmentSaveData
                {
                    apparelWorkOrders = Array.Empty<ApparelWorkOrderSaveData>(),
                    apparelWorkOrderTerminalStates =
                        Array.Empty<ApparelWorkOrderTerminalStateSaveData>()
                },
                physical,
                characters,
                routing,
                new FixedBuildingDefinitionLookup(feedbenchDefinition),
                projector,
                massQuery);
        Require(IsLowercaseSha256(aggregate),
            "Detached five-contributor aggregate fingerprint is invalid.");
        ExpectMessage<InvalidOperationException>(
            () => ProductionOutputDestinationDurableSaveProjector.ComposeAggregate(
                feedbench.InstanceId,
                new[]
                {
                    new System.Collections.Generic.KeyValuePair<string, string>(
                        ProductionOutputDestinationDurableSaveProjector
                            .GenericBillsContributorId,
                        new string('a', 64))
                }),
            "required current-format contributor schema");

        carriedStack.state = WorldItemStackState.FacilityBuffer;
        carriedStack.destinationId = outputDestination;
        FacilityBufferPhysicalOccupancySnapshot depositWindow =
            ProductionOutputDestinationDurableSaveProjector.ProjectPhysicalOccupancy(
                outputDestination,
                physical,
                characters,
                massQuery);
        Require(depositWindow.NonCarriedMassGrams == 784L
            && depositWindow.CommittedCarriedMassGrams == 0L
            && depositWindow.TotalMassGrams == 784L,
            "Deposit-before-intent-retirement double-counted physical occupancy.");

        characters.actors[0].haulDeliveryIntent = null;
        FacilityBufferPhysicalOccupancySnapshot retiredIntent =
            ProductionOutputDestinationDurableSaveProjector.ProjectPhysicalOccupancy(
                outputDestination,
                physical,
                characters,
                massQuery);
        Require(retiredIntent.NonCarriedMassGrams == 784L
            && retiredIntent.CommittedCarriedMassGrams == 0L
            && retiredIntent.TotalMassGrams == 784L,
            "Retiring a completed haul intent changed deposited physical occupancy.");

        characters.actors[0].haulDeliveryIntent = haulIntent;
        carriedStack.destinationId = "production-output:building:qa:wrong";
        Expect<InvalidOperationException>(() =>
            ProductionOutputDestinationDurableSaveProjector.ProjectPhysicalOccupancy(
                outputDestination,
                physical,
                characters,
                massQuery));
        carriedStack.state = WorldItemStackState.Carried;
        carriedStack.destinationId = haulIntent.ownerCharacterId;

        physical.stacks.Remove(carriedStack);
        ExpectMessage<InvalidOperationException>(
            () => ProductionOutputDestinationDurableSaveProjector
                .ProjectPhysicalOccupancy(
                    outputDestination,
                    physical,
                    characters,
                    massQuery),
            "no physical carried stack");
        physical.stacks.Add(carriedStack);

        CharacterCarriedItemSaveData carriedInventoryRow =
            characters.actors[0].carryInventory.items[0];
        carriedInventoryRow.ownerOperationId = "haul:capacity-save:wrong-owner";
        ExpectMessage<InvalidOperationException>(
            () => ProductionOutputDestinationDurableSaveProjector
                .ProjectPhysicalOccupancy(
                    outputDestination,
                    physical,
                    characters,
                    massQuery),
            "conflicts with its physical and carried-inventory join");
        carriedInventoryRow.ownerOperationId = haulIntent.operationId;

        ResourceEconomyContentCatalog shuffledEconomy = new(
            items.Reverse(),
            recipes.Reverse(),
            Array.Empty<CropDefinitionSO>(),
            Array.Empty<CraftMaterialDefinitionSO>());
        ProductionOutputBufferCapacityProjector shuffledProjector = new(
            shuffledEconomy,
            maximumFactors,
            new ProductionPreparedOutputComponentCodec(),
            new PhysicalItemMassQuery(EditorItemCatalogFactory.Create()),
            facility => facility.OutputBufferCycleCapacity,
            (facility, recipe) => string.Equals(
                facility.WorkstationTag,
                recipe.WorkstationTag,
                StringComparison.Ordinal));
        ProductionOutputBufferCapacitySourceSnapshot shuffled =
            shuffledProjector.CaptureSource(feedbench, exactBatchMassGrams: 588L);
        Require(string.Equals(
                first.SourceDigest,
                shuffled.SourceDigest,
                StringComparison.Ordinal),
            "Capacity source digest changed after catalog enumeration shuffle.");
        string shuffledCatalogAggregate =
            ProductionOutputDestinationDurableSaveProjector.ProjectAggregateFromSave(
                feedbench.InstanceId,
                world,
                production,
                new DungeonCombatEquipmentSaveData
                {
                    craftOrders = new System.Collections.Generic.List<CombatEquipmentCraftOrderSaveData>()
                },
                new CombatEquipmentMaintenanceSaveData(),
                new DungeonCharacterEnvironmentSaveData
                {
                    apparelWorkOrders = Array.Empty<ApparelWorkOrderSaveData>(),
                    apparelWorkOrderTerminalStates =
                        Array.Empty<ApparelWorkOrderTerminalStateSaveData>()
                },
                physical,
                characters,
                routing,
                new FixedBuildingDefinitionLookup(feedbenchDefinition),
                shuffledProjector,
                massQuery);
        Require(string.Equals(
                aggregate,
                shuffledCatalogAggregate,
                StringComparison.Ordinal),
            "Detached aggregate changed after catalog enumeration shuffle.");

        ProductionFacilityHandle moved = Facility(
            feedbenchDefinition,
            new Vector2Int(18, 23),
            buffer.physicalOutputBufferCycleCapacity);
        ProductionOutputBufferCapacitySourceSnapshot movedSource =
            projector.CaptureSource(moved, exactBatchMassGrams: 588L);
        Require(movedSource.ProjectedPortfolioCapacityGrams == 4_200L
            && !string.Equals(
                first.SourceDigest,
                movedSource.SourceDigest,
                StringComparison.Ordinal),
            "Facility identity drift did not change the capacity source digest.");

        ProductionPreparedOutputBatchSaveData savedSource = new()
        {
            capacitySourceDigest = first.SourceDigest,
            outputBufferCycleCapacity = first.CycleCapacity,
            projectedPortfolioCapacityGrams =
                first.ProjectedPortfolioCapacityGrams,
            requiredMinimumCapacityGrams =
                first.RequiredMinimumCapacityGrams
        };
        ProductionOutputBufferCapacitySourceGuard.ValidateSaved(
            savedSource,
            first,
            "capacity-source-fixture");
        string savedBeforeStaleCheck = JsonUtility.ToJson(savedSource);
        ExpectMessage<InvalidOperationException>(
            () => ProductionOutputBufferCapacitySourceGuard.ValidateSaved(
                savedSource,
                movedSource,
                "capacity-source-fixture"),
            ProductionOutputBufferCapacitySourceGuard.StaleFailureToken);
        Require(string.Equals(
                JsonUtility.ToJson(savedSource),
                savedBeforeStaleCheck,
                StringComparison.Ordinal),
            "Stale capacity source validation mutated the durable batch.");

        ProductionOutputBufferCapacitySourceSnapshot heavyBatch =
            projector.CaptureSource(feedbench, exactBatchMassGrams: 1_200L);
        Require(heavyBatch.MaximumBatchMassGrams == 1_050L
            && heavyBatch.BatchMinimumCapacityGrams == 4_800L
            && heavyBatch.RequiredMinimumCapacityGrams == 4_800L
            && !string.Equals(
                first.SourceDigest,
                heavyBatch.SourceDigest,
                StringComparison.Ordinal),
            "Exact batch mass drift did not raise or rebind the capacity minimum.");
    }

    private static void VerifySawmillCapacitySource(
        BuildingSO[] buildings,
        ProductionRecipeSO[] recipes,
        ProductionMaximumOutputFactorCatalog maximumFactors)
    {
        ResourceItemDefinitionSO[] items = Resources
            .LoadAll<ResourceItemDefinitionSO>(ItemDefinitionSO.UnifiedResourcePath)
            .Where(value => value != null)
            .ToArray();
        ResourceEconomyContentCatalog economy = new(
            items,
            recipes,
            Array.Empty<CropDefinitionSO>(),
            Array.Empty<CraftMaterialDefinitionSO>());
        ProductionOutputBufferCapacityProjector projector = new(
            economy,
            maximumFactors,
            new ProductionPreparedOutputComponentCodec(),
            new PhysicalItemMassQuery(EditorItemCatalogFactory.Create()),
            facility => facility.OutputBufferCycleCapacity,
            (facility, recipe) => string.Equals(
                facility.WorkstationTag,
                recipe.WorkstationTag,
                StringComparison.Ordinal));

        BuildingSO sawmillDefinition = buildings.Single(value =>
            value != null
            && value.GetProductionWorkstationAbility()?.WorkstationTag
                == "workstation:sawmill");
        BuildingProductionBufferAbility buffer =
            sawmillDefinition.GetProductionBufferAbility();
        Require(buffer != null
            && buffer.physicalOutputBufferCycleCapacity == 4,
            "Sawmill has no exact four-cycle output-buffer authority.");

        ProductionFacilityHandle sawmill = Facility(
            sawmillDefinition,
            new Vector2Int(31, 17),
            buffer.physicalOutputBufferCycleCapacity);
        ProductionOutputBufferCapacitySourceSnapshot first =
            projector.CaptureSource(sawmill, exactBatchMassGrams: 3_600L);
        ProductionOutputBufferCapacitySourceSnapshot repeat =
            projector.CaptureSource(sawmill, exactBatchMassGrams: 3_600L);
        Require(first.MaximumBatchMassGrams == 3_600L
            && first.ProjectedPortfolioCapacityGrams == 14_400L
            && first.BatchMinimumCapacityGrams == 14_400L
            && first.RequiredMinimumCapacityGrams == 14_400L
            && string.Equals(first.SourceDigest, repeat.SourceDigest,
                StringComparison.Ordinal)
            && IsLowercaseSha256(first.SourceDigest),
            "Sawmill capacity source was not deterministic at exact 14,400g.");
    }

    private static void VerifyWorkOnlyFamilyCapacitySources(
        BuildingSO[] buildings,
        ProductionRecipeSO[] recipes,
        ProductionMaximumOutputFactorCatalog maximumFactors)
    {
        VerifyWorkOnlyFamilyCapacitySource(
            buildings,
            recipes,
            maximumFactors,
            "workstation:charcoal-kiln",
            new[] { "recipe:charcoal" },
            maximumBatchMassGrams: 900L,
            projectedCapacityGrams: 3_600L,
            position: new Vector2Int(41, 17));
        VerifyWorkOnlyFamilyCapacitySource(
            buildings,
            recipes,
            maximumFactors,
            "workstation:mill",
            new[]
            {
                "recipe:malt",
                "recipe:milling-flour",
                "recipe:starch"
            },
            maximumBatchMassGrams: 700L,
            projectedCapacityGrams: 2_800L,
            position: new Vector2Int(43, 17));
        VerifyWorkOnlyFamilyCapacitySource(
            buildings,
            recipes,
            maximumFactors,
            "workstation:steelworks",
            new[] { "recipe:steel-ingot" },
            maximumBatchMassGrams: 850L,
            projectedCapacityGrams: 3_400L,
            position: new Vector2Int(45, 17));
        VerifyWorkOnlyFamilyCapacitySource(
            buildings,
            recipes,
            maximumFactors,
            "workstation:v3:treated-lumber",
            new[] { "recipe:treated-lumber" },
            maximumBatchMassGrams: 2_300L,
            projectedCapacityGrams: 9_200L,
            position: new Vector2Int(47, 17));
    }

    private static void VerifyWorkOnlyFamilyCapacitySource(
        BuildingSO[] buildings,
        ProductionRecipeSO[] recipes,
        ProductionMaximumOutputFactorCatalog maximumFactors,
        string workstationTag,
        string[] expectedRecipeIds,
        long maximumBatchMassGrams,
        long projectedCapacityGrams,
        Vector2Int position)
    {
        ProductionRecipeSO[] reachable = recipes
            .Where(value => string.Equals(
                value.WorkstationTag,
                workstationTag,
                StringComparison.Ordinal))
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        Require(reachable.Select(value => value.RecipeId).SequenceEqual(
                expectedRecipeIds,
                StringComparer.Ordinal),
            $"Workstation '{workstationTag}' recipe family drifted.");
        Require(reachable.All(value =>
                ProductionPreparedOutputMigrationScope.Contains(value.RecipeId)),
            $"Workstation '{workstationTag}' retains a legacy output bypass.");
        Require(reachable.All(value =>
                maximumFactors.ResolveMaximum(value).Equals(
                    ProductionOutputFactor.One)),
            $"Workstation '{workstationTag}' maximum factor drifted from 1/1.");

        ResourceItemDefinitionSO[] items = Resources
            .LoadAll<ResourceItemDefinitionSO>(ItemDefinitionSO.UnifiedResourcePath)
            .Where(value => value != null)
            .ToArray();
        ResourceEconomyContentCatalog economy = new(
            items,
            recipes,
            Array.Empty<CropDefinitionSO>(),
            Array.Empty<CraftMaterialDefinitionSO>());
        ProductionOutputBufferCapacityProjector projector = new(
            economy,
            maximumFactors,
            new ProductionPreparedOutputComponentCodec(),
            new PhysicalItemMassQuery(EditorItemCatalogFactory.Create()),
            facility => facility.OutputBufferCycleCapacity,
            (facility, recipe) => string.Equals(
                facility.WorkstationTag,
                recipe.WorkstationTag,
                StringComparison.Ordinal));
        BuildingSO definition = buildings.Single(value =>
            value != null
            && string.Equals(
                value.GetProductionWorkstationAbility()?.WorkstationTag,
                workstationTag,
                StringComparison.Ordinal));
        BuildingProductionBufferAbility buffer =
            definition.GetProductionBufferAbility();
        Require(buffer != null
            && buffer.physicalOutputBufferCycleCapacity == 4,
            $"Workstation '{workstationTag}' has no authored four-cycle authority.");
        ProductionFacilityHandle facility = Facility(
            definition,
            position,
            buffer.physicalOutputBufferCycleCapacity);
        ProductionOutputBufferCapacitySourceSnapshot first =
            projector.CaptureSource(facility, maximumBatchMassGrams);
        ProductionOutputBufferCapacitySourceSnapshot repeat =
            projector.CaptureSource(facility, maximumBatchMassGrams);
        Require(first.MaximumBatchMassGrams == maximumBatchMassGrams
            && first.ProjectedPortfolioCapacityGrams == projectedCapacityGrams
            && first.BatchMinimumCapacityGrams == projectedCapacityGrams
            && first.RequiredMinimumCapacityGrams == projectedCapacityGrams
            && string.Equals(
                first.SourceDigest,
                repeat.SourceDigest,
                StringComparison.Ordinal)
            && IsLowercaseSha256(first.SourceDigest),
            $"Workstation '{workstationTag}' capacity projection drifted.");
    }

    private static ProductionFacilityHandle Facility(
        BuildingSO definition,
        Vector2Int position,
        int cycleCapacity)
    {
        string definitionId = definition.ContentDefinitionId.Length > 0
            ? definition.ContentDefinitionId
            : "building:" + definition.id;
        return new ProductionFacilityHandle(
            new object(),
            new BuildingInstanceId(
                "building:qa:capacity-source:" + definition.id),
            position,
            isDestroyed: false,
            stockSensorInstallationItemId: string.Empty,
            allowsOverflowDump: false,
            overflowOffset: Vector2Int.zero,
            definitionId,
            definition.GetProductionWorkstationAbility().WorkstationTag,
            cycleCapacity);
    }

    private static bool IsLowercaseSha256(string value) =>
        value != null
        && value.Length == 64
        && value.All(character =>
            character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f');

    private sealed class FixedBuildingDefinitionLookup : IBuildingDefinitionLookup
    {
        private readonly BuildingSO definition;

        internal FixedBuildingDefinitionLookup(BuildingSO definition) =>
            this.definition = definition;

        public BuildingSO GetBuilding(int id)
        {
            if (definition == null || definition.id != id)
                throw new InvalidOperationException("Building definition fixture mismatch.");
            return definition;
        }
    }

    private static void Expect<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException(
            "Expected exception was not thrown: " + typeof(T).Name + ".");
    }

    private static void ExpectMessage<T>(Action action, string token)
        where T : Exception
    {
        try
        {
            action();
        }
        catch (T exception)
        {
            Require(exception.Message.Contains(token, StringComparison.Ordinal),
                $"Expected failure token '{token}', got '{exception.Message}'.");
            return;
        }
        throw new InvalidOperationException(
            "Expected exception was not thrown: " + typeof(T).Name + ".");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
#endif
