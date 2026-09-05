using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

public static class ProductionFacilityOutputCapacityContributorRegistryDebugScenarios
{
    private const string CropHarvestEnvelopeArtifactPath =
        "Artifacts/QA/v27-crop-harvest-maximum-envelope.csv";

    [MenuItem("DungeonStory/V27/Physical Mass/Verify Facility Output Capacity Contributors")]
    public static void RunFromMenu()
    {
        RunAll();
        Debug.Log("V27_FACILITY_OUTPUT_CAPACITY_CONTRIBUTORS_PASS");
    }

    public static void RunAll()
    {
        FixedSelector selector = new(new Dictionary<string, long>(
            StringComparer.Ordinal)
        {
            ["item:a"] = 100L,
            ["item:b"] = 50L,
            ["item:c"] = 250L,
            ["item:d"] = 200L
        });
        FixedContributor first = new(
            "contributor:a",
            new ProductionFacilityOutputCapacityBranch(
                "branch:combined",
                new[]
                {
                    Request("output:qa:a", "item:a"),
                    Request("output:qa:b", "item:b")
                }),
            new ProductionFacilityOutputCapacityBranch(
                "branch:heavy",
                new[] { Request("output:qa:c", "item:c") }));
        FixedContributor second = new(
            "contributor:b",
            new ProductionFacilityOutputCapacityBranch(
                "branch:medium",
                new[] { Request("output:qa:d", "item:d") }));
        ProductionFacilityCapacitySubject subject = Subject();

        ProductionFacilityOutputCapacityContributorRegistry forward = new(
            new IProductionFacilityOutputCapacityContributor[] { first, second },
            selector);
        ProductionFacilityOutputCapacityContributorRegistry reverse = new(
            new IProductionFacilityOutputCapacityContributor[] { second, first },
            selector);
        ProductionFacilityOutputCapacityAggregateSnapshot a = forward.Capture(subject);
        ProductionFacilityOutputCapacityAggregateSnapshot b = reverse.Capture(subject);
        Require(a.MaximumBatchMassGrams == 250L
            && a.ApplicableContributorCount == 2
            && a.BranchCount == 3
            && string.Equals(a.WinningContributorId, "contributor:a", StringComparison.Ordinal)
            && string.Equals(a.WinningBranchId, "branch:heavy", StringComparison.Ordinal),
            "Contributor registry summed alternative branches or selected the wrong maximum.");
        Require(string.Equals(forward.RegistryFingerprint, reverse.RegistryFingerprint,
                StringComparison.Ordinal)
            && string.Equals(a.SourceDigest, b.SourceDigest, StringComparison.Ordinal),
            "Contributor registry depends on insertion order.");

        ExpectThrows<InvalidOperationException>(() => new
            ProductionFacilityOutputCapacityContributorRegistry(
                new IProductionFacilityOutputCapacityContributor[]
                {
                    first,
                    new FixedContributor(
                        "contributor:a",
                        new ProductionFacilityOutputCapacityBranch(
                            "branch:duplicate",
                            new[] { Request("output:qa:d", "item:d") }))
                },
                selector));
        ProductionFacilityOutputCapacityAggregateSnapshot empty =
            EmptyProductionFacilityOutputCapacityContributorRegistry.Instance
                .Capture(subject);
        Require(empty.MaximumBatchMassGrams == 0L
            && empty.ApplicableContributorCount == 0
            && empty.BranchCount == 0,
            "Empty contributor registry did not remain an explicit zero authority.");
        VerifyAuthoredCertifiedSeedContributor();
        VerifyAuthoredApparelContributor();
        VerifySurgicalRecipeBackedPreprojection();
        VerifyCombatCraftPrimaryContributor();
        VerifyAuthoredCropHarvestContributor();
        WorldResourceOutputMaximumEnvelopeDebugScenarios.RunAll();
    }

    public static void RunSurgicalRecipeBackedPreprojectionFocused()
    {
        VerifySurgicalRecipeBackedPreprojection();
    }

    private static void VerifyAuthoredCropHarvestContributor()
    {
        AssetContentSource definitions = new();
        ResourceEconomyContentCatalog economy = new(
            definitions.GetAll<ResourceItemDefinitionSO>(),
            definitions.GetAll<ProductionRecipeSO>(),
            definitions.GetAll<CropDefinitionSO>(),
            definitions.GetAll<CraftMaterialDefinitionSO>());
        Require(economy.Crops.Count == 12,
            "Crop harvest contributor must enumerate all 12 catalog crops.");

        NaturalGoldenHarvestReachableMaximumWitnessContributor
            witnessContributor = new(
                definitions,
                new CharacterPerformanceFormulaCatalog(definitions));
        CropHarvestReachableMaximumWitnessSnapshot reachableWitness =
            witnessContributor.Capture();
        CropGenomeReachableMaximumWitnessCatalog genomeWitnesses = new(
            definitions);
        CropHarvestFacilityOutputCapacityContributor contributor = new(
            economy,
            definitions,
            new ICropHarvestReachableMaximumWitnessContributor[]
            {
                witnessContributor
            });
        IPhysicalItemMassQuery mass = new PhysicalItemMassQuery(
            EditorItemCatalogFactory.Create());
        ProductionPreparedOutputComponentCodec codec = new();
        ProductionOutputMaximumMassRegistry maximumMass = new(
            new IProductionOutputMaximumMassCapability[]
            {
                new StandardDefinitionProductionOutputCapability(economy, codec),
                new CropHarvestSeedLotOutputCapability(economy)
            },
            mass);
        ProductionFacilityOutputCapacityContributorRegistry registry = new(
            new IProductionFacilityOutputCapacityContributor[] { contributor },
            maximumMass);

        BuildingSO[] plots = definitions.GetAll<BuildingSO>()
            .Where(value => value != null
                && value.GetAbility<BuildingCropPlotAbility>() != null)
            .OrderBy(
                ProductionFacilityDefinitionIdentity.Resolve,
                StringComparer.Ordinal)
            .ToArray();
        Require(plots.Length == 4,
            "Expected four authored crop-plot facilities, found "
            + plots.Length + ".");
        List<CropHarvestEnvelopeRow> envelopeRows = new();
        int seedQuantityMaximum = CropHarvestOutputRules
            .ResolveReturnedSeedQuantity(
                CropHarvestOutputRules.MaximumReturnedSeedCount,
                reachableWitness.ReturnedSeedMultiplier,
                hasSeedSelection: true);
        foreach (BuildingSO plot in plots)
        {
            BuildingCropPlotAbility cropPlot =
                plot.GetAbility<BuildingCropPlotAbility>();
            BuildingProductionWorkstationAbility workstation =
                plot.GetProductionWorkstationAbility();
            BuildingProductionBufferAbility buffer =
                plot.GetProductionBufferAbility();
            Require(workstation != null
                    && buffer != null
                    && buffer.physicalOutputBufferCycleCapacity is >= 2 and <= 4,
                "Crop plot lacks a canonical workstation/output buffer: "
                + plot.name);
            ProductionFacilityCapacitySubject subject = new(
                (BuildingInstanceId)("building:qa:crop-contributor:"
                    + ProductionFacilityDefinitionIdentity.Resolve(plot)),
                Vector2Int.zero,
                ProductionFacilityDefinitionIdentity.Resolve(plot),
                workstation.WorkstationTag,
                buffer.physicalOutputBufferCycleCapacity,
                ProductionFacilityCapacitySubjectAdapter
                    .CaptureWorkstationLaneProfile(plot),
                ProductionFacilityCapacitySubjectAdapter
                    .CaptureProcessFluidProfile(plot));
            ProductionFacilityOutputCapacityContribution captured =
                contributor.Capture(subject);
            CropDefinitionSO[] applicable = economy.Crops
                .Where(value => value != null
                    && (!cropPlot.Indoor || value.IndoorAllowed))
                .OrderBy(value => value.CropId, StringComparer.Ordinal)
                .ToArray();
            Require(captured.AppliesToFacility
                    && captured.Branches.Count == applicable.Length
                    && captured.Branches.All(value => value.Outputs.Count == 2)
                    && captured.Branches.All(value =>
                        value.SemanticSourceDigest?.Length == 64)
                    && captured.Branches.Select(value => value.BranchId)
                        .Distinct(StringComparer.Ordinal).Count()
                        == applicable.Length,
                "Crop contributor branch census drifted for " + plot.name);
            Require(captured.Branches.SelectMany(value => value.Outputs)
                    .Count(value => string.Equals(
                        value.CapabilityId,
                        ProductionOutputCapabilityIds.CropHarvestSeedLot,
                        StringComparison.Ordinal)) == applicable.Length,
                "Crop contributor did not assign one returned-seed capability per crop.");

            ProductionFacilityOutputCapacityAggregateSnapshot aggregate =
                registry.Capture(subject);
            long expectedMaximum = applicable.Max(crop =>
            {
                CropGenomeReachableMaximumWitnessSnapshot genome =
                    genomeWitnesses.Capture(crop.CropId);
                ProductionOutputFactor output = cropPlot.Indoor
                    ? ProductionOutputFactorAuthority
                        .ResolveMaximumGrandProject("crop-indoor")
                    : ProductionOutputFactor.One;
                int harvestQuantity = CropHarvestOutputRules
                    .ResolveHarvestQuantity(
                        crop.Yield,
                        output.Numerator / (float)output.Denominator,
                        reachableWitness.WorkerYieldMultiplier,
                        1f,
                        genome.YieldMultiplier,
                        hasSoilDiagnostics: true);
                int seedQuantity = CropHarvestOutputRules
                    .ResolveReturnedSeedQuantity(
                        CropHarvestOutputRules.MaximumReturnedSeedCount,
                        reachableWitness.ReturnedSeedMultiplier,
                        hasSeedSelection: true);
                return checked(
                    mass.GetDefinitionUnitMass(
                        (ItemDefinitionId)crop.HarvestItemId).Value
                    * harvestQuantity
                    + mass.GetDefinitionUnitMass(
                        (ItemDefinitionId)crop.SeedItemId).Value
                    * seedQuantity);
            });
            Require(aggregate.ApplicableContributorCount == 1
                    && aggregate.BranchCount == applicable.Length
                    && aggregate.MaximumBatchMassGrams == expectedMaximum,
                "Crop contributor maximum mass drifted for " + plot.name);
            ProductionFacilityOutputCapacityContribution repeat = new
                CropHarvestFacilityOutputCapacityContributor(
                    economy,
                    definitions,
                    new ICropHarvestReachableMaximumWitnessContributor[]
                    {
                        new NaturalGoldenHarvestReachableMaximumWitnessContributor(
                            definitions,
                            new CharacterPerformanceFormulaCatalog(definitions))
                    }).Capture(subject);
            Require(string.Equals(
                    captured.SourceDigest,
                    repeat.SourceDigest,
                    StringComparison.Ordinal),
                "Crop contributor digest is not deterministic for " + plot.name);

            foreach (CropDefinitionSO crop in applicable)
            {
                CropGenomeReachableMaximumWitnessSnapshot genome =
                    genomeWitnesses.Capture(crop.CropId);
                ProductionOutputFactor grandProject = cropPlot.Indoor
                    ? ProductionOutputFactorAuthority
                        .ResolveMaximumGrandProject("crop-indoor")
                    : ProductionOutputFactor.One;
                int harvestQuantity = CropHarvestOutputRules
                    .ResolveHarvestQuantity(
                        crop.Yield,
                        grandProject.Numerator
                            / (float)grandProject.Denominator,
                        reachableWitness.WorkerYieldMultiplier,
                        1f,
                        genome.YieldMultiplier,
                        hasSoilDiagnostics: true);
                long harvestUnitMass = mass.GetDefinitionUnitMass(
                    (ItemDefinitionId)crop.HarvestItemId).Value;
                long seedUnitMass = mass.GetDefinitionUnitMass(
                    (ItemDefinitionId)crop.SeedItemId).Value;
                long batchMass = checked(
                    checked(harvestUnitMass * harvestQuantity)
                    + checked(seedUnitMass * seedQuantityMaximum));
                CanonicalSemanticDigestBuilder digest = new();
                digest.Append("v27-crop-harvest-reachable-envelope@2");
                digest.Append(ProductionFacilityDefinitionIdentity.Resolve(plot));
                digest.Append(workstation.WorkstationTag);
                digest.Append(cropPlot.Indoor);
                digest.Append(crop.CropId);
                digest.Append(crop.HarvestItemId);
                digest.Append(crop.SeedItemId);
                digest.Append(crop.Yield);
                digest.Append(reachableWitness.WitnessId);
                digest.Append(reachableWitness.SourceDigest);
                digest.Append(genome.GenomeId);
                digest.Append(genome.SourceDigest);
                digest.AppendFloat(reachableWitness.WorkerYieldMultiplier);
                digest.AppendFloat(reachableWitness.ReturnedSeedMultiplier);
                digest.Append(grandProject.Numerator);
                digest.Append(grandProject.Denominator);
                ProductionOutputFactor ecology = new(
                    Mathf.RoundToInt(genome.YieldMultiplier * 40f),
                    40L);
                digest.Append(ecology.Numerator);
                digest.Append(ecology.Denominator);
                digest.Append(harvestQuantity);
                digest.Append(harvestUnitMass);
                digest.Append(seedQuantityMaximum);
                digest.Append(seedUnitMass);
                digest.Append(batchMass);
                digest.Append(buffer.physicalOutputBufferCycleCapacity);
                digest.Append(captured.SourceDigest);
                digest.Append(mass.AuthorityRevision);
                envelopeRows.Add(new CropHarvestEnvelopeRow(
                    ProductionFacilityDefinitionIdentity.Resolve(plot),
                    workstation.WorkstationTag,
                    cropPlot.Indoor,
                    crop.CropId,
                    crop.Yield,
                    reachableWitness.WitnessId,
                    reachableWitness.SourceDigest,
                    reachableWitness.WorkerYieldMultiplier,
                    reachableWitness.ReturnedSeedMultiplier,
                    grandProject,
                    ecology,
                    harvestQuantity,
                    crop.HarvestItemId,
                    harvestUnitMass,
                    seedQuantityMaximum,
                    crop.SeedItemId,
                    seedUnitMass,
                    batchMass,
                    buffer.physicalOutputBufferCycleCapacity,
                    checked(batchMass
                        * buffer.physicalOutputBufferCycleCapacity),
                    captured.SourceDigest,
                    digest.ComputeSha256()));
            }
        }
        Require(envelopeRows.Count == 48
                && envelopeRows.Select(value => value.StableKey)
                    .Distinct(StringComparer.Ordinal).Count() == 48,
            "Crop maximum envelope must contain 4 facilities x 12 crops.");
        WriteCropHarvestEnvelopeArtifact(envelopeRows);
    }

    private static void WriteCropHarvestEnvelopeArtifact(
        IReadOnlyList<CropHarvestEnvelopeRow> rows)
    {
        V27BalanceArtifactWriter.WriteIfDifferent(
            CropHarvestEnvelopeArtifactPath,
            stream => WriteCropHarvestEnvelopeCsv(stream, rows));
        string hash = V27BalanceArtifactWriter.ComputeSha256(
            CropHarvestEnvelopeArtifactPath);
        Require(hash.Length == 64
                && hash.All(value => value is >= '0' and <= '9'
                    or >= 'a' and <= 'f'),
            "Crop maximum envelope artifact hash is invalid.");
    }

    private static void WriteCropHarvestEnvelopeCsv(
        Stream stream,
        IReadOnlyList<CropHarvestEnvelopeRow> rows)
    {
        using StreamWriter writer = new(
            stream,
            new UTF8Encoding(false, true),
            16_384,
            leaveOpen: true);
        WriteCropCsvRow(writer, new[]
        {
            "schemaVersion",
            "facilityDefinitionId",
            "workstationTag",
            "indoor",
            "cropId",
            "baseYield",
            "witnessId",
            "witnessSourceDigest",
            "workerYieldMultiplier",
            "returnedSeedMultiplier",
            "grandProjectNumerator",
            "grandProjectDenominator",
            "ecologyNumerator",
            "ecologyDenominator",
            "soilNumerator",
            "soilDenominator",
            "harvestQuantityMaximum",
            "harvestItemId",
            "harvestUnitMassGrams",
            "returnedSeedQuantityMaximum",
            "seedItemId",
            "seedUnitMassGrams",
            "maximumBatchMassGrams",
            "cycleCapacity",
            "portfolioCapacityGrams",
            "contributorSourceDigest",
            "sourceDigest"
        });
        foreach (CropHarvestEnvelopeRow row in rows
                     .OrderBy(value => value.FacilityDefinitionId,
                         StringComparer.Ordinal)
                     .ThenBy(value => value.CropId, StringComparer.Ordinal))
        {
            WriteCropCsvRow(writer, new[]
            {
                "v27-crop-harvest-reachable-envelope@2",
                row.FacilityDefinitionId,
                row.WorkstationTag,
                row.Indoor ? "true" : "false",
                row.CropId,
                row.BaseYield.ToString(CultureInfo.InvariantCulture),
                row.WitnessId,
                row.WitnessSourceDigest,
                row.WorkerYieldMultiplier.ToString("R",
                    CultureInfo.InvariantCulture),
                row.ReturnedSeedMultiplier.ToString("R",
                    CultureInfo.InvariantCulture),
                row.GrandProject.Numerator.ToString(CultureInfo.InvariantCulture),
                row.GrandProject.Denominator.ToString(CultureInfo.InvariantCulture),
                row.Ecology.Numerator.ToString(
                    CultureInfo.InvariantCulture),
                row.Ecology.Denominator.ToString(
                    CultureInfo.InvariantCulture),
                CropHarvestOutputRules.SoilDiagnosticsMaximum.Numerator.ToString(
                    CultureInfo.InvariantCulture),
                CropHarvestOutputRules.SoilDiagnosticsMaximum.Denominator.ToString(
                    CultureInfo.InvariantCulture),
                row.HarvestQuantityMaximum.ToString(CultureInfo.InvariantCulture),
                row.HarvestItemId,
                row.HarvestUnitMassGrams.ToString(CultureInfo.InvariantCulture),
                row.ReturnedSeedQuantityMaximum.ToString(
                    CultureInfo.InvariantCulture),
                row.SeedItemId,
                row.SeedUnitMassGrams.ToString(CultureInfo.InvariantCulture),
                row.MaximumBatchMassGrams.ToString(CultureInfo.InvariantCulture),
                row.CycleCapacity.ToString(CultureInfo.InvariantCulture),
                row.PortfolioCapacityGrams.ToString(CultureInfo.InvariantCulture),
                row.ContributorSourceDigest,
                row.SourceDigest
            });
        }
        writer.Flush();
    }

    private static void WriteCropCsvRow(StreamWriter writer, string[] fields)
    {
        for (int index = 0; index < fields.Length; index++)
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

    private static void VerifyCombatCraftPrimaryContributor()
    {
        AssetContentSource definitions = new();
        ResourceCombatEquipmentCatalog equipment = new(definitions);
        CombatCraftDefinitionCatalog crafts = new(equipment);
        CombatCraftFacilityEligibilityQuery eligibility = new(
            definitions,
            crafts);
        ResourceEconomyContentCatalog economy = new(
            LoadAll<ResourceItemDefinitionSO>("Assets/Resources/SO/Economy/Items"),
            LoadAll<ProductionRecipeSO>("Assets/Resources/SO/Economy/Recipes"),
            LoadAll<CropDefinitionSO>("Assets/Resources/SO/Economy/Crops"),
            LoadAll<CraftMaterialDefinitionSO>("Assets/Resources/SO/Economy/Materials"));
        IPhysicalItemMassQuery mass = new PhysicalItemMassQuery(
            EditorItemCatalogFactory.Create());
        GameplayEffectResultBoundsCatalog bounds = new(definitions);
        CombatRejectedRecoveryProjector recovery = new(
            equipment,
            economy,
            new V23MaterialSalvageCalculator(
                new ResourceMaterialEconomicProfileCatalog(definitions)),
            mass,
            bounds);
        CombatCraftFacilityOutputCapacityContributor contributor = new(
            eligibility,
            recovery);
        BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Modular/S08_대장작업대.asset");
        Require(definition != null,
            "Authored S08 combat crafting facility is missing.");
        CombatCraftFacilityEligibilitySnapshot facility =
            CombatCraftFacilityEligibility.Capture(definition, crafts);
        Require(crafts.All.Count == 63
            && facility.CraftDefinitions.Count == 63
            && facility.CraftDefinitions.Count(value => value.Kind
                == CombatCraftOutputKind.UniqueEquipment) == 61
            && facility.CraftDefinitions.Count(value => value.Kind
                == CombatCraftOutputKind.GenericAmmunition) == 2
            && facility.OutputBufferCycleCapacity == 4,
            "Authored S08 61-equipment/2-ammunition allowlist drifted.");
        ExpectThrows<InvalidOperationException>(() =>
            CombatCraftAllowlist.Capture(Array.Empty<string>()));
        ExpectThrows<InvalidOperationException>(() =>
            CombatCraftAllowlist.Capture(new[] { "weapon:dagger", " weapon:axe" }));
        ExpectThrows<InvalidOperationException>(() =>
            CombatCraftAllowlist.Capture(new[] { "weapon:dagger", "weapon:dagger" }));
        Require(!crafts.TryGetExact(" weapon:dagger", out _),
            "Combat craft catalog silently normalized a noncanonical ID.");

        BuildingProductionWorkstationAbility workstation =
            definition.GetProductionWorkstationAbility();
        BuildingProductionBufferAbility buffer =
            definition.GetProductionBufferAbility();
        ProductionFacilityCapacitySubject subject = new(
            (BuildingInstanceId)"building:qa:combat-craft-contributor",
            Vector2Int.zero,
            ProductionFacilityDefinitionIdentity.Resolve(definition),
            workstation.WorkstationTag,
            buffer.physicalOutputBufferCycleCapacity,
            ProductionFacilityCapacitySubjectAdapter
                .CaptureWorkstationLaneProfile(definition),
            ProductionFacilityCapacitySubjectAdapter.CaptureProcessFluidProfile(
                definition));
        ProductionFacilityOutputCapacityContribution captured =
            contributor.Capture(subject);
        int primaryBranchCount = captured.Branches.Count(value =>
            value.BranchId.StartsWith(
                "combat-craft-primary:", StringComparison.Ordinal));
        int recoveryBranchCount = captured.Branches.Count(value =>
            value.BranchId.StartsWith(
                "combat-craft-recovery:", StringComparison.Ordinal));
        int distinctBranchCount = captured.Branches.Select(value => value.BranchId)
            .Distinct(StringComparer.Ordinal).Count();
        Require(captured.AppliesToFacility
            && captured.Branches.Count == 295
            && distinctBranchCount == 295
            && primaryBranchCount == 63
            && recoveryBranchCount == 232,
            $"Combat craft primary/recovery contributor branch census drifted: total={captured.Branches.Count}, distinct={distinctBranchCount}, primary={primaryBranchCount}, recovery={recoveryBranchCount}.");
        ExpectThrows<InvalidOperationException>(() => eligibility.TryCapture(
            new ProductionFacilityCapacitySubject(
                (BuildingInstanceId)"building:qa:combat-subject-drift",
                Vector2Int.zero,
                subject.DefinitionId,
                "workstation:qa:wrong",
                subject.OutputBufferCycleCapacity,
                subject.WorkstationLaneProfile,
                subject.ProcessFluidProfile),
            out _));

        CombatRejectedRecoveryProjection[] recoveryBranches = equipment.All
            .OrderBy(value => value.EquipmentId, StringComparer.Ordinal)
            .SelectMany(value => recovery.CaptureDefinitionMaximums(
                value.EquipmentId))
            .ToArray();
        Require(bounds.RequireFiniteMaximum(
                GameplayEffectTargetIds.SalvageYield) == 10f
            && equipment.All.Count == 61
            && economy.Materials.Count == 12
            && recoveryBranches.Length == 252
            && recoveryBranches.Count(value => value.Outputs.Count == 0) == 20
            && recoveryBranches.Count(value => value.Outputs.Count > 0) == 232,
            "Combat rejected-recovery authored census/effect bound drifted.");
        CombatRejectedRecoveryProjection recoveryWinner = recoveryBranches
            .OrderByDescending(value => value.ClampedOutputMassGrams)
            .ThenBy(value => value.CraftDefinitionId, StringComparer.Ordinal)
            .ThenBy(value => value.MaterialId, StringComparer.Ordinal)
            .First();
        Require(string.Equals(
                recoveryWinner.CraftDefinitionId,
                "armor:powered-harness",
                StringComparison.Ordinal)
            && recoveryWinner.InputEquipmentMassGrams == 18_000L
            && recoveryWinner.ClampedOutputMassGrams == 17_700L
            && recoveryWinner.DeclaredLossMassGrams == 300L,
            "Combat rejected-recovery maximum is not the mass-conserving 17,700g powered harness branch.");
        CombatRejectedRecoveryProjection blacksteel = recoveryBranches.Single(
            value => string.Equals(
                    value.CraftDefinitionId,
                    "armor:powered-harness",
                    StringComparison.Ordinal)
                && value.Outputs.Any(output => string.Equals(
                    output.ItemId,
                    "material:blacksteel-ingot",
                    StringComparison.Ordinal)));
        Require(blacksteel.DesiredOutputMassGrams == 136_000L
            && blacksteel.ClampedOutputMassGrams == 17_700L
            && blacksteel.Outputs.Select(value => (value.ItemId, value.Quantity))
                .SequenceEqual(new[]
                {
                    ("component:machine-parts", 2),
                    ("component:powered-armor-joint", 1),
                    ("component:precision-parts", 1),
                    ("material:blacksteel-ingot", 9)
                }),
            "Powered-harness blacksteel recovery vector drifted from the deterministic mass clamp.");
        CombatRejectedRecoveryProjection gold = recoveryBranches.Single(
            value => string.Equals(
                    value.CraftDefinitionId,
                    "armor:powered-harness",
                    StringComparison.Ordinal)
                && value.DesiredOutputMassGrams == 160_500L);
        Require(gold.ClampedOutputMassGrams <= gold.InputEquipmentMassGrams
            && recoveryBranches.All(value =>
                value.ClampedOutputMassGrams <= value.InputEquipmentMassGrams)
            && string.Equals(
                blacksteel.SourceDigest,
                recovery.ProjectDefinitionMaximum(
                    blacksteel.CraftDefinitionId,
                    blacksteel.MaterialId).SourceDigest,
                StringComparison.Ordinal),
            "Combat rejected recovery is non-conserving or non-deterministic.");
        ProductionPreparedOutputComponentCodec codec = new();
        ProductionOutputMaximumMassRegistry maximumMass = new(
            new IProductionOutputMaximumMassCapability[]
            {
                new StandardDefinitionProductionOutputCapability(economy, codec),
                new CombatEquipmentCraftOutputCapability(equipment),
                new CombatAmmunitionCraftOutputCapability(
                    new ResourceItemDefinitionCatalog(
                        definitions.GetAll<ItemDefinitionSO>()))
            },
            mass);
        ProductionFacilityOutputCapacityContributorRegistry contributors = new(
            new IProductionFacilityOutputCapacityContributor[] { contributor },
            maximumMass);
        ProductionFacilityOutputCapacityAggregateSnapshot aggregate =
            contributors.Capture(subject);
        Require(aggregate.ApplicableContributorCount == 1
            && aggregate.BranchCount == 295
            && aggregate.MaximumBatchMassGrams == 18_000L
            && string.Equals(
                aggregate.WinningBranchId,
                "combat-craft-primary:armor:powered-harness",
                StringComparison.Ordinal),
            "Combat craft primary maximum is not the 18kg powered harness.");

        ProductionMaximumOutputFactorCatalog factors = new(
            LoadAll<BuildingSO>("Assets/Resources/SO/Building"));
        ProductionOutputBufferCapacityProjector projector = new(
            economy,
            factors,
            codec,
            mass,
            _ => buffer.physicalOutputBufferCycleCapacity,
            (_, recipe) => string.Equals(
                recipe.WorkstationTag,
                workstation.WorkstationTag,
                StringComparison.Ordinal),
            maximumMass.CaptureAutomatic,
            maximumMass.CaptureDeclared,
            contributors);
        ProductionOutputBufferCapacitySourceSnapshot source =
            projector.CapturePortfolioSource(subject);
        Require(source.MaximumBatchMassGrams == 18_000L
            && source.ProjectedPortfolioCapacityGrams == 72_000L,
            "Authored S08 primary no-bill envelope is not 72,000g.");

        Require(CombatAmmunitionCraftDefinitions.TryGetExact(
                CombatItemDefinitions.ArrowBundleRecipeId,
                out var arrow)
            && arrow.OutputQuantity == 20
            && arrow.FixedInputs.Count == 2
            && CombatAmmunitionCraftDefinitions.TryGetExact(
                CombatItemDefinitions.BoltBundleRecipeId,
                out var bolt)
            && bolt.OutputQuantity == 12
            && bolt.FixedInputs.Count == 2,
            "Combat ammunition output/BOM catalog drifted.");
    }

    private static void VerifySurgicalRecipeBackedPreprojection()
    {
        ResourceEconomyContentCatalog economy = new(
            LoadAll<ResourceItemDefinitionSO>("Assets/Resources/SO/Economy/Items"),
            LoadAll<ProductionRecipeSO>("Assets/Resources/SO/Economy/Recipes"),
            LoadAll<CropDefinitionSO>("Assets/Resources/SO/Economy/Crops"),
            LoadAll<CraftMaterialDefinitionSO>("Assets/Resources/SO/Economy/Materials"));
        ProductionRecipeSO[] recipes = economy.Recipes
            .Where(value => value != null
                && string.Equals(
                    value.WorkstationTag,
                    "m06",
                    StringComparison.Ordinal))
            .OrderBy(value => value.RecipeId, StringComparer.Ordinal)
            .ToArray();
        SurgicalPartProductionOutputMaximumMassCapability surgical = new();
        Require(recipes.Length == 3
            && recipes.All(value => value.ProcessKind
                == ProductionProcessKind.WorkOnly)
            && recipes.All(value => value.Outputs.Count == 1)
            && recipes.SelectMany(value => value.Outputs).All(value =>
                value.Role == ProductionOutputRole.Main
                && value.Amount == 1
                && Math.Abs(value.Probability - 1f) < 0.0001f
                && surgical.CanHandle(value.ItemId)),
            "M06 surgical recipe reachability drifted from the three recipe-backed outputs.");

        BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/Medical/M06_보철조립대.asset");
        BuildingProductionWorkstationAbility workstation =
            definition?.GetProductionWorkstationAbility();
        BuildingProductionBufferAbility buffer =
            definition?.GetProductionBufferAbility();
        Require(definition != null
            && workstation != null
            && buffer != null
            && string.Equals(
                workstation.WorkstationTag,
                "m06",
                StringComparison.Ordinal)
            && buffer.physicalOutputBufferCycleCapacity == 4,
            "Authored M06 workstation/buffer authority drifted.");

        IPhysicalItemMassQuery mass = new PhysicalItemMassQuery(
            EditorItemCatalogFactory.Create());
        ProductionPreparedOutputComponentCodec codec = new();
        ProductionOutputMaximumMassRegistry maximumMass = new(
            new IProductionOutputMaximumMassCapability[]
            {
                new StandardDefinitionProductionOutputCapability(economy, codec),
                surgical
            },
            mass);
        ProductionMaximumOutputFactorCatalog factors = new(
            LoadAll<BuildingSO>("Assets/Resources/SO/Building"));
        ProductionOutputBufferCapacityProjector projector = new(
            economy,
            factors,
            codec,
            mass,
            _ => buffer.physicalOutputBufferCycleCapacity,
            (_, recipe) => string.Equals(
                recipe.WorkstationTag,
                workstation.WorkstationTag,
                StringComparison.Ordinal),
            maximumMass.CaptureAutomatic,
            maximumMass.CaptureDeclared,
            EmptyProductionFacilityOutputCapacityContributorRegistry.Instance,
            clearanceProfiles:
                new ProductionOutputClearanceNaturalBootstrapProfileSource());
        ProductionFacilityCapacitySubject subject = new(
            (BuildingInstanceId)"building:qa:m06-recipe-backed",
            Vector2Int.zero,
            ProductionFacilityDefinitionIdentity.Resolve(definition),
            workstation.WorkstationTag,
            buffer.physicalOutputBufferCycleCapacity,
            ProductionFacilityCapacitySubjectAdapter
                .CaptureWorkstationLaneProfile(definition),
            ProductionFacilityCapacitySubjectAdapter.CaptureProcessFluidProfile(
                definition));
        ProductionOutputBufferCapacitySourceSnapshot source =
            projector.CapturePortfolioSource(subject);
        Require(source.MaximumBatchMassGrams == 1_800L
            && source.ProjectedPortfolioCapacityGrams == 7_200L,
            "M06 generic recipe preprojection no longer proves 7,200g without a contributor.");
    }

    private static void VerifyAuthoredApparelContributor()
    {
        AssetContentSource definitions = new();
        ResourceApparelDefinitionCatalog apparel = new(definitions);
        ResourceTextileMaterialCatalog materials = new(definitions);
        GameplayEffectResultBoundsCatalog bounds = new(definitions);
        IPhysicalItemMassQuery mass = new PhysicalItemMassQuery(
            EditorItemCatalogFactory.Create());
        ResourceEconomyContentCatalog economy = new(
            LoadAll<ResourceItemDefinitionSO>("Assets/Resources/SO/Economy/Items"),
            LoadAll<ProductionRecipeSO>("Assets/Resources/SO/Economy/Recipes"),
            LoadAll<CropDefinitionSO>("Assets/Resources/SO/Economy/Crops"),
            LoadAll<CraftMaterialDefinitionSO>("Assets/Resources/SO/Economy/Materials"));
        ProductionPreparedOutputComponentCodec codec = new();
        ProductionOutputMaximumMassRegistry maximumMass = new(
            new IProductionOutputMaximumMassCapability[]
            {
                new StandardDefinitionProductionOutputCapability(economy, codec),
                new ApparelWorkOrderOutputCapability(apparel)
            },
            mass);
        ApparelFacilityOutputCapacityContributor contributor = new(
            apparel,
            materials,
            mass,
            bounds);
        ProductionFacilityOutputCapacityContributorRegistry contributors = new(
            new IProductionFacilityOutputCapacityContributor[] { contributor },
            maximumMass);

        BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/V22Apparel/"
            + "V22_9301_재단_재봉_작업대.asset");
        Require(definition != null
            && ApparelTailoringFacilityEligibility.IsEligible(definition),
            "Authored tailoring bench is not eligible through the shared predicate.");
        BuildingProductionWorkstationAbility workstation =
            definition.GetProductionWorkstationAbility();
        BuildingProductionBufferAbility buffer =
            definition.GetProductionBufferAbility();
        Require(buffer.physicalOutputBufferCycleCapacity == 4,
            "Authored tailoring bench cycle capacity drifted.");
        ProductionFacilityCapacitySubject subject = new(
            (BuildingInstanceId)"building:qa:apparel-contributor",
            Vector2Int.zero,
            ProductionFacilityDefinitionIdentity.Resolve(definition),
            workstation.WorkstationTag,
            buffer.physicalOutputBufferCycleCapacity,
            ProductionFacilityCapacitySubjectAdapter
                .CaptureWorkstationLaneProfile(definition),
            ProductionFacilityCapacitySubjectAdapter.CaptureProcessFluidProfile(
                definition));
        ProductionFacilityOutputCapacityContribution captured =
            contributor.Capture(subject);
        Require(apparel.Definitions.Count == 56
            && materials.Definitions.Count == 12
            && captured.Branches.Count == 549,
            "Authored apparel/material branch census drifted: apparel="
            + apparel.Definitions.Count
            + "; materials=" + materials.Definitions.Count
            + "; branches=" + captured.Branches.Count + ".");
        Require(captured.Branches.Select(value => value.BranchId)
                .Distinct(StringComparer.Ordinal).Count() == 549
            && captured.Branches.SelectMany(value => value.Outputs)
                .All(value => value.OutputLineId
                    is ApparelPhysicalTransaction.OutputLineId
                    or ApparelPhysicalTransaction.RejectedRecoveryOutputLineId),
            "Apparel contributor emitted duplicate or non-authoritative output lines.");

        ProductionFacilityOutputCapacityAggregateSnapshot aggregate =
            contributors.Capture(subject);
        Require(aggregate.ApplicableContributorCount == 1
            && aggregate.BranchCount == 549
            && aggregate.MaximumBatchMassGrams == 1_380L
            && string.Equals(
                aggregate.WinningContributorId,
                ApparelFacilityOutputCapacityContributor.Id,
                StringComparison.Ordinal),
            "Authored apparel no-bill maximum envelope drifted.");

        ProductionMaximumOutputFactorCatalog factors = new(
            LoadAll<BuildingSO>("Assets/Resources/SO/Building"));
        ProductionOutputBufferCapacityProjector projector = new(
            economy,
            factors,
            codec,
            mass,
            _ => buffer.physicalOutputBufferCycleCapacity,
            (_, recipe) => string.Equals(
                recipe.WorkstationTag,
                workstation.WorkstationTag,
                StringComparison.Ordinal),
            maximumMass.CaptureAutomatic,
            maximumMass.CaptureDeclared,
            contributors);
        ProductionOutputBufferCapacitySourceSnapshot source =
            projector.CapturePortfolioSource(subject);
        Require(source.MaximumBatchMassGrams == 1_380L
            && source.ProjectedPortfolioCapacityGrams == 5_520L,
            "Authored tailoring portfolio did not preserve the 5,520g envelope.");

        ProductionFacilityOutputCapacityContribution repeat = new
            ApparelFacilityOutputCapacityContributor(
                apparel,
                materials,
                mass,
                bounds).Capture(subject);
        Require(string.Equals(
                captured.SourceDigest,
                repeat.SourceDigest,
                StringComparison.Ordinal),
            "Apparel contributor source digest is not deterministic.");
    }

    private static void VerifyAuthoredCertifiedSeedContributor()
    {
        ResourceEconomyContentCatalog content = new(
            LoadAll<ResourceItemDefinitionSO>("Assets/Resources/SO/Economy/Items"),
            LoadAll<ProductionRecipeSO>("Assets/Resources/SO/Economy/Recipes"),
            LoadAll<CropDefinitionSO>("Assets/Resources/SO/Economy/Crops"),
            LoadAll<CraftMaterialDefinitionSO>("Assets/Resources/SO/Economy/Materials"));
        IPhysicalItemMassQuery mass = new PhysicalItemMassQuery(
            EditorItemCatalogFactory.Create());
        ProductionPreparedOutputComponentCodec codec = new();
        ProductionOutputMaximumMassRegistry maximumMass = new(
            new IProductionOutputMaximumMassCapability[]
            {
                new StandardDefinitionProductionOutputCapability(content, codec),
                new CertifiedSeedOutputCapability(content)
            },
            mass);
        ProductionFacilityOutputCapacityContributorRegistry contributors = new(
            new IProductionFacilityOutputCapacityContributor[]
            {
                new CertifiedSeedFacilityOutputCapacityContributor(content)
            },
            maximumMass);
        BuildingSO definition = AssetDatabase.LoadAssetAtPath<BuildingSO>(
            "Assets/Resources/SO/Building/ResearchOverhaul/RF93_육종_온실.asset");
        Require(definition != null
            && CertifiedSeedFacilityEligibility.IsEligible(definition),
            "Authored RF93 is not eligible through the shared capability predicate.");
        BuildingProductionWorkstationAbility workstation =
            definition.GetProductionWorkstationAbility();
        BuildingProductionBufferAbility buffer =
            definition.GetProductionBufferAbility();
        ProductionFacilityCapacitySubject subject = new(
            (BuildingInstanceId)"building:qa:rf93-contributor",
            Vector2Int.zero,
            ProductionFacilityDefinitionIdentity.Resolve(definition),
            workstation.WorkstationTag,
            buffer.physicalOutputBufferCycleCapacity,
            ProductionFacilityCapacitySubjectAdapter
                .CaptureWorkstationLaneProfile(definition),
            ProductionFacilityCapacitySubjectAdapter.CaptureProcessFluidProfile(
                definition));
        ProductionFacilityOutputCapacityAggregateSnapshot aggregate =
            contributors.Capture(subject);
        long expected = content.Crops
            .Where(value => value != null)
            .Max(value => mass.GetDefinitionUnitMass(
                (ItemDefinitionId)value.SeedItemId).Value);
        Require(aggregate.ApplicableContributorCount == 1
            && aggregate.BranchCount == content.Crops.Count
            && aggregate.MaximumBatchMassGrams == expected
            && string.Equals(
                aggregate.WinningContributorId,
                CertifiedSeedFacilityOutputCapacityContributor.Id,
                StringComparison.Ordinal),
            "Authored certified-seed no-bill maximum envelope drifted.");

        ProductionMaximumOutputFactorCatalog factors = new(
            LoadAll<BuildingSO>("Assets/Resources/SO/Building"));
        ProductionOutputBufferCapacityProjector projector = new(
            content,
            factors,
            codec,
            mass,
            _ => buffer.physicalOutputBufferCycleCapacity,
            (_, recipe) => string.Equals(
                recipe.WorkstationTag,
                workstation.WorkstationTag,
                StringComparison.Ordinal),
            maximumMass.CaptureAutomatic,
            maximumMass.CaptureDeclared,
            contributors);
        ProductionOutputBufferCapacitySourceSnapshot source =
            projector.CapturePortfolioSource(subject);
        Require(source.MaximumBatchMassGrams >= expected
            && source.ProjectedPortfolioCapacityGrams
                == checked(source.MaximumBatchMassGrams
                    * buffer.physicalOutputBufferCycleCapacity),
            "Authored RF93 portfolio did not include the certified-seed no-bill envelope.");
    }

    private static T[] LoadAll<T>(string root) where T : UnityEngine.Object =>
        AssetDatabase.FindAssets("t:" + typeof(T).Name, new[] { root })
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<T>)
            .Where(value => value != null)
            .ToArray();

    private readonly struct CropHarvestEnvelopeRow
    {
        internal CropHarvestEnvelopeRow(
            string facilityDefinitionId,
            string workstationTag,
            bool indoor,
            string cropId,
            int baseYield,
            string witnessId,
            string witnessSourceDigest,
            float workerYieldMultiplier,
            float returnedSeedMultiplier,
            ProductionOutputFactor grandProject,
            ProductionOutputFactor ecology,
            int harvestQuantityMaximum,
            string harvestItemId,
            long harvestUnitMassGrams,
            int returnedSeedQuantityMaximum,
            string seedItemId,
            long seedUnitMassGrams,
            long maximumBatchMassGrams,
            int cycleCapacity,
            long portfolioCapacityGrams,
            string contributorSourceDigest,
            string sourceDigest)
        {
            FacilityDefinitionId = facilityDefinitionId;
            WorkstationTag = workstationTag;
            Indoor = indoor;
            CropId = cropId;
            BaseYield = baseYield;
            WitnessId = witnessId;
            WitnessSourceDigest = witnessSourceDigest;
            WorkerYieldMultiplier = workerYieldMultiplier;
            ReturnedSeedMultiplier = returnedSeedMultiplier;
            GrandProject = grandProject;
            Ecology = ecology;
            HarvestQuantityMaximum = harvestQuantityMaximum;
            HarvestItemId = harvestItemId;
            HarvestUnitMassGrams = harvestUnitMassGrams;
            ReturnedSeedQuantityMaximum = returnedSeedQuantityMaximum;
            SeedItemId = seedItemId;
            SeedUnitMassGrams = seedUnitMassGrams;
            MaximumBatchMassGrams = maximumBatchMassGrams;
            CycleCapacity = cycleCapacity;
            PortfolioCapacityGrams = portfolioCapacityGrams;
            ContributorSourceDigest = contributorSourceDigest;
            SourceDigest = sourceDigest;
        }

        internal string FacilityDefinitionId { get; }
        internal string WorkstationTag { get; }
        internal bool Indoor { get; }
        internal string CropId { get; }
        internal int BaseYield { get; }
        internal string WitnessId { get; }
        internal string WitnessSourceDigest { get; }
        internal float WorkerYieldMultiplier { get; }
        internal float ReturnedSeedMultiplier { get; }
        internal ProductionOutputFactor GrandProject { get; }
        internal ProductionOutputFactor Ecology { get; }
        internal int HarvestQuantityMaximum { get; }
        internal string HarvestItemId { get; }
        internal long HarvestUnitMassGrams { get; }
        internal int ReturnedSeedQuantityMaximum { get; }
        internal string SeedItemId { get; }
        internal long SeedUnitMassGrams { get; }
        internal long MaximumBatchMassGrams { get; }
        internal int CycleCapacity { get; }
        internal long PortfolioCapacityGrams { get; }
        internal string ContributorSourceDigest { get; }
        internal string SourceDigest { get; }
        internal string StableKey => FacilityDefinitionId + "|" + CropId;
    }

    private sealed class AssetContentSource : IGameContentDefinitionSource
    {
        public IReadOnlyList<T> GetAll<T>() where T : ScriptableObject =>
            AssetDatabase.FindAssets("t:" + typeof(T).Name,
                    new[] { "Assets/Resources/SO" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<T>)
                .Where(value => value != null)
                .OrderBy(value => AssetDatabase.GetAssetPath(value),
                    StringComparer.Ordinal)
                .ToArray();

        public T RequireSingle<T>() where T : ScriptableObject =>
            GetAll<T>().Single();
    }

    private static ProductionFacilityOutputMaximumMassRequest Request(
        string line,
        string item) => new(line, item, "capability:qa", 1);

    private static ProductionFacilityCapacitySubject Subject() => new(
        (BuildingInstanceId)"building:qa:contributor",
        Vector2Int.zero,
        "building-definition:qa:contributor",
        "workstation:qa:contributor",
        4,
        new ProductionFacilityWorkstationLaneCapacityProfile(
            ProductionWorkstationLanePolicy
                .ManualWithDetachedBatchProcessors,
            1,
            0),
        ProductionFacilityProcessFluidCapacityProfile.Empty);

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void ExpectThrows<T>(Action action) where T : Exception
    {
        try
        {
            action();
        }
        catch (T)
        {
            return;
        }
        throw new InvalidOperationException("Expected " + typeof(T).Name + ".");
    }

    private sealed class FixedContributor :
        IProductionFacilityOutputCapacityContributor
    {
        private readonly ProductionFacilityOutputCapacityBranch[] branches;

        internal FixedContributor(
            string contributorId,
            params ProductionFacilityOutputCapacityBranch[] branches)
        {
            ContributorId = contributorId;
            this.branches = branches;
        }

        public string ContributorId { get; }
        public int ContractVersion => 1;

        public ProductionFacilityOutputCapacityContribution Capture(
            ProductionFacilityCapacitySubject subject) => new(
            ContributorId,
            ContractVersion,
            true,
            branches);
    }

    private sealed class FixedSelector :
        IProductionOutputMaximumMassCapabilitySelector
    {
        private readonly IReadOnlyDictionary<string, long> masses;
        internal FixedSelector(IReadOnlyDictionary<string, long> masses) =>
            this.masses = masses;

        public ProductionOutputMaximumMassProjection CaptureForCapability(
            string outputLineId,
            string itemId,
            string capabilityId,
            int maximumQuantity)
        {
            long unitMass = masses[itemId];
            const string codecId = "codec:qa:facility-capacity";
            const int version = 1;
            ProductionOutputCapabilityDescriptor descriptor = new(
                outputLineId,
                itemId,
                capabilityId,
                version,
                codecId,
                version,
                ProductionOutputCapabilityDescriptorFingerprint.Capture(
                    outputLineId,
                    itemId,
                    capabilityId,
                    version,
                    codecId,
                    version));
            return new ProductionOutputMaximumMassProjection(
                descriptor,
                maximumQuantity,
                unitMass,
                checked(unitMass * maximumQuantity),
                1L,
                new string('a', 64));
        }
    }
}
