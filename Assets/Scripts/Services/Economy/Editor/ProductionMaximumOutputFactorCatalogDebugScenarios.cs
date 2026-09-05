#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ProductionMaximumOutputFactorCatalogDebugScenarios
{
    private const string GrandProjectEnvelopeArtifactPath =
        "Artifacts/QA/v27-grand-project-affected-recipe-envelope.csv";

    [MenuItem(
        "DungeonStory/V27/Production/Verify Idle Non-Producer Capacity Focused")]
    public static void RunIdleNonProducerCapacityFocused()
    {
        VerifyIdleNonProducerCapacityIsZero(
            new ProductionMaximumOutputFactorCatalog(
                Resources.LoadAll<BuildingSO>("SO/Building")));
        Debug.Log("V27_IDLE_NON_PRODUCER_CAPACITY_ZERO=PASS");
    }

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
        VerifyAffectedGrandProjectRecipeEnvelopes(
            buildings,
            recipes,
            catalog);

        VerifyFeedbenchCapacitySource(buildings, recipes, catalog);
        VerifySawmillCapacitySource(buildings, recipes, catalog);
        VerifyWorkOnlyFamilyCapacitySources(buildings, recipes, catalog);
        VerifyTailoringSpecialCapabilityCapacity(buildings, recipes, catalog);
        VerifyRuinedBatchClaimCapacity(buildings, recipes, catalog);
        VerifySyntheticMultiUnitRuinedPortfolioEnvelope();
        VerifyIdleNonProducerCapacityIsZero(catalog);

        ProductionRecipeSO silage = recipes.Single(value =>
            string.Equals(value.RecipeId, "recipe:silage", StringComparison.Ordinal));
        Require(catalog.ResolveMaximum(silage).Equals(ProductionOutputFactor.One),
            "Silage maximum support factor drifted from 1/1.");
        IReadOnlyList<ProductionAuthoredSupportProfileSnapshot> batchProfiles =
            catalog.CaptureBatchSupportProfiles(silage);
        Require(batchProfiles.Count == 2,
            $"Silage expected two authored fermenter profiles, got {batchProfiles.Count}.");
        Require(batchProfiles.All(value =>
                value.Kind == ProductionSupportKind.BatchProcessor
                && value.BatchCapacity == 1
                && value.MaximumLinkedInstancesPerWorkstation == 1
                && value.WorkSpeedFactor.Equals(ProductionOutputFactor.One)),
            "Silage batch profile did not freeze finite lane/work-speed authority.");
        Expect<InvalidOperationException>(() =>
            new ProductionMaximumOutputFactorCatalog(Array.Empty<BuildingSO>())
                .ResolveMaximum(silage));

        VerifyDeterministicNonUnitSupportEnvelope();

        Debug.Log("[ProductionMaximumOutputFactorCatalog] focused scenarios passed.");
    }

    private static void VerifyIdleNonProducerCapacityIsZero(
        ProductionMaximumOutputFactorCatalog maximumFactors)
    {
        ResourceItemDefinitionSO[] items = Resources
            .LoadAll<ResourceItemDefinitionSO>(ItemDefinitionSO.UnifiedResourcePath)
            .Where(value => value != null)
            .ToArray();
        ResourceEconomyContentCatalog economy = new(
            items,
            Array.Empty<ProductionRecipeSO>(),
            Array.Empty<CropDefinitionSO>(),
            Array.Empty<CraftMaterialDefinitionSO>());
        ProductionPreparedOutputComponentCodec componentCodec = new();
        IPhysicalItemMassQuery massQuery = new PhysicalItemMassQuery(
            EditorItemCatalogFactory.Create());
        ProductionOutputBufferCapacityProjector projector = new(
            economy,
            maximumFactors,
            componentCodec,
            massQuery,
            facility => facility.OutputBufferCycleCapacity,
            (facility, recipe) => false,
            clearanceProfiles: new RejectingClearanceProfileSource());
        ProductionFacilityCapacitySubject subject = new(
            (BuildingInstanceId)"building:qa:idle-non-producer",
            new Vector2Int(3, 5),
            "building-definition:qa:idle-non-producer",
            "workstation:qa:idle-non-producer",
            4,
            new ProductionFacilityWorkstationLaneCapacityProfile(
                ProductionWorkstationLanePolicy
                    .ManualWithDetachedBatchProcessors,
                1,
                0),
            ProductionFacilityProcessFluidCapacityProfile.Empty);

        ProductionOutputBufferCapacitySourceSnapshot first =
            projector.CapturePortfolioSource(subject);
        ProductionOutputBufferCapacitySourceSnapshot second =
            projector.CapturePortfolioSource(subject);
        Require(first.CycleCapacity == 4
            && first.MaximumBatchMassGrams == 0L
            && first.ProjectedPortfolioCapacityGrams == 0L
            && first.BatchMinimumCapacityGrams == 0L
            && first.RequiredMinimumCapacityGrams == 0L
            && string.IsNullOrEmpty(first.ClearanceProfileDigest)
            && string.IsNullOrEmpty(first.ClearanceGateDigest)
            && string.IsNullOrEmpty(first.ClearanceAuthorityDigest)
            && string.Equals(
                first.SourceDigest,
                second.SourceDigest,
                StringComparison.Ordinal),
            "Idle non-producer capacity did not remain deterministic zero authority.");
    }

    private sealed class RejectingClearanceProfileSource :
        IProductionOutputClearanceProfileSource
    {
        public string AuthorityDigest { get; } = new string('a', 64);

        public ProductionOutputClearanceProfileSnapshot Capture(
            ProductionFacilityCapacitySubject facility) =>
            throw new InvalidOperationException(
                "Idle non-producer capacity queried clearance authority.");
    }

    private static void VerifyAffectedGrandProjectRecipeEnvelopes(
        BuildingSO[] buildings,
        ProductionRecipeSO[] recipes,
        ProductionMaximumOutputFactorCatalog catalog)
    {
        GrandProjectRecipeEnvelopeExpectation[] expectations =
        {
            new("recipe:advanced-medicine", 23L, 20L, 140L, 280L),
            new("recipe:alcohol", 23L, 20L, 1_000L, 1_500L),
            new("recipe:anesthetic", 23L, 20L, 120L, 240L),
            new("recipe:antidote", 23L, 20L, 120L, 240L),
            new("recipe:antiseptic", 23L, 20L, 360L, 540L),
            new("recipe:blood-stimulant", 23L, 20L, 100L, 200L),
            new("recipe:candle", 23L, 20L, 400L, 600L),
            new("recipe:dreamleaf-analgesic", 23L, 20L, 120L, 240L),
            new("recipe:fang-poison", 23L, 20L, 120L, 240L),
            new("recipe:hallucinogenic-distillate", 23L, 20L, 150L, 300L),
            new("recipe:herbal-poultice", 23L, 20L, 400L, 600L),
            new("recipe:mana-awakener", 23L, 20L, 100L, 200L),
            new("recipe:resin-balm", 23L, 20L, 150L, 300L),
            new("recipe:ritual-reagent", 23L, 20L, 250L, 500L),
            new("recipe:rot-toxin", 23L, 20L, 200L, 400L),
            new("recipe:rune-leather", 23L, 20L, 400L, 800L),
            new("recipe:soap", 23L, 20L, 500L, 750L),
            new("recipe:solvent", 23L, 20L, 300L, 600L),
            new("recipe:standard-medicine", 23L, 20L, 320L, 480L),
            new("recipe:vitality-tonic", 23L, 20L, 400L, 600L),
            new("source:quarry", 5L, 4L, 10_050L, 15_300L)
        };
        GrandProjectRecipeEnvelopeExpectation[] orderedExpectations =
            expectations
                .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
                .ToArray();
        Require(orderedExpectations.Length == 21
            && orderedExpectations.Select(value => value.RecipeId)
                .Distinct(StringComparer.Ordinal).Count() == 21,
            "Grand Project recipe envelope fixture is not an exact 21-row set.");

        ProductionRecipeSO[] affected = recipes
            .Where(value => !catalog.ResolveMaximum(value)
                .Equals(ProductionOutputFactor.One))
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        Require(affected.Select(value => value.RecipeId).SequenceEqual(
                orderedExpectations.Select(value => value.RecipeId),
                StringComparer.Ordinal),
            "Grand Project affected recipe identity set drifted.");

        IPhysicalItemMassQuery massQuery = new PhysicalItemMassQuery(
            EditorItemCatalogFactory.Create());
        GrandProjectRecipeEnvelopeRow[] rows = affected
            .Select(recipe => CaptureGrandProjectRecipeEnvelope(
                recipe,
                buildings,
                catalog,
                massQuery))
            .ToArray();
        for (int index = 0; index < rows.Length; index++)
        {
            GrandProjectRecipeEnvelopeRow actual = rows[index];
            GrandProjectRecipeEnvelopeExpectation expected =
                orderedExpectations[index];
            Require(string.Equals(
                    actual.RecipeId,
                    expected.RecipeId,
                    StringComparison.Ordinal)
                && actual.FactorNumerator == expected.FactorNumerator
                && actual.FactorDenominator == expected.FactorDenominator
                && actual.BaseMaximumMassGrams
                    == expected.BaseMaximumMassGrams
                && actual.ScaledMaximumMassGrams
                    == expected.ScaledMaximumMassGrams,
                $"Grand Project maximum envelope drifted for '{expected.RecipeId}'.");
        }

        VerifyFacilityGrandProjectEnvelope(
            rows,
            "workstation:distillery",
            "recipe:alcohol",
            1_500L,
            6_000L);
        VerifyFacilityGrandProjectEnvelope(
            rows,
            "workstation:apothecary",
            "recipe:soap",
            750L,
            3_000L);
        VerifyFacilityGrandProjectEnvelope(
            rows,
            "workstation:alchemy",
            "recipe:rune-leather",
            800L,
            3_200L);
        VerifyFacilityGrandProjectEnvelope(
            rows,
            "workstation:quarry",
            "source:quarry",
            15_300L,
            61_200L);

        V27BalanceArtifactWriter.WriteIfDifferent(
            GrandProjectEnvelopeArtifactPath,
            stream => WriteGrandProjectRecipeEnvelopeCsv(stream, rows));
        string artifactHash = V27BalanceArtifactWriter.ComputeSha256(
            GrandProjectEnvelopeArtifactPath);
        Require(IsLowercaseSha256(artifactHash),
            "Grand Project recipe envelope artifact hash is invalid.");
    }

    private static GrandProjectRecipeEnvelopeRow
        CaptureGrandProjectRecipeEnvelope(
            ProductionRecipeSO recipe,
            BuildingSO[] buildings,
            ProductionMaximumOutputFactorCatalog catalog,
            IPhysicalItemMassQuery massQuery)
    {
        ProductionOutputFactor factor = catalog.ResolveMaximum(recipe);
        BuildingSO[] facilities = buildings
            .Where(value => value != null
                && string.Equals(
                    value.GetProductionWorkstationAbility()?.WorkstationTag,
                    recipe.WorkstationTag,
                    StringComparison.Ordinal))
            .OrderBy(value => value.ContentDefinitionId, StringComparer.Ordinal)
            .ToArray();
        Require(facilities.Length == 1,
            $"Grand Project recipe '{recipe.RecipeId}' must resolve one facility.");
        BuildingProductionBufferAbility buffer = facilities[0]
            .GetProductionBufferAbility();
        Require(buffer != null
            && buffer.physicalOutputBufferCycleCapacity == 4,
            $"Grand Project recipe '{recipe.RecipeId}' has no four-cycle buffer authority.");
        string facilityDefinitionId = facilities[0].ContentDefinitionId.Length > 0
            ? facilities[0].ContentDefinitionId
            : "building:" + facilities[0].id.ToString(
                CultureInfo.InvariantCulture);

        ProductionOutputDefinition[] outputs = recipe
            .CaptureCanonicalOutputs()
            .Where(value => value.Probability > 0f
                && ProductionOutputRoleRules.IsPhysical(value.Role))
            .OrderBy(value => value.OutputLineId, StringComparer.Ordinal)
            .ToArray();
        Require(outputs.Length > 0,
            $"Grand Project recipe '{recipe.RecipeId}' has no physical output.");
        long baseMass = 0L;
        long scaledMass = 0L;
        List<string> outputProofs = new(outputs.Length);
        foreach (ProductionOutputDefinition output in outputs)
        {
            long unitMass = massQuery.GetDefinitionUnitMass(
                (ItemDefinitionId)output.ItemId).Value;
            int maximumQuantity = factor.CeilQuantity(output.Amount);
            baseMass = checked(baseMass + checked(unitMass * output.Amount));
            scaledMass = checked(
                scaledMass + checked(unitMass * maximumQuantity));
            outputProofs.Add(
                output.OutputLineId + "|" + output.ItemId + "|"
                + output.Amount.ToString(CultureInfo.InvariantCulture) + "|"
                + maximumQuantity.ToString(CultureInfo.InvariantCulture) + "|"
                + unitMass.ToString(CultureInfo.InvariantCulture));
        }

        CanonicalSemanticDigestBuilder digest = new();
        digest.Append("v27-grand-project-affected-recipe-envelope@1");
        digest.Append(recipe.RecipeId);
        digest.Append(ProductionRecipeSemanticDigest.Capture(recipe));
        digest.Append(catalog.CaptureRecipeSourceDigest(recipe));
        digest.Append(massQuery.AuthorityRevision);
        digest.Append(facilityDefinitionId);
        digest.Append(recipe.WorkstationTag);
        digest.Append(factor.Numerator);
        digest.Append(factor.Denominator);
        digest.Append(baseMass);
        digest.Append(scaledMass);
        digest.Append(outputProofs.Count);
        foreach (string proof in outputProofs)
            digest.Append(proof);

        return new GrandProjectRecipeEnvelopeRow(
            recipe.RecipeId,
            facilityDefinitionId,
            recipe.WorkstationTag,
            factor.Numerator,
            factor.Denominator,
            baseMass,
            scaledMass,
            buffer.physicalOutputBufferCycleCapacity,
            checked(scaledMass * buffer.physicalOutputBufferCycleCapacity),
            string.Join(";", outputProofs),
            digest.ComputeSha256());
    }

    private static void VerifyFacilityGrandProjectEnvelope(
        IReadOnlyList<GrandProjectRecipeEnvelopeRow> rows,
        string workstationTag,
        string expectedWinnerRecipeId,
        long expectedMaximumBatchMassGrams,
        long expectedCapacityGrams)
    {
        GrandProjectRecipeEnvelopeRow winner = rows
            .Where(value => string.Equals(
                value.WorkstationTag,
                workstationTag,
                StringComparison.Ordinal))
            .OrderByDescending(value => value.ScaledMaximumMassGrams)
            .ThenBy(value => value.RecipeId, StringComparer.Ordinal)
            .First();
        Require(string.Equals(
                winner.RecipeId,
                expectedWinnerRecipeId,
                StringComparison.Ordinal)
            && winner.ScaledMaximumMassGrams
                == expectedMaximumBatchMassGrams
            && winner.PortfolioCapacityGrams == expectedCapacityGrams,
            $"Grand Project facility envelope drifted for '{workstationTag}'.");
    }

    private static void WriteGrandProjectRecipeEnvelopeCsv(
        Stream stream,
        IReadOnlyList<GrandProjectRecipeEnvelopeRow> rows)
    {
        using StreamWriter writer = new(
            stream,
            new UTF8Encoding(false, true),
            16_384,
            leaveOpen: true);
        WriteCsvRow(writer, new[]
        {
            "schemaVersion",
            "recipeId",
            "facilityDefinitionId",
            "workstationTag",
            "factorNumerator",
            "factorDenominator",
            "baseMaximumMassGrams",
            "scaledMaximumMassGrams",
            "cycleCapacity",
            "portfolioCapacityGrams",
            "outputProofs",
            "sourceDigest"
        });
        foreach (GrandProjectRecipeEnvelopeRow row in rows
                     .OrderBy(value => value.RecipeId, StringComparer.Ordinal))
        {
            WriteCsvRow(writer, new[]
            {
                "v27-grand-project-affected-recipe-envelope@1",
                row.RecipeId,
                row.FacilityDefinitionId,
                row.WorkstationTag,
                row.FactorNumerator.ToString(CultureInfo.InvariantCulture),
                row.FactorDenominator.ToString(CultureInfo.InvariantCulture),
                row.BaseMaximumMassGrams.ToString(CultureInfo.InvariantCulture),
                row.ScaledMaximumMassGrams.ToString(CultureInfo.InvariantCulture),
                row.CycleCapacity.ToString(CultureInfo.InvariantCulture),
                row.PortfolioCapacityGrams.ToString(CultureInfo.InvariantCulture),
                row.OutputProofs,
                row.SourceDigest
            });
        }
        writer.Flush();
    }

    private static void WriteCsvRow(StreamWriter writer, string[] values)
    {
        for (int index = 0; index < values.Length; index++)
        {
            if (index > 0)
                writer.Write(',');
            V27BalanceCsvSerializer.WriteEscapedField(
                writer,
                (values[index] ?? string.Empty).AsSpan());
        }
        writer.Write('\r');
        writer.Write('\n');
    }

    private readonly struct GrandProjectRecipeEnvelopeExpectation
    {
        internal GrandProjectRecipeEnvelopeExpectation(
            string recipeId,
            long factorNumerator,
            long factorDenominator,
            long baseMaximumMassGrams,
            long scaledMaximumMassGrams)
        {
            RecipeId = recipeId;
            FactorNumerator = factorNumerator;
            FactorDenominator = factorDenominator;
            BaseMaximumMassGrams = baseMaximumMassGrams;
            ScaledMaximumMassGrams = scaledMaximumMassGrams;
        }

        internal string RecipeId { get; }
        internal long FactorNumerator { get; }
        internal long FactorDenominator { get; }
        internal long BaseMaximumMassGrams { get; }
        internal long ScaledMaximumMassGrams { get; }
    }

    private readonly struct GrandProjectRecipeEnvelopeRow
    {
        internal GrandProjectRecipeEnvelopeRow(
            string recipeId,
            string facilityDefinitionId,
            string workstationTag,
            long factorNumerator,
            long factorDenominator,
            long baseMaximumMassGrams,
            long scaledMaximumMassGrams,
            int cycleCapacity,
            long portfolioCapacityGrams,
            string outputProofs,
            string sourceDigest)
        {
            RecipeId = recipeId;
            FacilityDefinitionId = facilityDefinitionId;
            WorkstationTag = workstationTag;
            FactorNumerator = factorNumerator;
            FactorDenominator = factorDenominator;
            BaseMaximumMassGrams = baseMaximumMassGrams;
            ScaledMaximumMassGrams = scaledMaximumMassGrams;
            CycleCapacity = cycleCapacity;
            PortfolioCapacityGrams = portfolioCapacityGrams;
            OutputProofs = outputProofs;
            SourceDigest = sourceDigest;
        }

        internal string RecipeId { get; }
        internal string FacilityDefinitionId { get; }
        internal string WorkstationTag { get; }
        internal long FactorNumerator { get; }
        internal long FactorDenominator { get; }
        internal long BaseMaximumMassGrams { get; }
        internal long ScaledMaximumMassGrams { get; }
        internal int CycleCapacity { get; }
        internal long PortfolioCapacityGrams { get; }
        internal string OutputProofs { get; }
        internal string SourceDigest { get; }
    }

    private static void VerifyDeterministicNonUnitSupportEnvelope()
    {
        BuildingSO combined = Support(
            "support:qa-combined",
            new[] { "support:qa-heat", "support:qa-air" },
            ProductionSupportKind.Passive,
            1.4f);
        BuildingSO heat = Support(
            "support:qa-heat",
            new[] { "support:qa-heat" },
            ProductionSupportKind.Passive,
            1.25f);
        BuildingSO air = Support(
            "support:qa-air",
            new[] { "support:qa-air" },
            ProductionSupportKind.Passive,
            1.25f);
        BuildingSO batch = Support(
            "support:qa-batch",
            new[] { "support:qa-batch" },
            ProductionSupportKind.BatchProcessor,
            2f);
        ProductionRecipeSO recipe = ScriptableObject
            .CreateInstance<ProductionRecipeSO>();
        try
        {
            recipe.Configure(
                "recipe:qa-support-envelope",
                "QA support envelope",
                string.Empty,
                "qa",
                "work:craft",
                string.Empty,
                1f,
                Array.Empty<ItemAmountDefinition>(),
                Array.Empty<ProductionOutputDefinition>());
            recipe.ConfigureWorkshop(
                "workstation:qa",
                new[] { "support:qa-heat", "support:qa-air" },
                ProductionProcessKind.WorkOnly,
                "support:qa-batch");

            BuildingSO[] ordered = { combined, heat, air, batch };
            ProductionMaximumOutputFactorCatalog first = new(ordered);
            ProductionMaximumOutputFactorCatalog shuffled = new(
                ordered.Reverse());
            // Stable ordering can let the single-tag support claim heat first,
            // then the combined support claim air. The combined support is
            // still multiplied exactly once, yielding 5/4 * 7/5 = 7/4.
            ProductionOutputFactor expected = new(7L, 4L);
            Require(first.ResolveMaximum(recipe).Equals(expected)
                && shuffled.ResolveMaximum(recipe).Equals(expected),
                "Support maximum did not choose the exact best distinct-provider envelope.");
            IReadOnlyList<ProductionAuthoredSupportAssignmentSnapshot>
                assignments = first.CaptureFeasibleAssignments(recipe);
            Require(assignments.Count == 4
                && assignments.All(value => value.Supports
                    .Select(support => support.SupportId)
                    .Distinct(StringComparer.Ordinal)
                    .Count() == value.Supports.Count),
                "Overlapping support tags were not enumerated as four unique-provider assignments.");
            Require(string.Equals(
                    first.SourceDigest,
                    shuffled.SourceDigest,
                    StringComparison.Ordinal)
                && string.Equals(
                    first.CaptureRecipeSourceDigest(recipe),
                    shuffled.CaptureRecipeSourceDigest(recipe),
                    StringComparison.Ordinal),
                "Support maximum changed after authored provider order shuffle.");

            batch.GetProductionSupportAbility().kind = ProductionSupportKind.Passive;
            ProductionMaximumOutputFactorCatalog invalidBatch = new(ordered);
            ExpectMessage<InvalidOperationException>(
                () => invalidBatch.ResolveMaximum(recipe),
                "no authored batch support provider");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(recipe);
            UnityEngine.Object.DestroyImmediate(combined);
            UnityEngine.Object.DestroyImmediate(heat);
            UnityEngine.Object.DestroyImmediate(air);
            UnityEngine.Object.DestroyImmediate(batch);
        }
    }

    private static void VerifySyntheticMultiUnitRuinedPortfolioEnvelope()
    {
        ResourceItemDefinitionSO[] items = Resources
            .LoadAll<ResourceItemDefinitionSO>(ItemDefinitionSO.UnifiedResourcePath)
            .Where(value => value != null)
            .ToArray();
        IPhysicalItemMassQuery massQuery = new PhysicalItemMassQuery(
            EditorItemCatalogFactory.Create());
        ResourceItemDefinitionSO sixHundredGramItem = items
            .OrderBy(value => value.ItemId, StringComparer.Ordinal)
            .First(value => massQuery.GetDefinitionUnitMass(
                (ItemDefinitionId)value.ItemId).Value == 600L);
        ProductionRecipeSO recipe = ScriptableObject
            .CreateInstance<ProductionRecipeSO>();
        BuildingSO support = ScriptableObject.CreateInstance<BuildingSO>();
        try
        {
            BuildingAbilityCollection supportAbilities = new();
            supportAbilities.Add(new BuildingProductionSupportAbility
            {
                supportId = "support:qa-ruin-fluid-fuel",
                featureTags = new[] { "support:qa-ruin-fluid-fuel" },
                compatibleWorkstationTags = new[]
                {
                    "workstation:qa-multi-unit-ruin"
                },
                 kind = ProductionSupportKind.Passive,
                 maximumLinkedInstancesPerWorkstation = 1,
                cleanWaterPerCycle = 0.6f,
                wastewaterPerCycle = 0.3f,
                wastewaterComposition =
                    ProcessWastewaterComposition.IndustrialEffluent,
                requiresFuel = true,
                fuelItemId = sixHundredGramItem.ItemId,
                fuelPerCycle = 1,
                outputMultiplier = 1f
            });
            support.ReplaceAbilities(supportAbilities);
            recipe.Configure(
                "recipe:qa-multi-unit-ruin-envelope",
                "QA multi-unit ruin envelope",
                string.Empty,
                "qa",
                BuiltInWorkTypeIds.Craft.Value,
                string.Empty,
                1f,
                new[]
                {
                    new ItemAmountDefinition(
                        sixHundredGramItem.ItemId,
                        4)
                },
                new[]
                {
                    new ProductionOutputDefinition(
                        "output:main",
                        ProductionOutputRole.Main,
                        sixHundredGramItem.ItemId,
                        1)
                });
            recipe.ConfigureWorkshop(
                "workstation:qa-multi-unit-ruin",
                new[] { "support:qa-ruin-fluid-fuel" },
                ProductionProcessKind.PassiveBatch,
                prepareWork: 1f,
                finishWork: 1f,
                processGameHours: 12f,
                cleanWater: 1.2f,
                wastewater: 0.6f,
                failedBatchItemId: sixHundredGramItem.ItemId,
                wastewaterKind:
                    ProcessWastewaterComposition.FermentationEffluent);
            recipe.ConfigureProficiency(
                BuiltInCharacterProficiencyIds.Crafting);
            recipe.ConfigureProcessClass(
                ProductionProcessClass.CookingSimpleMixing);

            ResourceEconomyContentCatalog economy = new(
                items,
                new[] { recipe },
                Array.Empty<CropDefinitionSO>(),
                Array.Empty<CraftMaterialDefinitionSO>());
            ProductionPreparedOutputComponentCodec componentCodec = new();
            ProductionMaximumOutputFactorCatalog factors = new(
                new[] { support });
            ProductionOutputMaximumMassRegistry maximumMass = new(
                new IProductionOutputMaximumMassCapability[]
                {
                    new StandardDefinitionProductionOutputCapability(
                        economy,
                        componentCodec)
                },
                massQuery);
            ProductionOutputBufferCapacityProjector projector = new(
                economy,
                factors,
                componentCodec,
                massQuery,
                facility => facility.OutputBufferCycleCapacity,
                (facility, candidate) => string.Equals(
                    facility.WorkstationTag,
                    candidate.WorkstationTag,
                    StringComparison.Ordinal),
                maximumMass.CaptureAutomatic,
                maximumMass.CaptureDeclared);
            ProductionFacilityCapacitySubject subject = new(
                (BuildingInstanceId)"building:qa:multi-unit-ruin",
                new Vector2Int(7, 9),
                "building-definition:qa-multi-unit-ruin",
                recipe.WorkstationTag,
                4,
                new ProductionFacilityWorkstationLaneCapacityProfile(
                    ProductionWorkstationLanePolicy
                        .ManualWithDetachedBatchProcessors,
                    1,
                    0),
                new ProductionFacilityProcessFluidCapacityProfile(
                    new[] { BuiltInWorkTypeIds.Craft.Value },
                    0.3f,
                    0.1f));
            ProductionOutputBufferCapacitySourceSnapshot portfolio =
                projector.CapturePortfolioSource(subject);
            Require(portfolio.MaximumBatchMassGrams == 3_000L
                && portfolio.ProjectedPortfolioCapacityGrams == 12_000L
                && portfolio.RequiredMinimumCapacityGrams == 12_000L,
                "Synthetic multi-unit ruined WIP did not reserve recipe, facility, support-fluid and support-fuel mass together.");

            ProductionBillRecord oversized = ProductionBillRecord.Create(
                (ProductionBillId)"production-bill:qa-oversized-ruin",
                recipe.RecipeId,
                subject.FacilityId,
                ProductionOrderMode.RepeatCount,
                1,
                0,
                ProductionBatchStage.Processing,
                "production-input:production-bill:qa-oversized-ruin");
            oversized.SetOutputDestination(
                ProductionOutputDestinationId.FromFacility(
                    subject.FacilityId).Value);
            oversized.SetMaterialsConsumed(true);
            oversized.SetWipInput(new ProductionWipInputReceipt(
                "production-wip:production-bill:qa-oversized-ruin:00000001",
                6,
                3_600L));
            oversized.SetProcessFluidConsumed(true);
            oversized.SetProcessFluid(new ProductionProcessFluidReceipt(
                600L,
                300L,
                wastewaterComponents: new[]
                {
                    new ProcessWastewaterComponent(
                        ProcessWastewaterComposition.FermentationEffluent,
                        ProcessWastewaterSourceKind.Recipe,
                        recipe.RecipeId,
                        0.6f)
                }));
            ProductionOutputCapabilityDescriptor wasteDescriptor = maximumMass
                .CaptureAutomatic(
                    ProductionRuinedBatchDispositionPlan
                        .RecoverableWasteOutputLineId,
                    sixHundredGramItem.ItemId,
                    1)
                .Descriptor;
            ProductionRuinedOutputCapacityClaim oversizedClaim = projector
                .CaptureRuinedClaim(oversized, recipe, wasteDescriptor);
            ExpectMessage<InvalidOperationException>(
                () => projector.CaptureSource(subject, oversizedClaim),
                "ruined-wip-exceeds-authored-maximum");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(recipe);
            UnityEngine.Object.DestroyImmediate(support);
        }
    }

    private static void VerifyTailoringSpecialCapabilityCapacity(
        BuildingSO[] buildings,
        ProductionRecipeSO[] recipes,
        ProductionMaximumOutputFactorCatalog maximumFactors)
    {
        const string workstationTag = "workstation:v22:tailoring";
        ProductionRecipeSO[] tailoringRecipes = recipes
            .Where(value => string.Equals(
                value.WorkstationTag,
                workstationTag,
                StringComparison.Ordinal))
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        Require(tailoringRecipes.Length == 58,
            $"Expected 58 tailoring recipes, got {tailoringRecipes.Length}.");

        ResourceItemDefinitionSO[] items = Resources
            .LoadAll<ResourceItemDefinitionSO>(ItemDefinitionSO.UnifiedResourcePath)
            .Where(value => value != null)
            .ToArray();
        ResourceEconomyContentCatalog economy = new(
            items,
            tailoringRecipes,
            Array.Empty<CropDefinitionSO>(),
            Array.Empty<CraftMaterialDefinitionSO>());
        ProductionPreparedOutputComponentCodec componentCodec = new();
        IPhysicalItemMassQuery massQuery = new PhysicalItemMassQuery(
            EditorItemCatalogFactory.Create());
        IApparelDefinitionCatalog apparel = new ResourceApparelDefinitionCatalog(
            CharacterAiEditorTestDependencies.ContentDefinitions);
        ProductionOutputMaximumMassRegistry maximumMass = new(
            new IProductionOutputMaximumMassCapability[]
            {
                new StandardDefinitionProductionOutputCapability(
                    economy,
                    componentCodec),
                new EnvironmentalWorkwearProductionOutputMaximumMassCapability(
                    apparel)
            },
            massQuery);
        ProductionOutputBufferCapacityProjector projector = new(
            economy,
            maximumFactors,
            componentCodec,
            massQuery,
            facility => facility.OutputBufferCycleCapacity,
            (facility, recipe) => string.Equals(
                facility.WorkstationTag,
                recipe.WorkstationTag,
                StringComparison.Ordinal),
            maximumMass.CaptureAutomatic);

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
            "Tailoring workstation has no exact four-cycle output authority.");
        ProductionOutputBufferCapacitySourceSnapshot source =
            projector.CapturePortfolioSource(
                Facility(definition, new Vector2Int(31, 17), 4));
        Require(source.MaximumBatchMassGrams == 1_380L
            && source.ProjectedPortfolioCapacityGrams == 5_520L
            && source.BatchMinimumCapacityGrams == 0L
            && source.RequiredMinimumCapacityGrams == 5_520L,
            "Tailoring special-capability capacity was not exact 1,380g/cycle and 5,520g/four cycles.");
    }

    private static void VerifyRuinedBatchClaimCapacity(
        BuildingSO[] buildings,
        ProductionRecipeSO[] recipes,
        ProductionMaximumOutputFactorCatalog maximumFactors)
    {
        ProductionRecipeSO recipe = recipes.Single(value => string.Equals(
            value.RecipeId,
            "recipe:silage",
            StringComparison.Ordinal));
        ResourceItemDefinitionSO[] items = Resources
            .LoadAll<ResourceItemDefinitionSO>(ItemDefinitionSO.UnifiedResourcePath)
            .Where(value => value != null)
            .ToArray();
        ResourceEconomyContentCatalog economy = new(
            items,
            recipes,
            Array.Empty<CropDefinitionSO>(),
            Array.Empty<CraftMaterialDefinitionSO>());
        ProductionPreparedOutputComponentCodec componentCodec = new();
        IPhysicalItemMassQuery massQuery = new PhysicalItemMassQuery(
            EditorItemCatalogFactory.Create());
        ProductionOutputMaximumMassRegistry maximumMass = new(
            new IProductionOutputMaximumMassCapability[]
            {
                new StandardDefinitionProductionOutputCapability(
                    economy,
                    componentCodec)
            },
            massQuery);
        ProductionOutputBufferCapacityProjector projector = new(
            economy,
            maximumFactors,
            componentCodec,
            massQuery,
            facility => facility.OutputBufferCycleCapacity,
            (facility, candidate) => string.Equals(
                facility.WorkstationTag,
                candidate.WorkstationTag,
                StringComparison.Ordinal),
            maximumMass.CaptureAutomatic,
            maximumMass.CaptureDeclared);
        int authoredInputQuantity = recipe.Inputs.Sum(value => value.Amount);
        long authoredWipMassGrams = recipe.Inputs.Aggregate(
            0L,
            (sum, value) => checked(sum
                + massQuery.GetDefinitionUnitMass(
                    (ItemDefinitionId)value.ItemId).Value * value.Amount));
        long authoredCleanWaterMassGrams =
            ProductionFluidMassRules.ToMassGrams(recipe.CleanWaterPerCycle);
        long authoredWastewaterMassGrams =
            ProductionFluidMassRules.ToMassGrams(recipe.WastewaterPerCycle);

        ProductionBillRecord record = ProductionBillRecord.Create(
            (ProductionBillId)"production-bill:ruined-claim",
            recipe.RecipeId,
            new BuildingInstanceId("building:qa:ruined-claim"),
            ProductionOrderMode.RepeatCount,
            remainingCycles: 1,
            targetStock: 0,
            ProductionBatchStage.Processing,
            "production-input:production-bill:ruined-claim");
        record.SetOutputDestination(
            "production-output:building:qa:ruined-claim");
        record.SetMaterialsConsumed(true);
        record.SetWipInput(new ProductionWipInputReceipt(
            "production-wip:production-bill:ruined-claim:00000001",
            authoredInputQuantity,
            authoredWipMassGrams));
        record.SetProcessFluidConsumed(true);
        record.SetProcessFluid(new ProductionProcessFluidReceipt(
            authoredCleanWaterMassGrams,
            authoredWastewaterMassGrams));

        ProductionOutputCapabilityDescriptor wasteDescriptor = maximumMass
            .CaptureAutomatic(
                ProductionRuinedBatchDispositionPlan
                    .RecoverableWasteOutputLineId,
                recipe.SpoilageItemId,
                1)
            .Descriptor;
        ProductionRuinedOutputCapacityClaim liveClaim = projector
            .CaptureRuinedClaim(record, recipe, wasteDescriptor);
        BuildingSO workstationDefinition = buildings.Single(value =>
            value != null
            && string.Equals(
                value.GetProductionWorkstationAbility()?.WorkstationTag,
                recipe.WorkstationTag,
                StringComparison.Ordinal));
        ProductionFacilityCapacitySubject subject = new(
            record.buildingInstanceId,
            new Vector2Int(41, 17),
            ProductionFacilityDefinitionIdentity.Resolve(workstationDefinition),
            recipe.WorkstationTag,
            4,
            ProductionFacilityCapacitySubjectAdapter
                .CaptureWorkstationLaneProfile(workstationDefinition));
        ProductionOutputBufferCapacitySourceSnapshot liveSource = projector
            .CaptureSource(subject, liveClaim);
        Require(
            liveClaim.Disposition.RecoverableWasteQuantity == 1
            && liveClaim.Disposition.SpoilageUnitMassGrams == 600L
            && liveClaim.Disposition.RecoverableWasteMassGrams == 600L
            && liveClaim.Disposition.ProcessWastewaterMassGrams
                == authoredWastewaterMassGrams
            && liveClaim.MaximumMassProof.MaximumBatchMassGrams == 600L
            && liveSource.BatchMinimumCapacityGrams == 2_400L
            && liveSource.RequiredMinimumCapacityGrams == 4_200L,
            "Authored silage ruin claim did not remain within the exact portfolio envelope.");

        ProductionBillSaveData saved = new()
        {
            billId = record.billId.Value,
            recipeId = record.recipeId,
            buildingInstanceId = record.buildingInstanceId.Value,
            cycleSequence = record.cycleSequence,
            wipInputCommitId = record.wipInputCommitId,
            wipInputQuantity = record.wipInputQuantity,
            wipInputMassGrams = record.wipInputMassGrams,
            processFluidConsumed = record.processFluidConsumed,
            processCleanWaterMassGrams = record.processCleanWaterMassGrams,
            processWastewaterMassGrams = record.processWastewaterMassGrams,
            processWastewaterComponents = record.processWastewaterComponents
                .Select(value => value.Clone())
                .ToList(),
            processManualWaterTransfers = record.processManualWaterTransfers
                .Select(value => value.Clone())
                .ToList()
        };
        ProductionRuinedOutputCapacityClaim detachedClaim = projector
            .CaptureRuinedClaim(saved, wasteDescriptor);
        ProductionOutputBufferCapacitySourceSnapshot detachedSource = projector
            .CaptureSource(subject, detachedClaim);
        Require(
            string.Equals(
                liveClaim.SourceDigest,
                detachedClaim.SourceDigest,
                StringComparison.Ordinal)
            && string.Equals(
                liveClaim.MaximumMassProof.SourceDigest,
                detachedClaim.MaximumMassProof.SourceDigest,
                StringComparison.Ordinal)
            && string.Equals(
                liveSource.SourceDigest,
                detachedSource.SourceDigest,
                StringComparison.Ordinal),
            "Live and detached ruined-output capacity claims diverged.");

        saved.outputDestinationId =
            ProductionOutputDestinationId.FromFacility(subject.FacilityId).Value;
        saved.preparedOutput = new ProductionPreparedOutputBatchSaveData
        {
            phase = ProductionPreparedOutputPhase.ResolvedWaitingForOutputSpace,
            billId = saved.billId,
            cycleSequence = saved.cycleSequence,
            recipeId = saved.recipeId,
            destinationId = saved.outputDestinationId,
            recipeDefinitionDigest =
                ProductionRecipeSemanticDigest.Capture(recipe),
            migrationProfileDigest = ProductionPreparedOutputMigrationScope
                .CaptureProfileDigest(recipe),
            capacitySourceDigest = detachedSource.SourceDigest,
            maximumMassProofDigest = detachedClaim.MaximumMassProof.SourceDigest,
            maximumBatchMassGrams = detachedClaim.MaximumMassProof
                .MaximumBatchMassGrams,
            capacityClaimDigest = detachedClaim.SourceDigest,
            outputBufferCycleCapacity = detachedSource.CycleCapacity,
            projectedPortfolioCapacityGrams = detachedSource
                .ProjectedPortfolioCapacityGrams,
            requiredMinimumCapacityGrams = detachedSource
                .RequiredMinimumCapacityGrams,
            outcomeFingerprint = new string('a', 64),
            batchCommitId = "production-output-batch:ruined-capacity-detached",
            totalPhysicalMassGrams = liveClaim.Disposition
                .RecoverableWasteMassGrams,
            totalDeclaredLossMassGrams = liveClaim.Disposition
                .DeclaredLossMassGrams,
            lines = new System.Collections.Generic.List<
                ProductionPreparedOutputLineSaveData>
            {
                new()
                {
                    outputLineId = wasteDescriptor.OutputLineId,
                    role = ProductionOutputRole.RecoverableWaste,
                    itemId = wasteDescriptor.ItemId,
                    outputCapabilityId = wasteDescriptor.CapabilityId,
                    outputCapabilityVersion = wasteDescriptor.CapabilityVersion,
                    outputComponentCodecId = wasteDescriptor.ComponentCodecId,
                    outputComponentCodecVersion =
                        wasteDescriptor.ComponentCodecVersion,
                    outputCapabilityFingerprint = wasteDescriptor.Fingerprint,
                    quantity = liveClaim.Disposition.RecoverableWasteQuantity,
                    exactMassGrams = liveClaim.Disposition
                        .RecoverableWasteMassGrams
                }
            }
        };
        BuildingSO definition = buildings.Single(value => value != null
            && string.Equals(
                ProductionFacilityDefinitionIdentity.Resolve(value),
                subject.DefinitionId,
                StringComparison.Ordinal));
        ModularFacilityWorldSaveData world = new()
        {
            buildings = new System.Collections.Generic.List<
                ModularFacilityBuildingSaveData>
            {
                new()
                {
                    persistentInstanceId = subject.FacilityId.Value,
                    buildingId = definition.id,
                    centerX = subject.Position.x,
                    centerY = subject.Position.y
                }
            }
        };
        DungeonProductionBillSaveData production = new()
        {
            bills = new System.Collections.Generic.List<ProductionBillSaveData>
            {
                saved
            }
        };
        ProductionOutputCapacityDurableProjection durable =
            ProductionOutputDestinationDurableSaveProjector
                .ProjectCapacityRoutingFromSave(
                    subject.FacilityId,
                    world,
                    production,
                    EmptyGenericTerminalPayload(),
                    new DungeonPhysicalItemSaveData(),
                    new DungeonCharacterWorldSaveData(),
                    new ProductionPreparedOutputRoutingSaveData(),
                    Array.Empty<FacilityOutputExactRouteOutboxSaveData>(),
                    new FixedBuildingDefinitionLookup(definition),
                    projector,
                    massQuery);
        Require(
            durable.Profile != null
            && durable.Profile.MaxMassGrams ==
                detachedSource.RequiredMinimumCapacityGrams,
            "Detached durable projector did not retain the ruined-output proof-sized capacity.");

        string validClaimDigest = saved.preparedOutput.capacityClaimDigest;
        saved.preparedOutput.capacityClaimDigest = new string('e', 64);
        ExpectMessage<InvalidOperationException>(
            () => ProductionOutputDestinationDurableSaveProjector
                .ProjectCapacityRoutingFromSave(
                    subject.FacilityId,
                    world,
                    production,
                    EmptyGenericTerminalPayload(),
                    new DungeonPhysicalItemSaveData(),
                    new DungeonCharacterWorldSaveData(),
                    new ProductionPreparedOutputRoutingSaveData(),
                    Array.Empty<FacilityOutputExactRouteOutboxSaveData>(),
                    new FixedBuildingDefinitionLookup(definition),
                    projector,
                    massQuery),
            "stale ruined-output capacity proof");
        saved.preparedOutput.capacityClaimDigest = validClaimDigest;

        ProductionPreparedOutputRoutingBatchSaveData terminalBatch = new()
        {
            batchCommitId = saved.preparedOutput.batchCommitId,
            ownerBillId = saved.billId,
            ownerRecipeId = saved.recipeId,
            ownerFacilityId = subject.FacilityId.Value,
            cycleSequence = saved.cycleSequence,
            destinationId = saved.outputDestinationId,
            capacitySourceDigest = detachedSource.SourceDigest,
            outputBufferCycleCapacity = detachedSource.CycleCapacity,
            projectedPortfolioCapacityGrams = detachedSource
                .ProjectedPortfolioCapacityGrams,
            requiredMinimumCapacityGrams = detachedSource
                .RequiredMinimumCapacityGrams,
            maximumMassProofDigest = detachedClaim.MaximumMassProof.SourceDigest,
            maximumBatchMassGrams = detachedClaim.MaximumMassProof
                .MaximumBatchMassGrams,
            capacityClaimDigest = detachedClaim.SourceDigest,
            lines = new System.Collections.Generic.List<
                ProductionPreparedOutputRoutingLineSaveData>
            {
                new()
                {
                    outputLineId = wasteDescriptor.OutputLineId,
                    role = ProductionOutputRole.RecoverableWaste,
                    itemId = wasteDescriptor.ItemId,
                    originalQuantity = liveClaim.Disposition
                        .RecoverableWasteQuantity,
                    remainingQuantity = liveClaim.Disposition
                        .RecoverableWasteQuantity,
                    originalMassGrams = liveClaim.Disposition
                        .RecoverableWasteMassGrams,
                    remainingMassGrams = liveClaim.Disposition
                        .RecoverableWasteMassGrams
                }
            }
        };
        ProductionPreparedOutputRoutingSaveData terminalRouting = new()
        {
            batches = new System.Collections.Generic.List<
                ProductionPreparedOutputRoutingBatchSaveData>
            {
                terminalBatch
            }
        };
        ProductionOutputCapacityDurableProjection terminalDurable =
            ProductionOutputDestinationDurableSaveProjector
                .ProjectCapacityRoutingFromSave(
                    subject.FacilityId,
                    world,
                    new DungeonProductionBillSaveData(),
                    EmptyGenericTerminalPayload(),
                    new DungeonPhysicalItemSaveData(),
                    new DungeonCharacterWorldSaveData(),
                    terminalRouting,
                    Array.Empty<FacilityOutputExactRouteOutboxSaveData>(),
                    new FixedBuildingDefinitionLookup(definition),
                    projector,
                    massQuery);
        Require(
            terminalDurable.Profile != null
            && terminalDurable.Profile.MaxMassGrams ==
                detachedSource.RequiredMinimumCapacityGrams,
            "Terminal ruined routing did not retain the proof-sized capacity after its production bill retired.");

        terminalBatch.maximumBatchMassGrams -= 600L;
        ExpectMessage<InvalidOperationException>(
            () => ProductionOutputDestinationDurableSaveProjector
                .ProjectCapacityRoutingFromSave(
                    subject.FacilityId,
                    world,
                    new DungeonProductionBillSaveData(),
                    EmptyGenericTerminalPayload(),
                    new DungeonPhysicalItemSaveData(),
                    new DungeonCharacterWorldSaveData(),
                    terminalRouting,
                    Array.Empty<FacilityOutputExactRouteOutboxSaveData>(),
                    new FixedBuildingDefinitionLookup(definition),
                    projector,
                    massQuery),
            "invalid capacity authority");
        terminalBatch.maximumBatchMassGrams = detachedClaim.MaximumMassProof
            .MaximumBatchMassGrams;

        saved.wipInputMassGrams += 600L;
        ProductionRuinedOutputCapacityClaim wipDrift = projector
            .CaptureRuinedClaim(saved, wasteDescriptor);
        Require(
            !string.Equals(
                liveClaim.SourceDigest,
                wipDrift.SourceDigest,
                StringComparison.Ordinal)
            && wipDrift.MaximumMassProof.MaximumBatchMassGrams == 1_200L,
            "Ruined-output WIP drift did not invalidate and resize the capacity claim.");
    }

    private static BuildingSO Support(
        string supportId,
        string[] featureTags,
        ProductionSupportKind kind,
        float outputMultiplier)
    {
        BuildingSO result = ScriptableObject.CreateInstance<BuildingSO>();
        BuildingAbilityCollection abilities = new();
        abilities.Add(new BuildingProductionSupportAbility
        {
            supportId = supportId,
            featureTags = featureTags,
            compatibleWorkstationTags = new[] { "workstation:qa" },
             kind = kind,
             outputMultiplier = outputMultiplier,
             maximumLinkedInstancesPerWorkstation = 1
         });
        result.ReplaceAbilities(abilities);
        return result;
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
            projector.CapturePortfolioSource(feedbench);
        ProductionOutputBufferCapacitySourceSnapshot repeat =
            projector.CapturePortfolioSource(feedbench);
        Require(first.MaximumBatchMassGrams == 1_050L
            && first.ProjectedPortfolioCapacityGrams == 4_200L
            && first.BatchMinimumCapacityGrams == 0L
            && first.RequiredMinimumCapacityGrams == 4_200L
            && string.Equals(first.SourceDigest, repeat.SourceDigest,
                StringComparison.Ordinal)
            && IsLowercaseSha256(first.SourceDigest),
            "Feedbench capacity source was not deterministic at exact 4,200g.");

        ProductionRecipeSO authoredRecipe = recipes
            .Where(value => string.Equals(
                value.WorkstationTag,
                feedbench.WorkstationTag,
                StringComparison.Ordinal))
            .Where(value => value.CaptureCanonicalOutputs().Any(output =>
                output != null
                && output.Probability > 0f
                && output.Role is ProductionOutputRole.Main
                    or ProductionOutputRole.Byproduct))
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .First();
        ProductionOutputDefinition authoredOutput = authoredRecipe
            .CaptureCanonicalOutputs()
            .Where(output => output != null
                && output.Probability > 0f
                && output.Role is ProductionOutputRole.Main
                    or ProductionOutputRole.Byproduct)
            .OrderBy(output => output.OutputLineId, StringComparer.Ordinal)
            .First();
        int authoredMaximumQuantity = maximumFactors
            .ResolveMaximum(authoredRecipe)
            .CeilQuantity(authoredOutput.Amount);
        long definitionUnitMass = massQuery.GetDefinitionUnitMass(
            (ItemDefinitionId)authoredOutput.ItemId).Value;
        string descriptorFingerprint =
            ProductionOutputCapabilityDescriptorFingerprint.Capture(
                authoredOutput.OutputLineId,
                authoredOutput.ItemId,
                ProductionOutputCapabilityIds.StandardDefinition,
                ProductionOutputCapabilityIds.StandardDefinitionVersion,
                ProductionOutputCapabilityIds.DefinitionOnlyCodec,
                ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion);
        ProductionPreparedOutputLineSaveData forgedLine = new()
        {
            outputLineId = authoredOutput.OutputLineId,
            role = authoredOutput.Role,
            itemId = authoredOutput.ItemId,
            outputCapabilityId =
                ProductionOutputCapabilityIds.StandardDefinition,
            outputCapabilityVersion =
                ProductionOutputCapabilityIds.StandardDefinitionVersion,
            outputComponentCodecId =
                ProductionOutputCapabilityIds.DefinitionOnlyCodec,
            outputComponentCodecVersion =
                ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion,
            outputCapabilityFingerprint = descriptorFingerprint,
            quantity = authoredMaximumQuantity + 1,
            exactMassGrams = checked(
                definitionUnitMass * (authoredMaximumQuantity + 1L))
        };
        ExpectMessage<InvalidOperationException>(
            () => projector.CapturePreparedClaim(
                "production-bill:qa-forged-normal-maximum",
                1,
                authoredRecipe,
                new[] { forgedLine }),
            "exceeds its authored output maximum");

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
            projector.CapturePortfolioSource(savedSubject);
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
                projector.CapturePortfolioSource(movedSavedSubject).SourceDigest,
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
                    EmptyGenericTerminalPayload(),
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
                EmptyGenericTerminalPayload(),
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
            shuffledProjector.CapturePortfolioSource(feedbench);
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
                EmptyGenericTerminalPayload(),
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
            projector.CapturePortfolioSource(moved);
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

        Require(first.RequiredMinimumCapacityGrams == 4_200L,
            "Feedbench portfolio capacity drifted after detached-source validation.");
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
            projector.CapturePortfolioSource(sawmill);
        ProductionOutputBufferCapacitySourceSnapshot repeat =
            projector.CapturePortfolioSource(sawmill);
        Require(first.MaximumBatchMassGrams == 3_600L
            && first.ProjectedPortfolioCapacityGrams == 14_400L
            && first.BatchMinimumCapacityGrams == 0L
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
            "workstation:quarry",
            new[] { "source:quarry" },
            maximumBatchMassGrams: 15_300L,
            projectedCapacityGrams: 61_200L,
            position: new Vector2Int(41, 19),
            expectedMaximumFactor: new ProductionOutputFactor(5L, 4L));
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
        Vector2Int position,
        ProductionOutputFactor? expectedMaximumFactor = null)
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
                ProductionPreparedOutputMigrationScope
                    .CaptureProfileDigest(value).Length == 64),
            $"Workstation '{workstationTag}' has a noncanonical prepared-output profile.");
        ProductionOutputFactor requiredMaximumFactor = expectedMaximumFactor
            ?? ProductionOutputFactor.One;
        Require(reachable.All(value =>
                maximumFactors.ResolveMaximum(value).Equals(
                    requiredMaximumFactor)),
            $"Workstation '{workstationTag}' maximum factor drifted from "
            + $"{requiredMaximumFactor.Numerator}/"
            + $"{requiredMaximumFactor.Denominator}.");

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
            projector.CapturePortfolioSource(facility);
        ProductionOutputBufferCapacitySourceSnapshot repeat =
            projector.CapturePortfolioSource(facility);
        Require(first.MaximumBatchMassGrams == maximumBatchMassGrams
            && first.ProjectedPortfolioCapacityGrams == projectedCapacityGrams
            && first.BatchMinimumCapacityGrams == 0L
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
            cycleCapacity,
            ProductionFacilityCapacitySubjectAdapter
                .CaptureProcessFluidProfile(definition),
            ProductionFacilityCapacitySubjectAdapter
                .CaptureWorkstationLaneProfile(definition));
    }

    private static bool IsLowercaseSha256(string value) =>
        value != null
        && value.Length == 64
        && value.All(character =>
            character is >= '0' and <= '9'
            || character is >= 'a' and <= 'f');

    private static DungeonProductionGenericBillTerminalDrainSaveData
        EmptyGenericTerminalPayload() => new()
        {
            version = DungeonProductionGenericBillTerminalDrainSaveData
                .CurrentVersion,
            entries = new System.Collections.Generic.List<
                ProductionGenericBillTerminalDrainSaveData>()
        };

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
