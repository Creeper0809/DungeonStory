using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class WorldResourceOutputMaximumEnvelopeDebugScenarios
{
    public const string ArtifactPath =
        "Artifacts/QA/v27-world-resource-maximum-envelope.csv";

    [MenuItem("Tools/DungeonStory/Economy/Verify World Resource Maximum Envelope")]
    public static void RunFromMenu()
    {
        string report = RunAll();
        Debug.Log(report);
    }

    public static string RunAll()
    {
        EditorContentSource source = new();
        ResourceEconomyContentCatalog economy = source.CreateEconomyCatalog();
        IPhysicalItemMassQuery mass = new PhysicalItemMassQuery(
            EditorItemCatalogFactory.Create());
        ProductionOutputMaximumMassRegistry maximumMass =
            CreateMaximumRegistry(economy, mass);
        WorldResourceSourceBindingCatalog bindings = new(
            new IWorldResourceSourceBindingContributor[]
            {
                new BuiltInWorldResourceSourceBindingContributor()
            });
        WorldResourceOutputMaximumEnvelopeAuthority authority = new(
            economy,
            bindings,
            maximumMass);

        Require(bindings.Bindings.Count == 4,
            "Expected four registered WorldResource topology bindings.");
        Require(authority.Envelopes.Count == 3,
            "Expected three unique WorldResource output recipe envelopes.");
        RequireMaximum(authority, "source:grass", 320L, 1);
        RequireMaximum(authority, "source:logging", 9_200L, 2);
        RequireMaximum(authority, "source:saltstone", 5_300L, 2);
        Require(authority.Require("source:grass").BindingIds.Count == 2,
            "Grass and brush bindings did not collapse to one recipe envelope.");

        VerifyBindingInputOrderDeterminism(economy, maximumMass);
        VerifyProbabilityLossAndFactorCeiling(source, mass);
        WriteArtifact(authority.Envelopes);

        string hash = V27BalanceArtifactWriter.ComputeSha256(ArtifactPath);
        Require(hash.Length == 64,
            "WorldResource maximum artifact hash is invalid.");
        return string.Join("\n", new[]
        {
            "# V27 WorldResource Maximum Envelope",
            "bindingCount=4",
            "uniqueRecipeCount=3",
            "source:grass=320",
            "source:logging=9200",
            "source:saltstone=5300",
            "probabilityPositiveIncluded=true",
            "probabilityZeroExcluded=true",
            "declaredLossExcluded=true",
            "factorCeilingExact=true",
            "bindingInputShuffleDeterministic=true",
            "artifactSha256=" + hash,
            "RESULT=PASS"
        });
    }

    private static ProductionOutputMaximumMassRegistry CreateMaximumRegistry(
        IResourceEconomyContentCatalog economy,
        IPhysicalItemMassQuery mass)
    {
        ProductionPreparedOutputComponentCodec codec = new();
        return new ProductionOutputMaximumMassRegistry(
            new IProductionOutputMaximumMassCapability[]
            {
                new StandardDefinitionProductionOutputCapability(economy, codec)
            },
            mass);
    }

    private static void RequireMaximum(
        IWorldResourceOutputMaximumEnvelopeAuthority authority,
        string recipeId,
        long expectedMass,
        int expectedPhysicalLines)
    {
        WorldResourceOutputMaximumEnvelopeSnapshot value = authority.Require(
            recipeId);
        Require(value.MaximumOutputMassGrams == expectedMass,
            recipeId + " maximum mass drifted: "
            + value.MaximumOutputMassGrams + " != " + expectedMass);
        Require(value.Lines.Count(line => line.MaximumQuantity > 0)
                == expectedPhysicalLines,
            recipeId + " maximum physical-line census drifted.");
        Require(value.Lines.Sum(line => line.MaximumMassGrams)
                == value.MaximumOutputMassGrams,
            recipeId + " maximum line sum drifted.");
    }

    private static void VerifyBindingInputOrderDeterminism(
        IResourceEconomyContentCatalog economy,
        IProductionOutputMaximumMassRegistry maximumMass)
    {
        WorldResourceSourceBinding[] canonical =
            new BuiltInWorldResourceSourceBindingContributor()
                .CaptureBindings().ToArray();
        WorldResourceOutputMaximumEnvelopeAuthority forward = new(
            economy,
            new WorldResourceSourceBindingCatalog(new[]
            {
                new StaticBindingContributor(canonical)
            }),
            maximumMass);
        WorldResourceOutputMaximumEnvelopeAuthority reverse = new(
            economy,
            new WorldResourceSourceBindingCatalog(new[]
            {
                new StaticBindingContributor(canonical.Reverse())
            }),
            maximumMass);
        Require(string.Equals(
                forward.AuthorityFingerprint,
                reverse.AuthorityFingerprint,
                StringComparison.Ordinal)
            && forward.Envelopes.Select(value => value.SourceDigest)
                .SequenceEqual(
                    reverse.Envelopes.Select(value => value.SourceDigest),
                    StringComparer.Ordinal),
            "WorldResource maximum authority depends on binding input order.");
    }

    private static void VerifyProbabilityLossAndFactorCeiling(
        EditorContentSource source,
        IPhysicalItemMassQuery mass)
    {
        ResourceItemDefinitionSO log = source.GetAll<ResourceItemDefinitionSO>()
            .Single(value => string.Equals(
                value.ItemId,
                "resource:log",
                StringComparison.Ordinal));
        ProductionRecipeSO recipe = ScriptableObject
            .CreateInstance<ProductionRecipeSO>();
        try
        {
            recipe.Configure(
                "source:qa-maximum-semantics",
                "QA maximum semantics",
                string.Empty,
                "alchemy",
                BuiltInWorkTypeIds.Logging.Value,
                string.Empty,
                1f,
                Array.Empty<ItemAmountDefinition>(),
                new[]
                {
                    new ProductionOutputDefinition(
                        "output:qa-maximum-semantics/positive",
                        ProductionOutputRole.Main,
                        log.ItemId,
                        1,
                        0.01f),
                    new ProductionOutputDefinition(
                        "output:qa-maximum-semantics/zero",
                        ProductionOutputRole.Byproduct,
                        log.ItemId,
                        100,
                        0f),
                    new ProductionOutputDefinition(
                        "output:qa-maximum-semantics/loss",
                        ProductionOutputRole.DeclaredLoss,
                        log.ItemId,
                        100,
                        1f)
                });
            recipe.ConfigureProficiency(
                BuiltInCharacterProficiencyIds.Fieldwork);
            recipe.ConfigureProcessClass(ProductionProcessClass.Gathering);
            recipe.ConfigureFlowRole(ProductionFlowRole.Source);
            ResourceEconomyContentCatalog economy = new(
                new[] { log },
                new[] { recipe },
                Array.Empty<CropDefinitionSO>(),
                Array.Empty<CraftMaterialDefinitionSO>());
            WorldResourceSourceBinding binding = new(
                "world-resource-binding:qa-maximum-semantics",
                WorldResourceSourceBindingKind.Visual,
                WorldResourceVisualKind.Tree,
                default,
                BuiltInWorkTypeIds.Logging,
                recipe.RecipeId);
            WorldResourceOutputMaximumEnvelopeAuthority authority = new(
                economy,
                new WorldResourceSourceBindingCatalog(new[]
                {
                    new StaticBindingContributor(new[] { binding })
                }),
                CreateMaximumRegistry(economy, mass));
            WorldResourceOutputMaximumEnvelopeSnapshot envelope =
                authority.Require(recipe.RecipeId);
            WorldResourceOutputMaximumLineSnapshot positive = envelope.Lines
                .Single(value => value.OutputLineId.EndsWith(
                    "/positive",
                    StringComparison.Ordinal));
            Require(positive.MaximumQuantity == 2
                    && positive.MaximumMassGrams
                        == checked(positive.UnitMassGrams * 2L),
                "Positive-probability factor ceiling was not reserved.");
            Require(envelope.Lines.Where(value => value.OutputLineId.EndsWith(
                        "/zero",
                        StringComparison.Ordinal)
                    || value.OutputLineId.EndsWith(
                        "/loss",
                        StringComparison.Ordinal))
                .All(value => value.MaximumQuantity == 0
                    && value.MaximumMassGrams == 0L),
                "Probability-zero or DeclaredLoss output consumed physical capacity.");
        }
        finally
        {
            UnityEngine.Object.DestroyImmediate(recipe);
        }
    }

    private static void WriteArtifact(
        IReadOnlyList<WorldResourceOutputMaximumEnvelopeSnapshot> rows)
    {
        V27BalanceArtifactWriter.WriteIfDifferent(
            ArtifactPath,
            stream =>
            {
                using StreamWriter writer = new(
                    stream,
                    new UTF8Encoding(false, true),
                    16_384,
                    leaveOpen: true);
                WriteCsvRow(writer, new[]
                {
                    "schema",
                    "recipeId",
                    "bindingIds",
                    "recipeSourceDigest",
                    "maximumFactorNumerator",
                    "maximumFactorDenominator",
                    "lineProofs",
                    "maximumOutputMassGrams",
                    "massAuthorityRevision",
                    "maximumRegistryFingerprint",
                    "sourceDigest"
                });
                foreach (WorldResourceOutputMaximumEnvelopeSnapshot row
                         in rows.OrderBy(
                             value => value.RecipeId,
                             StringComparer.Ordinal))
                {
                    string lineProofs = string.Join(
                        ";",
                        row.Lines.OrderBy(
                                value => value.OutputLineId,
                                StringComparer.Ordinal)
                            .Select(value => string.Join(
                                "|",
                                value.OutputLineId,
                                value.Role.ToString(),
                                value.ItemId,
                                value.InclusionProbability.ToString(
                                    "R",
                                    CultureInfo.InvariantCulture),
                                value.MaximumQuantity.ToString(
                                    CultureInfo.InvariantCulture),
                                value.UnitMassGrams.ToString(
                                    CultureInfo.InvariantCulture),
                                value.MaximumMassGrams.ToString(
                                    CultureInfo.InvariantCulture),
                                value.ProjectionSourceDigest)));
                    WriteCsvRow(writer, new[]
                    {
                        WorldResourceOutputMaximumEnvelopeAuthority.Schema,
                        row.RecipeId,
                        string.Join(";", row.BindingIds),
                        row.RecipeSourceDigest,
                        row.MaximumOutputFactor.Numerator.ToString(
                            CultureInfo.InvariantCulture),
                        row.MaximumOutputFactor.Denominator.ToString(
                            CultureInfo.InvariantCulture),
                        lineProofs,
                        row.MaximumOutputMassGrams.ToString(
                            CultureInfo.InvariantCulture),
                        row.MassAuthorityRevision.ToString(
                            CultureInfo.InvariantCulture),
                        row.MaximumRegistryFingerprint,
                        row.SourceDigest
                    });
                }
                writer.Flush();
            });
    }

    private static void WriteCsvRow(
        StreamWriter writer,
        IReadOnlyList<string> fields)
    {
        for (int index = 0; index < fields.Count; index++)
        {
            if (index > 0)
                writer.Write(',');
            V27BalanceCsvSerializer.WriteEscapedField(
                writer,
                (fields[index] ?? string.Empty).AsSpan());
        }
        writer.Write('\r');
        writer.Write('\n');
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    private sealed class StaticBindingContributor :
        IWorldResourceSourceBindingContributor
    {
        private readonly IReadOnlyList<WorldResourceSourceBinding> values;

        internal StaticBindingContributor(
            IEnumerable<WorldResourceSourceBinding> values)
        {
            this.values = Array.AsReadOnly((values
                    ?? throw new ArgumentNullException(nameof(values)))
                .ToArray());
        }

        public string ContributorId =>
            "world-resource-source-bindings:qa-static";
        public int ContractVersion => 1;
        public IReadOnlyList<WorldResourceSourceBinding> CaptureBindings() =>
            values;
    }

    private sealed class EditorContentSource
    {
        internal T[] GetAll<T>() where T : UnityEngine.Object =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name,
                    new[] { "Assets/Resources" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(value => value != null)
                .ToArray();

        internal ResourceEconomyContentCatalog CreateEconomyCatalog() => new(
            GetAll<ResourceItemDefinitionSO>(),
            GetAll<ProductionRecipeSO>(),
            GetAll<CropDefinitionSO>(),
            GetAll<CraftMaterialDefinitionSO>());
    }
}
