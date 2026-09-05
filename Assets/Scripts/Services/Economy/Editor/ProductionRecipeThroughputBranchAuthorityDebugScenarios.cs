#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

public static class ProductionRecipeThroughputBranchAuthorityDebugScenarios
{
    private const string WorkstationTag = "workstation:qa-branch-authority";

    [MenuItem("DungeonStory/V27/Production/Validate Recipe Throughput Branch Authority")]
    public static void Validate()
    {
        VerifyAssignmentFactorsAreAppliedOnce();
        VerifySupportAssignmentsRemainSeparated();
        VerifyPassiveAndZeroPhysicalAreTypedMissing();
        VerifyMissingProjectionFailsLoud();
        VerifyProjectionDriftAndOverflowFailLoud();
        VerifyInputShuffleIsDeterministic();
        Debug.Log(
            "[ProductionRecipeThroughputBranchAuthority] focused scenarios passed.");
    }

    private static void VerifyAssignmentFactorsAreAppliedOnce()
    {
        BuildingSO heat = Support(
            "support:qa-heat",
            "support:qa-heat",
            ProductionSupportKind.Passive,
            outputMultiplier: 2f);
        BuildingSO air = Support(
            "support:qa-air",
            "support:qa-air",
            ProductionSupportKind.Passive,
            outputMultiplier: 3f);
        ProductionRecipeSO recipe = Recipe(
            "recipe:qa-factor-once",
            ProductionProcessKind.WorkOnly,
            new[] { Output("output:qa-main", "item:qa-main", 1) },
            new[] { "support:qa-heat", "support:qa-air" });
        try
        {
            ProductionMaximumOutputFactorCatalog factors = new(
                new[] { heat, air });
            ProductionAuthoredSupportAssignmentSnapshot assignment = factors
                .CaptureFeasibleAssignments(recipe)
                .Single();
            FixedMaximumMassRegistry registry = new(new Dictionary<string, long>
            {
                ["item:qa-main"] = 100L
            });
            ProductionRecipeThroughputBranchQueryResult result =
                new ProductionRecipeThroughputBranchAuthority(registry).Capture(
                    recipe,
                    ProductionFacilityProcessFluidCapacityProfile.Empty,
                    assignment);

            Require(result.IsComplete
                && result.Branches.Single().MaximumOutputMassGrams == 600L
                && registry.CapturedQuantities.Single() == 6,
                "The exact assignment's 2x and 3x output factors were not multiplied exactly once.");
        }
        finally
        {
            Destroy(recipe, heat, air);
        }
    }

    private static void VerifySupportAssignmentsRemainSeparated()
    {
        BuildingSO doubleSupport = Support(
            "support:qa-double",
            "support:qa-alternative",
            ProductionSupportKind.Passive,
            outputMultiplier: 2f);
        BuildingSO tripleSupport = Support(
            "support:qa-triple",
            "support:qa-alternative",
            ProductionSupportKind.Passive,
            outputMultiplier: 3f);
        ProductionRecipeSO recipe = Recipe(
            "recipe:qa-assignment-separation",
            ProductionProcessKind.WorkOnly,
            new[] { Output("output:qa-main", "item:qa-main", 2) },
            new[] { "support:qa-alternative" });
        try
        {
            ProductionMaximumOutputFactorCatalog factors = new(
                new[] { tripleSupport, doubleSupport });
            ProductionAuthoredSupportAssignmentSnapshot[] assignments = factors
                .CaptureFeasibleAssignments(recipe)
                .ToArray();
            FixedMaximumMassRegistry registry = new(new Dictionary<string, long>
            {
                ["item:qa-main"] = 100L
            });
            ProductionRecipeThroughputBranchAuthority authority = new(registry);
            ProductionRecipeThroughputBranchSnapshot[] branches = assignments
                .Select(assignment => authority.Capture(
                        recipe,
                        ProductionFacilityProcessFluidCapacityProfile.Empty,
                        assignment)
                    .Branches.Single())
                .OrderBy(value => value.MaximumOutputMassGrams)
                .ToArray();

            Require(assignments.Length == 2
                && branches.Select(value => value.MaximumOutputMassGrams)
                    .SequenceEqual(new[] { 400L, 600L })
                && branches.Select(value => value.SupportAssignmentSourceDigest)
                    .Distinct(StringComparer.Ordinal).Count() == 2,
                "Alternative support assignments were merged into one recipe branch maximum.");
        }
        finally
        {
            Destroy(recipe, doubleSupport, tripleSupport);
        }
    }

    private static void VerifyPassiveAndZeroPhysicalAreTypedMissing()
    {
        BuildingSO batch = Support(
            "support:qa-batch",
            "support:qa-batch",
            ProductionSupportKind.BatchProcessor,
            outputMultiplier: 1f,
            batchCapacity: 2);
        ProductionRecipeSO passive = Recipe(
            "recipe:qa-passive-missing",
            ProductionProcessKind.PassiveBatch,
            new[] { Output("output:qa-main", "item:qa-main", 1) },
            new[] { "support:qa-batch" },
            "support:qa-batch");
        ProductionRecipeSO zeroPhysical = Recipe(
            "recipe:qa-zero-physical",
            ProductionProcessKind.WorkOnly,
            new[]
            {
                new ProductionOutputDefinition(
                    "output:qa-loss",
                    ProductionOutputRole.DeclaredLoss,
                    "loss:qa",
                    1)
            });
        try
        {
            ProductionAuthoredSupportAssignmentSnapshot passiveAssignment =
                new ProductionMaximumOutputFactorCatalog(new[] { batch })
                    .CaptureFeasibleAssignments(passive)
                    .Single();
            ProductionAuthoredSupportAssignmentSnapshot emptyAssignment =
                new ProductionMaximumOutputFactorCatalog(
                        Array.Empty<BuildingSO>())
                    .CaptureFeasibleAssignments(zeroPhysical)
                    .Single();
            FixedMaximumMassRegistry registry = new(new Dictionary<string, long>
            {
                ["item:qa-main"] = 100L
            });
            ProductionRecipeThroughputBranchAuthority authority = new(registry);
            ProductionRecipeThroughputBranchQueryResult passiveResult =
                authority.Capture(
                    passive,
                    ProductionFacilityProcessFluidCapacityProfile.Empty,
                    passiveAssignment);
            ProductionRecipeThroughputBranchQueryResult zeroResult =
                authority.Capture(
                    zeroPhysical,
                    ProductionFacilityProcessFluidCapacityProfile.Empty,
                    emptyAssignment);

            Require(!passiveResult.IsComplete
                && passiveResult.MissingReason
                    == ProductionThroughputGapReason
                        .RecipeOutputBranchAuthorityMissing
                && !zeroResult.IsComplete
                && zeroResult.MissingReason
                    == ProductionThroughputGapReason
                        .RecipeOutputBranchAuthorityMissing
                && registry.CaptureCount == 0,
                "Passive or zero-physical output was presented as a complete normal branch.");
        }
        finally
        {
            Destroy(passive, zeroPhysical, batch);
        }
    }

    private static void VerifyMissingProjectionFailsLoud()
    {
        ProductionRecipeSO recipe = Recipe(
            "recipe:qa-missing-projection",
            ProductionProcessKind.WorkOnly,
            new[] { Output("output:qa-missing", "item:qa-missing", 1) });
        try
        {
            ProductionAuthoredSupportAssignmentSnapshot assignment =
                EmptyAssignment(recipe);
            ProductionRecipeThroughputBranchAuthority authority = new(
                new FixedMaximumMassRegistry(
                    new Dictionary<string, long>()));
            Expect<InvalidOperationException>(() => authority.Capture(
                recipe,
                ProductionFacilityProcessFluidCapacityProfile.Empty,
                assignment));
        }
        finally
        {
            Destroy(recipe);
        }
    }

    private static void VerifyProjectionDriftAndOverflowFailLoud()
    {
        ProductionRecipeSO driftRecipe = Recipe(
            "recipe:qa-projection-drift",
            ProductionProcessKind.WorkOnly,
            new[] { Output("output:qa-drift", "item:qa-drift", 1) });
        ProductionRecipeSO quantityOverflow = Recipe(
            "recipe:qa-quantity-overflow",
            ProductionProcessKind.WorkOnly,
            new[]
            {
                Output("output:qa-overflow", "item:qa-overflow", int.MaxValue)
            },
            new[] { "support:qa-double-overflow" });
        ProductionRecipeSO sumOverflow = Recipe(
            "recipe:qa-sum-overflow",
            ProductionProcessKind.WorkOnly,
            new[]
            {
                Output("output:qa-a", "item:qa-a", 1),
                Output("output:qa-b", "item:qa-b", 1)
            });
        BuildingSO doubleSupport = Support(
            "support:qa-double-overflow",
            "support:qa-double-overflow",
            ProductionSupportKind.Passive,
            outputMultiplier: 2f);
        try
        {
            ProductionRecipeThroughputBranchAuthority drift = new(
                new FixedMaximumMassRegistry(
                    new Dictionary<string, long>
                    {
                        ["item:qa-drift"] = 100L
                    },
                    driftDescriptor: true));
            Expect<InvalidOperationException>(() => drift.Capture(
                driftRecipe,
                ProductionFacilityProcessFluidCapacityProfile.Empty,
                EmptyAssignment(driftRecipe)));

            ProductionAuthoredSupportAssignmentSnapshot overflowAssignment =
                new ProductionMaximumOutputFactorCatalog(
                        new[] { doubleSupport })
                    .CaptureFeasibleAssignments(quantityOverflow)
                    .Single();
            ProductionRecipeThroughputBranchAuthority quantity = new(
                new FixedMaximumMassRegistry(
                    new Dictionary<string, long>
                    {
                        ["item:qa-overflow"] = 1L
                    }));
            Expect<OverflowException>(() => quantity.Capture(
                quantityOverflow,
                ProductionFacilityProcessFluidCapacityProfile.Empty,
                overflowAssignment));

            ProductionRecipeThroughputBranchAuthority total = new(
                new FixedMaximumMassRegistry(
                    new Dictionary<string, long>
                    {
                        ["item:qa-a"] = long.MaxValue,
                        ["item:qa-b"] = long.MaxValue
                    }));
            Expect<OverflowException>(() => total.Capture(
                sumOverflow,
                ProductionFacilityProcessFluidCapacityProfile.Empty,
                EmptyAssignment(sumOverflow)));
        }
        finally
        {
            Destroy(
                driftRecipe,
                quantityOverflow,
                sumOverflow,
                doubleSupport);
        }
    }

    private static void VerifyInputShuffleIsDeterministic()
    {
        ProductionOutputDefinition a = Output(
            "output:qa-a",
            "item:qa-a",
            2);
        ProductionOutputDefinition b = Output(
            "output:qa-b",
            "item:qa-b",
            3);
        ProductionRecipeSO forward = Recipe(
            "recipe:qa-output-shuffle",
            ProductionProcessKind.WorkOnly,
            new[] { a, b });
        ProductionRecipeSO reverse = Recipe(
            "recipe:qa-output-shuffle",
            ProductionProcessKind.WorkOnly,
            new[] { b, a });
        try
        {
            FixedMaximumMassRegistry registry = new(new Dictionary<string, long>
            {
                ["item:qa-b"] = 200L,
                ["item:qa-a"] = 100L
            });
            ProductionRecipeThroughputBranchAuthority authority = new(registry);
            ProductionRecipeThroughputBranchQueryResult first = authority.Capture(
                forward,
                ProductionFacilityProcessFluidCapacityProfile.Empty,
                EmptyAssignment(forward));
            ProductionRecipeThroughputBranchQueryResult second = authority.Capture(
                reverse,
                ProductionFacilityProcessFluidCapacityProfile.Empty,
                EmptyAssignment(reverse));

            Require(first.Branches.Single().MaximumOutputMassGrams == 800L
                && string.Equals(
                    first.Branches.Single().SourceDigest,
                    second.Branches.Single().SourceDigest,
                    StringComparison.Ordinal)
                && string.Equals(
                    first.SourceDigest,
                    second.SourceDigest,
                    StringComparison.Ordinal),
                "Canonical output input order changed the normal branch projection.");
        }
        finally
        {
            Destroy(forward, reverse);
        }
    }

    private static ProductionAuthoredSupportAssignmentSnapshot EmptyAssignment(
        ProductionRecipeSO recipe) => new ProductionMaximumOutputFactorCatalog(
            Array.Empty<BuildingSO>())
        .CaptureFeasibleAssignments(recipe)
        .Single();

    private static ProductionRecipeSO Recipe(
        string recipeId,
        ProductionProcessKind kind,
        IReadOnlyList<ProductionOutputDefinition> outputs,
        IReadOnlyList<string> supportTags = null,
        string batchSupportTag = "")
    {
        ProductionRecipeSO recipe = ScriptableObject
            .CreateInstance<ProductionRecipeSO>();
        recipe.Configure(
            recipeId,
            recipeId,
            string.Empty,
            "qa-branch-authority",
            "work:craft",
            string.Empty,
            1f,
            Array.Empty<ItemAmountDefinition>(),
            outputs);
        recipe.ConfigureWorkshop(
            WorkstationTag,
            supportTags ?? Array.Empty<string>(),
            kind,
            batchSupportTag,
            processGameHours: kind == ProductionProcessKind.PassiveBatch
                ? 1f
                : 0f,
            failedBatchItemId: kind == ProductionProcessKind.PassiveBatch
                ? "resource:qa-spoilage"
                : string.Empty);
        recipe.ConfigureProficiency(BuiltInCharacterProficiencyIds.Crafting);
        recipe.ConfigureProcessClass(
            ProductionProcessClass.CookingSimpleMixing);
        return recipe;
    }

    private static ProductionOutputDefinition Output(
        string lineId,
        string itemId,
        int amount) => new(
        lineId,
        ProductionOutputRole.Main,
        itemId,
        amount);

    private static BuildingSO Support(
        string supportId,
        string featureTag,
        ProductionSupportKind kind,
        float outputMultiplier,
        int batchCapacity = 1)
    {
        BuildingSO support = ScriptableObject.CreateInstance<BuildingSO>();
        BuildingAbilityCollection abilities = new();
        abilities.Add(new BuildingProductionSupportAbility
        {
            supportId = supportId,
            featureTags = new[] { featureTag },
            compatibleWorkstationTags = new[] { WorkstationTag },
            kind = kind,
            batchCapacity = batchCapacity,
            maximumLinkedInstancesPerWorkstation = 1,
            workSpeedMultiplier = 1f,
            outputMultiplier = outputMultiplier
        });
        support.ReplaceAbilities(abilities);
        return support;
    }

    private static string Digest(string token)
    {
        CanonicalSemanticDigestBuilder digest = new();
        digest.Append(
            "production-recipe-throughput-branch-authority-debug@1");
        digest.Append(token);
        return digest.ComputeSha256();
    }

    private static void Destroy(params UnityEngine.Object[] values)
    {
        foreach (UnityEngine.Object value in values)
        {
            if (value != null)
                UnityEngine.Object.DestroyImmediate(value);
        }
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
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
            "Expected exception was not thrown: " + typeof(T).Name);
    }

    private sealed class FixedMaximumMassRegistry :
        IProductionOutputMaximumMassRegistry
    {
        private readonly IReadOnlyDictionary<string, long> unitMassByItem;
        private readonly bool driftDescriptor;
        private readonly List<int> capturedQuantities = new();

        internal FixedMaximumMassRegistry(
            IReadOnlyDictionary<string, long> unitMassByItem,
            bool driftDescriptor = false)
        {
            this.unitMassByItem = unitMassByItem
                ?? throw new ArgumentNullException(nameof(unitMassByItem));
            this.driftDescriptor = driftDescriptor;
            CanonicalSemanticDigestBuilder digest = new();
            digest.Append("qa-maximum-mass-registry@1");
            foreach (KeyValuePair<string, long> pair in unitMassByItem
                         .OrderBy(value => value.Key, StringComparer.Ordinal))
            {
                digest.Append(pair.Key);
                digest.Append(pair.Value);
            }
            digest.Append(driftDescriptor);
            RegistryFingerprint = digest.ComputeSha256();
        }

        public IReadOnlyList<string> CapabilityIds { get; } = Array.AsReadOnly(
            new[] { ProductionOutputCapabilityIds.StandardDefinition });
        public IReadOnlyList<ProductionOutputCapabilityContractSnapshot>
            CapabilityContracts { get; } = Array.Empty<
                ProductionOutputCapabilityContractSnapshot>();
        public string RegistryFingerprint { get; }
        internal IReadOnlyList<int> CapturedQuantities => capturedQuantities;
        internal int CaptureCount => capturedQuantities.Count;

        public ProductionOutputMaximumMassProjection CaptureAutomatic(
            string outputLineId,
            string itemId,
            int maximumQuantity)
        {
            if (!unitMassByItem.TryGetValue(itemId, out long unitMass))
            {
                throw new InvalidOperationException(
                    "Missing fixture maximum-mass projection: " + itemId);
            }
            capturedQuantities.Add(maximumQuantity);
            string projectedLineId = driftDescriptor
                ? outputLineId + "-drift"
                : outputLineId;
            string capabilityId =
                ProductionOutputCapabilityIds.StandardDefinition;
            int capabilityVersion =
                ProductionOutputCapabilityIds.StandardDefinitionVersion;
            string codecId = ProductionOutputCapabilityIds.DefinitionOnlyCodec;
            int codecVersion =
                ProductionOutputCapabilityIds.DefinitionOnlyCodecVersion;
            ProductionOutputCapabilityDescriptor descriptor = new(
                projectedLineId,
                itemId,
                capabilityId,
                capabilityVersion,
                codecId,
                codecVersion,
                ProductionOutputCapabilityDescriptorFingerprint.Capture(
                    projectedLineId,
                    itemId,
                    capabilityId,
                    capabilityVersion,
                    codecId,
                    codecVersion));
            long maximumMass = checked(unitMass * maximumQuantity);
            CanonicalSemanticDigestBuilder digest = new();
            digest.Append("qa-maximum-mass-projection@1");
            digest.Append(descriptor.Fingerprint);
            digest.Append(maximumQuantity);
            digest.Append(unitMass);
            digest.Append(maximumMass);
            return new ProductionOutputMaximumMassProjection(
                descriptor,
                maximumQuantity,
                unitMass,
                maximumMass,
                17L,
                digest.ComputeSha256());
        }

        public ProductionOutputMaximumMassProjection CaptureDeclared(
            ProductionOutputCapabilityDescriptor descriptor,
            int maximumQuantity) => throw new NotSupportedException();
    }
}
#endif
