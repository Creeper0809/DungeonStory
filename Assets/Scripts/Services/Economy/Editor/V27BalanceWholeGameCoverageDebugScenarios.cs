#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using DungeonStory.Balance;
using UnityEditor;
using UnityEngine;

public static class V27BalanceWholeGameCoverageDebugScenarios
{
    public const string ReportPath =
        "Artifacts/QA/v27-balance-whole-game-coverage.txt";

    private static readonly string[] RequiredDomains =
    {
        "agriculture",
        "combat",
        "content",
        "defense",
        "economy",
        "facilities",
        "items",
        "labor",
        "medical",
        "offense",
        "production",
        "research"
    };

    private static readonly int[] RequiredDailyRoutineSeeds =
    {
        157181,
        157182,
        157183
    };

    private const double DailyRoutineMeanTolerance = 0.05d;
    private const double DailyRoutineMaximumCv = 0.12d;
    private const double DailyRoutinePerSeedCollapseFloor = 0.80d;

    [MenuItem("DungeonStory/V27/Verify Whole-Game Ledger Coverage")]
    public static void RunFromMenu()
    {
        string report = RunAll(out V27BalanceAuditOutput audit);
        byte[] bytes = new UTF8Encoding(false, true).GetBytes(report);
        V27BalanceArtifactWriter.WriteIfDifferent(
            ReportPath,
            stream => stream.Write(bytes, 0, bytes.Length));
        V27BalanceAudit.RefreshManifestEvidenceHashes(audit);
        AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
        Debug.Log(report);
    }

    public static string RunAll() => RunAll(out _);

    private static string RunAll(out V27BalanceAuditOutput audit)
    {
        string laborMatrix = V27LaborAuthorityMatrixDebugScenarios.RunAll();
        string[] requiredLaborMatrixMarkers =
        {
            "RESULT=PASS; cells=360",
            "PASS V27_LABOR_MATRIX_360_CELLS",
            "PASS V27_LABOR_MATRIX_ACTUAL_EFFECTIVE_RATIO",
            "PASS V27_LABOR_MATRIX_TECH_MONOTONIC",
            "PASS V27_LABOR_MATRIX_SURVIVAL_MONOTONIC",
            "PASS V27_LABOR_MATRIX_GROWTH_CUT_FIRST",
            "PASS V27_LABOR_MATRIX_SHORTAGE_CRISIS_EXPOSED"
        };
        string[] missingLaborMatrixMarkers = requiredLaborMatrixMarkers
            .Where(marker => !laborMatrix.Contains(marker, StringComparison.Ordinal))
            .ToArray();
        if (missingLaborMatrixMarkers.Length > 0)
        {
            throw new InvalidOperationException(
                "V27 labor authority matrix is incomplete: "
                + string.Join(",", missingLaborMatrixMarkers));
        }

        audit = V27BalanceAudit.Generate(
            BalanceLedgerExecutionMode.AuditOnly);
        if (audit.IntegrityFailures.Count != 0 || audit.CriticalCount != 0)
        {
            throw new InvalidOperationException(
                $"V27 coverage requires a clean audit: integrity="
                + $"{audit.IntegrityFailures.Count}; critical={audit.CriticalCount}.");
        }

        (double actualMean, double effectiveMean) =
            RequireFreshDailyRoutineEvidence();
        RequireFreshDungeonExpansionEvidence();
        CapacityEvidenceSummary capacity =
            RequireFreshCapacityAndContinuityEvidence();
        RequireFreshOutputCapacityEvidence();
        V27BalanceBuilderNoClobberDebugScenarios.RequireFreshEvidence();

        IReadOnlyList<CanonicalBalanceMetricRecord> records = audit.Ledger.Records;
        string[] missingDomains = RequiredDomains
            .Where(domain => records.All(record =>
                !string.Equals(record.Domain, domain, StringComparison.Ordinal)))
            .ToArray();
        if (missingDomains.Length > 0)
        {
            throw new InvalidOperationException(
                "V27 ledger is missing domains: " + string.Join(",", missingDomains));
        }

        IReadOnlyList<string> networkFailures =
            BranchedProductionNetworkDebugScenarios.Validate();
        ProductionNetworkCoverageSnapshot network =
            BranchedProductionNetworkDebugScenarios.LastCoverage;
        if (networkFailures.Count != 0
            || network.ProducerOrphanCount != 0
            || network.ConsumerOrphanCount != 0)
        {
            throw new InvalidOperationException(
                "V27 production network coverage failed: "
                + string.Join(" | ", networkFailures));
        }

        int itemDefinitions = records.Count(record =>
            string.Equals(record.DefinitionKind, "item", StringComparison.Ordinal)
            && string.Equals(record.Metric, "acquisition-cost", StringComparison.Ordinal));
        int recipeDefinitions = AssetDatabase.FindAssets("t:ProductionRecipeSO")
            .Select(AssetDatabase.GUIDToAssetPath)
            .Select(AssetDatabase.LoadAssetAtPath<ProductionRecipeSO>)
            .Where(value => value != null)
            .Select(value => value.RecipeId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        int activeBuildingDefinitions = records.Count(record =>
            string.Equals(record.Domain, "facilities", StringComparison.Ordinal)
            && string.Equals(
                record.Metric,
                "construction-authored-wu:redistributed",
                StringComparison.Ordinal));
        int serializedDefinitionCount = records
            .Where(record => string.Equals(
                record.DefinitionKind,
                "serialized-property",
                StringComparison.Ordinal))
            .Select(record => record.StableId)
            .Distinct(StringComparer.Ordinal)
            .Count();
        int approvedButUnapplied = records.Count(record =>
            record.ApprovalKey.Length > 0
            && !string.Equals(record.AssetApplied, "true", StringComparison.Ordinal));
        if (itemDefinitions != 413
            || network.DefinitionCount != 363
            || recipeDefinitions != 354
            || activeBuildingDefinitions != 356
            || approvedButUnapplied != 0)
        {
            throw new InvalidOperationException(
                $"V27 coverage count drift: items={itemDefinitions}; "
                + $"resourceItems={network.DefinitionCount}; recipes={recipeDefinitions}; "
                + $"buildings={activeBuildingDefinitions}; unapplied={approvedButUnapplied}.");
        }

        StringBuilder report = new();
        report.Append("RESULT=PASS; rows=")
            .Append(records.Count.ToString(CultureInfo.InvariantCulture))
            .Append("; domains=")
            .Append(RequiredDomains.Length.ToString(CultureInfo.InvariantCulture))
            .Append("; producerOrphans=0; consumerOrphans=0; approvedUnapplied=0\n");
        report.Append("PASS V27_WHOLE_GAME_SERIALIZED_AUTHORITY definitions=")
            .Append(serializedDefinitionCount.ToString(CultureInfo.InvariantCulture))
            .Append('\n');
        report.Append("PASS V27_WHOLE_GAME_ITEM_DEFINITIONS total=")
            .Append(itemDefinitions.ToString(CultureInfo.InvariantCulture))
            .Append("; resource=")
            .Append(network.DefinitionCount.ToString(CultureInfo.InvariantCulture))
            .Append("; nonResource=")
            .Append((itemDefinitions - network.DefinitionCount).ToString(CultureInfo.InvariantCulture))
            .Append('\n');
        report.Append("PASS V27_WHOLE_GAME_RECIPE_DEFINITIONS total=")
            .Append(recipeDefinitions.ToString(CultureInfo.InvariantCulture))
            .Append("; maxDepth=")
            .Append(network.MaximumRecipeDepth.ToString(CultureInfo.InvariantCulture))
            .Append('\n');
        report.Append("PASS V27_WHOLE_GAME_ACTIVE_BUILDINGS total=")
            .Append(activeBuildingDefinitions.ToString(CultureInfo.InvariantCulture))
            .Append('\n');
        report.Append("PASS V27_WHOLE_GAME_PRODUCER_LINKS links=")
            .Append(network.ProducerLinkCount.ToString(CultureInfo.InvariantCulture))
            .Append("; orphans=0\n");
        report.Append("PASS V27_WHOLE_GAME_CONSUMER_LINKS links=")
            .Append(network.ConsumerLinkCount.ToString(CultureInfo.InvariantCulture))
            .Append("; orphans=0\n");
        report.Append("PASS V27_WHOLE_GAME_EXACT_APPROVAL_APPLICATION unapplied=0\n");
        report.Append("PASS V27_WHOLE_GAME_LABOR_AUTHORITY_MATRIX cells=360\n");
        report.Append("PASS V27_WHOLE_GAME_DAILY_ROUTINE_3_SEEDS actualMean=")
            .Append(actualMean.ToString("0.000000", CultureInfo.InvariantCulture))
            .Append("; effectiveMean=")
            .Append(effectiveMean.ToString("0.000000", CultureInfo.InvariantCulture))
            .Append("\n");
        report.Append("PASS V27_WHOLE_GAME_DUNGEON_EXPANSION research=3; ")
            .Append("columns=27,49,65,81; layouts=1536\n");
        report.Append("PASS V27_WHOLE_GAME_SERVICE_CONTINUITY paths=10; ")
            .Append("populationAuthority=6; liveExecution=productionActorPerPath\n");
        report.Append("PASS V27_WHOLE_GAME_ASSET_BACKED_SPATIAL_CAPACITY layouts=")
            .Append(capacity.AssetBackedLayouts.ToString(CultureInfo.InvariantCulture))
            .Append("; minimumWidths=27,27,27,49,65,81; authoredWidths=27,27,27,49,65,81\n");
        report.Append("PASS V27_WHOLE_GAME_PAIRED_CLUTTER seeds=")
            .Append(capacity.PairedSeeds.ToString(CultureInfo.InvariantCulture))
            .Append("; windows=")
            .Append(capacity.PairedWindows.ToString(CultureInfo.InvariantCulture))
            .Append("; accessEgressClutter=0; rngCrossTalk=0\n");
        report.Append("PASS V27_WHOLE_GAME_OUTPUT_CAPACITY checks=2; ")
            .Append("typedBlock=true; workConserved=true; quantityConserved=true\n");
        foreach (string domain in RequiredDomains)
        {
            int count = records.Count(record =>
                string.Equals(record.Domain, domain, StringComparison.Ordinal));
            report.Append("PASS V27_DOMAIN_ROWS domain=")
                .Append(domain)
                .Append("; rows=")
                .Append(count.ToString(CultureInfo.InvariantCulture))
                .Append('\n');
        }
        return report.ToString();
    }

    private static void RequireFreshOutputCapacityEvidence()
    {
        string path = Path.GetFullPath(
            V27OutputCapacityEvidenceDebugScenarios.ReportPath);
        if (!File.Exists(path))
            throw new InvalidOperationException(
                "V27 output-capacity PlayMode evidence is missing.");
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        string[] exactLines =
        {
            "RESULT=PASS; checks=2; failures=0",
            "sourceDigest=" + V27OutputCapacityEvidenceDebugScenarios
                .ComputeEvidenceSourceDigest()
        };
        if (exactLines.Any(expected =>
                !lines.Contains(expected, StringComparer.Ordinal))
            || !lines.Any(line => line.StartsWith(
                "PASS OUTPUT_CONTAINMENT_TYPED_BLOCK_RECOVERY ",
                StringComparison.Ordinal))
            || !lines.Any(line => line.StartsWith(
                "PASS CROP_OUTPUT_CONTAINMENT_TYPED_BLOCK_RECOVERY ",
                StringComparison.Ordinal)))
        {
            throw new InvalidOperationException(
                "V27 output-capacity PlayMode evidence is stale or incomplete.");
        }
    }

    private static (double ActualMean, double EffectiveMean)
        RequireFreshDailyRoutineEvidence()
    {
        string[] sourcePaths =
        {
            "Assets/Scripts/Services/Character/Work/Editor/DailyRoutineWuPlayModeVerifier.cs",
            "Assets/Scripts/Models/Work/SettlementLaborAuthority.cs",
            "Assets/Scripts/Services/Character/AI/CharacterAiDecisionPipeline.cs",
            "Assets/Scripts/Services/Character/Ability/AbilityWork.cs",
            "Assets/Scripts/Services/Character/Work/WorkTaskExecutor.cs",
            "Assets/Scripts/Models/Economy/Content/WorldResourceRuntime.cs",
            "Assets/Scripts/Services/Economy/ProductionItemGateway.cs",
            "Assets/Scripts/Services/Economy/CropEcologyRuntime.cs",
            "Assets/Scripts/Services/Economy/CropPlotRuntime.cs"
        };
        DateTime latestSource = sourcePaths
            .Select(Path.GetFullPath)
            .Select(path => File.Exists(path)
                ? File.GetLastWriteTimeUtc(path)
                : throw new FileNotFoundException(
                    "Daily-routine freshness source is missing.",
                    path))
            .Max();
        List<double> actualSamples = new(RequiredDailyRoutineSeeds.Length);
        List<double> effectiveSamples = new(RequiredDailyRoutineSeeds.Length);
        foreach (int seed in RequiredDailyRoutineSeeds)
        {
            string path = Path.GetFullPath(
                $"Artifacts/QA/phase157-daily-routine-wu-seed-{seed}.txt");
            if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) < latestSource)
            {
                throw new InvalidOperationException(
                    $"Daily-routine evidence is missing or stale: seed={seed}.");
            }
            string[] lines = File.ReadAllLines(path, Encoding.UTF8);
            RequireExactLine(lines, "observedDays=5", seed);
            RequireExactLine(lines, $"runSeed={seed}", seed);
            RequireExactLine(
                lines,
                "runtimeDiagnosticsGate=ai-runtime-gate-v3",
                seed);
            if (!lines.Any(line => line.StartsWith(
                    "RESULT=PASS; failures=0;",
                    StringComparison.Ordinal)
                && line.EndsWith("capturedIssues=0", StringComparison.Ordinal)))
            {
                throw new InvalidOperationException(
                    $"Daily-routine result contract failed: seed={seed}.");
            }
            double days = ParseEvidenceNumber(lines, "observedDays=", seed);
            double actors = ParseEvidenceNumber(lines, "actors=", seed);
            double divisor = checked(days * actors);
            double actual = ParseEvidenceNumber(lines, "actualLaborWU=", seed) / divisor;
            double effective = ParseEvidenceNumber(
                lines,
                "outputEquivalentWU=",
                seed) / divisor;
            if (actual < SettlementLaborAuthority.ActualWuPerAdultDay
                    * DailyRoutinePerSeedCollapseFloor
                || effective < SettlementLaborAuthority.EffectiveOutputWuPerAdultDay
                    * DailyRoutinePerSeedCollapseFloor)
            {
                throw new InvalidOperationException(
                    $"Daily-routine per-seed collapse floor failed: seed={seed}; "
                    + $"actual={actual:R}; effective={effective:R}.");
            }
            actualSamples.Add(actual);
            effectiveSamples.Add(effective);
        }

        double actualMean = actualSamples.Average();
        double effectiveMean = effectiveSamples.Average();
        double actualTarget = SettlementLaborAuthority.ActualWuPerAdultDay;
        double effectiveTarget = SettlementLaborAuthority.EffectiveOutputWuPerAdultDay;
        double actualCv = CoefficientOfVariation(actualSamples, actualMean);
        double effectiveCv = CoefficientOfVariation(effectiveSamples, effectiveMean);
        bool actualMeanInBand = Math.Abs(actualMean - actualTarget)
            <= actualTarget * DailyRoutineMeanTolerance;
        bool effectiveMeanInBand = Math.Abs(effectiveMean - effectiveTarget)
            <= effectiveTarget * DailyRoutineMeanTolerance;
        if (!actualMeanInBand
            || !effectiveMeanInBand
            || actualCv > DailyRoutineMaximumCv
            || effectiveCv > DailyRoutineMaximumCv)
        {
            throw new InvalidOperationException(
                "Daily-routine three-seed authority failed: "
                + $"actualMean={actualMean:R}; effectiveMean={effectiveMean:R}; "
                + $"actualCv={actualCv:R}; effectiveCv={effectiveCv:R}; "
                + $"meanTolerance={DailyRoutineMeanTolerance:R}; "
                + $"maximumCv={DailyRoutineMaximumCv:R}.");
        }

        return (actualMean, effectiveMean);
    }

    private static double CoefficientOfVariation(
        IReadOnlyList<double> samples,
        double mean)
    {
        if (samples == null || samples.Count == 0 || mean <= 0d)
            throw new InvalidOperationException(
                "Daily-routine CV requires positive non-empty samples.");

        double squaredDeviation = 0d;
        for (int index = 0; index < samples.Count; index++)
        {
            double delta = samples[index] - mean;
            squaredDeviation += delta * delta;
        }
        return Math.Sqrt(squaredDeviation / samples.Count) / mean;
    }

    private static void RequireFreshDungeonExpansionEvidence()
    {
        string[] sourcePaths =
        {
            "Assets/Scripts/Services/Infrastructure/DungeonSpaceExpansionRuntime.cs",
            "Assets/Scripts/Services/Infrastructure/Editor/DungeonSpaceExpansionDebugScenarios.cs",
            "Assets/Scripts/Services/Infrastructure/Editor/DungeonSpaceExpansionPlayModeVerifier.cs",
            "Assets/Scripts/Services/Infrastructure/ModularFacilityWorldSaveService.cs",
            "Assets/Scripts/Services/Infrastructure/Save/DungeonAggregateReferencePreflight.cs",
            "Assets/Scripts/Services/Infrastructure/Registration/DungeonProgressionOffenseRegistration.cs",
            "Assets/Scripts/Controllers/Grid/System/GridSystemManager.cs",
            "Assets/Scripts/Models/Grid/Core/Grid.cs",
            "Assets/Scripts/Services/Research/Editor/ResearchProjectAssetBuilder.cs",
            "Assets/Scripts/Services/Economy/Editor/V27PopulationCapacityDebugScenarios.cs",
            "Assets/Resources/SO/Research/Projects/mining_quarry.asset",
            "Assets/Resources/SO/Research/Projects/mining_stonecutting.asset",
            "Assets/Resources/SO/Research/Projects/mining_deep.asset"
        };
        DateTime latestSource = sourcePaths
            .Select(Path.GetFullPath)
            .Select(path => File.Exists(path)
                ? File.GetLastWriteTimeUtc(path)
                : throw new FileNotFoundException(
                    "Dungeon-expansion freshness source is missing.",
                    path))
            .Max();

        RequireFreshEvidence(
            "Artifacts/QA/v27-balance-expansion-editmode.txt",
            latestSource,
            new[]
            {
                "RESULT=PASS; failures=0; research=3; columns=27,49,65,81;",
                "PASS\tEXPANSION_RESEARCH_ASSETS_EXACT",
                "PASS\tEXPANSION_EVENT_27_49_65_81_EXACT",
                "PASS\tEXPANSION_OUT_OF_ORDER_DEEP_IDEMPOTENT",
                "PASS\tEXPANSION_GRID_COPY_ATOMIC_AND_OCCUPANTS_PRESERVED",
                "PASS\tEXPANSION_SAVE_V5_LAYOUT_ROUNDTRIP_EXACT",
                "PASS\tEXPANSION_SAVE_RESEARCH_LAYOUT_AUTHORITY_EXACT",
                "PASS\tEXPANSION_E_KEY_DEVELOPER_ONLY"
            });
        RequireFreshEvidence(
            "Artifacts/QA/v27-balance-expansion-playmode.txt",
            latestSource,
            new[]
            {
                "RESULT=PASS; failures=0; liveResearchCompletions=3; publications=3;",
                "PASS\tEXPANSION_LIVE_RESEARCH_QUARRY_27_TO_49",
                "PASS\tEXPANSION_LIVE_RESEARCH_STONECUTTING_49_TO_65",
                "PASS\tEXPANSION_LIVE_RESEARCH_DEEP_MINING_65_TO_81",
                "PASS\tEXPANSION_LIVE_EVENT_PUBLICATION_EXACT_ONCE",
                "PASS\tEXPANSION_LIVE_ENTRANCE_AND_COORDINATES_PRESERVED",
                "PASS\tEXPANSION_LIVE_REQUIRED_FACILITIES_PRESERVED_NO_DEMOLITION"
            });

        string layoutPath = Path.GetFullPath(
            "Artifacts/QA/v27-balance-layout-256-seed.txt");
        if (!File.Exists(layoutPath))
        {
            throw new InvalidOperationException(
                "Dungeon-expansion 1536-layout evidence is missing.");
        }
        string layout = File.ReadAllText(layoutPath, Encoding.UTF8);
        string currentLayout = V27PopulationCapacityDebugScenarios
            .RunAll()
            .TrimEnd('\r', '\n');
        if (!string.Equals(
                layout.TrimEnd('\r', '\n'),
                currentLayout,
                StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Dungeon-expansion 1536-layout evidence does not match "
                + "the current-source deterministic recomputation.");
        }
        string[] layoutMarkers =
        {
            "RESULT=PASS; seedsPerStage=256; stages=6; passed=1536;",
            "successRatePermille=1000;",
            "minimumHeadroomPermille=477;",
            "maximumNormalCellUtilizationPermille=440;",
            "maximumFaultCellUtilizationPermille=616;",
            "heuristicFalseNegative=0;",
            "stageColumnsUsedHeadroom=1:27:25:691,3:27:27:666,6:27:42:481,"
                + "12:49:69:530,18:65:97:502,24:81:127:477"
        };
        string[] missing = layoutMarkers
            .Where(marker => !layout.Contains(marker, StringComparison.Ordinal))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                "Dungeon-expansion layout evidence is incomplete: "
                + string.Join(",", missing));
        }
    }

    private static CapacityEvidenceSummary
        RequireFreshCapacityAndContinuityEvidence()
    {
        string[] serviceSources =
        {
            "Assets/Scripts/Services/Economy/Editor/V27ServiceContinuityEvidenceDebugScenarios.cs",
            "Assets/Scripts/Services/Economy/Editor/V27SixAdultSurvivalLoopDebugScenarios.cs",
            "Assets/Scripts/Services/Economy/SurvivalContinuityCatalogQuery.cs",
            "Assets/Scripts/Services/Economy/V27SurvivalClosedLoopModels.cs",
            "Assets/Scripts/Services/Survival/Editor/PrimitiveStartSurvivalPlayModeVerifier.cs",
            "Assets/Scripts/Services/Survival/CharacterPrimitiveSurvivalRunner.cs",
            "Assets/Scripts/Services/Survival/CharacterSafeReliefRunner.cs",
            "Assets/Scripts/Services/Character/AI/Editor/CharacterAiSelfCarePlayModeVerifier.cs"
        };
        DateTime serviceCutoff = LatestSource(serviceSources);
        RequireFreshEvidence(
            V27ServiceContinuityEvidenceDebugScenarios.ReportPath,
            serviceCutoff,
            new[]
            {
                "RESULT=PASS; populationAuthority=6; livePathActorCount=6; outageCoverageHours=24; paths=10; consoleIssues=0",
                "PASS V27_SERVICE_CONTINUITY_STATIC_CLOSED_LOOP population=6; grossFoodPermille=1250;netFoodPermille=1100;reserveDays=7",
                "PASS V27_SERVICE_CONTINUITY_FIELD_MEAL_PRODUCTION_LIVE physicalCost=1",
                "PASS V27_SERVICE_CONTINUITY_SAFE_DRINK_PRODUCTION_LIVE physicalCost=1",
                "PASS V27_SERVICE_CONTINUITY_FLOOR_REST_PRODUCTION_LIVE",
                "PASS V27_SERVICE_CONTINUITY_BUCKET_WASH_PRODUCTION_LIVE physicalCost=1",
                "PASS V27_SERVICE_CONTINUITY_LATRINE_PRODUCTION_LIVE",
                "PASS V27_SERVICE_CONTINUITY_PRIMARY_AND_FALLBACK_PATHS_EXACT count=10",
                "PASS V27_SERVICE_CONTINUITY_EVIDENCE_SCOPE staticPopulationModel=6; liveExecution=sixAdultsFiveSimultaneousOutages"
            });
        RequireFreshEvidencePrefixes(
            PrimitiveStartSurvivalPlayModeVerifier.SixAdultOutageReportPath,
            serviceCutoff,
            new[]
            {
                "PASS V27_SIX_ADULT_OUTAGE_SIX_LIVE_ADULTS:",
                "PASS V27_SIX_ADULT_OUTAGE_CANCEL_NO_RECOVERY_NO_CONSUME:",
                "PASS V27_SIX_ADULT_OUTAGE_ONE_GAME_DAY_ELAPSED:",
                "PASS V27_SIX_ADULT_OUTAGE_ALL_FIVE_FALLBACKS_COMPLETED:",
                "PASS V27_SIX_ADULT_OUTAGE_PHYSICAL_EXACT_NO_MINT:",
                "PASS V27_SIX_ADULT_OUTAGE_NO_DEATH_DOWN_BREAKDOWN:",
                "PASS V27_SIX_ADULT_OUTAGE_PRIMARY_RESTORE_TRANSACTION:",
                "PASS V27_SIX_ADULT_OUTAGE_PRIMARY_FACILITY_IDENTITY_EXACT:",
                "PASS V27_SIX_ADULT_OUTAGE_RECOVERY_PRIMARY_DOMINANCE:",
                "PASS V27_SIX_ADULT_OUTAGE_RECOVERY_PRIMITIVE_STARTS_LE_5_PERCENT:",
                "PASS V27_SIX_ADULT_OUTAGE_RESULT: liveAdults=6;outageHours=24;fallbacks=5;restore=exact;primitiveRecoveryPermille=0;"
            });
        RequireFreshEvidencePrefixes(
            PrimitiveStartSurvivalPlayModeVerifier.PopulationStageReportPath,
            serviceCutoff,
            new[]
            {
                "PASS V27_POPULATION_STAGE_BASELINE_CAPTURED:",
                "PASS V27_POPULATION_STAGE_STORAGE_AUTHORITY:",
                "PASS V27_POPULATION_STAGE_CLOSED_LOOP_1: gross=2800;net=2660;recurring=363;growth=387;emergency=100;disposition=minimum-plot-warning",
                "PASS V27_POPULATION_STAGE_SEVEN_DAY_PHYSICAL_RESERVE_6:",
                "PASS V27_POPULATION_STAGE_FIXED_WORLD_FEATURES_6: interiorNodes=0;reserved=0;cells=",
                "PASS V27_POPULATION_STAGE_RUNTIME_HEADROOM_6: outside=0;fixedWorldFeatures=0;headroomPermille=308;grid=60x3",
                "PASS V27_POPULATION_STAGE_RESEARCH_SPACE_12: columns=49;developerKeyUsed=False",
                "PASS V27_POPULATION_STAGE_FIXED_WORLD_FEATURES_12: interiorNodes=0;reserved=0;cells=",
                "PASS V27_POPULATION_STAGE_RUNTIME_HEADROOM_12: outside=0;fixedWorldFeatures=0;headroomPermille=312;grid=66x3",
                "PASS V27_POPULATION_STAGE_RESEARCH_SPACE_18: columns=65;developerKeyUsed=False",
                "PASS V27_POPULATION_STAGE_FIXED_WORLD_FEATURES_18: interiorNodes=0;reserved=0;cells=",
                "PASS V27_POPULATION_STAGE_RUNTIME_HEADROOM_18: outside=0;fixedWorldFeatures=0;headroomPermille=307;grid=82x3",
                "PASS V27_POPULATION_STAGE_RESEARCH_SPACE_24: columns=81;developerKeyUsed=False",
                "PASS V27_POPULATION_STAGE_FIXED_WORLD_FEATURES_24: interiorNodes=0;reserved=0;cells=",
                "PASS V27_POPULATION_STAGE_RUNTIME_HEADROOM_24: outside=0;fixedWorldFeatures=0;headroomPermille=316;grid=98x3",
                "PASS V27_POPULATION_STAGE_ALL_STAGES_EXACT: populations=1,3,6,12,18,24;physicalReserve=true;researchExpansion=true;developerE=false"
            });

        string spatialSourceDigest =
            V27AssetBackedSpatialCapacityDebugScenarios.CaptureSourceDigest();
        string spatialCsvPath = Path.GetFullPath(
            V27AssetBackedSpatialCapacityDebugScenarios.SpatialCsvPath);
        if (!File.Exists(spatialCsvPath))
            throw new InvalidOperationException(
                "Asset-backed spatial-capacity evidence is missing.");
        string spatialCsvDigest = HashFile(spatialCsvPath);

        RequireDigestEvidence(
            "Artifacts/QA/v27-balance-shared-cell-congestion.txt",
            spatialSourceDigest,
            spatialCsvDigest,
            new[]
            {
                "RESULT=PASS; authority=shared-access-union; normalLimitPermille=700; faultLimitPermille=900",
                "PASS population=24;width=81;normalPeak=600;faultPeak=840;normalStoragePeak=654;faultStoragePeak=476;minimumHeadroom=316"
            });
        RequireDigestEvidence(
            "Artifacts/QA/v27-balance-expansion-tiers.txt",
            spatialSourceDigest,
            spatialCsvDigest,
            new[]
            {
                "RESULT=PASS; authority=asset-backed-capacity; developerEKey=false; automaticPopulationTrigger=false; authoredColumns=27,49,65,81; maxWidth=104",
                "PASS population=6;requiredInteriorColumns=27;authoredTargetInteriorColumns=27;spareColumns=0;researchGate=start",
                "PASS population=12;requiredInteriorColumns=49;authoredTargetInteriorColumns=49;spareColumns=0;researchGate=research:mining:quarry",
                "PASS population=18;requiredInteriorColumns=65;authoredTargetInteriorColumns=65;spareColumns=0;researchGate=research:mining:stonecutting",
                "PASS population=24;requiredInteriorColumns=81;authoredTargetInteriorColumns=81;spareColumns=0;researchGate=research:mining:deep"
            });
        string[] spatialRows = File.ReadAllLines(spatialCsvPath, Encoding.UTF8);
        if (spatialRows.Length != 1537
            || spatialRows.Skip(1).Any(line =>
                !line.Contains(",true,,", StringComparison.Ordinal)))
            throw new InvalidOperationException(
                $"Asset-backed spatial-capacity rows failed: rows={spatialRows.Length - 1}.");

        string pairedReportPath = Path.GetFullPath(
            V27PairedClutterPlayModeVerifier.ReportPath);
        if (!File.Exists(pairedReportPath))
            throw new InvalidOperationException(
                "Paired clutter evidence is missing.");
        string[] pairedReport = File.ReadAllLines(pairedReportPath, Encoding.UTF8);
        string aggregate = pairedReport.FirstOrDefault() ?? string.Empty;
        if (!aggregate.StartsWith("RESULT=PASS; seeds=", StringComparison.Ordinal)
            || !aggregate.Contains("failures=0; consoleIssues=0", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Paired clutter aggregate result failed: " + aggregate);
        string expectedSourceDigest = V27PairedClutterPlayModeVerifier
            .ComputeEvidenceSourceDigest();
        if (!string.Equals(
                ParseAggregateToken(aggregate, "sourceDigest="),
                expectedSourceDigest,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Paired clutter evidence source digest is stale.");
        int pairedSeeds = ParseAggregateInt(aggregate, "seeds=");
        int pairedWindows = ParseAggregateInt(aggregate, "windows=");
        if (pairedSeeds is < 32 or > 64 || pairedWindows != pairedSeeds * 16)
            throw new InvalidOperationException(
                $"Paired clutter sample contract failed: seeds={pairedSeeds}; windows={pairedWindows}.");
        string[] pairedMarkers =
        {
            "PASS\tPAIRED_RUN_CLEAN_REPEATABILITY_EXACT\t",
            "PASS\tPAIRED_RUN_EXOGENOUS_EVENTS_EXACT\t",
            "PASS\tPAIRED_CLUTTER_ATTRIBUTION\t",
            "PASS\tPAIRED_BURST_QUANTITY_CONSERVED\t",
            "PASS\tPAIRED_KEYED_PRODUCTION_BURST_APPLIED\t",
            "PASS\tPAIRED_PRODUCTION_BURST_HAUL_PRIORITY\t",
            "PASS\tPAIRED_HAULER_FAULT_AFTER_PHYSICAL_PICKUP\t",
            "PASS\tFLOOR_CLUTTER_ACCESS_EGRESS_ZERO\t",
            "PASS\tFLOOR_CLUTTER_RECOVERY_ZERO\t",
            "PASS\tRNG_CAUSAL_CONE_NO_CROSS_TALK\t"
        };
        foreach (string marker in pairedMarkers)
        {
            if (!pairedReport.Any(line => line.StartsWith(marker, StringComparison.Ordinal)))
                throw new InvalidOperationException(
                    "Paired clutter marker is missing: " + marker);
        }
        int expectedFaultArms = checked(pairedSeeds * 2);
        foreach (string exactFaultMarker in new[]
                 {
                     $"PASS\tPAIRED_KEYED_PRODUCTION_BURST_APPLIED\tarms={expectedFaultArms}",
                     $"PASS\tPAIRED_PRODUCTION_BURST_HAUL_PRIORITY\tarms={expectedFaultArms}",
                     $"PASS\tPAIRED_HAULER_FAULT_AFTER_PHYSICAL_PICKUP\tarms={expectedFaultArms}"
                 })
        {
            if (!pairedReport.Contains(exactFaultMarker))
                throw new InvalidOperationException(
                    "Paired production intervention count is missing: " + exactFaultMarker);
        }
        string pairedCsvPath = Path.GetFullPath(
            V27PairedClutterPlayModeVerifier.PairedCsvPath);
        string floorCsvPath = Path.GetFullPath(
            V27PairedClutterPlayModeVerifier.ClutterCsvPath);
        if (!File.Exists(pairedCsvPath) || !File.Exists(floorCsvPath))
            throw new InvalidOperationException(
                "Paired clutter CSV evidence is missing.");
        if (!string.Equals(
                ParseAggregateToken(aggregate, "pairedCsvSha256="),
                V27BalanceArtifactWriter.ComputeSha256(
                    V27PairedClutterPlayModeVerifier.PairedCsvPath),
                StringComparison.Ordinal)
            || !string.Equals(
                ParseAggregateToken(aggregate, "floorCsvSha256="),
                V27BalanceArtifactWriter.ComputeSha256(
                    V27PairedClutterPlayModeVerifier.ClutterCsvPath),
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Paired clutter CSV digest does not match the completed run.");
        string[] pairedRows = File.ReadAllLines(pairedCsvPath, Encoding.UTF8);
        if (pairedRows.Length != pairedWindows + 1
            || pairedRows.Skip(1).Any(line =>
                !line.Contains(",true,", StringComparison.Ordinal)))
            throw new InvalidOperationException(
                $"Paired clutter CSV contract failed: rows={pairedRows.Length - 1}.");

        string randomManifestPath = Path.GetFullPath(
            V27RandomStreamManifestDebugScenarios.ReportPath);
        string currentRandomManifest = V27RandomStreamManifestDebugScenarios.RunAll()
            .TrimEnd('\r', '\n');
        if (!File.Exists(randomManifestPath)
            || !string.Equals(
                File.ReadAllText(randomManifestPath, Encoding.UTF8).TrimEnd('\r', '\n'),
                currentRandomManifest,
                StringComparison.Ordinal))
            throw new InvalidOperationException(
                "Random-stream manifest is missing or does not match current source.");

        return new CapacityEvidenceSummary(1536, pairedSeeds, pairedWindows);
    }

    private static DateTime LatestSource(IEnumerable<string> sourcePaths) =>
        sourcePaths
            .Select(Path.GetFullPath)
            .Select(path => File.Exists(path)
                ? File.GetLastWriteTimeUtc(path)
                : throw new FileNotFoundException("V27 evidence source is missing.", path))
            .Max();

    private static int ParseAggregateInt(string line, string key)
    {
        int start = line.IndexOf(key, StringComparison.Ordinal);
        if (start < 0)
            throw new InvalidOperationException("Aggregate key is missing: " + key);
        start += key.Length;
        int end = line.IndexOf(';', start);
        if (end < 0 || !int.TryParse(
                line.Substring(start, end - start),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out int value))
            throw new InvalidOperationException("Aggregate value is invalid: " + key);
        return value;
    }

    private static string ParseAggregateToken(string line, string key)
    {
        int start = line.IndexOf(key, StringComparison.Ordinal);
        if (start < 0)
            throw new InvalidOperationException("Aggregate key is missing: " + key);
        start += key.Length;
        int end = line.IndexOf(';', start);
        if (end < 0 || end == start)
            throw new InvalidOperationException("Aggregate value is invalid: " + key);
        return line.Substring(start, end - start);
    }

    private readonly struct CapacityEvidenceSummary
    {
        public CapacityEvidenceSummary(
            int assetBackedLayouts,
            int pairedSeeds,
            int pairedWindows)
        {
            AssetBackedLayouts = assetBackedLayouts;
            PairedSeeds = pairedSeeds;
            PairedWindows = pairedWindows;
        }

        public int AssetBackedLayouts { get; }
        public int PairedSeeds { get; }
        public int PairedWindows { get; }
    }

    private static void RequireFreshEvidence(
        string relativePath,
        DateTime latestSource,
        IReadOnlyList<string> exactLines)
    {
        string path = Path.GetFullPath(relativePath);
        if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) < latestSource)
        {
            throw new InvalidOperationException(
                $"Dungeon-expansion evidence is missing or stale: {relativePath}.");
        }
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        string[] missing = exactLines
            .Where(marker => !lines.Contains(marker, StringComparer.Ordinal))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"Dungeon-expansion evidence is incomplete: {relativePath}; "
                + string.Join(",", missing));
        }
    }

    private static void RequireFreshEvidencePrefixes(
        string relativePath,
        DateTime latestSource,
        IReadOnlyList<string> requiredPrefixes)
    {
        string path = Path.GetFullPath(relativePath);
        if (!File.Exists(path) || File.GetLastWriteTimeUtc(path) < latestSource)
        {
            throw new InvalidOperationException(
                $"V27 evidence is missing or stale: {relativePath}.");
        }

        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        string[] missing = requiredPrefixes
            .Where(prefix => !lines.Any(line =>
                line.StartsWith(prefix, StringComparison.Ordinal)))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"V27 evidence prefixes are incomplete: {relativePath}; "
                + string.Join(",", missing));
        }
    }

    private static void RequireDigestEvidence(
        string relativePath,
        string sourceDigest,
        string artifactDigest,
        IReadOnlyList<string> exactLines)
    {
        string path = Path.GetFullPath(relativePath);
        if (!File.Exists(path))
            throw new InvalidOperationException(
                $"V27 digest-bound evidence is missing: {relativePath}.");
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        string[] required = exactLines
            .Concat(new[]
            {
                "sourceDigest=" + sourceDigest,
                "spatialCsvSha256=" + artifactDigest
            })
            .ToArray();
        string[] missing = required
            .Where(marker => !lines.Contains(marker, StringComparer.Ordinal))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"V27 digest-bound evidence is stale or incomplete: {relativePath}; "
                + string.Join(",", missing));
        }
    }

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        using SHA256 sha = SHA256.Create();
        byte[] bytes = sha.ComputeHash(stream);
        const string Digits = "0123456789abcdef";
        char[] output = new char[bytes.Length * 2];
        for (int index = 0; index < bytes.Length; index++)
        {
            output[index * 2] = Digits[bytes[index] >> 4];
            output[index * 2 + 1] = Digits[bytes[index] & 0xf];
        }
        return new string(output);
    }

    private static void RequireExactLine(
        IReadOnlyList<string> lines,
        string expected,
        int seed)
    {
        if (!lines.Contains(expected, StringComparer.Ordinal))
        {
            throw new InvalidOperationException(
                $"Daily-routine marker is missing: seed={seed}; marker={expected}.");
        }
    }

    private static double ParseEvidenceNumber(
        IReadOnlyList<string> lines,
        string prefix,
        int seed)
    {
        string line = lines.SingleOrDefault(value => value.StartsWith(
            prefix,
            StringComparison.Ordinal))
            ?? throw new InvalidOperationException(
                $"Daily-routine numeric marker is missing: seed={seed}; prefix={prefix}.");
        return double.Parse(
            line.Substring(prefix.Length),
            NumberStyles.Float,
            CultureInfo.InvariantCulture);
    }
}
#endif
