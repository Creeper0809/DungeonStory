using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

public static class R18ContentReferenceDebugScenarios
{
    private const string RootPath =
        "Assets/Resources/SO/GameContentCatalog.asset";
    private const string RoomSettingsPath =
        "Assets/Resources/Config/RoomEnvironmentSettings.asset";
    private const string MissingId = "fixture:r18:missing";

    [MenuItem("DungeonStory/V27/Content/Validate R18 References")]
    public static void RunAll()
    {
        GameContentCatalogSO root = RequireAsset<GameContentCatalogSO>(RootPath);
        ScriptableObject[] definitions = LoadDefinitions(root);
        BuildingSO[] buildings = definitions.OfType<BuildingSO>().ToArray();
        FacilityEvolutionRecipeSO facilityRecipe = definitions
            .OfType<FacilityEvolutionRecipeSO>()
            .FirstOrDefault(value => value.resultBuilding != null
                && value.formulaCapabilities?.Count == 1)
            ?? throw new InvalidOperationException(
                "No registered single-capability facility recipe was found.");
        ProductionRecipeSO productionRecipe = RequireProductionRecipe(
            definitions,
            buildings);
        BuildingSO workstationConsumer = buildings.FirstOrDefault(value =>
            value.GetAbility<BuildingProductionSupportAbility>()
                ?.compatibleWorkstationTags?.Length > 0)
            ?? throw new InvalidOperationException(
                "No production support with compatible workstation references was found.");
        BuildingSO serviceHub = RequireServiceHub(buildings);
        BuildingSO serviceSupport = buildings.FirstOrDefault(value =>
            value.GetAbility<BuildingServiceSupportAbility>()
                ?.compatibleHubTags?.Length > 0)
            ?? throw new InvalidOperationException(
                "No service support with compatible hub references was found.");
        SeasonalWorldEventDefinitionSO seasonalEvent = definitions
            .OfType<SeasonalWorldEventDefinitionSO>()
            .FirstOrDefault(value => value.wildlifeArrivalProfile?.IsConfigured == true)
            ?? throw new InvalidOperationException(
                "No configured seasonal wildlife event was found.");
        BuildingSO anchorOwner = buildings.FirstOrDefault(value =>
            value.FacilityAnchors.Slots.Count > 0)
            ?? throw new InvalidOperationException(
                "No registered building with a facility anchor was found.");
        RoomEnvironmentSettingsSO roomSettings =
            RequireAsset<RoomEnvironmentSettingsSO>(RoomSettingsPath);

        ScriptableObject[] sources =
        {
            facilityRecipe,
            productionRecipe,
            workstationConsumer,
            serviceHub,
            serviceSupport,
            seasonalEvent,
            anchorOwner,
            roomSettings
        };
        SourceSnapshot[] snapshots = sources.Distinct()
            .Select(SourceSnapshot.Capture).ToArray();
        try
        {
            ContentReferenceCatalog catalog = ContentReferenceSelection.CreateCatalog(root);
            VerifyCandidates(
                catalog,
                buildings,
                facilityRecipe,
                productionRecipe,
                serviceHub);
            VerifyProductionRecipe(catalog, buildings, productionRecipe);
            VerifyWorkstationConsumer(catalog, workstationConsumer);
            VerifyServiceReferences(
                catalog,
                buildings,
                serviceHub,
                serviceSupport);
            VerifyHabitatReference(catalog, seasonalEvent);
            VerifyAnchorReference(catalog, anchorOwner);
            VerifyFacilityMaterials(catalog, facilityRecipe);
            VerifyFacilityModules(catalog, definitions, facilityRecipe);
            VerifyRoomRoles(catalog, roomSettings);
            Debug.Log(
                "R18_CONTENT_REFERENCES=PASS; eleven providers; rejection; repair retry; owner eligibility; unchanged source");
        }
        finally
        {
            foreach (SourceSnapshot snapshot in snapshots)
                snapshot.RequireUnchanged();
        }
    }

    // Invoke only through the isolated batch-mode Inspector contract runner.
    public static void VerifyInspectorGUI()
    {
        if (!Application.isBatchMode)
            throw new InvalidOperationException(
                "R18 Inspector verification requires isolated batch mode.");

        GameContentCatalogSO root = RequireAsset<GameContentCatalogSO>(RootPath);
        ScriptableObject[] definitions = LoadDefinitions(root);
        BuildingSO[] buildings = definitions.OfType<BuildingSO>().ToArray();
        ProductionRecipeSO recipeSource = RequireProductionRecipe(
            definitions,
            buildings);
        FacilityEvolutionRecipeSO facilitySource = definitions
            .OfType<FacilityEvolutionRecipeSO>()
            .First(value => value.formulaCapabilities?.Count > 0);
        SeasonalWorldEventDefinitionSO habitatSource = definitions
            .OfType<SeasonalWorldEventDefinitionSO>()
            .First(value => value.wildlifeArrivalProfile?.IsConfigured == true);
        BuildingSO anchorSource = buildings.First(value =>
            value.FacilityAnchors.Slots.Count > 0);
        RoomEnvironmentSettingsSO roomSource =
            RequireAsset<RoomEnvironmentSettingsSO>(RoomSettingsPath);
        ScriptableObject[] sources =
        {
            recipeSource, facilitySource, habitatSource, anchorSource, roomSource
        };
        SourceSnapshot[] snapshots = sources.Select(SourceSnapshot.Capture).ToArray();
        ScriptableObject[] clones = sources.Select(value =>
        {
            ScriptableObject clone = Object.Instantiate(value);
            clone.hideFlags = HideFlags.HideAndDontSave;
            return clone;
        }).ToArray();
        string[] cloneJson = clones.Select(EditorJsonUtility.ToJson).ToArray();
        ContentReferenceInspectorProbeWindow window =
            ScriptableObject.CreateInstance<ContentReferenceInspectorProbeWindow>();
        try
        {
            ProductionRecipeSO recipe = (ProductionRecipeSO)clones[0];
            FacilityEvolutionRecipeSO facility =
                (FacilityEvolutionRecipeSO)clones[1];
            SeasonalWorldEventDefinitionSO habitat =
                (SeasonalWorldEventDefinitionSO)clones[2];
            BuildingSO anchor = (BuildingSO)clones[3];
            RoomEnvironmentSettingsSO room =
                (RoomEnvironmentSettingsSO)clones[4];

            ContentReferenceSelection.Invalidate();
            window.position = new Rect(0, 0, 720, 260);
            window.ShowUtility();

            ContentReferenceCatalog catalog =
                ContentReferenceSelection.CreateCatalog(root);
            string productionSupport = catalog.Entries(
                    new ContentReferenceAttribute(
                        ContentReferenceKind.ProductionSupport),
                    recipe)
                .Select(value => value.Id)
                .First();
            VerifyPendingArrayElementUndo(
                window,
                recipe,
                "requiredSupportTags",
                productionSupport,
                "pending production recipe support");

            string moduleReplacement = catalog.Entries(
                    new ContentReferenceAttribute(
                        ContentReferenceKind.FacilityEvolutionModule),
                    facility,
                    facility.formulaCapabilities[0])
                .Select(value => value.Id)
                .FirstOrDefault(value => !string.Equals(
                    value,
                    facility.formulaCapabilities[0].evolutionModuleId,
                    StringComparison.Ordinal))
                ?? facility.formulaCapabilities[0].evolutionModuleId;
            VerifyPendingStringUndo(
                window,
                facility,
                "formulaCapabilities.Array.data[0].evolutionModuleId",
                moduleReplacement,
                "pending facility module");

            string habitatReplacement = ExpectedHabitats().First(value =>
                !string.Equals(
                    value,
                    habitat.wildlifeArrivalProfile.requiredHabitatId,
                    StringComparison.Ordinal));
            VerifyPendingStringUndo(
                window,
                habitat,
                "wildlifeArrivalProfile.requiredHabitatId",
                habitatReplacement,
                "pending wildlife habitat");

            VerifyPendingRoleUndo(
                window,
                room,
                FacilityRoleCatalog.All[0].Id);

            string anchorReplacement = FacilityAnchorPurposeCatalog.All
                .Select(value => value.PurposeId)
                .First(value => !string.Equals(
                    value,
                    anchor.FacilityAnchors.Slots[0].purposeId,
                    StringComparison.Ordinal));
            VerifyPendingStringUndo(
                window,
                anchor,
                "facilityAnchors.slots.Array.data[0].purposeId",
                anchorReplacement,
                "pending facility anchor purpose");

            Require(window.DrawCount >= 5,
                "R18 PropertyDrawer GUI events did not execute.");
            for (int index = 0; index < clones.Length; index++)
                Require(EditorJsonUtility.ToJson(clones[index]) == cloneJson[index],
                    $"Inspector Undo did not restore clone '{clones[index].name}'.");
            Debug.Log(
                "R18_CONTENT_REFERENCE_INSPECTOR=PASS; recipe; module; habitat; role; anchor; undo; unchanged source");
        }
        finally
        {
            window.Property = null;
            window.Close();
            if (window != null) Object.DestroyImmediate(window);
            foreach (ScriptableObject clone in clones)
            {
                Undo.ClearUndo(clone);
                Object.DestroyImmediate(clone);
            }
            foreach (SourceSnapshot snapshot in snapshots)
                snapshot.RequireUnchanged();
        }
    }

    private static void VerifyCandidates(
        ContentReferenceCatalog catalog,
        IReadOnlyList<BuildingSO> buildings,
        FacilityEvolutionRecipeSO facilityRecipe,
        ProductionRecipeSO productionRecipe,
        BuildingSO serviceHub)
    {
        BuildingAbility[] abilities = buildings
            .SelectMany(value => value.Abilities)
            .Where(value => value != null).ToArray();
        EvolutionModuleRegistry modules = new();

        RequireCandidateIds(catalog, ContentReferenceKind.StockCategory,
            Enum.GetValues(typeof(StockCategory)).Cast<StockCategory>()
                .Select(StockCategoryPersistenceId.ToId));
        RequireCandidateIds(
            catalog,
            ContentReferenceKind.FacilityEvolutionModule,
            modules.All.Where(value => value.IsPositiveModule
                    && FacilityEvolutionModifierApplicability
                        .IsPositiveModuleEligibleForNewGeneration(
                            facilityRecipe.resultBuilding,
                            value,
                            out _))
                .Select(value => value.ModuleId),
            facilityRecipe,
            facilityRecipe.formulaCapabilities[0]);
        RequireCandidateIds(catalog, ContentReferenceKind.EquipmentEvolutionModule,
            modules.All.Where(value => value.ModuleId.StartsWith(
                    "equipment:", StringComparison.Ordinal))
                .Select(value => value.ModuleId));
        RequireCandidateIds(catalog, ContentReferenceKind.ProductionWorkstation,
            abilities.OfType<BuildingProductionWorkstationAbility>()
                .Select(value => value.WorkstationTag));
        RequireCandidateIds(
            catalog,
            ContentReferenceKind.ProductionSupport,
            abilities.OfType<BuildingProductionSupportAbility>()
                .Where(value => value.SupportsWorkstation(
                    productionRecipe.WorkstationTag))
                .SelectMany(value => value.featureTags ?? Array.Empty<string>()),
            productionRecipe);
        RequireCandidateIds(
            catalog,
            ContentReferenceKind.ProductionBatchSupport,
            abilities.OfType<BuildingProductionSupportAbility>()
                .Where(value => value.kind == ProductionSupportKind.BatchProcessor
                    && value.SupportsWorkstation(
                        productionRecipe.WorkstationTag))
                .SelectMany(value => value.featureTags ?? Array.Empty<string>()),
            productionRecipe);
        RequireCandidateIds(catalog, ContentReferenceKind.ServiceHub,
            abilities.OfType<BuildingServiceHubAbility>()
                .Select(value => value.ServiceHubTag));
        BuildingServiceHubAbility hub = serviceHub
            .GetAbility<BuildingServiceHubAbility>();
        RequireCandidateIds(
            catalog,
            ContentReferenceKind.ServiceFeature,
            abilities.OfType<BuildingServiceSupportAbility>()
                .Where(value => value.SupportsHub(hub.ServiceHubTag))
                .SelectMany(value => value.featureTags ?? Array.Empty<string>()),
            serviceHub,
            hub);
        RequireCandidateIds(catalog, ContentReferenceKind.WildlifeHabitat,
            ExpectedHabitats());
        RequireCandidateIds(catalog, ContentReferenceKind.FacilityRole,
            FacilityRoleCatalog.All.Select(value => value.Id));
        RequireCandidateIds(catalog, ContentReferenceKind.FacilityAnchorPurpose,
            FacilityAnchorPurposeCatalog.All.Select(value => value.PurposeId));
    }

    private static void VerifyProductionRecipe(
        ContentReferenceCatalog catalog,
        IReadOnlyList<BuildingSO> buildings,
        ProductionRecipeSO source)
    {
        ProductionRecipeSO clone = Object.Instantiate(source);
        string baseline = EditorJsonUtility.ToJson(source);
        BuildingProductionSupportAbility[] supports = buildings
            .SelectMany(value => value.Abilities)
            .OfType<BuildingProductionSupportAbility>()
            .ToArray();
        string incompatibleSupport = RequireIncompatibleProductionFeature(
            source,
            supports,
            batchOnly: false);
        string incompatibleBatchSupport = RequireIncompatibleProductionFeature(
            source,
            supports,
            batchOnly: true);
        try
        {
            RequireClean(catalog.Validate(new ScriptableObject[] { clone }),
                "Production recipe baseline");
            SetString(clone, "workstationTag", MissingId);
            RequireRejected(catalog, clone, MissingId,
                "unknown production workstation");

            Restore(clone, baseline);
            SetStringArray(clone, "requiredSupportTags", MissingId);
            RequireRejected(catalog, clone, MissingId,
                "unknown production support");

            Restore(clone, baseline);
            SetStringArray(
                clone,
                "requiredSupportTags",
                incompatibleSupport);
            RequireRejected(catalog, clone, incompatibleSupport,
                "registered support from another workstation");

            Restore(clone, baseline);
            SetString(clone, "batchSupportTag", MissingId);
            RequireRejected(catalog, clone, MissingId,
                "unknown production batch support");

            Restore(clone, baseline);
            SetString(clone, "batchSupportTag", incompatibleBatchSupport);
            RequireRejected(catalog, clone, incompatibleBatchSupport,
                "registered batch support from another workstation");

            Restore(clone, baseline);
            RequireRepairRetry(catalog, clone, "Production recipe repair");
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }
    }

    private static void VerifyWorkstationConsumer(
        ContentReferenceCatalog catalog,
        BuildingSO source)
    {
        BuildingSO clone = Object.Instantiate(source);
        string baseline = EditorJsonUtility.ToJson(source);
        try
        {
            RequireClean(catalog.Validate(new ScriptableObject[] { clone }),
                "Production workstation consumer baseline");
            clone.GetAbility<BuildingProductionSupportAbility>()
                .compatibleWorkstationTags = new[] { MissingId };
            RequireRejected(catalog, clone, MissingId,
                "unknown compatible production workstation");

            Restore(clone, baseline);
            RequireRepairRetry(catalog, clone,
                "Production workstation consumer repair");
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }
    }

    private static void VerifyServiceReferences(
        ContentReferenceCatalog catalog,
        IReadOnlyList<BuildingSO> buildings,
        BuildingSO hubSource,
        BuildingSO supportSource)
    {
        BuildingSO hub = Object.Instantiate(hubSource);
        BuildingSO support = Object.Instantiate(supportSource);
        string hubBaseline = EditorJsonUtility.ToJson(hubSource);
        string supportBaseline = EditorJsonUtility.ToJson(supportSource);
        string incompatibleFeature = RequireIncompatibleServiceFeature(
            hubSource,
            buildings.SelectMany(value => value.Abilities)
                .OfType<BuildingServiceSupportAbility>());
        try
        {
            RequireClean(catalog.Validate(new ScriptableObject[] { hub }),
                "Service hub baseline");
            BuildingServiceHubAbility hubAbility =
                hub.GetAbility<BuildingServiceHubAbility>();
            if ((hubAbility.managedRequiredFeatureTags?.Length ?? 0) > 0)
                hubAbility.managedRequiredFeatureTags = new[] { MissingId };
            else
                hubAbility.automatedRequiredFeatureTags = new[] { MissingId };
            RequireRejected(catalog, hub, MissingId,
                "unknown required service feature");

            Restore(hub, hubBaseline);
            hubAbility = hub.GetAbility<BuildingServiceHubAbility>();
            if ((hubAbility.managedRequiredFeatureTags?.Length ?? 0) > 0)
                hubAbility.managedRequiredFeatureTags =
                    new[] { incompatibleFeature };
            else
                hubAbility.automatedRequiredFeatureTags =
                    new[] { incompatibleFeature };
            RequireRejected(catalog, hub, incompatibleFeature,
                "registered service feature from another hub");

            Restore(hub, hubBaseline);
            RequireRepairRetry(catalog, hub, "Service hub repair");

            RequireClean(catalog.Validate(new ScriptableObject[] { support }),
                "Service support baseline");
            support.GetAbility<BuildingServiceSupportAbility>()
                .compatibleHubTags = new[] { MissingId };
            RequireRejected(catalog, support, MissingId,
                "unknown compatible service hub");

            Restore(support, supportBaseline);
            RequireRepairRetry(catalog, support, "Service support repair");
        }
        finally
        {
            Object.DestroyImmediate(hub);
            Object.DestroyImmediate(support);
        }
    }

    private static void VerifyHabitatReference(
        ContentReferenceCatalog catalog,
        SeasonalWorldEventDefinitionSO source)
    {
        SeasonalWorldEventDefinitionSO clone = Object.Instantiate(source);
        string baseline = EditorJsonUtility.ToJson(source);
        try
        {
            RequireClean(catalog.Validate(new ScriptableObject[] { clone }),
                "Seasonal habitat baseline");
            clone.wildlifeArrivalProfile.requiredHabitatId = MissingId;
            RequireRejected(catalog, clone, MissingId,
                "unknown wildlife habitat");

            Restore(clone, baseline);
            RequireRepairRetry(catalog, clone, "Seasonal habitat repair");
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }
    }

    private static void VerifyAnchorReference(
        ContentReferenceCatalog catalog,
        BuildingSO source)
    {
        BuildingSO clone = Object.Instantiate(source);
        string baseline = EditorJsonUtility.ToJson(source);
        try
        {
            RequireClean(catalog.Validate(new ScriptableObject[] { clone }),
                "Facility anchor baseline");
            clone.FacilityAnchors.Slots[0].purposeId = MissingId;
            RequireRejected(catalog, clone, MissingId,
                "unknown facility anchor purpose");

            Restore(clone, baseline);
            RequireRepairRetry(catalog, clone, "Facility anchor repair");
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }
    }

    private static void VerifyFacilityMaterials(
        ContentReferenceCatalog catalog,
        FacilityEvolutionRecipeSO source)
    {
        FacilityEvolutionRecipeSO clone = Object.Instantiate(source);
        string baseline = EditorJsonUtility.ToJson(source);
        try
        {
            RequireClean(catalog.Validate(new ScriptableObject[] { clone }),
                "Facility material baseline");
            clone.requiredMaterials = new[]
            {
                new FacilityEvolutionMaterialRequirement
                {
                    materialId = "stock:fixture-missing",
                    amount = 3
                }
            };
            RequireRejected(catalog, clone, "stock:fixture-missing",
                "unknown facility stock category");

            clone.requiredMaterials = new[]
            {
                new FacilityEvolutionMaterialRequirement
                {
                    materialId = "stock:general",
                    amount = 3
                }
            };
            Require(clone.requiredMaterials[0].amount == 3,
                "Facility material amount was not retained by the authored field.");
            RequireClean(catalog.Validate(new ScriptableObject[] { clone }),
                "stock:general facility material");

            Restore(clone, baseline);
            RequireRepairRetry(catalog, clone, "Facility material repair");
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }
    }

    private static void VerifyFacilityModules(
        ContentReferenceCatalog catalog,
        IReadOnlyList<ScriptableObject> definitions,
        FacilityEvolutionRecipeSO source)
    {
        FacilityEvolutionRecipeSO clone = Object.Instantiate(source);
        string baseline = EditorJsonUtility.ToJson(source);
        EvolutionModuleRegistry registry = new();
        try
        {
            RequireClean(catalog.Validate(new ScriptableObject[] { clone }),
                "Facility module baseline");
            clone.formulaCapabilities[0].evolutionModuleId =
                "facility:fixture-missing";
            RequireRejected(catalog, clone, "facility:fixture-missing",
                "unregistered facility module");

            Restore(clone, baseline);
            string ineligibleModule = definitions
                .OfType<FacilityEvolutionRecipeSO>()
                .Where(value => value != source && value.resultBuilding != null)
                .SelectMany(value => (value.formulaCapabilities
                        ?? new List<FacilityEvolutionFormulaCapabilityDefinition>())
                    .Where(capability => capability != null)
                    .Select(capability => new { Recipe = value, Capability = capability }))
                .Where(value => registry.TryGet(
                        value.Capability.evolutionModuleId,
                        out EvolutionModuleDefinition module)
                    && module.IsPositiveModule
                    && FacilityEvolutionModifierApplicability
                        .IsPositiveModuleEligibleForNewGeneration(
                            value.Recipe.resultBuilding,
                            module,
                            out _)
                    && !FacilityEvolutionModifierApplicability
                        .IsPositiveModuleEligibleForNewGeneration(
                            source.resultBuilding,
                            module,
                            out _))
                .Select(value => value.Capability.evolutionModuleId)
                .Distinct(StringComparer.Ordinal)
                .FirstOrDefault()
                ?? throw new InvalidOperationException(
                    "No registered module eligible for another recipe and ineligible for the fixture owner was found.");
            clone.formulaCapabilities[0].evolutionModuleId = ineligibleModule;
            RequireRejected(catalog, clone, ineligibleModule,
                "other-owner ineligible facility module");

            Restore(clone, baseline);
            RequireRepairRetry(catalog, clone, "Facility module repair");
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }
    }

    private static void VerifyRoomRoles(
        ContentReferenceCatalog catalog,
        RoomEnvironmentSettingsSO source)
    {
        RoomEnvironmentSettingsSO clone = Object.Instantiate(source);
        string baseline = EditorJsonUtility.ToJson(source);
        string knownRole = FacilityRoleCatalog.All[0].Id;
        try
        {
            RequireClean(catalog.Validate(new ScriptableObject[] { clone }),
                "Room role baseline");
            SetRoleOverrides(clone, MissingId);
            RequireRejected(catalog, clone, MissingId,
                "unknown room role override");

            Restore(clone, baseline);
            SetRoleOverrides(clone, knownRole, knownRole);
            IReadOnlyList<string> duplicateErrors =
                catalog.Validate(new ScriptableObject[] { clone });
            Require(duplicateErrors.Any(value => value.Contains(
                        "duplicated", StringComparison.OrdinalIgnoreCase)),
                "A duplicate room role override was accepted: "
                + string.Join(" | ", duplicateErrors));

            Restore(clone, baseline);
            RequireRepairRetry(catalog, clone, "Room role repair");
        }
        finally
        {
            Object.DestroyImmediate(clone);
        }
    }

    private static void RequireCandidateIds(
        ContentReferenceCatalog catalog,
        ContentReferenceKind kind,
        IEnumerable<string> expected,
        ScriptableObject root = null,
        object owner = null)
    {
        ContentReferenceAttribute reference = new(kind);
        IEnumerable<string> actual = root == null
            ? catalog.Entries(reference).Select(value => value.Id)
            : catalog.Entries(reference, root, owner).Select(value => value.Id);
        string[] actualIds = Normalize(actual);
        string[] expectedIds = Normalize(expected);
        Require(expectedIds.Length > 0,
            $"The independent {kind} provider returned no candidates.");
        Require(actualIds.SequenceEqual(expectedIds, StringComparer.Ordinal),
            $"{kind} candidates disagree with their provider. Expected "
            + $"[{string.Join(", ", expectedIds)}], actual "
            + $"[{string.Join(", ", actualIds)}].");
    }

    private static IEnumerable<string> ExpectedHabitats() =>
        Enum.GetValues(typeof(WildlifeHabitatType))
            .Cast<WildlifeHabitatType>()
            .Select(value => value.ToString().ToLowerInvariant())
            .Where(value => SeasonalWildlifeArrivalRules.TryParseHabitat(
                value,
                out _));

    private static ProductionRecipeSO RequireProductionRecipe(
        IEnumerable<ScriptableObject> definitions,
        IReadOnlyList<BuildingSO> buildings)
    {
        BuildingProductionSupportAbility[] supports = buildings
            .SelectMany(value => value.Abilities)
            .OfType<BuildingProductionSupportAbility>()
            .ToArray();
        return definitions.OfType<ProductionRecipeSO>()
            .FirstOrDefault(value =>
            {
                HashSet<string> allowed = ProductionFeatureIds(
                        value,
                        supports,
                        batchOnly: false)
                    .ToHashSet(StringComparer.Ordinal);
                HashSet<string> allowedBatch = ProductionFeatureIds(
                        value,
                        supports,
                        batchOnly: true)
                    .ToHashSet(StringComparer.Ordinal);
                return value.WorkstationTag.Length > 0
                    && value.RequiredSupportTags.Count > 0
                    && value.BatchSupportTag.Length > 0
                    && value.RequiredSupportTags.All(allowed.Contains)
                    && allowedBatch.Contains(value.BatchSupportTag)
                    && FindIncompatibleProductionFeature(
                        value,
                        supports,
                        batchOnly: false) != null
                    && FindIncompatibleProductionFeature(
                        value,
                        supports,
                        batchOnly: true) != null;
            })
            ?? throw new InvalidOperationException(
                "No registered production recipe covers owner-scoped support and batch-support fixtures.");
    }

    private static BuildingSO RequireServiceHub(
        IReadOnlyList<BuildingSO> buildings)
    {
        BuildingServiceSupportAbility[] supports = buildings
            .SelectMany(value => value.Abilities)
            .OfType<BuildingServiceSupportAbility>()
            .ToArray();
        return buildings.FirstOrDefault(value =>
        {
            BuildingServiceHubAbility hub =
                value.GetAbility<BuildingServiceHubAbility>();
            if (hub == null) return false;
            string[] required = (hub.managedRequiredFeatureTags
                    ?? Array.Empty<string>())
                .Concat(hub.automatedRequiredFeatureTags
                    ?? Array.Empty<string>())
                .ToArray();
            HashSet<string> allowed = supports
                .Where(support => support.SupportsHub(hub.ServiceHubTag))
                .SelectMany(support => support.featureTags
                    ?? Array.Empty<string>())
                .ToHashSet(StringComparer.Ordinal);
            return required.Length > 0
                && required.All(allowed.Contains)
                && FindIncompatibleServiceFeature(value, supports) != null;
        }) ?? throw new InvalidOperationException(
            "No service hub covers owner-scoped compatible and incompatible feature fixtures.");
    }

    private static IEnumerable<string> ProductionFeatureIds(
        ProductionRecipeSO recipe,
        IEnumerable<BuildingProductionSupportAbility> supports,
        bool batchOnly) => supports
        .Where(value => value.SupportsWorkstation(recipe.WorkstationTag)
            && (!batchOnly
                || value.kind == ProductionSupportKind.BatchProcessor))
        .SelectMany(value => value.featureTags ?? Array.Empty<string>())
        .Where(value => !string.IsNullOrWhiteSpace(value))
        .Distinct(StringComparer.Ordinal);

    private static string RequireIncompatibleProductionFeature(
        ProductionRecipeSO recipe,
        IEnumerable<BuildingProductionSupportAbility> supports,
        bool batchOnly) => FindIncompatibleProductionFeature(
            recipe,
            supports,
            batchOnly)
        ?? throw new InvalidOperationException(
            "No registered " + (batchOnly ? "batch " : string.Empty)
            + "support feature belongs only to another workstation.");

    private static string FindIncompatibleProductionFeature(
        ProductionRecipeSO recipe,
        IEnumerable<BuildingProductionSupportAbility> supports,
        bool batchOnly)
    {
        BuildingProductionSupportAbility[] values = supports.ToArray();
        HashSet<string> allowed = ProductionFeatureIds(
                recipe,
                values,
                batchOnly)
            .ToHashSet(StringComparer.Ordinal);
        return values
            .Where(value => !value.SupportsWorkstation(recipe.WorkstationTag)
                && (!batchOnly
                    || value.kind == ProductionSupportKind.BatchProcessor))
            .SelectMany(value => value.featureTags ?? Array.Empty<string>())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)
                && !allowed.Contains(value));
    }

    private static string RequireIncompatibleServiceFeature(
        BuildingSO hubOwner,
        IEnumerable<BuildingServiceSupportAbility> supports) =>
        FindIncompatibleServiceFeature(hubOwner, supports)
        ?? throw new InvalidOperationException(
            "No registered service feature belongs only to another hub.");

    private static string FindIncompatibleServiceFeature(
        BuildingSO hubOwner,
        IEnumerable<BuildingServiceSupportAbility> supports)
    {
        BuildingServiceHubAbility hub = hubOwner
            .GetAbility<BuildingServiceHubAbility>();
        if (hub == null) return null;
        BuildingServiceSupportAbility[] values = supports.ToArray();
        HashSet<string> allowed = values
            .Where(value => value.SupportsHub(hub.ServiceHubTag))
            .SelectMany(value => value.featureTags ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .ToHashSet(StringComparer.Ordinal);
        return values
            .Where(value => !value.SupportsHub(hub.ServiceHubTag))
            .SelectMany(value => value.featureTags ?? Array.Empty<string>())
            .FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)
                && !allowed.Contains(value));
    }

    private static void VerifyPendingArrayElementUndo(
        ContentReferenceInspectorProbeWindow window,
        ScriptableObject target,
        string arrayPath,
        string pendingValue,
        string context)
    {
        Undo.ClearUndo(target);
        using SerializedObject serialized = new(target);
        SerializedProperty array = RequireProperty(serialized, arrayPath);
        Require(array.isArray, context + " is not an array property.");
        int appliedCount = array.arraySize;
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("R18 " + context);
        array.arraySize++;
        SerializedProperty pending = array.GetArrayElementAtIndex(appliedCount);
        pending.stringValue = pendingValue;
        DrawProbe(window, pending, context);
        Require(ReadAppliedArraySize(target, arrayPath) == appliedCount,
            context + " was applied while drawing.");
        Require(serialized.ApplyModifiedProperties(),
            context + " did not apply for the Undo probe.");
        Undo.CollapseUndoOperations(undoGroup);
        Require(ReadAppliedArraySize(target, arrayPath) == appliedCount + 1,
            context + " was not applied before Undo.");
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        serialized.Update();
        Require(ReadAppliedArraySize(target, arrayPath) == appliedCount,
            context + " was not restored by Undo.");
    }

    private static void VerifyPendingRoleUndo(
        ContentReferenceInspectorProbeWindow window,
        RoomEnvironmentSettingsSO target,
        string roleId)
    {
        const string path = "roleColorOverrides";
        Undo.ClearUndo(target);
        using SerializedObject serialized = new(target);
        SerializedProperty array = RequireProperty(serialized, path);
        int appliedCount = array.arraySize;
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("R18 pending room role");
        array.arraySize++;
        SerializedProperty pending = array.GetArrayElementAtIndex(appliedCount)
            .FindPropertyRelative("roleId");
        pending.stringValue = roleId;
        DrawProbe(window, pending, "pending room role");
        Require(ReadAppliedArraySize(target, path) == appliedCount,
            "Pending room role was applied while drawing.");
        Require(serialized.ApplyModifiedProperties(),
            "Pending room role did not apply for the Undo probe.");
        Undo.CollapseUndoOperations(undoGroup);
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        serialized.Update();
        Require(ReadAppliedArraySize(target, path) == appliedCount,
            "Pending room role was not restored by Undo.");
    }

    private static void VerifyPendingStringUndo(
        ContentReferenceInspectorProbeWindow window,
        ScriptableObject target,
        string propertyPath,
        string pendingValue,
        string context)
    {
        Undo.ClearUndo(target);
        using SerializedObject serialized = new(target);
        SerializedProperty property = RequireProperty(serialized, propertyPath);
        string original = property.stringValue;
        if (string.Equals(original, pendingValue, StringComparison.Ordinal))
        {
            DrawProbe(window, property, context);
            Require(ReadAppliedString(target, propertyPath) == original,
                context + " changed while drawing its sole valid value.");
            return;
        }
        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();
        Undo.SetCurrentGroupName("R18 " + context);
        property.stringValue = pendingValue;
        DrawProbe(window, property, context);
        Require(ReadAppliedString(target, propertyPath) == original,
            context + " was applied while drawing.");
        Require(serialized.ApplyModifiedProperties(),
            context + " did not apply for the Undo probe.");
        Undo.CollapseUndoOperations(undoGroup);
        Require(ReadAppliedString(target, propertyPath) == pendingValue,
            context + " was not applied before Undo.");
        Undo.FlushUndoRecordObjects();
        Undo.PerformUndo();
        serialized.Update();
        Require(ReadAppliedString(target, propertyPath) == original,
            context + " was not restored by Undo.");
    }

    private static void DrawProbe(
        ContentReferenceInspectorProbeWindow window,
        SerializedProperty property,
        string context)
    {
        window.Property = property;
        int previous = window.DrawCount;
        window.SendEvent(new Event { type = EventType.Layout });
        window.SendEvent(new Event { type = EventType.Repaint });
        if (window.Failure != null)
            throw new InvalidOperationException(context, window.Failure);
        Require(window.DrawCount > previous,
            "Inspector GUI did not execute: " + context);
    }

    private static int ReadAppliedArraySize(
        ScriptableObject target,
        string path)
    {
        using SerializedObject serialized = new(target);
        return RequireProperty(serialized, path).arraySize;
    }

    private static string ReadAppliedString(
        ScriptableObject target,
        string path)
    {
        using SerializedObject serialized = new(target);
        return RequireProperty(serialized, path).stringValue;
    }

    private static void SetString(
        ScriptableObject target,
        string path,
        string value)
    {
        using SerializedObject serialized = new(target);
        RequireProperty(serialized, path).stringValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetStringArray(
        ScriptableObject target,
        string path,
        params string[] values)
    {
        using SerializedObject serialized = new(target);
        SerializedProperty array = RequireProperty(serialized, path);
        array.arraySize = values.Length;
        for (int index = 0; index < values.Length; index++)
            array.GetArrayElementAtIndex(index).stringValue = values[index];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void SetRoleOverrides(
        RoomEnvironmentSettingsSO target,
        params string[] roleIds)
    {
        using SerializedObject serialized = new(target);
        SerializedProperty overrides = RequireProperty(
            serialized,
            "roleColorOverrides");
        overrides.arraySize = roleIds.Length;
        for (int index = 0; index < roleIds.Length; index++)
            overrides.GetArrayElementAtIndex(index)
                .FindPropertyRelative("roleId").stringValue = roleIds[index];
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static SerializedProperty RequireProperty(
        SerializedObject serialized,
        string path) => serialized.FindProperty(path)
        ?? throw new InvalidOperationException(
            $"Serialized property '{path}' was not found on '{serialized.targetObject.name}'.");

    private static void RequireRejected(
        ContentReferenceCatalog catalog,
        ScriptableObject clone,
        string expectedText,
        string context)
    {
        IReadOnlyList<string> errors =
            catalog.Validate(new ScriptableObject[] { clone });
        Require(errors.Any(value => value.Contains(
                    expectedText,
                    StringComparison.OrdinalIgnoreCase)),
            $"Catalog accepted {context}: {string.Join(" | ", errors)}");
    }

    private static void RequireRepairRetry(
        ContentReferenceCatalog catalog,
        ScriptableObject clone,
        string context)
    {
        RequireClean(catalog.Validate(new[] { clone }), context);
        RequireClean(catalog.Validate(new[] { clone }), context + " retry");
    }

    private static void RequireClean(
        IEnumerable<string> errors,
        string context)
    {
        string[] values = errors?.ToArray() ?? Array.Empty<string>();
        Require(values.Length == 0,
            context + " failed: " + string.Join(" | ", values));
    }

    private static string[] Normalize(IEnumerable<string> values) =>
        (values ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

    private static void Restore(ScriptableObject clone, string json)
    {
        EditorJsonUtility.FromJsonOverwrite(json, clone);
    }

    private static ScriptableObject[] LoadDefinitions(GameContentCatalogSO root) =>
        root.DomainCatalogs.OfType<GameDomainContentCatalogSO>()
            .SelectMany(value => value.Definitions)
            .Where(value => value != null)
            .Distinct()
            .ToArray();

    private static T RequireAsset<T>(string path) where T : Object =>
        AssetDatabase.LoadAssetAtPath<T>(path)
        ?? throw new InvalidOperationException(
            $"Required asset '{path}' was not found.");

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class SourceSnapshot
    {
        private readonly ScriptableObject source;
        private readonly string json;
        private readonly bool dirty;

        private SourceSnapshot(ScriptableObject source)
        {
            this.source = source;
            json = EditorJsonUtility.ToJson(source);
            dirty = EditorUtility.IsDirty(source);
        }

        public static SourceSnapshot Capture(ScriptableObject source) =>
            new(source ?? throw new ArgumentNullException(nameof(source)));

        public void RequireUnchanged()
        {
            Require(EditorJsonUtility.ToJson(source) == json,
                $"R18 fixture changed source asset '{source.name}'.");
            Require(EditorUtility.IsDirty(source) == dirty,
                $"R18 fixture changed dirty state for '{source.name}'.");
        }
    }
}
