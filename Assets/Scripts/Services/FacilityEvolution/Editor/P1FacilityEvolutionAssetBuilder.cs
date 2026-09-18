using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class P1FacilityEvolutionAssetBuilder
{
    private const string BuildingFolder = "Assets/Resources/SO/Building/Modular";
    private const string RecipeFolder = "Assets/Resources/SO/FacilityEvolution/P1";
    private const string TokenDefinitionFolder = "Assets/Resources/SO/FacilityEvolution/RecordTokens/P1";

    [MenuItem("DungeonStory/Debug/Facility Evolution/Ensure P1 Evolution Assets")]
    public static void EnsureP1EvolutionAssetsFromMenu()
    {
        EnsureP1EvolutionAssets();
    }

    /// <summary>
    /// Controlled migration entry point. It changes only formulaPolicy and
    /// formulaCapabilities on the six already-authored P1 recipe assets; it never
    /// rewrites their recipe, visibility, room, material, or research fields.
    /// </summary>
    [MenuItem("DungeonStory/Build/Facility Evolution/Apply P1 Formula Metadata Only")]
    public static void ApplyP1FormulaMetadataOnlyFromMenu()
    {
        FacilityEvolutionRecipeSO[] recipes = AssetDatabase
            .FindAssets("t:FacilityEvolutionRecipeSO", new[] { RecipeFolder })
            .Select(AssetDatabase.GUIDToAssetPath)
            .OrderBy(path => path, StringComparer.Ordinal)
            .Select(AssetDatabase.LoadAssetAtPath<FacilityEvolutionRecipeSO>)
            .Where(recipe => recipe != null)
            .ToArray();
        string[] expected = CreateRecipeSpecs()
            .Select(spec => spec.evolutionId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        string[] actual = recipes.Select(recipe => recipe.EffectiveId)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (!actual.SequenceEqual(expected, StringComparer.Ordinal))
            throw new InvalidOperationException(
                "Formula-only migration requires exactly the six authored P1 recipe IDs; "
                + "it will not infer metadata for missing, renamed, or extra recipes.");

        foreach (FacilityEvolutionRecipeSO recipe in recipes)
        {
            ApplyFormulaAuthoring(recipe, recipe.EffectiveId);
            EditorUtility.SetDirty(recipe);
        }
        AssetDatabase.SaveAssets();
    }

    /// <summary>
    /// Applies the Phase77 operational metadata to the exact display lineage
    /// only. This intentionally does not invoke either broad facility or
    /// service-room builders, so unrelated authored BuildingSO fields remain
    /// untouched.
    /// </summary>
    [MenuItem("DungeonStory/Build/Facility Evolution/Apply P1 Display Operational Metadata Only")]
    public static void ApplyP1DisplayOperationalMetadataOnlyFromMenu()
    {
        BuildingSO source = LoadRequiredBuilding("S02_잡화진열선반");
        BuildingSO result = LoadRequiredBuilding("S03_잠금진열장");
        ApplyPurchaseVisitorFacility(source);
        ApplyPurchaseVisitorFacility(result);
        ApplySecureRetailDisplaySupport(result);
        EditorUtility.SetDirty(source);
        EditorUtility.SetDirty(result);
        AssetDatabase.SaveAssets();
    }

    public static void EnsureP1EvolutionAssets()
    {
        AssetDatabase.Refresh();
        P1FacilitySynthesisAssetBuilder.EnsureP1SynthesisAssets();
        EnsureFolder(RecipeFolder);
        EnsureFolder(TokenDefinitionFolder);
        EnsureRecordTokenDefinitionAssets();

        EvolutionRecipeSpec[] specs = CreateRecipeSpecs();
        PruneStaleRecipeAssets(specs);
        foreach (EvolutionRecipeSpec spec in specs)
        {
            EnsureRecipeAsset(spec);
        }

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }


    private static void EnsureRecordTokenDefinitionAssets()
    {
        foreach (RecordTokenDefinitionSpec spec in CreateRecordTokenDefinitionSpecs())
        {
            string assetPath = $"{TokenDefinitionFolder}/{spec.assetName}.asset";
            FacilityEvolutionRecordTokenDefinitionSO definition =
                AssetDatabase.LoadAssetAtPath<FacilityEvolutionRecordTokenDefinitionSO>(assetPath);
            if (definition == null)
            {
                definition = ScriptableObject.CreateInstance<FacilityEvolutionRecordTokenDefinitionSO>();
                AssetDatabase.CreateAsset(definition, assetPath);
            }

            definition.id = spec.id;
            definition.tokenId = spec.tokenId;
            definition.displayName = spec.displayName;
            definition.description = spec.description;
            definition.sourceMetric = spec.sourceMetric;
            definition.threshold = spec.threshold;
            definition.decayPolicy = spec.decayPolicy;
            definition.consumePolicy = spec.consumePolicy;
            definition.recipeTags = spec.recipeTags ?? Array.Empty<string>();
            definition.uiHint = spec.uiHint;
            EditorUtility.SetDirty(definition);
        }
    }

    private static BuildingSO LoadRequiredBuilding(string assetName)
    {
        BuildingSO building = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            $"{BuildingFolder}/{assetName}.asset");
        if (building == null)
        {
            throw new InvalidOperationException(
                "Phase77 display metadata requires authored building '"
                + assetName + "'.");
        }

        return building;
    }

    private static void ApplyPurchaseVisitorFacility(BuildingSO building)
    {
        BuildingFacilityAbility facility = building.GetAbility<BuildingFacilityAbility>();
        if (facility == null)
        {
            facility = new BuildingFacilityAbility();
            building.AbilityModules.Add(facility);
        }

        facility.settings ??= new FacilityData();
        facility.settings.roles = FacilityRole.Purchase;
        facility.settings.capacity = 1;
        facility.settings.useDuration = 1.5f;
        facility.settings.requiredWorkers = 1;
        facility.settings.disabledWhenDamaged = true;
        facility.settings.SetSupportedWorkTypeIds(new[]
        {
            BuiltInWorkTypeIds.Operate,
            BuiltInWorkTypeIds.Restock,
            BuiltInWorkTypeIds.Repair
        });
        if (building.GetAbility<BuildingRoomRequirementAbility>() == null)
        {
            building.AbilityModules.Add(new BuildingRoomRequirementAbility());
        }
    }

    private static void ApplySecureRetailDisplaySupport(BuildingSO building)
    {
        BuildingServiceSupportAbility support = building
            .GetAbility<BuildingServiceSupportAbility>();
        if (support == null)
        {
            support = new BuildingServiceSupportAbility();
            building.AbilityModules.Add(support);
        }

        support.supportId = "service-retail-secure-display";
        support.featureTags = new[] { "service:display" };
        support.compatibleHubTags = new[] { "service:retail" };
        support.modifierType = ServiceSupportModifierType.Security;
        support.capacity = 0;
        support.requiresPower = false;
        support.cleanWaterPerUse = 0f;
        support.wastewaterPerUse = 0f;
        support.allowsManualWaterFallback = false;
        support.workSpeedMultiplier = 1f;
        support.satisfactionModifier = 0f;
        // Preserve the source display's established retail revenue modifier
        // when the physical replacement takes over its display role.
        support.revenueModifier = 2;
    }

    private static void EnsureRecipeAsset(EvolutionRecipeSpec spec)
    {
        string assetPath = $"{RecipeFolder}/{spec.assetName}.asset";
        FacilityEvolutionRecipeSO recipe = AssetDatabase.LoadAssetAtPath<FacilityEvolutionRecipeSO>(assetPath);
        if (recipe == null)
        {
            recipe = ScriptableObject.CreateInstance<FacilityEvolutionRecipeSO>();
            AssetDatabase.CreateAsset(recipe, assetPath);
        }

        recipe.id = spec.id;
        recipe.evolutionId = spec.evolutionId;
        recipe.displayName = spec.displayName;
        recipe.description = spec.description;
        recipe.resultBuilding = LoadBuilding(spec.resultAssetName);
        recipe.fromFacilities = spec.sourceAssetNames
            .Select(LoadBuilding)
            .Where((building) => building != null)
            .ToArray();
        recipe.fromLineageTags = spec.sourceLineageTags ?? Array.Empty<string>();
        recipe.requiredStarGrade = spec.requiredStarGrade;
        recipe.resultStarGrade = spec.resultStarGrade;
        recipe.publicByDefault = spec.publicByDefault;
        recipe.requiredResearchRecipeId = spec.requiredResearchRecipeId;
        recipe.requireUsableRoom = true;
        recipe.requiredRoomScores = spec.requiredRoomScores ?? Array.Empty<FacilityEvolutionMetricRequirement>();
        recipe.requiredRoomMetrics = spec.requiredRoomMetrics ?? Array.Empty<FacilityEvolutionMetricRequirement>();
        recipe.requiredRoomTags = spec.requiredRoomTags ?? Array.Empty<string>();
        recipe.requiredUniqueFixtures = spec.requiredUniqueFixtureAssetNames
            .Select(LoadBuilding)
            .Where((building) => building != null)
            .ToArray();
        recipe.requiredRecordTokens = spec.requiredRecordTokens ?? Array.Empty<FacilityEvolutionTokenRequirement>();
        recipe.requiredMaterials = spec.requiredMaterials ?? Array.Empty<FacilityEvolutionMaterialRequirement>();
        recipe.allowedMutationTags = spec.allowedMutationTags ?? Array.Empty<string>();
        recipe.consumeRecordTokens = spec.consumeRecordTokens;
        recipe.identityPressureWeights = spec.identityPressureWeights ?? Array.Empty<FacilityEvolutionValue>();
        recipe.minimumIdentityScore = spec.minimumIdentityScore;
        ApplyFormulaAuthoring(recipe, spec.evolutionId);
        EditorUtility.SetDirty(recipe);
    }

    // This migration is deliberately a closed mapping for the six authored P1 recipes.
    // A later recipe receives no implicit module, policy, or formula fallback.
    private static void ApplyFormulaAuthoring(
        FacilityEvolutionRecipeSO recipe,
        string evolutionId)
    {
        string moduleId = evolutionId switch
        {
            "evolve_research_desk_to_alchemy_bench"
                or "evolve_mana_shelf_to_ritual_focus" => "facility:research",
            "evolve_training_dummy_to_archery_target" =>
                "facility:training-operations",
            "evolve_guard_desk_to_tactical_table" =>
                "facility:security-operations",
            "evolve_commercial_hearth_to_grill" => "facility:survival",
            "evolve_shop_display_to_secure_display" =>
                "facility:service-support",
            _ => throw new InvalidOperationException(
                $"Facility recipe '{evolutionId}' requires explicit v1 formula metadata.")
        };
        // A recipe enumerates only its explicitly authored capability. The
        // result-building consumer policy is still rechecked at every fresh
        // offer/pending resolution; unlisted generic modules cannot leak in
        // merely because their IDs happen to share the facility namespace.
        ApplyFormulaAuthoring(recipe, evolutionId, new[] { moduleId });
    }

    public static void ApplyFormulaAuthoringForEditorTest(
        FacilityEvolutionRecipeSO recipe,
        string moduleId)
    {
        if (recipe == null) throw new ArgumentNullException(nameof(recipe));
        if (string.IsNullOrWhiteSpace(moduleId) || moduleId != moduleId.Trim())
            throw new ArgumentException("A canonical test facility module ID is required.", nameof(moduleId));
        ApplyFormulaAuthoring(recipe, recipe.EffectiveId, new[] { moduleId });
    }

    private static void ApplyFormulaAuthoring(
        FacilityEvolutionRecipeSO recipe,
        string evolutionId,
        IEnumerable<string> moduleIds)
    {
        string[] modules = (moduleIds ?? Array.Empty<string>())
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.Ordinal)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();
        if (modules.Length == 0)
            throw new InvalidOperationException("Facility formula authoring requires modules.");
        recipe.formulaCapabilities = modules.Select(moduleId =>
        {
            string suffix = moduleId.Replace(':', '-');
            string capabilityId = "facility-evolution:" + evolutionId + ":" + suffix;
            return new FacilityEvolutionFormulaCapabilityDefinition
            {
                capabilityId = capabilityId,
                evolutionModuleId = moduleId,
                baseCost = 1,
                triggerFrequencyCostPerUnit = 0,
                guaranteedProcCost = 0,
                areaCostPerExtraTarget = 0,
                multiEffectCostPerExtraEffect = 0,
                narrativeAffinity = 1f,
                affinityKeys = (recipe.allowedMutationTags ?? Array.Empty<string>())
                    .Where(value => !string.IsNullOrWhiteSpace(value))
                    .Append(moduleId)
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(value => value, StringComparer.Ordinal).ToList(),
                conflictGroups = new List<string> { "facility-module" },
                forbiddenSynergies = new List<string>(),
                formatterId = capabilityId,
                applicatorId = capabilityId,
                parameterRanges = new List<FacilityEvolutionFormulaRangeDefinition>
                {
                    Range(NarrativeFormulaParameterIds.Magnitude, 5000, 10000, 1000, 4, 1),
                    FixedRange(NarrativeFormulaParameterIds.Duration, 0),
                    FixedRange(NarrativeFormulaParameterIds.Count, 1),
                    FixedRange(NarrativeFormulaParameterIds.TargetCount, 1)
                }
            };
        }).ToList();
        FacilityEvolutionFormulaPolicyDefinition policy = new()
        {
            formulaVersion = FacilityFormulaEvolutionAuthority.DrawbackModuleSelectionFormulaVersion,
            baseBudget = 1,
            powerScale = 4,
            softCapK = 8f,
            minimumImportance = 0f,
            maximumImportance = 4f,
            milestoneWeights = new List<float> { 1f, 1.5f, 2f, 3f },
            drawbackCredit = new NarrativeFormulaDrawbackCreditPolicyDefinition
            {
                playerChoiceMaximumBudgetFraction = 0.5f,
                automaticMaximumBudgetFraction = 0.35f,
                absoluteMaximumCredit = 3,
                requireNegativeEvidenceForAutomatic = true
            },
            triggerFrequencyUnits = 0,
            guaranteedProc = true,
            targetCount = 1
        };
        policy.catalogSha256 = ComputeFormulaCatalogSha256(
            recipe, recipe.formulaCapabilities, policy);
        recipe.formulaPolicy = policy;
    }

    private static FacilityEvolutionFormulaRangeDefinition Range(
        string parameterId, long minimum, long maximum, long quantum,
        int decimals, int cost) => new()
    {
        parameterId = parameterId,
        minimumUnits = minimum,
        maximumUnits = maximum,
        quantumUnits = quantum,
        decimalPlaces = decimals,
        costPerQuantum = cost
    };

    private static FacilityEvolutionFormulaRangeDefinition FixedRange(
        string parameterId, long units) => Range(parameterId, units, units, 1, 0, 1);

    private static string ComputeFormulaCatalogSha256(
        FacilityEvolutionRecipeSO recipe,
        IEnumerable<FacilityEvolutionFormulaCapabilityDefinition> capabilities,
        FacilityEvolutionFormulaPolicyDefinition policy)
    {
        StringBuilder material = new();
        material.Append(recipe.EffectiveId).Append('\n')
            .Append(policy.formulaVersion).Append(':').Append(policy.baseBudget).Append(':')
            .Append(policy.powerScale).Append(':')
            .Append(policy.softCapK.ToString("R", CultureInfo.InvariantCulture)).Append(':')
            .Append(policy.minimumImportance.ToString("R", CultureInfo.InvariantCulture)).Append(':')
            .Append(policy.maximumImportance.ToString("R", CultureInfo.InvariantCulture)).Append(':')
            .Append(policy.triggerFrequencyUnits).Append(':').Append(policy.guaranteedProc).Append(':')
            .Append(policy.targetCount).Append('\n');
        foreach (FacilityEvolutionFormulaCapabilityDefinition capability in capabilities
                     .OrderBy(value => value.evolutionModuleId, StringComparer.Ordinal))
        {
            material.Append(capability.capabilityId).Append('\n')
            .Append(capability.evolutionModuleId).Append('\n')
            .Append(capability.baseCost).Append(':')
            .Append(capability.triggerFrequencyCostPerUnit).Append(':')
            .Append(capability.guaranteedProcCost).Append(':')
            .Append(capability.areaCostPerExtraTarget).Append(':')
            .Append(capability.multiEffectCostPerExtraEffect).Append(':')
            .Append(capability.narrativeAffinity.ToString("R", CultureInfo.InvariantCulture)).Append('\n')
            .Append(capability.formatterId).Append(':').Append(capability.applicatorId).Append('\n');
            foreach (string value in (capability.affinityKeys ?? new List<string>())
                         .OrderBy(value => value, StringComparer.Ordinal))
                material.Append("affinity:").Append(value).Append('\n');
            foreach (string value in (capability.conflictGroups ?? new List<string>())
                         .OrderBy(value => value, StringComparer.Ordinal))
                material.Append("conflict:").Append(value).Append('\n');
            foreach (string value in (capability.forbiddenSynergies ?? new List<string>())
                         .OrderBy(value => value, StringComparer.Ordinal))
                material.Append("forbid:").Append(value).Append('\n');
            foreach (FacilityEvolutionFormulaRangeDefinition range in capability.parameterRanges
                         .OrderBy(value => value.parameterId, StringComparer.Ordinal))
            {
                material.Append(range.parameterId).Append(':')
                    .Append(range.minimumUnits).Append(':').Append(range.maximumUnits).Append(':')
                    .Append(range.quantumUnits).Append(':').Append(range.decimalPlaces).Append(':')
                    .Append(range.costPerQuantum).Append('\n');
            }
        }
        NarrativeFormulaDrawbackCreditPolicyDefinition drawback = policy.drawbackCredit
            ?? throw new InvalidOperationException("Facility drawback-credit policy is missing.");
        material.Append("drawback:")
            .Append(drawback.playerChoiceMaximumBudgetFraction.ToString("R", CultureInfo.InvariantCulture)).Append(':')
            .Append(drawback.automaticMaximumBudgetFraction.ToString("R", CultureInfo.InvariantCulture)).Append(':')
            .Append(drawback.absoluteMaximumCredit).Append(':')
            .Append(drawback.requireNegativeEvidenceForAutomatic).Append('\n');
        foreach (float value in policy.milestoneWeights ?? new List<float>())
            material.Append("milestone:").Append(value.ToString("R", CultureInfo.InvariantCulture)).Append('\n');
        foreach (EvolutionModuleDefinition module in new EvolutionModuleRegistry().All
                     .Where(value => value.ModuleId.StartsWith("facility:", StringComparison.Ordinal))
                     .OrderBy(value => value.ModuleId, StringComparer.Ordinal))
        {
            material.Append("module:").Append(module.ModuleId).Append(':')
                .Append((int)module.BurdenKind).Append(':')
                .Append(module.RiskWeight).Append(':')
                .Append(module.MaximumDrawbackSeverity).Append(':')
                .Append(FormatModifiers(module.Benefits)).Append(':')
                .Append(FormatModifiers(module.Burdens)).Append(':')
                .Append(string.Join(",", module.NegativeEvidenceMarkers)).Append(':')
                .Append(string.Join(",", module.ForbiddenSynergyModuleIds)).Append('\n');
        }
        return NarrativeInferenceHash.ComputeSha256Utf8(material.ToString())
            .Substring("sha256:".Length);
    }

    private static string FormatModifiers(
        IEnumerable<EvolutionEffectModifier> modifiers) => string.Join(",",
        (modifiers ?? Array.Empty<EvolutionEffectModifier>())
        .OrderBy(value => value.statId, StringComparer.Ordinal)
        .Select(value => value.statId + ":"
            + value.additive.ToString("R", CultureInfo.InvariantCulture) + ":"
            + value.multiplier.ToString("R", CultureInfo.InvariantCulture)));

    private static void PruneStaleRecipeAssets(IEnumerable<EvolutionRecipeSpec> specs)
    {
        HashSet<string> allowedPaths = specs
            .Select(spec => $"{RecipeFolder}/{spec.assetName}.asset")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (string guid in AssetDatabase.FindAssets("t:FacilityEvolutionRecipeSO", new[] { RecipeFolder }))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (!allowedPaths.Contains(path))
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }

    private static EvolutionRecipeSpec[] CreateRecipeSpecs()
    {
        return new[]
        {
            new EvolutionRecipeSpec(
                9101,
                "EV_CommercialGrill",
                "evolve_commercial_hearth_to_grill",
                "상업 화덕 진화",
                "조리 지원과 손님 회전이 갖춰진 방에서 간이화덕을 고기그릴로 진화시킨다.",
                "D02_고기그릴",
                new[] { "D01_간이화덕" },
                requiredStarGrade: 1,
                resultStarGrade: 2,
                publicByDefault: true,
                requiredResearchRecipeId: string.Empty,
                requiredRoomScores: new[]
                {
                    Min(FacilityEvolutionTerms.Dining, 20f),
                    Min(FacilityEvolutionTerms.Cooking, 14f)
                },
                requiredRoomMetrics: Array.Empty<FacilityEvolutionMetricRequirement>(),
                requiredRecordTokens: Array.Empty<FacilityEvolutionTokenRequirement>(),
                allowedMutationTags: new[] { FacilityEvolutionTerms.Service, FacilityEvolutionTerms.Logistics },
                requiredUniqueFixtureAssetNames: new[] { "D03_조리손질대" },
                identityPressureWeights: new[]
                {
                    Value(FacilityEvolutionTerms.Service, 0.6f),
                    Value(FacilityEvolutionTerms.Logistics, 0.4f)
                },
                minimumIdentityScore: 0.1f),

            new EvolutionRecipeSpec(
                9102,
                "EV_SecureDisplay",
                "evolve_shop_display_to_secure_display",
                "상점 진열장 진화",
                "판매 서비스와 조명이 갖춰진 상점에서 잡화진열선반을 잠금진열장으로 진화시킨다.",
                "S03_잠금진열장",
                new[] { "S02_잡화진열선반" },
                requiredStarGrade: 1,
                resultStarGrade: 2,
                publicByDefault: true,
                requiredResearchRecipeId: string.Empty,
                requiredRoomScores: new[]
                {
                    Min("Shop", 22f),
                    Min(FacilityEvolutionTerms.Service, 5f),
                    Min(FacilityEvolutionTerms.Luxury, 3f)
                },
                requiredRoomMetrics: Array.Empty<FacilityEvolutionMetricRequirement>(),
                requiredRecordTokens: Array.Empty<FacilityEvolutionTokenRequirement>(),
                allowedMutationTags: new[] { FacilityEvolutionTerms.Luxury, FacilityEvolutionTerms.Service },
                requiredUniqueFixtureAssetNames: new[] { "S01_판매카운터", "E07_촛대" },
                identityPressureWeights: new[]
                {
                    Value(FacilityEvolutionTerms.Luxury, 0.45f),
                    Value(FacilityEvolutionTerms.Service, 0.35f),
                    Value(FacilityEvolutionTerms.Security, 0.2f)
                },
                minimumIdentityScore: 0.1f),

            new EvolutionRecipeSpec(
                9103,
                "EV_TacticalTable",
                "evolve_guard_desk_to_tactical_table",
                "경비 지휘 진화",
                "훈련 시설과 함께 운영된 경비초소책상을 전술지도탁자로 진화시킨다.",
                "G04_전술지도탁자",
                new[] { "G01_경비초소책상" },
                requiredStarGrade: 1,
                resultStarGrade: 2,
                publicByDefault: true,
                requiredResearchRecipeId: string.Empty,
                requiredRoomScores: new[]
                {
                    Min(FacilityEvolutionTerms.Training, 22f),
                    Min(FacilityEvolutionTerms.Combat, 10f),
                    Min(FacilityEvolutionTerms.Defense, 2f)
                },
                requiredRoomMetrics: Array.Empty<FacilityEvolutionMetricRequirement>(),
                requiredRecordTokens: Array.Empty<FacilityEvolutionTokenRequirement>(),
                allowedMutationTags: new[] { FacilityEvolutionTerms.Brutal, FacilityEvolutionTerms.Security },
                requiredUniqueFixtureAssetNames: new[] { "T01_훈련허수아비" },
                identityPressureWeights: new[]
                {
                    Value(FacilityEvolutionTerms.Combat, 0.6f),
                    Value(FacilityEvolutionTerms.Security, 0.4f)
                },
                minimumIdentityScore: 0.2f),

            new EvolutionRecipeSpec(
                9104,
                "EV_ArcheryTarget",
                "evolve_training_dummy_to_archery_target",
                "요새 훈련 진화",
                "경비 체계가 갖춰진 훈련실에서 허수아비를 정밀 사격과녁으로 진화시킨다.",
                "T02_사격과녁",
                new[] { "T01_훈련허수아비" },
                requiredStarGrade: 1,
                resultStarGrade: 2,
                publicByDefault: true,
                requiredResearchRecipeId: string.Empty,
                requiredRoomScores: new[]
                {
                    Min(FacilityEvolutionTerms.Combat, 10f),
                    Min(FacilityEvolutionTerms.Defense, 4f),
                    Min(FacilityEvolutionTerms.Security, 5f)
                },
                requiredRoomMetrics: Array.Empty<FacilityEvolutionMetricRequirement>(),
                requiredRecordTokens: Array.Empty<FacilityEvolutionTokenRequirement>(),
                allowedMutationTags: new[] { FacilityEvolutionTerms.Combat, FacilityEvolutionTerms.Security },
                requiredUniqueFixtureAssetNames: new[] { "G01_경비초소책상", "G02_경보종" },
                identityPressureWeights: new[]
                {
                    Value(FacilityEvolutionTerms.Combat, 0.55f),
                    Value(FacilityEvolutionTerms.Security, 0.45f)
                },
                minimumIdentityScore: 0.2f),

            new EvolutionRecipeSpec(
                9105,
                "EV_AlchemyBench",
                "evolve_research_desk_to_alchemy_bench",
                "비전 연구 진화",
                "마력 안정 장치가 있는 연구실에서 연구책상을 연금술작업대로 진화시킨다.",
                "Q02_연금술작업대",
                new[] { "Q01_연구책상" },
                requiredStarGrade: 1,
                resultStarGrade: 2,
                publicByDefault: true,
                requiredResearchRecipeId: string.Empty,
                requiredRoomScores: new[]
                {
                    Min(FacilityEvolutionTerms.Research, 24f),
                    Min(FacilityEvolutionTerms.Mana, 2f),
                    Min(FacilityEvolutionTerms.Security, 2.5f)
                },
                requiredRoomMetrics: Array.Empty<FacilityEvolutionMetricRequirement>(),
                requiredRecordTokens: Array.Empty<FacilityEvolutionTokenRequirement>(),
                allowedMutationTags: new[] { FacilityEvolutionTerms.Research, FacilityEvolutionTerms.Ritual },
                requiredUniqueFixtureAssetNames: new[] { "Q03_연구용책장", "M03_룬안정기" },
                identityPressureWeights: new[]
                {
                    Value(FacilityEvolutionTerms.Ritual, 0.8f),
                    Value(FacilityEvolutionTerms.Security, 0.2f)
                },
                minimumIdentityScore: 0.2f),

            new EvolutionRecipeSpec(
                9106,
                "EV_RitualFocus",
                "evolve_mana_shelf_to_ritual_focus",
                "마력 의식 진화",
                "시약과 의식 조명을 갖춘 마력실에서 수정선반을 의식초점석으로 진화시킨다.",
                "M04_의식초점석",
                new[] { "M01_마력수정선반" },
                requiredStarGrade: 1,
                resultStarGrade: 2,
                publicByDefault: true,
                requiredResearchRecipeId: string.Empty,
                requiredRoomScores: new[]
                {
                    Min(FacilityEvolutionTerms.Mana, 24f),
                    Min(FacilityEvolutionTerms.Research, 2f),
                    Min(FacilityEvolutionTerms.Luxury, 3f)
                },
                requiredRoomMetrics: Array.Empty<FacilityEvolutionMetricRequirement>(),
                requiredRecordTokens: Array.Empty<FacilityEvolutionTokenRequirement>(),
                allowedMutationTags: new[] { FacilityEvolutionTerms.Mana, FacilityEvolutionTerms.Ritual },
                requiredUniqueFixtureAssetNames: new[] { "Q04_시약선반", "E07_촛대" },
                identityPressureWeights: new[]
                {
                    Value(FacilityEvolutionTerms.Ritual, 0.8f),
                    Value(FacilityEvolutionTerms.Luxury, 0.2f)
                },
                minimumIdentityScore: 0.2f)
        };
    }

    private static RecordTokenDefinitionSpec[] CreateRecordTokenDefinitionSpecs()
    {
        return new[]
        {
            new RecordTokenDefinitionSpec(
                9201,
                "RT_MercenaryHangout",
                FacilityEvolutionTerms.MercenaryHangout,
                "용병 단골화",
                "용병 손님이 반복해서 이용한 기록이다. 전투/연회 계보의 역사 증거로 남긴다.",
                FacilityEvolutionTerms.CombatVisitorRatio,
                0.25f,
                "recent+long-term",
                FacilityEvolutionRecordTokenConsumePolicy.Preserve,
                new[] { FacilityEvolutionTerms.Combat, FacilityEvolutionTerms.Dining },
                "용병 손님이 이 시설의 분위기를 바꾸고 있습니다."),

            new RecordTokenDefinitionSpec(
                9202,
                "RT_HighMeatConsumption",
                FacilityEvolutionTerms.HighMeatConsumption,
                "고기 소비 과다",
                "고기 재고를 많이 소비한 기록이다. 식당, 연회, 전투 식당 계보에 영향을 준다.",
                FacilityEvolutionTerms.StockCostPerVisit,
                1f,
                "daily",
                FacilityEvolutionRecordTokenConsumePolicy.Preserve,
                new[] { FacilityEvolutionTerms.Meat, FacilityEvolutionTerms.Dining },
                "고기 소비가 시설의 정체성으로 굳어지고 있습니다."),

            new RecordTokenDefinitionSpec(
                9203,
                "RT_FrequentBrawls",
                FacilityEvolutionTerms.FrequentBrawls,
                "잦은 소란",
                "싸움과 소란이 반복된 기록이다. 전투/무법 계보의 변이 후보를 강화한다.",
                FacilityEvolutionTerms.BrawlCount,
                1f,
                "recent-weighted",
                FacilityEvolutionRecordTokenConsumePolicy.Preserve,
                new[] { FacilityEvolutionTerms.Combat, FacilityEvolutionTerms.Outlaw },
                "소란이 단순 사고가 아니라 이 시설의 문화가 되고 있습니다."),

            new RecordTokenDefinitionSpec(
                9204,
                "RT_NoblePatronage",
                FacilityEvolutionTerms.NoblePatronage,
                "귀족 후원",
                "귀족 손님이 반복 방문하거나 큰돈을 지불한 기록이다. 고급/귀족 계보의 역사 증거로 남긴다.",
                FacilityEvolutionTerms.NobleVisitorRatio,
                0.25f,
                "long-term",
                FacilityEvolutionRecordTokenConsumePolicy.Preserve,
                new[] { FacilityEvolutionTerms.Luxury, FacilityEvolutionTerms.Noble },
                "귀족층이 이 시설을 알아보기 시작했습니다."),

            new RecordTokenDefinitionSpec(
                9205,
                "RT_GuardRallyPoint",
                FacilityEvolutionTerms.GuardRallyPoint,
                "경비 집결지",
                "경비나 전투 인원이 이 시설을 집결지처럼 이용한 기록이다.",
                FacilityEvolutionTerms.ZoneSafetyScore,
                0.4f,
                "recent+long-term",
                FacilityEvolutionRecordTokenConsumePolicy.Preserve,
                new[] { FacilityEvolutionTerms.Security, FacilityEvolutionTerms.Combat },
                "이 시설 주변이 경비 동선의 기준점이 되고 있습니다."),

            new RecordTokenDefinitionSpec(
                9206,
                "RT_IntruderBloodied",
                FacilityEvolutionTerms.IntruderBloodied,
                "침입자 유혈 기록",
                "침입자에게 피해를 입히거나 침입을 저지한 기록이다.",
                FacilityEvolutionTerms.IntruderDamageDealt,
                1f,
                "event-memory",
                FacilityEvolutionRecordTokenConsumePolicy.Preserve,
                new[] { FacilityEvolutionTerms.Security, FacilityEvolutionTerms.Brutal },
                "침입자와의 충돌이 시설의 평판에 남았습니다."),

            new RecordTokenDefinitionSpec(
                9207,
                "RT_CleanServiceStreak",
                FacilityEvolutionTerms.CleanServiceStreak,
                "청결한 서비스 연속",
                "높은 만족도와 낮은 실패율이 이어진 기록이다.",
                FacilityEvolutionTerms.ServiceQuality,
                0.7f,
                "streak",
                FacilityEvolutionRecordTokenConsumePolicy.Preserve,
                new[] { FacilityEvolutionTerms.Service, FacilityEvolutionTerms.Luxury },
                "안정적인 서비스가 고급화의 근거가 되고 있습니다."),

            new RecordTokenDefinitionSpec(
                9208,
                "RT_HighTurnoverService",
                FacilityEvolutionTerms.HighTurnoverService,
                "빠른 회전 서비스",
                "많은 손님을 빠르게 처리한 기록이다.",
                FacilityEvolutionTerms.TurnoverRate,
                0.65f,
                "daily",
                FacilityEvolutionRecordTokenConsumePolicy.Preserve,
                new[] { FacilityEvolutionTerms.Crowd, FacilityEvolutionTerms.Service },
                "손님을 빠르게 돌리는 운영이 자리 잡고 있습니다."),

            new RecordTokenDefinitionSpec(
                9209,
                "RT_OutlawRumor",
                FacilityEvolutionTerms.OutlawRumor,
                "무법자 소문",
                "범죄, 절도, 부정적 소문이 시설에 붙은 기록이다.",
                FacilityEvolutionTerms.NegativeMentionCount,
                1f,
                "rumor-decay",
                FacilityEvolutionRecordTokenConsumePolicy.Preserve,
                new[] { FacilityEvolutionTerms.Outlaw, FacilityEvolutionTerms.Fear },
                "나쁜 소문이 다른 가능성의 그림자를 만들고 있습니다.")
        };
    }

    private static BuildingSO LoadBuilding(string assetName)
    {
        if (string.IsNullOrWhiteSpace(assetName))
        {
            return null;
        }

        return AssetDatabase.LoadAssetAtPath<BuildingSO>($"{BuildingFolder}/{assetName}.asset");
    }

    private static FacilityEvolutionValue Value(string key, float value)
    {
        return new FacilityEvolutionValue(key, value);
    }

    private static FacilityEvolutionMetricRequirement Min(string key, float value)
    {
        return new FacilityEvolutionMetricRequirement
        {
            key = key,
            requireMin = true,
            minValue = value
        };
    }

    private static FacilityEvolutionMetricRequirement Max(string key, float value)
    {
        return new FacilityEvolutionMetricRequirement
        {
            key = key,
            requireMax = true,
            maxValue = value
        };
    }

    private static FacilityEvolutionTokenRequirement Token(string key, int count)
    {
        return new FacilityEvolutionTokenRequirement
        {
            key = key,
            minCount = count
        };
    }

    private static void EnsureFolder(string folder)
    {
        string normalized = folder.Replace('\\', '/');
        string[] parts = normalized.Split('/');
        string current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            string next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[i]);
            }

            current = next;
        }
    }

    private sealed class EvolutionRecipeSpec
    {
        public EvolutionRecipeSpec(
            int id,
            string assetName,
            string evolutionId,
            string displayName,
            string description,
            string resultAssetName,
            string[] sourceAssetNames,
            int requiredStarGrade,
            int resultStarGrade,
            bool publicByDefault,
            string requiredResearchRecipeId,
            FacilityEvolutionMetricRequirement[] requiredRoomScores,
            FacilityEvolutionMetricRequirement[] requiredRoomMetrics,
            FacilityEvolutionTokenRequirement[] requiredRecordTokens,
            string[] allowedMutationTags,
            string[] sourceLineageTags = null,
            string[] requiredRoomTags = null,
            string[] requiredUniqueFixtureAssetNames = null,
            FacilityEvolutionMaterialRequirement[] requiredMaterials = null,
            FacilityEvolutionValue[] identityPressureWeights = null,
            float minimumIdentityScore = 0f,
            bool consumeRecordTokens = false)
        {
            this.id = id;
            this.assetName = assetName;
            this.evolutionId = evolutionId;
            this.displayName = displayName;
            this.description = description;
            this.resultAssetName = resultAssetName;
            this.sourceAssetNames = sourceAssetNames ?? Array.Empty<string>();
            this.requiredStarGrade = requiredStarGrade;
            this.resultStarGrade = resultStarGrade;
            this.publicByDefault = publicByDefault;
            this.requiredResearchRecipeId = requiredResearchRecipeId ?? string.Empty;
            this.requiredRoomScores = requiredRoomScores ?? Array.Empty<FacilityEvolutionMetricRequirement>();
            this.requiredRoomMetrics = requiredRoomMetrics ?? Array.Empty<FacilityEvolutionMetricRequirement>();
            this.requiredRecordTokens = requiredRecordTokens ?? Array.Empty<FacilityEvolutionTokenRequirement>();
            this.allowedMutationTags = allowedMutationTags ?? Array.Empty<string>();
            this.sourceLineageTags = sourceLineageTags ?? Array.Empty<string>();
            this.requiredRoomTags = requiredRoomTags ?? Array.Empty<string>();
            this.requiredUniqueFixtureAssetNames = requiredUniqueFixtureAssetNames ?? Array.Empty<string>();
            this.requiredMaterials = requiredMaterials ?? Array.Empty<FacilityEvolutionMaterialRequirement>();
            this.identityPressureWeights = identityPressureWeights ?? Array.Empty<FacilityEvolutionValue>();
            this.minimumIdentityScore = Mathf.Clamp01(minimumIdentityScore);
            this.consumeRecordTokens = consumeRecordTokens;
        }

        public readonly int id;
        public readonly string assetName;
        public readonly string evolutionId;
        public readonly string displayName;
        public readonly string description;
        public readonly string resultAssetName;
        public readonly string[] sourceAssetNames;
        public readonly int requiredStarGrade;
        public readonly int resultStarGrade;
        public readonly bool publicByDefault;
        public readonly string requiredResearchRecipeId;
        public readonly FacilityEvolutionMetricRequirement[] requiredRoomScores;
        public readonly FacilityEvolutionMetricRequirement[] requiredRoomMetrics;
        public readonly FacilityEvolutionTokenRequirement[] requiredRecordTokens;
        public readonly string[] allowedMutationTags;
        public readonly string[] sourceLineageTags;
        public readonly string[] requiredRoomTags;
        public readonly string[] requiredUniqueFixtureAssetNames;
        public readonly FacilityEvolutionMaterialRequirement[] requiredMaterials;
        public readonly FacilityEvolutionValue[] identityPressureWeights;
        public readonly float minimumIdentityScore;
        public readonly bool consumeRecordTokens;
    }

    private sealed class RecordTokenDefinitionSpec
    {
        public RecordTokenDefinitionSpec(
            int id,
            string assetName,
            string tokenId,
            string displayName,
            string description,
            string sourceMetric,
            float threshold,
            string decayPolicy,
            FacilityEvolutionRecordTokenConsumePolicy consumePolicy,
            string[] recipeTags,
            string uiHint)
        {
            this.id = id;
            this.assetName = assetName;
            this.tokenId = tokenId;
            this.displayName = displayName;
            this.description = description;
            this.sourceMetric = sourceMetric;
            this.threshold = threshold;
            this.decayPolicy = decayPolicy;
            this.consumePolicy = consumePolicy;
            this.recipeTags = recipeTags ?? Array.Empty<string>();
            this.uiHint = uiHint;
        }

        public readonly int id;
        public readonly string assetName;
        public readonly string tokenId;
        public readonly string displayName;
        public readonly string description;
        public readonly string sourceMetric;
        public readonly float threshold;
        public readonly string decayPolicy;
        public readonly FacilityEvolutionRecordTokenConsumePolicy consumePolicy;
        public readonly string[] recipeTags;
        public readonly string uiHint;
    }
}
