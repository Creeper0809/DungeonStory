#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionWorkStatPolicyMaximumContributorDebugScenarios
{
    private const string FacilityId = "building:qa-stat-contributor";
    private const string WorkstationTag = "workstation:qa-stat-contributor";

    [MenuItem("DungeonStory/V27/Production/Validate Work Stat Maximum Contributor")]
    public static void Validate()
    {
        VerifyNeutralAndGatheringMaximums();
        VerifyRepeatedCaptureIsDeterministic();
        VerifyWrongWorkTypeAndMissingSourceAreTypedGaps();
        VerifyCharacterPerformanceMaximumContributor();
        VerifyCharacterWorkContextMaximumContributor();
        VerifyWorkEnvironmentMaximumContributor();
        VerifyCraftsmanshipMaximumContributor();
        VerifyFacilityDefinitionCatalogRuntimeArchetypeBoundary();
        VerifyFacilityEvolutionMaximumContributor();
        VerifyAutomationAssistedMaximumContributor();
        VerifyAutomaticWorkRateMaximumQuery();
        Debug.Log(
            "[ProductionWorkStatPolicyMaximumContributor] focused scenarios passed.");
    }

    private static void VerifyNeutralAndGatheringMaximums()
    {
        ProductionRecipeSO craft = Recipe(
            "recipe:qa-stat-neutral",
            BuiltInWorkTypeIds.Craft);
        ProductionRecipeSO gather = Recipe(
            "recipe:qa-stat-gather",
            BuiltInWorkTypeIds.Gather);
        try
        {
            ProductionWorkStatPolicyMaximumContributor neutral = new(
                new WorkStatPolicyRegistry(Array.Empty<IWorkStatPolicy>()));
            ProductionWorkStatPolicyMaximumContributor gathering = new(
                new WorkStatPolicyRegistry(new IWorkStatPolicy[]
                {
                    new GatheringStatPolicy(new EmptyFacilityCapabilityQuery())
                }));

            ProductionWorkRateMaximumContributorResult neutralResult = neutral
                .Capture(Context(craft));
            ProductionWorkRateMaximumContributorResult gatheringResult = gathering
                .Capture(Context(gather));
            Require(neutralResult.HasUpperBound
                    && neutralResult.UpperBound.ScaledValue
                    == ProductionWorkRateFixedPointUpperBound.Scale,
                "A neutral work-stat maximum did not publish exactly 1.0.");
            Require(gatheringResult.HasUpperBound
                    && gatheringResult.UpperBound.ScaledValue >= 1_100_000_000L
                    && gatheringResult.UpperBound.ScaledValue <= 1_100_000_001L,
                "The gathering work-stat maximum is not a conservative 1.10 bound.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(craft);
            UnityEngine.Object.DestroyImmediate(gather);
        }
    }

    private static void VerifyRepeatedCaptureIsDeterministic()
    {
        ProductionRecipeSO recipe = Recipe(
            "recipe:qa-stat-deterministic",
            BuiltInWorkTypeIds.Craft);
        try
        {
            ProductionWorkStatPolicyMaximumContributor contributor = new(
                new WorkStatPolicyRegistry(Array.Empty<IWorkStatPolicy>()));
            ProductionWorkRateMaximumContributorResult first = contributor
                .Capture(Context(recipe));
            ProductionWorkRateMaximumContributorResult second = contributor
                .Capture(Context(recipe));
            Require(first.HasUpperBound
                    && second.HasUpperBound
                    && first.UpperBound.Equals(second.UpperBound)
                    && string.Equals(
                        first.SourceDigest,
                        second.SourceDigest,
                        StringComparison.Ordinal),
                "Repeated work-stat contributor capture is not deterministic.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static void VerifyWrongWorkTypeAndMissingSourceAreTypedGaps()
    {
        ProductionRecipeSO recipe = Recipe(
            "recipe:qa-stat-gap",
            BuiltInWorkTypeIds.Craft);
        try
        {
            ProductionWorkStatPolicyMaximumContributor wrong = new(
                new WrongWorkTypeQuery());
            ProductionWorkStatPolicyMaximumContributor missing = new(
                new ThrowingMaximumQuery());
            ProductionWorkRateMaximumContributorResult wrongResult = wrong
                .Capture(Context(recipe));
            ProductionWorkRateMaximumContributorResult missingResult = missing
                .Capture(Context(recipe));
            Require(!wrongResult.HasUpperBound
                    && wrongResult.MissingReason
                    == ProductionRecipeWorkRateMaximumGapReason.ContributorRejected
                    && wrongResult.Detail.Contains(
                        "WORK_TYPE_MISMATCH",
                        StringComparison.Ordinal),
                "A wrong-work-type maximum was not retained as a typed gap.");
            Require(!missingResult.HasUpperBound
                    && missingResult.MissingReason
                    == ProductionRecipeWorkRateMaximumGapReason.ContributorRejected
                    && missingResult.Detail.Contains(
                        "NO_MAXIMUM",
                        StringComparison.Ordinal),
                "A missing maximum source was not retained as a typed gap.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static void VerifyCharacterPerformanceMaximumContributor()
    {
        CharacterPerformanceFormulaDefinitionSO formula = ScriptableObject
            .CreateInstance<CharacterPerformanceFormulaDefinitionSO>();
        ProductionRecipeSO recipe = Recipe(
            "recipe:qa-performance-contributor",
            BuiltInWorkTypeIds.Craft);
        ProductionRecipeSO unmapped = Recipe(
            "recipe:qa-performance-unmapped",
            BuiltInWorkTypeIds.Gather);
        try
        {
            formula.Configure(
                "performance:qa:craft-speed",
                "QA craft speed",
                CharacterPerformanceFormulaDomain.Work,
                CharacterPerformanceResultChannel.Speed,
                1f,
                new[]
                {
                    new CharacterPerformanceCapacityInput(
                        CharacterFunctionalCapacityId.PrecisionManipulation,
                        1f,
                        CharacterPerformanceInputRole.Contribution)
                },
                BuiltInCharacterProficiencyIds.Crafting.Value,
                string.Empty,
                0f,
                string.Empty,
                BuiltInWorkTypeIds.Craft.Value);
            CharacterPerformanceFormulaCatalog catalog = new(
                new DefinitionSource(formula));
            ProductionCharacterPerformanceMaximumContributor contributor = new(
                catalog,
                new FixedPerformanceMaximumQuery(
                    formula.FormulaId,
                    formula.FormulaId,
                    1.75d));
            ProductionCharacterPerformanceMaximumContributor wrong = new(
                catalog,
                new FixedPerformanceMaximumQuery(
                    formula.FormulaId,
                    "performance:qa:wrong",
                    1.75d));

            ProductionWorkRateMaximumContributorResult complete = contributor
                .Capture(Context(recipe));
            ProductionWorkRateMaximumContributorResult mismatch = wrong
                .Capture(Context(recipe));
            ProductionWorkRateMaximumContributorResult missing = contributor
                .Capture(Context(unmapped));
            Require(complete.HasUpperBound
                    && complete.UpperBound.ScaledValue == 1_750_000_000L,
                "The character performance definition maximum was not published.");
            Require(!mismatch.HasUpperBound
                    && mismatch.Detail.Contains(
                        "FORMULA_ID_MISMATCH",
                        StringComparison.Ordinal),
                "A mismatched performance formula maximum was not rejected.");
            Require(!missing.HasUpperBound
                    && missing.MissingReason
                    == ProductionRecipeWorkRateMaximumGapReason.ContributorRejected,
                "An unmapped work performance formula was not a typed gap.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(formula);
            UnityEngine.Object.DestroyImmediate(recipe);
            UnityEngine.Object.DestroyImmediate(unmapped);
        }
    }

    private static void VerifyCharacterWorkContextMaximumContributor()
    {
        ProductionRecipeSO recipe = Recipe(
            "recipe:qa-character-context-maximum",
            BuiltInWorkTypeIds.Craft);
        try
        {
            CharacterWorkContextDefinitionMaximumQuery query = new(
                new GameplayEffectResultBoundsCatalog(
                    Array.Empty<GameplayEffectDefinitionSO>()));
            ProductionCharacterWorkContextMaximumContributor contributor = new(
                query);
            ProductionWorkRateMaximumContributorResult result = contributor
                .Capture(Context(recipe));
            Require(result.HasUpperBound
                    && result.UpperBound.ScaledValue == 4_375_000_000L,
                "The exact nine-factor character context maximum was not "
                + "published for ordinary work.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static void VerifyWorkEnvironmentMaximumContributor()
    {
        ProductionRecipeSO craft = Recipe(
            "recipe:qa-room-environment-craft",
            BuiltInWorkTypeIds.Craft);
        ProductionRecipeSO cook = Recipe(
            "recipe:qa-room-environment-cook",
            BuiltInWorkTypeIds.Cook);
        try
        {
            ProductionWorkEnvironmentMaximumContributor contributor = new(
                new RoomMaximumQuery());
            ProductionWorkRateMaximumContributorResult craftResult = contributor
                .Capture(Context(craft));
            ProductionWorkRateMaximumContributorResult cookResult = contributor
                .Capture(Context(cook));
            Require(ProductionWorkRateFixedPointUpperBound.TryFromDoubleUpperBound(
                    RoomWorkEnvironmentRateAuthority.MaximumSpeedMultiplier,
                    out ProductionWorkRateFixedPointUpperBound expectedCraft,
                    out ProductionRecipeWorkRateMaximumGapReason failureReason),
                "The authored room maximum could not be quantized: "
                + failureReason);
            Require(craftResult.HasUpperBound
                    && craftResult.UpperBound.Equals(expectedCraft),
                "Craft did not publish the authored 1.15 room maximum.");
            Require(cookResult.HasUpperBound
                    && cookResult.UpperBound.ScaledValue
                    == ProductionWorkRateFixedPointUpperBound.Scale,
                "Cook incorrectly inherited a room work-speed bonus.");
            Require(Mathf.Approximately(
                        RoomWorkEnvironmentRateAuthority.ResolveSpeedMultiplier(0f),
                        RoomWorkEnvironmentRateAuthority.MinimumSpeedMultiplier)
                    && Mathf.Approximately(
                        RoomWorkEnvironmentRateAuthority.ResolveSpeedMultiplier(100f),
                        RoomWorkEnvironmentRateAuthority.MaximumSpeedMultiplier),
                "The shared live room speed formula drifted at its endpoints.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(craft);
            UnityEngine.Object.DestroyImmediate(cook);
        }
    }

    private static void VerifyCraftsmanshipMaximumContributor()
    {
        ProductionRecipeSO recipe = Recipe(
            "recipe:qa-craftsmanship-maximum",
            BuiltInWorkTypeIds.Craft);
        try
        {
            BuildingCraftsmanshipDefinitionMaximumQuery query = new();
            BuildingCraftsmanshipDefinitionMaximumSnapshot snapshot = query
                .Capture(FacilityId);
            ProductionCraftsmanshipMaximumContributor contributor = new(query);
            ProductionWorkRateMaximumContributorResult result = contributor
                .Capture(Context(recipe));
            Require(ProductionWorkRateFixedPointUpperBound.TryFromDoubleUpperBound(
                    snapshot.MaximumMultiplier,
                    out ProductionWorkRateFixedPointUpperBound expected,
                    out ProductionRecipeWorkRateMaximumGapReason failureReason),
                "The craftsmanship maximum could not be quantized: "
                + failureReason);
            Require(snapshot.MaximumTier == CraftsmanshipQualityTier.Mythic
                    && result.HasUpperBound
                    && result.UpperBound.Equals(expected),
                "The restore-valid Mythic craftsmanship maximum was not published.");
            bool invalidRejected = false;
            try
            {
                CraftsmanshipQualityRules.ProjectionMultiplier(
                    (CraftsmanshipQualityTier)int.MaxValue);
            }
            catch (ArgumentOutOfRangeException)
            {
                invalidRejected = true;
            }
            Require(invalidRejected,
                "An undefined future craftsmanship tier received a neutral fallback.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static void VerifyFacilityDefinitionCatalogRuntimeArchetypeBoundary()
    {
        BuildingSO authored = ScriptableObject.CreateInstance<BuildingSO>();
        BuildingSO runtimeOnly = ScriptableObject.CreateInstance<BuildingSO>();
        BuildingSO malformedWorkstation =
            ScriptableObject.CreateInstance<BuildingSO>();
        try
        {
            authored.ConfigureAuthoredContentIdentity(
                FacilityId,
                1,
                "qa-authored-definition");
            runtimeOnly.id = -1_950_010_000;
            malformedWorkstation.id = -1_950_010_001;
            BuildingAbilityCollection abilities = new();
            abilities.Add(new BuildingProductionWorkstationAbility
            {
                workstationTag = WorkstationTag,
                lanePolicy = ProductionWorkstationLanePolicy
                    .ManualWithDetachedBatchProcessors,
                manualWorkLaneCount = 1,
                automaticWorkLaneCount = 0
            });
            malformedWorkstation.ReplaceAbilities(abilities);

            ProductionFacilityDefinitionCatalog catalog = new(
                new DefinitionSource(runtimeOnly, authored));
            Require(catalog.DefinitionCount == 1
                    && catalog.IgnoredRuntimeArchetypeCount == 1
                    && ReferenceEquals(catalog.Require(FacilityId), authored),
                "The facility catalog did not isolate an identityless runtime "
                + "archetype from authored definitions.");

            bool malformedRejected = false;
            try
            {
                _ = new ProductionFacilityDefinitionCatalog(
                    new DefinitionSource(malformedWorkstation));
            }
            catch (InvalidOperationException exception)
            {
                malformedRejected = exception.Message.Contains(
                    "authored production workstation",
                    StringComparison.Ordinal);
            }
            Require(malformedRejected,
                "An identityless authored production workstation was silently "
                + "excluded as a runtime archetype.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(authored);
            UnityEngine.Object.DestroyImmediate(runtimeOnly);
            UnityEngine.Object.DestroyImmediate(malformedWorkstation);
        }
    }

    private static void VerifyFacilityEvolutionMaximumContributor()
    {
        BuildingSO service = ScriptableObject.CreateInstance<BuildingSO>();
        ProductionRecipeSO recipe = Recipe(
            "recipe:qa-evolution-maximum",
            BuiltInWorkTypeIds.Operate);
        try
        {
            service.ConfigureAuthoredContentIdentity(
                FacilityId,
                1,
                "qa-evolution-maximum");
            service.Facility = new FacilityData
            {
                roles = FacilityRole.Meal,
                capacity = 1
            };
            ProductionFacilityDefinitionCatalog definitions = new(
                new DefinitionSource(service));
            ProductionFacilityEvolutionWorkRateMaximumContributor contributor =
                new(
                    definitions,
                    new FacilityEvolutionWorkSpeedDefinitionMaximumQuery(
                        new EvolutionModuleRegistry()));
            ProductionWorkRateMaximumContributorResult result = contributor
                .Capture(Context(recipe));
            Require(result.HasUpperBound
                    && result.UpperBound.ScaledValue
                    == 8L * ProductionWorkRateFixedPointUpperBound.Scale,
                "The facility-evolution maximum was not published at the "
                + "shared live clamp.");

            ProductionFacilityEvolutionWorkRateMaximumContributor missing = new(
                new ProductionFacilityDefinitionCatalog(
                    new DefinitionSource()),
                new FacilityEvolutionWorkSpeedDefinitionMaximumQuery(
                    new EvolutionModuleRegistry()));
            ProductionWorkRateMaximumContributorResult missingResult = missing
                .Capture(Context(recipe));
            Require(!missingResult.HasUpperBound
                    && missingResult.MissingReason
                    == ProductionRecipeWorkRateMaximumGapReason
                        .ContributorRejected,
                "A missing facility definition did not remain a typed gap.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(service);
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static void VerifyAutomationAssistedMaximumContributor()
    {
        const string ManualFacilityId = "building:qa-stat-manual";
        BuildingSO assisted = ScriptableObject.CreateInstance<BuildingSO>();
        BuildingSO manual = ScriptableObject.CreateInstance<BuildingSO>();
        ProductionRecipeSO recipe = Recipe(
            "recipe:qa-assisted-maximum",
            BuiltInWorkTypeIds.Craft);
        try
        {
            assisted.ConfigureAuthoredContentIdentity(
                FacilityId,
                1,
                "qa-assisted-maximum");
            BuildingAbilityCollection abilities = new();
            abilities.Add(new BuildingAutomationAbility
            {
                maximumMode = AutomationMode.Automatic,
                assistedWorkMultiplier = 1.35f,
                automaticWorkPerSecond = 1.25f
            });
            assisted.ReplaceAbilities(abilities);
            manual.ConfigureAuthoredContentIdentity(
                ManualFacilityId,
                1,
                "qa-manual-maximum");

            ProductionAutomationAssistedWorkDefinitionMaximumQuery query = new(
                new ProductionFacilityDefinitionCatalog(
                    new DefinitionSource(assisted, manual)));
            AutomationAssistedWorkDefinitionMaximumSnapshot assistedSnapshot =
                query.Capture(FacilityId);
            AutomationAssistedWorkDefinitionMaximumSnapshot manualSnapshot =
                query.Capture(ManualFacilityId);
            ProductionAutomationAssistedWorkMaximumContributor contributor = new(
                query);
            ProductionWorkRateMaximumContributorResult result = contributor
                .Capture(Context(recipe));
            Require(ProductionWorkRateFixedPointUpperBound.TryFromDoubleUpperBound(
                    assistedSnapshot.MaximumMultiplier,
                    out ProductionWorkRateFixedPointUpperBound expected,
                    out ProductionRecipeWorkRateMaximumGapReason failureReason),
                "The assisted maximum could not be quantized: " + failureReason);
            Require(assistedSnapshot.MaximumMode == AutomationMode.Automatic
                    && assistedSnapshot.MaximumMultiplier > 1.349999d
                    && assistedSnapshot.MaximumMultiplier < 1.350001d
                    && result.HasUpperBound
                    && result.UpperBound.Equals(expected),
                "The authored PoweredAssist multiplier was not published.");
            Require(manualSnapshot.MaximumMode == AutomationMode.Manual
                    && manualSnapshot.MaximumMultiplier.Equals(1d),
                "A facility without automation did not remain neutral.");
            Require(Mathf.Approximately(
                    AutomationWorkRateAuthority.ResolveConditionMultiplier(
                        AutomationWorkRateAuthority.MaintenanceFullCondition,
                        0f),
                    AutomationWorkRateAuthority.MaximumConditionMultiplier),
                "The shared automation condition maximum drifted.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(assisted);
            UnityEngine.Object.DestroyImmediate(manual);
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static void VerifyAutomaticWorkRateMaximumQuery()
    {
        BuildingSO automatic = ScriptableObject.CreateInstance<BuildingSO>();
        ProductionRecipeSO recipe = Recipe(
            "recipe:qa-automatic-rate",
            BuiltInWorkTypeIds.Craft);
        try
        {
            automatic.ConfigureAuthoredContentIdentity(
                FacilityId,
                1,
                "qa-automatic-rate");
            BuildingAbilityCollection abilities = new();
            abilities.Add(new BuildingAutomationAbility
            {
                maximumMode = AutomationMode.Automatic,
                assistedWorkMultiplier = 1.35f,
                automaticWorkPerSecond = 1.25f
            });
            automatic.ReplaceAbilities(abilities);
            ProductionFacilityDefinitionCatalog definitions = new(
                new DefinitionSource(automatic));
            ProductionAutomaticWorkRateMaximumQuery automaticQuery = new(
                definitions);
            ProductionRecipeWorkRateMaximumContext automaticContext = Context(
                recipe,
                automaticLanes: true);
            ProductionWorkRateMaximumContributorResult direct = automaticQuery
                .Capture(automaticContext);
            ProductionWorkRateMaximumContributorResult laneMismatch =
                automaticQuery.Capture(Context(recipe));
            Require(direct.HasUpperBound
                    && direct.UpperBound.ScaledValue == 1_250_000_000L,
                "The authored automatic work rate was not published.");
            Require(!laneMismatch.HasUpperBound
                    && laneMismatch.MissingReason
                    == ProductionRecipeWorkRateMaximumGapReason
                        .AutomaticLaneMismatch,
                "A manual-only lane received an automatic work rate.");

            IProductionRecipeWorkRateMaximumContributor stat =
                new ProductionWorkStatPolicyMaximumContributor(
                    new WorkStatPolicyRegistry(
                        Array.Empty<IWorkStatPolicy>()));
            ProductionRecipeWorkRateMaximumAuthority authority = new(
                new ProductionWorkRateContributorManifest(new[]
                {
                    ProductionWorkStatPolicyMaximumContributor
                        .StableContributorId
                }),
                new[] { stat },
                automaticQuery);
            ProductionRecipeWorkRateMaximumAuthorityResult integrated = authority
                .CaptureDetailed(
                    FacilityId,
                    WorkstationTag,
                    automaticContext.LaneProfile,
                    recipe);
            Require(integrated.HasSnapshot
                    && integrated.Snapshot.AutomaticMilliWuPerSecond == 1_250L
                    && integrated.Snapshot.ManualMilliWuPerSecond == 1_000L,
                "The actual automatic-rate query did not integrate with the "
                + "recipe work-rate authority.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(automatic);
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static ProductionRecipeWorkRateMaximumContext Context(
        ProductionRecipeSO recipe,
        bool automaticLanes = false) => new(
        FacilityId,
        WorkstationTag,
        new ProductionFacilityWorkstationLaneCapacityProfile(
            automaticLanes
                ? ProductionWorkstationLanePolicy
                    .ModeExclusiveManualOrAutomaticWithDetachedBatchProcessors
                : ProductionWorkstationLanePolicy
                    .ManualWithDetachedBatchProcessors,
            1,
            automaticLanes ? 1 : 0),
        recipe);

    private static ProductionRecipeSO Recipe(
        string recipeId,
        WorkTypeId workTypeId)
    {
        ProductionRecipeSO recipe = ScriptableObject
            .CreateInstance<ProductionRecipeSO>();
        recipe.Configure(
            recipeId,
            recipeId,
            string.Empty,
            "qa-stat-contributor",
            workTypeId.Value,
            string.Empty,
            1f,
            Array.Empty<ItemAmountDefinition>(),
            new[]
            {
                new ProductionOutputDefinition(
                    "output:qa-main",
                    ProductionOutputRole.Main,
                    "resource:qa-output",
                    1)
            });
        recipe.ConfigureWorkshop(
            WorkstationTag,
            Array.Empty<string>(),
            ProductionProcessKind.WorkOnly);
        recipe.ConfigureProficiency(BuiltInCharacterProficiencyIds.Crafting);
        recipe.ConfigureProcessClass(ProductionProcessClass.CookingSimpleMixing);
        return recipe;
    }

    private static string Digest(string value)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("production-work-stat-contributor-qa@1");
        digest.Append(value);
        return digest.ComputeSha256();
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private sealed class EmptyFacilityCapabilityQuery : IFacilityCapabilityQuery
    {
        public IReadOnlyList<BuildableObject> FindOperational(
            FacilityCapabilityKind capability,
            string buildingDefinitionId = "") => Array.Empty<BuildableObject>();

        public IReadOnlyList<BuildableObject> FindOperational(
            ResearchFacilityCommandKind command) => Array.Empty<BuildableObject>();
    }

    private sealed class WrongWorkTypeQuery :
        IWorkStatPolicyDefinitionMaximumQuery
    {
        public WorkStatPolicyDefinitionMaximumSnapshot CaptureDefinitionMaximum(
            WorkTypeId workTypeId) => new(
            BuiltInWorkTypeIds.Gather,
            1d,
            Digest("wrong-work-type"));
    }

    private sealed class ThrowingMaximumQuery :
        IWorkStatPolicyDefinitionMaximumQuery
    {
        public WorkStatPolicyDefinitionMaximumSnapshot CaptureDefinitionMaximum(
            WorkTypeId workTypeId) => throw new InvalidOperationException(
            "NO_MAXIMUM");
    }

    private sealed class DefinitionSource : IGameContentDefinitionSource
    {
        private readonly ScriptableObject[] values;

        internal DefinitionSource(params ScriptableObject[] values)
        {
            this.values = values ?? Array.Empty<ScriptableObject>();
        }

        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject =>
            values.OfType<T>().ToArray();

        public T RequireSingle<T>() where T : ScriptableObject =>
            GetAll<T>().Single();
    }

    private sealed class FixedPerformanceMaximumQuery :
        ICharacterPerformanceDefinitionMaximumQuery
    {
        private readonly string requiredFormulaId;
        private readonly string returnedFormulaId;
        private readonly double maximum;

        internal FixedPerformanceMaximumQuery(
            string requiredFormulaId,
            string returnedFormulaId,
            double maximum)
        {
            this.requiredFormulaId = requiredFormulaId;
            this.returnedFormulaId = returnedFormulaId;
            this.maximum = maximum;
        }

        public CharacterPerformanceDefinitionMaximumSnapshot Capture(
            string formulaId)
        {
            if (!string.Equals(
                    formulaId,
                    requiredFormulaId,
                    StringComparison.Ordinal))
            {
                throw new KeyNotFoundException("NO_PERFORMANCE_MAXIMUM");
            }
            return new CharacterPerformanceDefinitionMaximumSnapshot(
                returnedFormulaId,
                maximum,
                1d,
                maximum,
                1d,
                Digest("performance:" + returnedFormulaId));
        }
    }

    private sealed class RoomMaximumQuery :
        IWorkEnvironmentDefinitionMaximumQuery
    {
        public WorkEnvironmentDefinitionMaximumSnapshot
            CaptureDefinitionMaximum(WorkTypeId workTypeId) =>
            RoomWorkEnvironmentRateAuthority.CaptureDefinitionMaximum(
                workTypeId);
    }
}
#endif
